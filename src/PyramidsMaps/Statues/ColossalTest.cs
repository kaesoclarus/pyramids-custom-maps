using System;
using System.Collections.Generic;
using Pyramid.Build;
using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// The scene for the colossal test pyramid (base 282 m, centred at z −130): flattens the ground under it on a
    /// copy of the terrain (the game's own terrain asset is never edited), hides the scenery that would stand inside
    /// it (map decorations such as the obelisks, and the dunes' dressing), and takes the normal map's boundary
    /// walls down (the play area is widened to match). A pyramid too big for the map (the Million-Block Pyramid)
    /// also gets a new terrain tile south of it. Everything is put back when the world ends.
    /// </summary>
    internal static class ColossalTest
    {
        /// <summary>How far past the pyramid's edge the ground is level (front, other sides), then blends back.</summary>
        private const float FlatFront = 6f, FlatOther = 8f, Blend = 12f;

        private static readonly List<GameObject> Hidden = new List<GameObject>();
        private static Terrain _terrain;
        private static TerrainData _original, _copy;

        public static void Show()
        {
            var site = UnityEngine.Object.FindAnyObjectByType<PyramidSite>();
            var map = StatueWorld.Map;
            if (site == null || map == null) return;
            var def = StatueWorld.Definition;
            float half = def.baseSize * def.cellSize.x * 0.5f;
            var c = site.transform.position;
            // the level area, in world x/z
            var flat = Rect.MinMaxRect(c.x - half - FlatOther, c.z - half - FlatOther, c.x + half + FlatOther, c.z + half + FlatFront);

            HideObject(GameObject.Find("/Map/Boundaries"));
            int decor = 0, dressing = 0;
            foreach (var d in UnityEngine.Object.FindObjectsByType<Pyramid.World.MapDecor>())
                if (Inside(flat, d.transform.position, 2f)) { HideObject(d.gameObject); decor++; }
            var dress = GameObject.Find("/Environment/Dressing");
            if (dress != null) dressing = HideProps(dress.transform, flat);

            if (_copy == null)
            {
                try { Flatten(flat, c.y); }
                catch (Exception e) { Plugin.Log.LogError($"Colossal test: flattening failed: {e}"); RestoreTerrain(); }
            }
            Plugin.Log.LogInfo($"Colossal test: site at z {c.z:0}, {def.baseSize}-block base ({half * 2:0} m); hid {decor} decorations, " +
                               $"{dressing} scenery props and the boundary walls; ground level from x {flat.xMin:0}..{flat.xMax:0}, z {flat.yMin:0}..{flat.yMax:0}.");
        }

        public static void Hide()
        {
            foreach (var go in Hidden) if (go != null) go.SetActive(true);
            Hidden.Clear();
            RestoreTerrain();
        }

        private static void HideObject(GameObject go)
        {
            if (go == null || !go.activeSelf) return;
            go.SetActive(false);
            Hidden.Add(go);
        }

        private static bool Inside(Rect r, Vector3 p, float margin) =>
            p.x >= r.xMin - margin && p.x <= r.xMax + margin && p.z >= r.yMin - margin && p.z <= r.yMax + margin;

        /// <summary>Hides the props (the first objects down each branch that draw or collide) inside the area.</summary>
        private static int HideProps(Transform t, Rect flat)
        {
            int n = 0;
            for (int i = 0; i < t.childCount; i++)
            {
                var ch = t.GetChild(i);
                bool prop = ch.GetComponent<LODGroup>() != null || ch.GetComponent<Renderer>() != null || ch.GetComponent<Collider>() != null;
                if (prop)
                {
                    if (ch.gameObject.activeSelf && Inside(flat, ch.position, 2f)) { HideObject(ch.gameObject); n++; }
                }
                else n += HideProps(ch, flat);
            }
            return n;
        }

        // ---------- the ground ----------

        private static void Flatten(Rect flat, float groundY)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Terrain>())
                if (t != null && t.name == "Terrain" && t.gameObject.scene.IsValid()) { _terrain = t; break; }
            if (_terrain == null) { Plugin.Log.LogWarning("Colossal test: no terrain to flatten."); return; }
            _original = _terrain.terrainData;
            _copy = UnityEngine.Object.Instantiate(_original);
            _copy.name = _original.name + "_Colossal";
            var tpos = _terrain.transform.position;
            Level(_copy, tpos, flat, groundY);
            _terrain.terrainData = _copy;
            if (_terrain.TryGetComponent<TerrainCollider>(out var col)) col.terrainData = _copy;

            // A pyramid too big for the map: more ground south of it.
            if (flat.yMin - Blend < tpos.z)
            {
                AddSouthTile();
                Level(_south.terrainData, _south.transform.position, flat, groundY);
            }
        }

        /// <summary>Levels the area (plus a smooth blend back to the dunes) on one terrain's data.</summary>
        private static void Level(TerrainData data, Vector3 tpos, Rect flat, float groundY)
        {
            int res = data.heightmapResolution;
            var size = data.size;
            float step = size.x / (res - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((flat.xMin - Blend - tpos.x) / step), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((flat.xMax + Blend - tpos.x) / step), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((flat.yMin - Blend - tpos.z) / step), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((flat.yMax + Blend - tpos.z) / step), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            if (x1 <= x0 || z1 <= z0) return;   // the area misses this terrain
            var heights = data.GetHeights(x0, z0, w, h);
            float level = Mathf.Clamp01((groundY - tpos.y) / size.y);
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float wx = tpos.x + (x0 + i) * step, wz = tpos.z + (z0 + j) * step;
                    float dx = Mathf.Max(0f, Mathf.Max(flat.xMin - wx, wx - flat.xMax));
                    float dz = Mathf.Max(0f, Mathf.Max(flat.yMin - wz, wz - flat.yMax));
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d >= Blend) continue;
                    float k = d <= 0f ? 1f : 1f - Smooth(d / Blend);
                    heights[j, i] = Mathf.Lerp(heights[j, i], level, k);
                }
            data.SetHeights(x0, z0, heights);
        }

        // ---------- more ground to the south ----------

        /// <summary>How deep a band of the map's south dunes is mirrored, over and over, to make the new ground.</summary>
        private const float MirrorBand = 150f;

        private static Terrain _south;
        private static Terrain[] _neighbours;

        /// <summary>
        /// Adds a terrain tile the size of the map against its south edge (z below −300). Its ground is the map's
        /// own south dunes, mirrored at the seam (so the two meet without a step) and repeated every
        /// <see cref="MirrorBand"/> metres; same textures, no trees. Removed when the world ends.
        /// </summary>
        private static void AddSouthTile()
        {
            var data = UnityEngine.Object.Instantiate(_original);
            data.name = _original.name + "_South";
            float depth = data.size.z;

            // heights ([z, x]; row 0 is the tile's south edge, the last row is the seam)
            int res = data.heightmapResolution;
            var src = _original.GetHeights(0, 0, res, res);
            var dst = new float[res, res];
            for (int j = 0; j < res; j++)
            {
                int from = MirrorRow(j, res, depth);
                for (int i = 0; i < res; i++) dst[j, i] = src[from, i];
            }
            data.SetHeights(0, 0, dst);

            // textures
            int ares = data.alphamapResolution, layers = data.alphamapLayers;
            if (layers > 0)
            {
                var a = _original.GetAlphamaps(0, 0, ares, ares);
                var b = new float[ares, ares, layers];
                for (int j = 0; j < ares; j++)
                {
                    int from = MirrorRow(j, ares, depth);
                    for (int i = 0; i < ares; i++)
                        for (int l = 0; l < layers; l++) b[j, i, l] = a[from, i, l];
                }
                data.SetAlphamaps(0, 0, b);
            }

            // no trees or grass
            data.treeInstances = new TreeInstance[0];
            int dres = data.detailResolution;
            if (dres > 0)
            {
                var none = new int[dres, dres];
                for (int l = 0; l < data.detailPrototypes.Length; l++) data.SetDetailLayer(0, 0, l, none);
            }

            var go = new GameObject("Terrain_South (Pyramids Custom Maps)");
            go.layer = _terrain.gameObject.layer;
            go.tag = _terrain.gameObject.tag;
            go.transform.SetParent(_terrain.transform.parent, false);
            go.transform.position = _terrain.transform.position + new Vector3(0f, 0f, -depth);
            var t = go.AddComponent<Terrain>();
            t.terrainData = data;
            t.materialTemplate = _terrain.materialTemplate;
            t.heightmapPixelError = _terrain.heightmapPixelError;
            t.basemapDistance = _terrain.basemapDistance;
            t.drawInstanced = _terrain.drawInstanced;
            t.shadowCastingMode = _terrain.shadowCastingMode;
            t.reflectionProbeUsage = _terrain.reflectionProbeUsage;
            t.renderingLayerMask = _terrain.renderingLayerMask;
            t.groupingID = _terrain.groupingID;
            t.allowAutoConnect = false;
            var col = go.AddComponent<TerrainCollider>();
            col.terrainData = data;
            if (_terrain.TryGetComponent<TerrainCollider>(out var orig)) col.sharedMaterial = orig.sharedMaterial;

            _neighbours = new[] { _terrain.leftNeighbor, _terrain.topNeighbor, _terrain.rightNeighbor, _terrain.bottomNeighbor };
            _terrain.SetNeighbors(_neighbours[0], _neighbours[1], _neighbours[2], t);
            t.SetNeighbors(null, _terrain, null, null);
            _south = t;
            Plugin.Log.LogInfo($"Colossal test: added {data.size.x:0} x {depth:0} m of ground south of the map (z {go.transform.position.z:0} .. {go.transform.position.z + depth:0}).");
        }

        /// <summary>For a row of the south tile, the row of the map whose ground it copies.</summary>
        private static int MirrorRow(int row, int res, float depth)
        {
            float d = (1f - row / (float)(res - 1)) * depth;   // metres south of the seam
            float m = Mathf.PingPong(d, MirrorBand);            // mirrored into the band north of the seam
            return Mathf.Clamp(Mathf.RoundToInt(m / depth * (res - 1)), 0, res - 1);
        }

        private static void RemoveSouthTile()
        {
            if (_south == null) return;
            if (_terrain != null && _neighbours != null)
                _terrain.SetNeighbors(_neighbours[0], _neighbours[1], _neighbours[2], _neighbours[3]);
            var data = _south.terrainData;
            UnityEngine.Object.Destroy(_south.gameObject);
            if (data != null) UnityEngine.Object.Destroy(data);
            _south = null; _neighbours = null;
        }

        private static void RestoreTerrain()
        {
            RemoveSouthTile();
            if (_terrain != null && _original != null)
            {
                _terrain.terrainData = _original;
                if (_terrain.TryGetComponent<TerrainCollider>(out var col)) col.terrainData = _original;
            }
            if (_copy != null) UnityEngine.Object.Destroy(_copy);
            _terrain = null; _original = null; _copy = null;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}

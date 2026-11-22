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
    /// walls down (the play area is widened to match). Everything is put back when the world ends.
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

            int res = _copy.heightmapResolution;
            var size = _copy.size;
            var tpos = _terrain.transform.position;
            float step = size.x / (res - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((flat.xMin - Blend - tpos.x) / step), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((flat.xMax + Blend - tpos.x) / step), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((flat.yMin - Blend - tpos.z) / step), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((flat.yMax + Blend - tpos.z) / step), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var heights = _copy.GetHeights(x0, z0, w, h);
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
            _copy.SetHeights(x0, z0, heights);
            _terrain.terrainData = _copy;
            if (_terrain.TryGetComponent<TerrainCollider>(out var col)) col.terrainData = _copy;
        }

        private static void RestoreTerrain()
        {
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

using System;
using Pyramid.Build;
using PyramidsMaps.Shapes;
using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// Sobek's oval pool: a basin dug into a copy of the terrain (the game's own terrain asset is never edited)
    /// and a sheet of the river's own water in it. The floor is level with the ground he stands on, 0.6 m under the water;
    /// a low bank rises at the shore so the water's edge tucks into the ground. Shown while a Sobek world is
    /// active (including the main-menu preview), and fully undone when it ends.
    /// </summary>
    internal static class SobekPool
    {
        /// <summary>Water surface and the basin floor, in metres above the site. The floor is level with the ground
        /// Sobek stands on, so it's one even depth under the water.</summary>
        private const float WaterY = 0.6f, FloorY = 0f;
        /// <summary>The bank: how high it rises at the shore, how far in it slopes down, how far out it spreads.</summary>
        private const float BankY = 0.75f, BankIn = 6f, BankOut = 4f;

        private static GameObject _water;
        private static Terrain _terrain;
        private static TerrainData _original, _copy;

        public static void Show()
        {
            Hide();
            var site = UnityEngine.Object.FindAnyObjectByType<PyramidSite>();
            if (site == null) { Plugin.Log.LogWarning("Sobek pool: no build site found."); return; }
            // The classic site (Sobek worlds are classic worlds); this runs before the game moves the site there.
            var pos = site.ClassicPosition;
            var rot = site.transform.rotation;
            try { DigBasin(pos, rot); } catch (Exception e) { Plugin.Log.LogError($"Sobek pool: digging the basin failed: {e}"); RestoreTerrain(); }
            try { MakeWater(pos, rot); } catch (Exception e) { Plugin.Log.LogError($"Sobek pool: making the water failed: {e}"); }
        }

        public static void Hide()
        {
            if (_water != null) { UnityEngine.Object.Destroy(_water); _water = null; }
            RestoreTerrain();
        }

        // ---------- the water ----------

        private static void MakeWater(Vector3 sitePos, Quaternion siteRot)
        {
            var river = FindRiver();
            if (river == null) { Plugin.Log.LogWarning("Sobek pool: no river found to borrow its water from."); return; }

            const int seg = 160;
            var verts = new Vector3[seg + 1];
            var uvs = new Vector2[seg + 1];
            var tris = new int[seg * 3];
            verts[0] = Vector3.zero;
            uvs[0] = Uv(sitePos);
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var local = new Vector3(Mathf.Sin(a) * (float)SobekShape.PoolRX, 0f, (float)SobekShape.PoolCZ + Mathf.Cos(a) * (float)SobekShape.PoolRZ);
                verts[i + 1] = local;
                uvs[i + 1] = Uv(sitePos + siteRot * local);
                tris[i * 3] = 0; tris[i * 3 + 1] = i + 1; tris[i * 3 + 2] = (i + 1) % seg + 1;
            }
            // centre vertex at the oval's centre, not the site's
            verts[0] = new Vector3(0f, 0f, (float)SobekShape.PoolCZ);
            uvs[0] = Uv(sitePos + siteRot * verts[0]);

            var mesh = new Mesh { name = "SobekPoolWater" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _water = new GameObject("SobekPool");
            _water.layer = river.gameObject.layer;
            _water.transform.SetPositionAndRotation(sitePos + Vector3.up * WaterY, siteRot);
            _water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = _water.AddComponent<MeshRenderer>();
            // the river's own material: its scroller animates the same material, so the pool ripples with it
            mr.sharedMaterial = river.sharedMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = river.receiveShadows;
            Plugin.Log.LogInfo($"Sobek pool: water at y {WaterY} m, {SobekShape.PoolRX * 2:0} x {SobekShape.PoolRZ * 2:0} m, material '{river.sharedMaterial?.name}'.");
        }

        /// <summary>Texture coordinates from world position: one repeat every 17 m (the river is 17 m wide).</summary>
        private static Vector2 Uv(Vector3 world) => new Vector2(world.x / 17f, world.z / 17f);

        private static MeshRenderer FindRiver()
        {
            var go = GameObject.Find("/Environment/River");
            if (go != null && go.TryGetComponent<MeshRenderer>(out var mr)) return mr;
            foreach (var s in UnityEngine.Object.FindObjectsByType<Pyramid.Feel.TextureScroller>())
                if (s.name == "River" && s.TryGetComponent<MeshRenderer>(out mr)) return mr;
            return null;
        }

        // ---------- the basin ----------

        private static void DigBasin(Vector3 sitePos, Quaternion siteRot)
        {
            _terrain = FindTerrain();
            if (_terrain == null) { Plugin.Log.LogWarning("Sobek pool: no active terrain to dig the basin in."); return; }
            _original = _terrain.terrainData;
            _copy = UnityEngine.Object.Instantiate(_original);
            _copy.name = _original.name + "_Sobek";


            int res = _copy.heightmapResolution;
            var size = _copy.size;
            var tpos = _terrain.transform.position;
            float step = size.x / (res - 1);
            var inv = Quaternion.Inverse(siteRot);

            // only the samples around the oval
            float reach = (float)Math.Max(SobekShape.PoolRX, Math.Abs(SobekShape.PoolCZ) + SobekShape.PoolRZ) + BankOut + 2f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((sitePos.x - reach - tpos.x) / step), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((sitePos.x + reach - tpos.x) / step), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((sitePos.z - reach - tpos.z) / step), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((sitePos.z + reach - tpos.z) / step), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var heights = _copy.GetHeights(x0, z0, w, h);   // [z, x], 0..1 of size.y

            int changed = 0;
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    var world = new Vector3(tpos.x + (x0 + i) * step, 0f, tpos.z + (z0 + j) * step);
                    var local = inv * (world - sitePos);
                    float d = EdgeDistance(local.x, local.z);   // + inside the oval, − outside
                    float offset;
                    if (d >= BankIn) offset = FloorY;
                    else if (d >= 0f) offset = Mathf.Lerp(FloorY, BankY, Smooth(1f - d / BankIn));
                    else if (d > -BankOut) offset = Mathf.Lerp(BankY, 0f, Smooth(-d / BankOut));
                    else continue;

                    float worldY = tpos.y + heights[j, i] * size.y + offset;
                    // the site is level at its own height; shift relative to it
                    heights[j, i] = Mathf.Clamp01((worldY - tpos.y) / size.y);
                    changed++;
                }
            _copy.SetHeights(x0, z0, heights);
            _terrain.terrainData = _copy;
            if (_terrain.TryGetComponent<TerrainCollider>(out var col)) col.terrainData = _copy;
            Plugin.Log.LogInfo($"Sobek pool: shaped the basin ({changed:N0} terrain samples, floor {FloorY} m, bank {BankY} m).");
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

        /// <summary>
        /// The classic map's terrain ("Terrain"; the Grand one is "Terrain_Grand"). It may still be switched off
        /// at this point if the last world shown was Grand, so look at inactive ones too.
        /// </summary>
        private static Terrain FindTerrain()
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Terrain>())
                if (t != null && t.name == "Terrain" && t.gameObject.scene.IsValid()) return t;
            return null;
        }

        /// <summary>Roughly how far (metres) a site-local point is inside the oval's edge (negative: outside).</summary>
        private static float EdgeDistance(float x, float z)
        {
            float rx = (float)SobekShape.PoolRX, rz = (float)SobekShape.PoolRZ;
            float dz = z - (float)SobekShape.PoolCZ;
            float ex = x / rx, ez = dz / rz;
            float s = Mathf.Sqrt(ex * ex + ez * ez);
            if (s < 1e-4f) return Mathf.Min(rx, rz);
            // the oval's radius in this point's direction
            float len = Mathf.Sqrt(x * x + dz * dz);
            float c = x / len, sn = dz / len;
            float r = 1f / Mathf.Sqrt(c * c / (rx * rx) + sn * sn / (rz * rz));
            return (1f - s) * r;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}

using System.Collections.Generic;
using HarmonyLib;
using Pyramid.Build;
using Pyramid.Workers;
using Pyramid.World;
using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// The game treats everything inside the pyramid's square site as "on the pyramid". The cat's square is
    /// 129 m across but she is slim, so: a spot is on the cat only where her footprint is, and Shabti route
    /// around her real outline (her bounding rectangle) instead of the square.
    /// </summary>
    [HarmonyPatch]
    internal static class StatueFootprintPatches
    {
        /// <summary>Footprint columns within <paramref name="margin"/> of the point (site-local metres).</summary>
        [HarmonyPatch(typeof(PyramidPlacement), "InsideLocal")]
        [HarmonyPrefix]
        private static bool InsideLocal(PyramidLayout layout, Vector3 local, float margin, ref bool __result)
        {
            if (!StatueLayout.IsStatue(layout)) return true;
            var cs = layout.CellSize;
            int cx = Mathf.RoundToInt(local.x / cs.x), cz = Mathf.RoundToInt(local.z / cs.z);
            if (margin <= 0f)
            {
                __result = StatueWorld.BlocksGround(cx, cz);
                return false;
            }
            int r = Mathf.CeilToInt(margin / Mathf.Min(cs.x, cs.z));
            __result = false;
            for (int dx = -r; dx <= r && !__result; dx++)
                for (int dz = -r; dz <= r && !__result; dz++)
                {
                    if (!StatueWorld.BlocksGround(cx + dx, cz + dz)) continue;
                    // distance from the point to that column's square
                    float ex = Mathf.Max(0f, Mathf.Abs(local.x - (cx + dx) * cs.x) - 0.5f * cs.x);
                    float ez = Mathf.Max(0f, Mathf.Abs(local.z - (cz + dz) * cs.z) - 0.5f * cs.z);
                    if (ex * ex + ez * ez <= margin * margin) __result = true;
                }
            return false;
        }

        /// <summary>Does a (rotated) building footprint, grown by margin, touch her footprint?</summary>
        [HarmonyPatch(typeof(PyramidPlacement), nameof(PyramidPlacement.FootprintOverlaps))]
        [HarmonyPrefix]
        private static bool FootprintOverlaps(PyramidState pyramid, Vector3 position, float yaw, Vector3 size, float margin, ref bool __result)
        {
            if (pyramid == null || pyramid.Site == null || !StatueLayout.IsStatue(pyramid.Layout)) return true;
            __result = Overlaps(pyramid, position, yaw, size, margin);
            return false;
        }

        /// <summary>Pushes a building out of her footprint, away from her middle.</summary>
        [HarmonyPatch(typeof(PyramidPlacement), nameof(PyramidPlacement.OutsideFootprint))]
        [HarmonyPrefix]
        private static bool OutsideFootprint(PyramidState pyramid, Vector3 position, float yaw, Vector3 size, float clearance, ref Vector3 __result)
        {
            if (pyramid == null || pyramid.Site == null || !StatueLayout.IsStatue(pyramid.Layout)) return true;
            __result = position;
            if (!Overlaps(pyramid, position, yaw, size, clearance)) return false;
            var t = pyramid.Site.transform;
            var box = Box(pyramid.Layout);
            var local = t.InverseTransformPoint(position);
            var dir = new Vector2(local.x - box.center.x, local.z - box.center.y);
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
            dir.Normalize();
            for (float d = 0.5f; d <= 150f; d += 0.5f)
            {
                var p = t.TransformPoint(new Vector3(local.x + dir.x * d, local.y, local.z + dir.y * d));
                if (!Overlaps(pyramid, p, yaw, size, clearance)) { p.y = position.y; __result = p; break; }
            }
            return false;
        }

        private static bool Overlaps(PyramidState pyramid, Vector3 position, float yaw, Vector3 size, float margin)
        {
            var t = pyramid.Site.transform;
            var cs = pyramid.Layout.CellSize;
            var local = t.InverseTransformPoint(position);
            float rel = (yaw - t.eulerAngles.y) * Mathf.Deg2Rad;
            float c = Mathf.Abs(Mathf.Cos(rel)), s = Mathf.Abs(Mathf.Sin(rel));
            float ex = c * size.x * 0.5f + s * size.z * 0.5f + margin;
            float ez = s * size.x * 0.5f + c * size.z * 0.5f + margin;
            for (float x = local.x - ex; x <= local.x + ex + 0.01f; x += 0.5f)
                for (float z = local.z - ez; z <= local.z + ez + 0.01f; z += 0.5f)
                    if (StatueWorld.BlocksGround(Mathf.RoundToInt(x / cs.x), Mathf.RoundToInt(z / cs.z))) return true;
            return false;
        }

        // ---------- Shabti routes ----------

        private static string _boxFor;
        private static Rect _box; // site-local metres: x/z extents of her footprint

        private static Rect Box(PyramidLayout layout)
        {
            if (_boxFor == StatueWorld.Map?.Id) return _box;
            var v = StatueWorld.Shape;
            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            for (int x = -v.Radius; x <= v.Radius; x++)
                for (int z = -v.Radius; z <= v.Radius; z++)
                    if (StatueWorld.BlocksGround(x, z))
                    {
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
                    }
            var cs = layout.CellSize;
            _box = Rect.MinMaxRect((minX - 0.5f) * cs.x, (minZ - 0.5f) * cs.z, (maxX + 0.5f) * cs.x, (maxZ + 0.5f) * cs.z);
            _boxFor = StatueWorld.Map?.Id;
            return _box;
        }

        /// <summary>Like the game's Route (around the square's corners), around the cat's rectangle.</summary>
        [HarmonyPatch(typeof(ShabtiPaths), nameof(ShabtiPaths.Route))]
        [HarmonyPrefix]
        private static bool Route(PyramidState py, Vector3 from, Vector3 to, float margin, List<Vector3> path)
        {
            if (py == null || py.Site == null || !StatueLayout.IsStatue(py.Layout)) return true;
            var t = py.Site.transform;
            var box = Box(py.Layout);
            Vector2 a = Flat(t.InverseTransformPoint(from)), b = Flat(t.InverseTransformPoint(to));
            if (!Crosses(a, b, box) || box.Contains(a) || box.Contains(b)) { path.Add(to); return false; }

            float m = Mathf.Max(0.05f, margin);
            var c = new[]
            {
                new Vector2(box.xMax + m, box.yMax + m), new Vector2(box.xMax + m, box.yMin - m),
                new Vector2(box.xMin - m, box.yMin - m), new Vector2(box.xMin - m, box.yMax + m),
            };
            float best = float.MaxValue;
            int bi = -1, bj = -1;
            for (int i = 0; i < 4; i++)
            {
                if (Crosses(a, c[i], box) || Crosses(c[i], b, box)) continue;
                float d = Vector2.Distance(a, c[i]) + Vector2.Distance(c[i], b);
                if (d < best) { best = d; bi = i; bj = -1; }
            }
            for (int i = 0; i < 4; i++)
                foreach (int k in new[] { -1, 1 })
                {
                    int j = (i + k + 4) % 4;
                    if (Crosses(a, c[i], box) || Crosses(c[j], b, box)) continue;
                    float d = Vector2.Distance(a, c[i]) + Vector2.Distance(c[i], c[j]) + Vector2.Distance(c[j], b);
                    if (d < best) { best = d; bi = i; bj = j; }
                }
            float y = t.InverseTransformPoint(from).y;
            if (bi >= 0) path.Add(t.TransformPoint(new Vector3(c[bi].x, y, c[bi].y)));
            if (bj >= 0) path.Add(t.TransformPoint(new Vector3(c[bj].x, y, c[bj].y)));
            path.Add(to);
            return false;
        }

        private static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>Does the segment pass through the rectangle's interior (Liang–Barsky)?</summary>
        private static bool Crosses(Vector2 p, Vector2 q, Rect r)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = q - p;
            bool Clip(float den, float num)
            {
                if (Mathf.Abs(den) < 1e-6f) return num <= 0f;
                float tt = num / den;
                if (den < 0f) { if (tt > t1) return false; if (tt > t0) t0 = tt; }
                else { if (tt < t0) return false; if (tt < t1) t1 = tt; }
                return true;
            }
            const float e = 0.01f;
            if (!Clip(-d.x, p.x - (r.xMin + e)) || !Clip(d.x, (r.xMax - e) - p.x) ||
                !Clip(-d.y, p.y - (r.yMin + e)) || !Clip(d.y, (r.yMax - e) - p.y)) return false;
            return t0 < t1;
        }
    }
}

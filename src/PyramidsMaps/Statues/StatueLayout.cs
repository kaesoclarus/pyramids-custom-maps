using System.Collections.Generic;
using HarmonyLib;
using Pyramid.Build;
using Pyramid.Data;
using PyramidsMaps.Shapes;
using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// Builds a PyramidLayout from the cat's blocks: one game layer per block layer, each split into rectangular
    /// sections (at most <see cref="Tile"/> cells a side), front sections first.
    /// </summary>
    [HarmonyPatch]
    internal static class StatueLayout
    {
        /// <summary>Section size for the cat (the pyramid uses 12; her blocks are half the size).</summary>
        private const int Tile = 16;

        [System.ThreadStatic] private static PyramidDefinition _building;

        /// <summary>
        /// Layouts built for the cat: every cat-only patch checks this. Weak, so layouts the game drops (the menu
        /// rebuilds one per save card shown) can be collected.
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PyramidLayout, object> StatueLayouts =
            new System.Runtime.CompilerServices.ConditionalWeakTable<PyramidLayout, object>();

        private static readonly object Mark = new object();

        public static bool IsStatue(PyramidLayout layout) => layout != null && StatueLayouts.TryGetValue(layout, out _);

        private static readonly AccessTools.FieldRef<PyramidLayout, int> TotalCellsRef =
            AccessTools.FieldRefAccess<PyramidLayout, int>("<TotalCells>k__BackingField");

        [HarmonyPatch(typeof(PyramidLayout), MethodType.Constructor, typeof(PyramidDefinition))]
        [HarmonyPrefix]
        private static void BeforeCtor(PyramidDefinition def) => _building = def;

        [HarmonyPatch(typeof(PyramidLayout), MethodType.Constructor, typeof(PyramidDefinition))]
        [HarmonyPostfix]
        private static void AfterCtor() => _building = null;

        [HarmonyPatch(typeof(PyramidLayout), "BuildWhole")]
        [HarmonyPrefix]
        private static bool BuildWhole(PyramidLayout __instance)
        {
            var map = StatueWorld.MapOfDefinition(_building);
            if (map == null) return true;
            Build(__instance, StatueWorld.ShapeOf(map));
            StatueLayouts.Add(__instance, Mark);
            return false;
        }

        private static void Build(PyramidLayout layout, Voxels map)
        {
            int cell = 0;
            int layers = map.Height;
            var rects = new List<RectInt>();
            for (int y = 0; y < layers; y++)
            {
                rects.Clear();
                Decompose(map, y, Tile, rects);

                int frontZ = int.MinValue;
                foreach (var r in rects) frontZ = Mathf.Max(frontZ, r.yMax);
                rects.Sort((a, b) =>
                {
                    float da = Dist2(a, 0f, frontZ), db = Dist2(b, 0f, frontZ);
                    if (da != db) return da.CompareTo(db);
                    return a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
                });

                int firstSection = layout.Sections.Count, firstCell = cell;
                foreach (var r in rects)
                {
                    // RectInt here holds inclusive max in xMax/yMax (see Cut)
                    var s = new PyramidSection(layout.Sections.Count, 0, layout.Layers.Count, y, r.x, r.xMax, r.y, r.yMax, cell);
                    layout.Sections.Add(s);
                    cell += s.Count;
                }
                layout.Layers.Add(new PyramidLayer(layout.Layers.Count, 0, y, y, firstSection, layout.Sections.Count - firstSection, firstCell, cell - firstCell));
            }
            layout.Stages.Add(new PyramidStage(0, map.Size, 0, layout.Layers.Count, 0, cell));
            TotalCellsRef(layout) = cell;
            Plugin.Log.LogInfo($"Statue layout: {cell:N0} cells, {layout.Layers.Count} layers, {layout.Sections.Count} sections.");
        }

        /// <summary>
        /// Splits one layer's blocks into rectangles: greedy maximal row runs grown towards +z, then cut into
        /// pieces of at most <paramref name="tile"/> cells a side.
        /// </summary>
        private static void Decompose(Voxels map, int y, int tile, List<RectInt> into)
        {
            int R = map.Radius, n = map.Size;
            var used = new bool[n, n];
            bool Free(int x, int z) => map[x, y, z] && !used[x + R, z + R];

            for (int z = -R; z <= R; z++)
            {
                for (int x = -R; x <= R; x++)
                {
                    if (!Free(x, z)) continue;
                    int x1 = x;
                    while (x1 + 1 <= R && Free(x1 + 1, z)) x1++;
                    int z1 = z;
                    while (z1 + 1 <= R && RowFree(z1 + 1)) z1++;
                    bool RowFree(int zz) { for (int xx = x; xx <= x1; xx++) if (!Free(xx, zz)) return false; return true; }

                    for (int zz = z; zz <= z1; zz++)
                        for (int xx = x; xx <= x1; xx++)
                            used[xx + R, zz + R] = true;

                    Cut(x, x1, z, z1, tile, into);
                    x = x1;
                }
            }
        }

        private static void Cut(int x0, int x1, int z0, int z1, int tile, List<RectInt> into)
        {
            int w = x1 - x0 + 1, d = z1 - z0 + 1;
            int nx = Mathf.Max(1, Mathf.CeilToInt((float)w / tile)), nz = Mathf.Max(1, Mathf.CeilToInt((float)d / tile));
            int zs = z0;
            for (int j = 0; j < nz; j++)
            {
                int dz = d / nz + (j < d % nz ? 1 : 0);
                int xs = x0;
                for (int i = 0; i < nx; i++)
                {
                    int dx = w / nx + (i < w % nx ? 1 : 0);
                    into.Add(new RectInt(xs, zs, dx - 1, dz - 1)); // width/height stored as (size − 1) → xMax/yMax inclusive
                    xs += dx;
                }
                zs += dz;
            }
        }

        private static float Dist2(RectInt r, float ax, float az)
        {
            float cx = (r.x + r.xMax) * 0.5f, cz = (r.y + r.yMax) * 0.5f;
            return (cx - ax) * (cx - ax) + (cz - az) * (cz - az);
        }
    }
}

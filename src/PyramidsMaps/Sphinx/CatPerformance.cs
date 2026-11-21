using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Pyramid.Build;
using Pyramid.Player;
using UnityEngine;

namespace PyramidsMaps.Sphinx
{
    /// <summary>
    /// Per-layout lookups for the cat: each cell's coordinates and its six neighbours, so the "touches a laid
    /// block" check is a handful of array reads instead of searches.
    /// </summary>
    internal sealed class CatCells
    {
        private static readonly ConditionalWeakTable<PyramidLayout, CatCells> Table = new ConditionalWeakTable<PyramidLayout, CatCells>();

        public readonly int[] Y;
        public readonly int[] Neighbours; // 6 per cell, −1 where there's no cat block

        private CatCells(PyramidLayout layout)
        {
            int n = layout.TotalCells;
            Y = new int[n];
            Neighbours = new int[n * 6];
            var dirs = new[] { new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0),
                               new Vector3Int(0, -1, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
            for (int i = 0; i < n; i++)
            {
                var c = layout.CellCoord(i);
                Y[i] = c.y;
                for (int d = 0; d < 6; d++) Neighbours[i * 6 + d] = layout.CellIndex(c + dirs[d]);
            }
        }

        public static CatCells For(PyramidLayout layout) => Table.GetValue(layout, l => new CatCells(l));

        public bool Touches(PyramidProgress p, int cell)
        {
            if (cell < 0 || cell >= Y.Length) return false;
            if (Y[cell] == 0) return true;
            return NextToLaid(p, cell);
        }

        /// <summary>A laid block is directly beside, above or below this cell (ignores the ground rule).</summary>
        public bool NextToLaid(PyramidProgress p, int cell)
        {
            if (cell < 0 || cell >= Y.Length) return false;
            int b = cell * 6;
            for (int d = 0; d < 6; d++)
            {
                int nb = Neighbours[b + d];
                if (nb >= 0 && p.IsFilled(nb)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The player's aim scans every open section each frame. On the pyramid that's one layer; on the cat every
    /// section is open, so while the aim runs it only sees the sections within reach of the player.
    /// </summary>
    [HarmonyPatch]
    internal static class CatAimPerformance
    {
        [System.ThreadStatic] private static bool _filter;
        [System.ThreadStatic] private static Vector3 _feetLocal;
        [System.ThreadStatic] private static float _range;
        private static readonly List<int> Near = new List<int>();
        private static PyramidState _py;

        [HarmonyPatch(typeof(PlayerActions), "FindPlaceCell")]
        [HarmonyPrefix]
        private static void BeforeFindPlaceCell(PlayerActions __instance, float range)
        {
            if (_py == null) _py = Object.FindAnyObjectByType<PyramidState>();
            if (_py == null || _py.Site == null || !SphinxLayout.IsCat(_py.Layout)) return;
            _feetLocal = _py.Site.transform.InverseTransformPoint(__instance.transform.position);
            _range = range + 2f;
            _filter = true;
        }

        [HarmonyPatch(typeof(PlayerActions), "FindPlaceCell")]
        [HarmonyFinalizer]
        private static void AfterFindPlaceCell() => _filter = false;

        [HarmonyPatch(typeof(PyramidProgress), nameof(PyramidProgress.OpenSections), MethodType.Getter)]
        [HarmonyPostfix]
        private static void OpenSections(PyramidProgress __instance, ref IReadOnlyList<int> __result)
        {
            if (!_filter || !SphinxLayout.IsCat(__instance.Layout)) return;
            var layout = __instance.Layout;
            var cs = layout.CellSize;
            Near.Clear();
            foreach (int s in __result)
            {
                var sec = layout.Sections[s];
                // distance from the feet to the section's box (local metres)
                float dx = Mathf.Max(0f, Mathf.Max(sec.MinX * cs.x - 0.5f * cs.x - _feetLocal.x, _feetLocal.x - (sec.MaxX * cs.x + 0.5f * cs.x)));
                float dz = Mathf.Max(0f, Mathf.Max(sec.MinZ * cs.z - 0.5f * cs.z - _feetLocal.z, _feetLocal.z - (sec.MaxZ * cs.z + 0.5f * cs.z)));
                float dy = Mathf.Max(0f, Mathf.Abs(sec.Y * cs.y - _feetLocal.y) - cs.y);
                if (dx * dx + dz * dz + dy * dy <= _range * _range) Near.Add(s);
            }
            __result = Near;
        }
    }
}

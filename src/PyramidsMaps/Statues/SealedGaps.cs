using System.Collections.Generic;
using HarmonyLib;
using Pyramid.Build;
using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// Any-order building can wall in an empty spot: blocks get laid beside, above and below it until nothing is
    /// left to place it against, and the build can never be finished. Such a gap is filled in automatically.
    ///
    /// A gap is an empty block of the shape (or a group of them, touching) with no way out: no side and no top
    /// opens onto air outside the shape. An opening only below doesn't count either: a block more than a metre
    /// above your feet can't be laid, so a spot reachable only from underneath is as stuck as a sealed one.
    /// The host checks the empty blocks next to every block laid (by anyone: players, Shabti, machines), and the
    /// whole world once when it loads, so worlds that are already stuck get unstuck too. Statue-layout maps only;
    /// never the final brick.
    /// </summary>
    [HarmonyPatch]
    internal sealed class SealedGaps : MonoBehaviour
    {
        private static readonly List<int> Laid = new List<int>();
        private static bool _scanAll;

        [HarmonyPatch(typeof(PyramidProgress), nameof(PyramidProgress.Fill), typeof(int))]
        [HarmonyPostfix]
        private static void AfterFill(PyramidProgress __instance, int cell)
        {
            if (StatueLayout.IsStatue(__instance.Layout)) Laid.Add(cell);
        }

        [HarmonyPatch(typeof(PyramidState), nameof(PyramidState.ServerLoad), typeof(ulong[]), typeof(int[]), typeof(int[]))]
        [HarmonyPostfix]
        private static void AfterLoad() => _scanAll = true;

        /// <summary>Testing: don't fill gaps found after blocks are laid (the scan on loading still runs).</summary>
        private static bool _paused;

        /// <summary>
        /// DevTools test: lays the six blocks around an empty block of the shape, walling it in. Normally it's filled
        /// at once; with <paramref name="leaveIt"/> it stays until the world is loaded again (tests the load scan).
        /// </summary>
        public static void DevMakeGap(bool leaveIt)
        {
            var py = FindAnyObjectByType<PyramidState>();
            var p = py != null ? py.Progress : null;
            if (p == null || !py.IsServerInitialized || !StatueLayout.IsStatue(p.Layout))
            {
                Plugin.Log.LogWarning("Sealed gaps test: needs the host in a statue-map world.");
                return;
            }
            var cells = StatueCells.For(p.Layout);
            for (int c = 0; c < p.Layout.TotalCells; c++)
            {
                if (cells.Y[c] < 1 || p.IsFilled(c) || py.IsCapstone(c)) continue;
                bool ok = true;
                for (int d = 0; d < 6 && ok; d++) { int nb = cells.Neighbours[c * 6 + d]; ok = nb >= 0 && !py.IsCapstone(nb); }
                if (!ok) continue;
                var walls = new List<int>();
                for (int d = 0; d < 6; d++) walls.Add(cells.Neighbours[c * 6 + d]);
                _paused = leaveIt;
                py.ServerFillCells(walls, -1);
                Plugin.Log.LogWarning($"Sealed gaps test: walled in the empty block {p.Layout.CellCoord(c)} (cell {c}) at {py.CellWorldCenter(c)}" +
                                      (leaveIt ? "; it stays empty until the world is loaded again." : "; it should be filled next."));
                return;
            }
            Plugin.Log.LogWarning("Sealed gaps test: no empty block with six neighbours in the shape left.");
        }

        private PyramidState _py;
        private readonly List<int> _pocket = new List<int>();
        private readonly List<int> _fill = new List<int>();
        private readonly Queue<int> _queue = new Queue<int>();
        private int[] _flood;     // per cell: the flood that last visited it
        private int[] _settled;   // per cell: the round in which it was found open (or walled in and queued to fill)
        private int _floodId, _round;

        private void Update()
        {
            if (Laid.Count == 0 && !_scanAll) return;
            if (_py == null) _py = FindAnyObjectByType<PyramidState>();
            var p = _py != null ? _py.Progress : null;
            if (p == null || !_py.IsServerInitialized || !StatueLayout.IsStatue(p.Layout)) { Laid.Clear(); _scanAll = false; return; }

            var cells = StatueCells.For(p.Layout);
            int n = p.Layout.TotalCells;
            if (_flood == null || _flood.Length != n) { _flood = new int[n]; _settled = new int[n]; _floodId = _round = 0; }
            if (++_round == int.MaxValue) { System.Array.Clear(_settled, 0, n); _round = 1; }
            _fill.Clear();

            if (_paused && !_scanAll) { Laid.Clear(); return; }
            if (_scanAll)
            {
                _scanAll = false;
                _paused = false;
                for (int c = 0; c < n; c++) Consider(p, cells, c);
                if (_fill.Count > 0) Plugin.Log.LogInfo($"Sealed gaps: found {_fill.Count} walled-in empty block(s) in the loaded world.");
            }
            foreach (int c in Laid)
            {
                if (c < 0 || c >= n) continue;
                for (int d = 0; d < 6; d++) Consider(p, cells, cells.Neighbours[c * 6 + d]);
            }
            Laid.Clear();

            if (_fill.Count == 0) return;
            _py.ServerFillCells(_fill, -1);
            Plugin.Log.LogInfo($"Sealed gaps: filled {_fill.Count} walled-in empty block(s) that could no longer be reached " +
                               $"(first at {p.Layout.CellCoord(_fill[0])}).");
        }

        /// <summary>If this empty block is part of a walled-in gap, adds the whole gap to the fill list.</summary>
        private void Consider(PyramidProgress p, StatueCells cells, int start)
        {
            if (start < 0 || _settled[start] == _round || p.IsFilled(start)) return;
            if (++_floodId == int.MaxValue) { System.Array.Clear(_flood, 0, _flood.Length); _floodId = 1; }
            _pocket.Clear();
            _queue.Clear();
            _flood[start] = _floodId;
            _queue.Enqueue(start);
            bool open = false;
            while (_queue.Count > 0 && !open)
            {
                int c = _queue.Dequeue();
                _pocket.Add(c);
                for (int d = 0; d < 6 && !open; d++)
                {
                    int nb = cells.Neighbours[c * 6 + d];
                    if (nb < 0)
                    {
                        // outside the shape: air, except below (the ground under layer 0, or a drop you can't build up into)
                        if (d != 3) open = true;   // 3 = down (see StatueCells' directions)
                        continue;
                    }
                    if (p.IsFilled(nb) || _flood[nb] == _floodId) continue;
                    if (_settled[nb] == _round) { open = true; continue; }   // joins a region already found open
                    _flood[nb] = _floodId;
                    _queue.Enqueue(nb);
                }
            }
            // An open region is usually big: stop as soon as it's known to be open, and remember every block seen so
            // far as open, so later checks this round that run into them stop at once.
            foreach (int c in _pocket) _settled[c] = _round;
            if (open)
            {
                foreach (int c in _queue) _settled[c] = _round;
                return;
            }
            foreach (int c in _pocket) if (!_py.IsCapstone(c)) _fill.Add(c);
        }
    }
}

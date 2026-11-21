using System;
using System.Collections.Generic;
using System.Linq;
using Pyramid.Data;
using Pyramid.Workers;
using Pyramid.Heat;
using PyramidsMaps.Shapes;
using UnityEngine;

namespace PyramidsMaps.Sphinx
{
    /// <summary>
    /// State of the Sphinx map for the world being hosted: the shape, its PyramidDefinition (a scaled copy of the
    /// classic one) and the brick gates scaled to the Sphinx's size while a Sphinx world runs.
    /// </summary>
    internal static class SphinxWorld
    {
        public const string MapId = "sphinx";

        public static bool Active { get; private set; }

        private static Voxels _shape;
        public static Voxels Shape => _shape ?? (_shape = SphinxShape.Build());

        private static int[,] _columnTop;

        /// <summary>Top block of the cat's column + 1 (0 = no blocks there). Cached.</summary>
        public static int ColumnTop(int x, int z)
        {
            var v = Shape;
            if (_columnTop == null)
            {
                _columnTop = new int[v.Size, v.Size];
                for (int cx = -v.Radius; cx <= v.Radius; cx++)
                    for (int cz = -v.Radius; cz <= v.Radius; cz++)
                        _columnTop[cx + v.Radius, cz + v.Radius] = v.ColumnTop(cx, cz);
            }
            if (x < -v.Radius || x > v.Radius || z < -v.Radius || z > v.Radius) return 0;
            return _columnTop[x + v.Radius, z + v.Radius];
        }

        /// <summary>The Sphinx's PyramidDefinition; the layout patch recognises it by reference.</summary>
        public static PyramidDefinition Definition { get; private set; }

        /// <summary>Sphinx bricks / classic pyramid bricks.</summary>
        public static float Scale { get; private set; } = 1f;

        private static readonly List<Action> Undo = new List<Action>();

        public static void Activate(PyramidDefinition classic, int classicCells)
        {
            if (Active) return;
            if (classic == null) { Plugin.Log.LogError("Sphinx: no classic PyramidDefinition to copy."); return; }

            int cells = Shape.CellCount;
            Scale = classicCells > 0 ? (float)cells / classicCells : 1f;
            if (Definition == null) Definition = MakeDefinition(classic, cells, Shape.Height);

            ScaleBrickGates();
            AdjustPlaceRule();
            Active = true;
            Plugin.Log.LogInfo($"Sphinx world on: {cells:N0} blocks, {Shape.Height} layers, brick gates x{Scale:0.###}.");
        }

        public static void Deactivate()
        {
            if (!Active) return;
            for (int i = Undo.Count - 1; i >= 0; i--)
            {
                try { Undo[i](); } catch (Exception e) { Plugin.Log.LogError($"Sphinx undo failed: {e}"); }
            }
            Undo.Clear();
            Active = false;
            Plugin.Log.LogInfo("Sphinx world off: brick gates restored.");
        }

        // ---------- definition ----------

        private static PyramidDefinition MakeDefinition(PyramidDefinition classic, int cells, int layers)
        {
            var d = UnityEngine.Object.Instantiate(classic);
            d.name = "PyramidDefinitionSphinx";
            d.hideFlags = HideFlags.DontUnloadUnusedAsset;
            // The cat's blocks: half a pyramid block each way, for smooth curves
            d.cellSize = new Vector3((float)SphinxShape.CellW, (float)SphinxShape.CellH, (float)SphinxShape.CellW);
            d.baseSize = Shape.Size;
            float s = Scale;

            // Milestones: same rhythm, scaled to the Sphinx, strictly rising and below the total
            var ms = new List<int>();
            foreach (int m in classic.milestones ?? new int[0])
            {
                int v = Mathf.Max(1, Mathf.RoundToInt(m * s));
                if (v >= cells) break;
                if (ms.Count == 0 || v > ms[ms.Count - 1]) ms.Add(v);
            }
            d.milestones = ms.ToArray();

            // Brick value per layer: the classic curve stretched over the Sphinx's layers, the top one is the capstone
            var src = classic.brickValueByLayer ?? new int[0];
            if (src.Length > 0)
            {
                var vals = new int[layers];
                for (int i = 0; i < layers; i++)
                {
                    float t = layers > 1 ? (float)i / (layers - 1) : 1f;
                    vals[i] = src[Mathf.Clamp(Mathf.RoundToInt(t * (src.Length - 1)), 0, src.Length - 1)];
                }
                d.brickValueByLayer = vals;
            }

            // Layer titles: same names, at the matching height of the Sphinx
            int classicLayers = Mathf.Max(1, (classic.baseSize + 1) / 2);
            if (d.layerTitles != null)
                for (int i = 0; i < d.layerTitles.Length; i++)
                {
                    var t = d.layerTitles[i];
                    t.layer = Mathf.Clamp(Mathf.RoundToInt((float)t.layer * (layers - 1) / Mathf.Max(1, classicLayers - 1)), 0, layers - 1);
                    d.layerTitles[i] = t;
                }

            Plugin.Log.LogInfo($"Sphinx definition: {d.milestones.Length} milestones (first {string.Join(", ", d.milestones.Take(5))} ...), " +
                               $"brick values [{string.Join(",", d.brickValueByLayer ?? new int[0])}].");
            return d;
        }

        // ---------- place rule ----------

        /// <summary>
        /// The game lets a block's base be at most 1 m above your feet: right for 2.5 m pyramid layers, but the
        /// cat's layers are 1.25 m, so that rule wouldn't let you stack on a brick you've just laid. Allow one
        /// cat layer above your feet.
        /// </summary>
        private static void AdjustPlaceRule()
        {
            float rise = (float)SphinxShape.CellH + 0.05f;
            foreach (var t in Resources.FindObjectsOfTypeAll<GameTuning>())
            {
                float original = t.placeMaxRise;
                if (original >= rise) continue;
                t.placeMaxRise = rise;
                Undo.Add(() => t.placeMaxRise = original);
            }
        }

        // ---------- brick gates ----------

        private static void ScaleBrickGates()
        {
            int items = 0, levels = 0, others = 0;
            foreach (var item in Resources.FindObjectsOfTypeAll<ItemDefinition>())
            {
                if (item.unlockAtBricks > 0) { ScaleGate(item, item.unlockAtBricks, v => item.unlockAtBricks = v); items++; }
                if (item.levelUnlockAtBricks != null && item.levelUnlockAtBricks.Length > 0) { ScaleArray(item.levelUnlockAtBricks); levels++; }
            }
            foreach (var mon in Resources.FindObjectsOfTypeAll<MonumentDefinition>())
                if (mon.unlockAtBricks > 0) { ScaleGate(mon, mon.unlockAtBricks, v => mon.unlockAtBricks = v); others++; }
            foreach (var entry in FindTeamUpgradeEntries())
                if (entry.levelUnlockAtBricks != null && entry.levelUnlockAtBricks.Length > 0) { ScaleArray(entry.levelUnlockAtBricks); levels++; }
            foreach (var t in Resources.FindObjectsOfTypeAll<ShabtiTuning>())
            {
                // no Stone Ramps on the cat: the bubble says what to do instead
                string original = t.bubbleNeedRamps;
                t.bubbleNeedRamps = CatShabtiFixes.NeedPostUpBubble;
                Undo.Add(() => t.bubbleNeedRamps = original);
            }
            foreach (var t in Resources.FindObjectsOfTypeAll<ShabtiRosterTuning>())
                if (t.marketUnlockAtBricks > 0) { ScaleGate(t, t.marketUnlockAtBricks, v => t.marketUnlockAtBricks = v); others++; }
            foreach (var t in Resources.FindObjectsOfTypeAll<HeatTuning>())
                if (t.startsAtBricks > 0) { ScaleGate(t, t.startsAtBricks, v => t.startsAtBricks = v); others++; }
            Plugin.Log.LogInfo($"Sphinx: scaled {items} blueprint unlocks, {levels} upgrade-level lists, {others} other gates.");
        }

        private static void ScaleGate(object owner, long original, Action<long> set)
        {
            set(Mathf.Max(1, Mathf.RoundToInt(original * Scale)));
            Undo.Add(() => set(original));
        }

        private static void ScaleArray(long[] a)
        {
            var original = (long[])a.Clone();
            for (int i = 0; i < a.Length; i++)
                if (a[i] > 0) a[i] = Math.Max(1L, (long)Math.Round(a[i] * (double)Scale));
            Undo.Add(() => Array.Copy(original, a, a.Length));
        }

        /// <summary>TeamUpgradeEntry lives in lists on some ScriptableObject; find them by reflection.</summary>
        private static IEnumerable<TeamUpgradeEntry> FindTeamUpgradeEntries()
        {
            var seen = new HashSet<TeamUpgradeEntry>();
            foreach (var so in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                var type = so.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith("Pyramid")) continue;
                foreach (var f in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                {
                    if (f.GetValue(so) is IEnumerable<TeamUpgradeEntry> list)
                        foreach (var e in list)
                            if (e != null && seen.Add(e)) yield return e;
                }
            }
        }
    }
}

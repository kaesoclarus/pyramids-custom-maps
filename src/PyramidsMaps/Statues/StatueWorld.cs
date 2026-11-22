using System;
using System.Collections.Generic;
using System.Linq;
using Pyramid.Data;
using Pyramid.Workers;
using Pyramid.Heat;
using PyramidsMaps.Maps;
using PyramidsMaps.Shapes;
using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// The statue map of the world being shown or hosted: which map it is, its shape, its PyramidDefinition (a
    /// scaled copy of the classic one), and the brick gates scaled to the statue's size while it runs.
    /// </summary>
    internal static class StatueWorld
    {
        /// <summary>The active map, or null for the game's own pyramid.</summary>
        public static MapRegistry.MapInfo Map { get; private set; }

        public static bool Active => Map != null;

        /// <summary>Per map, built once: the shape, its column heights and its definition.</summary>
        private sealed class MapData
        {
            public Voxels Shape;
            public int[,] ColumnTop;
            public PyramidDefinition Definition;
        }

        private static readonly Dictionary<string, MapData> Data = new Dictionary<string, MapData>();

        private static MapData DataOf(MapRegistry.MapInfo map)
        {
            if (!Data.TryGetValue(map.Id, out var d))
            {
                var v = map.Build();
                d = new MapData { Shape = v, ColumnTop = new int[v.Size, v.Size] };
                for (int cx = -v.Radius; cx <= v.Radius; cx++)
                    for (int cz = -v.Radius; cz <= v.Radius; cz++)
                        d.ColumnTop[cx + v.Radius, cz + v.Radius] = v.ColumnTop(cx, cz);
                Data[map.Id] = d;
            }
            return d;
        }

        /// <summary>The active statue's blocks (null when no statue map is active).</summary>
        public static Voxels Shape => Active ? DataOf(Map).Shape : null;

        /// <summary>A map's blocks.</summary>
        public static Voxels ShapeOf(MapRegistry.MapInfo map) => DataOf(map).Shape;

        /// <summary>Top block of the active statue's column + 1 (0 = no blocks there, or no statue).</summary>
        public static int ColumnTop(int x, int z)
        {
            if (!Active) return 0;
            var d = DataOf(Map);
            int r = d.Shape.Radius;
            if (x < -r || x > r || z < -r || z > r) return 0;
            return d.ColumnTop[x + r, z + r];
        }

        /// <summary>The active statue's PyramidDefinition; the layout patch recognises it by reference.</summary>
        public static PyramidDefinition Definition => Active ? DataOf(Map).Definition : null;

        /// <summary>The statue map a PyramidDefinition was made for, or null.</summary>
        public static MapRegistry.MapInfo MapOfDefinition(PyramidDefinition def)
        {
            if (def == null) return null;
            foreach (var m in MapRegistry.All)
                if (Data.TryGetValue(m.Id, out var d) && d.Definition == def) return m;
            return null;
        }

        /// <summary>Statue bricks / classic pyramid bricks.</summary>
        public static float Scale { get; private set; } = 1f;

        private static readonly List<Action> Undo = new List<Action>();

        public static void Activate(MapRegistry.MapInfo map, PyramidDefinition classic, int classicCells)
        {
            if (map == null) { Deactivate(); return; }
            if (Map == map) return;
            Deactivate();
            if (classic == null) { Plugin.Log.LogError($"{map.Id}: no classic PyramidDefinition to copy."); return; }

            var data = DataOf(map);
            int cells = data.Shape.CellCount;
            Scale = classicCells > 0 ? (float)cells / classicCells : 1f;
            Map = map;   // set first: the definition and the gates below read the active map
            if (data.Definition == null) data.Definition = MakeDefinition(classic, cells, data.Shape.Height);

            ScaleBrickGates();
            AdjustPlaceRule();
            try { map.OnActivate?.Invoke(); } catch (Exception e) { Plugin.Log.LogError($"{map.Id}: switching on failed: {e}"); }
            Plugin.Log.LogInfo($"Statue world on ({map.Id}): {cells:N0} blocks, {data.Shape.Height} layers, brick gates x{Scale:0.###}.");
        }

        public static void Deactivate()
        {
            if (!Active) return;
            var map = Map;
            try { map.OnDeactivate?.Invoke(); } catch (Exception e) { Plugin.Log.LogError($"{map.Id}: switching off failed: {e}"); }
            for (int i = Undo.Count - 1; i >= 0; i--)
            {
                try { Undo[i](); } catch (Exception e) { Plugin.Log.LogError($"Statue undo failed: {e}"); }
            }
            Undo.Clear();
            Map = null;
            Plugin.Log.LogInfo($"Statue world off ({map.Id}): brick gates restored.");
        }

        // ---------- definition ----------

        private static PyramidDefinition MakeDefinition(PyramidDefinition classic, int cells, int layers)
        {
            var d = UnityEngine.Object.Instantiate(classic);
            d.name = "PyramidDefinition" + char.ToUpperInvariant(Map.Id[0]) + Map.Id.Substring(1);
            d.hideFlags = HideFlags.DontUnloadUnusedAsset;
            // The statue's blocks: half a pyramid block each way, for smooth curves
            d.cellSize = new Vector3((float)Map.CellW, (float)Map.CellH, (float)Map.CellW);
            d.baseSize = Shape.Size;
            float s = Scale;

            // Milestones: same rhythm, scaled to the statue, strictly rising and below the total
            var ms = new List<int>();
            foreach (int m in classic.milestones ?? new int[0])
            {
                int v = Mathf.Max(1, Mathf.RoundToInt(m * s));
                if (v >= cells) break;
                if (ms.Count == 0 || v > ms[ms.Count - 1]) ms.Add(v);
            }
            d.milestones = ms.ToArray();

            // Brick value per layer: the classic curve stretched over the statue's layers, the top one is the capstone
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

            // Layer titles: same names, at the matching height of the statue
            int classicLayers = Mathf.Max(1, (classic.baseSize + 1) / 2);
            if (d.layerTitles != null)
                for (int i = 0; i < d.layerTitles.Length; i++)
                {
                    var t = d.layerTitles[i];
                    t.layer = Mathf.Clamp(Mathf.RoundToInt((float)t.layer * (layers - 1) / Mathf.Max(1, classicLayers - 1)), 0, layers - 1);
                    d.layerTitles[i] = t;
                }

            Plugin.Log.LogInfo($"{Map.Id} definition: {d.milestones.Length} milestones (first {string.Join(", ", d.milestones.Take(5))} ...), " +
                               $"brick values [{string.Join(",", d.brickValueByLayer ?? new int[0])}].");
            return d;
        }

        // ---------- place rule ----------

        /// <summary>
        /// The game lets a block's base be at most 1 m above your feet: right for 2.5 m pyramid layers, but the
        /// statue's layers are 1.25 m, so that rule wouldn't let you stack on a brick you've just laid. Allow one
        /// statue layer above your feet.
        /// </summary>
        private static void AdjustPlaceRule()
        {
            float rise = (float)Map.CellH + 0.05f;
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
                // no Stone Ramps on a statue: the bubble says what to do instead
                string original = t.bubbleNeedRamps;
                t.bubbleNeedRamps = StatueShabtiFixes.NeedPostUpBubble;
                Undo.Add(() => t.bubbleNeedRamps = original);
            }
            foreach (var t in Resources.FindObjectsOfTypeAll<ShabtiRosterTuning>())
                if (t.marketUnlockAtBricks > 0) { ScaleGate(t, t.marketUnlockAtBricks, v => t.marketUnlockAtBricks = v); others++; }
            foreach (var t in Resources.FindObjectsOfTypeAll<HeatTuning>())
                if (t.startsAtBricks > 0) { ScaleGate(t, t.startsAtBricks, v => t.startsAtBricks = v); others++; }
            Plugin.Log.LogInfo($"{Map.Id}: scaled {items} blueprint unlocks, {levels} upgrade-level lists, {others} other gates.");
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

using HarmonyLib;
using Pyramid.Build;
using Pyramid.Globe;
using PyramidsMaps.Maps;
using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// Shabti builders on the cat. They plan on the game's "builder plane": a square grid at one level where
    /// every square inside the pyramid counts as floor. On the cat:
    ///  - a square is floor only if a laid brick is under it (no walking on air; overhangs are left to players);
    ///  - a Work Post on the cat builds the level it stands on (the cat is built in any order, so not only the
    ///    lowest unfinished layer).
    /// A Work Post on the ground still builds the bottom layer, as on the pyramid.
    /// </summary>
    [HarmonyPatch]
    internal static class StatueShabtiPatches
    {
        [HarmonyPatch(typeof(PyramidState), nameof(PyramidState.FillBuilderField))]
        [HarmonyPostfix]
        private static void FillBuilderField(PyramidState __instance, int level, BuilderField field, bool __result)
        {
            if (!__result || field == null || level <= 0 || !StatueLayout.IsStatue(__instance.Layout)) return;
            var layout = __instance.Layout;
            var prog = __instance.Progress;
            int y = layout.Layers[level].Y;
            int r = field.Radius;
            for (int x = -r; x <= r; x++)
                for (int z = -r; z <= r; z++)
                {
                    int below = layout.CellIndex(new Vector3Int(x, y - 1, z));
                    if (below < 0 || !prog.IsFilled(below)) field.Set(x, z, 0, false);
                }
        }

        [HarmonyPatch(typeof(PyramidState), nameof(PyramidState.BuilderPlaneWalkable))]
        [HarmonyPostfix]
        private static void BuilderPlaneWalkable(PyramidState __instance, int level, int x, int z, ref bool __result)
        {
            if (!__result || level <= 0 || !StatueLayout.IsStatue(__instance.Layout)) return;
            var layout = __instance.Layout;
            int below = layout.CellIndex(new Vector3Int(x, layout.Layers[level].Y - 1, z));
            if (below < 0 || !__instance.Progress.IsFilled(below)) __result = false;
        }

        /// <summary>On the cat, the level a spot stands on is simply its height (any unfinished layer).</summary>
        [HarmonyPatch(typeof(PyramidState), nameof(PyramidState.BuilderLevelAt))]
        [HarmonyPrefix]
        private static bool BuilderLevelAt(PyramidState __instance, Vector3 world, float heightTolerance, float slackCells, ref int __result)
        {
            if (!StatueLayout.IsStatue(__instance.Layout) || __instance.Site == null) return true;
            __result = -1;
            if (__instance.Progress.Finished) return false;
            var layout = __instance.Layout;
            var local = __instance.Site.transform.InverseTransformPoint(world);
            int level = Mathf.RoundToInt(local.y / layout.CellSize.y);
            if (level < 0 || level >= layout.Layers.Count || __instance.Progress.IsLayerComplete(level)) return false;
            if (__instance.TryBuilderPlaneCoord(level, world, heightTolerance, slackCells, out _)) __result = level;
            return false;
        }
    }

    /// <summary>A modded cat isn't a pyramid: keep it off the game's online world rankings.</summary>
    [HarmonyPatch]
    internal static class StatueRankingPatches
    {
        [HarmonyPatch(typeof(PyramidRecord), nameof(PyramidRecord.Rankable), MethodType.Getter)]
        [HarmonyPostfix]
        private static void Rankable(PyramidRecord __instance, ref bool __result)
        {
            if (!__result) return;
            bool cat = MapRegistry.Find(WorldMaps.Get(__instance.slotId)) != null ||
                       (StatueWorld.Active && !string.IsNullOrEmpty(__instance.slotId) && __instance.slotId == Pyramid.Core.WorldSave.SessionSlot);
            if (cat) __result = false;
        }
    }
}

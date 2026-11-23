using HarmonyLib;
using Pyramid.Core;
using Pyramid.World;
using PyramidsMaps.Statues;
using UnityEngine;

namespace PyramidsMaps.Maps
{
    /// <summary>
    /// Lets the sledge lift climb a bigger pyramid map all the way up. The lift's Height upgrade tops out at
    /// 26 steps, the Classic pyramid's 26 layers; the game scales that by 43/26 in the Grand Pyramid
    /// (<see cref="SledgeLift.GrandHeightScale"/>). A pyramid map such as the Colossal Pyramid (71 layers) runs
    /// as a Classic world, so it gets the same treatment here: steps × layers / 26. Every player runs the mod,
    /// so host and clients agree on the number.
    /// </summary>
    [HarmonyPatch]
    internal static class LiftPatches
    {
        /// <summary>Layers in the Classic pyramid: what the Height upgrade's top level is sized for.</summary>
        private const float ClassicLayers = 26f;

        [HarmonyPatch(typeof(SledgeLift), nameof(SledgeLift.HeightLevels), MethodType.Getter)]
        [HarmonyPostfix]
        private static void ScaleForBigPyramid(ref int __result)
        {
            var map = StatueWorld.Map;
            if (map == null || map.IsStatue || map.PyramidBase <= 0) return;
            int layers = (map.PyramidBase + 1) / 2;
            if (layers <= ClassicLayers) return;
            float classic = WorldKinds.IsGrand ? __result / SledgeLift.GrandHeightScale : __result;
            __result = Mathf.RoundToInt(classic * layers / ClassicLayers);
        }

        private static readonly AccessTools.FieldRef<SledgeLiftVisual, Transform[]> RailSegments =
            AccessTools.FieldRefAccess<SledgeLiftVisual, Transform[]>("railSegments");

        /// <summary>
        /// The lift's track is a fixed set of rail pieces in its prefab, one per step, enough for the Grand
        /// Pyramid. On a taller pyramid the cage climbs past the last piece, so copy more pieces from the last
        /// one before the track is laid out; the game then places and scales them like its own.
        /// </summary>
        [HarmonyPatch(typeof(SledgeLiftVisual), nameof(SledgeLiftVisual.Layout))]
        [HarmonyPrefix]
        private static void EnoughRails(SledgeLiftVisual __instance, int levels)
        {
            ref Transform[] rails = ref RailSegments(__instance);
            int need = levels - 1;   // segment i joins stop i+1 to i+2
            if (rails == null || rails.Length == 0 || need <= rails.Length) return;
            Transform model = null;
            for (int i = rails.Length - 1; i >= 0 && model == null; i--) model = rails[i];
            if (model == null) return;
            int had = rails.Length;
            var more = new Transform[need];
            System.Array.Copy(rails, more, had);
            for (int i = had; i < need; i++)
            {
                var copy = Object.Instantiate(model, model.parent, false);
                copy.name = model.name + " (extra " + (i + 1) + ")";
                copy.gameObject.SetActive(false);
                more[i] = copy;
            }
            rails = more;
            Plugin.Log.LogInfo($"Sledge lift: added {need - had} rail pieces (now {need}) for a {levels}-step track.");
        }
    }
}

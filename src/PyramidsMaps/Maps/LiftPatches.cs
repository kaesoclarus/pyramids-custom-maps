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
    }
}

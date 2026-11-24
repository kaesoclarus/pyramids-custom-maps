using HarmonyLib;
using Pyramid.UI;
using PyramidsMaps.Statues;

namespace PyramidsMaps.Maps
{
    /// <summary>
    /// The game's HUD names "the Great Pyramid" in its milestone banners ("25% of the Great Pyramid"), the final
    /// "THE GREAT PYRAMID STANDS!", the top layer's "The Great Pyramid stands" and the progress line's "THE GREAT
    /// PYRAMID IS COMPLETE!". On a custom map those say the map's own name instead (<see cref="MapRegistry.MapInfo.Called"/>).
    /// </summary>
    [HarmonyPatch]
    internal static class HudTextPatches
    {
        private static readonly AccessTools.FieldRef<Hud, string> SectionBase = AccessTools.FieldRefAccess<Hud, string>("_sectionBase");

        /// <summary>The text with the Great Pyramid swapped for the map's name, in the same case.</summary>
        internal static string Rename(string s)
        {
            var map = StatueWorld.Map;
            if (map == null || string.IsNullOrEmpty(s) || s.IndexOf("REAT PYRAMID", System.StringComparison.OrdinalIgnoreCase) < 0) return s;
            string called = map.Called;
            string capital = called.Length > 0 ? char.ToUpperInvariant(called[0]) + called.Substring(1) : called;
            return s.Replace("THE GREAT PYRAMID", called.ToUpperInvariant())
                    .Replace("The Great Pyramid", capital)
                    .Replace("the Great Pyramid", called);
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.Banner), typeof(string), typeof(string), typeof(UnityEngine.Color), typeof(float))]
        [HarmonyPrefix]
        private static void BannerShort(ref string title, ref string subtitle)
        {
            title = Rename(title);
            subtitle = Rename(subtitle);
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.Banner), typeof(string), typeof(string), typeof(UnityEngine.Color), typeof(float),
            typeof(Pyramid.Audio.Sfx), typeof(float), typeof(float))]
        [HarmonyPrefix]
        private static void BannerWithSound(ref string title, ref string subtitle)
        {
            title = Rename(title);
            subtitle = Rename(subtitle);
        }

        [HarmonyPatch(typeof(Hud), "ApplySectionText")]
        [HarmonyPrefix]
        private static void SectionLine(Hud __instance)
        {
            ref string s = ref SectionBase(__instance);
            s = Rename(s);
        }
    }
}

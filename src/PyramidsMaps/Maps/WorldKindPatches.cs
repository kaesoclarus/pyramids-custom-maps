using System;
using System.IO;
using HarmonyLib;
using Pyramid.Build;
using Pyramid.Core;
using Pyramid.Data;
using PyramidsMaps.Statues;
using UnityEngine;

namespace PyramidsMaps.Maps
{
    /// <summary>
    /// Hooks the game's own Classic/Grand world switch. PyramidState.ApplyWorldKind runs on the host right after
    /// the save slot is chosen and before anything is restored from the save, so that's where a custom map is
    /// switched on (and switched off again when the world closes).
    /// </summary>
    [HarmonyPatch]
    internal static class WorldKindPatches
    {
        private static bool _stopping;

        private static readonly AccessTools.FieldRef<PyramidSite, PyramidDefinition> SiteDefinition =
            AccessTools.FieldRefAccess<PyramidSite, PyramidDefinition>("definition");
        private static readonly AccessTools.FieldRef<PyramidSite, PyramidLayout> SiteLayout =
            AccessTools.FieldRefAccess<PyramidSite, PyramidLayout>("_layout");

        [HarmonyPatch(typeof(PyramidState), nameof(PyramidState.OnStopNetwork))]
        [HarmonyPrefix]
        private static void BeforeStop() => _stopping = true;

        [HarmonyPatch(typeof(PyramidState), nameof(PyramidState.OnStopNetwork))]
        [HarmonyPostfix]
        private static void AfterStop() => _stopping = false;

        [HarmonyPatch(typeof(PyramidState), "ApplyWorldKind")]
        [HarmonyPrefix]
        private static void BeforeApplyWorldKind(PyramidState __instance, WorldKind kind)
        {
            try
            {
                var map = _stopping ? null : MapForThisSession(__instance, kind);
                if (map != null)
                {
                    var site = UnityEngine.Object.FindAnyObjectByType<PyramidSite>();
                    var classic = site != null ? site.ClassicDefinition : null;
                    int classicCells = classic != null ? new PyramidLayout(classic).TotalCells : 23426;
                    StatueWorld.Activate(map, classic, classicCells);
                }
                else
                {
                    StatueWorld.Deactivate();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Choosing the map failed, playing the normal world: {e}");
                StatueWorld.Deactivate();
            }
        }

        // ---------- multiplayer: telling joining players which statue it is ----------

        /// <summary>
        /// The world kind reaches clients as one synced byte (0 = pyramid, 1 = Grand; the game reads anything but
        /// 1 as a pyramid). For a statue world the host sends 0x40 | map index &lt;&lt; 4 | shape version: modded
        /// clients build that statue, unmodded clients fall back to the pyramid. The cat is map 0, so her byte is
        /// 0x40 | 5 as in 0.3.0 (which read the low 6 bits as her version); a 0.3.0 player joining another
        /// statue's world sees a version mismatch and is told to update.
        /// </summary>
        private const byte StatueFlag = 0x40;
        private static byte SyncByte(MapRegistry.MapInfo map) => (byte)(StatueFlag | ((map.SyncIndex & 0x3) << 4) | (map.ShapeVersion & 0xF));

        private static readonly AccessTools.FieldRef<PyramidState, FishNet.Object.Synchronizing.SyncVar<byte>> WorldKindVar =
            AccessTools.FieldRefAccess<PyramidState, FishNet.Object.Synchronizing.SyncVar<byte>>("_worldKind");

        private static byte RawKind(PyramidState s)
        {
            try { return WorldKindVar(s)?.Value ?? 0; } catch { return 0; }
        }

        [HarmonyPatch(typeof(PyramidState), "ApplyWorldKind")]
        [HarmonyPostfix]
        private static void AfterApplyWorldKind(PyramidState __instance, WorldKind kind)
        {
            try
            {
                if (!IsServer(__instance) || _stopping) return;
                var v = WorldKindVar(__instance);
                if (StatueWorld.Active && v != null && v.Value != SyncByte(StatueWorld.Map)) v.Value = SyncByte(StatueWorld.Map);   // syncs to clients
                Plugin.Log.LogInfo($"[hook] ApplyWorldKind({kind}) on the host: statue={StatueWorld.Map?.Id ?? "none"} synced={RawKind(__instance)} " +
                                   $"cells={__instance.Layout?.TotalCells} layers={__instance.Layout?.Layers.Count}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"ApplyWorldKind postfix: {e.Message}");
            }
        }

        // ---------- main-menu showcase ----------

        /// <summary>Slot the menu's showcase is about to draw (null: a crew pyramid from the globe, or none).</summary>
        private static string _showcaseSlot;
        private static bool _inShowcase;

        [HarmonyPatch(typeof(Pyramid.UI.SlotShowcase), "TryShow")]
        [HarmonyPrefix]
        private static void BeforeTryShow(Pyramid.UI.SlotShowcase __instance)
        {
            _inShowcase = true;
            try
            {
                var t = Traverse.Create(__instance);
                bool crew = t.Property("SelectedCrew").GetValue() != null;
                var selected = t.Property("Selected").GetValue<SlotInfo>();
                _showcaseSlot = crew ? null : selected?.Id;
                _showcaseIsNewPlot = !crew && selected == null;
            }
            catch (Exception e)
            {
                _showcaseSlot = null;
                _showcaseIsNewPlot = false;
                Plugin.Log.LogWarning($"Showcase slot unknown: {e.Message}");
            }
        }

        private static bool _showcaseIsNewPlot;

        [HarmonyPatch(typeof(Pyramid.UI.SlotShowcase), "TryShow")]
        [HarmonyFinalizer]
        private static void AfterTryShow() => _inShowcase = false;

        /// <summary>The empty plot has nothing to show; when new worlds will be cats, show the finished cat.</summary>
        [HarmonyPatch(typeof(PyramidState), nameof(PyramidState.LocalShowcase), typeof(ulong[]), typeof(int[]), typeof(string))]
        [HarmonyPrefix]
        private static void BeforeLocalShowcase(PyramidState __instance, ref ulong[] bits)
        {
            if (bits != null || !_inShowcase || !_showcaseIsNewPlot || !StatueWorld.Active) return;
            var layout = __instance.Layout;
            if (!Statues.StatueLayout.IsStatue(layout)) return;
            int n = layout.TotalCells;
            bits = new ulong[(n + 63) / 64];
            for (int i = 0; i < n; i++) bits[i >> 6] |= 1UL << (i & 63);
        }

        private static bool IsServer(PyramidState s)
        {
            // FishNet's IsServerInitialized throws before the object has been networked (e.g. on the main menu)
            try { return s != null && s.IsServerInitialized; } catch { return false; }
        }

        /// <summary>After the game picks Classic/Grand for the site, swap in the Sphinx definition if it's on.</summary>
        [HarmonyPatch(typeof(PyramidSite), nameof(PyramidSite.ApplyKind))]
        [HarmonyPostfix]
        private static void AfterSiteApplyKind(PyramidSite __instance, ref bool __result)
        {
            if (!StatueWorld.Active || StatueWorld.Definition == null) return;
            if (SiteDefinition(__instance) == StatueWorld.Definition) return;
            SiteDefinition(__instance) = StatueWorld.Definition;
            SiteLayout(__instance) = null;
            Physics.SyncTransforms();
            __result = true; // makes PyramidState rebuild its progress and raise LayoutSwapped
        }

        /// <summary>
        /// Which map the world being shown uses. Hosting: the slot's marker (new worlds get one from NewWorldMap).
        /// Main menu: the showcased slot's marker, and the empty plot previews NewWorldMap. Clients: stage 6.
        /// </summary>
        private static MapRegistry.MapInfo MapForThisSession(PyramidState state, WorldKind kind)
        {
            if (!IsServer(state))
            {
                if (_inShowcase)
                {
                    if (kind != WorldKind.Classic) return null;
                    if (_showcaseIsNewPlot) return MapRegistry.ForNewWorlds;
                    return MapRegistry.Find(WorldMaps.Get(_showcaseSlot));
                }

                // a client in someone else's world: the host's synced byte says which statue, if any
                byte raw = RawKind(state);
                if ((raw & 0xC0) != StatueFlag) return null;
                var hostMap = MapRegistry.BySyncIndex((raw >> 4) & 0x3);
                int hostShape = raw & 0xF;
                if (hostMap == null || hostShape != (hostMap.ShapeVersion & 0xF))
                {
                    Plugin.Log.LogError($"The host's world is a statue this mod doesn't have (map {(raw >> 4) & 0x3}, shape version {hostShape}; " +
                                        $"{(hostMap != null ? $"this mod's {hostMap.Id} is version {hostMap.ShapeVersion}" : "no such map here")}). " +
                                        "Both players need the same version of Pyramids Custom Maps.");
                    return null;
                }
                Plugin.Log.LogInfo($"Joined a {hostMap.Id} world: building its layout.");
                return hostMap;
            }
            string slot = WorldSave.SessionSlot;
            if (string.IsNullOrEmpty(slot)) return null;

            if (IsNewWorld())
            {
                string wanted = (Plugin.NewWorldMap.Value ?? "").Trim().ToLowerInvariant();
                var map = MapRegistry.Find(wanted);
                if (map != null && kind == WorldKind.Classic)
                {
                    WorldMaps.Set(slot, map);
                    return map;
                }
                if (wanted.Length > 0 && map == null)
                    Plugin.Log.LogWarning($"NewWorldMap = '{wanted}' isn't a known map (known: {MapRegistry.KnownIds}); starting a normal world.");
                WorldMaps.Clear(slot);
                return null;
            }

            string stored = WorldMaps.Get(slot);
            if (string.IsNullOrEmpty(stored)) return null;
            var storedMap = MapRegistry.Find(stored);
            if (storedMap == null)
            {
                Plugin.Log.LogError($"Slot {slot} is a '{stored}' world, a map this version of the mod doesn't have. Not applying it.");
                return null;
            }
            if (kind != WorldKind.Classic)
            {
                Plugin.Log.LogWarning($"Slot {slot} is a {stored} world but its save says {kind}; not applying the statue.");
                return null;
            }
            string shape = WorldMaps.GetValue(slot, "shape");
            if (shape != storedMap.ShapeVersion.ToString())
            {
                Plugin.Log.LogError($"Slot {slot} was started with an older {stored} shape ({shape ?? "1"}; this mod has {storedMap.ShapeVersion}). " +
                                    "Its saved bricks don't fit the new shape: start a new world. Not applying the statue.");
                return null;
            }
            return storedMap;
        }

        /// <summary>True when the slot being hosted has no save yet (the same test WorldSave makes before loading).</summary>
        private static bool IsNewWorld()
        {
            var ws = UnityEngine.Object.FindAnyObjectByType<WorldSave>();
            if (ws == null) return false;
            var t = Traverse.Create(ws);
            string file = t.Property("FilePath").GetValue<string>();
            bool hasBackup = t.Method("AnySavedBackup").GetValue<bool>();
            return !string.IsNullOrEmpty(file) && !File.Exists(file) && !hasBackup;
        }
    }
}

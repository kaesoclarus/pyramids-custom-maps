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
        private static readonly System.Diagnostics.Stopwatch _switchClock = new System.Diagnostics.Stopwatch();

        private static readonly AccessTools.FieldRef<PyramidSite, PyramidDefinition> SiteDefinition =
            AccessTools.FieldRefAccess<PyramidSite, PyramidDefinition>("definition");
        private static readonly AccessTools.FieldRef<PyramidSite, bool> FocusCaptured =
            AccessTools.FieldRefAccess<PyramidSite, bool>("_focusCaptured");
        private static readonly AccessTools.FieldRef<PyramidSite, float> ClassicFocusY =
            AccessTools.FieldRefAccess<PyramidSite, float>("_classicFocusY");
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
            _switchClock.Restart();
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
        /// 1 as a pyramid, so unmodded clients fall back to the pyramid). For a custom map the host sends:
        ///  - the cat and Sobek (codes 0 and 1, released in 0.3.0 / 0.4.0): 0x40 | code &lt;&lt; 4 | shape version,
        ///    as those versions expect, so their players still recognise them;
        ///  - every other map: 0xC0 | code (codes 2–63). 0.4.0 and older read that as a normal pyramid, so those
        ///    maps need everyone on the same version. The code changes whenever the shape does.
        /// </summary>
        private const byte StatueFlag = 0x40, NewLayout = 0x80;

        private static byte SyncByte(MapRegistry.MapInfo map) =>
            map.SyncCode <= 1
                ? (byte)(StatueFlag | (map.SyncCode << 4) | (map.ShapeVersion & 0xF))
                : (byte)(StatueFlag | NewLayout | (map.SyncCode & 0x3F));

        /// <summary>The map the host's byte names, or null; <paramref name="problem"/> says why not, when it's a custom map we can't match.</summary>
        private static MapRegistry.MapInfo MapOfSyncByte(byte raw, out string problem)
        {
            problem = null;
            if ((raw & StatueFlag) == 0) return null;
            if ((raw & NewLayout) != 0)
            {
                int code = raw & 0x3F;
                var m = MapRegistry.BySyncCode(code);
                if (m == null || code <= 1) problem = $"map code {code}, which this version of the mod doesn't have";
                return problem == null ? m : null;
            }
            int legacy = (raw >> 4) & 0x3, version = raw & 0xF;
            var map = MapRegistry.BySyncCode(legacy);
            if (map == null || legacy > 1) problem = $"map {legacy} (old layout), which this version of the mod doesn't have";
            else if ((map.ShapeVersion & 0xF) != version) problem = $"{map.Id} shape version {version}; this mod's is {map.ShapeVersion}";
            return problem == null ? map : null;
        }

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
            var map = StatueWorld.Map;
            var t = __instance.transform;
            bool moved = false;
            if (map.SiteZ.HasValue && Mathf.Abs(t.position.z - (float)map.SiteZ.Value) > 0.01f)
            {
                // a map with its own site position (the game moves it back for a normal world)
                t.position = new Vector3(t.position.x, t.position.y, (float)map.SiteZ.Value);
                moved = true;
            }
            if (SiteDefinition(__instance) == StatueWorld.Definition && !moved) return;
            SiteDefinition(__instance) = StatueWorld.Definition;
            SiteLayout(__instance) = null;
            if (!map.IsStatue)
            {
                // the menu camera looks at MenuFocus: lift it for a big pyramid, as the game does for the Grand one
                var focus = t.Find("MenuFocus");
                var def = StatueWorld.Definition;
                if (focus != null && !FocusCaptured(__instance))
                {
                    // the game remembers the normal height the first time it moves the focus: record it before we do
                    FocusCaptured(__instance) = true;
                    ClassicFocusY(__instance) = focus.localPosition.y;
                }
                if (focus != null)
                    focus.localPosition =new Vector3(focus.localPosition.x, Mathf.Round((def.baseSize + 1) / 2 * def.cellSize.y * 0.32f), focus.localPosition.z);
            }
            Physics.SyncTransforms();
            __result = true; // makes PyramidState rebuild its progress and raise LayoutSwapped
        }

        /// <summary>After the game finished switching worlds (walls and scenery set for the kind): the map's own changes.</summary>
        [HarmonyPatch(typeof(PyramidState), "ApplyWorldKind")]
        [HarmonyPostfix]
        private static void AfterApplyWorldKindScene()
        {
            if (!_stopping) StatueWorld.Applied();
            if (StatueWorld.Map != null && !StatueWorld.Map.IsStatue)
                Plugin.Log.LogInfo($"[perf] World switch to {StatueWorld.Map.Id} took {_switchClock.ElapsedMilliseconds:N0} ms (includes building the pyramid's layout).");
        }

        /// <summary>A map with its own walkable area (the normal one comes from PlayerTuning).</summary>
        [HarmonyPatch(typeof(Pyramid.Data.PlayerTuning), nameof(Pyramid.Data.PlayerTuning.PlayAreaMin), MethodType.Getter)]
        [HarmonyPostfix]
        private static void PlayAreaMin(ref Vector2 __result)
        {
            var a = StatueWorld.Map?.PlayArea;
            if (a.HasValue) __result = new Vector2(a.Value.minX, a.Value.minZ);
        }

        [HarmonyPatch(typeof(Pyramid.Data.PlayerTuning), nameof(Pyramid.Data.PlayerTuning.PlayAreaMax), MethodType.Getter)]
        [HarmonyPostfix]
        private static void PlayAreaMax(ref Vector2 __result)
        {
            var a = StatueWorld.Map?.PlayArea;
            if (a.HasValue) __result = new Vector2(a.Value.maxX, a.Value.maxZ);
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
                var hostMap = MapOfSyncByte(raw, out string problem);
                if (problem != null)
                {
                    Plugin.Log.LogError($"The host's world is a custom map this mod can't match ({problem}). " +
                                        "Both players need the same version of Pyramids Custom Maps.");
                    return null;
                }
                if (hostMap == null) return null;
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

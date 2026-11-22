using System.Collections.Generic;
using HarmonyLib;
using Pyramid.Core;
using Pyramid.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PyramidsMaps.Maps
{
    /// <summary>
    /// Main menu, empty plot: the game offers Pyramid and Grand Pyramid buttons. This adds a "Custom map" dropdown
    /// next to them listing every custom map (a copy of one of the game's own dropdowns, so it looks and sounds
    /// like the rest of the menu, and scrolls however many maps there are). Picking a map starts new worlds with
    /// it; picking Pyramid or Grand, or the dropdown's first entry, goes back to the game's own. The choice is kept
    /// in the NewWorldMap setting.
    /// </summary>
    [HarmonyPatch(typeof(SlotShowcase))]
    internal static class MenuMapChooser
    {
        private static readonly AccessTools.FieldRef<SlotShowcase, Button> ClassicRef = AccessTools.FieldRefAccess<SlotShowcase, Button>("kindClassicButton");
        private static readonly AccessTools.FieldRef<SlotShowcase, Button> GrandRef = AccessTools.FieldRefAccess<SlotShowcase, Button>("kindGrandButton");
        private static readonly AccessTools.FieldRef<SlotShowcase, TMP_Text> BodyRef = AccessTools.FieldRefAccess<SlotShowcase, TMP_Text>("plotBodyText");
        private static readonly AccessTools.FieldRef<SlotShowcase, Color> OffTintRef = AccessTools.FieldRefAccess<SlotShowcase, Color>("kindOffTint");
        private static readonly AccessTools.FieldRef<SlotShowcase, string> ShownKeyRef = AccessTools.FieldRefAccess<SlotShowcase, string>("_shownKey");

        /// <summary>The first entry: no custom map (the game's own Pyramid / Grand buttons apply).</summary>
        private const string NoneLabel = "Custom map...";

        private sealed class Chooser
        {
            public TMP_Dropdown Dropdown;
            public readonly List<MapRegistry.MapInfo> Maps = new List<MapRegistry.MapInfo>();   // index + 1 = dropdown option
        }

        private static readonly Dictionary<SlotShowcase, Chooser> Choosers = new Dictionary<SlotShowcase, Chooser>();

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AfterAwake(SlotShowcase __instance)
        {
            try
            {
                var classic = ClassicRef(__instance);
                var grand = GrandRef(__instance);
                if (classic == null || grand == null) { Plugin.Log.LogWarning("Map chooser: the empty plot has no Pyramid/Grand buttons; not adding the map list."); return; }

                // Pyramid / Grand: back to the game's own map
                classic.onClick.AddListener(() => Choose(__instance, null, WorldKind.Classic));
                grand.onClick.AddListener(() => Choose(__instance, null, WorldKind.Grand));

                var source = FindTemplateDropdown();
                if (source == null) { Plugin.Log.LogWarning("Map chooser: no dropdown in the game to copy; not adding the map list."); return; }

                var go = Object.Instantiate(source.gameObject, grand.transform.parent, false);
                go.name = "CustomMapDropdown";
                go.SetActive(true);
                var dd = go.GetComponent<TMP_Dropdown>();
                dd.onValueChanged = new TMP_Dropdown.DropdownEvent();   // drop the copied game listeners

                var chooser = new Chooser { Dropdown = dd };
                var options = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData(NoneLabel) };
                foreach (var map in MapRegistry.Visible)
                {
                    chooser.Maps.Add(map);
                    options.Add(new TMP_Dropdown.OptionData(map.Title));
                }
                dd.ClearOptions();
                dd.AddOptions(options);
                dd.SetValueWithoutNotify(0);
                dd.onValueChanged.AddListener(i =>
                {
                    if (i <= 0) Choose(__instance, null, WorldKind.Classic);
                    else Choose(__instance, chooser.Maps[i - 1], WorldKind.Classic);
                });

                FitListToOptions(dd, options.Count);
                go.AddComponent<WheelScroll>();
                Place((RectTransform)classic.transform, (RectTransform)grand.transform, (RectTransform)go.transform);
                Choosers[__instance] = chooser;
                Plugin.Log.LogInfo($"Map chooser: added a dropdown with {chooser.Maps.Count} map(s) to the empty plot (copied from '{source.name}').");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Map chooser failed: {e}");
            }
        }

        /// <summary>
        /// Makes the open list tall enough to show every map without scrolling, up to most of the screen's height
        /// (the dropdown keeps the list on screen, opening upwards if it must). Past that it scrolls.
        /// </summary>
        private static void FitListToOptions(TMP_Dropdown dd, int count)
        {
            try
            {
                var template = dd.template;
                var scroll = template != null ? template.GetComponent<ScrollRect>() : null;
                var item = dd.itemText != null ? dd.itemText.transform.parent as RectTransform : null;
                if (template == null || scroll == null || scroll.viewport == null || item == null) return;
                var canvas = dd.GetComponentInParent<Canvas>()?.rootCanvas;
                float screenH = canvas != null ? ((RectTransform)canvas.transform).rect.height : 1080f;

                float itemH = item.rect.height;
                var content = scroll.content;
                float spacing = 0f;
                if (content != null && content.GetComponent<VerticalLayoutGroup>() is VerticalLayoutGroup vlg) spacing = vlg.spacing;
                float extra = template.rect.height - scroll.viewport.rect.height;   // the list's border around the items
                float want = count * itemH + (count - 1) * spacing + extra + 4f;
                float height = Mathf.Min(want, screenH * 0.85f);
                if (height > template.rect.height) template.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                if (Plugin.DevTools.Value)
                    Plugin.Log.LogInfo($"[dropdown] list height {template.rect.height:0} for {count} entries of {itemH:0} (screen {screenH:0}).");
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"Map chooser: couldn't size the list: {e.Message}"); }
        }

        /// <summary>
        /// Mouse-wheel scrolling for the open list. The game's dropdowns are short and its menus don't pass the
        /// wheel to the list, so this moves the list itself: one notch scrolls about one entry.
        /// </summary>
        private sealed class WheelScroll : MonoBehaviour
        {
            private TMP_Dropdown _dd;
            private void Awake() => _dd = GetComponent<TMP_Dropdown>();

            private void Update()
            {
                if (_dd == null) return;
                bool dev = Plugin.DevTools.Value;
                if (!_dd.IsExpanded) { _described = false; return; }
                var scroll = OpenList();
                if (dev && !_described) { _described = true; Describe(scroll); }
                float wheel = ReadWheel(out string source);
                if (Mathf.Abs(wheel) < 0.0001f) return;
                if (dev) Plugin.Log.LogInfo($"[dropdown] wheel {wheel} from {source}.");

                if (scroll == null || scroll.content == null || scroll.viewport == null)
                {
                    if (dev) Plugin.Log.LogInfo($"[dropdown] wheel {wheel}: no scroll area found (scroll {scroll != null}).");
                    return;
                }
                scroll.scrollSensitivity = 0f;   // don't also scroll it the usual way, if that ever works
                float range = scroll.content.rect.height - scroll.viewport.rect.height;
                float step = _dd.itemText != null
                    ? Mathf.Max(20f, ((RectTransform)_dd.itemText.transform.parent).rect.height)
                    : 40f;
                float before = scroll.verticalNormalizedPosition;
                if (range > 0f) scroll.verticalNormalizedPosition = Mathf.Clamp01(before + Mathf.Sign(wheel) * step / range);
                _watch = dev ? 3 : 0;
                if (dev) Plugin.Log.LogInfo($"[dropdown] wheel {wheel}: content {scroll.content.rect.height:0} viewport {scroll.viewport.rect.height:0} " +
                                            $"step {step:0} position {before:0.000} -> {scroll.verticalNormalizedPosition:0.000}");
            }

            private static bool _legacyOff, _listedDevices;

            /// <summary>The wheel this frame, from any mouse the input system knows, else Unity's older input.</summary>
            private static float ReadWheel(out string source)
            {
                source = "";
                if (Plugin.DevTools.Value && !_listedDevices)
                {
                    _listedDevices = true;
                    var sb = new System.Text.StringBuilder($"[dropdown] input devices (scroll {UnityEngine.InputSystem.InputSystem.settings.scrollDeltaBehavior}, " +
                                                           $"update {UnityEngine.InputSystem.InputSystem.settings.updateMode}):");
                    foreach (var d in UnityEngine.InputSystem.InputSystem.devices) sb.Append($"\n  {d.name} ({d.GetType().Name}, enabled {d.enabled})");
                    Plugin.Log.LogInfo(sb.ToString());
                }
                foreach (var d in UnityEngine.InputSystem.InputSystem.devices)
                {
                    if (!(d is UnityEngine.InputSystem.Mouse m)) continue;
                    float y = m.scroll.ReadValue().y;
                    if (Mathf.Abs(y) > 0.0001f) { source = m.name; return y; }
                }
                if (!_legacyOff)
                {
                    try
                    {
                        float y = Input.mouseScrollDelta.y;
                        if (Mathf.Abs(y) > 0.0001f) { source = "old input"; return y; }
                    }
                    catch (System.InvalidOperationException) { _legacyOff = true; }
                }
                return 0f;
            }

            private bool _described;
            private int _watch;

            private void LateUpdate()
            {
                if (_watch <= 0 || !_dd.IsExpanded) return;
                _watch--;
                var scroll = OpenList();
                if (scroll != null) Plugin.Log.LogInfo($"[dropdown] late: position {scroll.verticalNormalizedPosition:0.000}, content y {scroll.content?.anchoredPosition.y:0}");
            }

            private static void Describe(ScrollRect scroll)
            {
                if (scroll == null) { Plugin.Log.LogInfo("[dropdown] open, but no ScrollRect found."); return; }
                var sb = new System.Text.StringBuilder($"[dropdown] open list '{scroll.name}': vertical {scroll.vertical}, sensitivity {scroll.scrollSensitivity}, " +
                                                       $"movement {scroll.movementType}, content '{scroll.content?.name}', viewport '{scroll.viewport?.name}'");
                foreach (var c in scroll.GetComponentsInChildren<Component>(true))
                {
                    if (c is Transform || c is CanvasRenderer || c is Graphic || c is TMP_Text) continue;
                    sb.Append($"\n  {c.gameObject.name}: {c.GetType().FullName}");
                }
                Plugin.Log.LogInfo(sb.ToString());
            }

            private ScrollRect OpenList()
            {
                var list = Traverse.Create(_dd).Field("m_Dropdown").GetValue<GameObject>();
                if (list != null) return list.GetComponentInChildren<ScrollRect>();
                foreach (var s in FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
                    if (s.gameObject.name == "Dropdown List" && s.gameObject.activeInHierarchy) return s;
                return null;
            }
        }

        /// <summary>One of the game's own dropdowns (settings menu first: those have the click sounds).</summary>
        private static TMP_Dropdown FindTemplateDropdown()
        {
            TMP_Dropdown any = null;
            foreach (var d in Resources.FindObjectsOfTypeAll<TMP_Dropdown>())
            {
                if (d == null || !d.gameObject.scene.IsValid() || d.template == null) continue;
                if (d.GetComponent<UiSfx>() != null) return d;
                if (any == null) any = d;
            }
            return any;
        }

        /// <summary>
        /// Pyramid, Grand and the dropdown share the space the game's two buttons used: the buttons a little
        /// narrower, the dropdown wider, even gaps. Button labels shrink to fit.
        /// </summary>
        private static void Place(RectTransform classic, RectTransform grand, RectTransform dropdown)
        {
            if (grand.parent != null && grand.parent.GetComponent<LayoutGroup>() != null)
            {
                // the game lays the row out itself: just size the dropdown like a wider button
                var le = dropdown.GetComponent<LayoutElement>() ?? dropdown.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = grand.rect.width * 1.5f;
                le.preferredHeight = grand.rect.height;
                return;
            }

            float w = classic.rect.width, h = classic.rect.height;
            float gap = Mathf.Max(0f, Mathf.Abs(grand.anchoredPosition.x - classic.anchoredPosition.x) - w);
            float left = Mathf.Min(classic.anchoredPosition.x, grand.anchoredPosition.x) - w * 0.5f;
            float total = Mathf.Abs(grand.anchoredPosition.x - classic.anchoredPosition.x) + w;
            float y = classic.anchoredPosition.y;
            // widths: button, button, dropdown = 1 : 1 : 1.25
            float unit = (total - 2 * gap) / 3.25f;
            float[] widths = { unit, unit, unit * 1.25f };
            var rects = new[] { classic, grand, dropdown };

            dropdown.anchorMin = classic.anchorMin;
            dropdown.anchorMax = classic.anchorMax;
            dropdown.pivot = classic.pivot;
            float x = left;
            for (int i = 0; i < rects.Length; i++)
            {
                var r = rects[i];
                r.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, widths[i]);
                if (i == 2) r.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                float cx = x + widths[i] * 0.5f;
                r.anchoredPosition = new Vector2(cx + (r.pivot.x - 0.5f) * widths[i], y);
                x += widths[i] + gap;
                foreach (var text in r.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMax = text.fontSize;
                    text.fontSizeMin = Mathf.Max(8f, text.fontSize * 0.5f);
                }
            }
        }

        /// <summary>DevTools (F6 on the main menu): pick the next custom map, wrapping to none.</summary>
        internal static void ChooseNext()
        {
            var showcase = Object.FindAnyObjectByType<SlotShowcase>();
            if (showcase == null) return;
            var maps = new List<MapRegistry.MapInfo>(MapRegistry.Visible);
            int i = maps.IndexOf(MapRegistry.ForNewWorlds);
            var next = i + 1 < maps.Count ? maps[i + 1] : null;
            Choose(showcase, next, WorldKind.Classic);
            Plugin.Log.LogInfo($"DevTools: menu map is now {next?.Id ?? "the normal pyramid"}.");
        }

        private static void Choose(SlotShowcase showcase, MapRegistry.MapInfo map, WorldKind kind)
        {
            if (map == null && MapRegistry.ForNewWorlds == null) return;   // a normal choice, nothing of ours to undo
            MapRegistry.ChooseForNewWorlds(map);
            ShownKeyRef(showcase) = "\0";     // same world kind, different map: make the showcase redraw
            showcase.ChooseKind(kind);
        }

        /// <summary>Tints, the dropdown's shown entry and the description, after the game sets them for Pyramid / Grand.</summary>
        [HarmonyPatch("FillPlotKind")]
        [HarmonyPostfix]
        private static void AfterFillPlotKind(SlotShowcase __instance)
        {
            if (!Choosers.TryGetValue(__instance, out var chooser) || chooser.Dropdown == null) return;
            var chosen = MapRegistry.ForNewWorlds;
            if (chosen != null && WorldKinds.PendingNewWorld != WorldKind.Classic) { MapRegistry.ChooseForNewWorlds(null); chosen = null; }

            int index = chosen != null ? chooser.Maps.IndexOf(chosen) + 1 : 0;
            if (chooser.Dropdown.value != index) chooser.Dropdown.SetValueWithoutNotify(index);
            var off = OffTintRef(__instance);
            if (chooser.Dropdown.image != null) chooser.Dropdown.image.color = chosen != null ? Color.white : off;

            if (chosen == null) return;
            var classic = ClassicRef(__instance);
            if (classic != null && classic.image != null) classic.image.color = off;
            var body = BodyRef(__instance);
            if (body != null) body.text = chosen.Body;
        }
    }
}

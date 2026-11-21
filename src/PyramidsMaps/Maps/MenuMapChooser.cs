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
    /// Main menu, empty plot: the game offers Pyramid and Grand Pyramid buttons. This adds one button per custom
    /// map (copies of the Grand button, laid out after it). Choosing a custom map starts new worlds with it;
    /// choosing Pyramid or Grand goes back to the game's own. The choice is kept in the NewWorldMap setting.
    /// </summary>
    [HarmonyPatch(typeof(SlotShowcase))]
    internal static class MenuMapChooser
    {
        private static readonly AccessTools.FieldRef<SlotShowcase, Button> ClassicRef = AccessTools.FieldRefAccess<SlotShowcase, Button>("kindClassicButton");
        private static readonly AccessTools.FieldRef<SlotShowcase, Button> GrandRef = AccessTools.FieldRefAccess<SlotShowcase, Button>("kindGrandButton");
        private static readonly AccessTools.FieldRef<SlotShowcase, TMP_Text> BodyRef = AccessTools.FieldRefAccess<SlotShowcase, TMP_Text>("plotBodyText");
        private static readonly AccessTools.FieldRef<SlotShowcase, Color> OffTintRef = AccessTools.FieldRefAccess<SlotShowcase, Color>("kindOffTint");
        private static readonly AccessTools.FieldRef<SlotShowcase, string> ShownKeyRef = AccessTools.FieldRefAccess<SlotShowcase, string>("_shownKey");

        private static readonly Dictionary<SlotShowcase, List<(MapRegistry.MapInfo map, Button button)>> Buttons =
            new Dictionary<SlotShowcase, List<(MapRegistry.MapInfo, Button)>>();

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AfterAwake(SlotShowcase __instance)
        {
            try
            {
                var classic = ClassicRef(__instance);
                var grand = GrandRef(__instance);
                if (classic == null || grand == null) { Plugin.Log.LogWarning("Map chooser: the empty plot has no Pyramid/Grand buttons; not adding map buttons."); return; }

                // Pyramid / Grand: back to the game's own map
                classic.onClick.AddListener(() => Choose(__instance, null, WorldKind.Classic));
                grand.onClick.AddListener(() => Choose(__instance, null, WorldKind.Grand));

                var list = new List<(MapRegistry.MapInfo, Button)>();
                var gRect = (RectTransform)grand.transform;
                var cRect = (RectTransform)classic.transform;
                Vector2 step = gRect.anchoredPosition - cRect.anchoredPosition;
                bool laidOut = grand.transform.parent != null && grand.transform.parent.GetComponent<LayoutGroup>() != null;
                int i = 0;
                foreach (var map in MapRegistry.All)
                {
                    i++;
                    var go = Object.Instantiate(grand.gameObject, grand.transform.parent);
                    go.name = "MapButton_" + map.Id;
                    var button = go.GetComponent<Button>();
                    button.onClick = new Button.ButtonClickedEvent();
                    var m = map;
                    button.onClick.AddListener(() => Choose(__instance, m, WorldKind.Classic));
                    foreach (var text in go.GetComponentsInChildren<TMP_Text>(true)) text.text = map.Title;
                    if (!laidOut) ((RectTransform)go.transform).anchoredPosition = gRect.anchoredPosition + step * i;
                    list.Add((map, button));
                }
                if (!laidOut) FitInRow(cRect, gRect, list);
                Buttons[__instance] = list;
                Plugin.Log.LogInfo($"Map chooser: added {list.Count} map button(s) to the empty plot.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"Map chooser failed: {e}");
            }
        }

        /// <summary>
        /// Lays Pyramid, Grand and the map buttons out in the space the game's two buttons used: same overall
        /// width, narrower buttons, even gaps. Labels shrink to fit.
        /// </summary>
        private static void FitInRow(RectTransform classic, RectTransform grand, List<(MapRegistry.MapInfo map, Button button)> extra)
        {
            var rects = new List<RectTransform> { classic, grand };
            foreach (var (_, b) in extra) rects.Add((RectTransform)b.transform);

            float w = classic.rect.width;
            float gap = Mathf.Max(0f, Mathf.Abs(grand.anchoredPosition.x - classic.anchoredPosition.x) - w);
            float left = Mathf.Min(classic.anchoredPosition.x, grand.anchoredPosition.x) - w * 0.5f;
            float total = Mathf.Abs(grand.anchoredPosition.x - classic.anchoredPosition.x) + w;
            int n = rects.Count;
            float nw = (total - gap * (n - 1)) / n;
            float y = classic.anchoredPosition.y;

            for (int i = 0; i < n; i++)
            {
                var r = rects[i];
                r.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, nw);
                float cx = left + nw * 0.5f + i * (nw + gap);
                // anchoredPosition is the pivot; place the button's centre at cx
                r.anchoredPosition = new Vector2(cx + (r.pivot.x - 0.5f) * nw, y);
                foreach (var text in r.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.enableAutoSizing = true;
                    text.fontSizeMax = text.fontSize;
                    text.fontSizeMin = Mathf.Max(8f, text.fontSize * 0.5f);
                }
            }
        }

        private static void Choose(SlotShowcase showcase, MapRegistry.MapInfo map, WorldKind kind)
        {
            if (map == null && MapRegistry.ForNewWorlds == null) return;   // a normal choice, nothing of ours to undo
            MapRegistry.ChooseForNewWorlds(map);
            ShownKeyRef(showcase) = "\0";     // same world kind, different map: make the showcase redraw
            showcase.ChooseKind(kind);
        }

        /// <summary>Tints and the description, after the game sets them for Pyramid / Grand.</summary>
        [HarmonyPatch("FillPlotKind")]
        [HarmonyPostfix]
        private static void AfterFillPlotKind(SlotShowcase __instance)
        {
            if (!Buttons.TryGetValue(__instance, out var list)) return;
            var chosen = MapRegistry.ForNewWorlds;
            if (chosen != null && WorldKinds.PendingNewWorld != WorldKind.Classic) { MapRegistry.ChooseForNewWorlds(null); chosen = null; }
            var off = OffTintRef(__instance);
            foreach (var (map, button) in list)
                if (button != null && button.image != null) button.image.color = map == chosen ? Color.white : off;
            if (chosen == null) return;
            var classic = ClassicRef(__instance);
            if (classic != null && classic.image != null) classic.image.color = off;
            var body = BodyRef(__instance);
            if (body != null) body.text = chosen.Body;
        }
    }
}

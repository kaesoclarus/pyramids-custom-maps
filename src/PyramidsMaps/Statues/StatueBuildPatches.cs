using System.Collections.Generic;
using HarmonyLib;
using Pyramid.Build;
using Pyramid.World;
using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// How the cat is built: every block is open from the start (not one layer at a time), but a block can only
    /// be laid touching a block already laid, or on the ground layer. No floating bricks; overhangs grow
    /// outward from their neighbours. Only affects layouts built for the cat.
    /// </summary>
    [HarmonyPatch]
    internal static class StatueBuildPatches
    {
        private static readonly AccessTools.FieldRef<PyramidProgress, List<int>> OpenRef =
            AccessTools.FieldRefAccess<PyramidProgress, List<int>>("_open");
        private static readonly AccessTools.FieldRef<PyramidProgress, bool> OpenDirtyRef =
            AccessTools.FieldRefAccess<PyramidProgress, bool>("_openDirty");

        /// <summary>Open sections: all that aren't complete.</summary>
        [HarmonyPatch(typeof(PyramidProgress), "RebuildOpen")]
        [HarmonyPrefix]
        private static bool RebuildOpen(PyramidProgress __instance)
        {
            if (!StatueLayout.IsStatue(__instance.Layout)) return true;
            var open = OpenRef(__instance);
            OpenDirtyRef(__instance) = false;
            open.Clear();
            int n = __instance.Layout.Sections.Count;
            for (int s = 0; s < n; s++)
                if (!__instance.IsSectionComplete(s)) open.Add(s);
            return false;
        }

        /// <summary>Same answer as the game's, without scanning the (long) open list.</summary>
        [HarmonyPatch(typeof(PyramidProgress), nameof(PyramidProgress.IsSectionOpen))]
        [HarmonyPrefix]
        private static bool IsSectionOpen(PyramidProgress __instance, int s, ref bool __result)
        {
            if (!StatueLayout.IsStatue(__instance.Layout)) return true;
            __result = s >= 0 && s < __instance.Layout.Sections.Count && !__instance.IsSectionComplete(s);
            return false;
        }

        /// <summary>An open, empty block can only be laid if it touches a laid block (or is on the ground layer).</summary>
        [HarmonyPatch(typeof(PyramidProgress), nameof(PyramidProgress.IsCellOpenAndEmpty))]
        [HarmonyPostfix]
        private static void IsCellOpenAndEmpty(PyramidProgress __instance, int cell, ref bool __result)
        {
            if (__result && StatueLayout.IsStatue(__instance.Layout) && !Touches(__instance, cell))
                __result = false;
        }

        /// <summary>
        /// Trebuchets, pigeons and Shabti pick spots that aren't "taken": treat blocks that don't touch a laid
        /// block as taken, so machines never lay a floating brick either.
        /// </summary>
        [HarmonyPatch(typeof(PyramidState), nameof(PyramidState.IsTaken))]
        [HarmonyPostfix]
        private static void IsTaken(PyramidState __instance, int cell, ref bool __result)
        {
            if (__result) return;
            var p = __instance.Progress;
            if (p != null && StatueLayout.IsStatue(p.Layout) && !Touches(p, cell)) __result = true;
        }

        public static bool Touches(PyramidProgress p, int cell) => StatueCells.For(p.Layout).Touches(p, cell);
    }

    /// <summary>
    /// Drawing and collisions: the renderer switches finished layers to square slabs and cores (made for the
    /// pyramid). For the cat every layer stays in the exact per-section mode.
    /// </summary>
    [HarmonyPatch]
    internal static class StatueRenderPatches
    {
        [System.ThreadStatic] private static bool _inCatRebuild;

        /// <summary>Size of the "build here next" markers, as a fraction of a block.</summary>
        private const float MarkerScale = 0.45f;

        private static readonly AccessTools.FieldRef<PyramidRenderer, PyramidState> StateRef =
            AccessTools.FieldRefAccess<PyramidRenderer, PyramidState>("state");
        private static readonly AccessTools.FieldRef<PyramidRenderer, List<Matrix4x4>> GhostsRef =
            AccessTools.FieldRefAccess<PyramidRenderer, List<Matrix4x4>>("_ghosts");
        private static readonly AccessTools.FieldRef<PyramidRenderer, Bounds> GhostBoundsRef =
            AccessTools.FieldRefAccess<PyramidRenderer, Bounds>("_ghostBounds");
        private static readonly AccessTools.FieldRef<PyramidRenderer, HashSet<int>> SuppressedRef =
            AccessTools.FieldRefAccess<PyramidRenderer, HashSet<int>>("_suppressed");
        private static readonly System.Func<PyramidRenderer, int, float, Matrix4x4> CellMatrix =
            AccessTools.MethodDelegate<System.Func<PyramidRenderer, int, float, Matrix4x4>>(AccessTools.Method(typeof(PyramidRenderer), "CellMatrix"));

        [HarmonyPatch(typeof(PyramidRenderer), "Rebuild")]
        [HarmonyPrefix]
        private static void BeforeRebuild(PyramidRenderer __instance)
        {
            var state = StateRef(__instance);
            _inCatRebuild = state != null && StatueLayout.IsStatue(state.Layout);
            if (_inCatRebuild) BlockBottoms.Cap(MeshesRef(__instance));
        }

        private static readonly AccessTools.FieldRef<PyramidRenderer, Mesh[]> MeshesRef =
            AccessTools.FieldRefAccess<PyramidRenderer, Mesh[]>("_meshes");

        /// <summary>
        /// Markers on the cat: only the spots you can build right now (open, empty, touching a laid block),
        /// drawn small, so the growing edge shows where to place next without hiding the statue.
        /// </summary>
        [HarmonyPatch(typeof(PyramidRenderer), "Rebuild")]
        [HarmonyPostfix]
        private static void AfterRebuildMarkers(PyramidRenderer __instance)
        {
            if (!_inCatRebuild) return;
            var state = StateRef(__instance);
            var layout = state.Layout;
            var prog = state.Progress;
            var cells = StatueCells.For(layout);
            var ghosts = GhostsRef(__instance);
            var suppressed = SuppressedRef(__instance);
            ghosts.Clear();
            if (!Plugin.ShowMarkers.Value) return;
            bool any = false;
            var bounds = default(Bounds);
            for (int i = 0; i < layout.TotalCells; i++)
            {
                if (prog.IsFilled(i) || state.IsPending(i) || suppressed.Contains(i) || state.IsCapstone(i)) continue;
                if (!cells.NextToLaid(prog, i)) continue;
                ghosts.Add(CellMatrix(__instance, i, MarkerScale));
                var c = state.CellWorldCenter(i);
                if (any) bounds.Encapsulate(c); else { bounds = new Bounds(c, Vector3.one); any = true; }
            }
            if (any) { bounds.Expand(2f); GhostBoundsRef(__instance) = bounds; }
        }

        [HarmonyPatch(typeof(PyramidRenderer), "Rebuild")]
        [HarmonyFinalizer]
        private static void AfterRebuild() => _inCatRebuild = false;

        [HarmonyPatch(typeof(PyramidProgress), nameof(PyramidProgress.IsLayerComplete))]
        [HarmonyPrefix]
        private static bool IsLayerComplete(ref bool __result)
        {
            if (!_inCatRebuild) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// Standing on the cat: the game finds the walking surface and "treads" (finished steps) by square rings and
    /// caps them at the current layer. For the cat: the highest laid block in the column, and a tread is a
    /// column built to its full height.
    /// </summary>
    [HarmonyPatch]
    internal static class StatueSurfacePatches
    {
        [HarmonyPatch(typeof(PyramidPlacement), nameof(PyramidPlacement.SurfaceLayer))]
        [HarmonyPrefix]
        private static bool SurfaceLayer(PyramidState pyramid, int cx, int cz, ref int __result)
        {
            if (pyramid == null || !StatueLayout.IsStatue(pyramid.Layout)) return true;
            __result = TopFilled(pyramid, cx, cz);
            return false;
        }

        [HarmonyPatch(typeof(PyramidPlacement), nameof(PyramidPlacement.TryGetTread))]
        [HarmonyPrefix]
        private static bool TryGetTread(PyramidState pyramid, Vector3 point, ref int layer, ref float height, ref bool __result)
        {
            if (pyramid == null || !StatueLayout.IsStatue(pyramid.Layout) || pyramid.Site == null) return true;
            layer = -1;
            height = point.y;
            var layout = pyramid.Layout;
            var t = pyramid.Site.transform;
            var local = t.InverseTransformPoint(point);
            int cx = Mathf.RoundToInt(local.x / layout.CellSize.x), cz = Mathf.RoundToInt(local.z / layout.CellSize.z);
            int s = TopFilled(pyramid, cx, cz);
            __result = s >= 0 && s == StatueWorld.ColumnTop(cx, cz) - 1;
            if (__result)
            {
                layer = s;
                height = t.TransformPoint(new Vector3(local.x, (layout.Layers[s].Y + 1) * layout.CellSize.y, local.z)).y;
            }
            return false;
        }

        [HarmonyPatch(typeof(PyramidPlacement), nameof(PyramidPlacement.IsTreadCell))]
        [HarmonyPrefix]
        private static bool IsTreadCell(PyramidState pyramid, int layer, int cx, int cz, ref bool __result)
        {
            if (pyramid == null || !StatueLayout.IsStatue(pyramid.Layout)) return true;
            __result = layer >= 0 && layer == StatueWorld.ColumnTop(cx, cz) - 1 && TopFilled(pyramid, cx, cz) == layer;
            return false;
        }

        [HarmonyPatch(typeof(PyramidPlacement), nameof(PyramidPlacement.IsWorkSurfaceColumn))]
        [HarmonyPrefix]
        private static bool IsWorkSurfaceColumn(PyramidState pyramid, int cx, int cz, ref bool __result)
        {
            if (pyramid == null || !StatueLayout.IsStatue(pyramid.Layout)) return true;
            int s = TopFilled(pyramid, cx, cz);
            __result = s >= 0 && s < StatueWorld.ColumnTop(cx, cz) - 1;
            return false;
        }

        /// <summary>Highest laid block in the column, or −1.</summary>
        private static int TopFilled(PyramidState pyramid, int cx, int cz)
        {
            var layout = pyramid.Layout;
            var progress = pyramid.Progress;
            for (int y = StatueWorld.ColumnTop(cx, cz) - 1; y >= 0; y--)
            {
                int i = layout.CellIndex(new Vector3Int(cx, y, cz));
                if (i >= 0 && progress.IsFilled(i)) return y;
            }
            return -1;
        }
    }

    /// <summary>Everything else that assumed a square pyramid.</summary>
    [HarmonyPatch]
    internal static class StatueWorldPatches
    {
        /// <summary>No building on the cat's footprint (the game keeps out a square around the whole site).</summary>
        [HarmonyPatch(typeof(BuildArea), nameof(BuildArea.OverlapsKeepOut))]
        [HarmonyPrefix]
        private static bool OverlapsKeepOut(BuildArea __instance, Vector3 position, Vector3 size, float yaw, ref bool __result)
        {
            var pyramid = Traverse.Create(__instance).Field("pyramid").GetValue<PyramidState>();
            if (pyramid == null || !StatueLayout.IsStatue(pyramid.Layout) || pyramid.Site == null) return true;

            var t = pyramid.Site.transform;
            var cs = pyramid.Layout.CellSize;
            var local = t.InverseTransformPoint(position);
            float rel = (yaw - t.eulerAngles.y) * Mathf.Deg2Rad;
            float c = Mathf.Abs(Mathf.Cos(rel)), s = Mathf.Abs(Mathf.Sin(rel));
            float ex = c * size.x * 0.5f + s * size.z * 0.5f + __instance.Margin;
            float ez = s * size.x * 0.5f + c * size.z * 0.5f + __instance.Margin;

            __result = false;
            for (float x = local.x - ex; x <= local.x + ex + 0.01f && !__result; x += 0.5f)
                for (float z = local.z - ez; z <= local.z + ez + 0.01f && !__result; z += 0.5f)
                    if (StatueWorld.BlocksGround(Mathf.RoundToInt(x / cs.x), Mathf.RoundToInt(z / cs.z))) __result = true;
            return false;
        }

        /// <summary>Stone ramps, scaffold platforms and the sledge lift snap to pyramid steps: not for sale on the cat.</summary>
        [HarmonyPatch(typeof(ShopService), nameof(ShopService.IsOnSale))]
        [HarmonyPostfix]
        private static void IsOnSale(ShopService __instance, int index, ref bool __result)
        {
            if (!__result || !StatueWorld.IsStatue) return;
            var item = __instance.Catalog != null ? __instance.Catalog.Get(index) : null;
            var prefab = item != null && item.prefab != null ? item.prefab.gameObject : null;
            if (prefab == null) return;
            if (prefab.GetComponentInChildren<StoneRamp>(true) != null ||
                prefab.GetComponentInChildren<ScaffoldPlatform>(true) != null ||
                prefab.GetComponentInChildren<SledgeLift>(true) != null)
                __result = false;
        }
    }
}

using System.Text;
using HarmonyLib;
using Pyramid.Build;
using Pyramid.Player;
using Pyramid.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PyramidsMaps.Sphinx
{
    /// <summary>
    /// Press F8 in a cat world: logs, for the open spots around your laid bricks, which of the game's checks
    /// accepts or rejects each one, and what the aim currently picks.
    /// </summary>
    internal sealed class CatDiagnostics : MonoBehaviour
    {
        private static readonly System.Action<PyramidRenderer> MarkDirty =
            AccessTools.MethodDelegate<System.Action<PyramidRenderer>>(AccessTools.Method(typeof(PyramidRenderer), "MarkDirty"));

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // F7: markers on/off (remembered in the config)
            if (kb.f7Key.wasPressedThisFrame)
            {
                Plugin.ShowMarkers.Value = !Plugin.ShowMarkers.Value;
                foreach (var r in FindObjectsByType<PyramidRenderer>()) MarkDirty(r);
                Plugin.Log.LogInfo($"Build markers {(Plugin.ShowMarkers.Value ? "on" : "off")}.");
            }

            // F9 twice within 2 s (DevTools on, host, cat world): fill everything but the final brick
            if (Plugin.DevTools.Value && kb.f9Key.wasPressedThisFrame)
            {
                if (Time.unscaledTime - _lastF9 < 2f) { _lastF9 = -10f; DevFillAllButLast(); }
                else { _lastF9 = Time.unscaledTime; Plugin.Log.LogWarning("DevTools: press F9 again within 2 s to fill the cat (all but the final brick)."); }
            }

            if (!kb.f8Key.wasPressedThisFrame) return;
            try { Plugin.Log.LogInfo(Describe()); }
            catch (System.Exception e) { Plugin.Log.LogError($"F8 diagnostic failed: {e}"); }
        }

        private float _lastF9 = -10f;

        private static void DevFillAllButLast()
        {
            var py = FindAnyObjectByType<PyramidState>();
            if (py == null || !SphinxLayout.IsCat(py.Layout)) { Plugin.Log.LogWarning("DevTools: not a cat world."); return; }
            if (!py.IsServerInitialized) { Plugin.Log.LogWarning("DevTools: only the host can fill."); return; }
            var cells = new System.Collections.Generic.List<int>();
            for (int i = 0; i < py.Layout.TotalCells; i++)
                if (!py.Progress.IsFilled(i) && !py.IsCapstone(i)) cells.Add(i);
            py.ServerFillCells(cells, -1);
            Plugin.Log.LogWarning($"DevTools: filled {cells.Count} blocks; the final brick (cell {py.CapstoneCell}, {py.CellWorldCenter(py.CapstoneCell)}) is left.");
        }

        private static string Describe()
        {
            var py = FindAnyObjectByType<PyramidState>();
            if (py == null || !SphinxLayout.IsCat(py.Layout)) return "F8: not a cat world.";

            PlayerActions me = null;
            foreach (var a in FindObjectsByType<PlayerActions>())
                if (a.IsOwner) { me = a; break; }
            if (me == null) return "F8: no local player.";

            var feet = me.transform.position;
            var cam = Camera.main;
            var eye = cam != null ? cam.transform.position : feet + Vector3.up * 1.6f;
            var layout = py.Layout;
            var prog = py.Progress;
            var sb = new StringBuilder();
            sb.AppendLine($"F8 cat diagnostic: feet {feet} eye {eye} placeMaxRise {py.Tuning?.placeMaxRise} " +
                          $"placed {prog.TotalPlaced} open sections {prog.OpenSections.Count}");

            // what the aim would pick right now
            if (cam != null)
            {
                bool found = me.TryFindPlaceCell(eye, cam.transform.forward, 25f, 6f, -1, null, out int cell, out int blocked, out string blocker, out int climb);
                sb.AppendLine($"  aim (cone 25°, range 6): found={found} cell={Cell(layout, cell)} blocked={Cell(layout, blocked)} '{blocker}' climb={Cell(layout, climb)}");
            }

            // laid bricks within 5 m and their open neighbours
            int shown = 0;
            for (int i = 0; i < layout.TotalCells && shown < 12; i++)
            {
                if (!prog.IsFilled(i)) continue;
                if ((py.CellWorldCenter(i) - feet).sqrMagnitude > 25f) continue;
                var c = layout.CellCoord(i);
                foreach (var d in new[] { new Vector3Int(0, 1, 0), new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) })
                {
                    int n = layout.CellIndex(c + d);
                    if (n < 0 || prog.IsFilled(n) || shown >= 12) continue;
                    shown++;
                    bool blockedN = PyramidOccupancy.IsBlocked(py, n, out string why);
                    sb.AppendLine($"  {Cell(layout, n)} next to {c}: taken={py.IsTaken(n)} sectionOpen={prog.IsSectionOpen(layout.SectionOfCell(n))} " +
                                  $"openEmpty={prog.IsCellOpenAndEmpty(n)} touches={CatBuildPatches.Touches(prog, n)} canTake={py.CanTakeBrick(n)} " +
                                  $"blocked={blockedN} '{why}' sight={me.CanSeeCell(py, eye, n)} range: {py.DevDescribePlaceRange(feet, n, 6f)}");
                }
            }
            if (shown == 0) sb.AppendLine("  no laid bricks within 5 m.");

            // Work Posts: where they stand and how the game reads their step
            foreach (var post in FindObjectsByType<Pyramid.Workers.ShabtiShrine>())
            {
                var t = Traverse.Create(post);
                var pos = post.transform.position;
                var local = py.Site.transform.InverseTransformPoint(pos);
                float tol = t.Property("PlaneTolerance").GetValue<float>();
                int at = py.BuilderLevelAt(pos, tol, 0.75f, false, 0);
                int guess = Mathf.RoundToInt(local.y / layout.CellSize.y);
                bool planeOk = guess >= 0 && guess < layout.Layers.Count && py.TryBuilderPlaneCoord(guess, pos, tol, 0.75f, out _);
                sb.AppendLine($"  Work Post at {pos} (site-local y {local.y:0.00}, {(local.y / layout.CellSize.y):0.00} layers): job={post.Job} " +
                              $"onSurface={t.Field("_onSurface").GetValue()} status={t.Field("_surfaceStatus").GetValue()} planeLevel={t.Field("_planeLevel").GetValue()} " +
                              $"levelAt={at} heightGuess={guess} layerDone={(guess >= 0 && guess < layout.Layers.Count && prog.IsLayerComplete(guess))} " +
                              $"planeCoordOk={planeOk} tol={tol:0.00} builderLevel={py.BuilderLevel}");
            }
            return sb.ToString();
        }

        private static string Cell(PyramidLayout layout, int cell) =>
            cell < 0 ? "-" : $"#{cell}{layout.CellCoord(cell)}";
    }
}

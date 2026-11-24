using System.Text;
using HarmonyLib;
using Pyramid.Build;
using Pyramid.Player;
using Pyramid.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// Press F8 in a cat world: logs, for the open spots around your laid bricks, which of the game's checks
    /// accepts or rejects each one, and what the aim currently picks.
    /// </summary>
    internal sealed class StatueDiagnostics : MonoBehaviour
    {
        private static readonly System.Action<PyramidRenderer> MarkDirty =
            AccessTools.MethodDelegate<System.Action<PyramidRenderer>>(AccessTools.Method(typeof(PyramidRenderer), "MarkDirty"));

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // DevTools: log what UI each left click lands on (for finding what blocks a menu control)
            var mouse = Mouse.current;
            if (Plugin.DevTools.Value && mouse != null && mouse.leftButton.wasPressedThisFrame && UnityEngine.EventSystems.EventSystem.current != null)
            {
                var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = mouse.position.ReadValue() };
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(data, hits);
                var sb2 = new StringBuilder($"[ui-click] at {data.position}: {hits.Count} hit(s)");
                for (int i = 0; i < hits.Count && i < 6; i++)
                {
                    var h = hits[i];
                    var cv = h.gameObject.GetComponentInParent<Canvas>();
                    sb2.Append($"\n  {i}: {Path(h.gameObject.transform)} (canvas '{cv?.name}' mode {cv?.renderMode} layer {cv?.sortingLayerName} order {cv?.sortingOrder}, raycaster {h.module?.GetType().Name})");
                }
                Plugin.Log.LogInfo(sb2.ToString());
            }

            // F6 (DevTools, main menu): next custom map in the empty plot
            // Ctrl+F6 (DevTools, host, statue world): wall in an empty block on purpose, to test SealedGaps
            // (Shift too: leave it unfilled until the world is loaded again)
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            if (Plugin.DevTools.Value && kb.f6Key.wasPressedThisFrame && ctrl)
                SealedGaps.DevMakeGap(kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            else if (Plugin.DevTools.Value && kb.f6Key.wasPressedThisFrame) PyramidsMaps.Maps.MenuMapChooser.ChooseNext();

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
                else { _lastF9 = Time.unscaledTime; Plugin.Log.LogWarning("DevTools: press F9 again within 2 s to fill the statue (all but the final brick)."); }
            }

            // On a test map: frame rate every 10 s, for measuring what the game can handle
            if (StatueWorld.Map != null && StatueWorld.Map.DevOnly)
            {
                _frames++;
                _worst = Mathf.Max(_worst, Time.unscaledDeltaTime);
                if (Time.unscaledTime - _since >= 10f)
                {
                    float span = Time.unscaledTime - _since;
                    var py = FindAnyObjectByType<PyramidState>();
                    Plugin.Log.LogInfo($"[perf] {_frames / span:0.0} fps average, worst frame {_worst * 1000f:0} ms over {span:0} s; " +
                                       $"{(py != null ? py.Progress.TotalPlaced : 0):N0} of {(py != null ? py.Layout.TotalCells : 0):N0} blocks placed.");
                    _since = Time.unscaledTime; _frames = 0; _worst = 0f;
                }
            }

            if (!kb.f8Key.wasPressedThisFrame) return;
            try { Plugin.Log.LogInfo(Describe()); }
            catch (System.Exception e) { Plugin.Log.LogError($"F8 diagnostic failed: {e}"); }
            try { Plugin.Log.LogInfo(DescribeBlockMeshes()); }
            catch (System.Exception e) { Plugin.Log.LogError($"F8 block-mesh report failed: {e}"); }
            try { Plugin.Log.LogInfo(DescribeLayers()); }
            catch (System.Exception e) { Plugin.Log.LogError($"F8 layer report failed: {e}"); }
        }

        private static readonly AccessTools.FieldRef<PyramidRenderer, byte[]> LayerModeRef =
            AccessTools.FieldRefAccess<PyramidRenderer, byte[]>("_layerMode");
        private static readonly AccessTools.FieldRef<PyramidRenderer, System.Collections.IDictionary> ActiveLayersRef =
            AccessTools.FieldRefAccess<PyramidRenderer, System.Collections.IDictionary>("_activeLayers");

        /// <summary>Per layer: blocks, filled, the renderer's mode and what it draws (count and bounds).</summary>
        private static string DescribeLayers()
        {
            var py = FindAnyObjectByType<PyramidState>();
            var r = FindAnyObjectByType<PyramidRenderer>();
            if (py == null || r == null) return "F8 layers: no pyramid or renderer.";
            var layout = py.Layout; var prog = py.Progress;
            var modes = LayerModeRef(r); var active = ActiveLayersRef(r);
            var sb = new System.Text.StringBuilder($"Layers ({layout.Layers.Count}, height {layout.HeightMetres:0.0} m, footprint ±{layout.FootprintHalf:0.0} m): layer y cells filled mode drawn bounds");
            for (int l = 0; l < layout.Layers.Count; l++)
            {
                var L = layout.Layers[l];
                string drawn = "-";
                if (active != null && active.Contains(l))
                {
                    var chunk = active[l];
                    var t = chunk.GetType();
                    object count = AccessTools.Property(t, "Count")?.GetValue(chunk) ?? AccessTools.Field(t, "Count")?.GetValue(chunk);
                    object bounds = AccessTools.Field(t, "Bounds")?.GetValue(chunk) ?? AccessTools.Property(t, "Bounds")?.GetValue(chunk);
                    drawn = $"{count} {bounds}";
                }
                sb.Append($"\n  {l} y{L.Y} {L.CellCount} {prog.LayerFilled(l)} m{(modes != null && l < modes.Length ? modes[l] : -1)} {drawn}");
            }
            return sb.ToString();
        }

        private static readonly AccessTools.FieldRef<PyramidRenderer, Mesh[]> RendererMeshes =
            AccessTools.FieldRefAccess<PyramidRenderer, Mesh[]>("_meshes");

        /// <summary>Each block model's face area by direction (top, bottom, sides), to see which faces it has.</summary>
        private static string DescribeBlockMeshes()
        {
            var sb = new System.Text.StringBuilder("Block meshes (face area, m² in model units: up / down / sides):");
            foreach (var r in FindObjectsByType<PyramidRenderer>())
                foreach (var m in RendererMeshes(r) ?? new Mesh[0])
                {
                    if (m == null) continue;
                    if (!m.isReadable) { sb.Append($"\n  {m.name}: not readable"); continue; }
                    var v = m.vertices; var t = m.triangles;
                    float up = 0, down = 0, side = 0;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                        float area = n.magnitude * 0.5f;
                        if (area <= 0) continue;
                        float ny = n.y / (2 * area);
                        if (ny > 0.7f) up += area; else if (ny < -0.7f) down += area; else side += area;
                    }
                    sb.Append($"\n  {m.name}: {up:0.00} / {down:0.00} / {side:0.00}  (bounds {m.bounds.size})");
                }
            return sb.ToString();
        }

        private float _lastF9 = -10f;
        private float _since, _worst;
        private int _frames;

        private void DevFillAllButLast()
        {
            var py = FindAnyObjectByType<PyramidState>();
            if (py == null || !StatueWorld.Active) { Plugin.Log.LogWarning("DevTools: not a custom map world."); return; }
            if (!py.IsServerInitialized) { Plugin.Log.LogWarning("DevTools: only the host can fill."); return; }
            StartCoroutine(FillInChunks(py));
        }

        /// <summary>Blocks per fill call: each call is one network message, so big maps fill over many frames.</summary>
        private const int FillChunk = 3000;

        private static System.Collections.IEnumerator FillInChunks(PyramidState py)
        {
            var cells = new System.Collections.Generic.List<int>();
            for (int i = 0; i < py.Layout.TotalCells; i++)
                if (!py.Progress.IsFilled(i) && !py.IsCapstone(i)) cells.Add(i);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var chunk = new System.Collections.Generic.List<int>(FillChunk);
            for (int at = 0; at < cells.Count; at += FillChunk)
            {
                chunk.Clear();
                for (int k = at; k < Mathf.Min(cells.Count, at + FillChunk); k++) chunk.Add(cells[k]);
                py.ServerFillCells(chunk, -1);
                if ((at / FillChunk) % 20 == 0) Plugin.Log.LogInfo($"DevTools: filled {Mathf.Min(cells.Count, at + FillChunk):N0} of {cells.Count:N0} ...");
                yield return null;
            }
            Plugin.Log.LogWarning($"DevTools: filled {cells.Count:N0} blocks in {clock.ElapsedMilliseconds / 1000f:0.0} s; the final brick " +
                                  $"(cell {py.CapstoneCell}, {py.CellWorldCenter(py.CapstoneCell)}) is left.");
        }

        private static string Describe()
        {
            var py = FindAnyObjectByType<PyramidState>();
            if (py == null || !StatueLayout.IsStatue(py.Layout)) return "F8: not a statue world.";

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
            sb.AppendLine($"F8 statue diagnostic: feet {feet} eye {eye} placeMaxRise {py.Tuning?.placeMaxRise} " +
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
                                  $"openEmpty={prog.IsCellOpenAndEmpty(n)} touches={StatueBuildPatches.Touches(prog, n)} canTake={py.CanTakeBrick(n)} " +
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

        private static string Path(Transform t)
        {
            var s = t.name;
            for (int i = 0; i < 4 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        private static string Cell(PyramidLayout layout, int cell) =>
            cell < 0 ? "-" : $"#{cell}{layout.CellCoord(cell)}";
    }
}

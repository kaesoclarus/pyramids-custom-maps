using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Pyramid.Workers;
using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// Shabti builders, part 2:
    ///  - Ground builders start their walk at the Work Post. The game snaps the start to the edge of the
    ///    pyramid's square site (right beside the stones on a pyramid, but tens of metres from the slim cat).
    ///  - Once the bottom layer is done, ground builders ask for Stone Ramps, which aren't sold on the cat: say
    ///    what to do instead.
    /// </summary>
    [HarmonyPatch]
    internal static class StatueShabtiFixes
    {
        public static string NeedPostUp => $"Put the Work Post up on {StatueWorld.Map?.StatueName ?? "the statue"}, next to a brick crate";
        public static string NeedPostUpBubble => $"Post us up on {StatueWorld.Map?.StatueName ?? "the statue"}!";

        /// <summary>In PrepareField, Mathf.Abs(int) only feeds the "inside the square → snap to its edge" test.</summary>
        [HarmonyPatch(typeof(ShabtiShrine), "PrepareField")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PrepareField(IEnumerable<CodeInstruction> code)
        {
            var abs = AccessTools.Method(typeof(Mathf), nameof(Mathf.Abs), new[] { typeof(int) });
            var ours = AccessTools.Method(typeof(StatueShabtiFixes), nameof(AbsUnlessStatue));
            int n = 0;
            foreach (var ins in code)
            {
                if (ins.Calls(abs)) { n++; yield return new CodeInstruction(OpCodes.Call, ours).MoveLabelsFrom(ins); }
                else yield return ins;
            }
            if (n != 2) Plugin.Log.LogWarning($"Shabti ground-start fix: expected 2 Mathf.Abs(int) in PrepareField, found {n}.");
        }

        /// <summary>On the cat, never "inside the square": the walk starts where the Work Post is.</summary>
        public static int AbsUnlessStatue(int v) => StatueWorld.Active ? int.MaxValue : Mathf.Abs(v);

        [HarmonyPatch(typeof(ShabtiShrine), "NeedRampsLine")]
        [HarmonyPostfix]
        private static void NeedRampsLine(ref string __result)
        {
            if (StatueWorld.Active) __result = NeedPostUp;
        }

        /// <summary>The look card says "Needs Stone Ramps to climb" (a fixed string in DescribeLook).</summary>
        [HarmonyPatch(typeof(ShabtiShrine), nameof(ShabtiShrine.DescribeLook))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> DescribeLook(IEnumerable<CodeInstruction> code)
        {
            var ours = AccessTools.Method(typeof(StatueShabtiFixes), nameof(RampsLook));
            foreach (var ins in code)
            {
                if (ins.opcode == OpCodes.Ldstr && (ins.operand as string) == "Needs Stone Ramps to climb")
                    yield return new CodeInstruction(OpCodes.Call, ours).MoveLabelsFrom(ins);
                else yield return ins;
            }
        }

        public static string RampsLook() => StatueWorld.Active ? NeedPostUp : "Needs Stone Ramps to climb";
    }
}

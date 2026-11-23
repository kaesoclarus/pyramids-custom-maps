using System.Diagnostics;
using HarmonyLib;
using Pyramid.Build;
using Pyramid.Core;
using Pyramid.Net;
using PyramidsMaps.Statues;

namespace PyramidsMaps.Maps
{
    /// <summary>
    /// Joining a huge world. Loading a save of a million-block pyramid freezes the game for several seconds, and
    /// the host's own game is a client of its own server, so two short waits ran out: the client's connect
    /// attempts (about 5 s) and the server's wait for the client's hello (8 s). Both are raised to about 60 s
    /// (the hello wait on custom maps only). Also times the slow steps of loading, as [perf] lines.
    /// </summary>
    [HarmonyPatch]
    internal static class JoinPatches
    {
        private const float CustomMapHelloTimeout = 60f;
        private static float? _gameTimeout;
        private static readonly AccessTools.FieldRef<VersionAuthenticator, float> HelloTimeout =
            AccessTools.FieldRefAccess<VersionAuthenticator, float>("helloTimeout");

        [HarmonyPatch(typeof(VersionAuthenticator), nameof(VersionAuthenticator.OnRemoteConnection))]
        [HarmonyPrefix]
        private static void LongerWait(VersionAuthenticator __instance)
        {
            ref float t = ref HelloTimeout(__instance);
            if (_gameTimeout == null) _gameTimeout = t;
            t = StatueWorld.Map != null ? System.Math.Max(_gameTimeout.Value, CustomMapHelloTimeout) : _gameTimeout.Value;
        }

        /// <summary>
        /// The host's own client (Tugboat, over loopback) tries to connect 10 times, 0.5 s apart, and gives up after
        /// about 5 s. The server answers on the main thread, which a million-block world freezes for longer than
        /// that while it loads, so the client gave up and the game went back to the menu. Allow about 60 s.
        /// </summary>
        [HarmonyPatch(typeof(LiteNetLib.NetManager), nameof(LiteNetLib.NetManager.Connect),
            typeof(System.Net.IPEndPoint), typeof(LiteNetLib.Utils.NetDataWriter))]
        [HarmonyPrefix]
        private static void PatientConnect(LiteNetLib.NetManager __instance)
        {
            if (__instance.MaxConnectAttempts * __instance.ReconnectDelay < 60000)
                __instance.MaxConnectAttempts = 60000 / System.Math.Max(1, __instance.ReconnectDelay);
        }

        // ---------- timing ----------

        private static readonly Stopwatch LoadClock = new Stopwatch(), StateClock = new Stopwatch();

        [HarmonyPatch(typeof(WorldSave), "Load")]
        [HarmonyPrefix]
        private static void LoadStart() => LoadClock.Restart();

        [HarmonyPatch(typeof(WorldSave), "Load")]
        [HarmonyPostfix]
        private static void LoadEnd()
        {
            if (StatueWorld.Map != null) Plugin.Log.LogInfo($"[perf] Loading the save took {LoadClock.ElapsedMilliseconds:N0} ms.");
        }

        [HarmonyPatch(typeof(PyramidState), nameof(PyramidState.ServerLoad), typeof(ulong[]), typeof(int[]), typeof(int[]))]
        [HarmonyPrefix]
        private static void StateStart() => StateClock.Restart();

        [HarmonyPatch(typeof(PyramidState), nameof(PyramidState.ServerLoad), typeof(ulong[]), typeof(int[]), typeof(int[]))]
        [HarmonyPostfix]
        private static void StateEnd()
        {
            if (StatueWorld.Map != null) Plugin.Log.LogInfo($"[perf] Restoring the pyramid's blocks took {StateClock.ElapsedMilliseconds:N0} ms.");
        }

        [HarmonyPatch(typeof(VersionAuthenticator), "RefuseJoin")]
        [HarmonyPrefix]
        private static void LogRefusal(string reason, string who) =>
            Plugin.Log.LogWarning($"Join refused ({who}): {reason}");
    }
}

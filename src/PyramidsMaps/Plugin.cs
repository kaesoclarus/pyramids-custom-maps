using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PyramidsMaps
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "pyramidsmaps.custommaps";
        public const string Name = "Pyramids Custom Maps";
        public const string Version = "0.5.0";

        /// <summary>Game build this mod was written against.</summary>
        public const string TestedGameVersion = "0.43.0";

        internal static ManualLogSource Log;
        internal static string PluginDir;
        internal static ConfigEntry<string> NewWorldMap;
        internal static ConfigEntry<bool> ShowMarkers;
        internal static ConfigEntry<bool> DevTools;

        private ConfigEntry<bool> _dumpScene;

        private void Awake()
        {
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Info.Location);

            NewWorldMap = Config.Bind("Maps", "NewWorldMap", "",
                "Map for worlds you START from now on: a map id (\"sphinx\" is the Great Cat) or empty for the normal pyramid. " +
                "Existing worlds keep the map they were started with.");
            ShowMarkers = Config.Bind("Maps", "ShowMarkers", true,
                "Statue worlds (Great Cat and other custom maps): show small markers on the spots next to your bricks where you can build. F7 toggles in game.");
            DevTools = Config.Bind("Debug", "DevTools", false,
                "Testing only, in a throwaway world: F9 twice (host, statue world) fills every block except the final one.");
            _dumpScene = Config.Bind("Debug", "DumpSceneOnLoad", false,
                "Write the scene hierarchy, map components and classic.json to the plugin's dump folder when the game scene loads.");

            Log.LogInfo($"{Name} {Version} loaded. Game version {Application.version}, Unity {Application.unityVersion}. New worlds: '{NewWorldMap.Value}'.");
            if (Application.version != TestedGameVersion)
                Log.LogWarning($"Written for game version {TestedGameVersion}; this is {Application.version}. Things may break.");

            // Patch each group on its own, so one that no longer fits the game (after an update) doesn't stop the rest
            var harmony = new Harmony(Guid);
            int ok = 0, failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                try { harmony.CreateClassProcessor(type).Patch(); ok++; }
                catch (Exception e) { failed++; Log.LogError($"Patch group {type.Name} failed to apply: {e.InnerException?.Message ?? e.Message}"); }
            }
            Log.LogInfo($"Patch groups applied: {ok}" + (failed > 0 ? $", FAILED: {failed}" : "") + ".");
            Maps.MapRegistry.CheckCodes();
            gameObject.AddComponent<Statues.StatueDiagnostics>();
            gameObject.AddComponent<Statues.PhotoMode>();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogInfo($"Scene loaded: '{scene.name}' (index {scene.buildIndex}, {mode}).");
            if (!_dumpScene.Value) return;
            try
            {
                SceneDump.Write(scene, Path.Combine(PluginDir, "dump"));
            }
            catch (Exception e)
            {
                Log.LogError($"Scene dump failed: {e}");
            }
        }
    }
}

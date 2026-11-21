using System;
using System.IO;
using UnityEngine;

namespace PyramidsMaps.Maps
{
    /// <summary>
    /// Remembers which custom map each save slot uses, in small sidecar files kept apart from the game's saves:
    /// %USERPROFILE%\AppData\LocalLow\PattezGames\How They Built the Pyramids\PyramidsMaps\worlds\&lt;slot&gt;.map
    /// A slot without a sidecar is a normal world and is never touched.
    /// </summary>
    internal static class WorldMaps
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "PyramidsMaps", "worlds");

        private static string PathOf(string slot) => Path.Combine(Folder, Sanitize(slot) + ".map");

        /// <summary>The map id stored for a slot, or null for a normal world.</summary>
        public static string Get(string slot) => GetValue(slot, "map");

        /// <summary>A value from the slot's marker file ("key=value" lines), or null.</summary>
        public static string GetValue(string slot, string key)
        {
            if (string.IsNullOrEmpty(slot)) return null;
            try
            {
                var p = PathOf(slot);
                if (!File.Exists(p)) return null;
                foreach (var line in File.ReadAllLines(p))
                    if (line.StartsWith(key + "=", StringComparison.Ordinal)) return line.Substring(key.Length + 1).Trim();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not read the map of slot {slot}: {e.Message}");
            }
            return null;
        }

        public static void Set(string slot, string mapId)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(PathOf(slot), new[]
            {
                "map=" + mapId,
                "shape=" + Shapes.SphinxShape.Version,
                "created=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "mod=" + Plugin.Version,
                "game=" + Application.version,
            });
            Plugin.Log.LogInfo($"Slot {slot} is now a '{mapId}' world.");
        }

        public static void Clear(string slot)
        {
            try
            {
                var p = PathOf(slot);
                if (File.Exists(p))
                {
                    File.Delete(p);
                    Plugin.Log.LogInfo($"Slot {slot} is a new normal world: removed its old map marker.");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not clear the map of slot {slot}: {e.Message}");
            }
        }

        private static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }
    }
}

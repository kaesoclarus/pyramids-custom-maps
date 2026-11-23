using System;
using System.IO;
using System.Reflection;
using BepInEx;

namespace PyramidsMaps
{
    /// <summary>
    /// The mod ships as two plugins built from this same code: <b>Statues</b> (the maps with statue blocks, 1 m
    /// wide: the cat, Sobek, Anubis, Horus, Giza, the Colossi, Sphinx Avenue) and <b>More Pyramids</b> (the maps with
    /// the game's 2 m pyramid blocks: the upside-down pyramids, Diamond, Step, Bent, Nubian, Ziggurat, Colossal and
    /// Million-Block). Which one this build is comes from the PACK_PYRAMIDS constant (see the .csproj).
    ///
    /// Each DLL holds every map's code; the pack only decides which maps the main menu offers for new worlds.
    /// Worlds already started on a map, and worlds joined from a host, always get their map, so nothing breaks
    /// when someone has the other mod, or only one of them.
    ///
    /// With both installed, only one of them runs (the newer; the Statues one if they're the same version), and it
    /// offers every map and reads both mods' player-map folders. The other one stays idle.
    /// </summary>
    internal static class Packs
    {
        public const string StatuesGuid = "pyramidsmaps.custommaps", PyramidsGuid = "pyramidsmaps.morepyramids";
        public const string StatuesName = "Pyramids Custom Maps: Statues", PyramidsName = "Pyramids Custom Maps: More Pyramids";
        public const string StatuesDll = "PyramidsMaps.dll", PyramidsDll = "PyramidsMorePyramids.dll";

#if PACK_PYRAMIDS
        public const bool IsStatuesBuild = false;
        public const string Guid = PyramidsGuid, Name = PyramidsName;
        private const string OtherDll = StatuesDll, OtherName = StatuesName;
#else
        public const bool IsStatuesBuild = true;
        public const string Guid = StatuesGuid, Name = StatuesName;
        private const string OtherDll = PyramidsDll, OtherName = PyramidsName;
#endif

        /// <summary>Which maps the menu offers.</summary>
        public static bool Statues = IsStatuesBuild, Pyramids = !IsStatuesBuild;

        /// <summary>The other mod's folder when it's installed too (and this one runs for both), else null.</summary>
        public static string OtherDir;

        /// <summary>
        /// Looks for the other mod. Returns false when it's installed and should run instead of this one; this
        /// plugin then does nothing. Both plugins make the same choice on their own.
        /// </summary>
        public static bool ShouldRun(string myPath, out string note)
        {
            note = null;
            string other = null;
            try
            {
                foreach (var f in Directory.GetFiles(Paths.PluginPath, OtherDll, SearchOption.AllDirectories)) { other = f; break; }
            }
            catch (Exception e) { note = $"Couldn't look for {OtherName}: {e.Message}"; }
            if (other == null) return true;

            Version mine = VersionOf(myPath), theirs = VersionOf(other);
            bool otherRuns = theirs > mine || (theirs == mine && !IsStatuesBuild);
            if (otherRuns)
            {
                note = $"{OtherName} {theirs} is installed too; it runs every map of both, so this one ({mine}) stays idle.";
                return false;
            }
            Statues = Pyramids = true;
            OtherDir = Path.GetDirectoryName(other);
            note = $"{OtherName} {theirs} is installed too: this one ({mine}) runs every map of both" +
                   (theirs < mine ? ", as the newer one." : ".");
            return true;
        }

        private static Version VersionOf(string dll)
        {
            try { return AssemblyName.GetAssemblyName(dll).Version; }
            catch { return new Version(0, 0); }
        }

        /// <summary>Whether the menu offers this map for new worlds. Player maps come with either mod.</summary>
        public static bool Offers(Maps.MapRegistry.MapInfo m) =>
            m.IsUserMap || (m.IsPyramidBlocks ? Pyramids : Statues);
    }
}

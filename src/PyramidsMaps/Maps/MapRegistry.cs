using System.Collections.Generic;

namespace PyramidsMaps.Maps
{
    /// <summary>
    /// The custom maps a new world can be started with. Add a new map here (and its world logic) and it shows up
    /// as a button on the main menu's empty plot, next to the game's own Pyramid and Grand Pyramid.
    /// </summary>
    internal static class MapRegistry
    {
        public sealed class MapInfo
        {
            /// <summary>Stable id, stored in each world's marker file and in the NewWorldMap setting. Never change it.</summary>
            public string Id;
            /// <summary>Button label on the empty plot.</summary>
            public string Title;
            /// <summary>Description shown on the empty plot when it's chosen.</summary>
            public string Body;
        }

        public static readonly IReadOnlyList<MapInfo> All = new[]
        {
            new MapInfo
            {
                Id = Sphinx.SphinxWorld.MapId,
                Title = "Great Cat",
                Body = "A giant stone cat, crouched to pounce: 24,052 blocks you can lay in any order. (Pyramids Custom Maps mod)",
            },
        };

        public static MapInfo Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var m in All) if (m.Id == id) return m;
            return null;
        }

        /// <summary>The map new worlds will use (from the NewWorldMap setting), or null for the game's own.</summary>
        public static MapInfo ForNewWorlds => Find((Plugin.NewWorldMap.Value ?? "").Trim().ToLowerInvariant());

        public static void ChooseForNewWorlds(MapInfo map) => Plugin.NewWorldMap.Value = map?.Id ?? "";
    }
}

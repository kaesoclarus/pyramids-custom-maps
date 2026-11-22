using System;
using System.Collections.Generic;
using PyramidsMaps.Shapes;

namespace PyramidsMaps.Maps
{
    /// <summary>
    /// The custom maps a new world can be started with. Each one replaces the pyramid with a statue built brick
    /// by brick. Add a map here (with its shape) and it shows up as a button on the main menu's empty plot, next
    /// to the game's own Pyramid and Grand Pyramid.
    /// </summary>
    internal static class MapRegistry
    {
        public sealed class MapInfo
        {
            /// <summary>Stable id, stored in each world's marker file and in the NewWorldMap setting. Never change it.</summary>
            public string Id;
            /// <summary>
            /// Stable number 0–3, sent to joining players in the synced world-kind byte. Never change or reuse it.
            /// </summary>
            public int SyncIndex;
            /// <summary>Button label on the empty plot.</summary>
            public string Title;
            /// <summary>Description shown on the empty plot when it's chosen.</summary>
            public string Body;
            /// <summary>The statue in running text, e.g. in "Put the Work Post up on the cat".</summary>
            public string StatueName;
            /// <summary>The shape's version (0–15): bump when the shape changes, so old worlds aren't loaded onto it.</summary>
            public int ShapeVersion;
            /// <summary>Block size in metres (width and depth, height).</summary>
            public double CellW, CellH;
            /// <summary>Builds the statue's blocks.</summary>
            public Func<Voxels> Build;
            /// <summary>Optional: called when a world of this map starts or ends (e.g. to add scenery).</summary>
            public Action OnActivate, OnDeactivate;
        }

        public static readonly IReadOnlyList<MapInfo> All = new[]
        {
            new MapInfo
            {
                Id = "sphinx",
                SyncIndex = 0,
                Title = "Great Cat",
                Body = "A giant stone cat, crouched to pounce: 24,052 blocks you can lay in any order. (Pyramids Custom Maps mod)",
                StatueName = "the cat",
                ShapeVersion = SphinxShape.Version,
                CellW = SphinxShape.CellW, CellH = SphinxShape.CellH,
                Build = SphinxShape.Build,
            },
            new MapInfo
            {
                Id = "sobek",
                SyncIndex = 1,
                Title = "Sobek",
                Body = "A giant crocodile lying in his pool, jaws open to the sky: 19,538 blocks you can lay in any order. (Pyramids Custom Maps mod)",
                StatueName = "Sobek",
                ShapeVersion = SobekShape.Version,
                CellW = SobekShape.CellW, CellH = SobekShape.CellH,
                Build = SobekShape.Build,
                OnActivate = Statues.SobekPool.Show,
                OnDeactivate = Statues.SobekPool.Hide,
            },
            new MapInfo
            {
                Id = "anubis",
                SyncIndex = 2,
                Title = "Anubis",
                Body = "A giant jackal keeping watch from his shrine, ears high: 11,588 blocks you can lay in any order. (Pyramids Custom Maps mod)",
                StatueName = "Anubis",
                ShapeVersion = AnubisShape.Version,
                CellW = AnubisShape.CellW, CellH = AnubisShape.CellH,
                Build = AnubisShape.Build,
            },
            new MapInfo
            {
                Id = "horus",
                // The last free index: the synced byte has room for 4 maps (0–3). A fifth map needs a new layout.
                SyncIndex = 3,
                Title = "Horus",
                Body = "A giant falcon with outstretched wings, wearing the double crown: 47,679 blocks, 100 m tall, any order. (Pyramids Custom Maps mod)",
                StatueName = "Horus",
                ShapeVersion = HorusShape.Version,
                CellW = HorusShape.CellW, CellH = HorusShape.CellH,
                Build = HorusShape.Build,
            },
        };

        public static MapInfo Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            id = id.Trim().ToLowerInvariant();
            foreach (var m in All) if (m.Id == id) return m;
            return null;
        }

        public static MapInfo BySyncIndex(int index)
        {
            foreach (var m in All) if (m.SyncIndex == index) return m;
            return null;
        }

        public static string KnownIds => string.Join(", ", Array.ConvertAll(new List<MapInfo>(All).ToArray(), m => m.Id));

        /// <summary>The map new worlds will use (from the NewWorldMap setting), or null for the game's own.</summary>
        public static MapInfo ForNewWorlds => Find(Plugin.NewWorldMap.Value);

        public static void ChooseForNewWorlds(MapInfo map) => Plugin.NewWorldMap.Value = map?.Id ?? "";
    }
}

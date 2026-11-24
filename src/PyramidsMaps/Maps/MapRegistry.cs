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
            /// Number 0–63 sent to joining players in the synced world-kind byte, so they build the same map. Never
            /// reuse one. 0 and 1 (the cat and Sobek, released in 0.3.0/0.4.0) use the old byte layout, which also
            /// carries the shape version; every other map must get a NEW code whenever its ShapeVersion changes.
            /// </summary>
            public int SyncCode;
            /// <summary>Button label on the empty plot.</summary>
            public string Title;
            /// <summary>Description shown on the empty plot when it's chosen.</summary>
            public string Body;
            /// <summary>The statue in running text, e.g. in "Put the Work Post up on the cat".</summary>
            public string StatueName;
            /// <summary>
            /// The map's full name in running text, where the game says "the Great Pyramid" (milestone banners: "25% of
            /// the Million-Block Pyramid"). Unset: "the " + Title.
            /// </summary>
            public string NameInText;
            public string Called => NameInText ?? "the " + Title;
            /// <summary>The shape's version (0–15): bump when the shape changes, so old worlds aren't loaded onto it.</summary>
            public int ShapeVersion;
            /// <summary>Block size in metres (width and depth, height).</summary>
            public double CellW, CellH;
            /// <summary>Builds the statue's blocks. Null for a pyramid map (the game's own pyramid, resized).</summary>
            public Func<Voxels> Build;
            /// <summary>Optional: called when a world of this map starts or ends (e.g. to add scenery).</summary>
            public Action OnActivate, OnDeactivate;
            /// <summary>
            /// Optional: called after the game has finished switching to the world (it re-enables the normal map's
            /// walls and scenery during the switch, so changes to those go here). May run more than once.
            /// </summary>
            public Action OnApplied;

            /// <summary>A pyramid map: base width in pyramid blocks (odd). 0 for a statue map.</summary>
            public int PyramidBase;
            /// <summary>Optional: where the build site's centre goes (world z), instead of the normal one.</summary>
            public double? SiteZ;
            /// <summary>Optional: the walkable area (world x/z), instead of the normal one.</summary>
            public (float minX, float minZ, float maxX, float maxZ)? PlayArea;
            /// <summary>Only offered on the menu with DevTools on (test maps).</summary>
            public bool DevOnly;
            /// <summary>
            /// Optional: ground under the statue stays free for machines and routes where its underside is at least
            /// this high (metres). Unset: every column with blocks takes up the ground.
            /// </summary>
            public double? GroundClearance;

            public bool IsStatue => Build != null;
            /// <summary>Built with the game's 2 m pyramid blocks (the More Pyramids mod), not statue blocks.</summary>
            public bool IsPyramidBlocks => PyramidBase > 0 || CellW >= 2;
            /// <summary>A player-made map from a .vox file (sync codes 32–63).</summary>
            public bool IsUserMap => SyncCode >= 32;
        }

        /// <summary>Every map: the built-in ones, then the player-made ones from the maps folder (loaded once).</summary>
        public static IReadOnlyList<MapInfo> All => _all ?? (_all = Combine());

        private static List<MapInfo> _all;

        private static List<MapInfo> Combine()
        {
            var list = new List<MapInfo>(BuiltIn);
            list.AddRange(UserMaps.Load());
            return list;
        }

        private static readonly MapInfo[] BuiltIn = new[]
        {
            new MapInfo
            {
                Id = "sphinx",
                SyncCode = 0,
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
                SyncCode = 1,
                Title = "Sobek",
                NameInText = "Sobek",
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
                SyncCode = 2,
                Title = "Anubis",
                NameInText = "Anubis",
                Body = "A giant jackal keeping watch from his shrine, ears high: 11,588 blocks you can lay in any order. (Pyramids Custom Maps mod)",
                StatueName = "Anubis",
                ShapeVersion = AnubisShape.Version,
                CellW = AnubisShape.CellW, CellH = AnubisShape.CellH,
                Build = AnubisShape.Build,
            },
            new MapInfo
            {
                Id = "horus",
                SyncCode = 3,
                Title = "Horus",
                NameInText = "Horus",
                Body = "A giant falcon with outstretched wings, wearing the double crown: 47,679 blocks, 100 m tall, any order. (Pyramids Custom Maps mod)",
                StatueName = "Horus",
                ShapeVersion = HorusShape.Version,
                CellW = HorusShape.CellW, CellH = HorusShape.CellH,
                Build = HorusShape.Build,
            },
            new MapInfo
            {
                Id = "inverted",
                SyncCode = 5,
                Title = "Upside-Down",
                NameInText = "the Upside-Down Pyramid",
                Body = "The classic pyramid's own blocks, upside down on its tip: build up to a plaza 65 m in the air and crown it. 23,427 blocks, any order. (Pyramids Custom Maps mod)",
                StatueName = "the pyramid",
                ShapeVersion = InvertedShape.Version,
                CellW = InvertedShape.CellW, CellH = InvertedShape.CellH,
                Build = InvertedShape.Build,
                GroundClearance = InvertedShape.GroundClearance,
            },
            new MapInfo
            {
                // The Grand Pyramid upside down: its 170 m plaza reaches a little past the normal play area
                // (x ±85 against ±81, back edge z −129 against −124), so the play area widens and the walls come down.
                Id = "inverted-grand",
                SyncCode = 6,
                Title = "Upside-Down Grand",
                NameInText = "the Upside-Down Grand Pyramid",
                Body = "The Grand Pyramid's own blocks, upside down on its tip: build up to a 170 m plaza 107 m in the air. 105,996 blocks, any order. (Pyramids Custom Maps mod)",
                StatueName = "the pyramid",
                ShapeVersion = InvertedShape.GrandVersion,
                CellW = InvertedShape.CellW, CellH = InvertedShape.CellH,
                Build = InvertedShape.BuildGrand,
                GroundClearance = InvertedShape.GroundClearance,
                PlayArea = (-92f, -136f, 92f, 46.4f),
                OnApplied = Statues.MapEdges.Open,
                OnDeactivate = Statues.MapEdges.Close,
            },
            new MapInfo
            {
                Id = "diamond",
                SyncCode = 7,
                Title = "Diamond",
                Body = "An upside-down pyramid with a pyramid standing on its plaza: a 127 m diamond balanced on its point. 44,251 blocks, any order. (Pyramids Custom Maps mod)",
                StatueName = "the diamond",
                ShapeVersion = InvertedShape.DiamondVersion,
                CellW = InvertedShape.CellW, CellH = InvertedShape.CellH,
                Build = InvertedShape.BuildDiamond,
                GroundClearance = InvertedShape.GroundClearance,
            },
            new MapInfo
            {
                Id = "step",
                SyncCode = 8,
                Title = "Step Pyramid",
                Body = "Djoser's Step Pyramid, the first pyramid ever: six tiers rising 60 m on a 122 m base. 38,921 blocks, any order. (Pyramids Custom Maps mod)",
                StatueName = "the pyramid",
                ShapeVersion = PyramidVariants.StepVersion,
                CellW = PyramidVariants.CellW, CellH = PyramidVariants.CellH,
                Build = PyramidVariants.BuildStep,
            },
            new MapInfo
            {
                Id = "bent",
                SyncCode = 9,
                Title = "Bent Pyramid",
                Body = "The Bent Pyramid: steep at the bottom, gentler above, after its builders changed their minds halfway up. 38,235 blocks, any order. (Pyramids Custom Maps mod)",
                StatueName = "the pyramid",
                ShapeVersion = PyramidVariants.BentVersion,
                CellW = PyramidVariants.CellW, CellH = PyramidVariants.CellH,
                Build = PyramidVariants.BuildBent,
            },
            new MapInfo
            {
                Id = "nubian",
                SyncCode = 10,
                Title = "Nubian Field",
                Body = "A royal cemetery of a dozen steep Nubian pyramids, like those at Meroe. Finish on the tallest. 7,684 blocks, any order. (Pyramids Custom Maps mod)",
                StatueName = "the pyramids",
                ShapeVersion = PyramidVariants.NubianVersion,
                CellW = PyramidVariants.CellW, CellH = PyramidVariants.CellH,
                Build = PyramidVariants.BuildNubian,
            },
            new MapInfo
            {
                Id = "ziggurat",
                SyncCode = 11,
                Title = "Ziggurat",
                Body = "A Mesopotamian ziggurat: three terraces, a grand stairway and a shrine on top, 45 m high. 11,943 blocks, any order. (Pyramids Custom Maps mod)",
                StatueName = "the ziggurat",
                ShapeVersion = PyramidVariants.ZigguratVersion,
                CellW = PyramidVariants.CellW, CellH = PyramidVariants.CellH,
                Build = PyramidVariants.BuildZiggurat,
            },
            new MapInfo
            {
                Id = "giza",
                SyncCode = 12,
                Title = "Giza",
                NameInText = "Giza",
                Body = "The Giza plateau: Khufu, Khafre and Menkaure, and the Great Sphinx keeping watch. Build them in any order; Khufu's tip is the last brick. 63,213 blocks. (Pyramids Custom Maps mod)",
                StatueName = "the monuments",
                ShapeVersion = GizaShape.Version,
                CellW = GizaShape.CellW, CellH = GizaShape.CellH,
                Build = GizaShape.Build,
            },
            new MapInfo
            {
                Id = "colossi",
                SyncCode = 13,
                Title = "Colossi",
                Body = "The Colossi of Memnon: two pharaohs seated on their thrones, 38 m tall. The sun disc on the right one is the last brick. 10,776 blocks, any order. (Pyramids Custom Maps mod)",
                StatueName = "the colossi",
                ShapeVersion = MonumentShapes.ColossiVersion,
                CellW = MonumentShapes.CellW, CellH = MonumentShapes.CellH,
                Build = MonumentShapes.BuildColossi,
            },
            new MapInfo
            {
                Id = "avenue",
                SyncCode = 14,
                Title = "Sphinx Avenue",
                Body = "A processional road lined with twelve sphinxes, leading to a 37 m obelisk whose tip is the last brick. 4,346 blocks, any order. (Pyramids Custom Maps mod)",
                StatueName = "the avenue",
                ShapeVersion = MonumentShapes.AvenueVersion,
                CellW = MonumentShapes.CellW, CellH = MonumentShapes.CellH,
                Build = MonumentShapes.BuildAvenue,
            },
            new MapInfo
            {
                // The game's own pyramid at about 4.5x the Grand Pyramid's blocks (base 141 = 282 m, 71 layers = 178 m,
                // 477,191 blocks), moved south so it fits the terrain. Began as a performance test (hence the id, kept
                // so worlds made with it still load): 60 fps finished, an 85 KB save.
                Id = "colossal-test",
                SyncCode = 4,
                Title = "Colossal Pyramid",
                Body = "The pyramid at about 4.5 times the Grand Pyramid: 477,191 blocks, 178 m tall, on a plain cleared south of the river. A very long build. (Pyramids Custom Maps mod)",
                StatueName = "the pyramid",
                ShapeVersion = 1,
                PyramidBase = 141,
                SiteZ = -130,
                PlayArea = (-172f, -296f, 172f, 46.4f),

                OnApplied = Statues.ColossalTest.Show,
                OnDeactivate = Statues.ColossalTest.Hide,
            },
            new MapInfo
            {
                // The game's own pyramid with over a million blocks: base 181 (362 m), 91 layers (227.5 m),
                // 1,004,731 blocks, about 9.5x the Grand Pyramid. Too big for the map, so ColossalTest adds a
                // terrain tile south of it; the north edge sits where the Colossal Pyramid's does (z 11).
                Id = "million",
                SyncCode = 15,
                Title = "Million-Block Pyramid",
                Body = "The pyramid with over a million blocks: 1,004,731 of them, 227 m tall on a 362 m base, about 9.5 times the Grand Pyramid. The desert is extended south to fit it. The longest build there is. (Pyramids Custom Maps mod)",
                StatueName = "the pyramid",
                ShapeVersion = 1,
                PyramidBase = 181,
                SiteZ = -170,
                PlayArea = (-212f, -376f, 212f, 46.4f),

                OnApplied = Statues.ColossalTest.Show,
                OnDeactivate = Statues.ColossalTest.Hide,
            },
        };

        /// <summary>The maps offered on the main menu: the installed mod's (see <see cref="Packs"/>); test maps only with DevTools on.</summary>
        public static IEnumerable<MapInfo> Visible
        {
            get { foreach (var m in All) if (IsOffered(m)) yield return m; }
        }

        public static bool IsOffered(MapInfo m) => m != null && Packs.Offers(m) && (!m.DevOnly || Plugin.DevTools.Value);

        public static MapInfo Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            id = id.Trim().ToLowerInvariant();
            foreach (var m in All) if (m.Id == id) return m;
            return null;
        }

        /// <summary>Logs an error for any map code used twice or out of range (called once at startup).</summary>
        public static void CheckCodes()
        {
            var seen = new Dictionary<int, string>();
            foreach (var m in All)
            {
                if (m.SyncCode < 0 || m.SyncCode > 63) Plugin.Log.LogError($"Map {m.Id}: sync code {m.SyncCode} is outside 0–63.");
                else if (seen.TryGetValue(m.SyncCode, out var other)) Plugin.Log.LogError($"Maps {other} and {m.Id} share sync code {m.SyncCode}: multiplayer would mix them up.");
                else seen[m.SyncCode] = m.Id;
            }
        }

        public static MapInfo BySyncCode(int code)
        {
            foreach (var m in All) if (m.SyncCode == code) return m;
            return null;
        }

        public static string KnownIds => string.Join(", ", Array.ConvertAll(new List<MapInfo>(All).ToArray(), m => m.Id));

        /// <summary>The map new worlds will use (from the NewWorldMap setting), or null for the game's own.</summary>
        public static MapInfo ForNewWorlds { get { var m = Find(Plugin.NewWorldMap.Value); return IsOffered(m) ? m : null; } }

        public static void ChooseForNewWorlds(MapInfo map) => Plugin.NewWorldMap.Value = map?.Id ?? "";
    }
}

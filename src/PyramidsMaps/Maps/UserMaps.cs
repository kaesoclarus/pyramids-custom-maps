using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using PyramidsMaps.Shapes;

namespace PyramidsMaps.Maps
{
    /// <summary>
    /// Player-made maps: every MagicaVoxel .vox file in BepInEx\plugins\PyramidsMaps\maps becomes a map in the
    /// menu's dropdown. An optional text file with the same name sets its title, description and block size.
    ///  - Each map is identified by its file's contents (a hash): saves made with it refuse to load if the file
    ///    changes, and in multiplayer the host sends a code from the hash, so joining players need the same file.
    ///  - After loading, a background check runs the build-by-hand simulation and logs any blocks that can't be
    ///    reached, so map makers hear about problems before players do.
    /// </summary>
    internal static class UserMaps
    {
        public static string Folder => Path.Combine(Plugin.PluginDir, "maps");

        /// <summary>Player maps use sync codes 32–63, from their hash (built-in maps use 0–31).</summary>
        private const int FirstCode = 32, Codes = 32;

        /// <summary>The normal play area around the site (site-local metres): x ±81, z −80 … +90.</summary>
        private const double PlayHalfX = 81, PlayBack = 80, PlayFront = 90;

        public static List<MapRegistry.MapInfo> Load()
        {
            var maps = new List<MapRegistry.MapInfo>();
            try
            {
                Directory.CreateDirectory(Folder);
                WriteReadme();
                var files = Directory.GetFiles(Folder, "*.vox");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    try { maps.Add(LoadOne(file)); }
                    catch (Exception e) { Plugin.Log.LogError($"Player map {Path.GetFileName(file)}: can't use it: {e.Message}"); }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Player maps: couldn't read {Folder}: {e.Message}");
            }
            if (maps.Count > 0)
            {
                Plugin.Log.LogInfo($"Player maps: loaded {maps.Count} from {Folder}.");
                CheckInBackground(maps);
            }
            return maps;
        }

        private static MapRegistry.MapInfo LoadOne(string file)
        {
            var bytes = File.ReadAllBytes(file);
            var model = VoxFile.Read(bytes);
            var shape = VoxFile.ToVoxels(model);
            uint hash = VoxFile.Hash(bytes);
            string name = Path.GetFileNameWithoutExtension(file);
            var opts = ReadOptions(Path.ChangeExtension(file, ".txt"));

            bool pyramidBlocks = opts.TryGetValue("blocks", out var blocks) && blocks.Trim().ToLowerInvariant() == "pyramid";
            double cellW = pyramidBlocks ? 2.0 : 1.0, cellH = pyramidBlocks ? 2.5 : 1.25;
            int count = shape.CellCount;
            string title = opts.TryGetValue("title", out var t) && t.Trim().Length > 0 ? t.Trim() : Nice(name);
            string body = opts.TryGetValue("description", out var d) && d.Trim().Length > 0
                ? $"{d.Trim()} {count:N0} blocks. (player map: {name}.vox)"
                : $"A player-made map ({name}.vox): {count:N0} blocks, any order. (Pyramids Custom Maps mod)";

            var map = new MapRegistry.MapInfo
            {
                Id = "vox-" + Sanitize(name),
                SyncCode = FirstCode + (int)(hash % Codes),
                Title = title,
                Body = body,
                StatueName = opts.TryGetValue("name", out var sn) && sn.Trim().Length > 0 ? sn.Trim() : "the statue",
                // the file's hash: a world started with this map won't load onto an edited file
                ShapeVersion = (int)(hash & 0x7FFFFFFF),
                CellW = cellW, CellH = cellH,
                Build = () => VoxFile.ToVoxels(VoxFile.Read(bytes)),
            };
            if (opts.TryGetValue("clearance", out var c) && double.TryParse(c.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double clear))
                map.GroundClearance = clear;

            if (model.ModelCount > 1)
                Plugin.Log.LogWarning($"Player map {name}.vox has {model.ModelCount} models; only the first is used.");
            double halfX = shape.Radius * cellW, depth = shape.Radius * cellW;
            if (halfX > PlayHalfX || depth > PlayBack)
                Plugin.Log.LogWarning($"Player map {name}.vox is about {2 * halfX:0} m across; parts may stand outside the play area " +
                                      $"(it reaches {PlayHalfX:0} m to each side and {PlayBack:0} m behind the site).");
            Plugin.Log.LogInfo($"Player map {name}.vox: '{title}', {count:N0} blocks, {shape.Height} layers, " +
                               $"{(pyramidBlocks ? "pyramid" : "statue")} blocks, multiplayer code {map.SyncCode}.");
            return map;
        }

        /// <summary>key=value lines from the map's optional text file.</summary>
        private static Dictionary<string, string> ReadOptions(string path)
        {
            var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return o;
            foreach (var line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.TrimStart().StartsWith("#")) continue;
                o[line.Substring(0, eq).Trim()] = line.Substring(eq + 1);
            }
            return o;
        }

        private static string Nice(string name)
        {
            var s = name.Replace('_', ' ').Replace('-', ' ').Trim();
            return s.Length == 0 ? "Player map" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s);
        }

        private static string Sanitize(string name)
        {
            var sb = new StringBuilder();
            foreach (char ch in name.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(ch) ? ch : '-');
            return sb.ToString();
        }

        /// <summary>Runs the build-by-hand check for each player map off the main thread, one at a time.</summary>
        private static void CheckInBackground(List<MapRegistry.MapInfo> maps)
        {
            Task.Run(() =>
            {
                foreach (var m in maps)
                {
                    try
                    {
                        var v = m.Build();
                        bool pyramid = m.CellW >= 2;
                        Reach.MaxRise = 1.3; Reach.ReachCells = pyramid ? 3 : 6; Reach.ClimbLayers = pyramid ? 1 : 2;
                        var left = Reach.GreedyAnyOrder(v, true, out _);
                        if (left.Count == 0)
                            Plugin.Log.LogInfo($"Player map '{m.Title}': every block can be built by hand.");
                        else
                        {
                            var (x, y, z) = left[0];
                            Plugin.Log.LogWarning($"Player map '{m.Title}': {left.Count:N0} blocks can't be reached when building by hand " +
                                                  $"(for example layer {y} at x {x}, z {z}). Players can't finish it; add steps or supports in the model.");
                        }
                    }
                    catch (Exception e) { Plugin.Log.LogError($"Player map '{m.Title}': the build check failed: {e.Message}"); }
                }
            });
        }

        private static void WriteReadme()
        {
            string path = Path.Combine(Folder, "README.txt");
            if (File.Exists(path)) return;
            File.WriteAllText(path,
@"PLAYER-MADE MAPS (Pyramids Custom Maps)

Put MagicaVoxel models (.vox) in this folder. Each one becomes a map in the main menu's
""Custom map"" dropdown, named after its file. Restart the game after adding or changing one.

THE MODEL
- MagicaVoxel's front faces the river; its up is up. The model is centred on the build site and
  its lowest voxels stand on the ground. Colours are ignored. Only the first model in a file is used.
- Each voxel is one block: 1 x 1 x 1.25 m (statue blocks) unless the options say otherwise.
- Players build in any order, but every block must touch one already laid (or the ground), and
  they can only place a block about one layer above their feet. The mod checks this when the game
  starts and writes any blocks that can't be reached to BepInEx\LogOutput.log.
- The normal play area is 162 m wide and reaches about 80 m behind the site and 90 m in front.
- The highest block is the last brick (if several share the top layer, one of them is).

OPTIONS (optional): a text file with the same name, e.g. my_map.vox + my_map.txt:
  title=My Map
  description=A short line for the menu.
  blocks=pyramid          (2 x 2 x 2.5 m pyramid blocks instead of statue blocks)
  name=the statue         (how Shabti messages refer to it)
  clearance=8             (ground under overhangs at least this high stays free for machines)

MULTIPLAYER AND SAVES
- Everyone who plays together needs the same file. The host's game sends a code made from the
  file's contents; a player without the same file gets an error in the log.
- A world remembers the exact file it was started with. If you edit the file, that world won't
  load the map any more: keep a copy, or start a new world.
");
        }
    }
}

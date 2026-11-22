using System;
using System.Collections.Generic;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// Can every block be laid by hand? Simulates the build layer by layer: while layer L is laid, layers below it
    /// are complete. A player stands on top of a block (or the ground) with headroom, walks to neighbouring spots,
    /// climbs up to <see cref="ClimbLayers"/> layers at once and drops any height. To lay a block of layer L they
    /// must stand at level L or higher (the game's "1 m above your feet" rule) within <see cref="ReachCells"/>.
    /// </summary>
    public static class Reach
    {
        public static int ClimbLayers = 2;      // a pyramid step (2.5 m) is climbable: two 1.25 m layers
        public static int HeadroomLayers = 2;   // 2.5 m
        public static double ReachCells = 6;    // place range, metres at 1 m cells
        public static double CellH = 1.25;      // layer height in cells' width units (metres at 1 m cells)
        /// <summary>
        /// How far above your feet a block's base may be (metres). The game's rule is 1 m; null means
        /// "anywhere within reach in 3D" (the relaxed rule this mod can use for the cat).
        /// </summary>
        public static double? MaxRise = 1.0;

        public sealed class Report
        {
            public int Layer, Cells, Unreachable;
            public (int x, int z) Example;
        }

        public static List<Report> Check(Voxels v)
        {
            var reports = new List<Report>();
            int H = v.Height, R = v.Radius, N = v.Size;
            for (int L = 0; L < H; L++)
            {
                // spots: (x, z, level) where level = 0 (ground) or a block below at level − 1 (built: < L), and headroom
                bool Solid(int x, int y, int z) => y < L && v[x, y, z];
                var reach = new HashSet<(int, int, int)>();
                var queue = new Queue<(int x, int z, int y)>();
                for (int x = -R - 1; x <= R + 1; x++)
                    foreach (int z in new[] { -R - 1, R + 1 }) { reach.Add((x, z, 0)); queue.Enqueue((x, z, 0)); }
                for (int z = -R; z <= R; z++)
                    foreach (int x in new[] { -R - 1, R + 1 }) { reach.Add((x, z, 0)); queue.Enqueue((x, z, 0)); }

                bool Spot(int x, int z, int y)
                {
                    if (y < 0) return false;
                    if (y > 0 && !Solid(x, y - 1, z)) return false;
                    for (int k = 0; k < HeadroomLayers; k++) if (Solid(x, y + k, z)) return false;
                    return true;
                }

                while (queue.Count > 0)
                {
                    var (x, z, y) = queue.Dequeue();
                    foreach (var (dx, dz) in Dirs)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx < -R - 1 || nx > R + 1 || nz < -R - 1 || nz > R + 1) continue;
                        // any spot in the neighbouring column from (y − anything) up to y + ClimbLayers
                        for (int ny = Math.Min(L, y + ClimbLayers); ny >= 0; ny--)
                        {
                            if (!Spot(nx, nz, ny)) continue;
                            if (ny > y)
                            {
                                // climbing needs headroom above the start spot up to the new level
                                bool clear = true;
                                for (int k = y; k < ny + HeadroomLayers && clear; k++) if (Solid(x, k, z)) clear = false;
                                if (!clear) continue;
                            }
                            if (reach.Add((nx, nz, ny))) queue.Enqueue((nx, nz, ny));
                        }
                    }
                }

                // highest standing spot per column (feet level)
                var top = new Dictionary<(int, int), int>();
                foreach (var (x, z, y) in reach)
                    if (!top.TryGetValue((x, z), out int t) || y > t) top[(x, z)] = y;

                int cells = 0, bad = 0;
                (int, int) ex = default;
                int r = (int)Math.Ceiling(ReachCells);
                for (int x = -R; x <= R; x++)
                    for (int z = -R; z <= R; z++)
                    {
                        if (!v[x, L, z]) continue;
                        cells++;
                        bool ok = false;
                        for (int dx = -r; dx <= r && !ok; dx++)
                            for (int dz = -r; dz <= r && !ok; dz++)
                            {
                                if (!top.TryGetValue((x + dx, z + dz), out int feet)) continue;
                                double flat2 = dx * dx + dz * dz;
                                double rise = (L - feet) * CellH;            // block base above the feet (m)
                                if (MaxRise.HasValue)
                                    ok = rise <= MaxRise.Value + 1e-6 && flat2 + Math.Pow(Math.Max(0, -rise - CellH), 2) <= ReachCells * ReachCells;
                                else
                                    ok = flat2 + rise * rise <= ReachCells * ReachCells;
                            }
                        if (!ok) { bad++; ex = (x, z); }
                    }
                reports.Add(new Report { Layer = L, Cells = cells, Unreachable = bad, Example = ex });
            }
            return reports;
        }

        /// <summary>
        /// Any-order building: every block is open from the start, but a block must touch a laid block (or sit on
        /// layer 0). Repeatedly lays every block a player can reach on the structure built so far.
        /// Returns the blocks that never get laid (empty = the whole shape can be built by hand).
        /// </summary>
        public static List<(int x, int y, int z)> GreedyAnyOrder(Voxels target, out int waves) => GreedyAnyOrder(target, false, out waves);

        /// <summary>
        /// As above; with <paramref name="keepSteps"/> a spot once stood on stays usable, as when a player leaves a
        /// step open to climb on and fills it in later from above (the placement rules allow laying below your
        /// feet). The plain version lays everything it can at once and can bury its own way up.
        /// </summary>
        public static List<(int x, int y, int z)> GreedyAnyOrder(Voxels target, bool keepSteps, out int waves)
        {
            int R = target.Radius, H = target.Height;
            var built = new Voxels(R, target.Layers);
            int remaining = target.CellCount;
            var everStood = new HashSet<(int, int, int)>();
            waves = 0;
            while (remaining > 0)
            {
                var spots = StandingSpots(built, R, H, keepSteps ? everStood : null);
                if (keepSteps) { everStood.UnionWith(spots); spots = everStood; }
                var top = new Dictionary<(int, int), List<int>>();
                foreach (var (x, z, y) in spots)
                {
                    if (!top.TryGetValue((x, z), out var l)) top[(x, z)] = l = new List<int>();
                    l.Add(y);
                }
                var place = new List<(int, int, int)>();
                int r = (int)Math.Ceiling(ReachCells);
                for (int x = -R; x <= R; x++)
                    for (int y = 0; y < H; y++)
                        for (int z = -R; z <= R; z++)
                        {
                            if (!target[x, y, z] || built[x, y, z]) continue;
                            bool touches = y == 0 || built[x + 1, y, z] || built[x - 1, y, z] || built[x, y + 1, z] ||
                                           built[x, y - 1, z] || built[x, y, z + 1] || built[x, y, z - 1];
                            if (!touches) continue;
                            if (CanReach(top, x, y, z, r)) place.Add((x, y, z));
                        }
                if (place.Count == 0) break;
                foreach (var (x, y, z) in place) built[x, y, z] = true;
                remaining -= place.Count;
                waves++;
            }
            var left = new List<(int, int, int)>();
            for (int x = -R; x <= R; x++)
                for (int y = 0; y < H; y++)
                    for (int z = -R; z <= R; z++)
                        if (target[x, y, z] && !built[x, y, z]) left.Add((x, y, z));
            return left;
        }

        private static bool CanReach(Dictionary<(int, int), List<int>> top, int x, int y, int z, int r)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    double flat2 = dx * dx + dz * dz;
                    if (flat2 > ReachCells * ReachCells) continue;
                    if (!top.TryGetValue((x + dx, z + dz), out var feetList)) continue;
                    foreach (int feet in feetList)
                    {
                        double rise = (y - feet) * CellH;
                        bool ok = MaxRise.HasValue
                            ? rise <= MaxRise.Value + 1e-6 && flat2 + Math.Pow(Math.Max(0, -rise - CellH), 2) <= ReachCells * ReachCells
                            : flat2 + rise * rise <= ReachCells * ReachCells;
                        if (ok) return true;
                    }
                }
            return false;
        }

        /// <summary>Every spot a player can walk/climb to on the blocks built so far (x, z, feet level).</summary>
        private static HashSet<(int, int, int)> StandingSpots(Voxels built, int R, int H, HashSet<(int, int, int)> kept = null)
        {
            var reach = new HashSet<(int, int, int)>();
            var queue = new Queue<(int x, int z, int y)>();
            for (int x = -R - 1; x <= R + 1; x++)
                foreach (int z in new[] { -R - 1, R + 1 }) { reach.Add((x, z, 0)); queue.Enqueue((x, z, 0)); }
            for (int z = -R; z <= R; z++)
                foreach (int x in new[] { -R - 1, R + 1 }) { reach.Add((x, z, 0)); queue.Enqueue((x, z, 0)); }
            // steps kept open: the player left them clear, so they can still be stood on and climbed from
            if (kept != null) foreach (var s in kept) if (reach.Add(s)) queue.Enqueue(s);

            bool Spot(int x, int z, int y)
            {
                if (y < 0 || y > H) return false;
                if (y > 0 && !built[x, y - 1, z]) return false;
                for (int k = 0; k < HeadroomLayers; k++) if (built[x, y + k, z]) return false;
                return true;
            }

            while (queue.Count > 0)
            {
                var (x, z, y) = queue.Dequeue();
                foreach (var (dx, dz) in Dirs)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < -R - 1 || nx > R + 1 || nz < -R - 1 || nz > R + 1) continue;
                    for (int ny = Math.Min(H, y + ClimbLayers); ny >= 0; ny--)
                    {
                        if (!Spot(nx, nz, ny)) continue;
                        if (ny > y)
                        {
                            bool clear = true;
                            for (int k = y; k < ny + HeadroomLayers && clear; k++) if (built[x, k, z]) clear = false;
                            if (!clear && kept != null && kept.Contains((x, z, y))) clear = true;   // a kept step is still open
                            if (!clear) continue;
                        }
                        if (reach.Add((nx, nz, ny))) queue.Enqueue((nx, nz, ny));
                    }
                }
            }
            return reach;
        }

        private static readonly (int, int)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    }
}

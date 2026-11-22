using System;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// Sobek: a great crocodile coming out of a pool, gaping. The lower jaw and front feet rest in the water, the
    /// head is tipped back with the upper jaw raised high, the body lies low and the tail curls round behind in
    /// an S. Ridges (scutes) run down the back and tail, and teeth line both jaws. Faces +z, like the cat.
    ///
    /// Modelled in metres as smooth solids, then sampled into the same 1 × 1.25 m blocks as the cat.
    /// </summary>
    public static class SobekShape
    {
        public const double CellW = 1.0, CellH = 1.25;
        public const int Radius = 80;
        private const double Scale = 1.3;

        /// <summary>Bump when the shape changes: worlds started with another version won't match their save.</summary>
        public const int Version = 1;

        private const int MaxLayers = 32;

        /// <summary>
        /// He lies in an oval pool: its centre z and half-sizes (metres, site coordinates), and the water level.
        /// Sized to the classic play area: world z −123 … 21 with the site centre at z −44, x ±46.
        /// </summary>
        public const double PoolCZ = -7, PoolRX = 46, PoolRZ = 72, PoolLevel = 0.9;

        /// <summary>Cells he's moved forward after modelling, so his tail stays well inside the pool
        /// (the site centre is at z −44 and the play area ends at z −124).</summary>
        private const int ShiftZ = 4;

        // The jaw hinge and how far the head is tipped back (degrees)
        private const double HingeY = 4.4, HingeZ = 24, Gape = 34;

        public static Voxels Build()
        {
            var croc = Croc();
            var v = new Voxels(Radius, MaxLayers);
            croc.Sample(v, CellW, CellH, Scale, 0);
            ShapeModel.Prune(v);
            AddTeeth(v);
            return Shifted(v, ShiftZ);
        }

        /// <summary>Whether a point (metres from the site centre) is in the pool.</summary>
        public static bool InPool(double x, double z)
        {
            double ex = x / PoolRX, ez = (z - PoolCZ) / PoolRZ;
            return ex * ex + ez * ez <= 1;
        }

        /// <summary>
        /// The pool as cells, on a grid <paramref name="poolRadius"/> cells each way from the site centre
        /// (index [x + poolRadius, z + poolRadius]). Cells under him count as pool too.
        /// </summary>
        public static bool[,] PoolCells(Voxels v, out int poolRadius)
        {
            int pr = poolRadius = Math.Max(v.Radius, (int)Math.Ceiling(Math.Max(PoolRX, Math.Abs(PoolCZ) + PoolRZ)) + 1);
            var pool = new bool[2 * pr + 1, 2 * pr + 1];
            for (int x = -pr; x <= pr; x++)
                for (int z = -pr; z <= pr; z++)
                    pool[x + pr, z + pr] = InPool(x * CellW, z * CellW);
            return pool;
        }

        private static Voxels Shifted(Voxels v, int dz)
        {
            var s = new Voxels(v.Radius, v.Layers);
            for (int x = -v.Radius; x <= v.Radius; x++)
                for (int y = 0; y < v.Layers; y++)
                    for (int z = -v.Radius; z <= v.Radius; z++)
                        if (v[x, y, z]) s[x, y, z + dz] = true;
            return s;
        }

        private static int Cell(double m) => (int)Math.Round(m * Scale / CellW);

        // ---------- the crocodile (metres; y up from the ground, z forward, x to his left) ----------

        private static ShapeModel Croc()
        {
            var c = new ShapeModel();

            // Body: broad and flat, belly on the ground, shoulders raised a little as he heaves out of the water
            c.Flat(0, 4.6, -17, 0, 5.4, 14, 8.4, 8.0, 4.8, 5.6);              // trunk
            c.Ellipsoid(0, 5.8, 11, 7.4, 5.4, 7.5);                          // shoulders
            c.Ellipsoid(0, 4.2, -15, 7.0, 4.4, 6.5);                         // hips
            c.Flat(0, 5.8, 15, 0, HingeY + 0.4, HingeZ - 1, 6.4, 5.6, 4.8, 4.0); // thick neck into the head

            // Lower jaw: resting on the ground, in the water
            c.Flat(0, 3.0, HingeZ - 3, 0, 1.2, HingeZ + 17.5, 5.8, 2.6, 2.4, 1.2);
            c.Ellipsoid(0, 1.2, HingeZ + 17.2, 3.0, 1.3, 2.4);                // chin, the jaw's rounded tip

            // Head: skull and snout tipped back together around the hinge
            HeadFlat(c, 0, 1.8, -5, 0, 1.6, 2.5, 6.2, 5.6, 3.4, 3.0);         // skull, broad and flat-topped
            HeadFlat(c, 0, 1.3, 2.5, 0, 1.1, 16, 5.2, 2.9, 2.7, 1.8);         // snout, long and tapering
            var tip = Head(0, 1.6, 15.6);
            c.Ellipsoid(tip.x, tip.y, tip.z, 2.6, 1.7, 2.1);                 // nostril knob on the snout tip
            foreach (int s in new[] { -1, 1 })
            {
                var eye = Head(s * 2.8, 3.6, -1.5);
                c.Ellipsoid(eye.x, eye.y, eye.z, 1.6, 1.5, 1.7);             // eyes, raised on top of the head
                var cheek = Head(s * 4.6, 0.4, 1.5);
                c.Ellipsoid(cheek.x, cheek.y, cheek.z, 2.0, 1.8, 3.0);       // jaw muscles behind the eyes
            }

            // Legs: splayed out sideways, elbows and knees up; front feet in the pool
            foreach (int s in new[] { -1, 1 })
            {
                c.Capsule(s * 6, 5, 10, s * 12.5, 5.2, 13, 2.7, 2.2);           // upper arm to the elbow
                c.Capsule(s * 12.5, 5.2, 13, s * 13.5, 1.4, 18.5, 2.2, 1.7);    // forearm down into the water
                c.Ellipsoid(s * 14, 0.8, 20.5, 2.8, 0.9, 3.0);                  // front foot
                foreach (double a in new[] { -40.0, -10.0, 20.0, 50.0 })
                {
                    double r = a * Math.PI / 180;
                    c.Capsule(s * 14, 0.7, 21, s * (14 + 4.2 * Math.Sin(r)), 0.6, 21 + 4.2 * Math.Cos(r), 0.75, 0.55); // toes, spread
                }

                c.Capsule(s * 6, 4.4, -12, s * 12.5, 5.4, -8.5, 3.2, 2.5);      // thigh, knee forward and out
                c.Capsule(s * 12.5, 5.4, -8.5, s * 14, 1.4, -13.5, 2.5, 1.9);   // shin back down to the ankle
                c.Ellipsoid(s * 15, 0.8, -12, 3.0, 0.9, 3.8);                   // hind foot, turned forward
                foreach (double a in new[] { -20.0, 15.0, 50.0 })
                {
                    double r = a * Math.PI / 180;
                    c.Capsule(s * 15, 0.7, -10.5, s * (15 + 4.4 * Math.Sin(r)), 0.6, -10.5 + 4.4 * Math.Cos(r), 0.8, 0.6);
                }
            }

            // Tail: heavy at the root, curling round behind him in an S, lying on the ground
            var tail = new[]
            {
                (0.0, -19.0), (-2.5, -26.5), (-7.0, -33.0), (-12.0, -38.0), (-15.0, -44.5),
                (-14.0, -50.5), (-9.5, -55.0), (-3.0, -56.5), (3.5, -55.0), (8.5, -51.5), (11.5, -47.0),
            };
            int segs = tail.Length - 1;
            double W(int i) => 6.4 - 5.4 * i / segs;       // half-width, root to tip
            double Ht(int i) => 4.6 - 3.6 * i / segs;      // half-height
            for (int i = 0; i < segs; i++)
            {
                var (ax, az) = tail[i];
                var (bx, bz) = tail[i + 1];
                c.Flat(ax, Ht(i), az, bx, Ht(i + 1), bz, W(i), W(i + 1), Ht(i), Ht(i + 1));
            }

            // Scutes: rows of ridges down the back, then a double crest along the tail
            foreach (double x in new[] { -5.4, -2.6, 2.6, 5.4 })
                for (double z = -15; z <= 15; z += 3.4)
                    Scute(c, x, z, Math.Abs(x) > 4 ? 0.85 : 1.25);
            for (int i = 0; i < segs; i++)
            {
                var (ax, az) = tail[i];
                var (bx, bz) = tail[i + 1];
                double len = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                double sxn = (bz - az) / len, szn = -(bx - ax) / len;  // sideways
                for (double d = 0; d < len; d += 3.2)
                {
                    double f = i + d / len;
                    double x = ax + (bx - ax) * d / len, z = az + (bz - az) * d / len;
                    double off = f < segs * 0.6 ? 1.4 - f / segs : 0;  // the double crest merges into one
                    double size = 1.1 - 0.5 * f / segs;
                    if (off > 0.3)
                        foreach (int s in new[] { -1, 1 }) Scute(c, x + s * off * sxn, z + s * off * szn, size);
                    else Scute(c, x, z, size);
                }
            }
            return c;
        }

        /// <summary>A point on the head, given in the head's own frame (origin at the hinge, z' along the snout),
        /// tipped back by the gape.</summary>
        private static (double x, double y, double z) Head(double x, double y, double z)
        {
            double a = Gape * Math.PI / 180;
            return (x, HingeY + y * Math.Cos(a) + z * Math.Sin(a), HingeZ + z * Math.Cos(a) - y * Math.Sin(a));
        }

        private static void HeadFlat(ShapeModel c, double ax, double ay, double az, double bx, double by, double bz,
            double wa, double wb, double ha, double hb)
        {
            var a = Head(ax, ay, az);
            var b = Head(bx, by, bz);
            c.Flat(a.x, a.y, a.z, b.x, b.y, b.z, wa, wb, ha, hb);
        }

        private static void Scute(ShapeModel c, double x, double z, double size)
        {
            double top = c.TopAt(x, z, 20);
            if (top < 0) return;
            c.Ellipsoid(x, top + 0.2 * size, z, 1.0 * size, 1.5 * size, 1.2 * size);
        }

        /// <summary>
        /// Teeth: single blocks along both jaws' edges, every other cell. Upper teeth hang one block below the
        /// raised upper jaw; lower teeth stand one block on top of the lower jaw. Only where there's open mouth for them.
        /// </summary>
        private static void AddTeeth(Voxels v)
        {
            int zFrom = Cell(HingeZ + 4), zTo = Cell(HingeZ + 17);
            for (int z = zFrom; z <= zTo; z++)
                foreach (int s in new[] { -1, 1 })
                {
                    // the outermost column of the lower jaw at this z (lower jaw only: a block on the ground)
                    int x = 0;
                    while (x < 12 && v[s * (x + 1), 0, z]) x++;
                    int col = s * x;
                    if (!v[col, 0, z]) continue;
                    int lowerTop = 0;  // first empty layer above the lower jaw
                    while (lowerTop < v.Layers && v[col, lowerTop, z]) lowerTop++;
                    int gapEnd = lowerTop;
                    while (gapEnd < v.Layers && !v[col, gapEnd, z]) gapEnd++;
                    bool upperAbove = gapEnd < v.Layers;
                    bool lower = (z - zFrom) % 2 == 0;  // lower and upper teeth interlock
                    if (lower && gapEnd - lowerTop >= 3) v[col, lowerTop, z] = true;
                    if (!lower && upperAbove && gapEnd - lowerTop >= 4) v[col, gapEnd - 1, z] = true;
                }
        }
    }
}

using System;
using System.Collections.Generic;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// The great cat, crouched and about to pounce: rump high over bunched hind legs, back dipping to raised
    /// shoulder blades, chest and head low, front legs flat forward, ears up, tail stretched out behind with the
    /// tip flicked up. She faces +z (the river and the camp) and stands on a low base that follows her outline.
    ///
    /// Modelled in metres as smooth solids, then sampled into blocks of CellW × CellW × CellH, half the size of
    /// a pyramid block each way, so her curves come out smooth.
    /// </summary>
    public static class SphinxShape
    {
        /// <summary>Block size in metres (width/depth, height).</summary>
        public const double CellW = 1.0, CellH = 1.25;

        /// <summary>Half the footprint in cells (metres at CellW = 1).</summary>
        public const int Radius = 64;

        /// <summary>The model is drawn about 31 m tall; this scales it up (1.25 → about 39 m, 25,000 blocks).</summary>
        private const double Scale = 1.25;

        /// <summary>Bump when the shape changes: worlds started with another version won't match their save.</summary>
        public const int Version = 5;

        private const int MaxLayers = 48;
        private const int BaseLayers = 1;
        private const double BaseMargin = 3.0;     // metres the base reaches beyond her outline

        public static Voxels Build()
        {
            var cat = Cat();
            var v = new Voxels(Radius, MaxLayers);

            // The cat: a block is solid when a third or more of it lies inside the model (27 samples)
            for (int x = -Radius; x <= Radius; x++)
                for (int z = -Radius; z <= Radius; z++)
                {
                    if (!NearCat(cat, x * CellW / Scale, z * CellW / Scale)) continue;
                    for (int y = BaseLayers; y < MaxLayers; y++)
                    {
                        int inside = 0;
                        for (int i = -1; i <= 1; i++)
                            for (int j = -1; j <= 1; j++)
                                for (int k = -1; k <= 1; k++)
                                {
                                    double mx = (x + i * 0.33) * CellW / Scale;
                                    double my = (y - BaseLayers + 0.5 + j * 0.33) * CellH / Scale;
                                    double mz = (z + k * 0.33) * CellW / Scale;
                                    if (cat.Inside(mx, my, mz)) inside++;
                                }
                        if (inside >= 9) v[x, y, z] = true;
                    }
                }

            // The base: one oval slab under her, BaseMargin beyond her outline
            // Tidy up: single blocks sticking out with at most one neighbour (sampling noise)
            Prune(v);

            double cx = (cat.MinX + cat.MaxX) / 2 * Scale, cz = (cat.MinZ + cat.MaxZ) / 2 * Scale;
            double ax = (cat.MaxX - cat.MinX) / 2 * Scale + BaseMargin, az = (cat.MaxZ - cat.MinZ) / 2 * Scale + BaseMargin;
            for (int x = -Radius; x <= Radius; x++)
                for (int z = -Radius; z <= Radius; z++)
                {
                    double ex = (x * CellW - cx) / ax, ez = (z * CellW - cz) / az;
                    if (ex * ex + ez * ez <= 1)
                        for (int y = 0; y < BaseLayers; y++) v[x, y, z] = true;
                }

            // Carved details: eyes, and the hollows of the ears
            foreach (int s in new[] { -1, 1 })
            {
                CarveFront(v, Cell(s * 3.6), Layer(16.4), 2);                // eyes
                CarveFront(v, Cell(s * 3.6), Layer(16.4) + 1, 1);
                CarveFront(v, Cell(s * 6.6), Layer(24), 1);                  // ear hollows
                CarveFront(v, Cell(s * 6.8), Layer(25.5), 1);
                CarveFront(v, Cell(s * 7.0), Layer(27), 1);
            }
            return v;
        }

        private static int Cell(double m) => (int)Math.Round(m * Scale / CellW);
        private static int Layer(double m) => BaseLayers + (int)Math.Floor(m * Scale / CellH);

        private static void Prune(Voxels v)
        {
            var remove = new List<(int, int, int)>();
            for (int x = -v.Radius; x <= v.Radius; x++)
                for (int y = 0; y < v.Layers; y++)
                    for (int z = -v.Radius; z <= v.Radius; z++)
                    {
                        if (!v[x, y, z]) continue;
                        int n = (v[x + 1, y, z] ? 1 : 0) + (v[x - 1, y, z] ? 1 : 0) + (v[x, y + 1, z] ? 1 : 0) +
                                (v[x, y - 1, z] ? 1 : 0) + (v[x, y, z + 1] ? 1 : 0) + (v[x, y, z - 1] ? 1 : 0);
                        if (n <= 1) remove.Add((x, y, z));
                    }
            foreach (var (x, y, z) in remove) v[x, y, z] = false;
        }

        /// <summary>Removes the frontmost <paramref name="depth"/> blocks of the row (x, y).</summary>
        private static void CarveFront(Voxels v, int x, int y, int depth)
        {
            for (int z = v.Radius; z >= -v.Radius && depth > 0; z--)
                if (v[x, y, z]) { v[x, y, z] = false; depth--; }
        }

        private static bool NearCat(Model cat, double x, double z) =>
            x >= cat.MinX - 1 && x <= cat.MaxX + 1 && z >= cat.MinZ - 1 && z <= cat.MaxZ + 1;

        // ---------- the cat (metres; y up from the base, z forward, x to her left) ----------

        private static Model Cat()
        {
            var c = new Model();

            // Hindquarters: rump raised, thighs bunched, feet flat under her ready to spring
            c.Ellipsoid(0, 17.5, -22, 7, 6.5, 8.5);                         // rump
            foreach (int s in new[] { -1, 1 })
            {
                c.Ellipsoid(s * 5.6, 13, -20, 3.8, 7.5, 8.5);               // thigh
                c.Capsule(s * 6.4, 7, -26, s * 6.4, 2.2, -28.5, 2.4, 2);    // hock, down to the heel
                c.Capsule(s * 6.2, 2.5, -27, s * 5.8, 8.5, -21, 2.6, 3.4);  // back of the leg, heel to thigh
                c.Capsule(s * 6.4, 1.5, -28.5, s * 6.2, 1.5, -15, 1.9, 1.9);// hind foot, flat
                c.Ellipsoid(s * 6.2, 1.6, -14.5, 2.3, 1.6, 2.2);            // hind toes
            }

            // Body: long and lean, sloping down from the rump to the chest; daylight under the belly
            c.Capsule(0, 17, -18, 0, 13.5, 2, 6.4, 6);                      // loins and back
            c.Capsule(0, 13.5, 2, 0, 11, 12, 6, 6.6);                       // ribcage
            c.Ellipsoid(0, 9, 14.5, 6.2, 6, 6.2);                           // chest, low to the ground

            // Shoulder blades, standing up above the back as she crouches
            foreach (int s in new[] { -1, 1 })
                c.Ellipsoid(s * 4.2, 16.5, 8.5, 2.8, 4.2, 4.6);

            // Front legs: elbows out and up, forearms flat on the ground, big paws reaching forward
            foreach (int s in new[] { -1, 1 })
            {
                c.Capsule(s * 4.8, 13, 9.5, s * 7.8, 5, 14, 2.9, 2.5);      // upper arm to the elbow
                c.Capsule(s * 7.8, 4.5, 14, s * 6, 2, 31, 2.5, 2.2);        // forearm along the ground
                c.Ellipsoid(s * 6, 1.8, 33, 3.2, 1.8, 3.6);                 // paw
                foreach (double t in new[] { -1.9, -0.6, 0.7, 2.0 })
                    c.Ellipsoid(s * 6 + t, 1.5, 35.8, 0.95, 1.4, 1.3);      // toes
            }

            // Neck and head: head held low and forward, eyes locked on the prey; a big round cat face
            c.Capsule(0, 12.5, 13, 0, 14.5, 22, 5.4, 5);                    // neck
            c.Ellipsoid(0, 16, 25.5, 8.4, 7.2, 6.8);                        // skull, broad
            foreach (int s in new[] { -1, 1 })
                c.Ellipsoid(s * 5.2, 13, 28, 4.2, 3.8, 3.8);                // cheeks, wide
            c.Ellipsoid(0, 12.2, 30.5, 3.8, 3, 3.2);                        // muzzle
            c.Ellipsoid(0, 14.4, 32.6, 1.7, 1.4, 1.4);                      // nose
            c.Ellipsoid(0, 9.6, 29.5, 2.8, 1.8, 2.6);                       // chin
            foreach (int s in new[] { -1, 1 })
                c.Ear(s * 5.4, 20.5, 24, s * 7.6, 31, 23, 4.4, 1.6);        // ears: tall, pricked up

            // Tail: out behind, low over the ground, the tip flicked up
            var tail = new[]
            {
                (0.0, 20.0, -29.0), (-1.5, 15.5, -35.0), (-3.5, 9.5, -39.0), (-5.5, 5.0, -42.5),
                (-8.0, 3.2, -45.5), (-10.5, 3.4, -47.8), (-12.5, 5.5, -48.8), (-13.5, 8.5, -48.6),
            };
            for (int i = 0; i + 1 < tail.Length; i++)
            {
                var (ax, ay, az) = tail[i];
                var (bx, by, bz) = tail[i + 1];
                double ra = 2.6 - 1.3 * i / (tail.Length - 1), rb = 2.6 - 1.3 * (i + 1) / (tail.Length - 1);
                c.Capsule(ax, ay, az, bx, by, bz, ra, rb);
            }
            return c;
        }

        /// <summary>A union of simple solids with a running bounding box.</summary>
        private sealed class Model
        {
            private readonly List<Func<double, double, double, double>> _parts = new List<Func<double, double, double, double>>();
            public double MinX = double.MaxValue, MaxX = double.MinValue, MinZ = double.MaxValue, MaxZ = double.MinValue;

            public bool Inside(double x, double y, double z)
            {
                foreach (var p in _parts) if (p(x, y, z) <= 0) return true;
                return false;
            }

            private void Bound(double x, double z, double r)
            {
                MinX = Math.Min(MinX, x - r); MaxX = Math.Max(MaxX, x + r);
                MinZ = Math.Min(MinZ, z - r); MaxZ = Math.Max(MaxZ, z + r);
            }

            public void Ellipsoid(double cx, double cy, double cz, double rx, double ry, double rz)
            {
                Bound(cx, cz, Math.Max(rx, rz));
                _parts.Add((x, y, z) =>
                {
                    double dx = (x - cx) / rx, dy = (y - cy) / ry, dz = (z - cz) / rz;
                    return dx * dx + dy * dy + dz * dz - 1;
                });
            }

            /// <summary>A capsule from A to B whose radius goes from ra to rb.</summary>
            public void Capsule(double ax, double ay, double az, double bx, double by, double bz, double ra, double rb)
            {
                Bound(ax, az, ra); Bound(bx, bz, rb);
                _parts.Add((x, y, z) =>
                {
                    double vx = bx - ax, vy = by - ay, vz = bz - az;
                    double len2 = vx * vx + vy * vy + vz * vz;
                    double t = len2 > 0 ? ((x - ax) * vx + (y - ay) * vy + (z - az) * vz) / len2 : 0;
                    t = Math.Max(0, Math.Min(1, t));
                    double px = ax + t * vx - x, py = ay + t * vy - y, pz = az + t * vz - z;
                    return Math.Sqrt(px * px + py * py + pz * pz) - (ra + (rb - ra) * t);
                });
            }

            /// <summary>An ear: a cone from a wide base at A to a point at B, flattened front-to-back.</summary>
            public void Ear(double ax, double ay, double az, double bx, double by, double bz, double r, double thick)
            {
                Bound(ax, az, r);
                double flat = r / thick;
                _parts.Add((x, y, z) =>
                {
                    double vx = bx - ax, vy = by - ay, vz = bz - az;
                    double len2 = vx * vx + vy * vy + vz * vz;
                    double t = Math.Max(0, Math.Min(1, ((x - ax) * vx + (y - ay) * vy + (z - az) * vz) / len2));
                    double px = ax + t * vx - x, py = ay + t * vy - y, pz = (az + t * vz - z) * flat;
                    return Math.Sqrt(px * px + py * py + pz * pz) - r * (1 - t) - 0.15;
                });
            }
        }
    }
}

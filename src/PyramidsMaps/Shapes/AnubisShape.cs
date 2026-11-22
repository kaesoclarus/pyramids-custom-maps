using System;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// Anubis: a jackal lying on a tall shrine chest, head held high and ears up, keeping watch. Front legs
    /// stretched out, hind legs tucked under, and his tail hanging down the back of the chest. The chest has a
    /// lip at the top and a step at the base. Faces +z, like the others.
    ///
    /// Modelled in metres as smooth solids, then sampled into the same 1 × 1.25 m blocks as the other statues.
    /// </summary>
    public static class AnubisShape
    {
        public const double CellW = 1.0, CellH = 1.25;
        public const int Radius = 64;
        private const double Scale = 1.3;

        /// <summary>Bump when the shape changes: worlds started with another version won't match their save.</summary>
        public const int Version = 1;

        private const int MaxLayers = 40;

        /// <summary>The chest: half-width, half-length, height (model metres).</summary>
        private const double ChestHX = 7, ChestHZ = 19, ChestH = 7;

        public static Voxels Build()
        {
            var m = Model();
            var v = new Voxels(Radius, MaxLayers);
            m.Sample(v, CellW, CellH, Scale, 0);
            ShapeModel.Prune(v);
            return v;
        }

        // ---------- the statue (metres; y up from the ground, z forward, x to his left) ----------

        private static ShapeModel Model()
        {
            var c = new ShapeModel();
            double t = ChestH;   // top of the chest: the jackal lies on it

            // The shrine chest: a step at the base, the body, and a lip around the top
            c.Box(0, 0.5, 0, ChestHX + 0.8, 0.5, ChestHZ + 0.8);
            c.Box(0, t / 2, 0, ChestHX, t / 2, ChestHZ);
            c.Box(0, t - 0.5, 0, ChestHX + 0.7, 0.5, ChestHZ + 0.7);
            // The stairway up the front, between his paws: one block layer per block of run, so it can be
            // climbed as it's built (a little gentler than that, so no step comes out two layers high)
            double run = t * CellW / CellH * 1.1;
            c.Ramp(0, 2.2, ChestHZ + run, ChestHZ, t);

            // Body: lean and long, lying flat, chest deep at the front
            c.Flat(0, t + 4.0, -13, 0, t + 4.6, 3, 4.2, 4.4, 4.0, 4.4);
            c.Ellipsoid(0, t + 5.0, 4, 4.3, 5.0, 4.0);                       // chest
            foreach (int s in new[] { -1, 1 })
            {
                // Hind legs: haunch tucked, the foot flat along the side, pointing forward
                c.Ellipsoid(s * 3.2, t + 4.0, -11, 2.8, 4.0, 5.0);           // haunch
                c.Capsule(s * 3.6, t + 1.0, -15, s * 3.9, t + 0.9, -5.5, 1.2, 1.0);  // hind foot
                c.Ellipsoid(s * 3.9, t + 0.9, -5, 1.3, 0.9, 1.5);            // hind toes

                // Front legs: straight out in front, paws flat
                c.Capsule(s * 2.4, t + 3.6, 4.5, s * 2.5, t + 1.2, 8.0, 1.6, 1.3);   // upper leg into the elbow
                c.Capsule(s * 2.5, t + 1.2, 8.0, s * 2.5, t + 1.0, 15.0, 1.3, 1.1);  // forearm along the chest top
                c.Ellipsoid(s * 2.5, t + 0.9, 16.2, 1.5, 0.9, 1.9);          // paw
            }

            // Neck and head: held high, long thin snout pointing forward, tall ears
            c.Capsule(0, t + 6.5, 4.5, 0, t + 12.5, 8.5, 3.1, 2.5);           // neck
            c.Ellipsoid(0, t + 13.8, 9.2, 2.8, 2.5, 3.0);                    // skull
            c.Flat(0, t + 13.3, 11.0, 0, t + 12.4, 17.0, 2.0, 1.0, 1.6, 1.0); // snout
            c.Ellipsoid(0, t + 12.6, 16.8, 1.1, 1.0, 1.0);                   // nose
            foreach (int s in new[] { -1, 1 })
                c.Ear(s * 1.6, t + 15.2, 8.4, s * 2.3, t + 21.0, 8.0, 2.0, 1.0); // ears: tall and pointed

            // Tail: from the rump over the back edge of the chest, hanging down its back face
            var tail = new[] { (0.0, t + 3.5, -16.5), (0.0, t + 1.6, -19.6), (0.0, t - 1.5, -20.3), (0.0, t - 4.0, -20.3), (0.0, t - 6.0, -20.1) };
            for (int i = 0; i + 1 < tail.Length; i++)
            {
                var (ax, ay, az) = tail[i];
                var (bx, by, bz) = tail[i + 1];
                c.Capsule(ax, ay, az, bx, by, bz, 1.3 - 0.15 * i, 1.3 - 0.15 * (i + 1));
            }
            return c;
        }
    }
}

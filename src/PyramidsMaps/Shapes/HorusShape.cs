using System;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// Horus: a falcon standing upright on a low base, wings folded along his sides, wearing the double crown
    /// (the tall white crown inside the red one). Tall and narrow: the climb is the challenge. Faces +z.
    ///
    /// Modelled in metres as smooth solids, then sampled into the same 1 × 1.25 m blocks as the other statues.
    /// </summary>
    public static class HorusShape
    {
        public const double CellW = 1.0, CellH = 1.25;
        public const int Radius = 64;
        private const double Scale = 3.0;

        /// <summary>Bump when the shape changes: worlds started with another version won't match their save.</summary>
        public const int Version = 1;

        private const int MaxLayers = 96;

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

            // Base: a low block he stands on
            const double b = 2.4;
            c.Box(0, b / 2, -0.5, 5.5, b / 2, 6.5);
            // Steps up the front of the base (one block layer per block of run)
            c.Ramp(0, 2.5, 6.0 + b * CellW / CellH * 1.1, 6.0, b);

            // Legs: short and feathered, and talons gripping the front of the base
            foreach (int s in new[] { -1, 1 })
            {
                c.Capsule(s * 1.9, b, 0.6, s * 2.0, 6.5, 0.2, 1.5, 1.7);         // leg
                foreach (double a in new[] { -25.0, 0.0, 25.0 })
                {
                    double r = a * Math.PI / 180;
                    c.Capsule(s * 1.9, b + 0.5, 1.0, s * 1.9 + Math.Sin(r) * 2.6, b + 0.4, 1.0 + Math.Cos(r) * 2.6, 0.7, 0.5); // talons
                }
            }

            // Body: an upright egg, breast pushed forward
            c.Ellipsoid(0, 12, -0.5, 4.9, 8, 4.6);
            c.Ellipsoid(0, 13.5, 1.2, 4.6, 5.8, 4.2);                         // breast

            if (OutstretchedWings) SpreadWings(c);
            else
            {
                // Folded wings: along his sides, sweeping down and back, the tips crossing low behind him
                foreach (int s in new[] { -1, 1 })
                {
                    c.Flat(s * 4.9, 18.0, -0.6, s * 2.6, 3.5, -5.6, 1.6, 1.0, 4.4, 1.8);  // wing
                    c.Ellipsoid(s * 4.7, 17.4, -0.4, 2.0, 2.8, 3.6);                  // shoulder of the wing
                }
            }

            // Tail: straight down behind, resting on the base
            c.Flat(0, 7.5, -4.2, 0, b, -6.6, 2.6, 1.8, 1.0, 0.7);

            // Neck and head
            c.Ellipsoid(0, 19.2, 0.4, 3.0, 2.6, 3.0);                         // neck
            c.Ellipsoid(0, 22.5, 0.8, 3.6, 3.4, 3.8);                         // head
            c.Capsule(0, 22.3, 3.8, 0, 21.0, 6.4, 1.4, 0.8);                  // beak
            c.Capsule(0, 21.0, 6.4, 0, 19.6, 5.9, 0.8, 0.45);                 // hooked tip
            foreach (int s in new[] { -1, 1 })
                c.Ellipsoid(s * 2.8, 23.0, 2.4, 1.0, 0.8, 1.0);               // brows over the eyes

            // The double crown: the red crown (a short drum with its tall back plate) holding the white crown
            c.Capsule(0, 25.5, 0.2, 0, 26.8, 0.2, 2.6, 2.5);                  // red crown, drum
            c.Box(0, 28.2, -2.1, 2.2, 2.6, 0.6);                              // red crown, back plate
            c.Capsule(0, 26.5, 0.3, 0, 31.5, 0.1, 1.8, 0.9);                  // white crown, tall cone
            c.Ellipsoid(0, 31.9, 0.1, 1.2, 1.3, 1.2);                         // white crown, knob on top
            return c;
        }

        /// <summary>Wings spread out to the sides (true) or folded along his body (false).</summary>
        public static bool OutstretchedWings = true;

        /// <summary>
        /// The protective spread: each wing reaches out from the shoulder, raised a little and swept slightly back,
        /// with long primary feathers fanning down and out from it. The wings face forward (thin front to back,
        /// about 3 blocks thick) so you can walk out along the top edge while building.
        /// </summary>
        private static void SpreadWings(ShapeModel c)
        {
            foreach (int s in new[] { -1, 1 })
            {
                c.Ellipsoid(s * 4.6, 17.6, -0.6, 2.6, 3.0, 3.0);                              // shoulder
                // The arm along the top: shoulder to wrist, then the hand out to the tip
                c.Flat(s * 4.6, 18.4, -0.6, s * 11.0, 22.0, -1.4, 1.1, 0.9, 1.9, 1.5);
                c.Flat(s * 11.0, 22.0, -1.4, s * 17.0, 23.2, -2.0, 0.9, 0.7, 1.5, 1.0);
                // Feathers hanging from the arm, longer and more outward towards the tip
                for (int i = 0; i < 7; i++)
                {
                    double t = i / 6.0;
                    double rx = 5.5 + 11.0 * t, ry = 18.6 + 4.4 * t - 0.6 * t * t, rz = -0.7 - 1.2 * t;
                    double len = 7.0 + 3.5 * t;
                    double ang = (15 + 40 * t) * Math.PI / 180;              // from straight down, swinging outward
                    double tx = rx + s * 0 + Math.Sin(ang) * len, ty = ry - Math.Cos(ang) * len;
                    c.Flat(s * rx, ry, rz, s * tx, ty, rz - 0.4, 0.75, 0.55, 1.5, 0.9);
                }
            }
        }
    }
}

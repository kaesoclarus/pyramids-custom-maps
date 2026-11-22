using System;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// Monument groups in statue blocks (1 × 1.25 m), facing +z (the river):
    ///  - the Colossi of Memnon: two pharaohs seated on thrones, side by side;
    ///  - an avenue of sphinxes: two facing rows of small sphinxes lining a road to a tall obelisk.
    /// </summary>
    public static class MonumentShapes
    {
        public const double CellW = 1.0, CellH = 1.25;

        /// <summary>Bump when a shape changes (and give its map a new sync code).</summary>
        public const int ColossiVersion = 1, AvenueVersion = 1;

        /// <summary>Copies a separately sampled piece into v at (ox, oz), optionally turned a quarter (facing ±x).</summary>
        private static void Stamp(Voxels v, Voxels piece, int ox, int oz, int quarterTurns = 0)
        {
            for (int x = -piece.Radius; x <= piece.Radius; x++)
                for (int y = 0; y < piece.Layers; y++)
                    for (int z = -piece.Radius; z <= piece.Radius; z++)
                    {
                        if (!piece[x, y, z]) continue;
                        int rx = x, rz = z;
                        for (int q = 0; q < ((quarterTurns % 4) + 4) % 4; q++) { int t = rx; rx = rz; rz = -t; }   // +z → +x per turn
                        v[ox + rx, y, oz + rz] = true;
                    }
        }

        private static Voxels Sample(ShapeModel m, int radius, int layers, double scale)
        {
            var p = new Voxels(radius, layers);
            m.Sample(p, CellW, CellH, scale, 0);
            ShapeModel.Prune(p);
            return p;
        }

        // ---------- the Colossi of Memnon ----------

        private const double ColossusScale = 1.7;

        /// <summary>
        /// Two pharaohs seated on thrones on low plinths, hands on their knees, wearing the nemes, about 36 m tall,
        /// side by side with a gap between. The right one wears a small sun disc on top, so his is the last brick.
        /// </summary>
        public static Voxels BuildColossi()
        {
            var left = Sample(Colossus(false), 14, 32, ColossusScale);
            var right = Sample(Colossus(true), 14, 32, ColossusScale);
            var v = new Voxels(36, 33);
            Stamp(v, left, -17, 0);
            Stamp(v, right, 17, 0);
            return v;
        }

        /// <summary>A seated pharaoh (metres, before scaling): plinth, throne, the figure, the nemes headdress.</summary>
        private static ShapeModel Colossus(bool sunDisc)
        {
            var c = new ShapeModel();
            c.Box(0, 1.0, -0.5, 5.0, 1.0, 7.0);                           // plinth, 2 m
            c.Box(0, 4.5, -2.5, 4.2, 2.5, 4.5);                           // throne seat block, seat at 7 m
            c.Box(0, 11.0, -6.2, 4.2, 4.0, 0.9);                          // throne back
            foreach (int s in new[] { -1, 1 })
            {
                c.Capsule(s * 1.7, 8.2, -4.0, s * 1.7, 8.4, 3.2, 1.7, 1.6);   // thigh, along the seat
                c.Capsule(s * 1.7, 8.0, 3.4, s * 1.7, 2.6, 3.8, 1.4, 1.2);    // shin, straight down
                c.Ellipsoid(s * 1.7, 2.5, 5.0, 1.2, 0.7, 1.9);                // foot on the plinth
                c.Capsule(s * 3.5, 15.0, -4.0, s * 3.3, 10.4, -2.8, 1.1, 1.0); // upper arm
                c.Capsule(s * 3.3, 10.4, -2.8, s * 2.3, 9.6, 2.6, 1.0, 0.9);   // forearm along the thigh
                c.Ellipsoid(s * 2.3, 9.5, 3.0, 1.0, 0.7, 1.3);                // hand on the knee
                c.Flat(s * 2.0, 19.2, -3.5, s * 2.4, 15.4, -3.3, 0.6, 0.6, 1.3, 1.5); // nemes flap
            }
            c.Ellipsoid(0, 12.0, -4.0, 3.4, 4.6, 2.3);                    // torso
            c.Ellipsoid(0, 15.0, -4.0, 3.9, 1.6, 2.3);                    // shoulders
            c.Capsule(0, 15.6, -4.0, 0, 17.2, -3.8, 1.2, 1.1);            // neck
            c.Ellipsoid(0, 18.7, -3.6, 1.8, 2.1, 2.0);                    // head
            c.Ellipsoid(0, 18.4, -1.7, 0.45, 0.6, 0.45);                  // nose
            c.Capsule(0, 16.9, -2.3, 0, 15.6, -2.0, 0.5, 0.4);            // beard
            c.Ellipsoid(0, 19.8, -3.8, 2.2, 1.4, 2.2);                    // nemes cap
            if (sunDisc) c.Ellipsoid(0, 21.6, -3.8, 1.0, 1.0, 0.6);       // sun disc
            return c;
        }

        // ---------- the avenue of sphinxes ----------

        private const double AvenueSphinxScale = 0.45;
        private const int RowX = 17, FirstZ = 38, Spacing = 15, PerSide = 6;
        private const int ObeliskZ = -58;

        /// <summary>
        /// Twelve small sphinxes (the Giza sphinx at 0.45 scale, about 21 m long) in two rows facing across a
        /// processional road, leading back to a 4 m square obelisk 36 m tall with a pyramid tip, the last brick.
        /// </summary>
        public static Voxels BuildAvenue()
        {
            var sphinx = Sample(GizaShape.SphinxModel(), 16, 12, AvenueSphinxScale);
            var v = new Voxels(70, 31);
            for (int i = 0; i < PerSide; i++)
            {
                int z = FirstZ - i * Spacing;
                Stamp(v, sphinx, -RowX, z, 1);    // left row faces +x, across the road
                Stamp(v, sphinx, RowX, z, 3);     // right row faces −x
            }
            Obelisk(v, 0, ObeliskZ);
            return v;
        }

        /// <summary>A square obelisk on a base: 5 × 5 base course, a shaft tapering from 4 to 2 blocks, a pyramid tip.</summary>
        private static void Obelisk(Voxels v, int cx, int cz)
        {
            void Square(int y, int lo, int hi) { for (int x = lo; x <= hi; x++) for (int z = lo; z <= hi; z++) v[cx + x, y, cz + z] = true; }
            Square(0, -3, 3);                                 // base course, 7 × 7
            Square(1, -2, 2);                                 // plinth, 5 × 5
            const int shaft = 25;
            for (int y = 2; y < 2 + shaft; y++)
            {
                bool wide = y < 2 + shaft * 2 / 3;
                if (wide) Square(y, -2, 1); else Square(y, -1, 1);    // 4 × 4, then 3 × 3
            }
            int t = 2 + shaft;
            Square(t, -1, 1);                                 // pyramidion: 3 × 3 …
            Square(t + 1, 0, 0);                              // … to a single tip
            Square(t + 2, 0, 0);
        }
    }
}

using System;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// The Giza plateau in miniature: Khufu, Khafre and Menkaure on a diagonal from front-left to back-right, as
    /// on the real plateau, and the Great Sphinx lying in front of Khafre. In statue blocks (1 × 1.25 m), so the
    /// pyramids rise at the real 51° (one block in per layer) and the Sphinx has room for detail. Every piece
    /// stands on the ground, so they can be built in any order; Khufu is the tallest and his tip is the last brick.
    /// </summary>
    public static class GizaShape
    {
        public const double CellW = 1.0, CellH = 1.25;
        public const int Radius = 78;

        /// <summary>Bump when the shape changes (and give its map a new sync code).</summary>
        public const int Version = 1;

        // centres (site blocks, +z towards the river) and half-bases
        private const int KhufuX = -30, KhufuZ = 0, KhufuHalf = 27;
        private const int KhafreX = 27, KhafreZ = -31, KhafreHalf = 25;
        private const int MenkaureX = 62, MenkaureZ = -64, MenkaureHalf = 13;
        private const int SphinxX = 27, SphinxZ = 25;  // the Sphinx's body centre, facing +z
        private const double SphinxScale = 1.3;

        public static Voxels Build()
        {
            var v = new Voxels(Radius, KhufuHalf + 2);
            Pyramid(v, KhufuX, KhufuZ, KhufuHalf);
            Pyramid(v, KhafreX, KhafreZ, KhafreHalf);
            Pyramid(v, MenkaureX, MenkaureZ, MenkaureHalf);

            // the Sphinx, sampled on its own and set down in front of Khafre
            var sphinx = new Voxels(40, 24);
            SphinxModel().Sample(sphinx, CellW, CellH, SphinxScale, 0);
            ShapeModel.Prune(sphinx);
            for (int x = -sphinx.Radius; x <= sphinx.Radius; x++)
                for (int y = 0; y < sphinx.Layers; y++)
                    for (int z = -sphinx.Radius; z <= sphinx.Radius; z++)
                        if (sphinx[x, y, z]) v[SphinxX + x, y, SphinxZ + z] = true;
            return v;
        }

        /// <summary>A true pyramid: half-base h, one block in per layer, a single block on top.</summary>
        private static void Pyramid(Voxels v, int cx, int cz, int h)
        {
            for (int y = 0; y <= h; y++)
                for (int x = cx - (h - y); x <= cx + (h - y); x++)
                    for (int z = cz - (h - y); z <= cz + (h - y); z++)
                        v[x, y, z] = true;
        }

        /// <summary>
        /// The Great Sphinx (metres, before scaling): a lion lying with its front paws stretched out, and a pharaoh's
        /// head wearing the nemes headdress, its side flaps falling to the shoulders. On a low base.
        /// </summary>
        private static ShapeModel SphinxModel()
        {
            var c = new ShapeModel();
            c.Box(0, 0.4, 1, 6.6, 0.4, 23.5);                                   // base
            c.Flat(0, 5.0, -16, 0, 5.6, 6, 5.4, 5.2, 4.8, 5.4);                // body
            c.Ellipsoid(0, 6.6, 7, 5.0, 6.0, 4.4);                              // chest
            foreach (int s in new[] { -1, 1 })
            {
                c.Ellipsoid(s * 3.4, 4.6, -13, 3.0, 4.4, 5.0);                  // haunch
                c.Capsule(s * 5.4, 1.2, -18, s * 5.6, 1.1, -8, 1.4, 1.2);       // hind leg tucked along the side
                c.Capsule(s * 3.2, 3.5, 8, s * 3.2, 1.6, 11, 1.9, 1.7);         // front leg down from the chest
                c.Capsule(s * 3.2, 1.6, 11, s * 3.2, 1.4, 21.5, 1.7, 1.6);      // forearm stretched forward
                c.Ellipsoid(s * 3.2, 1.3, 22.6, 2.0, 1.3, 2.2);                 // paw
            }
            c.Capsule(5.4, 1.0, -19.5, 6.0, 0.9, -7, 0.8, 0.7);                 // tail along the right hip

            // Neck and head: the face looks straight ahead, framed by the nemes
            c.Capsule(0, 9.5, 8, 0, 13.5, 10.2, 3.1, 2.7);                      // neck
            c.Ellipsoid(0, 15.6, 11, 2.8, 3.3, 3.0);                            // head
            c.Ellipsoid(0, 14.6, 13.6, 0.9, 1.3, 0.8);                          // nose
            c.Ellipsoid(0, 13.0, 13.0, 1.4, 0.9, 1.0);                          // chin
            c.Ellipsoid(0, 17.8, 10.6, 3.5, 1.8, 3.5);                          // nemes cap
            foreach (int s in new[] { -1, 1 })
                c.Flat(s * 3.3, 16.8, 10.6, s * 3.9, 11.0, 10.9, 0.8, 0.9, 2.3, 2.8); // nemes flaps to the shoulders
            c.Capsule(0, 16.5, 8.4, 0, 11.5, 6.6, 1.3, 1.0);                    // nemes tail down the back of the neck
            return c;
        }
    }
}

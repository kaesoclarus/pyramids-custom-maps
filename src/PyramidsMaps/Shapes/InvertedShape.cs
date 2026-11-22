namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// The upside-down pyramids: the game's own pyramids, block for block (same block size, same count), balanced
    /// on their tips, plus a capstone at the centre of the plaza on top. One block touches the ground; each layer is
    /// one block wider on every side than the one below, up to the full base in the air. Every layer overhangs the
    /// one under it, which the statue rules (build out from any laid block) allow.
    ///  - Classic (base 51): 23,426 + 1 blocks, a 102 m plaza 65 m up.
    ///  - Grand (base 85): 105,995 + 1 blocks, a 170 m plaza 107.5 m up.
    /// </summary>
    public static class InvertedShape
    {
        /// <summary>The game's own pyramid block: 2 × 2 × 2.5 m.</summary>
        public const double CellW = 2.0, CellH = 2.5;

        /// <summary>The classic and Grand pyramids' bases, in blocks.</summary>
        public const int Base = 51, GrandBase = 85;

        /// <summary>Bump when a shape changes: worlds started with another version won't match their save.</summary>
        public const int Version = 1, GrandVersion = 1;

        public static Voxels Build() => Build(Base);

        public static Voxels BuildGrand() => Build(GrandBase);

        public static Voxels Build(int baseSize)
        {
            int r = baseSize / 2, layers = r + 1;
            var v = new Voxels(r, layers + 1);
            for (int y = 0; y < layers; y++)
                for (int x = -y; x <= y; x++)
                    for (int z = -y; z <= y; z++)
                        v[x, y, z] = true;
            // The capstone: one block standing at the centre of the plaza, the last brick
            v[0, layers, 0] = true;
            return v;
        }

        /// <summary>Bump when the diamond changes.</summary>
        public const int DiamondVersion = 1;

        public static Voxels BuildDiamond() => BuildDiamond(Base);

        /// <summary>
        /// The diamond: the upside-down pyramid with a pyramid standing on its plaza, sharing the widest layer
        /// (an octahedron), balanced on one point with the other in the sky. The top point is the last brick.
        /// Classic size: 44,251 blocks, 51 layers, 127.5 m tall.
        /// </summary>
        public static Voxels BuildDiamond(int baseSize)
        {
            int r = baseSize / 2, layers = 2 * r + 1;
            var v = new Voxels(r, layers);
            for (int y = 0; y < layers; y++)
            {
                int half = y <= r ? y : 2 * r - y;
                for (int x = -half; x <= half; x++)
                    for (int z = -half; z <= half; z++)
                        v[x, y, z] = true;
            }
            return v;
        }

        /// <summary>
        /// Ground counts as under the statue only where its underside is lower than this (metres): a 14 m square
        /// around the tip. Elsewhere the overhang is high enough for machines and Shabti routes underneath.
        /// </summary>
        public const double GroundClearance = 8;
    }
}

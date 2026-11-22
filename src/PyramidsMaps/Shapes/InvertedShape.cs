namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// The upside-down pyramid: the classic pyramid's exact blocks (same size, same 23,426 of them), balanced on
    /// its tip, plus a capstone at the centre of the plaza on top. One block touches the ground; each layer is one
    /// block wider on every side than the one below, up to the 51-block (102 m) base, 65 m in the air. Every layer
    /// overhangs the one under it, which the statue rules (build out from any laid block) allow.
    /// </summary>
    public static class InvertedShape
    {
        /// <summary>The game's own pyramid block: 2 × 2 × 2.5 m.</summary>
        public const double CellW = 2.0, CellH = 2.5;

        /// <summary>The classic pyramid's base, in blocks.</summary>
        public const int Base = 51;

        /// <summary>Bump when the shape changes: worlds started with another version won't match their save.</summary>
        public const int Version = 1;

        public static Voxels Build()
        {
            int r = Base / 2, layers = r + 1;
            var v = new Voxels(r, layers + 1);
            for (int y = 0; y < layers; y++)
                for (int x = -y; x <= y; x++)
                    for (int z = -y; z <= y; z++)
                        v[x, y, z] = true;
            // The capstone: one block standing at the centre of the plaza, the last brick (23,427 in all)
            v[0, layers, 0] = true;
            return v;
        }

        /// <summary>
        /// Ground counts as under the statue only where its underside is lower than this (metres): a 14 m square
        /// around the tip. Elsewhere the overhang is high enough for machines and Shabti routes underneath.
        /// </summary>
        public const double GroundClearance = 8;
    }
}

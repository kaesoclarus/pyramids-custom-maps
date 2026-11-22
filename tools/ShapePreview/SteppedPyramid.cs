namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// The game's own pyramid layout at any odd base: each layer one block in from the last on every side.
    /// The Colossal Pyramid map (base 141, 71 layers) has no modelled shape, so its renders use this.
    /// </summary>
    internal static class SteppedPyramid
    {
        public static Voxels Build(int baseSize)
        {
            int half = baseSize / 2, layers = (baseSize + 1) / 2;
            var p = new Voxels(half, layers);
            for (int y = 0; y < layers; y++)
                for (int x = -(half - y); x <= half - y; x++)
                    for (int z = -(half - y); z <= half - y; z++)
                        p[x, y, z] = true;
            return p;
        }
    }
}

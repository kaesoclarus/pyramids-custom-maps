using System;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// A build shape as a 3D grid of blocks. x and z are cell coordinates centred on the site (like PyramidLayout's),
    /// y is the layer, +z = front (towards the river). No Unity types, so the shape tools run outside the game.
    /// </summary>
    public sealed class Voxels
    {
        public readonly int Radius;
        public readonly int Layers;
        private readonly bool[,,] _v;

        public Voxels(int radius, int layers)
        {
            Radius = radius;
            Layers = layers;
            _v = new bool[2 * radius + 1, layers, 2 * radius + 1];
        }

        public int Size => 2 * Radius + 1;

        public bool InBounds(int x, int y, int z) =>
            x >= -Radius && x <= Radius && z >= -Radius && z <= Radius && y >= 0 && y < Layers;

        public bool this[int x, int y, int z]
        {
            get => InBounds(x, y, z) && _v[x + Radius, y, z + Radius];
            set { if (InBounds(x, y, z)) _v[x + Radius, y, z + Radius] = value; }
        }

        /// <summary>Number of the highest layer that has a block, plus one.</summary>
        public int Height
        {
            get
            {
                for (int y = Layers - 1; y >= 0; y--)
                    if (LayerCellCount(y) > 0) return y + 1;
                return 0;
            }
        }

        public int CellCount
        {
            get
            {
                int n = 0;
                foreach (var b in _v) if (b) n++;
                return n;
            }
        }

        public int LayerCellCount(int y)
        {
            int n = 0;
            for (int x = -Radius; x <= Radius; x++)
                for (int z = -Radius; z <= Radius; z++)
                    if (this[x, y, z]) n++;
            return n;
        }

        /// <summary>Blocks with nothing directly underneath (overhangs).</summary>
        public int Overhangs
        {
            get
            {
                int n = 0;
                for (int x = -Radius; x <= Radius; x++)
                    for (int y = 1; y < Layers; y++)
                        for (int z = -Radius; z <= Radius; z++)
                            if (this[x, y, z] && !this[x, y - 1, z]) n++;
                return n;
            }
        }

        /// <summary>Top block of the column + 1, or 0 when the column is empty.</summary>
        public int ColumnTop(int x, int z)
        {
            for (int y = Layers - 1; y >= 0; y--)
                if (this[x, y, z]) return y + 1;
            return 0;
        }
    }
}

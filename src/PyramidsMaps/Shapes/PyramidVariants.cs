using System;
using System.Collections.Generic;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// Real-world pyramid variants, in the game's own pyramid blocks (2 × 2 × 2.5 m), built block by block so the
    /// steps come out crisp. All face +z (the river). Built with the statue rules (any order, touch a laid block).
    /// </summary>
    public static class PyramidVariants
    {
        public const double CellW = 2.0, CellH = 2.5;

        /// <summary>Bump when a shape changes (and give its map a new sync code).</summary>
        public const int StepVersion = 1, BentVersion = 1, NubianVersion = 1, ZigguratVersion = 1;

        /// <summary>Fills a rectangle of layer y: half-sizes in blocks, centred on (cx, cz).</summary>
        private static void Rect(Voxels v, int y, int hx, int hz, int cx = 0, int cz = 0)
        {
            for (int x = cx - hx; x <= cx + hx; x++)
                for (int z = cz - hz; z <= cz + hz; z++)
                    v[x, y, z] = true;
        }

        // ---------- Djoser's Step Pyramid ----------

        /// <summary>
        /// Six tiers, each 4 layers (10 m) tall and 4 blocks in from the one below, on a 61 × 55 block (122 × 110 m)
        /// base: 60 m to the flat top, with a capstone at its centre as the last brick. 38,921 blocks.
        /// </summary>
        public static Voxels BuildStep()
        {
            const int hx0 = 30, hz0 = 27, tiers = 6, tierLayers = 4, inset = 4;
            int layers = tiers * tierLayers;
            var v = new Voxels(hx0, layers + 1);
            for (int t = 0; t < tiers; t++)
                for (int k = 0; k < tierLayers; k++)
                    Rect(v, t * tierLayers + k, hx0 - t * inset, hz0 - t * inset);
            v[0, layers, 0] = true;   // capstone
            return v;
        }

        // ---------- the Bent Pyramid ----------

        /// <summary>
        /// A 61-block (122 m) base. The lower part rises steeply, one block in per layer (about 51°), for 13 layers;
        /// then the slope eases to about 43° (in by 1, 1, 2 blocks per layer) up to the tip, the last brick.
        /// </summary>
        public static Voxels BuildBent()
        {
            const int half0 = 30, steep = 13;
            var halves = new List<int>();
            int h = half0;
            for (int y = 0; y <= steep; y++) { halves.Add(h); if (y < steep) h--; }
            int[] pattern = { 1, 1, 2 };
            for (int i = 0; h > 0; i++) { h = Math.Max(0, h - pattern[i % pattern.Length]); halves.Add(h); }
            var v = new Voxels(half0, halves.Count);
            for (int y = 0; y < halves.Count; y++) Rect(v, y, halves[y], halves[y]);
            return v;
        }

        // ---------- a Nubian pyramid field ----------

        /// <summary>
        /// A dozen small steep pyramids in three loose rows, each one block in per two layers (about 68°), like
        /// the royal cemetery at Meroë. The tallest stands in the middle of the back row; its tip is the last brick.
        /// </summary>
        public static Voxels BuildNubian()
        {
            // (x, z, half-base): centres in blocks, half-base in blocks (base = 2 * half + 1)
            var pyramids = new (int x, int z, int half)[]
            {
                (-22, -16, 5), (-8, -18, 6), (8, -17, 8), (23, -15, 5),
                (-26, -1, 4), (-13, 1, 5), (1, 0, 4), (14, 2, 6), (27, 0, 4),
                (-18, 15, 4), (-3, 16, 5), (12, 15, 4),
            };
            int tallest = 0;
            foreach (var p in pyramids) tallest = Math.Max(tallest, p.half);
            var v = new Voxels(32, 2 * tallest + 1);
            foreach (var p in pyramids)
                for (int y = 0; y <= 2 * p.half; y++)
                    Rect(v, y, p.half - y / 2, p.half - y / 2, p.x, p.z);
            return v;
        }

        // ---------- a ziggurat ----------

        /// <summary>
        /// Three terraces in the style of Ur: a 39 × 27 block (78 × 54 m) first terrace 8 layers (20 m) tall, a
        /// second 5 blocks in and 5 layers tall, a shrine 4 blocks further in and 4 layers tall, with a capstone on
        /// its roof as the last brick. A grand central stairway climbs straight up the front to the first terrace
        /// and on to the second; two side stairways run along the front face and meet it at the top.
        /// One layer per block of stair, so every step is one easy climb.
        /// </summary>
        public static Voxels BuildZiggurat()
        {
            const int hx1 = 19, hz1 = 13, h1 = 8;        // first terrace
            const int hx2 = hx1 - 5, hz2 = hz1 - 5, h2 = 5;   // second terrace
            const int hx3 = hx2 - 4, hz3 = hz2 - 4, h3 = 4;   // shrine
            int top = h1 + h2 + h3;
            int radius = hz1 + h1 + 1;
            var v = new Voxels(Math.Max(radius, hx1 + 1), top + 1);

            for (int y = 0; y < h1; y++) Rect(v, y, hx1, hz1);
            for (int y = h1; y < h1 + h2; y++) Rect(v, y, hx2, hz2);
            for (int y = h1 + h2; y < top; y++) Rect(v, y, hx3, hz3);
            v[0, top, 0] = true;   // capstone

            // Central stairway, 3 blocks wide: from the ground in front of the first terrace up to its top...
            for (int d = 1; d <= h1; d++)            // d = blocks out from the front face
                for (int y = 0; y < h1 - d + 1 && y < h1; y++)
                    for (int x = -1; x <= 1; x++) v[x, y, hz1 + d] = true;
            // ...and on up across the first terrace's top to the second terrace
            for (int d = 1; d <= h2; d++)
                for (int y = h1; y < h1 + h2 - d + 1 && y < h1 + h2; y++)
                    for (int x = -1; x <= 1; x++) v[x, y, hz2 + d] = true;

            // Side stairways: 2 blocks deep along the front face, rising from each end towards the middle
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < h1; i++)                       // i blocks along from the outer end
                {
                    int x = s * (hx1 - i);
                    for (int y = 0; y <= i && y < h1; y++)
                        for (int z = hz1 + 1; z <= hz1 + 2; z++) v[x, y, z] = true;
                }
            return v;
        }
    }
}

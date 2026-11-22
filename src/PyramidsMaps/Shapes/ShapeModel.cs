using System;
using System.Collections.Generic;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// A statue modelled in metres as a union of simple smooth solids, with a running bounding box.
    /// Shared by every map's shape; sample it into blocks with <see cref="Sample"/>.
    /// </summary>
    public sealed class ShapeModel
    {
        /// <summary>One solid: a test that is ≤ 0 inside it, and a box around it, so points far away skip the test.</summary>
        private sealed class Part
        {
            public double X0, Y0, Z0, X1, Y1, Z1;
            public Func<double, double, double, double> Test;
        }

        private readonly List<Part> _parts = new List<Part>();
        public double MinX = double.MaxValue, MaxX = double.MinValue, MinZ = double.MaxValue, MaxZ = double.MinValue;
        /// <summary>The highest any part reaches (metres).</summary>
        public double MaxY = double.MinValue;

        private const double BoxSlack = 1e-6;

        private void Add(double x0, double y0, double z0, double x1, double y1, double z1, Func<double, double, double, double> test)
        {
            MaxY = Math.Max(MaxY, y1);
            _parts.Add(new Part { X0 = x0 - BoxSlack, Y0 = y0 - BoxSlack, Z0 = z0 - BoxSlack, X1 = x1 + BoxSlack, Y1 = y1 + BoxSlack, Z1 = z1 + BoxSlack, Test = test });
        }

        public bool Inside(double x, double y, double z)
        {
            foreach (var p in _parts)
            {
                if (x < p.X0 || x > p.X1 || y < p.Y0 || y > p.Y1 || z < p.Z0 || z > p.Z1) continue;
                if (p.Test(x, y, z) <= 0) return true;
            }
            return false;
        }

        /// <summary>The model's highest point above (x, z), or −1 when nothing is there.</summary>
        public double TopAt(double x, double z, double from = 40)
        {
            for (double y = from; y >= 0; y -= 0.05)
                if (Inside(x, y, z)) return y;
            return -1;
        }

        private void Bound(double x, double z, double r)
        {
            MinX = Math.Min(MinX, x - r); MaxX = Math.Max(MaxX, x + r);
            MinZ = Math.Min(MinZ, z - r); MaxZ = Math.Max(MaxZ, z + r);
        }

        public void Ellipsoid(double cx, double cy, double cz, double rx, double ry, double rz)
        {
            Bound(cx, cz, Math.Max(rx, rz));
            Add(cx - rx, cy - ry, cz - rz, cx + rx, cy + ry, cz + rz, (x, y, z) =>
            {
                double dx = (x - cx) / rx, dy = (y - cy) / ry, dz = (z - cz) / rz;
                return dx * dx + dy * dy + dz * dz - 1;
            });
        }

        /// <summary>An axis-aligned box: centre and half-sizes.</summary>
        public void Box(double cx, double cy, double cz, double hx, double hy, double hz)
        {
            Bound(cx, cz, Math.Max(hx, hz));
            Add(cx - hx, cy - hy, cz - hz, cx + hx, cy + hy, cz + hz, (x, y, z) => Math.Max(Math.Abs(x - cx) - hx, Math.Max(Math.Abs(y - cy) - hy, Math.Abs(z - cz) - hz)));
        }

        /// <summary>
        /// A ramp (sampled into blocks it becomes a staircase): half-width hw around x = cx, rising from the
        /// ground at z = zFoot to <paramref name="height"/> at z = zTop.
        /// </summary>
        public void Ramp(double cx, double hw, double zFoot, double zTop, double height)
        {
            double zMin = Math.Min(zFoot, zTop), zMax = Math.Max(zFoot, zTop);
            Bound(cx, (zMin + zMax) / 2, Math.Max(hw, (zMax - zMin) / 2));
            Add(cx - hw, 0, zMin, cx + hw, Math.Max(0, height), zMax, (x, y, z) =>
            {
                if (Math.Abs(x - cx) > hw || z < zMin || z > zMax || y < 0) return 1;
                double top = height * (zFoot - z) / (zFoot - zTop);
                return y <= top ? -1 : 1;
            });
        }

        /// <summary>A capsule from A to B whose radius goes from ra to rb.</summary>
        public void Capsule(double ax, double ay, double az, double bx, double by, double bz, double ra, double rb)
        {
            Bound(ax, az, ra); Bound(bx, bz, rb);
            double cr = Math.Max(ra, rb);
            Add(Math.Min(ax, bx) - cr, Math.Min(ay, by) - cr, Math.Min(az, bz) - cr, Math.Max(ax, bx) + cr, Math.Max(ay, by) + cr, Math.Max(az, bz) + cr, (x, y, z) =>
            {
                double vx = bx - ax, vy = by - ay, vz = bz - az;
                double len2 = vx * vx + vy * vy + vz * vz;
                double t = len2 > 0 ? ((x - ax) * vx + (y - ay) * vy + (z - az) * vz) / len2 : 0;
                t = Math.Max(0, Math.Min(1, t));
                double px = ax + t * vx - x, py = ay + t * vy - y, pz = az + t * vz - z;
                return Math.Sqrt(px * px + py * py + pz * pz) - (ra + (rb - ra) * t);
            });
        }

        /// <summary>
        /// A flattened capsule from A to B: an oval cross-section, half-width w (sideways, level) and half-height h
        /// (in the vertical plane through A–B), each tapering from the A value to the B value. Rounded ends.
        /// For jaws, tails and flat bodies.
        /// </summary>
        public void Flat(double ax, double ay, double az, double bx, double by, double bz, double wa, double wb, double ha, double hb)
        {
            Bound(ax, az, Math.Max(wa, ha)); Bound(bx, bz, Math.Max(wb, hb));
            double vx = bx - ax, vy = by - ay, vz = bz - az;
            double len = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            double ux = vx / len, uy = vy / len, uz = vz / len;
            // side = u × up (level), normal = side × u
            double sx = uz, sz = -ux, sl = Math.Sqrt(sx * sx + sz * sz);
            if (sl < 1e-9) { sx = 1; sz = 0; sl = 1; }
            sx /= sl; sz /= sl;
            double nx = -sz * uy, ny = sz * ux - sx * uz, nz = sx * uy;
            double fr = Math.Max(Math.Max(wa, wb), Math.Max(ha, hb));
            Add(Math.Min(ax, bx) - fr, Math.Min(ay, by) - fr, Math.Min(az, bz) - fr, Math.Max(ax, bx) + fr, Math.Max(ay, by) + fr, Math.Max(az, bz) + fr, (x, y, z) =>
            {
                double px = x - ax, py = y - ay, pz = z - az;
                double along = px * ux + py * uy + pz * uz;
                double t = Math.Max(0, Math.Min(1, along / len));
                double w = wa + (wb - wa) * t, h = ha + (hb - ha) * t;
                double qx = px - ux * t * len, qy = py - uy * t * len, qz = pz - uz * t * len;
                double qs = (qx * sx + qz * sz) / w, qn = (qx * nx + qy * ny + qz * nz) / h;
                double beyond = along < 0 ? along : along > len ? along - len : 0;
                double qa = beyond / Math.Min(w, h);
                return qs * qs + qn * qn + qa * qa - 1;
            });
        }

        /// <summary>An ear: a cone from a wide base at A to a point at B, flattened front-to-back.</summary>
        public void Ear(double ax, double ay, double az, double bx, double by, double bz, double r, double thick)
        {
            Bound(ax, az, r);
            double flat = r / thick;
            double er = r + 0.15;
            Add(Math.Min(ax, bx) - er, Math.Min(ay, by) - er, Math.Min(az, bz) - er, Math.Max(ax, bx) + er, Math.Max(ay, by) + er, Math.Max(az, bz) + er, (x, y, z) =>
            {
                double vx = bx - ax, vy = by - ay, vz = bz - az;
                double len2 = vx * vx + vy * vy + vz * vz;
                double t = Math.Max(0, Math.Min(1, ((x - ax) * vx + (y - ay) * vy + (z - az) * vz) / len2));
                double px = ax + t * vx - x, py = ay + t * vy - y, pz = (az + t * vz - z) * flat;
                return Math.Sqrt(px * px + py * py + pz * pz) - r * (1 - t) - 0.15;
            });
        }

        /// <summary>
        /// Samples the model into blocks from layer <paramref name="firstLayer"/> up: a block is solid when a third
        /// or more of it (9 of 27 samples) lies inside. The model is scaled by <paramref name="scale"/>.
        /// </summary>
        public void Sample(Voxels v, double cellW, double cellH, double scale, int firstLayer)
        {
            for (int x = -v.Radius; x <= v.Radius; x++)
                for (int z = -v.Radius; z <= v.Radius; z++)
                {
                    double mx0 = x * cellW / scale, mz0 = z * cellW / scale;
                    if (mx0 < MinX - 1 || mx0 > MaxX + 1 || mz0 < MinZ - 1 || mz0 > MaxZ + 1) continue;
                    for (int y = firstLayer; y < v.Layers; y++)
                    {
                        // every sample of this layer and above is over the model's top: nothing more in this column
                        if ((y - firstLayer + 0.5 - 0.33) * cellH / scale > MaxY + BoxSlack) break;
                        int inside = 0;
                        for (int i = -1; i <= 1; i++)
                            for (int j = -1; j <= 1; j++)
                                for (int k = -1; k <= 1; k++)
                                {
                                    double mx = (x + i * 0.33) * cellW / scale;
                                    double my = (y - firstLayer + 0.5 + j * 0.33) * cellH / scale;
                                    double mz = (z + k * 0.33) * cellW / scale;
                                    if (Inside(mx, my, mz)) inside++;
                                }
                        if (inside >= 9) v[x, y, z] = true;
                    }
                }
        }

        /// <summary>Removes single blocks sticking out with at most one neighbour (sampling noise).</summary>
        public static void Prune(Voxels v)
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
    }
}

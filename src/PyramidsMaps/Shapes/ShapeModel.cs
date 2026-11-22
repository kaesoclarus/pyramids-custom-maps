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
        private readonly List<Func<double, double, double, double>> _parts = new List<Func<double, double, double, double>>();
        public double MinX = double.MaxValue, MaxX = double.MinValue, MinZ = double.MaxValue, MaxZ = double.MinValue;

        public bool Inside(double x, double y, double z)
        {
            foreach (var p in _parts) if (p(x, y, z) <= 0) return true;
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
            _parts.Add((x, y, z) =>
            {
                double dx = (x - cx) / rx, dy = (y - cy) / ry, dz = (z - cz) / rz;
                return dx * dx + dy * dy + dz * dz - 1;
            });
        }

        /// <summary>A capsule from A to B whose radius goes from ra to rb.</summary>
        public void Capsule(double ax, double ay, double az, double bx, double by, double bz, double ra, double rb)
        {
            Bound(ax, az, ra); Bound(bx, bz, rb);
            _parts.Add((x, y, z) =>
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
            _parts.Add((x, y, z) =>
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
            _parts.Add((x, y, z) =>
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

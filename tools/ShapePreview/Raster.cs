using System;
using System.IO;
using System.IO.Compression;
using PyramidsMaps.Shapes;

/// <summary>
/// Ray-traced preview of a block shape: orthographic camera, sun with shadows, sky light with ambient occlusion.
/// Writes PNG (no external libraries).
/// </summary>
static class Raster
{
    public static byte[] Render(Voxels v, double cw, double ch, double yawDeg, double pitchDeg, int width, int height)
    {
        int R = v.Radius, H = Math.Max(1, v.Height);
        // world box in metres: x,z ∈ [−(R+.5)cw, (R+.5)cw], y ∈ [0, H·ch]
        double yaw = yawDeg * Math.PI / 180, pitch = pitchDeg * Math.PI / 180;
        // camera looks along d; yaw 0 = from the front (+z) looking towards −z
        var d = Norm(new V(-Math.Sin(yaw) * Math.Cos(pitch), -Math.Sin(pitch), -Math.Cos(yaw) * Math.Cos(pitch)));
        var right = Norm(Cross(d, new V(0, 1, 0)));
        var up = Cross(right, d);
        var sun = Norm(new V(-0.45, 0.8, 0.55));

        // fit the shape's bounding box on screen
        double ext = (R + 0.5) * cw, top = H * ch;
        double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
        foreach (var cx in new[] { -ext, ext }) foreach (var cy in new[] { 0.0, top }) foreach (var cz in new[] { -ext, ext })
        {
            var p = new V(cx, cy, cz);
            double u = Dot(p, right), w = Dot(p, up);
            minU = Math.Min(minU, u); maxU = Math.Max(maxU, u); minV = Math.Min(minV, w); maxV = Math.Max(maxV, w);
        }
        // tighten to actual blocks
        double tminU = double.MaxValue, tmaxU = double.MinValue, tminV = double.MaxValue, tmaxV = double.MinValue;
        for (int x = -R; x <= R; x++) for (int z = -R; z <= R; z++) for (int y = 0; y < H; y++)
        {
            if (!v[x, y, z]) continue;
            foreach (var ox in new[] { -.5, .5 }) foreach (var oy in new[] { 0.0, 1.0 }) foreach (var oz in new[] { -.5, .5 })
            {
                var p = new V((x + ox) * cw, (y + oy) * ch, (z + oz) * cw);
                double u = Dot(p, right), w = Dot(p, up);
                tminU = Math.Min(tminU, u); tmaxU = Math.Max(tmaxU, u); tminV = Math.Min(tminV, w); tmaxV = Math.Max(tmaxV, w);
            }
        }
        double pad = 0.06 * Math.Max(tmaxU - tminU, tmaxV - tminV);
        tminU -= pad; tmaxU += pad; tminV -= pad; tmaxV += pad;
        double scale = Math.Min(width / (tmaxU - tminU), height / (tmaxV - tminV));
        double cu = (tminU + tmaxU) / 2, cv = (tminV + tmaxV) / 2;
        var centre = new V(0, top / 2, 0);
        double back = 4 * (ext + top);

        var px = new byte[width * height * 3];
        for (int j = 0; j < height; j++)
            for (int i = 0; i < width; i++)
            {
                double u = cu + (i + 0.5 - width / 2.0) / scale;
                double w = cv - (j + 0.5 - height / 2.0) / scale;
                var o = Add(Add(Mul(right, u), Mul(up, w)), Mul(d, -back));
                // shift origin so u/w are absolute (right/up pass through world origin)
                var col = Shade(v, cw, ch, o, d, sun, j, height);
                int k = (j * width + i) * 3;
                px[k] = col.r; px[k + 1] = col.g; px[k + 2] = col.b;
            }
        return Png(px, width, height);
    }

    static (byte r, byte g, byte b) Shade(Voxels v, double cw, double ch, V o, V d, V sun, int row, int rows)
    {
        // background: soft sky gradient; ground plane at y = 0 catches shadows
        double t0 = row / (double)rows;
        var sky = (r: 236 - 20 * t0, g: 228 - 22 * t0, b: 214 - 26 * t0);
        if (!Trace(v, cw, ch, o, d, out var hit, out var n, out int hx, out int hy, out int hz))
        {
            // ground
            if (d.Y < 0)
            {
                double tg = -o.Y / d.Y;
                var g = Add(o, Mul(d, tg));
                bool inShadow = Trace(v, cw, ch, Add(g, Mul(sun, 0.01)), sun, out _, out _, out _, out _, out _);
                double f = inShadow ? 0.72 : 1.0;
                return ((byte)(sky.r * f), (byte)(sky.g * f), (byte)(sky.b * f * 0.98));
            }
            return ((byte)sky.r, (byte)sky.g, (byte)sky.b);
        }
        double lambert = Math.Max(0, Dot(n, sun));
        bool shadow = lambert > 0 && Trace(v, cw, ch, Add(hit, Mul(n, 0.02)), sun, out _, out _, out _, out _, out _);
        double direct = shadow ? 0 : lambert;
        double ao = Occlusion(v, hx, hy, hz, n);
        double skyLight = 0.42 + 0.18 * n.Y;
        double light = (0.78 * direct + skyLight) * ao;
        // sandstone, a little warmer and lighter higher up
        double heightTint = Math.Min(1, hit.Y / 40.0);
        double r = (196 + 22 * heightTint) * light, gr = (166 + 18 * heightTint) * light, b = (120 + 10 * heightTint) * light;
        // faint block edges so the brickwork reads
        double ex = Frac(hit.X / cw + 0.5), ey = Frac(hit.Y / ch), ez = Frac(hit.Z / cw + 0.5);
        double edge = Math.Min(Math.Min(EdgeDist(ex, n.X == 0), EdgeDist(ey, n.Y == 0)), EdgeDist(ez, n.Z == 0));
        double line = edge < 0.05 ? 0.9 : 1.0;
        return (Clamp(r * line), Clamp(gr * line), Clamp(b * line));
    }

    static double EdgeDist(double f, bool use) => use ? Math.Min(f, 1 - f) : 1;
    static double Frac(double x) => x - Math.Floor(x);
    static byte Clamp(double x) => (byte)Math.Max(0, Math.Min(255, x));

    /// <summary>Ambient occlusion from the blocks around the exposed face.</summary>
    static double Occlusion(Voxels v, int x, int y, int z, V n)
    {
        int nx = x + Math.Sign((int)Math.Round(n.X)), ny = y + Math.Sign((int)Math.Round(n.Y)), nz = z + Math.Sign((int)Math.Round(n.Z));
        int count = 0, total = 0;
        for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) for (int c = -1; c <= 1; c++)
        {
            total++;
            if (v[nx + a, ny + b, nz + c]) count++;
        }
        return 1 - 0.55 * count / total;
    }

    /// <summary>Voxel DDA (Amanatides–Woo) through blocks of cw × ch × cw centred on integer x/z, y from 0.</summary>
    static bool Trace(Voxels v, double cw, double ch, V o, V d, out V hit, out V n, out int hx, out int hy, out int hz)
    {
        hit = default; n = default; hx = hy = hz = 0;
        int R = v.Radius, H = v.Layers;
        // grid coords: gx = x/cw + .5 + R → cell floor(gx) − R ; gy = y/ch
        double gx = o.X / cw + 0.5 + R, gy = o.Y / ch, gz = o.Z / cw + 0.5 + R;
        double dx = d.X / cw, dy = d.Y / ch, dz = d.Z / cw;
        int N = 2 * R + 1;
        // clip to the grid box
        double tmin = 0, tmax = double.MaxValue;
        if (!Slab(gx, dx, 0, N, ref tmin, ref tmax) || !Slab(gy, dy, 0, H, ref tmin, ref tmax) || !Slab(gz, dz, 0, N, ref tmin, ref tmax)) return false;
        double t = tmin + 1e-6;
        double px = gx + dx * t, py = gy + dy * t, pz = gz + dz * t;
        int ix = Math.Min(N - 1, (int)Math.Floor(px)), iy = Math.Min(H - 1, (int)Math.Floor(py)), iz = Math.Min(N - 1, (int)Math.Floor(pz));
        int sx = Math.Sign(dx), sy = Math.Sign(dy), sz = Math.Sign(dz);
        double tdx = sx != 0 ? Math.Abs(1 / dx) : double.MaxValue, tdy = sy != 0 ? Math.Abs(1 / dy) : double.MaxValue, tdz = sz != 0 ? Math.Abs(1 / dz) : double.MaxValue;
        double nxT = sx > 0 ? (ix + 1 - px) * tdx : sx < 0 ? (px - ix) * tdx : double.MaxValue;
        double nyT = sy > 0 ? (iy + 1 - py) * tdy : sy < 0 ? (py - iy) * tdy : double.MaxValue;
        double nzT = sz > 0 ? (iz + 1 - pz) * tdz : sz < 0 ? (pz - iz) * tdz : double.MaxValue;
        // which face we entered through
        int face = 0;
        {
            double ax = Math.Abs(px - Math.Round(px)), ay = Math.Abs(py - Math.Round(py)), az = Math.Abs(pz - Math.Round(pz));
            face = ax < ay && ax < az ? 0 : ay < az ? 1 : 2;
        }
        double tt = t;
        while (ix >= 0 && iy >= 0 && iz >= 0 && ix < N && iy < H && iz < N)
        {
            if (v[ix - R, iy, iz - R])
            {
                hx = ix - R; hy = iy; hz = iz - R;
                var g = new V(gx + dx * tt, gy + dy * tt, gz + dz * tt);
                hit = new V((g.X - 0.5 - R) * cw, g.Y * ch, (g.Z - 0.5 - R) * cw);
                n = face == 0 ? new V(-sx, 0, 0) : face == 1 ? new V(0, -sy, 0) : new V(0, 0, -sz);
                return true;
            }
            if (nxT < nyT && nxT < nzT) { ix += sx; tt = t + nxT; nxT += tdx; face = 0; }
            else if (nyT < nzT) { iy += sy; tt = t + nyT; nyT += tdy; face = 1; }
            else { iz += sz; tt = t + nzT; nzT += tdz; face = 2; }
        }
        return false;
    }

    static bool Slab(double o, double d, double lo, double hi, ref double tmin, ref double tmax)
    {
        if (Math.Abs(d) < 1e-12) return o >= lo && o <= hi;
        double t1 = (lo - o) / d, t2 = (hi - o) / d;
        if (t1 > t2) (t1, t2) = (t2, t1);
        tmin = Math.Max(tmin, t1); tmax = Math.Min(tmax, t2);
        return tmin <= tmax;
    }

    // ---------- tiny vector type ----------
    struct V { public double X, Y, Z; public V(double x, double y, double z) { X = x; Y = y; Z = z; } }
    static V Add(V a, V b) => new V(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    static V Mul(V a, double s) => new V(a.X * s, a.Y * s, a.Z * s);
    static double Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    static V Cross(V a, V b) => new V(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    static V Norm(V a) { double l = Math.Sqrt(Dot(a, a)); return Mul(a, 1 / l); }

    // ---------- PNG ----------
    static byte[] Png(byte[] rgb, int w, int h)
    {
        var raw = new MemoryStream();
        for (int y = 0; y < h; y++) { raw.WriteByte(0); raw.Write(rgb, y * w * 3, w * 3); }
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true)) { raw.Position = 0; raw.CopyTo(zs); }
        var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        BE(ihdr, 0, w); BE(ihdr, 4, h); ihdr[8] = 8; ihdr[9] = 2;
        Chunk(png, "IHDR", ihdr);
        Chunk(png, "IDAT", z.ToArray());
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    static void BE(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BE(len, 0, data.Length); s.Write(len);
        var td = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
        Array.Copy(data, 0, td, 4, data.Length);
        s.Write(td);
        var crc = new byte[4]; BE(crc, 0, (int)Crc(td)); s.Write(crc);
    }

    static uint[] _crcTable;
    static uint Crc(byte[] b)
    {
        if (_crcTable == null)
        {
            _crcTable = new uint[256];
            for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; _crcTable[n] = c; }
        }
        uint crc = 0xFFFFFFFF;
        foreach (var x in b) crc = _crcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }
}

// Prints a build shape's numbers and writes a static HTML preview (no script needed to view it):
// side and front silhouettes, four isometric corner views and a top view.
// Usage: ShapePreview <out.html>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PyramidsMaps.Shapes;

// --shape cat|sobek picks the statue (default cat)
var argList = new List<string>(args);
string shape = "cat";
int si = argList.IndexOf("--shape");
if (si >= 0 && si + 1 < argList.Count) { shape = argList[si + 1].ToLowerInvariant(); argList.RemoveRange(si, 2); }
args = argList.ToArray();
var v = shape == "sobek" ? SobekShape.Build() : SphinxShape.Build();
if (shape == "sobek")
{
    var pool = SobekShape.PoolCells(v, out int pr);
    // how close his footprint comes to the pool's edge (cells with any block, any layer)
    {
        double closest = double.MaxValue; int outside = 0;
        for (int x = -v.Radius; x <= v.Radius; x++) for (int z = -v.Radius; z <= v.Radius; z++)
        {
            if (v.ColumnTop(x, z) == 0) continue;
            if (!pool[x + pr, z + pr]) { outside++; continue; }
            for (int r = 1; r < 40 && r < closest; r++)
            {
                bool hit = false;
                for (int a = 0; a < 32 && !hit; a++)
                {
                    int px = x + pr + (int)Math.Round(r * Math.Cos(a * Math.PI / 16)), pz = z + pr + (int)Math.Round(r * Math.Sin(a * Math.PI / 16));
                    if (px < 0 || pz < 0 || px >= pool.GetLength(0) || pz >= pool.GetLength(1) || !pool[px, pz]) hit = true;
                }
                if (hit) { closest = Math.Min(closest, r); break; }
            }
        }
        Console.WriteLine($"Pool edge: {(outside > 0 ? $"{outside} of his columns are OUTSIDE the pool" : "he's fully inside")}, closest water margin {closest:0} m");
    }
    Raster.PoolLevel = SobekShape.PoolLevel;
    Raster.PoolAt = (x, z) =>
    {
        int ix = (int)Math.Round(x / SobekShape.CellW) + pr, iz = (int)Math.Round(z / SobekShape.CellW) + pr;
        return ix >= 0 && iz >= 0 && ix < pool.GetLength(0) && iz < pool.GetLength(1) && pool[ix, iz];
    };
    // where the pool reaches, in world z (site centre at −44; play area z −124 … 46.4, x ±81)
    double minZ = double.MaxValue, maxZ = double.MinValue, maxX = 0; int cells = 0;
    for (int a = 0; a < pool.GetLength(0); a++) for (int b = 0; b < pool.GetLength(1); b++)
        if (pool[a, b]) { cells++; minZ = Math.Min(minZ, b - pr); maxZ = Math.Max(maxZ, b - pr); maxX = Math.Max(maxX, Math.Abs(a - pr)); }
    Console.WriteLine($"Pool: {cells:N0} m², world z {minZ - 44:0} … {maxZ - 44:0}, x ±{maxX:0}");
}
string title = shape == "sobek" ? "Sobek preview" : "Cat statue preview";
var outPath = args.Length > 0 ? args[0] : "statue-preview.html";

// --gallery <folder>: 1920×1080 renders for a mod page, then stop
if (args.Length > 1 && args[0] == "--gallery")
{
    Directory.CreateDirectory(args[1]);
    foreach (var (file, yaw, pitch) in new[] { ("01-hero", 35.0, 18.0), ("02-front", 0.0, 8.0), ("03-side", -90.0, 6.0), ("04-behind", 215.0, 22.0), ("05-above", 25.0, 58.0) })
    {
        File.WriteAllBytes(Path.Combine(args[1], file + ".png"), Raster.Render(v, SphinxShape.CellW, SphinxShape.CellH, yaw, pitch, 1920, 1080));
        Console.WriteLine($"wrote {file}.png");
    }
    return;
}

int layers = v.Height;
{
    // the last brick: the game finishes on the final cell of the top layer
    int top = layers - 1, n = 0; double sx = 0, sz = 0;
    for (int x = -v.Radius; x <= v.Radius; x++) for (int z = -v.Radius; z <= v.Radius; z++) if (v[x, top, z]) { n++; sx += x; sz += z; }
    Console.WriteLine($"Top layer {top}: {n} blocks around x {sx / Math.Max(1, n):0.#}, z {sz / Math.Max(1, n):0.#} (site cells; +z is the front)");
}
Console.WriteLine($"Statue: {v.CellCount:N0} blocks of {SphinxShape.CellW} x {SphinxShape.CellW} x {SphinxShape.CellH} m, {layers} layers ({layers * SphinxShape.CellH:0.#} m tall), {v.Overhangs} overhanging blocks");
Console.WriteLine("(classic pyramid: 23,426 blocks, 26 layers, 65 m)");
{
    Reach.MaxRise = 1.3; Reach.ReachCells = 6;   // cat worlds allow one 1.25 m layer above your feet
    var left = Reach.GreedyAnyOrder(v, out int waves);
    Console.WriteLine($"Any-order building (touch a laid block; cat reach rule, 1.3 m): " +
        (left.Count == 0 ? $"the whole cat can be built by hand ({waves} waves)." : $"{left.Count} blocks never reachable after {waves} waves."));
    var byLayer = new SortedDictionary<int, int>();
    foreach (var (x, y, z) in left) byLayer[y] = byLayer.TryGetValue(y, out int n) ? n + 1 : 1;
    foreach (var kv in byLayer) Console.WriteLine($"    layer {kv.Key,2}: {kv.Value} left (e.g. {left.Find(c => c.y == kv.Key)})");
}
foreach (var (label, rise, range) in new (string, double?, double)[] { ("layer by layer, game rule", 1.0, 6) })
{
    Reach.MaxRise = rise; Reach.ReachCells = range;
    int totalBad = 0;
    var lines = new List<string>();
    foreach (var r in Reach.Check(v))
    {
        totalBad += r.Unreachable;
        if (r.Unreachable > 0)
            lines.Add($"    layer {r.Layer,2}: {r.Unreachable,4} of {r.Cells,4} out of reach (e.g. x={r.Example.x}, z={r.Example.z})");
    }
    Console.WriteLine($"Reach, {label}: " + (totalBad == 0 ? "every block can be laid." : $"{totalBad} blocks can't be laid."));
    foreach (var l in lines) Console.WriteLine(l);
}

string stats = $"{v.CellCount:N0} blocks ({SphinxShape.CellW:0.##} Ã— {SphinxShape.CellW:0.##} Ã— {SphinxShape.CellH:0.##} m) Â· {layers} layers ({layers * SphinxShape.CellH:0.#} m tall)";
var body = new StringBuilder("<div class=\"row\">");
string stem = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath)), Path.GetFileNameWithoutExtension(outPath));
var views = new (string name, string file, double yaw, double pitch, int w, int h)[]
{
    ("Front three-quarter", "34", 35, 20, 1000, 640),
    ("Front", "front", 0, 6, 1000, 520),
    ("Side (she faces right)", "side", -90, 4, 1000, 460),
    ("From behind", "back", 215, 24, 1000, 640),
    ("From above", "above", 25, 62, 1000, 760),
};
foreach (var (name, file, yaw, pitch, w, h) in views)
{
    var png = Raster.Render(v, SphinxShape.CellW, SphinxShape.CellH, yaw, pitch, w, h);
    File.WriteAllBytes($"{stem}-{file}.png", png);
    body.Append($"<figure><figcaption>{name}</figcaption><img alt=\"{name}\" src=\"data:image/png;base64,{Convert.ToBase64String(png)}\"></figure>");
}
body.Append("</div>");
File.WriteAllText(outPath, Page.Replace("__TITLE__", title).Replace("__STATS__", stats).Replace("__BODY__", body.ToString()));
Console.WriteLine($"Preview written to {Path.GetFullPath(outPath)} and {stem}-*.png");

partial class Program
{
    const string Page = @"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>__TITLE__</title>
<style>
:root{--bg:#f4efe6;--fg:#2b2620;--muted:#6f655a;--panel:#fffaf2;--line:#d9cfbf}
@media (prefers-color-scheme:dark){:root{--bg:#1d1a16;--fg:#efe7da;--muted:#a99d8c;--panel:#26221d;--line:#3a342c}}
body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.4 system-ui,sans-serif}
main{max-width:1200px;margin:0 auto;padding:20px 16px}
h1{font-size:20px;margin:0 0 4px}.sub{color:var(--muted);margin:0 0 16px}
.row{display:flex;flex-wrap:wrap;gap:16px}
figure{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:12px;margin:0 0 16px;flex:1 1 300px;min-width:0}
figure.narrow{max-width:560px}
figcaption{color:var(--muted);font-size:13px;margin-bottom:6px}
img{width:100%;height:auto;display:block;border-radius:6px}
</style></head><body><main>
<h1>__TITLE__</h1><p class=""sub"">__STATS__</p>
__BODY__
</main></body></html>";
}

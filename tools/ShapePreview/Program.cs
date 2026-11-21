// Prints a build shape's numbers and writes a static HTML preview (no script needed to view it):
// side and front silhouettes, four isometric corner views and a top view.
// Usage: ShapePreview <out.html>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using PyramidsMaps.Shapes;

var v = SphinxShape.Build();
var outPath = args.Length > 0 ? args[0] : "statue-preview.html";

int layers = v.Height;
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
File.WriteAllText(outPath, Page.Replace("__STATS__", stats).Replace("__BODY__", body.ToString()));
Console.WriteLine($"Preview written to {Path.GetFullPath(outPath)} and {stem}-*.png");

partial class Program
{
    const string Page = @"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>Cat Statue Preview</title>
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
<h1>Cat statue preview</h1><p class=""sub"">__STATS__ Â· she faces the river</p>
__BODY__
</main></body></html>";
}

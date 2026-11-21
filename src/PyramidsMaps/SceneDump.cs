using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Pyramid.Build;
using Pyramid.Data;
using Pyramid.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PyramidsMaps
{
    /// <summary>
    /// Phase 1 recon: writes the loaded scene to text files, since UnityExplorer doesn't run on Unity 6000.4.
    ///  - hierarchy.txt   every GameObject, its components, position and active state
    ///  - components.txt  serialized fields of the game's map components
    ///  - classic.json    the classic map layout, the reference for new maps
    ///  - terrain_*.raw   16-bit heightmaps (little-endian, row-major, resolution in classic.json)
    /// </summary>
    internal static class SceneDump
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly Type[] MapComponents =
        {
            typeof(PyramidSite), typeof(BuildArea), typeof(RiverEdge), typeof(ClayPit),
            typeof(MapDecorations), typeof(MapDecor), typeof(WorldKindVariant), typeof(Terrain),
        };

        public static void Write(Scene scene, string dir)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "hierarchy.txt"), Hierarchy(scene));
            File.WriteAllText(Path.Combine(dir, "components.txt"), Components());
            File.WriteAllText(Path.Combine(dir, "classic.json"), ClassicJson(dir));
            Plugin.Log.LogInfo($"Scene dump written to {dir}");
        }

        // ---------- hierarchy ----------

        private static string Hierarchy(Scene scene)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Scene '{scene.name}', game {Application.version}, dumped {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine("# name [components] pos=world position (rot y) (inactive)");
            foreach (var root in scene.GetRootGameObjects())
                Walk(root.transform, 0, sb);
            return sb.ToString();
        }

        private static void Walk(Transform t, int depth, StringBuilder sb)
        {
            var comps = t.GetComponents<Component>()
                .Where(c => c != null && !(c is Transform))
                .Select(c => c.GetType().Name);
            sb.Append(' ', depth * 2)
              .Append(t.name)
              .Append(" [").Append(string.Join(", ", comps)).Append(']')
              .Append(" pos=").Append(V(t.position))
              .Append(" yaw=").Append(t.eulerAngles.y.ToString("0.#", Inv));
            if (t.localScale != Vector3.one) sb.Append(" scale=").Append(V(t.localScale));
            if (!t.gameObject.activeSelf) sb.Append(" (inactive)");
            sb.AppendLine();
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), depth + 1, sb);
        }

        // ---------- component fields ----------

        private static string Components()
        {
            var sb = new StringBuilder();
            foreach (var type in MapComponents)
            {
                foreach (var c in UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include))
                {
                    var comp = (Component)c;
                    sb.AppendLine($"== {type.FullName} on '{PathOf(comp.transform)}'");
                    DumpFields(comp, sb, "  ", 0);
                    var b = comp.GetComponent<BoxCollider>();
                    if (b != null) sb.AppendLine($"  [BoxCollider] center={V(b.center)} size={V(b.size)} worldBounds={b.bounds}");
                }
            }

            // ScriptableObjects the pyramid site points at
            foreach (var site in UnityEngine.Object.FindObjectsByType<PyramidSite>(FindObjectsInactive.Include))
            {
                foreach (var def in new[] { site.ClassicDefinition, site.GrandDefinition })
                {
                    if (def == null) continue;
                    sb.AppendLine($"== PyramidDefinition '{def.name}'");
                    DumpFields(def, sb, "  ", 0);
                }
            }
            return sb.ToString();
        }

        private static void DumpFields(object o, StringBuilder sb, string indent, int depth)
        {
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (var t = o.GetType(); t != null && t != typeof(MonoBehaviour) && t != typeof(ScriptableObject) && t != typeof(Component) && t != typeof(object); t = t.BaseType)
            {
                foreach (var f in t.GetFields(F | BindingFlags.DeclaredOnly))
                {
                    bool serialized = f.IsPublic || f.GetCustomAttribute<SerializeField>() != null;
                    if (!serialized || f.IsNotSerialized || f.Name.StartsWith("NetworkInitialize")) continue;
                    object v;
                    try { v = f.GetValue(o); } catch (Exception e) { v = "<" + e.GetType().Name + ">"; }
                    sb.Append(indent).Append(f.FieldType.Name).Append(' ').Append(f.Name).Append(" = ").AppendLine(Show(v));
                }
            }
        }

        private static string Show(object v)
        {
            switch (v)
            {
                case null: return "null";
                case UnityEngine.Object uo when uo == null: return "null (destroyed)";
                case Component c: return $"{c.GetType().Name} on '{PathOf(c.transform)}'";
                case GameObject g: return $"GameObject '{PathOf(g.transform)}'";
                case UnityEngine.Object uo: return $"{uo.GetType().Name} '{uo.name}'";
                case Vector3 v3: return V(v3);
                case Vector2 v2: return $"({v2.x.ToString("0.###", Inv)}, {v2.y.ToString("0.###", Inv)})";
                case float f: return f.ToString("0.####", Inv);
                case string s: return "\"" + s + "\"";
                case IList list:
                {
                    var items = list.Cast<object>().Take(40).Select(Show).ToList();
                    var more = list.Count > 40 ? $", ... ({list.Count} total)" : "";
                    return "[" + string.Join(", ", items) + more + "]";
                }
                default: return Convert.ToString(v, Inv);
            }
        }

        // ---------- classic.json ----------

        private static string ClassicJson(string dir)
        {
            var j = new Json();
            j.Open();
            j.Str("id", "classic").Num("formatVersion", 1).Str("gameVersion", Application.version);

            var site = UnityEngine.Object.FindAnyObjectByType<PyramidSite>(FindObjectsInactive.Include);
            if (site != null)
            {
                j.Key("pyramid").Open()
                    .Vec("position", site.ClassicPosition)
                    .Num("baseSize", site.ClassicDefinition != null ? site.ClassicDefinition.baseSize : 0)
                    .Str("definition", site.ClassicDefinition != null ? site.ClassicDefinition.name : null)
                    .Vec("grandPosition", site.GrandPosition)
                    .Num("grandBaseSize", site.GrandDefinition != null ? site.GrandDefinition.baseSize : 0)
                    .Close();
            }

            j.Key("rivers").OpenArr();
            foreach (var r in UnityEngine.Object.FindObjectsByType<RiverEdge>(FindObjectsInactive.Include))
            {
                j.Open().Str("name", r.name).Vec("position", r.transform.position).Num("yaw", r.transform.eulerAngles.y);
                Box(j, r.GetComponent<BoxCollider>());
                j.Num("bankLandZ", Get<float>(r, "bankLandZ")).Num("toWaterYaw", Get<float>(r, "toWaterYaw")).Num("flowDir", Get<float>(r, "flowDir"));
                j.Close();
            }
            j.CloseArr();

            j.Key("clayPits").OpenArr();
            foreach (var p in UnityEngine.Object.FindObjectsByType<ClayPit>(FindObjectsInactive.Include))
            {
                j.Open().Str("name", p.name).Vec("position", p.transform.position).Num("yaw", p.transform.eulerAngles.y);
                Box(j, p.GetComponent<BoxCollider>());
                j.Close();
            }
            j.CloseArr();

            var tuning = Resources.FindObjectsOfTypeAll<PlayerTuning>().FirstOrDefault();
            if (tuning != null)
            {
                j.Key("playArea").Open()
                    .Vec2("min", tuning.playAreaMin).Vec2("max", tuning.playAreaMax)
                    .Vec2("grandMin", tuning.grandPlayAreaMin).Vec2("grandMax", tuning.grandPlayAreaMax)
                    .Close();
            }

            j.Key("terrains").OpenArr();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
            {
                var d = t.terrainData;
                j.Open().Str("name", t.name).Bool("active", t.gameObject.activeInHierarchy).Vec("position", t.transform.position);
                if (d != null)
                {
                    string raw = "terrain_" + t.name + ".raw";
                    WriteHeights(d, Path.Combine(dir, raw));
                    j.Vec("size", d.size).Num("heightmapResolution", d.heightmapResolution).Str("heightmap", raw)
                     .Num("alphamapResolution", d.alphamapResolution)
                     .Key("layers").OpenArr();
                    foreach (var l in d.terrainLayers ?? new TerrainLayer[0])
                        j.Raw(l != null ? Json.Quote(l.name) : "null");
                    j.CloseArr();
                }
                j.Close();
            }
            j.CloseArr();

            j.Key("decor").OpenArr();
            foreach (var m in UnityEngine.Object.FindObjectsByType<MapDecor>(FindObjectsInactive.Include))
                j.Open().Num("id", Get<byte>(m, "id")).Str("name", Get<string>(m, "displayName") ?? m.name)
                 .Str("path", PathOf(m.transform)).Vec("position", m.transform.position).Num("yaw", m.transform.eulerAngles.y).Close();
            j.CloseArr();

            var variant = UnityEngine.Object.FindAnyObjectByType<WorldKindVariant>(FindObjectsInactive.Include);
            if (variant != null)
            {
                j.Key("worldKindVariant").Open().Str("on", PathOf(variant.transform));
                j.Key("classicOnly").OpenArr();
                foreach (var g in Get<System.Collections.Generic.List<GameObject>>(variant, "classicOnly") ?? new System.Collections.Generic.List<GameObject>())
                    j.Raw(Json.Quote(g != null ? PathOf(g.transform) : null));
                j.CloseArr().Key("grandOnly").OpenArr();
                foreach (var g in Get<System.Collections.Generic.List<GameObject>>(variant, "grandOnly") ?? new System.Collections.Generic.List<GameObject>())
                    j.Raw(Json.Quote(g != null ? PathOf(g.transform) : null));
                j.CloseArr().Close();
            }

            j.Close();
            return j.ToString();
        }

        private static void Box(Json j, BoxCollider b)
        {
            if (b == null) return;
            j.Vec("boxCenter", b.center).Vec("boxSize", b.size).Vec("worldMin", b.bounds.min).Vec("worldMax", b.bounds.max);
        }

        private static void WriteHeights(TerrainData d, string path)
        {
            int n = d.heightmapResolution;
            float[,] h = d.GetHeights(0, 0, n, n);
            using (var w = new BinaryWriter(File.Create(path)))
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        w.Write((ushort)Mathf.Clamp(Mathf.RoundToInt(h[y, x] * 65535f), 0, 65535));
        }

        // ---------- helpers ----------

        private static T Get<T>(object o, string field)
        {
            var f = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return f != null && f.GetValue(o) is T v ? v : default;
        }

        internal static string PathOf(Transform t)
        {
            var p = t.name;
            while ((t = t.parent) != null) p = t.name + "/" + p;
            return p;
        }

        private static string V(Vector3 v) =>
            $"({v.x.ToString("0.###", Inv)}, {v.y.ToString("0.###", Inv)}, {v.z.ToString("0.###", Inv)})";

        /// <summary>Tiny JSON writer (the game's Newtonsoft is available, but this keeps the plugin dependency-free).</summary>
        private sealed class Json
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private bool _needComma;
            private int _depth;

            public static string Quote(string s) =>
                s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

            private Json Sep() { if (_needComma) _sb.Append(','); _sb.Append('\n').Append(' ', _depth * 2); _needComma = true; return this; }
            public Json Key(string k) { Sep(); _sb.Append(Quote(k)).Append(": "); _needComma = false; _pendingValue = true; return this; }
            private bool _pendingValue;
            private Json Val(string v) { if (_pendingValue) { _pendingValue = false; _sb.Append(v); _needComma = true; } else { Sep(); _sb.Append(v); } return this; }
            public Json Raw(string v) => Val(v);
            public Json Open() { Val("{"); _depth++; _needComma = false; return this; }
            public Json Close() { _depth--; _sb.Append('\n').Append(' ', _depth * 2).Append('}'); _needComma = true; return this; }
            public Json OpenArr() { Val("["); _depth++; _needComma = false; return this; }
            public Json CloseArr() { _depth--; _sb.Append('\n').Append(' ', _depth * 2).Append(']'); _needComma = true; return this; }
            public Json Str(string k, string v) => Key(k).Val(Quote(v));
            public Json Num(string k, double v) => Key(k).Val(v.ToString("0.####", Inv));
            public Json Bool(string k, bool v) => Key(k).Val(v ? "true" : "false");
            public Json Vec(string k, Vector3 v) => Key(k).Val($"[{v.x.ToString("0.###", Inv)}, {v.y.ToString("0.###", Inv)}, {v.z.ToString("0.###", Inv)}]");
            public Json Vec2(string k, Vector2 v) => Key(k).Val($"[{v.x.ToString("0.###", Inv)}, {v.y.ToString("0.###", Inv)}]");
            public override string ToString() => _sb.ToString().TrimStart() + "\n";
        }
    }
}

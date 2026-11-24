using System.Collections.Generic;
using UnityEngine;

namespace PyramidsMaps.Statues
{
    /// <summary>
    /// The game's block models (PyramidBlock_0..7) have no bottom face: on its pyramid nobody ever sees one. On
    /// custom maps that overhang (the upside-down maps, the Diamond, statues' arms and jaws) you look up at the
    /// underside and see into the blocks. This adds a flat bottom to each model, once, the first time a custom-map
    /// layout is drawn. It sits a hair above the lowest vertices, so the model's own chamfer ring stays in front of
    /// it where they overlap. On the normal pyramid the extra face is always hidden, so it changes nothing there.
    /// </summary>
    internal static class BlockBottoms
    {
        private static readonly HashSet<int> Done = new HashSet<int>();

        public static void Cap(IEnumerable<Mesh> meshes)
        {
            if (meshes == null) return;
            foreach (var m in meshes)
            {
                if (m == null || !Done.Add(m.GetInstanceID())) continue;
                try { CapOne(m); }
                catch (System.Exception e) { Plugin.Log.LogError($"Block bottoms: couldn't add a bottom to {m.name}: {e.Message}"); }
            }
        }

        private static void CapOne(Mesh m)
        {
            if (!m.isReadable) { Plugin.Log.LogWarning($"Block bottoms: {m.name} isn't readable; left as it is."); return; }
            var v = new List<Vector3>(); m.GetVertices(v);
            if (v.Count == 0) return;
            var b = m.bounds;
            float minY = b.min.y, band = b.size.y * 0.02f;

            // the rectangle spanned by the lowest vertices (the bottom edge of the chamfers)
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var p in v)
                if (p.y <= minY + band) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z); }
            if (x1 - x0 < b.size.x * 0.5f || z1 - z0 < b.size.z * 0.5f)
            {
                float ix = b.size.x * 0.04f, iz = b.size.z * 0.04f;
                x0 = b.min.x + ix; x1 = b.max.x - ix; z0 = b.min.z + iz; z1 = b.max.z - iz;
            }
            float y = minY + b.size.y * 0.001f;

            int first = v.Count;
            v.Add(new Vector3(x0, y, z0)); v.Add(new Vector3(x1, y, z0)); v.Add(new Vector3(x1, y, z1)); v.Add(new Vector3(x0, y, z1));

            // every other vertex channel the model has gets four matching entries
            var n = new List<Vector3>(); m.GetNormals(n);
            var t = new List<Vector4>(); m.GetTangents(t);
            var c = new List<Color>(); m.GetColors(c);
            var uvs = new List<Vector2>[4];
            for (int ch = 0; ch < 4; ch++) { uvs[ch] = new List<Vector2>(); m.GetUVs(ch, uvs[ch]); }
            Color tint = c.Count > 0 ? c[0] : Color.white;
            for (int k = 0; k < 4; k++)
            {
                if (n.Count > 0) n.Add(Vector3.down);
                if (t.Count > 0) t.Add(new Vector4(1f, 0f, 0f, 1f));
                if (c.Count > 0) c.Add(tint);
            }
            var corner = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            for (int ch = 0; ch < 4; ch++)
                if (uvs[ch].Count > 0)
                {
                    // reuse the texture area the model's top uses, so the bottom looks like the same stone
                    Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
                    for (int i = 0; i < first; i++)
                        if (n.Count == 0 || n[i].y > 0.7f) { lo = Vector2.Min(lo, uvs[ch][i]); hi = Vector2.Max(hi, uvs[ch][i]); }
                    if (lo.x > hi.x) { lo = Vector2.zero; hi = Vector2.one; }
                    for (int k = 0; k < 4; k++) uvs[ch].Add(lo + Vector2.Scale(corner[k], hi - lo));
                }

            var tris = new List<int>(); m.GetTriangles(tris, 0);
            // winding with the face pointing down (as the model's own faces wind)
            tris.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });

            m.SetVertices(v);
            if (n.Count > 0) m.SetNormals(n);
            if (t.Count > 0) m.SetTangents(t);
            if (c.Count > 0) m.SetColors(c);
            for (int ch = 0; ch < 4; ch++) if (uvs[ch].Count > 0) m.SetUVs(ch, uvs[ch]);
            m.SetTriangles(tris, 0, false);
            Plugin.Log.LogInfo($"Block bottoms: added a {x1 - x0:0.00} x {z1 - z0:0.00} bottom to {m.name}.");
        }
    }
}

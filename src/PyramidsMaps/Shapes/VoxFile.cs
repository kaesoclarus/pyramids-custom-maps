using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PyramidsMaps.Shapes
{
    /// <summary>
    /// MagicaVoxel .vox files, for player-made maps. Reads the first model's voxels (colours are ignored) and turns
    /// them into a statue: MagicaVoxel's z (up) becomes the layer, its front (−y) faces the river (+z here), the
    /// model is centred on the site and its lowest voxels stand on the ground. No Unity types, so the shape tools
    /// can use it too.
    /// </summary>
    public static class VoxFile
    {
        /// <summary>Largest model accepted, each way (blocks): bigger models would be slow and huge.</summary>
        public const int MaxSize = 256;

        public sealed class Model
        {
            public int SizeX, SizeY, SizeZ;
            public readonly List<(byte x, byte y, byte z)> Voxels = new List<(byte, byte, byte)>();
            /// <summary>How many models the file holds (only the first is used).</summary>
            public int ModelCount;
        }

        public static Model Read(string path) => Read(File.ReadAllBytes(path));

        public static Model Read(byte[] data)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "VOX ") throw new InvalidDataException("not a MagicaVoxel .vox file");
                r.ReadInt32();   // version
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "MAIN") throw new InvalidDataException("no MAIN chunk");
                int mainContent = r.ReadInt32(), mainChildren = r.ReadInt32();
                r.ReadBytes(mainContent);
                long end = r.BaseStream.Position + mainChildren;

                var model = new Model();
                bool haveSize = false, haveVoxels = false;
                while (r.BaseStream.Position + 12 <= end)
                {
                    string id = Encoding.ASCII.GetString(r.ReadBytes(4));
                    int content = r.ReadInt32(), children = r.ReadInt32();
                    long next = r.BaseStream.Position + content + children;
                    if (id == "SIZE")
                    {
                        model.ModelCount++;
                        if (!haveSize)
                        {
                            model.SizeX = r.ReadInt32(); model.SizeY = r.ReadInt32(); model.SizeZ = r.ReadInt32();
                            haveSize = true;
                        }
                    }
                    else if (id == "XYZI" && !haveVoxels)
                    {
                        int n = r.ReadInt32();
                        for (int i = 0; i < n; i++)
                        {
                            byte x = r.ReadByte(), y = r.ReadByte(), z = r.ReadByte();
                            r.ReadByte();   // colour index
                            model.Voxels.Add((x, y, z));
                        }
                        haveVoxels = true;
                    }
                    r.BaseStream.Position = next;
                }
                if (!haveSize || !haveVoxels) throw new InvalidDataException("no model in the file");
                if (model.SizeX > MaxSize || model.SizeY > MaxSize || model.SizeZ > MaxSize)
                    throw new InvalidDataException($"model is {model.SizeX} x {model.SizeY} x {model.SizeZ}; the most is {MaxSize} each way");
                return model;
            }
        }

        /// <summary>The model as a statue: centred, front towards +z, lowest voxels on the ground.</summary>
        public static Voxels ToVoxels(Model m)
        {
            if (m.Voxels.Count == 0) throw new InvalidDataException("the model is empty");
            int minZ = int.MaxValue, maxZ = 0;
            foreach (var p in m.Voxels) { minZ = Math.Min(minZ, p.z); maxZ = Math.Max(maxZ, p.z); }
            int cx = m.SizeX / 2, cy = m.SizeY / 2;
            int radius = Math.Max(Math.Max(cx, m.SizeX - 1 - cx), Math.Max(cy, m.SizeY - 1 - cy)) + 1;
            var v = new Voxels(radius, maxZ - minZ + 1);
            foreach (var p in m.Voxels)
                v[p.x - cx, p.z - minZ, -(p.y - cy)] = true;   // MagicaVoxel's front is −y; ours is +z
            return v;
        }

        /// <summary>Writes a statue as a single-model .vox (all one colour): for examples and for testing the reader.</summary>
        public static void Write(Voxels v, string path)
        {
            int size = v.Size;
            if (size > MaxSize || v.Layers > MaxSize) throw new ArgumentException("too big for a .vox model");
            var cells = new List<(int x, int y, int z)>();
            for (int x = -v.Radius; x <= v.Radius; x++)
                for (int y = 0; y < v.Layers; y++)
                    for (int z = -v.Radius; z <= v.Radius; z++)
                        if (v[x, y, z]) cells.Add((x + v.Radius, v.Radius - z, y));   // back to MagicaVoxel axes
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int sizeChunk = 12 + 12, xyziChunk = 12 + 4 + 4 * cells.Count;
                w.Write(Encoding.ASCII.GetBytes("VOX ")); w.Write(150);
                w.Write(Encoding.ASCII.GetBytes("MAIN")); w.Write(0); w.Write(sizeChunk + xyziChunk);
                w.Write(Encoding.ASCII.GetBytes("SIZE")); w.Write(12); w.Write(0);
                w.Write(size); w.Write(size); w.Write(v.Layers);
                w.Write(Encoding.ASCII.GetBytes("XYZI")); w.Write(4 + 4 * cells.Count); w.Write(0);
                w.Write(cells.Count);
                foreach (var (x, y, z) in cells) { w.Write((byte)x); w.Write((byte)y); w.Write((byte)z); w.Write((byte)1); }
            }
        }

        /// <summary>FNV-1a over the file's bytes: identifies a map's exact contents (saves and multiplayer).</summary>
        public static uint Hash(byte[] data)
        {
            uint h = 2166136261;
            foreach (var b in data) { h ^= b; h *= 16777619; }
            return h;
        }
    }
}

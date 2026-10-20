using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BonkTelegraphs
{
    sealed class Quad
    {
        public float MinX, MaxX, MinY, MaxY;
        public float Z = 0.03f;
        public string Texture;
        public uint GrowMs;
        public uint WaitMs;
        public bool GrowAlongX;
        public bool FaceCamera;
        public uint SpinMs;
        public bool CastsNoShadow;
    }

    sealed class Mesh
    {
        public readonly List<float[]> Corners = new List<float[]>();
        public readonly List<ushort> Triangles = new List<ushort>();
    }

    static class M2
    {
        const uint HoldMs = 10000;
        const uint BoneTransformed = 0x200;
        const uint BoneBillboardLockZ = 0x40;
        const int SpinKeys = 24;
        const ushort StandAnim = 0, HoldAnim = 158;
        const uint SpinStandMs = 1000, SpinHoldStartMs = 1333;
        const uint StandFlags = 1;
        const short Opaque = 0x7FFF;

        public static byte[] Write(string name, Quad quad, byte[] playableLookup)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);
            w.Write(new byte[324]);
            Dictionary<string, KeyValuePair<uint, uint>> arrays = new Dictionary<string, KeyValuePair<uint, uint>>();

            uint nameOfs = Begin(w);
            w.Write(Encoding.ASCII.GetBytes(name + "\0"));
            arrays["name"] = Pair((uint)name.Length + 1, nameOfs);

            uint length = quad.GrowMs > 0 ? quad.WaitMs + quad.GrowMs + HoldMs : 1000;
            float radius = Math.Max(Math.Max(Math.Abs(quad.MinX), Math.Abs(quad.MaxX)), Math.Max(Math.Abs(quad.MinY), Math.Abs(quad.MaxY))) * 1.5f;

            uint ofs = Begin(w);
            if (quad.SpinMs > 0)
            {
                WriteSequence(w, quad, StandAnim, 0, SpinStandMs, StandFlags, radius);
                WriteSequence(w, quad, HoldAnim, SpinHoldStartMs, SpinHoldStartMs + quad.SpinMs, 0, radius);
                arrays["animations"] = Pair(2, ofs);
                arrays["animationLookup"] = Pair(HoldAnim + 1u, WriteHoldAnimationLookup(w));
                playableLookup = PlayHoldAsHold(playableLookup);
            }
            else
            {
                WriteSequence(w, quad, StandAnim, 0, length, 0, radius);
                arrays["animations"] = Pair(1, ofs);
                arrays["animationLookup"] = Pair(1, WriteShorts(w, 0));
            }

            ofs = Begin(w);
            w.Write(playableLookup);
            arrays["playableAnimationLookup"] = Pair((uint)(playableLookup.Length / 4), ofs);

            Mesh mesh = Square(quad);
            uint rangesOfs = 0, timesOfs = 0, valuesOfs = 0, keyCount = 0;
            if (quad.GrowMs > 0)
            {
                List<uint> times = new List<uint> { 0 };
                if (quad.WaitMs > 0)
                    times.Add(quad.WaitMs);
                times.Add(quad.WaitMs + quad.GrowMs);
                times.Add(length);
                keyCount = (uint)times.Count;

                rangesOfs = Begin(w);
                w.Write(0u);
                w.Write(keyCount - 1);
                timesOfs = Begin(w);
                foreach (uint t in times)
                    w.Write(t);
                valuesOfs = Begin(w);
                float speck = quad.GrowAlongX ? 0.001f : 0.02f;
                for (int i = 0; i < keyCount; i++)
                {
                    bool full = i >= keyCount - 2;
                    w.Write(full ? 1f : speck);
                    w.Write(full || quad.GrowAlongX ? 1f : speck);
                    w.Write(1f);
                }
            }
            ofs = Begin(w);
            WriteBone(w, BoneFlags(quad), rangesOfs, keyCount, timesOfs, quad.GrowMs > 0 ? valuesOfs : 0);
            arrays["bones"] = Pair(1, ofs);

            ofs = Begin(w);
            w.Write((short)-1);
            arrays["keyBoneLookup"] = Pair(1, ofs);

            ofs = Begin(w);
            for (int v = 0; v < mesh.Corners.Count; v++)
            {
                float[] c = mesh.Corners[v];
                w.Write(c[0]); w.Write(c[1]); w.Write(quad.Z);
                w.Write(new byte[] { 255, 0, 0, 0 });
                w.Write(new byte[] { 0, 0, 0, 0 });
                w.Write(0f); w.Write(0f); w.Write(1f);
                w.Write(c[2]);
                w.Write(c[3]);
                w.Write(0f); w.Write(0f);
            }
            uint vertexCount = (uint)mesh.Corners.Count, indexCount = (uint)mesh.Triangles.Count;
            arrays["vertices"] = Pair(vertexCount, ofs);

            uint[] mostBones = { 256, 75, 53, 21 };
            uint[,] viewOfs = new uint[4, 5];
            for (int view = 0; view < 4; view++)
            {
                viewOfs[view, 0] = Begin(w);
                for (int i = 0; i < vertexCount; i++)
                    w.Write((ushort)i);
                viewOfs[view, 1] = Begin(w);
                foreach (ushort i in mesh.Triangles)
                    w.Write(i);
                viewOfs[view, 2] = Begin(w);
                w.Write(new byte[4 * vertexCount]);
                viewOfs[view, 3] = Begin(w);
                w.Write((ushort)0);
                w.Write((ushort)0);
                w.Write((ushort)0);
                w.Write((ushort)vertexCount);
                w.Write((ushort)0);
                w.Write((ushort)indexCount);
                w.Write((ushort)1);
                w.Write((ushort)0);
                w.Write((ushort)1);
                w.Write((ushort)0);
                w.Write((quad.MinX + quad.MaxX) / 2); w.Write((quad.MinY + quad.MaxY) / 2); w.Write(quad.Z);
                viewOfs[view, 4] = Begin(w);
                w.Write((byte)0x04);
                w.Write((sbyte)0);
                w.Write((ushort)0);
                w.Write((ushort)0);
                w.Write((ushort)0);
                w.Write((short)-1);
                w.Write((ushort)0);
                w.Write((ushort)0);
                w.Write((ushort)1);
                w.Write((ushort)0);
                w.Write((ushort)0);
                w.Write((ushort)0);
                w.Write((ushort)0);
            }
            ofs = Begin(w);
            for (int view = 0; view < 4; view++)
            {
                w.Write(vertexCount); w.Write(viewOfs[view, 0]);
                w.Write(indexCount); w.Write(viewOfs[view, 1]);
                w.Write(vertexCount); w.Write(viewOfs[view, 2]);
                w.Write(1u); w.Write(viewOfs[view, 3]);
                w.Write(1u); w.Write(viewOfs[view, 4]);
                w.Write(mostBones[view]);
            }
            arrays["views"] = Pair(4, ofs);

            uint textureNameOfs = Begin(w);
            w.Write(Encoding.ASCII.GetBytes(quad.Texture + "\0"));
            ofs = Begin(w);
            w.Write(0u);
            w.Write(0u);
            w.Write((uint)quad.Texture.Length + 1);
            w.Write(textureNameOfs);
            arrays["textures"] = Pair(1, ofs);

            arrays["transparency"] = Pair(1, quad.SpinMs > 0 ? WriteOpaqueOverSequences(w) : WriteOpaque(w));

            arrays["texReplace"] = Pair(1, WriteShorts(w, 0));
            ofs = Begin(w);
            w.Write((ushort)0x17);
            w.Write((ushort)2);
            arrays["renderFlags"] = Pair(1, ofs);
            arrays["boneLookup"] = Pair(4, WriteShorts(w, 0, 0, 0, 0));
            arrays["texLookup"] = Pair(1, WriteShorts(w, 0));
            arrays["texUnitLookup"] = Pair(1, WriteShorts(w, 0));
            arrays["transparencyLookup"] = Pair(1, WriteShorts(w, 0));
            if (quad.SpinMs > 0)
            {
                arrays["uvAnimations"] = Pair(1, WriteSpin(w, quad.SpinMs));
                arrays["uvAnimationLookup"] = Pair(1, WriteShorts(w, 0));
            }
            else
                arrays["uvAnimationLookup"] = Pair(1, WriteShorts(w, -1));

            ms.Position = 0;
            w.Write(Encoding.ASCII.GetBytes("MD20"));
            w.Write(256u);
            WritePair(w, arrays, "name");
            w.Write(0u);
            foreach (string array in new[] { "globalSequences", "animations", "animationLookup", "playableAnimationLookup", "bones", "keyBoneLookup",
                "vertices", "views", "colors", "textures", "transparency", "textureFlipbooks", "uvAnimations", "texReplace", "renderFlags",
                "boneLookup", "texLookup", "texUnitLookup", "transparencyLookup", "uvAnimationLookup" })
                WritePair(w, arrays, array);
            WriteBounds(w, quad, true);
            w.Write(radius);
            w.Write(new byte[28]);
            for (int i = 0; i < 11; i++)
                w.Write(0UL);
            if (ms.Position != 324)
                throw new InvalidOperationException("M2 header came out " + ms.Position + " bytes.");
            return ms.ToArray();
        }

        static Mesh Square(Quad quad)
        {
            Mesh mesh = new Mesh();
            foreach (float[] c in new[] { new[] { quad.MinX, quad.MinY }, new[] { quad.MaxX, quad.MinY }, new[] { quad.MaxX, quad.MaxY }, new[] { quad.MinX, quad.MaxY } })
                mesh.Corners.Add(new[] { c[0], c[1], (c[0] - quad.MinX) / (quad.MaxX - quad.MinX), (c[1] - quad.MinY) / (quad.MaxY - quad.MinY) });
            mesh.Triangles.AddRange(new ushort[] { 0, 1, 2, 0, 2, 3 });
            return mesh;
        }

        static void WriteBone(BinaryWriter w, uint flags, uint rangesOfs, uint keyCount, uint timesOfs, uint scalesOfs)
        {
            w.Write(-1);
            w.Write(flags);
            w.Write((short)-1);
            w.Write((ushort)0);
            WriteEmptyTrack(w);
            WriteEmptyTrack(w);
            if (scalesOfs > 0)
            {
                w.Write((ushort)1);
                w.Write((short)-1);
                w.Write(1u); w.Write(rangesOfs);
                w.Write(keyCount); w.Write(timesOfs);
                w.Write(keyCount); w.Write(scalesOfs);
            }
            else
                WriteEmptyTrack(w);
            w.Write(0f); w.Write(0f); w.Write(0f);
        }

        static uint BoneFlags(Quad quad)
        {
            uint flags = 0;
            if (quad.GrowMs > 0 || quad.FaceCamera)
                flags |= BoneTransformed;
            if (quad.FaceCamera)
                flags |= BoneBillboardLockZ;
            return flags;
        }

        static void WriteSequence(BinaryWriter w, Quad quad, ushort anim, uint start, uint end, uint flags, float radius)
        {
            w.Write(anim);
            w.Write((ushort)0);
            w.Write(start);
            w.Write(end);
            w.Write(0f);
            w.Write(flags);
            w.Write((short)0x7FFF);
            w.Write((ushort)0);
            w.Write(0u);
            w.Write(0u);
            w.Write(150u);
            WriteBounds(w, quad, false);
            w.Write(radius);
            w.Write((short)-1);
            w.Write((ushort)0);
        }

        static uint WriteHoldAnimationLookup(BinaryWriter w)
        {
            short[] lookup = new short[HoldAnim + 1];
            for (int i = 0; i < lookup.Length; i++)
                lookup[i] = -1;
            lookup[StandAnim] = 0;
            lookup[HoldAnim] = 1;
            return WriteShorts(w, lookup);
        }

        static byte[] PlayHoldAsHold(byte[] playableLookup)
        {
            byte[] lookup = (byte[])playableLookup.Clone();
            int entry = HoldAnim * 4;
            if (entry + 4 <= lookup.Length)
            {
                BitConverter.GetBytes(HoldAnim).CopyTo(lookup, entry);
                BitConverter.GetBytes((ushort)0).CopyTo(lookup, entry + 2);
            }
            return lookup;
        }

        static uint WriteOpaque(BinaryWriter w)
        {
            uint timesOfs = Begin(w);
            w.Write(0u);
            uint valuesOfs = Begin(w);
            w.Write(Opaque);
            uint ofs = Begin(w);
            w.Write((ushort)0);
            w.Write((short)-1);
            w.Write(0u); w.Write(0u);
            w.Write(1u); w.Write(timesOfs);
            w.Write(1u); w.Write(valuesOfs);
            return ofs;
        }

        static uint WriteOpaqueOverSequences(BinaryWriter w)
        {
            uint rangesOfs = Begin(w);
            w.Write(0u); w.Write(0u);
            w.Write(1u); w.Write(1u);
            uint timesOfs = Begin(w);
            w.Write(0u);
            w.Write(SpinHoldStartMs);
            uint valuesOfs = Begin(w);
            w.Write(Opaque);
            w.Write(Opaque);
            uint ofs = Begin(w);
            w.Write((ushort)0);
            w.Write((short)-1);
            w.Write(2u); w.Write(rangesOfs);
            w.Write(2u); w.Write(timesOfs);
            w.Write(2u); w.Write(valuesOfs);
            return ofs;
        }

        static uint WriteSpin(BinaryWriter w, uint spinMs)
        {
            uint keys = SpinKeys + 2;
            uint rangesOfs = Begin(w);
            w.Write(0u); w.Write(0u);
            w.Write(1u); w.Write(keys - 1);
            uint timesOfs = Begin(w);
            w.Write(0u);
            for (int key = 0; key <= SpinKeys; key++)
                w.Write(SpinHoldStartMs + (uint)((ulong)spinMs * (ulong)key / SpinKeys));
            uint valuesOfs = Begin(w);
            w.Write(0f); w.Write(0f); w.Write(0f); w.Write(1f);
            for (int key = 0; key <= SpinKeys; key++)
            {
                double half = Math.PI * key / SpinKeys;
                w.Write(0f); w.Write(0f); w.Write((float)Math.Sin(half)); w.Write((float)Math.Cos(half));
            }
            uint ofs = Begin(w);
            WriteEmptyTrack(w);
            w.Write((ushort)1);
            w.Write((short)-1);
            w.Write(2u); w.Write(rangesOfs);
            w.Write(keys); w.Write(timesOfs);
            w.Write(keys); w.Write(valuesOfs);
            WriteEmptyTrack(w);
            return ofs;
        }

        static KeyValuePair<uint, uint> Pair(uint count, uint offset)
        {
            return new KeyValuePair<uint, uint>(count, offset);
        }

        static void WritePair(BinaryWriter w, Dictionary<string, KeyValuePair<uint, uint>> arrays, string name)
        {
            KeyValuePair<uint, uint> pair;
            if (arrays.TryGetValue(name, out pair))
            {
                w.Write(pair.Key);
                w.Write(pair.Value);
            }
            else
                w.Write(0UL);
        }

        static uint Begin(BinaryWriter w)
        {
            long pad = (16 - w.BaseStream.Position % 16) % 16;
            w.Write(new byte[pad]);
            return (uint)w.BaseStream.Position;
        }

        static uint WriteShorts(BinaryWriter w, params short[] values)
        {
            uint ofs = Begin(w);
            foreach (short v in values)
                w.Write(v);
            return ofs;
        }

        static void WriteEmptyTrack(BinaryWriter w)
        {
            w.Write((ushort)0);
            w.Write((short)-1);
            w.Write(new byte[24]);
        }

        static void WriteBounds(BinaryWriter w, Quad quad, bool header)
        {
            bool flat = !header && quad.CastsNoShadow;
            w.Write(flat ? 0f : quad.MinX); w.Write(flat ? 0f : quad.MinY); w.Write(0f);
            w.Write(flat ? 0f : quad.MaxX); w.Write(flat ? 0f : quad.MaxY); w.Write(header ? 0.5f : quad.Z);
        }
    }
}

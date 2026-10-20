using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BonkTelegraphs
{
    static class Dump
    {
        static readonly string[] M2Arrays = {
            "globalSequences", "animations", "animationLookup", "playableAnimationLookup", "bones", "keyBoneLookup", "vertices", "views",
            "colors", "textures", "transparency", "textureFlipbooks", "uvAnimations", "texReplace", "renderFlags", "boneLookup", "texLookup",
            "texUnitLookup", "transparencyLookup", "uvAnimationLookup" };
        static readonly string[] M2ArraysAfter = {
            "collisionIndices", "collisionPositions", "collisionNormals", "attachments", "attachmentLookup", "events", "lights", "cameras",
            "cameraLookup", "ribbons", "particles" };

        public static void List(string mpq, string pattern)
        {
            using (MpqReader reader = new MpqReader(mpq))
            {
                byte[] list = reader.Read("(listfile)");
                if (list == null)
                {
                    Console.WriteLine("No (listfile).");
                    return;
                }
                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
                foreach (string line in Encoding.ASCII.GetString(list).Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    if (regex.IsMatch(line))
                        Console.WriteLine(line);
            }
        }

        public static void M2(byte[] data)
        {
            BinaryReader r = new BinaryReader(new MemoryStream(data));
            Console.WriteLine("magic {0} version {1} size {2}", Encoding.ASCII.GetString(r.ReadBytes(4)), r.ReadUInt32(), data.Length);
            uint nameCount = r.ReadUInt32(), nameOfs = r.ReadUInt32();
            Console.WriteLine("name '{0}' globalFlags {1}", Encoding.ASCII.GetString(data, (int)nameOfs, (int)Math.Max(0, nameCount - 1)), r.ReadUInt32());
            uint[] counts = new uint[M2Arrays.Length], offsets = new uint[M2Arrays.Length];
            for (int i = 0; i < M2Arrays.Length; i++)
            {
                counts[i] = r.ReadUInt32();
                offsets[i] = r.ReadUInt32();
                Console.WriteLine("  {0,-24} {1,6} @ {2}", M2Arrays[i], counts[i], offsets[i]);
            }
            float[] floats = new float[14];
            for (int i = 0; i < 14; i++)
                floats[i] = r.ReadSingle();
            Console.WriteLine("  bounds {0}", string.Join(" ", Array.ConvertAll(floats, f => f.ToString("0.###"))));
            for (int i = 0; i < M2ArraysAfter.Length; i++)
                Console.WriteLine("  {0,-24} {1,6} @ {2}", M2ArraysAfter[i], r.ReadUInt32(), r.ReadUInt32());
            Console.WriteLine("header ends at {0}", r.BaseStream.Position);
        }

        public static void Hex(byte[] data, int offset, int length)
        {
            for (int row = 0; row < length; row += 16)
            {
                StringBuilder line = new StringBuilder();
                line.AppendFormat("{0,6}: ", offset + row);
                for (int i = 0; i < 16 && row + i < length && offset + row + i < data.Length; i++)
                    line.AppendFormat("{0:X2} ", data[offset + row + i]);
                Console.WriteLine(line);
            }
        }

        public static void Blp(byte[] data)
        {
            BinaryReader r = new BinaryReader(new MemoryStream(data));
            Console.WriteLine("magic {0} type {1} compression {2} alphaDepth {3} alphaType {4} hasMips {5} {6}x{7} size {8}",
                Encoding.ASCII.GetString(r.ReadBytes(4)), r.ReadUInt32(), r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte(),
                r.ReadUInt32(), r.ReadUInt32(), data.Length);
            uint[] offsets = new uint[16], sizes = new uint[16];
            for (int i = 0; i < 16; i++)
                offsets[i] = r.ReadUInt32();
            for (int i = 0; i < 16; i++)
                sizes[i] = r.ReadUInt32();
            for (int i = 0; i < 16 && offsets[i] != 0; i++)
                Console.WriteLine("  mip {0}: {1} bytes @ {2}", i, sizes[i], offsets[i]);
        }
    }
}

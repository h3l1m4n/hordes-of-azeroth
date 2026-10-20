using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace HordesOfAzeroth.Launcher
{
    static class ClientPatcher
    {
        sealed class Patch
        {
            public int Offset;
            public byte[] Original;
            public byte[] Patched;
        }

        const int FieldOfViewOffset = 0x4089B4;
        const float ClassicFieldOfView = (float)(Math.PI / 2);
        const double WidescreenReferenceFieldOfView = 1.925;
        const double WidescreenReferenceAspect = 16.0 / 9.0;
        const double ClassicAspect = 4.0 / 3.0;
        const double MaxFieldOfView = 2.6;

        public static void Apply(GameFolder folder)
        {
            Dictionary<string, object> spec = ReadSpec(folder);
            string source = Path.Combine(folder.Root, spec["source"] as string);
            string target = Path.Combine(folder.Root, spec["target"] as string);
            List<Patch> patches = ((object[])spec["patches"]).OfType<Dictionary<string, object>>().Select(p => new Patch
            {
                Offset = int.Parse(((string)p["offset"]).Replace("0x", ""), NumberStyles.HexNumber),
                Original = Bytes((string)p["original"]),
                Patched = Bytes((string)p["patched"]),
            }).ToList();

            if (File.Exists(target))
            {
                byte[] existing = File.ReadAllBytes(target);
                if (patches.Where(p => p.Offset != FieldOfViewOffset).All(p => Matches(existing, p.Offset, p.Patched)))
                    return;
            }
            if (!string.Equals(Md5(source), spec["sourceMd5"] as string, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("WoW.exe is not the English 1.12.1 (build 5875) client, so it can't be patched.");

            byte[] data = File.ReadAllBytes(source);
            foreach (Patch patch in patches)
            {
                if (patch.Original.Length != patch.Patched.Length || !Matches(data, patch.Offset, patch.Original))
                    throw new InvalidDataException($"WoW.exe doesn't match the expected bytes at 0x{patch.Offset:X6}.");
                Array.Copy(patch.Patched, 0, data, patch.Offset, patch.Patched.Length);
            }
            File.WriteAllBytes(target, data);
        }

        public static void SetFieldOfView(GameFolder folder, bool widescreen)
        {
            string target = Path.Combine(folder.Root, ReadSpec(folder)["target"] as string);
            if (!File.Exists(target))
                return;
            byte[] wanted = BitConverter.GetBytes(widescreen ? WidescreenFieldOfView(GameConfig.Resolution(folder)) : ClassicFieldOfView);
            using (FileStream stream = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                byte[] current = new byte[wanted.Length];
                stream.Seek(FieldOfViewOffset, SeekOrigin.Begin);
                if (stream.Read(current, 0, current.Length) == current.Length && current.SequenceEqual(wanted))
                    return;
                stream.Seek(FieldOfViewOffset, SeekOrigin.Begin);
                stream.Write(wanted, 0, wanted.Length);
            }
        }

        static float WidescreenFieldOfView(string resolution)
        {
            double aspect = ParseAspect(resolution);
            if (aspect <= ClassicAspect)
                return ClassicFieldOfView;
            double verticalSlope = Math.Tan(WidescreenReferenceFieldOfView / 2) / WidescreenReferenceAspect;
            return (float)Math.Min(MaxFieldOfView, 2 * Math.Atan(verticalSlope * aspect));
        }

        static double ParseAspect(string resolution)
        {
            string[] parts = (resolution ?? "").Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height) && width > 0 && height > 0)
                return (double)width / height;
            return WidescreenReferenceAspect;
        }

        static Dictionary<string, object> ReadSpec(GameFolder folder)
        {
            string path = Path.Combine(folder.Config, "client-patches.json");
            return (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path));
        }

        static bool Matches(byte[] data, int offset, byte[] expected)
        {
            if (offset + expected.Length > data.Length)
                return false;
            for (int i = 0; i < expected.Length; i++)
                if (data[offset + i] != expected[i])
                    return false;
            return true;
        }

        static byte[] Bytes(string hex) => hex.Split(' ').Select(b => Convert.ToByte(b, 16)).ToArray();

        static string Md5(string path)
        {
            using (MD5 md5 = MD5.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    sealed class Image
    {
        public readonly int Width, Height;
        public readonly float[] R, G, B, A;

        public Image(int width, int height)
        {
            Width = width;
            Height = height;
            R = new float[width * height];
            G = new float[width * height];
            B = new float[width * height];
            A = new float[width * height];
        }

        public void Set(int x, int y, float r, float g, float b, float a)
        {
            int i = y * Width + x;
            R[i] = r;
            G[i] = g;
            B[i] = b;
            A[i] = a;
        }

        public void ClearEdge()
        {
            for (int x = 0; x < Width; x++)
            {
                A[x] = 0;
                A[(Height - 1) * Width + x] = 0;
            }
            for (int y = 0; y < Height; y++)
            {
                A[y * Width] = 0;
                A[y * Width + Width - 1] = 0;
            }
        }

        public Image Half()
        {
            Image half = new Image(Math.Max(1, Width / 2), Math.Max(1, Height / 2));
            for (int y = 0; y < half.Height; y++)
                for (int x = 0; x < half.Width; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int sx = Math.Min(Width - 1, x * 2 + dx), sy = Math.Min(Height - 1, y * 2 + dy);
                            int i = sy * Width + sx;
                            float w = A[i] + 0.001f;
                            r += R[i] * w;
                            g += G[i] * w;
                            b += B[i] * w;
                            a += w;
                        }
                    half.Set(x, y, r / a, g / a, b / a, Math.Max(0, (a - 0.004f) / 4));
                }
            return half;
        }
    }

    delegate void ColourRule(ref float r, ref float g, ref float b);

    static class Blp
    {
        const int PaletteOffset = 148;

        public static bool CanRecolour(byte[] blp)
        {
            return blp.Length > PaletteOffset + 1024 && BitConverter.ToUInt32(blp, 0) == 0x32504C42u && (blp[8] == 1 || blp[8] == 2);
        }

        public static byte[] Recolour(byte[] blp, ColourRule rule)
        {
            byte[] result = (byte[])blp.Clone();
            if (!CanRecolour(blp))
                throw new NotSupportedException("Only paletted and DXT BLP2 textures can be recoloured.");
            if (blp[8] == 1)
            {
                RecolourPalette(result, rule);
                return result;
            }
            bool dxt1 = blp[10] == 0;
            int blockSize = dxt1 ? 8 : 16, colourAt = dxt1 ? 0 : 8;
            for (int mip = 0; mip < 16; mip++)
            {
                uint offset = BitConverter.ToUInt32(blp, 20 + mip * 4), size = BitConverter.ToUInt32(blp, 84 + mip * 4);
                if (offset == 0 || size == 0)
                    break;
                for (uint block = offset; block + blockSize <= offset + size; block += (uint)blockSize)
                    RecolourBlock(result, (int)block + colourAt, dxt1, rule);
            }
            return result;
        }

        static void RecolourPalette(byte[] data, ColourRule rule)
        {
            for (int i = 0; i < 256; i++)
            {
                int at = PaletteOffset + i * 4;
                float b = data[at] / 255f, g = data[at + 1] / 255f, r = data[at + 2] / 255f;
                rule(ref r, ref g, ref b);
                data[at] = (byte)Math.Round(Math.Min(1f, b) * 255);
                data[at + 1] = (byte)Math.Round(Math.Min(1f, g) * 255);
                data[at + 2] = (byte)Math.Round(Math.Min(1f, r) * 255);
            }
        }

        static void RecolourBlock(byte[] data, int at, bool dxt1, ColourRule rule)
        {
            ushort c0 = BitConverter.ToUInt16(data, at), c1 = BitConverter.ToUInt16(data, at + 2);
            uint indices = BitConverter.ToUInt32(data, at + 4);
            ushort n0 = Recolour565(c0, rule), n1 = Recolour565(c1, rule);

            if (!dxt1 || c0 > c1)
            {
                if (n0 < n1)
                {
                    ushort t = n0; n0 = n1; n1 = t;
                    indices ^= 0x55555555;
                }
                else if (n0 == n1)
                    indices = 0;
            }
            else if (n0 > n1)
            {
                ushort t = n0; n0 = n1; n1 = t;
                for (int i = 0; i < 16; i++)
                    if (((indices >> (i * 2)) & 3) < 2)
                        indices ^= 1u << (i * 2);
            }

            Buffer.BlockCopy(BitConverter.GetBytes(n0), 0, data, at, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(n1), 0, data, at + 2, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(indices), 0, data, at + 4, 4);
        }

        static ushort Recolour565(ushort c, ColourRule rule)
        {
            float[] rgb = From565(c);
            rule(ref rgb[0], ref rgb[1], ref rgb[2]);
            return To565(rgb[0], rgb[1], rgb[2]);
        }

        public static byte[] WriteDxt3(Image image, bool clearEdge = false)
        {
            List<byte[]> mips = new List<byte[]>();
            for (Image level = image; ; level = level.Half())
            {
                if (clearEdge)
                    level.ClearEdge();
                mips.Add(EncodeDxt3(level));
                if (level.Width == 1 && level.Height == 1)
                    break;
            }

            using (MemoryStream output = new MemoryStream())
            {
                BinaryWriter w = new BinaryWriter(output);
                w.Write(0x32504C42u);
                w.Write(1u);
                w.Write((byte)2);
                w.Write((byte)8);
                w.Write((byte)1);
                w.Write((byte)1);
                w.Write((uint)image.Width);
                w.Write((uint)image.Height);
                uint offset = 148 + 1024;
                for (int i = 0; i < 16; i++)
                {
                    w.Write(i < mips.Count ? offset : 0u);
                    if (i < mips.Count)
                        offset += (uint)mips[i].Length;
                }
                for (int i = 0; i < 16; i++)
                    w.Write(i < mips.Count ? (uint)mips[i].Length : 0u);
                w.Write(new byte[1024]);
                foreach (byte[] mip in mips)
                    w.Write(mip);
                return output.ToArray();
            }
        }

        static byte[] EncodeDxt3(Image image)
        {
            int blocksX = (image.Width + 3) / 4, blocksY = (image.Height + 3) / 4;
            byte[] data = new byte[blocksX * blocksY * 16];
            int pos = 0;
            for (int by = 0; by < blocksY; by++)
                for (int bx = 0; bx < blocksX; bx++)
                {
                    float[] r = new float[16], g = new float[16], b = new float[16], a = new float[16];
                    for (int i = 0; i < 16; i++)
                    {
                        int x = Math.Min(image.Width - 1, bx * 4 + i % 4), y = Math.Min(image.Height - 1, by * 4 + i / 4);
                        int s = y * image.Width + x;
                        r[i] = image.R[s];
                        g[i] = image.G[s];
                        b[i] = image.B[s];
                        a[i] = image.A[s];
                    }

                    for (int i = 0; i < 16; i += 2)
                    {
                        int lo = Clamp((int)Math.Round(a[i] * 15), 0, 15), hi = Clamp((int)Math.Round(a[i + 1] * 15), 0, 15);
                        data[pos++] = (byte)(lo | (hi << 4));
                    }

                    int bright = -1, dark = -1;
                    float brightL = -1, darkL = 2;
                    for (int i = 0; i < 16; i++)
                    {
                        if (a[i] <= 0.02f)
                            continue;
                        float l = r[i] * 0.3f + g[i] * 0.59f + b[i] * 0.11f;
                        if (l > brightL) { brightL = l; bright = i; }
                        if (l < darkL) { darkL = l; dark = i; }
                    }
                    if (bright < 0)
                        bright = dark = 0;
                    ushort c0 = To565(r[bright], g[bright], b[bright]), c1 = To565(r[dark], g[dark], b[dark]);
                    if (c0 < c1)
                    {
                        ushort t = c0; c0 = c1; c1 = t;
                    }
                    float[][] palette = { From565(c0), From565(c1), null, null };
                    palette[2] = Mix(palette[0], palette[1], 2f / 3);
                    palette[3] = Mix(palette[0], palette[1], 1f / 3);
                    uint indices = 0;
                    for (int i = 0; i < 16; i++)
                    {
                        int best = 0;
                        float bestD = float.MaxValue;
                        for (int p = 0; p < 4; p++)
                        {
                            float dr = palette[p][0] - r[i], dg = palette[p][1] - g[i], db = palette[p][2] - b[i];
                            float d = dr * dr + dg * dg + db * db;
                            if (d < bestD) { bestD = d; best = p; }
                        }
                        indices |= (uint)best << (i * 2);
                    }
                    data[pos++] = (byte)c0;
                    data[pos++] = (byte)(c0 >> 8);
                    data[pos++] = (byte)c1;
                    data[pos++] = (byte)(c1 >> 8);
                    data[pos++] = (byte)indices;
                    data[pos++] = (byte)(indices >> 8);
                    data[pos++] = (byte)(indices >> 16);
                    data[pos++] = (byte)(indices >> 24);
                }
            return data;
        }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }

        static ushort To565(float r, float g, float b)
        {
            int r5 = Clamp((int)Math.Round(r * 31), 0, 31), g6 = Clamp((int)Math.Round(g * 63), 0, 63), b5 = Clamp((int)Math.Round(b * 31), 0, 31);
            return (ushort)((r5 << 11) | (g6 << 5) | b5);
        }

        static float[] From565(ushort c)
        {
            return new[] { ((c >> 11) & 31) / 31f, ((c >> 5) & 63) / 63f, (c & 31) / 31f };
        }

        static float[] Mix(float[] x, float[] y, float t)
        {
            return new[] { x[0] * t + y[0] * (1 - t), x[1] * t + y[1] * (1 - t), x[2] * t + y[2] * (1 - t) };
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class AddonTextures
    {
        public static List<string> Write(ClientData client, string root)
        {
            string folder = Path.Combine(root, "Interface", "AddOns", "HordesOfAzeroth", "Textures");
            Directory.CreateDirectory(folder);
            Dictionary<string, Image> textures = new Dictionary<string, Image>
            {
                { "ClassCrests.tga", ClassCrests.Build(client) },
                { "PlayerFrameNoPower.tga", PlayerFrameNoPower.Build(client) },
            };
            List<string> written = new List<string>();
            foreach (KeyValuePair<string, Image> texture in textures)
            {
                string target = Path.Combine(folder, texture.Key);
                File.WriteAllBytes(target, Tga.Write(texture.Value));
                written.Add(target);
            }
            return written;
        }

        public static Image ReadBlp(ClientData client, string path)
        {
            byte[] blp = client.Read(path);
            if (blp == null)
                throw new FileNotFoundException(path + " is not in the client's archives.");
            return BlpReader.Decode(blp);
        }
    }

    static class PlayerFrameNoPower
    {
        const string Source = "Interface\\TargetingFrame\\UI-TargetingFrame-NoMana.blp";
        const int ShiftLeft = 37;
        const int ShiftDown = 8;

        public static Image Build(ClientData client)
        {
            Image source = AddonTextures.ReadBlp(client, Source);
            Image frame = new Image(source.Width, source.Height);
            for (int y = ShiftDown; y < frame.Height; y++)
                for (int x = 0; x + ShiftLeft < source.Width; x++)
                {
                    int i = (y - ShiftDown) * source.Width + x + ShiftLeft;
                    frame.Set(x, y, source.R[i], source.G[i], source.B[i], source.A[i]);
                }
            return frame;
        }
    }

    static class ClassCrests
    {
        const int Cell = 128;
        const int Columns = 4;
        const int AtlasSize = Cell * Columns;
        const float EdgeRadius = 0.985f;
        const float EdgeSoftness = 0.03f;

        sealed class Crest
        {
            public string Icon;
            public bool Mirror;
            public float[] Recolour;
            public float Window;
            public float OffsetX, OffsetY;
        }

        static readonly float[] PaladinGold =
        {
            -0.10f, 0.32f, 0.74f, 0.07f,
            0.22f, 0.36f, 0.46f, -0.01f,
            1.21f, -0.02f, -0.10f, -0.13f,
        };

        static readonly Crest[] Crests =
        {
            new Crest { Icon = "INV_Sword_27", Window = 56, OffsetY = -1 },
            new Crest { Icon = "INV_Staff_13", Window = 55 },
            new Crest { Icon = "INV_ThrowingKnife_04", Mirror = true, Window = 54 },
            new Crest { Icon = "INV_Misc_MonsterClaw_04", Window = 53 },
            new Crest { Icon = "INV_Weapon_Bow_07", Window = 54, OffsetY = -2 },
            new Crest { Icon = "Spell_Nature_BloodLust", Window = 49 },
            new Crest { Icon = "INV_Staff_30", Mirror = true, Window = 54 },
            new Crest { Icon = "Spell_Nature_Drowsy", Window = 51, OffsetX = 1, OffsetY = -1 },
            new Crest { Icon = "Ability_ThunderBolt", Mirror = true, Recolour = PaladinGold, Window = 49, OffsetX = 0.5f, OffsetY = -0.5f },
        };

        public static Image Build(ClientData client)
        {
            Image atlas = new Image(AtlasSize, AtlasSize);
            for (int i = 0; i < Crests.Length; i++)
            {
                Image icon = AddonTextures.ReadBlp(client, "Interface\\Icons\\" + Crests[i].Icon + ".blp");
                Paint(atlas, icon, Crests[i], i % Columns * Cell, i / Columns * Cell);
            }
            return atlas;
        }

        static void Paint(Image atlas, Image icon, Crest crest, int left, int top)
        {
            float scale = crest.Window / Cell;
            float originX = icon.Width / 2f - crest.Window / 2 + crest.OffsetX;
            float originY = icon.Height / 2f - crest.Window / 2 + crest.OffsetY;
            float centre = (Cell - 1) / 2f, radius = Cell / 2f;
            for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                {
                    float sx = originX + (x + 0.5f) * scale - 0.5f;
                    float sy = originY + (y + 0.5f) * scale - 0.5f;
                    if (crest.Mirror)
                        sx = icon.Width - 1 - sx;
                    float[] c = Bicubic.Sample(icon, sx, sy);
                    if (crest.Recolour != null)
                        c = Recoloured(c, crest.Recolour);
                    float distance = (float)Math.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / radius;
                    float alpha = Math.Max(0, Math.Min(1, (EdgeRadius - distance) / EdgeSoftness + 0.5f));
                    atlas.Set(left + x, top + y, c[0], c[1], c[2], alpha);
                }
        }

        static float[] Recoloured(float[] c, float[] matrix)
        {
            float[] result = new float[3];
            for (int row = 0; row < 3; row++)
            {
                float v = matrix[row * 4] * c[0] + matrix[row * 4 + 1] * c[1] + matrix[row * 4 + 2] * c[2] + matrix[row * 4 + 3];
                result[row] = Math.Max(0, Math.Min(1, v));
            }
            return result;
        }
    }

    static class Bicubic
    {
        public static float[] Sample(Image image, float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            float[] result = new float[3];
            for (int m = -1; m <= 2; m++)
            {
                float wy = Weight(m - fy);
                int py = Math.Max(0, Math.Min(image.Height - 1, iy + m));
                for (int n = -1; n <= 2; n++)
                {
                    float w = wy * Weight(n - fx);
                    int px = Math.Max(0, Math.Min(image.Width - 1, ix + n));
                    int i = py * image.Width + px;
                    result[0] += image.R[i] * w;
                    result[1] += image.G[i] * w;
                    result[2] += image.B[i] * w;
                }
            }
            for (int k = 0; k < 3; k++)
                result[k] = Math.Max(0, Math.Min(1, result[k]));
            return result;
        }

        static float Weight(float t)
        {
            const float a = -0.5f;
            t = Math.Abs(t);
            if (t <= 1)
                return (a + 2) * t * t * t - (a + 3) * t * t + 1;
            if (t < 2)
                return a * t * t * t - 5 * a * t * t + 8 * a * t - 4 * a;
            return 0;
        }
    }

    static class BlpReader
    {
        const int DirectCompression = 2;

        public static Image Decode(byte[] blp)
        {
            if (blp.Length < 148 || blp[0] != 'B' || blp[1] != 'L' || blp[2] != 'P' || blp[3] != '2')
                throw new InvalidDataException("Not a BLP2 texture.");
            byte compression = blp[8], alphaDepth = blp[9], alphaType = blp[10];
            int width = BitConverter.ToInt32(blp, 12), height = BitConverter.ToInt32(blp, 16);
            int offset = BitConverter.ToInt32(blp, 20);
            if (compression != DirectCompression)
                throw new InvalidDataException("Only DXT compressed BLP2 textures are supported.");
            bool dxt1 = alphaDepth <= 1;
            bool dxt5 = !dxt1 && alphaType == 7;
            Image image = new Image(width, height);
            int blockBytes = dxt1 ? 8 : 16;
            int blocksX = (width + 3) / 4;
            for (int by = 0; by < (height + 3) / 4; by++)
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int at = offset + (by * blocksX + bx) * blockBytes;
                    float[] alphas = dxt1 ? null : dxt5 ? Dxt5Alpha(blp, at) : Dxt3Alpha(blp, at);
                    int colourAt = dxt1 ? at : at + 8;
                    float[][] palette = ColourPalette(blp, colourAt, dxt1);
                    uint indices = BitConverter.ToUInt32(blp, colourAt + 4);
                    for (int i = 0; i < 16; i++)
                    {
                        int x = bx * 4 + i % 4, y = by * 4 + i / 4;
                        if (x >= width || y >= height)
                            continue;
                        float[] c = palette[(indices >> (i * 2)) & 3];
                        image.Set(x, y, c[0], c[1], c[2], alphas == null ? c[3] : alphas[i]);
                    }
                }
            return image;
        }

        static float[][] ColourPalette(byte[] data, int at, bool dxt1)
        {
            ushort c0 = BitConverter.ToUInt16(data, at), c1 = BitConverter.ToUInt16(data, at + 2);
            int[] a = Expand565(c0), b = Expand565(c1);
            if (!dxt1 || c0 > c1)
                return new[] { Colour(a, b, 1, 0, 1), Colour(a, b, 0, 1, 1), Colour(a, b, 2, 1, 3), Colour(a, b, 1, 2, 3) };
            return new[] { Colour(a, b, 1, 0, 1), Colour(a, b, 0, 1, 1), Colour(a, b, 1, 1, 2), new float[] { 0, 0, 0, 0 } };
        }

        static int[] Expand565(ushort c)
        {
            int r = (c >> 11) & 31, g = (c >> 5) & 63, b = c & 31;
            return new[] { r << 3, g << 2, b << 3 };
        }

        static float[] Colour(int[] a, int[] b, int weightA, int weightB, int total)
        {
            float[] c = new float[4];
            for (int k = 0; k < 3; k++)
                c[k] = (a[k] * weightA + b[k] * weightB) / total / 255f;
            c[3] = 1;
            return c;
        }

        static float[] Dxt3Alpha(byte[] data, int at)
        {
            float[] alphas = new float[16];
            for (int i = 0; i < 16; i++)
                alphas[i] = ((data[at + i / 2] >> (i % 2 * 4)) & 15) / 15f;
            return alphas;
        }

        static float[] Dxt5Alpha(byte[] data, int at)
        {
            int a0 = data[at], a1 = data[at + 1];
            int[] levels = new int[8];
            levels[0] = a0;
            levels[1] = a1;
            if (a0 > a1)
                for (int k = 2; k < 8; k++)
                    levels[k] = (a0 * (8 - k) + a1 * (k - 1)) / 7;
            else
            {
                for (int k = 2; k < 6; k++)
                    levels[k] = (a0 * (6 - k) + a1 * (k - 1)) / 5;
                levels[6] = 0;
                levels[7] = 255;
            }
            ulong bits = 0;
            for (int k = 0; k < 6; k++)
                bits |= (ulong)data[at + 2 + k] << (8 * k);
            float[] alphas = new float[16];
            for (int i = 0; i < 16; i++)
                alphas[i] = levels[(bits >> (3 * i)) & 7] / 255f;
            return alphas;
        }
    }

    static class Tga
    {
        const byte TrueColour = 2;
        const byte AlphaBits = 8;

        public static byte[] Write(Image image)
        {
            byte[] data = new byte[18 + image.Width * image.Height * 4];
            data[2] = TrueColour;
            BitConverter.GetBytes((ushort)image.Width).CopyTo(data, 12);
            BitConverter.GetBytes((ushort)image.Height).CopyTo(data, 14);
            data[16] = 32;
            data[17] = AlphaBits;
            int at = 18;
            for (int y = image.Height - 1; y >= 0; y--)
                for (int x = 0; x < image.Width; x++)
                {
                    int i = y * image.Width + x;
                    data[at++] = ToByte(image.B[i]);
                    data[at++] = ToByte(image.G[i]);
                    data[at++] = ToByte(image.R[i]);
                    data[at++] = ToByte(image.A[i]);
                }
            return data;
        }

        static byte ToByte(float v)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v * 255)));
        }
    }
}

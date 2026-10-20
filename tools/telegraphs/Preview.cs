using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace BonkTelegraphs
{
    static class Preview
    {
        public static void Save(byte[] blp, string path)
        {
            BinaryReader r = new BinaryReader(new MemoryStream(blp));
            r.BaseStream.Position = 12;
            int width = (int)r.ReadUInt32(), height = (int)r.ReadUInt32();
            uint offset = r.ReadUInt32();
            using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                int blocksX = (width + 3) / 4;
                for (int by = 0; by < (height + 3) / 4; by++)
                    for (int bx = 0; bx < blocksX; bx++)
                    {
                        int pos = (int)offset + (by * blocksX + bx) * 16;
                        ushort c0 = BitConverter.ToUInt16(blp, pos + 8), c1 = BitConverter.ToUInt16(blp, pos + 10);
                        uint indices = BitConverter.ToUInt32(blp, pos + 12);
                        float[][] palette = { Color565(c0), Color565(c1), null, null };
                        palette[2] = Mix(palette[0], palette[1], 2f / 3);
                        palette[3] = Mix(palette[0], palette[1], 1f / 3);
                        for (int i = 0; i < 16; i++)
                        {
                            int x = bx * 4 + i % 4, y = by * 4 + i / 4;
                            if (x >= width || y >= height)
                                continue;
                            float a = ((blp[pos + i / 2] >> (i % 2 * 4)) & 15) / 15f;
                            float[] c = palette[(indices >> (i * 2)) & 3];
                            float[] ground = { 0.30f, 0.38f, 0.18f };
                            bitmap.SetPixel(x, y, Color.FromArgb(
                                (int)(255 * (c[0] * a + ground[0] * (1 - a))),
                                (int)(255 * (c[1] * a + ground[1] * (1 - a))),
                                (int)(255 * (c[2] * a + ground[2] * (1 - a)))));
                        }
                    }
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        static float[] Color565(ushort c)
        {
            return new[] { ((c >> 11) & 31) / 31f, ((c >> 5) & 63) / 63f, (c & 31) / 31f };
        }

        static float[] Mix(float[] x, float[] y, float t)
        {
            return new[] { x[0] * t + y[0] * (1 - t), x[1] * t + y[1] * (1 - t), x[2] * t + y[2] * (1 - t) };
        }
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BonkTelegraphs
{
    static class Png
    {
        static readonly uint[] CrcTable = BuildCrcTable();

        public static void WriteRgb(string path, int width, int height, byte[] rgb)
        {
            using (FileStream file = File.Create(path))
            {
                file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                byte[] header = new byte[13];
                PutBigEndian(header, 0, (uint)width);
                PutBigEndian(header, 4, (uint)height);
                header[8] = 8;
                header[9] = 2;
                WriteChunk(file, "IHDR", header);
                WriteChunk(file, "IDAT", Zlib(Scanlines(width, height, rgb)));
                WriteChunk(file, "IEND", new byte[0]);
            }
        }

        static byte[] Scanlines(int width, int height, byte[] rgb)
        {
            int stride = width * 3;
            byte[] raw = new byte[(stride + 1) * height];
            for (int y = 0; y < height; y++)
                Buffer.BlockCopy(rgb, y * stride, raw, y * (stride + 1) + 1, stride);
            return raw;
        }

        static byte[] Zlib(byte[] data)
        {
            using (MemoryStream output = new MemoryStream())
            {
                output.WriteByte(0x78);
                output.WriteByte(0x01);
                using (DeflateStream deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                    deflate.Write(data, 0, data.Length);
                byte[] checksum = new byte[4];
                PutBigEndian(checksum, 0, Adler32(data));
                output.Write(checksum, 0, 4);
                return output.ToArray();
            }
        }

        static void WriteChunk(Stream stream, string type, byte[] data)
        {
            byte[] length = new byte[4];
            PutBigEndian(length, 0, (uint)data.Length);
            stream.Write(length, 0, 4);
            byte[] typed = new byte[4 + data.Length];
            Encoding.ASCII.GetBytes(type, 0, 4, typed, 0);
            Buffer.BlockCopy(data, 0, typed, 4, data.Length);
            stream.Write(typed, 0, typed.Length);
            byte[] crc = new byte[4];
            PutBigEndian(crc, 0, Crc32(typed));
            stream.Write(crc, 0, 4);
        }

        static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte value in data)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }

        static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte value in data)
                crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }

        static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }

        static void PutBigEndian(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
}

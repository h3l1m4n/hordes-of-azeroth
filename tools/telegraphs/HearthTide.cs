using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class HearthTide
    {
        public const uint DisplayId = 18585;
        const uint ModelId = 3585;
        const string Source = "Spells\\ManaTideInfuse_Base.m2";
        const string Folder = "World\\Generic\\BonkTelegraph\\";
        const string Name = "HearthTide";
        const float Size = 0.45f;
        const float Opacity = 0.5f;
        const uint TurnMs = 3200;

        const int AnimationsCountOffset = 28, AnimationsOffset = 32, AnimationStart = 4, AnimationEnd = 8;
        const int BonesCountOffset = 52, BonesOffset = 56, BoneSize = 0x6C;
        const int BoneTranslation = 0x0C, BoneRotation = 0x28, BoneScale = 0x44;
        const int TransparencyCountOffset = 100, TransparencyOffset = 104, TrackSize = 0x1C;
        const int TrackTimesCount = 0x0C, TrackTimesOffset = 0x10, TrackValuesCount = 0x14, TrackValuesOffset = 0x18;

        public static string Add(ClientData client, IDictionary<string, byte[]> files, Dbc displays, Dbc models)
        {
            byte[] source = client.Read(Source);
            if (source == null)
                throw new FileNotFoundException(Source + " is not in the client's archives.");
            byte[] m2 = (byte[])source.Clone();
            if (U32(m2, AnimationsCountOffset) != 1 || U32(m2, BonesCountOffset) != 1)
                throw new InvalidDataException(Source + " is not the one-bone, one-sequence swirl 1.12 has.");

            int animation = (int)U32(m2, AnimationsOffset);
            uint start = U32(m2, animation + AnimationStart), end = U32(m2, animation + AnimationEnd);
            int bone = (int)U32(m2, BonesOffset);
            foreach (int track in new[] { BoneTranslation, BoneRotation, BoneScale })
                Retime(m2, bone + track, start, end);
            SetFloats(m2, bone + BoneScale, Size);

            uint transparencies = U32(m2, TransparencyCountOffset), transparency = U32(m2, TransparencyOffset);
            for (uint i = 0; i < transparencies; i++)
            {
                int track = (int)(transparency + i * TrackSize);
                Retime(m2, track, start, end);
                SetShorts(m2, track, (short)(Opacity * short.MaxValue));
            }
            Write(m2, animation + AnimationEnd, start + TurnMs);

            files[Folder + Name + ".m2"] = m2;
            StandIns.AddSizedDisplay(displays, models, DisplayId, ModelId, Name);
            return string.Format("creature display {0}: the Hearthstone ring's swirl ({1}, size x{2}, {3}% opacity, a turn every {4} ms)",
                DisplayId, Source, Size, (int)(Opacity * 100), TurnMs);
        }

        static void Retime(byte[] m2, int track, uint start, uint end)
        {
            uint count = U32(m2, track + TrackTimesCount), offset = U32(m2, track + TrackTimesOffset);
            for (uint i = 0; i < count; i++)
            {
                int at = (int)(offset + i * 4);
                uint time = U32(m2, at);
                if (time >= start)
                    Write(m2, at, start + (uint)((ulong)(time - start) * TurnMs / (end - start)));
            }
        }

        static void SetFloats(byte[] m2, int track, float value)
        {
            uint count = U32(m2, track + TrackValuesCount) * 3, offset = U32(m2, track + TrackValuesOffset);
            for (uint i = 0; i < count; i++)
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, m2, (int)(offset + i * 4), 4);
        }

        static void SetShorts(byte[] m2, int track, short value)
        {
            uint count = U32(m2, track + TrackValuesCount), offset = U32(m2, track + TrackValuesOffset);
            for (uint i = 0; i < count; i++)
                Buffer.BlockCopy(BitConverter.GetBytes(value), 0, m2, (int)(offset + i * 2), 2);
        }

        static void Write(byte[] data, int at, uint value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, at, 4);
        }

        static uint U32(byte[] data, int at)
        {
            return BitConverter.ToUInt32(data, at);
        }
    }
}

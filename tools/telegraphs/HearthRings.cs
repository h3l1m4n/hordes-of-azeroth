using System.Collections.Generic;

namespace BonkTelegraphs
{
    static class HearthRings
    {
        public const uint FirstDisplayId = 18400;
        const uint FirstModelId = 3400;
        const float Radius = 5f;
        const uint ShortestMs = 8000;
        const uint LongestMs = 15000;
        const uint DurationStepMs = 1000;
        const uint ResumeStepMs = 500;
        const string Folder = "World\\Generic\\BonkTelegraph\\";
        const string OutlineName = "HearthOutline";
        static readonly float[] Bright = { 0.62f, 0.88f, 1f };
        static readonly float[] Deep = { 0.1f, 0.36f, 0.95f };

        sealed class Fill
        {
            public string Name;
            public uint DurationMs;
            public uint DoneMs;
        }

        static List<Fill> Fills()
        {
            List<Fill> fills = new List<Fill>();
            for (uint duration = ShortestMs; duration <= LongestMs; duration += DurationStepMs)
                for (uint done = 0; done < duration; done += ResumeStepMs)
                    fills.Add(new Fill { Name = "HearthFill" + duration + "_" + done, DurationMs = duration, DoneMs = done });
            return fills;
        }

        public static string AddDisplays(Dbc displays, Dbc models)
        {
            StandIns.AddSizedDisplay(displays, models, FirstDisplayId, FirstModelId, OutlineName);
            List<Fill> fills = Fills();
            for (int i = 0; i < fills.Count; i++)
                StandIns.AddSizedDisplay(displays, models, FirstDisplayId + 1 + (uint)i, FirstModelId + 1 + (uint)i, fills[i].Name);
            return string.Format("creature displays {0}-{1}: Hearthstone ring, outline then {2} fills ({3}-{4} s, resumable every {5} ms)",
                FirstDisplayId, FirstDisplayId + fills.Count, fills.Count, ShortestMs / 1000, LongestMs / 1000, ResumeStepMs);
        }

        public static void AddModels(Dictionary<string, byte[]> files, byte[] playableLookup)
        {
            string outlineTexture = Folder + OutlineName + ".blp";
            string fillTexture = Folder + "HearthFill.blp";
            files[outlineTexture] = Blp.WriteDxt3(Kit.PaintCircle(false, Bright, Deep), true);
            files[fillTexture] = Blp.WriteDxt3(Kit.PaintCircle(true, Bright, Deep), true);

            files[Folder + OutlineName + ".m2"] = M2.Write("BonkTelegraph" + OutlineName, Disc(outlineTexture), playableLookup);
            foreach (Fill fill in Fills())
            {
                Quad quad = Disc(fillTexture);
                quad.GrowMs = fill.DurationMs - fill.DoneMs;
                quad.GrowFrom = (float)fill.DoneMs / fill.DurationMs;
                files[Folder + fill.Name + ".m2"] = M2.Write("BonkTelegraph" + fill.Name, quad, playableLookup);
            }
        }

        static Quad Disc(string texture)
        {
            return new Quad { MinX = -Radius, MaxX = Radius, MinY = -Radius, MaxY = Radius, Texture = texture, CastsNoShadow = true };
        }
    }
}

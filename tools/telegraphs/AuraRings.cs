using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BonkTelegraphs
{
    sealed class AuraRingLook
    {
        public string Name;
        public uint TemplateSpell;
        public uint FirstSpellId;
        public float[] Dark;
        public float[] Bright;
        public float HazeAlpha;
        public float StreakAlpha;
        public float RimAlpha;
        public float MaxAlpha;
    }

    static class AuraRings
    {
        const uint FirstEffectId = 3100;
        const uint FirstKitId = 7300;
        const uint FirstVisualId = 8300;
        const uint StateKitTemplate = 301;
        const uint StateVisualTemplate = 565;
        const string Folder = "World\\Generic\\BonkTelegraph\\";

        const int SpellVisualField = 115;
        const int VisualPrecastKitField = 1, VisualCastKitField = 2, VisualImpactKitField = 3, VisualStateKitField = 4, VisualChannelKitField = 5;
        const int KitBaseEffectField = 5;
        const int EffectNameField = 1, EffectPathField = 2, EffectScaleField = 4;

        const int TextureSize = 512;
        const float QuadHalf = 1.04f;
        const float Lift = 0.08f;
        const float HazeStart = 0.35f;
        const float HazeCurve = 2.2f;
        const float EdgeMiddle = 0.975f;
        const float EdgeWobble = 0.05f;
        const float RimWidth = 0.025f;
        const float FadeWidth = 0.06f;
        const float StreakThreshold = 0.45f;
        const float StreakContrast = 4f;
        const float StreakLight = 0.3f;
        const float SpiralTwist = 3f;
        const uint SwirlMs = 36000;

        public static readonly AuraRingLook[] Looks =
        {
            new AuraRingLook { Name = "BloodAuraRing", TemplateSpell = 33401, FirstSpellId = 33402,
                Dark = new[] { 0.25f, 0f, 0.02f }, Bright = new[] { 0.58f, 0.05f, 0.06f },
                HazeAlpha = 0.3f, StreakAlpha = 0.42f, RimAlpha = 0.4f, MaxAlpha = 0.8f },
            new AuraRingLook { Name = "IcePathRing", TemplateSpell = 33424, FirstSpellId = 33425,
                Dark = new[] { 0.5f, 0.75f, 0.95f }, Bright = new[] { 0.88f, 0.97f, 1f },
                HazeAlpha = 0.3f, StreakAlpha = 0.42f, RimAlpha = 0.45f, MaxAlpha = 0.8f },
        };

        public static List<string> AddSpells(Dbc spells, Dbc visuals, Dbc kits, Dbc effects)
        {
            uint[] kitTemplate = kits.Find(StateKitTemplate), visualTemplate = visuals.Find(StateVisualTemplate);
            if (kitTemplate == null || visualTemplate == null)
                throw new InvalidDataException("The aura rings' template kit and visual are not in the client's tables.");

            List<string> manifest = new List<string>();
            uint slot = 0;
            foreach (AuraRingLook look in Looks)
            {
                uint[] template = spells.Find(look.TemplateSpell);
                if (template == null)
                    throw new InvalidDataException("Spell " + look.TemplateSpell + " is not in Spell.dbc.");

                uint firstSlot = slot;
                for (int i = 0; i < StandIns.Sizes.Length; i++, slot++)
                {
                    string name = ModelName(look, StandIns.Sizes[i]);
                    uint effectId = FirstEffectId + slot, kitId = FirstKitId + slot, visualId = FirstVisualId + slot, spellId = look.FirstSpellId + (uint)i;
                    if (effects.Find(effectId) != null || kits.Find(kitId) != null || visuals.Find(visualId) != null || spells.Find(spellId) != null)
                        throw new InvalidOperationException("The ids for aura ring " + name + " are taken.");

                    uint[] effect = new uint[effects.FieldCount];
                    effect[0] = effectId;
                    effect[EffectNameField] = effects.AddString(name);
                    effect[EffectPathField] = effects.AddString(Folder + name + ".mdx");
                    effect[EffectScaleField] = BitConverter.ToUInt32(BitConverter.GetBytes(1f), 0);
                    effects.Records.Add(effect);

                    uint[] kit = (uint[])kitTemplate.Clone();
                    kit[0] = kitId;
                    kit[KitBaseEffectField] = effectId;
                    kits.Records.Add(kit);

                    uint[] visual = (uint[])visualTemplate.Clone();
                    visual[0] = visualId;
                    visual[VisualPrecastKitField] = visual[VisualCastKitField] = visual[VisualImpactKitField] = visual[VisualChannelKitField] = 0;
                    visual[VisualStateKitField] = kitId;
                    visuals.Records.Add(visual);

                    uint[] spell = (uint[])template.Clone();
                    spell[0] = spellId;
                    spell[SpellVisualField] = visualId;
                    spells.Records.Add(spell);
                }

                uint last = slot - 1;
                manifest.Add(string.Format("spells {0}-{1} ({2}, {3}-{4} yd): visuals {5}-{6}, state kits {7}-{8}, base effects {9}-{10}",
                    look.FirstSpellId, look.FirstSpellId + last - firstSlot, look.Name, StandIns.Sizes[0], StandIns.Sizes[StandIns.Sizes.Length - 1],
                    FirstVisualId + firstSlot, FirstVisualId + last, FirstKitId + firstSlot, FirstKitId + last, FirstEffectId + firstSlot, FirstEffectId + last));
            }
            return manifest;
        }

        public static void AddModels(Dictionary<string, byte[]> files, byte[] playableLookup)
        {
            foreach (AuraRingLook look in Looks)
            {
                string texture = Folder + look.Name + ".blp";
                files[texture] = Blp.WriteDxt3(Paint(look), true);
                foreach (float size in StandIns.Sizes)
                {
                    string name = ModelName(look, size);
                    float half = QuadHalf * size;
                    Quad quad = new Quad { MinX = -half, MaxX = half, MinY = -half, MaxY = half, Z = Lift, Texture = texture, FaceCamera = true, SpinMs = SwirlMs };
                    files[Folder + name + ".m2"] = M2.Write("BonkTelegraph" + name, quad, playableLookup);
                }
            }
        }

        static string ModelName(AuraRingLook look, float size)
        {
            return look.Name + size.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', '_');
        }

        static Image Paint(AuraRingLook look)
        {
            float pixel = 2 * QuadHalf / TextureSize;
            Image image = new Image(TextureSize, TextureSize);
            for (int py = 0; py < TextureSize; py++)
                for (int px = 0; px < TextureSize; px++)
                {
                    float x = -QuadHalf + (px + 0.5f) * pixel, y = -QuadHalf + (py + 0.5f) * pixel;
                    float u = (px + 0.5f) / TextureSize, v = (py + 0.5f) / TextureSize;
                    float radius = (float)Math.Sqrt(x * x + y * y);
                    float edge = EdgeMiddle + (Fbm(u, v, 5, 5, 1) - 0.5f) * EdgeWobble;
                    float haze = (float)Math.Pow(Clamp01((radius - HazeStart) / (edge - HazeStart)), HazeCurve);
                    float twist = SpiralTwist * radius;
                    float su = 0.5f + (float)((u - 0.5f) * Math.Cos(twist) - (v - 0.5f) * Math.Sin(twist));
                    float sv = 0.5f + (float)((u - 0.5f) * Math.Sin(twist) + (v - 0.5f) * Math.Cos(twist));
                    float streak = Clamp01((Fbm(su, sv, 10, 4, 2) - StreakThreshold) * StreakContrast) * haze;
                    float rimDistance = (radius - edge) / RimWidth;
                    float rim = (float)Math.Exp(-rimDistance * rimDistance);
                    float fade = Clamp01((edge + FadeWidth / 2 - radius) / FadeWidth);
                    float alpha = Math.Min(look.MaxAlpha, look.HazeAlpha * haze + look.StreakAlpha * streak + look.RimAlpha * rim) * fade;
                    float light = Clamp01(rim + StreakLight * streak);
                    image.Set(px, py, Mix(look.Dark[0], look.Bright[0], light), Mix(look.Dark[1], look.Bright[1], light), Mix(look.Dark[2], look.Bright[2], light), alpha);
                }
            return image;
        }

        static float Fbm(float u, float v, int cells, int octaves, uint seed)
        {
            float sum = 0, total = 0, amplitude = 1;
            for (int octave = 0; octave < octaves; octave++)
            {
                int scale = cells << octave;
                sum += amplitude * ValueNoise(u * scale, v * scale, seed * 16 + (uint)octave);
                total += amplitude;
                amplitude *= 0.5f;
            }
            return sum / total;
        }

        static float ValueNoise(float x, float y, uint seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = Smooth(x - ix), fy = Smooth(y - iy);
            float top = Mix(Lattice(ix, iy, seed), Lattice(ix + 1, iy, seed), fx);
            float bottom = Mix(Lattice(ix, iy + 1, seed), Lattice(ix + 1, iy + 1, seed), fx);
            return Mix(top, bottom, fy);
        }

        static float Lattice(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / 4294967295f;
            }
        }

        static float Smooth(float t) { return t * t * (3 - 2 * t); }

        static float Mix(float a, float b, float t) { return a + (b - a) * t; }

        static float Clamp01(float v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
    }
}

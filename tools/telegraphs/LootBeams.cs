using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BonkTelegraphs
{
    static class LootBeams
    {
        const uint SpotlightSpell = 25824;
        const uint SpotlightVisual = 7402;
        const uint SpotlightKit = 6448;
        const uint MoonBeamEffect = 2989;
        const uint FirstSpellId = 33456;
        const uint FirstVisualId = 8460;
        const uint FirstKitId = 7460;
        const uint FirstEffectId = 3200;

        const int SpellVisualField = 115, NameField = 120;
        const int VisualStateKitField = 4;
        const int KitSpecialEffectField = 12;
        const int EffectModelField = 2;

        const string Folder = "Spells\\";
        const string BeamModel = Folder + "MoonBeam_Impact_Base.m2";
        const string BeamTexture = Folder + "MoonBeam.blp";
        const string CircleTexture = Folder + "White-Circle.blp";

        const int TexturesCountOffset = 92, TexturesOffset = 96;
        const int ParticlesCountOffset = 316, ParticlesOffset = 320;
        const int ParticleSize = 504, ParticleColoursOffset = 336;
        const float DefaultWhiteCore = 0.25f;
        const int VerticesCountOffset = 68, VerticesOffset = 72, VertexSize = 48, VertexHeightOffset = 8;
        static readonly int[] BoxSideOffsets = { 180, 184, 192, 196, 208, 212, 220, 224 };
        const int VertexBoxTopOffset = 200, BoundingBoxTopOffset = 228;
        const float BeamHeight = 2.6f;
        const float BeamWidth = 0.28f;

        public class Beam
        {
            public string Name;
            public float R, G, B;
            public float WhiteCore = DefaultWhiteCore;
            public float Gain = 1.0f;
            public float Height = BeamHeight;
            public float Width = BeamWidth;
        }

        static readonly Beam[] Beams =
        {
            new Beam { Name = "Uncommon",  R = 0.12f, G = 1.0f,  B = 0.0f },
            new Beam { Name = "Rare",      R = 0.0f,  G = 0.35f, B = 1.0f, WhiteCore = 0.0f, Gain = 1.6f },
            new Beam { Name = "Epic",      R = 0.55f, G = 0.1f, B = 1.0f, WhiteCore = 0.0f, Gain = 1.5f },
            new Beam { Name = "Legendary", R = 1.0f,  G = 0.5f,  B = 0.0f },
        };

        public static List<string> AddLootBeams(ClientData client, IDictionary<string, byte[]> files, Dbc spells, Dbc visuals, Dbc kits, Dbc effects)
        {
            return AddBeams(client, files, spells, visuals, kits, effects, Beams, FirstSpellId, FirstVisualId, FirstKitId, FirstEffectId);
        }

        public static List<string> AddBeams(ClientData client, IDictionary<string, byte[]> files, Dbc spells, Dbc visuals, Dbc kits, Dbc effects,
            Beam[] beams, uint firstSpellId, uint firstVisualId, uint firstKitId, uint firstEffectId)
        {
            uint[] spell = spells.Find(SpotlightSpell), visual = visuals.Find(SpotlightVisual), kit = kits.Find(SpotlightKit), effect = effects.Find(MoonBeamEffect);
            if (spell == null || visual == null || kit == null || effect == null || kit[KitSpecialEffectField] != MoonBeamEffect)
                throw new InvalidDataException("The Spotlight spell's rows are not where 1.12 has them.");

            byte[] model = Require(client, BeamModel), beamTexture = Require(client, BeamTexture), circleTexture = Require(client, CircleTexture);
            List<string> manifest = new List<string>();
            for (uint i = 0; i < beams.Length; i++)
            {
                Beam beam = beams[i];
                uint spellId = firstSpellId + i, visualId = firstVisualId + i, kitId = firstKitId + i, effectId = firstEffectId + i;
                if (spells.Find(spellId) != null || visuals.Find(visualId) != null || kits.Find(kitId) != null || effects.Find(effectId) != null)
                    throw new InvalidOperationException("Loot beam ids for " + beam.Name + " are taken.");

                ColourRule rule = Tint(beam);
                string modelName = Folder + "BonkLootBeam" + beam.Name;
                Dictionary<string, string> textures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { BeamTexture, modelName + ".blp" },
                    { CircleTexture, modelName + "Circle.blp" },
                };
                files[modelName + ".blp"] = Blp.Recolour(beamTexture, rule);
                files[modelName + "Circle.blp"] = Blp.Recolour(circleTexture, rule);
                files[modelName + ".m2"] = Recolour(model, textures, rule, beam.Width, beam.Height);

                uint[] newEffect = (uint[])effect.Clone();
                newEffect[0] = effectId;
                newEffect[EffectModelField] = effects.AddString(modelName + ".mdx");
                effects.Records.Add(newEffect);

                uint[] newKit = (uint[])kit.Clone();
                newKit[0] = kitId;
                newKit[KitSpecialEffectField] = effectId;
                kits.Records.Add(newKit);

                uint[] newVisual = (uint[])visual.Clone();
                newVisual[0] = visualId;
                newVisual[VisualStateKitField] = kitId;
                visuals.Records.Add(newVisual);

                uint[] newSpell = (uint[])spell.Clone();
                newSpell[0] = spellId;
                newSpell[SpellVisualField] = visualId;
                newSpell[NameField] = spells.AddString(beam.Name + " Beam");
                spells.Records.Add(newSpell);

                manifest.Add(string.Format("spell {0} ({1} Beam): visual {2}, state kit {3}, effect {4}, {5}.m2", spellId, beam.Name, visualId, kitId, effectId, modelName));
            }
            return manifest;
        }

        static ColourRule Tint(Beam beam)
        {
            return (ref float r, ref float g, ref float b) =>
            {
                r = Math.Min(1.0f, r * (beam.WhiteCore + (1.0f - beam.WhiteCore) * beam.R) * beam.Gain);
                g = Math.Min(1.0f, g * (beam.WhiteCore + (1.0f - beam.WhiteCore) * beam.G) * beam.Gain);
                b = Math.Min(1.0f, b * (beam.WhiteCore + (1.0f - beam.WhiteCore) * beam.B) * beam.Gain);
            };
        }

        static byte[] Require(ClientData client, string name)
        {
            byte[] data = client.Read(name);
            if (data == null)
                throw new FileNotFoundException(name + " is not in the client's archives.");
            return data;
        }

        static byte[] Recolour(byte[] source, IDictionary<string, string> textures, ColourRule rule, float width, float height)
        {
            MemoryStream ms = new MemoryStream();
            ms.Write(source, 0, source.Length);
            byte[] m2 = ms.GetBuffer();
            if (BitConverter.ToUInt32(m2, 4) != 256)
                throw new InvalidDataException(BeamModel + " is not a version 256 model.");

            uint count = BitConverter.ToUInt32(m2, TexturesCountOffset), offset = BitConverter.ToUInt32(m2, TexturesOffset);
            HashSet<string> swapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (uint i = 0; i < count; i++)
            {
                int entry = (int)(offset + i * 16);
                uint length = BitConverter.ToUInt32(m2, entry + 8);
                if (length == 0)
                    continue;
                string name = Encoding.ASCII.GetString(m2, (int)BitConverter.ToUInt32(m2, entry + 12), (int)length - 1);
                string recoloured;
                if (!textures.TryGetValue(name, out recoloured))
                    continue;
                ms.Position = ms.Length;
                int padding = (int)((16 - ms.Length % 16) % 16);
                ms.Write(new byte[padding], 0, padding);
                uint nameOfs = (uint)ms.Length;
                byte[] bytes = Encoding.ASCII.GetBytes(recoloured + "\0");
                ms.Write(bytes, 0, bytes.Length);
                m2 = ms.GetBuffer();
                WriteUInt32(m2, entry + 8, (uint)bytes.Length);
                WriteUInt32(m2, entry + 12, nameOfs);
                swapped.Add(name);
            }
            if (swapped.Count != textures.Count)
                throw new InvalidDataException(BeamModel + " does not name the textures expected.");

            Stretch(m2, width, height);

            count = BitConverter.ToUInt32(m2, ParticlesCountOffset);
            offset = BitConverter.ToUInt32(m2, ParticlesOffset);
            for (uint i = 0; i < count; i++)
            {
                int particle = (int)(offset + i * ParticleSize);
                for (int c = 0; c < 3; c++)
                {
                    int bgra = particle + ParticleColoursOffset + c * 4;
                    float b = m2[bgra] / 255f, g = m2[bgra + 1] / 255f, r = m2[bgra + 2] / 255f;
                    rule(ref r, ref g, ref b);
                    m2[bgra] = (byte)Math.Round(b * 255);
                    m2[bgra + 1] = (byte)Math.Round(g * 255);
                    m2[bgra + 2] = (byte)Math.Round(r * 255);
                }
            }
            return ms.ToArray();
        }

        static void Stretch(byte[] m2, float width, float height)
        {
            uint count = BitConverter.ToUInt32(m2, VerticesCountOffset), offset = BitConverter.ToUInt32(m2, VerticesOffset);
            for (uint i = 0; i < count; i++)
            {
                int vertex = (int)(offset + i * VertexSize);
                ScaleFloat(m2, vertex, width);
                ScaleFloat(m2, vertex + 4, width);
                ScaleFloat(m2, vertex + VertexHeightOffset, height);
            }
            foreach (int side in BoxSideOffsets)
                ScaleFloat(m2, side, width);
            ScaleFloat(m2, VertexBoxTopOffset, height);
            ScaleFloat(m2, BoundingBoxTopOffset, height);
        }

        static void ScaleFloat(byte[] data, int at, float factor)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(BitConverter.ToSingle(data, at) * factor), 0, data, at, 4);
        }

        static void WriteUInt32(byte[] data, int at, uint value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, at, 4);
        }
    }
}

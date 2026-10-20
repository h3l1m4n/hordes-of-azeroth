using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class PenanceVisuals
    {
        const uint PenanceBoltSpell = 591;
        const uint RaptureBoltSpell = 598;
        const uint PenanceVisualId = 8500;
        const uint RaptureVisualId = 8501;
        const uint HolyMissileHighEffectId = 3160;
        const uint HolyWrathVisualId = 126;
        const float BoltSpeed = 30.0f;

        const int SpellSpeedField = 37, SpellVisualField = 115;
        const int VisualPrecastKitField = 1, VisualCastKitField = 2, VisualHasMissileField = 6, VisualMissileModelField = 7;
        const int EffectNameField = 1, EffectPathField = 2;

        public static List<string> AddPenanceVisuals(Dbc spells, Dbc visuals, Dbc effects)
        {
            uint[] holyWrath = visuals.Find(HolyWrathVisualId);
            if (holyWrath == null || holyWrath[VisualHasMissileField] == 0)
                throw new InvalidDataException("Spell visual " + HolyWrathVisualId + " (Holy Wrath) has no missile.");

            uint medEffect = holyWrath[VisualMissileModelField];
            AddEffect(effects, medEffect, HolyMissileHighEffectId, "Holy Missile High", "Spells\\Holy_Missile_High.mdx");
            AddVisual(visuals, holyWrath, PenanceVisualId, medEffect);
            AddVisual(visuals, holyWrath, RaptureVisualId, HolyMissileHighEffectId);
            SetBolt(spells, PenanceBoltSpell, PenanceVisualId);
            SetBolt(spells, RaptureBoltSpell, RaptureVisualId);

            return new List<string>
            {
                string.Format("spell {0} (Penance bolt): visual {1}, Holy Missile Med at {2} yd/s, Holy Wrath impact", PenanceBoltSpell, PenanceVisualId, BoltSpeed),
                string.Format("spell {0} (Rapture bolt): visual {1}, Holy Missile High (effect {2}) at {3} yd/s", RaptureBoltSpell, RaptureVisualId,
                    HolyMissileHighEffectId, BoltSpeed),
            };
        }

        static void AddEffect(Dbc effects, uint templateId, uint id, string name, string path)
        {
            uint[] template = effects.Find(templateId);
            if (template == null)
                throw new InvalidDataException("Spell visual effect " + templateId + " is not in the client's tables.");
            if (effects.Find(id) != null)
                throw new InvalidOperationException("Spell visual effect " + id + " is taken.");
            uint[] effect = (uint[])template.Clone();
            effect[0] = id;
            effect[EffectNameField] = effects.AddString(name);
            effect[EffectPathField] = effects.AddString(path);
            effects.Records.Add(effect);
        }

        static void AddVisual(Dbc visuals, uint[] template, uint id, uint missileEffect)
        {
            if (visuals.Find(id) != null)
                throw new InvalidOperationException("Spell visual " + id + " is taken.");
            uint[] visual = (uint[])template.Clone();
            visual[0] = id;
            visual[VisualPrecastKitField] = 0;
            visual[VisualCastKitField] = 0;
            visual[VisualMissileModelField] = missileEffect;
            visuals.Records.Add(visual);
        }

        static void SetBolt(Dbc spells, uint spellId, uint visualId)
        {
            uint[] spell = spells.Find(spellId);
            if (spell == null)
                throw new InvalidDataException("Spell " + spellId + " is not in Spell.dbc.");
            spell[SpellVisualField] = visualId;
            spell[SpellSpeedField] = BitConverter.ToUInt32(BitConverter.GetBytes(BoltSpeed), 0);
        }
    }
}

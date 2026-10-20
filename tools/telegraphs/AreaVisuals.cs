using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class AreaVisuals
    {
        const uint FirstVisualId = 8100;
        const uint FirstKitId = 7100;
        const int SpellVisualField = 115;
        const int VisualAreaKitField = 13;
        const int VisualHasMissileField = 6;
        const int VisualMissileModelField = 7;
        const int KitProcTypeField = 15;
        const int KitProcParamOneField = 23;
        const uint ProcRain = 9;
        const float Rate = 11.0f;

        static readonly uint[] Spells =
        {
            22908,
        };

        public static List<string> AddAreaVisuals(Dbc spells, Dbc visuals, Dbc kits)
        {
            uint nextKit = FirstKitId, nextVisual = FirstVisualId;
            List<string> manifest = new List<string>();
            foreach (uint spellId in Spells)
            {
                uint[] spell = spells.Find(spellId);
                if (spell == null)
                    throw new InvalidDataException("Spell " + spellId + " is not in Spell.dbc.");
                uint visualId = spell[SpellVisualField];
                uint[] visual = visuals.Find(visualId);
                uint kitId = visual != null ? visual[VisualAreaKitField] : 0;
                uint[] kit = kitId != 0 ? kits.Find(kitId) : null;
                int proc = -1;
                for (int i = 0; kit != null && i < 4; i++)
                    if (kit[KitProcTypeField + i] == ProcRain)
                    {
                        proc = i;
                        break;
                    }
                if (proc < 0)
                    throw new InvalidDataException("Spell " + spellId + "'s visual " + visualId + " rains nothing over its area.");

                uint kitCopy = nextKit++;
                if (kits.Find(kitCopy) != null)
                    throw new InvalidOperationException("Spell visual kit " + kitCopy + " is taken.");
                uint[] newKit = (uint[])kit.Clone();
                newKit[0] = kitCopy;
                float oldRate = BitConverter.ToSingle(BitConverter.GetBytes(kit[KitProcParamOneField + proc]), 0);
                newKit[KitProcParamOneField + proc] = BitConverter.ToUInt32(BitConverter.GetBytes(Rate), 0);
                kits.Records.Add(newKit);

                uint copy = nextVisual++;
                if (visuals.Find(copy) != null)
                    throw new InvalidOperationException("Spell visual " + copy + " is taken.");
                uint[] newVisual = (uint[])visual.Clone();
                newVisual[0] = copy;
                newVisual[VisualAreaKitField] = kitCopy;
                newVisual[VisualHasMissileField] = 0;
                newVisual[VisualMissileModelField] = 0;
                visuals.Records.Add(newVisual);

                spell[SpellVisualField] = copy;
                manifest.Add(string.Format("spell {0}: visual {1} -> {2} (area kit {3} -> {4}, rains {5} -> {6} a second)",
                    spellId, visualId, copy, kitId, kitCopy, oldRate, Rate));
            }
            return manifest;
        }
    }
}

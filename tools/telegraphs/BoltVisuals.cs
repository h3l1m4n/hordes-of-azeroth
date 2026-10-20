using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class BoltVisuals
    {
        const uint FirstVisualId = 8250;
        const int SpellVisualField = 115;
        const int VisualImpactKitField = 3;
        const int VisualHasMissileField = 6;
        const int VisualMissileModelField = 7;

        static readonly uint[] Spells =
        {
            9053, 9483, 9532, 9613, 9672, 11021, 11131, 11985, 12466, 12471, 12739, 13748, 14034, 15228, 15230, 15242, 16788, 17393,
            17503, 19391, 19452, 19728, 20297, 20792, 20793, 20816, 20822, 20823, 20825, 20829, 21369, 21832, 24334, 25748, 27806,
        };

        public static List<string> AddBoltVisuals(Dbc spells, Dbc visuals)
        {
            uint nextVisual = FirstVisualId;
            Dictionary<uint, uint> copies = new Dictionary<uint, uint>();
            List<string> manifest = new List<string>();
            foreach (uint spellId in Spells)
            {
                uint[] spell = spells.Find(spellId);
                if (spell == null)
                    throw new InvalidDataException("Spell " + spellId + " is not in Spell.dbc.");
                uint visualId = spell[SpellVisualField];
                uint copy;
                if (!copies.TryGetValue(visualId, out copy))
                {
                    uint[] visual = visuals.Find(visualId);
                    if (visual == null || visual[VisualHasMissileField] == 0)
                        throw new InvalidDataException("Spell " + spellId + "'s visual " + visualId + " has no missile.");

                    copy = nextVisual++;
                    if (visuals.Find(copy) != null)
                        throw new InvalidOperationException("Spell visual " + copy + " is taken.");
                    uint[] newVisual = (uint[])visual.Clone();
                    newVisual[0] = copy;
                    newVisual[VisualImpactKitField] = 0;
                    newVisual[VisualHasMissileField] = 0;
                    newVisual[VisualMissileModelField] = 0;
                    visuals.Records.Add(newVisual);
                    copies[visualId] = copy;
                    manifest.Add(string.Format("enemy bolt visual {0} -> {1} (no missile {2}, impact kit {3} played by the server)",
                        visualId, copy, visual[VisualMissileModelField], visual[VisualImpactKitField]));
                }
                spell[SpellVisualField] = copy;
            }
            manifest.Add(string.Format("{0} enemy bolt spells fly as creatures", Spells.Length));
            return manifest;
        }
    }
}

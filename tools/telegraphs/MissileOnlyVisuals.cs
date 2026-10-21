using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class MissileOnlyVisuals
    {
        const uint FirstVisualId = 8550;
        const int SpellSpeedField = 37;
        const int SpellVisualField = 115;
        const int VisualPrecastKitField = 1;
        const int VisualCastKitField = 2;
        const int VisualHasMissileField = 6;

        static readonly uint[][] LookFromSource =
        {
            new uint[] { 17286, 24239 },
        };

        public static List<string> AddMissileOnlyVisuals(Dbc spells, Dbc visuals)
        {
            uint nextVisual = FirstVisualId;
            List<string> manifest = new List<string>();
            foreach (uint[] pair in LookFromSource)
            {
                uint[] look = spells.Find(pair[0]);
                uint[] source = spells.Find(pair[1]);
                if (look == null || source == null)
                    throw new InvalidDataException("Spell " + pair[0] + " or " + pair[1] + " is not in Spell.dbc.");
                uint visualId = source[SpellVisualField];
                uint[] visual = visuals.Find(visualId);
                if (visual == null || visual[VisualHasMissileField] == 0)
                    throw new InvalidDataException("Spell " + pair[1] + "'s visual " + visualId + " has no missile.");

                uint copy = nextVisual++;
                if (visuals.Find(copy) != null)
                    throw new InvalidOperationException("Spell visual " + copy + " is taken.");
                uint[] newVisual = (uint[])visual.Clone();
                newVisual[0] = copy;
                newVisual[VisualPrecastKitField] = 0;
                newVisual[VisualCastKitField] = 0;
                visuals.Records.Add(newVisual);

                look[SpellVisualField] = copy;
                look[SpellSpeedField] = source[SpellSpeedField];
                manifest.Add(string.Format("spell {0}: visual {1} (missile of spell {2}'s visual {3}, no precast kit {4}, no cast kit {5}, its missile speed)",
                    pair[0], copy, pair[1], visualId, visual[VisualPrecastKitField], visual[VisualCastKitField]));
            }
            return manifest;
        }

        static readonly uint[][] MissileSpeeds =
        {
            new uint[] { 20685, 40 },
        };

        public static List<string> SetMissileSpeeds(Dbc spells)
        {
            List<string> manifest = new List<string>();
            foreach (uint[] entry in MissileSpeeds)
            {
                uint[] spell = spells.Find(entry[0]);
                if (spell == null)
                    throw new InvalidDataException("Spell " + entry[0] + " is not in Spell.dbc.");
                spell[SpellSpeedField] = BitConverter.ToUInt32(BitConverter.GetBytes((float)entry[1]), 0);
                manifest.Add(string.Format("spell {0}: missile speed {1} yd/s", entry[0], entry[1]));
            }
            return manifest;
        }
    }
}

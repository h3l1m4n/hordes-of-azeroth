using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class GroundlessVisuals
    {
        const uint FirstVisualId = 8150;
        const int SpellVisualField = 115;
        const int VisualHasAreaEffectField = 11;
        const int VisualAreaModelField = 12;
        const int VisualAreaKitField = 13;

        static readonly uint[] Spells =
        {
            1543,
        };

        public static List<string> AddGroundlessVisuals(Dbc spells, Dbc visuals)
        {
            uint nextVisual = FirstVisualId;
            List<string> manifest = new List<string>();
            foreach (uint spellId in Spells)
            {
                uint[] spell = spells.Find(spellId);
                if (spell == null)
                    throw new InvalidDataException("Spell " + spellId + " is not in Spell.dbc.");
                uint visualId = spell[SpellVisualField];
                uint[] visual = visuals.Find(visualId);
                if (visual == null || (visual[VisualAreaModelField] == 0 && visual[VisualAreaKitField] == 0))
                    throw new InvalidDataException("Spell " + spellId + "'s visual " + visualId + " leaves nothing on the ground.");

                uint copy = nextVisual++;
                if (visuals.Find(copy) != null)
                    throw new InvalidOperationException("Spell visual " + copy + " is taken.");
                uint[] newVisual = (uint[])visual.Clone();
                newVisual[0] = copy;
                newVisual[VisualHasAreaEffectField] = 0;
                newVisual[VisualAreaModelField] = 0;
                newVisual[VisualAreaKitField] = 0;
                visuals.Records.Add(newVisual);

                spell[SpellVisualField] = copy;
                manifest.Add(string.Format("spell {0}: visual {1} -> {2} (no ground model {3}, no area kit {4})",
                    spellId, visualId, copy, visual[VisualAreaModelField], visual[VisualAreaKitField]));
            }
            return manifest;
        }
    }
}

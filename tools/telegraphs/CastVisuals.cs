using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class CastVisuals
    {
        const uint FirstVisualId = 8000;
        const uint FirstKitId = 7000;
        const int SpellVisualField = 115;
        const int VisualPrecastKitField = 1;
        const int VisualCastKitField = 2;
        const int VisualHasMissileField = 6;
        const int KitAnimField = 2;
        const uint None = uint.MaxValue;

        static readonly uint[] Spells =
        {
            116,
            133,
            11366,
            7268,
            16075,
            403,
            686,
            5176,
            24239,
            24099,
        };

        static readonly HashSet<uint> FiredFromShotSources = new HashSet<uint>
        {
            403,
        };

        public static List<string> AddCastVisuals(Dbc spells, Dbc visuals, Dbc kits)
        {
            Dictionary<uint, uint> kitCopies = new Dictionary<uint, uint>(), visualCopies = new Dictionary<uint, uint>();
            uint nextKit = FirstKitId, nextVisual = FirstVisualId;
            List<string> manifest = new List<string>();
            foreach (uint spellId in Spells)
            {
                uint[] spell = spells.Find(spellId);
                if (spell == null)
                    throw new InvalidDataException("Spell " + spellId + " is not in Spell.dbc.");
                uint visualId = spell[SpellVisualField];
                uint[] visual = visuals.Find(visualId);
                if (visual == null || visual[VisualHasMissileField] == 0)
                    throw new InvalidDataException("Spell " + spellId + "'s visual " + visualId + " has no missile.");
                uint kitId = visual[VisualCastKitField];
                uint[] kit = kitId != 0 ? kits.Find(kitId) : null;
                if (kit == null || kit[KitAnimField] == 0 || kit[KitAnimField] == None)
                    throw new InvalidDataException("Spell " + spellId + "'s visual " + visualId + " plays no cast animation.");

                uint copy;
                if (!visualCopies.TryGetValue(visualId, out copy))
                {
                    uint kitCopy;
                    if (!kitCopies.TryGetValue(kitId, out kitCopy))
                    {
                        kitCopy = nextKit++;
                        if (kits.Find(kitCopy) != null)
                            throw new InvalidOperationException("Spell visual kit " + kitCopy + " is taken.");
                        uint[] newKit = (uint[])kit.Clone();
                        newKit[0] = kitCopy;
                        newKit[KitAnimField] = None;
                        kits.Records.Add(newKit);
                        kitCopies[kitId] = kitCopy;
                    }

                    copy = nextVisual++;
                    if (visuals.Find(copy) != null)
                        throw new InvalidOperationException("Spell visual " + copy + " is taken.");
                    uint[] newVisual = (uint[])visual.Clone();
                    newVisual[0] = copy;
                    newVisual[VisualCastKitField] = kitCopy;
                    if (FiredFromShotSources.Contains(spellId))
                    {
                        newVisual[VisualPrecastKitField] = 0;
                        newVisual[VisualCastKitField] = 0;
                    }
                    visuals.Records.Add(newVisual);
                    visualCopies[visualId] = copy;
                }
                spell[SpellVisualField] = copy;
                manifest.Add(FiredFromShotSources.Contains(spellId)
                    ? string.Format("spell {0}: visual {1} -> {2} (no precast or cast kit, the server sends the hero's cast kit)", spellId, visualId, copy)
                    : string.Format("spell {0}: visual {1} -> {2} (cast kit {3} -> {4}, no animation)", spellId, visualId, copy, kitId, kitCopies[kitId]));
            }
            return manifest;
        }
    }
}

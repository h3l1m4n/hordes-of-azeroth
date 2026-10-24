using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class MutedSounds
    {
        const int KitSoundField = 13;
        const uint SwingSound = 3091;

        static readonly uint[] SwingKits = { 324, 366, 394, 399, 253, 733 };

        public static List<string> MuteMeleeSwings(Dbc kits)
        {
            List<string> manifest = new List<string>();
            foreach (uint kitId in SwingKits)
            {
                uint[] kit = kits.Find(kitId);
                if (kit == null || kit[KitSoundField] != SwingSound)
                    throw new InvalidDataException("Spell visual kit " + kitId + " does not play the melee swing sound.");
                kit[KitSoundField] = 0;
                manifest.Add(string.Format("kit {0}: swing sound {1} muted", kitId, SwingSound));
            }
            return manifest;
        }

        const int SpellVisualField = 115;
        const uint HearthstoneSpell = 8690;
        const uint HearthstoneVisual = 220;

        public static string ClearHearthstoneCast(Dbc spells)
        {
            uint[] spell = spells.Find(HearthstoneSpell);
            if (spell == null || spell[SpellVisualField] != HearthstoneVisual)
                throw new InvalidDataException("Spell " + HearthstoneSpell + " does not show the Hearthstone's cast visual " + HearthstoneVisual + ".");
            spell[SpellVisualField] = 0;
            return string.Format("spell {0}: Hearthstone cast visual {1} and its hum cleared; the run's ring shows the cast instead", HearthstoneSpell, HearthstoneVisual);
        }

        static readonly uint[][] SoundSwaps =
        {
            new uint[] { 487, 3320, 1273 },
        };

        public static List<string> SwapKitSounds(Dbc kits)
        {
            List<string> manifest = new List<string>();
            foreach (uint[] swap in SoundSwaps)
            {
                uint[] kit = kits.Find(swap[0]);
                if (kit == null || kit[KitSoundField] != swap[1])
                    throw new InvalidDataException("Spell visual kit " + swap[0] + " does not play sound " + swap[1] + ".");
                kit[KitSoundField] = swap[2];
                manifest.Add(string.Format("kit {0}: sound {1} -> {2}", swap[0], swap[1], swap[2]));
            }
            return manifest;
        }
    }
}

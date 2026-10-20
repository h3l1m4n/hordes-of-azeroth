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
    }
}

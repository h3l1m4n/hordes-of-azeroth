using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class QuietKits
    {
        const int KitShakeField = 14;
        const uint FirstKitId = 7400;

        static readonly uint[] ShakingKits = { 6466, 4811 };

        public static List<string> AddQuietCopies(Dbc kits)
        {
            List<string> manifest = new List<string>();
            uint nextKit = FirstKitId;
            foreach (uint kitId in ShakingKits)
            {
                uint[] kit = kits.Find(kitId);
                if (kit == null)
                    throw new InvalidDataException("Spell visual kit " + kitId + " is not in SpellVisualKit.dbc.");
                if (kits.Find(nextKit) != null)
                    throw new InvalidOperationException("Spell visual kit " + nextKit + " is taken.");

                uint[] copy = (uint[])kit.Clone();
                copy[0] = nextKit;
                copy[KitShakeField] = 0;
                kits.Records.Add(copy);
                manifest.Add(string.Format("kit {0}: copy of kit {1} without its camera shake {2}", nextKit, kitId, kit[KitShakeField]));
                nextKit++;
            }
            return manifest;
        }
    }
}

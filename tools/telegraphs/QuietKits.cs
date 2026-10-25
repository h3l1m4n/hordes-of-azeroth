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

        const int KitAnimationField = 2;
        const int KitSoundField = 13;
        const uint NoAnimation = uint.MaxValue;

        static readonly uint[][] StillClawKits =
        {
            new uint[] { 269, 7410 },
            new uint[] { 3370, 7411 },
            new uint[] { 182, 7413 },
            new uint[] { 268, 7414 },
        };

        const int KitSpecialEffectField = 12;
        const uint NoEffect = uint.MaxValue;

        static readonly uint[][] AreaFreeKits =
        {
            new uint[] { 3374, 7415 },
        };

        public static List<string> AddAreaFreeCopies(Dbc kits)
        {
            List<string> manifest = new List<string>();
            foreach (uint[] pair in AreaFreeKits)
            {
                uint[] kit = kits.Find(pair[0]);
                if (kit == null)
                    throw new InvalidDataException("Spell visual kit " + pair[0] + " is not in SpellVisualKit.dbc.");
                if (kits.Find(pair[1]) != null)
                    throw new InvalidOperationException("Spell visual kit " + pair[1] + " is taken.");

                uint[] copy = (uint[])kit.Clone();
                copy[0] = pair[1];
                copy[KitSpecialEffectField] = NoEffect;
                kits.Records.Add(copy);
                manifest.Add(string.Format("kit {0}: copy of kit {1} without its area model {2}", pair[1], pair[0], kit[KitSpecialEffectField]));
            }
            return manifest;
        }

        public static List<string> AddStillClawCopies(Dbc kits)
        {
            List<string> manifest = new List<string>();
            foreach (uint[] pair in StillClawKits)
            {
                uint[] kit = kits.Find(pair[0]);
                if (kit == null)
                    throw new InvalidDataException("Spell visual kit " + pair[0] + " is not in SpellVisualKit.dbc.");
                if (kits.Find(pair[1]) != null)
                    throw new InvalidOperationException("Spell visual kit " + pair[1] + " is taken.");

                uint[] copy = (uint[])kit.Clone();
                copy[0] = pair[1];
                copy[KitAnimationField] = NoAnimation;
                copy[KitSoundField] = 0;
                kits.Records.Add(copy);
                manifest.Add(string.Format("kit {0}: copy of claw kit {1} with its ghost model but no body animation or sound", pair[1], pair[0]));
            }
            return manifest;
        }

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

using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class BossLooks
    {
        const int KitAnimField = 2, KitHeadField = 3, KitChestField = 4, KitBaseField = 5, KitLeftHandField = 6, KitRightHandField = 7, KitBreathField = 8;
        const int KitLeftWeaponField = 9, KitSpecialTwoField = 12;
        const uint NoEffect = 0xFFFFFFFF;
        public const uint FirstKitId = 7450;

        sealed class Look
        {
            public string Name;
            public uint Donor;
            public int Slot;
            public uint Effect;
            public bool KeepAnim;
        }

        static readonly Look[] Looks =
        {
            new Look { Name = "frost breath", Donor = 3449, Slot = KitBreathField, Effect = 1344, KeepAnim = true },
            new Look { Name = "acid breath", Donor = 3449, Slot = KitHeadField, Effect = 2980, KeepAnim = true },
            new Look { Name = "shadow burst", Donor = 2350, Slot = KitBaseField, Effect = 150 },
            new Look { Name = "shadow pool", Donor = 2350, Slot = KitBaseField, Effect = 1282 },
            new Look { Name = "poison burst", Donor = 6377, Slot = KitBaseField, Effect = 236 },
            new Look { Name = "holy burst", Donor = 487, Slot = KitBaseField, Effect = 1722 },
            new Look { Name = "dust nova", Donor = 356, Slot = KitBaseField, Effect = 2683, KeepAnim = true },
            new Look { Name = "shadow fissure", Donor = 2350, Slot = KitBaseField, Effect = 3035 },
        };

        public static List<string> AddLooks(Dbc kits, Dbc effectNames)
        {
            List<string> manifest = new List<string>();
            uint nextKit = FirstKitId;
            foreach (Look look in Looks)
            {
                uint[] donor = kits.Find(look.Donor);
                if (donor == null)
                    throw new InvalidDataException("Spell visual kit " + look.Donor + " is not in SpellVisualKit.dbc.");
                if (effectNames.Find(look.Effect) == null)
                    throw new InvalidDataException("Spell visual effect " + look.Effect + " is not in SpellVisualEffectName.dbc.");
                if (kits.Find(nextKit) != null)
                    throw new InvalidOperationException("Spell visual kit " + nextKit + " is taken.");

                uint[] kit = (uint[])donor.Clone();
                kit[0] = nextKit;
                foreach (int field in new[] { KitHeadField, KitChestField, KitBaseField, KitLeftHandField, KitRightHandField, KitBreathField, KitLeftWeaponField, KitSpecialTwoField })
                    kit[field] = NoEffect;
                if (!look.KeepAnim)
                    kit[KitAnimField] = NoEffect;
                kit[look.Slot] = look.Effect;
                kits.Records.Add(kit);
                manifest.Add(string.Format("kit {0}: {1}, from kit {2} with effect {3}", nextKit, look.Name, look.Donor, look.Effect));
                nextKit++;
            }
            return manifest;
        }
    }
}

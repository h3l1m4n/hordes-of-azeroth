using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    sealed class AuraLook
    {
        public uint SpellId;
        public string Name;
        public string Icon;
        public string ToolTip;
        public uint StateKit;
        public uint Visual;
        public uint IconId;
    }

    static class AuraLooks
    {
        const uint RetributionRingSpell = 7069;
        const uint StateVisualTemplate = 565;
        const uint FirstVisualId = 8350;

        const uint ThornsStateEffect = 1942, WhirlwindStateEffect = 361;
        const uint ThornsKit = 895, WhirlwindSpinKit = 369;
        const uint ThornsStateEffectCopy = 3150, WhirlwindStateEffectCopy = 3151;
        public const uint ThornsStateKit = 7350, WhirlwindStateKit = 7351, WhirlwindSpinOnlyKit = 7352, LootSparkleStateKit = 7353, MailSparkleStateKit = 7354;
        const uint PartyFeverStateKit = 560, LootSparkleEffect = 14;
        const uint SandVortexKit = 6472;
        const uint ShadowTranceVisual = 5219;
        const uint PresenceOfMindStateKit = 4013, BerserkerRageStateKit = 108, RetaliationStateKit = 6443, ArcanePowerStateKit = 3769, ManaShieldStateKit = 990;

        const int Attributes = 6, AttributesEx = 7;
        const int ProcFlags = 24, ProcChance = 25, ProcCharges = 26;
        const int EffectField = 61, EffectBasePointsField = 76, EffectTargetAField = 82, EffectTargetBField = 85, EffectRadiusField = 88;
        const int EffectAuraField = 91, EffectTriggerSpellField = 109;
        const int SpellVisualField = 115, SpellIconField = 117, NameField = 120, ToolTipField = 147;
        const uint ApplyAura = 6, AuraDummy = 4, TargetCaster = 1;
        const int VisualPrecastKitField = 1, VisualCastKitField = 2, VisualImpactKitField = 3, VisualStateKitField = 4, VisualChannelKitField = 5;
        const int KitAnimField = 2, KitChestEffectField = 4, KitBaseEffectField = 5;
        const int IconPathField = 1;
        const uint None = uint.MaxValue;

        public static readonly AuraLook[] Looks =
        {
            new AuraLook { SpellId = 33401, Name = "Blood Aura", Icon = "Spell_Shadow_RitualOfSacrifice", ToolTip = "Enemies near you bleed." },
            new AuraLook { SpellId = 33424, Name = "Ice Path", Icon = "Spell_Frost_FrostArmor02", ToolTip = "Ice around you slows nearby enemies." },
            new AuraLook { SpellId = 33447, Name = "Spiked Shield", Icon = "Spell_Nature_Thorns", ToolTip = "Spikes strike enemies near you while your Shield holds.", StateKit = ThornsStateKit },
            new AuraLook { SpellId = 33448, Name = "Steady Aim", Icon = "Ability_Hunter_AimedShot", ToolTip = "Standing still. Steady Aim attacks more often.", StateKit = PresenceOfMindStateKit },
            new AuraLook { SpellId = 33449, Name = "Bladestorm", Icon = "INV_Axe_17", ToolTip = "Spinning, striking all enemies near you.", StateKit = WhirlwindStateKit },
            new AuraLook { SpellId = 33450, Name = "Vengeance", Icon = "Spell_Holy_Retribution", ToolTip = "Damage you take is stored for your next strike.", StateKit = BerserkerRageStateKit },
            new AuraLook { SpellId = 33451, Name = "Vengeance", Icon = "Spell_Holy_Retribution", ToolTip = "Your next strike releases a burst of stored damage.", StateKit = RetaliationStateKit },
            new AuraLook { SpellId = 33452, Name = "Arcane Power", Icon = "Spell_Nature_Lightning", ToolTip = "Your spells deal 30% more damage.", StateKit = ArcanePowerStateKit },
            new AuraLook { SpellId = 33453, Name = "Phase Rush", Icon = "Spell_Magic_LesserInvisibilty", ToolTip = "Immune to damage.", StateKit = ManaShieldStateKit },
            new AuraLook { SpellId = 33454, Name = "Loot Sparkle", Icon = "Spell_Holy_HolyBolt", ToolTip = "Something worth picking up.", StateKit = LootSparkleStateKit },
            new AuraLook { SpellId = 33455, Name = "Mail Sparkle", Icon = "Spell_Holy_HolyBolt", ToolTip = "You have mail.", StateKit = MailSparkleStateKit },
            new AuraLook { SpellId = 33460, Name = "Sand Vortex", Icon = "Spell_Nature_Cyclone", ToolTip = "A sand vortex.", StateKit = SandVortexKit },
            new AuraLook { SpellId = 33461, Name = "Souls", Icon = "Spell_Shadow_Twilight", ToolTip = "You deal 5% more damage for each soul.", Visual = ShadowTranceVisual },
            new AuraLook { SpellId = 33462, Name = "Enrage", ToolTip = "Haste increased.", Visual = 870, IconId = 95 },
            new AuraLook { SpellId = 33463, Name = "Divine Shield", ToolTip = "A golden bubble protects you.", Visual = 154, IconId = 81 },
            new AuraLook { SpellId = 33464, Name = "Renew", ToolTip = "Renew heals you every second. At full health it smites a nearby enemy instead.", Visual = 280, IconId = 321 },
            new AuraLook { SpellId = 33465, Name = "Lightning Shield", ToolTip = "Enemies that strike you are zapped for Nature damage and gain 1 stack of Static. Each charge zaps once.", Visual = 37, IconId = 19 },
            new AuraLook { SpellId = 33466, Name = "Blade Flurry", ToolTip = "Your single-target hits also strike more enemies nearby.", Visual = 211, IconId = 1477 },
            new AuraLook { SpellId = 33467, Name = "Deterrence", ToolTip = "Dodge chance increased by 50%.", Visual = 5739, IconId = 83 },
            new AuraLook { SpellId = 33468, Name = "Revived", ToolTip = "Immune to physical damage.", Visual = 302, IconId = 303 },
        };

        public static List<string> AddAuraLooks(Dbc spells, Dbc visuals, Dbc kits, Dbc effects, Dbc icons)
        {
            uint[] source = spells.Find(RetributionRingSpell), visualTemplate = visuals.Find(StateVisualTemplate);
            if (source == null || visualTemplate == null)
                throw new InvalidDataException("Spell " + RetributionRingSpell + " or visual " + StateVisualTemplate + " is not in the client's tables.");

            List<string> manifest = AddStateKits(kits, effects);
            uint visualId = FirstVisualId;
            foreach (AuraLook look in Looks)
            {
                if (spells.Find(look.SpellId) != null)
                    throw new InvalidOperationException("Spell " + look.SpellId + " is taken.");

                uint[] spell = (uint[])source.Clone();
                spell[0] = look.SpellId;
                spell[Attributes] = 0;
                spell[AttributesEx] = 0;
                spell[ProcFlags] = 0;
                spell[ProcChance] = 0;
                spell[ProcCharges] = 0;
                spell[EffectField] = ApplyAura;
                spell[EffectBasePointsField] = 0;
                spell[EffectTargetAField] = TargetCaster;
                spell[EffectTargetBField] = 0;
                spell[EffectRadiusField] = 0;
                spell[EffectAuraField] = AuraDummy;
                spell[EffectTriggerSpellField] = 0;
                spell[SpellIconField] = look.IconId != 0 ? look.IconId : FindIcon(icons, look.Icon);
                spell[NameField] = spells.AddString(look.Name);
                spell[ToolTipField] = spells.AddString(look.ToolTip);
                spell[SpellVisualField] = 0;

                if (look.Visual != 0)
                {
                    spell[SpellVisualField] = look.Visual;
                    manifest.Add(string.Format("spell {0} ({1}): visual {2}", look.SpellId, look.Name, look.Visual));
                }
                else if (look.StateKit != 0)
                {
                    if (visuals.Find(visualId) != null)
                        throw new InvalidOperationException("Spell visual " + visualId + " is taken.");
                    uint[] visual = (uint[])visualTemplate.Clone();
                    visual[0] = visualId;
                    visual[VisualPrecastKitField] = visual[VisualCastKitField] = visual[VisualImpactKitField] = visual[VisualChannelKitField] = 0;
                    visual[VisualStateKitField] = look.StateKit;
                    visuals.Records.Add(visual);
                    spell[SpellVisualField] = visualId;
                    manifest.Add(string.Format("spell {0} ({1}): visual {2}, state kit {3}", look.SpellId, look.Name, visualId, look.StateKit));
                    visualId++;
                }
                else
                    manifest.Add(string.Format("spell {0} ({1}): dummy aura on the caster, aura ring template", look.SpellId, look.Name));
                spells.Records.Add(spell);
            }
            return manifest;
        }

        static List<string> AddStateKits(Dbc kits, Dbc effects)
        {
            CopyEffect(effects, ThornsStateEffect, ThornsStateEffectCopy);
            CopyEffect(effects, WhirlwindStateEffect, WhirlwindStateEffectCopy);

            uint[] thorns = CopyKit(kits, ThornsKit, ThornsStateKit);
            thorns[KitChestEffectField] = None;
            thorns[KitBaseEffectField] = ThornsStateEffectCopy;

            uint[] whirlwind = CopyKit(kits, WhirlwindSpinKit, WhirlwindStateKit);
            whirlwind[KitAnimField] = None;
            whirlwind[KitBaseEffectField] = WhirlwindStateEffectCopy;

            uint[] spinOnly = CopyKit(kits, WhirlwindSpinKit, WhirlwindSpinOnlyKit);
            spinOnly[KitBaseEffectField] = None;

            uint[] sparkle = CopyKit(kits, PartyFeverStateKit, LootSparkleStateKit);
            sparkle[KitChestEffectField] = LootSparkleEffect;
            sparkle[KitBaseEffectField] = LootSparkleEffect;

            uint[] mailSparkle = CopyKit(kits, PartyFeverStateKit, MailSparkleStateKit);
            mailSparkle[KitChestEffectField] = None;
            mailSparkle[KitBaseEffectField] = LootSparkleEffect;

            return new List<string>
            {
                string.Format("state kit {0}: thorns at the feet (effect {1} copies {2})", ThornsStateKit, ThornsStateEffectCopy, ThornsStateEffect),
                string.Format("state kit {0}: whirlwind blades at the feet (effect {1} copies {2})", WhirlwindStateKit, WhirlwindStateEffectCopy, WhirlwindStateEffect),
                string.Format("kit {0}: kit {1}'s spin with no blades", WhirlwindSpinOnlyKit, WhirlwindSpinKit),
                string.Format("state kit {0}: the corpse loot sparkle (effect {1}) at the chest and the feet, silent", LootSparkleStateKit, LootSparkleEffect),
                string.Format("state kit {0}: the corpse loot sparkle (effect {1}) at the feet only, silent", MailSparkleStateKit, LootSparkleEffect),
            };
        }

        static void CopyEffect(Dbc effects, uint sourceId, uint id)
        {
            uint[] source = effects.Find(sourceId);
            if (source == null)
                throw new InvalidDataException("Spell visual effect " + sourceId + " is not in the client's tables.");
            if (effects.Find(id) != null)
                throw new InvalidOperationException("Spell visual effect " + id + " is taken.");
            uint[] effect = (uint[])source.Clone();
            effect[0] = id;
            effects.Records.Add(effect);
        }

        static uint[] CopyKit(Dbc kits, uint sourceId, uint id)
        {
            uint[] source = kits.Find(sourceId);
            if (source == null)
                throw new InvalidDataException("Spell visual kit " + sourceId + " is not in the client's tables.");
            if (kits.Find(id) != null)
                throw new InvalidOperationException("Spell visual kit " + id + " is taken.");
            uint[] kit = (uint[])source.Clone();
            kit[0] = id;
            kits.Records.Add(kit);
            return kit;
        }

        static uint FindIcon(Dbc icons, string name)
        {
            string suffix = "\\" + name;
            foreach (uint[] icon in icons.Records)
                if (icons.GetString(icon[IconPathField]).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return icon[0];
            throw new InvalidDataException("Icon " + name + " is not in SpellIcon.dbc.");
        }
    }
}

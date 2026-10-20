using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class ExploderVisuals
    {
        const uint LitFuseSpell = 33400;
        const uint KitId = 7200;
        const uint VisualId = 8200;
        const uint DynamiteKit = 238;
        const uint StateVisual = 349;
        const uint TemplateSpell = 4051;
        const uint IconSpell = 7978;
        const uint None = uint.MaxValue;

        const int Attributes = 6, AttributesEx = 7;
        const int ProcFlags = 24, ProcChance = 25, ProcCharges = 26, BaseLevel = 28, SpellLevel = 29;
        const int EffectAuraField = 91, EffectTriggerSpellField = 109;
        const int SpellVisualField = 115, SpellIconField = 117, NameField = 120, ToolTipField = 147;
        const uint AuraDummy = 4;
        const int VisualStateKitField = 4;
        const int KitAnimField = 2;

        public static List<string> AddExploderVisuals(Dbc spells, Dbc visuals, Dbc kits)
        {
            uint[] dynamite = kits.Find(DynamiteKit);
            uint[] stateVisual = visuals.Find(StateVisual);
            uint[] template = spells.Find(TemplateSpell);
            uint[] icon = spells.Find(IconSpell);
            if (dynamite == null || stateVisual == null || template == null || icon == null)
                throw new InvalidDataException("The lit fuse's sources are not in the client's tables.");
            if (kits.Find(KitId) != null || visuals.Find(VisualId) != null || spells.Find(LitFuseSpell) != null)
                throw new InvalidOperationException("The lit fuse's ids are taken.");

            uint[] kit = (uint[])dynamite.Clone();
            kit[0] = KitId;
            kit[KitAnimField] = None;
            kits.Records.Add(kit);

            uint[] visual = (uint[])stateVisual.Clone();
            visual[0] = VisualId;
            visual[VisualStateKitField] = KitId;
            visuals.Records.Add(visual);

            uint[] spell = (uint[])template.Clone();
            spell[0] = LitFuseSpell;
            spell[Attributes] = 0;
            spell[AttributesEx] = 0;
            spell[ProcFlags] = 0;
            spell[ProcChance] = 0;
            spell[ProcCharges] = 0;
            spell[BaseLevel] = 0;
            spell[SpellLevel] = 0;
            spell[EffectAuraField] = AuraDummy;
            spell[EffectTriggerSpellField] = 0;
            spell[SpellVisualField] = VisualId;
            spell[SpellIconField] = icon[SpellIconField];
            spell[NameField] = spells.AddString("Lit Fuse");
            spell[ToolTipField] = spells.AddString("Blows up when it reaches you.");
            spells.Records.Add(spell);

            return new List<string>
            {
                string.Format("spell {0} (Lit Fuse): visual {1}, state kit {2} (kit {3}'s lit dynamite, no animation)",
                    LitFuseSpell, VisualId, KitId, DynamiteKit),
            };
        }
    }
}

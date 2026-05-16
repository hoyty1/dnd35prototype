// ============================================================================
// SpellDatabase_O.cs — Spells starting with O
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsO()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.OBSCURE_OBJECT,
                    Name = "Obscure Object",
                    Description = "Masks object against scrying. Duration 8 hours. PHB p.258",
                    SpellLevel = 2, School = "Abjuration",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Anti-scrying not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.OBSCURING_MIST,
                    Name = "Obscuring Mist",
                    Description = "Mist spreads in a 20-ft radius and grants concealment (20% miss chance) to creatures inside. Duration 1 min/level. PHB p.258",
                    SpellLevel = 1, School = "Conjuration",
                    ClassList = new[] { "Wizard", "Sorcerer", "Druid", "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Close,
                    RangeSquares = 4,
                    AreaRadius = 4,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4,
                    AoERangeSquares = 4,
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 10,
                    BuffType = "concealment",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.OPEN_CLOSE,
                    Name = "Open/Close",
                    Description = "Opens or closes small or light things (door, chest, bottle, etc.).",
                    SpellLevel = 0, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Object interaction not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.OWLS_WISDOM,
                    Name = "Owl's Wisdom",
                    Description = "Subject gains +4 enhancement bonus to WIS for 1 min/level. Affects Will saves, Wis-based skills, and Cleric/Druid/Ranger spell DCs. Does NOT grant bonus spells. PHB p.259",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "WIS",
                    BuffStatBonus = 4,
                    BuffDurationRounds = 30,
                    BuffType = "enhancement",
                    BuffBonusType = BonusType.Enhancement,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // OTILUKE'S RESILIENT SPHERE  (PHB p.263)
        // Evocation [Force]
        // Level: Sor/Wiz 4
        // Components: V, S, M (a hemisphere of clear crystal and a matching
        //   hemisphere of gum arabic)
        // Casting Time: 1 standard action
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Target: One Large or smaller creature
        // Duration: 1 min./level (D)
        // Saving Throw: Reflex negates
        // Spell Resistance: Yes
        //
        // A globe of shimmering force encloses the target creature.
        // The sphere moves with the creature — they can move and act freely.
        // The sphere is INDESTRUCTIBLE by normal means (no HP, no Hardness).
        // Nothing can pass through the sphere, in or out (bidirectional isolation).
        // Only Disintegrate, Rod of Cancellation, Rod of Negation, or Dispel Magic can remove it.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.RESILIENT_SPHERE,
                    Name = "Resilient Sphere",
                    Description = "Evocation [Force]. A globe of shimmering force encloses the target creature. The sphere moves with the creature — they can move and act freely, but nothing can pass through in either direction. Indestructible by normal means. Only Disintegrate, Rod of Cancellation/Negation, or Dispel Magic can remove it. Duration 1 min/level (D). PHB p.263",
                    SpellLevel = 4,
                    School = "Evocation [Force]",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Debuff,
                    BuffType = SpellNames.RESILIENT_SPHERE,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SpellResistanceApplies = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // Aliases
        RegisterClassSpellAlias("obscuring_mist_clr", SpellNames.OBSCURING_MIST, "Cleric", 1);

        // Aliases — Owl's Wisdom: Cleric 2, Druid 2, Paladin 2, Ranger 2
        RegisterClassSpellAlias("owls_wisdom_clr", SpellNames.OWLS_WISDOM, "Cleric", 2);
        RegisterClassSpellAlias("owls_wisdom_drd", SpellNames.OWLS_WISDOM, "Druid", 2);
        RegisterClassSpellAlias("owls_wisdom_pal", SpellNames.OWLS_WISDOM, "Paladin", 2);
        RegisterClassSpellAlias("owls_wisdom_rgr", SpellNames.OWLS_WISDOM, "Ranger", 2);

    }
}

// ============================================================================
// SpellDatabase_V.cs — Spells starting with V
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsV()
    {
        // ──────────────────────────────────────────────────────────────
        // VAMPIRIC TOUCH  (PHB p.281)
        // Necromancy [Negative Energy]
        // Level: Sor/Wiz 3
        // Components: V, S
        // Casting Time: 1 standard action
        // Range: Touch
        // Target: Living creature touched
        // Duration: Instantaneous/1 hour; see text
        // Saving Throw: None
        // Spell Resistance: Yes
        //
        // You must succeed on a melee touch attack. Your touch deals
        // 1d6 points of damage for every two caster levels (max 10d6).
        // You gain temporary hit points equal to the damage you deal.
        // However, you can't gain more temporary hit points than the
        // subject's current hit points + 10, which is enough to kill
        // the subject. The temporary hit points disappear 1 hour later.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.VAMPIRIC_TOUCH,
                    Name = "Vampiric Touch",
                    Description = "Necromancy [Negative Energy]. Melee touch attack. Deals 1d6 negative energy damage "
                        + "per 2 caster levels (max 10d6). Caster gains temporary hit points equal to damage dealt "
                        + "(capped by caster's max HP), lasting 1 hour. SR: Yes. PHB p.281",
                    SpellLevel = 3,
                    School = "Necromancy",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 3),
                        new SpellAvailability("Wizard", 3)
                    },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Damage,
                    DamageType = "negative",
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.VENTRILOQUISM,
                    Name = "Ventriloquism",
                    Description = "Throws voice for 1 min/level. Will disbelief. PHB p.298",
                    SpellLevel = 1, School = "Illusion",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Sound manipulation not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.VIRTUE,
                    Name = "Virtue",
                    Description = "Subject gains 1 temporary hit point for 1 minute.",
                    SpellLevel = 0, School = "Transmutation",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffTempHP = 1,
                    BuffDurationRounds = 10,
                    BuffType = "temp_hp",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

    }
}

// ============================================================================
// SpellDatabase_B.cs — Spells starting with B
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsB()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.BANE,
                    Name = "Bane",
                    Description = "Enemies take –1 on attack rolls and saves vs fear. 1 min/level. Will save negates. PHB p.203",
                    SpellLevel = 1, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy, // Simplified from area
                    RangeSquares = 10,
                    AreaRadius = 10,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffAttackBonus = -1,
                    BuffSaveBonus = -1,
                    BuffDurationRounds = 30,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // [CONSOLIDATED] domain_barkskin merged into barkskin — alias registered below

        // ── Barkskin — Druid/Ranger version (PHB p.202) ──────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.BARKSKIN,
                    Name = "Barkskin",
                    Description = "Transmutation. Barkskin toughens a creature's skin, granting a +2 enhancement bonus to natural "
                        + "armor, which increases by 1 for every three caster levels above 3rd (max +5 at 12th). "
                        + "Duration 10 min/level. Components: V, S, DF. PHB p.202",
                    SpellLevel = 2,
                    School = "Transmutation",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Druid", 2),
                        new SpellAvailability("Ranger", 2)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Buff,
                    BuffACBonus = 2,
                    BuffDurationRounds = 100, // Legacy fallback: 10 min/level
                    BuffType = "natural_armor",
                    BuffBonusType = BonusType.Enhancement,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 10,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasDivineFocus = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.BESTOW_CURSE,
                    Name = "Bestow Curse",
                    Description = "Necromancy. Melee touch attack places a terrible curse on the target. Choose one: -6 to one ability score (min 1), -4 on attacks/saves/checks, or 50% chance each turn to act normally. Will negates. SR: Yes. Permanent until removed. PHB p.203",
                    SpellLevel = 3, School = "Necromancy",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Permanent,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true
                });

        RegisterClassSpellAlias("bestow_curse_wiz", SpellNames.BESTOW_CURSE, "Wizard", 4);
        RegisterClassSpellAlias("bestow_curse_sor", SpellNames.BESTOW_CURSE, "Sorcerer", 4);

        Register(new SpellData
                {
                    SpellId = SpellNames.BEARS_ENDURANCE,
                    Name = "Bear's Endurance",
                    Description = "Subject gains +4 enhancement bonus to CON for 1 min/level. Grants +2 HP per Hit Die (real HP, not temporary). When spell ends, HP removed — can cause death. PHB p.203",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "CON",
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

        Register(new SpellData
                {
                    SpellId = SpellNames.BLESS,
                    Name = "Bless",
                    Description = "Allies in 50-ft burst gain +1 morale bonus on attack rolls and saves vs fear. 1 min/level. PHB p.205",
                    SpellLevel = 1, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Area, // 50-ft burst centered on caster
                    RangeSquares = 0, // Self-centered burst
                    AreaRadius = 10,
                    // AoE properties
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 10, // 50 ft radius = 10 squares
                    AoERangeSquares = 0, // Self-centered burst (centered on caster)
                    AoEFilter = AoETargetFilter.AlliesOnly,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 1,
                    BuffSaveBonus = 1, // vs fear, simplified to all saves
                    BuffDurationRounds = 30, // Legacy: 30 rounds at CL3
                    BuffType = "morale",
                    BuffBonusType = BonusType.Morale,
                    BonusTypeExplicitlySet = true,
                    // Duration system: 1 min/level (D&D 3.5e PHB p.205)
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ── Bless Weapon (PHB p.205) ────────────────────────────────────
        // Transmutation
        // Level: Paladin 1
        // Components: V, S
        // Casting Time: 1 standard action
        // Range: Touch
        // Target: Weapon touched
        // Duration: 1 min./level
        // Saving Throw: None
        // Spell Resistance: No
        //
        // This transmutation makes a weapon strike true against evil
        // foes. The weapon is treated as having a +1 enhancement bonus
        // for the purpose of bypassing the DR of evil creatures or
        // striking evil incorporeal creatures. The weapon also becomes
        // good-aligned for the purpose of overcoming DR/good.
        // Against evil targets, all critical threat rolls are auto-confirmed.
        //
        // NOTE: Prototype implements this as a self-targeting weapon buff
        //       that grants +1 sacred bonus to attack vs evil creatures,
        //       auto-confirms crits vs evil, and treats weapon as good-aligned.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.BLESS_WEAPON,
                    Name = "Bless Weapon",
                    Description = "Transmutation. Your weapon strikes true against evil foes. Treated as +1 enhancement for bypassing DR, "
                        + "good-aligned for overcoming DR/good. Auto-confirms critical threats vs evil. Duration 1 min./level. "
                        + "No save. No SR. Components: V, S. PHB p.205",
                    SpellLevel = 1,
                    School = "Transmutation",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Paladin", 1)
                    },
                    TargetType = SpellTargetType.Self, // Affects caster's weapon
                    RangeSquares = 0,
                    EffectType = SpellEffectType.Buff,
                    BuffAttackBonus = 1, // +1 enhancement vs evil (simplified as general +1 sacred)
                    BuffDurationRounds = 30, // Legacy fallback: ~30 rounds at CL 3
                    BuffType = "sacred",
                    BuffBonusType = BonusType.Sacred,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // Bless Weapon: Paladin 1
        RegisterClassSpellAlias("bless_weapon_pal", SpellNames.BLESS_WEAPON, "Paladin", 1);

        // ── Break Enchantment (PHB p.207) ────────────────────────────────
        // Abjuration
        // Level: Bard 4, Clr 5, Pal 4, Sor/Wiz 5
        // Components: V, S
        // Casting Time: 1 minute
        // Range: Close (25 ft. + 5 ft./2 levels)
        // Targets: Up to one creature per level, all within 30 ft. of each other
        // Duration: Instantaneous
        // Saving Throw: None (see text)
        // Spell Resistance: No
        //
        // This spell frees victims from enchantments, transmutations,
        // and curses. Break Enchantment can reverse even an instantaneous
        // effect. For each such effect, you make a caster level check
        // (1d20 + CL, max +15) against DC 11 + caster level of the effect.
        //
        // NOTE: Prototype implementation uses PerformTargetedDispel with
        //       a +15 CL cap, targeting enchantment/transmutation/curse
        //       effects. Simplified to single-target for now.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.BREAK_ENCHANTMENT,
                    Name = "Break Enchantment",
                    Description = "Abjuration. Frees victims from enchantments, transmutations, and curses. "
                        + "Caster level check (1d20 + CL, max +15) vs DC 11 + effect's CL. Instantaneous. "
                        + "No save, no SR. Components: V, S. PHB p.207",
                    SpellLevel = 5,
                    School = "Abjuration",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Cleric", 5),
                        new SpellAvailability("Sorcerer", 5),
                        new SpellAvailability("Wizard", 5),
                        new SpellAvailability("Paladin", 4),
                        new SpellAvailability("Bard", 4)
                    },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Dispel,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    DurationType = DurationType.Instantaneous,
                    ActionType = SpellActionType.Standard, // Simplified from 1 minute for prototype
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // Break Enchantment: Bard 4, Paladin 4  (Clr 5, Sor/Wiz 5 already in AvailableFor)
        RegisterClassSpellAlias("break_enchantment_brd", SpellNames.BREAK_ENCHANTMENT, "Bard", 4);
        RegisterClassSpellAlias("break_enchantment_pal", SpellNames.BREAK_ENCHANTMENT, "Paladin", 4);

        // Backward-compatibility aliases for consolidated blindness/deafness
        RegisterAlias("blindness_deafness_wiz", SpellNames.BLINDNESS_DEAFNESS);
        RegisterAlias("blindness_deafness_brd", SpellNames.BLINDNESS_DEAFNESS);
        RegisterAlias("blindness_deafness_clr", SpellNames.BLINDNESS_DEAFNESS);

        // ── Blindness/Deafness (PHB p.206) ──
        // Necromancy. V only. Medium range. Fortitude negates. SR: Yes.
        // Permanent (D). Caster chooses blindness or deafness at cast time.
        // Level: Brd 2, Clr 3, Sor/Wiz 2
        Register(new SpellData
                {
                    SpellId = SpellNames.BLINDNESS_DEAFNESS,
                    Name = "Blindness/Deafness",
                    Description = "Makes subject blind or deaf. Fortitude negates. Permanent (D). Components: V. PHB p.206",
                    SpellLevel = 2, School = "Necromancy",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Wizard", 2),
                        new SpellAvailability("Sorcerer", 2),
                        new SpellAvailability("Bard", 2),
                        new SpellAvailability("Cleric", 3)
                    },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Medium,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Fortitude",
                    SpellResistanceApplies = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = false,
                    BuffDurationRounds = -1, // Permanent
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ── Blink (PHB p.206) ──
        // Transmutation. Sor/Wiz 3. Personal range. 1 round/level (D).
        // Subject blinks back and forth between Material and Ethereal Plane.
        // Defensive: 50% miss chance (20% if attacker can see invisible OR strike ethereal, 0% if both).
        // Offensive: 20% miss chance on own attacks, +2 attack bonus (invisible), deny target Dex to AC.
        // Spells targeting blinking creature: 50% failure. Area spells: half damage.
        Register(new SpellData
                {
                    SpellId = SpellNames.BLINK,
                    Name = "Blink",
                    Description = "You blink back and forth between the Material and Ethereal Planes. Attacks against you have a 50% miss chance. Your attacks have a 20% miss chance but gain +2 (invisible). Duration 1 round/level (D). PHB p.206",
                    SpellLevel = 3,
                    School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffType = "blink",
                    BuffBonusType = BonusType.Untyped,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.BLUR,
                    Name = "Blur",
                    Description = "Illusion (Glamer). Subject touched appears blurred and wavering, gaining concealment (20% miss chance). See Invisible does not negate this effect; True Seeing does. Duration 1 min/level (D). Save: Will negates (harmless). SR: Yes (harmless). Components: V. PHB p.206",
                    SpellLevel = 2,
                    School = "Illusion (Glamer)",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.Touch,
                    RangeCategory = SpellRangeCategory.Touch,
                    IsTouch = true,
                    IsMeleeTouch = true,
                    EffectType = SpellEffectType.Buff,
                    BuffType = "concealment",
                    BuffBonusType = BonusType.Concealment,
                    BonusTypeExplicitlySet = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.BULLS_STRENGTH,
                    Name = "Bull's Strength",
                    Description = "Subject gains +4 enhancement bonus to STR for 1 min/level. Affects melee attack, damage, and Str-based skills. PHB p.207",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "STR",
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

        Register(new SpellData
                {
                    SpellId = SpellNames.BURNING_HANDS,
                    Name = "Burning Hands",
                    Description = "1d4/level fire damage (max 5d4) in 15-ft cone. Reflex half. PHB p.207",
                    SpellLevel = 1, School = "Evocation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Area, // Cone AoE from caster
                    RangeSquares = 3, // 15-ft cone ~3 squares
                    AreaRadius = 3,
                    // AoE properties
                    AoEShapeType = AoEShape.Cone,
                    AoESizeSquares = 3, // 15 ft = 3 squares length
                    AoERangeSquares = 0, // Cone originates from caster (no placement range)
                    AoEFilter = AoETargetFilter.All, // Hits all creatures in cone
                    EffectType = SpellEffectType.Damage,
                    DamageDice = 4, DamageCount = 3, // 3d4 at CL3
                    DamageType = "fire",
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // Aliases — Bear's Endurance: Cleric 2, Druid 2, Ranger 2, Bard 2
        RegisterClassSpellAlias("bears_endurance_clr", SpellNames.BEARS_ENDURANCE, "Cleric", 2);
        RegisterClassSpellAlias("bears_endurance_drd", SpellNames.BEARS_ENDURANCE, "Druid", 2);
        RegisterClassSpellAlias("bears_endurance_rgr", SpellNames.BEARS_ENDURANCE, "Ranger", 2);
        RegisterClassSpellAlias("bears_endurance_brd", SpellNames.BEARS_ENDURANCE, "Bard", 2);

        // Aliases — Bull's Strength: Cleric 2, Druid 2, Paladin 2, Bard 2
        RegisterClassSpellAlias("bulls_strength_clr", SpellNames.BULLS_STRENGTH, "Cleric", 2);
        RegisterClassSpellAlias("bulls_strength_drd", SpellNames.BULLS_STRENGTH, "Druid", 2);
        RegisterClassSpellAlias("bulls_strength_pal", SpellNames.BULLS_STRENGTH, "Paladin", 2);
        RegisterClassSpellAlias("bulls_strength_brd", SpellNames.BULLS_STRENGTH, "Bard", 2);

        // ── Phase 1: Bard/Paladin/Ranger/Druid class assignments ──

        // Barkskin: Druid 2 (already Ranger 2 via domain)
        RegisterClassSpellAlias("barkskin_drd", SpellNames.BARKSKIN, "Druid", 2);
        RegisterAlias("domain_barkskin", SpellNames.BARKSKIN);

        // Bless: Paladin 1
        RegisterClassSpellAlias("bless_pal", SpellNames.BLESS, "Paladin", 1);

        // Blur: Bard 2
        RegisterClassSpellAlias("blur_brd", SpellNames.BLUR, "Bard", 2);

    }
}

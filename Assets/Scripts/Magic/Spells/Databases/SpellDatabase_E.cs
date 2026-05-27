// ============================================================================
// SpellDatabase_E.cs — Spells starting with E
// Part of the SpellDatabase partial class.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

public static partial class SpellDatabase
{
    private static void RegisterSpellsE()
    {
        Register(new SpellData
                {
                    SpellId = SpellNames.EAGLES_SPLENDOR,
                    Name = "Eagle's Splendor",
                    Description = "Subject gains +4 enhancement bonus to CHA for 1 min/level. Affects Cha-based skills and Sorcerer/Bard/Paladin spell DCs. Does NOT grant bonus spells. PHB p.225",
                    SpellLevel = 2, School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "CHA",
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
                    SpellId = SpellNames.ENDURE_ELEMENTS,
                    Name = "Endure Elements",
                    Description = "Exist comfortably in hot or cold environments. Duration 24 hours. PHB p.226",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Wizard", "Cleric" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Touch,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Environmental protection not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.ENLARGE_PERSON,
                    Name = "Enlarge Person",
                    HasMaterialComponent = true, // M: powdered iron (common — covered by spell component pouch)
                    Description = "Humanoid creature doubles in size. +2 STR, -2 DEX, -1 AC/attack (size). Duration 1 min/level. Components: V, S, M (powdered iron). PHB p.226",
                    SpellLevel = 1, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.SingleAlly,
                    RangeCategory = SpellRangeCategory.Close,
                    EffectType = SpellEffectType.Buff,
                    BuffStatName = "STR",
                    BuffStatBonus = 2, // +2 size bonus to STR
                    BuffDurationRounds = 10, // Legacy fallback: 1 minute
                    BuffType = "enlarge",
                    BuffBonusType = BonusType.Size,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    AllowsSavingThrow = true, // Fortitude negates (unwilling)
                    SavingThrowType = "Fortitude",
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // ENTANGLE  (PHB p.227)
        // Transmutation
        // Level: Druid 1, Plant 1, Ranger 1
        // Casting Time: 1 standard action
        // Range: Long (400 ft. + 40 ft./level)
        // Area: Plants in a 40-ft.-radius spread
        // Duration: 1 min/level (D)
        // Saving Throw: Reflex partial; see text
        // Spell Resistance: No
        //
        // Grasses, weeds, and other plants entangle creatures.
        // Entangled: -2 attack, -4 Dex, can't move.
        // Break free: DC 20 Str or Escape Artist check.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.DOMAIN_ENTANGLE,
                    Name = "Entangle",
                    Description = "Plants entangle creatures in 40-ft radius. Reflex partial. Entangled: -2 attack, -4 Dex, can't move. Break free: DC 20 Str/Escape Artist. PHB p.227",
                    SpellLevel = 1,
                    School = "Transmutation",
                    ClassList = new string[] { "Cleric" },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Long,
                    AreaRadius = 8,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 8,
                    EffectType = SpellEffectType.Debuff,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.ENTHRALL,
                    Name = "Enthrall",
                    Description = "Captivates all within 100 ft + 10 ft/level. Will negates. Duration 1 hour or until distracted. PHB p.227",
                    SpellLevel = 2, School = "Enchantment",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 22,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Will",
                    BuffDurationRounds = -1,
                    ActionType = SpellActionType.FullRound,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Captivation not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.ENTROPIC_SHIELD,
                    Name = "Entropic Shield",
                    Description = "Ranged attacks against you have 20% miss chance. Duration 1 min/level. PHB p.227",
                    SpellLevel = 1, School = "Abjuration",
                    ClassList = new[] { "Cleric" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffDurationRounds = 10, // Legacy fallback: 1 minute
                    BuffType = "entropic",
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = false
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.ERASE,
                    Name = "Erase",
                    Description = "Mundane or magical writing vanishes. PHB p.227",
                    SpellLevel = 1, School = "Transmutation",
                    ClassList = new[] { "Wizard" },
                    TargetType = SpellTargetType.Self,
                    RangeSquares = 5,
                    EffectType = SpellEffectType.Buff,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    IsPlaceholder = true,
                    PlaceholderReason = "[PLACEHOLDER - Writing mechanics not implemented]"
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.ENERVATION,
                    Name = "Enervation",
                    Description = "Ranged touch attack bestows 1d4 negative levels. No save. Negative levels fade after CL hours. PHB p.226",
                    SpellLevel = 4, School = "Necromancy",
                    ClassList = new[] { "Wizard", "Sorcerer" },
                    TargetType = SpellTargetType.SingleEnemy,
                    RangeCategory = SpellRangeCategory.Close,
                    IsTouch = true,
                    IsRangedTouch = true,
                    EffectType = SpellEffectType.Debuff,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = true,
                    DurationType = DurationType.Hours,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        Register(new SpellData
                {
                    SpellId = SpellNames.EXPEDITIOUS_RETREAT,
                    Name = "Expeditious Retreat",
                    Description = "Your base land speed increases by +30 ft enhancement bonus. Duration 1 min/level (dismissible). PHB p.228",
                    SpellLevel = 1,
                    School = "Transmutation",
                    ClassList = new[] { "Wizard", "Sorcerer", "Bard" },
                    TargetType = SpellTargetType.Self,
                    RangeCategory = SpellRangeCategory.Personal,
                    EffectType = SpellEffectType.Buff,
                    BuffSpeedBonusFeet = 30,
                    BuffType = "enhancement",
                    BuffBonusType = BonusType.Enhancement,
                    BonusTypeExplicitlySet = true,
                    DurationType = DurationType.Minutes,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true
                });

        // ──────────────────────────────────────────────────────────────
        // EVARD'S BLACK TENTACLES  (PHB p.228)
        // Conjuration (Creation)
        // Level: Sor/Wiz 4
        // Components: V, S, M (a piece of tentacle from a giant octopus
        //   or giant squid)
        // Casting Time: 1 standard action
        // Range: Medium (100 ft. + 10 ft./level)
        // Area: 20-ft.-radius spread
        // Duration: 1 round/level (D)
        // Saving Throw: None
        // Spell Resistance: No
        //
        // This spell conjures a field of rubbery black tentacles, each 10
        // feet long. These waving members seem to spring forth from the
        // earth, floor, or whatever surface is underfoot. They grasp and
        // entwine around creatures that enter or are caught in the area.
        // Every creature within the area of the spell must make a grapple
        // check, opposed by the tentacles' grapple check. Treat the
        // tentacles attacking a particular target as a Large creature with
        // a base attack bonus equal to your caster level and a Strength
        // score of 19 (+4 modifier). Thus the tentacles' grapple check
        // modifier is equal to your caster level + 8.
        //
        // IMPLEMENTATION: AoE persistent area effect
        // (BlackTentaclesAreaEffect). Each round, tentacles grapple
        // check against all creatures in area. Grappled creatures take
        // 1d6+4 bludgeoning damage per round. Creatures can break free
        // with grapple or Escape Artist check vs tentacle grapple check.
        // ──────────────────────────────────────────────────────────────
        Register(new SpellData
                {
                    SpellId = SpellNames.EVARDS_BLACK_TENTACLES,
                    Name = "Evard's Black Tentacles",
                    Description = "Conjuration (Creation). A field of rubbery black tentacles fills a 20-ft radius spread. "
                        + "Tentacles grapple creatures in the area each round (grapple mod = CL + 8). "
                        + "Grappled creatures take 1d6+4 bludgeoning damage per round. "
                        + "Duration 1 round/level (D). Components: V, S, M (tentacle piece). PHB p.228",
                    HasMaterialComponent = true, // M: tentacle from giant octopus/squid (common — covered by spell component pouch)
                    SpellLevel = 4,
                    School = "Conjuration (Creation)",
                    AvailableFor = new List<SpellAvailability>
                    {
                        new SpellAvailability("Sorcerer", 4),
                        new SpellAvailability("Wizard", 4)
                    },
                    TargetType = SpellTargetType.Area,
                    RangeCategory = SpellRangeCategory.Medium,
                    AreaRadius = 4,                     // 20-ft radius = 4 squares
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 4,                 // 20-ft radius
                    AoEFilter = AoETargetFilter.All,
                    EffectType = SpellEffectType.Debuff,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = true,
                    IsDismissible = true,
                    AllowsSavingThrow = false,
                    SpellResistanceApplies = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    IsPlaceholder = false
                });

        // Aliases — Eagle's Splendor: Cleric 2, Paladin 2, Bard 2
        RegisterClassSpellAlias("eagles_splendor_clr", SpellNames.EAGLES_SPLENDOR, "Cleric", 2);
        RegisterClassSpellAlias("eagles_splendor_pal", SpellNames.EAGLES_SPLENDOR, "Paladin", 2);
        RegisterClassSpellAlias("eagles_splendor_brd", SpellNames.EAGLES_SPLENDOR, "Bard", 2);
        RegisterClassSpellAlias("endure_elements_clr", SpellNames.ENDURE_ELEMENTS, "Cleric", 1);

        // ── Phase 1: Bard/Paladin/Ranger/Druid class assignments ──

        // Endure Elements: Paladin 1, Ranger 1, Druid 1
        RegisterClassSpellAlias("endure_elements_pal", SpellNames.ENDURE_ELEMENTS, "Paladin", 1);
        RegisterClassSpellAlias("endure_elements_rgr", SpellNames.ENDURE_ELEMENTS, "Ranger", 1);
        RegisterClassSpellAlias("endure_elements_drd", SpellNames.ENDURE_ELEMENTS, "Druid", 1);

        // Entangle: Druid 1, Ranger 1
        RegisterClassSpellAlias("entangle_drd", SpellNames.DOMAIN_ENTANGLE, "Druid", 1);
        RegisterClassSpellAlias("entangle_rgr", SpellNames.DOMAIN_ENTANGLE, "Ranger", 1);

        // ── EARTHQUAKE — PHB p.225 ──
        Register(new SpellData
                {
                    SpellId = SpellNames.EARTHQUAKE,
                    Name = "Earthquake",
                    Description = "Evocation [Earth]. 80-ft-radius spread. Intense tremor shakes terrain. Creatures must succeed on Reflex save DC 15 or fall prone. Structures take massive damage. Duration: 1 round. Caves may collapse. PHB p.225",
                    SpellLevel = 8, School = "Evocation",
                    ClassList = new[] { "Cleric", "Druid" },
                    TargetType = SpellTargetType.Area,
                    AoEShapeType = AoEShape.Burst,
                    AoESizeSquares = 16,  // 80-ft radius
                    AoERangeSquares = 0,
                    AoEFilter = AoETargetFilter.All,
                    RangeCategory = SpellRangeCategory.Long,
                    EffectType = SpellEffectType.Control,
                    AllowsSavingThrow = true,
                    SavingThrowType = "Reflex",
                    SaveHalves = false,
                    SpellResistanceApplies = false,
                    DurationType = DurationType.Rounds,
                    DurationValue = 1,
                    DurationScalesWithLevel = false,
                    ActionType = SpellActionType.Standard,
                    ProvokesAoO = true,
                    HasVerbalComponent = true,
                    HasSomaticComponent = true,
                    HasMaterialComponent = true,
                    IsPlaceholder = false
                });

    }
}

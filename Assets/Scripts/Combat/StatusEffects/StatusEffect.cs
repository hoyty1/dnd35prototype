using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Condition stacking behavior.
/// </summary>
public enum ConditionStackingRule
{
    /// <summary>Only one instance can be active. Re-applying refreshes/replaces duration.</summary>
    Refresh,

    /// <summary>Multiple sources can coexist (prototype support; most core conditions use Refresh).</summary>
    StackBySource
}

/// <summary>
/// D&D 3.5 condition metadata used by combat/stat/action systems.
/// </summary>
public sealed class ConditionDefinition
{
    public CombatConditionType Type;
    public string DisplayName;
    public string ShortLabel;
    public string Description;
    public ConditionStackingRule StackingRule;

    // Core combat modifiers.
    public int AttackModifier;
    public int ArmorClassModifier;
    public int FortitudeModifier;
    public int ReflexModifier;
    public int WillModifier;
    public int InitiativeModifier;
    public int SkillCheckModifier;
    public int AbilityCheckModifier;
    /// <summary>Modifier on weapon damage rolls (Sickened -2, DMG p.301); read through CharacterStats.ConditionWeaponDamageModifier (CMB-003).</summary>
    public int WeaponDamageModifier;

    // Movement model.
    public bool PreventsMovement;
    public float MovementMultiplier;

    // Capability flags.
    public bool PreventsAoO;
    public bool PreventsThreatening;
    public bool PreventsStandardActions;
    public bool PreventsFullRoundActions;
    public bool PreventsSpellcasting;

    // Tactical/special-state markers.
    public bool IsFearCondition;
    public bool DeniesDexToAc;
    public bool GrantsCombatAdvantage;
    public bool CoupDeGraceVulnerable;

    // Defensive state (e.g., petrified hardness).
    public int Hardness;

    public ConditionDefinition CloneFor(CombatConditionType type)
    {
        return new ConditionDefinition
        {
            Type = type,
            DisplayName = DisplayName,
            ShortLabel = ShortLabel,
            Description = Description,
            StackingRule = StackingRule,
            AttackModifier = AttackModifier,
            ArmorClassModifier = ArmorClassModifier,
            FortitudeModifier = FortitudeModifier,
            ReflexModifier = ReflexModifier,
            WillModifier = WillModifier,
            InitiativeModifier = InitiativeModifier,
            SkillCheckModifier = SkillCheckModifier,
            AbilityCheckModifier = AbilityCheckModifier,
            WeaponDamageModifier = WeaponDamageModifier,
            PreventsMovement = PreventsMovement,
            MovementMultiplier = MovementMultiplier,
            PreventsAoO = PreventsAoO,
            PreventsThreatening = PreventsThreatening,
            PreventsStandardActions = PreventsStandardActions,
            PreventsFullRoundActions = PreventsFullRoundActions,
            PreventsSpellcasting = PreventsSpellcasting,
            IsFearCondition = IsFearCondition,
            DeniesDexToAc = DeniesDexToAc,
            GrantsCombatAdvantage = GrantsCombatAdvantage,
            CoupDeGraceVulnerable = CoupDeGraceVulnerable,
            Hardness = Hardness
        };
    }
}

/// <summary>
/// Centralized condition metadata and normalization utilities.
/// </summary>
public static class ConditionRules
{
    private static readonly Dictionary<CombatConditionType, ConditionDefinition> Definitions = BuildDefinitions();

    private static Dictionary<CombatConditionType, ConditionDefinition> BuildDefinitions()
    {
        var map = new Dictionary<CombatConditionType, ConditionDefinition>();

        void Add(ConditionDefinition def) => map[def.Type] = def;

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.None,
            DisplayName = "None",
            ShortLabel = "--",
            Description = "No condition.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Blinded,
            DisplayName = "Blinded",
            ShortLabel = "BL",
            Description = "Cannot see. -2 AC, -2 attack, loses DEX to AC, 50% miss chance on attacks, half speed.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            ArmorClassModifier = -2,
            DeniesDexToAc = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0.5f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.BlownAway,
            DisplayName = "Blown Away",
            ShortLabel = "BA",
            Description = "Overpowered by severe wind; knocked down and unable to act normally.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -4,
            ArmorClassModifier = -4,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Checked,
            DisplayName = "Checked",
            ShortLabel = "CK",
            Description = "Severe wind checks movement; creature cannot advance against the wind this round.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            PreventsMovement = true,
            MovementMultiplier = 0f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Confused,
            DisplayName = "Confused",
            ShortLabel = "CF",
            Description = "Acts unpredictably. Tactical behavior is AI-controlled.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            SkillCheckModifier = -2,
            PreventsAoO = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Cowering,
            DisplayName = "Cowering",
            ShortLabel = "CW",
            Description = "Frozen in fear. Takes no actions and suffers AC penalty.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = -2,
            DeniesDexToAc = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            IsFearCondition = true,
            MovementMultiplier = 0f,
            PreventsMovement = true,
            GrantsCombatAdvantage = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Dazed,
            DisplayName = "Dazed",
            ShortLabel = "DA",
            Description = "Can take no actions, including movement.",
            StackingRule = ConditionStackingRule.Refresh,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Dazzled,
            DisplayName = "Dazzled",
            ShortLabel = "DZ",
            Description = "-1 attack and sight-based checks.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -1,
            SkillCheckModifier = -1,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.HideousLaughter,
            DisplayName = "Hideous Laughter",
            ShortLabel = "HA",
            Description = "Laughing uncontrollably. Falls prone and cannot take actions, but is not helpless.",
            StackingRule = ConditionStackingRule.Refresh,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Dead,
            DisplayName = "Dead",
            ShortLabel = "DE",
            Description = "Dead creature; cannot act.",
            StackingRule = ConditionStackingRule.Refresh,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f,
            CoupDeGraceVulnerable = false
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Deafened,
            DisplayName = "Deafened",
            ShortLabel = "DF",
            Description = "Cannot hear. -4 initiative and some perception penalties.",
            StackingRule = ConditionStackingRule.Refresh,
            InitiativeModifier = -4,
            SkillCheckModifier = -2,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Disabled,
            DisplayName = "Disabled",
            ShortLabel = "DB",
            Description = "At 0 HP: one move or one standard action each turn.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Dying,
            DisplayName = "Dying",
            ShortLabel = "DY",
            Description = "At negative HP and losing life; unconscious/helpless.",
            StackingRule = ConditionStackingRule.Refresh,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f,
            GrantsCombatAdvantage = true,
            CoupDeGraceVulnerable = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.EnergyDrained,
            DisplayName = "Energy Drained",
            ShortLabel = "ED",
            Description = "Negative levels weaken all checks and combat output.",
            StackingRule = ConditionStackingRule.StackBySource,
            AttackModifier = -1,
            FortitudeModifier = -1,
            ReflexModifier = -1,
            WillModifier = -1,
            SkillCheckModifier = -1,
            AbilityCheckModifier = -1,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Entangled,
            DisplayName = "Entangled",
            ShortLabel = "EN",
            Description = "-2 attack, -4 DEX, moves at half speed, cannot run or charge. Somatic casting requires concentration (DC 15 + spell level).",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            PreventsMovement = false,
            MovementMultiplier = 0.5f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Exhausted,
            DisplayName = "Exhausted",
            ShortLabel = "EX",
            Description = "-6 STR/DEX equivalent penalties, half speed.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -3,
            ArmorClassModifier = -3,
            SkillCheckModifier = -3,
            AbilityCheckModifier = -3,
            MovementMultiplier = 0.5f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Fascinated,
            DisplayName = "Fascinated",
            ShortLabel = "FA",
            Description = "Pays attention to source and cannot take other actions.",
            StackingRule = ConditionStackingRule.Refresh,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f,
            PreventsMovement = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Charmed,
            DisplayName = "Charmed",
            ShortLabel = "CHM",
            Description = "Treats source as a trusted friend. Will not attack source and may assist them.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Asleep,
            DisplayName = "Asleep",
            ShortLabel = "Zzz",
            Description = "Sleeping state. Creature is unconscious and helpless until awakened or duration expires.",
            StackingRule = ConditionStackingRule.Refresh,
            DeniesDexToAc = true,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f,
            GrantsCombatAdvantage = true,
            CoupDeGraceVulnerable = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Fatigued,
            DisplayName = "Fatigued",
            ShortLabel = "FT",
            Description = "-2 STR/DEX equivalent penalties; cannot run or charge.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -1,
            ArmorClassModifier = -1,
            SkillCheckModifier = -1,
            AbilityCheckModifier = -1,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.FlatFooted,
            DisplayName = "Flat-Footed",
            ShortLabel = "FF",
            Description = "Denied DEX to AC and cannot make AoOs.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = -2,
            DeniesDexToAc = true,
            PreventsAoO = true,
            MovementMultiplier = 1f,
            GrantsCombatAdvantage = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Frightened,
            DisplayName = "Frightened",
            ShortLabel = "FR",
            Description = "-2 attacks/saves/checks; generally flees from source.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            FortitudeModifier = -2,
            ReflexModifier = -2,
            WillModifier = -2,
            SkillCheckModifier = -2,
            AbilityCheckModifier = -2,
            IsFearCondition = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Grappled,
            DisplayName = "Grappled",
            ShortLabel = "GR",
            Description = "-4 attack, no AoOs, no threatened squares, restricted actions.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -4,
            PreventsAoO = true,
            PreventsThreatening = true,
            SkillCheckModifier = -2,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Helpless,
            DisplayName = "Helpless",
            ShortLabel = "HP",
            Description = "Cannot defend, move, or act; loses DEX to AC and is vulnerable to coup de grace.",
            StackingRule = ConditionStackingRule.Refresh,
            DeniesDexToAc = true,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f,
            GrantsCombatAdvantage = true,
            CoupDeGraceVulnerable = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Incorporeal,
            DisplayName = "Incorporeal",
            ShortLabel = "IC",
            Description = "No physical body; special interaction with mundane attacks.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = 2,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Invisible,
            DisplayName = "Invisible",
            ShortLabel = "IV",
            Description = "Visibility-based combat advantages and concealment.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = 2,
            MovementMultiplier = 1f,
            GrantsCombatAdvantage = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Nauseated,
            DisplayName = "Nauseated",
            ShortLabel = "NA",
            Description = "Only a single move action allowed; no attacks or spellcasting.",
            StackingRule = ConditionStackingRule.Refresh,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Panicked,
            DisplayName = "Panicked",
            ShortLabel = "PN",
            Description = "Severe fear, drops items and flees; cannot attack or cast.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            FortitudeModifier = -2,
            ReflexModifier = -2,
            WillModifier = -2,
            SkillCheckModifier = -2,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            IsFearCondition = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Paralyzed,
            DisplayName = "Paralyzed",
            ShortLabel = "PA",
            Description = "Frozen and helpless; cannot move or act.",
            StackingRule = ConditionStackingRule.Refresh,
            DeniesDexToAc = true,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f,
            GrantsCombatAdvantage = true,
            CoupDeGraceVulnerable = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Petrified,
            DisplayName = "Petrified",
            ShortLabel = "PT",
            Description = "Turned to stone; inert and helpless. Hardness 8.",
            StackingRule = ConditionStackingRule.Refresh,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f,
            GrantsCombatAdvantage = true,
            CoupDeGraceVulnerable = true,
            Hardness = 8
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Pinned,
            DisplayName = "Pinned",
            ShortLabel = "PD",
            Description = "Immobilized in grapple; can usually only attempt escape.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -4,
            ArmorClassModifier = -4,
            DeniesDexToAc = true,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f,
            GrantsCombatAdvantage = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Prone,
            DisplayName = "Prone",
            ShortLabel = "PR",
            Description = "Grounded; melee/ranged modifiers handled in attack resolution.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Shaken,
            DisplayName = "Shaken",
            ShortLabel = "SH",
            Description = "-2 attack, saves, and checks.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            FortitudeModifier = -2,
            ReflexModifier = -2,
            WillModifier = -2,
            SkillCheckModifier = -2,
            IsFearCondition = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Sickened,
            DisplayName = "Sickened",
            ShortLabel = "SI",
            Description = "-2 attack, weapon damage, saves and skill checks.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            WeaponDamageModifier = -2, // DMG p.301 (CMB-003)
            FortitudeModifier = -2,
            ReflexModifier = -2,
            WillModifier = -2,
            SkillCheckModifier = -2,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Stable,
            DisplayName = "Stable",
            ShortLabel = "STB",
            Description = "At negative HP but no longer losing HP; unconscious.",
            StackingRule = ConditionStackingRule.Refresh,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f,
            GrantsCombatAdvantage = true,
            CoupDeGraceVulnerable = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Staggered,
            DisplayName = "Staggered",
            ShortLabel = "SG",
            Description = "Only one move action or one standard action each turn.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Stunned,
            DisplayName = "Stunned",
            ShortLabel = "ST",
            Description = "Drops held items, loses DEX to AC, cannot act.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = -2,
            DeniesDexToAc = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 1f,
            GrantsCombatAdvantage = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Turned,
            DisplayName = "Turned",
            ShortLabel = "TU",
            Description = "Repelled by divine power; must flee and cannot take offensive actions.",
            StackingRule = ConditionStackingRule.Refresh,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            IsFearCondition = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Unconscious,
            DisplayName = "Unconscious",
            ShortLabel = "UC",
            Description = "Unaware and helpless.",
            StackingRule = ConditionStackingRule.Refresh,
            DeniesDexToAc = true,
            PreventsMovement = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f,
            GrantsCombatAdvantage = true,
            CoupDeGraceVulnerable = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Blinking,
            DisplayName = "Blinking",
            ShortLabel = "BK",
            Description = "Rapidly shifting between Material and Ethereal Planes. 50% miss chance vs attacks (reduced by attacker capabilities). Own attacks have 20% miss chance but gain +2 and deny Dex to AC.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        // Existing project-specific states retained.
        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Poisoned,
            DisplayName = "Poisoned",
            ShortLabel = "PO",
            Description = "Afflicted by poison; exact penalties depend on poison source.",
            StackingRule = ConditionStackingRule.StackBySource,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Bleeding,
            DisplayName = "Bleeding",
            ShortLabel = "BLD",
            Description = "Loses HP at the start of each turn until bleeding is stopped.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Disarmed,
            DisplayName = "Disarmed",
            ShortLabel = "DS",
            Description = "Primary weapon lost.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -4,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Feinted,
            DisplayName = "Feinted",
            ShortLabel = "FE",
            Description = "Temporary DEX-denial marker from feint action.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f,
            DeniesDexToAc = true,
            GrantsCombatAdvantage = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.ChargePenalty,
            DisplayName = "Charge Penalty",
            ShortLabel = "CH",
            Description = "-2 AC until next turn start.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = -2,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Flanked,
            DisplayName = "Flanked",
            ShortLabel = "FL",
            // PHB p.153: only the flanking attackers get +2 on melee attack rolls
            // (CombatUtils.FlankingAttackBonus via IsAttackerFlanking). The defender takes
            // no AC penalty, so this condition is a display tag only (CMB-001).
            Description = "Display tag: flanking melee attackers get +2 to hit. No AC penalty.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = 0,
            MovementMultiplier = 1f,
            GrantsCombatAdvantage = true
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.LostShieldAC,
            DisplayName = "Lost Shield AC",
            ShortLabel = "LS",
            Description = "Shield bonus temporarily lost after shield bash.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        // Bestow Curse conditions
        Add(new ConditionDefinition
        {
            Type = CombatConditionType.BestowCurseGeneralPenalty,
            DisplayName = "Cursed (General)",
            ShortLabel = "CG",
            Description = "Bestow Curse: -4 on attacks, saves, ability checks, and skill checks.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -4,
            FortitudeModifier = -4,
            ReflexModifier = -4,
            WillModifier = -4,
            SkillCheckModifier = -4,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.BestowCurseActionLoss,
            DisplayName = "Cursed (Action Loss)",
            ShortLabel = "CA",
            Description = "Bestow Curse: 50% chance each turn to lose all actions.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        // Compatibility aliases.
        map[CombatConditionType.KnockedDown] = map[CombatConditionType.Prone].CloneFor(CombatConditionType.KnockedDown);
        map[CombatConditionType.Grappling] = map[CombatConditionType.Grappled].CloneFor(CombatConditionType.Grappling);

        return map;
    }

    public static CombatConditionType Normalize(CombatConditionType type)
    {
        switch (type)
        {
            case CombatConditionType.KnockedDown:
                return CombatConditionType.Prone;
            case CombatConditionType.Grappling:
                return CombatConditionType.Grappled;
            default:
                return type;
        }
    }

    public static ConditionDefinition GetDefinition(CombatConditionType type)
    {
        type = Normalize(type);
        if (Definitions.TryGetValue(type, out ConditionDefinition definition))
            return definition;

        return Definitions[CombatConditionType.None];
    }

    public static bool IsHelplessLike(CombatConditionType type)
    {
        ConditionDefinition def = GetDefinition(type);
        return def.CoupDeGraceVulnerable || Normalize(type) == CombatConditionType.Helpless;
    }
}

/// <summary>
/// Core condition effect entry.
/// </summary>
[Serializable]
public class StatusEffect
{
    public CombatConditionType Type;
    public string SourceName;
    public int RemainingRounds; // -1 = indefinite

    /// <summary>
    /// The creature whose initiative count was current when this condition was applied or last refreshed: its
    /// duration ticks at that count (PHB p.138, CMB-006; see <see cref="TurnDurations"/>). Null ticks at the round
    /// boundary.
    /// </summary>
    [NonSerialized] public CharacterController DurationAnchor;

    public StatusEffect(CombatConditionType type, string sourceName, int rounds)
    {
        Type = ConditionRules.Normalize(type);
        SourceName = sourceName ?? "Unknown";
        RemainingRounds = rounds;
        DurationAnchor = TurnDurations.CurrentAnchor;
    }

    /// <summary>
    /// Tick one round. Returns true if expired this tick. Called once per round, at the duration anchor's initiative
    /// count or, without one, at the round boundary.
    /// </summary>
    public bool Tick()
    {
        if (RemainingRounds < 0) return false;
        if (RemainingRounds <= 0) return true;
        RemainingRounds--;
        return RemainingRounds <= 0;
    }

    public string GetDurationLabel()
    {
        if (RemainingRounds < 0) return "∞";
        return $"{Mathf.Max(0, RemainingRounds)}rd";
    }

    public string GetDisplayString()
    {
        var def = ConditionRules.GetDefinition(Type);
        return $"{def.DisplayName}({GetDurationLabel()})";
    }
}

/// <summary>
/// Result payload for special maneuver checks.
/// </summary>
public class SpecialAttackResult
{
    public bool Success;
    public string ManeuverName;
    public string Log;
    public int CheckRoll;
    public int CheckTotal;
    public int OpposedRoll;
    public int OpposedTotal;
    public int DamageDealt;
    public bool ProvokedAoO;
    public bool TargetKilled;

    // Overrun-specific metadata.
    public bool DefenderAvoided;
    public bool AttackerActionConsumed = true;

    // Trip metadata (PHB p.158, p.96; CMB-079).
    /// <summary>A trip lost at the opposed check: the defender may try to trip the tripper back (PHB p.158). Never set after a free trip.</summary>
    public bool CounterTripAllowed;
    /// <summary>This result is a counter-trip (the defender's reaction after a failed trip), reported as a trip by the defender.</summary>
    public bool IsCounterTrip;
    /// <summary>
    /// A trip by an attacker with Improved Trip landed and its attack (PHB p.96) is still to be made:
    /// CharacterController.PrepareImprovedTripFollowUp sets it with the fields below, and
    /// GameManager.HandleTripAftermath makes the attack (CharacterController.ResolveImprovedTripFollowUp, which
    /// clears it) after the trip's log and melee reactions.
    /// </summary>
    public bool ImprovedTripFollowUpPending;
    /// <summary>
    /// Bonus of the attack step the trip replaced, a PC's two-weapon main-hand penalty included (null: the free
    /// trip's triggering hit, or full BAB).
    /// </summary>
    public int? FollowUpAttackBonus;
    /// <summary>The hit that triggered a free trip (MM trip), made again as the follow-up, or null.</summary>
    public CombatResult FollowUpTrigger;
    /// <summary>Natural-sequence index of the natural attack to make as the follow-up, or -1.</summary>
    public int FollowUpNaturalAttackIndex = -1;
    /// <summary>The Improved Trip attack made after a trip that landed (PHB p.96), or null.</summary>
    public CombatResult FollowUpAttack;
    /// <summary>Log label of <see cref="FollowUpAttack"/>.</summary>
    public string FollowUpLabel;
    /// <summary>Why an Improved Trip attack was not made after a trip that landed (out of reach, a ranged weapon), or null.</summary>
    public string FollowUpNote;

    // Coup de grace metadata (PHB p.153; CMB-004).
    /// <summary>A coup de grace's weapon damage: an automatic critical, the dice and static modifier rolled the multiplier number of times.</summary>
    public WeaponDamageRoll WeaponDamageRoll;
    /// <summary>Sneak attack damage a coup de grace added once (never multiplied, PHB p.140).</summary>
    public int SneakAttackDamage;
    /// <summary>
    /// A coup de grace's special-ability dice (energy, burst, alignment, bane) and specific-item damage, added once (never
    /// multiplied, PHB p.140).
    /// </summary>
    public int ExtraDamage;
    /// <summary>The target's Fortification negated the coup de grace's critical: the weapon damage was rolled once (DMG p.219).</summary>
    public bool CritNegated;
    /// <summary>
    /// A coup de grace's damage before mitigation: <see cref="WeaponDamageRoll"/> total + <see cref="SneakAttackDamage"/>
    /// + <see cref="ExtraDamage"/>.
    /// </summary>
    public int RawDamage;
}

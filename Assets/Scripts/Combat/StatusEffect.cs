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
            Description = "Severe wind checks movement and ranged attacks.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            MovementMultiplier = 0.5f
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
            Description = "Can take no actions.",
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
            Description = "-2 attack, -2 AC, half movement.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            ArmorClassModifier = -2,
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
            Description = "-2 attack, saves, checks, and damage-equivalent pressure.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
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
            Description = "Enemies gain flanking bonuses.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = -2,
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

    public StatusEffect(CombatConditionType type, string sourceName, int rounds)
    {
        Type = ConditionRules.Normalize(type);
        SourceName = sourceName ?? "Unknown";
        RemainingRounds = rounds;
    }

    /// <summary>
    /// Tick one round. Returns true if expired this tick.
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
}

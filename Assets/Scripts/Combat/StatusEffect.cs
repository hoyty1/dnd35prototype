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
/// D&D 3.5 style condition definition metadata.
///
/// IMPORTANT:
/// - We implement direct numeric effects that are already wired in combat math.
/// - Rule-heavy behavior (e.g., total action denial, precise movement limits, spellcasting limits)
///   is exposed via flags and hook stubs in ConditionManager for incremental rollout.
/// </summary>
public sealed class ConditionDefinition
{
    public CombatConditionType Type;
    public string DisplayName;
    public string ShortLabel;
    public string Description;
    public ConditionStackingRule StackingRule;

    // Numeric modifiers (applied generically in CharacterStats where appropriate)
    public int AttackModifier;
    public int ArmorClassModifier;
    public int FortitudeModifier;
    public int ReflexModifier;
    public int WillModifier;
    public int InitiativeModifier;

    // Movement model (generic).
    public bool PreventsMovement;
    public float MovementMultiplier;

    // Combat capability flags.
    public bool PreventsAoO;
    public bool PreventsThreatening;

    // Rule stub flags for higher-level behavior (action UI / spellcasting / AI).
    public bool PreventsStandardActions;
    public bool PreventsFullRoundActions;
    public bool PreventsSpellcasting;
    public bool IsFearCondition;

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
            PreventsMovement = PreventsMovement,
            MovementMultiplier = MovementMultiplier,
            PreventsAoO = PreventsAoO,
            PreventsThreatening = PreventsThreatening,
            PreventsStandardActions = PreventsStandardActions,
            PreventsFullRoundActions = PreventsFullRoundActions,
            PreventsSpellcasting = PreventsSpellcasting,
            IsFearCondition = IsFearCondition
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

        void Add(ConditionDefinition def)
        {
            map[def.Type] = def;
        }

        // Core baseline condition used for unknown/custom values.
        Add(new ConditionDefinition
        {
            Type = CombatConditionType.None,
            DisplayName = "None",
            ShortLabel = "--",
            Description = "No condition",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        // ===== PHB-style conditions (direct numeric effects + rule stubs) =====
        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Blinded,
            DisplayName = "Blinded",
            ShortLabel = "BL",
            Description = "Cannot see. Severe combat penalties.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            ArmorClassModifier = -2,
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
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Deafened,
            DisplayName = "Deafened",
            ShortLabel = "DF",
            Description = "Cannot hear; initiative and perception penalties (stubbed).",
            StackingRule = ConditionStackingRule.Refresh,
            InitiativeModifier = -4,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Entangled,
            DisplayName = "Entangled",
            ShortLabel = "EN",
            Description = "-2 attack, -4 DEX-like effects, half movement.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            ArmorClassModifier = -2,
            MovementMultiplier = 0.5f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Fatigued,
            DisplayName = "Fatigued",
            ShortLabel = "FT",
            Description = "Cannot charge/run; STR/DEX penalties handled in class systems.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f,
            IsFearCondition = false
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Exhausted,
            DisplayName = "Exhausted",
            ShortLabel = "EX",
            Description = "Severe fatigue; half speed and action limitations (stubbed).",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 0.5f,
            AttackModifier = -3,
            ArmorClassModifier = -3
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Shaken,
            DisplayName = "Shaken",
            ShortLabel = "SH",
            Description = "-2 attack, saves, checks.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            FortitudeModifier = -2,
            ReflexModifier = -2,
            WillModifier = -2,
            IsFearCondition = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Frightened,
            DisplayName = "Frightened",
            ShortLabel = "FR",
            Description = "Fear escalation from Shaken; includes movement behavior stub.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            FortitudeModifier = -2,
            ReflexModifier = -2,
            WillModifier = -2,
            IsFearCondition = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Panicked,
            DisplayName = "Panicked",
            ShortLabel = "PN",
            Description = "Severe fear; cannot perform meaningful actions (stubbed).",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            FortitudeModifier = -2,
            ReflexModifier = -2,
            WillModifier = -2,
            IsFearCondition = true,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Sickened,
            DisplayName = "Sickened",
            ShortLabel = "SI",
            Description = "-2 attack, weapon damage, saves, checks.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -2,
            FortitudeModifier = -2,
            ReflexModifier = -2,
            WillModifier = -2,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Nauseated,
            DisplayName = "Nauseated",
            ShortLabel = "NA",
            Description = "Cannot attack/cast/concentrate (stubbed).",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -10,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Dazed,
            DisplayName = "Dazed",
            ShortLabel = "DA",
            Description = "Can take no actions (stubbed action lock).",
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
            Type = CombatConditionType.Stunned,
            DisplayName = "Stunned",
            ShortLabel = "ST",
            Description = "Drops held items, cannot act, -2 AC.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = -2,
            PreventsStandardActions = true,
            PreventsFullRoundActions = true,
            PreventsSpellcasting = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Paralyzed,
            DisplayName = "Paralyzed",
            ShortLabel = "PA",
            Description = "Immobile and helpless (stubbed).",
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
            Type = CombatConditionType.Helpless,
            DisplayName = "Helpless",
            ShortLabel = "HP",
            Description = "Completely at opponent's mercy (stubbed coup-de-grace handling).",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = -4,
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
            Type = CombatConditionType.Unconscious,
            DisplayName = "Unconscious",
            ShortLabel = "UC",
            Description = "Unconscious and helpless. Cannot act or threaten.",
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
            Type = CombatConditionType.Disabled,
            DisplayName = "Disabled",
            ShortLabel = "DB",
            Description = "At 0 HP: can take only one move or one standard action.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Staggered,
            DisplayName = "Staggered",
            ShortLabel = "SG",
            Description = "Nonlethal damage equals current HP: can take one move or one standard action each turn.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Dying,
            DisplayName = "Dying",
            ShortLabel = "DY",
            Description = "At -1 to -9 HP, unconscious and losing 1 HP each turn.",
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
            Type = CombatConditionType.Stable,
            DisplayName = "Stable",
            ShortLabel = "STB",
            Description = "At negative HP, unconscious but no longer losing HP.",
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
            Type = CombatConditionType.Prone,
            DisplayName = "Prone",
            ShortLabel = "PR",
            Description = "Melee/ranged situational modifiers handled by attack resolution.",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Grappled,
            DisplayName = "Grappled",
            ShortLabel = "GR",
            Description = "-4 attack, no movement, no attacks of opportunity, no threatened squares; only light weapons or unarmed strikes.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -4,
            PreventsMovement = true,
            PreventsAoO = true,
            PreventsThreatening = true,
            MovementMultiplier = 0f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Pinned,
            DisplayName = "Pinned",
            ShortLabel = "PD",
            Description = "Held immobile in a grapple. Can only attempt to escape.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -4,
            ArmorClassModifier = -4,
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
            Type = CombatConditionType.Invisible,
            DisplayName = "Invisible",
            ShortLabel = "IV",
            Description = "Visibility benefits are mostly situational (stubbed for engine-wide handling).",
            StackingRule = ConditionStackingRule.Refresh,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.FlatFooted,
            DisplayName = "Flat-Footed",
            ShortLabel = "FF",
            Description = "Cannot use DEX to AC (stubbed via AC adjustment approximation).",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = -2,
            PreventsAoO = true,
            MovementMultiplier = 1f
        });

        // ===== Prototype-specific existing tactical states =====
        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Disarmed,
            DisplayName = "Disarmed",
            ShortLabel = "DS",
            Description = "Weapon unavailable; attack penalty approximated.",
            StackingRule = ConditionStackingRule.Refresh,
            AttackModifier = -4,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Feinted,
            DisplayName = "Feinted",
            ShortLabel = "FE",
            Description = "Legacy marker for feint state (no direct AC modifier; handled by feint attack logic).",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = 0,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.ChargePenalty,
            DisplayName = "Charge Penalty",
            ShortLabel = "CH",
            Description = "-2 AC until start of next turn.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = -2,
            MovementMultiplier = 1f
        });

        Add(new ConditionDefinition
        {
            Type = CombatConditionType.Flanked,
            DisplayName = "Flanked",
            ShortLabel = "FL",
            Description = "Tactical state; enemies gain flanking bonuses.",
            StackingRule = ConditionStackingRule.Refresh,
            ArmorClassModifier = -2,
            MovementMultiplier = 1f
        });

        // Compatibility alias (normalized to Prone)
        map[CombatConditionType.KnockedDown] = map[CombatConditionType.Prone].CloneFor(CombatConditionType.KnockedDown);

        return map;
    }

    public static CombatConditionType Normalize(CombatConditionType type)
    {
        switch (type)
        {
            case CombatConditionType.KnockedDown:
                return CombatConditionType.Prone;
            default:
                return type;
        }
    }

    public static ConditionDefinition GetDefinition(CombatConditionType type)
    {
        type = Normalize(type);
        if (Definitions.TryGetValue(type, out var definition))
            return definition;

        return Definitions[CombatConditionType.None];
    }
}

/// <summary>
/// Core non-spell combat condition effect entry.
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

public enum CombatConditionType
{
    None = 0,

    // PHB-style conditions
    Blinded,
    Dazzled,
    Deafened,
    Entangled,
    Fatigued,
    Exhausted,
    Shaken,
    Frightened,
    Panicked,
    Sickened,
    Nauseated,
    Dazed,
    Stunned,
    Paralyzed,
    Helpless,
    Unconscious,
    Disabled,
    Staggered,
    Dying,
    Stable,
    Prone,
    Grappled,
    Pinned,
    Invisible,
    FlatFooted,

    // Prototype tactical / legacy conditions
    Disarmed,
    Feinted,
    ChargePenalty,
    Flanked,

    // Normalized alias
    KnockedDown
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
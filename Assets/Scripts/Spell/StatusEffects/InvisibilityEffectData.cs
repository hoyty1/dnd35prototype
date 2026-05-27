using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// InvisibilityEffectData.cs — Reusable invisibility status effect system
//
// Designed to support all variants of invisibility in D&D 3.5e:
//   • Invisibility spell (PHB p.245) — breaks on attack/hostile action
//   • Greater Invisibility (future) — does NOT break on attack
//   • Improved Invisibility (future) — same as Greater Invisibility
//   • Magic items (e.g., Ring of Invisibility, Cloak of Elvenkind)
//   • Special abilities (e.g., Invisible Stalker, certain class features)
//
// Core PHB mechanics implemented:
//   • Total concealment (50% miss chance) against attackers
//   • Invisible attacker gets +2 bonus on attack rolls
//   • Opponents lose Dex bonus to AC against invisible attacker
//   • +20 Hide bonus while moving, +40 while stationary
//   • Breaks on attack/hostile spell (standard Invisibility only)
//   • Interaction with See Invisibility, True Seeing, Glitterdust
// ============================================================================

/// <summary>
/// Identifies the source category that created the invisibility effect.
/// Used to determine expiration rules, dispel interaction, and stacking behavior.
/// </summary>
public enum InvisibilitySourceType
{
    /// <summary>Standard spell-based invisibility (Invisibility, Greater Invisibility, etc.)</summary>
    Spell,
    /// <summary>Spell-like ability (e.g., Invisible Stalker innate ability)</summary>
    SpellLikeAbility,
    /// <summary>Magic item (e.g., Ring of Invisibility, Cloak of Elvenkind)</summary>
    MagicItem,
    /// <summary>Supernatural ability (e.g., Ghost's invisibility)</summary>
    Supernatural,
    /// <summary>Extraordinary ability (rare, e.g., certain creature abilities)</summary>
    Extraordinary
}

/// <summary>
/// Runtime metadata for an active invisibility effect.
/// This is a reusable data class that supports all variants of invisibility
/// across spells, magic items, and special abilities.
///
/// D&D 3.5e PHB Reference:
///   Invisibility: p.245
///   Greater Invisibility: p.245
///   See Invisibility: p.275
///   Concealment rules: p.152
///   Attack modifiers: p.141
/// </summary>
[System.Serializable]
public class InvisibilityEffectData
{
    // ======================== CORE STATE ========================

    /// <summary>Whether the subject is currently invisible.</summary>
    public bool IsInvisible;

    /// <summary>Remaining duration in combat rounds. -1 = permanent/until dismissed.</summary>
    public int DurationRemainingRounds;

    /// <summary>Whether the invisible subject is currently moving (affects Hide bonus).</summary>
    public bool IsMoving;

    // ======================== VARIANT CONFIGURATION ========================

    /// <summary>
    /// If true, the invisibility effect ends when the subject makes an attack roll
    /// or casts an offensive spell. Standard Invisibility = true, Greater Invisibility = false.
    /// </summary>
    public bool BreaksOnAttack = true;

    /// <summary>
    /// If true, the effect can be dismissed as a standard action by the caster/subject.
    /// Most spell-based invisibility is dismissible.
    /// </summary>
    public bool IsDismissible = true;

    /// <summary>
    /// Concealment miss chance percentage. 50 = total concealment (standard).
    /// Could be modified by special conditions or partial invisibility variants.
    /// </summary>
    public int ConcealmentMissChance = 50;

    /// <summary>
    /// Hide bonus while moving (+20 standard for Invisibility).
    /// </summary>
    public int HideBonusMoving = 20;

    /// <summary>
    /// Hide bonus while stationary (+40 standard for Invisibility).
    /// </summary>
    public int HideBonusStationary = 40;

    // ======================== SOURCE TRACKING ========================

    /// <summary>
    /// The source type of this invisibility effect (spell, item, ability, etc.).
    /// Used for dispel interaction and rules resolution.
    /// </summary>
    public InvisibilitySourceType SourceType = InvisibilitySourceType.Spell;

    /// <summary>
    /// The spell ID that created this effect (e.g., "invisibility", "greater_invisibility").
    /// Empty/null for non-spell sources.
    /// </summary>
    public string SourceSpellId;

    /// <summary>
    /// Human-readable name of the source (e.g., "Invisibility", "Ring of Invisibility").
    /// Used in combat log messages.
    /// </summary>
    public string SourceName;

    // ======================== CASTER TRACKING ========================

    /// <summary>Runtime reference to the caster (not serialized).</summary>
    [System.NonSerialized] public CharacterController Caster;

    /// <summary>Serializable caster name for persistence.</summary>
    public string CasterName;

    // ======================== METHODS ========================

    /// <summary>
    /// Sets the caster reference and serializable name.
    /// </summary>
    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }

    /// <summary>
    /// Returns the appropriate Hide bonus based on current movement state.
    /// PHB p.245: +20 on Hide checks while moving, +40 while stationary.
    /// </summary>
    public int GetCurrentHideBonus()
    {
        if (!IsInvisible) return 0;
        return IsMoving ? HideBonusMoving : HideBonusStationary;
    }

    /// <summary>
    /// Returns the attack roll bonus an invisible attacker receives.
    /// PHB p.141: Invisible attacker gains +2 on attack rolls.
    /// </summary>
    public int GetAttackBonus()
    {
        return IsInvisible ? 2 : 0;
    }

    /// <summary>
    /// Returns true if the invisibility effect should be tracked by
    /// the StatusEffectManager under the given spell ID.
    /// </summary>
    public bool MatchesSpellId(string spellId)
    {
        if (string.IsNullOrEmpty(SourceSpellId) || string.IsNullOrEmpty(spellId))
            return false;
        return string.Equals(SourceSpellId, spellId, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true if this is a standard Invisibility spell effect
    /// (as opposed to Greater Invisibility, magic item, etc.).
    /// </summary>
    public bool IsStandardInvisibility =>
        SourceType == InvisibilitySourceType.Spell
        && string.Equals(SourceSpellId, SpellNames.INVISIBILITY, System.StringComparison.Ordinal);

    /// <summary>
    /// Returns true if this invisibility comes from any spell-like source
    /// (spells or spell-like abilities) as opposed to items/supernatural.
    /// </summary>
    public bool IsSpellBased =>
        SourceType == InvisibilitySourceType.Spell
        || SourceType == InvisibilitySourceType.SpellLikeAbility;

    /// <summary>
    /// Factory: Creates a standard Invisibility spell effect (PHB p.245).
    /// Breaks on attack, 50% miss chance, 1 min/level duration, dismissible.
    /// </summary>
    public static InvisibilityEffectData CreateStandardInvisibility(int durationRounds, CharacterController caster)
    {
        var data = new InvisibilityEffectData
        {
            IsInvisible = true,
            DurationRemainingRounds = durationRounds,
            IsMoving = false,
            BreaksOnAttack = true,
            IsDismissible = true,
            ConcealmentMissChance = 50,
            HideBonusMoving = 20,
            HideBonusStationary = 40,
            SourceType = InvisibilitySourceType.Spell,
            SourceSpellId = SpellNames.INVISIBILITY,
            SourceName = "Invisibility"
        };
        data.SetCaster(caster);
        return data;
    }

    /// <summary>
    /// Factory: Creates a Greater Invisibility spell effect (PHB p.245).
    /// Does NOT break on attack, 1 round/level duration, dismissible.
    /// </summary>
    public static InvisibilityEffectData CreateGreaterInvisibility(int durationRounds, CharacterController caster)
    {
        var data = new InvisibilityEffectData
        {
            IsInvisible = true,
            DurationRemainingRounds = durationRounds,
            IsMoving = false,
            BreaksOnAttack = false, // Key difference from standard Invisibility
            IsDismissible = true,
            ConcealmentMissChance = 50,
            HideBonusMoving = 20,
            HideBonusStationary = 40,
            SourceType = InvisibilitySourceType.Spell,
            SourceSpellId = "greater_invisibility",
            SourceName = "Greater Invisibility"
        };
        data.SetCaster(caster);
        return data;
    }

    /// <summary>
    /// Factory: Creates an invisibility effect from a magic item source.
    /// </summary>
    public static InvisibilityEffectData CreateFromMagicItem(string itemName, bool breaksOnAttack, int durationRounds = -1)
    {
        return new InvisibilityEffectData
        {
            IsInvisible = true,
            DurationRemainingRounds = durationRounds,
            IsMoving = false,
            BreaksOnAttack = breaksOnAttack,
            IsDismissible = true,
            ConcealmentMissChance = 50,
            HideBonusMoving = 20,
            HideBonusStationary = 40,
            SourceType = InvisibilitySourceType.MagicItem,
            SourceSpellId = string.Empty,
            SourceName = itemName ?? "Magic Item"
        };
    }

    /// <summary>
    /// Factory: Creates an invisibility effect from a special ability.
    /// </summary>
    public static InvisibilityEffectData CreateFromAbility(string abilityName, InvisibilitySourceType sourceType,
        bool breaksOnAttack, int durationRounds = -1)
    {
        return new InvisibilityEffectData
        {
            IsInvisible = true,
            DurationRemainingRounds = durationRounds,
            IsMoving = false,
            BreaksOnAttack = breaksOnAttack,
            IsDismissible = false,
            ConcealmentMissChance = 50,
            HideBonusMoving = 20,
            HideBonusStationary = 40,
            SourceType = sourceType,
            SourceSpellId = string.Empty,
            SourceName = abilityName ?? "Special Ability"
        };
    }
}

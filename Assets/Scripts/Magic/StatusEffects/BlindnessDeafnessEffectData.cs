using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// BlindnessDeafnessEffectData.cs — Reusable blindness/deafness effect system
//
// Designed to support all sources of blindness/deafness in D&D 3.5e:
//   • Blindness/Deafness spell (PHB p.206) — permanent until removed
//   • Glitterdust blindness (already handled separately)
//   • Poison, disease, or other affliction sources
//   • Magic items or special abilities
//
// Blind condition mechanics (PHB):
//   • -2 AC penalty
//   • Lose Dex bonus to AC
//   • 50% miss chance on all attacks (total concealment)
//   • Move at half speed
//   • -4 penalty to Search and Str/Dex-based checks
//   • Auto-fail Spot and vision-based checks
//
// Deaf condition mechanics (PHB):
//   • -4 initiative penalty
//   • 20% spell failure for spells with verbal components
//   • Auto-fail Listen checks
//   • Immune to sonic/language-dependent effects
// ============================================================================

/// <summary>
/// Identifies the affliction type for a BlindnessDeafnessEffectData.
/// </summary>
public enum BlindDeafType
{
    /// <summary>The creature is blinded.</summary>
    Blindness,
    /// <summary>The creature is deafened.</summary>
    Deafness
}

/// <summary>
/// Identifies the source category that created the blindness/deafness effect.
/// Used to determine removal rules and dispel interaction.
/// </summary>
public enum BlindDeafSourceType
{
    /// <summary>Standard spell-based effect (Blindness/Deafness spell).</summary>
    Spell,
    /// <summary>Spell-like ability (e.g., innate creature ability).</summary>
    SpellLikeAbility,
    /// <summary>Poison or disease source.</summary>
    PoisonOrDisease,
    /// <summary>Magic item effect.</summary>
    MagicItem,
    /// <summary>Supernatural ability.</summary>
    Supernatural,
    /// <summary>Extraordinary ability (e.g., physical damage to eyes).</summary>
    Extraordinary
}

/// <summary>
/// Runtime metadata for an active blindness or deafness effect.
/// This is a reusable data class that supports all variants of blindness/deafness
/// across spells, poisons, diseases, and special abilities.
///
/// D&D 3.5e PHB Reference:
///   Blindness/Deafness spell: p.206
///   Blinded condition: p.305 (Glossary)
///   Deafened condition: p.307 (Glossary)
///   Concealment rules: p.152
/// </summary>
[System.Serializable]
public class BlindnessDeafnessEffectData
{
    // ======================== CORE STATE ========================

    /// <summary>Whether this effect applies blindness or deafness.</summary>
    public BlindDeafType AfflictionType;

    /// <summary>Whether the effect is currently active.</summary>
    public bool IsActive = true;

    /// <summary>
    /// Remaining duration in combat rounds. -1 = permanent (until magically removed).
    /// The Blindness/Deafness spell has permanent duration per PHB p.206.
    /// </summary>
    public int DurationRemainingRounds = -1;

    // ======================== VARIANT CONFIGURATION ========================

    /// <summary>
    /// If true, the effect can be dismissed as a standard action by the caster.
    /// PHB p.206: Blindness/Deafness is dismissible (D).
    /// </summary>
    public bool IsDismissible = true;

    /// <summary>
    /// If true, this effect is permanent until magically cured (Remove Blindness/Deafness, etc.).
    /// </summary>
    public bool IsPermanent = true;

    // ======================== SOURCE TRACKING ========================

    /// <summary>
    /// The source type of this effect (spell, poison, item, ability, etc.).
    /// Used for dispel interaction and rules resolution.
    /// </summary>
    public BlindDeafSourceType SourceType = BlindDeafSourceType.Spell;

    /// <summary>
    /// The spell ID that created this effect (e.g., "blindness_deafness_wiz").
    /// Empty/null for non-spell sources.
    /// </summary>
    public string SourceSpellId;

    /// <summary>
    /// Human-readable name of the source (e.g., "Blindness/Deafness", "Poisoned Dart").
    /// Used in combat log messages.
    /// </summary>
    public string SourceName;

    /// <summary>
    /// The caster level of the source effect (used for dispel checks).
    /// </summary>
    public int CasterLevel;

    // ======================== CASTER TRACKING ========================

    /// <summary>Runtime reference to the caster (not serialized).</summary>
    [System.NonSerialized] public CharacterController Caster;

    /// <summary>Serializable caster name for persistence.</summary>
    public string CasterName;

    // ======================== QUERY METHODS ========================

    /// <summary>Returns true if this effect causes blindness.</summary>
    public bool IsBlindness => AfflictionType == BlindDeafType.Blindness && IsActive;

    /// <summary>Returns true if this effect causes deafness.</summary>
    public bool IsDeafness => AfflictionType == BlindDeafType.Deafness && IsActive;

    /// <summary>
    /// Returns true if this effect comes from any spell-like source
    /// (spells or spell-like abilities) as opposed to items/supernatural.
    /// </summary>
    public bool IsSpellBased =>
        SourceType == BlindDeafSourceType.Spell
        || SourceType == BlindDeafSourceType.SpellLikeAbility;

    /// <summary>
    /// Returns the AC penalty for a blinded creature.
    /// PHB p.305: Blinded creatures take a -2 penalty to AC.
    /// </summary>
    public int GetACPenalty()
    {
        return IsBlindness ? -2 : 0;
    }

    /// <summary>
    /// Returns true if this effect denies Dex bonus to AC.
    /// PHB p.305: Blinded creatures lose their Dex bonus to AC.
    /// </summary>
    public bool DeniesDexToAC()
    {
        return IsBlindness;
    }

    /// <summary>
    /// Returns the miss chance percentage for a blinded attacker.
    /// PHB p.305: 50% miss chance (total concealment) on all attacks.
    /// </summary>
    public int GetAttackMissChance()
    {
        return IsBlindness ? 50 : 0;
    }

    /// <summary>
    /// Returns the movement speed multiplier for a blinded creature.
    /// PHB p.305: Half speed when blinded.
    /// </summary>
    public float GetMovementMultiplier()
    {
        return IsBlindness ? 0.5f : 1.0f;
    }

    /// <summary>
    /// Returns the initiative penalty for a deafened creature.
    /// PHB p.307: -4 penalty to initiative checks.
    /// </summary>
    public int GetInitiativePenalty()
    {
        return IsDeafness ? -4 : 0;
    }

    /// <summary>
    /// Returns the spell failure chance for a deafened creature casting spells with verbal components.
    /// PHB p.307: 20% chance of spell failure for spells with verbal components.
    /// </summary>
    public int GetVerbalSpellFailureChance()
    {
        return IsDeafness ? 20 : 0;
    }

    /// <summary>
    /// Returns the Str/Dex skill check penalty for a blinded creature.
    /// PHB p.305: -4 penalty to Search checks and most Str/Dex-based skill checks.
    /// </summary>
    public int GetSkillCheckPenalty()
    {
        return IsBlindness ? -4 : 0;
    }

    /// <summary>
    /// Sets the caster reference and serializable name.
    /// </summary>
    public void SetCaster(CharacterController caster)
    {
        Caster = caster;
        CasterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : string.Empty;
    }

    /// <summary>
    /// Returns true if this effect matches the given spell ID.
    /// </summary>
    public bool MatchesSpellId(string spellId)
    {
        if (string.IsNullOrEmpty(SourceSpellId) || string.IsNullOrEmpty(spellId))
            return false;
        return string.Equals(SourceSpellId, spellId, System.StringComparison.Ordinal);
    }

    // ======================== FACTORY METHODS ========================

    /// <summary>
    /// Factory: Creates a Blindness effect from the Blindness/Deafness spell (PHB p.206).
    /// Permanent duration, dismissible by caster.
    /// </summary>
    public static BlindnessDeafnessEffectData CreateSpellBlindness(string sourceSpellId, CharacterController caster, int casterLevel)
    {
        var data = new BlindnessDeafnessEffectData
        {
            AfflictionType = BlindDeafType.Blindness,
            IsActive = true,
            DurationRemainingRounds = -1, // Permanent
            IsDismissible = true,
            IsPermanent = true,
            SourceType = BlindDeafSourceType.Spell,
            SourceSpellId = sourceSpellId,
            SourceName = "Blindness/Deafness",
            CasterLevel = casterLevel
        };
        data.SetCaster(caster);
        return data;
    }

    /// <summary>
    /// Factory: Creates a Deafness effect from the Blindness/Deafness spell (PHB p.206).
    /// Permanent duration, dismissible by caster.
    /// </summary>
    public static BlindnessDeafnessEffectData CreateSpellDeafness(string sourceSpellId, CharacterController caster, int casterLevel)
    {
        var data = new BlindnessDeafnessEffectData
        {
            AfflictionType = BlindDeafType.Deafness,
            IsActive = true,
            DurationRemainingRounds = -1, // Permanent
            IsDismissible = true,
            IsPermanent = true,
            SourceType = BlindDeafSourceType.Spell,
            SourceSpellId = sourceSpellId,
            SourceName = "Blindness/Deafness",
            CasterLevel = casterLevel
        };
        data.SetCaster(caster);
        return data;
    }

    /// <summary>
    /// Factory: Creates a blindness/deafness effect from a non-spell source
    /// (poison, disease, ability, etc.).
    /// </summary>
    public static BlindnessDeafnessEffectData CreateFromSource(
        BlindDeafType type,
        BlindDeafSourceType sourceType,
        string sourceName,
        int durationRounds = -1,
        bool isDismissible = false)
    {
        return new BlindnessDeafnessEffectData
        {
            AfflictionType = type,
            IsActive = true,
            DurationRemainingRounds = durationRounds,
            IsDismissible = isDismissible,
            IsPermanent = durationRounds == -1,
            SourceType = sourceType,
            SourceSpellId = string.Empty,
            SourceName = sourceName ?? "Unknown Source",
            CasterLevel = 0
        };
    }
}

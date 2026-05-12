using UnityEngine;
using DND35e.Identifiers;

// ============================================================================
// AttributeEnhancementEffectData.cs — Unified system for attribute enhancement spells
//
// D&D 3.5e PHB: Bear's Endurance, Bull's Strength, Cat's Grace,
//               Eagle's Splendor, Fox's Cunning, Owl's Wisdom
//
// All six spells grant a +4 enhancement bonus to one ability score for
// 1 min/level. Enhancement bonuses of the same type to the same ability
// score do NOT stack — only the highest applies.
//
// Bear's Endurance special: The CON increase grants +2 HP per Hit Die.
// These are real HP (not temporary). When the spell ends, those HP are
// removed — if this drops the creature to 0 or below, normal dying/death
// rules apply.
// ============================================================================

/// <summary>
/// Runtime data for an active attribute enhancement spell effect on a character.
/// Tracks which ability score is enhanced, the bonus amount, duration, and
/// (for Bear's Endurance) the HP bonus granted.
/// </summary>
[System.Serializable]
public class AttributeEnhancementEffectData
{
    // ======================== CORE STATE ========================

    /// <summary>Which ability score receives the enhancement bonus.</summary>
    public AbilityType EnhancedAbility;

    /// <summary>The enhancement bonus amount (typically +4).</summary>
    public int BonusAmount;

    /// <summary>Caster level at time of casting.</summary>
    public int CasterLevel;

    /// <summary>Remaining duration in combat rounds. -1 = permanent/indefinite.</summary>
    public int DurationRemainingRounds;

    /// <summary>Whether the effect is currently active.</summary>
    public bool IsActive;

    /// <summary>
    /// For Bear's Endurance: The total HP bonus granted (2 per HD).
    /// Added to both current and max HP. On removal, subtracted from both.
    /// Zero for all other enhancement spells.
    /// </summary>
    public int GrantedBonusHP;

    // ======================== SOURCE TRACKING ========================

    /// <summary>The spell ID that created this effect.</summary>
    public string SourceSpellId;

    /// <summary>Human-readable name of the source spell.</summary>
    public string SourceName;

    /// <summary>Name of the caster who applied this effect.</summary>
    public string CasterName;

    /// <summary>Runtime reference to the caster (not serialized).</summary>
    [System.NonSerialized] public CharacterController Caster;

    // ======================== QUERIES ========================

    /// <summary>Whether this is a Bear's Endurance effect (CON enhancement with HP grant).</summary>
    public bool IsBearsEndurance => EnhancedAbility == AbilityType.CON &&
                                    SourceSpellId == SpellNames.BEARS_ENDURANCE;

    /// <summary>The modifier change from the enhancement bonus (+4 score = +2 modifier).</summary>
    public int ModifierChange => BonusAmount / 2;

    /// <summary>Get the ability score name as a string (STR, DEX, CON, etc.).</summary>
    public string AbilityName => EnhancedAbility.ToString();

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
    /// Expire the effect due to duration ending or dispel.
    /// </summary>
    public void Expire(string reason)
    {
        if (!IsActive) return;
        IsActive = false;
        Debug.Log($"[AttributeEnhancement] {SourceName} on {CasterName}: expired — {reason}");
    }

    // ======================== STATIC HELPERS ========================

    /// <summary>
    /// Calculate Bear's Endurance HP bonus: 2 HP per Hit Die.
    /// </summary>
    public static int CalculateBearsEnduranceHP(int hitDice)
    {
        return Mathf.Max(0, hitDice * 2);
    }

    /// <summary>
    /// Get the ability type targeted by a given spell ID.
    /// Returns AbilityType.STR as default if spell ID is not recognized.
    /// </summary>
    public static AbilityType GetAbilityForSpell(string spellId)
    {
        if (spellId == SpellNames.BEARS_ENDURANCE) return AbilityType.CON;
        if (spellId == SpellNames.BULLS_STRENGTH) return AbilityType.STR;
        if (spellId == SpellNames.CATS_GRACE) return AbilityType.DEX;
        if (spellId == SpellNames.EAGLES_SPLENDOR) return AbilityType.CHA;
        if (spellId == SpellNames.FOXS_CUNNING) return AbilityType.INT;
        if (spellId == SpellNames.OWLS_WISDOM) return AbilityType.WIS;
        return AbilityType.STR; // fallback
    }

    /// <summary>
    /// Get the spell display name for a given spell ID.
    /// </summary>
    public static string GetSpellDisplayName(string spellId)
    {
        if (spellId == SpellNames.BEARS_ENDURANCE) return "Bear's Endurance";
        if (spellId == SpellNames.BULLS_STRENGTH) return "Bull's Strength";
        if (spellId == SpellNames.CATS_GRACE) return "Cat's Grace";
        if (spellId == SpellNames.EAGLES_SPLENDOR) return "Eagle's Splendor";
        if (spellId == SpellNames.FOXS_CUNNING) return "Fox's Cunning";
        if (spellId == SpellNames.OWLS_WISDOM) return "Owl's Wisdom";
        return "Unknown Enhancement";
    }

    /// <summary>
    /// Check if a spell ID is one of the 6 attribute enhancement spells.
    /// </summary>
    public static bool IsAttributeEnhancementSpell(string spellId)
    {
        return spellId == SpellNames.BEARS_ENDURANCE ||
               spellId == SpellNames.BULLS_STRENGTH ||
               spellId == SpellNames.CATS_GRACE ||
               spellId == SpellNames.EAGLES_SPLENDOR ||
               spellId == SpellNames.FOXS_CUNNING ||
               spellId == SpellNames.OWLS_WISDOM;
    }

    // ======================== FACTORY METHODS ========================

    /// <summary>
    /// Factory: Creates an attribute enhancement effect for any of the 6 spells.
    /// For Bear's Endurance, calculates HP bonus based on Hit Dice.
    /// </summary>
    public static AttributeEnhancementEffectData Create(string spellId, int casterLevel,
        int targetHitDice, CharacterController caster)
    {
        AbilityType ability = GetAbilityForSpell(spellId);
        string displayName = GetSpellDisplayName(spellId);
        int durationRounds = casterLevel * 10; // 1 min/level = 10 rounds/level

        int bonusHP = 0;
        if (spellId == SpellNames.BEARS_ENDURANCE)
        {
            bonusHP = CalculateBearsEnduranceHP(targetHitDice);
        }

        var data = new AttributeEnhancementEffectData
        {
            EnhancedAbility = ability,
            BonusAmount = 4, // All 6 spells grant +4
            CasterLevel = casterLevel,
            DurationRemainingRounds = durationRounds,
            IsActive = true,
            GrantedBonusHP = bonusHP,
            SourceSpellId = spellId,
            SourceName = displayName
        };
        data.SetCaster(caster);
        return data;
    }

    /// <summary>
    /// Factory: Creates an attribute enhancement effect with a specific bonus amount (for testing).
    /// </summary>
    public static AttributeEnhancementEffectData CreateWithBonus(string spellId, int bonusAmount,
        int casterLevel, int targetHitDice, CharacterController caster)
    {
        var data = Create(spellId, casterLevel, targetHitDice, caster);
        data.BonusAmount = bonusAmount;
        return data;
    }
}

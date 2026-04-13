using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages active spell effects on a single character.
/// Tracks durations, handles effect application/removal, and enforces D&D 3.5e stacking rules.
///
/// D&D 3.5e Stacking Rules (PHB p.177):
///   - Most bonuses of the same TYPE do not stack (only the highest applies).
///   - Dodge bonuses stack. Circumstance bonuses stack.
///   - Untyped bonuses stack.
///   - Penalties always stack.
///   - Same spell from multiple casters: only the best applies (no stacking).
///
/// This component is attached to each character GameObject alongside CharacterController.
/// </summary>
public class StatusEffectManager : MonoBehaviour
{
    /// <summary>List of all active spell effects on this character.</summary>
    public List<ActiveSpellEffect> ActiveEffects { get; private set; } = new List<ActiveSpellEffect>();

    /// <summary>Reference to the character's stats for applying/removing modifications.</summary>
    private CharacterStats _stats;

    /// <summary>Reference to the character's spellcasting component (may be null for non-casters).</summary>
    private SpellcastingComponent _spellComp;

    /// <summary>Initialize with character references.</summary>
    public void Init(CharacterStats stats)
    {
        _stats = stats;
        _spellComp = GetComponent<SpellcastingComponent>();
    }

    /// <summary>
    /// Add a new spell effect to this character.
    /// Handles D&D 3.5e stacking rules: same spell doesn't stack (replaces with longer duration),
    /// same bonus type doesn't stack (only highest applies).
    /// Returns the created ActiveSpellEffect, or null if the effect was suppressed.
    /// </summary>
    public ActiveSpellEffect AddEffect(SpellData spell, string casterName, int casterLevel)
    {
        if (spell == null || _stats == null) return null;

        var effect = new ActiveSpellEffect(spell, casterName, casterLevel, _stats.CharacterName);

        // Check for same spell already active (D&D 3.5e: same spell doesn't stack, use longer duration)
        var existingSameSpell = ActiveEffects.FirstOrDefault(e => e.Spell.SpellId == spell.SpellId);
        if (existingSameSpell != null)
        {
            // If new duration is longer, replace; otherwise ignore
            if (effect.RemainingRounds > existingSameSpell.RemainingRounds ||
                (effect.RemainingRounds == -1 && existingSameSpell.RemainingRounds != -1))
            {
                Debug.Log($"[StatusEffect] {_stats.CharacterName}: Replacing {spell.Name} with longer duration " +
                          $"({existingSameSpell.RemainingRounds} → {effect.RemainingRounds} rounds)");
                RemoveEffect(existingSameSpell);
            }
            else
            {
                Debug.Log($"[StatusEffect] {_stats.CharacterName}: {spell.Name} already active with equal/longer duration, ignoring");
                return null;
            }
        }

        // Check for same bonus type stacking (D&D 3.5e: most typed bonuses don't stack)
        if (!string.IsNullOrEmpty(spell.BuffType) && spell.BuffType != "untyped" && spell.BuffType != "dodge")
        {
            var existingSameType = ActiveEffects.Where(e =>
                e.BonusType == spell.BuffType && e.Spell.SpellId != spell.SpellId).ToList();

            foreach (var existing in existingSameType)
            {
                // For same bonus type, check if the new effect is stronger
                // Compare the primary bonus value
                int existingPower = GetEffectPower(existing);
                int newPower = GetEffectPowerFromSpell(spell);

                if (newPower <= existingPower)
                {
                    Debug.Log($"[StatusEffect] {_stats.CharacterName}: {spell.Name} ({spell.BuffType} +{newPower}) " +
                              $"suppressed by {existing.Spell.Name} ({existing.BonusType} +{existingPower})");
                    return null; // Don't apply, existing is equal or better
                }
                else
                {
                    // New is stronger, remove old
                    Debug.Log($"[StatusEffect] {_stats.CharacterName}: {spell.Name} ({spell.BuffType} +{newPower}) " +
                              $"replaces {existing.Spell.Name} ({existing.BonusType} +{existingPower})");
                    RemoveEffect(existing);
                }
            }
        }

        // Store the stat modifications that will be applied
        effect.AppliedAttackBonus = spell.BuffAttackBonus;
        effect.AppliedDamageBonus = spell.BuffDamageBonus;
        effect.AppliedSaveBonus = spell.BuffSaveBonus;
        effect.AppliedACBonus = spell.BuffACBonus;
        effect.AppliedShieldBonus = spell.BuffShieldBonus;
        effect.AppliedDeflectionBonus = spell.BuffDeflectionBonus;
        effect.AppliedTempHP = spell.BuffTempHP;
        effect.AppliedStatName = spell.BuffStatName;
        effect.AppliedStatBonus = spell.BuffStatBonus;

        // Apply stat modifications
        ApplyStatModifications(effect);
        effect.IsApplied = true;

        ActiveEffects.Add(effect);

        Debug.Log($"[StatusEffect] {_stats.CharacterName}: {effect.GetDetailedString()}");
        return effect;
    }

    /// <summary>
    /// Tick all active effects by 1 round. Returns list of effects that expired.
    /// Call this at the end of each combat round (or start of new round).
    /// </summary>
    public List<ActiveSpellEffect> TickAllEffects()
    {
        var expired = new List<ActiveSpellEffect>();

        foreach (var effect in ActiveEffects)
        {
            if (effect.Tick())
            {
                expired.Add(effect);
            }
        }

        // Remove expired effects and reverse their stat modifications
        foreach (var effect in expired)
        {
            RemoveEffect(effect);
        }

        return expired;
    }

    /// <summary>
    /// Remove a specific effect and reverse its stat modifications.
    /// </summary>
    public void RemoveEffect(ActiveSpellEffect effect)
    {
        if (effect == null) return;

        if (effect.IsApplied)
        {
            ReverseStatModifications(effect);
            effect.IsApplied = false;
        }

        ActiveEffects.Remove(effect);

        // Also remove from SpellcastingComponent's ActiveBuffs for backward compat
        if (_spellComp != null && effect.Spell != null)
        {
            _spellComp.ActiveBuffs.Remove(effect.Spell.SpellId);
        }

        Debug.Log($"[StatusEffect] {_stats.CharacterName}: {effect.Spell?.Name ?? "Unknown"} effect removed");
    }

    /// <summary>
    /// Remove all effects from a specific spell (by spell ID).
    /// </summary>
    public void RemoveEffectsBySpellId(string spellId)
    {
        var toRemove = ActiveEffects.Where(e => e.Spell != null && e.Spell.SpellId == spellId).ToList();
        foreach (var effect in toRemove)
        {
            RemoveEffect(effect);
        }
    }

    /// <summary>
    /// Remove all active effects (e.g., on death or rest).
    /// </summary>
    public void RemoveAllEffects()
    {
        var all = new List<ActiveSpellEffect>(ActiveEffects);
        foreach (var effect in all)
        {
            RemoveEffect(effect);
        }
    }

    /// <summary>
    /// Check if the character has an active effect from a specific spell.
    /// </summary>
    public bool HasEffect(string spellId)
    {
        return ActiveEffects.Any(e => e.Spell != null && e.Spell.SpellId == spellId);
    }

    /// <summary>
    /// Get remaining rounds for a specific spell effect. Returns 0 if not active.
    /// </summary>
    public int GetRemainingRounds(string spellId)
    {
        var effect = ActiveEffects.FirstOrDefault(e => e.Spell != null && e.Spell.SpellId == spellId);
        return effect?.RemainingRounds ?? 0;
    }

    /// <summary>
    /// Get all active effects as display strings for UI.
    /// </summary>
    public List<string> GetActiveEffectDisplayStrings()
    {
        return ActiveEffects.Select(e => e.GetDisplayString()).ToList();
    }

    /// <summary>
    /// Get a compact buff summary string for the party panel UI.
    /// Shows abbreviated buff names with durations.
    /// </summary>
    public string GetBuffSummaryString()
    {
        if (ActiveEffects.Count == 0) return "";

        var parts = new List<string>();
        foreach (var effect in ActiveEffects)
        {
            string name = GetAbbreviatedName(effect.Spell?.Name ?? "?");
            string dur = effect.GetDurationDisplayString();
            parts.Add($"<color=#88FF88>{name}</color>({dur})");
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Get the number of active effects.
    /// </summary>
    public int ActiveEffectCount => ActiveEffects.Count;

    // ========== PRIVATE HELPERS ==========

    /// <summary>Apply stat modifications from an effect to the character.</summary>
    private void ApplyStatModifications(ActiveSpellEffect effect)
    {
        if (_stats == null) return;

        // Attack bonus (morale type for Bless, etc.)
        if (effect.AppliedAttackBonus != 0)
            _stats.MoraleAttackBonus += effect.AppliedAttackBonus;

        // Damage bonus
        if (effect.AppliedDamageBonus != 0)
            _stats.MoraleDamageBonus += effect.AppliedDamageBonus;

        // Save bonus
        if (effect.AppliedSaveBonus != 0)
            _stats.MoraleSaveBonus += effect.AppliedSaveBonus;

        // Spell AC bonus (Mage Armor)
        if (effect.AppliedACBonus != 0)
        {
            _stats.SpellACBonus = effect.AppliedACBonus;
            if (_spellComp != null)
            {
                _spellComp.MageArmorActive = true;
                _spellComp.MageArmorACBonus = effect.AppliedACBonus;
            }
        }

        // Shield bonus
        if (effect.AppliedShieldBonus != 0)
            _stats.ShieldBonus += effect.AppliedShieldBonus;

        // Deflection bonus
        if (effect.AppliedDeflectionBonus != 0)
            _stats.DeflectionBonus += effect.AppliedDeflectionBonus;

        // Temp HP
        if (effect.AppliedTempHP != 0)
            _stats.TempHP += effect.AppliedTempHP;

        // Stat buff (STR, DEX, CON, etc.)
        if (!string.IsNullOrEmpty(effect.AppliedStatName) && effect.AppliedStatBonus != 0)
        {
            ApplyStatBonus(effect.AppliedStatName, effect.AppliedStatBonus);
        }
    }

    /// <summary>Reverse stat modifications from an expired/removed effect.</summary>
    private void ReverseStatModifications(ActiveSpellEffect effect)
    {
        if (_stats == null) return;

        if (effect.AppliedAttackBonus != 0)
            _stats.MoraleAttackBonus -= effect.AppliedAttackBonus;

        if (effect.AppliedDamageBonus != 0)
            _stats.MoraleDamageBonus -= effect.AppliedDamageBonus;

        if (effect.AppliedSaveBonus != 0)
            _stats.MoraleSaveBonus -= effect.AppliedSaveBonus;

        if (effect.AppliedACBonus != 0)
        {
            _stats.SpellACBonus = 0;
            if (_spellComp != null)
            {
                _spellComp.MageArmorActive = false;
                _spellComp.MageArmorACBonus = 0;
            }
        }

        if (effect.AppliedShieldBonus != 0)
            _stats.ShieldBonus -= effect.AppliedShieldBonus;

        if (effect.AppliedDeflectionBonus != 0)
            _stats.DeflectionBonus -= effect.AppliedDeflectionBonus;

        if (effect.AppliedTempHP != 0)
        {
            _stats.TempHP = Mathf.Max(0, _stats.TempHP - effect.AppliedTempHP);
        }

        if (!string.IsNullOrEmpty(effect.AppliedStatName) && effect.AppliedStatBonus != 0)
        {
            ApplyStatBonus(effect.AppliedStatName, -effect.AppliedStatBonus);
        }
    }

    /// <summary>Apply a bonus to a specific ability score.</summary>
    private void ApplyStatBonus(string statName, int bonus)
    {
        if (_stats == null) return;

        switch (statName.ToUpper())
        {
            case "STR": _stats.STR += bonus; break;
            case "DEX": _stats.DEX += bonus; break;
            case "CON":
                _stats.CON += bonus;
                // CON changes affect HP: +1 HP per level per +2 CON
                int hpChange = (_stats.Level * (bonus / 2));
                if (bonus > 0)
                    _stats.BonusMaxHP += hpChange;
                else
                    _stats.BonusMaxHP = Mathf.Max(0, _stats.BonusMaxHP + hpChange);
                break;
            case "INT": _stats.INT += bonus; break;
            case "WIS": _stats.WIS += bonus; break;
            case "CHA": _stats.CHA += bonus; break;
        }
    }

    /// <summary>Get the "power" of an existing effect for stacking comparison.</summary>
    private int GetEffectPower(ActiveSpellEffect effect)
    {
        int power = 0;
        power += Mathf.Abs(effect.AppliedAttackBonus);
        power += Mathf.Abs(effect.AppliedDamageBonus);
        power += Mathf.Abs(effect.AppliedSaveBonus);
        power += Mathf.Abs(effect.AppliedACBonus);
        power += Mathf.Abs(effect.AppliedShieldBonus);
        power += Mathf.Abs(effect.AppliedDeflectionBonus);
        power += Mathf.Abs(effect.AppliedStatBonus);
        return power;
    }

    /// <summary>Get the "power" of a spell's bonuses for stacking comparison.</summary>
    private int GetEffectPowerFromSpell(SpellData spell)
    {
        int power = 0;
        power += Mathf.Abs(spell.BuffAttackBonus);
        power += Mathf.Abs(spell.BuffDamageBonus);
        power += Mathf.Abs(spell.BuffSaveBonus);
        power += Mathf.Abs(spell.BuffACBonus);
        power += Mathf.Abs(spell.BuffShieldBonus);
        power += Mathf.Abs(spell.BuffDeflectionBonus);
        power += Mathf.Abs(spell.BuffStatBonus);
        return power;
    }

    /// <summary>Get abbreviated spell name for compact UI display.</summary>
    private string GetAbbreviatedName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "?";
        if (fullName.Length <= 8) return fullName;

        // Common abbreviations
        switch (fullName)
        {
            case "Mage Armor": return "MgArmor";
            case "Shield of Faith": return "SoF";
            case "Bull's Strength": return "BullStr";
            case "Cat's Grace": return "CatGrc";
            case "Bear's Endurance": return "BearEnd";
            case "Owl's Wisdom": return "OwlWis";
            case "Eagle's Splendor": return "EglSpl";
            case "Fox's Cunning": return "FoxCun";
            default:
                // Truncate to 7 chars
                return fullName.Substring(0, 7) + "…";
        }
    }
}

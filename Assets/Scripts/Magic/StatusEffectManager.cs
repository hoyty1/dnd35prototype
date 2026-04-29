using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages active spell effects on a single character.
/// Tracks durations, handles effect application/removal, and enforces D&D 3.5e stacking rules.
///
/// D&D 3.5e Stacking Rules (PHB p.177):
///   - Most bonuses of the same TYPE to the same statistic do NOT stack (only the highest applies).
///   - Dodge bonuses always stack.
///   - Circumstance bonuses always stack.
///   - Untyped bonuses always stack.
///   - House Rule: Luck bonuses stack (per user request).
///   - Different bonus types to the same stat DO stack.
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

    /// <summary>Reference to the character controller for occupancy + visual size updates.</summary>
    private CharacterController _controller;

    /// <summary>Initialize with character references.</summary>
    public void Init(CharacterStats stats)
    {
        _stats = stats;
        _spellComp = GetComponent<SpellcastingComponent>();
        _controller = GetComponent<CharacterController>();
    }

    /// <summary>
    /// Add a new spell effect to this character.
    /// Handles D&D 3.5e stacking rules:
    ///   1. Same spell doesn't stack (replaces with longer duration).
    ///   2. Same bonus type to same stat doesn't stack (highest only) — unless stackable type.
    ///   3. Different bonus types to same stat DO stack.
    ///   4. Stackable types: Dodge, Untyped, Circumstance, Luck (house rule).
    /// Returns the created ActiveSpellEffect, or null if the effect was suppressed.
    /// </summary>
    public ActiveSpellEffect AddEffect(SpellData spell, string casterName, int casterLevel)
    {
        if (spell == null || _stats == null) return null;

        var effect = new ActiveSpellEffect(spell, casterName, casterLevel, _stats.CharacterName);
        BonusType bonusType = spell.GetEffectiveBonusType();

        // === RULE 1: Same spell doesn't stack (D&D 3.5e: use longer duration) ===
        var existingSameSpell = ActiveEffects.FirstOrDefault(e => e.Spell.SpellId == spell.SpellId);
        if (existingSameSpell != null)
        {
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

        // === RULE 2: Same bonus type stacking check ===
        // Only applies to "core" D&D bonus types that have standard stacking rules.
        // Spell-specific types (MirrorImage, Invisibility, etc.) only check same-spell (handled above).
        if (BonusTypeHelper.IsCoreType(bonusType) && !BonusTypeHelper.DoesStack(bonusType))
        {
            // Non-stackable type — check if another effect of the same bonus type exists
            // We need to check per-stat: same bonus type to the same stat doesn't stack.
            // For simplicity, we compare effects with the same BonusTypeEnum.
            var existingSameType = ActiveEffects.Where(e =>
                e.BonusTypeEnum == bonusType && e.Spell.SpellId != spell.SpellId).ToList();

            if (existingSameType.Count > 0)
            {
                // Check if bonuses overlap on the same stats
                foreach (var existing in existingSameType)
                {
                    bool overlaps = DoBonusesOverlap(spell, existing);
                    if (!overlaps) continue; // Different stats, both apply

                    int existingPower = GetEffectPower(existing);
                    int newPower = GetEffectPowerFromSpell(spell);

                    string bonusTypeName = BonusTypeHelper.GetDisplayName(bonusType);

                    if (newPower <= existingPower)
                    {
                        // New effect is weaker or equal — suppressed
                        string logMsg = $"⚠ {spell.Name} doesn't stack with existing {bonusTypeName} bonus " +
                                        $"from {existing.Spell.Name} (+{existingPower} vs +{newPower})";
                        Debug.Log($"[StatusEffect] {_stats.CharacterName}: {logMsg}");
                        LogCombatMessage($"{_stats.CharacterName}: {logMsg}");
                        return null;
                    }
                    else
                    {
                        // New effect is stronger — replace the old one
                        string logMsg = $"⚠ {spell.Name} ({bonusTypeName} +{newPower}) replaces " +
                                        $"{existing.Spell.Name} ({bonusTypeName} +{existingPower})";
                        Debug.Log($"[StatusEffect] {_stats.CharacterName}: {logMsg}");
                        LogCombatMessage($"{_stats.CharacterName}: {logMsg}");
                        RemoveEffect(existing);
                    }
                }
            }
        }
        // Stackable types (Dodge, Untyped, Circumstance, Luck): no stacking check needed, all apply

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
        effect.AppliedSecondaryStatName = null;
        effect.AppliedSecondaryStatBonus = 0;
        effect.AppliedSkillName = spell.BuffSkillName;
        effect.AppliedSkillBonus = spell.BuffSkillBonus;
        effect.AppliedSpeedBonusFeet = spell.BuffSpeedBonusFeet;
        effect.AppliedSizeCategoryShift = 0;
        effect.AppliedDamageResistanceAmount = spell.BuffDamageResistanceAmount;
        effect.AppliedDamageResistanceType = spell.BuffDamageResistanceType;
        effect.AppliedDamageImmunityType = spell.BuffDamageImmunityType;
        effect.AppliedDamageReductionAmount = spell.BuffDamageReductionAmount;
        effect.AppliedDamageReductionBypass = spell.BuffDamageReductionBypass;
        effect.AppliedDamageReductionRangedOnly = spell.BuffDamageReductionRangedOnly;

        if (spell.SpellId == "jump" && string.Equals(effect.AppliedSkillName, "Jump", System.StringComparison.OrdinalIgnoreCase) && effect.AppliedSkillBonus == 0)
        {
            int level = Mathf.Max(1, casterLevel);
            effect.AppliedSkillBonus = level >= 7 ? 30 : (level >= 3 ? 20 : 10);
        }

        // Protection from alignment grants conditional AC/save bonuses and ward effects.
        // It should NOT apply unconditional Deflection/Save bonuses directly to stats.
        if (AlignmentProtectionRules.TryGetProtectionTypeForSpell(spell.SpellId, out AlignmentProtectionType protectionType))
        {
            effect.ProtectionAgainstAlignment = protectionType;
            effect.ProtectionDeflectionBonus = Mathf.Max(0, spell.BuffDeflectionBonus > 0 ? spell.BuffDeflectionBonus : spell.BuffACBonus);
            effect.ProtectionResistanceBonus = Mathf.Max(0, spell.BuffSaveBonus);
            effect.ProtectionBlocksMentalControl = true;
            effect.ProtectionBlocksSummonedContact = true;

            effect.AppliedDeflectionBonus = 0;
            effect.AppliedSaveBonus = 0;
        }

        // Concealment / miss chance metadata (non-stacking; highest applies at attack time).
        // Keep spell-specific handling explicit to avoid accidental false positives from BuffType aliases.
        if (spell.SpellId == "blur" || spell.SpellId == "obscuring_mist" || spell.SpellId == "fog_cloud")
        {
            effect.MissChance = 20;
            effect.IsTotalConcealment = false;
            effect.ConcealmentSource = spell.Name;
        }
        else if (spell.SpellId == "displacement")
        {
            effect.MissChance = 50;
            effect.IsTotalConcealment = true;
            effect.ConcealmentSource = spell.Name;
        }
        else if (spell.SpellId == "invisibility")
        {
            effect.MissChance = 50;
            effect.IsTotalConcealment = true;
            effect.ConcealmentSource = spell.Name;
        }
        else if (spell.SpellId == "entropic_shield")
        {
            effect.MissChance = 20;
            effect.IsTotalConcealment = false;
            effect.ConcealmentSource = spell.Name;
            effect.MissChanceAgainstRangedOnly = true;
        }

        // Apply stat modifications
        ApplyStatModifications(effect);
        effect.IsApplied = true;

        ActiveEffects.Add(effect);

        // Log with bonus type info
        string typeStr = bonusType != BonusType.Untyped ? $" [{BonusTypeHelper.GetDisplayName(bonusType)}]" : "";
        Debug.Log($"[StatusEffect] {_stats.CharacterName}: Applied{typeStr} — {effect.GetDetailedString()}");
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

        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, "protection_from_arrows", System.StringComparison.Ordinal) && _stats != null)
            _stats.ActiveProtectionFromArrowsEffect = null;

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
    /// Check if the character has an active effect of a specific bonus type.
    /// </summary>
    public bool HasBonusType(BonusType type)
    {
        return ActiveEffects.Any(e => e.BonusTypeEnum == type);
    }

    /// <summary>
    /// Get the total bonus of a specific type currently active on the character.
    /// For non-stackable types, returns the highest value.
    /// For stackable types (Dodge, Untyped, Circumstance, Luck), returns the sum.
    /// </summary>
    public int GetTotalBonusOfType(BonusType type)
    {
        var matching = ActiveEffects.Where(e => e.BonusTypeEnum == type).ToList();
        if (matching.Count == 0) return 0;

        if (BonusTypeHelper.DoesStack(type))
        {
            return matching.Sum(e => GetEffectPower(e));
        }
        else
        {
            return matching.Max(e => GetEffectPower(e));
        }
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

    /// <summary>
    /// Check if two effects' bonuses overlap on the same stats.
    /// Used to determine if same-type stacking rules apply.
    /// E.g., two enhancement bonuses to STR overlap, but enhancement to STR and enhancement to DEX don't.
    /// For general bonuses (attack, damage, saves, AC), they overlap if both modify the same category.
    /// </summary>
    private bool DoBonusesOverlap(SpellData newSpell, ActiveSpellEffect existing)
    {
        // Size-changing transmutations overlap each other by definition.
        string newId = newSpell?.SpellId ?? string.Empty;
        string existingId = existing?.Spell?.SpellId ?? string.Empty;
        bool newIsSizeShift = newId == "enlarge_person" || newId == "reduce_person";
        bool existingIsSizeShift = existingId == "enlarge_person" || existingId == "reduce_person";
        if (newIsSizeShift && existingIsSizeShift) return true;
        // If both modify the same ability score
        if (!string.IsNullOrEmpty(newSpell.BuffStatName) && !string.IsNullOrEmpty(existing.AppliedStatName))
        {
            if (newSpell.BuffStatName.ToUpper() == existing.AppliedStatName.ToUpper())
                return true;
            // Different stats with same bonus type — both can apply
            // BUT if either also has attack/damage/save/AC bonuses, check those too
        }

        // If both modify attack bonus
        if (newSpell.BuffAttackBonus != 0 && existing.AppliedAttackBonus != 0) return true;
        // If both modify land speed
        if (newSpell.BuffSpeedBonusFeet != 0 && existing.AppliedSpeedBonusFeet != 0) return true;
        // If both modify the same skill
        if (!string.IsNullOrEmpty(newSpell.BuffSkillName) && !string.IsNullOrEmpty(existing.AppliedSkillName)
            && string.Equals(newSpell.BuffSkillName, existing.AppliedSkillName, System.StringComparison.OrdinalIgnoreCase)) return true;
        // If both modify damage bonus
        if (newSpell.BuffDamageBonus != 0 && existing.AppliedDamageBonus != 0) return true;
        // If both modify save bonus
        if (newSpell.BuffSaveBonus != 0 && existing.AppliedSaveBonus != 0) return true;
        // If both grant same typed resistance
        if (newSpell.BuffDamageResistanceAmount > 0 && existing.AppliedDamageResistanceAmount > 0 &&
            newSpell.BuffDamageResistanceType == existing.AppliedDamageResistanceType) return true;

        // If both grant same immunity type
        if (newSpell.BuffDamageImmunityType != DamageType.Untyped &&
            newSpell.BuffDamageImmunityType == existing.AppliedDamageImmunityType) return true;

        // If both grant DR, treat as overlapping mitigation category
        if (newSpell.BuffDamageReductionAmount > 0 && existing.AppliedDamageReductionAmount > 0) return true;
        // If both modify AC (armor type)
        if (newSpell.BuffACBonus != 0 && existing.AppliedACBonus != 0) return true;
        // If both modify shield bonus
        if (newSpell.BuffShieldBonus != 0 && existing.AppliedShieldBonus != 0) return true;
        // If both modify deflection bonus
        if (newSpell.BuffDeflectionBonus != 0 && existing.AppliedDeflectionBonus != 0) return true;

        // For stat bonuses to DIFFERENT stats with same bonus type, don't count as overlap
        if (!string.IsNullOrEmpty(newSpell.BuffStatName) && !string.IsNullOrEmpty(existing.AppliedStatName))
        {
            if (newSpell.BuffStatName.ToUpper() != existing.AppliedStatName.ToUpper())
                return false;
        }

        // If both have stat bonuses and at least one doesn't have a stat name, assume overlap
        if (newSpell.BuffStatBonus != 0 && existing.AppliedStatBonus != 0) return true;

        return false;
    }

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

        // Land speed enhancement bonus (feet).
        if (effect.AppliedSpeedBonusFeet != 0)
            _stats.LandSpeedEnhancementBonusFeet += effect.AppliedSpeedBonusFeet;

        // Typed resistance
        if (effect.AppliedDamageResistanceAmount > 0 && effect.AppliedDamageResistanceType != DamageType.Untyped)
            _stats.AddDamageResistance(effect.AppliedDamageResistanceType, effect.AppliedDamageResistanceAmount);

        // Typed immunity
        if (effect.AppliedDamageImmunityType != DamageType.Untyped)
            _stats.AddDamageImmunity(effect.AppliedDamageImmunityType);

        // Damage reduction
        if (effect.AppliedDamageReductionAmount > 0)
            _stats.AddDamageReduction(effect.AppliedDamageReductionAmount, effect.AppliedDamageReductionBypass, effect.AppliedDamageReductionRangedOnly);


        ApplySpellSpecificAdjustments(effect, applying: true);

        // Stat buff(s) (STR, DEX, CON, etc.)
        if (!string.IsNullOrEmpty(effect.AppliedStatName) && effect.AppliedStatBonus != 0)
        {
            ApplyStatBonus(effect.AppliedStatName, effect.AppliedStatBonus);
        }

        if (!string.IsNullOrEmpty(effect.AppliedSecondaryStatName) && effect.AppliedSecondaryStatBonus != 0)
        {
            ApplyStatBonus(effect.AppliedSecondaryStatName, effect.AppliedSecondaryStatBonus);
        }

        if (!string.IsNullOrEmpty(effect.AppliedSkillName) && effect.AppliedSkillBonus != 0)
            ApplySkillBonus(effect.AppliedSkillName, effect.AppliedSkillBonus);
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

        if (effect.AppliedSpeedBonusFeet != 0)
            _stats.LandSpeedEnhancementBonusFeet = Mathf.Max(0, _stats.LandSpeedEnhancementBonusFeet - effect.AppliedSpeedBonusFeet);

        if (effect.AppliedDamageResistanceAmount > 0 && effect.AppliedDamageResistanceType != DamageType.Untyped)
            _stats.RemoveDamageResistance(effect.AppliedDamageResistanceType, effect.AppliedDamageResistanceAmount);

        if (effect.AppliedDamageImmunityType != DamageType.Untyped)
            _stats.RemoveDamageImmunity(effect.AppliedDamageImmunityType);

        if (effect.AppliedDamageReductionAmount > 0)
            _stats.RemoveDamageReduction(effect.AppliedDamageReductionAmount, effect.AppliedDamageReductionBypass, effect.AppliedDamageReductionRangedOnly);

        ApplySpellSpecificAdjustments(effect, applying: false);

        if (!string.IsNullOrEmpty(effect.AppliedStatName) && effect.AppliedStatBonus != 0)
        {
            ApplyStatBonus(effect.AppliedStatName, -effect.AppliedStatBonus);
        }

        if (!string.IsNullOrEmpty(effect.AppliedSecondaryStatName) && effect.AppliedSecondaryStatBonus != 0)
        {
            ApplyStatBonus(effect.AppliedSecondaryStatName, -effect.AppliedSecondaryStatBonus);
        }

        if (!string.IsNullOrEmpty(effect.AppliedSkillName) && effect.AppliedSkillBonus != 0)
            ApplySkillBonus(effect.AppliedSkillName, -effect.AppliedSkillBonus);
    }

    /// <summary>
    /// Handles spell-specific mechanics that are not represented by generic bonus fields.
    /// Currently used for Enlarge Person / Reduce Person size-shift behavior.
    /// </summary>
    private void ApplySpellSpecificAdjustments(ActiveSpellEffect effect, bool applying)
    {
        if (_stats == null || effect?.Spell == null) return;

        string spellId = effect.Spell.SpellId;
        if (string.IsNullOrEmpty(spellId)) return;

        if (spellId == "disguise_self")
        {
            if (applying)
            {
                _stats.DisguiseCompetenceBonus += 10;
            }
            else
            {
                _stats.DisguiseCompetenceBonus = Mathf.Max(0, _stats.DisguiseCompetenceBonus - 10);
                _controller?.ClearDisguiseSelfEffect();
            }

            return;
        }

        if (spellId == "expeditious_retreat")
        {
            if (!applying)
                _controller?.ClearExpeditiousRetreatEffect();

            return;
        }

        if (spellId == "enlarge_person" || spellId == "reduce_person")
        {
            int shift = (spellId == "enlarge_person") ? 1 : -1;

            if (applying)
            {
                bool sizeChanged = TryApplySizeShift(shift);
                effect.AppliedSizeCategoryShift = sizeChanged ? shift : 0;

                if (spellId == "enlarge_person")
                {
                    effect.AppliedStatName = "STR";
                    effect.AppliedStatBonus = 2;
                    effect.AppliedSecondaryStatName = "DEX";
                    effect.AppliedSecondaryStatBonus = -2;
                }
                else
                {
                    effect.AppliedStatName = "STR";
                    effect.AppliedStatBonus = -2;
                    effect.AppliedSecondaryStatName = "DEX";
                    effect.AppliedSecondaryStatBonus = 2;
                }

                Debug.Log($"[StatusEffect] {_stats.CharacterName}: {spellId} apply size shift {(sizeChanged ? "succeeded" : "failed")} (delta {shift:+#;-#;0})");
                ForceSizeVisualRefresh();
            }
            else if (effect.AppliedSizeCategoryShift != 0)
            {
                bool reverted = TryApplySizeShift(-effect.AppliedSizeCategoryShift);
                Debug.Log($"[StatusEffect] {_stats.CharacterName}: {spellId} expire size shift revert {(reverted ? "succeeded" : "failed")} (delta {-effect.AppliedSizeCategoryShift:+#;-#;0})");
                ForceSizeVisualRefresh();
            }
        }
    }

    private bool TryApplySizeShift(int shift)
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();

        if (_controller != null)
            return _controller.ChangeSize(shift);

        return _stats.TryShiftCurrentSize(shift);
    }

    private void ForceSizeVisualRefresh()
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();

        if (_controller != null)
            _controller.UpdateVisualSize();
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

    private void ApplySkillBonus(string skillName, int bonus)
    {
        if (_stats == null || string.IsNullOrWhiteSpace(skillName) || bonus == 0)
            return;

        if (string.Equals(skillName, "Jump", System.StringComparison.OrdinalIgnoreCase))
            _stats.JumpEnhancementBonus += bonus;
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
        power += Mathf.Abs(effect.AppliedSecondaryStatBonus);
        power += Mathf.Abs(effect.AppliedSkillBonus);
        power += Mathf.Abs(effect.AppliedSpeedBonusFeet);
        power += Mathf.Abs(effect.AppliedSizeCategoryShift) * 2;
        power += Mathf.Abs(effect.AppliedDamageResistanceAmount);
        power += Mathf.Abs(effect.AppliedDamageReductionAmount);
        if (effect.AppliedDamageImmunityType != DamageType.Untyped) power += 999; // immunity is always strongest
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
        power += Mathf.Abs(spell.BuffSkillBonus);
        power += Mathf.Abs(spell.BuffSpeedBonusFeet);
        power += Mathf.Abs(spell.BuffDamageResistanceAmount);
        power += Mathf.Abs(spell.BuffDamageReductionAmount);
        if (spell.BuffDamageImmunityType != DamageType.Untyped) power += 999;
        return power;
    }

    /// <summary>
    /// Log a combat message that will be visible to the player.
    /// Uses the static CombatLog if available.
    /// </summary>
    private void LogCombatMessage(string message)
    {
        // CombatLog integration: broadcast the stacking message
        Debug.Log($"[COMBAT LOG] {message}");
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
            case "Expeditious Retreat": return "ExpRet";
            default:
                // Truncate to 7 chars
                return fullName.Substring(0, 7) + "…";
        }
    }
}
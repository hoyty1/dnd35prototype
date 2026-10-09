using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Manages active spell effects on a single character.
/// Tracks durations, handles effect application/removal, and enforces D&D 3.5e stacking rules.
///
/// D&D 3.5e stacking rules (PHB p.171-172 "Combining Magical Effects"; glossary p.305-313; DMG p.21), applied per
/// statistic by <see cref="BonusStacking"/> over <see cref="CharacterStats.Bonuses"/>, where every applied effect
/// registers its attack, weapon damage, save, skill, speed and ability bonuses with its real <see cref="BonusType"/>:
///   - Bonuses of the same type to the same statistic do not stack (only the highest applies); luck included.
///   - Dodge bonuses stack; circumstance and untyped bonuses stack unless they come from the same source.
///   - Different bonus types to the same statistic stack.
///   - Penalties: of one type, only the worst; untyped penalties from different sources add up.
///   - The same spell never stacks with itself (the source of its bonuses is its SpellId): only the best applies.
///   - Effects that do not stack still coexist: when the better one ends, the other applies again (PHB p.172). Only
///     size-changing transmutations still replace each other, and a second copy of a spell with side effects of its own
///     (flags, size, attribute enhancement tracking) keeps one copy (<see cref="CanCoexistWithItself"/>).
/// Armor, deflection, natural armor enhancement and typed save bonuses take the highest applied value per type
/// (Recompute* helpers). No house rule applies (owner directive 2026-10-09).
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
    /// Add a new spell effect to this character. Stacking (PHB p.171-172):
    ///   1. The same spell does not stack with itself. A copy that is no stronger and lasts no longer than an active one
    ///      adds nothing (the active one is re-timed when the counts are equal); a copy that is at least as strong and
    ///      lasts at least as long replaces it; otherwise both coexist for a spell that allows it
    ///      (<see cref="CanCoexistWithItself"/>) and only the better one counts, else the stronger one is kept. Dropping
    ///      the dominated copy differs from PHB p.172 only when the better copy is dispelled early (SPL-134).
    ///   2. Effects of different spells all apply; for each statistic the bonuses of one type do not stack (highest
    ///      only) except dodge, circumstance and untyped ones (<see cref="BonusStacking"/>). Size-changing
    ///      transmutations still replace or suppress each other.
    /// Returns the created ActiveSpellEffect, or null if the effect was not added.
    /// <paramref name="durationRounds"/> overrides the spell's default duration before the same-spell comparison, for a
    /// caller whose rule sets its own count (CMB-006).
    /// </summary>
    public ActiveSpellEffect AddEffect(SpellData spell, string casterName, int casterLevel, int? durationRounds = null)
    {
        if (spell == null || _stats == null) return null;

        var effect = new ActiveSpellEffect(spell, casterName, casterLevel, _stats.CharacterName);
        if (durationRounds.HasValue)
            effect.RemainingRounds = durationRounds.Value;
        BonusType bonusType = spell.GetEffectiveBonusType();

        // The values this copy applies (some depend on the caster level), set before the stacking comparison.
        ConfigureAppliedValues(effect, spell, casterLevel);

        // === RULE 1: the same spell does not stack with itself (PHB p.172, "Same Effect More than Once in Different Strengths") ===
        var sameSpell = ActiveEffects.Where(e => e != null && e.Spell != null && e.Spell.SpellId == spell.SpellId).ToList();
        foreach (var existing in sameSpell)
        {
            // A bonus copy and a penalty copy of one spell (Prayer from an ally's caster and from a foe's, PHB p.264) are
            // different effects: both apply (the ledger keeps a source's best bonus and its worst penalty apart).
            if (EffectSign(existing) != EffectSign(effect))
                continue;

            int newPower = GetEffectPower(effect);
            int oldPower = GetEffectPower(existing);
            long newLasts = Lasts(effect.RemainingRounds);
            long oldLasts = Lasts(existing.RemainingRounds);

            if (oldPower >= newPower && oldLasts >= newLasts)
            {
                // An equal count from the current initiative count ends no earlier than the existing effect, which
                // ends within its remaining rounds: keep the existing effect, timed from the new count (CMB-006).
                if (effect.RemainingRounds == existing.RemainingRounds && effect.RemainingRounds > 0)
                    TurnDurations.Refresh(existing, effect.RemainingRounds);
                Debug.Log($"[StatusEffect] {_stats.CharacterName}: {spell.Name} already active as strong and as long, ignoring");
                return null;
            }

            if (newPower >= oldPower && newLasts >= oldLasts)
            {
                Debug.Log($"[StatusEffect] {_stats.CharacterName}: Replacing {spell.Name} with a copy at least as strong and as long " +
                          $"(+{oldPower} → +{newPower}, {existing.RemainingRounds} → {effect.RemainingRounds} rounds)");
                RemoveEffect(existing);
                continue;
            }

            if (CanCoexistWithItself(effect))
            {
                // Both copies operate; only the better one counts (same source), and the other applies when it ends.
                Debug.Log($"[StatusEffect] {_stats.CharacterName}: second {spell.Name} coexists (+{oldPower}, {existing.RemainingRounds} rounds " +
                          $"and +{newPower}, {effect.RemainingRounds} rounds); the better applies");
                continue;
            }

            // A spell with side effects of its own keeps one copy: the stronger one, so the bonus that applies now is
            // the one RAW gives (PHB p.172); the weaker copy's longer tail is lost (SPL-134).
            if (newPower > oldPower)
            {
                Debug.Log($"[StatusEffect] {_stats.CharacterName}: Replacing {spell.Name} with a stronger, shorter copy " +
                          $"(+{oldPower} → +{newPower}, {existing.RemainingRounds} → {effect.RemainingRounds} rounds)");
                RemoveEffect(existing);
            }
            else
            {
                Debug.Log($"[StatusEffect] {_stats.CharacterName}: {spell.Name} already active and stronger, ignoring the weaker, longer copy");
                return null;
            }
        }

        // === RULE 2: size-changing transmutations (Enlarge Person, Reduce Person and their mass versions) replace or
        // suppress each other: a creature has one size. Every other effect applies; same-type bonuses are resolved per
        // statistic when they are read (CharacterStats.Bonuses, BonusStacking).
        if (IsSizeShiftSpell(spell.SpellId))
        {
            foreach (var existing in ActiveEffects.Where(e => e != null && e.Spell != null && e.Spell.SpellId != spell.SpellId
                                                              && IsSizeShiftSpell(e.Spell.SpellId)).ToList())
            {
                int existingPower = GetEffectPower(existing);
                int newPower = GetEffectPowerFromSpell(spell);
                if (newPower <= existingPower)
                {
                    string logMsg = $"⚠ {spell.Name} does not change the size set by {existing.Spell.Name}";
                    Debug.Log($"[StatusEffect] {_stats.CharacterName}: {logMsg}");
                    LogCombatMessage($"{_stats.CharacterName}: {logMsg}");
                    return null;
                }
                string replaceMsg = $"⚠ {spell.Name} replaces {existing.Spell.Name}";
                Debug.Log($"[StatusEffect] {_stats.CharacterName}: {replaceMsg}");
                LogCombatMessage($"{_stats.CharacterName}: {replaceMsg}");
                RemoveEffect(existing);
            }
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
    /// The values <paramref name="effect"/> applies: the spell's Buff* fields plus the caster-level and spell-specific
    /// adjustments, so every path that adds the effect (a cast, a scroll, a wand, a potion) grants the same bonus.
    /// </summary>
    private void ConfigureAppliedValues(ActiveSpellEffect effect, SpellData spell, int casterLevel)
    {
        effect.AppliedAttackBonus = spell.BuffAttackBonus;
        effect.AppliedDamageBonus = spell.BuffDamageBonus;
        // Divine Favor: +1 per three caster levels on attack and weapon damage rolls, at least +1 (PHB p.224). The owner's
        // PHB prints the maximum as +6 (the SRD says +3); the two differ only from caster level 12. Set here so every
        // path that adds the effect (a cast, a scroll, a wand) grants the same bonus.
        if (string.Equals(spell.SpellId, SpellNames.DIVINE_FAVOR, System.StringComparison.Ordinal))
        {
            int favor = Mathf.Clamp(casterLevel / 3, 1, 6);
            effect.AppliedAttackBonus = favor;
            effect.AppliedDamageBonus = favor;
        }
        effect.AppliedSaveBonus = spell.BuffSaveBonus;
        effect.AppliedACBonus = spell.BuffACBonus;
        effect.AppliedShieldBonus = spell.BuffShieldBonus;
        effect.AppliedDeflectionBonus = spell.BuffDeflectionBonus;
        // Barkskin's AC value is an enhancement bonus to natural armor (PHB p.203), not an armor bonus like Mage Armor's.
        // Its size follows the effect's caster level here, so every path that adds it (a cast, a staff, a scroll or a
        // potion) grants the same bonus (SPL-037).
        if (string.Equals(spell.BuffType, SpellData.NaturalArmorBuffType, System.StringComparison.OrdinalIgnoreCase))
        {
            effect.AppliedNaturalArmorEnhancementBonus = string.Equals(spell.SpellId, SpellNames.BARKSKIN, System.StringComparison.Ordinal)
                ? GameManager.BarkskinNaturalArmorBonus(casterLevel)
                : spell.BuffACBonus;
            effect.AppliedACBonus = 0;
        }
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

        if (spell.SpellId == SpellNames.JUMP && string.Equals(effect.AppliedSkillName, "Jump", System.StringComparison.OrdinalIgnoreCase) && effect.AppliedSkillBonus == 0)
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

        // Remove Fear's +4 is a morale bonus on saves against fear only (PHB p.271): SaveRules adds it for a fear save
        // (CharacterStats.RemoveFearMoraleBonus), so it must not also reach every save through the pool (CHR-018).
        if (string.Equals(spell.SpellId, SpellNames.REMOVE_FEAR, System.StringComparison.Ordinal))
            effect.AppliedSaveBonus = 0;

        // Concealment / miss chance metadata (non-stacking; highest applies at attack time).
        // Keep spell-specific handling explicit to avoid accidental false positives from BuffType aliases.
        if (spell.SpellId == SpellNames.BLUR || spell.SpellId == SpellNames.OBSCURING_MIST || spell.SpellId == SpellNames.FOG_CLOUD || spell.SpellId == SpellNames.DARKNESS)
        {
            effect.MissChance = 20;
            effect.IsTotalConcealment = false;
            effect.ConcealmentSource = spell.Name;
        }
        else if (spell.SpellId == SpellNames.DISPLACEMENT)
        {
            effect.MissChance = 50;
            effect.IsTotalConcealment = false;
            effect.ConcealmentSource = spell.Name;
        }
        else if (spell.SpellId == SpellNames.BLINK)
        {
            // Blink's miss chance is handled dynamically in GetMissChance based on attacker capabilities.
            // Base 50% miss chance, reduced to 20% if attacker can see invisible OR strike ethereal, 0% if both.
            // We set MissChance=50 as the default and override in CharacterController.GetMissChance.
            effect.MissChance = 50;
            effect.IsTotalConcealment = false;
            effect.ConcealmentSource = spell.Name;
        }
        else if (spell.SpellId == SpellNames.INVISIBILITY
                 || spell.SpellId == SpellNames.INVISIBILITY_SPHERE
                 || spell.SpellId == "greater_invisibility"
                 || spell.SpellId == "improved_invisibility")
        {
            effect.MissChance = 50;
            effect.IsTotalConcealment = true;
            effect.ConcealmentSource = spell.Name;
        }
        else if (spell.SpellId == SpellNames.ENTROPIC_SHIELD)
        {
            effect.MissChance = 20;
            effect.IsTotalConcealment = false;
            effect.ConcealmentSource = spell.Name;
            effect.MissChanceAgainstRangedOnly = true;
        }

        // Blindness/Deafness spell: permanent condition effect.
        // The actual condition (Blinded/Deafened) is applied via CharacterController.ApplyBlindnessEffect/ApplyDeafnessEffect.
        // StatusEffectManager tracks the spell effect for dispel/duration management.
        if (spell.SpellId == SpellNames.BLINDNESS_DEAFNESS)
        {
            effect.RemainingRounds = -1; // Permanent until removed
        }

        // Haste's +1 bonus on attack rolls and +1 dodge bonus to AC and Reflex saves are the Haste* fields, which
        // ApplyHasteBuff sets (PHB p.239); the effect applies only the +30 ft enhancement bonus to speed, so the
        // attack and Reflex bonuses are not counted twice and Mage Armor is not replaced (SPL-024, SPL-025).
        if (string.Equals(spell.SpellId, SpellNames.HASTE, System.StringComparison.Ordinal))
        {
            effect.AppliedAttackBonus = 0;
            effect.AppliedSaveBonus = 0;
            effect.AppliedACBonus = 0;
        }

        // Rage (PHB p.268): +2 morale bonus to Strength and Constitution, registered as the effect's two ability bonuses
        // so they end with it; its +1 morale bonus is on Will saves only and its -2 AC penalty is SpellRageACPenalty, both
        // set in ApplySpellSpecificAdjustments (the spell data's BuffSaveBonus and BuffACBonus would reach every save and
        // the armor bonus).
        if (string.Equals(spell.SpellId, SpellNames.RAGE, System.StringComparison.Ordinal))
        {
            effect.AppliedStatName = "STR";
            effect.AppliedStatBonus = 2;
            effect.AppliedSecondaryStatName = "CON";
            effect.AppliedSecondaryStatBonus = 2;
            effect.AppliedSaveBonus = 0;
            effect.AppliedACBonus = 0;
        }
    }

    /// <summary>
    /// Tick all active effects by 1 round, whatever their duration anchor. Returns list of effects that expired.
    /// Combat ticks each effect at its anchor's initiative count instead (<see cref="TickEffects"/>, CMB-006).
    /// </summary>
    public List<ActiveSpellEffect> TickAllEffects() => TickEffects(null);

    /// <summary>
    /// Tick by 1 round the active effects whose <see cref="ActiveSpellEffect.DurationAnchor"/> passes
    /// <paramref name="ticksForAnchor"/> (null: every effect), remove the expired ones and reverse their stat
    /// modifications. Returns the effects that expired.
    /// </summary>
    public List<ActiveSpellEffect> TickEffects(System.Func<CharacterController, bool> ticksForAnchor)
    {
        var expired = new List<ActiveSpellEffect>();

        foreach (var effect in ActiveEffects)
        {
            if (ticksForAnchor != null && !ticksForAnchor(effect.DurationAnchor))
                continue;
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

        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.PROTECTION_FROM_ARROWS, System.StringComparison.Ordinal) && _stats != null)
            _stats.ActiveProtectionFromArrowsEffect = null;

        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.STONESKIN, System.StringComparison.Ordinal) && _stats != null)
            _stats.ActiveStoneskinEffect = null;

        // D&D 3.5e: Clear Sanctuary flag when the spell effect expires or is removed.
        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.SANCTUARY, System.StringComparison.Ordinal) && _stats != null)
        {
            _stats.SanctuaryActive = false;
            _stats.SanctuaryDC = 0;
            _stats.SanctuaryCasterLevel = 0;
        }

        // D&D 3.5e: Clear Hide from Undead flag when the spell effect expires or is removed.
        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.HIDE_FROM_UNDEAD, System.StringComparison.Ordinal) && _stats != null)
        {
            _stats.HideFromUndeadActive = false;
            _stats.HideFromUndeadDC = 0;
            _stats.HideFromUndeadCasterLevel = 0;
        }

        // D&D 3.5e: Clear Remove Fear morale bonus when the spell effect expires or is removed.
        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.REMOVE_FEAR, System.StringComparison.Ordinal) && _stats != null)
        {
            _stats.RemoveFearMoraleBonus = 0;
        }

        // D&D 3.5e: Clear Entropic Shield when the spell effect expires or is removed.
        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.ENTROPIC_SHIELD, System.StringComparison.Ordinal) && _stats != null)
        {
            _stats.EntropicShieldActive = false;
            _stats.EntropicShieldCasterLevel = 0;
        }

        // D&D 3.5e: Clear Magic Stone when the spell effect expires or is removed.
        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.MAGIC_STONE, System.StringComparison.Ordinal) && _stats != null)
        {
            _stats.MagicStoneActive = false;
            _stats.MagicStoneCharges = 0;
            _stats.MagicStoneCasterLevel = 0;
        }

        // Shield of Faith: ReverseStatModifications above already removed its deflection (AppliedDeflectionBonus);
        // only the indicator field follows the copies left, so the bonus is not subtracted twice (ITM-001). Two copies
        // may coexist (the better applies, PHB p.172), so the indicator shows the best one still active.
        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.SHIELD_OF_FAITH, System.StringComparison.Ordinal) && _stats != null)
            _stats.ShieldOfFaithDeflectionBonus = BestAppliedDeflection(SpellNames.SHIELD_OF_FAITH);

        // SPL-003: Death Knell, Silence and Align Weapon flags end with their tracked effect (expiry, dispel or rest).
        // Death Knell's +2 STR is an applied stat of the effect, so ReverseStatModifications above already took it back.
        if (effect.Spell != null && _stats != null)
            EffectService.ClearClericSpell2Flags(_stats, effect.Spell.SpellId, reverseDeathKnellStr: false);

        // Shield Other (PHB p.278, 1 hour/level): the damage-sharing link ends with the spell on the protected subject.
        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.SHIELD_OTHER, System.StringComparison.Ordinal) && _stats != null)
            ClearShieldOtherLink(_stats);

        // Globe of Invulnerability (PHB p.236): the emanation around the caster ends with the caster's tracked effect,
        // whether it expires, is dispelled or is cleared (SPL-037).
        if (effect.Spell != null && string.Equals(effect.Spell.SpellId, SpellNames.GLOBE_OF_INVULNERABILITY, System.StringComparison.Ordinal))
            LesserGlobeOfInvulnerabilityAreaEffect.EndGlobesOf(_controller != null ? _controller : GetComponent<CharacterController>(), SpellNames.GLOBE_OF_INVULNERABILITY);

        // Also remove from SpellcastingComponent's ActiveBuffs for backward compat (unless another copy still operates)
        if (_spellComp != null && effect.Spell != null && !HasEffect(effect.Spell.SpellId))
        {
            _spellComp.ActiveBuffs.Remove(effect.Spell.SpellId);
        }

        Debug.Log($"[StatusEffect] {_stats.CharacterName}: {effect.Spell?.Name ?? "Unknown"} effect removed");
    }

    /// <summary>Ends the Shield Other link that protects this subject, on both sides of the link.</summary>
    private static void ClearShieldOtherLink(CharacterStats protectedStats)
    {
        if (protectedStats == null || !protectedStats.ShieldOtherProtectedActive)
            return;

        CharacterController protector = protectedStats.ShieldOtherProtector;
        if (protector != null && protector.Stats != null && protector.Stats.ShieldOtherProtected != null
            && protector.Stats.ShieldOtherProtected.Stats == protectedStats)
        {
            protector.Stats.ShieldOtherProtectorActive = false;
            protector.Stats.ShieldOtherProtected = null;
        }

        protectedStats.ShieldOtherProtectedActive = false;
        protectedStats.ShieldOtherProtector = null;
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

    /// <summary>The highest deflection bonus among the active copies of <paramref name="spellId"/> (0 when none).</summary>
    public int BestAppliedDeflection(string spellId)
    {
        int best = 0;
        foreach (var e in ActiveEffects)
            if (e != null && e.IsApplied && e.Spell != null && e.Spell.SpellId == spellId && e.AppliedDeflectionBonus > best)
                best = e.AppliedDeflectionBonus;
        return best;
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
    /// The "power" of the active effects of one bonus type, by the PHB stacking rules: the sum over distinct spells for a
    /// type that stacks (dodge, circumstance, untyped), else the highest; two copies of one spell count once.
    /// </summary>
    public int GetTotalBonusOfType(BonusType type)
    {
        var matching = ActiveEffects.Where(e => e != null && e.BonusTypeEnum == type).ToList();
        if (matching.Count == 0) return 0;

        var perSpell = matching
            .GroupBy(e => e.Spell != null ? e.Spell.SpellId : string.Empty)
            .Select(g => g.Max(e => GetEffectPower(e)))
            .ToList();
        return BonusTypeHelper.DoesStack(type) ? perSpell.Sum() : perSpell.Max();
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
    /// Apply the stat modifications of an effect: its typed attack, weapon damage, save, skill, speed and ability bonuses
    /// go to <see cref="CharacterStats.Bonuses"/> (resolved per statistic by the PHB stacking rules when read), and the
    /// per-type AC and save fields are recomputed as the highest applied value of their type.
    /// </summary>
    private void ApplyStatModifications(ActiveSpellEffect effect)
    {
        if (_stats == null) return;

        RegisterBonuses(effect, includeStats: false);

        // Save bonus: a resistance or competence bonus (Resistance, Shield Other, Guidance) is kept as the highest of its
        // type so it does not stack with a cloak, ring or ioun stone of the same type (CharacterStats.GetSaveTotal), and
        // Bless's and Aid's morale bonus applies only against fear (SaveRules; CHR-018); every other spell save bonus
        // or penalty is in the ledger with its type (RegisterBonuses).
        if (effect.AppliedSaveBonus != 0 && IsTypedSaveBonus(effect))
            RecomputeTypedSpellSaveBonuses(effect, null);

        // Armor bonus (Mage Armor): the highest applied one (PHB p.171), which CharacterStats.ArmorClass compares with worn armor.
        if (effect.AppliedACBonus != 0)
            RecomputeSpellArmor(effect, null);

        // Shield bonus
        if (effect.AppliedShieldBonus != 0)
            _stats.ShieldBonus += effect.AppliedShieldBonus;

        // Deflection bonus: spell deflection bonuses do not stack (PHB p.171), so the field holds the highest
        // applied one (e.g. Shield of Faith +2 with Shield Other +1 gives +2), not their sum (ITM-001).
        if (effect.AppliedDeflectionBonus != 0)
            RecomputeSpellDeflection(effect, null);

        // Natural armor enhancement (Barkskin): the highest applied one counts (same-type bonuses do not stack).
        if (effect.AppliedNaturalArmorEnhancementBonus != 0)
            RecomputeSpellNaturalArmorEnhancement(effect, null);

        // Temp HP — False Life is handled separately via FalseLifeEffectData (1d10+CL calculation).
        // Only apply static temp HP for other spells that use BuffTempHP directly.
        if (effect.AppliedTempHP != 0 && effect.Spell != null && effect.Spell.SpellId != SpellNames.FALSE_LIFE)
            _stats.TempHP += effect.AppliedTempHP;

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

        // Ability score bonuses (from the spell data, or set by the size shift above): registered with their type, and
        // the score fields follow the stacked totals (SyncAbilityScores).
        RegisterBonuses(effect, includeStats: true);
        SyncAbilityScores(effect, effect.AppliedStatName, effect.AppliedSecondaryStatName);
    }

    /// <summary>
    /// Set CharacterStats.DeflectionBonus to the highest deflection bonus among the applied spell effects
    /// (same-type bonuses do not stack, PHB p.171). <paramref name="adding"/> is an effect being applied that is
    /// not yet in ActiveEffects; <paramref name="removing"/> is one being reversed. Ring deflection is kept in
    /// RingDeflectionBonus and combined at read time (CharacterStats.EffectiveDeflectionBonus).
    /// </summary>
    private void RecomputeSpellDeflection(ActiveSpellEffect adding, ActiveSpellEffect removing)
    {
        int highest = adding != null ? Mathf.Max(0, adding.AppliedDeflectionBonus) : 0;
        foreach (var active in ActiveEffects)
        {
            if (active == null || active == removing || !active.IsApplied) continue;
            if (active.AppliedDeflectionBonus > highest) highest = active.AppliedDeflectionBonus;
        }
        _stats.DeflectionBonus = highest;
    }

    private const int SaveBonusPool = 0, SaveBonusResistance = 1, SaveBonusCompetence = 2, SaveBonusFearMorale = 3;

    /// <summary>
    /// Which kind of save bonus <paramref name="effect"/> gives (a positive one): resistance (a resistance-typed spell
    /// such as Resistance, and the save part of Shield Other and Shield of Law, whose AC part is a deflection bonus,
    /// PHB p.278), competence (Guidance), morale against fear only (Bless, PHB p.205; Aid, PHB p.196), or the ledger
    /// (everything else, with the effect's own type: CharacterStats.Bonuses, AllSaves).
    /// </summary>
    private static int SaveBonusKind(ActiveSpellEffect effect)
    {
        if (effect == null || effect.AppliedSaveBonus <= 0)
            return SaveBonusPool;
        string id = effect.Spell != null ? effect.Spell.SpellId : null;
        if (string.Equals(id, SpellNames.BLESS, System.StringComparison.Ordinal)
            || string.Equals(id, SpellNames.AID, System.StringComparison.Ordinal))
            return SaveBonusFearMorale;
        if (effect.BonusTypeEnum == BonusType.Resistance
            || string.Equals(id, SpellNames.SHIELD_OTHER, System.StringComparison.Ordinal)
            || string.Equals(id, SpellNames.SHIELD_OF_LAW, System.StringComparison.Ordinal))
            return SaveBonusResistance;
        if (effect.BonusTypeEnum == BonusType.Competence)
            return SaveBonusCompetence;
        return SaveBonusPool;
    }

    /// <summary>True for a save penalty that applies only against fear effects: Bane's -1 (PHB p.203).</summary>
    private static bool IsFearOnlySavePenalty(ActiveSpellEffect effect)
    {
        return effect != null && effect.AppliedSaveBonus < 0 && effect.Spell != null
            && string.Equals(effect.Spell.SpellId, SpellNames.BANE, System.StringComparison.Ordinal);
    }

    /// <summary>True when <paramref name="effect"/>'s save bonus is kept apart from the untyped pool (<see cref="SaveBonusKind"/>).</summary>
    private static bool IsTypedSaveBonus(ActiveSpellEffect effect) => SaveBonusKind(effect) != SaveBonusPool;

    /// <summary>
    /// Set CharacterStats.ResistanceSaveBonusFromSpells, CompetenceSaveBonusFromSpells and FearMoraleSaveBonusFromSpells
    /// to the highest save bonus of each kind among the applied spell effects (PHB p.171: same-type bonuses do not stack;
    /// CHR-018). Arguments as for <see cref="RecomputeSpellDeflection"/>. Items of the same types are combined at read
    /// time (CharacterStats.EffectiveResistanceSaveBonus, EffectiveCompetenceSaveBonus); the fear morale bonus is added
    /// by SaveRules for a save against fear.
    /// </summary>
    private void RecomputeTypedSpellSaveBonuses(ActiveSpellEffect adding, ActiveSpellEffect removing)
    {
        int resistance = 0, competence = 0, fearMorale = 0;
        void Take(ActiveSpellEffect e)
        {
            switch (SaveBonusKind(e))
            {
                case SaveBonusResistance: resistance = Mathf.Max(resistance, e.AppliedSaveBonus); break;
                case SaveBonusCompetence: competence = Mathf.Max(competence, e.AppliedSaveBonus); break;
                case SaveBonusFearMorale: fearMorale = Mathf.Max(fearMorale, e.AppliedSaveBonus); break;
            }
        }
        if (adding != null)
            Take(adding);
        foreach (var active in ActiveEffects)
        {
            if (active == null || active == removing || active == adding || !active.IsApplied) continue;
            Take(active);
        }
        _stats.ResistanceSaveBonusFromSpells = resistance;
        _stats.CompetenceSaveBonusFromSpells = competence;
        _stats.FearMoraleSaveBonusFromSpells = fearMorale;
    }

    /// <summary>
    /// Set CharacterStats.SpellNaturalArmorEnhancementBonus to the highest natural armor enhancement among the applied
    /// spell effects (Barkskin, PHB p.203; same-type bonuses do not stack, PHB p.171). Arguments as for
    /// <see cref="RecomputeSpellDeflection"/>. An amulet of natural armor is combined at read time (ArmorClass).
    /// </summary>
    private void RecomputeSpellNaturalArmorEnhancement(ActiveSpellEffect adding, ActiveSpellEffect removing)
    {
        int highest = adding != null ? Mathf.Max(0, adding.AppliedNaturalArmorEnhancementBonus) : 0;
        foreach (var active in ActiveEffects)
        {
            if (active == null || active == removing || !active.IsApplied) continue;
            if (active.AppliedNaturalArmorEnhancementBonus > highest) highest = active.AppliedNaturalArmorEnhancementBonus;
        }
        _stats.SpellNaturalArmorEnhancementBonus = highest;
    }

    /// <summary>Reverse stat modifications from an expired/removed effect.</summary>
    private void ReverseStatModifications(ActiveSpellEffect effect)
    {
        if (_stats == null) return;

        // The ledger entries this effect registered (attack, damage, save, skill, speed, abilities) end with it; a weaker
        // effect of the same type that did not count applies again (PHB p.172).
        _registeredStats.TryGetValue(effect, out RegisteredStats registered);
        _registeredStats.Remove(effect);
        _stats.Bonuses.RemoveOwner(effect);

        if (effect.AppliedSaveBonus != 0 && IsTypedSaveBonus(effect))
            RecomputeTypedSpellSaveBonuses(null, effect);

        if (effect.AppliedACBonus != 0)
            RecomputeSpellArmor(null, effect);

        if (effect.AppliedShieldBonus != 0)
            _stats.ShieldBonus -= effect.AppliedShieldBonus;

        if (effect.AppliedDeflectionBonus != 0)
            RecomputeSpellDeflection(null, effect);

        if (effect.AppliedNaturalArmorEnhancementBonus != 0)
            RecomputeSpellNaturalArmorEnhancement(null, effect);

        // False Life temp HP removal is handled by CharacterController.RemoveFalseLifeEffect()
        if (effect.AppliedTempHP != 0 && (effect.Spell == null || effect.Spell.SpellId != SpellNames.FALSE_LIFE))
        {
            _stats.TempHP = Mathf.Max(0, _stats.TempHP - effect.AppliedTempHP);
        }

        if (effect.AppliedDamageResistanceAmount > 0 && effect.AppliedDamageResistanceType != DamageType.Untyped)
            _stats.RemoveDamageResistance(effect.AppliedDamageResistanceType, effect.AppliedDamageResistanceAmount);

        if (effect.AppliedDamageImmunityType != DamageType.Untyped)
            _stats.RemoveDamageImmunity(effect.AppliedDamageImmunityType);

        if (effect.AppliedDamageReductionAmount > 0)
            _stats.RemoveDamageReduction(effect.AppliedDamageReductionAmount, effect.AppliedDamageReductionBypass, effect.AppliedDamageReductionRangedOnly);

        ApplySpellSpecificAdjustments(effect, applying: false);

        // Ability scores follow the ledger totals without this effect.
        SyncAbilityScores(effect, effect.AppliedStatName, effect.AppliedSecondaryStatName);

        // A caller that set an ability bonus on the effect after AddEffect (Death Knell's +2 Strength, which it adds to
        // the score itself) gets it taken back directly, as before the ledger (CHR-010).
        string reverseSourceId = effect.Spell?.SpellId;
        if (!string.IsNullOrEmpty(effect.AppliedStatName) && effect.AppliedStatBonus != 0
            && !registered.Covers(effect.AppliedStatName, effect.AppliedStatBonus))
        {
            ApplyStatBonus(effect.AppliedStatName, -effect.AppliedStatBonus, reverseSourceId);
        }

        if (!string.IsNullOrEmpty(effect.AppliedSecondaryStatName) && effect.AppliedSecondaryStatBonus != 0
            && !registered.Covers(effect.AppliedSecondaryStatName, effect.AppliedSecondaryStatBonus))
        {
            ApplyStatBonus(effect.AppliedSecondaryStatName, -effect.AppliedSecondaryStatBonus, reverseSourceId);
        }
    }

    // ========== TYPED BONUS LEDGER ==========

    /// <summary>The ability bonuses an effect registered in the ledger (to tell them from ones a caller set afterwards).</summary>
    private struct RegisteredStats
    {
        public string Name1;
        public int Bonus1;
        public string Name2;
        public int Bonus2;

        public bool Covers(string name, int bonus)
        {
            return (bonus == Bonus1 && string.Equals(name, Name1, System.StringComparison.OrdinalIgnoreCase))
                || (bonus == Bonus2 && string.Equals(name, Name2, System.StringComparison.OrdinalIgnoreCase));
        }
    }

    private readonly Dictionary<ActiveSpellEffect, RegisteredStats> _registeredStats = new Dictionary<ActiveSpellEffect, RegisteredStats>();

    /// <summary>
    /// The bonus type <paramref name="effect"/>'s bonuses have in the ledger: its BonusType when that is one of the PHB
    /// types, Size for Enlarge and Reduce Person (DMG p.21: the Strength change of enlarge person is a size bonus), the
    /// spell's own BuffBonusType when that is a PHB type, else untyped (Haste's, Bane's and Doom's modifiers have no type).
    /// </summary>
    private static BonusType LedgerType(ActiveSpellEffect effect)
    {
        if (effect == null)
            return BonusType.Untyped;
        if (BonusTypeHelper.IsCoreType(effect.BonusTypeEnum))
            return effect.BonusTypeEnum;
        if (effect.Spell != null && IsSizeShiftSpell(effect.Spell.SpellId))
            return BonusType.Size;
        if (effect.Spell != null && BonusTypeHelper.IsCoreType(effect.Spell.BuffBonusType))
            return effect.Spell.BuffBonusType;
        return BonusType.Untyped;
    }

    /// <summary>
    /// Records <paramref name="effect"/>'s attack, weapon damage, save (outside the typed save kinds), skill and speed
    /// bonuses (or, with <paramref name="includeStats"/>, its ability bonuses) in <see cref="CharacterStats.Bonuses"/>,
    /// owned by the effect, with its type and its SpellId as the stacking source (the same spell never stacks with itself).
    /// </summary>
    private void RegisterBonuses(ActiveSpellEffect effect, bool includeStats)
    {
        if (_stats == null || effect == null)
            return;
        BonusLedger ledger = _stats.Bonuses;
        string source = effect.Spell != null ? effect.Spell.SpellId : null;
        string label = effect.Spell != null && !string.IsNullOrWhiteSpace(effect.Spell.Name) ? effect.Spell.Name : "spell";
        BonusType type = LedgerType(effect);

        if (!includeStats)
        {
            ledger.Add(effect, BonusTarget.AttackRoll, type, effect.AppliedAttackBonus, source, label);
            ledger.Add(effect, BonusTarget.WeaponDamage, type, effect.AppliedDamageBonus, source, label);
            if (effect.AppliedSaveBonus != 0 && !IsTypedSaveBonus(effect))
            {
                // Bane's -1 is on saves against fear effects only (PHB p.203); SaveRules adds it for a fear save.
                BonusTarget saveTarget = IsFearOnlySavePenalty(effect) ? BonusTarget.FearSaves : BonusTarget.AllSaves;
                ledger.Add(effect, saveTarget, type, effect.AppliedSaveBonus, source, label);
            }
            // Every spell land-speed bonus here is an enhancement bonus (Longstrider p.249, Expeditious Retreat p.228,
            // Haste p.239), so they do not stack with each other or with boots of striding and springing.
            ledger.Add(effect, BonusTarget.LandSpeedFeet, BonusType.Enhancement, effect.AppliedSpeedBonusFeet, source, label);
            if (!string.IsNullOrWhiteSpace(effect.AppliedSkillName) && effect.AppliedSkillBonus != 0)
            {
                if (string.Equals(effect.AppliedSkillName, SpellData.AllSkillsBuffSkillName, System.StringComparison.OrdinalIgnoreCase))
                    ledger.Add(effect, BonusTarget.AllSkills, type, effect.AppliedSkillBonus, source, label);
                else
                    ledger.Add(effect, BonusTarget.Skill, type, effect.AppliedSkillBonus, source, label, effect.AppliedSkillName);
            }
            return;
        }

        var registered = new RegisteredStats();
        if (TryAbilityTarget(effect.AppliedStatName, out BonusTarget statTarget) && effect.AppliedStatBonus != 0)
        {
            ledger.Add(effect, statTarget, type, effect.AppliedStatBonus, source, label);
            registered.Name1 = effect.AppliedStatName;
            registered.Bonus1 = effect.AppliedStatBonus;
        }
        if (TryAbilityTarget(effect.AppliedSecondaryStatName, out BonusTarget secondTarget) && effect.AppliedSecondaryStatBonus != 0)
        {
            ledger.Add(effect, secondTarget, type, effect.AppliedSecondaryStatBonus, source, label);
            registered.Name2 = effect.AppliedSecondaryStatName;
            registered.Bonus2 = effect.AppliedSecondaryStatBonus;
        }
        _registeredStats[effect] = registered;
    }

    private static bool TryAbilityTarget(string statName, out BonusTarget target)
    {
        target = BonusTarget.Strength;
        if (string.IsNullOrEmpty(statName))
            return false;
        switch (statName.ToUpperInvariant())
        {
            case "STR": target = BonusTarget.Strength; return true;
            case "DEX": target = BonusTarget.Dexterity; return true;
            case "CON": target = BonusTarget.Constitution; return true;
            case "INT": target = BonusTarget.Intelligence; return true;
            case "WIS": target = BonusTarget.Wisdom; return true;
            case "CHA": target = BonusTarget.Charisma; return true;
            default: return false;
        }
    }

    private static AbilityType ToAbilityType(BonusTarget target)
    {
        switch (target)
        {
            case BonusTarget.Dexterity: return AbilityType.DEX;
            case BonusTarget.Constitution: return AbilityType.CON;
            case BonusTarget.Intelligence: return AbilityType.INT;
            case BonusTarget.Wisdom: return AbilityType.WIS;
            case BonusTarget.Charisma: return AbilityType.CHA;
            default: return AbilityType.STR;
        }
    }

    /// <summary>
    /// Brings the ability score fields named by <paramref name="statName"/> and <paramref name="secondStatName"/> in
    /// step with the stacked ledger totals (<see cref="CharacterStats.SyncAbilityScoreWithBonuses"/>). A change in
    /// Constitution changes hit points by 1 per Hit Die per 2 points (CHR-071), except for an attribute enhancement spell
    /// (Bear's Endurance), whose hit points CharacterController.ApplyAttributeEnhancement handles.
    /// </summary>
    private void SyncAbilityScores(ActiveSpellEffect cause, string statName, string secondStatName)
    {
        SyncAbilityScore(cause, statName);
        if (!string.Equals(statName, secondStatName, System.StringComparison.OrdinalIgnoreCase))
            SyncAbilityScore(cause, secondStatName);
    }

    private void SyncAbilityScore(ActiveSpellEffect cause, string statName)
    {
        if (_stats == null || !TryAbilityTarget(statName, out BonusTarget target))
            return;
        AbilityType ability = ToAbilityType(target);
        int delta = _stats.SyncAbilityScoreWithBonuses(ability);
        if (delta == 0 || ability != AbilityType.CON)
            return;
        string sourceSpellId = cause != null && cause.Spell != null ? cause.Spell.SpellId : null;
        if (sourceSpellId != null && AttributeEnhancementEffectData.IsAttributeEnhancementSpell(sourceSpellId))
            return;
        int hpChange = _stats.GetHitDice() * (delta / 2);
        if (delta > 0)
            _stats.BonusMaxHP += hpChange;
        else
            _stats.BonusMaxHP = Mathf.Max(0, _stats.BonusMaxHP + hpChange);
    }

    /// <summary>
    /// Set CharacterStats.SpellACBonus (an armor bonus, Mage Armor) to the highest armor bonus among the applied spell
    /// effects (PHB p.171: armor bonuses do not stack; SPL-025). Arguments as for <see cref="RecomputeSpellDeflection"/>.
    /// </summary>
    private void RecomputeSpellArmor(ActiveSpellEffect adding, ActiveSpellEffect removing)
    {
        int best = 0, worst = 0;
        void Take(ActiveSpellEffect e)
        {
            if (e.AppliedACBonus > best) best = e.AppliedACBonus;
            if (e.AppliedACBonus < worst) worst = e.AppliedACBonus;
        }
        if (adding != null)
            Take(adding);
        foreach (var active in ActiveEffects)
        {
            if (active == null || active == removing || active == adding || !active.IsApplied) continue;
            Take(active);
        }
        _stats.SpellACBonus = best + worst;
        if (_spellComp != null)
        {
            _spellComp.MageArmorActive = best > 0;
            _spellComp.MageArmorACBonus = best;
        }
    }

    /// <summary>True for Enlarge Person, Reduce Person and their mass versions.</summary>
    private static bool IsSizeShiftSpell(string spellId)
    {
        return spellId == SpellNames.ENLARGE_PERSON || spellId == SpellNames.REDUCE_PERSON
            || spellId == SpellNames.MASS_ENLARGE_PERSON || spellId == SpellNames.MASS_REDUCE_PERSON;
    }

    /// <summary>Remaining rounds for comparison: permanent (-1) and concentration (-2) count as longest.</summary>
    private static long Lasts(int remainingRounds) => remainingRounds < 0 ? long.MaxValue : remainingRounds;

    /// <summary>
    /// Spells whose removal or application does more than add and remove bonuses (flags on CharacterStats, a
    /// controller effect, a size change, temporary hit points, damage mitigation, a miss chance): a second copy of one of
    /// these keeps a single copy instead of coexisting.
    /// </summary>
    private static readonly HashSet<string> SingleCopySpellIds = new HashSet<string>
    {
        SpellNames.PROTECTION_FROM_ARROWS, SpellNames.STONESKIN, SpellNames.SANCTUARY, SpellNames.HIDE_FROM_UNDEAD,
        SpellNames.REMOVE_FEAR, SpellNames.ENTROPIC_SHIELD, SpellNames.MAGIC_STONE,
        SpellNames.DEATH_KNELL, SpellNames.SILENCE, SpellNames.ALIGN_WEAPON, SpellNames.SHIELD_OTHER,
        SpellNames.GLOBE_OF_INVULNERABILITY, SpellNames.FALSE_LIFE, SpellNames.RAGE, SpellNames.DISGUISE_SELF,
        SpellNames.EXPEDITIOUS_RETREAT, SpellNames.INVISIBILITY, SpellNames.INVISIBILITY_SPHERE, SpellNames.GLITTERDUST,
        SpellNames.ENLARGE_PERSON, SpellNames.REDUCE_PERSON, SpellNames.MASS_ENLARGE_PERSON, SpellNames.MASS_REDUCE_PERSON,
        SpellNames.HASTE, SpellNames.PRAYER, SpellNames.BLINDNESS_DEAFNESS, SpellNames.DIVINE_POWER
    };

    /// <summary>
    /// True when two copies of <paramref name="effect"/>'s spell may operate at once (PHB p.172: both continue, the
    /// better applies, and the other remains when one ends): a spell that only adds bonuses (Divine Favor, Bless,
    /// Heroism, Magic Fang, Barkskin, ...).
    /// </summary>
    private static bool CanCoexistWithItself(ActiveSpellEffect effect)
    {
        string id = effect?.Spell?.SpellId;
        if (string.IsNullOrEmpty(id) || SingleCopySpellIds.Contains(id))
            return false;
        if (AttributeEnhancementEffectData.IsAttributeEnhancementSpell(id))
            return false;
        if (AlignmentProtectionRules.TryGetProtectionTypeForSpell(id, out _))
            return false;
        return effect.AppliedTempHP == 0 && effect.AppliedShieldBonus == 0 && effect.AppliedDamageResistanceAmount == 0
            && effect.AppliedDamageImmunityType == DamageType.Untyped && effect.AppliedDamageReductionAmount == 0
            && effect.MissChance == 0;
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

        if (spellId == SpellNames.FALSE_LIFE)
        {
            if (!applying)
            {
                _controller?.RemoveFalseLifeEffect();
            }
            return;
        }

        // Attribute Enhancement Spells: Bear's Endurance, Bull's Strength, Cat's Grace,
        // Eagle's Splendor, Fox's Cunning, Owl's Wisdom
        if (AttributeEnhancementEffectData.IsAttributeEnhancementSpell(spellId))
        {
            if (!applying && _controller != null)
            {
                _controller.RemoveAttributeEnhancementBySpellId(spellId);
            }
            return;
        }

        // Rage spell (PHB p.268): the -2 AC penalty and the +1 morale bonus on Will saves. The Will bonus is a ledger
        // entry owned by the effect, so ReverseStatModifications removes it with the effect's +2 Strength and
        // Constitution (ConfigureAppliedValues), and it does not stack with another morale bonus on Will.
        if (spellId == SpellNames.RAGE)
        {
            if (applying)
            {
                _stats.SpellRageACPenalty = -2;
                _stats.Bonuses.Add(effect, BonusTarget.Will, BonusType.Morale, 1, SpellNames.RAGE, effect.Spell.Name);
            }
            else
            {
                _stats.SpellRageACPenalty = 0;
            }
            return;
        }

        if (spellId == SpellNames.DISGUISE_SELF)
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

        if (spellId == SpellNames.EXPEDITIOUS_RETREAT)
        {
            if (!applying)
                _controller?.ClearExpeditiousRetreatEffect();

            return;
        }

        if (spellId == SpellNames.INVISIBILITY)
        {
            if (applying)
            {
                int rounds = Mathf.Max(0, effect.RemainingRounds);
                _controller?.ApplyInvisibilityEffect(rounds, caster: _controller, isMoving: false);
            }
            else
            {
                _controller?.ClearInvisibilityEffect();
            }

            return;
        }

        // Invisibility Sphere: the recipient's tracking effect represents the entire
        // sphere. The actual per-creature invisibility for the recipient and any
        // affected creatures is managed by the InvisibilitySphereEffect emanation
        // (see GameManager_NewSpells.ApplyInvisibilitySphere). When this tracking
        // effect is removed (expiration/dispel/dismiss), end the sphere for all.
        if (spellId == SpellNames.INVISIBILITY_SPHERE)
        {
            if (!applying && _controller != null && GameManager.Instance != null)
            {
                GameManager.Instance.EndInvisibilitySphereForRecipient(_controller, "spell ended");
            }
            return;
        }

        if (spellId == SpellNames.GLITTERDUST)
        {
            if (!applying)
                _controller?.ClearGlitterdustEffect();

            return;
        }

        if (spellId == SpellNames.ENLARGE_PERSON || spellId == SpellNames.REDUCE_PERSON
            || spellId == SpellNames.MASS_ENLARGE_PERSON || spellId == SpellNames.MASS_REDUCE_PERSON)
        {
            bool isEnlarge = spellId == SpellNames.ENLARGE_PERSON || spellId == SpellNames.MASS_ENLARGE_PERSON;
            int shift = isEnlarge ? 1 : -1;

            if (applying)
            {
                bool sizeChanged = TryApplySizeShift(shift);
                effect.AppliedSizeCategoryShift = sizeChanged ? shift : 0;

                if (isEnlarge)
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
    private void ApplyStatBonus(string statName, int bonus, string sourceSpellId = null)
    {
        if (_stats == null) return;

        switch (statName.ToUpper())
        {
            case "STR": _stats.STR += bonus; break;
            case "DEX": _stats.DEX += bonus; break;
            case "CON":
                _stats.CON += bonus;
                // Bear's Endurance handles HP through CharacterController.ApplyAttributeEnhancement
                // using Hit Dice (not Level). Skip generic HP calc for attribute enhancement spells.
                if (sourceSpellId == null || !AttributeEnhancementEffectData.IsAttributeEnhancementSpell(sourceSpellId))
                {
                    // CON changes affect HP: +1 HP per Hit Die per +2 CON (CHR-071)
                    int hpChange = (_stats.GetHitDice() * (bonus / 2));
                    if (bonus > 0)
                        _stats.BonusMaxHP += hpChange;
                    else
                        _stats.BonusMaxHP = Mathf.Max(0, _stats.BonusMaxHP + hpChange);
                }
                break;
            case "INT": _stats.INT += bonus; break;
            case "WIS": _stats.WIS += bonus; break;
            case "CHA": _stats.CHA += bonus; break;
        }
    }

    /// <summary>+1 when <paramref name="effect"/>'s modifiers add up to a bonus, -1 to a penalty, 0 when it has none.</summary>
    private static int EffectSign(ActiveSpellEffect effect)
    {
        int sum = effect.AppliedAttackBonus + effect.AppliedDamageBonus + effect.AppliedSaveBonus + effect.AppliedSkillBonus
            + effect.AppliedACBonus + effect.AppliedShieldBonus + effect.AppliedDeflectionBonus
            + effect.AppliedNaturalArmorEnhancementBonus + effect.AppliedStatBonus + effect.AppliedSecondaryStatBonus
            + effect.AppliedSpeedBonusFeet;
        return sum > 0 ? 1 : (sum < 0 ? -1 : 0);
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
        power += Mathf.Abs(effect.AppliedNaturalArmorEnhancementBonus);
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
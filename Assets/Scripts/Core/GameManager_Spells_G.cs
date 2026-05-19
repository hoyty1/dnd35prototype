// ============================================================================
// GameManager_Spells_G.cs — Spell resolution methods starting with "G".
//
// Part of the GameManager partial class.
// D&D 3.5e PHB rules.
// ============================================================================
using DND35e.Identifiers;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;

public partial class GameManager
{
    // ================================================================
    //  GREATER MAGIC WEAPON — PHB 3.5e p.251
    //  Transmutation. Clr 4, Pal 3, Sor/Wiz 3.
    //  +1 enhancement bonus per 4 CL (max +5).
    //  Duration: 1 hour/level.
    // ================================================================

    private static bool IsGreaterMagicWeaponSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.GREATER_MAGIC_WEAPON, StringComparison.Ordinal);
    }

    private ItemData _pendingGreaterMagicWeaponItem;

    private bool TryHandleGreaterMagicWeaponSelection(CharacterController caster, CharacterController target)
    {
        if (!IsGreaterMagicWeaponSpell(_pendingSpell))
        {
            _pendingGreaterMagicWeaponItem = null;
            return false;
        }

        if (target == null || target.Stats == null)
            return false;

        if (_pendingGreaterMagicWeaponItem != null)
            return false;

        if (!TryGetMagicWeaponInventoryOptions(target, out List<ItemData> weaponOptions, out List<string> weaponLabels))
        {
            CombatUI?.ShowCombatLog($"⚠ {target.Stats.CharacterName} has no weapon in inventory to enchant with Greater Magic Weapon.");
            _pendingSpell = null;
            _pendingGreaterMagicWeaponItem = null;
            ShowActionChoices();
            return true;
        }

        if (weaponOptions.Count == 1)
        {
            _pendingGreaterMagicWeaponItem = weaponOptions[0];
            return false;
        }

        CombatUI?.ShowPickUpItemSelection(
            actorName: caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster",
            itemOptions: weaponLabels,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= weaponOptions.Count)
                {
                    _pendingSpell = null;
                    _pendingGreaterMagicWeaponItem = null;
                    ShowActionChoices();
                    return;
                }

                _pendingGreaterMagicWeaponItem = weaponOptions[selectedIndex];
                PerformSpellCast(caster, target);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingGreaterMagicWeaponItem = null;
                ShowActionChoices();
            },
            titleOverride: "Greater Magic Weapon - Select Weapon",
            bodyOverride: $"Choose which weapon from {target.Stats.CharacterName}'s inventory to enchant.",
            optionButtonColorOverride: new Color(0.24f, 0.34f, 0.56f, 1f));
        return true;
    }

    private bool TryApplyGreaterMagicWeaponToPendingItem(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (!IsGreaterMagicWeaponSpell(spell))
            return false;

        ItemData weapon = _pendingGreaterMagicWeaponItem;
        _pendingGreaterMagicWeaponItem = null;

        if (weapon == null)
        {
            CombatUI?.ShowCombatLog("⚠ Greater Magic Weapon failed: no weapon selected.");
            return true;
        }

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        int rounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;

        // Enhancement bonus = +1 per 4 CL, max +5
        int enhancementBonus = Mathf.Min(5, Mathf.Max(1, casterLevel / 4));

        var effect = new ItemSpellEffect(spell.SpellId, spell.Name, casterName, casterLevel, rounds)
        {
            BonusType = BonusType.Enhancement,
            EnhancementBonusAttack = enhancementBonus,
            EnhancementBonusDamage = enhancementBonus,
            CountsAsMagicForBypass = true
        };

        weapon.AddOrReplaceItemSpellEffect(effect);

        int effectiveAttackBonus = weapon.GetEnhancementAttackBonus();
        int effectiveDamageBonus = weapon.GetEnhancementDamageBonus();
        string recipientName = target != null && target.Stats != null ? target.Stats.CharacterName : "target";

        CombatUI?.ShowCombatLog($"<color=#88FFEE>✨ {spell.Name} enchants {recipientName}'s {weapon.Name}: +{enhancementBonus} enhancement for {effect.GetDurationDisplayString()} (CL {casterLevel}).</color>");
        CombatUI?.ShowCombatLog($"<color=#88FFEE>🗡 {weapon.Name} effective enhancement now +{Mathf.Max(effectiveAttackBonus, effectiveDamageBonus)} (attack +{effectiveAttackBonus}, damage +{effectiveDamageBonus}); counts as magic: yes.</color>");
        Debug.Log($"[GameManager] Greater Magic Weapon: {weapon.Name} +{enhancementBonus} enhancement, CL {casterLevel}, {rounds} rounds");

        UpdateAllStatsUI();
        return true;
    }

    // ================================================================
    //  GREATER INVISIBILITY — PHB p.245
    //  Illusion (Glamer). Brd 4, Sor/Wiz 4.
    //  Range: Personal or Touch.
    //  Duration: 1 round/level (D) — Dismissible.
    //  Like Invisibility but does NOT break when attacking.
    // ================================================================

    private static bool IsGreaterInvisibilitySpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.GREATER_INVISIBILITY, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Greater Invisibility: touch spell, applies invisibility that does NOT
    /// break on attack. Duration: 1 round/level. Dismissible.
    /// </summary>
    private bool TryResolveGreaterInvisibilitySpellEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellResult result)
    {
        if (!IsGreaterInvisibilitySpell(spell) || caster == null || caster.Stats == null)
            return false;

        if (result == null)
            return true;

        // Determine recipient (self or touched ally)
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null)
            return true;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, casterLevel); // 1 round/level
        string casterName = caster.Stats.CharacterName ?? "Unknown";

        // Create Greater Invisibility effect data — key difference: BreaksOnAttack = false
        InvisibilityEffectData effectData = InvisibilityEffectData.CreateGreaterInvisibility(durationRounds, caster);
        recipient.ApplyInvisibilityEffectData(effectData);

        // Track via StatusEffectManager
        if (recipient.StatusEffectManager != null)
        {
            recipient.StatusEffectManager.AddEffect(spell, casterName, casterLevel);
        }

        result.BuffApplied = true;
        result.BuffDescription = $"Buff: Greater Invisibility for {durationRounds} round(s). Does NOT break on attack.";

        CombatUI?.ShowCombatLog($"<color=#9966FF>✨ {recipient.Stats.CharacterName} becomes invisible (Greater Invisibility)!</color>");
        CombatUI?.ShowCombatLog($"<color=#AA88FF>   Duration: {durationRounds} round(s). Does NOT break on attack.</color>");
        CombatUI?.ShowCombatLog($"<color=#AA88FF>   +2 attack bonus, enemies denied Dex to AC, 50% miss chance.</color>");

        Debug.Log($"[GreaterInvisibility] {casterName} -> {recipient.Stats.CharacterName}: {durationRounds} rounds, breaksOnAttack=false");
        return true;
    }

}

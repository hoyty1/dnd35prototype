// ============================================================================
// GameManager_Spells_F.cs — Spell resolution methods starting with "F".
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
    //  FREEDOM OF MOVEMENT  (PHB p.233)
    // ================================================================
    // Touch. 10 min/level. Immune to paralysis, entanglement,
    // grapple penalties, and movement restrictions.

    private bool TryResolveFreedomOfMovementSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.FREEDOM_OF_MOVEMENT) return false;
        if (caster == null || caster.Stats == null || target == null || target.Stats == null) return false;
        if (!result.Success) return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel * 100; // 10 min/level = 100 rounds/level

        target.Stats.FreedomOfMovementActive = true;
        target.Stats.FreedomOfMovementRoundsRemaining = durationRounds;

        // Remove existing paralysis/entanglement
        if (target.HasCondition(CombatConditionType.Paralyzed))
            target.RemoveCondition(CombatConditionType.Paralyzed);
        if (target.HasCondition(CombatConditionType.Entangled))
            target.RemoveCondition(CombatConditionType.Entangled);

        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null) effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#88CCFF>🦅✨ Freedom of Movement! {casterName} grants {targetName} freedom from movement restrictions for {durationRounds} rounds.</color>");
        Debug.Log($"[FreedomOfMovement] {casterName} -> {targetName}: duration {durationRounds} rounds");

        result.BuffApplied = true;
        result.BuffDescription = $"Freedom of Movement ({durationRounds} rounds)";
        return true;
    }

    // ================================================================
    //  FLAME ARROW — PHB 3.5e p.231
    //  Transmutation [Fire]. Sor/Wiz 3.
    //  Targets up to 50 projectiles (arrows, bolts, sling bullets, or
    //  any other ammunition) in caster's inventory.
    //  Each deals +1d6 fire damage when shot.
    //  Duration: 10 min/level or until all charges discharged.
    //  Does NOT apply to versatile throwing weapons (daggers, javelins).
    // ================================================================

    private static bool IsFlameArrowSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.FLAME_ARROW, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Flame Arrow by finding all ammunition (arrows, bolts, sling bullets, etc.)
    /// in the caster's inventory and applying an ItemSpellEffect with BonusDamageDice="1d6",
    /// DamageType=fire. Works on any ItemType.Ammunition projectile; excludes versatile
    /// throwing weapons (items that can be both thrown and used in melee, e.g. daggers, javelins).
    /// </summary>
    private bool TryResolveFlameArrowSpell(CharacterController caster, SpellData spell)
    {
        if (!IsFlameArrowSpell(spell) || caster == null || caster.Stats == null)
            return false;

        var inventory = Combat_GetCharacterInventory(caster);
        if (inventory == null)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} has no inventory for Flame Arrow.");
            return true;
        }

        // Find all ammunition stacks in inventory (arrows, bolts, sling bullets, etc.)
        // Exclude versatile throwing weapons (IsThrown items that can also be used in melee)
        var ammoStacks = new List<ItemData>();
        if (inventory.GeneralSlots != null)
        {
            foreach (var item in inventory.GeneralSlots)
            {
                if (item != null && item.Type == ItemType.Ammunition && item.HasAmmoRemaining && !item.IsThrown)
                    ammoStacks.Add(item);
            }
        }

        if (ammoStacks.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} has no projectiles in inventory to enchant with Flame Arrow.");
            return true;
        }

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string casterName = caster.Stats.CharacterName;

        int totalEnchanted = 0;
        int maxProjectiles = 50;

        foreach (var ammo in ammoStacks)
        {
            if (totalEnchanted >= maxProjectiles)
                break;

            int toEnchant = Mathf.Min(ammo.Quantity, maxProjectiles - totalEnchanted);

            var effect = new ItemSpellEffect(spell.SpellId, spell.Name, casterName, casterLevel, durationRounds)
            {
                BonusDamageDice = "1d6",
                BonusDamageType = "fire",
                EnchantedAmmoRemaining = toEnchant
            };

            ammo.AddOrReplaceItemSpellEffect(effect);
            totalEnchanted += toEnchant;
        }

        CombatUI?.ShowCombatLog($"<color=#FF8844>🔥 {casterName} casts Flame Arrow — {totalEnchanted} projectiles now deal +1d6 fire damage [{durationRounds} rounds].</color>");
        Debug.Log($"[GameManager] Flame Arrow: {casterName} enchanted {totalEnchanted} projectiles with +1d6 fire, CL {casterLevel}, {durationRounds} rounds");

        UpdateAllStatsUI();
        return true;
    }

    // ================================================================
    //  FIRE SHIELD — Self Buff with Retribution Damage (PHB p.230)
    // ================================================================

    private static bool IsFireShieldSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.FIRE_SHIELD, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Fire Shield: self-buff with two modes (warm/chill).
    /// Warm Shield: 50% cold damage reduction, retribution 1d6+CL fire (max +15)
    /// Chill Shield: 50% fire damage reduction, retribution 1d6+CL cold (max +15)
    /// If the reduced damage source allowed a save for half, the protected
    /// character instead takes no damage from that source.
    /// Duration: 1 round/level (D). PHB p.230
    /// </summary>
    private bool TryResolveFireShieldSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (!IsFireShieldSpell(spell) || caster == null || caster.Stats == null)
            return false;

        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null)
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));

        // Use the player's shield-type choice; fall back to warm if not set (NPC casts)
        bool isWarmShield = _pendingFireShieldIsWarm ?? true;

        // Track via StatusEffectManager for duration and UI
        StatusEffectManager recipientStatusMgr = recipient.GetComponent<StatusEffectManager>();
        if (recipientStatusMgr == null)
            recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        recipientStatusMgr.Init(recipient.Stats);

        ActiveSpellEffect effect = recipientStatusMgr.AddEffect(
            spell,
            caster.Stats.CharacterName ?? spell.Name,
            casterLevel);

        // Store warm/chill flag and CL on the character for retribution + damage reduction
        recipient.Stats.FireShieldActive = true;
        recipient.Stats.FireShieldIsWarm = isWarmShield;
        recipient.Stats.FireShieldCasterLevel = casterLevel;
        recipient.Stats.FireShieldDurationRounds = durationRounds;

        // Register with the generic melee reaction service
        var fireShieldReaction = new FireShieldReactionEffect(recipient);
        MeleeReactionService.Register(fireShieldReaction);

        string resistType = isWarmShield ? "cold" : "fire";
        string retribType = isWarmShield ? "fire" : "cold";
        string shieldName = isWarmShield ? "Warm Shield" : "Chill Shield";
        int maxBonus = 15;

        CombatUI?.ShowCombatLog($"🔥 {recipient.Stats.CharacterName} is wreathed in {(isWarmShield ? "warm" : "chill")} flames! (Fire Shield — {shieldName})");
        CombatUI?.ShowCombatLog($"  {resistType} damage reduced by 50% (0 if save-for-half)");
        CombatUI?.ShowCombatLog($"  Retribution: 1d6+{Mathf.Min(casterLevel, maxBonus)} {retribType} damage to melee attackers");
        CombatUI?.ShowCombatLog($"  Duration: {durationRounds} round(s)");

        Debug.Log($"[FireShield] {recipient.Stats.CharacterName}: {shieldName}, CL {casterLevel}, {durationRounds} rounds");

        // Clear the pending choice
        _pendingFireShieldIsWarm = null;

        return true;
    }

    /// <summary>
    /// Called when a character with Fire Shield is struck by a melee attack.
    /// Deals retribution damage (1d6 + CL, max +15) to the attacker.
    /// No save for retribution damage. PHB p.230: triggers on any creature
    /// striking the defender with its body or a handheld weapon (includes reach).
    /// </summary>
    /// <summary>
    /// LEGACY method — kept for backward compatibility.
    /// All attack resolution code should use MeleeReactionService.TriggerReactions() instead.
    /// This method now delegates to the service for Fire Shield specifically.
    /// </summary>
    public void ResolveFireShieldRetribution(CharacterController defender, CharacterController attacker)
    {
        Debug.Log($"[FireShield] ResolveFireShieldRetribution (legacy redirect) | defender={defender?.Stats?.CharacterName ?? "null"} | attacker={attacker?.Stats?.CharacterName ?? "null"}");

        if (defender == null || defender.Stats == null || !defender.Stats.FireShieldActive)
            return;
        if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead)
            return;

        // Create a temporary effect instance and fire it directly.
        // In normal flow, the registered FireShieldReactionEffect handles this
        // through MeleeReactionService.TriggerReactions().
        var tempEffect = new FireShieldReactionEffect(defender);
        tempEffect.OnMeleeAttackHit(attacker, defender, null);
    }

}

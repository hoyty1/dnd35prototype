using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// D&D 3.5e Specific Magic Item Behavior Framework
// Pure C# (NOT MonoBehaviour) — behaviors attach to ItemData, not GameObjects.
// Combat hooks are called from CharacterController.PerformSingleAttackWithCrit
// and from Inventory.RecalculateStats for passive stat modifications.
// ============================================================================

/// <summary>
/// Base class for all specific magic item behaviors.
/// Subclasses implement combat hooks, stat modifications, and activated abilities.
/// Stored on ItemData.SpecificItemBehavior and invoked from CharacterController.
/// </summary>
public abstract class SpecificItemBehavior
{
    // Reference to the item data this behavior is attached to
    protected ItemData Item;

    // Reference to the wielder/wearer (set on equip, cleared on unequip)
    protected CharacterController Wielder;

    // Is this item currently equipped?
    protected bool IsEquipped;

    /// <summary>Display name for combat log messages.</summary>
    public virtual string DisplayName => Item?.Name ?? GetType().Name;

    // ========================================================================
    //  LIFECYCLE
    // ========================================================================

    /// <summary>Called when the behavior is first created and attached to an item.</summary>
    public virtual void Initialize(ItemData item)
    {
        Item = item;
    }

    /// <summary>Called when the item is equipped by a character.</summary>
    public virtual void OnEquip(CharacterController character)
    {
        Wielder = character;
        IsEquipped = true;
    }

    /// <summary>Called when the item is unequipped.</summary>
    public virtual void OnUnequip()
    {
        IsEquipped = false;
        Wielder = null;
    }

    // ========================================================================
    //  OFFENSIVE COMBAT HOOKS (called from PerformSingleAttackWithCrit)
    // ========================================================================

    /// <summary>
    /// Modify the attack bonus before the attack roll.
    /// Called after standard enchantment attack bonuses are calculated.
    /// </summary>
    /// <param name="target">The target being attacked.</param>
    /// <param name="attackBonus">Current attack bonus (modify in place).</param>
    /// <param name="logNotes">Append notes for combat log.</param>
    public virtual void OnPreAttackRoll(CharacterController target, ref int attackBonus, List<string> logNotes) { }

    /// <summary>
    /// Modify damage after base damage is rolled but before DR/resistance.
    /// </summary>
    /// <param name="target">The target taking damage.</param>
    /// <param name="damage">Current raw damage (modify in place).</param>
    /// <param name="isCrit">Whether this is a confirmed critical hit.</param>
    /// <param name="logNotes">Append notes for combat log.</param>
    public virtual void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes) { }

    /// <summary>
    /// Called when a critical hit is confirmed. Use for crit-triggered effects
    /// like Nine Lives Stealer death, Sword of Life Stealing negative level, etc.
    /// </summary>
    /// <param name="target">The target that was critically hit.</param>
    /// <param name="damage">The critical damage dealt.</param>
    /// <param name="logNotes">Append notes for combat log.</param>
    public virtual void OnCriticalHit(CharacterController target, int damage, List<string> logNotes) { }

    /// <summary>
    /// Called after damage is applied and target is confirmed alive.
    /// Use for on-hit effects like poison, negative levels, sleep, etc.
    /// </summary>
    /// <param name="target">The target that was hit.</param>
    /// <param name="finalDamage">The actual damage dealt after DR/resistance.</param>
    /// <param name="logNotes">Append notes for combat log.</param>
    public virtual void OnHitApplied(CharacterController target, int finalDamage, List<string> logNotes) { }

    /// <summary>
    /// Called when the wielder kills a target with this weapon.
    /// </summary>
    /// <param name="target">The target that was killed.</param>
    /// <param name="logNotes">Append notes for combat log.</param>
    public virtual void OnKill(CharacterController target, List<string> logNotes) { }

    // ========================================================================
    //  DEFENSIVE COMBAT HOOKS (called when wielder/wearer is attacked)
    // ========================================================================

    /// <summary>
    /// Modify the wielder/wearer's AC against an incoming attack.
    /// </summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="acBonus">Additional AC bonus to apply (modify in place).</param>
    /// <param name="logNotes">Append notes for combat log.</param>
    public virtual void OnDefendAgainstAttack(CharacterController attacker, ref int acBonus, List<string> logNotes) { }

    /// <summary>
    /// Called when an attack roll against the wielder/wearer has been made.
    /// Can force rerolls or negate hits.
    /// </summary>
    /// <param name="attackResult">The attack result.</param>
    /// <param name="forceReroll">Set true to force attacker to reroll.</param>
    /// <param name="logNotes">Append notes for combat log.</param>
    public virtual void OnAttackedBy(CombatResult attackResult, ref bool forceReroll, List<string> logNotes) { }

    // ========================================================================
    //  SPELL DEFENSE HOOKS (called from spell resolution)
    // ========================================================================

    /// <summary>
    /// Called when the wielder/wearer is targeted by a spell. Can absorb or negate the spell.
    /// Returns true if the spell was absorbed/negated and should not take effect.
    /// </summary>
    /// <param name="spellName">Name of the incoming spell.</param>
    /// <param name="spellLevel">Level of the incoming spell.</param>
    /// <param name="caster">The caster of the spell.</param>
    /// <param name="logNotes">Append notes for combat log.</param>
    public virtual bool OnSpellTargeted(string spellName, int spellLevel, CharacterController caster, List<string> logNotes)
    {
        return false; // By default, spells are not absorbed
    }

    // ========================================================================
    //  PASSIVE STAT MODIFICATIONS (called from Inventory.RecalculateStats)
    // ========================================================================

    /// <summary>
    /// Apply passive stat modifications when item is equipped.
    /// Called during stat recalculation. Use for save bonuses, skill bonuses, etc.
    /// </summary>
    /// <param name="stats">The character stats to modify.</param>
    public virtual void ApplyPassiveStatBonuses(CharacterStats stats) { }

    /// <summary>
    /// Remove passive stat modifications when item is unequipped.
    /// Must exactly reverse everything done in ApplyPassiveStatBonuses.
    /// </summary>
    /// <param name="stats">The character stats to un-modify.</param>
    public virtual void RemovePassiveStatBonuses(CharacterStats stats) { }

    // ========================================================================
    //  ACTIVATED ABILITIES (called from item use UI)
    // ========================================================================

    /// <summary>Whether this item has an activated ability that can be used right now.</summary>
    public virtual bool CanActivate()
    {
        return false;
    }

    /// <summary>
    /// Get a description of the activated ability for UI display.
    /// </summary>
    public virtual string GetActivateDescription()
    {
        return "";
    }

    /// <summary>
    /// Use the item's activated ability. Returns true if successfully used.
    /// </summary>
    /// <param name="target">Optional target for the ability.</param>
    /// <param name="logNotes">Append notes for combat log.</param>
    public virtual bool Activate(CharacterController target, List<string> logNotes)
    {
        return false;
    }

    // ========================================================================
    //  CHARGE / DAILY USE TRACKING
    // ========================================================================

    /// <summary>
    /// Reset daily use counters. Called on long rest.
    /// </summary>
    public virtual void OnLongRest() { }

    /// <summary>
    /// Get remaining uses for display (e.g., "2/3 uses remaining").
    /// Returns null if item has no limited uses.
    /// </summary>
    public virtual string GetUsesDisplay()
    {
        return null;
    }

    // ========================================================================
    //  TOOLTIP
    // ========================================================================

    /// <summary>
    /// Get tooltip text describing the item's current state and special properties.
    /// Override in subclasses to provide item-specific tooltip information.
    /// </summary>
    public virtual string GetTooltipText()
    {
        return "";
    }

    // ========================================================================
    //  HELPERS
    // ========================================================================

    /// <summary>Log a combat message via Debug.Log with consistent formatting.</summary>
    protected void Log(string message)
    {
        Debug.Log($"[SpecificItem:{DisplayName}] {message}");
    }

    /// <summary>
    /// Check if a target creature type matches a given type string (case-insensitive).
    /// </summary>
    protected bool IsCreatureType(CharacterController target, string creatureType)
    {
        if (target?.Stats == null) return false;
        string targetType = target.Stats.CreatureType ?? "";
        return targetType.Equals(creatureType, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if a target has any of the given creature types/subtypes.
    /// </summary>
    protected bool IsCreatureTypeAny(CharacterController target, params string[] types)
    {
        if (target?.Stats == null) return false;
        string targetType = target.Stats.CreatureType ?? "";
        foreach (var t in types)
        {
            if (targetType.Equals(t, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

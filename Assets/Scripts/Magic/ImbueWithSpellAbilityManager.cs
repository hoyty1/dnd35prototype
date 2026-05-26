// ============================================================================
// ImbueWithSpellAbilityManager.cs — Static helper for Imbue with Spell Ability
//
// Handles:
//   - Validation (target willing, non-caster, INT/WIS checks)
//   - Transferring prepared spells from caster to target
//   - Locking caster spell slots
//   - Target casting imbued spells
//   - Cleanup on dismissal / dispel / death
//
// D&D 3.5e PHB p.243
// ============================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

public static class ImbueWithSpellAbilityManager
{
    // ================================================================
    //  VALIDATION
    // ================================================================

    /// <summary>
    /// Checks whether the target is a valid recipient for Imbue with Spell Ability.
    /// Returns (isValid, errorReason).
    /// </summary>
    public static (bool isValid, string reason) ValidateTarget(CharacterController caster, CharacterController target)
    {
        if (target == null || target.Stats == null)
            return (false, "No valid target.");

        if (caster == null || caster.Stats == null)
            return (false, "No valid caster.");

        if (target == caster)
            return (false, "Cannot imbue yourself.");

        // Target must be a non-spellcaster
        var targetSpellComp = target.GetComponent<SpellcastingComponent>();
        if (targetSpellComp != null && targetSpellComp.CanCastSpells)
            return (false, $"{target.Stats.CharacterName} is already a spellcaster.");

        // Target must be willing (ally)
        if (target.IsPlayerControlled != caster.IsPlayerControlled)
            return (false, $"{target.Stats.CharacterName} is not a willing ally.");

        // Target must have INT ≥ 9 and WIS ≥ 9
        int targetInt = target.Stats.INT;
        int targetWis = target.Stats.WIS;
        if (targetInt < 9)
            return (false, $"{target.Stats.CharacterName} has INT {targetInt} (minimum 9 required).");
        if (targetWis < 9)
            return (false, $"{target.Stats.CharacterName} has WIS {targetWis} (minimum 9 required).");

        // Target already has active imbued spells
        if (target.Stats.ImbueWithSpellAbilityTargetActive)
            return (false, $"{target.Stats.CharacterName} already has imbued spells active.");

        // Caster must be a cleric with prepared slots
        if (!caster.Stats.IsCleric)
            return (false, $"{caster.Stats.CharacterName} is not a Cleric.");

        // Check caster already has imbue active
        if (caster.Stats.ImbueWithSpellAbilityCasterActive)
            return (false, $"{caster.Stats.CharacterName} already has an active Imbue with Spell Ability.");

        return (true, string.Empty);
    }

    /// <summary>
    /// Returns the maximum spell level the target can receive.
    /// PHB p.243: 1st-level only if WIS ≤ 12; up to 2nd-level if WIS ≥ 13.
    /// </summary>
    public static int GetMaxImbuableLevel(CharacterController target)
    {
        if (target == null || target.Stats == null) return 0;
        int wis = target.Stats.WIS;
        return wis >= 13 ? 2 : 1;
    }

    /// <summary>
    /// Returns available 1st-level (and optionally 2nd-level) prepared spells the caster
    /// can transfer. Only non-domain, non-used slots with prepared spells.
    /// </summary>
    public static List<(SpellSlot slot, int index)> GetTransferableSlots(
        CharacterController caster, int maxLevel)
    {
        var result = new List<(SpellSlot, int)>();
        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null) return result;

        for (int i = 0; i < spellComp.SpellSlots.Count; i++)
        {
            var slot = spellComp.SpellSlots[i];
            if (slot == null) continue;
            if (slot.Level < 1 || slot.Level > maxLevel) continue;
            if (!slot.CanCast) continue;
            if (slot.LockedByImbue) continue;
            // Domain slots can be transferred per RAW (no restriction)
            result.Add((slot, i));
        }

        return result;
    }

    // ================================================================
    //  SPELL TRANSFER (called after caster selects spells to imbue)
    // ================================================================

    /// <summary>
    /// Transfers the selected spell slots from caster to target.
    /// Locks caster slots and creates ImbueSpellEntry on target.
    /// Maximum 3 spells transferred.
    /// </summary>
    public static void TransferSpells(
        CharacterController caster,
        CharacterController target,
        List<int> selectedSlotIndices)
    {
        if (caster == null || target == null) return;
        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null) return;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int wisBonus = caster.Stats.WISMod;
        string casterName = caster.Stats.CharacterName ?? "Unknown";

        // Clear previous state just in case
        target.Stats.ImbuedSpells.Clear();
        caster.Stats.ImbueLockedSlotIndices.Clear();

        int transferred = 0;
        foreach (int idx in selectedSlotIndices)
        {
            if (transferred >= 3) break;
            if (idx < 0 || idx >= spellComp.SpellSlots.Count) continue;

            var slot = spellComp.SpellSlots[idx];
            if (slot == null || !slot.CanCast || slot.LockedByImbue) continue;

            SpellData spell = slot.PreparedSpell;
            if (spell == null) continue;

            // Calculate DC: 10 + spell level + WIS modifier
            int saveDC = 10 + spell.SpellLevel + wisBonus;

            var entry = new ImbueSpellEntry(spell, casterLevel, saveDC, idx, casterName);
            target.Stats.ImbuedSpells.Add(entry);

            // Lock the caster's slot
            slot.LockedByImbue = true;
            caster.Stats.ImbueLockedSlotIndices.Add(idx);

            transferred++;
            Debug.Log($"[ImbueWithSpellAbility] Transferred '{spell.Name}' (slot #{idx}) from {casterName} to {target.Stats.CharacterName}. CL={casterLevel}, DC={saveDC}");
        }

        if (transferred > 0)
        {
            // Set active flags
            caster.Stats.ImbueWithSpellAbilityCasterActive = true;
            caster.Stats.ImbueTarget = target;

            target.Stats.ImbueWithSpellAbilityTargetActive = true;
            target.Stats.ImbueCaster = caster;

            // Sync caster's slot counts
            spellComp.SyncPreparedSpellsFromSlots();

            Debug.Log($"[ImbueWithSpellAbility] Transfer complete: {transferred} spell(s) from {casterName} to {target.Stats.CharacterName}");
        }
        else
        {
            Debug.LogWarning("[ImbueWithSpellAbility] No spells were transferred!");
        }
    }

    // ================================================================
    //  TARGET CASTING AN IMBUED SPELL
    // ================================================================

    /// <summary>
    /// Called when the target casts one of their imbued spells.
    /// Returns the ImbueSpellEntry to be resolved, or null if not found.
    /// Removes the entry from the target's list and unlocks the caster's slot.
    /// </summary>
    public static ImbueSpellEntry CastImbuedSpell(CharacterController target, string spellId)
    {
        if (target == null || target.Stats == null) return null;
        if (!target.Stats.ImbueWithSpellAbilityTargetActive) return null;

        var entry = target.Stats.ImbuedSpells.FirstOrDefault(
            e => e.Spell != null && e.Spell.SpellId == spellId);
        if (entry == null)
        {
            Debug.LogWarning($"[ImbueWithSpellAbility] {target.Stats.CharacterName} tried to cast imbued spell '{spellId}' but it was not found.");
            return null;
        }

        // Remove from target's list
        target.Stats.ImbuedSpells.Remove(entry);
        Debug.Log($"[ImbueWithSpellAbility] {target.Stats.CharacterName} casts imbued '{entry.Spell.Name}'. {target.Stats.ImbuedSpells.Count} imbued spell(s) remaining.");

        // Unlock the caster's slot
        UnlockCasterSlot(target.Stats.ImbueCaster, entry.LockedSlotIndex);

        // If no more imbued spells remain, end the effect entirely
        if (target.Stats.ImbuedSpells.Count == 0)
        {
            Debug.Log($"[ImbueWithSpellAbility] All imbued spells discharged. Ending effect.");
            EndImbueEffect(target.Stats.ImbueCaster, target, "all spells discharged");
        }

        return entry;
    }

    /// <summary>
    /// Returns true if the target has any imbued spells available to cast.
    /// </summary>
    public static bool HasImbuedSpells(CharacterController character)
    {
        return character != null &&
               character.Stats != null &&
               character.Stats.ImbueWithSpellAbilityTargetActive &&
               character.Stats.ImbuedSpells.Count > 0;
    }

    /// <summary>
    /// Returns the list of imbued spell entries for display in the action menu.
    /// </summary>
    public static List<ImbueSpellEntry> GetImbuedSpells(CharacterController character)
    {
        if (!HasImbuedSpells(character)) return new List<ImbueSpellEntry>();
        return new List<ImbueSpellEntry>(character.Stats.ImbuedSpells);
    }

    // ================================================================
    //  CLEANUP / DISMISSAL / DISPEL
    // ================================================================

    /// <summary>
    /// Unlocks a specific slot on the caster.
    /// </summary>
    private static void UnlockCasterSlot(CharacterController caster, int slotIndex)
    {
        if (caster == null) return;
        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null) return;

        if (slotIndex >= 0 && slotIndex < spellComp.SpellSlots.Count)
        {
            var slot = spellComp.SpellSlots[slotIndex];
            if (slot != null && slot.LockedByImbue)
            {
                slot.LockedByImbue = false;
                // The slot is now available for re-preparation after rest, but still "used"
                // per D&D 3.5e rules: the spell was consumed when imbued.
                Debug.Log($"[ImbueWithSpellAbility] Unlocked caster slot #{slotIndex} ({slot})");
            }
        }

        caster.Stats.ImbueLockedSlotIndices.Remove(slotIndex);
        spellComp.SyncPreparedSpellsFromSlots();
    }

    /// <summary>
    /// Ends the Imbue with Spell Ability effect completely.
    /// Called when: all spells discharged, spell dismissed, dispelled, or either party dies.
    /// Unlocks all remaining caster slots and clears target imbued spells.
    /// </summary>
    public static void EndImbueEffect(CharacterController caster, CharacterController target, string reason)
    {
        Debug.Log($"[ImbueWithSpellAbility] Ending effect — reason: {reason}");

        // Unlock caster slots
        if (caster != null && caster.Stats != null)
        {
            var spellComp = caster.GetComponent<SpellcastingComponent>();
            if (spellComp != null)
            {
                foreach (int idx in caster.Stats.ImbueLockedSlotIndices)
                {
                    if (idx >= 0 && idx < spellComp.SpellSlots.Count)
                    {
                        var slot = spellComp.SpellSlots[idx];
                        if (slot != null)
                        {
                            slot.LockedByImbue = false;
                            Debug.Log($"[ImbueWithSpellAbility] Unlocked slot #{idx} ({slot})");
                        }
                    }
                }
                spellComp.SyncPreparedSpellsFromSlots();
            }

            caster.Stats.ImbueWithSpellAbilityCasterActive = false;
            caster.Stats.ImbueTarget = null;
            caster.Stats.ImbueLockedSlotIndices.Clear();

            // Remove status effect from caster
            var casterStatusMgr = caster.GetComponent<StatusEffectManager>();
            casterStatusMgr?.RemoveEffectsBySpellId(SpellNames.IMBUE_WITH_SPELL_ABILITY);
        }

        // Clear target imbued spells
        if (target != null && target.Stats != null)
        {
            int remainingCount = target.Stats.ImbuedSpells.Count;
            target.Stats.ImbuedSpells.Clear();
            target.Stats.ImbueWithSpellAbilityTargetActive = false;
            target.Stats.ImbueCaster = null;

            // Remove status effect from target
            var targetStatusMgr = target.GetComponent<StatusEffectManager>();
            targetStatusMgr?.RemoveEffectsBySpellId(SpellNames.IMBUE_WITH_SPELL_ABILITY);

            if (remainingCount > 0)
                Debug.Log($"[ImbueWithSpellAbility] {remainingCount} unused imbued spell(s) lost.");
        }
    }

    /// <summary>
    /// Called when a character dies. If they were part of an imbue link, ends the effect.
    /// </summary>
    public static void HandleDeath(CharacterController deadCharacter)
    {
        if (deadCharacter == null || deadCharacter.Stats == null) return;

        // Check if dead character was a caster
        if (deadCharacter.Stats.ImbueWithSpellAbilityCasterActive)
        {
            var target = deadCharacter.Stats.ImbueTarget;
            EndImbueEffect(deadCharacter, target, $"{deadCharacter.Stats.CharacterName} (caster) died");
        }

        // Check if dead character was a target
        if (deadCharacter.Stats.ImbueWithSpellAbilityTargetActive)
        {
            var caster = deadCharacter.Stats.ImbueCaster;
            EndImbueEffect(caster, deadCharacter, $"{deadCharacter.Stats.CharacterName} (target) died");
        }
    }

    /// <summary>
    /// Called when the effect is dismissed voluntarily by the caster.
    /// </summary>
    public static void Dismiss(CharacterController caster)
    {
        if (caster == null || !caster.Stats.ImbueWithSpellAbilityCasterActive) return;
        var target = caster.Stats.ImbueTarget;
        EndImbueEffect(caster, target, "dismissed by caster");
    }
}

using UnityEngine;
using DND35e.Identifiers;

// ════════════════════════════════════════════════════════════════════════════
//  Counterspell Manager — D&D 3.5e Sprint 3 Ring of Counterspells System
//
//  DMG p.230: "This ring might seem to be a Ring of Spell Storing, Minor,
//  until it is fully identified. When a spell is cast into the ring, it
//  cannot be cast from the ring; instead, it serves as a counter to that
//  specific spell. The ring can hold only one spell at a time. When that
//  spell is cast upon the wearer, the stored spell counters it automatically."
//
//  Key rules:
//  - Holds ONE spell, max 6th level
//  - Spell must be cast INTO the ring by a spellcaster (consumes the spell slot)
//  - When the stored spell is cast on the wearer, it counters automatically
//  - No action required from the wearer — it's instantaneous and automatic
//  - After countering, the ring is empty and must be reloaded
//  - Only counters the EXACT same spell (not similar or same-school spells)
//  - DMG clarification: "self-only" spells don't trigger it (e.g., Shield)
// ════════════════════════════════════════════════════════════════════════════

public static class CounterspellManager
{
    private const int MAX_COUNTERSPELL_LEVEL = 6;

    // ── Check: can this spell be stored in a Ring of Counterspells? ──
    public static bool CanStoreSpell(int spellLevel)
    {
        return spellLevel >= 1 && spellLevel <= MAX_COUNTERSPELL_LEVEL;
    }

    // ── Store a spell in the ring ──
    /// <summary>
    /// Store a spell in the Ring of Counterspells. Consumes the caster's spell slot.
    /// Returns true on success.
    /// </summary>
    public static bool StoreCounterspell(ItemData ring, string spellId, string spellName, int spellLevel, string casterName)
    {
        if (ring == null || string.IsNullOrEmpty(spellId)) return false;
        if (ring.RingId != RingNames.RING_OF_COUNTERSPELLS) return false;

        if (spellLevel < 1 || spellLevel > MAX_COUNTERSPELL_LEVEL)
        {
            Debug.Log($"[RingCounterspell] Cannot store level {spellLevel} spell — max is {MAX_COUNTERSPELL_LEVEL}.");
            if (GameManager.Instance != null)
                GameManager.Instance.CombatUI?.ShowCombatLog($"<color=#FF6666>❌ {spellName} (level {spellLevel}) is too high for Ring of Counterspells (max level {MAX_COUNTERSPELL_LEVEL}).</color>");
            return false;
        }

        if (!string.IsNullOrEmpty(ring.RingCounterspellStored))
        {
            Debug.Log($"[RingCounterspell] Ring already holds a counterspell: {ring.RingCounterspellStoredName}");
            if (GameManager.Instance != null)
                GameManager.Instance.CombatUI?.ShowCombatLog($"<color=#FF6666>❌ Ring of Counterspells already holds {ring.RingCounterspellStoredName}. Remove it first.</color>");
            return false;
        }

        ring.RingCounterspellStored = spellId;
        ring.RingCounterspellStoredName = spellName;
        ring.RingCounterspellStoredLevel = spellLevel;

        string msg = $"💍 {casterName} stores {spellName} (level {spellLevel}) in the Ring of Counterspells.";
        Debug.Log($"[RingCounterspell] {msg}");
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(msg);

        return true;
    }

    // ── Remove stored spell (manual clearing) ──
    public static void ClearStoredCounterspell(ItemData ring)
    {
        if (ring == null) return;
        ring.RingCounterspellStored = "";
        ring.RingCounterspellStoredName = "";
        ring.RingCounterspellStoredLevel = 0;
    }

    // ── Check: does wearer's Ring of Counterspells counter this incoming spell? ──
    /// <summary>
    /// Check all rings equipped on the target. If a Ring of Counterspells holds
    /// the same spell being cast, automatically counter it and consume the stored spell.
    /// Returns true if the spell was countered.
    /// </summary>
    public static bool TryRingCounterspell(CharacterController target, SpellData incomingSpell)
    {
        if (target == null || incomingSpell == null) return false;

        // Get target's inventory
        var invComp = target.Inventory;
        if (invComp == null) return false;
        var inv = invComp.GetInventory();
        if (inv == null) return false;

        // Check both ring slots
        ItemData counterspellRing = null;
        if (inv.LeftRingSlot != null && IsMatchingCounterspellRing(inv.LeftRingSlot, incomingSpell.SpellId))
            counterspellRing = inv.LeftRingSlot;
        else if (inv.RightRingSlot != null && IsMatchingCounterspellRing(inv.RightRingSlot, incomingSpell.SpellId))
            counterspellRing = inv.RightRingSlot;

        if (counterspellRing == null) return false;

        // Counter the spell!
        string targetName = target.Stats?.CharacterName ?? "Unknown";
        string storedName = counterspellRing.RingCounterspellStoredName;

        // Consume the stored spell
        ClearStoredCounterspell(counterspellRing);

        string msg = $"<color=#FFD700>💍✨ {targetName}'s Ring of Counterspells automatically counters {incomingSpell.Name}!</color>\n" +
                     $"  The stored {storedName} negates the incoming spell. Ring is now empty.";
        Debug.Log($"[RingCounterspell] Auto-countered {incomingSpell.Name} on {targetName}");
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(msg);

        return true;
    }

    // ── Helper: does this ring match the incoming spell? ──
    private static bool IsMatchingCounterspellRing(ItemData ring, string incomingSpellId)
    {
        if (ring == null || !ring.IsRing) return false;
        if (ring.RingId != RingNames.RING_OF_COUNTERSPELLS) return false;
        if (string.IsNullOrEmpty(ring.RingCounterspellStored)) return false;

        // Exact spell match required per DMG p.230
        return string.Equals(ring.RingCounterspellStored, incomingSpellId, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Check if a ring has a counterspell stored (for UI/tooltip display).
    /// </summary>
    public static bool HasStoredCounterspell(ItemData ring)
    {
        return ring != null && ring.RingId == RingNames.RING_OF_COUNTERSPELLS
            && !string.IsNullOrEmpty(ring.RingCounterspellStored);
    }

    /// <summary>
    /// Get display string for the stored counterspell (for tooltips).
    /// </summary>
    public static string GetStoredCounterspellDisplay(ItemData ring)
    {
        if (!HasStoredCounterspell(ring)) return "Empty — no counterspell stored";
        return $"Loaded: {ring.RingCounterspellStoredName} (level {ring.RingCounterspellStoredLevel})";
    }
}

using UnityEngine;

// ════════════════════════════════════════════════════════════════════
//  Ring Charge Manager — D&D 3.5e Sprint 2 Active Ring System
//  Manages charge pools for charge-based rings (Ring of the Ram).
//  DMG p.233: Ring of the Ram — 50 charges, regain 1d10 per day.
// ════════════════════════════════════════════════════════════════════

/// <summary>
/// Manages charge consumption and regeneration for charge-based rings.
/// Currently supports Ring of the Ram (50 charges, regen 1d10/day).
/// Charges are stored directly on ItemData fields.
/// </summary>
public static class RingChargeManager
{
    /// <summary>
    /// Check if a ring has enough charges for the specified cost.
    /// </summary>
    public static bool HasCharges(ItemData ring, int cost)
    {
        if (ring == null) return false;
        return ring.RingCurrentCharges >= cost;
    }

    /// <summary>
    /// Consume charges from a ring. Returns true if successful.
    /// </summary>
    public static bool ConsumeCharges(ItemData ring, int cost)
    {
        if (ring == null || ring.RingCurrentCharges < cost)
            return false;

        ring.RingCurrentCharges -= cost;
        Debug.Log($"[RingChargeManager] {ring.Name}: consumed {cost} charge(s). Remaining: {ring.RingCurrentCharges}/{ring.RingMaxCharges}");
        return true;
    }

    /// <summary>
    /// Regenerate charges on rest. Each charge-based ring regenerates
    /// up to its RingChargesPerDay value.
    /// Ring of the Ram: regains 1d10 charges per day (DMG p.233).
    /// </summary>
    public static void RegenerateCharges(ItemData ring)
    {
        if (ring == null || ring.RingMaxCharges <= 0 || ring.RingChargesPerDay <= 0)
            return;

        if (ring.RingCurrentCharges >= ring.RingMaxCharges)
            return;

        // Roll 1d(ChargesPerDay) for regeneration
        // Ring of Ram: RingChargesPerDay = 10, so rolls 1d10
        int regained = Random.Range(1, ring.RingChargesPerDay + 1);
        int oldCharges = ring.RingCurrentCharges;
        ring.RingCurrentCharges = Mathf.Min(ring.RingMaxCharges, ring.RingCurrentCharges + regained);
        int actualRegained = ring.RingCurrentCharges - oldCharges;

        Debug.Log($"[RingChargeManager] {ring.Name}: regenerated {actualRegained} charges (rolled {regained}). Now: {ring.RingCurrentCharges}/{ring.RingMaxCharges}");
    }

    /// <summary>
    /// Get display string for charge status.
    /// </summary>
    public static string GetChargeDisplayString(ItemData ring)
    {
        if (ring == null || ring.RingMaxCharges <= 0)
            return "";
        return $"{ring.RingCurrentCharges}/{ring.RingMaxCharges} charges";
    }
}

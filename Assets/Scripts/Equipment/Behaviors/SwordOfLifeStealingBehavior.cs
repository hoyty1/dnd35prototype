using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Sword of Life Stealing (SRD / DMG p.228)
//
// +2 longsword. On a confirmed critical hit against a living creature:
//   - Bestows 1 negative level on the target.
//   - Wielder gains 1d6 temporary HP (last 24 hours; capped at max HP).
//   - Fort DC 16 to remove the negative level after 24 hours.
//
// The negative level effect is a death effect (Fort DC 16 to remove).
// Does NOT apply to undead, constructs, or other creatures immune to
// energy drain / negative levels.
// ============================================================================

/// <summary>
/// Sword of Life Stealing specific item behavior.
/// Bestows negative levels on critical hits and grants temp HP to wielder.
/// </summary>
public class SwordOfLifeStealingBehavior : SpecificItemBehavior
{
    // SRD: Fort DC 16 to remove the negative level after 24 hours
    private const int NegativeLevelRemovalDC = 16;

    public override string DisplayName => "Sword of Life Stealing";

    /// <summary>
    /// On confirmed critical hit: bestow 1 negative level on living targets,
    /// and grant 1d6 temporary HP to the wielder.
    /// </summary>
    public override void OnCriticalHit(CharacterController target, int damage, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;
        if (!IsEquipped || Wielder == null) return;

        // Only works against living creatures — not undead, constructs, or those immune
        string creatureType = target.Stats.CreatureType ?? "";
        if (creatureType.Equals("Undead", System.StringComparison.OrdinalIgnoreCase) ||
            creatureType.Equals("Construct", System.StringComparison.OrdinalIgnoreCase))
        {
            logNotes.Add($"💀 Sword of Life Stealing: {target.Stats.CharacterName} is {creatureType} — no life stealing effect.");
            return;
        }

        // D&D 3.5e: Creatures immune to critical hits are also immune to energy drain from weapons
        if (target.Stats.Immunities != null && target.Stats.Immunities.immuneToCriticalHits)
        {
            logNotes.Add($"💀 Sword of Life Stealing: {target.Stats.CharacterName} is immune to the life stealing effect.");
            return;
        }

        // Bestow 1 negative level
        target.ApplyNegativeLevels(1, "Sword of Life Stealing");
        logNotes.Add($"💀 <color=#8B0000>Sword of Life Stealing</color> bestows a negative level on {target.Stats.CharacterName}! (Fort DC {NegativeLevelRemovalDC} to remove after 24 hrs)");
        Log($"Negative level bestowed on {target.Stats.CharacterName}");

        // Grant 1d6 temporary HP to wielder
        int tempHP = DiceService.D6("Sword of Life Stealing temp HP");
        if (Wielder.Stats != null)
        {
            Wielder.Stats.TempHP = Mathf.Max(Wielder.Stats.TempHP, tempHP);
            logNotes.Add($"💀 <color=#8B0000>{Wielder.Stats.CharacterName}</color> gains {tempHP} temporary HP from life stealing!");
            Log($"Wielder gains {tempHP} temp HP");
        }
    }
}

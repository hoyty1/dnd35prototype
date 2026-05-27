using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Sword of the Planes (SRD / DMG p.228)
//
// +1 longsword with variable enhancement based on the current plane and target:
//   - Material Plane: +1 enhancement (base)
//   - Transitive Planes (Astral/Ethereal/Shadow): +2 enhancement
//   - Inner Planes (Elemental/Energy): +3 enhancement
//   - Outer Planes: +4 enhancement
//
// Additionally gains bonus vs extraplanar creatures (approximated by type):
//   - Outsider, Elemental → +1 extra enhancement
//   - Celestial, Fiend, Demon, Devil, Angel, Archon → +2 extra
//
// 1/day: Plane Shift (as spell) — transport wielder and allies to another plane.
//
// CL 15, 22,315 gp.
// ============================================================================

/// <summary>
/// Sword of the Planes specific item behavior.
/// Variable enhancement based on current plane + creature type.
/// Includes Plane Shift activated ability (1/day).
/// </summary>
public class SwordOfThePlanesBehavior : SpecificItemBehavior
{
    private const int BaseEnhancement = 1;
    private int _planeShiftUsesRemaining = 1;

    public override string DisplayName => "Sword of the Planes";

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _planeShiftUsesRemaining = 1;
    }

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);

        PlaneType currentPlane = GameManager.Instance?.CurrentPlane ?? PlaneType.Material;
        int totalEnh = BaseEnhancement + GetPlaneBonus(currentPlane);
        string planeName = PlaneHelper.GetDisplayName(currentPlane);

        GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.SpellResistance("🌌", $"Sword of the Planes attunes to {character.Stats?.CharacterName} — +{totalEnh} on {planeName}"));
        Log($"Equipped on {planeName}: effective +{totalEnh}");
    }

    // ========================================================================
    //  ATTACK ROLL: Plane-based enhancement + creature type bonus
    // ========================================================================

    public override void OnPreAttackRoll(CharacterController target, ref int attackBonus, List<string> logNotes)
    {
        int planeBonus = GetPlaneBonus(GameManager.Instance?.CurrentPlane ?? PlaneType.Material);
        int creatureBonus = GetExtraEnhancementByCreatureType(target);
        int totalBonus = planeBonus + creatureBonus;

        if (totalBonus > 0)
        {
            attackBonus += totalBonus;
            var parts = new List<string>();
            if (planeBonus > 0)
                parts.Add($"+{planeBonus} plane");
            if (creatureBonus > 0)
                parts.Add($"+{creatureBonus} vs {target?.Stats?.CreatureType ?? "extraplanar"}");
            logNotes?.Add($"🌌 Sword of the Planes: {string.Join(", ", parts)} attack");
        }
    }

    // ========================================================================
    //  DAMAGE ROLL: Same plane-based + creature type bonus
    // ========================================================================

    public override void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes)
    {
        int planeBonus = GetPlaneBonus(GameManager.Instance?.CurrentPlane ?? PlaneType.Material);
        int creatureBonus = GetExtraEnhancementByCreatureType(target);
        int totalBonus = planeBonus + creatureBonus;

        if (totalBonus > 0)
        {
            damage += totalBonus;
            logNotes?.Add($"🌌 Sword of the Planes: +{totalBonus} damage");
        }
    }

    // ========================================================================
    //  ACTIVATED: Plane Shift (1/day)
    // ========================================================================

    public override bool CanActivate()
    {
        return IsEquipped && _planeShiftUsesRemaining > 0;
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (_planeShiftUsesRemaining <= 0)
        {
            logNotes?.Add("🌌 Sword of the Planes: Plane Shift already used today.");
            return false;
        }

        _planeShiftUsesRemaining--;

        PlaneType currentPlane = GameManager.Instance?.CurrentPlane ?? PlaneType.Material;
        string wielderName = Wielder?.Stats?.CharacterName ?? "Wielder";

        // Determine destination: toggle between Material and a preset extraplanar destination
        PlaneType destination;
        if (currentPlane == PlaneType.Material)
        {
            // Default to Astral when leaving Material (most common transit plane)
            destination = PlaneType.Astral;
        }
        else
        {
            // Return to Material Plane
            destination = PlaneType.Material;
        }

        string fromName = PlaneHelper.GetDisplayName(currentPlane);
        string toName = PlaneHelper.GetDisplayName(destination);

        logNotes?.Add($"🌌 <color=#9370DB>{wielderName} activates PLANE SHIFT!</color>");
        logNotes?.Add($"🌌 <color=#FF00FF>Shifting from {fromName} to {toName}...</color>");
        Log($"Plane Shift: {fromName} → {toName}");

        // Execute the plane shift
        GameManager.Instance?.SetCurrentPlane(destination);

        // Report new enhancement
        int newTotal = BaseEnhancement + GetPlaneBonus(destination);
        logNotes?.Add($"🌌 <color=#00FF00>Arrived on {toName}! Sword now functions as +{newTotal}</color>");

        return true;
    }

    public override string GetActivateDescription()
    {
        if (_planeShiftUsesRemaining <= 0)
            return "Plane Shift: already used today.";

        PlaneType currentPlane = GameManager.Instance?.CurrentPlane ?? PlaneType.Material;
        string currentName = PlaneHelper.GetDisplayName(currentPlane);

        if (currentPlane == PlaneType.Material)
            return $"Plane Shift (1/day): Travel to another plane. Currently on {currentName}.";
        return $"Plane Shift (1/day): Return to Material Plane. Currently on {currentName}.";
    }

    public override string GetUsesDisplay()
    {
        PlaneType currentPlane = GameManager.Instance?.CurrentPlane ?? PlaneType.Material;
        int totalEnh = BaseEnhancement + GetPlaneBonus(currentPlane);
        string planeLabel = currentPlane == PlaneType.Material ? "Material" : PlaneHelper.GetDisplayName(currentPlane);

        if (_planeShiftUsesRemaining > 0)
            return $"+{totalEnh} ({planeLabel}) | Shift: ready";
        return $"+{totalEnh} ({planeLabel}) | Shift: used";
    }

    public override void OnLongRest()
    {
        base.OnLongRest();
        _planeShiftUsesRemaining = 1;
        Log("Plane Shift use refreshed");
    }

    // ========================================================================
    //  HELPERS: Calculate enhancement bonuses
    // ========================================================================

    /// <summary>
    /// Extra enhancement bonus from being on a non-Material plane.
    /// Material = +0, Transitive = +1, Inner = +2, Outer = +3.
    /// </summary>
    private int GetPlaneBonus(PlaneType plane)
    {
        if (plane == PlaneType.Material) return 0;
        if (PlaneHelper.IsTransitivePlane(plane)) return 1;  // +2 total
        if (PlaneHelper.IsInnerPlane(plane)) return 2;        // +3 total
        if (PlaneHelper.IsOuterPlane(plane)) return 3;        // +4 total
        return 0;
    }

    /// <summary>
    /// Extra enhancement bonus based on the target's creature type.
    /// Outsiders/Elementals = +1, Celestials/Fiends = +2.
    /// Stacks with plane bonus but capped so total extra doesn't exceed +3.
    /// </summary>
    private int GetExtraEnhancementByCreatureType(CharacterController target)
    {
        if (target?.Stats == null) return 0;

        // +2 extra vs celestials/fiends (powerful extraplanar beings)
        if (IsCreatureTypeAny(target, "Celestial", "Fiend", "Demon", "Devil", "Angel", "Archon"))
            return 2;

        // +1 extra vs outsiders/elementals
        if (IsCreatureTypeAny(target, "Outsider", "Elemental"))
            return 1;

        return 0;
    }

    // ========================================================================
    //  TOOLTIP
    // ========================================================================

    public string GetTooltipText()
    {
        var lines = new List<string>();
        PlaneType currentPlane = GameManager.Instance?.CurrentPlane ?? PlaneType.Material;
        int totalEnh = BaseEnhancement + GetPlaneBonus(currentPlane);

        lines.Add($"<b>Sword of the Planes</b> (+{totalEnh} longsword)");
        lines.Add($"Current plane: {PlaneHelper.GetDisplayName(currentPlane)}");
        lines.Add("Enhancement: +1 Material, +2 Transitive, +3 Inner, +4 Outer");
        lines.Add("Bonus vs extraplanar creatures: +1 Outsider/Elemental, +2 Celestial/Fiend");
        lines.Add($"Plane Shift: {(_planeShiftUsesRemaining > 0 ? "available" : "used today")} (1/day)");
        return string.Join("\n", lines);
    }
}

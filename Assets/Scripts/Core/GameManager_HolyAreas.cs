// ============================================================================
// GameManager_HolyAreas.cs — Resolution logic for Consecrate and Desecrate
// persistent area effects (PHB 3.5e).
// Tracks holy/unholy ground areas on the grid and applies undead penalties/
// bonuses and turning check modifiers.
// Part of the GameManager partial class.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;

public partial class GameManager
{
    // ================================================================
    //  Active area effect tracking for Consecrate / Desecrate
    // ================================================================

    /// <summary>Tracks a Consecrate or Desecrate area effect on the grid.</summary>
    private class HolyAreaEffect
    {
        public string SpellId;           // SpellNames.CONSECRATE or DESECRATE
        public CharacterController Caster;
        public int CasterLevel;
        public HashSet<Vector2Int> AffectedCells;
        public int RoundsRemaining;      // -1 = effectively permanent for combat duration
        public bool IsConsecrate => SpellId == SpellNames.CONSECRATE;
    }

    private readonly List<HolyAreaEffect> _activeHolyAreas = new List<HolyAreaEffect>();

    // ================================================================
    //  CONSECRATE — PHB p.212
    //  Evocation [Good]. Cleric 2. V, S, M (vial of holy water), DF.
    //  Area: 20-ft-radius emanation. Duration: 2 hr/level.
    //  Undead in area: -1 profane penalty to attacks, damage, saves.
    //  Turning checks in area: +3 sacred bonus.
    //  Counters/dispels Desecrate.
    // ================================================================

    private bool TryResolveConsecrateAreaEffect(
        CharacterController caster, SpellData spell,
        HashSet<Vector2Int> aoeCells, List<CharacterController> targets, out string log)
    {
        log = string.Empty;
        if (spell == null || spell.SpellId != SpellNames.CONSECRATE)
            return false;

        if (caster == null || caster.Stats == null || aoeCells == null)
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);

        // Remove any existing Desecrate areas that overlap
        RemoveOverlappingHolyAreas(aoeCells, SpellNames.DESECRATE);

        // Create the area effect
        var area = new HolyAreaEffect
        {
            SpellId = SpellNames.CONSECRATE,
            Caster = caster,
            CasterLevel = casterLevel,
            AffectedCells = new HashSet<Vector2Int>(aoeCells),
            RoundsRemaining = -1 // 2 hr/level — effectively permanent for combat
        };
        _activeHolyAreas.Add(area);

        // Apply effects to undead currently in the area
        int affectedCount = 0;
        StringBuilder sb = new StringBuilder();
        sb.Append($"<color=#FFFF99>✨ {caster.Stats.CharacterName} casts Consecrate! Positive energy fills the area.</color>\n");

        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t?.Stats == null) continue;
                if (IsUndead(t))
                {
                    ApplyConsecrateDebuffs(t);
                    affectedCount++;
                    sb.Append($"  💀 {t.Stats.CharacterName} (undead) suffers -1 to attacks, damage, and saves!\n");
                }
            }
        }

        sb.Append($"  +3 sacred bonus to turning checks in the area.");
        log = sb.ToString();

        Debug.Log($"[Consecrate] Area created by {caster.Stats.CharacterName}, {aoeCells.Count} cells, {affectedCount} undead affected");
        return true;
    }

    // ================================================================
    //  DESECRATE — PHB p.218
    //  Evocation [Evil]. Cleric 2. V, S, M (vial of unholy water), DF.
    //  Area: 20-ft-radius emanation. Duration: 2 hr/level.
    //  Undead in area: +1 profane bonus to attacks, damage, saves.
    //  Turning checks in area: -3 profane penalty.
    //  Undead created in area gain +1 HP per HD.
    //  Counters/dispels Consecrate.
    // ================================================================

    private bool TryResolveDesecrateAreaEffect(
        CharacterController caster, SpellData spell,
        HashSet<Vector2Int> aoeCells, List<CharacterController> targets, out string log)
    {
        log = string.Empty;
        if (spell == null || spell.SpellId != SpellNames.DESECRATE)
            return false;

        if (caster == null || caster.Stats == null || aoeCells == null)
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);

        // Remove any existing Consecrate areas that overlap
        RemoveOverlappingHolyAreas(aoeCells, SpellNames.CONSECRATE);

        // Create the area effect
        var area = new HolyAreaEffect
        {
            SpellId = SpellNames.DESECRATE,
            Caster = caster,
            CasterLevel = casterLevel,
            AffectedCells = new HashSet<Vector2Int>(aoeCells),
            RoundsRemaining = -1 // 2 hr/level — effectively permanent for combat
        };
        _activeHolyAreas.Add(area);

        // Apply effects to undead currently in the area
        int affectedCount = 0;
        StringBuilder sb = new StringBuilder();
        sb.Append($"<color=#CC66FF>💀 {caster.Stats.CharacterName} casts Desecrate! Negative energy fills the area.</color>\n");

        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t?.Stats == null) continue;
                if (IsUndead(t))
                {
                    ApplyDesecrateBuffs(t);
                    affectedCount++;
                    sb.Append($"  💀 {t.Stats.CharacterName} (undead) gains +1 to attacks, damage, and saves!\n");
                }
            }
        }

        sb.Append($"  -3 profane penalty to turning checks in the area.");
        log = sb.ToString();

        Debug.Log($"[Desecrate] Area created by {caster.Stats.CharacterName}, {aoeCells.Count} cells, {affectedCount} undead affected");
        return true;
    }

    // ================================================================
    //  Holy Area Helper Methods
    // ================================================================

    /// <summary>Applies Consecrate debuffs to an undead character: -1 attacks, damage, saves.</summary>
    private void ApplyConsecrateDebuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        target.Stats.ConsecrateActive = true;
        // The penalties are tracked via the flag; attack/damage/save code checks ConsecrateActive
    }

    /// <summary>Removes Consecrate debuffs from a character.</summary>
    private void RemoveConsecrateDebuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        target.Stats.ConsecrateActive = false;
    }

    /// <summary>Applies Desecrate buffs to an undead character: +1 attacks, damage, saves.</summary>
    private void ApplyDesecrateBuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        target.Stats.DesecrateActive = true;
        // The bonuses are tracked via the flag; attack/damage/save code checks DesecrateActive
    }

    /// <summary>Removes Desecrate buffs from a character.</summary>
    private void RemoveDesecrateBuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        target.Stats.DesecrateActive = false;
    }

    /// <summary>Removes overlapping holy areas of a given type from the active list.</summary>
    private void RemoveOverlappingHolyAreas(HashSet<Vector2Int> newCells, string spellIdToRemove)
    {
        for (int i = _activeHolyAreas.Count - 1; i >= 0; i--)
        {
            if (_activeHolyAreas[i].SpellId != spellIdToRemove) continue;

            // Check if any cells overlap
            bool overlaps = _activeHolyAreas[i].AffectedCells.Overlaps(newCells);
            if (overlaps)
            {
                // Remove buffs/debuffs from characters in the removed area
                CleanupHolyAreaCharacters(_activeHolyAreas[i]);
                _activeHolyAreas.RemoveAt(i);
                Debug.Log($"[HolyArea] Removed overlapping {spellIdToRemove} area");
            }
        }
    }

    /// <summary>Removes Consecrate/Desecrate effects from all characters in a holy area.</summary>
    private void CleanupHolyAreaCharacters(HolyAreaEffect area)
    {
        var allChars = GetAllCharacters();
        if (allChars == null) return;

        foreach (var ch in allChars)
        {
            if (ch?.Stats == null) continue;
            if (!area.AffectedCells.Contains(ch.GridPosition)) continue;

            if (area.IsConsecrate)
                RemoveConsecrateDebuffs(ch);
            else
                RemoveDesecrateBuffs(ch);
        }
    }

    /// <summary>
    /// Checks whether a character is undead based on their CreatureType.
    /// </summary>
    private bool IsUndead(CharacterController character)
    {
        if (character?.Stats == null) return false;
        string ct = character.Stats.CreatureType;
        return !string.IsNullOrWhiteSpace(ct) &&
               ct.Trim().Equals("Undead", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Called when a character moves to update Consecrate/Desecrate area effects.
    /// Should be called after any character movement.
    /// </summary>
    public void UpdateHolyAreaEffectsForCharacter(CharacterController character)
    {
        if (character?.Stats == null || _activeHolyAreas.Count == 0) return;

        bool inConsecrate = false;
        bool inDesecrate = false;

        foreach (var area in _activeHolyAreas)
        {
            if (area.AffectedCells.Contains(character.GridPosition))
            {
                if (area.IsConsecrate)
                    inConsecrate = true;
                else
                    inDesecrate = true;
            }
        }

        // Apply/remove Consecrate effects for undead
        if (IsUndead(character))
        {
            if (inConsecrate && !character.Stats.ConsecrateActive)
            {
                ApplyConsecrateDebuffs(character);
                CombatUI?.ShowCombatLog($"<color=#FFFF99>✨ {character.Stats.CharacterName} enters consecrated ground (undead: -1 attacks/damage/saves).</color>");
            }
            else if (!inConsecrate && character.Stats.ConsecrateActive)
            {
                RemoveConsecrateDebuffs(character);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("✨", $"{character.Stats.CharacterName} leaves consecrated ground."));
            }

            if (inDesecrate && !character.Stats.DesecrateActive)
            {
                ApplyDesecrateBuffs(character);
                CombatUI?.ShowCombatLog($"<color=#CC66FF>💀 {character.Stats.CharacterName} enters desecrated ground (undead: +1 attacks/damage/saves).</color>");
            }
            else if (!inDesecrate && character.Stats.DesecrateActive)
            {
                RemoveDesecrateBuffs(character);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("💀", $"{character.Stats.CharacterName} leaves desecrated ground."));
            }
        }
    }

    /// <summary>
    /// Gets the turning check modifier for a character's current position.
    /// Returns +3 in Consecrate areas, -3 in Desecrate areas, 0 otherwise.
    /// </summary>
    public int GetTurningCheckModifierAtPosition(Vector2Int position)
    {
        int modifier = 0;
        foreach (var area in _activeHolyAreas)
        {
            if (area.AffectedCells.Contains(position))
            {
                if (area.IsConsecrate)
                    modifier += 3; // +3 sacred bonus
                else
                    modifier -= 3; // -3 profane penalty
            }
        }
        return modifier;
    }
}

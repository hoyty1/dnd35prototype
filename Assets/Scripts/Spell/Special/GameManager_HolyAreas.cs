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

/// <summary>
/// The modifiers Consecrate and Desecrate give an undead creature in their area, through the bonus ledger
/// (CharacterStats.Bonuses; batch 11 step 23, SPL-042): Consecrate a -1 penalty (untyped) on attack rolls, damage rolls and
/// saving throws (PHB p.212), Desecrate a +1 profane bonus on the same (PHB p.218). Built on attack rolls, weapon damage
/// rolls and saves; the undead creature's other damage rolls (spells, spell-like abilities) are SPL-050. Each is one
/// source, so two overlapping areas of the same spell do not stack (PHB p.171). The flags on CharacterStats show them on
/// the status indicator. An undead creature already in the area at the casting counts as entering it, and any occupied
/// square in the area puts it in (delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1).
/// </summary>
public static class HolyAreaRules
{
    public const string ConsecrateSource = "consecrate";
    public const string DesecrateSource = "desecrate";

    /// <summary>Gives (<paramref name="on"/>) or takes away Consecrate's -1 on attack rolls, damage rolls and saves.</summary>
    public static void SetConsecrated(CharacterStats stats, bool on)
    {
        if (stats == null)
            return;
        stats.ConsecrateActive = on;
        int value = on ? -1 : 0;
        stats.Bonuses.Set(ConsecrateSource, BonusTarget.AttackRoll, BonusType.Untyped, value, "Consecrate");
        stats.Bonuses.Set(ConsecrateSource, BonusTarget.WeaponDamage, BonusType.Untyped, value, "Consecrate");
        stats.Bonuses.Set(ConsecrateSource, BonusTarget.AllSaves, BonusType.Untyped, value, "Consecrate");
    }

    /// <summary>Gives (<paramref name="on"/>) or takes away Desecrate's +1 profane bonus on attack rolls, damage rolls and saves.</summary>
    public static void SetDesecrated(CharacterStats stats, bool on)
    {
        if (stats == null)
            return;
        stats.DesecrateActive = on;
        int value = on ? 1 : 0;
        stats.Bonuses.Set(DesecrateSource, BonusTarget.AttackRoll, BonusType.Profane, value, "Desecrate");
        stats.Bonuses.Set(DesecrateSource, BonusTarget.WeaponDamage, BonusType.Profane, value, "Desecrate");
        stats.Bonuses.Set(DesecrateSource, BonusTarget.AllSaves, BonusType.Profane, value, "Desecrate");
    }
}

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

        // Apply effects to undead currently in the area: they count as entering it (delegated RAW reading 2026-10-14,
        // RULES_COVERAGE 4.1).
        int affectedCount = 0;
        StringBuilder sb = new StringBuilder();
        sb.Append($"<color=#FFFF99>✨ {caster.Stats.CharacterName} casts Consecrate! Positive energy fills the area.</color>\n");

        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t?.Stats == null) continue;
                if (SpellTargetingService.IsUndead(t))
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

        // Apply effects to undead currently in the area: they count as entering it (delegated RAW reading 2026-10-14,
        // RULES_COVERAGE 4.1).
        int affectedCount = 0;
        StringBuilder sb = new StringBuilder();
        sb.Append($"<color=#CC66FF>💀 {caster.Stats.CharacterName} casts Desecrate! Negative energy fills the area.</color>\n");

        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t?.Stats == null) continue;
                if (SpellTargetingService.IsUndead(t))
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
        HolyAreaRules.SetConsecrated(target.Stats, true);
    }

    /// <summary>Removes Consecrate debuffs from a character.</summary>
    private void RemoveConsecrateDebuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        HolyAreaRules.SetConsecrated(target.Stats, false);
    }

    /// <summary>Applies Desecrate buffs to an undead character: +1 attacks, damage, saves.</summary>
    private void ApplyDesecrateBuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        HolyAreaRules.SetDesecrated(target.Stats, true);
    }

    /// <summary>Removes Desecrate buffs from a character.</summary>
    private void RemoveDesecrateBuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        HolyAreaRules.SetDesecrated(target.Stats, false);
    }

    /// <summary>
    /// Ends every Consecrate and Desecrate area and takes their modifiers from every creature (batch 11 step 23, SPL-042):
    /// called with the other area effects when the battlefield is cleared and after a fight, so no area or modifier
    /// outlives its encounter (the grid and the creature slots are reused, CRE-046).
    /// </summary>
    private void ClearAllHolyAreas()
    {
        _activeHolyAreas.Clear();
        var allChars = GetAllCharacters();
        if (allChars == null) return;
        foreach (var ch in allChars)
        {
            if (ch?.Stats == null) continue;
            if (ch.Stats.ConsecrateActive) RemoveConsecrateDebuffs(ch);
            if (ch.Stats.DesecrateActive) RemoveDesecrateBuffs(ch);
        }
    }

    /// <summary>
    /// Removes the areas of <paramref name="spellIdToRemove"/> that the new area overlaps (each spell counters and dispels
    /// the other, PHB p.212, p.218), then re-judges every creature that had that spell's modifier against the areas that
    /// remain (<see cref="UpdateHolyAreaEffectsForCharacter"/>), so a creature still standing in another area of the
    /// removed spell keeps its modifier (review of batch 11 step 23).
    /// </summary>
    private void RemoveOverlappingHolyAreas(HashSet<Vector2Int> newCells, string spellIdToRemove)
    {
        bool removedAny = false;
        for (int i = _activeHolyAreas.Count - 1; i >= 0; i--)
        {
            if (_activeHolyAreas[i].SpellId != spellIdToRemove) continue;
            if (!_activeHolyAreas[i].AffectedCells.Overlaps(newCells)) continue;

            _activeHolyAreas.RemoveAt(i);
            removedAny = true;
            Debug.Log($"[HolyArea] Removed overlapping {spellIdToRemove} area");
        }

        if (!removedAny)
            return;
        var allChars = GetAllCharacters();
        if (allChars == null) return;
        foreach (var ch in allChars)
        {
            if (ch?.Stats == null) continue;
            bool hadModifier = spellIdToRemove == SpellNames.CONSECRATE ? ch.Stats.ConsecrateActive : ch.Stats.DesecrateActive;
            if (hadModifier)
                UpdateHolyAreaEffectsForCharacter(ch);
        }
    }

    /// <summary>
    /// Whether <paramref name="character"/> is in <paramref name="area"/>: any square it occupies is one of the area's,
    /// so a Large creature partly inside is in it, as the cast's own target list counts it (AoESystem.GetTargetsInArea).
    /// </summary>
    private static bool IsInHolyArea(CharacterController character, HolyAreaEffect area)
    {
        if (character == null || area?.AffectedCells == null)
            return false;
        List<Vector2Int> squares = character.GetOccupiedSquares();
        if (squares == null || squares.Count == 0)
            return area.AffectedCells.Contains(character.GridPosition);
        foreach (Vector2Int square in squares)
        {
            if (area.AffectedCells.Contains(square))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Checks whether a character is undead based on their CreatureType.
    /// Delegates to <see cref="SpellTargetingService.IsUndead"/>.
    /// </summary>
    private bool IsUndead(CharacterController character)
        => SpellTargetingService.IsUndead(character);

    /// <summary>
    /// Called when a character moves to update Consecrate/Desecrate area effects: an undead creature entering an area
    /// gains its modifiers and one leaving it loses them (PHB p.212, p.218). NotifyCharacterMovement calls it after every
    /// move (batch 11 step 23, SPL-042).
    /// </summary>
    public void UpdateHolyAreaEffectsForCharacter(CharacterController character)
    {
        if (character?.Stats == null) return;
        // With no area left, only a creature still holding a modifier needs judging (an overlapped area was dispelled).
        if (_activeHolyAreas.Count == 0 && !character.Stats.ConsecrateActive && !character.Stats.DesecrateActive) return;

        bool inConsecrate = false;
        bool inDesecrate = false;

        foreach (var area in _activeHolyAreas)
        {
            // Any occupied square in the area (IsInHolyArea), the same membership the cast's targets use.
            if (IsInHolyArea(character, area))
            {
                if (area.IsConsecrate)
                    inConsecrate = true;
                else
                    inDesecrate = true;
            }
        }

        // Apply/remove Consecrate effects for undead
        if (SpellTargetingService.IsUndead(character))
        {
            if (inConsecrate && !character.Stats.ConsecrateActive)
            {
                ApplyConsecrateDebuffs(character);
                CombatUI?.ShowCombatLog(CombatLogHelper.Color($"✨ {character.Stats.CharacterName} enters consecrated ground (undead: -1 attacks/damage/saves).", "FFFF99"));
            }
            else if (!inConsecrate && character.Stats.ConsecrateActive)
            {
                RemoveConsecrateDebuffs(character);
                CombatUI?.ShowCombatLog(CombatLogHelper.Info("✨", $"{character.Stats.CharacterName} leaves consecrated ground."));
            }

            if (inDesecrate && !character.Stats.DesecrateActive)
            {
                ApplyDesecrateBuffs(character);
                CombatUI?.ShowCombatLog(CombatLogHelper.Color($"💀 {character.Stats.CharacterName} enters desecrated ground (undead: +1 attacks/damage/saves).", "CC66FF"));
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

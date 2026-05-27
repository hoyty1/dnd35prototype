using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Absorbing Shield (SRD / DMG p.219)
//
// +1 heavy steel shield with a dull black, non-reflective surface.
//
// Standard version (50,170 gp):
//   - Can disintegrate one touched object every 2 days
//   - Absorbs up to 50 spell levels of targeted spells
//
// Greater version (82,670 gp):
//   - Can disintegrate one touched object every 1 day
//   - Absorbs up to 100 spell levels of targeted spells
//
// Spell absorption:
//   - Only absorbs spells that specifically target the wielder
//   - Area spells and effects are NOT absorbed
//   - When full, no more spells can be absorbed
//   - Stored levels reset on a new day (long rest)
// ============================================================================

/// <summary>
/// Absorbing Shield specific item behavior.
/// Disintegrates objects on touch and absorbs targeted spells.
/// </summary>
public class AbsorbingShieldBehavior : SpecificItemBehavior
{
    private const int StandardMaxSpellLevels = 50;
    private const int GreaterMaxSpellLevels = 100;
    private const int StandardRechargeDays = 2;
    private const int GreaterRechargeDays = 1;

    private readonly bool _isGreater;
    private readonly int _maxSpellLevels;
    private readonly int _rechargeDays;

    // Disintegrate object ability
    private bool _disintegrateAvailable;
    private int _daysUntilRecharge;

    // Spell absorption
    private int _absorbedSpellLevels;

    public override string DisplayName => _isGreater ? "Absorbing Shield (Greater)" : "Absorbing Shield";

    public AbsorbingShieldBehavior(bool isGreater = false)
    {
        _isGreater = isGreater;
        _maxSpellLevels = isGreater ? GreaterMaxSpellLevels : StandardMaxSpellLevels;
        _rechargeDays = isGreater ? GreaterRechargeDays : StandardRechargeDays;
    }

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _disintegrateAvailable = true;
        _daysUntilRecharge = 0;
        _absorbedSpellLevels = 0;
    }

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);

        string name = character.Stats?.CharacterName ?? "Wielder";
        Log($"Equipped by {name} — can absorb up to {_maxSpellLevels} spell levels");

        GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.RoyalBlue("🛡", $"️ {DisplayName} active — absorbs spells ({_absorbedSpellLevels}/{_maxSpellLevels} levels stored)"));
    }

    // ========================================================================
    //  SPELL ABSORPTION: Intercept targeted spells
    // ========================================================================

    /// <summary>
    /// Attempts to absorb a spell targeting the wielder.
    /// Only absorbs single-target spells. Returns true if spell was absorbed.
    /// </summary>
    public override bool OnSpellTargeted(string spellName, int spellLevel, CharacterController caster, List<string> logNotes)
    {
        // Can't absorb if full
        if (_absorbedSpellLevels >= _maxSpellLevels)
        {
            logNotes?.Add($"🛡️ {DisplayName}: absorption full ({_absorbedSpellLevels}/{_maxSpellLevels})!");
            Log($"Cannot absorb {spellName} (level {spellLevel}) — full");
            return false;
        }

        // Can't absorb if spell level would exceed capacity
        if (_absorbedSpellLevels + spellLevel > _maxSpellLevels)
        {
            logNotes?.Add($"🛡️ {DisplayName}: not enough capacity to absorb {spellName} (level {spellLevel}, " +
                         $"{_maxSpellLevels - _absorbedSpellLevels} levels remaining)!");
            Log($"Cannot absorb {spellName} — insufficient capacity ({_maxSpellLevels - _absorbedSpellLevels} remaining, need {spellLevel})");
            return false;
        }

        // Can't absorb cantrips (level 0)
        if (spellLevel <= 0)
        {
            return false;
        }

        // Absorb the spell!
        _absorbedSpellLevels += spellLevel;

        string wielderName = Wielder?.Stats?.CharacterName ?? "wielder";
        string casterName = caster?.Stats?.CharacterName ?? "unknown caster";

        logNotes?.Add($"🛡️ <color=#4169E1>{DisplayName} ABSORBS {spellName}!</color> " +
                     $"({_absorbedSpellLevels}/{_maxSpellLevels} spell levels stored)");
        Log($"Absorbed {spellName} (level {spellLevel}) from {casterName} — {_absorbedSpellLevels}/{_maxSpellLevels} total");

        GameManager.Instance?.CombatUI?.ShowCombatLog(CombatLogHelper.RoyalBlue("🛡️", $"{DisplayName} absorbs {casterName}'s {spellName}! ({_absorbedSpellLevels}/{_maxSpellLevels} levels)"));

        return true; // Spell negated
    }

    /// <summary>
    /// Current absorbed spell levels.
    /// </summary>
    public int AbsorbedSpellLevels => _absorbedSpellLevels;

    /// <summary>
    /// Maximum spell levels this shield can absorb.
    /// </summary>
    public int MaxSpellLevels => _maxSpellLevels;

    /// <summary>
    /// Whether the shield can still absorb spells.
    /// </summary>
    public bool CanAbsorb => _absorbedSpellLevels < _maxSpellLevels;

    // ========================================================================
    //  ACTIVATED: Disintegrate object (touch)
    // ========================================================================

    public override bool CanActivate()
    {
        return IsEquipped && _disintegrateAvailable;
    }

    public override string GetActivateDescription()
    {
        if (!_disintegrateAvailable)
            return $"Disintegrate recharging ({_daysUntilRecharge} day(s) remaining).";
        return $"Touch an object to disintegrate it utterly. Recharges in {_rechargeDays} day(s). " +
               $"Spell absorption: {_absorbedSpellLevels}/{_maxSpellLevels} levels stored.";
    }

    /// <summary>
    /// Activate to disintegrate an object. In combat, this could be used to destroy
    /// an enemy's weapon or equipment.
    /// </summary>
    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (!_disintegrateAvailable)
        {
            logNotes?.Add($"{DisplayName}: ability recharging ({_daysUntilRecharge} day(s) remaining).");
            return false;
        }

        _disintegrateAvailable = false;
        _daysUntilRecharge = _rechargeDays;

        logNotes?.Add($"🛡️ <color=#4169E1>{DisplayName}</color>: disintegrates the touched object!");
        Log($"Disintegrate object used — recharges in {_rechargeDays} days");

        // In a full implementation, this would target a specific item on the target
        // or a held/worn object and destroy it.
        return true;
    }

    // ========================================================================
    //  REST / DAILY RESET
    // ========================================================================

    public override void OnLongRest()
    {
        // Recharge disintegrate
        if (!_disintegrateAvailable)
        {
            _daysUntilRecharge--;
            if (_daysUntilRecharge <= 0)
            {
                _disintegrateAvailable = true;
                _daysUntilRecharge = 0;
                Log("Disintegrate recharged");
            }
        }

        // Reset absorbed spell levels on new day
        if (_absorbedSpellLevels > 0)
        {
            Log($"Spell absorption reset ({_absorbedSpellLevels} levels cleared)");
            _absorbedSpellLevels = 0;
        }
    }

    public override string GetUsesDisplay()
    {
        var parts = new List<string>();

        // Disintegrate status
        parts.Add(_disintegrateAvailable ? "Disintegrate ready" : $"Recharging ({_daysUntilRecharge}d)");

        // Absorption status
        int remaining = _maxSpellLevels - _absorbedSpellLevels;
        parts.Add($"Absorb: {remaining}/{_maxSpellLevels}");

        return string.Join(" | ", parts);
    }

    // ========================================================================
    //  TOOLTIP
    // ========================================================================

    public string GetTooltipText()
    {
        var lines = new List<string>();
        string enhText = _isGreater ? "+1 (greater)" : "+1";
        lines.Add($"<b>{DisplayName}</b> ({enhText} heavy steel shield)");
        lines.Add($"Spell absorption: {_absorbedSpellLevels}/{_maxSpellLevels} levels stored");
        lines.Add($"Disintegrate object: {(_disintegrateAvailable ? "ready" : $"recharging ({_daysUntilRecharge}d)")}");

        int remaining = _maxSpellLevels - _absorbedSpellLevels;
        if (remaining > 0)
            lines.Add($"<color=#4169E1>Can absorb {remaining} more spell levels</color>");
        else
            lines.Add("<color=#FF0000>Absorption full!</color>");

        return string.Join("\n", lines);
    }
}

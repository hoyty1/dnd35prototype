using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caster's Shield (SRD): +1 light wooden shield with a leather strip that can
/// hold a single scroll of up to 3rd level. The wielder can prepare a spell on
/// the strip (Scribe Scroll feat, takes a day), and then cast it directly from
/// the shield as if reading a scroll — without needing a free hand.
/// Does NOT grant a caster level bonus — it's purely a scroll storage/casting system.
/// </summary>
public class CastersShieldBehavior : SpecificItemBehavior
{
    /// <summary>Name of the spell currently scribed on the shield strip.</summary>
    public string ScribedSpellName;

    /// <summary>Level of the scribed spell (max 3).</summary>
    public int ScribedSpellLevel;

    /// <summary>Whether a spell is currently scribed and available for use.</summary>
    public bool HasScribedSpell => !string.IsNullOrEmpty(ScribedSpellName);

    private const int MaxSpellLevel = 3;

    public override bool CanActivate()
    {
        return IsEquipped && HasScribedSpell;
    }

    public override string GetActivateDescription()
    {
        if (!HasScribedSpell)
            return "No spell scribed on the shield strip. Use Scribe Scroll to prepare a spell (max 3rd level).";
        return $"Cast {ScribedSpellName} (level {ScribedSpellLevel}) from the shield strip. Consumed on use.";
    }

    /// <summary>
    /// Cast the scribed spell from the shield strip. The spell is consumed.
    /// </summary>
    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (!HasScribedSpell)
        {
            logNotes.Add("Caster's Shield: no spell scribed on the strip.");
            return false;
        }

        string spellName = ScribedSpellName;
        int level = ScribedSpellLevel;

        // Consume the scribed spell
        ScribedSpellName = null;
        ScribedSpellLevel = 0;

        logNotes.Add($"Caster's Shield: casts {spellName} (level {level}) from the shield strip! Strip is now empty.");
        Log($"Cast {spellName} from shield strip");

        // Actual spell effect would be resolved by the spell system.
        // The shield just provides the scroll-like casting mechanism.
        return true;
    }

    /// <summary>
    /// Scribe a spell onto the shield's leather strip. Requires Scribe Scroll feat.
    /// </summary>
    /// <param name="spellName">Name of the spell to scribe.</param>
    /// <param name="spellLevel">Level of the spell (must be 0-3).</param>
    public bool ScribeSpell(string spellName, int spellLevel)
    {
        if (spellLevel > MaxSpellLevel)
        {
            Log($"Cannot scribe {spellName} (level {spellLevel}) — maximum is {MaxSpellLevel}.");
            return false;
        }

        if (HasScribedSpell)
        {
            Log($"Shield strip already has {ScribedSpellName} scribed. Must use or erase it first.");
            return false;
        }

        ScribedSpellName = spellName;
        ScribedSpellLevel = spellLevel;
        Log($"Scribed {spellName} (level {spellLevel}) onto shield strip.");
        return true;
    }

    public override string GetUsesDisplay()
    {
        return HasScribedSpell
            ? $"Scribed: {ScribedSpellName} (level {ScribedSpellLevel})"
            : "Shield strip empty";
    }
}

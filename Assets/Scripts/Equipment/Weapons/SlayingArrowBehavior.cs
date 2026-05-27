using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slaying Arrow / Greater Slaying Arrow (SRD):
/// Slaying Arrow: On hit vs designated creature type, target must make DC 20 Fort save or die instantly.
/// Greater Slaying Arrow: Same but DC 23.
/// Both are consumed on use (whether or not the target saves).
/// </summary>
public class SlayingArrowBehavior : SpecificItemBehavior
{
    private readonly string _designatedType;
    private readonly int _saveDC;
    private readonly bool _isGreater;

    /// <param name="designatedType">The creature type this arrow slays (e.g., "Undead", "Dragon").</param>
    /// <param name="isGreater">True for Greater Slaying Arrow (DC 23), false for standard (DC 20).</param>
    public SlayingArrowBehavior(string designatedType, bool isGreater = false)
    {
        _designatedType = designatedType ?? "Humanoid";
        _isGreater = isGreater;
        _saveDC = isGreater ? 23 : 20;
    }

    public override string DisplayName => _isGreater
        ? $"Greater Slaying Arrow ({_designatedType})"
        : $"Slaying Arrow ({_designatedType})";

    public override void OnHitApplied(CharacterController target, int finalDamage, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;

        // Check if target matches the designated creature type
        if (!IsCreatureType(target, _designatedType))
        {
            logNotes.Add($"{DisplayName}: {target.Stats.CharacterName} is not a {_designatedType} — no slaying effect.");
            return;
        }

        var save = SavingThrowResolver.ResolveFortitudeSave(target.Stats, _saveDC, DisplayName);

        if (!save.Succeeded)
        {
            // Instant death
            logNotes.Add($"{DisplayName}: {target.Stats.CharacterName} SLAIN! (Fort DC {_saveDC} failed: roll {save.Roll}, total {save.Total})");
            Log($"{target.Stats.CharacterName} slain by {DisplayName} (Fort {save.Total} < DC {_saveDC})");

            // Deal massive damage to kill the target
            target.Stats.CurrentHP = -100;
            target.OnDeath();
        }
        else
        {
            logNotes.Add($"{DisplayName}: {target.Stats.CharacterName} survives! (Fort DC {_saveDC}: roll {save.Roll}, total {save.Total})");
            Log($"{target.Stats.CharacterName} survives {DisplayName} (Fort {save.Total} >= DC {_saveDC})");
        }
    }
}

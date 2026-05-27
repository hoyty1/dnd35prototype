using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sleep Arrow (SRD): On hit, deals no HP damage — instead the target must make
/// a DC 11 Will save or fall asleep. The arrow is consumed on use.
/// </summary>
public class SleepArrowBehavior : SpecificItemBehavior
{
    private const int SleepDC = 11;

    public override void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes)
    {
        // Sleep Arrow converts all damage to sleep effect — no HP damage
        damage = 0;
        logNotes.Add("Sleep Arrow: damage converted to sleep effect");
    }

    public override void OnHitApplied(CharacterController target, int finalDamage, List<string> logNotes)
    {
        if (target == null || target.Stats == null) return;

        var save = SavingThrowResolver.ResolveWillSave(target.Stats, SleepDC, "Sleep Arrow");

        if (!save.Succeeded)
        {
            // Sleep for a long duration (effectively until woken — standard action to wake)
            target.ApplyCondition(CombatConditionType.Asleep, 100, "Sleep Arrow");
            logNotes.Add($"Sleep Arrow: {target.Stats.CharacterName} falls asleep! (Will DC {SleepDC} failed: {save.Roll}, total {save.Total})");
            Log($"{target.Stats.CharacterName} falls asleep (Will {save.Total} < DC {SleepDC})");
        }
        else
        {
            logNotes.Add($"Sleep Arrow: {target.Stats.CharacterName} resists sleep (Will DC {SleepDC}: roll {save.Roll}, total {save.Total})");
            Log($"{target.Stats.CharacterName} resists sleep (Will {save.Total} >= DC {SleepDC})");
        }
    }
}

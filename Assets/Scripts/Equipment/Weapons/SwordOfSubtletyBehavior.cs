using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sword of Subtlety (SRD): +1 short sword that provides a +4 bonus to
/// attack and damage rolls when making a sneak attack. The bonus is in addition
/// to the weapon's +1 enhancement bonus.
/// </summary>
public class SwordOfSubtletyBehavior : SpecificItemBehavior
{
    private const int SneakAttackBonus = 4;

    public override void OnPreAttackRoll(CharacterController target, ref int attackBonus, List<string> logNotes)
    {
        // The +4 attack bonus on sneak attacks is applied if the wielder would qualify
        // for a sneak attack this round. We check if the target is flat-footed or flanked.
        if (Wielder == null || target == null) return;

        // Check if a sneak attack would apply (target denied Dex or flanked)
        if (IsSneakAttackSituation(target))
        {
            attackBonus += SneakAttackBonus;
            logNotes.Add($"Sword of Subtlety: +{SneakAttackBonus} attack (sneak attack)");
        }
    }

    public override void OnDamageRoll(CharacterController target, ref int damage, bool isCrit, List<string> logNotes)
    {
        if (Wielder == null || target == null) return;

        if (IsSneakAttackSituation(target))
        {
            damage += SneakAttackBonus;
            logNotes.Add($"Sword of Subtlety: +{SneakAttackBonus} damage (sneak attack)");
        }
    }

    private bool IsSneakAttackSituation(CharacterController target)
    {
        if (target == null || Wielder == null) return false;

        // Check if target is denied Dex bonus (flat-footed, stunned, etc.)
        bool targetDeniedDex = target.HasCondition(CombatConditionType.FlatFooted)
            || target.HasCondition(CombatConditionType.Stunned)
            || target.HasCondition(CombatConditionType.Paralyzed)
            || target.HasCondition(CombatConditionType.Blinded)
            || target.HasCondition(CombatConditionType.Feinted);

        return targetDeniedDex;
    }
}

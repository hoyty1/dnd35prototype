using System.Collections.Generic;

/// <summary>
/// Cleave and Great Cleave (PHB p.92; decision D7 of docs/designs/pc_npc_parity_batch12.md, a delegated RAW reading
/// recorded in docs/systems/RULES_COVERAGE.md 4.1), the same for every attacker and every melee attack origin (the
/// post-attack hook, GameManager.OnAttackResolved, stage 8). Pure: no dice, no log.
/// - <see cref="DroppedTarget"/>: the attack's damage dropped the foe: killed it, took it below 0 hit points, or knocked it
///   unconscious (nonlethal damage too). A foe left disabled at exactly 0 hit points has not dropped.
/// - <see cref="MayCleave"/>: the attacker has Cleave (once per round, CharacterController.CleaveUsedThisRound) or Great
///   Cleave (no limit).
/// - <see cref="Candidates"/>: the other creatures within the attacker's reach with that weapon (reach weapons and
///   creatures of every size, AttackLegality's reach), that it perceives (sees, or has pinpointed: PHB p.152, DMG p.295)
///   and may attack (AttackLegality.CanDirectlyAttack: the Resilient Sphere, the Improved Grab hold bar, a failed ward
///   save). RAW says "another creature", so allies are candidates too; preferring foes is the decider's choice
///   (IAttackDecider.ChooseCleaveTarget).
/// </summary>
public static class CleaveRules
{
    /// <summary>
    /// True when <paramref name="r"/>, a melee attack that was made and hit, dropped <paramref name="target"/>: it killed it,
    /// took it from 0 or more hit points to below 0, or knocked it unconscious (it was not down before the roll and was
    /// down right after it: <see cref="CombatResult.DefenderDownBefore"/>, <see cref="CombatResult.DefenderDownAfter"/>,
    /// recorded per result, so a later result of the same step never makes an earlier one count). A ranged attack never
    /// cleaves; a hit on a Mirror Image figure dropped nothing. <paramref name="target"/> is not read (kept for callers).
    /// </summary>
    public static bool DroppedTarget(CombatResult r, CharacterController target)
    {
        if (r == null || r.NotMade || !r.Hit || r.IsRangedAttack || r.MirrorImageFigure)
            return false;
        if (r.TargetKilled)
            return true;
        if (r.DefenderHPBefore >= 0 && r.DefenderHPAfter < 0)
            return true;
        return !r.DefenderDownBefore && r.DefenderDownAfter;
    }

    /// <summary>The first result of <paramref name="results"/> that dropped <paramref name="target"/> (<see cref="DroppedTarget"/>), or null.</summary>
    public static CombatResult FirstDropping(IList<CombatResult> results, CharacterController target)
    {
        if (results == null)
            return null;
        for (int i = 0; i < results.Count; i++)
            if (DroppedTarget(results[i], target))
                return results[i];
        return null;
    }

    /// <summary>
    /// True when <paramref name="attacker"/> may make a Cleave attack now: it has Great Cleave (no limit per round), or
    /// Cleave and has not used it this round. <paramref name="great"/> says which feat applies.
    /// </summary>
    public static bool MayCleave(CharacterController attacker, out bool great)
    {
        great = false;
        if (attacker == null || attacker.Stats == null)
            return false;
        great = FeatManager.HasGreatCleave(attacker.Stats);
        if (great)
            return true;
        return FeatManager.HasCleave(attacker.Stats) && !attacker.CleaveUsedThisRound;
    }

    /// <summary>
    /// The creatures <paramref name="attacker"/> may cleave into after dropping <paramref name="dropped"/>: every other
    /// living creature (PHB p.92 "another creature", foe or not) that it perceives (CanVisuallyPerceive, or a square it has
    /// pinpointed: HasPinpointed; an unseen creature whose square it does not know is not offered, PHB p.152), within its
    /// reach for the attack's weapon (<paramref name="intent"/>: its melee mode and weapon; a natural attack's natural
    /// reach) and passing AttackLegality.CanDirectlyAttack (read only: an unrolled ward save is allowed and rolled when the
    /// attack is made).
    /// </summary>
    public static List<CharacterController> Candidates(CharacterController attacker, CharacterController dropped,
        IEnumerable<CharacterController> everyone, AttackIntent intent, bool natural)
    {
        var list = new List<CharacterController>();
        if (attacker == null || everyone == null)
            return list;
        DirectAttackKind kind = natural ? DirectAttackKind.Natural : DirectAttackKind.Weapon;
        foreach (CharacterController c in everyone)
        {
            if (c == null || c == attacker || c == dropped || c.Stats == null || c.IsDead || c.Stats.IsDead)
                continue;
            if (!attacker.CanVisuallyPerceive(c) && !attacker.HasPinpointed(c))
                continue;
            if (list.Contains(c))
                continue;
            if (!AttackLegality.CanDirectlyAttack(attacker, c, kind, intent).Ok)
                continue;
            list.Add(c);
        }
        return list;
    }
}

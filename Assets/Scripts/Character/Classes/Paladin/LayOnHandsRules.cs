using UnityEngine;

/// <summary>What one use of Lay on Hands did (<see cref="LayOnHandsRules.Resolve"/>).</summary>
public struct LayOnHandsResult
{
    /// <summary>The use happened: the standard action is spent (a missed touch on an undead creature counts).</summary>
    public bool Performed;
    /// <summary>Why it did not happen (when <see cref="Performed"/> is false).</summary>
    public string Refusal;
    /// <summary>The target is undead: a melee touch attack and damage instead of healing.</summary>
    public bool HarmsUndead;
    public bool TouchHit;
    public int TouchRoll;
    public int TouchTotal;
    public int TouchArmorClass;
    /// <summary>Points taken from the day's pool.</summary>
    public int PointsSpent;
    /// <summary>Lethal hit points restored (healing).</summary>
    public int HitPointsHealed;
    /// <summary>Nonlethal damage removed (healing).</summary>
    public int NonlethalHealed;
    /// <summary>Damage the undead target took (after its immunities; damage reduction does not apply).</summary>
    public int DamageDealt;
    public int PoolRemaining;
    /// <summary>One combat log line.</summary>
    public string Log;
}

/// <summary>
/// The one Lay on Hands rule for PCs and NPCs (PHB p.44; CHR-020). The PC Lay on Hands button
/// (GameManager.OnLayOnHandsButtonPressed) and the AI (GameManager.TryLayOnHandsForAI) both call <see cref="Resolve"/>.
/// From 2nd paladin level, with a Charisma score of 12 or higher: a pool of paladin level x Charisma bonus hit points a
/// day, spent in any amounts on any number of touches. Using it is a standard action, a supernatural ability, so it does
/// not provoke attacks of opportunity (PHB p.142). On a living creature (the paladin or another creature she touches)
/// it heals; on an undead creature it needs a successful melee touch attack, and the paladin then chooses how many
/// points to spend as damage (no saving throw). The touch reaches the creatures within the paladin's natural reach.
/// Readings: a construct is not healed (as a cure is not, SpellDiceRules.OutcomeFor; MM p.307: a construct does not
/// heal); a grappling paladin cannot use it, since it is not among the actions a grappler may take (PHB p.156); and a
/// holder may not touch the creature it holds with one body part to harm it (an attack, MM p.310; CMB-163). The amount
/// spent is the points the target needs (its damage, or the undead's remaining hit points), capped by the pool, unless
/// the caller asks for less (delegated RAW reading 2026-10-14: spending beyond what heals or destroys does nothing).
/// </summary>
public static class LayOnHandsRules
{
    /// <summary>The dice context of the touch attack against an undead creature (scenarios force it by this name).</summary>
    public const string TouchAttackRollContext = "Lay on Hands touch attack";

    /// <summary>Points left in the day's pool (0 for a creature without the ability).</summary>
    public static int RemainingPool(CharacterStats stats)
    {
        LayOnHandsData data = stats != null ? stats.GetPaladinLayOnHands() : null;
        return data != null ? data.RemainingPool : 0;
    }

    /// <summary>The day's pool (paladin level x Charisma bonus; 0 below 2nd level or with Charisma below 12).</summary>
    public static int MaxPool(CharacterStats stats)
    {
        LayOnHandsData data = stats != null ? stats.GetPaladinLayOnHands() : null;
        return data != null ? data.MaxPool : 0;
    }

    /// <summary>True when the creature has the ability at all (paladin 2nd level or higher), used or not.</summary>
    public static bool HasAbility(CharacterStats stats) => stats != null && stats.PaladinLevel >= 2;

    /// <summary>Whether <paramref name="actor"/> may lay on hands now (ability, pool, standard action, not grappling).</summary>
    public static bool CanUse(CharacterController actor, out string reason)
    {
        reason = string.Empty;
        if (actor == null || actor.Stats == null)
        {
            reason = "No active character";
            return false;
        }
        if (!HasAbility(actor.Stats))
        {
            reason = "Paladin level 2 required";
            return false;
        }
        if (MaxPool(actor.Stats) <= 0)
        {
            reason = "Charisma 12 or higher required";
            return false;
        }
        if (RemainingPool(actor.Stats) <= 0)
        {
            reason = "Pool spent today";
            return false;
        }
        if (actor.Actions == null || !actor.Actions.HasStandardAction)
        {
            reason = "Standard action used";
            return false;
        }
        if (actor.IsGrappling())
        {
            reason = "Grappling (not a grapple action, PHB p.156)";
            return false;
        }
        return true;
    }

    /// <summary>True for an undead creature (lay on hands damages it).</summary>
    public static bool IsUndead(CharacterStats stats) => SpellDiceRules.IsUndead(stats);

    /// <summary>The hit points of damage healing could remove: lethal damage plus nonlethal damage (CharacterStats.HealDamage, CHR-011).</summary>
    public static int HealingNeeded(CharacterStats stats)
    {
        if (stats == null)
            return 0;
        return Mathf.Max(0, stats.TotalMaxHP - stats.CurrentHP) + Mathf.Max(0, stats.NonlethalDamage);
    }

    /// <summary>
    /// Whether <paramref name="target"/> can receive the paladin's touch now: itself or a creature within its natural
    /// reach, not dead; a living, non-construct creature that has damage to heal, or an undead creature
    /// (<paramref name="harmsUndead"/>).
    /// </summary>
    public static bool IsValidTarget(CharacterController actor, CharacterController target, out bool harmsUndead, out string reason)
    {
        harmsUndead = false;
        reason = string.Empty;
        if (actor == null || actor.Stats == null || target == null || target.Stats == null)
        {
            reason = "No target";
            return false;
        }
        if (target.Stats.IsDead)
        {
            reason = "Target is dead";
            return false;
        }
        if (target != actor && !actor.IsTargetInUnarmedReach(target))
        {
            reason = "Not within touch";
            return false;
        }

        if (IsUndead(target.Stats))
        {
            harmsUndead = true;
            if (target == actor)
            {
                reason = "Cannot touch itself to harm";
                return false;
            }
            if (actor.IsBarredFromOrdinaryAttackOn(target, out string holdReason))
            {
                reason = holdReason;
                return false;
            }
            return true;
        }

        if (SpellDiceRules.IsConstruct(target.Stats))
        {
            reason = "A construct is not healed";
            return false;
        }
        if (HealingNeeded(target.Stats) <= 0)
        {
            reason = "Not hurt";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Uses Lay on Hands on <paramref name="target"/>: spends the standard action, then heals, or rolls the melee touch
    /// attack and on a hit deals the spent points as damage. <paramref name="requestedPoints"/> caps the points spent
    /// (null: what the target needs). Deals damage through GameManager.DealDamage when a GameManager exists; the caller
    /// checks the combat end and refreshes the UI.
    /// </summary>
    public static LayOnHandsResult Resolve(CharacterController actor, CharacterController target, int? requestedPoints = null)
    {
        var result = new LayOnHandsResult();
        if (!CanUse(actor, out string reason) || !IsValidTarget(actor, target, out bool harmsUndead, out reason))
        {
            result.Refusal = reason;
            return result;
        }
        if (!actor.CommitStandardAction())
        {
            result.Refusal = "Standard action used";
            return result;
        }

        LayOnHandsData pool = actor.Stats.GetPaladinLayOnHands();
        string actorName = actor.Stats.CharacterName;
        string targetName = target == actor ? target.Stats.CharacterName + " (self)" : target.Stats.CharacterName;
        result.Performed = true;
        result.HarmsUndead = harmsUndead;

        if (!harmsUndead)
        {
            int need = HealingNeeded(target.Stats);
            int points = Mathf.Min(need, pool.RemainingPool);
            if (requestedPoints.HasValue)
                points = Mathf.Min(points, Mathf.Max(1, requestedPoints.Value));
            result.PointsSpent = pool.HealLiving(points);
            result.HitPointsHealed = target.Stats.HealDamage(result.PointsSpent, out int nonlethalHealed);
            result.NonlethalHealed = nonlethalHealed;
            result.PoolRemaining = pool.RemainingPool;
            result.Log = $"{actorName} lays on hands on {targetName}: {result.PointsSpent} points heal {result.HitPointsHealed} hit points"
                + (nonlethalHealed > 0 ? $" and {nonlethalHealed} nonlethal damage" : string.Empty)
                + $" ({result.PoolRemaining}/{pool.MaxPool} left today).";
            return result;
        }

        // Undead: a melee touch attack that provokes nothing (PHB p.44), with the racial attack bonus against the
        // target's kind and its racial dodge against giants (CHR-019), as the other touch attacks.
        int roll = DiceService.D20(TouchAttackRollContext);
        int modifier = actor.GetManeuverMeleeTouchAttackModifier() + RacialTraitRules.AttackBonusAgainst(actor.Stats, target.Stats);
        int total = roll + modifier;
        int touchAC = target.Stats.TouchArmorClass + RacialTraitRules.KindDodgeACBonusAgainst(target.Stats, actor.Stats);
        bool hit = CharacterController.IsManeuverTouchAttackHit(roll, total, touchAC);
        result.TouchRoll = roll;
        result.TouchTotal = total;
        result.TouchArmorClass = touchAC;
        result.TouchHit = hit;
        string touchLine = $"melee touch d20 {roll} {CharacterStats.FormatMod(modifier)} = {total} vs touch AC {touchAC}";
        if (!hit)
        {
            result.PoolRemaining = pool.RemainingPool;
            result.Log = $"{actorName} reaches to lay on hands on {targetName} and misses ({touchLine}); no points spent.";
            return result;
        }

        int harmPoints = Mathf.Min(Mathf.Max(0, target.Stats.CurrentHP), pool.RemainingPool);
        if (requestedPoints.HasValue)
            harmPoints = Mathf.Min(harmPoints, Mathf.Max(1, requestedPoints.Value));
        harmPoints = Mathf.Max(harmPoints, Mathf.Min(1, pool.RemainingPool));
        result.PointsSpent = pool.HarmUndead(harmPoints);
        DamagePacket packet = DamagePackets.Supernatural("Lay on Hands", DamageType.Untyped);
        DamageResolutionResult dealt = GameManager.Instance != null
            ? GameManager.Instance.DealDamage(target, result.PointsSpent, packet)
            : GameManager.ApplyDamagePacket(target, result.PointsSpent, packet);
        result.DamageDealt = dealt != null ? dealt.FinalDamage : 0;
        result.PoolRemaining = pool.RemainingPool;
        result.Log = $"{actorName} lays on hands on the undead {targetName} ({touchLine}, hit): {result.PointsSpent} points deal {result.DamageDealt} damage"
            + (target.Stats.IsDead ? $"; {target.Stats.CharacterName} is destroyed" : string.Empty)
            + $" ({result.PoolRemaining}/{pool.MaxPool} left today).";
        return result;
    }
}

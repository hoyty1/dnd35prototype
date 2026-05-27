using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// SpellTargetingService — Centralized spell targeting validation.
//
// Phase 4I extraction.  Consolidates the creature-type checks, alignment
// queries, null/dead guards, and HD-gated targeting predicates that were
// previously scattered across GameManager.SpellCasting.cs,
// GameManager_Spells_*.cs, and GameManager_DomainSpells.cs.
//
// All methods are **pure static** — no GameManager state required.
// They depend only on their arguments and on existing utility classes
// (TeamUtility, SpellUtilities, AlignmentHelper).
//
// Call sites still make their own decisions (save DCs, logging, effects);
// this service only answers the question "Is this a valid target?"
// ============================================================================

/// <summary>
/// Static utility providing creature-type, alignment, HD, and range
/// predicates used by spell targeting throughout the system.
/// <para>
/// <b>PHB 3.5e targeting overview (p.175-176):</b>
/// A spell can target creatures, objects, or areas. The spell description
/// specifies valid targets. Common restrictions include creature type
/// (e.g. "one humanoid creature"), Hit Dice limits, alignment axes,
/// and range/area constraints.
/// </para>
/// </summary>
public static class SpellTargetingService
{
    // ════════════════════════════════════════════════════════════
    //  Null / Dead Guards
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns <c>true</c> if the target exists, has valid stats, and is alive.
    /// This is the most common pre-check before any spell targeting logic.
    /// <para><b>PHB 3.5e p.175:</b> "You must be able to see or touch a target…
    /// A creature that has already been destroyed cannot be targeted."</para>
    /// </summary>
    public static bool IsValidAliveTarget(CharacterController target)
    {
        return target != null
            && target.Stats != null
            && !target.Stats.IsDead;
    }

    /// <summary>
    /// Full targeting pre-check: caster, target, and spell are all non-null,
    /// target has stats, and target is alive.
    /// </summary>
    public static bool PassesBasicTargetingChecks(
        CharacterController caster,
        CharacterController target,
        SpellData spell)
    {
        return caster != null
            && target != null
            && spell != null
            && target.Stats != null
            && !target.Stats.IsDead;
    }

    // ════════════════════════════════════════════════════════════
    //  Creature Type Predicates
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns <c>true</c> if the creature's type is "Humanoid".
    /// <para><b>PHB 3.5e p.310 (Humanoid type):</b> A humanoid usually has two arms,
    /// two legs, and one head. Humanoids with 1 HD exchange the features of
    /// their humanoid type for class levels.</para>
    /// <para>Used by: Enlarge/Reduce Person, Hold Person, Charm Person,
    /// Dominate Person, Daze, Ghoul Touch, Mass Enlarge/Reduce Person.</para>
    /// </summary>
    /// <remarks>Delegates to <see cref="TeamUtility.IsHumanoid"/>.</remarks>
    public static bool IsHumanoid(CharacterController target)
        => TeamUtility.IsHumanoid(target);

    /// <summary>
    /// Returns <c>true</c> if the creature's type is "Undead".
    /// <para><b>PHB 3.5e p.317 (Undead type):</b> Undead are once-living creatures
    /// animated by spiritual or supernatural forces. They are immune to
    /// mind-affecting effects, poison, sleep, paralysis, stunning, disease,
    /// death effects, critical hits, nonlethal damage, ability drain, energy drain,
    /// and fatigue/exhaustion.</para>
    /// <para>Used by: Searing Light, Command Undead, Halt Undead, Consecrate/Desecrate,
    /// Disrupting Weapon, healing spells (reversed effect).</para>
    /// </summary>
    public static bool IsUndead(CharacterController target)
    {
        if (target?.Stats == null) return false;
        string ct = target.Stats.CreatureType;
        return !string.IsNullOrWhiteSpace(ct)
            && ct.Trim().Equals("Undead", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <c>true</c> if the creature's type is "Construct".
    /// <para><b>PHB 3.5e p.307 (Construct type):</b> Constructs are animated objects
    /// or artificially constructed creatures. Immune to mind-affecting effects,
    /// poison, sleep, paralysis, stunning, disease, death effects, necromancy,
    /// critical hits, nonlethal damage, ability damage/drain, fatigue, exhaustion,
    /// and energy drain.</para>
    /// <para>Used by: Searing Light (reduced damage), immunity to many debuffs.</para>
    /// </summary>
    public static bool IsConstruct(CharacterController target)
    {
        if (target?.Stats == null) return false;
        string ct = target.Stats.CreatureType;
        return !string.IsNullOrWhiteSpace(ct)
            && ct.Trim().Equals("Construct", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <c>true</c> if the creature's type is "Animal".
    /// <para><b>PHB 3.5e p.305 (Animal type):</b> Animals are non-humanoid creatures,
    /// usually vertebrates with no magical abilities and no innate capacity for
    /// language or culture. Int ≤ 2.</para>
    /// <para>Used by: Hold Animal, Calm Animals, Dominate Animal, Speak with Animals,
    /// Animal Growth, Charm Animal.</para>
    /// </summary>
    public static bool IsAnimal(CharacterController target)
    {
        if (target?.Stats == null) return false;
        string ct = target.Stats.CreatureType;
        return !string.IsNullOrWhiteSpace(ct)
            && ct.Trim().Equals("Animal", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <c>true</c> if the creature's type is "Plant".
    /// <para><b>PHB 3.5e p.313 (Plant type):</b> Plants are vegetable creatures.
    /// Immune to mind-affecting effects, poison, sleep, paralysis, polymorph,
    /// stunning, and critical hits.</para>
    /// <para>Used by: Command Plants, Blight, Plant Growth, Speak with Plants.</para>
    /// </summary>
    public static bool IsPlant(CharacterController target)
    {
        if (target?.Stats == null) return false;
        string ct = target.Stats.CreatureType;
        return !string.IsNullOrWhiteSpace(ct)
            && ct.Trim().Equals("Plant", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <c>true</c> if the creature's type is "Outsider" or "Extraplanar".
    /// <para><b>PHB 3.5e p.313 (Outsider type):</b> An outsider is at least partially
    /// composed of the essence of a plane other than the Material Plane.
    /// The Extraplanar subtype applies when on a plane other than its native.</para>
    /// <para>Used by: Dismissal, Banishment, Protection from Evil/Good
    /// (blocks summoned creatures), Holy Word / Blasphemy.</para>
    /// </summary>
    public static bool IsOutsiderOrExtraplanar(CharacterController target)
    {
        if (target?.Stats == null) return false;
        string ct = (target.Stats.CreatureType ?? "").Trim().ToLowerInvariant();
        return ct == "outsider" || ct == "extraplanar";
    }

    /// <summary>
    /// Returns <c>true</c> if the creature is "living" — neither Undead nor Construct.
    /// <para><b>PHB 3.5e p.309:</b> Undead and constructs are not alive in the
    /// conventional sense; they are immune to effects that target living creatures
    /// (e.g., [Fear], [Death], many necromancy spells, healing).</para>
    /// <para>Used by: Ghoul Touch, Cause Fear, Scare, Fear, Touch of Idiocy,
    /// Vampiric Touch, Death effects.</para>
    /// </summary>
    /// <remarks>Delegates to <see cref="SpellUtilities.IsLivingCreatureForFear"/>.</remarks>
    public static bool IsLivingCreature(CharacterController target)
        => SpellUtilities.IsLivingCreatureForFear(target);

    /// <summary>
    /// Returns the normalised creature type string (lower-cased, trimmed).
    /// Returns <c>"humanoid"</c> if the type is null or empty (default for PCs).
    /// </summary>
    public static string GetCreatureType(CharacterController target)
    {
        if (target?.Stats == null) return "humanoid";
        string ct = target.Stats.CreatureType;
        return string.IsNullOrWhiteSpace(ct) ? "humanoid" : ct.Trim().ToLowerInvariant();
    }

    // ════════════════════════════════════════════════════════════
    //  Mind-Affecting Immunity
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns <c>true</c> if the creature is immune to [Mind-Affecting] effects.
    /// <para><b>PHB 3.5e p.309:</b> Undead, constructs, oozes, plants, and vermin
    /// are typically immune to all mind-affecting effects (charms, compulsions,
    /// morale effects, patterns, and phantasms).</para>
    /// <para>Used by: Charm Person/Monster, Dominate Person/Monster, Hold Person/Monster,
    /// Daze, Confusion, Suggestion, Sleep, Hypnotism, etc.</para>
    /// </summary>
    /// <remarks>Delegates to <see cref="SpellUtilities.IsImmuneToMindAffecting"/>.</remarks>
    public static bool IsImmuneToMindAffecting(CharacterController target)
        => SpellUtilities.IsImmuneToMindAffecting(target);

    // ════════════════════════════════════════════════════════════
    //  Hit Dice Queries
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the effective Hit Dice of the target, used for HD-limited
    /// and HD-pool spell targeting.
    /// <para><b>PHB 3.5e p.175:</b> Some spells restrict targets by HD
    /// (e.g., Daze ≤ 4 HD, Cause Fear ≤ 5 HD). Character levels count
    /// as HD for this purpose.</para>
    /// </summary>
    /// <remarks>Delegates to <see cref="TeamUtility.GetHitDice"/>.</remarks>
    public static int GetHitDice(CharacterController target)
        => TeamUtility.GetHitDice(target);

    /// <summary>
    /// Returns <c>true</c> if the target's HD do not exceed the specified limit.
    /// <para>Common limits: Daze = 4 HD, Charm Person = 4 HD (implementation),
    /// Cause Fear = 5 HD, Sleep = 4 HD pool, Color Spray = varies.</para>
    /// </summary>
    public static bool IsWithinHDLimit(CharacterController target, int maxHD)
        => GetHitDice(target) <= maxHD;

    // ════════════════════════════════════════════════════════════
    //  Compound Targeting Predicates
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Validates a target for "Person" spells: must be humanoid.
    /// <para><b>PHB 3.5e:</b> Enlarge Person (p.226), Reduce Person (p.268)
    /// — "This spell causes instant growth of a humanoid creature."
    /// Mass versions (p.226, p.268) target one humanoid per CL.</para>
    /// </summary>
    public static bool IsValidPersonSpellTarget(CharacterController target)
    {
        return IsValidAliveTarget(target) && IsHumanoid(target);
    }

    /// <summary>
    /// Validates a target for humanoid + mind-affecting spells with an optional HD limit.
    /// <para><b>PHB 3.5e:</b>
    /// <list type="bullet">
    ///   <item>Hold Person (p.241): "one humanoid creature", mind-affecting, no HD limit.</item>
    ///   <item>Charm Person (p.209): "one humanoid creature", mind-affecting. Implementation uses HD ≤ 4.</item>
    ///   <item>Daze (p.217): "one humanoid creature of 4 HD or less", mind-affecting.</item>
    ///   <item>Dominate Person (p.224): "one humanoid", mind-affecting, no HD limit.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="target">The target to validate.</param>
    /// <param name="maxHD">Maximum HD allowed, or 0 for no HD limit.</param>
    /// <returns><c>true</c> if the target passes humanoid + mind-affecting + HD checks.</returns>
    public static bool IsValidHumanoidMindAffectingTarget(
        CharacterController target, int maxHD = 0)
    {
        if (!IsValidAliveTarget(target)) return false;
        if (!IsHumanoid(target)) return false;
        if (IsImmuneToMindAffecting(target)) return false;
        if (maxHD > 0 && GetHitDice(target) > maxHD) return false;
        return true;
    }

    /// <summary>
    /// Validates a target for "any creature, mind-affecting" spells
    /// (e.g., Charm Monster, Hold Monster, Confusion).
    /// <para><b>PHB 3.5e:</b>
    /// <list type="bullet">
    ///   <item>Charm Monster (p.209): "one living creature", mind-affecting.</item>
    ///   <item>Hold Monster (p.241): "one living creature", mind-affecting.</item>
    ///   <item>Confusion (p.212): area effect, mind-affecting, compulsion.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static bool IsValidMindAffectingTarget(CharacterController target)
    {
        if (!IsValidAliveTarget(target)) return false;
        if (IsImmuneToMindAffecting(target)) return false;
        return true;
    }

    /// <summary>
    /// Validates a target for "living creature" spells (Fear effects,
    /// Ghoul Touch, Vampiric Touch, etc.).
    /// <para><b>PHB 3.5e p.309:</b> Undead and constructs are not living
    /// and are immune to effects requiring a living target.</para>
    /// </summary>
    public static bool IsValidLivingTarget(CharacterController target)
    {
        return IsValidAliveTarget(target) && IsLivingCreature(target);
    }

    /// <summary>
    /// Validates a target for "living humanoid" spells (Ghoul Touch).
    /// <para><b>PHB 3.5e p.235:</b> Ghoul Touch — "one living humanoid".</para>
    /// </summary>
    public static bool IsValidLivingHumanoidTarget(CharacterController target)
    {
        return IsValidAliveTarget(target)
            && IsLivingCreature(target)
            && IsHumanoid(target);
    }

    // ════════════════════════════════════════════════════════════
    //  Creature Type Filtering (for Mass / AoE Spells)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Filters a list of characters to only those matching a creature type
    /// predicate, removing nulls and dead characters.
    /// <para><b>PHB 3.5e p.175:</b> Area spells affect all valid targets in
    /// the area; each target must individually meet any creature type
    /// restrictions specified by the spell.</para>
    /// </summary>
    /// <param name="candidates">All potential targets in the area.</param>
    /// <param name="typePredicate">Creature-type filter (e.g., <c>IsAnimal</c>).</param>
    /// <returns>List of alive targets matching the predicate.</returns>
    public static List<CharacterController> FilterByCreatureType(
        List<CharacterController> candidates,
        Func<CharacterController, bool> typePredicate)
    {
        var result = new List<CharacterController>();
        if (candidates == null) return result;
        foreach (var c in candidates)
        {
            if (!IsValidAliveTarget(c)) continue;
            if (typePredicate != null && !typePredicate(c)) continue;
            result.Add(c);
        }
        return result;
    }

    /// <summary>
    /// Filters to humanoid targets only (for Mass Enlarge/Reduce Person, etc.).
    /// </summary>
    public static List<CharacterController> FilterHumanoids(
        List<CharacterController> candidates)
        => FilterByCreatureType(candidates, IsHumanoid);

    /// <summary>
    /// Filters to animal targets only (for Calm Animals, Animal Growth, etc.).
    /// </summary>
    public static List<CharacterController> FilterAnimals(
        List<CharacterController> candidates)
        => FilterByCreatureType(candidates, IsAnimal);

    // ════════════════════════════════════════════════════════════
    //  Alignment Queries for Targeting
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns <c>true</c> if the target's alignment falls on the specified
    /// moral/ethical axis.  Used by alignment-burst spells.
    /// <para><b>PHB 3.5e:</b>
    /// <list type="bullet">
    ///   <item>Holy Smite (p.241): full damage to Evil, half to non-Good non-Evil.</item>
    ///   <item>Unholy Blight (p.297): full damage to Good, half to non-Good non-Evil.</item>
    ///   <item>Chaos Hammer (p.208): full damage to Lawful, half to non-Lawful non-Chaotic.</item>
    ///   <item>Order's Wrath (p.258): full damage to Chaotic, half to non-Lawful non-Chaotic.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static bool IsAlignmentOnAxis(Alignment alignment, string axis)
    {
        switch ((axis ?? "").Trim().ToLowerInvariant())
        {
            case "good":    return AlignmentHelper.IsGood(alignment);
            case "evil":    return AlignmentHelper.IsEvil(alignment);
            case "lawful":  return AlignmentHelper.IsLawful(alignment);
            case "chaotic": return AlignmentHelper.IsChaotic(alignment);
            default:        return false;
        }
    }

    /// <summary>
    /// Returns <c>true</c> if the target's alignment is on the Good axis.
    /// </summary>
    public static bool IsGoodAligned(CharacterController target)
    {
        if (target?.Stats == null) return false;
        return AlignmentHelper.IsGood(target.Stats.CharacterAlignment);
    }

    /// <summary>
    /// Returns <c>true</c> if the target's alignment is on the Evil axis.
    /// </summary>
    public static bool IsEvilAligned(CharacterController target)
    {
        if (target?.Stats == null) return false;
        return AlignmentHelper.IsEvil(target.Stats.CharacterAlignment);
    }

    /// <summary>
    /// Returns <c>true</c> if the target's alignment is on the Lawful axis.
    /// </summary>
    public static bool IsLawfulAligned(CharacterController target)
    {
        if (target?.Stats == null) return false;
        return AlignmentHelper.IsLawful(target.Stats.CharacterAlignment);
    }

    /// <summary>
    /// Returns <c>true</c> if the target's alignment is on the Chaotic axis.
    /// </summary>
    public static bool IsChaoticAligned(CharacterController target)
    {
        if (target?.Stats == null) return false;
        return AlignmentHelper.IsChaotic(target.Stats.CharacterAlignment);
    }

    // ════════════════════════════════════════════════════════════
    //  Range & Distance Helpers
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the grid distance in squares between two characters.
    /// D&D 3.5e uses a 5-ft grid; 1 square = 5 ft.
    /// <para><b>PHB 3.5e p.176:</b> Spell range categories —
    /// Personal, Touch, Close (25 ft + 5 ft/2 levels),
    /// Medium (100 ft + 10 ft/level), Long (400 ft + 40 ft/level).</para>
    /// </summary>
    public static int GetDistanceSquares(CharacterController a, CharacterController b)
    {
        if (a == null || b == null) return int.MaxValue;
        return SquareGridUtils.GetDistance(a.GridPosition, b.GridPosition);
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="target"/> is within
    /// <paramref name="maxSquares"/> grid squares of <paramref name="source"/>.
    /// </summary>
    public static bool IsWithinRange(CharacterController source,
        CharacterController target, int maxSquares)
    {
        return GetDistanceSquares(source, target) <= maxSquares;
    }

    /// <summary>
    /// Returns <c>true</c> if the target is within 30 ft (6 squares) of the
    /// anchor position. Common for "no two of which can be more than 30 ft apart"
    /// constraints on mass/area spells.
    /// <para><b>PHB 3.5e:</b> Mass Enlarge Person (p.226), Mass Reduce Person (p.268),
    /// Mass Bull's Strength, etc. — "No two subjects more than 30 ft apart."</para>
    /// </summary>
    public static bool IsWithin30Ft(CharacterController target, Vector2Int anchor)
    {
        if (target == null) return false;
        return SquareGridUtils.GetDistance(target.GridPosition, anchor) <= 6;
    }

    // ════════════════════════════════════════════════════════════
    //  Mass Spell Target Gathering
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Gathers valid targets for a mass spell with a "30 ft spread" constraint.
    /// Filters by a creature-type predicate, enforces the 30-ft proximity rule,
    /// and caps at <paramref name="maxTargets"/>.
    /// <para>Optionally filters to allies of the caster only (for beneficial spells).</para>
    /// </summary>
    /// <param name="allCharacters">All combatants on the field.</param>
    /// <param name="caster">The caster (used for ally filtering).</param>
    /// <param name="anchor">The primary target / centre of the spread.</param>
    /// <param name="maxTargets">Maximum number of targets (usually = caster level).</param>
    /// <param name="typePredicate">Creature-type filter, or null for any living creature.</param>
    /// <param name="alliesOnly">If true, only include allies of the caster.</param>
    /// <returns>Capped list of valid targets within 30 ft of the anchor.</returns>
    public static List<CharacterController> GatherMassSpellTargets(
        List<CharacterController> allCharacters,
        CharacterController caster,
        CharacterController anchor,
        int maxTargets,
        Func<CharacterController, bool> typePredicate = null,
        bool alliesOnly = false)
    {
        var targets = new List<CharacterController>();
        if (allCharacters == null || anchor == null) return targets;

        // Always include the anchor first if it passes the predicate
        if (IsValidAliveTarget(anchor)
            && (typePredicate == null || typePredicate(anchor)))
        {
            targets.Add(anchor);
        }

        Vector2Int anchorPos = anchor.GridPosition;

        foreach (var candidate in allCharacters)
        {
            if (candidate == anchor) continue;
            if (!IsValidAliveTarget(candidate)) continue;
            if (typePredicate != null && !typePredicate(candidate)) continue;

            if (alliesOnly && candidate != caster && !TeamUtility.IsAlly(caster, candidate))
                continue;

            if (!IsWithin30Ft(candidate, anchorPos))
                continue;

            targets.Add(candidate);
        }

        // Cap at max targets
        if (targets.Count > maxTargets)
            targets.RemoveRange(maxTargets, targets.Count - maxTargets);

        return targets;
    }
}

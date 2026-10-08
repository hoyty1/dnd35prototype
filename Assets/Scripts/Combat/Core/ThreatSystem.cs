using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// D&D 3.5 Threat System - Attacks of Opportunity
// ============================================================================
//
// Rules implemented:
// 1. Characters threaten all squares within their reach (default 1 = 8 adjacent)
// 2. Moving OUT of a threatened square provokes an AoO
// 3. Each character gets 1 AoO per round (Combat Reflexes: 1 + DEX mod)
// 4. 5-foot step does NOT provoke AoOs
// 5. AoO is a free melee attack resolved immediately
// ============================================================================

/// <summary>
/// Stores info about a single AoO that would be provoked during movement.
/// </summary>
[System.Serializable]
public class AoOThreatInfo
{
    /// <summary>The enemy character that would make the AoO.</summary>
    public CharacterController Threatener;

    /// <summary>The square the mover is leaving that provokes this AoO.</summary>
    public Vector2Int ProvokedAtSquare;

    /// <summary>Index in the path where this AoO is provoked.</summary>
    public int PathIndex;

    public AoOThreatInfo(CharacterController threatener, Vector2Int square, int pathIndex)
    {
        Threatener = threatener;
        ProvokedAtSquare = square;
        PathIndex = pathIndex;
    }

    public override string ToString()
    {
        return $"{Threatener.Stats.CharacterName} (at [{ProvokedAtSquare.x},{ProvokedAtSquare.y}])";
    }
}

/// <summary>
/// Result of an AoO-aware pathfinding query.
/// Contains both the path and a list of AoOs that would be provoked.
/// </summary>
public class AoOPathResult
{
    /// <summary>The computed path (list of cells from start to destination, excluding start).</summary>
    public List<Vector2Int> Path = new List<Vector2Int>();

    /// <summary>AoOs that would be provoked along this path.</summary>
    public List<AoOThreatInfo> ProvokedAoOs = new List<AoOThreatInfo>();

    /// <summary>True if the path would provoke at least one AoO.</summary>
    public bool ProvokesAoOs => ProvokedAoOs.Count > 0;

    /// <summary>Get a distinct list of enemy names that would get AoOs.</summary>
    public List<string> GetThreateningEnemyNames()
    {
        var names = new HashSet<string>();
        foreach (var aoo in ProvokedAoOs)
            names.Add(aoo.Threatener.Stats.CharacterName);
        return new List<string>(names);
    }
}

/// <summary>
/// Static utility class for D&D 3.5 threat and Attack of Opportunity calculations.
/// Tracks AoO usage per character and provides threat queries.
/// </summary>
public static class ThreatSystem
{
    // ========================================================================
    // THREAT QUERIES
    // ========================================================================

    /// <summary>
    /// Get all squares threatened by a character based on their reach.
    /// Default reach for Medium creatures = 1 square = 8 adjacent squares.
    /// </summary>
    /// <param name="character">The threatening character.</param>
    /// <returns>Set of grid coordinates that this character threatens.</returns>
    public static HashSet<Vector2Int> GetThreatenedSquares(CharacterController character)
    {
        var threatened = new HashSet<Vector2Int>();
        if (character == null || character.Stats == null || character.Stats.IsDead) return threatened;

        // ============================================================
        // D&D 3.5 Rule: Only characters with MELEE weapons threaten squares.
        // Ranged weapons (bows, crossbows, slings) do NOT threaten.
        // Unarmed/natural weapons DO threaten (they count as melee).
        // ============================================================
        if (!character.HasMeleeWeaponEquipped())
        {
            Debug.Log($"[ThreatSystem] {character.Stats.CharacterName} has NO melee weapon — threatens 0 squares (ranged only)");
            return threatened;
        }

        if (character.Stats.ActiveConditions != null)
        {
            for (int i = 0; i < character.Stats.ActiveConditions.Count; i++)
            {
                CombatConditionType activeType = ConditionRules.Normalize(character.Stats.ActiveConditions[i].Type);
                ConditionDefinition def = ConditionRules.GetDefinition(activeType);
                if (def.PreventsThreatening)
                {
                    if (activeType == CombatConditionType.Grappled)
                        Debug.Log($"[ThreatSystem] {character.Stats.CharacterName} is grappled and threatens 0 squares.");
                    return threatened;
                }
            }
        }

        int minThreatDistance = character.GetMeleeMinAttackDistance();
        int maxThreatDistance = character.GetMeleeMaxAttackDistance();
        List<Vector2Int> occupiedSquares = character.GetOccupiedSquares();

        // Threat ring(s) are measured with Chebyshev distance (diagonals = 1)
        // from every square occupied by the creature footprint.
        for (int i = 0; i < occupiedSquares.Count; i++)
        {
            Vector2Int origin = occupiedSquares[i];
            for (int dx = -maxThreatDistance; dx <= maxThreatDistance; dx++)
            {
                for (int dy = -maxThreatDistance; dy <= maxThreatDistance; dy++)
                {
                    Vector2Int target = new Vector2Int(origin.x + dx, origin.y + dy);
                    int distance = SquareGridUtils.GetChebyshevDistance(origin, target);
                    if (distance >= minThreatDistance && distance <= maxThreatDistance)
                        threatened.Add(target);
                }
            }
        }

        for (int i = 0; i < occupiedSquares.Count; i++)
            threatened.Remove(occupiedSquares[i]);

        Vector2Int basePos = character.GridPosition;
        Debug.Log($"[ThreatSystem] {character.Stats.CharacterName} threatens {threatened.Count} squares from footprint base ({basePos.x},{basePos.y}) at Chebyshev distances {minThreatDistance}-{maxThreatDistance} (melee weapon equipped)");
        return threatened;
    }

    /// <summary>
    /// Check if a specific square is threatened by any enemy of the given character.
    /// </summary>
    /// <param name="square">The square to check.</param>
    /// <param name="mover">The character who would be moving (used to determine who is an enemy).</param>
    /// <param name="allCharacters">All characters in the combat.</param>
    /// <returns>True if any living enemy threatens this square.</returns>
    public static bool IsSquareThreatened(Vector2Int square, CharacterController mover, List<CharacterController> allCharacters)
    {
        if (mover == null || mover.Stats == null)
        {
            Debug.LogWarning($"[ThreatSystem] IsSquareThreatened called with invalid mover at ({square.x},{square.y}).");
            return false;
        }

        if (allCharacters == null || allCharacters.Count == 0)
            return false;

        foreach (var character in allCharacters)
        {
            if (character == null || character == mover) continue;
            if (character.Stats == null || character.Stats.IsDead) continue;
            if (character.Team == mover.Team) continue; // Same team

            if (GetThreatenedSquares(character).Contains(square))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get which enemies threaten a specific square.
    /// </summary>
    /// <param name="square">The square to check.</param>
    /// <param name="mover">The character moving (determines who is an enemy).</param>
    /// <param name="allCharacters">All characters in combat.</param>
    /// <returns>List of enemy characters that threaten this square.</returns>
    public static List<CharacterController> GetThreateningEnemies(Vector2Int square, CharacterController mover, List<CharacterController> allCharacters)
    {
        var threateners = new List<CharacterController>();
        var seenThreateners = new HashSet<CharacterController>();

        if (mover == null || mover.Stats == null)
        {
            Debug.LogWarning($"[ThreatSystem] GetThreateningEnemies called with invalid mover at ({square.x},{square.y}).");
            return threateners;
        }

        if (allCharacters == null || allCharacters.Count == 0)
            return threateners;

        foreach (var character in allCharacters)
        {
            if (character == null || character == mover) continue;
            if (character.Stats == null || character.Stats.IsDead) continue;
            if (character.Team == mover.Team) continue;

            if (GetThreatenedSquares(character).Contains(square) && seenThreateners.Add(character))
            {
                // Resilient Sphere blocks threat across the barrier
                if (ResilientSphereAreaEffect.DoesSphereBlockInteraction(character, mover))
                    continue;

                threateners.Add(character);
            }
        }

        if (threateners.Count > 0)
        {
            Debug.Log($"[ThreatSystem] Square ({square.x},{square.y}) threatened by: {string.Join(", ", threateners.ConvertAll(c => c != null && c.Stats != null ? c.Stats.CharacterName : "<unknown>"))}");
        }

        return threateners;
    }

    /// <summary>
    /// Estimate expected incoming damage if attacker performs a ranged attack while threatened.
    /// This is intended for AI risk assessment and does not guarantee exact combat outcome.
    /// </summary>
    public static float CalculateExpectedAoODamageForRangedAttack(CharacterController attacker, List<CharacterController> threateningEnemies = null)
    {
        if (attacker == null || attacker.Stats == null || attacker.Stats.IsDead)
            return 0f;

        if (threateningEnemies == null)
        {
            GameManager gm = GameManager.Instance;
            List<CharacterController> allCharacters = gm != null ? gm.GetAllCharactersForAI() : null;
            threateningEnemies = GetThreateningEnemies(attacker.GridPosition, attacker, allCharacters);
        }

        if (threateningEnemies == null || threateningEnemies.Count == 0)
            return 0f;

        float totalExpectedDamage = 0f;

        for (int i = 0; i < threateningEnemies.Count; i++)
        {
            CharacterController enemy = threateningEnemies[i];
            if (enemy == null || enemy.Stats == null)
                continue;

            if (!CanMakeAoO(enemy))
                continue;

            totalExpectedDamage += EstimateExpectedAoODamage(enemy, attacker);
        }

        return totalExpectedDamage;
    }

    private static float EstimateExpectedAoODamage(CharacterController threatener, CharacterController target)
    {
        if (threatener == null || threatener.Stats == null || target == null || target.Stats == null)
            return 0f;

        int meleeAttackBonus = threatener.Stats.GetMeleeAttackBonus();
        int targetArmorClass = target.Stats.GetArmorClass();

        // d20 chance approximation. Clamp to preserve natural 1/20-style floor/ceiling behavior.
        float hitChance = Mathf.Clamp((21f + meleeAttackBonus - targetArmorClass) / 20f, 0.05f, 0.95f);
        float averageDamage = EstimateAverageMeleeDamage(threatener);

        return hitChance * averageDamage;
    }

    private static float EstimateAverageMeleeDamage(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null)
            return 0f;

        ItemData weapon = attacker.GetEquippedMainWeapon();
        if (weapon != null && weapon.IsWeapon)
        {
            // The dice the attack code rolls for the wielder's size (DMG Tables 2-2 and 2-3).
            attacker.GetScaledWeaponDamageDice(weapon, out int count, out int dice);
            float diceAverage = count > 0 && dice > 0
                ? count * (dice + 1) * 0.5f
                : 2.5f;

            float damageBonus = weapon.BonusDamage;
            if (weapon.WeaponCat == WeaponCategory.Melee || weapon.IsThrown)
                damageBonus += attacker.Stats.STRMod;

            return Mathf.Max(1f, diceAverage + damageBonus);
        }

        // Natural attack (size-scaled, with its own Strength multiple) or unarmed strike (size- and monk-scaled).
        NaturalAttackDefinition natural = attacker.Stats.GetPrimaryNaturalAttack();
        if (natural != null)
        {
            attacker.Stats.GetScaledNaturalAttackDamage(natural, out int naturalCount, out int naturalDice);
            return Mathf.Max(1f, naturalCount * (naturalDice + 1) * 0.5f + attacker.Stats.GetNaturalAttackDamageBonus(natural));
        }

        var unarmed = attacker.GetUnarmedDamage();
        return Mathf.Max(1f, unarmed.damageCount * (unarmed.damageDice + 1) * 0.5f + unarmed.bonusDamage + attacker.Stats.STRMod);
    }

    // ========================================================================
    // AoO USAGE TRACKING
    // ========================================================================

    /// <summary>
    /// Reset AoO counters for a character at the start of their turn.
    /// </summary>
    public static void ResetAoOForTurn(CharacterController character)
    {
        if (character == null || character.Stats == null) return;

        character.Stats.AttacksOfOpportunityUsed = 0;
        character.Stats.MaxAttacksOfOpportunity = FeatManager.GetMaxAoOPerRound(character.Stats);

        bool hasCR = FeatManager.HasCombatReflexes(character.Stats);
        Debug.Log($"[ThreatSystem] {character.Stats.CharacterName} AoO reset: {character.Stats.MaxAttacksOfOpportunity} AoOs available" +
                  (hasCR ? $" (Combat Reflexes: 1 + {character.Stats.DEXMod} DEX mod)" : " (default: 1)"));
    }

    /// <summary>
    /// Check if a character can still make an AoO this round.
    /// D&D 3.5: Only characters with melee weapons can make AoOs.
    /// </summary>
    public static bool CanMakeAoO(CharacterController character)
    {
        if (character == null || character.Stats == null || character.Stats.IsDead) return false;

        if (!character.Stats.CanMakeAttacksOfOpportunity)
            return false;

        // Ranged-only characters cannot make AoOs
        if (!character.HasMeleeWeaponEquipped()) return false;

        if (character.Stats.ActiveConditions != null)
        {
            for (int i = 0; i < character.Stats.ActiveConditions.Count; i++)
            {
                CombatConditionType activeType = ConditionRules.Normalize(character.Stats.ActiveConditions[i].Type);
                ConditionDefinition def = ConditionRules.GetDefinition(activeType);
                if (def.PreventsAoO)
                {
                    if (activeType == CombatConditionType.Grappled)
                        Debug.Log($"[ThreatSystem] {character.Stats.CharacterName} is grappled and cannot make attacks of opportunity.");
                    return false;
                }
            }
        }

        return character.Stats.AttacksOfOpportunityUsed < character.Stats.MaxAttacksOfOpportunity;
    }

    /// <summary>
    /// Use one AoO for a character. Returns false if none remaining.
    /// </summary>
    public static bool UseAoO(CharacterController character)
    {
        if (!CanMakeAoO(character)) return false;
        character.Stats.AttacksOfOpportunityUsed++;
        Debug.Log($"[ThreatSystem] {character.Stats.CharacterName} used AoO ({character.Stats.AttacksOfOpportunityUsed}/{character.Stats.MaxAttacksOfOpportunity})");
        return true;
    }

    // ========================================================================
    // PATH AoO ANALYSIS
    // ========================================================================

    /// <summary>
    /// Analyze a movement path and determine which AoOs would be provoked.
    /// PHB p.137-138: moving out of a threatened square provokes an AoO from the threatening
    /// enemy, and moving out of more than one square threatened by the same enemy in the same
    /// round counts as only one opportunity for that enemy, so Combat Reflexes (PHB p.92) never
    /// gives a second movement AoO against the same mover in one round (owner ruling 2026-10-08,
    /// CMB-128: per round, not per movement). Each enemy is therefore listed at most once, at
    /// the first square it threatens, only if it has an AoO left this round, and not at all when
    /// its movement opportunity against this mover already came earlier in the mover's round
    /// (<see cref="HasHadMovementOpportunity"/>: the first move of a double move, a move before a
    /// bull rush follow, a charge before its bull rush follow). The executors record the
    /// opportunity when they resolve the step (<see cref="RecordMovementOpportunity"/>); this
    /// analysis only reads it, so calling it changes nothing. Its result (the PC AoO confirmation
    /// prompt, the AI's provoke counts) therefore reflects the record; the hover path preview
    /// (GameManager.GetPreviewThreatenedSquares) and the A* threat cost (SquareGrid.FindSafePath)
    /// use raw threatened squares and do not (GRID-020).
    /// With <paramref name="forcedMovement"/> (a bull rush push) the round record is not read:
    /// whether a pushed defender shares it, and over which round, is an open owner question (CMB-150).
    /// </summary>
    /// <param name="mover">The character moving.</param>
    /// <param name="path">The movement path (list of squares, NOT including the starting square).</param>
    /// <param name="allCharacters">All characters in combat.</param>
    /// <returns>List of AoOs that would be provoked.</returns>
    public static List<AoOThreatInfo> AnalyzePathForAoOs(CharacterController mover, List<Vector2Int> path, List<CharacterController> allCharacters, bool suppressFirstSquareAoO = false, bool forcedMovement = false)
    {
        var provokedAoOs = new List<AoOThreatInfo>();
        if (mover == null || mover.Stats == null) return provokedAoOs;
        if (path == null || path.Count == 0) return provokedAoOs;
        if (mover.Stats.IsSwarm) return provokedAoOs;

        // Enemies already listed on this path (one opportunity each, PHB p.138).
        var enemyAoOUsedThisMovement = new Dictionary<CharacterController, int>();

        // Build threatened squares for each enemy
        var enemyThreats = new Dictionary<CharacterController, HashSet<Vector2Int>>();
        foreach (var character in allCharacters)
        {
            if (character == mover) continue;
            if (character.Stats.IsDead) continue;
            if (character.Team == mover.Team) continue;
            // Its one movement opportunity against this mover this round already came (CMB-128).
            if (!forcedMovement && HasHadMovementOpportunity(character, mover)) continue;

            enemyThreats[character] = GetThreatenedSquares(character);
            enemyAoOUsedThisMovement[character] = 0;
        }

        // The mover starts at their current base position.
        Vector2Int previousBaseSquare = mover.GridPosition;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int currentBaseSquare = path[i];
            List<Vector2Int> previousOccupiedSquares = mover.GetOccupiedSquaresAt(previousBaseSquare);

            if (suppressFirstSquareAoO && i == 0)
            {
                previousBaseSquare = currentBaseSquare;
                continue;
            }

            // Check each enemy: does leaving any previously occupied square provoke?
            foreach (var kvp in enemyThreats)
            {
                var enemy = kvp.Key;
                var threatenedSquares = kvp.Value;

                bool provoked = false;
                for (int s = 0; s < previousOccupiedSquares.Count; s++)
                {
                    if (threatenedSquares.Contains(previousOccupiedSquares[s]))
                    {
                        provoked = true;
                        break;
                    }
                }

                if (!provoked)
                    continue;

                // Resilient Sphere blocks AoOs across the barrier
                if (ResilientSphereAreaEffect.DoesSphereBlockInteraction(enemy, mover))
                    continue;

                // One opportunity per enemy per round (PHB p.138; earlier moves this round were
                // filtered out above), and only if the enemy still has an AoO left this round
                // (Combat Reflexes raises that cap).
                int usedThisMovement = enemyAoOUsedThisMovement[enemy];
                int remainingGlobal = enemy.Stats.MaxAttacksOfOpportunity - enemy.Stats.AttacksOfOpportunityUsed;

                if (usedThisMovement == 0 && remainingGlobal > 0)
                {
                    Vector2Int provokedFrom = previousOccupiedSquares.Count > 0
                        ? previousOccupiedSquares[0]
                        : previousBaseSquare;

                    provokedAoOs.Add(new AoOThreatInfo(enemy, provokedFrom, i));
                    enemyAoOUsedThisMovement[enemy]++;
                    Debug.Log($"[ThreatSystem] Movement from base ({previousBaseSquare.x},{previousBaseSquare.y}) to ({currentBaseSquare.x},{currentBaseSquare.y}) provokes AoO from {enemy.Stats.CharacterName}");
                }
            }

            previousBaseSquare = currentBaseSquare;
        }

        if (provokedAoOs.Count > 0)
        {
            Debug.Log($"[ThreatSystem] Path analysis: {provokedAoOs.Count} AoOs would be provoked");
        }
        else
        {
            Debug.Log("[ThreatSystem] Path analysis: no AoOs provoked (safe path)");
        }

        return provokedAoOs;
    }

    // ========================================================================
    // MOVEMENT OPPORTUNITIES PER ROUND (PHB p.138, CMB-128)
    // ========================================================================

    /// <summary>
    /// True when <paramref name="threatener"/>'s movement opportunity against <paramref name="mover"/>
    /// already came this round: earlier in the mover's round (since the start of its turn) the mover
    /// left a square the threatener threatens and an executor recorded it
    /// (<see cref="RecordMovementOpportunity"/>). PHB p.138: moving out of more than one square
    /// threatened by the same opponent in the same round is one opportunity for that opponent
    /// (owner ruling 2026-10-08). Read by <see cref="AnalyzePathForAoOs"/>.
    /// </summary>
    public static bool HasHadMovementOpportunity(CharacterController threatener, CharacterController mover)
    {
        if (threatener == null || mover == null)
            return false;
        return mover.MovementOpportunityThreateners.Contains(threatener);
    }

    /// <summary>
    /// Records that <paramref name="mover"/> left a square <paramref name="threatener"/> threatens, so
    /// that opponent gets no further movement opportunity against it this round (PHB p.138). The
    /// movement executors call it for each opponent listed at the step they resolve, whether or not
    /// the AoO is then made (the opportunity came either way): GameManager.ResolveMovementAoOsBeforeStep
    /// (PC moves and withdraw, NPC, summon and compulsion moves, both charge paths, crawl, the overrun
    /// continuation) and the bull rush follow (GameManager.ResolveBullRushStepAoOs). Movement executors
    /// that resolve no movement AoOs at all also record nothing: the PC move-through overrun and its
    /// Normal Move branch (CMB-151), the attacker's follow after a targeted overrun push
    /// (TryPushTargetAway, CMB-116, CMB-151) and the grapple moves (GrappleSystem.ExecuteGrappleMovement,
    /// ExecuteFreeAdjacentGrappleMovement, CMB-152). Not called for provocations that are not movement
    /// (casting, ranged attacks, standing up, maneuver starts, the bull rush entry into the defender's
    /// space, CMB-113) nor for a bull rush push (CMB-150).
    /// </summary>
    public static void RecordMovementOpportunity(CharacterController threatener, CharacterController mover)
    {
        if (threatener == null || mover == null || threatener == mover)
            return;
        mover.MovementOpportunityThreateners.Add(threatener);
    }

    /// <summary>
    /// Starts a new round for <see cref="HasHadMovementOpportunity"/>: called by
    /// CharacterController.StartNewTurn (the mover's round boundary) and when a slot is reset.
    /// </summary>
    public static void ClearMovementOpportunities(CharacterController mover)
    {
        if (mover == null)
            return;
        mover.MovementOpportunityThreateners.Clear();
    }

    // ========================================================================
    // AoO RESOLUTION
    // ========================================================================

    private static ItemData ResolveBestAoOWeapon(CharacterController threatener)
    {
        if (threatener == null)
            return null;

        ItemData mainWeapon = threatener.GetEquippedMainWeapon();
        if (mainWeapon != null && mainWeapon.WeaponCat == WeaponCategory.Melee)
            return mainWeapon;

        ItemData offHandWeapon = threatener.GetOffHandAttackWeapon();
        if (offHandWeapon != null && offHandWeapon.WeaponCat == WeaponCategory.Melee)
            return offHandWeapon;

        // Null means unarmed/natural fallback in CharacterController.Attack.
        return null;
    }

    /// <summary>
    /// Execute an Attack of Opportunity. The threatener makes an immediate free melee attack.
    /// </summary>
    /// <param name="threatener">The character making the AoO.</param>
    /// <param name="target">The character being attacked (the one who provoked).</param>
    /// <param name="trigger">What provoked the AoO, for the ScenarioHooks.AoOResolved test hook only
    /// ("spellcast", "maneuver", "standup", "ranged", ...); null means "movement" or "other".</param>
    /// <returns>The CombatResult of the AoO, or null if the AoO couldn't be made.</returns>
    public static CombatResult ExecuteAoO(CharacterController threatener, CharacterController target, bool isFromMovement = false, string trigger = null)
    {
        if (threatener == null || target == null || threatener.Stats == null || target.Stats == null)
            return null;

        // Resilient Sphere blocks AoOs across the barrier in either direction
        if (ResilientSphereAreaEffect.DoesSphereBlockInteraction(threatener, target))
        {
            Debug.Log($"[ThreatSystem] {threatener.Stats.CharacterName} cannot make AoO against {target.Stats.CharacterName}: Resilient Sphere blocks interaction across barrier.");
            return null;
        }

        if (target.HasTotalConcealment(threatener, incomingIsRangedAttack: false))
        {
            Debug.Log($"[ThreatSystem] {threatener.Stats.CharacterName} cannot make AoO against {target.Stats.CharacterName}: target has total concealment.");
            return null;
        }

        if (!UseAoO(threatener))
        {
            Debug.Log($"[ThreatSystem] {threatener.Stats.CharacterName} has no remaining AoOs this round!");
            return null;
        }

        // ── Spring Attack / Shot on the Run: suppress AoO from the attacked target ──
        // D&D 3.5e PHB p.100: When using Spring Attack, the target of the attack
        // does not get an attack of opportunity against the moving character.
        if (isFromMovement && target.Stats != null && target.Stats.IsUsingSpringAttackMovement
            && target.Stats.SpringAttackTarget == threatener)
        {
            Debug.Log($"[ThreatSystem] Spring Attack/Shot on the Run: {threatener.Stats.CharacterName}'s AoO suppressed (was the attack target).");
            return null;
        }

        Debug.Log($"[ThreatSystem] === ATTACK OF OPPORTUNITY ===");
        Debug.Log($"[ThreatSystem] {threatener.Stats.CharacterName} makes AoO against {target.Stats.CharacterName}!");

        // Mobility feat: +4 dodge bonus to AC against AoO from movement
        int mobilityPenalty = 0;
        if (isFromMovement && FeatManager.HasMobility(target.Stats))
        {
            mobilityPenalty = -4; // Applied as attack penalty (equivalent to +4 dodge AC)
            Debug.Log($"[ThreatSystem] Mobility: {target.Stats.CharacterName} gains +4 dodge AC vs movement AoO (applied as -4 attack penalty)");
        }

        // AoO is a single melee attack at full BAB (PHB p.137), so a threatener who flanks
        // the target gets the +2 flanking bonus and flank-based sneak attack (PHB p.153;
        // the defender's Flanked tag gives no AC penalty, CMB-001).
        // Prefer an actually melee-capable equipped weapon so an off-hand melee weapon
        // can still be used when the primary slot is currently ranged.
        ItemData aooWeapon = ResolveBestAoOWeapon(threatener);
        GameManager gm = GameManager.Instance;
        List<CharacterController> allCombatants = gm != null ? gm.GetAllCharactersForAI() : null;
        bool isFlanking = CombatUtils.IsAttackerFlanking(threatener, target, allCombatants, out CharacterController flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string flankPartnerName = flankPartner != null && flankPartner.Stats != null ? flankPartner.Stats.CharacterName : null;
        CombatResult result = threatener.Attack(target, isFlanking, flankBonus, flankPartnerName, null, null, aooWeapon, mobilityPenalty);

        // Mark this as an AoO in the result for logging
        result.IsAttackOfOpportunity = true;

        // Inert test hook; the trigger names what provoked the AoO (movement, spellcast, maneuver, standup, ...).
        ScenarioHooks.AoOResolved?.Invoke(threatener, target, trigger ?? (isFromMovement ? "movement" : "other"), result);

        // Innate trip follow-up (e.g., wolf bite) is a free action and should not consume AoO economy.
        // The one shared rule decides it (only an AoO made with the trigger attack, MM Trip (Ex), CMB-125) and
        // writes its log line and reactions, as on a turn; the aftermath waits for the AoO's own log below.
        SpecialAttackResult freeTripResult = gm != null ? gm.ResolveFreeTripAfterHit(threatener, target, result, null) : null;

        // Log the result
        if (result.Hit)
        {
            string critStr = result.CritConfirmed ? " (CRITICAL HIT!)" : "";
            Debug.Log($"[ThreatSystem] AoO HIT! {threatener.Stats.CharacterName} deals {result.TotalDamage} damage to {target.Stats.CharacterName}{critStr}");
            Debug.Log($"[ThreatSystem] {target.Stats.CharacterName} HP: {result.DefenderHPBefore} → {result.DefenderHPAfter}");

            // Melee reaction effects (Fire Shield, Thorns, etc.) — AoO is a melee attack
            MeleeReactionService.TriggerReactions(threatener, target, result);

            if (result.TargetKilled)
            {
                Debug.Log($"[ThreatSystem] {target.Stats.CharacterName} was SLAIN by the Attack of Opportunity!");
            }
        }
        else
        {
            Debug.Log($"[ThreatSystem] AoO MISS! {threatener.Stats.CharacterName} rolled {result.DieRoll} + mods = {result.TotalRoll} vs AC {result.TargetAC}");
        }

        // The Improved Trip attack after a free trip that landed (PHB p.96, CMB-079), after the AoO's own
        // log and melee reactions; a free trip gets no counter-trip.
        if (freeTripResult != null)
            gm?.HandleTripAftermath(threatener, target, freeTripResult, null);

        Debug.Log($"[ThreatSystem] === END AoO ===");
        return result;
    }

    // ========================================================================
    // SHARED AoO RULES FOR MOVEMENT AND MANEUVERS
    // Used by the GameManager AoO helpers (ResolveMovementAoOsBeforeStep,
    // ResolveManeuverInitiationAoOs) so PCs, NPCs and summons follow one rule.
    // ========================================================================

    /// <summary>The mover's state captured before an AoO resolves.</summary>
    public struct MoverAoOSnapshot
    {
        public bool WasProne;
        public bool WasMovementBlocked;
    }

    public static MoverAoOSnapshot CaptureMoverState(CharacterController mover)
    {
        var snapshot = new MoverAoOSnapshot();
        if (mover == null || mover.Stats == null)
            return snapshot;

        snapshot.WasProne = mover.Stats.IsProne;
        snapshot.WasMovementBlocked = mover.Stats.MovementBlockedByCondition;
        return snapshot;
    }

    /// <summary>
    /// True when the mover is dead, unconscious or at 0 HP or less. Matches the check
    /// <c>CharacterController.MoveAlongPath</c> and the AI routines make after moving.
    /// </summary>
    public static bool IsMoverIncapacitated(CharacterController mover)
    {
        if (mover == null || mover.Stats == null)
            return true;

        return mover.IsDead || mover.IsUnconscious || mover.Stats.IsDead || mover.Stats.CurrentHP <= 0;
    }

    /// <summary>
    /// PHB p.137: an attack of opportunity interrupts the provoking movement. Movement stops
    /// if the AoO incapacitates the mover, knocks it prone (a free trip such as a wolf's bite)
    /// or gives it a condition that prevents movement. Prone or blocking conditions the mover
    /// already had before the AoO do not stop it, so only what the AoO changed counts.
    /// </summary>
    public static bool ShouldStopMovementAfterAoO(CharacterController mover, MoverAoOSnapshot before, out string reason)
    {
        reason = string.Empty;
        if (IsMoverIncapacitated(mover))
        {
            reason = "incapacitated";
            return true;
        }

        if (!before.WasProne && mover.Stats.IsProne)
        {
            reason = "knocked prone";
            return true;
        }

        if (!before.WasMovementBlocked && mover.Stats.MovementBlockedByCondition)
        {
            reason = "unable to move";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Table 8-2 and PHB p.154-158: Grapple, Sunder, Trip and Disarm provoke from the target
    /// unless the attacker has Improved Grapple, Improved Sunder, Improved Trip or Improved
    /// Disarm (trip is always the unarmed version here: trip weapons are not modelled).
    /// Bull rush provokes from every enemy that threatens the attacker; Improved Bull Rush only
    /// removes the defender's AoO (see <see cref="GetManeuverAoOProvokers"/>). Coup de Grace
    /// provokes from every threatening enemy (PHB p.153).
    /// </summary>
    public static bool DoesManeuverProvokeAoO(SpecialAttackType type, CharacterController attacker)
    {
        CharacterStats stats = attacker != null ? attacker.Stats : null;
        switch (type)
        {
            case SpecialAttackType.Grapple:
                return stats == null || !stats.HasFeat("Improved Grapple");
            case SpecialAttackType.Sunder:
                return stats == null || !stats.HasFeat("Improved Sunder");
            case SpecialAttackType.Trip:
                return stats == null || !stats.HasFeat("Improved Trip");
            case SpecialAttackType.Disarm:
                return stats == null || !stats.HasFeat("Improved Disarm");
            case SpecialAttackType.BullRushAttack:
            case SpecialAttackType.BullRushCharge:
            case SpecialAttackType.CoupDeGrace:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The enemies that get an AoO when <paramref name="attacker"/> starts this maneuver: each one
    /// still able to make an AoO this round (<see cref="CanMakeAoO"/>). For a bull rush at the end
    /// of a charge that includes enemies that already had a movement AoO during the charge: entering
    /// the defender's space is the bull rush's own provocation (PHB p.154), separate from the charge
    /// movement, whose squares PHB p.138 counts as one opportunity (owner decision 2026-10-07). So
    /// only an enemy with an AoO left this round, normally through Combat Reflexes, takes a second.
    /// </summary>
    public static List<CharacterController> GetManeuverAoOProvokers(
        CharacterController attacker,
        CharacterController target,
        SpecialAttackType type,
        List<CharacterController> allCharacters)
    {
        var provokers = new List<CharacterController>();
        if (attacker == null || attacker.Stats == null || !DoesManeuverProvokeAoO(type, attacker))
            return provokers;

        if (type == SpecialAttackType.CoupDeGrace)
        {
            provokers = GetThreateningEnemies(attacker.GridPosition, attacker, allCharacters);
        }
        else if (type == SpecialAttackType.BullRushAttack || type == SpecialAttackType.BullRushCharge)
        {
            // PHB p.154: moving into the defender's space provokes from each opponent that
            // threatens you, including the defender; Improved Bull Rush spares only the defender.
            provokers = GetEnemiesThreateningFootprint(attacker, allCharacters);
            if (attacker.Stats.HasFeat("Improved Bull Rush"))
                provokers.Remove(target);
        }
        else if (target != null && target.Stats != null && !target.Stats.IsDead
            && GetEnemiesThreateningFootprint(attacker, allCharacters).Contains(target))
        {
            // Only a target that threatens the attacker gets the AoO (PHB p.137): a reach-weapon
            // disarm or sunder against a foe that cannot reach back provokes nothing.
            provokers.Add(target);
        }

        provokers.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !CanMakeAoO(enemy));
        return provokers;
    }

    /// <summary>
    /// The enemies that get an AoO when <paramref name="actor"/> stands up from prone (PHB p.143,
    /// Table 8-2): every living enemy that threatens any square of its footprint and can still
    /// make an AoO. Shared by the PC Stand Up button and the AI stand-up step (CMB-074).
    /// </summary>
    public static List<CharacterController> GetStandUpAoOProvokers(CharacterController actor, List<CharacterController> allCharacters)
    {
        var provokers = GetEnemiesThreateningFootprint(actor, allCharacters);
        provokers.RemoveAll(enemy => enemy == null || enemy.Stats == null || enemy.Stats.IsDead || !CanMakeAoO(enemy));
        return provokers;
    }

    /// <summary>Every enemy that threatens any square of <paramref name="actor"/>'s footprint.</summary>
    private static List<CharacterController> GetEnemiesThreateningFootprint(CharacterController actor, List<CharacterController> allCharacters)
    {
        var provokers = new List<CharacterController>();
        if (actor == null || actor.Stats == null)
            return provokers;

        provokers = GetThreateningEnemies(actor.GridPosition, actor, allCharacters);
        List<Vector2Int> footprint = actor.GetOccupiedSquares();
        if (footprint != null)
        {
            for (int i = 0; i < footprint.Count; i++)
            {
                if (footprint[i] == actor.GridPosition)
                    continue;

                List<CharacterController> squareThreats = GetThreateningEnemies(footprint[i], actor, allCharacters);
                for (int j = 0; j < squareThreats.Count; j++)
                {
                    if (!provokers.Contains(squareThreats[j]))
                        provokers.Add(squareThreats[j]);
                }
            }
        }

        return provokers;
    }

    /// <summary>
    /// Whether an initiation AoO stops the maneuver. A hit foils a Grapple or Sunder attempt
    /// (RAW differs, CMB-083). A Disarm fails only if the AoO deals damage (PHB p.155). Trip,
    /// Bull Rush and Coup de Grace go ahead unless the attacker is incapacitated.
    /// </summary>
    public static bool DoesManeuverAoODisruptAttempt(SpecialAttackType type, CombatResult aooResult)
    {
        if (aooResult == null || !aooResult.Hit)
            return false;

        switch (type)
        {
            case SpecialAttackType.Grapple:
            case SpecialAttackType.Sunder:
                return true;
            case SpecialAttackType.Disarm:
                return aooResult.TotalDamage > 0;
            default:
                return false;
        }
    }

    public static string GetManeuverAoOLabel(SpecialAttackType type)
    {
        switch (type)
        {
            case SpecialAttackType.Grapple: return "Grapple";
            case SpecialAttackType.Sunder: return "Sunder";
            case SpecialAttackType.Trip: return "Trip";
            case SpecialAttackType.Disarm: return "Disarm";
            case SpecialAttackType.BullRushAttack:
            case SpecialAttackType.BullRushCharge: return "Bull Rush";
            case SpecialAttackType.CoupDeGrace: return "Coup de Grace";
            default: return type.ToString();
        }
    }

    // ========================================================================
    // SIMPLE PATH GENERATION
    // ========================================================================

    /// <summary>
    /// Generate a simple straight-line path from start to destination.
    /// Moves one step at a time toward the destination.
    /// This is used when no complex pathfinding is needed.
    /// </summary>
    /// <param name="start">Starting position (excluded from path).</param>
    /// <param name="destination">Target position (included in path).</param>
    /// <returns>List of grid positions forming the path (excluding start).</returns>
    public static List<Vector2Int> GenerateSimplePath(Vector2Int start, Vector2Int destination)
    {
        var path = new List<Vector2Int>();
        Vector2Int current = start;

        int maxSteps = 50; // Safety limit
        int steps = 0;

        while (current != destination && steps < maxSteps)
        {
            int dx = System.Math.Sign(destination.x - current.x);
            int dy = System.Math.Sign(destination.y - current.y);
            current = new Vector2Int(current.x + dx, current.y + dy);
            path.Add(current);
            steps++;
        }

        return path;
    }
}

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
            float diceAverage = weapon.DamageCount > 0 && weapon.DamageDice > 0
                ? weapon.DamageCount * (weapon.DamageDice + 1) * 0.5f
                : 2.5f;

            float damageBonus = weapon.BonusDamage;
            if (weapon.WeaponCat == WeaponCategory.Melee || weapon.IsThrown)
                damageBonus += attacker.Stats.STRMod;

            return Mathf.Max(1f, diceAverage + damageBonus);
        }

        // Basic fallback for natural/unarmed AoOs.
        return Mathf.Max(1f, 2f + attacker.Stats.STRMod);
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
    /// D&D 3.5 rule: Moving OUT of a threatened square provokes an AoO from the
    /// threatening character, but only if the character is also threatening the square
    /// you move INTO (or you leave their threatened area entirely).
    ///
    /// Simplified: leaving a square threatened by an enemy provokes AoO from that enemy.
    /// Each enemy can only provoke once per movement (they use their AoO on the first opportunity).
    /// </summary>
    /// <param name="mover">The character moving.</param>
    /// <param name="path">The movement path (list of squares, NOT including the starting square).</param>
    /// <param name="allCharacters">All characters in combat.</param>
    /// <returns>List of AoOs that would be provoked.</returns>
    public static List<AoOThreatInfo> AnalyzePathForAoOs(CharacterController mover, List<Vector2Int> path, List<CharacterController> allCharacters, bool suppressFirstSquareAoO = false)
    {
        var provokedAoOs = new List<AoOThreatInfo>();
        if (path == null || path.Count == 0) return provokedAoOs;

        // Track which enemies have already been triggered (each gets at most their max AoOs)
        var enemyAoOUsedThisMovement = new Dictionary<CharacterController, int>();

        // Build threatened squares for each enemy
        var enemyThreats = new Dictionary<CharacterController, HashSet<Vector2Int>>();
        foreach (var character in allCharacters)
        {
            if (character == mover) continue;
            if (character.Stats.IsDead) continue;
            if (character.Team == mover.Team) continue;

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

                // Check if this enemy can still make AoOs
                int usedThisMovement = enemyAoOUsedThisMovement[enemy];
                int remainingGlobal = enemy.Stats.MaxAttacksOfOpportunity - enemy.Stats.AttacksOfOpportunityUsed;
                int remainingThisMovement = remainingGlobal - usedThisMovement;

                if (remainingThisMovement > 0)
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
    /// <returns>The CombatResult of the AoO, or null if the AoO couldn't be made.</returns>
    public static CombatResult ExecuteAoO(CharacterController threatener, CharacterController target)
    {
        if (threatener == null || target == null || threatener.Stats == null || target.Stats == null)
            return null;

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

        Debug.Log($"[ThreatSystem] === ATTACK OF OPPORTUNITY ===");
        Debug.Log($"[ThreatSystem] {threatener.Stats.CharacterName} makes AoO against {target.Stats.CharacterName}!");

        // AoO is a single melee attack at full BAB (no flanking, no range).
        // Prefer an actually melee-capable equipped weapon so an off-hand melee weapon
        // can still be used when the primary slot is currently ranged.
        ItemData aooWeapon = ResolveBestAoOWeapon(threatener);
        CombatResult result = threatener.Attack(target, false, 0, null, null, null, aooWeapon);

        // Mark this as an AoO in the result for logging
        result.IsAttackOfOpportunity = true;

        // Innate trip follow-up (e.g., wolf bite) is a free action and should not consume AoO economy.
        if (result.Hit
            && threatener.Stats != null
            && threatener.Stats.HasTripAttack
            && target.Stats != null
            && !target.Stats.IsDead
            && !target.HasCondition(CombatConditionType.Prone))
        {
            SpecialAttackResult tripResult = threatener.ExecuteSpecialAttack(SpecialAttackType.Trip, target);
            Debug.Log($"[ThreatSystem] Free trip follow-up from AoO by {threatener.Stats.CharacterName}: Success={tripResult.Success} | {tripResult.Log}");
        }

        // Log the result
        if (result.Hit)
        {
            string critStr = result.CritConfirmed ? " (CRITICAL HIT!)" : "";
            Debug.Log($"[ThreatSystem] AoO HIT! {threatener.Stats.CharacterName} deals {result.TotalDamage} damage to {target.Stats.CharacterName}{critStr}");
            Debug.Log($"[ThreatSystem] {target.Stats.CharacterName} HP: {result.DefenderHPBefore} → {result.DefenderHPAfter}");

            if (result.TargetKilled)
            {
                Debug.Log($"[ThreatSystem] {target.Stats.CharacterName} was SLAIN by the Attack of Opportunity!");
            }
        }
        else
        {
            Debug.Log($"[ThreatSystem] AoO MISS! {threatener.Stats.CharacterName} rolled {result.DieRoll} + mods = {result.TotalRoll} vs AC {result.TargetAC}");
        }

        Debug.Log($"[ThreatSystem] === END AoO ===");
        return result;
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

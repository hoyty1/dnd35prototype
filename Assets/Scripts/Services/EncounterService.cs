using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// D&D 3.5 Encounter Service - Combat encounter lifecycle management
// ============================================================================

/// <summary>
/// Centralized service for managing combat encounter lifecycle.
/// Handles combat initiation, victory detection, XP awards, enemy tracking,
/// and encounter state transitions. Extracted from GameManager following
/// the EconomyService pattern.
///
/// PATTERN NOTES:
/// 1. MonoBehaviour attached to the GameManager GameObject.
/// 2. Initialize() called by GameManager after Awake().
/// 3. Owns encounter-tracking state (defeated enemies, combat count, XP totals).
/// 4. GameManager delegates encounter queries here but retains spawn/UI coordination.
/// </summary>
public class EncounterService : MonoBehaviour
{
    // ==================== STATE ====================

    /// <summary>Enemies defeated in the current combat for XP calculation.</summary>
    private readonly HashSet<CharacterController> _defeatedEnemiesThisCombat = new HashSet<CharacterController>();

    /// <summary>Total number of completed combat encounters in this session.</summary>
    public int CompletedCombatCount { get; private set; }

    /// <summary>Total loot items collected across all encounters.</summary>
    public int TotalLootItemsCollected { get; private set; }

    /// <summary>Total XP from defeated enemies across all encounters.</summary>
    public int TotalEncounterXPDefeated { get; private set; }

    // Cached references set during Initialize().
    private GameManager _gameManager;
    private Func<CombatUI> _combatUIProvider;

    private CombatUI CombatUI => _combatUIProvider?.Invoke();

    // ==================== EVENTS ====================

    /// <summary>Fired when combat ends in victory. Payload = list of defeated enemies.</summary>
    public event Action<List<CharacterController>> OnCombatVictory;

    /// <summary>Fired when a new encounter begins.</summary>
    public event Action OnEncounterStarted;

    // ==================== LIFECYCLE ====================

    /// <summary>
    /// Called by GameManager after Awake to inject dependencies.
    /// </summary>
    public void Initialize(GameManager gameManager, Func<CombatUI> combatUIProvider)
    {
        _gameManager = gameManager;
        _combatUIProvider = combatUIProvider;
        CompletedCombatCount = 0;
        TotalLootItemsCollected = 0;
        TotalEncounterXPDefeated = 0;
        Debug.Log("[EncounterService] Initialized");
    }

    /// <summary>Clean up encounter state for a new combat.</summary>
    public void ResetForNewEncounter()
    {
        _defeatedEnemiesThisCombat.Clear();
        Debug.Log("[EncounterService] Reset for new encounter");
    }

    // ==================== ENEMY TRACKING ====================

    /// <summary>
    /// Register an enemy as defeated for XP tracking.
    /// Only tracks enemies that are truly defeated (HP ≤ 0, no regeneration).
    /// </summary>
    /// <param name="character">The defeated character.</param>
    /// <param name="sourceContext">Debug context for where this was called from.</param>
    /// <param name="hasRegenerationCheck">Delegate to check if a creature has regeneration/fast healing.</param>
    public void RegisterDefeatedEnemy(
        CharacterController character,
        string sourceContext,
        Func<CharacterController, bool> hasRegenerationCheck = null)
    {
        if (character == null || character.Stats == null)
            return;

        if (character.Team != CharacterTeam.Enemy)
            return;

        bool countsAsDefeated = character.Stats.CurrentHP <= 0
            && !(hasRegenerationCheck?.Invoke(character) ?? false);
        if (!countsAsDefeated)
            return;

        if (_defeatedEnemiesThisCombat.Contains(character))
            return;

        _defeatedEnemiesThisCombat.Add(character);
        string enemyName = string.IsNullOrWhiteSpace(character.Stats.CharacterName) ? "Unknown Enemy" : character.Stats.CharacterName;
        string cr = string.IsNullOrWhiteSpace(character.Stats.ChallengeRating) ? "—" : character.Stats.ChallengeRatingDisplay;
        Debug.Log($"[EncounterService] Enemy defeated: {enemyName} (CR {cr}) | source={sourceContext}");
    }

    /// <summary>
    /// Capture a snapshot of all currently defeated enemies for XP calculation.
    /// Typically called at combat victory.
    /// </summary>
    /// <param name="npcs">The list of all NPCs in the encounter.</param>
    /// <param name="sourceContext">Debug context.</param>
    /// <param name="hasRegenerationCheck">Delegate for regeneration check.</param>
    public void CaptureDefeatedEnemiesSnapshot(
        List<CharacterController> npcs,
        string sourceContext,
        Func<CharacterController, bool> hasRegenerationCheck = null)
    {
        if (npcs == null) return;

        for (int i = 0; i < npcs.Count; i++)
            RegisterDefeatedEnemy(npcs[i], sourceContext, hasRegenerationCheck);

        Debug.Log($"[EncounterService] Snapshot captured | source={sourceContext} | tracked={_defeatedEnemiesThisCombat.Count}");
    }

    /// <summary>
    /// Get all enemies defeated in the current combat.
    /// </summary>
    public List<CharacterController> GetDefeatedEnemies()
    {
        return new List<CharacterController>(_defeatedEnemiesThisCombat);
    }

    /// <summary>
    /// Whether a specific enemy has been tracked as defeated.
    /// </summary>
    public bool IsEnemyDefeated(CharacterController character)
    {
        return character != null && _defeatedEnemiesThisCombat.Contains(character);
    }

    // ==================== VICTORY & XP ====================

    /// <summary>
    /// Check if all NPCs in the encounter are dead (combat victory condition).
    /// </summary>
    /// <param name="npcs">List of all NPCs.</param>
    /// <param name="isActiveCombatant">Delegate to check if an NPC counts as an active combatant.</param>
    /// <returns>True if all enemy NPCs are dead or inactive.</returns>
    public bool AreAllEnemiesDead(
        List<CharacterController> npcs,
        Func<CharacterController, bool> isActiveCombatant)
    {
        if (npcs == null || npcs.Count == 0) return true;

        for (int i = 0; i < npcs.Count; i++)
        {
            var npc = npcs[i];
            if (npc == null || npc.Stats == null) continue;
            if (npc.Team != CharacterTeam.Enemy) continue;
            if (!(isActiveCombatant?.Invoke(npc) ?? true)) continue;
            if (!npc.Stats.IsDead) return false;
        }
        return true;
    }

    /// <summary>
    /// Calculate total XP from defeated enemies in the current combat.
    /// Uses D&amp;D 3.5e CR-to-XP tables.
    /// </summary>
    /// <returns>Total XP awarded.</returns>
    public int CalculateEncounterXP()
    {
        int totalXp = 0;
        foreach (var enemy in _defeatedEnemiesThisCombat)
        {
            if (enemy == null || enemy.Stats == null) continue;
            if (ChallengeRatingUtils.TryParse(enemy.Stats.ChallengeRating, out float cr))
                totalXp += ChallengeRatingUtils.GetXpForCr(cr);
        }
        return totalXp;
    }

    /// <summary>
    /// Award XP to party members from defeated enemies.
    /// D&amp;D 3.5e: XP is split equally among party members.
    /// </summary>
    /// <param name="partyMembers">Living party members to receive XP.</param>
    /// <returns>XP per party member.</returns>
    public int AwardXP(List<CharacterController> partyMembers)
    {
        int totalXp = CalculateEncounterXP();
        if (totalXp <= 0 || partyMembers == null || partyMembers.Count == 0)
            return 0;

        int xpPerMember = totalXp / partyMembers.Count;

        foreach (var pc in partyMembers)
        {
            if (pc != null && pc.Stats != null && !pc.Stats.IsDead)
            {
                pc.Stats.ExperiencePoints += xpPerMember;
                Debug.Log($"[EncounterService] {pc.Stats.CharacterName} awarded {xpPerMember} XP (total: {pc.Stats.ExperiencePoints})");
            }
        }

        return xpPerMember;
    }

    /// <summary>
    /// Register completion of a combat loop iteration. Updates running totals.
    /// </summary>
    /// <param name="lootedCount">Number of items looted this encounter.</param>
    public void RegisterCombatCompletion(int lootedCount)
    {
        CompletedCombatCount++;
        TotalLootItemsCollected += Mathf.Max(0, lootedCount);

        int encounterXp = CalculateEncounterXP();
        TotalEncounterXPDefeated += Mathf.Max(0, encounterXp);

        Debug.Log($"[EncounterService] Combat #{CompletedCombatCount} completed | loot={lootedCount} | xp={encounterXp} | totalXP={TotalEncounterXPDefeated}");
    }

    // ==================== ENCOUNTER DIFFICULTY ====================

    /// <summary>
    /// Calculate the Encounter Level (EL) for a set of enemies.
    /// D&amp;D 3.5e DMG p.49: EL is based on the combined CRs of all enemies.
    /// </summary>
    /// <param name="enemies">List of enemies in the encounter.</param>
    /// <returns>Estimated Encounter Level.</returns>
    public static int CalculateEncounterLevel(List<CharacterController> enemies)
    {
        if (enemies == null || enemies.Count == 0)
            return 0;

        float highestCR = 0f;
        int enemyCount = 0;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.Stats == null) continue;
            if (ChallengeRatingUtils.TryParse(enemy.Stats.ChallengeRating, out float cr))
            {
                highestCR = Mathf.Max(highestCR, cr);
                enemyCount++;
            }
        }

        if (enemyCount <= 1)
            return Mathf.RoundToInt(highestCR);

        // Simplified EL calculation: highest CR + adjustment for multiple creatures
        // Each doubling of creature count adds +2 to EL
        int adjustment = Mathf.FloorToInt(Mathf.Log(enemyCount, 2f)) * 2;
        return Mathf.RoundToInt(highestCR) + adjustment;
    }

    /// <summary>
    /// Get the Average Party Level (APL) for a set of party members.
    /// </summary>
    /// <param name="partyMembers">List of party members.</param>
    /// <returns>Average party level (rounded down).</returns>
    public static int CalculateAPL(List<CharacterController> partyMembers)
    {
        if (partyMembers == null || partyMembers.Count == 0)
            return 1;

        int totalLevels = 0;
        int count = 0;
        foreach (var pc in partyMembers)
        {
            if (pc != null && pc.Stats != null)
            {
                totalLevels += pc.Stats.Level;
                count++;
            }
        }

        return count > 0 ? totalLevels / count : 1;
    }

    // ==================== COMBAT STATS SUMMARY ====================

    /// <summary>
    /// Get a formatted summary of the session's combat statistics.
    /// </summary>
    public string GetSessionSummary()
    {
        return $"📊 Combat Loop Stats — Fights: {CompletedCombatCount} | Loot Items: {TotalLootItemsCollected} | XP Defeated: {TotalEncounterXPDefeated}";
    }
}

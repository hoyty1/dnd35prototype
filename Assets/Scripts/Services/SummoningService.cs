using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DND35.Magic;

// ============================================================================
// D&D 3.5 Summoning Service - Centralized summoned creature management
// ============================================================================

/// <summary>
/// Centralized service for managing summoned creatures in combat.
/// Handles summoning, unsummoning, duration tracking, commands, and cleanup.
/// Extracted from GameManager following the EconomyService pattern.
///
/// PATTERN NOTES:
/// 1. MonoBehaviour attached to the GameManager GameObject.
/// 2. Initialize() called by GameManager after Awake(), passing required dependencies.
/// 3. Owns summoning state (active summons, allied/enemy sets).
/// 4. GameManager delegates summoning operations here but retains UI coordination.
/// 5. Provides public API for AI, UI, and combat systems to query summon state.
/// </summary>
public class SummoningService : MonoBehaviour
{
    // ==================== INNER TYPES ====================

    /// <summary>
    /// Tracks an individual summoned creature's lifecycle.
    /// </summary>
    public class ActiveSummonInstance
    {
        public CharacterController Controller;
        public CharacterController Caster;
        public int RemainingRounds;
        public int TotalDurationRounds;
        public string SourceSpellId;
        public bool IsAlliedToPCs;
        public bool SmiteUsed;
        public SummonCommand CurrentCommand;
        public bool IsConcentrationSummon;
        public bool HasEnteredPostConcentrationDuration;
    }

    // ==================== STATE ====================

    private readonly HashSet<CharacterController> _summonedAllies = new HashSet<CharacterController>();
    private readonly HashSet<CharacterController> _summonedEnemies = new HashSet<CharacterController>();
    private readonly List<ActiveSummonInstance> _activeSummons = new List<ActiveSummonInstance>();

    // Cached references set during Initialize().
    private GameManager _gameManager;
    private Func<CombatUI> _combatUIProvider;

    // ==================== PROPERTIES ====================

    /// <summary>All currently active summoned allies (player team).</summary>
    public IReadOnlyCollection<CharacterController> SummonedAllies => _summonedAllies;

    /// <summary>All currently active summoned enemies.</summary>
    public IReadOnlyCollection<CharacterController> SummonedEnemies => _summonedEnemies;

    /// <summary>All active summon instances.</summary>
    public IReadOnlyList<ActiveSummonInstance> ActiveSummons => _activeSummons;

    /// <summary>Number of currently active summons.</summary>
    public int ActiveSummonCount => _activeSummons.Count;

    private CombatUI CombatUI => _combatUIProvider?.Invoke();

    // ==================== LIFECYCLE ====================

    /// <summary>
    /// Called by GameManager after Awake to inject dependencies.
    /// </summary>
    public void Initialize(GameManager gameManager, Func<CombatUI> combatUIProvider)
    {
        _gameManager = gameManager;
        _combatUIProvider = combatUIProvider;
        Debug.Log("[SummoningService] Initialized");
    }

    /// <summary>Clean up all summoning state (e.g. between encounters).</summary>
    public void Cleanup()
    {
        _activeSummons.Clear();
        _summonedAllies.Clear();
        _summonedEnemies.Clear();
        Debug.Log("[SummoningService] Cleaned up");
    }

    // ==================== CORE QUERIES ====================

    /// <summary>
    /// Check if a character is a summoned creature.
    /// </summary>
    public bool IsSummonedCreature(CharacterController character)
    {
        return GetActiveSummon(character) != null;
    }

    /// <summary>
    /// Get the active summon instance for a character, or null if not summoned.
    /// </summary>
    public ActiveSummonInstance GetActiveSummon(CharacterController character)
    {
        if (character == null) return null;
        for (int i = 0; i < _activeSummons.Count; i++)
        {
            var summon = _activeSummons[i];
            if (summon != null && summon.Controller == character)
                return summon;
        }
        return null;
    }

    /// <summary>
    /// Try to get the remaining and total duration for a summon.
    /// </summary>
    public bool TryGetSummonRemainingRounds(CharacterController character, out int remaining, out int total)
    {
        remaining = 0;
        total = 1;
        var summon = GetActiveSummon(character);
        if (summon == null) return false;

        remaining = Mathf.Max(0, summon.RemainingRounds);
        total = Mathf.Max(1, summon.TotalDurationRounds);
        return true;
    }

    /// <summary>
    /// Get a display-friendly name for a summoned creature including remaining duration.
    /// </summary>
    public string GetSummonDisplayName(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return "";

        if (!TryGetSummonRemainingRounds(character, out int remaining, out _))
            return character.Stats.CharacterName;

        string roundsWord = remaining == 1 ? "round" : "rounds";
        return $"{character.Stats.CharacterName} [S] ({remaining} {roundsWord})";
    }

    // ==================== COMMANDS ====================

    /// <summary>
    /// Try to get the current command assigned to a summoned creature.
    /// </summary>
    public bool TryGetSummonCommand(CharacterController character, out SummonCommand command)
    {
        command = null;
        var summon = GetActiveSummon(character);
        if (summon == null)
            return false;

        command = summon.CurrentCommand ?? SummonCommand.AttackNearest();
        return true;
    }

    /// <summary>
    /// Get the caster who summoned a given creature. Returns null if not a summon.
    /// </summary>
    public CharacterController GetSummonCaster(CharacterController summon)
    {
        ActiveSummonInstance data = GetActiveSummon(summon);
        return data?.Caster;
    }

    /// <summary>
    /// Set a command for a summoned creature.
    /// </summary>
    public void SetSummonCommand(CharacterController summon, SummonCommand command)
    {
        if (summon == null || command == null)
            return;

        var active = GetActiveSummon(summon);
        if (active == null)
            return;

        if (string.Equals(active.SourceSpellId, SpellNames.SUMMON_SWARM, StringComparison.Ordinal))
        {
            CombatUI?.ShowCombatLog("⚠ Summon Swarm cannot be controlled.");
            return;
        }

        if (!summon.IsControllable)
        {
            CombatUI?.ShowCombatLog("⚠ This summoned ally is AI-controlled and cannot receive direct commands.");
            return;
        }

        active.CurrentCommand = command;

        string summonName = GetSummonDisplayName(summon);
        CombatUI?.ShowCombatLog($"<color=#66E8FF>{summonName}: {command.Description}.</color>");
    }

    // ==================== REGISTRATION ====================

    /// <summary>
    /// Register a creature as summoned (e.g. from scenario setup or spell casting).
    /// </summary>
    public void RegisterSummonedCreature(
        CharacterController summon,
        CharacterController caster,
        int durationRounds,
        string sourceSpellId,
        bool isConcentrationSummon = false)
    {
        if (summon == null) return;

        ActiveSummonInstance existing = GetActiveSummon(summon);
        if (existing != null)
            _activeSummons.Remove(existing);

        CharacterController resolvedCaster = caster ?? summon;
        int clampedDuration = Mathf.Max(1, durationRounds);

        var newSummon = new ActiveSummonInstance
        {
            Controller = summon,
            Caster = resolvedCaster,
            RemainingRounds = clampedDuration,
            TotalDurationRounds = clampedDuration,
            SourceSpellId = string.IsNullOrWhiteSpace(sourceSpellId) ? "scenario_setup" : sourceSpellId,
            IsAlliedToPCs = summon.Team == CharacterTeam.Player,
            SmiteUsed = false,
            CurrentCommand = SummonCommand.AttackNearest(),
            IsConcentrationSummon = isConcentrationSummon,
            HasEnteredPostConcentrationDuration = false
        };

        _activeSummons.Add(newSummon);

        if (summon.Team == CharacterTeam.Player)
            _summonedAllies.Add(summon);
        else
            _summonedEnemies.Add(summon);

        Debug.Log($"[SummoningService] Registered summon: {summon.Stats?.CharacterName ?? "?"} | caster={resolvedCaster.Stats?.CharacterName ?? "?"} | duration={clampedDuration} rounds | spell={newSummon.SourceSpellId}");
    }

    // ==================== DURATION TRACKING ====================

    /// <summary>
    /// Tick all summon durations at the end of a round. Removes expired summons.
    /// Called by GameManager at end-of-round.
    /// </summary>
    /// <param name="isCasterMaintainingConcentration">
    /// Delegate to check if a caster is still maintaining concentration for a summon.
    /// </param>
    /// <returns>List of expired summon instances that need despawn effects.</returns>
    public List<ActiveSummonInstance> TickDurations(Func<CharacterController, bool> isCasterMaintainingConcentration)
    {
        var expired = new List<ActiveSummonInstance>();
        if (_activeSummons.Count == 0) return expired;

        foreach (var summon in _activeSummons)
        {
            if (summon == null || summon.Controller == null || summon.Controller.Stats == null)
            {
                expired.Add(summon);
                continue;
            }

            if (summon.Controller.Stats.IsDead)
            {
                expired.Add(summon);
                continue;
            }

            bool holdByConcentration = summon.IsConcentrationSummon
                && !summon.HasEnteredPostConcentrationDuration
                && (isCasterMaintainingConcentration?.Invoke(summon.Caster) ?? false);

            if (!holdByConcentration)
            {
                if (summon.IsConcentrationSummon && !summon.HasEnteredPostConcentrationDuration)
                {
                    summon.HasEnteredPostConcentrationDuration = true;
                    summon.RemainingRounds = Mathf.Max(1, summon.TotalDurationRounds);
                    CombatUI?.ShowCombatLog($"<color=#FFAA44>{summon.Controller.Stats.CharacterName}: concentration ended, {summon.RemainingRounds} rounds until dismissal.</color>");
                }
                else
                {
                    summon.RemainingRounds--;
                }
            }

            var visual = summon.Controller.GetComponent<SummonedCreatureVisual>();
            if (visual != null)
                visual.SetDuration(summon.RemainingRounds, summon.TotalDurationRounds);

            if (!holdByConcentration)
            {
                if (summon.RemainingRounds == 2)
                    CombatUI?.ShowCombatLog($"<color=#66E8FF>{summon.Controller.Stats.CharacterName}: 2 rounds remaining.</color>");
                else if (summon.RemainingRounds == 1)
                    CombatUI?.ShowCombatLog($"<color=#FFCC66>{summon.Controller.Stats.CharacterName}: 1 round remaining!</color>");
            }

            if (!holdByConcentration && summon.RemainingRounds <= 0)
                expired.Add(summon);
        }

        // Remove expired from tracking (caller handles despawn coroutines)
        foreach (var ex in expired)
            _activeSummons.Remove(ex);

        return expired;
    }

    // ==================== REMOVAL ====================

    /// <summary>
    /// Remove a summon from tracking (called after despawn coroutine completes).
    /// </summary>
    public void RemoveSummon(ActiveSummonInstance summon)
    {
        if (summon == null) return;
        _activeSummons.Remove(summon);

        if (summon.Controller != null)
        {
            _summonedAllies.Remove(summon.Controller);
            _summonedEnemies.Remove(summon.Controller);
        }
    }

    /// <summary>
    /// Remove a summon by its controller reference.
    /// </summary>
    public void RemoveSummonByController(CharacterController controller)
    {
        var summon = GetActiveSummon(controller);
        if (summon != null)
            RemoveSummon(summon);
    }

    /// <summary>
    /// Handle cleanup when a summoned creature dies.
    /// Returns the ActiveSummonInstance if found (for despawn coroutine), or null.
    /// </summary>
    public ActiveSummonInstance HandleSummonDeath(CharacterController maybeSummon)
    {
        if (maybeSummon == null) return null;

        ActiveSummonInstance summon = GetActiveSummon(maybeSummon);
        if (summon == null) return null;

        _activeSummons.Remove(summon);
        return summon;
    }

    // ==================== UTILITY ====================

    /// <summary>
    /// Check if a spell ID corresponds to a Summon Monster spell (I through IX).
    /// </summary>
    public static bool IsSummonMonsterSpell(SpellData spell)
    {
        if (spell == null || string.IsNullOrWhiteSpace(spell.SpellId))
            return false;
        return SummonMonsterLists.GetSummonMonsterSpellLevel(spell.SpellId) > 0;
    }

    /// <summary>
    /// Check if a spell ID corresponds to Summon Swarm.
    /// </summary>
    public static bool IsSummonSwarmSpell(SpellData spell)
    {
        return spell != null
               && string.Equals(spell.SpellId, SpellNames.SUMMON_SWARM, StringComparison.Ordinal);
    }

    /// <summary>
    /// Get filtered summon options for a given spell and caster.
    /// </summary>
    public static List<SummonMonsterOption> GetSummonOptionsForSpell(SpellData spell, CharacterController caster, int listLevel)
    {
        if (spell == null || caster == null || caster.Stats == null || listLevel <= 0)
            return new List<SummonMonsterOption>();
        return SummonMonsterLists.GetFilteredOptionsForListLevel(listLevel, caster.Stats);
    }

    /// <summary>
    /// Get all active summons for a specific caster.
    /// </summary>
    public List<ActiveSummonInstance> GetActiveSummonsForCaster(CharacterController caster)
    {
        var result = new List<ActiveSummonInstance>();
        if (caster == null) return result;

        for (int i = 0; i < _activeSummons.Count; i++)
        {
            if (_activeSummons[i]?.Caster == caster)
                result.Add(_activeSummons[i]);
        }
        return result;
    }
}

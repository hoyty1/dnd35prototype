using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Centralized runtime service for combat conditions and status effects.
///
/// Responsibilities:
/// - Track active combat conditions per character
/// - Apply/remove/query condition state
/// - Run turn/round-based expiration processing: each condition ticks at its duration anchor's initiative count
///   (PHB p.138, CMB-006; see TurnDurations), an unanchored one at the round boundary
/// - Coordinate tactical condition refreshes (for example Flanked)
/// - Cleanup condition state on death/combat end
///
/// This service intentionally interoperates with CharacterController/ConditionManager so existing
/// condition consumers keep working while GameManager delegates orchestration concerns.
/// </summary>
public class ConditionService : MonoBehaviour
{
    [Serializable]
    public class ActiveCondition
    {
        public CombatConditionType Type;
        public int RemainingRounds;
        public CharacterController Source;
        public string SourceName;
        public string SourceCategory;
        public string SourceId;
        public object Data;
        public bool ExpiresAtEndOfTurn;
        public bool ExpiresAtStartOfTurn;
    }

    [Serializable]
    public class ConditionEscapeCheckData
    {
        public string SaveType;
        public int SaveDC;
        public string SourceName;
        public string SourceSpellId;
        public bool CheckAtTurnStart = true;
        public bool RemoveHelplessOnSuccess = true;
    }

    private readonly Dictionary<CharacterController, List<ActiveCondition>> _activeConditionsByCharacter =
        new Dictionary<CharacterController, List<ActiveCondition>>();

    private Func<List<CharacterController>> _allCharactersProvider;

    public event Action<CharacterController, ActiveCondition> OnConditionExpired;

    public void Initialize(Func<List<CharacterController>> allCharactersProvider)
    {
        _allCharactersProvider = allCharactersProvider;
        SyncAllTrackedCharacters();
    }

    public void ApplyCondition(
        CharacterController target,
        CombatConditionType type,
        int rounds,
        CharacterController source = null,
        object data = null,
        bool expiresAtEndOfTurn = false,
        bool expiresAtStartOfTurn = false,
        string sourceNameOverride = null,
        string sourceCategory = "Unknown",
        string sourceId = null)
    {
        if (!IsValidCharacter(target))
            return;

        string sourceName = ResolveSourceName(target, source, sourceNameOverride);
        string normalizedSourceCategory = string.IsNullOrWhiteSpace(sourceCategory) ? "Unknown" : sourceCategory;

        target.ApplyConditionDirect(type, rounds, sourceName);
        SyncCharacter(target);

        ActiveCondition tracked = FindActiveCondition(target, type);
        if (tracked == null)
            return;

        tracked.Source = source;
        tracked.SourceName = sourceName;
        tracked.SourceCategory = normalizedSourceCategory;
        tracked.SourceId = sourceId;
        tracked.Data = data;
        tracked.ExpiresAtEndOfTurn = expiresAtEndOfTurn;
        tracked.ExpiresAtStartOfTurn = expiresAtStartOfTurn;
    }

    public bool RemoveCondition(CharacterController target, CombatConditionType type)
    {
        if (!IsValidCharacter(target))
            return false;

        bool removed = target.RemoveConditionDirect(type);
        SyncCharacter(target);
        return removed;
    }

    public int RemoveAllConditions(CharacterController target)
    {
        if (!IsValidCharacter(target))
            return 0;

        SyncCharacter(target);
        if (!_activeConditionsByCharacter.TryGetValue(target, out List<ActiveCondition> activeList) || activeList == null || activeList.Count == 0)
            return 0;

        int removed = 0;
        // Copy list so removal does not mutate while iterating.
        List<ActiveCondition> snapshot = new List<ActiveCondition>(activeList);
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (target.RemoveConditionDirect(snapshot[i].Type))
                removed++;
        }

        SyncCharacter(target);
        return removed;
    }

    /// <summary>
    /// Ticks <paramref name="target"/>'s conditions by one round, only those whose duration anchor passes
    /// <paramref name="ticksForAnchor"/> (null: all), and raises <see cref="OnConditionExpired"/> for each that ended.
    /// </summary>
    public List<ActiveCondition> UpdateConditionTimers(CharacterController target, Func<CharacterController, bool> ticksForAnchor = null)
    {
        var expired = new List<ActiveCondition>();
        if (!IsValidCharacter(target))
            return expired;

        List<ActiveCondition> previousConditions = GetActiveConditions(target);
        List<bool> consumedPreviousCondition = previousConditions != null
            ? new List<bool>(new bool[previousConditions.Count])
            : null;

        List<StatusEffect> expiredEffects = target.TickConditionsDirect(ticksForAnchor);
        if (expiredEffects == null || expiredEffects.Count == 0)
        {
            SyncCharacter(target);
            return expired;
        }

        for (int i = 0; i < expiredEffects.Count; i++)
        {
            StatusEffect effect = expiredEffects[i];
            ActiveCondition condition = ConvertToActiveCondition(effect, ResolveSourceCharacter(effect));

            int metadataIndex = FindBestMetadataMatchIndex(previousConditions, consumedPreviousCondition, effect);
            if (metadataIndex >= 0)
            {
                ActiveCondition metadata = previousConditions[metadataIndex];
                condition.Source = metadata.Source;
                condition.SourceName = !string.IsNullOrWhiteSpace(condition.SourceName) ? condition.SourceName : metadata.SourceName;
                condition.SourceCategory = metadata.SourceCategory;
                condition.SourceId = metadata.SourceId;
                condition.Data = metadata.Data;
                condition.ExpiresAtEndOfTurn = metadata.ExpiresAtEndOfTurn;
                condition.ExpiresAtStartOfTurn = metadata.ExpiresAtStartOfTurn;
                consumedPreviousCondition[metadataIndex] = true;
            }

            expired.Add(condition);
            OnConditionExpired?.Invoke(target, condition);
        }

        SyncCharacter(target);
        return expired;
    }

    /// <summary>The source name of the flat-footed state every combatant has at the start of a battle (CMB-028).</summary>
    public const string CombatStartFlatFootedSource = "Start of battle";

    /// <summary>The source category of the combat-start flat-footed state (CMB-028).</summary>
    public const string CombatStartSourceCategory = "CombatStart";

    /// <summary>
    /// The start of a battle, PC or NPC alike: every combatant is flat-footed until its first regular turn in the
    /// initiative order (PHB p.137; CMB-028). The state has no duration and ends at the start of that turn
    /// (<see cref="OnTurnStart"/>, as a start-of-turn condition), whether or not the creature can act then. Uncanny
    /// dodge keeps the Dexterity bonus (CharacterStats.DeniedDexToAcByCondition) and Combat Reflexes allows attacks of
    /// opportunity (ThreatSystem.CanMakeAoO); the creature is still flat-footed for everything else. A creature that
    /// joins later (a summoned monster) acts at once on its summoner's turn (PHB p.285) and is not flat-footed.
    /// Exceptions: a creature that is never caught flat-footed (CharacterStats.IsNeverFlatFooted, the minotaur's natural
    /// cunning, MM p.189) is skipped, and formians with the hive mind (MM p.108) are flat-footed only while every formian
    /// of their group is (<see cref="EndHiveMindFlatFootedIfAnyAware"/>; a group is the formians on one side of the
    /// battle, with a queen assumed within 50 miles).
    /// A new battle also starts a new round for attacks of opportunity: each combatant's count is reset and its cap set
    /// (ThreatSystem.ResetAoOForTurn; 1 + Dexterity with Combat Reflexes, PHB p.92), so a creature does not carry spent
    /// AoOs or a stale cap from the last battle into its flat-footed start (CMB-164).
    /// Called by GameManager.StartCombat before the first turn; a surprise round, once there is one, would apply the
    /// same state through this method.
    /// </summary>
    public void ApplyCombatStartFlatFooted(IReadOnlyList<CharacterController> combatants)
    {
        if (combatants == null)
            return;

        for (int i = 0; i < combatants.Count; i++)
        {
            CharacterController c = combatants[i];
            if (!IsValidCharacter(c) || c.Stats.IsDead)
                continue;

            ThreatSystem.ResetAoOForTurn(c);

            if (c.Stats.IsNeverFlatFooted)
                continue;

            ApplyCondition(c, CombatConditionType.FlatFooted, -1, source: null, expiresAtStartOfTurn: true,
                sourceNameOverride: CombatStartFlatFootedSource, sourceCategory: CombatStartSourceCategory);
        }

        for (int i = 0; i < combatants.Count; i++)
            EndHiveMindFlatFootedIfAnyAware(combatants[i], combatants);
    }

    /// <summary>
    /// Formian hive mind (Ex), MM p.108: if one formian in a group is not flat-footed, none of them are. When
    /// <paramref name="member"/> has the hive mind (CharacterStats.HiveMindTag) and some living hive-mind creature on its
    /// side in <paramref name="pool"/> is not flat-footed, the combat-start flat-footed state ends for every hive-mind
    /// creature on that side. Called after the combat start and at each turn start (OnTurnStart), PC or NPC alike.
    /// </summary>
    public void EndHiveMindFlatFootedIfAnyAware(CharacterController member, IReadOnlyList<CharacterController> pool)
    {
        if (!IsValidCharacter(member) || pool == null || !member.Stats.HasCreatureTag(CharacterStats.HiveMindTag))
            return;

        bool anyAware = false;
        for (int i = 0; i < pool.Count && !anyAware; i++)
        {
            CharacterController other = pool[i];
            if (IsHiveMindGroupMate(member, other) && !other.HasConditionDirect(CombatConditionType.FlatFooted))
                anyAware = true;
        }
        if (!anyAware)
            return;

        for (int i = 0; i < pool.Count; i++)
        {
            CharacterController other = pool[i];
            if (!IsHiveMindGroupMate(member, other))
                continue;
            SyncCharacter(other);
            ActiveCondition existing = FindActiveCondition(other, CombatConditionType.FlatFooted);
            if (existing == null || existing.SourceCategory != CombatStartSourceCategory)
                continue;
            if (RemoveCondition(other, CombatConditionType.FlatFooted))
                OnConditionExpired?.Invoke(other, existing);
        }
    }

    private static bool IsHiveMindGroupMate(CharacterController member, CharacterController other)
        => IsValidCharacter(other) && !other.Stats.IsDead && other.Team == member.Team
           && other.Stats.HasCreatureTag(CharacterStats.HiveMindTag);

    /// <summary>
    /// Start of <paramref name="actor"/>'s turn, PC or NPC alike: called once, from GameManager.OnTurnStarted, before
    /// the turn-skip check (CMB-006, CMB-075). Ends its start-of-turn conditions (ChargePenalty), runs the turn-start
    /// escape saves and refreshes negative levels.
    /// </summary>
    public void OnTurnStart(CharacterController actor)
    {
        if (!IsValidCharacter(actor))
            return;

        SyncCharacter(actor);
        ExpireTurnBoundaryConditions(actor, expireAtStart: true);
        // A formian whose combat-start flat-footed state just ended makes its whole group aware (MM p.108).
        EndHiveMindFlatFootedIfAnyAware(actor, GetAllCharactersInternal());
        ResolveTurnStartEscapeChecks(actor);

        actor.Stats?.RefreshNegativeLevelState();
        SpellcastingComponent spellComp = actor.Spellcasting;
        spellComp?.ApplyNegativeLevelSlotLoss();
    }

    public void OnTurnEnd(CharacterController actor)
    {
        if (!IsValidCharacter(actor))
            return;

        SyncCharacter(actor);
        ExpireTurnBoundaryConditions(actor, expireAtStart: false);
    }

    /// <summary>
    /// The round boundary (GameManager.OnNewRound): ticks the conditions whose duration anchor passes
    /// <paramref name="ticksForAnchor"/> (null: every condition), which in combat are those with no anchor in the
    /// initiative order (CMB-006).
    /// </summary>
    public void OnRoundBoundary(Func<CharacterController, bool> ticksForAnchor = null)
    {
        TickConditionDurations(ticksForAnchor);
    }

    /// <summary>
    /// <paramref name="count"/>'s initiative count was reached (TurnService.OnInitiativeCountReached), before the
    /// creature there acts: ticks every condition anchored to it, on any creature (PHB p.138, CMB-006).
    /// </summary>
    public void OnInitiativeCountReached(CharacterController count)
    {
        if (count == null)
            return;

        TickConditionDurations(anchor => TurnDurations.TicksAtCount(anchor, count));
    }

    public bool HasCondition(CharacterController target, CombatConditionType type)
    {
        if (!IsValidCharacter(target))
            return false;

        SyncCharacter(target);
        return target.HasConditionDirect(type);
    }

    public int GetConditionDuration(CharacterController target, CombatConditionType type)
    {
        if (!IsValidCharacter(target))
            return 0;

        SyncCharacter(target);

        CombatConditionType normalized = ConditionRules.Normalize(type);
        if (!_activeConditionsByCharacter.TryGetValue(target, out List<ActiveCondition> activeList) || activeList == null)
            return 0;

        for (int i = 0; i < activeList.Count; i++)
        {
            ActiveCondition condition = activeList[i];
            if (ConditionRules.Normalize(condition.Type) == normalized)
                return condition.RemainingRounds;
        }

        return 0;
    }

    public List<ActiveCondition> GetActiveConditions(CharacterController target)
    {
        if (!IsValidCharacter(target))
            return new List<ActiveCondition>();

        SyncCharacter(target);

        if (_activeConditionsByCharacter.TryGetValue(target, out List<ActiveCondition> activeList) && activeList != null)
            return new List<ActiveCondition>(activeList);

        return new List<ActiveCondition>();
    }

    public void CleanupOnDeath(CharacterController target)
    {
        if (target == null)
            return;

        RemoveAllConditions(target);
        _activeConditionsByCharacter.Remove(target);
    }

    /// <summary>
    /// Drops the synced condition record of <paramref name="target"/> without touching its conditions (they live on
    /// stats that are about to be replaced). Used when the controller is reused for a different creature, so the new
    /// creature's first sync does not inherit the old record's source and turn-boundary data (CRE-046).
    /// </summary>
    public void ForgetCharacter(CharacterController target)
    {
        if (target != null)
            _activeConditionsByCharacter.Remove(target);
    }

    public void CleanupOnCombatEnd(List<CharacterController> allCharacters)
    {
        if (allCharacters == null)
            return;

        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController c = allCharacters[i];
            if (c == null)
                continue;

            RemoveAllConditions(c);
        }

        _activeConditionsByCharacter.Clear();
    }

    public void RefreshFlankedConditions(List<CharacterController> allCharacters)
    {
        if (allCharacters == null)
            return;

        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController character = allCharacters[i];
            if (!IsValidCharacter(character))
                continue;

            bool hasFlanked = character.HasCondition(CombatConditionType.Flanked);

            if (character.Stats.IsDead)
            {
                if (hasFlanked)
                    RemoveCondition(character, CombatConditionType.Flanked);
                continue;
            }

            bool shouldBeFlanked = false;
            for (int j = 0; j < allCharacters.Count; j++)
            {
                CharacterController enemy = allCharacters[j];
                if (!IsValidCharacter(enemy) || enemy == character)
                    continue;
                if (enemy.Stats.IsDead)
                    continue;
                if (enemy.Team == character.Team)
                    continue;

                if (CombatUtils.IsAttackerFlanking(enemy, character, allCharacters, out CharacterController _))
                {
                    shouldBeFlanked = true;
                    break;
                }
            }

            if (shouldBeFlanked && !hasFlanked)
                ApplyCondition(character, CombatConditionType.Flanked, -1, source: null, sourceNameOverride: "Flanking");
            else if (!shouldBeFlanked && hasFlanked)
                RemoveCondition(character, CombatConditionType.Flanked);
        }
    }

    public CharacterController GetConditionSource(CharacterController target, CombatConditionType type)
    {
        if (!IsValidCharacter(target))
            return null;

        SyncCharacter(target);
        ActiveCondition condition = FindActiveCondition(target, type);
        return condition != null ? condition.Source : null;
    }

    private void TickConditionDurations(Func<CharacterController, bool> ticksForAnchor)
    {
        SyncAllTrackedCharacters();

        List<CharacterController> characters = GetAllCharactersInternal();
        for (int i = 0; i < characters.Count; i++)
        {
            CharacterController character = characters[i];
            if (!IsValidCharacter(character) || character.Stats.IsDead)
                continue;

            UpdateConditionTimers(character, ticksForAnchor);
        }
    }

    private void ExpireTurnBoundaryConditions(CharacterController actor, bool expireAtStart)
    {
        if (!_activeConditionsByCharacter.TryGetValue(actor, out List<ActiveCondition> conditions) || conditions == null || conditions.Count == 0)
            return;

        List<CombatConditionType> toRemove = new List<CombatConditionType>();
        for (int i = 0; i < conditions.Count; i++)
        {
            ActiveCondition condition = conditions[i];
            if (condition == null)
                continue;

            bool shouldExpire = expireAtStart
                ? condition.ExpiresAtStartOfTurn
                : condition.ExpiresAtEndOfTurn;

            if (shouldExpire)
                toRemove.Add(condition.Type);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            CombatConditionType type = toRemove[i];
            ActiveCondition existing = FindActiveCondition(actor, type);
            if (!RemoveCondition(actor, type))
                continue;

            if (existing != null)
                OnConditionExpired?.Invoke(actor, existing);
        }
    }

    private void ResolveTurnStartEscapeChecks(CharacterController actor)
    {
        if (!_activeConditionsByCharacter.TryGetValue(actor, out List<ActiveCondition> conditions) || conditions == null || conditions.Count == 0)
            return;

        for (int i = 0; i < conditions.Count; i++)
        {
            ActiveCondition condition = conditions[i];
            if (condition == null || condition.Data is not ConditionEscapeCheckData escapeData)
                continue;

            if (!escapeData.CheckAtTurnStart)
                continue;

            // The one save roll (SaveRollRules, D13), against the condition's source spell and its caster (the
            // protection ward against the original caster, SPL-018).
            SaveRoll save = SaveRollRules.Roll(actor.Stats, EscapeSaveType(escapeData.SaveType), escapeData.SaveDC,
                EscapeSaveContext(escapeData, condition), null);
            bool success = save.Succeeded;

            Debug.Log($"[ConditionService] {actor.Stats.CharacterName} {escapeData.SaveType} save to end {condition.Type}: d20({save.Roll}) + {save.Modifier} = {save.Total} vs DC {escapeData.SaveDC} => {SaveRollRules.Outcome(save)}");

            if (!success)
                continue;

            if (RemoveCondition(actor, condition.Type))
                OnConditionExpired?.Invoke(actor, condition);

            if (escapeData.RemoveHelplessOnSuccess && condition.Type == CombatConditionType.Paralyzed)
                RemoveCondition(actor, CombatConditionType.Helpless);

            break;
        }
    }

    /// <summary>An escape save's type by name; an unrecognized name counts as Will, as before.</summary>
    private static SavingThrowType EscapeSaveType(string saveType)
        => SaveRules.TryParse(saveType, out SavingThrowType save) ? save : SavingThrowType.Will;

    /// <summary>
    /// What an escape save is against (SaveRules, CHR-018): the condition's source spell, with the creature that imposed
    /// the condition as its caster (the protection ward against that caster's alignment, PHB p.266; SPL-018), else a
    /// labelled effect of that creature.
    /// </summary>
    private static SaveContext EscapeSaveContext(ConditionEscapeCheckData escapeData, ActiveCondition condition)
    {
        CharacterController source = condition != null ? condition.Source : null;
        if (!string.IsNullOrEmpty(escapeData.SourceSpellId))
            return SaveContext.ForSpellId(escapeData.SourceSpellId, source != null ? source.Stats : null);
        return SaveContext.Effect(escapeData.SourceName ?? (condition != null ? condition.SourceName : null), source);
    }

    private static int FindBestMetadataMatchIndex(List<ActiveCondition> previousConditions, List<bool> consumed, StatusEffect effect)
    {
        if (previousConditions == null || effect == null)
            return -1;

        CombatConditionType normalized = ConditionRules.Normalize(effect.Type);
        string sourceName = effect.SourceName ?? string.Empty;

        for (int i = 0; i < previousConditions.Count; i++)
        {
            ActiveCondition candidate = previousConditions[i];
            if (candidate == null)
                continue;
            if (consumed != null && i < consumed.Count && consumed[i])
                continue;
            if (ConditionRules.Normalize(candidate.Type) != normalized)
                continue;

            string candidateSourceName = candidate.SourceName ?? string.Empty;
            if (string.Equals(candidateSourceName, sourceName, StringComparison.Ordinal))
                return i;
        }

        for (int i = 0; i < previousConditions.Count; i++)
        {
            ActiveCondition candidate = previousConditions[i];
            if (candidate == null)
                continue;
            if (consumed != null && i < consumed.Count && consumed[i])
                continue;
            if (ConditionRules.Normalize(candidate.Type) == normalized)
                return i;
        }

        return -1;
    }

    private static string ResolveSourceName(CharacterController target, CharacterController source, string sourceNameOverride)
    {
        if (!string.IsNullOrWhiteSpace(sourceNameOverride))
            return sourceNameOverride;

        if (source != null && source.Stats != null && !string.IsNullOrWhiteSpace(source.Stats.CharacterName))
            return source.Stats.CharacterName;

        if (target != null && target.Stats != null && !string.IsNullOrWhiteSpace(target.Stats.CharacterName))
            return target.Stats.CharacterName;

        return "Unknown";
    }

    private ActiveCondition FindActiveCondition(CharacterController target, CombatConditionType type)
    {
        if (!_activeConditionsByCharacter.TryGetValue(target, out List<ActiveCondition> activeList) || activeList == null)
            return null;

        CombatConditionType normalized = ConditionRules.Normalize(type);
        for (int i = 0; i < activeList.Count; i++)
        {
            ActiveCondition candidate = activeList[i];
            if (candidate == null)
                continue;
            if (ConditionRules.Normalize(candidate.Type) == normalized)
                return candidate;
        }

        return null;
    }

    private void SyncAllTrackedCharacters()
    {
        List<CharacterController> characters = GetAllCharactersInternal();
        for (int i = 0; i < characters.Count; i++)
            SyncCharacter(characters[i]);
    }

    private void SyncCharacter(CharacterController character)
    {
        if (character == null)
            return;

        List<StatusEffect> effects = character.GetActiveConditions();
        if (effects == null)
            effects = new List<StatusEffect>();

        List<ActiveCondition> mapped = new List<ActiveCondition>(effects.Count);
        for (int i = 0; i < effects.Count; i++)
        {
            StatusEffect effect = effects[i];
            if (effect == null)
                continue;

            CharacterController source = ResolveSourceCharacter(effect);
            ActiveCondition activeCondition = ConvertToActiveCondition(effect, source);

            // Preserve turn-boundary metadata when re-syncing the same condition.
            ActiveCondition previous = FindSyncedCondition(_activeConditionsByCharacter, character, activeCondition.Type);
            if (previous != null)
            {
                activeCondition.Data = previous.Data;
                activeCondition.ExpiresAtEndOfTurn = previous.ExpiresAtEndOfTurn;
                activeCondition.ExpiresAtStartOfTurn = previous.ExpiresAtStartOfTurn;
                activeCondition.SourceCategory = previous.SourceCategory;
                activeCondition.SourceId = previous.SourceId;

                if (activeCondition.Source == null)
                    activeCondition.Source = previous.Source;

                if (string.IsNullOrWhiteSpace(activeCondition.SourceName))
                    activeCondition.SourceName = previous.SourceName;
            }

            mapped.Add(activeCondition);
        }

        _activeConditionsByCharacter[character] = mapped;
    }

    private static ActiveCondition FindSyncedCondition(
        Dictionary<CharacterController, List<ActiveCondition>> store,
        CharacterController character,
        CombatConditionType type)
    {
        if (store == null || character == null)
            return null;

        if (!store.TryGetValue(character, out List<ActiveCondition> existing) || existing == null)
            return null;

        CombatConditionType normalized = ConditionRules.Normalize(type);
        for (int i = 0; i < existing.Count; i++)
        {
            ActiveCondition current = existing[i];
            if (current == null)
                continue;
            if (ConditionRules.Normalize(current.Type) == normalized)
                return current;
        }

        return null;
    }

    private ActiveCondition ConvertToActiveCondition(StatusEffect effect, CharacterController source)
    {
        return new ActiveCondition
        {
            Type = effect != null ? ConditionRules.Normalize(effect.Type) : CombatConditionType.None,
            RemainingRounds = effect != null ? effect.RemainingRounds : 0,
            Source = source,
            SourceName = effect != null ? effect.SourceName : null,
            SourceCategory = "Unknown",
            SourceId = null,
            Data = null,
            ExpiresAtEndOfTurn = false,
            ExpiresAtStartOfTurn = false
        };
    }

    private CharacterController ResolveSourceCharacter(StatusEffect effect)
    {
        if (effect == null || string.IsNullOrWhiteSpace(effect.SourceName))
            return null;

        List<CharacterController> allCharacters = GetAllCharactersInternal();
        for (int i = 0; i < allCharacters.Count; i++)
        {
            CharacterController candidate = allCharacters[i];
            if (!IsValidCharacter(candidate))
                continue;

            if (candidate.Stats != null && string.Equals(candidate.Stats.CharacterName, effect.SourceName, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private List<CharacterController> GetAllCharactersInternal()
    {
        if (_allCharactersProvider == null)
            return new List<CharacterController>();

        List<CharacterController> all = _allCharactersProvider.Invoke();
        return all ?? new List<CharacterController>();
    }

    private static bool IsValidCharacter(CharacterController character)
    {
        return character != null && character.Stats != null;
    }
}

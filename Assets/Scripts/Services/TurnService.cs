using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Owns initiative order and turn progression for combat.
/// GameManager delegates turn flow state to this service.
/// </summary>
public class TurnService : MonoBehaviour
{
    [Serializable]
    public class InitiativeEntry
    {
        public CharacterController Character;
        public int Roll;
        public int Modifier;
        public int Total;
        public bool IsPC;

        public InitiativeEntry(CharacterController character, bool isPC)
        {
            Character = character;
            IsPC = isPC;
            Modifier = character != null && character.Stats != null ? character.Stats.InitiativeModifier : 0;
            Roll = DiceRoller.D20();
            Total = Roll + Modifier;
        }

        public override string ToString()
        {
            if (Character == null || Character.Stats == null)
                return "<null>";

            string modStr = Modifier >= 0 ? $"+{Modifier}" : Modifier.ToString();
            return $"{Character.Stats.CharacterName}: {Total} (d20={Roll} {modStr})";
        }
    }

    private readonly List<InitiativeEntry> _initiativeOrder = new List<InitiativeEntry>();
    private int _currentInitiativeIndex;
    private int _currentRound;
    private CharacterController _currentCharacter;
    private bool _combatActive;
    // The last initiative count reached (CMB-006): its entry and round, so a count is reported once per round.
    private InitiativeEntry _lastCountReachedEntry;
    private int _lastCountReachedRound = -1;
    // The creature whose turn is running although it left the order during that turn (RemoveFromInitiative; CRE-312).
    private CharacterController _vacatedTurnOwner;

    public IReadOnlyList<InitiativeEntry> InitiativeOrder => _initiativeOrder;

    private readonly List<CharacterController> _orderAtLastCombatEnd = new List<CharacterController>();

    /// <summary>
    /// The creatures of the initiative order as it stood when <see cref="EndCombat"/> last ran, in order: the order is
    /// cleared before <see cref="OnCombatEnded"/> is raised, and the combat-end poison secondaries resolve in it (CMB-007).
    /// </summary>
    public IReadOnlyList<CharacterController> OrderAtLastCombatEnd => _orderAtLastCombatEnd;
    public int CurrentInitiativeIndex => _currentInitiativeIndex;
    public int CurrentRound => _currentRound;
    public CharacterController CurrentCharacter => _currentCharacter;

    /// <summary>
    /// The creature whose turn is still running after it left the initiative order during that turn (a summon slain by
    /// an AoO on its own move, by a reaction to its own attack, or in its end-of-turn check; grouped AI turns step 4,
    /// CRE-312), until the advance that ends the turn; null otherwise. <see cref="CurrentCharacter"/> is null meanwhile,
    /// and <see cref="CurrentInitiativeIndex"/> already holds the slot of the creature that acts next.
    /// </summary>
    public CharacterController VacatedTurnOwner => _vacatedTurnOwner;

    /// <summary>
    /// Whether the turn running now is <paramref name="character"/>'s: it is the current creature, or it left the order
    /// during its own turn and the turn has not advanced yet (<see cref="VacatedTurnOwner"/>). Reference checks only, so a
    /// creature destroyed at the end of the frame it left play in gets the same answer in that frame and later.
    /// </summary>
    public bool IsCurrentTurnOf(CharacterController character)
        => !ReferenceEquals(character, null)
           && (ReferenceEquals(_currentCharacter, character) || ReferenceEquals(_vacatedTurnOwner, character));
    public bool IsCombatActive => _combatActive;

    public event Action<CharacterController> OnTurnStarted;
    public event Action<int> OnNewRound;
    /// <summary>
    /// An initiative count was reached, before its creature's turn starts (<see cref="OnTurnStarted"/>), once per
    /// count per round, also when its creature is dead and its turn is skipped. Durations anchored to that creature
    /// tick here (PHB p.138, CMB-006; <see cref="TurnDurations"/>).
    /// </summary>
    public event Action<CharacterController> OnInitiativeCountReached;
    /// <summary>
    /// A creature left the initiative order (<see cref="RemoveFromInitiative"/>): (removed, heir). The durations it
    /// anchored should tick at the heir's count from now on, null meaning the round boundary (CMB-006,
    /// <see cref="TurnDurations.Reanchor"/>). The heir is the neighbouring count that keeps one tick per round: the next
    /// count if the removed one was not reached this round (null when it was the last), the next one if it was reached
    /// earlier this round, and the previous one if it is the current count (null when it was the first, which then
    /// loses one extra round).
    /// </summary>
    public event Action<CharacterController, CharacterController> OnInitiativeCountRemoved;
    public event Action OnCombatEnded;

    public void StartCombat(
        List<CharacterController> pcs,
        List<CharacterController> npcs,
        Func<CharacterController, bool> isPCPredicate,
        IReadOnlyList<CharacterController> forcedFirstCharacters = null)
    {
        _initiativeOrder.Clear();
        _orderAtLastCombatEnd.Clear(); // only ever the order of the fight that just ended (CMB-007)
        _currentInitiativeIndex = 0;
        _currentRound = 0;
        _currentCharacter = null;
        _vacatedTurnOwner = null;
        _combatActive = true;
        ResetCountReached();

        if (pcs != null)
        {
            for (int i = 0; i < pcs.Count; i++)
            {
                CharacterController pc = pcs[i];
                if (!IsEligibleCombatant(pc))
                    continue;

                bool isPc = isPCPredicate == null || isPCPredicate(pc);
                InitiativeEntry entry = new InitiativeEntry(pc, isPc);
                _initiativeOrder.Add(entry);
                Debug.Log($"[TurnService][Initiative] {entry}");
            }
        }

        if (npcs != null)
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                CharacterController npc = npcs[i];
                if (!IsEligibleCombatant(npc))
                    continue;

                bool isPc = isPCPredicate != null && isPCPredicate(npc);
                InitiativeEntry entry = new InitiativeEntry(npc, isPc);
                _initiativeOrder.Add(entry);
                Debug.Log($"[TurnService][Initiative] {entry}");
            }
        }

        SortInitiativeOrder();
        ApplyForcedFirstOrdering(forcedFirstCharacters);

        if (_initiativeOrder.Count == 0)
        {
            EndCombat();
            return;
        }

        _currentRound = 1;
        // Effects created while the round boundary is processed have no initiative count (CMB-006).
        TurnDurations.ClearCurrentAnchor();
        OnNewRound?.Invoke(_currentRound);

        Debug.Log("[TurnService][Initiative] ===== INITIATIVE ORDER =====");
        for (int i = 0; i < _initiativeOrder.Count; i++)
        {
            InitiativeEntry e = _initiativeOrder[i];
            string tag = GetInitiativeRoleLabel(e.Character);
            Debug.Log($"[TurnService][Initiative] #{i + 1}: [{tag}] {e}");
        }
        Debug.Log("[TurnService][Initiative] =============================");

        StartTurnAtCurrentIndex();
    }

    public void SortInitiativeOrder()
    {
        _initiativeOrder.Sort((a, b) =>
        {
            int cmp = b.Total.CompareTo(a.Total);
            if (cmp != 0) return cmp;

            cmp = b.Modifier.CompareTo(a.Modifier);
            if (cmp != 0) return cmp;

            return UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1;
        });
    }

    private void ApplyForcedFirstOrdering(IReadOnlyList<CharacterController> forcedFirstCharacters)
    {
        if (forcedFirstCharacters == null || forcedFirstCharacters.Count == 0 || _initiativeOrder.Count == 0)
            return;

        List<InitiativeEntry> forcedEntries = new List<InitiativeEntry>();

        for (int i = 0; i < forcedFirstCharacters.Count; i++)
        {
            CharacterController forcedCharacter = forcedFirstCharacters[i];
            if (!IsEligibleCombatant(forcedCharacter))
                continue;

            int index = _initiativeOrder.FindIndex(e => e.Character == forcedCharacter);
            if (index < 0)
                continue;

            InitiativeEntry entry = _initiativeOrder[index];
            _initiativeOrder.RemoveAt(index);
            forcedEntries.Add(entry);
        }

        if (forcedEntries.Count == 0)
            return;

        _initiativeOrder.InsertRange(0, forcedEntries);
        _currentInitiativeIndex = 0;

        List<string> forcedNames = new List<string>();
        for (int i = 0; i < forcedEntries.Count; i++)
        {
            string name = forcedEntries[i].Character != null && forcedEntries[i].Character.Stats != null
                ? forcedEntries[i].Character.Stats.CharacterName
                : "<null>";
            forcedNames.Add(name);
        }

        Debug.Log($"[TurnService][Initiative] Forced first order applied: {string.Join(", ", forcedNames)}");
    }

    public void StartTurnAtCurrentIndex()
    {
        Debug.Log($"[TurnService][Flow] StartTurnAtCurrentIndex ENTER | combatActive={_combatActive} | initiativeCount={_initiativeOrder.Count} | index={_currentInitiativeIndex} | round={_currentRound}");

        if (!_combatActive)
        {
            Debug.Log("[TurnService][Flow] EARLY RETURN | reason=combat inactive");
            return;
        }

        if (_initiativeOrder.Count == 0)
        {
            Debug.Log("[TurnService][Flow] No initiative entries remain; ending combat.");
            EndCombat();
            return;
        }

        int attempts = 0;
        while (attempts < _initiativeOrder.Count)
        {
            if (_currentInitiativeIndex >= _initiativeOrder.Count)
            {
                _currentInitiativeIndex = 0;
                _currentRound++;
                TurnDurations.ClearCurrentAnchor();
                OnNewRound?.Invoke(_currentRound);
                // A round-boundary removal (an expired summon, synchronous since CRE-312) can empty the order (and so end
                // the fight, RemoveFromInitiative): there is no turn left to start.
                if (!_combatActive || _initiativeOrder.Count == 0)
                    return;
            }

            InitiativeEntry entry = _initiativeOrder[_currentInitiativeIndex];
            // The entries after this count, in order: if a tick at this count (a duration, a poison secondary,
            // concentration) takes this count's creature out of play, synchronously since CRE-312, the first of them still
            // in the order is the creature that now comes next, and nobody is skipped.
            List<InitiativeEntry> following = _initiativeOrder.GetRange(_currentInitiativeIndex + 1, _initiativeOrder.Count - _currentInitiativeIndex - 1);
            ReachInitiativeCount(entry);
            if (!_combatActive || _initiativeOrder.Count == 0)
                return;
            int at = _initiativeOrder.IndexOf(entry);
            if (at < 0)
            {
                _currentInitiativeIndex = _initiativeOrder.Count; // none of the following is left: the round wraps
                foreach (InitiativeEntry next in following)
                {
                    int nextAt = _initiativeOrder.IndexOf(next);
                    if (nextAt >= 0)
                    {
                        _currentInitiativeIndex = nextAt;
                        break;
                    }
                }
                continue; // the removal shortened the order, so the loop stays bounded
            }
            _currentInitiativeIndex = at; // an earlier entry removed at this count shifted it

            if (IsEligibleCombatant(entry.Character))
            {
                _currentCharacter = entry.Character;
                _vacatedTurnOwner = null;
                OnTurnStarted?.Invoke(_currentCharacter);
                return;
            }

            _currentInitiativeIndex++;
            attempts++;
        }

        EndCombat();
    }

    /// <summary>
    /// Makes <paramref name="entry"/>'s count the current one for new durations and raises
    /// <see cref="OnInitiativeCountReached"/> the first time this round (CMB-006).
    /// </summary>
    private void ReachInitiativeCount(InitiativeEntry entry)
    {
        if (entry == null)
            return;

        TurnDurations.SetCurrentAnchor(entry.Character);
        if (ReferenceEquals(entry, _lastCountReachedEntry) && _lastCountReachedRound == _currentRound)
            return;

        _lastCountReachedEntry = entry;
        _lastCountReachedRound = _currentRound;
        if (entry.Character != null)
            OnInitiativeCountReached?.Invoke(entry.Character);
    }

    private void ResetCountReached()
    {
        _lastCountReachedEntry = null;
        _lastCountReachedRound = -1;
        TurnDurations.ClearCurrentAnchor();
    }

    public void EndTurn()
    {
        if (!_combatActive)
            return;

        AdvanceToNextTurn();
    }

    public void AdvanceToNextTurn()
    {
        if (!_combatActive)
            return;

        // A creature that left the order during its own turn already handed its slot to the next creature (CRE-312).
        if (!ReferenceEquals(_vacatedTurnOwner, null))
            _vacatedTurnOwner = null;
        else
            _currentInitiativeIndex++;
        StartTurnAtCurrentIndex();
    }

    public void EndCombat()
    {
        Debug.Log($"[TurnService][Flow] EndCombat ENTER | combatActive={_combatActive} | initiativeCount={_initiativeOrder.Count} | currentRound={_currentRound}");
        _combatActive = false;
        _currentCharacter = null;
        _vacatedTurnOwner = null;
        _orderAtLastCombatEnd.Clear();
        foreach (InitiativeEntry entry in _initiativeOrder)
            if (entry != null && entry.Character != null)
                _orderAtLastCombatEnd.Add(entry.Character);
        _initiativeOrder.Clear();
        _currentInitiativeIndex = 0;
        ResetCountReached();
        Debug.Log("[TurnService][Flow] EndCombat invoking OnCombatEnded event.");
        OnCombatEnded?.Invoke();
        Debug.Log("[TurnService][Flow] EndCombat EXIT");
    }

    /// <summary>
    /// Force-clear initiative/combat state without emitting OnCombatEnded.
    /// Used by encounter-loop reset paths after loot/rest to avoid stale turn state.
    /// </summary>
    public void ForceResetWithoutCallbacks(string context)
    {
        string safeContext = string.IsNullOrWhiteSpace(context) ? "unspecified" : context;
        Debug.Log($"[TurnService][Flow] ForceResetWithoutCallbacks | context={safeContext} | active={_combatActive} | initiativeCount={_initiativeOrder.Count} | round={_currentRound}");

        _combatActive = false;
        _currentCharacter = null;
        _vacatedTurnOwner = null;
        _initiativeOrder.Clear();
        _orderAtLastCombatEnd.Clear(); // a reset fight leaves no order for the combat-end poison secondaries (CMB-007)
        _currentInitiativeIndex = 0;
        _currentRound = 0;
        ResetCountReached();
    }

    public void AddToInitiative(CharacterController combatant, bool isPC, CharacterController insertAfter = null)
    {
        if (combatant == null || combatant.Stats == null)
            return;

        InitiativeEntry entry = new InitiativeEntry(combatant, isPC);
        int insertIdx = ResolveInsertIndex(entry, insertAfter);

        _initiativeOrder.Insert(insertIdx, entry);
        // During a vacated turn (CRE-312) the index already holds the next creature, which has not acted: one inserted at
        // that index comes before it and acts next, so the index stays.
        if (insertIdx < _currentInitiativeIndex
            || (insertIdx == _currentInitiativeIndex && ReferenceEquals(_vacatedTurnOwner, null)))
            _currentInitiativeIndex++;

        Debug.Log($"[TurnService][Initiative] Added {combatant.Stats.CharacterName} at position {insertIdx + 1}: {entry.Total}");
    }

    public bool RemoveFromInitiative(CharacterController combatant)
    {
        if (combatant == null)
            return false;

        int index = _initiativeOrder.FindIndex(e => e.Character == combatant);
        if (index < 0)
            return false;

        CharacterController durationHeir = GetDurationHeir(index);
        // The creature whose turn it is leaves during that turn (a summon slain by an AoO on its own move, by a reaction
        // to its own attack, or dying in its end-of-turn check; grouped AI turns step 4, CRE-312): the next creature now
        // fills its slot, so the index stays on it (on Count at the last slot, so the round wraps) and the turn is marked
        // vacated (VacatedTurnOwner): the AdvanceToNextTurn that ends the turn starts that creature without stepping past
        // it. Further removals in the same turn keep the rules below, which hold because the index now points at a
        // creature that has not acted yet (a removal of that one lets the next slide in). A removal at the round boundary
        // (TickSummonDurations, index already 0 and the turn's creature another) is neither case.
        bool vacated = !ReferenceEquals(_vacatedTurnOwner, null);
        bool removingCurrentTurn = !vacated && index == _currentInitiativeIndex && ReferenceEquals(_currentCharacter, combatant);
        _initiativeOrder.RemoveAt(index);
        OnInitiativeCountRemoved?.Invoke(combatant, durationHeir);

        if (removingCurrentTurn)
            _vacatedTurnOwner = combatant;
        else if (index < _currentInitiativeIndex)
            _currentInitiativeIndex = Mathf.Max(0, _currentInitiativeIndex - 1);
        else if (!vacated && _currentInitiativeIndex >= _initiativeOrder.Count)
            _currentInitiativeIndex = 0;

        if (_currentCharacter == combatant)
            _currentCharacter = null;

        if (_initiativeOrder.Count == 0 && _combatActive)
            EndCombat();

        return true;
    }

    /// <summary>
    /// The creature whose count takes over the durations anchored to the entry at <paramref name="index"/> when it
    /// leaves the order (CMB-006; see <see cref="OnInitiativeCountRemoved"/>).
    /// </summary>
    private CharacterController GetDurationHeir(int index)
    {
        InitiativeEntry removed = _initiativeOrder[index];
        bool isCurrentCount = index == _currentInitiativeIndex
            && ReferenceEquals(removed, _lastCountReachedEntry) && _lastCountReachedRound == _currentRound;

        if (isCurrentCount)
            return index > 0 ? _initiativeOrder[index - 1].Character : null;

        // Reached earlier this round (the next count was reached too) or not yet reached (nor is the next count): the
        // next count ticks once more before the removed count would have come up again.
        return index + 1 < _initiativeOrder.Count ? _initiativeOrder[index + 1].Character : null;
    }

    public InitiativeEntry GetInitiative(CharacterController character)
    {
        if (character == null)
            return null;

        return _initiativeOrder.Find(e => e.Character == character);
    }

    public CharacterController GetNextCharacter()
    {
        if (_initiativeOrder.Count == 0)
            return null;

        // After a vacated turn (CRE-312) the index already holds the creature that acts next.
        int index = ReferenceEquals(_vacatedTurnOwner, null) ? _currentInitiativeIndex + 1 : _currentInitiativeIndex;
        if (index >= _initiativeOrder.Count)
            index = 0;

        int attempts = 0;
        while (attempts < _initiativeOrder.Count)
        {
            InitiativeEntry entry = _initiativeOrder[index];
            if (IsEligibleCombatant(entry.Character))
                return entry.Character;

            index++;
            if (index >= _initiativeOrder.Count)
                index = 0;
            attempts++;
        }

        return null;
    }

    public bool IsCharactersTurn(CharacterController character)
    {
        return character != null && character == _currentCharacter;
    }

    public bool HasInitiativeEntries()
    {
        bool hasEntries = _initiativeOrder.Count > 0;
        Debug.Log($"[TurnService][Flow] HasInitiativeEntries -> {hasEntries} (count={_initiativeOrder.Count})");
        return hasEntries;
    }

    public string GetInitiativeOrderString()
    {
        if (_initiativeOrder.Count == 0)
            return "No combatants";

        List<string> parts = new List<string>();
        for (int i = 0; i < _initiativeOrder.Count; i++)
        {
            InitiativeEntry e = _initiativeOrder[i];
            string name = e.Character != null && e.Character.Stats != null ? e.Character.Stats.CharacterName : "<null>";
            string tag = GetInitiativeIcon(e.Character);
            parts.Add($"{tag}{name}({e.Total})");
        }

        return string.Join(" → ", parts);
    }

    public string GetInitiativeDisplayString()
    {
        if (_initiativeOrder.Count == 0)
            return string.Empty;

        List<string> parts = new List<string>();
        for (int i = 0; i < _initiativeOrder.Count; i++)
        {
            InitiativeEntry e = _initiativeOrder[i];
            if (e.Character == null || e.Character.Stats == null || e.Character.Stats.IsDead)
                continue;

            string name = $"{GetInitiativeIcon(e.Character)}{e.Character.Stats.CharacterName}";
            // No marker while the turn's creature has left the order (CRE-312): the index already holds the next one.
            if (i == _currentInitiativeIndex && ReferenceEquals(_vacatedTurnOwner, null))
                parts.Add($"<color=#FFDD44><b>▶{name}</b></color>");
            else
                parts.Add($"<color={GetInitiativeColorHex(e.Character)}>{name}</color>");
        }

        return "Init: " + string.Join(" → ", parts);
    }

    private static string GetInitiativeRoleLabel(CharacterController character)
    {
        if (character == null)
            return "Unknown";

        if (character.Team == CharacterTeam.Player)
            return character.IsControllable ? "Player-Controlled Ally" : "AI Ally";

        if (character.Team == CharacterTeam.Enemy)
            return character.IsControllable ? "Player-Controlled Enemy" : "Enemy AI";

        return character.IsControllable ? "Player-Controlled Neutral" : "Neutral";
    }

    private static string GetInitiativeIcon(CharacterController character)
    {
        if (character == null)
            return "•";

        if (character.Team == CharacterTeam.Player)
            return character.IsControllable ? "🟦" : "🟩";

        if (character.Team == CharacterTeam.Enemy)
            return character.IsControllable ? "🟧" : "🟥";

        return "⬜";
    }

    private static string GetInitiativeColorHex(CharacterController character)
    {
        if (character == null)
            return "#CCCCCC";

        if (character.Team == CharacterTeam.Player)
            return character.IsControllable ? "#66B3FF" : "#77EE99";

        if (character.Team == CharacterTeam.Enemy)
            return character.IsControllable ? "#FFB366" : "#FF6666";

        return "#CCCCCC";
    }

    private static bool IsEligibleCombatant(CharacterController combatant)
    {
        return combatant != null && combatant.Stats != null && !combatant.Stats.IsDead;
    }

    private int ResolveInsertIndex(InitiativeEntry entry, CharacterController insertAfter)
    {
        int insertIdx = -1;

        if (insertAfter != null)
        {
            int anchorIdx = _initiativeOrder.FindIndex(e => e.Character == insertAfter);
            if (anchorIdx >= 0)
            {
                InitiativeEntry anchor = _initiativeOrder[anchorIdx];
                entry.Roll = anchor.Roll;
                entry.Modifier = anchor.Modifier;
                entry.Total = anchor.Total;

                insertIdx = anchorIdx + 1;
                while (insertIdx < _initiativeOrder.Count)
                {
                    InitiativeEntry e = _initiativeOrder[insertIdx];
                    if (e.Total != entry.Total || e.Character == insertAfter)
                        break;

                    insertIdx++;
                }
            }
        }

        if (insertIdx >= 0)
            return insertIdx;

        insertIdx = 0;
        while (insertIdx < _initiativeOrder.Count)
        {
            InitiativeEntry current = _initiativeOrder[insertIdx];
            if (entry.Total > current.Total)
                break;
            if (entry.Total == current.Total && entry.Modifier > current.Modifier)
                break;
            insertIdx++;
        }

        return insertIdx;
    }
}

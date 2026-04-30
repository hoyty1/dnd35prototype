using System;
using System.Collections.Generic;
using UnityEngine;

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
            Roll = UnityEngine.Random.Range(1, 21);
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

    public IReadOnlyList<InitiativeEntry> InitiativeOrder => _initiativeOrder;
    public int CurrentInitiativeIndex => _currentInitiativeIndex;
    public int CurrentRound => _currentRound;
    public CharacterController CurrentCharacter => _currentCharacter;
    public bool IsCombatActive => _combatActive;

    public event Action<CharacterController> OnTurnStarted;
    public event Action<int> OnNewRound;
    public event Action OnCombatEnded;

    public void StartCombat(
        List<CharacterController> pcs,
        List<CharacterController> npcs,
        Func<CharacterController, bool> isPCPredicate,
        IReadOnlyList<CharacterController> forcedFirstCharacters = null)
    {
        _initiativeOrder.Clear();
        _currentInitiativeIndex = 0;
        _currentRound = 0;
        _currentCharacter = null;
        _combatActive = true;

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
                OnNewRound?.Invoke(_currentRound);
            }

            InitiativeEntry entry = _initiativeOrder[_currentInitiativeIndex];
            if (IsEligibleCombatant(entry.Character))
            {
                _currentCharacter = entry.Character;
                OnTurnStarted?.Invoke(_currentCharacter);
                return;
            }

            _currentInitiativeIndex++;
            attempts++;
        }

        EndCombat();
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

        _currentInitiativeIndex++;
        StartTurnAtCurrentIndex();
    }

    public void EndCombat()
    {
        Debug.Log($"[TurnService][Flow] EndCombat ENTER | combatActive={_combatActive} | initiativeCount={_initiativeOrder.Count} | currentRound={_currentRound}");
        _combatActive = false;
        _currentCharacter = null;
        _initiativeOrder.Clear();
        _currentInitiativeIndex = 0;
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
        _initiativeOrder.Clear();
        _currentInitiativeIndex = 0;
        _currentRound = 0;
    }

    public void AddToInitiative(CharacterController combatant, bool isPC, CharacterController insertAfter = null)
    {
        if (combatant == null || combatant.Stats == null)
            return;

        InitiativeEntry entry = new InitiativeEntry(combatant, isPC);
        int insertIdx = ResolveInsertIndex(entry, insertAfter);

        _initiativeOrder.Insert(insertIdx, entry);
        if (insertIdx <= _currentInitiativeIndex)
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

        _initiativeOrder.RemoveAt(index);

        if (index < _currentInitiativeIndex)
            _currentInitiativeIndex = Mathf.Max(0, _currentInitiativeIndex - 1);
        else if (_currentInitiativeIndex >= _initiativeOrder.Count)
            _currentInitiativeIndex = 0;

        if (_currentCharacter == combatant)
            _currentCharacter = null;

        if (_initiativeOrder.Count == 0 && _combatActive)
            EndCombat();

        return true;
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

        int index = _currentInitiativeIndex + 1;
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
            if (i == _currentInitiativeIndex)
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

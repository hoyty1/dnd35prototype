using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks per-NPC last-known enemy positions and Listen-based pinpointing for concealed targets.
/// D&D 3.5e behavior:
/// - If target has total concealment, attack last known square.
/// - If target moved from that square, attack misses automatically.
/// - Listen DC 20 can pinpoint current square (still concealed, concealment miss chance still applies).
/// </summary>
public class LastKnownPositionTracker : MonoBehaviour
{
    private const int MaxConsecutiveLastKnownAutoMisses = 3;

    private readonly Dictionary<CharacterController, Vector2Int> _lastKnownGridPositions = new Dictionary<CharacterController, Vector2Int>();
    private readonly HashSet<CharacterController> _pinpointedThisRound = new HashSet<CharacterController>();
    private readonly Dictionary<CharacterController, int> _consecutiveLastKnownAutoMisses = new Dictionary<CharacterController, int>();

    private CharacterController _owner;

    private void Awake()
    {
        _owner = GetComponent<CharacterController>();
    }

    public void UpdateLastKnownPosition(CharacterController character)
    {
        if (_owner == null || character == null || character.Stats == null || character.Stats.IsDead)
            return;

        _lastKnownGridPositions[character] = character.GridPosition;
        _consecutiveLastKnownAutoMisses[character] = 0;
    }

    public void UpdateVisibleCharacters(List<CharacterController> visibleCharacters)
    {
        if (visibleCharacters == null)
            return;

        for (int i = 0; i < visibleCharacters.Count; i++)
        {
            CharacterController character = visibleCharacters[i];
            if (character == null || character == _owner)
                continue;

            UpdateLastKnownPosition(character);
        }
    }

    public bool HasLastKnownPosition(CharacterController character)
    {
        return character != null && _lastKnownGridPositions.ContainsKey(character);
    }

    public Vector2Int? GetLastKnownPosition(CharacterController character)
    {
        if (character == null)
            return null;

        if (_lastKnownGridPositions.TryGetValue(character, out Vector2Int pos))
            return pos;

        return null;
    }

    public bool IsAtLastKnownPosition(CharacterController character, Vector2Int lastKnownPos)
    {
        if (character == null)
            return false;

        return character.GridPosition == lastKnownPos;
    }

    public bool IsPinpointedThisRound(CharacterController character)
    {
        return character != null && _pinpointedThisRound.Contains(character);
    }

    public int RegisterLastKnownAutoMiss(CharacterController character)
    {
        if (character == null)
            return 0;

        int current = 0;
        _consecutiveLastKnownAutoMisses.TryGetValue(character, out current);
        current++;
        _consecutiveLastKnownAutoMisses[character] = current;

        return current;
    }

    public int GetConsecutiveLastKnownAutoMisses(CharacterController character)
    {
        if (character == null)
            return 0;

        return _consecutiveLastKnownAutoMisses.TryGetValue(character, out int misses) ? misses : 0;
    }

    public bool ShouldForgetTargetAfterAutoMisses(CharacterController character)
    {
        return GetConsecutiveLastKnownAutoMisses(character) >= MaxConsecutiveLastKnownAutoMisses;
    }

    /// <summary>
    /// Forget a target's stale concealment tracking state.
    ///
    /// IMPORTANT: This is not a blacklist.
    /// If the target becomes visible again, UpdateLastKnownPosition() will
    /// re-add them immediately and reset their consecutive auto-miss counter.
    /// </summary>
    public void ForgetTarget(CharacterController character)
    {
        if (character == null)
            return;

        // Remove stale tracking data only.
        _lastKnownGridPositions.Remove(character);
        _pinpointedThisRound.Remove(character);
        _consecutiveLastKnownAutoMisses.Remove(character);
    }

    public void ClearRoundPinpointData()
    {
        _pinpointedThisRound.Clear();
    }

    /// <summary>
    /// Attempt ONE Listen check per round to pinpoint concealed characters.
    /// D&D 3.5e: this is a single perception check for the round, not one roll per target.
    /// </summary>
    public void AttemptListenChecks(List<CharacterController> concealedTargets, GameManager gameManager)
    {
        _pinpointedThisRound.Clear();

        if (_owner == null || concealedTargets == null || concealedTargets.Count == 0)
            return;

        // Build a valid concealed target list first (alive, has stats, currently not visible).
        List<CharacterController> validConcealedTargets = new List<CharacterController>();
        bool incomingIsRangedAttack = _owner.IsEquippedWeaponRanged();

        for (int i = 0; i < concealedTargets.Count; i++)
        {
            CharacterController target = concealedTargets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;

            if (_owner.CanSee(target, incomingIsRangedAttack))
                continue;

            validConcealedTargets.Add(target);
        }

        if (validConcealedTargets.Count == 0)
            return;

        // ═══════════════════════════════════════════════════════════════
        // D&D 3.5E LISTEN CHECK RULES:
        // - Listen is resolved as a single check for the situation/round.
        // - You do not roll separately per concealed creature.
        // - DC 20 is used here to pinpoint concealed creature locations.
        // - Success pinpoints all concealed targets in this set.
        // - Failure pinpoints none of them.
        // ═══════════════════════════════════════════════════════════════
        int listenDC = 20;
        int listenBonus = GetListenBonus();
        int d20Roll = Random.Range(1, 21);
        int listenTotal = d20Roll + listenBonus;
        bool success = listenTotal >= listenDC;

        string ownerName = _owner.Stats != null ? _owner.Stats.CharacterName : _owner.name;

        if (gameManager != null && gameManager.CombatUI != null)
        {
            gameManager.CombatUI.ShowCombatLog(string.Empty);
            gameManager.CombatUI.ShowCombatLog($"{ownerName} makes Listen check to pinpoint concealed enemies");

            if (validConcealedTargets.Count == 1)
            {
                CharacterController onlyTarget = validConcealedTargets[0];
                string onlyTargetName = onlyTarget.Stats != null ? onlyTarget.Stats.CharacterName : onlyTarget.name;
                gameManager.CombatUI.ShowCombatLog($"  Concealed target: {onlyTargetName}");
            }
            else
            {
                List<string> targetNames = new List<string>(validConcealedTargets.Count);
                for (int i = 0; i < validConcealedTargets.Count; i++)
                {
                    CharacterController target = validConcealedTargets[i];
                    targetNames.Add(target.Stats != null ? target.Stats.CharacterName : target.name);
                }

                gameManager.CombatUI.ShowCombatLog($"  Concealed targets: {string.Join(", ", targetNames)}");
            }

            gameManager.CombatUI.ShowCombatLog($"  Listen: d20({d20Roll}) + {listenBonus} = {listenTotal} vs DC {listenDC}");
        }

        if (success)
        {
            for (int i = 0; i < validConcealedTargets.Count; i++)
            {
                CharacterController target = validConcealedTargets[i];
                _pinpointedThisRound.Add(target);
                _consecutiveLastKnownAutoMisses[target] = 0;
            }

            if (gameManager != null && gameManager.CombatUI != null)
            {
                gameManager.CombatUI.ShowCombatLog("  ✓ SUCCESS - All targets pinpointed!");
                for (int i = 0; i < validConcealedTargets.Count; i++)
                {
                    CharacterController target = validConcealedTargets[i];
                    string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;
                    gameManager.CombatUI.ShowCombatLog($"    • {targetName} location pinpointed");
                }

                gameManager.CombatUI.ShowCombatLog("  Can attack current positions (still 50% concealment)");
            }
        }
        else if (gameManager != null && gameManager.CombatUI != null)
        {
            gameManager.CombatUI.ShowCombatLog("  ✗ FAILURE - Cannot locate any targets");
            gameManager.CombatUI.ShowCombatLog("  Must attack last known positions");
        }

        if (gameManager != null && gameManager.CombatUI != null)
            gameManager.CombatUI.ShowCombatLog(string.Empty);

        // Example combat logs:
        // BEFORE (incorrect - multiple checks):
        //   Marcus makes Listen check to locate Thoran
        //   Marcus makes Listen check to locate Valdor
        //   Marcus makes Listen check to locate Lyra
        //
        // AFTER (correct - one check):
        //   Marcus makes Listen check to pinpoint concealed enemies
        //     Concealed targets: Thoran, Valdor, Lyra
        //     Listen: d20(12) + 3 = 15 vs DC 20
        //     ✗ FAILURE - Cannot locate any targets
        //
        // AFTER (correct success):
        //   Pip makes Listen check to pinpoint concealed enemies
        //     Concealed targets: Thoran, Valdor, Lyra
        //     Listen: d20(18) + 4 = 22 vs DC 20
        //     ✓ SUCCESS - All targets pinpointed!
    }

    public void ClearAllPositions()
    {
        _lastKnownGridPositions.Clear();
        _pinpointedThisRound.Clear();
        _consecutiveLastKnownAutoMisses.Clear();
    }

    private int GetListenBonus()
    {
        if (_owner == null || _owner.Stats == null)
            return 0;

        return _owner.Stats.GetSkillBonus("Listen");
    }
}

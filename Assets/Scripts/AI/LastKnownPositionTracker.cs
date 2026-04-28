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
    private readonly Dictionary<CharacterController, Vector2Int> _lastKnownGridPositions = new Dictionary<CharacterController, Vector2Int>();
    private readonly HashSet<CharacterController> _pinpointedThisRound = new HashSet<CharacterController>();

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

    public void ClearRoundPinpointData()
    {
        _pinpointedThisRound.Clear();
    }

    public void AttemptListenChecks(List<CharacterController> concealedTargets, GameManager gameManager)
    {
        _pinpointedThisRound.Clear();

        if (_owner == null || concealedTargets == null || concealedTargets.Count == 0)
            return;

        for (int i = 0; i < concealedTargets.Count; i++)
        {
            CharacterController target = concealedTargets[i];
            if (target == null || target.Stats == null || target.Stats.IsDead)
                continue;

            bool incomingIsRangedAttack = _owner.IsEquippedWeaponRanged();
            if (_owner.CanSee(target, incomingIsRangedAttack))
                continue;

            int listenDC = 20;
            int listenBonus = GetListenBonus();
            int d20Roll = Random.Range(1, 21);
            int listenTotal = d20Roll + listenBonus;
            bool success = listenTotal >= listenDC;

            string ownerName = _owner.Stats != null ? _owner.Stats.CharacterName : _owner.name;
            string targetName = target.Stats != null ? target.Stats.CharacterName : target.name;

            if (gameManager != null && gameManager.CombatUI != null)
            {
                gameManager.CombatUI.ShowCombatLog($"{ownerName} makes Listen check to locate {targetName}");
                gameManager.CombatUI.ShowCombatLog($"  Listen: d20({d20Roll}) + {listenBonus} = {listenTotal} vs DC {listenDC}");
            }

            if (success)
            {
                _pinpointedThisRound.Add(target);
                if (gameManager != null && gameManager.CombatUI != null)
                {
                    gameManager.CombatUI.ShowCombatLog($"  ✓ SUCCESS - {targetName} pinpointed!");
                    gameManager.CombatUI.ShowCombatLog("  Can attack current position (still 50% concealment)");
                }
            }
            else if (gameManager != null && gameManager.CombatUI != null)
            {
                gameManager.CombatUI.ShowCombatLog($"  ✗ FAILURE - Cannot locate {targetName}");
                gameManager.CombatUI.ShowCombatLog("  Must attack last known position");
            }
        }
    }

    public void ClearAllPositions()
    {
        _lastKnownGridPositions.Clear();
        _pinpointedThisRound.Clear();
    }

    private int GetListenBonus()
    {
        if (_owner == null || _owner.Stats == null)
            return 0;

        return _owner.Stats.GetSkillBonus("Listen");
    }
}

using System;
using System.Collections.Generic;

/// <summary>
/// The turn-start perception of every combatant, PC or NPC (batch 12 step 8, CMB-094; docs/systems/PC_NPC_PARITY.md).
/// <c>GameManager.BeginActingTurn</c> calls <see cref="ProcessTurnStart"/> for every creature that acts, before the
/// Turn Undead check and the compulsion pass. It is the creature's one Listen check of the round: the AI's target choice
/// reads its pinpoints and rolls nothing (AI-007 fixed in the same step).
///
/// Rules applied: DMG p.295 (invisibility): any creature may use hearing to find an invisible creature, a Listen check
/// each round; PHB p.152 (total concealment): an attacker who does not know the square attacks the square it believes,
/// and misses outright when the target is not there; a pinpointed target is still attacked with the 50% miss chance.
/// The check itself (a DC 20 Listen to pinpoint every concealed enemy whose square the creature tracks, or Spot against
/// Hide with See Invisibility) is <see cref="LastKnownPositionTracker.AttemptListenChecks"/>: a pinpoint records the
/// square, becomes the last-known square and lapses when the creature leaves it (DMG p.295), and a deafened creature
/// pinpoints nothing by hearing (PHB p.307). Gaps: the DC is a flat 20 (CMB-182), and only an unseen enemy the creature
/// tracks (one it saw vanish) is checked, so one that was never seen cannot be pinpointed (CMB-183).
/// </summary>
public static class PerceptionRules
{
    /// <summary>
    /// The perception step at the start of <paramref name="actor"/>'s turn: records the squares of the enemies it sees,
    /// then makes one Listen check (or Spot against See Invisibility) to pinpoint the enemies it cannot see but tracks.
    /// With no such enemy the last turn's pinpoints are dropped, so a pinpoint holds from one turn-start check to the next
    /// while the creature stays in the pinpointed square.
    /// </summary>
    /// <param name="actor">The creature whose turn starts.</param>
    /// <param name="all">Every combatant.</param>
    /// <param name="log">Optional combat log sink for the lead line.</param>
    /// <param name="gameManager">The GameManager whose combat log the check writes to; <c>GameManager.Instance</c> when null.</param>
    public static void ProcessTurnStart(CharacterController actor, List<CharacterController> all, Action<string> log = null, GameManager gameManager = null)
    {
        if (actor == null || actor.Stats == null || actor.Stats.IsDead || all == null)
            return;

        LastKnownPositionTracker tracker = LastKnownPositionTracker.Ensure(actor);
        if (tracker == null)
            return;

        var visibleEnemies = new List<CharacterController>();
        var concealedTrackedEnemies = new List<CharacterController>();

        bool incomingIsRangedAttack = actor.IsEquippedWeaponRanged();

        for (int i = 0; i < all.Count; i++)
        {
            CharacterController enemy = all[i];
            if (enemy == null || enemy == actor || enemy.Stats == null || enemy.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(actor, enemy))
                continue;

            if (actor.CanSee(enemy, incomingIsRangedAttack))
                visibleEnemies.Add(enemy);
            else if (tracker.HasLastKnownPosition(enemy))
                concealedTrackedEnemies.Add(enemy);
        }

        tracker.UpdateVisibleCharacters(visibleEnemies);

        if (concealedTrackedEnemies.Count > 0)
        {
            log?.Invoke($"{actor.Stats.CharacterName} attempts to locate concealed targets:");
            tracker.AttemptListenChecks(concealedTrackedEnemies, gameManager != null ? gameManager : GameManager.Instance);
        }
        else
        {
            tracker.ClearRoundPinpointData();
        }
    }
}

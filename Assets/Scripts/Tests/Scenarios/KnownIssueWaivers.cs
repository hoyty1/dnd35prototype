#if UNITY_EDITOR
using System.Collections.Generic;

namespace Tests.Scenarios
{
    /// <summary>
    /// Waivers that apply to every scenario: invariant violations the harness detects that a filed issue already
    /// explains. Each run counts the hits per waiver and reports the ones that never matched (unusedWaivers), so a
    /// fixed issue shows up as an unused waiver to delete. Add an error-log regex only after seeing the real
    /// message in a run, always with its issue ID.
    /// </summary>
    public static class KnownIssueWaivers
    {
        public static readonly IReadOnlyList<Waiver> Global = new List<Waiver>
        {
            // Combat end is detected only where each damage path checks. AI-run attackers (AI-run PCs, Player-team
            // summons) that drop the last enemy through NPCPerformAttack never trigger victory; the harness then
            // halts the fight itself. Only ends where every out creature has HP <= 0, so the game's own predicate
            // agrees the side is out (seen in Play mode on the victory side; kept for both sides because a side can
            // also go out on a turn that runs no end check, for example a PC dropped by an AoO on its own turn).
            new Waiver { IssueId = "CORE-011", Invariant = ScenarioChecks.CombatEndDetected, DetailRegex = @"^(victory|defeat) side out \(cause=hp<=0\)" },

            // The game's end predicates differ from the harness policy (dead, dying or unconscious): the game counts
            // only HP <= 0 as defeated, so a side asleep or knocked out by nonlethal damage at positive HP is still
            // in (combat-end-detected with cause=unconscious>0), a disabled creature at 0 HP ends the fight early,
            // and its defeat scan ignores Player-team summons and allies.
            new Waiver { IssueId = "CORE-037", Invariant = ScenarioChecks.CombatEndDetected, DetailRegex = @"^(victory|defeat) side out \(cause=unconscious>0\)" },
            new Waiver { IssueId = "CORE-037", Invariant = ScenarioChecks.CombatEndEarly },

            // StartPCTurn and other paths can set the phase again after CombatOver (tick kills, safety-net victory).
            new Waiver { IssueId = "CORE-003", Invariant = ScenarioChecks.PhaseAfterCombatOver },
        };
    }
}
#endif

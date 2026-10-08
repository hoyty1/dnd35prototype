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
            // StartPCTurn and other paths can set the phase again after CombatOver (tick kills, safety-net victory).
            new Waiver { IssueId = "CORE-003", Invariant = ScenarioChecks.PhaseAfterCombatOver },
        };
    }
}
#endif

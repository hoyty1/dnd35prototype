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
            // Empty since batch 12 step 6: CORE-003's phase-after-CombatOver waiver went when every turn-start effect ran
            // before the one turn-start combat-end check (rules/turn-start-acid-arrow-last-enemy).
        };
    }
}
#endif

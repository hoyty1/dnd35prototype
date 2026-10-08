using System.Collections.Generic;

/// <summary>
/// Who is still in the fight, for the end of combat (CORE-011, CORE-034, CORE-037; docs/systems/PC_NPC_PARITY.md,
/// plan step 12). One predicate for both sides, read by <see cref="GameManager.EvaluateCombatEnd"/> and by the
/// scenario harness checks.
///
/// No 3.5e rule defines when a fight is over; this is the owner's definition (2026-10-07). A creature is out of the
/// fight when it is dead, dying or unconscious, whatever regeneration or fast healing it has. Unconscious covers:
/// stable (PHB p.145), knocked out by nonlethal damage (HP state Unconscious), the Unconscious condition, petrified
/// (a petrified creature is considered unconscious: PHB glossary p.311, DMG condition summary p.301) and asleep (the
/// game's Sleep, Deep Slumber and Color Spray add Unconscious with Asleep; a sleep arrow, DMG p.228, adds only
/// Asleep). A disabled creature is still in: at 0 HP (PHB p.145), or at negative HP with Diehard (PHB p.93).
/// Not detected yet: an ability score of 0 (Con 0 is dead, Int, Wis or Cha 0 is unconscious, DMG p.289-290; CHR-074).
///
/// A side is out when every active member is out and it has at least one active member, or had one earlier in this
/// combat (<see cref="SideCounts.PlayersHadMembers"/>, so a side whose last creature was trapped, destroyed or
/// removed is out). Sides are teams: the party, its allies and its summons are one side, every hostile creature the
/// other; Neutral creatures belong to neither. A creature under the other side's control still counts for its own
/// team, and a tie is a defeat (both pending the owner, CORE-038).
/// </summary>
public static class CombatEndRules
{
    /// <summary>Per-side counts of active members and of members still in the fight.</summary>
    public struct SideCounts
    {
        public int PlayersAll;
        public int PlayersIn;
        public int EnemiesAll;
        public int EnemiesIn;

        /// <summary>The party side had an active member earlier in this combat (set by GameManager).</summary>
        public bool PlayersHadMembers;

        /// <summary>The hostile side had an active member earlier in this combat (set by GameManager).</summary>
        public bool EnemiesHadMembers;

        /// <summary>The party side has, or had, members and none of them is still in the fight.</summary>
        public bool PlayersOut => (PlayersAll > 0 || PlayersHadMembers) && PlayersIn == 0;

        /// <summary>The hostile side has, or had, members and none of them is still in the fight.</summary>
        public bool EnemiesOut => (EnemiesAll > 0 || EnemiesHadMembers) && EnemiesIn == 0;
    }

    /// <summary>
    /// Dead, dying, stable or unconscious (HP state or the Unconscious condition), petrified or asleep; a missing
    /// creature is out.
    /// </summary>
    public static bool IsOutOfFight(CharacterController c)
    {
        if (c == null || c.Stats == null)
            return true;
        return c.IsDead
            || c.Stats.IsDead
            || c.IsUnconscious
            || c.HasConditionDirect(CombatConditionType.Petrified)
            || c.HasConditionDirect(CombatConditionType.Asleep);
    }

    /// <summary>A creature that takes part in the combat: present, with stats, and active in the scene.</summary>
    public static bool IsActiveCombatant(CharacterController c)
        => c != null && c.gameObject != null && c.gameObject.activeInHierarchy && c.Stats != null;

    /// <summary>Counts each side over <paramref name="combatants"/>, skipping inactive and Neutral creatures.</summary>
    public static SideCounts Count(IEnumerable<CharacterController> combatants)
    {
        var counts = new SideCounts();
        if (combatants == null)
            return counts;

        foreach (CharacterController c in combatants)
        {
            if (!IsActiveCombatant(c) || c.Team == CharacterTeam.Neutral)
                continue;

            bool inFight = !IsOutOfFight(c);
            if (c.Team == CharacterTeam.Player)
            {
                counts.PlayersAll++;
                if (inFight) counts.PlayersIn++;
            }
            else
            {
                counts.EnemiesAll++;
                if (inFight) counts.EnemiesIn++;
            }
        }

        return counts;
    }
}

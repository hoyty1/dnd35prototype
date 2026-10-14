using System.Collections.Generic;
using DND35e.Identifiers;

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
/// other; Neutral creatures belong to neither. One control predicate for both sides (owner interpretation 2026-10-11,
/// extended to the party side as a delegated RAW reading 2026-10-14, CORE-038): a creature charmed by, commanded by or
/// under the control of a creature of the opposing side (Charm Person and Charm Monster PHB p.209, Command Undead
/// p.211, Dominate Person p.224, a domain's command of elementals, PHB p.159) counts as out of its own side
/// (<see cref="IsControlledByTeam"/>); it leaves its side and does not join the other. A creature under Suggestion or
/// Mass Suggestion still counts for its side: suggestion is a compulsion that sets one course of activity, not a charm
/// or control (PHB p.173, p.285; delegated RAW reading 2026-10-14). When both sides are out at once,
/// GameManager.EvaluateCombatEnd first rolls the dying PCs' stabilization checks (PHB p.145) and the party wins if any PC
/// is stable or better when they end; the PHB p.146 recovery belongs to the post-combat recovery (CORE-015).
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

    /// <summary>
    /// Whether <paramref name="c"/> is charmed by, commanded by or under the control of a creature on
    /// <paramref name="team"/> other than itself: its Command Undead or Dominate Person controller
    /// (<see cref="CharacterController.CommandUndeadController"/>), or the source of a Charmed or Commanded condition in
    /// <paramref name="conditions"/> (its tracked source, else the charm data's caster). A Charmed condition from Mass
    /// Suggestion does not count, on either side: suggestion is a compulsion that sets one course of activity, not a charm
    /// or ongoing control (PHB p.173, p.285; delegated RAW reading 2026-10-14, CORE-038). The game models Mass Suggestion
    /// with the Charmed condition (SPL-139); a suggested creature that leaves the fight is out through what it does.
    /// </summary>
    public static bool IsControlledByTeam(CharacterController c, CharacterTeam team, IEnumerable<ConditionService.ActiveCondition> conditions)
    {
        if (c == null)
            return false;

        CharacterController commander = c.CommandUndeadController;
        if (commander != null && commander != c && commander.Team == team)
            return true;

        if (conditions == null)
            return false;

        foreach (ConditionService.ActiveCondition condition in conditions)
        {
            if (condition == null)
                continue;
            CombatConditionType type = ConditionRules.Normalize(condition.Type);
            if (type != CombatConditionType.Charmed && type != CombatConditionType.Commanded)
                continue;
            if (condition.SourceId == SpellNames.MASS_SUGGESTION)
                continue;
            CharacterController source = condition.Source;
            if (source == null && condition.Data is CharmedConditionData charm)
                source = charm.Caster;
            if (source != null && source != c && source.Team == team)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Counts each side over <paramref name="combatants"/>, skipping inactive and Neutral creatures. A hostile creature
    /// for which <paramref name="outOfHostileSide"/> returns true (charmed by or under the control of the party side,
    /// owner interpretation 2026-10-11) and a party-side creature for which <paramref name="outOfPartySide"/> returns
    /// true (charmed by or under the control of the hostile side, the same predicate applied to the party, delegated RAW
    /// reading 2026-10-14; CORE-038) are counted as members that are out.
    /// </summary>
    public static SideCounts Count(IEnumerable<CharacterController> combatants, System.Func<CharacterController, bool> outOfHostileSide = null,
        System.Func<CharacterController, bool> outOfPartySide = null)
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
                if (inFight && outOfPartySide != null && outOfPartySide(c))
                    inFight = false;
                if (inFight) counts.PlayersIn++;
            }
            else
            {
                counts.EnemiesAll++;
                if (inFight && outOfHostileSide != null && outOfHostileSide(c))
                    inFight = false;
                if (inFight) counts.EnemiesIn++;
            }
        }

        return counts;
    }
}

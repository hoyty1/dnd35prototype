using System;
using System.Collections.Generic;

/// <summary>
/// When a poison's secondary save comes (CMB-007). DMG p.296-297: every exposed creature makes the initial Fortitude save
/// (failure: the initial damage), and makes a second save against the same DC 1 minute after exposure whatever the first
/// result was (failure: the secondary damage). A minute is 10 rounds (PHB p.138), so the delay is counted in rounds of the initiative order, never in real time: an
/// open menu, a slow player or a faster AI changes nothing.
///
/// The count runs like every duration (TurnDurations, CMB-006): an <see cref="ActivePoison"/> takes the creature whose
/// initiative count is current at exposure as its anchor (the attacker on its own turn, the turn's creature for an
/// attack of opportunity), loses a round each time that count is reached (GameManager.OnInitiativeCountReached), and
/// rolls the secondary save when it reaches 0, so 10 rounds after exposure, at the same count, before that creature
/// acts. An anchor that leaves the initiative order passes its poisons to the same heir as its durations
/// (<see cref="Reanchor"/>, GameManager.OnInitiativeCountRemoved); a poison with no anchor in the order ticks at the
/// round boundary (<see cref="TickAtRoundBoundary"/>).
///
/// A secondary still pending when the fight ends resolves at combat end (<see cref="ResolvePendingAtCombatEnd"/>), in
/// initiative order, after the phase is CombatOver and before the loot window or the defeat screen: the minute passes
/// before anything else can happen to the party (delegated RAW reading, 2026-11-16, docs/systems/RULES_COVERAGE.md 4.1).
/// A dead victim makes no save, in a fight or at combat end: its poison is dropped (delegated RAW reading, 2026-11-24:
/// PHB p.145, a dead character's soul departs at once, so nothing is left to save or to take ability damage; the books do
/// not say it outright, docs/systems/RULES_COVERAGE.md 4.1).
///
/// The rules are actor-agnostic: PCs and NPCs, victims and anchors alike (docs/systems/PC_NPC_PARITY.md).
/// </summary>
public static class PoisonTiming
{
    /// <summary>DMG p.296: secondary damage 1 minute after exposure, 10 rounds (PHB p.138).</summary>
    public const int SecondaryDelayRounds = 10;

    /// <summary>
    /// The game's ingested-poison delay, 10 minutes. The DMG gives 1 minute for every type; kept as a possibly intentional
    /// deviation until the owner rules (CMB-307, owner question Q6 of docs/designs/grouped_ai_turns.md).
    /// </summary>
    public const int IngestedSecondaryDelayRounds = 100;

    /// <summary>
    /// <paramref name="count"/>'s initiative count was reached: every pending poison on <paramref name="victims"/>
    /// anchored to it loses a round, and the ones that reach 0 roll their secondary save. Returns how many resolved.
    /// </summary>
    public static int TickAtCount(IEnumerable<CharacterController> victims, CharacterController count)
    {
        if (count == null)
            return 0;
        return Tick(victims, anchor => anchor == count);
    }

    /// <summary>
    /// The round boundary: every pending poison whose anchor ticks there (<paramref name="ticksAtRoundBoundary"/>,
    /// GameManager's TicksAtRoundBoundary: no anchor, or one not in the initiative order) loses a round, and the ones
    /// that reach 0 roll their secondary save. Returns how many resolved.
    /// </summary>
    public static int TickAtRoundBoundary(IEnumerable<CharacterController> victims, Func<CharacterController, bool> ticksAtRoundBoundary)
    {
        if (ticksAtRoundBoundary == null)
            return 0;
        return Tick(victims, ticksAtRoundBoundary);
    }

    /// <summary>
    /// <paramref name="from"/> left the initiative order: every poison on <paramref name="victims"/> anchored to it is
    /// anchored to <paramref name="to"/> instead (null: the round boundary), as TurnDurations.Reanchor does for durations.
    /// Returns how many moved.
    /// </summary>
    public static int Reanchor(IEnumerable<CharacterController> victims, CharacterController from, CharacterController to)
    {
        if (victims == null || from == null)
            return 0;
        int moved = 0;
        foreach (CharacterController victim in victims)
        {
            List<ActivePoison> poisons = victim != null ? victim.ActivePoisons : null;
            if (poisons == null)
                continue;
            foreach (ActivePoison poison in poisons)
            {
                if (poison != null && !poison.SecondaryResolved && poison.DurationAnchor == from)
                {
                    poison.DurationAnchor = to;
                    moved++;
                }
            }
        }
        return moved;
    }

    /// <summary>
    /// The fight is over: every secondary still pending on <paramref name="victimsInInitiativeOrder"/> resolves now, victim
    /// by victim in the order given (the initiative order), each victim's poisons in exposure order. Returns how many
    /// resolved. A victim listed twice resolves once. <paramref name="resolvedVictims"/>, when given, receives the victim of
    /// each secondary save rolled, in order.
    /// </summary>
    public static int ResolvePendingAtCombatEnd(IEnumerable<CharacterController> victimsInInitiativeOrder, List<CharacterController> resolvedVictims = null)
    {
        if (victimsInInitiativeOrder == null)
            return 0;
        int resolved = 0;
        var seen = new HashSet<CharacterController>();
        foreach (CharacterController victim in victimsInInitiativeOrder)
        {
            if (victim == null || !seen.Add(victim))
                continue;
            List<ActivePoison> poisons = victim.ActivePoisons;
            if (poisons == null || poisons.Count == 0)
                continue;
            // A snapshot: a secondary effect cannot add a poison, but the list is changed below.
            foreach (ActivePoison poison in poisons.ToArray())
            {
                if (Resolve(victim, poison))
                {
                    resolved++;
                    resolvedVictims?.Add(victim);
                }
            }
            Prune(victim);
        }
        return resolved;
    }

    /// <summary>True when <paramref name="victim"/> has a secondary save still to come.</summary>
    public static bool HasPending(CharacterController victim)
    {
        List<ActivePoison> poisons = victim != null ? victim.ActivePoisons : null;
        if (poisons == null)
            return false;
        foreach (ActivePoison poison in poisons)
            if (IsPending(poison))
                return true;
        return false;
    }

    private static bool IsPending(ActivePoison poison)
        => poison != null && poison.PoisonData != null && !poison.SecondaryResolved;

    private static int Tick(IEnumerable<CharacterController> victims, Func<CharacterController, bool> ticksForAnchor)
    {
        if (victims == null)
            return 0;
        int resolved = 0;
        foreach (CharacterController victim in victims)
        {
            List<ActivePoison> poisons = victim != null ? victim.ActivePoisons : null;
            if (poisons == null || poisons.Count == 0)
                continue;
            foreach (ActivePoison poison in poisons.ToArray())
            {
                if (!IsPending(poison) || !ticksForAnchor(poison.DurationAnchor))
                    continue;
                poison.RoundsUntilSecondary--;
                if (poison.RoundsUntilSecondary <= 0 && Resolve(victim, poison))
                    resolved++;
            }
            Prune(victim);
        }
        return resolved;
    }

    /// <summary>Rolls <paramref name="poison"/>'s secondary save on <paramref name="victim"/>; a dead victim makes none.</summary>
    private static bool Resolve(CharacterController victim, ActivePoison poison)
    {
        if (!IsPending(poison))
            return false;
        poison.RoundsUntilSecondary = 0;
        if (victim.IsDead)
        {
            poison.SecondaryResolved = true; // the dead make no save and take no damage (delegated RAW reading, 2026-11-24)
            return false;
        }
        victim.ProcessPoisonSecondaryDamage(poison);
        if (!poison.SecondaryResolved)
            poison.SecondaryResolved = true; // ProcessPoisonSecondaryDamage returns early only without stats
        return true;
    }

    /// <summary>Drops resolved and empty entries, as the old real-time loop did.</summary>
    private static void Prune(CharacterController victim)
    {
        List<ActivePoison> poisons = victim.ActivePoisons;
        if (poisons == null)
            return;
        poisons.RemoveAll(p => p == null || p.PoisonData == null || p.SecondaryResolved);
    }
}

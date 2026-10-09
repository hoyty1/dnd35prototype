using System;
using System.Collections.Generic;

/// <summary>
/// Turn-relative durations, shared by every condition and spell effect (CMB-006).
///
/// PHB p.138 (The Combat Round): an effect that lasts a number of rounds ends just before the same initiative count it
/// began on. So each <see cref="StatusEffect"/> (a condition) and each <see cref="ActiveSpellEffect"/> (a spell effect
/// in a StatusEffectManager) records its duration anchor when it is created: the creature whose initiative count is
/// current at that moment (<see cref="CurrentAnchor"/>), which is the caster or applier when it acts on its own turn.
/// The effect then loses one round each time that creature's initiative count comes up
/// (<see cref="TurnService.OnInitiativeCountReached"/>, raised for a dead creature's count too), before the creature at
/// that count acts. A 1-round stun applied on the applier's turn therefore covers the target's next turn whatever the
/// initiative order, and a 1-round effect a creature puts on itself lasts until the start of its next turn.
///
/// A source whose rule names another creature's turn sets the anchor explicitly with <see cref="Anchor"/>: Stunning
/// Fist landed on an attack of opportunity lasts until just before the monk's next action (PHB p.101), not the
/// provoker's. An effect an area keeps up while a creature stays inside (fog concealment, Entangle) is re-applied at the
/// round boundary, so it ticks there too (<see cref="AtRoundBoundary"/>, <see cref="SustainAtRoundBoundary"/>).
///
/// An effect created outside any creature's count (out of combat, after victory or defeat, while the round boundary is
/// processed, or in a test with no TurnService) has no anchor and ticks at the round boundary (GameManager.OnNewRound),
/// as before. When a creature leaves the initiative order (a summon that ends or is slain), the effects it anchored pass
/// to a neighbouring count (<see cref="TurnService.OnInitiativeCountRemoved"/>, <see cref="Reanchor"/>), so they keep one
/// tick per round; an anchor that is still not in the order ticks at the round boundary (<see cref="TicksAtRoundBoundary"/>).
/// </summary>
public static class TurnDurations
{
    /// <summary>
    /// The creature whose initiative count is current: set by TurnService when a count is reached, null at the round
    /// boundary and outside combat. New conditions and spell effects take it as their duration anchor.
    /// </summary>
    public static CharacterController CurrentAnchor { get; private set; }

    public static void SetCurrentAnchor(CharacterController anchor) => CurrentAnchor = anchor;

    public static void ClearCurrentAnchor() => CurrentAnchor = null;

    /// <summary>
    /// Durations created inside the returned scope (<c>using (TurnDurations.Anchor(monk)) { ... }</c>) tick at
    /// <paramref name="anchor"/>'s count; with a null anchor the current count is kept. For a rule that times the
    /// effect from a creature other than the one whose count is current (Stunning Fist, PHB p.101).
    /// </summary>
    public static AnchorScope Anchor(CharacterController anchor)
        => new AnchorScope(anchor != null ? anchor : CurrentAnchor);

    /// <summary>
    /// Durations created inside the returned scope have no anchor and tick at the round boundary: for effects an area
    /// re-applies at the start of each round while a creature stays inside.
    /// </summary>
    public static AnchorScope AtRoundBoundary() => new AnchorScope(null);

    /// <summary>Restores the previous anchor when disposed, unless the count moved on inside the scope.</summary>
    public readonly struct AnchorScope : IDisposable
    {
        private readonly CharacterController _previous;
        private readonly CharacterController _scoped;

        internal AnchorScope(CharacterController scoped)
        {
            _previous = CurrentAnchor;
            _scoped = scoped;
            CurrentAnchor = scoped;
        }

        public void Dispose()
        {
            if (CurrentAnchor == _scoped)
                CurrentAnchor = _previous;
        }
    }

    /// <summary>
    /// True when an effect with this anchor ticks at the round boundary: no anchor, a destroyed one, or one that is not
    /// in the initiative order (<paramref name="isInInitiative"/>; null means no initiative order, so every effect does).
    /// </summary>
    public static bool TicksAtRoundBoundary(CharacterController anchor, Func<CharacterController, bool> isInInitiative)
    {
        if (anchor == null)
            return true;
        return isInInitiative == null || !isInInitiative(anchor);
    }

    /// <summary>True when an effect with <paramref name="effectAnchor"/> ticks at <paramref name="reachedCount"/>'s initiative count.</summary>
    public static bool TicksAtCount(CharacterController effectAnchor, CharacterController reachedCount)
        => reachedCount != null && effectAnchor != null && effectAnchor == reachedCount;

    /// <summary>
    /// Re-application of a condition that is already active (one instance per type, or per source): keeps whichever
    /// application ends later. An indefinite instance stays. A new indefinite or longer application replaces the
    /// remaining rounds and the anchor. An equal count also takes the new anchor: the existing instance ends within
    /// its remaining rounds, the new one exactly that many rounds from the current count, so never earlier.
    /// </summary>
    public static void Refresh(StatusEffect existing, int rounds)
    {
        if (existing == null || existing.RemainingRounds < 0)
            return;

        if (rounds < 0 || rounds >= existing.RemainingRounds)
        {
            existing.RemainingRounds = rounds;
            existing.DurationAnchor = CurrentAnchor;
        }
    }

    /// <summary>
    /// The same rule for a spell effect that is already active (the same spell does not stack): keeps whichever casting
    /// ends later; an equal count takes the current anchor. A permanent (-1) or concentration (-2) effect stays.
    /// Returns true when the new casting's duration was taken.
    /// </summary>
    public static bool Refresh(ActiveSpellEffect existing, int rounds)
    {
        if (existing == null || existing.RemainingRounds < 0)
            return false;

        if (rounds == -1 || rounds >= existing.RemainingRounds)
        {
            existing.RemainingRounds = rounds;
            existing.DurationAnchor = CurrentAnchor;
            return true;
        }
        return false;
    }

    /// <summary>
    /// An area keeps <paramref name="effect"/> up while the creature stays inside and re-applies it at the start of each
    /// round: it lasts <paramref name="rounds"/> and ticks at the round boundary, so it never lapses at a creature's count
    /// before the area renews it.
    /// </summary>
    public static void SustainAtRoundBoundary(ActiveSpellEffect effect, int rounds)
    {
        if (effect == null)
            return;
        effect.RemainingRounds = rounds;
        effect.DurationAnchor = null;
    }

    /// <summary>
    /// <paramref name="from"/> left the initiative order: every condition and spell effect on <paramref name="characters"/>
    /// anchored to it is anchored to <paramref name="to"/> instead (null: the round boundary). Returns how many moved.
    /// </summary>
    public static int Reanchor(IEnumerable<CharacterController> characters, CharacterController from, CharacterController to)
    {
        if (characters == null || from == null)
            return 0;

        int moved = 0;
        foreach (CharacterController c in characters)
        {
            if (c == null)
                continue;

            List<StatusEffect> conditions = c.Stats != null ? c.Stats.ActiveConditions : null;
            if (conditions != null)
            {
                foreach (StatusEffect condition in conditions)
                {
                    if (condition != null && condition.DurationAnchor == from)
                    {
                        condition.DurationAnchor = to;
                        moved++;
                    }
                }
            }

            StatusEffectManager statusMgr = c.StatusEffectManager;
            if (statusMgr != null && statusMgr.ActiveEffects != null)
            {
                foreach (ActiveSpellEffect effect in statusMgr.ActiveEffects)
                {
                    if (effect != null && effect.DurationAnchor == from)
                    {
                        effect.DurationAnchor = to;
                        moved++;
                    }
                }
            }
        }
        return moved;
    }
}

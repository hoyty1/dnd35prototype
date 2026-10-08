using System.Collections.Generic;

namespace DND35.AI
{
    /// <summary>
    /// STOPGAP (owner decision 2026-10-07, CMB-102 open item 7, AI-060): what the AI's maneuver
    /// evaluation remembers about one creature's own maneuvers during its current turn. It is an AI
    /// decision limit, not a rule; PCs are never limited by it.
    ///
    /// - Once a maneuver succeeds, the AI makes no further maneuver that turn, so a trip that lands is
    ///   followed by attacks on the prone target instead of a disarm or grapple chain. A grapple that
    ///   took hold keeps its own handoff (the rest of the turn becomes grapple actions).
    /// - After a maneuver fails (a failed check, or an attempt foiled by an attack of opportunity), the
    ///   AI does not retry that maneuver type against that target that turn; it may still attack, or
    ///   use a different maneuver if the existing evaluation picks one.
    /// - Coup de grace is never limited.
    ///
    /// Read and written only by AIService (TryExecutePreferredManeuver). Owned by each creature
    /// (CharacterController.AIManeuverMemory) and cleared by CharacterController.StartNewTurn.
    /// Replacement plan: weighted personality scoring in the AI profiles (odds of success, value of the
    /// result, personality weights; docs/designs/enemy_ai_knowledge_and_personalities.md), which will
    /// choose between a maneuver and an attack at every step and retire this class (AI-060).
    /// </summary>
    public sealed class AIManeuverTurnMemory
    {
        private readonly List<KeyValuePair<CharacterController, SpecialAttackType>> _failed
            = new List<KeyValuePair<CharacterController, SpecialAttackType>>();

        /// <summary>True once one of this creature's maneuvers succeeded this turn.</summary>
        public bool ManeuverSucceededThisTurn { get; private set; }

        /// <summary>Forgets the turn (called at the creature's turn start).</summary>
        public void Clear()
        {
            ManeuverSucceededThisTurn = false;
            _failed.Clear();
        }

        /// <summary>Records one maneuver the AI attempted this turn and whether it succeeded.</summary>
        public void Record(CharacterController target, SpecialAttackType type, bool succeeded)
        {
            if (type == SpecialAttackType.CoupDeGrace)
                return;

            if (succeeded)
            {
                ManeuverSucceededThisTurn = true;
                return;
            }

            if (target != null && !HasFailed(target, type))
                _failed.Add(new KeyValuePair<CharacterController, SpecialAttackType>(target, type));
        }

        /// <summary>True when a <paramref name="type"/> attempt against <paramref name="target"/> failed this turn.</summary>
        public bool HasFailed(CharacterController target, SpecialAttackType type)
        {
            for (int i = 0; i < _failed.Count; i++)
            {
                if (_failed[i].Key == target && _failed[i].Value == type)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True when the stopgap forbids the AI to attempt <paramref name="type"/> against
        /// <paramref name="target"/> now: a maneuver already succeeded this turn, or this type already
        /// failed against this target. Coup de grace is never forbidden.
        /// </summary>
        public bool Forbids(CharacterController target, SpecialAttackType type, out string reason)
        {
            reason = null;
            if (type == SpecialAttackType.CoupDeGrace)
                return false;

            if (ManeuverSucceededThisTurn)
            {
                reason = "a maneuver already succeeded this turn";
                return true;
            }

            if (HasFailed(target, type))
            {
                reason = $"{type} already failed against this target this turn";
                return true;
            }

            return false;
        }
    }
}

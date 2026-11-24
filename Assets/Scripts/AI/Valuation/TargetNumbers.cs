using System.Collections.Generic;
using DND35.AI.Knowledge;

namespace DND35.AI.Valuation
{
    /// <summary>
    /// The one place the AI's valuations read another creature's check modifiers, maneuver numbers, AoO estimates and the
    /// state behind a foe's next-round reach (alive, move, the charge's conditions; AI-089) (batch 14 step 8,
    /// docs/designs/personalities_batch14.md "AI/Valuation"): the true values, straight from the shared rules getters the
    /// maneuver cores roll with, so an estimate never drifts from the roll. It is the [KnowledgeLegacy] choke point until
    /// batch 17 (fair sight) decides what an observer may know of them (a foe's Strength, feats and held items); the
    /// omniscience lint (OmniscienceLintTests) skips this file and holds every other file under AI/Valuation and
    /// AI/Personality to the knowledge facade. Name no member here Stats, GridPosition, HasCondition or Is*: the lint's target
    /// pattern (target*) matches the class name itself, so a call such as TargetNumbers.IsAlive(foe) is flagged in every
    /// caller. Pure reads: no dice, nothing changed.
    /// </summary>
    [KnowledgeLegacy("a target's check modifiers, AoO estimates and the state behind a foe's next-round reach are read true until batch 17 gates them by what the observer knows")]
    public static class TargetNumbers
    {
        /// <summary>The whole modifier of <paramref name="self"/>'s maneuver touch attack on <paramref name="target"/> at <paramref name="bab"/> (its racial bonus against the target's kind included).</summary>
        public static int TouchAttackModifier(CharacterController self, CharacterController target, int? bab)
            => self != null ? self.GetManeuverTouchAttackModifierAgainst(target, bab) : 0;

        /// <summary><paramref name="target"/>'s touch AC against <paramref name="self"/>'s maneuver touch attack.</summary>
        public static int TouchArmorClass(CharacterController self, CharacterController target)
            => target != null ? target.GetManeuverTouchArmorClassAgainst(self) : 0;

        /// <summary>
        /// <paramref name="target"/>'s current hit points, read true (0 without a creature): the death touch's value needs
        /// the number its 1d6 per cleric level must reach (PHB p.186; batch 14 step 14). The knowledge facade has only the
        /// WoundBand, so batch 17 decides what an observer may know of the exact number (the plan's HpCurrent).
        /// </summary>
        public static int CurrentHitPoints(CharacterController target)
            => target != null && target.Stats != null ? target.Stats.CurrentHP : 0;

        /// <summary>
        /// <paramref name="target"/> is a living creature: it has stats and is not dead (CharacterStats.IsDead), read true
        /// (false without a creature). The foe-reach estimate (TurnOptionValue.FoeReach, batch 14 step 13) reads it for the
        /// foe and for the creature it could attack; moved here by AI-089 so the valuation reads no foe past this choke point.
        /// </summary>
        public static bool Alive(CharacterController target)
            => target != null && target.Stats != null && !target.Stats.IsDead;

        /// <summary><paramref name="target"/>'s move in squares this round (CharacterStats.MoveRange: 0 when a condition blocks movement), read true (0 without stats; AI-089).</summary>
        public static int MoveSquares(CharacterController target)
            => target != null && target.Stats != null ? target.Stats.MoveRange : 0;

        /// <summary>
        /// The creature-side conditions of <paramref name="target"/>'s charge next round (GameManager.CanCharge's legality
        /// read ahead, PHB p.154; TurnOptionValue.FoeReach.CanChargeNextRound): a melee attack to make (a melee weapon, or
        /// unarmed: CharacterController.HasMeleeWeaponEquipped), standing, not grappling, not fatigued, exhausted or
        /// entangled, and not limited to a move action. Read true, in that order (AI-089); the caller checks the move.
        /// </summary>
        public static bool ChargeConditionsMet(CharacterController target)
            => target != null && target.Stats != null && target.HasMeleeWeaponEquipped()
               && !target.HasCondition(CombatConditionType.Prone) && !target.IsGrappling()
               && !target.Stats.IsFatiguedOrExhausted && !target.HasCondition(CombatConditionType.Entangled)
               && (target.Actions == null || !target.Actions.MoveActionOnly);

        /// <summary><paramref name="target"/> may take only a single move or standard action this round (its action economy's SingleActionOnly; AI-089).</summary>
        public static bool SingleActionOnly(CharacterController target)
            => target != null && target.Actions != null && target.Actions.SingleActionOnly;

        /// <summary><paramref name="target"/>'s modifier to resist a trip: the better of Strength and Dexterity, size, stability (PHB p.158).</summary>
        public static int TripResistModifier(CharacterController target)
            => target != null ? target.GetTripOrOverrunDefenderCheckModifier() : 0;

        /// <summary><paramref name="target"/>'s Strength-check modifier when it trips (its counter-trip, PHB p.158).</summary>
        public static int TripCheckModifier(CharacterController target)
            => target != null ? target.GetTripAttackerCheckModifier() : 0;

        /// <summary>May <paramref name="target"/> try to trip <paramref name="self"/> back after a trip by it fails (PHB p.158; CharacterController.CanCounterTrip)?</summary>
        public static bool CanCounterTrip(CharacterController self, CharacterController target)
            => target != null && self != null && target.CanCounterTrip(self, out _);

        /// <summary><paramref name="target"/>'s opposed grapple-check modifier against a grapple started on it (PHB p.156).</summary>
        public static int GrappleResistModifier(CharacterController target)
            => target != null ? target.GetGrappleCheckModifierToResistStart() : 0;

        /// <summary>A hold by <paramref name="self"/> automatically fails: <paramref name="target"/> is two or more sizes larger (PHB p.156).</summary>
        public static bool GrappleHoldFailsBySize(CharacterController self, CharacterController target)
            => self != null && self.GrappleHoldFailsBySize(target);

        /// <summary>The terms of a disarm by <paramref name="self"/> on <paramref name="target"/>'s item in <paramref name="slot"/> (null: the default); false when it holds nothing to disarm.</summary>
        public static bool TryDisarm(CharacterController self, CharacterController target, EquipSlot? slot, int? bab,
            out CharacterController.DisarmCheckModifiers modifiers, ItemData attackerWeaponOverride = null)
        {
            modifiers = default(CharacterController.DisarmCheckModifiers);
            return self != null && self.TryGetDisarmCheckModifiersAgainst(target, slot, bab, out modifiers, attackerWeaponOverride);
        }

        /// <summary>The terms of <paramref name="target"/>'s counter-disarm on <paramref name="self"/> (PHB p.155; ResolveDisarm's counter: its main-hand weapon, full BAB, the default item).</summary>
        public static bool TryCounterDisarm(CharacterController self, CharacterController target, out CharacterController.DisarmCheckModifiers modifiers)
        {
            modifiers = default(CharacterController.DisarmCheckModifiers);
            return target != null && target.TryGetDisarmCheckModifiersAgainst(self, null, null, out modifiers);
        }

        /// <summary>The terms of a sunder by <paramref name="self"/> on <paramref name="target"/>'s item in <paramref name="slot"/> (null: the default), with <paramref name="attackerWeaponOverride"/> or natural attack <paramref name="naturalAttackIndex"/> (0 or more) when given; false when nothing can be sundered.</summary>
        public static bool TrySunder(CharacterController self, CharacterController target, EquipSlot? slot, int? bab,
            out CharacterController.SunderCheckModifiers modifiers, ItemData attackerWeaponOverride = null, int naturalAttackIndex = -1)
        {
            modifiers = default(CharacterController.SunderCheckModifiers);
            return self != null && self.TryGetSunderCheckModifiersAgainst(target, slot, bab, out modifiers, attackerWeaponOverride, naturalAttackIndex);
        }

        /// <summary>The chance <paramref name="provoker"/>'s AoO hits <paramref name="victim"/> (ThreatSystem.EstimateAoOHitChance): the provoker's true attack bonus, read here so batch 17 can gate it.</summary>
        public static float AoOHitChance(CharacterController provoker, CharacterController victim)
            => provoker != null && victim != null ? ThreatSystem.EstimateAoOHitChance(provoker, victim) : 0f;

        /// <summary>The expected damage of <paramref name="provoker"/>'s AoO on <paramref name="victim"/> (ThreatSystem.EstimateExpectedAoODamage): the provoker's true attack and damage, read here so batch 17 can gate them.</summary>
        public static float ExpectedAoODamage(CharacterController provoker, CharacterController victim)
            => provoker != null && victim != null ? ThreatSystem.EstimateExpectedAoODamage(provoker, victim) : 0f;

        /// <summary>The creatures that take an AoO when <paramref name="self"/> starts <paramref name="type"/> on <paramref name="target"/> (ThreatSystem.GetManeuverAoOProvokers, the rule the maneuver core resolves).</summary>
        public static List<CharacterController> ManeuverProvokers(CharacterController self, CharacterController target, SpecialAttackType type,
            List<CharacterController> allCharacters)
            => ThreatSystem.GetManeuverAoOProvokers(self, target, type, allCharacters);
    }
}

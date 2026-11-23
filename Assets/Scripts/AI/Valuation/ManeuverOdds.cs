using System.Collections.Generic;

namespace DND35.AI.Valuation
{
    /// <summary>
    /// The AI's estimate of one maneuver attempt (batch 14 step 8, docs/designs/personalities_batch14.md "Maneuver scorer"
    /// and D6): the chance of each stage the maneuver core runs and what the attempt risks. Each chance mirrors the roll in
    /// the shared rules code from the same modifiers (no rule is added here). Read by the maneuver scorer (step 9).
    /// </summary>
    public struct ManeuverOddsEstimate
    {
        public SpecialAttackType Type;

        /// <summary>The estimate covers this maneuver (trip, grapple, disarm, sunder); false for the rest.</summary>
        public bool Supported;

        /// <summary>
        /// False when the attempt cannot succeed whatever the dice: the target holds nothing to disarm or has nothing to
        /// sunder, the attacker has nothing to sunder with, or a hold fails by size (PHB p.155-158).
        /// </summary>
        public bool CanSucceed;

        /// <summary>The BAB the step rolls at (the caller's, see <see cref="ManeuverOdds.StepBab"/>).</summary>
        public int StepBab;

        /// <summary>A trip or grapple starts with a melee touch attack (PHB p.156, p.158).</summary>
        public bool HasTouchAttack;

        /// <summary>The chance that touch attack hits (a natural 1 misses, a natural 20 hits); 1 without one.</summary>
        public float TouchChance;

        /// <summary>The chance of winning the opposed check or roll once it is made (trip: Strength against Strength or Dexterity; grapple: grapple checks; disarm and sunder: attack rolls).</summary>
        public float CheckChance;

        /// <summary>The creatures that take an initiation AoO (ThreatSystem.GetManeuverAoOProvokers).</summary>
        public int Provokers;

        /// <summary>The sum of their expected AoO damage on the attacker (ThreatSystem.EstimateExpectedAoODamage).</summary>
        public float ExpectedAoODamage;

        /// <summary>
        /// The chance no initiation AoO stops the attempt: for a disarm, grapple or sunder each AoO hit is taken to stop it
        /// (ThreatSystem.DoesManeuverAoODisruptAttempt: a disarm only when the hit deals damage, PHB p.155, which a hit is
        /// taken to do); 1 for a trip. An AoO that drops the attacker is not counted.
        /// </summary>
        public float NotFoiledChance;

        /// <summary>A failed attempt lets the defender try back: a trip lost at the opposed check (not with a missed touch), PHB p.158; a lost disarm without Improved Disarm, PHB p.155, p.95.</summary>
        public bool CounterPossible;

        /// <summary>The chance the defender's counter-trip or counter-disarm succeeds, when it makes one.</summary>
        public float CounterChance;

        /// <summary>A sunder that would deal nonlethal damage: a win deals the item nothing (PHB p.165).</summary>
        public bool DealsNoDamage;

        /// <summary>The chance the attempt succeeds: not foiled, the touch hits, the check is won.</summary>
        public float SuccessChance => Supported && CanSucceed ? NotFoiledChance * TouchChance * CheckChance : 0f;

        /// <summary>The chance the attempt is made and lost at its opposed check, the case a counter follows.</summary>
        public float LostCheckChance => Supported && CanSucceed ? NotFoiledChance * TouchChance * (1f - CheckChance) : 0f;
    }

    /// <summary>
    /// The maneuver odds (batch 14 step 8): <see cref="Estimate"/> reads the attacker's own numbers directly and the
    /// target's and the provokers' (their AoO estimates) through <see cref="TargetNumbers"/> (the [KnowledgeLegacy] choke point until batch 17). Pure: no dice,
    /// nothing changed. The modifiers are the ones the rolls add (CharacterController.GetTripAttackerCheckModifier,
    /// GetManeuverTouchAttackModifierAgainst, GetGrappleCheckModifierToStart, GetDisarmCheckModifiers, the sunder terms),
    /// so <c>ManeuverOddsTests</c> can pin them to the rolls.
    /// </summary>
    public static class ManeuverOdds
    {
        /// <summary>
        /// The BAB a maneuver replacing <paramref name="self"/>'s next main-hand step rolls at: GameManager.GetManeuverStepBab
        /// with the step hook's intent (its two-weapon state, PHB p.160), else the step's substitute BAB.
        /// </summary>
        public static int StepBab(CharacterController self)
        {
            if (self == null || self.Stats == null)
                return 0;
            GameManager gm = GameManager.Instance;
            if (gm != null)
                return gm.GetManeuverStepBab(self, gm.ManeuverStepIntentForAI);
            return self.GetManeuverSubstituteBAB(self.ProgressiveAttackPool.MainHandStepsUsed);
        }

        /// <summary>
        /// The odds of <paramref name="self"/>'s <paramref name="type"/> on <paramref name="target"/> at
        /// <paramref name="stepBab"/>, against the item in <paramref name="itemSlot"/> for a disarm or sunder (null: the item
        /// the rules code picks by default), made with <paramref name="attackerWeaponOverride"/> for a disarm or sunder (the
        /// hand the step pays, as GameManager.TryConsumeDisarmAttackAction and TryConsumeSunderAttackAction pass it; null: the
        /// main-hand weapon). The provokers come from <paramref name="allCharacters"/> (null: every creature in the
        /// encounter). The attempt is taken to be legal (GameManager.CanStartManeuver decides that). For a creature fighting
        /// with its natural attacks, the grapple check and the sunder attack read the natural attack the step would give up
        /// (<see cref="StepNaturalAttackIndex"/>), as the rolls do once the step is committed.
        /// </summary>
        public static ManeuverOddsEstimate Estimate(CharacterController self, CharacterController target, SpecialAttackType type, int stepBab,
            EquipSlot? itemSlot = null, List<CharacterController> allCharacters = null, ItemData attackerWeaponOverride = null)
        {
            var odds = new ManeuverOddsEstimate
            {
                Type = type,
                StepBab = stepBab,
                TouchChance = 1f,
                NotFoiledChance = 1f,
            };
            if (self == null || self.Stats == null || target == null)
                return odds;

            switch (type)
            {
                case SpecialAttackType.Trip:
                    odds.Supported = true;
                    odds.CanSucceed = true;
                    odds.HasTouchAttack = true;
                    odds.TouchChance = TouchHitChance(TargetNumbers.TouchAttackModifier(self, target, stepBab), TargetNumbers.TouchArmorClass(self, target));
                    odds.CheckChance = CharacterController.EstimateOpposedCheckWinChance(self.GetTripAttackerCheckModifier(), TargetNumbers.TripResistModifier(target));
                    odds.CounterPossible = TargetNumbers.CanCounterTrip(self, target);
                    if (odds.CounterPossible)
                        odds.CounterChance = CharacterController.EstimateOpposedCheckWinChance(TargetNumbers.TripCheckModifier(target), self.GetTripOrOverrunDefenderCheckModifier());
                    break;

                case SpecialAttackType.Grapple:
                    odds.Supported = true;
                    odds.HasTouchAttack = true;
                    odds.CanSucceed = !TargetNumbers.GrappleHoldFailsBySize(self, target);
                    odds.TouchChance = TouchHitChance(TargetNumbers.TouchAttackModifier(self, target, stepBab), TargetNumbers.TouchArmorClass(self, target));
                    // The grapple check keeps the step's BAB but not a secondary natural attack's penalty (CMB-102): the
                    // penalty of the natural attack the step would give up, not the last one recorded.
                    int checkBab = GameManager.Instance != null
                        ? self.GetGrappleCheckBabAfterTouchAttack(stepBab, true, StepNaturalAttackIndex(self, false))
                        : self.GetGrappleCheckBabAfterTouchAttack(stepBab, true);
                    odds.CheckChance = CharacterController.EstimateOpposedCheckWinChance(self.GetGrappleCheckModifierToStart(checkBab), TargetNumbers.GrappleResistModifier(target));
                    break;

                case SpecialAttackType.Disarm:
                    odds.Supported = true;
                    if (TargetNumbers.TryDisarm(self, target, itemSlot, stepBab, out CharacterController.DisarmCheckModifiers disarm, attackerWeaponOverride))
                    {
                        odds.CanSucceed = true;
                        odds.CheckChance = CharacterController.EstimateOpposedCheckWinChance(disarm.AttackerModifier, disarm.DefenderModifier);
                        if (!self.Stats.HasFeat("Improved Disarm")
                            && TargetNumbers.TryCounterDisarm(self, target, out CharacterController.DisarmCheckModifiers counter))
                        {
                            odds.CounterPossible = true;
                            odds.CounterChance = CharacterController.EstimateOpposedCheckWinChance(counter.AttackerModifier, counter.DefenderModifier);
                        }
                    }
                    break;

                case SpecialAttackType.Sunder:
                    odds.Supported = true;
                    // A natural attacker sunders with the natural attack the step gives up; when that one cannot sunder
                    // (no slashing or bludgeoning damage, PHB p.158) the step is refused (CMB-102).
                    int sunderNatural = -1;
                    if (attackerWeaponOverride == null && GameManager.Instance != null && self.GetManeuverSubstituteStepKind() == AttackStepKind.NaturalSequence)
                    {
                        sunderNatural = StepNaturalAttackIndex(self, true);
                        if (sunderNatural < 0)
                            break;
                    }
                    if (TargetNumbers.TrySunder(self, target, itemSlot, stepBab, out CharacterController.SunderCheckModifiers sunder, attackerWeaponOverride, sunderNatural))
                    {
                        odds.CanSucceed = true;
                        odds.DealsNoDamage = sunder.DealsNonlethalDamage;
                        // ResolveSunder: the attacker wins when its total is at least the defender's.
                        odds.CheckChance = ChanceAtLeast(sunder.AttackerModifier, sunder.DefenderModifier);
                    }
                    break;

                default:
                    return odds;
            }

            GameManager gm = GameManager.Instance;
            List<CharacterController> all = allCharacters ?? (gm != null ? gm.GetAllCharactersForAI() : null);
            List<CharacterController> provokers = TargetNumbers.ManeuverProvokers(self, target, type, all);
            bool hitsDisrupt = ThreatSystem.CanManeuverAoOHitDisruptAttempt(type);
            for (int i = 0; i < provokers.Count; i++)
            {
                CharacterController provoker = provokers[i];
                odds.Provokers++;
                odds.ExpectedAoODamage += TargetNumbers.ExpectedAoODamage(provoker, self);
                if (hitsDisrupt)
                    odds.NotFoiledChance *= 1f - TargetNumbers.AoOHitChance(provoker, self);
            }

            return odds;
        }

        /// <summary>
        /// The natural attack <paramref name="self"/>'s next maneuver step would give up (GameManager.GetManeuverNaturalAttackIndexForAI;
        /// a sunder's must be able to sunder, <paramref name="forSunder"/>); -1 for a creature not fighting with its natural
        /// attacks, or without a GameManager.
        /// </summary>
        public static int StepNaturalAttackIndex(CharacterController self, bool forSunder)
        {
            GameManager gm = GameManager.Instance;
            return gm != null && self != null && self.Stats != null ? gm.GetManeuverNaturalAttackIndexForAI(self, forSunder) : -1;
        }

        /// <summary>
        /// The chance a d20 + <paramref name="attackModifier"/> touch attack hits <paramref name="touchArmorClass"/>
        /// (CharacterController.IsManeuverTouchAttackHit: a natural 20 hits, a natural 1 misses), exact over the 20 rolls.
        /// </summary>
        public static float TouchHitChance(int attackModifier, int touchArmorClass)
        {
            int hits = 0;
            for (int roll = 1; roll <= 20; roll++)
                if (CharacterController.IsManeuverTouchAttackHit(roll, roll + attackModifier, touchArmorClass))
                    hits++;
            return hits / 20f;
        }

        /// <summary>The chance d20 + <paramref name="attackerModifier"/> is at least d20 + <paramref name="defenderModifier"/>, exact over the 400 roll pairs (ties to the attacker).</summary>
        public static float ChanceAtLeast(int attackerModifier, int defenderModifier)
        {
            int wins = 0;
            for (int a = 1; a <= 20; a++)
                for (int d = 1; d <= 20; d++)
                    if (a + attackerModifier >= d + defenderModifier)
                        wins++;
            return wins / 400f;
        }
    }
}

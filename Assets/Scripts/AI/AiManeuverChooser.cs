using System.Collections.Generic;
using System.Globalization;
using DND35.AI.Knowledge;
using DND35.AI.Valuation;
using UnityEngine;

namespace DND35.AI
{
    /// <summary>What <see cref="AiManeuverChooser.Decide"/> chose for one attack step.</summary>
    public enum ManeuverChoiceKind
    {
        /// <summary>The attack: no maneuver was proposed, or the proposal lost to the attack it would replace.</summary>
        Attack,
        /// <summary>A coup de grace (PHB p.153): the rules allow one and the creature takes it.</summary>
        CoupDeGrace,
        /// <summary>The Role's proposed maneuver beat the attack.</summary>
        Maneuver
    }

    /// <summary>
    /// The terms of one maneuver-versus-attack comparison (batch 14 step 9, docs/designs/personalities_batch14.md "Maneuver
    /// scorer"), in expected hit points. <see cref="AiManeuverChooser.Gather"/> fills them from the creatures; the golden
    /// table of ManeuverChooserTests fills them by hand. Pure data.
    /// </summary>
    public struct ManeuverScoreInputs
    {
        public SpecialAttackType Type;
        /// <summary>The odds (ManeuverOdds.Estimate): SuccessChance, LostCheckChance, CounterChance, ExpectedAoODamage.</summary>
        public float SuccessChance;
        public float LostCheckChance;
        public bool CounterPossible;
        public float CounterChance;
        public float ExpectedAoODamage;
        /// <summary>S: the value of the attack the maneuver replaces (hit chance times average damage; the whole sequence when the maneuver replaces it).</summary>
        public float AttackValue;
        /// <summary>D: the average damage of one hit of the replaced attack.</summary>
        public float HitDamage;
        /// <summary>dH: the gain in that attack's hit chance against a prone target (+4 for a melee attack, PHB p.311).</summary>
        public float ProneHitGain;
        /// <summary>Th: the target's expected damage on the creature in a round (the ThreatPerRound fact; 0 when it cannot attack).</summary>
        public float TargetThreat;
        /// <summary>r: the creature's own attack steps left after this one, this turn.</summary>
        public int StepsLeft;
        /// <summary>a: the creature's allies that threaten the target and can attack.</summary>
        public int AlliesThreatening;
        /// <summary>The creature has Improved Trip: a trip that lands gives an immediate melee attack (PHB p.96).</summary>
        public bool ImprovedTrip;
        /// <summary>The share of the target's threat its disarmable weapon carries (1 when it fights with a held weapon, else 0).</summary>
        public float TargetWeaponShare;
        /// <summary>What the creature loses to a counter-disarm: 0.75 times its own round damage when it fights with a held weapon, else 0.</summary>
        public float OwnWeaponValue;
        /// <summary>rho (C7): 1 with no personality; lerp(0.4, 1.6, RiskAversion) with one.</summary>
        public float Rho;
        /// <summary>The traits' RoleDesire for Attack (PersonalityProfile.RoleShift); 0 with no personality.</summary>
        public float AttackDesire;
    }

    /// <summary>The scorer's verdict on <see cref="ManeuverScoreInputs"/>: the maneuver wins iff Net &gt;= Threshold.</summary>
    public struct ManeuverScore
    {
        /// <summary>V: the value of the maneuver once it succeeds.</summary>
        public float Value;
        /// <summary>The expected cost: the initiation AoOs times rho, and the counter-trip or counter-disarm.</summary>
        public float Cost;
        /// <summary>P_success times V minus the cost.</summary>
        public float Net;
        /// <summary>S times (1 + 0.5 desire(Attack)) plus the margin max(0.5, 0.1 S).</summary>
        public float Threshold;
        /// <summary>False for a maneuver with no value model (sunder, bull rush): the attack is kept.</summary>
        public bool Modelled;
        public bool Wins => Modelled && Net >= Threshold;
    }

    /// <summary>One step's decision.</summary>
    public struct ManeuverDecision
    {
        public ManeuverChoiceKind Kind;
        /// <summary>The Role's proposal (the profile's GetPreferredManeuver past its gates, or the no-profile proposer); null when none.</summary>
        public SpecialAttackType? Proposed;
        /// <summary>The scored terms, when a proposal was scored.</summary>
        public ManeuverScoreInputs Inputs;
        public ManeuverScore Score;
        public string Reason;

        /// <summary>The maneuver to execute: the coup de grace, or the winning proposal; null for the attack.</summary>
        public SpecialAttackType? Chosen => Kind == ManeuverChoiceKind.CoupDeGrace ? SpecialAttackType.CoupDeGrace
            : Kind == ManeuverChoiceKind.Maneuver ? Proposed : null;

        public bool UsesManeuver => Kind != ManeuverChoiceKind.Attack;
    }

    /// <summary>
    /// The AI's maneuver-versus-attack choice (batch 14 step 9; AI-035, AI-060, AI-058, AI-025; design choice C9: the Role
    /// proposes, the scorer disposes). Asked once before each melee attack step of an AI-run creature (the step hook of
    /// GameManager.RunAttackSequence, through AIService.CreateMeleeStepManeuverEvaluator), before a ranged or thrown sequence
    /// (GameManager.NPCPerformAttack's one try) and by the ranged kiter's adjacent attack. In order:
    /// <list type="number">
    /// <item>A coup de grace when the rules allow one (GameManager.CanUseCoupDeGraceAttackOption) and the creature takes it
    /// (AIService.UsesCoupDeGrace: the data override, the traits, the Role).</item>
    /// <item>No maneuver while grappling, without an attack step left, or on a helpless or unconscious target (AI-058: the
    /// attack, which a helpless target cannot dodge, PHB p.153, is worth more than knocking it down).</item>
    /// <item>The Role's proposal, at most one per step (<see cref="Propose"/>), then <see cref="Score"/> against the attack
    /// it replaces: it wins iff P_success x V - costs &gt;= S x (1 + 0.5 desire(Attack)) + max(0.5, 0.1 S).</item>
    /// </list>
    /// There is no per-turn memory (the AI-060 stopgap is gone): every step is decided again from the odds and values, so a
    /// trip that failed is tried again only while it still beats the attack, and a prone target is no longer proposed a
    /// trip. The natural-sequence limit of one substitute stays (GameManager.RunAttackSequence, naturalSubstituteUsed). The
    /// constants are starter numbers (AI tuning, not rules), pinned by ManeuverChooserTests. Reads only: no dice, nothing
    /// changed; the caller executes the choice. Timed in the Maneuver bucket; one AiDecisionTrace row ("Maneuver") per
    /// decision that had a proposal or a coup de grace, and a follow-up row when the executor refuses the choice
    /// (<see cref="TraceRefused"/>).
    /// </summary>
    public static class AiManeuverChooser
    {
        /// <summary>A prone defender takes -4 AC against melee attacks (PHB p.311).</summary>
        public const int ProneMeleeAcPenalty = 4;
        /// <summary>The share of the target's round threat a trip takes away (its stand-up move, -4 on its melee attacks).</summary>
        public const float TripThreatFactor = 0.4f;
        /// <summary>The share of the target's weapon threat a disarm takes away (it may draw another weapon).</summary>
        public const float DisarmThreatFactor = 0.75f;
        /// <summary>The share of the target's round threat a grapple takes away.</summary>
        public const float GrappleThreatFactor = 0.5f;
        /// <summary>How far one unit of the traits' Attack desire raises the attack's side.</summary>
        public const float AttackDesireWeight = 0.5f;
        /// <summary>The margin's floor and its share of S: a near tie keeps the attack.</summary>
        public const float MarginFloor = 0.5f;
        public const float MarginFactor = 0.1f;

        /// <summary>
        /// The pure comparison: V by type, the costs, the threshold.
        /// <list type="bullet">
        /// <item>Trip: V = dH D (r + a) + 0.4 Th, plus S + dH D with Improved Trip (its attack follows a trip that landed, so it
        /// is made against the prone defender's -4 melee AC, PHB p.96, p.311); cost rho AoO + LostCheck x Counter x 0.4 Th
        /// (a counter-trip follows only a lost opposed check, PHB p.158).</item>
        /// <item>Disarm: V = 0.75 Th x the weapon's share; cost rho AoO + LostCheck x Counter x the creature's own weapon value
        /// (PHB p.155).</item>
        /// <item>Grapple: V = 0.5 Th + D; cost rho AoO.</item>
        /// <item>Sunder, bull rush and the rest: no value model (no shipped Role proposes them), so the attack is kept.</item>
        /// </list>
        /// </summary>
        public static ManeuverScore Score(in ManeuverScoreInputs i)
        {
            var s = new ManeuverScore();
            float aoo = i.Rho * Mathf.Max(0f, i.ExpectedAoODamage);
            float counter = i.CounterPossible ? i.LostCheckChance * i.CounterChance : 0f;
            switch (i.Type)
            {
                case SpecialAttackType.Trip:
                    s.Modelled = true;
                    s.Value = i.ProneHitGain * i.HitDamage * (Mathf.Max(0, i.StepsLeft) + Mathf.Max(0, i.AlliesThreatening))
                        + TripThreatFactor * i.TargetThreat
                        + (i.ImprovedTrip ? i.AttackValue + i.ProneHitGain * i.HitDamage : 0f);
                    s.Cost = aoo + counter * TripThreatFactor * i.TargetThreat;
                    break;
                case SpecialAttackType.Disarm:
                    s.Modelled = true;
                    s.Value = DisarmThreatFactor * i.TargetThreat * i.TargetWeaponShare;
                    s.Cost = aoo + counter * i.OwnWeaponValue;
                    break;
                case SpecialAttackType.Grapple:
                    s.Modelled = true;
                    s.Value = GrappleThreatFactor * i.TargetThreat + i.HitDamage;
                    s.Cost = aoo;
                    break;
                default:
                    s.Modelled = false;
                    break;
            }
            s.Net = i.SuccessChance * s.Value - s.Cost;
            float attack = Mathf.Max(0f, i.AttackValue);
            s.Threshold = attack * (1f + AttackDesireWeight * i.AttackDesire) + Mathf.Max(MarginFloor, MarginFactor * attack);
            return s;
        }

        /// <summary>
        /// The decision for <paramref name="npc"/>'s next attack step on <paramref name="target"/> (see the class summary).
        /// <paramref name="wholeAttack"/>: the maneuver replaces the whole attack action, not one step (the ranged kiter's
        /// adjacent attack, a ranged or thrown sequence's one try): S is the whole attack's value and no step follows.
        /// </summary>
        public static ManeuverDecision Decide(CharacterController npc, CharacterController target, AIProfile profile, bool wholeAttack = false)
        {
            if (npc == null || npc.Stats == null || target == null || target.Stats == null)
                return new ManeuverDecision { Kind = ManeuverChoiceKind.Attack, Reason = "no proposal" };
            ManeuverDecision d;
            // Only the decision is timed; its console line is built after the scope closes, and only with the trace on.
            using (AiDecisionTimer.Open(AiTimeBucket.Maneuver, npc))
                d = DecideCore(npc, target, profile, wholeAttack);
            if (AIService.TraceEnabled && d.Proposed.HasValue && d.Score.Modelled)
                Debug.Log($"[AI][Maneuver] {npc.Stats.CharacterName} vs {target.Stats.CharacterName}: {d.Proposed.Value} "
                          + (d.Score.Wins ? "beats" : "loses to") + " the attack (" + d.Reason + ")");
            return d;
        }

        private static ManeuverDecision DecideCore(CharacterController npc, CharacterController target, AIProfile profile, bool wholeAttack)
        {
            var d = new ManeuverDecision { Kind = ManeuverChoiceKind.Attack, Reason = "no proposal" };
            if (npc == null || npc.Stats == null || target == null || target.Stats == null)
                return d;

            GameManager gm = GameManager.Instance;
            if (gm != null && gm.CanUseCoupDeGraceAttackOption(npc) && AIService.UsesCoupDeGrace(npc))
            {
                d.Kind = ManeuverChoiceKind.CoupDeGrace;
                d.Reason = "coup de grace";
                TraceRow(npc, d, "CoupDeGrace");
                return d;
            }

            if (npc.IsGrappling())
                return d;
            // Trip, disarm, sunder and grapple need only the next attack step, iterative or natural (CMB-102); the
            // executor enforces the exact cost of the chosen type.
            if (npc.Actions == null || (!npc.Actions.HasStandardAction && !npc.CanCommitAttack(npc.GetManeuverSubstituteStepKind(), out _)))
                return d;

            if (IsHelplessOrDown(npc, target))
            {
                SpecialAttackType? wouldPropose = Propose(gm, npc, target, profile);
                if (wouldPropose.HasValue)
                {
                    d.Proposed = wouldPropose;
                    d.Reason = "target is helpless: attack (AI-058)";
                    TraceRow(npc, d, wouldPropose.Value.ToString());
                }
                return d;
            }

            SpecialAttackType? proposal = Propose(gm, npc, target, profile);
            if (!proposal.HasValue)
                return d;
            d.Proposed = proposal;
            d.Inputs = Gather(npc, target, proposal.Value, wholeAttack);
            d.Score = Score(d.Inputs);
            d.Kind = d.Score.Wins ? ManeuverChoiceKind.Maneuver : ManeuverChoiceKind.Attack;
            d.Reason = Describe(d.Inputs, d.Score);
            TraceRow(npc, d, proposal.Value.ToString());
            return d;
        }

        /// <summary>
        /// The Role's proposal for this step, past the maneuver's own gates (as AIService.ShouldUseManeuver had them): the
        /// profile's GetPreferredManeuver (a ranged-only loadout proposes nothing; a trip needs a standing target, a melee
        /// weapon and CharacterController.CanTrip, PHB p.158; a disarm a disarmable weapon; a grapple the Role's
        /// ShouldInitiateGrapple; a sunder a sunderable item, an attack that may sunder and lethal damage, PHB p.158, p.165),
        /// or, with no profile, GameManager.ChooseNPCFallbackManeuver (trip, else disarm at STR mod 3+, else grapple at STR
        /// mod 4+ without Improved Grab; AI-025: the one no-profile chooser). Null when nothing is proposed.
        /// </summary>
        public static SpecialAttackType? Propose(GameManager gm, CharacterController npc, CharacterController target, AIProfile profile)
        {
            if (npc == null || target == null || npc.Stats == null || target.Stats == null)
                return null;
            if (profile == null)
                return gm != null ? gm.ChooseNPCFallbackManeuverForAI(npc, target) : null;

            SpecialAttackType? preferred = profile.GetPreferredManeuver(npc, target);
            if (!preferred.HasValue)
                return null;
            SpecialAttackType type = preferred.Value;
            if (!npc.CanPerformSpecialAttack(type))
            {
                Debug.Log($"[AI][Maneuver] {npc.Stats.CharacterName} skips {type} due to ranged-only loadout ({npc.GetPrimaryWeaponType()}).");
                return null;
            }
            switch (type)
            {
                case SpecialAttackType.Trip:
                    return !target.HasCondition(CombatConditionType.Prone) && npc.HasMeleeWeaponEquipped() && npc.CanTrip(target, out _)
                        ? type : (SpecialAttackType?)null;
                case SpecialAttackType.Disarm:
                    return target.HasDisarmableWeaponEquipped() ? type : (SpecialAttackType?)null;
                case SpecialAttackType.Grapple:
                    return profile.ShouldInitiateGrapple(npc, target) ? type : (SpecialAttackType?)null;
                case SpecialAttackType.Sunder:
                    return target.HasSunderableItemEquipped() && npc.CanSunderWithAttack(-1, out _) && !npc.WouldSunderDealNonlethalDamage()
                        ? type : (SpecialAttackType?)null;
                default:
                    return type;
            }
        }

        /// <summary>
        /// The scorer's terms for <paramref name="npc"/>'s <paramref name="type"/> on <paramref name="target"/> now: the odds
        /// (ManeuverOdds.Estimate at ManeuverOdds.StepBab), the replaced attack (its modifier at the step's BAB, the target's AC
        /// fact, the average damage of its weapon or of the natural attack the step gives up), the target's ThreatPerRound
        /// fact, the steps left, the allies threatening the target, and the traits' rho and Attack desire.
        /// </summary>
        public static ManeuverScoreInputs Gather(CharacterController npc, CharacterController target, SpecialAttackType type, bool wholeAttack = false)
        {
            var i = new ManeuverScoreInputs { Type = type, Rho = 1f };
            if (npc == null || npc.Stats == null || target == null || target.Stats == null)
                return i;

            GameManager gm = GameManager.Instance;
            List<CharacterController> roster = gm != null ? gm.GetAllCharactersForAI() : new List<CharacterController>();
            int stepBab = ManeuverOdds.StepBab(npc);
            ManeuverOddsEstimate odds = ManeuverOdds.Estimate(npc, target, type, stepBab, null, roster);
            i.SuccessChance = odds.SuccessChance;
            i.LostCheckChance = odds.LostCheckChance;
            i.CounterPossible = odds.CounterPossible;
            i.CounterChance = odds.CounterChance;
            i.ExpectedAoODamage = odds.ExpectedAoODamage;

            ITargetView view = KnowledgeService.ViewOf(npc, target);
            FactValue acFact = view != null ? view.Get(FactKey.AcEstimate) : FactValue.Unknown;
            int ac = acFact.IsKnown ? acFact.Number : target.Stats.ArmorClass;
            FactValue threat = view != null ? view.Get(FactKey.ThreatPerRound) : FactValue.Unknown;
            i.TargetThreat = threat.IsKnown ? Mathf.Max(0f, threat.Amount) : 0f;
            bool targetArmed = view != null && (Flag(view, FactKey.WieldsMelee) || Flag(view, FactKey.WieldsRanged));
            i.TargetWeaponShare = targetArmed ? 1f : 0f;

            StepAttack(npc, target, stepBab, out int attackModifier, out float hitDamage);
            float hit = AttackValue.HitChance(attackModifier, ac);
            i.HitDamage = hitDamage;
            i.ProneHitGain = Mathf.Max(0f, AttackValue.HitChance(attackModifier, ac - ProneMeleeAcPenalty) - hit);
            i.ImprovedTrip = npc.Stats.HasFeat("Improved Trip");

            bool canContinue = CanMakeMoreSteps(npc);
            if (wholeAttack)
            {
                i.StepsLeft = 0;
                i.AttackValue = canContinue ? Mathf.Max(hit * hitDamage, AttackValue.ExpectedRoundDamage(npc, ac)) : hit * hitDamage;
            }
            else
            {
                i.AttackValue = hit * hitDamage;
                i.StepsLeft = canContinue ? StepsLeftAfterThis(npc) : 0;
            }

            i.AlliesThreatening = AlliesThreatening(npc, target, roster);
            ItemData own = npc.GetEquippedMainWeapon();
            i.OwnWeaponValue = own != null && own.IsWeapon ? DisarmThreatFactor * AttackValue.ExpectedRoundDamage(npc, ac) : 0f;

            Personality.PersonalityProfile p = npc.Personality;
            if (p != null)
            {
                i.Rho = p.Rho;
                i.AttackDesire = p.RoleShift(ActionRoles.Attack);
            }
            return i;
        }

        /// <summary>
        /// The replaced attack's modifier and average hit damage at <paramref name="stepBab"/>: a held weapon's terms
        /// (CharacterController.BuildAttackBonus) and average damage (AttackValue.AverageWeaponDamage); for a creature fighting
        /// with its natural attacks, the natural attack the step gives up (ManeuverOdds.StepNaturalAttackIndex; stepBab carries
        /// its secondary penalty); else the unarmed strike. An AI estimate (no flanking, criticals or riders, AI-084).
        /// </summary>
        private static void StepAttack(CharacterController npc, CharacterController target, int stepBab, out int modifier, out float damage)
        {
            CharacterStats st = npc.Stats;
            int meleeTerms = st.GetMeleeAttackBonus() - st.BaseAttackBonus;
            ItemData weapon = npc.GetEquippedMainWeapon();
            if (npc.GetManeuverSubstituteStepKind() == AttackStepKind.NaturalSequence)
            {
                int index = ManeuverOdds.StepNaturalAttackIndex(npc, false);
                NaturalAttackDefinition natural = index >= 0 ? st.GetNaturalAttackAtSequenceIndex(index) : st.GetPrimaryNaturalAttack();
                modifier = stepBab + meleeTerms;
                damage = natural != null ? AttackValue.AverageNaturalDamage(st, natural) : AttackValue.AverageMeleeDamage(npc);
                return;
            }
            if (weapon != null && weapon.IsWeapon)
            {
                modifier = stepBab + npc.BuildAttackBonus(target, weapon, false, null, false, 0, 0, CharacterController.IsWeaponTwoHanded(weapon)).Total;
                damage = AttackValue.AverageWeaponDamage(npc, weapon, offHand: false);
                return;
            }
            modifier = stepBab + meleeTerms;
            damage = AttackValue.AverageMeleeDamage(npc);
        }

        /// <summary>Whether another attack step can follow this one this turn: a full attack already, or the move action is still free (PHB p.143).</summary>
        private static bool CanMakeMoreSteps(CharacterController npc)
        {
            AttackPool pool = npc.ProgressiveAttackPool;
            return pool.IsFullAttack || (npc.Actions != null && npc.Actions.HasMoveAction);
        }

        /// <summary>The creature's main-hand or natural steps left after this one (CharacterController.GetMainHandAttackBudget).</summary>
        private static int StepsLeftAfterThis(CharacterController npc)
        {
            AttackPool pool = npc.ProgressiveAttackPool;
            int budget = pool.MainHandBudget > 0 ? pool.MainHandBudget : npc.GetMainHandAttackBudget(npc.GetManeuverSubstituteStepKind());
            return Mathf.Max(0, budget - pool.MainHandStepsUsed - 1);
        }

        /// <summary>The creature's allies (not itself) that can attack this round and threaten the target's space.</summary>
        private static int AlliesThreatening(CharacterController npc, CharacterController target, List<CharacterController> roster)
        {
            int count = 0;
            if (roster == null)
                return 0;
            for (int k = 0; k < roster.Count; k++)
            {
                CharacterController ally = roster[k];
                if (ally == null || ally == npc || ally == target || ally.Stats == null || ally.Stats.IsDead || !TeamUtility.IsAlly(npc, ally))
                    continue;
                if (KnowledgeFacts.CanAttackThisRound(ally) && KnowledgeFacts.ThreatensSpaceOf(ally, target))
                    count++;
            }
            return count;
        }

        /// <summary>The target is helpless or unconscious (the Helpless and Down facts): it gets the attack, never a maneuver (AI-058).</summary>
        public static bool IsHelplessOrDown(CharacterController npc, CharacterController target)
        {
            ITargetView view = KnowledgeService.ViewOf(npc, target);
            return view != null && (Flag(view, FactKey.Helpless) || Flag(view, FactKey.Down));
        }

        private static bool Flag(ITargetView view, FactKey key)
        {
            FactValue f = view.Get(key);
            return f.IsKnown && f.Flag;
        }

        private static string Describe(in ManeuverScoreInputs i, in ManeuverScore s)
        {
            if (!s.Modelled)
                return "no value model for " + i.Type + ": attack";
            return "P " + F(i.SuccessChance) + " V " + F(s.Value) + " cost " + F(s.Cost) + " net " + F(s.Net)
                   + " vs S " + F(i.AttackValue) + " threshold " + F(s.Threshold);
        }

        /// <summary>
        /// A follow-up "Maneuver" row when the executor refused the decided <paramref name="refused"/> (an unaffordable step, a
        /// target out of the one try's reach, a coup de grace the executor's own gate denies): the step stayed an attack, so
        /// the trace says so instead of leaving the decided maneuver as the last word.
        /// </summary>
        public static void TraceRefused(CharacterController npc, SpecialAttackType refused)
        {
            if (npc == null || !AiDecisionTrace.HasOpenTurn(npc))
                return;
            AiDecisionTrace.Record(npc, "Maneuver", "Attack", refused.ToString(), 0f, "executor refused " + refused + ": attack");
        }

        private static void TraceRow(CharacterController npc, in ManeuverDecision d, string legacy)
        {
            if (!AiDecisionTrace.HasOpenTurn(npc))
                return;
            string chosen = d.Kind == ManeuverChoiceKind.Attack ? "Attack" : d.Chosen.ToString();
            AiDecisionTrace.Record(npc, "Maneuver", chosen, legacy, d.Score.Net - d.Score.Threshold, d.Reason);
        }

        private static string F(float f) => f.ToString("0.##", CultureInfo.InvariantCulture);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DND35.AI
{
    /// <summary>
    /// Preferred combat distance/style for the AI profile.
    /// </summary>
    public enum CombatStyle
    {
        Melee,
        Ranged,
        Mixed
    }

    /// <summary>
    /// What an AI-run creature does when no target passes its attack guard (AiAttackGuard; batch 13 step 5, design
    /// choice C1, docs/systems/RULES_COVERAGE.md 4.1): the ordered list of AIProfile.GetOutOfReachAlternatives, tried by
    /// AiOutOfReach.Run through the action catalog until one is performed. Batch 14 re-weights the list; batch 22
    /// replaces it with the evaluator.
    /// </summary>
    public enum OutOfReachAlternative
    {
        /// <summary>Attack another target the guard allows (the catalog's attack or full attack entry).</summary>
        AttackOtherTarget,
        /// <summary>Move to the profile's preferred range of the focus target (Movement.PreferredRangeSquares; the move entry), then attack if a target is now allowed (AI-045).</summary>
        PreferredRange,
        /// <summary>Move to the reach or range of the weapon in hand from the focus target (the move entry), then attack if a target is now allowed (AI-045).</summary>
        Approach,
        /// <summary>Withdraw from the focus target (the withdraw entry; PHB p.143). In no default list.</summary>
        Withdraw,
        /// <summary>Total defense (the total_defense entry; PHB p.142, D2).</summary>
        TotalDefense
    }

    /// <summary>
    /// What an AI-run creature does with the move action left after a standard action that is not an attack (a cast, a
    /// song, a maintained spell, Lay on Hands, Turn Undead, the healer's cure or buff; batch 14 step 11, AI-077, design
    /// choice C8): AIProfile.ChooseAfterStandardAction, tilted by the traits' RoleDesire (AiBlend.AfterStandardAction). A
    /// turn is a standard and a move action in either order (PHB p.135, p.138, D1), and not moving is legal. An AI choice,
    /// not a rule; batch 22's evaluator replaces it.
    /// </summary>
    public enum AfterStandardActionChoice
    {
        /// <summary>Spend nothing more this turn.</summary>
        Stay,
        /// <summary>Move toward the focus target, to the reach or range of the weapon in hand (the move entry).</summary>
        Approach,
        /// <summary>Move away from the nearest foe (the move entry, its path priced by AiBlend.AoOCost).</summary>
        KeepDistance,
        /// <summary>A prone creature that wants to move stands up with its move action instead (PHB p.141-142).</summary>
        StandUp
    }

    /// <summary>
    /// The pure reads AIProfile.ChooseAfterStandardAction decides on (batch 14 step 11, C8). AIService builds it after the
    /// standard action; a unit test may fill it by hand.
    /// </summary>
    public struct AfterStandardActionContext
    {
        /// <summary>The creature may use its move action now (AiLegal.CanMoveNow; for a prone creature, the stand-up entry with a move action left).</summary>
        public bool CanMove;
        /// <summary>The creature is prone and not grappling, so any move it wants stands it up first (PHB p.141).</summary>
        public bool Prone;
        /// <summary>
        /// The creature chose at its turn start to stay prone (or crawled and stayed prone; AIProfile.ChooseProneStart, batch
        /// 14 step 13): a KeepDistance wish does not stand it up again (<see cref="AIProfile.StandFirstIfProne"/>), which
        /// would spend its move for no distance and give up the +4 AC against ranged attacks it stayed down for (PHB p.311).
        /// </summary>
        public bool StayingProne;
        /// <summary>The routine's focus target is alive, not within the reach or range of the weapon in hand, and not an undead the creature has turned (approaching within 10 feet would end the turning, PHB p.159).</summary>
        public bool FocusOutOfReach;
        /// <summary>A foe is within the keep-away distance (AiBlend.KeepAwayDistance of the kiter's 2 squares).</summary>
        public bool FoeWithinKeepAway;
        /// <summary>A foe threatens the creature's square (ThreatSystem.IsSquareThreatened).</summary>
        public bool Threatened;
        /// <summary>The creature can still fight from range after backing off (AIService.CanFightFromRange, AI-039).</summary>
        public bool CanFightFromRange;
        /// <summary>The Role's combat style (the routine's without a profile).</summary>
        public CombatStyle Style;
        /// <summary>The Role is a casting profile (AIProfile.IsCastingProfile).</summary>
        public bool CastingRole;

        /// <summary>Approach is a choice: the focus is out of reach.</summary>
        public bool CanApproach => CanMove && FocusOutOfReach;

        /// <summary>KeepDistance is a choice: a foe is close or threatens the creature.</summary>
        public bool CanKeepDistance => CanMove && (FoeWithinKeepAway || Threatened);
    }

    /// <summary>
    /// What a Healer-Role creature does when its cure found no target in reach (batch 14 step 12, AI-047, AI-063; design
    /// choice C8, owner question Q8): AIProfile.ChooseHealerApproach, tilted by the traits' RoleDesire
    /// (AiBlend.HealerApproach). A turn is a standard and a move action in either order (PHB p.135, p.138, D1), so the
    /// healer may move beside its patient and touch it with the cure in the same turn (a touch spell's range, PHB p.175). An
    /// AI choice, not a rule; batch 22's evaluator replaces it.
    /// </summary>
    public enum HealerApproachChoice
    {
        /// <summary>Today's turn: the spell walk over every castable spell, then the ranged kiter.</summary>
        Kiter,
        /// <summary>A 5-foot step or a move beside the patient (HealerApproachContext.TouchSquare), then the cure.</summary>
        MoveAndCast,
        /// <summary>A move toward a patient no move reaches this turn (HealerApproachContext.PartialSquare), then the kiter's standard action.</summary>
        PartialApproach
    }

    /// <summary>
    /// The pure reads AIProfile.ChooseHealerApproach decides on (batch 14 step 12, C8). AIService builds it after the cure
    /// found no target (AIService.BuildHealerApproachContext); a unit test may fill it by hand.
    /// </summary>
    public struct HealerApproachContext
    {
        /// <summary>The healer's cure has a patient other than itself (HealerAIProfile.GetPriorityHealTarget).</summary>
        public bool HasPatient;
        /// <summary>The patient is at or below the Role's critical threshold (HealerAIProfile.CriticalHealingThreshold, 25%).</summary>
        public bool PatientCritical;
        /// <summary>The healer's distance to the patient in squares, before any move.</summary>
        public int Distance;
        /// <summary>
        /// The price per provoker of a path that provokes (C6, Q8): the Role's (1000 for a Healer, which avoids AoOs; at
        /// most 6 for a critical patient), then the traits' ProvokeAoO vote (AiBlend.AoOCost). At 1000 or more no
        /// approach provokes.
        /// </summary>
        public float AoOPrice;
        /// <summary>A square beside the patient the healer reaches this turn within the AoO price, or null.</summary>
        public Vector2Int? TouchSquare;
        /// <summary>TouchSquare is reached by a 5-foot step (PHB p.144: it provokes nothing), not by a move action.</summary>
        public bool TouchByFiveFootStep;
        /// <summary>When no square beside the patient is reached: a move's destination closer to it, or null.</summary>
        public Vector2Int? PartialSquare;

        /// <summary>MoveAndCast is a choice: a square beside the patient is reached this turn.</summary>
        public bool CanMoveAndCast => HasPatient && TouchSquare.HasValue;

        /// <summary>PartialApproach is a choice: a move brings the healer closer to the patient.</summary>
        public bool CanApproachPartly => HasPatient && PartialSquare.HasValue;
    }

    /// <summary>
    /// What an AI-run creature does with the standard action of a melee turn that has a target in reach (batch 14 step 13,
    /// AI-063, batch 13's C5, design choice C8): AIProfile.ChooseMeleeTurnOption, tilted by the traits' RoleDesire
    /// (AiBlend.MeleeTurn). Aid another is a standard action, an attack roll against AC 10 for +2 on the friend's next
    /// attack against the opponent or +2 AC against the opponent's next attack (PHB p.154, D3); total defense is a standard
    /// action for +4 dodge AC for a round and no attacks of opportunity (PHB p.142, D4). An AI choice, not a rule; batch
    /// 22's evaluator replaces it.
    /// </summary>
    public enum MeleeTurnOption
    {
        /// <summary>The routine's attack (or full attack), each step open to a maneuver (AiManeuverChooser).</summary>
        Attack,
        /// <summary>Aid another: +2 on an ally's next attack against the opponent (the aid-another entry, variant offense).</summary>
        AidOffense,
        /// <summary>Aid another: +2 AC for an ally against the opponent's next attack (the aid-another entry, variant defense).</summary>
        AidDefense,
        /// <summary>Total defense (the total-defense entry).</summary>
        TotalDefense
    }

    /// <summary>
    /// The pure reads AIProfile.ChooseMeleeTurnOption decides on (batch 14 step 13, C8), in expected hit points for the
    /// round (DND35.AI.Valuation.TurnOptionValue). AIService builds it before the melee attack (AIService.BuildMeleeTurnOptionContext);
    /// a unit test may fill it by hand. An alternative is open only when its catalog entry is legal now and its targets pass
    /// the entry's own check (GameManager.GetAidAnotherTargetRefusal).
    /// </summary>
    public struct MeleeTurnOptionContext
    {
        /// <summary>S: the attack's value (the full attack's expected damage when the full-round action is free, else one attack's).</summary>
        public float AttackValue;
        /// <summary>The best aid offense (ally and opponent below): aid roll x the ally's gain in hit chance from +2 x its hit damage.</summary>
        public float AidOffenseValue;
        public bool CanAidOffense;
        public CharacterController AidOffenseAlly;
        public CharacterController AidOffenseOpponent;
        /// <summary>The best aid defense: aid roll x the opponent's lost hits on the ally at +2 AC x its hit damage x its share on the ally.</summary>
        public float AidDefenseValue;
        public bool CanAidDefense;
        public CharacterController AidDefenseAlly;
        public CharacterController AidDefenseOpponent;
        /// <summary>The foes' next-round damage on the creature that +4 AC takes away (0 when no foe can reach it).</summary>
        public float TotalDefenseValue;
        /// <summary>The total-defense entry is legal now.</summary>
        public bool CanTotalDefense;
        /// <summary>A foe can attack the creature next round (TurnOptionValue.CanReach: it threatens it, a move brings it into reach, or a ranged weapon covers it).</summary>
        public bool FoeCanReach;

        /// <summary>The margin an alternative must beat the attack by: max(0.5, 0.15 S) (starter constant).</summary>
        public float Margin => Mathf.Max(0.5f, 0.15f * Mathf.Max(0f, AttackValue));

        /// <summary>Whether <paramref name="option"/> may be chosen (the attack always; total defense only when a foe can reach the creature).</summary>
        public bool IsOpen(MeleeTurnOption option)
        {
            switch (option)
            {
                case MeleeTurnOption.Attack: return true;
                case MeleeTurnOption.AidOffense: return CanAidOffense;
                case MeleeTurnOption.AidDefense: return CanAidDefense;
                case MeleeTurnOption.TotalDefense: return CanTotalDefense && FoeCanReach;
                default: return false;
            }
        }

        /// <summary>The value of <paramref name="option"/> in expected hit points.</summary>
        public float ValueOf(MeleeTurnOption option)
        {
            switch (option)
            {
                case MeleeTurnOption.Attack: return AttackValue;
                case MeleeTurnOption.AidOffense: return AidOffenseValue;
                case MeleeTurnOption.AidDefense: return AidDefenseValue;
                case MeleeTurnOption.TotalDefense: return FoeCanReach ? TotalDefenseValue : 0f;
                default: return 0f;
            }
        }
    }

    /// <summary>
    /// What a prone AI-run creature does at the start of its turn (batch 14 step 13, D5, design choice C8): standing up is
    /// a move action that provokes (PHB p.141, Table 8-2), crawling 5 feet is a move action that provokes from every
    /// attacker threatening any point of it (PHB p.142), and prone is -4 on melee attacks, no ranged weapon but a crossbow,
    /// +4 AC against ranged attacks and -4 against melee (PHB p.311). Nothing makes a prone creature stand: the old
    /// unconditional stand was an AI choice. AIProfile.ChooseProneStart; an AI choice, not a rule.
    /// </summary>
    public enum ProneStartChoice
    {
        /// <summary>Stand up with the move action (the stand-up entry), as before batch 14 step 13.</summary>
        StandUp,
        /// <summary>Stay down and act from the ground.</summary>
        StayProne,
        /// <summary>Crawl 5 feet to ProneStartContext.CrawlSquare (the crawl entry), then act from the ground.</summary>
        Crawl
    }

    /// <summary>The pure reads AIProfile.ChooseProneStart decides on (batch 14 step 13, C8). AIService.BuildProneStartContext builds it; a test may fill it by hand.</summary>
    public struct ProneStartContext
    {
        /// <summary>A foe threatens a square of the creature's space (ThreatSystem.IsSquareThreatened): prone, it takes -4 AC against those melee attacks.</summary>
        public bool Threatened;
        /// <summary>The creature's turn plan works prone (AIService.PlannedActionWorksProne): a Ranged Role with a crossbow it can shoot, or a non-Melee casting Role whose best spell reaches the nearest foe from the ground and that holds no other ranged or throwable weapon, a Healer only on an OffensiveSpell turn (PHB p.311).</summary>
        public bool PlannedActionWorksProne;
        /// <summary>A foe in view fights with a ranged weapon (the WieldsRanged fact), against which prone gives +4 AC (PHB p.311).</summary>
        public bool RangedFoeInView;
        /// <summary>A foe could threaten the creature's square next turn (TurnOptionValue.CanReach).</summary>
        public bool SquareReachableNextTurn;
        /// <summary>The crawl entry is legal now (prone, a move action, no 5-foot step taken).</summary>
        public bool CanCrawl;
        /// <summary>An adjacent square the creature may crawl to that no foe threatens now or could threaten next turn, or null.</summary>
        public Vector2Int? CrawlSquare;
    }

    /// <summary>
    /// Grapple posture for the AI profile.
    /// </summary>
    public enum GrappleBehavior
    {
        Avoid,
        EscapeOnly,
        InitiateWhenSafe,
        Aggressive,
        Maintain
    }

    /// <summary>
    /// Ordered/weighted target tag preference.
    /// </summary>
    [Serializable]
    public class TagPriority
    {
        [Tooltip("Target fact to test, parsed by DND35.AI.Knowledge.FactQuery and matched whole (e.g. 'Race: Elf', 'Armor: Light Armor', 'HP State: Dying', 'Unarmored')")]
        public string TagName;

        [Tooltip("Weight for this tag. Higher value = stronger impact.")]
        public float Priority = 1f;

        [Tooltip("If true, matching this tag reduces score instead of increasing it.")]
        public bool IsPenalty;

        public TagPriority() { }

        public TagPriority(string tagName, float priority, bool isPenalty = false)
        {
            TagName = tagName;
            Priority = priority;
            IsPenalty = isPenalty;
        }
    }

    /// <summary>
    /// Preferences for maneuver usage.
    /// </summary>
    [Serializable]
    public class ManeuverPreferences
    {
        public bool AttemptTrip;
        public bool AttemptDisarm;
        public bool AttemptBullRush;
        public bool AttemptSunder;
        public bool AttemptOverrun;

        public bool UseAidAnother;
        public bool UseCombatExpertise;
        public bool UsePowerAttack;
    }

    /// <summary>
    /// Preferences for movement/pathing.
    /// </summary>
    [Serializable]
    public class MovementPreferences
    {
        [Tooltip("If true, pathing will strongly prefer non-provoking movement paths.")]
        public bool AvoidAoOs = true;

        [Tooltip("Preferred distance in squares from primary target (0 = adjacent/melee)")]
        [Min(0)]
        public int PreferredRangeSquares = 0;

        [Tooltip("Try to preserve preferred range by backing away when too close.")]
        public bool MaintainDistance;

        [Tooltip("Prefer movement choices that create flanking.")]
        public bool SeekFlanking = true;

        [Tooltip("Reserved flag for future tactical cover support.")]
        public bool UseCover = true;
    }
}

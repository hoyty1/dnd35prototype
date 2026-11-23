using System;
using System.Collections.Generic;
using DND35.AI.Knowledge;

namespace DND35.AI.Personality
{
    /// <summary>
    /// The target considerations a trait weighs (batch 14 step 3; docs/designs/personalities_batch14.md "Considerations").
    /// Each is a value in [0, 1] read from an ITargetView by <see cref="Considerations.Evaluate"/>. Appended only: the
    /// values index <see cref="PersonalityProfile"/>'s folded weights.
    /// </summary>
    public enum ConsiderationId
    {
        WoundSeverity,
        IsDown,
        Helpless,
        Softness,
        KillChance,
        KnownCaster,
        KnownHealer,
        Proximity,
        ThreatensMe,
        EngagedByAlly,
        FlankOpportunity,
        Clustered,
        LooksDangerous,
        Fleeing,
        ThreatToWard,
        HurtMe
    }

    /// <summary>A trait's opinion on a yes/no decision (design 4.1): Forbid and Insist count -2 and +2 in the vote.</summary>
    public enum Opinion : sbyte
    {
        Forbid = -2,
        Against = -1,
        Neutral = 0,
        For = 1,
        Insist = 2
    }

    /// <summary>
    /// The yes/no decisions traits vote on in batch 14 (C5). Design 4.4's IgnoreAoO and ProvokeAoO opinions are one
    /// decision here (AIService.EvaluateMovementOptions reads one flag). Withdraw waits for batch 18.
    /// </summary>
    public enum BoolDecision
    {
        /// <summary>Take a coup de grace on a helpless foe (PHB p.153) when the rules allow it.</summary>
        CoupDeGrace,
        /// <summary>Skip downed foes as targets (yes) or keep them valid (no).</summary>
        IgnoreUnconscious,
        /// <summary>Charge when a charge is legal (PHB p.154).</summary>
        Charge,
        /// <summary>Accept a path that provokes attacks of opportunity (PHB p.137).</summary>
        ProvokeAoO
    }

    /// <summary>
    /// The closed set of conditions an opinion row can carry (design 4.1: ids with a parameter, not lambdas), each read
    /// from <see cref="JudgeContext"/>.
    /// </summary>
    public enum OpinionCondition
    {
        Always,
        /// <summary>The creature's own wound band is at least the row's band (Hurt, BadlyHurt, Down).</summary>
        SelfWoundAtLeast,
        /// <summary>No foe threatens the creature's space.</summary>
        NoEnemyThreatensSelf,
        /// <summary>A standing foe is in the creature's reach.</summary>
        StandingThreatInReach
    }

    /// <summary>The alignment a trait hints at; a mismatch only warns (owner, 2026-10-30).</summary>
    public enum AlignmentHint
    {
        None,
        Good,
        Evil,
        Lawful,
        Chaotic
    }

    /// <summary>One weighted consideration of a trait's target term.</summary>
    public readonly struct TargetWeight
    {
        public readonly ConsiderationId Consideration;
        public readonly float Weight;

        public TargetWeight(ConsiderationId consideration, float weight)
        {
            Consideration = consideration;
            Weight = weight;
        }
    }

    /// <summary>How much a trait wants actions of one role (C3's RoleDesire column), in [-1, 1].</summary>
    public readonly struct RoleDesire
    {
        /// <summary>A single ActionRoles flag (Attack, Approach, Retreat, Defend, Support or Heal).</summary>
        public readonly ActionRoles Role;
        public readonly float Desire;

        public RoleDesire(ActionRoles role, float desire)
        {
            Role = role;
            Desire = desire;
        }
    }

    /// <summary>
    /// One opinion of a trait (C5): on <see cref="Decision"/>, <see cref="WhenTrue"/> while <see cref="Condition"/> holds,
    /// else <see cref="Otherwise"/>. A row with a <see cref="MinDifficulty"/> is skipped below it (C11: Sadistic's coup de
    /// grace Insist needs Normal; Easy skips it). Set once through its constructor and initializer, read-only afterwards
    /// (the TraitDatabase rows are shared).
    /// </summary>
    public sealed class OpinionRule
    {
        public BoolDecision Decision { get; }
        public Opinion WhenTrue { get; }
        public OpinionCondition Condition { get; init; } = OpinionCondition.Always;
        /// <summary>The band of <see cref="OpinionCondition.SelfWoundAtLeast"/>.</summary>
        public WoundBand Band { get; init; } = WoundBand.Hurt;
        public Opinion Otherwise { get; init; } = Opinion.Neutral;
        public AIService.AIDifficultyLevel? MinDifficulty { get; init; }

        public OpinionRule(BoolDecision decision, Opinion opinion)
        {
            Decision = decision;
            WhenTrue = opinion;
        }

        /// <summary>This row's opinion in <paramref name="ctx"/>: Neutral when its difficulty mark is not met.</summary>
        public Opinion Evaluate(in JudgeContext ctx)
        {
            if (MinDifficulty.HasValue && ctx.EffectiveDifficulty < MinDifficulty.Value)
                return Opinion.Neutral;
            return Holds(ctx) ? WhenTrue : Otherwise;
        }

        private bool Holds(in JudgeContext ctx)
        {
            switch (Condition)
            {
                case OpinionCondition.SelfWoundAtLeast:
                    return ctx.SelfWound >= Band;
                case OpinionCondition.NoEnemyThreatensSelf:
                    return !ctx.EnemyThreatensSelf;
                case OpinionCondition.StandingThreatInReach:
                    return ctx.StandingThreatInReach;
                default:
                    return true;
            }
        }

        public override string ToString()
        {
            string s = Decision + " " + WhenTrue;
            if (Condition != OpinionCondition.Always)
                s += " if " + Condition + (Condition == OpinionCondition.SelfWoundAtLeast ? "(" + Band + ")" : "")
                     + (Otherwise != Opinion.Neutral ? ", else " + Otherwise : "");
            if (MinDifficulty.HasValue)
                s += " (" + MinDifficulty.Value + "+)";
            return s;
        }
    }

    /// <summary>
    /// The creature's own state a yes/no vote reads (C5's conditions) and the difficulty (C11). The caller fills it from the
    /// creature's own state; nothing here reads a target.
    /// </summary>
    public struct JudgeContext
    {
        /// <summary>The creature's own wound band.</summary>
        public WoundBand SelfWound;
        /// <summary>A foe threatens the creature's space.</summary>
        public bool EnemyThreatensSelf;
        /// <summary>A standing foe is in the creature's reach.</summary>
        public bool StandingThreatInReach;
        /// <summary>
        /// The AI difficulty. AiBlend fills a null one from <see cref="AiBlend.SettingsDifficulty"/> (the game setting;
        /// Normal until step 4 wires AIService.Difficulty), so a caller that leaves it unset still honours Easy. Read
        /// directly by PersonalityProfile.Judge, a null is Normal.
        /// </summary>
        public AIService.AIDifficultyLevel? Difficulty;

        public AIService.AIDifficultyLevel EffectiveDifficulty => Difficulty ?? AIService.AIDifficultyLevel.Normal;
    }

    /// <summary>
    /// One personality trait (batch 14 step 3; design 4.1 and C3): plain data, one shared instance per id in
    /// <see cref="TraitDatabase"/>. Every member is set once through an object initializer and is read-only afterwards
    /// (the arrays are copied into read-only lists), so no caller can change the shared row (the database-template rule
    /// of CLAUDE.md). It holds only what batch 14 reads: the target weights, the opinions, the minimum tier,
    /// RiskAversion, the preferred range adjustment and the RoleDesire column. Cruelty, MoraleBreak, Discipline,
    /// Persistence and the Withdraw opinions wait for batch 18 (AI-026).
    /// </summary>
    public sealed class TraitDefinition
    {
        private IReadOnlyList<TargetWeight> _target = Array.Empty<TargetWeight>();
        private IReadOnlyList<OpinionRule> _opinions = Array.Empty<OpinionRule>();
        private IReadOnlyList<RoleDesire> _roleDesires = Array.Empty<RoleDesire>();

        public string Id { get; init; }
        public CognitionTier MinTier { get; init; }
        public AlignmentHint Alignment { get; init; }
        /// <summary>The target term's weights (design 4.4); their absolute values sum to 1.</summary>
        public IReadOnlyList<TargetWeight> Target { get => _target; init => _target = Freeze(value); }
        public IReadOnlyList<OpinionRule> Opinions { get => _opinions; init => _opinions = Freeze(value); }
        /// <summary>RiskAversion: 0.5 is neutral (rho 1.0, C7); may lie outside [0, 1] (Berserker -.3), the lerp clamps it.</summary>
        public float RiskAversion { get; init; } = 0.5f;
        /// <summary>Squares added to the Role's preferred range (never averaged; design 4.2).</summary>
        public int PreferredRangeAdjust { get; init; }
        /// <summary>True when <see cref="PreferredRangeAdjust"/> applies only to a Ranged, Mixed or casting Role (Cowardly).</summary>
        public bool RangeAdjustOnlyRangedOrCasting { get; init; }
        /// <summary>Each a single ActionRoles flag (TraitDatabase reports any other; PersonalityProfile.IsSingleRole).</summary>
        public IReadOnlyList<RoleDesire> RoleDesires { get => _roleDesires; init => _roleDesires = Freeze(value); }
        /// <summary>Mindless: removes every other trait (design 4.1).</summary>
        public bool ExclusiveLock { get; init; }

        /// <summary>This trait's opinion on <paramref name="decision"/> in <paramref name="ctx"/>: its first row for the decision, or Neutral.</summary>
        public Opinion OpinionOn(BoolDecision decision, in JudgeContext ctx)
        {
            for (int i = 0; i < _opinions.Count; i++)
                if (_opinions[i] != null && _opinions[i].Decision == decision)
                    return _opinions[i].Evaluate(ctx);
            return Opinion.Neutral;
        }

        /// <summary>This trait's desire for <paramref name="role"/> (one flag), 0 when it names none.</summary>
        public float DesireFor(ActionRoles role)
        {
            for (int i = 0; i < _roleDesires.Count; i++)
                if (_roleDesires[i].Role == role)
                    return _roleDesires[i].Desire;
            return 0f;
        }

        /// <summary>The weight of <paramref name="id"/> in the target term, 0 when absent.</summary>
        public float TargetWeightOf(ConsiderationId id)
        {
            for (int i = 0; i < _target.Count; i++)
                if (_target[i].Consideration == id)
                    return _target[i].Weight;
            return 0f;
        }

        public override string ToString() => Id;

        /// <summary>A read-only copy of <paramref name="items"/> (empty for null), so the given array cannot change the row later.</summary>
        private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0)
                return Array.Empty<T>();
            var copy = new T[items.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = items[i];
            return Array.AsReadOnly(copy);
        }
    }
}

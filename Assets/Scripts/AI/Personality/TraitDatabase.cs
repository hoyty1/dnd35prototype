using System;
using System.Collections.Generic;
using DND35.AI.Knowledge;

namespace DND35.AI.Personality
{
    /// <summary>
    /// The seven starter traits (batch 14 step 3; docs/designs/personalities_batch14.md "The seven starter traits", the
    /// target weights and opinions of design 4.4 verbatim; RoleDesire new in batch 14). AI tuning data, not rules: a
    /// trait only chooses among actions the rules cores allow. Ids match case-insensitively. The rows are shared and
    /// read-only (TraitDefinition's members are init-only, its lists and <see cref="All"/> read-only wrappers;
    /// TraitDatabaseTests pins every number). Recipe: docs/DEVELOPMENT_RECIPES.md "Add a trait".
    /// </summary>
    public static class TraitDatabase
    {
        public const string Sadistic = "Sadistic";
        public const string Cowardly = "Cowardly";
        public const string Smart = "Smart";
        public const string Caster = "Caster";
        public const string Healer = "Healer";
        public const string Berserker = "Berserker";
        public const string Mindless = "Mindless";

        private static Dictionary<string, TraitDefinition> _byId;
        private static IReadOnlyList<TraitDefinition> _all;

        /// <summary>Every trait, in table order.</summary>
        public static IReadOnlyList<TraitDefinition> All
        {
            get
            {
                Init();
                return _all;
            }
        }

        /// <summary>The trait named <paramref name="id"/> (case-insensitive), or null.</summary>
        public static TraitDefinition Get(string id)
        {
            Init();
            if (string.IsNullOrWhiteSpace(id))
                return null;
            return _byId.TryGetValue(id.Trim(), out TraitDefinition t) ? t : null;
        }

        private static void Init()
        {
            if (_all != null)
                return;
            var all = new List<TraitDefinition>
            {
                new TraitDefinition
                {
                    Id = Sadistic,
                    MinTier = CognitionTier.Low,
                    Alignment = AlignmentHint.Evil,
                    RiskAversion = 0.3f,
                    Target = new[]
                    {
                        W(ConsiderationId.WoundSeverity, 0.3f),
                        W(ConsiderationId.IsDown, 0.3f),
                        W(ConsiderationId.Helpless, 0.2f),
                        W(ConsiderationId.Softness, 0.1f),
                        W(ConsiderationId.Fleeing, 0.1f)
                    },
                    Opinions = new[]
                    {
                        // Owner, 2026-10-30: a sadistic coup de grace at Normal difficulty and above, never on Easy.
                        new OpinionRule(BoolDecision.CoupDeGrace, Opinion.Insist) { MinDifficulty = AIService.AIDifficultyLevel.Normal },
                        new OpinionRule(BoolDecision.IgnoreUnconscious, Opinion.Forbid)
                    },
                    RoleDesires = new[] { D(ActionRoles.Attack, 0.4f), D(ActionRoles.Retreat, -0.4f) }
                },
                new TraitDefinition
                {
                    Id = Cowardly,
                    MinTier = CognitionTier.Animal,
                    RiskAversion = 1.0f,
                    PreferredRangeAdjust = 2,
                    RangeAdjustOnlyRangedOrCasting = true,
                    Target = new[]
                    {
                        W(ConsiderationId.ThreatensMe, -0.3f),
                        W(ConsiderationId.EngagedByAlly, 0.3f),
                        W(ConsiderationId.LooksDangerous, -0.2f),
                        W(ConsiderationId.Proximity, -0.2f)
                    },
                    Opinions = new[]
                    {
                        new OpinionRule(BoolDecision.Charge, Opinion.Forbid),
                        new OpinionRule(BoolDecision.ProvokeAoO, Opinion.Forbid)
                        {
                            Condition = OpinionCondition.SelfWoundAtLeast,
                            Band = WoundBand.Hurt,
                            Otherwise = Opinion.Against
                        }
                    },
                    RoleDesires = new[]
                    {
                        D(ActionRoles.Retreat, 0.8f), D(ActionRoles.Defend, 0.5f), D(ActionRoles.Approach, -0.5f), D(ActionRoles.Attack, -0.2f)
                    }
                },
                new TraitDefinition
                {
                    Id = Smart,
                    MinTier = CognitionTier.Average,
                    RiskAversion = 0.6f,
                    Target = new[]
                    {
                        W(ConsiderationId.KillChance, 0.45f),
                        W(ConsiderationId.KnownHealer, 0.15f),
                        W(ConsiderationId.KnownCaster, 0.15f),
                        W(ConsiderationId.FlankOpportunity, 0.15f),
                        W(ConsiderationId.IsDown, -0.1f)
                    },
                    Opinions = new[]
                    {
                        new OpinionRule(BoolDecision.IgnoreUnconscious, Opinion.For) { Condition = OpinionCondition.StandingThreatInReach },
                        new OpinionRule(BoolDecision.CoupDeGrace, Opinion.For)
                        {
                            Condition = OpinionCondition.NoEnemyThreatensSelf,
                            Otherwise = Opinion.Against
                        },
                        new OpinionRule(BoolDecision.ProvokeAoO, Opinion.Against)
                    }
                },
                new TraitDefinition
                {
                    Id = Caster,
                    MinTier = CognitionTier.Low,
                    NeedsSpells = true,
                    RiskAversion = 0.7f,
                    Target = new[]
                    {
                        W(ConsiderationId.Clustered, 0.4f),
                        W(ConsiderationId.KnownCaster, 0.3f),
                        W(ConsiderationId.ThreatensMe, -0.3f)
                    },
                    Opinions = new[]
                    {
                        new OpinionRule(BoolDecision.Charge, Opinion.Forbid),
                        new OpinionRule(BoolDecision.ProvokeAoO, Opinion.Against)
                    },
                    RoleDesires = new[] { D(ActionRoles.Retreat, 0.4f), D(ActionRoles.Approach, -0.2f), D(ActionRoles.Support, 0.2f) }
                },
                new TraitDefinition
                {
                    Id = Healer,
                    MinTier = CognitionTier.Low,
                    NeedsSpells = true,
                    RiskAversion = 0.3f,
                    Target = new[]
                    {
                        W(ConsiderationId.ThreatToWard, 0.4f),
                        W(ConsiderationId.EngagedByAlly, 0.3f),
                        W(ConsiderationId.ThreatensMe, -0.3f)
                    },
                    Opinions = new[]
                    {
                        new OpinionRule(BoolDecision.CoupDeGrace, Opinion.Against),
                        new OpinionRule(BoolDecision.Charge, Opinion.Against)
                    },
                    RoleDesires = new[] { D(ActionRoles.Heal, 0.8f), D(ActionRoles.Support, 0.6f), D(ActionRoles.Defend, 0.1f) }
                },
                new TraitDefinition
                {
                    Id = Berserker,
                    MinTier = CognitionTier.Animal,
                    RiskAversion = -0.3f,
                    Target = new[]
                    {
                        W(ConsiderationId.Proximity, 0.4f),
                        W(ConsiderationId.HurtMe, 0.4f),
                        W(ConsiderationId.LooksDangerous, 0.2f)
                    },
                    Opinions = new[]
                    {
                        new OpinionRule(BoolDecision.Charge, Opinion.Insist),
                        new OpinionRule(BoolDecision.ProvokeAoO, Opinion.For)
                    },
                    RoleDesires = new[]
                    {
                        D(ActionRoles.Attack, 0.6f), D(ActionRoles.Approach, 0.6f), D(ActionRoles.Retreat, -0.8f), D(ActionRoles.Defend, -0.6f)
                    }
                },
                new TraitDefinition
                {
                    Id = Mindless,
                    MinTier = CognitionTier.Mindless,
                    RiskAversion = 0f,
                    ExclusiveLock = true,
                    Target = new[] { W(ConsiderationId.Proximity, 1.0f) }
                }
            };

            var byId = new Dictionary<string, TraitDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (TraitDefinition t in all)
            {
                byId[t.Id] = t;
                // A RoleDesire on a combined or unknown flag would be dropped silently by PersonalityProfile.
                foreach (RoleDesire d in t.RoleDesires)
                    if (!PersonalityProfile.IsSingleRole(d.Role))
                        UnityEngine.Debug.LogError("[Personality] trait " + t.Id + ": RoleDesire " + d.Role
                                                   + " is not one ActionRoles flag; PersonalityProfile ignores it");
            }
            _byId = byId;
            _all = all.AsReadOnly();
        }

        private static TargetWeight W(ConsiderationId id, float weight) => new TargetWeight(id, weight);

        private static RoleDesire D(ActionRoles role, float desire) => new RoleDesire(role, desire);
    }
}

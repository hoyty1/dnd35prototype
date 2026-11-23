using System;
using System.Collections.Generic;

namespace DND35.AI.Knowledge
{
    /// <summary>What <see cref="ITargetView.Observer"/> knows about <see cref="ITargetView.Subject"/>, one fact at a time.</summary>
    public interface ITargetView
    {
        CharacterController Observer { get; }
        CharacterController Subject { get; }

        /// <summary>A single-valued fact (every key but Subtype and Condition); <see cref="FactValue.Unknown"/> when not known.</summary>
        FactValue Get(FactKey key);

        /// <summary>
        /// True when the fact <paramref name="key"/> is known to have the value <paramref name="value"/>, matched whole and
        /// case-insensitively (a multi-valued key holds it among its values). Never a substring match (AI-078).
        /// </summary>
        bool Has(FactKey key, string value);
    }

    /// <summary>Gives an observer's view of a subject. Batch 17 adds a provider that gates facts by perception and checks.</summary>
    public interface IKnowledgeProvider
    {
        ITargetView GetView(CharacterController observer, CharacterController subject);
    }

    /// <summary>
    /// The AI's knowledge source (batch 13 step 7). Every AI target-scoring term that reads another creature's facts asks
    /// <see cref="Provider"/> for a view. Today that is <see cref="OmniscientKnowledgeProvider"/> (the truth, at
    /// Confirmed); batch 17 swaps in a provider gated by what the observer perceives and recalls, and batch 14 scores the
    /// facts (docs/systems/AI.md). A null assignment restores the omniscient default.
    /// </summary>
    public static class KnowledgeService
    {
        private static IKnowledgeProvider _provider = OmniscientKnowledgeProvider.Instance;

        public static IKnowledgeProvider Provider
        {
            get => _provider;
            set => _provider = value ?? OmniscientKnowledgeProvider.Instance;
        }

        /// <summary>The current provider's view, or null when either creature is missing.</summary>
        public static ITargetView ViewOf(CharacterController observer, CharacterController subject)
            => subject == null ? null : Provider.GetView(observer, subject);
    }

    /// <summary>
    /// The truth about every creature, at <see cref="KnowledgeLevel.Confirmed"/>: the provider until batch 17. Facts are
    /// computed when asked, from the subject's live state (stats, hands, armor slot, conditions) and, for the relational
    /// facts of batch 14 step 2 (Distance to ThreatPerRound), the observer's state and the fight's creatures, so a view
    /// never goes stale and asking for one fact costs nothing for the others. A view reads the fight's creatures once
    /// (on its first relational fact) and keeps that list: take a view per decision and never keep one across turns.
    /// </summary>
    public sealed class OmniscientKnowledgeProvider : IKnowledgeProvider
    {
        public static readonly OmniscientKnowledgeProvider Instance = new OmniscientKnowledgeProvider();

        private readonly Func<List<CharacterController>> _roster;

        /// <summary>
        /// The omniscient provider. <paramref name="roster"/> gives the creatures of the fight for the relational facts
        /// (ThreatensAllyInNeed, EngagedByAlly, FoesNear, FlankingNow); null reads the scene's combatants
        /// (GameManager.GetAllCharactersForAI), and none without a GameManager. A static suite passes its own creatures.
        /// </summary>
        public OmniscientKnowledgeProvider(Func<List<CharacterController>> roster = null)
        {
            _roster = roster;
        }

        public ITargetView GetView(CharacterController observer, CharacterController subject) => new TruthView(this, observer, subject);

        private List<CharacterController> Roster()
        {
            if (_roster != null)
                return _roster() ?? new List<CharacterController>();
            GameManager gm = GameManager.Instance;
            return gm != null ? gm.GetAllCharactersForAI() : new List<CharacterController>();
        }

        private sealed class TruthView : ITargetView
        {
            private const KnowledgeLevel L = KnowledgeLevel.Confirmed;

            private readonly OmniscientKnowledgeProvider _provider;
            private List<CharacterController> _roster;

            /// <summary>The fight's creatures, read once per view (a view lives for one decision).</summary>
            private List<CharacterController> Roster => _roster ??= _provider.Roster();

            public CharacterController Observer { get; }
            public CharacterController Subject { get; }

            public TruthView(OmniscientKnowledgeProvider provider, CharacterController observer, CharacterController subject)
            {
                _provider = provider;
                Observer = observer;
                Subject = subject;
            }

            public FactValue Get(FactKey key)
            {
                CharacterController s = Subject;
                if (s == null || s.Stats == null)
                    return FactValue.Unknown;
                CharacterStats st = s.Stats;
                switch (key)
                {
                    case FactKey.Species:
                        return FactValue.Of(L, s.DisplayedRace);
                    case FactKey.CreatureType:
                        return KnowledgeFacts.TryCreatureTypeOf(st, out CreatureTypeId type)
                            ? FactValue.Of(L, (int)type, KnowledgeFacts.CreatureTypeName(type))
                            : FactValue.Unknown;
                    case FactKey.Size:
                    {
                        SizeCategory size = s.GetCurrentSizeCategory();
                        return FactValue.Of(L, (int)size, size.ToString());
                    }
                    case FactKey.ArmorCategory:
                    {
                        WornArmor armor = KnowledgeFacts.WornArmorOf(s);
                        return FactValue.Of(L, (int)armor, armor.ToString());
                    }
                    case FactKey.ShieldCarried:
                        return FactValue.Of(L, KnowledgeFacts.HandsHold(s, KnowledgeFacts.IsShield));
                    case FactKey.WieldsMelee:
                        return FactValue.Of(L, KnowledgeFacts.HandsHold(s, KnowledgeFacts.IsMeleeWeapon));
                    case FactKey.WieldsRanged:
                        return FactValue.Of(L, KnowledgeFacts.HandsHold(s, KnowledgeFacts.IsRangedWeapon));
                    case FactKey.WieldsReach:
                        return FactValue.Of(L, KnowledgeFacts.HandsHold(s, KnowledgeFacts.IsReachWeapon));
                    case FactKey.WoundBand:
                    {
                        WoundBand band = KnowledgeFacts.WoundBandOf(st.CurrentHP, st.TotalMaxHP);
                        return FactValue.Of(L, (int)band, band.ToString());
                    }
                    case FactKey.HpState:
                        return FactValue.Of(L, (int)s.CurrentHPState, s.CurrentHPState.ToString());
                    case FactKey.Caster:
                        return FactValue.Of(L, s.Spellcasting != null || st.IsSpellcaster);
                    case FactKey.Mindless:
                        return FactValue.Of(L, st.IsMindless);
                    // Batch 14 step 2: the facts of the personality considerations.
                    case FactKey.Down:
                        return FactValue.Of(L, s.IsUnconscious);
                    case FactKey.Helpless:
                        return FactValue.Of(L, s.IsHelplessForCoupDeGrace());
                    case FactKey.Fleeing:
                        return FactValue.Of(L, KnowledgeFacts.IsFleeing(s));
                    case FactKey.Healer:
                        return FactValue.Of(L, KnowledgeFacts.IsHealer(s));
                    case FactKey.AcEstimate:
                        return FactValue.OfNumber(L, st.ArmorClass);
                    case FactKey.TouchAcEstimate:
                        return FactValue.OfNumber(L, st.TouchArmorClass);
                    case FactKey.HpCurrent:
                        return FactValue.OfNumber(L, st.CurrentHP);
                    case FactKey.HpMax:
                        return FactValue.OfNumber(L, st.TotalMaxHP);
                    default:
                        return GetRelational(key, s); // Subtype and Condition hold several values: Has
                }
            }

            /// <summary>The facts about the subject relative to the observer: Unknown without an observer.</summary>
            private FactValue GetRelational(FactKey key, CharacterController s)
            {
                CharacterController o = Observer;
                if (o == null || o.Stats == null)
                    return FactValue.Unknown;
                switch (key)
                {
                    case FactKey.Distance:
                        return FactValue.OfNumber(L, o.GetMinimumDistanceToTarget(s, chebyshev: false));
                    case FactKey.ThreatensObserver:
                        return FactValue.Of(L, s.Team != o.Team && KnowledgeFacts.ThreatensSpaceOf(s, o)); // as ThreatSystem.GetThreateningEnemies
                    case FactKey.ThreatensAllyInNeed:
                        return FactValue.Of(L, s.Team != o.Team && KnowledgeFacts.ThreatensAllyInNeed(o, s, Roster));
                    case FactKey.EngagedByAlly:
                        return FactValue.Of(L, KnowledgeFacts.EngagedByAlly(o, s, Roster));
                    case FactKey.FoesNear:
                        return FactValue.OfNumber(L, KnowledgeFacts.FoesNear(o, s, Roster));
                    case FactKey.FlankingNow:
                        return FactValue.Of(L, CombatUtils.IsAttackerFlanking(o, s, Roster, out _));
                    case FactKey.DamagedObserver:
                    {
                        int damage = o.DamageMemory.DamageFrom(s);
                        return new FactValue(L, damage > 0, damage, damage > 0 ? "true" : "false");
                    }
                    case FactKey.ThreatPerRound:
                        return FactValue.OfAmount(L, KnowledgeFacts.CanAttackThisRound(s)
                            ? DND35.AI.Valuation.AttackValue.ExpectedRoundDamage(s, o.Stats.ArmorClass)
                            : 0f);
                    default:
                        return FactValue.Unknown;
                }
            }

            public bool Has(FactKey key, string value)
            {
                CharacterController s = Subject;
                if (s == null || s.Stats == null || string.IsNullOrWhiteSpace(value))
                    return false;
                string v = value.Trim();
                switch (key)
                {
                    case FactKey.Subtype:
                        return s.Stats.HasCreatureTag(v);
                    case FactKey.Condition:
                    {
                        List<StatusEffect> active = s.GetActiveConditionsDirect();
                        for (int i = 0; i < active.Count; i++)
                            if (active[i] != null && string.Equals(active[i].Type.ToString(), v, StringComparison.OrdinalIgnoreCase))
                                return true;
                        return false;
                    }
                    case FactKey.CreatureType:
                        return CreatureTypeProgressionDatabase.TryParseCreatureType(v, out CreatureTypeId want)
                            && KnowledgeFacts.TryCreatureTypeOf(s.Stats, out CreatureTypeId have) && have == want;
                    default:
                    {
                        FactValue f = Get(key);
                        return f.IsKnown && f.Text != null && string.Equals(f.Text.Trim(), v, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }
    }
}

using DND35.AI.Knowledge;
using DND35.AI.Valuation;
using UnityEngine;

namespace DND35.AI.Personality
{
    /// <summary>
    /// The target considerations of the personality term (batch 14 step 3; docs/designs/personalities_batch14.md
    /// "Considerations"; design 4.3). Each returns a value in [0, 1] read only through an <see cref="ITargetView"/> (the
    /// knowledge facade: OmniscienceLintTests fails on a direct read of a target's state here), and 0 for a fact the view
    /// does not know. AI estimates for choosing a target, never a rule. The observer's own state (its expected damage, its
    /// size) is read from the view's Observer.
    /// </summary>
    public static class Considerations
    {
        /// <summary>The distance in squares at which Proximity reaches 0 (1 when adjacent: (12 - d) / 11).</summary>
        public const int ProximityRange = 12;

        /// <summary>Foes near a target at which Clustered saturates (starter constant: min(FoesNear, 3) / 3).</summary>
        public const int ClusterSaturation = 3;

        /// <summary>KillChance's threat value of a known caster or healer (design 4.3).</summary>
        public const float CasterOrHealerThreat = 1f;

        /// <summary>KillChance's threat value of any other standing target (design 4.3's frontliner value).</summary>
        public const float OtherThreat = 0.6f;

        /// <summary>The value of <paramref name="id"/> for the view's subject, in [0, 1]; 0 without a view.</summary>
        public static float Evaluate(ConsiderationId id, ITargetView view)
        {
            if (view == null)
                return 0f;
            switch (id)
            {
                case ConsiderationId.WoundSeverity: return WoundSeverity(view);
                case ConsiderationId.IsDown: return FlagValue(view, FactKey.Down);
                case ConsiderationId.Helpless: return FlagValue(view, FactKey.Helpless);
                case ConsiderationId.Softness: return Softness(view);
                case ConsiderationId.KillChance: return KillChance(view, MyExpectedDamage(view));
                case ConsiderationId.KnownCaster: return FlagValue(view, FactKey.Caster);
                case ConsiderationId.KnownHealer: return FlagValue(view, FactKey.Healer);
                case ConsiderationId.Proximity: return Proximity(view);
                case ConsiderationId.ThreatensMe: return FlagValue(view, FactKey.ThreatensObserver);
                case ConsiderationId.EngagedByAlly: return FlagValue(view, FactKey.EngagedByAlly);
                case ConsiderationId.FlankOpportunity: return FlagValue(view, FactKey.FlankingNow);
                case ConsiderationId.Clustered: return Clustered(view);
                case ConsiderationId.LooksDangerous: return LooksDangerous(view);
                case ConsiderationId.Fleeing: return FlagValue(view, FactKey.Fleeing);
                case ConsiderationId.ThreatToWard: return FlagValue(view, FactKey.ThreatensAllyInNeed);
                case ConsiderationId.HurtMe: return FlagValue(view, FactKey.DamagedObserver);
                default: return 0f;
            }
        }

        /// <summary>1 when the view knows the boolean fact <paramref name="key"/> to be true, else 0.</summary>
        public static float FlagValue(ITargetView view, FactKey key)
        {
            FactValue f = view.Get(key);
            return f.IsKnown && f.Flag ? 1f : 0f;
        }

        private static bool IsDown(ITargetView view) => FlagValue(view, FactKey.Down) > 0f;

        /// <summary>
        /// A standing target's wound band: Unhurt 0, Hurt .33, BadlyHurt .66; a standing creature at 0 hit points or fewer
        /// (disabled, PHB p.145) 1; a downed one 0 (IsDown and Helpless value it instead).
        /// </summary>
        public static float WoundSeverity(ITargetView view)
        {
            if (IsDown(view))
                return 0f;
            FactValue band = view.Get(FactKey.WoundBand);
            if (!band.IsKnown)
                return 0f;
            switch ((WoundBand)band.Number)
            {
                case WoundBand.Hurt: return 0.33f;
                case WoundBand.BadlyHurt: return 0.66f;
                case WoundBand.Down: return 1f;
                default: return 0f;
            }
        }

        /// <summary>The worn armor: Unarmored 1, Light .67, Medium .33, Heavy 0; armor of another category .5.</summary>
        public static float Softness(ITargetView view)
        {
            FactValue armor = view.Get(FactKey.ArmorCategory);
            if (!armor.IsKnown)
                return 0f;
            switch ((WornArmor)armor.Number)
            {
                case WornArmor.Unarmored: return 1f;
                case WornArmor.Light: return 0.67f;
                case WornArmor.Medium: return 0.33f;
                case WornArmor.Heavy: return 0f;
                default: return 0.5f;
            }
        }

        /// <summary>The observer's expected round damage against the subject's AC estimate (AttackValue); 0 without an observer or an AC.</summary>
        public static float MyExpectedDamage(ITargetView view)
        {
            CharacterController me = view != null ? view.Observer : null;
            if (me == null || me.Stats == null)
                return 0f;
            FactValue ac = view.Get(FactKey.AcEstimate);
            if (!ac.IsKnown)
                return 0f;
            return AttackValue.ExpectedRoundDamage(me, ac.Number);
        }

        /// <summary>
        /// The chance this round's damage drops the subject, times its threat value (design 4.3: "reduce their numbers"):
        /// clamp01(<paramref name="myExpectedDamage"/> / HpCurrent) times 1 for a known caster or healer, .6 otherwise,
        /// and 0 when it is Down. A subject at 0 hit points or fewer that still stands counts any damage as enough.
        /// </summary>
        public static float KillChance(ITargetView view, float myExpectedDamage)
        {
            if (view == null || IsDown(view) || myExpectedDamage <= 0f)
                return 0f;
            FactValue hp = view.Get(FactKey.HpCurrent);
            if (!hp.IsKnown)
                return 0f;
            float share = hp.Number <= 0 ? 1f : Mathf.Clamp01(myExpectedDamage / hp.Number);
            bool valuable = FlagValue(view, FactKey.Caster) > 0f || FlagValue(view, FactKey.Healer) > 0f;
            return share * (valuable ? CasterOrHealerThreat : OtherThreat);
        }

        /// <summary>(12 - distance) / 11, clamped to [0, 1]: 1 when adjacent, 0 at 12 squares or more.</summary>
        public static float Proximity(ITargetView view)
        {
            FactValue d = view.Get(FactKey.Distance);
            if (!d.IsKnown)
                return 0f;
            return Mathf.Clamp01((ProximityRange - d.Number) / (float)(ProximityRange - 1));
        }

        /// <summary>Other foes within 2 squares of the subject: min(FoesNear, 3) / 3.</summary>
        public static float Clustered(ITargetView view)
        {
            FactValue n = view.Get(FactKey.FoesNear);
            if (!n.IsKnown)
                return 0f;
            return Mathf.Clamp(n.Number, 0, ClusterSaturation) / (float)ClusterSaturation;
        }

        /// <summary>
        /// How dangerous the subject looks (design 4.3, Apparent facts only; starter values): 1 when it is larger than the
        /// observer (Medium without one), or wears heavy armor with a melee weapon in hand; .5 for heavy armor alone or
        /// medium armor with a melee weapon; else 0.
        /// </summary>
        public static float LooksDangerous(ITargetView view)
        {
            FactValue size = view.Get(FactKey.Size);
            CharacterController me = view.Observer;
            SizeCategory mine = me != null ? me.GetCurrentSizeCategory() : SizeCategory.Medium;
            if (size.IsKnown && size.Number > (int)mine)
                return 1f;

            FactValue armor = view.Get(FactKey.ArmorCategory);
            bool melee = FlagValue(view, FactKey.WieldsMelee) > 0f;
            if (!armor.IsKnown)
                return 0f;
            WornArmor worn = (WornArmor)armor.Number;
            if (worn == WornArmor.Heavy)
                return melee ? 1f : 0.5f;
            if (worn == WornArmor.Medium && melee)
                return 0.5f;
            return 0f;
        }
    }
}

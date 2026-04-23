using UnityEngine;

namespace DND35.AI.Profiles
{
    [CreateAssetMenu(fileName = "Evoker AI", menuName = "DND35/AI/Profiles/Spellcasters/Evoker")]
    public class EvokerAIProfile : SpellcasterAIProfile
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Evoker";
            if (string.IsNullOrWhiteSpace(Description))
                Description = "Offensive spellcaster focused on evocation and aggressive AoE use.";

            CombatStyle = CombatStyle.Ranged;
            Aggression = 0.8f;
            PrioritizeWounded = true;

            PreferredCastingDistanceSquares = 6;
            Movement.PreferredRangeSquares = PreferredCastingDistanceSquares;

            if (SchoolPriorities.Count == 0)
            {
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Evocation, 10f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Transmutation, 5f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Abjuration, 3f));
            }

            AOECasting.PreferSingleTarget = false;
            AOECasting.MinimumEnemiesInAOE = 2;
            AOECasting.AvoidHittingAllies = true;
            AOECasting.MaxAcceptableAllyCasualties = 1f;
            AOECasting.AcceptableAllyDamagePercent = 0.20f;

            SpellSelection.BuffBeforeDamage = false;
            SpellSelection.ConserveHighLevelSpells = false;
        }
    }
}

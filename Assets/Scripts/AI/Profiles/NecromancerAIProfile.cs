using UnityEngine;

namespace DND35.AI.Profiles
{
    [CreateAssetMenu(fileName = "Necromancer AI", menuName = "DND35/AI/Profiles/Spellcasters/Necromancer")]
    public class NecromancerAIProfile : SpellcasterAIProfile
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Necromancer";
            if (string.IsNullOrWhiteSpace(Description))
                Description = "Debuff and attrition specialist emphasizing necromancy and tactical pressure.";

            CombatStyle = CombatStyle.Ranged;
            Aggression = 0.6f;
            PrioritizeWounded = true;

            PreferredCastingDistanceSquares = 5;
            Movement.PreferredRangeSquares = PreferredCastingDistanceSquares;

            if (SchoolPriorities.Count == 0)
            {
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Necromancy, 10f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Conjuration, 7f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Evocation, 4f));
            }

            AOECasting.PreferSingleTarget = true;
            AOECasting.MinimumEnemiesInAOE = 3;
            AOECasting.AvoidHittingAllies = true;
            AOECasting.MaxAcceptableAllyCasualties = 0f;

            SpellSelection.BuffBeforeDamage = true;
            SpellSelection.ConserveHighLevelSpells = true;
            SpellSelection.UseUtilitySpells = true;
        }
    }
}

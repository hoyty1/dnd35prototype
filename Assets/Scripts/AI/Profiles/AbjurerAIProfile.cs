using UnityEngine;

namespace DND35.AI.Profiles
{
    [CreateAssetMenu(fileName = "Abjurer AI", menuName = "DND35/AI/Profiles/Spellcasters/Abjurer")]
    public class AbjurerAIProfile : SpellcasterAIProfile
    {
        protected override void OnEnable()
        {
            base.OnEnable();

            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Abjurer";
            if (string.IsNullOrWhiteSpace(Description))
                Description = "Defensive spellcaster prioritizing protection, control, and ally safety.";

            CombatStyle = CombatStyle.Mixed;
            Aggression = 0.3f;

            PreferredCastingDistanceSquares = 4;
            Movement.PreferredRangeSquares = PreferredCastingDistanceSquares;

            if (SchoolPriorities.Count == 0)
            {
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Abjuration, 10f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Transmutation, 6f));
                SchoolPriorities.Add(new SpellSchoolPriority(SpellSchool.Evocation, 4f));
            }

            AOECasting.PreferSingleTarget = true;
            AOECasting.MinimumEnemiesInAOE = 3;
            AOECasting.AvoidHittingAllies = true;
            AOECasting.MaxAcceptableAllyCasualties = 0f;
            AOECasting.AcceptableAllyDamagePercent = 0.05f;

            SpellSelection.BuffBeforeDamage = true;
            SpellSelection.ConserveHighLevelSpells = true;
            SpellSelection.CounterEnemySpells = true;
        }
    }
}

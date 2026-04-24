using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Tactical humanoid profile: disciplined melee behavior with moderate aggression,
    /// flanking awareness, and safer movement than berserk units.
    /// </summary>
    [CreateAssetMenu(fileName = "Humanoid AI", menuName = "DND35/AI/Profiles/Humanoid")]
    public class HumanoidAIProfile : AIProfile
    {
        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Humanoid";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Tactical humanoid combatant that favors positioning, flanking, and measured pressure.";

            CombatStyle = CombatStyle.Melee;
            Aggression = 0.7f;
            PrioritizeWounded = true;
            PrioritizeIsolated = true;
            SwitchTargetsOften = false;

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = true;
            Movement.PreferredRangeSquares = 0;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = true;
            Movement.UseCover = false;

            GrappleBehavior = GrappleBehavior.InitiateWhenSafe;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = true;
            Maneuvers.AttemptDisarm = true;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = false;

            EnsureDefaultTags();
        }

        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            // Tactical humanoids generally prioritize ongoing combat pressure and objectives.
            return false;
        }

        private void EnsureDefaultTags()
        {
            if (TagPriorities == null)
                TagPriorities = new System.Collections.Generic.List<TagPriority>();

            if (TagPriorities.Count > 0)
                return;

            TagPriorities.Add(new TagPriority("HP State: Disabled", 3f));
            TagPriorities.Add(new TagPriority("HP State: Staggered", 5f));
            TagPriorities.Add(new TagPriority("Armor: Heavy Armor", 2f, true));
            TagPriorities.Add(new TagPriority("Armor: Unarmored", 3f));
        }
    }
}

using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Grappler profile: seeks favorable grapples and control maneuvers.
    /// </summary>
    [CreateAssetMenu(fileName = "Grappler AI", menuName = "DND35/AI/Profiles/Grappler")]
    public class GrapplerAIProfile : AIProfile
    {
        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Grappler";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Control-focused melee profile that seeks grapples on isolated/vulnerable targets.";

            CombatStyle = CombatStyle.Melee;
            Aggression = 0.7f;
            PrioritizeWounded = false;
            PrioritizeIsolated = true;
            SwitchTargetsOften = false;

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = true;
            Movement.PreferredRangeSquares = 0;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = true;
            Movement.UseCover = false;

            GrappleBehavior = GrappleBehavior.Aggressive;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = true;
            Maneuvers.AttemptDisarm = true;

            EnsureDefaultTags();
        }

        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            // Grapplers focus on control, not execution.
            return false;
        }

        private void EnsureDefaultTags()
        {
            if (TagPriorities == null)
                TagPriorities = new System.Collections.Generic.List<TagPriority>();

            if (TagPriorities.Count > 0)
                return;

            TagPriorities.Add(new TagPriority("Armor: Unarmored", 5f));
            TagPriorities.Add(new TagPriority("Armor: Light Armor", 3f));
            TagPriorities.Add(new TagPriority("Armor: Heavy Armor", 3f, true));
            TagPriorities.Add(new TagPriority("Race: Halfling", 3f));
            TagPriorities.Add(new TagPriority("Race: Dwarf", 2f, true));
        }
    }
}

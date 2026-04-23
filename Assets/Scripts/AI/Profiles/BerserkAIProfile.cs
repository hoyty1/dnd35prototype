using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Aggressive melee profile: low safety, high pressure, wounded-target preference.
    /// </summary>
    [CreateAssetMenu(fileName = "Berserk AI", menuName = "DND35/AI/Profiles/Berserk")]
    public class BerserkAIProfile : AIProfile
    {
        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Berserk";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Aggressive melee combatant that pushes into danger and focuses wounded targets.";

            CombatStyle = CombatStyle.Melee;
            Aggression = 1f;
            PrioritizeWounded = true;
            PrioritizeIsolated = false;
            SwitchTargetsOften = false;

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = false;
            Movement.PreferredRangeSquares = 0;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = false;
            Movement.UseCover = false;

            GrappleBehavior = GrappleBehavior.Aggressive;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = true;
            Maneuvers.UsePowerAttack = true;

            EnsureDefaultTags();
        }

        private void EnsureDefaultTags()
        {
            if (TagPriorities == null)
                TagPriorities = new System.Collections.Generic.List<TagPriority>();

            if (TagPriorities.Count > 0)
                return;

            TagPriorities.Add(new TagPriority("HP State: Disabled", 4f));
            TagPriorities.Add(new TagPriority("HP State: Staggered", 6f));
            TagPriorities.Add(new TagPriority("HP State: Dying", 9f));
        }
    }
}

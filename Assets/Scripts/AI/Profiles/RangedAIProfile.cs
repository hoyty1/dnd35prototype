using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Ranged profile: keep distance, avoid AoOs, and target less armored enemies.
    /// </summary>
    [CreateAssetMenu(fileName = "Ranged AI", menuName = "DND35/AI/Profiles/Ranged")]
    public class RangedAIProfile : AIProfile
    {
        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Ranged";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Ranged specialist that keeps distance and prefers vulnerable/unarmored targets.";

            CombatStyle = CombatStyle.Ranged;
            Aggression = 0.35f;
            PrioritizeWounded = false;
            PrioritizeIsolated = true;
            SwitchTargetsOften = true;

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = true;
            Movement.PreferredRangeSquares = 6;
            Movement.MaintainDistance = true;
            Movement.SeekFlanking = false;
            Movement.UseCover = true;

            GrappleBehavior = GrappleBehavior.EscapeOnly;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = false;
            Maneuvers.AttemptDisarm = false;

            EnsureDefaultTags();
        }

        private void EnsureDefaultTags()
        {
            if (TagPriorities == null)
                TagPriorities = new System.Collections.Generic.List<TagPriority>();

            if (TagPriorities.Count > 0)
                return;

            TagPriorities.Add(new TagPriority("Armor: Unarmored", 8f));
            TagPriorities.Add(new TagPriority("Armor: Light Armor", 5f));
            TagPriorities.Add(new TagPriority("Wielding: Unarmed", 3f));
            TagPriorities.Add(new TagPriority("Armor: Heavy Armor", 6f, true));
        }
    }
}

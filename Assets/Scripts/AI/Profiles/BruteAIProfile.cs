using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// The plain melee profile of the Brute archetype (ogres, giants, bulettes, golems; batch 13 step 8, AI-004) and of
    /// a Caster-archetype creature that spawns without spells (the couatl and the spirit naga until batch 16 gives them
    /// their spell-likes). Owner question Q4's default (2026-10-29, a design default the owner may overrule;
    /// docs/systems/RULES_COVERAGE.md 4.1): melee, high aggression, wounded foes first, the Power Attack flag set as
    /// UndeadBruteAIProfile sets it (unread until AI-026 is fixed, so no Power Attack yet), and nothing else: no trip,
    /// disarm, sunder, bull rush or overrun, no forced coup de grace and no aggressive grapple (unlike BerserkAIProfile),
    /// so it adds no maneuver choice before batch 14 scores maneuvers. Everything else is the AIProfile default.
    /// </summary>
    [CreateAssetMenu(fileName = "Brute AI", menuName = "DND35/AI/Profiles/Brute")]
    public class BruteAIProfile : AIProfile
    {
        public override void ApplyRuntimeDefaults()
        {
            base.ApplyRuntimeDefaults();

            ProfileName = "Brute";
            Description = "Plain melee combatant that closes in and hits the most wounded foe, without combat maneuvers.";

            CombatStyle = CombatStyle.Melee;
            Aggression = 0.9f;
            PrioritizeWounded = true;
            PrioritizeIsolated = false;
            SwitchTargetsOften = false;

            if (Movement == null)
                Movement = new MovementPreferences();
            Movement.PreferredRangeSquares = 0;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = false;

            GrappleBehavior = GrappleBehavior.EscapeOnly;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();
            Maneuvers.AttemptTrip = false;
            Maneuvers.AttemptDisarm = false;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = true; // as UndeadBruteAIProfile; unread until AI-026
        }
    }
}

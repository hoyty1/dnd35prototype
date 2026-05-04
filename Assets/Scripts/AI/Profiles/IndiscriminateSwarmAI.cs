using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Uncontrolled swarm AI: targets nearest living creature (friend or foe) and keeps pursuing it until death.
    /// </summary>
    [CreateAssetMenu(fileName = "Indiscriminate Swarm AI", menuName = "DND35/AI/Profiles/Indiscriminate Swarm")]
    public class IndiscriminateSwarmAI : SwarmAI
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            ProfileName = "Indiscriminate Swarm";
            Description = "Targets the nearest living creature regardless of alignment or team.";
        }
    }
}

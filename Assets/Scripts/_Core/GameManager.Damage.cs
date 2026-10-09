using UnityEngine;

/// <summary>
/// GameManager partial class: the one way rules code deals damage outside a weapon attack roll (SPL-004;
/// docs/systems/PC_NPC_PARITY.md, plan step 12).
///
/// <see cref="DealDamage"/> sends the damage through <see cref="CharacterStats.ApplyIncomingDamage"/> with a typed
/// <see cref="DamagePacket"/> (see <see cref="DamagePackets"/>), so immunity, Protection from Energy, resistance,
/// Fire Shield and, for weapon-like damage only, damage reduction apply; then it runs the shared after-damage checks
/// (<see cref="AfterDamageTaken"/>): the concentration check on the damage actually taken and the death handling.
/// Spell handlers of both sides, area spells, Fire Shield, breath weapons and monster special attacks call it. The
/// combat-end check (<see cref="EvaluateCombatEnd"/>) stays with the caller, once the whole effect has resolved, so a
/// multi-target effect never ends the combat halfway through its targets.
/// </summary>
public partial class GameManager
{
    /// <summary>
    /// Applies <paramref name="rawDamage"/> to <paramref name="target"/> through the mitigation pipeline, then runs
    /// the concentration and death checks for the damage taken. Returns the mitigation result (FinalDamage is the
    /// damage taken); an empty result when there is no target.
    /// </summary>
    public DamageResolutionResult DealDamage(CharacterController target, int rawDamage, DamagePacket packet)
    {
        DamageResolutionResult result = ApplyDamagePacket(target, rawDamage, packet);
        AfterDamageTaken(target, result.FinalDamage);
        return result;
    }

    /// <summary>
    /// Only the mitigation pipeline, for a caller whose own pipeline runs the after-damage checks with the damage
    /// taken (the single-target spell pipelines read <c>SpellResult.DamageDealt</c> and <c>TargetKilled</c>).
    /// </summary>
    public static DamageResolutionResult ApplyDamagePacket(CharacterController target, int rawDamage, DamagePacket packet)
    {
        if (target == null || target.Stats == null)
            return new DamageResolutionResult();

        if (packet != null)
            packet.RawDamage = Mathf.Max(0, rawDamage);
        return target.Stats.ApplyIncomingDamage(rawDamage, packet);
    }

    /// <summary>
    /// The shared checks after a creature took damage: a concentration check for an ongoing spell or a held charge
    /// (PHB p.170), and the death handling (OnDeath and summon cleanup) when the damage killed it.
    /// </summary>
    public void AfterDamageTaken(CharacterController target, int damageTaken)
    {
        if (target == null || target.Stats == null)
            return;

        if (damageTaken > 0)
            CheckConcentrationOnDamage(target, damageTaken);

        if (target.Stats.IsDead)
        {
            target.OnDeath();
            HandleSummonDeathCleanup(target);
        }
    }

    /// <summary>
    /// A log suffix for mitigated damage: empty when nothing was prevented, otherwise
    /// " (N rolled; Resist 10 ...)" from <see cref="DamageResolutionResult.GetMitigationSummary"/>.
    /// </summary>
    public static string DescribeMitigation(DamageResolutionResult result)
    {
        if (result == null || result.RawDamage <= 0)
            return string.Empty;

        string summary = result.GetMitigationSummary();
        if (result.TotalPrevented <= 0 && string.IsNullOrEmpty(summary))
            return string.Empty;

        return string.IsNullOrEmpty(summary)
            ? $" ({result.RawDamage} rolled)"
            : $" ({result.RawDamage} rolled; {summary})";
    }
}

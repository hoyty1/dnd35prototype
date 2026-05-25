using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// D&D 3.5e Mounted Combat mechanics (PHB p.157-158).
///
/// Handles:
/// - Mounted Combat feat: negate hit on mount via Ride check (1/round)
/// - Ride-By Attack: attack during charge, continue moving
/// - Spirited Charge: double damage on charge (triple with lance)
/// - Trample: overrun without opponent avoiding, mount gets free hoof attack
/// - Mounted Archery: halve ranged attack penalty while mounted
/// - Mounted charge rules
/// - Mount natural attacks (hoof, bite) for war-trained mounts
/// </summary>
public static class MountedCombatSystem
{
    // ════════════════════════════════════════════════════════════════
    // MOUNTED COMBAT FEAT (PHB p.98)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempt to use Mounted Combat feat to negate a hit on the mount.
    /// PHB p.98: Once per round when your mount is hit in combat, you may
    /// attempt a Ride check (as a reaction) to negate the hit.
    /// The hit is negated if your Ride check result > the opponent's attack roll.
    /// </summary>
    /// <param name="rider">Mounted character with Mounted Combat feat.</param>
    /// <param name="mount">Mount that was hit.</param>
    /// <param name="attackRollTotal">Attacker's total attack roll.</param>
    /// <returns>(negated, log) — whether the hit was negated and detail string.</returns>
    public static (bool negated, string log) TryMountedCombatNegate(
        CharacterController rider, MountSystem.MountInstance mount, int attackRollTotal)
    {
        if (rider == null || rider.Stats == null || mount == null)
            return (false, "Invalid rider/mount");

        // Must have Mounted Combat feat
        if (!rider.Stats.HasFeat("Mounted Combat"))
            return (false, $"{rider.Stats.CharacterName} does not have Mounted Combat feat.");

        // Once per round limit
        if (rider.Stats.HasUsedMountedCombatThisRound)
            return (false, $"{rider.Stats.CharacterName} already used Mounted Combat this round.");

        // Make Ride check vs attack roll
        int rideBonus = rider.Stats.GetSkillBonus("Ride");
        int roll = Random.Range(1, 21);
        int rideTotal = roll + rideBonus;

        rider.Stats.HasUsedMountedCombatThisRound = true;

        if (rideTotal > attackRollTotal)
        {
            string log = $"🛡️ Mounted Combat: {rider.Stats.CharacterName} negates hit on {mount.Data.Name}! " +
                         $"(Ride d20({roll})+{rideBonus}={rideTotal} > attack {attackRollTotal})";
            Debug.Log($"[MountedCombat] {log}");
            return (true, log);
        }
        else
        {
            string log = $"🛡️ Mounted Combat: {rider.Stats.CharacterName} fails to negate hit on {mount.Data.Name}. " +
                         $"(Ride d20({roll})+{rideBonus}={rideTotal} ≤ attack {attackRollTotal})";
            Debug.Log($"[MountedCombat] {log}");
            return (false, log);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // SPIRITED CHARGE (PHB p.100)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calculate Spirited Charge damage multiplier.
    /// PHB p.100: Double damage on mounted charge (triple with lance).
    /// Prerequisites: Mounted Combat, Ride-By Attack, Ride 1 rank.
    /// </summary>
    /// <param name="rider">Charging rider.</param>
    /// <param name="weaponName">Name of the weapon used (checks for "Lance").</param>
    /// <returns>Damage multiplier (1 = normal, 2 = double, 3 = triple with lance).</returns>
    public static int GetSpiritedChargeDamageMultiplier(CharacterStats rider, string weaponName)
    {
        if (rider == null) return 1;
        if (!rider.HasFeat("Spirited Charge")) return 1;

        bool isLance = !string.IsNullOrEmpty(weaponName) &&
                       weaponName.ToLower().Contains("lance");

        return isLance ? 3 : 2;
    }

    /// <summary>
    /// Apply Spirited Charge damage multiplier to base damage.
    /// Only active when mounted and charging.
    /// </summary>
    public static int ApplySpiritedChargeDamage(CharacterController rider, int baseDamage, string weaponName)
    {
        if (rider == null || rider.Stats == null) return baseDamage;
        if (!MountSystem.IsMounted(rider)) return baseDamage;

        int multiplier = GetSpiritedChargeDamageMultiplier(rider.Stats, weaponName);
        if (multiplier > 1)
        {
            int result = baseDamage * multiplier;
            Debug.Log($"[MountedCombat] Spirited Charge: {rider.Stats.CharacterName} deals {multiplier}× damage ({baseDamage} → {result}) with {weaponName}");
            return result;
        }
        return baseDamage;
    }

    // ════════════════════════════════════════════════════════════════
    // RIDE-BY ATTACK (PHB p.99)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if rider can use Ride-By Attack.
    /// PHB p.99: Attack during charge, then continue moving.
    /// Total movement can't exceed double mount's speed.
    /// </summary>
    public static bool CanUseRideByAttack(CharacterController rider)
    {
        if (rider == null || rider.Stats == null) return false;
        return MountSystem.IsMounted(rider) && rider.Stats.HasFeat("Ride-By Attack");
    }

    /// <summary>
    /// Check if rider has remaining movement after a Ride-By Attack charge.
    /// </summary>
    public static int GetRideByRemainingMovement(CharacterController rider, int distanceMoved)
    {
        if (!CanUseRideByAttack(rider)) return 0;

        var mount = MountSystem.GetMount(rider);
        if (mount == null) return 0;

        int maxChargeDistance = mount.Data.MovementSpeed * 2; // Double move for charge
        return Mathf.Max(0, maxChargeDistance - distanceMoved);
    }

    // ════════════════════════════════════════════════════════════════
    // TRAMPLE (PHB p.101)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if rider can use Trample feat.
    /// PHB p.101: Target cannot choose to avoid mounted overrun.
    /// Mount gets free hoof attack against knocked-down targets.
    /// </summary>
    public static bool CanUseTrample(CharacterController rider)
    {
        if (rider == null || rider.Stats == null) return false;
        return MountSystem.IsMounted(rider) && rider.Stats.HasFeat("Trample");
    }

    /// <summary>
    /// Execute mount's hoof attack against a prone target after trample.
    /// PHB p.101: +4 bonus on attack rolls against prone targets.
    /// </summary>
    public static (bool hit, int damage, string log) ExecuteTrampleHoofAttack(
        CharacterController rider, CharacterController target)
    {
        if (rider == null || target == null || target.Stats == null)
            return (false, 0, "Invalid target");

        var mount = MountSystem.GetMount(rider);
        if (mount == null || mount.Data.NaturalAttacks == null || mount.Data.NaturalAttacks.Length == 0)
            return (false, 0, "No mount or no natural attacks");

        // Use first hoof attack
        MountNaturalAttack hoof = null;
        foreach (var atk in mount.Data.NaturalAttacks)
        {
            if (atk.Name == "Hoof") { hoof = atk; break; }
        }
        if (hoof == null) hoof = mount.Data.NaturalAttacks[0];

        int roll = Random.Range(1, 21);
        int totalAttack = roll + hoof.AttackBonus + 4; // +4 vs prone
        int targetAC = target.Stats.AC;

        bool hit = (roll == 20) || (roll != 1 && totalAttack >= targetAC);
        int damage = 0;
        string log;

        if (hit)
        {
            damage = hoof.RollDamage();
            target.Stats.TakeDamage(damage);
            log = $"🐎 Trample: {mount.Data.Name} hoof attack hits {target.Stats.CharacterName}! " +
                  $"(d20({roll})+{hoof.AttackBonus}+4(prone)={totalAttack} vs AC {targetAC}) for {damage} damage";
        }
        else
        {
            log = $"🐎 Trample: {mount.Data.Name} hoof attack misses {target.Stats.CharacterName}. " +
                  $"(d20({roll})+{hoof.AttackBonus}+4(prone)={totalAttack} vs AC {targetAC})";
        }

        Debug.Log($"[MountedCombat] {log}");
        return (hit, damage, log);
    }

    // ════════════════════════════════════════════════════════════════
    // MOUNTED ARCHERY (PHB p.98)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the ranged attack penalty for shooting while mounted.
    /// PHB p.98: Normal penalty is -4 (double move) or -8 (running).
    /// Mounted Archery feat halves these penalties.
    /// </summary>
    /// <param name="rider">Mounted character.</param>
    /// <param name="mountIsRunning">True if mount used run action (quadruple speed).</param>
    /// <returns>Penalty to ranged attack rolls (negative number).</returns>
    public static int GetMountedRangedPenalty(CharacterController rider, bool mountIsRunning = false)
    {
        if (rider == null || rider.Stats == null) return 0;
        if (!MountSystem.IsMounted(rider)) return 0;

        int basePenalty = mountIsRunning ? -8 : -4;

        // Mounted Archery halves the penalty
        if (rider.Stats.HasFeat("Mounted Archery"))
            return basePenalty / 2;

        return basePenalty;
    }

    // ════════════════════════════════════════════════════════════════
    // MOUNT NATURAL ATTACKS
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Execute all natural attacks for a war-trained mount.
    /// PHB p.157: A war-trained mount can attack while being ridden.
    /// Controlled mounts can only move, not attack.
    /// </summary>
    public static string ExecuteMountAttacks(CharacterController rider, CharacterController target)
    {
        if (rider == null || target == null || target.Stats == null)
            return "Invalid rider/target";

        var mount = MountSystem.GetMount(rider);
        if (mount == null)
            return "Not mounted";

        if (!mount.Data.IsWarTrained)
            return $"{mount.Data.Name} is not war-trained and cannot attack.";

        if (mount.ControlState == MountControlState.Controlled)
            return $"{mount.Data.Name} is controlled and can only take move actions.";

        if (mount.Data.NaturalAttacks == null || mount.Data.NaturalAttacks.Length == 0)
            return $"{mount.Data.Name} has no natural attacks.";

        var logs = new System.Collections.Generic.List<string>();
        int totalDamage = 0;

        foreach (var attack in mount.Data.NaturalAttacks)
        {
            int roll = Random.Range(1, 21);
            int totalAttack = roll + attack.AttackBonus;
            int targetAC = target.Stats.AC;
            bool hit = (roll == 20) || (roll != 1 && totalAttack >= targetAC);

            if (hit)
            {
                int damage = attack.RollDamage();
                target.Stats.TakeDamage(damage);
                totalDamage += damage;
                logs.Add($"{attack.Name} hits! (d20({roll})+{attack.AttackBonus}={totalAttack} vs AC {targetAC}) → {damage} dmg");
            }
            else
            {
                logs.Add($"{attack.Name} misses. (d20({roll})+{attack.AttackBonus}={totalAttack} vs AC {targetAC})");
            }
        }

        string result = $"🐎 {mount.Data.Name} attacks {target.Stats.CharacterName}: " + string.Join("; ", logs);
        if (totalDamage > 0) result += $" [Total: {totalDamage} damage]";
        Debug.Log($"[MountedCombat] {result}");
        return result;
    }

    // ════════════════════════════════════════════════════════════════
    // MOUNTED CHARGE
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Check if a mounted charge is valid. PHB p.154:
    /// - Must move at least 10 feet (2 squares)
    /// - Must move in a straight line
    /// - Must end adjacent to target
    /// - Gains +2 attack, -2 AC
    /// </summary>
    public static bool CanMountedCharge(CharacterController rider)
    {
        return rider != null && rider.Stats != null && MountSystem.IsMounted(rider);
    }

    /// <summary>
    /// Get the mounted charge bonus to attack (+2, per PHB p.154).
    /// </summary>
    public static int GetChargeAttackBonus() => 2;

    /// <summary>
    /// Get the mounted charge AC penalty (-2, per PHB p.154).
    /// </summary>
    public static int GetChargeACPenalty() => -2;

    /// <summary>
    /// Process damage for a mounted charge attack.
    /// Includes Spirited Charge multiplier if applicable.
    /// </summary>
    public static int ProcessMountedChargeDamage(CharacterController rider, int baseDamage, string weaponName)
    {
        return ApplySpiritedChargeDamage(rider, baseDamage, weaponName);
    }
}

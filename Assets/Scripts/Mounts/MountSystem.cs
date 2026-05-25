using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// D&D 3.5e Mount System (PHB p.157-158, PHB p.80 Ride skill).
///
/// Core mechanics:
/// - Mount/dismount (move action, or free action with DC 20 Ride check)
/// - Ride skill checks for control, combat, soft fall, spur, etc.
/// - Mount and rider act on same initiative
/// - Controlled mount takes only move actions; uncontrolled acts independently
/// - Size compatibility: rider must be at least one size smaller than mount
/// - Carrying capacity enforcement
/// </summary>
public static class MountSystem
{
    // ── Active mount tracking ──
    private static readonly Dictionary<CharacterController, MountInstance> _mountedCharacters
        = new Dictionary<CharacterController, MountInstance>();

    /// <summary>
    /// Runtime instance of a mount, with mutable HP and state.
    /// </summary>
    public class MountInstance
    {
        public MountData Data;
        public int CurrentHP;
        public int MaxHP;
        public MountControlState ControlState;
        public bool IsAlive => CurrentHP > 0;

        /// <summary>Position on the grid (in squares from origin).</summary>
        public Vector2Int GridPosition;

        public MountInstance(MountData template)
        {
            Data = template.Clone();
            MaxHP = template.HitPoints;
            CurrentHP = MaxHP;
            ControlState = MountControlState.Uncontrolled;
        }

        public void TakeDamage(int amount)
        {
            amount = Mathf.Max(0, amount);
            CurrentHP = Mathf.Max(0, CurrentHP - amount);
            if (CurrentHP <= 0)
                Debug.Log($"[Mount] {Data.Name} has been killed!");
        }

        public void Heal(int amount)
        {
            amount = Mathf.Max(0, amount);
            CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        }

        public override string ToString()
        {
            return $"{Data.Name} (HP {CurrentHP}/{MaxHP}, {ControlState})";
        }
    }

    // ════════════════════════════════════════════════════════════════
    // RIDE SKILL DCs (PHB p.80)
    // ════════════════════════════════════════════════════════════════

    public const int DC_GUIDE_WITH_KNEES = 5;      // Free hands for casting/fighting
    public const int DC_STAY_IN_SADDLE = 5;         // Avoid falling when hit
    public const int DC_FIGHT_WITH_WARHORSE = 10;   // Control non-war-trained mount in battle
    public const int DC_COVER = 15;                 // Half cover from mount
    public const int DC_SOFT_FALL = 15;             // No damage when knocked off
    public const int DC_LEAP = 15;                  // Jump obstacle while mounted
    public const int DC_SPUR_MOUNT = 15;            // +10 speed for 1 round
    public const int DC_CONTROL_IN_BATTLE = 20;     // Control non-war mount in combat
    public const int DC_FAST_MOUNT_DISMOUNT = 20;   // Mount/dismount as free action

    // ════════════════════════════════════════════════════════════════
    // MOUNT / DISMOUNT
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempt to mount a creature. PHB p.157:
    /// - Normal: move action
    /// - DC 20 Ride check: free action (fast mount)
    /// - Mount must be within 1 square
    /// - Size compatibility: rider at least one size smaller
    /// </summary>
    /// <param name="rider">Character attempting to mount.</param>
    /// <param name="mount">Mount instance to ride.</param>
    /// <param name="attemptFastMount">If true, attempt DC 20 for free action.</param>
    /// <returns>Result log string, or null on failure.</returns>
    public static string TryMount(CharacterController rider, MountInstance mount, bool attemptFastMount = false)
    {
        if (rider == null || rider.Stats == null || mount == null || mount.Data == null)
            return null;

        if (IsMounted(rider))
        {
            Debug.LogWarning($"[Mount] {rider.Stats.CharacterName} is already mounted!");
            return null;
        }

        if (!mount.IsAlive)
        {
            Debug.LogWarning($"[Mount] {mount.Data.Name} is dead and cannot be ridden.");
            return null;
        }

        // Size check: rider must be at least one size category smaller
        if (!CanRideMount(rider.Stats, mount.Data))
        {
            string msg = $"{rider.Stats.CharacterName} cannot ride {mount.Data.Name} (size incompatible — rider must be smaller than mount)";
            Debug.LogWarning($"[Mount] {msg}");
            return msg;
        }

        // Fast mount attempt (DC 20 Ride check → free action)
        bool fastMount = false;
        string rideCheckLog = "";
        if (attemptFastMount)
        {
            int rideBonus = rider.Stats.GetSkillBonus("Ride");
            int roll = Random.Range(1, 21);
            int total = roll + rideBonus;
            fastMount = total >= DC_FAST_MOUNT_DISMOUNT;
            rideCheckLog = $" (Ride check: d20({roll})+{rideBonus}={total} vs DC {DC_FAST_MOUNT_DISMOUNT} — {(fastMount ? "FAST MOUNT" : "failed, uses move action")})";
        }

        // Register mount
        _mountedCharacters[rider] = mount;

        string actionCost = fastMount ? "free action" : "move action";
        string log = $"🐎 {rider.Stats.CharacterName} mounts {mount.Data.Name} ({actionCost}){rideCheckLog}";
        Debug.Log($"[Mount] {log}");
        return log;
    }

    /// <summary>
    /// Attempt to dismount. PHB p.157:
    /// - Normal: move action
    /// - DC 20 Ride check: free action (fast dismount)
    /// </summary>
    public static string TryDismount(CharacterController rider, bool attemptFastDismount = false)
    {
        if (rider == null || rider.Stats == null)
            return null;

        if (!IsMounted(rider))
        {
            Debug.LogWarning($"[Mount] {rider.Stats.CharacterName} is not mounted!");
            return null;
        }

        MountInstance mount = _mountedCharacters[rider];

        bool fastDismount = false;
        string rideCheckLog = "";
        if (attemptFastDismount)
        {
            int rideBonus = rider.Stats.GetSkillBonus("Ride");
            int roll = Random.Range(1, 21);
            int total = roll + rideBonus;
            fastDismount = total >= DC_FAST_MOUNT_DISMOUNT;
            rideCheckLog = $" (Ride check: d20({roll})+{rideBonus}={total} vs DC {DC_FAST_MOUNT_DISMOUNT} — {(fastDismount ? "FAST DISMOUNT" : "failed, uses move action")})";
        }

        _mountedCharacters.Remove(rider);

        string actionCost = fastDismount ? "free action" : "move action";
        string log = $"🐎 {rider.Stats.CharacterName} dismounts from {mount.Data.Name} ({actionCost}){rideCheckLog}";
        Debug.Log($"[Mount] {log}");
        return log;
    }

    // ════════════════════════════════════════════════════════════════
    // RIDE SKILL CHECKS
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Make a Ride check against a DC. Returns (success, total, log).
    /// </summary>
    public static (bool success, int total, string log) MakeRideCheck(CharacterStats stats, int dc, string description)
    {
        if (stats == null) return (false, 0, "No stats");
        int rideBonus = stats.GetSkillBonus("Ride");
        int roll = Random.Range(1, 21);
        int total = roll + rideBonus;
        bool success = total >= dc;
        string log = $"Ride ({description}): d20({roll})+{rideBonus}={total} vs DC {dc} — {(success ? "SUCCESS" : "FAILED")}";
        return (success, total, log);
    }

    /// <summary>
    /// Control mount check (DC 5 for war-trained, DC 20 for untrained in battle).
    /// PHB p.157: Controlled mount can only take move actions; rider directs.
    /// </summary>
    public static bool TryControlMount(CharacterController rider, MountInstance mount, bool inCombat)
    {
        if (rider == null || rider.Stats == null || mount == null) return false;

        // War-trained mounts are easy to control
        int dc = DC_GUIDE_WITH_KNEES; // DC 5 basic control
        if (inCombat && !mount.Data.IsWarTrained)
            dc = DC_CONTROL_IN_BATTLE; // DC 20 for untrained mount in combat

        var (success, total, log) = MakeRideCheck(rider.Stats, dc, "Control Mount");
        Debug.Log($"[Mount] {rider.Stats.CharacterName}: {log}");

        mount.ControlState = success ? MountControlState.Controlled : MountControlState.Uncontrolled;
        return success;
    }

    /// <summary>
    /// Guide with knees (DC 5) — keep both hands free for fighting/casting.
    /// PHB p.80: "You can react instantly to guide your mount with your knees..."
    /// </summary>
    public static bool TryGuideWithKnees(CharacterStats stats)
    {
        var (success, _, log) = MakeRideCheck(stats, DC_GUIDE_WITH_KNEES, "Guide with Knees");
        Debug.Log($"[Mount] {(stats?.CharacterName ?? "?")} {log}");
        return success;
    }

    /// <summary>
    /// Stay in saddle (DC 5) — avoid falling when mount takes damage or stumbles.
    /// </summary>
    public static bool TryStayInSaddle(CharacterStats stats)
    {
        var (success, _, log) = MakeRideCheck(stats, DC_STAY_IN_SADDLE, "Stay in Saddle");
        Debug.Log($"[Mount] {(stats?.CharacterName ?? "?")} {log}");
        return success;
    }

    /// <summary>
    /// Fight with combat-trained mount (DC 10).
    /// War-trained mounts fight automatically; non-war require this check.
    /// </summary>
    public static bool TryFightMounted(CharacterController rider, MountInstance mount)
    {
        if (mount.Data.IsWarTrained) return true;

        var (success, _, log) = MakeRideCheck(rider.Stats, DC_FIGHT_WITH_WARHORSE, "Fight Mounted");
        Debug.Log($"[Mount] {rider.Stats.CharacterName}: {log}");
        return success;
    }

    /// <summary>
    /// Soft fall (DC 15) — take no damage when unhorsed.
    /// PHB p.80: "You can react to a fall and take no damage."
    /// </summary>
    public static bool TrySoftFall(CharacterStats stats)
    {
        var (success, _, log) = MakeRideCheck(stats, DC_SOFT_FALL, "Soft Fall");
        Debug.Log($"[Mount] {(stats?.CharacterName ?? "?")} {log}");
        return success;
    }

    /// <summary>
    /// Cover (DC 15) — gain half cover from mount (+4 AC, +2 Reflex).
    /// PHB p.80: "You can drop down and hang alongside your mount..."
    /// </summary>
    public static bool TryCover(CharacterStats stats)
    {
        var (success, _, log) = MakeRideCheck(stats, DC_COVER, "Cover");
        Debug.Log($"[Mount] {(stats?.CharacterName ?? "?")} {log}");
        return success;
    }

    /// <summary>
    /// Spur mount (DC 15) — gain +10 ft speed for 1 round, mount takes 1d4 damage.
    /// </summary>
    public static (bool success, string log) TrySpurMount(CharacterController rider, MountInstance mount)
    {
        if (rider == null || rider.Stats == null || mount == null)
            return (false, "Invalid rider/mount");

        var (success, total, checkLog) = MakeRideCheck(rider.Stats, DC_SPUR_MOUNT, "Spur Mount");

        if (success)
        {
            int spurDamage = Random.Range(1, 5); // 1d4
            mount.TakeDamage(spurDamage);
            string log = $"🐎 {rider.Stats.CharacterName} spurs {mount.Data.Name}: +10 ft speed this round! ({mount.Data.Name} takes {spurDamage} damage) — {checkLog}";
            Debug.Log($"[Mount] {log}");
            return (true, log);
        }
        else
        {
            string log = $"🐎 {rider.Stats.CharacterName} fails to spur {mount.Data.Name} — {checkLog}";
            Debug.Log($"[Mount] {log}");
            return (false, log);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // QUERIES
    // ════════════════════════════════════════════════════════════════

    /// <summary>Is this character currently mounted?</summary>
    public static bool IsMounted(CharacterController character)
    {
        return character != null && _mountedCharacters.ContainsKey(character);
    }

    /// <summary>Get the mount instance for a mounted rider. Returns null if not mounted.</summary>
    public static MountInstance GetMount(CharacterController rider)
    {
        if (rider != null && _mountedCharacters.TryGetValue(rider, out MountInstance mount))
            return mount;
        return null;
    }

    /// <summary>Get the rider for a specific mount instance. Returns null if no rider.</summary>
    public static CharacterController GetRider(MountInstance mount)
    {
        if (mount == null) return null;
        foreach (var kvp in _mountedCharacters)
        {
            if (kvp.Value == mount) return kvp.Key;
        }
        return null;
    }

    /// <summary>
    /// Size compatibility check. PHB p.157: rider must be at least one size category smaller.
    /// Small riders can ride Medium mounts, Medium riders can ride Large mounts, etc.
    /// </summary>
    public static bool CanRideMount(CharacterStats riderStats, MountData mountData)
    {
        if (riderStats == null || mountData == null) return false;
        return (int)riderStats.CurrentSizeCategory < (int)mountData.Size;
    }

    /// <summary>
    /// Get the effective mounted movement speed in feet.
    /// Controlled mount uses its own speed. Heavy load reduces speed.
    /// </summary>
    public static int GetMountedSpeed(MountInstance mount)
    {
        if (mount == null || mount.Data == null) return 0;
        return mount.Data.MovementSpeed;
    }

    /// <summary>
    /// Get the mounted AC bonus for a rider vs an unmounted attacker.
    /// PHB p.157: +1 higher ground bonus when mounted vs opponents on foot.
    /// </summary>
    public static int GetMountedACBonus(CharacterController rider, CharacterController attacker)
    {
        if (!IsMounted(rider)) return 0;
        // +1 vs opponents on foot (higher ground advantage)
        if (!IsMounted(attacker)) return 1;
        return 0;
    }

    /// <summary>
    /// Check carrying capacity. Returns the load category based on carried weight.
    /// </summary>
    public static string GetLoadCategory(MountInstance mount, int carriedWeight)
    {
        if (mount == null || mount.Data == null) return "Unknown";
        if (carriedWeight <= mount.Data.LightLoad) return "Light";
        if (carriedWeight <= mount.Data.MediumLoad) return "Medium";
        if (carriedWeight <= mount.Data.HeavyLoad) return "Heavy";
        return "Overloaded";
    }

    /// <summary>
    /// Force dismount a rider (e.g., mount killed, mount panicked, rider knocked off).
    /// Rider may attempt DC 15 Soft Fall to avoid 1d6 falling damage.
    /// </summary>
    public static string ForceDismount(CharacterController rider, bool allowSoftFall = true)
    {
        if (!IsMounted(rider))
            return null;

        MountInstance mount = _mountedCharacters[rider];
        _mountedCharacters.Remove(rider);

        bool softFall = false;
        int fallDamage = 0;
        string fallLog = "";

        if (allowSoftFall && rider.Stats != null)
        {
            softFall = TrySoftFall(rider.Stats);
            if (!softFall)
            {
                fallDamage = Random.Range(1, 7); // 1d6 falling damage
                rider.Stats.TakeDamage(fallDamage);
                fallLog = $" Takes {fallDamage} falling damage!";
            }
            else
            {
                fallLog = " Soft fall — no damage.";
            }
        }

        string log = $"🐎 {rider.Stats?.CharacterName ?? "Rider"} is dismounted from {mount.Data.Name}!{fallLog}";
        Debug.Log($"[Mount] {log}");
        return log;
    }

    /// <summary>Remove all mounts (e.g., end of encounter).</summary>
    public static void ClearAllMounts()
    {
        _mountedCharacters.Clear();
        Debug.Log("[Mount] All mounts cleared.");
    }

    /// <summary>Get all currently mounted characters.</summary>
    public static IEnumerable<CharacterController> GetAllMountedCharacters()
    {
        return _mountedCharacters.Keys;
    }

    /// <summary>Create a new mount instance from a type.</summary>
    public static MountInstance CreateMount(MountType type)
    {
        MountData template = MountDatabase.GetMount(type);
        if (template == null) return null;
        return new MountInstance(template);
    }
}

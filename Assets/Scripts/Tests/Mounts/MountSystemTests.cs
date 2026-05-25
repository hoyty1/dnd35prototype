using UnityEngine;
using System.Collections.Generic;

namespace Tests.Mounts
{
/// <summary>
/// Phase 3 Mount System Tests — validates D&D 3.5e mount mechanics:
///   - Mount database (PHB p.273 stats)
///   - Mount/dismount mechanics (move action, DC 20 fast)
///   - Ride skill checks (DC 5 control, DC 10 fight, DC 15 soft fall, DC 20 fast)
///   - Size compatibility (rider must be smaller than mount)
///   - Mounted Combat feat (negate hit on mount)
///   - Spirited Charge (double/triple damage)
///   - Mounted Archery (halve penalty)
///   - Ride-By Attack, Trample detection
///   - Mounted AC bonus (+1 vs foot)
///   - Carrying capacity
/// </summary>
public static class MountSystemTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== PHASE 3 MOUNT SYSTEM TESTS ==========");

        // Mount Database
        TestMountDatabaseInit();
        TestMountDatabaseStats();
        TestMountNaturalAttacks();

        // Mount Data
        TestMountDataClone();
        TestMountInstance();

        // Size Compatibility
        TestSizeCompatibility();

        // Mount/Dismount
        TestMountAndDismount();
        TestForceDismount();

        // Ride Skill Checks
        TestRideCheckDCs();

        // Mounted Combat Feat
        TestMountedCombatFeatDetection();
        TestMountedCombatNegate();

        // Spirited Charge
        TestSpiritedChargeDamage();
        TestSpiritedChargeLance();

        // Mounted Archery
        TestMountedArcheryPenalty();

        // Ride-By Attack & Trample
        TestRideByAttackDetection();
        TestTrampleDetection();
        TestTrampleHoofAttack();

        // Mounted AC Bonus
        TestMountedACBonus();

        // Carrying Capacity
        TestCarryingCapacity();

        // Feat Summary
        TestMountedFeatSummary();

        Debug.Log($"========== PHASE 3 MOUNT RESULTS: {_passed} passed, {_failed} failed ==========");
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            string extra = string.IsNullOrEmpty(detail) ? "" : $" | {detail}";
            Debug.LogError($"  [FAIL] {testName}{extra}");
        }
    }

    // ── Helper: create CharacterStats ──
    private static CharacterStats MakeStats(string name, int level, string className,
        int str = 14, int dex = 14, int con = 14, int wis = 14, int cha = 10, int bab = 4,
        SizeCategory size = SizeCategory.Medium,
        params string[] feats)
    {
        var stats = new CharacterStats(name, level, className,
            str, dex, con, wis, 10, cha,
            bab, 4, 0,
            8, 1, 0,
            6, 1, level * 8);

        stats.CurrentSizeCategory = size;
        stats.BaseSizeCategory = size;

        if (feats != null && feats.Length > 0)
            stats.AddFeats(new List<string>(feats));

        return stats;
    }

    // =============================================================
    // MOUNT DATABASE TESTS
    // =============================================================

    private static void TestMountDatabaseInit()
    {
        Debug.Log("--- Mount Database Init ---");
        MountDatabase.Init();
        Assert(MountDatabase.Count >= 9, "Mount database has at least 9 mount types",
            $"Actual: {MountDatabase.Count}");
    }

    private static void TestMountDatabaseStats()
    {
        Debug.Log("--- Mount Database Stats (PHB Accuracy) ---");

        // Light Horse
        var lightHorse = MountDatabase.GetMount(MountType.LightHorse);
        Assert(lightHorse != null, "Light Horse exists in database");
        Assert(lightHorse.MovementSpeed == 60, "Light Horse speed is 60 ft",
            $"Actual: {lightHorse.MovementSpeed}");
        Assert(lightHorse.ArmorClass == 13, "Light Horse AC is 13",
            $"Actual: {lightHorse.ArmorClass}");
        Assert(lightHorse.HitPoints == 19, "Light Horse HP is 19",
            $"Actual: {lightHorse.HitPoints}");
        Assert(!lightHorse.IsWarTrained, "Light Horse is NOT war-trained");
        Assert(lightHorse.Size == SizeCategory.Large, "Light Horse is Large");

        // Heavy Warhorse
        var heavyWar = MountDatabase.GetMount(MountType.HeavyWarhorse);
        Assert(heavyWar != null, "Heavy Warhorse exists in database");
        Assert(heavyWar.MovementSpeed == 50, "Heavy Warhorse speed is 50 ft",
            $"Actual: {heavyWar.MovementSpeed}");
        Assert(heavyWar.HitPoints == 30, "Heavy Warhorse HP is 30",
            $"Actual: {heavyWar.HitPoints}");
        Assert(heavyWar.Strength == 18, "Heavy Warhorse STR is 18",
            $"Actual: {heavyWar.Strength}");
        Assert(heavyWar.IsWarTrained, "Heavy Warhorse IS war-trained");

        // Pony (Medium size — suitable for Small riders)
        var pony = MountDatabase.GetMount(MountType.Pony);
        Assert(pony != null, "Pony exists in database");
        Assert(pony.Size == SizeCategory.Medium, "Pony is Medium",
            $"Actual: {pony.Size}");
        Assert(pony.MovementSpeed == 40, "Pony speed is 40 ft",
            $"Actual: {pony.MovementSpeed}");

        // By name lookup
        var byName = MountDatabase.GetMountByName("Heavy Warhorse");
        Assert(byName != null && byName.Type == MountType.HeavyWarhorse, "GetMountByName works for Heavy Warhorse");
    }

    private static void TestMountNaturalAttacks()
    {
        Debug.Log("--- Mount Natural Attacks ---");

        var heavyWar = MountDatabase.GetMount(MountType.HeavyWarhorse);
        Assert(heavyWar.NaturalAttacks != null, "Heavy Warhorse has natural attacks");
        Assert(heavyWar.NaturalAttacks.Length == 3, "Heavy Warhorse has 3 natural attacks (2 hooves + bite)",
            $"Actual: {heavyWar.NaturalAttacks.Length}");

        // First attack should be a hoof
        Assert(heavyWar.NaturalAttacks[0].Name == "Hoof", "First attack is Hoof",
            $"Actual: {heavyWar.NaturalAttacks[0].Name}");
        Assert(heavyWar.NaturalAttacks[0].IsPrimary, "Hoof is primary attack");

        // Bite should be secondary
        Assert(heavyWar.NaturalAttacks[2].Name == "Bite", "Third attack is Bite",
            $"Actual: {heavyWar.NaturalAttacks[2].Name}");
        Assert(!heavyWar.NaturalAttacks[2].IsPrimary, "Bite is secondary attack");

        // Damage roll should be >= 1
        int damage = heavyWar.NaturalAttacks[0].RollDamage();
        Assert(damage >= 1, "Hoof damage roll is at least 1", $"Actual: {damage}");
    }

    // =============================================================
    // MOUNT DATA TESTS
    // =============================================================

    private static void TestMountDataClone()
    {
        Debug.Log("--- Mount Data Clone ---");
        var original = MountDatabase.GetMount(MountType.HeavyWarhorse);
        var clone = original.Clone();

        Assert(clone.Name == original.Name, "Clone has same name");
        Assert(clone.Strength == original.Strength, "Clone has same STR");
        Assert(clone.NaturalAttacks.Length == original.NaturalAttacks.Length, "Clone has same number of attacks");

        // Verify deep copy — modifying clone doesn't affect original
        clone.Strength += 10;
        Assert(clone.Strength != original.Strength, "Modifying clone doesn't affect original");
    }

    private static void TestMountInstance()
    {
        Debug.Log("--- Mount Instance ---");
        var mount = MountSystem.CreateMount(MountType.LightWarhorse);
        Assert(mount != null, "CreateMount returns valid instance");
        Assert(mount.IsAlive, "New mount is alive");
        Assert(mount.CurrentHP == mount.MaxHP, "New mount has full HP");
        Assert(mount.ControlState == MountControlState.Uncontrolled, "New mount is uncontrolled");

        mount.TakeDamage(10);
        Assert(mount.CurrentHP == mount.MaxHP - 10, "Damage reduces HP",
            $"Expected {mount.MaxHP - 10}, got {mount.CurrentHP}");

        mount.Heal(5);
        Assert(mount.CurrentHP == mount.MaxHP - 5, "Healing restores HP",
            $"Expected {mount.MaxHP - 5}, got {mount.CurrentHP}");

        // Kill the mount
        mount.TakeDamage(999);
        Assert(mount.CurrentHP == 0, "Mount HP floors at 0");
        Assert(!mount.IsAlive, "Dead mount is not alive");
    }

    // =============================================================
    // SIZE COMPATIBILITY TESTS
    // =============================================================

    private static void TestSizeCompatibility()
    {
        Debug.Log("--- Size Compatibility ---");
        var mediumRider = MakeStats("MedFighter", 6, "Fighter", size: SizeCategory.Medium);
        var smallRider = MakeStats("Halfling", 6, "Fighter", size: SizeCategory.Small);

        var largeMount = MountDatabase.GetMount(MountType.LightHorse); // Large
        var mediumMount = MountDatabase.GetMount(MountType.Pony);      // Medium

        // Medium rider + Large mount = OK
        Assert(MountSystem.CanRideMount(mediumRider, largeMount), "Medium rider can ride Large mount");

        // Small rider + Medium mount = OK
        Assert(MountSystem.CanRideMount(smallRider, mediumMount), "Small rider can ride Medium mount");

        // Medium rider + Medium mount = FAIL
        Assert(!MountSystem.CanRideMount(mediumRider, mediumMount), "Medium rider cannot ride Medium mount");

        // Small rider + Large mount = OK
        Assert(MountSystem.CanRideMount(smallRider, largeMount), "Small rider can ride Large mount");

        // Null safety
        Assert(!MountSystem.CanRideMount(null, largeMount), "Null rider returns false");
        Assert(!MountSystem.CanRideMount(mediumRider, null), "Null mount data returns false");
    }

    // =============================================================
    // MOUNT / DISMOUNT TESTS
    // =============================================================

    private static void TestMountAndDismount()
    {
        Debug.Log("--- Mount and Dismount ---");

        // Create a test character with a GameObject
        var go = new GameObject("TestRider_Mount");
        var rider = go.AddComponent<CharacterController>();
        rider.Stats = MakeStats("Knight", 6, "Fighter", dex: 14);

        var mount = MountSystem.CreateMount(MountType.HeavyWarhorse);

        // Not mounted initially
        Assert(!MountSystem.IsMounted(rider), "Not mounted initially");

        // Mount
        string mountLog = MountSystem.TryMount(rider, mount);
        Assert(mountLog != null, "Mount succeeds");
        Assert(MountSystem.IsMounted(rider), "Is mounted after mounting");
        Assert(MountSystem.GetMount(rider) == mount, "GetMount returns correct mount");

        // Can't double-mount
        string doubleMountLog = MountSystem.TryMount(rider, mount);
        Assert(doubleMountLog == null, "Cannot mount when already mounted");

        // Dismount
        string dismountLog = MountSystem.TryDismount(rider);
        Assert(dismountLog != null, "Dismount succeeds");
        Assert(!MountSystem.IsMounted(rider), "Not mounted after dismounting");

        // Can't dismount when not mounted
        string badDismount = MountSystem.TryDismount(rider);
        Assert(badDismount == null, "Cannot dismount when not mounted");

        // Cleanup
        Object.DestroyImmediate(go);
        MountSystem.ClearAllMounts();
    }

    private static void TestForceDismount()
    {
        Debug.Log("--- Force Dismount ---");

        var go = new GameObject("TestRider_Force");
        var rider = go.AddComponent<CharacterController>();
        rider.Stats = MakeStats("Knight", 6, "Fighter", dex: 14);

        var mount = MountSystem.CreateMount(MountType.LightHorse);
        MountSystem.TryMount(rider, mount);
        Assert(MountSystem.IsMounted(rider), "Mounted before force dismount");

        string log = MountSystem.ForceDismount(rider, allowSoftFall: false);
        Assert(log != null, "Force dismount produces log");
        Assert(!MountSystem.IsMounted(rider), "Not mounted after force dismount");

        Object.DestroyImmediate(go);
        MountSystem.ClearAllMounts();
    }

    // =============================================================
    // RIDE SKILL CHECK TESTS
    // =============================================================

    private static void TestRideCheckDCs()
    {
        Debug.Log("--- Ride Skill Check DCs ---");

        // Verify DC constants match PHB p.80
        Assert(MountSystem.DC_GUIDE_WITH_KNEES == 5, "Guide with Knees DC is 5");
        Assert(MountSystem.DC_STAY_IN_SADDLE == 5, "Stay in Saddle DC is 5");
        Assert(MountSystem.DC_FIGHT_WITH_WARHORSE == 10, "Fight with Warhorse DC is 10");
        Assert(MountSystem.DC_COVER == 15, "Cover DC is 15");
        Assert(MountSystem.DC_SOFT_FALL == 15, "Soft Fall DC is 15");
        Assert(MountSystem.DC_LEAP == 15, "Leap DC is 15");
        Assert(MountSystem.DC_SPUR_MOUNT == 15, "Spur Mount DC is 15");
        Assert(MountSystem.DC_CONTROL_IN_BATTLE == 20, "Control in Battle DC is 20");
        Assert(MountSystem.DC_FAST_MOUNT_DISMOUNT == 20, "Fast Mount/Dismount DC is 20");

        // MakeRideCheck returns valid structure
        var stats = MakeStats("Rider", 6, "Fighter", dex: 16);
        var (success, total, log) = MountSystem.MakeRideCheck(stats, 5, "test");
        Assert(!string.IsNullOrEmpty(log), "Ride check produces log string");
        Assert(total >= 1, "Ride check total is at least 1 (d20 minimum)");
    }

    // =============================================================
    // MOUNTED COMBAT FEAT TESTS
    // =============================================================

    private static void TestMountedCombatFeatDetection()
    {
        Debug.Log("--- Mounted Combat Feat Detection ---");
        var withFeat = MakeStats("Knight", 6, "Fighter",
            feats: new[] { "Mounted Combat" });
        var withoutFeat = MakeStats("Fighter", 6, "Fighter");

        Assert(FeatManager.HasMountedCombat(withFeat), "Character with Mounted Combat detected");
        Assert(!FeatManager.HasMountedCombat(withoutFeat), "Character without Mounted Combat not detected");
        Assert(FeatManager.HasRideByAttack(
            MakeStats("K", 6, "Fighter", feats: new[] { "Ride-By Attack" })),
            "Ride-By Attack detected");
        Assert(FeatManager.HasSpiritedCharge(
            MakeStats("K", 6, "Fighter", feats: new[] { "Spirited Charge" })),
            "Spirited Charge detected");
        Assert(FeatManager.HasTrample(
            MakeStats("K", 6, "Fighter", feats: new[] { "Trample" })),
            "Trample detected");
        Assert(FeatManager.HasMountedArchery(
            MakeStats("K", 6, "Fighter", feats: new[] { "Mounted Archery" })),
            "Mounted Archery detected");
    }

    private static void TestMountedCombatNegate()
    {
        Debug.Log("--- Mounted Combat Negate ---");

        var go = new GameObject("TestRider_MC");
        var rider = go.AddComponent<CharacterController>();
        rider.Stats = MakeStats("Knight", 10, "Fighter",
            feats: new[] { "Mounted Combat" });

        var mount = MountSystem.CreateMount(MountType.HeavyWarhorse);
        MountSystem.TryMount(rider, mount);

        // First use should succeed or fail (but be allowed)
        rider.Stats.HasUsedMountedCombatThisRound = false;
        var (negated1, log1) = MountedCombatSystem.TryMountedCombatNegate(rider, mount, 10);
        Assert(!string.IsNullOrEmpty(log1), "Mounted Combat produces log");
        Assert(rider.Stats.HasUsedMountedCombatThisRound, "Mounted Combat marked as used this round");

        // Second use should fail (once per round)
        var (negated2, log2) = MountedCombatSystem.TryMountedCombatNegate(rider, mount, 10);
        Assert(!negated2, "Second Mounted Combat attempt fails (once per round)");
        Assert(log2.Contains("already used"), "Log indicates already used",
            $"Log: {log2}");

        // Without feat
        var go2 = new GameObject("TestRider_NoMC");
        var noFeatRider = go2.AddComponent<CharacterController>();
        noFeatRider.Stats = MakeStats("Peasant", 1, "Fighter");
        var (negated3, log3) = MountedCombatSystem.TryMountedCombatNegate(noFeatRider, mount, 10);
        Assert(!negated3, "Cannot use Mounted Combat without feat");

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(go2);
        MountSystem.ClearAllMounts();
    }

    // =============================================================
    // SPIRITED CHARGE TESTS
    // =============================================================

    private static void TestSpiritedChargeDamage()
    {
        Debug.Log("--- Spirited Charge Damage ---");

        // With Spirited Charge feat, non-lance weapon → ×2
        var riderStats = MakeStats("Charger", 8, "Fighter",
            feats: new[] { "Spirited Charge", "Ride-By Attack", "Mounted Combat" });
        int multiplier = MountedCombatSystem.GetSpiritedChargeDamageMultiplier(riderStats, "Longsword");
        Assert(multiplier == 2, "Spirited Charge with longsword gives ×2",
            $"Actual: {multiplier}");

        // Without feat → ×1
        var noFeat = MakeStats("Fighter", 8, "Fighter");
        int noMultiplier = MountedCombatSystem.GetSpiritedChargeDamageMultiplier(noFeat, "Longsword");
        Assert(noMultiplier == 1, "Without Spirited Charge gives ×1",
            $"Actual: {noMultiplier}");
    }

    private static void TestSpiritedChargeLance()
    {
        Debug.Log("--- Spirited Charge Lance ---");

        var riderStats = MakeStats("Lancer", 8, "Fighter",
            feats: new[] { "Spirited Charge", "Ride-By Attack", "Mounted Combat" });

        // Lance → ×3
        int lanceMultiplier = MountedCombatSystem.GetSpiritedChargeDamageMultiplier(riderStats, "Lance");
        Assert(lanceMultiplier == 3, "Spirited Charge with lance gives ×3",
            $"Actual: {lanceMultiplier}");

        // Heavy Lance also counts
        int heavyLance = MountedCombatSystem.GetSpiritedChargeDamageMultiplier(riderStats, "Heavy Lance");
        Assert(heavyLance == 3, "Spirited Charge with Heavy Lance gives ×3",
            $"Actual: {heavyLance}");

        // Apply to actual damage
        int baseDamage = 10;
        var go = new GameObject("TestRider_Lance");
        var rider = go.AddComponent<CharacterController>();
        rider.Stats = riderStats;
        var mount = MountSystem.CreateMount(MountType.HeavyWarhorse);
        MountSystem.TryMount(rider, mount);

        int result = MountedCombatSystem.ApplySpiritedChargeDamage(rider, baseDamage, "Lance");
        Assert(result == 30, "10 base damage × 3 (lance) = 30",
            $"Actual: {result}");

        int swordResult = MountedCombatSystem.ApplySpiritedChargeDamage(rider, baseDamage, "Longsword");
        Assert(swordResult == 20, "10 base damage × 2 (sword) = 20",
            $"Actual: {swordResult}");

        Object.DestroyImmediate(go);
        MountSystem.ClearAllMounts();
    }

    // =============================================================
    // MOUNTED ARCHERY TESTS
    // =============================================================

    private static void TestMountedArcheryPenalty()
    {
        Debug.Log("--- Mounted Archery Penalty ---");

        // Without feat: -4 penalty
        var go1 = new GameObject("TestRider_NoMA");
        var noFeat = go1.AddComponent<CharacterController>();
        noFeat.Stats = MakeStats("Archer", 6, "Fighter");
        var mount1 = MountSystem.CreateMount(MountType.LightHorse);
        MountSystem.TryMount(noFeat, mount1);

        int noFeatPenalty = MountedCombatSystem.GetMountedRangedPenalty(noFeat, false);
        Assert(noFeatPenalty == -4, "Without Mounted Archery: -4 penalty",
            $"Actual: {noFeatPenalty}");

        // Running: -8
        int noFeatRunning = MountedCombatSystem.GetMountedRangedPenalty(noFeat, true);
        Assert(noFeatRunning == -8, "Without Mounted Archery, running: -8",
            $"Actual: {noFeatRunning}");

        Object.DestroyImmediate(go1);
        MountSystem.ClearAllMounts();

        // With feat: -2 penalty
        var go2 = new GameObject("TestRider_MA");
        var withFeat = go2.AddComponent<CharacterController>();
        withFeat.Stats = MakeStats("HorseArcher", 6, "Fighter",
            feats: new[] { "Mounted Archery", "Mounted Combat" });
        var mount2 = MountSystem.CreateMount(MountType.LightHorse);
        MountSystem.TryMount(withFeat, mount2);

        int featPenalty = MountedCombatSystem.GetMountedRangedPenalty(withFeat, false);
        Assert(featPenalty == -2, "With Mounted Archery: -2 penalty",
            $"Actual: {featPenalty}");

        int featRunning = MountedCombatSystem.GetMountedRangedPenalty(withFeat, true);
        Assert(featRunning == -4, "With Mounted Archery, running: -4",
            $"Actual: {featRunning}");

        // Not mounted: 0 penalty
        MountSystem.TryDismount(withFeat);
        int unmountedPenalty = MountedCombatSystem.GetMountedRangedPenalty(withFeat);
        Assert(unmountedPenalty == 0, "Not mounted: 0 penalty",
            $"Actual: {unmountedPenalty}");

        Object.DestroyImmediate(go2);
        MountSystem.ClearAllMounts();
    }

    // =============================================================
    // RIDE-BY ATTACK & TRAMPLE TESTS
    // =============================================================

    private static void TestRideByAttackDetection()
    {
        Debug.Log("--- Ride-By Attack Detection ---");

        var go = new GameObject("TestRider_RBA");
        var rider = go.AddComponent<CharacterController>();
        rider.Stats = MakeStats("Knight", 6, "Fighter",
            feats: new[] { "Ride-By Attack", "Mounted Combat" });
        var mount = MountSystem.CreateMount(MountType.HeavyWarhorse);
        MountSystem.TryMount(rider, mount);

        Assert(MountedCombatSystem.CanUseRideByAttack(rider), "Mounted rider with Ride-By Attack can use it");

        MountSystem.TryDismount(rider);
        Assert(!MountedCombatSystem.CanUseRideByAttack(rider), "Unmounted rider cannot use Ride-By Attack");

        // Check remaining movement
        MountSystem.TryMount(rider, mount);
        int remaining = MountedCombatSystem.GetRideByRemainingMovement(rider, 40);
        // Heavy warhorse speed = 50, double = 100, moved 40 → 60 remaining
        Assert(remaining == 60, "Ride-By remaining movement after 40ft charge = 60ft",
            $"Actual: {remaining}");

        Object.DestroyImmediate(go);
        MountSystem.ClearAllMounts();
    }

    private static void TestTrampleDetection()
    {
        Debug.Log("--- Trample Detection ---");

        var go = new GameObject("TestRider_Trample");
        var rider = go.AddComponent<CharacterController>();
        rider.Stats = MakeStats("Knight", 6, "Fighter",
            feats: new[] { "Trample", "Mounted Combat" });
        var mount = MountSystem.CreateMount(MountType.HeavyWarhorse);
        MountSystem.TryMount(rider, mount);

        Assert(MountedCombatSystem.CanUseTrample(rider), "Mounted rider with Trample can use it");

        MountSystem.TryDismount(rider);
        Assert(!MountedCombatSystem.CanUseTrample(rider), "Unmounted rider cannot use Trample");

        Object.DestroyImmediate(go);
        MountSystem.ClearAllMounts();
    }

    private static void TestTrampleHoofAttack()
    {
        Debug.Log("--- Trample Hoof Attack ---");

        var riderGO = new GameObject("TestRider_TH");
        var rider = riderGO.AddComponent<CharacterController>();
        rider.Stats = MakeStats("Knight", 8, "Fighter",
            feats: new[] { "Trample", "Mounted Combat" });

        var targetGO = new GameObject("TestTarget_TH");
        var target = targetGO.AddComponent<CharacterController>();
        target.Stats = MakeStats("Goblin", 1, "Fighter", str: 8, dex: 10, con: 10);

        var mount = MountSystem.CreateMount(MountType.HeavyWarhorse);
        MountSystem.TryMount(rider, mount);

        int hpBefore = target.Stats.CurrentHP;
        var (hit, damage, log) = MountedCombatSystem.ExecuteTrampleHoofAttack(rider, target);
        Assert(!string.IsNullOrEmpty(log), "Trample hoof attack produces log");
        // Can't assert hit/miss deterministically due to random, but verify structure
        if (hit)
        {
            Assert(damage >= 1, "Trample hit deals at least 1 damage", $"Actual: {damage}");
            Assert(target.Stats.CurrentHP < hpBefore, "Target HP decreased after trample hit");
        }

        Object.DestroyImmediate(riderGO);
        Object.DestroyImmediate(targetGO);
        MountSystem.ClearAllMounts();
    }

    // =============================================================
    // MOUNTED AC BONUS TESTS
    // =============================================================

    private static void TestMountedACBonus()
    {
        Debug.Log("--- Mounted AC Bonus ---");

        var mountedGO = new GameObject("TestRider_AC");
        var mountedChar = mountedGO.AddComponent<CharacterController>();
        mountedChar.Stats = MakeStats("Knight", 6, "Fighter");

        var footGO = new GameObject("TestFoot_AC");
        var footChar = footGO.AddComponent<CharacterController>();
        footChar.Stats = MakeStats("Soldier", 6, "Fighter");

        var mount = MountSystem.CreateMount(MountType.HeavyWarhorse);
        MountSystem.TryMount(mountedChar, mount);

        // Mounted vs foot: +1 AC
        int bonus = MountSystem.GetMountedACBonus(mountedChar, footChar);
        Assert(bonus == 1, "Mounted rider gets +1 AC vs opponent on foot",
            $"Actual: {bonus}");

        // Foot vs foot: no bonus
        int noBonus = MountSystem.GetMountedACBonus(footChar, mountedChar);
        Assert(noBonus == 0, "Unmounted character gets no mounted AC bonus",
            $"Actual: {noBonus}");

        Object.DestroyImmediate(mountedGO);
        Object.DestroyImmediate(footGO);
        MountSystem.ClearAllMounts();
    }

    // =============================================================
    // CARRYING CAPACITY TESTS
    // =============================================================

    private static void TestCarryingCapacity()
    {
        Debug.Log("--- Carrying Capacity ---");

        var mount = MountSystem.CreateMount(MountType.HeavyWarhorse);
        // Heavy Warhorse: Light 300, Medium 600, Heavy 900

        Assert(MountSystem.GetLoadCategory(mount, 100) == "Light", "100 lbs = Light load");
        Assert(MountSystem.GetLoadCategory(mount, 300) == "Light", "300 lbs = Light load (boundary)");
        Assert(MountSystem.GetLoadCategory(mount, 301) == "Medium", "301 lbs = Medium load");
        Assert(MountSystem.GetLoadCategory(mount, 600) == "Medium", "600 lbs = Medium load (boundary)");
        Assert(MountSystem.GetLoadCategory(mount, 601) == "Heavy", "601 lbs = Heavy load");
        Assert(MountSystem.GetLoadCategory(mount, 900) == "Heavy", "900 lbs = Heavy load (boundary)");
        Assert(MountSystem.GetLoadCategory(mount, 901) == "Overloaded", "901 lbs = Overloaded");

        MountSystem.ClearAllMounts();
    }

    // =============================================================
    // FEAT SUMMARY TESTS
    // =============================================================

    private static void TestMountedFeatSummary()
    {
        Debug.Log("--- Mounted Feat Summary ---");

        var knight = MakeStats("Knight", 8, "Fighter",
            feats: new[] { "Mounted Combat", "Ride-By Attack", "Spirited Charge", "Trample", "Mounted Archery" });
        string summary = FeatManager.GetFeatSummary(knight);

        Assert(summary.Contains("Mounted Combat"), "Summary includes Mounted Combat");
        Assert(summary.Contains("Ride-By Attack"), "Summary includes Ride-By Attack");
        Assert(summary.Contains("Spirited Charge"), "Summary includes Spirited Charge");
        Assert(summary.Contains("Trample"), "Summary includes Trample");
        Assert(summary.Contains("Mounted Archery"), "Summary includes Mounted Archery");
    }
}
}

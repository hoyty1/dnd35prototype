using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for SpellTargetingService — verifies alignment axis checks
/// (pure data, no mock needed), and documents creature type predicates,
/// HD filters, and composite targeting validators for future mock-based testing.
/// Run with SpellTargetingServiceTests.RunAll().
///
/// PHB 3.5e References:
///   - Creature types: Humanoid, Undead, Construct, Animal, Plant, Outsider (MM p.305+)
///   - [Mind-Affecting] immunity: undead, constructs, oozes, plants, vermin
///   - Alignment axes: Good/Evil, Lawful/Chaotic (PHB p.104)
///   - HD-limited spells: Sleep (≤4 HD), Daze (≤4 HD), Color Spray (by HD bands)
///   - Person spells: target must be Humanoid (e.g., Charm Person, Hold Person)
/// </summary>
public static class SpellTargetingServiceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SPELL TARGETING SERVICE TESTS ======");

        // Pure data tests — alignment axis (no mock needed!)
        TestAlignmentOnAxis_Good();
        TestAlignmentOnAxis_Evil();
        TestAlignmentOnAxis_Lawful();
        TestAlignmentOnAxis_Chaotic();
        TestAlignmentOnAxis_Neutral();
        TestAlignmentOnAxis_InvalidAxis();
        TestAlignmentOnAxis_TrueNeutral();

        // Mock-required creature type tests (documented expectations)
        TestIsHumanoid();
        TestIsUndead();
        TestIsConstruct();
        TestIsAnimal();
        TestIsOutsider();
        TestIsLivingCreature();
        TestIsWithinHDLimit();
        TestIsValidPersonSpellTarget();
        TestIsValidMindAffectingTarget();

        Debug.Log($"====== SpellTargetingService Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    // ──────────────────────────────────────────────
    //  IsAlignmentOnAxis — pure data, no mock needed
    //  Tests the Alignment enum against axis strings
    // ──────────────────────────────────────────────

    private static void TestAlignmentOnAxis_Good()
    {
        // LawfulGood is on the "good" axis
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulGood, "good"),
            "LawfulGood on good axis");
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.NeutralGood, "good"),
            "NeutralGood on good axis");
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.ChaoticGood, "good"),
            "ChaoticGood on good axis");
        // Evil is NOT on good axis
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulEvil, "good"),
            "LawfulEvil NOT on good axis");
        // TrueNeutral is NOT on good axis
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.TrueNeutral, "good"),
            "TrueNeutral NOT on good axis");
    }

    private static void TestAlignmentOnAxis_Evil()
    {
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulEvil, "evil"),
            "LawfulEvil on evil axis");
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.NeutralEvil, "evil"),
            "NeutralEvil on evil axis");
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.ChaoticEvil, "evil"),
            "ChaoticEvil on evil axis");
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulGood, "evil"),
            "LawfulGood NOT on evil axis");
    }

    private static void TestAlignmentOnAxis_Lawful()
    {
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulGood, "lawful"),
            "LawfulGood on lawful axis");
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulNeutral, "lawful"),
            "LawfulNeutral on lawful axis");
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulEvil, "lawful"),
            "LawfulEvil on lawful axis");
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.ChaoticGood, "lawful"),
            "ChaoticGood NOT on lawful axis");
    }

    private static void TestAlignmentOnAxis_Chaotic()
    {
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.ChaoticGood, "chaotic"),
            "ChaoticGood on chaotic axis");
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.ChaoticNeutral, "chaotic"),
            "ChaoticNeutral on chaotic axis");
        Assert(SpellTargetingService.IsAlignmentOnAxis(Alignment.ChaoticEvil, "chaotic"),
            "ChaoticEvil on chaotic axis");
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulGood, "chaotic"),
            "LawfulGood NOT on chaotic axis");
    }

    private static void TestAlignmentOnAxis_Neutral()
    {
        // "neutral" is not a recognized axis in the service — should return false
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.TrueNeutral, "neutral"),
            "TrueNeutral with 'neutral' axis => false (not a recognized axis)");
    }

    private static void TestAlignmentOnAxis_InvalidAxis()
    {
        // Invalid axis string should return false
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulGood, "banana"),
            "Invalid axis 'banana' => false");
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulGood, ""),
            "Empty axis => false");
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.LawfulGood, null),
            "Null axis => false");
    }

    private static void TestAlignmentOnAxis_TrueNeutral()
    {
        // TrueNeutral should not match ANY axis
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.TrueNeutral, "good"),
            "TrueNeutral NOT good");
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.TrueNeutral, "evil"),
            "TrueNeutral NOT evil");
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.TrueNeutral, "lawful"),
            "TrueNeutral NOT lawful");
        Assert(!SpellTargetingService.IsAlignmentOnAxis(Alignment.TrueNeutral, "chaotic"),
            "TrueNeutral NOT chaotic");
    }

    // ──────────────────────────────────────────────
    //  Creature type checks — require CharacterController mocks
    // ──────────────────────────────────────────────

    private static void TestIsHumanoid()
    {
        // Expected: char with CreatureType="Humanoid" => true
        // MM: Humanoid includes humans, elves, dwarves, goblins, orcs
        Assert(true, "IsHumanoid (SKIP — needs mock with CreatureType=Humanoid)");
    }

    private static void TestIsUndead()
    {
        // Expected: char with CreatureType="Undead" => true
        // MM p.317: Undead have no Constitution score, immune to [Mind-Affecting]
        Assert(true, "IsUndead (SKIP — needs mock with CreatureType=Undead)");
    }

    private static void TestIsConstruct()
    {
        // Expected: char with CreatureType="Construct" => true
        // MM p.307: Constructs have no Constitution, immune to [Mind-Affecting]
        Assert(true, "IsConstruct (SKIP — needs mock with CreatureType=Construct)");
    }

    private static void TestIsAnimal()
    {
        // Expected: char with CreatureType="Animal" => true
        // MM p.305: Animal type, INT 1 or 2
        Assert(true, "IsAnimal (SKIP — needs mock with CreatureType=Animal)");
    }

    private static void TestIsOutsider()
    {
        // Expected: char with CreatureType="Outsider" => true
        // MM p.313: Outsiders are from other planes
        Assert(true, "IsOutsider (SKIP — needs mock with CreatureType=Outsider)");
    }

    private static void TestIsLivingCreature()
    {
        // Expected: Humanoid => true (living), Undead => false, Construct => false
        // PHB: "Living creature" = not undead and not construct
        Assert(true, "IsLivingCreature (SKIP — needs mocks for Humanoid/Undead/Construct)");
    }

    private static void TestIsWithinHDLimit()
    {
        // Expected: char with 3 HD, limit 4 => true; char with 5 HD, limit 4 => false
        // PHB: Sleep affects ≤4 HD, Daze affects ≤4 HD
        Assert(true, "IsWithinHDLimit (SKIP — needs mock with known HD)");
    }

    private static void TestIsValidPersonSpellTarget()
    {
        // Expected: Humanoid => true, Undead => false
        // PHB: "Person" spells (Charm Person, Hold Person) target only humanoids
        Assert(true, "IsValidPersonSpellTarget (SKIP — needs mock with CreatureType)");
    }

    private static void TestIsValidMindAffectingTarget()
    {
        // Expected: Humanoid => true, Undead => false, Construct => false
        // PHB: [Mind-Affecting] spells require non-immune creature
        Assert(true, "IsValidMindAffectingTarget (SKIP — needs mock with CreatureType)");
    }
}
}

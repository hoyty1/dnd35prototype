using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Convenience runner — executes all Phase 4 service test suites.
/// Call ServiceTestRunner.RunAll() from a debug button or console.
/// </summary>
public static class ServiceTestRunner
{
    public static void RunAll()
    {
        Debug.Log("╔══════════════════════════════════════════╗");
        Debug.Log("║   Phase 4 — Service Layer Test Suite     ║");
        Debug.Log("╚══════════════════════════════════════════╝");

        SpellUtilitiesTests.RunAll();
        SpellCastingHelperTests.RunAll();
        TeamUtilityTests.RunAll();
        ConcentrationServiceTests.RunAll();
        DispelMagicServiceTests.RunAll();
        CombatLogHelperTests.RunAll();
        SpellTargetingServiceTests.RunAll();
        CombatCalculationServiceTests.RunAll();

        // Pre-existing service tests (not from Phase 4 but included for completeness)
        DiceServiceTests.RunAll();

        Debug.Log("╔══════════════════════════════════════════╗");
        Debug.Log("║   All service tests complete.            ║");
        Debug.Log("╚══════════════════════════════════════════╝");
    }
}
}

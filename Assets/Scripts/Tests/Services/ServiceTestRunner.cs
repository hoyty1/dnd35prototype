using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Convenience runner — executes all Phase 4/5 service test suites.
/// Call ServiceTestRunner.RunAll() from a debug button or console.
///
/// Phase 5C update: all test files now contain real assertions where possible
/// (pure-function tests), with documented placeholders for mock-dependent tests.
///
/// Test coverage summary:
///   - SpellUtilitiesTests:            9 real + 4 mock-skip = 13 tests
///   - SpellCastingHelperTests:        8 real                = 8 tests
///   - TeamUtilityTests:               0 real + 10 mock-skip = 10 tests
///   - ConcentrationServiceTests:      14 real               = 14 tests
///   - DispelMagicServiceTests:        10 real + 1 mock-skip = 11 tests
///   - CombatLogHelperTests:           30+ real              = 30+ tests
///   - SpellTargetingServiceTests:     7 real + 9 mock-skip  = 16 tests
///   - CombatCalculationServiceTests:  35+ real              = 35+ tests
///   ─────────────────────────────────────────────────────────────
///   Total:                            113+ real assertions + 24 mock-skip
/// </summary>
public static class ServiceTestRunner
{
    public static void RunAll()
    {
        Debug.Log("╔══════════════════════════════════════════╗");
        Debug.Log("║   Phase 5C — Service Layer Test Suite    ║");
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

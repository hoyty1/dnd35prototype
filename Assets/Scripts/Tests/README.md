# D&D 3.5e Prototype - Test Suite

## Directory Structure

- **Core/**: Core combat flow and turn-sequencing tests (reserved for cross-system tests)
- **Combat/**: Combat mechanics and attack behavior tests
- **Maneuvers/**: Special maneuver tests (grapple, overrun, sunder, etc.)
- **Character/**: Character-system tests (encumbrance, proficiencies, penalties)
- **Utilities/**: Shared helper and fixture utilities for runtime tests

## Current Test File Organization

### Combat
- `DamageModifierTests.cs`
- `DualWieldPenaltyTests.cs`
- `FlankingReachRulesTests.cs`
- `NonlethalDamageTests.cs`
- `RangeCalculatorTests.cs`
- `RapidShotTests.cs`
- `ReachWeaponRulesTests.cs`
- `UnarmedDamageModeTests.cs`

### Maneuvers
- `GrappleDamageRulesTests.cs`
- `OverrunRulesTests.cs`
- `SunderInventoryRemovalTests.cs`

### Character
- `EncumbranceTests.cs`
- `ProficiencyAndAcpTests.cs`

## Utilities

### `TestHelpers`
Common setup helpers:
- Database initialization
- Character + stats creation shortcuts
- Grid positioning
- Action reset
- Cleanup helpers

### `MockCharacterFactory`
Reusable mock archetypes:
- Commoner
- Fighter
- Skeleton
- Goblin

### `TestFixtures`
Reusable runtime fixture bases:
- `D35TestBase` for common setup/teardown and object tracking
- `CombatTestBase` for attacker/defender test scaffolding

## Naming Convention

- File names use **PascalCase** and end with **`Tests.cs`**.
- Namespaces match category folders:
  - `Tests.Combat`
  - `Tests.Maneuvers`
  - `Tests.Character`
  - `Tests.Utilities`

## Runtime Usage Pattern

Most current tests are runtime-driven static suites with `RunAll()`/`RunAllTests()` entry points.

Example usage from a temporary test runner MonoBehaviour:

```csharp
using Tests.Maneuvers;

public class TempRunner : MonoBehaviour
{
    private void Start()
    {
        OverrunRulesTests.RunAll();
    }
}
```


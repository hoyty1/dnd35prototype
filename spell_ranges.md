# D&D 3.5E SPELL RANGES

## Standard Range Categories

### Close Range
- Formula: 5 squares + 1 square per 2 caster levels
- D&D 3.5e equivalent: 25 ft + 5 ft/2 levels

| CL | Range (sq) | Range (ft) |
|----|------------|------------|
| 1  | 5          | 25         |
| 2  | 6          | 30         |
| 5  | 7          | 35         |
| 10 | 10         | 50         |
| 20 | 15         | 75         |

### Medium Range
- Formula: 20 squares + 2 squares per caster level
- D&D 3.5e equivalent: 100 ft + 10 ft/level

| CL | Range (sq) | Range (ft) |
|----|------------|------------|
| 1  | 22         | 110        |
| 5  | 30         | 150        |
| 10 | 40         | 200        |
| 20 | 60         | 300        |

### Long Range
- Formula: 80 squares + 8 squares per caster level
- D&D 3.5e equivalent: 400 ft + 40 ft/level

| CL | Range (sq) | Range (ft) |
|----|------------|------------|
| 1  | 88         | 440        |
| 5  | 120        | 600        |
| 10 | 160        | 800        |
| 20 | 240        | 1200       |

## Usage in Code

```csharp
// Create spell data preconfigured with a standard category.
var spell = SpellData.CreateWithRange(SpellRangeCategory.Medium);

// Configure an existing spell.
spell.SetRange(SpellRangeCategory.Long);

// Convenience helpers.
spell.SetRangeClose();
spell.SetRangeMedium();
spell.SetRangeLong();
```

## Notes
- `SpellRangeCategory.Custom` leaves existing manual range values unchanged.
- `GetRangeSquaresForCasterLevel(int casterLevel)` remains the source of truth for dynamic scaling.

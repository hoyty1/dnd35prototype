# D&D 3.5e PHB Spell Range Audit Report

## Audit Scope
- Repository: `/home/ubuntu/dnd35prototype`
- Files audited: all `Assets/Scripts/Magic/SpellDatabase_*.cs`
- Audited target: all **implemented** spells (`IsPlaceholder != true`)
- Total implemented spells discovered: **90**
- PHB-reference comparable spells in current implementation: **84**
- Non-PHB/custom spells excluded from PHB mismatch scoring: **6**
  - `acid_fog`, `chromatic_orb`, `electric_jolt`, `test_cone_30`, `test_cone_60`, `test_line_60`

## PHB Range Model Used
- **Personal**: `RangeSquares = -1`
- **Touch**: `RangeSquares = 1`
- **Close**: `RangeSquares = 5`, `RangeIncreasePerLevels = 2`, `RangeIncreaseSquares = 1`
- **Medium**: `RangeSquares = 20`, `RangeIncreasePerLevels = 1`, `RangeIncreaseSquares = 2`
- **Long**: `RangeSquares = 80`, `RangeIncreasePerLevels = 1`, `RangeIncreaseSquares = 8`
- **Fixed/special** examples retained where appropriate (e.g., cone lengths, self-centered burst, fixed feet ranges)

## Findings (Before Fix)
- PHB-comparable implemented spells with incorrect range definitions: **30**

### Incorrect Cleric Ranges (fixed)
- `doom` (to Medium scaling)
- `command` (to Close scaling)
- `cause_fear_clr` (to Close scaling)
- `remove_fear` (to Close scaling)
- `spiritual_weapon` (to Medium scaling)
- `sound_burst` (to Close scaling)
- `flame_strike` (to Medium scaling)
- `shield_other` (to Close scaling)
- `hold_person` (to Medium scaling)
- `silence` (to Long scaling)

### Incorrect Wizard Ranges (fixed)
- `ray_of_frost` (to Close scaling)
- `disrupt_undead` (to Close scaling)
- `flare` (to Close scaling)
- `mage_armor` (Personal -> Touch)
- `enlarge_person` (to Close scaling)
- `sleep` (to Medium scaling)
- `grease` (to Close scaling)
- `cause_fear` (to Close scaling)
- `ray_of_enfeeblement` (to Close scaling)
- `reduce_person` (to Close scaling)
- `flaming_sphere` (to Medium scaling)
- `shatter` (to Close scaling)
- `web` (to Medium scaling)
- `glitterdust` (to Medium scaling)
- `blindness_deafness_wiz` (to Medium scaling)
- `daze_monster` (to Medium scaling)
- `hideous_laughter` (to Close scaling)
- `scare` (to Medium scaling)
- `spectral_hand` (Personal -> Medium scaling)
- `blur` (Touch -> Personal)

## Validation (After Fix)
- Re-audited all implemented spells with the same PHB comparator map.
- PHB-comparable mismatches remaining: **0**

## Files Modified
- `Assets/Scripts/Magic/SpellDatabase_ClericLevel1.cs`
- `Assets/Scripts/Magic/SpellDatabase_ClericLevel2.cs`
- `Assets/Scripts/Magic/SpellDatabase_WizardCantrips.cs`
- `Assets/Scripts/Magic/SpellDatabase_WizardLevel1.cs`
- `Assets/Scripts/Magic/SpellDatabase_WizardLevel2.cs`

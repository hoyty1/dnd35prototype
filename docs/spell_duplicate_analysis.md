# Spell Duplicate Analysis Report

**Date:** May 27, 2026  
**Project:** D&D 3.5e Prototype (`/home/ubuntu/dnd35prototype`)  
**Scope:** All spell definitions in `Assets/Scripts/Magic/Spells/Databases/` and `Assets/Scripts/Identifiers/SpellNames.cs`

---

## Summary

| Metric | Count |
|--------|-------|
| Total SpellData entries registered | 296 |
| Total class/alias registrations | 137 |
| Total SpellNames constants | 305 |
| **Duplicate display names** (same `Name`, different `SpellId`) | **4 spells → 9 entries** |
| Domain-prefixed full duplicates | 12 entries |
| Class-suffixed full duplicates | 6 entries |
| **Total entries consolidatable** | **~18 entries → ~6 canonical spells** |

---

## Category 1: Domain-Prefixed Duplicates (`domain_*`)

These are Druid/specialty spells that Clerics access via domain spell lists. They exist as **separate full SpellData entries** rather than using the existing `AvailableFor` + `AddAvailability()` system.

### True Duplicates (base spell EXISTS — identical mechanics, different SpellId)

| Domain SpellId | Base SpellId | Spell Name | Domain Level | Base Level | Domain File | Base File |
|---|---|---|---|---|---|---|
| `domain_barkskin` | `barkskin` | Barkskin | 2 | 2 | SpellDatabase_B.cs | SpellDatabase_B.cs |
| `domain_wind_wall` | `wind_wall` | Wind Wall | **2** | **3** | SpellDatabase_W.cs | SpellDatabase_W.cs |

> **Wind Wall** has different spell levels (domain grants it at level 2, base Wizard/Druid version is level 3). This is correct per PHB — domain spells can grant lower-level access. The `AvailableFor` system already supports per-class levels.

### Domain-Only Spells (NO base version exists)

These are primarily Druid spells that only appear in the codebase as `domain_*` entries, granted to Clerics via domains. They have **no base Druid/Ranger version** registered.

| SpellId | Spell Name (inferred) | Level | File | External References |
|---|---|---|---|---|
| `domain_calm_animals` | Calm Animals | 1 | SpellDatabase_C.cs | 2 |
| `domain_desecrate` | Desecrate | 2 | SpellDatabase_D.cs | 6 |
| `domain_detect_secret_doors` | Detect Secret Doors | 1 | SpellDatabase_D.cs | 1 |
| `domain_entangle` | Entangle | 1 | SpellDatabase_E.cs | 4 |
| `domain_heat_metal` | Heat Metal | 2 | SpellDatabase_H.cs | 2 |
| `domain_hold_animal` | Hold Animal | 2 | SpellDatabase_H.cs | 2 |
| `domain_longstrider` | Longstrider | 1 | SpellDatabase_L.cs | 1 |
| `domain_magic_stone` | Magic Stone | 1 | SpellDatabase_M.cs | 7 |
| `domain_produce_flame` | Produce Flame | 2 | SpellDatabase_P.cs | 2 |
| `domain_soften_earth` | Soften Earth and Stone | 2 | SpellDatabase_S.cs | 3 |

> **Note:** These are NOT true duplicates — they're the *only* registration of these spells. The `domain_` prefix is misleading because it implies these are domain-specific copies of existing spells. In reality, the base Druid versions were never registered. Removing the `domain_` prefix and registering proper base + domain availability would be the correct consolidation.

---

## Category 2: Class-Suffixed Duplicates (`*_wiz`, `*_clr`, `*_brd`)

These are spells that exist as **separate full SpellData entries per class** because the spell level differs by class.

### Blindness/Deafness (3 entries)

| SpellId | Classes | Level | File |
|---|---|---|---|
| `blindness_deafness_wiz` | Wizard, Sorcerer | 2 | SpellDatabase_B.cs:304 |
| `blindness_deafness_brd` | Bard | 2 | SpellDatabase_B.cs:326 |
| `blindness_deafness_clr` | Cleric | **3** | SpellDatabase_B.cs:348 |

> Wiz/Sor and Bard are both level 2, but Cleric is level 3. Could be consolidated into 1-2 entries using `AvailableFor`.

### Single-Class Suffixed (no base version)

| SpellId | Spell Name | Level | File |
|---|---|---|---|
| `detect_magic_wiz` | Detect Magic | 0 | SpellDatabase_D.cs:316 |
| `detect_poison_wiz` | Detect Poison | 0 | SpellDatabase_D.cs:335 |
| `resistance_wiz` | Resistance | 0 | SpellDatabase_R.cs:282 |

> These have only the `_wiz` suffix — no base version exists. The `_wiz` suffix is unnecessary since `AvailableFor` already handles multi-class availability. The suffix just adds confusion.

---

## Category 3: Alias System Usage

The codebase has a well-designed **alias system** (`RegisterClassSpellAlias`) with **137 aliases** that correctly map class-specific IDs to canonical spells. Examples:

```
RegisterClassSpellAlias("bears_endurance_clr", SpellNames.BEARS_ENDURANCE, "Cleric", 2);
RegisterClassSpellAlias("bears_endurance_drd", SpellNames.BEARS_ENDURANCE, "Druid", 2);
RegisterClassSpellAlias("barkskin_drd",        SpellNames.BARKSKIN,        "Druid", 2);
```

This system already solves the problem — but the 18 entries above were registered as **full SpellData entries** instead of using this alias pattern.

---

## Recommendations

### Priority 1: Rename Domain-Only Spells (Low Risk, High Clarity)
Remove `domain_` prefix from 10 spells that have no base version. These are canonical spells that happen to be granted via domains:
- `domain_calm_animals` → `calm_animals`  
- `domain_desecrate` → `desecrate`  
- `domain_entangle` → `entangle`  
- etc.

**Impact:** 10 SpellNames constants + references in ~34 call sites + domain database entries.  
**Risk:** Medium — requires updating all reference sites.

### Priority 2: Consolidate True Duplicates Using Aliases (Medium Risk)
For `domain_barkskin` and `domain_wind_wall`, convert to aliases of the base spell:
```csharp
// Instead of Register(new SpellData { SpellId = "domain_barkskin", ... })
RegisterClassSpellAlias("domain_barkskin", SpellNames.BARKSKIN, "Cleric", 2, "Plant");
```

**Impact:** Remove 2 full SpellData entries, add 2 alias lines.  
**Risk:** Low — the alias system handles level differences per class.

### Priority 3: Consolidate Blindness/Deafness (Medium Risk)
Merge 3 entries into 1 canonical entry with `AvailableFor`:
```csharp
Register(new SpellData {
    SpellId = "blindness_deafness",
    AvailableFor = new List<SpellAvailability> {
        new SpellAvailability("Wizard", 2),
        new SpellAvailability("Sorcerer", 2),
        new SpellAvailability("Bard", 2),
        new SpellAvailability("Cleric", 3)
    }
});
```
Keep old IDs as aliases for backward compatibility.

**Impact:** -2 SpellData entries, needs BLINDNESS_DEAFNESS alias updates.  
**Risk:** Medium — `BLINDNESS_DEAFNESS` constant currently points to `_wiz` variant.

### Priority 4: Clean Up `_wiz` Suffixed Cantrips (Low Risk)
Rename `detect_magic_wiz` → `detect_magic`, `detect_poison_wiz` → `detect_poison`, `resistance_wiz` → `resistance`. Register old IDs as aliases.

**Impact:** 3 renames + aliases for backward compat.  
**Risk:** Low — these are cantrips with limited external references.

---

## Not Duplicates (Correct Separate Entries)

The following are **correctly** separate entries and should NOT be consolidated:
- **Summon Monster I/II/III/IV** — different spell levels, different summon tables
- **Summon Nature's Ally I/II/III/IV** — same reason
- **Cure/Inflict Light/Moderate/Serious/Critical Wounds** — different spell levels and dice
- **Test spells** (TEST_CONE_30, TEST_CONE_60, TEST_LINE_60) — development/testing tools

---

## Total Consolidation Potential

| Action | Entries Affected | Net SpellData Reduction |
|--------|-----------------|------------------------|
| Rename 10 domain-only spells | 10 | 0 (rename only) |
| Convert 2 domain duplicates to aliases | 2 | -2 |
| Merge Blindness/Deafness | 3 → 1 | -2 |
| Rename 3 `_wiz` cantrips | 3 | 0 (rename only) |
| **Total** | **18** | **-4 entries, 13 renames, 2 new aliases** |

> The codebase is largely well-organized. The 137 existing aliases show the team understood the pattern. These 18 entries appear to be early registrations made before the alias system was fully established.

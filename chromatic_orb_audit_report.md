# Chromatic Orb Spell Audit Report

## 1. Current Implementation Details

**File:** `Assets/Scripts/Magic/SpellDatabase_C.cs` (lines 142–158)

| Field | Value |
|---|---|
| **SpellId** | `chromatic_orb` |
| **Name** | "Chromatic Orb" |
| **Description** | "Ranged touch attack deals 1d8 damage (type varies by caster level). At CL3: fire, 1d8." |
| **SpellLevel** | 1 |
| **School** | Evocation |
| **ClassList** | Wizard |
| **TargetType** | SingleEnemy |
| **RangeSquares** | 5 (Close range — 25 ft.) |
| **IsTouch** | true |
| **IsRangedTouch** | true |
| **EffectType** | Damage |
| **DamageDice** | d8 |
| **DamageCount** | 1 |
| **DamageType** | "fire" |
| **ActionType** | Standard |
| **ProvokesAoO** | true |
| **AllowsSavingThrow** | *(not set — defaults to false)* |
| **SpellResistanceApplies** | *(not set — defaults to false)* |
| **Components** | *(not set — no V/S/M defined)* |

**Additional notes:** There is no special effect handling code anywhere in the codebase referencing `chromatic_orb` or `CHROMATIC_ORB` beyond the spell registration and the `SpellNames` identifier constant. No caster-level-based damage type switching logic exists.

---

## 2. Official D&D 3.5e Rules for "Chromatic Orb"

### ⚠️ Critical Finding: Chromatic Orb Does NOT Exist in D&D 3.5e

**Chromatic Orb is NOT a D&D 3.5 Edition spell.** It does not appear in the D&D 3.5e Player's Handbook, SRD, Spell Compendium, or any official 3.5e first-party supplement.

The spell's publication history:

| Edition | Source | Notes |
|---|---|---|
| **AD&D 1e** | *Unearthed Arcana* (1985) | 1st-level Illusionist spell with level-scaled color effects (light → death) |
| **AD&D 2e** | *The Complete Wizard's Handbook* (1990) | 1st-level Wizard spell, Alteration/Evocation school |
| **D&D 3.0/3.5e** | **Does not exist** | Not in PHB, SRD, or any official supplement |
| **D&D 5e** | *Player's Handbook* (2014) | 1st-level Evocation, 3d8 damage, choose type, 90 ft range |

### Closest 3.5e Equivalent: Lesser Orb Spells (Spell Compendium)

The *Spell Compendium* (3.5e) includes a family of **Lesser Orb** spells (not "Chromatic Orb"):

| Spell | Level | School | Damage | Range | Save | SR |
|---|---|---|---|---|---|---|
| Lesser Orb of Acid | 1 | Conjuration (Creation) | 1d8/2 CL (max 5d8) | Close | None | No |
| Lesser Orb of Cold | 1 | Conjuration (Creation) | 1d8/2 CL (max 5d8) | Close | None | No |
| Lesser Orb of Electricity | 1 | Conjuration (Creation) | 1d8/2 CL (max 5d8) | Close | None | No |
| Lesser Orb of Fire | 1 | Conjuration (Creation) | 1d8/2 CL (max 5d8) | Close | None | No |
| Lesser Orb of Sound | 1 | Conjuration (Creation) | 1d6/2 CL (max 5d6) | Close | None | No |

**Key traits of the Lesser Orb spells:**
- **School:** Conjuration (Creation) — **not** Evocation
- **Components:** V, S
- **Range:** Close (25 ft. + 5 ft./2 levels)
- **Attack:** Ranged touch attack
- **Damage:** Starts at 1d8 at CL 1, gains +1d8 per 2 caster levels (CL 3 = 2d8, CL 5 = 3d8, etc.), max 5d8 at CL 9
- **Saving Throw:** None
- **Spell Resistance:** No (major feature — bypasses SR)
- **Duration:** Instantaneous
- **No material component** (just V, S)

### AD&D 2e Chromatic Orb (for reference)

If the implementation is intended to reflect the AD&D version:

- **Level:** 1st (Illusionist/Wizard)
- **School:** Alteration/Evocation
- **Components:** V, S, M (gem worth 50+ gp, consumed)
- **Range:** 30 yards
- **Attack:** Normal attack roll with distance-based bonuses
- **Damage & Effects scale by caster level:**
  - CL 1: White (1d4, light effect)
  - CL 2: Red (1d6, heat/debuff)
  - CL 3: Orange (1d8, fire/ignition)
  - CL 4: Yellow (1d10, blindness)
  - CL 5: Green (1d12, stinking cloud)
  - CL 6: Turquoise (2d8, magnetism)
  - CL 7: Blue (2d16, paralysis)
  - CL 10: Violet (petrification)
  - CL 12: Black (death)
- **Saving Throw:** Negates special effect (not damage)
- **SR:** N/A (AD&D didn't use SR system)

---

## 3. Discrepancies

Since Chromatic Orb doesn't exist in D&D 3.5e, **every aspect of this spell is a discrepancy with the 3.5e PHB** — the spell simply isn't in the book. However, comparing against the most likely intended sources:

### If intended as a 3.5e Spell Compendium "Lesser Orb" spell:

| Aspect | Current Implementation | Lesser Orb (Spell Compendium) | Status |
|---|---|---|---|
| **Name** | "Chromatic Orb" | "Lesser Orb of [element]" | ❌ Wrong name |
| **School** | Evocation | **Conjuration (Creation)** | ❌ Wrong school |
| **Damage scaling** | Fixed 1d8 (no scaling) | 1d8 per 2 CL (max 5d8) | ❌ Missing scaling |
| **Damage type** | Hardcoded "fire" | Varies per spell variant | ⚠️ Partially wrong — should be one specific type per spell, or let player choose |
| **Saving Throw** | None (default false) | None | ✅ Correct |
| **Spell Resistance** | No (default false) | No | ✅ Correct (but for wrong reason — Conjuration bypasses SR, not Evocation) |
| **Range** | Close (5 squares) | Close (25 ft. + 5 ft./2 CL) | ⚠️ Base correct, but no scaling defined |
| **Components** | Not specified | V, S (no material) | ⚠️ Missing |
| **Attack type** | Ranged touch | Ranged touch | ✅ Correct |
| **Class list** | Wizard only | Sorcerer/Wizard | ❌ Missing Sorcerer |

### If intended as an AD&D 2e Chromatic Orb adaptation:

| Aspect | Current Implementation | AD&D 2e Chromatic Orb | Status |
|---|---|---|---|
| **Damage at CL 3** | 1d8 fire | 1d8 fire (orange orb) | ✅ Matches for CL 3 specifically |
| **CL-based scaling** | None implemented | Full color/damage/effect table | ❌ Missing all scaling |
| **Special effects** | None | Light, heat, fire, blindness, paralysis, petrification, death | ❌ Missing entirely |
| **Material component** | Not specified | Gem worth 50+ gp (consumed) | ❌ Missing |
| **Saving throw** | None | Save negates special effect | ❌ Missing |
| **School** | Evocation | Alteration/Evocation | ⚠️ Partially correct |

---

## 4. Recommendations

### Option A: Remove the spell (Strict 3.5e PHB adherence)
Since Chromatic Orb doesn't exist in 3.5e, the cleanest approach for PHB-only accuracy is to **remove it** and replace it with the official **Lesser Orb** spells from the Spell Compendium if desired.

### Option B: Replace with Lesser Orb spells (3.5e Spell Compendium)
Replace the single "Chromatic Orb" with the official Lesser Orb spell family:
1. Change school from Evocation → **Conjuration (Creation)**
2. Add **damage scaling**: 1d8 per 2 caster levels, max 5d8 at CL 9
3. Create separate spell entries per element (acid, cold, electricity, fire, sound) OR implement element choice at cast time
4. Add **Sorcerer** to the class list
5. Set components to V, S
6. Confirm range scales properly (Close: 25 ft. + 5 ft./2 CL)
7. Keep: No saving throw, No SR, Ranged touch attack

### Option C: Keep as custom/homebrew spell (clearly mark it)
If the intent is to keep a simplified "chromatic orb" as a custom spell:
1. **Mark it as non-PHB/custom** in spell data (the range audit already flags it as non-PHB)
2. Add damage scaling (currently flat 1d8 is very weak for higher levels)
3. Consider adding element choice at cast time
4. Document that this is a homebrew adaptation

### Priority Fix (regardless of option chosen):
- **Damage scaling is missing** — flat 1d8 with no CL scaling makes this spell nearly useless past level 1
- **Sorcerer should be on the class list** if keeping the spell
- **School should be Conjuration** if aligning with Spell Compendium orb spells

---

*Report generated: May 8, 2026*
*Codebase: /home/ubuntu/dnd35prototype*

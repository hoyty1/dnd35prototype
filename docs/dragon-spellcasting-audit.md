# Dragon Spellcasting Comprehensive Audit

## D&D 3.5e Monster Manual vs. Current Implementation (`DragonData.cs`)

**Date:** May 28, 2026
**Scope:** All 10 true dragon types across the 6 implemented age categories (Wyrmling → Adult)
**Source of Truth:** D&D 3.5e Monster Manual / d20 SRD

---

## Executive Summary

The current implementation has **critical gaps** in dragon spellcasting:

| Issue | Count | Details |
|-------|-------|---------|
| Dragons with **correct** caster levels | **2**/10 | Red, Silver |
| Dragons with **wrong** caster levels | **3**/10 | Blue, Gold, Bronze |
| Dragons **completely missing** spellcasting | **5**/10 | Black, Green, White, Copper, Brass |
| Spell-like abilities implemented | **0**/10 | None — all `SpellLikeAbilityIds` arrays empty |
| Spells known (all dragons) | Only `mage_armor`, `shield` | 128 sorcerer spells available in SpellDatabase |
| `IsSpellcaster` returns true | **0**/10 | All dragons use Warrior class; SpellcastingComponent never initializes |

### Systemic Blockers

1. **`IsSpellcaster` always returns `false`** — Dragons use the Warrior character class, so the spellcasting system never activates.
2. **`SpellcastingComponent` never initialized** — Gated behind `IsSpellcaster`.
3. **`SpellLikeAbilityIds` empty for all dragons** — No SLAs defined anywhere.
4. **Only 4/10 dragons have any spell IDs**, and those only list `mage_armor` + `shield` (placeholder values).

---

## Dragon-by-Dragon Audit

### Age Category Reference

| Index | Age Category | Abbreviation |
|-------|-------------|-------------|
| 0 | Wyrmling | W |
| 1 | Very Young | VY |
| 2 | Young | Y |
| 3 | Juvenile | J |
| 4 | Young Adult | YA |
| 5 | Adult | A |
| 6+ | Mature Adult through Great Wyrm | *Not yet implemented* |

---

### 🔴 Red Dragon (Chromatic)

**Spellcasting starts:** Young (age 3) — casts as Sorcerer

#### Caster Levels

| Age | MM/SRD CL | Impl CL | Status |
|-----|----------|---------|--------|
| Wyrmling | — | 0 | ✅ Correct |
| Very Young | — | 0 | ✅ Correct |
| Young | 1 | 1 | ✅ Correct |
| Juvenile | 3 | 3 | ✅ Correct |
| Young Adult | 5 | 5 | ✅ Correct |
| Adult | 7 | 7 | ✅ Correct |

#### Spells Known (Implementation)

Currently: `mage_armor`, `shield` — **placeholder only**

**Should have:** Full sorcerer spell progression at each CL. At CL 1: 4 cantrips known, 2 1st-level spells known. At CL 7 (Adult): up to 4th-level spells.

#### Spell-Like Abilities (MM/SRD)

| Gained At | Ability | Frequency | In Impl? |
|-----------|---------|-----------|----------|
| Juvenile (age 3) | Locate Object | 1/day per age cat above Juvenile | ❌ Missing |
| Old (age 7) | Suggestion | 3/day | *(outside impl range)* |
| Ancient (age 9) | Find the Path | 1/day | *(outside impl range)* |
| Great Wyrm (age 12) | Discern Location | 1/day | *(outside impl range)* |

**Verdict:** ✅ Caster levels correct. ❌ Spells known incomplete. ❌ Locate Object SLA missing for Juvenile+.

---

### 🔵 Blue Dragon (Chromatic)

**Spellcasting starts:** Juvenile (age 4) — **NOT** Young as currently implemented

#### Caster Levels

| Age | MM/SRD CL | Impl CL | Status |
|-----|----------|---------|--------|
| Wyrmling | — | 0 | ✅ Correct |
| Very Young | — | 0 | ✅ Correct |
| Young | — | **1** | ❌ **WRONG** — should be 0 |
| Juvenile | 1 | **3** | ❌ **WRONG** — should be 1 |
| Young Adult | 3 | **5** | ❌ **WRONG** — should be 3 |
| Adult | 5 | **7** | ❌ **WRONG** — should be 5 |

**Root cause:** Implementation copied Red Dragon's progression (starts Young, CL 1/3/5/7) instead of Blue's own (starts Juvenile, CL 1/3/5).

#### Spells Known (Implementation)

Currently: `mage_armor`, `shield` — **placeholder only**

#### Spell-Like Abilities (MM/SRD)

| Gained At | Ability | Frequency | In Impl? |
|-----------|---------|-----------|----------|
| Any age | Create/Destroy Water | 3/day | ❌ Missing |
| Very Young+ | Sound Imitation (Ex) | At will | ❌ Missing |
| Adult (age 5) | Ventriloquism | 3/day | ❌ Missing |
| Old (age 7) | Hallucinatory Terrain | 1/day | *(outside impl range)* |
| Ancient (age 9) | Veil | 2/day | *(outside impl range)* |
| Great Wyrm (age 12) | Mirage Arcana | 1/day | *(outside impl range)* |

**Verdict:** ❌ All caster levels shifted one age too early and +2 too high. ❌ Create/Destroy Water SLA missing at ALL ages. ❌ Sound Imitation missing.

---

### 🟢 Green Dragon (Chromatic)

**Spellcasting starts:** Juvenile (age 4) — **completely missing from implementation**

#### Caster Levels

| Age | MM/SRD CL | Impl CL | Status |
|-----|----------|---------|--------|
| Wyrmling | — | 0 | ✅ Correct (trivially) |
| Very Young | — | 0 | ✅ Correct (trivially) |
| Young | — | 0 | ✅ Correct (trivially) |
| Juvenile | 1 | **0** | ❌ **MISSING** |
| Young Adult | 3 | **0** | ❌ **MISSING** |
| Adult | 5 | **0** | ❌ **MISSING** |

#### Spells Known (Implementation)

Currently: *(empty)* — **completely missing**

#### Spell-Like Abilities (MM/SRD)

| Gained At | Ability | Frequency | In Impl? |
|-----------|---------|-----------|----------|
| Any age | Water Breathing (Ex) | Constant | ❌ Missing |
| Adult (age 5) | Suggestion | 3/day | ❌ Missing |
| Old (age 7) | Plant Growth | 1/day | *(outside impl range)* |
| Ancient (age 9) | Dominate Person | 3/day | *(outside impl range)* |
| Great Wyrm (age 12) | Command Plants | 1/day | *(outside impl range)* |

**Verdict:** ❌ Spellcasting entirely missing — needs CL 1/3/5 at Juvenile/YA/Adult. ❌ Water Breathing and Suggestion SLAs missing.

---

### ⬛ Black Dragon (Chromatic)

**Spellcasting starts:** Young Adult (age 5) — **completely missing from implementation**

#### Caster Levels

| Age | MM/SRD CL | Impl CL | Status |
|-----|----------|---------|--------|
| Wyrmling | — | 0 | ✅ Correct (trivially) |
| Very Young | — | 0 | ✅ Correct (trivially) |
| Young | — | 0 | ✅ Correct (trivially) |
| Juvenile | — | 0 | ✅ Correct (trivially) |
| Young Adult | 1 | **0** | ❌ **MISSING** |
| Adult | 3 | **0** | ❌ **MISSING** |

#### Spells Known (Implementation)

Currently: *(empty)* — **completely missing**

#### Spell-Like Abilities (MM/SRD)

| Gained At | Ability | Frequency | In Impl? |
|-----------|---------|-----------|----------|
| Any age | Water Breathing (Ex) | Constant | ❌ Missing |
| Juvenile (age 3) | Darkness | 3/day (10ft radius per age cat) | ❌ Missing |
| Adult (age 5) | Corrupt Water | 1/day | ❌ Missing |
| Old (age 7) | Plant Growth | 1/day | *(outside impl range)* |
| Ancient (age 9) | Insect Plague | 3/day | *(outside impl range)* |
| Great Wyrm (age 12) | Charm Reptiles | 3/day | *(outside impl range)* |

**Verdict:** ❌ Spellcasting entirely missing — needs CL 1/3 at YA/Adult. ❌ Darkness SLA missing at Juvenile+. ❌ Corrupt Water missing at Adult.

---

### ⬜ White Dragon (Chromatic)

**Spellcasting starts:** Adult (age 6) — the latest of all dragons. **Completely missing from implementation.**

#### Caster Levels

| Age | MM/SRD CL | Impl CL | Status |
|-----|----------|---------|--------|
| Wyrmling | — | 0 | ✅ Correct (trivially) |
| Very Young | — | 0 | ✅ Correct (trivially) |
| Young | — | 0 | ✅ Correct (trivially) |
| Juvenile | — | 0 | ✅ Correct (trivially) |
| Young Adult | — | 0 | ✅ Correct (trivially) |
| Adult | 1 | **0** | ❌ **MISSING** |

#### Spells Known (Implementation)

Currently: *(empty)* — **completely missing**

#### Spell-Like Abilities (MM/SRD)

| Gained At | Ability | Frequency | In Impl? |
|-----------|---------|-----------|----------|
| Any age | Icewalking (Ex) | Constant | ❌ Missing |
| Juvenile (age 3) | Fog Cloud | 3/day | ❌ Missing |
| Adult (age 5) | Gust of Wind | 3/day | ❌ Missing |
| Old (age 7) | Freezing Fog (Su) | Special | *(outside impl range)* |
| Ancient (age 9) | Wall of Ice | 3/day | *(outside impl range)* |
| Great Wyrm (age 12) | Control Weather | 1/day | *(outside impl range)* |

**Verdict:** ❌ Only CL 1 at Adult needed in current range — but it's missing. ❌ Fog Cloud and Gust of Wind SLAs missing. ❌ Icewalking missing.

---

### 🟡 Gold Dragon (Metallic)

**Spellcasting starts:** Young (age 3) — **implementation has it starting at Very Young (WRONG)**

#### Caster Levels

| Age | MM/SRD CL | Impl CL | Status |
|-----|----------|---------|--------|
| Wyrmling | — | 0 | ✅ Correct |
| Very Young | — | **1** | ❌ **WRONG** — should be 0 |
| Young | 1 | **3** | ❌ **WRONG** — should be 1 |
| Juvenile | 3 | **5** | ❌ **WRONG** — should be 3 |
| Young Adult | 5 | **7** | ❌ **WRONG** — should be 5 |
| Adult | 7 | **9** | ❌ **WRONG** — should be 7 |

**Root cause:** Implementation starts spellcasting one age category too early (Very Young instead of Young) and all subsequent CLs are +2 too high.

#### Spells Known (Implementation)

Currently: `mage_armor`, `shield` — **placeholder only**

#### Spell-Like Abilities (MM/SRD)

| Gained At | Ability | Frequency | In Impl? |
|-----------|---------|-----------|----------|
| Any age | Alternate Form (Su) | At will | ❌ Missing |
| Any age | Water Breathing (Ex) | Constant | ❌ Missing |
| Juvenile (age 3) | Bless | 3/day | ❌ Missing |
| Adult (age 5) | Luck Bonus (Sp) | 1/day (touch gem, +1 luck to saves) | ❌ Missing |
| Adult (age 5) | Fire Aura (Su) | Special | ❌ Missing |
| Adult (age 5) | Weakening Breath (Su) | Special (alt breath) | ❌ Missing |
| Old (age 7) | Geas/Quest | 1/day | *(outside impl range)* |
| Old (age 7) | Detect Gems | 3/day | *(outside impl range)* |
| Ancient (age 9) | Sunburst | 1/day | *(outside impl range)* |
| Great Wyrm (age 12) | Foresight | 1/day | *(outside impl range)* |

**Verdict:** ❌ All caster levels off by one age / +2 CL. ❌ Bless SLA missing. ❌ Alternate Form, Water Breathing, Luck Bonus, Fire Aura, Weakening Breath all missing.

---

### ⚪ Silver Dragon (Metallic)

**Spellcasting starts:** Young (age 3)

#### Caster Levels

| Age | MM/SRD CL | Impl CL | Status |
|-----|----------|---------|--------|
| Wyrmling | — | 0 | ✅ Correct |
| Very Young | — | 0 | ✅ Correct |
| Young | 1 | 1 | ✅ Correct |
| Juvenile | 3 | 3 | ✅ Correct |
| Young Adult | 5 | 5 | ✅ Correct |
| Adult | 7 | 7 | ✅ Correct |

#### Spells Known (Implementation)

Currently: `mage_armor`, `shield` — **placeholder only**

#### Spell-Like Abilities (MM/SRD)

| Gained At | Ability | Frequency | In Impl? |
|-----------|---------|-----------|----------|
| Any age | Alternate Form (Su) | At will | ❌ Missing |
| Any age | Cloudwalking (Su) | Constant | ❌ Missing |
| Juvenile (age 3) | Feather Fall | 2/day | ❌ Missing |
| Adult (age 5) | Fog Cloud | 3/day | ❌ Missing |
| Adult (age 5) | Paralyzing Breath (Su) | Special (alt breath) | ❌ Missing |
| Old (age 7) | Control Winds | 3/day | *(outside impl range)* |
| Ancient (age 9) | Control Weather | 1/day | *(outside impl range)* |
| Great Wyrm (age 12) | Reverse Gravity | 1/day | *(outside impl range)* |

**Verdict:** ✅ Caster levels correct! ❌ Spells known incomplete. ❌ All SLAs missing (Feather Fall, Fog Cloud, Alternate Form, Cloudwalking, Paralyzing Breath).

---

### 🟤 Bronze Dragon (Metallic)

**Spellcasting starts:** Young (age 3) — **implementation has it starting at Juvenile (WRONG)**

#### Caster Levels

| Age | MM/SRD CL | Impl CL | Status |
|-----|----------|---------|--------|
| Wyrmling | — | 0 | ✅ Correct |
| Very Young | — | 0 | ✅ Correct |
| Young | 1 | **0** | ❌ **MISSING** — should be 1 |
| Juvenile | 3 | **1** | ❌ **WRONG** — should be 3 |
| Young Adult | 5 | **3** | ❌ **WRONG** — should be 5 |
| Adult | 7 | **5** | ❌ **WRONG** — should be 7 |

**Root cause:** Implementation starts spellcasting one age category too late (Juvenile instead of Young) and all CLs are shifted down by 2.

#### Spells Known (Implementation)

Currently: *(empty)* — **completely missing**

#### Spell-Like Abilities (MM/SRD)

| Gained At | Ability | Frequency | In Impl? |
|-----------|---------|-----------|----------|
| Any age | Speak with Animals (Sp) | At will | ❌ Missing |
| Any age | Water Breathing (Ex) | Constant | ❌ Missing |
| Any age | Alternate Form (Su) | At will | ❌ Missing |
| Adult (age 5) | Create Food and Water | 3/day | ❌ Missing |
| Adult (age 5) | Fog Cloud | 3/day | ❌ Missing |
| Adult (age 5) | Repulsion Breath (Su) | Special (alt breath) | ❌ Missing |
| Old (age 7) | Detect Thoughts | 3/day | *(outside impl range)* |
| Ancient (age 9) | Control Water | 3/day | *(outside impl range)* |
| Great Wyrm (age 12) | Control Weather | 1/day | *(outside impl range)* |

**Verdict:** ❌ All caster levels shifted one age too late / -2 CL. ❌ Speak with Animals, Water Breathing, Alternate Form SLAs missing at all ages. ❌ Fog Cloud, Create Food and Water missing at Adult.

---

### 🟠 Copper Dragon (Metallic)

**Spellcasting starts:** Young (age 3) — **completely missing from implementation**

#### Caster Levels

| Age | MM/SRD CL | Impl CL | Status |
|-----|----------|---------|--------|
| Wyrmling | — | 0 | ✅ Correct (trivially) |
| Very Young | — | 0 | ✅ Correct (trivially) |
| Young | 1 | **0** | ❌ **MISSING** |
| Juvenile | 3 | **0** | ❌ **MISSING** |
| Young Adult | 5 | **0** | ❌ **MISSING** |
| Adult | 7 | **0** | ❌ **MISSING** |

#### Spells Known (Implementation)

Currently: *(empty)* — **completely missing**

#### Spell-Like Abilities (MM/SRD)

| Gained At | Ability | Frequency | In Impl? |
|-----------|---------|-----------|----------|
| Any age | Spider Climb (Ex) | Constant (stone only) | ❌ Missing |
| Adult (age 5) | Stone Shape | 2/day | ❌ Missing |
| Old (age 7) | Transmute Rock to Mud / Mud to Rock | 1/day | *(outside impl range)* |
| Ancient (age 9) | Wall of Stone | 1/day | *(outside impl range)* |
| Great Wyrm (age 12) | Move Earth | 1/day | *(outside impl range)* |

**Verdict:** ❌ Spellcasting entirely missing — needs CL 1/3/5/7 at Young through Adult. ❌ Spider Climb and Stone Shape SLAs missing.

---

### 🟡 Brass Dragon (Metallic)

**Spellcasting starts:** Young (age 3) — **completely missing from implementation**

#### Caster Levels

| Age | MM/SRD CL | Impl CL | Status |
|-----|----------|---------|--------|
| Wyrmling | — | 0 | ✅ Correct (trivially) |
| Very Young | — | 0 | ✅ Correct (trivially) |
| Young | 1 | **0** | ❌ **MISSING** |
| Juvenile | 3 | **0** | ❌ **MISSING** |
| Young Adult | 5 | **0** | ❌ **MISSING** |
| Adult | 7 | **0** | ❌ **MISSING** |

#### Spells Known (Implementation)

Currently: *(empty)* — **completely missing**

#### Spell-Like Abilities (MM/SRD)

| Gained At | Ability | Frequency | In Impl? |
|-----------|---------|-----------|----------|
| Any age | Speak with Animals (Sp) | At will | ❌ Missing |
| Juvenile (age 3) | Endure Elements | 3/day (10ft radius per age cat) | ❌ Missing |
| Adult (age 5) | Suggestion | 1/day | ❌ Missing |
| Old (age 7) | Control Winds | 1/day | *(outside impl range)* |
| Ancient (age 9) | Control Weather | 1/day | *(outside impl range)* |

Also has **Sleep Breath (Su)** — alternate breath weapon (cone of sleep gas) at all ages.

**Verdict:** ❌ Spellcasting entirely missing — needs CL 1/3/5/7 at Young through Adult. ❌ Speak with Animals, Endure Elements, Suggestion SLAs missing. ❌ Sleep Breath missing.

---

## Correction Plan Summary

### Priority 1: Fix Caster Levels (8 dragons need changes)

| Dragon | Current CL (W/VY/Y/J/YA/A) | Correct CL | Change Needed |
|--------|-----------------------------|-------------|---------------|
| **Red** | 0/0/1/3/5/7 | 0/0/1/3/5/7 | ✅ None |
| **Silver** | 0/0/1/3/5/7 | 0/0/1/3/5/7 | ✅ None |
| **Blue** | 0/0/1/3/5/7 | 0/0/0/1/3/5 | Fix: Y→0, J→1, YA→3, A→5 |
| **Gold** | 0/1/3/5/7/9 | 0/0/1/3/5/7 | Fix: VY→0, Y→1, J→3, YA→5, A→7 |
| **Bronze** | 0/0/0/1/3/5 | 0/0/1/3/5/7 | Fix: Y→1, J→3, YA→5, A→7 |
| **Green** | 0/0/0/0/0/0 | 0/0/0/1/3/5 | Add: J→1, YA→3, A→5 |
| **Black** | 0/0/0/0/0/0 | 0/0/0/0/1/3 | Add: YA→1, A→3 |
| **White** | 0/0/0/0/0/0 | 0/0/0/0/0/1 | Add: A→1 |
| **Copper** | 0/0/0/0/0/0 | 0/0/1/3/5/7 | Add: Y→1, J→3, YA→5, A→7 |
| **Brass** | 0/0/0/0/0/0 | 0/0/1/3/5/7 | Add: Y→1, J→3, YA→5, A→7 |

### Priority 2: Fix Spellcasting System

1. **Enable sorcerer spellcasting for dragons** — Dragons should not need to be `IsSpellcaster` via character class; dragon innate sorcerer casting is separate from class-based casting.
2. **Implement proper spells known** — Based on sorcerer spells-known table at each CL, not just `mage_armor`/`shield`.
3. **Initialize SpellcastingComponent** for dragon creatures with CL > 0.

### Priority 3: Implement Spell-Like Abilities

SLAs within the implemented age range (Wyrmling through Adult) that need implementation:

| Dragon | SLA | Age Required | Frequency |
|--------|-----|-------------|-----------|
| **Red** | Locate Object | Juvenile+ | 1/day per age cat |
| **Blue** | Create/Destroy Water | Any | 3/day |
| **Blue** | Sound Imitation (Ex) | Very Young+ | At will |
| **Blue** | Ventriloquism | Adult+ | 3/day |
| **Green** | Water Breathing (Ex) | Any | Constant |
| **Green** | Suggestion | Adult+ | 3/day |
| **Black** | Water Breathing (Ex) | Any | Constant |
| **Black** | Darkness | Juvenile+ | 3/day |
| **Black** | Corrupt Water | Adult+ | 1/day |
| **White** | Icewalking (Ex) | Any | Constant |
| **White** | Fog Cloud | Juvenile+ | 3/day |
| **White** | Gust of Wind | Adult+ | 3/day |
| **Gold** | Alternate Form (Su) | Any | At will |
| **Gold** | Water Breathing (Ex) | Any | Constant |
| **Gold** | Bless | Juvenile+ | 3/day |
| **Gold** | Luck Bonus (Sp) | Adult+ | 1/day |
| **Gold** | Fire Aura (Su) | Adult+ | Special |
| **Gold** | Weakening Breath (Su) | Adult+ | Alt breath |
| **Silver** | Alternate Form (Su) | Any | At will |
| **Silver** | Cloudwalking (Su) | Any | Constant |
| **Silver** | Feather Fall | Juvenile+ | 2/day |
| **Silver** | Fog Cloud | Adult+ | 3/day |
| **Silver** | Paralyzing Breath (Su) | Adult+ | Alt breath |
| **Bronze** | Speak with Animals (Sp) | Any | At will |
| **Bronze** | Water Breathing (Ex) | Any | Constant |
| **Bronze** | Alternate Form (Su) | Any | At will |
| **Bronze** | Create Food and Water | Adult+ | 3/day |
| **Bronze** | Fog Cloud | Adult+ | 3/day |
| **Bronze** | Repulsion Breath (Su) | Adult+ | Alt breath |
| **Copper** | Spider Climb (Ex) | Any | Constant |
| **Copper** | Stone Shape | Adult+ | 2/day |
| **Brass** | Speak with Animals (Sp) | Any | At will |
| **Brass** | Endure Elements | Juvenile+ | 3/day |
| **Brass** | Suggestion | Adult+ | 1/day |
| **Brass** | Sleep Breath (Su) | Any | Alt breath |

### Priority 4: Future Age Categories

When Mature Adult through Great Wyrm are implemented, additional SLAs and higher CLs will be needed. Full data is documented per-dragon above.

---

## Appendix: Sorcerer Spells Known by Caster Level

For reference — the number of spells a sorcerer knows at each CL (relevant for populating `SorcererSpellIds`):

| CL | 0th | 1st | 2nd | 3rd | 4th |
|----|-----|-----|-----|-----|-----|
| 1 | 4 | 2 | — | — | — |
| 3 | 5 | 3 | 1 | — | — |
| 5 | 6 | 4 | 2 | 1 | — |
| 7 | 6 | 4 | 3 | 2 | 1 |
| 9 | 6 | 4 | 4 | 3 | 2 |

128 sorcerer-accessible spells (levels 0-9) already exist in `SpellDatabase`. Appropriate spells should be selected per dragon type to match their thematic identity (e.g., fire spells for Red, illusion for Blue, etc.).

---

## Appendix: Caster Level Progression Pattern

All true dragons follow a **+2 CL per age category** progression once spellcasting begins:

| Category | Chromatic | Metallic |
|----------|-----------|----------|
| Earliest start | Red: Young (CL 1) | Gold/Silver/Bronze/Copper/Brass: Young (CL 1) |
| Middle start | Blue/Green: Juvenile (CL 1) | — |
| Latest start | Black: Young Adult (CL 1) | — |
| Very latest | White: Adult (CL 1) | — |

Metallic dragons are generally more magically capable, gaining spellcasting earlier and having more SLAs. White dragons are the weakest spellcasters (only CL 1 within implemented range), while Red/Gold/Silver/Copper/Brass reach CL 7 by Adult.

# Summon Nature's Ally I–IV — Creature Audit Report

**Date:** 2026-05-26  
**Scope:** Audit the current SNA I–IV creature lists (commit `7d7a968`) against the canonical D&D 3.5 PHB tables (p.288-289) and cross-reference with the project's NPCDatabase.  
**Action required:** This is an audit-only report. No code changes have been made.

---

## Executive Summary

| Metric | Count |
|--------|-------|
| Total canonical SNA I–IV creatures | **45** |
| Present in NPCDatabase (direct ID) | **29** |
| Resolvable via summon alias | **1** (owl → eagle) |
| **Missing from NPCDatabase entirely** | **15** |
| Wrong entries in current SNA lists | **18** (creatures at wrong level or not on SNA list at all) |
| Current lists using wrong NpcDefinitionId | **1** (Brown Bear uses `dire_bear` instead of `brown_bear`) |

### Critical Finding

**The current SNA lists are based on the Summon Monster (SM) creature tables, not the Summon Nature's Ally (SNA) tables.** The SNA and SM lists are completely different in D&D 3.5e — SM summons celestial/fiendish-templated animals and outsiders, while SNA summons regular animals, fey, and elementals (no templates). Every single SNA level has incorrect creatures assigned.

---

## Legend

| Icon | Meaning |
|------|---------|
| ✅ | Creature exists in NPCDatabase (direct `Id` match) |
| 🔄 | Creature resolves via `RegisterSummonAlias()` |
| ❌ | Creature is **missing** from NPCDatabase |
| 📋 | Currently listed in the SNA code (may be at wrong level) |
| ⚠️ | Aquatic creature — lower priority for land-based prototype |

---

## SNA I — 1st-Level (PHB p.288)

### Canonical Creatures (8)

| Status | Creature | NpcDefinitionId | In Current List? | Notes |
|--------|----------|-----------------|------------------|-------|
| ✅ | Dire Rat | `dire_rat` | ✅ Yes | Correct |
| ✅ | Eagle | `eagle` | ❌ No | Missing from SNA I list |
| ✅ | Monkey | `monkey` | ✅ Yes | Correct |
| ✅ ⚠️ | Octopus | `octopus` | ❌ No | Aquatic — low priority |
| ✅ | Owl | `owl` | ✅ Yes | Resolves via alias `owl` → `eagle` |
| ❌ ⚠️ | Porpoise | `porpoise` | ❌ No | Aquatic — **not in DB**, lowest priority |
| ✅ | Dog, Riding | `riding_dog` | ❌ No | In DB but not in SNA I list |
| ✅ | Snake, Small Viper | `viper_small` | ✅ Yes | Correct |

### Wrong Entries Currently in SNA I (should be removed)

| Creature | NpcDefinitionId | Problem |
|----------|-----------------|---------|
| Dog | `dog` | Not on SNA I list. PHB lists "Dog, Riding" not "Dog" |
| Hawk | `hawk` | Not on any SNA list. Hawk is on SM I (fiendish) |
| Badger | `badger` | Not on any SNA list. Badger is on SM I (celestial). Note: `badger` alias → `dire_badger` |

### SNA I Summary
- **Canonical:** 8 creatures (6 land, 2 aquatic)
- **In DB:** 7/8 (missing only Porpoise)
- **Currently listed:** 4/8 correct, 3 wrong entries, 4 canonical creatures missing

---

## SNA II — 2nd-Level (PHB p.288)

### Canonical Creatures (12)

| Status | Creature | NpcDefinitionId | In Current List? | Notes |
|--------|----------|-----------------|------------------|-------|
| ✅ | Bear, Black | `black_bear` | ❌ No | In DB, not in SNA II list |
| ✅ | Crocodile | `crocodile` | ❌ No | In DB, not in SNA II list |
| ✅ | Dire Badger | `dire_badger` | ❌ No | In DB, not in SNA II list |
| ✅ | Dire Bat | `dire_bat` | ❌ No | In DB, not in SNA II list |
| ❌ ⚠️ | Shark, Medium | `medium_shark` | ❌ No | Aquatic — **not in DB** |
| ✅ | Snake, Medium Viper | `viper_medium` | ✅ Yes | Correct |
| ❌ ⚠️ | Squid | `squid` | ❌ No | Aquatic — **not in DB** |
| ✅ | Wolverine | `wolverine` | ❌ No | In DB, not in SNA II list |
| ✅ | Elemental, Small (Air) | `small_air_elemental` | ❌ No | In DB, not in SNA II list |
| ✅ | Elemental, Small (Earth) | `small_earth_elemental` | ❌ No | In DB, not in SNA II list |
| ✅ | Elemental, Small (Fire) | `small_fire_elemental` | ❌ No | In DB, not in SNA II list |
| ✅ | Elemental, Small (Water) | `small_water_elemental` | ❌ No | In DB, not in SNA II list |

### Wrong Entries Currently in SNA II (should be removed)

| Creature | NpcDefinitionId | Problem |
|----------|-----------------|---------|
| Wolf | `wolf` | Not on SNA II. Wolf is on SM II (fiendish). `wolf` alias → `wolf_pack_hunter` |
| Eagle | `eagle` | Not on SNA II. Eagle is on SNA **I** |
| Boar | `boar` | Not on SNA II. Boar is on SM III (fiendish) |
| Giant Bee | `giant_bee` | Not on any SNA list. Giant Bee is on SM II (celestial). `giant_bee` alias → `dire_bat` |
| Riding Dog | `riding_dog` | Not on SNA II. Riding Dog is on SNA **I** |

### SNA II Summary
- **Canonical:** 12 creatures (10 land, 2 aquatic)
- **In DB:** 10/12 (missing Medium Shark, Squid)
- **Currently listed:** 1/12 correct, 5 wrong entries, 11 canonical creatures missing

---

## SNA III — 3rd-Level (PHB p.288-289)

### Canonical Creatures (13)

| Status | Creature | NpcDefinitionId | In Current List? | Notes |
|--------|----------|-----------------|------------------|-------|
| ✅ | Ape | `ape` | ❌ No | In DB, not in SNA III list |
| ✅ | Dire Weasel | `dire_weasel` | ❌ No | In DB, not in SNA III list |
| ✅ | Dire Wolf | `dire_wolf` | ❌ No | In DB, not in SNA III list |
| ✅ | Eagle, Giant | `giant_eagle` | ❌ No | In DB, not in SNA III list |
| ✅ | Lion | `lion` | ❌ No | In DB, not in SNA III list |
| ✅ | Owl, Giant | `giant_owl` | ❌ No | In DB, not in SNA III list |
| ❌ | Satyr (without pipes) | `satyr` | ❌ No | Fey — **not in DB** |
| ✅ | Snake, Constrictor | `constrictor_snake` | ✅ Yes | Correct |
| ✅ | Snake, Large Viper | `viper_large` | ❌ No | In DB, not in SNA III list |
| ❌ | Elemental, Medium (Air) | `medium_air_elemental` | ❌ No | **Not in DB** — only Small elementals exist |
| ❌ | Elemental, Medium (Earth) | `medium_earth_elemental` | ❌ No | **Not in DB** |
| ❌ | Elemental, Medium (Fire) | `medium_fire_elemental` | ❌ No | **Not in DB** |
| ❌ | Elemental, Medium (Water) | `medium_water_elemental` | ❌ No | **Not in DB** |

### Wrong Entries Currently in SNA III (should be removed)

| Creature | NpcDefinitionId | Problem |
|----------|-----------------|---------|
| Black Bear | `black_bear` | Correct creature but wrong level — belongs on SNA **II** |
| Dire Badger | `dire_badger` | Correct creature but wrong level — belongs on SNA **II** |
| Crocodile | `crocodile` | Correct creature but wrong level — belongs on SNA **II** |
| Wolverine | `wolverine` | Correct creature but wrong level — belongs on SNA **II** |
| Dire Bat | `dire_bat` | Correct creature but wrong level — belongs on SNA **II** |

### SNA III Summary
- **Canonical:** 13 creatures (8 land animals, 1 fey, 4 elementals)
- **In DB:** 8/13 (missing Satyr + all 4 Medium Elementals)
- **Currently listed:** 1/13 correct, 5 wrong-level entries, 12 canonical creatures missing

---

## SNA IV — 4th-Level (PHB p.289)

### Canonical Creatures (12)

| Status | Creature | NpcDefinitionId | In Current List? | Notes |
|--------|----------|-----------------|------------------|-------|
| ✅ | Bear, Brown (Grizzly) | `brown_bear` | ❌ No | In DB as `brown_bear`. Current list uses `dire_bear` which is WRONG creature |
| ❌ | Crocodile, Giant | `giant_crocodile` | ❌ No | **Not in DB** (only regular `crocodile` exists) |
| ✅ | Dire Boar | `dire_boar` | ❌ No | In DB, not in SNA IV list |
| ❌ | Dire Wolverine | `dire_wolverine` | ❌ No | **Not in DB** (only regular `wolverine` exists) |
| ✅ ⚠️ | Shark, Large | `large_shark` | ❌ No | In DB — aquatic but implemented |
| ✅ | Snake, Huge Viper | `viper_huge` | ❌ No | In DB, not in SNA IV list |
| ✅ | Tiger | `tiger` | ❌ No | In DB, not in SNA IV list |
| ❌ | Unicorn | `unicorn` | ❌ No | Magical beast — **not in DB** |
| ❌ | Elemental, Large (Air) | `large_air_elemental` | ❌ No | **Not in DB** |
| ❌ | Elemental, Large (Earth) | `large_earth_elemental` | ❌ No | **Not in DB** |
| ❌ | Elemental, Large (Fire) | `large_fire_elemental` | ❌ No | **Not in DB** |
| ❌ | Elemental, Large (Water) | `large_water_elemental` | ❌ No | **Not in DB** |

### Wrong Entries Currently in SNA IV (should be removed/fixed)

| Creature | NpcDefinitionId | Problem |
|----------|-----------------|---------|
| Dire Wolf | `dire_wolf` | Correct creature but wrong level — belongs on SNA **III** |
| Lion | `lion` | Correct creature but wrong level — belongs on SNA **III** |
| Giant Eagle | `giant_eagle` | Correct creature but wrong level — belongs on SNA **III** |
| Giant Owl | `giant_owl` | Correct creature but wrong level — belongs on SNA **III** |
| Brown Bear | `dire_bear` | **Wrong NpcDefinitionId!** Uses `dire_bear` (CR 7 Dire Bear) instead of `brown_bear` (CR 4 Brown/Grizzly Bear) |

### SNA IV Summary
- **Canonical:** 12 creatures (5 land animals, 1 aquatic, 1 magical beast, 1 fey-adjacent, 4 elementals)
- **In DB:** 5/12 (missing Giant Crocodile, Dire Wolverine, Unicorn + all 4 Large Elementals)
- **Currently listed:** 0/12 correct (all 5 entries are wrong-level or wrong-ID), 12 canonical creatures missing

---

## NPCDatabase Missing Creatures Summary

### Must Create (15 creatures)

| Priority | Creature | Expected ID | SNA Level | Type | Notes |
|----------|----------|-------------|-----------|------|-------|
| 🔴 High | Medium Air Elemental | `medium_air_elemental` | III | Elemental | Scale up from `small_air_elemental` |
| 🔴 High | Medium Earth Elemental | `medium_earth_elemental` | III | Elemental | Scale up from `small_earth_elemental` |
| 🔴 High | Medium Fire Elemental | `medium_fire_elemental` | III | Elemental | Scale up from `small_fire_elemental` |
| 🔴 High | Medium Water Elemental | `medium_water_elemental` | III | Elemental | Scale up from `small_water_elemental` |
| 🔴 High | Large Air Elemental | `large_air_elemental` | IV | Elemental | MM p.95-100 |
| 🔴 High | Large Earth Elemental | `large_earth_elemental` | IV | Elemental | MM p.95-100 |
| 🔴 High | Large Fire Elemental | `large_fire_elemental` | IV | Elemental | MM p.95-100 |
| 🔴 High | Large Water Elemental | `large_water_elemental` | IV | Elemental | MM p.95-100 |
| 🟡 Medium | Dire Wolverine | `dire_wolverine` | IV | Animal | MM p.67, based on existing `wolverine` |
| 🟡 Medium | Giant Crocodile | `giant_crocodile` | IV | Animal | MM p.271, based on existing `crocodile` |
| 🟡 Medium | Unicorn | `unicorn` | IV | Magical Beast | MM p.249-250 |
| 🟡 Medium | Satyr (without pipes) | `satyr` | III | Fey | MM p.219, CR 2 without pipes |
| 🟢 Low | Porpoise | `porpoise` | I | Animal | Aquatic — land prototype only |
| 🟢 Low | Shark, Medium | `medium_shark` | II | Animal | Aquatic — land prototype only |
| 🟢 Low | Squid | `squid` | II | Animal | Aquatic — land prototype only |

### Existing Summon Aliases to Review

The current alias system maps some summon IDs to unrelated creatures as stand-ins:

| Alias ID | Maps To | Display Name | Assessment |
|----------|---------|--------------|------------|
| `wolf` | `wolf_pack_hunter` | Wolf | ✅ Reasonable — wolf variant |
| `badger` | `dire_badger` | Badger | ⚠️ Wrong — Badger ≠ Dire Badger (different CR) |
| `riding_dog` | `dog` | Riding Dog | ⚠️ Reversed — should be dog aliasing to riding_dog, not vice versa |
| `owl` | `eagle` | Owl | ⚠️ Wrong creature entirely — Owl and Eagle are different animals |
| `raven` | `eagle` | Raven | ⚠️ Wrong creature entirely |
| `giant_bee` | `dire_bat` | Giant Bee | ⚠️ Wrong creature entirely |

---

## Corrected SNA Lists (What the Code Should Look Like)

### SNA I — Corrected

```
Dire Rat          → dire_rat           ✅ ready
Eagle             → eagle              ✅ ready
Monkey            → monkey             ✅ ready
Octopus           → octopus            ✅ ready (aquatic)
Owl               → owl                ✅ ready (via alias → eagle, but alias is wrong creature)
Porpoise          → porpoise           ❌ needs DB entry (aquatic)
Dog, Riding       → riding_dog         ✅ ready
Snake, Sm. Viper  → viper_small        ✅ ready
```

### SNA II — Corrected

```
Bear, Black       → black_bear              ✅ ready
Crocodile         → crocodile               ✅ ready
Dire Badger       → dire_badger             ✅ ready
Dire Bat          → dire_bat                ✅ ready
Shark, Medium     → medium_shark            ❌ needs DB entry (aquatic)
Snake, Med. Viper → viper_medium            ✅ ready
Squid             → squid                   ❌ needs DB entry (aquatic)
Wolverine         → wolverine               ✅ ready
Sm. Air Elemental → small_air_elemental     ✅ ready
Sm. Earth Elem.   → small_earth_elemental   ✅ ready
Sm. Fire Elem.    → small_fire_elemental    ✅ ready
Sm. Water Elem.   → small_water_elemental   ✅ ready
```

### SNA III — Corrected

```
Ape               → ape                     ✅ ready
Dire Weasel       → dire_weasel             ✅ ready
Dire Wolf         → dire_wolf               ✅ ready
Eagle, Giant      → giant_eagle             ✅ ready
Lion              → lion                    ✅ ready
Owl, Giant        → giant_owl               ✅ ready
Satyr             → satyr                   ❌ needs DB entry
Snake, Constrictor→ constrictor_snake        ✅ ready
Snake, Lg. Viper  → viper_large             ✅ ready
Med. Air Elem.    → medium_air_elemental    ❌ needs DB entry
Med. Earth Elem.  → medium_earth_elemental  ❌ needs DB entry
Med. Fire Elem.   → medium_fire_elemental   ❌ needs DB entry
Med. Water Elem.  → medium_water_elemental  ❌ needs DB entry
```

### SNA IV — Corrected

```
Bear, Brown       → brown_bear              ✅ ready (NOT dire_bear!)
Crocodile, Giant  → giant_crocodile         ❌ needs DB entry
Dire Boar         → dire_boar               ✅ ready
Dire Wolverine    → dire_wolverine          ❌ needs DB entry
Shark, Large      → large_shark             ✅ ready (aquatic but in DB)
Snake, Huge Viper → viper_huge              ✅ ready
Tiger             → tiger                   ✅ ready
Unicorn           → unicorn                 ❌ needs DB entry
Lg. Air Elem.     → large_air_elemental     ❌ needs DB entry
Lg. Earth Elem.   → large_earth_elemental   ❌ needs DB entry
Lg. Fire Elem.    → large_fire_elemental    ❌ needs DB entry
Lg. Water Elem.   → large_water_elemental   ❌ needs DB entry
```

---

## Overall Readiness by SNA Level

| Level | Canonical | In DB | Ready Now (land) | Aquatic Only | Needs New DB Entry |
|-------|-----------|-------|-------------------|--------------|--------------------|
| SNA I | 8 | 7 | 6 | 1 missing | 1 (Porpoise) |
| SNA II | 12 | 10 | 8 | 2 missing | 2 (Med. Shark, Squid) |
| SNA III | 13 | 8 | 8 | 0 | 5 (Satyr + 4 Med. Elementals) |
| SNA IV | 12 | 5 | 5 | 0 | 7 (Giant Croc, Dire Wolverine, Unicorn + 4 Lg. Elementals) |
| **Total** | **45** | **30** | **27** | **3 missing** | **15** |

---

## Recommended Action Plan

### Phase 1 — Fix Creature Lists (no new NPCs needed)
Rewrite `GetSummonNaturesAllyI–IV()` using only creatures that already exist in NPCDatabase. This alone gives us 27 land creatures across all 4 levels (vs. the current 24 entries, most of which are wrong).

### Phase 2 — Add Medium & Large Elementals (8 NPCs)
Scale up from existing Small Elemental templates. These are core to the SNA identity (druids summoning nature spirits) and needed for levels III & IV.

### Phase 3 — Add Missing Animals (3 NPCs)
- `dire_wolverine` — scale up from `wolverine`
- `giant_crocodile` — scale up from `crocodile`  
- `satyr` — new fey creature (CR 2 without pipes)

### Phase 4 — Add Special Creatures (1 NPC)
- `unicorn` — magical beast with special abilities (MM p.249)

### Phase 5 — Aquatic Creatures (3 NPCs, optional)
- `porpoise`, `medium_shark`, `squid` — only if aquatic combat is added

---

*Report generated from project state at commit `7d7a968`. No code changes were made.*

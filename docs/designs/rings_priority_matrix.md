# Magic Rings Priority Matrix — Impact vs Complexity Analysis

> **Source:** D&D 3.5e Dungeon Master's Guide (pp. 229–233), Core Rules Only  
> **Project:** `/home/ubuntu/dnd35prototype`  
> **Date:** 2026-05-24

---

## Scoring Methodology

### Impact Score (1–5)
How much value the ring adds to gameplay and player experience:

| Score | Label | Criteria |
|-------|-------|----------|
| 5 | **Critical** | Core combat/survival mechanic, used constantly, iconic D&D item |
| 4 | **High** | Frequently useful, significant tactical value, broad applicability |
| 3 | **Moderate** | Situationally valuable, enhances specific builds or encounters |
| 2 | **Low** | Niche use cases, rarely changes outcomes |
| 1 | **Minimal** | Flavor/RP item, almost never mechanically relevant |

### Complexity Score (1–5)
Implementation difficulty considering existing systems:

| Score | Label | Criteria |
|-------|-------|----------|
| 1 | **Trivial** | Single stat modifier, existing system handles it |
| 2 | **Simple** | Few stat mods or one conditional trigger |
| 3 | **Moderate** | New UI/activation needed, multiple interacting effects |
| 4 | **Hard** | New subsystem required, complex state management |
| 5 | **Very Hard** | Multiple new systems, heavy AI/spell integration |

### Priority Score Formula
```
Priority Score = Impact × (6 - Complexity)
```
- Maximum possible: 5 × 5 = 25 (high impact, trivial complexity)
- Minimum possible: 1 × 1 = 1 (minimal impact, very hard complexity)

Higher score = implement sooner.

---

## Complete Priority Matrix

### Sorted by Priority Score (Descending)

| Rank | Ring | Impact | Complexity | Priority Score | Tier | New Systems Needed |
|------|------|--------|------------|----------------|------|--------------------|
| 1 | **Protection +1 to +5** | 5 | 1 | **25** | T1 | None — DeflectionBonus exists |
| 2 | **Resistance, Minor/Major/Greater** | 5 | 1 | **25** | T1 | None — save bonus fields exist |
| 3 | **Freedom of Movement** | 5 | 2 | **20** | T1 | None — spell already exists |
| 4 | **Evasion** | 4 | 1 | **20** | T1 | None — HasEvasion exists |
| 5 | **Force Shield** | 4 | 1 | **20** | T1 | None — ShieldBonus exists |
| 6 | **Counterspells** | 4 | 2 | **16** | T2 | Counterspell trigger system |
| 7 | **Invisibility** | 4 | 2 | **16** | T2 | Command activation UI |
| 8 | **Spell Storing, Minor** | 4 | 3 | **12** | T3 | Spell storage system |
| 9 | **Sustenance** | 3 | 1 | **15** | T1 | None (if hunger not tracked) |
| 10 | **Climbing** | 3 | 1 | **15** | T1 | None — competence bonus to Climb |
| 11 | **Jumping** | 3 | 1 | **15** | T1 | None — competence bonus to Jump |
| 12 | **Swimming** | 3 | 1 | **15** | T1 | None — competence bonus to Swim |
| 13 | **Feather Falling** | 3 | 2 | **12** | T1 | Auto-trigger on fall detection |
| 14 | **Mind Shielding** | 3 | 1 | **15** | T1 | None — immunity flags |
| 15 | **Water Walking** | 2 | 2 | **8** | T1 | Movement system hook |
| 16 | **Animal Friendship** | 3 | 2 | **12** | T2 | Charm animal mechanics |
| 17 | **Chameleon Power** | 3 | 2 | **12** | T2 | Stealth bonus + activation |
| 18 | **Energy Resistance, Minor/Major/Greater** | 4 | 2 | **16** | T1 | None — DamageResistances exists |
| 19 | **Blinking** | 4 | 2 | **16** | T2 | Command activation UI |
| 20 | **Spell Turning** | 4 | 4 | **8** | T3 | SR-like reflection system |
| 21 | **Ram** | 3 | 3 | **9** | T2 | Charge-based ranged force attack |
| 22 | **Wizardry (I–IV)** | 4 | 2 | **16** | T1 | Bonus spell slot system |
| 23 | **Regeneration** | 4 | 3 | **12** | T3 | Fast healing + regrowth system |
| 24 | **Telekinesis** | 3 | 3 | **9** | T2 | Command activation + spell link |
| 25 | **X-Ray Vision** | 2 | 4 | **4** | T3 | Vision/wall-piercing rendering |
| 26 | **Friend Shield (pair)** | 2 | 3 | **6** | T3 | Paired item + damage sharing |
| 27 | **Shooting Stars** | 3 | 4 | **6** | T3 | Multiple activated abilities, area effects |
| 28 | **Spell Storing, Major** | 4 | 4 | **8** | T3 | Extended spell storage system |
| 29 | **Three Wishes** | 3 | 4 | **6** | T4 | WishExecutor exists, needs charge depletion |
| 30 | **Djinni Calling** | 2 | 5 | **2** | T4 | Planar ally summoning AI |
| 31 | **Elemental Command** | 3 | 5 | **3** | T4 | Multi-mode activated abilities, summon system |
| 32 | **Air/Earth/Fire/Water** | 3 | 5 | **3** | T4 | (Variants of Elemental Command) |
| 33 | **Spell Storing (standard)** | 4 | 3 | **12** | T3 | Spell storage system |

---

## Priority Quadrant Analysis

### 🟢 Quadrant 1: HIGH IMPACT / LOW COMPLEXITY — **Implement First**
*Priority Score ≥ 15*

| Ring | I | C | PS | Notes |
|------|---|---|----|-------|
| Protection +1–+5 | 5 | 1 | 25 | Single field: `DeflectionBonus` |
| Resistance (all) | 5 | 1 | 25 | Single field per save type |
| Freedom of Movement | 5 | 2 | 20 | Existing spell, continuous |
| Evasion | 4 | 1 | 20 | Existing `HasEvasion` flag |
| Force Shield | 4 | 1 | 20 | Existing `ShieldBonus` field |
| Energy Resistance (all) | 4 | 2 | 16 | Existing `DamageResistances` dict |
| Wizardry I–IV | 4 | 2 | 16 | Bonus spell slots per level |
| Counterspells | 4 | 2 | 16 | Reactive trigger |
| Invisibility | 4 | 2 | 16 | Existing spell + command UI |
| Blinking | 4 | 2 | 16 | Existing spell + command UI |
| Sustenance | 3 | 1 | 15 | Immunity flag |
| Climbing | 3 | 1 | 15 | +10 competence to Climb |
| Jumping | 3 | 1 | 15 | +10 competence to Jump |
| Swimming | 3 | 1 | 15 | +10 competence to Swim |
| Mind Shielding | 3 | 1 | 15 | Immunity flags |

**Total: 15 rings | Est. effort: 1–2 weeks**

### 🟡 Quadrant 2: HIGH IMPACT / HIGH COMPLEXITY — **Plan Carefully**
*Impact ≥ 4, Complexity ≥ 3*

| Ring | I | C | PS | Notes |
|------|---|---|----|-------|
| Spell Storing (Minor) | 4 | 3 | 12 | Needs spell storage subsystem |
| Spell Storing (Standard) | 4 | 3 | 12 | Same system, higher level cap |
| Spell Storing (Major) | 4 | 4 | 8 | Same system, highest level cap |
| Regeneration | 4 | 3 | 12 | Fast healing + limb regrowth |
| Spell Turning | 4 | 4 | 8 | SR + reflection tracking |

**Total: 5 rings | Est. effort: 2–3 weeks**

### 🔵 Quadrant 3: LOW IMPACT / LOW COMPLEXITY — **Fill In Gaps**
*Impact ≤ 3, Complexity ≤ 2*

| Ring | I | C | PS | Notes |
|------|---|---|----|-------|
| Feather Falling | 3 | 2 | 12 | Auto-trigger, simple |
| Animal Friendship | 3 | 2 | 12 | Charm animal 1/day |
| Chameleon Power | 3 | 2 | 12 | Stealth bonus + disguise |
| Water Walking | 2 | 2 | 8 | Movement flag |

**Total: 4 rings | Est. effort: 3–5 days**

### 🔴 Quadrant 4: LOW IMPACT / HIGH COMPLEXITY — **Defer or Simplify**
*Impact ≤ 3, Complexity ≥ 3*

| Ring | I | C | PS | Notes |
|------|---|---|----|-------|
| Ram | 3 | 3 | 9 | Charge-based ranged attack |
| Telekinesis | 3 | 3 | 9 | Spell link + activation |
| Friend Shield | 2 | 3 | 6 | Paired item system |
| Shooting Stars | 3 | 4 | 6 | Multiple complex abilities |
| X-Ray Vision | 2 | 4 | 4 | Rendering system needed |
| Three Wishes | 3 | 4 | 6 | WishExecutor helps, but charges |
| Elemental Command (×4) | 3 | 5 | 3 | Most complex rings in game |
| Djinni Calling | 2 | 5 | 2 | Full summoning AI needed |

**Total: 9 rings | Est. effort: 3–4 weeks**

---

## Recommended Implementation Order

### Sprint 1: Foundation + Quick Wins (Week 1–2)
**Goal:** Ring infrastructure + all Quadrant 1 rings

| Order | Ring | PS | Why First |
|-------|------|----|-----------|
| 1 | Protection +1–+5 | 25 | Highest PS, tests slot system |
| 2 | Resistance (Minor/Major/Greater) | 25 | Same pattern, save bonuses |
| 3 | Evasion | 20 | Single flag flip |
| 4 | Force Shield | 20 | Single stat mod |
| 5 | Freedom of Movement | 20 | Tests continuous spell effect |
| 6 | Energy Resistance (all) | 16 | Existing damage resistance system |
| 7 | Wizardry I–IV | 16 | Important for casters |
| 8 | Invisibility | 16 | First command-word ring |
| 9 | Blinking | 16 | Second command-word ring |
| 10 | Counterspells | 16 | Reactive trigger pattern |
| 11–15 | Sustenance, Climbing, Jumping, Swimming, Mind Shielding | 15 | Simple stat/flag rings |

**Deliverables:**
- `RingFactory` / `RingDatabase` — ring registration pipeline
- `RecalculateStats()` updated to process ring slots
- Command-word activation UI (reusable for all future rings)
- 15+ rings fully functional

### Sprint 2: Moderate Systems (Week 3–4)
**Goal:** Quadrant 2 + Quadrant 3 rings

| Order | Ring | PS | Why Now |
|-------|------|----|---------|
| 16 | Spell Storing (Minor) | 12 | Builds spell storage system |
| 17 | Spell Storing (Standard) | 12 | Extends same system |
| 18 | Regeneration | 12 | Fast healing ticker |
| 19 | Feather Falling | 12 | Auto-trigger pattern |
| 20 | Animal Friendship | 12 | Charm effect 1/day |
| 21 | Chameleon Power | 12 | Stealth + disguise |
| 22 | Ram | 9 | Charge-based force attack |
| 23 | Telekinesis | 9 | Links to existing spell |

**Deliverables:**
- Spell storage subsystem (reusable for other items)
- Charge depletion system
- Auto-trigger framework
- 8 more rings functional (23 total)

### Sprint 3: Complex Rings (Week 5–7)
**Goal:** Quadrant 2 remainder + select Quadrant 4

| Order | Ring | PS | Why Now |
|-------|------|----|---------|
| 24 | Spell Storing (Major) | 8 | Extends existing storage system |
| 25 | Spell Turning | 8 | SR + reflection |
| 26 | Water Walking | 8 | Movement hook |
| 27 | Three Wishes | 6 | WishExecutor exists |
| 28 | Shooting Stars | 6 | Multi-ability ring |
| 29 | Friend Shield | 6 | Paired item prototype |

**Deliverables:**
- Spell reflection system
- Wish charge depletion
- Multi-ability ring framework
- 6 more rings (29 total)

### Sprint 4: Legendary Rings (Week 8–10)
**Goal:** Remaining Quadrant 4 — defer if needed

| Order | Ring | PS | Notes |
|-------|------|----|-------|
| 30 | X-Ray Vision | 4 | May simplify to skill bonus |
| 31 | Elemental Command (Air) | 3 | Template for all variants |
| 32 | Elemental Command (Earth/Fire/Water) | 3 | Clone + customize |
| 33 | Djinni Calling | 2 | Requires summoning AI |

**Deliverables:**
- Elemental command multi-mode framework
- Summoning/calling subsystem
- All 33 ring types complete

---

## Summary Statistics

| Metric | Value |
|--------|-------|
| **Total Rings** | 33 types (47 variants) |
| **Average Priority Score** | 12.1 |
| **Median Priority Score** | 12 |
| **Highest Priority** | Protection, Resistance (25) |
| **Lowest Priority** | Djinni Calling (2) |
| **Quadrant 1 (do first)** | 15 rings (45%) |
| **Quadrant 2 (plan carefully)** | 5 rings (15%) |
| **Quadrant 3 (fill gaps)** | 4 rings (12%) |
| **Quadrant 4 (defer)** | 9 rings (27%) |
| **Rings using only existing systems** | 15 (45%) |
| **Rings needing command activation UI** | 8 (24%) |
| **Rings needing new subsystems** | 10 (30%) |
| **Estimated total effort** | 8–10 weeks |

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `RecalculateStats()` ring integration breaks existing gear | Medium | High | Unit test all existing equipment before/after |
| Command activation UI delays | Medium | Medium | Stub with auto-activate; add UI later |
| Spell storage system scope creep | High | Medium | Implement Minor first, gate Major behind it |
| Elemental Command scope explosion | High | High | Implement as simplified stat rings first; add modes incrementally |
| Ring of Three Wishes balance issues | Low | High | Gate behind DM confirmation dialog |

---

*This matrix should be reviewed after Sprint 1 completion. Actual complexity may shift once the ring infrastructure is built and shared systems are available for reuse.*

# D&D 3.5e Rods — Complexity Tier Classification

> **Companion to:** rods_implementation_plan.md  
> **Purpose:** Classify all 36 rod items by implementation complexity  
> **Date:** May 2026

---

## Tier Overview

| Tier | Stars | Description | Count | Est. Time Per Rod |
|------|-------|-------------|:-----:|:-----------------:|
| 1 | ⭐ | Simple Passive / Single Toggle | 2 | 0.5 days |
| 2 | ⭐⭐ | Single Active Ability | 5 | 1 day |
| 3 | ⭐⭐⭐ | Multi-Use / Metamagic / Moderate Complexity | 25 | 1–2 days |
| 4 | ⭐⭐⭐⭐ | Multi-Ability Complex Systems | 4 | 3–4 days |

---

## Tier 1 — Simple Passive ⭐

**Characteristics:**
- Passive bonuses while held (no activation required)
- Single on/off toggle at most
- No daily limits, charges, or complex state

| Rod | Price | Key Mechanic | Implementation |
|-----|------:|:-------------|:---------------|
| **Immovable Rod** | 5,000 gp | Button toggles fixed-in-space state | Set `isImmovable` flag; apply 8,000 lb weight limit; DC 30 to move. Minimal state. |
| **Rod of Splendor** | 25,000 gp | +4 CHA enhancement while held | Apply stat modifier on equip, remove on unequip. Daily abilities (apparel/tent) are flavor, low combat impact — can be deferred. |

**Total Tier 1 Effort:** ~1 day

---

## Tier 2 — Single Active Ability ⭐⭐

**Characteristics:**
- One primary active ability
- Command word or use-activated
- May have daily limits but straightforward tracking
- No complex multi-mode behavior

| Rod | Price | Key Mechanic | Implementation |
|-----|------:|:-------------|:---------------|
| **Rod of Metal & Mineral Detection** | 10,500 gp | Detect metal within 30 ft (full-round) | Scan tiles in radius, highlight metal objects/deposits. Unlimited use. |
| **Rod of Enemy Detection** | 23,500 gp | Detect hostiles within 60 ft, 3/day | Scan for hostile creatures, point toward nearest. Track 3 daily uses × 10 min each. |
| **Rod of Cancellation** | 11,000 gp | Drain magic from touched item; rod destroyed | Melee touch attack → remove item enchantment → destroy rod. One-time-use item. |
| **Rod of Negation** | 37,000 gp | Dispel item magic via ray, 3/day | Ranged touch attack → *greater dispel magic* vs item properties. Track 3/day. |
| **Rod of Flame Extinguishing** | 15,000 gp | Extinguish fires, charge-based | 10 charges/day, renewable. Different charge costs by fire size. Niche utility. |

**Total Tier 2 Effort:** ~5 days

---

## Tier 3 — Metamagic / Multi-Use / Moderate Complexity ⭐⭐⭐

**Characteristics:**
- Multiple uses or modes but within a single system
- Metamagic rods: integrate with spell casting, daily tracking
- Combat rods: weapon transformation + special effects
- May require new subsystems but scope is bounded

### Metamagic Rods (18 variants)

All metamagic rods share a common system — implement once, parameterize for each type/tier.

| Rod Type | Lesser (≤3rd) | Normal (≤6th) | Greater (≤9th) | Key Mechanic |
|:---------|:---:|:---:|:---:|:-------------|
| **Empower** | 9,000 | 32,500 | 73,000 | Numeric spell effects ×1.5 |
| **Enlarge** | 3,000 | 11,000 | 24,500 | Double spell range |
| **Extend** | 3,000 | 11,000 | 24,500 | Double spell duration |
| **Maximize** | 14,000 | 54,000 | 121,500 | All numeric values at maximum |
| **Quicken** | 35,000 | 75,500 | 170,000 | Cast as swift action |
| **Silent** | 3,000 | 11,000 | 24,500 | No verbal component needed |

**Shared Implementation:**
- Metamagic system core: 3–4 days
- Per-type implementation: ~0.5 days each (3 days for 6 types)
- Tier variants are data-driven (change MaxSpellLevel param): ~0 extra days
- **Subtotal: ~6–7 days for all 18**

### Non-Metamagic Tier 3 Rods (7 rods)

| Rod | Price | Key Mechanic | Complexity Notes |
|-----|------:|:-------------|:-----------------|
| **Rod of Python** | 13,000 gp | Transform to snake ally, 3/day + 1/week giant | Requires creature summoning system, basic AI, telepathic commands. |
| **Rod of the Viper** | 19,000 gp | +2 heavy mace, poison serpent head 1/day | Weapon stats + poison effect (1d10 CON, Fort DC 14). Evil-only restriction. |
| **Rod of Withering** | 25,000 gp | Touch attack: 1d4 STR + 1d4 CON damage | Apply ability damage on successful touch. Straightforward. |
| **Rod of Flailing** | 50,000 gp | Transform to +3 dire flail; +4 AC/saves 1/day | Weapon transformation + defensive buff. Two distinct abilities. |
| **Rod of Rulership** | 60,000 gp | Mass charm 300 HD, 120 ft, Will DC 16 | Apply charm effect to multiple targets, track 500 min total usage. |
| **Rod of Security** | 61,000 gp | Transport to safe demiplane | Create safe zone, track 200 person-days total. Mostly narrative. |
| **Rod of Alertness** | 85,000 gp | +1 mace, +1 init, 8 at-will detects, alertness mode 1/day, animate 1/day | Many abilities but each is simple. Passive bonus + detect spells + group buff. |

**Subtotal:** ~7–8 days for non-metamagic Tier 3

**Total Tier 3 Effort:** ~13–15 days

---

## Tier 4 — Multi-Ability Complex ⭐⭐⭐⭐

**Characteristics:**
- Multiple distinct ability systems
- Require dedicated managers/controllers
- Complex state management
- Novel mechanics not shared with other items

| Rod | Price | Key Mechanic | Complexity Notes |
|-----|------:|:-------------|:-----------------|
| **Rod of Wonder** | 12,000 gp | d100 table → 20 unique effect categories | Must implement ~20 distinct spell/effect types. Random outcome engine. Each effect has different targets, saves, areas. |
| **Rod of Thunder & Lightning** | 33,000 gp | 5 distinct abilities (3 × 1/day, 1 × 1/day, 1 × 1/week) | Five completely different combat abilities. Mixed damage types. Combined mode. |
| **Rod of Absorption** | 50,000 gp | Absorb spells → store levels → convert to slots | Novel absorption mechanic. Integrates with enemy spell casting and wielder's spell slot economy. |
| **Rod of Lordly Might** | 70,000 gp | 4 weapon forms + 3 spell-likes + climbing pole + compass + door forcing | Most complex rod. 6+ modes, each with different stats. Spell-like abilities. Utility functions. |

**Individual Estimates:**
- Rod of Wonder: 3–4 days
- Rod of Thunder & Lightning: 2–3 days
- Rod of Absorption: 2–3 days
- Rod of Lordly Might: 3–4 days

**Total Tier 4 Effort:** ~11–14 days

---

## Implementation Order by Tier

### Recommended Build Sequence

```
Week 1:  Foundation + Tier 1 (Immovable Rod, Splendor passive)
Week 2:  Tier 2 (Cancellation, Negation, Enemy Detection, Metal Detection, Flame Ext.)
Week 3:  Tier 3 — Metamagic Core System
Week 4:  Tier 3 — Metamagic All 18 Variants
Week 5:  Tier 3 — Combat Rods (Flailing, Python, Viper, Withering)
Week 6:  Tier 3 — Utility (Alertness, Rulership, Security)
Week 7:  Tier 4 — Wonder + Thunder & Lightning
Week 8:  Tier 4 — Absorption + Lordly Might
```

### Dependency Graph

```
Foundation (Equip System, Rod Base Classes)
├── Tier 1: Immovable Rod, Splendor
├── Tier 2: Cancellation, Negation, Detection rods, Flame Ext.
├── Tier 3 — Metamagic System
│   └── All 18 metamagic rod variants
├── Tier 3 — Combat Rods
│   ├── Withering (needs: ability damage system)
│   ├── Viper (needs: poison system, alignment check)
│   ├── Flailing (needs: weapon transform, dire flail stats)
│   └── Python (needs: creature summoning, basic creature AI)
├── Tier 3 — Utility
│   ├── Alertness (needs: detect spells, passive bonuses)
│   ├── Rulership (needs: mass charm system, duration tracking)
│   └── Security (needs: demiplane/safe-zone logic)
├── Tier 4
│   ├── Wonder (needs: ~20 spell/effect implementations)
│   ├── Thunder & Lightning (needs: sonic damage, electricity, combined modes)
│   ├── Absorption (needs: spell interception, spell slot restoration)
│   └── Lordly Might (needs: multi-form weapon, 3 spell-likes, utility modes)
```

---

## Complexity Metrics Summary

| Metric | Value |
|--------|-------|
| Total unique rod types | 24 |
| Total individual rod items | 36 |
| New systems required | 6 |
| Tier 1 rods | 2 |
| Tier 2 rods | 5 |
| Tier 3 rods | 25 (18 metamagic + 7 other) |
| Tier 4 rods | 4 |
| Estimated total effort | 7.5 weeks |
| Code files (new) | ~12 |
| Code files (modified) | ~4 |

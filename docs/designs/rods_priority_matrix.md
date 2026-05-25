# D&D 3.5e Rods — Priority Matrix (Impact vs. Complexity)

> **Companion to:** rods_implementation_plan.md  
> **Purpose:** Rank all 36 rod items by gameplay impact and implementation complexity  
> **Date:** May 2026

---

## Priority Matrix Visualization

```
                        HIGH IMPACT
                            │
          P1 CORE           │           P2 HIGH
    ┌─────────────────┐     │     ┌─────────────────┐
    │ • Metamagic     │     │     │ • Absorption     │
    │   Rods (all 18) │     │     │ • Lordly Might   │
    │ • Immovable Rod │     │     │ • Thunder &      │
    │                 │     │     │   Lightning      │
    └─────────────────┘     │     │ • Rod of Wonder  │
                            │     └─────────────────┘
   LOW ─────────────────────┼──────────────────────── HIGH
   COMPLEXITY               │                    COMPLEXITY
          P4 QUICK WINS     │           P3 MODERATE
    ┌─────────────────┐     │     ┌─────────────────┐
    │ • Metal & Min.  │     │     │ • Python         │
    │   Detection     │     │     │ • Alertness      │
    │ • Flame Ext.    │     │     │ • Rulership      │
    │ • Splendor      │     │     │ • Security       │
    │ • Enemy Det.    │     │     │ • Flailing        │
    └─────────────────┘     │     └─────────────────┘
                            │
                        LOW IMPACT
```

---

## Priority Tiers

### P1 — CORE (Build First) 🔴

These are foundational or high-frequency items that define the rod category.

| Rod | Price | Impact | Complexity | Rationale |
|:----|------:|:------:|:----------:|:----------|
| **Immovable Rod** | 5,000 gp | ★★★★★ | ★☆☆☆☆ | Most iconic rod in D&D. Simple to implement. Used constantly by creative players. High visibility, low effort. |
| **Metamagic Rods (all 18)** | 3,000–170,000 gp | ★★★★★ | ★★★☆☆ | Core spellcaster mechanic. Every caster wants one. 18 variants from one shared system. High ROI. |

**Estimated effort:** 2 weeks  
**Value delivered:** ~53% of all rod items (19 of 36)

---

### P2 — HIGH PRIORITY 🟠

High gameplay impact but require dedicated complex systems.

| Rod | Price | Impact | Complexity | Rationale |
|:----|------:|:------:|:----------:|:----------|
| **Rod of Cancellation** | 11,000 gp | ★★★★☆ | ★☆☆☆☆ | Unique "destroy magic item" mechanic. One-use makes it simple. Dramatic gameplay moments. |
| **Rod of Negation** | 37,000 gp | ★★★★☆ | ★★☆☆☆ | Counter-magic utility. Dispel magic items 3/day. Straightforward ray attack. |
| **Rod of Withering** | 25,000 gp | ★★★★☆ | ★★☆☆☆ | Ability damage weapon (STR + CON). Builds on existing ability damage system. |
| **Rod of the Viper** | 19,000 gp | ★★★☆☆ | ★★☆☆☆ | +2 heavy mace with poison. Needs alignment check system. |
| **Rod of Flailing** | 50,000 gp | ★★★☆☆ | ★★★☆☆ | Weapon transformation + defensive buff. Two separate systems. |
| **Rod of Absorption** | 50,000 gp | ★★★★★ | ★★★★☆ | Novel spell absorption mechanic. Integrates with spell slot economy. Complex state. |
| **Rod of Lordly Might** | 70,000 gp | ★★★★★ | ★★★★★ | Most complex item in the game. 4 weapon forms + 3 spell-likes + utility. Showcase item. |

**Estimated effort:** 2.5 weeks  
**Value delivered:** +7 rods (total: 26 of 36)

---

### P3 — MODERATE PRIORITY 🟡

Good gameplay value, moderate complexity. Fun but not essential.

| Rod | Price | Impact | Complexity | Rationale |
|:----|------:|:------:|:----------:|:----------|
| **Rod of Wonder** | 12,000 gp | ★★★★☆ | ★★★★☆ | 20 random effect categories. Enormous fun factor. Implementation is wide (many effects) rather than deep. |
| **Rod of Thunder & Lightning** | 33,000 gp | ★★★★☆ | ★★★★☆ | 5 distinct combat abilities. Multiple damage types. Combined attack mode. |
| **Rod of Python** | 13,000 gp | ★★★☆☆ | ★★★☆☆ | Creature summoning + basic AI. Reusable system for other summon items. |
| **Rod of Alertness** | 85,000 gp | ★★★☆☆ | ★★★☆☆ | Many abilities but each is simple. Good passive bonuses. 8 detect spells at-will. |
| **Rod of Rulership** | 60,000 gp | ★★★☆☆ | ★★★☆☆ | Mass charm. Needs mass save system. 500-minute total usage tracking. |

**Estimated effort:** 2 weeks  
**Value delivered:** +5 rods (total: 31 of 36)

---

### P4 — LOWER PRIORITY 🟢

Niche utility or minimal combat impact. Implement last or defer.

| Rod | Price | Impact | Complexity | Rationale |
|:----|------:|:------:|:----------:|:----------|
| **Rod of Metal & Mineral Detection** | 10,500 gp | ★★☆☆☆ | ★☆☆☆☆ | Niche exploration tool. Simple scan mechanic. |
| **Rod of Enemy Detection** | 23,500 gp | ★★☆☆☆ | ★★☆☆☆ | Detect hostiles. Useful but not exciting. May duplicate existing detection spells. |
| **Rod of Flame Extinguishing** | 15,000 gp | ★★☆☆☆ | ★★☆☆☆ | Charge-based fire suppression. Very situational. |
| **Rod of Splendor** | 25,000 gp | ★★☆☆☆ | ★☆☆☆☆ | +4 CHA passive (easy). Apparel/tent abilities are mostly narrative. |
| **Rod of Security** | 61,000 gp | ★★☆☆☆ | ★★★☆☆ | Safe demiplane. Mostly narrative/non-combat. 200 person-day tracking. |

**Estimated effort:** 1 week  
**Value delivered:** +5 rods (total: 36 of 36, 100%)

---

## Impact Scoring Criteria

| Score | Meaning | Examples |
|:-----:|:--------|:--------|
| ★★★★★ | Game-changing. Every party wants this. | Metamagic rods (core caster boost), Immovable Rod (universally iconic) |
| ★★★★☆ | Highly useful. Frequently chosen. | Rod of Absorption (spell defense), Rod of Wonder (massive fun) |
| ★★★☆☆ | Solid utility. Good for specific builds. | Rod of Python (summoner), Rod of Flailing (martial) |
| ★★☆☆☆ | Niche or situational. | Rod of Flame Extinguishing, Rod of Metal Detection |
| ★☆☆☆☆ | Rarely relevant. | — (no rods rated this low) |

## Complexity Scoring Criteria

| Score | Meaning | What's Involved |
|:-----:|:--------|:----------------|
| ★☆☆☆☆ | Trivial | Single flag toggle or stat modifier |
| ★★☆☆☆ | Simple | One active ability, basic daily tracking |
| ★★★☆☆ | Moderate | Weapon transformation, creature AI, or mass-target effects |
| ★★★★☆ | Complex | Multiple distinct systems, ~20 sub-effects, or novel mechanics |
| ★★★★★ | Very Complex | 6+ modes, combines weapon/spell/utility systems |

---

## Cumulative Progress Projection

| End of Week | Rods Complete | % of Total | Cumulative Items |
|:-----------:|:------------:|:----------:|:----------------:|
| 1 | 1 (foundation) | 3% | Immovable Rod |
| 2 | 19 | 53% | + 18 Metamagic Rods |
| 3 | 24 | 67% | + Cancellation, Negation, Withering, Viper, Flailing |
| 4 | 26 | 72% | + Absorption, Lordly Might |
| 5 | 28 | 78% | + Wonder, Thunder & Lightning |
| 6 | 31 | 86% | + Python, Alertness, Rulership |
| 7 | 34 | 94% | + Detection rods, Flame Ext., Splendor |
| 7.5 | 36 | 100% | + Security, Enemy Detection |

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|:-----|:----------:|:------:|:-----------|
| Metamagic system scope creep | Medium | High | Lock scope to 6 core types, no homebrew |
| Rod of Wonder effect bugs | High | Medium | Implement effects incrementally, test each |
| Lordly Might mode-switching bugs | Medium | Medium | Use state machine pattern, extensive tests |
| Absorption + spell system integration | Medium | High | Design clean interface between systems |
| Python creature AI issues | Medium | Low | Keep AI simple: follow, attack target |
| Save/load state corruption | Low | High | Comprehensive serialization tests early |

---

## Dependencies on Existing Systems

| Rod | Required Existing System | Status |
|:----|:------------------------|:-------|
| Metamagic Rods | Spell casting system | ✅ Exists |
| Withering, Viper | Ability damage system | ✅ Exists (from poisons) |
| Flailing | Weapon transformation | ⚠️ Needs extension |
| Python | Creature summoning + AI | ⚠️ Needs new system |
| Absorption | Spell interception hooks | ❌ New system needed |
| Lordly Might | Multi-form weapon system | ❌ New system needed |
| Wonder | ~20 spell effect implementations | ⚠️ Partially exists |
| Cancellation | Magic item property removal | ⚠️ Needs extension |
| Rulership | Mass charm/save system | ⚠️ Partially exists |

---

## Summary

| Metric | Value |
|:-------|:------|
| Total rods to implement | 36 |
| P1 Core items | 19 (53%) |
| P2 High priority | 7 (19%) |
| P3 Moderate priority | 5 (14%) |
| P4 Lower priority | 5 (14%) |
| New systems required | 6 |
| Existing systems to extend | 4 |
| Total estimated time | 7.5 weeks |
| Highest ROI item | Immovable Rod (iconic, trivial to build) |
| Highest ROI system | Metamagic Rod System (18 items from 1 system) |

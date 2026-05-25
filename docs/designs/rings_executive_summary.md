# D&D 3.5e Magic Rings — Executive Summary & Roadmap

> Consolidated from 4 planning documents | Generated 2026-05-24  
> Source: DMG 3.5e pp. 229–233 (Core Rules Only)

---

## At a Glance

| Metric | Value |
|--------|-------|
| **Distinct ring types** | 33 |
| **Total item variants** (counting +1–+5, energy types, etc.) | 47 |
| **Implementation tiers** | 4 |
| **New systems required** | 7 (4 major, 3 minor) |
| **New files to create** | 8 |
| **Existing files to modify** | 10+ |
| **Estimated total effort** | 8–10 weeks |
| **Quick wins (Day 1–2)** | 5 rings (Protection, Energy Resist, Evasion, FoM, Force Shield) |
| **Rings using existing systems only** | 15 (45%) |

---

## Current Codebase Status

### ✅ Already Working
- **Ring equipment slots** — `EquipSlot.LeftRing`, `EquipSlot.RightRing`, `EquipSlot.EitherRing`
- **Inventory slots wired** — `Inventory.LeftRingSlot`, `Inventory.RightRingSlot`
- **UI ring slots** — "L RING" / "R RING" displayed in both InventoryUI and PreCombatInventoryUI
- **Spell effect ticking** — GameManager already ticks ring slots for item spell durations
- **Key stats/systems** — DeflectionBonus, DamageResistances, HasEvasion, FoM spell, Invisibility, Blink, Telekinesis, Wish system, Spell Turning, wand charge system, staff charge system, spell slot system

### ❌ Missing
- No ring items registered in ItemDatabase
- No ring stat effects in `RecalculateStats()`
- No ring-specific ItemData fields
- No ring activation system (command word / use-activated)
- No RingFactory (unlike WandFactory/PotionFactory/ScrollFactory)

---

## Complexity Distribution

```
⭐      Simple (12 rings / 36%)    — static flags and bonuses
⭐⭐     Low    ( 9 rings / 27%)    — simple abilities, existing spells
⭐⭐⭐    Medium ( 5 rings / 15%)    — daily tracking, spell slot mods
⭐⭐⭐⭐   High   ( 4 rings / 12%)    — new subsystems required
⭐⭐⭐⭐⭐  V.High ( 3 rings /  9%)    — multiple new systems + AI
```

### By Activation Type
| Type | Count | % |
|------|-------|---|
| Continuous Passive | 18 | 55% |
| Command Word Active | 7 | 21% |
| Charged/Expendable | 3 | 9% |
| Spell Storage | 3 | 9% |
| Multi-Power Complex | 2 | 6% |

---

## Priority Quadrant Analysis

### 🟢 Q1: High Impact / Low Complexity — **DO FIRST** (15 rings, 45%)
Rings of Protection, Resistance, Freedom of Movement, Evasion, Force Shield, Energy Resistance, Wizardry I–IV, Invisibility, Blinking, Counterspells, Sustenance, Climbing, Jumping, Swimming, Mind Shielding.
> **Est. effort: 1–2 weeks**

### 🟡 Q2: High Impact / High Complexity — **PLAN CAREFULLY** (5 rings, 15%)
Spell Storing (Minor/Standard/Major), Regeneration, Spell Turning.
> **Est. effort: 2–3 weeks**

### 🔵 Q3: Low Impact / Low Complexity — **FILL IN GAPS** (4 rings, 12%)
Feather Falling, Animal Friendship, Chameleon Power, Water Walking.
> **Est. effort: 3–5 days**

### 🔴 Q4: Low Impact / High Complexity — **DEFER** (9 rings, 27%)
Ram, Telekinesis, Friend Shield, Shooting Stars, X-Ray Vision, Three Wishes, Elemental Command ×4, Djinni Calling.
> **Est. effort: 3–4 weeks**

---

## Top 10 Priority Rings (by Impact × Simplicity Score)

| Rank | Ring | Score | Why |
|------|------|-------|-----|
| 1 | Protection +1–+5 | **25** | DeflectionBonus field exists; highest-impact ring |
| 2 | Resistance (Minor/Major/Greater) | **25** | Save bonus fields exist |
| 3 | Freedom of Movement | **20** | FoM spell already implemented |
| 4 | Evasion | **20** | Single flag: `HasEvasion = true` |
| 5 | Force Shield | **20** | ShieldBonus field exists |
| 6 | Energy Resistance (15 variants) | **16** | DamageResistances dict exists |
| 7 | Wizardry I–IV | **16** | High value for spellcasters |
| 8 | Invisibility | **16** | Existing spell + introduces command word pattern |
| 9 | Blinking | **16** | Existing spell, reuses command word pattern |
| 10 | Counterspells | **16** | Reactive trigger, unique gameplay |

---

## Required New Systems (7 Total)

| # | System | Effort | Unlocks | Priority |
|---|--------|--------|---------|----------|
| 1 | **Ring Equipment Framework** | 2–3 days | ALL 33 rings | 🔴 CRITICAL |
| 2 | **Command Word Activation** | 2 days | 9 Tier 2 rings | 🟠 HIGH |
| 3 | **Ring Charge Tracking** | 1 day | Ram, Three Wishes | 🟠 HIGH |
| 4 | **Spell Slot Doubling** | 3 days | Wizardry I–IV | 🟠 HIGH |
| 5 | **Spell Storage** | 4–5 days | Spell Storing, Counterspells | 🟡 MEDIUM |
| 6 | **Daily Use Tracking** | 2 days | Shooting Stars, Spell Turning | 🟡 MEDIUM |
| 7 | **Elemental Dominance** | 5–7 days | Elemental Command ×4 | 🟢 LOW |

**Total new system effort: ~19–23 days**

---

## 4-Sprint Implementation Roadmap

### Sprint 1 — Foundation + Quick Wins (Weeks 1–2)
| Deliverable | Details |
|-------------|---------|
| Ring Framework | `RingFactory.cs`, `RingDatabase.cs`, `RingDefinition.cs`, ItemData ring fields |
| RecalculateStats integration | Ring slots processed for passive bonuses |
| Command Word Activation UI | Reusable for all active rings |
| **15 rings functional** | Protection, Energy Resistance, Evasion, FoM, Force Shield, Invisibility, Blinking, Wizardry, Counterspells, + 6 flag/skill rings |

### Sprint 2 — Active & Moderate Rings (Weeks 3–4)
| Deliverable | Details |
|-------------|---------|
| Spell Storage subsystem | Reusable for other item types |
| Charge depletion system | Extends wand pattern |
| **8 more rings (23 total)** | Spell Storing Minor/Std, Regeneration, Feather Falling, Animal Friendship, Chameleon Power, Ram, Telekinesis |

### Sprint 3 — Complex Rings (Weeks 5–7)
| Deliverable | Details |
|-------------|---------|
| Spell reflection system | For Spell Turning |
| Multi-ability ring framework | For Shooting Stars |
| Paired item prototype | For Friend Shield |
| **6 more rings (29 total)** | Spell Storing Major, Spell Turning, Water Walking, Three Wishes, Shooting Stars, Friend Shield |

### Sprint 4 — Legendary Rings (Weeks 8–10)
| Deliverable | Details |
|-------------|---------|
| Elemental Command framework | 4 variants sharing one system |
| Summoning/Calling subsystem | For Djinni Calling |
| **4 more rings (33 total)** | X-Ray Vision, Elemental Command ×4, Djinni Calling |

---

## Quick Wins (Implement in First 2 Days)

These 5 rings can be implemented almost immediately once the Ring Framework exists, because every stat field they need is already in the codebase:

1. **Ring of Protection +1–+5** → set `DeflectionBonus` (already used by Shield of Faith)
2. **Ring of Evasion** → set `HasEvasion = true` (already used by Rogue/Monk)
3. **Ring of Energy Resistance** (15 variants) → add to `DamageResistances` dict
4. **Ring of Freedom of Movement** → apply existing FoM continuous buff
5. **Ring of Force Shield** → set `ShieldBonus = 2` when toggled active

---

## Dependency Tree (Simplified)

```
Ring Equipment Framework ← MUST BUILD FIRST
│
├─► Passive Rings (Tier 1) ← existing stat systems
│
├─► Command Word Activation System
│   ├─► Spell-Like Rings (Invisibility, Blink, Telekinesis, etc.)
│   └─► Charged Rings (Ram, Three Wishes)
│
├─► Spell Slot Doubling → Wizardry I–IV
│
├─► Spell Storage System → Spell Storing, Counterspells
│
├─► Daily Use Tracking → Shooting Stars, Spell Turning
│
└─► Summoning + Elemental Dominance → Djinni Calling, Elemental Command ×4
```

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `RecalculateStats()` ring integration breaks existing gear | Medium | High | Unit test all equipment before/after |
| Command activation UI delays | Medium | Medium | Stub with auto-activate; add UI later |
| Spell storage system scope creep | High | Medium | Implement Minor first, gate Major behind it |
| Elemental Command scope explosion | High | High | Implement as simplified stat rings first; add modes incrementally |
| Ring of Three Wishes balance issues | Low | High | Gate behind DM confirmation dialog |

---

## File Impact Summary

### 8 New Files
```
RingDefinition.cs              — Data structure for ring definitions
RingDatabase.cs                — Registry of all 33 ring types
RingFactory.cs                 — Ring item generation
RingEffectType.cs              — Enum for ring effect types
SpellStorageData.cs            — Spell storage for rings
RingActivationPanel.cs         — Ring activation UI
SpellStorageUI.cs              — Spell store/retrieve UI
ElementalDominanceSystem.cs    — Elemental command framework
```

### 10+ Modified Files
```
ItemData.cs                    — Ring-specific fields
Inventory.cs                   — RecalculateStats() ring processing
ItemDatabase.cs                — CloneItem() ring field copying
GameManager.cs                 — TryUseItem() ring branch
SceneBootstrap.cs              — RingDatabase initialization
CharacterStats.cs              — Ring-derived stats
ItemIDs.cs                     — Ring item ID constants
PreCombatInventoryUI.cs        — Ring tooltip updates
InventoryUI.cs                 — Ring display
SpellSlotCalculator.cs         — Wizardry doubling logic
```

---

*Executive summary consolidating: rings_implementation_plan.md, rings_by_complexity.md, ring_systems_required.md, rings_priority_matrix.md*

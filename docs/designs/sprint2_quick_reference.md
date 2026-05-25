# Sprint 2: Active Ability Rings — Quick Reference Card

> **9 rings | Command word activation | Daily/Weekly/Charge tracking**  
> **Rules:** Core D&D 3.5e only (PHB/DMG/MM)

---

## Ring Summary Table

| # | Ring | Price | Activation | Frequency | Key Effect | Action |
|---|------|-------|------------|-----------|------------|--------|
| 1 | **Invisibility** | 20,000 gp | Command | At will | *Invisibility* 30 rounds (CL 3) | Standard |
| 2 | **Blinking** | 27,000 gp | Command | At will | *Blink* 7 rounds (CL 7) | Standard |
| 3 | **Animal Friendship** | 10,800 gp | Command | 3/day | *Charm Animal* DC 11, ≤12 HD total | Standard |
| 4 | **Ram** | 8,600 gp | Command | 50 charges (regen 1d10/day) | 1–3 charges: 1d6/charge force + bull rush Str 25 | Standard |
| 5 | **Telekinesis** | 75,000 gp | Command | At will | *Telekinesis* — 3 modes (CL 9) | Standard |
| 6 | **X-Ray Vision** | 25,000 gp | Command | At will (Con penalty) | See through barriers 20 ft, 10 rounds | Standard |
| 7 | **Shooting Stars** | 50,000 gp | Command | Mixed (see below) | 5 abilities (CL 12) | Standard |
| 8 | **Spell Turning** | 98,280 gp | Automatic | 1d4+6 levels/rest | Reflects targeted spells | None |
| 9 | **Djinni Calling** | 125,000 gp | Command | 1/week | Summon Noble Djinni 1 hour | Full-round |

---

## Frequency Systems

### At Will (no tracking)
- Invisibility, Blinking, Telekinesis, X-Ray Vision*, Light, Dancing Lights

### Daily Uses (reset on rest)
| Ring | Ability | Uses/Day |
|------|---------|----------|
| Animal Friendship | Charm Animal | 3 |
| Shooting Stars | Ball Lightning | 1 |
| Shooting Stars | Faerie Fire | 2 |

### Weekly Uses (reset every 7th rest)
| Ring | Ability | Uses/Week |
|------|---------|-----------|
| Djinni Calling | Summon Djinni | 1 |
| Shooting Stars | Shooting Stars | 3 |

### Charge-Based (regenerates on rest)
| Ring | Max | Regen/Day | Cost/Use |
|------|-----|-----------|----------|
| Ram | 50 | 1d10 | 1–3 per activation |

### Pool-Based (refreshes on rest)
| Ring | Pool Size | Depletes By |
|------|-----------|-------------|
| Spell Turning | 1d4+6 levels | Reflecting spells (level-for-level) |

*\*X-Ray Vision: at will but 1 Con damage per use after first per rest*

---

## Shooting Stars — 5 Abilities Detail

| Ability | Freq | Restriction | Effect |
|---------|------|-------------|--------|
| Light | At will | None | As *Light* spell, 120 min |
| Dancing Lights | At will | Outdoors, night | As *Dancing Lights*, 10 rounds |
| Faerie Fire | 2/day | None | Outline targets, -20 Hide, reveal invisible |
| Ball Lightning | 1/day | None | 1–4 balls: 4d6/3d6/2d6/1d6+1 each, Ref DC 13 |
| Shooting Stars | 3/week | Outdoors, night | 1–3 stars: 12 fire dmg each, 5-ft burst, Ref DC 13 |

---

## Noble Djinni Stats (MM p.114)

| Stat | Value |
|------|-------|
| HP | 45 (7d8+14) |
| AC | 16 (touch 12, FF 13) |
| Attacks | 2 slams +10 (1d8+6) |
| Saves | Fort +7, Ref +8, Will +7 |
| Abilities | Str 18, Dex 17, Con 14, Int 14, Wis 15, Cha 15 |
| Speed | 20 ft, fly 60 ft (perfect) |
| Immunities | Acid |
| Special | If slain, ring becomes permanently inert |

---

## New Systems Needed

| System | Purpose | Integration Point |
|--------|---------|-------------------|
| **TryUseRing()** | Ring activation routing | `GameManager.cs:~5407` (item use switch) |
| **Daily use tracking** | 3/day, 2/day, 1/day limits | `ItemData` fields + rest handler reset |
| **Weekly use tracking** | 1/week, 3/week limits | `ItemData` fields + 7-rest counter |
| **Charge system** | 50 charges, regen 1d10/day | `ItemData` fields + rest handler regen |
| **RingAbilitySelectionPanel** | Multi-ability UI | New panel (clone StaffSpellSelectionPanel) |
| **Charm Animal** | New spell effect | Team change + status effect |
| **Force Bolt** | Ram ranged touch + bull rush | New combat effect |
| **Noble Djinni** | Creature definition | Add to NPCDatabase |
| **Equip-triggered effect** | Spell Turning on equip | Inventory equip handler |

---

## Key Existing Code to Reuse

| What | Where | Used By |
|------|-------|---------|
| `ApplyInvisibilityEffect()` | `CharacterController.cs:1708` | Ring of Invisibility |
| `HasActiveBlinkEffect` | `CharacterController.cs:1583` | Ring of Blinking |
| `ApplySpellTurningEffect()` | `GameManager_Spells_Phase2.cs:392` | Ring of Spell Turning |
| `SpawnSummonedCreature()` | `GameManager.SpellCasting.cs:228` | Ring of Djinni Calling |
| `CanUseItemManipulationAction()` | `GameManager.cs:4322` | All active rings |
| `TryUseStaff()` pattern | `GameManager.cs:5792` | Template for TryUseRing |
| Rest handler | `GameManager.cs:935` | Daily/weekly/charge resets |

---

## New Files to Create

| File | Lines | Purpose |
|------|-------|---------|
| `GameManager_Rings.cs` | ~500 | All 9 ring activation handlers (partial class) |
| `RingAbilitySelectionPanel.cs` | ~100 | UI panel for multi-ability rings |
| `RingAbilityOption.cs` | ~25 | Data class for panel options |

---

## Implementation Priority (if time-constrained)

1. ⚡ Core infrastructure (activation + tracking)
2. 🟢 Ring of Invisibility (simplest, validates system)
3. 🟢 Ring of Blinking (same pattern)
4. 🟡 Ring of the Ram (validates charges)
5. 🟡 Ring of Animal Friendship (validates daily uses)
6. 🟡 Ring of Spell Turning (validates equip-trigger)
7. 🟠 Ring of Telekinesis (moderate)
8. 🟠 Ring of Djinni Calling (validates weekly + summoning)
9. 🔴 Ring of X-Ray Vision (least combat impact)
10. 🔴 Ring of Shooting Stars (most complex, do last)

---

## Estimated Effort: 15–20 days total

| Phase | Days | Scope |
|-------|------|-------|
| Infrastructure | 1–4 | Activation, tracking, UI panel |
| Simple rings | 5–7 | Invisibility, Blinking, Telekinesis |
| Moderate rings | 8–12 | Animal Friendship, Ram, Spell Turning |
| Complex rings | 13–18 | X-Ray Vision, Shooting Stars, Djinni Calling |
| Polish & testing | 19–20 | Integration, edge cases, regression |

---

*Quick Reference v1.0 — Sprint 2 Active Ability Rings*  
*See `sprint2_detailed_implementation_plan.md` for full specifications*

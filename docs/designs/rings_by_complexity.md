# Magic Rings by Complexity Tier

> Quick reference — organized by implementation difficulty.  
> Source: DMG 3.5e pages 229–233 (core only).

---

## TIER 1: Foundation ⭐–⭐⭐ (14 rings, ~1–2 weeks)

Simple passive bonuses using existing systems. Implement first.

| # | Ring | Price (gp) | Type | Complexity | Existing System? |
|---|------|-----------|------|-----------|-----------------|
| 1 | Ring of Protection +1 | 2,000 | Passive | ⭐ | ✅ DeflectionBonus |
| 2 | Ring of Protection +2 | 8,000 | Passive | ⭐ | ✅ DeflectionBonus |
| 3 | Ring of Protection +3 | 18,000 | Passive | ⭐ | ✅ DeflectionBonus |
| 4 | Ring of Protection +4 | 32,000 | Passive | ⭐ | ✅ DeflectionBonus |
| 5 | Ring of Protection +5 | 50,000 | Passive | ⭐ | ✅ DeflectionBonus |
| 6 | Ring of Energy Resistance, Minor | 12,000 | Passive | ⭐⭐ | ✅ DamageResistances |
| 7 | Ring of Energy Resistance, Major | 28,000 | Passive | ⭐⭐ | ✅ DamageResistances |
| 8 | Ring of Energy Resistance, Greater | 44,000 | Passive | ⭐⭐ | ✅ DamageResistances |
| 9 | Ring of Evasion | 25,000 | Passive | ⭐⭐ | ✅ HasEvasion |
| 10 | Ring of Freedom of Movement | 40,000 | Passive | ⭐⭐ | ✅ FoM spell effect |
| 11 | Ring of Force Shield | 8,500 | Toggle | ⭐⭐ | ⚠️ Need toggle system |
| 12 | Ring of Mind Shielding | 8,000 | Passive | ⭐ | ⚠️ Need immunity flag |
| 13 | Ring of Feather Falling | 2,200 | Passive | ⭐ | ⚠️ Flag only (no fall system) |
| 14 | Ring of Sustenance | 2,500 | Passive | ⭐ | ⚠️ Flag only (no food system) |
| 15 | Ring of Swimming | 2,500 | Passive | ⭐ | ⚠️ Skill bonus (future) |
| 16 | Ring of Swimming, Improved | 10,000 | Passive | ⭐ | ⚠️ Skill bonus (future) |
| 17 | Ring of Climbing | 2,500 | Passive | ⭐ | ⚠️ Skill bonus (future) |
| 18 | Ring of Climbing, Improved | 10,000 | Passive | ⭐ | ⚠️ Skill bonus (future) |
| 19 | Ring of Jumping | 2,500 | Passive | ⭐ | ⚠️ Skill bonus (future) |
| 20 | Ring of Jumping, Improved | 10,000 | Passive | ⭐ | ⚠️ Skill bonus (future) |
| 21 | Ring of Water Walking | 15,000 | Passive | ⭐ | ⚠️ Flag only (no water system) |

**Quick Wins (implement in first 2 days):**
- Ring of Protection +1–+5 (deflection to AC — already have the stat field)
- Ring of Evasion (set HasEvasion = true)
- Ring of Energy Resistance (add to DamageResistances list)
- Ring of Freedom of Movement (apply FoM buff)

---

## TIER 2: Active Abilities ⭐⭐–⭐⭐⭐ (9 rings, ~1–2 weeks)

Rings with command word activation or daily-use abilities.

| # | Ring | Price (gp) | Type | Complexity | Spell Dependency |
|---|------|-----------|------|-----------|-----------------|
| 1 | Ring of Invisibility | 20,000 | Command word | ⭐⭐ | ✅ Invisibility |
| 2 | Ring of Blinking | 27,000 | Command word | ⭐⭐ | ✅ Blink |
| 3 | Ring of Telekinesis | 75,000 | Command word | ⭐⭐⭐ | ✅ Telekinesis |
| 4 | Ring of Regeneration | 90,000 | Continuous | ⭐⭐⭐ | ⚠️ Need fast healing |
| 5 | Ring of Ram | 8,600 | Charged (50) | ⭐⭐⭐ | ⚠️ Need bull rush |
| 6 | Ring of Animal Friendship | 10,800 | Command word | ⭐⭐ | ⚠️ Need Charm Animal |
| 7 | Ring of Chameleon Power | 12,700 | Passive + CW | ⭐⭐ | ⚠️ Need Disguise Self |
| 8 | Ring of X-Ray Vision | 25,000 | Command word | ⭐⭐ | ⚠️ Need vision system |
| 9 | Ring of Meld into Stone | 27,000 | Command word | ⭐⭐ | ⚠️ Need Meld into Stone |

**Requires:** Command Word Activation System (new)  
**New System Effort:** ~2 days for activation framework

---

## TIER 3: Complex Mechanics ⭐⭐⭐–⭐⭐⭐⭐ (6 rings, ~2–3 weeks)

Rings requiring new subsystems.

| # | Ring | Price (gp) | Type | Complexity | New System Needed |
|---|------|-----------|------|-----------|------------------|
| 1 | Ring of Wizardry I | 20,000 | Passive | ⭐⭐⭐ | Spell slot doubling |
| 2 | Ring of Wizardry II | 40,000 | Passive | ⭐⭐⭐ | Spell slot doubling |
| 3 | Ring of Wizardry III | 70,000 | Passive | ⭐⭐⭐ | Spell slot doubling |
| 4 | Ring of Wizardry IV | 100,000 | Passive | ⭐⭐⭐ | Spell slot doubling |
| 5 | Ring of Counterspells | 4,000 | Storage | ⭐⭐⭐ | Spell storage + auto-counter |
| 6 | Ring of Spell Storing, Minor | 18,000 | Storage | ⭐⭐⭐⭐ | Spell storage system |
| 7 | Ring of Spell Storing | 50,000 | Storage | ⭐⭐⭐⭐ | Spell storage system |
| 8 | Ring of Spell Storing, Major | 200,000 | Storage | ⭐⭐⭐⭐ | Spell storage system |
| 9 | Ring of Spell Turning | 98,280 | Daily use | ⭐⭐⭐⭐ | Daily reflection tracking |
| 10 | Ring of Shooting Stars | 50,000 | Daily use | ⭐⭐⭐⭐ | Multi-ability daily tracking |
| 11 | Ring of Friend Shield | 50,000 | Paired | ⭐⭐⭐ | Paired item + Shield Other |

**Largest Effort:** Spell Storage System (~5 days)  
**Highest Value:** Ring of Wizardry (directly impacts spellcaster power)

---

## TIER 4: Legendary Rings ⭐⭐⭐⭐⭐ (4 ring types, ~2–3 weeks)

Most powerful rings; each requires major new systems.

| # | Ring | Price (gp) | Type | Complexity | New System Needed |
|---|------|-----------|------|-----------|------------------|
| 1 | Ring of Three Wishes | 120,600 | Charged (3) | ⭐⭐⭐⭐⭐ | ✅ Wish exists! (2 days) |
| 2 | Ring of Djinni Calling | 125,000 | Summoning | ⭐⭐⭐⭐⭐ | Summoning system |
| 3 | Ring of Elemental Command (Air) | 200,000 | Multi-power | ⭐⭐⭐⭐⭐ | Elemental dominance |
| 4 | Ring of Elemental Command (Earth) | 200,000 | Multi-power | ⭐⭐⭐⭐⭐ | Elemental dominance |
| 5 | Ring of Elemental Command (Fire) | 200,000 | Multi-power | ⭐⭐⭐⭐⭐ | Elemental dominance |
| 6 | Ring of Elemental Command (Water) | 200,000 | Multi-power | ⭐⭐⭐⭐⭐ | Elemental dominance |

**Easiest Legendary:** Ring of Three Wishes (Wish system already exists)  
**Hardest:** Ring of Elemental Command (4 variants × 6 abilities each)

---

## Complexity Distribution

```
⭐ (Simple):        12 rings  (36%)  — static flags/bonuses
⭐⭐ (Low):          9 rings  (27%)  — simple abilities, existing spells
⭐⭐⭐ (Medium):      5 rings  (15%)  — daily tracking, spell slots
⭐⭐⭐⭐ (High):       4 rings  (12%)  — new subsystems required
⭐⭐⭐⭐⭐ (Very High): 3 rings  (9%)   — multiple subsystems, AI
                    ─────────────────
                    33 distinct types
```

## Type Distribution

```
Continuous Passive:  18 rings  (55%)
Command Word Active:  7 rings  (21%)
Charged/Expendable:   3 rings  (9%)
Spell Storage:        3 rings  (9%)
Multi-Power Complex:  2 rings  (6%)
```

---

## Recommended Implementation Order

1. **Ring of Protection** — highest impact, simplest to implement
2. **Ring of Energy Resistance** — uses existing resistance system
3. **Ring of Evasion** — single flag toggle
4. **Ring of Freedom of Movement** — existing spell effect
5. **Ring of Force Shield** — introduces toggle pattern
6. **Ring of Invisibility** — introduces command word pattern
7. **Ring of Blinking** — uses command word pattern
8. **Ring of Wizardry** — high value for spellcasters
9. **Ring of Three Wishes** — leverages existing Wish system
10. Everything else in priority order

---

*Quick reference generated 2026-05-24. Core DMG 3.5e only.*

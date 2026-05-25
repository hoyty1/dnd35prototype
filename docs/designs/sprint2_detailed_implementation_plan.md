# Sprint 2: Active Ability Rings — Detailed Implementation Plan

> **Project:** D&D 3.5e Unity Prototype  
> **Sprint:** 2 of 4 (Ring Implementation)  
> **Scope:** 9 Active Ability Rings with Command Word Activation  
> **Rules Authority:** Core D&D 3.5e ONLY (PHB, DMG, MM)  
> **Date:** May 2026  
> **Prerequisites:** Sprint 1 complete — 36 passive ring variants, RingFactory, RingDatabase, RingNames infrastructure

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Sprint 1 Foundation Recap](#2-sprint-1-foundation-recap)
3. [Ring Specifications — All 9 Rings](#3-ring-specifications--all-9-rings)
4. [New Systems Required](#4-new-systems-required)
5. [Implementation Phases](#5-implementation-phases)
6. [Codebase Integration Points](#6-codebase-integration-points)
7. [UI/UX Design](#7-uiux-design)
8. [Testing Plan](#8-testing-plan)
9. [Risk Assessment & Mitigation](#9-risk-assessment--mitigation)
10. [Appendix: File Change Summary](#10-appendix-file-change-summary)

---

## 1. Executive Summary

Sprint 2 adds **9 rings with active abilities** that require player interaction — command word activation, use-frequency tracking, charge systems, and special mechanics. These rings build on Sprint 1's passive ring infrastructure (RingFactory, RingDatabase, RingNames, ItemData ring fields) and leverage numerous existing systems (spell casting, summoning, invisibility, blink, spell turning).

### Key Deliverables
- **Command Word Activation System** — New `TryUseRing()` method following the `TryUseStaff()` pattern
- **Use Tracking Systems** — Daily uses (3/day), weekly uses (1/week, 3/week), charge-based (50 charges, regen 10/day)
- **Ring Ability Selection Panel** — For multi-ability rings (Ring of Shooting Stars)
- **5 rings leveraging existing spell effects** — Invisibility, Blink, Telekinesis, Spell Turning, summoning
- **4 rings requiring new effect implementations** — Animal Friendship (Charm Animal), Ram (force bolt + bull rush), X-Ray Vision, Shooting Stars (5 unique abilities)
- **1 new creature definition** — Noble Djinni for Ring of Djinni Calling

### Complexity Breakdown
| Tier | Rings | Est. Effort |
|------|-------|-------------|
| Simple (reuse existing spell) | Invisibility, Blink, Telekinesis | 2–3 days |
| Moderate (new effect + tracking) | Animal Friendship, Ram, Spell Turning | 4–5 days |
| Complex (multi-ability or new systems) | X-Ray Vision, Shooting Stars, Djinni Calling | 6–8 days |
| **Infrastructure** | Activation system, UI, tracking | **3–4 days** |
| **Total Estimate** | | **15–20 days** |

---

## 2. Sprint 1 Foundation Recap

### Existing Ring Infrastructure (commit `a77a6f7`)

| File | Purpose | Key Elements |
|------|---------|--------------|
| `RingNames.cs` | String constants for all ring names | `RING_OF_INVISIBILITY`, `RING_OF_BLINKING`, etc. |
| `RingFactory.cs` | Creates ring ItemData instances | `CreateRing(ringName)` with stat bonuses, metadata |
| `RingDatabase.cs` | Registry of all ring definitions | `Initialize()`, `Get(name)`, ring lookup |
| `ItemData.cs` | Ring fields on item model | `IsRing`, `RingSlot`, ring-specific properties |
| `CharacterStats.cs` | Stat modification from rings | Ring bonus application on equip/unequip |
| `Inventory.cs` | Ring slot management | Two ring slots, equip/unequip handling |
| `ItemDatabase.cs` | Master item registry | Ring entries integrated |

### What Sprint 1 Does NOT Have (Sprint 2 Must Add)
- No `TryUseRing()` method — rings cannot be "used" as an action
- No ring activation UI — no way to trigger command word abilities
- No daily/weekly/charge tracking on rings
- No charge regeneration on rest
- No multi-ability ring selection panel
- All 9 Sprint 2 rings exist in RingDatabase as **passive-only stubs** (name/price/slot but no active effects)

---

## 3. Ring Specifications — All 9 Rings

### 3.1 Ring of Invisibility
| Property | Value |
|----------|-------|
| **Name Constant** | `RingNames.RING_OF_INVISIBILITY` |
| **Market Price** | 20,000 gp |
| **Activation** | Command word (standard action) |
| **Frequency** | At will (unlimited) |
| **Effect** | Casts *Invisibility* (PHB p.245) on the wearer |
| **Duration** | Per spell: 1 min/level (10 rounds at CL 3, but ring CL = 3 per DMG) |
| **Caster Level** | 3rd |
| **Special Rules** | Breaks on attack or offensive action per standard Invisibility rules |
| **Aura** | Faint illusion |

**Implementation Notes:**
- Calls existing `CharacterController.ApplyInvisibilityEffect()` (found at `CharacterController.cs:1708`)
- `HasActiveInvisibilityEffect` property already tracks active state (`CharacterController.cs:1535`)
- Duration: 30 rounds (CL 3 × 10 rounds/level... but Invisibility is 1 min/level = 10 rounds per CL)
- Actually per DMG, ring CL = 3, so duration = 3 minutes = 30 rounds
- No new systems needed beyond the activation framework

**Pseudocode:**
```
TryUseRing_Invisibility(caster, ring):
    if not CanUseItemManipulationAction(caster, ActionType.Standard):
        return false
    durationRounds = 30  // CL 3 × 10 rounds/level
    caster.CharacterController.ApplyInvisibilityEffect(durationRounds, caster)
    ConsumeItemManipulationAction(caster)
    LogMessage("{caster.Name} activates Ring of Invisibility")
    return true
```

---

### 3.2 Ring of Blinking
| Property | Value |
|----------|-------|
| **Name Constant** | `RingNames.RING_OF_BLINKING` |
| **Market Price** | 27,000 gp |
| **Activation** | Command word (standard action) |
| **Frequency** | At will (unlimited) |
| **Effect** | Casts *Blink* (PHB p.206) on the wearer |
| **Duration** | Per spell: 1 round/level = 7 rounds (CL 7 per DMG) |
| **Caster Level** | 7th |
| **Special Rules** | 50% miss chance on incoming attacks; 20% miss chance on wearer's attacks; can move through solid objects |
| **Aura** | Moderate transmutation |

**Implementation Notes:**
- `HasActiveBlinkEffect` property exists (`CharacterController.cs:1583`)
- Full blink miss chance system is already implemented in combat resolution
- Duration: 7 rounds (CL 7)
- Straightforward activation — same pattern as Invisibility

**Pseudocode:**
```
TryUseRing_Blink(caster, ring):
    if not CanUseItemManipulationAction(caster, ActionType.Standard):
        return false
    durationRounds = 7  // CL 7 × 1 round/level
    caster.CharacterController.ApplyBlinkEffect(durationRounds, caster)
    ConsumeItemManipulationAction(caster)
    LogMessage("{caster.Name} activates Ring of Blinking")
    return true
```

---

### 3.3 Ring of Animal Friendship
| Property | Value |
|----------|-------|
| **Name Constant** | `RingNames.RING_OF_ANIMAL_FRIENDSHIP` |
| **Market Price** | 10,800 gp |
| **Activation** | Command word (standard action) |
| **Frequency** | 3/day |
| **Effect** | Casts *Charm Animal* (PHB p.208) on target animal |
| **Duration** | 1 hour/level = 1 hour (CL 1... but DMG says functions as charm animal) |
| **Caster Level** | Not specified in DMG entry — use minimum CL 1 for druid spell |
| **Save** | Will DC 11 negates |
| **Target** | One animal within 25 ft (close range at CL 1) |
| **Special Rules** | Only works on Animal type creatures; can have up to 12 HD of charmed animals simultaneously (as per the ring description in DMG) |
| **Aura** | Faint enchantment |

**Implementation Notes:**
- **Charm Animal spell is NOT currently implemented** — only exists as a stub name in StaffDatabase.cs:538
- Needs new spell effect: simplified charm that changes creature disposition/team
- Must validate target is Animal type (check creature type field)
- **Daily use tracking** — first ring requiring the daily uses system
- HD cap: Sum of charmed animal HD ≤ 12 (ring-specific rule from DMG p.232)

**New Systems Required:**
1. Charm Animal effect (new spell implementation)
2. Daily use counter on ring items
3. Target validation (animal type only)
4. Charmed animal HD tracking per ring

**Pseudocode:**
```
TryUseRing_AnimalFriendship(caster, ring):
    if not CanUseItemManipulationAction(caster, ActionType.Standard):
        return false
    if ring.DailyUsesRemaining <= 0:
        LogMessage("Ring of Animal Friendship has no uses remaining today")
        return false
    // Enter targeting mode
    StartTargetingMode(caster, TargetType.SingleEnemy, range: 25ft, callback: (target) =>
        if target.CreatureType != CreatureType.Animal:
            LogMessage("Target must be an animal")
            return
        if GetCharmedAnimalHDTotal(caster, ring) + target.HitDice > 12:
            LogMessage("Cannot charm: would exceed 12 HD limit")
            return
        willSaveDC = 11
        if target.RollWillSave() >= willSaveDC:
            LogMessage("{target.Name} resists the charm")
        else:
            ApplyCharmAnimalEffect(target, caster, durationRounds: 600) // 1 hour
            LogMessage("{target.Name} is charmed by {caster.Name}")
        ring.DailyUsesRemaining -= 1
        ConsumeItemManipulationAction(caster)
    )
    return true
```

---

### 3.4 Ring of the Ram
| Property | Value |
|----------|-------|
| **Name Constant** | `RingNames.RING_OF_THE_RAM` |
| **Market Price** | 8,600 gp |
| **Activation** | Command word (standard action) |
| **Frequency** | Charge-based: 50 charges max, expend 1–3 per use, regenerates 10 charges/dawn (see DMG errata — not "1d10" but a flat 10 from the 2003 errata context; however DMG p.233 says "regain 1d10 charges per day") |
| **Effect** | Force bolt deals damage and/or bull rush |
| **Damage** | 1d6 per charge expended (1 charge = 1d6, 2 charges = 2d6, 3 charges = 3d6) |
| **Range** | Close (25 ft + 5 ft/2 levels = 50 ft at CL 9) |
| **Bull Rush** | Strength 25 (+7 modifier) check; +1 per charge expended |
| **Caster Level** | 9th |
| **Attack Roll** | Ranged touch attack using wearer's stats |
| **Special** | Can target objects; deals double damage to objects |
| **Aura** | Moderate evocation |

**Implementation Notes:**
- **New charge system** with regeneration — first ring using charges
- Charges tracked via `ItemData.CurrentCharges` / `ItemData.MaxCharges` (may need to add these fields)
- Regeneration: Add charge restoration logic to rest handler (`GameManager.cs:935-980`)
- Requires ranged touch attack roll (existing combat system supports touch attacks)
- Bull rush uses existing bull rush mechanics if available, or simplified version
- Player chooses 1–3 charges to expend → needs a small UI for charge selection

**New Systems Required:**
1. Charge tracking on ItemData (CurrentCharges, MaxCharges)
2. Charge regeneration in rest handler
3. Charge selection UI (1/2/3 charges)
4. Ranged touch attack from ring
5. Force bolt damage + bull rush effect

**Pseudocode:**
```
TryUseRing_Ram(caster, ring):
    if not CanUseItemManipulationAction(caster, ActionType.Standard):
        return false
    if ring.CurrentCharges <= 0:
        LogMessage("Ring of the Ram has no charges remaining")
        return false
    // Open charge selection panel (1, 2, or 3)
    maxCharges = Min(3, ring.CurrentCharges)
    OpenChargeSelectionPanel(maxCharges, callback: (chargesSpent) =>
        StartTargetingMode(caster, TargetType.SingleEnemy, range: 50ft, callback: (target) =>
            // Ranged touch attack
            attackRoll = RollRangedTouchAttack(caster, target)
            if attackRoll.IsHit:
                // Damage
                damage = Roll(chargesSpent, d6)
                if target.IsObject:
                    damage *= 2
                target.TakeDamage(damage, DamageType.Force)
                // Bull rush
                bullRushStr = 25 + chargesSpent  // +1 per charge
                bullRushCheck = Roll(1, d20) + GetStrMod(bullRushStr)
                targetCheck = Roll(1, d20) + target.GetStrMod() + target.SizeModifier
                if bullRushCheck > targetCheck:
                    PushTarget(target, distance: 5ft * (bullRushCheck - targetCheck))
            ring.CurrentCharges -= chargesSpent
            ConsumeItemManipulationAction(caster)
        )
    )
    return true

// In rest handler:
OnRest():
    foreach ring in equippedRings where ring.IsChargeRing:
        ring.CurrentCharges = Min(ring.MaxCharges, ring.CurrentCharges + Roll(1, d10))
```

---

### 3.5 Ring of Telekinesis
| Property | Value |
|----------|-------|
| **Name Constant** | `RingNames.RING_OF_TELEKINESIS` |
| **Market Price** | 75,000 gp |
| **Activation** | Command word (standard action) |
| **Frequency** | At will (unlimited) |
| **Effect** | Casts *Telekinesis* (PHB p.292) — all three modes available |
| **Modes** | **Sustained Force** (move 375 lbs, concentration), **Combat Maneuver** (bull rush/disarm/grapple/trip at CL vs target), **Violent Thrust** (hurl objects/creatures, 375 lb limit, 15d6 max damage) |
| **Caster Level** | 9th |
| **Duration** | Concentration (up to 9 rounds) or instantaneous (violent thrust) |
| **Range** | Long (400 ft + 40 ft/level = 760 ft at CL 9) |
| **Aura** | Moderate transmutation |

**Implementation Notes:**
- Telekinesis spell constant exists: `SpellNames.TELEKINESIS`
- The full Telekinesis spell implementation needs to be verified for all three modes
- For the prototype, may simplify to **Violent Thrust** mode only (most combat-relevant)
- Alternatively, open a mode selection panel (Sustained Force / Combat Maneuver / Violent Thrust)
- At will, so no use tracking needed — just activation system

**Prototype Simplification Recommendation:**
- Implement **Violent Thrust** as primary mode: ranged attack, deals damage based on object weight
- **Combat Maneuver** as secondary: opposed CL check vs target for bull rush/trip
- **Sustained Force** as flavor only (log message, no mechanical implementation)
- This follows the same simplification approach used elsewhere in the prototype

**Pseudocode:**
```
TryUseRing_Telekinesis(caster, ring):
    if not CanUseItemManipulationAction(caster, ActionType.Standard):
        return false
    // Open mode selection: Violent Thrust, Combat Maneuver, Sustained Force
    OpenTelekinesisPanel(callback: (mode) =>
        switch mode:
            case ViolentThrust:
                StartTargetingMode(caster, range: 760ft, callback: (target) =>
                    // Hurl objects at target
                    attackRoll = RollRangedAttack(caster, target)
                    if attackRoll.IsHit:
                        damage = Roll(min(15, objectCount), d6)
                        target.TakeDamage(damage, DamageType.Bludgeoning)
                    ConsumeItemManipulationAction(caster)
                )
            case CombatManeuver:
                StartTargetingMode(caster, range: 760ft, callback: (target) =>
                    // Opposed check: CL 9 + d20 vs target CMB defense
                    ApplyTelekineticManeuver(caster, target, CL: 9)
                    ConsumeItemManipulationAction(caster)
                )
            case SustainedForce:
                LogMessage("{caster.Name} telekinetically manipulates objects")
                ConsumeItemManipulationAction(caster)
    )
    return true
```

---

### 3.6 Ring of X-Ray Vision
| Property | Value |
|----------|-------|
| **Name Constant** | `RingNames.RING_OF_XRAY_VISION` |
| **Market Price** | 25,000 gp |
| **Activation** | Command word (standard action) |
| **Frequency** | At will, but see fatigue rules |
| **Effect** | See through solid matter up to 20 ft |
| **Penetration** | 20 ft stone/wood/dirt, 10 ft iron, 1 ft gold/lead/platinum block it entirely |
| **Scanning** | 10 ft × 10 ft area per round |
| **Duration** | 1 minute per use (10 rounds) |
| **Fatigue** | Each use after the first in a single hour causes Con damage (1d4 Con drain) — per DMG errata the penalty is not drain but 1 point of Con damage per use after first |
| **Caster Level** | Not specified — treat as CL 5 |
| **Aura** | Moderate divination |

**Implementation Notes:**
- **No existing vision-through-walls system** — this is entirely new
- For the prototype, X-Ray Vision is primarily a **narrative/exploration tool** with limited combat application
- **Recommended prototype implementation:**
  - Apply a status effect "X-Ray Vision" for 10 rounds
  - Mechanically: reveal hidden/invisible creatures within 20 ft (grants See Invisibility-like effect)
  - Reveal secret doors / traps within 20 ft (if such system exists)
  - The "see through walls" aspect is hard to represent in a tactical combat prototype
  - Con damage on repeated use within same hour provides a meaningful cost
- Track "uses this hour" — simpler approach: track uses since last rest, apply Con damage after first use

**Prototype Simplification:**
- **Combat mode:** Grants See Invisibility + reveals hidden creatures within 20 ft for 10 rounds
- **Exploration mode:** Log message indicating what is found (script-driven, encounter-specific)
- **Fatigue:** Apply 1 Con damage on second+ use per rest period (simplified from "per hour")

**Pseudocode:**
```
TryUseRing_XRayVision(caster, ring):
    if not CanUseItemManipulationAction(caster, ActionType.Standard):
        return false
    // Con damage on repeated use
    ring.XRayUsesThisRest += 1
    if ring.XRayUsesThisRest > 1:
        conDamage = 1  // simplified from 1d4
        caster.ApplyAbilityDamage(AbilityScore.Constitution, conDamage)
        LogMessage("{caster.Name} strains from X-Ray vision use (1 Con damage)")
    // Apply effect
    ApplyStatusEffect(caster, "XRayVision", durationRounds: 10, effects:
        - SeeInvisibility: true
        - RevealHidden: true
        - Range: 20ft
    )
    ConsumeItemManipulationAction(caster)
    LogMessage("{caster.Name} activates X-Ray vision, seeing through barriers")
    return true
```

---

### 3.7 Ring of Shooting Stars
| Property | Value |
|----------|-------|
| **Name Constant** | `RingNames.RING_OF_SHOOTING_STARS` |
| **Market Price** | 50,000 gp |
| **Activation** | Command word (varies per ability) |
| **Caster Level** | 12th |
| **Aura** | Strong evocation |

**Five Abilities:**

#### 3.7a Dancing Lights (at will, outdoors at night only)
| Property | Value |
|----------|-------|
| **Frequency** | At will |
| **Effect** | As *Dancing Lights* spell (PHB p.216) |
| **Duration** | 1 minute (10 rounds) |
| **Restriction** | Outdoors at night only |
| **Action** | Standard action |

#### 3.7b Light (at will)
| Property | Value |
|----------|-------|
| **Frequency** | At will |
| **Effect** | As *Light* spell (PHB p.248) |
| **Duration** | 120 minutes (CL 12) |
| **Action** | Standard action |

#### 3.7c Ball Lightning (1/day, special)
| Property | Value |
|----------|-------|
| **Frequency** | 1/day |
| **Effect** | Create 1–4 balls of lightning; each can be directed (move action) to strike a target |
| **Damage** | Each ball deals electricity damage on contact (per DMG: varies by number created — 1 ball = 4d6, 2 balls = 3d6 each, 3 balls = 2d6 each, 4 balls = 1d6+1 each) |
| **Range** | 120 ft |
| **Duration** | Balls persist for 4 rounds or until discharged |
| **Save** | Reflex DC 13 half |
| **Action** | Standard action to create, move action to direct |

#### 3.7d Shooting Stars (3/week, outdoors at night only)
| Property | Value |
|----------|-------|
| **Frequency** | 3/week |
| **Effect** | Fire 1–3 shooting stars (flame strikes from above) |
| **Damage** | Each star: 12d6 fire in 5-ft radius, Reflex DC 13 half |
| **Range** | 70 ft |
| **Restriction** | Outdoors at night only |
| **Action** | Standard action |

**Note:** Per DMG p.233, the actual shooting stars ability deals less damage. Cross-referencing: each star creates a 5-ft-radius burst dealing a total of 12 points of damage (not 12d6). Let me re-verify: DMG p.233 states "Shooting Stars: This ability functions outdoors at night... The wearer can fire up to three shooting stars... Each shooting star deals 12 points of fire damage." So **12 flat fire damage per star, Reflex DC 13 half, 5-ft radius**.

**Corrected:**
| **Damage** | Each star: 12 fire damage in 5-ft radius, Reflex DC 13 half |

#### 3.7e Faerie Fire (2/day)
| Property | Value |
|----------|-------|
| **Frequency** | 2/day |
| **Effect** | As *Faerie Fire* spell (PHB p.229) |
| **Duration** | 12 minutes (CL 12) |
| **Area** | 5-ft-radius burst per creature/object targeted |
| **Effect** | Outlined creatures take -20 to Hide, visible if invisible |
| **Action** | Standard action |

**Implementation Notes:**
- **Most complex ring** — five abilities with mixed frequency tracking
- Requires **Ring Ability Selection Panel** (like StaffSpellSelectionPanel) — shows 5 abilities with remaining uses
- `DANCING_LIGHTS` spell constant exists in SpellNames
- `FAERIE_FIRE` needs to be checked / may need implementation
- Ball Lightning is entirely new
- Shooting Stars is entirely new
- Daily tracking: Ball Lightning (1/day), Faerie Fire (2/day)
- Weekly tracking: Shooting Stars (3/week)
- At-will: Dancing Lights, Light

**Use Tracking Structure:**
```
ring.DailyUses["BallLightning"] = { Used: 0, Max: 1 }
ring.DailyUses["FaerieFire"] = { Used: 0, Max: 2 }
ring.WeeklyUses["ShootingStars"] = { Used: 0, Max: 3 }
// Dancing Lights and Light: unlimited, no tracking
```

---

### 3.8 Ring of Spell Turning
| Property | Value |
|----------|-------|
| **Name Constant** | `RingNames.RING_OF_SPELL_TURNING` |
| **Market Price** | 98,280 gp |
| **Activation** | **Automatic** (no action required — always active while worn) |
| **Frequency** | Pool-based: 1d4+6 spell levels absorbed, then inert until recharged |
| **Effect** | As *Spell Turning* spell (PHB p.283) — reflects targeted spells back at caster |
| **Recharge** | New pool when re-equipped or on specific trigger (see notes) |
| **Caster Level** | 13th |
| **Special Rules** | Only reflects spells that specifically target the wearer (not area effects); partial reflection if remaining levels < spell level; both casters affected by the same spell if partial |
| **Aura** | Strong abjuration |

**Implementation Notes:**
- **Spell Turning is already fully implemented** at `GameManager_Spells_Phase2.cs:392-426`
- Uses `StatusEffect.CustomTag = $"SpellTurning:{turningLevels}"` to track remaining pool
- Existing implementation: self-only, rolls 1d4+6, stores remaining levels
- **Ring difference from spell:** The ring applies this effect automatically when equipped, not as a cast spell
- Ring should apply the Spell Turning status effect with 1d4+6 levels on equip
- When pool is depleted, ring becomes inert (no more reflections)
- **Recharge question:** DMG p.233 does not specify recharge. Standard interpretation: the ring provides one use of Spell Turning (1d4+6 levels) and then becomes non-functional until the ring is removed and re-equipped (which re-rolls the pool). Some DMs rule it refreshes daily. **Prototype decision: refresh on rest (long rest = new 1d4+6 pool).**

**Pseudocode:**
```
OnEquipRing_SpellTurning(wearer, ring):
    turningLevels = Roll(1, d4) + 6  // 7-10 levels
    ApplyStatusEffect(wearer, "SpellTurning",
        duration: Permanent,  // until levels depleted
        customTag: $"SpellTurning:{turningLevels}"
    )
    LogMessage("{wearer.Name} dons Ring of Spell Turning ({turningLevels} levels)")

OnUnequipRing_SpellTurning(wearer, ring):
    RemoveStatusEffect(wearer, "SpellTurning")

OnRest_SpellTurning(wearer, ring):
    RemoveStatusEffect(wearer, "SpellTurning")
    newLevels = Roll(1, d4) + 6
    ApplyStatusEffect(wearer, "SpellTurning",
        duration: Permanent,
        customTag: $"SpellTurning:{newLevels}"
    )
    LogMessage("Ring of Spell Turning recharged ({newLevels} levels)")
```

---

### 3.9 Ring of Djinni Calling
| Property | Value |
|----------|-------|
| **Name Constant** | `RingNames.RING_OF_DJINNI_CALLING` |
| **Market Price** | 125,000 gp |
| **Activation** | Command word (full-round action) |
| **Frequency** | 1/week (7-day cooldown, not "per calendar week") |
| **Effect** | Summons a specific Noble Djinni to serve for 1 hour |
| **Duration** | 1 hour (600 rounds), or until dismissed/slain |
| **Caster Level** | 17th |
| **Special Rules** | Always summons the SAME djinni (it is bound to the ring); if the djinni is slain, ring becomes non-magical for 1 year; djinni serves willingly but must not be mistreated (DM discretion) |
| **Aura** | Strong conjuration |

**Noble Djinni Stats (MM p.114-115):**
| Stat | Value |
|------|-------|
| **Type** | Outsider (Air, Extraplanar) |
| **Size** | Large |
| **HD** | 7d8+14 (45 HP) |
| **AC** | 16 (-1 size, +3 Dex, +4 natural), touch 12, flat-footed 13 |
| **Speed** | 20 ft, fly 60 ft (perfect) |
| **BAB/Grapple** | +7/+15 |
| **Attacks** | Slam +10 melee (1d8+6) |
| **Full Attack** | 2 slams +10 melee (1d8+6) |
| **Space/Reach** | 10 ft / 10 ft |
| **Special Attacks** | Air mastery, whirlwind |
| **Special Qualities** | Darkvision 60 ft, immunity to acid, plane shift, telepathy 100 ft |
| **Saves** | Fort +7, Ref +8, Will +7 |
| **Abilities** | Str 18, Dex 17, Con 14, Int 14, Wis 15, Cha 15 |
| **Spell-Like Abilities** | At will: *invisibility* (self only), *plane shift*; 1/day: *create food and water*, *create wine* (as *create water* but wine), *major creation* (created vegetable matter permanent), *persistent image* (DC 17), *wind walk*; 1/day: *gaseous form* (up to 1 hour) |

**Implementation Notes:**
- **Summoning system exists** at `GameManager.SpellCasting.cs:228-310` (`SpawnSummonedCreature()`)
- `ActiveSummonInstance` class handles duration, controller, team assignment (`GameManager.cs:445-480`)
- **Noble Djinni is NOT in NPCDatabase** — must create a new `NPCDefinition`
- Weekly use tracking needed (new system)
- Special: if djinni is slain, ring becomes non-magical (set a flag, check on activation)
- Full-round action (not standard) — requires check via `CanUseItemManipulationAction` with FullRound type

**Pseudocode:**
```
TryUseRing_DjinniCalling(caster, ring):
    if not CanUseItemManipulationAction(caster, ActionType.FullRound):
        return false
    if ring.DjinniSlain:
        LogMessage("The Ring of Djinni Calling is inert — its djinni was slain")
        return false
    if ring.WeeklyUsesRemaining <= 0:
        LogMessage("Cannot summon the djinni — already called this week")
        return false
    // Spawn Djinni
    djinniDef = NPCDatabase.Get("Noble_Djinni")
    spawnPos = GetAdjacentEmptyTile(caster.Position, size: Large)
    djinni = SpawnSummonedCreature(djinniDef, spawnPos, caster,
        durationRounds: 600,  // 1 hour
        isAlly: true
    )
    djinni.ActiveSummonInstance.SourceDescription = "Ring of Djinni Calling"
    ring.WeeklyUsesRemaining -= 1
    ConsumeItemManipulationAction(caster)
    LogMessage("{caster.Name} calls forth a Noble Djinni from the ring!")
    return true

// When djinni is killed:
OnSummonKilled(summon):
    if summon.SourceDescription == "Ring of Djinni Calling":
        sourceRing.DjinniSlain = true
        LogMessage("The djinni is destroyed! The ring becomes inert.")
```

---

## 4. New Systems Required

### 4.1 Ring Activation System (Core)

**Purpose:** Allow players to "use" equipped rings as an action, similar to using staffs.

**Integration Point:** `GameManager.cs` line ~5407 — the item use switch statement.

**Current code flow:**
```csharp
// GameManager.cs:5407-5434 (approximate)
if (item.IsScroll) { TryUseScroll(...); }
else if (item.IsWand) { TryUseWand(...); }
else if (item.IsStaff) { TryUseStaff(...); }
else { /* potion */ }
```

**Required change — add ring branch:**
```csharp
if (item.IsScroll) { TryUseScroll(...); }
else if (item.IsWand) { TryUseWand(...); }
else if (item.IsStaff) { TryUseStaff(...); }
else if (item.IsRing && item.HasActiveAbility) { TryUseRing(...); }
else { /* potion */ }
```

**New `TryUseRing()` method** (modeled on `TryUseStaff()` at `GameManager.cs:5792-5861`):
```csharp
public bool TryUseRing(CharacterSheet caster, ItemData ring)
{
    // 1. Validate ring has active ability
    if (!ring.HasActiveAbility) return false;

    // 2. Check action economy
    ActionType requiredAction = GetRingActionType(ring);
    if (!CanUseItemManipulationAction(caster, requiredAction)) return false;

    // 3. Route to specific ring handler
    switch (ring.ItemName)
    {
        case RingNames.RING_OF_INVISIBILITY:
            return ActivateRingOfInvisibility(caster, ring);
        case RingNames.RING_OF_BLINKING:
            return ActivateRingOfBlinking(caster, ring);
        case RingNames.RING_OF_ANIMAL_FRIENDSHIP:
            return ActivateRingOfAnimalFriendship(caster, ring);
        case RingNames.RING_OF_THE_RAM:
            return ActivateRingOfTheRam(caster, ring);
        case RingNames.RING_OF_TELEKINESIS:
            return ActivateRingOfTelekinesis(caster, ring);
        case RingNames.RING_OF_XRAY_VISION:
            return ActivateRingOfXRayVision(caster, ring);
        case RingNames.RING_OF_SHOOTING_STARS:
            return ActivateRingOfShootingStars(caster, ring);
        case RingNames.RING_OF_DJINNI_CALLING:
            return ActivateRingOfDjinniCalling(caster, ring);
        default:
            return false;
    }
}
```

**Note:** Ring of Spell Turning is NOT in this switch — it's automatic (activated on equip, not by command).

---

### 4.2 Use Frequency Tracking

Three tracking systems needed, all stored on `ItemData`:

#### 4.2a Daily Uses
```csharp
// New fields on ItemData
public Dictionary<string, int> RingDailyUsesRemaining;  // key = ability name
public Dictionary<string, int> RingDailyUsesMax;         // key = ability name

// Example initialization for Ring of Animal Friendship:
RingDailyUsesRemaining["CharmAnimal"] = 3;
RingDailyUsesMax["CharmAnimal"] = 3;

// Example for Ring of Shooting Stars:
RingDailyUsesRemaining["BallLightning"] = 1;
RingDailyUsesMax["BallLightning"] = 1;
RingDailyUsesRemaining["FaerieFire"] = 2;
RingDailyUsesMax["FaerieFire"] = 2;
```

**Reset in rest handler** (`GameManager.cs:935-980`):
```csharp
// Add to existing rest handler:
foreach (var ring in GetAllEquippedRings(character))
{
    if (ring.RingDailyUsesRemaining != null)
    {
        foreach (var key in ring.RingDailyUsesMax.Keys)
            ring.RingDailyUsesRemaining[key] = ring.RingDailyUsesMax[key];
    }
}
```

#### 4.2b Weekly Uses
```csharp
// New fields on ItemData
public Dictionary<string, int> RingWeeklyUsesRemaining;
public Dictionary<string, int> RingWeeklyUsesMax;

// Ring of Djinni Calling:
RingWeeklyUsesRemaining["SummonDjinni"] = 1;
RingWeeklyUsesMax["SummonDjinni"] = 1;

// Ring of Shooting Stars:
RingWeeklyUsesRemaining["ShootingStars"] = 3;
RingWeeklyUsesMax["ShootingStars"] = 3;
```

**Weekly reset approach:**
- **Option A (Simple):** Reset weekly uses every 7th rest. Track `RestsSinceWeeklyReset` counter.
- **Option B (Calendar):** Track actual day number, reset when 7 days have passed.
- **Recommended:** Option A — simpler, fits the prototype's rest-based time model. Add `int RestsSinceWeeklyReset` to party/game state. Increment on rest. When it reaches 7, reset all weekly uses and set counter to 0.

#### 4.2c Charge System
```csharp
// New fields on ItemData (may already partially exist for wands)
public int CurrentCharges;
public int MaxCharges;
public int ChargesRegainedPerDay;  // For Ring of Ram: 1d10 (store as -10 for "roll 1d10")
public bool ChargesRegenerate;

// Ring of the Ram:
CurrentCharges = 50;
MaxCharges = 50;
ChargesRegainedPerDay = -10;  // Negative = roll 1d10
ChargesRegenerate = true;
```

**Charge regeneration in rest handler:**
```csharp
foreach (var ring in GetAllEquippedRings(character))
{
    if (ring.ChargesRegenerate && ring.CurrentCharges < ring.MaxCharges)
    {
        int regained = (ring.ChargesRegainedPerDay < 0)
            ? Roll(1, Math.Abs(ring.ChargesRegainedPerDay))  // 1d10
            : ring.ChargesRegainedPerDay;
        ring.CurrentCharges = Math.Min(ring.MaxCharges, ring.CurrentCharges + regained);
    }
}
```

---

### 4.3 Charm Animal Effect (New Spell)

**Required for:** Ring of Animal Friendship

**Implementation approach:**
1. Add `SpellNames.CHARM_ANIMAL` constant (currently only exists as stub reference)
2. Create simplified Charm effect:
   - Apply "Charmed" status effect to target
   - Change target's team alignment to match caster (target stops attacking caster's allies)
   - Track charmed targets per ring (for 12 HD cap)
   - Duration: 600 rounds (1 hour at CL 1)
3. **Targeting validation:** Target must have `CreatureType.Animal`
4. **Will save:** DC 11 (fixed per ring description)
5. **Break conditions:** If caster or allies attack the charmed animal, charm breaks

**Creature Type Check:**
- Verify if `NPCDefinition` or creature data has a `CreatureType` field
- If not, add `CreatureType` enum: `Animal`, `MagicalBeast`, `Humanoid`, `Undead`, etc.
- For the prototype, this may be simplified to a tag or string field

---

### 4.4 Ring Ability Selection Panel (UI)

**Required for:** Ring of Shooting Stars (5 abilities), Ring of Telekinesis (3 modes), Ring of the Ram (charge selection)

**Model:** Follows `StaffSpellSelectionPanel` pattern (used for staff spell selection)

**Panel types needed:**

1. **RingAbilitySelectionPanel** — Lists available abilities with remaining uses
   - Used by: Ring of Shooting Stars
   - Shows: Ability name, description, uses remaining (e.g., "Ball Lightning (1/1 today)")
   - Grays out: Abilities with 0 uses remaining
   - Callback: Selected ability triggers the corresponding effect

2. **TelekinesisModPanel** — Lists three Telekinesis modes
   - Used by: Ring of Telekinesis
   - Shows: Violent Thrust, Combat Maneuver, Sustained Force

3. **ChargeSelectionPanel** — Simple 1/2/3 selector
   - Used by: Ring of the Ram
   - Shows: Available charges, lets player pick 1–3

All three can potentially be a **single generic panel** with configurable options:
```csharp
public class RingAbilitySelectionPanel : MonoBehaviour
{
    public void Show(List<RingAbilityOption> options, Action<RingAbilityOption> onSelect)
    {
        // Populate UI buttons from options
        // Each button: name, description, uses remaining, enabled/disabled
        // On click: invoke onSelect callback with chosen option
    }
}

public class RingAbilityOption
{
    public string Name;
    public string Description;
    public int UsesRemaining;  // -1 = unlimited
    public int UsesMax;        // -1 = unlimited
    public bool IsEnabled;
    public string AbilityKey;  // Used in callback to identify which ability
}
```

---

### 4.5 Noble Djinni Creature Definition

**Required for:** Ring of Djinni Calling

**Add to NPCDatabase** (or equivalent creature registry):
```csharp
new NPCDefinition
{
    Name = "Noble Djinni",
    CreatureType = CreatureType.Outsider,
    Subtypes = new[] { "Air", "Extraplanar" },
    Size = CreatureSize.Large,
    HitDice = 7,
    HitDieType = 8,
    ConMod = 2,
    HP = 45,
    AC = 16,
    TouchAC = 12,
    FlatFootedAC = 13,
    BaseAttackBonus = 7,
    GrappleBonus = 15,
    Speed = 20,
    FlySpeed = 60,
    Attacks = new[]
    {
        new Attack { Name = "Slam", Bonus = 10, Damage = "1d8+6", Type = DamageType.Bludgeoning }
    },
    FullAttack = new[]
    {
        new Attack { Name = "Slam", Bonus = 10, Damage = "1d8+6" },
        new Attack { Name = "Slam", Bonus = 10, Damage = "1d8+6" }
    },
    Str = 18, Dex = 17, Con = 14, Int = 14, Wis = 15, Cha = 15,
    FortSave = 7, RefSave = 8, WillSave = 7,
    Immunities = new[] { DamageType.Acid },
    DarkVision = 60,
    SpellLikeAbilities = new[]
    {
        // Simplified for prototype — not all SLAs need full implementation
        new SLA { Name = "Invisibility", Frequency = "At Will", SelfOnly = true },
    }
}
```

---

### 4.6 Equip-Triggered Effects

**Required for:** Ring of Spell Turning

The current ring equip system applies passive stat bonuses. Ring of Spell Turning needs to:
1. Apply a status effect when equipped (not when "used")
2. Remove the status effect when unequipped
3. Refresh on rest

**Integration point:** The ring equip/unequip handlers in `Inventory.cs` and/or `CharacterStats.cs`.

**Add to equip handler:**
```csharp
// In ring equip logic:
if (ring.ItemName == RingNames.RING_OF_SPELL_TURNING)
{
    int turningLevels = Roll(1, 4) + 6;
    ApplySpellTurningEffect(wearer, turningLevels);
    ring.SpellTurningLevelsRemaining = turningLevels;
}
```

---

### 4.7 Ring of the Ram — Force Bolt & Bull Rush

**New combat effect** — not an existing spell:

1. **Ranged touch attack** at target within 50 ft
2. **Force damage** (1d6 per charge, 1–3 charges)
3. **Bull rush attempt** with Str 25 (+7 mod) + 1 per charge spent
4. **Double damage vs objects**

**Bull rush resolution:**
- Attacker roll: d20 + 7 (Str 25 mod) + charges_spent + 4 (Large virtual size for force)
- Defender roll: d20 + Str mod + size mod
- If attacker wins: push target 5 ft per point of difference
- Uses existing bull rush mechanics if present; otherwise implement simplified version

---

### 4.8 Ring of Shooting Stars — Ball Lightning & Shooting Stars Effects

#### Ball Lightning (new effect):
1. Player chooses 1–4 balls to create
2. Damage inversely scales with count: 1→4d6, 2→3d6 each, 3→2d6 each, 4→1d6+1 each
3. Balls persist for 4 rounds
4. Each round, player can direct a ball (move action) to strike a target
5. Reflex DC 13 for half
6. **Prototype simplification:** Create balls and immediately strike targets (skip the multi-round directing). Player chooses targets for each ball.

#### Shooting Stars (new effect):
1. Player fires 1–3 stars at locations within 70 ft
2. Each creates a 5-ft-radius burst
3. 12 fire damage per star (flat, not dice), Reflex DC 13 half
4. **Restriction:** Outdoors at night only (check environment flag)

#### Faerie Fire:
- May have existing implementation (check `SpellNames.FAERIE_FIRE`)
- Effect: Outlined creatures visible if invisible, -20 Hide penalty
- Apply as status effect to targets in area

---

## 5. Implementation Phases

### Phase 1: Core Infrastructure (Days 1–4)

#### 1A: Ring Activation System (Days 1–2)
**Files Modified:** `GameManager.cs`, `ItemData.cs`
- [ ] Add `HasActiveAbility` property to `ItemData`
- [ ] Add `IsRing` check to item use switch (`GameManager.cs:~5407`)
- [ ] Implement `TryUseRing()` method (skeleton with switch statement)
- [ ] Implement `GetRingActionType()` — returns Standard or FullRound per ring
- [ ] Test: Equip a ring, click "Use" → routes to TryUseRing → logs "not yet implemented" for each ring

#### 1B: Use Tracking Systems (Days 2–3)
**Files Modified:** `ItemData.cs`, `GameManager.cs` (rest handler), `RingFactory.cs`
- [ ] Add daily use fields: `RingDailyUsesRemaining`, `RingDailyUsesMax` dictionaries
- [ ] Add weekly use fields: `RingWeeklyUsesRemaining`, `RingWeeklyUsesMax` dictionaries
- [ ] Add charge fields: `CurrentCharges`, `MaxCharges`, `ChargesRegenerate`, `ChargesRegainedPerDay`
- [ ] Add `RestsSinceWeeklyReset` to game state
- [ ] Implement daily use reset in rest handler
- [ ] Implement weekly use reset in rest handler (every 7th rest)
- [ ] Implement charge regeneration in rest handler
- [ ] Update `RingFactory` to initialize use tracking for each Sprint 2 ring
- [ ] Test: Rest → verify daily uses reset, charges regenerate, weekly counter advances

#### 1C: Ring Ability Selection Panel (Days 3–4)
**Files Created:** `RingAbilitySelectionPanel.cs` (or adapt `StaffSpellSelectionPanel`)
- [ ] Create generic `RingAbilitySelectionPanel` UI component
- [ ] Create `RingAbilityOption` data class
- [ ] Wire panel to `TryUseRing()` flow for multi-ability rings
- [ ] Test: Open panel for Ring of Shooting Stars → shows 5 abilities → selecting one closes panel

---

### Phase 2: Simple Rings — Existing Spell Effects (Days 5–7)

#### 2A: Ring of Invisibility (Day 5)
**Files Modified:** `GameManager.cs` (or new `GameManager_Rings.cs` partial class)
- [ ] Implement `ActivateRingOfInvisibility()` — calls `ApplyInvisibilityEffect(30, caster)`
- [ ] Log activation message
- [ ] Consume standard action
- [ ] Test: Activate → character becomes invisible for 30 rounds → breaks on attack

#### 2B: Ring of Blinking (Day 5)
- [ ] Implement `ActivateRingOfBlinking()` — calls blink effect with 7-round duration
- [ ] Test: Activate → 50% miss chance on incoming, 20% on outgoing

#### 2C: Ring of Telekinesis (Days 6–7)
- [ ] Implement `ActivateRingOfTelekinesis()` — opens mode selection panel
- [ ] Implement **Violent Thrust** mode: ranged attack, damage calculation
- [ ] Implement **Combat Maneuver** mode: opposed CL check for bull rush/trip
- [ ] **Sustained Force:** Log-only (narrative flavor)
- [ ] Test: Each mode functions correctly

---

### Phase 3: Moderate Rings — New Effects with Tracking (Days 8–12)

#### 3A: Ring of Animal Friendship (Days 8–9)
**Files Modified:** `GameManager.cs`, `SpellNames.cs`
- [ ] Add `SpellNames.CHARM_ANIMAL` constant
- [ ] Implement `ActivateRingOfAnimalFriendship()` — targeting mode, animal type check
- [ ] Implement Charm Animal effect: Will save DC 11, change target team, apply charmed status
- [ ] Enforce 12 HD cap per ring
- [ ] Track daily uses (3/day)
- [ ] Implement charm break on allied attack
- [ ] Test: Charm animal → verify team change, save works, HD cap enforced, daily limit works

#### 3B: Ring of the Ram (Days 9–10)
**Files Modified:** `GameManager.cs`, `RingAbilitySelectionPanel.cs`
- [ ] Implement charge selection (1/2/3)
- [ ] Implement ranged touch attack
- [ ] Implement force damage (1d6 per charge)
- [ ] Implement bull rush with Str 25 + charges
- [ ] Double damage vs objects
- [ ] Track charges; verify regeneration on rest (1d10/day)
- [ ] Test: Various charge amounts, hit/miss, bull rush resolution, object damage

#### 3C: Ring of Spell Turning (Days 11–12)
**Files Modified:** `Inventory.cs` or `CharacterStats.cs` (equip handler), `GameManager.cs` (rest handler)
- [ ] Apply Spell Turning status effect on ring equip (1d4+6 pool)
- [ ] Remove on unequip
- [ ] Refresh pool on rest
- [ ] Leverage existing `SpellTurning:{N}` CustomTag system
- [ ] Test: Equip → pool appears → targeted spell reflected → pool decreases → deplete → no more reflections → rest → new pool

---

### Phase 4: Complex Rings — Multi-Ability & Summoning (Days 13–18)

#### 4A: Ring of X-Ray Vision (Days 13–14)
**Files Modified:** `GameManager.cs`, `CharacterController.cs`
- [ ] Implement X-Ray Vision status effect (See Invisibility + reveal hidden, 20 ft range)
- [ ] Track uses per rest for Con damage mechanic
- [ ] Apply Con damage on 2nd+ use per rest
- [ ] Test: First use → no penalty, second use → Con damage, reveals invisible creatures

#### 4B: Ring of Shooting Stars (Days 14–16)
**Files Modified:** `GameManager.cs`, `SpellNames.cs`, `RingAbilitySelectionPanel.cs`
- [ ] Wire Ring of Shooting Stars to ability selection panel (5 abilities)
- [ ] Implement **Light** (at will) — apply light status/illumination
- [ ] Implement **Dancing Lights** (at will, night/outdoor check) — existing spell if available
- [ ] Implement **Faerie Fire** (2/day) — outline targets, -20 Hide, reveal invisible
- [ ] Implement **Ball Lightning** (1/day) — create 1–4 balls, damage scaling, Reflex DC 13
- [ ] Implement **Shooting Stars** (3/week, night/outdoor) — 1–3 stars, 12 damage, 5-ft burst, Reflex DC 13
- [ ] Wire all use tracking (daily + weekly)
- [ ] Test: Each ability independently; verify use limits; verify reset on rest

#### 4C: Ring of Djinni Calling (Days 17–18)
**Files Modified:** `NPCDatabase.cs` (or equivalent), `GameManager.cs`
- [ ] Create Noble Djinni `NPCDefinition` with full stats from MM
- [ ] Implement `ActivateRingOfDjinniCalling()` — full-round action, weekly check
- [ ] Call `SpawnSummonedCreature()` with Djinni definition, 600-round duration
- [ ] Implement djinni death handler: set `DjinniSlain` flag on ring, make ring inert
- [ ] Weekly use tracking (1/week)
- [ ] Test: Summon → Djinni appears as ally → fights alongside party → 600 rounds → despawns
- [ ] Test: Kill Djinni → ring becomes inert → cannot summon again

---

### Phase 5: Polish & Integration Testing (Days 19–20)

- [ ] **UI Polish:** Tooltip updates showing ring abilities, charges, uses remaining
- [ ] **Combat log messages** for all ring activations
- [ ] **Edge cases:** What happens if ring is unequipped mid-effect? (Invisibility stays, Spell Turning removed)
- [ ] **Interaction testing:** Multiple rings equipped simultaneously
- [ ] **Save/Load:** Verify ring state (charges, uses, djinni slain flag) persists
- [ ] **Full playtest:** Create a test encounter using each of the 9 rings
- [ ] **Bug fixes and balance adjustments**

---

## 6. Codebase Integration Points

### 6.1 Files to Modify

| File | Changes | Priority |
|------|---------|----------|
| **GameManager.cs** | Add `TryUseRing()`, ring activation handlers, rest handler updates | Critical |
| **ItemData.cs** | Add daily/weekly/charge tracking fields, `HasActiveAbility` | Critical |
| **RingFactory.cs** | Initialize Sprint 2 rings with active ability data | Critical |
| **RingDatabase.cs** | Register Sprint 2 ring definitions | Critical |
| **Inventory.cs** | Equip-triggered effects (Spell Turning) | High |
| **CharacterStats.cs** | Spell Turning on equip/unequip | High |
| **SpellNames.cs** | Add `CHARM_ANIMAL` constant | Medium |

### 6.2 Files to Create

| File | Purpose | Priority |
|------|---------|----------|
| **GameManager_Rings.cs** | Partial class for all ring activation methods (keeps GameManager.cs clean) | Critical |
| **RingAbilitySelectionPanel.cs** | UI panel for multi-ability rings | High |
| **RingAbilityOption.cs** | Data class for panel options | High |
| **NobleDjinniDefinition** | NPCDefinition entry for Djinni (may be added to existing NPCDatabase) | Medium |

### 6.3 Existing Methods to Leverage

| Method | Location | Used By |
|--------|----------|---------|
| `ApplyInvisibilityEffect()` | `CharacterController.cs:1708` | Ring of Invisibility |
| `HasActiveBlinkEffect` | `CharacterController.cs:1583` | Ring of Blinking |
| `ApplySpellTurningEffect()` | `GameManager_Spells_Phase2.cs:392` | Ring of Spell Turning |
| `SpawnSummonedCreature()` | `GameManager.SpellCasting.cs:228` | Ring of Djinni Calling |
| `CanUseItemManipulationAction()` | `GameManager.cs:4322` | All active rings |
| `ConsumeItemManipulationAction()` | (near 4322) | All active rings |
| `TryUseStaff()` | `GameManager.cs:5792` | Template for TryUseRing |
| `ActiveSummonInstance` | `GameManager.cs:445` | Djinni Calling tracking |
| Rest handler | `GameManager.cs:935` | Daily/weekly/charge reset |

### 6.4 Item Use Flow — Full Integration Diagram

```
Player clicks "Use Item" on equipped ring
    │
    ▼
GameManager.TryUseConsumableFromInventory()  [~line 5390]
    │
    ├─ IsScroll? → TryUseScroll()
    ├─ IsWand?   → TryUseWand()
    ├─ IsStaff?  → TryUseStaff()
    ├─ IsRing && HasActiveAbility?  → TryUseRing()  ◄── NEW
    │       │
    │       ├─ Check action economy (Standard or FullRound)
    │       ├─ Check use limits (daily/weekly/charges)
    │       │
    │       ├─ Single-ability rings → Execute directly
    │       │   ├─ Invisibility → ApplyInvisibilityEffect()
    │       │   ├─ Blinking → ApplyBlinkEffect()
    │       │   ├─ Animal Friendship → Targeting → Charm
    │       │   ├─ Ram → Charge Select → Targeting → Force Bolt
    │       │   ├─ X-Ray Vision → Apply Status Effect
    │       │   └─ Djinni Calling → SpawnSummonedCreature()
    │       │
    │       └─ Multi-ability rings → Open Selection Panel
    │           ├─ Shooting Stars → RingAbilitySelectionPanel (5 options)
    │           └─ Telekinesis → RingAbilitySelectionPanel (3 modes)
    │
    └─ else → Potion
```

---

## 7. UI/UX Design

### 7.1 Ring Tooltip Updates

**Current (Sprint 1):** Shows ring name, price, passive bonuses.

**Sprint 2 addition:** Active rings show additional info:

```
┌─────────────────────────────────┐
│ Ring of Animal Friendship       │
│ Market Price: 10,800 gp         │
│ ─────────────────────────────── │
│ Active Ability:                 │
│   Charm Animal (3/day)          │
│   Uses remaining: 2/3           │
│   Will Save DC 11               │
│   Standard action to activate   │
│ ─────────────────────────────── │
│ Aura: Faint enchantment         │
└─────────────────────────────────┘

┌─────────────────────────────────┐
│ Ring of the Ram                 │
│ Market Price: 8,600 gp          │
│ ─────────────────────────────── │
│ Active Ability:                 │
│   Force Bolt (charge-based)     │
│   Charges: 47/50                │
│   Spend 1-3 charges per use     │
│   Ranged touch attack, 50 ft    │
│   Standard action to activate   │
│ ─────────────────────────────── │
│ Aura: Moderate evocation        │
└─────────────────────────────────┘
```

### 7.2 Ring Ability Selection Panel (Shooting Stars)

```
┌──────────────────────────────────────┐
│     Ring of Shooting Stars           │
│ ──────────────────────────────────── │
│                                      │
│  [✦] Light              (at will)   │
│  [✦] Dancing Lights     (at will)*  │
│  [✦] Faerie Fire        (1/2 today) │
│  [✦] Ball Lightning     (0/1 today) │  ← grayed out
│  [✦] Shooting Stars     (2/3 week)* │
│                                      │
│  * = Outdoors at night only          │
│                                      │
│              [Cancel]                │
└──────────────────────────────────────┘
```

### 7.3 Charge Selection Panel (Ring of the Ram)

```
┌─────────────────────────────────┐
│     Ring of the Ram             │
│     Charges: 47/50              │
│ ─────────────────────────────── │
│                                 │
│  Expend charges:                │
│  [1] 1d6 damage, +8 bull rush  │
│  [2] 2d6 damage, +9 bull rush  │
│  [3] 3d6 damage, +10 bull rush │
│                                 │
│           [Cancel]              │
└─────────────────────────────────┘
```

### 7.4 Combat Log Messages

| Ring | Activation Message | Effect Message |
|------|--------------------|----------------|
| Invisibility | "{Name} speaks a command word and fades from sight" | "{Name} is invisible (30 rounds)" |
| Blinking | "{Name} activates the Ring of Blinking" | "{Name} blinks between planes (7 rounds)" |
| Animal Friendship | "{Name} commands the Ring of Animal Friendship" | "{Target} is charmed! / {Target} resists the charm (DC 11)" |
| Ram | "{Name} points the Ring of the Ram ({N} charges)" | "{Target} hit for {X} force damage / Bull rush: pushed {Y} ft" |
| Telekinesis | "{Name} activates Ring of Telekinesis" | (varies by mode) |
| X-Ray Vision | "{Name} peers through barriers with X-Ray vision" | "Hidden creatures revealed within 20 ft" |
| Shooting Stars | "{Name} invokes the Ring of Shooting Stars" | (varies by ability) |
| Spell Turning | (automatic, no activation) | "{N} spell levels reflected back at {Caster}!" |
| Djinni Calling | "{Name} calls forth the Noble Djinni!" | "A Noble Djinni appears to serve!" |

---

## 8. Testing Plan

### 8.1 Unit Tests per Ring

#### Ring of Invisibility
| Test | Expected Result |
|------|-----------------|
| Activate while having standard action | Character gains invisibility, 30-round duration |
| Activate with no action available | Fails with message |
| Attack while invisible | Invisibility breaks |
| Duration expires | Invisibility removed |
| Multiple activations | Each creates new duration |

#### Ring of Blinking
| Test | Expected Result |
|------|-----------------|
| Activate | Blink effect for 7 rounds |
| Incoming attack while blinking | 50% miss chance applied |
| Outgoing attack while blinking | 20% miss chance applied |
| Duration expires | Blink effect removed |

#### Ring of Animal Friendship
| Test | Expected Result |
|------|-----------------|
| Target animal, first use of day | Will save DC 11; charm on fail |
| Target non-animal | "Target must be an animal" message |
| Use 4th time in a day | "No uses remaining" message |
| Exceed 12 HD cap | "Cannot charm: would exceed 12 HD" |
| Attack charmed animal | Charm breaks |
| Rest | Daily uses reset to 3 |

#### Ring of the Ram
| Test | Expected Result |
|------|-----------------|
| Spend 1 charge, hit | 1d6 force damage + bull rush |
| Spend 3 charges, hit | 3d6 force damage + bull rush (Str 28) |
| Miss on touch attack | No damage, charges still spent |
| Hit object | Double damage |
| 0 charges remaining | "No charges remaining" |
| Rest with 40 charges | Gains 1d10, capped at 50 |

#### Ring of Telekinesis
| Test | Expected Result |
|------|-----------------|
| Violent Thrust — hit | Damage dealt based on object weight |
| Combat Maneuver — win check | Target bull rushed/tripped |
| Sustained Force | Log message only |

#### Ring of X-Ray Vision
| Test | Expected Result |
|------|-----------------|
| First use | X-Ray vision 10 rounds, no Con damage |
| Second use same rest | X-Ray vision + 1 Con damage |
| Reveals invisible creature | Invisible creature within 20 ft becomes visible |
| Rest | Uses counter resets |

#### Ring of Shooting Stars
| Test | Expected Result |
|------|-----------------|
| Light at will | Light effect applied |
| Dancing Lights outdoors at night | Effect applied |
| Dancing Lights indoors | "Must be outdoors at night" |
| Faerie Fire 1st use | Target outlined, -20 Hide |
| Faerie Fire 3rd use | "No uses remaining today" (max 2) |
| Ball Lightning (1 ball) | 4d6 damage, Reflex DC 13 half |
| Ball Lightning (4 balls) | 1d6+1 each, 4 targets |
| Ball Lightning 2nd use same day | "No uses remaining today" |
| Shooting Stars 1st use | Up to 3 stars, 12 damage each |
| Shooting Stars 4th use same week | "No uses remaining this week" |
| Rest | Daily uses reset (Ball Lightning, Faerie Fire) |
| 7th rest | Weekly uses reset (Shooting Stars) |

#### Ring of Spell Turning
| Test | Expected Result |
|------|-----------------|
| Equip ring | Spell Turning pool (7–10 levels) applied |
| Targeted by 3rd-level spell, pool has 8 | Spell reflected, pool → 5 |
| Targeted by spell exceeding pool | Partial reflection, both affected |
| Pool reaches 0 | Ring inert, no more reflections |
| Unequip | Spell Turning effect removed |
| Rest while equipped | New pool (1d4+6) |

#### Ring of Djinni Calling
| Test | Expected Result |
|------|-----------------|
| Summon with full-round action | Djinni spawns as ally, 600-round duration |
| Summon with only standard action | Fails (requires full-round) |
| Second summon same week | "Already called this week" |
| 7th rest | Weekly use resets |
| Djinni killed | Ring becomes inert, "DjinniSlain" flag set |
| Try to summon after djinni killed | "Ring is inert — djinni was slain" |
| Djinni attacks enemies | Fights alongside party |
| 600 rounds expire | Djinni despawns |

### 8.2 Integration Tests

| Test Scenario | Rings Involved | Expected |
|---------------|---------------|----------|
| Two active rings equipped simultaneously | Invisibility + Ram | Both can be activated in same combat |
| Ring + spell stacking | Spell Turning ring + cast Spell Turning | Both pools active (per D&D rules) |
| Rest with multiple rings | Ram + Animal Friendship + Shooting Stars | All daily/charge resets occur |
| Unequip mid-effect | Invisibility (active) → unequip | Invisibility persists (it's on the character, not the ring) |
| Unequip Spell Turning | Spell Turning → unequip | Effect removed immediately |
| Save/Load with ring state | Ram (partial charges) + Djinni (slain) | State preserved across save/load |

### 8.3 Regression Tests

| Test | Ensure |
|------|--------|
| Sprint 1 passive rings | All 36 passive variants still work |
| Scroll/Wand/Staff use | Adding ring branch doesn't break other item use |
| Existing spell casting | Ring-triggered spells don't interfere |
| Rest handler | Existing resets (rage, turn undead, domains) still work |
| Combat flow | Ring activations don't break turn order |

---

## 9. Risk Assessment & Mitigation

### 9.1 Technical Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **GameManager.cs bloat** — adding 9 ring handlers to already-large file | High | Medium | Use `GameManager_Rings.cs` partial class; keep handlers modular |
| **Spell Turning edge cases** — partial reflection, same-spell interaction | Medium | High | Reuse existing implementation; add unit tests for edge cases |
| **Summoning system integration** — Djinni lifetime/death tracking | Medium | High | Follow exact `SpawnSummonedCreature` pattern; add death callback |
| **Weekly use tracking accuracy** — rest-count approach may drift | Low | Low | Accept approximation for prototype; document limitation |
| **Save/Load serialization** — new ItemData fields need serialization | Medium | High | Add all new fields to serialization; test save/load early |
| **Bull rush mechanics** — Ring of Ram needs proper push resolution | Medium | Medium | Implement simplified version if full bull rush doesn't exist |
| **UI panel creation** — Unity UI requires prefab + scene setup | Medium | Medium | Clone StaffSpellSelectionPanel as template |
| **Charm Animal creature type check** — NPCDefinition may lack type field | Medium | Medium | Add CreatureType enum if missing; tag animals in database |

### 9.2 Design Risks

| Risk | Mitigation |
|------|------------|
| **X-Ray Vision too powerful or too weak** | Limit to See Invisibility + hidden creature reveal (combat-relevant); note as prototype simplification |
| **Ring of Telekinesis too complex** | Simplify to Violent Thrust + Combat Maneuver; Sustained Force is flavor-only |
| **Shooting Stars balance** — 5 abilities may be confusing | Clear UI panel with descriptions; grayed-out unavailable options |
| **Djinni too powerful as permanent summon** | 600-round duration is long but finite; weekly limit + death penalty balances |

### 9.3 Schedule Risks

| Risk | Mitigation |
|------|------------|
| **Infrastructure takes longer** | Start with core activation system; rings can be stubbed |
| **Ball Lightning complexity** | Simplify to immediate-strike (skip multi-round directing) |
| **Telekinesis three modes** | Implement Violent Thrust first; other modes are stretch goals |
| **Total scope exceeds estimate** | Prioritize rings by value: Invisibility, Blinking, Ram (most likely to be used in combat) |

### 9.4 Priority Order (if time-constrained)

1. **Core infrastructure** (activation system, use tracking) — blocks everything
2. **Ring of Invisibility** — simplest, validates the activation system
3. **Ring of Blinking** — second simplest, same pattern
4. **Ring of the Ram** — validates charge system
5. **Ring of Animal Friendship** — validates daily use system
6. **Ring of Spell Turning** — validates equip-triggered system
7. **Ring of Telekinesis** — moderate complexity
8. **Ring of Djinni Calling** — validates weekly + summoning
9. **Ring of X-Ray Vision** — least combat impact
10. **Ring of Shooting Stars** — most complex, do last

---

## 10. Appendix: File Change Summary

### Modified Files (estimated lines changed)

| File | Est. Lines Added | Nature of Changes |
|------|-----------------|-------------------|
| `GameManager.cs` | +30 | Ring branch in item use switch, rest handler updates |
| `ItemData.cs` | +40 | Daily/weekly/charge tracking fields, HasActiveAbility |
| `RingFactory.cs` | +120 | Sprint 2 ring initialization with active ability data |
| `RingDatabase.cs` | +20 | Register Sprint 2 rings |
| `Inventory.cs` | +15 | Equip-triggered effects (Spell Turning) |
| `CharacterStats.cs` | +10 | Spell Turning equip/unequip |
| `SpellNames.cs` | +2 | CHARM_ANIMAL, FAERIE_FIRE constants |
| `NPCDatabase.cs` | +40 | Noble Djinni definition |

### New Files

| File | Est. Lines | Purpose |
|------|-----------|---------|
| `GameManager_Rings.cs` | ~500 | All ring activation handlers (partial class) |
| `RingAbilitySelectionPanel.cs` | ~100 | UI panel for multi-ability rings |
| `RingAbilityOption.cs` | ~25 | Data class for panel options |

### Total Estimated Code Changes
- **Modified:** ~277 lines across 8 files
- **New:** ~625 lines across 3 files
- **Grand total:** ~900 lines of new/modified code

---

*Document version: 1.0 — Sprint 2 Detailed Implementation Plan*  
*Based on codebase analysis of commit `a77a6f7` (Sprint 1 complete)*  
*Rules reference: D&D 3.5e PHB, DMG, MM (core only)*

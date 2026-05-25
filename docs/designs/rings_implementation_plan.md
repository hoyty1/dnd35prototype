# D&D 3.5e Magic Rings — Comprehensive Implementation Plan

> **Source:** DMG 3.5e pages 229–233 + SRD  
> **Scope:** Core rules ONLY (PHB/DMG/MM). No supplements.  
> **Project:** `/home/ubuntu/dnd35prototype`  
> **Date:** 2026-05-24

---

## Executive Summary

This document catalogs **all 33 distinct magic rings** from the DMG 3.5e core rules (counting variants: 47 total ring items), categorizes them by implementation complexity, identifies required new systems, and provides a phased implementation roadmap.

### Key Statistics

| Metric | Value |
|--------|-------|
| **Total distinct ring types** | 33 |
| **Total ring item variants** | 47 (counting +1 through +5, energy types, etc.) |
| **Tier 1 (Simple)** | 14 rings |
| **Tier 2 (Active)** | 9 rings |
| **Tier 3 (Complex)** | 6 rings |
| **Tier 4 (Legendary)** | 4 rings |
| **Estimated total time** | 8–12 weeks |
| **New systems required** | 4 major, 3 minor |

### Current Codebase Status

✅ **Ring equipment slots already exist** — `EquipSlot.LeftRing`, `EquipSlot.RightRing`, `EquipSlot.EitherRing`  
✅ **Inventory slots wired** — `Inventory.LeftRingSlot`, `Inventory.RightRingSlot`  
✅ **UI displays ring slots** — "L RING" and "R RING" in both InventoryUI and PreCombatInventoryUI  
✅ **Spell effect ticking** — GameManager already ticks `LeftRingSlot`/`RightRingSlot` for item spell durations  
❌ **No ring items registered** — ItemDatabase has zero ring entries  
❌ **No ring stat effects** — `RecalculateStats()` ignores ring slots  
❌ **No ring-specific ItemData fields** — No `IsRing`, `RingType`, etc.  
❌ **No ring activation system** — No command word / use-activated ring framework  
❌ **No RingFactory** — Unlike WandFactory/PotionFactory/ScrollFactory  

---

## Complete Ring Catalog (DMG 3.5e Core)

### Ring #1: Ring of Protection (+1 to +5)

| Property | Value |
|----------|-------|
| **DMG Page** | 232 |
| **Type** | Continuous passive |
| **Complexity** | ⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | +1: 2,000 gp · +2: 8,000 gp · +3: 18,000 gp · +4: 32,000 gp · +5: 50,000 gp |
| **Caster Level** | 5th |
| **Aura** | Faint abjuration |
| **Variants** | 5 (+1 through +5) |

**Core Mechanics:**
- Provides a **deflection bonus to AC** equal to the ring's bonus (+1 to +5)
- Continuous — always active while worn
- Deflection bonuses do NOT stack (highest wins)

**Implementation Requirements:**
- Add `DeflectionBonus` from ring to `RecalculateStats()`
- `CharacterStats.DeflectionBonus` already exists (used by Shield of Faith)
- Need to ensure ring deflection doesn't stack with spell deflection (take highest)

**Testing Checklist:**
- [ ] Ring equippable in either ring slot
- [ ] Deflection bonus appears in AC calculation
- [ ] Does NOT stack with Shield of Faith (take highest)
- [ ] Unequipping removes the bonus
- [ ] Two Protection rings — only one bonus applies (highest)
- [ ] All 5 variants (+1 through +5) work

---

### Ring #2: Ring of Feather Falling

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Continuous passive (auto-activate) |
| **Complexity** | ⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | 2,200 gp |
| **Caster Level** | 1st |
| **Aura** | Faint transmutation |

**Core Mechanics:**
- Automatically activates Feather Fall when wearer falls more than 5 feet
- No action required — immediate response
- Continuous effect while worn

**Implementation Requirements:**
- Falling system not currently implemented in grid-based prototype
- Can register as a tag/flag on the character for future use
- Minimal immediate gameplay impact in current tactical grid

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Character gains "feather_falling" tag/flag
- [ ] Flag removed on unequip

---

### Ring #3: Ring of Sustenance

| Property | Value |
|----------|-------|
| **DMG Page** | 232 |
| **Type** | Continuous passive |
| **Complexity** | ⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | 2,500 gp |
| **Caster Level** | 5th |
| **Aura** | Faint conjuration |

**Core Mechanics:**
- Wearer needs no food or water to survive
- Wearer needs only 2 hours of sleep per day (still needs 8 hours rest for spells)
- Must be worn for 1 full week before it takes effect

**Implementation Requirements:**
- No food/water/sleep system in current prototype
- Register as passive flag for future use

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Sustenance flag set

---

### Ring #4: Ring of Swimming

| Property | Value |
|----------|-------|
| **DMG Page** | 232 |
| **Type** | Continuous passive |
| **Complexity** | ⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | 2,500 gp (standard) · 10,000 gp (improved, +10) |
| **Caster Level** | 2nd / 7th |
| **Aura** | Faint transmutation |
| **Variants** | 2 (standard +5, improved +10) |

**Core Mechanics:**
- Grants +5 competence bonus on Swim checks (standard)
- Grants +10 competence bonus on Swim checks (improved)

**Implementation Requirements:**
- Skill check system needs competence bonus support
- No Swim skill checks currently used in tactical combat
- Register as passive bonus for future use

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Swim bonus stored on character

---

### Ring #5: Ring of Climbing

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Continuous passive |
| **Complexity** | ⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | 2,500 gp (standard) · 10,000 gp (improved, +10) |
| **Caster Level** | 5th |
| **Aura** | Faint transmutation |
| **Variants** | 2 (standard +5, improved +10) |

**Core Mechanics:**
- Grants +5 competence bonus on Climb checks (standard)
- Grants +10 competence bonus on Climb checks (improved)

**Implementation Requirements:**
- Same as Swimming — skill bonus for future use
- No climbing in tactical grid currently

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Climb bonus stored on character

---

### Ring #6: Ring of Jumping

| Property | Value |
|----------|-------|
| **DMG Page** | 231 |
| **Type** | Continuous passive |
| **Complexity** | ⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | 2,500 gp (standard) · 10,000 gp (improved, +10) |
| **Caster Level** | 2nd / 7th |
| **Aura** | Faint transmutation |
| **Variants** | 2 (standard +5, improved +10) |

**Core Mechanics:**
- Grants +5 competence bonus on Jump checks (standard)
- Grants +10 competence bonus on Jump checks (improved)

**Implementation Requirements:**
- Same as Swimming/Climbing — skill bonus for future use

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Jump bonus stored on character

---

### Ring #7: Ring of Mind Shielding

| Property | Value |
|----------|-------|
| **DMG Page** | 232 |
| **Type** | Continuous passive |
| **Complexity** | ⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | 8,000 gp |
| **Caster Level** | 3rd |
| **Aura** | Faint abjuration |

**Core Mechanics:**
- Immune to Detect Thoughts
- Immune to Discern Lies
- Immune to any attempt to magically discern alignment

**Implementation Requirements:**
- Register as immunity flag
- Detect Thoughts and alignment detection spells check for this immunity

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Mind shielding immunity flag set
- [ ] Detect Thoughts blocked (if implemented)

---

### Ring #8: Ring of Force Shield

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Activated (free action toggle) |
| **Complexity** | ⭐⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | 8,500 gp |
| **Caster Level** | 9th |
| **Aura** | Moderate evocation |

**Core Mechanics:**
- Generates a shield-sized wall of force when activated
- Provides +2 shield bonus to AC (as heavy shield)
- NO armor check penalty
- NO arcane spell failure chance
- Activated/deactivated as a free action
- Hand holding the ring is occupied (cannot hold items)

**Implementation Requirements:**
- Free action activation toggle
- Apply +2 shield bonus to AC when active
- Track activation state on the item
- Ensure no ACP/ASF from force shield

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Activation provides +2 shield AC
- [ ] No ACP or ASF
- [ ] Deactivation removes bonus
- [ ] Shield bonus doesn't stack with equipped shield

---

### Ring #9: Ring of Counterspells

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Charged (single spell storage) |
| **Complexity** | ⭐⭐⭐ |
| **Tier** | 3 — Complex |
| **Market Price** | 4,000 gp |
| **Caster Level** | 11th |
| **Aura** | Moderate evocation |

**Core Mechanics:**
- Can store a single spell of 1st–6th level
- If that exact spell is cast on the wearer, it's automatically countered
- No action required — automatic
- Countered spell is consumed; ring can be reloaded

**Implementation Requirements:**
- Spell storage system (store one spell in the ring)
- Automatic counter-trigger when matching spell targets wearer
- UI to cast a spell into the ring
- Track stored spell ID on item

**Testing Checklist:**
- [ ] Can store a spell into ring
- [ ] Stored spell auto-counters matching incoming spell
- [ ] Counter consumes stored spell
- [ ] Can reload with new spell
- [ ] Only 1st–6th level spells storable

---

### Ring #10: Ring of Ram

| Property | Value |
|----------|-------|
| **DMG Page** | 232 |
| **Type** | Charged (50 charges, expendable) |
| **Complexity** | ⭐⭐⭐ |
| **Tier** | 2 — Active |
| **Market Price** | 8,600 gp |
| **Caster Level** | 9th |
| **Aura** | Moderate transmutation |

**Core Mechanics:**
- 50 charges, non-rechargeable (becomes nonmagical when expended)
- Ranged touch attack, 50-ft max range, no range penalty
- 1 charge: 1d6 force damage + bull rush (Str 25, Large)
- 2 charges: 2d6 force damage + bull rush (Str 25 +1 bonus)
- 3 charges: 3d6 force damage + bull rush (Str 25 +2 bonus)
- Can also open doors (Str 25/27/29 for 1/2/3 charges)

**Implementation Requirements:**
- Charge tracking on ItemData (existing pattern from wands)
- Ranged touch attack resolution
- Bull rush mechanics
- Charge selection UI (1, 2, or 3 charges)
- Force damage type

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Can select 1/2/3 charges
- [ ] Ranged touch attack rolls correctly
- [ ] Damage scales with charges
- [ ] Bull rush resolves
- [ ] Charges deplete
- [ ] Becomes nonmagical at 0 charges

---

### Ring #11: Ring of Animal Friendship

| Property | Value |
|----------|-------|
| **DMG Page** | 229 |
| **Type** | Spell-like (command word) |
| **Complexity** | ⭐⭐ |
| **Tier** | 2 — Active |
| **Market Price** | 10,800 gp |
| **Caster Level** | 3rd |
| **Aura** | Faint enchantment |

**Core Mechanics:**
- On command, casts Charm Animal on a target
- At will (no daily limit)
- Will save DC 11 negates

**Implementation Requirements:**
- Command word activation system
- Charm Animal spell effect (if not implemented)
- Standard action to activate

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Command activation works
- [ ] Charm Animal effect applies to target
- [ ] Will save offered

---

### Ring #12: Ring of Energy Resistance (Minor/Major/Greater)

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Continuous passive |
| **Complexity** | ⭐⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | Minor: 12,000 gp · Major: 28,000 gp · Greater: 44,000 gp |
| **Caster Level** | 3rd / 7th / 11th |
| **Aura** | Faint/Moderate/Moderate abjuration |
| **Variants** | 15 (3 tiers × 5 energy types: acid, cold, electricity, fire, sonic) |

**Core Mechanics:**
- Absorbs first X points of damage per round from chosen energy type
- Minor: resistance 10
- Major: resistance 20
- Greater: resistance 30
- One energy type per ring (chosen at creation)
- Continuous while worn

**Implementation Requirements:**
- Energy resistance system already exists (`DamageResistances`)
- Add ring resistance entry on equip, remove on unequip
- Need to track which energy type this specific ring resists
- Custom ItemData field: `RingEnergyType` (acid/cold/electricity/fire/sonic)

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Correct energy type resistance applied
- [ ] Resistance value correct (10/20/30)
- [ ] Removed on unequip
- [ ] Stacking rules enforced (doesn't stack with Resist Energy spell — highest wins)

---

### Ring #13: Ring of Chameleon Power

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Continuous passive + command word active |
| **Complexity** | ⭐⭐ |
| **Tier** | 2 — Active |
| **Market Price** | 12,700 gp |
| **Caster Level** | 3rd |
| **Aura** | Faint illusion |

**Core Mechanics:**
- Passive: +10 competence bonus on Hide checks (free action to activate)
- Active: Can use Disguise Self at will (standard action)

**Implementation Requirements:**
- Hide/stealth bonus tracking
- Disguise Self spell integration
- Dual mode (passive + active)

**Testing Checklist:**
- [ ] Ring equippable
- [ ] +10 Hide bonus applies
- [ ] Disguise Self usable at will

---

### Ring #14: Ring of X-Ray Vision

| Property | Value |
|----------|-------|
| **DMG Page** | 233 |
| **Type** | Command word active |
| **Complexity** | ⭐⭐ |
| **Tier** | 2 — Active |
| **Market Price** | 25,000 gp |
| **Caster Level** | 6th |
| **Aura** | Moderate divination |

**Core Mechanics:**
- On command, see through solid matter (20-ft range)
- Penetrates: 1 ft stone, 1 inch metal, 3 ft wood/dirt
- Lead blocks vision
- **Drawback:** 1 point Constitution damage per minute after first 10 minutes/day

**Implementation Requirements:**
- Line of sight through walls (complex for grid system)
- Constitution damage tracking
- Timer for usage

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Vision effect activates
- [ ] Constitution damage applies after 10 min

---

### Ring #15: Ring of Blinking

| Property | Value |
|----------|-------|
| **DMG Page** | 229 |
| **Type** | Command word active |
| **Complexity** | ⭐⭐ |
| **Tier** | 2 — Active |
| **Market Price** | 27,000 gp |
| **Caster Level** | 7th |
| **Aura** | Moderate transmutation |

**Core Mechanics:**
- On command, wearer blinks as per the Blink spell
- 50% miss chance against attacks
- 20% chance own attacks miss
- Can move through solid objects (50% chance of failure)

**Implementation Requirements:**
- Blink spell effect already exists
- Command word activation triggers spell effect
- Standard action to activate

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Blink effect applies on command
- [ ] Miss chances correct

---

### Ring #16: Ring of Invisibility

| Property | Value |
|----------|-------|
| **DMG Page** | 231 |
| **Type** | Command word active |
| **Complexity** | ⭐⭐ |
| **Tier** | 2 — Active |
| **Market Price** | 20,000 gp |
| **Caster Level** | 3rd |
| **Aura** | Faint illusion |

**Core Mechanics:**
- On command, wearer becomes invisible as per Invisibility spell
- Ends when wearer attacks or casts a spell
- At will (no daily limit)

**Implementation Requirements:**
- Invisibility spell effect already exists
- Command word activation → apply Invisibility
- Standard action to activate

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Invisibility applies on command
- [ ] Breaks on attack/spell
- [ ] Can reactivate

---

### Ring #17: Ring of Wizardry (I, II, III, IV)

| Property | Value |
|----------|-------|
| **DMG Page** | 233 |
| **Type** | Continuous passive |
| **Complexity** | ⭐⭐⭐ |
| **Tier** | 3 — Complex |
| **Market Price** | I: 20,000 gp · II: 40,000 gp · III: 70,000 gp · IV: 100,000 gp |
| **Caster Level** | 11th / 14th / 17th / 20th |
| **Aura** | Moderate/Strong (varies) universal |
| **Variants** | 4 (I through IV) |

**Core Mechanics:**
- **Doubles** the arcane spellcaster's spells per day for a specific level
- Ring I: doubles 1st-level arcane spells
- Ring II: doubles 2nd-level arcane spells
- Ring III: doubles 3rd-level arcane spells
- Ring IV: doubles 4th-level arcane spells
- Only works for arcane spellcasters (Wizard, Sorcerer, Bard)
- Does NOT double bonus spells from high ability scores

**Implementation Requirements:**
- Hook into spell slot calculation
- Track which spell level is doubled
- Only apply to base arcane spell slots (not bonus slots)
- Restrict to arcane casters

**Testing Checklist:**
- [ ] Ring equippable only by arcane casters
- [ ] Base spell slots doubled for correct level
- [ ] Bonus slots NOT doubled
- [ ] Removing ring removes extra slots
- [ ] Cannot prepare/use spells beyond allotment after removal

---

### Ring #18: Ring of Evasion

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Continuous passive |
| **Complexity** | ⭐⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | 25,000 gp |
| **Caster Level** | 7th |
| **Aura** | Moderate transmutation |

**Core Mechanics:**
- Grants the Evasion ability continuously
- Successful Reflex save for half damage → take NO damage instead
- Does NOT stack with class-granted Evasion (already have it)
- Does NOT grant Improved Evasion

**Implementation Requirements:**
- Evasion system already exists on CharacterStats
- Set `HasEvasion = true` on equip, remove on unequip
- Need to check if character already has class Evasion (don't double-apply)

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Evasion applies (0 damage on successful Reflex save vs half)
- [ ] Stacks correctly with existing class Evasion (no-op)
- [ ] Removed on unequip

---

### Ring #19: Ring of Spell Storing (Minor/Standard/Major)

| Property | Value |
|----------|-------|
| **DMG Page** | 232 |
| **Type** | Spell storage + casting |
| **Complexity** | ⭐⭐⭐⭐ |
| **Tier** | 3 — Complex |
| **Market Price** | Minor: 18,000 gp · Standard: 50,000 gp · Major: 200,000 gp |
| **Caster Level** | 5th / 9th / 17th |
| **Aura** | Moderate/Strong evocation |
| **Variants** | 3 (Minor/Standard/Major) |

**Core Mechanics:**
- Stores spells cast into it (up to capacity in total spell levels)
- Minor: up to 3 spell levels
- Standard: up to 5 spell levels  
- Major: up to 10 spell levels
- Any creature wearing the ring can cast stored spells
- Spells use the original caster's level, save DC, etc.
- Reusable — can be refilled after spells are cast

**Implementation Requirements:**
- Spell storage data structure on ItemData
- UI to cast spells INTO the ring
- UI to cast spells FROM the ring
- Track caster level/DC per stored spell
- Spell level capacity enforcement

**Testing Checklist:**
- [ ] Can store spells up to capacity
- [ ] Can cast stored spells
- [ ] Correct caster level/DC used
- [ ] Capacity correctly enforced
- [ ] Can refill after use

---

### Ring #20: Ring of Shooting Stars

| Property | Value |
|----------|-------|
| **DMG Page** | 232 |
| **Type** | Daily use abilities (multiple) |
| **Complexity** | ⭐⭐⭐⭐ |
| **Tier** | 3 — Complex |
| **Market Price** | 50,000 gp |
| **Caster Level** | 12th |
| **Aura** | Strong evocation |

**Core Mechanics:**
- **Outdoors at night:**
  - Dancing Lights: 1/hour
  - Light: 2/night
  - Ball Lightning: 1/night (1-4 balls, 1d6–4d6 electricity each)
  - Shooting Stars: 3/week (12 impact + 24 fire in 5-ft radius, Reflex DC 13)
- **Indoors/Underground:**
  - Faerie Fire: 2/day
  - Spark Shower: 1/day (2d8 or 4d8 vs metal-wearing)

**Implementation Requirements:**
- Daily/weekly use tracking
- Multiple ability modes
- Environment detection (outdoors vs indoors) — may simplify
- Ball Lightning targeting and damage
- Shooting Stars as ranged attack + AoE

**Testing Checklist:**
- [ ] All abilities function
- [ ] Use limits track correctly
- [ ] Damage calculations correct

---

### Ring #21: Ring of Spell Turning

| Property | Value |
|----------|-------|
| **DMG Page** | 232 |
| **Type** | Daily charges |
| **Complexity** | ⭐⭐⭐⭐ |
| **Tier** | 3 — Complex |
| **Market Price** | 98,280 gp |
| **Caster Level** | 13th |
| **Aura** | Strong abjuration |

**Core Mechanics:**
- Up to three times per day, reflects 1d4+6 spell levels of spells back at caster
- Only affects spells that target the wearer (not AoE)
- If a spell is too high level for remaining capacity, it's split
- Resets daily

**Implementation Requirements:**
- Spell Turning spell effect already partially implemented
- Daily use counter (3/day)
- Spell level reflection tracking
- Split spell logic

**Testing Checklist:**
- [ ] Activates on incoming targeted spell
- [ ] Reflects correct number of spell levels
- [ ] Daily limit enforced
- [ ] Split spell logic works

---

### Ring #22: Ring of Telekinesis

| Property | Value |
|----------|-------|
| **DMG Page** | 232 |
| **Type** | Spell-like (at will) |
| **Complexity** | ⭐⭐⭐ |
| **Tier** | 2 — Active |
| **Market Price** | 75,000 gp |
| **Caster Level** | 9th |
| **Aura** | Moderate transmutation |

**Core Mechanics:**
- Use Telekinesis spell on command
- At will (no daily limit)
- Violent thrust, sustained force, or combat maneuver
- CL 9th for weight limits

**Implementation Requirements:**
- Telekinesis spell already implemented
- Command word activation → cast Telekinesis at CL 9

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Telekinesis activates on command
- [ ] CL 9 used for effects

---

### Ring #23: Ring of Regeneration

| Property | Value |
|----------|-------|
| **DMG Page** | 232 |
| **Type** | Continuous passive |
| **Complexity** | ⭐⭐⭐ |
| **Tier** | 2 — Active |
| **Market Price** | 90,000 gp |
| **Caster Level** | 15th |
| **Aura** | Strong conjuration |

**Core Mechanics:**
- Regenerate 1 hit point per round (fast healing 1)
- Lost body parts regrow in 1d6+1 days
- Does NOT prevent death from massive damage
- Does NOT protect against effects that don't deal HP damage

**Implementation Requirements:**
- Fast Healing per-round tracking in OnRoundStart
- Apply healing at start of wearer's turn
- Already similar to Regenerate spell mechanics

**Testing Checklist:**
- [ ] Ring equippable
- [ ] 1 HP healed per round
- [ ] Doesn't exceed max HP
- [ ] Healing logged

---

### Ring #24: Ring of Freedom of Movement

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Continuous passive |
| **Complexity** | ⭐⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | 40,000 gp |
| **Caster Level** | 7th |
| **Aura** | Moderate abjuration |

**Core Mechanics:**
- Continuous Freedom of Movement effect
- Immune to entangle, slow, web, hold, paralysis
- Cannot be grappled
- Move normally in water
- No movement penalties

**Implementation Requirements:**
- Freedom of Movement spell effect already exists
- Apply as continuous buff on equip
- Check existing condition immunities

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Immune to movement-restricting effects
- [ ] Cannot be grappled
- [ ] Removed on unequip

---

### Ring #25: Ring of Three Wishes

| Property | Value |
|----------|-------|
| **DMG Page** | 233 |
| **Type** | Charged (3 uses, expendable) |
| **Complexity** | ⭐⭐⭐⭐⭐ |
| **Tier** | 4 — Legendary |
| **Market Price** | 120,600 gp |
| **Caster Level** | 20th |
| **Aura** | Strong universal |

**Core Mechanics:**
- Contains 3 Wish spells
- Each use consumes one wish
- After all 3 wishes used, becomes nonmagical
- NO XP cost when cast from the ring
- Full Wish spell effect

**Implementation Requirements:**
- Wish spell system (WishExecutor + WishUI already exist!)
- Charge tracking (3 charges)
- Becomes nonmagical at 0 charges
- No XP cost override

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Can cast Wish (invokes WishUI)
- [ ] Charges deplete
- [ ] No XP cost
- [ ] Becomes nonmagical at 0

---

### Ring #26: Ring of Djinni Calling

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Summoning (1/day) |
| **Complexity** | ⭐⭐⭐⭐⭐ |
| **Tier** | 4 — Legendary |
| **Market Price** | 125,000 gp |
| **Caster Level** | 17th |
| **Aura** | Strong conjuration |

**Core Mechanics:**
- Calls a specific djinni from Elemental Plane of Air
- Djinni appears next round, serves for up to 1 hour/day
- Djinni is a specific individual (not random)
- If the djinni is slain, ring becomes nonmagical and worthless
- Standard action to call

**Implementation Requirements:**
- Summoning system for named creature
- Djinni stat block (Monster Manual)
- Duration tracking (1 hour/day)
- Permanent death = ring destruction
- AI for summoned creature

**Testing Checklist:**
- [ ] Ring equippable
- [ ] Djinni summoned on command
- [ ] Duration tracked
- [ ] Death destroys ring
- [ ] Daily limit enforced

---

### Ring #27: Ring of Elemental Command (Air)

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Multiple spell-like + passive |
| **Complexity** | ⭐⭐⭐⭐⭐ |
| **Tier** | 4 — Legendary |
| **Market Price** | 200,000 gp |
| **Caster Level** | 15th |
| **Aura** | Strong conjuration |

**Core Mechanics:**
- Appears as Ring of Feather Falling until fully activated (slay air elemental)
- Common powers: Elementals can't attack within 5 ft, Charm Monster DC 17 on air elementals, +2 resistance save vs air creatures, +4 morale attack vs air creatures, bypass elemental DR
- Air-specific powers:
  - Feather Fall (unlimited, self)
  - Resist Energy (electricity) (unlimited, self)
  - Gust of Wind (2/day)
  - Wind Wall (unlimited)
  - Air Walk (1/day, self)
  - Chain Lightning (1/week)
- **Weakness:** -2 on saves vs earth-based effects

**Implementation Requirements:**
- Full elemental dominance system
- Multiple spell-like abilities with varied daily limits
- Activation condition tracking
- Elemental type detection

**Testing Checklist:**
- [ ] Initial appearance as Feather Falling ring
- [ ] Full activation after elemental kill
- [ ] All 6 spell-like abilities work
- [ ] Elemental dominance bonuses apply
- [ ] Earth weakness penalty applies

---

### Ring #28: Ring of Elemental Command (Earth)

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Multiple spell-like + passive |
| **Complexity** | ⭐⭐⭐⭐⭐ |
| **Tier** | 4 — Legendary |
| **Market Price** | 200,000 gp |

**Core Mechanics:**
- Appears as Ring of Meld into Stone until activated
- Earth-specific powers:
  - Meld into Stone (unlimited, self)
  - Soften Earth and Stone (unlimited)
  - Stone Shape (2/day)
  - Stoneskin (1/week, self)
  - Passwall (2/week)
  - Wall of Stone (1/day)
- **Weakness:** -2 on saves vs air-based effects

---

### Ring #29: Ring of Elemental Command (Fire)

| Property | Value |
|----------|-------|
| **DMG Page** | 231 |
| **Type** | Multiple spell-like + passive |
| **Complexity** | ⭐⭐⭐⭐⭐ |
| **Tier** | 4 — Legendary |
| **Market Price** | 200,000 gp |

**Core Mechanics:**
- Appears as Major Ring of Energy Resistance (Fire) until activated
- Fire-specific powers:
  - Resist Energy, Fire (as major ring = 20)
  - Burning Hands (unlimited)
  - Flaming Sphere (2/day)
  - Pyrotechnics (2/day)
  - Wall of Fire (1/day)
  - Flame Strike (2/week)
- **Weakness:** -2 on saves vs water-based effects

---

### Ring #30: Ring of Elemental Command (Water)

| Property | Value |
|----------|-------|
| **DMG Page** | 231 |
| **Type** | Multiple spell-like + passive |
| **Complexity** | ⭐⭐⭐⭐⭐ |
| **Tier** | 4 — Legendary |
| **Market Price** | 200,000 gp |

**Core Mechanics:**
- Appears as Ring of Water Walking until activated
- Water-specific powers:
  - Water Walk (unlimited)
  - Create Water (unlimited)
  - Water Breathing (unlimited)
  - Wall of Ice (1/day)
  - Ice Storm (2/week)
  - Control Water (2/week)
- **Weakness:** -2 on saves vs fire-based effects

---

### Ring #31: Ring of Water Walking

| Property | Value |
|----------|-------|
| **DMG Page** | 233 |
| **Type** | Continuous passive |
| **Complexity** | ⭐ |
| **Tier** | 1 — Foundation |
| **Market Price** | 15,000 gp |
| **Caster Level** | 9th |
| **Aura** | Moderate transmutation |

**Core Mechanics:**
- Walk on water as per Water Walk spell
- Continuous while worn

**Implementation Requirements:**
- No water terrain in current prototype
- Register as passive flag

---

### Ring #32: Ring of Meld into Stone

| Property | Value |
|----------|-------|
| **DMG Page** | 231 |
| **Type** | Spell-like (command word) |
| **Complexity** | ⭐⭐ |
| **Tier** | 2 — Active |
| **Market Price** | 27,000 gp |
| **Caster Level** | 5th |
| **Aura** | Faint transmutation |

**Core Mechanics:**
- Use Meld into Stone spell on command
- At will (no daily limit)

**Implementation Requirements:**
- Meld into Stone spell not implemented
- Low priority for tactical grid combat

---

### Ring #33: Ring of Friend Shield

| Property | Value |
|----------|-------|
| **DMG Page** | 230 |
| **Type** | Paired item, spell-like |
| **Complexity** | ⭐⭐⭐ |
| **Tier** | 3 — Complex |
| **Market Price** | 50,000 gp (for the pair) |
| **Caster Level** | 10th |
| **Aura** | Moderate abjuration |

**Core Mechanics:**
- Always comes in a pair
- Either wearer can activate Shield Other on the other wearer
- No range limitation
- Standard action to activate

**Implementation Requirements:**
- Paired item system
- Shield Other spell integration
- Track which ring is paired with which

---

## Implementation Tiers Summary

### TIER 1: Foundation — Simple Passive Rings (14 rings)

These rings provide continuous bonuses and require minimal new systems.

| Ring | Complexity | Priority | Impact Score |
|------|-----------|----------|-------------|
| Ring of Protection +1–+5 | ⭐ | **HIGH** | 25 |
| Ring of Energy Resistance (minor/major/greater) | ⭐⭐ | **HIGH** | 20 |
| Ring of Evasion | ⭐⭐ | **HIGH** | 20 |
| Ring of Freedom of Movement | ⭐⭐ | **HIGH** | 20 |
| Ring of Force Shield | ⭐⭐ | **HIGH** | 16 |
| Ring of Mind Shielding | ⭐ | Medium | 15 |
| Ring of Feather Falling | ⭐ | Medium | 10 |
| Ring of Sustenance | ⭐ | Low | 5 |
| Ring of Climbing (+5/+10) | ⭐ | Low | 5 |
| Ring of Swimming (+5/+10) | ⭐ | Low | 5 |
| Ring of Jumping (+5/+10) | ⭐ | Low | 5 |
| Ring of Water Walking | ⭐ | Low | 5 |
| Ring of Warmth | ⭐ | Low | 5 |

**Est. Time:** 1–2 weeks  
**Dependencies:** Ring framework, RecalculateStats integration, ItemData ring fields

### TIER 2: Active Abilities — Spell-Like Rings (9 rings)

These rings cast spells or provide active abilities, requiring command word activation.

| Ring | Complexity | Priority | Impact Score |
|------|-----------|----------|-------------|
| Ring of Invisibility | ⭐⭐ | **HIGH** | 16 |
| Ring of Blinking | ⭐⭐ | **HIGH** | 16 |
| Ring of Telekinesis | ⭐⭐⭐ | **HIGH** | 12 |
| Ring of Regeneration | ⭐⭐⭐ | Medium | 12 |
| Ring of Ram | ⭐⭐⭐ | Medium | 12 |
| Ring of Animal Friendship | ⭐⭐ | Medium | 8 |
| Ring of Chameleon Power | ⭐⭐ | Medium | 8 |
| Ring of X-Ray Vision | ⭐⭐ | Low | 8 |
| Ring of Meld into Stone | ⭐⭐ | Low | 4 |

**Est. Time:** 1–2 weeks  
**Dependencies:** Command word activation system, relevant spells implemented

### TIER 3: Complex Mechanics (6 rings)

These rings require new subsystems for spell storage, countering, or daily power tracking.

| Ring | Complexity | Priority | Impact Score |
|------|-----------|----------|-------------|
| Ring of Wizardry I–IV | ⭐⭐⭐ | **HIGH** | 12 |
| Ring of Counterspells | ⭐⭐⭐ | Medium | 9 |
| Ring of Spell Storing (minor/std/major) | ⭐⭐⭐⭐ | Medium | 8 |
| Ring of Spell Turning | ⭐⭐⭐⭐ | Medium | 8 |
| Ring of Shooting Stars | ⭐⭐⭐⭐ | Low | 4 |
| Ring of Friend Shield | ⭐⭐⭐ | Low | 6 |

**Est. Time:** 2–3 weeks  
**Dependencies:** Spell slot system modification, spell storage system, daily use tracking

### TIER 4: Legendary Rings (4 rings)

The most powerful and complex rings requiring multiple new systems.

| Ring | Complexity | Priority | Impact Score |
|------|-----------|----------|-------------|
| Ring of Three Wishes | ⭐⭐⭐⭐⭐ | Medium | 5 |
| Ring of Djinni Calling | ⭐⭐⭐⭐⭐ | Low | 5 |
| Ring of Elemental Command (×4) | ⭐⭐⭐⭐⭐ | Low | 5 |

**Est. Time:** 2–3 weeks  
**Dependencies:** Wish system (exists!), summoning system, elemental dominance system

---

## Implementation Roadmap

### Phase 1: Ring Foundation (Week 1–2)

**Goal:** Get any ring working in the game.

1. **Ring Framework** (2–3 days)
   - Add `IsRing` bool + `RingId` string + `RingBonusValue` int to ItemData
   - Create `RingFactory.cs` — generates ItemData for each ring type
   - Create `RingDatabase.cs` — registry of all ring definitions (like StaffDatabase)
   - Add ring equip/unequip hooks in `Inventory.RecalculateStats()`
   - Add `RingEffectType` enum (Passive, CommandWord, Charged, SpellStorage)
   
2. **Simple Passive Rings** (2–3 days)
   - Ring of Protection +1 through +5
   - Ring of Energy Resistance (all 15 variants)
   - Ring of Evasion
   - Ring of Freedom of Movement
   - Ring of Force Shield

3. **Placeholder Rings** (1 day)
   - Create all remaining rings as ItemData entries with descriptive tooltips
   - Mark unimplemented abilities as stubs (like staff system)

### Phase 2: Active Rings (Week 3–4)

**Goal:** Rings that cast spells or grant abilities.

1. **Command Word Activation System** (2 days)
   - "Use Ring" action in combat UI
   - Standard action consumption
   - Ring-specific activation UI

2. **Spell-Like Rings** (3–4 days)
   - Ring of Invisibility (cast Invisibility)
   - Ring of Blinking (cast Blink)
   - Ring of Telekinesis (cast Telekinesis)
   - Ring of Animal Friendship (cast Charm Animal)
   - Ring of Chameleon Power (+10 Hide + Disguise Self)

3. **Charged Rings** (2–3 days)
   - Ring of Ram (50 charges, ranged force + bull rush)
   - Ring of Regeneration (1 HP/round — fast healing)

### Phase 3: Complex Rings (Week 5–7)

**Goal:** Rings requiring new subsystems.

1. **Spell Slot Doubling** (3 days)
   - Ring of Wizardry I–IV
   - Hook into spell preparation/slot system
   - Only affects arcane base slots

2. **Spell Storage System** (4–5 days)
   - Ring of Spell Storing (minor/standard/major)
   - UI to store spells into ring
   - UI to cast from ring
   - Capacity tracking

3. **Counter/Turning Systems** (3–4 days)
   - Ring of Counterspells (store + auto-counter)
   - Ring of Spell Turning (daily reflection)

4. **Multi-Ability Rings** (2–3 days)
   - Ring of Shooting Stars (daily abilities)
   - Ring of Friend Shield (paired ring)

### Phase 4: Legendary Rings (Week 8–10)

**Goal:** Most powerful rings with complex interactions.

1. **Ring of Three Wishes** (2 days)
   - Leverage existing WishExecutor + WishUI
   - 3 charges, no XP cost
   - Becomes nonmagical at 0

2. **Ring of Djinni Calling** (4–5 days)
   - Summoning system for named creature
   - Djinni stat block
   - Duration tracking
   - Death = ring destruction

3. **Rings of Elemental Command** (5–7 days)
   - 4 variants with shared framework
   - Activation condition (slay elemental)
   - Multiple spell-like abilities with varied limits
   - Elemental dominance bonuses
   - Opposing element weakness

---

## Dependency Tree

```
Ring Equipment Framework (Foundation — REQUIRED FIRST)
├── ItemData: IsRing, RingId, RingBonusValue, RingEffectType
├── RingDatabase.cs (all ring definitions)
├── RingFactory.cs (item generation)
├── Inventory.RecalculateStats() ring processing
└── UI: ring equip/unequip in PreCombatInventoryUI
    │
    ├── Passive Bonus Rings (Tier 1)
    │   ├── Ring of Protection (uses existing DeflectionBonus)
    │   ├── Ring of Energy Resistance (uses existing DamageResistances)
    │   ├── Ring of Evasion (uses existing HasEvasion)
    │   ├── Ring of Freedom of Movement (uses existing FoM effect)
    │   └── Ring of Force Shield (toggle shield bonus)
    │
    ├── Command Word Activation System
    │   ├── Spell-Like Rings (Tier 2)
    │   │   ├── Ring of Invisibility (needs Invisibility spell ✅)
    │   │   ├── Ring of Blinking (needs Blink spell ✅)
    │   │   ├── Ring of Telekinesis (needs Telekinesis spell ✅)
    │   │   └── Ring of Animal Friendship (needs Charm Animal)
    │   │
    │   └── Charged Rings (Tier 2)
    │       ├── Ring of Ram (needs ranged touch + bull rush)
    │       └── Ring of Three Wishes (needs Wish ✅)
    │
    ├── Spell Slot Modification System
    │   └── Ring of Wizardry I–IV (Tier 3)
    │
    ├── Spell Storage System
    │   ├── Ring of Spell Storing (Tier 3)
    │   └── Ring of Counterspells (Tier 3)
    │
    ├── Daily Use Tracking System
    │   ├── Ring of Spell Turning (Tier 3)
    │   └── Ring of Shooting Stars (Tier 3)
    │
    └── Summoning + Elemental Dominance Systems
        ├── Ring of Djinni Calling (Tier 4)
        └── Ring of Elemental Command ×4 (Tier 4)
```

---

## Existing System Compatibility

| System | Status | Rings That Use It |
|--------|--------|-------------------|
| DeflectionBonus (AC) | ✅ Exists | Ring of Protection |
| DamageResistances | ✅ Exists | Ring of Energy Resistance |
| HasEvasion | ✅ Exists | Ring of Evasion |
| Freedom of Movement | ✅ Exists | Ring of Freedom of Movement |
| Invisibility spell | ✅ Exists | Ring of Invisibility |
| Blink spell | ✅ Exists | Ring of Blinking |
| Telekinesis spell | ✅ Exists | Ring of Telekinesis |
| Wish system | ✅ Exists | Ring of Three Wishes |
| Spell Turning spell | ✅ Exists | Ring of Spell Turning |
| Wand charge system | ✅ Exists (template) | Ring of Ram |
| Staff charge system | ✅ Exists (template) | Ring of Three Wishes |
| Spell slot system | ✅ Exists | Ring of Wizardry |
| Summoning system | ❌ Needed | Ring of Djinni Calling |
| Elemental dominance | ❌ Needed | Ring of Elemental Command |
| Command word UI | ❌ Needed | All active rings |
| Spell storage UI | ❌ Needed | Ring of Spell Storing, Counterspells |
| Ring-specific RecalcStats | ❌ Needed | All passive rings |

---

*Document generated 2026-05-24. Core DMG 3.5e rules only.*

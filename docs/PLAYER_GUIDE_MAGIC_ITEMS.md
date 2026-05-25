# 🗡️ D&D 3.5e Prototype — Magic Items Player Guide

> **Version 1.0** · Covers all implemented magic item systems  
> *Based on the D&D 3.5e Player's Handbook, Dungeon Master's Guide, and SRD*

---

## Table of Contents

- [Getting Started](#getting-started)
- [Inventory & Equipment Basics](#inventory--equipment-basics)
  - [Opening the Inventory](#opening-the-inventory)
  - [Equipment Slots](#equipment-slots)
  - [Equipping & Unequipping Items](#equipping--unequipping-items)
  - [Slotless Items](#slotless-items)
  - [Pre-Combat Inventory Management](#pre-combat-inventory-management)
- [Item Quality & Color Coding](#item-quality--color-coding)
- [Rings](#rings)
  - [The Two-Ring Rule](#the-two-ring-rule)
  - [Passive Rings](#passive-rings)
  - [Active Rings](#active-rings)
  - [Charge-Based Rings](#charge-based-rings)
  - [Ring Use Tracking & Resting](#ring-use-tracking--resting)
- [Rods](#rods)
  - [Metamagic Rods](#metamagic-rods)
  - [Using a Metamagic Rod Step-by-Step](#using-a-metamagic-rod-step-by-step)
  - [Rod Power Levels](#rod-power-levels)
  - [Combat & Utility Rods](#combat--utility-rods)
  - [Legendary Rods](#legendary-rods)
- [Staves](#staves)
  - [Activating a Staff](#activating-a-staff)
  - [The Spell Selection Panel](#the-spell-selection-panel)
  - [Charges & Expended Staves](#charges--expended-staves)
  - [Use Magic Device Fallback](#use-magic-device-fallback)
- [Metamagic System](#metamagic-system)
  - [Metamagic Feats Overview](#metamagic-feats-overview)
  - [Prepared vs. Spontaneous Casters](#prepared-vs-spontaneous-casters)
  - [Rod-Assisted Metamagic (No Slot Increase)](#rod-assisted-metamagic-no-slot-increase)
  - [Stacking Metamagic](#stacking-metamagic)
- [Wondrous Items](#wondrous-items)
  - [Activation Types](#activation-types)
  - [Passive & Continuous Items](#passive--continuous-items)
  - [Command Word Items](#command-word-items)
  - [Use-Activated Items](#use-activated-items)
  - [Consumable Items](#consumable-items)
  - [Summoning Items](#summoning-items)
  - [Spell-Like Ability Items](#spell-like-ability-items)
  - [Daily, Weekly & Monthly Limits](#daily-weekly--monthly-limits)
- [Specific Magic Items](#specific-magic-items)
  - [How Specific Items Work](#how-specific-items-work)
  - [Notable Weapons](#notable-weapons)
  - [Notable Armor & Shields](#notable-armor--shields)
- [Special Systems](#special-systems)
  - [Apparatus of Kwalish](#apparatus-of-kwalish)
  - [Construct Guardians](#construct-guardians)
  - [Planar Travel](#planar-travel)
  - [Cubic Gate](#cubic-gate)
- [Quick Reference](#quick-reference)
  - [Hotkeys & Shortcuts](#hotkeys--shortcuts)
  - [Bonus Stacking Rules](#bonus-stacking-rules)
  - [Troubleshooting](#troubleshooting)

---

## Getting Started

This prototype implements over **300 magic items** from the D&D 3.5e Dungeon Master's Guide with ~95% fidelity to the source rules. Magic items are divided into several categories:

| Category | Count | Examples |
|:---------|:-----:|:--------|
| **Rings** | 33 | Ring of Protection, Ring of Invisibility, Ring of Spell Turning |
| **Rods** | 36 | Metamagic Rods (21), Combat Rods (7), Utility (5), Legendary (3) |
| **Staves** | 20 | Staff of Fire, Staff of Power, Staff of the Woodlands |
| **Wondrous Items** | 164+ | Boots of Speed, Bag of Holding, Figurines of Wondrous Power |
| **Specific Weapons/Armor** | 54 | Holy Avenger, Oathbow, Frost Brand, Mithral Full Plate of Speed |

All items follow D&D 3.5e stacking rules, activation mechanics, and use limitations.

---

## Inventory & Equipment Basics

### Opening the Inventory

Press **`I`** during your active character's turn to open the Inventory panel. The panel displays:

- **Equipment Slots** (top) — 14 body slots arranged in a 7×2 grid
- **General Inventory** (bottom) — 20 slots in a 5×4 grid for carried items
- **Slotless Items** — A separate section for items that don't occupy body slots
- **Character Stats** — Name, AC, HP, and current equipment bonuses

Press **`I`** again or **`Esc`** to close the panel.

### Equipment Slots

Your character has **14 body equipment slots**, following the D&D 3.5e standard:

| Slot | What Goes Here | Notes |
|:-----|:---------------|:------|
| **Head** | Helmets, headbands, circlets | Headband of Intellect, Helm of Brilliance |
| **Face/Eyes** | Goggles, lenses, masks | Eyes of the Eagle, Goggles of Night |
| **Neck** | Amulets, necklaces, periapts | Amulet of Natural Armor, Periapt of Wisdom |
| **Torso** | Vests, shirts, robes | Vest of Resistance, Monk's Belt |
| **Armor** | Armor and robes | Chain Shirt, Full Plate, Bracers of Armor |
| **Waist** | Belts, girdles | Belt of Giant Strength, Girdle of Femininity |
| **Back** | Cloaks, capes, mantles | Cloak of Resistance, Wings of Flying |
| **Wrists** | Bracers, bracelets | Bracers of Archery, Shackles of Compliance |
| **Hands** | Gloves, gauntlets | Gloves of Dexterity, Gauntlets of Ogre Power |
| **Left Ring** | Ring | One of your two ring slots |
| **Right Ring** | Ring | One of your two ring slots |
| **Feet** | Boots, sandals, slippers | Boots of Speed, Slippers of Spider Climbing |
| **Left Hand** | Weapon, shield, rod | Off-hand items |
| **Right Hand** | Weapon, shield, rod | Main-hand items |

### Equipping & Unequipping Items

**To equip an item:**
1. **Click** the item in your General Inventory — it becomes highlighted/selected
2. **Click** the appropriate Equipment Slot — the item moves into that slot
3. If the slot is already occupied, the items **swap** automatically

**To unequip an item:**
- **Click** an occupied Equipment Slot — the item returns to the first empty General Inventory slot

**To use a consumable:**
- **Click** the consumable once to select it, then **click it again** to use it

**To drop an item:**
- **Right-click** an item in General Inventory → select **"Drop"** from the context menu

> ⚠️ **Two-Handed Weapons:** Equipping a two-handed weapon (greatsword, greataxe, longbow, etc.) automatically unequips whatever is in your off-hand slot. The displaced item returns to your General Inventory.

### Slotless Items

Some wondrous items don't occupy a body slot — **Ioun Stones**, **Bags of Holding**, and similar items. These use the **Slotless** equipment category.

- You can have up to **10 slotless items** equipped simultaneously
- Slotless items appear in their own section of the Inventory panel
- Equipping/unequipping slotless items works the same as regular equipment
- Passive bonuses from slotless items apply automatically when equipped

### Pre-Combat Inventory Management

Before combat begins, you can access the **Pre-Combat Inventory UI** which offers additional features:

- **Stash Browsing** — Browse and transfer items from the party stash
- **Character Selection** — Switch between party members to manage their gear
- **Drag-and-Drop** — Drag items between characters or to/from the stash
- **Right-Click Quick Actions** — Fast equip, transfer, or examine items
- **Validation Feedback** — The UI warns you about invalid equipment combinations

---

## Item Quality & Color Coding

Items in your inventory are color-coded by quality:

| Color | Quality | Meaning |
|:------|:--------|:--------|
| ⬜ White | Standard | Normal, non-magical equipment |
| 🟦 Blue | Masterwork | +1 attack (weapons) or −1 ACP (armor) |
| 🟪 Purple | Special Material | Mithral, Adamantine, Cold Iron, etc. |
| 🟨 Gold | Magical | Enchanted with a magic enhancement bonus |

**Stack counts** appear as `×#` for stackable items (arrows, bolts). **Charge counts** display as `n/max` for wands and similar items. Ammunition quantities show the current count.

---

## Rings

### The Two-Ring Rule

Per D&D 3.5e rules, a character may wear **at most two magic rings** at one time — one on each hand (**Left Ring** and **Right Ring** slots). This limit is hard-coded and cannot be bypassed.

If both ring slots are full and you want to equip a new ring, you must first unequip one of your current rings.

### Passive Rings

Many rings provide constant bonuses simply by being worn:

| Ring | Effect |
|:-----|:-------|
| Ring of Protection +1/+2/+3/+4/+5 | Deflection bonus to AC |
| Ring of Sustenance | No need for food/water; 2-hour sleep |
| Ring of Swimming | +5 competence bonus to Swim |
| Ring of Climbing | +5 competence bonus to Climb |
| Ring of Jumping | +5 competence bonus to Jump |
| Ring of Feather Falling | Constant *feather fall* effect |
| Ring of Mind Shielding | Immune to *detect thoughts*, *discern lies* |
| Ring of Counterspells | Auto-counterspell one stored spell |
| Ring of Freedom of Movement | Continuous *freedom of movement* |
| Ring of Evasion | Evasion ability (as rogue) |
| Ring of Force Shield | +2 shield bonus to AC (no ACP) |

**Deflection bonus stacking:** If you wear two Rings of Protection, only the **highest bonus** applies (they don't stack). This follows D&D 3.5e's "same type doesn't stack" rule.

### Active Rings

Some rings have abilities you activate during your turn. These cost an action and may have daily/weekly limits:

| Ring | Activation | Limit | Effect |
|:-----|:-----------|:------|:-------|
| Ring of Invisibility | Command word | At will | *Invisibility* on self |
| Ring of Blinking | Command word | At will | *Blink* effect |
| Ring of Telekinesis | Command word | At will | *Telekinesis* (up to 5 lbs/level) |
| Ring of Animal Friendship | Standard action | 1/day | Charm animal (Will DC 17) |
| Ring of the Ram | Standard action | Charged | Force ram (1–3 charges per use) |
| Ring of X-Ray Vision | Standard action | Per-rest | See through walls (Con damage risk) |
| Ring of Shooting Stars | Varies | Daily | Ball lightning, faerie fire, spark shower |
| Ring of Spell Turning | Automatic | Charged | Reflects 1d4+6 spell levels (on equip) |
| Ring of Djinni Calling | Standard action | 1/day | Summon djinni servant |

**Multi-ability rings** (like Ring of Shooting Stars) present a **selection panel** when activated — choose which ability to use from the list.

### Charge-Based Rings

Some rings use a **charge pool** instead of daily uses:

- **Ring of the Ram:** 50 charges total. Each use costs 1–3 charges (more charges = more damage). Regenerates **1d10 charges per day** on rest.
- **Ring of Spell Turning:** Absorbs 1d4+6 spell levels when first equipped. Does **not** regenerate — once spent, the pool is empty until re-equipped.

Charge pools display in the item tooltip. Monitor your charges carefully!

### Ring Use Tracking & Resting

- **Daily uses** reset when you rest
- **Weekly uses** reset every 7 rests
- **Charge regeneration** occurs on rest (Ring of the Ram: 1d10/day)
- **X-Ray Vision** uses are tracked per rest; excessive use causes Constitution damage

---

## Rods

Rods are held items — they occupy a **hand slot** (Left Hand or Right Hand), not a worn slot. You must be holding a rod to use it.

### Metamagic Rods

The most common rods are **Metamagic Rods**, which let you apply metamagic effects to your spells **without increasing the spell slot level**. This is enormously powerful for spellcasters.

There are **21 metamagic rods** — 7 metamagic types × 3 power levels:

| Metamagic Type | Effect on Spell |
|:---------------|:----------------|
| **Empower** | Variable numeric effects increased by 50% |
| **Enlarge** | Range doubled |
| **Extend** | Duration doubled |
| **Maximize** | All variable numeric effects at maximum value |
| **Quicken** | Cast as a free action instead of standard |
| **Silent** | Cast without verbal components |
| **Still** | Cast without somatic components |

### Using a Metamagic Rod Step-by-Step

1. **Equip the rod** in a hand slot (Left or Right Hand)
2. **Begin casting a spell** normally during your turn
3. When prompted, **select the metamagic rod** to apply its effect
4. The system validates the spell level against the rod's maximum
5. If valid, the metamagic is applied with **zero slot increase**
6. The rod consumes **1 daily use** (standard: 3 uses/day)

> 💡 **Key Benefit:** Normally, applying Empower to a 3rd-level spell would require a 5th-level slot (+2 levels). With a rod, it still uses a 3rd-level slot!

### Rod Power Levels

Each metamagic rod comes in three power levels that determine the **maximum base spell level** it can affect:

| Power Level | Max Spell Level | Example |
|:------------|:----------------|:--------|
| **Lesser** | 3rd level | Lesser Rod of Empower: works on spells level 1–3 |
| **Normal** | 6th level | Rod of Empower: works on spells level 1–6 |
| **Greater** | 9th level | Greater Rod of Empower: works on any spell |

All metamagic rods have **3 uses per day** by default. Daily uses reset at dawn (on rest).

### Combat & Utility Rods

Beyond metamagic, several rods have direct combat or utility functions:

| Rod | Category | Effect |
|:----|:---------|:-------|
| Rod of Absorption | Combat | Absorb spells, convert to own casting |
| Rod of Cancellation | Combat | Permanently drain a magic item's enchantment |
| Rod of Enemy Detection | Utility | Detect nearest enemies (60 ft) |
| Rod of Flailing | Combat | +3 flail, auto–Two-Weapon Fighting |
| Rod of Lordly Might | Legendary | Transforms into 6 different weapons; multiple powers |
| Rod of Negation | Combat | Negate another rod's power |
| Rod of Wonder | Utility | Random magical effect each use |

### Legendary Rods

Three rods are classified as **Legendary** — exceptionally powerful artifacts:

- **Rod of Lordly Might** — Morphing weapon with 6 forms (battleaxe, flaming lance, climbing pole, etc.)
- **Rod of Absorption** — Absorb incoming spells and convert their energy to fuel your own casting
- **Rod of Security** — Create an extradimensional safe haven

---

## Staves

Staves are powerful spellcasting tools that contain **multiple spells** and use a **shared charge pool**. Each staff starts with **50 charges** and cannot be recharged in the current implementation (per core DMG 3.5e).

### Activating a Staff

To cast a spell from a staff, you must meet **one** of these requirements:

1. **Class Match** — The spell appears on your class's spell list (Wizard, Cleric, Druid, etc.)
2. **Magic Domain** — You're a Cleric with the Magic Domain (grants access to all staff spells)
3. **Use Magic Device** — Make a UMD check (DC 20) to activate the staff regardless of class

Staves use **Spell Trigger** activation — no material components or focuses needed.

### The Spell Selection Panel

When you activate a staff during combat:

1. The **Spell Selection Panel** opens, showing all spells stored in the staff
2. Each spell displays:
   - **Spell name and level**
   - **Charge cost** (varies by spell — higher-level spells cost more charges)
   - **Caster Level (CL)** — the staff's built-in caster level
   - **Save DC** — calculated as `10 + spell level + (spell level ÷ 2)`
3. Spells with **insufficient charges** are **grayed out** and unselectable
4. **Click** the spell you want to cast

> 📋 **Example — Staff of Fire (CL 8, 50 charges):**  
> - *Burning Hands* (1st) — 1 charge  
> - *Fireball* (3rd) — 1 charge  
> - *Wall of Fire* (4th) — 1 charge  

### Charges & Expended Staves

- Staves start with **50 charges maximum**
- Each spell cast consumes charges (shown in the selection panel)
- Current charges display in the item tooltip as `charges: n/50`
- When a staff reaches **0 charges**, it becomes **expended** and reverts to a **non-magical quarterstaff**
- **Charges cannot be restored** in the current implementation

> ⚠️ **Budget your charges carefully!** A Staff of Power with 50 charges might seem generous, but casting its high-level spells can drain it quickly.

### Use Magic Device Fallback

If your character doesn't have the matching class for a staff's spells:

1. Attempt to activate the staff
2. The system automatically checks for **Use Magic Device** skill
3. A **DC 20 check** is rolled: `d20 + UMD skill bonus ≥ 20`
4. On success, the staff activates normally for this use
5. On failure, you've wasted your action — try again next turn

---

## Metamagic System

### Metamagic Feats Overview

The prototype implements all **9 metamagic feats** from the PHB:

| Feat | Slot Increase | Effect |
|:-----|:-------------:|:-------|
| **Empower Spell** | +2 | Variable numeric effects ×1.5 |
| **Enlarge Spell** | +1 | Double the spell's range |
| **Extend Spell** | +1 | Double the spell's duration |
| **Heighten Spell** | Variable | Increase spell's effective level (for DC) |
| **Maximize Spell** | +3 | All variable numeric effects at maximum |
| **Quicken Spell** | +4 | Cast as free action (1 quickened spell/round) |
| **Silent Spell** | +1 | Remove verbal component |
| **Still Spell** | +1 | Remove somatic component |
| **Widen Spell** | +3 | Double the spell's area |

**Applicability restrictions:**
- **Empower/Maximize** — Only work on spells with numeric effects (damage/healing)
- **Enlarge** — Only works on spells with range > 0 (not Touch or Personal)
- **Extend** — Doesn't apply to Instantaneous, Permanent, or Concentration durations
- **Widen** — Only works on spells with an area of effect
- **Heighten** — You choose the new level; DC adjusts accordingly

### Prepared vs. Spontaneous Casters

The metamagic workflow differs based on your casting type:

#### Prepared Casters (Wizard, Cleric, Druid, Paladin, Ranger)
- Metamagic is chosen **during spell preparation** (before combat)
- The modified spell occupies a **higher-level slot** (base level + slot increase)
- **No casting time change** — still cast as the spell's normal casting time
- Example: Empowered *Fireball* (3rd + 2 = **5th-level slot**)

#### Spontaneous Casters (Sorcerer, Bard)
- Metamagic is applied **at casting time**
- The spell uses a **higher-level slot** (same as prepared)
- Casting time changes to **full-round action** (instead of standard action)
- **Exception:** Quicken Spell always makes the spell a **free action**

### Rod-Assisted Metamagic (No Slot Increase)

When you use a **Metamagic Rod** to apply a feat:

- The metamagic effect is applied normally
- **The spell slot level does NOT increase** — this is the rod's major benefit
- Rod-sourced metamagic is tracked separately from feat-sourced metamagic
- You can combine a rod metamagic with a feat metamagic on the same spell
- The effective spell level cap of **9th level** still applies to the total

> 💡 **Power Combo:** Use a Rod of Quicken on a *Fireball* — it becomes a free-action Fireball using only a 3rd-level slot!

### Stacking Metamagic

You can apply **multiple metamagic effects** to a single spell:

- Each metamagic feat can only be applied **once** per spell (no stacking the same feat)
- Slot increases from feats are **additive**: Empowered + Extended *Fireball* = 3 + 2 + 1 = **6th-level slot**
- Rod-sourced metamagic contributes **zero** to the slot increase
- The total effective spell level (after all adjustments) **cannot exceed 9th level**
- If a combination would exceed 9th level, the system rejects it with an error message

---

## Wondrous Items

Wondrous Items are the largest and most diverse magic item category, spanning everything from stat-boosting headbands to flying carpets.

### Activation Types

Every wondrous item has one of four activation types:

| Type | How It Works | Player Action |
|:-----|:-------------|:--------------|
| **Passive** | Always active when equipped | None — equip and forget |
| **Continuous** | Constant ongoing effect | None — always on while worn |
| **Command Word** | Speak a word to activate | Click item → speak command |
| **Use-Activated** | Physical interaction triggers it | Click item → use it |

### Passive & Continuous Items

These items work automatically once equipped:

**Ability Score Boosters:**
- Headband of Intellect (+2/+4/+6 Int)
- Amulet of Health (+2/+4/+6 Con)
- Gloves of Dexterity (+2/+4/+6 Dex)
- Belt of Giant Strength (+4/+6 Str)
- Periapt of Wisdom (+2/+4/+6 Wis)
- Cloak of Charisma (+2/+4/+6 Cha)

**Armor Class:**
- Bracers of Armor +1 through +8
- Amulet of Natural Armor +1 through +5
- Dusty Rose Ioun Stone (+1 insight AC)

**Movement:**
- Boots of Striding and Springing (+10 ft speed, +5 Jump)
- Winged Boots (fly 60 ft, 3×/day)
- Slippers of Spider Climbing (spider climb at will)
- Carpet of Flying (fly at various speeds based on size)

**Storage:**
- Bag of Holding (Type I–IV, various weight/volume capacities)
- Handy Haversack (retrieval as free action)
- Portable Hole (extradimensional 6 ft × 10 ft space)

### Command Word Items

Command word items require you to **activate them** during your turn:

- **Boots of Speed** — Grants haste for up to **10 rounds/day** (rounds don't need to be consecutive)
  - +1 dodge AC, +1 attack bonus, +30 ft speed, extra attack
  - Rounds tick down at the start of each of your turns while active
- **Horn of Valhalla** — Summon barbarian warriors (1×/week, varies by horn type)
- **Figurines of Wondrous Power** — Summon an animal companion (weekly/monthly limits)

### Use-Activated Items

These items activate through physical use or interaction:

- **Apparatus of Kwalish** — Operate levers to control the iron lobster vehicle
- **Elemental Gems** — Crush to summon a Large elemental (single use)
- **Bag of Tricks** — Pull out a fuzzy ball that becomes a random animal

### Consumable Items

Some wondrous items are **single-use** (marked internally with `usesPerDay = -1`):

| Item | Effect |
|:-----|:-------|
| Bead of Force | Throw to create a 10 ft *resilient sphere* (5d6 damage) |
| Elemental Gem (4 types) | Crush to summon Large Air/Earth/Fire/Water Elemental |
| Necklace of Fireballs (beads) | Detach and throw beads for *fireball* effects |
| Dust of Disappearance | Sprinkle on self for *greater invisibility* (2d6 rounds) |
| Dust of Dryness | Absorbs water or creates a water geyser |

Once used, consumable items are **permanently expended**.

### Summoning Items

Several wondrous items can **summon creatures** to fight alongside you:

| Item | Creature | Duration | Limit | Mountable? |
|:-----|:---------|:---------|:------|:-----------|
| Figurine: Bronze Griffon | Griffon | 6 hours | 1×/week | ✅ Yes |
| Figurine: Ebony Fly | Giant fly | 12 hours | 3×/week | ✅ Yes |
| Figurine: Golden Lions | 2 Lions | 1 hour | 1×/week | ❌ No |
| Figurine: Ivory Goats | Varies | Varies | 1×/month | ✅ (travel) |
| Figurine: Marble Elephant | Elephant | 24 hours | 1×/month | ✅ Yes |
| Figurine: Obsidian Steed | Nightmare | 24 hours | 1×/week | ✅ Yes |
| Figurine: Onyx Dog | Dog | 6 hours | 1×/week | ❌ No |
| Figurine: Serpentine Owl | Owl/Giant Owl | 8 hours | 1×/week | ✅ (giant) |
| Figurine: Silver Raven | Raven | 24 hours | At will | ❌ No |
| Bag of Tricks | Random animal | 10 min | Unlimited | ❌ No |
| Elemental Gem | Large Elemental | 1 round/lvl | Single use | ❌ No |

Summoned creatures act as allies under your control. Mountable creatures can carry you, replacing your movement speed with theirs.

### Spell-Like Ability Items

Some wondrous items grant spell-like abilities with their own daily use limits:

- Abilities are listed in the item tooltip with remaining uses
- Uses per day track individually (e.g., "Teleport 1/day" and "Plane Shift 1/day" are separate)
- Uses display as a comma-separated count in the tooltip
- All spell-like ability uses reset on rest

### Daily, Weekly & Monthly Limits

Wondrous items use three tiers of use-limit tracking:

| Reset Period | When It Resets | Examples |
|:-------------|:---------------|:---------|
| **Daily** | After any rest | Boots of Speed (10 rounds), most command word items |
| **Weekly** | Every 7 rests | Figurines of Wondrous Power, Cubic Gate sides |
| **Monthly** | Every 30 rests | Ivory Goats, Marble Elephant |

> ⚠️ **Rest is required** to reset uses. Daily uses don't reset on their own — you must actually take a rest through the game's rest mechanic.

---

## Specific Magic Items

### How Specific Items Work

Specific Magic Items are unique named items from the DMG with **custom behavior scripts** that trigger automatically during combat. Unlike generic +1/+2 weapons, these items have special abilities that activate based on game events.

Each specific item has a **Behavior** system that hooks into:
- **OnEquip / OnUnequip** — Setup or tear down when the item is worn
- **Attack Rolls** — Modify attack bonus against certain targets
- **Damage Rolls** — Add bonus damage, change damage type, or trigger effects
- **On Hit** — Trigger effects when you successfully hit (life drain, smiting, etc.)
- **On Critical** — Special effects on critical hits (instakill, level drain, etc.)
- **Defensive** — Modify AC, grant resistances, or reflect attacks
- **Round/Turn Start** — Per-round effects (aura, regeneration, etc.)

### Notable Weapons

#### ⚔️ Holy Avenger
*+2 cold iron longsword (becomes +5 in paladin hands)*

| Condition | Bonus |
|:----------|:------|
| Non-paladin | +2 enhancement, +2d6 holy damage vs evil |
| Paladin | +5 enhancement, +2d6 holy vs evil, SR (5 + paladin level) in 5 ft aura, *greater dispel magic* 1/round free action |

The Holy Avenger is the ultimate paladin weapon. Its SR aura protects nearby allies too!

#### 🏹 Oathbow
*+2 composite longbow (+2 Str)*

- **Once per day**, declare a **sworn enemy** (speak the oath)
- Against the sworn enemy: enhancement becomes **+5**, deal **+2d6 bonus damage** per hit
- Against all others while oath is active: the bow functions as **masterwork only** with a **−1 penalty**
- The oath persists until the enemy is slain (or 7 days pass)
- Only **one sworn enemy** at a time — you can't re-oath until the current one resolves

> 💡 **Tactical tip:** Declare your oath against the toughest enemy in a fight to maximize the +5/+2d6 bonus, but be aware of the penalties against everyone else!

#### Other Notable Weapons

| Weapon | Key Mechanic |
|:-------|:-------------|
| **Frost Brand** | +3 cold damage, extinguish fires, cold resistance 10, anti-fire-creature bonuses |
| **Sun Blade** | +2 bastard sword / +4 vs undead, usable as short sword, *daylight* on command |
| **Dwarven Thrower** | +2 warhammer / +3 thrown (returning), +2d8 vs giants (dwarf only) |
| **Luck Blade** | +2 short sword, +1 luck to saves, 1d4–1 *wishes* |
| **Nine Lives Stealer** | +2 longsword, instakill on crit (Fort DC 20 or die), 9 charges |
| **Sword of the Planes** | +1 longsword, +2 on Astral/+3 on other planes |
| **Sword of Life Stealing** | +2 longsword, 1d6 negative energy on crit (heals wielder) |
| **Mace of Smiting** | +3 heavy mace, +5 vs constructs, instakill constructs on crit |
| **Mace of Terror** | +2 heavy mace, *fear* aura 3/day (Will DC 16, 30 ft cone) |
| **Rapier of Puncturing** | +2 rapier, 1d6 Con damage on crit (Fort DC 17 negates) |
| **Sylvan Scimitar** | +3 scimitar in forests, *druid*-enhanced, entangle on crit |
| **Shatterspike** | +1 longsword, sunder bonus +4, auto-destroy items hardness ≤ 10 |
| **Javelin of Lightning** | 5d6 lightning bolt on throw (single use per throw, reforms) |
| **Slaying Arrow** | Instakill designated creature type (Fort DC 20 or die) |
| **Sleep Arrow** | *Sleep* effect on hit (Will DC 11 or unconscious 1d3 minutes) |

### Notable Armor & Shields

| Armor/Shield | Key Mechanic |
|:-------------|:-------------|
| **Mithral Full Plate of Speed** | +1 full plate (max dex +3, ACP −3), *haste* 10 rounds/day |
| **Demon Armor** | +4 full plate, claw attacks 1d10+1, *negative energy contagion* |
| **Breastplate of Command** | +2 breastplate, +2 Charisma for leadership, *command* 1/day |
| **Armor of Rage** | +1 breastplate, *rage* 1/day (as barbarian), −2 AC during rage |
| **Banded Mail of Luck** | +3 banded mail, force reroll 1 attack/day |
| **Animated Shield** | +2 shield that floats and defends on its own (free both hands) |
| **Absorbing Shield** | +1 heavy shield, *disintegrate* touch 1/day, absorb *disintegrate* |
| **Lion's Shield** | +2 heavy shield, lion bite 2d6 3/day |
| **Winged Shield** | +3 heavy shield, *fly* 1/day (5 minutes) |
| **Caster's Shield** | +1 light shield, stores one spell (up to 3rd level) |
| **Spellguard Shield** | +1 light shield, +2 bonus to saves vs spells |

---

## Special Systems

### Apparatus of Kwalish

The **Apparatus of Kwalish** is a Large iron vehicle shaped like a lobster, operated by **10 levers** from inside. It holds **2 Medium creatures**, has **AC 20, HP 200, Hardness 10**, and a sealed air supply lasting **10 hours**.

#### Lever Controls

| Lever | Up Position | Down Position |
|:-----:|:------------|:-------------|
| **1** | Legs/tentacles extend — walk 10 ft/round | Retract legs |
| **2** | Forward window unshutters | Forward window shutters |
| **3** | Side windows unshutter (2) | Side windows shutter |
| **4** | Open pincers extend — pincer attack +10, 2d6 | Pincers retract |
| **5** | Open forward hatch | Close forward hatch |
| **6** | Slow swim forward — 30 ft/round | Slow swim backward — 30 ft/round |
| **7** | Fast swim forward — 200 ft/round | Turn left 90° |
| **8** | Antenna light (30 ft radius) | Antenna light off |
| **9** | — | Turn right 90° |
| **10** | — | Descend 200 ft/round (underwater) |

> ⚠️ **Air Supply:** When the apparatus is sealed (hatches closed), the air supply lasts 10 hours for 2 occupants. Monitor this in extended underwater expeditions!

### Construct Guardians

Two items create autonomous **construct companions** that can guard areas, patrol, and fight:

#### Iron Cobra
- **Type:** Tiny construct · **AC 20** · **HP 30** · Fast Healing 3
- **Attack:** Bite +10, 1d3 + poison (Fort DC 16, 1d6 Con damage)
- **Behavior:** Autonomous AI — patrols a 30 ft guard radius
  - Detects hidden enemies
  - Attacks intruders automatically
  - Returns to patrol when threats are eliminated
- **Activation:** Command word (deploy/recall)
- **Cost:** 80,000 gp

#### Stone Horse
Two variants are available:

| Variant | Speed | AC | HP | Str | Best For |
|:--------|:-----:|:--:|:--:|:---:|:---------|
| **Courser** (10,000 gp) | 50 ft | 14 | 30 | 16 | Fast travel, pursuit |
| **Destrier** (14,800 gp) | 40 ft | 16 | 45 | 20 | Combat mount, durability |

- Activates on command word (stone statuette → living horse)
- **Construct traits:** Does not eat, sleep, or tire
- Functions as a combat mount — grants rider its movement speed
- Reverts to stone on command or when reduced to 0 HP

### Planar Travel

The prototype implements a full **Planar Travel System** supporting **26 planes of existence** from the DMG:

#### The Planes

| Category | Planes |
|:---------|:-------|
| **Material** | Material Plane |
| **Transitive** | Astral, Ethereal, Shadow |
| **Inner (Elemental)** | Air, Earth, Fire, Water |
| **Inner (Energy)** | Positive Energy, Negative Energy |
| **Outer (Good)** | Mount Celestia, Bytopia, Arcadia, Elysium, Beastlands, Arborea |
| **Outer (Neutral)** | Mechanus, Acheron, Outlands, Limbo |
| **Outer (Evil)** | Nine Hells, Gehenna, Gray Waste, Abyss, Carceri, Pandemonium |

Each plane has unique **environmental effects** including:
- **Gravity** — Normal, Heavy, Light, None, or Subjective Directional
- **Time Flow** — Normal, accelerated, or decelerated relative to Material Plane
- **Elemental/Energy Traits** — Automatic damage, healing, or transformative effects
- **Alignment Traits** — Bonuses or penalties based on your alignment

Items that interact with the Planar Travel system include *Plane Shift* spells, the Cubic Gate, and the Amulet of the Planes.

### Cubic Gate

The **Cubic Gate** is a Major Wondrous Item — a small cube with each of its **6 faces** attuned to a different plane.

- **Activation:** Turn the cube to the desired face and activate
- **Effect:** Opens a *gate* to the attuned plane
- **Limit:** Each face can be used **3 times per week** (tracked individually)
- **Weekly Reset:** Face uses reset every 7 rests

The 6 planes are assigned when the item is created and displayed in the tooltip. Choose your face carefully — once a gate opens, planar environmental effects apply!

---

## Quick Reference

### Hotkeys & Shortcuts

| Key | Action | Context |
|:---:|:-------|:-------|
| **I** | Toggle Inventory panel | During your active turn |
| **C** | Toggle Character Sheet | Any time |
| **K** | Toggle Skills panel | Any time |
| **Esc** | Close current panel / Cancel | Any panel open |
| **Enter** | Confirm action / Loot all | Loot collection UI |
| **1–9** | Select numbered option | Bull rush push distance; numbered actions |
| **+/=** | Zoom camera in | Exploration / Combat |
| **−** | Zoom camera out | Exploration / Combat |
| **R** | Reset camera | Exploration / Combat |
| **W/↑** | Pan camera up | Exploration / Combat |
| **S/↓** | Pan camera down | Exploration / Combat |
| **A/←** | Pan camera left | Exploration / Combat |
| **D/→** | Pan camera right | Exploration / Combat |
| **F12** | Toggle Spell Testing panel | Debug/testing mode |

### Inventory Interactions Summary

| Action | How |
|:-------|:----|
| **Equip item** | Click item in inventory → click equipment slot |
| **Unequip item** | Click occupied equipment slot |
| **Swap items** | Click item in inventory → click occupied slot (auto-swaps) |
| **Use consumable** | Click consumable twice (select → use) |
| **Drop item** | Right-click item → "Drop" |
| **Activate ring** | Equip ring → use ring ability from action menu |
| **Activate wondrous item** | Equip item → use from action menu (Command Word / Use-Activated only) |
| **Cast from staff** | Equip staff → activate → select spell from panel |
| **Apply metamagic rod** | Hold rod in hand → cast spell → select rod when prompted |

### Bonus Stacking Rules

The game enforces **D&D 3.5e stacking rules** — bonuses of the same type do **not** stack (highest wins):

| Bonus Type | Example Sources | Stacks? |
|:-----------|:----------------|:-------:|
| Enhancement | +1 Sword, +2 Sword | ❌ Highest only |
| Deflection | Ring of Protection +1, +3 | ❌ Highest only |
| Natural Armor | Amulet of Natural Armor +2, +4 | ❌ Highest only |
| Armor | Bracers of Armor +3, +5 | ❌ Highest only |
| Shield | Shield +1, Shield +2 | ❌ Highest only |
| Resistance | Cloak of Resistance +2, +3 | ❌ Highest only |
| Dodge | Various dodge sources | ✅ All stack |
| Circumstance | Various circumstance bonuses | ✅ All stack |
| Untyped | Ioun stones, misc effects | ✅ All stack |

### Troubleshooting

#### "I can't equip this item"
- **Check the slot:** The item may require a specific slot. Hover over it to see which slot it needs.
- **Slot occupied:** If the target slot has an item, equipping will swap them. Make sure you have room in General Inventory for the swap.
- **Ring limit:** You can only wear 2 rings. Unequip one first.
- **Slotless limit:** Maximum 10 slotless items. Unequip one first.
- **Two-handed conflict:** A two-handed weapon needs both hands. Unequip your off-hand item first, or let the auto-swap handle it.

#### "My staff won't activate"
- **Wrong class:** Your character's class must match one of the staff's spell classes. Try Use Magic Device (DC 20) if you have the skill.
- **No charges:** The staff may be expended (0 charges). Check the tooltip for remaining charges. An expended staff is just a quarterstaff.
- **Not equipped:** The staff must be in a hand slot (Left or Right Hand).

#### "My metamagic rod isn't working"
- **Spell too high level:** Check the rod's power level — Lesser (≤3rd), Normal (≤6th), Greater (≤9th).
- **Out of daily uses:** Metamagic rods have 3 uses/day. Rest to reset.
- **Rod not held:** The rod must be in a hand slot, not just in inventory.
- **Cap exceeded:** The total effective spell level (base + feat metamagic) can't exceed 9th level.

#### "My ring ability won't activate"
- **Out of daily/weekly uses:** Check your ring's remaining uses in the tooltip. Rest to reset daily uses.
- **Out of charges:** Charge-based rings (Ring of the Ram) have finite charges. Check remaining charges.
- **Action economy:** Some ring abilities require a standard action. Make sure you haven't used your action this turn.

#### "My wondrous item doesn't seem to do anything"
- **Passive/Continuous items** work automatically — there's nothing to "activate." Check your character sheet for the stat bonuses.
- **Daily uses spent:** Command Word and Use-Activated items may have daily limits. Rest to reset.
- **Wrong activation type:** Make sure you're using the right method (Command Word requires speaking; Use-Activated requires physical interaction).

#### "My bonuses aren't stacking"
- This is usually correct! D&D 3.5e's stacking rules mean same-type bonuses don't add together. Wearing two Rings of Protection gives you only the **higher** bonus.
- **Dodge and untyped bonuses** are the exceptions — these always stack.
- Check the character sheet to see your total calculated bonuses.

#### "My character's stats changed unexpectedly"
- Stats recalculate automatically every time you equip or unequip an item
- **Armor properties** (Max Dex Bonus, Armor Check Penalty, Arcane Spell Failure) are recalculated accounting for masterwork quality and special materials (mithral, adamantine)
- **Encumbrance** from carried weight can affect movement speed and impose additional ACP

---

*This guide covers the magic item systems as implemented in the current prototype build. For rules questions not covered here, refer to the D&D 3.5e Player's Handbook (Chapter 7) and Dungeon Master's Guide (Chapter 7–8).*

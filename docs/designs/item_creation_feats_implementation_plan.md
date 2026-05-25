# Magic Item Creation Feats — Implementation Plan

> **D&D 3.5e Prototype · Pre-Combat Crafting Workshop**  
> Comprehensive design for all 8 PHB item creation feats  
> Target: `/home/ubuntu/dnd35prototype/Assets/Scripts/Crafting/`

---

## Table of Contents

1. [Overview](#1-overview)
2. [System Architecture](#2-system-architecture)
3. [All 8 Creation Feats — Complete Mechanics](#3-all-8-creation-feats--complete-mechanics)
4. [Cost System](#4-cost-system)
5. [Time System](#5-time-system)
6. [Spell Prerequisite System](#6-spell-prerequisite-system)
7. [Validation System](#7-validation-system)
8. [UI / Menu Design](#8-ui--menu-design)
9. [Crafting Workflow](#9-crafting-workflow)
10. [Item Filtering — What Can Be Crafted](#10-item-filtering--what-can-be-crafted)
11. [Special Requirements](#11-special-requirements)
12. [XP Management](#12-xp-management)
13. [Time Management](#13-time-management)
14. [Integration Points](#14-integration-points)
15. [Data Structures](#15-data-structures)
16. [Implementation Phases](#16-implementation-phases)
17. [Testing Requirements](#17-testing-requirements)

---

## 1. Overview

### What Is Item Creation?

Item creation feats allow spellcasting characters to craft magic items during downtime, spending gold, XP, and time instead of finding items as loot. This is one of the most impactful subsystems in D&D 3.5e because it lets players convert surplus resources into precisely the items they need.

### Why It Matters for the Prototype

| Impact Area | Effect |
|:------------|:-------|
| **Economy** | Players spend gold + XP to create items at **half market price** |
| **Power Level** | Custom item selection is extremely powerful — often considered the strongest feat category |
| **Time Gating** | Crafting costs real in-game days — prevents unlimited creation between encounters |
| **Wizard Identity** | Wizards get item creation feats as bonus feats (levels 5/10/15/20) |
| **Scroll Universality** | Scribe Scroll is **free** for all spellcasters — every caster can make scrolls |

### Core Formula Summary

```
Gold Cost    = Market Price / 2
XP Cost      = Market Price / 25
Time         = 1 day per 1,000 gp of base price (minimum 1 day)
```

### Design Principles

1. **Pre-Combat Only** — Crafting happens in the Pre-Combat Hub menu, never during combat
2. **Immediate Resolution** — Time "fast-forwards"; no real-time waiting
3. **Full Validation** — Check feat, caster level, spells, gold, XP before allowing crafting
4. **Leverage Existing Databases** — All craftable items already exist in our item databases
5. **No Level Loss** — Prevent XP expenditure that would drop the character below their current level

---

## 2. System Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────┐
│                    PRE-COMBAT HUB                       │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌───────────┐  │
│  │ Inventory │ │  Store   │ │Spell Prep│ │  CRAFTING  │  │
│  │   (📦)   │ │  (🏪)   │ │  (🔮)   │ │WORKSHOP(⚒)│  │
│  └──────────┘ └──────────┘ └──────────┘ └─────┬─────┘  │
└───────────────────────────────────────────────┼─────────┘
                                                │
                          ┌─────────────────────▼───────────────────────┐
                          │         CRAFTING WORKSHOP UI                │
                          │  ┌───────────────────────────────────────┐  │
                          │  │  Character Selector (party members)   │  │
                          │  ├───────────────────────────────────────┤  │
                          │  │  Feat Tabs: [Scroll][Potion][Wand]   │  │
                          │  │  [Arms&Armor][Wondrous][Rod]         │  │
                          │  │  [Staff][Ring]                        │  │
                          │  │  (only feats the character has)       │  │
                          │  ├───────────────────────────────────────┤  │
                          │  │  Item Browser (filtered list)         │  │
                          │  │  - Search / sort / category filter    │  │
                          │  │  - Shows: name, cost, time, prereqs  │  │
                          │  ├───────────────────────────────────────┤  │
                          │  │  Cost Preview Panel                   │  │
                          │  │  - Gold: ✅ 2,500 gp (have 5,000)    │  │
                          │  │  - XP:   ✅ 200 XP  (have 45,000)    │  │
                          │  │  - Time: 5 days                       │  │
                          │  │  - Spells: ✅ fireball, ❌ haste      │  │
                          │  │  - CL: ✅ CL 9 (need CL 5)           │  │
                          │  ├───────────────────────────────────────┤  │
                          │  │  [Begin Crafting]  [Cancel]           │  │
                          │  └───────────────────────────────────────┘  │
                          └─────────────────────────────────────────────┘
                                                │
               ┌────────────────────────────────┼────────────────────────────────┐
               │                                │                                │
     ┌─────────▼──────────┐          ┌──────────▼──────────┐          ┌──────────▼──────────┐
     │ CraftingValidator   │          │ CraftingCostCalc    │          │  CraftingTimeCalc   │
     │ - CheckFeat()       │          │ - GoldCost()        │          │  - DaysRequired()   │
     │ - CheckCasterLevel()│          │ - XPCost()          │          │  - AdvanceTime()    │
     │ - CheckSpells()     │          │ - MaterialCost()    │          │  - TriggerResets()  │
     │ - CheckResources()  │          │ - BasePrice()       │          │                     │
     └────────────────────┘          └─────────────────────┘          └─────────────────────┘
               │                                │                                │
               └────────────────────────────────┼────────────────────────────────┘
                                                │
                          ┌─────────────────────▼───────────────────────┐
                          │         CraftingExecutor                     │
                          │  - DeductGold(character, amount)            │
                          │  - DeductXP(character, amount)              │
                          │  - CreateItem(itemTemplate) → ItemData      │
                          │  - AddToInventory(character, item)          │
                          │  - AdvanceDays(count)                       │
                          │  - LogResult(combatLog)                     │
                          └─────────────────────────────────────────────┘
                                                │
                          ┌─────────────────────▼───────────────────────┐
                          │         EXISTING SYSTEMS                     │
                          │  CharacterStats — XP, gold, feats, level    │
                          │  SpellcastingComponent — known spells        │
                          │  ItemDatabase — item templates               │
                          │  RodDatabase/RingDatabase/etc. — categories  │
                          │  Inventory — add items to character          │
                          │  GameManager — time/rest resets              │
                          └─────────────────────────────────────────────┘
```

### New Files to Create

```
Assets/Scripts/Crafting/
├── CraftingFeatType.cs           # Enum for 8 creation feats
├── CraftableItemDefinition.cs    # Template for a craftable item
├── CraftableItemRegistry.cs      # Registry of all craftable items per feat
├── CraftingCostCalculator.cs     # Gold, XP, base price calculations
├── CraftingTimeCalculator.cs     # Day calculations, time advancement
├── CraftingValidator.cs          # Full prerequisite validation
├── CraftingExecutor.cs           # Execute crafting (deduct costs, create item)
├── CraftingProject.cs            # Data class for an in-progress crafting project
├── CraftingWorkshopUI.cs         # Main crafting menu UI
├── CraftingItemBrowserPanel.cs   # Item selection/browsing sub-panel
├── CraftingCostPreviewPanel.cs   # Cost/prereq preview sub-panel
└── CraftingConfirmationDialog.cs # "Are you sure?" confirmation
```

### Modified Files

```
Assets/Scripts/UI/PreCombatHubUI.cs          # Add "⚒ Crafting Workshop" button
Assets/Scripts/Core/GameManager.cs           # Add crafting callback, time advancement
Assets/Scripts/Character/FeatDefinitions.cs  # Remove IsPlaceholder from creation feats
Assets/Scripts/Character/CharacterStats.cs   # Add XP spending method, crafting gold
Assets/Scripts/Core/SceneBootstrap.cs        # Initialize CraftableItemRegistry
```

---

## 3. All 8 Creation Feats — Complete Mechanics

### 3.1 Scribe Scroll

| Property | Value |
|:---------|:------|
| **Prerequisite** | Caster level 1st |
| **Special** | **FREE for all spellcasters** — granted automatically |
| **Creates** | Spell scrolls |
| **Spell Level Limit** | Any spell the caster knows/prepares |
| **Base Price** | Spell Level × Caster Level × 25 gp |
| **Gold Cost** | Base Price / 2 |
| **XP Cost** | Base Price / 25 |
| **Time** | 1 day per 1,000 gp base price (minimum 1 day) |
| **Spell Required** | The spell being scribed (must know/prepare it) |

**Base Price Examples:**

| Scroll | Spell Lvl | CL | Base Price | Gold Cost | XP Cost | Time |
|:-------|:---------:|:--:|:----------:|:---------:|:-------:|:----:|
| *Magic Missile* (1st) | 1 | 1 | 25 gp | 12 gp | 1 XP | 1 day |
| *Fireball* (3rd) | 3 | 5 | 375 gp | 187 gp | 15 XP | 1 day |
| *Stoneskin* (4th) | 4 | 7 | 700 gp | 350 gp | 28 XP | 1 day |
| *Heal* (6th) | 6 | 11 | 1,650 gp | 825 gp | 66 XP | 2 days |
| *Wish* (9th) | 9 | 17 | 3,825 gp | 1,912 gp | 153 XP | 4 days |

**Note:** 0th-level scrolls use spell level 0.5 for pricing: `0.5 × CL × 25 = CL × 12.5 gp`.

### 3.2 Brew Potion

| Property | Value |
|:---------|:------|
| **Prerequisite** | Caster level 3rd |
| **Creates** | Potions |
| **Spell Level Limit** | 3rd level or lower |
| **Targeting** | Must target one or more creatures (no area-only spells) |
| **Base Price** | Spell Level × Caster Level × 50 gp |
| **Gold Cost** | Base Price / 2 |
| **XP Cost** | Base Price / 25 |
| **Time** | 1 day (always — potions are quick) |
| **Spell Required** | The spell being bottled |

**Base Price Examples:**

| Potion | Spell Lvl | CL | Base Price | Gold Cost | XP Cost |
|:-------|:---------:|:--:|:----------:|:---------:|:-------:|
| *Cure Light Wounds* | 1 | 1 | 50 gp | 25 gp | 2 XP |
| *Bull's Strength* | 2 | 3 | 300 gp | 150 gp | 12 XP |
| *Fly* | 3 | 5 | 750 gp | 375 gp | 30 XP |
| *Haste* | 3 | 5 | 750 gp | 375 gp | 30 XP |

**Note:** 0th-level potions use spell level 0.5 for pricing: `0.5 × CL × 50 = CL × 25 gp`.

**Ineligible spells for potions:**
- Spells with range "Personal" (unless also targets creatures)
- Spells with area-only effects (e.g., *Fireball*, *Wall of Fire*)
- Spells above 3rd level

### 3.3 Craft Wand

| Property | Value |
|:---------|:------|
| **Prerequisite** | Caster level 5th |
| **Creates** | Wands (50 charges) |
| **Spell Level Limit** | 4th level or lower |
| **Base Price** | Spell Level × Caster Level × 750 gp |
| **Gold Cost** | Base Price / 2 |
| **XP Cost** | Base Price / 25 |
| **Time** | 1 day per 1,000 gp base price (minimum 1 day) |
| **Spell Required** | The spell being stored |
| **Charges** | Always 50 |

**Base Price Examples:**

| Wand | Spell Lvl | CL | Base Price | Gold Cost | XP Cost | Time |
|:-----|:---------:|:--:|:----------:|:---------:|:-------:|:----:|
| *Cure Light Wounds* | 1 | 1 | 750 gp | 375 gp | 30 XP | 1 day |
| *Scorching Ray* | 2 | 3 | 4,500 gp | 2,250 gp | 180 XP | 5 days |
| *Fireball* | 3 | 5 | 11,250 gp | 5,625 gp | 450 XP | 12 days |
| *Stoneskin* | 4 | 7 | 21,000 gp | 10,500 gp | 840 XP | 21 days |

**Note:** 0th-level wands use spell level 0.5: `0.5 × CL × 750 = CL × 375 gp`.

### 3.4 Craft Magic Arms and Armor

| Property | Value |
|:---------|:------|
| **Prerequisite** | Caster level 5th |
| **Creates** | Magic weapons, armor, shields |
| **Requires** | **Masterwork base item** (must already exist in inventory) |
| **Base Price** | Enhancement bonus squared × 2,000 gp (weapons) or × 1,000 gp (armor/shields) |
| **Gold Cost** | Base Price / 2 |
| **XP Cost** | Base Price / 25 |
| **Time** | 1 day per 1,000 gp base price |
| **Spells Required** | Vary by enchantment (see table below) |
| **CL Requirement** | 3 × enhancement bonus (minimum) |

**Enhancement Bonus Pricing:**

| Bonus | Weapon Base Price | Armor/Shield Base Price | Gold Cost (W) | Gold Cost (A) | XP (W) | XP (A) | Time (W) | Time (A) |
|:-----:|:-----------------:|:----------------------:|:-------------:|:-------------:|:------:|:------:|:--------:|:--------:|
| +1 | 2,000 gp | 1,000 gp | 1,000 gp | 500 gp | 80 XP | 40 XP | 2 days | 1 day |
| +2 | 8,000 gp | 4,000 gp | 4,000 gp | 2,000 gp | 320 XP | 160 XP | 8 days | 4 days |
| +3 | 18,000 gp | 9,000 gp | 9,000 gp | 4,500 gp | 720 XP | 360 XP | 18 days | 9 days |
| +4 | 32,000 gp | 16,000 gp | 16,000 gp | 8,000 gp | 1,280 XP | 640 XP | 32 days | 16 days |
| +5 | 50,000 gp | 25,000 gp | 25,000 gp | 12,500 gp | 2,000 XP | 1,000 XP | 50 days | 25 days |

**Upgrading:** When upgrading from +N to +M, the cost is the *difference* between the two prices (not the full +M cost).

**Special Ability Pricing (adds equivalent bonus):**

| Weapon Ability | Equiv. Bonus | Required Spell |
|:---------------|:------------:|:---------------|
| Flaming | +1 | *Flame Blade* or *Flame Strike* or *Fireball* |
| Frost | +1 | *Chill Metal* or *Ice Storm* |
| Shock | +1 | *Call Lightning* or *Lightning Bolt* |
| Keen | +1 | *Keen Edge* |
| Ghost Touch | +1 | *Plane Shift* or *Etherealness* |
| Holy | +2 | *Holy Smite* |
| Unholy | +2 | *Unholy Blight* |
| Vorpal | +5 | *Circle of Death* or *Keen Edge* |

| Armor Ability | Equiv. Bonus | Required Spell |
|:-------------|:------------:|:---------------|
| Fortification, Light | +1 | *Limited Wish* or *Miracle* |
| Shadow | +1 | *Invisibility* or *Shadow Walk* |
| Silent Moves | +1 | *Silence* |
| Spell Resistance (13) | +2 | *Spell Resistance* |
| Fortification, Moderate | +3 | *Limited Wish* or *Miracle* |
| Fortification, Heavy | +5 | *Limited Wish* or *Miracle* |

**Maximum total bonus:** +10 (enhancement + special abilities combined).

**Required spell for base enhancement:** `+1` needs no specific spell; `+2`+ needs caster level ≥ `3 × bonus`.

### 3.5 Craft Wondrous Item

| Property | Value |
|:---------|:------|
| **Prerequisite** | Caster level 3rd |
| **Creates** | Wondrous items (cloaks, boots, amulets, belts, etc.) |
| **Base Price** | Per item (from DMG table / existing database) |
| **Gold Cost** | Market Price / 2 |
| **XP Cost** | Market Price / 25 |
| **Time** | 1 day per 1,000 gp market price |
| **Spells Required** | Vary by item (see DMG) |

**Common Wondrous Item Examples:**

| Item | Market Price | Gold Cost | XP Cost | Time | Required Spells |
|:-----|:-----------:|:---------:|:-------:|:----:|:----------------|
| Cloak of Resistance +1 | 1,000 gp | 500 gp | 40 XP | 1 day | *Resistance* |
| Boots of Speed | 12,000 gp | 6,000 gp | 480 XP | 12 days | *Haste* |
| Headband of Intellect +2 | 4,000 gp | 2,000 gp | 160 XP | 4 days | *Fox's Cunning* |
| Belt of Giant Strength +4 | 16,000 gp | 8,000 gp | 640 XP | 16 days | *Bull's Strength* |
| Amulet of Natural Armor +3 | 18,000 gp | 9,000 gp | 720 XP | 18 days | *Barkskin* |
| Bag of Holding (Type I) | 2,500 gp | 1,250 gp | 100 XP | 3 days | *Secret Chest* |
| Carpet of Flying (5×5) | 35,000 gp | 17,500 gp | 1,400 XP | 35 days | *Overland Flight* |
| Iron Cobra | 80,000 gp | 40,000 gp | 3,200 XP | 80 days | *Animate Objects*, *Geas/Quest* |

### 3.6 Craft Rod

| Property | Value |
|:---------|:------|
| **Prerequisite** | Caster level 9th |
| **Creates** | Rods |
| **Base Price** | Per rod (from DMG table / RodDatabase) |
| **Gold Cost** | Market Price / 2 |
| **XP Cost** | Market Price / 25 |
| **Time** | 1 day per 1,000 gp market price |
| **Spells Required** | Vary by rod |

**Common Rod Examples:**

| Rod | Market Price | Gold Cost | XP Cost | Time | Required Spells |
|:----|:-----------:|:---------:|:-------:|:----:|:----------------|
| Metamagic (Lesser Extend) | 3,000 gp | 1,500 gp | 120 XP | 3 days | *Extend Spell* (feat) |
| Metamagic (Normal Empower) | 32,500 gp | 16,250 gp | 1,300 XP | 33 days | *Empower Spell* (feat) |
| Metamagic (Greater Quicken) | 170,000 gp | 85,000 gp | 6,800 XP | 170 days | *Quicken Spell* (feat) |
| Rod of Wonder | 12,000 gp | 6,000 gp | 480 XP | 12 days | *Confusion*, *Fireball* |
| Rod of Enemy Detection | 23,500 gp | 11,750 gp | 940 XP | 24 days | *Detect Enemies* |

**Metamagic Rod special rule:** The crafter must possess the corresponding metamagic feat.

### 3.7 Craft Staff

| Property | Value |
|:---------|:------|
| **Prerequisite** | Caster level 12th |
| **Creates** | Staves (50 charges) |
| **Base Price** | Per staff (from DMG table / StaffDatabase) |
| **Gold Cost** | Market Price / 2 |
| **XP Cost** | Market Price / 25 |
| **Time** | 1 day per 1,000 gp market price |
| **Spells Required** | **ALL spells stored in the staff** |
| **Special** | Crafter must supply all spells; CL must be ≥ highest spell CL |

**Staff Examples:**

| Staff | Market Price | Gold Cost | XP Cost | Time | Required Spells |
|:------|:-----------:|:---------:|:-------:|:----:|:----------------|
| Staff of Fire | 17,750 gp | 8,875 gp | 710 XP | 18 days | *Burning Hands*, *Fireball*, *Wall of Fire* |
| Staff of Healing | 27,750 gp | 13,875 gp | 1,110 XP | 28 days | *Cure Serious*, *Lesser Restoration*, *Remove Disease* |
| Staff of Power | 211,000 gp | 105,500 gp | 8,440 XP | 211 days | *Magic Missile*, *Ray of Enfeeblement*, *Levitate*, *Lightning Bolt*, *Fireball*, *Cone of Cold*, *Hold Monster*, *Globe of Invulnerability*, *Wall of Force* |

### 3.8 Forge Ring

| Property | Value |
|:---------|:------|
| **Prerequisite** | Caster level 12th |
| **Creates** | Rings |
| **Base Price** | Per ring (from DMG table / RingDatabase) |
| **Gold Cost** | Market Price / 2 |
| **XP Cost** | Market Price / 25 |
| **Time** | 1 day per 1,000 gp market price |
| **Spells Required** | Vary by ring |

**Ring Examples:**

| Ring | Market Price | Gold Cost | XP Cost | Time | Required Spells |
|:-----|:-----------:|:---------:|:-------:|:----:|:----------------|
| Ring of Protection +1 | 2,000 gp | 1,000 gp | 80 XP | 2 days | *Shield of Faith* |
| Ring of Protection +3 | 18,000 gp | 9,000 gp | 720 XP | 18 days | *Shield of Faith* |
| Ring of Invisibility | 20,000 gp | 10,000 gp | 800 XP | 20 days | *Invisibility* |
| Ring of Spell Turning | 98,280 gp | 49,140 gp | 3,931 XP | 99 days | *Spell Turning* |
| Ring of Freedom of Movement | 40,000 gp | 20,000 gp | 1,600 XP | 40 days | *Freedom of Movement* |

---

## 4. Cost System

### 4.1 Base Price Formulas

| Creation Type | Base Price Formula |
|:-------------|:-------------------|
| **Scroll** | Spell Level × Caster Level × 25 gp |
| **Potion** | Spell Level × Caster Level × 50 gp |
| **Wand** | Spell Level × Caster Level × 750 gp |
| **Arms/Armor** | Enhancement² × 2,000 gp (weapon) or × 1,000 gp (armor) |
| **Wondrous Item** | Fixed per item (from database / DMG) |
| **Rod** | Fixed per item (from database / DMG) |
| **Staff** | Fixed per item (from database / DMG) |
| **Ring** | Fixed per item (from database / DMG) |

### 4.2 Derived Costs

```csharp
public static class CraftingCostCalculator
{
    /// Gold cost = market price / 2 (always)
    public static int GoldCost(int marketPrice) => Mathf.Max(1, marketPrice / 2);

    /// XP cost = market price / 25 (always)
    public static int XPCost(int marketPrice) => Mathf.Max(1, marketPrice / 25);

    /// Days = base price / 1000, minimum 1
    public static int DaysRequired(int basePrice) => Mathf.Max(1, basePrice / 1000);

    /// Scroll base price
    public static int ScrollBasePrice(int spellLevel, int casterLevel)
    {
        float effectiveLevel = spellLevel == 0 ? 0.5f : spellLevel;
        return Mathf.RoundToInt(effectiveLevel * casterLevel * 25f);
    }

    /// Potion base price
    public static int PotionBasePrice(int spellLevel, int casterLevel)
    {
        float effectiveLevel = spellLevel == 0 ? 0.5f : spellLevel;
        return Mathf.RoundToInt(effectiveLevel * casterLevel * 50f);
    }

    /// Wand base price (50 charges)
    public static int WandBasePrice(int spellLevel, int casterLevel)
    {
        float effectiveLevel = spellLevel == 0 ? 0.5f : spellLevel;
        return Mathf.RoundToInt(effectiveLevel * casterLevel * 750f);
    }

    /// Weapon enhancement base price
    public static int WeaponEnhancementBasePrice(int totalBonus)
        => totalBonus * totalBonus * 2000;

    /// Armor/shield enhancement base price
    public static int ArmorEnhancementBasePrice(int totalBonus)
        => totalBonus * totalBonus * 1000;

    /// Upgrade cost (difference between new and old)
    public static int UpgradeCost(int newBonus, int oldBonus, bool isWeapon)
    {
        int newPrice = isWeapon ? WeaponEnhancementBasePrice(newBonus) : ArmorEnhancementBasePrice(newBonus);
        int oldPrice = isWeapon ? WeaponEnhancementBasePrice(oldBonus) : ArmorEnhancementBasePrice(oldBonus);
        return newPrice - oldPrice;
    }
}
```

### 4.3 Material Component Costs

Some items have **additional material component costs** on top of the standard formula:

| Item | Extra Material Cost | Reason |
|:-----|:-------------------:|:-------|
| Scroll of *Stoneskin* | 250 gp diamond dust | Spell's material component |
| Potion of *Nondetection* | 50 gp | Spell's material component |
| Any item requiring costly component | Component cost | Added on top of base gold cost |

```
Total Gold Cost = (Market Price / 2) + Material Component Costs
```

### 4.4 Maximum Crafting Per Day

Per DMG p.282: A character can craft items worth up to **1,000 gp per day**. Items costing more than 1,000 gp take multiple days. A character can only work on **one item at a time**.

---

## 5. Time System

### 5.1 Time Calculation

```
Days Required = ceil(Base Price / 1,000)   [minimum 1 day]
```

**All potions take exactly 1 day** regardless of price (DMG special rule).

### 5.2 Time Advancement Design

Crafting happens in **downtime mode** — when the player confirms crafting, time fast-forwards:

1. Calculate days required
2. Display confirmation: "This will take N days. Proceed?"
3. On confirm: advance game time by N days
4. Trigger appropriate resets for each day:
   - Daily ability resets (rings, wondrous items, rod uses)
   - Weekly resets every 7 days
   - Monthly resets every 30 days
5. Item appears in crafter's inventory

### 5.3 Time Examples

| Item Being Crafted | Base Price | Days | Notes |
|:-------------------|:---------:|:----:|:------|
| Scroll of *Fireball* | 375 gp | 1 | Below 1,000 → minimum 1 day |
| Potion of *Fly* | 750 gp | 1 | Potions always 1 day |
| Wand of *Magic Missile* | 750 gp | 1 | Below 1,000 → minimum 1 day |
| Ring of Protection +1 | 2,000 gp | 2 | |
| +1 Longsword | 2,000 gp | 2 | |
| +3 Full Plate | 9,000 gp | 9 | |
| +5 Greatsword | 50,000 gp | 50 | Major time commitment |
| Staff of Power | 211,000 gp | 211 | Nearly 7 months! |

### 5.4 Interruption Rules

Per DMG: If crafting is interrupted, the crafter loses **materials and time spent so far** but not XP.

**For the prototype:** Crafting is atomic (instant fast-forward), so interruption doesn't apply in the current design. If we add multi-encounter interruptions later:

```csharp
public class CraftingProject
{
    public int DaysCompleted;
    public int DaysTotal;
    public int GoldSpentSoFar;  // Lost on interruption
    public int XPReserved;      // Refunded on interruption
    public bool IsComplete => DaysCompleted >= DaysTotal;
}
```

---

## 6. Spell Prerequisite System

### 6.1 How Prerequisites Work

Each magic item has **required spells** that the crafter must provide. The crafter can supply spells from:

1. **Known/Prepared Spells** — Check via `SpellcastingComponent.GetKnownSpellsForClass()`
2. **Class Spell List Access** — Classes that "know all" (Cleric, Druid) via `SpellcastingComponent.ClassKnowsAllSpells()`
3. **Spell Substitution** — Missing spells increase Spellcraft DC by +5 per missing spell (DMG p.215)
4. **Another Character** — A different party member can supply a missing spell (they must be present)

### 6.2 Spell Substitution Rule (DMG p.215)

> "If the creator doesn't have a required spell (or doesn't know it), he can attempt to complete the creation anyway by increasing the DC of the Spellcraft check by +5 for each missing spell."

**Implementation:**

```csharp
public static class CraftingValidator
{
    /// Check spell prerequisites. Returns (metSpells, missingSpells, substitutionDC)
    public static SpellPrereqResult CheckSpellPrereqs(
        CharacterController crafter,
        List<string> requiredSpellIds,
        List<CharacterController> partyMembers)
    {
        var met = new List<string>();
        var missing = new List<string>();

        foreach (string spellId in requiredSpellIds)
        {
            bool found = CrafterKnowsSpell(crafter, spellId);
            if (!found)
                found = PartyMemberKnowsSpell(partyMembers, spellId, crafter);

            if (found) met.Add(spellId);
            else missing.Add(spellId);
        }

        int substitutionDC = 5 * missing.Count;
        return new SpellPrereqResult(met, missing, substitutionDC);
    }
}
```

### 6.3 Per-Category Spell Requirements

#### Scrolls, Potions, Wands
- Require **exactly the spell being stored** — no substitution allowed for the primary spell
- The crafter must know/prepare the specific spell

#### Magic Arms and Armor
- `+1` enhancement: No specific spell, just Craft Magic Arms and Armor feat
- Special abilities (Flaming, Frost, etc.): Require specific spells (see table in §3.4)
- Multiple abilities require all prerequisite spells

#### Wondrous Items, Rods, Rings, Staves
- Each item has a specific list of prerequisite spells defined in the DMG
- These will be stored in `CraftableItemDefinition.RequiredSpellIds`

### 6.4 Spell Prerequisite Data Storage

Add to the crafting data:

```csharp
public class CraftableItemDefinition
{
    public string ItemId;                    // Links to ItemDatabase
    public CraftingFeatType RequiredFeat;
    public int MinimumCasterLevel;
    public List<string> RequiredSpellIds;    // Spell IDs from SpellDatabase
    public List<string> RequiredFeatNames;   // Additional feat prerequisites
    public int MaterialComponentCostGp;      // Extra material costs
    public bool RequiresMasterworkBase;      // For arms/armor
    public string MasterworkBaseItemId;      // Which base item to consume
}
```

---

## 7. Validation System

### 7.1 Complete Validation Checklist

Before crafting can begin, the system validates ALL of:

```
┌─────────────────────────────────────────────┐
│            VALIDATION PIPELINE              │
├─────────────────────────────────────────────┤
│ 1. Has the required Item Creation feat?     │
│    → CharacterStats.HasFeat("Brew Potion")  │
│                                             │
│ 2. Meets minimum caster level?              │
│    → CL ≥ item's required CL               │
│    → Feat CL: Scroll≥1, Potion≥3,          │
│      Wand/Arms≥5, Rod≥9, Staff/Ring≥12     │
│                                             │
│ 3. Has required spells?                     │
│    → Direct: crafter knows the spell        │
│    → Party: another member supplies it      │
│    → Substitution: +5 DC per missing        │
│                                             │
│ 4. Has required additional feats?           │
│    → Metamagic rods need metamagic feat     │
│    → Some items need specific feats         │
│                                             │
│ 5. Has sufficient gold?                     │
│    → Gold ≥ (MarketPrice / 2) + materials   │
│                                             │
│ 6. Has sufficient XP?                       │
│    → XP ≥ (MarketPrice / 25)               │
│    → XP after spending ≥ level threshold    │
│                                             │
│ 7. Has masterwork base? (Arms/Armor only)   │
│    → Item in inventory with Masterwork=true │
│                                             │
│ 8. Doesn't violate maximum limits?          │
│    → Max +10 total bonus (Arms/Armor)       │
│    → Max 4th level spell (Wands)            │
│    → Max 3rd level spell (Potions)          │
│    → Potion must target creatures           │
└─────────────────────────────────────────────┘
```

### 7.2 Validation Result Structure

```csharp
public class CraftingValidationResult
{
    public bool IsValid;
    public bool HasFeat;
    public bool MeetsCasterLevel;
    public int RequiredCasterLevel;
    public int ActualCasterLevel;
    public bool HasAllSpells;
    public List<string> MetSpells;
    public List<string> MissingSpells;
    public int SpellSubstitutionDC;
    public bool HasRequiredFeats;
    public List<string> MissingFeats;
    public bool HasGold;
    public int GoldRequired;
    public int GoldAvailable;
    public bool HasXP;
    public int XPRequired;
    public int XPAvailable;
    public int XPMinimumForLevel;       // Can't go below this
    public bool HasMasterworkBase;      // Arms/Armor only
    public string FailureReason;        // Human-readable summary
}
```

---

## 8. UI / Menu Design

### 8.1 Pre-Combat Hub Integration

Modify `PreCombatHubUI` to add a new button:

```
Current Buttons:
  📦 Manage Inventory (Stash)
  🏪 Open Store
  🔮 Prepare Spells
  ⚔  Start Encounter
  ← Back to Encounter Selection

New Layout (add between Prepare Spells and Start Encounter):
  📦 Manage Inventory (Stash)
  🏪 Open Store
  🔮 Prepare Spells
  ⚒  Crafting Workshop            ← NEW
  ⚔  Start Encounter
  ← Back to Encounter Selection
```

The button is **always visible** but shows "(No crafters)" and is grayed out if no party member has any item creation feat.

### 8.2 Crafting Workshop — Main Layout

```
┌──────────────────────────────────────────────────────────────────────┐
│                      ⚒ CRAFTING WORKSHOP                            │
│                                                                      │
│  Crafter: [▼ Aldric the Wizard (CL 9) ▼]    Gold: 12,450 gp        │
│           XP: 45,200 / 45,000 (Level 9)                             │
│                                                                      │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │ FEATS: [Scroll] [Wand] [Wondrous] [Arms & Armor]             │  │
│  │        (grayed out: Rod, Staff, Ring - don't have feat)        │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  ┌──────────────────────────┐  ┌──────────────────────────────────┐  │
│  │  ITEM BROWSER            │  │  COST PREVIEW                    │  │
│  │                          │  │                                  │  │
│  │  Search: [________]      │  │  Wand of Fireball (CL 5)        │  │
│  │  Sort: [Name ▼]         │  │                                  │  │
│  │                          │  │  Gold:  5,625 gp    ✅ (have     │  │
│  │  ┌────────────────────┐  │  │                      12,450)     │  │
│  │  │ Wand of Cure Light │  │  │  XP:   450 XP       ✅ (have    │  │
│  │  │ Wand of Magic Miss │  │  │                      45,200)     │  │
│  │  │ Wand of Fireball ◄─┤──┤──│  Time:  12 days                 │  │
│  │  │ Wand of Scorching  │  │  │  CL:    ✅ CL 9 ≥ CL 5         │  │
│  │  │ Wand of Lightning  │  │  │                                  │  │
│  │  │ Wand of Stoneskin  │  │  │  Spells:                         │  │
│  │  │ ...                │  │  │    ✅ Fireball (known)            │  │
│  │  └────────────────────┘  │  │                                  │  │
│  │                          │  │  ┌──────────────────────────────┐│  │
│  │  Showing 24 items        │  │  │  [⚒ Begin Crafting]         ││  │
│  └──────────────────────────┘  │  └──────────────────────────────┘│  │
│                                │                                  │  │
│                                └──────────────────────────────────┘  │
│                                                                      │
│  [← Back to Hub]                                                     │
└──────────────────────────────────────────────────────────────────────┘
```

### 8.3 Item Browser Panel

Features:
- **Search field** — Filter items by name substring
- **Sort** — By name (A-Z), price (low-high, high-low), spell level, caster level
- **Category filter** — Subcategories per feat (e.g., for Wondrous: head, neck, back, etc.)
- **Availability indicator** — Items the character can't craft show with ⚠️ icon and red tint
- **Scroll list** — Standard Unity scroll view, items as clickable rows
- **Selected item** — Highlighted row, details shown in Cost Preview

Each row displays:
```
[Icon] Item Name                  Gold: X gp | XP: X | Time: X days
```

### 8.4 Cost Preview Panel

Shows full details for the selected item:
- Item name and description (truncated)
- **Gold cost** — amount + ✅/❌ status (with "have X gp" note)
- **XP cost** — amount + ✅/❌ status (with "have X XP" note)
- **XP safety** — Warning if XP would drop below level threshold
- **Time** — Days required
- **Caster Level** — Required vs. actual, ✅/❌
- **Spell prerequisites** — Each spell listed with ✅ known / ⚠️ party member / ❌ missing (+5 DC)
- **Additional feats** — If required (metamagic for rods)
- **Masterwork base** — For arms/armor, shows if base item is in inventory
- **"Begin Crafting" button** — Enabled only when all requirements met (or substitution accepted)

### 8.5 Confirmation Dialog

```
┌────────────────────────────────────────────┐
│         CONFIRM CRAFTING                    │
│                                            │
│  Aldric will craft:                        │
│  ✨ Wand of Fireball (CL 5, 50 charges)   │
│                                            │
│  This will cost:                           │
│    💰 5,625 gold pieces                    │
│    ⭐ 450 experience points                │
│    🕐 12 days                              │
│                                            │
│  After crafting:                           │
│    Gold: 12,450 → 6,825 gp                │
│    XP:   45,200 → 44,750                  │
│    Level: 9 (safe — need 36,000 for L9)   │
│                                            │
│  [✅ Confirm]     [❌ Cancel]               │
└────────────────────────────────────────────┘
```

### 8.6 Crafting In Progress (Fast-Forward) Animation

When crafting executes:
1. Brief overlay: "⚒ Crafting in progress... (12 days)"
2. Progress bar fills (cosmetic — actually instant)
3. Result popup: "✅ Aldric crafted Wand of Fireball! (50 charges)"
4. Item added to inventory
5. Return to Crafting Workshop (can craft more)

---

## 9. Crafting Workflow

### 9.1 Complete Step-by-Step Flow

```
 ┌───────────────────────────────────────┐
 │  1. OPEN CRAFTING WORKSHOP            │
 │     (Pre-Combat Hub → "⚒ Crafting")   │
 │     Check: at least one party member   │
 │     has an item creation feat          │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  2. SELECT CRAFTER                    │
 │     Dropdown lists party members with  │
 │     item creation feats. Shows CL,     │
 │     gold, XP for selected character.   │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  3. SELECT CREATION FEAT TAB          │
 │     Only feats the character has are   │
 │     enabled. Others grayed out.        │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  4. BROWSE & SELECT ITEM              │
 │     Filter/search items craftable via  │
 │     this feat. Click to select.        │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  5. REVIEW COST PREVIEW               │
 │     See gold, XP, time, spell reqs.   │
 │     All checks displayed with ✅/❌.   │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  6. CLICK "BEGIN CRAFTING"            │
 │     Only enabled if all checks pass   │
 │     (or substitution accepted).        │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  7. CONFIRMATION DIALOG               │
 │     Shows final summary of costs.      │
 │     "After crafting" resource preview. │
 │     [Confirm] or [Cancel].             │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  8. EXECUTE CRAFTING                  │
 │     a. Deduct gold from character      │
 │     b. Deduct XP from character        │
 │     c. Advance time (N days)           │
 │     d. Trigger daily/weekly resets      │
 │     e. Create item via ItemDatabase    │
 │     f. Add to character's inventory    │
 │     g. Consume masterwork base (Arms)  │
 │     h. Log result                      │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  9. RESULT                            │
 │     Show success message.              │
 │     Item appears in inventory.         │
 │     Return to Workshop (craft more).   │
 └───────────────────────────────────────┘
```

### 9.2 Special Workflow: Craft Magic Arms and Armor

This feat has a unique sub-workflow because it operates on existing items:

```
 ┌───────────────────────────────────────┐
 │  A. SELECT BASE OPERATION             │
 │     [Create New +1 Item]              │
 │     [Upgrade Existing Item]            │
 │     [Add Special Ability]              │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  B. SELECT BASE ITEM (from inventory)  │
 │     Shows masterwork weapons/armor     │
 │     or existing magical items.         │
 │     Shows current enhancement & slots. │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  C. SELECT ENHANCEMENT/ABILITY        │
 │     For new: +1 (mandatory first)      │
 │     For upgrade: +2, +3, +4, +5        │
 │     For ability: Flaming, Frost, etc.  │
 │     Shows total bonus (max +10).       │
 └──────────────────┬────────────────────┘
                    ▼
 │  Continue to standard cost preview... │
```

### 9.3 Special Workflow: Scribe Scroll / Brew Potion / Craft Wand

These feats create items from **the caster's own spells**, not from a database list:

```
 ┌───────────────────────────────────────┐
 │  A. SELECT SPELL FROM KNOWN SPELLS    │
 │     Shows all known spells filtered:   │
 │     - Scroll: any level                │
 │     - Potion: 0-3, targets creatures   │
 │     - Wand: 0-4                        │
 │     Each spell shows auto-calculated   │
 │     cost and time.                     │
 └──────────────────┬────────────────────┘
                    ▼
 ┌───────────────────────────────────────┐
 │  B. SELECT CASTER LEVEL (optional)    │
 │     CL defaults to minimum for spell.  │
 │     Higher CL = higher cost but better │
 │     effect (scroll CL, wand CL).       │
 │     CL slider: min → character's CL    │
 └──────────────────┬────────────────────┘
                    ▼
 │  Continue to standard cost preview... │
```

---

## 10. Item Filtering — What Can Be Crafted

### 10.1 Per-Feat Item Sources

| Feat | Source Database | Filter Logic |
|:-----|:---------------|:-------------|
| **Scribe Scroll** | Character's known spells | All spell levels; generate scroll ItemData on-the-fly |
| **Brew Potion** | Character's known spells | Level ≤ 3, must target creatures (not area-only, not personal) |
| **Craft Wand** | Character's known spells | Level ≤ 4 |
| **Craft Magic Arms & Armor** | Character's inventory (masterwork base) + EnchantmentFactory | Masterwork items for new; existing magic items for upgrade |
| **Craft Wondrous Item** | `WondrousItemDatabase.GetAllItems()` | All wondrous items with `CrafterCasterLevel ≤ character CL` |
| **Craft Rod** | `RodDatabase.GetAllRods()` | All rods; metamagic rods also need the metamagic feat |
| **Craft Staff** | `StaffDatabase.GetAllStaves()` | All staves; crafter must know ALL spells in staff |
| **Forge Ring** | `RingDatabase.GetAllRings()` | All rings with `CrafterCasterLevel ≤ character CL` |

### 10.2 Filtering Implementation

```csharp
public static class CraftableItemRegistry
{
    /// Get all items a character can craft with a specific feat
    public static List<CraftableItemDefinition> GetCraftableItems(
        CharacterController crafter,
        CraftingFeatType feat)
    {
        switch (feat)
        {
            case CraftingFeatType.ScribeScroll:
                return GetScrollCraftables(crafter);
            case CraftingFeatType.BrewPotion:
                return GetPotionCraftables(crafter);
            case CraftingFeatType.CraftWand:
                return GetWandCraftables(crafter);
            case CraftingFeatType.CraftMagicArmsAndArmor:
                return GetArmsArmorCraftables(crafter);
            case CraftingFeatType.CraftWondrousItem:
                return GetWondrousCraftables(crafter);
            case CraftingFeatType.CraftRod:
                return GetRodCraftables(crafter);
            case CraftingFeatType.CraftStaff:
                return GetStaffCraftables(crafter);
            case CraftingFeatType.ForgeRing:
                return GetRingCraftables(crafter);
            default:
                return new List<CraftableItemDefinition>();
        }
    }
}
```

### 10.3 Dynamic Item Generation (Scrolls, Potions, Wands)

These three feats generate items **dynamically from the character's spell list**, not from a fixed database:

```csharp
private static List<CraftableItemDefinition> GetScrollCraftables(CharacterController crafter)
{
    var result = new List<CraftableItemDefinition>();
    var sc = crafter.GetComponent<SpellcastingComponent>();
    if (sc == null) return result;

    foreach (var spell in sc.GetAllKnownSpells().Select(SpellDatabase.GetSpell).Where(s => s != null))
    {
        int cl = GetMinimumCasterLevel(spell);
        result.Add(new CraftableItemDefinition
        {
            ItemId = $"scroll_{spell.SpellId}",
            DisplayName = $"Scroll of {spell.Name}",
            RequiredFeat = CraftingFeatType.ScribeScroll,
            MinimumCasterLevel = Mathf.Max(1, cl),
            MarketPrice = CraftingCostCalculator.ScrollBasePrice(spell.SpellLevel, cl),
            RequiredSpellIds = new List<string> { spell.SpellId },
            IsDynamic = true,
            SourceSpellId = spell.SpellId,
            SourceSpellLevel = spell.SpellLevel,
            DefaultCasterLevel = cl
        });
    }
    return result;
}
```

---

## 11. Special Requirements

### 11.1 Masterwork Base Item (Arms & Armor)

**D&D 3.5e Rule:** You can only enchant a **masterwork** weapon, armor, or shield. The masterwork item is consumed/transformed during crafting.

**Implementation:**
1. When "Craft Magic Arms and Armor" tab is selected, scan the crafter's inventory for masterwork items
2. Display available base items
3. On crafting, remove the masterwork base item and replace with the magical version
4. The masterwork cost (typically 300 gp for weapons, 150 gp for armor) is NOT included in enchantment cost

```csharp
public static List<ItemData> GetMasterworkBaseItems(Inventory inventory)
{
    return inventory.GetAllItems()
        .Where(item => item.IsMasterwork && (item.IsWeapon || item.IsArmor || item.IsShield))
        .Where(item => item.EnhancementBonus == 0) // Not already magical
        .ToList();
}

// For upgrading existing magical items:
public static List<ItemData> GetUpgradeableItems(Inventory inventory)
{
    return inventory.GetAllItems()
        .Where(item => item.IsWeapon || item.IsArmor || item.IsShield)
        .Where(item => item.EnhancementBonus >= 1 && item.EnhancementBonus < 5)
        .ToList();
}
```

### 11.2 Enhancement Progression Rules

Magic weapons and armor must follow a strict progression:

1. **First enhancement** must be `+1` (can't jump to +2)
2. Enhancement bonus: +1 to +5
3. Special abilities add equivalent bonus (+1 for Flaming, +2 for Holy, etc.)
4. **Total equivalent bonus** (enhancement + special abilities) cannot exceed **+10**
5. An item must have at least `+1` enhancement before adding special abilities

### 11.3 Metamagic Rod Special Rule

Crafting a metamagic rod requires the crafter to **possess the corresponding metamagic feat**:

```csharp
// Validation for metamagic rods
if (rod.RodIsMetamagic)
{
    string metamagicFeatName = MetamagicData.GetDisplayName(rod.RodMetamagicType);
    if (!crafter.Stats.HasFeat(metamagicFeatName))
    {
        result.HasRequiredFeats = false;
        result.MissingFeats.Add(metamagicFeatName);
    }
}
```

### 11.4 Staff Special Rule

The crafter must be able to **cast ALL spells** stored in the staff:

```csharp
public static bool CanCraftStaff(CharacterController crafter, StaffDefinition staff)
{
    var sc = crafter.GetComponent<SpellcastingComponent>();
    foreach (var spellEntry in staff.Spells)
    {
        if (!sc.KnowsSpell(SpellDatabase.GetSpell(spellEntry.SpellId), crafter.Stats.PrimaryClass))
            return false;
    }
    return true;
}
```

### 11.5 Potion Targeting Restriction

Potions can only contain spells that **target one or more creatures** (not area-only, not personal range):

```csharp
public static bool IsValidPotionSpell(SpellData spell)
{
    if (spell.SpellLevel > 3) return false;
    if (spell.Range == SpellRange.Personal) return false; // Exception: some Personal spells are allowed
    if (spell.TargetType == SpellTargetType.AreaOnly) return false;
    return true;
}
```

---

## 12. XP Management

### 12.1 Preventing Level Loss

**Critical Rule:** Spending XP on item creation must NEVER cause a character to drop below their current level's XP threshold.

```
D&D 3.5e XP Table:
Level 1:  0 XP
Level 2:  1,000 XP
Level 3:  3,000 XP
Level 4:  6,000 XP
Level 5:  10,000 XP
Level 6:  15,000 XP
Level 7:  21,000 XP
Level 8:  28,000 XP
Level 9:  36,000 XP
Level 10: 45,000 XP
...
Formula: Level N requires N × (N-1) × 500 XP
```

**Implementation using existing `ExperienceCalculator`:**

```csharp
public static bool CanSpendXP(CharacterStats stats, int xpCost)
{
    int currentLevel = stats.Level;
    int minXPForLevel = ExperienceCalculator.GetXPForLevel(currentLevel);
    int xpAfterSpending = stats.ExperiencePoints - xpCost;
    return xpAfterSpending >= minXPForLevel;
}

public static int MaxSpendableXP(CharacterStats stats)
{
    int currentLevel = stats.Level;
    int minXPForLevel = ExperienceCalculator.GetXPForLevel(currentLevel);
    return stats.ExperiencePoints - minXPForLevel;
}
```

### 12.2 XP Spending Method (Add to CharacterStats)

```csharp
/// <summary>
/// Spend XP on item creation. Returns false if would cause level loss.
/// </summary>
public bool SpendXP(int amount)
{
    if (amount <= 0) return false;
    int minXP = ExperienceCalculator.GetXPForLevel(Level);
    if (ExperiencePoints - amount < minXP)
    {
        Debug.LogWarning($"[XP] Cannot spend {amount} XP — would drop below level {Level} threshold ({minXP})");
        return false;
    }
    ExperiencePoints -= amount;
    Debug.Log($"[XP] {CharacterName} spent {amount} XP on crafting. Remaining: {ExperiencePoints}");
    return true;
}
```

### 12.3 UI Safety Warnings

The Cost Preview panel shows XP warnings:

| XP Situation | Display |
|:------------|:--------|
| XP plenty | `✅ 450 XP (have 45,200; floor: 36,000)` |
| XP tight | `⚠️ 450 XP (have 36,500; floor: 36,000 — only 500 available!)` |
| XP insufficient | `❌ 450 XP (have 36,300; floor: 36,000 — only 300 available!)` |
| Would cause level loss | `🚫 450 XP (WOULD LOSE LEVEL 9! Need 36,000 minimum)` |

---

## 13. Time Management

### 13.1 Days Passing

When crafting completes, the game advances N days. Each day triggers:

```csharp
public static void AdvanceCraftingDays(int days, List<CharacterController> party)
{
    for (int d = 0; d < days; d++)
    {
        _gameDayCount++;

        // Daily resets (same as rest)
        RingActivationManager.OnRest(party);
        WondrousItemActivation.OnRest(party);
        RodDatabase.ResetDailyUses();

        // Weekly resets every 7 days
        if (_gameDayCount % 7 == 0)
        {
            RingUseTracker.Instance?.OnWeeklyReset();
            WondrousItemActivation.OnWeeklyReset(party);
            RodDatabase.ResetWeeklyUses();
        }

        // Monthly resets every 30 days
        if (_gameDayCount % 30 == 0)
        {
            WondrousItemActivation.OnMonthlyReset(party);
        }

        // Heal naturally (1 HP per level per day of rest)
        foreach (var pc in party)
        {
            if (pc.Stats != null)
                pc.Stats.HealDamage(pc.Stats.Level);
        }
    }
}
```

### 13.2 Day Counter

Add to `GameManager`:

```csharp
private int _gameDayCount = 0;
public int GameDayCount => _gameDayCount;

/// Advance game days (called from CraftingExecutor and rest)
public void AdvanceDays(int count)
{
    CraftingTimeCalculator.AdvanceCraftingDays(count, GetAllPCs());
}
```

### 13.3 UI Time Display

Show the player clear information about time cost:

```
Time: 12 days
  └ Items reset: daily abilities, rod uses, ring charges
  └ Natural healing: 12 × Level HP recovered
  └ If > 7 days: weekly resets also trigger
```

---

## 14. Integration Points

### 14.1 Existing System Connections

| System | Integration | How |
|:-------|:-----------|:----|
| **ItemDatabase** | Retrieve item templates for crafting | `ItemDatabase.Get(id)`, `ItemDatabase.CloneItem(id)` |
| **RodDatabase** | List all craftable rods | `RodDatabase.GetAllRods()`, market prices from `ItemData.PriceGp` |
| **RingDatabase** | List all craftable rings | `RingDatabase.GetAllRings()` |
| **WondrousItemDatabase** | List all craftable wondrous items | `WondrousItemDatabase.GetAllItems()` |
| **StaffDatabase** | List all craftable staves | `StaffDatabase.GetAllStaves()`, spell lists from `StaffDefinition` |
| **SpecificItemDatabase** | **Excluded** — specific items cannot be player-crafted | N/A |
| **SpellDatabase** | Spell lookups for prerequisites | `SpellDatabase.GetSpell(id)` |
| **SpellcastingComponent** | Check known spells for prerequisites | `GetKnownSpellsForClass()`, `GetAllKnownSpells()` |
| **CharacterStats** | Feat checks, XP, gold, level, caster level | `HasFeat()`, `ExperiencePoints`, `ComponentGold`, `Level` |
| **FeatDefinitions** | Validate item creation feat ownership | Remove `IsPlaceholder` flag, feat already defined |
| **Inventory** | Add crafted items, check masterwork bases | `AddItem()`, `GetAllItems()` |
| **PreCombatHubUI** | Add Crafting Workshop button | New callback: `_onOpenCraftingWorkshop` |
| **GameManager** | Time advancement, day counter | New `AdvanceDays()` method |
| **ExperienceCalculator** | XP floor calculations | `GetXPForLevel()` |
| **EnchantmentFactory** | Create enchantments for crafted weapons/armor | `CreateEnchantment()`, `ApplyEnchantment()` |
| **SceneBootstrap** | Initialize CraftableItemRegistry | `CraftableItemRegistry.Init()` call |

### 14.2 Gold System

Currently `CharacterStats.ComponentGold` is used for spell components. For crafting:

**Option A (Recommended):** Use the same `ComponentGold` pool — crafting materials come from the same gold reserve as spell components.

**Option B:** Add a separate `CraftingGold` field — more accurate but adds complexity.

**Decision: Option A.** Rename in UI to "Gold" and document that it covers both spell components and crafting materials. Add `CraftingGoldSpent` tracking field for statistics.

```csharp
// Add to CharacterStats
public int TotalCraftingGoldSpent = 0;
public int TotalCraftingXPSpent = 0;
public int TotalItemsCrafted = 0;
```

### 14.3 Crafted Item Identification

Crafted items should be distinguishable from found items:

```csharp
// Add to ItemData
public bool IsCraftedByPlayer = false;
public string CraftedByCharacterName = "";
public int CraftedOnDay = 0;
```

This allows the tooltip to show "Crafted by Aldric (Day 23)" and enables future features like selling crafted items.

---

## 15. Data Structures

### 15.1 CraftingFeatType Enum

```csharp
public enum CraftingFeatType
{
    ScribeScroll,
    BrewPotion,
    CraftWand,
    CraftMagicArmsAndArmor,
    CraftWondrousItem,
    CraftRod,
    CraftStaff,
    ForgeRing
}
```

### 15.2 CraftableItemDefinition

```csharp
/// <summary>
/// Template for a craftable item. Pre-built for database items,
/// dynamically generated for scrolls/potions/wands.
/// </summary>
public class CraftableItemDefinition
{
    // Identity
    public string ItemId;                    // Links to ItemDatabase or generated ID
    public string DisplayName;               // Human-readable name
    public string Description;               // Short description
    public CraftingFeatType RequiredFeat;

    // Costs
    public int MarketPrice;                  // Full market price
    public int GoldCost => CraftingCostCalculator.GoldCost(MarketPrice);
    public int XPCost => CraftingCostCalculator.XPCost(MarketPrice);
    public int DaysRequired => RequiredFeat == CraftingFeatType.BrewPotion
        ? 1 : CraftingCostCalculator.DaysRequired(MarketPrice);
    public int MaterialComponentCostGp;      // Additional material costs

    // Prerequisites
    public int MinimumCasterLevel;
    public List<string> RequiredSpellIds = new List<string>();
    public List<string> RequiredFeatNames = new List<string>();

    // Arms & Armor specific
    public bool RequiresMasterworkBase;
    public string MasterworkBaseItemId;
    public int EnhancementBonus;             // For weapons/armor
    public string SpecialAbilityId;          // For adding specific enchantments

    // Dynamic items (scrolls/potions/wands)
    public bool IsDynamic;
    public string SourceSpellId;
    public int SourceSpellLevel;
    public int DefaultCasterLevel;
    public int CustomCasterLevel;            // Player-selected CL (scroll/wand)

    // Category (for UI filtering)
    public string Category;                  // "Head", "Neck", "Weapon", etc.
    public string SubCategory;               // More specific grouping
}
```

### 15.3 CraftingProject

```csharp
/// <summary>
/// Represents an in-progress or completed crafting project.
/// Used for execution and history tracking.
/// </summary>
public class CraftingProject
{
    // Identity
    public string ProjectId = System.Guid.NewGuid().ToString();
    public CraftableItemDefinition Definition;
    public CharacterController Crafter;

    // State
    public CraftingProjectState State = CraftingProjectState.NotStarted;
    public int DaysCompleted;
    public int DaysTotal;

    // Costs committed
    public int GoldCost;
    public int XPCost;
    public int MaterialCost;

    // Validation snapshot
    public CraftingValidationResult ValidationResult;

    // Result
    public ItemData CraftedItem;             // Set on completion
    public int CompletionDay;                // Game day when completed

    // Arms/Armor specific
    public ItemData ConsumedBaseItem;        // Masterwork base consumed
    public int PreviousEnhancement;          // For upgrade tracking
}

public enum CraftingProjectState
{
    NotStarted,
    InProgress,    // For future multi-session crafting
    Completed,
    Failed,
    Cancelled
}
```

### 15.4 Crafting History (Optional)

```csharp
/// <summary>
/// Tracks all completed crafting projects for statistics/achievements.
/// </summary>
public static class CraftingHistory
{
    private static List<CraftingProject> _completedProjects = new List<CraftingProject>();

    public static void RecordCompletion(CraftingProject project)
    {
        _completedProjects.Add(project);
        Debug.Log($"[Crafting] History: {_completedProjects.Count} items crafted total");
    }

    public static int TotalItemsCrafted => _completedProjects.Count;
    public static int TotalGoldSpent => _completedProjects.Sum(p => p.GoldCost);
    public static int TotalXPSpent => _completedProjects.Sum(p => p.XPCost);
    public static int TotalDaysSpent => _completedProjects.Sum(p => p.DaysTotal);
}
```

---

## 16. Implementation Phases

### Phase 1: Core Infrastructure (3–4 days)

**Files:** `CraftingFeatType.cs`, `CraftableItemDefinition.cs`, `CraftingCostCalculator.cs`, `CraftingTimeCalculator.cs`, `CraftingValidator.cs`

**Tasks:**
- [ ] Create `CraftingFeatType` enum
- [ ] Create `CraftableItemDefinition` data class
- [ ] Implement `CraftingCostCalculator` with all formulas (scroll, potion, wand, arms, fixed-price)
- [ ] Implement `CraftingTimeCalculator` with day calculations
- [ ] Implement `CraftingValidator` with full validation pipeline
- [ ] Add `SpendXP()` method to `CharacterStats`
- [ ] Add crafting tracking fields to `CharacterStats`
- [ ] Write unit tests for cost calculations
- [ ] Write unit tests for validation logic

**Acceptance:** All cost/time calculations match DMG tables; validation catches all invalid states.

### Phase 2: Feat Activation (1–2 days)

**Files:** `FeatDefinitions.cs`, `FeatManager.cs`

**Tasks:**
- [ ] Remove `IsPlaceholder = true` from all 8 item creation feats
- [ ] Add `GetItemCreationFeats(CharacterStats)` to `FeatManager`
- [ ] Add `GetCasterLevel(CharacterStats)` helper (handles Ranger/Paladin half-CL)
- [ ] Implement Scribe Scroll auto-grant for all spellcasters
- [ ] Verify feat prerequisites (CasterLevel type) work correctly
- [ ] Write tests for feat validation

**Acceptance:** Feats show in character sheet; prerequisites correctly evaluated.

### Phase 3: CraftableItemRegistry — Database Items (3–4 days)

**Files:** `CraftableItemRegistry.cs`, `CraftableItemDefinition.cs`

**Tasks:**
- [ ] Build registry initialization in `CraftableItemRegistry.Init()`
- [ ] Register all wondrous items from `WondrousItemDatabase` with spell prerequisites
- [ ] Register all rods from `RodDatabase` with spell prerequisites
- [ ] Register all rings from `RingDatabase` with spell prerequisites
- [ ] Register all staves from `StaffDatabase` with spell prerequisites
- [ ] Define spell prerequisites for top 50 most common items
- [ ] Define spell prerequisites for remaining items (can use "unknown" fallback)
- [ ] Wire initialization into `SceneBootstrap.cs`

**Acceptance:** Registry returns correct craftable items for each feat type.

### Phase 4: CraftableItemRegistry — Dynamic Items (2–3 days)

**Files:** `CraftableItemRegistry.cs`

**Tasks:**
- [ ] Implement `GetScrollCraftables()` from character known spells
- [ ] Implement `GetPotionCraftables()` with targeting filter
- [ ] Implement `GetWandCraftables()` with level 4 cap
- [ ] Implement scroll/potion/wand item creation (generate ItemData on-the-fly)
- [ ] Handle 0th-level spell pricing (×0.5)
- [ ] Handle caster level selection for scrolls/wands
- [ ] Write tests for dynamic item generation

**Acceptance:** Scrolls/potions/wands correctly generated from known spells with proper pricing.

### Phase 5: CraftingExecutor (2–3 days)

**Files:** `CraftingExecutor.cs`, `CraftingProject.cs`, `GameManager.cs`

**Tasks:**
- [ ] Implement `CraftingExecutor.Execute(CraftingProject)` — main execution method
- [ ] Implement gold deduction (from `ComponentGold`)
- [ ] Implement XP deduction (with level-loss prevention)
- [ ] Implement time advancement (call `GameManager.AdvanceDays()`)
- [ ] Implement daily/weekly/monthly reset triggers during time advancement
- [ ] Implement item creation and inventory addition
- [ ] Implement masterwork base item consumption (arms/armor)
- [ ] Implement item enchantment creation (arms/armor upgrades)
- [ ] Add crafted-item metadata (`IsCraftedByPlayer`, etc.)
- [ ] Implement `CraftingHistory` tracking
- [ ] Add `AdvanceDays()` to `GameManager`
- [ ] Wire rest/reset systems into time advancement
- [ ] Write integration tests for full crafting flow

**Acceptance:** Complete crafting flow works end-to-end — deducts costs, advances time, creates item.

### Phase 6: Crafting Workshop UI — Main Panel (3–4 days)

**Files:** `CraftingWorkshopUI.cs`, `PreCombatHubUI.cs`

**Tasks:**
- [ ] Add "⚒ Crafting Workshop" button to `PreCombatHubUI`
- [ ] Add `_onOpenCraftingWorkshop` callback to `PreCombatHubUI.Open()`
- [ ] Create `CraftingWorkshopUI` fullscreen panel (matching existing UI style)
- [ ] Implement character selector dropdown (party members with creation feats)
- [ ] Implement feat tab bar (only enabled feats)
- [ ] Implement character info display (CL, gold, XP, level)
- [ ] Implement "← Back to Hub" navigation
- [ ] Wire GameManager to open/close CraftingWorkshopUI

**Acceptance:** UI opens from Pre-Combat Hub; character and feat selection works.

### Phase 7: Item Browser Panel (2–3 days)

**Files:** `CraftingItemBrowserPanel.cs`

**Tasks:**
- [ ] Implement scrollable item list from `CraftableItemRegistry`
- [ ] Implement search/filter text field
- [ ] Implement sort options (name, price, level)
- [ ] Implement category filters (subcategories per feat type)
- [ ] Implement item row display (icon, name, cost summary)
- [ ] Implement availability indicators (✅/⚠️/❌)
- [ ] Implement item selection (click to select, highlight)
- [ ] Connect selection to Cost Preview panel

**Acceptance:** Items display correctly per feat; filtering/sorting works; selection triggers preview.

### Phase 8: Cost Preview & Confirmation (2–3 days)

**Files:** `CraftingCostPreviewPanel.cs`, `CraftingConfirmationDialog.cs`

**Tasks:**
- [ ] Implement cost preview panel showing all validation details
- [ ] Display gold cost with availability check
- [ ] Display XP cost with level-loss warning
- [ ] Display time (days) with reset information
- [ ] Display caster level check
- [ ] Display spell prerequisites (per-spell ✅/⚠️/❌)
- [ ] Display additional feat requirements
- [ ] Display masterwork base item (arms/armor)
- [ ] Implement "Begin Crafting" button (enabled only when valid)
- [ ] Implement confirmation dialog with final cost summary
- [ ] Implement crafting progress animation (brief overlay)
- [ ] Implement result notification

**Acceptance:** Full cost preview displays correctly; confirmation flow works; crafting executes.

### Phase 9: Arms & Armor Special UI (2–3 days)

**Files:** `CraftingWorkshopUI.cs` (extend), `CraftingItemBrowserPanel.cs` (extend)

**Tasks:**
- [ ] Implement "Create New / Upgrade / Add Ability" sub-menu
- [ ] Implement masterwork base item selector (from inventory)
- [ ] Implement enhancement level selector (+1 through +5)
- [ ] Implement special ability selector with prerequisite display
- [ ] Implement total bonus display (max +10 rule)
- [ ] Implement upgrade cost calculation (difference pricing)
- [ ] Implement base item consumption confirmation
- [ ] Write tests for arms/armor crafting flow

**Acceptance:** Full arms/armor crafting workflow works including upgrades.

### Phase 10: Polish, Testing, & Documentation (2–3 days)

**Tasks:**
- [ ] Comprehensive play-testing of all 8 feat types
- [ ] Edge case testing (0 gold, max XP spend, level 1 crafter, etc.)
- [ ] UI polish (colors, spacing, tooltip formatting)
- [ ] Error message polish (clear, player-friendly language)
- [ ] Combat log entries for crafting ("Aldric spent 12 days crafting Wand of Fireball")
- [ ] Update Player Guide with crafting section
- [ ] Update FeatDefinitions descriptions (remove [PLACEHOLDER])
- [ ] Performance testing (large item lists)
- [ ] Save/load verification (crafting state persists)

**Acceptance:** All feats fully functional; no crashes; clear UI; documentation updated.

### Phase Summary

| Phase | Description | Estimated Days | Priority |
|:-----:|:-----------|:-------------:|:--------:|
| 1 | Core Infrastructure | 3–4 | 🔴 Critical |
| 2 | Feat Activation | 1–2 | 🔴 Critical |
| 3 | Registry — Database Items | 3–4 | 🔴 Critical |
| 4 | Registry — Dynamic Items | 2–3 | 🔴 Critical |
| 5 | CraftingExecutor | 2–3 | 🔴 Critical |
| 6 | Workshop UI — Main | 3–4 | 🟡 High |
| 7 | Item Browser | 2–3 | 🟡 High |
| 8 | Cost Preview & Confirm | 2–3 | 🟡 High |
| 9 | Arms & Armor Special | 2–3 | 🟡 High |
| 10 | Polish & Testing | 2–3 | 🟡 High |
| | **TOTAL** | **22–32 days** | |

---

## 17. Testing Requirements

### 17.1 Unit Tests

#### Cost Calculator Tests

```csharp
public static class CraftingCostTests
{
    // Scroll costs
    [Test] ScrollOfMagicMissile_CL1_Costs12gp_1xp()      // 1×1×25 = 25 → 12gp, 1xp
    [Test] ScrollOfFireball_CL5_Costs187gp_15xp()          // 3×5×25 = 375 → 187gp, 15xp
    [Test] ScrollOfWish_CL17_Costs1912gp_153xp()           // 9×17×25 = 3825 → 1912gp, 153xp
    [Test] ScrollOf0thLevel_Uses0_5Multiplier()             // 0.5×1×25 = 12gp

    // Potion costs
    [Test] PotionOfCureLightWounds_CL1_Costs25gp()         // 1×1×50 = 50 → 25gp
    [Test] PotionOfFly_CL5_Costs375gp()                    // 3×5×50 = 750 → 375gp

    // Wand costs
    [Test] WandOfCureLightWounds_CL1_Costs375gp()          // 1×1×750 = 750 → 375gp
    [Test] WandOfFireball_CL5_Costs5625gp()                // 3×5×750 = 11250 → 5625gp

    // Arms & Armor costs
    [Test] Plus1Weapon_Costs1000gp()                        // 1²×2000 = 2000 → 1000gp
    [Test] Plus5Weapon_Costs25000gp()                       // 5²×2000 = 50000 → 25000gp
    [Test] Plus1Armor_Costs500gp()                          // 1²×1000 = 1000 → 500gp
    [Test] UpgradeWeaponPlus1ToPlus3_Costs8000gp()          // (9-1)×2000 = 16000 → 8000gp

    // Time calculations
    [Test] ScrollOfFireball_Takes1Day()                     // 375/1000 = 1 (minimum)
    [Test] WandOfFireball_Takes12Days()                     // 11250/1000 = 12 (ceil)
    [Test] Plus5Weapon_Takes50Days()                        // 50000/1000 = 50
    [Test] PotionAlways1Day()                               // Special rule
}
```

#### Validation Tests

```csharp
public static class CraftingValidationTests
{
    // Feat checks
    [Test] CrafterWithoutFeat_Fails()
    [Test] CrafterWithFeat_Passes()
    [Test] AllCastersGetScribeScroll_Free()

    // Caster level checks
    [Test] CL3_CanBrewPotion()
    [Test] CL2_CannotBrewPotion()
    [Test] CL5_CanCraftWand()
    [Test] CL4_CannotCraftWand()
    [Test] CL12_CanForgeRing()
    [Test] CL11_CannotForgeRing()

    // Spell prerequisites
    [Test] KnowsRequiredSpell_Passes()
    [Test] MissingSpell_ShowsSubstitutionDC()
    [Test] PartyMemberHasSpell_Passes()

    // XP safety
    [Test] SufficientXP_Passes()
    [Test] InsufficientXP_Fails()
    [Test] WouldCauseLevelLoss_Fails()
    [Test] ExactlyAtLevelThreshold_Fails()

    // Gold checks
    [Test] SufficientGold_Passes()
    [Test] InsufficientGold_Fails()

    // Arms & Armor special
    [Test] HasMasterworkBase_Passes()
    [Test] NoMasterworkBase_Fails()
    [Test] TotalBonusExceeds10_Fails()
    [Test] UpgradeFromPlus1ToPlus2_Passes()

    // Potion restrictions
    [Test] PersonalSpell_InvalidForPotion()
    [Test] AreaOnlySpell_InvalidForPotion()
    [Test] Level4Spell_InvalidForPotion()

    // Wand restrictions
    [Test] Level5Spell_InvalidForWand()

    // Metamagic rod special
    [Test] MetamagicRod_RequiresMetamagicFeat()
    [Test] MetamagicRod_WithoutFeat_Fails()
}
```

#### Execution Tests

```csharp
public static class CraftingExecutionTests
{
    [Test] Execute_DeductsGold()
    [Test] Execute_DeductsXP()
    [Test] Execute_CreatesItem()
    [Test] Execute_AddsToInventory()
    [Test] Execute_AdvancesTime()
    [Test] Execute_TriggersResets()
    [Test] Execute_ConsumesBase_ArmsArmor()
    [Test] Execute_TracksHistory()
    [Test] Execute_SetsCraftedMetadata()
}
```

### 17.2 Integration Tests

| Test Scenario | Feats Tested | Verification |
|:-------------|:-------------|:-------------|
| Wizard crafts Scroll of Fireball | Scribe Scroll | Scroll in inventory, 187 gp deducted, 15 XP deducted, 1 day passed |
| Cleric brews Potion of Cure Light | Brew Potion | Potion in inventory, 25 gp deducted, 2 XP deducted, 1 day passed |
| Wizard crafts Wand of Magic Missile | Craft Wand | Wand (50 charges) in inventory, 375 gp deducted, 30 XP deducted, 1 day |
| Fighter+Wizard enchants +1 Longsword | Craft Arms | Masterwork consumed, +1 longsword in inventory, 1000 gp, 80 XP, 2 days |
| Wizard upgrades +1 to +3 Sword | Craft Arms | Old +1 consumed, +3 in inventory, cost is difference only |
| Wizard adds Flaming to +1 Sword | Craft Arms | Sword gains Flaming, +2 equiv total, checks Fireball prerequisite |
| Wizard crafts Cloak of Resistance +1 | Craft Wondrous | Cloak in inventory, 500 gp, 40 XP, 1 day, checks Resistance spell |
| Wizard crafts Lesser Rod of Extend | Craft Rod | Rod in inventory, checks Extend Spell feat |
| Wizard crafts Staff of Fire | Craft Staff | Staff (50 charges) in inventory, checks Burning Hands + Fireball + Wall of Fire |
| Wizard forges Ring of Protection +1 | Forge Ring | Ring in inventory, 1000 gp, 80 XP, 2 days, checks Shield of Faith |
| Crafter with 0 gold fails | Any | Validation rejects, no deductions |
| Crafter at XP floor fails | Any | Validation rejects with level-loss warning |
| Non-caster can't craft | Any | No creation feats available in workshop |

### 17.3 UI Tests

| Test | Expected Result |
|:-----|:---------------|
| Open Workshop with no crafters | Button grayed out, "(No crafters)" label |
| Select character updates feat tabs | Only feats the character has are enabled |
| Switch between feat tabs | Item list updates to correct category |
| Search filters items | Only matching items shown |
| Select item shows cost preview | All costs, prereqs, warnings displayed |
| Insufficient resources disables button | "Begin Crafting" grayed out, reason shown |
| Confirmation dialog shows correct totals | Gold/XP/time match preview |
| After crafting, resources updated | Gold, XP, and inventory reflect changes |
| Multiple crafting sessions work | Can craft several items in sequence |
| Back to Hub works | Returns to pre-combat hub correctly |

---

## Appendix A: Spell Prerequisites for Common Items

### Wondrous Items — Top 30

| Item | CL | Required Spells |
|:-----|:--:|:----------------|
| Cloak of Resistance +1-5 | 5 | *Resistance* |
| Headband of Intellect +2/+4/+6 | 8 | *Fox's Cunning* |
| Amulet of Health +2/+4/+6 | 8 | *Bear's Endurance* |
| Gloves of Dexterity +2/+4/+6 | 8 | *Cat's Grace* |
| Belt of Giant Strength +4/+6 | 12 | *Bull's Strength* |
| Periapt of Wisdom +2/+4/+6 | 8 | *Owl's Wisdom* |
| Cloak of Charisma +2/+4/+6 | 8 | *Eagle's Splendor* |
| Bracers of Armor +1-8 | ×2 | *Mage Armor* |
| Amulet of Natural Armor +1-5 | ×3 | *Barkskin* |
| Boots of Speed | 10 | *Haste* |
| Boots of Striding and Springing | 8 | *Longstrider* |
| Bag of Holding I-IV | 9 | *Secret Chest* |
| Handy Haversack | 9 | *Secret Chest* |
| Goggles of Night | 3 | *Darkvision* |
| Eyes of the Eagle | 3 | *Clairaudience/Clairvoyance* |
| Slippers of Spider Climbing | 4 | *Spider Climb* |
| Winged Boots | 8 | *Fly* |
| Pearl of Power (1st-9th) | ×2 | None (but creator must cast spells of the pearl's level) |
| Ioun Stone (Dusty Rose) | 12 | None |
| Ioun Stone (Pale Blue) | 12 | None |
| Carpet of Flying | 10 | *Overland Flight* |
| Portable Hole | 12 | *Plane Shift* |
| Monk's Belt | 10 | *Righteous Might* or monk levels |
| Vest/Shirt of Resistance +1-5 | 5 | *Resistance* |
| Gauntlets of Ogre Power | 6 | *Bull's Strength* |

### Rings — All Implemented

| Ring | CL | Required Spells |
|:-----|:--:|:----------------|
| Ring of Protection +1-5 | 5 | *Shield of Faith* |
| Ring of Feather Falling | 1 | *Feather Fall* |
| Ring of Sustenance | 5 | *Create Food and Water* |
| Ring of Swimming | 2 | None |
| Ring of Climbing | 5 | None |
| Ring of Jumping | 2 | *Jump* |
| Ring of Counterspells | 11 | *Imbue with Spell Ability* |
| Ring of Mind Shielding | 3 | *Nondetection* |
| Ring of Force Shield | 9 | *Wall of Force* |
| Ring of Freedom of Movement | 7 | *Freedom of Movement* |
| Ring of Evasion | 7 | *Jump* |
| Ring of Invisibility | 3 | *Invisibility* |
| Ring of Blinking | 7 | *Blink* |
| Ring of Telekinesis | 9 | *Telekinesis* |
| Ring of the Ram | 9 | *Bull's Strength*, *Telekinesis* |
| Ring of X-Ray Vision | 6 | *True Seeing* |
| Ring of Spell Turning | 13 | *Spell Turning* |
| Ring of Shooting Stars | 12 | *Light*, *Faerie Fire*, *Produce Flame*, *Lightning Bolt* |

---

## Appendix B: Caster Level Quick Reference

| Class | CL Calculation | Notes |
|:------|:---------------|:------|
| Wizard | = Wizard level | Full caster |
| Sorcerer | = Sorcerer level | Full caster |
| Cleric | = Cleric level | Full caster |
| Druid | = Druid level | Full caster |
| Bard | = Bard level | Full caster (for CL purposes) |
| Paladin | = Paladin level − 3 | Half caster; CL 1 at level 4 |
| Ranger | = Ranger level − 3 | Half caster; CL 1 at level 4 |
| Multiclass | = Highest single-class CL | D&D 3.5e uses highest CL |

---

## Appendix C: Maximum Crafting Budget by Level

| Level | XP Total | XP Floor | Max XP Spend | Typical Gold |
|:-----:|:--------:|:--------:|:------------:|:------------:|
| 3 | 3,000+ | 3,000 | ~0-3,000 | 2,700 gp |
| 5 | 10,000+ | 10,000 | ~0-10,000 | 9,000 gp |
| 7 | 21,000+ | 21,000 | ~0-21,000 | 19,000 gp |
| 9 | 36,000+ | 36,000 | ~0-36,000 | 36,000 gp |
| 12 | 66,000+ | 66,000 | ~0-66,000 | 88,000 gp |
| 15 | 105,000+ | 105,000 | ~0-105,000 | 200,000 gp |
| 20 | 190,000+ | 190,000 | ~0-190,000 | 760,000 gp |

**Note:** "Max XP Spend" assumes the character is exactly at level threshold. Characters with XP above the threshold can spend the surplus. Gold values from DMG Table 5-1 (NPC gear value).

---

*This document is the complete implementation specification for the Magic Item Creation Feats system. All phases are designed to be implementable directly from this plan without additional research.*

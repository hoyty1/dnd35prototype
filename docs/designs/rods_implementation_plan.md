# D&D 3.5e Rods — Comprehensive Implementation Plan

> **Source:** Dungeon Master's Guide 3.5e, Pages 234–237  
> **Status:** Pre-Implementation Planning  
> **Previous Sprint:** Ring Implementation (Sprint 3, 91% complete)  
> **Date:** May 2026

---

## RODS OVERVIEW

### Total Rod Count
- **18 unique non-metamagic rods** from DMG/SRD
- **6 metamagic rod types × 3 power levels = 18 metamagic variants**
- **Grand total: 36 individual rod items** (24 unique rod types)

### Price Ranges
| Category | Price Range |
|----------|------------|
| Cheapest | Metamagic Rod of Enlarge/Extend/Silent, Lesser — 3,000 gp |
| Mid-Range | Rod of Wonder — 12,000 gp |
| High-End | Rod of Alertness — 85,000 gp |
| Most Expensive | Metamagic Rod of Quicken, Greater — 170,000 gp |

### Rod Physical Properties (General)
- **Length:** 2–3 feet
- **Weight:** ~5 lbs (Rod of Lordly Might: 10 lbs)
- **Material:** Iron or other metal
- **AC:** 9 | **HP:** 10 | **Hardness:** 10 | **Break DC:** 27
- **Equipment Slot:** Held item (MainHand or OffHand) — NOT worn equipment
- **Default Weapon Use:** Most function as light mace or club when used as weapons
- **Charges:** Most rods do NOT have charges (exceptions: Rod of Negation historically, Rod of Flame Extinguishing with 10 renewable charges)
- **Usability:** Anyone can use a rod (no class restriction unless specified)

### Rod Special Qualities (Random Generation)
| d% Roll | Quality |
|---------|---------|
| 01 | Intelligent rod (cannot have charges) |
| 02–31 | Design/inscription hints at function |
| 32–100 | No special qualities |

---

## COMPLETE ROD CATALOG

### Random Generation Table (DMG Table 7-16)

| d% (Medium) | d% (Major) | Rod | Market Price |
|:--|:--|:--|:--|
| 01–07 | — | Metamagic, Enlarge, lesser | 3,000 gp |
| 08–14 | — | Metamagic, Extend, lesser | 3,000 gp |
| 15–21 | — | Metamagic, Silent, lesser | 3,000 gp |
| 22–28 | — | Immovable Rod | 5,000 gp |
| 29–35 | — | Metamagic, Empower, lesser | 9,000 gp |
| 36–42 | — | Metal and Mineral Detection | 10,500 gp |
| 43–53 | 01–04 | Cancellation | 11,000 gp |
| 54–57 | 05–06 | Metamagic, Enlarge | 11,000 gp |
| 58–61 | 07–08 | Metamagic, Extend | 11,000 gp |
| 62–65 | 09–10 | Metamagic, Silent | 11,000 gp |
| 66–71 | 11–14 | Wonder | 12,000 gp |
| 72–79 | 15–18 | Python | 13,000 gp |
| 80–83 | — | Metamagic, Maximize, lesser | 14,000 gp |
| 84–89 | 19–21 | Flame Extinguishing | 15,000 gp |
| 90–97 | 22–25 | Viper | 19,000 gp |
| — | 26–30 | Enemy Detection | 23,500 gp |
| — | 31–36 | Metamagic, Enlarge, greater | 24,500 gp |
| — | 37–42 | Metamagic, Extend, greater | 24,500 gp |
| — | 43–48 | Metamagic, Silent, greater | 24,500 gp |
| — | 49–53 | Splendor | 25,000 gp |
| — | 54–58 | Withering | 25,000 gp |
| 98–99 | 59–64 | Metamagic, Empower | 32,500 gp |
| — | 65–69 | Thunder and Lightning | 33,000 gp |
| 100 | 70–73 | Metamagic, Quicken, lesser | 35,000 gp |
| — | 74–77 | Negation | 37,000 gp |
| — | 78–80 | Absorption | 50,000 gp |
| — | 81–84 | Flailing | 50,000 gp |
| — | 85–86 | Metamagic, Maximize | 54,000 gp |
| — | 87–88 | Rulership | 60,000 gp |
| — | 89–90 | Security | 61,000 gp |
| — | 91–92 | Lordly Might | 70,000 gp |
| — | 93–94 | Metamagic, Empower, greater | 73,000 gp |
| — | 95–96 | Metamagic, Quicken | 75,500 gp |
| — | 97–98 | Alertness | 85,000 gp |
| — | 99 | Metamagic, Maximize, greater | 121,500 gp |
| — | 100 | Metamagic, Quicken, greater | 170,000 gp |

---

## ROD CATEGORIES

### Category 1: Metamagic Rods (6 types × 3 levels = 18 variants)

See **metamagic_rods_specification.md** for full details.

| Type | Lesser (≤3rd) | Normal (≤6th) | Greater (≤9th) |
|------|:---:|:---:|:---:|
| **Empower** | 9,000 gp | 32,500 gp | 73,000 gp |
| **Enlarge** | 3,000 gp | 11,000 gp | 24,500 gp |
| **Extend** | 3,000 gp | 11,000 gp | 24,500 gp |
| **Maximize** | 14,000 gp | 54,000 gp | 121,500 gp |
| **Quicken** | 35,000 gp | 75,500 gp | 170,000 gp |
| **Silent** | 3,000 gp | 11,000 gp | 24,500 gp |

- **All:** CL 17th, Strong (no school), 3 uses/day
- **Activation:** Use-activated (apply when casting spell)
- **Restriction:** One metamagic rod per spell; can combine with wielder's own metamagic feats
- **Sorcerer Note:** Full-round action still required (same as spontaneous metamagic)

> **Note:** The DMG/SRD includes 6 metamagic rod types (Empower, Enlarge, Extend, Maximize, Quicken, Silent). The "Widen" metamagic rod is NOT in the core DMG — it appears in supplemental material only. We implement the core 6.

### Category 2: Combat Rods (5 rods)

#### Rod of Flailing — 50,000 gp
- **CL:** 9th | **Aura:** Moderate enchantment
- **Activation:** Command word (move action to transform)
- **Effect:** Transforms into +3 dire flail (double weapon, extra attack at -2)
- **Special:** Once/day as free action: +4 deflection AC, +4 resistance to saves for 10 minutes (works even in rod form)
- **Charges:** None
- **Daily Limits:** Defensive ability 1/day; weapon transformation unlimited
- **Crafting:** Craft Rod, Craft Magic Arms and Armor, *bless*

#### Rod of the Viper — 19,000 gp
- **CL:** 10th | **Aura:** Moderate necromancy
- **Activation:** Command word
- **Effect:** Functions as +2 heavy mace; head transforms into serpent 1/day for 10 min
- **Special:** While serpent-active, successful strikes poison target: 1d10 Con damage immediately + 1d10 Con damage 1 min later (Fort DC 14 negates each)
- **Restriction:** Only functions for evil wielders
- **Daily Limits:** Serpent transformation 1/day
- **Crafting:** Craft Rod, Craft Magic Arms and Armor, *poison*; creator must be evil

#### Rod of Python — 13,000 gp
- **CL:** 10th | **Aura:** Moderate transmutation
- **Activation:** Command word (standard action)
- **Effect:** Transforms into constrictor snake (3/day, 10 min each) or giant constrictor (1/week, 10 min)
- **Snake Stats (Constrictor):** HD 3d8+6, HP 19, Attack +5 melee (1d3+4 + grab), Constrict 1d3+4
- **Special:** Snake obeys wielder's telepathic commands; if killed → rod form, unusable 24 hrs
- **Daily Limits:** Constrictor 3/day, Giant Constrictor 1/week
- **Crafting:** Craft Rod, *animate objects*, *polymorph*

#### Rod of Cancellation — 11,000 gp
- **CL:** 17th | **Aura:** Strong abjuration
- **Activation:** Touch (melee touch attack)
- **Effect:** Drains ALL magical properties from touched item
- **Special:** Target item makes DC 23 Will save (or holder's Will if better); rod becomes brittle and useless after one use; drained items only restored by *wish*/*miracle*
- **Charges:** ONE USE ONLY — rod is destroyed after use
- **Crafting:** Craft Rod, *Mordenkainen's disjunction*

#### Rod of Withering — 25,000 gp
- **CL:** 13th | **Aura:** Strong necromancy
- **Activation:** Touch attack (melee touch)
- **Effect:** Functions as +1 light mace; deals no HP damage, instead deals 1d4 Strength damage + 1d4 Constitution damage on successful touch attack
- **Daily Limits:** None specified (unlimited touch attacks per RAW)
- **Crafting:** Craft Rod, *contagion*

### Category 3: Utility Rods (10 rods)

#### Immovable Rod — 5,000 gp
- **CL:** 10th | **Aura:** Moderate transmutation
- **Activation:** Button press (move action)
- **Effect:** Rod becomes fixed in space, defying gravity
- **Special:** Supports up to 8,000 lbs before falling; DC 30 Strength check to move 10 ft in 1 round; deactivate by pressing button again
- **Uses:** Unlimited
- **Crafting:** Craft Rod, *levitate*

#### Rod of Alertness — 85,000 gp
- **CL:** 11th | **Aura:** Moderate abjuration, divination, enchantment, evocation
- **Activation:** Multiple (passive + standard action for active abilities)
- **Passive:** Functions as +1 light mace; +1 insight bonus on initiative checks
- **At-Will Abilities (standard action each):** *detect evil*, *detect good*, *detect chaos*, *detect law*, *detect magic*, *discern lies*, *light*, *see invisibility*
- **Alertness Mode (1/day, standard action):** Plant in ground → detects hostile creatures within 120 ft, 8 flanges each cast *light* (60 ft range), *prayer* on allies within 20 ft, mental warning to wielder; lasts 10 minutes
- **Animate Objects (1/day):** Animates up to 8 Small objects within 5 ft radius for 11 rounds
- **Crafting:** Craft Rod, *alarm*, *detect chaos/evil/good/law/magic*, *discern lies*, *light*, *see invisibility*, *prayer*, *animate objects*

#### Rod of Enemy Detection — 23,500 gp
- **CL:** 10th | **Aura:** Moderate divination
- **Activation:** Standard action
- **Effect:** Pulses and points toward nearest hostile creature within 60 ft; detects invisible, ethereal, hidden, disguised enemies; full-round concentration pinpoints nearest + indicates total count
- **Daily Limits:** 3/day, each use up to 10 minutes
- **Crafting:** Craft Rod, *true seeing*

#### Rod of Flame Extinguishing — 15,000 gp
- **CL:** 12th | **Aura:** Strong transmutation
- **Activation:** Standard action (touch)
- **Charge-Based Effects:**
  - **Free:** Extinguish Medium or smaller nonmagical fire with touch
  - **1 charge:** Extinguish Large+ nonmagical fire, or Medium/smaller magic fire; suppresses continuous magic flames 6 rounds; counter instantaneous fire spells (readied action)
  - **2 charges:** Extinguish Large+ magical fire (e.g., *fireball*, *wall of fire*)
  - **3 charges:** Deal 6d6 damage to fire creature (melee touch attack)
- **Charges:** 10, renewed daily (up to 10/day)
- **Crafting:** Craft Rod, *pyrotechnics*

#### Rod of Metal and Mineral Detection — 10,500 gp
- **CL:** 9th | **Aura:** Moderate divination
- **Activation:** Full-round action (concentration)
- **Effect:** Pulses and points to largest mass of metal within 30 ft; can focus on specific metal/mineral to find location and approximate quantity
- **Uses:** Unlimited
- **Crafting:** Craft Rod, *locate object*

#### Rod of Negation — 37,000 gp
- **CL:** 15th | **Aura:** Strong varied
- **Activation:** Standard action (ranged touch attack — ray)
- **Effect:** Negates spell/spell-like functions of magic items; functions like *greater dispel magic* against item's magical properties; cannot negate artifacts
- **Special:** For instantaneous effects, must ready action; no saving throw for target item
- **Daily Limits:** 3/day
- **Crafting:** Craft Rod, *dispel magic*, *limited wish* or *miracle*

#### Rod of Rulership — 60,000 gp
- **CL:** 20th | **Aura:** Strong enchantment
- **Activation:** Standard action
- **Effect:** Command obedience/fealty from creatures within 120 ft; affects up to 300 HD of creatures; Int 12+ gets Will DC 16 save
- **Special:** Creatures obey as absolute sovereign; magic breaks if command contradicts nature; rod crumbles after 500 minutes total use (non-continuous)
- **Crafting:** Craft Rod, *mass charm monster*

#### Rod of Security — 61,000 gp
- **CL:** 20th | **Aura:** Strong conjuration
- **Activation:** Standard action
- **Effect:** Creates nondimensional safe space for wielder + up to 199 other creatures; natural healing at 5× normal rate
- **Duration:** 200 person-days total (200 days ÷ number of creatures); non-continuous; rod becomes nonmagical when used up
- **Crafting:** Craft Rod, *gate*

#### Rod of Splendor — 25,000 gp
- **CL:** 12th | **Aura:** Strong conjuration, transmutation
- **Passive:** +4 enhancement bonus to Charisma while held/carried
- **Active Abilities:**
  - **Apparel (1/day):** Creates finest clothing (7,000–10,000 gp apparent value), lasts 12 hours; vanishes if sold/given away/used as components
  - **Palatial Tent (1/week):** Creates 60 ft silk pavilion with furnishings and food for 100 people, lasts 1 day
- **Crafting:** Craft Rod, *eagle's splendor*, *fabricate*, *major creation*

#### Rod of Thunder and Lightning — 33,000 gp
- **CL:** 9th | **Aura:** Moderate evocation
- **Base:** Functions as +2 light mace
- **Five Abilities:**
  1. **Thunder (1/day):** Free action — next strike is +3 light mace, stuns opponent (Fort DC 13 negates)
  2. **Lightning (1/day):** Free action — next strike deals normal +2 damage + 2d6 electricity (touch attack if normal miss)
  3. **Thunderclap (1/day):** Standard action — 2d6 sonic damage, deafens 2d6 rounds (DC 14)
  4. **Lightning Stroke (1/day):** Standard action — 5 ft wide lightning bolt, 9d6 damage, 200 ft range (Reflex DC 14 half)
  5. **Thunder & Lightning Combined (1/week):** Standard action — thunderclap + forked lightning bolt; 9d6 lightning (1s and 2s count as 3s) + 2d6 sonic; single Reflex DC 14 for half of both + deafness
- **Crafting:** Craft Rod, Craft Magic Arms and Armor, *lightning bolt*, *shout*

### Category 4: Legendary/Complex Rods (2 rods)

#### Rod of Absorption — 50,000 gp
- **CL:** 15th | **Aura:** Strong abjuration
- **Activation:** Readied action (passive absorption)
- **Effect:** Draws single-target spells and ray-based spell-like abilities directed at wielder, nullifying effect and storing spell levels
- **Storage:** Maximum 50 spell levels; cannot be recharged
- **Conversion:** Wielder converts stored levels to cast prepared spells without expending preparation (bards/sorcerers: cast known spells of appropriate level)
- **New Rod Status:** Roll d% ÷ 2 for remaining absorption capacity; roll d% again — on 71–100, half the absorbed levels are still stored from previous use
- **Crafting:** Craft Rod, *spell turning*

#### Rod of Lordly Might — 70,000 gp
- **CL:** 19th | **Aura:** Strong enchantment, evocation, necromancy, transmutation
- **Physical:** Thick metal rod, flanged ball at one end, six buttons, weighs 10 lbs
- **Spell-Like Functions (1/day each):**
  1. *Hold person* on touch — Will DC 14 negates (melee touch attack)
  2. *Fear* — all enemies within 10 ft, Will DC 16 partial
  3. Deal 2d4 HP damage on touch + heal wielder same amount — Will DC 17 half (melee touch attack)
- **Weapon Forms (unlimited switching):**
  1. **Default:** +2 light mace
  2. **Button 1:** +1 flaming longsword (4 ft)
  3. **Button 2:** +4 battleaxe (4 ft)
  4. **Button 3:** +3 shortspear or +3 longspear (6–15 ft; lance at 15 ft)
- **Utility Functions (unlimited):**
  - **Button 4:** Climbing pole — spike + 3 hooks, extends 5–50 ft, fold-out horizontal bars; supports 4,000 lbs
  - **Button 5:** Retract climbing pole
  - **Button 6:** Compass — indicates magnetic north, approximate depth/height underground
  - **Door Forcing:** Plant base and extend — Strength modifier +12
- **Crafting:** Craft Rod, Craft Magic Arms and Armor, *inflict light wounds*, *bull's strength*, *flame blade*, *hold person*, *fear*

### Category 5: Rod of Wonder (Special — Random Effects)

#### Rod of Wonder — 12,000 gp
- **CL:** 10th | **Aura:** Moderate enchantment
- **Activation:** Standard action, point at target
- **Range:** 60 ft (varies by effect)
- **Effect:** Roll d100 on the Wondrous Effects table
- **Uses:** Unlimited
- **Crafting:** Craft Rod, *confusion*; creator must be chaotic

**Wondrous Effects Table (d100):**

| d% | Effect |
|:--|:--|
| 01–05 | *Slow* creature pointed at for 10 rounds (Will DC 15 negates) |
| 06–10 | *Faerie fire* surrounds the target |
| 11–15 | Deludes wielder for 1 round into believing rod functions as indicated by second d% roll (no save) |
| 16–20 | *Gust of wind* at windstorm force (Fort DC 14 negates) |
| 21–25 | Wielder learns target's surface thoughts (*detect thoughts*) for 1d4 rounds (no save) |
| 26–30 | *Stinking cloud* at 30 ft range (Fort DC 15 negates) |
| 31–33 | Heavy rain falls for 1 round in 60 ft radius centered on wielder |
| 34–36 | Summon animal: rhino (01–25), elephant (26–50), or mouse (51–100) |
| 37–46 | *Lightning bolt* (70 ft long, 5 ft wide), 6d6 damage (Reflex DC 15 half) |
| 47–49 | 600 large butterflies pour forth for 2 rounds, blind everyone within 25 ft (Reflex DC 14 negates) |
| 50–53 | *Enlarge person* on target within 60 ft (Fort DC 13 negates) |
| 54–58 | *Darkness*, 30 ft diameter hemisphere, centered 30 ft from rod |
| 59–62 | Grass grows in 160 sq ft area, or existing grass grows 10× normal |
| 63–65 | Turn ethereal any nonliving object up to 1,000 lbs / 30 cu ft |
| 66–69 | Reduce wielder to 1/12 height (no save) |
| 70–79 | *Fireball* at target or 100 ft ahead, 6d6 damage (Reflex DC 15 half) |
| 80–84 | *Invisibility* on rod wielder |
| 85–87 | Leaves grow from target within 60 ft, last 24 hours |
| 88–90 | 10–40 gems (1 gp each) shoot 30 ft stream, 1 dmg each; 5d4 hits divided among targets |
| 91–95 | Shimmering colors over 40×30 ft area; blind 1d6 rounds (Fort DC 15 negates) |
| 96–97 | Wielder (50%) or target (50%) turns permanently blue, green, or purple (no save) |
| 98–100 | *Flesh to stone* (or *stone to flesh* if stone) within 60 ft (Fort DC 18 negates) |

---

## ROD COMPLEXITY TIERS

See **rods_by_complexity.md** for the full tier classification document.

### Summary

| Tier | Stars | Description | Rod Count |
|------|-------|-------------|-----------|
| 1 | ⭐ | Simple/Passive | 2 |
| 2 | ⭐⭐ | Single Active Ability | 5 |
| 3 | ⭐⭐⭐ | Multi-Use / Metamagic | 25 |
| 4 | ⭐⭐⭐⭐ | Multi-Ability Complex | 4 |

---

## NEW SYSTEMS REQUIRED

### SYSTEM 1: Rod Equipment & Activation Framework

**Purpose:** Rods are held items that must be in-hand to use. Some function as weapons.

**Components:**
```csharp
public enum EquipSlot
{
    // Existing slots...
    MainHand,
    OffHand,
    // Rods occupy MainHand or OffHand
}

public enum RodActivationType
{
    Passive,        // Always on when held (Alertness initiative bonus, Splendor CHA)
    CommandWord,    // Standard action to activate (Python, Flailing transform)
    UseActivated,   // Apply during another action (Metamagic rods during casting)
    ButtonPress,    // Move action (Immovable Rod)
    TouchAttack,    // Melee touch attack (Cancellation, Withering)
    PointAndActivate // Standard action, aim at target (Wonder, Enemy Detection)
}

public class RodData
{
    public string RodName;
    public int MarketPrice;
    public int CasterLevel;
    public RodActivationType ActivationType;
    public int Weight = 5; // Default 5 lbs
    public bool FunctionsAsWeapon;
    public string WeaponType; // "LightMace", "HeavyMace", "DireFlail", etc.
    public int WeaponEnhancementBonus;
    public int UsesPerDay = -1; // -1 = unlimited
    public int MaxCharges = -1; // -1 = no charges
    public bool IsEvil; // Alignment restriction (Viper)
}
```

**Implementation Notes:**
- Rod must be held to activate abilities (check equipment slot)
- Rods that function as weapons use existing weapon attack system
- Some rods transform between rod/weapon forms (move or command action)
- Cannot use two rods simultaneously (one per hand, most require active wielding)

**Time:** 2–3 days

### SYSTEM 2: Metamagic Rod System

**Purpose:** Apply metamagic feats to spells without increasing slot level.

See **metamagic_rods_specification.md** for complete system design.

```csharp
public enum MetamagicType
{
    Empower,    // Numeric effects +50%
    Enlarge,    // Double range
    Extend,     // Double duration
    Maximize,   // All numeric values maximized
    Quicken,    // Cast as swift action
    Silent      // No verbal component required
}

public enum MetamagicRodTier
{
    Lesser,  // Spells ≤ 3rd level
    Normal,  // Spells ≤ 6th level
    Greater  // Spells ≤ 9th level
}

public class MetamagicRodEffect
{
    public MetamagicType Type;
    public MetamagicRodTier Tier;
    public int MaxSpellLevel; // 3, 6, or 9
    public int UsesPerDay = 3;
    public int UsesToday = 0;

    public bool CanApplyToSpell(int spellLevel)
    {
        return spellLevel <= MaxSpellLevel && UsesToday < UsesPerDay;
    }

    public void ApplyMetamagic()
    {
        UsesToday++;
    }

    public void ResetOnRest()
    {
        UsesToday = 0;
    }
}
```

**Integration Points:**
- Spell casting UI: show available metamagic options from held rod
- Apply metamagic effect without changing spell slot
- Track daily uses (3/day per rod)
- Sorcerers: still full-round action with metamagic rod
- Cannot combine two metamagic rods on one spell
- CAN combine rod + wielder's own metamagic feat (only feat raises slot)
- Reset uses on long rest

**Time:** 5–7 days

### SYSTEM 3: Rod of Wonder Random Effects Engine

**Purpose:** Implement the 100-entry random effects table.

```csharp
public class RodOfWonderManager
{
    public struct WonderEffect
    {
        public int MinRoll;
        public int MaxRoll;
        public string EffectName;
        public string Description;
        public Action<Character, Vector2Int> Execute;
    }

    private static List<WonderEffect> _effectTable;

    public static WonderEffect TriggerRandomEffect(Character wielder, Vector2Int target)
    {
        int roll = Random.Range(1, 101); // d100
        var effect = _effectTable.First(e => roll >= e.MinRoll && roll <= e.MaxRoll);
        effect.Execute?.Invoke(wielder, target);
        return effect;
    }
}
```

**Effect Categories to Implement:**
- **Spell Effects:** *Slow*, *Faerie Fire*, *Lightning Bolt*, *Fireball*, *Enlarge Person*, *Darkness*, *Invisibility*, *Stinking Cloud*, *Gust of Wind*, *Flesh to Stone*, *Detect Thoughts*
- **Summoning:** Rhino, Elephant, Mouse
- **Environmental:** Rain, Grass Growth, Shimmering Colors
- **Conjuration:** Butterflies (blind), Gems (damage stream)
- **Transmutation:** Reduce wielder, Leaves on target, Ethereal object, Color change
- **Meta:** Delusion effect (fake result)

**Time:** 3–4 days

### SYSTEM 4: Rod of Absorption — Spell Absorption System

**Purpose:** Absorb incoming spells and convert stored levels to spell slots.

```csharp
public class RodOfAbsorptionEffect
{
    public int MaxStorageLevels = 50;
    public int CurrentAbsorbed = 0;   // Levels absorbed (capacity used)
    public int StoredLevels = 0;       // Levels available for conversion
    public int RemainingCapacity => MaxStorageLevels - CurrentAbsorbed;

    /// <summary>
    /// Attempt to absorb an incoming single-target spell or ray.
    /// Only works on spells targeting the wielder directly.
    /// </summary>
    public bool AbsorbSpell(int spellLevel)
    {
        if (spellLevel > RemainingCapacity)
            return false; // Cannot absorb — rod is full

        CurrentAbsorbed += spellLevel;
        StoredLevels += spellLevel;
        return true; // Spell nullified
    }

    /// <summary>
    /// Convert stored levels to regain a spell slot.
    /// </summary>
    public bool ConvertToSpellSlot(Character wielder, int slotLevel)
    {
        if (StoredLevels < slotLevel)
            return false;

        StoredLevels -= slotLevel;
        // Restore spell slot for prepared casters
        // OR allow known-spell cast for spontaneous casters
        wielder.RestoreSpellSlot(slotLevel);
        return true;
    }

    /// <summary>
    /// Initialize a found rod with random remaining capacity.
    /// </summary>
    public void InitializeFound()
    {
        int capacityPercent = Random.Range(1, 101);
        CurrentAbsorbed = MaxStorageLevels - (capacityPercent / 2);

        int storedRoll = Random.Range(1, 101);
        if (storedRoll >= 71)
            StoredLevels = CurrentAbsorbed / 2;
        else
            StoredLevels = 0;
    }
}
```

**Mechanics:**
- Readied action to absorb (or auto-absorb on directed spells)
- Only absorbs single-target spells and rays aimed at wielder
- Rod cannot be recharged — once 50 levels absorbed, no more absorption
- Stored levels can be converted to spell slots at any time
- Track both "absorption capacity used" and "stored levels available"

**Time:** 2–3 days

### SYSTEM 5: Multi-Form Rod System (Lordly Might)

**Purpose:** Rod that transforms between multiple weapon/utility forms.

```csharp
public enum LordlyMightForm
{
    Mace,           // +2 light mace (default)
    FlamingSword,   // +1 flaming longsword
    Battleaxe,      // +4 battleaxe
    Spear,          // +3 shortspear / longspear
    ClimbingPole,   // 5-50 ft pole with rungs
    Compass         // Indicates north / depth / height
}

public class RodOfLordlyMight
{
    public LordlyMightForm CurrentForm = LordlyMightForm.Mace;
    public int HoldPersonUsesToday = 0;  // 1/day
    public int FearUsesToday = 0;        // 1/day
    public int DrainHealUsesToday = 0;   // 1/day

    public WeaponStats GetCurrentWeaponStats()
    {
        return CurrentForm switch
        {
            LordlyMightForm.Mace => new WeaponStats("Light Mace", +2, "1d6"),
            LordlyMightForm.FlamingSword => new WeaponStats("Longsword", +1, "1d8", flaming: true),
            LordlyMightForm.Battleaxe => new WeaponStats("Battleaxe", +4, "1d8"),
            LordlyMightForm.Spear => new WeaponStats("Shortspear", +3, "1d6"),
            _ => null
        };
    }

    public void SwitchForm(LordlyMightForm newForm)
    {
        CurrentForm = newForm;
        // Update weapon stats, UI, etc.
    }
}
```

**Complexity:**
- 4 weapon forms with different stats
- 2 utility forms (climbing pole, compass)
- 3 spell-like abilities (1/day each)
- Door-forcing mechanic (Str +12)
- Climbing pole supports 4,000 lbs, extends 5–50 ft

**Time:** 3–4 days

### SYSTEM 6: Creature Summoning (Python Rod)

**Purpose:** Rod transforms into a creature ally.

```csharp
public class SummonedRodCreature
{
    public string CreatureName; // "Constrictor Snake", "Giant Constrictor Snake"
    public int HD;
    public int HP;
    public int AttackBonus;
    public string Damage;
    public bool HasGrab;
    public string ConstrictDamage;
    public int DurationRounds; // 100 (10 minutes)
    public bool IsAlive = true;

    // If killed, rod unusable for 24 hours
    public bool RodDisabledUntilRest = false;
}
```

**Time:** 2 days

---

## IMPLEMENTATION TIMELINE

### **PHASE 1: Foundation (1 week)**
- [ ] Rod equipment slot system (MainHand/OffHand)
- [ ] RodFactory and RodDatabase
- [ ] RodNames enum (all 36 rod items)
- [ ] Basic activation framework (command word, button, touch, etc.)
- [ ] Rod UI panel (show held rod abilities, uses remaining)
- [ ] Daily use tracking + reset on rest
- [ ] Save/load rod state

### **PHASE 2: Simple Rods (1 week)**
- [ ] Immovable Rod (button → fixed in space, 8,000 lb limit, DC 30 to move)
- [ ] Rod of Metal and Mineral Detection (locate metal within 30 ft)
- [ ] Rod of Enemy Detection (detect hostiles within 60 ft, 3/day)
- [ ] Rod of Flame Extinguishing (charge-based fire suppression)
- [ ] Rod of Splendor (+4 CHA passive, apparel/tent creation)

### **PHASE 3: Metamagic Rods (2 weeks)**
- [ ] Metamagic application system core
- [ ] Spell casting UI integration (show metamagic options)
- [ ] Empower implementation (+50% numeric effects)
- [ ] Enlarge implementation (double range)
- [ ] Extend implementation (double duration)
- [ ] Maximize implementation (max all numeric values)
- [ ] Quicken implementation (swift action casting)
- [ ] Silent implementation (remove verbal component)
- [ ] Lesser/Normal/Greater tier restrictions
- [ ] Daily use tracking (3/day each rod)
- [ ] Integration tests: 6 types × 3 tiers = 18 variants

### **PHASE 4: Combat Rods (1 week)**
- [ ] Rod of Flailing (+3 dire flail, +4 AC/saves 1/day)
- [ ] Rod of Python (transform to constrictor snake, creature AI)
- [ ] Rod of the Viper (+2 heavy mace, poison 1/day, evil-only)
- [ ] Rod of Cancellation (drain magic item, one-use, destroy rod)
- [ ] Rod of Withering (Str + Con damage on touch)

### **PHASE 5: Complex Utility Rods (1.5 weeks)**
- [ ] Rod of Wonder (100 random effects table)
- [ ] Rod of Alertness (passive bonuses + 8 at-will detects + alertness mode + animate)
- [ ] Rod of Rulership (mass charm, 300 HD, 500 min total)
- [ ] Rod of Thunder and Lightning (5 distinct abilities)
- [ ] Rod of Negation (dispel magic items, 3/day)
- [ ] Rod of Security (nondimensional safe space)

### **PHASE 6: Legendary Rods (1 week)**
- [ ] Rod of Absorption (absorb spells, store levels, convert to slots)
- [ ] Rod of Lordly Might (6 forms, 3 spell-likes, door forcing, compass)

### **Total Estimated Time: 7.5 weeks**

---

## DETAILED ROD SPECIFICATIONS — QUICK REFERENCE

### All 18 Non-Metamagic Rods (sorted by price)

| # | Rod | Price | CL | Activation | Key Mechanic |
|---|:----|------:|:--:|:-----------|:-------------|
| 1 | Immovable Rod | 5,000 | 10 | Button (move) | Fixed in space, 8,000 lb |
| 2 | Rod of Metal & Mineral Detection | 10,500 | 9 | Full-round | Detect metal 30 ft |
| 3 | Rod of Cancellation | 11,000 | 17 | Touch attack | Drain item, one-use |
| 4 | Rod of Wonder | 12,000 | 10 | Standard | d100 random effect |
| 5 | Rod of Python | 13,000 | 10 | Command word | Snake ally 3/day |
| 6 | Rod of Flame Extinguishing | 15,000 | 12 | Standard/touch | 10 charges/day, fire suppression |
| 7 | Rod of the Viper | 19,000 | 10 | Command word | +2 mace, poison 1/day, evil |
| 8 | Rod of Enemy Detection | 23,500 | 10 | Standard | Detect hostiles 60 ft, 3/day |
| 9 | Rod of Splendor | 25,000 | 12 | Passive + daily | +4 CHA, apparel, tent |
| 10 | Rod of Withering | 25,000 | 13 | Touch attack | 1d4 STR + 1d4 CON dmg |
| 11 | Rod of Thunder & Lightning | 33,000 | 9 | Multiple | +2 mace, 5 abilities |
| 12 | Rod of Negation | 37,000 | 15 | Standard (ray) | Dispel item magic, 3/day |
| 13 | Rod of Absorption | 50,000 | 15 | Readied | Absorb spells, max 50 levels |
| 14 | Rod of Flailing | 50,000 | 9 | Command word | +3 dire flail, +4 AC 1/day |
| 15 | Rod of Rulership | 60,000 | 20 | Standard | Mass charm 300 HD, 500 min |
| 16 | Rod of Security | 61,000 | 20 | Standard | Safe demiplane, 200 person-days |
| 17 | Rod of Lordly Might | 70,000 | 19 | Buttons/commands | 4 weapons + 3 spell-likes + utility |
| 18 | Rod of Alertness | 85,000 | 11 | Multiple | +1 mace, +1 init, 8 detects, alertness |

---

## PRIORITY MATRIX

See **rods_priority_matrix.md** for the full impact vs. complexity analysis.

### Summary — Build Order Recommendation

| Priority | Rods | Rationale |
|----------|------|-----------|
| **P1 — Core** | Immovable Rod, Metamagic Rods (all 18) | Iconic items; metamagic is core caster mechanic |
| **P2 — High** | Rod of Cancellation, Negation, Flailing, Viper, Withering | Combat utility, relatively straightforward |
| **P3 — Medium** | Rod of Wonder, Thunder & Lightning, Python, Enemy Detection | Moderate complexity, high fun factor |
| **P4 — Lower** | Alertness, Rulership, Absorption, Lordly Might | Complex multi-ability implementations |
| **P5 — Niche** | Security, Splendor, Flame Extinguishing, Metal & Mineral Detection | Situational use, lower combat impact |

---

## TESTING PLAN

### Per-Rod Standard Tests
- [ ] Equip rod in MainHand → verify held
- [ ] Activate each ability → verify effect
- [ ] Track uses/charges → verify decrement
- [ ] Rest → verify daily use reset
- [ ] Save game → load game → verify rod state preserved
- [ ] Unequip rod → verify abilities deactivate

### Metamagic Rod Tests
- [ ] Wizard casts Fireball with Rod of Empower, Lesser → damage ×1.5
- [ ] Verify 3rd-level slot consumed (not 5th)
- [ ] Use count shows 2/3 remaining
- [ ] Try to use with 4th-level spell + Lesser rod → blocked
- [ ] Cast 3 spells with metamagic → 0/3 remaining → 4th attempt blocked
- [ ] Long rest → uses reset to 3/3
- [ ] Try two metamagic rods on one spell → blocked
- [ ] Metamagic rod + own feat → rod doesn't raise slot, feat does

### Immovable Rod Tests
- [ ] Press button → rod fixed in space (doesn't fall)
- [ ] Apply 8,000 lbs force → rod holds
- [ ] Apply 8,001+ lbs → rod falls
- [ ] DC 30 Strength check → rod moves 10 ft
- [ ] Press button again → rod released

### Rod of Absorption Tests
- [ ] Enemy casts single-target spell at wielder → absorbed
- [ ] Spell levels stored correctly (e.g., Fireball = 3 levels)
- [ ] Wielder converts 3 stored levels → regains 3rd-level slot
- [ ] Fill to 50 levels → no more absorption
- [ ] AoE spell → NOT absorbed (only single-target/ray)

### Rod of Wonder Tests
- [ ] Activate → roll d100 → correct effect triggers
- [ ] Multiple activations → different rolls/effects
- [ ] Verify each of the 20 effect categories works
- [ ] Effects that target wielder work correctly
- [ ] Save DCs match table values

### Rod of Lordly Might Tests
- [ ] Default form = +2 light mace stats
- [ ] Button 1 → +1 flaming longsword, fire damage
- [ ] Button 2 → +4 battleaxe stats
- [ ] Button 3 → +3 spear stats
- [ ] Hold person ability (1/day) → touch, Will DC 14
- [ ] Fear ability (1/day) → 10 ft, Will DC 16
- [ ] Drain/heal ability (1/day) → 2d4, Will DC 17
- [ ] Climbing pole extends 5–50 ft
- [ ] Compass shows north/depth

---

## SUCCESS CRITERIA

Complete when:
- [ ] All 36 rod items implemented and functional
- [ ] Rod equipment slot system (held items) operational
- [ ] Metamagic rod system fully integrated with spell casting
- [ ] Daily use tracking + rest reset working
- [ ] Charge tracking working (Flame Extinguishing)
- [ ] One-use rod destruction (Cancellation)
- [ ] Limited-use rod depletion (Rulership 500 min, Security 200 days)
- [ ] Rod of Wonder all 20 effect categories triggering correctly
- [ ] Rod of Absorption spell absorption + conversion working
- [ ] Rod of Lordly Might all 6 forms + 3 spell-likes functional
- [ ] Creature summoning (Python) with basic AI
- [ ] UI displays rod abilities, uses remaining, charges
- [ ] Save/load preserves all rod state
- [ ] All tests pass

---

## ESTIMATED DELIVERABLES

### Code Files
1. `RodFactory.cs` — Rod creation and initialization
2. `RodDatabase.cs` — All 36 rod definitions with stats
3. `RodNames.cs` — Enum of all rod names
4. `RodData.cs` — Rod data model (extends ItemData)
5. `MetamagicRodManager.cs` — Metamagic application system
6. `MetamagicType.cs` — Metamagic enum and tier definitions
7. `RodOfWonderEffects.cs` — 20 effect implementations + d100 table
8. `RodOfAbsorptionManager.cs` — Spell absorption + conversion
9. `RodOfLordlyMightController.cs` — Multi-form weapon system
10. `RodOfPythonController.cs` — Creature summoning
11. `RodActivationHandler.cs` — Activation type routing
12. Updated `ItemData.cs` — Rod-specific fields
13. Updated `EquipmentManager.cs` — Held item slot logic
14. Updated `SpellCastingUI.cs` — Metamagic rod integration
15. Updated `GameManager.cs` — Rod activation hooks
16. Updated `SaveLoadManager.cs` — Rod state persistence

### Documentation
1. `rods_implementation_plan.md` — This document
2. `rods_by_complexity.md` — Tier classification
3. `metamagic_rods_specification.md` — Detailed metamagic mechanics
4. `rods_priority_matrix.md` — Impact vs. complexity analysis

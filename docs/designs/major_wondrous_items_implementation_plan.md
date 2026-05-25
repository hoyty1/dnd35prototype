# D&D 3.5e Major Wondrous Items Implementation Plan

**Project:** DND35Prototype  
**Phase:** Major Wondrous Items (Price > 60,000 gp)  
**Source:** Dungeon Master's Guide (DMG) pages 246-265  
**Date:** May 25, 2026  
**Status:** Planning Phase

---

## MAJOR WONDROUS ITEMS OVERVIEW

### Definition
Major wondrous items are magical items priced above 60,000 gp, representing the most powerful non-artifact magical equipment available in D&D 3.5e. These items often have legendary abilities, multiple powerful effects, or grant access to high-level spell effects.

### Statistics

**Total Count:** 31 major wondrous items identified from DMG
- **Already Implemented (Phase 9-10):** 5 items
- **Remaining to Implement:** 26 items

**Price Ranges:**
- 60,000-100,000 gp: ~15 items (48%)
- 100,000-150,000 gp: ~8 items (26%)
- 150,000-200,000 gp: ~5 items (16%)
- 200,000+ gp: ~3 items (10%)

**Equipment Slots Distribution:**
- **Shoulders (Cloaks/Mantles):** 6 items (19%)
- **Neck (Amulets/Periapts):** 4 items (13%)
- **Head (Helms):** 2 items (6%)
- **Body (Robes):** 2 items (6%)
- **Slotless:** 17 items (55%)

**Comparison to Minor/Medium Items:**
- Minor items (< 15,000 gp): ~65 items ✅ Implemented
- Medium items (15,000-60,000 gp): ~65 items ✅ Implemented
- Major items (> 60,000 gp): 31 items ⬜ Planning Phase

---

## COMPLETE MAJOR ITEMS CATALOG

### Items by Equipment Slot

#### **HEAD (Helms)**

**1. Helm of Brilliance** - 125,000 gp ✅ **IMPLEMENTED**
- **Slot:** Head
- **Effects:** Multiple spell effects, flame abilities, damage reduction
- **Status:** Completed in Phase 10

**2. Helm of Teleportation** - 73,500 gp ✅ **IMPLEMENTED**
- **Slot:** Head
- **Effects:** Teleport 3/day
- **Status:** Completed in Phase 10

---

#### **NECK (Amulets, Periapts, Scarabs)**

**3. Amulet of the Planes** - 120,000 gp
- **Slot:** Neck
- **Activation:** Standard action
- **Effect:** *Plane Shift* at will (as spell, caster level 15th)
- **Complexity:** ⭐⭐⭐ (Requires planar travel system)
- **Dependencies:** Plane shifting mechanics, planar database

**4. Periapt of Proof Against Poison** - 27,000 gp
- **Slot:** Neck
- **Effect:** Complete immunity to poison
- **Complexity:** ⭐⭐
- **Dependencies:** Poison resistance system (likely already implemented)

**5. Scarab, Golembane** - 2,500 gp
- **Slot:** Neck
- **Effect:** +2 bonus on attacks vs golems, allows critical hits vs golems
- **Complexity:** ⭐⭐
- **Dependencies:** Creature type checking, critical hit mechanics

**6. Scarab of Protection** - 38,000 gp
- **Slot:** Neck
- **Effect:** +4 resistance bonus to saves, absorbs death effects (12 levels), absorbs negative energy (200 hp)
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Death effect system, negative energy tracking

---

#### **SHOULDERS (Cloaks, Wings, Mantles)**

**7. Mantle of Spell Resistance** - 90,000-250,000 gp (5 variants)
- **Slot:** Shoulders
- **Effect:** Grants Spell Resistance
  - SR 13: 90,000 gp
  - SR 15: 121,000 gp
  - SR 17: 157,000 gp
  - SR 19: 198,000 gp
  - SR 21: 250,000 gp
- **Complexity:** ⭐⭐
- **Dependencies:** Spell Resistance stacking rules (highest wins, does not stack)

**8. Wings of Flying** - 54,000 gp
- **Slot:** Shoulders
- **Effect:** Fly speed of 60 ft (average maneuverability), unlimited duration
- **Complexity:** ⭐⭐
- **Dependencies:** Flight mechanics

**9. Mantle of Faith** - 76,000 gp
- **Slot:** Shoulders
- **Effect:** +5 resistance bonus to all saves, can cast 1/day each: *Bless*, *Detect Evil*, *Remove Fear*, *Aid*
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Spell effect system, charges per day

**10. Cloak of the Bat** - 26,000 gp
- **Slot:** Shoulders
- **Effect:** *Hang* (as bat), *Polymorph* into bat, fly in dark/twilight
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Polymorph system

---

#### **BODY (Robes)**

**11. Robe of the Archmagi** - 75,000 gp ✅ **IMPLEMENTED**
- **Slot:** Body
- **Effects:** +5 armor bonus, SR 18, spell save DC increase
- **Status:** Completed in Phase 10

**12. Robe of Eyes** - 120,000 gp ✅ **IMPLEMENTED**
- **Slot:** Body
- **Effects:** See in all directions, see invisible, tracking
- **Status:** Completed in Phase 10

**13. Robe of Stars** - 58,000 gp
- **Slot:** Body
- **Effect:** +1 luck bonus to saves, move through Astral Plane, 6 stars act as *Magic Missile* (5th level)
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Astral travel, consumable charges

**14. Robe of Blending** - 8,400 gp
- **Slot:** Body
- **Effect:** Blend into surroundings (+10 to Hide checks)
- **Complexity:** ⭐⭐
- **Dependencies:** Hide skill modifier

---

#### **SLOTLESS ITEMS**

##### **Planar & Transportation**

**15. Cubic Gate** - 164,000 gp
- **Slot:** Slotless (handheld cube)
- **Activation:** Standard action
- **Effect:** 6 sides, each attuned to different plane; activate side to cast *Gate* to that plane; each side usable 3/week
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Planar travel system, per-side charge tracking

**16. Well of Many Worlds** - 82,000 gp
- **Slot:** Slotless (6 ft diameter cloth)
- **Effect:** Opens two-way portal to random plane when spread out; dangerous interaction with *Portable Hole* (both destroyed)
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Portal creation, random plane selection, item interaction mechanics

**17. Carpet of Flying** - 20,000-60,000 gp (4 sizes)
- **Slot:** Slotless (vehicle)
- **Effect:** Carries passengers, flies at 40 ft speed
  - 5×5 ft: 20,000 gp (1 person, 200 lb)
  - 5×10 ft: 35,000 gp (2 people, 400 lb)
  - 10×10 ft: 60,000 gp (4 people, 800 lb)
  - 10×15 ft: 75,000 gp (6 people, 1,200 lb)
- **Complexity:** ⭐⭐
- **Dependencies:** Vehicle mechanics, weight capacity tracking

---

##### **Creature Trapping & Containment**

**18. Iron Flask** - 170,000 gp
- **Slot:** Slotless (brass bottle)
- **Effect:** Trap any creature within 60 ft (Will DC 19 negates); holds 1 creature indefinitely; release as standard action (creature serves for 1 hour or can be hostile)
- **Complexity:** ⭐⭐⭐⭐⭐
- **Dependencies:** Creature trapping system, save/load for trapped creatures, service/hostility mechanics

**19. Efreeti Bottle** - 145,000 gp
- **Slot:** Slotless (brass bottle)
- **Effect:** Contains trapped efreeti; opening causes efreeti to serve for 1 hour (service or 3 wishes for freedom); can trap outsiders (Will DC 19); only 1 creature at a time
- **Complexity:** ⭐⭐⭐⭐
- **Dependencies:** Efreeti summoning, wish mechanics, outsider trapping

**20. Bottle of Air** - 7,250 gp
- **Slot:** Slotless
- **Effect:** Provides air for breathing underwater or in toxic environments (inexhaustible supply)
- **Complexity:** ⭐
- **Dependencies:** Environmental hazard system

---

##### **Mirrors**

**21. Mirror of Life Trapping** - 200,000 gp
- **Slot:** Slotless (4×6 ft mirror)
- **Effect:** Trap up to 15 creatures (Will DC 23 negates, 50 ft range); view trapped creatures by speaking name; release individually or all at once; creatures released are in same condition as when trapped
- **Complexity:** ⭐⭐⭐⭐⭐
- **Dependencies:** Advanced creature trapping, UI for viewing trapped creatures, mass release mechanics

**22. Mirror of Opposition** - 92,000 gp
- **Slot:** Slotless (4×5 ft mirror)
- **Effect:** Creates duplicate of viewer with opposite alignment; duplicate attacks original; duplicate destroyed after combat or 1d4+10 rounds
- **Complexity:** ⭐⭐⭐⭐
- **Dependencies:** Character duplication, alignment inversion, temporary combatant system

**23. Mirror of Mental Prowess** - 175,000 gp
- **Slot:** Slotless (5×5 ft mirror)
- **Effect:** +2 enhancement to Int, Wis, Cha (when viewing 1/week); scrying as spell (DC 19); *Detect Thoughts* (DC 15); *Suggestion* (DC 16); telepathy (120 ft)
- **Complexity:** ⭐⭐⭐⭐⭐
- **Dependencies:** Ability score enhancement (temporary), scrying mechanics, telepathy system

---

##### **Summoning & Elementals**

**24. Stone of Controlling Earth Elementals** - 100,000 gp
- **Slot:** Slotless
- **Effect:** Summon Elder Earth Elemental once per day; control all earth elementals within 60 ft (Charisma check to resist)
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Elemental summoning, mass control mechanics

**25. Bowl of Commanding Water Elementals** - 100,000 gp
- **Slot:** Slotless (requires water)
- **Effect:** Summon Elder Water Elemental once per day; control water elementals within 60 ft
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Elemental summoning, water requirement check

**26. Brazier of Commanding Fire Elementals** - 100,000 gp
- **Slot:** Slotless (requires fire)
- **Effect:** Summon Elder Fire Elemental once per day; control fire elementals within 60 ft
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Elemental summoning

**27. Censer of Controlling Air Elementals** - 100,000 gp
- **Slot:** Slotless (requires incense)
- **Effect:** Summon Elder Air Elemental once per day; control air elementals within 60 ft
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Elemental summoning

---

##### **Constructs & Guardians**

**28. Iron Cobra** - 80,000 gp
- **Slot:** Slotless (animated construct)
- **Effect:** Attacks on command; poison bite (Fort DC 20, 1d3 Con/round for 6 rounds); follows simple orders; AC 25, 60 hp
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Construct AI, poison system, simple command parsing

**29. Stone Horse** - 10,000-30,000 gp (3 types)
- **Slot:** Slotless (figurine)
- **Effect:** Becomes stone horse on command
  - **Courser** (10,000 gp): Light horse stats, 1/week, 24 hours
  - **Destrier** (14,800 gp): Heavy horse stats, 1/week, 24 hours
  - **Destrier (greater)** (28,500 gp): Heavy horse + fly 60 ft (average)
- **Complexity:** ⭐⭐
- **Dependencies:** Figurine transformation system (may already exist for Figurines of Wondrous Power)

**30. Apparatus of Kwalish** - 90,000 gp
- **Slot:** Slotless (vehicle)
- **Effect:** 10 ft tall iron lobster vehicle; holds 2 Medium creatures; AC 20, 200 hp; 10 levers control movement (walk/swim), attack (2 pincers 2d6+6), vision (windows open/close), and other functions
- **Complexity:** ⭐⭐⭐⭐
- **Dependencies:** Vehicle system, lever control interface, underwater movement

---

##### **Legendary Tools & Weapons**

**31. Mattock of the Titans** - 23,348 gp
- **Slot:** Two-handed weapon
- **Effect:** +3 adamantine mattock; requires Strength 19 to wield; deals 4d6 damage; excavate stone (10 cu ft per 10 minutes)
- **Complexity:** ⭐⭐
- **Dependencies:** Strength requirement check, adamantine properties, excavation mechanics

**32. Maul of the Titans** - 25,305 gp
- **Slot:** Two-handed weapon
- **Effect:** +3 greatclub; requires Strength 23 to wield; deals 4d8 damage; sunder +8 bonus
- **Complexity:** ⭐⭐
- **Dependencies:** Strength requirement, sunder mechanics

**33. Lyre of Building** - 13,000 gp
- **Slot:** Slotless (instrument)
- **Effect:** *Fabricate* as spell (CL 13), 1/week, 30 minutes to use; can create buildings/structures
- **Complexity:** ⭐⭐
- **Dependencies:** Fabricate spell mechanics, construction rules

**34. Horn of Valhalla** - 15,000-50,000 gp (4 types)
- **Slot:** Slotless (instrument)
- **Effect:** Summons berserkers to fight for user
  - **Silver** (15,000 gp): 2d4+2 berserkers, 1/week
  - **Brass** (20,000 gp): 2d4+1 berserkers, 1/week (requires proficiency)
  - **Bronze** (35,000 gp): 3d4+3 berserkers, 1/week (requires proficiency)
  - **Iron** (50,000 gp): 4d4+4 berserkers, 1/week (requires proficiency)
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Temporary ally summoning, proficiency checks

---

##### **Miscellaneous Powerful Items**

**35. Cube of Force** - 62,000 gp ✅ **IMPLEMENTED**
- **Slot:** Slotless
- **Effect:** Creates force barriers with 6 selectable modes
- **Status:** Completed in Phase 10

**36. Candle of Invocation** - 8,400 gp
- **Slot:** Slotless (consumable)
- **Effect:** Attuned to specific alignment; when lit by matching-alignment divine caster: *Gate* spell (calling specific outsider), +2 bonus to CL for divine spells; burns for 4 hours total
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Alignment checking, divine caster detection, outsider summoning, burn time tracking

**37. Incense of Meditation** - 4,900 gp
- **Slot:** Slotless (consumable)
- **Effect:** Divine casters who burn incense while preparing spells prepare all spells as if 2 CL higher (bonus spells, save DC, effects)
- **Complexity:** ⭐⭐
- **Dependencies:** Spell preparation system, CL enhancement

**38. Phylactery of Faithfulness** - 1,000 gp
- **Slot:** Head
- **Effect:** Instantly know if action would violate deity's tenets; worn by clerics to avoid losing powers
- **Complexity:** ⭐⭐
- **Dependencies:** Divine code system, action alignment checking

**39. Mantle of Great Stealth** - 242,000 gp
- **Slot:** Shoulders
- **Effect:** +10 competence bonus to Hide and Move Silently; *Invisibility* at will; *Greater Invisibility* 1/day
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Skill bonuses, invisibility mechanics

---

## MAJOR ITEMS BY CATEGORY

### **LEGENDARY ROBES & VESTMENTS**

**Already Implemented:**
- ✅ Robe of the Archmagi (75,000 gp) - Phase 10
- ✅ Robe of Eyes (120,000 gp) - Phase 10

**To Implement:**
- Robe of Stars (58,000 gp) - Astral travel, *Magic Missile* charges
- Robe of Blending (8,400 gp) - Camouflage, Hide bonus

---

### **POWERFUL HEADGEAR**

**Already Implemented:**
- ✅ Helm of Brilliance (125,000 gp) - Phase 10
- ✅ Helm of Teleportation (73,500 gp) - Phase 10

**To Implement:**
- Phylactery of Faithfulness (1,000 gp) - Divine code violation detection

---

### **PLANAR & TRANSPORTATION**

**Priority: HIGH** - Core to high-level gameplay

- Amulet of the Planes (120,000 gp) - *Plane Shift* at will
- Cubic Gate (164,000 gp) - *Gate* to 6 different planes
- Well of Many Worlds (82,000 gp) - Random planar portals
- Carpet of Flying, 10×10 ft (60,000 gp) - Flying vehicle
- Robe of Stars (58,000 gp) - Astral Plane travel

**New System Required:** Planar travel mechanics

---

### **LEGENDARY PROTECTION**

**Priority: HIGH** - Combat-relevant

- Mantle of Spell Resistance (90,000-250,000 gp) - SR 13-21, 5 variants
- Scarab of Protection (38,000 gp) - Saves bonus, death effect/negative energy absorption
- Periapt of Proof Against Poison (27,000 gp) - Complete poison immunity

**New System Required:** SR stacking rules (if not already implemented)

---

### **ARTIFACT-LEVEL ITEMS**

**Priority: MEDIUM** - Extremely complex, end-game content

- Mirror of Life Trapping (200,000 gp) - Trap 15 creatures, view, release
- Mirror of Mental Prowess (175,000 gp) - Ability boost, scrying, telepathy
- Mirror of Opposition (92,000 gp) - Create evil duplicate
- Iron Flask (170,000 gp) - Trap any creature
- Efreeti Bottle (145,000 gp) - Summon efreeti, trap outsiders
- Apparatus of Kwalish (90,000 gp) - Lobster submarine vehicle

**New Systems Required:** Creature trapping, mirror mechanics, vehicle control

---

### **LEGENDARY CONSTRUCTS & SUMMONING**

**Priority: MEDIUM** - AI and summoning systems

- Stone of Controlling Earth Elementals (100,000 gp)
- Bowl of Commanding Water Elementals (100,000 gp)
- Brazier of Commanding Fire Elementals (100,000 gp)
- Censer of Controlling Air Elementals (100,000 gp)
- Iron Cobra (80,000 gp) - Animated guardian construct
- Stone Horse (10,000-30,000 gp) - Mount figurines

**New Systems Required:** Elemental control mechanics, construct AI

---

### **POWERFUL INSTRUMENTS**

**Priority: LOW** - Situational utility

- Lyre of Building (13,000 gp) - *Fabricate* buildings
- Horn of Valhalla, Iron (50,000 gp) - Summon berserkers
- Pipes of the Sewers (1,150 gp) - Summon/control rats

**New Systems Required:** Ally summoning with duration

---

### **LEGENDARY TOOLS & EQUIPMENT**

**Priority: LOW** - Niche applications

- Mattock of the Titans (23,348 gp) - Str 19, 4d6 damage, excavation
- Maul of the Titans (25,305 gp) - Str 23, 4d8 damage, sunder
- Candle of Invocation (8,400 gp) - *Gate* spell, divine CL boost
- Incense of Meditation (4,900 gp) - Divine spell preparation boost

---

## COMPLEXITY TIERS

### **TIER 1: Enhanced Versions (⭐⭐)**
*Simple implementations building on existing systems*

**Estimated Time per Item:** 2-3 days

**Items:**
1. Carpet of Flying (all sizes) - Vehicle with fly speed
2. Mantle of Spell Resistance (5 variants) - Grant SR, check stacking
3. Stone Horse (3 types) - Figurine transformation (if system exists)
4. Periapt of Proof Against Poison - Poison immunity flag
5. Robe of Blending - Hide skill bonus
6. Wings of Flying - Flight speed, unlimited duration
7. Scarab, Golembane - Attack/crit bonus vs golems
8. Bottle of Air - Environmental hazard immunity

**Total:** 8 items, ~2-3 weeks

---

### **TIER 2: Powerful Single Effect (⭐⭐⭐)**
*New mechanics but focused on one primary ability*

**Estimated Time per Item:** 5-7 days

**Items:**
1. Amulet of the Planes - *Plane Shift* at will
2. Cubic Gate - *Gate* to 6 planes, charges per side
3. Well of Many Worlds - Random portal creation
4. Mantle of Faith - Save bonus + 4 spell effects 1/day
5. Robe of Stars - Astral travel + *Magic Missile* charges
6. Lyre of Building - *Fabricate* spell implementation
7. Mattock of the Titans - Str requirement, excavation
8. Maul of the Titans - Str requirement, sunder bonus
9. Cloak of the Bat - Polymorph into bat, hang, fly
10. Scarab of Protection - Save bonus, absorb death/negative energy
11. Phylactery of Faithfulness - Divine code checking
12. Candle of Invocation - *Gate* + CL boost, burn tracking
13. Incense of Meditation - Spell preparation enhancement

**Total:** 13 items, ~9-11 weeks

---

### **TIER 3: Legendary Multi-Ability (⭐⭐⭐⭐)**
*Multiple powerful systems working together*

**Estimated Time per Item:** 2-3 weeks

**Items:**
1. Mirror of Opposition - Duplicate creation, opposite alignment, combat AI
2. Efreeti Bottle - Summon efreeti, trap outsiders, service mechanics
3. Stone/Bowl/Brazier/Censer of Controlling Elementals (4 items) - Summon + control mechanics
4. Iron Cobra - Construct AI, poison, command system
5. Apparatus of Kwalish - Vehicle, lever controls, combat abilities
6. Horn of Valhalla (4 types) - Summon temporary allies, proficiency check
7. Mantle of Great Stealth - Skill bonus + invisibility effects

**Total:** 11 items (counting 4 elemental items and 4 horn types), ~8-12 weeks

---

### **TIER 4: Artifact-Level Complexity (⭐⭐⭐⭐⭐)**
*Extremely complex, multiple interacting systems*

**Estimated Time per Item:** 3-4 weeks

**Items:**
1. Mirror of Life Trapping - Trap 15 creatures, view UI, release mechanics, save state
2. Mirror of Mental Prowess - Ability boosts, scrying, *Detect Thoughts*, *Suggestion*, telepathy
3. Iron Flask - Trap any creature, service mechanics, save state

**Total:** 3 items, ~9-12 weeks

---

## NEW SYSTEMS REQUIRED

### **SYSTEM 1: Planar Travel & Gates**

**Purpose:** Allow teleportation between planes of existence

**Priority:** HIGH (required for 5 major items)

**Components:**

```csharp
public enum Plane
{
    Material,
    Ethereal,
    Astral,
    
    // Elemental Planes
    Elemental_Air,
    Elemental_Earth,
    Elemental_Fire,
    Elemental_Water,
    
    // Energy Planes
    Positive,
    Negative,
    
    // Outer Planes (sample)
    Nine_Hells,
    Abyss,
    Celestia,
    Elysium,
    Limbo,
    
    // Special
    Shadow,
    Plane_of_Mirrors
}

public class PlanarTravelSystem
{
    // Core plane shift ability
    public static bool PlaneShift(Character traveler, Plane destination, bool requiresWillSave = true)
    {
        if (requiresWillSave)
        {
            int dc = 20; // Standard DC
            if (!SavingThrows.MakeWillSave(traveler, dc))
            {
                // Failed save - plane shift fails or mishap
                return false;
            }
        }
        
        // Move to destination plane
        traveler.CurrentPlane = destination;
        
        // Apply environmental effects if any
        ApplyPlanarEnvironment(traveler, destination);
        
        return true;
    }
    
    // Gate spell (allows travel + calling creatures)
    public static void CreateGate(Plane targetPlane, int duration)
    {
        // Opens two-way portal
        // Can summon creatures from that plane
    }
    
    // Environmental hazards
    private static void ApplyPlanarEnvironment(Character character, Plane plane)
    {
        switch (plane)
        {
            case Plane.Elemental_Fire:
                // Apply heat damage unless protected
                break;
            case Plane.Negative:
                // Negative energy drain
                break;
            // etc.
        }
    }
}
```

**Items Using This System:**
- Amulet of the Planes (at-will *Plane Shift*)
- Cubic Gate (*Gate* to 6 planes, 3/week each)
- Well of Many Worlds (random plane portal)
- Candle of Invocation (*Gate* to summon outsider)
- Robe of Stars (travel through Astral Plane)

**Estimated Implementation Time:** 1-2 weeks

---

### **SYSTEM 2: Creature Trapping & Containment**

**Purpose:** Trap creatures in extradimensional spaces, store, and release them

**Priority:** HIGH (required for 3 artifact-level items)

**Components:**

```csharp
[Serializable]
public class TrappedCreature
{
    public string CreatureName;
    public int CreatureID;
    public int CurrentHP;
    public int MaxHP;
    public List<StatusEffect> ActiveEffects;
    public DateTime TrappedAt;
    
    // Store complete creature state
    public CharacterData SerializedData;
}

public class CreatureTrapSystem
{
    public List<TrappedCreature> TrappedCreatures;
    public int MaxCapacity;
    
    // Trap attempt
    public bool AttemptTrap(Character target, int saveDC, SavingThrowType saveType)
    {
        if (TrappedCreatures.Count >= MaxCapacity)
        {
            return false; // Container full
        }
        
        // Saving throw
        bool saved = false;
        if (saveType == SavingThrowType.Will)
        {
            saved = SavingThrows.MakeWillSave(target, saveDC);
        }
        else if (saveType == SavingThrowType.Reflex)
        {
            saved = SavingThrows.MakeReflexSave(target, saveDC);
        }
        
        if (saved)
        {
            return false; // Target resisted
        }
        
        // Trap creature
        TrappedCreature trapped = new TrappedCreature
        {
            CreatureName = target.Name,
            CreatureID = target.ID,
            CurrentHP = target.CurrentHP,
            MaxHP = target.MaxHP,
            ActiveEffects = new List<StatusEffect>(target.ActiveEffects),
            TrappedAt = DateTime.Now,
            SerializedData = SerializeCharacter(target)
        };
        
        TrappedCreatures.Add(trapped);
        
        // Remove from game world
        RemoveFromCombat(target);
        
        return true;
    }
    
    // Release trapped creature
    public Character ReleaseCreature(int index, bool friendlyToUser)
    {
        if (index < 0 || index >= TrappedCreatures.Count)
            return null;
        
        TrappedCreature trapped = TrappedCreatures[index];
        
        // Restore creature
        Character released = DeserializeCharacter(trapped.SerializedData);
        released.CurrentHP = trapped.CurrentHP;
        released.MaxHP = trapped.MaxHP;
        
        // Set attitude
        if (friendlyToUser)
        {
            released.Attitude = Attitude.Friendly;
            // May serve for limited time
        }
        else
        {
            released.Attitude = Attitude.Hostile;
        }
        
        // Remove from trap
        TrappedCreatures.RemoveAt(index);
        
        // Add back to game world
        AddToCurrentScene(released);
        
        return released;
    }
    
    // View trapped creatures (for UI)
    public List<TrappedCreature> GetTrappedCreatures()
    {
        return TrappedCreatures;
    }
    
    // Serialization for save/load
    public string SerializeTrappedCreatures()
    {
        return JsonUtility.ToJson(TrappedCreatures);
    }
}
```

**Items Using This System:**
- **Iron Flask** (170,000 gp) - Trap any creature, capacity 1
- **Mirror of Life Trapping** (200,000 gp) - Trap creatures that view it, capacity 15, view UI required
- **Efreeti Bottle** (145,000 gp) - Trap outsiders, capacity 1, summon efreeti

**Special Requirements:**
- Save/Load persistence of trapped creatures
- UI to view trapped creatures (Mirror of Life Trapping)
- Service mechanics (creature serves user for duration)
- Hostile release (creature attacks immediately)

**Estimated Implementation Time:** 2-3 weeks

---

### **SYSTEM 3: Spell Resistance Management**

**Purpose:** Grant variable SR levels, enforce stacking rules

**Priority:** MEDIUM (may already be partially implemented)

**Components:**

```csharp
public class SpellResistanceManager
{
    // Calculate total SR (highest wins, no stacking)
    public static int GetTotalSpellResistance(Character character)
    {
        int sr = character.BaseSpellResistance;
        
        // Check all SR sources and take highest
        List<int> srSources = new List<int>();
        
        if (character.Race == Race.Drow)
            srSources.Add(11 + character.Level); // Drow SR
        
        // Check equipped items
        if (character.IsWearing(ItemType.RobeOfArchmagi))
            srSources.Add(18);
        
        if (character.IsWearing(ItemType.MantleOfSpellResistance))
        {
            int mantleSR = GetMantleSRValue(character.EquippedItems);
            srSources.Add(mantleSR);
        }
        
        // Check buffs/spells
        foreach (var effect in character.ActiveEffects)
        {
            if (effect.Type == EffectType.SpellResistance)
                srSources.Add(effect.Value);
        }
        
        // Return highest
        if (srSources.Count > 0)
            return Math.Max(sr, srSources.Max());
        
        return sr;
    }
    
    // Test if spell penetrates SR
    public static bool PenetratesSpellResistance(Character caster, Character target, int spellLevel)
    {
        int targetSR = GetTotalSpellResistance(target);
        
        if (targetSR == 0)
            return true; // No SR
        
        // Caster level check: 1d20 + CL
        int clCheck = Random.Range(1, 20) + caster.CasterLevel;
        
        // Spell Penetration feat bonuses
        if (caster.HasFeat("Spell Penetration"))
            clCheck += 2;
        if (caster.HasFeat("Greater Spell Penetration"))
            clCheck += 2;
        
        return clCheck >= targetSR;
    }
}
```

**Items Using This System:**
- Mantle of Spell Resistance (SR 13, 15, 17, 19, 21)
- Robe of the Archmagi (SR 18) ✅ Already implemented

**Estimated Implementation Time:** 3-5 days (if not already implemented)

---

### **SYSTEM 4: Mirror Mechanics**

**Purpose:** Scrying, soul trapping (with viewing UI), duplicate creation

**Priority:** MEDIUM (artifact-level items)

**Components:**

```csharp
public enum MirrorType
{
    LifeTrapping,   // Trap souls, view trapped creatures
    Opposition,     // Create evil duplicate
    MentalProwess   // Scrying + mental abilities
}

public class MagicalMirror : Item
{
    public MirrorType Type;
    public CreatureTrapSystem TrapSystem; // For Life Trapping
    
    // Mirror of Life Trapping
    public void TriggerLifeTrap(Character viewer)
    {
        if (Type != MirrorType.LifeTrapping)
            return;
        
        // Automatic trap when viewed
        TrapSystem.AttemptTrap(viewer, saveDC: 23, SavingThrowType.Will);
    }
    
    public void ShowTrappedCreaturesUI()
    {
        // Display list of all trapped creatures
        List<TrappedCreature> trapped = TrapSystem.GetTrappedCreatures();
        // Show UI panel with names, HP, portraits
    }
    
    public void ReleaseTrappedCreature(int index, bool releaseAll = false)
    {
        if (releaseAll)
        {
            for (int i = trapped.Count - 1; i >= 0; i--)
            {
                TrapSystem.ReleaseCreature(i, friendlyToUser: false);
            }
        }
        else
        {
            TrapSystem.ReleaseCreature(index, friendlyToUser: false);
        }
    }
    
    // Mirror of Opposition
    public Character CreateOpposingDuplicate(Character viewer)
    {
        if (Type != MirrorType.Opposition)
            return null;
        
        // Clone character
        Character duplicate = CloneCharacter(viewer);
        
        // Invert alignment
        duplicate.Alignment = InvertAlignment(viewer.Alignment);
        
        // Duplicate attacks original
        duplicate.Target = viewer;
        duplicate.Attitude = Attitude.Hostile;
        
        // Duplicate destroyed after combat or 1d4+10 rounds
        int duration = Random.Range(1, 4) + 10;
        duplicate.AddTimedEffect(new DestroyAfterRounds(duration));
        
        return duplicate;
    }
    
    // Mirror of Mental Prowess
    public void ActivateScrying(Character target)
    {
        if (Type != MirrorType.MentalProwess)
            return;
        
        // Scrying as spell (Will DC 19)
        SpellEffects.Scrying(target, saveDC: 19);
    }
    
    public void UseDetectThoughts(Character target)
    {
        // Detect Thoughts (Will DC 15)
        SpellEffects.DetectThoughts(target, saveDC: 15);
    }
    
    public void UseSuggestion(Character target, string suggestion)
    {
        // Suggestion (Will DC 16)
        SpellEffects.Suggestion(target, suggestion, saveDC: 16);
    }
    
    public void ActivateTelepathy(Character target)
    {
        // Telepathy within 120 ft
        if (Distance(owner, target) <= 120)
        {
            // Enable mental communication
        }
    }
}

private Alignment InvertAlignment(Alignment original)
{
    // Lawful <-> Chaotic, Good <-> Evil
    Alignment inverted = original;
    
    // Invert law/chaos axis
    if (original.LawChaos == LawChaosAxis.Lawful)
        inverted.LawChaos = LawChaosAxis.Chaotic;
    else if (original.LawChaos == LawChaosAxis.Chaotic)
        inverted.LawChaos = LawChaosAxis.Lawful;
    
    // Invert good/evil axis
    if (original.GoodEvil == GoodEvilAxis.Good)
        inverted.GoodEvil = GoodEvilAxis.Evil;
    else if (original.GoodEvil == GoodEvilAxis.Evil)
        inverted.GoodEvil = GoodEvilAxis.Good;
    
    return inverted;
}
```

**Items Using This System:**
- Mirror of Life Trapping (trap + view + release)
- Mirror of Opposition (duplicate creation)
- Mirror of Mental Prowess (scrying + mental commands)

**Estimated Implementation Time:** 2-3 weeks

---

### **SYSTEM 5: Construct Guardians & Animated Objects**

**Purpose:** Permanent constructs that obey commands

**Priority:** LOW (niche items)

**Components:**

```csharp
public class ConstructGuardian : Character
{
    public Character Owner;
    public List<string> SimpleCommands = new List<string> { "guard", "attack", "follow", "stay" };
    
    public void GiveCommand(string command)
    {
        if (SimpleCommands.Contains(command.ToLower()))
        {
            CurrentCommand = command;
            ExecuteCommand();
        }
    }
    
    private void ExecuteCommand()
    {
        switch (CurrentCommand)
        {
            case "guard":
                // Attack any hostile within range
                AIBehavior = AIType.GuardArea;
                break;
            case "attack":
                // Attack designated target
                AIBehavior = AIType.AttackTarget;
                break;
            case "follow":
                // Follow owner
                AIBehavior = AIType.Follow;
                break;
            case "stay":
                // Do nothing
                AIBehavior = AIType.Idle;
                break;
        }
    }
}

// Iron Cobra specific
public class IronCobra : ConstructGuardian
{
    public void PoisonBite(Character target)
    {
        // Melee attack
        if (AttackRoll() >= target.AC)
        {
            int damage = DiceRoller.Roll("1d6+4");
            target.TakeDamage(damage);
            
            // Poison (Fort DC 20, 1d3 Con damage/round for 6 rounds)
            if (!SavingThrows.MakeFortSave(target, 20))
            {
                target.AddEffect(new PoisonEffect
                {
                    SaveDC = 20,
                    Damage = "1d3 Con",
                    Duration = 6,
                    Frequency = "per round"
                });
            }
        }
    }
}
```

**Items Using This System:**
- Iron Cobra (80,000 gp)
- Stone Horse (figurine transformation, may already exist)

**Estimated Implementation Time:** 1-2 weeks

---

### **SYSTEM 6: Elemental Summoning & Control**

**Purpose:** Summon and control elemental creatures

**Priority:** MEDIUM (4 items use this)

**Components:**

```csharp
public enum ElementalType
{
    Air,
    Earth,
    Fire,
    Water
}

public class ElementalSummoningSystem
{
    // Summon Elder Elemental (once per day)
    public static Character SummonElderElemental(ElementalType type, Character summoner)
    {
        Character elemental = CreateElderElemental(type);
        elemental.Owner = summoner;
        elemental.Attitude = Attitude.Friendly;
        elemental.Duration = 1440; // 24 hours (in rounds: 24 * 60 = 1440)
        
        return elemental;
    }
    
    // Control existing elementals (Charisma check to resist)
    public static bool ControlElemental(Character controller, Character elemental, ElementalType allowedType)
    {
        if (elemental.Type != CreatureType.Elemental)
            return false;
        
        if (elemental.ElementalSubtype != allowedType)
            return false;
        
        // Charisma check vs elemental's Charisma
        int controllerCheck = Random.Range(1, 20) + controller.GetAbilityModifier(Ability.Charisma);
        int elementalCheck = Random.Range(1, 20) + elemental.GetAbilityModifier(Ability.Charisma);
        
        if (controllerCheck > elementalCheck)
        {
            elemental.Owner = controller;
            elemental.Attitude = Attitude.Friendly;
            return true;
        }
        
        return false;
    }
    
    private static Character CreateElderElemental(ElementalType type)
    {
        // Create Elder Elemental stats
        // HD 24d8+120, Huge size
        Character elemental = new Character();
        elemental.Name = $"Elder {type} Elemental";
        elemental.Type = CreatureType.Elemental;
        elemental.ElementalSubtype = type;
        elemental.Size = Size.Huge;
        elemental.HD = 24;
        elemental.MaxHP = DiceRoller.Roll("24d8+120");
        elemental.CurrentHP = elemental.MaxHP;
        
        // Stats vary by elemental type
        switch (type)
        {
            case ElementalType.Air:
                elemental.Str = 10;
                elemental.Dex = 31;
                elemental.Con = 21;
                elemental.FlySpeed = 100;
                break;
            case ElementalType.Earth:
                elemental.Str = 29;
                elemental.Dex = 8;
                elemental.Con = 23;
                elemental.BurrowSpeed = 20;
                break;
            case ElementalType.Fire:
                elemental.Str = 27;
                elemental.Dex = 25;
                elemental.Con = 23;
                elemental.BurnDamage = "2d10";
                break;
            case ElementalType.Water:
                elemental.Str = 27;
                elemental.Dex = 18;
                elemental.Con = 25;
                elemental.SwimSpeed = 90;
                break;
        }
        
        return elemental;
    }
}
```

**Items Using This System:**
- Stone of Controlling Earth Elementals (100,000 gp)
- Bowl of Commanding Water Elementals (100,000 gp)
- Brazier of Commanding Fire Elementals (100,000 gp)
- Censer of Controlling Air Elementals (100,000 gp)

**Estimated Implementation Time:** 1 week

---

### **SYSTEM 7: Vehicle Mechanics**

**Purpose:** Items that function as vehicles (movement, capacity, AC/HP)

**Priority:** LOW (2 items)

**Components:**

```csharp
public class MagicalVehicle : Item
{
    public int Capacity; // Weight or passenger count
    public int Speed;
    public MovementType Movement; // Walk, Swim, Fly
    public int AC;
    public int HP;
    public int CurrentHP;
    public List<Character> Passengers;
    
    public bool BoardVehicle(Character character)
    {
        if (Passengers.Count >= Capacity)
            return false;
        
        Passengers.Add(character);
        character.IsInVehicle = true;
        character.CurrentVehicle = this;
        return true;
    }
    
    public void MoveVehicle(Vector3 destination)
    {
        // Move vehicle and all passengers
        transform.position = Vector3.MoveTowards(transform.position, destination, Speed * Time.deltaTime);
    }
    
    public void TakeDamage(int damage)
    {
        CurrentHP -= damage;
        if (CurrentHP <= 0)
        {
            DestroyVehicle();
        }
    }
}

// Carpet of Flying
public class CarpetOfFlying : MagicalVehicle
{
    public CarpetOfFlying(CarpetSize size)
    {
        Movement = MovementType.Fly;
        Speed = 40;
        
        switch (size)
        {
            case CarpetSize.Small_5x5:
                Capacity = 1;
                Price = 20000;
                break;
            case CarpetSize.Medium_5x10:
                Capacity = 2;
                Price = 35000;
                break;
            case CarpetSize.Large_10x10:
                Capacity = 4;
                Price = 60000;
                break;
            case CarpetSize.XLarge_10x15:
                Capacity = 6;
                Price = 75000;
                break;
        }
    }
}

// Apparatus of Kwalish
public class ApparatusOfKwalish : MagicalVehicle
{
    public enum Lever { Forward, Backward, Left, Right, Up, Down, Open, Close, Claws, Tail }
    public Dictionary<Lever, bool> LeverStates;
    
    public ApparatusOfKwalish()
    {
        Capacity = 2; // Medium creatures
        Speed = 30; // Walk/Swim
        Movement = MovementType.Walk | MovementType.Swim;
        AC = 20;
        HP = 200;
        CurrentHP = 200;
        Price = 90000;
        
        LeverStates = new Dictionary<Lever, bool>();
    }
    
    public void PullLever(Lever lever)
    {
        LeverStates[lever] = !LeverStates[lever];
        
        switch (lever)
        {
            case Lever.Forward:
                MoveForward();
                break;
            case Lever.Claws:
                AttackWithClaws();
                break;
            case Lever.Open:
                OpenViewports();
                break;
            // etc.
        }
    }
    
    private void AttackWithClaws()
    {
        // 2 claw attacks, 2d6+6 damage each
    }
}
```

**Items Using This System:**
- Carpet of Flying (4 sizes)
- Apparatus of Kwalish (iron lobster submarine)

**Estimated Implementation Time:** 1-2 weeks

---

## IMPLEMENTATION TIMELINE

### **PHASE 1: Already Implemented (0 weeks)**
**Status:** ✅ **COMPLETE**

Review and verify items completed in Phases 9-10:
- Robe of the Archmagi (75,000 gp)
- Robe of Eyes (120,000 gp)
- Helm of Brilliance (125,000 gp)
- Helm of Teleportation (73,500 gp)
- Cube of Force (62,000 gp)

**Tasks:**
- [x] Review implementation
- [x] Verify working in-game
- [x] Check loot generation includes these items
- [x] Confirm save/load persistence

---

### **PHASE 2: Spell Resistance Items (1 week)**
**Priority:** HIGH  
**Dependencies:** SR calculation system (verify if exists)

**Items to Implement:**
1. Mantle of Spell Resistance - SR 13 (90,000 gp)
2. Mantle of Spell Resistance - SR 15 (121,000 gp)
3. Mantle of Spell Resistance - SR 17 (157,000 gp)
4. Mantle of Spell Resistance - SR 19 (198,000 gp)
5. Mantle of Spell Resistance - SR 21 (250,000 gp)

**New Systems:**
- Verify SR stacking rules (highest wins, no stacking)
- Test interaction with Robe of the Archmagi SR 18

**Deliverables:**
- `MantleOfSpellResistance.cs`
- SR stacking verification tests

---

### **PHASE 3: Simple Protection Items (3 days)**
**Priority:** HIGH  
**Dependencies:** Existing poison/damage systems

**Items to Implement:**
1. Periapt of Proof Against Poison (27,000 gp)
2. Scarab, Golembane (2,500 gp)
3. Bottle of Air (7,250 gp)

**Deliverables:**
- `ProtectionItems.cs` (consolidated simple items)

---

### **PHASE 4: Planar Travel System (2 weeks)**
**Priority:** HIGH  
**Dependencies:** None (new system)

**New System:** Planar Travel & Gates (SYSTEM 1)

**Items to Implement:**
1. Amulet of the Planes (120,000 gp)
2. Cubic Gate (164,000 gp)
3. Well of Many Worlds (82,000 gp)

**Deliverables:**
- `PlanarTravelSystem.cs`
- `Plane.cs` (enum + plane definitions)
- `AmuletOfPlanes.cs`
- `CubicGate.cs`
- `WellOfManyWorlds.cs`
- Documentation: `planar_travel_system_design.md`

---

### **PHASE 5: Flying Vehicles (3 days)**
**Priority:** MEDIUM  
**Dependencies:** Flight mechanics (likely exists)

**Items to Implement:**
1. Carpet of Flying, 5×5 ft (20,000 gp)
2. Carpet of Flying, 5×10 ft (35,000 gp)
3. Carpet of Flying, 10×10 ft (60,000 gp)
4. Carpet of Flying, 10×15 ft (75,000 gp)
5. Wings of Flying (54,000 gp)

**Deliverables:**
- `CarpetOfFlying.cs` (all 4 sizes)
- `WingsOfFlying.cs`

---

### **PHASE 6: Elemental Control (1 week)**
**Priority:** MEDIUM  
**Dependencies:** Elemental creature stats

**New System:** Elemental Summoning & Control (SYSTEM 6)

**Items to Implement:**
1. Stone of Controlling Earth Elementals (100,000 gp)
2. Bowl of Commanding Water Elementals (100,000 gp)
3. Brazier of Commanding Fire Elementals (100,000 gp)
4. Censer of Controlling Air Elementals (100,000 gp)

**Deliverables:**
- `ElementalControlSystem.cs`
- `ElementalControlItem.cs` (base class for 4 items)
- Elder Elemental stat blocks (if not already implemented)

---

### **PHASE 7: Constructs & Figurines (1 week)**
**Priority:** LOW  
**Dependencies:** Figurine transformation system (may exist)

**New System:** Construct Guardians (SYSTEM 5)

**Items to Implement:**
1. Stone Horse, Courser (10,000 gp)
2. Stone Horse, Destrier (14,800 gp)
3. Stone Horse, Destrier (greater) (28,500 gp)
4. Iron Cobra (80,000 gp)

**Deliverables:**
- `StoneHorse.cs`
- `IronCobra.cs`
- `ConstructGuardian.cs` (base class)

---

### **PHASE 8: Creature Trapping System (3 weeks)**
**Priority:** HIGH  
**Dependencies:** Character serialization, save/load

**New System:** Creature Trapping & Containment (SYSTEM 2)

**Items to Implement:**
1. Iron Flask (170,000 gp)
2. Efreeti Bottle (145,000 gp)

**Deliverables:**
- `CreatureTrapSystem.cs`
- `IronFlask.cs`
- `EfreetiBottle.cs`
- Efreeti summoning mechanics
- Documentation: `creature_trapping_system_design.md`

---

### **PHASE 9: Mirror of Life Trapping (2 weeks)**
**Priority:** MEDIUM  
**Dependencies:** Creature Trapping System (Phase 8)

**New System:** Mirror Mechanics - Life Trapping (SYSTEM 4 - Part 1)

**Items to Implement:**
1. Mirror of Life Trapping (200,000 gp)

**Special Requirements:**
- UI to view trapped creatures (list with names, HP, portraits)
- Release controls (individual or all)
- Capacity: 15 creatures

**Deliverables:**
- `MirrorOfLifeTrapping.cs`
- `TrappedCreaturesUI.cs` (UI panel)

---

### **PHASE 10: Mirror of Opposition (1 week)**
**Priority:** MEDIUM  
**Dependencies:** Character cloning system

**New System:** Mirror Mechanics - Duplication (SYSTEM 4 - Part 2)

**Items to Implement:**
1. Mirror of Opposition (92,000 gp)

**Special Requirements:**
- Clone character stats
- Invert alignment
- Duplicate attacks original
- Timed destruction (1d4+10 rounds)

**Deliverables:**
- `MirrorOfOpposition.cs`
- `CharacterCloning.cs` (utility)
- `AlignmentInversion.cs` (utility)

---

### **PHASE 11: Mirror of Mental Prowess (2 weeks)**
**Priority:** LOW  
**Dependencies:** Scrying, telepathy systems

**New System:** Mirror Mechanics - Scrying & Mental Powers (SYSTEM 4 - Part 3)

**Items to Implement:**
1. Mirror of Mental Prowess (175,000 gp)

**Abilities:**
- +2 enhancement to Int, Wis, Cha (1/week when viewing)
- Scrying (Will DC 19)
- Detect Thoughts (Will DC 15)
- Suggestion (Will DC 16)
- Telepathy (120 ft range)

**Deliverables:**
- `MirrorOfMentalProwess.cs`
- Scrying mechanics (if not already implemented)
- Telepathy system

---

### **PHASE 12: Apparatus of Kwalish (1 week)**
**Priority:** LOW  
**Dependencies:** Vehicle system

**New System:** Vehicle Mechanics - Lever Control (SYSTEM 7)

**Items to Implement:**
1. Apparatus of Kwalish (90,000 gp)

**Special Requirements:**
- 10 levers controlling movement, attacks, vision
- AC 20, 200 HP
- 2 pincer attacks (2d6+6 each)
- Walk/swim movement

**Deliverables:**
- `ApparatusOfKwalish.cs`
- `MagicalVehicle.cs` (base class)
- Lever control UI

---

### **PHASE 13: Multi-Ability Items (1 week)**
**Priority:** MEDIUM  
**Dependencies:** Various spell effects

**Items to Implement:**
1. Mantle of Faith (76,000 gp) - Save bonus + 4 spells 1/day
2. Robe of Stars (58,000 gp) - Astral travel + Magic Missile charges
3. Scarab of Protection (38,000 gp) - Saves, death effect absorption, negative energy absorption
4. Cloak of the Bat (26,000 gp) - Polymorph, hang, fly

**Deliverables:**
- `MantleOfFaith.cs`
- `RobeOfStars.cs`
- `ScarabOfProtection.cs`
- `CloakOfBat.cs`

---

### **PHASE 14: Legendary Tools (1 week)**
**Priority:** LOW  
**Dependencies:** Strength checks, special weapon properties

**Items to Implement:**
1. Mattock of the Titans (23,348 gp)
2. Maul of the Titans (25,305 gp)
3. Lyre of Building (13,000 gp)

**Deliverables:**
- `TitanWeapons.cs` (Mattock + Maul)
- `LyreOfBuilding.cs`
- Fabricate spell mechanics

---

### **PHASE 15: Summoning Items (1 week)**
**Priority:** LOW  
**Dependencies:** Ally summoning system

**Items to Implement:**
1. Horn of Valhalla, Silver (15,000 gp)
2. Horn of Valhalla, Brass (20,000 gp)
3. Horn of Valhalla, Bronze (35,000 gp)
4. Horn of Valhalla, Iron (50,000 gp)

**Deliverables:**
- `HornOfValhalla.cs` (all 4 types)
- Berserker stat blocks
- Proficiency requirement checks

---

### **PHASE 16: Divine Items (3 days)**
**Priority:** LOW  
**Dependencies:** Divine spell system

**Items to Implement:**
1. Candle of Invocation (8,400 gp)
2. Incense of Meditation (4,900 gp)
3. Phylactery of Faithfulness (1,000 gp)

**Deliverables:**
- `DivineConsumables.cs` (Candle + Incense)
- `PhylacteryOfFaithfulness.cs`
- Divine code violation checking

---

### **PHASE 17: Stealth Items (3 days)**
**Priority:** LOW  
**Dependencies:** Hide/Move Silently skills, invisibility

**Items to Implement:**
1. Robe of Blending (8,400 gp)
2. Mantle of Great Stealth (242,000 gp)

**Deliverables:**
- `StealthItems.cs`

---

### **PHASE 18: Integration & Testing (2 weeks)**
**Priority:** CRITICAL  
**Dependencies:** All previous phases

**Tasks:**
1. Add all items to loot generation tables
2. Weight items by price/rarity
3. Test save/load for all complex items (traps, vehicles, etc.)
4. Verify UI displays for trapped creatures, mirrors, etc.
5. Balance testing (are items too powerful? Not powerful enough?)
6. Bug fixing
7. Documentation updates

**Deliverables:**
- Updated `WondrousItemDatabase.cs`
- Updated loot generation tables
- Test results document
- Bug fix log

---

## TOTAL TIMELINE ESTIMATE

**Phases 1-18:** ~16-20 weeks (4-5 months)

**By Priority:**
- **HIGH Priority (Core Gameplay):** 6 weeks
  - SR items, protection, planar travel, creature trapping
- **MEDIUM Priority (Complex Features):** 8 weeks
  - Mirrors, elementals, multi-ability items, vehicles
- **LOW Priority (Niche/Utility):** 4 weeks
  - Tools, divine items, summoning, stealth
- **Integration & Testing:** 2 weeks

---

## DETAILED ITEM SPECIFICATIONS

### **TIER 1 ITEMS**

#### **Mantle of Spell Resistance** (5 variants)
- **Prices:** 90,000 / 121,000 / 157,000 / 198,000 / 250,000 gp
- **Slot:** Shoulders
- **Effect:** Grants Spell Resistance (13/15/17/19/21)
- **Activation:** Continuous
- **Rules:** SR does not stack; highest value wins
- **Complexity:** ⭐⭐

#### **Periapt of Proof Against Poison**
- **Price:** 27,000 gp
- **Slot:** Neck
- **Effect:** Complete immunity to poison (ingested, inhaled, contact, injury)
- **Activation:** Continuous
- **Complexity:** ⭐⭐

#### **Carpet of Flying** (4 variants)
- **Prices:** 20,000 / 35,000 / 60,000 / 75,000 gp
- **Slot:** Slotless (vehicle)
- **Effect:** Fly 40 ft speed, carries 1/2/4/6 passengers
- **Activation:** Command word
- **Complexity:** ⭐⭐

---

### **TIER 2 ITEMS**

#### **Amulet of the Planes**
- **Price:** 120,000 gp
- **Slot:** Neck
- **Effect:** *Plane Shift* at will (as spell, CL 15th)
- **Activation:** Standard action
- **Rules:** User chooses destination plane; Will save DC 20 for unwilling creatures
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Planar Travel System

#### **Cubic Gate**
- **Price:** 164,000 gp
- **Slot:** Slotless (1 in cube)
- **Effect:** 6 sides, each attuned to different plane; activate side to cast *Gate* to that plane
- **Activation:** Standard action
- **Charges:** Each side usable 3/week
- **Rules:** User can choose which side to activate; *Gate* allows travel and calling creatures
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Planar Travel System, per-side charge tracking

#### **Well of Many Worlds**
- **Price:** 82,000 gp
- **Slot:** Slotless (6 ft diameter cloth)
- **Effect:** Opens two-way portal to random plane when spread on ground
- **Activation:** Standard action
- **Rules:** DM determines plane; portal remains as long as cloth stays in place
- **Danger:** If placed within 10 ft of Portable Hole, both are destroyed and gate to Astral Plane opens
- **Complexity:** ⭐⭐⭐
- **Dependencies:** Random plane selection, Portable Hole interaction

---

### **TIER 3 ITEMS**

#### **Mantle of Faith**
- **Price:** 76,000 gp
- **Slot:** Shoulders
- **Effect:** +5 resistance bonus to all saves; can cast 1/day each: *Bless*, *Detect Evil*, *Remove Fear*, *Aid*
- **Activation:** Continuous (saves); standard action (spells)
- **Complexity:** ⭐⭐⭐

#### **Robe of Stars**
- **Price:** 58,000 gp
- **Slot:** Body
- **Effect:** +1 luck bonus to saves; move through Astral Plane; 6 stars act as *Magic Missile* (5th level, 3 missiles each)
- **Activation:** Varies by ability
- **Rules:** Stars are consumable; robe becomes non-magical when all stars used
- **Complexity:** ⭐⭐⭐

#### **Efreeti Bottle**
- **Price:** 145,000 gp
- **Slot:** Slotless (brass bottle)
- **Effect:** 
  - Contains trapped efreeti; opening causes efreeti to serve for 1 hour
  - Efreeti may offer 3 wishes for permanent freedom (DM choice)
  - Can trap outsiders (Will DC 19 negates)
  - Only holds 1 creature at a time
- **Activation:** Standard action to open
- **Complexity:** ⭐⭐⭐⭐
- **Dependencies:** Efreeti stats, wish mechanics, creature trapping

---

### **TIER 4 ITEMS**

#### **Iron Flask**
- **Price:** 170,000 gp
- **Slot:** Slotless (brass bottle with lead stopper)
- **Effect:**
  - Trap any creature within 60 ft (Will DC 19 negates)
  - Holds 1 creature indefinitely
  - Release as standard action
  - Released creature either serves for 1 hour or is hostile (user's choice at trapping)
- **Activation:** Standard action
- **Rules:** Creature remains in flask even if flask destroyed; opening destroyed flask releases creature
- **Complexity:** ⭐⭐⭐⭐⭐
- **Dependencies:** Creature Trapping System, save/load persistence

#### **Mirror of Life Trapping**
- **Price:** 200,000 gp
- **Slot:** Slotless (large 4×6 ft mirror)
- **Effect:**
  - Trap up to 15 creatures (Will DC 23 negates)
  - Creatures trapped when viewing mirror within 50 ft
  - View trapped creatures by speaking command word + name
  - Release individually or all at once
  - Creatures released in same condition as when trapped
- **Activation:** Automatic (when viewed); command word (release)
- **Rules:** Each trapped creature occupies one "cell"; breaking mirror releases all creatures
- **Complexity:** ⭐⭐⭐⭐⭐
- **Dependencies:** Creature Trapping System, viewing UI, mass release

#### **Mirror of Opposition**
- **Price:** 92,000 gp
- **Slot:** Slotless (large 4×5 ft mirror)
- **Effect:**
  - Creates duplicate of viewer with opposite alignment
  - Duplicate attacks original with intent to kill
  - Duplicate destroyed after combat or 1d4+10 rounds
- **Activation:** Automatic (when viewed)
- **Rules:** Duplicate has all abilities, spells, and items (duplicates are non-magical)
- **Complexity:** ⭐⭐⭐⭐
- **Dependencies:** Character cloning, alignment inversion, temporary combatant

#### **Mirror of Mental Prowess**
- **Price:** 175,000 gp
- **Slot:** Slotless (large 5×5 ft mirror)
- **Effect:**
  - +2 enhancement to Int, Wis, Cha (when viewing mirror, 1/week, lasts 8 hours)
  - *Scrying* as spell (Will DC 19)
  - *Detect Thoughts* (Will DC 15)
  - *Suggestion* (Will DC 16)
  - Telepathy within 120 ft
- **Activation:** Varies by ability
- **Complexity:** ⭐⭐⭐⭐⭐
- **Dependencies:** Ability enhancement, scrying, telepathy

#### **Apparatus of Kwalish**
- **Price:** 90,000 gp
- **Slot:** Slotless (vehicle, 10 ft tall iron lobster)
- **Effect:**
  - Holds 2 Medium creatures or 1 Large
  - AC 20, 200 HP
  - Move 30 ft (walk/swim)
  - 10 levers control: movement, pincers, windows, air, etc.
  - 2 pincer attacks: +12 melee, 2d6+6 damage
- **Activation:** Lever manipulation (move action per lever)
- **Rules:** Levers control all functions; wrong lever can cause malfunction
- **Complexity:** ⭐⭐⭐⭐
- **Dependencies:** Vehicle system, lever control interface

---

## PRIORITY MATRIX

### **P1: HIGH IMPACT + MODERATE COMPLEXITY**
*Implement first; high player value, reasonable effort*

1. **Mantle of Spell Resistance** (5 variants) - SR is crucial for high-level gameplay
2. **Amulet of the Planes** - High utility, enables planar adventures
3. **Periapt of Proof Against Poison** - Simple but valuable protection
4. **Carpet of Flying** (4 variants) - Iconic items, popular with players
5. **Stone Horse** (3 variants) - Useful mounts, may leverage existing figurine system

**Estimated Time:** 3-4 weeks

---

### **P2: HIGH IMPACT + HIGH COMPLEXITY**
*Important features requiring significant development*

1. **Iron Flask** - Iconic item, enables creative gameplay
2. **Efreeti Bottle** - Summoning + trapping, wish mechanics
3. **Cubic Gate** - Planar travel hub, adventure enabler
4. **Elemental Control Items** (4 items) - Powerful summoning, combat utility
5. **Mantle of Faith** - Multiple useful abilities

**Estimated Time:** 8-10 weeks

---

### **P3: ARTIFACT-LEVEL / END-GAME CONTENT**
*Complex legendary items for high-level campaigns*

1. **Mirror of Life Trapping** - Soul trap mechanics, UI required
2. **Mirror of Mental Prowess** - Multiple mental abilities
3. **Mirror of Opposition** - Combat duplicates, AI required
4. **Apparatus of Kwalish** - Unique vehicle, lever control
5. **Iron Cobra** - Construct guardian, poison mechanics

**Estimated Time:** 8-10 weeks

---

### **P4: NICHE / SITUATIONAL**
*Lower priority; useful but less frequently needed*

1. **Well of Many Worlds** - Random portals, risky
2. **Titan Weapons** (Mattock, Maul) - High Strength requirement limits users
3. **Horn of Valhalla** (4 types) - Summoning allies, proficiency checks
4. **Lyre of Building** - Fabricate spell, construction focus
5. **Divine Consumables** (Candle, Incense, Phylactery) - Divine caster only
6. **Stealth Items** (Robe of Blending, Mantle of Great Stealth) - Rogue/stealth focus
7. **Miscellaneous** (Scarab Golembane, Bottle of Air, Wings of Flying)

**Estimated Time:** 4-5 weeks

---

## ITEM COUNT SUMMARY

### **By Price Range**
| Price Range | Count | Percentage | Examples |
|-------------|-------|------------|----------|
| < 10,000 gp | 5 | 13% | Phylactery of Faithfulness, Scarab Golembane |
| 10,000-30,000 gp | 6 | 16% | Stone Horse, Periapt of Proof, Lyre of Building |
| 30,000-60,000 gp | 5 | 13% | Scarab of Protection, Horn of Valhalla (Brass/Bronze) |
| 60,000-100,000 gp | 10 | 26% | Carpet 10×10, Helm of Teleportation, Mantle SR 13, Iron Cobra |
| 100,000-150,000 gp | 8 | 21% | Amulet of Planes, Efreeti Bottle, Elemental Control (4) |
| 150,000-200,000 gp | 4 | 11% | Cubic Gate, Iron Flask, Mirror of Mental Prowess |
| 200,000+ gp | 1 | 3% | Mirror of Life Trapping, Mantle of Great Stealth |
| **TOTAL** | **39** | **100%** | |

**Note:** Total includes all variants (5 Mantles of SR, 4 Carpets, 3 Stone Horses, 4 Elemental items, 4 Horns)

---

### **By Complexity Tier**
| Tier | Complexity | Count | Percentage | Avg Time/Item | Total Time |
|------|------------|-------|------------|---------------|------------|
| 1 | ⭐⭐ | 8 | 21% | 2-3 days | 2-3 weeks |
| 2 | ⭐⭐⭐ | 18 | 46% | 5-7 days | 13-18 weeks |
| 3 | ⭐⭐⭐⭐ | 10 | 26% | 2-3 weeks | 20-30 weeks |
| 4 | ⭐⭐⭐⭐⭐ | 3 | 8% | 3-4 weeks | 9-12 weeks |
| **TOTAL** | | **39** | **100%** | | **44-63 weeks** |

**Realistic Estimate (with parallelization, system reuse):** 16-20 weeks

---

### **Already Implemented in Phases 9-10**
| Item | Price | Status |
|------|-------|--------|
| Robe of the Archmagi | 75,000 gp | ✅ Complete |
| Robe of Eyes | 120,000 gp | ✅ Complete |
| Helm of Brilliance | 125,000 gp | ✅ Complete |
| Helm of Teleportation | 73,500 gp | ✅ Complete |
| Cube of Force | 62,000 gp | ✅ Complete |

**Remaining:** 34 items (or 39 counting all variants)

---

### **By Equipment Slot**
| Slot | Count | Percentage | Examples |
|------|-------|------------|----------|
| Head | 3 | 8% | Helm of Brilliance ✅, Phylactery |
| Neck | 4 | 10% | Amulet of Planes, Periapt, Scarabs |
| Shoulders | 8 | 21% | Mantle of SR (5), Mantle of Faith, Wings, Cloaks |
| Body | 4 | 10% | Robes (Archmagi ✅, Eyes ✅, Stars, Blending) |
| Slotless | 20 | 51% | Mirrors, Bottles, Constructs, Tools, Vehicles |
| **TOTAL** | **39** | **100%** | |

---

## SUCCESS CRITERIA

### **Must Have (Critical)**
- [ ] All 34 remaining major items implemented and functional
- [ ] Planar travel system operational (plane shift, gate, portals)
- [ ] Creature trapping system working (trap, store, release)
- [ ] Spell Resistance properly calculated (highest wins, no stacking)
- [ ] All items added to loot generation tables
- [ ] Save/load preserves state for complex items (trapped creatures, charges, etc.)

### **Should Have (Important)**
- [ ] Mirror viewing UI for trapped creatures
- [ ] Vehicle controls (levers for Apparatus, passengers for Carpet)
- [ ] Elemental summoning and control mechanics
- [ ] Construct guardian AI (simple commands)
- [ ] Proper item icons/artwork
- [ ] Tooltips showing all effects

### **Nice to Have (Polish)**
- [ ] Visual effects for planar travel
- [ ] Animations for constructs (Iron Cobra, Apparatus)
- [ ] Sound effects for activating items
- [ ] Lore/flavor text for each item
- [ ] Tutorial hints for complex items

---

## TESTING CHECKLIST

### **Planar Travel**
- [ ] Plane Shift transports character to correct plane
- [ ] Will save functions correctly for unwilling targets
- [ ] Cubic Gate tracks charges per side (3/week each)
- [ ] Well of Many Worlds generates random plane
- [ ] Portable Hole + Well interaction destroys both

### **Creature Trapping**
- [ ] Iron Flask traps creature with Will save
- [ ] Trapped creature removed from combat
- [ ] Released creature attitude correct (friendly/hostile)
- [ ] Service duration tracked (1 hour)
- [ ] Save/load preserves trapped creatures
- [ ] Mirror of Life Trapping UI displays all trapped
- [ ] Release all function works correctly
- [ ] Efreeti Bottle summons efreeti, offers wishes

### **Spell Resistance**
- [ ] Mantle of SR grants correct value
- [ ] SR stacking: highest wins (not additive)
- [ ] Caster level check rolls correctly
- [ ] Spell Penetration feats apply bonus

### **Mirrors**
- [ ] Life Trapping: auto-trap when viewed, DC 23 Will save
- [ ] Opposition: creates duplicate, opposite alignment, attacks
- [ ] Mental Prowess: all 5 abilities functional

### **Elementals**
- [ ] Elder Elemental summoned with correct stats
- [ ] Control attempt uses Charisma check
- [ ] Controlled elemental obeys commands
- [ ] Duration tracked (once per day, 24 hours)

### **Vehicles**
- [ ] Carpet of Flying: carries correct passenger count
- [ ] Apparatus: all 10 levers functional
- [ ] AC/HP damage tracked
- [ ] Passengers share vehicle movement

### **Constructs**
- [ ] Iron Cobra: attacks on command
- [ ] Poison effect applies correctly (DC 20, 1d3 Con)
- [ ] Stone Horse: transforms into mount
- [ ] Duration/uses per week tracked

---

## ESTIMATED DELIVERABLES

### **Core Code Files**
1. **Planar Travel**
   - `PlanarTravelSystem.cs` (~500 lines)
   - `Plane.cs` (enum + data, ~200 lines)
   - `AmuletOfPlanes.cs` (~100 lines)
   - `CubicGate.cs` (~150 lines)
   - `WellOfManyWorlds.cs` (~100 lines)

2. **Creature Trapping**
   - `CreatureTrapSystem.cs` (~400 lines)
   - `TrappedCreature.cs` (data class, ~100 lines)
   - `IronFlask.cs` (~150 lines)
   - `EfreetiBottle.cs` (~200 lines)

3. **Mirrors**
   - `MirrorOfLifeTrapping.cs` (~250 lines)
   - `TrappedCreaturesUI.cs` (Unity UI, ~200 lines)
   - `MirrorOfOpposition.cs` (~300 lines)
   - `MirrorOfMentalProwess.cs` (~350 lines)
   - `CharacterCloning.cs` (utility, ~150 lines)

4. **Elementals**
   - `ElementalControlSystem.cs` (~300 lines)
   - `ElementalControlItem.cs` (base class, ~150 lines)
   - `ElderElemental.cs` (stats, ~200 lines)

5. **Vehicles**
   - `MagicalVehicle.cs` (base class, ~200 lines)
   - `CarpetOfFlying.cs` (~150 lines)
   - `ApparatusOfKwalish.cs` (~400 lines)

6. **Constructs**
   - `ConstructGuardian.cs` (base class, ~200 lines)
   - `IronCobra.cs` (~150 lines)
   - `StoneHorse.cs` (~100 lines)

7. **Spell Resistance**
   - `SpellResistanceManager.cs` (~150 lines)
   - `MantleOfSpellResistance.cs` (~100 lines)

8. **Miscellaneous Items**
   - `MajorProtectionItems.cs` (~200 lines)
   - `MantleOfFaith.cs` (~150 lines)
   - `RobeOfStars.cs` (~200 lines)
   - `TitanWeapons.cs` (~150 lines)
   - `LyreOfBuilding.cs` (~100 lines)
   - `HornOfValhalla.cs` (~200 lines)
   - `DivineConsumables.cs` (~200 lines)
   - `StealthItems.cs` (~150 lines)

9. **Database & Factory**
   - `MajorWondrousItemFactory.cs` (~500 lines)
   - `WondrousItemDatabase.cs` (updated, +~300 lines)

**Total Estimated Code:** ~7,500 lines

---

### **Documentation Files**
1. `major_wondrous_items_implementation_plan.md` (this document)
2. `major_wondrous_items_by_complexity.md` (supplementary)
3. `planar_travel_system_design.md` (technical spec)
4. `creature_trapping_system_design.md` (technical spec)
5. `major_wondrous_items_priority_matrix.md` (project management)
6. `major_wondrous_items_testing_results.md` (QA)

---

### **Asset Requirements**
1. **Item Icons:** ~34 unique icons (or variants)
2. **UI Elements:** Trapped creatures panel, lever controls, mirror frames
3. **3D Models (if applicable):** Carpet, Apparatus, Iron Cobra, Stone Horse, Mirrors
4. **Visual Effects:** Planar travel shimmer, trap effect, duplicate spawn
5. **Sound Effects:** Activation sounds, mirror shattering, construct movement

---

## RISK ASSESSMENT

### **HIGH RISK**
| Risk | Impact | Mitigation |
|------|--------|------------|
| **Creature trapping save/load fails** | Critical - lose trapped creatures on reload | Extensive serialization testing, backup data structures |
| **Planar travel breaks existing systems** | High - teleportation conflicts | Careful integration, test existing teleport items |
| **Mirror duplication AI buggy** | High - duplicates don't attack or behave incorrectly | Separate AI controller for duplicates, state machine |
| **Performance issues with 15 trapped creatures** | Medium - UI lag or save file bloat | Optimize serialization, lazy load UI elements |

### **MEDIUM RISK**
| Risk | Impact | Mitigation |
|------|--------|------------|
| **Elemental control Charisma checks unclear** | Medium - balance issues | Reference DMG rules carefully, playtest |
| **Vehicle movement conflicts with player control** | Medium - input handling issues | Separate control scheme when in vehicle |
| **SR stacking edge cases** | Medium - incorrect calculations | Unit tests for all combinations |

### **LOW RISK**
| Risk | Impact | Mitigation |
|------|--------|------------|
| **Missing item icons** | Low - aesthetic only | Use placeholder icons initially |
| **Titan weapon Strength checks** | Low - easy to implement | Existing ability check system |
| **Divine item interactions** | Low - limited player base | Clear tooltips, optional features |

---

## DEPENDENCIES MAP

```
Planar Travel System
├── Amulet of the Planes
├── Cubic Gate
├── Well of Many Worlds
├── Candle of Invocation (Gate spell)
└── Robe of Stars (Astral travel)

Creature Trapping System
├── Iron Flask
├── Efreeti Bottle
└── Mirror of Life Trapping

Spell Resistance System
└── Mantle of Spell Resistance (5 variants)

Mirror Mechanics
├── Mirror of Life Trapping (extends Creature Trapping)
├── Mirror of Opposition
└── Mirror of Mental Prowess

Elemental Summoning
├── Stone of Controlling Earth Elementals
├── Bowl of Commanding Water Elementals
├── Brazier of Commanding Fire Elementals
└── Censer of Controlling Air Elementals

Vehicle System
├── Carpet of Flying (4 sizes)
└── Apparatus of Kwalish

Construct AI
├── Iron Cobra
└── Stone Horse (3 types)

Ally Summoning
└── Horn of Valhalla (4 types)

Independent Items (no major systems)
├── Periapt of Proof Against Poison
├── Scarab, Golembane
├── Scarab of Protection
├── Bottle of Air
├── Wings of Flying
├── Mantle of Faith
├── Robe of Stars
├── Robe of Blending
├── Cloak of the Bat
├── Mattock of the Titans
├── Maul of the Titans
├── Lyre of Building
├── Candle of Invocation
├── Incense of Meditation
├── Phylactery of Faithfulness
└── Mantle of Great Stealth
```

---

## DEVELOPMENT ROADMAP

### **Month 1: Foundation Systems**
- Weeks 1-2: Planar Travel System + 3 items
- Week 3: Spell Resistance + Mantle variants
- Week 4: Simple protection items, Carpets

**Milestone:** Core travel and protection items functional

---

### **Month 2: Complex Trapping & Elementals**
- Weeks 1-3: Creature Trapping System + Iron Flask + Efreeti Bottle
- Week 4: Elemental Control System + 4 items

**Milestone:** Trapping and summoning operational

---

### **Month 3: Mirrors & Vehicles**
- Weeks 1-2: Mirror of Life Trapping (with UI)
- Week 3: Mirror of Opposition
- Week 4: Apparatus of Kwalish

**Milestone:** Legendary mirror mechanics complete

---

### **Month 4: Constructs, Tools & Polish**
- Week 1: Constructs (Iron Cobra, Stone Horse)
- Week 2: Tools & Weapons (Titans, Lyre, Horns)
- Week 3: Miscellaneous items (Divine, Stealth, etc.)
- Week 4: Mirror of Mental Prowess

**Milestone:** All items implemented

---

### **Month 5: Integration & Testing**
- Weeks 1-2: Loot integration, balance testing
- Weeks 3-4: Bug fixing, polish, documentation

**Milestone:** Production-ready release

---

## CONCLUSION

This implementation plan provides a comprehensive roadmap for adding all 34 remaining major wondrous items from D&D 3.5e DMG to the DND35Prototype project. The plan prioritizes high-impact items, builds foundational systems first, and provides detailed specifications for each item and system.

**Key Takeaways:**
- **16-20 week timeline** is realistic with phased approach
- **7 new major systems** required (planar travel, creature trapping, mirrors, etc.)
- **Prioritize P1/P2 items** for maximum player impact
- **Extensive testing required** for save/load, trapping, and AI systems
- **Artifact-level items** (Mirrors, Iron Flask) are end-game content

**Next Steps:**
1. Review and approve this plan
2. Begin Phase 2 (Spell Resistance items)
3. Prototype Planar Travel System (Phase 4)
4. Establish testing protocols for creature trapping

---

**Document Version:** 1.0  
**Last Updated:** May 25, 2026  
**Status:** Awaiting Approval

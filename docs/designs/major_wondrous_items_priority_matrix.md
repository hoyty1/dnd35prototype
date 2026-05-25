# Major Wondrous Items Priority Matrix

**Project:** DND35Prototype  
**Date:** May 25, 2026  
**Purpose:** Prioritize implementation order for maximum impact

---

## PRIORITY FRAMEWORK

### **Evaluation Criteria**

Each item is scored on 4 dimensions (1-5 scale):

**1. Player Impact (1-5)**
- How useful/desirable is this item in actual gameplay?
- Does it enable new strategies or dramatically enhance character power?

**2. Implementation Complexity (1-5)**
- 1 = Trivial (< 1 day)
- 2 = Simple (2-3 days)
- 3 = Moderate (5-7 days)
- 4 = Complex (2-3 weeks)
- 5 = Artifact-level (3-4 weeks)

**3. System Dependencies (0-3)**
- 0 = No new systems required
- 1 = Minor new system or extension
- 2 = Major new system required
- 3 = Multiple major systems required

**4. ROI Score**
- Calculated as: `Player Impact / (Complexity + Dependencies)`
- Higher ROI = better return on development time

---

## COMPLETE PRIORITY MATRIX

| Item | Price (gp) | Impact | Complex | Deps | ROI | Priority |
|------|-----------|--------|---------|------|-----|----------|
| **Mantle of Spell Resistance SR 13** | 90,000 | 5 | 2 | 0 | 2.50 | **P1** |
| **Periapt of Proof Against Poison** | 27,000 | 4 | 2 | 0 | 2.00 | **P1** |
| **Carpet of Flying 10×10** | 60,000 | 5 | 2 | 0 | 2.50 | **P1** |
| **Stone Horse (all 3 types)** | 10k-28k | 4 | 2 | 0 | 2.00 | **P1** |
| **Wings of Flying** | 54,000 | 4 | 2 | 0 | 2.00 | **P1** |
| **Amulet of the Planes** | 120,000 | 5 | 3 | 2 | 1.00 | **P2** |
| **Cubic Gate** | 164,000 | 5 | 3 | 2 | 1.00 | **P2** |
| **Well of Many Worlds** | 82,000 | 3 | 3 | 2 | 0.60 | **P2** |
| **Mantle of Faith** | 76,000 | 4 | 3 | 1 | 1.00 | **P2** |
| **Stone of Controlling Earth Elementals** | 100,000 | 4 | 3 | 2 | 0.80 | **P2** |
| **Bowl of Commanding Water Elementals** | 100,000 | 4 | 3 | 2 | 0.80 | **P2** |
| **Brazier of Commanding Fire Elementals** | 100,000 | 4 | 3 | 2 | 0.80 | **P2** |
| **Censer of Controlling Air Elementals** | 100,000 | 4 | 3 | 2 | 0.80 | **P2** |
| **Scarab of Protection** | 38,000 | 4 | 3 | 1 | 1.00 | **P2** |
| **Robe of Stars** | 58,000 | 3 | 3 | 2 | 0.60 | **P2** |
| **Iron Flask** | 170,000 | 5 | 5 | 3 | 0.63 | **P3** |
| **Efreeti Bottle** | 145,000 | 5 | 4 | 3 | 0.71 | **P3** |
| **Mirror of Life Trapping** | 200,000 | 4 | 5 | 3 | 0.50 | **P3** |
| **Iron Cobra** | 80,000 | 3 | 4 | 2 | 0.50 | **P3** |
| **Apparatus of Kwalish** | 90,000 | 3 | 4 | 2 | 0.50 | **P3** |
| **Mirror of Opposition** | 92,000 | 3 | 4 | 2 | 0.50 | **P3** |
| **Horn of Valhalla, Iron** | 50,000 | 3 | 4 | 2 | 0.50 | **P3** |
| **Mirror of Mental Prowess** | 175,000 | 3 | 5 | 3 | 0.38 | **P4** |
| **Lyre of Building** | 13,000 | 2 | 3 | 1 | 0.50 | **P4** |
| **Mattock of the Titans** | 23,348 | 2 | 3 | 1 | 0.50 | **P4** |
| **Maul of the Titans** | 25,305 | 2 | 3 | 1 | 0.50 | **P4** |
| **Candle of Invocation** | 8,400 | 3 | 3 | 2 | 0.60 | **P4** |
| **Incense of Meditation** | 4,900 | 3 | 3 | 1 | 0.75 | **P4** |
| **Phylactery of Faithfulness** | 1,000 | 2 | 3 | 2 | 0.40 | **P4** |
| **Robe of Blending** | 8,400 | 2 | 2 | 0 | 1.00 | **P4** |
| **Cloak of the Bat** | 26,000 | 3 | 3 | 1 | 0.75 | **P4** |
| **Mantle of Great Stealth** | 242,000 | 4 | 3 | 1 | 1.00 | **P4** |
| **Scarab, Golembane** | 2,500 | 2 | 2 | 0 | 1.00 | **P4** |
| **Bottle of Air** | 7,250 | 2 | 2 | 0 | 1.00 | **P4** |

---

## PRIORITY 1 (P1): QUICK WINS - HIGH IMPACT, LOW COMPLEXITY

**Theme:** Build on existing systems, high player value, fast implementation

**Total Items:** 8 (counting all variants)  
**Estimated Time:** 2-3 weeks  
**ROI Range:** 2.00-2.50

### **Items List**

#### **1. Mantle of Spell Resistance (5 variants)**
**Impact:** 5/5 - SR is crucial for high-level magic defense  
**Complexity:** 2/5 - SR system likely exists (Robe of Archmagi has SR 18)  
**Dependencies:** 0 - Just verify SR stacking rules  
**Time:** 1 day for all 5 variants

**Why P1:**
- Spell Resistance is one of the most valuable defenses at high levels
- Simple data-driven implementation (just different SR values)
- Players will use these immediately
- Enables survival against high-level casters

---

#### **2. Carpet of Flying (4 sizes)**
**Impact:** 5/5 - Flight is universally useful, iconic item  
**Complexity:** 2/5 - Flight mechanics likely exist  
**Dependencies:** 0 - Vehicle/passenger tracking (simple)  
**Time:** 1 day for all 4 sizes

**Why P1:**
- Flight fundamentally changes exploration and combat
- Bypasses terrain hazards
- Party favorite item
- Scales with party size (different carpet sizes)

---

#### **3. Periapt of Proof Against Poison**
**Impact:** 4/5 - Complete poison immunity is powerful  
**Complexity:** 2/5 - Single flag toggle  
**Dependencies:** 0 - Poison system likely exists  
**Time:** 0.5 days

**Why P1:**
- Hard counter to poison-based enemies
- Simple "set immunity flag" implementation
- Valuable for dungeon crawling

---

#### **4. Stone Horse (3 types)**
**Impact:** 4/5 - Reliable mounts with combat utility  
**Complexity:** 2/5 - Figurine system likely exists from minor items  
**Dependencies:** 0 - Reuse Figurines of Wondrous Power code  
**Time:** 1 day for all 3 types

**Why P1:**
- Mounts solve travel/mobility issues
- Greater Destrier can fly (very useful)
- Low implementation cost if figurine system exists

---

#### **5. Wings of Flying**
**Impact:** 4/5 - Unlimited flight for one character  
**Complexity:** 2/5 - Grant fly speed  
**Dependencies:** 0 - Flight system exists  
**Time:** 0.5 days

**Why P1:**
- Personal flight is always useful
- Wearable (doesn't take up hands/actions like Carpet)
- Simple implementation

---

### **Additional P1 Items (Lower Impact)**

- **Robe of Blending** - +10 Hide bonus (stealth builds)
- **Scarab, Golembane** - +2 vs golems, allow crits (niche but simple)
- **Bottle of Air** - Breathe underwater/toxic air (situational but simple)

---

## PRIORITY 2 (P2): CORE SYSTEMS - HIGH IMPACT, MODERATE COMPLEXITY

**Theme:** Implement foundational systems that unlock multiple items

**Total Items:** 13  
**Estimated Time:** 8-10 weeks  
**ROI Range:** 0.60-1.00

### **System-Dependent Items**

#### **PLANAR TRAVEL SYSTEM** (Implement Week 1-2)

**Items Unlocked:**
1. **Amulet of the Planes** (120,000 gp) - At-will plane shift
2. **Cubic Gate** (164,000 gp) - Gates to 6 planes
3. **Well of Many Worlds** (82,000 gp) - Random portals
4. **Robe of Stars** (58,000 gp) - Astral travel

**Why P2:**
- Enables high-level planar adventures
- Opens entire new gameplay dimension
- 4 items depend on this system
- Amulet and Cubic Gate are extremely powerful

---

#### **ELEMENTAL SUMMONING SYSTEM** (Implement Week 3)

**Items Unlocked:**
1. **Stone of Controlling Earth Elementals** (100,000 gp)
2. **Bowl of Commanding Water Elementals** (100,000 gp)
3. **Brazier of Commanding Fire Elementals** (100,000 gp)
4. **Censer of Controlling Air Elementals** (100,000 gp)

**Why P2:**
- All 4 items use same system (efficient)
- Elder Elementals are powerful combat allies
- Control mechanics add tactical depth

---

#### **MULTI-ABILITY ITEMS** (No new systems, moderate complexity)

1. **Mantle of Faith** (76,000 gp) - +5 saves + 4 spells/day
2. **Scarab of Protection** (38,000 gp) - +4 saves + absorptions

**Why P2:**
- High defensive value
- Useful for clerics/divine casters
- Moderate complexity but no major systems

---

## PRIORITY 3 (P3): ARTIFACT-LEVEL - HIGH COMPLEXITY, HIGH IMPACT

**Theme:** End-game legendary items requiring major systems

**Total Items:** 7  
**Estimated Time:** 11-15 weeks  
**ROI Range:** 0.50-0.71

### **CREATURE TRAPPING SYSTEM** (Implement Weeks 1-3)

**Items Unlocked:**
1. **Iron Flask** (170,000 gp) - Trap any creature
2. **Efreeti Bottle** (145,000 gp) - Summon efreeti, trap outsiders
3. **Mirror of Life Trapping** (200,000 gp) - Trap 15 creatures, UI

**Why P3:**
- Extremely iconic items (especially Iron Flask)
- Complex system required (serialization, save/load)
- High player interest but significant dev time
- Efreeti Bottle includes wish mechanics (very complex)

---

### **CONSTRUCT GUARDIANS**

1. **Iron Cobra** (80,000 gp) - Autonomous guardian with poison
2. **Apparatus of Kwalish** (90,000 gp) - Vehicle with lever controls

**Why P3:**
- Unique gameplay (guardians, vehicle control)
- Complex AI and control systems
- Cool factor is high but niche utility

---

### **ADVANCED MIRRORS**

1. **Mirror of Opposition** (92,000 gp) - Create evil duplicate
2. **Horn of Valhalla, Iron** (50,000 gp) - Summon berserker army

**Why P3:**
- Complex character duplication
- Alignment inversion logic
- Multiple temporary allies (Horn)
- High complexity for moderate impact

---

## PRIORITY 4 (P4): NICHE UTILITY - SITUATIONAL USE

**Theme:** Specialized items for specific builds or situations

**Total Items:** 11  
**Estimated Time:** 4-6 weeks  
**ROI Range:** 0.38-1.00

### **Divine/Caster Items**

1. **Candle of Invocation** (8,400 gp) - Gate + CL boost for divine casters
2. **Incense of Meditation** (4,900 gp) - Spell preparation boost
3. **Phylactery of Faithfulness** (1,000 gp) - Divine code checker

**Why P4:**
- Divine caster only
- Situational benefits
- Complex divine code system (Phylactery)

---

### **Titan Weapons & Tools**

1. **Mattock of the Titans** (23,348 gp) - Requires Str 19, excavation
2. **Maul of the Titans** (25,305 gp) - Requires Str 23, sunder
3. **Lyre of Building** (13,000 gp) - Fabricate spell (construction)

**Why P4:**
- High Strength requirement limits users (Titan weapons)
- Construction-focused (Lyre) - niche utility
- Cool but not combat-essential

---

### **Stealth & Utility**

1. **Mantle of Great Stealth** (242,000 gp) - +10 Hide/Move Silently, invisibility
2. **Cloak of the Bat** (26,000 gp) - Polymorph into bat, fly
3. **Mirror of Mental Prowess** (175,000 gp) - 5 mental abilities (scrying, etc.)

**Why P4:**
- Stealth-focused (rogue/assassin builds)
- Mental Prowess is artifact-level complexity but niche appeal
- Lower priority than combat/survival items

---

## IMPLEMENTATION ROADMAP

### **MONTH 1: P1 Quick Wins**
**Goal:** Maximize player value with minimal dev time

**Week 1:**
- Mantle of Spell Resistance (all 5)
- Periapt of Proof Against Poison
- Scarab, Golembane
- Bottle of Air

**Week 2:**
- Carpet of Flying (all 4 sizes)
- Wings of Flying

**Week 3:**
- Stone Horse (all 3 types)
- Robe of Blending

**Week 4:**
- Testing, bug fixes, loot integration

**Deliverable:** 8 items functional, players can use immediately

---

### **MONTH 2-3: P2 Core Systems**
**Goal:** Build foundational systems for planar travel and summoning

**Weeks 1-2: Planar Travel System**
- Build plane database, travel mechanics
- Amulet of the Planes
- Cubic Gate
- Well of Many Worlds
- Robe of Stars

**Week 3: Elemental Control System**
- Build summoning + control mechanics
- Stone/Bowl/Brazier/Censer (all 4 elemental items)

**Week 4-5: Multi-Ability Items**
- Mantle of Faith
- Scarab of Protection

**Week 6: Testing & Integration**

**Deliverable:** 11 items, planar adventures enabled

---

### **MONTH 4-6: P3 Artifacts**
**Goal:** Implement legendary end-game items

**Weeks 1-3: Creature Trapping System**
- Build serialization, trap mechanics
- Iron Flask
- Efreeti Bottle (including wish mechanics)

**Weeks 4-5: Mirror of Life Trapping**
- Build UI for viewing trapped creatures
- Mass release mechanics

**Weeks 6-7: Constructs**
- Iron Cobra
- Apparatus of Kwalish (vehicle + levers)

**Weeks 8-9: Advanced Mirrors & Summoning**
- Mirror of Opposition
- Horn of Valhalla (all 4 types)

**Week 10: Testing & Polish**

**Deliverable:** 7 artifact-level items complete

---

### **MONTH 7: P4 Niche Items (Optional)**
**Goal:** Round out the item collection

**Weeks 1-2:**
- Divine items (Candle, Incense, Phylactery)
- Titan weapons (Mattock, Maul)
- Lyre of Building

**Weeks 3-4:**
- Stealth items (Mantle of Great Stealth, Cloak of Bat)
- Mirror of Mental Prowess

**Deliverable:** All 39 items complete

---

## DECISION MATRIX

Use this decision tree to determine implementation order:

```
START
├─ Does it build on existing systems? (No new systems)
│  ├─ YES → Is player impact high (4-5)?
│  │  ├─ YES → **P1** (Quick Win)
│  │  └─ NO → **P4** (Low Priority)
│  └─ NO → Does it unlock multiple items?
│     ├─ YES → **P2** (Core System)
│     └─ NO → Is it artifact-level complexity (5)?
│        ├─ YES → **P3** (Artifact)
│        └─ NO → **P2** or **P4** (depends on impact)
```

---

## RISK ASSESSMENT BY PRIORITY

### **P1 Risks:** LOW
- All items leverage existing systems
- Well-understood mechanics
- Fast implementation
- **Mitigation:** Thorough testing of SR stacking, flight mechanics

---

### **P2 Risks:** MEDIUM
- **Planar Travel:** New system, could break existing teleportation
  - **Mitigation:** Careful integration, separate plane-shift from teleport
- **Elemental Summoning:** Elder Elementals need balanced stats
  - **Mitigation:** Reference DMG stats exactly, playtest

---

### **P3 Risks:** HIGH
- **Creature Trapping:** Serialization bugs could corrupt saves
  - **Mitigation:** Extensive save/load testing, backup systems
- **Mirror of Life Trapping UI:** Could be performance issue with 15 creatures
  - **Mitigation:** Virtual scrolling, lazy loading portraits
- **Efreeti Bottle Wishes:** Wish spell is notoriously broken
  - **Mitigation:** Limit wish scope, DM approval required

---

### **P4 Risks:** LOW-MEDIUM
- **Divine Code System (Phylactery):** Complex logic for action evaluation
  - **Mitigation:** Start with simple tenets, expand later
- **Mirror of Mental Prowess:** 5 separate abilities, high integration cost
  - **Mitigation:** Implement abilities one at a time

---

## SUCCESS METRICS

### **After P1 (Month 1):**
- [ ] 8 items functional
- [ ] Player feedback: "I want to use these items"
- [ ] No major bugs
- [ ] Loot tables updated

### **After P2 (Month 3):**
- [ ] 19 total items functional
- [ ] Planar travel working (no crashes, correct environmental effects)
- [ ] Elementals balanced in combat
- [ ] Players using planar items for exploration

### **After P3 (Month 6):**
- [ ] 26 total items functional
- [ ] Creature trapping works through save/load
- [ ] Iron Flask is iconic/fun to use
- [ ] No performance issues with Mirror UI

### **After P4 (Month 7):**
- [ ] All 39 items complete
- [ ] Comprehensive testing complete
- [ ] Documentation updated
- [ ] Ready for release

---

## BUDGET ALLOCATION

**Total Estimated Time:** 28-30 weeks (7 months)

**By Priority:**
- **P1:** 2-3 weeks (8% of total time, 21% of items)
- **P2:** 8-10 weeks (32% of total time, 33% of items)
- **P3:** 11-15 weeks (48% of total time, 18% of items)
- **P4:** 4-6 weeks (16% of total time, 28% of items)

**Recommendation:**
- **Minimum Viable Product (MVP):** P1 + P2 = 10-13 weeks
  - Delivers 19 most impactful items
  - Includes major systems (planar travel, summoning)
  - 65% player value for 40% dev time

- **Full Release:** P1 + P2 + P3 = 21-28 weeks
  - Delivers 26 items including artifacts
  - All major systems complete
  - Can defer P4 to post-release updates

---

## DEPENDENCY GRAPH

```
P1: Quick Wins (No dependencies)
└─ Mantle SR, Carpet, Periapt, Stone Horse, Wings, etc.

P2: Core Systems
├─ Planar Travel System
│  └─ Amulet of Planes, Cubic Gate, Well, Robe of Stars
├─ Elemental Summoning System
│  └─ Stone, Bowl, Brazier, Censer (4 items)
└─ Multi-Ability Items (independent)
   └─ Mantle of Faith, Scarab of Protection

P3: Artifacts
├─ Creature Trapping System
│  ├─ Iron Flask
│  ├─ Efreeti Bottle
│  └─ Mirror of Life Trapping (+ UI)
├─ Construct AI System
│  ├─ Iron Cobra
│  └─ Apparatus of Kwalish
└─ Mirror Mechanics
   ├─ Mirror of Opposition
   └─ Mirror of Mental Prowess

P4: Niche Items (independent or small systems)
└─ Divine, Titan Weapons, Stealth items
```

---

## FINAL RECOMMENDATIONS

### **PHASE 1 (Immediate - Month 1):**
Implement **P1 items only**. These give maximum player satisfaction with minimum dev time. Get these into players' hands quickly for feedback.

### **PHASE 2 (Months 2-3):**
Build **Planar Travel System** and **Elemental Summoning**. These are high-impact systems that unlock 11 items and enable planar adventures.

### **PHASE 3 (Months 4-6):**
Implement **Creature Trapping System** and artifacts. These are end-game content and can be released later once core systems are stable.

### **PHASE 4 (Month 7 - Optional):**
Polish and add **P4 niche items**. These can be added in post-release updates based on player demand.

---

**Document Version:** 1.0  
**Last Updated:** May 25, 2026  
**Status:** Ready for Review

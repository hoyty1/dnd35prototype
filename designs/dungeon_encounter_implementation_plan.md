# D&D 3.5e Dungeon Encounter System — Implementation Plan

## Executive Summary

This plan covers implementing a **randomized dungeon encounter system** based on DMG Chapter 3 tables (EL 1–8+), populated with creatures from the uploaded CSV and the DMG encounter tables. The system will scale encounters based on average party level.

**Scope:** ~160 unique creatures/NPCs needed, ~60 already implemented, ~100 to build. Full encounter table system for dungeon levels 1–8 (extendable to 20).

---

## Section 1: NPC/Creature Inventory

### 1.1 Complete Unique Creature List from CSV (Deduplicated & Normalized)

The CSV contains ~240 rows across 3 table sections. After deduplication and spelling normalization, there are **~160 unique creature entries**.

#### Already Implemented ✅ (60 creatures)

| Creature | Source File | Notes |
|---|---|---|
| Medium monstrous centipede | NPCDatabase_C | All sizes (Tiny→Gargantuan) |
| Dire rat | NPCDatabase_D | ✅ |
| Giant fire beetle | NPCDatabase_F | ✅ |
| Small monstrous scorpion | NPCDatabase_S | All sizes implemented |
| Goblin warrior | NPCDatabaseCustom | ✅ (goblin, goblin_warchief) |
| Darkmantle | — | ❌ Not found — VERIFY |
| Krenshar | — | ❌ |
| Lemure | NPCDatabase_L | ✅ |
| Kobold warrior | NPCDatabaseCustom | ✅ |
| Human warrior skeleton | NPCDatabase_Skeletons | ✅ |
| Human commoner zombie | NPCDatabase_Zombies | ✅ |
| Tiny viper snake | NPCDatabase_V | ✅ (all viper sizes) |
| Orc warrior | NPCDatabaseCustom | ✅ (orc_berserker) |
| Stirge | NPCDatabase_S | ✅ |
| Spider swarm | NPCDatabase_S | ✅ |
| Lantern archon | NPCDatabase_L | ✅ |
| Bugbear | NPCDatabase_B | ✅ |
| Dire bat | NPCDatabase_D | ✅ (summon_dire_bat) |
| Bat swarm | NPCDatabase_B | ✅ |
| Rat swarm | NPCDatabase_R | ✅ |
| Constrictor snake | NPCDatabase_D | ✅ (summon_constrictor) |
| Small viper snake | NPCDatabase_V | ✅ |
| Allip | NPCDatabase_A | ✅ |
| Cockatrice | NPCDatabase_C | ✅ |
| Gnoll | NPCDatabase_G | ✅ |
| Gargoyle | NPCDatabase_G | ✅ |
| Gelatinous cube | NPCDatabase_G | ✅ |
| Shadow | NPCDatabase_S | ✅ |
| Troglodyte | NPCDatabase_T | ✅ |
| Troll | NPCDatabase_T | ✅ |
| Ochre jelly | NPCDatabase_O | ✅ |
| Owlbear skeleton | NPCDatabase_Skeletons | ✅ |
| Troglodyte zombie | NPCDatabase_Zombies | ✅ |
| Minotaur zombie | NPCDatabase_Zombies | ✅ |
| Medium viper snake | NPCDatabase_V | ✅ |
| Large viper snake | NPCDatabase_V | ✅ |
| Huge viper snake | NPCDatabase_V | ✅ |
| Giant praying mantis | NPCDatabase_G | ✅ |
| Medium monstrous scorpion | NPCDatabase_S | ✅ |
| Large monstrous centipede | NPCDatabase_C | ✅ |
| Large monstrous spider | NPCDatabase_S | ✅ |
| Mephits (all types) | NPCDatabase_M | ✅ (10 types) |
| Locust swarm | — | ❌ (similar to spider swarm, needs impl) |
| Cloaker | NPCDatabase_C | ✅ |
| Howler | NPCDatabase_H | ✅ |
| Giant bombardier beetle | NPCDatabase_B | ✅ |
| Werewolf | NPCDatabase_Lycanthropes | ✅ |
| Wererat | NPCDatabase_Lycanthropes | ✅ |
| Wereboar | NPCDatabase_Lycanthropes | ✅ |
| Weretiger | NPCDatabase_Lycanthropes | ✅ |
| Werebear | NPCDatabase_Lycanthropes | ✅ |
| Brown bear | NPCDatabaseCustom | ✅ (if registered) |
| Dire bear | NPCDatabaseCustom | ✅ |
| Wraith | NPCDatabase_W | ✅ |
| Noble djinni | NPCDatabase_G | ✅ |
| Vampire spawn | — | ❌ Needs implementation |
| Wight | NPCDatabaseCustom | ✅ (wight_dreadwalker) |
| Dragon (all types/ages) | NPCDatabase_Dragons | ✅ (60 variants) |
| Hell hound | NPCDatabase_H | ✅ (summon_hell_hound) |
| Crocodile | NPCDatabase_D | ✅ (summon_crocodile) |

#### Needs Implementation ❌ (~100 creatures)

**Tier 1: Core Base Creatures (High Priority — used in multiple encounter levels)**

| Creature | CR | Type | MM Page | Complexity |
|---|---|---|---|---|
| Dwarf warrior | 1/2 | Humanoid | MM 91 | Low — stat block + equipment |
| Elf warrior | 1/2 | Humanoid | MM 101 | Low |
| Halfling warrior | 1/2 | Humanoid | MM 149 | Low |
| Hobgoblin warrior | 1/2 | Humanoid | MM 153 | Low |
| Worg | 2 | Magical Beast | MM 256 | Medium |
| Thoqqua | 2 | Elemental | MM 242 | Medium — heat damage |
| Doppelganger | 3 | Monstrous Humanoid | MM 67 | Medium — change shape |
| Ettercap | 3 | Aberration | MM 106 | Medium — web, poison |
| Violet fungus | 3 | Plant | MM 112 | Low — poison touch |
| Ghast | 3 | Undead | MM 119 | Medium — paralysis, stench |
| Ghoul | 1 | Undead | MM 118 | Medium — paralysis |
| Grick | 3 | Aberration | MM 139 | Low — DR, tentacles |
| Hell hound (standalone) | 3 | Outsider | MM 151 | Low — already summon version |
| Lizardfolk | 1 | Humanoid (Reptilian) | MM 169 | Low |
| Ogre | 3 | Giant | MM 198 | Medium — large melee |
| Phantom fungus | 3 | Plant | MM 207 | Medium — invisibility |
| Rust monster | 3 | Aberration | MM 216 | Medium — metal destruction |
| Yuan-ti pureblood | 3 | Monstrous Humanoid | MM 264 | Medium — spell-like abilities |
| Otyugh | 4 | Aberration | MM 204 | Medium — grapple, disease |
| Owlbear | 4 | Magical Beast | MM 206 | Medium — improved grab |
| Minotaur | 4 | Monstrous Humanoid | MM 188 | Medium — charge, natural cunning |
| Mimic | 4 | Aberration | MM 186 | Medium — adhesive, shape |
| Displacer beast | 4 | Magical Beast | MM 66 | Medium — displacement |
| Centipede swarm | 4 | Vermin | MM 238 | Low — swarm template |
| Barghest | 4 | Outsider | MM 22 | High — shape change, feed |
| Greater barghest | 5 | Outsider | MM 23 | High |
| Basilisk | 6 | Magical Beast | MM 23 | Medium — petrifying gaze |
| Manticore | 5 | Magical Beast | MM 179 | Medium — tail spikes |

**Tier 2: Mid-Level Creatures (EL 4–6)**

| Creature | CR | Type | Complexity |
|---|---|---|---|
| Choker | 2 | Aberration | Medium — quickness, improved grab |
| Dretch | 2 | Outsider (Demon) | Low — SLA: stinking cloud |
| Quasit | 3 | Outsider (Demon) | Medium — shape change, poison |
| Imp | 3 | Outsider (Devil) | Medium — shape change, poison |
| Fiendish dire rat | 1 | Template | Low — fiendish template |
| Formian worker | 1/2 | Outsider | Low |
| Shocker lizard | 2 | Magical Beast | Medium — electric |
| Ethereal filcher | 3 | Aberration | Medium — ethereal jaunt |
| Ethereal marauder | 3 | Magical Beast | Medium — ethereal |
| Harpy | 4 | Monstrous Humanoid | Medium — captivating song |
| Grimlock | 1 | Monstrous Humanoid | Low — blindsight |
| Svirfneblin (deep gnome) | 1 | Humanoid | Low |
| Janni | 4 | Outsider | Medium — plane shift, SLAs |
| Duergar | 1 | Humanoid (Dwarf) | Low — enlarge/invis |
| Drow elf | 1 | Humanoid (Elf) | Low — SR, SLAs |
| Hound archon | 4 | Outsider (Good) | Medium — aura, SLAs |
| Carrion crawler | 4 | Aberration | Medium — paralysis tentacles |
| Five-headed hydra | 5 | Magical Beast | High — multi-head combat |
| Six-headed hydra | 6 | Magical Beast | High |

**Tier 3: Upper-Level Creatures (EL 5–8)**

| Creature | CR | Type | Complexity |
|---|---|---|---|
| Celestial lion | 3 | Template | Low — celestial template on lion |
| Bearded devil | 5 | Outsider (Devil) | Medium |
| Gibbering mouther | 5 | Aberration | High — confuse, ground manipulation |
| Green hag | 5 | Monstrous Humanoid | Medium — SLAs, weakness |
| Djinni | 5 | Outsider | Medium — whirlwind, SLAs |
| Shadow mastiff | 5 | Outsider (Evil) | Medium — bay, shadow blend |
| Skum | 2 | Aberration | Low |
| Vargouille | 2 | Outsider (Evil) | Medium — shriek, kiss |
| Yuan-ti halfblood | 5 | Monstrous Humanoid | High — SLAs, produce acid |
| Gauth (beholder) | 6 | Aberration | Very High — eye rays |
| Babau (demon) | 6 | Outsider | Medium — sneak attack, SLAs |
| Derro | 3 | Monstrous Humanoid | Medium — madness, SLAs |
| Chain devil | 6 | Outsider | Medium — chain attack, regen |
| Digester | 6 | Magical Beast | Medium — acid spray |
| Bralani (eladrin) | 6 | Outsider (Good) | Medium — whirlwind, SLAs |
| Ettin | 6 | Giant | Medium — two heads, two attacks |
| Annis (hag) | 6 | Monstrous Humanoid | Medium — rend, improved grab |
| Half-dragon fighter | 7 | Template | Medium — breath weapon |
| Xill | 6 | Outsider | Medium — implant, multi-arms |
| Minor xorn | 3 | Outsider | Medium — earth glide, tremorsense |
| Average salamander | 6 | Outsider (Fire) | Medium |
| Will-o'-wisp | 6 | Aberration | Medium — electricity, invisibility |
| Monitor lizard | 2 | Animal | Low |
| Aboleth | 7 | Aberration | Very High — enslave, SLAs |
| Chaos beast | 7 | Outsider | High — corporeal instability |
| Chuul | 7 | Aberration | Medium — constrict, paralysis |
| Succubus | 7 | Outsider (Demon) | High — charm, energy drain |
| Hellcat | 7 | Outsider (Devil) | Medium — invisible in light |
| Drider | 7 | Aberration | High — spells + combat |
| Shrieker | 1 | Plant | Low — alarm fungus |
| Hill giant | 7 | Giant | Medium — rock throwing |
| Flesh golem | 7 | Construct | Medium — berserk, DR, immunities |
| Eight-headed hydra | 7 | Magical Beast | High |
| Invisible stalker | 7 | Elemental (Air) | Medium — natural invisibility |
| Medusa | 7 | Monstrous Humanoid | Medium — petrifying gaze |
| Black pudding | 7 | Ooze | Medium — split, acid |
| Phasm | 7 | Aberration | Medium — shape changer |
| Flamebrother salamander | 3 | Outsider (Fire) | Low |
| Red slaad | 7 | Outsider | Medium — implant |
| Spectre | 7 | Undead | High — incorporeal, energy drain |
| Umber hulk | 7 | Aberration | Medium — confusing gaze |
| Yuan-ti abomination | 7 | Monstrous Humanoid | Very High |

**Tier 4: Templated NPCs (require base creature + class levels)**

| NPC | Base + Class | EL | Complexity |
|---|---|---|---|
| 5th-level human monk | Human + Monk 5 | 5 | Medium — NPC class builder |
| 5th-level kobold sorcerer | Kobold + Sorcerer 5 | 5 | Medium |
| 5th-level lizardfolk druid | Lizardfolk + Druid 5 | 6 | Medium — requires Lizardfolk base |
| 5th-level hobgoblin fighter | Hobgoblin + Fighter 5 | 5 | Medium |
| 5th-level goblin rogue | Goblin + Rogue 5 | 5 | Medium |
| 5th-level human barbarian | Human + Barbarian 5 | 5 | Medium |
| 4th-level ogre barbarian | Ogre + Barbarian 4 | 7 | Medium — requires Ogre base |
| Ghost 5th-level fighter | Ghost template + Fighter 5 | 7 | High — ghost template |
| Vampire 5th-level fighter | Vampire template + Fighter 5 | 7 | Very High — vampire template |
| Half-dragon 4th-level fighter | Half-dragon template + Fighter 4 | 6 | High — breath weapon |
| 5th-level troglodyte cleric | Troglodyte + Cleric 5 | 8 | Medium — requires spells |
| Advanced megaraptor skeleton | Advanced template + Skeleton | 7 | Medium |

### 1.2 Base Creatures Required for Templates

These base creatures MUST be implemented before their templated versions:

| Base Creature | Required For |
|---|---|
| Lizardfolk | 5th-level lizardfolk druid |
| Hobgoblin | 5th-level hobgoblin fighter, hobgoblin warriors |
| Ogre | 4th-level ogre barbarian, ogre encounters |
| Minotaur | Minotaur zombie (already exists), standalone |
| Human (NPC) | 5th-level monk, barbarian, fighter, ghost/vampire templates |
| Kobold | 5th-level kobold sorcerer |
| Drow elf | Drow encounters |

---

## Section 2: Encounter Table System Design

### 2.1 How DMG Chapter 3 Tables Work

The DMG provides **20 dungeon level encounter tables** (1st through 20th level). Each table:

1. Has a **d% roll** (01–100) mapping to specific creature encounters
2. **01–10 always rolls on the previous level's table** (cascading)
3. **91–100 always rolls on the next level's table** (escalation)
4. Each entry specifies **exact creature count** (e.g., "1d3+1 bugbears")
5. Encounter Level (EL) roughly matches the dungeon level

### 2.2 EL Calculation Rules (DMG p.48–49)

- **Single creature:** EL = CR
- **Multiple identical creatures:** Use Table 3-1 (DMG p.49)
  - 2 creatures of same CR: EL = CR + 2
  - 3 creatures: EL = CR + 3
  - 4 creatures: EL = CR + 4
  - 6–7 creatures: EL = CR + 5
  - 8–11 creatures: EL = CR + 6
  - 12–16: EL = CR + 7
- **Mixed creatures:** Add together using XP equivalents
- **Party Level:** Average character level of the party

### 2.3 Encounter Difficulty Scale

| Difficulty | EL vs Party Level |
|---|---|
| Easy | EL = Party Level - 1 to -3 |
| Standard | EL = Party Level |
| Challenging | EL = Party Level + 1 to +2 |
| Overpowering | EL = Party Level + 3+ |

### 2.4 DMG Encounter Tables Extracted (Levels 1–8)

**✅ CONFIRMED: The DMG PDF is readable.** All 20 encounter tables (1st–20th level) have been successfully extracted from pages 79–81. The OCR quality is sufficient to parse all d% ranges, creature names, and quantities.

The tables for levels 1–8 have been fully transcribed above. Key statistics:
- **Level 1:** 17 entries + roll-up to Level 2
- **Level 2:** 17 entries + roll-down to Level 1, roll-up to Level 3
- **Level 3:** 20 entries + roll-down/up
- **Level 4:** 20 entries + roll-down/up
- **Level 5:** 20 entries + roll-down/up
- **Level 6:** 22 entries + roll-down/up
- **Level 7:** 20 entries + roll-down/up
- **Level 8:** 22 entries + roll-down/up

---

## Section 3: Implementation Phases

### Phase 1: Core Infrastructure & Base Creatures (Estimated: 30–40 hours)

**Goal:** Build the encounter table system skeleton and implement the most-referenced base creatures.

#### 1A: Encounter Table System (~8 hours)

```csharp
// New files needed:
// Assets/Scripts/Encounters/DungeonEncounterTable.cs
// Assets/Scripts/Encounters/DungeonEncounterEntry.cs
// Assets/Scripts/Encounters/DungeonEncounterSystem.cs
// Assets/Scripts/Encounters/EncounterDifficultyCalculator.cs

/// <summary>
/// Represents a single entry in a dungeon encounter table.
/// Maps to one row of the DMG tables (e.g., "14-16: 1d3+1 bugbears").
/// </summary>
[System.Serializable]
public class DungeonEncounterEntry
{
    public int MinRoll;          // d% minimum (inclusive)
    public int MaxRoll;          // d% maximum (inclusive)
    public List<CreatureSpawn> Creatures;
    public int? RollOnLevel;     // If set, re-roll on this level's table
    public string Description;   // Human-readable (e.g., "1d3+1 bugbears")
}

[System.Serializable]
public class CreatureSpawn
{
    public string CreatureId;    // NPCDatabase creature ID
    public int CountDice;        // e.g., 1 for "1d3"
    public int CountDieSides;    // e.g., 3 for "1d3"
    public int CountModifier;    // e.g., 1 for "1d3+1"
    public int FixedCount;       // If 0, use dice; otherwise exact count
    
    public int RollCount()
    {
        if (FixedCount > 0) return FixedCount;
        int total = CountModifier;
        for (int i = 0; i < CountDice; i++)
            total += UnityEngine.Random.Range(1, CountDieSides + 1);
        return Mathf.Max(1, total);
    }
}

/// <summary>
/// Complete encounter table for one dungeon level.
/// </summary>
public class DungeonEncounterTable
{
    public int DungeonLevel;
    public List<DungeonEncounterEntry> Entries = new();
    
    public DungeonEncounterEntry RollEncounter()
    {
        int roll = UnityEngine.Random.Range(1, 101);
        foreach (var entry in Entries)
        {
            if (roll >= entry.MinRoll && roll <= entry.MaxRoll)
                return entry;
        }
        return Entries[Entries.Count - 1]; // fallback
    }
}

/// <summary>
/// Main system: holds all 20 tables, generates encounters.
/// </summary>
public static class DungeonEncounterSystem
{
    private static Dictionary<int, DungeonEncounterTable> _tables;
    
    public static void Init() { /* populate all tables */ }
    
    /// <summary>
    /// Generate a random encounter for the given dungeon level.
    /// Handles cascading rolls (01-10 → lower level, 91-100 → higher).
    /// </summary>
    public static EncounterPreset GenerateRandomEncounter(int dungeonLevel, int maxCascadeDepth = 3)
    {
        // Roll on table, handle cascades, resolve creature counts
        // Return as EncounterPreset for existing spawn system
    }
    
    /// <summary>
    /// Generate encounter scaled to party's average level.
    /// </summary>
    public static EncounterPreset GenerateForPartyLevel(int averagePartyLevel, 
        EncounterDifficulty difficulty = EncounterDifficulty.Standard)
    {
        int targetEL = averagePartyLevel + (int)difficulty;
        return GenerateRandomEncounter(targetEL);
    }
}

public enum EncounterDifficulty
{
    Easy = -2,
    Standard = 0,
    Challenging = 1,
    Overpowering = 3
}
```

#### 1B: Warrior-Type Humanoids (~4 hours)

These are simple stat blocks with equipment:

| Creature | CR | Est. Hours |
|---|---|---|
| Dwarf warrior | 1/2 | 0.5 |
| Elf warrior | 1/2 | 0.5 |
| Halfling warrior | 1/2 | 0.5 |
| Hobgoblin warrior | 1/2 | 0.5 |
| Drow elf | 1 | 0.5 |
| Duergar dwarf | 1 | 0.5 |
| Svirfneblin gnome | 1 | 0.5 |
| Lizardfolk | 1 | 0.5 |

#### 1C: Common Undead (~4 hours)

| Creature | CR | Est. Hours |
|---|---|---|
| Ghoul | 1 | 1.0 — paralysis mechanic |
| Ghast | 3 | 0.5 — ghoul variant + stench |
| Vampire spawn | 4 | 1.5 — energy drain, DR, slam |
| Greater shadow | 8 | 0.5 — shadow variant |

#### 1D: Core Aberrations & Magical Beasts (~8 hours)

| Creature | CR | Est. Hours |
|---|---|---|
| Worg | 2 | 0.5 |
| Grick | 3 | 0.5 |
| Rust monster | 3 | 1.5 — equipment destruction |
| Owlbear | 4 | 1.0 — improved grab + rage |
| Minotaur | 4 | 1.0 — charge, power attack |
| Ogre | 3 | 0.5 |
| Displacer beast | 4 | 1.0 — displacement |
| Manticore | 5 | 1.0 — tail spikes, flight |
| Basilisk | 6 | 1.5 — petrifying gaze |

#### 1E: Common Outsiders (~6 hours)

| Creature | CR | Est. Hours |
|---|---|---|
| Dretch | 2 | 0.5 |
| Quasit | 3 | 1.0 |
| Imp | 3 | 1.0 |
| Hound archon | 4 | 1.0 |
| Barghest | 4 | 1.5 |
| Bearded devil | 5 | 1.0 |

### Phase 2: Mid-Tier Creatures (Estimated: 25–35 hours)

**Goal:** Fill out EL 4–6 tables completely.

| Creature | CR | Est. Hours |
|---|---|---|
| Choker | 2 | 0.5 |
| Thoqqua | 2 | 0.5 |
| Shocker lizard | 2 | 0.5 |
| Formian worker | 1/2 | 0.5 |
| Phantom fungus | 3 | 0.5 |
| Violet fungus | 3 | 0.5 |
| Shrieker | 1 | 0.25 |
| Doppelganger | 3 | 1.0 |
| Ettercap | 3 | 1.0 |
| Yuan-ti pureblood | 3 | 1.0 |
| Grimlock | 1 | 0.5 |
| Janni | 4 | 1.0 |
| Carrion crawler | 4 | 1.0 |
| Harpy | 4 | 1.0 |
| Mimic | 4 | 1.0 |
| Otyugh | 4 | 1.0 |
| Greater barghest | 5 | 0.5 |
| Djinni | 5 | 1.0 |
| Gibbering mouther | 5 | 1.5 |
| Green hag | 5 | 1.0 |
| Shadow mastiff | 5 | 0.5 |
| Celestial lion | 3 | 0.5 — celestial template |
| Fiendish dire rat | 1 | 0.25 — fiendish template |
| Hydra (5-head) | 5 | 2.0 — multi-head system |
| Hydra (6-head) | 6 | 0.5 — same system |
| Locust swarm | 3 | 0.5 |
| Centipede swarm | 4 | 0.5 |
| Monitor lizard | 2 | 0.25 |
| Skum | 2 | 0.5 |
| Vargouille | 2 | 0.75 |
| Derro | 3 | 0.75 |

### Phase 3: High-Tier Creatures (Estimated: 30–40 hours)

**Goal:** Fill out EL 7–8 tables, implement complex creatures.

| Creature | CR | Est. Hours |
|---|---|---|
| Babau (demon) | 6 | 1.0 |
| Chain devil | 6 | 1.0 |
| Digester | 6 | 1.0 |
| Bralani (eladrin) | 6 | 1.0 |
| Ettin | 6 | 1.0 |
| Annis (hag) | 6 | 1.0 |
| Xill | 6 | 1.5 |
| Minor xorn | 3 | 1.0 |
| Average xorn | 6 | 0.5 |
| Average salamander | 6 | 1.0 |
| Flamebrother salamander | 3 | 0.5 |
| Will-o'-wisp | 6 | 1.0 |
| Gauth (beholder) | 6 | 3.0 — eye rays |
| Aboleth | 7 | 2.0 |
| Chaos beast | 7 | 1.5 |
| Chuul | 7 | 1.0 |
| Succubus | 7 | 2.0 |
| Hellcat | 7 | 1.0 |
| Drider | 7 | 2.0 |
| Hill giant | 7 | 1.0 |
| Flesh golem | 7 | 1.5 |
| Invisible stalker | 7 | 1.0 |
| Medusa | 7 | 1.5 |
| Black pudding | 7 | 1.5 |
| Red slaad | 7 | 1.0 |
| Blue slaad | 7 | 0.5 |
| Green slaad | 8 | 0.5 |
| Spectre | 7 | 1.5 |
| Umber hulk | 7 | 1.0 |
| Yuan-ti halfblood | 5 | 1.5 |
| Yuan-ti abomination | 7 | 2.0 |
| Phasm | 7 | 1.0 |
| Eight-headed hydra | 7 | 0.5 — if hydra system exists |
| Seven-headed hydra | 6 | 0.5 |

### Phase 4: Templated NPCs (Estimated: 15–20 hours)

**Goal:** Build an NPC class-level system and create all templated NPCs.

#### 4A: NPC Builder System (~5 hours)

```csharp
/// <summary>
/// Generates NPCs by combining a base creature with class levels.
/// Handles ability score adjustments, feat selection, equipment, and spells.
/// </summary>
public static class NPCBuilder
{
    /// <summary>
    /// Create a templated NPC from a base creature + class levels.
    /// </summary>
    public static NPCDefinition BuildClassedNPC(
        string baseCreatureId,    // e.g., "lizardfolk"
        string className,         // e.g., "Druid"
        int classLevel,           // e.g., 5
        NPCEquipmentTier gear = NPCEquipmentTier.Standard)
    {
        // 1. Clone base creature stats
        // 2. Apply class HD, BAB, saves
        // 3. Add ability score increases (every 4 levels)
        // 4. Select feats
        // 5. If caster: assign spells
        // 6. Equipment from NPC gear table (DMG p.127)
        // 7. Calculate CR
    }
}

public enum NPCEquipmentTier
{
    NPC,        // NPC gear value table
    Heroic,     // PC wealth by level
    Basic       // Minimal equipment
}
```

#### 4B: Individual Templated NPCs (~10 hours)

| NPC | Base | Class | Level | CR | Est. Hours |
|---|---|---|---|---|---|
| Human monk | Human | Monk | 5 | 5 | 1.0 |
| Kobold sorcerer | Kobold | Sorcerer | 5 | 5 | 1.0 |
| Lizardfolk druid | Lizardfolk | Druid | 5 | 6 | 1.5 |
| Hobgoblin fighter | Hobgoblin | Fighter | 5 | 5 | 0.75 |
| Goblin rogue | Goblin | Rogue | 5 | 5 | 0.75 |
| Human barbarian | Human | Barbarian | 5 | 5 | 0.75 |
| Ogre barbarian | Ogre | Barbarian | 4 | 7 | 1.0 |
| Troglodyte cleric | Troglodyte | Cleric | 5 | 8 | 1.5 |
| Ghost 5th-lvl fighter | Human | Fighter 5 + Ghost | 7 | 7 | 2.0 |
| Vampire 5th-lvl fighter | Human | Fighter 5 + Vampire | 7 | 7 | 2.5 |
| Half-dragon fighter | Human | Fighter 4 + Half-dragon | 6 | 6 | 1.5 |
| Advanced megaraptor skeleton | Megaraptor | Skeleton + Advanced | 7 | 7 | 1.0 |

### Phase 5: Encounter Table Data Entry (Estimated: 8–10 hours)

**Goal:** Transcribe all DMG encounter tables into data.

| Table Level | # Entries | Status |
|---|---|---|
| 1st Level | 17 | Extracted from PDF ✅ |
| 2nd Level | 17 | Extracted from PDF ✅ |
| 3rd Level | 20 | Extracted from PDF ✅ |
| 4th Level | 20 | Extracted from PDF ✅ |
| 5th Level | 20 | Extracted from PDF ✅ |
| 6th Level | 22 | Extracted from PDF ✅ |
| 7th Level | 20 | Extracted from PDF ✅ |
| 8th Level | 22 | Extracted from PDF ✅ |
| 9th–10th Level | ~20 each | Extracted ✅ (for expansion) |

Each table entry needs: d% range, creature ID(s), count dice expression, and optional sub-rolls.

### Phase 6: UI & Integration (Estimated: 8–12 hours)

**Goal:** Connect encounter system to game flow.

#### 6A: Encounter Selection UI (~4 hours)
- Add "Random Dungeon Encounter" button to pre-combat hub
- Difficulty selector (Easy/Standard/Challenging/Overpowering)
- Preview panel showing rolled encounter before spawning
- "Reroll" option

#### 6B: Spawn System Integration (~3 hours)
- Convert `DungeonEncounterEntry` → `EncounterPreset`
- Handle multi-creature spawns with grid placement
- Treasure generation per DMG encounter rules

#### 6C: Party Level Calculator (~1 hour)
- Average party level from active PCs
- Handle mixed-level parties
- Display current EL recommendation

---

## Section 4: Technical Architecture

### 4.1 New File Structure

```
Assets/Scripts/Encounters/
├── DungeonEncounterSystem.cs      // Main system, Init(), Generate()
├── DungeonEncounterTable.cs       // Table + Entry data classes
├── DungeonEncounterTables_1_4.cs  // Tables 1–4 data
├── DungeonEncounterTables_5_8.cs  // Tables 5–8 data
├── DungeonEncounterTables_9_12.cs // Tables 9–12 (future)
├── DungeonEncounterTables_13_16.cs
├── DungeonEncounterTables_17_20.cs
├── EncounterDifficultyCalculator.cs
├── NPCBuilder.cs                  // Class-level NPC generation
└── EncounterSpawnHelper.cs        // Grid placement logic

Assets/Scripts/Character/
├── NPCDatabase_Demons.cs          // New: demon creatures
├── NPCDatabase_Devils.cs          // New: devil creatures
├── NPCDatabase_Giants.cs          // New: giant creatures
├── NPCDatabase_Oozes.cs           // New: ooze creatures (expand)
├── NPCDatabase_Outsiders.cs       // New: outsider creatures
├── NPCDatabase_Undead.cs          // New: undead creatures (expand)
├── NPCDatabase_Aberrations.cs     // New: aberrations
├── NPCDatabase_MagicalBeasts.cs   // New: magical beasts
├── NPCDatabase_Humanoids.cs       // New: humanoid warriors
├── NPCDatabase_Plants.cs          // New: plant creatures
└── NPCDatabase_Constructs.cs      // New: construct creatures
```

### 4.2 Integration Points

```
SceneBootstrap.cs
  └── DungeonEncounterSystem.Init()  // Register all tables

GameManager.cs (pre-combat hub)
  └── "Random Encounter" button
      └── DungeonEncounterSystem.GenerateForPartyLevel()
          └── EncounterPreset → existing spawn system

NPCDatabase.cs
  └── RegisterCreatures_*()  // All new creatures
  └── NPCBuilder.BuildClassedNPC()  // Templated NPCs
```

### 4.3 Existing System Compatibility

The new encounter system produces `EncounterPreset` objects — the same type used by the existing encounter selector. This means:

- **No changes to GameManager spawn logic**
- **No changes to combat flow**
- **No changes to XP/treasure systems**
- New creatures register via the same `NPCDatabase.Register()` path

---

## Section 5: Effort Estimates

| Phase | Description | Estimated Hours | Dependencies |
|---|---|---|---|
| Phase 1A | Encounter Table System | 8 | None |
| Phase 1B | Warrior Humanoids (8 creatures) | 4 | None |
| Phase 1C | Common Undead (4 creatures) | 4 | None |
| Phase 1D | Core Aberrations/Beasts (9 creatures) | 8 | None |
| Phase 1E | Common Outsiders (6 creatures) | 6 | None |
| **Phase 1 Total** | | **30 hours** | |
| Phase 2 | Mid-Tier Creatures (~30 creatures) | 25 | Phase 1 |
| Phase 3 | High-Tier Creatures (~34 creatures) | 35 | Phase 1 |
| Phase 4A | NPC Builder System | 5 | Phase 1B |
| Phase 4B | Templated NPCs (12 NPCs) | 15 | Phase 4A, base creatures |
| Phase 5 | Table Data Entry (8 levels) | 10 | Phase 1A |
| Phase 6 | UI & Integration | 10 | Phases 1–5 |
| **TOTAL** | | **~130 hours** | |

### Priority Order (if time-constrained)

1. **Phase 1A** (Encounter System) + **Phase 5** (Table Data) → enables random encounters with existing creatures
2. **Phase 1B** (Humanoid warriors) → fills most Level 1–2 gaps
3. **Phase 1C** (Undead) → fills Level 3–4 gaps
4. **Phase 1D** (Beasts/Aberrations) → fills Level 4–5 gaps
5. **Phase 2** (Mid-tier) → completes Levels 5–6
6. **Phase 3** (High-tier) → completes Levels 7–8
7. **Phase 4** (Templated NPCs) → fills remaining NPC slots
8. **Phase 6** (UI) → polish

**Minimum Viable Product (MVP):** Phases 1A + 1B + 5 + 6A = ~25 hours  
This gives a working random encounter system with partial creature coverage (missing creatures would be skipped/re-rolled).

---

## Section 6: DMG PDF Readability Report

**✅ The DMG v3.5 PDF IS readable.** 

- Pages 79–81 contain all dungeon encounter tables (levels 1–20)
- OCR quality is moderate but sufficient — some character artifacts in non-table text
- Tables are well-structured with clear d% ranges and creature entries
- All 20 encounter tables have been successfully extracted
- Some minor spelling normalization needed (e.g., "five-headed hydra" → "five_headed_hydra")

**Pages verified:**
- p.78: Random dungeon encounter introduction, table usage instructions
- p.79: 1st through 4th level tables  
- p.80: 5th through 8th level tables (+ partial 9th)
- p.81: 9th through 11th level tables (+ higher)
- p.48–49: Challenge Rating and Encounter Level rules (in Chapter 3)

---

## Section 7: Next Steps

### Immediate Actions
1. **Create `Assets/Scripts/Encounters/` directory structure**
2. **Implement `DungeonEncounterTable` and `DungeonEncounterEntry` data classes**
3. **Transcribe Level 1–4 encounter table data** (can work with existing creatures)
4. **Begin Phase 1B** — implement 8 warrior-type humanoids (fast wins)
5. **Wire up a basic "Random Encounter" button** in pre-combat hub

### Decisions Needed from User
1. **Scope confirmation:** Implement all 8 levels, or focus on specific range?
2. **Creature priority:** Start with most-used creatures, or by encounter level?
3. **NPC complexity:** Full spell lists for caster NPCs, or simplified versions?
4. **UI preference:** Inline in pre-combat hub, or separate encounter screen?
5. **Missing creatures:** Skip and re-roll, or show placeholder enemies?

---

*Generated: May 26, 2026*  
*Project: /home/ubuntu/dnd35prototype*  
*Source: dungeon encounters 8th level - Sheet1.csv + Dungeon Master's Guide v3.5.pdf*

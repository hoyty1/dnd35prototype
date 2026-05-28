# Undead Creatures by Intelligence Category

> **Purpose:** Categorize all implemented undead by Intelligence and Wisdom scores to design
> appropriate AI behaviors — mindless undead shamble and attack, while intelligent undead
> use tactics, spells, and special abilities strategically.
>
> **Generated:** 2026-05-28 | **Total Undead:** 32

---

## Summary by Intelligence Tier

| Tier | INT Range | Count | CR Range | AI Profiles Used |
|------|-----------|-------|----------|------------------|
| **Mindless** | INT — (No Score) | 18 | 1/3–4 | UndeadMindless |
| **Low Intelligence** | INT 3–6 | 4 | 3–8 | Humanoid |
| **Average Intelligence** | INT 7–12 | 5 | 3–8 | Grappler, Humanoid, Undead |
| **High Intelligence** | INT 13–18 | 5 | 1–7 | Humanoid |

## Wisdom Score Distribution

| WIS Score | Count | Undead |
|-----------|-------|--------|
| 10 | 19 | Bugbear Zombie, Heavy Warhorse Skeleton, Human Archer Skeleton, Human Commone... |
| 11 | 1 | Allip |
| 12 | 3 | Bodak, Greater Shadow, Shadow |
| 13 | 3 | Vampire Spawn, Wight, Wight Dreadwalker |
| 14 | 6 | Ghast, Ghost, Ghoul, Mummy, Spectre, Wraith |

---

## Mindless (INT — (No Score))

> No intelligence at all. Operates on pure necromantic animation. Cannot think, plan, or adapt. Attacks nearest target, no tactical awareness.

### Zombie Templates

| Name | CR | INT | WIS | HD | AI Profile | Notable Abilities |
|------|----|-----|-----|----|------------|-------------------|
| Human Commoner Zombie | 1/2 | — | 10 | 2 | UndeadMindless | Single Actions Only, DR 5/slashing, Undead Traits |
| Human Warrior Zombie | 1/2 | — | 10 | 2 | UndeadMindless | Single Actions Only, DR 5/slashing, Undead Traits |
| Troglodyte Zombie | 1 | — | 10 | 4 | UndeadMindless | Single Actions Only, DR 5/slashing, Undead Traits |
| Bugbear Zombie | 2 | — | 10 | 6 | UndeadMindless | Single Actions Only, DR 5/slashing, Undead Traits |
| Ogre Zombie | 3 | — | 10 | 8 | UndeadMindless | Single Actions Only, DR 5/slashing, Undead Traits |
| Owlbear Zombie | 3 | — | 10 | 10 | UndeadMindless | Single Actions Only, DR 5/slashing, Undead Traits |
| Minotaur Zombie | 4 | — | 10 | 12 | UndeadMindless | Single Actions Only, DR 5/slashing, Undead Traits |

### Skeleton Templates

| Name | CR | INT | WIS | HD | AI Profile | Notable Abilities |
|------|----|-----|-----|----|------------|-------------------|
| Human Archer Skeleton | 1/3 | — | 10 | 1 | UndeadMindless | DR 5/bludgeoning, Improved Initiative (bonus), Undead Traits |
| Human Warrior Skeleton | 1/3 | — | 10 | 1 | UndeadMindless | DR 5/bludgeoning, Improved Initiative (bonus), Undead Traits |
| Wolf Skeleton | 1 | — | 10 | 2 | UndeadMindless | DR 5/bludgeoning, Improved Initiative (bonus), Undead Traits |
| Heavy Warhorse Skeleton | 2 | — | 10 | 4 | UndeadMindless | DR 5/bludgeoning, Improved Initiative (bonus), Undead Traits |
| Owlbear Skeleton | 2 | — | 10 | 5 | UndeadMindless | DR 5/bludgeoning, Improved Initiative (bonus), Undead Traits |
| Minotaur Skeleton | 3 | — | 10 | 6 | UndeadMindless | DR 5/bludgeoning, Improved Initiative (bonus), Undead Traits |
| Troll Skeleton | 3 | — | 10 | 6 | UndeadMindless | DR 5/bludgeoning, Improved Initiative (bonus), Undead Traits |
| Megaraptor Skeleton | 4 | — | 10 | 8 | UndeadMindless | DR 5/bludgeoning, Improved Initiative (bonus), Undead Traits |

### Standard Undead

| Name | CR | INT | WIS | AI Profile | AI Behavior | Notable Abilities |
|------|----|-----|-----|------------|-------------|-------------------|
| Skeleton Archer | — | — | 10 | UndeadMindless | RangedKiter | — |
| Skeleton Warrior | — | — | 10 | UndeadMindless | DefensiveMelee | — |
| Zombie | — | — | 10 | UndeadMindless | — | — |

#### Recommended AI Behavior for Mindless Undead

- **Target Selection:** Attack nearest enemy; no prioritization
- **Movement:** Move directly toward closest target (no flanking, no tactical positioning)
- **Special Abilities:** Used automatically (e.g., slam attack) — no decision-making
- **Retreat:** Never retreats — fights until destroyed
- **Group Tactics:** None — each creature acts independently
- **Zombies:** Single standard action per round (move OR attack, not both)
- **Skeletons:** Can take full actions; benefit from Improved Initiative

## Low Intelligence (INT 3–6)

> Simple tactics. Can follow basic orders, use simple weapons, and recognize obvious threats. Limited problem-solving.

| Name | CR | INT | WIS | AI Profile | AI Behavior | Notable Abilities |
|------|----|-----|-----|------------|-------------|-------------------|
| Shadow | 3 | 6 | 12 | Humanoid | — | Incorporeal, Turn Resistance +2 |
| Mummy | 5 | 6 | 14 | Humanoid | — | Disease, Despair, Mummy Rot |
| Bodak | 8 | 6 | 12 | Humanoid | — | Death Gaze |
| Greater Shadow | 8 | 6 | 12 | Humanoid | — | Incorporeal, Create Spawn, Turn Resistance +2 |

#### Recommended AI Behavior for Low Intelligence Undead

- **Target Selection:** Prefer wounded or isolated targets
- **Movement:** Basic tactical positioning; may circle around obstacles
- **Special Abilities:** Used with basic awareness (e.g., Death Gaze against visible targets)
- **Retreat:** May withdraw if severely damaged (< 25% HP)
- **Group Tactics:** Basic swarming — multiple creatures focus same target

## Average Intelligence (INT 7–12)

> Human-like reasoning. Can use tactics, set ambushes, coordinate with allies, and make situational decisions.

| Name | CR | INT | WIS | AI Profile | AI Behavior | Notable Abilities |
|------|----|-----|-----|------------|-------------|-------------------|
| Allip | 3 | 11 | 11 | Humanoid | — | Incorporeal |
| Wight | 3 | 11 | 13 | Undead | — | Energy Drain, Create Spawn |
| Ghost | 7 | 12 | 14 | Humanoid | — | Incorporeal, Energy Drain, Manifestation, Frightful Moan, Corrupting Touch, Rejuvenation, Turn Resistance +4 |
| Mohrg | 8 | 11 | 10 | Grappler | — | Improved Grab, Paralysis, Create Spawn |
| Wight Dreadwalker | — | 11 | 13 | Humanoid | — | Energy Drain, Create Spawn |

#### Recommended AI Behavior for Average Intelligence Undead

- **Target Selection:** Prioritize spellcasters and weakened enemies
- **Movement:** Seek flanking positions; avoid AoOs when possible
- **Special Abilities:** Tactical use — Energy Drain on high-value targets, Create Spawn on isolated enemies
- **Retreat:** Will retreat to reposition or lure enemies into traps
- **Group Tactics:** Coordinate attacks; one grapples while others attack

## High Intelligence (INT 13–18)

> Very smart. Uses advanced tactics, leverages special abilities strategically, and can plan multi-step approaches.

| Name | CR | INT | WIS | AI Profile | AI Behavior | Notable Abilities |
|------|----|-----|-----|------------|-------------|-------------------|
| Ghoul | 1 | 13 | 14 | Humanoid | — | Paralysis, Disease, Turn Resistance +2 |
| Ghast | 3 | 13 | 14 | Humanoid | — | Paralysis, Disease, Stench, Turn Resistance +2 |
| Vampire Spawn | 4 | 13 | 13 | Humanoid | — | Energy Drain, Dominate, Blood Drain, Turn Resistance +2 |
| Wraith | 5 | 14 | 14 | Humanoid | — | Incorporeal, Energy Drain, Create Spawn |
| Spectre | 7 | 14 | 14 | Humanoid | — | Incorporeal, Energy Drain, Create Spawn, Turn Resistance +2 |

#### Recommended AI Behavior for High Intelligence Undead

- **Target Selection:** Analyze party composition; prioritize healers, then casters, then martial
- **Movement:** Sophisticated positioning; use incorporeal movement through walls
- **Special Abilities:** Strategic use — Energy Drain to weaken before killing, Dominate on key targets
- **Retreat:** Will retreat strategically; use hit-and-run tactics
- **Group Tactics:** Coordinate with other undead; use minions as shields

---

## Current AI Profile Distribution

| AI Profile | Count | Undead Using It | Appropriate? |
|------------|-------|-----------------|--------------|
| Grappler | 1 | Mohrg | ✅ Correct for Mohrg (Improved Grab) |
| Humanoid | 12 | Allip, Bodak, Ghast, Ghost, Ghoul, Greater Shadow, Mummy,... | ⚠️ Should use Undead-specific profiles |
| Undead | 1 | Wight | ✅ Generic undead profile |
| UndeadMindless | 18 | Bugbear Zombie, Heavy Warhorse Skeleton, Human Archer Ske... | ✅ Correct for mindless |

## Special Abilities Cross-Reference

| Ability | Undead With It | AI Implications |
|---------|----------------|-----------------|
| Blood Drain | Vampire Spawn | Grapple then drain — prefer isolated targets |
| Corrupting Touch | Ghost | Prioritize physically strong targets (targets Fort save) |
| Create Spawn | Greater Shadow, Mohrg, Spectre, Wight, Wight Dr... | Kill priority matters — new spawn created from slain enemies |
| DR 5/bludgeoning | Heavy Warhorse Skeleton, Human Archer Skeleton,... | — |
| DR 5/slashing | Bugbear Zombie, Human Commoner Zombie, Human Wa... | — |
| Death Gaze | Bodak | Should position to affect maximum enemies each round |
| Despair | Mummy | Aura effect — position near clusters of enemies |
| Disease | Ghast, Ghoul, Mummy | Long-term threat; AI should prioritize contact attacks |
| Dominate | Vampire Spawn | Target party leader or strongest fighter; maintain concentration |
| Energy Drain | Ghost, Spectre, Vampire Spawn, Wight, Wight Dre... | Should prioritize melee contact; targets with most levels = highest... |
| Frightful Moan | Ghost | AoE fear — use before engaging to weaken party |
| Improved Grab | Mohrg | Should grapple then use tongue paralysis attack |
| Improved Initiative | Heavy Warhorse Skeleton, Human Archer Skeleton,... | No AI impact (passive bonus) |
| Incorporeal | Allip, Ghost, Greater Shadow, Shadow, Spectre, ... | Must understand magic weapon requirement; can move through walls fo... |
| Manifestation | Ghost | Toggle ethereal/material — use tactically to avoid damage |
| Mummy Rot | Mummy | Prioritize melee contact; disease stacks with Despair |
| Paralysis | Ghast, Ghoul, Mohrg | Should cycle between targets to paralyze multiple enemies |
| Rejuvenation | Ghost | — |
| Single Actions Only | Bugbear Zombie, Human Commoner Zombie, Human Wa... | — |
| Stench | Ghast | Aura effect — stay close to enemies for sickened condition |
| Turn Resistance | Ghast, Ghost, Ghoul, Greater Shadow, Shadow, Sp... | No AI impact (passive defense) |
| Undead Traits | Bugbear Zombie, Heavy Warhorse Skeleton, Human ... | Immune to mind-affecting, poison, disease, crits, sneak attacks |

---

## Recommended AI Profile Assignments

Based on this analysis, the following AI profile assignments are recommended:

| Undead | Current Profile | Recommended Profile | Reason |
|--------|----------------|--------------------:|--------|
| All Zombies | UndeadMindless | ✅ UndeadMindless | Correct — mindless, single action only |
| All Skeletons | UndeadMindless | ✅ UndeadMindless | Correct — mindless but full actions |
| Shadow | Humanoid | 🔄 UndeadIncorporeal | Needs incorporeal movement AI + Strength damage targeting |
| Greater Shadow | Humanoid | 🔄 UndeadIncorporeal | Needs incorporeal movement AI + Create Spawn awareness |
| Allip | Humanoid | 🔄 UndeadIncorporeal | Incorporeal; babble ability needs AoE positioning |
| Wraith | Humanoid | 🔄 UndeadIncorporeal | Incorporeal + Energy Drain + Create Spawn |
| Spectre | Humanoid | 🔄 UndeadIncorporeal | Incorporeal + Energy Drain; high INT needs tactics |
| Ghost | Humanoid | 🔄 UndeadIncorporeal | Incorporeal with multiple special attacks; needs complex AI |
| Ghoul | Humanoid | 🔄 UndeadTactical | High INT; should cycle paralysis between targets |
| Ghast | Humanoid | 🔄 UndeadTactical | High INT; paralysis + stench aura positioning |
| Wight | Undead | 🔄 UndeadTactical | Average INT; Energy Drain + Create Spawn tactics |
| Wight Dreadwalker | Humanoid | 🔄 UndeadTactical | Enhanced wight; needs coordinated Energy Drain |
| Vampire Spawn | Humanoid | 🔄 UndeadTactical | High INT; Dominate + Blood Drain combo |
| Mummy | Humanoid | 🔄 UndeadBrute | Low INT but tough; Despair aura + Mummy Rot |
| Bodak | Humanoid | 🔄 UndeadBrute | Low INT; Death Gaze needs facing/positioning |
| Mohrg | Grappler | ✅ Grappler | Correct — Improved Grab into tongue paralysis |

## New AI Profiles Needed

### UndeadMindless (Exists)
- Already implemented for zombies and skeletons
- Attack nearest target, no tactics, no retreat
- Zombies: single action restriction

### UndeadIncorporeal (New)
- For: Shadow, Greater Shadow, Allip, Wraith, Spectre, Ghost
- Key behaviors:
  - Move through walls to reach isolated targets
  - Retreat into solid objects when damaged
  - Prefer targets vulnerable to touch attacks
  - Use Create Spawn awareness (avoid overkilling when spawn would be useful)
  - Strength Damage users: target STR-dependent characters
  - Energy Drain users: prioritize high-level targets

### UndeadTactical (New)
- For: Ghoul, Ghast, Wight, Wight Dreadwalker, Vampire Spawn
- Key behaviors:
  - Cycle paralysis attacks between multiple targets
  - Ghast: position for stench aura to affect multiple enemies
  - Energy Drain users: focus drain on key party members
  - Vampire Spawn: attempt Dominate before melee engagement
  - Coordinate with pack — one paralyzes, others attack paralyzed targets
  - Coup de grâce paralyzed/helpless enemies

### UndeadBrute (New)
- For: Mummy, Bodak
- Key behaviors:
  - Mummy: advance into melee, let Despair aura trigger, then slam for Mummy Rot
  - Bodak: position for Death Gaze to hit multiple targets per round
  - Low INT limits tactical complexity — straightforward but effective
  - Do not retreat; rely on DR and turn resistance for survival

---

## Source Files

| File | Undead Count | Notes |
|------|-------------|-------|
| `NPCDatabaseCustom.cs` | 4 | Custom variants (Skeleton Warrior/Archer, Zombie, Wight Dreadwalker) |
| `NPCDatabase_A.cs` | 1 | Allip |
| `NPCDatabase_B.cs` | 1 | Bodak |
| `NPCDatabase_G.cs` | 4 | Ghoul, Ghast, Ghost, Greater Shadow |
| `NPCDatabase_M.cs` | 2 | Mohrg, Mummy |
| `NPCDatabase_S.cs` | 2 | Shadow, Spectre |
| `NPCDatabase_V.cs` | 1 | Vampire Spawn |
| `NPCDatabase_W.cs` | 2 | Wight, Wraith |
| `SkeletonTemplate.cs` | 8 | Factory-generated skeleton variants (8 types) |
| `ZombieTemplate.cs` | 7 | Factory-generated zombie variants (7 types) |

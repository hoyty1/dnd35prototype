# Monster Inventory by Creature Type

## D&D 3.5e Unity Prototype — Complete Creature Registry

**Date:** May 28, 2026
**Total Creatures:** 335
**Creature Types:** 15
**Source:** NPCDatabase files + DragonData.cs

---

### Summary by Type

| Type | Count | CR Range | AI Profiles Used |
|------|-------|----------|-----------------|
| **Aberration** | 23 | 2–13 | Default |
| **Animal** | 49 | 0.16666666666666666–999 | Default |
| **Construct** | 1 | 7 | Default |
| **Dragon** | 60 | 2–17 | Dragon |
| **Elemental** | 9 | 1–3 | Default |
| **Fey** | 1 | 2 | Default |
| **Giant** | 10 | 3–999 | Default |
| **Humanoid** | 57 | 1/4–999 | Default |
| **Magical Beast** | 30 | 1/3–12 | Default |
| **Monstrous Humanoid** | 12 | 1–7 | Default |
| **Ooze** | 4 | 3–7 | Default |
| **Outsider** | 43 | 1/2–10 | Default |
| **Plant** | 4 | 1–8 | Default |
| **Undead** | 17 | 1–999 | Default |
| **Vermin** | 15 | 1/3–999 | Default |

---

### Aberration (23 creatures)

> Bizarre creatures with alien anatomies, strange abilities, and often aberrant minds. Usually have darkvision 60 ft.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Choker | 2 | Default | Improved Grab |
| Skum | 2 | Default | Rake |
| Ethereal Filcher | 3 | Default | — |
| Ettercap | 3 | Default | — |
| Grick | 3 | Default | DR 10 |
| Rust Monster | 3 | Default | — |
| Carrion Crawler | 4 | Default | — |
| Mimic | 4 | Default | Improved Grab |
| Otyugh | 4 | Default | Improved Grab |
| Cloaker | 5 | Default | — |
| Gibbering Mouther | 5 | Default | Improved Grab, DR 5, Aura |
| Gauth | 6 | Default | — |
| Will-o'-Wisp | 6 | Default | — |
| Aboleth | 7 | Default | — |
| Chuul | 7 | Default | Improved Grab |
| Drider | 7 | Default | SR 17 |
| Phasm | 7 | Default | — |
| Umber Hulk | 7 | Default | Aura |
| Dark Naga | 8 | Default | — |
| Destrachan | 8 | Default | — |
| Mind Flayer | 8 | Default | Improved Grab, SR 25 |
| Spirit Naga | 9 | Default | — |
| Beholder | 13 | Default | SR 28 |

---

### Animal (49 creatures)

> Nonmagical, naturally occurring creatures. Low Intelligence (1-2), no magical abilities, and natural weapons only.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Monkey | 1/6 | Default | — |
| Raven | 1/6 | Default | — |
| Dire Rat | 1/3 | Default | — |
| Dog | 1/3 | Default | — |
| Giant Rat | 1/3 | Default | — |
| Hawk | 1/3 | Default | — |
| Owl | 1/3 | Default | — |
| Viper (Tiny) | 1/3 | Default | — |
| Badger | 1/2 | Default | — |
| Eagle | 1/2 | Default | — |
| Viper (Small) | 1/2 | Default | — |
| Hyena | 1 | Default | Trip |
| Riding Dog | 1 | Default | — |
| Viper (Medium) | 1 | Default | — |
| Ape | 2 | Default | — |
| Bat Swarm | 2 | Default | — |
| Bison | 2 | Default | — |
| Black Bear | 2 | Default | — |
| Boar | 2 | Default | — |
| Cheetah | 2 | Default | Trip |
| Constrictor Snake | 2 | Default | — |
| Crocodile | 2 | Default | — |
| Dire Badger | 2 | Default | — |
| Dire Bat | 2 | Default | — |
| Dire Weasel | 2 | Default | — |
| Large Shark | 2 | Default | — |
| Large Viper | 2 | Default | — |
| Leopard | 2 | Default | Improved Grab, Pounce, Rake |
| Monitor Lizard | 2 | Default | — |
| Rat Swarm | 2 | Default | — |
| Viper (Large) | 2 | Default | — |
| Wolverine | 2 | Default | — |
| Deinonychus | 3 | Default | — |
| Dire Ape | 3 | Default | — |
| Dire Wolf | 3 | Default | — |
| Lion | 3 | Default | — |
| Viper (Huge) | 3 | Default | — |
| Dire Boar | 4 | Default | — |
| Dire Wolverine | 4 | Default | — |
| Giant Crocodile | 4 | Default | Improved Grab |
| Dire Lion | 5 | Default | — |
| Giant Constrictor Snake | 5 | Default | Improved Grab |
| Brown Bear | ? | Default | — |
| Dire Bear | ? | Default | — |
| Dire Tiger | ? | Default | — |
| Octopus | ? | Default | — |
| Small Viper | ? | Default | — |
| Tiger | ? | Default | — |
| Wolf | ? | Default | — |

---

### Construct (1 creatures)

> Animated objects or artificially created creatures. Immune to mind-affecting effects, poison, disease, and many conditions.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Flesh Golem | 7 | Default | DR 5, Mindless |

---

### Dragon (60 creatures)

> Reptilian creatures with powerful innate magic, breath weapons, and frightful presence. All true dragons cast as sorcerers.

*10 true dragon types × 6 age categories (Wyrmling through Adult) = 60 variants.*
*All dragons use the **Dragon** AI profile with tactical breath weapon positioning and sorcerer spellcasting.*

#### Chromatic Dragons (Evil)

| Dragon Type | CR Range | Breath Weapon | Spellcasting | Notable Abilities |
|-------------|----------|---------------|-------------|-------------------|
| **Red** | 4–15 | Fire Cone | CL 1/3/5/7 | Frightful Presence |
| **Blue** | 3–16 | Lightning Line | CL 1/3/5 | Frightful Presence |
| **Green** | 3–15 | Acid Cone | CL 1/3/5 | Frightful Presence |
| **Black** | 2–12 | Acid Line | CL 1/3 | Frightful Presence |
| **White** | 2–11 | Cold Cone | CL 1 | Frightful Presence |

#### Metallic Dragons (Good)

| Dragon Type | CR Range | Breath Weapon | Secondary Breath | Spellcasting | Notable Abilities |
|-------------|----------|---------------|-----------------|-------------|-------------------|
| **Gold** | 5–17 | Fire Cone | Weakening Gas | CL 1/3/5/7 | Frightful Presence |
| **Silver** | 4–15 | Cold Cone | Paralysis Gas | CL 1/3/5/7 | Frightful Presence |
| **Bronze** | 3–15 | Lightning Line | Repulsion Gas | CL 1/3/5/7 | Frightful Presence |
| **Copper** | 2–13 | Acid Line | Slow Gas | CL 1/3/5/7 | Frightful Presence |
| **Brass** | 2–11 | Fire Line | Sleep Gas | CL 1/3/5/7 | Frightful Presence |

---

### Elemental (9 creatures)

> Beings composed of one of the four classical elements (air, earth, fire, water). Immune to poison, sleep, paralysis, and stunning.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Small Air Elemental | 1 | Default | — |
| Small Earth Elemental | 1 | Default | — |
| Small Fire Elemental | 1 | Default | — |
| Small Water Elemental | 1 | Default | — |
| Thoqqua | 2 | Default | — |
| Medium Air Elemental | 3 | Default | — |
| Medium Earth Elemental | 3 | Default | — |
| Medium Fire Elemental | 3 | Default | — |
| Medium Water Elemental | 3 | Default | — |

---

### Fey (1 creatures)

> Supernatural creatures closely tied to nature. Low-light vision, often with spell-like abilities.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Satyr | 2 | Default | DR 5 |

---

### Giant (10 creatures)

> Humanoid-shaped creatures of great size and strength. Generally low Reflex saves but high Fortitude.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Ogre | 3 | Default | — |
| Troll | 5 | Default | — |
| Ettin | 6 | Default | — |
| Hill Giant | 7 | Default | — |
| Ogre Mage | 8 | Default | Regen 5, SR 19 |
| Stone Giant | 8 | Default | — |
| Frost Giant | 9 | Default | — |
| Fire Giant | 10 | Default | — |
| Gruumsh Bonecrusher | ? | Default | — |
| Ogre Brute | ? | Default | — |

---

### Humanoid (57 creatures)

> Bipedal creatures with language and culture. Proficient with weapons and armor. The most common creature type.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Kobold Warrior | 1/4 | Default | — |
| Goblin | 1/3 | Default | — |
| Goblin Warrior | 1/3 | Default | — |
| Mirror Test Goblin Archer | 1/3 | Default | — |
| Dwarf Warrior | 1/2 | Default | — |
| Elf Warrior | 1/2 | Default | — |
| Half-Orc Warrior | 1/2 | Default | — |
| Halfling Warrior | 1/2 | Default | — |
| Hobgoblin | 1/2 | Default | — |
| Hobgoblin Warrior | 1/2 | Default | — |
| Human Commoner | 1/2 | Default | — |
| Human Warrior | 1/2 | Default | — |
| Orc Warrior | 1/2 | Default | — |
| Drow | 1 | Default | SR 12 |
| Duergar | 1 | Default | — |
| Gnoll | 1 | Default | — |
| Lizardfolk | 1 | Default | — |
| Svirfneblin | 1 | Default | SR 12 |
| Troglodyte | 1 | Default | — |
| Bugbear | 2 | Default | — |
| Human Monk | 3 | Default | — |
| Human Paladin | 3 | Default | — |
| Human Monk | 5 | Default | — |
| Human Paladin | 5 | Default | — |
| Human Monk | 7 | Default | — |
| Human Paladin | 7 | Default | — |
| XP Piñata Goblin | 15 | Default | — |
| Aelindra Swiftarrow | ? | Default | — |
| Arcane Missile Adept | ? | Default | — |
| Arcane Target Dummy | ? | Default | — |
| Borlin Ironbolt | ? | Default | — |
| Brawler Grog | ? | Default | — |
| Brutus the Grappler | ? | Default | — |
| Elara Keeneye | ? | Default | — |
| Evil Acolyte | ? | Default | — |
| Finn Lightfoot | ? | Default | — |
| Garrick Strongbow | ? | Default | — |
| Goblin Ravager | ? | Default | — |
| Goblin Warchief | ? | Default | — |
| Goblin Warrior | ? | Default | — |
| Hobgoblin Sergeant | ? | Default | — |
| Human Cleric | ? | Default | — |
| Human Paladin | ? | Default | — |
| Kira Windrunner | ? | Default | — |
| Marcus Longshot | ? | Default | — |
| Misty Veilweaver | ? | Default | — |
| Neutral Bandit | ? | Default | — |
| Neutral Mage | ? | Default | — |
| Orc Berserker | ? | Default | — |
| Orc Grapple Drill | ? | Default | — |
| Pip Quickfingers | ? | Default | — |
| Roland Ironheart | ? | Default | — |
| Throk the Mighty | ? | Default | — |
| Thug Crusher | ? | Default | — |
| Vhalzor the Corrupter | ? | Default | — |
| Weak Grappler | ? | Default | — |
| Zephyr Windcaller | ? | Default | — |

---

### Magical Beast (30 creatures)

> Similar to animals but with Intelligence 3+ and/or supernatural or extraordinary abilities. Darkvision 60 ft and low-light vision.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Fiendish Dire Rat | 1/3 | Default | — |
| Stirge | 1/2 | Default | Blood Drain |
| Darkmantle | 1 | Default | Improved Grab |
| Krenshar | 1 | Default | Aura |
| Hippogriff | 2 | Default | — |
| Shocker Lizard | 2 | Default | — |
| Worg | 2 | Default | Trip |
| Ankheg | 3 | Default | Improved Grab |
| Cockatrice | 3 | Default | — |
| Ethereal Marauder | 3 | Default | — |
| Giant Eagle | 3 | Default | — |
| Giant Owl | 3 | Default | — |
| Unicorn | 3 | Default | — |
| Celestial Lion | 4 | Default | DR 5 |
| Displacer Beast | 4 | Default | — |
| Five-Headed Hydra | 4 | Default | — |
| Owlbear | 4 | Default | Improved Grab |
| Basilisk | 5 | Default | — |
| Manticore | 5 | Default | — |
| Phase Spider | 5 | Default | — |
| Digester | 6 | Default | Breath Weapon |
| Girallon | 6 | Default | — |
| Seven-Headed Hydra | 6 | Default | — |
| Bulette | 7 | Default | — |
| Chimera | 7 | Default | Breath Weapon |
| Behir | 8 | Default | Improved Grab, Rake |
| Gorgon | 8 | Default | Breath Weapon |
| Hellwasp Swarm | 8 | Default | DR 10, Swarm |
| Nine-Headed Hydra | 8 | Default | — |
| Purple Worm | 12 | Default | Improved Grab |

---

### Monstrous Humanoid (12 creatures)

> Humanoid-shaped creatures with monstrous features. Darkvision 60 ft. Often have natural weapons and special attacks.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Grimlock | 1 | Default | — |
| Derro | 3 | Default | SR 15 |
| Doppelganger | 3 | Default | — |
| Yuan-ti Pureblood | 3 | Default | SR 14 |
| Gargoyle | 4 | Default | — |
| Harpy | 4 | Default | Aura |
| Minotaur | 4 | Default | — |
| Green Hag | 5 | Default | SR 18 |
| Yuan-ti Halfblood | 5 | Default | SR 16 |
| Annis | 6 | Default | Improved Grab, DR 2, SR 17 |
| Medusa | 7 | Default | Aura |
| Yuan-ti Abomination | 7 | Default | Improved Grab, SR 18 |

---

### Ooze (4 creatures)

> Amorphous or mutable creatures. Mindless (no Int score), immune to poison, sleep, paralysis, polymorph, and stunning. Blind but blindsight.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Gelatinous Cube | 3 | Default | — |
| Gray Ooze | 4 | Default | Mindless |
| Ochre Jelly | 5 | Default | — |
| Black Pudding | 7 | Default | Improved Grab, Mindless |

---

### Outsider (43 creatures)

> Beings from other planes of existence. Cannot be raised or resurrected. Darkvision 60 ft. Proficient with all simple and martial weapons.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Formian Worker | 1/2 | Default | — |
| Lemure | 1 | Default | — |
| Dretch | 2 | Default | — |
| Dretch | 2 | Default | DR 5 |
| Imp | 2 | Default | DR 5 |
| Lantern Archon | 2 | Default | — |
| Quasit | 2 | Default | DR 5 |
| Vargouille | 2 | Default | Aura |
| Arrowhawk, Juvenile | 3 | Default | — |
| Claw | 3 | Default | — |
| Flamebrother Salamander | 3 | Default | — |
| Hell Hound | 3 | Default | — |
| Howler | 3 | Default | — |
| Minor Xorn | 3 | Default | DR 5 |
| Yeth Hound | 3 | Default | — |
| Barghest | 4 | Default | DR 5 |
| Hound Archon | 4 | Default | DR 10, SR 16 |
| Janni | 4 | Default | — |
| Bearded Devil | 5 | Default | DR 5, SR 17 |
| Djinni | 5 | Default | — |
| Nightmare | 5 | Default | — |
| Noble Djinni | 5 | Default | — |
| Shadow Mastiff | 5 | Default | Trip, Aura |
| Average Salamander | 6 | Default | — |
| Average Xorn | 6 | Default | DR 5 |
| Babau | 6 | Default | DR 10, SR 14 |
| Bralani | 6 | Default | DR 10, SR 17 |
| Chain Devil | 6 | Default | Regen 2, DR 5, SR 18 |
| Xill | 6 | Default | Improved Grab, SR 21 |
| Chaos Beast | 7 | Default | DR 10, SR 15 |
| Formian Taskmaster | 7 | Default | — |
| Greater Barghest | 7 | Default | DR 10 |
| Hellcat | 7 | Default | Improved Grab, Pounce, Rake, DR 5 |
| Invisible Stalker | 7 | Default | — |
| Red Slaad | 7 | Default | — |
| Succubus | 7 | Default | DR 10, SR 18 |
| Blue Slaad | 8 | Default | DR 5 |
| Efreeti | 8 | Default | — |
| Elder Xorn | 8 | Default | DR 5 |
| Erinyes | 8 | Default | DR 5, SR 20 |
| Green Slaad | 9 | Default | DR 10, SR 22 |
| Couatl | 10 | Default | — |
| Rakshasa | 10 | Default | DR 15, SR 27 |

---

### Plant (4 creatures)

> Vegetable creatures. Immune to mind-affecting effects, poison, sleep, paralysis, polymorph, and stunning. Low-light vision.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Shrieker | 1 | Default | Mindless |
| Phantom Fungus | 3 | Default | — |
| Violet Fungus | 3 | Default | Mindless |
| Treant | 8 | Default | — |

---

### Undead (17 creatures)

> Once-living creatures animated by spiritual or supernatural forces. Immune to mind-affecting, poison, sleep, paralysis, stunning, disease, and death effects. No Constitution score.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Ghoul | 1 | Default | — |
| Allip | 3 | Default | — |
| Ghast | 3 | Default | Stench DC 15 |
| Shadow | 3 | Default | — |
| Wight | 3 | Default | — |
| Vampire Spawn | 4 | Default | DR 5 |
| Mummy | 5 | Default | DR 5, Aura |
| Wraith | 5 | Default | — |
| Ghost | 7 | Default | — |
| Spectre | 7 | Default | Incorporeal |
| Bodak | 8 | Default | DR 10 |
| Greater Shadow | 8 | Default | Incorporeal |
| Mohrg | 8 | Default | — |
| Skeleton Archer | ? | Default | — |
| Skeleton Warrior | ? | Default | — |
| Wight Dreadwalker | ? | Default | — |
| Zombie | ? | Default | — |

---

### Vermin (15 creatures)

> Insects, arachnids, and similar invertebrates. Mindless, darkvision 60 ft. No Intelligence score.

| Name | CR | AI Profile | Notable Abilities |
|------|-----|-----------|-------------------|
| Giant Fire Beetle | 1/3 | Default | — |
| Giant Centipede | 1/2 | Default | — |
| Giant Bee | 1 | Default | — |
| Giant Worker Ant | 1 | Default | Improved Grab, Mindless |
| Spider Swarm | 1 | Default | — |
| Giant Bombardier Beetle | 2 | Default | — |
| Huge Monstrous Centipede | 2 | Default | — |
| Giant Praying Mantis | 3 | Default | — |
| Giant Wasp | 3 | Default | — |
| Locust Swarm | 3 | Default | Swarm, Mindless |
| Centipede Swarm | 4 | Default | Swarm, Mindless |
| Giant Stag Beetle | 4 | Default | Mindless |
| Monstrous Centipede (Medium) | ? | Default | — |
| Monstrous Scorpion (Small) | ? | Default | — |
| Monstrous Spider (Small) | ? | Default | — |

---

### AI Profile Distribution

| AI Profile | Count | Description |
|-----------|-------|-------------|
| **Default** | 275 | Standard melee AI |
| **Dragon** | 60 | Tactical breath weapon positioning + spell priority |

---

### Special Abilities Overview

| Ability | Count | Creature Types |
|---------|-------|---------------|
| Breath Weapon | 63 | Dragon, Magical Beast |
| DR | 50 | Aberration, Construct, Dragon, Fey, Magical Beast, Monstrous Humanoid, Outsider, Undead |
| SR | 43 | Aberration, Dragon, Giant, Humanoid, Monstrous Humanoid, Outsider |
| Sorcerer CL | 33 | Dragon |
| Improved Grab | 20 | Aberration, Animal, Magical Beast, Monstrous Humanoid, Ooze, Outsider, Vermin |
| Frightful Presence | 20 | Dragon |
| Immune Fire | 18 | Dragon |
| Immune Acid | 18 | Dragon |
| Immune Lightning | 12 | Dragon |
| Immune Cold | 12 | Dragon |
| Mindless | 9 | Construct, Ooze, Plant, Vermin |
| Aura | 8 | Aberration, Magical Beast, Monstrous Humanoid, Outsider, Undead |
| Rake | 4 | Aberration, Animal, Magical Beast, Outsider |
| Trip | 4 | Animal, Magical Beast, Outsider |
| Swarm | 3 | Magical Beast, Vermin |
| Regen | 2 | Giant, Outsider |
| Incorporeal | 2 | Undead |
| Pounce | 2 | Animal, Outsider |
| Weakening Gas | 2 | Dragon |
| Paralysis Gas | 2 | Dragon |
| Repulsion Gas | 2 | Dragon |
| Slow Gas | 2 | Dragon |
| Sleep Gas | 2 | Dragon |

---

### Source Files

| File | Creatures |
|------|-----------|
| `NPCDatabaseCustom.cs` | 44 |
| `NPCDatabase_A.cs` | 7 |
| `NPCDatabase_B.cs` | 20 |
| `NPCDatabase_C.cs` | 14 |
| `NPCDatabase_D.cs` | 24 |
| `NPCDatabase_Dragons.cs` | 60 |
| `NPCDatabase_E.cs` | 17 |
| `NPCDatabase_F.cs` | 8 |
| `NPCDatabase_G.cs` | 28 |
| `NPCDatabase_H.cs` | 20 |
| `NPCDatabase_I.cs` | 2 |
| `NPCDatabase_J.cs` | 1 |
| `NPCDatabase_K.cs` | 2 |
| `NPCDatabase_L.cs` | 9 |
| `NPCDatabase_M.cs` | 16 |
| `NPCDatabase_N.cs` | 2 |
| `NPCDatabase_O.cs` | 8 |
| `NPCDatabase_P.cs` | 7 |
| `NPCDatabase_Q.cs` | 1 |
| `NPCDatabase_R.cs` | 5 |
| `NPCDatabase_S.cs` | 13 |
| `NPCDatabase_T.cs` | 4 |
| `NPCDatabase_U.cs` | 2 |
| `NPCDatabase_V.cs` | 8 |
| `NPCDatabase_W.cs` | 5 |
| `NPCDatabase_X.cs` | 4 |
| `NPCDatabase_Y.cs` | 4 |

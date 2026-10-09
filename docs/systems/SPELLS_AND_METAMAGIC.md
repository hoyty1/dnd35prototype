> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# Spells and metamagic: content and status

This is the content-and-status reference for spells: what is in the spell database, which classes can reach it, how IDs, ranges and metamagic behave, how far each casting class and each cleric domain gets, and what is missing compared with the PHB. How the code is built (data model, cast pipelines, handler chains, durations, areas) is in [architecture/spells.md](../architecture/spells.md); bugs are in [issues/SPL.md](../issues/SPL.md) and cited here by ID.

Code paths are under `Assets/Scripts/`. Counts were computed at the commit above by parsing every `Register(new SpellData { ... })` block in `Spell/Database/SpellDatabase_*.cs`, then applying the same rules as the runtime: `SpellData.EnsureAvailabilityFromLegacyClassList` (a `ClassList` entry becomes an `AvailableFor` entry at `SpellLevel`, and Wizard and Sorcerer are mirrored) and `RegisterClassSpellAlias` (adds a class entry to the canonical spell). Unless a table says otherwise, domain availability, which `AnnotateDomainAvailabilityFromDomainDatabase` adds at runtime, is excluded. Nothing in this doc was verified in Play mode. A quick re-check: `grep -c "^\s*Register(" Assets/Scripts/Spell/Database/SpellDatabase_*.cs` should sum to 292.

## 1. Spell inventory

### Totals

292 registered spells in 23 files (`SpellDatabase_A.cs` to `SpellDatabase_Z.cs`, no Q/X/Y). 47 have `IsPlaceholder = true` and 245 do not. The `SpellDatabase.cs` header comment ("~140 spells", levels 0-2) is stale (SPL-083).

| `SpellLevel` | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| Spells | 27 | 66 | 82 | 44 | 43 | 12 | 9 | 4 | 4 | 1 |
| Placeholders | 7 | 16 | 22 | 1 | 1 | 0 | 0 | 0 | 0 | 0 |

### By class and level

General class lists (`AvailableFor` entries without a domain), counted at the per-class level. "Reachable" is the highest spell level the class can actually prepare or cast today (section 4).

| Class | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | Spells | Placeholders | Reachable |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Wizard | 19 | 41 | 52 | 29 | 26 | 9 | 8 | 2 | 2 | 1 | 189 | 33 | 2 |
| Sorcerer | 19 | 41 | 52 | 29 | 26 | 9 | 8 | 2 | 2 | 1 | 189 | 33 | 9 (PHB table) |
| Cleric | 12 | 33 | 37 | 27 | 22 | 3 | 1 | 3 | 2 | 0 | 140 | 24 | 2 (slots to 5) |
| Druid | 9 | 7 | 12 | 9 | 4 | 5 | 1 | 0 | 2 | 0 | 48 | 5 | 2 |
| Bard | 13 | 11 | 25 | 12 | 9 | 3 | 1 | 0 | 0 | 0 | 73 | 5 | 6 (PHB table) |
| Ranger | 0 | 11 | 6 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 18 | 4 | none (SPL-039) |
| Paladin | 0 | 12 | 4 | 6 | 1 | 0 | 0 | 0 | 0 | 0 | 23 | 3 | none (SPL-039) |
| Adept | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | none (CHR-024) |

- Sorcerer equals Wizard because the legacy `ClassList` conversion mirrors the two. No spell is Sorcerer-only or Wizard-only.
- Druid has 49 entries for 48 spells (Dispel Magic is on the Druid list at 3 and at 4); Bard has 74 entries for 73 spells (Tasha's Hideous Laughter at 1 and 2).
- `Character/Classes/NPC/AdeptSpellList.cs` is a separate list of 60 IDs, of which 48 resolve; it has no production caller (CHR-024).
- 36 class entries sit at a level other than the spell's `SpellLevel` (for example Hold Person: `SpellLevel` 2, Wizard 3; Bestow Curse: 3, Wizard 4; See Invisible: 2, Bard 3). Casting, slot validation and DCs read `SpellLevel`, so these are wrong at runtime (SPL-008).
- Spells of a level above "Reachable" are data only for that class: about 117 spells of levels 3-9 cannot be prepared by any prepared caster (SPL-041).

### Implementation status

Each spell was classified by whether its ID (constant or literal, aliases resolved) appears in effect code: `Spell/` outside `Database/`, the GameManager partials in `_Core/`, `Services/`, `Combat/` and the `CharacterStats`/`CharacterController` effect hooks. Class default lists, NPC and dragon data, treasure tables, AI, UI and item factories were not counted. The heuristic over-counts: a referenced handler can still be unreachable (SPL-037, SPL-042) or partial (SPL-043, SPL-045, SPL-046).

| Kind | Meaning | Spells | Of which placeholders |
|---|---|---|---|
| Bespoke | ID referenced by effect code, or a summon spell (summons are detected by ID pattern) | 214 | 2 (Summon Monster I, II; they work, SPL-021) |
| Data-driven | no ID reference; dice (`SpellCaster.Cast`) or `Buff*` fields (`StatusEffectManager.AddEffect`) carry the effect | 33 | 2 (Glyph of Warding, Lesser Restoration: generic dice only, SPL-044) |
| No effect | neither | 45 | 43 |

Data-driven, not placeholder (31): Acid Splash, Cure Minor Wounds, Guidance, Inflict Minor Wounds, Ray of Frost, Resistance, Virtue; Bane, Bless, Bless Weapon, Burning Hands, Chill Touch, Cure Light Wounds, Divine Favor, Inflict Light Wounds, Longstrider; Acid Arrow, Acid Fog, Cure Moderate Wounds, Flame Strike, Inflict Moderate Wounds, Scorching Ray, Shatter, and the three test spells (`test_cone_30` Flame Jet, `test_cone_60` Glacial Blast, `test_line_60` Lightning Lance); Cure Serious Wounds, Heroism, Inflict Serious Wounds; Cure Critical Wounds, Inflict Critical Wounds. Burning Hands, Shocking Grasp, Flame Strike and the Cure and Inflict spells scale with caster level and Cure/Inflict invert on undead through `SpellDiceRules` (SPL-005, 2026-10-09); Scorching Ray still fires one ray (SPL-125), Shatter hits any creature at 1d6 (SPL-126) and Chill Touch is one touch whose save negates its damage (SPL-127); all `Buff*` attack, damage and save bonuses become morale bonuses (SPL-026).

No effect, not placeholder (2): Command and Hypnotic Pattern (SPL-045).

| Class | Bespoke | Data-driven | No effect |
|---|---|---|---|
| Wizard/Sorcerer | 144 | 13 | 32 |
| Cleric | 98 | 21 | 21 |
| Druid | 39 | 4 | 5 |
| Bard | 60 | 7 | 6 |
| Ranger | 13 | 1 | 4 |
| Paladin | 15 | 5 | 3 |

### Placeholders (47)

The flag does not block casting; it filters auto-prepare, the item factories, NPC templates and staff lists, and adds a UI marker (SPL-044, UI-009). Class lists as registered (Sor omitted; it mirrors Wiz).

| Level | Spells |
|---|---|
| 0 | Arcane Mark (Wiz), Detect Magic (Brd/Clr/Drd/Wiz), Detect Poison (Clr/Drd/Wiz 0, Pal/Rgr 1), Light (Brd/Clr/Drd/Wiz), Mage Hand (Brd/Wiz), Open/Close (Wiz), Read Magic (Brd/Clr/Drd/Wiz 0, Pal/Rgr 1) |
| 1 | Alarm (Brd/Rgr/Wiz), Comprehend Languages (Clr/Wiz), Deathwatch (Clr), Detect Secret Doors (Clr), Endure Elements (Clr/Drd/Pal/Rgr/Wiz), Erase, Feather Fall, Floating Disk, Hold Portal, Identify, Mount, Nystul's Magic Aura, Silent Image, Unseen Servant, Ventriloquism (all Wiz), Summon Monster I (Clr/Wiz, works) |
| 2 | Arcane Lock, Darkvision, Detect Thoughts, Knock, Minor Image, Pyrotechnics, Rope Trick, Spider Climb, Whispering Wind (Wiz); Augury, Delay Poison, Enthrall, Find Traps, Gentle Repose, Lesser Restoration, Make Whole, Remove Paralysis, Status, Zone of Truth (Clr); Locate Object, Obscure Object (Wiz 2, Clr 3); Summon Monster II (Clr/Wiz, works) |
| 3-4 | Glyph of Warding (Clr 3; resolves as 2d8 fire, likely against its caster, SPL-044), Giant Vermin (Clr 4) |

### Database entries that deviate from the PHB

Levels and lists below are from the PHB spell lists (Chapter 11), checked from memory rather than against the book text. Each row cites its issue ID.

| Entry | In code | PHB |
|---|---|---|
| Flame Strike | `SpellLevel` 2, Cleric only (SPL-099) | Clr 5, Drd 4 |
| Acid Fog | Wiz/Sor 2 (SPL-099), "-2 attack area" (SPL-048) | Sor/Wiz 6 |
| Acid Arrow and Melf's Acid Arrow | two entries (`acid_arrow`, data-driven; `melfs_acid_arrow`, bespoke) (SPL-103) | one spell, Sor/Wiz 2 |
| `test_cone_30`, `test_cone_60`, `test_line_60` | registered on the Wiz/Sor 2 list (SPL-104) | test fixtures, not PHB spells |
| Plane Shift | Clr 7, Wiz 7 (SPL-100) | Clr 5, Sor/Wiz 7 |
| Heal, True Seeing | Drd 6; Drd 5 and no Wiz (SPL-100) | Drd 7; Drd 7, Sor/Wiz 6 |
| Mislead | Brd 6 (SPL-100) | Brd 5 |
| Druid cure ladder | Cure Moderate 2, Cure Serious 3, no Cure Critical (SPL-101) | Drd 3, 4, 5 |
| Dispel Magic | Drd 3 and Drd 4 (SPL-101) | Drd 4 only |
| Ranger | Cure Light Wounds 1, Detect Undead 1 (SPL-101) | Cure Light Wounds 2; Detect Undead not on the list |
| Bard | Bull's Strength, Bear's Endurance, Owl's Wisdom at 2; Hideous Laughter at 1 and 2 (SPL-102) | not on the Bard list; Hideous Laughter Brd 1 only |
| Cleric general list | Holy Smite, Order's Wrath, Chaos Hammer, Unholy Blight, Command Plants, Spike Stones at 4; Dominate Animal, Plant Growth at 3; Protection from Arrows at 2 (SPL-102) | domain-only (Good/Law/Chaos/Evil/Plant/Earth/Animal) or Druid/Sor/Wiz |
| Formerly `domain_*` spells | Calm Animals, Detect Secret Doors, Longstrider, Magic Stone (Clr 1); Heat Metal, Hold Animal, Produce Flame, Soften Earth and Stone (Clr 2), all from `ClassList = { "Cleric" }` (SPL-105) | see section 2, "Duplicates history" |
| Bane, Sound Burst; Chaos Hammer, Holy Smite, Order's Wrath, Unholy Blight | Bane and Sound Burst are `SingleEnemy` (SPL-048); the four alignment bursts are `TargetType.Area` but resolve against one clicked target (SPL-046) | area spells |
| Display names | "See Invisible", "Floating Disk" (SPL-106) | See Invisibility, Tenser's Floating Disk |

### Notable PHB spells missing at levels 0-5

PHB levels from memory, not checked against the book text. "Not in DB" means no `SpellData` exists; "not on list" means the spell exists but this class cannot select it.

| Class | Not in DB | In DB, not on this class's list |
|---|---|---|
| Wizard/Sorcerer | Magic Mouth, Misdirection (2); Fly, Suggestion, Tongues, Gaseous Form, Major Image, Arcane Sight, Clairaudience/Clairvoyance, Nondetection, Water Breathing (3); Animate Dead, Polymorph, Shadow Conjuration, Scrying, Lesser Geas, Stone Shape, Minor Creation (4); Cloudkill, Teleport, Summon Monster V, Wall of Stone, Feeblemind, Baleful Polymorph, Waves of Fatigue, Overland Flight, Transmute Rock to Mud (5) | Detect Secret Doors (1); Gentle Repose (3); Dismissal (5) |
| Cleric | Bless Water, Curse Water (1); Undetectable Alignment (2); Animate Dead, Create Food and Water, Deeper Darkness, Speak with Dead, Stone Shape, Water Breathing (3); Air Walk, Discern Lies, Divination, Tongues, Sending, Lesser Planar Ally (4); Raise Dead, Mass Cure Light Wounds, Slay Living, Righteous Might, Dispel Evil/Good/Law/Chaos, Spell Resistance, Summon Monster V, Wall of Stone, Commune (5) | Flame Strike and Plane Shift at 5 (registered at 2 and 7) |
| Druid | Charm Animal, Faerie Fire, Goodberry, Shillelagh, Speak with Animals, Pass without Trace (1); Flame Blade, Chill Metal, Tree Shape (2); Greater Magic Fang, Spike Growth, Stone Shape (3); Rusting Grasp, Reincarnate, Air Walk, Scrying (4); Baleful Polymorph, Call Lightning Storm, Summon Nature's Ally V, Transmute Rock to Mud (5) | Cure Minor Wounds, Flare, Guidance, Virtue (0); Calm Animals, Longstrider, Magic Stone, Produce Flame (1); Heat Metal, Hold Animal, Soften Earth and Stone, Lesser Restoration, Delay Poison, Spider Climb (2); Dominate Animal, Plant Growth, Neutralize Poison, Poison, Remove Disease (3); Flame Strike, Command Plants, Spike Stones, Giant Vermin, Repel Vermin (4); Cure Critical Wounds, Death Ward (5) |
| Bard | Summon Instrument (0); Lesser Confusion, Magic Mouth (1); Suggestion, Tongues, Misdirection (2); Good Hope, Major Image, Gaseous Form, Lesser Geas (3); Greater Dispel Magic, Mass Cure Light Wounds, Greater Heroism, Summon Monster V (5) | Flare, Open/Close (0); Cause Fear, Comprehend Languages, Detect Secret Doors, Erase, Feather Fall, Identify, Nystul's Magic Aura, Obscure Object, Silent Image, Summon Monster I, Unseen Servant, Ventriloquism (1); Calm Emotions, Detect Thoughts, Enthrall, Locate Object, Minor Image, Pyrotechnics, Summon Monster II, Whispering Wind, Delay Poison (2); Blink, Confusion, Remove Curse, Summon Monster III (3); Summon Monster IV, Neutralize Poison, Repel Vermin (4) |
| Ranger | Charm Animal, Speak with Animals, Pass without Trace (1) and most other Ranger utility spells | Calm Animals, Longstrider, Delay Poison (1); Hold Animal, Summon Nature's Ally II (2; Cure Light Wounds is on the list, but at 1); Command Plants, Plant Growth, Neutralize Poison, Remove Disease, Cure Moderate Wounds, Darkvision, Summon Nature's Ally III (3); Cure Serious Wounds, Summon Nature's Ally IV (4) |
| Paladin | Bless Water (1); Undetectable Alignment (2); Discern Lies, Heal Mount (3); Holy Sword, Dispel Evil, Mark of Justice (4) | Create Water, Lesser Restoration, Virtue (1); Delay Poison, Remove Paralysis, Shield Other, Zone of Truth (2); Cure Moderate Wounds, Prayer, Remove Blindness/Deafness (3); Cure Serious Wounds, Death Ward, Neutralize Poison, Restoration (4) |

For the encounter-driven game loop, the high-value gaps are the combat staples Fly, Suggestion, Cloudkill, Summon Monster V and Summon Nature's Ally V, Raise Dead (the only raising effects in the database are Resurrection, Clr 7, which no Cleric can reach, and Wish, Sor/Wiz 9), Mass Cure Light Wounds and Righteous Might.

## 2. Spell IDs and aliases

**Files and order.** `SpellDatabase.Init()` calls the partial methods `RegisterSpellsA()` to `RegisterSpellsZ()` in file order, then `AnnotateDomainAvailabilityFromDomainDatabase`, `AnnotateSpellDescriptors`, `SpellComponentRegistry.Init` and `SpellCategoryClassifier.ReclassifyAll`, which rewrites `EffectType` (details in [architecture/spells.md](../architecture/spells.md#spell-data-model)).

**SpellNames.** `Spell/Data/SpellNames.cs` (namespace `DND35e.Identifiers`) has 318 `public const string` entries: 299 string literals and 19 constants defined as another constant (deprecated: `BLINDNESS_DEAFNESS_WIZ/_CLR/_BRD`, `DETECT_MAGIC_WIZ`, `DETECT_POISON_WIZ`, `RESISTANCE_WIZ`, `SEE_INVISIBILITY` (equal to `SEE_INVISIBLE`) and the 12-entry `DOMAIN_*` set, which has 0 references; SPL-076). Of the 299 literal values, 292 are the registered spells (every registered spell has a constant), 2 are legacy IDs kept only as aliases (`hideous_laughter`, `see_invisibility`), and 5 are not registered at all (`PROTECTION_FROM_ENERGY_ACID/_COLD/_ELECTRICITY/_FIRE/_SONIC`). IDs are lowercase snake_case; the display `Name` can differ (`see_invisible` is "See Invisible", `soften_earth` is "Soften Earth and Stone", `tashas_hideous_laughter`).

**Alias registration.**
- `RegisterAlias(alias, canonical)` only maps an ID. The alias map is case-insensitive; `_spells` is case-sensitive. 23 calls: the 12 `domain_*` IDs from the consolidation (below), `domain_protection_from_chaos/_good/_law`, `blindness_deafness_{wiz,brd,clr}`, `detect_magic_wiz`, `detect_poison_wiz`, `resistance_wiz`, `hideous_laughter`, `see_invisibility`.
- `RegisterClassSpellAlias(alias, canonical, className, level, domain = null)` maps the ID and calls `canonical.AddAvailability(className, level, domain)`. It works only if the canonical spell is already registered (files run A to Z), else it logs a warning. 132 calls, none with a domain. Alias IDs are literal strings `<canonical>_<suffix>`: `_brd` 41, `_drd` 26 (plus one `_dru`), `_clr` 25, `_pal` 17, `_rgr` 14, `_wiz` 3, `_sor` 3, plus `hideous_laughter_brd` and `see_invisibility_brd`. By class: Bard 43, Druid 27, Cleric 25, Paladin 17, Ranger 14, Wizard 3, Sorcerer 3.
- `Register` silently overwrites a duplicate `SpellId`.

**Rules for new spells.** One `SpellData` per PHB spell. Put the primary class and level in `ClassList`/`SpellLevel` or an explicit `AvailableFor` list; add other classes at other levels with `RegisterClassSpellAlias` in the same or a later file. Never register `domain_*` or per-class copies; domain lists come from `Character/Religion/DomainDatabase.cs`. Recipe: "Add a spell (data)" in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md).

**Lookup.**
- `SpellDatabase.GetSpell(id)` tries `_spells`, then the alias map, and returns the canonical `SpellData`, so `spell.SpellId` is canonical even when an alias was passed. It logs a warning on a miss (SPL-069). Handlers compare `spell.SpellId` with canonical constants using ordinal comparison.
- `GetSpellByName(name)` is a case-insensitive linear scan of `Name`. It has 10 non-test call lines in 7 files outside the database (`ScrollValidator`, `WandValidator`, `ItemBuilder`, `WandData`, `CharmedBehaviorController`, `SpellcastingComponent`, `_Core/GameManager.cs` with 4), plus one in `SpellDatabase.IsSpellInDomain`; the common `GetSpell(x) ?? GetSpellByName(x)` logs a spurious miss for display names.
- Raw IDs that never pass through `GetSpell` do not match canonical comparisons. `ActiveSpellEffect` keeps backward-compatibility `case "domain_protection_from_good/law/chaos"` labels next to the canonical constants (dead, since casts carry canonical IDs). `BardClass` lists the cantrip `"read_magic_wiz"`, which is neither registered nor aliased; its `GetSpell(id) != null` filter drops it, so the default Bard silently loses Read Magic (CHR-059).

**Per-class levels.** `SpellLevel` is a single primary level. `GetSpellLevelFor(className, domain)` returns the lowest matching `AvailableFor` level; `IsAvailableFor(className, level)` needs an exact level and ignores domain entries. About 250 non-test lines outside the database read `.SpellLevel`; 13 call `GetSpellLevelFor(` (item factories and validators, and two in `SpellcastingComponent`). Spontaneous learning, the item factories and the save DC (`SpellSaveDCRules.GetEffectiveSpellLevel`, SPL-001) use per-class levels; slot validation and `CanCast` use `SpellLevel` (SPL-008). `InitWizard` filters a selected spellbook with `IsAvailableFor("Wizard", spell.SpellLevel)`, which drops every spell whose Wizard level differs (Hold Person, Bestow Curse, Contagion, Remove Curse).

**Duplicates history.** Commit aa1ad37 (2026-05-27) consolidated the duplicates listed in the old `spell_duplicate_analysis` report:
- The 10 domain-only copies became canonical entries: Calm Animals, Desecrate, Detect Secret Doors, Entangle, Heat Metal, Hold Animal, Longstrider, Magic Stone, Produce Flame, Soften Earth and Stone. `domain_barkskin` and `domain_wind_wall` were deleted in favour of Barkskin and Wind Wall.
- The three Blindness/Deafness entries became one (`blindness_deafness`, `SpellLevel` 2; Wiz/Sor/Brd 2, Clr 3).
- `detect_magic_wiz`, `detect_poison_wiz` and `resistance_wiz` were renamed to `detect_magic`, `detect_poison` and `resistance`.

IDs that must keep resolving (saves, prepared lists, tests): `domain_barkskin`, `domain_wind_wall`, `domain_calm_animals`, `domain_desecrate`, `domain_detect_secret_doors`, `domain_entangle`, `domain_heat_metal`, `domain_hold_animal`, `domain_longstrider`, `domain_magic_stone`, `domain_produce_flame`, `domain_soften_earth`, `blindness_deafness_{wiz,brd,clr}`, `detect_magic_wiz`, `detect_poison_wiz`, `resistance_wiz`, `hideous_laughter`, `see_invisibility`. Deliberately separate entries (do not merge): Summon Monster I-IV, Summon Nature's Ally I-IV, Cure and Inflict Light to Critical Wounds, and the test spells.

Open follow-ups from that consolidation:
- Nine of the renamed spells kept `ClassList = { "Cleric" }`, which puts them on the general Cleric list; only Entangle gained Druid/Ranger. PHB targets (from memory of the PHB lists, not checked against the book text): Calm Animals Drd 1/Rgr 1; Heat Metal Drd 2; Hold Animal Drd 2/Rgr 2; Longstrider Drd 1/Rgr 1; Magic Stone Clr 1/Drd 1; Produce Flame Drd 1; Soften Earth and Stone Drd 2; Detect Secret Doors Brd 1/Sor/Wiz 1. Remove the general Cleric entry except for Desecrate and Magic Stone. Produce Flame's `SpellLevel` is 2, so a Druid 1 entry also needs SPL-008 fixed for correct DCs.
- `Tests/Combat/BlindnessDeafnessRulesTests.cs` asserts `GetSpell(BLINDNESS_DEAFNESS_CLR).SpellLevel == 3`; since aa1ad37 that is 2. The test should assert `GetSpellLevelFor("Cleric") == 3`.

## 3. Ranges

`SpellRangeCategory` (`Spell/Data/SpellRanges.cs`), in 5-ft squares, integer division rounding down:

| Category | Squares | Feet | CL 1 | CL 5 | CL 10 | CL 20 | Spells |
|---|---|---|---|---|---|---|---|
| Personal | -1 | self | | | | | 39 |
| Touch | 1 | touch | | | | | 92 |
| Close | 5 + 1 per 2 CL | 25 ft + 5 ft/2 levels | 5 | 7 | 10 | 15 | 55 |
| Medium | 20 + 2 per CL | 100 ft + 10 ft/level | 22 | 30 | 40 | 60 | 47 |
| Long | 80 + 8 per CL | 400 ft + 40 ft/level | 88 | 120 | 160 | 240 | 14 |
| Unlimited | 9999 (`UNLIMITED_RANGE_SQUARES`) | | | | | | 0 |
| Custom | raw fields | | | | | | 2 (Animate Rope 10, Shield of Law 4) |
| (none set) | raw `RangeSquares` | | | | | | 43 |

The example values match `Tests/Magic/SpellRangeCategoryTests.cs`. `SpellRanges.GetFormulaDescription` returns the UI strings ("5 sq + 1 sq/2 lv", "Self", "Touch", "Unlimited").

**Semantics.**
- The database idiom is the object initializer `RangeCategory = SpellRangeCategory.Medium`. `SpellData.CreateWithRange`, `SetRange`, `SetRangeClose/Medium/Long` exist but only tests call them.
- The `RangeCategory` setter has a side effect: a non-Custom value calls `SpellRanges.Configure`, which overwrites `RangeSquares`, `RangeIncreasePerLevels` and `RangeIncreaseSquares`. A later `RangeSquares = N` in the same initializer changes the raw field, but range math ignores it, because `GetRangeSquaresForCasterLevel` uses the category profile for any non-Custom category.
- With Custom or no category, `SpellRanges.TryDetectCategory` promotes raw values only on an exact (base, per-levels, squares) match; otherwise the raw values are used as a fixed range. A base of 0 or less is returned unchanged (self).
- AoE placement uses `AoERangeSquares` when it is above 0, a fixed value that does not scale: Acid Fog 22, Call Lightning 22, Flame Strike 22, Fog Cloud 22, Rainbow Pattern 20, Obscuring Mist 4, Darkness 1, Daylight 1.

**The 43 raw-range entries.** None sets a per-level increase, so all are fixed ranges. For cone and line spells `RangeSquares` holds the area length.

| `RangeSquares` | Spells |
|---|---|
| 0 | Bless, Bless Weapon, Prayer |
| 2 | Mending, Prestidigitation, Purify Food and Drink |
| 3 | Burning Hands, Color Spray (cones) |
| 5 | Alarm, Create Water, Detect Poison, Erase, Feather Fall, Ghost Sound, Mage Hand, Make Whole, Mount, Open/Close, Remove Paralysis, Summon Monster I, Summon Monster II, Unseen Servant, Ventriloquism, Zone of Truth |
| 6 | Fear, Shout, Flame Jet (cones) |
| 8 | Minor Image, Pyrotechnics, Silent Image |
| 10 | Bane |
| 12 | Detect Thoughts; Gust of Wind, Lightning Lance (lines); Glacial Blast (cone) |
| 22 | Acid Fog, Call Lightning, Enthrall, Hold Portal, Knock, Message |
| 24 | Lightning Bolt (line), Produce Flame |

PHB Close-range spells among them (Summon Monster I/II, Ghost Sound, Mage Hand and others) therefore never grow with caster level.

**Which caster level the range uses.**

| Consumer | Caster level passed |
|---|---|
| PC single-target targeting (`GameManager.ShowSpellTargets`) | `Stats.Level` (total character level) |
| AoE placement and preview (`EnterAoETargetingMode`, `UpdateAoEPreview`, `ClearAoEPreviewHighlights`, `HandleAoETargetClick`) | `AoERangeSquares` if above 0, else `Stats.GetCasterLevel()` |
| NPC casts (`TryNPCPerformSpellCast`) | `Stats.GetCasterLevel()` |
| Summon and Summon Swarm placement (`ShowSummonPlacementTargets`, `ShowSummonSwarmPlacementTargets`, and the summon resolution in `GameManager.SpellCasting.cs`) | `Stats.Level` |
| Combat spell-button label (`CombatUI`) | `Stats.Level` |
| Spell selection screen (`SpellSelectionUI`) | none: displays the raw fields |

Caveats: Enlarge Spell doubles only raw fields, so it has no range effect on category spells (SPL-010); `Stats.Level` is wrong for multiclass casters (SPL-017).

## 4. Casting classes

| Class | Model | Slots and cap | Spells known or preparable | Status |
|---|---|---|---|---|
| Wizard | prepared, INT (`SpellcastingComponent.InitWizard`) | `GetWizardSlotsForLevel`: L1 3/1, L2 4/2, L3 4/2/1, L4+ fixed at 4/3/2. No 3rd-level slots at any level | creation selection (`SelectedSpellIds`, filtered by `IsAvailableFor("Wizard", SpellLevel)`); with none, every Wizard cantrip, plus every Wizard 1-2 spell unless the character also has Cleric or Druid levels | works to 2nd level (SPL-041). Specialist: +1 slot per castable level, cantrips included, school matched by string equality (SPL-009). Ring of Wizardry doubles regular slots only for its level if below 3, so rings III and IV do nothing |
| Sorcerer | spontaneous, CHA (`SpontaneousCastingData`) | PHB slots-per-day and spells-known tables to level 20, CHA bonus slots by the PHB formula | creation selection, learned at the per-class level; learn, forget and swap exist | works for casting. `CanCast` checks `SpellLevel` (SPL-008); metamagic does not work (SPL-040). Sorcerer takes precedence over Bard |
| Cleric | prepared, WIS (`InitCleric`) | `GetClericBaseRegularSlotsForCasterLevel`: PHB table to caster level 9 (5th-level slots), clamped there; one domain slot per castable level 1+ | all Cleric spells of levels 0-2 (selected orisons or all orisons, plus all 1st and 2nd) | slots of level 3-5 stay empty unless filled with a metamagic-prepared lower spell (inferred from code). Domain slots of level 3+ are always empty. Manual preparation rejects domain-only spells (SPL-014) |
| Cleric spontaneous | convert a prepared non-domain spell (`SpontaneousCastFromSpecificSpell`, `SpontaneousCastFromSlot`) | `SpontaneousCastingHelper.GetSpontaneousSpellId`: Cure or Inflict Minor to Critical, levels 0-4 | `CharacterStats.SpontaneousCasting` (Cure or Inflict) | works; no level 5+ (mass cures are not in the database) |
| Druid | prepared, WIS (`InitDruid`) | `GetDruidSlotsForLevel`: L1 3/1, L2 4/2, L3+ fixed at 4/2/1 | all Druid 0-2 spells; with a creation selection, any selected ID is added without a class check | works to 2nd level (SPL-041). No spontaneous summoning: the PHB conversion of a prepared spell into Summon Nature's Ally has no code (SPL-108) |
| Bard | spontaneous, CHA | PHB tables to level 20, max 6th level | creation selection; the creation UI's spells-known table is wrong (UI-003) | as Sorcerer. Bardic Music is separate (CHR-020) |
| Ranger | partial (`PartialCasterData`), WIS | PHB table from class level 4, spell levels 1-4, PHB bonus formula | none | display only: no `SpellSlots`, no preparation UI, empty combat list (SPL-039). Caster level counts the full class level (PHB: half; SPL-017) |
| Paladin | partial, WIS | as Ranger | none | as Ranger; Paladin-only spells such as Bless Weapon are unreachable |
| Adept (NPC) | none | none | `AdeptSpellList` (unused) | cannot cast; counted as arcane (DMG Chapter 4, NPC classes: divine) (CHR-024) |

Shared rules and gaps:
- Prepared casters get a flat +1 bonus slot when the ability modifier is at least the spell level, not the PHB bonus-spells table (SPL-034).
- A multiclass character with Sorcerer or Bard cannot cast its prepared spells (SPL-011). Multiclass prepared pools are kept per class (`SpellSlot.CasterClassName`), in the order Wizard, Cleric, Druid.
- There is no per-day model: `GameManager.RestorePartyAfterCombat` restores slots after every won combat, and `RestoreAllSlots` returns early for spontaneous and partial casters, so a multiclass with one of them never restores its prepared slots (see [architecture/spells.md](../architecture/spells.md#casting-resources); the rest model is the party-management backlog).
- `RefreshSpellSlots` (level-up, Ring of Wizardry, creation) drops metamagic preparations and imbue locks (SPL-013). Creation-time preparation is matched to slots by position (SPL-015).
- NPC casters use the same component but cast only through `TryNPCPerformSpellCast`: no area spells and no metamagic (AI-001; see [AI.md](AI.md)). Its `SpellcastingComponent.CanCast` check reads the empty legacy slot array of a spontaneous caster, so an AI-run sorcerer, bard or spellcasting dragon is refused every spell (SPL-123, seen in Play mode 2026-10-09).
- **Save DC** (one rule for every path since 2026-10-09, SPL-001): `SpellSaveDCRules` (`Spell/Casting/SpellSaveDCRules.cs`). A spell that carries `SaveDC > 0` (scroll, wand, stored or imbued spell) keeps it; otherwise 10 + the spell's level for the casting class (PHB p.177; the heightened level with Heighten Spell, PHB p.95) + that class's key ability (Wizard INT; Sorcerer and Bard CHA; Cleric, Druid, Paladin, Ranger and Adept WIS, DMG p.108) + Spell Focus and Greater Spell Focus for the parsed school (PHB p.94, p.100) + 1 for a gnome's illusion (PHB p.17). The casting class is an explicit class, else the class of the slot the caster last spent on that spell (`SpellcastingComponent.GetCastingClassForSpellDC`: the slot is already used when the cast resolves its DC; a sorcerer's or bard's cast records its class too), else the class of an unused prepared slot holding the spell, else the highest-level casting class whose list has the spell, else the highest casting class; a creature with no casting class uses its best mental modifier, as before. Spell-like abilities: `ExplainSpellLikeAbility` (MM p.315: sorcerer/wizard level, else cleric, druid, bard, paladin, ranger, + CHA; no caller yet). Magic items: `ForMagicItem` (DMG p.214: 10 + level + level / 2) for scrolls and wands, at the level on the item's arcane or divine list (`GetItemSpellLevel`; prices still use the lowest level on any list, ITM-074); a staff's DC is the wielder's own (`StaffValidator.CalculateStaffSaveDC`, DMG p.214; staffs roll no saves yet). `SpellCaster.Cast`, `SpellUtilities.GetSpellSaveDC`, the GameManager handlers (`GetSpellSaveDC`, which passes `_pendingMetamagic` for the pending PC cast), `SpellcastingComponent.GetSpellDC`, Ring of Spell Storing and Imbue with Spell Ability all call it. Crafted scrolls bake the crafter's own DC through the same rule (whether they should use the item DC instead is ITM-008, an owner question); the save-modifier and SR helpers are still duplicated (SPL-122).

## 5. Metamagic

The nine PHB metamagic feats are `MetamagicFeatId` in `Spell/Components/MetamagicData.cs`. A character has a feat when `Stats.HasFeat(MetamagicData.GetFeatName(id))` matches the exact name ("Empower Spell" and so on; `SpellcastingComponent.GetKnownMetamagicFeats`); the feats are defined in `Character/Feats/FeatDefinitions.cs`. The effective level is base plus adjustments, capped at 9 by `GetEffectiveSpellLevel`. Three parallel switches hold the adjustments (static `GetLevelAdjustment(feat)`, `GetStandardLevelAdjustment(feat)` and instance `GetLevelAdjustment(feat, baseLevel)`) and must stay in sync. The `MetamagicSystem` pipeline in `MetamagicModifier.cs` is not the runtime path; only `MetamagicSystem.IsSpontaneousCaster` runs in game (SPL-055).

| Feat | Adj. | Offered when (`MetamagicData.IsApplicable`) | Effect in code | Deviation from PHB (Chapter 5, Feats, metamagic feat entries; from memory) |
|---|---|---|---|---|
| Enlarge Spell | +1 | `RangeSquares > 0`, which includes Touch (1) | `SpellCaster.ApplyMetamagicToSpellData` doubles raw range fields (ignored by category spells) and, since 0dd8e76, `AoESizeSquares` and `AreaRadius` | PHB: close, medium or long range only, range only. Here touch spells qualify, category ranges do not change and area doubles (SPL-010) |
| Extend Spell | +1 | `BuffDurationRounds != 0` (legacy field) | doubles `DurationValue` and `BuffDurationRounds` unless `DurationType` is Instantaneous, Permanent or Concentration | every spell has its PHB duration through `SpellDurationRules` since 2026-10-09 (SPL-002), so Extend now doubles the spells that set the legacy field; it is still offered and charged for five of them whose duration it cannot change (permanent or concentration) and never offered for about 76 spells without the legacy field (SPL-095); handlers that read the spell's duration follow it, others with fixed durations ignore it (SPL-038) |
| Widen Spell | +3 | `AreaRadius > 0` or `TargetType == Area` | doubles `AreaRadius` and `AoESizeSquares` | persistent zones ignore it (SPL-029) |
| Quicken Spell | +4 | always | `ActionType = Free`, one per round (`MarkQuickenedSpellCast`, reset each round) | needs a standard action to open the cast menu (SPL-019); PHB excludes spells with a casting time over 1 full round, the code does not (SPL-098) |
| Silent Spell | +1 | always | clears `HasVerbalComponent` | |
| Still Spell | +1 | always | clears `HasSomaticComponent` | arcane spell failure is still rolled (PHB: no somatic component, no failure chance; SPL-096) |
| Empower Spell | +2 | `EffectType` Damage or Healing | `SpellCaster.Cast` adds `RoundToInt` of 50% of the rolled total (dice plus `BonusDamage`/`BonusHealing`) | only the generic dice path: Fireball, Lightning Bolt and other custom resolvers ignore it (SPL-038) |
| Maximize Spell | +3 | same test as Empower (no dice inspection) | `SpellCaster.Cast` sets every die to maximum; with Empower, maximum x1.5 | PHB, with Empower: maximum plus half of a normal roll (SPL-097); custom resolvers ignore it (SPL-038) |
| Heighten Spell | to `HeightenToLevel` | always | `SpellSaveDCRules` uses `HeightenToLevel` for the DC on the generic path and in every GameManager handler that resolves the pending PC cast (through `GameManager.GetSpellSaveDC`) | PHB: every level-dependent effect; here Lesser Globe and dispel use the base level (SPL-038) |

**Sources of metamagic.**

| Path | How | Status |
|---|---|---|
| Prepared (Wizard, Cleric, Druid) | `UI/Spells/SpellPreparationUI.cs` slot dropdowns; stored in `SpellSlot.AppliedMetamagic`; cast through `CastWizardSpellWithMetamagic` | works, with the gaps below |
| Spontaneous (Sorcerer, Bard) | `CombatUI.ShowMetamagicPanel`, shown when the caster is spontaneous and has a metamagic feat | non-functional: confirm requires `HasSlotAtLevel`, which reads only `SpellSlots` (empty for a pure Sorcerer or Bard); no Heighten level picker (SPL-040) |
| Scrolls and wands | `InitiateScrollCastThroughPipeline` / `InitiateWandCastThroughPipeline` (`_Core/GameManager.cs`) build `MetamagicData` from `ScrollData`/`WandData` feats and `HeightenToLevel`, apply it to a clone and copy the item DC into `SaveDC` | works; reader's caster level (SPL-016); crafting cost bug (ITM-009) |
| Self-use consumables | `GameManager.BuildConsumableSpellVariant` | flat average Empower (SPL-033); setup duplicated three times (SPL-064) |
| Metamagic rods | `MetamagicData.RodAppliedMetamagic`, `Equipment/Rods/MetamagicRodActivation.cs`; design in `docs/designs/metamagic_rods_specification.md` | data and tests only; rods can be bought but not used (ITM-018) |
| F12 test panel | `SpellTestingPanel` toggles to `GameManager.TestCastSpellFromPanel` | debug only |
| NPCs | `TryNPCPerformSpellCast` passes null; `AISpellcastingStrategist.GetMetamagicScore` scores but never applies | none (AI-001) |

**Prepared metamagic in `SpellPreparationUI`.** PHB rule: a prepared caster chooses metamagic when preparing, and the spell occupies a slot of its adjusted level. The UI puts the arithmetic in the slot row instead of feat checkboxes, so only exact fits are offered.
- Each row's dropdown lists "(Empty)", the known spells of that slot level, and options prefixed with a lightning-bolt glyph: lower-level known spells (cantrips included) combined with metamagic the character has, where the total equals the slot level. Generated in `SpellPreparationUI.CreateSlotRow`: one fixed-adjustment feat with base + adjustment = slot; Heighten for any lower-level spell, with `HeightenToLevel = slot.Level`; pairs of two different fixed-adjustment feats. There are no three-feat combinations, and Heighten is never paired. Every option passes `IsApplicable`. Domain slots, specialist slots and level-0 slots get no options.
- Label: glyph, adjectives joined by "+" in enum order, spell name, then "(lvl X +Y → lvl Z)" (`BuildMetamagicOptionLabel`).
- Choosing an option prepares the slot immediately: `OnSlotChanged` calls `SpellcastingComponent.PrepareSpellInSlotWithMetamagic`, and on failure the dropdown resets to "(Empty)". "Confirm Preparation" (`OnConfirm`) only syncs `PreparedSpells`, closes the screen and fires `OnPreparationConfirmed`.
- Backend validation in `PrepareSpellInSlotWithMetamagic`: level match first, then domain and specialist slots rejected, then `IsSpellKnownByClass` for the slot's casting class. The level check uses the capped `GetEffectiveSpellLevel`, so only the UI's exact-sum generation prevents an over-cap combination; `ExceedsMaxSpellLevel` is not called.
- Character-creation preparation (`OpenForCreation`, `CreateCreationSlotRow`) offers no metamagic. Auto-Prepare (`AutoPrepareWizardSlots`/`Cleric`/`Druid`) never chooses metamagic and wipes existing metamagic preparations, because `SpellSlot.Prepare` sets `AppliedMetamagic = null`.
- Because Wizard and Druid slots stop at 2nd level, the useful combinations today are +1 feats on cantrips or 1st-level spells and Empower on cantrips; Cleric 3rd-5th level slots can hold only such lower-level metamagic spells (inferred from code).

**Casting a metamagic-prepared slot.** `SpellcastingComponent.GetUniqueAvailableSpells` returns a clone carrying `MetamagicDataRef`, keyed by `SpellId + "|" + MetamagicData.GetDisplayName()`. `CombatUI` passes the reference to the cast callback, and `ConsumePendingSpellSlot` calls `CastWizardSpellWithMetamagic`, which looks for a slot at the effective level with the same spell and key, then the same spell, then, as a last resort, any unused prepared slot at that level whatever it holds (SPL-012). `SpellCaster.ApplyMetamagicToSpellData` applies the data-changing feats and `SpellCaster.Cast` applies Empower, Maximize and Heighten. Traps:
- The key is the matching key for the dropdown, the prepared counts and the slot to spend. Changing `GetAdjective` or `GetDisplayName` breaks it.
- The key omits `HeightenToLevel`, so `CountAvailablePreparedSpell` merges Heightened copies of one spell prepared at different levels.
- `CountAvailablePreparedSpell` returns 999 for any spell with `SpellLevel` 0 before checking metamagic, so a metamagic cantrip in a 1st-level slot shows as unlimited; the slot is still spent correctly (inferred from code).
- A spontaneous metamagic cast costs a full-round action, decided by `MetamagicSystem.IsSpontaneousCaster` (has Sorcerer or Bard), so a Wizard/Sorcerer also pays it for prepared metamagic.

Tests: `Tests/Magic/MetamagicSystemTests.cs` and `Tests/Equipment/RodTests.cs`; recent metamagic and wand work is untested (TST-015). Recipe: "Add a metamagic feat" in [DEVELOPMENT_RECIPES.md](../DEVELOPMENT_RECIPES.md).

## 6. Cleric domains

Data: `Character/Religion/DomainDatabase.cs` (22 PHB domains, `DomainData` with a display-only power text and domain spells for levels 1 and 2 only). Choice: `CharacterCreationUI` step `ChooseDomains` at creation and `DomainSelectionUI` at level-up (UI-033); stored in `CharacterStats.ChosenDomains`. Powers: `Spell/Domain/GameManager_DomainPowers.cs`; domain spell handlers: `GameManager_DomainSpells.cs` and `GameManager_DomainAreaSpells.cs`. Every domain spell ID resolves to a database entry; spell status is in the "Spells" column (PH = placeholder).

| Domain | Spells 1 / 2 | Granted power (PHB Chapter 3, Cleric) | Code status |
|---|---|---|---|
| Air | Obscuring Mist / Wind Wall | turn or destroy earth creatures, rebuke or command air creatures | wrong: affects the domain's own subtype (air), turn or rebuke chosen by alignment, shares the Turn Undead pool; the Commanded result has no consumer (CHR-062) |
| Animal | Calm Animals / Hold Animal | Speak with Animals 1/day; Knowledge (nature) class skill | not implemented |
| Chaos | Protection from Law / Shatter | chaos spells at +1 CL | works for spells tagged Chaotic; the Magic Circle tags use wrong IDs (SPL-035) |
| Death | Cause Fear / Death Knell | death touch 1/day | unreachable: `ActivateDomainPowerByName` only sets `_pendingDomainPower`, and `ResolveDomainPowerOnTarget` has no caller; because the use is never spent, a cleric with Death listed first can never use the other domain's power (CHR-060) |
| Destruction | Inflict Light Wounds / Shatter | smite: +4 attack, +cleric level damage, 1/day | +4 attack works; the damage bonus is never added (CHR-005); ranged attacks also qualify |
| Earth | Magic Stone / Soften Earth and Stone | turn air, rebuke earth | wrong, as Air |
| Evil | Protection from Good / Desecrate (Desecrate unreachable, SPL-042) | evil spells at +1 CL | works |
| Fire | Burning Hands / Produce Flame | turn water, rebuke fire | wrong, as Air |
| Good | Protection from Evil / Aid | good spells at +1 CL | works; Flame Strike is wrongly tagged Good (SPL-107) |
| Healing | Cure Light / Cure Moderate Wounds | healing spells at +1 CL | works |
| Knowledge | Detect Secret Doors (PH) / Detect Thoughts (PH) | all Knowledge skills are class skills; divination at +1 CL | CL works; class skills set only on level-up and still cost 2 points (CHR-023) |
| Law | Protection from Chaos / Calm Emotions | law spells at +1 CL | works |
| Luck | Entropic Shield / Aid | reroll one roll 1/day, must take the reroll | deviates: a pre-armed toggle that keeps the better roll (`CharacterStats.ApplyLuckReroll`); hooks attack rolls, saves through `SavingThrowResolver` and skill checks only (CHR-063) |
| Magic | Nystul's Magic Aura (PH) / Identify (PH) | use scrolls, wands and other spell-trigger and spell-completion items as a wizard of half cleric level | works (`HasMagicDomain` in the scroll, wand and staff validators and `QuickItemUsePanel`) |
| Plant | Entangle / Barkskin | rebuke or command plant creatures; Knowledge (nature) class skill | partial: rebuke only, shared Turn Undead pool, allies affected; class skill not granted |
| Protection | Sanctuary / Shield Other | protective ward: touch, +cleric level resistance bonus on the next save, 1/day | not implemented; the `DomainDatabase` text says +1 |
| Strength | Enlarge Person / Bull's Strength | +cleric level enhancement bonus to STR for 1 round, 1/day | works, but through the untyped `TemporarySTRBonus`, so it stacks with enhancement bonuses (CHR-065) |
| Sun | Endure Elements (PH) / Heat Metal | greater turning 1/day: undead that would be turned are destroyed | works, with no level bonus as in the PHB; the file header comment still claims +2 levels and +1d10 (CHR-066). Heat Metal never deals its per-round damage (`ProcessHeatMetalTick` has no caller; SPL-109) |
| Travel | Longstrider / Locate Object (PH) | act normally despite movement-impeding magic for 1 round per cleric level per day; Survival class skill | no-op: `TravelDomainFreedomRounds` is set and ticked but never read (CHR-061); class skill not granted |
| Trickery | Disguise Self / Invisibility | Bluff, Disguise and Hide are class skills | not implemented |
| War | Magic Weapon / Spiritual Weapon | Martial Weapon Proficiency and Weapon Focus with the deity's favored weapon | works at initial creation only (`GameManager.GrantWarDomainFeats`); a domain picked at level-up gets nothing (CHR-064) |
| Water | Obscuring Mist / Fog Cloud | turn fire, rebuke water | wrong, as Air |

Cross-cutting:
- **Caster level bonus.** `CharacterStats.GetDomainBoostedCasterLevel(spell, className)` adds at most +1 when a chosen domain matches a `SpellDescriptor` (Good, Evil, Lawful, Chaotic, Healing, Divination). With no class name it uses the highest casting class, so a multiclass cleric also boosts non-cleric spells, and the many plain `GetCasterLevel()` calls in spell code get no bonus. Descriptors come from `SpellDatabase.AnnotateSpellDescriptors`, which silently skips unknown IDs.
- **Uses.** The `*DomainUsesToday` counters reset in `RestorePartyAfterCombat` after every combat, not per day.
- **UI.** One Domain Power button (`UI/Combat/ActionButtonPanel.cs`, visible for a hard-coded list of 11 domains) activates the first available power in `ChosenDomains` order; there is no chooser and no status icons.
- **Turning math.** Plant and elemental turning duplicate the turn calculation (`GetMaxTurnableHD`) instead of extending `Combat/Special/TurnUndeadSystem.cs`, which has no rebuke mode (CMB-026). The intended PHB rule: Air turns earth and rebukes air; Earth turns air and rebukes earth; Fire turns water and rebukes fire; Water turns fire and rebukes water, each with its own 3 + CHA modifier uses per day. Rebuke makes creatures cower for 10 rounds; command applies when their HD is at most half the cleric level.
- **Domain slots.** Spells of levels 3-9 are missing for all 22 domains, so domain slots of level 3+ are empty (CHR-023, SPL-041). Domain-only spells reach domain slots only through auto-prepare (SPL-014).
- No domain-power tests exist.

## 7. Gaps and backlog toward PHB fidelity

Ordered by impact on the encounter-and-party game loop. Defects cite their issue IDs; items without an ID are missing content or features rather than filed defects.

1. **Spell progression.** Extend Wizard and Druid slot tables to level 20 and give Clerics known spells for every slot level (SPL-041); enable Ranger and Paladin casting (SPL-039) and Adept casting (CHR-024); use per-class spell levels everywhere (SPL-008). Without this, Wizards, Clerics and Druids never cast a spell above 2nd level (reached at character level 3), and Rangers and Paladins cast nothing.
2. **One rules-correct resolution path.** Single DC function: done (`SpellSaveDCRules`, SPL-001, 2026-10-09); caster level by casting class (SPL-017); damage through `ApplyIncomingDamage`: done for every custom handler and area effect (`GameManager.DealDamage`, SPL-004, 2026-10-09; Insect Plague and DR is an owner question); dice that scale with caster level: done for the generic branch (`SpellDiceRules`, SPL-005, 2026-10-09); unify the four pipelines (SPL-054) and fix the unreachable branches (SPL-037, SPL-042, SPL-046).
3. **Durations.** The legacy `BuffDurationRounds` durations and the never-expiring Silence, Death Knell and Align Weapon flags were fixed on 2026-10-09 (SPL-002, SPL-003: one rule, `SpellDurationRules`; Silence's length is an owner question). Left: the sentinel handling (SPL-059), turn-based expiry (SPL-032) and the Extend offer test (SPL-095).
4. **Metamagic.** Spontaneous metamagic with a Heighten picker (SPL-040); metamagic in custom resolvers (SPL-038); Enlarge as a range multiplier limited to Close/Medium/Long, without area doubling (SPL-010); `CanExtend` aligned with the effect check (SPL-095); Still Spell skipping arcane spell failure (SPL-096) and the Empower + Maximize formula (SPL-097); Quicken limited to spells of at most 1 full round (SPL-098); keep metamagic through `RefreshSpellSlots` and Auto-Prepare (SPL-013); rods wired into casting (ITM-018); NPC metamagic (AI-001).
5. **Database correctness.** Fix the PHB level and class-list deviations in section 1 (Flame Strike, Acid Fog: SPL-099; Plane Shift, Heal, True Seeing, Mislead: SPL-100; Druid and Ranger cures, Druid Dispel Magic at 3: SPL-101; Bard and Cleric list extras: SPL-102); merge Acid Arrow into Melf's Acid Arrow (SPL-103); move the three test spells off the Wizard/Sorcerer list (SPL-104); finish the class availability of the formerly `domain_*` spells (SPL-105); fix `read_magic_wiz` in `BardClass` (CHR-059) and the Blindness/Deafness test (TST-026); unflag Summon Monster I/II (SPL-021) and audit the other placeholder flags (SPL-044).
6. **Missing spells.** Add the "Not in DB" spells from section 1, starting with combat and recovery staples: Fly, Suggestion, Cloudkill, Summon Monster V and Summon Nature's Ally V (the summon tables stop at IV; the Summon Monster V list is empty), Raise Dead, Mass Cure Light Wounds, Righteous Might, Slay Living, Dispel Evil, Animate Dead.
7. **Placeholders with no effect** (43): most are utility or exploration spells. Implement those that matter in a battle grid (Feather Fall, Spider Climb, Darkvision, Endure Elements, Delay Poison, Lesser Restoration, Remove Paralysis, Silent/Minor Image) and leave out-of-combat divinations as flavor; Command and Hypnotic Pattern need real mechanics (SPL-045).
8. **Cleric domains.** Fix Death touch reachability (CHR-060), Travel (CHR-061), the elemental subtype mapping with separate pools (CHR-062), Luck (forced reroll) (CHR-063), War on level-up (CHR-064), Strength as a typed enhancement bonus (CHR-065), and the Flame Strike Good tag (SPL-107); add Protection (a ward hooked into `SavingThrowResolver.ResolveSave`), Animal, Trickery and domain class skills through a `DomainData.ClassSkills` list used by `InitializeSkills`, `RefreshSkillClassFlags` and `GetSkillPointCost` (CHR-023); domain spells for levels 3-9; daily rather than per-combat uses once a rest model exists; a power chooser UI.
9. **Druid spontaneous summoning** (SPL-108): convert a prepared non-domain spell into Summon Nature's Ally of the same level, reusing the cleric conversion path and `SummonMonsterLists`.
10. **Costly components and casting conditions.** Enforce components before the slot is spent (SPL-020); Silence must stop verbal casting in combat: SPL-092 covers NPC casts, and the PC menu and cast pipelines skip the same check (see [architecture/spells.md](../architecture/spells.md#durations-and-ticking)).

# Batch 11: one spell-cast core for PCs and NPCs

> Status (2026-10-15): design approved for building; steps 1 to 9 built (see below). Produced by a read-only design workflow (three mappers, three independent designs, three judges who all chose the strangler design, one synthesis), then amended with seven rules decisions made the same day under the owner's delegation (three-judge panels). PC_NPC_PARITY step 9. Built so far: step 1 (filing, 34b7f2e), steps 3 to 6 (see each step's Built note) and step 2 (the cast-stage side channel, `ScenarioTraceDiff` and the spell routing golden suite, 2026-10-15; its scenario runs are the batch 11 reference, docs/TESTING.md 3.2). Line numbers in step tasks are approximate: anchor on code. **Where any step text below still calls something "pending the owner's ruling" or an "owner question", decisions D1-D7 supersede it: apply the decision in the step named in the decisions table.**

## Summary

Today a spell resolves differently depending on who casts it and where it came from. PC casts go through two big routines: one for single targets, one for areas. Enemy casts go through a third, slimmer routine that skips 32 of the 51 spell effects, refuses every area spell and ignores metamagic. Scrolls, wands, held touch spells, summons and Grease each have their own partial copies. As a result, rules fixes land on one side only, stale PC choices leak into enemy casts, and enemy wizards cannot cast Fireball. This batch replaces all of them with one "cast request" that the spell menu, scrolls, wands, the F12 panel and the enemy AI all fill in the same way. One staged executor runs every rule on that request in a fixed RAW order, and one table holds every spell's special effect. The only remaining PC/NPC difference is who answers the questions: you through the menus, or the AI.

The work is ordered so that nothing changes by accident. The first three commits add measuring tools and pin today's behaviour with scenarios. The next seven move code without changing any fight: the scenario trace of every pre-existing job must stay identical, and any behaviour difference is kept on purpose as a labelled 'legacy quirk'. Eight commits then fix one rule family each: the Globe blocking damage, quickened spells not provoking, the entangled check, refunds, caster level, saves and spell resistance, and metamagic on Fireball and the other custom spells. Each fix applies to every cast path at once and comes with its own explained trace differences. The last six commits let the enemy AI use what is now shared: energy choices, aimed area spells that spare its allies, summons, held touch charges and metamagic. They finish by deleting the old code. Fights will get noticeably harder once enemy casters gain area spells, summons and metamagic. That is the RAW result, and each such step records before and after soak numbers for you. Several questions need your ruling (see the open questions). Until you answer, the plan keeps today's behaviour for them and applies it to both sides.

## Rules decisions (2026-10-15, delegated; the owner may overrule)

Each was decided by three independent readings of the PHB/DMG/MM text and a merge. Record each in `docs/systems/RULES_COVERAGE.md` 4.1 when the step that applies it lands (explicit RAW needs no 4.1 row; its delegated sub-readings do).

| ID | Question | Vote | Kind | Applied in step |
|---|---|---|---|---|
| D1 | Casting while concentrating | 3-0 for (c), explicit RAW | explicit-RAW | `raw-concentration-rules` |
| D2 | Maintaining a concentration spell | 3-0 for Yes: a standard action each round after the casting  | explicit-RAW | `raw-concentration-rules` |
| D3 | Spell components for every caster | all casters, PC, NPC and monster, need components and a pouc | gap-best-reading | `raw-components-all-casters` |
| D4 | Countered hostile spells and invisibility | Merged by majority | gap-best-reading | `raw-counterspell-order-parity` |
| D5 | Area origins at grid intersections | 3-0 unanimous (clear-by-reading, explicit-RAW, explicit-RAW) | explicit-RAW (with two delegated sub-readings: creature-centred origins, and the origin and square count for larger creatures' cones and for grid-line cones) | `raw-area-origin-intersections` |
| D6 | One damage roll per area effect | 3-0 unanimous: roll once per effect, then resolve the outcom | gap-best-reading | `raw-area-damage-one-roll` |
| D7 | Mirror Image | 3-0 on the core: | clear-by-reading | `raw-mirror-image` |

### D1. Casting while concentrating

**Decision.** Option (c). The rules say outright that you cannot cast a spell while concentrating on another one, and no Concentration check lets the caster keep both.

Ceasing concentration is a free action, so casting any new spell first ends the spell being maintained. No roll is made, and the new spell is then cast normally. If the old spell's duration is "concentration + N rounds" (summon swarm, hypnotic pattern, rainbow pattern), it still runs those N rounds.

This holds for every casting time, a quickened (free-action) spell included. The bar is on the state of concentrating, not on how many actions the caster has, so option (b) is not what decides it. Option (b)'s premise is still true and is a separate gap: maintaining costs a standard action each round.

The DC 15 + spell level check in the code has no source in the rules. PCs and NPCs follow the same rule.

**Basis.** **PHB p.176, Duration, "Concentration"**
- Maintaining a spell is a standard action that does not provoke attacks of opportunity.
- Anything that could break concentration while casting can also break it while maintaining, and the spell then ends.
- It states flatly that a caster can't cast a spell while concentrating on another one.
- Some spells last a stated time after concentration ceases.

**PHB p.176, "(D) Dismissible"**
- A concentration spell can be ended at no action cost by ceasing to concentrate on your turn.

**PHB p.139 and p.141 (Table 8-2), p.144 (Cease Concentration on Spell)**
- Ceasing concentration is a free action.

**PHB p.69-70, Concentration skill, and p.170, Concentration under casting**
- Distractions that can break concentration: damage, continuous damage, spells, motion, weather, entangled, grappling.
- Casting defensively is DC 15 + spell level.
- Neither page has an entry for casting another spell while maintaining one. The code's DC 15 + level citing p.170 is a misreading of the casting-defensively or entangled DC.

**Concentration is a state, not just the standard action**
- p.176 lets damage taken while maintaining break the spell, and that damage comes on other creatures' turns.
- p.69 lists concentrating on an active spell among the things a distraction can break.
- PHB p.29 (bardic music) says that "even while" using music that needs no concentration, a bard can't cast spells. The word "even" assumes concentration already bars casting.
- So a quickened spell does not escape the bar. Quicken Spell only lifts the limit of one spell per round.

**Consistency with RULES_COVERAGE 4.1**
- It matches the CHR-026 bardic ruling (a new performance ends the old one, and stopping is free).
- Because this is explicit RAW, it does not need a 4.1 interpretation entry.

**Edge case**
- A spell cast outside the caster's own turn while concentrating (for example feather fall): casting is allowed, and it ends the old spell at that moment, because ceasing is a free action.

**Scope**
- Scrolls count, because spell completion items are treated like spells (DMG p.213).
- Wands and spell-like abilities are not literally "casting a spell". They are a separate open question and are not ruled on here.

**Change needed.** Yes, behaviour changes.

1. **`GameManager.HandleConcentrationOnCasting`** (`F:\DNDPrototype\Assets\Scripts\Spell\Resolution\GameManager.SpellCasting.cs:8883`, called about 18 times there and 3 times in `GameManager_Grease.cs`). Today it does three things:
   - Summon Swarm goes into its tail. This is correct.
   - A new concentration spell ends the old one. This is correct.
   - Otherwise it rolls `ConcentrationManager.CheckConcentrationOnCasting` (`ConcentrationManager.cs:221`) against `ConcentrationService.GetCastingWhileConcentratingDC` (`ConcentrationService.cs:146`, 15 + the new spell's level). On a success the old spell is kept. This third branch is not in the rules.

   Fix: every cast ends the current concentration with no roll. Turn the Summon Swarm tail into a general "concentration + N" tail.
2. **Remove or retire** `CheckConcentrationOnCasting` and `GetCastingWhileConcentratingDC` and its constant, along with `Tests/Services/ConcentrationServiceTests.cs:132` (`TestCastingWhileConcentratingDC`). Fix the doc comments that cite p.170 for this check.
3. **NPC parity.** `TryNPCPerformSpellCast` never calls this gate, so NPCs keep their concentration spell with no check at all (SPL-114). Move the step into a shared pre-cast helper that the NPC pipeline and the scroll pipeline both use.
4. **AI and UI.** The AI should weigh losing its maintained spell before it casts. The UI should warn that casting will end the current spell.
5. **Separate gap, file a new SPL issue.** Maintaining a concentration spell should cost the caster's standard action every round (PHB p.176), for PCs and NPCs alike, using the bardic music `ConcentratedThisTurn` model. Today nothing charges that cost for spells.
6. **Docs.** Update the SPL-114 text and the RULES_COVERAGE Ch.10 row, and note the wand and spell-like ability sub-question as open.

Not verified in Play mode.

**Confidence.** high

**Dissent.** None on the outcome. Reading 3 framed its answer as "(c) with part of (b)": the standard-action cost of maintaining is real but separate, and does not decide this question. All three readings agree on that, and all three mark wands and spell-like abilities as out of scope.

### D2. Maintaining a concentration spell

**Decision.** Yes. All three readings agree, so this is a 3-0 majority and the PHB states the rule outright.

**General rule.** Keeping a Concentration-duration spell going costs a standard action on each of the caster's turns after the turn it was cast. That action does not provoke attacks of opportunity.

**Casting turn.** No maintain action is owed. The casting was that turn's standard action, and the spell takes effect at once.

**When the spell ends.** If the caster's turn ends without a standard action spent concentrating, the spell ends at the end of that turn. That end-of-turn timing is a delegated reading; it matches the bardic-music ruling in RULES_COVERAGE 4.1 (CHR-026).
- A "Concentration + N" spell instead runs its N-round tail, which costs no action.
- Ceasing concentration on purpose is a free action. Dismissing a concentration spell takes no action.
- A spell never lasts past its stated "up to X" maximum.
- Anything that breaks concentration while casting also breaks it while maintaining.
- A target moving out of range counts as broken concentration.
- A caster who cannot take a standard action (for example dazed, stunned or nauseated) cannot maintain the spell.

**Casting another spell.** The caster cannot cast a spell while concentrating on another one. Casting ends the concentration; a "+N" spell then runs its tail.

**PCs and NPCs.** The rule is identical for both.

**Spell-specific PHB texts (printed pages):**
- **Mage Hand (p.249, Concentration):** propelling the object up to 15 ft is a move action on top of the standard action to maintain it. The spell text only sets an action for propelling, so it does not conflict with the general rule and both apply. This is a delegated reading; all three readings chose it.
- **Rainbow Pattern (p.268, Concentration + 1 round/level):** moving the pattern is a free-action gesture, in addition to maintaining it.
- **Silent Image (p.279), Minor Image (p.254, +2 rounds) and Major Image (p.252, +3 rounds):** moving the image has no separate action. It is part of the concentration action. This is a delegated reading.
- **Concentration spells with no special action of their own:**
  - Hypnotic Pattern (p.242, +2 rounds)
  - Summon Swarm (+2 rounds)
  - Wall of Fire (p.298, Concentration + 1 round/level). This is a Concentration spell, contrary to the question.
  - Detect Magic and the other detect spells (p.219 and after; information builds up round by round)
  - Telekinesis, sustained-force and combat-maneuver versions (p.292)
  - Contact Other Plane (p.212, which restates the standard action, one question per round)
- **Not Concentration spells, so they need no upkeep:**
  - Flaming Sphere (p.232) and Spiritual Weapon (p.283): directing or redirecting is a move action with no attack of opportunity and no concentration (p.143 and p.175).
  - Dominate Person and Dominate Animal (p.224): a new command is a move action that redirects the spell.
  - Dancing Lights (p.216): no concentration needed to move the lights.
  - Call Lightning (p.207, 1 min./level): each later bolt costs a standard action, but skipping a round does not end the spell.

**Basis.** Printed pages; the pdf page is the printed page + 1.
- **PHB p.176, Duration: Concentration.** Maintaining is a standard action with no attack of opportunity. What breaks concentration while casting also breaks it while maintaining, and the spell ends. You cannot cast a spell while concentrating on another. A "Concentration + N" spell continues after you stop. A target moving out of range counts as broken concentration.
- **PHB p.176, Dismissible.** A concentration spell ends when you stop concentrating on your turn, at no action. The phrase "on your turn" implies concentrating is something you do each turn.
- **PHB p.140, "Concentrating to Maintain a Spell."** Restates the same rule.
- **PHB p.141, Table 8-2:**
  - "Concentrate to maintain an active spell" is a standard action.
  - "Direct or redirect an active spell" is a move action.
  - "Cease concentration on a spell" is a free action.
- **PHB p.143, Direct or Redirect a Spell** (re-read). Flaming sphere and spiritual weapon are the examples. It is a move action with no attack of opportunity and no concentration. p.175 says the same.
- **PHB p.69-70, Concentration skill.** Concentrating on an active spell and directing a spell are listed as separate activities. The table has no row for casting a spell while concentrating.
- **Mage Hand p.249** (re-read). Duration is Concentration. Propelling the object is a move action.
- **Specific over general.** A spell-specific action is added only where the spell text states one. Mage Hand's move action is added on top of maintaining because the two texts do not conflict, so both get an effect.
- **Delegated readings.** End-of-turn lapse timing, Mage Hand's action being added, and Silent Image movement being folded into the concentration action. The lapse timing matches RULES_COVERAGE 4.1 (CHR-026, bardic music, 2026-10-14).

**Change needed.** Yes, behaviour changes. Today, concentration spells last for free and casters act normally while concentrating. Proposed changes, in one actor-agnostic path that both the PC UI and the AI call:

1. **Per-turn upkeep.** Add a shared "Concentrate on <spell>" standard action, modelled on `BardicMusicRules.Concentrate` / `ConcentratedThisTurn`.
   - Count the casting turn as maintained.
   - At the end of the caster's turn, if the spell was not maintained, end it or move it into its +N tail.
   - Enforce the "up to X" cap.
2. **Casting while concentrating.** Casting any spell, including a quickened one, ends the concentration or starts its tail. Remove the invented DC 15 + spell level check: `ConcentrationService.CASTING_WHILE_CONCENTRATING_DC_BASE` (`Assets/Scripts/Services/ConcentrationService.cs:65`) and its branch in `GameManager.HandleConcentrationOnCasting` (`Assets/Scripts/Spell/Resolution/GameManager.SpellCasting.cs` around line 8750, which cites PHB p.170 wrongly).
3. **Spell-specific actions:**
   - Mage Hand propel: a move action in addition to maintaining.
   - Rainbow Pattern move: a free action.
   - Flaming Sphere, Spiritual Weapon and Dominate commands: a move action, with no upkeep.
   - Call Lightning: a standard action per bolt, and skipping a round does not end the spell.
4. **NPC path.** The NPC cast path needs concentration tracking (SPL-114) so NPCs pay the same upkeep. The AI must weigh maintaining a spell against other uses of its standard action.

Docs:
- Record the ruling in `docs/systems/RULES_COVERAGE.md` 4.1 as explicit RAW, with the three delegated sub-readings.
- File a new SPL issue for the missing upkeep and the invented check.
- Note in the AI tactics docs that Silent Image, Hypnotic Pattern, Wall of Fire and the detect spells cost the caster's standard action every round.

**Confidence.** high (general rule, explicit text stated three times). Medium for the delegated sub-readings: Mage Hand's action being added, Silent Image movement being folded in, and the end-of-turn lapse timing.

**Dissent.** None on the general rule. The only lower-confidence spot was Silent Image movement, flagged by one reading, but the other two readings reached the same conclusion. No reading argued that Mage Hand's move action replaces the standard action. Left open: whether a quickened spell may be cast on a turn spent concentrating. The p.176 bar on casting while concentrating says no, and this decision follows that.

### D3. Spell components for every caster

**Decision.** All three readings agree on the rule, and the PCs and NPCs it covers. PCs, class-level NPCs and spellcasting monsters need their components to cast spells with M, F or DF components. No rule exempts NPCs or monsters.

**What each component needs**
- **Spell component pouch.** It covers every M and F component with no listed cost. It does not cover:
  - a component or focus with a price,
  - a divine focus,
  - a focus too big for a pouch.
- **Divine focus.** A divine cast of a DF spell needs one:
  - a holy symbol for a cleric, paladin or adept (an unholy symbol for an evil cleric),
  - holly or mistletoe for a druid or ranger.
- **Split components.** For an M/DF or F/DF spell, the arcane version uses the M or F and the divine version uses the DF.
- **Monsters casting cleric or domain spells as arcane spells.** This covers the couatl, guardian and spirit nagas, and dragons with cleric-list spells. They need no divine focus and use the arcane M or F instead.
- **Sorcerer-casting monsters** (dragons, couatl, nagas, formian queen, aboleth mage and similar) must supply M and F like anyone else. A creature without hands touches the component or carries it on its body.
- **Eschew Materials is a core PHB feat**, contrary to the question's premise. It waives only M components costing 1 gp or less. It never waives F, DF or a costlier M. Couatl and nagas still need a pouch for their focus spells.
- **Costly components and oversized foci** are always separate real items, for everyone.

**Are NPCs assumed to carry the cheap kit?** The books never say NPCs automatically have it. All three readings agree that the DMG and MM possession lists are abbreviated, so a missing pouch is not an exemption: the DMG 5th-level wizard's printed fireball, web and glitterdust would otherwise be uncastable. So the game should give the kit as real, losable inventory items, the same way it does for PCs.

**What to give each caster** (PC kits and NPC spawn alike)
- **Every class-level caster** (bard, cleric, druid, paladin, ranger once it casts, sorcerer, wizard, adept): a spell component pouch.
- **Divine casters, in addition:**
  - cleric, paladin and adept: a wooden holy symbol, or an unholy one if evil;
  - druid and ranger: holly and mistletoe.
- **Every monster that casts as a sorcerer:** a pouch carried on its body. A divine focus only if it casts divine spells as divine spells. Eschew Materials, where its MM entry lists it, stays as a real feat.
- **Costly components:** only when bought from the NPC's spare gear wealth or found.

The only thing that should block a cheap-component cast is losing the pouch or focus, for example through disarm, sunder, theft, confiscation, or wild shape without Natural Spell. It should block PCs and NPCs the same way.

**Merge note.** The three readings agree on the rule, the Eschew Materials scope, the arcane-DF exemption and the outfitting. They differed only on whether paladins, druids and rangers also get a pouch: reading 1 said all casters get one, reading 2 gave the paladin none, reading 3 gave druids and rangers one only if needed. I chose a pouch for every caster. The pouch is a condition for any no-cost M or F, and these classes have such spells (for example protection from evil, M/DF, when not cast divinely; ranger and druid M spells). Giving everyone the same kit keeps one simple rule and changes nothing for casters who never need it.

**Basis.** I re-checked these pages myself:
- **PHB p.140:** you must have the materials to cast an M, F or DF spell. Components with no listed cost are assumed only if you have your spell component pouch.
- **PHB p.174:** do not track no-cost M components as long as you have the pouch; no-cost foci are in the pouch. The divine focus is a holy or unholy symbol (cleric, paladin) or mistletoe or holly (druid, ranger). For M/DF or F/DF spells, the arcane version uses the M or F.
- **PHB p.94:** Eschew Materials is a core General feat that waives material components only.
- **MM p.315:** spellcasting creatures follow the same rules. A creature without hands "does need material components" and must touch them or carry them on its person. Some take Eschew Materials to avoid fussing with cheap components.

Cited by the readings:
- **PHB:**
  - p.130: what the pouch excludes (priced components, divine foci, foci too big for a pouch).
  - p.98: Natural Spell lets a wild-shaped druid use melded components.
  - Starting kits: bard p.30, sorcerer p.55 and wizard p.58 get a pouch; cleric p.33 and paladin p.46 get a wooden holy symbol; druid p.37 gets holly and mistletoe.
- **DMG:**
  - p.99: a cleric whose holy symbols are confiscated is reduced to a fraction of his power, and Eschew Materials is a wizard's way around such a ban. Possession is enforced for NPCs.
  - p.108: each adept has a holy symbol as divine focus.
  - p.110-112: NPC gear is basic equipment plus extra equipment bought with leftover wealth, chosen by the DM.
  - p.54: a wizard's treasure includes their material components.
  - p.128: run an NPC as a player runs a PC.
  - Sample NPCs, p.114-126: the clerics and paladins list holy symbols, but the bard, sorcerer and wizard list no pouch. The 5th-level wizard lists no spellbook while preparing M spells, so the lists are abbreviated.
- **MM:**
  - p.69: dragons cast as sorcerers, some casting cleric and domain spells as arcane spells.
  - p.37-38: the couatl's cleric spells are arcane, so it needs no divine focus, and it gets Eschew Materials as a bonus feat.
  - The nagas (p.191-193) and the formian queen (p.110) also get Eschew Materials as a bonus feat. A bonus feat that waives components only matters if components are otherwise required. Their focus spells show they must still carry foci.

Reading chosen: the requirement is plainly written for every caster. Who carries what is a gap. I filled it with the DMG rule that the DM outfits NPCs, the PHB starting kits and the sample holy symbols. Every text keeps an effect, and the reading is consistent with RULES_COVERAGE 4.1 (gear melds in wild shape unless Natural Spell applies).

**What the code does today** (unanimous across the readings, cross-checked):
- `SpellComponentRegistry.ValidatePouchRequirement` (`Assets/Scripts/Spell/Casting/SpellComponentSystem.cs:276`) is called only from PC `PerformSpellCast` (`Spell/Resolution/GameManager.SpellCasting.cs:1731-1753`), gated on `HasMaterialComponent`, which is set on about 36-38 spells.
- `PerformAoESpellCast`, the summon and Grease paths through `TryConsumePendingSpellCast`, and `TryNPCPerformSpellCast` skip it (SPL-114).
- No focus check exists; `SpellData.HasDivineFocus` is never read and is set on only 2 spells.
- No holy symbol or holly item exists in `ItemDatabase`.
- PC kits:
  - the cleric, bard, sorcerer and wizard kits add `COMPONENT_SPELL_POUCH`;
  - the cleric kit has no holy symbol;
  - druid, paladin and ranger get nothing.
- NPCs carry no pouch.
- Eschew Materials is an inert feat string on the couatl and naga definitions (`NPCDatabase_C.cs:717`, `NPCDatabase_D.cs:343`, `NPCDatabase_N.cs:109`; CHR-022).
- Only Stoneskin consumes a costly component, and only after the spell slot is spent (SPL-020).

**Change needed.** Yes, behaviour changes. Today NPCs and monsters cast every M, F and DF spell freely. PCs are checked only for a pouch, and only on single-target casts. Nobody ever needs a focus or a divine focus.

1. **One shared component check, before the spell slot is spent.** It is actor-agnostic and called by every cast pipeline, including `TryNPCPerformSpellCast`, AoE, summon and held charge. This fixes SPL-114 and folds in SPL-020. The check:
   - takes the arcane or divine side of an M/DF or F/DF spell from how the caster casts; innate cleric lists count as arcane;
   - needs a pouch for no-cost M (or Eschew Materials, for M of 1 gp or less);
   - needs a pouch for no-cost F (Eschew Materials does not help);
   - needs a divine focus item for divine DF;
   - needs the specific item for a costly or oversized M or F;
   - lets Natural Spell use melded components;
   - leaves scroll and wand casts exempt.
2. **AI spell selection** filters on the same check.
3. **Spell data:** add an F flag, fill in DF for every spell including the M/DF and F/DF splits, and audit the M flags against the PHB.
4. **New items** (PHB p.128-130 prices): wooden holy symbol (1 gp), silver holy symbol (25 gp), unholy symbol, and holly and mistletoe (free). Add them to `ItemDatabase.CloneItem` and `ItemBuilder.CopyBaseProperties`.
5. **PC kits:**
   - every caster class gets a pouch;
   - cleric, paladin and adept get a holy or unholy symbol by alignment;
   - druid and ranger get holly and mistletoe.
6. **NPC spawn:**
   - In the shared setup (`GameManager.NPCSetup`), give the same kit to every class-level NPC caster.
   - Give a pouch to every sorcerer-casting monster, including the ones with Eschew Materials.
   - Clear these items on slot reuse (`ResetCharacterSlotForSpawn` / `ResetForNewCreature`, CRE-046).
7. **Feat:** implement Eschew Materials as a real feat (CHR-022).
8. **Docs:**
   - Record a delegated-RAW row dated 2026-10-15 in `docs/systems/RULES_COVERAGE.md` 4.1.
   - Update SPL-114, SPL-020 and CHR-022.
   - File new issues for the missing focus items and the cleric kit.
   - Mark the work as not verified in Play mode.

**What players will notice:** ordinary fights barely change, because everyone gets the kit. Losing a pouch or symbol now blocks casting for both sides. PC area and summon casts without a pouch, and divine casts without a focus item, become blocked.

**Confidence.** medium-high (the rule and the Eschew Materials scope are explicit text; giving NPCs the gear is a DM-gear reading that all three readings reached independently)

**Dissent.** There was no real dissent. Reading 2 said NPCs are "assumed" to carry the cheap kit, while readings 1 and 3 said the books do not assume it and the gear should be given as items. All three end up giving it as real items, so the result is the same. The readings split on whether paladins, druids and rangers get a pouch; I resolved that in favour of a pouch for every caster.

### D4. Countered hostile spells and invisibility

**Decision.** The answer differs by counter type, and the rule is the same for PCs and NPCs. (1) A readied counterspell (PHB p.170-171) stops the hostile spell, and this includes dispel magic used as a counterspell when its check succeeds. In that case invisibility does NOT end, although the caster still loses the spell or slot. (2) A Ring of Counterspells (DMG p.230) absorbs the hostile spell, and in that case invisibility DOES end. If a counter fails (a failed dispel check, or a failed Spellcraft identification), the spell resolves normally and is an attack. Every cast path should run in this order: readied-counterspell check, and on success return with the caster still invisible; then BreakInvisibilityOnHostileSpellCast; then the Ring of Counterspells check. Invisibility sphere and mass invisibility follow the same rule. Greater invisibility does not end on attacks, so it is not affected.

**Basis.** I re-read the decisive pages. Page numbers are printed; the PDF page is printed + 1.
- PHB p.245, Invisibility: the spell ends if the subject attacks a creature. A spell counts as an attack if it targets a foe, or if its area or effect includes a foe, as the invisible character perceives it. The test is about what the cast spell targeted. It does not depend on the outcome: a miss, a save or spell resistance still ends invisibility.
- PHB p.175, Aiming a Spell: a target need not be chosen until casting is finished.
- PHB p.170: a counterspeller readies an action that waits until the opponent tries to cast. PHB p.160: a readied action interrupts and resolves before the action that triggered it. PHB p.170-171: if the counter succeeds, both spells negate each other with no other results.
Counterspell case: the counter interrupts a casting that has not finished, so no target was chosen and no area was placed. The Invisibility test ("a spell targeting a foe or whose area includes a foe") is never met, which is consistent with "no other results".
Ring case: DMG p.230 (PDF p231) says the ring acts only when the stored spell "is cast upon the wearer" and counters it at once, needing no action or knowledge from the wearer. The casting has therefore finished with the wearer chosen as target. If the wearer is a foe, the PHB p.245 test is already met. "As a counterspell action" describes how the spell is negated, not when. Ending invisibility follows from the caster's own targeting, not from the negated spell, so "no other results" does not stop it.
This split gives every text an effect and follows the specific trigger wording of each source. It matches how attacks work: an attack that is negated after it is made still counts, and one that is stopped before it is made does not. I found no owner ruling on invisibility or counterspells in RULES_COVERAGE 4.1 that conflicts.
Code today (checked by grep):
- PC single-target path, GameManager.SpellCasting.cs: TryResolveCounterspell (line 1989), then TryRingCounterspell (line 2016), then BreakInvisibilityOnHostileSpellCast (line 2038). The counterspell case is right. A ring absorb wrongly leaves the caster invisible.
- PC area path, same file: counterspell check (line 3724), then the break (line 3746). Correct. The ring does not apply to area spells.
- NPC path, GameManager.NPCTurns.cs: the break (line 1343) runs before TryResolveCounterspell (line 1346) and TryRingCounterspell (line 1355). A readied counterspell wrongly ends the NPC's invisibility. The ring case is right.
- The break is defined at GameManager.cs:3206.
- The NPC order is already filed as SPL-118 (docs/issues/SPL.md:51; PC_NPC_PARITY.md:122,141). Its fix text says only "counterspell before the invisibility break". If the ring check also moved ahead of the break, that would be wrong.
Play does not change today. RULES_COVERAGE records that readied counterspells cannot be reached (SPL-047) and that the ring never holds a spell (ITM-020).

**Change needed.** The code should change, but nothing changes in play until SPL-047 or ITM-020 is fixed.
- NPC path (F:/DNDPrototype/Assets/Scripts/_Core/GameManager.NPCTurns.cs): move BreakInvisibilityOnHostileSpellCast from line 1343 to after the early return on a successful TryResolveCounterspell, and before TryRingCounterspell.
- PC single-target path (F:/DNDPrototype/Assets/Scripts/Spell/Resolution/GameManager.SpellCasting.cs): move the break from line 2038 up to between the counterspell check (line 1989) and the ring check (line 2016).
- PC area path: no change needed.
- Better still, put the "counterspell, then break, then ring" order in one actor-agnostic helper that both pipelines call. Check that TryConsumePendingSpellCast and the held-charge delivery path follow the same order.
- Docs: update the SPL-118 fix text and PC_NPC_PARITY.md step 141 to the three-step order, and record the PC ring-order bug there. Record the ruling as a delegated RAW reading in RULES_COVERAGE.md 4.1.
- Tests: once counterspelling can be reached, add tests that an invisible caster stays invisible when its spell is counterspelled and loses invisibility when a ring absorbs it, for both a PC and an NPC.
- Separate open question: PHB p.175 requires the counterspeller to see its target. TryResolveCounterspell has not been checked for this when the caster is invisible.

**Confidence.** medium

**Dissent.** Reading 1 held that invisibility stays in the ring case too. It treated "as a counterspell action" as bringing the PHB p.170 "no other results" rule over to the ring, and read Concentration's "cast to no effect" as a parallel. I rejected this. The ring's own trigger, "cast upon the wearer", means targeting has already finished, and the Invisibility test depends on the caster choosing a foe as target, not on whether the spell takes effect. Readings 2 and 3, and my re-read of DMG p.230 and PHB p.175, agree on this. Reading 1's point that the PC ring path already keeps the caster invisible describes the current code, not RAW.

### D5. Area origins at grid intersections

**Decision.** All three readings agree (3-0), so this is a majority without a re-read. I also checked the decisive PHB p.175 text myself (pdf p176), and it confirms them. The rule applies identically to PCs and NPCs.

(1) Point of origin. Every area spell's point of origin is a grid intersection, never a square. This covers bursts, emanations, spreads, spheres and cylinders. The caster picks the intersection, which must be within range and in line of effect.

(2) Who is in the area. Count from the origin to other intersections, the same way as movement: diagonals alternate 1 and 2 squares. A square is in the area when its far edge is within the radius; touching only its near edge does not count. Equivalently, the count from the origin to the square's far corner is at most the radius.
- Every footprint is even-width, with four identical quadrants.
- 5 ft. = 2x2 = 4 squares; 10 ft. = 12; 15 ft. = 24; 20 ft. = 44 (8 squares across); 30 ft. = 96; 40 ft. = 172.
- Spreads count their distance around walls.

(3) Areas centred on you or on a creature (delegated gap; the majority reading). The origin is still an intersection: the one at, or nearest, the centre of that creature's space.
- Small or Medium: any of the four corners of its square, chosen by the caster.
- Large (2x2): the centre intersection.
- Huge (3x3): one of the four corners of the central square.
- The creature's own space is always inside the area.

(4) Cones and lines start at any corner of the caster's square, in a direction the caster chooses.
- For a creature bigger than one square (delegated, by analogy with MM p.69 dragon breath and tail sweep): any intersection on, or adjacent to, the edge of its space.
- A cone is a quarter circle measured with the same counting. A diagonal cone is exactly one quadrant of the burst: 3, 6, 11, 24, 43 and 96 squares at 10, 15, 20, 30, 40 and 60 ft.
- A cone aimed along a grid line is symmetric about the grid line through its starting corner. Per DMG p.29 it covers the same number of squares as the diagonal cone (for example 6 at 15 ft., 24 at 30 ft., 96 at 60 ft.). Its exact cell pattern is not fixed by text: check it against the DMG p.305-307 template images before encoding it.

(5) Line of effect for an area is traced from the origin intersection: a burst's centre point, a cone's starting point, an emanation's point of origin (PHB p.176).

(6) Where the PHB far-edge count and the DMG "most of the square inside the circle" guideline differ on edge squares, the PHB count governs.

**Basis.** PHB p.175, Aiming a Spell (re-read here, pdf p176):
- Area: you choose where the spell originates, and the point of origin of a spell is always a grid intersection.
- Count distance from the origin as for movement or ranged attacks, but intersection to intersection rather than centre to centre. Diagonals alternate as usual.
- A square counts if the area reaches its far edge; touching only its near edge does not count.
- Spread: its point of origin must be a grid intersection, and distance is counted around corners.
- Burst, emanation and spread all measure from the chosen point of origin.
- Cone: a quarter circle starting from any corner of your square. Line: also starts from any corner of your square.

PHB p.176: line of effect for an area is traced from its origin (a spherical burst's centre point, a cone's starting point, an emanation's point of origin).

DMG p.28-29, Area Spells:
- A burst or emanation is centred on a chosen intersection of grid lines; a cone has a declared direction and a starting intersection.
- The section calls itself guidelines (its majority-of-the-square test), refers to the templates on DMG p.305-307, and asks that a turned (diagonal) area keep the same number of squares.

MM p.69 (dragons): breath starts at any intersection adjacent to the dragon, and a tail sweep starts from an intersection on the edge of its space. This is the basis for edge-intersection origins for creatures bigger than one square.

PHB p.304-305 has no area figures. The DMG p.305-307 templates are images that could not be read from the text extraction.

Choices among readings:
- Specific beats general, and the PHB rule text beats the DMG guideline on which squares count.
- "Centered on you" names no square, so "always an intersection" still governs; the nearest-centre intersection gives every text an effect.
- None of this conflicts with any owner ruling in RULES_COVERAGE 4.1, which has no area entry.

**Change needed.** Yes. This changes behaviour on both the PC and NPC paths.

Code today, in F:/DNDPrototype/Assets/Scripts/Spell/AreaEffects/AoESystem.cs:
- GetBurstCells centres the burst on a square: 21, 37, 61, 121 and 205 squares at 10, 15, 20, 30 and 40 ft., against RAW 12, 24, 44, 96 and 172. Per the readings, a 5-ft. burst is a 3x3 (9 squares) or a 5-square plus shape, instead of 2x2.
- GetConeCells starts at the caster's cell. Diagonal cones already match the RAW quadrant. Cardinal cones use an invented 1-3-3 pattern (7 squares at 15 ft.) or expand-then-shrink widths (26 squares at 30 ft., 98 at 60 ft.).
- The cone comment is stale and cites the wrong pages (PHB p.304 / DMG p.69).
- GetLineCellsToTarget traces lines from the centre of the caster's square.

Callers:
- PC: GameManager.SpellCasting.cs about lines 2480, 2932 and 3336. PerformAoESpellCast traces line of effect from the area's average square (about lines 3547-3560).
- NPC: SpellcasterAIProfile.cs 199/201/205, DragonAIProfile.cs 520 and GameManager.NPCTurns.cs 1526/2008 (breath starts at npc.GridPosition; line 1526 hard-codes a length of 2).
- Emanations in EffectService are cell-based.

Needed:
1. One shared intersection-origin geometry helper used by the PC preview, PC cast, NPC cast, breath weapons and emanations.
2. Membership by the far-corner 1-2-1 count.
3. The PC UI snaps to the nearest intersection and offers corner choices for cones.
4. The AI scores candidate intersections (at least the 4 corners of each target's square) and the cone's corner and tilt, the same choices the PC has.
5. Creature-centred origins use the nearest-centre rule.
6. Cones and lines start at a corner of the caster's square (an edge intersection for larger creatures). Cardinal cones match the diagonal square count, with the pattern checked against the DMG p.307 template.
7. Line of effect is traced from the origin.
8. Update tests and scenarios that assume the current footprints.

Docs:
- File a new SPL or GRID issue; none exists today.
- Update the Ch.10 row of docs/systems/RULES_COVERAGE.md and add a 4.1 entry for the delegated sub-readings.
- Update the spells/grid architecture docs.

Not verified in Play mode.

**Confidence.** High on the core rule: intersection origins, the far-edge count, cones and lines from a corner, and line of effect from the origin. Medium on the delegated details: the creature-centred intersection choice, origins for larger creatures, and the exact cell pattern of grid-line cones, because the DMG p.305-307 template images could not be read.

**Dissent.** None on the core rule. A minor variance: reading 3 lets a larger creature use an intersection inside its space, as well as one on its edge, for a creature-centred origin. Reading 3 also did not commit to the same square count for grid-line cones as for diagonal ones. Readings 1 and 2 both did, citing DMG p.29, and that is adopted.

### D6. One damage roll per area effect

**Decision.** Roll the damage once each time an area damage effect is released (one Fireball detonation, one Lightning Bolt, one Cone of Cold, one breath, one acid spray). Every creature, and every unattended object, in the area takes that same rolled total. Each creature then resolves its own part against that shared number: spell resistance, its own saving throw (half on a success, rounded down), Evasion or Improved Evasion, immunity, resistance and vulnerability, DR where it applies, and the item check on a natural 1. Empower and Maximize change that one roll, so they apply once per casting. Effects that are separate by their own text keep separate rolls: each Magic Missile missile, trap darts rolled "per target", and Chain Lightning, whose secondary bolts take half of the primary's single total. The same rule covers PCs and NPCs, and spells, scrolls, wands, spell-like abilities, breath weapons and other supernatural area damage. Lower confidence: when an effect harms creatures at different times (a wall passed through, a cloud each round), each separate damage instance gets its own roll, and creatures hit by the same instance share it.

**Basis.** The books never say in so many words whether area damage is rolled once or per creature. All three readings searched the PHB, DMG and MM for roll-once and roll-separately wording and found none, so this is a delegated best reading. Every text that touches the question treats an area effect as one damage quantity:
(1) Fireball (PHB p.231): one explosion deals 1d6 per caster level to every creature in the area, and unattended objects "also take this damage", which is the same quantity. Lightning Bolt (PHB p.248) is worded the same way.
(2) Chain Lightning (PHB p.208): each secondary bolt deals half as much damage as the primary bolt. That only makes sense if the primary has a single total; it does not roll again.
(3) Iron golem (MM p.137): the golem heals 6 hit points when a fireball's damage total is 18. A fireball has one total.
(4) Items Surviving after a Saving Throw (PHB p.177): an item that fails its save takes whatever damage the attack deals, which is one figure per attack. DMG p.303-304 (catching on fire) agrees: burning items take the same damage as the character.
(5) DMG p.22 surprise example: the characters make their saves (plural), then the spell's damage (singular) is assessed. Spell Turning (PHB p.282) also splits one amount of "the damage".
(6) Where the books want a separate roll per target they say so: trap darts "per target" (DMG), and one 1d4+1 per Magic Missile missile (the Empower example on PHB p.93).
The books do make SR (PHB p.177), the save ("Half", PHB p.177) and Evasion (PHB p.50) explicitly per creature, and that stays per creature under this reading. So every text has an effect. DMG p.36 (a multiple-target spell damages its creatures simultaneously) fits this reading but does not decide it. The DMG p.36 Spell Roll variant (the save DC is rolled once for many creatures) is optional and only an analogy, so it carries no weight. The alternative reading is that "XdY to every creature" means a fresh roll for each creature, perhaps citing PHB p.176's "Creatures" areas acting like targeted spells. But that sentence covers the Creatures area subtype, not spreads or lines, and the per-creature reading leaves Chain Lightning's "half as much" and the golem's "damage total" without a referent. Nothing in RULES_COVERAGE.md 4.1 conflicts with this. Printed pages are cited; the pdf page is the printed page + 1.

**Change needed.** Yes, this changes behaviour. Average damage stays the same, but all creatures in one area now take the same pre-save number, so their results move together, and the log shows one damage roll per cast. What the code does today, checked by grep and by reading the source, not verified in Play mode:
- `GameManager.TryResolveScaledAoEDamageSpell` (Assets/Scripts/Spell/Resolution/GameManager_Spells_Shared.cs, lines 250-253), which handles Fireball, Lightning Bolt and Call Lightning and is called from GameManager.SpellCasting.cs:3882, rolls the d6s inside the per-target loop, after the SR check. Wall of Ice sections get a separate roll (about lines 305-308). Fix: roll once before the loop, halve per creature on a save, and give objects the same total.
- The Cone of Cold handler (GameManager_Spells_Phase1.cs, around line 73) also rolls inside its target loop. Fix: the same as above.
- The generic area branch of `PerformAoESpellCast` (GameManager.SpellCasting.cs, about lines 4672-4737) calls `SpellCaster.Cast` once per target. `Cast` rolls `SpellDiceRules.Roll` (SpellCaster.cs:509) and applies Empower on every call. Fix: roll the dice once per cast, with Maximize and Empower, and pass the pre-rolled total into `Cast` (for example as an optional parameter) so that `Cast` only resolves SR, the save, Evasion and mitigation for each target.
- `TryNPCUseBombardierAcidSpray` (Assets/Scripts/_Core/GameManager.NPCTurns.cs, about lines 1553-1556) rolls 6d4 for each victim. Fix: roll once outside the loop. Separately, its dice labels are mislabelled "Fireball damage die" and "Fireball Reflex save", and its save DC is hard-coded at 12; check that against the MM.
- Already correct: `NPCExecuteBreathWeaponForAI` (NPCTurns.cs:2060-2062) rolls `totalDamage` once. Chain Lightning (Phase1.cs:127-131) rolls the primary once and halves it for each secondary. Magic Missile rolls each missile separately.
- `TryNPCPerformSpellCast` refuses area spells (AI-001), so the future shared NPC area-cast path must use the same roll-once code.
Also: audit the other bespoke area handlers (Sunburst in Phase2 and others) with a grep for `DiceRoller`/`DiceService`/`SpellCaster.Cast` inside per-target loops. Add a static test showing that two creatures in one Fireball that both fail the save take identical damage, and that a creature that saves takes half of that same roll. Review the scenario assertions in Tests/Scenarios/Catalog/RulesScenarios.cs near line 7456, which reflects on `TryResolveScaledAoEDamageSpell` (line 7561), and any tests that count damage-die draws per target. Record this as a delegated RAW reading, dated, in docs/systems/RULES_COVERAGE.md 4.1, file it under the next free SPL ID, and label it in code comments.

**Confidence.** medium-high on the core rule: the vote was unanimous and several texts (Fireball, Chain Lightning, iron golem, item saves) only make sense with one shared total, though no sentence states the rule outright. Low-medium on the extension to effects that deal damage at different times (walls, clouds).

**Dissent.** None on the decision. The readings differed only in scope. Reading 3 adds the extension for damage dealt at different times (lower confidence). Each reading listed a different subset of code sites; together they cover TryResolveScaledAoEDamageSpell, Cone of Cold, the generic PerformAoESpellCast/SpellCaster.Cast branch and the bombardier spray, and I checked the first and the last two myself. All three noted the per-target alternative (PHB p.176 Creatures areas acting like targeted spells) and rejected it.

### D7. Mirror Image

**Decision.** Resolve Mirror Image the RAW way: a count of images on the caster and a random figure pick for each attack. Remove the end-of-turn swap. The model is identical for PC and NPC casters and attackers.

(1) Images. 1d4 + 1 per three caster levels, maximum 8 in total. They are figments in a cluster, each within 5 ft of another figment or the caster, and they move with the caster. They are unreal: they occupy no squares, block nothing, threaten nothing, and are never destroyed for losing formation.

(2) Attacks. Each attack an enemy makes against the caster picks a figure uniformly at random from the remaining images plus the caster, so the caster is chosen with chance 1/(N+1). This covers each attack of a full attack, each AoO, each natural or off-hand attack, and each touch or ray attack.
- Caster chosen: the attack resolves normally against the caster.
- Image chosen: the attack roll is made against 10 + the caster's size modifier + the caster's Dex modifier. This is not touch AC, so deflection, dodge and insight do not count. A hit destroys that image and deals no damage; a miss changes nothing.

(3) Spells cast at the caster. An enemy's targeted spell uses the same random pick for each target or missile.
- With an attack roll (ray, touch): it destroys the image only on a hit against the image AC.
- Without an attack roll (magic missile, hold person and the like): the spell is wasted on the image and the image is destroyed. Majority 2-1, medium confidence.
Allies' spells reach the caster directly.

(4) Area spells. Images are never destroyed or revealed; they only seem to be burned or killed. The caster is affected normally where it really stands.

(5) No effect when:
- the caster is invisible;
- the attacker cannot see, because it is blinded, has its eyes shut, is in darkness without darkvision, or has no eyes at all;
- the attacker has true seeing.
Such attacks go straight to the caster, with the normal invisibility or blindness rules.

(6) Learning and re-confounding (majority 2-1, delegated reading).
- An enemy learns which figure is real when it sees an attack or spell strike a figure that does not vanish. From then on it attacks the caster directly, with no random pick.
- Whenever the caster moves at least one square of its own movement (a move, 5-foot step, withdraw, charge and so on), that knowledge is cleared for every enemy, at no action cost.

(7) Blindsight, blindsense and tremorsense (split, decided here). These senses do not defeat the spell by themselves. A creature that can see is fooled. One that cannot see (blind or eyeless, such as a grimlock) is not fooled under the spell's own "must be able to see the images" rule, and fights through its blindsight normally.

(8) The swap model (SPL-117) is a deviation to remove, together with the clone tokens and the nearest-figure AI override. The text ties re-confounding to the caster's movement. A free end-of-turn swap without movement, offered only to controllable casters, has no textual basis. Under a per-attack random pick, position carries no information anyway.

**Basis.** PHB p.254, Mirror Image, read directly:
- Range Personal, Target You, no save or SR line.
- 1d4 images plus 1 per three caster levels, maximum eight in total.
- The figments stay in a cluster, each within 5 ft of another figment or the caster. The caster can move into and through them, and they move through each other.
- Once separated, vision and hearing cannot tell which figure is the caster. The figments mimic the caster's actions.
- Enemies trying to attack the caster or cast spells at it must pick among indistinguishable targets, and "generally" a random roll decides whether the pick is real.
- Any successful attack on an image destroys it. Image AC is 10 + size modifier + Dex modifier.
- Figments seem to react normally to area spells, for example appearing burned or dead.
- While moving, the caster can merge with and split off from figments, so enemies who have learned which one is real are confounded again.
- An attacker must be able to see the images to be fooled. If the caster is invisible or the attacker shuts its eyes, the spell has no effect, and being unable to see carries the blindness penalties.
- The opening line says the figments disappear when struck.

Supporting texts:
- PHB p.175 (Target): you must specifically choose a target, and may have to guess. This supports a random pick for spells and a spell spent on a figment.
- PHB p.173 (Figment): figments are unreal and produce no real effects. So images occupy no squares and do not absorb area effects.
- PHB p.296 (true seeing): the subject sees through illusions.

How the split points were decided:
- (a) "Attack you or cast spells at you" share one selection rule, and "disappear when struck" gives the strike its effect. So a spell landing on an image destroys it (2 of 3 readings).
- (b) "Enemies who have learned which image is real" needs a learned state to have any effect. The visible proof is a strike that does not dispel the figure, and the merge/split clause ties the reset to movement (2 of 3 readings).
- (c) MM p.306: blindsight lets a creature fight as well as a sighted one, and makes invisibility and darkness irrelevant. It says nothing about illusions. The spell exempts only those who cannot see. So the decisive test is "can it see the images": a sighted creature with blindsight still sees them and is fooled, and a creature that cannot see is not.

Specific beats general: the spell's own image AC line overrides both touch AC and the general figment AC on p.173. RULES_COVERAGE 4.1 has no Mirror Image ruling, so there is no conflict. The delegated readings (a), (b) and (c) should be recorded there as owner-delegated interpretations.

**Change needed.** Yes. Behaviour changes for both sides. Today's code, which I spot-checked by grep, matches all three readings. It lives in Assets/Scripts/Spell/Special/GameManager_MirrorImage.cs:
- Images are CharacterController clone tokens that occupy and block squares.
- The count is `Random.Range(2,6)` (line 75): it ignores caster level and the maximum of 8, and blocked cells reduce it further.
- The clone's AC is the caster's TouchArmorClass (line 159, 228).
- There is no random pick. An attack on the caster's own token is always real. Clone hits go through TryHandleMirrorImageCloneAttacked (line 214).
- Any targeted spell on a clone dissipates it with no ray or touch roll (TryHandleMirrorImageSpellTargetAttack, line 278).
- Clones keep their offsets when the caster moves (FollowMirrorImageCasterMovement, line 615).
- The swap is offered only when IsControllable (line 417/422, SPL-117).
- The AI attacks the nearest figure map-wide through GetMirrorImagePriorityTargetForAI (line 799, AI-036).
- No exemption exists for an invisible caster, a blind attacker or true seeing.

Change:
- Replace the clone tokens with per-caster state:
  - an image count of 1d4 + floor(CL/3), maximum 8;
  - the set of enemies that know which figure is real.
- Clear this state in ResetForNewCreature and ResetCharacterSlotForSpawn (CRE-046).
- Add one actor-agnostic resolver, called from the shared weapon-attack path (PerformSingleAttackWithCrit, AoOs) and from every cast pipeline's target selection.
  - It skips everything for an invisible caster, an attacker that cannot see, true seeing, a knowing enemy, or an allied caster.
  - Otherwise it rolls 1d(N+1).
  - On an image: an attack roll goes against 10 + size + current Dex, and a hit destroys one image with no damage. A no-roll spell is spent and destroys the image.
- Area spells: no change to images.
- Learning: a strike on the real caster seen by an enemy marks it as knowing. The caster's own movement of one square or more clears every enemy's knowledge.
- Delete:
  - the swap: TryBeginMirrorImageSwapSelection, HandleMirrorImageSwapCellClick, CompleteMirrorImageSwapSelection, `_isSelectingMirrorImageSwap` and its hooks in GameManager.CombatActions.cs and GameManager.cs;
  - the clone spawn and follow code;
  - GetMirrorImagePriorityTargetForAI. The AI then targets the caster normally and may value area spells or true seeing.

Bookkeeping:
- Remove SPL-117 and update AI-036.
- Rewrite MirrorImageRulesTests.
- Update the SpellDatabase_M description, the ITM-080 potion branch, docs/architecture/spells.md, PC_NPC_PARITY.md and KNOWN_ISSUES.md.
- Record readings (a), (b) and (c) in RULES_COVERAGE.md 4.1 as owner-delegated interpretations.
- Not verified in Play mode.

**Confidence.** High on the core model and on removing the swap. Medium on (a), whether a no-roll spell destroys the image, and on (b), the trigger for learning. Medium-low on (c), blindsight.

**Dissent.** Reading 2 says a targeted spell with no attack roll that lands on an image is wasted and the image survives. Its grounds:
- PHB p.306 says an attack's outcome is decided by an attack roll, so only a hitting roll is a "successful attack".
- PHB p.171 says a spell on the wrong sort of target is wasted.
Reading 1 would not track learned knowledge at all, because a fresh random pick on every attack makes the merge clause automatic. That is simpler, but it gives "enemies who have learned" no effect.
On blindsight, reading 2 says blindsight, blindsense and tremorsense are never fooled, because the spell names only vision and hearing as fooled. Reading 3 would leave the question to the owner.

The F12 test panel keeps all its bypasses (no arcane spell failure, no pouch, free costly components, unlimited slots) as explicit test-panel flags on the cast request: it is a developer tool, not part of play.

## Architecture

## End state

**Every cast is a `SpellCastRequest`.** The sources are the spell menu, cleric conversion, scrolls, wands, F12, held-charge hold and delivery, summon, swarm, Grease, the NPC AI and the charmed heal. Each one builds a request and hands it to one executor, `GameManager.ExecuteCast(req, Action<CastOutcome> done)`. Rules code reads only the request and its per-cast `CastContext`, never the GameManager `_pending*` bag and never `IsControllable`.

### Types (new; C# 9; global namespace)

These live in `Assets/Scripts/Spell/Casting/Core/`; partials are in `Spell/Resolution/`, and the AI types in `AI/Spellcasting/`.

- `CastSource`:
  - `Kind`: PreparedSlot, SpontaneousSlot, SpontaneousConversion, Scroll, Wand, HeldChargeHold, HeldChargeDelivery or TestPanel.
  - `Item`, `ItemIndex`, the item's caster level and save DC, `ConversionLevel` and `SacrificedSpellId`. These replace the `CombatUI.IsSpontaneousCast` reads.
  - `[Flags] TestPanelBypass Bypass` (Components, ArcaneFailure, Slots), so every F12 privilege is explicit.
- `CastChoices`: every RAW caster choice. Energy type, Fire Shield warm or chill, Disguise Self race, target weapon and Keen Edge ammo, rope item, Blindness or Deafness mode, destination, wall mode and endpoints, Grease mode, summon option, list level and count.
- `CastAim`: `Target`, `Cells`, `AreaTargets` (after line-of-effect filtering) and `Origin`.
- `SpellCastRequest`:
  - `Caster`.
  - `Spell`: always a clone, with `MetamagicDataRef` stamped.
  - `Metamagic`, `Source`, `Aim`, `Choices` and `ICastDecider Decider`.
  - `LegacyCastQuirk Quirks`: a temporary `[Flags]` enum, see "Migration rule" below.
- `CastContext`, one per cast:
  - `Request`, `Result`, `EffectNegatedBySave`, `Snapshot`, `CasterLevel`, `SaveDC` and a `LogPrefix` built once.
  - Helpers `IsBlockedByGlobe(t)` (effective, heightened level), `TargetForgoesSave(t)`, `RollSpellDice(dice)` (metamagic-aware), `RollSave(t, save)` (SaveRules plus the spell situational bonus) and `RollSpellResistance(t)` (one rule with Spell Penetration).
  - `SpellcastResourceSnapshot` is today a private nested class (GameManager.cs:10764). It moves to an `internal sealed class CastResourceSnapshot` so the context can hold it, and it gains the scroll stack and both wand charge stores (ITM-034, ITM-084).
- `ICastDecider`, the only PC/NPC difference:
  - `FillChoices`, `CanFill(choice)`, `ChooseProvocation` (Defensive, Proceed or Cancel), `ChooseTouchDelivery`, `ForgoSave(ctx, self)`, `ChooseMirrorImageSwap` and `IsSynchronous`.
  - `UiCastDecider` wraps the existing dialogs and the AoO prompt.
  - `AiCastDecider` (AI/Spellcasting) answers inline.
- `CasterLevelRules.For(req)` and `RangeSquares(req)` give one caster-level and range rule (SPL-017).
- `CastStageTrace.Mark(ctx, stage, detail)`: a single null check on `ScenarioHooks.CastStage`, inert outside the harness.

### Executor (`GameManager.CastCore.cs`)

One fixed, RAW-ordered stage list runs for every cast:

1. Legality (no cost): sphere, grapple or pin with casting time and V/S, Silence for V, components keyed on Source, the quicken once-per-round limit, metamagic validity, the target, range and LoS/LoE from `CasterLevelRules`. `CheckLegality(req)` has no side effects and is shared by the PC highlight, the AI planner and the executor.
2. Cost: the action from the request (standard; full round for spontaneous metamagic; free or swift for quickened; the item action; none for a held-charge delivery in the round of casting, the attack action (or one attack of a full attack) for a later one: delegated RAW reading 2026-10-14, SPL-156).
3. Snapshot and Spend.
4. Distraction: entangled and grappled Concentration checks.
5. Failure: deafened 20% once per cast, ASF for somatic spells, Blink 20%.
6. Provocation through the decider, skipped for quickened casts and held delivery. Cancel restores the whole snapshot.
7. The concentration-on-cast gate, after the cancel point.
8. Counterspell, then the Ring of Counterspells, then the hostile-cast invisibility break.
9. Resolve, one of:
   - Single: Mirror Image redirect, target Blink, the Globe ward before any damage, `SpellCaster.Cast`, the single-target registry, then ApplySpellBuff.
   - Area: the area registry first-match, else the generic loop.
   - Summon.
   - HoldCharge.
10. PostCast: concentration tracking, held charge kept on a miss, OnDeath per kill.
11. CombatEnd: `EvaluateCombatEnd`, always.
12. `FinishCast`: one log line with the prefixes, `EndCast`.

The executor is continuation-based. The UI decider can suspend it at the AoO prompt and the choice dialogs; Wall of Ice and Wall of Force resume via callback. `RunCastSync(req, out outcome)` is the AI wrapper: it returns a bool exactly as `TryNPCPerformSpellCast` does today, and it raises an editor violation if the decider went async. UI pacing (`CurrentSubPhase`, highlights, `ShowActionChoices`, `AfterAttackDelay`) happens only in the Ui finish continuation, never inside stages.

### Handlers (`GameManager.SpellHandlerRegistry.cs`)

- `SpellHandlerRegistry.SingleTarget` holds 51 entries:
  - the 50 `TryResolve*SpellEffect` methods plus `TryResolveSoundBurstStunEffect`;
  - each entry has `SpellIds` (one id per entry as built in step 5: Lesser Globe claims only `LESSER_GLOBE_OF_INVULNERABILITY`; Globe of Invulnerability is an area handler), a `Gate` (None, Success or SuccessNotNegated) and `ContinueToBuff` (Shield Other, Sound Burst stun);
  - each entry is a lambda over the unchanged method.
- `SpellHandlerRegistry.Area` holds the ~40 area handlers plus Hypnotism, Sleep, Deep Slumber, Color Spray and Fear, with an `Async` flag. The generic per-target loop is the fallback.
- A reflection test keeps the registry complete and free of overlaps.

### Cast stack

`BeginCast(req)` pushes a `CastContext` on a stack and `EndCast` pops it. This replaces a global swap, so a synchronous AI cast nests cleanly; in the editor a mismatched pop is a harness violation. During migration the old `_pending*` names are private forwarding properties onto the top context, so ~600 `_pendingSpell` sites keep compiling. Reflection pins move to `Harness_*` accessors (TST-006).

### PC and AI wrappers

- UI builds a request with `SpellCastRequest.FromPending` at spell selection, item use, F12 or the touch prompt.
- The AI builds one with `SpellCastRequest.ForAI` from `AiCastPlanner` (spell x metamagic x aim), filtered by `CheckLegality`, with the top 5 tried in order.
- `IsControllable` is read only where an entry point picks the decider.

### Migration rule

- Structural commits are pure moves. Every pre-existing scenario trace hash must stay identical, and any old PC/NPC or per-path difference survives only as a named `LegacyCastQuirk` bit. Quirk bits are set by request builders, read only in stage methods, and each one cites an issue ID.
- RAW-fix commits each remove a family of quirks on every path at once. Each changed hash must be explained by `ScenarioTraceDiff` first divergence.

## Steps

One commit each, run by the implement, two reviewers, repair and commit loop. A full regression runs after the pure-move steps and at the end.

### 1. File the unfiled cast-pipeline defects and refresh stale issue evidence (`file-cast-pipeline-issues`)

This is a documentation-only commit: no C# changes. Branch docs/cleanup-2026-10.

Goal: file every defect found while mapping the spell cast paths, so later batch-11 steps can close them by ID.

Before filing each finding:
- Verify it in the code at HEAD. Line numbers are approximate; grep the member names.
- Verify the rule in the PHB or DMG PDF. See the memory note 'core-rulebook-pdfs': search with pdftotext; PDF page = printed page + 1. Summarise only.
- If a finding does not hold, do not file it, and say so in the commit body.

Numbering: use the next free IDs from the 'Entries per section' table in docs/KNOWN_ISSUES.md (SPL is expected to start at SPL-141). Bump Next ID and the counts, and add any High entries to Top issues.

Format: follow the entry format in docs/issues/SPL.md ('- **SPL-NNN** · **Sev** · Player-visible — **Title.** text *Evidence:* ... *Fix:* ...').

Findings to file:
(a) Globe of Invulnerability timing:
  - PerformSpellCast (Assets/Scripts/Spell/Resolution/GameManager.SpellCasting.cs) tests LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell (~:2163-2173) only after SpellCaster.Cast (~:2140) has already applied damage and healing. The NPC path has the same order (Assets/Scripts/_Core/GameManager.NPCTurns.cs ~:1439-1448).
  - The custom AoE handlers in PerformAoESpellCast never check the Globe. The only call sites are ~:2124, ~:4680 and NPCTurns ~:1406.
  - DoesAnyGlobeBlockSpell compares the base SpellLevel, not the heightened level.
  - Rules: PHB p.246 (lesser globe) and p.236 (globe); Heighten, PHB p.95.
(b) Failed casts still land effects:
  - The Sound Burst stun guard (~:2230) and TryResolveSoundBurstStunEffect ignore result.Success.
  - TryResolveAnimateRopeSpellEffect (_Core/GameManager.cs ~:9940-9968) entangles when Success=false and no attack roll was made (deafened failure, Globe).
(c) Quickened casts go through ResolveSpellcastProvocation (~:1967 and ~:3705). PHB p.98: a quickened spell does not provoke.
(d) Quicken is offered for spontaneous casts and is honoured before the spontaneous full-round rule. CombatUI.cs ~:3078 hides the warning. PHB p.98 Special forbids it. The defect is latent behind SPL-040.
(e) The entangled casting check, ResolveEntangledSomaticCastingConcentration (~:8216-8262):
  - It rolls d20 + caster level + ability modifier with a 'Dispel Magic check' dice label, instead of the Concentration skill used by the grappled and defensive checks.
  - It runs only for somatic spells.
  - Rule: Concentration DC 15 + spell level for any spell (PHB p.70 Concentration table; entangled, PHB glossary). Verify both pages.
(f) HoldPendingMeleeTouchCharge (~:1494-1641) is a fifth cast pipeline. It skips the Resilient Sphere block, CanBeginSpellcastWhileGrappledOrPinned, the entangled check, Blink, the pouch, the counterspell and the Ring of Counterspells.
(g) Slot-consume failure loses the action:
  - The 'failed to consume' exits (~:1767-1772, ~:1951-1957, ~:3694-3700, and the ASF and Blink consume failures) call ClearSpellcastResourceSnapshot rather than Restore.
  - The action has already been spent, so the caster loses it with no spell and no refund.
(h) Cleric spontaneous conversion cannot take metamagic: the convert button passes null metamagic (UI/Combat/CombatUI.cs ~:2745). Verify the rule (PHB p.88, spontaneous casting and metamagic).
(i) Allies always fail their saves:
  - ShouldForceTargetToAcceptSave (~:1325) makes every ally, the caster included, forgo the save against every spell, harmful ones included.
  - It is used at ~:2040, ~:4707, ~:4743 and NPCTurns ~:1362.
  - PHB p.177: a creature may voluntarily forgo a save; this is not automatic failure against harmful effects.
(j) The deafened 20% verbal failure lives inside SpellCaster.Cast (Assets/Scripts/Spell/Casting/SpellCaster.cs ~:89):
  - the generic AoE loop rolls it once per target;
  - custom area handlers never roll it.
  RAW: one roll per casting.
(k) Wish spends nothing: HandleWishSpellCast (GameManager_Spells_W.cs ~:1043) spends no slot or action, provokes nothing and rolls no ASF. Mark it 9th level, outside the 1-8 scope.
(l) HandleConcentrationOnCasting order:
  - It runs before ResolveSpellcastProvocation on every pipeline (~:1965/1967, ~:1611/1613, ~:3703/3705, GameManager_Grease.cs ~:294/295).
  - So a cast cancelled at the AoO prompt still has rolled and possibly ended the old concentration spell.
  - Also note that the check itself is not in the PHB (decided 2026-10-15 as decision D1: it is removed in step raw-concentration-rules).

Refresh the evidence of existing entries:
- AI-001: the Area refusal is at NPCTurns ~:1294.
- SPL-006: add HoldPendingMeleeTouchCharge.
- SPL-091: Remove Curse and Remove Blindness/Deafness do nothing on the NPC path.
- SPL-057.
- SPL-114.

Other docs:
- docs/architecture/spells.md: fix the anchors (PerformSpellCast ~:1645, PerformAoESpellCast ~:3543) and add HoldPendingMeleeTouchCharge and Wish to the pipeline table.
- docs/systems/PC_NPC_PARITY.md step 9: add a one-line pointer saying batch 11 implements it.

Commit subject: 'File cast-pipeline defects found mapping batch 11'. The commit body lists the new IDs.

**Verification.** No code changes, so no compile is needed. Check:
- every new entry cites file and member evidence and a verified rule page;
- the KNOWN_ISSUES counts and Next ID add up;
- `git diff --stat` shows only docs/.

**Risk.** Low. Filing a finding that does not hold: mitigated by re-verifying each finding in code and in the PDF. Do not phrase owner-pending items (l) as rulings.

**Filed (2026-10-15).** The IDs that later steps cite as "step-1 issue (x)": (a) SPL-141, (b) SPL-142, (c) SPL-143, (d) SPL-144, (e) SPL-145, (f) SPL-146, (g) SPL-147, (h) SPL-148, (i) SPL-149, (j) SPL-150, (k) SPL-151, (l) SPL-152; the separate gap from decision D1 item 5 (maintaining costs a standard action, decision D2) is SPL-153. Every finding held on re-verification; three details were corrected: both globes are on PHB p.236 (not p.246); Wish rolling no arcane spell failure is correct (no somatic component, PHB p.122); and a held charge does meet the Ring of Counterspells, at delivery, since `PerformSpellCast` checks the Ring for a delivered charge too. Review of the filing added SPL-154 (holding the charge of a cleric's converted cure or inflict spell ignores the conversion; closed in step 14), widened SPL-142 to Sound Burst's save negating its damage, and raised SPL-147 to Med. The entangled DC (step 13) is a delegated RAW reading of 2026-10-14 recorded in `docs/systems/RULES_COVERAGE.md` 4.1: PHB p.170 gives a flat DC 15, the glossary (p.308) and the p.70 table with its casting footnote give DC 15 + the spell's level, and the latter is applied.

### 2. Add the inert cast-stage side channel, trace-diff tool and spell routing golden suite (`cast-stage-trace-tooling`)

Tooling only. Runtime behaviour and every scenario trace hash must stay unchanged.

1. Hook. In Assets/Scripts/_Core/ScenarioHooks.cs add `public static Action<CharacterController, SpellData, string, string> CastStage;` It must be null outside the harness and cleared wherever ScenarioHooks is reset.

2. Mark helper. Add Assets/Scripts/Spell/Casting/Core/CastStageTrace.cs: `public static class CastStageTrace { public static void Mark(CharacterController caster, SpellData spell, string stage, string detail = null) }`.
   - Mark may only null-check the hook and invoke it. No logging, no dice, no allocation when the hook is null.
   - At call sites, pass constant strings or guard with `if (ScenarioHooks.CastStage != null)` before building any interpolated detail.

3. Mark call sites, with no other change:
   - the existing stage points of PerformSpellCast, PerformAoESpellCast, HoldPendingMeleeTouchCharge and TryConsumePendingSpellCast (GameManager.SpellCasting.cs), TryNPCPerformSpellCast (_Core/GameManager.NPCTurns.cs), and the Grease casts (Spell/Special/GameManager_Grease.cs);
   - the stage points are: legality, cost, distraction (entangled and grappled), failure (ASF and Blink), spend, concentration-on-cast, provocation result, counterspell, ring, invisibility break, mirror image, target blink, globe, SpellCaster.Cast, handler claimed (the handler name), buff and combat-end;
   - one Mark at the top of each handler that claims a cast (single-target and area).

4. Writer. The scenario harness (Assets/Scripts/Tests/Scenarios) subscribes while a job runs and writes `Logs/Scenarios/<runId>/<jobId>/caststages.jsonl`. This file is outside the hashed trace (ScenarioRunner.Hash).

5. Diff tool. Add `Tests.Scenarios.ScenarioTraceDiff.FirstDivergence(string runA, string runB, string jobId, string[] ignoreEvents)`, editor-only and callable through the Unity MCP. It reports the first differing trace event with context and the matching caststages lines from both runs. Document the procedure in docs/TESTING.md 3.5.

6. Golden suite. Add the static suite `Tests.Magic.SpellRoutingGoldenTests`:
   - For every SpellDatabase spell, it records: the post-AI/SpellCategoryClassifier.ReclassifyAll EffectType, TargetType and AoEShapeType; SpellEffectRouting.ReachesApplySpellBuff; the PC routing branch BeginPendingSpellTargeting would take (AoE, summon, swarm, Wish, self or single); and whether TryNPCPerformSpellCast refuses it.
   - Compute these from data, without casting.
   - Compare the result with tools/tests/spell-routing-golden.json, generated in this commit. A diff fails with the changed spells listed.
   - Register the suite with the StaticSuiteRunner like the others.

7. Harness accessors (TST-006). Add Harness_SetPendingCast(spell, metamagic) and Harness_GetPendingSpell to _Core/GameManager.ScenarioHarness.cs. Switch the reflection on `_pendingSpell` and `_pendingMetamagic` in Assets/Scripts/Tests/Scenarios RulesScenarios.cs (~:7398-7573; grep the whole Tests folder for both names) to these accessors. Step 4 makes those fields properties, which would break GetField.

8. Files and docs. New .cs files need the Unity-generated .meta: refresh through the Unity MCP and commit the metas; never hand-write them. Update docs/TESTING.md (side channel, diff tool, golden file).

**Verification.** 1. `bash tools/compile_check.sh`: exit=0 errors=0 warnings=508.
2. Static suites, edit pass then Play pass (Tests.Runner.StaticSuiteRunner.RunFromCommand): verdict OK, and the new golden suite passes.
3. ScenarioHarness.QueueFresh for rules/* and smoke/* seeds 1-3, and soak/dmg-random-encounters seeds 1-50:
   - every job's trace hash must equal the batch 10 run recorded in commit 01630e70 / docs/TESTING.md;
   - caststages.jsonl files exist for jobs that cast.
4. Record this run ID in the commit body as the batch 11 reference run.

**Risk.** Low. An allocating or logging Mark would perturb hashes; the hash check catches it. Reflection pin changes must keep the scenario assertions identical.

**Built (2026-10-15).** As planned, with these choices: the stage names are constants on `CastStageTrace` (`begin` with the pipeline's method name, then `legality`, `cost`, `distraction`, `failure`, `spend`, `concentration-on-cast`, `provocation`, `counterspell`, `ring`, `invisibility-break`, `mirror-image`, `target-blink`, `globe`, `spellcaster-cast`, `handler`, `buff`, `combat-end`); the shared helpers mark their own outcome (entangled and grappled concentration, ASF, `ConsumePendingSpellSlot`, `HandleConcentrationOnCasting`, the three provocation helpers, `BreakInvisibilityOnHostileSpellCast`), so every pipeline that calls them is covered once; the 51 single-target handlers mark `handler` once past every check that can still decline the cast (a missed ray writes no line; the Sound Burst stun and Shield Other, which act and then let the generic path go on, mark `<name> (continues)`), the summon and swarm placements mark `handler` after the provocation, the NPC combat end is marked in `TryNPCPerformSpellCastForAI`, while an area handler is marked as the first statement of its dispatch block in `PerformAoESpellCast` (the area handlers are called only there, and several have multi-step guards, so the dispatch block is the exact claim point); a job's id is its trace file name, and `caststages.jsonl` is written only for jobs that marked a stage; the harness also got `Harness_GetPendingMetamagic`, to restore the pending metamagic without reflection; the golden suite reports Grease as its own branch (`grease`, the caster's mode choice). Reference runs: edit pass 20261015T130539Z-edit, Play pass 20261015T130553Z-play, `QueueFresh` batch 20261015-090608 (rules 20261015-090614, smoke -090753, soak -090802, -090827, -090842, -090911, -090931): all 605 trace hashes equal batch 10's. Rerun after the review fixes (batch 20261015-092800: rules 20261015-092805, smoke -092944, soak -092953, -093017, -093032, -093059, -093120; static passes 20261015T132722Z-edit and 20261015T132744Z-play): all 605 hashes again equal batch 10's; these are the batch 11 reference runs.

### 3. Pin today's cast behaviour with Ui, Scripted and item scenarios (`cast-pipeline-pin-scenarios`)

Tests only, plus one harness hook. No rules change.

1. Ui casts in the trace. Make ScenarioHooks.SpellCast (grep its current firing point on the NPC path) fire at the cost point of PerformSpellCast and PerformAoESpellCast for every caster, so Ui-actor casts appear in the trace.

2. Add Assets/Scripts/Tests/Scenarios/Catalog/CastPipelineScenarios.cs with these scenarios. Use forced dice where the existing catalog does.
   - rules/cast-ui-fireball: a Ui area cast with Reflex saves, damage per target and slot spent.
   - rules/cast-ui-cone-line: Burning Hands and Lightning Bolt.
   - rules/cast-scroll-ui and rules/cast-wand-ui: through InitiateScrollCastThroughPipeline / InitiateWandCastThroughPipeline. The item is spent, slots are untouched and the item action is charged.
   - rules/cast-scroll-aoo-cancel and rules/cast-wand-aoo-cancel: Known, ITM-084.
   - rules/cast-held-touch-charge: Ui Shocking Grasp, Discharge Later, deliver; a miss keeps the charge.
   - rules/cast-pair-<family>: paired Ui and Scripted casts on identical setups, asserting the same end state (conditions, effects, HP). One spell per guard group: Cause Fear, Melf's Acid Arrow, Death Knell, Silence, Sound Burst, Searing Light, Death Ward, Hold Animal, Resist Energy, Magic Weapon. Mark current mismatches Known with SPL-054, SPL-112 or SPL-113.
   - Known with the IDs filed in step 1:
     - rules/cast-globe-damage (Globe after damage);
     - rules/cast-quickened-threatened (quickened provokes);
     - rules/cast-empowered-fireball (SPL-038);
     - rules/cast-ally-in-burning-hands (allies auto-fail saves);
     - rules/cast-entangled (entangled formula);
     - rules/cast-slot-failure-refund (lost action).

3. Driving and waivers:
   - Drive Ui casts through the existing Step.Cast / Harness_SelectSpell, Harness_AnswerTouchSpellPromptCastNow and Step.AnswerAoO.
   - Add Harness_* accessors instead of new reflection (TST-006).
   - Known waivers go wherever the harness keeps scenario waivers; each one carries an issue ID.

4. Docs: list the new scenarios in docs/TESTING.md 3.4.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK in both passes.
3. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50:
   - New scenarios Pass or are Known with an ID.
   - Pre-existing jobs: hashes equal the step 2 reference, except Ui-actor jobs that cast. For each of those, ScenarioTraceDiff with ignoreEvents=[the SpellCast event name] must report no divergence.
4. Record the run ID.

**Risk.** Low. The new SpellCast firing must not change the order of any other trace event. Pinning undecided behaviour as Known is not a ruling, so word the waivers that way.

**Built (2026-10-15).** As planned, with these choices: `ScenarioHooks.SpellCast` fires at the cost point of all four PC pipelines, `PerformSpellCast` (not for the delivery of a held charge, which is no cast), `PerformAoESpellCast`, `HoldPendingMeleeTouchCharge` and `TryConsumePendingSpellCast` (summons and Grease), with a null target for the last three, so every Ui cast is one `cast` event. A known pin is a scenario expectation, `ScenarioBuilder.ExpectKnown(issue, name, check)` (`Expectation.KnownIssue`): its check states today's behaviour; a pass makes the job Known, a fail makes it Fail, so the step that changes the behaviour rewrites the pin as a plain expectation (the plan's "from Known to Pass"). Scenario waivers (`.Waive`) stay for invariant violations; none was needed. New Ui steps: `CastChoosing` (selection-list answers), `CastHeld` and `DeliverHeld` (Discharge Later and the Discharge button), `UseItem` (`TryUseConsumableFromInventory`, then the target click once the selection opens); new accessors `Harness_AnswerTouchSpellPromptDischargeLater` and `Harness_ChooseFirstSelectionOption`. The scenarios record facts (`cpp key=value` notes) in Assert steps and judge them in expectations. The prototype wizard has no slots above 2nd level (SPL-041), so the wizard scenarios that need one add the slot they cast from. Results after the step 3 review (run 20261015-102844, seeds 1-3): 6 Pass (`cast-ui-fireball`, `cast-ui-cone-line`, and the pairs `cause-fear`, `acid-arrow`, `searing-light`, `death-ward`) and 17 Known: `cast-pair-silence`, `-sound-burst` and `-hold-animal` (SPL-054: handlers missing from the NPC chain; step 6 turns them to Pass), `cast-pair-magic-weapon` (SPL-113) and `cast-pair-resist-energy` (SPL-112: the Ui caster picks cold, the NPC path forces fire; step 22 turns both to Pass), `cast-pair-death-knell` (the end states match, but both sides share SPL-155: the spell's generic 10 damage kills the dying target before the handler, so no caster gains the benefit), `cast-held-touch-charge` (SPL-156: a delivery after the casting round costs no action, where it takes the attack action by the delegated RAW reading of 2026-10-14), `cast-scroll-aoo-cancel` (ITM-084) and `cast-wand-aoo-cancel` (ITM-084, and ITM-085: the wand cast provokes as a spell, where a wand's activation does not provoke and its retrieval does), `cast-globe-damage` (SPL-141), `cast-quickened-threatened` (SPL-143, with a quickened Magic Missile so no attack roll is involved), `cast-empowered-fireball` (SPL-038), `cast-ally-in-burning-hands` (SPL-149), `cast-entangled` (SPL-145), `cast-slot-failure-refund` (SPL-147), and `cast-scroll-ui` and `cast-wand-ui`, which besides their passing checks (item spent, no slot, no action left) pin two filed issues the plan did not name: the item use is charged as one full-round action (ITM-005, read from `ActionEconomy.FullRoundActionUsed`) and the magic missiles count at the user's caster level 5, not the item's 1 (SPL-016; step 14, `raw-resources-caster-level`, which applies one caster-level rule, must rewrite that pin). Every pair also checks that both casts ran (both steps `done`, one `cast` event per caster), and the pair end state includes temporary hit points and the Death Knell bonus. Runs before the review: the new scenarios alone (20261015-100858); static passes 20261015T140917Z-edit and 20261015T140927Z-play, both OK; `QueueFresh` batches 20261015-100944 (rules 20261015-100949, smoke -101139) and 20261015-101200 (soak -101205, -101231, -101245, -101312, -101333), every run OK. Against step 2's reference runs, 581 of the 605 old jobs keep their hash and all keep their verdict; the 24 changed jobs are the Ui-actor jobs that cast (8 scenarios, seeds 1-3), each identical under `ScenarioTraceDiff` with `ignoreEvents` `cast`. After the review (test code and comments only): the cast scenarios alone (20261015-102844) and `QueueFresh` batch 20261015-103011 (rules 20261015-103017, smoke -103204; both OK, 624 jobs: the 69 cast jobs 18 Pass and 51 Known, every other job's hash and verdict equal to run 20261015-100949 and -101139; the soak was not rerun, no code it reaches changed). The step 3 reference runs are batch 20261015-103011 for `rules/*` and `smoke/*` and the soak runs above.

### 4. Introduce SpellCastRequest and a per-cast context behind the existing _pending* names (`per-cast-request-context`)

Goal: every cast carries its own state, so PC choices, item flags and the F12 flag cannot leak into a later cast. There is no other rule change.

New types, in Assets/Scripts/Spell/Casting/Core/ (C# 9, sealed classes, no init accessors or record structs):
- `CastSourceKind`: PreparedSlot, SpontaneousSlot, SpontaneousConversion, Scroll, Wand, HeldChargeHold, HeldChargeDelivery, TestPanel.
- `[Flags] TestPanelBypass`: None, Components, ArcaneFailure, Slots.
- `CastSource`: Kind, ItemData Item, int ItemIndex=-1, int? ItemCasterLevel, int? SaveDCOverride, int ConversionLevel=-1, string SacrificedSpellId, TestPanelBypass Bypass.
- `CastChoices`: Energy for Resist Energy and Protection from Energy; FireShieldWarm; DisguiseRace; MagicWeaponItem, KeenEdgeItem, KeenEdgeIsAmmo, GreaterMagicWeaponItem; AnimateRopeItem; GreaseMode; the wall modes; SummonSelection, SummonListLevel, SummonCountInfo, SummonSwarmNpcId.
- `CastAim`: Target, Cells, AreaTargets, Origin.
- `SpellCastRequest`: Caster, Spell (always a clone), Metamagic, Source, Choices, Aim, ICastDecider Decider.
- `CastContext`: Request, plus the fields the later executor needs.
- `ICastDecider`, with two stubs: UiCastDecider and AiCastDecider (AI/Spellcasting/). Each has only `IsSynchronous` for now.
- Move GameManager's private nested `SpellcastResourceSnapshot` (_Core/GameManager.cs ~:10764) to `internal sealed class CastResourceSnapshot`. A public type cannot expose a private nested class (CS0052).

GameManager changes:
1. Cast stack. Add `Stack<CastContext> _castStack`, `CastContext BeginCast(SpellCastRequest req)` and `void EndCast(CastContext ctx)`. In the editor, a pop that does not match the top raises a ScenarioHarness violation.
2. Forwarding properties. Replace each per-cast field with a private forwarding property onto the top context, keeping the same name: `_pendingSpell`, `_pendingMetamagic`, `_pendingSpellFromHeldCharge`, the Resist Energy, Protection from Energy and Fire Shield choices, Disguise Self, the weapon and rope picks, the summon fields, the scroll and wand fields, `_testPanelCastActive`, the Grease and wall modes. Grep all of them.
   - The bool item and test flags become getters on `Source.Kind`.
   - Wall ring and line sub-phase sequencing fields stay as plain fields: they are UI sequencing.
   - If no cast is open, a getter returns the default and a setter opens a Ui context, so the UI scratch flow is unchanged.
3. Entry points call BeginCast instead of their hand-written reset lists:
   - OnSpellSelectedWithMetamagic (_Core/GameManager.cs ~:9548);
   - InitiateScrollCastThroughPipeline and InitiateWandCastThroughPipeline;
   - the F12 path (_Core/GameManager.TestPanel.cs ~:67-94). It must clone the template instead of assigning the database instance;
   - the touch prompt and OnDischargeHeldTouchButtonPressed;
   - TryNPCPerformSpellCast;
   - the CharmedBehaviorController heal.
4. Prompt re-entry. The prompts that re-enter BeginPendingSpellTargeting (Resist Energy, Protection from Energy, Fire Shield, walls, Grease) must not open a second context.
5. Spontaneous conversion. The CombatUI spontaneous-conversion state (IsSpontaneousCast, SpontaneousCastLevel, SpontaneousSacrificedSpellId) is copied into CastSource once, when the UI request is built. Rules code reads the request.
6. Closing contexts. Every exit, cancel and reset ends the context: CancelAoETargeting, CancelSpellTargeting, OnSelfAoECancelled, the combat reset (~_Core/GameManager.cs:1145-1170) and every early return in PerformSpellCast, PerformAoESpellCast, HoldPendingMeleeTouchCharge, TryConsumePendingSpellCast and the NPC path.
7. Read from the request:
   - TryRollArcaneSpellFailure and the Stoneskin component bypass read `Source` (Bypass and Kind).
   - ConsumeScrollAfterCast and ConsumeWandChargeAfterCast spend for `req.Caster`, not ActivePC.
   - The scroll or wand branch of ConsumePendingSpellSlot keys on Source, not ReferenceEquals(spell, _pendingSpell).
8. NPC defaults. Move the NPC defaults out of ApplySpellBuff's `!caster.IsControllable` branches (GameManager.SpellCasting.cs ~:7068 and ~:7121; Fire, warm shield) into the NPC request build through AiCastDecider, with identical values. After this, rules code no longer reads IsControllable there.

Keep, do not change:
- the scroll ASF exemption (decided in step 13);
- the order of every check;
- all dice-context strings.

Issues and docs:
- Closes SPL-116 (stale-flag part; scroll ASF stays open) and SPL-036 (the bypass travels with the request: it is no longer cleared early, nor leaked).
- Closes the stale-choice half of SPL-112 and SPL-113. Narrow those entries to the AI-choice half.
- Closes the 'scroll cast after a cancelled conversion logs as spontaneous' leak, if it was filed.
- Update docs/architecture/spells.md (per-cast context) and the issue files. New files need their Unity-generated .meta committed.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK in both passes. Grep Assets/Scripts/Tests for every renamed field before committing (TST-006).
3. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Hashes must equal step 3's run, except jobs where a stale flag or choice used to leak. Explain each changed job with ScenarioTraceDiff in the commit body.
4. New rules/stale-cast-state:
   - a PC picks Resist Energy Cold and cancels, then a Scripted NPC casts Resist Energy and gets Fire;
   - a PC wand cast is cancelled at targeting, and the next NPC cast still rolls ASF;
   - an F12 single-target cast keeps its bypass through resolution.
5. rules/spell-save-dc (Heighten through `_pendingMetamagic`) and rules/zero-level-spells-aoo-cancel pass.

**Risk.** Medium.
- A missed exit leaves a context open; the stack violation catches it in scenarios.
- A prompt that re-enters and opens a second context loses the first choice; it is covered by the Resist Energy and Fire Shield scenarios.
- Properties break GetField reflection, so check the TST-006 pins.

**Built (2026-10-15).** As planned, with these choices. Types in `Spell/Casting/Core/` (`CastSource.cs` with `CastSourceKind` and `TestPanelBypass`, `CastChoices.cs`, which also holds the `GreaseCastMode`, `WallOfFireMode` and `WallOfIceMode` enums moved out of GameManager so a public type can carry them, `CastAim.cs`, `SpellCastRequest.cs`, `CastContext.cs`, `ICastDecider.cs`, `UiCastDecider.cs`, `CastResourceSnapshot.cs`) and `AI/Spellcasting/AiCastDecider.cs`; the stack, the entry helpers and the forwarding properties in the new partial `Spell/Resolution/GameManager.CastContext.cs`. `CastChoices.Energy` is one field for Resist Energy and Protection from Energy (one spell per cast). `SpellCastRequest.ForUi` clones the spell (with metamagic a plain clone with the pre-cast changes, exactly the old menu clone; without, `CloneForCast`, which keeps the template's `MetamagicDataRef` reference so the cast reads as the menu's instance did), `ForUiPrebuilt` takes a scroll's, wand's or held charge's own clone (a held charge's metamagic is not applied twice), `ForAI` clones and fills the choices through `AiCastDecider.FillDefaultChoices` (fire for Resist Energy and Protection from Energy, warm Fire Shield: the old values; `ApplySpellBuff` no longer reads `IsControllable`). The interface has only `IsSynchronous`; the AI defaults are a method of `AiCastDecider` outside it. Exits: assigning null to `_pendingSpell` ends the open Ui cast (every legacy exit already does), the three cancels, the failed-spend and missing-spellcasting returns of `PerformSpellCast`, `PerformAoESpellCast`, `HoldPendingMeleeTouchCharge` and `TryConsumePendingSpellCast` call `EndPendingCast`, a new Ui cast ends any Ui cast left open (exits that never cleared the spell, such as a target list with no valid target or the harness's cancel to the action menu, are closed there, not violations), the combat reset calls `EndAllCasts`, and `TryNPCPerformSpellCast` opens its cast after its null guard and ends it in a `finally` (its body is now `ResolveNPCSpellCastInContext`; the charmed heal and the Scripted Cast step reach it through `TryNPCPerformSpellCastForAI`, so they need no cast of their own). A mismatched `EndCast`, a Ui cast begun inside an AI cast and a Ui exit that tries to end an AI cast raise the new hook `ScenarioHooks.CastStackViolation`, a `cast-stack` violation. The touch prompt runs inside the selection's cast; Discharge Later marks a prepared or spontaneous source `HeldChargeHold` (a conversion keeps its kind, SPL-154). The scroll and wand "entered the pipeline" checks of the item-use code read `ConsumableCastAwaitingSpend` (the cast this use opened, its item not yet spent), which keeps today's behaviour for an item cast that ends unspent. `CleanupScrollCastState` and `CleanupWandCastState` only mark the item spent. `CleanupTestPanelCast` no longer runs when `PerformSpellCast` starts; ending a TestPanel cast forgets the panel's caster. The spontaneous-conversion state is copied from `CombatUI` once, in `BuildSpellMenuCastSource`, which then clears the menu's state; that closes the unfiled leak of a scroll or later cast logged as spontaneous after a cancelled conversion. New harness accessors `Harness_OpenCastCount`, `Harness_CurrentCastRequest`, `Harness_EndAllCasts` and `Harness_CancelSelection` (right-click). Tests: the static suite `Tests.Magic.CastRequestContextTests` (28 checks, Play mode) and the scenarios `rules/stale-cast-state-energy`, `-fire-shield` (the prompt's re-entry keeps the Ui choice; the NPC side is cast from an Assert step after widening the 4th-level slot view, SPL-041), `-wand-asf` and `-f12-bypass`. No reflection pin named a renamed field (TST-006: the two pins on `_pendingSpell` and `_pendingMetamagic` went through `Harness_SetPendingCast` since step 2). Runs (Unity 6000.4.0f1 through the MCP): static edit pass 20261015T150048Z-edit and Play pass 20261015T150057Z-play, both verdict OK, 0 regressions, the new suite 28/28; `QueueFresh` batch 20261015-110114 (rules 20261015-110119, smoke -110308, soak -110317, -110341, -110355, -110423, -110445): every one of the 624 pre-existing rules and smoke jobs and the 50 soak jobs has the trace hash of step 3's reference runs, so no job changed (no leak was reached by an existing scenario); the run's FAIL was the first version of `rules/stale-cast-state-fire-shield` (the NPC executor refused the added 4th-level slot), fixed and rerun with the other new scenarios in batch 20261015-110702 (run 20261015-110707: 12 Pass); `rules/spell-save-dc` (Heighten through `_pendingMetamagic`) and `rules/zero-level-spells-aoo-cancel` pass in batch 20261015-110533. These are the step 4 reference runs.

### 5. Put the 51 single-target handlers in a SpellId registry and switch the PC chain to it (`handler-registry-pc`)

Pure move on the PC side.

1. Create the registry. Add the partial Assets/Scripts/Spell/Resolution/GameManager.SpellHandlerRegistry.cs with `HandlerGate { None, Success, SuccessNotNegated }` and `SpellHandlerEntry { string Name; HashSet<string> SpellIds; HandlerGate Gate; bool ContinueToBuff; Func<CharacterController, CharacterController, SpellData, SpellResult, bool> Resolve; }`.
   - Build a list in today's PC chain order (GameManager.SpellCasting.cs ~:2175-2385). It has 51 entries: the 50 methods named TryResolve*SpellEffect plus TryResolveSoundBurstStunEffect.
   - Each entry wraps the existing method unchanged; keep the method names for the TST-006 pins.
   - SpellIds come from what each method actually claims. TryResolveLesserGlobeSpellEffect claims one ID through IsLesserGlobeOfInvulnerabilitySpell (corrected 2026-10-15: the predicate tests LESSER_GLOBE_OF_INVULNERABILITY only, so do not register Globe of Invulnerability against it); Fire Shield and Dimension Door use predicates that are plain id checks. Read each method.
2. Gates must be exactly today's guards:
   - None: Cause Fear, Ghoul Touch, Scare, Animate Rope and the Sound Burst stun.
   - SuccessNotNegated: Ray of Enfeeblement, Touch of Idiocy, Melf's Acid Arrow, Resilient Sphere, Mirror Image, Dimensional Anchor, Death Knell, Shield Other, Silence, Align Weapon, Hold Animal, Heat Metal, Magic Vestment, Dominate Animal and Command Plants.
   - Success: the rest.
   - ContinueToBuff: Shield Other and the Sound Burst stun.
   - A handler that returns false counts as not claimed; Ray of Enfeeblement and Touch of Idiocy do this on a miss.
   - Re-derive every gate from the current guard expressions, and note any difference from this list in the commit body.
3. `bool RunSingleTargetHandlers(caster, target, spell, result, bool negated, out bool continueToBuff)`:
   - It walks the entries for the spell's SpellId and evaluates the gate. Claimed = returned true and not ContinueToBuff.
   - It preserves today's semantics exactly, including the flag groups: anyPriorHandled, anyClericHandled with Shield Other excluded, and group 4b not checking groups 33-44. That is harmless only because the SpellIds are disjoint; verify it.
4. PerformSpellCast replaces the cascade with this call. The ApplySpellBuff gate is unchanged: not claimed, Success, ReachesApplySpellBuff and !negated.
5. NPC shadow ledger. TryNPCPerformSpellCast keeps its 19-handler chain. After the chain, compute the registry's prediction with a pure `PredictSingleTargetClaim(spell, result, negated)` that runs no handler, and send `shadow: npcClaimed=X registryWouldClaim=Y` through CastStageTrace.Mark. That writes only to the side channel.
6. Static suite Tests.Magic.SpellHandlerRegistryTests:
   - every private GameManager method matching TryResolve*SpellEffect, plus TryResolveSoundBurstStunEffect, is registered exactly once, found by reflection; do not hardcode the count;
   - no SpellId is claimed by two entries;
   - every SpellId exists in SpellDatabase;
   - gates match the manifest.
7. Docs: docs/architecture/spells.md and docs/DEVELOPMENT_RECIPES.md 'add a single-target handler' become one registry line. Update the CLAUDE.md trap about anyPriorHandled and anyClericHandled to describe the registry; keep the SPL-054 note that the NPC path still differs.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK, with the new registry suite passing.
3. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Every trace hash must equal step 4's run.
4. The shadow ledger in caststages.jsonl lists the NPC casts where the registry would claim differently. They must all be spells among the 32 PC-only handlers, or Prayer or Searing Light, whose NPC guards are narrower. Summarise the ledger in the commit body: it is the expected diff for step 6.

**Risk.** Low to medium. A gate transcription error shows as a hash change in Ui scenarios, where they cover it. Handlers with no scenario coverage rely on reading the guards carefully, so list them in the commit body.

**Built (2026-10-15).** As planned, with these findings. The registry is `Spell/Resolution/GameManager.SpellHandlerRegistry.cs`: `HandlerGate`, `SpellHandlerEntry` (with `Name`, `SpellIds`, `Gate`, `ContinueToBuff`, `Resolve`), `BuildSingleTargetHandlers` (one `Add` line per handler, a lambda over the unchanged method), `RunSingleTargetHandlers`, the pure `PredictSingleTargetClaim`/`PredictSingleTargetClaimEntry` over the instance-free table `DescribeSingleTargetHandlers`, and `HandlerGatePasses`. Every handler claims exactly one spell id through an ordinal id check with no side effect before it; the plan's note that Lesser Globe claims two ids through `IsLesserGlobeOfInvulnerabilitySpell` does not hold (the predicate tests `LESSER_GLOBE_OF_INVULNERABILITY` only; Globe of Invulnerability is an area handler), and Fire Shield's and Dimension Door's predicates are plain id checks too. The 51 ids are disjoint, so the four flag groups (including `anyClericHandled` leaving Shield Other out and the domain group not checking the 4th-level group) reduce to one lookup. The gates re-derived from the guard expressions equal the plan's list exactly. Two notes on ContinueToBuff: Shield Other returns true only when `result.Success` is false, which its gate excludes, and the Sound Burst stun always returns false, so for both the old chain's treatment of a true return (Shield Other outside `anyClericHandled`, Sound Burst inside it) never mattered and ContinueToBuff reproduces it. The NPC shadow prediction is taken from the result before the chain runs (what the registry would see) and marked after it as stage `shadow` with `npcClaimed=<bool> registryWouldClaim=<bool> [entry=<handler>]`, plus `registryWouldRun=<handler> npcRan=false` when the registry would run a ContinueToBuff entry (`PredictSingleTargetContinuingEntry`): those never claim, so the claim comparison alone cannot show them, and the NPC chain runs neither the Sound Burst stun nor Shield Other's link; it runs only while `ScenarioHooks.CastStage` is set. New static suite `Tests.Magic.SpellHandlerRegistryTests` (484 checks, edit or Play; the manifest pins each entry's name, gate, ContinueToBuff flag and the spell id read from the method's own guard). Runs: static edit pass 20261015T153644Z-edit (OK, 0 regressions; the new suite 428/428 with `all=true`), Play pass 20261015T154134Z-play (121 suites, 8905 passed, 80 failed, OK, 0 regressions; 428/428); `QueueFresh` batch 20261015-113652 (rules 20261015-113658, smoke -113848, soak -113858, -113922, -113936, -114004, -114025): every one of the 686 jobs has the trace hash and verdict of step 4's reference runs (633 Pass, 51 Known, 2 Inconclusive). Shadow ledger: 239 `shadow` lines; the registry would claim differently in 12 casts, all in the `rules/cast-pair-*` NPC casts of PC-only handlers: Death Knell, Death Ward, Hold Animal and Silence (3 seeds each); and the 3 NPC Sound Burst casts of `rules/cast-pair-sound-burst` show `registryWouldRun=TryResolveSoundBurstStunEffect npcRan=false`: the claims agree there, but step 6 will start rolling the Fort save and stunning on a failed one (PHB p.281), so that Known pin (SPL-054) changes too; no NPC cast Shield Other in these runs. Every other NPC cast agrees (Cause Fear, Fire Shield, Melf's Acid Arrow, Prayer and Searing Light claimed by both). The expected diff of step 6 is these 15 casts (5 jobs, 3 seeds each). Re-run after the review fixes (continuing-entry ledger, manifest spell ids): edit pass of the suite 484/484 (`all=true`), Play pass 484/484 in its own session after the scenarios, `QueueFresh` batch 20261015-114811 (rules 20261015-114817, smoke -115003, soak -115012, -115036, -115050, -115117, -115138): 686 jobs, every hash and verdict equal to step 4's, 239 `shadow` lines, 15 differences as above. Ui scenarios exercise 9 of the 51 entries (Cause Fear, Melf's Acid Arrow, Fire Shield, Death Knell, Silence, the Sound Burst stun, Searing Light, Death Ward, Hold Animal); the other 42 rest on the guard reading and the manifest in the suite.

### 6. Run the shared handler registry on the NPC cast path (SPL-054 handlers) (`npc-handler-registry`)

Behaviour change, one cause: NPC single-target casts run the same 51-entry registry as PCs.

1. Switch the NPC chain. In TryNPCPerformSpellCast (Assets/Scripts/_Core/GameManager.NPCTurns.cs, the chain at ~:1450-1526), replace the 19-term chain with RunSingleTargetHandlers from step 5, using the PC gates. Remove the shadow prediction.
2. Audit the 32 handlers newly reachable for NPCs. For each, check for:
   - UI prompts or CombatUI dialogs;
   - reads of ActivePC, IsControllable or `_pending*` choices;
   - per-turn follow-ups that only the PC turn advances (for example SpiritualWeaponRoundsRemaining and its attacks: confirm the NPC turn ticks it).
   Fix any actor-specific code so that it reads the caster. For a handler that needs a caster choice the AI cannot yet give (for example a Dimension Door destination), exclude that spell from AI candidates in AIService/AISpellcastingStrategist selection. That is a selection exclusion, not a rule fork. Note the exclusion in SPL-091.
3. Remove Curse and Remove Blindness/Deafness should now resolve for NPCs. Add them to SpellEffectRouting.HandlerOwnedSpellIds only if routing requires it.
4. Issues and docs:
   - Close the handler half of SPL-054. The entry stays open for the separate NPC executor (step 8).
   - Narrow SPL-091 to what remains (summons, escapes, excluded spells).
   - Update docs/systems/PC_NPC_PARITY.md and docs/systems/AI.md.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK.
3. rules/cast-pair-* (step 3): the families that only mismatched because of handlers flip from Known to Pass; remove their waivers.
4. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Every changed hash must belong to a job listed in step 5's shadow ledger. Run ScenarioTraceDiff on 3 changed soak seeds to confirm the first divergence is a newly claimed handler. Record the soak outcome counts (wins, wipes, rounds) in docs/TESTING.md.

**Risk.** Medium. Newly live NPC effects change enemy behaviour: Silence areas, Death Knell, Spiritual Weapon, Ghoul Touch paralysis. A PC-only assumption inside a handler (ActivePC) would misfire on AI turns; the audit and the paired scenarios guard this.

**Built (2026-10-15).** As planned, with these findings. `ResolveNPCSpellCastInContext` (the body of `TryNPCPerformSpellCast`) calls `RunSingleTargetHandlers(npc, target, spell, result, effectNegatedBySave, out _)` after the globe check, exactly where `PerformSpellCast` does, and runs `ApplySpellBuff` only for an unclaimed, successful, unnegated cast that `ReachesApplySpellBuff`; the 19-term chain, the `shadow` mark, `PredictSingleTargetClaim`, `PredictSingleTargetClaimEntry`, `PredictSingleTargetContinuingEntry` and `CastStageTrace.Shadow` are gone. Audit of the 32 newly reachable handlers (Ghoul Touch, Dimensional Anchor, Remove Curse, Dimension Door, Death Knell, Shield Other, Silence, the Sound Burst stun, Spiritual Weapon, Align Weapon, Invisibility Purge, Remove Disease, Remove Blindness/Deafness, the four alignment bursts, Death Ward, Divine Power, Freedom of Movement, Spell Immunity, Neutralize Poison, Poison, Dismissal, Repel Vermin, Imbue with Spell Ability, Hold Animal, Produce Flame, Heat Metal, Magic Vestment, Dominate Animal, Command Plants): none reads ActivePC, IsControllable or a `_pending*` choice (Dimension Door reads `IsPlayerControlled`, below), and all take the caster, DC (`GetSpellSaveDC`, which reads the open cast's metamagic, null for an AI request) and caster level from the caster they are given. Choices that are not the AI's to give: Dimension Door picks its destination inside the handler by `IsPlayerControlled` (an escape square, else next to the weakest foe), and Imbue with Spell Ability opens a slot panel only a player can answer; both are kept out of the AI's candidates by `AISpellcastingStrategist.NeedsCasterChoiceAiCannotGive`, checked in `AIService.SelectSpell` and `SelectLowerLevelAlternative` (a selection exclusion, filed in SPL-091; a Scripted or charmed cast still runs the handler). Align Weapon (alignment from the caster's alignment) and Spell Immunity (always Magic Missile) make the same default choice for every caster, so they need no exclusion. Per-turn follow-ups: Spiritual Weapon's `ProcessSpiritualWeaponTurnStart` has no caller on either side (SPL-043), so the NPC turn misses nothing the PC turn does; Heat Metal's `ProcessHeatMetalTick` has no caller either (SPL-109, both sides), and Ghoul Touch's paralysis ticks on the target's conditions and tracked effect as for a PC cast. The four alignment bursts are TargetType.Area, so the NPC path still refuses them before the handler step (AI-001). Remove Curse and Remove Blindness/Deafness need no `SpellEffectRouting` entry: the handler step runs before, and regardless of, the routing check. Tests: `rules/cast-pair-silence`, `-sound-burst` and `-hold-animal` are plain expectations now; new pairs `rules/cast-pair-ghoul-touch`, `-remove-curse` and `-remove-blindness` (the last two give both casters the condition and check it is there before the cast and gone after it); `Tests.Magic.SpellHandlerRegistryTests` drops its 14 prediction checks and checks that the AI excludes exactly Dimension Door and Imbue among the 51 registered ids (522 checks). Runs (docs/TESTING.md 3.2): compile 0 errors, 508 warnings; static edit pass OK, the registry suite 522/522 (20261015T160106Z-edit), Play pass 20261015T160129Z-play OK (121 suites, 8999 passed, 80 failed, 0 regressions); `QueueFresh` batch 20261015-120150 (rules 20261015-120157, smoke -120346, soak -120356, -120421, -120436, -120504, -120525): every changed hash is one of step 5's 15 ledger casts (`cast-pair-silence`, `-sound-burst`, `-hold-animal` Known to Pass; `-death-ward` and `-death-knell` hash only, the latter still Known for SPL-155), the 9 new pair jobs pass, and every other rules, smoke and soak hash equals step 5's. No soak seed changed (no soak caster casts a spell whose handler the NPC path gained), so `ScenarioTraceDiff` ran on 5 changed rules jobs instead of 3 soak seeds: each first divergence is the Scripted cast's newly claimed handler. Soak: 45 Victory, 2 Defeat, 3 Stalemate, mean 5.14 rounds, 50 Pass. No delegated RAW reading was needed.

**Review fixes (2026-10-15).** The review of step 6 found shared defects that the newly live NPC handlers now reach, and fixed three in the same change (both paths, so each changes PC casts too): (a) Ghoul Touch (gate None) paralyzed and released its stench when the cast had failed (spell resistance, a globe, a deafened caster's verbal failure); `TryResolveGhoulTouchSpellEffect` now returns with no effect for a failed result after its miss check (PHB p.235, SR yes; SPL-142 narrowed to Sound Burst and Animate Rope); (b) Spiritual Weapon and Produce Flame took the generic `SpellCaster.Cast` dice on top of their handler's strike, now `DamageResolvedByHandler` (SPL-124 closed); (c) Dimension Door's foe tests compared `IsPlayerControlled`, now `TeamUtility.IsEnemy` (the escape-or-aggressive choice by control stays, a PC/NPC fork listed in PC_NPC_PARITY under SPL-091). Filed: SPL-157 (Remove Blindness/Deafness cures both conditions, PHB p.270: the caster picks one) and SPL-158 (Dismissal treats native outsiders as extraplanar and kills the creature). New scenarios: `rules/cast-pair-ghoul-touch-sr`, `-spiritual-weapon`, `-produce-flame` (forced dice, exact hit points), `rules/cast-ai-spiritual-weapon` (an AI-run cleric chooses the spell; the dummy loses only the strike) and `rules/cast-ai-skips-dimension-door` (an AI-run wizard with only Dimension Door never casts it). Runs: see docs/TESTING.md 3.2.

### 7. Extract the staged cast core and run PC single-target casts through it (pure move) (`cast-core-single-target-pc`)

Pure move. Every PC trace hash must stay identical.

1. Core API. Add the partial Assets/Scripts/Spell/Resolution/GameManager.CastCore.cs with:
   - `enum CastStage { Legality, Cost, Distraction, Failure, Spend, ConcentrationOnCast, Provocation, Counterspell, RingCounterspell, InvisibilityBreak, MirrorImage, TargetBlink, Globe, Resolve, Handlers, Buff, PostCast, CombatEnd }`;
   - `CastOutcomeKind { Resolved, Refused, Cancelled, Interrupted, Failed }` and `CastOutcome { Kind, At, Reason, Result }`;
   - `void ExecuteCast(CastContext ctx, Action<CastOutcome> done)`: continuation style, one private method per stage;
   - `CastOutcome CheckLegality(SpellCastRequest req)`: no side effects;
   - `void FinishCast(CastContext ctx, CastOutcome o)`: the single exit. It logs once, with the spontaneous and quickened prefixes built once in ctx.LogPrefix (SPL-063, single-target part); calls EndCast; then calls the decider's finish continuation.
2. UI-only code. The Ui finish continuation does the UI work: CurrentSubPhase, ShowActionChoices, AfterAttackDelay, highlight clearing and _lastCombatLog. Stage methods must not touch CombatUI except through CombatLogHelper logging.
3. Move the body of PerformSpellCast (GameManager.SpellCasting.cs ~:1671-2500) into the stages, in exactly today's order:
   - test-panel cleanup (now a no-op);
   - IsValidTargetForSpell;
   - the choice re-prompts (now UiCastDecider.FillChoices);
   - the weapon pickers;
   - pouch;
   - snapshot, action and spend;
   - entangled, grappled, ASF and Blink;
   - HandleConcentrationOnCasting, then provocation (UiCastDecider.ChooseProvocation wraps ShowAoOActionConfirmation and ResolveThreatenedSpellcast);
   - counterspell, Ring and invisibility break;
   - Mirror Image, target Blink;
   - SpellCaster.Cast, then the Globe (still after Cast in this step);
   - registry, ApplySpellBuff, PostCast;
   - EvaluateCombatEnd only when the target was killed or is out of the fight, as today.
4. Legacy quirks. Add `[Flags] enum LegacyCastQuirk` in Spell/Casting/Core/. Every behaviour that must stay as it is now but will be fixed later becomes a named bit, with an XML comment citing the issue. Examples:
   - EvaluateCombatEndOnlyOnTargetDeath;
   - GlobeAfterCast;
   - EntangledUsesCasterLevelFormula;
   - QuickenedProvokes;
   - SlotFailureClearsSnapshot;
   - ConcentrationGateBeforeProvocation;
   - RangeHighlightUsesCharacterLevel;
   - SoundBurstStunIgnoresSuccess;
   - AnimateRopeIgnoresSuccess;
   - AlliesForgoEverySave;
   - DeafenedRolledInSpellCaster;
   - ScrollSkipsArcaneFailure.
   Rules for the bits:
   - request builders set them, and only stage methods read them;
   - the UI builder sets the bits that describe today's PC path.
5. PerformSpellCast becomes: build or continue the request, then ExecuteCast with UiCastDecider.
6. Keep unchanged:
   - every method name reflected by tests (grep Assets/Scripts/Tests: ResolveThreatenedSpellcast, ResolveGrappledOrPinnedCastingConcentration and others);
   - all dice-context strings and log texts.
7. Docs: docs/architecture/spells.md (the core and the stage list). New files need their Unity-generated .meta committed.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK.
3. New static suite Tests.Magic.CastCoreStageOrderTests. A recording fake decider casts a self buff, a touch spell, a ray and a buff on another creature; it asserts the stage sequence and that no stage calls CombatUI except for the log.
4. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Every hash must equal step 6's run, and caststages.jsonl must match stage for stage. Pay attention to the harness steps AnswerAoO, Harness_SelectSpell and Harness_AnswerTouchSpellPromptCastNow.

**Risk.** Medium to high, because of the size of the mechanical move:
- The prompt flow (choice dialogs, AoO prompt) must resume at the same point.
- Fifteen-odd early exits must each map to a CastOutcome.
- Any reordering changes dice and shows up as a hash change; fix the order rather than explain it.

**Built (2026-10-15).** As planned, with these choices. `Spell/Resolution/GameManager.CastCore.cs` holds `CastStage`, `CastOutcomeKind`, `CastOutcome` (`Kind`, `At`, `Reason`, `Result`, plus `Detail` for a refusal's missing component, `CombatEnded`, and `BackToActionMenu` for the UI), `ExecuteCast`, `CheckLegality` (the target and component tests, no side effect) and one `CastStage<Name>` method per stage, each calling the next, ending in `FinishCast`. The UI's side is `GameManager.CastDeciders.cs` (`UiFillCastChoices`, `UiChooseCastProvocation`, `UiOnCastFinished`, and `AiResolveCastProvocation` for step 8); `ICastDecider` gained `FillChoices` (returns `CastChoiceFill`: Filled, Suspended while a dialog is open, Refused), `ChooseProvocation` (answers `CastProvocationAnswer`: Proceeds, Lost, Cancelled), `BeforeCombatEndCheck` and `OnCastFinished`, which `AiCastDecider` implements too. `PerformSpellCast` keeps the UI's part before a cast (the begin mark, `Animating`, hiding the Disguise Self selector, the redundant spontaneous-state clear) and hands the open cast, with its caster and target, to `ExecuteCast`. Order details kept exactly: Legality runs the target test, then the choices (the three dialogs re-enter through `BeginPendingSpellTargeting` and the three weapon pickers through `PerformSpellCast`; a suspended run ends with no outcome and the cast stays open; the pickers' refusals became `CastChoiceFill.Refused`), then the pouch; the willing-target touch skip and the forgone save are still settled right after the invisibility break; with `GlobeAfterCast` the observed order is Resolve, Globe, Handlers; the CombatEnd stage first shows the result log (`FinishCast` shows it for the other exits, Mirror Image's), then runs the decider's `BeforeCombatEndCheck` (the UI: `_lastCombatLog`, `UpdateAllStatsUI`, which also applies and removes Flanked conditions, and the highlight clearing), then `EvaluateCombatEnd`, which keeps the old order exactly (review fix, 2026-10-15: the first build ran the refresh after the check). While a run waits for the provocation answer, `ExecuteCast` and `PerformSpellCast` refuse a re-entry of the same cast (`CastContext.AwaitingDecider`, `RejectCastReentry`: a warning and a `cast-stack` violation), because a second run would reset the waiting run's state and spend again. The quickened and spontaneous prefixes are built once in the Cost stage (`CastContext.LogPrefix`, SPL-063 single-target part). Exits: 18 `FinishCast` calls, each with a `CastOutcome` (invalid target, choice refused, no pouch: Refused at Legality; no spellcasting component: Failed at Cost; entangled and grappled: Interrupted at Distraction; ASF and Blink caster: Failed at Failure, with their slot-not-spent variants; slot not spent at Spend; AoO cancel: Cancelled, AoO lost: Interrupted at Provocation; counterspell, Ring, target Blink: Failed; Mirror Image: Resolved at MirrorImage; resolved and combat over: Resolved at CombatEnd). `LegacyCastQuirk` (`Spell/Casting/Core/LegacyCastQuirk.cs`) has 15 bits: the plan's 12 plus `HeldChargeDeliveryIsFree` (SPL-156), `QuickenBeforeSpontaneousFullRound` (SPL-144) and `SpontaneousFullRoundByCasterClass` (SPL-159, filed in the review: the full-round cost decided by class, not by the cast's source; step 12 removes it); `EvaluateCombatEndOnlyOnTargetDeath` cites CORE-039 (filed in the review; step 11 removes it; the end state evaluates after every cast); `RangeHighlightUsesCharacterLevel` is declared but read by no stage yet (the range test is still the highlight's, step 14). `ForUi` and `ForUiPrebuilt` set `LegacyCastQuirkSets.UiPath` (an explicit list of today's PC-path bits; the test asserts the list, so step 8's NPC-only bits get their own set); `ForAI` sets none until step 8. Where the core already has the RAW behaviour a cleared bit switches to it (the combat-end check after every cast, no provocation for a quickened cast, a restored snapshot after a failed spend, the concentration step after the provocation); for the other bits the stage calls `RequireLegacyQuirk`, which raises a `cast-stack` violation for a request without the bit and keeps the legacy behaviour. Test: the static suite `Tests.Magic.CastCoreStageOrderTests` (72 checks in Play mode, 29 in edit mode; it also covers a refused and a suspended choice, and the re-entry guard). Runs: docs/TESTING.md 3.2 (batch 20261015-124932): all 710 rules, smoke and soak jobs keep step 6's hash and verdict and all 208 `caststages.jsonl` files are identical; after the review fixes, batch 20261015-130807 (rules 20261015-130815, smoke -131006, soak -131017, -131044, -131101, -131130, -131153) again keeps all 710 hashes and verdicts and all 208 `caststages.jsonl` files. One delegated RAW reading, recorded in RULES_COVERAGE 4.1 (2026-10-14 delegation): the full-round action for metamagic belongs to spontaneous casting, so it follows the cast's source, not the caster's classes (SPL-159); the SPL-156 row also gained PHB p.141's stated costs for touching friends with a held charge.

### 8. Run NPC single-target casts through the same cast core with an AI decider (pure move) (`cast-core-npc-single-target`)

Pure move. NPC behaviour is preserved by LegacyCastQuirk bits.

1. AiCastDecider (AI/Spellcasting/AiCastDecider.cs):
   - `IsSynchronous = true`.
   - FillChoices returns today's NPC defaults: Fire, warm shield, own race, and no weapon (so Magic Weapon still fizzles as today).
   - ChooseProvocation wraps today's NPC defensive-cast decision and ResolveNPCSpellcastProvocation. Keep that method name: it is reflected by ConcentrationServiceTests.
2. `bool RunCastSync(SpellCastRequest req, out CastOutcome o)` in GameManager.CastCore.cs runs ExecuteCast and asserts that the continuation completed before returning. In the editor, a harness violation is raised otherwise.
3. TryNPCPerformSpellCast (Assets/Scripts/_Core/GameManager.NPCTurns.cs):
   - keep its signature and its return-value meaning;
   - it becomes: build `SpellCastRequest.ForAI`, `RunCastSync`, `return outcome.Kind == Resolved` (or whatever bool it returns today for each exit; map each one exactly);
   - keep TryNPCPerformSpellCastForAI and the EvaluateCombatEnd wrapper behaviour.
4. Enumerate every behavioural difference between the old NPC body and the core as configured for PCs, by reading both. Each difference becomes a LegacyCastQuirk bit set by the AI builder, with an issue ID. Expected bits:
   - Npc counterspell, Ring and invisibility order (SPL-118);
   - NpcNoConcentrationTracking and NpcNoConcentrationOnCastCheck (SPL-114);
   - NpcNoComponentCheck (SPL-114; removed in step raw-components-all-casters, decision D3);
   - NpcRangeUsesCasterLevel;
   - NpcRefusesArea (AI-001);
   - NpcNullMetamagic (AI-001);
   - NpcNoMirrorImageSwap (SPL-117);
   - the combat-end check (CORE-039): the NPC wrapper TryNPCPerformSpellCastForAI evaluates after every cast that returns true, which is the end state; the PC bit EvaluateCombatEndOnlyOnTargetDeath stays off the AI set, and the wrapper's check and trace mark are reproduced exactly;
   - plus anything else found.
   The AI set is its own constant (LegacyCastQuirkSets), checked by CastCoreStageOrderTests against an explicit list; it differs from UiPath only by bits that name a PC/NPC parity issue.
   The old order of checks and dice must be reproduced exactly while a bit is set.
5. Delete the old NPC body.
6. Invariant: no stage reads IsControllable. Only the entry points choose the decider. Grep the core to confirm.
7. Issues and docs:
   - Close SPL-054 (one executor and one registry for single-target casts). Also note the summon and held paths remain until step 10.
   - Update docs/systems/PC_NPC_PARITY.md (step 9 progress, with the quirk list) and docs/systems/AI.md.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK, including ConcentrationServiceTests and GrappleDamageRulesTests, which reflect names.
3. CastCoreStageOrderTests extended: the AI path runs the same stages as the PC path (except steps skipped by quirk bits, listed).
4. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Every hash must equal step 7's run. rules/npc-cast-threatened, npc-cast-threatened-normal, npc-cast-unthreatened and npc-cast-spontaneous pass.

**Risk.** High if the AI path picks up UI side effects; the invariant that stages do not touch CombatUI, plus identical soak hashes, guard this. Turn re-entrancy (CORE-012): the cast stack violation catches a nested cast that does not unwind.

**Built (2026-10-15).** As planned, with these choices. `TryNPCPerformSpellCast` keeps its signature and its null-argument check, marks its begin and runs `SpellCastRequest.ForAI` through `RunCastSync(req, out outcome, stageObserver = null)` (`GameManager.CastCore.cs`), which opens the context, runs `ExecuteCast`, ends a run that did not finish (or threw) and returns `CastOutcome.WasCast` (`!BackToActionMenu`): true once the slot is spent, whatever came of the spell, and false for a refusal or an unspendable slot, which maps every exit of the old body exactly; a run that did not finish synchronously, or a decider that is not synchronous, raises a `cast-stack` violation (a run stopped before its slot was spent comes back Refused, so it returns false). `RunCastSync` puts the GameManager's cancel snapshot (`_pendingSpellcastSnapshot`, which the Cost stage captures and the later stages clear) back as it was before the run, as the old body never touched it, so an AI cast nested over a PC cast waiting at its AoO prompt cannot take that cast's snapshot (review finding; per-cast snapshots on `CastContext` can replace this when the area path joins in step 9). `TryNPCPerformSpellCastForAI` is unchanged, so its combat-end check, reason string and trace mark stay where they were. `AiCastDecider.FillChoices` fills today's defaults at the Legality stage (it no longer happens in `ForAI`; only unset choices), `ChooseProvocation` wraps `ResolveNPCSpellcastProvocation` (name kept for `ConcentrationServiceTests`), and `OnCastFinished` (`GameManager.AiOnCastFinished`) does the old body's follow-up for a cast that counts: the result log becomes the last combat log, then `UpdateAllStatsUI` (which also refreshes the Flanked conditions, so its place in the order matters to the trace). Reading the two bodies side by side gave 13 NPC bits (`LegacyCastQuirkSets.AiPath` = `UiPath` without `EvaluateCombatEndOnlyOnTargetDeath` and `RangeHighlightUsesCharacterLevel`, plus): the plan's `NpcRangeUsesCasterLevel` (SPL-017), `NpcRefusesArea` and `NpcNullMetamagic` (AI-001), `NpcNoComponentCheck`, `NpcNoConcentrationOnCastCheck` and `NpcNoConcentrationTracking` (SPL-114), `NpcInvisibilityBreakBeforeCounterspell` (SPL-118; the plan's "Npc counterspell, Ring and invisibility order") and `NpcNoMirrorImageSwap` (SPL-117; declared but read by no stage, as the swap is an end-of-turn action outside the core; step 26 removes it); and, found by the reading, `NpcOwnLegalityChecks` (no standard action, a dead target, no spellcasting, no slot, then the shared target test, all refused with a trace mark and no log line, and a failed commit of the standard action refused at Cost; step 22), `NpcSpendsBeforeFailureChecks` (the Spend stage before Failure, so the ASF and Blink failures spend nothing more, and an unspendable slot logs no error; step 13), `NpcBlinkLabelsAndSilentPass` (the NPC dice labels of both Blink rolls and no log for a passed check; step 13), `NpcResultLogBeforeTargetDeath` (the result log before `OnDeath`; step 11) and `NpcCombatEndCheckedByCaller` (no check in the CombatEnd stage, the wrapper checks after every cast it reports, after the stat refresh; CORE-039, step 11), filed as SPL-160 where no issue existed. Kept exactly by the shared stages: the entangled and grappled helpers with the same arguments, `CastSpellFromSlot` through `ConsumePendingSpellSlot` (the same `spend` marks), `SpellCaster.Cast` with null metamagic, the save and immunity log lines, the globe after the cast, the registry, `ApplySpellBuff`. The touch-skip and forgone-save tests now run before the counterspell for an NPC (they read only the creatures). The cast now uses the request's clone of the spell where the old body used the caller's instance (same id, level and metamagic record). The old body is deleted. No stage reads `IsControllable`, `IsPC`, `ActivePC` or `IsPlayerControlled` or names a decider type (grep, and `CastCoreStageOrderTests`' source scan). Tests: `CastCoreStageOrderTests` (146 checks in Play mode after the review fixes, 65 in edit mode) adds the AI set (it differs from `UiPath` exactly by an explicit list of 15 parity bits; every `Npc*` bit is AI-only), four AI casts through `RunCastSync` with a recording wrapper of the real AI decider (the PC sequence with Spend before Failure, no ConcentrationOnCast and InvisibilityBreak before Counterspell; true, Resolved at CombatEnd, the slot and action spent, the last combat log set), four silent AI refusals (no standard action, a Self spell on another creature, out of range, an area spell) and a refused non-synchronous decider; `CastRequestContextTests` checks the defaults through `FillChoices`. Runs (docs/TESTING.md 3.2): batch 20261015-133100 (rules 20261015-133109, smoke -133302, soak -133314, -133341, -133358, -133428, -133452): all 710 jobs keep step 7's trace hash and verdict; 154 `caststages.jsonl` files differ from step 7's, each only by the AI path's new `legality ok` mark (268 lines), the rest identical.

### 9. Run area casts through the cast core with an area handler registry (pure move) (`cast-core-area`)

Pure move. Hashes stay identical.

1. Area registry. In GameManager.SpellHandlerRegistry.cs add `AreaHandlerEntry { Name; HashSet<string> SpellIds; bool Async; Func<CastContext, AreaHandlerResult> Resolve }`.
   - Register the handler blocks of PerformAoESpellCast (GameManager.SpellCasting.cs ~:3589-4903) in today's first-match order. That is the ~40 TryResolve*/TryHandle* area methods plus ResolveHypnotismSpell, ResolveSleepSpell, ResolveDeepSlumberSpell, ResolveColorSpraySpell and ResolveFearSpell.
   - Each entry is a lambda over the existing method, which keeps its signature and its `_pendingSpell` reads (they still work through the forwarding properties).
   - Wall of Ice and Wall of Force are Async; the core resumes on their callback.
   - The generic per-target loop is the fallback entry.
2. Resolve stage. PerformAoESpellCast's preamble (line-of-effect filter, snapshot, action, spend, grappled, ASF, concentration, provocation, counterspell, invisibility break) becomes the core's stages with a ResolveArea variant.
   - Add LegacyCastQuirk bits for what the area path skips today: AreaSkipsEntangled, AreaSkipsBlink, AreaSkipsRing, AreaSkipsPouch (SPL-006, SPL-114) and AreaSkipsTestPanelCleanup if relevant. The area builder sets them.
3. One FinishAreaCast exit (SPL-063, area exits):
   - it builds the log prefix once and runs EvaluateCombatEnd and AfterAttackDelay through the Ui continuation;
   - the per-block repeated prefix, cleanup and EvaluateCombatEnd calls are removed;
   - preserve exactly which blocks run EvaluateCombatEnd today with a quirk bit AreaCombatEndOnlyInSomeHandlers. List the blocks: Concealment areas, Glitterdust, Web, Rainbow Pattern, Wall of Fire, Wall of Ice, Wall of Force, Flaming Sphere, Entangle, Spike Stones, Black Tentacles, Crushing Despair, Mind Fog, Mass Suggestion and Shield of Law have none;
   - the handlers that do their own AfterAttackDelay (Hypnotism, Sleep, Deep Slumber, Color Spray, Fear) stop doing so and return to the core.
4. Cancellation. CancelAoETargeting, CancelSpellTargeting and OnSelfAoECancelled end through EndCast, which clears the scroll and wand state (the SPL-116 cancel part, if it is not already closed).
5. Keep these names, which tests reflect: EnterAoETargetingMode, _isAoETargeting, TryResolveScaledAoEDamageSpell, TryResolveIceStormAoE, TryResolveInsectPlagueSpell, TryResolveFlamingSphereAoECast. Grep Assets/Scripts/Tests.
6. Close SPL-063 (if no exit duplication remains; otherwise narrow it). Update docs/architecture/spells.md.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK, including AreaControlSpellsTests, SleepSpellRulesTests, WebSpellRulesTests and FlamingSphereRulesTests at their baseline counts.
3. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50: every hash equals step 8's run. rules/cast-ui-fireball, cast-ui-cone-line, prayer-cast, spell-damage-mitigation and spell-save-dc pass.

**Risk.** Medium to high. The async wall resume points are the riskiest. Moving AfterAttackDelay out of five handlers can reorder log lines; if logs are hashed, keep the order.

**Built (2026-10-15).** As planned, with these choices. `SpellCastRequest` gained `ResolveKind` (`CastResolveKind.Single` or `Area`) and the area builder `ForUiArea(request, targets, cells)`, which turns the open Ui cast into an area cast (no single target, the aim's cells and creatures) with `LegacyCastQuirkSets.UiAreaPath`; `PerformAoESpellCast` keeps its signature and only does the UI's part (the begin mark, `Animating`, the redundant spontaneous-state clear, the re-entry guard) before `ExecuteCast`. The core's stages take the area path as follows: Legality filters the aim by line of effect first (`FilterAreaAimByLineOfEffect`, the old centroid-or-caster origin, now recorded on `CastAim.Origin`), skips the single-target test for an area request and asks the decider's `FillChoices` (a no-op for area spells: their wall modes are chosen while aiming), then the pouch test, which `AreaSkipsPouch` passes; Cost, Distraction, Failure, Spend, ConcentrationOnCast, Provocation and Counterspell are the shared ones (the `LogError` texts of the three unspendable-slot exits keep their old AoE wording for an area cast); RingCounterspell is skipped; InvisibilityBreak tests the area's creatures and goes to the new area Resolve stage (`CastStageResolveArea`). The area registry (`BuildAreaHandlers`, `AreaHandlerEntry`, `AreaHandlerResult`) holds the 40 handler blocks in the old order (`TryHandleConcealmentAreaSpellCast` with its seven ids, Consecrate, Desecrate, the scaled damage spells, Daylight, Wind Wall, Halt Undead, the five `Resolve*Spell` handlers, Glitterdust, Web, Rainbow Pattern, Ice Storm, Shout, the three walls, Flaming Sphere, the four domain areas, Calm Animals, Calm Emotions, the twelve Phase 1 and 2 spells and Black Tentacles) and the generic per-target loop as the fallback (`ResolveGenericAreaSpell`, the old inline loop moved out unchanged). Every handler's guard was read: a plain id test after null tests, so the registry dispatches by id (`AreaHandlersFor`), and no id is claimed twice. Wall of Ice and Wall of Force are `Async`: `Resolve` returns `Pending` and their callback calls `ResumeAreaCast`. The single exit `FinishAreaCast` shows the log once with `CastContext.LogPrefix`, calls the decider's `BeforeCombatEndCheck` (the old `_lastCombatLog`, `UpdateAllStatsUI`, highlight order), makes Rainbow Pattern's old concentration call, runs `EvaluateCombatEnd` under the entry's old context and its trace mark, and ends in `FinishCast`, whose UI continuation starts `AfterAttackDelay(1.5 s)` unless the combat ended; the five `Resolve*Spell` handlers return their log instead of showing it, refreshing the UI, checking and pacing the turn themselves (the order of their log, stat refresh and check is unchanged). Reading the old blocks against the plan: the blocks with no combat-end check are 24, not the plan's 15 (also Consecrate, Desecrate, Daylight, Wind Wall, Soften Earth, Plant Growth, Calm Animals, Calm Emotions and Persistent Image), each entry's `CombatEndContext` pins the old context string; and the five `Resolve*Spell` handlers never logged the quickened or spontaneous line, kept by a sixth area bit filed as SPL-161. Quirk bits (`LegacyCastQuirk` is now a `long` enum): `AreaSkipsEntangled`, `AreaSkipsBlink`, `AreaSkipsRing`, `AreaSkipsPouch` (SPL-006, SPL-114), `AreaCombatEndOnlyInSomeHandlers` (CORE-039) and `AreaLogPrefixOnlyInSomeHandlers` (SPL-161); `UiAreaPath` is the `UiPath` bits an area cast shows (the cost, provocation and concentration-order bits, the scroll's spell failure, the generic loop's forgone saves and SpellCaster deafness roll) plus these six. `AreaSkipsTestPanelCleanup` was not needed: since step 4 no cast runs a test-panel cleanup at its start. Cancellation (item 4) needed no change: `CancelAoETargeting`, `CancelSpellTargeting` and `OnSelfAoECancelled` already end the cast through `EndPendingCast` and `EndCast` (step 4), so its scroll, wand and F12 source ends with it. Observable differences outside the trace, all unreached by the scenarios: a cancel at the AoO prompt now logs "cancels spell cast" before the stat refresh (as single-target casts since step 7); Fear's log becomes the last combat log (nothing reads it); the `[AoE Spell]` console copy comes from `FlushCastLog`; the combat-end phase event's caller frame is `FinishAreaCast` (no reference trace ended a fight on a PC area cast). The names reflected by tests are kept. Tests: `CastCoreStageOrderTests` (193 checks in Play mode, 87 in edit mode) adds the area set and three area casts by the recording decider; `SpellHandlerRegistryTests` (898) adds the area manifest; new scenarios `rules/cast-ui-sleep` (Known, SPL-162: Sleep's 1-round casting time costs only the standard action) and `rules/cast-ui-sleep-quickened` (Known, SPL-161; the area exit declares the victory). Runs (docs/TESTING.md 3.2): batch 20261015-141729 (rules 20261015-141739, smoke -141929, soak -141943, -142012, -142031, -142103, -142128): all 710 jobs keep step 8's trace hash and verdict; 18 `caststages.jsonl` files (the area-casting jobs) differ only by the new `legality ok` mark. SPL-063 is narrowed to the summon, Grease and held-charge exits (step 10). No delegated RAW reading was needed.

### 10. Move summon, swarm, Grease and touch-charge holding onto the cast core (pure move) (`cast-core-summon-held-grease`)

Pure move. Hashes stay identical.

1. ResolveSummon variant. PerformSummonMonsterCast (GameManager.SpellCasting.cs ~:712) and PerformSummonSwarmCast (~:612) become request builders. The summon and swarm choices live in CastChoices, and the core gets a ResolveSummon variant.
2. Grease. The three Grease casts (Spell/Special/GameManager_Grease.cs ~:287, ~:420, ~:473) become one request with Choices.GreaseMode.
3. Delete TryConsumePendingSpellCast (~:34). Its spend is the core's Spend stage.
4. Held charges:
   - The touch prompt (Cast Now or Discharge Later, _Core/GameManager.cs ~:9597) becomes UiCastDecider.ChooseTouchDelivery.
   - Holding is a request with Source.Kind=HeldChargeHold that runs the core's stages through Spend and Provocation, then stores the charge with SetHeldTouchCharge (ResolveHoldCharge).
   - Delete HoldPendingMeleeTouchCharge (~:1494-1641).
   - Delivery (OnDischargeHeldTouchButtonPressed) is a request with Source.Kind=HeldChargeDelivery.
5. Quirk bits, set by the builders, for what each path skips today:
   - SummonSkipsBlink, SummonSkipsCounterspell, SummonSkipsPouch;
   - GreaseSkips* (same set);
   - HoldSkipsSphere, HoldSkipsGrappleLegality, HoldSkipsEntangled, HoldSkipsBlink, HoldSkipsPouch, HoldSkipsCounterspell;
   - DeliverySkipsCounterspell.
   Cite SPL-006, SPL-114 and the held-charge issue filed in step 1.
6. AI side: AiCastDecider.ChooseTouchDelivery always returns 'cast now', as today.
7. Update docs/architecture/spells.md: the pipeline table now shows one core with Single, Area, Summon and HoldCharge resolve variants.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK, including the Summon tests.
3. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50: every hash equals step 9's run. rules/cast-held-touch-charge and rules/summon-creature-entries pass.
4. Grep confirms that TryConsumePendingSpellCast and HoldPendingMeleeTouchCharge are gone.

**Risk.** Medium. The summon placement prompts and initiative insertion (AI-015 parallel lists) must stay exactly in place. A Grease mode prompt must not open a second context.

**Built (2026-10-15).** As planned, with these choices. `CastResolveKind` gained `Summon`, `Grease` and `HoldCharge`: Grease got its own variant rather than riding on Summon, because its three modes resolve an area, an object or armor and end with a result log, so the core has five resolve variants (Single, Area, Summon, Grease, HoldCharge). Builders: `SpellCastRequest.ForUiSummon(req, destination)` (`CastAim.Destination`; the creature, list level, count rule and swarm type were already `CastChoices`), `ForUiGrease(req, objectHolder, areaTargets, cells)` (one request for the three Grease casts, its mode in `CastChoices.GreaseMode`; the creature holding or wearing a greased object is `CastAim.ObjectHolder`, not `Aim.Target`, because the spell targets the object, which also keeps the `cast` trace event's target null as before) and `ForUiHoldCharge(req)` (a prepared or spontaneous slot's source becomes `HeldChargeHold`, a conversion keeps its kind). The entry points `PerformSummonMonsterCast`, `PerformSummonSwarmCast`, `PerformGreaseCast` (replacing `PerformGreaseAreaCast`, `PerformGreaseObjectCast` and `PerformGreaseArmorCast`) and `BeginHoldTouchCharge` (replacing `HoldPendingMeleeTouchCharge`) keep their pre-cast UI checks (the summon's room test re-opens the placement), then the begin mark, `Animating`, the builder and `ExecuteCast` (`OpenUiCastForBuilder` refuses a re-entry while the cast waits for its AoO answer). `TryConsumePendingSpellCast`, `HoldPendingMeleeTouchCharge`, `HandleSpellcastCancelledFromAoOPrompt` and `HandleInterruptedSpellCast` (no caller left) are deleted. The resolve stages `CastStageResolveSummon` (`ResolveSummonMonsterCast`, `ResolveSummonSwarmCast`: the old callbacks' bodies, the count roll, `SpawnSummonedCreature`, `InsertIntoInitiative` and `RegisterActiveSummon` in the old order), `CastStageResolveGrease` (`ResolveGreaseCast`) and `CastStageResolveHoldCharge` (`SetHeldTouchCharge`); exits `FinishEffectCast` (summon and hold: Resolved at Resolve, so the UI decider pauses 1 s as the old code did), `FinishGreaseCast` (Resolved at CombatEnd with the log: 1.5 s and the last combat log) and `FinishFailedSummon` (a creature that could not be spawned; it now ends the cast, which the old code left open until the next Ui cast replaced it). `CastOutcome.ShowsActionMenu` keeps the old "back to the action menu" follow-ups. The touch prompt is `ICastDecider.ChooseTouchDelivery` (`CastTouchDelivery`: CastNow, HoldCharge, Cancel; `UiChooseTouchDelivery`, answered in `GameManager.OnTouchDeliveryChosen`); `AiCastDecider.ChooseTouchDelivery` always casts now. Quirk bits (the enum now has 53 named bits; bit 43 is unused): the planned `SummonSkipsBlink`, `SummonSkipsCounterspell`, `SummonSkipsPouch`, `GreaseSkipsBlink`, `GreaseSkipsCounterspell`, `GreaseSkipsPouch`, `HoldSkipsGrappleLegality` (read by `CheckHoldChargeLegality`, which runs the grapple test for a hold without the bit), `HoldSkipsEntangled`, `HoldSkipsBlink`, `HoldSkipsPouch`, `HoldSkipsCounterspell` and `DeliverySkipsCounterspell` (in `UiPath`, required by the Counterspell stage for a delivery), plus what reading the old code found: `GreaseSkipsInvisibilityBreak` (SPL-006), `GreaseConcentrationCheckedTwice` (SPL-152), `HoldSkipsChoices` (SPL-146: a hold asks no energy type or weapon), `HoldIgnoresConversion` (SPL-154), `SummonItemCastCommitsCastAction` (filed as SPL-163: a summon from a scroll or wand spent the casting action as well), `LostCastShowsActionMenu` (SPL-006: a summon or Grease lost to a distraction or ASF went straight back to the action menu) and `EffectResolveSkipsCombatEndCheck` (CORE-039). Sets `UiSummonPath`, `UiGreasePath` and `UiHoldPath`. The planned `HoldSkipsSphere` was dropped: holding a charge inside a Resilient Sphere sends nothing through it (PHB p.258), so the hold needs no sphere test; only a delivery across the boundary does, and that belongs to the shared sphere gate of CMB-092 (delegated RAW reading 2026-10-14). A summon and a hold target no creature, so the InvisibilityBreak stage breaks nothing for them: a summoned creature, a swarm laid on a foe's square included, harms only indirectly (PHB p.245 names summoning monsters and having them attack; delegated RAW reading 2026-10-14, no bit). Review also filed SPL-164 (a summon acts after the caster's turn rather than at once on it; step 12) and SPL-165 (wrong Resilient Sphere page cites). Observable differences outside the trace, unreached by the scenarios: a cancel at the AoO prompt logs "cancels spell cast" before the stat refresh, a resolved summon refreshes the stat panels before clearing the highlights, and the begin marks name the new builders. Tests: `CastCoreStageOrderTests` (265 checks in Play mode) adds the three sets as explicit lists, the AI touch choice, a hold and a Grease armor cast by the recording decider; new scenarios `rules/cast-ui-summon-monster` (Known, SPL-162), `-summon-aoo-cancel`, `-summon-entangled`, `-summon-swarm`, `rules/cast-ui-grease` and `rules/cast-held-aoo-cancel`, run first on the unchanged code for reference hashes, with the new Ui step `CastAt(spellId, x, y, choices)` and `CombatUI.Harness_FindSummonCreatureOption`. Runs (docs/TESTING.md 3.2): batch 20261015-150738 (rules 20261015-150751, smoke -150947, soak -151004, -151036, -151059, -151134, -151203): all 716 pre-existing jobs keep step 9's trace hash and verdict, the 18 new jobs their hashes on the unchanged code; only `rules/cast-held-touch-charge`'s `caststages.jsonl` files differ (the begin mark's name and the new `legality ok` mark). SPL-063 is closed. Two delegated RAW readings (RULES_COVERAGE 4.1): the hold inside a sphere, and summoning and invisibility.

### 11. Block Globe-covered spells before damage on every path, and stop failed casts from landing effects (`raw-failed-cast-gating`)

First RAW-fix commit.

1. Globe stage before damage. Remove the GlobeAfterCast quirk.
   - Single-target: the Globe stage runs before SpellCaster.Cast, so a covered target takes no damage or healing and no handler or buff runs. Log it with CombatLogHelper.
   - Area: before any area handler or the generic loop runs, ctx.Aim.AreaTargets drops every creature that ctx.IsBlockedByGlobe(t) blocks (LesserGlobeOfInvulnerabilityAreaEffect.DoesAnyGlobeBlockSpell). Grep every area handler that recomputes targets from cells and make it use the filtered list.
   - Out of scope, noted in the issue: persistent areas (Web, Stinking Cloud and the like) affecting a creature that later enters a globe.
   - The Heighten part stays for step 17.
   - Rules: PHB p.246 (lesser globe excludes spells of 3rd level or lower) and p.236 (globe, 4th or lower).
2. Gates. In the registry manifest, the Sound Burst stun and Animate Rope gates change to Success. Remove the quirk bits SoundBurstStunIgnoresSuccess and AnimateRopeIgnoresSuccess. RAW: a spell that failed (SR, deafness, Globe) has no effect. (Ghoul Touch, the third gate-None handler with this gap, was fixed in step 6's review: the handler tests the failed result itself, as Cause Fear and Scare do; `rules/cast-pair-ghoul-touch-sr`.)
3. Combat end after every cast (CORE-039). Remove the EvaluateCombatEndOnlyOnTargetDeath quirk from the PC request builders, so the CombatEnd stage runs EvaluateCombatEnd after every resolved cast, as the NPC path already does, and the AreaCombatEndOnlyInSomeHandlers quirk from the area builder, so FinishAreaCast checks after every area cast too (step 9). Remove AreaLogPrefixOnlyInSomeHandlers with it, so every area log carries the quickened and spontaneous lines (SPL-161, close it), and EffectResolveSkipsCombatEndCheck from the summon, Grease and hold builders (step 10), so their exits check too. Close CORE-039.
4. Close the step-1 issues for (a) the Globe single-target and area parts (keep the Heighten part open, narrowed) and (b). Update rules/cast-globe-damage from Known to Pass.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK. The registry suite's gate manifest is updated.
3. New rules/area-globe: a Fireball against a creature in a lesser globe takes no damage; others take normal damage.
4. New rules/sound-burst-failed: SR beaten means no stun.
5. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Every changed hash's first divergence (ScenarioTraceDiff) must be a Globe block or a no-longer-rolled stun save or entangle. List them in the commit body.

**Risk.** Low to medium. Area handlers that recompute targets from cells could bypass the filter; grep them. The soak likely has few globes, so few hashes change.

### 12. Apply RAW action cost and provocation rules for quickened, spontaneous and cancelled casts (`raw-action-cost-provocation`)

Rules changes. All of them live in the core's Cost, Legality, Provocation and ConcentrationOnCast stages, for every path.

1. Quickened casts do not provoke (PHB p.98; verify in the PDF). Skip the Provocation stage when req.Metamagic has Quicken. Remove the QuickenedProvokes quirk.
2. Quicken once per round. Move the check (spellComp.HasCastQuickenedSpellThisRound, today only in the UI) into Legality, so both sides are bound by it.
3. Spontaneous metamagic is a full-round action when the cast is spontaneous: Source.Kind SpontaneousSlot or SpontaneousConversion. This replaces the class-based MetamagicSystem.IsSpontaneousCaster test in the Cost stage (PHB p.88; delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1). Remove the SpontaneousFullRoundByCasterClass quirk and close SPL-159.
4. Quicken refused on spontaneous casts (PHB p.98 Special). Legality refuses Quicken when the source is spontaneous, and the UI no longer offers it (UI/Combat/CombatUI.cs ~:3078). This is latent until step 18 fixes SPL-040.
5. SpellData.ActionType in Cost:
   - Audit which spells have a non-Standard ActionType or a casting time other than 1 standard action.
   - Charge the RAW casting action where the data encodes it (full-round, free or swift).
   - Spells with a 1-round casting time (Summon Monster, Summon Nature's Ally): keep today's handling and file an issue if they do not match PHB p.174 (casting time of 1 round), rather than inventing timing. SPL-164 (filed 2026-10-15) is the summon half: the creature should act at once, on the caster's turn when the casting completes (PHB p.285, p.288), not after the caster's turn in the casting round; fix it with SPL-162 so the casting-time change places the summon's action correctly.
   - The audit must also catch 1-round spells the data stores as Standard (SPL-162, found 2026-10-15): Sleep (PHB p.280), Deep Slumber (PHB p.217, like Sleep), Hypnotism (PHB p.242) and Call Lightning (PHB p.207). Set them to FullRound and charge the full-round action; then `rules/cast-ui-sleep`'s ExpectKnown("SPL-162") becomes a plain Expect that no action is left ("---"). Close or narrow SPL-162 (its delayed-effect part may stay open, as for summons).
6. Concentration gate after the cancel point. HandleConcentrationOnCasting moves after the Provocation stage resolves to Proceed or Defensive, so a Cancel leaves no rolled check and no ended spell. Remove the ConcentrationGateBeforeProvocation quirk, and GreaseConcentrationCheckedTwice with it (step 10: Grease runs the step once). The check itself is removed later, in step raw-concentration-rules (decision D1). Also remove SummonItemCastCommitsCastAction (step 10, SPL-163) with the one action cost from the cast's source.
7. Close the step-1 issues (c), (d) (latent part) and (l) (order part; the owner-question part is settled by decision D4). Update rules/cast-quickened-threatened to Pass.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK. Update MetamagicSystemTests or SpontaneousCastAvailabilityTests if they pin the class-based rule, and record the new counts in tools/tests/static-suites.json.
3. New rules/quicken-once-per-round, for a Scripted actor and a Ui actor.
4. rules/zero-level-spells-aoo-cancel and npc-cast-threatened* pass.
5. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Each changed hash's first divergence must be a skipped AoO prompt or roll for a quickened cast, a moved concentration roll, or an ActionType change. Explain each in the commit body.

**Risk.** Medium. Moving the concentration roll changes the dice order in any job where a concentrating caster casts, which is expected. ActionType honouring could change summon timing; keep summons as they are and file instead.

### 13. Make the entangled, deafened, Silence, ASF and Blink checks one RAW rule on every path (`raw-distraction-failure-stages`)

All of these are in the core's Legality, Distraction and Failure stages. Remove every related quirk bit on all paths: AreaSkipsEntangled, AreaSkipsBlink, Summon*, Grease*, Hold* for Blink and entangled (SummonSkipsBlink, GreaseSkipsBlink, HoldSkipsBlink, HoldSkipsEntangled), LostCastShowsActionMenu (step 10: a lost summon or Grease then pauses like any lost cast), EntangledUsesCasterLevelFormula, DeafenedRolledInSpellCaster and ScrollSkipsArcaneFailure.

1. Entangled. Any spell needs a Concentration check through ConcentrationManager.MakeSpellcastingConcentrationCheck, DC 15 + spell level (PHB p.308 and p.70 with its footnote 1, against the flat DC 15 of p.170: delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1; SPL-145). Replace the formula in ResolveEntangledSomaticCastingConcentration (GameManager.SpellCasting.cs ~:8340-8387). Keep the name if tests reflect it.
2. Deafened. Roll the 20% verbal failure once per cast in the Failure stage. Remove the roll from SpellCaster.Cast (Assets/Scripts/Spell/Casting/SpellCaster.cs ~:89). First grep every other caller of SpellCaster.Cast; any caller outside the core must get the roll from the shared helper.
3. Silence (SPL-092). Legality refuses verbal spells for a caster inside a Silence effect, unless the cast has Silent Spell. Use the same silence query the condition code uses (grep SilenceActive and the Silence area effect).
4. ASF only for somatic spells (SPL-096). TryRollArcaneSpellFailure rolls only when the spell, after metamagic (Still Spell), has a somatic component.
5. Scroll ASF. Verify the DMG p.238 scroll rule in the PDF.
   - If scrolls of arcane spells with a somatic component are subject to ASF, roll it for Source.Kind=Scroll. Wands stay exempt (no somatic component).
   - If the PDF says otherwise, keep the current exemption and record the finding in SPL-116.
6. Blink. The caster's 20% Blink failure uses SpellResolutionService.TryBlinkCasterFailure on every path, and the target's 50% uses TryBlinkTargetFailure, so the service becomes live. Keep the existing dice-context strings, so forced-dice scenarios hold; if the service's strings differ, change the service to match the live ones.
7. Close or narrow:
   - the step-1 issues (e), (j) and (f) (the Blink and entangled parts);
   - SPL-092, SPL-096 and SPL-116 (the scroll ASF part; close SPL-116 if nothing remains);
   - SPL-006 (the entangled and Blink parts).
   rules/cast-entangled becomes Pass.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK. SpellResolutionServiceTests stay green; update any Blink assertions that pinned the old duplication.
3. New rules/cast-silenced (PC and NPC refused for V spells; Silent Spell allowed) and rules/cast-deafened-area (one roll for a Burning Hands).
4. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Each changed hash must be attributable to one of these checks, by ScenarioTraceDiff.

**Risk.** Medium. Removing the deafened roll from SpellCaster.Cast changes dice order for any deafened caster. Silence now blocks NPC casters standing in their own Silence, which is RAW but visible.

### 14. Refund items and actions correctly and use one caster-level rule (`raw-resources-caster-level`)

1. Resource snapshot (ITM-084). CastResourceSnapshot also records the scroll stack (count) and both wand charge stores (ITM-034: update both). A Cancel at the provocation prompt restores the item, the slot and the deferred item action.
2. Slot-consume failure (step-1 issue (g)). Every 'failed to consume' exit restores the snapshot instead of clearing it, so the action is refunded. Remove the SlotFailureClearsSnapshot quirk, and HoldIgnoresConversion (step 10, SPL-154), so a held converted charge spends the sacrificed spell.
   - Held charge of a spontaneous conversion (SPL-154). The hold spends the source on the request (Kind SpontaneousConversion, SacrificedSpellId) instead of passing `isSpontaneous = false, -1, null`, so a cleric who holds a converted cure or inflict spell gives up the sacrificed spell.
3. CasterLevelRules (SPL-017). Add Assets/Scripts/Spell/Casting/Core/CasterLevelRules.cs:
   - `For(SpellCastRequest)`: the item caster level if set; otherwise the casting class's caster level, with paladin and ranger at one-half class level (PHB p.44 and p.48; verify), plus the domain boost where GetDomainBoostedCasterLevel applies it, minus negative levels, minimum 1.
   - `RangeSquares(SpellCastRequest)`.
   - Reuse GetCasterLevel and SpellDurationRules.CasterLevelFor where they are already correct; do not duplicate them.
4. Use it in:
   - the PC range highlight in ShowSpellTargets (GameManager.SpellCasting.cs ~:1424-1432, today Stats.Level; removes RangeHighlightUsesCharacterLevel);
   - the summon and swarm range helpers;
   - ApplySpellBuff's Stats.Level reads;
   - the NPC range check (removes NpcRangeUsesCasterLevel; both now use the same rule).
5. Close ITM-084, SPL-017, SPL-154 and step-1 issue (g). rules/cast-scroll-aoo-cancel, cast-wand-aoo-cancel and cast-slot-failure-refund become Pass.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK. Add a CasterLevelRules static suite covering a wizard, a cleric with a domain, a paladin and ranger at level 4 and 8, a scroll, a wand and negative levels.
3. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Changed hashes come from paladin or ranger casts (range and duration) or refund cases; explain them.

**Risk.** Low to medium. Halving paladin and ranger caster level shortens their spell durations, which is visible to players and RAW.

### 15. Unify counterspell, Ring, invisibility and concentration rules for every caster and path (`raw-counterspell-order-parity`)

Remove these remaining quirk bits:
- the NPC counterspell order bit;
- NpcNoConcentrationTracking and NpcNoConcentrationOnCastCheck;
- AreaSkipsRing;
- the Summon, Grease and Hold counterspell bits and DeliverySkipsCounterspell;
- GreaseSkipsInvisibilityBreak and HoldSkipsChoices (step 10): decide under the owner's delegation whether greasing an object a foe holds or wears is an attack on the foe (PHB p.245) and whether a Ring of Counterspells meets it, and ask a hold's choices at the hold;
- the PC-path pouch bits: AreaSkipsPouch, SummonSkipsPouch, GreaseSkipsPouch, HoldSkipsPouch;
- HoldSkipsGrappleLegality.

1. Order (SPL-118). Every cast runs counterspell, then the Ring of Counterspells, then the hostile-cast invisibility break, which is today's PC order. Ruling 2026-10-15 (judge panel; see the design doc, decision D4): a readied counterspell (or dispel magic used as one) that succeeds stops the hostile spell and the caster stays invisible; a Ring of Counterspells absorbing it still ends the invisibility; a failed counter lets the spell resolve as an attack.
2. Counterspell coverage. A held charge is countered when it is cast (the hold), not when it is delivered (PHB p.141: the charge is the spell already cast). Delivery stays uncounterable.
3. Concentration. NPC casts get BeginConcentrationTracking and HandleConcentrationOnCasting exactly like PCs (SPL-114). Only the tracking moves here; the casting-while-concentrating rule itself changes in step raw-concentration-rules.
4. Pouch. The component check runs on the area, summon, Grease and hold paths for every caster whose request does not carry the NpcNoComponentCheck bit. That bit stays for this step only; step raw-components-all-casters removes it.
5. Grapple. Holding a charge now runs CanBeginSpellcastWhileGrappledOrPinned (step-1 issue (f)). It runs no Resilient Sphere test: holding sends nothing through the sphere (PHB p.258; delegated RAW reading 2026-10-14); a delivery to a creature on the other side is refused by the shared sphere gate of CMB-092. Keep the grapple refusal's log out of `CheckLegality` when it is used as a pure pre-check.
6. Ring and area spells. Before removing AreaSkipsRing, decide under the owner's delegation whether an area spell whose area takes in the ring's wearer counts as cast on the wearer (DMG p.230), and whether the ring then counters the whole spell (as a counterspell does, PHB p.170) or only its effect on the wearer. RAW is silent; record the reading in docs/systems/RULES_COVERAGE.md 4.1 and list it in the commit body (SPL-006).
7. Close or narrow:
   - SPL-118;
   - SPL-006 (close if nothing remains);
   - SPL-114 (narrow it to the NPC component question);
   - step-1 issue (f).
   Update docs/systems/PC_NPC_PARITY.md.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK.
3. New rules/npc-concentration-tracking: an NPC Silent Image ends when the caster is damaged and fails Concentration. New rules/cast-held-grappled: a grappled PC cannot hold a somatic touch spell.
4. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Changed hashes must trace to NPC concentration tracking or checks, order changes in counterspell jobs, or pouch refusals on the PC paths.

**Risk.** Medium. NPC concentration tracking can end NPC illusions earlier in the soak, which is RAW. The pouch check on PC area casts may refuse a party caster without a pouch, which is correct and visible.

### 16. Casting ends concentration, and maintaining it costs a standard action (RAW) (`raw-concentration-rules`)

Apply decisions D1 and D2 of the design doc (judge panel 2026-10-15, explicit RAW, PHB p.176), identically for PCs and NPCs on the cast core.
D1: a caster cannot cast a spell while concentrating on another. Casting any spell (any casting time, quickened included; scrolls count, DMG p.213) first ends the maintained spell as a free action, with no roll; a 'Concentration + N rounds' spell then runs its N-round tail. Remove the non-RAW DC 15 + spell level check (GameManager.HandleConcentrationOnCasting third branch, ConcentrationManager.CheckConcentrationOnCasting, ConcentrationService.GetCastingWhileConcentratingDC and its test), turn the Summon Swarm tail into a general tail, warn the PC in the UI before a cast ends a maintained spell, and let the AI weigh the loss. Wands and spell-like abilities: record as an open delegated question in SPL-114 (not decided).
D2: maintaining a Concentration-duration spell costs a standard action on each of the caster's turns after the casting turn (no AoO); if the turn ends without it, the spell ends at the end of that turn (delegated timing, consistent with the CHR-026 bardic ruling); a caster who cannot take a standard action cannot maintain; spell-specific actions add to it (Mage Hand propel is a move action on top; Rainbow Pattern's move is free; image spells' movement is part of the concentration action). Reuse the bardic-music ConcentratedThisTurn model; PC: a 'Maintain <spell>' action and an end-of-turn lapse notice; AI: maintain when the spell is worth more than its other standard action. Record D1/D2 in RULES_COVERAGE 4.1 (D2's sub-readings as delegated readings dated 2026-10-15).
Read the full decisions in the design doc first.

**Verification.** Static tests for both rules (PC and NPC); scenarios: a caster maintaining Silent Image casts a second spell and the image ends with no roll; a caster who spends its standard action elsewhere loses the image at end of turn; an AI caster maintains. Explain every trace change.

**Risk.** Every concentration spell and every caster AI is touched; the AI may now drop maintained spells.

### 17. Every caster needs its components; NPCs carry a pouch and the right focus (`raw-components-all-casters`)

Apply decision D3 of the design doc (judge panel 2026-10-15): PCs, class-level NPCs and spellcasting monsters all need their M, F and DF components (PHB p.174); a spell component pouch covers every unpriced M and F component; costly components and foci, divine foci and oversized foci are separate items; Eschew Materials is not a core feat. Remove the NpcNoComponentCheck quirk bit so the one component check in the Legality stage covers every caster. Give NPC casters the gear the decision describes (spell component pouch for arcane casters; holy or unholy symbol for clerics, paladins and adepts; holly or mistletoe for druids and rangers; the monster cases as the decision says) through the shared creature setup (clone-safe: NPCDefinition.Clone and the template CopyDefinitionFields, CRE-023; pooled slot reset, CRE-046). A caster without a needed component is refused at legality (the AI filters such spells). Close the SPL-114 component part. Read the full decision in the design doc first.

**Verification.** Static tests: an NPC wizard without a pouch cannot cast Grease; with the default gear it can; a cleric without a holy symbol cannot cast a DF spell; costly components are spent. Rules scenario for an NPC caster disarmed of its focus (if feasible). Soak: expect no change in outcomes beyond explained traces.

**Risk.** Every NPC caster gains gear; a missed entry silently disables its spells, so add an audit test that every NPC with spells has the components its spell list needs.

### 18. Voluntary saves, one spell-resistance roll and spell save bonuses on every spell save (`raw-saves-and-sr`)

1. Voluntary save (step-1 issue (i)). Replace ShouldForceTargetToAcceptSave (GameManager.SpellCasting.cs ~:1346; uses at ~:2079, ~:4809, ~:4847 and the old NPC site) with ctx.TargetForgoesSave(target), which asks the target's own decider (ICastDecider.ForgoSave).
   - Both deciders forgo the save only against a spell that is harmless or beneficial to that target: runtime EffectType Buff, or Healing on a living target. They do not forgo it against Damage, Debuff or Control.
   - A charmed or confused ally keeps today's Buff-only rule.
   - Call it in the generic loop and in every custom area handler that rolls saves.
   - Remove the AlliesForgoEverySave quirk.
   - Rule: PHB p.177 (voluntarily giving up a save).
2. One spell-resistance rule (SPL-122 partly, SPL-057 implementation part). ctx.RollSpellResistance(t) = SpellSaveResolver.RollSpellResistance with GetCasterLevelForSpellResistanceCheck, which includes Spell Penetration.
   - It replaces the inline d20 + caster level SR rolls in ResolveHypnotismSpell, ResolveSleepSpell, ResolveDeepSlumberSpell, ResolveColorSpraySpell and ResolveFearSpell.
   - TryOvercomeSpellResistance delegates to it.
   - Keep one d20 per check, in the same position, so hashes move only where Spell Penetration applies.
   - The SR-flag audit in SPL-057 stays open; narrow the entry to it.
3. SPL-018. Move the spell-specific branch of SpellCaster.GetSaveModifier into SaveRules.SituationalBonus (Assets/Scripts/Character/Stats/SaveRules.cs), keyed on a SaveContext for the spell and its caster.
   - All spell-handler SpellSaveResolver.RollSave sites (about 26) use SaveRules.Modifier with that context, through ctx.RollSave.
   - Item save sites stay with the parity SaveService work; note that in SPL-018.
4. Close or narrow:
   - step-1 issue (i);
   - SPL-018 (spell-handler half);
   - SPL-122 (the SR and save-modifier duplicates on the cast path);
   - SPL-057 (narrowed).
   rules/cast-ally-in-burning-hands becomes Pass.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK; update the save-bonus suites' counts.
3. New rules/sleep-spell-penetration: a caster with Spell Penetration beats SR at the right rate under forced dice. New rules/charm-save-bonus-custom-handler: if a custom handler rolls a save with a spell bonus, the bonus applies.
4. rules/save-bonuses and spell-save-dc pass.
5. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Changed hashes: allies now rolling saves (a new die), Spell Penetration totals, or save totals with spell bonuses.

**Risk.** Medium. Allies now roll saves, so the die order shifts in jobs where allies stand in areas. Mis-classifying a spell as harmless would let allies skip saves; base the decision on EffectType after ReclassifyAll, and test both cases.

### 19. Carry metamagic on the spell clone and apply it to every custom resolver (SPL-038) (`metamagic-on-the-spell`)

1. Stamp metamagic on the clone. SpellCaster.ApplyMetamagicToSpellData stamps the metamagic on the clone it returns (spell.MetamagicDataRef).
   - SpellData.Clone nulls MetamagicDataRef (SpellData.cs ~:565), so stamp it after cloning.
   - Guard SpellcastingComponent.CountAvailablePreparedSpell (~:2000) and the related menu counts: only metamagic-slot clones may count against metamagic slots. The menu counts must be unchanged; add a static test.
2. Save DC. GetSpellSaveDC (Spell/Special/GameManager_Grease.cs ~:264) drops ReferenceEquals(spell, _pendingSpell) and uses spell.MetamagicDataRef. SpellSaveDCRules.Compute already falls back to it.
3. Dice. Add `SpellDiceRules.Roll(dice, MetamagicData mm, string context)`, exposed as ctx.RollSpellDice.
   - Maximize: every die at its maximum.
   - Empower: x1.5 of the roll.
   - Both: the maximum plus half of a normal roll (SPL-097; PHB p.97, verify).
   - SpellCaster.Cast's generic dice move onto it.
4. Convert every custom resolver that rolls spell dice:
   - TryResolveScaledAoEDamageSpell (Fireball, Lightning Bolt, Call Lightning), Ice Storm, Shout, Cone of Cold, Chain Lightning, Circle of Death, Sunburst, Insect Plague, Flame Strike if custom;
   - Searing Light, Vampiric Touch, Melf's Acid Arrow, Produce Flame, Heat Metal, the alignment bursts (ResolveAlignmentBurstSpell), Flaming Sphere;
   - and any other that grep finds (DiceRoller loops inside Spell/).
   Keep exactly one Roll call per original die and the same context strings, so plain casts keep identical hashes.
5. Extend. Handlers that compute their own rounds use SpellDurationRules on the cast's spell, so Extend applies.
6. Heighten Globe. DoesAnyGlobeBlockSpell uses the effective (heightened) level (PHB p.95).
7. SPL-095. MetamagicData.CanExtend uses the same DurationType test as ApplyMetamagicToSpellData.
8. Close SPL-038, SPL-097, SPL-095 and the Heighten part of the Globe issue. rules/cast-empowered-fireball becomes Pass.

**Verification.** 1. Compile: errors=0, warnings=508.
2. Static suites OK. SpellDiceRulesTests and MetamagicSystemTests are updated for SPL-097, with their counts raised in tools/tests/static-suites.json. New static suite: per custom resolver, a dice spec with and without Empower, Maximize and both. The menu-count test passes.
3. rules/spell-damage-mitigation extended with an Empowered and a Maximized Fireball and Searing Light under forced dice. rules/spell-save-dc and spell-durations pass.
4. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Plain-cast hashes are identical; changes only in metamagic jobs.

**Risk.** Medium. A resolver conversion that changes the number of Roll calls shifts every later roll in a seed; identical plain-cast hashes catch it. A MetamagicDataRef leak into menu counts is player-visible; the static test guards it.

### 20. One damage roll per area effect, shared by every target (`raw-area-damage-one-roll`)

Apply decision D6 of the design doc (judge panel 2026-10-15): roll the damage once each time an area damage effect is released (one Fireball detonation, one Lightning Bolt, one Cone of Cold, one breath weapon, one acid spray) and apply that same total to every creature and unattended object in the area; each target then resolves its own spell resistance, save (half rounded down), Evasion or Improved Evasion, immunity, resistance, vulnerability and DR. Empower and Maximize change that one roll. Effects that are separate by their own text keep separate rolls (each Magic Missile missile, and per the decision). Record it in RULES_COVERAGE 4.1 as a delegated reading. Apply it on the shared area core for PCs and NPCs, including breath weapons if they resolve through shared code (otherwise file it for batch 16). Read the full decision in the design doc first.

**Verification.** Static tests: a Fireball over three targets rolls once (forced dice) and each target saves separately; Evasion halves to zero; an empowered Fireball multiplies the one roll. Expect trace changes in area-spell scenarios and the soak; explain them.

**Risk.** Changes every area spell result; scenario expectations that encode per-target rolls must be updated deliberately.

### 21. Make MetamagicSystem the live legality rule and fix metamagic slot spending (`metamagic-legality-and-slots`)

1. MetamagicSystem goes live (SPL-055). Legality calls MetamagicSystem.ValidateMetamagicApplication, and Cost calls GetMetamagicCastingAction. Both live in Spell/Components/MetamagicModifier.cs.
   - Fold step 12's inline rules into them and keep the behaviour: full round for spontaneous casts, no Quicken on spontaneous casts, quickened as free or swift.
   - Delete the MetamagicSystem members nothing calls and their uncalled SpellcastingComponent wrappers.
   - Trim MetamagicSystemTests to the live API and update its static-suites.json counts.
2. Slot selection (SPL-012).
   - CastSpellFromSlot skips LockedByImbue, DisabledByNegativeLevel and HasMetamagic slots for plain casts, and uses slot.CanCast.
   - CastWizardSpellWithMetamagic matches a slot by AppliedMetamagic and drops the last-resort 'any unused slot at the effective level'.
3. Sorcerers and bards (SPL-040). Metamagic casts spend through SpontaneousData at the effective level. The combat metamagic panel's HasSlotAtLevel check uses the spontaneous per-day counts.
4. Cleric conversion (step-1 issue (h)). Spontaneous conversion takes the request's metamagic:
   - the CombatUI convert button (~:2745) passes the chosen metamagic;
   - SpontaneousCastFromSlot spends a slot of the effective level;
   - the cast is a full-round action (PHB p.88; verify).
5. NPC metamagic execution. The AI request uses spell.MetamagicDataRef from the prepared metamagic slots that SpellcastingComponent.GetCastablePreparedSpells creates (~:2055). This removes the NpcNullMetamagic quirk; the AI still chooses no metamagic itself.
6. Scenarios. The Scripted cast step forwards Step.Metamagic (Tests/Scenarios/ScenarioSteps.cs ~:597-606).
7. Close SPL-055, SPL-012, SPL-040, step-1 issues (d) and (h), and the metamagic-execution half of AI-001 (narrow AI-001 to area casting and AI metamagic choice).

**Verification.** 1. Compile: errors=0. Record the warning count; deleting callers of obsolete APIs may lower it, so record any new baseline.
2. Static suites OK: MetamagicSystemTests (trimmed), SpontaneousCastAvailabilityTests and ClericSpellcastingRulesTests.
3. New rules/metamagic-cast-paths:
   - a sorcerer Empower is a full-round cast and spends a spontaneous slot;
   - a sorcerer Quicken is refused;
   - a cleric spontaneous Extend cure;
   - a wizard plain cast does not spend a metamagic slot;
   - a Scripted Empower cast.
4. New rules/npc-cast-prepared-metamagic.
5. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50; explain the changes (NPC prepared metamagic casts, slot selection).

**Risk.** Medium. Making MetamagicSystem live may surface bugs its tests pinned as expected. Slot-selection changes can make a spell uncastable when only a locked slot remains, which is RAW and player-visible.

### 22. Give the AI real caster choices, a legality filter and ordered retries for single-target casts (`ai-single-target-decider`)

AI quality on shared rules. Rules code is not touched.

1. AiCastDecider.FillChoices (AI/Spellcasting/AiCastDecider.cs):
   - Resist Energy and Protection from Energy: the energy type of the visible enemies' known attacks (breath weapon type, energy-damage attacks, last energy damage taken), falling back to Fire.
   - Fire Shield: warm or chill against the same.
   - Magic Weapon, Keen Edge and Greater Magic Weapon: the recipient's wielded primary weapon (Keen Edge only for piercing or slashing).
   - Disguise Self: own race.
   - Keep the heuristics in AI/. Rules code never branches on the decider.
   - This removes the remaining AI-default behaviour behind SPL-112 and SPL-113.
2. Selection. AIService.SelectSpell and SelectBestSpellTarget (and the matching AISpellcastingStrategist code) filter candidates through CheckLegality(SpellCastRequest.ForAI(...)): IsValidTargetForSpell, CasterLevelRules range to the footprint, and the TeamUtility side test instead of `Team ==` (AI-016 at these sites).
3. Retry. TryExecuteSpellcastAction walks the ranked (spell, target) list and tries the next candidate when RunCastSync returns Refused. Cap it at the top 5 (AI-063, single-target part).
4. Unchanged: area, summon and escape spells remain excluded or refused, as today. Do not add or drop candidate categories here, so the soak moves once per feature.
5. Close SPL-112 and SPL-113, the single-target part of AI-063, and AI-016 if no other sites remain (otherwise narrow it). Update docs/systems/AI.md and AI_ACTION_COVERAGE.md.

**Verification.** 1. Compile: errors=0, warnings at baseline.
2. Static suites OK (AIProfileFrameworkTests).
3. New rules/npc-resist-energy-choice: against a red dragon's fire breath, the AI picks Fire; against a white dragon, Cold. New rules/npc-magic-weapon: an NPC cleric enchants an ally's wielded weapon. New rules/ai-spell-retry: the first pick is illegal and the AI casts the second.
4. rules/cast-pair-resist-energy and cast-pair-magic-weapon become Pass.
5. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Record the new soak baseline (wins, wipes, rounds, spells cast by type) in docs/TESTING.md, and spot-check 3 changed seeds with ScenarioTraceDiff.

**Risk.** Medium for AI behaviour. Retries lengthen AI turns, so keep the cap at 5. The energy heuristic must not read hidden information the AI could not know; use visible attacks and damage taken.

### 23. Route area spells by geometry, fix area side filtering and give the shapeless area spells real areas (`area-routing-data`)

This changes behaviour for PC casts of these spells. NPC area casting stays off until step 21.

1. SPL-119. AoESystem.GetTargetsInArea(cells, caster, filter) (Spell/AreaEffects/AoESystem.cs) filters AlliesOnly and EnemiesOnly with TeamUtility relative to the caster and drops the casterIsPC flag. Update every caller.
2. Routing. BeginPendingSpellTargeting and the request builders route a cast to the area resolve when `TargetType == Area || AoEShapeType != None`.
3. SPL-046. Give Chaos Hammer, Holy Smite, Order's Wrath and Unholy Blight their PHB areas (20-ft-radius burst; verify the range and area in the PDF), and Sound Burst and Bane theirs (verify the PDF).
   - Resolve them per target through area registry entries that wrap ResolveAlignmentBurstSpell and the Sound Burst damage-and-stun logic, keeping each target's save, SR and the Globe filter from the core.
   - Remove their single-target registry entries if they become unreachable, and update the registry suite.
4. SPL-042. Give Consecrate, Desecrate, Calm Animals, Wall of Thorns and Persistent Image PHB shapes, so their existing area handlers become reachable. Check each handler works once reached; fix whatever is needed to make it run as written.
5. Regenerate tools/tests/spell-routing-golden.json deliberately, and list every changed spell in the commit body.
6. Close SPL-119, SPL-046 and SPL-042. Update docs/systems/SPELLS (or the matching systems doc) and docs/architecture/spells.md.

**Verification.** 1. Compile: errors=0, warnings at baseline.
2. Static suites OK; the routing golden diff is reviewed.
3. New static AoESystem target-filter tests for Player-team and Enemy-team casters with AlliesOnly and EnemiesOnly.
4. New rules/cast-pair-alignment-burst (Ui), rules/sound-burst-area and rules/consecrate-area.
5. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Changed hashes come only from jobs casting these spells or from Enemy-team area buffs.

**Risk.** Medium. These are player-visible changes to six to eleven spells. A previously dead handler may have its own bugs once reached; fix them in scope, or file them and keep the spell on its old routing with a note.

### 24. Area spells originate at grid intersections (PHB p.175) (`raw-area-origin-intersections`)

Apply decision D5 of the design doc (judge panel 2026-10-15, explicit RAW): every area spell's point of origin is a grid intersection chosen by the caster within range and line of effect; a square is in a burst, emanation, spread, sphere or cylinder when its far edge is within the radius counted from the origin like movement (diagonals alternate 1 and 2); cones and lines start from a corner (intersection) of the caster's space; line of effect is traced from the origin. Apply the decision's delegated sub-readings for creature-centred effects, larger creatures' origins and grid-line cones. Change AoESystem shape generation and the PC area-targeting cursor (snap to intersections, preview the RAW footprint), and the AI aiming (candidate origins at intersections, ally sparing unchanged). Record the delegated sub-readings in RULES_COVERAGE 4.1. Read the full decision in the design doc first.

**Verification.** Static golden tests of footprints: a 20-ft-radius burst covers the PHB p.175 pattern; a 15-ft cone from each corner and direction; a 60-ft line. Rules scenarios for PC and AI Fireball aiming. Regenerate tools/tests/spell-routing-golden.json deliberately if shapes are part of it. Expect soak trace changes wherever area spells occur.

**Risk.** Touches every area spell footprint and the targeting UI; the PC cursor needs a hand play-test (say so in the commit).

### 25. Let NPCs cast and aim area spells through the shared core (AI-001 area half) (`npc-area-casting`)

1. Remove the NpcRefusesArea quirk (the old refusal at NPCTurns ~:1294). The AI builds area requests and runs them with RunCastSync.
2. Aim search. Add AI/Spellcasting/AreaAimSearch.cs:
   - Candidate origins are cells within CasterLevelRules.RangeSquares: for bursts, enemy cells and their neighbours; for cones and lines, 8 directions from the caster. Cap at about 200 candidates.
   - Footprints come from AoESystem.GetBurstCells, GetConeCells and GetLineCellsToTarget, and the same line-of-effect filter as Legality.
   - Score = enemies hit minus a weight times allies hit, using TeamUtility, plus the spell's expected value from the strategist.
   - Rewrite SpellcasterAIProfile.EvaluateAOECast and GetAreaDenialScore on top of the found placement's value. If no placement is legal or worthwhile, drop the spell; there is no fallback.
3. Decider capability. AiCastDecider.CanFill reports false for the wall endpoints (Wall of Fire, Wall of Ice, Wall of Force), Grease object mode and the Summon Swarm placement. CheckLegality refuses those requests because the choices are incomplete. This is a decider capability, not a rule fork. Record them in AI.md as AI follow-ups.
4. SelectSpell candidates now include area spells, within the step-19 retry cap.
5. New scenarios:
   - rules/npc-cast-fireball: an Enemy-team wizard spares its allies and hits the party.
   - rules/npc-cast-bless: allies only.
   - rules/npc-cast-web.
   - rules/ai-area-aim: a clustered party with an adjacent ally; the burst avoids the ally and the cone is aimed at the cluster.
6. Close the area-casting half of AI-001 and the area part of AI-063. Update docs/systems/AI.md, AI_ACTION_COVERAGE.md and PC_NPC_PARITY.md.

**Verification.** 1. Compile: errors=0, warnings at baseline.
2. Static suites OK. A new AreaAimSearch static suite with a fixed grid layout checks that the scores and the chosen origin are deterministic.
3. The new scenarios pass.
4. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Rules jobs without enemy area casters are identical. Record the new soak baseline (wins, wipes, rounds, area casts per encounter) next to step 19's numbers in docs/TESTING.md, and spot-check 3 seeds so their first divergence is an NPC area cast.

**Risk.** High balance impact: enemy Fireball, Web and Sleep will wipe low-level parties more often, which is the RAW result. Aim-search cost per turn is bounded by the candidate cap. Burst-origin and per-target damage questions are open with the owner; keep the current geometry.

### 26. Mirror Image by RAW: a random figure per attack, no swap (`raw-mirror-image`)

Apply decision D7 of the design doc (judge panel 2026-10-15): Mirror Image (PHB p.254) creates 1d4 + 1 per three caster levels images (maximum 8) that move with the caster and occupy no squares; each attack against the caster picks uniformly at random among the remaining images plus the caster (chance 1/(N+1) for the caster); an image hit by an attack is destroyed; follow the decision for touch attacks, area effects, spells that need no roll, attackers that cannot see (blindsight etc.) and how an attacker who learns the real figure is handled. Remove the end-of-turn swap (TryBeginMirrorImageSwapSelection, the NpcNoMirrorImageSwap bit, the IsControllable gate, AI-036's map-wide priority target if it depends on the swap) and close SPL-117. Same for PCs and NPCs, attacker and caster. Record the delegated sub-readings in RULES_COVERAGE 4.1. Read the full decision in the design doc first.

**Verification.** Static tests with forced dice: attack distribution over N images, destruction, the caster hit on the right roll; PC and AI attackers alike. Update Mirror Image scenarios; expect trace changes where Mirror Image appears.

**Risk.** Removes a PC-facing UI flow (the swap); docs and UI strings must be updated.

### 27. Let NPCs summon, use Dimension Door, deliver held charges and swap Mirror Image figures (`npc-summon-escape-held-mirror`)

1. Summons (SPL-091). AiCastDecider fills the summon choices:
   - the option from SummonMonsterLists at the slot's list level (highest CR that is useful for its side);
   - the count via the existing RollSummonCreatureCount logic;
   - placement via FindBestAdditionalSummonCell.
   The core's ResolveSummon inserts initiative entries exactly as the PC path does. Watch the index-parallel _npcAIBehaviors list (AI-015) and slot reuse (CRE-046): summon state must be cleared on reset.
2. Dimension Door. Choices.Destination comes from the existing escape scoring (FindDimensionDoorDestination, or the AI retreat logic). Remove the step-6 AI exclusion.
3. Held charges (SPL-115):
   - AiCastDecider.ChooseTouchDelivery returns 'cast now' when the target is in reach, else 'hold' only if the planner can move to touch this turn.
   - When SpellcastingComponent.HeldTouchSpell is set, the AI adds a deliver-held-charge candidate: move into reach, then a delivery request (no action in the round of casting; after it, the attack action or one attack of a full attack, as for the PC: SPL-156, delegated RAW reading 2026-10-14). A miss keeps the charge for both sides (PHB p.141).
   - Multi-turn approach planning is AI-047; keep holding off if the planner cannot reach.
4. Mirror Image: nothing to do here. Step raw-mirror-image (before this step) replaced the swap with the RAW random-figure model for both sides, so NPC casters use Mirror Image like any buff.

5. Close SPL-091, SPL-115 and SPL-117. Update docs/systems/AI.md, PC_NPC_PARITY.md and AI_ACTION_COVERAGE.md.

**Verification.** 1. Compile: errors=0, warnings at baseline.
2. Static suites OK (Summon tests).
3. New scenarios: rules/npc-summon-monster (a Scripted cleric casts Summon Monster II; the creature joins initiative on the caster's side and acts), rules/npc-dimension-door, rules/npc-held-touch (an AI Shocking Grasp miss, then delivery next round), rules/cast-pair-mirror-image.
4. rules/summon-creature-entries and smoke/two-goblins-ai pass.
5. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Record the new soak baseline in docs/TESTING.md.

**Risk.** Medium to high. NPC summons add actors mid-fight (initiative and parallel-list bookkeeping) and raise difficulty. Moving the Mirror Image swap to a shared turn end must not double-trigger for PCs.

### 28. Let the AI choose metamagic and fix its spell-resistance estimate (closes AI-001) (`ai-metamagic-choice`)

1. Metamagic candidates. AiCastPlanner (AI/Spellcasting/; extend AISpellcastingStrategist if the planner lives there) adds a metamagic dimension.
   - Spontaneous casters: for each (spell x known metamagic feat) from SpellcastingComponent.GetKnownMetamagicFeats, filter through CanCastWithMetamagic and MetamagicSystem.ValidateMetamagicApplication, then CheckLegality.
   - Prepared casters: use their prepared metamagic slots as listed.
   - Value Empower and Maximize by the expected-damage delta through SpellDiceRules (average), against the full-round cost for spontaneous casters. Value Extend by the duration gain for buffs, Quicken by the extra action, and Heighten by the save DC and Globe penetration.
   - Replace GetMetamagicScore.
   - Respect the retry cap.
2. AI-046. Fix the SR estimate to `(21 + effectiveCL - SR) / 20`, clamped to 0..1 (AISpellcastingStrategist.cs ~:431-440).
3. Close AI-001 and AI-046. Update docs/systems/AI.md and AI_ACTION_COVERAGE.md.

**Verification.** 1. Compile: errors=0, warnings at baseline.
2. Static suites OK. A new planner static test checks that Empowered Fireball outranks plain Fireball for a sorcerer when expected damage justifies the full round.
3. New rules/ai-metamagic-choice (the couatl with Empower, or a sorcerer).
4. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50. Record the baseline.

**Risk.** Medium. More AI turn time is bounded by the cap. Sorcerer enemies get stronger, which is the RAW result.

### 29. Delete the legacy cast code and dormant services, and bring the cast docs up to date (`retire-legacy-cast-code`)

1. LegacyCastQuirk. Delete every remaining bit (NpcNoComponentCheck was already removed in step raw-components-all-casters, decision D3). Keep that bit with its SPL-114 reference, or delete the enum entirely if the owner has ruled and a commit implemented the ruling.
2. SPL-056.
   - Delete SpellApplicationService and its GameManager registration.
   - Trim SpellResolutionService to the Blink and SR helpers the core uses, or fold them into the core and delete the service.
   - Port SpellResolutionServiceTests' still-relevant Blink and SR assertions into CastCoreStageOrderTests or a new suite.
   - Update tools/tests/static-suites.json (removed and added counts).
3. Forwarding properties. Convert the reads in GameManager.CastCore.cs, GameManager.SpellHandlerRegistry.cs and the stage helpers from the `_pending*` properties to ctx members. The remaining `_pending*` properties become read-only views of the current cast (no setters outside the request builders). Document them in docs/architecture/spells.md as 'the current cast's request'. Grep that no rules file assigns them.
4. Wish (step-1 issue (k)). Leave it as filed: 9th level is outside scope.
5. Docs:
   - CLAUDE.md 'Architecture in brief' item 5: one cast core with Single, Area, Summon and HoldCharge resolve variants, one handler registry, and requests from every source.
   - CLAUDE.md 'Traps that bite': the SPL-054 and ApplySpellBuff lines now point to the registry and SpellEffectRouting.
   - docs/architecture/spells.md and the docs/DEVELOPMENT_RECIPES.md 'Add a spell' recipe (a registry line, routing, scenario).
   - docs/systems/PC_NPC_PARITY.md step 9 marked done, with what remains (the NPC components ruling).
   - KNOWN_ISSUES counts and Top issues.
6. Close SPL-056.

**Verification.** 1. Compile: errors=0. Record the warning count; update the CLAUDE.md baseline if it changed.
2. Static suites OK, with the static-suites.json entries consistent.
3. Grep: no file outside Spell/Casting/Core, the request builders and UI assigns `_pendingSpell`; SpellApplicationService is gone.
4. QueueFresh rules/* and smoke/* seeds 1-3, and soak 1-50: hashes identical to step 23's run, since this is a pure cleanup.

**Risk.** Low to medium. Deletions touch TST-006 reflection pins, so grep Assets/Scripts/Tests first. A forgotten writer of a `_pending*` property shows up as a compile error once its setter is removed.

## Behaviour changes

The structural steps 4-10 keep every fight identical. The only exception is in step 4: stale PC choices and stale scroll, wand or F12 flags no longer leak into later casts. All other changes come from the fix and AI steps, each with its own explained trace diff.

**Intended (RAW or parity):**

**Enemy (NPC) casts:**
- Every special spell effect now works for NPC casts, which gain the 32 they lacked: Silence areas, Death Knell, Spiritual Weapon, Ghoul Touch paralysis, Remove Curse, Remove Blindness/Deafness and more.
- NPCs track and lose concentration spells, and they make the casting-while-concentrating check (current rule, pending your ruling).
- NPCs cast prepared metamagic spells, choose metamagic, pick sensible energy types and weapons, and retry another spell when one is illegal.
- NPCs cast aimed area spells that spare their allies; walls, Grease object mode and Summon Swarm stay player-only for now.
- NPCs summon monsters, use Dimension Door, deliver held touch charges and swap Mirror Image figures.

**Both sides:**
- A Globe of Invulnerability blocks covered spells before any damage or healing, on area spells too, and at the heightened level.
- The Sound Burst stun and Animate Rope no longer land when the spell failed.
- Quickened spells do not provoke and are limited to one per round in the rules core. Quicken cannot be used on spontaneous casts.
- Spontaneous metamagic is a full-round action because the cast is spontaneous, not because of the caster's class.
- The casting-while-concentrating check happens after the attack-of-opportunity Cancel point.
- Entangled casting is a Concentration check, DC 15 + spell level, for every spell.
- Deafness is rolled once per cast, Silence blocks verbal spells, ASF applies only to somatic spells, and scrolls roll ASF if DMG p.238 confirms it.
- Blink applies on every path.
- Cancelling a threatened scroll or wand cast refunds the item and the action, and a slot-lookup failure refunds the action.
- Paladin and ranger caster level is half their class level, and the range highlight uses caster level.
- The counterspell, Ring of Counterspells and invisibility order is the same for everyone. A held charge can be countered when cast.
- The spell component pouch check runs on every player cast path, area, summon and Grease included.
- Allies caught in a harmful area roll their saves instead of auto-failing.
- Spell Penetration counts against SR for Sleep, Color Spray, Hypnotism, Deep Slumber and Fear.
- Spell-specific save bonuses (e.g. Charm Person +5) apply at every spell save.
- Empower and Maximize apply to Fireball and every custom damage spell; Empower plus Maximize is the maximum plus half a roll.
- Extend works on hard-coded durations and is offered only where it works.
- Plain casts no longer spend locked or metamagic slots.
- Sorcerer, bard and cleric-conversion metamagic works.

**Spell areas:**
- Chaos Hammer, Holy Smite, Order's Wrath, Unholy Blight, Sound Burst and Bane become real areas.
- Consecrate, Desecrate, Calm Animals, Wall of Thorns and Persistent Image become reachable.
- Area buffs and Enemies-only spells cast by enemies hit the correct side.

**Risked (watched in each batch):**
- The soak re-baselines at steps 6, 19, 21, 22 and 23, and enemy casters become much more dangerous.
- A converted dice roller that changes its roll count would shift every later roll; the identical plain-cast hashes guard this.
- The UI prompt flow (AoO prompt, choice dialogs, wall prompts) could resume at a different point in step 7 or 9; the harness prompt steps guard this.
- Newly reachable handlers may expose bugs on AI turns.
- Summons add initiative bookkeeping (AI-015, CRE-046).
- Tests that reflect on private names (TST-006) can break at runtime if a rename slips through.

**Not changed:**
- per-target damage rolls;
- the burst origin on the cell centre;
- Wish;
- NPC component pouches (still exempt, pending your ruling);
- the per-round save for Hold Person and Hold Monster.

## Not in scope

- SPL-110 (no per-round Will save against Hold Person or Hold Monster): it needs the shared end-of-turn save hook from PC_NPC_PARITY step 3. Schedule it as its own commit after this batch; the save DC and metamagic are already on the cast context.
- Wish (9th level, outside the 1-8 scope): filed in step 1 as spending nothing; not routed through the core.
- SPL-057 data audit (SR flags on the 148 unflagged spells; mind-affecting and fear status from descriptors): step 16 unifies only the SR roll.
- AI choosers for wall endpoints (Wall of Fire, Ice, Force), Grease object mode and Summon Swarm placement: the AI is refused these until choosers exist.
- Multi-turn AI planning to approach and deliver a held touch charge (AI-047), beyond moving into reach this turn.
- NPC use of scrolls, wands and other consumables (AI-009), although the request's CastSource now supports item casts.
- Persistent area effects (Web, Stinking Cloud and similar) acting on a creature that later enters a globe.
- SPL-019 and SPL-098 quicken UI details beyond moving the once-per-round check into the core.
- SPL-110 per-round saves against Hold Person and Hold Monster: after this batch, with the shared end-of-turn hook.

## Questions the synthesis raised, and how they were settled

- Casting while concentrating. Today a caster keeps an older Concentration-duration spell by passing a DC 15 + spell level check when casting a new one; the PHB has no such check. After this batch the check applies to NPCs as well. Options: (a) any number of spells may be cast as long as concentration is maintained with a standard action each round (PHB p.176); (b) casting a new spell ends the old one; (c) keep the current check as your interpretation.
- Should maintaining a Concentration-duration spell cost a standard action every round (PHB p.176), for PCs and NPCs alike? Today nothing charges it except bardic music.
- NPC spell components. Should class-level NPC casters be assumed to carry a spell component pouch and a divine focus? Do monsters that cast as sorcerers (dragons, MM p.69; the couatl) need material components? Should Eschew Materials be implemented? Until you rule, NPCs stay exempt from the pouch check. This is the one PC/NPC rules difference left after the batch, and it is recorded in SPL-114.
- Does a hostile spell that is counterspelled, or stopped by a Ring of Counterspells, still end the caster's invisibility (PHB p.245)? The plan uses the current player order for everyone: counterspell first, and no invisibility break when the spell is countered.
- Burst origin: PHB p.175 puts it on a grid intersection, but the code centres bursts on a square, which makes each burst one square wider. Change this (it alters every burst footprint and the AI aiming), or keep it?
- Area damage: the core books don't say whether one Fireball damage roll applies to every target or each target gets its own roll. The code rolls per target. Keep that as your interpretation?
- F12 test panel: keep all its bypasses (no ASF, no pouch, free costly components, unlimited slots) as explicit test-panel flags, or drop some of them?
- Mirror Image: PHB p.254 says each attack targets a random figure. The code instead lets the caster swap figures at the end of the turn, and this batch gives that swap to NPCs as well. Keep the swap model, or replace it with RAW random targeting for both sides in a later batch?

Settled 2026-10-15: the seven rules questions by decisions D1-D7 above; the F12 bypasses stay as explicit test-panel flags.

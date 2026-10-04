> Status (2026-10-03): proposal, nothing built yet.

# Enemy AI: knowledge tags and blended personalities

Scope: this design answers the two owner requests. The first is "visual" knowledge tags that knowledge rolls and between-battle intel can improve, for both sides. The second is many AI profiles, with several weighted profiles on one enemy. Paths are relative to `Assets/Scripts/` unless they start with `docs/`. Line numbers are at `3d11fd1` on `docs/cleanup-2026-10`. This document comes from four code-reading research memos, three competing designs and three judge reviews. Nothing in it was checked in Play mode.

---

## 1. Summary

**What to build:**

- **Personalities.** Each enemy keeps its one existing AI profile, which still owns the turn routine (melee, kiter, healer, dragon, swarm). We call this its **Role**. On top of the Role it gets a list of weighted **traits** such as `Cowardly:0.6, Healer:0.4`.
  - Each trait is a data row in a `TraitDatabase`. A row holds weights over a shared library of about 20 target considerations, plus opinions on yes/no decisions (charge, coup de grace, provoke an AoO), a few temperament dials, and a minimum Intelligence.
  - Adding trait number 30 means adding a row, not writing a class.
- **Knowledge.** Every AI decision, and later the enemy panels the player sees, read targets through one `TargetView` facade, never through `target.Stats`.
  - The first view provider is omniscient, so it behaves like today.
  - A second provider, built later, answers only from what the observing side legitimately knows. That covers what it can see, what it has observed in combat, what it learned from Knowledge, Spellcraft or Sense Motive checks, and what it learned before the battle.
  - Knowledge is stored as discrete 3.5-style levels, not probabilities.
- **Symmetry.** The same view code serves the AI (the enemy side's knowledge) and the UI (the party's knowledge). One **Information Mode** setting, `Omniscient` or `Rules`, switches both.

**Why this shape:**

- Each enemy's existing concrete profile class stays, so all eight places that check `is HealerAIProfile`, `is DragonAIProfile` and so on keep working untouched.
- An NPC with no traits takes exactly today's code path, so nothing regresses.
- Traits arrive first, on the omniscient view, which means you see Cowardly, Sadistic and Smart in play before any fog-of-war work.
- The knowledge layer then plugs in behind the same facade without rewriting the traits.

**Order of work:**

1. Prerequisite fixes.
2. Increment 1: weighted traits.
3. Increment 2: fair sight (perceived knowledge).
4. Increment 3: stances, withdraw and healer movement.
5. Phase 4: knowledge rolls, hub intel and encounter-seeded enemy intel.
6. Later phases: divination, enemy "demeanor" tells, and a unified action evaluator.

**Honest effort estimate:**

- Increment 1 is medium: about 15 new files and about 20 edited call sites.
- Increments 2 and 3 are each large. Increment 2 touches the attack, cast and damage pipelines and the UI. Increment 3 depends on combat-rules fixes (CMB-073, AI-047, AI-010).
- Phase 4 needs data work: skill ranks for monsters.

---

## 2. What exists today

### 2.1 Your earlier profiles

**The Sadistic, Cowardly and Smart profiles are not in this repository.** Searches covered:

- the working tree (`*.cs`, `*.asset`, `*.json`);
- every local and remote-tracking branch and all 1,056 commits (`git log --all -S`);
- `stash@{0}`;
- sibling Unity projects under `F:/` and `D:/`.

The only "smart" hits are comments in `AI/Profiles/DragonAIProfile.cs:296, 329`. Not checked: unreachable git objects, unfetched remotes, and other drives or OneDrive. If those profiles exist somewhere, send them and their logic becomes trait rows (§4.4).

What does exist is 19 profile classes:

- Directly under `AIProfile`: Animal, Humanoid, Berserk, Grappler, Ranged, Spellcaster, Dragon, SwarmAI, UndeadMindless, UndeadTactical, UndeadBrute, UndeadIncorporeal.
- Under Spellcaster: Healer, Evoker, Abjurer, Necromancer, Lich, Vampire.
- Under Swarm: IndiscriminateSwarm.

`HealerAIProfile` is only reached by DMG Cleric spawns, and those have no spells (ENC-021).

### 2.2 Profile machinery we build on

- **One profile per NPC.** It lives in `CharacterController.aiProfile` (`Character/Controller/CharacterController.cs:310`). `GameManager.BuildRuntimeAIProfile` (`_Core/GameManager.NPCSetup.cs:758-816`) builds it from one `NPCDefinition.AIProfileArchetype` (`Character/Creatures/NPCDatabase.cs:627`) and assigns it at `NPCSetup.cs:748`.
  - `Brute` and `Caster` fall through to `null` (AI-004).
- **Virtual hooks.** `AIProfile` (`AI/AIProfile.cs`) exposes these, called from about 20 sites in `Services/AIService.cs` and `_Core/GameManager.NPCTurns.cs`:
  - `ScoreTarget`
  - `ShouldPreferCharge`
  - `ShouldUseCoupDeGrace`
  - `ShouldIgnoreAoO`
  - `ShouldIgnoreUnconsciousTargets`
  - `ShouldSwitchTargetsMidFullAttack`
  - `ShouldTakeFiveFootStepToContinueFullAttack`
  - `GetPreferredManeuver`
  - `GetRangedAoORiskToleranceMultiplier`
  - `ShouldInitiateGrapple`
- **Plain fields read directly.** Callers read these fields rather than calling a method: `Movement`, `CombatStyle`, `TagPriorities`, `PrioritizeWounded`/`PrioritizeIsolated`, `Maneuvers`, `GrappleBehavior`.
- **Routine choice.** It depends on two things:
  - The concrete class: `is SwarmAI`, `is HealerAIProfile` and `is DragonAIProfile` at `AIService.cs:183, 197, 260`.
  - `NPCAIBehavior`, read through `GetNPCBehaviorForAI` (`NPCTurns.cs:62`) from the parallel list `_npcAIBehaviors`.

  Other type checks are at `AIService.cs:2518`, `AISpellcastingStrategist.cs:1506`, `GrappleSystem.cs:1812, 1836` and `GameManager.SpellCasting.cs:777`.
- **Mismatched scales.** Profile scores are on very different scales. `UndeadMindlessAIProfile.ScoreTarget` returns `100000 - 1000*dist`, Swarm returns `100 - dist`, and the base profile returns about 10-40. The perception and concealment adjustment (±50 to 120, `AIService.cs:1925-1997`) swamps every profile term.
- **Tags are global ground truth.**
  - `CharacterTags` is filled by `StatusTagManager` with `Race:` (from `DisplayedRace`, so Disguise Self works), `Class:` (the true class), `HP State:`, `Status:` (every condition, including Charmed), `Wielding:` (item names, which can include "+1") and `Armor:`.
  - `AIProfile.TagPriorities` matches these with a two-way substring test (`AIProfile.cs:324-347`), so `Elf` matches `Race: Half-Elf`.
  - `HP State: Staggered` is the 3.5 *nonlethal* state (`CharacterController.cs:4431`). Several profiles weight it as "low HP", so those weights almost never fire.
- **The AI is omniscient.** It reads exact HP, AC, touch AC, all three saves, class, level, SR, immunities, and the true square of unseen targets. The places are:
  - `AIService.GetTargetPriority` (2010), `CalculateThreat` (2038) and `SelectLowestHPEnemy` (2612);
  - base `ScoreTarget`;
  - the Vampire, Undead, Dragon, Lich and Spellcaster overrides;
  - `AISpellcastingStrategist.GetSaveExploitScore` (544) and its SR and resistance reads.
- **The player is also omniscient.** `CharacterInfoPanel.UpdateCharacterStats` (145-240) shows exact HP, the AC breakdown, BAB and six ability scores. The hover tooltip shows SR and immunities. The combat log prints "vs AC N" and "HP before -> after".
- **Partial, mostly dead knowledge pieces.**
  - `LastKnownPositionTracker` stores positions only and rolls Listen against a flat DC 20 on every call (AI-007). A duplicate store sits at `CharacterController.cs:546` (AI-021).
  - The strategist's learning memory has writers with no callers (AI-011).
  - `RollSpellcraftIdentification` (`CharacterController.cs:12204`) exists and is used only for counterspelling.
  - Events such as `AttackResolvedEvent`, `SpellCastEvent` and `DamageTakenEvent` are declared but never published.
  - **NPCs have no skills.** `InitializeSkills` runs only on PC paths, so every trained-only check fails for monsters.

---

## 3. Knowledge tags

### 3.1 Data model

A "tag" becomes a **fact**: a typed key with a value, held by a side about a subject, plus how it was learned.

```csharp
// AI/Knowledge/KnowledgeTypes.cs
public enum KnowledgeLevel : byte {
  Unknown = 0,
  Apparent = 1,   // seen or heard now, no check: armor category, size, prone, wound band
  Inferred = 2,   // reasoned from behaviour (saw it cast -> spellcaster); the only level that can be wrong
  Recalled = 3,   // made a Knowledge / Spellcraft / Sense Motive DC
  Confirmed = 4 } // divination, or a measured outcome (fire did 0 damage)
public enum KnowledgeSource : byte { Sight, Hearing, Scent, Behaviour, AttackOutcome, SaveOutcome,
  DamageOutcome, KnowledgeCheck, SpellcraftCheck, SenseMotiveCheck, SpotVsDisguise, Divination,
  Scouting, GatherInformation, EncounterIntel, AllyCallout }
public enum FactKey : ushort {
  Size, Species, ArmorCategory, ShieldCarried, WieldsMelee, WieldsRanged, WieldsReach,
  DivineFocusVisible, SpellbookVisible, WoundBand, Down, Prone, Grappled, Helpless, Fleeing,
  VisibleEffect, CreatureType, Subtype, CastsArcane, CastsDivine, KnownSpell, ClassRole,
  ACAtMost, ACMoreThan, SaveTendency, ImmuneTo, ResistsEnergy, HasDR, HasSR, Vulnerability,
  SpecialAttack, Charmed, Dominated, Demeanor, HPExact /* deathwatch only */ }
public readonly struct KnownFact { FactKey Key; int SubjectId; FactValue Value;
  KnowledgeLevel Level; KnowledgeSource Source; int LearnedRound; bool Stale; }
```

**Why discrete levels.** The judges preferred discrete levels to the float confidence used in two of the designs:

- 3.5 has no probabilistic knowledge: you made the DC or you did not.
- Discrete levels show up cleanly in the UI as icons (eye for seen, book for recalled, sword for learned in combat).
- They are easy to gate by Intelligence.

**Scopes.**

| Scope | Holds | Storage |
|---|---|---|
| Live, per observer | Apparent facts, computed on demand from ground truth, gated by `CanPerceive(observer, subject)` | Not stored; cached per (observer, subject) per turn |
| Side store (`SideKnowledgeStore`, one per team) | Inferred, Recalled and Confirmed facts in this encounter, plus last-seen snapshots of Apparent facts (`Stale = true`) | Keyed by `(EncounterEntityId, FactKey)` |
| Lore (`LoreBook`) | Species facts ("orc: humanoid (orc), light sensitivity") | Party Bestiary for the session; enemy faction books later |

**Keys.** The 15 NPC slot GameObjects are reused, and the hub's Back button re-spawns into the same slots. So facts are keyed by a new `CharacterController.EncounterEntityId`, assigned in `InitializeNPCFromDefinition` (`NPCSetup.cs:445`). Species facts are keyed by `LoreKey`, which is `SourceNpcDefinitionId` (`NPCSetup.cs:501`) with the `spawn_` prefix stripped. The side store is reset in `SetupEnemyEncounter` and `ResetCombatStateForNextEncounter`, **not** in `OnCombatEnded`, because an ordinary victory never reaches it (CORE-002).

**Sharing.** A creature with Int 3 or more that can speak and is not silenced shares its learned facts with its side at the end of its turn (talking is a free action). Animals share positions only, and mindless creatures share nothing. PCs always share. The rule is the same for both teams.

### 3.2 Sources

| Source | What it gives | Rules | When |
|---|---|---|---|
| **Visual** (`VisualFactExtractor`) | Size, Species as it appears, armor *category* (from `ItemData.VisualTags`, never the item name), shield, weapon kind and reach (base item, no "+1"), holy symbol or spellbook visible, wound band, visibly apparent conditions, visible spell effects (Blur, Mirror Image, Displacement, Fire Shield, Enlarge, Haste, the Shield disc) | Conditions come from a **whitelist** in `ConditionVisibility`: prone, grappled, helpless, paralysed, entangled, fleeing and blinded are visible; Charmed, Poisoned, EnergyDrained, Feinted and the curses are not. Incorporeal creatures count as visible (today a 50% miss chance makes them "unseen"). | Inc 2 |
| **Observed in combat** (`KnowledgeService.RecordOutcome`, fed by published events) | A hit with total *t* gives `ACAtMost(t)`. A miss with total *t* (excluding natural 1 and 20) gives `ACMoreThan(t)`. Damage ignored or reduced gives `ImmuneTo` or `ResistsEnergy`. Save outcomes give `SaveTendency` (Inferred). Casting gives `CastsArcane`/`CastsDivine`. Rage, sneak attack and turning give `ClassRole`. | This is how 3.5 characters learn "that one is hard to hit". It replaces the dead `RecordSpellSaveSuccess` and `RecordIneffectiveDamage` (AI-011). | Inc 2 for AC and roles; phase 4 for saves and immunities |
| **Checks** (`IdentificationService`) | Knowledge, by creature type: DC 10 + HD, trained only, 1 fact plus 1 per 5 points over the DC, drawn from the species lore list in order (type, worst special attack, defences, vulnerabilities, save tendencies, senses). Spellcraft: 15 + spell level for a spell being cast, 20 + level for an effect already in place, 25 + level after saving against it. Sense Motive: 25 to notice an enchantment (15 if dominated). Spot vs Disguise reveals the true Species. | Knowledge uses no action and allows no retry. The skill by type is arcana, dungeoneering, local, nature, religion or the planes, per the PHB table. No core rule lets Knowledge reveal a PC's *class*. Monsters need skill ranks first (§6). | Phase 4 |
| **Party, between battles** (pre-combat hub) | "Study Foes": Knowledge on each species in the already-spawned roster, take 10 allowed. "Scout": best Hide + Move Silently vs the enemies' best Spot/Listen; success gives the Apparent facts for the whole roster; **failure raises the enemy intel level**. "Gather Information": optional and labelled a house rule, because RAW needs a settlement and 1d4+1 hours and the game has no clock. | Each action spends a **preparation budget** (abstract hours per encounter: 0-1 for a wandering monster, 4-8 for a known lair), so research is a choice rather than click-everything. Uses the party-best pattern from `TreasureItemConverter.FindBestAppraiser` (52-80). | Phase 4 |
| **Enemies, between battles** (encounter-seeded) | `EncounterIntelLevel`: **Unaware** (default) gets nothing. **Alert** (failed party scout, or a lair) gets the Apparent facts for every PC. **Watched** (sentries) adds behaviour facts, such as the arcane caster who pre-buffed. **Informed** adds faction memory of named PCs; later, fleeing survivors report back to the faction. | Seeded at `StartCombat` (`GameManager.cs:3632`, beside `ResetCombatState` at 3732). That is the first point after the hub, so it reflects gear the party changed there. | Phase 4 |
| **Divination** | *Deathwatch* gives `HPExact` as a band; *detect evil/magic* give aura facts | The spells are placeholders today (`SpellDatabase_D.cs:146, 316`) | Later |

### 3.3 Intelligence tiers

`CognitionTier` is derived from `NPCDefinition.INT` and `IsMindless`. **The tier caps reasoning, not perception.** `TargetView.TryGet` returns false for any fact key above the observer's tier, and trait considerations above the tier contribute 0 (the trace shows them as "skipped (tier)").

| Tier | Int | MM examples | May reason with |
|---|---|---|---|
| Mindless | — | zombie, skeleton, ooze, vermin | Position, Down, Size |
| Animal | 1-2 | wolf, bear | + WoundBand, Prone/Helpless/Grappled, Fleeing, "who hurt me" |
| Low | 3-7 | ogre, troll | + all Apparent facts; Inferred caster status only if it saw the cast this round |
| Average | 8-12 | orc, goblin, gnoll, bugbear | + all Inferred facts, its own Recalled facts, AC bounds |
| Keen | 13-16 | hobgoblin captain, most casters | + side facts, simple inferences ("robed arcane caster → weak Fort") |
| Brilliant | 17+ | adult dragons, liches | everything known, plus counter-play (dispel a known buff, avoid a known immunity) |

`AIService._difficulty` is unread today (AI-012). It could later mean "play one tier lower" on Easy. It never grants knowledge a creature does not have.

### 3.4 Fairness and symmetry

- **One API for both sides.** `KnowledgeService.GetView(observer, subject)` is the only way the AI or the UI reads enemy data. `GameManager.UpdateAllStatsUI` (`GameManager.cs:2983`) already computes an `observer`; it just has to pass it on.
- **Party UI in Rules mode.**
  - `CharacterInfoPanel` and `CharacterHoverTooltipUI.BuildTooltipText` (237-298) render Species, Size, wound band, armor category, "AC 15-17 (observed)", and known facts with source icons. Unknown values show as "?".
  - The combat log replaces "vs AC N" (`Combat/Core/CombatResult.cs:325, 495, 511-513, 619`) with hit/miss, and shows enemy HP as wound-band transitions.
  - An enemy's spell name appears only after a Spellcraft success.
- **Wound visibility is a house rule, and labelled as one.** The core books have no rule for seeing HP. The setting is `Strict` (Down only), `Coarse` (Unhurt / Hurt / Badly hurt below 50% / Down), the default, or `Fine` (five bands). Allies always see each other's Fine bands, and each creature knows its own exact HP. This replaces the misuse of `HP State: Staggered`.
- **Enemy-view overlay** (debug key): hover a PC to see what the enemy side currently knows about it. This is the player's evidence that the AI is not cheating.
- **Default is `Omniscient`** until play-tested with traces. Control flags (`AI:MagicMissileOnly`, `ScenarioOverride:*`, `Uses Armor-Based Targeting`) move out of `CharacterTags` into an `AIControlFlags` set and are never facts. `StatusTagManager` stops emitting `Class:`.

---

## 4. Personalities

### 4.1 Model: one Role plus weighted traits

```csharp
// AI/Personality/TraitDefinition.cs  (plain C#, stateless, one shared instance per id; not a ScriptableObject)
public sealed class TraitDefinition {
  public string Id;                                   // "cowardly"
  public CognitionTier MinTier;                       // hard gate
  public AlignmentHint Alignment;                     // warning only (needs CRE-002)
  public NPCAIProfileArchetype? ImpliedRole;          // Healer -> HealerAIProfile, Caster -> SpellcasterAIProfile
  public (ConsiderationId id, float w)[] Target;      // Σ|w| = 1, each consideration in [0,1]
  public OpinionRule[] Opinions;                      // (BoolDecision, Opinion, ConditionId, param)
  public PersonalityParams Params;                    // RiskAversion, Cruelty, MoraleBreak, Discipline, Persistence
  public (Stance s, float desire)[] Stances;          // used from increment 3
  public int PreferredRangeAdjust;                    // never averaged; applied to the Role's range
  public bool ExclusiveLock;                          // Mindless
}
public enum Opinion : sbyte { Forbid = -2, Against = -1, Neutral = 0, For = 1, Insist = 2 }
```

**Role.** The Role is today's concrete profile, still stored in `aiProfile` and built by `BuildRuntimeAIProfile`. It is chosen as follows:

- the explicit `AIProfileArchetype`, if one is set;
- otherwise the highest-weight trait that has an `ImpliedRole` and whose capability the creature has (cure spells for Healer, spell slots for Caster);
- otherwise Humanoid, or Animal for animals.

Traits never own routines. This avoids a composite profile that would fail the eight concrete-type checks.

**Conditions** are a closed set of ids with parameters, not lambdas, so they stay serialisable and tunable: `Always`, `SelfWoundAtLeast(band)`, `SelfThreatened`, `NoEnemyThreatensSelf`, `AlliesDownFraction(x)`, `TargetDown`, `StandingThreatInReach`, `WardStanding`.

**`PersonalityValidator`** runs at build time:

- An unknown trait id, or a trait below its `MinTier`, is dropped, the remaining weights are renormalised, and one warning is logged per definition.
- `ExclusiveLock` (Mindless) removes every other trait.
- An alignment mismatch only logs a warning.

### 4.2 Blending rules

The NPC's weights are normalised so that Σw = 1. One rule applies per kind of decision:

- **Target scores: a linear blend, folded at spawn.**
  - Effective weight per consideration: `e_j = Σ_i w_i · traitWeight_ij`. This is computed once in `PersonalityProfile.Build`.
  - Per candidate: `Score = RoleProfile.ScoreTarget(t, self) + K · Σ_j e_j · cons_j(view)`, with `K = 20`.
  - The trait term is bounded to ±20, enough to reorder targets within the role's usual 10-40 band but below the ±50-120 perception tiers, so "visible beats concealed" still holds.
  - The role's own base terms are counted once (no double counting), and the cost does not depend on how many traits an enemy has.
  - Invalid targets (`float.MinValue`) are filtered out before blending.
  - We do **not** min-max normalise: that would turn a trivial preference into a full-scale swing.
- **Yes/no decisions: weighted vote with a quorum veto.** In order:
  1. Rules gates (legal, action available).
  2. The data override `EnemyUseCoupDeGraceOverride` (`AIService.cs:2311`) still wins over everything.
  3. If traits whose summed weight is at least 0.5 say `Forbid`, the answer is false. Likewise `Insist` at 0.5 or more makes it true. An exact 0.5/0.5 tie falls through to step 5.
  4. Otherwise compute `v = Σ w_i · opinion_i`, where a Forbid or Insist below quorum counts as ∓2. If `v > 0.15` the answer is true; if `v < -0.15` it is false.
  5. Otherwise use the **Role profile's legacy answer**.

  Legacy hooks are never converted into votes. That avoids both problems the judges found: base `ShouldPreferCharge` returning true for everyone, and deliberate Dragon or Mindless "no" answers being outvoted.
- **Exclusive choices** (maneuver, target): argmax with stable tie-breaks. `CombatStyle`, `PreferredRangeSquares` and `GrappleBehavior` come from the Role and are adjusted by traits, never averaged: averaging a range of 0 with a range of 6 gives 3, which strands a reach-1 creature.
- **Numeric dials:** `PersonalityParams` are averaged by weight once. `GetRangedAoORiskToleranceMultiplier` becomes `lerp(2.0, 0.25, RiskAversion)`, with the caller's clamp kept at `AIService.cs:1058`.
- **Per-turn cache:** `AITurnCache` holds the chosen target, the maneuver, each boolean answer, and the Sanctuary and Listen rolls per (observer, target, round). This is required because `SelectBestTarget` runs 2-4 times per turn and `GetPreferredManeuver` runs twice per action (`AIService.cs:2233, 2288`).

**Stance (increment 3).** Stance chooses the routine for the turn:

`StanceScore(s) = Σ w_i·desire_i(s) − RiskAversion·PlanRisk(s) + 0.08 if s == current`

Stances map onto the existing routines:

| Stance | Routine |
|---|---|
| Engage | AggressiveMelee |
| Skirmish | RangedKiter |
| Support | the healer branch, generalised to any NPC with a castable heal or buff |
| Hold | DefensiveMelee |
| Withdraw | the new `ExecuteWithdrawTurn` |

The Swarm, Dragon and Mindless roles bypass stance selection.

### 4.3 Consideration library (all return [0,1] from a `TargetView`)

| Id | Meaning | Facts read | Tier |
|---|---|---|---|
| WoundSeverity | standing target's wound band | WoundBand | Animal |
| IsDown | dying, unconscious or stable on the floor | Down | Mindless |
| Helpless | prone, grappled, paralysed or entangled | conditions | Animal |
| Softness | armor category, narrowed by learned AC bounds | ArmorCategory, AC* | Low |
| KillChance | P(down this round) × threat value: "reduce their numbers" | WoundBand, AC estimate, roles | Average |
| KnownCaster / KnownHealer | caster or healer role at Inferred level or better | CastsArcane/Divine, ClassRole | Low |
| Proximity | 1 when adjacent, 0 at 12 squares | believed position | Mindless |
| ThreatensMe | target threatens my square | position, reach | Animal |
| EngagedByAlly | target is adjacent to one of my allies | position | Animal |
| Isolated | no allies of the target within 2 squares | position | Animal |
| FlankOpportunity | I can reach a flanking square | position | Animal |
| Clustered | other foes within 2 squares (area-spell value) | position | Low |
| LooksDangerous | heavy armor plus big weapon, or larger size | Apparent | Animal |
| HurtMe | damaged me since my last turn | DamageOutcome | Animal |
| ThreatToWard | adjacent to my ward or leader | position | Low |
| MarkedByLeader | the leader's blackboard mark | blackboard | Average |
| FlatFooted | has not acted yet, or flanked | initiative | Average |
| Fleeing | visibly fleeing | Fleeing | Animal |
| OpposedFaith | known opposed alignment or divine caster | Alignment, CastsDivine | Low |

`KillChance = clamp01(myExpectedDamage(t) / estRemainingHP(t)) × threatValue(t)`. The threat value is 1.0 for a known caster or healer, 0.6 for a frontliner, 0.5 when unknown, and 0 when Down. The expected-damage estimate uses the believed AC (from the armor category or learned bounds), and the remaining HP uses the band midpoint × estimated max HP (from HD if identified, otherwise a size prior).

**Smart vs Sadistic.** "Attack softer targets to reduce enemy numbers" is *efficiency*, which is Smart's KillChance. "Finish the helpless" is *cruelty*, which is Sadistic's IsDown and Helpless weights. Keeping the two apart lets you mix them, for example a smart sadist bandit leader.

### 4.4 Starter catalogue

Target weights sum to 1 in absolute value. RA is RiskAversion and MB is MoraleBreak.

| Trait | Min tier | Target weights | Opinions | Params / stance |
|---|---|---|---|---|
| **Sadistic** | Low (Evil warning) | WoundSeverity .3, IsDown .3, Helpless .2, Softness .1, Fleeing .1 | CoupDeGrace Insist; IgnoreUnconscious Forbid; Withdraw Against | Cruelty .9, RA .3; Engage |
| **Cowardly** | Animal | ThreatensMe −.3, EngagedByAlly +.3, LooksDangerous −.2, Proximity −.2 | Charge Forbid; ProvokeAoO Forbid if SelfWound ≥ Hurt, else Against; IgnoreAoO Forbid; Withdraw For if SelfWound ≥ Badly or AlliesDown ≥ .5 | RA 1.0, MB .8; range +2 for ranged/caster roles; Skirmish, Withdraw |
| **Smart** | Average | KillChance .45, KnownHealer .15, KnownCaster .15, FlankOpportunity .15, IsDown −.1 | IgnoreUnconscious For if StandingThreatInReach; CoupDeGrace For if NoEnemyThreatensSelf, else Against; ProvokeAoO Against | RA .6, Persistence .5 |
| **Caster** | Low | Clustered .4, KnownCaster .3, ThreatensMe −.3 | Charge Forbid; ProvokeAoO Against | RA .7; implied role Spellcaster; Skirmish |
| **Healer** | Low | ThreatToWard .4, EngagedByAlly .3, ThreatensMe −.3 | CoupDeGrace Against; Charge Against | RA .3; implied role Healer; Support desire = ally band urgency |
| **Berserker** | Animal | Proximity .4, HurtMe .4, LooksDangerous .2 | Charge Insist; Withdraw Forbid; IgnoreAoO For | RA −.3, MB 0; Engage |
| **Protector** | Low | ThreatToWard .6, ThreatensMe .2, Proximity .2 | Withdraw Forbid if WardStanding; Charge Against | Discipline .8; Hold; needs `AIWardName` |
| **Opportunist** | Average | Helpless .3, FlatFooted .3, Isolated .2, KillChance .2 | ProvokeAoO Against; CoupDeGrace For | RA .6 |
| **Pack Hunter** | Animal | FlankOpportunity .35, EngagedByAlly .35, Isolated .15, Helpless .15 | Withdraw For if AlliesDown ≥ .5 | Engage |
| **Mindless** | Mindless, lock | Proximity 1.0 | none (Role defaults) | RA 0; Engage only |
| **Zealot** | Low | OpposedFaith .4, KnownCaster .3, LooksDangerous .3 | Withdraw Forbid | MB 0 |
| **Ambusher** | Average | FlatFooted .4, Isolated .3, Softness .3 | Charge For if target flat-footed; Withdraw For if SelfThreatened after striking | RA .7; full value needs surprise (CMB-028) |
| **Disciplined** | Average | MarkedByLeader .6, EngagedByAlly .2, FlankOpportunity .2 | Withdraw Against while the leader stands | Discipline .9 |
| **Leader** | Average | KillChance .4, KnownCaster .3, Isolated .3 | — | posts its chosen target as the side mark (Inc 3) |

Later candidates: Territorial, Predator, Merciful/Captor, Glory-seeker, Arrogant, Vengeful, Greedy.

**No default presets are attached to existing archetypes.** A preset table (for example Humanoid → Disciplined .5 + Smart .3 + Cowardly .2) is provided **opt-in only**, and is switched on per archetype after trace parity is shown.

### 4.5 Worked examples

**Goblin shaman: `Cowardly:0.6, Healer:0.4`.** This needs a new `goblin_adept` in `NPCDatabase_G.cs`: an MM goblin with DMG Adept 1 (`Character/Classes/NPC/AdeptClass.cs`), Int 10, Wis 13, *cure light wounds*, *bless* and *cause fear*. Its tier is Average. Healer's implied role gives Role = `HealerAIProfile`, so the healer branch (`AIService.cs:197-257`) runs and falls back to `ExecuteRangedKiterTurn`. RiskAversion = .6·1.0 + .4·.3 = **.72**.

- **Increment 1 (hooks only).**
  - Charge: Cowardly Forbid with weight .6, which meets the quorum, so it never charges.
  - Avoids AoOs.
  - Kiting range is the Role's plus 2.
  - AoO risk tolerance is `lerp(2, .25, .72) ≈ .74`.
  - Target scoring prefers foes engaged by goblin allies and away from itself.
  - Heal-target filter: it skips heal targets whose touch square would provoke while Cowardly's ProvokeAoO answer is Forbid.
- **Blocker.** `TryExecuteSpellcastAction` (`AIService.cs:2340`) is synchronous and never moves the caster, so the shaman only heals allies already in touch range until move-then-touch exists (AI-047).
- **Increment 3 stance table** (desires: Healer Support = ally urgency, Badly .85 / Near death 1.0; Cowardly Skirmish .3, +.4 when threatened; Cowardly Withdraw .1 unhurt to .9 badly hurt and threatened):

| Situation | Support | Skirmish | Withdraw | Result |
|---|---|---|---|---|
| A. Ally badly hurt; a safe touch square exists (PlanRisk .1) | .4·.85 − .72·.1 = **.27** | .18 | .06 | Moves to the unthreatened side and heals |
| B. Every touch square is threatened (PlanRisk .7) | .34 − .50 = −.16 | **.18** | .06 | Stays back; *bless* or sling |
| B″. As B but weights flipped (Healer .6, Cowardly .4; RA .58), ally near death | .60 − .41 = **.19** | .13 | — | The dutiful healer takes the risk |
| C. Shaman itself badly hurt and threatened | −.01 | — | **.47** | Withdraws (no AoO from the first square) |

Weight order has a visible, predictable effect, which is what "cowardly first, then healer" should mean.

**Sadistic orc: `Sadistic:0.7, Berserker:0.3`** (Role Humanoid, `orc_warrior`, `NPCDatabase_O.cs:298`, Int 8, Average tier).

The folded weights are: WoundSeverity .21, IsDown .21, Helpless .14, Proximity .12, HurtMe .12, Softness .07, Fleeing .07, LooksDangerous .06.

| Target | Inputs | Trait term (× K = 20) |
|---|---|---|
| Fighter | heavy armor, unhurt, adjacent, hit the orc last round | .12 + .12 + .06 = .30 → **+6.0** |
| Wizard | no armor, badly hurt, 4 squares away | .21·.66 + .07·1 + .12·.67 = .29 → **+5.8** |
| Cleric | Down, adjacent, medium armor | .21 + .12 + .07·.33 = .35 → **+7.1** |

Sadistic's IgnoreUnconscious Forbid (.7) keeps the cleric a valid target, and CoupDeGrace Insist (.7) triggers a coup de grace. A Smart hobgoblin in the same spot charges the wizard instead: the Down cleric's threat value is 0, so its KillChance is 0, and IsDown is −.1.

**Smart hobgoblin captain: `Smart:0.5, Disciplined:0.3, Leader:0.2`.** A new `hobgoblin_captain`: MM hobgoblin with Fighter 3 and Int 13 (Keen tier), Role Humanoid. Smart and Leader put KillChance and KnownCaster on top, and the Leader posts the mark that Disciplined goblins follow.

- **Omniscient mode** (Inc 1): it knows the wizard is a caster on round 1.
- **Rules mode** (Inc 2): KnownCaster is 0 until the wizard casts in view, or the encounter is Watched or Informed. Until then, Softness (no armor) and KillChance still rank the wizard high, for a legitimate reason.

That difference, shown in the trace, is the demonstration of fairness.

**Zombie: `Mindless`.** The lock removes everything else, so `Mindless, Smart:0.5` logs "Smart dropped: requires Average tier". Its behaviour equals today's `UndeadMindlessAIProfile`. In Rules mode it goes to the nearest *perceived* target or its last-known square, never the true square of an invisible PC (AI-051). It ignores disguises, wound bands and casters.

---

## 5. Code plan

### 5.1 New files

| File | Content |
|---|---|
| `AI/Personality/TraitDefinition.cs`, `OpinionRule.cs`, `PersonalityParams.cs`, `Stance.cs` | Types from §4.1 |
| `AI/Personality/TraitDatabase.cs` (+ `TraitDatabase_Core.cs` partials) | Catalogue rows in the `NPCDatabase` style, with a registry by id |
| `AI/Personality/Considerations.cs` | `ConsiderationId` and static evaluators over `ITargetView` |
| `AI/Personality/PersonalityProfile.cs`, `PersonalityFactory.cs`, `PersonalityValidator.cs`, `PersonalitySpec.cs` | Parse `"Cowardly:0.6,Healer:0.4"`, validate, fold weights and params, resolve the Role |
| `AI/Personality/AIDecisions.cs`, `AITurnCache.cs` | The facade wrapping each hook call site; per-turn cache |
| `AI/Knowledge/KnowledgeTypes.cs`, `ITargetView.cs`, `TargetView.cs`, `IKnowledgeProvider.cs`, `OmniscientKnowledgeProvider.cs` | Inc 1 |
| `AI/Knowledge/KnowledgeService.cs`, `SideKnowledgeStore.cs`, `VisualFactExtractor.cs`, `ConditionVisibility.cs`, `PerceivedKnowledgeProvider.cs` | Inc 2 |
| `AI/Knowledge/IdentificationService.cs`, `LoreBook.cs`, `Encounters/EncounterContext.cs`, `UI/Encounter/IntelPanelUI.cs` | Phase 4 |
| `AI/Debug/AIDecisionTrace.cs`, `AITraceOverlay.cs` | §8 |
| `Tests/AI/PersonalityBlendTests.cs`, `PersonalityValidatorTests.cs`, `TargetViewTests.cs`, `OmniscienceLintTests.cs` | §8 |

### 5.2 Changes

| Where | Change |
|---|---|
| `NPCDefinition` (`NPCDatabase.cs`, near 627) | Add `public string AIPersonality;` (validated against `TraitDatabase` at load) and `public string AIWardName;`. Phase 4 adds `List<SkillRank> Skills` and `List<LoreEntry> Lore`. Keep `AIProfileArchetype` and `AIBehavior`. |
| `GameManager.InitializeNPCFromDefinition` (`NPCSetup.cs:445`, 748) | Resolve the Role (archetype, else the implied role from the spec), then `npc.aiProfile = BuildRuntimeAIProfile(...)` and `npc.Personality = PersonalityFactory.Build(def, npc.aiProfile, npc.Stats)`. Assign `EncounterEntityId`. Call `Tags.ClearAllTags()` and reset the trackers on the reused slot. |
| `BuildRuntimeAIProfile` (758-816) | Add a Brute case (Berserk or Humanoid) and a Caster case (Spellcaster) for AI-004. Destroy the slot's previous profile instance (AI-017). |
| `CharacterController` | Add `PersonalityProfile Personality` and `int EncounterEntityId`. Later, `NPCAIBehavior AIBehavior` moves here from the parallel list (AI-015). |
| Target scoring: `AIService.SelectBestTargetFromProfile` (~1790), `SelectAdaptiveFullAttackTarget` (3074), `NPCTurns.cs:733` | `profile.ScoreTarget(t, self)` becomes `AIDecisions.ScoreTarget(self, t)`, which returns the legacy value unchanged when `Personality == null`. |
| Boolean and choice sites: `EvaluateMovementOptions` (2074-2082), `TryTakeTacticalFiveFootStep` (1089), `ShouldUseManeuver` / `TryExecutePreferredManeuver` (2233, 2288), `ShouldNPCUseCoupDeGrace` (2315), `SelectBestAction` (2589), ignore-unconscious (1782, 3044; `NPCTurns.cs:547, 652, 709`), `NPCTurns.cs:297, 563, 1136`, `CalculateRangedRiskTolerance` (1058) | Each goes through `AIDecisions.Judge(BoolDecision, ...)` or `ChooseManeuver`, with the same no-personality pass-through. |
| `NPCTurns.cs:62` (`GetNPCBehaviorForAI`) | Inc 3: `AIDecisions.ChooseBehavior(npc, legacyBehavior)` maps the chosen stance to the routine. This is the single override point for "Cowardly turns AggressiveMelee into DefensiveMelee". |
| `TryExecuteSpellcastAction` (`AIService.cs:2340`) | Inc 3: move-then-touch for touch heals, and never target allies offensively (AI-047). |
| Archetype writers: `DungeonEncounterSpawner.UpdateAIForClass` (224-290), `ZombieTemplate.cs:216`, `SkeletonTemplate.cs:250`, `LycanthropeTemplate.cs:388, 851`, `NPCTemplateAIConfigurator.cs:31` | Also write or append `AIPersonality`: Zombie and Skeleton get the Mindless lock; Lycanthrope appends Pack Hunter. |
| Omniscient reads → `ITargetView`: `GetTargetPriority` (2010), `CalculateThreat` (2038), `SelectLowestHPEnemy` (2612), base `ScoreTarget` (85-88), Vampire (102-127), UndeadTactical (95-111), UndeadIncorporeal (78-103), Dragon (97-124, 554), Lich (109-111), Spellcaster (115-122, 260), strategist (392-437, 544-552, 596-608, 703-716) | Inc 1 converts the reads in base `ScoreTarget`, the AIService methods and the traits. Inc 2 converts the profiles. The strategist and Dragon convert later. Unconverted code carries a `[KnowledgeLegacy]` marker, and the trace flags its reads. |
| `StatusTagManager`, `AIProfile.TargetHasMatchingTag` | Stop emitting `Class:`; use base weapon names. Matching becomes exact or explicit-wildcard (`"Armor:*"`), with the old substring path marked `[Obsolete]`. `HP State: Staggered` weights become `Wounds:` bands. |
| `GameEventSystem` publishers | Inc 2: publish `AttackResolvedEvent` and `SpellCastEvent`. Phase 4: `DamageTakenEvent` with resist/immune flags. Publish only; resolution is unchanged. |
| `CharacterInfoPanel` (145-240), `CharacterHoverTooltipUI` (237-298), `CombatResult`/`FullAttackResult` strings | Inc 2: render the party `TargetView` in Rules mode. |
| `PreCombatHubUI` (buttons 150-156), `StartCombat` (3632) | Phase 4: Intel sub-window using `ReturnToPreCombatHubFromSubWindow` (1535); seed enemy intel at combat start. |

### 5.3 Migration of today's single profiles

- Nothing is deleted, and every existing definition keeps its archetype and gets no traits, so its behaviour is identical by construction. The no-personality branch calls the original hook directly.
- The concrete profile stays in `aiProfile`, so the eight type-check sites need no resolver. A grep test still fails on any *new* `is/as XxxAIProfile` outside the existing sites, to keep it that way.
- Profile-specific quirks that are worth keeping move to consideration weights only when a profile is ported to the view. For example, Vampire's "low Will" becomes `SaveTendency(Will, Weak)`, usable only once the vampire knows or guesses it.

---

## 6. Prerequisites from docs/issues

| When | Issue | Why |
|---|---|---|
| Before Inc 1 | **AI-053** | Every profile logs as "Default AI", so the trace cannot name Roles. |
| | **AI-004** | Brute (29 definitions) and Caster get a null Role. |
| | **AI-017** | `OnEnable` overwrites tuning, and instances leak on reused slots. Apply params after `OnEnable`, and destroy old instances. |
| | **AI-018**, **TST-002** | `CombatUI` null dereference at `AIService.cs:59-60`, and no runner for `RunAll`. Without these fixes the tests below never run. |
| | **AI-005 / AI-007** | `SelectBestTarget` re-rolls Sanctuary and Listen on every call, so cached decisions would still flip within a turn. |
| | Tag leak / **AI-021** | `ClearAllTags` has no callers, and the position stores are never cleared, so state leaks across encounters on reused slots. |
| Before Cowardly counts as "complete" | **CMB-073** (High) | NPC movement never provokes AoOs, so "avoid AoOs" changes nothing. Until it lands, the trace labels it "AoO risk simulated". |
| | **AI-047** | The healer never moves to touch (the shaman example). |
| | **AI-010**, **CMB-075** | `FleeHealthThreshold` is unread, and the turn-skip gate pre-empts Panicked creatures. Withdraw needs both. |
| | **AI-002**, **AI-034** | Casters on melee routines never cast; move actions are not checked. |
| | **ENC-021** | DMG casters have no spells, so the Healer and Caster traits cannot act. |
| Before Inc 2 | **AI-051** | Routines path to the true square of unseen targets (`AIService.cs:531, 882, 1188`). |
| | **AI-036** | The Mirror Image override bypasses all scoring. |
| | **AI-011** | Becomes the outcome-fact writers. |
| Before Phase 4 | NPC skills (no issue exists yet; file one) | `NPCDefinition` has no skills field and `InitializeSkills` never runs for NPCs, so trained-only checks always fail for monsters. |
| | **ENC-015** | The DMG `SpawnResult` is dropped at `EncounterSelectionUI.cs:676`, so DMG encounters carry no intel context. |
| | **CORE-002** | `OnCombatEnded` is skipped on victory. Use `HandleCombatVictoryDetected` (`GameManager.cs:3579`). |
| | **CRE-002** | Spawned monsters have no alignment (alignment hints, Zealot, *detect evil*). |
| | **CMB-028** | Surprise and encounter distance, needed for the Alert/Watched levels and Ambusher. |

---

## 7. Roadmap

### Increment 1: weighted traits (medium)

- **Prerequisites:** the "Before Inc 1" rows of §6.
- **Build:**
  - `TargetView` with `OmniscientKnowledgeProvider`, using wound bands computed from truth so the trait logic already works in bands.
  - The considerations, `TraitDatabase` with Cowardly, Sadistic, Smart, Healer, Caster, Berserker and Mindless, `PersonalityFactory` and `PersonalityValidator`.
  - `AIDecisions` at every site listed in §5.2, plus `AITurnCache`.
  - `NPCDefinition.AIPersonality`, `AIDecisionTrace` v1, and the AI-004 Role cases.
- **Data:**
  - `goblin_adept` (`Cowardly:0.6,Healer:0.4`).
  - An `orc_sadist` variant (`Sadistic:0.7,Berserker:0.3`).
  - `hobgoblin_captain` (`Smart:0.5,Humanoid` Role; the Disciplined and Leader traits follow in Inc 3).
  - Presets `ai_personality_test` and `ai_sadist_fallen`.
- **Done when:**
  - The AI.md §12.2 presets produce identical traces with and without the feature for every definition that has no traits.
  - The shaman never charges.
  - The orc does a coup de grace on a downed PC.
  - The captain focuses the softest, most wounded target.
  - The validator rejects Smart on a zombie.
- **Known limit:** the shaman only heals allies already in touch range, and AoO avoidance is cosmetic until CMB-073.

### Increment 2: fair sight (large)

- **Prerequisites:** AI-051, AI-036 and the Inc 1 fixes.
- **Build:**
  - `KnowledgeService`, `SideKnowledgeStore`, `VisualFactExtractor` and `ConditionVisibility`.
  - `PerceivedKnowledgeProvider` with tier filtering and `EncounterEntityId` keys.
  - Sighting in `ProcessRoundStartPerception` (`AIService.cs:2934`) and at the start of each PC turn.
  - Publish `AttackResolvedEvent` and `SpellCastEvent`, which give AC bounds and caster or role facts.
  - Port base `ScoreTarget`, `GetTargetPriority`, `CalculateThreat`, Humanoid, Berserk, Grappler, Ranged and Animal to the view.
  - Information Mode (default Omniscient), the wound-band setting, party UI and log redaction in Rules mode, and the Enemy-view overlay.
- **Done when:**
  - In Rules mode the captain does not prioritise the wizard as a caster until it casts.
  - Nothing scores or paths against an unpinpointed invisible PC.
  - Every fact in the trace shows its level and source.
  - The omniscience lint passes for `AI/Personality` and `AI/Knowledge`.
- **Risk:** the AI may look dumber. This is mitigated by legitimate priors (armor, spellbook, holy symbol) and AC learning, and by keeping Omniscient as the default until tuned.

### Increment 3: stances, withdraw and healer movement (large; rules-gated)

- **Prerequisites:** CMB-073, AI-047, AI-010, CMB-075, AI-034 and ENC-021 (for adept spells).
- **Build:**
  - `ChooseBehavior` at `NPCTurns.cs:62` with stance desires and hysteresis.
  - The healer branch generalised into a Support stance for any NPC with a castable heal or buff.
  - Move-then-touch casting, and `ExecuteWithdrawTurn` (the PHB withdraw action).
  - Disciplined, Leader and Protector traits with a `SideBlackboard` for the leader's mark.
  - Preset `ai_shaman_dilemma` with situations A, B and C.
- **Done when:** the shaman picks Support, Skirmish and Withdraw in A, B and C, and flipping the weights reproduces B″, all in trace form.

### Later phases

- **Phase 4. Knowing things.** NPC skill ranks (MM skill lines, starting with the preset creatures), `IdentificationService` (Knowledge, Spellcraft, Sense Motive, Spot vs Disguise for both sides), the Bestiary, the hub Intel panel (Study Foes, Scout, optional Gather Information) with a preparation budget, `EncounterContext` with intel levels (ENC-015), save and immunity outcome learning, and enemy encounter-preview name gating.
- **Phase 5.** Divination as fact sources (*deathwatch*, *detect magic/evil*), and faction memory carried home by fleeing survivors.
- **Phase 5b. Demeanor tells.** A `Demeanor` fact, revealed by Sense Motive or by Knowledge for MM-iconic temperaments ("goblins are cowardly"). Opinion reasons become one-line log tells ("the goblin adept hangs back behind its allies").
- **Phase 6.** Once CMB-073 and AI-019 (A* per cell) are fixed: a unified candidate evaluator in hit-point-equivalents, with the consideration library becoming its terms, quorum-veto trace lines, and the retirement of `TagPriorities` and the duplicated routines.

---

## 8. Testing and the decision trace

**`AIDecisionTrace`**

- One record per NPC turn, kept in a ring buffer of the last 50.
- Logged as one collapsed `[AI-TRACE]` line when `AIService.TraceEnabled` is on, and viewable in an overlay for the selected NPC.
- Written at these points:
  - routing (`AIService.cs:182-298`)
  - the score loop (~1786-1797)
  - `EvaluateMovementOptions` (2059-2140)
  - `SelectSpell`
  - `ShouldUseManeuver` (2213)
  - `SelectBestAction`

```
[AI-TRACE R3 goblin_adept#7 tier=Average role=Healer traits=Cowardly .60, Healer .40 RA=.72 mode=Rules]
 TARGET  fighter#1 legacy 18.0 + traits -4.2 (ThreatensMe 1.0 [Rules/Sight], EngagedByAlly 0) + percep 0 = 13.8
         wizard#3  legacy 14.5 + traits +3.1 (EngagedByAlly 1.0, LooksDangerous 0)      = 17.6  <- chosen
 BOOL    Charge: Cowardly Forbid (held .60 >= .50) -> false
         ProvokeAoO: Cowardly Forbid (SelfWound Hurt) -> false   [AoO risk simulated: CMB-073 open]
 SKIPPED Smart.KillChance (not present); Healer.KnownCaster (tier ok, fact Unknown -> 0)
 CACHE   target=wizard#3 maneuver=none cdg=false
```

**Unit tests (pure, no GameManager).** They follow the `Tests/AI/AIProfileFrameworkTests.cs` pattern and use the F10 runner.

- Blending:
  - A single trait at weight 1.0 equals that trait.
  - Folded weights equal the per-trait sum.
  - Raising Cowardly's weight never raises the chosen candidate's ThreatensMe value.
  - Quorum boundaries at .5 ± ε.
  - The legacy fallback at |v| < .15.
  - The data override wins.
  - Determinism under a fixed seed.
- Validator: the tier drop with renormalisation, the Mindless lock, and an unknown id.
- `TargetView`:
  - Disguise changes Species.
  - Charmed is not Apparent.
  - "+1 Longsword" becomes "Longsword".
  - Band thresholds per wound mode.
  - A hit at 17 and a miss at 15 give AC in (15, 17].
  - Natural 1 and 20 are excluded.
  - Slot re-spawn clears encounter facts.
- Guards:
  - The **omniscience lint** fails if `AI/Personality/**` or `AI/Knowledge/**` (except the providers) reads `.Stats.(CurrentHP|ArmorClass|TouchArmorClass|FortitudeSave|ReflexSave|WillSave|SpellResistance|Level|IsWizard|IsCleric|IsRogue|HasClass)`.
  - A grep test fails on new concrete profile type checks.

**Scenario presets (Play mode, seeded RNG).** `ai_personality_test`, `ai_sadist_fallen`, `ai_captain_focus`, `ai_shaman_dilemma` (A/B/C layouts), `ai_zombie_shamble`, and `ai_fairness_mirror` (a disguised PC, an invisible PC, a Mage-Armored PC). Accepted traces are stored as golden files under `docs/testing/`. The Inc 1 gate is a trace diff for single-archetype NPCs with the feature on and off.

**Telemetry per encounter.** Vetoes fired, legacy fallbacks taken, stance flips, and considerations skipped by tier. These counters show which weights need tuning.

---

## 9. Open questions for the owner

1. **Where are your Sadistic, Cowardly and Smart profiles?** They are confirmed absent from this repo and its history. If they exist, their logic becomes trait rows, and your tuning replaces the starter values in §4.4.
2. **Which wound-visibility mode should be the default:** Strict, Coarse (proposed) or Fine? It is a house rule either way.
3. **In Rules mode, should the encounter cards and NPC panels still show creature names, CR and EL before identification?** These are DM meta-knowledge today.
4. **Is a sadistic coup de grace on downed PCs acceptable at Normal difficulty,** or should Easy and Normal add a rules gate?
5. **Should the "Rumours" version of Gather Information exist** before a clock and town system exist, or wait for them? And is the abstract preparation budget an acceptable stand-in for hours?
6. **Should Increment 3 (stances) and Phase 4 (knowledge rolls and hub intel) swap?** Stances finish request 2 but are blocked by the CMB-073, AI-047 and AI-010 rules fixes. Phase 4 finishes request 1 but needs monster skill data.
7. **Should alignment stay a warning,** as proposed, or become a hard gate, such as Sadistic requiring Evil? A hard gate cannot work until CRE-002 is fixed.
8. **Should Rules mode eventually become the default,** or stay a difficulty and realism option?

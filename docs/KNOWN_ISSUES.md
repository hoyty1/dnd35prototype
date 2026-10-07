> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# Known issues

Open defects, rules deviations, unreachable features, development traps and tech debt in the DNDPrototype codebase (Unity 6000.4.0f1, D&D 3.5e tactical combat). Each entry was found by static reading of the code and git history and checked against the code; unless an entry says otherwise, nothing here was reproduced in Play mode.

## How to use these files

- **IDs are stable.** Each entry has an ID made of a section prefix and a number (`CMB-004`). IDs are never reused or renumbered; refer to issues by ID in commits, comments and other docs.
- **Re-verify before fixing.** Line numbers drift with every commit. Treat `path:line` references as hints: grep for the named class or method, confirm the issue still exists, then fix it.
- **When you fix an issue,** delete its entry from `issues/<PREFIX>.md` in the same commit (and its Top issues row, if any) and cite the ID in the commit message (for example `Fix flanking AC double count (CMB-001)`). If you fix only part of it, rewrite the entry to describe what is left.
- **When you find a new issue,** add it to the matching section and group with the next free number for that prefix (the highest ID ever used in that section plus one, not a gap left by a deleted entry). State the evidence (paths, Class.Method), the player-visible effect if any, a suggested fix, and anything you could not verify.
- **Keep the "Entries per section" table below current.** When you add an entry, use the Next ID shown for its section, then bump Next ID and the counts; when you fix (delete) an entry, lower the counts but leave Next ID unchanged.
- **Cross-references** use IDs ("see CORE-012"). When you delete a fixed entry, grep `docs/` for its ID and update the entries and docs that point to it.
- **Player-visible** in an entry means a player can notice the effect in normal play. Entries without it affect only developers.

Severity:

| Severity | Meaning |
|---|---|
| **High** | Breaks the build, the repo or a fresh clone; soft-locks or corrupts a session; or is a rules error that affects most fights or every character of a kind. Fix first. |
| **Med** | Wrong behavior in a specific feature, rule or code path; a selectable feature that does nothing; or a trap likely to cause the next bug. |
| **Low** | Edge cases, latent issues, cosmetic or log-only problems, dead code and stale comments. |

Groups inside each section: *Bugs and rules deviations* (code does the wrong thing), *Unreachable or stubbed features* (code or data exists but cannot be used in play), *Development traps* (things that mislead or break the next change), *Performance*, *Dead code, duplication and tech debt*, and *Misleading code comments and strings*.

Entries per section:

| Section | Prefix | High | Med | Low | Total | Next ID |
|---|---|---|---|---|---|---|
| [Build, repo and tooling](issues/REPO.md) | REPO | 0 | 5 | 8 | 13 | REPO-019 |
| [Runtime loop and core](issues/CORE.md) | CORE | 2 | 19 | 15 | 36 | CORE-037 |
| [Combat](issues/CMB.md) | CMB | 6 | 37 | 38 | 81 | CMB-083 |
| [Grid and movement](issues/GRID.md) | GRID | 1 | 7 | 9 | 17 | GRID-018 |
| [Spells and metamagic](issues/SPL.md) | SPL | 10 | 54 | 47 | 111 | SPL-112 |
| [Characters, feats and classes](issues/CHR.md) | CHR | 7 | 24 | 36 | 67 | CHR-068 |
| [Creatures, templates and summoning](issues/CRE.md) | CRE | 4 | 17 | 16 | 37 | CRE-038 |
| [Encounters](issues/ENC.md) | ENC | 1 | 7 | 13 | 21 | ENC-022 |
| [Items, store, crafting and treasure](issues/ITM.md) | ITM | 4 | 31 | 33 | 68 | ITM-069 |
| [AI](issues/AI.md) | AI | 1 | 19 | 35 | 55 | AI-057 |
| [UI](issues/UI.md) | UI | 1 | 11 | 38 | 50 | UI-051 |
| [Tests](issues/TST.md) | TST | 0 | 8 | 18 | 26 | TST-027 |
| **All** | | 37 | 239 | 306 | 582 | |

## Top issues

The most impactful entries, mainly High. Repo breakers, soft-locks and crashes come first, then rules errors that affect every fight.

| ID | Severity | Area | Summary | Player-visible |
|---|---|---|---|---|
| [CORE-001](issues/CORE.md) | High | Runtime loop and core | Party wipe soft-locks the game (no defeat screen or exit path) | Yes |
| [CORE-034](issues/CORE.md) | High | Runtime loop and core | A downed regenerating or fast-healing enemy is never counted as defeated, so the fight cannot be won | Yes |
| [ENC-001](issues/ENC.md) | High | Encounters | Enemies 6+ spawn off the grid and cannot move; more than 15 enemies are dropped | Yes |
| [UI-001](issues/UI.md) | High | UI | Combat log adds about 50 pooled GameObjects on every log call (growth not measured) | Yes |
| [CMB-002](issues/CMB.md) | High | Combat | Full attacks use STR for ranged attacks and drop morale attack bonuses | Yes |
| [CMB-003](issues/CMB.md) | High | Combat | Morale and situational damage bonuses never reach weapon damage | Yes |
| [CMB-004](issues/CMB.md) | High | Combat | Critical hits multiply only weapon dice; coup de grace multiplies sneak attack | Yes |
| [CMB-005](issues/CMB.md) | High | Combat | Movement AoOs resolve after the step, so they can auto-miss but are still spent | Yes |
| [CMB-073](issues/CMB.md) | High | Combat | NPC, summon and condition-forced movement never provokes attacks of opportunity | Yes |
| [CMB-006](issues/CMB.md) | High | Combat | Conditions tick at the round boundary (GameManager.OnNewRound -> ConditionService.OnRoundEnd), so 1-round conditions can cost the target nothing | Yes |
| [CHR-001](issues/CHR.md) | High | Characters, feats and classes | Created PCs get CON hit points twice; CON HP clamped to +1 per level | Yes |
| [CHR-002](issues/CHR.md) | High | Characters, feats and classes | Class BAB and hit-die tables contradict class data and RAW (e.g. Bard 1/2 BAB) | Yes |
| [CHR-018](issues/CHR.md) | High | Characters, feats and classes | Divine Grace and other save bonuses are computed but never applied | Yes |
| [CHR-019](issues/CHR.md) | High | Characters, feats and classes | Most racial traits are display-only | Yes |
| [SPL-001](issues/SPL.md) | High | Spells and metamagic | Save DC formula depends on code path; Sorcerer and Bard DCs use WIS | Yes |
| [SPL-002](issues/SPL.md) | High | Spells and metamagic | 54 spells that set only BuffDurationRounds get a 0-round duration | Yes |
| [SPL-003](issues/SPL.md) | High | Spells and metamagic | Silence, Death Knell and Align Weapon never expire | Yes |
| [SPL-004](issues/SPL.md) | High | Spells and metamagic | Custom spell damage bypasses energy resistance, immunity and DR | Yes |
| [SPL-005](issues/SPL.md) | High | Spells and metamagic | Generic spell dice do not scale with caster level; Cure/Inflict ignore undead | Yes |
| [SPL-006](issues/SPL.md) | High | Spells and metamagic | AoE, NPC, summon and Grease cast paths skip Blink, AoO, concentration and counterspell checks | Yes |
| [SPL-037](issues/SPL.md) | High | Spells and metamagic | Spell-specific branches at the end of ApplySpellBuff never run | Yes |
| [CRE-002](issues/CRE.md) | High | Creatures, templates and summoning | Spawned NPCs have no alignment, so smite, aligned weapons and alignment spells ignore them | Yes |
| [CRE-004](issues/CRE.md) | High | Creatures, templates and summoning | Spawned NPCs ignore class BAB and saves | Yes |
| [ITM-004](issues/ITM.md) | High | Items, store, crafting and treasure | NPCs spawn without weapons and shields listed under MainHand, OffHand or Ranged | Yes |
| [ITM-001](issues/ITM.md) | High | Items, store, crafting and treasure | Ring of Protection deflection accumulates on every stat recalculation | Yes |
| [AI-001](issues/AI.md) | High | AI | NPCs never cast area spells, although AI scoring favours them | Yes |
| [SPL-007](issues/SPL.md) | High | Spells and metamagic | NPC-cast Control spells take effect even when the target saves | Yes |

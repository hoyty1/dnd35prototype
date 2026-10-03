> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-03.

# Documentation index

The code is the source of truth. These docs cover only what the code can't tell you quickly: how the parts fit together, where things live, the traps, and what is broken or unfinished. If a doc disagrees with the code, the doc is wrong. Fix it in the same change.

## Living docs

| Doc | Read it when |
|---|---|
| [`../CLAUDE.md`](../CLAUDE.md) | Always. It is the agent working agreement and loads into every session. |
| [`../README.md`](../README.md) | You want the human-facing overview: what this is and how to open and play it. |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | You need the big picture: runtime loop, composition (`SceneBootstrap`, the `GameManager` partial files), the service layer, the folder map of `Assets/Scripts` and cross-cutting conventions. Its Parts table links the subsystem parts below. |
| [`architecture/`](architecture/) | You need to know how a subsystem works or where its code lives. One part each: [`combat-and-grid.md`](architecture/combat-and-grid.md), [`spells.md`](architecture/spells.md), [`characters-and-creatures.md`](architecture/characters-and-creatures.md), [`items-and-economy.md`](architecture/items-and-economy.md), [`ai-encounters-ui.md`](architecture/ai-encounters-ui.md). |
| [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) | Before you change a subsystem, and when something behaves oddly. It holds the rules for issue IDs, severity, the entry counts and next free ID per area, and the Top issues table. |
| [`issues/`](issues/) | You need the entries themselves. One file per ID prefix: [`REPO`](issues/REPO.md), [`CORE`](issues/CORE.md), [`CMB`](issues/CMB.md), [`GRID`](issues/GRID.md), [`SPL`](issues/SPL.md), [`CHR`](issues/CHR.md), [`CRE`](issues/CRE.md), [`ENC`](issues/ENC.md), [`ITM`](issues/ITM.md), [`AI`](issues/AI.md), [`UI`](issues/UI.md), [`TST`](issues/TST.md). An ID such as `CMB-004` lives in `issues/CMB.md`. |
| [`DEVELOPMENT_RECIPES.md`](DEVELOPMENT_RECIPES.md) | You are adding a spell or spell effect, metamagic feat, condition, feat, class feature, maneuver, item, monster, template, encounter, action button or UI panel. |
| [`TESTING.md`](TESTING.md) | You need to compile-check, run a test suite or play-test a change. |
| [`systems/`](systems/) | You need content, status against the 3.5e rules, or the backlog for one system. One doc per system; see the table below. |
| [`designs/`](designs/) | Three design plans for features that are not fully built (creature trapping, item creation feats, remaining class features). Each starts with a status header verified on 2026-10-03; the body below it is design intent, not a description of the code. |
| [`archive/INDEX.md`](archive/INDEX.md) | You want an old doc. Retired docs are listed there, with the `git show` command to read them. |

### Systems docs

Developer references aimed at the project goal: battles by the 3.5e core rules (PHB/DMG/MM), DMG-style random encounters, between-battle party management by PHB/DMG rules, and much deeper enemy AI. The architecture parts say how the code is built; the systems docs say what content exists, how close it is to the books, and what is left to do. Each cites issue IDs instead of repeating them.

| Doc | Read it when |
|---|---|
| [`systems/AI.md`](systems/AI.md) | You touch enemy AI. Start here for any AI request: the decision pipeline, behaviours and profiles, which combat options the AI can and cannot use, deviations from the rules, and how to extend it ([request playbooks](systems/AI.md#118-request-playbooks)). Companions: [`AI_ACTION_COVERAGE.md`](systems/AI_ACTION_COVERAGE.md) (every combat option: AI vs PC) and [`AI_PLAYBOOKS.md`](systems/AI_PLAYBOOKS.md) (worked plans for likely AI requests). |
| [`systems/RULES_COVERAGE.md`](systems/RULES_COVERAGE.md) | You plan rules-fidelity work or wonder whether a rule is implemented. PHB/DMG/MM coverage by chapter (implemented, partial, missing, deviating), conditions, house rules and unconfirmed deviations, highest-leverage gaps. |
| [`systems/ENCOUNTERS.md`](systems/ENCOUNTERS.md) | You change encounter generation: the four encounter sources, DMG dungeon tables, CR/EL/XP math, spawning, treasure and XP after the fight, DMG coverage. |
| [`systems/PARTY_MANAGEMENT.md`](systems/PARTY_MANAGEMENT.md) | You change the between-battle loop: loot, treasure, XP, level-up, rest and recovery, store, stash, spell preparation, crafting, and what persists between encounters, measured against PHB/DMG. |
| [`systems/SPELLS_AND_METAMAGIC.md`](systems/SPELLS_AND_METAMAGIC.md) | You add or fix spells: spell inventory by class and level, placeholders, IDs and aliases, ranges, casting classes, metamagic, cleric domains, missing PHB spells. |
| [`systems/CREATURES.md`](systems/CREATURES.md) | You add or fix monsters: creature inventory by type and CR, special abilities, templates, dragons, summoning, NPC classes, missing MM monsters. |
| [`systems/MAGIC_ITEMS.md`](systems/MAGIC_ITEMS.md) | You add or fix equipment: weapons, armor, materials, special abilities, consumables, rings, rods, staves, wondrous items, and how items are acquired, measured against PHB ch.7 and DMG ch.7. |

## Documentation policy

These rules exist because the previous docs drifted badly. About 80 docs were written alongside the code. By October 2026 none was fully accurate: the best matched the code about 90% and most 20-60%.

1. **Few living docs, kept true.** Prefer updating an existing doc over adding a new one. A new doc needs a reason the existing ones can't serve.
2. **Status line.** Every doc starts with `> Verified against commit <hash> (<date>) on <date>.` When you re-verify a doc, update the line.
3. **Same-change updates.** A change that alters structure, flows, conventions or known issues updates the affected docs in the same commit: `ARCHITECTURE.md` or the matching `architecture/` part; the matching `issues/` file, plus the counts and Top issues table in `KNOWN_ISSUES.md`; `DEVELOPMENT_RECIPES.md`; the `systems/` doc that covers the area (status rows, cited IDs, backlog).
4. **Describe what is, not what was planned.** Plans go in `designs/` with a status header: what is built, what is not, verified against which commit. When a plan is fully built, delete it. Its useful content moves into the living docs, and git keeps the original.
5. **No point-in-time reports in the tree.** Audits, investigations and completion reports belong in commit messages, PR descriptions or the session. If one must be kept, put it in `archive/`, dated.
6. **Markdown only.** No `.docx` / `.pdf` exports in git; generate them on demand (e.g. pandoc). No docs inside `Assets/`, where Unity imports them as assets. The exception is a short README describing an asset folder.
7. **Grep-able references.** Use repo-relative paths and `Class.Method` names. Use `path:line` sparingly, because line numbers drift.
8. **Honest status.** Say "not verified in Play mode" when something was only read, not run. Mark data-only or unreachable features as such.

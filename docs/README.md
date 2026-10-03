> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

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
| `systems/` (planned, not yet written) | You need deep, per-system detail (status tables, rules notes, backlog). See below. |
| [`designs/`](designs/) | Older design and implementation plans for features that are not fully built. They do not have verified status headers yet; check the code before trusting them. |
| [`archive/INDEX.md`](archive/INDEX.md) | You want an old doc. Retired docs are listed there, with the `git show` command to read them. |

Planned `systems/` docs. They are developer references aimed at the project goal: core 3.5e rules, DMG-style random encounters, and between-battle party management by PHB/DMG rules.

- `AI.md`: where enemy AI stands today. It covers the decision pipeline, the behaviours and profiles, which combat options the AI can and cannot use, and how to extend it.
- `RULES_COVERAGE.md`: a PHB/DMG/MM coverage map listing what is implemented, partial, missing or deviating, with links to issues.
- `ENCOUNTERS.md`: random encounter generation (DMG tables, CR/EL), spawning and encounter content.
- `PARTY_MANAGEMENT.md`: the between-battle loop (rest, healing, XP, level-up, treasure, loot, store, crafting) measured against PHB/DMG rules.
- `SPELLS_AND_METAMAGIC.md`, `CREATURES.md` (including templates and summoning), `MAGIC_ITEMS.md` (including enchantments, materials and crafting): content inventories, status and backlog.

Until they exist, use `ARCHITECTURE.md` with its `architecture/` parts and `KNOWN_ISSUES.md` with its `issues/` files.

## Documentation policy

These rules exist because the previous docs drifted badly. About 80 docs were written alongside the code. By October 2026 none was fully accurate: the best matched the code about 90% and most 20-60%.

1. **Few living docs, kept true.** Prefer updating an existing doc over adding a new one. A new doc needs a reason the existing ones can't serve.
2. **Status line.** Every doc starts with `> Verified against commit <hash> (<date>) on <date>.` When you re-verify a doc, update the line.
3. **Same-change updates.** A change that alters structure, flows, conventions or known issues updates the affected docs in the same commit: `ARCHITECTURE.md` or the matching `architecture/` part; the matching `issues/` file, plus the counts and Top issues table in `KNOWN_ISSUES.md`; `DEVELOPMENT_RECIPES.md`.
4. **Describe what is, not what was planned.** Plans go in `designs/` with a status header: what is built, what is not, verified against which commit. When a plan is fully built, delete it. Its useful content moves into the living docs, and git keeps the original.
5. **No point-in-time reports in the tree.** Audits, investigations and completion reports belong in commit messages, PR descriptions or the session. If one must be kept, put it in `archive/`, dated.
6. **Markdown only.** No `.docx` / `.pdf` exports in git; generate them on demand (e.g. pandoc). No docs inside `Assets/`, where Unity imports them as assets. The exception is a short README describing an asset folder.
7. **Grep-able references.** Use repo-relative paths and `Class.Method` names. Use `path:line` sparingly, because line numbers drift.
8. **Honest status.** Say "not verified in Play mode" when something was only read, not run. Mark data-only or unreachable features as such.

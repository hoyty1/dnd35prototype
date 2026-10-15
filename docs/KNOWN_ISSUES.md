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
| [Runtime loop and core](issues/CORE.md) | CORE | 0 | 18 | 15 | 33 | CORE-039 |
| [Combat](issues/CMB.md) | CMB | 0 | 53 | 63 | 116 | CMB-172 |
| [Grid and movement](issues/GRID.md) | GRID | 0 | 8 | 12 | 20 | GRID-024 |
| [Spells and metamagic](issues/SPL.md) | SPL | 2 | 61 | 62 | 125 | SPL-141 |
| [Characters, feats and classes](issues/CHR.md) | CHR | 2 | 28 | 42 | 72 | CHR-081 |
| [Creatures, templates and summoning](issues/CRE.md) | CRE | 0 | 29 | 31 | 60 | CRE-070 |
| [Encounters](issues/ENC.md) | ENC | 0 | 7 | 15 | 22 | ENC-024 |
| [Items, store, crafting and treasure](issues/ITM.md) | ITM | 0 | 36 | 41 | 77 | ITM-085 |
| [AI](issues/AI.md) | AI | 1 | 22 | 40 | 63 | AI-065 |
| [UI](issues/UI.md) | UI | 0 | 12 | 38 | 50 | UI-052 |
| [Tests](issues/TST.md) | TST | 0 | 8 | 27 | 35 | TST-042 |
| **All** | | 5 | 287 | 394 | 686 | |

## Top issues

The most impactful entries, mainly High. Repo breakers, soft-locks and crashes come first, then rules errors that affect every fight.

| ID | Severity | Area | Summary | Player-visible |
|---|---|---|---|---|
| [AI-001](issues/AI.md) | High | AI | NPCs never cast area spells, although AI scoring favours them | Yes |

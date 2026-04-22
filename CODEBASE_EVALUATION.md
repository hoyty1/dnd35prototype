# D&D 3.5e Prototype — Comprehensive Codebase Evaluation

Date: 2026-04-22 (UTC)  
Scope: Entire `Assets/Scripts` tree, **including tests** per request.

---

### Executive Summary

The project has strong domain coverage and many useful mechanics already encoded, but it is currently constrained by a few very large “coordination-heavy” files.

Most important findings:
- The codebase is **functionally rich but structurally top-heavy** around `GameManager`, `CharacterController`, and `CombatUI`.
- Refactoring of combat systems has started, but orchestration logic is still concentrated in `GameManager`.
- UI is heavily programmatic and productive for prototyping, but there is substantial **UI construction duplication** and event wiring complexity.
- Data definitions are readable and complete, but large registry files (items/spells/enemies/feats) are becoming difficult to maintain without tooling and stronger schema boundaries.
- Test coverage exists and is valuable, but naming and harness style are inconsistent.

High-level recommendation: next phase should focus on **decomposing orchestration and UI state management** before adding major new mechanics.

---

### 1) Current State Assessment

#### 1.1 Codebase metrics (including tests)

- Total `.cs` files: **109**
- Total lines: **71,211**
- Average file size: **653.3 lines/file**

Size distribution (using provided thresholds):
- `<300 lines`: **62 files** (good)
- `300–800 lines`: **27 files** (acceptable)
- `800–1500 lines`: **14 files** (consider splitting)
- `>1500 lines`: **6 files** (should split)

#### 1.2 Largest files

1. `Assets/Scripts/Core/GameManager.cs` — **12,370**
2. `Assets/Scripts/Character/CharacterController.cs` — **6,921**
3. `Assets/Scripts/UI/CombatUI.cs` — **5,275**
4. `Assets/Scripts/UI/CharacterCreationUI.cs` — **3,129**
5. `Assets/Scripts/Character/CharacterStats.cs` — **2,474**
6. `Assets/Scripts/CombatSystems/GrappleSystem.cs` — **1,748**

#### 1.3 Directory organization snapshot

| Directory | Files | Lines | Avg Lines/File |
|---|---:|---:|---:|
| `Character` | 16 | 13,771 | 860.7 |
| `Classes` | 8 | 877 | 109.6 |
| `Combat` | 12 | 2,925 | 243.8 |
| `CombatSystems` | 7 | 6,085 | 869.3 |
| `Core` | 5 | 14,338 | 2,867.6 |
| `Grid` | 4 | 1,337 | 334.2 |
| `Inventory` | 5 | 3,488 | 697.6 |
| `Magic` | 20 | 8,758 | 437.9 |
| `Tests` | 13 | 2,878 | 221.4 |
| `UI` | 19 | 16,754 | 881.8 |

#### 1.4 Complexity indicators

Using `lizard` function analysis:
- Total functions detected: **2,046**
- Functions with cyclomatic complexity >15: **141**
- Functions with cyclomatic complexity >30: **37**
- Functions length >100 lines: **89**
- Functions length >200 lines: **28**

Notable high-risk functions:
- `CharacterController.ResolveGrappleAction` — length **2473**, CCN **415**
- `CombatUI.UpdateActionButtons` — length **800**, CCN **382**
- `GameManager.ExecuteSpecialAttack` (in-core section) — length **350**, CCN **99**

---

### 2) File-by-File Evaluation of Major Files

Below uses your criteria (size, responsibilities, cohesion, dependencies, naming).

#### 2.1 `Core/GameManager.cs` (12,370 lines)
- **Size:** Should split (far beyond threshold)
- **Responsibilities:** Turn flow, AI, input handling, action economy, maneuver execution, spell paths, logging, UI mediation
- **Cohesion:** Mixed concerns; currently orchestrator + rules + presentation triggers
- **Dependencies:** Very high fan-out (`CombatUI`, grid, character, inventory, spells, systems)
- **Naming:** Mostly clear; many internal state flags are descriptive but volume is overwhelming

Assessment: despite partial-class split files in `CombatSystems`, this remains a **god orchestrator**.

#### 2.2 `Character/CharacterController.cs` (6,921 lines)
- **Size:** Should split
- **Responsibilities:** Combat resolution, grappling, attacks, conditions, inventory/equipment interactions, action commits
- **Cohesion:** Mixed (especially grappling + attack pipelines + condition effects)
- **Dependencies:** High coupling to `GameManager.Instance`, `Stats`, inventory, combat utilities
- **Naming:** Good local naming; method volume and overload depth reduce readability

Assessment: `ResolveGrappleAction` is an especially strong extraction candidate.

#### 2.3 `UI/CombatUI.cs` (5,275 lines)
- **Size:** Should split
- **Responsibilities:** HUD stats, action buttons, state labels, modal dialogs, spell menus, prompts, layout concerns
- **Cohesion:** Mixed UI composition + state evaluation + business gating
- **Dependencies:** Heavy dependence on `GameManager` queries and character state
- **Naming:** Clear labels, but class is too broad for maintainability

Assessment: `UpdateActionButtons` alone is effectively a subsystem.

#### 2.4 `UI/CharacterCreationUI.cs` (3,129 lines)
- **Size:** Should split
- **Responsibilities:** Multi-step workflow, selection logic, validation, summary rendering, quick-start flows, dynamic widget generation
- **Cohesion:** Better than `CombatUI`, but still too broad
- **Dependencies:** Class registry, races, deities, domains, feats, spells
- **Naming:** Generally clear

Assessment: wizard-style flow object + step components would reduce complexity.

#### 2.5 `Character/CharacterStats.cs` (2,474 lines)
- **Size:** Should split
- **Responsibilities:** Core stats, class feature derivation, encumbrance, spellcasting semantics, many derived properties
- **Cohesion:** Domain-related but broad; essentially multiple stat engines in one file
- **Dependencies:** many static databases and class checks via string class names
- **Naming:** Good property names, but class breadth is high

Assessment: good candidate for compositional stat calculators.

#### 2.6 `Core/SceneBootstrap.cs` (1,405 lines)
- **Size:** Consider splitting
- **Responsibilities:** scene setup + UI factory + button wiring + initialization
- **Cohesion:** Mixed runtime bootstrapping and UI construction
- **Dependencies:** Broad and direct
- **Naming:** clear

Assessment: bootstrap is carrying both composition root and UI builder duties.

#### 2.7 `Inventory/ItemDatabase.cs` (1,424 lines) and spell registration files
- **Size:** Acceptable-to-large for data catalogs
- **Responsibilities:** mostly registry data (good)
- **Cohesion:** high; low branching, mostly declarations
- **Dependencies:** low-to-medium
- **Naming:** good IDs and labels

Assessment: this is large due data volume, not algorithmic complexity; still needs better authoring workflow.

---

### 3) Identified Issues (Prioritized)

### HIGH PRIORITY

#### Issue 1: Orchestration concentration in `GameManager`
- **Problem:** Too many subdomains in one central class.
- **Impact:** Readability and onboarding cost; harder regression isolation; increased risk per change.
- **Recommendation:** Move from partial-method grouping to service/coordinator classes with explicit interfaces.
- **Effort:** High.

#### Issue 2: Combat logic concentration in `CharacterController`
- **Problem:** Character object mixes state + rules execution + special-case mechanics.
- **Impact:** Hard to test edge cases independently; brittle changes around grapple and action sequences.
- **Recommendation:** Extract `CharacterCombatResolver`, `GrappleResolver`, and `AttackSequenceResolver`.
- **Effort:** High.

#### Issue 3: Monolithic `CombatUI`
- **Problem:** One class owns panel construction, state derivation, and interaction flows.
- **Impact:** High CCN and long methods, slower iteration, high bug surface for button-state regressions.
- **Recommendation:** Split into sub-panels/controllers and introduce an action-state view model.
- **Effort:** High.

### MEDIUM PRIORITY

#### Issue 4: Programmatic UI construction duplication
- **Problem:** Repeated `MakeText/MakeButton/CreatePanel` and font-loading patterns across many UI files.
- **Impact:** Inconsistent styling, repeated fixes, UI drift.
- **Recommendation:** Central `UIFactory` + `UITheme` abstraction.
- **Effort:** Medium.

#### Issue 5: Static database sprawl and manual registration
- **Problem:** Data content authored as code across item/spell/feat/enemy registries.
- **Impact:** merge conflicts, manual validation burden, difficult tooling.
- **Recommendation:** move toward ScriptableObjects/JSON + schema validation.
- **Effort:** Medium.

#### Issue 6: Naming and test convention inconsistencies
- **Problem:** mixed `*Test.cs`/`*Tests.cs`, mixed test harness style.
- **Impact:** discoverability and automation friction.
- **Recommendation:** standardize naming/test runner integration.
- **Effort:** Low.

### LOW PRIORITY

#### Issue 7: Magic numbers in UI/layout and data-heavy files
- **Problem:** many hard-coded values for positions/colors/sizes.
- **Impact:** tune cost and visual inconsistency.
- **Recommendation:** constants/theme files/layout profiles.
- **Effort:** Low.

---

### 4) Recommendations by Category

#### 4.1 File Organization

Proposed top-level re-org (incremental):

```text
Assets/Scripts/
  Application/
    Combat/
      CombatFlowCoordinator.cs
      TurnCoordinator.cs
      ActionSelectionCoordinator.cs
    Bootstrap/
      SceneCompositionRoot.cs
      UICompositionRoot.cs
  Domain/
    Characters/
      CharacterStats.cs
      CharacterCombatState.cs
      Calculators/
    Combat/
      Resolvers/
      Maneuvers/
    Inventory/
    Magic/
  Data/
    Items/
    Spells/
    Feats/
    Enemies/
  Presentation/
    HUD/
    Panels/
    Dialogs/
    Common/
      UIFactory.cs
      UITheme.cs
  Tests/
```

#### 4.2 Code Structure & Separation of Concerns

- Keep `GameManager` as app-level orchestrator only; move rules to dedicated services.
- Convert giant switch/if action flows into command handlers:
  - `ISpecialAttackCommand` per attack type
  - `INpcBehaviorStrategy` per AI behavior
- Introduce explicit “query services” for UI button states instead of direct branching in `CombatUI`.

#### 4.3 Data Organization

- Keep core catalogs but progressively externalize high-volume declarative datasets:
  - `ItemDatabase` entries
  - spell registry definitions
  - enemy presets/encounter lists
- Add integrity validator pass at startup (duplicate IDs, missing icon IDs, invalid spell refs, etc.).

#### 4.4 UI Organization

Split `CombatUI` into component controllers:
- `CombatHUDController`
- `ActionButtonsPanelController`
- `SpecialAttackMenuController`
- `SpellCastingPanelController`
- `CombatLogController`
- `DialogController` (AoO confirm, list pickers, yes/no)

Use a shared model:
- `ActionButtonStateModel` computed once per refresh and bound to UI.

#### 4.5 Utility & Helpers

Create reusable helpers:
- `UIFactory` for text/button/panel builders
- `FontProvider` and `ColorPalette`
- `TextFormatters` for combat/status strings
- `RulesConstants` (`FeetPerSquare`, common DCs, caps)

---

### 5) Specific Refactoring Proposals

#### Recommendation 1: Split `CharacterController`

**Current:** single class handles attacks, grapple subsystem, maneuver resolution, state and utility logic.  
**Proposed:**
- `CharacterController` (state + facade only)
- `CharacterAttackResolver`
- `CharacterGrappleResolver`
- `CharacterManeuverResolver`
- `CharacterActionCommitter`

**Benefit:** smaller blast radius, targeted unit tests, easier reading.

#### Recommendation 2: Decompose `CombatUI`

**Current:** one class computes + renders + wires many menus and dialogs.  
**Proposed:**
- `CombatUI` (composition/wiring)
- `ActionButtonsPresenter` (moves `UpdateActionButtons` logic)
- `CombatPromptService` (AoO, confirmation, item/target pickers)
- `CombatPanelsFactory` (all panel construction)

**Benefit:** major reduction in CCN and method length hotspots.

#### Recommendation 3: Turn partial `GameManager` split into service modules

**Current:** partial class files hold large parts of the same mutable state.  
**Proposed:**
- `GameManager` uses injected services (`GrappleService`, `OverrunService`, `TurnUndeadService`, etc.)
- Services expose command-style APIs, receive context objects instead of touching global fields.

**Benefit:** true modularity and cleaner dependency graph.

#### Recommendation 4: Build a Data Layer

**Current:** static in-code registration for multiple domains.  
**Proposed:**
- Keep current APIs but back with serialized definitions (`ScriptableObject`/JSON)
- Introduce validation pipeline and generation scripts.

**Benefit:** non-code data edits, safer balancing, easier content expansion.

---

### 6) Code Quality Metrics & Findings

#### 6.1 God classes
- `GameManager` (12k+)
- `CharacterController` (6.9k)
- `CombatUI` (5.2k)

#### 6.2 Long/complex methods
- 28 functions >200 lines
- 37 functions with CCN >30
- largest outliers are concentrated in top 3 classes above

#### 6.3 Duplication patterns
- Repeated UI constructor helpers across multiple UI files.
- Repeated font/resource bootstrap patterns (`Resources.GetBuiltinResource<Font>`) across many UI classes.
- Repeated test harness scaffolding (`RunAll`, `Assert`, helper builders) in tests.

#### 6.4 Magic numbers
- High volume in UI/build files (`CharacterCreationUI`, `CombatUI`, `SceneBootstrap`) and data catalogs.
- Indicates need for centralized constants/theme definitions.

#### 6.5 Deep nesting and branching
- Most severe in `GameManager`, `CharacterController`, `CombatUI`.
- Creates maintainability risk even when naming is good.

---

### 7) Naming Convention Review

Strengths:
- Domain terminology is clear and D&D-accurate.
- Public API names are generally descriptive.
- Private fields often use consistent underscore prefix.

Inconsistencies to fix:
- Test file naming mixed (`*Test.cs` vs `*Tests.cs`).
- No namespace usage across scripts (all global scope), increasing collision risk over time.
- Mixed helper naming (`MakePanel` vs `CreatePanel`) for equivalent responsibilities.

Recommended standardization:
- Use namespaces by layer/domain (`Dnd35.Core`, `Dnd35.UI`, etc.).
- Enforce file and class naming rules via analyzer.
- Standardize test naming to `*Tests.cs`.

---

### 8) Architecture Pattern Opportunities

#### 8.1 Command Pattern (high value)
Use command handlers for action execution:
- `AttackCommand`, `DisarmCommand`, `SunderCommand`, `CastSpellCommand`, etc.

#### 8.2 Strategy Pattern (high value)
AI behaviors currently selected via switch can be strategy objects:
- `AggressiveMeleeStrategy`, `RangedKiterStrategy`, `DefensiveMeleeStrategy`

#### 8.3 Observer/Event Bus (medium value)
Reduce direct UI polling and coupling:
- publish events for turn changes, condition changes, action-economy updates.

#### 8.4 State Pattern (medium value)
Player/NPC subphase handling can be explicit state objects rather than flag clusters.

#### 8.5 Factory/Builder (medium value)
Centralize dynamic UI and character/enemy creation to remove duplicated panel construction and setup code.

---

### 9) Documentation Opportunities

High-value additions:
- Combat action lifecycle diagram (turn flow + subphase transitions)
- `GameManager` service map after decomposition
- “How to add a new special attack” guide
- Data authoring guide for items/spells/enemies
- Testing guide (runtime tests vs unit tests, naming conventions, execution path)

Inline docs needed:
- Document invariants for attack-sequence state flags
- Document grapple state transitions and pin rules
- Document UI state refresh contract (`UpdateActionButtons` replacement)

---

### 10) Prioritized Action Plan (Impact vs Effort)

#### Priority 1 (High Impact, Low-Medium Risk)
1. Extract `ActionButtonsPresenter` from `CombatUI.UpdateActionButtons`  
   - Benefit: immediate reduction of largest UI hotspot.  
   - Effort: **12–18 hours**.

2. Introduce shared `UIFactory` + `UITheme`  
   - Benefit: remove repeated panel/button/text boilerplate.  
   - Effort: **8–14 hours**.

3. Standardize test naming/harness scaffolding  
   - Benefit: discoverability and automation readiness.  
   - Effort: **3–6 hours**.

#### Priority 2 (High Impact, Medium Risk)
1. Extract grapple resolver from `CharacterController`  
   - Benefit: isolates highest complexity method cluster.  
   - Effort: **20–32 hours**.

2. Convert NPC behavior switch to strategies  
   - Benefit: cleaner AI extension path.  
   - Effort: **10–16 hours**.

3. Begin GameManager service decomposition (one subsystem at a time)  
   - Benefit: structural scaling path for future mechanics.  
   - Effort: **24–40 hours** initial slice.

#### Priority 3 (Medium Impact, Low Risk)
1. Centralize constants (layout, common DCs, tuning values)  
   - Effort: **6–10 hours**.
2. Add namespaces and analyzer rules  
   - Effort: **6–12 hours**.
3. Add architecture docs + extension guides  
   - Effort: **6–10 hours**.

#### Priority 4 (Nice to Have)
1. Externalize large declarative datasets to content assets  
   - Effort: **20–40+ hours** staged.
2. Add validation/generation tooling for catalogs  
   - Effort: **12–24 hours**.

---

### 11) Immediate Next-Step Backlog (Suggested Order)

1. `CombatUI.UpdateActionButtons` extraction (first, highest ROI).  
2. `CharacterController.ResolveGrappleAction` extraction.  
3. `SceneBootstrap` split into `SceneCompositionRoot` + `UICompositionRoot`.  
4. Introduce `UIFactory/UITheme` and migrate one UI at a time.  
5. Namespace + naming normalization pass.  
6. Begin service-oriented decomposition of `GameManager`.

---

### Appendix A — Additional Observations

- No C# namespaces are currently declared under `Assets/Scripts`.
- Partial `GameManager` exists across `Core/GameManager.cs` and files in `CombatSystems/`, but major orchestration remains concentrated in core file.
- Existing tests are useful and domain-focused; converting these runtime static suites to a unified test runner over time will improve CI readiness.

---

### Bottom Line

The project is in a strong feature-prototype stage with good domain fidelity. The biggest maintainability gains now come from:
1) **de-monolithing coordination classes**,  
2) **formalizing UI composition patterns**, and  
3) **isolating combat rules into testable service modules**.

These steps will make future mechanics significantly cheaper and safer to add.
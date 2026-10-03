> Verified against commit 0dd8e76 (2026-06-01) on 2026-10-02.

# Retired documentation

About 80 documents were written alongside the code between April and June 2026. In October 2026 each one was audited against commit `0dd8e76`, and none was fully accurate: the best matched the code about 90%, and most 20-60%.
The cleanup replaces them with a few living docs (see [`../README.md`](../README.md)). Content that was still accurate was carried into those docs.
The retired originals are no longer in the working tree, but git keeps them. To read one as it was:

```bash
git show 0dd8e76:"<path>"
```

Treat a retired doc as history, not as a description of the current code.

## Retired in the first cleanup pass (41)

| Path at 0dd8e76 | Kind | Accuracy | Content now in |
|---|---|---:|---|
| `ARCHITECTURE.md` | replaced | ~45% | docs/ARCHITECTURE.md |
| `Assets/Scripts/Equipment/magic_item_enchantment_implementation_plan.md` | historical | ~30% | — (no unique content left) |
| `Assets/Scripts/Tests/AidAnother_ManualTestScenarios.md` | merged | ~65% | docs/TESTING.md § Manual scenarios |
| `Assets/Scripts/Tests/Combat/SleepSpell_ManualTestScenarios.md` | merged | ~65% | docs/TESTING.md § Manual scenarios |
| `Assets/Scripts/Tests/README.md` | replaced | ~55% | docs/TESTING.md |
| `chromatic_orb_audit_report.md` | obsolete | ~5% | — (no unique content left) |
| `CODE_ARCHITECTURE_ANALYSIS.md` | historical | ~25% | open debt → docs/KNOWN_ISSUES.md + docs/issues/ |
| `CODEBASE_EVALUATION.md` | historical | ~30% | open debt → docs/KNOWN_ISSUES.md + docs/issues/ |
| `COMBAT_REFACTOR_VERIFICATION.md` | obsolete | ~5% | — (no unique content left) |
| `designs/dungeon_encounter_implementation_plan.md` | historical | ~25% | — (no unique content left) |
| `designs/encounter_generator_implementation_plan.md` | historical | ~50% | — (no unique content left) |
| `DEVELOPMENT_EXAMPLES.md` | merged | ~30% | docs/DEVELOPMENT_RECIPES.md |
| `Disarm_Attack_Tracking_Investigation.md` | merged | ~45% | docs/ARCHITECTURE.md § Combat attack economy |
| `docs/codebase_analysis_report.md` | historical | ~35% | open debt → docs/KNOWN_ISSUES.md + docs/issues/ |
| `Docs/complete_missing_creatures_audit.md` | historical | ~25% | — (no unique content left) |
| `docs/designs/major_wondrous_items_by_complexity.md` | obsolete | ~40% | — (no unique content left) |
| `docs/designs/metamagic_feats_implementation_plan.md` | historical | ~40% | gaps → docs/KNOWN_ISSUES.md + docs/issues/ + SPELLS_AND_METAMAGIC.md |
| `docs/designs/npc_template_system_FINAL.md` | obsolete | ~30% | — (no unique content left) |
| `docs/designs/npc_templates_by_level.md` | historical | ~25% | — (no unique content left) |
| `docs/designs/planar_travel_system_design.md` | historical | ~20% | (PlanarTravelSystem exists but is never called; noted in KNOWN_ISSUES) |
| `docs/designs/rings_by_complexity.md` | obsolete | ~45% | — (no unique content left) |
| `docs/designs/rings_executive_summary.md` | historical | ~25% | — (no unique content left) |
| `docs/designs/rings_priority_matrix.md` | obsolete | ~40% | — (no unique content left) |
| `docs/designs/rods_priority_matrix.md` | obsolete | ~35% | — (no unique content left) |
| `docs/designs/sprint2_quick_reference.md` | obsolete | ~50% | — (no unique content left) |
| `docs/designs/sprint3_implementation_plan.md` | historical | ~30% | — (no unique content left) |
| `docs/designs/wondrous_items_priority_matrix.md` | obsolete | ~35% | — (no unique content left) |
| `docs/dragon-spellcasting-analysis.md` | obsolete | ~25% | — (no unique content left) |
| `Docs/EmanationArchitectureReview.md` | merged + historical | ~35% | docs/ARCHITECTURE.md § Spell area effects |
| `docs/monster-inventory-by-type.md` | obsolete | ~50% | — (derivable from NPCDatabase; stale counts) |
| `docs/MOUNTED_COMBAT_GUIDE.md` | obsolete | ~30% | — (mounted combat is unreachable in play; noted in KNOWN_ISSUES) |
| `docs/sna_creature_audit.md` | obsolete | ~15% | — (no unique content left) |
| `docs/undead-by-intelligence.md` | historical | ~50% | — (no unique content left) |
| `docs/undead-compendium.html` | obsolete | ~45% | — (and its .pdf) |
| `FOLDER_STRUCTURE_MAPPING.csv` | obsolete | ~88% | — (git show --name-status -M 8a79a42 is the true record) |
| `FOLDER_STRUCTURE_PROPOSAL.md` | merged + historical | ~60% | docs/ARCHITECTURE.md § Folder map |
| `phase5_validation.py` | obsolete | ~55% | — (crashes: expects level 9; writes to /home/ubuntu) |
| `REFACTORING_GUIDE.md` | merged | ~50% | docs/ARCHITECTURE.md § Dormant infrastructure; docs/DEVELOPMENT_RECIPES.md |
| `SERVICES.md` | merged | ~60% | docs/ARCHITECTURE.md § Services |
| `spell_range_audit_report.md` | obsolete | ~35% | — (no unique content left) |
| `test_range_calculator.py` | obsolete | ~90% | — (Tests/Combat/RangeCalculatorTests.cs covers the real code) |

## Still in the tree, pending consolidation (39)

These docs are just as stale, but their useful content goes into per-system docs (`docs/systems/`) that are not written yet. Until then, prefer [`../ARCHITECTURE.md`](../ARCHITECTURE.md) and [`../KNOWN_ISSUES.md`](../KNOWN_ISSUES.md). Read these only for design intent, and check every claim against the code.

| Path | Accuracy | Planned destination |
|---|---:|---|
| `Assets/Scripts/AI/README.md` | ~75% | docs/systems/AI.md |
| `Assets/Scripts/Equipment/armor_special_abilities_complete.md` | ~50% | docs/systems/MAGIC_ITEMS.md § Enchantments and materials |
| `Assets/Scripts/Equipment/enchantment_system_tests.md` | ~50% | docs/systems/MAGIC_ITEMS.md § Enchantments and materials |
| `Assets/Scripts/Equipment/MATERIAL_SYSTEM_ARCHITECTURE.md` | ~68% | docs/systems/MAGIC_ITEMS.md § Enchantments and materials |
| `Assets/Scripts/Equipment/shield_special_abilities_complete.md` | ~45% | docs/systems/MAGIC_ITEMS.md § Enchantments and materials |
| `Assets/Scripts/Equipment/weapon_special_abilities_complete.md` | ~65% | docs/systems/MAGIC_ITEMS.md § Enchantments and materials |
| `designs/encounter_generator_design.md` | ~55% | docs/systems/ENCOUNTERS.md |
| `designs/missing_creatures_implementation_plan.md` | ~25% | docs/systems/CREATURES.md + docs/DEVELOPMENT_RECIPES.md |
| `docs/designs/creature_class_application_system.md` | ~20% | docs/systems/CREATURES.md § NPC classes & templates |
| `docs/designs/dmg_template_verification.md` | ~25% | docs/systems/CREATURES.md § NPC classes & templates |
| `docs/designs/major_wondrous_items_implementation_plan.md` | ~30% | backlog → docs/systems/MAGIC_ITEMS.md § Wondrous |
| `docs/designs/major_wondrous_items_priority_matrix.md` | ~45% | docs/systems/MAGIC_ITEMS.md § Wondrous |
| `docs/designs/metamagic_rods_specification.md` | ~40% | docs/systems/MAGIC_ITEMS.md § Rods (backlog) |
| `docs/designs/npc_class_definitions.md` | ~45% | docs/systems/CREATURES.md § NPC classes & templates |
| `docs/designs/npc_classes_implementation_plan.md` | ~35% | docs/systems/CREATURES.md § NPC classes & templates |
| `docs/designs/ring_systems_required.md` | ~20% | docs/systems/MAGIC_ITEMS.md § Rings |
| `docs/designs/rings_implementation_plan.md` | ~35% | docs/systems/MAGIC_ITEMS.md § Rings |
| `docs/designs/rods_by_complexity.md` | ~40% | docs/systems/MAGIC_ITEMS.md § Rods |
| `docs/designs/rods_implementation_plan.md` | ~35% | docs/systems/MAGIC_ITEMS.md § Rods |
| `docs/designs/sprint2_detailed_implementation_plan.md` | ~40% | docs/systems/MAGIC_ITEMS.md § Rings |
| `docs/designs/wondrous_items_by_complexity.md` | ~40% | docs/systems/MAGIC_ITEMS.md § Wondrous |
| `docs/designs/wondrous_items_by_slot.md` | ~55% | docs/systems/MAGIC_ITEMS.md § Wondrous |
| `docs/designs/wondrous_items_implementation_plan.md` | ~35% | docs/systems/MAGIC_ITEMS.md § Wondrous |
| `docs/dragon-spellcasting-audit.md` | ~35% | docs/systems/CREATURES.md § Dragons |
| `docs/existing_systems_audit.md` | ~40% | docs/ARCHITECTURE.md (API cheat sheet) + docs/systems/MAGIC_ITEMS.md |
| `Docs/lycanthrope_template_analysis.md` | ~55% | docs/systems/CREATURES.md § Templates |
| `docs/METAMAGIC_PREPARATION_GUIDE.md` | ~90% | docs/systems/SPELLS_AND_METAMAGIC.md § Metamagic |
| `docs/PLAYER_GUIDE_MAGIC_ITEMS.md` | ~35% | docs/systems/MAGIC_ITEMS.md |
| `docs/sna_implementation_plan.md` | ~45% | docs/systems/CREATURES.md § Summoning |
| `docs/spell_duplicate_analysis.md` | ~25% | docs/systems/SPELLS_AND_METAMAGIC.md § Spell IDs & aliases |
| `Docs/template_quality_comparison.md` | ~40% | docs/systems/CREATURES.md § Templates |
| `Docs/TerrainManipulationSystem.md` | ~55% | docs/systems/CREATURES.md § Monster special abilities |
| `docs/tier3_specific_items_implementation_plan.md` | ~40% | backlog → docs/systems/MAGIC_ITEMS.md § Specific items |
| `domain_powers_implementation_plan.md` | ~25% | docs/systems/SPELLS_AND_METAMAGIC.md § Cleric domains |
| `GIBBERING_MOUTHER_IMPLEMENTATION.md` | ~40% | docs/systems/CREATURES.md § Monster special abilities |
| `MONSTER_SPECIAL_ABILITIES_ANALYSIS.md` | ~30% | docs/systems/CREATURES.md § Monster special abilities (backlog) |
| `spell_ranges.md` | ~90% | docs/systems/SPELLS_AND_METAMAGIC.md § Ranges |
| `STATUS_EFFECTS_WORKFLOW.md` | ~55% | docs/architecture/combat-and-grid.md + docs/systems/RULES_COVERAGE.md |
| `summon_monster_documentation.md` | ~40% | docs/systems/CREATURES.md § Summoning |

## Kept and being updated (4)

| Path | Accuracy | Plan |
|---|---:|---|
| `Assets/StreamingAssets/CreatureTokens/README.md` | ~55% | in place (remove the deleted CreatureTokenLoader integration section) |
| `docs/designs/creature_trapping_system_design.md` | ~20% | docs/designs/ (status header added) |
| `docs/designs/item_creation_feats_implementation_plan.md` | ~40% | docs/designs/ (status header added) |
| `docs/designs/missing_classes_implementation_plan.md` | ~28% | docs/designs/ (status header added) |

## Exported copies (.docx / .pdf)

The first cleanup pass removed all 119 tracked `.docx` and `.pdf` files. They were generated exports of the Markdown docs, and several had already drifted from their source. `*.docx` and `*.pdf` are now in `.gitignore`. To list them:
`git ls-tree -r --name-only 0dd8e76 | grep -E "\.(docx|pdf)$"`.

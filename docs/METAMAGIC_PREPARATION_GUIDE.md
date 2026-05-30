# Preparing Spells with Metamagic (Prepared Casters)

This guide explains how a prepared caster (Wizard, Cleric, Druid) applies metamagic
feats while preparing spells, and how that flow is implemented in the prototype.

> **Rules basis (D&D 3.5e, PHB p.88):** A prepared spellcaster chooses metamagic
> *at the time of preparation*. A metamagic-enhanced spell occupies a slot of the
> spell's adjusted level (base level + the feat's level adjustment) and is cast in
> the normal casting time. (Quicken Spell is the exception — it changes casting
> time, not slot rules, but still raises the slot level.)

---

## The player flow, step by step

1. **Open Spell Preparation.** This screen appears during pre-combat preparation
   (resting / morning). Each available spell slot is shown as its own row, grouped
   and labeled by level (e.g. `Level 3 Slot 1:`).

2. **Find the slot you want to fill.** The slot level is fixed by the row — a
   `Level 3 Slot` row can only hold something whose *effective* level is 3.

3. **Open that row's dropdown.** The dropdown lists, for that exact slot level:
   - `(Empty)` — leave the slot unprepared.
   - **Normal spells** of that level you know (e.g. `Fireball`).
   - **⚡ Metamagic options** — lower-level spells you know, already combined with
     metamagic feats *your character has*, where the math lands exactly on this
     slot's level.

4. **Pick a ⚡ option to apply metamagic.** Each metamagic option spells out the
   math, for example:

   ```
   ⚡ Empowered Magic Missile  (lvl 1 +2 → lvl 3)
   ```

   This reads as: *Base spell Magic Missile (1st level) + Empower Spell (+2 levels)
   = a 3rd-level slot* — which is exactly the slot you are filling.

5. **Confirm Preparation.** The slot now holds the metamagic-enhanced spell. When
   you cast it in combat it produces the enhanced effect (empowered damage,
   extended duration, etc.).

---

## Why a dropdown instead of checkboxes?

For **prepared** casters the slot level is decided *before* you choose the spell —
you are filling a specific slot. So instead of asking you to pick a spell and then
toggle feats and hope the total fits, the UI does the arithmetic for you and only
offers combinations that fit the slot exactly. This guarantees:

- You can never prepare a combination that is too big or too small for the slot.
- The adjusted level is always shown up front (`lvl 1 +2 → lvl 3`).
- It is always obvious which slot the metamagic spell will consume — it is the row
  you opened.

> Spontaneous casters (Sorcerer/Bard) work differently: they choose metamagic at
> *cast time* and pay with a higher-level slot then. That flow lives in the combat
> casting UI, not here.

---

## What metamagic options are generated

When a slot row is built (`SpellPreparationUI.CreateSlotRow` → option list), for a
non-domain, non-specialist slot above level 0 the UI generates options from every
lower-level spell the caster knows, combined with the metamagic feats the caster
actually has (`SpellcastingComponent.GetKnownMetamagicFeats()`):

| Feat            | Level adjustment | Notes                                              |
|-----------------|------------------|----------------------------------------------------|
| Enlarge Spell   | +1               | Only spells with a range > 0                        |
| Extend Spell    | +1               | Only spells with a duration                         |
| Silent Spell    | +1               | Any spell                                           |
| Still Spell     | +1               | Any spell                                           |
| Empower Spell   | +2               | Only spells with numeric (damage/healing) effects   |
| Maximize Spell  | +3               | Only spells with dice (damage/healing)              |
| Widen Spell     | +3               | Only area spells                                    |
| Quicken Spell   | +4               | Any spell                                           |
| Heighten Spell  | variable         | Raises the spell to *exactly* this slot's level     |

Generation rules:

- **Single fixed-adjustment feats** — an option appears only when
  `baseLevel + adjustment == slotLevel`.
- **Heighten Spell** — for any known spell with `baseLevel < slotLevel`, a single
  option is offered that raises it to exactly `slotLevel`
  (`HeightenToLevel = slotLevel`).
- **Two-feat combinations** — pairs of fixed-adjustment feats are offered when
  `baseLevel + adj1 + adj2 == slotLevel` (e.g. Empower + Silent on a 1st-level
  spell fits a 4th-level slot).
- **Applicability** is checked per feat via `MetamagicData.IsApplicable` (e.g.
  Empower only on damage/healing spells), so nonsensical combinations never appear.
- **Domain and specialist slots are excluded** — these cannot hold metamagic spells.

---

## Validation (defense in depth)

Even though the dropdown only offers valid combinations, the backend re-validates on
preparation in `SpellcastingComponent.PrepareSpellInSlotWithMetamagic`:

- The effective level (`MetamagicData.GetEffectiveSpellLevel(baseLevel)`) must equal
  the target slot's level, otherwise preparation is rejected.
- Domain / specialist slots reject metamagic.
- The base spell must be in the slot's caster-class spell list.

The effective level is also capped at 9th level (`GetEffectiveSpellLevel`).

---

## Key code references

- `Assets/Scripts/UI/Spells/SpellPreparationUI.cs`
  - `CreateSlotRow` / option-list build — generates the per-slot dropdown options.
  - `BuildMetamagicOptionLabel` — formats the `⚡ ... (lvl X +Y → lvl Z)` labels.
  - `OnSlotChanged` — routes a chosen option to the right preparation method.
- `Assets/Scripts/Spell/Components/SpellcastingComponent.cs`
  - `GetKnownMetamagicFeats`, `GetKnownSpellsForClass`,
    `PrepareSpellInSlotWithMetamagic`.
- `Assets/Scripts/Spell/Components/MetamagicData.cs`
  - `IsApplicable`, `GetLevelAdjustment`, `GetEffectiveSpellLevel`,
    `GetDisplayName` (combined adjective name used in labels).

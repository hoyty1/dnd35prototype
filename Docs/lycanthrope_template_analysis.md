# Lycanthrope Template Implementation Analysis

**Date:** 2026-05-23  
**Project:** `/home/ubuntu/dnd35prototype`  
**Status:** ✅ Fully Implemented — Quality improvements possible

---

## 1. Current Implementation Status

### Files & Line Counts

| File | Lines | Purpose |
|------|------:|---------|
| `Assets/Scripts/Character/LycanthropeTemplate.cs` | 1,124 | Template logic + Factory (8 variants) + Enum + Helpers |
| `Assets/Scripts/Character/NPCDatabase_Lycanthropes.cs` | 95 | Registration + 7 encounter presets |
| `Assets/Scripts/Effects/Disease.cs` | +1 line | `Lycanthropy` added to `DiseaseType` enum |
| `Assets/Scripts/Character/NPCDatabase.cs` | +2 lines | `RegisterCreatures_Lycanthropes()` in Init, `GetLycanthropeEncounterPresets()` in presets |
| **Total** | ~1,222 | |

### Brace Balance: ✅ Both files balanced (0 offset)

### Classes/Types Defined

| Type | Name | Purpose |
|------|------|---------|
| `static class` | `LycanthropeTemplate` | Core template logic (`Apply()` + helpers) |
| `struct` | `AnimalFormModifiers` | Data container for animal form stats |
| `enum` | `LycanthropeAnimalType` | 7 animal types (Wolf, DireWolf, Rat, Boar, DireBoar, Tiger, BrownBear) |
| `static class` | `LycanthropeFactory` | 8 pre-defined variants + `CreateFromRegistered()` |

### Registered Variants (8 total)

| ID | Name | Base Creature | Animal | CR | Natural? |
|----|------|--------------|--------|---:|----------|
| `werewolf` | Werewolf (Hybrid) | Human Warrior 1 | Wolf | 3 | ✅ |
| `werewolf_lord` | Werewolf Lord (Hybrid) | Human Fighter 10 | Dire Wolf | 14 | ✅ |
| `wererat` | Wererat (Hybrid) | Human Rogue 1 | Dire Rat | 2 | ✅ |
| `wereboar` | Wereboar (Hybrid) | Human Barbarian 1 | Boar | 4 | ✅ |
| `weretiger` | Weretiger (Hybrid) | Human Noble 4 | Tiger | 8 | ✅ |
| `werebear` | Werebear (Hybrid) | Human Commoner 1 | Brown Bear | 5 | ✅ |
| `dire_wereboar` | Dire Wereboar (Hybrid) | Human Barbarian 1 | Dire Boar | 7 | ✅ |
| `werewolf_afflicted` | Afflicted Werewolf (Hybrid) | Human Commoner 1 | Wolf | 2 | ❌ |

### Encounter Presets (7 total)

| ID | Name | EL | Description |
|----|------|----|-------------|
| `werewolf_pack` | 🐺 Werewolf Pack | 5 | 3 werewolves |
| `wererat_ambush` | 🐀 Wererat Ambush | 5 | 4 wererats |
| `wereboar_rampage` | 🐗 Wereboar Rampage | 6 | 2 wereboars |
| `weretiger_hunt` | 🐅 Weretiger Hunt | 8 | Solo weretiger |
| `lycanthrope_menagerie` | 🌕 Lycanthrope Menagerie | 8 | Mixed showcase |
| `werewolf_lord_encounter` | 🐺👑 Werewolf Lord | 15 | Boss + wolf escorts |
| `cursed_villagers` | 🌑 Cursed Villagers | 4 | 3 afflicted werewolves |

---

## 2. D&D 3.5e Rules Accuracy

### Stat Verification Results: ✅ ALL 6 TESTED VARIANTS PASS

Automated verification confirmed all hybrid form stat calculations match MM expectations:

| Variant | STR | DEX | CON | WIS | Total HD | BAB | Nat Armor | DR |
|---------|-----|-----|-----|-----|----------|-----|-----------|-----|
| Werewolf | 15 ✅ | 15 ✅ | 16 ✅ | 13 ✅ | 3 ✅ | 2 ✅ | 4 ✅ | 10/silver ✅ |
| Wererat | 10 ✅ | 21 ✅ | 14 ✅ | 14 ✅ | 2 ✅ | 0 ✅ | 3 ✅ | 10/silver ✅ |
| Wereboar | 20 ✅ | 10 ✅ | 20 ✅ | 12 ✅ | 4 ✅ | 3 ✅ | 8 ✅ | 10/silver ✅ |
| Weretiger | 26 ✅ | 20 ✅ | 18 ✅ | 14 ✅ | 10 ✅ | 8 ✅ | 5 ✅ | 10/silver ✅ |
| Werebear | 30 ✅ | 12 ✅ | 22 ✅ | 14 ✅ | 7 ✅ | 4 ✅ | 7 ✅ | 10/silver ✅ |
| Afflicted WW | 14 ✅ | 15 ✅ | 15 ✅ | 12 ✅ | 3 ✅ | 1 ✅ | 4 ✅ | 5/silver ✅ |

### Rules Implemented

| Rule | Status | Details |
|------|--------|---------|
| **Type: keep base + Shapechanger subtype** | ✅ | Base type preserved, "Shapechanger" tag added |
| **HD = creature HD + animal HD** | ✅ | Combined correctly for all variants |
| **Combined BAB** | ✅ | creature BAB + animal BAB |
| **Physical ability modifiers from animal** | ✅ | STR/DEX/CON modifiers per MM p.175 table |
| **WIS +2 all lycanthropes** | ✅ | Applied universally |
| **Natural armor = max(creature, animal) + 2** | ✅ | Correct formula |
| **DR 10/silver (natural)** | ✅ | |
| **DR 5/silver (afflicted)** | ✅ | |
| **Hybrid form: 2 claws + bite** | ✅ | Correct natural attacks with size-appropriate damage |
| **Bite = secondary (half STR)** | ✅ | `BonusDamageSource.StrengthHalf` |
| **Iron Will bonus feat** | ✅ | Added if not already present |
| **Scent in all forms** | ✅ | `HasScent = true` |
| **Curse of Lycanthropy (natural only)** | ✅ | `HasDiseaseOnHit = true, DiseaseType.Lycanthropy` on bite |
| **Afflicted cannot spread curse** | ✅ | No disease on hit when `isNatural = false` |
| **Trip on wolf/dire wolf bite** | ✅ | `HasTripAttack = true` with bonus |
| **Pounce for weretiger hybrid** | ✅ | `HasPounce = true` (tiger only) |
| **Improved grab (animal form only)** | ✅ | Correctly NOT carried to hybrid |
| **Rake (animal form only)** | ✅ | Correctly NOT carried to hybrid |
| **Hybrid size = larger of creature/animal** | ✅ | `Mathf.Max()` on `SizeCategory` |
| **CR calculation by animal HD** | ✅ | MM p.175 table (1-2 HD: +2, 3-5: +3, 6-10: +4) |
| **Low-light vision** | ⚠️ | Listed in `SpecialAbilities` text but not mechanically tracked (no system exists) |
| **Lycanthropic empathy** | ⚠️ | Listed in `SpecialAbilities` text but not mechanically tracked |

### Rules NOT Implemented (Design Choices)

| Rule | Status | Reason |
|------|--------|--------|
| **Three-form system (human/animal/hybrid)** | ❌ | Only hybrid form registered — correct for combat encounters |
| **Alternate Form as standard action** | ❌ | No form-shifting combat mechanic — noted in abilities text |
| **Control shape checks (afflicted)** | ❌ | No transformation control system — would need new mechanic |
| **Lycanthropic empathy checks** | ❌ | No social skill system in combat prototype |
| **Alignment restrictions per type** | ⚠️ | Not enforced mechanically (werebear=LG, werewolf=CE, etc.) |
| **Type change to "Magical Beast" in animal form** | N/A | Only hybrid form exists |
| **Vulnerability to removal of lycanthropy** | N/A | No curse removal system |

---

## 3. Quality Score

### Metric Breakdown (1-10)

| Metric | Score | Notes |
|--------|------:|-------|
| **D&D Rules Accuracy** | 9 | All verifiable hybrid stats correct. Missing only non-combat flavor rules. |
| **Code Organization** | 8 | Clean separation: Template → Factory → Registration. Good XML docs. |
| **Extensibility** | 7 | Easy to add new animal types via `LycanthropeAnimalType` enum + `GetAnimalModifiers()`. |
| **Code Reuse** | 6 | No `StripSpecialEffects` duplication (handles attacks differently). Some base creature def duplication with Skeleton/Zombie. |
| **Documentation** | 9 | MM page references, inline stat verification comments, comprehensive XML docs. |
| **Integration Quality** | 7 | Disease system integrated. NPCDatabase registration correct. No runtime application support. |
| **Error Handling** | 7 | Null checks, fallback defaults in enum switch, Debug.LogError for unknowns. |
| **Testing Infrastructure** | 8 | 7 encounter presets covering all CR ranges. Stat verification comments inline. |
| **Composability** | 3 | Cannot compose with other templates. No `ICreatureTemplate` integration. |
| **Runtime Application** | 2 | Build-time only. Cannot apply to arbitrary creatures at runtime. |
| **Multi-Form Support** | 2 | Only hybrid form. No form-switching mechanic. |
| **Overall** | **6.2** | |

### Comparison to Other Templates

| Metric | Celestial/Fiendish | Skeleton/Zombie | Lycanthrope |
|--------|---:|---:|---:|
| D&D Rules Accuracy | 9 | 9 | 9 |
| Modularity | 9 | 6 | 7 |
| Extensibility | 9 | 7 | 7 |
| Code Reuse | 8 | 4 | 6 |
| Composability | 8 | 2 | 3 |
| Runtime Application | 9 | 2 | 2 |
| Multi-Form Support | N/A | N/A | 2 |
| **Overall** | **8.3** | **6.3** | **6.2** |

---

## 4. Specific Issues

### 4.1 No `ICreatureTemplate` Integration (Medium Priority)

The Lycanthrope template doesn't participate in the `CreatureTemplateRegistry`. This means:
- Cannot be applied at runtime (e.g., if a PC gets cursed and transforms)
- Cannot be composed with other templates (e.g., "Fiendish Werewolf")
- `AppliedTemplateIds` is set to `{ "lycanthrope", "[prefix]" }` but the registry doesn't know about it

### 4.2 No `StripSpecialEffects` Duplication (✅ Not an Issue)

Unlike Skeleton/Zombie, the Lycanthrope template handles natural attacks by **replacing them entirely** (creates fresh claw + bite attacks for hybrid form) rather than stripping effects from existing ones. This is actually the **correct approach** for lycanthropes per MM — the hybrid form gets its own natural attacks, not modified versions of the base creature's.

### 4.3 Base Creature Definition Duplication (Low-Medium Priority)

The Lycanthrope factory defines base creatures inline that overlap with other templates:
- `base_human_warrior` — also in SkeletonFactory and ZombieFactory (similar but not identical stats)
- `base_human_commoner` — also in ZombieFactory (similar)
- `base_human_barbarian` — unique to Lycanthrope (2 variants: wereboar + dire wereboar)

Note: The Lycanthrope base creatures are **not identical** to the Skeleton/Zombie ones — they have different ability scores, equipment, feats, and BaseHitDieHP. This is intentional — the MM lists different base creatures for each template example. So this is less "duplication" and more "similar-but-distinct base creatures."

### 4.4 Only Hybrid Form Registered (Design Choice, Not Bug)

The template only builds **hybrid form** stat blocks. This is noted in the code comments:

> "Our implementation registers the HYBRID form stat block since that's the most common combat encounter form."

For a combat prototype, this is correct. You almost always encounter lycanthropes in hybrid form during combat. The human form is for RP/social encounters, and the animal form is a secondary combat option. However, if the game adds:
- PC lycanthropy (curse system)
- NPC form-shifting during combat
- Voluntary/involuntary transformation triggers

...then multi-form support would be needed.

### 4.5 Disease System Integration (Partial)

The Lycanthropy curse is implemented via the disease system:
- `DiseaseType.Lycanthropy` exists in the enum
- Bite attacks on natural lycanthropes set `HasDiseaseOnHit = true` + `DiseaseOnHitType = DiseaseType.Lycanthropy`
- The combat system (`CharacterController.cs` line 4501) properly calls `target.ExposeToDisease()` on hit

**What's missing:**
- No disease resolution for Lycanthropy specifically (no Fort DC 15 save implementation for this specific disease)
- No mechanical effects after contracting Lycanthropy (no involuntary transformation, no alignment shift)
- No cure/removal system (Belladonna, Remove Disease within 3 days, etc.)

The disease hookup is a **placeholder** — it fires the exposure event but the downstream effects aren't lycanthropy-specific yet.

### 4.6 AI Archetype Limitations (Minor)

The template uses:
- `NPCAIProfileArchetype.Berserk` for most lycanthropes
- `NPCAIProfileArchetype.Humanoid` for wererat

The enum doesn't have a `Lycanthrope` or `ShapeChanger` archetype. The current choices are reasonable (berserk = charge-and-attack, humanoid = tactical flanking) but wouldn't handle form-switching AI.

### 4.7 No Alignment Enforcement (Minor)

Per MM, each lycanthrope type has a specific alignment tendency:
- Werewolf: CE (Chaotic Evil)
- Wererat: LE (Lawful Evil)
- Wereboar: N (True Neutral)
- Weretiger: N (True Neutral)
- Werebear: LG (Lawful Good)

The template doesn't set `CharacterAlignment` — it inherits from the base creature. This matters for Smite Evil/Good targeting and Protection from Evil/Good effects.

---

## 5. What the Lycanthrope Template Does Better Than Skeleton/Zombie

1. **No `StripSpecialEffects` duplication** — Creates fresh attacks instead of stripping existing ones
2. **More complex template logic** — Merges two creature stat blocks (creature + animal), not just converting one
3. **Data-driven animal types** — `AnimalFormModifiers` struct + `GetAnimalModifiers()` switch makes adding new animal types clean
4. **Natural vs Afflicted handling** — Single `isNatural` parameter cleanly controls DR amount and curse spreading
5. **Proper disease system integration** — Uses existing `DiseaseOnHitType` on bite attacks
6. **More varied factory** — 8 variants spanning CR 2-14 with different base classes and equipment

---

## 6. What Celestial/Fiendish Does Better

1. **`ICreatureTemplate` interface** — Lycanthrope uses static class pattern
2. **Registry participation** — Lycanthrope can't be resolved by string ID at runtime
3. **Runtime application** — Can apply Celestial/Fiendish to any creature dynamically
4. **Shared base class** — `OutsiderTemplateBase` eliminates duplication; Lycanthrope has no base class
5. **Combat action system** — `TemplateSmiteSystem.cs` (247 lines) is a full combat action with UI, AI, targeting; Lycanthrope has no equivalent dedicated combat system
6. **Template composition** — `AppliedTemplateIds` list supports stacking; Lycanthrope is standalone

---

## 7. Refactoring Plan: Bringing Lycanthrope to Celestial/Fiendish Quality

### Phase A: `ICreatureTemplate` Wrapper (Effort: ~1 hour)

Create a thin wrapper that implements `ICreatureTemplate`:

```csharp
// Assets/Scripts/Character/Templates/LycanthropeCreatureTemplate.cs
public sealed class LycanthropeCreatureTemplate : ICreatureTemplate
{
    public string TemplateId => "lycanthrope";
    
    // Default to wolf hybrid for runtime application
    private LycanthropeAnimalType _animalType;
    private bool _isNatural;
    
    public LycanthropeCreatureTemplate(
        LycanthropeAnimalType animalType = LycanthropeAnimalType.Wolf,
        bool isNatural = true)
    {
        _animalType = animalType;
        _isNatural = isNatural;
    }
    
    public void ApplyToDefinition(NPCDefinition definition)
    {
        // Apply in-place since ApplyTemplatesClone already cloned
        NPCDefinition result = LycanthropeTemplate.Apply(definition, _animalType, _isNatural);
        if (result != null)
            definition.CopyFieldsFrom(result);
    }
}
```

**Challenge:** The Lycanthrope template requires an `animalType` parameter that the generic `ICreatureTemplate.ApplyToDefinition(NPCDefinition)` interface doesn't support. Options:
1. **Default animal type** — Use Wolf as default, override via `AppliedTemplateIds` naming convention (e.g., `"lycanthrope_wolf"`, `"lycanthrope_tiger"`)
2. **Extended metadata** — Add an optional `TemplateMetadata` dictionary to `NPCDefinition` that templates can read
3. **Per-animal-type templates** — Register each as a separate template: `"werewolf"`, `"wererat"`, `"wereboar"`, etc.

**Recommendation:** Option 3 is cleanest — register each lycanthrope type as its own template ID:

```csharp
{ "werewolf", new LycanthropeCreatureTemplate(LycanthropeAnimalType.Wolf, true) },
{ "wererat", new LycanthropeCreatureTemplate(LycanthropeAnimalType.Rat, true) },
{ "wereboar", new LycanthropeCreatureTemplate(LycanthropeAnimalType.Boar, true) },
{ "weretiger", new LycanthropeCreatureTemplate(LycanthropeAnimalType.Tiger, true) },
{ "werebear", new LycanthropeCreatureTemplate(LycanthropeAnimalType.BrownBear, true) },
```

### Phase B: Multi-Form System (Effort: ~4-6 hours)

This is the **biggest gap** and the hardest to close. Full multi-form support would require:

#### B.1: Form Data Structure

```csharp
public class LycanthropeFormData
{
    public NPCDefinition HumanoidForm;  // base creature stats
    public NPCDefinition AnimalForm;    // full animal stats
    public NPCDefinition HybridForm;    // merged stats (what we have now)
    public LycanthropeAnimalType AnimalType;
    public bool IsNatural;
}
```

#### B.2: Form-Switching Combat Action

Similar to `TemplateSmiteSystem.cs`, create `LycanthropeFormShiftSystem.cs`:

```csharp
public partial class GameManager
{
    public bool CanShiftForm(CharacterController actor, out string reason) { ... }
    public void OnShiftFormButtonPressed() { ... }
    public void ExecuteFormShift(CharacterController actor, LycanthropeForm targetForm) { ... }
}
```

- Shifting is a standard action (or move-equivalent for natural lycanthropes at higher levels)
- Replace the NPC's stat block with the appropriate form's stats
- Update visual appearance (sprite color)
- Trigger Fortitude save for afflicted lycanthropes (Control Shape DC 25)

#### B.3: Involuntary Transformation System

- Full moon trigger (if time tracking exists)
- Damage threshold trigger (when below 50% HP in human form)
- Control Shape check (WIS-based, DC 25 to resist)
- Berserk mode when involuntarily transformed

#### B.4: AI Form Selection

Add form-switching logic to AI:
- If in human form and combat starts → shift to hybrid
- If low HP and in animal form → consider fleeing
- Afflicted: may involuntarily shift if Control Shape fails

**Recommendation:** This is a **significant new system**. Only implement when:
- PC lycanthropy curse is added
- NPC AI needs to shift forms during combat
- Multi-phase boss encounters are desired

### Phase C: Curse System Deep Integration (Effort: ~2-3 hours)

#### C.1: Lycanthropy-Specific Disease Resolution

```csharp
// In Disease resolution system:
case DiseaseType.Lycanthropy:
    // DC 15 Fort save
    // If failed: creature contracts lycanthropy
    // Incubation: next full moon
    // Effect: involuntary transformation
    break;
```

#### C.2: Belladonna Cure

```csharp
// Within 1 hour of contracting: DC 20 Heal check
// Within 3 days: Remove Disease (CL 12+)
// After 3 days: Remove Curse (CL 12+) or Heal spell
```

#### C.3: PC Transformation

If a PC contracts lycanthropy:
- After incubation, involuntary shift during full moon
- Must make Control Shape checks
- Can eventually learn to control transformations
- Becomes natural lycanthrope after 3 full moons

**Recommendation:** Only implement when the game has a disease/curse resolution system with meaningful consequences.

### Phase D: Alignment Integration (Effort: ~30 min)

Add alignment to factory-created lycanthropes:

```csharp
lycan.CharacterAlignment = CharacterAlignment.ChaoticEvil; // werewolf
lycan.CharacterAlignment = CharacterAlignment.LawfulEvil;  // wererat
lycan.CharacterAlignment = CharacterAlignment.TrueNeutral;  // wereboar
lycan.CharacterAlignment = CharacterAlignment.TrueNeutral;  // weretiger
lycan.CharacterAlignment = CharacterAlignment.LawfulGood;   // werebear
```

This enables Smite Evil/Good, Protection from Evil, etc. to work correctly against lycanthropes.

---

## 8. Effort Estimates

| Phase | Effort | Priority | Unlocks |
|-------|--------|----------|---------|
| **A**: ICreatureTemplate wrapper | ~1 hour | Medium | Runtime application, registry integration |
| **B**: Multi-form system | ~4-6 hours | Low | Form-shifting combat, NPC AI forms |
| **C**: Curse deep integration | ~2-3 hours | Low | PC lycanthropy, disease resolution |
| **D**: Alignment integration | ~30 min | Medium | Smite/Protection targeting |
| **Total to match Celestial/Fiendish** | **~8-10 hours** | | |

### Quick Wins (< 1 hour total):
1. ✅ Phase D: Add alignment to factory creatures (~30 min)
2. ✅ Phase A: Create `ICreatureTemplate` wrapper class (~1 hour)

### Do When Needed:
3. Phase C: Curse resolution — when disease system is expanded
4. Phase B: Multi-form — when PC lycanthropy or NPC form-shifting is needed

---

## 9. Unique Challenges for Lycanthrope Template

### 9.1 Three-Form System

Unlike other templates (which are one-way transformations), lycanthropes have **three forms** that they actively switch between during play:

| Form | Stats | Natural Attacks | Equipment | AI |
|------|-------|----------------|-----------|-----|
| **Humanoid** | Base creature only | None (uses weapons) | Full equipment | Normal |
| **Hybrid** | Merged (what we have) | 2 claws + bite + weapons | Can use weapons | Aggressive |
| **Animal** | Animal stats + template bonuses | Animal attacks only | Cannot use weapons | Animal-like |

This is fundamentally different from Skeleton (one-way conversion), Zombie (one-way conversion), or Celestial/Fiendish (stat overlay).

### 9.2 Template Requires TWO Inputs

The `ICreatureTemplate.ApplyToDefinition(NPCDefinition)` interface takes a single creature definition. But Lycanthrope needs **two inputs**: a base creature AND an animal type. This is a design mismatch that requires either:
- Per-animal template IDs (recommended)
- Extended metadata on the NPCDefinition
- A more complex interface

### 9.3 Disease Transmission Creates New Lycanthropes

Unlike other templates, the Lycanthrope template can **create copies of itself** through the Curse of Lycanthropy. A werewolf bites a PC → PC becomes a werewolf → PC can now bite others. This recursive property doesn't exist in any other template.

### 9.4 Alignment Variation

Each lycanthrope type has its own alignment (CE, LE, N, LG), unlike Skeleton (always NE), Zombie (always NE), or Celestial/Fiendish (always Good/Evil). This means alignment-based systems need to check the specific lycanthrope type.

### 9.5 Natural vs Afflicted is a Spectrum

The natural/afflicted distinction isn't just DR 10 vs 5 — it affects:
- Curse spreading ability
- Transformation control
- Mental stats in animal form
- Ability to have children who are natural lycanthropes
- Whether they can be cured by Remove Disease

This makes the template more of a **character progression system** than a simple stat overlay.

---

## 10. Conclusion

### Current State: Good Foundation, Room for Growth

The Lycanthrope template is a **solid combat encounter system** that accurately implements D&D 3.5e hybrid form stats for 8 variants spanning CR 2-14. It integrates with the disease system for curse transmission and follows the established Skeleton/Zombie factory pattern.

### To Match Celestial/Fiendish Quality:

The gap is **not just about code architecture** — it's about the fundamental complexity difference:
- Celestial/Fiendish is a **stat overlay** (one creature → enhanced creature)
- Lycanthrope is a **creature merger** (two creatures → hybrid creature)

Matching the architectural quality (ICreatureTemplate, registry, runtime application) is ~1-2 hours. But matching the **gameplay depth** (multi-form, curse progression, form-shifting AI) is ~6-8 hours of new system development.

### Recommended Path:

1. **Now:** Add alignment to factory variants (30 min) ← Quick win
2. **Soon:** Create `ICreatureTemplate` wrapper with per-animal-type registration (1 hour) ← Enables runtime use
3. **When needed:** Multi-form system (4-6 hours) ← When game needs form-shifting
4. **When needed:** Deep curse integration (2-3 hours) ← When disease system matures

The Lycanthrope template is **6.2/10 quality** vs Celestial/Fiendish at **8.3/10**. The Quick Wins can bring it to **~7.0/10**; full multi-form support would bring it to **~8.5/10** (potentially higher than Celestial/Fiendish due to the added gameplay depth).

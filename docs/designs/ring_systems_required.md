# Ring Systems — Technical Specifications

> Required new systems and modifications for magic ring implementation.  
> Project: `/home/ubuntu/dnd35prototype`

---

## System 1: Ring Equipment Framework (FOUNDATION — Required First)

**Priority:** CRITICAL  
**Effort:** 2–3 days  
**Files Modified:** ItemData.cs, Inventory.cs, ItemDatabase.cs, RecalculateStats()  
**Files Created:** RingDatabase.cs, RingFactory.cs, RingDefinition.cs

### 1.1 ItemData Additions

```csharp
// New fields on ItemData.cs
public bool IsRing;                    // True if this item is a ring
public string RingId;                  // Unique ring ID (e.g., "ring_of_protection_1")
public int RingBonusValue;             // Numeric bonus (+1 to +5 for protection, etc.)
public string RingEnergyType;          // For energy resistance rings: "acid"/"cold"/"electricity"/"fire"/"sonic"
public RingEffectType RingEffect;      // What type of ring this is
public bool RingIsActive;              // For toggle rings (Force Shield)
public string RingStoredSpellId;       // For Ring of Counterspells
public int RingStoredSpellLevel;       // For Ring of Counterspells
public int RingWizardryLevel;          // 1-4 for Ring of Wizardry
```

### 1.2 RingEffectType Enum

```csharp
public enum RingEffectType
{
    None,
    Protection,          // Deflection bonus to AC
    EnergyResistance,    // Resist energy damage
    Evasion,             // Grant Evasion ability
    FreedomOfMovement,   // Continuous FoM
    ForceShield,         // Toggle +2 shield AC
    MindShielding,       // Immunity to mental detection
    Invisibility,        // Command word → Invisibility spell
    Blinking,            // Command word → Blink spell
    Telekinesis,         // Command word → Telekinesis spell
    Regeneration,        // Fast healing 1/round
    Ram,                 // Charged ranged force attack
    AnimalFriendship,    // Command word → Charm Animal
    ChameleonPower,      // +10 Hide + Disguise Self
    FeatherFalling,      // Auto Feather Fall
    Sustenance,          // No food/water needed
    SkillBonus,          // +5/+10 to specific skill
    WaterWalking,        // Walk on water
    XRayVision,          // See through walls
    MeldIntoStone,       // Command word → Meld into Stone
    Wizardry,            // Double arcane spell slots
    Counterspells,       // Store + auto-counter
    SpellStoring,        // Multi-spell storage
    SpellTurning,        // Daily spell reflection
    ShootingStars,       // Multiple daily abilities
    FriendShield,        // Paired Shield Other
    ThreeWishes,         // 3x Wish charges
    DjinniCalling,       // Summon djinni
    ElementalCommand,    // Elemental dominance + spells
}
```

### 1.3 RingDefinition Class

```csharp
public class RingDefinition
{
    public string RingId;               // Unique identifier
    public string Name;                 // Display name
    public string Description;          // Tooltip text
    public int MarketPrice;             // GP value
    public int CasterLevel;             // CL of ring
    public string AuraSchool;           // Aura school
    public string AuraStrength;         // Faint/Moderate/Strong
    public RingEffectType EffectType;   // What the ring does
    public int BonusValue;              // Numeric bonus (if applicable)
    public string EnergyType;           // Energy type (if applicable)
    public int MaxCharges;              // For charged rings (0 = unlimited)
    public bool IsExpendable;           // Becomes nonmagical when charges = 0
    public string SpellId;              // For spell-like rings
    public int SpellCasterLevel;        // CL for spell effect
    public string ActivationType;       // "continuous", "command", "use-activated"
    public RingImplementationStatus Status; // Implementation tracking
}

public enum RingImplementationStatus
{
    NotStarted,
    Stub,           // Registered but effect not implemented
    Partial,        // Some effects work
    Complete        // Fully functional
}
```

### 1.4 Inventory.RecalculateStats() Ring Processing

Add after existing armor/weapon processing:

```csharp
// ── Ring Effects ──
ProcessRingEffects(LeftRingSlot);
ProcessRingEffects(RightRingSlot);

private void ProcessRingEffects(ItemData ring)
{
    if (ring == null || !ring.IsRing) return;
    
    switch (ring.RingEffect)
    {
        case RingEffectType.Protection:
            // Take highest deflection bonus (ring vs spell)
            OwnerStats.DeflectionBonus = Mathf.Max(
                OwnerStats.DeflectionBonus, ring.RingBonusValue);
            break;
            
        case RingEffectType.EnergyResistance:
            // Add energy resistance entry
            AddRingEnergyResistance(ring);
            break;
            
        case RingEffectType.Evasion:
            OwnerStats.HasEvasion = true;
            break;
            
        case RingEffectType.ForceShield:
            if (ring.RingIsActive)
                OwnerStats.ShieldBonus = Mathf.Max(OwnerStats.ShieldBonus, 2);
            break;
            
        // ... other passive effects
    }
}
```

### 1.5 ItemDatabase.CloneItem() Update

Add ring field copying (same pattern as staff fields):

```csharp
clone.IsRing = src.IsRing;
clone.RingId = src.RingId;
clone.RingBonusValue = src.RingBonusValue;
clone.RingEnergyType = src.RingEnergyType;
clone.RingEffect = src.RingEffect;
clone.RingIsActive = src.RingIsActive;
clone.RingStoredSpellId = src.RingStoredSpellId;
clone.RingStoredSpellLevel = src.RingStoredSpellLevel;
clone.RingWizardryLevel = src.RingWizardryLevel;
```

---

## System 2: Command Word Activation (Required for Tier 2)

**Priority:** HIGH  
**Effort:** 2 days  
**Files Modified:** GameManager.cs (TryUseItem flow), CombatUI.cs  
**Files Created:** RingActivationPanel.cs (or extend QuickItemUsePanel)

### Design

When a player clicks "Use Item" and selects an equipped ring with command word activation:

1. GameManager detects `currentItem.IsRing && ring.ActivationType == "command"`
2. Shows activation UI (like staff spell selection but simpler)
3. Consumes standard action
4. Applies ring spell effect using existing spell resolution

### Integration Points

- `GameManager.TryUseItem()` — add `else if (currentItem.IsRing)` branch
- Reuse existing spell effect application from wand/staff system
- Ring of Invisibility → cast Invisibility at ring's CL
- Ring of Blinking → cast Blink at ring's CL
- Ring of Telekinesis → cast Telekinesis at ring's CL

---

## System 3: Ring Charge Tracking (Required for Charged Rings)

**Priority:** MEDIUM  
**Effort:** 1 day (reuse wand pattern)  
**Files Modified:** ItemData.cs (reuse CurrentCharges/MaxCharges from wand system)

### Design

Ring of Ram and Ring of Three Wishes use charges. This already exists for wands:

```csharp
// Already on ItemData:
public int CurrentCharges;
public int MaxCharges;
```

For rings, add:
- Ring of Ram: MaxCharges = 50, charge cost selection UI (1/2/3)
- Ring of Three Wishes: MaxCharges = 3, each use = 1 charge
- When charges hit 0 and `IsExpendable == true`, convert to nonmagical ring

---

## System 4: Spell Slot Doubling (Required for Ring of Wizardry)

**Priority:** HIGH (high gameplay impact)  
**Effort:** 3 days  
**Files Modified:** SpellSlotCalculator.cs (or wherever spell slots are computed), CharacterStats.cs

### Design

Ring of Wizardry doubles BASE arcane spell slots for a specific level:

```csharp
// In spell slot calculation:
int baseSlots = GetBaseArcaneSpellSlots(spellLevel);
int abilityBonusSlots = GetAbilityBonusSlots(spellLevel);

// Check for Ring of Wizardry
int wizardryLevel = GetEquippedRingOfWizardryLevel(); // 0 if none
if (wizardryLevel == spellLevel && IsArcaneClass)
{
    baseSlots *= 2; // Double base slots only
}

int totalSlots = baseSlots + abilityBonusSlots;
```

### Key Rules
- ONLY arcane casters (Wizard, Sorcerer, Bard)
- ONLY base spell slots doubled (not ability bonus slots)
- Cannot double slots for levels the caster can't cast yet
- If two Rings of Wizardry for same level: doesn't triple (same bonus type)

---

## System 5: Spell Storage (Required for Ring of Spell Storing + Counterspells)

**Priority:** MEDIUM  
**Effort:** 4–5 days  
**Files Created:** SpellStorageData.cs, SpellStorageUI.cs  
**Files Modified:** ItemData.cs, GameManager.cs

### Design

```csharp
public class StoredSpell
{
    public string SpellId;
    public int SpellLevel;
    public int CasterLevel;
    public int SaveDC;
    public string CasterName;
}

// On ItemData:
public List<StoredSpell> StoredSpells;  // For Ring of Spell Storing
public int SpellStorageCapacity;         // 3/5/10 total spell levels
```

### Storage Flow
1. Caster casts a spell "into" the ring (uses spell slot, no target)
2. Spell stored with original caster's CL, DC, etc.
3. Total spell levels cannot exceed capacity

### Casting Flow
1. Ring wearer activates ring (standard action)
2. UI shows stored spells
3. Select spell → cast at original CL/DC
4. Spell removed from storage

### Counterspell Flow (Ring of Counterspells)
1. Only stores ONE spell (level 1–6)
2. When that exact spell targets wearer → auto-counter
3. No action required from wearer
4. Stored spell consumed
5. Can reload

---

## System 6: Daily Use Tracking (Required for Shooting Stars, Spell Turning)

**Priority:** LOW-MEDIUM  
**Effort:** 2 days  
**Files Modified:** ItemData.cs, GameManager round tracking

### Design

```csharp
// On ItemData:
public Dictionary<string, int> DailyUsesRemaining; // ability name → uses left today
public Dictionary<string, int> WeeklyUsesRemaining; // ability name → uses left this week

// Reset on long rest:
public void ResetDailyUses() { ... }
public void ResetWeeklyUses() { ... }
```

### Ring of Shooting Stars Daily Abilities
```
"dancing_lights": 1/hour (track separately)
"light": 2/night
"ball_lightning": 1/night
"shooting_stars": 3/week
"faerie_fire": 2/day
"spark_shower": 1/day
```

---

## System 7: Elemental Dominance (Required for Ring of Elemental Command)

**Priority:** LOW (Tier 4)  
**Effort:** 5–7 days  
**Files Created:** ElementalDominanceSystem.cs, ElementalCommandRing.cs

### Design

Complex system with:
1. **Activation tracking** — ring starts as lesser form, requires killing elemental
2. **Elemental type detection** — identify creatures by elemental subtype
3. **Passive bonuses** — attack/save bonuses vs elementals
4. **Spell-like abilities** — 6 per ring variant, varied daily limits
5. **Weakness** — save penalty vs opposing element

### Data Structure
```csharp
public class ElementalCommandRingState
{
    public bool IsFullyActivated;
    public string ElementType; // "air", "earth", "fire", "water"
    public Dictionary<string, int> AbilityUsesRemaining;
    // Tracks: gust_of_wind: 2/day, wind_wall: unlimited, etc.
}
```

---

## Summary: New Systems by Priority

| # | System | Effort | Tier Unlocks | Priority |
|---|--------|--------|-------------|----------|
| 1 | Ring Equipment Framework | 2–3 days | ALL rings | **CRITICAL** |
| 2 | Command Word Activation | 2 days | Tier 2 (9 rings) | **HIGH** |
| 3 | Ring Charge Tracking | 1 day | Ram, Three Wishes | **HIGH** |
| 4 | Spell Slot Doubling | 3 days | Wizardry I–IV | **HIGH** |
| 5 | Spell Storage | 4–5 days | Spell Storing, Counterspells | MEDIUM |
| 6 | Daily Use Tracking | 2 days | Shooting Stars, Spell Turning | MEDIUM |
| 7 | Elemental Dominance | 5–7 days | Elemental Command ×4 | LOW |

**Total new system effort:** ~19–23 days

---

## File Impact Map

### New Files (8)
```
Assets/Scripts/Equipment/RingDefinition.cs         — Ring data structure
Assets/Scripts/Equipment/RingDatabase.cs            — Ring registry (all 33 types)
Assets/Scripts/Equipment/RingFactory.cs             — Ring item generation
Assets/Scripts/Equipment/RingEffectType.cs          — Ring effect enum
Assets/Scripts/Equipment/SpellStorageData.cs        — Spell storage for rings
Assets/Scripts/UI/Panels/RingActivationPanel.cs     — Ring use UI
Assets/Scripts/UI/Panels/SpellStorageUI.cs          — Spell store/retrieve UI
Assets/Scripts/Systems/ElementalDominanceSystem.cs  — Elemental command framework
```

### Modified Files (10+)
```
Assets/Scripts/Inventory/ItemData.cs                — Ring fields
Assets/Scripts/Inventory/Inventory.cs               — RecalculateStats() ring processing
Assets/Scripts/Inventory/ItemDatabase.cs             — CloneItem() ring fields
Assets/Scripts/Core/GameManager.cs                  — TryUseItem() ring branch
Assets/Scripts/Core/SceneBootstrap.cs               — RingDatabase.Init()
Assets/Scripts/Character/CharacterStats.cs          — Ring-derived stats
Assets/Scripts/Identifiers/ItemIDs.cs               — Ring item ID constants
Assets/Scripts/UI/PreCombatInventoryUI.cs            — Ring tooltip updates
Assets/Scripts/UI/InventoryUI.cs                    — Ring display
Assets/Scripts/Magic/SpellSlotCalculator.cs         — Wizardry doubling
```

---

*Technical specifications generated 2026-05-24.*

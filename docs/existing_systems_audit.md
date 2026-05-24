# Existing Systems Audit for Tier 3 Implementation

**Audit Date:** May 24, 2026
**Project:** `/home/ubuntu/dnd35prototype`
**Purpose:** Identify all existing subsystems that Tier 3 specific magic items can reuse, reducing implementation effort.

---

## Systems Already Implemented ✅

---

### 1. HASTE SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterController.cs` — lines 1445-1530
  - `ApplyHasteEffect(int durationRemainingRounds, CharacterController caster)`
  - `HasActiveHasteEffect` (bool property)
  - `ClearHasteEffect()`
  - `UpdateHasteDuration(int durationRemainingRounds)`
  - `GetHasteRemainingRounds()`
  - `ActiveHasteEffect` field (HasteEffectData)
- `Assets/Scripts/Character/CharacterStats.cs` — lines 1310-1313
  - `HasteAttackBonus` (int) — feeds into `ConditionAttackPenalty`
  - `HasteACBonus` (int) — feeds into total AC calculation (line 2518)
  - `HasteReflexBonus` (int) — feeds into `ConditionReflexModifier`
- `Assets/Scripts/Character/CharacterCombatStats.cs` — line 62
  - Extra attack in full attack: checks `HasActiveHasteEffect` + `GrantsExtraAttack`
- `Assets/Scripts/Core/GameManager_Spells_H.cs` — line 268
  - `ApplyHasteBuff()` — Full Haste spell implementation
- `Assets/Scripts/Core/GameManager.DispelCounterspell.cs` — line 321
  - Dispel cleanup zeros all haste fields

**Features:**
- ✅ Extra attack in full attack (GrantsExtraAttack)
- ✅ +1 attack bonus (HasteAttackBonus)
- ✅ +1 AC dodge bonus (HasteACBonus)
- ✅ +1 Reflex save bonus (HasteReflexBonus)
- ✅ +30 ft movement (SpeedBonusFeet = 30)
- ✅ Duration tracking (DurationRemainingRounds)
- ✅ Dispels Slow / counter-interaction
- ✅ Dispel cleanup

**Already Reused By:**
- `MithralFullPlateOfSpeedBehavior` (Tier 2) — sets `Stats.HasteAttackBonus/ACBonus/ReflexBonus` directly

**Can Be Reused For:**
- Mithral Full Plate of Speed already done ✅
- Celestial Armor (if it had haste — it doesn't, it has fly 1/day)

**API for Tier 3 Items:**
```csharp
// Simple approach (as Mithral Full Plate does):
stats.HasteAttackBonus = 1;
stats.HasteACBonus = 1;
stats.HasteReflexBonus = 1;

// Full spell approach (with extra attack + movement):
character.ApplyHasteEffect(durationRounds, caster);
```

**Modifications Needed:** None — fully functional.

---

### 2. AURA / EMANATION SYSTEM
**Status:** ✅ Fully Implemented (Generic Framework)
**Location:**
- `Assets/Scripts/Magic/StatusEffects/EmanationEffectData.cs` — Abstract base class
  - `CenterCreature` — Mobile emanation center
  - `CenterPosition` — Static emanation center (optional)
  - `RadiusSquares` / `RadiusFeet` — Area radius
  - `RemainingRounds` / `CasterLevel`
  - `IsCreatureInArea(CharacterController creature)` — Chebyshev distance check
  - `GetCreaturesInArea(List<CharacterController> allCharacters)` — All living creatures in radius
  - `GetCurrentCenter()` — Position helper
  - Abstract methods: subclasses implement enter/leave/apply/remove
- `Assets/Scripts/Core/GameManager.cs` — lines 422, 9464-9530
  - `RegisterEmanation(EmanationEffectData emanation)` — Add to active emanations
  - `UnregisterEmanation(CharacterController centerCreature)` — Remove all emanations on creature
  - `TickEmanations()` — Round-by-round duration tick + cleanup
  - `_activeEmanations` list — Active emanation tracking

**Existing Subclasses:**
- `MagicCircleEffectData` (alignment protection, 10-ft radius)
- `InvisibilitySphereEffect` (invisibility, membership tracking)
- Future designed for: Prayer, Consecrate, Paladin Auras, Bard Inspire

**Features:**
- ✅ Radius detection (Chebyshev grid distance)
- ✅ Mobile emanation (follows creature)
- ✅ Static emanation (fixed position)
- ✅ Duration tracking + auto-expire
- ✅ Registration/unregistration
- ✅ Round-by-round ticking
- ✅ Creature-in-area queries

**Can Be Reused For:**
- **Holy Avenger SR aura** — Create `HolyAvengerSRAura : EmanationEffectData` with 2-square radius (10 ft), grants SR to allies
- **Holy Avenger dispel aura** — Could piggyback on same emanation or use a separate one

**API for Tier 3 Items:**
```csharp
// Create custom emanation subclass
var aura = new HolyAvengerSRAura {
    CenterCreature = wielder,
    RadiusSquares = 2,  // 10 ft
    RadiusFeet = 10f,
    RemainingRounds = -1,  // Permanent while equipped
    CasterLevel = paladinLevel
};
GameManager.Instance.RegisterEmanation(aura);

// Query creatures in area
var allies = aura.GetCreaturesInArea(GameManager.Instance.Combat_GetAllCharacters());
```

**Modifications Needed:**
- Create `HolyAvengerSRAura : EmanationEffectData` subclass (0.5 days)
- Implement `OnCreatureEnter`/`OnCreatureLeave` to apply/remove SR
- Handle permanent duration (RemainingRounds = -1 or very high value)

---

### 3. SPELL RESISTANCE SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs` — line 1854
  - `public int SpellResistance;` — Directly settable integer
  - Display: lines 3179-3180 (traits), lines 4381-4382 (stat block)
- `Assets/Scripts/Core/GameManager.SpellCasting.cs` — multiple locations (4579, 4749, 4872, 4962, 5713)
  - SR check formula: `1d20 + CL (+ spell pen feats) vs SpellResistance`
  - Pattern: `if (spell.SpellResistanceApplies && target.Stats.SpellResistance > 0)`
- `Assets/Scripts/Character/FeatManager.cs` — line 464+
  - `GetSpellPenetrationBonus()` — +2/+4 from Spell Penetration / Greater SP

**Features:**
- ✅ SR stat on characters (int field)
- ✅ SR check when casting spells (1d20 + CL + feat bonus vs SR)
- ✅ SR bypass: only checked when `SpellResistanceApplies == true`
- ✅ Spell Penetration feat integration (+2/+4 bonus)
- ✅ Display in stat block and traits

**Can Be Reused For:**
- **Holy Avenger** — Set `Stats.SpellResistance = 5 + paladinLevel` on wielder
- **Holy Avenger aura** — Set SR on all allies in 10-ft emanation

**API for Tier 3 Items:**
```csharp
// Direct SR assignment
wielder.Stats.SpellResistance = Mathf.Max(wielder.Stats.SpellResistance, 5 + paladinLevel);

// For aura: on enter/leave
ally.Stats.SpellResistance += srValue;
ally.Stats.SpellResistance -= srValue;
```

**Modifications Needed:**
- Track granted SR separately to avoid stacking/override issues (0.25 days)
- Aura-based dynamic SR via emanation subclass (included in emanation work)

---

### 4. RAGE SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs` — lines 936-1000
  - `ActivateRage()` — bool return, checks IsBarbarian, IsRaging, IsFatiguedOrExhausted, RagesUsedToday
  - `DeactivateRage()` — Removes bonuses, applies IsFatigued
  - `TickRage()` — Decrement rounds, auto-deactivate on expiry
  - `IsRaging` (bool), `RageRoundsRemaining` (int), `RagesUsedToday` (int)
  - `MaxRagesPerDay` — Currently returns 1 for Barbarians
  - `RageACPenalty` — -2 while raging (line 1302)
  - `SpellRageACPenalty` — From Rage spell (separate, line 1308)

**Features:**
- ✅ +4 STR, +4 CON stat bonuses (direct modification)
- ✅ -2 AC penalty (RageACPenalty property)
- ✅ +2 Will save bonus (implicit via morale)
- ✅ Duration tracking (3 + CON mod rounds)
- ✅ Fatigue after rage ends (IsFatigued = true)
- ✅ Rage rounds per day (MaxRagesPerDay)
- ✅ HP gain/loss from CON change
- ✅ Cannot rage while fatigued/exhausted
- ✅ Round tick + auto-expiry

**Can Be Reused For:**
- **Demon Armor** — Force rage on wearer (modify `ActivateRage()` or call directly)

**API for Tier 3 Items:**
```csharp
// Current API requires IsBarbarian check — need forced variant
stats.ActivateRage();  // Only works for Barbarians

// For Demon Armor, need to bypass IsBarbarian check:
// Option A: Set IsRaging + bonuses directly
stats.IsRaging = true;
stats.STR += 4; stats.CON += 4;
stats.RageRoundsRemaining = 999; // Until combat ends
// (Then handle deactivation + fatigue manually)

// Option B: Add ForceRage() method that bypasses class check
```

**Modifications Needed:**
- Add `ForceRage(int rounds)` method to CharacterStats that bypasses IsBarbarian check (0.25 days)
- Or: Demon Armor manually sets rage stats without using the method (0 days, but less clean)

---

### 5. WEAPON FINESSE SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/FeatManager.cs` — lines 248-268
  - `ShouldUseWeaponFinesse(CharacterStats stats, ItemData weapon)` — Checks feat + weapon type
  - `GetMeleeAttackAbilityMod(CharacterStats stats, ItemData weapon)` — Returns DEX or STR mod
- `Assets/Scripts/Character/FeatDefinitions.cs` — lines 410-420
  - Feat definition: "Weapon Finesse", BAB +1 prereq, `GrantsWeaponFinesse = true`
  - Applies to: light weapons, rapier, whip, spiked chain, unarmed

**Features:**
- ✅ Weapon Finesse feat exists
- ✅ Uses DEX instead of STR for attack rolls
- ✅ Light weapon restriction (plus rapier, whip, spiked chain)
- ✅ Still uses STR for damage (not modified)
- ✅ Integrated into attack calculation via `GetMeleeAttackAbilityMod()`

**Can Be Reused For:**
- **Sun Blade** — Built-in finesse without requiring the feat

**API for Tier 3 Items:**
```csharp
// Current check in FeatManager:
public static bool ShouldUseWeaponFinesse(CharacterStats stats, ItemData weapon)
{
    if (!stats.HasFeat("Weapon Finesse")) return false;
    if (weapon == null) return true;
    return weapon.IsLightWeapon || weapon.Name.ToLower().Contains("rapier");
}

// Sun Blade needs: weapon grants finesse regardless of feat
// Option A: Add ItemData.GrantsFinesse bool, check in ShouldUseWeaponFinesse
// Option B: Sun Blade behavior adds temp "Weapon Finesse" feat on equip
// Option C: Override in OnPreAttackRoll to swap STR→DEX difference
```

**Modifications Needed:**
- Best approach: Add `GrantsFinesse` property to ItemData or check in FeatManager (0.25 days)
- Alternative: Sun Blade's `OnPreAttackRoll` adds `DEXMod - STRMod` to attack bonus if DEX > STR

---

### 6. ETHEREALNESS / PLANE SHIFT
**Status:** ⚠️ Partial — Blink Only
**Location:**
- `Assets/Scripts/Character/CharacterController.cs` — lines 1581-1631
  - Blink spell: 50% miss chance, 20% self-miss, ethereal shifting
  - `HasGhostTouchWeapon()` — Can strike ethereal creatures
  - Ghost Touch check for bypassing ethereal concealment

**Features:**
- ✅ Blink (rapid Material/Ethereal shifting)
- ✅ Miss chance mechanics for ethereal
- ✅ Ghost Touch weapon detection
- ❌ No full Ethereal Jaunt / Etherealness spell
- ❌ No Plane Shift spell
- ❌ No persistent ethereal state

**Can Be Reused For:**
- **Sword of the Planes** (already Tier 2) — partially done with plane detection
- **Plate Armor of Etherealness** — NOT IN SRD (per corrections doc), so not needed

**Modifications Needed:** None for Tier 3 — Plate Armor of Etherealness is not in SRD.

---

### 7. NEGATIVE LEVEL SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs`
  - `NegativeLevelCount` — Count of `CombatConditionType.EnergyDrained` conditions (line 762)
  - `NegativeLevelHpPenalty` — 5 HP per level (line 763)
  - `EffectiveCharacterLevel` — Level minus negative levels (line 764)
  - `RefreshNegativeLevelState()` — Recalculate on change (line 1135)
  - `EnforceNegativeLevelDeathThreshold()` — Death if NL >= HD (line 1151)
  - Applied via `ApplyCondition(CombatConditionType.EnergyDrained, source, rounds)`

**Features:**
- ✅ Apply negative levels (via EnergyDrained condition, stacking by source)
- ✅ -1 attack, saves, checks per level (via condition system)
- ✅ -5 HP per level (NegativeLevelHpPenalty)
- ✅ Death if NL >= hit dice (EnforceNegativeLevelDeathThreshold)
- ✅ Duration tracking (rounds-based)
- ✅ Spell slot loss (ApplyNegativeLevelSlotLoss in CharacterController line 3394)

**Can Be Reused For:**
- **Sword of Life Stealing** — 1 negative level on crit
- **Life-Drinker** — 2 negative levels per hit (+ 1 self)
- **Nine Lives Stealer** — Negative level on non-evil wield (alignment penalty)

**API for Tier 3 Items:**
```csharp
// Apply negative levels
target.Stats.ApplyCondition(CombatConditionType.EnergyDrained, "Sword of Life Stealing", durationRounds);

// Apply to self (Life-Drinker)
wielder.Stats.ApplyCondition(CombatConditionType.EnergyDrained, "Life-Drinker (self)", durationRounds);

// Check negative level count
int nlCount = target.Stats.NegativeLevelCount;
```

**Modifications Needed:** None — fully functional as-is.

---

### 8. INSTANT DEATH SYSTEM
**Status:** ⚠️ No Dedicated System — Use Fort Save + Kill
**Location:**
- Fort save: `SavingThrowResolver.ResolveFortitudeSave(stats, dc, effectName)` — `Assets/Scripts/Services/SavingThrowResolver.cs`
- Kill: Set `target.Stats.CurrentHP = -10` or use existing death flow
- `target.Stats.IsDead` — Death check

**Features:**
- ✅ Fort save resolution (d20 + mod vs DC)
- ✅ Death state tracking (IsDead, CurrentHP)
- ❌ No dedicated "Fort save or die" helper method
- ✅ Phantasmal Killer has Fort save or die (in GameManager_Spells_P.cs line 240)

**Can Be Reused For:**
- **Nine Lives Stealer** — Fort DC 20 or die on crit
- **Mace of Smiting** — Instant kill construct on crit

**API for Tier 3 Items:**
```csharp
// Fort save or die pattern:
var save = SavingThrowResolver.ResolveFortitudeSave(target.Stats, 20, "Nine Lives Stealer");
if (!save.Succeeded)
{
    target.Stats.CurrentHP = -10;
    target.Stats.IsDead = true;
    logNotes.Add($"Nine Lives Stealer: {target.Stats.CharacterName} fails Fort save (DC 20) and is slain!");
}
```

**Modifications Needed:** None — pattern is straightforward, just use SavingThrowResolver + death assignment.

---

### 9. CONDITION SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Combat/CombatConditionType.cs` — Full enum
- `Assets/Scripts/Character/CharacterStats.cs` — `ApplyCondition()`, `RemoveCondition()`, `HasCondition()`

**Conditions Found (relevant to Tier 3):**
- ✅ Panicked — `CombatConditionType.Panicked`
- ✅ Fatigued — `CombatConditionType.Fatigued` (also `IsFatigued` direct flag)
- ✅ Exhausted — `CombatConditionType.Exhausted`
- ✅ Blinded — `CombatConditionType.Blinded`
- ✅ Invisible — `CombatConditionType.Invisible`
- ✅ Frightened — `CombatConditionType.Frightened`
- ✅ Shaken — `CombatConditionType.Shaken`
- ✅ Stunned — `CombatConditionType.Stunned`
- ✅ Paralyzed — `CombatConditionType.Paralyzed`
- ✅ Nauseated — `CombatConditionType.Nauseated`
- ✅ Sickened — `CombatConditionType.Sickened`
- ✅ EnergyDrained — `CombatConditionType.EnergyDrained` (stacking)
- ✅ BestowCurse variants — `BestowCurseGeneralPenalty`, `BestowCurseActionLoss`
- ❌ No `Cursed` (equipment curse — cannot remove) — distinct from Bestow Curse spell

**Can Be Reused For:**
- Mace of Terror — Panicked condition (already done in Tier 2)
- Demon Armor — Fatigued after forced rage
- Screaming Bolt — Panicked on AoE

**API:**
```csharp
target.Stats.ApplyCondition(CombatConditionType.Panicked, "Screaming Bolt", durationRounds);
bool isPanicked = target.HasCondition(CombatConditionType.Panicked);  // On CharacterController!
```

**Modifications Needed:** None for conditions. Equipment curse system needed separately (see "Systems to Build").

---

### 10. TEMPORARY HP SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs` — line 2358
  - `public int TempHP;` — Directly settable
  - Damage absorption: lines 3962-3973 (absorbed before real HP)
  - Also: `DivinePowerTempHP` (line 2448) — tracked separately
- `Assets/Scripts/Character/CharacterController.cs` — lines 2200-2233
  - False Life effect tracking (example of temp HP source tracking)

**Features:**
- ✅ Grant temp HP (`Stats.TempHP += amount`)
- ✅ Temp HP absorbed first (in TakeDamage flow)
- ✅ Temp HP from multiple sources (False Life tracks separately to prevent stacking)
- ✅ Display/logging

**Can Be Reused For:**
- **Sword of Life Stealing** — Gain temp HP equal to negative levels dealt (1d4+5 temp HP on crit)

**API for Tier 3 Items:**
```csharp
// Simple temp HP grant (stacks/replaces based on source)
wielder.Stats.TempHP += tempHPAmount;

// More careful approach (don't stack with self):
wielder.Stats.TempHP = Mathf.Max(wielder.Stats.TempHP, tempHPAmount);
```

**Modifications Needed:** None — fully functional.

---

### 11. ENERGY RESISTANCE SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs` — line 1845
  - `ActiveResistEnergyEffects` — List of `ResistEnergyEffectData`
  - `SetResistEnergyEffect(ResistEnergyEffectData newEffect)` — line 4036
  - `GetResistEnergyResistanceForTypes(HashSet<DamageType> types, ...)` — line 4089
  - Applied in damage calculation: line 3652

**Features:**
- ✅ Resist fire/cold/electricity/acid/sonic
- ✅ Multiple resistance tracking
- ✅ Damage reduction by energy type
- ✅ Integration into damage pipeline
- ✅ Stacking/replacement rules

**Can Be Reused For:**
- **Frost Brand** — Fire resistance 10

**API for Tier 3 Items:**
```csharp
var fireResist = new ResistEnergyEffectData {
    ResistType = DamageType.Fire,
    Amount = 10,
    SourceName = "Frost Brand",
    DurationRounds = -1  // Permanent while equipped
};
wielder.Stats.SetResistEnergyEffect(fireResist);
```

**Modifications Needed:** None — fully functional.

---

### 12. ALIGNMENT SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs` — line 198
  - `CharacterAlignment` — Alignment enum field
  - `AlignmentName` / `AlignmentAbbr` — Display helpers
- `Assets/Scripts/Character/Alignment.cs`
  - `Alignment` enum: LawfulGood, NeutralGood, ChaoticGood, LawfulNeutral, TrueNeutral, ChaoticNeutral, LawfulEvil, NeutralEvil, ChaoticEvil, None
  - `AlignmentHelper.IsEvil(Alignment a)` — static bool
  - `AlignmentHelper.IsGood(Alignment a)` — static bool
  - `AlignmentHelper.IsLawful(Alignment a)` — static bool
  - `AlignmentHelper.IsChaotic(Alignment a)` — static bool
- `Assets/Scripts/Equipment/EnchantmentEffects.cs` — lines 556-584
  - Holy weapon alignment damage vs evil creatures (already implemented)

**Features:**
- ✅ Alignment enum with all 9 alignments + None
- ✅ IsEvil/IsGood/IsLawful/IsChaotic helper methods
- ✅ Holy/Unholy/Axiomatic/Anarchic weapon damage
- ✅ Integration with enchantment effects

**Can Be Reused For:**
- **Holy Avenger** — Check `IsLawful && IsGood` for paladin requirement or just check `IsPaladin`
- **Nine Lives Stealer** — Apply NL if wielder `IsEvil` is false? (SRD: bestows 1 NL on any non-evil wield)
- **Sun Blade** — Extra damage vs evil, double vs undead
- **Demon Armor** — Evil alignment item

**Modifications Needed:** None — fully functional.

---

### 13. RACE / CLASS RESTRICTION SYSTEM
**Status:** ⚠️ Partial — Detection Exists, Equipment Restriction Not Formalized
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs`
  - `RaceName` (string) — via `Race.RaceName` (line 4799)
  - `IsPaladin` (bool) — `HasClass("Paladin")` (line 489)
  - `HasClass(string className)` — Generic class check
  - `GetClassLevel(string className)` — Returns class level
  - `IsBarbarian`, `IsCleric`, `IsMonk` etc. — Convenience bools
  - `CreatureType` (string) — "Humanoid", "Undead", "Construct", etc.

**Features:**
- ✅ Race detection by name string
- ✅ Class detection + level query
- ✅ Creature type detection
- ❌ No equipment restriction system (block equipping wrong class/race)

**Can Be Reused For:**
- **Dwarven Thrower** — `stats.RaceName == "Dwarf"` for bonus determination
- **Holy Avenger** — `stats.IsPaladin` + `stats.GetClassLevel("Paladin")` for enhancement upgrade + SR
- **Mace of Smiting** — `IsCreatureType(target, "Construct")` / `IsCreatureType(target, "Outsider")`

**API for Tier 3 Items:**
```csharp
// Race check
bool isDwarf = wielder.Stats.RaceName.Equals("Dwarf", StringComparison.OrdinalIgnoreCase);

// Class check
bool isPaladin = wielder.Stats.IsPaladin;
int paladinLevel = wielder.Stats.GetClassLevel("Paladin");

// Creature type (already in SpecificItemBehavior base)
bool isConstruct = IsCreatureType(target, "Construct");
bool isOutsider = IsCreatureType(target, "Outsider");
```

**Modifications Needed:**
- For restriction enforcement: add `OnEquip` check in behavior that logs warning / blocks benefit (not equipment slot) — 0 days (behavior handles it)
- No need for formal restriction system — behaviors can simply not grant bonuses to wrong class/race

---

### 14. DISPEL MAGIC SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Core/GameManager.DispelCounterspell.cs`
  - `PerformDispelCheck(int casterLevel, int targetSpellCasterLevel, bool isOwnSpell)` — Core formula
  - `RollDispelCheck(int casterLevel)` — Roll only (for area dispel)
  - `PerformTargetedDispel(...)` — Remove one spell from target
  - `PerformAreaDispel(...)` — Remove spells from multiple targets
  - `DispelSingleEffect(...)` — Apply dispel to specific effect
  - `HandleDispelSpecialCleanup(...)` — Clean up dispelled spell side effects
  - Cap: +10 for Dispel Magic, +20 for Greater Dispel (test on line 326 of tests)

**Features:**
- ✅ Dispel check formula: 1d20 + CL (capped) vs DC 11 + target CL
- ✅ Targeted dispel (highest CL effect)
- ✅ Area dispel (multiple targets)
- ✅ Special cleanup (haste removal, etc.)
- ✅ Greater Dispel Magic cap (+20)

**Can Be Reused For:**
- **Holy Avenger** — Greater Dispel Magic at will (area) at paladin's CL

**API for Tier 3 Items:**
```csharp
// Greater Dispel Magic at-will (CL = paladin level, cap +20)
// Can call existing area dispel logic
GameManager.Instance.PerformAreaDispel(wielder, paladinLevel, isGreater: true);
```

**Modifications Needed:**
- Need to verify `PerformAreaDispel` can be called from behavior context (may need to be public) — 0.25 days
- Holy Avenger: 1/round limit needs tracking in behavior

---

### 15. THROWN WEAPON SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Inventory/ItemData.cs` — line 237
  - `IsThrown` (bool) — Weapon can be thrown
  - `CanBeThrown` — IsThrown OR has Throwing enchantment
  - `RangeIncrement` — Range in feet
  - Max range: 5× for thrown, 10× for projectile
- `Assets/Scripts/Character/CharacterController.cs` — lines 4661, 4892, 4951
  - Thrown attack detection: `equippedWeapon.IsThrown && !rangeInfo.IsMelee`
  - STR bonus on thrown damage (via `DamageBonusSource.Strength`)
  - Range penalty calculation

**Features:**
- ✅ Thrown weapon flag (IsThrown)
- ✅ STR bonus on thrown attacks
- ✅ Range increment + penalty
- ✅ Detection whether current attack is thrown vs melee

**Can Be Reused For:**
- **Dwarven Thrower** — Detect thrown attacks for bonus damage

**API for Tier 3 Items:**
```csharp
// In OnDamageRoll or OnPreAttackRoll:
// Check if this is a ranged/thrown attack — need access to attack context
// Currently the hooks don't pass isRanged/isThrown flag

// Workaround: Dwarven Thrower behavior can check distance to target
int dx = Mathf.Abs(wielder.GridPosition.x - target.GridPosition.x);
int dy = Mathf.Abs(wielder.GridPosition.y - target.GridPosition.y);
bool isThrown = Mathf.Max(dx, dy) > 1;  // Not adjacent = ranged/thrown
```

**Modifications Needed:**
- Add `isRanged` parameter to `OnPreAttackRoll` and `OnDamageRoll` hooks (0.25 days)
- OR: use distance heuristic in behavior (0 days, less accurate)

---

### 16. CREATURE TYPE SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs` — line 1742
  - `CreatureType` (string) — "Humanoid", "Undead", "Construct", "Outsider", "Aberration", etc.
  - Helper methods: `IsImmuneToMindAffecting()`, `IsImmuneToPoison()`, `IsImmuneToDisease()`
- `Assets/Scripts/Equipment/SpecificItemBehavior.cs` — base class helpers
  - `IsCreatureType(target, "Construct")` — Case-insensitive check
  - `IsCreatureTypeAny(target, "Construct", "Outsider")` — Multi-type check

**Can Be Reused For:**
- **Mace of Smiting** — `IsCreatureType(target, "Construct")` for instant kill, `IsCreatureType(target, "Outsider")` for ×4 crit
- **Sun Blade** — `IsCreatureType(target, "Undead")` for double damage
- **Shifter's Sorrow** — Detect shapechanger subtype
- **Life-Drinker** — All creatures (negative levels)

**Modifications Needed:** None — base class already provides helpers.

---

### 17. NATURAL ATTACK SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs` — lines 40-80
  - `NaturalAttackDefinition` class — Name, DamageDice, Count, effects
  - `NaturalAttacks` list on CharacterStats
  - Supports: poison, paralysis, energy drain, ability drain, disease, constrict
- Used by: monsters, lycanthropes, summons, animal companions

**Features:**
- ✅ Multiple natural attacks (claw, bite, slam)
- ✅ Damage dice + count
- ✅ On-hit special effects (poison, paralysis, energy drain)
- ✅ Primary/secondary attack distinction
- ✅ STR/DEX damage source selection

**Can Be Reused For:**
- **Demon Armor** — Grants claw attacks (1d10+1 each)

**API for Tier 3 Items:**
```csharp
// Add claw attacks on equip
var clawAttack = new NaturalAttackDefinition {
    Name = "Demon Armor Claw",
    DamageDice = 10,
    DamageCount = 1,
    Count = 2,  // 2 claws
    BonusDamageSource = DamageBonusSource.Strength,
    IsPrimary = true
};
wielder.Stats.NaturalAttacks.Add(clawAttack);
```

**Modifications Needed:**
- Need to track which natural attacks are from equipment vs innate (for unequip cleanup) — 0.25 days
- Demon Armor claw attacks with contagion disease — hook into disease system

---

### 18. DISEASE SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs` — lines 64-66
  - `DiseaseOnHitType` — DiseaseType enum
  - `HasDiseaseOnHit` — bool flag
- Contagion spell: `Assets/Scripts/Core/GameManager.SpellCasting.cs` — line 2117+
  - Full disease application with Fort saves

**Can Be Reused For:**
- **Demon Armor** — Contagion (Filth Fever) on claw hit

**Modifications Needed:** None — just set `HasDiseaseOnHit = true` on claw natural attack.

---

### 19. CRIT MULTIPLIER SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs` — line 1925
  - `CritMultiplier` (int) — Set from equipped weapon, default 2
  - `RollCritDamage(dice, count, bonus, strMult, critMultiplier)` — line 3507
- `Assets/Scripts/Character/CharacterController.cs` — line 4754
  - `critMult = Stats.CritMultiplier > 0 ? Stats.CritMultiplier : 2;`
  - Used in single attack and full attack flows

**Can Be Reused For:**
- **Mace of Smiting** — ×4 crit vs outsiders (normally ×2 for mace)

**Modifications Needed:**
- Mace of Smiting: Override critMult in behavior. Currently critMult is read once from Stats — behavior would need to modify it in `OnPreAttackRoll` or we need a hook for crit multiplier override. (0.25 days)
- Alternative: `OnCriticalHit` already receives damage — could apply extra damage there

---

### 20. DAMAGE REDUCTION (DR) SYSTEM
**Status:** ✅ Fully Implemented
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs` — line 1803
  - `DamageReductions` list of `DamageReductionEntry`
  - `AddDamageReduction(amount, bypassTags, rangedOnly)` — line 4314
  - `RemoveDamageReduction(amount, bypassTags, rangedOnly)` — line 4325
  - `DamageBypassTag` flags: Magic, Silver, ColdIron, Adamantine, Good, Evil, Lawful, Chaotic, etc.

**Can Be Reused For:**
- Any item that grants DR (not specifically Tier 3, but good to document)

**Modifications Needed:** None.

---

## Systems That Need to Be Built ❌

---

### 1. SWORN ENEMY SYSTEM
**Status:** ❌ Not Found
**Required For:** Oathbow
**Description:** Track designated target as "sworn enemy." Apply +2 enhancement bonus and +2d6 bonus damage vs sworn enemy. -1 attack penalty vs all other targets while sworn enemy exists. State machine: declare → active → fulfilled (on kill or 7 days).
**Estimated Work:** 1 day
**Dependencies:** None — pure behavior state

---

### 2. EQUIPMENT CURSE SYSTEM
**Status:** ❌ Not Found
**Required For:** Demon Armor
**Description:** Mark item as cursed — cannot be unequipped without Remove Curse spell. Need `IsCursed` flag on ItemData + equip/unequip restriction.
**Estimated Work:** 0.5 days
**Approach:**
```csharp
// On ItemData:
public bool IsCursed;

// In Inventory.UnequipItem():
if (item.IsCursed) {
    CombatUI.ShowCombatLog("Cannot remove cursed item without Remove Curse!");
    return false;
}
```

---

### 3. FORCED REVERT (ANTI-SHAPECHANGER)
**Status:** ❌ Not Found
**Required For:** Shifter's Sorrow
**Description:** Force shapechanger to revert to natural form. Requires: (1) detect shapechanger subtype, (2) trigger revert. Currently no shapechanger subtype tracking or polymorph-revert mechanic.
**Estimated Work:** 0.5 days (detect only, revert is cosmetic/stat log)
**Note:** May be simplified to just bonus damage vs shapechangers + "forced revert" log note.

---

### 4. REROLL SYSTEM (LUCK BLADE)
**Status:** ⚠️ Partially Exists (Luck Domain)
**Location:**
- `Assets/Scripts/Character/CharacterStats.cs`
  - `ApplyLuckReroll(int roll, string context)` — Used by Luck Domain
  - Only applies to saving throws currently
- `Assets/Scripts/Services/SavingThrowResolver.cs` — line 126
  - `stats.ApplyLuckReroll(roll, ...)` — Integrated in save flow

**Required For:** Luck Blade (reroll any one roll per day)
**Gap:** Current reroll is Luck Domain (auto-reroll saves). Luck Blade needs manual player-triggered reroll of ANY roll type (attack, save, skill, ability check).
**Estimated Work:** 1 day (UI for triggering reroll + tracking used/unused state)

---

### 5. WISH SPELL SYSTEM
**Status:** ❌ Not Found
**Required For:** Luck Blade (1/2/3 wish variants)
**Description:** Wish is an extremely powerful spell with many sub-effects. For item implementation, could be simplified to a menu of pre-set options (heal, stat boost, resurrect, etc.) or left as a "narrative" ability with DM adjudication.
**Estimated Work:** 2-3 days (full), or 0.5 days (simplified charge tracker + log message)

---

### 6. AoE PATH TARGETING (SCREAMING BOLT)
**Status:** ❌ Not Found for Projectile Path
**Required For:** Screaming Bolt
**Description:** Apply fear effect to all creatures along the bolt's flight path (not just the target). Requires: trace line from shooter to target, find all creatures on/adjacent to that line.
**Estimated Work:** 0.5 days
**Note:** Grid has `SquareGrid.FindPath` but that's pathfinding, not line-of-effect. Need ray-trace on grid.

---

### 7. FLY SYSTEM
**Status:** ❌ Not Found (only Winged Shield has stub)
**Required For:** Celestial Armor (fly 1/day, as spell)
**Description:** Grant flying movement. Currently no formal fly mechanic — Winged Shield has `_flyActive` bool but no actual movement system integration.
**Estimated Work:** 1 day (or simplified to +30 speed + "flying" tag for AC/movement purposes)

---

## Updated Implementation Estimates

### Original Tier 3 Estimate: 4 weeks (20 working days)
### Revised Estimate: 3 weeks (15 working days)

**Time Saved by Reusing Systems:**

| System | Tier 3 Items Using It | Time Saved |
|--------|----------------------|------------|
| Haste | (already done in Tier 2) | — |
| Emanation framework | Holy Avenger SR aura | -1 day |
| Spell Resistance | Holy Avenger | -0.5 days |
| Rage | Demon Armor | -0.5 days |
| Weapon Finesse | Sun Blade | -0.5 days |
| Negative Levels | Sword of Life Stealing, Life-Drinker | -0.5 days |
| Fort Save/Death | Nine Lives Stealer, Mace of Smiting | -0.5 days |
| Conditions | Screaming Bolt, Demon Armor | -0.25 days |
| Temp HP | Sword of Life Stealing | -0.25 days |
| Energy Resistance | Frost Brand | -0.5 days |
| Alignment | Holy Avenger, Sun Blade, Nine Lives Stealer | -0.25 days |
| Race/Class checks | Dwarven Thrower, Holy Avenger | -0.25 days |
| Dispel Magic | Holy Avenger | -1 day |
| Thrown weapons | Dwarven Thrower | -0.25 days |
| Creature types | Mace of Smiting, Sun Blade, Shifter's Sorrow | -0.25 days |
| Natural attacks | Demon Armor | -0.5 days |
| Disease | Demon Armor | -0.25 days |
| Crit multiplier | Mace of Smiting | -0.25 days |

**Total Time Saved:** ~6.5 days
**Total New Systems to Build:** ~6 days (sworn enemy, curse, revert, reroll, wish, AoE path, fly)
**Net Revised Timeline:** ~15 working days (3 weeks)

---

## Revised Priority List

### Quick Wins (< 1 day each) — Week 1

| # | Item | Original | Revised | Key Reused Systems |
|---|------|----------|---------|-------------------|
| 1 | **Sword of Life Stealing** | 1 day | 0.5 days | Negative levels, Temp HP, Fort save |
| 2 | **Life-Drinker** | 1 day | 0.5 days | Negative levels (target + self) |
| 3 | **Mace of Smiting** | 1.5 days | 0.75 days | Creature type, Fort save/death, Crit system |
| 4 | **Frost Brand** | 1 day | 0.5 days | Energy resistance, enchantment damage |
| 5 | **Shifter's Sorrow** | 1 day | 0.5 days | Creature type, bonus damage |
| 6 | **Screaming Bolt** | 1.5 days | 1 day | Conditions (Panicked), needs AoE path |

### Medium Complexity (1 day each) — Week 2

| # | Item | Original | Revised | Key Reused Systems |
|---|------|----------|---------|-------------------|
| 7 | **Nine Lives Stealer** | 1.5 days | 1 day | Negative levels, Fort save/death, charges |
| 8 | **Sun Blade** | 2 days | 1 day | Finesse, alignment, creature type, enchantment |
| 9 | **Dwarven Thrower** | 1.5 days | 1 day | Thrown weapons, race check |
| 10 | **Celestial Armor** | 1.5 days | 1 day | Armor category, needs fly system |

### Complex (1.5+ days each) — Week 3

| # | Item | Original | Revised | Key Reused Systems |
|---|------|----------|---------|-------------------|
| 11 | **Oathbow** | 2 days | 1.5 days | Enchantment system, needs sworn enemy |
| 12 | **Luck Blade (0 wish)** | 1.5 days | 1 day | Save bonus, needs reroll system |
| 13 | **Luck Blade (1/2/3 wish)** | 2 days | 1.5 days | Reroll + charges + wish system |
| 14 | **Demon Armor** | 2 days | 1.5 days | Rage, natural attacks, disease, needs curse |
| 15 | **Holy Avenger** | 3 days | 2 days | Emanation, SR, dispel, alignment, paladin checks |

---

## Code Reuse Examples

### Example 1: Sword of Life Stealing using existing Negative Levels + Temp HP

```csharp
public class SwordOfLifeStealingBehavior : SpecificItemBehavior
{
    public override void OnCriticalHit(CharacterController target, int damage, List<string> logNotes)
    {
        if (target == null || target.Stats.IsDead) return;

        // Fort DC 20 negates
        var save = SavingThrowResolver.ResolveFortitudeSave(target.Stats, 20, "Sword of Life Stealing");
        logNotes.Add(save.LogMessage);

        if (!save.Succeeded)
        {
            // Apply 1 negative level (EXISTING SYSTEM)
            target.Stats.ApplyCondition(CombatConditionType.EnergyDrained, "Sword of Life Stealing", 24 * 10);
            logNotes.Add($"Sword of Life Stealing: {target.Stats.CharacterName} gains 1 negative level!");

            // Wielder gains 1d4+5 temp HP (EXISTING SYSTEM)
            int tempHP = DiceService.Roll(1, 4, "Sword of Life Stealing temp HP") + 5;
            Wielder.Stats.TempHP += tempHP;
            logNotes.Add($"Sword of Life Stealing: {Wielder.Stats.CharacterName} gains {tempHP} temporary HP!");
        }
    }
}
```

### Example 2: Frost Brand using existing Energy Resistance

```csharp
public class FrostBrandBehavior : SpecificItemBehavior
{
    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);
        // Apply fire resistance 10 (EXISTING SYSTEM)
        var fireResist = new ResistEnergyEffectData {
            ResistType = DamageType.Fire,
            Amount = 10,
            SourceName = "Frost Brand",
            DurationRounds = -1  // Permanent while equipped
        };
        character.Stats.SetResistEnergyEffect(fireResist);
    }

    public override void OnUnequip()
    {
        // Remove fire resistance
        if (Wielder != null)
        {
            // Remove the Frost Brand entry from ActiveResistEnergyEffects
            Wielder.Stats.ActiveResistEnergyEffects?.RemoveAll(e => e.SourceName == "Frost Brand");
        }
        base.OnUnequip();
    }
}
```

### Example 3: Holy Avenger using existing Emanation + SR + Dispel

```csharp
public class HolyAvengerBehavior : SpecificItemBehavior
{
    private HolyAvengerSRAura _srAura;

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);
        if (!character.Stats.IsPaladin) return;

        int paladinLevel = character.Stats.GetClassLevel("Paladin");

        // Grant SR (EXISTING SYSTEM)
        int sr = 5 + paladinLevel;
        character.Stats.SpellResistance = Mathf.Max(character.Stats.SpellResistance, sr);

        // Register SR aura emanation (EXISTING FRAMEWORK)
        _srAura = new HolyAvengerSRAura {
            CenterCreature = character,
            RadiusSquares = 2,  // 10 ft
            RadiusFeet = 10f,
            RemainingRounds = 9999,
            CasterLevel = paladinLevel,
            SRValue = sr
        };
        GameManager.Instance.RegisterEmanation(_srAura);
    }
}
```

### Example 4: Demon Armor using existing Rage + Natural Attacks

```csharp
public class DemonArmorBehavior : SpecificItemBehavior
{
    private NaturalAttackDefinition _clawAttack;

    public override void OnEquip(CharacterController character)
    {
        base.OnEquip(character);
        // Grant claw attacks (EXISTING NATURAL ATTACK SYSTEM)
        _clawAttack = new NaturalAttackDefinition {
            Name = "Demon Armor Claw",
            DamageDice = 10,   // 1d10
            DamageCount = 1,
            Count = 2,         // 2 claws
            BonusDamageSource = DamageBonusSource.Strength,
            IsPrimary = false  // Secondary natural attacks at -5
        };
        character.Stats.NaturalAttacks.Add(_clawAttack);

        // Item is cursed
        Item.IsCursed = true;
    }
}
```

---

## Action Items

### Completed in This Audit
- [x] 20 systems found and documented
- [x] 7 systems need building (sworn enemy, curse, forced revert, reroll, wish, AoE path, fly)
- [x] API patterns documented for each reusable system
- [x] Time estimates revised (4 weeks → 3 weeks)
- [x] Implementation priority reordered by complexity

### Next Steps (for actual Tier 3 implementation)
1. Start with Quick Wins (Sword of Life Stealing, Life-Drinker, Mace of Smiting, Frost Brand, Shifter's Sorrow)
2. Build Sworn Enemy system for Oathbow
3. Build Equipment Curse system for Demon Armor
4. Build Reroll system for Luck Blade
5. Build HolyAvengerSRAura emanation subclass for Holy Avenger
6. Add `isRanged` parameter to behavior hooks for Dwarven Thrower
7. Implement simplified Fly system for Celestial Armor

### Minor Hook Modifications Needed
1. **Add `isRanged` bool to `OnPreAttackRoll` and `OnDamageRoll`** — for Dwarven Thrower thrown detection (0.25 days)
2. **Add `IsCursed` field to ItemData** — for Demon Armor cannot-unequip (0.25 days)
3. **Add finesse grant to FeatManager check** — for Sun Blade built-in finesse (0.25 days)
4. **Create HolyAvengerSRAura : EmanationEffectData** — for Holy Avenger SR aura (0.5 days)

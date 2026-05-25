# Major Wondrous Items by Complexity

**Project:** DND35Prototype  
**Date:** May 25, 2026  
**Related:** major_wondrous_items_implementation_plan.md

---

## OVERVIEW

This document provides a detailed breakdown of all major wondrous items organized by implementation complexity. Each tier represents exponentially increasing development time and technical challenges.

**Complexity Rating System:**
- ⭐ = Trivial (< 1 day)
- ⭐⭐ = Simple (2-3 days)
- ⭐⭐⭐ = Moderate (5-7 days)
- ⭐⭐⭐⭐ = Complex (2-3 weeks)
- ⭐⭐⭐⭐⭐ = Artifact-Level (3-4 weeks)

---

## TIER 1: SIMPLE IMPLEMENTATIONS (⭐⭐)

**Estimated Time per Item:** 2-3 days  
**Total Items:** 8  
**Total Time:** 2-3 weeks

These items build on existing systems with minimal new code. They primarily involve creating item data, assigning properties, and hooking into established mechanics.

---

### **1. Carpet of Flying (4 variants)**
**Price:** 20,000 / 35,000 / 60,000 / 75,000 gp  
**Complexity:** ⭐⭐

**Why Simple:**
- Flying mechanics likely already exist (Wings of Flying, Fly spell)
- Just needs vehicle/passenger capacity tracking
- Different sizes = different data values

**Implementation:**
```csharp
public class CarpetOfFlying : MagicalVehicle
{
    public enum Size { Small_5x5, Medium_5x10, Large_10x10, XLarge_10x15 }
    
    public CarpetOfFlying(Size size)
    {
        FlySpeed = 40;
        MovementType = Movement.Fly;
        
        switch(size)
        {
            case Size.Small_5x5: 
                Capacity = 1; 
                MaxWeight = 200; 
                Price = 20000; 
                break;
            // etc.
        }
    }
}
```

**Dependencies:**
- Flight system (assumed exists)
- Weight/passenger tracking

**Time Estimate:** 1 day (all 4 variants together)

---

### **2. Mantle of Spell Resistance (5 variants)**
**Price:** 90,000 / 121,000 / 157,000 / 198,000 / 250,000 gp  
**Complexity:** ⭐⭐

**Why Simple:**
- SR system likely exists (Robe of Archmagi already implemented with SR 18)
- Just grant different SR values
- Main logic: highest SR wins (no stacking)

**Implementation:**
```csharp
public class MantleOfSpellResistance : WondrousItem
{
    public int SpellResistanceValue { get; set; }
    
    public override void OnEquip(Character wearer)
    {
        wearer.AddSpellResistance(SpellResistanceValue);
    }
    
    public override void OnUnequip(Character wearer)
    {
        wearer.RemoveSpellResistance(SpellResistanceValue);
    }
}
```

**Dependencies:**
- Spell Resistance calculation system
- Stacking rules (verify highest wins)

**Time Estimate:** 1 day (including verification of SR stacking)

---

### **3. Stone Horse (3 variants)**
**Price:** 10,000 / 14,800 / 28,500 gp  
**Complexity:** ⭐⭐

**Why Simple:**
- Figurines of Wondrous Power likely already implemented (minor items)
- Stone Horse is just another figurine type
- Transform into mount on command

**Implementation:**
```csharp
public class StoneHorse : FigurineOfWondrousPower
{
    public enum Type { Courser, Destrier, DestrierGreater }
    
    public StoneHorse(Type type)
    {
        UsesPerWeek = 1;
        Duration = 24 * 60; // 24 hours in rounds
        
        switch(type)
        {
            case Type.Courser:
                CreatureType = "Light Horse";
                Price = 10000;
                break;
            case Type.Destrier:
                CreatureType = "Heavy Horse";
                Price = 14800;
                break;
            case Type.DestrierGreater:
                CreatureType = "Heavy Horse";
                CanFly = true;
                FlySpeed = 60;
                Price = 28500;
                break;
        }
    }
}
```

**Dependencies:**
- Figurine transformation system (from minor items)
- Horse creature stats

**Time Estimate:** 1 day (all 3 types)

---

### **4. Periapt of Proof Against Poison**
**Price:** 27,000 gp  
**Complexity:** ⭐⭐

**Why Simple:**
- Just sets poison immunity flag
- Single binary effect

**Implementation:**
```csharp
public class PeriaptOfProofAgainstPoison : WondrousItem
{
    public override void OnEquip(Character wearer)
    {
        wearer.ImmuneToPoisonAll = true;
    }
    
    public override void OnUnequip(Character wearer)
    {
        wearer.ImmuneToPoisonAll = false;
    }
}
```

**Dependencies:**
- Poison immunity system (likely exists for creatures like constructs)

**Time Estimate:** 0.5 days

---

### **5. Scarab, Golembane**
**Price:** 2,500 gp  
**Complexity:** ⭐⭐

**Why Simple:**
- Grant +2 bonus on attacks vs golems
- Allow critical hits vs golems (override immunity)

**Implementation:**
```csharp
public class ScarabGolembane : WondrousItem
{
    public override void OnEquip(Character wearer)
    {
        wearer.AddConditionalBonus(new ConditionalBonus
        {
            BonusType = BonusType.Luck,
            Value = 2,
            AppliesTo = "Attack",
            Condition = "Target is Golem"
        });
        
        wearer.CanCriticalHitGolems = true;
    }
}
```

**Dependencies:**
- Creature type checking (golem detection)
- Critical hit immunity override

**Time Estimate:** 0.5 days

---

### **6. Bottle of Air**
**Price:** 7,250 gp  
**Complexity:** ⭐⭐

**Why Simple:**
- Grants immunity to suffocation/drowning
- Single continuous effect

**Implementation:**
```csharp
public class BottleOfAir : WondrousItem
{
    public override void OnEquip(Character wearer)
    {
        wearer.ImmuneToSuffocation = true;
        wearer.CanBreatheUnderwater = true;
    }
}
```

**Dependencies:**
- Environmental hazard system (suffocation/drowning)

**Time Estimate:** 0.5 days

---

### **7. Wings of Flying**
**Price:** 54,000 gp  
**Complexity:** ⭐⭐

**Why Simple:**
- Grant fly speed 60 ft, unlimited duration
- Similar to Carpet of Flying but worn item

**Implementation:**
```csharp
public class WingsOfFlying : WondrousItem
{
    public override void OnEquip(Character wearer)
    {
        wearer.FlySpeed = 60;
        wearer.FlightManeuverability = Maneuverability.Average;
    }
    
    public override void OnUnequip(Character wearer)
    {
        wearer.FlySpeed = 0;
    }
}
```

**Dependencies:**
- Flight system

**Time Estimate:** 0.5 days

---

### **8. Robe of Blending**
**Price:** 8,400 gp  
**Complexity:** ⭐⭐

**Why Simple:**
- +10 competence bonus to Hide checks
- Single skill modifier

**Implementation:**
```csharp
public class RobeOfBlending : WondrousItem
{
    public override void OnEquip(Character wearer)
    {
        wearer.AddSkillBonus(Skill.Hide, 10, BonusType.Competence);
    }
}
```

**Dependencies:**
- Skill bonus system

**Time Estimate:** 0.25 days

---

## TIER 2: MODERATE COMPLEXITY (⭐⭐⭐)

**Estimated Time per Item:** 5-7 days  
**Total Items:** 18  
**Total Time:** 13-18 weeks

These items require new mechanics but are focused on a single primary ability or small set of related abilities.

---

### **9. Amulet of the Planes**
**Price:** 120,000 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- Requires planar travel system implementation
- At-will plane shift (unlimited uses)
- Will save for unwilling targets

**New System Required:** Planar Travel

**Implementation:**
```csharp
public class AmuletOfThePlanes : WondrousItem
{
    public void UsePlaneShift(Character wearer, Plane destination)
    {
        // At-will activation
        PlanarTravelSystem.PlaneShift(wearer, destination, requiresWillSave: false);
    }
}
```

**Dependencies:**
- Planar travel system
- Plane enum/database
- Will save mechanics

**Time Estimate:** 5 days (after planar system built)

---

### **10. Cubic Gate**
**Price:** 164,000 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- 6 sides, each attuned to different plane
- Track charges per side (3/week each = 18 total charges)
- Gate spell (allows calling creatures from plane)

**Implementation:**
```csharp
public class CubicGate : WondrousItem
{
    public Dictionary<int, Plane> SideAttunements; // Side 1-6 -> Plane
    public Dictionary<int, int> SideCharges; // Side 1-6 -> Charges remaining
    
    public void ActivateSide(int side)
    {
        if (SideCharges[side] > 0)
        {
            Plane destination = SideAttunements[side];
            PlanarTravelSystem.CreateGate(destination, duration: 10); // 1 round/level (CL 10)
            SideCharges[side]--;
        }
    }
    
    public void RestoreWeeklyCharges()
    {
        for (int i = 1; i <= 6; i++)
        {
            SideCharges[i] = 3;
        }
    }
}
```

**Dependencies:**
- Planar travel system
- Per-side charge tracking
- Weekly reset mechanics

**Time Estimate:** 7 days

---

### **11. Well of Many Worlds**
**Price:** 82,000 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- Random plane portal generation
- Two-way portal (unlike one-way plane shift)
- Dangerous interaction with Portable Hole

**Implementation:**
```csharp
public class WellOfManyWorlds : WondrousItem
{
    public void SpreadOnGround()
    {
        Plane randomPlane = GetRandomPlane();
        Portal portal = PlanarTravelSystem.CreatePortal(randomPlane, isTwoWay: true);
        
        // Check for Portable Hole within 10 ft
        if (DetectPortableHoleNearby())
        {
            DestroyBothItems();
            CreateAstralGate();
        }
    }
    
    private Plane GetRandomPlane()
    {
        Plane[] planes = { Plane.Ethereal, Plane.Astral, Plane.Elemental_Air, 
                           Plane.Elemental_Earth, Plane.Elemental_Fire, Plane.Elemental_Water };
        return planes[Random.Range(0, planes.Length)];
    }
}
```

**Dependencies:**
- Portal creation (different from plane shift)
- Random plane selection
- Item proximity detection
- Item destruction mechanics

**Time Estimate:** 6 days

---

### **12. Mantle of Faith**
**Price:** 76,000 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- Multiple abilities: +5 saves + 4 spells 1/day
- Charge tracking for 4 different spells

**Implementation:**
```csharp
public class MantleOfFaith : WondrousItem
{
    public Dictionary<string, int> DailyCharges; // Bless, Detect Evil, Remove Fear, Aid
    
    public override void OnEquip(Character wearer)
    {
        wearer.AddSaveBonus(5, BonusType.Resistance, SavingThrow.All);
        
        DailyCharges = new Dictionary<string, int>
        {
            { "Bless", 1 },
            { "DetectEvil", 1 },
            { "RemoveFear", 1 },
            { "Aid", 1 }
        };
    }
    
    public void CastSpell(string spellName)
    {
        if (DailyCharges[spellName] > 0)
        {
            SpellEffects.Cast(spellName, wearer);
            DailyCharges[spellName]--;
        }
    }
}
```

**Dependencies:**
- Spell effect system (Bless, Detect Evil, Remove Fear, Aid)
- Daily charge reset

**Time Estimate:** 5 days

---

### **13. Robe of Stars**
**Price:** 58,000 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- +1 luck bonus to saves (simple)
- Astral travel (requires planar system)
- 6 consumable stars (Magic Missile at 5th level)
- Becomes non-magical when all stars used

**Implementation:**
```csharp
public class RobeOfStars : WondrousItem
{
    public int StarsRemaining = 6;
    
    public void DetachStar()
    {
        if (StarsRemaining > 0)
        {
            SpellEffects.MagicMissile(casterLevel: 5, missiles: 3);
            StarsRemaining--;
            
            if (StarsRemaining == 0)
            {
                BecomeNonMagical();
            }
        }
    }
    
    public void TravelThroughAstralPlane(Vector3 destination)
    {
        PlanarTravelSystem.AstralTravel(wearer, destination);
    }
}
```

**Dependencies:**
- Astral Plane travel
- Consumable charges (star removal)
- Magic Missile spell
- Item becomes non-magical mechanic

**Time Estimate:** 6 days

---

### **14. Lyre of Building**
**Price:** 13,000 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- Fabricate spell (CL 13)
- Can create structures/buildings
- Takes 30 minutes to use
- 1/week limitation

**Implementation:**
```csharp
public class LyreOfBuilding : WondrousItem
{
    public int UsesRemaining = 1; // Resets weekly
    
    public void PlayLyre(Material material, int cubicFeet)
    {
        if (UsesRemaining > 0)
        {
            // Fabricate: transform raw materials into finished product
            SpellEffects.Fabricate(material, cubicFeet, casterLevel: 13);
            StartCoroutine(DelayedUse(30 * 60)); // 30 minutes
            UsesRemaining--;
        }
    }
}
```

**Dependencies:**
- Fabricate spell mechanics
- Material type system
- Construction rules
- Time passage (30 minutes)

**Time Estimate:** 5 days

---

### **15-16. Mattock/Maul of the Titans**
**Price:** 23,348 / 25,305 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- Requires high Strength to wield (19/23)
- Increased damage dice (4d6 / 4d8)
- Special abilities (excavation / sunder)

**Implementation:**
```csharp
public class MattockOfTitans : Weapon
{
    public int RequiredStrength = 19;
    
    public override bool CanWield(Character wielder)
    {
        return wielder.Strength >= RequiredStrength;
    }
    
    public override int RollDamage()
    {
        return DiceRoller.Roll("4d6") + StrengthBonus;
    }
    
    public void Excavate(Tile target)
    {
        // Remove 10 cubic feet of stone per 10 minutes
        if (target.Material == Material.Stone)
        {
            target.Remove(10); // cubic feet
        }
    }
}
```

**Dependencies:**
- Strength requirement checks
- Custom damage dice
- Excavation mechanics (Mattock)
- Sunder bonus (Maul)

**Time Estimate:** 4 days (both weapons together)

---

### **17. Cloak of the Bat**
**Price:** 26,000 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- Multiple abilities: hang, polymorph into bat, fly
- Polymorph requires creature transformation

**Implementation:**
```csharp
public class CloakOfBat : WondrousItem
{
    public void Hang()
    {
        // Hang from ceiling like bat (rest position)
        wearer.Posture = Posture.Hanging;
    }
    
    public void PolymorphIntoBat()
    {
        Polymorph.Transform(wearer, CreatureType.Bat, duration: Unlimited);
    }
    
    public void FlyInDarkness()
    {
        if (IsInDarkOrTwilight())
        {
            wearer.FlySpeed = 60;
        }
    }
}
```

**Dependencies:**
- Polymorph system
- Bat creature stats
- Light level detection

**Time Estimate:** 5 days

---

### **18. Scarab of Protection**
**Price:** 38,000 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- +4 resistance to all saves
- Absorbs death effects (12 levels)
- Absorbs negative energy (200 hp)
- Charge tracking for two separate pools

**Implementation:**
```csharp
public class ScarabOfProtection : WondrousItem
{
    public int DeathEffectCharges = 12;
    public int NegativeEnergyCharges = 200;
    
    public override void OnEquip(Character wearer)
    {
        wearer.AddSaveBonus(4, BonusType.Resistance, SavingThrow.All);
    }
    
    public bool AbsorbDeathEffect(int spellLevel)
    {
        if (DeathEffectCharges >= spellLevel)
        {
            DeathEffectCharges -= spellLevel;
            return true; // Effect absorbed
        }
        return false;
    }
    
    public bool AbsorbNegativeEnergy(int damage)
    {
        if (NegativeEnergyCharges >= damage)
        {
            NegativeEnergyCharges -= damage;
            return true;
        }
        return false;
    }
}
```

**Dependencies:**
- Death effect detection
- Negative energy damage tracking
- Charge depletion

**Time Estimate:** 6 days

---

### **19. Phylactery of Faithfulness**
**Price:** 1,000 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- Requires divine code/tenets system
- Must evaluate if action violates deity's rules
- AI/logic to determine violations

**Implementation:**
```csharp
public class PhylacteryOfFaithfulness : WondrousItem
{
    public bool WillViolateDivineTenets(Character wearer, Action proposedAction)
    {
        Deity deity = wearer.Deity;
        
        // Check action against deity's tenets
        foreach (var tenet in deity.Tenets)
        {
            if (tenet.IsViolatedBy(proposedAction))
            {
                return true;
            }
        }
        
        return false;
    }
}
```

**Dependencies:**
- Deity system with defined tenets
- Action classification
- Violation checking logic

**Time Estimate:** 7 days (requires complex divine code system)

---

### **20-21. Candle of Invocation / Incense of Meditation**
**Price:** 8,400 / 4,900 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- Alignment-specific (Candle)
- Divine caster requirement
- Temporary CL boost for spell preparation (Incense)
- Gate spell (Candle)
- Burn time tracking (consumable)

**Time Estimate:** 5 days (both items together)

---

### **22. Mantle of Great Stealth**
**Price:** 242,000 gp  
**Complexity:** ⭐⭐⭐

**Why Moderate:**
- +10 competence bonus to Hide and Move Silently
- Invisibility at will
- Greater Invisibility 1/day

**Implementation:**
```csharp
public class MantleOfGreatStealth : WondrousItem
{
    public int GreaterInvisibilityCharges = 1;
    
    public override void OnEquip(Character wearer)
    {
        wearer.AddSkillBonus(Skill.Hide, 10, BonusType.Competence);
        wearer.AddSkillBonus(Skill.MoveSilently, 10, BonusType.Competence);
    }
    
    public void UseInvisibility()
    {
        SpellEffects.Invisibility(wearer);
    }
    
    public void UseGreaterInvisibility()
    {
        if (GreaterInvisibilityCharges > 0)
        {
            SpellEffects.GreaterInvisibility(wearer);
            GreaterInvisibilityCharges--;
        }
    }
}
```

**Dependencies:**
- Invisibility spell effects
- Daily charge reset

**Time Estimate:** 4 days

---

### **23-26. Elemental Control Items (4 items)**
**Price:** 100,000 gp each  
**Complexity:** ⭐⭐⭐ each

**Why Moderate:**
- Summon Elder Elemental (large creature with complex stats)
- Control existing elementals (Charisma check vs target)
- Once per day limitation

**Time Estimate:** 7 days for all 4 (shared system)

---

## TIER 3: COMPLEX MULTI-ABILITY (⭐⭐⭐⭐)

**Estimated Time per Item:** 2-3 weeks  
**Total Items:** 10  
**Total Time:** 20-30 weeks

These items have multiple powerful systems working together, requiring significant integration work.

---

### **27. Efreeti Bottle**
**Price:** 145,000 gp  
**Complexity:** ⭐⭐⭐⭐

**Why Complex:**
- Summon efreeti with full stats and AI
- Service mechanics (1 hour of service)
- Wish negotiation (efreeti may offer 3 wishes for freedom)
- Can trap outsiders (like Iron Flask but limited)
- Creature trapping system

**Implementation Challenges:**
- Efreeti AI (obey commands for 1 hour)
- Wish mechanics (rare and powerful)
- Trap outsiders (requires creature type checking)
- Service duration tracking

**Dependencies:**
- Creature Trapping System
- Efreeti creature stats
- Wish spell implementation
- Service/command AI

**Time Estimate:** 2 weeks

---

### **28. Iron Cobra**
**Price:** 80,000 gp  
**Complexity:** ⭐⭐⭐⭐

**Why Complex:**
- Autonomous construct with AI
- Poison bite (Fort DC 20, 1d3 Con/round for 6 rounds)
- Follows simple commands (guard, attack, follow, stay)
- Permanent guardian (not consumable)

**Implementation Challenges:**
- Construct AI state machine
- Command parsing
- Guard mode (attack hostiles within range)
- Poison effect with duration

**Implementation:**
```csharp
public class IronCobra : ConstructGuardian
{
    public enum Command { Guard, Attack, Follow, Stay }
    public Command CurrentCommand;
    
    public override void Update()
    {
        switch (CurrentCommand)
        {
            case Command.Guard:
                AttackNearestHostile();
                break;
            case Command.Attack:
                AttackDesignatedTarget();
                break;
            case Command.Follow:
                FollowOwner();
                break;
            case Command.Stay:
                // Idle
                break;
        }
    }
    
    public void PoisonBite(Character target)
    {
        if (AttackRoll() >= target.AC)
        {
            int damage = DiceRoller.Roll("1d6+4");
            target.TakeDamage(damage);
            
            if (!SavingThrows.MakeFortSave(target, 20))
            {
                target.AddEffect(new PoisonEffect
                {
                    SaveDC = 20,
                    Damage = "1d3 Con",
                    Duration = 6,
                    Frequency = "per round"
                });
            }
        }
    }
}
```

**Dependencies:**
- Construct AI framework
- Command system
- Poison with Con damage
- Permanent guardian mechanics

**Time Estimate:** 2 weeks

---

### **29-32. Horn of Valhalla (4 types)**
**Price:** 15,000 / 20,000 / 35,000 / 50,000 gp  
**Complexity:** ⭐⭐⭐⭐ for the system

**Why Complex:**
- Summon 2d4+X berserkers (temporary allies)
- Berserker stats and AI
- Proficiency requirements (Brass, Bronze, Iron)
- Duration tracking (fight for 1 hour)
- 1/week limitation

**Implementation Challenges:**
- Summon multiple creatures at once
- Temporary ally AI (follow user, attack enemies)
- Proficiency checking (martial weapon proficiency required)
- Cleanup after duration ends

**Dependencies:**
- Berserker stat blocks
- Temporary ally system
- Proficiency checking
- Mass summoning

**Time Estimate:** 2.5 weeks (for all 4 horns)

---

### **33. Apparatus of Kwalish**
**Price:** 90,000 gp  
**Complexity:** ⭐⭐⭐⭐

**Why Complex:**
- Vehicle with 10 levers controlling functions
- Each lever has specific effect (forward, back, left, right, up, down, open/close windows, claw attacks, etc.)
- AC 20, 200 HP (can take damage)
- 2 pincer attacks (2d6+6 each)
- Holds 2 Medium creatures

**Implementation Challenges:**
- Lever control UI (10 buttons)
- Vehicle movement (walk + swim)
- Attack from vehicle
- Vision system (windows open/closed)
- Passenger management
- Damage to vehicle

**Implementation:**
```csharp
public class ApparatusOfKwalish : MagicalVehicle
{
    public enum Lever 
    { 
        Forward, Backward, Left, Right, 
        Up, Down, 
        OpenWindows, CloseWindows, 
        ExtendClaws, RetractClaws 
    }
    
    public Dictionary<Lever, bool> LeverStates;
    
    public void PullLever(Lever lever)
    {
        switch (lever)
        {
            case Lever.Forward:
                MoveForward(30); // 30 ft speed
                break;
            case Lever.ExtendClaws:
                AttackWithClaws();
                break;
            case Lever.OpenWindows:
                WindowsOpen = true;
                // Can see outside but vulnerable
                break;
            // etc.
        }
    }
    
    private void AttackWithClaws()
    {
        // 2 attacks at +12 melee
        for (int i = 0; i < 2; i++)
        {
            int attackRoll = DiceRoller.Roll("1d20") + 12;
            if (attackRoll >= target.AC)
            {
                int damage = DiceRoller.Roll("2d6+6");
                target.TakeDamage(damage);
            }
        }
    }
}
```

**Dependencies:**
- Vehicle system (base class)
- Lever control UI
- Underwater movement
- Vehicle HP/AC tracking
- Vehicle attacks

**Time Estimate:** 3 weeks

---

### **34. Mirror of Opposition**
**Price:** 92,000 gp  
**Complexity:** ⭐⭐⭐⭐

**Why Complex:**
- Clone entire character (stats, abilities, spells, items)
- Invert alignment
- Duplicate attacks original
- Duplicate's items are non-magical
- Duration: until defeated or 1d4+10 rounds

**Implementation Challenges:**
- Deep copy of character data
- Alignment inversion logic
- Duplicate AI (hostile to original)
- Non-magical item copies
- Timed destruction

**Implementation:**
```csharp
public class MirrorOfOpposition : MagicalMirror
{
    public Character CreateDuplicate(Character original)
    {
        // Deep clone character
        Character duplicate = DeepClone(original);
        
        // Invert alignment
        duplicate.Alignment = InvertAlignment(original.Alignment);
        
        // Make items non-magical
        foreach (var item in duplicate.Inventory)
        {
            item.IsMagical = false;
        }
        
        // Set to attack original
        duplicate.SetTarget(original);
        duplicate.Attitude = Attitude.Hostile;
        
        // Destruction timer
        int duration = Random.Range(1, 4) + 10;
        StartCoroutine(DestroyAfterRounds(duplicate, duration));
        
        return duplicate;
    }
    
    private Alignment InvertAlignment(Alignment original)
    {
        Alignment inverted = new Alignment();
        
        // Lawful <-> Chaotic
        if (original.LawChaos == LawChaosAxis.Lawful)
            inverted.LawChaos = LawChaosAxis.Chaotic;
        else if (original.LawChaos == LawChaosAxis.Chaotic)
            inverted.LawChaos = LawChaosAxis.Lawful;
        else
            inverted.LawChaos = LawChaosAxis.Neutral;
        
        // Good <-> Evil
        if (original.GoodEvil == GoodEvilAxis.Good)
            inverted.GoodEvil = GoodEvilAxis.Evil;
        else if (original.GoodEvil == GoodEvilAxis.Evil)
            inverted.GoodEvil = GoodEvilAxis.Good;
        else
            inverted.GoodEvil = GoodEvilAxis.Neutral;
        
        return inverted;
    }
}
```

**Dependencies:**
- Character deep cloning
- Alignment system
- Duplicate AI
- Timed entity destruction

**Time Estimate:** 2.5 weeks

---

## TIER 4: ARTIFACT-LEVEL (⭐⭐⭐⭐⭐)

**Estimated Time per Item:** 3-4 weeks  
**Total Items:** 3  
**Total Time:** 9-12 weeks

These are the most complex items, requiring multiple interacting systems, extensive UI, and careful save/load handling.

---

### **35. Iron Flask**
**Price:** 170,000 gp  
**Complexity:** ⭐⭐⭐⭐⭐

**Why Artifact-Level:**
- Trap ANY creature (not just outsiders)
- Store creature indefinitely with complete state preservation
- Service mechanics (creature serves for 1 hour OR is hostile)
- Must work with save/load system
- Capacity: 1 creature (but any power level)

**Implementation Challenges:**
- Serialize complete creature state
- Handle creatures with active effects, temporary bonuses, etc.
- Service AI (obey commands for 1 hour)
- Hostile release (immediate combat)
- Interaction with other trapping items (only 1 creature)

**Implementation:**
```csharp
public class IronFlask : WondrousItem
{
    public TrappedCreature Creature;
    
    public bool TrapCreature(Character target)
    {
        if (Creature != null)
            return false; // Already occupied
        
        // Will save DC 19
        if (SavingThrows.MakeWillSave(target, 19))
            return false; // Resisted
        
        // Trap creature
        Creature = new TrappedCreature
        {
            Name = target.Name,
            SerializedData = SerializeCharacter(target),
            TrapTime = DateTime.Now
        };
        
        // Remove from game world
        RemoveFromCombat(target);
        
        return true;
    }
    
    public Character ReleaseCreature(bool friendly)
    {
        if (Creature == null)
            return null;
        
        // Restore creature
        Character released = DeserializeCharacter(Creature.SerializedData);
        
        if (friendly)
        {
            // Serve for 1 hour
            released.Attitude = Attitude.Friendly;
            released.Owner = user;
            released.ServiceDuration = 600; // 1 hour = 600 rounds
        }
        else
        {
            // Hostile
            released.Attitude = Attitude.Hostile;
            released.Target = user;
        }
        
        // Clear flask
        Creature = null;
        
        return released;
    }
}
```

**Dependencies:**
- **Creature Trapping System** (must be built first)
- Character serialization/deserialization
- Service AI with command following
- Save/load persistence
- Will save mechanics

**Time Estimate:** 3.5 weeks

---

### **36. Mirror of Life Trapping**
**Price:** 200,000 gp  
**Complexity:** ⭐⭐⭐⭐⭐

**Why Artifact-Level:**
- Trap up to 15 creatures (massive capacity)
- **UI required** to view all trapped creatures
- Speak command word + name to view specific creature
- Release individually or all at once (mass release)
- Automatic trapping (triggers when viewed)
- 50 ft range
- Save/load must preserve all 15 creatures

**Implementation Challenges:**
- UI panel showing list of trapped creatures (names, portraits, HP)
- Mass release mechanics
- View individual creature details
- 15 separate storage slots
- Breaking mirror releases all (destruction mechanic)

**Implementation:**
```csharp
public class MirrorOfLifeTrapping : MagicalMirror
{
    public List<TrappedCreature> TrappedCreatures = new List<TrappedCreature>();
    public int MaxCapacity = 15;
    
    public void OnViewed(Character viewer)
    {
        if (TrappedCreatures.Count >= MaxCapacity)
            return; // Full
        
        // Automatic trap (Will DC 23)
        if (!SavingThrows.MakeWillSave(viewer, 23))
        {
            TrapCreature(viewer);
        }
    }
    
    private void TrapCreature(Character target)
    {
        TrappedCreature trapped = new TrappedCreature
        {
            Name = target.Name,
            Portrait = target.Portrait,
            CurrentHP = target.CurrentHP,
            MaxHP = target.MaxHP,
            SerializedData = SerializeCharacter(target)
        };
        
        TrappedCreatures.Add(trapped);
        RemoveFromCombat(target);
    }
    
    public void ShowTrappedCreaturesUI()
    {
        // Display UI panel
        TrappedCreaturesPanel panel = UIManager.ShowPanel<TrappedCreaturesPanel>();
        panel.PopulateList(TrappedCreatures);
    }
    
    public void ViewCreature(string name)
    {
        TrappedCreature creature = TrappedCreatures.Find(c => c.Name == name);
        if (creature != null)
        {
            // Show detailed view in mirror
            MirrorDisplay.ShowCreature(creature);
        }
    }
    
    public void ReleaseCreature(int index)
    {
        if (index < 0 || index >= TrappedCreatures.Count)
            return;
        
        Character released = DeserializeCharacter(TrappedCreatures[index].SerializedData);
        released.Attitude = Attitude.Hostile; // Always hostile
        
        TrappedCreatures.RemoveAt(index);
        AddToCurrentScene(released);
    }
    
    public void ReleaseAll()
    {
        for (int i = TrappedCreatures.Count - 1; i >= 0; i--)
        {
            ReleaseCreature(i);
        }
    }
    
    public void OnDestroyed()
    {
        ReleaseAll(); // Breaking mirror releases all
    }
}
```

**UI Components:**
```csharp
public class TrappedCreaturesPanel : UIPanel
{
    public List<TrappedCreatureRow> Rows;
    
    public void PopulateList(List<TrappedCreature> creatures)
    {
        foreach (var creature in creatures)
        {
            TrappedCreatureRow row = Instantiate(rowPrefab);
            row.SetData(creature.Name, creature.Portrait, $"{creature.CurrentHP}/{creature.MaxHP}");
            row.OnReleaseClicked += () => mirror.ReleaseCreature(creatures.IndexOf(creature));
            Rows.Add(row);
        }
    }
}
```

**Dependencies:**
- **Creature Trapping System**
- **UI Framework** for trapped creatures panel
- Mass release mechanics
- Mirror destruction triggers release
- Command word recognition

**Time Estimate:** 4 weeks (most complex item)

---

### **37. Mirror of Mental Prowess**
**Price:** 175,000 gp  
**Complexity:** ⭐⭐⭐⭐⭐

**Why Artifact-Level:**
- 5 different powerful abilities
- +2 enhancement to Int, Wis, Cha (temporary, 8 hours, 1/week)
- Scrying (as spell, DC 19)
- Detect Thoughts (DC 15)
- Suggestion (DC 16)
- Telepathy (120 ft range)

**Implementation Challenges:**
- Multiple spell-like abilities with different DCs
- Temporary ability score enhancement (8 hours)
- Scrying requires vision system
- Telepathy requires communication channel
- Usage tracking (ability boost 1/week)

**Implementation:**
```csharp
public class MirrorOfMentalProwess : MagicalMirror
{
    public bool AbilityBoostUsedThisWeek = false;
    
    public void GrantAbilityBoost(Character viewer)
    {
        if (!AbilityBoostUsedThisWeek)
        {
            viewer.AddTemporaryBonus(Ability.Intelligence, 2, duration: 480); // 8 hours = 480 rounds
            viewer.AddTemporaryBonus(Ability.Wisdom, 2, duration: 480);
            viewer.AddTemporaryBonus(Ability.Charisma, 2, duration: 480);
            
            AbilityBoostUsedThisWeek = true;
        }
    }
    
    public void Scry(Character target)
    {
        SpellEffects.Scrying(target, saveDC: 19, casterLevel: 17);
    }
    
    public void DetectThoughts(Character target)
    {
        SpellEffects.DetectThoughts(target, saveDC: 15);
    }
    
    public void Suggestion(Character target, string command)
    {
        SpellEffects.Suggestion(target, command, saveDC: 16);
    }
    
    public void UseTelepathy(Character target)
    {
        if (Distance(owner, target) <= 120)
        {
            TelepathyChannel.Establish(owner, target);
        }
    }
}
```

**Dependencies:**
- Scrying spell implementation
- Detect Thoughts spell
- Suggestion spell
- Telepathy communication system
- Temporary ability score enhancements
- Weekly usage reset

**Time Estimate:** 3.5 weeks

---

## COMPLEXITY SUMMARY TABLE

| Tier | Complexity | Items | Total Time | Priority |
|------|------------|-------|------------|----------|
| 1 | ⭐⭐ | 8 | 2-3 weeks | HIGH |
| 2 | ⭐⭐⭐ | 18 | 13-18 weeks | MEDIUM-HIGH |
| 3 | ⭐⭐⭐⭐ | 10 | 20-30 weeks | MEDIUM |
| 4 | ⭐⭐⭐⭐⭐ | 3 | 9-12 weeks | LOW (Artifact) |
| **TOTAL** | | **39** | **44-63 weeks** | |

**Realistic Estimate (with parallelization):** 16-20 weeks

---

## IMPLEMENTATION ORDER BY COMPLEXITY

### **Phase 1: Quick Wins (Tier 1)**
Build momentum with simple items that leverage existing systems.

**Items:** Carpets, Mantles of SR, Stone Horse, Periapt, Scarab, Bottle of Air, Wings, Robe of Blending

**Time:** 2-3 weeks

---

### **Phase 2: Core Systems + Tier 2**
Build planar travel, then implement dependent items.

**Items:** Amulet of Planes, Cubic Gate, Well of Many Worlds, Mantle of Faith, Robe of Stars, Lyre, Titan Weapons, Cloaks, Scarab of Protection, Divine items, Elemental Control

**Time:** 13-18 weeks

---

### **Phase 3: Complex Multi-System (Tier 3)**
Tackle items requiring significant integration.

**Items:** Efreeti Bottle, Iron Cobra, Horn of Valhalla, Apparatus of Kwalish, Mirror of Opposition

**Time:** 11-15 weeks

---

### **Phase 4: Artifacts (Tier 4)**
End-game legendary items.

**Items:** Iron Flask, Mirror of Life Trapping, Mirror of Mental Prowess

**Time:** 10-12 weeks

---

## KEY TAKEAWAYS

1. **Start with Tier 1** to build momentum and test systems
2. **Build Planar Travel early** (unlocks 5 items)
3. **Build Creature Trapping mid-way** (unlocks 3 artifact items)
4. **Leave artifacts for last** (most complex, highest risk)
5. **Parallelization possible** for independent items within same tier
6. **System reuse is key** - building CreatureTrapSystem once enables 3 items

---

**Document Version:** 1.0  
**Last Updated:** May 25, 2026

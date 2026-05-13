using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Combat
{
/// <summary>
/// Regression tests for Protection from Energy (D&D 3.5e PHB p.266).
/// Tests absorption mechanics, stacking rules, vulnerability interaction,
/// energy resistance interaction, mixed damage handling, and duration.
/// </summary>
public static class ProtectionFromEnergyRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void protection_from_energy_test() => RunAll();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== PROTECTION FROM ENERGY RULES TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        SpellDatabase.Init();

        TestSpellDefinition();
        TestAbsorptionCalculation();
        TestDamageAbsorption();
        TestSpellDischarge();
        TestOverflowDamage();
        TestNonStackingSameType();
        TestMultipleProtectionsDifferentTypes();
        TestMixedDamage();
        TestVulnerabilityInteraction();
        TestEnergyResistanceInteraction();
        TestDuration();
        TestEffectDataMethods();

        Debug.Log($"====== Protection from Energy Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  FAIL: {testName} {detail}");
        }
    }

    private static CharacterController CreateController(string name, int level = 5)
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: level,
            characterClass: "Wizard",
            str: 10,
            dex: 12,
            con: 12,
            wis: 10,
            intelligence: 16,
            cha: 10,
            bab: Mathf.Max(1, level / 2),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 50,
            raceName: "Human");

        GameObject go = new GameObject($"ProtEnergy_{name}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Init(stats, Vector2Int.zero, null, null);
        return controller;
    }

    private static void DestroyController(CharacterController controller)
    {
        if (controller != null && controller.gameObject != null)
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
    }

    // ================================================================
    //  TEST: Spell Definition
    // ================================================================
    private static void TestSpellDefinition()
    {
        Debug.Log("--- Test: Spell Definition ---");

        SpellData spell = SpellDatabase.GetSpell(SpellNames.PROTECTION_FROM_ENERGY);
        Assert(spell != null, "Spell exists in database");

        if (spell != null)
        {
            Assert(spell.Name == "Protection from Energy", "Spell name is correct");
            Assert(spell.School == "Abjuration", "School is Abjuration");
            Assert(spell.SpellLevel == 3, "Base spell level is 3");
            Assert(spell.RangeCategory == SpellRangeCategory.Touch, "Range is Touch");
            Assert(spell.TargetType == SpellTargetType.SingleAlly, "Target is SingleAlly");
            Assert(spell.EffectType == SpellEffectType.Buff, "Effect type is Buff");
            Assert(spell.IsDismissible, "Spell is dismissible");
            Assert(spell.DurationType == DurationType.Minutes, "Duration type is Minutes");
            Assert(spell.DurationValue == 10, "Duration value is 10 (minutes/level)");
            Assert(spell.DurationScalesWithLevel, "Duration scales with level");

            // Check class availability
            bool hasWizard = spell.IsAvailableFor("Wizard", 3);
            bool hasSorcerer = spell.IsAvailableFor("Sorcerer", 3);
            bool hasCleric = spell.IsAvailableFor("Cleric", 3);
            bool hasDruid = spell.IsAvailableFor("Druid", 3);

            Assert(hasWizard, "Available to Wizard at level 3");
            Assert(hasSorcerer, "Available to Sorcerer at level 3");
            Assert(hasCleric, "Available to Cleric at level 3");
            Assert(hasDruid, "Available to Druid at level 3");
        }

        // Check Ranger availability at level 2
        SpellData rangerSpell = SpellDatabase.GetSpell("protection_from_energy_rgr");
        Assert(rangerSpell != null, "Ranger alias exists in database",
            rangerSpell == null ? "(spell is null)" : "");

        // If rangerSpell resolves to the same canonical spell, check Ranger availability
        if (rangerSpell != null)
        {
            bool hasRanger = rangerSpell.IsAvailableFor("Ranger", 2);
            Assert(hasRanger, "Available to Ranger at level 2");
        }
    }

    // ================================================================
    //  TEST: Absorption Calculation (12 per CL, max 120)
    // ================================================================
    private static void TestAbsorptionCalculation()
    {
        Debug.Log("--- Test: Absorption Calculation ---");

        Assert(ProtectionFromEnergyEffectData.CalculateAbsorptionPool(1) == 12,
            "CL 1 = 12 points", $"got {ProtectionFromEnergyEffectData.CalculateAbsorptionPool(1)}");

        Assert(ProtectionFromEnergyEffectData.CalculateAbsorptionPool(5) == 60,
            "CL 5 = 60 points", $"got {ProtectionFromEnergyEffectData.CalculateAbsorptionPool(5)}");

        Assert(ProtectionFromEnergyEffectData.CalculateAbsorptionPool(7) == 84,
            "CL 7 = 84 points", $"got {ProtectionFromEnergyEffectData.CalculateAbsorptionPool(7)}");

        Assert(ProtectionFromEnergyEffectData.CalculateAbsorptionPool(10) == 120,
            "CL 10 = 120 points (max)", $"got {ProtectionFromEnergyEffectData.CalculateAbsorptionPool(10)}");

        Assert(ProtectionFromEnergyEffectData.CalculateAbsorptionPool(15) == 120,
            "CL 15 = 120 points (capped)", $"got {ProtectionFromEnergyEffectData.CalculateAbsorptionPool(15)}");

        Assert(ProtectionFromEnergyEffectData.CalculateAbsorptionPool(20) == 120,
            "CL 20 = 120 points (capped)", $"got {ProtectionFromEnergyEffectData.CalculateAbsorptionPool(20)}");
    }

    // ================================================================
    //  TEST: Damage Absorption (reduces protection points)
    // ================================================================
    private static void TestDamageAbsorption()
    {
        Debug.Log("--- Test: Damage Absorption ---");

        CharacterController target = CreateController("Defender", 5);

        target.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Fire,
            MaxAbsorptionPoints = 60,
            RemainingAbsorptionPoints = 60,
            DurationRemainingRounds = 500,
            CasterLevel = 5
        });

        // Take 20 fire damage
        int hpBefore = target.Stats.CurrentHP;
        DamagePacket packet = new DamagePacket
        {
            RawDamage = 20,
            Types = new HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Fireball"
        };
        DamageResolutionResult result = target.Stats.ApplyIncomingDamage(20, packet);

        Assert(result.ProtectionFromEnergyAbsorbed == 20,
            "20 fire damage fully absorbed", $"absorbed={result.ProtectionFromEnergyAbsorbed}");
        Assert(result.FinalDamage == 0,
            "No damage dealt", $"final={result.FinalDamage}");
        Assert(target.Stats.CurrentHP == hpBefore,
            "HP unchanged", $"hp={target.Stats.CurrentHP}, expected={hpBefore}");

        // Check remaining pool
        ProtectionFromEnergyEffectData effect = target.Stats.GetProtectionFromEnergy(DamageType.Fire);
        Assert(effect != null, "Protection still active");
        if (effect != null)
        {
            Assert(effect.RemainingAbsorptionPoints == 40,
                "40 points remaining (60 - 20)", $"remaining={effect.RemainingAbsorptionPoints}");
        }

        DestroyController(target);
    }

    // ================================================================
    //  TEST: Spell Discharge (ends when points reach 0)
    // ================================================================
    private static void TestSpellDischarge()
    {
        Debug.Log("--- Test: Spell Discharge ---");

        CharacterController target = CreateController("DischargeTarget", 5);

        target.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Cold,
            MaxAbsorptionPoints = 30,
            RemainingAbsorptionPoints = 30,
            DurationRemainingRounds = 500,
            CasterLevel = 3
        });

        // Take exactly 30 cold damage (should exhaust protection)
        DamagePacket packet = new DamagePacket
        {
            RawDamage = 30,
            Types = new HashSet<DamageType> { DamageType.Cold },
            Source = AttackSource.Spell,
            SourceName = "Cone of Cold"
        };
        DamageResolutionResult result = target.Stats.ApplyIncomingDamage(30, packet);

        Assert(result.ProtectionFromEnergyAbsorbed == 30,
            "All 30 points absorbed", $"absorbed={result.ProtectionFromEnergyAbsorbed}");
        Assert(result.ProtectionFromEnergyDischarged,
            "Protection was discharged");
        Assert(result.FinalDamage == 0,
            "No overflow damage", $"final={result.FinalDamage}");

        // Protection should be removed
        ProtectionFromEnergyEffectData effect = target.Stats.GetProtectionFromEnergy(DamageType.Cold);
        Assert(effect == null, "Protection removed after discharge");

        DestroyController(target);
    }

    // ================================================================
    //  TEST: Overflow Damage (carries through after exhausted)
    // ================================================================
    private static void TestOverflowDamage()
    {
        Debug.Log("--- Test: Overflow Damage ---");

        CharacterController target = CreateController("OverflowTarget", 5);

        target.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Fire,
            MaxAbsorptionPoints = 20,
            RemainingAbsorptionPoints = 20,
            DurationRemainingRounds = 500,
            CasterLevel = 2
        });

        int hpBefore = target.Stats.CurrentHP;

        // Take 50 fire damage (20 absorbed, 30 overflows)
        DamagePacket packet = new DamagePacket
        {
            RawDamage = 50,
            Types = new HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Fireball"
        };
        DamageResolutionResult result = target.Stats.ApplyIncomingDamage(50, packet);

        Assert(result.ProtectionFromEnergyAbsorbed == 20,
            "20 points absorbed", $"absorbed={result.ProtectionFromEnergyAbsorbed}");
        Assert(result.ProtectionFromEnergyDischarged,
            "Protection discharged on overflow");
        Assert(result.FinalDamage == 30,
            "30 overflow damage", $"final={result.FinalDamage}");
        Assert(target.Stats.CurrentHP == hpBefore - 30,
            "HP reduced by overflow amount", $"hp={target.Stats.CurrentHP}, expected={hpBefore - 30}");

        DestroyController(target);
    }

    // ================================================================
    //  TEST: Non-Stacking (same energy type replaces)
    // ================================================================
    private static void TestNonStackingSameType()
    {
        Debug.Log("--- Test: Non-Stacking Same Type ---");

        CharacterController target = CreateController("StackTarget", 5);

        // Apply 60-point fire protection
        target.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Fire,
            MaxAbsorptionPoints = 60,
            RemainingAbsorptionPoints = 60,
            DurationRemainingRounds = 500,
            CasterLevel = 5
        });

        // Absorb some damage
        DamagePacket packet = new DamagePacket
        {
            RawDamage = 30,
            Types = new HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Fireball"
        };
        target.Stats.ApplyIncomingDamage(30, packet);

        ProtectionFromEnergyEffectData effect1 = target.Stats.GetProtectionFromEnergy(DamageType.Fire);
        Assert(effect1 != null && effect1.RemainingAbsorptionPoints == 30,
            "30 points remaining after first hit", effect1 != null ? $"remaining={effect1.RemainingAbsorptionPoints}" : "(null)");

        // Apply NEW 120-point fire protection (should replace)
        target.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Fire,
            MaxAbsorptionPoints = 120,
            RemainingAbsorptionPoints = 120,
            DurationRemainingRounds = 1000,
            CasterLevel = 10
        });

        ProtectionFromEnergyEffectData effect2 = target.Stats.GetProtectionFromEnergy(DamageType.Fire);
        Assert(effect2 != null, "New protection exists");
        if (effect2 != null)
        {
            Assert(effect2.RemainingAbsorptionPoints == 120,
                "New protection has 120 points (replaced, not added)",
                $"remaining={effect2.RemainingAbsorptionPoints}");
            Assert(effect2.MaxAbsorptionPoints == 120,
                "New max is 120", $"max={effect2.MaxAbsorptionPoints}");
        }

        // Should only have one fire protection
        int fireCount = 0;
        if (target.Stats.ActiveProtectionFromEnergyEffects != null)
        {
            foreach (var eff in target.Stats.ActiveProtectionFromEnergyEffects)
            {
                if (eff != null && eff.ToDamageType() == DamageType.Fire)
                    fireCount++;
            }
        }
        Assert(fireCount == 1, "Only one fire protection (non-stacking)", $"count={fireCount}");

        DestroyController(target);
    }

    // ================================================================
    //  TEST: Multiple Protections (different energy types coexist)
    // ================================================================
    private static void TestMultipleProtectionsDifferentTypes()
    {
        Debug.Log("--- Test: Multiple Protections Different Types ---");

        CharacterController target = CreateController("MultiTarget", 5);

        // Apply fire protection
        target.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Fire,
            MaxAbsorptionPoints = 60,
            RemainingAbsorptionPoints = 60,
            DurationRemainingRounds = 500,
            CasterLevel = 5
        });

        // Apply cold protection
        target.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Cold,
            MaxAbsorptionPoints = 84,
            RemainingAbsorptionPoints = 84,
            DurationRemainingRounds = 700,
            CasterLevel = 7
        });

        // Apply acid protection
        target.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Acid,
            MaxAbsorptionPoints = 120,
            RemainingAbsorptionPoints = 120,
            DurationRemainingRounds = 1000,
            CasterLevel = 10
        });

        Assert(target.Stats.GetProtectionFromEnergy(DamageType.Fire) != null,
            "Fire protection active");
        Assert(target.Stats.GetProtectionFromEnergy(DamageType.Cold) != null,
            "Cold protection active");
        Assert(target.Stats.GetProtectionFromEnergy(DamageType.Acid) != null,
            "Acid protection active");
        Assert(target.Stats.GetProtectionFromEnergy(DamageType.Electricity) == null,
            "No electricity protection");

        int totalEffects = target.Stats.ActiveProtectionFromEnergyEffects != null
            ? target.Stats.ActiveProtectionFromEnergyEffects.Count : 0;
        Assert(totalEffects == 3, "Three protections active simultaneously", $"count={totalEffects}");

        // Take fire damage - should only affect fire protection
        DamagePacket firePacket = new DamagePacket
        {
            RawDamage = 25,
            Types = new HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Fireball"
        };
        target.Stats.ApplyIncomingDamage(25, firePacket);

        ProtectionFromEnergyEffectData fireEff = target.Stats.GetProtectionFromEnergy(DamageType.Fire);
        ProtectionFromEnergyEffectData coldEff = target.Stats.GetProtectionFromEnergy(DamageType.Cold);
        ProtectionFromEnergyEffectData acidEff = target.Stats.GetProtectionFromEnergy(DamageType.Acid);

        Assert(fireEff != null && fireEff.RemainingAbsorptionPoints == 35,
            "Fire protection reduced to 35", fireEff != null ? $"remaining={fireEff.RemainingAbsorptionPoints}" : "(null)");
        Assert(coldEff != null && coldEff.RemainingAbsorptionPoints == 84,
            "Cold protection unchanged at 84", coldEff != null ? $"remaining={coldEff.RemainingAbsorptionPoints}" : "(null)");
        Assert(acidEff != null && acidEff.RemainingAbsorptionPoints == 120,
            "Acid protection unchanged at 120", acidEff != null ? $"remaining={acidEff.RemainingAbsorptionPoints}" : "(null)");

        DestroyController(target);
    }

    // ================================================================
    //  TEST: Mixed Damage (only energy portion absorbed)
    // ================================================================
    private static void TestMixedDamage()
    {
        Debug.Log("--- Test: Mixed Damage ---");

        CharacterController target = CreateController("MixedTarget", 5);

        target.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Fire,
            MaxAbsorptionPoints = 60,
            RemainingAbsorptionPoints = 60,
            DurationRemainingRounds = 500,
            CasterLevel = 5
        });

        int hpBefore = target.Stats.CurrentHP;

        // Pure fire damage - should be absorbed
        DamagePacket firePacket = new DamagePacket
        {
            RawDamage = 15,
            Types = new HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Burning Hands"
        };
        DamageResolutionResult fireResult = target.Stats.ApplyIncomingDamage(15, firePacket);
        Assert(fireResult.ProtectionFromEnergyAbsorbed == 15,
            "Pure fire damage fully absorbed", $"absorbed={fireResult.ProtectionFromEnergyAbsorbed}");
        Assert(fireResult.FinalDamage == 0,
            "No damage from pure fire", $"final={fireResult.FinalDamage}");

        // Physical damage (slashing) - should NOT be absorbed
        DamagePacket slashPacket = new DamagePacket
        {
            RawDamage = 10,
            Types = new HashSet<DamageType> { DamageType.Slashing },
            Source = AttackSource.Weapon,
            SourceName = "Longsword"
        };
        DamageResolutionResult slashResult = target.Stats.ApplyIncomingDamage(10, slashPacket);
        Assert(slashResult.ProtectionFromEnergyAbsorbed == 0,
            "Slashing damage not absorbed by fire protection", $"absorbed={slashResult.ProtectionFromEnergyAbsorbed}");

        // Check fire protection unchanged by slashing
        ProtectionFromEnergyEffectData eff = target.Stats.GetProtectionFromEnergy(DamageType.Fire);
        Assert(eff != null && eff.RemainingAbsorptionPoints == 45,
            "Fire protection at 45 (only reduced by fire damage)",
            eff != null ? $"remaining={eff.RemainingAbsorptionPoints}" : "(null)");

        DestroyController(target);
    }

    // ================================================================
    //  TEST: Vulnerability Interaction
    //  Protection absorbs BEFORE vulnerability multiplier.
    //  If overflow: vulnerability applies to overflow damage only.
    // ================================================================
    private static void TestVulnerabilityInteraction()
    {
        Debug.Log("--- Test: Vulnerability Interaction ---");

        // Test the EffectData absorption directly - vulnerability would be applied
        // by the caller of ApplyIncomingDamage in the actual game flow.
        // The key rule: Protection absorbs raw damage BEFORE vulnerability multiplier.

        ProtectionFromEnergyEffectData effect = new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Fire,
            MaxAbsorptionPoints = 100,
            RemainingAbsorptionPoints = 100,
            DurationRemainingRounds = 500,
            CasterLevel = 9
        };

        // 40 fire damage: protection absorbs all 40, vulnerability NOT applied
        int absorbed1 = effect.AbsorbDamage(40);
        Assert(absorbed1 == 40,
            "Protection absorbs 40 (vulnerability not relevant)", $"absorbed={absorbed1}");
        Assert(effect.RemainingAbsorptionPoints == 60,
            "60 points remaining", $"remaining={effect.RemainingAbsorptionPoints}");

        // 80 fire damage: protection absorbs 60, 20 overflows
        // Vulnerability would apply to the 20 overflow: 20 * 1.5 = 30
        int absorbed2 = effect.AbsorbDamage(80);
        Assert(absorbed2 == 60,
            "Protection absorbs remaining 60", $"absorbed={absorbed2}");
        Assert(effect.IsDischarged,
            "Protection discharged");

        int overflow = 80 - absorbed2; // 20
        int vulnerableDamage = Mathf.FloorToInt(overflow * 1.5f); // 30
        Assert(overflow == 20, "20 overflow damage", $"overflow={overflow}");
        Assert(vulnerableDamage == 30, "Vulnerable overflow = 30 (20 * 1.5)", $"vulnDmg={vulnerableDamage}");
    }

    // ================================================================
    //  TEST: Energy Resistance Interaction
    //  Protection absorbs first, then Resist Energy applies to remainder.
    // ================================================================
    private static void TestEnergyResistanceInteraction()
    {
        Debug.Log("--- Test: Energy Resistance Interaction ---");

        CharacterController target = CreateController("ResistTarget", 5);

        // Apply Protection from Energy (fire) with 30 points
        target.Stats.SetProtectionFromEnergyEffect(new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Fire,
            MaxAbsorptionPoints = 30,
            RemainingAbsorptionPoints = 30,
            DurationRemainingRounds = 500,
            CasterLevel = 3
        });

        // Apply Resist Energy (fire) 10
        target.Stats.SetResistEnergyEffect(new ResistEnergyEffectData
        {
            EnergyType = ResistEnergyType.Fire,
            ResistanceAmount = 10,
            DurationRemainingRounds = 500
        });

        int hpBefore = target.Stats.CurrentHP;

        // Take 20 fire damage: Protection absorbs 20, Resist not used
        DamagePacket packet1 = new DamagePacket
        {
            RawDamage = 20,
            Types = new HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Fireball"
        };
        DamageResolutionResult result1 = target.Stats.ApplyIncomingDamage(20, packet1);

        Assert(result1.ProtectionFromEnergyAbsorbed == 20,
            "Protection absorbs 20 first", $"absorbed={result1.ProtectionFromEnergyAbsorbed}");
        Assert(result1.FinalDamage == 0,
            "No damage (Protection handles all)", $"final={result1.FinalDamage}");
        Assert(target.Stats.CurrentHP == hpBefore,
            "HP unchanged", $"hp={target.Stats.CurrentHP}");

        // Check remaining protection: 10
        ProtectionFromEnergyEffectData protEff = target.Stats.GetProtectionFromEnergy(DamageType.Fire);
        Assert(protEff != null && protEff.RemainingAbsorptionPoints == 10,
            "10 protection points remaining", protEff != null ? $"remaining={protEff.RemainingAbsorptionPoints}" : "(null)");

        // Take 35 fire damage: Protection absorbs 10 (discharged), 25 remains
        // Resist Energy reduces 25 by 10 → 15 final damage
        DamagePacket packet2 = new DamagePacket
        {
            RawDamage = 35,
            Types = new HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Fireball 2"
        };
        DamageResolutionResult result2 = target.Stats.ApplyIncomingDamage(35, packet2);

        Assert(result2.ProtectionFromEnergyAbsorbed == 10,
            "Protection absorbs remaining 10", $"absorbed={result2.ProtectionFromEnergyAbsorbed}");
        Assert(result2.ProtectionFromEnergyDischarged,
            "Protection discharged");
        Assert(result2.ResistanceApplied == 10,
            "Resist Energy reduces 25 by 10", $"resist={result2.ResistanceApplied}");
        Assert(result2.FinalDamage == 15,
            "15 final damage (35 - 10 protection - 10 resist)", $"final={result2.FinalDamage}");

        // Take more fire damage: Protection gone, only Resist Energy applies
        DamagePacket packet3 = new DamagePacket
        {
            RawDamage = 25,
            Types = new HashSet<DamageType> { DamageType.Fire },
            Source = AttackSource.Spell,
            SourceName = "Fireball 3"
        };
        DamageResolutionResult result3 = target.Stats.ApplyIncomingDamage(25, packet3);

        Assert(result3.ProtectionFromEnergyAbsorbed == 0,
            "No protection to absorb (discharged)", $"absorbed={result3.ProtectionFromEnergyAbsorbed}");
        Assert(result3.ResistanceApplied == 10,
            "Resist Energy still active, reduces by 10", $"resist={result3.ResistanceApplied}");
        Assert(result3.FinalDamage == 15,
            "15 final damage (25 - 10 resist)", $"final={result3.FinalDamage}");

        DestroyController(target);
    }

    // ================================================================
    //  TEST: Duration (10 min/level or until discharged)
    // ================================================================
    private static void TestDuration()
    {
        Debug.Log("--- Test: Duration ---");

        ProtectionFromEnergyEffectData effect = new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Electricity,
            MaxAbsorptionPoints = 60,
            RemainingAbsorptionPoints = 60,
            DurationRemainingRounds = 5, // 5 rounds remaining
            CasterLevel = 5
        };

        Assert(effect.IsActive, "Effect is active initially");

        // Simulate rounds ticking
        effect.DurationRemainingRounds--;
        Assert(effect.IsActive, "Active at 4 rounds");

        effect.DurationRemainingRounds--;
        Assert(effect.IsActive, "Active at 3 rounds");

        effect.DurationRemainingRounds--;
        Assert(effect.IsActive, "Active at 2 rounds");

        effect.DurationRemainingRounds--;
        Assert(effect.IsActive, "Active at 1 round");

        effect.DurationRemainingRounds--;
        Assert(!effect.IsActive, "Expired at 0 rounds");

        // Test early discharge
        ProtectionFromEnergyEffectData effect2 = new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Sonic,
            MaxAbsorptionPoints = 12,
            RemainingAbsorptionPoints = 12,
            DurationRemainingRounds = 1000,
            CasterLevel = 1
        };

        Assert(effect2.IsActive, "Effect2 is active");
        effect2.AbsorbDamage(12);
        Assert(effect2.IsDischarged, "Discharged after absorbing all points");
        Assert(!effect2.IsActive, "Not active after discharge (even with duration remaining)");
    }

    // ================================================================
    //  TEST: EffectData Methods
    // ================================================================
    private static void TestEffectDataMethods()
    {
        Debug.Log("--- Test: EffectData Methods ---");

        ProtectionFromEnergyEffectData effect = new ProtectionFromEnergyEffectData
        {
            EnergyType = ResistEnergyType.Fire,
            MaxAbsorptionPoints = 120,
            RemainingAbsorptionPoints = 120,
            DurationRemainingRounds = 1000,
            CasterLevel = 10
        };

        // Test ToDamageType
        Assert(effect.ToDamageType() == DamageType.Fire, "Fire maps to DamageType.Fire");

        // Test AbsorbDamage with 0
        int absorbed0 = effect.AbsorbDamage(0);
        Assert(absorbed0 == 0, "0 damage absorbed returns 0");
        Assert(effect.RemainingAbsorptionPoints == 120, "No change for 0 damage");

        // Test AbsorbDamage with negative
        int absorbedNeg = effect.AbsorbDamage(-5);
        Assert(absorbedNeg == 0, "Negative damage absorbed returns 0");

        // Test partial absorption
        int absorbed50 = effect.AbsorbDamage(50);
        Assert(absorbed50 == 50, "50 absorbed from 120 pool");
        Assert(effect.RemainingAbsorptionPoints == 70, "70 remaining");

        // Test all energy type mappings
        var typeMap = new Dictionary<ResistEnergyType, DamageType>
        {
            { ResistEnergyType.Acid, DamageType.Acid },
            { ResistEnergyType.Cold, DamageType.Cold },
            { ResistEnergyType.Electricity, DamageType.Electricity },
            { ResistEnergyType.Fire, DamageType.Fire },
            { ResistEnergyType.Sonic, DamageType.Sonic }
        };

        foreach (var kvp in typeMap)
        {
            var testEffect = new ProtectionFromEnergyEffectData { EnergyType = kvp.Key };
            Assert(testEffect.ToDamageType() == kvp.Value,
                $"{kvp.Key} maps to {kvp.Value}",
                $"got {testEffect.ToDamageType()}");
        }
    }
}
}

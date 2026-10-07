using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tests.Services
{
/// <summary>
/// Unit tests for ConcentrationService — verifies DC formulas for defensive
/// casting, damage, grappling, entanglement, vigorous/violent motion, casting
/// while concentrating, and success chance math.
/// Run with ConcentrationServiceTests.RunAll().
///
/// PHB 3.5e References:
///   - Defensive Casting: DC = 15 + spell level (p.170)
///   - Damage: DC = 10 + damage dealt + spell level (p.170)
///   - Grappled/Pinned: DC = 20 + spell level (p.170)
///   - Entangled: DC = 15 + spell level (p.170)
///   - Vigorous Motion: DC = 10 + spell level (p.170)
///   - Violent Motion: DC = 15 + spell level (p.170)
///   - Casting while concentrating: DC = 15 + new spell level (p.170)
///   - Natural 1 always fails, natural 20 always succeeds on ability checks
///   - Casting while threatened (p.140): provokes AoOs unless cast defensively;
///     a failed defensive check or a failed damage check loses the spell (SPL-006)
/// </summary>
public static class ConcentrationServiceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== CONCENTRATION SERVICE TESTS ======");

        // DC formula tests
        TestDefensiveCastingDC();
        TestDefensiveCastingDC_Cantrip();
        TestDamageDC();
        TestDamageDC_MinimalDamage();
        TestGrappledCastingDC();
        TestEntangledCastingDC();
        TestVigorousMotionDC();
        TestViolentMotionDC();
        TestCastingWhileConcentratingDC();

        // Success chance tests
        TestSuccessChancePercent_AutoSuccess();
        TestSuccessChancePercent_AutoFail();
        TestSuccessChancePercent_MidRange();
        TestSuccessChancePercent_Exact50();
        TestSuccessChanceFraction_Range();

        // Casting while threatened, shared by the PC prompt and the NPC cast path (SPL-006)
        TestAIDefensiveCastingChoice();
        TestThreatenedSpellcastResolution();
        TestLargeCasterThreatenedOnAnyFootprintSquare();

        Debug.Log($"====== ConcentrationService Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition) { _passed++; Debug.Log($"  ✅ {testName}"); }
        else { _failed++; Debug.LogError($"  ❌ {testName} {detail}"); }
    }

    // ──────────────────────────────────────────────
    //  DC formulas — pure functions
    // ──────────────────────────────────────────────

    private static void TestDefensiveCastingDC()
    {
        // DC = 15 + spellLevel; level 3 => 18
        int dc = ConcentrationService.GetDefensiveCastingDC(3);
        Assert(dc == 18, "DefensiveDC(3)==18", $"got {dc}");
    }

    private static void TestDefensiveCastingDC_Cantrip()
    {
        // DC = 15 + 0 = 15 for cantrips
        int dc = ConcentrationService.GetDefensiveCastingDC(0);
        Assert(dc == 15, "DefensiveDC(0)==15 (cantrip)", $"got {dc}");
    }

    private static void TestDamageDC()
    {
        // DC = 10 + damage + spellLevel; 12 damage, level 2 => 24
        int dc = ConcentrationService.GetDamageDC(12, 2);
        Assert(dc == 24, "DamageDC(12,2)==24", $"got {dc}");
    }

    private static void TestDamageDC_MinimalDamage()
    {
        // DC = 10 + 1 + 0 = 11 (1 damage, cantrip)
        int dc = ConcentrationService.GetDamageDC(1, 0);
        Assert(dc == 11, "DamageDC(1,0)==11 (min damage cantrip)", $"got {dc}");
    }

    private static void TestGrappledCastingDC()
    {
        // DC = 20 + spellLevel; level 4 => 24
        int dc = ConcentrationService.GetGrappledCastingDC(4);
        Assert(dc == 24, "GrappledDC(4)==24", $"got {dc}");
    }

    private static void TestEntangledCastingDC()
    {
        // DC = 15 + spellLevel; level 2 => 17
        int dc = ConcentrationService.GetEntangledCastingDC(2);
        Assert(dc == 17, "EntangledDC(2)==17", $"got {dc}");
    }

    private static void TestVigorousMotionDC()
    {
        // DC = 10 + spellLevel; level 5 => 15
        int dc = ConcentrationService.GetVigorousMotionDC(5);
        Assert(dc == 15, "VigorousDC(5)==15", $"got {dc}");
    }

    private static void TestViolentMotionDC()
    {
        // DC = 15 + spellLevel; level 3 => 18
        int dc = ConcentrationService.GetViolentMotionDC(3);
        Assert(dc == 18, "ViolentDC(3)==18", $"got {dc}");
    }

    private static void TestCastingWhileConcentratingDC()
    {
        // DC = 15 + newSpellLevel; casting a 5th-level spell while concentrating => 20
        int dc = ConcentrationService.GetCastingWhileConcentratingDC(5);
        Assert(dc == 20, "CastingWhileConcentrating(5)==20", $"got {dc}");
    }

    // ──────────────────────────────────────────────
    //  Success chance — clamped to [5%, 95%]
    //  Formula: (21 - (dc - bonus)) / 20 * 100
    // ──────────────────────────────────────────────

    private static void TestSuccessChancePercent_AutoSuccess()
    {
        // bonus +30 vs DC 10: need -20 on d20, always succeed => capped at 95%
        float pct = ConcentrationService.CalculateSuccessChancePercent(30, 10);
        Assert(pct >= 95f, "SuccessChance_AutoSuccess>=95", $"got {pct}");
    }

    private static void TestSuccessChancePercent_AutoFail()
    {
        // bonus -5 vs DC 30: need 35 on d20, impossible => capped at 5%
        float pct = ConcentrationService.CalculateSuccessChancePercent(-5, 30);
        Assert(pct <= 5f, "SuccessChance_AutoFail<=5", $"got {pct}");
    }

    private static void TestSuccessChancePercent_MidRange()
    {
        // bonus +10 vs DC 15: need 5+ on d20
        // Success = (21 - 5) / 20 * 100 = 80%
        float pct = ConcentrationService.CalculateSuccessChancePercent(10, 15);
        Assert(pct > 70f && pct < 90f, "SuccessChance_MidRange~80%", $"got {pct}");
    }

    private static void TestSuccessChancePercent_Exact50()
    {
        // bonus +0 vs DC 11: need 11+ on d20
        // Success = (21 - 11) / 20 * 100 = 50%
        float pct = ConcentrationService.CalculateSuccessChancePercent(0, 11);
        Assert(pct >= 45f && pct <= 55f, "SuccessChance_Exact50~50%", $"got {pct}");
    }

    private static void TestSuccessChanceFraction_Range()
    {
        // Fraction should be between 0.05 and 0.95
        float frac = ConcentrationService.CalculateSuccessChanceFraction(10, 15);
        Assert(frac >= 0.05f && frac <= 0.95f, "SuccessFraction in [0.05, 0.95]", $"got {frac}");

        // High bonus => should be near 0.95
        float high = ConcentrationService.CalculateSuccessChanceFraction(30, 10);
        Assert(high >= 0.90f, "SuccessFraction_High>=0.90", $"got {high}");

        // Low bonus => should be near 0.05
        float low = ConcentrationService.CalculateSuccessChanceFraction(-5, 30);
        Assert(low <= 0.10f, "SuccessFraction_Low<=0.10", $"got {low}");
    }

    // ──────────────────────────────────────────────
    //  Casting while threatened (SPL-006)
    //  PHB p.140: casting provokes from each threatening enemy unless cast
    //  defensively (Concentration DC 15 + spell level, failure loses the spell).
    //  GetSpellcastingConcentrationBonus = caster level + CON mod (+4 Combat Casting).
    // ──────────────────────────────────────────────

    private static CharacterStats BuildCasterStats(string name, string className, int level, int con)
    {
        return new CharacterStats(
            name: name,
            level: level,
            characterClass: className,
            str: 10,
            dex: 10,
            con: con,
            wis: 10,
            intelligence: 10,
            cha: 10,
            bab: Mathf.Max(1, level / 2),
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human");
    }

    private static CharacterController CreateController(CharacterStats stats, CharacterTeam team, Vector2Int gridPos)
    {
        // No InventoryComponent: an unequipped character threatens unarmed (CharacterEquipment.HasMeleeWeaponEquipped).
        GameObject go = new GameObject($"ConcentrationTest_{stats.CharacterName}");
        CharacterController controller = go.AddComponent<CharacterController>();
        controller.Stats = stats;
        controller.SetTeam(team);
        controller.GridPosition = gridPos;
        StatusEffectManager statusMgr = go.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);
        return controller;
    }

    private static void DestroyTestObject(Component component)
    {
        if (component != null && component.gameObject != null)
            Object.DestroyImmediate(component.gameObject);
    }

    private static SpellData BuildTestSpell(int level, SpellEffectType effectType)
    {
        // A fresh instance, never a SpellDatabase template.
        return new SpellData
        {
            SpellId = $"spl006_test_level_{level}_{effectType}",
            Name = $"SPL-006 Test Spell {level}",
            SpellLevel = level,
            EffectType = effectType
        };
    }

    private static void TestAIDefensiveCastingChoice()
    {
        ClassRegistry.Init();
        CharacterController strong = null;
        CharacterController weak = null;

        try
        {
            // Wizard 20, CON 30: bonus 20 + 10 = 30 vs DC 19 never fails.
            strong = CreateController(BuildCasterStats("Archmage", "Wizard", 20, 30), CharacterTeam.Enemy, new Vector2Int(1, 1));
            // Wizard 1, CON 3: bonus 1 - 4 = -3 vs DC 19 never succeeds.
            weak = CreateController(BuildCasterStats("Apprentice", "Wizard", 1, 3), CharacterTeam.Enemy, new Vector2Int(5, 5));

            SpellData attackSpell = BuildTestSpell(4, SpellEffectType.Damage);
            SpellData healSpell = BuildTestSpell(4, SpellEffectType.Healing);

            Assert(AISpellcastingStrategist.ShouldCastDefensively(strong, attackSpell),
                "AI casts defensively when the DC 15 + level check is likely");
            Assert(!AISpellcastingStrategist.ShouldCastDefensively(weak, attackSpell),
                "AI casts normally (provokes) when the defensive check is hopeless");

            weak.Stats.CurrentHP = 1;
            Assert(weak.Stats.TotalMaxHP >= 4, "Fixture: weak caster is at 25% HP or less", $"max HP {weak.Stats.TotalMaxHP}");
            Assert(AISpellcastingStrategist.ShouldCastDefensively(weak, healSpell),
                "AI risks casting a Healing spell defensively at 25% HP or less");

            // Regression: no adjacent enemy still means cast normally (2).
            int noThreat = AISpellcastingStrategist.EvaluateDefensiveCasting(strong, attackSpell, new List<CharacterController> { strong });
            Assert(noThreat == 2, "EvaluateDefensiveCasting: no adjacent enemy = cast normally", $"got {noThreat}");
        }
        catch (System.Exception ex)
        {
            Assert(false, "AI defensive casting choice ran without exceptions", ex.Message);
        }
        finally
        {
            DestroyTestObject(strong);
            DestroyTestObject(weak);
        }
    }

    private static void TestThreatenedSpellcastResolution()
    {
        // GameManager.ResolveThreatenedSpellcast is the shared PC/NPC core: the PC prompt's
        // buttons and the NPC cast path (ResolveNPCSpellcastProvocation) both call it.
        MethodInfo resolve = typeof(GameManager).GetMethod("ResolveThreatenedSpellcast", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo npcResolve = typeof(GameManager).GetMethod("ResolveNPCSpellcastProvocation", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert(resolve != null, "GameManager.ResolveThreatenedSpellcast exists");
        Assert(npcResolve != null, "GameManager.ResolveNPCSpellcastProvocation exists (NPC cast path)");
        if (resolve == null) return;

        ClassRegistry.Init();
        GameObject gmObject = new GameObject("ConcentrationTest_GameManager");
        GameManager gm = gmObject.AddComponent<GameManager>();
        CharacterController strong = null;
        CharacterController weak = null;
        CharacterController guard = null;

        try
        {
            strong = CreateController(BuildCasterStats("Archmage", "Wizard", 20, 30), CharacterTeam.Enemy, new Vector2Int(1, 1));
            weak = CreateController(BuildCasterStats("Apprentice", "Wizard", 1, 3), CharacterTeam.Enemy, new Vector2Int(1, 2));
            guard = CreateController(BuildCasterStats("Guard", "Fighter", 4, 14), CharacterTeam.Player, new Vector2Int(2, 1));
            SpellData spell = BuildTestSpell(4, SpellEffectType.Damage);
            var threats = new List<CharacterController> { guard };

            object noThreat = resolve.Invoke(gm, new object[] { weak, spell, new List<CharacterController>(), false });
            Assert(noThreat is bool nt && nt, "Unthreatened cast proceeds without a check");

            guard.Stats.AttacksOfOpportunityUsed = 0;
            object strongDefensive = resolve.Invoke(gm, new object[] { strong, spell, threats, true });
            Assert(strongDefensive is bool sd && sd, "Defensive cast proceeds on a made Concentration check");
            Assert(guard.Stats.AttacksOfOpportunityUsed == 0, "Defensive cast provokes no AoO",
                $"AoOs used {guard.Stats.AttacksOfOpportunityUsed}");

            object weakDefensive = resolve.Invoke(gm, new object[] { weak, spell, threats, true });
            Assert(weakDefensive is bool wd && !wd, "Failed defensive Concentration check loses the spell");
            Assert(guard.Stats.AttacksOfOpportunityUsed == 0, "Failed defensive cast still provokes no AoO",
                $"AoOs used {guard.Stats.AttacksOfOpportunityUsed}");

            // Regression for SPL-006: casting normally while threatened provokes an AoO.
            resolve.Invoke(gm, new object[] { strong, spell, threats, false });
            Assert(guard.Stats.AttacksOfOpportunityUsed == 1, "Normal cast while threatened provokes an AoO",
                $"AoOs used {guard.Stats.AttacksOfOpportunityUsed}");

            // A threatener that cannot make AoOs leaves the spell to resolve.
            guard.Stats.AttacksOfOpportunityUsed = 0;
            guard.Stats.CanMakeAttacksOfOpportunity = false;
            object noAoO = resolve.Invoke(gm, new object[] { weak, spell, threats, false });
            Assert(noAoO is bool na && na, "Normal cast proceeds when no threatener can make an AoO");
            Assert(guard.Stats.AttacksOfOpportunityUsed == 0, "No AoO is spent by a threatener that cannot make one");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
            Assert(false, "Threatened spellcast resolution ran without exceptions", inner.Message);
        }
        finally
        {
            DestroyTestObject(strong);
            DestroyTestObject(weak);
            DestroyTestObject(guard);
            Object.DestroyImmediate(gmObject);
        }
    }

    private static void TestLargeCasterThreatenedOnAnyFootprintSquare()
    {
        // GameManager.GetThreateningEnemiesForSpellcasting feeds both the PC prompt and the NPC
        // cast path. A Large caster at (2,2) fills (2,2)-(3,3); a Medium foe at (4,3) is adjacent
        // to (3,3) but two squares from the base square, and must still count as threatening.
        MethodInfo getThreats = typeof(GameManager).GetMethod("GetThreateningEnemiesForSpellcasting", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert(getThreats != null, "GameManager.GetThreateningEnemiesForSpellcasting exists");
        if (getThreats == null) return;

        ClassRegistry.Init();
        GameObject gmObject = new GameObject("ConcentrationTest_GameManager_Footprint");
        GameManager gm = gmObject.AddComponent<GameManager>();
        CharacterController ogreMage = null;
        CharacterController guard = null;

        try
        {
            CharacterStats casterStats = BuildCasterStats("Large Caster", "Wizard", 5, 14);
            casterStats.SetBaseSizeCategory(SizeCategory.Large);
            ogreMage = CreateController(casterStats, CharacterTeam.Enemy, new Vector2Int(2, 2));
            guard = CreateController(BuildCasterStats("Guard", "Fighter", 4, 14), CharacterTeam.Player, new Vector2Int(4, 3));
            gm.NPCs.Add(ogreMage);
            gm.PCs.Add(guard);

            var threats = getThreats.Invoke(gm, new object[] { ogreMage }) as List<CharacterController>;
            Assert(threats != null && threats.Contains(guard),
                "A foe adjacent to a non-base square of a Large caster threatens the cast",
                $"threats {(threats == null ? "null" : threats.Count.ToString())}");
            Assert(threats != null && threats.Count == 1, "Each threatener is listed once",
                $"threats {(threats == null ? "null" : threats.Count.ToString())}");
        }
        catch (System.Exception ex)
        {
            System.Exception inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
            Assert(false, "Large caster threat footprint ran without exceptions", inner.Message);
        }
        finally
        {
            gm.NPCs.Clear();
            gm.PCs.Clear();
            DestroyTestObject(ogreMage);
            DestroyTestObject(guard);
            Object.DestroyImmediate(gmObject);
        }
    }
}
}

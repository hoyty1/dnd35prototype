using System.Collections.Generic;
using System.Reflection;
using DND35e.Identifiers;
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
///   - Casting while concentrating: no check; a caster cannot cast while concentrating, so casting ends the
///     maintained spell (p.176; decision D1, batch 11 step 16; the old DC 15 + level check had no source)
///   - Maintaining a concentration spell: a standard action each later turn (p.176; ConcentrationRules, SPL-153)
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

        // The one concentration rule (batch 11 step 16; decisions D1 and D2, SPL-153)
        TestConcentrationTailAndLimitData();
        TestCastEndsConcentrationBySource();
        TestMaintainLifecycle();
        TestTailKeepsEffect();
        TestCasterWhoCannotActCannotMaintain();
        TestPinnedOrParalyzedCasterMaintains(CharacterTeam.Player);
        TestPinnedOrParalyzedCasterMaintains(CharacterTeam.Enemy);
        TestDefaultAiMaintainChoice();

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

    // ──────────────────────────────────────────────
    //  The one concentration rule (ConcentrationRules; batch 11 step 16, decisions D1 and D2, SPL-153; PHB p.176):
    //  casting ends the maintained spell with no roll, maintaining costs a standard action on each later turn, a turn
    //  that ends without it ends the spell, a "+N rounds" spell runs its tail, an "up to X" spell ends at its limit.
    // ──────────────────────────────────────────────

    private static void TestConcentrationTailAndLimitData()
    {
        int Tail(string id, int cl) => ConcentrationRules.TailRounds(SpellDatabase.GetSpell(id), cl);
        int Max(string id, int cl) => ConcentrationRules.MaxRounds(SpellDatabase.GetSpell(id), cl);
        Assert(Tail(SpellNames.HYPNOTIC_PATTERN, 5) == 2, "Hypnotic Pattern: concentration + 2 rounds (PHB p.242)", $"got {Tail(SpellNames.HYPNOTIC_PATTERN, 5)}");
        Assert(Tail(SpellNames.MINOR_IMAGE, 5) == 2, "Minor Image: concentration + 2 rounds (PHB p.254)", $"got {Tail(SpellNames.MINOR_IMAGE, 5)}");
        Assert(Tail(SpellNames.SUMMON_SWARM, 5) == 2, "Summon Swarm: concentration + 2 rounds (PHB p.289)", $"got {Tail(SpellNames.SUMMON_SWARM, 5)}");
        Assert(Tail(SpellNames.RAGE, 5) == 5 && Tail(SpellNames.LULLABY, 3) == 3 && Tail(SpellNames.RAINBOW_PATTERN, 7) == 7,
            "Rage, Lullaby and Rainbow Pattern: concentration + 1 round/level (PHB p.268, p.249)",
            $"{Tail(SpellNames.RAGE, 5)} {Tail(SpellNames.LULLABY, 3)} {Tail(SpellNames.RAINBOW_PATTERN, 7)}");
        Assert(Tail(SpellNames.SILENT_IMAGE, 5) == 0 && Tail(SpellNames.MAGE_HAND, 5) == 0 && Tail(SpellNames.DETECT_MAGIC, 5) == 0,
            "Silent Image, Mage Hand and Detect Magic have no tail (PHB p.279, p.249, p.219)");
        Assert(Max(SpellNames.CALM_EMOTIONS, 3) == 3, "Calm Emotions: concentration, up to 1 round/level (PHB p.207)", $"got {Max(SpellNames.CALM_EMOTIONS, 3)}");
        Assert(Max(SpellNames.DETECT_MAGIC, 2) == 20 && Max(SpellNames.DETECT_EVIL, 1) == 100,
            "Detect Magic up to 1 min./level, Detect Evil up to 10 min./level (PHB p.219, p.218)",
            $"{Max(SpellNames.DETECT_MAGIC, 2)} {Max(SpellNames.DETECT_EVIL, 1)}");
        Assert(Max(SpellNames.SILENT_IMAGE, 5) == 0 && Max(SpellNames.HYPNOTIC_PATTERN, 5) == 0, "Silent Image and Hypnotic Pattern have no 'up to' limit");
        Assert(ConcentrationRules.TailRounds(SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE), 5) == 0, "A spell without a Concentration duration has no tail");
    }

    private static void TestCastEndsConcentrationBySource()
    {
        bool Ends(CastSourceKind k) => ConcentrationRules.CastEndsConcentration(new CastSource { Kind = k });
        Assert(Ends(CastSourceKind.PreparedSlot) && Ends(CastSourceKind.SpontaneousSlot) && Ends(CastSourceKind.SpontaneousConversion)
               && Ends(CastSourceKind.HeldChargeHold) && Ends(CastSourceKind.TestPanel),
            "D1: casting from a slot, a spontaneous spell, a conversion, a hold or the F12 panel ends the concentration (PHB p.176)");
        Assert(Ends(CastSourceKind.Scroll), "D1: reading a scroll is casting its spell and ends the concentration (DMG p.213)");
        Assert(!Ends(CastSourceKind.HeldChargeDelivery), "D1: delivering a held charge is not a casting");
        Assert(!Ends(CastSourceKind.Wand), "A wand use does not end the concentration yet (open delegated question, SPL-114)");
    }

    private static CharacterController CreateConcentrator(string name, out ConcentrationManager conc, CharacterTeam team = CharacterTeam.Player)
    {
        // Init (not CreateController's bare Stats) so the condition manager exists and Stunned applies.
        GameObject go = new GameObject($"ConcentrationTest_{name}");
        CharacterController caster = go.AddComponent<CharacterController>();
        caster.Init(BuildCasterStats(name, "Wizard", 5, 12), new Vector2Int(1, 1), null, null);
        caster.SetTeam(team);
        if (caster.StatusEffectManager == null)
            go.AddComponent<StatusEffectManager>().Init(caster.Stats);
        conc = caster.Concentration;
        if (conc == null)
        {
            conc = go.AddComponent<ConcentrationManager>();
            conc.Init(caster.Stats, caster);
        }
        return caster;
    }

    private static void TestMaintainLifecycle()
    {
        ClassRegistry.Init();
        CharacterController caster = null;
        try
        {
            caster = CreateConcentrator("Maintainer", out ConcentrationManager conc);
            ActiveSpellEffect image = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.SILENT_IMAGE).Clone(), "Maintainer", 5);
            conc.BeginConcentration(image);
            Assert(conc.IsConcentrating && conc.MaintainedThisTurn, "D2: the casting turn counts as maintained (the casting was its standard action)");
            Assert(ConcentrationRules.OnCasterTurnEnded(caster) == "" && conc.IsConcentrating, "D2: the casting turn's end keeps the spell");

            ConcentrationRules.OnCasterCountReached(caster);
            Assert(ConcentrationRules.OwesMaintenance(caster) && ConcentrationRules.CanMaintain(caster, out _),
                "D2: the next turn owes the concentration, and a caster with its standard action can pay it");
            caster.Actions.Reset();
            string log = ConcentrationRules.Maintain(caster, out string reason);
            Assert(log != null && conc.MaintainedThisTurn && !caster.Actions.HasStandardAction,
                "D2: maintaining spends the standard action (PHB p.176, Table 8-2)", reason ?? log);
            Assert(!ConcentrationRules.CanMaintain(caster, out string again) && again == "Already concentrated this turn",
                "D2: one concentration per turn", again);
            Assert(ConcentrationRules.OnCasterTurnEnded(caster) == "" && conc.IsConcentrating && caster.StatusEffectManager.ActiveEffects.Contains(image),
                "D2: a maintained turn's end keeps the spell");

            ConcentrationRules.OnCasterCountReached(caster);
            caster.Actions.Reset();
            caster.CommitStandardAction(); // the standard action spent on something else (an attack)
            Assert(!ConcentrationRules.CanMaintain(caster, out string noAction) && noAction == "No standard action",
                "D2: a caster without its standard action cannot maintain", noAction);
            string lapse = ConcentrationRules.OnCasterTurnEnded(caster);
            Assert(!conc.IsConcentrating && !caster.StatusEffectManager.ActiveEffects.Contains(image) && lapse.Contains("it ends"),
                "D2: a turn that ends without the concentration ends Silent Image (no tail)", lapse);
        }
        catch (System.Exception ex)
        {
            Assert(false, "Concentration maintenance lifecycle ran without exceptions", ex.Message);
        }
        finally
        {
            DestroyTestObject(caster);
        }
    }

    private static void TestTailKeepsEffect()
    {
        ClassRegistry.Init();
        CharacterController caster = null;
        try
        {
            caster = CreateConcentrator("Tail Caster", out ConcentrationManager conc);
            ActiveSpellEffect image = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.MINOR_IMAGE).Clone(), "Tail Caster", 5);
            conc.BeginConcentration(image);
            string log = conc.CeaseConcentration("to cast Magic Missile");
            Assert(!conc.IsConcentrating && caster.StatusEffectManager.ActiveEffects.Contains(image) && image.RemainingRounds == 2
                   && image.DurationAnchor == caster && log.Contains("2 more rounds"),
                "D1: ceasing to concentrate on Minor Image leaves it 2 more rounds at the caster's count (PHB p.254, p.176)",
                $"{image.RemainingRounds} {log}");
            bool first = image.Tick();
            bool second = image.Tick();
            Assert(!first && second, "The 2-round tail expires at the second tick", $"{first} {second} {image.RemainingRounds}");
            caster.StatusEffectManager.RemoveEffect(image);

            ActiveSpellEffect again = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.MINOR_IMAGE).Clone(), "Tail Caster", 5);
            conc.BeginConcentration(again);
            conc.EndConcentration(silent: true);
            Assert(!caster.StatusEffectManager.ActiveEffects.Contains(again), "EndConcentration (the slot reuse) removes the effect with no tail");

            ActiveSpellEffect calm = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.CALM_EMOTIONS).Clone(), "Tail Caster", 2);
            conc.BeginConcentration(calm);
            Assert(conc.MaxRoundsForCurrent == 2, "Calm Emotions at caster level 2: up to 2 rounds", $"{conc.MaxRoundsForCurrent}");
            conc.OnOwnCountReached();
            Assert(conc.IsConcentrating, "One round in, Calm Emotions goes on");
            string limit = conc.OnOwnCountReached();
            Assert(!conc.IsConcentrating && !caster.StatusEffectManager.ActiveEffects.Contains(calm) && limit.Contains("limit"),
                "At its 'up to 2 rounds' limit Calm Emotions ends (PHB p.176, p.207)", limit);
        }
        catch (System.Exception ex)
        {
            Assert(false, "Concentration tail and limit ran without exceptions", ex.Message);
        }
        finally
        {
            DestroyTestObject(caster);
        }
    }

    private static void TestCasterWhoCannotActCannotMaintain()
    {
        ClassRegistry.Init();
        CharacterController caster = null;
        try
        {
            caster = CreateConcentrator("Dazed Caster", out ConcentrationManager conc);
            ActiveSpellEffect image = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.SILENT_IMAGE).Clone(), "Dazed Caster", 5);
            conc.BeginConcentration(image);
            conc.OnOwnCountReached();
            caster.Actions.Reset();
            caster.ApplyCondition(CombatConditionType.Stunned, 2, "test");
            bool stunned = caster.HasCondition(CombatConditionType.Stunned);
            bool canMaintain = ConcentrationRules.CanMaintain(caster, out string reason);
            Assert(stunned && !canMaintain && reason != null && reason.Contains("no standard action"),
                "D2: a stunned caster cannot maintain (no standard action)", (stunned ? "" : "not stunned; ") + reason);
            ConcentrationRules.OnCasterTurnEnded(caster);
            Assert(!conc.IsConcentrating, "D2: the stunned caster's spell ends at the end of its turn");
        }
        catch (System.Exception ex)
        {
            Assert(false, "A caster who cannot act ran without exceptions", ex.Message);
        }
        finally
        {
            DestroyTestObject(caster);
        }
    }

    /// <summary>
    /// Delegated RAW readings 2026-10-15 (ConcentrationRules.StandardActionBar): a pinned caster may maintain (PHB p.156
    /// lets it cast), and so may a paralyzed one (purely mental actions, PHB p.311), on the turn the turn-skip gate skips
    /// (CMB-075) too; an asleep caster may not. One rule for a PC and an NPC.
    /// </summary>
    private static void TestPinnedOrParalyzedCasterMaintains(CharacterTeam team)
    {
        ClassRegistry.Init();
        string side = team == CharacterTeam.Player ? "PC" : "NPC";
        CharacterController caster = null;
        try
        {
            caster = CreateConcentrator($"Held {side}", out ConcentrationManager conc, team);
            ActiveSpellEffect image = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.SILENT_IMAGE).Clone(), $"Held {side}", 5);
            conc.BeginConcentration(image);

            conc.OnOwnCountReached();
            caster.Actions.Reset();
            caster.ApplyCondition(CombatConditionType.Pinned, 2, "test");
            bool pinned = caster.HasCondition(CombatConditionType.Pinned);
            bool canPinned = ConcentrationRules.CanMaintain(caster, out string pinnedReason);
            Assert(pinned && canPinned, $"{side}: a pinned caster may maintain its spell (PHB p.156; delegated RAW reading 2026-10-15)",
                (pinned ? "" : "not pinned; ") + pinnedReason);
            string pinnedLog = ConcentrationRules.MaintainOnSkippedTurn(caster);
            Assert(pinnedLog != null && conc.MaintainedThisTurn && ConcentrationRules.OnCasterTurnEnded(caster) == "" && conc.IsConcentrating,
                $"{side}: on its skipped turn (CMB-075) a pinned caster keeps its spell up", pinnedLog);
            caster.RemoveCondition(CombatConditionType.Pinned);

            conc.OnOwnCountReached();
            caster.Actions.Reset();
            caster.ApplyCondition(CombatConditionType.Paralyzed, 2, "test");
            caster.ApplyCondition(CombatConditionType.Helpless, 2, "test");
            bool paralyzed = caster.HasCondition(CombatConditionType.Paralyzed) && caster.HasCondition(CombatConditionType.Helpless);
            bool canParalyzed = ConcentrationRules.CanMaintain(caster, out string paralyzedReason);
            Assert(paralyzed && canParalyzed, $"{side}: a paralyzed (helpless) caster may maintain, a purely mental action (PHB p.311; delegated RAW reading 2026-10-15)",
                (paralyzed ? "" : "not paralyzed; ") + paralyzedReason);
            string paralyzedLog = ConcentrationRules.MaintainOnSkippedTurn(caster);
            Assert(paralyzedLog != null && ConcentrationRules.OnCasterTurnEnded(caster) == "" && conc.IsConcentrating,
                $"{side}: on its skipped turn a paralyzed caster keeps its spell up", paralyzedLog);
            caster.RemoveCondition(CombatConditionType.Paralyzed);
            caster.RemoveCondition(CombatConditionType.Helpless);

            conc.OnOwnCountReached();
            caster.Actions.Reset();
            caster.ApplyCondition(CombatConditionType.Asleep, 2, "test");
            caster.ApplyCondition(CombatConditionType.Helpless, 2, "test");
            bool asleep = caster.HasCondition(CombatConditionType.Asleep);
            Assert(asleep && !ConcentrationRules.CanMaintain(caster, out _) && ConcentrationRules.MaintainOnSkippedTurn(caster) == null,
                $"{side}: an asleep (helpless) caster cannot maintain, even on its skipped turn");
            ConcentrationRules.OnCasterTurnEnded(caster);
            Assert(!conc.IsConcentrating, $"{side}: the asleep caster's spell ends at the end of its turn");
        }
        catch (System.Exception ex)
        {
            Assert(false, $"{side}: a pinned or paralyzed caster ran without exceptions", ex.Message);
        }
        finally
        {
            DestroyTestObject(caster);
        }
    }

    private static void TestDefaultAiMaintainChoice()
    {
        ClassRegistry.Init();
        CharacterController caster = null;
        CharacterController ally = null;
        CharacterController foe = null;
        try
        {
            caster = CreateConcentrator("AI Concentrator", out ConcentrationManager conc);
            ally = CreateController(BuildCasterStats("AI Ally", "Fighter", 5, 14), CharacterTeam.Player, new Vector2Int(2, 1));
            foe = CreateController(BuildCasterStats("AI Foe", "Fighter", 5, 14), CharacterTeam.Enemy, new Vector2Int(6, 1));
            var all = new List<CharacterController> { caster, ally, foe };
            bool Choose(int otherSpellLevel, bool canAttack, bool summon = false)
                => DND35.AI.AIProfile.DefaultShouldMaintainConcentration(caster, all, otherSpellLevel, canAttack, summon);

            ActiveSpellEffect image = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.SILENT_IMAGE).Clone(), "AI Concentrator", 5);
            conc.BeginConcentration(image);
            conc.OnOwnCountReached();
            caster.Actions.Reset();
            Assert(Choose(-1, false), "AI: with nothing better to do, the caster keeps its Silent Image up");
            Assert(!Choose(-1, true), "AI: an attack it can make now is worth more than an image on itself");
            Assert(!Choose(1, false), "AI: a spell it would cast is worth more than an image on itself (the cast ends it, D1)");
            Assert(!Choose(0, false), "AI: an image on itself is worth no more than a cantrip");
            conc.EndConcentration(silent: true);

            ActiveSpellEffect minor = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.MINOR_IMAGE).Clone(), "AI Concentrator", 5);
            conc.BeginConcentration(minor);
            conc.OnOwnCountReached();
            caster.Actions.Reset();
            Assert(Choose(-1, false), "AI: Minor Image (a 2-round tail) is still kept up when nothing else is worth the action");
            Assert(!Choose(-1, true), "AI: but an attack is worth more than Minor Image, whose tail keeps it 2 more rounds");
            conc.EndConcentration(silent: true);

            ActiveSpellEffect swarm = caster.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.SUMMON_SWARM).Clone(), "AI Concentrator", 5);
            conc.BeginConcentration(swarm);
            conc.OnOwnCountReached();
            caster.Actions.Reset();
            Assert(Choose(-1, false, summon: true), "AI: with nothing better to do, the caster keeps its Summon Swarm up");
            Assert(!Choose(1, false, summon: true), "AI: a spell is worth more than a swarm whose tail keeps it 2 more rounds");
            conc.EndConcentration(silent: true);

            ActiveSpellEffect lullaby = ally.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.LULLABY).Clone(), "AI Concentrator", 1);
            conc.BeginConcentration(lullaby);
            conc.OnOwnCountReached();
            caster.Actions.Reset();
            Assert(Choose(-1, true), "AI: a spell on another creature with a 1-round tail is worth more than an attack");
            Assert(Choose(0, true), "AI: and more than a cantrip");
            Assert(!Choose(1, true), "AI: but not more than a spell of 1st level or higher it would cast");
            conc.EndConcentration(silent: true);

            ActiveSpellEffect calm = foe.StatusEffectManager.AddEffect(SpellDatabase.GetSpell(SpellNames.CALM_EMOTIONS).Clone(), "AI Concentrator", 5);
            conc.BeginConcentration(calm);
            conc.OnOwnCountReached();
            caster.Actions.Reset();
            Assert(Choose(2, true), "AI: a spell that holds a foe (Calm Emotions, no tail) is worth more than another spell it would cast");
            conc.EndConcentration(silent: true);
        }
        catch (System.Exception ex)
        {
            Assert(false, "The AI's maintain choice ran without exceptions", ex.Message);
        }
        finally
        {
            DestroyTestObject(caster);
            DestroyTestObject(ally);
            DestroyTestObject(foe);
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

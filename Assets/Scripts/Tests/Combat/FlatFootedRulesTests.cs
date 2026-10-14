using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Tests.Utilities;

namespace Tests.Combat
{
/// <summary>
/// CMB-028: flat-footed at the start of a battle. PHB p.137 (Initiative, Flat-Footed): before its first regular turn in
/// the initiative order a creature is flat-footed: it cannot use its Dexterity bonus to AC (a penalty stays) and cannot
/// make attacks of opportunity. Uncanny dodge (barbarian 2nd level, PHB p.26; rogue 4th level, PHB p.50) keeps the
/// Dexterity bonus; Combat Reflexes (PHB p.92) allows attacks of opportunity while flat-footed. Losing the Dexterity
/// bonus also loses dodge bonuses (PHB p.93, Dodge), here the Dodge feat and Haste's +1.
///
/// Checks: AC, touch AC and the spell touch AC (SpellcastingComponent.GetTouchAC) without the Dexterity bonus and with
/// no other penalty; a Dexterity penalty kept; uncanny dodge for barbarian 2 and rogue 4 but not barbarian 1 or rogue 3;
/// the dodge bonuses; the shared readers (CharacterConditions.IsDexDenied, AttackCalculator.ShouldDenyDexToAC);
/// ThreatSystem.CanMakeAoO with and without Combat Reflexes; and the combat start through TurnService and a
/// ConditionService wired as GameManager wires them: everyone flat-footed, each creature until its own first turn.
/// Also: the spell touch AC equals TouchArmorClass (Haste, Blinded); the attack-of-opportunity count and the Combat
/// Reflexes cap reset at the combat start (PHB p.92, CMB-164); a never-flat-footed creature (minotaur, MM p.189) and
/// the formian hive mind (MM p.108); the sneak attack test for the other Dexterity-denying conditions (PHB p.50).
/// Run with FlatFootedRulesTests.RunAll().
/// </summary>
public static class FlatFootedRulesTests
{
    private static int _passed;
    private static int _failed;
    private static int _nextX;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== FLAT-FOOTED RULES TESTS (CMB-028) ======");
        TestHelpers.EnsureCoreDatabasesInitialized();

        CharacterController savedAnchor = TurnDurations.CurrentAnchor;
        try
        {
            TestFlatFootedLosesDexBonusOnly();
            TestDexPenaltyKept();
            TestUncannyDodge();
            TestDodgeBonusesLost();
            TestSharedDexDeniedReaders();
            TestNoAttacksOfOpportunity();
            TestCombatStartThroughTurnOrder();
            TestSpellTouchAcMatchesTouchArmorClass();
            TestAoOCountResetAtCombatStart();
            TestNeverFlatFooted();
            TestHiveMind();
            TestSneakAttackDexDenialConditions();
        }
        finally
        {
            TurnDurations.SetCurrentAnchor(savedAnchor);
        }

        Debug.Log($"====== Flat-Footed Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterController Make(string name, string cls = "Fighter", int level = 3, int dex = 16)
    {
        _nextX += 2;
        return TestHelpers.CreateCharacter(name: name, characterClass: cls, level: level, dex: dex,
            gridPosition: new Vector2Int(_nextX % 18, 2 + (_nextX / 18) * 2));
    }

    private static void Destroy(params Object[] objects)
    {
        foreach (Object o in objects)
        {
            if (o == null) continue;
            if (o is Component c) Object.DestroyImmediate(c.gameObject);
            else Object.DestroyImmediate(o);
        }
    }

    private static void FlatFoot(CharacterController c) => c.ApplyConditionDirect(CombatConditionType.FlatFooted, -1, "FlatFootedTest");

    private static bool IsFlatFooted(CharacterController c) => c.HasConditionDirect(CombatConditionType.FlatFooted);

    // ── AC ──────────────────────────────────────────────────────────

    private static void TestFlatFootedLosesDexBonusOnly()
    {
        CharacterController c = Make("FF Fighter", dex: 16);
        try
        {
            CharacterStats s = c.Stats;
            int dex = s.DexToArmorClass;
            int ac = s.ArmorClass, touch = s.TouchArmorClass, spellTouch = SpellcastingComponent.GetTouchAC(s);
            Assert(dex == 3, "Setup: DEX 16 adds +3 to AC", "dex to AC " + dex);

            FlatFoot(c);
            Assert(s.DeniedDexToAcByCondition && s.IsDexDeniedByFlatFooted, "Flat-footed denies the Dexterity bonus to AC (PHB p.137)");
            Assert(s.ArmorClass == ac - 3, "Flat-footed AC is the AC without the +3 Dexterity bonus, and no other penalty (PHB p.137)",
                "AC " + ac + " -> " + s.ArmorClass);
            Assert(s.TouchArmorClass == touch - 3, "Flat-footed touch AC loses the Dexterity bonus", "touch " + touch + " -> " + s.TouchArmorClass);
            Assert(SpellcastingComponent.GetTouchAC(s) == spellTouch - 3, "The spell touch AC (SpellCaster.Cast) loses the Dexterity bonus too",
                "spell touch " + spellTouch + " -> " + SpellcastingComponent.GetTouchAC(s));

            c.RemoveConditionDirect(CombatConditionType.FlatFooted);
            Assert(s.ArmorClass == ac && s.TouchArmorClass == touch, "Once no longer flat-footed the Dexterity bonus is back");
        }
        finally
        {
            Destroy(c);
        }
    }

    private static void TestDexPenaltyKept()
    {
        CharacterController c = Make("FF Clumsy", dex: 6);
        try
        {
            CharacterStats s = c.Stats;
            int ac = s.ArmorClass, touch = s.TouchArmorClass;
            Assert(s.DexToArmorClass == -2, "Setup: DEX 6 gives -2 to AC", "dex to AC " + s.DexToArmorClass);
            FlatFoot(c);
            Assert(s.ArmorClass == ac && s.TouchArmorClass == touch,
                "A flat-footed creature with a Dexterity penalty keeps the penalty: only a bonus is lost (PHB p.137)",
                "AC " + ac + " -> " + s.ArmorClass + ", touch " + touch + " -> " + s.TouchArmorClass);
        }
        finally
        {
            Destroy(c);
        }
    }

    private static void TestUncannyDodge()
    {
        CheckUncannyDodge("Barbarian", 2, true, "PHB p.26");
        CheckUncannyDodge("Barbarian", 1, false, "PHB p.26");
        CheckUncannyDodge("Rogue", 4, true, "PHB p.50");
        CheckUncannyDodge("Rogue", 3, false, "PHB p.50");
    }

    private static void CheckUncannyDodge(string cls, int level, bool expected, string cite)
    {
        CharacterController c = Make("FF " + cls + " " + level, cls, level, dex: 16);
        try
        {
            CharacterStats s = c.Stats;
            Assert(s.HasUncannyDodge == expected, $"{cls} {level}: uncanny dodge {(expected ? "yes" : "no")} ({cite})");
            int ac = s.ArmorClass;
            FlatFoot(c);
            int want = expected ? ac : ac - 3;
            Assert(s.ArmorClass == want, $"Flat-footed {cls} {level}: AC {want} ({(expected ? "uncanny dodge keeps" : "loses")} the +3 Dexterity bonus, {cite}, PHB p.137)",
                "AC " + ac + " -> " + s.ArmorClass);
            Assert(s.IsDexDeniedByFlatFooted == !expected && c.IsDexDenied == !expected,
                $"Flat-footed {cls} {level}: denied its Dexterity bonus = {!expected} for the shared readers");
            Assert(IsFlatFooted(c), $"Flat-footed {cls} {level} is still flat-footed (uncanny dodge only keeps the Dexterity bonus)");
        }
        finally
        {
            Destroy(c);
        }
    }

    private static void TestDodgeBonusesLost()
    {
        CharacterController c = Make("FF Dodger", dex: 14);
        try
        {
            CharacterStats s = c.Stats;
            s.Feats.Add("Dodge");
            s.HasteACBonus = 1;
            int ac = s.ArmorClass, touch = s.TouchArmorClass;
            Assert(s.DodgeACBonus == 2, "Setup: the Dodge feat and Haste give +2 dodge to AC", "dodge " + s.DodgeACBonus);
            FlatFoot(c);
            Assert(s.ArmorClass == ac - 2 - 2, "Flat-footed loses the +2 Dexterity bonus and the +2 dodge bonuses (PHB p.93, p.137)",
                "AC " + ac + " -> " + s.ArmorClass);
            Assert(s.TouchArmorClass == touch - 2 - 2, "Flat-footed touch AC loses the dodge bonuses too", "touch " + touch + " -> " + s.TouchArmorClass);
            s.HasteACBonus = 0;
        }
        finally
        {
            Destroy(c);
        }
    }

    // ── Shared readers ──────────────────────────────────────────────

    private static void TestSharedDexDeniedReaders()
    {
        CharacterController attacker = Make("FF Attacker"), target = Make("FF Target"), barbarian = Make("FF Barbarian", "Barbarian", 2);
        try
        {
            Assert(!AttackCalculator.ShouldDenyDexToAC(attacker, target, isFlanking: false) && !target.IsDexDenied,
                "Not flat-footed: Dexterity not denied");
            FlatFoot(target);
            FlatFoot(barbarian);
            Assert(AttackCalculator.ShouldDenyDexToAC(attacker, target, isFlanking: false) && target.IsDexDenied,
                "Flat-footed: AttackCalculator and CharacterConditions deny the Dexterity bonus (the sneak attack test, PHB p.50)");
            Assert(!AttackCalculator.ShouldDenyDexToAC(attacker, barbarian, isFlanking: false) && !barbarian.IsDexDenied,
                "Flat-footed barbarian 2 with uncanny dodge: not denied, so no sneak attack for being flat-footed (PHB p.26)");
        }
        finally
        {
            Destroy(attacker, target, barbarian);
        }
    }

    private static void TestNoAttacksOfOpportunity()
    {
        CharacterController plain = Make("FF Guard"), reflexes = Make("FF Reflexes");
        try
        {
            reflexes.Stats.Feats.Add("Combat Reflexes");
            Assert(ThreatSystem.CanMakeAoO(plain) && ThreatSystem.CanMakeAoO(reflexes), "Setup: both can make attacks of opportunity");
            FlatFoot(plain);
            FlatFoot(reflexes);
            Assert(!ThreatSystem.CanMakeAoO(plain), "A flat-footed creature cannot make attacks of opportunity (PHB p.137)");
            Assert(ThreatSystem.CanMakeAoO(reflexes), "Combat Reflexes allows attacks of opportunity while flat-footed (PHB p.92)");
        }
        finally
        {
            Destroy(plain, reflexes);
        }
    }

    // ── Combat start ────────────────────────────────────────────────

    private static void TestCombatStartThroughTurnOrder()
    {
        CharacterController a = Make("FF Order A"), b = Make("FF Order B"), c = Make("FF Order C"), dead = Make("FF Order Dead");
        var host = new GameObject("FlatFootedTest_Services");
        TurnService turns = host.AddComponent<TurnService>();
        ConditionService service = host.AddComponent<ConditionService>();
        var order = new List<CharacterController> { a, b, c };
        var all = new List<CharacterController> { a, b, c, dead };
        var log = new List<string>();
        try
        {
            dead.Stats.CurrentHP = -10;
            service.Initialize(() => all);
            turns.OnInitiativeCountReached += service.OnInitiativeCountReached;
            turns.OnTurnStarted += ch =>
            {
                service.OnTurnStart(ch); // as GameManager.OnTurnStarted
                log.Add(turns.CurrentRound + ":" + ch.Stats.CharacterName + ":" + (IsFlatFooted(ch) ? "ff" : "-")
                    + (IsFlatFooted(a) ? "A" : "") + (IsFlatFooted(b) ? "B" : "") + (IsFlatFooted(c) ? "C" : ""));
            };

            service.ApplyCombatStartFlatFooted(all); // as GameManager.StartCombat, before the first turn
            Assert(IsFlatFooted(a) && IsFlatFooted(b) && IsFlatFooted(c), "At the start of a battle every combatant is flat-footed (PHB p.137)");
            Assert(!IsFlatFooted(dead), "A dead creature is not made flat-footed");

            turns.StartCombat(order, null, _ => true, order);
            turns.EndTurn();
            turns.EndTurn();
            turns.EndTurn();
            string got = string.Join(" ", log);
            string want = "1:FF Order A:-BC 1:FF Order B:-C 1:FF Order C:- 2:FF Order A:-";
            Assert(got == want, "Each creature stops being flat-footed at the start of its first turn, the others stay until theirs (PHB p.137)", "got: " + got);
            turns.EndCombat();
        }
        finally
        {
            TurnDurations.ClearCurrentAnchor();
            Destroy(a, b, c, dead, host);
        }
    }

    // ── Review follow-ups ───────────────────────────────────────────

    private static void TestSpellTouchAcMatchesTouchArmorClass()
    {
        CharacterController c = Make("FF Hasted", dex: 14);
        try
        {
            CharacterStats s = c.Stats;
            s.HasteACBonus = 1;
            Assert(SpellcastingComponent.GetTouchAC(s) == s.TouchArmorClass,
                "Hasted: the spell touch AC equals TouchArmorClass (Haste's +1 dodge counts against touch attacks, PHB p.239)",
                "spell " + SpellcastingComponent.GetTouchAC(s) + " vs touch " + s.TouchArmorClass);
            int hastedTouch = s.TouchArmorClass;
            FlatFoot(c);
            Assert(SpellcastingComponent.GetTouchAC(s) == s.TouchArmorClass && s.TouchArmorClass == hastedTouch - 2 - 1,
                "Hasted and flat-footed: both lose the +2 Dexterity bonus and Haste's +1 dodge bonus",
                "spell " + SpellcastingComponent.GetTouchAC(s) + ", touch " + s.TouchArmorClass + ", before " + hastedTouch);
            c.RemoveConditionDirect(CombatConditionType.FlatFooted);
            c.ApplyConditionDirect(CombatConditionType.Blinded, 3, "FlatFootedTest");
            Assert(SpellcastingComponent.GetTouchAC(s) == s.TouchArmorClass,
                "Blinded: the spell touch AC equals TouchArmorClass (the condition AC terms count)",
                "spell " + SpellcastingComponent.GetTouchAC(s) + " vs touch " + s.TouchArmorClass);
            c.RemoveConditionDirect(CombatConditionType.Blinded);
            s.HasteACBonus = 0;
        }
        finally
        {
            Destroy(c);
        }
    }

    private static void TestAoOCountResetAtCombatStart()
    {
        CharacterController reflexes = Make("FF Stale Reflexes", dex: 16);
        var host = new GameObject("FlatFootedTest_AoOReset");
        ConditionService service = host.AddComponent<ConditionService>();
        var all = new List<CharacterController> { reflexes };
        try
        {
            reflexes.Stats.Feats.Add("Combat Reflexes");
            reflexes.Stats.AttacksOfOpportunityUsed = 3;
            reflexes.Stats.MaxAttacksOfOpportunity = 1;
            Assert(!ThreatSystem.CanMakeAoO(reflexes), "Setup: AoOs spent at the end of the last battle");
            service.Initialize(() => all);
            service.ApplyCombatStartFlatFooted(all);
            Assert(IsFlatFooted(reflexes) && reflexes.Stats.AttacksOfOpportunityUsed == 0 && reflexes.Stats.MaxAttacksOfOpportunity == 4,
                "Combat start resets the AoO count and sets the Combat Reflexes cap to 1 + DEX before the first turn (PHB p.92)",
                "used " + reflexes.Stats.AttacksOfOpportunityUsed + ", max " + reflexes.Stats.MaxAttacksOfOpportunity);
            Assert(ThreatSystem.CanMakeAoO(reflexes), "So a flat-footed creature with Combat Reflexes can make its AoOs");
        }
        finally
        {
            Destroy(reflexes, host);
        }
    }

    private static void TestNeverFlatFooted()
    {
        CharacterController minotaur = Make("FF Minotaur"), other = Make("FF Other");
        var host = new GameObject("FlatFootedTest_Never");
        ConditionService service = host.AddComponent<ConditionService>();
        var all = new List<CharacterController> { minotaur, other };
        try
        {
            minotaur.Stats.CreatureTags.Add(CharacterStats.NeverFlatFootedTag);
            service.Initialize(() => all);
            service.ApplyCombatStartFlatFooted(all);
            Assert(!IsFlatFooted(minotaur) && IsFlatFooted(other),
                "A creature that is never caught flat-footed (minotaur natural cunning, MM p.189) is not flat-footed at the start");
            Assert(ThreatSystem.CanMakeAoO(minotaur), "So it can make attacks of opportunity before its first turn");

            NPCDefinition def = NPCDatabase.Get("minotaur");
            Assert(def != null && def.CreatureTags != null && def.CreatureTags.Contains(CharacterStats.NeverFlatFootedTag),
                "The MM minotaur carries the NeverFlatFooted tag");
        }
        finally
        {
            Destroy(minotaur, other, host);
        }
    }

    private static void TestHiveMind()
    {
        CharacterController f1 = Make("FF Formian 1"), f2 = Make("FF Formian 2"), f3 = Make("FF Formian 3"),
            ally = Make("FF Formian Ally"), foe = Make("FF Formian Foe");
        var host = new GameObject("FlatFootedTest_HiveMind");
        ConditionService service = host.AddComponent<ConditionService>();
        var all = new List<CharacterController> { f1, f2, f3, ally, foe };
        try
        {
            foreach (CharacterController f in new[] { f1, f2, f3, foe })
                f.Stats.CreatureTags.Add(CharacterStats.HiveMindTag);
            foreach (CharacterController e in new[] { f1, f2, f3, ally })
                e.SetTeam(CharacterTeam.Enemy);
            foe.SetTeam(CharacterTeam.Player);
            service.Initialize(() => all);
            service.ApplyCombatStartFlatFooted(all);
            Assert(IsFlatFooted(f1) && IsFlatFooted(f2) && IsFlatFooted(f3) && IsFlatFooted(ally) && IsFlatFooted(foe),
                "Hive mind: every formian is flat-footed while all of its group are");

            service.OnTurnStart(f1);
            Assert(!IsFlatFooted(f1) && !IsFlatFooted(f2) && !IsFlatFooted(f3),
                "Hive mind: once one formian of the group is not flat-footed, none of them are (MM p.108)");
            Assert(IsFlatFooted(ally) && IsFlatFooted(foe),
                "A non-formian ally and a formian on the other side stay flat-footed until their own turns");

            NPCDefinition worker = NPCDatabase.Get("formian_worker");
            Assert(worker != null && worker.CreatureTags != null && worker.CreatureTags.Contains(CharacterStats.HiveMindTag),
                "The MM formian worker carries the HiveMind tag");
        }
        finally
        {
            Destroy(f1, f2, f3, ally, foe, host);
        }
    }

    private static void TestSneakAttackDexDenialConditions()
    {
        CharacterController rogue = Make("FF Rogue", "Rogue", 3), target = Make("FF Sneak Target");
        try
        {
            MethodInfo gate = typeof(CharacterController).GetMethod("IsTargetDeniedDexForSneakAttack",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(gate != null, "Reflection: CharacterController.IsTargetDeniedDexForSneakAttack exists (TST-006)");
            if (gate == null)
                return;

            bool Denied()
            {
                object[] args = { target, true, false, null };
                return (bool)gate.Invoke(rogue, args);
            }

            Assert(!Denied(), "Sneak attack test: a plain target is not denied its Dexterity bonus");
            target.ApplyConditionDirect(CombatConditionType.Blinded, 3, "FlatFootedTest");
            Assert(Denied(), "Sneak attack test: a blinded target is denied its Dexterity bonus (DMG p.300, PHB p.50)");
            target.RemoveConditionDirect(CombatConditionType.Blinded);
            target.ApplyConditionDirect(CombatConditionType.Cowering, 3, "FlatFootedTest");
            Assert(Denied(), "Sneak attack test: a cowering target is denied its Dexterity bonus");
            target.RemoveConditionDirect(CombatConditionType.Cowering);
            target.ApplyConditionDirect(CombatConditionType.Feinted, -1, "Someone else");
            Assert(!Denied(), "Sneak attack test: another creature's feint mark does not count for this attacker");
            target.RemoveConditionDirect(CombatConditionType.Feinted);
        }
        finally
        {
            Destroy(rogue, target);
        }
    }
}
}

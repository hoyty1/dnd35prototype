#if UNITY_EDITOR
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 14 (docs/designs/cast_core_batch11.md, raw-resources-caster-level; SPL-017, SPL-016): the one caster-level
/// rule, CasterLevelRules. A class's caster level (PHB p.171), the paladin's and ranger's one-half from 4th level and none
/// before (PHB p.44, p.48), through CharacterStats.GetCasterLevel; a wizard; a cleric's domain boost (+1 for a spell of
/// the domain's descriptor); a paladin and a ranger at levels 3, 4 and 8, their durations
/// (SpellDurationRules.CasterLevelFor) following; a scroll's and a wand's own caster level whoever uses it (DMG p.237,
/// p.245), and an item caster level on a non-item source ignored; negative levels lowering the class level first (DMG
/// p.293; delegated RAW reading 2026-10-14: a paladin 4 with one negative level has no caster level, so casts at the
/// minimum 1); a creature with no casting class at its Hit Dice less its negative levels, plus a worn item's bonus (MM
/// p.315; delegated RAW reading 2026-10-14); the paladin's and ranger's casting Concentration proxy on the class level
/// (CHR-085); the range at the cast's caster level and the
/// range test to the target's nearest square (the PC highlight's measure, now the NPC range test's). With a scene
/// GameManager (Play mode): an open scroll cast gives its caster level to the readers with no request in hand
/// (CasterLevelRules.ForCaster, SpellCastingHelper.GetEffectiveCasterLevel, SpellDurationRules.CasterLevelFor) until it
/// ends. The class caster-level checks run in edit or Play mode; the rest build characters and are skipped (not failed)
/// without a scene GameManager.
/// </summary>
public static class CasterLevelRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== CASTER LEVEL RULES TESTS (batch 11 step 14) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();

        TestClassCasterLevel();

        // The creature checks build characters (CharacterController.Init needs the scene's grid): Play mode only.
        if (GameManager.Instance != null)
        {
            TestCreatures();
            TestItems();
            TestNegativeLevels();
            TestRange();
            TestOpenCast(GameManager.Instance);
        }
        else
            Debug.Log("  SKIP: creature, item, negative level, range and open-cast checks (no scene GameManager; they run in the Play pass)");

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
    }

    private static void TestClassCasterLevel()
    {
        Assert(CasterLevelRules.ClassCasterLevel("Wizard", 5) == 5, "A wizard's caster level is the class level (PHB p.171)");
        Assert(CasterLevelRules.ClassCasterLevel("Cleric", 1) == 1, "A cleric's caster level is the class level");
        Assert(CasterLevelRules.ClassCasterLevel("Paladin", 3) == 0, "A paladin 3 has no caster level (PHB p.44)");
        Assert(CasterLevelRules.ClassCasterLevel("Paladin", 4) == 2, "A paladin 4 has caster level 2 (one-half, PHB p.44)");
        Assert(CasterLevelRules.ClassCasterLevel("Paladin", 8) == 4, "A paladin 8 has caster level 4");
        Assert(CasterLevelRules.ClassCasterLevel("Ranger", 3) == 0, "A ranger 3 has no caster level (PHB p.48)");
        Assert(CasterLevelRules.ClassCasterLevel("Ranger", 4) == 2, "A ranger 4 has caster level 2 (one-half, PHB p.48)");
        Assert(CasterLevelRules.ClassCasterLevel("ranger", 9) == 4, "A ranger 9 has caster level 4 (one-half, rounded down; the class name in any case)");
        Assert(CasterLevelRules.ClassCasterLevel("Wizard", -2) == 0, "A class level drained below 0 gives caster level 0");
        Assert(CasterLevelRules.IsHalfCasterClass("Paladin") && CasterLevelRules.IsHalfCasterClass("Ranger")
               && !CasterLevelRules.IsHalfCasterClass("Cleric") && !CasterLevelRules.IsHalfCasterClass(null),
            "Only the paladin and the ranger are half casters");
    }

    private static void TestCreatures()
    {
        SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
        SpellData bless = SpellDatabase.GetSpell(SpellNames.BLESS);
        SpellData cure = SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS);

        CharacterController wizard = null, cleric = null, pal3 = null, pal4 = null, pal8 = null, rgr4 = null, rgr8 = null, fighter = null;
        try
        {
            wizard = TestHelpers.CreateCharacter("CL Wizard", "Wizard", 5, intelligence: 16);
            Assert(wizard.Stats.GetCasterLevel() == 5 && CasterLevelRules.ForCreature(wizard, missile) == 5,
                "A wizard 5 casts at caster level 5", wizard.Stats.GetCasterLevel() + " / " + CasterLevelRules.ForCreature(wizard, missile));
            SpellCastRequest ai = SpellCastRequest.ForAI(wizard, null, missile);
            Assert(CasterLevelRules.For(ai) == 5, "A wizard 5's NPC request casts at caster level 5", "" + CasterLevelRules.For(ai));

            cleric = TestHelpers.CreateCleric("CL Cleric", 5);
            cleric.Stats.ChosenDomains.Add("Good");
            Assert(CasterLevelRules.ForCreature(cleric, bless) == 6,
                "A cleric 5 with the Good domain casts a [Good] spell (Bless) at caster level 6", "" + CasterLevelRules.ForCreature(cleric, bless));
            Assert(CasterLevelRules.ForCreature(cleric, cure) == 5,
                "A cleric 5 with the Good domain casts Cure Light Wounds at caster level 5", "" + CasterLevelRules.ForCreature(cleric, cure));

            pal3 = TestHelpers.CreateCharacter("CL Paladin 3", "Paladin", 3, wis: 14);
            pal4 = TestHelpers.CreateCharacter("CL Paladin 4", "Paladin", 4, wis: 14);
            pal8 = TestHelpers.CreateCharacter("CL Paladin 8", "Paladin", 8, wis: 14);
            Assert(pal3.Stats.GetCasterLevel() == 0, "A paladin 3 has caster level 0 (GetCasterLevel)", "" + pal3.Stats.GetCasterLevel());
            Assert(CasterLevelRules.ForCreature(pal3, bless) == 1, "A cast never works below caster level 1", "" + CasterLevelRules.ForCreature(pal3, bless));
            Assert(pal4.Stats.GetCasterLevel() == 2 && CasterLevelRules.ForCreature(pal4, bless) == 2,
                "A paladin 4 casts Bless at caster level 2", pal4.Stats.GetCasterLevel() + " / " + CasterLevelRules.ForCreature(pal4, bless));
            Assert(pal8.Stats.GetCasterLevel() == 4 && CasterLevelRules.ForCreature(pal8, bless) == 4,
                "A paladin 8 casts Bless at caster level 4", pal8.Stats.GetCasterLevel() + " / " + CasterLevelRules.ForCreature(pal8, bless));
            Assert(SpellDurationRules.CasterLevelFor(pal8, bless) == 4 && SpellDiceRules.CasterLevelFor(pal8.Stats, bless) == 4
                   && SpellCastingHelper.GetEffectiveCasterLevel(pal8, bless) == 4,
                "A paladin 8's durations, generic dice and handler context use caster level 4, not 8",
                SpellDurationRules.CasterLevelFor(pal8, bless) + " / " + SpellDiceRules.CasterLevelFor(pal8.Stats, bless));

            rgr4 = TestHelpers.CreateCharacter("CL Ranger 4", "Ranger", 4, wis: 14);
            rgr8 = TestHelpers.CreateCharacter("CL Ranger 8", "Ranger", 8, wis: 14);
            SpellData entangle = SpellDatabase.GetSpell(SpellNames.ENTANGLE);
            Assert(rgr4.Stats.GetCasterLevel() == 2 && CasterLevelRules.ForCreature(rgr4, entangle) == 2,
                "A ranger 4 casts at caster level 2", rgr4.Stats.GetCasterLevel() + " / " + CasterLevelRules.ForCreature(rgr4, entangle));
            Assert(rgr8.Stats.GetCasterLevel() == 4 && CasterLevelRules.ForCreature(rgr8, entangle) == 4,
                "A ranger 8 casts at caster level 4", rgr8.Stats.GetCasterLevel() + " / " + CasterLevelRules.ForCreature(rgr8, entangle));
            Assert(entangle != null && entangle.GetRangeSquaresForCasterLevel(4) == CasterLevelRules.RangeSquares(SpellCastRequest.ForAI(rgr8, null, entangle)),
                "A ranger 8's range is the range at caster level 4");

            // CHR-085: the casting Concentration proxy reads the class level, not the halved caster level (PHB p.69-70:
            // ranks, up to level + 3, which the half caster level of PHB p.44 and p.48 does not touch).
            Assert(pal4.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: false) == 4 + pal4.Stats.CONMod
                   && rgr4.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: false) == 4 + rgr4.Stats.CONMod,
                "A paladin 4's and a ranger 4's casting Concentration proxy is class level 4 + Con, not caster level 2 (CHR-085)",
                pal4.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: false) + " / " + rgr4.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: false));
            Assert(pal3.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: false) == 3 + pal3.Stats.CONMod,
                "A paladin 3's casting Concentration proxy is class level 3 + Con (CHR-085)", "" + pal3.Stats.GetSpellcastingConcentrationBonus(includeCombatCasting: false));

            fighter = TestHelpers.CreateWarrior("CL Fighter", 6);
            Assert(!fighter.Stats.IsSpellcaster && CasterLevelRules.ForCreature(fighter, missile) == fighter.Stats.GetHitDice(),
                "A creature with no casting class casts at its Hit Dice (MM p.315; delegated RAW reading 2026-10-14)", CasterLevelRules.ForCreature(fighter, missile) + " / HD " + fighter.Stats.GetHitDice());
            fighter.Stats.WondrousCasterLevelBonus = 1;
            Assert(CasterLevelRules.ForCreature(fighter, missile) == fighter.Stats.GetHitDice() + 1,
                "A worn item's +1 caster level raises a classless creature's Hit Dice caster level too", CasterLevelRules.ForCreature(fighter, missile) + " / HD " + fighter.Stats.GetHitDice());
            fighter.Stats.WondrousCasterLevelBonus = 0;
        }
        finally
        {
            TestHelpers.Cleanup(Go(wizard), Go(cleric), Go(pal3), Go(pal4), Go(pal8), Go(rgr4), Go(rgr8), Go(fighter));
        }
    }

    private static void TestItems()
    {
        SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
        CharacterController wizard = null, pal4 = null;
        try
        {
            wizard = TestHelpers.CreateCharacter("CL Scroll Reader", "Wizard", 5, intelligence: 16);
            SpellCastRequest scroll = SpellCastRequest.ForUiPrebuilt(wizard, missile.Clone(), null,
                new CastSource { Kind = CastSourceKind.Scroll, ItemCasterLevel = 1 });
            Assert(CasterLevelRules.For(scroll) == 1, "A wizard 5 reading a caster level 1 scroll casts at caster level 1 (DMG p.237)", "" + CasterLevelRules.For(scroll));
            Assert(CasterLevelRules.RangeSquares(scroll) == missile.GetRangeSquaresForCasterLevel(1),
                "The scroll's range is the range at the scroll's caster level", CasterLevelRules.RangeSquares(scroll) + " vs " + missile.GetRangeSquaresForCasterLevel(1));

            pal4 = TestHelpers.CreateCharacter("CL Wand User", "Paladin", 4, wis: 14);
            SpellCastRequest wand = SpellCastRequest.ForUiPrebuilt(pal4, missile.Clone(), null,
                new CastSource { Kind = CastSourceKind.Wand, ItemCasterLevel = 3 });
            Assert(CasterLevelRules.For(wand) == 3, "A paladin 4 using a caster level 3 wand casts at caster level 3 (DMG p.245)", "" + CasterLevelRules.For(wand));

            SpellCastRequest notItem = SpellCastRequest.ForUi(wizard, missile, null,
                new CastSource { Kind = CastSourceKind.PreparedSlot, ItemCasterLevel = 1 });
            Assert(CasterLevelRules.For(notItem) == 5, "An item caster level on a slot source is ignored", "" + CasterLevelRules.For(notItem));
            Assert(CasterLevelRules.ItemCasterLevel(new CastSource { Kind = CastSourceKind.Scroll, ItemCasterLevel = 0 }) == 1,
                "An item's caster level is at least 1");
        }
        finally
        {
            TestHelpers.Cleanup(Go(wizard), Go(pal4));
        }
    }

    private static void TestNegativeLevels()
    {
        SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
        SpellData bless = SpellDatabase.GetSpell(SpellNames.BLESS);
        CharacterController wizard = null, pal8 = null, pal4 = null, warrior = null;
        try
        {
            wizard = TestHelpers.CreateCharacter("CL Drained Wizard", "Wizard", 5, con: 18, intelligence: 16);
            wizard.Stats.HitDice = 5;
            NegativeLevelSystem.ApplyNegativeLevels(wizard, 2, "UnitTest");
            Assert(wizard.Stats.NegativeLevelCount == 2 && CasterLevelRules.ForCreature(wizard, missile) == 3,
                "A wizard 5 with two negative levels casts at caster level 3 (DMG p.293)", wizard.Stats.NegativeLevelCount + " / " + CasterLevelRules.ForCreature(wizard, missile));
            SpellCastRequest scroll = SpellCastRequest.ForUiPrebuilt(wizard, missile.Clone(), null,
                new CastSource { Kind = CastSourceKind.Scroll, ItemCasterLevel = 4 });
            Assert(CasterLevelRules.For(scroll) == 4, "Negative levels do not lower a scroll's own caster level", "" + CasterLevelRules.For(scroll));

            pal8 = TestHelpers.CreateCharacter("CL Drained Paladin 8", "Paladin", 8, con: 18, wis: 14);
            pal8.Stats.HitDice = 8;
            NegativeLevelSystem.ApplyNegativeLevels(pal8, 1, "UnitTest");
            Assert(CasterLevelRules.ForCreature(pal8, bless) == 3,
                "A paladin 8 with one negative level is an effective paladin 7: caster level 3 (delegated RAW reading 2026-10-14)", "" + CasterLevelRules.ForCreature(pal8, bless));

            pal4 = TestHelpers.CreateCharacter("CL Drained Paladin 4", "Paladin", 4, con: 18, wis: 14);
            pal4.Stats.HitDice = 4;
            NegativeLevelSystem.ApplyNegativeLevels(pal4, 1, "UnitTest");
            Assert(pal4.Stats.GetCasterLevel() == 0 && CasterLevelRules.ForCreature(pal4, bless) == 1,
                "A paladin 4 with one negative level has no caster level; a cast still works at the minimum 1", pal4.Stats.GetCasterLevel() + " / " + CasterLevelRules.ForCreature(pal4, bless));

            warrior = TestHelpers.CreateWarrior("CL Drained Warrior", 6);
            warrior.Stats.HitDice = 6;
            int hitDice = warrior.Stats.GetHitDice();
            NegativeLevelSystem.ApplyNegativeLevels(warrior, 1, "UnitTest");
            Assert(!warrior.Stats.IsSpellcaster && warrior.Stats.NegativeLevelCount == 1 && CasterLevelRules.ForCreature(warrior, missile) == Mathf.Max(1, hitDice - 1),
                "A creature with no casting class and one negative level casts at its Hit Dice - 1 (DMG p.293; delegated RAW reading 2026-10-14)",
                warrior.Stats.NegativeLevelCount + " / " + CasterLevelRules.ForCreature(warrior, missile) + " / HD " + hitDice);
        }
        finally
        {
            TestHelpers.Cleanup(Go(wizard), Go(pal8), Go(pal4), Go(warrior));
        }
    }

    private static void TestRange()
    {
        SpellData grasp = SpellDatabase.GetSpell(SpellNames.SHOCKING_GRASP);
        CharacterController wizard = null, near = null, far = null;
        try
        {
            wizard = TestHelpers.CreateCharacter("CL Range Wizard", "Wizard", 5, intelligence: 16, gridPosition: new Vector2Int(0, 0));
            near = TestHelpers.CreateWarrior("CL Near", 1);
            far = TestHelpers.CreateWarrior("CL Far", 1);
            near.GridPosition = new Vector2Int(1, 0);
            far.GridPosition = new Vector2Int(2, 0);
            SpellCastRequest req = SpellCastRequest.ForAI(wizard, near, grasp);
            Assert(CasterLevelRules.DistanceToTarget(wizard, near) == 1 && CasterLevelRules.InRange(req, near),
                "A touch spell reaches an adjacent creature (at least 1 square)");
            Assert(!CasterLevelRules.InRange(req, far), "A touch spell does not reach a creature 2 squares away");
            Assert(CasterLevelRules.InRange(req, wizard), "A caster is in range of its own spell");
        }
        finally
        {
            TestHelpers.Cleanup(Go(wizard), Go(near), Go(far));
        }
    }

    private static void TestOpenCast(GameManager gm)
    {
        SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
        CharacterController wizard = null;
        CastContext ctx = null;
        try
        {
            wizard = TestHelpers.CreateCharacter("CL Open Cast Wizard", "Wizard", 5, intelligence: 16);
            Assert(CasterLevelRules.ForCaster(wizard, missile) == 5, "No cast open: the wizard's own caster level 5");
            ctx = gm.BeginCast(SpellCastRequest.ForUiPrebuilt(wizard, missile.Clone(), null,
                new CastSource { Kind = CastSourceKind.Scroll, ItemCasterLevel = 1 }));
            Assert(ReferenceEquals(gm.OpenCastRequestFor(wizard, missile), ctx.Request), "The open scroll cast is found by caster and spell");
            Assert(CasterLevelRules.ForCaster(wizard, missile) == 1 && SpellCastingHelper.GetEffectiveCasterLevel(wizard, missile) == 1
                   && SpellDurationRules.CasterLevelFor(wizard, missile) == 1,
                "While the scroll cast is open, every reader gets the scroll's caster level 1 (SPL-016)",
                CasterLevelRules.ForCaster(wizard, missile) + " / " + SpellCastingHelper.GetEffectiveCasterLevel(wizard, missile));
            SpellData bless = SpellDatabase.GetSpell(SpellNames.BLESS);
            Assert(CasterLevelRules.ForCaster(wizard, bless) == 5, "Another spell by the same caster is not the open cast's");
            gm.EndCast(ctx);
            ctx = null;
            Assert(gm.OpenCastRequestFor(wizard, missile) == null && CasterLevelRules.ForCaster(wizard, missile) == 5,
                "Once the cast ends, the wizard's own caster level again");
        }
        finally
        {
            if (ctx != null)
                gm.EndCast(ctx);
            TestHelpers.Cleanup(Go(wizard));
        }
    }

    private static GameObject Go(CharacterController c) => c != null ? c.gameObject : null;

    private static void Assert(bool condition, string name, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log("  PASS: " + name);
        }
        else
        {
            _failed++;
            Debug.LogError("  FAIL: " + name + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }
}
}
#endif

using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Magic
{
/// <summary>
/// SPL-001: one save DC rule (<see cref="SpellSaveDCRules"/>) for every cast path.
/// Rules (checked 2026-10-08): PHB p.177 (DC = 10 + the spell's level for your class + INT for a wizard, CHA for a
/// sorcerer or bard, WIS for a cleric, druid, paladin or ranger; "always use the spell level applicable to your
/// class"), DMG p.108 (the adept casts with WIS), PHB p.94 and p.100 (Greater Spell Focus and Spell Focus, +1 each
/// for the school), PHB p.95 (Heighten Spell raises the spell level), PHB p.17 (gnome +1 on illusion spells),
/// MM p.315 (spell-like ability: 10 + the sorcerer/wizard level of the spell + CHA), DMG p.214 (magic item: 10 +
/// spell level + the modifier of the minimum score to cast that level; staffs use the wielder's DC), PHB p.222
/// (Dismissal's special DC is built on the spell's DC).
/// Builds CharacterStats only (no controllers), so it runs in edit or Play mode.
/// </summary>
public static class SpellSaveDCRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();

        Debug.Log("====== SPELL SAVE DC RULES TESTS (SPL-001) ======");

        TestKeyAbilityByClass();
        TestClassSpellLevel();
        TestMulticlassCastingClass();
        TestSpentSlotCastingClass();
        TestNoCastingClass();
        TestSpellFocus();
        TestHeighten();
        TestGnomeIllusion();
        TestPreBakedDC();
        TestSpellLikeAbility();
        TestMagicItems();
        TestSharedEntryPoints();

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
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

    private static void AssertEq(int actual, int expected, string testName)
    {
        Assert(actual == expected, testName, $"expected {expected}, got {actual}");
    }

    /// <summary>INT 18 (+4), WIS 14 (+2), CHA 12 (+1): every key ability gives a different DC.</summary>
    private static CharacterStats Build(string cls, int level = 5, string race = "Human", int intelligence = 18, int wis = 14, int cha = 12)
    {
        return new CharacterStats(
            name: cls + " " + level, level: level, characterClass: cls,
            str: 10, dex: 10, con: 10, wis: wis, intelligence: intelligence, cha: cha,
            bab: 0, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 4 * level, raceName: race);
    }

    private static SpellData Spell(string id)
    {
        SpellData spell = SpellDatabase.GetSpell(id);
        if (spell == null)
            Debug.LogError($"  FAIL: spell {id} missing from SpellDatabase");
        return spell;
    }

    // ── PHB p.177, DMG p.108 ───────────────────────────────────────────

    private static void TestKeyAbilityByClass()
    {
        Debug.Log("--- Key ability per class (PHB p.177, DMG p.108) ---");
        var rows = new (string cls, int mod, string ability)[]
        {
            ("Wizard", 4, "INT"), ("Sorcerer", 1, "CHA"), ("Bard", 1, "CHA"),
            ("Cleric", 2, "WIS"), ("Druid", 2, "WIS"), ("Paladin", 2, "WIS"), ("Ranger", 2, "WIS"), ("Adept", 2, "WIS"),
        };
        foreach (var r in rows)
        {
            CharacterStats s = Build(r.cls);
            int mod = SpellSaveDCRules.GetKeyAbilityModifier(s, SpellSaveDCRules.ResolveCastingClass(s, null), out string name);
            AssertEq(mod, r.mod, $"{r.cls} key ability modifier is {r.ability}");
            Assert(name == r.ability, $"{r.cls} key ability is named {r.ability}", $"got {name}");
        }

        // The SPL-001 symptom: a sorcerer's and a bard's DC used WIS. Sorcerer CHA 16 (+3), WIS 8 (-1).
        SpellData daze = Spell(SpellNames.DAZE);
        if (daze != null)
        {
            AssertEq(SpellSaveDCRules.Compute(Build("Sorcerer", 1, "Human", 10, 8, 16), daze), 13, "Sorcerer Daze DC 10 + 0 + CHA 3 = 13 (not WIS)");
            AssertEq(SpellSaveDCRules.Compute(Build("Bard", 1, "Human", 10, 8, 16), daze), 13, "Bard Daze DC 10 + 0 + CHA 3 = 13 (not WIS)");
            AssertEq(SpellSaveDCRules.Compute(Build("Wizard", 1, "Human", 16, 8, 10), daze), 13, "Wizard Daze DC 10 + 0 + INT 3 = 13");
        }
    }

    private static void TestClassSpellLevel()
    {
        Debug.Log("--- The spell level for the casting class (PHB p.177) ---");
        SpellData hold = Spell(SpellNames.HOLD_PERSON); // Brd 2, Clr 2, Sor/Wiz 3
        if (hold == null) return;
        AssertEq(SpellSaveDCRules.Compute(Build("Wizard"), hold), 10 + 3 + 4, "Wizard Hold Person: Sor/Wiz 3 + INT");
        AssertEq(SpellSaveDCRules.Compute(Build("Sorcerer"), hold), 10 + 3 + 1, "Sorcerer Hold Person: Sor/Wiz 3 + CHA");
        AssertEq(SpellSaveDCRules.Compute(Build("Bard"), hold), 10 + 2 + 1, "Bard Hold Person: Brd 2 + CHA");
        AssertEq(SpellSaveDCRules.Compute(Build("Cleric"), hold), 10 + 2 + 2, "Cleric Hold Person: Clr 2 + WIS");
        AssertEq(SpellSaveDCRules.Compute(Build("Cleric"), hold, null, "Wizard"), 10 + 3 + 4, "An explicit casting class wins");
    }

    private static void TestMulticlassCastingClass()
    {
        Debug.Log("--- Multiclass: the class whose list has the spell, else the highest casting class ---");
        CharacterStats s = Build("Cleric", 5);
        s.ClassLevels.Add(new ClassLevelEntry("Wizard", 1));
        s.InvalidateClassLevelCache();

        SpellData daze = Spell(SpellNames.DAZE); // Sor/Wiz/Brd 0, not a cleric spell
        if (daze != null)
            AssertEq(SpellSaveDCRules.Compute(s, daze), 10 + 0 + 4, "Cleric 5 / Wizard 1 Daze uses the wizard list and INT");

        SpellData hold = Spell(SpellNames.HOLD_PERSON); // on both lists: the higher class (Cleric 5) casts it
        if (hold != null)
            AssertEq(SpellSaveDCRules.Compute(s, hold), 10 + 2 + 2, "Cleric 5 / Wizard 1 Hold Person uses Cleric 2 and WIS");
    }

    /// <summary>
    /// PHB p.177 ("use the spell level applicable to your class"): a multiclass prepared caster's DC uses the class of
    /// the slot actually spent. The slot is used before the cast resolves its DC, so the rule must not fall back to the
    /// other class's unused copy of the same spell.
    /// </summary>
    private static void TestSpentSlotCastingClass()
    {
        Debug.Log("--- Multiclass: the DC uses the class of the slot that was spent ---");
        SpellData hold = Spell(SpellNames.HOLD_PERSON); // Clr 2, Sor/Wiz 3
        if (hold == null) return;

        CharacterStats s = Build("Cleric", 5); // INT +4, WIS +2
        s.ClassLevels.Add(new ClassLevelEntry("Wizard", 5));
        s.InvalidateClassLevelCache();

        GameObject go = new GameObject("SpellSaveDCTest_Multiclass");
        try
        {
            CharacterController controller = go.AddComponent<CharacterController>();
            controller.Stats = s;
            SpellcastingComponent comp = go.AddComponent<SpellcastingComponent>();
            comp.Init(s);

            string firstClass = null;
            for (int pass = 0; pass < 2; pass++)
            {
                comp.SpellSlots.Clear();
                var clericSlot = new SpellSlot(2, hold.Clone(), false, "Cleric");
                var wizardSlot = new SpellSlot(3, hold.Clone(), false, "Wizard");
                comp.SpellSlots.Add(clericSlot);
                comp.SpellSlots.Add(wizardSlot);
                // Pass 1 lets the component choose; pass 2 leaves only the class pass 1 did not spend.
                SpellSlot preMarked = null;
                if (pass == 1)
                {
                    preMarked = firstClass == "Cleric" ? clericSlot : wizardSlot;
                    preMarked.IsUsed = true;
                }

                bool cast = comp.CastSpellFromSlot(hold.Clone());
                Assert(cast, $"Pass {pass + 1}: Cleric 5 / Wizard 5 casts Hold Person from a slot");
                SpellSlot spent = clericSlot.IsUsed && clericSlot != preMarked ? clericSlot : wizardSlot;
                if (pass == 0)
                    firstClass = spent.CasterClassName;
                SpellSlot other = spent == clericSlot ? wizardSlot : clericSlot;
                Assert(comp.LastCastClassName == spent.CasterClassName,
                    $"Pass {pass + 1}: the component records the spent slot's class", $"got {comp.LastCastClassName}, spent {spent.CasterClassName}");
                if (pass == 0)
                    Assert(!other.IsUsed, "Pass 1: the other class still holds an unused Hold Person");

                int expected = spent.CasterClassName == "Cleric" ? 10 + 2 + 2 : 10 + 3 + 4;
                AssertEq(SpellSaveDCRules.Compute(controller, hold.Clone()), expected,
                    $"Pass {pass + 1}: DC uses the spent {spent.CasterClassName} slot ({(spent.CasterClassName == "Cleric" ? "Clr 2 + WIS" : "Sor/Wiz 3 + INT")})");
                AssertEq(comp.GetSpellDC(hold.Clone()), expected, $"Pass {pass + 1}: SpellcastingComponent.GetSpellDC agrees");
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void TestNoCastingClass()
    {
        Debug.Log("--- No casting class: best mental modifier (unchanged fallback) ---");
        SpellData hold = Spell(SpellNames.HOLD_PERSON);
        if (hold == null) return;
        SpellSaveDCBreakdown dc = SpellSaveDCRules.Explain(Build("Fighter"), hold);
        AssertEq(dc.Total, 10 + 2 + 4, "Fighter Hold Person: base level + best mental (INT 4)");
        Assert(dc.CastingClass == null, "Fighter has no casting class", $"got {dc.CastingClass}");
    }

    // ── PHB p.94, p.100 ────────────────────────────────────────────────

    private static void TestSpellFocus()
    {
        Debug.Log("--- Spell Focus and Greater Spell Focus (PHB p.94, p.100) ---");
        SpellData hold = Spell(SpellNames.HOLD_PERSON);       // Enchantment
        SpellData fireball = Spell(SpellNames.FIREBALL);      // "Evocation [Fire]"
        if (hold == null || fireball == null) return;

        CharacterStats s = Build("Wizard");
        s.Feats.Add("Spell Focus");
        s.SpellFocusSchool = "Enchantment";
        AssertEq(SpellSaveDCRules.Compute(s, hold), 10 + 3 + 4 + 1, "Spell Focus (Enchantment) +1 on Hold Person");
        AssertEq(SpellSaveDCRules.Compute(s, fireball), 10 + 3 + 4, "Spell Focus (Enchantment) does not apply to Evocation");

        s.Feats.Add("Greater Spell Focus");
        s.GreaterSpellFocusSchool = "Enchantment";
        AssertEq(SpellSaveDCRules.Compute(s, hold), 10 + 3 + 4 + 2, "Greater Spell Focus stacks (+2)");

        CharacterStats evoker = Build("Wizard");
        evoker.Feats.Add("Spell Focus");
        evoker.SpellFocusSchool = "Evocation";
        AssertEq(SpellSaveDCRules.Compute(evoker, fireball), 10 + 3 + 4 + 1, "Spell Focus (Evocation) applies to \"Evocation [Fire]\"");
        AssertEq(FeatManager.GetSpellFocusDCBonus(evoker, "Evocation [Fire]"), 1, "GetSpellFocusDCBonus parses the school");
    }

    // ── PHB p.95 ───────────────────────────────────────────────────────

    private static void TestHeighten()
    {
        Debug.Log("--- Heighten Spell raises the level used (PHB p.95) ---");
        SpellData hold = Spell(SpellNames.HOLD_PERSON);
        if (hold == null) return;
        var mm = new MetamagicData();
        mm.Toggle(MetamagicFeatId.HeightenSpell);
        mm.HeightenToLevel = 5;
        AssertEq(SpellSaveDCRules.Compute(Build("Wizard"), hold, mm), 10 + 5 + 4, "Hold Person heightened to 5th: DC uses 5");

        var empower = new MetamagicData();
        empower.Toggle(MetamagicFeatId.EmpowerSpell);
        AssertEq(SpellSaveDCRules.Compute(Build("Wizard"), hold, empower), 10 + 3 + 4, "Empower does not change the DC");

        SpellData clone = hold.Clone();
        clone.MetamagicDataRef = mm;
        AssertEq(SpellSaveDCRules.Compute(Build("Wizard"), clone), 10 + 5 + 4, "A prepared heightened clone (MetamagicDataRef) uses 5");
    }

    // ── PHB p.17 ───────────────────────────────────────────────────────

    private static void TestGnomeIllusion()
    {
        Debug.Log("--- Gnome +1 on illusion spells (PHB p.17) ---");
        SpellData colorSpray = Spell(SpellNames.COLOR_SPRAY); // Illusion, Sor/Wiz 1
        SpellData hold = Spell(SpellNames.HOLD_PERSON);
        if (colorSpray == null || hold == null) return;
        CharacterStats gnome = Build("Wizard", 5, "Gnome");
        int intMod = gnome.INTMod;
        AssertEq(SpellSaveDCRules.Compute(gnome, colorSpray), 10 + 1 + intMod + 1, "Gnome wizard Color Spray +1");
        AssertEq(SpellSaveDCRules.Compute(gnome, hold), 10 + 3 + intMod, "Gnome wizard Hold Person (enchantment) +0");
    }

    private static void TestPreBakedDC()
    {
        Debug.Log("--- A spell that carries its own DC keeps it (scroll, wand, imbued spell) ---");
        SpellData hold = Spell(SpellNames.HOLD_PERSON);
        if (hold == null) return;
        SpellData clone = hold.Clone();
        clone.SaveDC = 13;
        SpellSaveDCBreakdown dc = SpellSaveDCRules.Explain(Build("Wizard"), clone);
        AssertEq(dc.Total, 13, "Pre-baked DC 13 is kept");
        Assert(dc.Source == "fixed", "Pre-baked DC is reported as fixed", $"got {dc.Source}");
        Assert(hold.SaveDC == 0, "The database template is untouched");
    }

    // ── MM p.315 ───────────────────────────────────────────────────────

    private static void TestSpellLikeAbility()
    {
        Debug.Log("--- Spell-like abilities: Sor/Wiz level + CHA (MM p.315) ---");
        SpellData hold = Spell(SpellNames.HOLD_PERSON);
        if (hold == null) return;
        CharacterStats monster = Build("Fighter", 5, "Human", 10, 18, 14); // CHA 14 (+2), WIS 18 ignored
        SpellSaveDCBreakdown dc = SpellSaveDCRules.ExplainSpellLikeAbility(monster, hold);
        AssertEq(dc.Total, 10 + 3 + 2, "Hold Person as a spell-like ability: Sor/Wiz 3 + CHA");
        AssertEq(SpellSaveDCRules.GetSpellLikeAbilityLevel(hold), 3, "Spell-like level prefers the sorcerer/wizard version");
    }

    // ── DMG p.214 ──────────────────────────────────────────────────────

    private static void TestMagicItems()
    {
        Debug.Log("--- Magic items: 10 + level + the minimum ability modifier (DMG p.214) ---");
        int[] expected = { 10, 11, 13, 14, 16, 17 };
        for (int level = 0; level < expected.Length; level++)
            AssertEq(SpellSaveDCRules.ForMagicItem(level), expected[level], $"Magic item spell level {level}");
        AssertEq(WandFactory.CalculateWandSaveDC(2), 13, "Wand of a 2nd-level spell: DC 13 (the DMG example)");

        SpellData hold = Spell(SpellNames.HOLD_PERSON);
        if (hold != null)
        {
            ScrollData scroll = ScrollData.Create(hold, 3, true, 150);
            AssertEq(scroll.SaveDC, 13, "Arcane scroll of Hold Person (Brd 2) defaults to DC 13, not 12 (ITM-008)");
            AssertEq(ScrollData.Create(hold, 3, false, 150).SaveDC, 13, "Divine scroll of Hold Person (Clr 2) defaults to DC 13");
            ScrollData heightened = ScrollData.Create(hold, 9, true, 150, null, -1, 0, 4);
            AssertEq(heightened.SaveDC, 16, "Scroll heightened to 4th defaults to DC 16");
            WandData wand = WandData.Create(hold, 3, true, 4500);
            AssertEq(wand.SaveDC, 13, "Wand of Hold Person defaults to DC 13");
        }

        // An item spell's level is the level on its own kind of list: arcane Sor/Wiz 3 (no bard version), divine Clr 2.
        var split = new SpellData { SpellId = "spell_dc_test_split", Name = "Split Level Test", SpellLevel = 2, School = "Enchantment" };
        split.AvailableFor.Add(new SpellAvailability("Wizard", 3));
        split.AvailableFor.Add(new SpellAvailability("Sorcerer", 3));
        split.AvailableFor.Add(new SpellAvailability("Cleric", 2));
        AssertEq(SpellSaveDCRules.GetItemSpellLevel(split, true), 3, "Arcane item level: the Sor/Wiz level");
        AssertEq(SpellSaveDCRules.GetItemSpellLevel(split, false), 2, "Divine item level: the cleric level");
        AssertEq(ScrollData.Create(split, 5, true, 375).SaveDC, 14, "Arcane scroll (Sor/Wiz 3): DC 10 + 3 + 1");
        AssertEq(ScrollData.Create(split, 3, false, 150).SaveDC, 13, "Divine scroll (Clr 2): DC 10 + 2 + 1");
        AssertEq(WandData.Create(split, 5, true, 11250).SaveDC, 14, "Arcane wand (Sor/Wiz 3): DC 14");

        var entry = new StaffSpellEntry { SpellId = SpellNames.HOLD_PERSON, SpellName = "Hold Person", SpellLevel = 2 };
        AssertEq(StaffValidator.CalculateStaffSaveDC(null, entry), 13, "Staff without a wielder: magic-item DC");
    }

    private static void TestSharedEntryPoints()
    {
        Debug.Log("--- Every entry point gives the same DC ---");
        SpellData hold = Spell(SpellNames.HOLD_PERSON);
        if (hold == null) return;
        CharacterStats sorcerer = Build("Sorcerer", 5, "Human", 10, 8, 16); // CHA +3, WIS -1
        CharacterStats target = Build("Fighter", 1);
        int expected = 10 + 3 + 3;

        AssertEq(SpellSaveDCRules.Compute(sorcerer, hold), expected, "SpellSaveDCRules: Sorcerer Hold Person 16");
        AssertEq(SpellUtilities.GetSpellSaveDC(sorcerer, hold), expected, "SpellUtilities.GetSpellSaveDC (custom resolvers)");
        AssertEq(SpellUtilities.GetCastingAbilityModifier(sorcerer, hold), 3, "SpellUtilities.GetCastingAbilityModifier is CHA");

        SpellResult result = SpellCaster.Cast(hold.Clone(), sorcerer, target);
        Assert(result != null && result.RequiredSave, "SpellCaster.Cast rolls a save for Hold Person");
        if (result != null && result.RequiredSave)
            AssertEq(result.SaveDC, expected, "SpellCaster.Cast (PC and NPC single-target paths) uses the same DC");
    }
}
}

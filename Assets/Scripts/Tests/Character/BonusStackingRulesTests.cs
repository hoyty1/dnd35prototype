using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Bonus stacking by the rules as written (owner directive 2026-10-09: no house rules). Rules applied: PHB p.171
/// "Bonus Types" (same-type bonuses do not stack, except dodge, most circumstance and racial bonuses; the same for
/// penalties: only the worst of one type); PHB p.171-172 "Combining Magical Effects" (two bless spells give one bless's
/// benefit; different spells of one type do not stack; a weaker effect continues when the stronger ends; an unnamed
/// bonus stacks with any bonus); PHB glossary p.305 "bonus", p.306 "circumstance bonus" (stack unless from essentially
/// the same circumstance), p.307 "dodge bonus" (stack), p.310 "luck bonus" and "morale bonus" (do not stack), p.313
/// "stack" (not from the same source); DMG p.21 "Bonus Types". Spells: PHB p.205 Bless, p.224 Divine Favor, p.240
/// Heroism, p.264 Prayer, p.239 Haste, p.249 Longstrider, p.228 Expeditious Retreat, p.207 Bull's Strength, p.249 Mage
/// Armor; PHB p.29 Inspire Courage; PHB p.154 charge (+2, untyped); DMG p.267 Stone of Good Luck, DMG p.265 Robe of
/// Stars; DMG p.250 boots of striding and springing; PHB p.41 monk fast movement (enhancement); PHB p.268 Rage; PHB p.203
/// Bane (its save penalty only against fear); PHB p.278 Shield of Faith. Covers <see cref="BonusStacking"/>, <see cref="BonusLedger"/>,
/// <see cref="StatusEffectManager.AddEffect"/> and the CharacterStats totals (EffectAttackBonus, EffectWeaponDamageBonus,
/// EffectSaveBonus, EffectSkillBonus, LandSpeedEnhancementWithItemsFeet, ability scores). Builds bare CharacterStats and,
/// for spell effects, a StatusEffectManager on a plain GameObject; runs in edit or Play mode.
/// </summary>
public static class BonusStackingRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== BONUS STACKING RULES TESTS (RAW, 2026-10-09) ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();

        TestDoesStackTypes();
        TestTwoLuckBonuses();
        TestTwoMoraleBonuses();
        TestCircumstanceBonuses();
        TestUntypedBonuses();
        TestDodgeBonuses();
        TestPenalties();
        TestLedgerOwnersAndTargets();
        TestLuckSpellsDoNotStack();
        TestMoraleSpellsDoNotStack();
        TestSameSpellTwice();
        TestInspireCourageAndCharge();
        TestPrayerPenaltyStacksWithBane();
        TestLuckOnSaves();
        TestFearMoraleBeyondGeneralMorale();
        TestSkillBonuses();
        TestSpeedEnhancement();
        TestAbilityEnhancement();
        TestHasteAndMageArmor();
        TestMonkFastMovementIsEnhancement();
        TestRageSpell();
        TestBaneSavePenaltyOnlyAgainstFear();
        TestShieldOfFaithCopies();
        TestSkillRollMatchesSkillBonus();

        Debug.Log($"====== Bonus Stacking Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static void AssertEq(int got, int want, string testName)
    {
        Assert(got == want, testName, $"got {got}, expected {want}");
    }

    private static TypedBonus B(BonusType type, int value, string source) => new TypedBonus(type, value, source, source);

    private static int Combine(params TypedBonus[] mods) => BonusStacking.Combine(new List<TypedBonus>(mods));

    private static CharacterStats Build(string cls = "Fighter", int level = 3, int str = 10, int wis = 10)
    {
        var stats = new CharacterStats(
            name: "Stacker", level: level, characterClass: cls,
            str: str, dex: 10, con: 10, wis: wis, intelligence: 10, cha: 10,
            bab: 0, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 8, raceName: "Human");
        stats.EnsureMulticlassDataInitialized();
        if (stats.Skills == null || !stats.Skills.ContainsKey("Climb"))
            stats.InitializeSkills(cls, level);
        return stats;
    }

    /// <summary>A StatusEffectManager on a plain GameObject for <paramref name="stats"/>; destroy the object afterwards.</summary>
    private static StatusEffectManager Effects(CharacterStats stats, out GameObject go)
    {
        go = new GameObject("BonusStackingRulesTests_Subject");
        StatusEffectManager sem = go.AddComponent<StatusEffectManager>();
        sem.Init(stats);
        return sem;
    }

    private static SpellData Spell(string id) => SpellDatabase.GetSpell(id).Clone();

    // ── BonusStacking, pure ─────────────────────────────────────────

    private static void TestDoesStackTypes()
    {
        Assert(!BonusTypeHelper.DoesStack(BonusType.Luck), "Luck bonuses do not stack with each other (PHB glossary p.310; no house rule)");
        Assert(!BonusTypeHelper.DoesStack(BonusType.Morale), "Morale bonuses do not stack (PHB glossary p.310)");
        Assert(BonusTypeHelper.DoesStack(BonusType.Dodge), "Dodge bonuses stack (PHB glossary p.307)");
        Assert(BonusTypeHelper.DoesStack(BonusType.Circumstance), "Circumstance bonuses from different circumstances stack (PHB glossary p.306)");
        Assert(BonusTypeHelper.DoesStack(BonusType.Untyped), "Untyped bonuses from different sources stack (PHB p.172)");
        Assert(!BonusTypeHelper.DoesStack(BonusType.Enhancement) && !BonusTypeHelper.DoesStack(BonusType.Deflection)
               && !BonusTypeHelper.DoesStack(BonusType.Insight) && !BonusTypeHelper.DoesStack(BonusType.Sacred),
            "Named types (enhancement, deflection, insight, sacred, ...) do not stack (PHB p.171)");
    }

    private static void TestTwoLuckBonuses()
    {
        AssertEq(Combine(B(BonusType.Luck, 1, "prayer"), B(BonusType.Luck, 2, "divine_favor")), 2,
            "Two luck bonuses (+1 Prayer, +2 Divine Favor): only the highest, +2");
        AssertEq(Combine(B(BonusType.Luck, 1, "stone"), B(BonusType.Luck, 1, "robe")), 1,
            "Two +1 luck bonuses from different items: +1");
    }

    private static void TestTwoMoraleBonuses()
    {
        AssertEq(Combine(B(BonusType.Morale, 1, "bless"), B(BonusType.Morale, 2, "heroism")), 2,
            "Two morale bonuses (+1 Bless, +2 Heroism): only the highest, +2");
        AssertEq(Combine(B(BonusType.Morale, 1, "bless"), B(BonusType.Luck, 1, "prayer")), 2,
            "A morale and a luck bonus: different types stack, +2");
    }

    private static void TestCircumstanceBonuses()
    {
        AssertEq(Combine(B(BonusType.Circumstance, 2, "magnifying_glass"), B(BonusType.Circumstance, 2, "higher_ground")), 4,
            "Two circumstance bonuses from different circumstances stack: +4");
        AssertEq(Combine(B(BonusType.Circumstance, 2, "visual_acuity"), B(BonusType.Circumstance, 1, "visual_acuity")), 2,
            "Two circumstance bonuses from essentially the same circumstance do not: +2");
    }

    private static void TestUntypedBonuses()
    {
        AssertEq(Combine(B(BonusType.Untyped, 2, "charge"), B(BonusType.Untyped, 1, "haste")), 3,
            "Untyped bonuses from different sources stack: +3");
        AssertEq(Combine(B(BonusType.Untyped, 2, "charge"), B(BonusType.Untyped, 2, "charge")), 2,
            "Untyped bonuses from the same source do not: +2");
    }

    private static void TestDodgeBonuses()
    {
        AssertEq(Combine(B(BonusType.Dodge, 1, "dodge_feat"), B(BonusType.Dodge, 1, "haste"), B(BonusType.Dodge, 2, "fighting_defensively")), 4,
            "Dodge bonuses from different sources stack: +4");
        AssertEq(Combine(B(BonusType.Dodge, 1, "haste"), B(BonusType.Dodge, 1, "haste")), 1,
            "Two dodge bonuses from the same effect (two hastes) count once: +1");
    }

    private static void TestPenalties()
    {
        AssertEq(Combine(B(BonusType.Morale, -1, "bane"), B(BonusType.Morale, -2, "crushing_despair")), -2,
            "Two penalties of one type: only the worst, -2 (PHB p.171)");
        AssertEq(Combine(B(BonusType.Untyped, -1, "prayer"), B(BonusType.Untyped, -2, "shaken")), -3,
            "Untyped penalties from different sources add up: -3");
        AssertEq(Combine(B(BonusType.Untyped, -1, "prayer"), B(BonusType.Untyped, -1, "prayer")), -1,
            "The same source's penalty counts once: -1");
        AssertEq(Combine(B(BonusType.Morale, 2, "heroism"), B(BonusType.Morale, -1, "bane")), 1,
            "A bonus and a penalty of one type both apply: +2 - 1 = +1");
    }

    private static void TestLedgerOwnersAndTargets()
    {
        var ledger = new BonusLedger();
        object first = new object(), second = new object();
        ledger.Add(first, BonusTarget.AttackRoll, BonusType.Luck, 3, "divine_favor", "Divine Favor");
        ledger.Add(second, BonusTarget.AttackRoll, BonusType.Luck, 1, "divine_favor", "Divine Favor");
        ledger.Add(second, BonusTarget.AllSaves, BonusType.Morale, 2, "heroism", "Heroism");
        AssertEq(ledger.Total(BonusTarget.AttackRoll), 3, "Ledger: two copies of one spell give the better one");
        ledger.RemoveOwner(first);
        AssertEq(ledger.Total(BonusTarget.AttackRoll), 1, "Ledger: the weaker copy applies again when the better one ends (PHB p.172)");
        AssertEq(ledger.Total(BonusTarget.Will, null, BonusTarget.AllSaves), 2, "Ledger: an all-saves entry reaches the Will save");
        ledger.Set("charge", BonusTarget.AttackRoll, BonusType.Untyped, 2, "Charge");
        AssertEq(ledger.Total(BonusTarget.AttackRoll), 3, "Ledger: the charge's untyped +2 stacks with a luck bonus");
        ledger.Set("charge", BonusTarget.AttackRoll, BonusType.Untyped, 0);
        AssertEq(ledger.Total(BonusTarget.AttackRoll), 1, "Ledger: Set to 0 removes the keyed entry");
    }

    // ── Spell effects (StatusEffectManager) ─────────────────────────

    private static void TestLuckSpellsDoNotStack()
    {
        CharacterStats s = Build("Cleric", 9);
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            ActiveSpellEffect favor = sem.AddEffect(Spell(SpellNames.DIVINE_FAVOR), "Tester", 9);
            ActiveSpellEffect prayer = sem.AddEffect(GameManager.PrayerEffectSpell(SpellDatabase.GetSpell(SpellNames.PRAYER), ally: true), "Tester", 5, 5);
            Assert(favor != null && prayer != null, "Divine Favor and Prayer are both active (different spells coexist)");
            AssertEq(s.EffectAttackBonus, 3, "Divine Favor +3 luck and Prayer +1 luck: attack +3, not +4 (PHB glossary p.310)");
            AssertEq(s.EffectWeaponDamageBonus, 3, "Divine Favor and Prayer: weapon damage +3, not +4");
            sem.RemoveEffect(favor);
            AssertEq(s.EffectAttackBonus, 1, "Divine Favor ended: Prayer's +1 luck applies again");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestMoraleSpellsDoNotStack()
    {
        CharacterStats s = Build();
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int willBare = s.WillSave;
            ActiveSpellEffect bless = sem.AddEffect(Spell(SpellNames.BLESS), "Tester", 3);
            ActiveSpellEffect heroism = sem.AddEffect(Spell(SpellNames.HEROISM), "Tester", 5);
            Assert(bless != null && heroism != null, "Bless and Heroism are both active");
            AssertEq(s.EffectAttackBonus, 2, "Bless +1 morale and Heroism +2 morale: attack +2, not +3 (PHB glossary p.310)");
            AssertEq(s.WillSave, willBare + 2, "Heroism: +2 morale on Will saves");
            sem.RemoveEffect(heroism);
            AssertEq(s.EffectAttackBonus, 1, "Heroism ended: Bless's +1 morale applies again");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestSameSpellTwice()
    {
        CharacterStats s = Build("Cleric", 9);
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            sem.AddEffect(Spell(SpellNames.BLESS), "A", 3);
            sem.AddEffect(Spell(SpellNames.BLESS), "B", 3);
            AssertEq(s.EffectAttackBonus, 1, "Two Bless spells give the benefit of one: +1 (PHB p.171)");
            sem.RemoveEffectsBySpellId(SpellNames.BLESS);

            ActiveSpellEffect weak = sem.AddEffect(Spell(SpellNames.DIVINE_FAVOR), "Low", 3);
            ActiveSpellEffect strong = sem.AddEffect(Spell(SpellNames.DIVINE_FAVOR), "High", 9);
            Assert(weak != null && strong != null, "A stronger Divine Favor of the same duration is not ignored");
            AssertEq(s.EffectAttackBonus, 3, "Divine Favor at caster levels 3 and 9: the stronger +3 applies, not +4");
            sem.RemoveEffectsBySpellId(SpellNames.DIVINE_FAVOR);
            AssertEq(s.EffectAttackBonus, 0, "Both Divine Favors removed: no bonus left");

            ActiveSpellEffect strongShort = sem.AddEffect(Spell(SpellNames.DIVINE_FAVOR), "High", 9, 2);
            ActiveSpellEffect weakLong = sem.AddEffect(Spell(SpellNames.DIVINE_FAVOR), "Low", 3, 10);
            Assert(strongShort != null && weakLong != null && sem.ActiveEffects.Count == 2,
                "A weaker but longer Divine Favor coexists with the stronger one (PHB p.172: both operate)");
            AssertEq(s.EffectAttackBonus, 3, "Both copies active: only the stronger +3 counts");
            sem.RemoveEffect(strongShort);
            AssertEq(s.EffectAttackBonus, 1, "The stronger copy ended: the weaker +1 remains for its own duration");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestInspireCourageAndCharge()
    {
        CharacterStats s = Build();
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int willBare = s.WillSave;
            s.ApplyInspireCourage(1);
            sem.AddEffect(Spell(SpellNames.BLESS), "Tester", 3);
            AssertEq(s.EffectAttackBonus, 1, "Inspire Courage +1 morale and Bless +1 morale: attack +1 (PHB p.29, p.171)");
            AssertEq(s.EffectWeaponDamageBonus, 1, "Inspire Courage: +1 morale on weapon damage");
            AssertEq(s.WillSave, willBare, "Inspire Courage: no bonus on an ordinary save (only against charm and fear, PHB p.29)");
            AssertEq(SaveRules.Modifier(s, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.CHARM_PERSON)), willBare + 1,
                "Inspire Courage: +1 morale on a save against a charm spell");
            AssertEq(SaveRules.Modifier(s, SavingThrowType.Will, SaveContext.Fear), willBare + 1,
                "Inspire Courage and Bless (both +1 morale against fear): +1 on a fear save, not +2");
            s.Bonuses.Set(CharacterStats.ChargeBonusSource, BonusTarget.AttackRoll, BonusType.Untyped, 2, "Charge");
            AssertEq(s.EffectAttackBonus, 3, "The charge's untyped +2 stacks with the morale bonus: +3 (PHB p.154)");
            s.Bonuses.Set(CharacterStats.ChargeBonusSource, BonusTarget.AttackRoll, BonusType.Untyped, 0);
            s.RemoveInspireCourage();
            AssertEq(s.EffectAttackBonus, 1, "Inspire Courage ended: Bless's +1 remains");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestPrayerPenaltyStacksWithBane()
    {
        CharacterStats s = Build();
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            SpellData prayer = SpellDatabase.GetSpell(SpellNames.PRAYER);
            sem.AddEffect(GameManager.PrayerEffectSpell(prayer, ally: false), "Tester", 5, 5);
            sem.AddEffect(Spell(SpellNames.BANE), "Tester", 3);
            AssertEq(s.EffectAttackBonus, -2, "Prayer's -1 and Bane's -1 (untyped, different spells) add up: -2");
            sem.AddEffect(GameManager.PrayerEffectSpell(prayer, ally: false), "Other", 5, 5);
            AssertEq(s.EffectAttackBonus, -2, "A second Prayer adds no further penalty (same spell)");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestLuckOnSaves()
    {
        CharacterStats s = Build();
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int bare = s.ReflexSave;
            var inv = new global::Inventory { OwnerStats = s };
            inv.DirectEquip(WondrousItemFactory.CreateStoneOfGoodLuck(), EquipSlot.Slotless);
            AssertEq(s.ReflexSave, bare + 1, "Stone of Good Luck: +1 luck on saves (DMG p.267)");
            sem.AddEffect(GameManager.PrayerEffectSpell(SpellDatabase.GetSpell(SpellNames.PRAYER), ally: true), "Tester", 5, 5);
            AssertEq(s.ReflexSave, bare + 1, "Prayer's +1 luck and the stone's +1 luck: +1, not +2");
            inv.DirectEquip(WondrousItemFactory.CreateRobeOfStars(), EquipSlot.Torso);
            AssertEq(s.ReflexSave, bare + 1, "Robe of Stars too: still +1 luck");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestFearMoraleBeyondGeneralMorale()
    {
        CharacterStats s = Build();
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int bare = s.WillSave;
            sem.AddEffect(Spell(SpellNames.HEROISM), "Tester", 5);
            sem.AddEffect(Spell(SpellNames.BLESS), "Tester", 3);
            AssertEq(SaveRules.Modifier(s, SavingThrowType.Will, SaveContext.Fear), bare + 2,
                "Heroism +2 morale and Bless +1 morale against fear: +2 on a fear save, not +3");
            s.RemoveFearMoraleBonus = 4; // what the Remove Fear handler sets (PHB p.271)
            AssertEq(SaveRules.Modifier(s, SavingThrowType.Will, SaveContext.Fear), bare + 4,
                "Remove Fear's +4 morale against fear replaces Heroism's +2 there: +4");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestSkillBonuses()
    {
        CharacterStats s = Build();
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int bare = s.GetSkillBonus("Climb");
            sem.AddEffect(Spell(SpellNames.HEROISM), "Tester", 5);
            AssertEq(s.GetSkillBonus("Climb"), bare + 2, "Heroism: +2 morale on skill checks (PHB p.240)");
            sem.AddEffect(GameManager.PrayerEffectSpell(SpellDatabase.GetSpell(SpellNames.PRAYER), ally: true), "Tester", 5, 5);
            AssertEq(s.GetSkillBonus("Climb"), bare + 3, "Heroism (morale) and Prayer (luck) on a skill check: +3");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestSpeedEnhancement()
    {
        CharacterStats s = Build();
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int bare = s.EffectiveSpeedFeet;
            sem.AddEffect(Spell(SpellNames.LONGSTRIDER), "Tester", 3);
            AssertEq(s.EffectiveSpeedFeet, bare + 10, "Longstrider: +10 ft enhancement (PHB p.249)");
            sem.AddEffect(Spell(SpellNames.EXPEDITIOUS_RETREAT), "Tester", 3);
            AssertEq(s.EffectiveSpeedFeet, bare + 30, "Longstrider and Expeditious Retreat: +30 ft, not +40 (both enhancement)");
            sem.RemoveEffectsBySpellId(SpellNames.EXPEDITIOUS_RETREAT);
            s.WondrousSpeedBonus = 10;
            AssertEq(s.EffectiveSpeedFeet, bare + 10, "Longstrider and boots of striding and springing (+10 ft each): +10 ft");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestAbilityEnhancement()
    {
        CharacterStats s = Build(str: 14);
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int bare = s.EffectiveStrengthScore;
            sem.AddEffect(Spell(SpellNames.BULLS_STRENGTH), "Tester", 3);
            AssertEq(s.EffectiveStrengthScore, bare + 4, "Bull's Strength: +4 enhancement to Strength (PHB p.207)");
            s.WondrousEnhancementSTR = 4;
            AssertEq(s.EffectiveStrengthScore, bare + 4, "Bull's Strength and a +4 Strength item: +4, not +8 (PHB p.172)");
            s.WondrousEnhancementSTR = 6;
            AssertEq(s.EffectiveStrengthScore, bare + 6, "A +6 Strength item with Bull's Strength: the better +6");
            s.WondrousEnhancementSTR = 0;
            s.TemporarySTRBonus = 5;
            AssertEq(s.EffectiveStrengthScore, bare + 5, "Strength domain +5 (enhancement, PHB p.188) with Bull's Strength: +5");
            s.TemporarySTRBonus = 0;
            sem.RemoveEffectsBySpellId(SpellNames.BULLS_STRENGTH);
            AssertEq(s.EffectiveStrengthScore, bare, "Bull's Strength ended: Strength back to its score");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestHasteAndMageArmor()
    {
        CharacterStats s = Build();
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int acBare = s.ArmorClass;
            sem.AddEffect(Spell(SpellNames.MAGE_ARMOR), "Tester", 3);
            AssertEq(s.ArmorClass, acBare + 4, "Mage Armor: +4 armor bonus");
            sem.AddEffect(Spell(SpellNames.HASTE), "Tester", 5);
            AssertEq(s.SpellACBonus, 4, "Haste does not replace Mage Armor's armor bonus (SPL-025)");
            AssertEq(s.EffectAttackBonus, 0, "Haste's tracked effect adds no attack bonus of its own (its +1 is HasteAttackBonus; SPL-024)");
            AssertEq(s.EffectSaveBonus(SavingThrowType.Reflex), 0, "Haste's tracked effect adds no save bonus of its own (its +1 dodge on Reflex is HasteReflexBonus)");
            AssertEq(s.LandSpeedEnhancementBonusFeet, 30, "Haste: +30 ft enhancement bonus to speed");
        }
        finally { Object.DestroyImmediate(go); }
    }
    private static void TestMonkFastMovementIsEnhancement()
    {
        CharacterStats s = Build("Monk", 3);
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int baseFeet = s.BaseSpeed * 5;
            AssertEq(s.EffectiveSpeedFeet, baseFeet + 10, "Monk 3 unarmored: fast movement +10 ft (PHB p.41)");
            sem.AddEffect(Spell(SpellNames.HASTE), "Tester", 5);
            AssertEq(s.EffectiveSpeedFeet, baseFeet + 30,
                "Monk 3 with Haste: +30 ft, not +40 (fast movement is an enhancement bonus, PHB p.41; Haste's is too, p.239)");
            sem.RemoveEffectsBySpellId(SpellNames.HASTE);
            AssertEq(s.EffectiveSpeedFeet, baseFeet + 10, "Haste ended: the monk's +10 ft applies again");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestRageSpell()
    {
        CharacterStats s = Build(str: 14);
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int str = s.STR, con = s.CON, willBare = s.WillSave, acBare = s.ArmorClass;
            ActiveSpellEffect rage = sem.AddEffect(Spell(SpellNames.RAGE), "Tester", 5, 5);
            Assert(rage != null, "Rage is applied");
            AssertEq(s.STR, str + 2, "Rage: +2 morale to Strength (PHB p.268)");
            AssertEq(s.CON, con + 2, "Rage: +2 morale to Constitution");
            AssertEq(s.WillSave, willBare + 1, "Rage: +1 morale on Will saves");
            AssertEq(s.ArmorClass, acBare - 2, "Rage: -2 to AC");
            AssertEq(s.EffectSaveBonus(SavingThrowType.Fortitude), 0, "Rage: no bonus on Fortitude saves (Will only)");
            sem.AddEffect(Spell(SpellNames.RAGE), "Other", 5, 5);
            AssertEq(s.STR, str + 2, "A second Rage adds no more Strength (the same spell does not stack with itself, PHB p.171)");
            sem.AddEffect(Spell(SpellNames.HEROISM), "Tester", 5);
            AssertEq(s.WillSave, willBare + 2, "Rage +1 morale and Heroism +2 morale on Will: +2 (morale bonuses do not stack)");
            sem.RemoveEffectsBySpellId(SpellNames.HEROISM);
            sem.RemoveEffectsBySpellId(SpellNames.RAGE);
            AssertEq(s.STR, str, "Rage ended: Strength back to its score");
            AssertEq(s.CON, con, "Rage ended: Constitution back to its score");
            AssertEq(s.WillSave, willBare, "Rage ended: no Will bonus left");
            AssertEq(s.ArmorClass, acBare, "Rage ended: AC back");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestBaneSavePenaltyOnlyAgainstFear()
    {
        CharacterStats s = Build();
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            int willBare = s.WillSave;
            sem.AddEffect(Spell(SpellNames.BANE), "Tester", 3);
            AssertEq(s.WillSave, willBare, "Bane: no penalty on an ordinary save (only against fear, PHB p.203)");
            AssertEq(SaveRules.Modifier(s, SavingThrowType.Will, SaveContext.Fear), willBare - 1, "Bane: -1 on a save against fear");
            sem.AddEffect(GameManager.PrayerEffectSpell(SpellDatabase.GetSpell(SpellNames.PRAYER), ally: false), "Tester", 5, 5);
            AssertEq(s.WillSave, willBare - 1, "Prayer's -1 applies to every save");
            AssertEq(SaveRules.Modifier(s, SavingThrowType.Will, SaveContext.Fear), willBare - 2,
                "Bane's -1 against fear and Prayer's -1 (untyped, different spells): -2 on a fear save");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestShieldOfFaithCopies()
    {
        CharacterStats s = Build("Cleric", 12);
        StatusEffectManager sem = Effects(s, out GameObject go);
        try
        {
            SpellData strong = Spell(SpellNames.SHIELD_OF_FAITH);
            strong.BuffDeflectionBonus = 4; // caster level 12 (PHB p.278)
            SpellData weak = Spell(SpellNames.SHIELD_OF_FAITH);
            weak.BuffDeflectionBonus = 2;   // caster level 1
            ActiveSpellEffect strongShort = sem.AddEffect(strong, "High", 12, 3);
            ActiveSpellEffect weakLong = sem.AddEffect(weak, "Low", 1, 10);
            Assert(strongShort != null && weakLong != null, "A weaker but longer Shield of Faith coexists with the stronger one (PHB p.172)");
            AssertEq(s.DeflectionBonus, 4, "Both Shields of Faith active: the stronger +4 deflection applies");
            sem.RemoveEffect(strongShort);
            AssertEq(s.DeflectionBonus, 2, "The stronger copy ended: the weaker +2 remains");
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void TestSkillRollMatchesSkillBonus()
    {
        CharacterStats s = Build();
        StatusEffectManager sem = Effects(s, out GameObject go);
        Random.State saved = Random.state;
        try
        {
            sem.AddEffect(Spell(SpellNames.JUMP), "Tester", 1);
            sem.AddEffect(Spell(SpellNames.HEROISM), "Tester", 5);
            Random.InitState(424242);
            int roll = s.RollSkillCheck("Jump");
            Random.InitState(424242);
            int d20 = Random.Range(1, 21);
            AssertEq(roll - d20, s.GetSkillBonus("Jump"),
                "RollSkillCheck and GetSkillBonus agree under the Jump spell and Heroism (one shared spell and effect term)");
            Assert(s.SpellAndEffectSkillModifier("Jump") >= 12, "The Jump spell's +10 and Heroism's +2 reach the Jump roll",
                $"got {s.SpellAndEffectSkillModifier("Jump")}");
        }
        finally
        {
            Random.state = saved;
            Object.DestroyImmediate(go);
        }
    }
}
}

using System.Collections.Generic;
using System.Reflection;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Classes
{
/// <summary>
/// CHR-003 and CRE-042: the monk's class features by monk level, one rules core for PCs and NPC monks (PHB p.39-42,
/// Table 3-10 and 3-11; PHB p.101 Stunning Fist). Flurry of blows: the penalty (-2 to 4th level, -1 at 5th-8th, none
/// from 9th) on every attack of the round and on attacks of opportunity until the monk's next turn, the iterative attacks
/// from the base attack bonus plus the extra attack at the highest (two from 11th), declared before the first attack as a
/// full attack, through the shared attack sequence (CharacterCombatStats steps, TryCommitAttack,
/// ResolveAttackSequenceStep, FullAttack), with unarmed strikes or special monk weapons only, the full Strength bonus on
/// damage, no off-hand attacks. Unarmed damage by monk level and size. AC bonus (Wisdom plus the level bonus; lost in
/// armor, with a shield or a medium or heavy load, and while helpless or pinned) and fast movement; bracers of armor
/// are not armor. The NPC monk entries carry no baked speed, natural unarmed attack or natural armor (CRE-042), and the
/// AI declares the flurry through the same core. Needs Play mode (CharacterController.Init, TST-009; NPC spawns).
/// </summary>
public static class MonkClassRulesTests
{
    private static int _passed;
    private static int _failed;
    private static readonly Vector2Int SpawnPos = new Vector2Int(-70, -70);

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== MONK CLASS RULES TESTS (CHR-003, CRE-042) ==========");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();
        NPCDatabase.Init();

        TestFlurryPenaltyByLevel();
        TestFlurryTableColumn();
        TestMultiClassFlurryUsesTotalBab();
        TestUnarmedDamageByLevel();
        TestUnarmedDamageBySize();
        TestAcBonusByLevel();
        TestAcBonusLostByLoadAndConditions();
        TestFastMovementByLevel();
        TestSpecialMonkWeapons();
        TestArmorShieldAndBracers();
        TestFlurryThroughSharedSequence();
        TestFlurryFullAttackAndPenaltyUntilNextTurn();
        TestFlurryRefusals();
        TestGrappleStartedMidFlurry();
        TestNpcMonkEntries();
        TestNpcMonkSpawnedFeatures();
        TestAiFlurryChoice();

        Debug.Log($"========== RESULTS: {_passed} passed, {_failed} failed ==========");
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

    private static void AssertEq(int got, int want, string testName) => Assert(got == want, testName, $"got {got}, expected {want}");

    private static CharacterStats Build(int level, string cls = "Monk", int wis = 14, int str = 12, string race = "Human")
    {
        var stats = new CharacterStats(
            name: cls + level, level: level, characterClass: cls,
            str: str, dex: 12, con: 12, wis: wis, intelligence: 10, cha: 10,
            bab: 0, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: level * 8, raceName: race);
        stats.EnsureMulticlassDataInitialized();
        return stats;
    }

    private static CharacterController Create(string name, int level, Vector2Int pos, string cls = "Monk", int wis = 14,
        int str = 16, string race = "Human")
    {
        var go = new GameObject(name + "_MonkRulesTest");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        CharacterStats stats = Build(level, cls, wis, str, race);
        stats.CharacterName = name;
        controller.Init(stats, pos, null, null);
        inventory.Init(stats);
        controller.GridPosition = pos;
        return controller;
    }

    /// <summary>A target with plenty of hit points and AC 10, so repeated attacks never kill it.</summary>
    private static CharacterController CreateTarget(Vector2Int pos)
    {
        CharacterController t = Create("Target", 3, pos, "Fighter", 10, 10);
        t.Stats.AdjustMaxHP(1000);
        t.Stats.CurrentHP += 1000;
        return t;
    }

    private static ItemData Equip(CharacterController c, ItemData item, EquipSlot slot)
    {
        InventoryComponent inv = c.GetComponent<InventoryComponent>();
        inv.CharacterInventory.DirectEquip(item, slot);
        inv.CharacterInventory.RecalculateStats();
        return item;
    }

    private static void Unequip(CharacterController c, EquipSlot slot)
    {
        global::Inventory inv = c.GetComponent<InventoryComponent>().CharacterInventory;
        switch (slot)
        {
            case EquipSlot.Armor: inv.ArmorRobeSlot = null; break;
            case EquipSlot.LeftHand: inv.LeftHandSlot = null; break;
            case EquipSlot.RightHand: inv.RightHandSlot = null; break;
            case EquipSlot.Wrists: inv.WristsSlot = null; break;
        }
        inv.RecalculateStats();
    }

    private static void Cleanup(params CharacterController[] controllers)
    {
        GameManager gm = GameManager.Instance;
        foreach (CharacterController c in controllers)
        {
            if (c == null)
                continue;
            if (gm != null && gm.Grid != null)
                gm.Grid.ClearCreatureOccupancy(c);
            Object.DestroyImmediate(c.gameObject);
        }
    }

    private static string Join(int[] values) => string.Join("/", System.Array.ConvertAll(values, CharacterStats.FormatMod));

    // ── Pure rules ──────────────────────────────────────────────────

    // PHB p.40: -2 through 4th level, -1 at 5th, none at 9th.
    private static void TestFlurryPenaltyByLevel()
    {
        int[] want = { -2, -2, -2, -2, -1, -1, -1, -1, 0, 0, 0, 0 };
        string got = string.Empty;
        bool ok = true;
        for (int level = 1; level <= 12; level++)
        {
            int p = Build(level).FlurryOfBlowsAttackPenalty;
            got += (level > 1 ? "," : "") + p;
            ok &= p == want[level - 1];
        }
        Assert(ok, "Flurry penalty: -2 at 1st-4th, -1 at 5th-8th, 0 from 9th (PHB p.40)", got);
        AssertEq(Build(5, "Fighter").FlurryOfBlowsAttackPenalty, 0, "A non-monk has no flurry penalty");
    }

    // PHB Table 3-10, Flurry of Blows Attack Bonus column.
    private static void TestFlurryTableColumn()
    {
        var table = new Dictionary<int, int[]>
        {
            { 1, new[] { -2, -2 } }, { 2, new[] { -1, -1 } }, { 3, new[] { 0, 0 } }, { 4, new[] { 1, 1 } },
            { 5, new[] { 2, 2 } }, { 6, new[] { 3, 3 } }, { 7, new[] { 4, 4 } }, { 8, new[] { 5, 5, 0 } },
            { 9, new[] { 6, 6, 1 } }, { 10, new[] { 7, 7, 2 } }, { 11, new[] { 8, 8, 8, 3 } },
            { 15, new[] { 11, 11, 11, 6, 1 } }, { 20, new[] { 15, 15, 15, 10, 5 } }
        };
        foreach (KeyValuePair<int, int[]> row in table)
        {
            int[] got = Build(row.Key).GetFlurryOfBlowsModifiedBaseAttackBonuses();
            Assert(Join(got) == Join(row.Value), $"Monk {row.Key} flurry {Join(row.Value)} (PHB Table 3-10)", $"got {Join(got)}");
        }
        Assert(Build(4, "Fighter").GetFlurryOfBlowsBaseAttackBonuses().Length == 0, "A non-monk has no flurry steps");
    }

    // Delegated RAW reading 2026-10-14: the extra attack is at the character's highest base attack bonus (every class).
    private static void TestMultiClassFlurryUsesTotalBab()
    {
        CharacterStats s = Build(2);
        s.ClassLevels.Add(new ClassLevelEntry("Fighter", 4));
        s.HitDice = 0;
        s.InvalidateClassLevelCache();
        s.EnsureMulticlassDataInitialized();
        // Monk 2 (+1) and fighter 4 (+4): BAB +5; the monk 2 penalty -2: +3/+3.
        AssertEq(s.BaseAttackBonus, 5, "Monk 2 / fighter 4 has BAB +5");
        Assert(Join(s.GetFlurryOfBlowsModifiedBaseAttackBonuses()) == "+3/+3",
            "Monk 2 / fighter 4 flurries at +3/+3: total BAB, the monk-level penalty", Join(s.GetFlurryOfBlowsModifiedBaseAttackBonuses()));
    }

    private static string Damage(CharacterStats s)
    {
        bool monk = s.TryGetMonkUnarmedMediumDamage(out int count, out int dice);
        return (monk ? "" : "non-monk ") + count + "d" + dice;
    }

    // PHB Table 3-10, Unarmed Damage (Medium).
    private static void TestUnarmedDamageByLevel()
    {
        var table = new Dictionary<int, string>
        {
            { 1, "1d6" }, { 3, "1d6" }, { 4, "1d8" }, { 7, "1d8" }, { 8, "1d10" }, { 11, "1d10" },
            { 12, "2d6" }, { 15, "2d6" }, { 16, "2d8" }, { 19, "2d8" }, { 20, "2d10" }
        };
        foreach (KeyValuePair<int, string> row in table)
            Assert(Damage(Build(row.Key)) == row.Value, $"Monk {row.Key} Medium unarmed damage {row.Value} (PHB Table 3-10)", Damage(Build(row.Key)));
        Assert(Damage(Build(4, "Fighter")) == "non-monk 1d3", "A non-monk has no monk unarmed damage (1d3)", Damage(Build(4, "Fighter")));
    }

    // PHB Table 3-11 (Small and Large monks) is the weapon size table applied to Table 3-10 (DMG p.28); and the
    // controller's unarmed strike resizes by the monk's size (a halfling monk 4 deals 1d6).
    private static void TestUnarmedDamageBySize()
    {
        string[] medium = { "1d6", "1d8", "1d10", "2d6", "2d8", "2d10" };
        string[] small = { "1d4", "1d6", "1d8", "1d10", "2d6", "2d8" };
        string[] large = { "1d8", "2d6", "2d8", "3d6", "3d8", "4d8" };
        bool ok = true;
        string got = string.Empty;
        for (int i = 0; i < medium.Length; i++)
        {
            string s = WeaponDamageScaler.ScaleDamageExpression(medium[i], SizeCategory.Medium, SizeCategory.Small);
            string l = WeaponDamageScaler.ScaleDamageExpression(medium[i], SizeCategory.Medium, SizeCategory.Large);
            got += $"{medium[i]}->{s}/{l} ";
            ok &= s == small[i] && l == large[i];
        }
        Assert(ok, "Small and Large monk unarmed damage follow PHB Table 3-11", got);

        CharacterController halfling = null;
        try
        {
            halfling = Create("HalflingMonk", 4, new Vector2Int(-80, -80), race: "Halfling");
            var unarmed = halfling.GetUnarmedDamage();
            Assert(halfling.Stats.CurrentSizeCategory == SizeCategory.Small && unarmed.damageCount == 1 && unarmed.damageDice == 6,
                "A Small (halfling) monk 4 deals 1d6 unarmed (PHB Table 3-11)", $"{halfling.Stats.CurrentSizeCategory} {unarmed.damageCount}d{unarmed.damageDice}");
        }
        finally { Cleanup(halfling); }
    }

    // PHB p.41: Wisdom bonus plus +1 at 5th, +2 at 10th, +4 at 20th; in touch AC too.
    private static void TestAcBonusByLevel()
    {
        AssertEq(Build(1, wis: 14).MonkACBonus, 2, "Monk 1, WIS 14: AC bonus +2 (Wisdom only)");
        AssertEq(Build(4, wis: 14).MonkACBonus, 2, "Monk 4, WIS 14: +2");
        AssertEq(Build(5, wis: 14).MonkACBonus, 3, "Monk 5, WIS 14: +3 (+1 at 5th)");
        AssertEq(Build(10, wis: 14).MonkACBonus, 4, "Monk 10, WIS 14: +4 (+2 at 10th)");
        AssertEq(Build(20, wis: 14).MonkACBonus, 6, "Monk 20, WIS 14: +6 (+4 at 20th)");
        AssertEq(Build(5, wis: 8).MonkACBonus, 1, "Monk 5, WIS 8: a Wisdom penalty is not added; the level +1 still is");
        AssertEq(Build(5, "Fighter", wis: 14).MonkACBonus, 0, "A non-monk has no monk AC bonus");

        CharacterStats monk = Build(5, wis: 14);
        CharacterStats plain = Build(5, wis: 14);
        plain.ClassLevels.Clear();
        plain.ClassLevels.Add(new ClassLevelEntry("Fighter", 5));
        plain.CharacterClass = "Fighter";
        plain.InvalidateClassLevelCache();
        AssertEq(monk.TouchArmorClass - plain.TouchArmorClass, 3, "The monk AC bonus applies to touch AC (PHB p.41)");
    }

    private static void TestAcBonusLostByLoadAndConditions()
    {
        CharacterStats s = Build(5, wis: 14);
        s.CurrentEncumbrance = EncumbranceLevel.Medium;
        AssertEq(s.MonkACBonus, 0, "A medium load removes the monk AC bonus (PHB p.41)");
        AssertEq(s.MonkFastMovementBonusFeet, 0, "A medium load removes fast movement (PHB p.41)");
        s.CurrentEncumbrance = EncumbranceLevel.Light;
        AssertEq(s.MonkACBonus, 3, "Back to a light load: +3");

        s.ApplyCondition(CombatConditionType.Paralyzed, 3, "test");
        AssertEq(s.MonkACBonus, 0, "A helpless (paralyzed) monk loses the AC bonus (PHB p.41)");
        s.RemoveCondition(CombatConditionType.Paralyzed);
        s.ApplyCondition(CombatConditionType.Pinned, 3, "test");
        AssertEq(s.MonkACBonus, 0, "A pinned monk is immobilized and loses the AC bonus (delegated RAW reading 2026-10-14)");
        s.RemoveCondition(CombatConditionType.Pinned);
        s.ApplyCondition(CombatConditionType.FlatFooted, 1, "test");
        AssertEq(s.MonkACBonus, 3, "A flat-footed monk keeps the AC bonus (PHB p.41)");
        s.RemoveCondition(CombatConditionType.FlatFooted);
    }

    // PHB Table 3-10 Unarmored Speed Bonus: +10 ft at 3rd, +20 at 6th ... +60 at 18th; an enhancement bonus.
    private static void TestFastMovementByLevel()
    {
        var table = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 10 }, { 5, 10 }, { 6, 20 }, { 8, 20 }, { 9, 30 }, { 12, 40 }, { 15, 50 }, { 18, 60 }, { 20, 60 } };
        foreach (KeyValuePair<int, int> row in table)
            AssertEq(Build(row.Key).MonkFastMovementBonusFeet, row.Value, $"Monk {row.Key} fast movement +{row.Value} ft (PHB Table 3-10)");
        CharacterStats m6 = Build(6);
        AssertEq(m6.EffectiveSpeedFeet, 50, "Monk 6, human: 30 + 20 = 50 ft");
    }

    private static void TestSpecialMonkWeapons()
    {
        Assert(CharacterStats.IsSpecialMonkWeapon(ItemDatabase.CloneItem(ItemID.WeaponQuarterstaff)), "A quarterstaff is a special monk weapon (PHB p.40)");
        Assert(!CharacterStats.IsSpecialMonkWeapon(ItemDatabase.CloneItem(ItemID.WeaponLongsword)), "A longsword is not");
        Assert(CharacterStats.IsSpecialMonkWeapon(new ItemData { Id = "kama_masterwork", Name = "Masterwork Kama" })
            && CharacterStats.IsSpecialMonkWeapon(new ItemData { Id = "x", Name = "+1 Siangham" })
            && !CharacterStats.IsSpecialMonkWeapon(new ItemData { Id = "saif", Name = "Saif" }),
            "Monk weapons match a whole word of the id or name (kama, siangham; not 'saif')");
    }

    // ── Equipment (Play) ────────────────────────────────────────────

    // PHB p.40-41: armor or a shield removes the AC bonus, fast movement and flurry; bracers of armor are not armor
    // (DMG p.250), so they keep them while their armor bonus counts.
    private static void TestArmorShieldAndBracers()
    {
        CharacterController m = null;
        try
        {
            m = Create("ArmoredMonk", 6, new Vector2Int(-80, -82), wis: 14);
            int baseAC = m.Stats.ArmorClass;
            AssertEq(m.Stats.MonkACBonus, 3, "Monk 6, WIS 14, unarmored: +3");
            AssertEq(m.Stats.MonkFastMovementBonusFeet, 20, "Monk 6 unarmored: +20 ft");

            Equip(m, ItemDatabase.CloneItem(ItemID.ArmorLeather), EquipSlot.Armor);
            Assert(m.Stats.IsWearingArmor && m.Stats.MonkACBonus == 0 && m.Stats.MonkFastMovementBonusFeet == 0
                && !m.CanUseFlurryOfBlows(out _),
                "In leather armor the monk loses her AC bonus, fast movement and flurry (PHB p.40)",
                $"armor {m.Stats.IsWearingArmor}, AC bonus {m.Stats.MonkACBonus}, speed +{m.Stats.MonkFastMovementBonusFeet}");
            Unequip(m, EquipSlot.Armor);

            Equip(m, ItemDatabase.CloneItem(ItemID.ShieldLightWooden), EquipSlot.LeftHand);
            Assert(m.Stats.IsCarryingShield && m.Stats.MonkACBonus == 0 && !m.CanUseFlurryOfBlows(out _),
                "With a shield the monk loses her AC bonus and flurry (PHB p.40)", $"AC bonus {m.Stats.MonkACBonus}");
            Unequip(m, EquipSlot.LeftHand);

            Equip(m, WondrousItemFactory.CreateBracersOfArmor(2), EquipSlot.Wrists);
            Assert(!m.Stats.IsWearingArmor && m.Stats.MonkACBonus == 3 && m.Stats.MonkFastMovementBonusFeet == 20
                && m.CanUseFlurryOfBlows(out _),
                "Bracers of armor +2 keep the monk AC bonus, fast movement and flurry: they are not armor (CHR-003)",
                $"armor {m.Stats.IsWearingArmor}, AC bonus {m.Stats.MonkACBonus}, speed +{m.Stats.MonkFastMovementBonusFeet}");
            AssertEq(m.Stats.ArmorClass - baseAC, 2, "Bracers of armor +2 add their armor bonus on top of the monk AC");
            Unequip(m, EquipSlot.Wrists);
        }
        finally { Cleanup(m); }
    }

    // ── Flurry through the shared attack sequence (Play) ────────────

    // The NPC path: declare, then TryCommitAttack / ResolveAttackSequenceStep per step (CharacterCombatStats steps).
    private static void TestFlurryThroughSharedSequence()
    {
        CharacterController m = null, t = null;
        try
        {
            m = Create("SequenceMonk", 3, new Vector2Int(-80, -84));
            t = CreateTarget(new Vector2Int(-79, -84));
            AssertEq(m.GetIterativeAttackCount(), 1, "Monk 3 (BAB +2) has one iterative attack");
            Assert(m.TryBeginFlurryOfBlows(out string reason), "Monk 3 declares a flurry of blows", reason);
            Assert(m.ProgressiveAttackPool.IsFullAttack && !m.Actions.HasFullRoundAction && !m.Actions.HasMoveAction,
                "Declaring the flurry makes the turn a full attack and spends the full-round action (PHB p.40, p.143)");
            AssertEq(m.GetIterativeAttackCount(), 2, "Flurrying: two attack steps (the extra attack)");
            AssertEq(m.GetPlannedFullAttackCount(), 2, "The PC full attack sequence plans the flurry's two attacks");
            // One ladder (CMB-044): the flurry's steps are the iterative ladder's +2 with the extra attack at +2, and the
            // planned count and FullAttack read the same list (CharacterCombatStats.GetAttackBonuses).
            var flurrySteps = m.GetAttackBonuses();
            Assert(flurrySteps.Count == 2 && flurrySteps[0] == 2 && flurrySteps[1] == 2,
                "The flurry's steps are +2/+2 (the one iterative ladder plus the extra attack, PHB p.40)",
                $"got [{string.Join(",", flurrySteps)}]");
            // STR 16 (+3), Medium: a trip or grapple touch attack in the flurry at BAB +2 takes the -2 too (PHB p.40).
            AssertEq(m.GetManeuverMeleeTouchAttackModifier(2), 2 + 3 - 2, "A maneuver's touch attack in the flurry takes the flurry penalty");

            Assert(m.TryCommitAttack(AttackStepKind.MainHand, out int step0, out _) && step0 == 0, "Flurry step 1 commits");
            AssertEq(m.GetMainHandAttackStepBAB(step0), 2, "Flurry step 1 at BAB +2");
            CombatResult r0 = m.ResolveAttackSequenceStep(t, AttackStepKind.MainHand, step0, false, 0, null, null, null, 0, out string label0);
            Assert(r0 != null && label0 != null && label0.StartsWith("Flurry 1"), "The step resolves as a flurry attack", label0);
            Assert(r0 != null && r0.BreakdownBAB == 2 && HasModifier(r0, "Flurry of Blows", -2),
                "The step rolls at BAB +2 with the -2 flurry penalty", r0 != null ? r0.BreakdownBAB.ToString() : "null");

            Assert(m.TryCommitAttack(AttackStepKind.MainHand, out int step1, out _) && step1 == 1, "Flurry step 2 commits (the extra attack)");
            AssertEq(m.GetMainHandAttackStepBAB(step1), 2, "The extra attack is at the highest BAB, +2");
            Assert(!m.CanCommitAttack(AttackStepKind.MainHand, out _), "No third step at monk 3");
            Assert(!m.CanCommitAttack(AttackStepKind.OffHand, out string offReason), "A flurry has no off-hand attack", offReason);
        }
        finally { Cleanup(m, t); }
    }

    private static bool HasModifier(CombatResult r, string label, int value)
    {
        if (r == null || r.AttackBuffDebuffModifiers == null)
            return false;
        foreach (AttackModifierBreakdownEntry e in r.AttackBuffDebuffModifiers)
        {
            if (e.Label == label && e.Value == value)
                return true;
        }
        return false;
    }

    // Monk 8 (BAB +6/+1): +5/+5/+0 with the full Strength bonus, and the -1 on an attack of opportunity before her next
    // turn; gone after StartNewTurn (PHB p.40-41).
    private static void TestFlurryFullAttackAndPenaltyUntilNextTurn()
    {
        CharacterController m = null, t = null;
        try
        {
            m = Create("FlurryMonk", 8, new Vector2Int(-80, -86), str: 16);
            t = CreateTarget(new Vector2Int(-79, -86));
            Equip(m, ItemDatabase.CloneItem(ItemID.WeaponQuarterstaff), EquipSlot.RightHand);
            FullAttackResult f = m.FlurryOfBlows(t, false, 0, null);
            Assert(f != null && f.Attacks.Count == 3, "Monk 8 flurries three times (+6/+6/+1 BAB)", f != null ? f.Attacks.Count.ToString() : "null");
            if (f != null && f.Attacks.Count == 3)
            {
                Assert(f.Attacks[0].BreakdownBAB == 6 && f.Attacks[1].BreakdownBAB == 6 && f.Attacks[2].BreakdownBAB == 1,
                    "The flurry's steps are BAB +6, +6, +1", $"{f.Attacks[0].BreakdownBAB}/{f.Attacks[1].BreakdownBAB}/{f.Attacks[2].BreakdownBAB}");
                bool penalty = true, strength = true;
                foreach (CombatResult a in f.Attacks)
                {
                    penalty &= HasModifier(a, "Flurry of Blows", -1);
                    strength &= a.HasWeaponDamageBonus && a.WeaponDamageBonus.StrengthBonus == 3;
                }
                Assert(penalty, "Every flurry attack takes the monk 8 penalty -1 (PHB p.40)");
                Assert(strength, "A two-handed quarterstaff flurry adds STR x1 (+3), not x1-1/2 (PHB p.41)");
                Assert(f.AttackLabels.Count == 3 && f.AttackLabels[0].StartsWith("Flurry 1 (+8)"),
                    "Labels show the flurry bonus: BAB 6, flurry -1, STR +3 (+8)", f.AttackLabels.Count > 0 ? f.AttackLabels[0] : "none");
            }

            CombatResult aoo = m.Attack(t, false, 0, null);
            Assert(HasModifier(aoo, "Flurry of Blows", -1) && aoo.HasWeaponDamageBonus && aoo.WeaponDamageBonus.StrengthBonus == 4,
                "An attack before her next turn (an attack of opportunity) takes the -1 too, with the normal two-handed STR (PHB p.40)",
                aoo != null && aoo.HasWeaponDamageBonus ? aoo.WeaponDamageBonus.Describe() : "null");

            m.StartNewTurn();
            Assert(!m.IsUsingFlurryOfBlows, "The flurry ends at the start of the monk's next turn");
            CombatResult next = m.Attack(t, false, 0, null);
            Assert(!HasModifier(next, "Flurry of Blows", -1), "Her next turn's attack has no flurry penalty");
        }
        finally { Cleanup(m, t); }
    }

    private static void TestFlurryRefusals()
    {
        CharacterController m = null, fighter = null, t = null;
        try
        {
            m = Create("RefusedMonk", 3, new Vector2Int(-80, -88));
            t = CreateTarget(new Vector2Int(-79, -88));
            fighter = Create("NotAMonk", 3, new Vector2Int(-82, -88), "Fighter");
            Assert(!fighter.CanUseFlurryOfBlows(out _), "A non-monk cannot flurry");

            Equip(m, ItemDatabase.CloneItem(ItemID.WeaponLongsword), EquipSlot.RightHand);
            Assert(!m.CanUseFlurryOfBlows(out string weaponReason), "A flurry with a longsword is refused (PHB p.40)", weaponReason);
            Unequip(m, EquipSlot.RightHand);
            Assert(m.CanUseFlurryOfBlows(out _), "Unarmed, the monk may flurry");

            m.Attack(t, false, 0, null);
            m.RegisterAttackMade(AttackStepKind.MainHand);
            Assert(!m.CanUseFlurryOfBlows(out string afterReason), "After the first attack of the turn the flurry can no longer be declared", afterReason);

            m.StartNewTurn();
            m.Actions.UseMoveAction();
            Assert(!m.CanUseFlurryOfBlows(out string moveReason), "After a move action there is no full-round action for a flurry", moveReason);
        }
        finally { Cleanup(m, fighter, t); }
    }

    // A flurry step replaced by a grapple that succeeds (delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1): the
    // flurry's extra attack ends, the remaining grapple actions follow the plain BAB ladder (PHB p.156), and the flurry
    // penalty stays on her grapple attack rolls (PHB p.40). Monk 8: flurry +6/+6/+1 BAB, plain ladder +6/+1, penalty -1.
    private static void TestGrappleStartedMidFlurry()
    {
        CharacterController m = null, t = null;
        try
        {
            m = Create("GrappleMonk", 8, new Vector2Int(-80, -90));
            t = CreateTarget(new Vector2Int(-79, -90));
            Assert(m.TryBeginFlurryOfBlows(out string reason), "Monk 8 declares a flurry of blows", reason);
            AssertEq(m.GetIterativeAttackCount(), 3, "Flurrying monk 8: three attack steps");
            Assert(m.TryCommitManeuverSubstituteStep(-1, out int grappleBab, out int grappleStep, out string why) && grappleStep == 0,
                "A grapple replaces flurry step 1", why);
            AssertEq(grappleBab, 6, "The grapple's touch attack uses the step's BAB +6");

            // The grapple succeeded: link the two as ResolveGrapple does (the same reflection GrappleDamageRulesTests uses).
            MethodInfo establish = typeof(CharacterController).GetMethod("EstablishGrappleWith", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(establish != null, "CharacterController.EstablishGrappleWith exists (TST-006)");
            if (establish == null)
                return;
            establish.Invoke(m, new object[] { t });
            Assert(m.IsGrappling() && m.IsUsingFlurryOfBlows, "The monk is grappling and her flurry still lasts");
            AssertEq(m.GetIterativeAttackCount(), 2, "Grappling: the plain ladder (+6/+1), the flurry's extra attack ends (PHB p.156)");
            AssertEq(m.GetMainHandAttackStepBAB(1), 1, "The next grapple action is at the ladder's +1");
            AssertEq(m.FlurryAttackRollPenalty, -1, "The flurry penalty still applies to her attack rolls this round (PHB p.40)");

            Assert(m.TryConsumeIterativeGrappleAttackAction(out int attackBab, out _, out string grappleWhy) && attackBab == 1,
                "One more grapple action at BAB +1", grappleWhy);
            SpecialAttackResult hit = m.ResolveGrappleAction(GrappleActionType.AttackUnarmed, iterativeAttackBonusOverride: attackBab);
            Assert(hit != null && hit.Log != null && hit.Log.Contains("Flurry of blows penalty applied: -1"),
                "Her unarmed attack while grappling takes the flurry penalty", hit != null ? hit.Log : "null");
            Assert(!m.TryConsumeIterativeGrappleAttackAction(out _, out _, out _), "No third action: the flurry's extra attack is gone");
        }
        finally { Cleanup(m, t); }
    }

    // ── NPC monks (CRE-042) ──────────────────────────────────────────

    private static void TestNpcMonkEntries()
    {
        foreach (string id in new[] { "human_monk_3", "human_monk_5", "human_monk_7" })
        {
            NPCDefinition def = NPCDatabase.Get(id);
            if (def == null)
            {
                Assert(false, id + " exists");
                continue;
            }
            Assert(def.BaseSpeed == 6 && (def.NaturalAttacks == null || def.NaturalAttacks.Count == 0) && def.NaturalArmorBonus == 0,
                id + " stores the human 30 ft, no unarmed natural attack and no natural armor (CRE-042)",
                $"speed {def.BaseSpeed}, natural attacks {(def.NaturalAttacks != null ? def.NaturalAttacks.Count : 0)}, natural armor {def.NaturalArmorBonus}");
        }
    }

    private static CharacterController Spawn(string npcId, Vector2Int pos)
    {
        NPCDefinition template = NPCDatabase.Get(npcId);
        if (template == null || GameManager.Instance == null)
            return null;
        var go = new GameObject("MonkRulesTest_" + npcId);
        CharacterController cc = go.AddComponent<CharacterController>();
        GameManager.Instance.InitializeNPCFromDefinition(cc, CreatureTemplateRegistry.ApplyTemplatesClone(template), pos, null, null);
        return cc;
    }

    private static void TestNpcMonkSpawnedFeatures()
    {
        CharacterController m3 = null, m5 = null, m7 = null;
        try
        {
            m3 = Spawn("human_monk_3", new Vector2Int(-90, -90));
            m5 = Spawn("human_monk_5", new Vector2Int(-90, -92));
            m7 = Spawn("human_monk_7", new Vector2Int(-90, -94));
            if (m3 == null || m5 == null || m7 == null)
            {
                Assert(false, "the NPC monks spawn (needs a scene GameManager)");
                return;
            }

            AssertEq(m3.Stats.EffectiveSpeedFeet, 40, "Spawned monk 3: 30 + 10 = 40 ft, fast movement counted once (CRE-042)");
            AssertEq(m7.Stats.EffectiveSpeedFeet, 50, "Spawned monk 7: 30 + 20 = 50 ft");
            AssertEq(m5.Stats.MonkACBonus, 3, "Spawned monk 5 (WIS 15): AC bonus +2 + 1 = +3");
            var unarmed5 = m5.GetUnarmedDamage();
            Assert(m5.GetEquippedMainWeapon() == null && !m5.UsesInnateNaturalAttackSequence() && unarmed5.damageCount == 1 && unarmed5.damageDice == 8,
                "Spawned monk 5 strikes unarmed through the monk code: 1d8", $"{unarmed5.damageCount}d{unarmed5.damageDice}");
            Assert(m7.CanUseFlurryOfBlows(out string reason7), "Spawned monk 7 may flurry (the same rules core as a PC)", reason7);
            Assert(Join(m7.Stats.GetFlurryOfBlowsModifiedBaseAttackBonuses()) == "+4/+4", "Monk 7 flurry +4/+4 (PHB Table 3-10)",
                Join(m7.Stats.GetFlurryOfBlowsModifiedBaseAttackBonuses()));
        }
        finally { Cleanup(m3, m5, m7); }
    }

    // AI choice: against an AC the monk usually hits, the flurry's extra attack is worth its penalty.
    private static void TestAiFlurryChoice()
    {
        CharacterController m = null, t = null;
        try
        {
            m = Create("AiMonk", 3, new Vector2Int(-80, -96));
            t = CreateTarget(new Vector2Int(-79, -96));
            Assert(DND35.AI.AIProfile.DefaultShouldUseFlurryOfBlows(m, t), "The default AI flurries against AC 10");
            Equip(m, ItemDatabase.CloneItem(ItemID.ArmorLeather), EquipSlot.Armor);
            Assert(!DND35.AI.AIProfile.DefaultShouldUseFlurryOfBlows(m, t), "The AI never picks a flurry the rules refuse (armored)");
        }
        finally { Cleanup(m, t); }
    }
}
}

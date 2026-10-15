using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Classes
{
/// <summary>
/// CHR-020 (ranger): the ranger's combat class features for levels 1-8 on one rules core for PCs and NPCs (PHB p.46-48,
/// RangerRules). Favored enemy: selections at 1st and 5th (10th...) with a +2 increase on one favored enemy from the
/// second selection on (the book's 5th- and 10th-level examples), Table 3-14 choices only, matching by the creature's
/// real type and subtypes (tags, race and counts-as race; a changed type loses the old subtypes), the best of several
/// matching entries (no stacking), the bonus on weapon damage (the attack paths' FavoredEnemy term) and on Bluff, Listen,
/// Sense Motive, Spot and Survival against such creatures. Combat style: archery (Rapid Shot, Manyshot at 6th) or
/// two-weapon combat (Two-Weapon Fighting, Improved TWF at 6th), treated as having the feats only in light or no armor
/// (mithral counts one category lighter), also as prerequisites; never added to the feat list. Track and Endurance by
/// ranger level, also after a level-up. The animal companion's effective druid level is half the ranger level. NPC
/// rangers take their definition's choices or the DMG p.122 sample ranger's. Needs Play mode (CharacterController.Init;
/// the NPC spawn uses the scene GameManager).
/// </summary>
public static class RangerClassRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== RANGER CLASS RULES TESTS (CHR-020) ==========");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();
        NPCDatabase.Init();

        TestFavoredEnemySelections();
        TestFavoredEnemyMatching();
        TestFavoredEnemyNoStacking();
        TestFavoredEnemyDamage();
        TestFavoredEnemySkills();
        TestCombatStyleArmor();
        TestCombatStyleFeatsAndPrerequisites();
        TestLostStylePrerequisite();
        TestStyleFeatsInPlay();
        TestFavoredEnemyCoupDeGraceAndFeint();
        TestAutomaticFeatsByRangerLevel();
        TestCompanionEffectiveDruidLevel();
        TestCreationChoices();
        TestNpcChoices();
        TestNpcDefinitionsAreLegal();

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

    private static CharacterStats Build(int level, string cls = "Ranger", string race = "Human", int dex = 15)
    {
        var stats = new CharacterStats(
            name: cls + level, level: level, characterClass: cls,
            str: 14, dex: dex, con: 12, wis: 12, intelligence: 10, cha: 10,
            bab: 0, armorBonus: 0, shieldBonus: 0,
            damageDice: 8, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: level * 8, raceName: race);
        stats.EnsureMulticlassDataInitialized();
        return stats;
    }

    /// <summary>A ranger with favored enemies set from (type, bonus) pairs.</summary>
    private static CharacterStats Ranger(int level, params (string type, int bonus)[] favored)
    {
        CharacterStats r = Build(level);
        r.RangerFavoredEnemies = new FavoredEnemyData();
        foreach (var f in favored)
            r.RangerFavoredEnemies.Enemies.Add(new FavoredEnemyEntry(f.type, f.bonus));
        return r;
    }

    /// <summary>A creature with an NPC definition's type, tags and race, without spawning it.</summary>
    private static CharacterStats FromDef(string npcId)
    {
        NPCDefinition def = NPCDatabase.Get(npcId);
        if (def == null)
            return null;
        CharacterStats s = Build(1, "Fighter");
        s.Race = null;
        s.CreatureType = string.IsNullOrEmpty(def.CreatureType) ? "Humanoid" : def.CreatureType;
        s.CreatureTags.Clear();
        s.CreatureTags.AddRange(def.CreatureTags);
        RacialTraitRules.AttachNpcRace(s, def);
        return s;
    }

    private static CharacterStats OfType(string type, params string[] tags)
    {
        CharacterStats s = Build(1, "Fighter");
        s.Race = null;
        s.CreatureType = type;
        s.CreatureTags.Clear();
        s.CreatureTags.AddRange(tags);
        return s;
    }

    private static CharacterController Create(string name, CharacterStats stats, Vector2Int pos)
    {
        var go = new GameObject(name + "_RangerRulesTest");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        stats.CharacterName = name;
        controller.Init(stats, pos, null, null);
        inventory.Init(stats);
        controller.GridPosition = pos;
        return controller;
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

    // ── Favored enemy selections (PHB p.47) ─────────────────────────

    private static void TestFavoredEnemySelections()
    {
        AssertEq(RangerRules.FavoredEnemySelectionsDue(1), 1, "One favored enemy at 1st");
        AssertEq(RangerRules.FavoredEnemySelectionsDue(4), 1, "Still one at 4th");
        AssertEq(RangerRules.FavoredEnemySelectionsDue(5), 2, "Two at 5th");
        AssertEq(RangerRules.FavoredEnemySelectionsDue(8), 2, "Two at 8th");
        AssertEq(RangerRules.FavoredEnemySelectionsDue(10), 3, "Three at 10th");

        var fe = new FavoredEnemyData();
        Assert(!RangerRules.TryApplyFavoredEnemySelection(fe, 1, "Kobolds", null, out string bad), "A type not on Table 3-14 is refused", bad);
        Assert(!RangerRules.TryApplyFavoredEnemySelection(fe, 1, "Humanoid", null, out _), "A humanoid needs a subtype (Table 3-14)");
        Assert(!RangerRules.TryApplyFavoredEnemySelection(fe, 1, "Outsider", null, out _), "An outsider needs a subtype (Table 3-14)");
        Assert(RangerRules.TryApplyFavoredEnemySelection(fe, 1, "humanoid (goblinoid)", null, out _), "1st level: Humanoid (Goblinoid), any case");
        Assert(fe.Enemies[0].CreatureType == "Humanoid (Goblinoid)" && fe.Enemies[0].Bonus == 2, "Stored with the table's spelling at +2");
        Assert(!RangerRules.TryApplyFavoredEnemySelection(fe, 4, "Undead", null, out _), "No second selection before 5th level");
        AssertEq(RangerRules.PendingFavoredEnemySelections(fe, 5), 1, "One selection pending at 5th");
        Assert(RangerRules.NextSelectionGrantsIncrease(fe), "The 5th-level selection comes with a +2 increase");
        Assert(!RangerRules.TryApplyFavoredEnemySelection(fe, 5, "Undead", null, out _), "The increase must be chosen");
        Assert(!RangerRules.TryApplyFavoredEnemySelection(fe, 5, "Undead", "Animal", out _), "The increase must go to a favored enemy");
        Assert(!RangerRules.TryApplyFavoredEnemySelection(fe, 5, "Humanoid (Goblinoid)", "Humanoid (Goblinoid)", out _), "The new favored enemy must be a new type");
        Assert(RangerRules.TryApplyFavoredEnemySelection(fe, 5, "Undead", "Humanoid (Goblinoid)", out _), "5th level: undead, +2 on goblinoids");
        Assert(fe.Find("Humanoid (Goblinoid)").Bonus == 4 && fe.Find("Undead").Bonus == 2, "The book's 5th-level example: +4 and +2");
        AssertEq(RangerRules.PendingFavoredEnemySelections(fe, 8), 0, "Nothing pending at 8th");

        var fe2 = new FavoredEnemyData();
        RangerRules.TryApplyFavoredEnemySelection(fe2, 10, "Undead", null, out _);
        RangerRules.TryApplyFavoredEnemySelection(fe2, 10, "Aberration", "Aberration", out _);
        RangerRules.TryApplyFavoredEnemySelection(fe2, 10, "Vermin", "Undead", out _);
        Assert(fe2.Find("Undead").Bonus == 4 && fe2.Find("Aberration").Bonus == 4 && fe2.Find("Vermin").Bonus == 2,
            "The book's 10th-level example: +4, +4, +2 (the new one may take the increase)");
        AssertEq(fe2.TotalBonus, 10, "Three selections give 10 points of bonus (2 x 3 + 2 x 2)");
    }

    // ── Matching by real type and subtypes ─────────────────────────

    private static void TestFavoredEnemyMatching()
    {
        void Match(string entry, CharacterStats creature, bool want, string label)
        {
            Assert(creature != null && RangerRules.MatchesFavoredEnemy(entry, creature) == want, label,
                creature == null ? "creature missing" : $"type {creature.CreatureType}, tags {string.Join(",", creature.CreatureTags)}");
        }

        Match("Humanoid (Goblinoid)", FromDef("goblin"), true, "A goblin is a goblinoid (tag)");
        Match("Humanoid (Goblinoid)", FromDef("hobgoblin"), true, "A hobgoblin is a goblinoid");
        Match("Humanoid (Goblinoid)", FromDef("bugbear"), true, "A bugbear is a goblinoid");
        Match("Humanoid (Goblinoid)", FromDef("orc_warrior"), false, "An orc is not a goblinoid");
        Match("Humanoid (Orc)", FromDef("orc_warrior"), true, "An orc warrior is an orc");
        Match("Humanoid (Orc)", Build(1, "Fighter", "Half-Orc"), true, "A half-orc PC counts as an orc (PHB p.19)");
        Match("Humanoid (Orc)", OfType("Undead", "Orc"), false, "An orc zombie is undead and has lost the orc subtype (MM p.266)");
        Match("Humanoid (Human)", Build(1, "Fighter", "Human"), true, "A human PC is a human");
        Match("Humanoid (Human)", FromDef("human_warrior"), true, "A human warrior is a human");
        Match("Humanoid (Human)", FromDef("goblin"), false, "A goblin is not a human");
        Match("Humanoid (Human)", OfType("Humanoid"), false, "A humanoid with no subtype matches no humanoid entry");
        Match("Humanoid (Elf)", Build(1, "Fighter", "Half-Elf"), true, "A half-elf counts as an elf (PHB p.18)");
        Match("Humanoid (Elf)", FromDef("drow"), true, "A drow is an elf");
        Match("Humanoid (Reptilian)", FromDef("kobold_warrior"), true, "A kobold is reptilian");
        Match("Humanoid (Reptilian)", FromDef("lizardfolk"), true, "A lizardfolk is reptilian");
        Match("Humanoid (Gnoll)", FromDef("gnoll"), true, "A gnoll is a gnoll");
        Match("Outsider (Evil)", FromDef("dretch"), true, "A dretch is an evil outsider");
        Match("Outsider (Chaotic)", FromDef("dretch"), true, "A dretch is a chaotic outsider");
        Match("Outsider (Lawful)", FromDef("bearded_devil"), true, "A bearded devil is a lawful outsider");
        Match("Outsider (Evil)", FromDef("hound_archon"), false, "A hound archon is not an evil outsider");
        Match("Outsider (Native)", FromDef("rakshasa"), true, "A rakshasa is a native outsider");
        Match("Outsider (Fire)", FromDef("average_salamander"), true, "A salamander is a fire outsider");
        Match("Outsider (Native)", FromDef("average_xorn"), false, "A xorn is not a native outsider");
        Match("Undead", FromDef("zombie_shambler"), true, "A zombie is undead");
        Match("Magical Beast", OfType("MagicalBeast"), true, "Type names ignore spacing (MagicalBeast is Magical Beast)");
        Match("Monstrous Humanoid", OfType("Monstrous Humanoid"), true, "A monstrous humanoid matches its own type");
        Match("Humanoid (Human)", OfType("Monstrous Humanoid", "Human"), false, "A monstrous humanoid is not a humanoid");
        Match("Animal", OfType("Magical Beast"), false, "A celestial animal (a magical beast) is not an animal");
        Match("Humanoid (Human)", FromDef("werewolf"), true, "A werewolf is Humanoid (Human, Shapechanger) (MM p.175)");
        Match("Humanoid (Human)", FromDef("wererat"), true, "A wererat is a human (MM p.174)");
        Match("Humanoid (Human)", FromDef("werewolf_lord"), true, "A werewolf lord is a human");
        Match("Humanoid (Aquatic)", FromDef("lizardfolk"), false, "A lizardfolk is not aquatic (MM p.169: Humanoid (Reptilian))");
        Match("Outsider (Native)", FromDef("couatl"), true, "A couatl is a native outsider (MM p.37)");
        CharacterStats humanRanger = Ranger(1, ("Humanoid (Human)", 2));
        AssertEq(RangerRules.FavoredEnemyBonus(humanRanger, FromDef("werewolf")), 2, "+2 against a werewolf for a Humanoid (Human) ranger");
        AssertEq(RangerRules.FavoredEnemyBonus(humanRanger, FromDef("wererat")), 2, "+2 against a wererat for a Humanoid (Human) ranger");
    }

    private static void TestFavoredEnemyNoStacking()
    {
        CharacterStats r = Ranger(10, ("Outsider (Evil)", 4), ("Outsider (Lawful)", 2), ("Undead", 4));
        CharacterStats devil = FromDef("bearded_devil");
        AssertEq(RangerRules.FavoredEnemyBonus(r, devil), 4,
            "A devil is both an evil and a lawful outsider: the higher bonus applies, they do not stack (PHB p.47)");
        AssertEq(RangerRules.FavoredEnemyBonus(r, FromDef("formian_worker")), 2, "A formian (lawful only) gives +2");
        AssertEq(RangerRules.FavoredEnemyBonus(r, FromDef("hound_archon")), 2, "A hound archon (lawful good) gives +2");
        AssertEq(RangerRules.FavoredEnemyBonus(r, Build(1, "Fighter")), 0, "A human gives nothing");
        CharacterStats noRanger = Build(10, "Fighter");
        noRanger.RangerFavoredEnemies = r.RangerFavoredEnemies;
        AssertEq(RangerRules.FavoredEnemyBonus(noRanger, devil), 0, "Favored enemy data without ranger levels gives nothing");
    }

    // ── Weapon damage (the attack paths' term) ─────────────────────

    private static System.Func<int, string, int, int> ForceD20(int d20) => (sides, ctx, natural) => sides == 20 ? d20 : natural;

    private static void TestFavoredEnemyDamage()
    {
        CharacterController r = null, gob = null, man = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            ScenarioHooks.RollFilter = ForceD20(15);
            r = Create("FERanger", Ranger(5, ("Humanoid (Goblinoid)", 4), ("Undead", 2)), new Vector2Int(-80, -110));
            CharacterStats gobStats = FromDef("hobgoblin");
            gobStats.AdjustMaxHP(500);
            gobStats.CurrentHP += 500;
            gob = Create("FEHobgoblin", gobStats, new Vector2Int(-79, -110));
            CharacterStats manStats = Build(3, "Fighter");
            manStats.AdjustMaxHP(500);
            manStats.CurrentHP += 500;
            man = Create("FEHuman", manStats, new Vector2Int(-79, -111));

            CombatResult vsGob = r.Attack(gob, false, 0, null, null);
            AssertEq(vsGob.WeaponDamageBonus.FavoredEnemyBonus, 4, "+4 weapon damage against a favored goblinoid (hobgoblin)");
            Assert(vsGob.WeaponDamageBonus.Describe().Contains(RangerRules.FavoredEnemyDamageLabel), "The damage breakdown names the favored enemy term",
                vsGob.WeaponDamageBonus.Describe());
            CombatResult vsMan = r.Attack(man, false, 0, null, null);
            AssertEq(vsMan.WeaponDamageBonus.FavoredEnemyBonus, 0, "No favored enemy damage against a human");
            AssertEq(vsGob.WeaponDamageBonus.Total - vsMan.WeaponDamageBonus.Total, 4, "The favored enemy term is part of the static damage total");

            // A critical hit multiplies the static modifier, the favored enemy bonus included (PHB p.140).
            ScenarioHooks.RollFilter = ForceD20(20);
            CombatResult crit = r.Attack(gob, false, 0, null, null);
            Assert(crit.CritConfirmed && crit.WeaponDamageBonus.FavoredEnemyBonus == 4,
                "A confirmed critical keeps the favored enemy term in the multiplied static modifier");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(r, gob, man);
        }
    }

    // ── Skills (PHB p.47) ──────────────────────────────────────────

    private static void TestFavoredEnemySkills()
    {
        CharacterStats r = Ranger(1, ("Undead", 2));
        r.InitializeSkills("Ranger", 1);
        CharacterStats zombie = OfType("Undead");
        CharacterStats orc = FromDef("orc_warrior");
        foreach (string skill in RangerRules.FavoredEnemySkills)
            AssertEq(r.GetSkillBonusAgainst(skill, zombie) - r.GetSkillBonus(skill), 2, $"+2 {skill} against a favored enemy");
        AssertEq(r.GetSkillBonusAgainst("Spot", orc) - r.GetSkillBonus("Spot"), 0, "No Spot bonus against another creature");
        AssertEq(r.GetSkillBonusAgainst("Hide", zombie) - r.GetSkillBonus("Hide"), 0, "No bonus on Hide (not a favored enemy skill)");
        AssertEq(r.GetSkillBonusAgainst("Spot", null) - r.GetSkillBonus("Spot"), 0, "No creature, no bonus");
    }

    // ── Combat style (PHB p.48) ────────────────────────────────────

    private static ItemData Armor(ItemID id, bool mithral = false)
    {
        ItemData armor = ItemDatabase.CloneItem(id);
        if (mithral && armor != null)
            armor.Material = MaterialProperties.GetArmorMaterial(ItemMaterialType.Mithral, armor);
        return armor;
    }

    private static void TestCombatStyleArmor()
    {
        CharacterStats r = Build(2);
        r.RangerCombatStyle = CombatStyleData.Create(RangerCombatStyle.Archery);
        Assert(r.HasFeat("Rapid Shot") && !r.HasOwnFeat("Rapid Shot"), "Archery ranger 2 in no armor is treated as having Rapid Shot; it is not in his feat list");
        r.EquippedArmorItem = Armor(ItemID.ArmorStuddedLeather);
        Assert(r.HasFeat("Rapid Shot"), "Light armor keeps the combat style");
        r.EquippedArmorItem = Armor(ItemID.ArmorBreastplate);
        Assert(!r.HasFeat("Rapid Shot"), "Medium armor switches the combat style off");
        r.EquippedArmorItem = Armor(ItemID.ArmorPlate);
        Assert(!r.HasFeat("Rapid Shot"), "Heavy armor switches the combat style off");
        r.EquippedArmorItem = Armor(ItemID.ArmorBreastplate, mithral: true);
        Assert(r.HasFeat("Rapid Shot"), "A mithral breastplate counts as light armor (DMG p.284)");
        r.EquippedArmorItem = null;
        r.Feats.Add("Rapid Shot");
        r.EquippedArmorItem = Armor(ItemID.ArmorPlate);
        Assert(r.HasFeat("Rapid Shot"), "Rapid Shot taken as a feat works in any armor");

        CharacterStats r1 = Build(1);
        r1.RangerCombatStyle = CombatStyleData.Create(RangerCombatStyle.Archery);
        Assert(!r1.HasFeat("Rapid Shot"), "No combat style before 2nd ranger level");
        CharacterStats fighter = Build(4, "Fighter");
        fighter.RangerCombatStyle = CombatStyleData.Create(RangerCombatStyle.Archery);
        Assert(!fighter.HasFeat("Rapid Shot"), "Style data without ranger levels gives nothing");
        Assert(RangerRules.NeedsCombatStyleChoice(new CombatStyleData(), 2) && !RangerRules.NeedsCombatStyleChoice(new CombatStyleData(), 1),
            "The style is chosen at 2nd level");
        var chosen = new CombatStyleData();
        Assert(RangerRules.TryChooseCombatStyle(chosen, 2, RangerCombatStyle.TwoWeaponFighting, out _)
            && !RangerRules.TryChooseCombatStyle(chosen, 3, RangerCombatStyle.Archery, out _), "The style is chosen once");
    }

    private static void TestCombatStyleFeatsAndPrerequisites()
    {
        CharacterStats archer = Build(6, dex: 17);
        archer.RangerCombatStyle = CombatStyleData.Create(RangerCombatStyle.Archery);
        Assert(archer.HasFeat("Rapid Shot") && archer.HasFeat("Manyshot") && !archer.HasFeat("Improved Precise Shot"),
            "Archery ranger 6: Rapid Shot and Manyshot (Improved Combat Style), not yet Improved Precise Shot");
        Assert(!archer.HasFeat("Two-Weapon Fighting"), "Archery gives no two-weapon feat");

        CharacterStats twf = Build(6, dex: 15);
        twf.RangerCombatStyle = CombatStyleData.Create(RangerCombatStyle.TwoWeaponFighting);
        Assert(twf.HasFeat("Two-Weapon Fighting") && twf.HasFeat("Improved Two-Weapon Fighting"), "Two-weapon ranger 6: TWF and Improved TWF");
        var (main, off) = FeatManager.GetTWFPenalties(twf, lightOffHand: true);
        Assert(main == -2 && off == -2, "Two-weapon penalties with a light off-hand weapon are -2/-2", $"{main}/{off}");
        AssertEq(FeatManager.GetOffHandAttackCount(twf), 2, "Improved TWF gives a second off-hand attack");
        twf.EquippedArmorItem = Armor(ItemID.ArmorBreastplate);
        (main, off) = FeatManager.GetTWFPenalties(twf, lightOffHand: true);
        Assert(main == -4 && off == -8, "In medium armor the ranger fights without the style (-4/-8)", $"{main}/{off}");
        twf.EquippedArmorItem = null;

        // The style feats count as prerequisites while the style applies (delegated RAW reading 2026-10-14).
        CharacterStats twf2 = Build(2, dex: 15);
        twf2.RangerCombatStyle = CombatStyleData.Create(RangerCombatStyle.TwoWeaponFighting);
        FeatDefinition twoWeaponDefense = FeatDefinitions.GetFeat("Two-Weapon Defense");
        Assert(twoWeaponDefense != null && twoWeaponDefense.MeetsPrerequisites(twf2),
            "Two-weapon ranger 2 meets Two-Weapon Defense's TWF prerequisite through the style");
        twf2.EquippedArmorItem = Armor(ItemID.ArmorBreastplate);
        Assert(twoWeaponDefense != null && !twoWeaponDefense.MeetsPrerequisites(twf2), "...but not in medium armor");
        twf2.EquippedArmorItem = null;
        archer.Feats.Add("Point Blank Shot");
        Assert(FeatDefinitions.GetAvailableFeats(archer).Exists(f => f.FeatName == "Rapid Shot"),
            "Rapid Shot can still be taken as a feat (for use in heavier armor) once its prerequisites are met");
    }

    // PHB p.87: a feat taken on the strength of a style feat is unusable while the style does not apply.
    private static void TestLostStylePrerequisite()
    {
        CharacterStats r = Build(2, dex: 15);
        r.RangerCombatStyle = CombatStyleData.Create(RangerCombatStyle.TwoWeaponFighting);
        r.Feats.Add("Two-Weapon Defense");
        r.Feats.Add("Improved Two-Weapon Fighting");
        Assert(r.HasFeat("Two-Weapon Defense") && r.HasFeat("Improved Two-Weapon Fighting"),
            "Feats taken on the style's TWF work while the style applies");
        AssertEq(FeatManager.GetOffHandAttackCount(r), 2, "Improved TWF on the style's TWF: a second off-hand attack in no armor");
        r.EquippedArmorItem = Armor(ItemID.ArmorBreastplate);
        Assert(!r.HasFeat("Two-Weapon Defense") && !r.HasFeat("Improved Two-Weapon Fighting") && r.HasOwnFeat("Improved Two-Weapon Fighting"),
            "In medium armor they are unusable (PHB p.87) but still taken");
        AssertEq(FeatManager.GetOffHandAttackCount(r), 1, "In medium armor no second off-hand attack");
        r.Feats.Add("Greater Two-Weapon Fighting");
        Assert(!r.HasFeat("Greater Two-Weapon Fighting"), "The loss runs down the chain (Greater TWF needs the lost Improved TWF)");
        r.Feats.Add("Two-Weapon Fighting");
        Assert(r.HasFeat("Two-Weapon Defense") && r.HasFeat("Improved Two-Weapon Fighting") && r.HasFeat("Greater Two-Weapon Fighting"),
            "With Two-Weapon Fighting taken as a feat the chain works in any armor");

        CharacterStats fighter = Build(6, "Fighter", dex: 17);
        fighter.Feats.Add("Improved Two-Weapon Fighting");
        Assert(fighter.HasFeat("Improved Two-Weapon Fighting"), "A non-ranger's feats are not touched by the style check");
    }

    private static void TestStyleFeatsInPlay()
    {
        CharacterStats archer2 = Build(2, dex: 15);
        archer2.RangerCombatStyle = CombatStyleData.Create(RangerCombatStyle.Archery);
        AttackCalculator.CalculateRapidShot(archer2, true, true, out bool active, out int penalty);
        Assert(active && penalty == -2, "Archery ranger 2 without Point Blank Shot uses Rapid Shot (-2)", $"{active}/{penalty}");
        archer2.EquippedArmorItem = Armor(ItemID.ArmorBreastplate);
        AttackCalculator.CalculateRapidShot(archer2, true, true, out active, out _);
        Assert(!active, "...not in medium armor");

        CharacterStats archer6 = Build(6, dex: 15);
        archer6.RangerCombatStyle = CombatStyleData.Create(RangerCombatStyle.Archery);
        Assert(FeatManager.CanUseManyshot(archer6),
            "Archery ranger 6 with Dex 15 and no Point Blank Shot can use Manyshot (PHB p.48: without the prerequisites)");
        archer6.EquippedArmorItem = Armor(ItemID.ArmorBreastplate);
        Assert(!FeatManager.CanUseManyshot(archer6), "...not in medium armor");
        CharacterStats takenManyshot = Build(6, "Fighter", dex: 15);
        takenManyshot.Feats.Add("Manyshot");
        Assert(!FeatManager.CanUseManyshot(takenManyshot), "Manyshot taken as a feat still needs its prerequisites at use (PHB p.87)");
    }

    private static void TestFavoredEnemyCoupDeGraceAndFeint()
    {
        CharacterController r = null, gob = null, man = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            ScenarioHooks.RollFilter = ForceD20(10);
            CharacterStats rStats = Ranger(5, ("Humanoid (Goblinoid)", 4));
            rStats.InitializeSkills("Ranger", 5);
            r = Create("FECdgRanger", rStats, new Vector2Int(-90, -110));
            CharacterStats gobStats = FromDef("hobgoblin");
            gobStats.AdjustMaxHP(500);
            gobStats.CurrentHP += 500;
            gob = Create("FECdgHobgoblin", gobStats, new Vector2Int(-89, -110));
            CharacterStats manStats = Build(3, "Fighter");
            manStats.InitializeSkills("Fighter", 3);
            manStats.AdjustMaxHP(500);
            manStats.CurrentHP += 500;
            man = Create("FECdgHuman", manStats, new Vector2Int(-89, -111));

            // Feint (PHB p.155): the ranger's Bluff against a favored foe; his opposed check against a feinting one.
            SpecialAttackResult feintGob = r.ExecuteSpecialAttack(SpecialAttackType.Feint, gob);
            SpecialAttackResult feintMan = r.ExecuteSpecialAttack(SpecialAttackType.Feint, man);
            Assert(feintGob != null && feintMan != null
                   && (feintGob.CheckTotal - feintGob.CheckRoll) - (feintMan.CheckTotal - feintMan.CheckRoll) == 4,
                "+4 Bluff on a feint against a favored goblinoid, none against a human",
                feintGob != null && feintMan != null ? feintGob.Log + " | " + feintMan.Log : "no result");
            SpecialAttackResult gobFeints = gob.ExecuteSpecialAttack(SpecialAttackType.Feint, r);
            SpecialAttackResult manFeints = man.ExecuteSpecialAttack(SpecialAttackType.Feint, r);
            Assert(gobFeints != null && manFeints != null
                   && (gobFeints.OpposedTotal - gobFeints.OpposedRoll) - (manFeints.OpposedTotal - manFeints.OpposedRoll) == 4,
                "+4 on the ranger's Sense Motive against a feinting favored goblinoid",
                gobFeints != null && manFeints != null ? gobFeints.Log + " | " + manFeints.Log : "no result");

            // Coup de grace (PHB p.153): the favored enemy bonus is in the static modifier rolled the multiplier times.
            gob.ApplyCondition(CombatConditionType.Unconscious, 5, "Test");
            man.ApplyCondition(CombatConditionType.Unconscious, 5, "Test");
            SpecialAttackResult cdgGob = r.ExecuteSpecialAttack(SpecialAttackType.CoupDeGrace, gob);
            SpecialAttackResult cdgMan = r.ExecuteSpecialAttack(SpecialAttackType.CoupDeGrace, man);
            Assert(cdgGob != null && cdgMan != null && cdgGob.Success && cdgMan.Success
                   && cdgGob.WeaponDamageRoll.StaticPerRoll - cdgMan.WeaponDamageRoll.StaticPerRoll == 4,
                "A coup de grace on a favored goblinoid adds +4 to the static modifier",
                cdgGob != null && cdgMan != null ? cdgGob.WeaponDamageRoll.Describe() + " | " + cdgMan.WeaponDamageRoll.Describe() : "no result");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(r, gob, man);
        }
    }

    // ── Track and Endurance by ranger level ────────────────────────

    private static void TestAutomaticFeatsByRangerLevel()
    {
        CharacterStats r1 = Build(1);
        Assert(r1.HasOwnFeat("Track") && !r1.HasOwnFeat("Endurance"), "Ranger 1: Track, no Endurance");
        CharacterStats r3 = Build(3);
        Assert(r3.HasOwnFeat("Track") && r3.HasOwnFeat("Endurance"), "Ranger 3: Track and Endurance");

        // Fighter 2 / Ranger 1: Endurance comes at ranger level 3, not character level 3.
        CharacterStats multi = Build(2, "Fighter");
        multi.AddExperience(ExperienceCalculator.GetXPForLevel(5));
        AssertEq(multi.PendingLevelUps, 3, "Fighter 2 with 5th-level XP has three level-ups pending");
        multi.ApplyPendingLevelUp("Ranger");
        Assert(multi.HasOwnFeat("Track") && !multi.HasOwnFeat("Endurance"), "Fighter 2/Ranger 1 gains Track, not Endurance");
        multi.ApplyPendingLevelUp("Ranger");
        multi.ApplyPendingLevelUp("Ranger");
        Assert(multi.HasOwnFeat("Endurance") && multi.RangerLevel == 3, "Endurance at ranger level 3 after the level-ups");
    }

    private static void TestCompanionEffectiveDruidLevel()
    {
        AssertEq(RangerClass.GetEffectiveDruidLevel(3), 0, "No companion before 4th");
        AssertEq(RangerClass.GetEffectiveDruidLevel(4), 2, "Ranger 4: companion as a 2nd-level druid's (PHB p.48)");
        AssertEq(RangerClass.GetEffectiveDruidLevel(8), 4, "Ranger 8: effective druid level 4");
        AssertEq(Build(7).RangerEffectiveDruidLevel, 3, "Ranger 7: effective druid level 3 (rounded down)");
    }

    // ── Creation and NPC choices ───────────────────────────────────

    private static void TestCreationChoices()
    {
        CharacterCreationData kael = RangerClass.GetQuickStartCharacter();
        Assert(kael.RangerCombatStyleChoice == RangerCombatStyle.Archery && kael.RangerFavoredEnemies.Find("Undead") != null,
            "The Quick Start ranger has archery and favored enemy undead");
        CharacterStats s = Build(3);
        RangerRules.ApplyCreationChoices(s, kael.RangerFavoredEnemies, kael.RangerCombatStyleChoice);
        Assert(s.HasFeat("Rapid Shot") && RangerRules.FavoredEnemyBonus(s, OfType("Undead")) == 2, "Creation choices reach the character");
        s.RangerFavoredEnemies.Enemies[0].Bonus = 8;
        AssertEq(kael.RangerFavoredEnemies.Enemies[0].Bonus, 2, "The character's copy is not the creation record's");
        Assert(!RangerRules.HasPendingChoices(s), "Nothing pending for the Quick Start ranger 3");
        CharacterStats empty = Build(5);
        RangerRules.ApplyCreationChoices(empty, null, RangerCombatStyle.None);
        Assert(RangerRules.HasPendingChoices(empty), "A ranger without choices has them pending for the level-up flow");
    }

    private static void TestNpcChoices()
    {
        CharacterStats r5 = Build(5);
        RangerRules.ApplyNpcChoices(r5, null);
        Assert(r5.RangerCombatStyle.Style == RangerCombatStyle.Archery, "An NPC ranger without a choice takes archery (DMG p.122 sample)");
        Assert(r5.RangerFavoredEnemies.Find("Humanoid (Elf)")?.Bonus == 4 && r5.RangerFavoredEnemies.Find("Humanoid (Human)")?.Bonus == 2,
            "...and the sample's favored enemies at 5th: elves +4, humans +2", r5.RangerFavoredEnemies.GetSummary());
        CharacterStats r1 = Build(1);
        RangerRules.ApplyNpcChoices(r1, null);
        Assert(!r1.RangerCombatStyle.HasStyleSelected && r1.RangerFavoredEnemies.SelectionsMade == 1, "An NPC ranger 1 has one favored enemy and no style yet");

        var def = new NPCDefinition
        {
            RangerStyle = RangerCombatStyle.TwoWeaponFighting,
            RangerFavoredEnemies = new List<FavoredEnemyEntry> { new FavoredEnemyEntry("Undead", 4), new FavoredEnemyEntry("Humanoid (Orc)", 2) }
        };
        NPCDefinition copy = def.Clone();
        copy.RangerFavoredEnemies[0].Bonus = 10;
        AssertEq(def.RangerFavoredEnemies[0].Bonus, 4, "NPCDefinition.Clone copies the favored enemy entries (CRE-023)");
        CharacterStats r6 = Build(6);
        RangerRules.ApplyNpcChoices(r6, def);
        Assert(r6.HasFeat("Improved Two-Weapon Fighting") && RangerRules.FavoredEnemyBonus(r6, OfType("Undead")) == 4,
            "A definition's style and favored enemies are applied");

        // A spawned NPC ranger through the shared spawn path.
        if (GameManager.Instance == null)
        {
            Assert(false, "The NPC spawn check needs the scene GameManager (Play mode)");
            return;
        }
        var go = new GameObject("RangerRulesTest_npc");
        CharacterController npc = go.AddComponent<CharacterController>();
        try
        {
            GameManager.Instance.InitializeNPCFromDefinition(npc, NPCDatabase.Get("test_archer_gust").Clone(), new Vector2Int(-90, -110), null, null);
            Assert(npc.Stats.RangerLevel == 2 && npc.Stats.RangerCombatStyle != null && npc.Stats.RangerCombatStyle.Style == RangerCombatStyle.Archery,
                "A spawned NPC ranger 2 has its combat style");
            Assert(npc.Stats.HasFeat("Rapid Shot") && !npc.Stats.HasOwnFeat("Rapid Shot"), "...and is treated as having Rapid Shot in leather armor");
            Assert(npc.Stats.HasOwnFeat("Track"), "...and has Track");
            AssertEq(RangerRules.FavoredEnemyBonus(npc.Stats, Build(1, "Fighter", "Elf")), 2, "...and favored enemy elves +2 against an elf PC");
        }
        finally
        {
            Cleanup(npc);
        }
    }

    /// <summary>Every NPC definition with ranger levels names legal choices, or none (the DMG sample's then apply).</summary>
    private static void TestNpcDefinitionsAreLegal()
    {
        int checkedCount = 0;
        foreach (NPCDefinition def in NPCDatabase.AllNPCs)
        {
            if (def == null || !string.Equals(def.CharacterClass, "Ranger", System.StringComparison.OrdinalIgnoreCase) || def.Level <= 0)
                continue;
            checkedCount++;
            if (def.RangerFavoredEnemies == null || def.RangerFavoredEnemies.Count == 0)
                continue;
            int due = RangerRules.FavoredEnemySelectionsDue(def.Level);
            int total = 0;
            bool valid = def.RangerFavoredEnemies.Count == due;
            foreach (FavoredEnemyEntry e in def.RangerFavoredEnemies)
            {
                valid &= e != null && RangerRules.IsValidFavoredEnemyType(e.CreatureType) && e.Bonus >= 2 && e.Bonus % 2 == 0;
                total += e != null ? e.Bonus : 0;
            }
            valid &= total == 4 * due - 2;
            Assert(valid, def.Id + " names a legal favored enemy set for ranger level " + def.Level);
        }
        Assert(checkedCount > 0, "NPC rangers exist in the database", checkedCount.ToString());
    }
}
}

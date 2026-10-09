using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Character
{
/// <summary>
/// CHR-072: NPC classes and creature types grant weapon and armor proficiency through the same
/// CharacterStats.IsProficientWithWeapon / IsProficientWithArmor a PC uses.
/// Rules: DMG p.108-109 (NPC classes: warrior and aristocrat all simple and martial weapons, all armor and shields;
/// adept simple weapons; expert simple weapons and light armor; commoner one simple weapon); MM p.305-317 (type traits:
/// a humanoid with more than 1 racial HD uses the humanoid type's simple weapons and worn armor, a 1-HD humanoid's
/// warrior level is real; giants simple and martial weapons and worn armor; undead simple weapons and worn armor;
/// the weapons its entry describes, but a humanoid with real class levels only its class's; the shapechanger subtype
/// simple weapons, p.314). MM attack bonuses: goblin morningstar +2 (p.133), hobgoblin longsword +2 (p.153), ogre
/// greatclub +8 (p.199); gnoll battleaxe and shortbow, leather armor and heavy steel shield (p.130). The MM orc (p.203)
/// attacks with a falchion at +4; the game's orc_warrior carries a greataxe and scale mail instead (database gear),
/// which at the same STR and BAB has the same +4.
/// The class-table tests need no scene; the spawn tests go through GameManager.InitializeNPCFromDefinition and need
/// Play mode (a scene GameManager). Spawned controllers sit off the grid and are destroyed afterwards.
/// </summary>
public static class NpcProficiencyTests
{
    private static int _passed;
    private static int _failed;

    private static readonly Vector2Int SpawnPos = new Vector2Int(-50, -50);

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        NPCDatabase.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();

        Debug.Log("====== NPC PROFICIENCY TESTS (CHR-072) ======");

        TestNpcClassTables();
        TestStandInClassGrantsNothing();

        if (GameManager.Instance == null)
        {
            Assert(false, "NPC spawn proficiency tests need Play mode with a scene GameManager");
        }
        else
        {
            TestGoblinMorningstar();
            TestHobgoblinLongsword();
            TestOrcWarriorGreataxe();
            TestOgreGreatclub();
            TestGnollEntryProficiencies();
            TestCommonerOneWeapon();
            TestSkeletonWarriorUndeadType();
            TestClassAppliedToStandInBecomesReal();
            TestClassedHumanoidGetsNoCarriedWeapons();
            TestRacialHitDiceKeptBesideClass();
            TestShapechangerSubtype();
        }

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

    // ── Helpers ─────────────────────────────────────────────────────────

    private static CharacterStats MakeChar(string className)
    {
        return new CharacterStats(
            name: className + " Test",
            level: 1,
            characterClass: className,
            str: 12, dex: 12, con: 12,
            wis: 10, intelligence: 10, cha: 10,
            bab: 1,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 8);
    }

    /// <summary>The item template for <paramref name="id"/>; a missing id fails a check, so it cannot pass an assertion silently.</summary>
    private static ItemData Item(string id)
    {
        ItemData item = ItemDatabase.GetItem(id);
        if (item == null)
            Assert(false, "item id " + id + " exists in ItemDatabase");
        return item;
    }

    /// <summary>Proficiency with an item that exists; false for a missing id (Item records the failure).</summary>
    private static bool ProfWeapon(CharacterStats s, string id)
    {
        ItemData item = Item(id);
        return item != null && s.IsProficientWithWeapon(item);
    }

    private static bool ProfArmor(CharacterStats s, string id)
    {
        ItemData item = Item(id);
        return item != null && s.IsProficientWithArmor(item);
    }

    /// <summary>Non-proficiency with an item that exists; false for a missing id.</summary>
    private static bool NotProfWeapon(CharacterStats s, string id)
    {
        ItemData item = Item(id);
        return item != null && !s.IsProficientWithWeapon(item);
    }

    private static bool NotProfArmor(CharacterStats s, string id)
    {
        ItemData item = Item(id);
        return item != null && !s.IsProficientWithArmor(item);
    }

    private static CharacterController Spawn(string npcId)
    {
        NPCDefinition template = NPCDatabase.Get(npcId);
        if (template == null)
            return null;
        return Spawn(template.Clone(), npcId);
    }

    private static CharacterController Spawn(NPCDefinition def, string label)
    {
        var go = new GameObject("NpcProficiencyTest_" + label);
        CharacterController cc = go.AddComponent<CharacterController>();
        GameManager.Instance.InitializeNPCFromDefinition(cc, def, SpawnPos, null, null);
        return cc;
    }

    private static void Cleanup(CharacterController cc)
    {
        if (cc == null)
            return;
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.Grid != null)
            gm.Grid.ClearCreatureOccupancy(cc);
        Object.DestroyImmediate(cc.gameObject);
    }

    private static AttackBonusBreakdown MeleeBonus(CharacterController cc, ItemData weapon, bool twoHanded)
    {
        return cc.BuildAttackBonus(null, weapon, false, null, false, 0, cc.Stats.BaseAttackBonus, twoHanded);
    }

    /// <summary>
    /// Checks a spawned creature's main weapon: proficient, no armor penalty, and the MM attack bonus. The weapon must
    /// come from the spawn itself, also when the definition lists it under EquipSlot.MainHand (the ogre's greatclub;
    /// ITM-004, fixed 2026-10-08: before, DirectEquip dropped it and the test equipped it by hand). The spawned BAB must
    /// be the MM's (CRE-004, fixed 2026-10-08: a warrior level on a 1-HD humanoid is +1, MM p.310). A masterwork weapon the spawn may
    /// roll by CR is not part of BuildAttackBonus, so it does not change the expected figure.
    /// Likewise a parenthesised NPC feat such as "Weapon Focus (greatclub)" does nothing (CHR-008), so the MM's feat
    /// term <paramref name="mmFeatAttack"/> is replaced by the feat term the game computed. What is left is exactly the
    /// part CHR-072 changes: the proficiency penalties.
    /// </summary>
    private static void CheckMainWeapon(string npcId, string weaponId, bool twoHanded, int mmAttackBonus, int mmBab, int mmFeatAttack, string mmCite)
    {
        CharacterController cc = Spawn(npcId);
        try
        {
            if (cc == null || cc.Stats == null)
            {
                Assert(false, npcId + " spawns", "not in NPCDatabase");
                return;
            }

            ItemData weapon = cc.GetEquippedMainWeapon();
            Assert(weapon != null && weapon.Id != null && weapon.Id.EndsWith(weaponId),
                npcId + " spawns wielding its " + weaponId + " (ITM-004 for MainHand entries)", "main weapon " + (weapon != null ? weapon.Id : "none"));
            if (weapon == null)
                return;

            Assert(cc.Stats.IsProficientWithWeapon(weapon), npcId + " is proficient with its " + weaponId);
            AttackBonusBreakdown b = MeleeBonus(cc, weapon, twoHanded);
            Assert(b.WeaponNonProficiencyPenalty == 0, npcId + ": no weapon non-proficiency penalty",
                "got " + b.WeaponNonProficiencyPenalty);
            Assert(b.ArmorNonProficiencyPenalty == 0, npcId + ": no armor or shield non-proficiency penalty",
                "got " + b.ArmorNonProficiencyPenalty);
            int spawnedBab = cc.Stats.BaseAttackBonus;
            Assert(spawnedBab == mmBab, npcId + " spawns with the MM's BAB " + CharacterStats.FormatMod(mmBab) + " (CRE-004)",
                "got " + CharacterStats.FormatMod(spawnedBab));
            int gameFeat = b.Feats.TotalFeatAttackModifier;
            int expected = mmAttackBonus - mmBab + spawnedBab - mmFeatAttack + gameFeat;
            string babNote = "";
            if (gameFeat != mmFeatAttack)
                babNote += $", feat term {CharacterStats.FormatMod(gameFeat)} vs MM {CharacterStats.FormatMod(mmFeatAttack)} (CHR-008)";
            Assert(b.Total == expected,
                npcId + " " + weaponId + " attack bonus is the MM's " + CharacterStats.FormatMod(mmAttackBonus) + " (" + mmCite + babNote + ")",
                "got " + CharacterStats.FormatMod(b.Total) + ", expected " + CharacterStats.FormatMod(expected));
        }
        finally
        {
            Cleanup(cc);
        }
    }

    // ── Class tables (DMG p.108-109) ────────────────────────────────────

    private static void TestNpcClassTables()
    {
        foreach (string cls in new[] { "Warrior", "Aristocrat" })
        {
            CharacterStats s = MakeChar(cls);
            Assert(s.HasSimpleWeaponProficiency() && s.HasMartialWeaponProficiency(),
                cls + ": all simple and martial weapons (DMG p.108-109)");
            Assert(s.HasLightArmorProficiency() && s.HasMediumArmorProficiency() && s.HasHeavyArmorProficiency(),
                cls + ": all armor (DMG p.108-109)");
            Assert(s.HasShieldProficiency(), cls + ": shields (DMG p.108-109)");
            Assert(ProfWeapon(s, "longsword") && ProfArmor(s, "full_plate"),
                cls + ": longsword and full plate through IsProficientWithWeapon/IsProficientWithArmor");
            Assert(!s.HasTowerShieldProficiency(),
                cls + ": no tower shield until the owner rules on \"shields\" (CHR-072)");
        }

        CharacterStats adept = MakeChar("Adept");
        Assert(adept.HasSimpleWeaponProficiency() && !adept.HasMartialWeaponProficiency(),
            "Adept: simple weapons only (DMG p.108)");
        Assert(!adept.HasLightArmorProficiency() && !adept.HasShieldProficiency(),
            "Adept: no armor and no shields (DMG p.108)");

        CharacterStats expert = MakeChar("Expert");
        Assert(expert.HasSimpleWeaponProficiency() && !expert.HasMartialWeaponProficiency(),
            "Expert: simple weapons, not martial (DMG p.109)");
        Assert(expert.HasLightArmorProficiency() && !expert.HasMediumArmorProficiency(),
            "Expert: light armor only (DMG p.109)");
        Assert(!expert.HasShieldProficiency(), "Expert: no shields (DMG p.109)");

        CharacterStats commoner = MakeChar("Commoner");
        Assert(!commoner.HasSimpleWeaponProficiency() && !commoner.HasLightArmorProficiency() && !commoner.HasShieldProficiency(),
            "Commoner: no broad weapon, armor or shield proficiency (one simple weapon only, DMG p.109)");
    }

    private static void TestStandInClassGrantsNothing()
    {
        CharacterStats s = MakeChar("Warrior");
        s.RacialHitDiceStandInClass = "Warrior";
        Assert(!s.HasSimpleWeaponProficiency() && !s.HasMartialWeaponProficiency() && !s.HasLightArmorProficiency(),
            "A Warrior class that stands in for racial HD grants no proficiency (CRE-024, CHR-072)");
    }

    // ── Spawned creatures ───────────────────────────────────────────────

    private static void TestGoblinMorningstar()
    {
        // Warrior 1 (a real class level for a 1-HD humanoid, MM p.310): BAB +1, STR 11, Small +1 = +2.
        CheckMainWeapon("goblin", "morningstar", false, 2, 1, 0, "MM p.133");
    }

    private static void TestHobgoblinLongsword()
    {
        // Warrior 1: BAB +1, STR 13 = +2; studded leather (its light steel shield id does not exist, CRE-052).
        CheckMainWeapon("hobgoblin", "longsword", false, 2, 1, 0, "MM p.153");
    }

    private static void TestOrcWarriorGreataxe()
    {
        // Warrior 1: BAB +1, STR 17 = +4. The MM orc's +4 is for a falchion (MM p.203); orc_warrior's greataxe and scale
        // mail are database gear, with the same bonus at the same STR and BAB.
        CheckMainWeapon("orc_warrior", "greataxe", true, 4, 1, 0, "MM p.203 falchion +4, database greataxe");
    }

    private static void TestOgreGreatclub()
    {
        // Giant 4 HD (Warrior stand-in): BAB +3, STR 21 +5, Large -1, Weapon Focus +1 = +8; hide armor.
        CheckMainWeapon("ogre", "greatclub", true, 8, 3, 1, "MM p.199");

        CharacterController cc = Spawn("ogre");
        try
        {
            if (cc == null) { Assert(false, "ogre spawns"); return; }
            CharacterStats s = cc.Stats;
            Assert(s.RacialHitDiceStandInClass == "Warrior", "ogre: its Warrior class is a racial-HD stand-in",
                "got " + (s.RacialHitDiceStandInClass ?? "null"));
            Assert(ProfWeapon(s, "longsword"), "ogre: giants are proficient with martial weapons (MM p.310)");
            Assert(ProfArmor(s, "hide_armor") && ProfArmor(s, "leather_armor"),
                "ogre: proficient with the hide armor it wears and lighter armor (MM p.310)");
            Assert(NotProfArmor(s, "full_plate"),
                "ogre: not proficient with heavy armor it is not described wearing (MM p.310)");
            Assert(s.HasShieldProficiency() && !s.HasTowerShieldProficiency(),
                "ogre: shields through its armor proficiency, no tower shield");
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestGnollEntryProficiencies()
    {
        CharacterController cc = Spawn("gnoll");
        try
        {
            if (cc == null) { Assert(false, "gnoll spawns"); return; }
            CharacterStats s = cc.Stats;
            Assert(s.RacialHitDiceStandInClass == "Warrior",
                "gnoll: 2 humanoid racial HD, so its Warrior class is a stand-in (MM p.310)");
            Assert(ProfWeapon(s, "battleaxe") && ProfWeapon(s, "shortbow"),
                "gnoll: proficient with the battleaxe and shortbow of its entry (MM p.130)");
            Assert(ProfWeapon(s, "club"), "gnoll: humanoid type, all simple weapons (MM p.310)");
            Assert(NotProfWeapon(s, "longsword"),
                "gnoll: not proficient with a martial weapon outside its entry");
            Assert(ProfArmor(s, "leather_armor") && ProfArmor(s, ItemIDs.SHIELD_HEAVY_STEEL),
                "gnoll: proficient with the leather armor and heavy steel shield of its entry (MM p.130)");
            Assert(NotProfArmor(s, "chainmail"),
                "gnoll: not proficient with medium armor it is not described wearing (MM p.310)");
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestCommonerOneWeapon()
    {
        CharacterController cc = Spawn("human_commoner");
        try
        {
            if (cc == null) { Assert(false, "human_commoner spawns"); return; }
            CharacterStats s = cc.Stats;
            Assert(ProfWeapon(s, "club"), "human_commoner: proficient with the club it carries (DMG p.109)");
            Assert(NotProfWeapon(s, "dagger"),
                "human_commoner: one simple weapon only, not the dagger (DMG p.109)");
            Assert(!s.HasLightArmorProficiency(), "human_commoner: no armor (DMG p.109)");
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestSkeletonWarriorUndeadType()
    {
        CharacterController cc = Spawn("skeleton_warrior");
        try
        {
            if (cc == null) { Assert(false, "skeleton_warrior spawns"); return; }
            CharacterStats s = cc.Stats;
            Assert(ProfWeapon(s, "longsword"), "skeleton_warrior: proficient with the longsword it carries (MM p.317)");
            Assert(ProfWeapon(s, "club"), "skeleton_warrior: undead type, all simple weapons (MM p.317)");
            // RAW a skeleton keeps the base creature's weapon proficiencies (MM p.226), so a human warrior's skeleton
            // keeps martial weapons; the template drops the class (CRE-054), so that is not asserted here.
            Assert(ProfArmor(s, "banded_mail") && s.HasShieldProficiency(),
                "skeleton_warrior: the banded mail it wears and shields (MM p.317)");
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestClassAppliedToStandInBecomesReal()
    {
        NPCDefinition template = NPCDatabase.Get("gnoll");
        if (template == null) { Assert(false, "gnoll in NPCDatabase"); return; }
        NPCDefinition def = template.Clone();
        CreatureClassEngine.ApplyClassToDefinition(def, new FighterClass(), 1);
        Assert(template.ClassLevelsAreRacialHitDice == true, "ApplyClassToDefinition leaves the gnoll template untouched");

        CharacterController cc = Spawn(def, "gnoll_fighter");
        try
        {
            CharacterStats s = cc.Stats;
            Assert(s.RacialHitDiceStandInClass == null && ProfWeapon(s, "longsword")
                   && s.HasTowerShieldProficiency(),
                "gnoll with Fighter 1: the real class grants its proficiencies (PHB p.38)");
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestClassedHumanoidGetsNoCarriedWeapons()
    {
        // DMG encounter "Hobgoblin Cleric": the hobgoblin (1 HD, so its class level is real) carries a longsword, but a
        // humanoid with class levels is proficient by class (MM p.310) and a cleric with simple weapons only (PHB p.31).
        NPCDefinition template = NPCDatabase.Get("hobgoblin");
        if (template == null) { Assert(false, "hobgoblin in NPCDatabase"); return; }
        NPCDefinition def = template.Clone();
        Assert(CreatureProficiency.CarriedWeaponIds(def).Contains("longsword"), "hobgoblin data carries a longsword (MM p.153)");
        CreatureClassEngine.ApplyClassToDefinition(def, new ClericClass(), 3);
        Assert(!def.HasRacialHumanoidHitDice, "hobgoblin cleric: a 1-HD humanoid keeps no racial HD beside the class");

        CharacterController cc = Spawn(def, "hobgoblin_cleric");
        try
        {
            CharacterStats s = cc.Stats;
            Assert(NotProfWeapon(s, "longsword"),
                "hobgoblin cleric: not proficient with the longsword it carries, as a PC cleric is not (MM p.310, PHB p.31)");
            Assert(ProfWeapon(s, ItemIDs.MACE_HEAVY), "hobgoblin cleric: simple weapons through the Cleric class (PHB p.31)");
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestRacialHitDiceKeptBesideClass()
    {
        // A gnoll keeps its 2 humanoid racial HD beside adept levels, and with them the humanoid type's simple weapons,
        // worn armor and entry weapons (MM p.130, p.310); the adept class adds no armor (DMG p.108).
        NPCDefinition template = NPCDatabase.Get("gnoll");
        if (template == null) { Assert(false, "gnoll in NPCDatabase"); return; }
        NPCDefinition def = template.Clone();
        CreatureClassEngine.ApplyClassToDefinition(def, new AdeptClass(), 2);
        Assert(def.HasRacialHumanoidHitDice && !template.HasRacialHumanoidHitDice,
            "gnoll adept: racial HD kept beside the class, template untouched");

        CharacterController cc = Spawn(def, "gnoll_adept");
        try
        {
            CharacterStats s = cc.Stats;
            Assert(ProfWeapon(s, "battleaxe") && ProfWeapon(s, "club"),
                "gnoll adept: entry battleaxe and simple weapons from its racial HD (MM p.130, p.310)");
            Assert(ProfArmor(s, "leather_armor") && s.HasShieldProficiency(),
                "gnoll adept: the leather armor and shield of its entry (MM p.130, p.310)");
            Assert(NotProfWeapon(s, "longsword") && NotProfArmor(s, "chainmail"),
                "gnoll adept: no martial weapon or medium armor outside its entry");
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestShapechangerSubtype()
    {
        // The phasm is an aberration that is not humanoid in form, but the shapechanger subtype gives it all simple
        // weapons (MM p.314).
        CharacterController cc = Spawn("phasm");
        try
        {
            if (cc == null) { Assert(false, "phasm spawns"); return; }
            CharacterStats s = cc.Stats;
            Assert(ProfWeapon(s, "club") && NotProfWeapon(s, "longsword"),
                "phasm: shapechanger subtype, all simple weapons and no martial ones (MM p.314)");
        }
        finally
        {
            Cleanup(cc);
        }
    }
}

}

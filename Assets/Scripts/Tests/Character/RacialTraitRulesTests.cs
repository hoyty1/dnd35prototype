using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// CHR-019 and CRE-038: the combat racial traits through the shared formulas, for PCs and for NPCs of a PC race or an
/// MM subrace. Rules: PHB p.13 (human: 4 extra skill points at 1st level, 1 at each later level), p.15 (dwarf: +1 attack
/// against orcs, half-orcs included, and goblinoids; +4 dodge against the giant type, lost with the Dexterity bonus),
/// p.16 (elf: +2 Listen, Search, Spot; sleep immunity), p.17 (gnome: +1 attack against kobolds and goblinoids; +4 dodge
/// against giants; +1 DC of illusion spells; +2 Listen), p.18-19 (half-elf counts as an elf, half-orc as an orc), p.20
/// (halfling: +1 attack with thrown weapons and slings; +2 Climb, Jump, Listen, Move Silently), p.76 (size modifier on
/// Hide), p.136 (dodge bonuses go with the Dexterity bonus); MM p.92 (duergar), p.103 (drow: +2 Will against spells and
/// spell-like abilities), p.132 (svirfneblin: +2 on all saves, +4 dodge against all creatures), p.226 and p.266
/// (skeletons and zombies lose the base creature's racial traits), p.310 (ogres, trolls and ettins are giants). Builds
/// bare CharacterStats and, for the weapon attack builder and sleep immunity, CharacterControllers on plain
/// GameObjects; reads the NPC templates without changing them. Needs Play mode: CharacterController.Init uses the
/// sprite renderer that Awake creates, and Awake does not run in edit mode.
/// </summary>
public static class RacialTraitRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== RACIAL TRAIT RULES TESTS (CHR-019, CRE-038) ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();
        NPCDatabase.Init();

        TestRaceData();
        TestKinds();
        TestAttackBonusAgainstKinds();
        TestThrownAndSlingThroughAttackBuilder();
        TestDodgeAgainstGiants();
        TestSvirfneblinDodge();
        TestNpcRaces();
        TestUndeadTemplatesLoseRace();
        TestNpcRaceKeepsScoresAndSpeed();
        TestNpcSleepImmunity();
        TestNpcSaves();
        TestIllusionDc();
        TestSkills();
        TestHumanSkillPoints();

        Debug.Log($"====== Racial Trait Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats Pc(string race, string cls = "Fighter", int level = 1, int intelligence = 10)
    {
        var stats = new CharacterStats(
            name: race + " " + cls, level: level, characterClass: cls,
            str: 12, dex: 12, con: 12, wis: 10, intelligence: intelligence, cha: 10,
            bab: level, armorBonus: 0, shieldBonus: 0,
            damageDice: 8, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 10 * level, raceName: race);
        stats.EnsureMulticlassDataInitialized();
        return stats;
    }

    /// <summary>Stats as InitializeNPCFromDefinition builds them for the traits under test: scores, speed, type, tags, race.</summary>
    private static CharacterStats FromDef(NPCDefinition def)
    {
        var stats = new CharacterStats(
            name: def.Name, level: Mathf.Max(1, def.Level), characterClass: def.CharacterClass ?? "Warrior",
            str: def.STR, dex: def.DEX, con: def.CON, wis: def.WIS, intelligence: def.INT, cha: def.CHA,
            bab: def.BAB, armorBonus: 0, shieldBonus: 0,
            damageDice: 0, damageCount: 1, bonusDamage: 0,
            baseSpeed: def.BaseSpeed, atkRange: 1, baseHitDieHP: Mathf.Max(1, def.BaseHitDieHP));
        foreach (string tag in def.CreatureTags)
            stats.CreatureTags.Add(tag);
        stats.CreatureType = string.IsNullOrEmpty(def.CreatureType) ? "Humanoid" : def.CreatureType;
        stats.SetBaseSizeCategory(def.SizeCategory);
        RacialTraitRules.AttachNpcRace(stats, def);
        return stats;
    }

    private static CharacterStats Npc(string id)
    {
        NPCDefinition def = NPCDatabase.Get(id);
        Assert(def != null, $"NPC '{id}' exists");
        return def != null ? FromDef(def) : null;
    }

    private static CharacterController Controller(CharacterStats stats, List<GameObject> made)
    {
        var go = new GameObject(stats.CharacterName + "_RacialTraitTest");
        made.Add(go);
        CharacterController c = go.AddComponent<CharacterController>();
        InventoryComponent inv = go.AddComponent<InventoryComponent>();
        c.Init(stats, Vector2Int.zero, null, null);
        inv.Init(stats);
        return c;
    }

    private static void Destroy(List<GameObject> made)
    {
        foreach (GameObject go in made)
            if (go != null)
                Object.DestroyImmediate(go);
    }

    private static void TestRaceData()
    {
        Debug.Log("--- RaceData (PHB p.14-21, MM p.92, p.103, p.132) ---");
        RaceData dwarf = RaceDatabase.GetRace("Dwarf");
        RaceData gnome = RaceDatabase.GetRace("Gnome");
        RaceData halfling = RaceDatabase.GetRace("Halfling");
        Assert(dwarf.RacialACBonuses.TryGetValue("Giant", out int d) && d == 4, "Dwarf: +4 dodge vs Giant");
        Assert(gnome.RacialACBonuses.TryGetValue("Giant", out int g) && g == 4, "Gnome: +4 dodge vs Giant");
        AssertEq(halfling.ThrownAndSlingAttackBonus, 1, "Halfling: +1 thrown and sling");
        AssertEq(gnome.IllusionSpellDCBonus, 1, "Gnome: +1 illusion DC");

        RaceData drow = RaceDatabase.GetRace("Drow");
        RaceData duergar = RaceDatabase.GetRace("Duergar");
        RaceData svirf = RaceDatabase.GetRace("Svirfneblin");
        Assert(drow != null && drow.IsMonsterSubrace && drow.ImmunityToSleep && drow.SaveVsEnchantment == 2 && drow.WillSaveVsSpells == 2
               && drow.DarkvisionRange == 120 && drow.CountsAsRace == "Elf",
            "Drow: elf traits, +2 Will vs spells, darkvision 120, counts as elf");
        Assert(duergar != null && duergar.IsMonsterSubrace && duergar.StabilityBonus == 4 && duergar.SaveVsSpells == 2 && duergar.SaveVsPoison == 0
               && duergar.RacialACBonuses.ContainsKey("Giant") && duergar.RacialAttackBonuses.ContainsKey("Orc"),
            "Duergar: dwarf traits, poison immunity replaces +2 vs poison");
        Assert(duergar != null && duergar.WeaponFamiliarity.Count == 0 && dwarf.WeaponFamiliarity.Contains("dwarven_waraxe"),
            "Duergar: no waraxe or urgrosh familiarity (MM p.92); the dwarf keeps it");
        Assert(svirf != null && svirf.IsMonsterSubrace && svirf.SaveBonusAllSaves == 2 && svirf.SaveVsIllusion == 0
               && svirf.DodgeACBonusAllCreatures == 4 && svirf.RacialACBonuses.Count == 0 && svirf.IllusionSpellDCBonus == 1,
            "Svirfneblin: +2 all saves, +4 dodge vs all instead of vs giants, +1 illusion DC");
        Assert(gnome.RacialACBonuses.ContainsKey("Giant") && gnome.SaveVsIllusion == 2 && gnome.DodgeACBonusAllCreatures == 0,
            "Building the svirfneblin did not change the gnome");

        List<string> names = RaceDatabase.GetAllRaceNames();
        AssertEq(names.Count, 7, "GetAllRaceNames lists the 7 PHB races only");
        List<string> small = RaceDatabase.GetRaceNamesBySizeCategory(SizeCategory.Small);
        List<string> medium = RaceDatabase.GetRaceNamesBySizeCategory(SizeCategory.Medium);
        Assert(!small.Contains("Svirfneblin") && !medium.Contains("Drow") && !medium.Contains("Duergar") && small.Contains("Gnome"),
            "MM subraces are not Disguise Self / Alter Self forms");
    }

    private static void TestKinds()
    {
        Debug.Log("--- Kinds: types, subtypes, Orc Blood and Elven Blood ---");
        Assert(RacialTraitRules.IsOfKind(Pc("Half-Orc"), "Orc"), "A half-orc PC is an orc (PHB p.19)");
        Assert(RacialTraitRules.IsOfKind(Pc("Half-Elf"), "Elf"), "A half-elf PC is an elf (PHB p.18)");
        Assert(!RacialTraitRules.IsOfKind(Pc("Human"), "Orc"), "A human is no orc");
        Assert(RacialTraitRules.IsOfKind(Npc("orc_warrior"), "Orc"), "orc_warrior is an orc");
        Assert(RacialTraitRules.IsOfKind(Npc("half_orc_warrior"), "Orc"), "half_orc_warrior is an orc");
        foreach (string id in new[] { "goblin", "hobgoblin", "bugbear" })
            Assert(RacialTraitRules.IsOfKind(Npc(id), "Goblinoid"), $"{id} is a goblinoid");
        Assert(RacialTraitRules.IsOfKind(Npc("kobold_warrior"), "Kobold"), "kobold_warrior is a kobold");
        foreach (string id in new[] { "ogre", "troll", "ettin" })
            Assert(RacialTraitRules.IsOfKind(Npc(id), "Giant"), $"{id} is of the giant type (MM p.310)");
        Assert(!RacialTraitRules.IsOfKind(Npc("orc_warrior"), "Giant"), "orc_warrior is not a giant");

        CharacterStats goblinTag = Pc("Human");
        goblinTag.CreatureTags.Add("Goblin");
        Assert(RacialTraitRules.IsOfKind(goblinTag, "Goblinoid"), "A humanoid tagged Goblin is a goblinoid (PHB p.15)");

        NPCDefinition orcZombieDef = ZombieTemplate.Apply(NPCDatabase.Get("orc_warrior"));
        Assert(orcZombieDef != null && !RacialTraitRules.IsOfKind(FromDef(orcZombieDef), "Orc"), "An orc zombie is undead, not an orc (MM p.266)");
        NPCDefinition ogreSkeletonDef = SkeletonTemplate.Apply(NPCDatabase.Get("ogre"));
        Assert(ogreSkeletonDef != null && !RacialTraitRules.IsOfKind(FromDef(ogreSkeletonDef), "Giant"), "An ogre skeleton is undead, not a giant (MM p.226)");
    }

    private static void TestAttackBonusAgainstKinds()
    {
        Debug.Log("--- Racial attack bonus against kinds (PHB p.15, p.17) ---");
        CharacterStats dwarf = Pc("Dwarf");
        CharacterStats gnome = Pc("Gnome");
        CharacterStats human = Pc("Human");
        CharacterStats orc = Npc("orc_warrior");
        CharacterStats goblin = Npc("goblin");
        CharacterStats kobold = Npc("kobold_warrior");
        CharacterStats bugbear = Npc("bugbear");
        CharacterStats halfOrcPc = Pc("Half-Orc");

        AssertEq(RacialTraitRules.AttackBonusAgainst(dwarf, orc), 1, "Dwarf vs orc +1");
        AssertEq(RacialTraitRules.AttackBonusAgainst(dwarf, goblin), 1, "Dwarf vs goblin +1");
        AssertEq(RacialTraitRules.AttackBonusAgainst(dwarf, halfOrcPc), 1, "Dwarf vs half-orc PC +1 (orc blood)");
        AssertEq(RacialTraitRules.AttackBonusAgainst(dwarf, kobold), 0, "Dwarf vs kobold +0");
        AssertEq(RacialTraitRules.AttackBonusAgainst(gnome, kobold), 1, "Gnome vs kobold +1");
        AssertEq(RacialTraitRules.AttackBonusAgainst(gnome, bugbear), 1, "Gnome vs bugbear +1");
        AssertEq(RacialTraitRules.AttackBonusAgainst(gnome, orc), 0, "Gnome vs orc +0");
        AssertEq(RacialTraitRules.AttackBonusAgainst(human, orc), 0, "Human vs orc +0");
        AssertEq(dwarf.GetRacialAttackBonus(orc), 1, "CharacterStats.GetRacialAttackBonus delegates");

        CharacterStats dwarfNpc = Npc("dwarf_warrior");
        AssertEq(RacialTraitRules.AttackBonusAgainst(dwarfNpc, halfOrcPc), 1, "NPC dwarf_warrior vs half-orc PC +1 (CRE-038)");
        CharacterStats duergar = Npc("duergar");
        AssertEq(RacialTraitRules.AttackBonusAgainst(duergar, goblin), 1, "NPC duergar vs goblin +1");
        CharacterStats svirf = Npc("svirfneblin");
        AssertEq(RacialTraitRules.AttackBonusAgainst(svirf, kobold), 1, "NPC svirfneblin vs kobold +1");
    }

    private static void TestThrownAndSlingThroughAttackBuilder()
    {
        Debug.Log("--- Halfling +1 with thrown weapons and slings, through BuildAttackBonus (PHB p.20) ---");
        var made = new List<GameObject>();
        try
        {
            ItemData sling = ItemDatabase.CloneItem(ItemID.WeaponSling);
            ItemData dagger = ItemDatabase.CloneItem(ItemID.WeaponDagger);
            ItemData longbow = ItemDatabase.CloneItem(ItemID.WeaponLongbow);
            ItemData longsword = ItemDatabase.CloneItem(ItemID.WeaponLongsword);
            Assert(sling != null && dagger != null && longbow != null && longsword != null && dagger.IsThrown, "Weapons exist; the dagger is a thrown weapon");
            if (sling == null || dagger == null || longbow == null || longsword == null)
                return;

            CharacterController halfling = Controller(Pc("Halfling"), made);
            CharacterController human = Controller(Pc("Human"), made);
            CharacterController dwarf = Controller(Pc("Dwarf"), made);
            CharacterController orc = Controller(Npc("orc_warrior"), made);
            CharacterController ogre = Controller(Npc("ogre"), made);

            AssertEq(halfling.BuildAttackBonus(ogre, sling, true, null, false, 0, 1, false).RacialBonus, 1, "Halfling sling +1");
            AssertEq(halfling.BuildAttackBonus(ogre, dagger, true, null, false, 0, 1, false).RacialBonus, 1, "Halfling thrown dagger +1");
            AssertEq(halfling.BuildAttackBonus(ogre, dagger, false, null, false, 0, 1, false).RacialBonus, 0, "Halfling dagger in melee +0");
            AssertEq(halfling.BuildAttackBonus(ogre, longbow, true, null, false, 0, 1, false).RacialBonus, 0, "Halfling longbow +0");
            AssertEq(human.BuildAttackBonus(ogre, sling, true, null, false, 0, 1, false).RacialBonus, 0, "Human sling +0");
            AssertEq(dwarf.BuildAttackBonus(orc, longsword, false, null, false, 0, 1, false).RacialBonus, 1, "Dwarf longsword vs orc +1 in the attack builder");
            AssertEq(dwarf.BuildAttackBonus(ogre, longsword, false, null, false, 0, 1, false).RacialBonus, 0, "Dwarf longsword vs ogre +0");
            AttackBonusBreakdown b = dwarf.BuildAttackBonus(orc, longsword, false, null, false, 0, 1, false);
            AttackBonusBreakdown c = dwarf.BuildAttackBonus(ogre, longsword, false, null, false, 0, 1, false);
            AssertEq(b.Total - c.Total, 1, "The racial term reaches the attack total");
        }
        finally
        {
            Destroy(made);
        }
    }

    private static void TestDodgeAgainstGiants()
    {
        Debug.Log("--- +4 dodge against giants, lost with the Dexterity bonus (PHB p.15, p.17, p.136) ---");
        CharacterStats dwarf = Pc("Dwarf");
        CharacterStats gnome = Pc("Gnome");
        CharacterStats human = Pc("Human");
        CharacterStats ogre = Npc("ogre");
        CharacterStats troll = Npc("troll");
        CharacterStats orc = Npc("orc_warrior");

        AssertEq(RacialTraitRules.KindDodgeACBonusAgainst(dwarf, ogre), 4, "Dwarf vs ogre +4");
        AssertEq(RacialTraitRules.KindDodgeACBonusAgainst(gnome, troll), 4, "Gnome vs troll +4");
        AssertEq(RacialTraitRules.KindDodgeACBonusAgainst(dwarf, orc), 0, "Dwarf vs orc +0");
        AssertEq(RacialTraitRules.KindDodgeACBonusAgainst(human, ogre), 0, "Human vs ogre +0");
        NPCDefinition ogreSkeleton = SkeletonTemplate.Apply(NPCDatabase.Get("ogre"));
        AssertEq(RacialTraitRules.KindDodgeACBonusAgainst(dwarf, FromDef(ogreSkeleton)), 0, "Dwarf vs ogre skeleton +0 (undead)");
        AssertEq(RacialTraitRules.KindDodgeACBonusAgainst(Npc("dwarf_warrior"), ogre), 4, "NPC dwarf_warrior vs ogre +4 (CRE-038)");

        dwarf.ApplyCondition(CombatConditionType.FlatFooted, 1, "RacialTraitTest");
        Assert(dwarf.DeniedDexToAcByCondition, "Flat-footed denies Dexterity");
        AssertEq(RacialTraitRules.KindDodgeACBonusAgainst(dwarf, ogre), 0, "Flat-footed dwarf vs ogre +0");
    }

    private static void TestSvirfneblinDodge()
    {
        Debug.Log("--- Svirfneblin +4 dodge against all creatures (MM p.132) ---");
        NPCDefinition def = NPCDatabase.Get("svirfneblin");
        CharacterStats plain = FromDef(def);
        plain.Race = null;
        CharacterStats svirf = FromDef(def);
        Assert(svirf.Race != null && svirf.Race.RaceName == "Svirfneblin", "svirfneblin NPC has the Svirfneblin race");
        AssertEq(svirf.RacialDodgeACBonus, 4, "RacialDodgeACBonus +4");
        AssertEq(svirf.ArmorClass - plain.ArmorClass, 4, "ArmorClass +4");
        AssertEq(svirf.TouchArmorClass - plain.TouchArmorClass, 4, "Touch AC +4");
        AssertEq(SpellcastingComponent.GetTouchAC(svirf) - SpellcastingComponent.GetTouchAC(plain), 4, "Spell touch AC +4");
        AssertEq(RacialTraitRules.KindDodgeACBonusAgainst(svirf, Npc("ogre")), 0, "No separate bonus against giants");
        AssertEq(RacialTraitRules.DodgeACBonusAgainst(svirf, Npc("orc_warrior")), 4, "DodgeACBonusAgainst counts the all-creatures bonus");
        // Both flat-footed: both lose Dexterity and the condition's own AC terms, so only the racial dodge could differ.
        svirf.ApplyCondition(CombatConditionType.FlatFooted, 1, "RacialTraitTest");
        plain.ApplyCondition(CombatConditionType.FlatFooted, 1, "RacialTraitTest");
        AssertEq(svirf.RacialDodgeACBonus, 0, "Flat-footed: RacialDodgeACBonus 0");
        AssertEq(svirf.ArmorClass - plain.ArmorClass, 0, "Flat-footed svirfneblin loses it");
    }

    private static void TestNpcRaces()
    {
        Debug.Log("--- NPC races (CRE-038) ---");
        var expected = new Dictionary<string, string>
        {
            { "dwarf_warrior", "Dwarf" }, { "duergar", "Duergar" }, { "elf_warrior", "Elf" }, { "drow", "Drow" },
            { "halfling_warrior", "Halfling" }, { "half_orc_warrior", "Half-Orc" }, { "human_warrior", "Human" },
            { "human_commoner", "Human" }, { "human_monk_3", "Human" }, { "human_paladin_3", "Human" }, { "svirfneblin", "Svirfneblin" }
        };
        foreach (KeyValuePair<string, string> kvp in expected)
        {
            NPCDefinition def = NPCDatabase.Get(kvp.Key);
            RaceData race = RacialTraitRules.ResolveNpcRace(def);
            Assert(race != null && race.RaceName == kvp.Value, $"{kvp.Key} resolves to {kvp.Value}", race != null ? race.RaceName : "null");
        }
        Assert(RacialTraitRules.ResolveNpcRace(NPCDatabase.Get("orc_warrior")) == null, "orc_warrior has no PC race");
        Assert(RacialTraitRules.ResolveNpcRace(NPCDatabase.Get("ogre")) == null, "ogre has no PC race");

        NPCDefinition template = NPCDatabase.Get("dwarf_warrior");
        NPCDefinition clone = template.Clone();
        Assert(clone.RaceName == "Dwarf", "NPCDefinition.Clone copies RaceName");
    }

    private static void TestUndeadTemplatesLoseRace()
    {
        Debug.Log("--- Skeletons and zombies lose racial traits (MM p.226, p.266) ---");
        NPCDefinition dwarf = NPCDatabase.Get("dwarf_warrior");
        NPCDefinition skel = SkeletonTemplate.Apply(dwarf);
        NPCDefinition zombie = ZombieTemplate.Apply(NPCDatabase.Get("elf_warrior"));
        Assert(skel != null && skel.RaceName == null && RacialTraitRules.ResolveNpcRace(skel) == null, "Dwarf skeleton has no race");
        Assert(zombie != null && zombie.RaceName == null && RacialTraitRules.ResolveNpcRace(zombie) == null, "Elf zombie has no race");
        Assert(dwarf.RaceName == "Dwarf", "The dwarf_warrior template keeps its race");

        NPCDefinition undeadNamed = dwarf.Clone();
        undeadNamed.CreatureType = "Undead";
        Assert(RacialTraitRules.ResolveNpcRace(undeadNamed) == null, "Any non-humanoid entry gets no race");
    }

    private static void TestNpcRaceKeepsScoresAndSpeed()
    {
        Debug.Log("--- Attaching a race keeps the MM scores, size and speed ---");
        foreach (string id in new[] { "dwarf_warrior", "elf_warrior", "halfling_warrior", "human_warrior", "svirfneblin" }) // not the monks: CRE-042
        {
            NPCDefinition def = NPCDatabase.Get(id);
            CharacterStats s = FromDef(def);
            Assert(s.Race != null, $"{id} has a race");
            Assert(s.STR == def.STR && s.DEX == def.DEX && s.CON == def.CON && s.INT == def.INT && s.WIS == def.WIS && s.CHA == def.CHA,
                $"{id} keeps its MM ability scores");
            AssertEq(s.EffectiveSpeedFeet, def.BaseSpeed * 5, $"{id} keeps its entry speed");
            Assert(s.CurrentSizeCategory == def.SizeCategory, $"{id} keeps its size");
        }
        CharacterStats dwarfPc = Pc("Dwarf");
        AssertEq(dwarfPc.EffectiveSpeedFeet, 20, "A dwarf PC still moves 20 ft");
    }

    private static void TestNpcSleepImmunity()
    {
        Debug.Log("--- Elf and drow NPCs are immune to sleep (PHB p.16; CRE-038) ---");
        var made = new List<GameObject>();
        try
        {
            CharacterController elf = Controller(Npc("elf_warrior"), made);
            CharacterController drow = Controller(Npc("drow"), made);
            CharacterController human = Controller(Npc("human_warrior"), made);
            Assert(SpellUtilities.IsImmuneToSleepEffects(elf), "elf_warrior immune to sleep");
            Assert(SpellUtilities.IsImmuneToSleepEffects(drow), "drow immune to sleep");
            Assert(!SpellUtilities.IsImmuneToSleepEffects(human), "human_warrior not immune to sleep");
        }
        finally
        {
            Destroy(made);
        }
    }

    private static void TestNpcSaves()
    {
        Debug.Log("--- NPC racial save bonuses (PHB p.15-20, MM p.103, p.132; CRE-038) ---");
        SpellData daze = SpellDatabase.GetSpell(SpellNames.DAZE);
        SpellData fireball = SpellDatabase.GetSpell(SpellNames.FIREBALL);
        Assert(daze != null && fireball != null, "Daze and Fireball exist");
        if (daze == null || fireball == null)
            return;

        CharacterStats dwarf = Npc("dwarf_warrior");
        AssertEq(SaveRules.SituationalBonus(dwarf, SavingThrowType.Fortitude, SaveContext.Poison(), out _), 2, "dwarf_warrior +2 vs poison");
        AssertEq(SaveRules.SituationalBonus(dwarf, SavingThrowType.Reflex, SaveContext.ForSpell(fireball), out _), 2, "dwarf_warrior +2 vs spells");
        CharacterStats halfling = Npc("halfling_warrior");
        AssertEq(halfling.RacialAllSavesBonus, 1, "halfling_warrior +1 on all saves");
        AssertEq(SaveRules.SituationalBonus(halfling, SavingThrowType.Will, SaveContext.Fear, out _), 2, "halfling_warrior +2 morale vs fear");
        CharacterStats elf = Npc("elf_warrior");
        AssertEq(SaveRules.SituationalBonus(elf, SavingThrowType.Will, SaveContext.ForSpell(daze), out _), 2, "elf_warrior +2 vs enchantment");
        CharacterStats drow = Npc("drow");
        AssertEq(SaveRules.SituationalBonus(drow, SavingThrowType.Will, SaveContext.ForSpell(daze), out _), 4, "drow +2 vs enchantment and +2 Will vs spells");
        AssertEq(SaveRules.SituationalBonus(drow, SavingThrowType.Reflex, SaveContext.ForSpell(fireball), out _), 0, "drow's spell bonus is on Will only");
        CharacterStats svirf = Npc("svirfneblin");
        AssertEq(svirf.RacialAllSavesBonus, 2, "svirfneblin +2 on all saves");
        CharacterStats elfPc = Pc("Elf");
        AssertEq(SaveRules.SituationalBonus(elfPc, SavingThrowType.Will, SaveContext.ForSpell(daze), out _), 2, "Elf PC unchanged: +2 vs enchantment");
    }

    private static void TestIllusionDc()
    {
        Debug.Log("--- +1 DC on illusion spells: gnome PC and svirfneblin NPC (PHB p.17, MM p.132) ---");
        SpellData colorSpray = SpellDatabase.GetSpell(SpellNames.COLOR_SPRAY);
        if (colorSpray == null)
        {
            Assert(false, "Color Spray exists");
            return;
        }
        AssertEq(SpellSaveDCRules.Explain(Pc("Gnome", "Wizard", 3), colorSpray).RacialBonus, 1, "Gnome wizard +1");
        AssertEq(SpellSaveDCRules.Explain(Npc("svirfneblin"), colorSpray).RacialBonus, 1, "Svirfneblin +1");
        AssertEq(SpellSaveDCRules.Explain(Pc("Human", "Wizard", 3), colorSpray).RacialBonus, 0, "Human +0");
    }

    private static void TestSkills()
    {
        Debug.Log("--- Racial skill bonuses and the size modifier on Hide (PHB p.16-20, p.76) ---");
        CharacterStats elf = Pc("Elf");
        CharacterStats human = Pc("Human");
        CharacterStats halfling = Pc("Halfling");
        CharacterStats dwarf = Pc("Dwarf");
        elf.InitializeSkills("Fighter", 1);
        human.InitializeSkills("Fighter", 1);
        halfling.InitializeSkills("Fighter", 1);
        dwarf.InitializeSkills("Fighter", 1);

        AssertEq(elf.GetRacialSkillModifier("Listen"), 2, "Elf Listen +2");
        AssertEq(elf.GetRacialSkillModifier("Spot"), 2, "Elf Spot +2");
        AssertEq(elf.GetSkillBonus("Listen") - human.GetSkillBonus("Listen"), 2, "Elf Listen total is the human's +2 (same WIS)");
        AssertEq(halfling.GetRacialSkillModifier("Move Silently"), 2, "Halfling Move Silently +2");
        AssertEq(halfling.GetRacialSkillModifier("Climb"), 2, "Halfling Climb +2");
        AssertEq(halfling.GetRacialSkillModifier("Hide"), 4, "Halfling Hide +4 (Small)");
        AssertEq(human.GetRacialSkillModifier("Hide"), 0, "Human Hide +0");
        AssertEq(Pc("Half-Elf").GetRacialSkillModifier("Diplomacy"), 2, "Half-elf Diplomacy +2");
        AssertEq(dwarf.GetRacialSkillModifier("Search"), 0, "Dwarf Search +0 (stonecunning is conditional)");
        AssertEq(human.GetRacialSkillModifier("Listen"), 0, "Human Listen +0");
    }

    private static void TestHumanSkillPoints()
    {
        Debug.Log("--- Human extra skill points (PHB p.13) ---");
        CharacterStats human1 = Pc("Human", "Fighter", 1);
        CharacterStats dwarf1 = Pc("Dwarf", "Fighter", 1);
        human1.InitializeSkills("Fighter", 1);
        dwarf1.InitializeSkills("Fighter", 1);
        AssertEq(dwarf1.TotalSkillPoints, (2 + dwarf1.INTMod) * 4, "Dwarf fighter 1: (2 + INT) x 4");
        AssertEq(human1.TotalSkillPoints - dwarf1.TotalSkillPoints, 4, "Human fighter 1: 4 more");

        CharacterStats human3 = Pc("Human", "Fighter", 3);
        CharacterStats dwarf3 = Pc("Dwarf", "Fighter", 3);
        human3.InitializeSkills("Fighter", 3);
        dwarf3.InitializeSkills("Fighter", 3);
        AssertEq(human3.TotalSkillPoints - dwarf3.TotalSkillPoints, 6, "Human fighter 3: 4 + 1 + 1 more");

        AssertEq(human1.RacialExtraSkillPoints(1), 4, "RacialExtraSkillPoints at 1st level: 4");
        AssertEq(human1.RacialExtraSkillPoints(2), 1, "RacialExtraSkillPoints at 2nd level: 1");
        AssertEq(dwarf1.RacialExtraSkillPoints(2), 0, "A dwarf gets none");

        var made = new List<GameObject>();
        try
        {
            CharacterController humanC = Controller(Pc("Human", "Fighter", 1), made);
            CharacterController dwarfC = Controller(Pc("Dwarf", "Fighter", 1), made);
            LevelUpData h = LevelUpCalculator.CalculateLevelUp(humanC, 1, 2);
            LevelUpData d = LevelUpCalculator.CalculateLevelUp(dwarfC, 1, 2);
            LevelUpCalculator.RecalculateForSelectedClass(h, "Fighter");
            LevelUpCalculator.RecalculateForSelectedClass(d, "Fighter");
            AssertEq(h.SkillPointsNew - d.SkillPointsNew, 1, "Level-up to 2: the human gets 1 more");
        }
        finally
        {
            Destroy(made);
        }
    }
}
}

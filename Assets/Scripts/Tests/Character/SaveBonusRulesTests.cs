using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// CHR-018: the one saving throw modifier (CharacterStats.GetSaveTotal and SaveRules.Modifier) applies every save
/// bonus source with the PHB p.171 stacking rules (luck stacks by the owner's house rule, BonusType.cs), and the
/// shared resolvers (SavingThrowResolver, SpellSaveResolver) use it. Rules: PHB p.44 Divine Grace; PHB p.15-20 racial
/// save bonuses; PHB p.41 Still Mind; PHB p.26 and p.51 trap sense; PHB p.36 Resist Nature's Lure; PHB p.37 venom
/// immunity; PHB p.93-97 Great Fortitude, Iron Will, Lightning Reflexes; PHB p.272 Resistance; PHB p.238 Guidance;
/// PHB p.271 Remove Fear; PHB p.205 Bless and p.196 Aid (morale against fear only); PHB p.278 Shield Other (resistance
/// on saves); PHB p.44 Aura of Courage; PHB p.171 stacking (morale does not stack; racial does); DMG p.253 Cloak of Resistance and Cloak of Arachnida; DMG p.260 ioun stones (pale green and
/// orange prisms); DMG p.265 Robe of the Archmagi and Robe of Stars; DMG p.267 Stone of Good Luck. Builds bare
/// CharacterStats, an Inventory without a GameObject, for spell effects a StatusEffectManager on a plain GameObject,
/// and for the Aura of Courage CharacterControllers on plain GameObjects with an explicit combatant list; runs in edit
/// or Play mode.
/// </summary>
public static class SaveBonusRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== SAVE BONUS RULES TESTS (CHR-018) ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();

        TestDivineGrace();
        TestSaveFeats();
        TestHalfling();
        TestFearMoraleDoesNotStackWithRage();
        TestFearSpellDetection();
        TestDwarf();
        TestElfAndHalfElf();
        TestGnome();
        TestStillMind();
        TestTrapSense();
        TestResistNaturesLure();
        TestIndomitableWill();
        TestAuraOfCourage();
        TestVenomImmunity();
        TestResistanceItemsDoNotStack();
        TestRobeOfTheArchmagi();
        TestCompetenceAndLuckItems();
        TestCloakOfArachnida();
        TestOrangePrismCasterLevel();
        TestSpellSaveBonusTypes();
        TestResolversUseSharedModifier();
        TestSaveNameParsing();

        Debug.Log($"====== Save Bonus Rules Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats Build(string cls, int level, string race = "Human", int str = 10, int dex = 10,
        int con = 10, int wis = 10, int intelligence = 10, int cha = 10)
    {
        var stats = new CharacterStats(
            name: cls + level + race,
            level: level,
            characterClass: cls,
            str: str,
            dex: dex,
            con: con,
            wis: wis,
            intelligence: intelligence,
            cha: cha,
            bab: 0,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 8,
            raceName: race);
        stats.EnsureMulticlassDataInitialized();
        return stats;
    }

    /// <summary>The save with no conditional bonus, built from its parts: ability + base save.</summary>
    private static int Plain(CharacterStats s, SavingThrowType save)
    {
        switch (save)
        {
            case SavingThrowType.Fortitude: return s.CONMod + s.ClassFortSave;
            case SavingThrowType.Reflex: return s.DEXMod + s.ClassRefSave;
            default: return s.WISMod + s.ClassWillSave;
        }
    }

    private static global::Inventory NewInventory(CharacterStats stats)
    {
        return new global::Inventory { OwnerStats = stats };
    }

    // ── Class features ──────────────────────────────────────────────

    private static void TestDivineGrace()
    {
        CharacterStats p2 = Build("Paladin", 2, cha: 16);
        AssertEq(p2.FortitudeSave, Plain(p2, SavingThrowType.Fortitude) + 3, "Divine Grace: paladin 2 CHA 16 adds +3 to Fortitude (PHB p.44)");
        AssertEq(p2.ReflexSave, Plain(p2, SavingThrowType.Reflex) + 3, "Divine Grace: +3 to Reflex");
        AssertEq(p2.WillSave, Plain(p2, SavingThrowType.Will) + 3, "Divine Grace: +3 to Will");

        CharacterStats p1 = Build("Paladin", 1, cha: 16);
        AssertEq(p1.WillSave, Plain(p1, SavingThrowType.Will), "Divine Grace: none at paladin 1");

        CharacterStats low = Build("Paladin", 2, cha: 8);
        AssertEq(low.WillSave, Plain(low, SavingThrowType.Will), "Divine Grace: a CHA penalty is not applied");
    }

    private static void TestSaveFeats()
    {
        CharacterStats s = Build("Fighter", 1);
        s.Feats.Add("Iron Will");
        s.Feats.Add("Great Fortitude");
        s.Feats.Add("Lightning Reflexes");
        AssertEq(s.WillSave, Plain(s, SavingThrowType.Will) + 2, "Iron Will: +2 Will (PHB p.97)");
        AssertEq(s.FortitudeSave, Plain(s, SavingThrowType.Fortitude) + 2, "Great Fortitude: +2 Fortitude (PHB p.94)");
        AssertEq(s.ReflexSave, Plain(s, SavingThrowType.Reflex) + 2, "Lightning Reflexes: +2 Reflex (PHB p.97)");
    }

    // ── Racial bonuses (PHB p.15-20) ────────────────────────────────

    private static void TestHalfling()
    {
        CharacterStats h = Build("Fighter", 1, "Halfling");
        AssertEq(h.FortitudeSave, Plain(h, SavingThrowType.Fortitude) + 1, "Halfling: +1 racial bonus on Fortitude");
        AssertEq(h.ReflexSave, Plain(h, SavingThrowType.Reflex) + 1, "Halfling: +1 racial bonus on Reflex");
        AssertEq(h.WillSave, Plain(h, SavingThrowType.Will) + 1, "Halfling: +1 racial bonus on Will");
        AssertEq(SaveRules.Modifier(h, SavingThrowType.Will, SaveContext.Fear), h.WillSave + 2,
            "Halfling: +2 morale bonus against fear stacks with the +1 racial bonus");
        AssertEq(SaveRules.Modifier(h, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.CHARM_PERSON)), h.WillSave,
            "Halfling: no extra bonus against an enchantment");
        AssertEq(SaveRules.Modifier(h, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.CALM_EMOTIONS)), h.WillSave,
            "Halfling: no fear bonus against Calm Emotions (it suppresses fear; not a [Fear] spell, PHB p.207)");
        h.RemoveFearMoraleBonus = 4;
        AssertEq(SaveRules.Modifier(h, SavingThrowType.Will, SaveContext.Fear), h.WillSave + 4,
            "Halfling under Remove Fear: the morale bonuses do not stack (+4, not +6)");
        AssertEq(SaveRules.Modifier(h, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.SCARE)), h.WillSave + 4,
            "Remove Fear's +4 against the fear spell Scare");
    }

    private static void TestFearMoraleDoesNotStackWithRage()
    {
        CharacterStats hb = Build("Barbarian", 1, "Halfling");
        hb.IsRaging = true;
        AssertEq(SaveRules.Modifier(hb, SavingThrowType.Will, SaveContext.Fear), hb.WillSave,
            "Raging halfling barbarian: the +2 morale against fear does not stack with the rage's +2 morale on Will (PHB p.171, p.25)");
        AssertEq(SaveRules.Modifier(hb, SavingThrowType.Fortitude, SaveContext.Fear), hb.FortitudeSave + 2,
            "Raging halfling barbarian: the +2 against fear still applies on a Fortitude save (rage's morale is on Will only)");
        hb.RemoveFearMoraleBonus = 4;
        AssertEq(SaveRules.Modifier(hb, SavingThrowType.Will, SaveContext.Fear), hb.WillSave + 2,
            "Raging barbarian under Remove Fear: +4 morale, of which +2 beyond the rage's +2 (total morale +4, not +6)");
    }

    private static void TestFearSpellDetection()
    {
        Assert(!SaveContext.ForSpellId(SpellNames.CALM_EMOTIONS).IsFear, "Calm Emotions is not a fear effect");
        Assert(!SaveContext.ForSpellId(SpellNames.REMOVE_FEAR).IsFear, "Remove Fear is not a fear effect");
        Assert(SaveContext.ForSpellId(SpellNames.BANE).IsFear, "Bane is a fear effect (PHB p.203, [Fear])");
        Assert(SaveContext.ForSpellId(SpellNames.SCARE).IsFear, "Scare is a fear effect");
        Assert(SaveContext.ForSpellId(SpellNames.CAUSE_FEAR).IsFear, "Cause Fear is a fear effect");
    }

    private static void TestDwarf()
    {
        CharacterStats d = Build("Fighter", 1, "Dwarf");
        AssertEq(d.FortitudeSave, Plain(d, SavingThrowType.Fortitude), "Dwarf: no bonus against an ordinary effect");
        AssertEq(SaveRules.Modifier(d, SavingThrowType.Fortitude, SaveContext.Poison("medium_spider_poison")), d.FortitudeSave + 2,
            "Dwarf: +2 against poison");
        AssertEq(SaveRules.Modifier(d, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.HOLD_PERSON)), d.WillSave + 2,
            "Dwarf: +2 against spells");
        AssertEq(SaveRules.Modifier(d, SavingThrowType.Fortitude, SaveContext.ForSpellId(SpellNames.POISON)), d.FortitudeSave + 4,
            "Dwarf: the Poison spell gives +4 (spell +2 and poison +2; PHB p.171 lets racial bonuses stack; DMG p.21 reads otherwise, owner question in CHR-019)");
    }

    private static void TestElfAndHalfElf()
    {
        foreach (string race in new[] { "Elf", "Half-Elf" })
        {
            CharacterStats e = Build("Fighter", 1, race);
            AssertEq(SaveRules.Modifier(e, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.CHARM_PERSON)), e.WillSave + 2,
                race + ": +2 against an enchantment spell (Charm Person)");
            AssertEq(SaveRules.Modifier(e, SavingThrowType.Reflex, SaveContext.ForSpellId(SpellNames.FIREBALL)), e.ReflexSave,
                race + ": no bonus against an evocation (Fireball)");
        }
    }

    private static void TestGnome()
    {
        CharacterStats g = Build("Fighter", 1, "Gnome");
        AssertEq(SaveRules.Modifier(g, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.COLOR_SPRAY)), g.WillSave + 2,
            "Gnome: +2 against an illusion (Color Spray)");
        AssertEq(SaveRules.Modifier(g, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.CHARM_PERSON)), g.WillSave,
            "Gnome: no bonus against an enchantment");
    }

    // ── Conditional class features ──────────────────────────────────

    private static void TestStillMind()
    {
        CharacterStats m3 = Build("Monk", 3);
        SaveContext charm = SaveContext.ForSpellId(SpellNames.CHARM_PERSON);
        AssertEq(SaveRules.Modifier(m3, SavingThrowType.Will, charm), m3.WillSave + 2, "Still Mind: monk 3 +2 Will against an enchantment (PHB p.41)");
        AssertEq(SaveRules.Modifier(m3, SavingThrowType.Fortitude, charm), m3.FortitudeSave + 2, "Still Mind: it applies to every save against an enchantment");
        AssertEq(SaveRules.Modifier(m3, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.SCARE)), m3.WillSave, "Still Mind: not against a necromancy spell");
        CharacterStats m2 = Build("Monk", 2);
        AssertEq(SaveRules.Modifier(m2, SavingThrowType.Will, charm), m2.WillSave, "Still Mind: not at monk 2");
    }

    private static void TestTrapSense()
    {
        CharacterStats b3 = Build("Barbarian", 3);
        AssertEq(SaveRules.Modifier(b3, SavingThrowType.Reflex, SaveContext.Trap), b3.ReflexSave + 1, "Trap sense: barbarian 3 +1 Reflex against traps (PHB p.26)");
        AssertEq(SaveRules.Modifier(b3, SavingThrowType.Will, SaveContext.Trap), b3.WillSave, "Trap sense: Reflex only");
        AssertEq(b3.ReflexSave, Plain(b3, SavingThrowType.Reflex), "Trap sense: nothing against an ordinary effect");
        CharacterStats r6 = Build("Rogue", 6);
        AssertEq(SaveRules.Modifier(r6, SavingThrowType.Reflex, SaveContext.Trap), r6.ReflexSave + 2, "Trap sense: rogue 6 +2 (PHB p.51)");
        CharacterStats br = Build("Barbarian", 3);
        br.ClassLevels = new List<ClassLevelEntry> { new ClassLevelEntry("Barbarian", 3), new ClassLevelEntry("Rogue", 3) };
        br.InvalidateClassLevelCache();
        br.EnsureMulticlassDataInitialized();
        AssertEq(SaveRules.Modifier(br, SavingThrowType.Reflex, SaveContext.Trap), br.ReflexSave + 2, "Trap sense: barbarian 3 / rogue 3 bonuses stack");
    }

    private static void TestResistNaturesLure()
    {
        CharacterStats d4 = Build("Druid", 4);
        CharacterStats fey = Build("Fighter", 1);
        fey.CreatureType = "Fey";
        fey.ClassLevels = new List<ClassLevelEntry>();
        fey.InvalidateClassLevelCache();
        CharacterStats human = Build("Wizard", 1);
        AssertEq(SaveRules.Modifier(d4, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.CHARM_PERSON, fey)), d4.WillSave + 4,
            "Resist Nature's Lure: druid 4 +4 against a fey's spell-like ability (PHB p.36)");
        AssertEq(SaveRules.Modifier(d4, SavingThrowType.Will, SaveContext.ForSpellId(SpellNames.CHARM_PERSON, human)), d4.WillSave,
            "Resist Nature's Lure: nothing against a human wizard's spell");
    }

    private static void TestIndomitableWill()
    {
        CharacterStats b14 = Build("Barbarian", 14);
        b14.IsRaging = true;
        SaveContext charm = SaveContext.ForSpellId(SpellNames.CHARM_PERSON);
        AssertEq(SaveRules.Modifier(b14, SavingThrowType.Will, charm), b14.WillSave + 4, "Indomitable Will: raging barbarian 14 +4 Will against an enchantment spell (PHB p.26)");
        b14.IsRaging = false;
        AssertEq(SaveRules.Modifier(b14, SavingThrowType.Will, charm), b14.WillSave, "Indomitable Will: only while raging");
    }

    private static void TestAuraOfCourage()
    {
        var objects = new List<GameObject>();
        try
        {
            CharacterController MakeActor(string name, CharacterStats stats, CharacterTeam team, Vector2Int pos)
            {
                var go = new GameObject("SaveBonusRulesTests_" + name);
                objects.Add(go);
                CharacterController c = go.AddComponent<CharacterController>();
                c.Stats = stats;
                stats.OwnerCharacter = c;
                c.SetTeam(team);
                c.GridPosition = pos;
                return c;
            }

            CharacterStats palStats = Build("Paladin", 3, cha: 10);
            CharacterController paladin = MakeActor("Paladin", palStats, CharacterTeam.Player, new Vector2Int(10, 10));
            CharacterController near = MakeActor("Near", Build("Fighter", 1), CharacterTeam.Player, new Vector2Int(12, 10));
            CharacterController bent = MakeActor("Diagonal", Build("Fighter", 1), CharacterTeam.Player, new Vector2Int(12, 11));
            CharacterController far = MakeActor("Far", Build("Fighter", 1), CharacterTeam.Player, new Vector2Int(12, 12));
            CharacterController foe = MakeActor("Foe", Build("Fighter", 1), CharacterTeam.Enemy, new Vector2Int(11, 10));
            CharacterController halfling = MakeActor("Halfling", Build("Fighter", 1, "Halfling"), CharacterTeam.Player, new Vector2Int(10, 12));
            var all = new List<CharacterController> { paladin, near, bent, far, foe, halfling };

            AssertEq(SaveRules.AuraOfCourageBonus(near.Stats, all), 4, "Aura of Courage: an ally 10 ft away gets +4 morale against fear (PHB p.44)");
            AssertEq(SaveRules.AuraOfCourageBonus(bent.Stats, all), 4, "Aura of Courage: (2,1) squares is 10 ft with the 5-10-5 diagonal count");
            AssertEq(SaveRules.AuraOfCourageBonus(far.Stats, all), 0, "Aura of Courage: (2,2) squares is 15 ft, outside the aura");
            AssertEq(SaveRules.AuraOfCourageBonus(foe.Stats, all), 0, "Aura of Courage: an adjacent enemy gets nothing");
            AssertEq(SaveRules.AuraOfCourageBonus(palStats, all), 0, "Aura of Courage: no bonus for the paladin herself (she is immune instead)");
            AssertEq(SaveRules.AuraOfCourageBonus(halfling.Stats, all), 4, "Aura of Courage: a halfling 10 ft away also gets +4");

            int fullHp = palStats.CurrentHP;
            palStats.CurrentHP = -1;
            AssertEq(SaveRules.AuraOfCourageBonus(near.Stats, all), 0, "Aura of Courage: nothing while the paladin is dying");
            palStats.CurrentHP = fullHp;
            AssertEq(SaveRules.AuraOfCourageBonus(near.Stats, all), 4, "Aura of Courage: back once the paladin is conscious");

            CharacterStats pal2 = Build("Paladin", 2, cha: 10);
            palStats.ClassLevels = pal2.ClassLevels;
            palStats.InvalidateClassLevelCache();
            AssertEq(SaveRules.AuraOfCourageBonus(near.Stats, all), 0, "Aura of Courage: not at paladin 2");

            CharacterStats immune = Build("Paladin", 3);
            Assert(immune.IsImmuneToFear && immune.BlocksFearCondition(CombatConditionType.Frightened)
                && immune.BlocksFearCondition(CombatConditionType.Shaken) && immune.BlocksFearCondition(CombatConditionType.Panicked),
                "Aura of Courage: a paladin 3 is immune to fear (shaken, frightened, panicked)");
            immune.ApplyCondition(CombatConditionType.Frightened, 3, "Tester");
            Assert(immune.ActiveConditions == null || !immune.ActiveConditions.Exists(c => c.Type == CombatConditionType.Frightened),
                "Aura of Courage: frightened is not applied to the paladin");
            Assert(!immune.BlocksFearCondition(CombatConditionType.Stunned), "Aura of Courage: no immunity to other conditions");
            Assert(!Build("Paladin", 2).IsImmuneToFear, "Aura of Courage: a paladin 2 is not immune to fear");
        }
        finally
        {
            foreach (GameObject go in objects)
                Object.DestroyImmediate(go);
        }
    }

    private static void TestVenomImmunity()
    {
        Assert(Build("Druid", 9).IsImmuneToPoison(), "Venom immunity: a druid 9 is immune to poison (PHB p.37)");
        Assert(!Build("Druid", 8).IsImmuneToPoison(), "Venom immunity: not at druid 8");
    }

    // ── Items (DMG) ─────────────────────────────────────────────────

    private static void TestResistanceItemsDoNotStack()
    {
        CharacterStats s = Build("Fighter", 1);
        int bare = s.WillSave;
        global::Inventory inv = NewInventory(s);
        inv.DirectEquip(WondrousItemFactory.CreateCloakOfResistance(1), EquipSlot.Back);
        AssertEq(s.WillSave, bare + 1, "Cloak of Resistance +1 adds +1");
        inv.DirectEquip(RingFactory.CreateResistanceRing(2), EquipSlot.LeftRing);
        AssertEq(s.WillSave, bare + 2, "Ring of Resistance +2 with a +1 cloak: +2, not +3 (PHB p.171)");
    }

    private static void TestRobeOfTheArchmagi()
    {
        CharacterStats wiz = Build("Wizard", 3);
        int bareWiz = wiz.FortitudeSave;
        global::Inventory inv = NewInventory(wiz);
        inv.DirectEquip(WondrousItemFactory.CreateRobeOfTheArchmagi("neutral"), EquipSlot.Torso);
        AssertEq(wiz.FortitudeSave, bareWiz + 4, "Robe of the Archmagi: +4 resistance on saves for a wizard (DMG p.265)");
        inv.DirectEquip(WondrousItemFactory.CreateCloakOfResistance(2), EquipSlot.Back);
        AssertEq(wiz.FortitudeSave, bareWiz + 4, "Robe of the Archmagi with a +2 cloak: +4, not +6");
        AssertEq(wiz.GetCasterLevel(), 3, "Robe of the Archmagi: no caster level bonus (its +2 is on SR checks)");

        CharacterStats ftr = Build("Fighter", 3);
        int bareFtr = ftr.FortitudeSave;
        global::Inventory inv2 = NewInventory(ftr);
        inv2.DirectEquip(WondrousItemFactory.CreateRobeOfTheArchmagi("neutral"), EquipSlot.Torso);
        AssertEq(ftr.FortitudeSave, bareFtr, "Robe of the Archmagi: no save bonus for a fighter (arcane spellcasters only)");
        ftr.ClassLevels = new List<ClassLevelEntry> { new ClassLevelEntry("Fighter", 3), new ClassLevelEntry("Wizard", 1) };
        ftr.InvalidateClassLevelCache();
        ftr.EnsureMulticlassDataInitialized();
        AssertEq(ftr.FortitudeSave, Plain(ftr, SavingThrowType.Fortitude) + 4,
            "Robe of the Archmagi: a wizard level gained while wearing it counts at once (gate read at save time)");
    }

    private static void TestCompetenceAndLuckItems()
    {
        CharacterStats s = Build("Fighter", 1);
        int bare = s.ReflexSave;
        global::Inventory inv = NewInventory(s);
        inv.DirectEquip(WondrousItemFactory.CreateIounStonePaleGreenPrism(), EquipSlot.Slotless);
        AssertEq(s.ReflexSave, bare + 1, "Pale Green Prism: +1 competence on saves (DMG p.260)");
        inv.DirectEquip(WondrousItemFactory.CreateStoneOfGoodLuck(), EquipSlot.Slotless);
        AssertEq(s.ReflexSave, bare + 2, "Stone of Good Luck: +1 luck on top of the competence bonus");
        inv.DirectEquip(WondrousItemFactory.CreateRobeOfStars(), EquipSlot.Torso);
        AssertEq(s.ReflexSave, bare + 3, "Robe of Stars: +1 luck, stacking with the luckstone (house rule, BonusType.cs; DMG p.265)");
        inv.Unequip(EquipSlot.Torso);
        AssertEq(s.ReflexSave, bare + 2, "Robe of Stars removed: its luck bonus goes");
    }

    private static void TestCloakOfArachnida()
    {
        CharacterStats s = Build("Fighter", 1);
        global::Inventory inv = NewInventory(s);
        inv.DirectEquip(WondrousItemFactory.CreateCloakOfArachnida(), EquipSlot.Back);
        AssertEq(SaveRules.Modifier(s, SavingThrowType.Fortitude, SaveContext.Poison("medium_spider_poison")), s.FortitudeSave + 2,
            "Cloak of Arachnida: +2 luck on Fortitude saves against spider poison (DMG p.253)");
        AssertEq(SaveRules.Modifier(s, SavingThrowType.Fortitude, SaveContext.Poison("wyvern_poison")), s.FortitudeSave,
            "Cloak of Arachnida: nothing against other poison");
        AssertEq(s.FortitudeSave, Plain(s, SavingThrowType.Fortitude), "Cloak of Arachnida: nothing on an ordinary save");
    }

    private static void TestOrangePrismCasterLevel()
    {
        CharacterStats wiz = Build("Wizard", 3);
        global::Inventory inv = NewInventory(wiz);
        inv.DirectEquip(WondrousItemFactory.CreateIounStoneOrangePrism(), EquipSlot.Slotless);
        AssertEq(wiz.GetCasterLevel(), 4, "Orange Prism: wizard 3 casts at caster level 4 (DMG p.260)");
        AssertEq(wiz.GetCasterLevel("Wizard"), 4, "Orange Prism: also by class");
        CharacterStats ftr = Build("Fighter", 3);
        global::Inventory inv2 = NewInventory(ftr);
        inv2.DirectEquip(WondrousItemFactory.CreateIounStoneOrangePrism(), EquipSlot.Slotless);
        AssertEq(ftr.GetCasterLevel(), 0, "Orange Prism: a non-caster still has caster level 0");
    }

    // ── Spell save bonuses (StatusEffectManager) ────────────────────

    private static void TestSpellSaveBonusTypes()
    {
        var go = new GameObject("SaveBonusRulesTests_Subject");
        try
        {
            CharacterStats s = Build("Fighter", 1);
            StatusEffectManager sem = go.AddComponent<StatusEffectManager>();
            sem.Init(s);
            global::Inventory inv = NewInventory(s);
            int bare = s.WillSave;

            sem.AddEffect(SpellDatabase.GetSpell(SpellNames.RESISTANCE).Clone(), "Tester", 1);
            AssertEq(s.WillSave, bare + 1, "Resistance: +1 resistance on saves (PHB p.272)");
            inv.DirectEquip(WondrousItemFactory.CreateCloakOfResistance(1), EquipSlot.Back);
            AssertEq(s.WillSave, bare + 1, "Resistance with a +1 cloak: +1, not +2 (PHB p.171)");
            sem.RemoveEffectsBySpellId(SpellNames.RESISTANCE);
            AssertEq(s.WillSave, bare + 1, "Resistance ended: the cloak's +1 remains");
            AssertEq(s.MoraleSaveBonus, 0, "Resistance ended: nothing left in the spell save pool");

            inv.DirectEquip(WondrousItemFactory.CreateIounStonePaleGreenPrism(), EquipSlot.Slotless);
            sem.AddEffect(SpellDatabase.GetSpell(SpellNames.GUIDANCE).Clone(), "Tester", 1);
            AssertEq(s.WillSave, bare + 2, "Guidance with a Pale Green Prism: the competence bonuses do not stack (cloak +1, competence +1)");
            sem.RemoveEffectsBySpellId(SpellNames.GUIDANCE);

            sem.AddEffect(SpellDatabase.GetSpell(SpellNames.SHIELD_OTHER).Clone(), "Tester", 3);
            AssertEq(s.WillSave, bare + 2, "Shield Other with a +1 cloak: its +1 resistance on saves does not stack (PHB p.278)");
            AssertEq(s.ResistanceSaveBonusFromSpells, 1, "Shield Other: its save bonus is a resistance bonus");
            sem.RemoveEffectsBySpellId(SpellNames.SHIELD_OTHER);
            AssertEq(s.ResistanceSaveBonusFromSpells, 0, "Shield Other ended: no resistance bonus left");

            int beforeBless = s.WillSave;
            sem.AddEffect(SpellDatabase.GetSpell(SpellNames.BLESS).Clone(), "Tester", 1);
            AssertEq(s.WillSave, beforeBless, "Bless: no bonus on an ordinary save (PHB p.205: only against fear)");
            AssertEq(SaveRules.Modifier(s, SavingThrowType.Will, SaveContext.Fear), beforeBless + 1, "Bless: +1 morale against fear");
            sem.RemoveEffectsBySpellId(SpellNames.BLESS);
            AssertEq(SaveRules.Modifier(s, SavingThrowType.Will, SaveContext.Fear), beforeBless, "Bless ended: no bonus against fear");

            int beforeFear = s.WillSave;
            sem.AddEffect(SpellDatabase.GetSpell(SpellNames.REMOVE_FEAR).Clone(), "Tester", 1);
            s.RemoveFearMoraleBonus = 4; // set by the Remove Fear handler after AddEffect
            AssertEq(s.WillSave, beforeFear, "Remove Fear: no bonus on an ordinary save (PHB p.271: only against fear)");
            AssertEq(SaveRules.Modifier(s, SavingThrowType.Will, SaveContext.Fear), beforeFear + 4, "Remove Fear: +4 morale against fear");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ── The resolvers use the shared modifier ───────────────────────

    private static void TestResolversUseSharedModifier()
    {
        CharacterStats d = Build("Paladin", 2, "Dwarf", cha: 14);
        AssertEq(SavingThrowResolver.GetSaveModifier(d, SavingThrowResolver.SaveType.Will), d.WillSave,
            "SavingThrowResolver: plain modifier is the shared total (Divine Grace included)");
        AssertEq(SavingThrowResolver.GetSaveModifier(d, SavingThrowResolver.SaveType.Fortitude, SaveContext.Poison("x")), d.FortitudeSave + 2,
            "SavingThrowResolver: the dwarf's poison bonus through the context");
        SavingThrowResolver.SaveResult poison = SavingThrowResolver.ResolvePoisonSave(d, 99, "Medium Spider Venom");
        AssertEq(poison.Modifier, d.FortitudeSave + 2, "SavingThrowResolver.ResolvePoisonSave applies the poison bonus");
        SaveResult spell = SpellSaveResolver.RollSave(d, SaveType.Will, 99, SaveContext.ForSpellId(SpellNames.HOLD_PERSON));
        AssertEq(spell.Modifier, d.WillSave + 2, "SpellSaveResolver.RollSave applies the dwarf's spell bonus");
        AssertEq(SpellSaveResolver.RollSave(d, SaveType.Reflex, 99).Modifier, d.ReflexSave, "SpellSaveResolver.RollSave without a context: the shared total");
    }

    private static void TestSaveNameParsing()
    {
        Assert(SaveRules.TryParse("Fortitude", out SavingThrowType a) && a == SavingThrowType.Fortitude, "TryParse: Fortitude");
        Assert(SaveRules.TryParse("fort", out SavingThrowType b) && b == SavingThrowType.Fortitude, "TryParse: fort");
        Assert(SaveRules.TryParse(" REFLEX ", out SavingThrowType c) && c == SavingThrowType.Reflex, "TryParse: REFLEX with spaces");
        Assert(SaveRules.TryParse("Will negates", out SavingThrowType w) && w == SavingThrowType.Will, "TryParse: Will negates");
        Assert(!SaveRules.TryParse("None", out _), "TryParse: None is no save");
        CharacterStats s = Build("Fighter", 1);
        AssertEq(SaveRules.Modifier(s, "will"), s.WillSave, "Modifier by name: will");
        AssertEq(SaveRules.Modifier(s, ""), 0, "Modifier by name: empty is 0");
    }
}
}

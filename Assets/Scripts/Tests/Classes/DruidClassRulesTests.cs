using System.Collections.Generic;
using UnityEngine;

namespace Tests.Classes
{
/// <summary>
/// CHR-020 (druid): the druid's class features for levels 1-8 on one rules core for PCs and NPCs (PHB p.35-37).
/// Wild Shape (WildShapeRules): from 5th level, 1/2/3 uses a day at 5th/6th/7th; an animal of the bestiary, Small or
/// Medium (Large from 8th), Hit Dice up to the druid level, not a swarm, a templated entry or an alias; a standard action
/// either way. The form's Strength, Dexterity and Constitution (hit points by the new Constitution modifier), size,
/// natural armor, natural attacks, speed, body shape and extraordinary special attacks; the druid's mental scores, BAB
/// and class levels stay; her type becomes animal and her race is set aside; her worn and held gear melds (no weight, no
/// item use); no spell with a verbal or somatic component without Natural Spell; the change heals 1 hit point per
/// character level; changing back heals nothing and restores everything; slain, she reverts and stays dead; the rest
/// ends the form and restores the uses. The AI form choice. Nature Sense (+2 Knowledge (nature) and Survival) and Resist
/// Nature's Lure (+4 on saves against a fey's spell-like ability). A DMG lizardfolk druid 5 spawned through the encounter
/// spawner wild shapes as a PC does. Needs Play mode (CharacterController.Init; the NPC spawn uses the scene GameManager).
/// </summary>
public static class DruidClassRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== DRUID CLASS RULES TESTS (CHR-020) ==========");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();
        NPCDatabase.Init();

        TestUsesAndSizes();
        TestFormLegality();
        TestAssumeBlackBear();
        TestRevertRestoresEverything();
        TestRefusals();
        TestSpellsInForm();
        TestDeathReverts();
        TestRestEndsTheForm();
        TestLargeFormAtEighth();
        TestSpellBonusSurvivesTheForm();
        TestAiFormChoice();
        TestNatureSenseAndResistNaturesLure();
        TestNpcLizardfolkDruid();
        TestRacialBonusFeats();
        TestHitPointStateOnChange();
        TestMeldedConstitutionItem();
        TestRageAndDeathKnellUnderTheForm();
        TestDisguiseSelfAndTheForm();
        TestOnHitSaveDcInForm();
        TestWoodlandStride();

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

    /// <summary>A Human druid: Str 12, Dex 10, Con 12, Wis 16.</summary>
    private static CharacterStats Build(int level, string cls = "Druid")
    {
        var stats = new CharacterStats(
            name: cls + level, level: level, characterClass: cls,
            str: 12, dex: 10, con: 12, wis: 16, intelligence: 10, cha: 10,
            bab: 0, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: level * 8, raceName: "Human");
        stats.EnsureMulticlassDataInitialized();
        return stats;
    }

    /// <summary>A druid with the class kit (hide armor, scimitar, light wooden shield) and prepared druid spells.</summary>
    private static CharacterController Create(string name, int level, Vector2Int pos, string cls = "Druid")
    {
        var go = new GameObject(name + "_DruidRulesTest");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        CharacterStats stats = Build(level, cls);
        stats.CharacterName = name;
        controller.Init(stats, pos, null, null);
        inventory.Init(stats);
        if (cls == "Druid")
        {
            new DruidClass().SetupStartingEquipment(inventory);
            SpellcastingComponent spells = go.AddComponent<SpellcastingComponent>();
            spells.Init(stats);
            spells.AutoPrepareDruidSlots();
        }
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

    // ── Uses and sizes (PHB Table 3-8, p.37) ───────────────────────

    private static void TestUsesAndSizes()
    {
        AssertEq(WildShapeRules.UsesPerDay(Build(4)), 0, "Druid 4 has no wild shape");
        AssertEq(WildShapeRules.UsesPerDay(Build(5)), 1, "Druid 5: wild shape 1/day");
        AssertEq(WildShapeRules.UsesPerDay(Build(6)), 2, "Druid 6: 2/day");
        AssertEq(WildShapeRules.UsesPerDay(Build(7)), 3, "Druid 7: 3/day");
        AssertEq(WildShapeRules.UsesPerDay(Build(8)), 3, "Druid 8: still 3/day (4/day at 10th)");
        AssertEq(WildShapeRules.UsesPerDay(Build(5, "Ranger")), 0, "A ranger 5 has no wild shape");
        Assert(WildShapeRules.LargestSize(7) == SizeCategory.Medium && WildShapeRules.LargestSize(8) == SizeCategory.Large,
            "Small or Medium forms to 7th, Large from 8th");
        Assert(WildShapeRules.SmallestSize(8) == SizeCategory.Small && WildShapeRules.SmallestSize(11) == SizeCategory.Tiny,
            "Tiny forms only from 11th");
        AssertEq(WildShapeRules.DurationRounds(Build(5)), 3000, "Druid 5: 5 hours (3,000 rounds)");
    }

    // ── Form legality (PHB p.37, p.263) ────────────────────────────

    private static void TestFormLegality()
    {
        Assert(WildShapeRules.IsLegalForm(5, NPCDatabase.Get("wolf_pack_hunter"), out string r1), "Druid 5 may become a wolf (Medium, 2 HD)", r1);
        Assert(WildShapeRules.IsLegalForm(5, NPCDatabase.Get("black_bear"), out string r2), "Druid 5 may become a black bear (Medium, 3 HD)", r2);
        Assert(WildShapeRules.IsLegalForm(5, NPCDatabase.Get("badger"), out string r3), "Druid 5 may become a badger (Small)", r3);
        Assert(!WildShapeRules.IsLegalForm(5, NPCDatabase.Get("dire_wolf"), out string r4) && r4.Contains("Large"),
            "Druid 5 may not become a dire wolf (Large)", r4);
        Assert(WildShapeRules.IsLegalForm(8, NPCDatabase.Get("dire_wolf"), out string r5), "Druid 8 may become a dire wolf (Large, 6 HD)", r5);
        Assert(!WildShapeRules.IsLegalForm(8, NPCDatabase.Get("dire_tiger"), out string r6) && r6.Contains("HD"),
            "Druid 8 may not become a dire tiger (16 HD)", r6);
        Assert(!WildShapeRules.IsLegalForm(5, NPCDatabase.Get("hawk"), out string r7), "Druid 5 may not become a hawk (Tiny until 11th)", r7);
        Assert(!WildShapeRules.IsLegalForm(5, NPCDatabase.Get("bat_swarm"), out string r8), "A swarm is no form", r8);
        Assert(!WildShapeRules.IsLegalForm(5, NPCDatabase.Get("wolf"), out string r9), "The summon alias 'wolf' is not offered twice", r9);
        Assert(!WildShapeRules.IsLegalForm(5, NPCDatabase.Get("lizardfolk"), out string r10), "A humanoid is no form", r10);
        Assert(!WildShapeRules.IsLegalForm(4, NPCDatabase.Get("wolf_pack_hunter"), out string r11), "Druid 4 has no forms", r11);

        List<NPCDefinition> forms = WildShapeRules.GetAvailableForms(Build(5));
        bool allLegal = forms.Count > 0;
        foreach (NPCDefinition f in forms)
            allLegal &= WildShapeRules.IsLegalForm(5, f, out _);
        Assert(allLegal && forms.Exists(f => f.Id == "black_bear") && !forms.Exists(f => f.Id == "dire_wolf"),
            "The druid 5 form list holds only legal forms, the black bear but not the dire wolf", $"{forms.Count} forms");
        AssertEq(WildShapeRules.GetAvailableForms(Build(4)).Count, 0, "Druid 4 has an empty form list");
    }

    // ── Taking a form ──────────────────────────────────────────────

    private static void TestAssumeBlackBear()
    {
        CharacterController d = null;
        try
        {
            d = Create("BearDruid", 5, new Vector2Int(-80, -200));
            CharacterStats s = d.Stats;
            int ownAC = s.ArmorClass;
            int ownMax = s.TotalMaxHP;
            Assert(d.GetEquippedMainWeapon() != null && ownAC == 14, "Before: scimitar in hand, AC 14 (hide +3, light shield +1)", $"AC {ownAC}");
            s.CurrentHP = ownMax - 10;

            WildShapeResult result = WildShapeRules.Assume(d, "black_bear");
            Assert(result.Performed, "Druid 5 takes the black bear form", result.Refusal);
            if (!result.Performed)
                return;

            Assert(WildShapeRules.IsWildShaped(s) && s.ActiveWildShape.FormId == "black_bear", "She is in black bear form");
            Assert(s.STR == 19 && s.DEX == 13 && s.CON == 15, "The bear's Str 19, Dex 13, Con 15", $"{s.STR}/{s.DEX}/{s.CON}");
            Assert(s.WIS == 16 && s.INT == 10 && s.CHA == 10, "Her own Wis 16, Int 10, Cha 10");
            AssertEq(s.BaseAttackBonus, 3, "Her base attack bonus stays +3 (druid 5)");
            Assert(s.NaturalArmorBonus == 2 && s.ArmorBonus == 0 && s.ShieldBonus == 0, "Natural armor +2; armor and shield melded",
                $"NA {s.NaturalArmorBonus}, armor {s.ArmorBonus}, shield {s.ShieldBonus}");
            AssertEq(s.ArmorClass, 13, "AC 13 in bear form (10 + Dex 1 + natural 2)");
            Assert(d.GetEquippedMainWeapon() == null && d.UsesInnateNaturalAttackSequence(), "No weapon in hand; she fights with the bear's natural attacks");
            Assert(s.NaturalAttacks.Count == 2 && s.NaturalAttacks.Exists(a => a.Name == "Claw" && a.Count == 2) && s.NaturalAttacks.Exists(a => a.Name == "Bite"),
                "Two claws and a bite");
            Assert(s.CurrentSizeCategory == SizeCategory.Medium && s.BaseSpeed == 8, "Medium, speed 40 ft", $"{s.CurrentSizeCategory}, {s.BaseSpeed * 5} ft");
            Assert(s.CreatureType == "Animal" && s.Race == null, "Her type is animal and her race is set aside", $"{s.CreatureType}");
            Assert(s.IsExceptionallyStable && !s.HasScent, "The bear's four legs (stability), but not its scent (a special quality)");
            AssertEq(WildShapeRules.UsesLeft(s), 0, "The day's only use is spent");
            Assert(!d.Actions.HasStandardAction, "The change took her standard action");

            // Hit points: Con 12 (+1) to 15 (+2) is +5 at 5 HD; the change heals 5 (1 per level).
            AssertEq(s.TotalMaxHP, ownMax + 5, "Maximum hit points +5 from the Constitution modifier");
            AssertEq(s.CurrentHP, ownMax - 10 + 5 + 5, "Current hit points +5 (Constitution) +5 (a night's rest)");
            AssertEq(s.ActiveWildShape.HitPointsHealed, 5, "The change healed 5");

            global::Inventory inv = d.GetInventoryData();
            Assert(inv.ArmorRobeSlot == null && inv.RightHandSlot == null && inv.LeftHandSlot == null && s.ActiveWildShape.MeldedEquipment.Count == 3,
                "Hide armor, scimitar and shield are melded", $"{s.ActiveWildShape.MeldedEquipment.Count} melded");
            Assert(inv.GetTotalCarriedWeightLbs() == 0f && !WildShapeRules.CanManipulateItems(s), "Her gear weighs nothing and cannot be used");
            Assert(!inv.EquipFromInventory(0, EquipSlot.RightHand), "She cannot equip an item from her pack");
            Assert(d.DisplayedRace == "Black Bear", "The sheet shows the form", d.DisplayedRace);
        }
        finally
        {
            Cleanup(d);
        }
    }

    // ── Changing back ──────────────────────────────────────────────

    private static void TestRevertRestoresEverything()
    {
        CharacterController d = null;
        try
        {
            d = Create("RevertDruid", 5, new Vector2Int(-80, -204));
            CharacterStats s = d.Stats;
            int ownMax = s.TotalMaxHP;
            int ownAC = s.ArmorClass;
            s.CurrentHP = ownMax - 10;
            WildShapeRules.Assume(d, "black_bear");
            Assert(!WildShapeRules.CanRevert(d, out string noAction) && noAction.Contains("Standard"), "No change back in the same turn (standard action used)", noAction);
            d.StartNewTurn();
            WildShapeResult back = WildShapeRules.Revert(d);
            Assert(back.Performed, "She changes back with a standard action", back.Refusal);
            Assert(!WildShapeRules.IsWildShaped(s) && s.STR == 12 && s.DEX == 10 && s.CON == 12, "Her own Str 12, Dex 10, Con 12", $"{s.STR}/{s.DEX}/{s.CON}");
            Assert(s.CreatureType == "Humanoid" && s.Race != null && s.Race.RaceName == "Human", "Humanoid and human again");
            Assert(s.NaturalAttacks.Count == 0 && s.NaturalArmorBonus == 0 && s.BaseSpeed == 6 && !s.IsExceptionallyStable, "No natural attacks, natural armor or four-legged stability; 30 ft");
            AssertEq(s.ArmorClass, ownAC, "Her AC with armor and shield again");
            global::Inventory inv = d.GetInventoryData();
            Assert(inv.ArmorRobeSlot != null && inv.RightHandSlot != null && inv.RightHandSlot.Id == DND35e.Identifiers.ItemIDs.SCIMITAR && inv.LeftHandSlot != null,
                "Armor, scimitar and shield are back in their slots");
            AssertEq(s.TotalMaxHP, ownMax, "Maximum hit points back");
            AssertEq(s.CurrentHP, ownMax - 5, "Changing back takes the Constitution hit points away and heals nothing (the 5 healed stay)");
            Assert(d.DisplayedRace == "Human", "The sheet shows her race again", d.DisplayedRace);
            Assert(!d.Actions.HasStandardAction, "Changing back took the standard action");
        }
        finally
        {
            Cleanup(d);
        }
    }

    private static void TestRefusals()
    {
        CharacterController d4 = null, d6 = null;
        try
        {
            d4 = Create("YoungDruid", 4, new Vector2Int(-80, -208));
            Assert(!WildShapeRules.Assume(d4, "wolf_pack_hunter").Performed, "Druid 4 cannot wild shape");

            d6 = Create("TwiceDruid", 6, new Vector2Int(-80, -210));
            Assert(!WildShapeRules.Assume(d6, "dire_wolf").Performed, "Druid 6 cannot take a Large form");
            AssertEq(WildShapeRules.UsesLeft(d6.Stats), 2, "...and the refusal spends no use");
            Assert(d6.Actions.HasStandardAction, "...nor the standard action");
            Assert(WildShapeRules.Assume(d6, "wolf_pack_hunter").Performed, "Druid 6 becomes a wolf");
            Assert(d6.Stats.HasTripAttack, "The wolf's trip (an extraordinary special attack) comes with the form");
            d6.StartNewTurn();
            WildShapeResult again = WildShapeRules.Assume(d6, "black_bear");
            Assert(!again.Performed && again.Refusal.Contains("animal form"), "No change from one animal into another", again.Refusal);
            WildShapeRules.Revert(d6);
            Assert(!d6.Stats.HasTripAttack, "Trip goes with the form");
            d6.StartNewTurn();
            Assert(WildShapeRules.Assume(d6, "black_bear").Performed, "Her second use of the day: a black bear");
            d6.StartNewTurn();
            WildShapeRules.Revert(d6);
            d6.StartNewTurn();
            WildShapeResult third = WildShapeRules.Assume(d6, "wolf_pack_hunter");
            Assert(!third.Performed && third.Refusal.Contains("No uses"), "No third use at 6th level", third.Refusal);
        }
        finally
        {
            Cleanup(d4, d6);
        }
    }

    // ── Spells in animal form (PHB p.37, p.98) ─────────────────────

    private static void TestSpellsInForm()
    {
        var vs = new SpellData { SpellId = "test_vs", Name = "V,S test", SpellLevel = 1, HasVerbalComponent = true, HasSomaticComponent = true };
        var none = new SpellData { SpellId = "test_none", Name = "No V/S test", SpellLevel = 1, HasVerbalComponent = false, HasSomaticComponent = false };
        CharacterController d = null;
        try
        {
            d = Create("CasterDruid", 5, new Vector2Int(-80, -214));
            SpellcastingComponent spells = d.GetComponent<SpellcastingComponent>();
            int castable = spells.GetCastablePreparedSpells().Count;
            Assert(castable > 0 && spells.HasAnyCastablePreparedSpell(), "Before: the druid 5 has castable prepared spells", $"{castable}");
            WildShapeRules.Assume(d, "wolf_pack_hunter");
            Assert(spells.GetCastablePreparedSpells().Count == 0 && !spells.HasAnyCastablePreparedSpell(),
                "In wolf form no prepared spell is castable (she cannot speak or gesture)");
            Assert(!WildShapeRules.CanCastInCurrentForm(d.Stats, vs) && WildShapeRules.CanCastInCurrentForm(d.Stats, none),
                "A spell with neither a verbal nor a somatic component could still be cast");
            var silentOnly = new MetamagicData();
            silentOnly.AppliedMetamagic.Add(MetamagicFeatId.SilentSpell);
            var silentStill = new MetamagicData();
            silentStill.AppliedMetamagic.Add(MetamagicFeatId.SilentSpell);
            silentStill.AppliedMetamagic.Add(MetamagicFeatId.StillSpell);
            Assert(!WildShapeRules.CanCastInCurrentForm(d.Stats, vs, silentOnly), "A silent spell still has its somatic component: not in animal form");
            Assert(WildShapeRules.CanCastInCurrentForm(d.Stats, vs, silentStill), "A silent, stilled spell has no V or S component: castable in animal form (PHB p.100-101)");
            SpellData prepared = vs.Clone();
            prepared.MetamagicDataRef = silentStill;
            Assert(WildShapeRules.CanCastInCurrentForm(d.Stats, prepared), "...also when the metamagic comes with the prepared spell (MetamagicDataRef)");
            d.Stats.Feats.Add("Natural Spell");
            Assert(WildShapeRules.CanCastInCurrentForm(d.Stats, vs) && spells.GetCastablePreparedSpells().Count == castable,
                "With Natural Spell she casts in animal form");
            d.Stats.Feats.Remove("Natural Spell");
            d.StartNewTurn();
            WildShapeRules.Revert(d);
            AssertEq(spells.GetCastablePreparedSpells().Count, castable, "Back in her own form she casts again");
        }
        finally
        {
            Cleanup(d);
        }
    }

    // ── Death, rest ────────────────────────────────────────────────

    private static void TestDeathReverts()
    {
        CharacterController d = null;
        try
        {
            d = Create("DyingDruid", 5, new Vector2Int(-80, -218));
            int ownMax = d.Stats.TotalMaxHP;
            WildShapeRules.Assume(d, "black_bear");
            d.Stats.CurrentHP = -12;
            d.OnDeath();
            Assert(!WildShapeRules.IsWildShaped(d.Stats) && d.Stats.STR == 12, "Slain, she reverts to her own form");
            Assert(d.Stats.IsDead && d.Stats.CurrentHP == -12, "...and stays dead (her hit points unchanged)", $"{d.Stats.CurrentHP}");
            AssertEq(d.Stats.TotalMaxHP, ownMax, "...with her own maximum hit points");
            Assert(d.GetEquippedMainWeapon() != null, "...and her gear reappears");
        }
        finally
        {
            Cleanup(d);
        }
    }

    private static void TestRestEndsTheForm()
    {
        CharacterController d = null;
        try
        {
            d = Create("RestingDruid", 5, new Vector2Int(-80, -222));
            WildShapeRules.Assume(d, "wolf_pack_hunter");
            WildShapeRules.RefreshForRest(d);
            Assert(!WildShapeRules.IsWildShaped(d.Stats) && WildShapeRules.UsesLeft(d.Stats) == 1, "The rest ends the form and restores the use");
            Assert(d.GetEquippedMainWeapon() != null, "...with her gear back");
        }
        finally
        {
            Cleanup(d);
        }
    }

    // ── Large forms from 8th level ────────────────────────────────

    private static void TestLargeFormAtEighth()
    {
        CharacterController d = null;
        try
        {
            d = Create("LargeDruid", 8, new Vector2Int(-80, -226));
            WildShapeResult r = WildShapeRules.Assume(d, "dire_wolf");
            Assert(r.Performed, "Druid 8 becomes a dire wolf", r.Refusal);
            if (!r.Performed)
                return;
            CharacterStats s = d.Stats;
            Assert(s.BaseSizeCategory == SizeCategory.Large && s.CurrentSizeCategory == SizeCategory.Large && s.CurrentSizeCategory.GetSpaceWidthSquares() == 2,
                "Large, a 10-ft space", $"{s.CurrentSizeCategory}, {s.CurrentSizeCategory.GetSpaceWidthSquares()} squares wide");
            AssertEq(s.SizeModifier, -1, "Size modifier -1 on attacks and AC");
            s.GetScaledNaturalAttackDamage(s.NaturalAttacks[0], out int count, out int die);
            Assert(count == 1 && die == 8, "The dire wolf's bite deals its own 1d8 (no rescaling)", $"{count}d{die}");
            AssertEq(s.ArmorClass, 10 + 2 - 1 + 3, "AC 14 (Dex 15 +2, size -1, natural +3)");
            d.StartNewTurn();
            WildShapeRules.Revert(d);
            Assert(s.BaseSizeCategory == SizeCategory.Medium && s.CurrentSizeCategory == SizeCategory.Medium, "Medium again after changing back");
        }
        finally
        {
            Cleanup(d);
        }
    }

    /// <summary>A spell bonus to Strength stays on top of the form's score and is gone after (PHB p.263, p.171).</summary>
    private static void TestSpellBonusSurvivesTheForm()
    {
        CharacterController d = null;
        try
        {
            d = Create("BuffedDruid", 5, new Vector2Int(-80, -230));
            CharacterStats s = d.Stats;
            object owner = new object();
            s.Bonuses.Add(owner, BonusTarget.Strength, BonusType.Enhancement, 4, "test bull's strength");
            s.SyncAbilityScoreWithBonuses(AbilityType.STR);
            AssertEq(s.STR, 16, "Bull's strength +4: Str 16");
            AssertEq(s.NaturalAbilityScore(AbilityType.STR), 12, "Her own Strength is 12");
            WildShapeRules.Assume(d, "black_bear");
            AssertEq(s.STR, 23, "In bear form: the bear's 19 + 4");
            s.Bonuses.RemoveOwner(owner);
            s.SyncAbilityScoreWithBonuses(AbilityType.STR);
            AssertEq(s.STR, 19, "The spell ends in bear form: 19");
            d.StartNewTurn();
            WildShapeRules.Revert(d);
            AssertEq(s.STR, 12, "Back in her own form: 12");
        }
        finally
        {
            Cleanup(d);
        }
    }

    // ── AI choice (not a rule) ─────────────────────────────────────

    private static void TestAiFormChoice()
    {
        CharacterController d = null, foe = null;
        try
        {
            d = Create("AiDruid", 5, new Vector2Int(-80, -234));
            foe = Create("AiFoe", 3, new Vector2Int(-60, -234), "Fighter");
            foe.SetTeam(d.Team == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player);
            var all = new List<CharacterController> { d, foe };
            Assert(DND35.AI.AIProfile.DefaultWildShapeForm(d, all) == null, "A druid with spells left and no foe next to her keeps casting");
            foe.GridPosition = new Vector2Int(-79, -234);
            foe.Stats.CurrentHP = -3;
            foe.SyncHPStateFromCurrentHP();
            Assert(DND35.AI.AIProfile.DefaultWildShapeForm(d, all) == null, "A dying foe next to her is no threat: she keeps casting (CHR-031)");
            foe.Stats.CurrentHP = foe.Stats.TotalMaxHP;
            foe.SyncHPStateFromCurrentHP();
            NPCDefinition pick = DND35.AI.AIProfile.DefaultWildShapeForm(d, all);
            Assert(pick != null && WildShapeRules.IsLegalForm(5, pick, out _), "With a foe next to her she picks a legal form", pick != null ? pick.Id : "none");
            if (pick != null)
            {
                int ac = foe.Stats.ArmorClass;
                float best = 0f;
                foreach (NPCDefinition f in WildShapeRules.GetAvailableForms(d.Stats))
                    best = Mathf.Max(best, DND35.AI.AIProfile.ExpectedFormDamage(d.Stats, f, ac));
                Assert(Mathf.Approximately(DND35.AI.AIProfile.ExpectedFormDamage(d.Stats, pick, ac), best)
                    && best > DND35.AI.AIProfile.ExpectedWeaponDamage(d.Stats, ac),
                    "...the one with the best expected full attack, better than her scimitar", pick.Id);
            }
            WildShapeRules.Assume(d, "wolf_pack_hunter");
            d.StartNewTurn();
            Assert(DND35.AI.AIProfile.DefaultWildShapeForm(d, all) == null, "No wild shape while already in animal form");
        }
        finally
        {
            Cleanup(d, foe);
        }
    }

    // ── Nature Sense, Resist Nature's Lure (PHB p.35, p.37) ──────────

    private static void TestNatureSenseAndResistNaturesLure()
    {
        CharacterStats druid = Build(1);
        CharacterStats fighter = Build(1, "Fighter");
        if (!druid.Skills.ContainsKey("Survival")) druid.InitializeSkills("Druid", 1);
        if (!fighter.Skills.ContainsKey("Survival")) fighter.InitializeSkills("Fighter", 1);
        AssertEq(druid.GetClassFeatureSkillModifier("Survival") - fighter.GetClassFeatureSkillModifier("Survival"), 2, "Nature Sense: +2 Survival");
        AssertEq(druid.GetClassFeatureSkillModifier("Knowledge (Nature)"), 2, "Nature Sense: +2 Knowledge (nature)");
        AssertEq(druid.GetClassFeatureSkillModifier("Spot"), 0, "...and nothing on Spot");
        AssertEq(druid.GetSkillBonus("Survival") - fighter.GetSkillBonus("Survival"), 2, "The +2 is in the Survival total (same Wis)");

        CharacterStats d3 = Build(3), d4 = Build(4);
        var feySla = new SaveContext(feySpellLike: true, spell: true, label: "test fey spell-like ability");
        var plainSpell = new SaveContext(spell: true, label: "test spell");
        int d4Fey = SaveRules.Modifier(d4, SavingThrowType.Will, feySla);
        int d4Plain = SaveRules.Modifier(d4, SavingThrowType.Will, plainSpell);
        AssertEq(d4Fey - d4Plain, 4, "Resist Nature's Lure: druid 4 +4 against a fey's spell-like ability");
        AssertEq(SaveRules.Modifier(d3, SavingThrowType.Will, feySla) - SaveRules.Modifier(d3, SavingThrowType.Will, plainSpell), 0,
            "...not before 4th level");
    }

    // ── An NPC druid from a DMG encounter row ─────────────────────

    private static void TestNpcLizardfolkDruid()
    {
        if (GameManager.Instance == null)
        {
            Assert(false, "The NPC spawn check needs the scene GameManager (Play mode)");
            return;
        }
        DungeonEncounterSpawner.SpawnResult spawn = DungeonEncounterSpawner.PrepareEncounter(
            new EncounterDefinition("Druid test").AddCreatureWithClass("lizardfolk", "Druid", 5, count: 1));
        CharacterController npc = null;
        try
        {
            Assert(spawn.Count == 1, "The DMG lizardfolk druid 5 row resolves", string.Join("; ", spawn.Warnings));
            if (spawn.Count != 1)
                return;
            var go = new GameObject("DruidRulesTest_lizardfolk");
            npc = go.AddComponent<CharacterController>();
            GameManager.Instance.InitializeNPCFromDefinition(npc, NPCDatabase.Get(spawn.EnemyIds[0]).Clone(), new Vector2Int(-90, -240), null, null);
            CharacterStats s = npc.Stats;
            Assert(WildShapeRules.DruidLevel(s) == 5 && WildShapeRules.UsesPerDay(s) == 1, "The NPC is a druid 5 with 1 wild shape a day",
                $"druid {WildShapeRules.DruidLevel(s)}");
            int bab = s.BaseAttackBonus;
            int wis = s.WIS;
            npc.StartNewTurn();
            WildShapeResult r = WildShapeRules.Assume(npc, "black_bear");
            Assert(r.Performed, "The NPC druid takes the black bear form through the same rule", r.Refusal);
            Assert(s.STR == 19 && s.WIS == wis && s.BaseAttackBonus == bab && s.CreatureType == "Animal",
                "...with the bear's Strength, its own Wisdom and base attack bonus, and the animal type");
            Assert(s.NaturalAttacks.Exists(a => a.Name == "Claw") && !s.NaturalAttacks.Exists(a => a.Name == "Bite" && a.DamageDice == 4),
                "...and the bear's claws and bite instead of the lizardfolk's own attacks");
            npc.StartNewTurn();
            WildShapeRules.Revert(npc);
            Assert(s.CreatureType == "Humanoid" && s.NaturalAttacks.Exists(a => a.Name == "Bite" && a.DamageDice == 4),
                "Changing back restores the lizardfolk's type and its own bite", s.CreatureType);
        }
        finally
        {
            Cleanup(npc);
            DungeonEncounterSpawner.CleanupSpawnEntries(spawn);
        }
    }

    // ── Bonus feats, hit points, temporary scores, sheet, DCs ──────

    /// <summary>The form's racial bonus feats come with it and go with it; its other feats do not (PHB p.197).</summary>
    private static void TestRacialBonusFeats()
    {
        NPCDefinition template = NPCDatabase.Get("viper_medium");
        NPCDefinition clone = template != null ? template.Clone() : null;
        Assert(clone != null && clone.BonusFeats.Contains("Weapon Finesse") && !ReferenceEquals(clone.BonusFeats, template.BonusFeats),
            "NPCDefinition.Clone copies the racial bonus feats into a list of its own (CRE-023)");

        CharacterController d = null, own = null, wolf = null;
        try
        {
            d = Create("ViperDruid", 5, new Vector2Int(-80, -250));
            CharacterStats s = d.Stats;
            AttackCalculator.GetAttackAbilityModifier(s, null, false, out int ownMod, out string ownName);
            Assert(!s.HasFeat("Weapon Finesse") && ownName == "STR", "Before: no Weapon Finesse, natural attacks with Str", ownName);
            WildShapeResult r = WildShapeRules.Assume(d, "viper_medium");
            Assert(r.Performed, "Druid 5 becomes a Medium viper", r.Refusal);
            if (r.Performed)
            {
                AttackCalculator.GetAttackAbilityModifier(s, null, false, out int mod, out string name);
                Assert(s.HasFeat("Weapon Finesse") && name == "DEX(Finesse)" && mod == 3,
                    "In viper form she has its racial Weapon Finesse and bites with Dex 17 (+3)", $"{name} {mod}");
                Assert(s.ActiveWildShape.GainedFeats.Count == 1 && !s.HasFeat("Improved Initiative"),
                    "Only the bonus feat is gained, not the viper's other feats", string.Join(",", s.ActiveWildShape.GainedFeats));
                d.StartNewTurn();
                WildShapeRules.Revert(d);
                AttackCalculator.GetAttackAbilityModifier(s, null, false, out _, out string backName);
                Assert(!s.HasFeat("Weapon Finesse") && backName == "STR", "Changing back takes the feat away again", backName);
            }

            own = Create("FinesseDruid", 5, new Vector2Int(-80, -252));
            own.Stats.Feats.Add("Weapon Finesse");
            WildShapeRules.Assume(own, "viper_medium");
            Assert(own.Stats.ActiveWildShape != null && own.Stats.ActiveWildShape.GainedFeats.Count == 0, "A druid who took Weapon Finesse gains nothing new");
            own.StartNewTurn();
            WildShapeRules.Revert(own);
            Assert(own.Stats.HasFeat("Weapon Finesse"), "...and keeps her own Weapon Finesse after changing back");

            wolf = Create("TrackDruid", 5, new Vector2Int(-80, -254));
            WildShapeRules.Assume(wolf, "wolf_pack_hunter");
            Assert(wolf.Stats.HasFeat("Track") && !wolf.Stats.HasFeat("Weapon Focus"), "The wolf's racial Track comes with the form, its Weapon Focus does not");
            wolf.StartNewTurn();
            WildShapeRules.Revert(wolf);
            Assert(!wolf.Stats.HasFeat("Track"), "Track goes with the form");
        }
        finally
        {
            Cleanup(d, own, wolf);
        }
    }

    /// <summary>A change of hit points moves the HP state (PHB p.145); the rest's revert never kills.</summary>
    private static void TestHitPointStateOnChange()
    {
        CharacterController a = null, b = null, c = null, e = null;
        try
        {
            // Bear form: Con 12 -> 15 is +5 hp at 5 HD. At 3 hp she changes back to -2: dying.
            a = Create("LowDruid", 5, new Vector2Int(-80, -258));
            WildShapeRules.Assume(a, "black_bear");
            a.Stats.CurrentHP = 3;
            a.SyncHPStateFromCurrentHP();
            a.StartNewTurn();
            WildShapeRules.Revert(a);
            Assert(a.Stats.CurrentHP == -2 && a.CurrentHPState == HPState.Dying && a.IsUnconscious && !a.CanTakeTurnActions() && !a.IsDead,
                "Changing back at 3 hp leaves her dying at -2", $"{a.Stats.CurrentHP} {a.CurrentHPState}");

            // A change back that takes her to -10 or below kills her through the HP state.
            b = Create("DeadDruid", 5, new Vector2Int(-80, -260));
            WildShapeRules.Assume(b, "black_bear");
            b.Stats.CurrentHP = -6;
            b.SyncHPStateFromCurrentHP();
            b.StartNewTurn();
            WildShapeRules.Revert(b);
            Assert(b.Stats.CurrentHP == -11 && b.IsDead && b.Stats.IsDead, "At -6 the change back puts her at -11: dead (controller.IsDead)",
                $"{b.Stats.CurrentHP} {b.CurrentHPState}");

            // A disabled druid at 0 hp wild shapes: the change heals her above 0, and the standard action costs her 1 hp
            // once the act is complete (PHB p.145, CharacterController.ResolvePendingStrenuousDamage at the turn end).
            c = Create("DisabledDruid", 5, new Vector2Int(-80, -262));
            c.Stats.CurrentHP = 0;
            c.SyncHPStateFromCurrentHP();
            Assert(c.CurrentHPState == HPState.Disabled, "At 0 hp she is disabled");
            WildShapeRules.Assume(c, "black_bear");
            c.ResolvePendingStrenuousDamage();
            Assert(c.Stats.CurrentHP == 9 && c.CurrentHPState == HPState.Healthy && c.CanTakeTurnActions(),
                "After the change (+5 Con and +5 rest, then -1 for the strenuous act) she is at 9 hp and no longer disabled", $"{c.Stats.CurrentHP} {c.CurrentHPState}");

            // The post-combat rest ends the form of a dying druid without the Constitution loss: no death.
            e = Create("RestingDyingDruid", 5, new Vector2Int(-80, -264));
            WildShapeRules.Assume(e, "black_bear");
            e.Stats.CurrentHP = -8;
            e.SyncHPStateFromCurrentHP();
            WildShapeRules.RefreshForRest(e);
            Assert(!WildShapeRules.IsWildShaped(e.Stats) && !e.IsDead && !e.Stats.IsDead && e.Stats.CurrentHP == -8,
                "The rest's revert of a dying druid at -8 does not kill her (the rest heals her next)", $"{e.Stats.CurrentHP} {e.CurrentHPState}");
        }
        finally
        {
            Cleanup(a, b, c, e);
        }
    }

    /// <summary>A melded amulet of health takes its hit points with it and gives them back (PHB p.197, p.10).</summary>
    private static void TestMeldedConstitutionItem()
    {
        CharacterController d = null;
        try
        {
            d = Create("AmuletDruid", 5, new Vector2Int(-80, -266));
            CharacterStats s = d.Stats;
            int maxWithout = s.TotalMaxHP;
            global::Inventory inv = d.GetInventoryData();
            ItemData amulet = WondrousItemFactory.CreateAmuletOfHealth(2);
            Assert(inv.DirectEquip(amulet, amulet.Slot), "The druid wears an amulet of health +2");
            int ownMax = s.TotalMaxHP;
            AssertEq(ownMax - maxWithout, 5, "Con 12 + 2 = 14: +5 hp at 5 HD");
            s.CurrentHP = ownMax;

            WildShapeRules.Assume(d, "black_bear");
            Assert(s.ActiveWildShape != null && s.ActiveWildShape.MeldHitPointLoss == 5, "The melded amulet takes its 5 hp",
                s.ActiveWildShape != null ? s.ActiveWildShape.MeldHitPointLoss.ToString() : "not shaped");
            Assert(s.CurrentHP <= s.TotalMaxHP, "In the form her hit points do not exceed her maximum", $"{s.CurrentHP}/{s.TotalMaxHP}");
            AssertEq(s.TotalMaxHP, ownMax, "Bear form: -5 (amulet) +5 (Con 15) = her old maximum");

            d.StartNewTurn();
            WildShapeRules.Revert(d);
            Assert(inv.GetEquipped(amulet.Slot) == amulet, "The amulet is back");
            AssertEq(s.TotalMaxHP, ownMax, "Her maximum with the amulet again");
            AssertEq(s.CurrentHP, ownMax, "A druid at full hit points is still at full after the change and back");
        }
        finally
        {
            Cleanup(d);
        }
    }

    /// <summary>Rage and Death Knell, written straight into the scores, stay on top of the form (PHB p.25, p.263).</summary>
    private static void TestRageAndDeathKnellUnderTheForm()
    {
        CharacterController d = null;
        try
        {
            d = Create("RagingDruid", 5, new Vector2Int(-80, -268));
            CharacterStats s = d.Stats;
            int ownMax = s.TotalMaxHP;
            // A rage as CharacterStats.ActivateRage writes it (+4 Str and Con, +2 hp per Hit Die).
            s.IsRaging = true;
            s.STR += 4;
            s.CON += 4;
            s.AdjustMaxHP(10);
            s.CurrentHP += 10;
            AssertEq(s.NaturalAbilityScore(AbilityType.STR), 12, "Raging, her own Strength is still 12");
            WildShapeRules.Assume(d, "black_bear");
            Assert(s.STR == 23 && s.CON == 19, "Raging bear: 19 + 4 Str, 15 + 4 Con", $"{s.STR}/{s.CON}");
            s.DeactivateRage();
            Assert(s.STR == 19 && s.CON == 15, "The rage ends in bear form: the bear's own 19 and 15", $"{s.STR}/{s.CON}");
            d.StartNewTurn();
            WildShapeRules.Revert(d);
            Assert(s.STR == 12 && s.CON == 12, "Back in her own form: 12 and 12", $"{s.STR}/{s.CON}");
            AssertEq(s.TotalMaxHP, ownMax, "...with her own maximum hit points");

            // Death Knell's +2 Strength as EffectService writes it.
            s.WildShapeUsesToday = 0;
            s.DeathKnellActive = true;
            s.DeathKnellStrBonus = 2;
            s.STR += 2;
            d.StartNewTurn();
            WildShapeRules.Assume(d, "black_bear");
            AssertEq(s.STR, 21, "Death Knell's +2 stays on top of the bear's 19");
            s.STR -= s.DeathKnellStrBonus;
            s.DeathKnellActive = false;
            s.DeathKnellStrBonus = 0;
            d.StartNewTurn();
            WildShapeRules.Revert(d);
            AssertEq(s.STR, 12, "Death Knell ended in the form; back in her own form she has Str 12");
        }
        finally
        {
            Cleanup(d);
        }
    }

    /// <summary>The sheet shows the form, and a Disguise Self that is still running after the change back.</summary>
    private static void TestDisguiseSelfAndTheForm()
    {
        CharacterController d = null;
        try
        {
            d = Create("DisguisedDruid", 6, new Vector2Int(-80, -270));
            d.ApplyDisguiseSelfEffect("Elf", 50, d);
            WildShapeRules.Assume(d, "black_bear");
            Assert(d.DisplayedRace == "Black Bear", "In the form the sheet shows the bear", d.DisplayedRace);
            d.StartNewTurn();
            WildShapeRules.Revert(d);
            Assert(d.DisplayedRace == "Elf", "Back in her own form the running Disguise Self shows again", d.DisplayedRace);
            d.StartNewTurn();
            WildShapeRules.Assume(d, "black_bear");
            d.ClearDisguiseSelfEffect();
            Assert(d.DisplayedRace == "Black Bear", "Disguise Self ending in the form leaves the form on the sheet", d.DisplayedRace);
            d.StartNewTurn();
            WildShapeRules.Revert(d);
            Assert(d.DisplayedRace == "Human", "...and her own race after the change back", d.DisplayedRace);
        }
        finally
        {
            Cleanup(d);
        }
    }

    /// <summary>The DC of the form's poison is figured for the druid (MM p.313, p.6; delegated RAW reading 2026-10-14).</summary>
    private static void TestOnHitSaveDcInForm()
    {
        CharacterController d = null;
        try
        {
            d = Create("VenomDruid", 5, new Vector2Int(-80, -272));
            AssertEq(WildShapeRules.OnHitSaveDcModifier(d.Stats, 11), 0, "In her own form nothing changes a printed DC");
            WildShapeRules.Assume(d, "viper_medium");
            // A human has no racial Hit Dice; the viper's Con 11 gives +0: DC 10, one less than a DC 11 printed.
            AssertEq(WildShapeRules.OnHitSaveDcModifier(d.Stats, 11), -1, "In viper form: 10 + 0 racial HD / 2 + Con +0 = DC 10");
        }
        finally
        {
            Cleanup(d);
        }
    }

    private static void TestWoodlandStride()
    {
        Assert(!Build(1).HasWoodlandStride && Build(2).HasWoodlandStride, "Woodland Stride: a druid from 2nd level (PHB p.36)");
        Assert(!Build(6, "Ranger").HasWoodlandStride && Build(7, "Ranger").HasWoodlandStride, "...a ranger from 7th (PHB p.48)");
    }
}
}

using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Character
{
/// <summary>
/// CRE-002 and ITM-004: a spawned creature gets its definition's alignment and the gear its NPC data lists under the
/// MainHand, OffHand and Ranged aliases. Rules: MM entries give each creature an alignment line (MM p.7); celestial
/// creatures are always good (any) and fiendish ones always evil (any) (MM p.31, p.108), so the templates fix the
/// good-evil component and the creature keeps the base creature's law-chaos component (owner interpretation
/// 2026-10-11, CRE-057); an illegal base (celestial on an evil one, fiendish on a good one) is refused and keeps its
/// base statistics, and every templated Summon Monster option has a legal base (its alignment comes from the PHB table).
/// Skeletons and zombies are always neutral evil (MM p.226, p.266). Gear: MainHand is the right hand and OffHand the
/// left hand (ItemData.ResolveHandAlias, also at the bottom inventory layer); a weapon listed under Ranged is held by
/// a creature the AI runs through the ranged routine (AIService.RoutesToRangedTurn) and otherwise carried in the
/// pack (Inventory.EquipStartingLoadout), from which it can draw it in play (batch 13 step 15, D8; AiWeaponFallback). The alias and template
/// tests need no scene; the spawn tests go through
/// GameManager.InitializeNPCFromDefinition and need Play mode. Spawned controllers sit off the grid and are destroyed
/// afterwards. Database templates are cloned before any change.
/// </summary>
public static class NpcAlignmentGearTests
{
    private static int _passed;
    private static int _failed;

    private static readonly Vector2Int SpawnPos = new Vector2Int(-60, -60);

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        NPCDatabase.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();

        Debug.Log("====== NPC ALIGNMENT AND GEAR TESTS (CRE-002, ITM-004) ======");

        var snapshots = SnapshotDatabaseEntries("ogre", "human_paladin_3", "human_monk_3", "janni");

        TestHandAliases();
        TestDirectEquipAliases();
        TestInventoryAliasPaths();
        TestRangedRoutingPredicate();
        TestStartingLoadoutPolicy();
        TestGoodEvilAxis();
        TestOutsiderTemplates();
        TestUndeadTemplates();
        TestEveryDefinitionHasAlignment();
        TestMonsterManualAlignments();

        if (GameManager.Instance == null)
        {
            Assert(false, "NPC spawn alignment and gear tests need Play mode with a scene GameManager");
        }
        else
        {
            TestSpawnedAlignments();
            TestSpawnedTemplateAlignments();
            TestSpawnedHandAliasGear();
            TestSpawnedRangedGear();
            TestSpawnedRangedProfileGear();
        }

        TestDatabaseTemplatesUnchanged(snapshots);

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

    private static CharacterStats MakeChar()
    {
        return new CharacterStats(
            name: "Gear Test", level: 1, characterClass: "Warrior",
            str: 12, dex: 12, con: 12, wis: 10, intelligence: 10, cha: 10,
            bab: 1, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 8);
    }

    private static global::Inventory NewInventory()
    {
        return new global::Inventory { OwnerStats = MakeChar() };
    }

    /// <summary>A clone of the item <paramref name="id"/>; a missing id fails a check.</summary>
    private static ItemData Clone(string id)
    {
#pragma warning disable CS0618 // string item ids are the NPC data's own ids
        ItemData item = ItemDatabase.CloneItem(id);
#pragma warning restore CS0618
        if (item == null)
            Assert(false, "item id " + id + " exists in ItemDatabase");
        return item;
    }

    private static bool PackHas(global::Inventory inv, string idFragment)
    {
        if (inv == null || inv.GeneralSlots == null)
            return false;
        foreach (ItemData item in inv.GeneralSlots)
            if (item != null && item.Id != null && item.Id.Contains(idFragment))
                return true;
        return false;
    }

    private static bool Holds(ItemData slot, string idFragment)
    {
        return slot != null && slot.Id != null && slot.Id.Contains(idFragment);
    }

    private static string Describe(ItemData item) => item != null ? item.Id : "empty";

    private static CharacterController Spawn(NPCDefinition def, string label)
    {
        var go = new GameObject("NpcAlignmentGearTest_" + label);
        CharacterController cc = go.AddComponent<CharacterController>();
        GameManager.Instance.InitializeNPCFromDefinition(cc, def, SpawnPos, null, null);
        return cc;
    }

    /// <summary>Spawns a clone of <paramref name="npcId"/> after the spawn-time template step (as encounters do).</summary>
    private static CharacterController Spawn(string npcId)
    {
        NPCDefinition template = NPCDatabase.Get(npcId);
        if (template == null)
        {
            Assert(false, npcId + " exists in NPCDatabase");
            return null;
        }
        return Spawn(CreatureTemplateRegistry.ApplyTemplatesClone(template), npcId);
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

    // ── Hand aliases (ITM-004) ──────────────────────────────────────────

    private static void TestHandAliases()
    {
        Assert(ItemData.ResolveHandAlias(EquipSlot.MainHand) == EquipSlot.RightHand, "MainHand resolves to the right hand");
        Assert(ItemData.ResolveHandAlias(EquipSlot.Ranged) == EquipSlot.RightHand, "Ranged resolves to the right hand");
        Assert(ItemData.ResolveHandAlias(EquipSlot.OffHand) == EquipSlot.LeftHand, "OffHand resolves to the left hand");
        Assert(ItemData.ResolveHandAlias(EquipSlot.Armor) == EquipSlot.Armor, "other slots are unchanged");

        ItemData longsword = Clone(ItemIDs.LONGSWORD);
        ItemData shield = Clone(ItemIDs.SHIELD_HEAVY_STEEL);
        ItemData longbow = Clone(ItemIDs.LONGBOW);
        if (longsword == null || shield == null || longbow == null)
            return;
        Assert(longsword.CanEquipIn(EquipSlot.MainHand) && longsword.CanEquipIn(EquipSlot.OffHand),
            "a longsword (either hand) can be equipped in MainHand and OffHand");
        Assert(shield.CanEquipIn(EquipSlot.OffHand), "a heavy steel shield can be equipped in OffHand");
        Assert(!shield.CanEquipIn(EquipSlot.MainHand), "a shield cannot be equipped in MainHand (the right hand)");
        Assert(longbow.CanEquipIn(EquipSlot.Ranged), "a longbow can be equipped in Ranged");
        ItemData plate = Clone(ItemIDs.FULL_PLATE);
        Assert(plate != null && !plate.CanEquipIn(EquipSlot.MainHand), "armor cannot be equipped in MainHand");
    }

    private static void TestDirectEquipAliases()
    {
        global::Inventory inv = NewInventory();
        ItemData sword = Clone(ItemIDs.LONGSWORD);
        ItemData shield = Clone(ItemIDs.SHIELD_HEAVY_STEEL);
        Assert(inv.DirectEquip(sword, EquipSlot.MainHand) && inv.RightHandSlot == sword,
            "DirectEquip(MainHand) puts the longsword in the right hand", "right " + Describe(inv.RightHandSlot));
        Assert(inv.DirectEquip(shield, EquipSlot.OffHand) && inv.LeftHandSlot == shield,
            "DirectEquip(OffHand) puts the shield in the left hand", "left " + Describe(inv.LeftHandSlot));

        global::Inventory inv2 = NewInventory();
        ItemData shield2 = Clone(ItemIDs.SHIELD_HEAVY_STEEL);
        Assert(!inv2.DirectEquip(shield2, EquipSlot.MainHand) && inv2.RightHandSlot == null,
            "DirectEquip refuses a shield in MainHand and reports it");

        // The Ranged alias is a loadout decision: DirectEquip refuses it rather than overwrite the held weapon.
        global::Inventory inv3 = NewInventory();
        ItemData heldSword = Clone(ItemIDs.LONGSWORD);
        ItemData bow = Clone(ItemIDs.LONGBOW);
        inv3.DirectEquip(heldSword, EquipSlot.RightHand);
        Assert(!inv3.DirectEquip(bow, EquipSlot.Ranged) && inv3.RightHandSlot == heldSword,
            "DirectEquip(Ranged) is refused and the held longsword is kept", "right " + Describe(inv3.RightHandSlot));
    }

    private static int PackIndexOf(global::Inventory inv, ItemData item)
    {
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
            if (ReferenceEquals(inv.GeneralSlots[i], item))
                return i;
        return -1;
    }

    /// <summary>EquipFromInventory, GetEquipped and Unequip resolve the hand aliases too, so no item is lost.</summary>
    private static void TestInventoryAliasPaths()
    {
        global::Inventory inv = NewInventory();
        ItemData sword = Clone(ItemIDs.LONGSWORD);
        ItemData shield = Clone(ItemIDs.SHIELD_HEAVY_STEEL);
        if (sword == null || shield == null)
            return;
        inv.AddItem(sword);
        inv.AddItem(shield);
        Assert(inv.EquipFromInventory(PackIndexOf(inv, sword), EquipSlot.MainHand) && inv.RightHandSlot == sword && PackIndexOf(inv, sword) < 0,
            "EquipFromInventory(MainHand) moves the longsword from the pack to the right hand", "right " + Describe(inv.RightHandSlot));
        Assert(inv.EquipFromInventory(PackIndexOf(inv, shield), EquipSlot.OffHand) && inv.LeftHandSlot == shield,
            "EquipFromInventory(OffHand) moves the shield to the left hand", "left " + Describe(inv.LeftHandSlot));
        Assert(inv.GetEquipped(EquipSlot.MainHand) == sword && inv.GetEquipped(EquipSlot.OffHand) == shield,
            "GetEquipped reads the hand an alias stands for");
        Assert(inv.Unequip(EquipSlot.MainHand) && inv.RightHandSlot == null && PackIndexOf(inv, sword) >= 0,
            "Unequip(MainHand) returns the longsword to the pack");
    }

    /// <summary>The spawn's wield test is the AI's routing test (AIService.RoutesToRangedTurn).</summary>
    private static void TestRangedRoutingPredicate()
    {
        Assert(AIService.RoutesToRangedTurn(NPCAIBehavior.RangedKiter, null), "no profile: RangedKiter runs the ranged routine");
        Assert(!AIService.RoutesToRangedTurn(NPCAIBehavior.AggressiveMelee, null), "no profile: AggressiveMelee does not");
        Assert(AIService.RoutesToRangedTurn(NPCAIBehavior.Ranged, null), "no profile: the Ranged behaviour runs the ranged routine (AI-003)");
        Assert(!AIService.RoutesToRangedTurn(NPCAIBehavior.DefensiveMelee, null), "no profile: DefensiveMelee does not");

        var ranged = DND35.AI.AIProfile.CreateRuntime<DND35.AI.Profiles.RangedAIProfile>();
        var humanoid = DND35.AI.AIProfile.CreateRuntime<DND35.AI.Profiles.HumanoidAIProfile>();
        try
        {
            Assert(AIService.RoutesToRangedTurn(NPCAIBehavior.AggressiveMelee, ranged), "a Ranged profile runs the ranged routine with a melee behaviour");
            Assert(!AIService.RoutesToRangedTurn(NPCAIBehavior.DefensiveMelee, ranged), "DefensiveMelee keeps a Ranged profile in melee");
            Assert(AIService.RoutesToRangedTurn(NPCAIBehavior.RangedKiter, humanoid), "RangedKiter runs the ranged routine with a Humanoid profile");
            Assert(AIService.RoutesToRangedTurn(NPCAIBehavior.Ranged, humanoid), "Ranged runs the ranged routine with a Humanoid profile (AI-003)");
            Assert(!AIService.RoutesToRangedTurn(NPCAIBehavior.AggressiveMelee, humanoid), "AggressiveMelee with a Humanoid profile does not");
        }
        finally
        {
            Object.DestroyImmediate(ranged);
            Object.DestroyImmediate(humanoid);
        }
    }

    private static List<KeyValuePair<ItemData, EquipSlot>> Gear(params (string id, EquipSlot slot)[] entries)
    {
        var list = new List<KeyValuePair<ItemData, EquipSlot>>();
        foreach (var e in entries)
            list.Add(new KeyValuePair<ItemData, EquipSlot>(Clone(e.id), e.slot));
        return list;
    }

    private static void TestStartingLoadoutPolicy()
    {
        // A creature that fights at range holds its bow; the melee weapon goes to the pack.
        global::Inventory archer = NewInventory();
        archer.EquipStartingLoadout(Gear((ItemIDs.LONGSWORD, EquipSlot.MainHand), (ItemIDs.LONGBOW, EquipSlot.Ranged)), true);
        Assert(Holds(archer.RightHandSlot, "longbow") && PackHas(archer, "longsword"),
            "ranged AI: longbow in hand, longsword in the pack", "right " + Describe(archer.RightHandSlot));

        // Any other creature holds its melee weapon and carries the bow.
        global::Inventory fighter = NewInventory();
        fighter.EquipStartingLoadout(Gear((ItemIDs.LONGSWORD, EquipSlot.MainHand), (ItemIDs.LONGBOW, EquipSlot.Ranged)), false);
        Assert(Holds(fighter.RightHandSlot, "longsword") && PackHas(fighter, "longbow"),
            "melee AI: longsword in hand, longbow in the pack", "right " + Describe(fighter.RightHandSlot));

        // A two-handed bow held by a ranged creature moves its shield to the pack.
        global::Inventory shieldArcher = NewInventory();
        shieldArcher.EquipStartingLoadout(Gear((ItemIDs.LONGSWORD, EquipSlot.MainHand), (ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.OffHand),
            (ItemIDs.LONGBOW, EquipSlot.Ranged)), true);
        Assert(Holds(shieldArcher.RightHandSlot, "longbow") && shieldArcher.LeftHandSlot == null && PackHas(shieldArcher, "shield_heavy_steel"),
            "ranged AI with a shield: the two-handed longbow in hand, the shield in the pack", "left " + Describe(shieldArcher.LeftHandSlot));

        // Only the first Ranged weapon is held; an item that cannot be equipped is kept in the pack, not lost.
        global::Inventory odd = NewInventory();
        odd.EquipStartingLoadout(Gear((ItemIDs.SLING, EquipSlot.Ranged), (ItemIDs.LONGBOW, EquipSlot.Ranged), (ItemIDs.SHIELD_HEAVY_STEEL, EquipSlot.MainHand)), true);
        Assert(Holds(odd.RightHandSlot, "sling") && PackHas(odd, "longbow") && PackHas(odd, "shield_heavy_steel"),
            "the first Ranged weapon is held, a second one and an unequippable item go to the pack", "right " + Describe(odd.RightHandSlot));
    }

    // ── Alignment (CRE-002) ─────────────────────────────────────────────

    private static void TestGoodEvilAxis()
    {
        Assert(AlignmentHelper.WithGoodEvilAxis(Alignment.TrueNeutral, true) == Alignment.NeutralGood, "N made good is NG");
        Assert(AlignmentHelper.WithGoodEvilAxis(Alignment.LawfulGood, false) == Alignment.LawfulEvil, "LG made evil is LE");
        Assert(AlignmentHelper.WithGoodEvilAxis(Alignment.ChaoticEvil, true) == Alignment.ChaoticGood, "CE made good is CG");
        Assert(AlignmentHelper.WithGoodEvilAxis(Alignment.ChaoticNeutral, false) == Alignment.ChaoticEvil, "CN made evil is CE");
        Assert(AlignmentHelper.WithGoodEvilAxis(Alignment.None, false) == Alignment.NeutralEvil, "an unset alignment made evil is NE");
    }

    private static NPCDefinition WithTemplate(string npcId, string templateId)
    {
        NPCDefinition template = NPCDatabase.Get(npcId);
        if (template == null)
        {
            Assert(false, npcId + " exists in NPCDatabase");
            return null;
        }
        NPCDefinition def = template.Clone();
        def.AppliedTemplateIds = new List<string> { templateId };
        return CreatureTemplateRegistry.ApplyTemplatesClone(def);
    }

    /// <summary>MM p.31: celestial needs a good or neutral base; p.108: fiendish a nongood one. The templated creature keeps
    /// the base's law-chaos component (owner interpretation 2026-10-11, CRE-057); an illegal base is refused.</summary>
    private static void TestOutsiderTemplates()
    {
        NPCDefinition celestialPaladin = WithTemplate("human_paladin_3", "celestial");
        Assert(celestialPaladin != null && celestialPaladin.CharacterAlignment == Alignment.LawfulGood,
            "celestial template keeps an LG base LG (MM p.31)", celestialPaladin != null ? celestialPaladin.CharacterAlignment.ToString() : "");
        NPCDefinition celestialMonk = WithTemplate("human_monk_3", "celestial");
        Assert(celestialMonk != null && celestialMonk.CharacterAlignment == Alignment.LawfulGood,
            "celestial template makes an LN base LG (MM p.31; law-chaos kept, owner interpretation 2026-10-11, CRE-057)", celestialMonk != null ? celestialMonk.CharacterAlignment.ToString() : "");
        NPCDefinition fiendishOgre = WithTemplate("ogre", "fiendish");
        Assert(fiendishOgre != null && fiendishOgre.CharacterAlignment == Alignment.ChaoticEvil,
            "fiendish template keeps a CE base CE (MM p.108)", fiendishOgre != null ? fiendishOgre.CharacterAlignment.ToString() : "");

        // Illegal bases are refused (owner ruling 2026-10-11, CRE-057): no template id, alignment, smite or mitigation.
        NPCDefinition celestialOgre = WithTemplate("ogre", "celestial");
        Assert(celestialOgre != null && celestialOgre.CharacterAlignment == Alignment.ChaoticEvil && !celestialOgre.GainsSmiteEvil
            && (celestialOgre.AppliedTemplateIds == null || !celestialOgre.AppliedTemplateIds.Contains("celestial"))
            && celestialOgre.DamageReductionAmount == NPCDatabase.Get("ogre").DamageReductionAmount,
            "a celestial template on the CE ogre is refused: it stays a CE ogre without smite evil or DR (MM p.31)",
            celestialOgre != null ? celestialOgre.CharacterAlignment + " smite " + celestialOgre.GainsSmiteEvil + " DR " + celestialOgre.DamageReductionAmount : "");
        NPCDefinition fiendishPaladin = WithTemplate("human_paladin_3", "fiendish");
        Assert(fiendishPaladin != null && fiendishPaladin.CharacterAlignment == Alignment.LawfulGood && !fiendishPaladin.GainsSmiteGood
            && (fiendishPaladin.AppliedTemplateIds == null || !fiendishPaladin.AppliedTemplateIds.Contains("fiendish")),
            "a fiendish template on the LG paladin is refused: it stays LG without smite good (MM p.108)",
            fiendishPaladin != null ? fiendishPaladin.CharacterAlignment + " smite " + fiendishPaladin.GainsSmiteGood : "");
        Assert(new CelestialTemplate().CanApplyTo(NPCDatabase.Get("human_monk_3")) && !new CelestialTemplate().CanApplyTo(NPCDatabase.Get("ogre"))
            && new FiendishTemplate().CanApplyTo(NPCDatabase.Get("ogre")) && !new FiendishTemplate().CanApplyTo(NPCDatabase.Get("human_paladin_3")),
            "CanApplyTo: celestial on LN yes, on CE no; fiendish on CE yes, on LG no");

        // Every templated Summon Monster and Summon Nature's Ally option has a legal base (PHB p.285-287).
        var wizard = new CharacterStats("Summon Tester", 17, "Wizard", 10, 10, 10, 10, 18, 10, 8, 0, 0, 4, 1, 0, 6, 1, 6);
        var illegal = new List<string>();
        int checkedOptions = 0;
        for (int level = 1; level <= 9; level++)
        {
            foreach (bool sna in new[] { false, true })
            {
                foreach (SummonMonsterOption option in SummonMonsterLists.GetFilteredOptionsForListLevel(level, wizard, sna))
                {
                    if (option == null || string.IsNullOrWhiteSpace(option.TemplateId))
                        continue;
                    ICreatureTemplate template = CreatureTemplateRegistry.ResolveTemplates(new NPCDefinition { AppliedTemplateIds = new List<string> { option.TemplateId } })
                        .Find(t => t is OutsiderTemplateBase);
                    NPCDefinition baseDef = NPCDatabase.Get(option.NpcDefinitionId);
                    if (!(template is OutsiderTemplateBase outsider) || baseDef == null)
                        continue;
                    checkedOptions++;
                    if (!outsider.CanApplyTo(baseDef))
                        illegal.Add(option.TemplateId + " " + option.NpcDefinitionId + " (" + baseDef.CharacterAlignment + ")");
                }
            }
        }
        Assert(checkedOptions > 0 && illegal.Count == 0,
            $"every templated summon option has a legal base ({checkedOptions} checked)", string.Join(", ", illegal));
    }

    private static void TestUndeadTemplates()
    {
        NPCDefinition paladin = NPCDatabase.Get("human_paladin_3");
        if (paladin == null)
        {
            Assert(false, "human_paladin_3 exists in NPCDatabase");
            return;
        }
        NPCDefinition skeleton = SkeletonTemplate.Apply(paladin, "skeleton_test_paladin", "Paladin Skeleton", hasHands: true);
        Assert(skeleton != null && skeleton.CharacterAlignment == Alignment.NeutralEvil,
            "skeleton template: always neutral evil (MM p.226)", skeleton != null ? skeleton.CharacterAlignment.ToString() : "");
        NPCDefinition zombie = ZombieTemplate.Apply(paladin, "zombie_test_paladin", "Paladin Zombie");
        Assert(zombie != null && zombie.CharacterAlignment == Alignment.NeutralEvil,
            "zombie template: always neutral evil (MM p.266)", zombie != null ? zombie.CharacterAlignment.ToString() : "");
        NPCDefinition registrySkeleton = WithTemplate("human_paladin_3", "skeleton");
        Assert(registrySkeleton != null && registrySkeleton.CharacterAlignment == Alignment.NeutralEvil,
            "skeleton template through the spawn-time registry: neutral evil", registrySkeleton != null ? registrySkeleton.CharacterAlignment.ToString() : "");
        NPCDefinition registryZombie = WithTemplate("human_paladin_3", "zombie");
        Assert(registryZombie != null && registryZombie.CharacterAlignment == Alignment.NeutralEvil,
            "zombie template through the spawn-time registry: neutral evil", registryZombie != null ? registryZombie.CharacterAlignment.ToString() : "");
        NPCDefinition skeletonWolf = NPCDatabase.Get("skeleton_wolf");
        Assert(skeletonWolf != null && skeletonWolf.CharacterAlignment == Alignment.NeutralEvil,
            "the database's wolf skeleton is neutral evil", skeletonWolf != null ? skeletonWolf.CharacterAlignment.ToString() : "missing");
    }

    // ── Monster Manual alignment data (CRE-056) ─────────────────────────

    /// <summary>
    /// Every registered definition sets an alignment: every Monster Manual entry has an alignment line (MM p.7), and an unset
    /// alignment made smite, Protection from Evil, aligned weapons and the alignment spells ignore the creature
    /// (CRE-056: 211 of 389 definitions had none on 2026-10-14). The check covers entries without a challenge rating too
    /// (summon bases, presets and test fixtures), since they spawn as well.
    /// </summary>
    private static void TestEveryDefinitionHasAlignment()
    {
        var missing = new List<string>();
        int total = 0, withCr = 0;
        foreach (NPCDefinition def in NPCDatabase.AllNPCs)
        {
            if (def == null)
                continue;
            total++;
            if (!string.IsNullOrEmpty(def.ChallengeRating))
                withCr++;
            if (def.CharacterAlignment == Alignment.None)
                missing.Add(def.Id);
        }
        missing.Sort(System.StringComparer.Ordinal);
        Assert(total > 0 && missing.Count == 0,
            $"every registered NPC definition sets an alignment ({total} checked, {withCr} with a CR; MM p.7)",
            missing.Count + " unaligned: " + string.Join(", ", missing.Count > 40 ? missing.GetRange(0, 40) : missing));
    }

    private static void CheckDefinitionAlignment(string npcId, Alignment expected, string cite)
    {
        NPCDefinition def = NPCDatabase.Get(npcId);
        Assert(def != null && def.CharacterAlignment == expected, npcId + " is " + expected + " (" + cite + ")",
            def != null ? "got " + def.CharacterAlignment : "missing");
    }

    /// <summary>Entries that had no alignment before CRE-056, checked against their Monster Manual alignment lines.</summary>
    private static void TestMonsterManualAlignments()
    {
        // Dragons: the colour's alignment at every age (DragonData.GetAlignment).
        CheckDefinitionAlignment("dragon_black_wyrmling", Alignment.ChaoticEvil, "MM p.70");
        CheckDefinitionAlignment("dragon_blue_adult", Alignment.LawfulEvil, "MM p.72");
        CheckDefinitionAlignment("dragon_green_juvenile", Alignment.LawfulEvil, "MM p.74");
        CheckDefinitionAlignment("dragon_red_young", Alignment.ChaoticEvil, "MM p.75");
        CheckDefinitionAlignment("dragon_white_very_young", Alignment.ChaoticEvil, "MM p.77");
        CheckDefinitionAlignment("dragon_brass_young_adult", Alignment.ChaoticGood, "MM p.79");
        CheckDefinitionAlignment("dragon_bronze_wyrmling", Alignment.LawfulGood, "MM p.80");
        CheckDefinitionAlignment("dragon_copper_adult", Alignment.ChaoticGood, "MM p.82");
        CheckDefinitionAlignment("dragon_gold_young", Alignment.LawfulGood, "MM p.84");
        CheckDefinitionAlignment("dragon_silver_juvenile", Alignment.LawfulGood, "MM p.86");
        // Humanoids, monstrous humanoids and giants.
        CheckDefinitionAlignment("goblin", Alignment.NeutralEvil, "MM p.133");
        CheckDefinitionAlignment("gnoll", Alignment.ChaoticEvil, "MM p.130");
        CheckDefinitionAlignment("bugbear", Alignment.ChaoticEvil, "MM p.29");
        CheckDefinitionAlignment("troglodyte", Alignment.ChaoticEvil, "MM p.246");
        CheckDefinitionAlignment("gargoyle", Alignment.ChaoticEvil, "MM p.113");
        CheckDefinitionAlignment("troll", Alignment.ChaoticEvil, "MM p.247");
        // Outsiders.
        CheckDefinitionAlignment("lemure", Alignment.LawfulEvil, "MM p.57");
        CheckDefinitionAlignment("hell_hound", Alignment.LawfulEvil, "MM p.152");
        CheckDefinitionAlignment("howler", Alignment.ChaoticEvil, "MM p.155");
        CheckDefinitionAlignment("yeth_hound", Alignment.NeutralEvil, "MM p.261");
        CheckDefinitionAlignment("lantern_archon", Alignment.LawfulGood, "MM p.16");
        CheckDefinitionAlignment("noble_djinni", Alignment.ChaoticGood, "MM p.114");
        CheckDefinitionAlignment("elder_xorn", Alignment.TrueNeutral, "MM p.261");
        CheckDefinitionAlignment("air_mephit", Alignment.TrueNeutral, "MM p.181");
        CheckDefinitionAlignment("steam_mephit", Alignment.TrueNeutral, "MM p.185");
        // Undead, aberrations, magical beasts, oozes, elementals.
        CheckDefinitionAlignment("allip", Alignment.NeutralEvil, "MM p.10");
        CheckDefinitionAlignment("shadow", Alignment.ChaoticEvil, "MM p.221");
        CheckDefinitionAlignment("wraith", Alignment.LawfulEvil, "MM p.258");
        CheckDefinitionAlignment("cloaker", Alignment.ChaoticNeutral, "MM p.36");
        CheckDefinitionAlignment("giant_eagle", Alignment.NeutralGood, "MM p.93");
        CheckDefinitionAlignment("giant_owl", Alignment.NeutralGood, "MM p.205");
        CheckDefinitionAlignment("ankheg", Alignment.TrueNeutral, "MM p.14");
        CheckDefinitionAlignment("gelatinous_cube", Alignment.TrueNeutral, "MM p.201");
        CheckDefinitionAlignment("medium_earth_elemental", Alignment.TrueNeutral, "MM p.95-100");
        // Animals and vermin (always neutral, MM p.268, p.284), the variant helpers included.
        CheckDefinitionAlignment("wolf_pack_hunter", Alignment.TrueNeutral, "MM p.283");
        CheckDefinitionAlignment("wolf", Alignment.TrueNeutral, "summon alias of the wolf");
        CheckDefinitionAlignment("dire_rat", Alignment.TrueNeutral, "MM p.64");
        CheckDefinitionAlignment("monstrous_centipede_colossal", Alignment.TrueNeutral, "MM p.284");
        CheckDefinitionAlignment("monstrous_scorpion_huge", Alignment.TrueNeutral, "MM p.284");
        CheckDefinitionAlignment("monstrous_spider_tiny", Alignment.TrueNeutral, "MM p.284");
        CheckDefinitionAlignment("viper_tiny", Alignment.TrueNeutral, "MM p.268");
        // Custom entries built on MM creatures, and a class-bound fixture.
        CheckDefinitionAlignment("hobgoblin_sergeant", Alignment.LawfulEvil, "MM p.153");
        CheckDefinitionAlignment("orc_berserker", Alignment.ChaoticEvil, "MM p.203");
        CheckDefinitionAlignment("skeleton_warrior", Alignment.NeutralEvil, "MM p.226");
        CheckDefinitionAlignment("wight_dreadwalker", Alignment.LawfulEvil, "MM p.255");
        CheckDefinitionAlignment("human_paladin", Alignment.LawfulGood, "PHB p.43");
        // Open MM alignment lines (delegated RAW readings 2026-10-14, CRE-056): an "Any" or "(any)" line keeps the base
        // creature's alignment or axis; the ghost and vampire samples are human fighters (typically N, PHB p.13, p.104).
        CheckDefinitionAlignment("ghost", Alignment.TrueNeutral, "MM p.117-118 Any: the base human fighter's N");
        CheckDefinitionAlignment("vampire", Alignment.NeutralEvil, "MM p.252 Always evil (any): evil, the base's neutral law-chaos axis");
        CheckDefinitionAlignment("vampire_spawn", Alignment.ChaoticEvil, "MM p.253 Always evil (any), no base: PHB p.104 Table 6-1 Vampires CE");
        CheckDefinitionAlignment("lich", Alignment.NeutralEvil, "MM p.167 sample line");
        CheckDefinitionAlignment("average_salamander", Alignment.NeutralEvil, "MM p.219 Usually evil (any), no base, no lawful or chaotic subtype");
        // A class NPC takes its race's listed alignment (PHB p.104 Table 6-1 for PHB races); a human takes its class's
        // typical one, N where the table lists none.
        CheckDefinitionAlignment("human_monk_5", Alignment.LawfulNeutral, "PHB p.104 Table 6-1 Monks LN");
        CheckDefinitionAlignment("elf_warrior", Alignment.ChaoticGood, "PHB p.104 Table 6-1 Elves CG");
        CheckDefinitionAlignment("dwarf_warrior", Alignment.LawfulGood, "PHB p.104 Table 6-1 Dwarves LG");
        CheckDefinitionAlignment("halfling_warrior", Alignment.TrueNeutral, "PHB p.104 Table 6-1 Halflings N");
    }

    private static void CheckSpawnAlignment(string npcId, Alignment expected, string cite)
    {
        CharacterController cc = Spawn(npcId);
        try
        {
            Alignment got = cc != null && cc.Stats != null ? cc.Stats.CharacterAlignment : Alignment.None;
            Assert(got == expected, npcId + " spawns " + expected + " (" + cite + ")", "got " + got);
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestSpawnedAlignments()
    {
        CheckSpawnAlignment("ogre", Alignment.ChaoticEvil, "MM p.199");
        CheckSpawnAlignment("elf_warrior", Alignment.ChaoticGood, "MM p.101");
        CheckSpawnAlignment("human_paladin_3", Alignment.LawfulGood, "PHB p.43");
        CheckSpawnAlignment("human_monk_3", Alignment.LawfulNeutral, "PHB p.40");
        CheckSpawnAlignment("goblin", Alignment.NeutralEvil, "MM p.133; unaligned before CRE-056");
        CheckSpawnAlignment("dragon_blue_young", Alignment.LawfulEvil, "MM p.72; unaligned before CRE-056");
        CheckSpawnAlignment("wolf", Alignment.TrueNeutral, "MM p.283; unaligned before CRE-056");

        // A reused slot takes the new creature's alignment (CRE-046).
        CharacterController cc = Spawn("ogre");
        try
        {
            if (cc != null)
                GameManager.Instance.InitializeNPCFromDefinition(cc, NPCDatabase.Get("elf_warrior").Clone(), SpawnPos, null, null);
            Assert(cc != null && cc.Stats != null && cc.Stats.CharacterAlignment == Alignment.ChaoticGood,
                "a slot reused from an ogre for an elf warrior is CG", cc != null && cc.Stats != null ? cc.Stats.CharacterAlignment.ToString() : "");
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestSpawnedTemplateAlignments()
    {
        CheckSpawnAlignment("fiendish_wolf", Alignment.NeutralEvil, "wolf always neutral, MM p.283; fiendish, MM p.108");
        CheckSpawnAlignment("fiendish_dire_bear", Alignment.NeutralEvil, "dire bear always neutral, MM p.63; fiendish, MM p.108");
        CheckSpawnAlignment("skeleton_wolf", Alignment.NeutralEvil, "MM p.226");

        NPCDefinition celestialMonk = WithTemplate("human_monk_3", "celestial");
        CharacterController cc = celestialMonk != null ? Spawn(celestialMonk, "celestial_monk") : null;
        try
        {
            Assert(cc != null && cc.Stats != null && cc.Stats.CharacterAlignment == Alignment.LawfulGood && cc.Stats.HasTemplateSmiteEvil,
                "a spawned celestial monk is LG (law-chaos kept, owner interpretation 2026-10-11, CRE-057) with smite evil",
                cc != null && cc.Stats != null ? cc.Stats.CharacterAlignment.ToString() : "");
        }
        finally
        {
            Cleanup(cc);
        }
    }

    private static void TestSpawnedHandAliasGear()
    {
        CharacterController ogre = Spawn("ogre");
        try
        {
            global::Inventory inv = ogre != null ? ogre.InventoryComp.CharacterInventory : null;
            Assert(inv != null && Holds(inv.RightHandSlot, "greatclub"),
                "the MM ogre spawns holding its greatclub (MainHand entry; MM p.199)", "right " + Describe(inv?.RightHandSlot));
        }
        finally
        {
            Cleanup(ogre);
        }

        CharacterController paladin = Spawn("human_paladin_3");
        try
        {
            global::Inventory inv = paladin != null ? paladin.InventoryComp.CharacterInventory : null;
            Assert(inv != null && Holds(inv.RightHandSlot, "longsword"),
                "human_paladin_3 holds its longsword (MainHand entry)", "right " + Describe(inv?.RightHandSlot));
            Assert(inv != null && inv.LeftHandSlot != null && inv.LeftHandSlot.IsShield,
                "human_paladin_3 carries its heavy steel shield in the left hand (OffHand entry)", "left " + Describe(inv?.LeftHandSlot));
        }
        finally
        {
            Cleanup(paladin);
        }

        CharacterController ettin = Spawn("ettin");
        try
        {
            global::Inventory inv = ettin != null ? ettin.InventoryComp.CharacterInventory : null;
            Assert(inv != null && Holds(inv.RightHandSlot, "morningstar") && Holds(inv.LeftHandSlot, "morningstar"),
                "the ettin holds a morningstar in each hand (MainHand and OffHand entries; MM p.106)",
                "right " + Describe(inv?.RightHandSlot) + ", left " + Describe(inv?.LeftHandSlot));
        }
        finally
        {
            Cleanup(ettin);
        }
    }

    private static void TestSpawnedRangedGear()
    {
        CharacterController elf = Spawn("elf_warrior");
        try
        {
            global::Inventory inv = elf != null ? elf.InventoryComp.CharacterInventory : null;
            Assert(inv != null && Holds(inv.RightHandSlot, "longbow") && PackHas(inv, "longsword"),
                "the elf warrior (RangedKiter) holds its longbow and carries its longsword", "right " + Describe(inv?.RightHandSlot));
            Assert(elf != null && elf.IsEquippedWeaponRanged(), "the elf warrior's equipped weapon is ranged");
        }
        finally
        {
            Cleanup(elf);
        }

        CharacterController janni = Spawn("janni");
        try
        {
            global::Inventory inv = janni != null ? janni.InventoryComp.CharacterInventory : null;
            Assert(inv != null && Holds(inv.RightHandSlot, "scimitar") && PackHas(inv, "longbow"),
                "the janni (melee AI) holds its scimitar and carries its longbow", "right " + Describe(inv?.RightHandSlot));
        }
        finally
        {
            Cleanup(janni);
        }

        CharacterController monk = Spawn("human_monk_3");
        try
        {
            global::Inventory inv = monk != null ? monk.InventoryComp.CharacterInventory : null;
            Assert(inv != null && inv.RightHandSlot == null && inv.LeftHandSlot == null && PackHas(inv, "sling"),
                "human_monk_3 keeps its hands free for unarmed strikes and carries its sling", "right " + Describe(inv?.RightHandSlot));
        }
        finally
        {
            Cleanup(monk);
        }
    }

    /// <summary>
    /// The held weapon follows the AI's routing (AIService.RoutesToRangedTurn), not the behaviour alone: a melee
    /// behaviour with a Ranged profile runs the ranged routine and holds its bow; DefensiveMelee keeps it in melee.
    /// </summary>
    private static void TestSpawnedRangedProfileGear()
    {
        NPCDefinition janni = NPCDatabase.Get("janni");
        if (janni == null)
        {
            Assert(false, "janni exists in NPCDatabase");
            return;
        }

        NPCDefinition rangedProfile = janni.Clone();
        rangedProfile.AIBehavior = NPCAIBehavior.AggressiveMelee;
        rangedProfile.AIProfileArchetype = NPCAIProfileArchetype.Ranged;
        CharacterController archer = Spawn(rangedProfile, "janni_ranged_profile");
        try
        {
            global::Inventory inv = archer != null ? archer.InventoryComp.CharacterInventory : null;
            Assert(inv != null && Holds(inv.RightHandSlot, "longbow") && PackHas(inv, "scimitar"),
                "a melee-behaviour janni with a Ranged profile holds its longbow and carries its scimitar", "right " + Describe(inv?.RightHandSlot));
        }
        finally
        {
            Cleanup(archer);
        }

        NPCDefinition defensive = janni.Clone();
        defensive.AIBehavior = NPCAIBehavior.DefensiveMelee;
        defensive.AIProfileArchetype = NPCAIProfileArchetype.Ranged;
        CharacterController guard = Spawn(defensive, "janni_defensive");
        try
        {
            global::Inventory inv = guard != null ? guard.InventoryComp.CharacterInventory : null;
            Assert(inv != null && Holds(inv.RightHandSlot, "scimitar") && PackHas(inv, "longbow"),
                "a DefensiveMelee janni with a Ranged profile holds its scimitar", "right " + Describe(inv?.RightHandSlot));
        }
        finally
        {
            Cleanup(guard);
        }

        // AI-003 (batch 13 step 8): the Ranged behaviour without a profile (archetype None) runs the ranged routine, so the
        // spawn puts the bow in its hand too.
        NPCDefinition rangedBehaviour = janni.Clone();
        rangedBehaviour.AIBehavior = NPCAIBehavior.Ranged;
        rangedBehaviour.AIProfileArchetype = NPCAIProfileArchetype.None;
        CharacterController shooter = Spawn(rangedBehaviour, "janni_ranged_behaviour");
        try
        {
            global::Inventory inv = shooter != null ? shooter.InventoryComp.CharacterInventory : null;
            Assert(shooter != null && shooter.aiProfile == null, "a Ranged-behaviour janni of archetype None spawns without a profile",
                shooter != null && shooter.aiProfile != null ? shooter.aiProfile.GetType().Name : "");
            Assert(inv != null && Holds(inv.RightHandSlot, "longbow") && PackHas(inv, "scimitar"),
                "AI-003: a Ranged-behaviour janni without a profile holds its longbow and carries its scimitar", "right " + Describe(inv?.RightHandSlot));
        }
        finally
        {
            Cleanup(shooter);
        }
    }

    private struct DefinitionSnapshot
    {
        public string Id;
        public Alignment Alignment;
        public string TemplateIds;
    }

    private static string JoinTemplateIds(NPCDefinition def)
        => def == null || def.AppliedTemplateIds == null ? "" : string.Join(",", def.AppliedTemplateIds);

    /// <summary>The alignment and applied templates of the database entries the suite templates and spawns.</summary>
    private static List<DefinitionSnapshot> SnapshotDatabaseEntries(params string[] ids)
    {
        var list = new List<DefinitionSnapshot>();
        foreach (string id in ids)
        {
            NPCDefinition def = NPCDatabase.Get(id);
            if (def == null)
            {
                Assert(false, id + " exists in NPCDatabase");
                continue;
            }
            list.Add(new DefinitionSnapshot { Id = id, Alignment = def.CharacterAlignment, TemplateIds = JoinTemplateIds(def) });
        }
        return list;
    }

    /// <summary>Never mutate database templates: the entries the suite templated and spawned are unchanged.</summary>
    private static void TestDatabaseTemplatesUnchanged(List<DefinitionSnapshot> snapshots)
    {
        foreach (DefinitionSnapshot snap in snapshots)
        {
            NPCDefinition def = NPCDatabase.Get(snap.Id);
            bool same = def != null && def.CharacterAlignment == snap.Alignment && JoinTemplateIds(def) == snap.TemplateIds;
            Assert(same, "the database " + snap.Id + " keeps its alignment and templates after the tests",
                def != null ? def.CharacterAlignment + " [" + JoinTemplateIds(def) + "] vs " + snap.Alignment + " [" + snap.TemplateIds + "]" : "missing");
        }
    }
}

}

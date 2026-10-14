using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

namespace Tests.Crafting
{
/// <summary>
/// ITM-002: a brewed potion is built exactly as a store potion is (PotionFactory.BuildPotion) and can be drunk.
/// Rules: PHB p.89 (Brew Potion: a spell of 3rd level or lower that targets one or more creatures; base price spell
/// level x caster level x 50 gp); DMG p.229 (the drinker is the spell's caster and target; the potion works at its own
/// caster level); DMG p.286 (personal-range spells and Shield Other never exist as potions). The store and the brew list
/// offer only potions the drink path resolves (PotionFactory.IsOfferedAsPotion; the rest is ITM-080), and every offered
/// potion is checked to resolve. The item tests need no scene; the drinking
/// tests go through GameManager.TryUseConsumableFromInventory (the actor-agnostic item path the inventory UI calls) out
/// of combat on a spawned creature, so they need Play mode with a scene GameManager. Spawned controllers sit off the
/// grid and are destroyed afterwards. Database templates are never changed (brewing clones nothing from the databases).
/// </summary>
public static class BrewedPotionRulesTests
{
    private static int _passed;
    private static int _failed;

    private static readonly Vector2Int SpawnPos = new Vector2Int(-64, -64);

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        SpellDatabase.Init();
        ItemDatabase.Init();
        NPCDatabase.Init();

        Debug.Log("====== BREWED POTION RULES TESTS (ITM-002) ======");

        TestBrewedHealingPotion();
        TestBrewedPotionMatchesStorePotion();
        TestBrewedCureMinorWounds();
        TestBrewedBuffPotion();
        TestBrewedPotionsStack();
        TestBrewedPotionIds();
        TestNonHitPointHealingPotions();
        TestBrewListEligibility();
        TestEveryOfferedPotionResolves();

        if (GameManager.Instance == null)
        {
            Assert(false, "Drinking brewed potions needs Play mode with a scene GameManager");
        }
        else
        {
            TestDrinkBrewedHealingPotion();
            TestDrinkBrewedBuffPotion();
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

    private static CharacterStats Crafter(string className, int classLevel)
    {
        return new CharacterStats
        {
            CharacterName = "Test " + className,
            CharacterClass = className,
            ClassLevels = new List<ClassLevelEntry> { new ClassLevelEntry(className, classLevel) },
        };
    }

    /// <summary>Brews a potion of <paramref name="spellId"/> through CraftingExecutor.Execute: by default as a paid
    /// project whose costs are set to zero (so no gold, XP or days are needed), or in debug mode.</summary>
    private static ItemData Brew(string spellId, int casterLevel, bool debug = false)
    {
        bool oldDebug = CraftingValidator.DebugMode;
        CraftingValidator.DebugMode = debug;
        try
        {
            var def = new CraftableItemDefinition
            {
                ItemId = "potion_" + spellId,
                DisplayName = "Test potion",
                RequiredFeat = CraftingFeatType.BrewPotion,
                IsDynamic = true,
                DynamicSpellId = spellId,
            };
            var project = new CraftingProject { Definition = def, IsValid = true, Crafter = Crafter("Cleric", 5), ItemCasterLevel = casterLevel };
            CraftingExecutor.CraftingResult result = CraftingExecutor.Execute(project, null);
            return result.Success ? result.CraftedItem : null;
        }
        finally
        {
            CraftingValidator.DebugMode = oldDebug;
        }
    }

    private static string Dice(ItemData item) =>
        item == null ? "no item" : $"{item.ConsumableEffect} {item.HealDiceCount}d{item.HealDiceSides}+{item.HealBonus} flat {item.HealAmount}";

    private static CharacterController Spawn(string npcId)
    {
        NPCDefinition template = NPCDatabase.Get(npcId);
        if (template == null)
        {
            Assert(false, npcId + " exists in NPCDatabase");
            return null;
        }
        var go = new GameObject("BrewedPotionTest_" + npcId);
        CharacterController cc = go.AddComponent<CharacterController>();
        GameManager.Instance.InitializeNPCFromDefinition(cc, template.Clone(), SpawnPos, null, null);
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

    private static int IndexOf(global::Inventory inv, string itemId)
    {
        if (inv == null)
            return -1;
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
            if (inv.GeneralSlots[i] != null && inv.GeneralSlots[i].Id == itemId)
                return i;
        return -1;
    }

    // ── Brewed items ────────────────────────────────────────────────────

    private static void TestBrewedHealingPotion()
    {
        ItemData clw = Brew(SpellNames.CURE_LIGHT_WOUNDS, 3);
        Assert(clw != null, "Brewing a potion of Cure Light Wounds at CL 3 creates an item");
        if (clw == null)
            return;
        Assert(clw.IsPotion && clw.IsConsumable, "The brewed potion is a consumable potion");
        Assert(clw.ConsumableEffect == ConsumableEffectType.HealHP, "The brewed Cure Light Wounds potion heals (not ConsumableEffect None)", Dice(clw));
        Assert(clw.HealDiceCount == 1 && clw.HealDiceSides == 8 && clw.HealBonus == 3,
            "A CL 3 brewed potion of Cure Light Wounds heals 1d8+3 (DMG p.229, PHB p.215)", Dice(clw));
        Assert(clw.ConsumableMinimumCasterLevel == 3, "The brewed potion keeps the caster level set when brewing (PHB p.89-90)", $"CL {clw.ConsumableMinimumCasterLevel}");
        Assert(clw.BasePriceGp == 150, "Brewed Cure Light Wounds at CL 3 is priced 1 x 3 x 50 = 150 gp", $"{clw.BasePriceGp} gp");
        Assert(clw.PotionSpellLevel == 1, "The brewed potion records spell level 1", $"{clw.PotionSpellLevel}");
        Assert(clw.IsStackable && clw.MaxStackSize == 20 && clw.StackCount == 1, "The brewed potion stacks like a store potion");
        Assert(clw.Id == CraftingExecutor.CraftedPotionId(SpellNames.CURE_LIGHT_WOUNDS, 3), "The brewed potion's id names its spell and caster level", clw.Id);
        Assert(clw.ConsumableSpellName == SpellNames.CURE_LIGHT_WOUNDS, "The brewed potion links the spell by its id", clw.ConsumableSpellName);
    }

    private static void TestBrewedPotionMatchesStorePotion()
    {
        ItemData store = ItemDatabase.GetItem(PotionFactory.GeneratePotionId(SpellNames.CURE_MODERATE_WOUNDS));
        ItemData brewed = Brew(SpellNames.CURE_MODERATE_WOUNDS, 3);
        Assert(store != null && brewed != null, "Store and brewed potions of Cure Moderate Wounds exist");
        if (store == null || brewed == null)
            return;
        Assert(brewed.ConsumableEffect == store.ConsumableEffect && brewed.HealDiceCount == store.HealDiceCount
               && brewed.HealDiceSides == store.HealDiceSides && brewed.HealBonus == store.HealBonus && brewed.HealAmount == store.HealAmount,
            "A CL 3 brewed potion of Cure Moderate Wounds heals as the CL 3 store potion does", $"brewed {Dice(brewed)}, store {Dice(store)}");
        Assert(brewed.ConsumableMinimumCasterLevel == store.ConsumableMinimumCasterLevel && brewed.BasePriceGp == store.BasePriceGp
               && brewed.PotionSpellLevel == store.PotionSpellLevel,
            "Brewed and store CL 3 potions of Cure Moderate Wounds share caster level, price and spell level",
            $"brewed CL {brewed.ConsumableMinimumCasterLevel} {brewed.BasePriceGp} gp L{brewed.PotionSpellLevel}, store CL {store.ConsumableMinimumCasterLevel} {store.BasePriceGp} gp L{store.PotionSpellLevel}");
        Assert(brewed.IsStackable == store.IsStackable && brewed.MaxStackSize == store.MaxStackSize && brewed.Type == store.Type
               && brewed.WeightLbs == store.WeightLbs && brewed.Name == store.Name,
            "Brewed and store potions share item kind, stacking, weight and name",
            $"brewed '{brewed.Name}' {brewed.WeightLbs} lb, store '{store.Name}' {store.WeightLbs} lb");
    }

    private static void TestBrewedCureMinorWounds()
    {
        ItemData minor = Brew(SpellNames.CURE_MINOR_WOUNDS, 1);
        Assert(minor != null && minor.ConsumableEffect == ConsumableEffectType.HealHP && minor.HealDiceCount == 0 && minor.HealAmount == 1,
            "A brewed potion of Cure Minor Wounds heals a flat 1 point (PHB p.216)", Dice(minor));
        Assert(minor != null && minor.BasePriceGp == 25, "A CL 1 0-level potion is priced 25 gp (spell level counts as 1/2)", minor == null ? "" : $"{minor.BasePriceGp} gp");
    }

    private static void TestBrewedBuffPotion()
    {
        ItemData bulls = Brew(SpellNames.BULLS_STRENGTH, 3);
        Assert(bulls != null, "Brewing a potion of Bull's Strength at CL 3 creates an item");
        if (bulls == null)
            return;
        Assert(bulls.ConsumableEffect == ConsumableEffectType.SpellEffect, "A brewed Bull's Strength potion carries its spell effect", $"{bulls.ConsumableEffect}");
        Assert(bulls.ConsumableSpellName == SpellNames.BULLS_STRENGTH && bulls.ConsumableMinimumCasterLevel == 3,
            "A brewed Bull's Strength potion links the spell at CL 3", $"{bulls.ConsumableSpellName} CL {bulls.ConsumableMinimumCasterLevel}");
        Assert(bulls.BasePriceGp == 300, "Brewed Bull's Strength at CL 3 is priced 2 x 3 x 50 = 300 gp", $"{bulls.BasePriceGp} gp");
    }

    private static void TestBrewedPotionsStack()
    {
        var inv = new global::Inventory();
        ItemData a = Brew(SpellNames.CURE_LIGHT_WOUNDS, 1);
        ItemData b = Brew(SpellNames.CURE_LIGHT_WOUNDS, 1);
        ItemData c = Brew(SpellNames.CURE_LIGHT_WOUNDS, 2);
        inv.AddItem(a);
        inv.AddItem(b);
        inv.AddItem(c);
        int slots = 0;
        ItemData cl1Stack = null;
        foreach (ItemData slot in inv.GeneralSlots)
        {
            if (slot == null)
                continue;
            slots++;
            if (slot.ConsumableMinimumCasterLevel == 1)
                cl1Stack = slot;
        }
        Assert(slots == 2, "Brewed potions of one spell stack per caster level (two CL 1 in one slot, CL 2 apart)", $"{slots} slots");
        Assert(cl1Stack != null && cl1Stack.StackCount == 2, "The CL 1 stack holds two potions", cl1Stack == null ? "none" : $"{cl1Stack.StackCount}");
    }

    private static void TestBrewedPotionIds()
    {
        // Bull's Strength: the store potion is 2nd level at CL 3, 300 gp. A brewed one at CL 3 is the same item.
        ItemData paid = Brew(SpellNames.BULLS_STRENGTH, 3);
        Assert(paid != null && paid.Id == PotionFactory.GeneratePotionId(SpellNames.BULLS_STRENGTH)
               && CraftingExecutor.MatchesStorePotion(paid, SpellNames.BULLS_STRENGTH),
            "A brewed potion identical to the store potion takes the store id, so the two stack", paid?.Id);
        ItemData higher = Brew(SpellNames.BULLS_STRENGTH, 5);
        Assert(higher != null && higher.Id == CraftingExecutor.CraftedPotionId(SpellNames.BULLS_STRENGTH, 5),
            "A brewed potion at another caster level keeps its own id per caster level", higher?.Id);

        ItemData debug = Brew(SpellNames.BULLS_STRENGTH, 3, debug: true);
        Assert(debug != null && debug.Id == PotionFactory.GeneratePotionId(SpellNames.BULLS_STRENGTH) + CraftingExecutor.DebugIdSuffix
               && debug.Name.StartsWith("[DEBUG] "),
            "A debug-brewed potion is marked in its name and its id", debug == null ? "no item" : $"{debug.Id} '{debug.Name}'");

        var inv = new global::Inventory();
        inv.AddItem(Brew(SpellNames.BULLS_STRENGTH, 3));
        inv.AddItem(Brew(SpellNames.BULLS_STRENGTH, 3, debug: true));
        int slots = 0;
        foreach (ItemData slot in inv.GeneralSlots)
            if (slot != null)
                slots++;
        Assert(slots == 2, "A debug-brewed potion never joins a stack of paid potions", $"{slots} slots");
    }

    private static void TestNonHitPointHealingPotions()
    {
        // Remove Disease (PHB p.271) and Lesser Restoration (PHB p.272) are Healing-typed but restore no hit points.
        foreach (string id in new[] { SpellNames.REMOVE_DISEASE, SpellNames.LESSER_RESTORATION })
        {
            SpellData spell = SpellDatabase.GetSpell(id);
            if (spell == null)
            {
                Assert(false, id + " exists in SpellDatabase");
                continue;
            }
            ItemData potion = PotionFactory.BuildPotion(spell, "test_" + id, spell.SpellLevel, 5, 0);
            Assert(potion != null && potion.ConsumableEffect != ConsumableEffectType.HealHP && !PotionFactory.RestoresHitPoints(spell),
                $"A potion of {spell.Name} is not a hit-point healing potion", Dice(potion));
        }
        SpellData clw = SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS);
        Assert(PotionFactory.RestoresHitPoints(clw), "Cure Light Wounds restores hit points");
    }

    private static void TestBrewListEligibility()
    {
        List<CraftableItemDefinition> all = CraftableItemRegistry.GenerateAllPotionDefinitions();
        bool HasSpell(string id) => all.Exists(d => d.DynamicSpellId == id);
        bool InStore(string id)
        {
            foreach (string potionId in PotionFactory.RegisteredPotionIds)
                if (potionId == PotionFactory.GeneratePotionId(id))
                    return true;
            return false;
        }
        Assert(HasSpell(SpellNames.CURE_LIGHT_WOUNDS), "Cure Light Wounds can be brewed (targets a creature touched)");
        Assert(HasSpell(SpellNames.BULLS_STRENGTH), "Bull's Strength can be brewed (targets a creature touched)");
        Assert(HasSpell(SpellNames.HASTE) && HasSpell(SpellNames.HOLD_PERSON),
            "Haste and Hold Person can be brewed (each targets creatures; the drinker is the target)");
        SpellData shield = SpellDatabase.GetSpell(SpellNames.SHIELD);
        Assert(shield != null && !HasSpell(SpellNames.SHIELD), "Shield (range personal) cannot be brewed (DMG p.286)");

        // Not potions by the rules: PHB p.89 (targets one or more creatures), DMG p.286 (Shield Other).
        var notPotions = new (string id, string why)[]
        {
            (SpellNames.SHIELD_OTHER, "caster and target must differ, DMG p.286"),
            (SpellNames.BLESS, "an Area entry, PHB p.205"),
            (SpellNames.SLEEP, "an Area entry, PHB p.280"),
            (SpellNames.SPIRITUAL_WEAPON, "an Effect entry, PHB p.283"),
            (SpellNames.RAY_OF_ENFEEBLEMENT, "a ray, an Effect entry, PHB p.269"),
            (SpellNames.MAGIC_WEAPON, "targets a weapon (an oil, DMG p.229), PHB p.251"),
        };
        foreach (var (id, why) in notPotions)
        {
            Assert(SpellDatabase.GetSpell(id) != null && !PotionFactory.IsEligibleForPotion(SpellDatabase.GetSpell(id))
                   && !HasSpell(id) && !InStore(id),
                $"{id} is neither brewed nor sold as a potion ({why})");
        }

        // Allowed by the rules but not resolvable when drunk yet (ITM-080): not offered.
        foreach (string id in new[] { SpellNames.INFLICT_LIGHT_WOUNDS, SpellNames.REMOVE_DISEASE, SpellNames.LESSER_RESTORATION })
        {
            Assert(!HasSpell(id) && !InStore(id), $"{id} is not offered as a potion until it resolves when drunk (ITM-080)");
        }
        foreach (CraftableItemDefinition d in all)
        {
            SpellData spell = SpellDatabase.GetSpell(d.DynamicSpellId);
            if (spell != null && spell.GetEffectiveRangeCategory() == SpellRangeCategory.Personal)
            {
                Assert(false, "No personal-range spell is on the brew list (DMG p.286)", d.DynamicSpellId);
                return;
            }
        }
        Assert(true, "No personal-range spell is on the brew list (DMG p.286)");
    }

    /// <summary>The drink path resolves a potion when it heals hit points (dice or a flat amount) or is a SpellEffect of a
    /// Buff, Debuff, Control or Illusion spell (GameManager.TryApplySpellConsumableEffect).</summary>
    private static string ResolveProblem(ItemData potion)
    {
        if (potion == null)
            return "not built";
        switch (potion.ConsumableEffect)
        {
            case ConsumableEffectType.HealHP:
                return (potion.HealDiceCount > 0 && potion.HealDiceSides > 0) || potion.HealAmount > 0 ? null : "HealHP heals 0";
            case ConsumableEffectType.SpellEffect:
                SpellData spell = SpellDatabase.GetSpell(potion.ConsumableSpellName);
                if (spell == null)
                    return "spell not found";
                return spell.EffectType == SpellEffectType.Buff || spell.EffectType == SpellEffectType.Debuff
                       || spell.EffectType == SpellEffectType.Control || spell.EffectType == SpellEffectType.Illusion
                    ? null
                    : "unsupported " + spell.EffectType;
            default:
                return "ConsumableEffect " + potion.ConsumableEffect;
        }
    }

    private static void TestEveryOfferedPotionResolves()
    {
        List<CraftableItemDefinition> all = CraftableItemRegistry.GenerateAllPotionDefinitions();
        var brewProblems = new List<string>();
        foreach (CraftableItemDefinition d in all)
        {
            string problem = ResolveProblem(Brew(d.DynamicSpellId, d.RequiredCasterLevel));
            if (problem != null)
                brewProblems.Add($"{d.DynamicSpellId}: {problem}");
        }
        Assert(all.Count > 20 && brewProblems.Count == 0, "Every potion on the brew list resolves when drunk",
            $"{all.Count} brewable; " + string.Join(", ", brewProblems));

        var storeProblems = new List<string>();
        foreach (string potionId in PotionFactory.RegisteredPotionIds)
        {
            string problem = ResolveProblem(ItemDatabase.GetItem(potionId));
            if (problem != null)
                storeProblems.Add($"{potionId}: {problem}");
        }
        Assert(PotionFactory.RegisteredPotionIds.Count > 20 && storeProblems.Count == 0, "Every factory potion in the store resolves when drunk",
            $"{PotionFactory.RegisteredPotionIds.Count} store potions; " + string.Join(", ", storeProblems));
    }

    // ── Drinking (Play mode) ────────────────────────────────────────────

    private static void TestDrinkBrewedHealingPotion()
    {
        CharacterController ogre = Spawn("ogre");
        if (ogre == null)
            return;
        try
        {
            global::Inventory inv = ogre.InventoryComp != null ? ogre.InventoryComp.CharacterInventory : null;
            Assert(inv != null, "The spawned creature has an inventory");
            if (inv == null)
                return;
            ItemData first = Brew(SpellNames.CURE_LIGHT_WOUNDS, 3);
            inv.AddItem(first);
            inv.AddItem(Brew(SpellNames.CURE_LIGHT_WOUNDS, 3));
            string id = first != null ? first.Id : "";
            int index = IndexOf(inv, id);
            Assert(index >= 0 && inv.GeneralSlots[index].StackCount == 2, "Two brewed potions sit in one stack");
            if (index < 0)
                return;

            ogre.Stats.CurrentHP = 1;
            bool used = GameManager.Instance.TryUseConsumableFromInventory(ogre, index, out string feedback);
            int healed = ogre.Stats.CurrentHP - 1;
            Assert(used, "A creature can drink a brewed potion of Cure Light Wounds", feedback);
            Assert(healed >= 4 && healed <= 11, "The brewed CL 3 potion heals 1d8+3 (4 to 11)", $"healed {healed}: {feedback}");
            int after = IndexOf(inv, id);
            Assert(after >= 0 && inv.GeneralSlots[after].StackCount == 1, "Drinking uses up one potion of the stack",
                after < 0 ? "stack gone" : $"{inv.GeneralSlots[after].StackCount} left");
        }
        finally
        {
            Cleanup(ogre);
        }
    }

    private static void TestDrinkBrewedBuffPotion()
    {
        CharacterController ogre = Spawn("ogre");
        if (ogre == null)
            return;
        try
        {
            global::Inventory inv = ogre.InventoryComp != null ? ogre.InventoryComp.CharacterInventory : null;
            if (inv == null)
            {
                Assert(false, "The spawned creature has an inventory");
                return;
            }
            ItemData bulls = Brew(SpellNames.BULLS_STRENGTH, 3);
            string id = bulls != null ? bulls.Id : "";
            inv.AddItem(bulls);
            int index = IndexOf(inv, id);
            Assert(index >= 0, "The brewed Bull's Strength potion is in the inventory");
            if (index < 0)
                return;

            int strBefore = ogre.Stats.EffectiveStrengthScore;
            bool used = GameManager.Instance.TryUseConsumableFromInventory(ogre, index, out string feedback);
            Assert(used, "A creature can drink a brewed potion of Bull's Strength", feedback);
            StatusEffectManager sem = ogre.StatusEffectManager;
            Assert(sem != null && sem.HasEffect(SpellNames.BULLS_STRENGTH), "The drinker is the target of the potion's Bull's Strength (PHB p.89-90)");
            Assert(ogre.Stats.EffectiveStrengthScore == strBefore + 4, "Bull's Strength from the potion grants +4 Strength (PHB p.207)",
                $"{strBefore} -> {ogre.Stats.EffectiveStrengthScore}");
            int rounds = sem != null ? sem.GetRemainingRounds(SpellNames.BULLS_STRENGTH) : 0;
            Assert(rounds == 30, "The potion's effect lasts by its own caster level: 1 min/level at CL 3 is 30 rounds (DMG p.229)", $"{rounds} rounds");
            Assert(IndexOf(inv, id) < 0, "The single potion is used up");
        }
        finally
        {
            Cleanup(ogre);
        }
    }
}
}

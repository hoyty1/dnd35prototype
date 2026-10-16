using System;
using System.Collections.Generic;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 17 (raw-components-all-casters, decision D3 of docs/designs/cast_core_batch11.md; SPL-114, SPL-020):
/// every caster needs its material (M), focus (F) and divine focus (DF) components (PHB p.140, p.174), the spell
/// component pouch holds the unpriced ones (PHB p.130), and every PC and NPC caster carries the kit its class calls for
/// (SpellComponentKit). Edit mode (no scene): the Components line of every spell (SpellComponentLines, checked against
/// the PHB) and its flags; the pure rule SpellComponentRules.Refusal for a wizard, a cleric, a druid and a creature
/// casting as a sorcerer (the pouch, Eschew Materials, the M/DF and F/DF split, the divine focus by class, priced
/// components and foci); the kit by class, level and alignment. Play mode (built characters through the cast core): an
/// NPC wizard without a pouch is refused a Touch of Fatigue and, with the kit, casts it; the AI's castable list leaves
/// out what it is refused; a PC cleric without a holy symbol is refused Bless; Stoneskin's diamond dust is required and
/// spent with the slot; a scroll needs no pouch; and the audit: every NPCDatabase entry and DMG NPC template with a
/// spell list, spawned through GameManager.InitializeNPCFromDefinition, carries the components every spell on its list
/// needs (priced components excepted: an NPC has those only as treasure). Database templates are cloned before any
/// change.
/// </summary>
public static class SpellComponentRulesTests
{
    private static int _passed;
    private static int _failed;

    private static readonly Vector2Int SpawnPos = new Vector2Int(-70, -70);

    /// <summary>
    /// Spells whose verbal or somatic flag differs from the PHB line (SPL-175): ten blocks give a somatic component the
    /// PHB line lacks. (Hypnotic Pattern's and Rainbow Pattern's verbal component is the bard's only, which no flag
    /// models; their line keeps the V.)
    /// </summary>
    private static readonly HashSet<string> KnownVerbalSomaticMismatches = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SpellNames.COMMAND, SpellNames.COMMAND_PLANTS, SpellNames.DARKNESS, SpellNames.FEATHER_FALL, SpellNames.FLARE,
        SpellNames.HOLD_PORTAL, SpellNames.KNOCK, SpellNames.LIGHT, SpellNames.TRUE_STRIKE, SpellNames.VENTRILOQUISM,
    };

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== SPELL COMPONENT RULES TESTS (batch 11 step 17, SPL-114, SPL-020) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();
        ItemDatabase.Init();
        NPCDatabase.Init();
        FeatDefinitions.Init();

        TestComponentLines();
        TestPureRules();
        TestKits();

        if (GameManager.Instance == null)
            Debug.Log("  SKIP: the casts and the NPC audit (no scene GameManager; they run in the Play pass)");
        else
            TestInPlay(GameManager.Instance);

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

    private static CharacterStats Stats(string cls, int level, Alignment alignment = Alignment.TrueNeutral, params string[] feats)
    {
        CharacterStats stats = TestHelpers.CreateStats("Component " + cls, level, cls, wis: 16, intelligence: 16, cha: 16);
        stats.CharacterAlignment = alignment;
        foreach (string feat in feats)
            stats.Feats.Add(feat);
        return stats;
    }

    private static global::Inventory Pack(CharacterStats owner, params string[] itemIds)
    {
        var inv = new global::Inventory { OwnerStats = owner };
        foreach (string id in itemIds)
            inv.AddItem(Clone(id));
        return inv;
    }

    private static ItemData Clone(string id)
    {
#pragma warning disable CS0618 // ItemIDs constants
        ItemData item = ItemDatabase.CloneItem(id);
#pragma warning restore CS0618
        if (item == null)
            Assert(false, "item id " + id + " exists in ItemDatabase");
        return item;
    }

    private static SpellData Spell(string id) => SpellDatabase.GetSpell(id);

    private static string Refusal(CharacterStats stats, global::Inventory inv, string spellId, string castingClass)
        => SpellComponentRules.Refusal(stats, inv, Spell(spellId), castingClass, out _);

    private static string Show(string reason) => reason ?? "none";

    // ── The Components lines ────────────────────────────────────────────

    private static void TestComponentLines()
    {
        int missing = 0;
        int flagMismatch = 0;
        var verbalSomatic = new List<string>();
        foreach (SpellData spell in SpellDatabase.GetAllSpells())
        {
            if (spell == null)
                continue;
            string line = SpellComponentLines.Get(spell.SpellId);
            if (line == null)
            {
                missing++;
                Debug.LogWarning("  no Components line: " + spell.SpellId);
                continue;
            }
            List<string> tokens = SpellComponentLines.Tokens(line);
            bool m = tokens.Contains("M") || tokens.Contains("M/DF");
            bool f = tokens.Contains("F") || tokens.Contains("F/DF");
            bool df = tokens.Contains("DF") || tokens.Contains("M/DF") || tokens.Contains("F/DF");
            if (spell.HasMaterialComponent != m || spell.HasFocusComponent != f || spell.HasDivineFocus != df
                || spell.MaterialOrDivineFocus != tokens.Contains("M/DF") || spell.FocusOrDivineFocus != tokens.Contains("F/DF"))
            {
                flagMismatch++;
                Debug.LogWarning("  flags differ from the line: " + spell.SpellId + " (" + line + ")");
            }
            if (spell.HasVerbalComponent != tokens.Contains("V") || spell.HasSomaticComponent != tokens.Contains("S"))
                verbalSomatic.Add(spell.SpellId);
        }
        Assert(missing == 0, "Every registered spell has its PHB Components line (SpellComponentLines)", missing + " missing");
        Assert(flagMismatch == 0, "Every spell's M, F, DF, M/DF and F/DF flags are its line's (SpellComponentLines.Apply at registration)", flagMismatch + " differ");
        var unexpected = verbalSomatic.FindAll(id => !KnownVerbalSomaticMismatches.Contains(id));
        var fixedOnes = new List<string>(KnownVerbalSomaticMismatches);
        fixedOnes.RemoveAll(id => verbalSomatic.Contains(id));
        Assert(unexpected.Count == 0 && fixedOnes.Count == 0,
            "The verbal and somatic flags differ from the PHB line only for the 10 spells SPL-175 lists",
            "unexpected: " + string.Join(",", unexpected) + "; now matching: " + string.Join(",", fixedOnes));

        // Spot checks against the PHB (chapter 11).
        SpellData grease = Spell(SpellNames.GREASE);
        Assert(grease.HasMaterialComponent && !grease.HasFocusComponent && !grease.HasDivineFocus, "Grease is V, S, M (PHB p.237)");
        SpellData bless = Spell(SpellNames.BLESS);
        Assert(!bless.HasMaterialComponent && bless.HasDivineFocus && !bless.MaterialOrDivineFocus, "Bless is V, S, DF (PHB p.205)");
        SpellData holdPerson = Spell(SpellNames.HOLD_PERSON);
        Assert(holdPerson.HasFocusComponent && holdPerson.HasDivineFocus && holdPerson.FocusOrDivineFocus && !holdPerson.HasMaterialComponent,
            "Hold Person is V, S, F/DF (PHB p.241)");
        SpellData mageArmor = Spell(SpellNames.MAGE_ARMOR);
        Assert(mageArmor.HasFocusComponent && !mageArmor.HasMaterialComponent, "Mage Armor is V, S, F (PHB p.249; it was marked M)");
        SpellData pfe = Spell(SpellNames.PROTECTION_FROM_EVIL);
        Assert(pfe.HasMaterialComponent && pfe.HasDivineFocus && pfe.MaterialOrDivineFocus, "Protection from Evil is V, S, M/DF (PHB p.266)");
        SpellData deathKnell = Spell(SpellNames.DEATH_KNELL);
        Assert(!deathKnell.HasDivineFocus && !deathKnell.HasMaterialComponent, "Death Knell is V, S (PHB p.217)");
        SpellData consecrate = Spell(SpellNames.CONSECRATE);
        Assert(consecrate.HasMaterialComponent && consecrate.HasDivineFocus && !consecrate.MaterialOrDivineFocus, "Consecrate is V, S, M, DF: both (PHB p.212)");
    }

    // ── The pure rule ───────────────────────────────────────────────────

    private static void TestPureRules()
    {
        CharacterStats wizard = Stats("Wizard", 5);
        Assert(Refusal(wizard, Pack(wizard), SpellNames.GREASE, "Wizard") == SpellComponentRules.ReasonNoPouch,
            "A wizard without a pouch cannot cast Grease (M; PHB p.174, p.130)");
        Assert(Refusal(wizard, Pack(wizard, ItemIDs.COMPONENT_SPELL_POUCH), SpellNames.GREASE, "Wizard") == null,
            "With a spell component pouch the wizard can cast Grease");
        Assert(Refusal(wizard, Pack(wizard), SpellNames.MAGE_ARMOR, "Wizard") == SpellComponentRules.ReasonNoPouch,
            "A pouchless wizard cannot cast Mage Armor (F: an unpriced focus is in the pouch, PHB p.174)");
        Assert(Refusal(wizard, Pack(wizard), SpellNames.MAGIC_MISSILE, "Wizard") == null, "Magic Missile (V, S) needs nothing");

        CharacterStats eschewing = Stats("Wizard", 5, Alignment.TrueNeutral, SpellComponentRules.EschewMaterials);
        Assert(Refusal(eschewing, Pack(eschewing), SpellNames.GREASE, "Wizard") == null,
            "Eschew Materials: a pouchless wizard casts Grease (an M costing 1 gp or less; PHB p.94)");
        Assert(Refusal(eschewing, Pack(eschewing), SpellNames.MAGE_ARMOR, "Wizard") == SpellComponentRules.ReasonNoPouch,
            "Eschew Materials does not waive a focus: no Mage Armor without the pouch");
        Assert(Refusal(eschewing, Pack(eschewing, ItemIDs.COMPONENT_SPELL_POUCH), SpellNames.STONESKIN, "Wizard") == SpellComponentRules.ReasonNoCostlyComponent,
            "Eschew Materials does not waive a priced material component (Stoneskin's 250 gp of diamond dust)");

        CharacterStats cleric = Stats("Cleric", 5, Alignment.NeutralGood);
        Assert(Refusal(cleric, Pack(cleric, ItemIDs.COMPONENT_SPELL_POUCH), SpellNames.BLESS, "Cleric") == SpellComponentRules.ReasonNoDivineFocus,
            "A cleric without a holy symbol cannot cast Bless (DF; the pouch holds no divine focus, PHB p.130, p.174)");
        Assert(Refusal(cleric, Pack(cleric, ItemIDs.HOLY_SYMBOL_WOODEN), SpellNames.BLESS, "Cleric") == null,
            "With a wooden holy symbol the cleric casts Bless");
        Assert(Refusal(cleric, Pack(cleric, ItemIDs.UNHOLY_SYMBOL_SILVER), SpellNames.BLESS, "Cleric") == SpellComponentRules.ReasonNoDivineFocus,
            "A good cleric's divine focus is a holy symbol: an unholy one does not serve it (PHB p.174)");
        CharacterStats evilCleric = Stats("Cleric", 5, Alignment.NeutralEvil);
        Assert(Refusal(evilCleric, Pack(evilCleric, ItemIDs.UNHOLY_SYMBOL_WOODEN), SpellNames.BANE, "Cleric") == null
               && Refusal(evilCleric, Pack(evilCleric, ItemIDs.HOLY_SYMBOL_SILVER), SpellNames.BANE, "Cleric") == SpellComponentRules.ReasonNoDivineFocus,
            "An evil cleric's divine focus is an unholy symbol, and a looted holy symbol does not serve it (PHB p.174)");
        CharacterStats neutralCleric = Stats("Cleric", 5, Alignment.TrueNeutral);
        Assert(Refusal(neutralCleric, Pack(neutralCleric, ItemIDs.HOLY_SYMBOL_WOODEN), SpellNames.BLESS, "Cleric") == null
               && Refusal(neutralCleric, Pack(neutralCleric, ItemIDs.UNHOLY_SYMBOL_WOODEN), SpellNames.BLESS, "Cleric") == null,
            "A neutral cleric may use a holy or an unholy symbol (PHB p.130; delegated RAW reading 2026-10-14)");
        CharacterStats paladin = Stats("Paladin", 6, Alignment.LawfulGood);
        Assert(Refusal(paladin, Pack(paladin, ItemIDs.UNHOLY_SYMBOL_WOODEN), SpellNames.BLESS, "Paladin") == SpellComponentRules.ReasonNoDivineFocus
               && Refusal(paladin, Pack(paladin, ItemIDs.HOLY_SYMBOL_WOODEN), SpellNames.BLESS, "Paladin") == null,
            "A paladin's divine focus is a holy symbol (PHB p.174)");
        Assert(Refusal(cleric, Pack(cleric, ItemIDs.HOLY_SYMBOL_WOODEN), SpellNames.PROTECTION_FROM_EVIL, "Cleric") == null,
            "M/DF: a pouchless cleric casts Protection from Evil with the divine focus (PHB p.174)");
        Assert(Refusal(cleric, Pack(cleric, ItemIDs.COMPONENT_SPELL_POUCH), SpellNames.PROTECTION_FROM_EVIL, "Cleric") == SpellComponentRules.ReasonNoDivineFocus,
            "M/DF: the cleric's divine version needs the divine focus, not the material component");
        Assert(Refusal(wizard, Pack(wizard), SpellNames.PROTECTION_FROM_EVIL, "Wizard") == SpellComponentRules.ReasonNoPouch
               && Refusal(wizard, Pack(wizard, ItemIDs.COMPONENT_SPELL_POUCH), SpellNames.PROTECTION_FROM_EVIL, "Wizard") == null,
            "M/DF: the wizard's arcane version needs the material component (the pouch) and no divine focus");
        Assert(Refusal(cleric, Pack(cleric, ItemIDs.HOLY_SYMBOL_WOODEN), SpellNames.HOLD_PERSON, "Cleric") == null
               && Refusal(cleric, Pack(cleric), SpellNames.HOLD_PERSON, "Cleric") == SpellComponentRules.ReasonNoDivineFocus,
            "F/DF: the cleric's Hold Person needs the divine focus only");
        Assert(Refusal(wizard, Pack(wizard), SpellNames.HOLD_PERSON, "Wizard") == SpellComponentRules.ReasonNoPouch,
            "F/DF: the wizard's Hold Person needs its focus (in the pouch)");
        CharacterStats consecrator = Stats("Cleric", 5, Alignment.NeutralGood);
        consecrator.ComponentGold = 0;
        string consecratePoor = Refusal(consecrator, Pack(consecrator, ItemIDs.HOLY_SYMBOL_WOODEN), SpellNames.CONSECRATE, "Cleric");
        consecrator.ComponentGold = 50;
        string consecrateNoSymbol = Refusal(consecrator, Pack(consecrator), SpellNames.CONSECRATE, "Cleric");
        string consecrateOk = Refusal(consecrator, Pack(consecrator, ItemIDs.HOLY_SYMBOL_WOODEN), SpellNames.CONSECRATE, "Cleric");
        Assert(consecratePoor == SpellComponentRules.ReasonNoCostlyComponent && consecrateNoSymbol == SpellComponentRules.ReasonNoDivineFocus && consecrateOk == null,
            "M, DF: Consecrate needs both its 50 gp of holy water and silver dust and the divine focus (PHB p.212)",
            Show(consecratePoor) + " / " + Show(consecrateNoSymbol) + " / " + Show(consecrateOk));

        CharacterStats druid = Stats("Druid", 5);
        Assert(Refusal(druid, Pack(druid, ItemIDs.HOLY_SYMBOL_WOODEN), SpellNames.ENTANGLE, "Druid") == SpellComponentRules.ReasonNoDivineFocus
               && Refusal(druid, Pack(druid, ItemIDs.HOLLY_AND_MISTLETOE), SpellNames.ENTANGLE, "Druid") == null,
            "A druid's divine focus is holly and mistletoe, not a holy symbol (PHB p.174)");

        CharacterStats sorcerer = Stats("Sorcerer", 7);
        Assert(Refusal(sorcerer, Pack(sorcerer), SpellNames.BLESS, "Sorcerer") == null,
            "A creature casting a cleric spell as an arcane spell (a dragon, MM p.69) needs no divine focus (decision D3)");

        // Priced components and foci are items (or component gold), never in the pouch.
        CharacterStats stoneskinner = Stats("Wizard", 7);
        Assert(Refusal(stoneskinner, Pack(stoneskinner, ItemIDs.COMPONENT_SPELL_POUCH), SpellNames.STONESKIN, "Wizard") == SpellComponentRules.ReasonNoCostlyComponent
               && Refusal(stoneskinner, Pack(stoneskinner, ItemIDs.COMPONENT_DIAMOND_DUST), SpellNames.STONESKIN, "Wizard") == SpellComponentRules.ReasonNoPouch
               && Refusal(stoneskinner, Pack(stoneskinner, ItemIDs.COMPONENT_SPELL_POUCH, ItemIDs.COMPONENT_DIAMOND_DUST), SpellNames.STONESKIN, "Wizard") == null,
            "Stoneskin needs the pouch (granite) and the 250 gp of diamond dust (PHB p.284)");
        CharacterStats identifier = Stats("Wizard", 3);
        identifier.ComponentGold = 99;
        string identifyPoor = Refusal(identifier, Pack(identifier, ItemIDs.COMPONENT_SPELL_POUCH), SpellNames.IDENTIFY, "Wizard");
        identifier.ComponentGold = 100;
        string identifyRich = Refusal(identifier, Pack(identifier, ItemIDs.COMPONENT_SPELL_POUCH), SpellNames.IDENTIFY, "Wizard");
        string identifyNoPouch = Refusal(identifier, Pack(identifier), SpellNames.IDENTIFY, "Wizard");
        Assert(identifyPoor == SpellComponentRules.ReasonNoCostlyComponent && identifyRich == null && identifyNoPouch == SpellComponentRules.ReasonNoPouch,
            "Identify's 100 gp pearl with no item is paid from component gold (ITM-029), and its wine and owl feather come from the pouch (PHB p.243)",
            Show(identifyPoor) + " / " + Show(identifyRich) + " / " + Show(identifyNoPouch));
        CharacterStats warder = Stats("Cleric", 5, Alignment.NeutralGood);
        warder.ComponentGold = 200;
        string glyphNoPouch = Refusal(warder, Pack(warder, ItemIDs.HOLY_SYMBOL_WOODEN), SpellNames.GLYPH_OF_WARDING, "Cleric");
        string glyphPouch = Refusal(warder, Pack(warder, ItemIDs.HOLY_SYMBOL_WOODEN, ItemIDs.COMPONENT_SPELL_POUCH), SpellNames.GLYPH_OF_WARDING, "Cleric");
        Assert(glyphNoPouch == SpellComponentRules.ReasonNoPouch && glyphPouch == null,
            "Glyph of Warding's incense has no price: the caster needs the pouch as well as the 200 gp of diamond dust (PHB p.236-237)",
            Show(glyphNoPouch) + " / " + Show(glyphPouch));
        CharacterStats shielder = Stats("Cleric", 3);
        Assert(Refusal(shielder, Pack(shielder, ItemIDs.HOLY_SYMBOL_WOODEN), SpellNames.SHIELD_OTHER, "Cleric") == SpellComponentRules.ReasonNoFocus
               && Refusal(shielder, Pack(shielder, ItemIDs.FOCUS_PLATINUM_RINGS), SpellNames.SHIELD_OTHER, "Cleric") == null,
            "Shield Other's focus is its pair of platinum rings (PHB p.278), not in a pouch");
    }

    // ── The kits ────────────────────────────────────────────────────────

    private static string KitOf(CharacterStats stats) => string.Join(",", SpellComponentKit.KitFor(stats));

    private static void TestKits()
    {
        Assert(KitOf(Stats("Wizard", 1)) == ItemIDs.COMPONENT_SPELL_POUCH, "A wizard's kit is a spell component pouch (PHB p.58)");
        Assert(KitOf(Stats("Bard", 1)) == ItemIDs.COMPONENT_SPELL_POUCH && KitOf(Stats("Sorcerer", 1)) == ItemIDs.COMPONENT_SPELL_POUCH,
            "A bard's and a sorcerer's kit is a pouch (PHB p.30, p.55)");
        Assert(KitOf(Stats("Cleric", 1, Alignment.LawfulGood)) == ItemIDs.COMPONENT_SPELL_POUCH + "," + ItemIDs.HOLY_SYMBOL_WOODEN,
            "A good cleric carries a pouch and a wooden holy symbol (PHB p.33)");
        Assert(KitOf(Stats("Cleric", 1, Alignment.ChaoticEvil)) == ItemIDs.COMPONENT_SPELL_POUCH + "," + ItemIDs.UNHOLY_SYMBOL_WOODEN,
            "An evil cleric carries an unholy symbol (PHB p.174)");
        Assert(KitOf(Stats("Adept", 1, Alignment.NeutralEvil)) == ItemIDs.COMPONENT_SPELL_POUCH + "," + ItemIDs.UNHOLY_SYMBOL_WOODEN,
            "An adept carries a pouch and its symbol (DMG p.108)");
        Assert(KitOf(Stats("Druid", 1)) == ItemIDs.COMPONENT_SPELL_POUCH + "," + ItemIDs.HOLLY_AND_MISTLETOE,
            "A druid carries a pouch and holly and mistletoe (PHB p.37)");
        Assert(KitOf(Stats("Paladin", 1, Alignment.LawfulGood)) == ItemIDs.HOLY_SYMBOL_WOODEN
               && KitOf(Stats("Paladin", 4, Alignment.LawfulGood)) == ItemIDs.COMPONENT_SPELL_POUCH + "," + ItemIDs.HOLY_SYMBOL_WOODEN,
            "A paladin carries a holy symbol (PHB p.46), and a pouch once it casts (4th level, PHB p.44)");
        Assert(KitOf(Stats("Ranger", 3)) == "" && KitOf(Stats("Ranger", 4)) == ItemIDs.COMPONENT_SPELL_POUCH + "," + ItemIDs.HOLLY_AND_MISTLETOE,
            "A ranger carries the kit once it casts (4th level, PHB p.48)");
        Assert(KitOf(Stats("Fighter", 5)) == "", "A fighter carries no kit");
        CharacterStats dragon = Stats("Warrior", 5);
        dragon.InnateSpellcastingClass = "Sorcerer";
        Assert(KitOf(dragon) == ItemIDs.COMPONENT_SPELL_POUCH, "A monster casting as a sorcerer carries a pouch (MM p.69, p.315)");

        CharacterStats cleric = Stats("Cleric", 3, Alignment.NeutralGood);
        var inv = new global::Inventory { OwnerStats = cleric };
        inv.AddItem(Clone(ItemIDs.HOLY_SYMBOL_SILVER));
        int first = SpellComponentKit.Give(cleric, inv);
        int second = SpellComponentKit.Give(cleric, inv);
        Assert(first == 1 && second == 0 && SpellComponentRules.InventoryHoldsAny(inv, ItemIDs.COMPONENT_SPELL_POUCH)
               && !SpellComponentRules.InventoryHoldsAny(inv, ItemIDs.HOLY_SYMBOL_WOODEN),
            "The kit adds only what the pack lacks (a silver holy symbol already serves)", first + "/" + second);
        CharacterStats evil = Stats("Cleric", 3, Alignment.ChaoticEvil);
        var evilInv = new global::Inventory { OwnerStats = evil };
        evilInv.AddItem(Clone(ItemIDs.HOLY_SYMBOL_SILVER));
        SpellComponentKit.Give(evil, evilInv);
        Assert(SpellComponentRules.InventoryHoldsAny(evilInv, ItemIDs.UNHOLY_SYMBOL_WOODEN),
            "An evil cleric holding only a holy symbol still gets its unholy symbol (a holy symbol does not serve it)");
    }

    // ── Play mode: casts through the cast core and the NPC audit ─────────

    private static void TestInPlay(GameManager gm)
    {
        if (gm.Harness_OpenCastCount != 0)
        {
            Debug.Log("  SKIP: the casts (a cast is open in this Play session)");
            return;
        }

        CharacterController wizard = null;
        CharacterController foe = null;
        CharacterController cleric = null;
        try
        {
            wizard = TestHelpers.CreateCharacter("ComponentWizard", "Wizard", level: 9, con: 14, intelligence: 18, bab: 4);
            foe = TestHelpers.CreateCharacter("ComponentFoe", "Fighter", level: 10, con: 30, bab: 10, gridPosition: new Vector2Int(1, 0));
            wizard.SetTeam(CharacterTeam.Enemy);
            foe.SetTeam(CharacterTeam.Player);
            SpellcastingComponent sc = wizard.gameObject.AddComponent<SpellcastingComponent>();
            sc.Init(wizard.Stats);
            sc.SpellSlots.Clear();

            // An NPC wizard without a pouch: refused at Legality, nothing spent; the AI leaves the spell out.
            SpellSlot slot = AddSlot(sc, SpellNames.TOUCH_OF_FATIGUE, "Wizard");
            wizard.Actions.Reset();
            CastOutcome noPouch = gm.CheckLegality(SpellCastRequest.ForAI(wizard, foe, Spell(SpellNames.TOUCH_OF_FATIGUE)));
            Assert(noPouch.Kind == CastOutcomeKind.Refused && noPouch.Reason == SpellComponentRules.ReasonNoPouch,
                "An NPC wizard without a pouch is refused a Touch of Fatigue at Legality (the NpcNoComponentCheck bit is gone)", "" + noPouch);
            bool cast = gm.RunCastSync(SpellCastRequest.ForAI(wizard, foe, Spell(SpellNames.TOUCH_OF_FATIGUE)), out CastOutcome refused);
            Assert(!cast && !slot.IsUsed && !wizard.Actions.StandardActionUsed,
                "The refused NPC cast spends neither the slot nor the standard action", "" + refused);
            AddSlot(sc, SpellNames.GREASE, "Wizard");
            List<SpellData> castableWithoutPouch = sc.GetCastablePreparedSpells();
            Assert(!castableWithoutPouch.Exists(s => s.SpellId == SpellNames.GREASE)
                   && !castableWithoutPouch.Exists(s => s.SpellId == SpellNames.TOUCH_OF_FATIGUE),
                "The AI's castable list leaves out Grease and Touch of Fatigue without a pouch");
            Assert(SpellComponentRules.Refusal(wizard, Spell(SpellNames.GREASE), SpellCastRequest.AiSourceFor(wizard, Spell(SpellNames.GREASE)), out _) == SpellComponentRules.ReasonNoPouch,
                "An NPC wizard without a pouch cannot cast Grease");

            // A scroll supplies its spell's components (DMG p.238): no pouch needed.
            var scrollSource = new CastSource { Kind = CastSourceKind.Scroll };
            Assert(SpellComponentRules.Refusal(wizard, Spell(SpellNames.GREASE), scrollSource, out _) == null,
                "A scroll's Grease needs no pouch (the scroll supplies its components)");

            // With the default gear the same casts pass.
            Assert(SpellComponentKit.Give(wizard.Stats, wizard.GetInventoryData()) == 1, "The wizard's kit adds a pouch");
            Assert(sc.GetCastablePreparedSpells().Exists(s => s.SpellId == SpellNames.GREASE),
                "With the default gear the AI's castable list has Grease");
            wizard.Actions.Reset();
            cast = gm.RunCastSync(SpellCastRequest.ForAI(wizard, foe, Spell(SpellNames.TOUCH_OF_FATIGUE)), out CastOutcome withPouch);
            Assert(cast && withPouch.WasCast && slot.IsUsed, "With the default gear the NPC wizard casts Touch of Fatigue", "" + withPouch);

            // Costly components are spent with the slot (SPL-020): Stoneskin's diamond dust.
            SpellSlot stoneskinSlot = AddSlot(sc, SpellNames.STONESKIN, "Wizard");
            wizard.Actions.Reset();
            CastOutcome noDust = gm.CheckLegality(StoneskinRequest(wizard));
            Assert(noDust.Reason == SpellComponentRules.ReasonNoCostlyComponent, "Without diamond dust Stoneskin is refused at Legality", "" + noDust);
            wizard.GetInventoryData().AddItem(Clone(ItemIDs.COMPONENT_DIAMOND_DUST));
            cast = gm.RunCastSync(StoneskinRequest(wizard), out CastOutcome stoneskin);
            Assert(cast && stoneskin.WasCast && stoneskinSlot.IsUsed
                   && !SpellComponentRules.InventoryHoldsAny(wizard.GetInventoryData(), ItemIDs.COMPONENT_DIAMOND_DUST),
                "With the dust Stoneskin is cast and the dust is used up with the slot (PHB p.174)", "" + stoneskin);
            Assert(SpellComponentRules.InventoryHoldsAny(wizard.GetInventoryData(), ItemIDs.COMPONENT_SPELL_POUCH),
                "The pouch is not consumed");
            var removed = new List<KeyValuePair<int, ItemData>>();
            wizard.GetInventoryData().AddItem(Clone(ItemIDs.COMPONENT_DIAMOND_DUST));
            string spentLog = SpellComponentRules.SpendCostlyComponents(wizard, Spell(SpellNames.STONESKIN), new CastSource(), removed, out int gold);
            SpellComponentRules.RefundCostlyComponents(wizard, removed, gold);
            Assert(spentLog != null && removed.Count == 1 && SpellComponentRules.InventoryHoldsAny(wizard.GetInventoryData(), ItemIDs.COMPONENT_DIAMOND_DUST),
                "A refund (a cast cancelled at its AoO prompt) gives the dust back");

            // A priced component held in a hand passes the test and is spent from the hand, and a refund puts it back there.
            global::Inventory wizInv = wizard.GetInventoryData();
            ItemData packDust = TakeItem(wizInv, ItemIDs.COMPONENT_DIAMOND_DUST);
            ItemData heldBefore = wizInv.RightHandSlot;
            wizInv.RightHandSlot = Clone(ItemIDs.COMPONENT_DIAMOND_DUST);
            var handRemoved = new List<KeyValuePair<int, ItemData>>();
            bool handPasses = SpellComponentRules.Refusal(wizard, Spell(SpellNames.STONESKIN), new CastSource(), out _) == null;
            string handLog = SpellComponentRules.SpendCostlyComponents(wizard, Spell(SpellNames.STONESKIN), new CastSource(), handRemoved, out int handGold);
            bool handEmptied = wizInv.RightHandSlot == null;
            SpellComponentRules.RefundCostlyComponents(wizard, handRemoved, handGold);
            Assert(handPasses && handLog != null && handEmptied && wizInv.RightHandSlot != null && wizInv.RightHandSlot.Id == ItemIDs.COMPONENT_DIAMOND_DUST,
                "Diamond dust held in a hand serves Stoneskin, is used up from the hand, and a refund puts it back in the hand",
                handPasses + "/" + (handLog ?? "no log") + "/" + handEmptied);
            wizInv.RightHandSlot = heldBefore;
            if (packDust != null)
                wizInv.AddItem(packDust);

            // A counterspell is a casting of the counter (PHB p.170): a Grease (V, S, M) countering a Grease needs the pouch
            // (the unused Grease slot above; Haste against Slow would do the same, but the test wizard's legacy SlotsRemaining
            // view stops below 3rd level); the castable tests of a prepared caster agree (the AI's and the counter's), while
            // the PC's Cast Spell button still opens the menu, where the refusal is logged.
            bool counterWithPouch = wizard.HasSpellAvailableForCounter(SpellNames.GREASE);
            bool anyWithPouch = sc.HasAnyCastablePreparedSpell();
            ItemData pouch = TakeItem(wizInv, ItemIDs.COMPONENT_SPELL_POUCH);
            bool counterWithoutPouch = wizard.HasSpellAvailableForCounter(SpellNames.GREASE);
            bool anyWithoutPouch = sc.HasAnyCastablePreparedSpell();
            bool slotLeftWithoutPouch = sc.HasAnySpellWithSlotLeft();
            bool listEmptyWithoutPouch = sc.GetCastablePreparedSpells().Count == 0;
            if (pouch != null)
                wizInv.AddItem(pouch);
            Assert(pouch != null && counterWithPouch && !counterWithoutPouch,
                "A pouchless wizard cannot counter a Grease with its own Grease (M); with the kit it can (PHB p.170, p.174)",
                counterWithPouch + "/" + counterWithoutPouch);
            Assert(anyWithPouch && !anyWithoutPouch && listEmptyWithoutPouch && slotLeftWithoutPouch,
                "Without its components a prepared caster has no castable spell for the AI (HasAnyCastablePreparedSpell agrees with the castable list), but the Cast Spell button still opens (HasAnySpellWithSlotLeft)",
                anyWithPouch + "/" + anyWithoutPouch + "/" + listEmptyWithoutPouch + "/" + slotLeftWithoutPouch);

            // A PC cleric without a holy symbol cannot cast a DF spell.
            cleric = TestHelpers.CreateCharacter("ComponentCleric", "Cleric", level: 5, wis: 16, gridPosition: new Vector2Int(0, 2));
            cleric.SetTeam(CharacterTeam.Player);
            cleric.Stats.CharacterAlignment = Alignment.NeutralGood;
            SpellcastingComponent csc = cleric.gameObject.AddComponent<SpellcastingComponent>();
            csc.Init(cleric.Stats);
            csc.SpellSlots.Clear();
            AddSlot(csc, SpellNames.BLESS, "Cleric");
            cleric.Actions.Reset();
            // Bless is an area (the 50-ft. burst on the caster, PHB p.205): an area request, as the PC's area cast builds it
            // (a single-target request of an Area spell is refused, area-needs-aim, batch 11 step 25).
            SpellCastRequest blessNoSymbol = SpellCastRequest.ForUiArea(SpellCastRequest.ForUi(cleric, Spell(SpellNames.BLESS), null, new CastSource()),
                new List<CharacterController> { cleric }, new HashSet<Vector2Int> { cleric.GridPosition }, AoESystem.GetCreatureCentredOrigins(cleric)[0]);
            CastOutcome noSymbol = gm.CheckLegality(blessNoSymbol);
            Assert(noSymbol.Kind == CastOutcomeKind.Refused && noSymbol.Reason == SpellComponentRules.ReasonNoDivineFocus,
                "A PC cleric without a holy symbol is refused Bless at Legality (DF, PHB p.174)", "" + noSymbol);
            SpellComponentKit.Give(cleric.Stats, cleric.GetInventoryData());
            // Bless is an area (the 50-ft. burst on the caster, PHB p.205): an area request, as the PC's area cast builds it
            // (a single-target request of an Area spell is refused, area-needs-aim, batch 11 step 25).
            SpellCastRequest blessWithSymbol = SpellCastRequest.ForUiArea(SpellCastRequest.ForUi(cleric, Spell(SpellNames.BLESS), null, new CastSource()),
                new List<CharacterController> { cleric }, new HashSet<Vector2Int> { cleric.GridPosition }, AoESystem.GetCreatureCentredOrigins(cleric)[0]);
            CastOutcome withSymbol = gm.CheckLegality(blessWithSymbol);
            Assert(withSymbol.Kind == CastOutcomeKind.Resolved, "With the kit's holy symbol the cleric may cast Bless", "" + withSymbol);
        }
        finally
        {
            if (wizard != null) Object.DestroyImmediate(wizard.gameObject);
            if (foe != null) Object.DestroyImmediate(foe.gameObject);
            if (cleric != null) Object.DestroyImmediate(cleric.gameObject);
        }

        TestNpcAudit(gm);
    }

    /// <summary>
    /// An AI request for Stoneskin on the caster without the NPC path's own slot pre-check (NpcOwnLegalityChecks), whose
    /// legacy SlotsRemaining view caps a test wizard below 4th level; the slot itself is spent as usual.
    /// </summary>
    private static SpellCastRequest StoneskinRequest(CharacterController wizard)
    {
        SpellCastRequest req = SpellCastRequest.ForAI(wizard, wizard, Spell(SpellNames.STONESKIN));
        req.Quirks &= ~LegacyCastQuirk.NpcOwnLegalityChecks;
        return req;
    }

    /// <summary>Takes the first item with <paramref name="itemId"/> out of <paramref name="inv"/>'s pack; null when there is none.</summary>
    private static ItemData TakeItem(global::Inventory inv, string itemId)
    {
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            ItemData item = inv.GeneralSlots[i];
            if (item != null && string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase))
            {
                inv.GeneralSlots[i] = null;
                return item;
            }
        }
        return null;
    }

    private static SpellSlot AddSlot(SpellcastingComponent sc, string spellId, string cls)
    {
        SpellData template = Spell(spellId);
        var slot = new SpellSlot(template.SpellLevel, template.Clone(), false, cls);
        sc.SpellSlots.Add(slot);
        return slot;
    }

    /// <summary>
    /// The audit decision D3's risk calls for: every NPC with spells carries the components its spell list needs, so a
    /// missed kit entry cannot silently disable its spells. Priced components and foci are left out (an NPC has them only
    /// as treasure).
    /// </summary>
    private static void TestNpcAudit(GameManager gm)
    {
        var defs = new List<KeyValuePair<string, NPCDefinition>>();
        foreach (NPCDefinition def in NPCDatabase.AllNPCs)
        {
            if (def != null && HasSpells(def))
                defs.Add(new KeyValuePair<string, NPCDefinition>(def.Id, CreatureTemplateRegistry.ApplyTemplatesClone(def)));
        }
        foreach (NPCTemplate template in NPCTemplateDatabase.GetAllTemplates())
        {
            NPCDefinition def = template != null ? QuickSpawnSystem.CreateFromTemplate(template) : null;
            if (def != null && HasSpells(def))
                defs.Add(new KeyValuePair<string, NPCDefinition>("template " + template.ClassName + " " + template.Level, def));
        }

        int casters = 0;
        int spellsChecked = 0;
        var failures = new List<string>();
        var pricedLeftOut = new List<string>();
        foreach (KeyValuePair<string, NPCDefinition> entry in defs)
        {
            CharacterController cc = null;
            try
            {
                var go = new GameObject("ComponentAudit_" + entry.Key);
                cc = go.AddComponent<CharacterController>();
                gm.InitializeNPCFromDefinition(cc, entry.Value, SpawnPos, null, null);
                if (cc.Spellcasting == null)
                    continue;
                casters++;
                // The definition's lists (a lower bound) and what Init gave the spawned creature: every prepared slot
                // (domain slots and auto-filled ones included) and the spontaneous known spells.
                var ids = new HashSet<string>(entry.Value.KnownSpellIds ?? new List<string>());
                if (entry.Value.PreparedSpellSlotIds != null)
                    ids.UnionWith(entry.Value.PreparedSpellSlotIds);
                foreach (SpellSlot spawnedSlot in cc.Spellcasting.SpellSlots)
                {
                    if (spawnedSlot != null && spawnedSlot.PreparedSpell != null)
                        ids.Add(spawnedSlot.PreparedSpell.SpellId);
                }
                if (cc.Spellcasting.IsSpontaneousCaster)
                    ids.UnionWith(cc.Spellcasting.SpontaneousData.GetAllKnownSpellIds());
                foreach (string id in ids)
                {
                    SpellData spell = SpellDatabase.GetSpell(id);
                    if (spell == null)
                        continue;
                    spellsChecked++;
                    string reason = SpellComponentRules.Refusal(cc, spell, SpellCastRequest.AiSourceFor(cc, spell), out string missing);
                    if (reason == null)
                        continue;
                    if (reason == SpellComponentRules.ReasonNoCostlyComponent || IsPricedFocus(reason, spell))
                        pricedLeftOut.Add(entry.Key + ": " + id + " (" + missing + ")");
                    else
                        failures.Add(entry.Key + ": " + id + " (" + reason + ", " + missing + ")");
                }
            }
            catch (Exception e)
            {
                failures.Add(entry.Key + ": spawn threw " + e.GetType().Name + " " + e.Message);
            }
            finally
            {
                if (cc != null)
                {
                    if (gm.Grid != null)
                        gm.Grid.ClearCreatureOccupancy(cc);
                    Object.DestroyImmediate(cc.gameObject);
                }
            }
        }
        Debug.Log($"  audit: {casters} spawned casters, {spellsChecked} spells checked");
        // Spells an NPC cannot cast for want of a priced component or focus, which it has only as treasure (decision D3,
        // CRE-072): listed so they are visible, not silently disabled.
        Debug.Log($"  audit: {pricedLeftOut.Count} NPC spells left out for a priced component or focus (CRE-072): " + string.Join("; ", pricedLeftOut));
        Assert(casters > 0 && failures.Count == 0,
            "Audit: every NPC entry and DMG NPC template with spells carries the components its spells need (the kit; decision D3)",
            string.Join("; ", failures));
    }

    private static bool HasSpells(NPCDefinition def)
        => (def.KnownSpellIds != null && def.KnownSpellIds.Count > 0) || (def.PreparedSpellSlotIds != null && def.PreparedSpellSlotIds.Count > 0);

    private static bool IsPricedFocus(string reason, SpellData spell)
    {
        if (reason != SpellComponentRules.ReasonNoFocus)
            return false;
        SpellComponentRequirements req = SpellComponentRegistry.GetRequirements(spell.SpellId);
        return req != null && req.FocusComponents != null && req.FocusComponents.Count > 0;
    }
}
}

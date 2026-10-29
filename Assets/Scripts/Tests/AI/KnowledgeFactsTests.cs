#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DND35.AI;
using DND35.AI.Knowledge;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.AI
{
/// <summary>
/// Batch 13 step 7 (docs/designs/action_catalog_batch13.md; AI-078, AI-021): the typed facts AI target scoring reads.
/// <list type="bullet">
/// <item>Every TagPriority string a shipped profile writes (every AIProfile type's ApplyRuntimeDefaults) parses into a
/// fact query (FactQuery.Parse) without the bare-word species fallback; a string that names no fact does not.</item>
/// <item>Matching is whole: "Elf" and "Race: Elf" do not match a half-elf; "HP State: Staggered" reads the BadlyHurt wound
/// band, while Disabled and Dying stay hit point states.</item>
/// <item>The wound bands: Unhurt at full hit points, Hurt at half or more, BadlyHurt below half and above 0, Down at 0 or
/// less.</item>
/// <item>The omniscient provider reads a real creature's live state (species, creature type with its spelling
/// normalized, armor, hands, mindless; a mindless creature is not undead), and KnowledgeService.Provider can be swapped
/// and restored.</item>
/// <item>The control tags translate to AIControlFlags.</item>
/// <item>The armor fact reads the armor's typed category; spell scoring penalizes a mind-affecting spell against an
/// intelligent plant (the shared immunity rule, MM p.313).</item>
/// </list>
/// Play mode (CharacterController.Init, TST-009).
/// </summary>
public static class KnowledgeFactsTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== KNOWLEDGE FACTS TESTS (batch 13 step 7, AI-078) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();

        var made = new List<CharacterController>();
        IKnowledgeProvider before = KnowledgeService.Provider;
        try
        {
            TestShippedStringsParse();
            TestParseShapes();
            TestWoundBands();
            TestExactMatchingOnStubViews();
            TestProviderSwap();
            TestOmniscientViewOfCreature(made);
            TestControlFlags();
            TestMindAffectingImmunityScore(made);
        }
        finally
        {
            KnowledgeService.Provider = before;
            foreach (CharacterController c in made)
                if (c != null)
                    Object.DestroyImmediate(c.gameObject);
        }

        Debug.Log($"====== Knowledge Facts Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = null)
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  [PASS] {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [FAIL] {testName}" + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }

    private static void TestShippedStringsParse()
    {
        Type baseType = typeof(AIProfile);
        List<Type> types = baseType.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && baseType.IsAssignableFrom(t))
            .OrderBy(t => t.FullName)
            .ToList();
        int strings = 0;
        var unparsed = new List<string>();
        var fallback = new List<string>();
        var unknownRace = new List<string>();
        List<string> races = RaceDatabase.GetAllRaceNames() ?? new List<string>();
        foreach (Type t in types)
        {
            AIProfile p = ScriptableObject.CreateInstance(t) as AIProfile;
            if (p == null)
                continue;
            try
            {
                p.ApplyRuntimeDefaults();
                if (p.TagPriorities == null)
                    continue;
                foreach (TagPriority tp in p.TagPriorities)
                {
                    if (tp == null || string.IsNullOrWhiteSpace(tp.TagName))
                        continue;
                    strings++;
                    FactQuery q = FactQuery.Parse(tp.TagName);
                    if (q == null)
                        unparsed.Add(t.Name + ": " + tp.TagName);
                    else if (q.IsSpeciesFallback)
                        fallback.Add(t.Name + ": " + tp.TagName);
                    else if (q.Key == FactKey.Species && !races.Any(r => string.Equals(r, q.Value, StringComparison.OrdinalIgnoreCase)))
                        unknownRace.Add(t.Name + ": " + tp.TagName);
                }
            }
            finally
            {
                Object.DestroyImmediate(p);
            }
        }
        Assert(strings >= 30, "the shipped profiles write their TagPriority strings", strings + " strings over " + types.Count + " profile types");
        Assert(unparsed.Count == 0, "every shipped TagPriority string parses into a fact query", string.Join("; ", unparsed));
        // A bare word that names no fact parses as a species, so a misspelled weight would match nothing silently.
        Assert(fallback.Count == 0, "no shipped TagPriority string relies on the bare-word species fallback", string.Join("; ", fallback));
        Assert(unknownRace.Count == 0, "every shipped species query names a RaceDatabase race", string.Join("; ", unknownRace));
    }

    private static void Expect(string text, FactKey key, FactQuery.QueryKind kind, string value)
    {
        FactQuery q = FactQuery.Parse(text);
        Assert(q != null && q.Key == key && q.Kind == kind && q.Value == value,
            "'" + text + "' parses to " + kind + " " + key + (value != null ? "=" + value : ""), q != null ? q.ToString() : "null");
    }

    private static void TestParseShapes()
    {
        Expect("HP State: Staggered", FactKey.WoundBand, FactQuery.QueryKind.Equals, "BadlyHurt");
        Expect("HP State: Disabled", FactKey.HpState, FactQuery.QueryKind.Equals, "Disabled");
        Expect("HP State: Dying", FactKey.HpState, FactQuery.QueryKind.Equals, "Dying");
        Expect("Armor: Unarmored", FactKey.ArmorCategory, FactQuery.QueryKind.Equals, "Unarmored");
        Expect("Armor: Light Armor", FactKey.ArmorCategory, FactQuery.QueryKind.Equals, "Light");
        Expect("Armor: Heavy Armor", FactKey.ArmorCategory, FactQuery.QueryKind.Equals, "Heavy");
        Expect("Unarmored", FactKey.ArmorCategory, FactQuery.QueryKind.Equals, "Unarmored");
        Expect("Wielding: Unarmed", FactKey.WieldsMelee, FactQuery.QueryKind.NoManufacturedWeapon, null);
        Expect("Race: Halfling", FactKey.Species, FactQuery.QueryKind.Equals, "Halfling");
        Expect("Elf", FactKey.Species, FactQuery.QueryKind.Equals, "Elf");
        Assert(FactQuery.Parse("Elf").IsSpeciesFallback && FactQuery.Parse("Unarmoured").IsSpeciesFallback
            && !FactQuery.Parse("Race: Elf").IsSpeciesFallback && !FactQuery.Parse("Unarmored").IsSpeciesFallback,
            "a bare word that names no fact is marked as the species fallback ('Unarmoured' is one)");
        Expect("Type: MagicalBeast", FactKey.CreatureType, FactQuery.QueryKind.Equals, "Magical Beast");
        Expect("Wound: Badly Hurt", FactKey.WoundBand, FactQuery.QueryKind.Equals, "BadlyHurt");
        Expect("Condition: Prone", FactKey.Condition, FactQuery.QueryKind.Equals, "Prone");
        Assert(FactQuery.Parse("Class: Wizard") == null && FactQuery.Parse("Nonsense: Value") == null && FactQuery.Parse("  ") == null,
            "a string that names no fact does not parse (the Class: tag is gone, AI-078)");
    }

    private static void TestWoundBands()
    {
        Assert(KnowledgeFacts.WoundBandOf(20, 20) == WoundBand.Unhurt && KnowledgeFacts.WoundBandOf(25, 20) == WoundBand.Unhurt,
            "full hit points (or more, temporary) is Unhurt");
        Assert(KnowledgeFacts.WoundBandOf(19, 20) == WoundBand.Hurt && KnowledgeFacts.WoundBandOf(10, 20) == WoundBand.Hurt
            && KnowledgeFacts.WoundBandOf(5, 9) == WoundBand.Hurt, "half or more is Hurt (10/20, 5/9)");
        Assert(KnowledgeFacts.WoundBandOf(9, 20) == WoundBand.BadlyHurt && KnowledgeFacts.WoundBandOf(1, 20) == WoundBand.BadlyHurt
            && KnowledgeFacts.WoundBandOf(4, 9) == WoundBand.BadlyHurt, "below half and above 0 is BadlyHurt (9/20, 1/20, 4/9)");
        Assert(KnowledgeFacts.WoundBandOf(0, 20) == WoundBand.Down && KnowledgeFacts.WoundBandOf(-5, 20) == WoundBand.Down,
            "0 or less is Down");
    }

    /// <summary>A view with fixed facts, for matching and provider tests.</summary>
    private sealed class StubView : ITargetView
    {
        public CharacterController Observer => null;
        public CharacterController Subject => null;
        public readonly Dictionary<FactKey, FactValue> Facts = new Dictionary<FactKey, FactValue>();

        public FactValue Get(FactKey key) => Facts.TryGetValue(key, out FactValue v) ? v : FactValue.Unknown;

        public bool Has(FactKey key, string value)
        {
            FactValue f = Get(key);
            return f.IsKnown && f.Text != null && string.Equals(f.Text, value, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class StubProvider : IKnowledgeProvider
    {
        public readonly StubView View = new StubView();
        public int Calls;
        public ITargetView GetView(CharacterController observer, CharacterController subject) { Calls++; return View; }
    }

    private static void TestExactMatchingOnStubViews()
    {
        var halfElf = new StubView();
        halfElf.Facts[FactKey.Species] = FactValue.Of(KnowledgeLevel.Confirmed, "Half-Elf");
        Assert(!FactQuery.Parse("Elf").Matches(halfElf) && !FactQuery.Parse("Race: Elf").Matches(halfElf)
            && FactQuery.Parse("Race: Half-Elf").Matches(halfElf), "AI-078: 'Elf' and 'Race: Elf' do not match a half-elf; 'Race: Half-Elf' does");

        var staggered = new StubView();
        staggered.Facts[FactKey.HpState] = FactValue.Of(KnowledgeLevel.Confirmed, (int)HPState.Staggered, HPState.Staggered.ToString());
        staggered.Facts[FactKey.WoundBand] = FactValue.Of(KnowledgeLevel.Confirmed, (int)WoundBand.Hurt, WoundBand.Hurt.ToString());
        var badly = new StubView();
        badly.Facts[FactKey.HpState] = FactValue.Of(KnowledgeLevel.Confirmed, (int)HPState.Healthy, HPState.Healthy.ToString());
        badly.Facts[FactKey.WoundBand] = FactValue.Of(KnowledgeLevel.Confirmed, (int)WoundBand.BadlyHurt, WoundBand.BadlyHurt.ToString());
        FactQuery q = FactQuery.Parse("HP State: Staggered");
        Assert(q.Matches(badly) && !q.Matches(staggered), "AI-078: 'HP State: Staggered' matches a badly hurt target, not a lightly hurt staggered one");

        var empty = new StubView();
        empty.Facts[FactKey.WieldsMelee] = FactValue.Of(KnowledgeLevel.Confirmed, false);
        empty.Facts[FactKey.WieldsRanged] = FactValue.Of(KnowledgeLevel.Confirmed, false);
        var armed = new StubView();
        armed.Facts[FactKey.WieldsMelee] = FactValue.Of(KnowledgeLevel.Confirmed, true);
        armed.Facts[FactKey.WieldsRanged] = FactValue.Of(KnowledgeLevel.Confirmed, false);
        Assert(FactQuery.Parse("Wielding: Unarmed").Matches(empty) && !FactQuery.Parse("Wielding: Unarmed").Matches(armed)
            && !FactQuery.Parse("Wielding: Unarmed").Matches(new StubView()), "'Wielding: Unarmed' needs both hands known to hold no weapon");
    }

    private static void TestProviderSwap()
    {
        Assert(KnowledgeService.Provider is OmniscientKnowledgeProvider, "the default provider is the omniscient one (until batch 17)");
        var stub = new StubProvider();
        stub.View.Facts[FactKey.Species] = FactValue.Of(KnowledgeLevel.Apparent, "Elf");
        KnowledgeService.Provider = stub;
        var go = new GameObject("KnowledgeFactsTests subject");
        try
        {
            ITargetView view = KnowledgeService.ViewOf(null, go.AddComponent<CharacterController>());
            Assert(stub.Calls == 1 && ReferenceEquals(view, stub.View) && FactQuery.Parse("Race: Elf").Matches(view),
                "a swapped provider answers KnowledgeService.ViewOf");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
        KnowledgeService.Provider = null;
        Assert(KnowledgeService.Provider is OmniscientKnowledgeProvider, "a null provider restores the omniscient one");
    }

    private static void TestOmniscientViewOfCreature(List<CharacterController> made)
    {
        CharacterController observer = TestHelpers.CreateCharacter(name: "Knowledge Observer", characterClass: "Fighter", level: 1);
        CharacterController subject = TestHelpers.CreateCharacter(name: "Knowledge Subject", characterClass: "Fighter", level: 3);
        made.Add(observer);
        made.Add(subject);

        subject.SetDisplayedRace("Half-Elf");
        subject.Stats.CreatureType = "MagicalBeast";
        subject.Stats.IsMindless = true;
        ITargetView view = OmniscientKnowledgeProvider.Instance.GetView(observer, subject);
        FactValue species = view.Get(FactKey.Species);
        Assert(species.Level == KnowledgeLevel.Confirmed && species.Text == "Half-Elf", "the omniscient view gives the shown species at Confirmed", species.ToString());
        Assert(view.Has(FactKey.CreatureType, "Magical Beast") && view.Has(FactKey.CreatureType, "MagicalBeast")
            && view.Get(FactKey.CreatureType).Text == "Magical Beast", "the creature type's spelling is normalized (MagicalBeast = Magical Beast)");
        Assert(view.Get(FactKey.Mindless).Flag && !view.Has(FactKey.CreatureType, "Undead"),
            "AI-078: a mindless creature is not undead");

        subject.Stats.CurrentHP = Mathf.Max(1, subject.Stats.TotalMaxHP / 2 - 1);
        Assert(view.Get(FactKey.WoundBand).Text == "BadlyHurt", "the wound band is read live from hit points",
            subject.Stats.CurrentHP + "/" + subject.Stats.TotalMaxHP);

        WornArmor armor = KnowledgeFacts.WornArmorOf(subject);
        bool unarmoredTag = subject.Tags.HasTag("Unarmored");
        Assert((armor == WornArmor.Unarmored) == unarmoredTag, "the armor fact agrees with the Unarmored display tag", armor + " tag " + unarmoredTag);

        // The armor fact reads the typed category, not display tags: plate with no visual tags is still heavy armor.
        global::Inventory inv = subject.Inventory != null ? subject.Inventory.GetInventory() : null;
        if (inv != null)
        {
            ItemData worn = inv.ArmorRobeSlot;
            ItemData plate = ItemDatabase.CloneItem(DND35e.Identifiers.ItemID.ArmorPlate);
            try
            {
                if (plate != null)
                {
                    plate.VisualTags.Clear();
                    inv.ArmorRobeSlot = plate;
                }
                Assert(plate != null && plate.ArmorCat == ArmorCategory.Heavy && KnowledgeFacts.WornArmorOf(subject) == WornArmor.Heavy
                    && view.Has(FactKey.ArmorCategory, "Heavy"), "armor with its category set and no visual tags is read by its category (Heavy)",
                    KnowledgeFacts.WornArmorOf(subject).ToString());
                inv.ArmorRobeSlot = null;
                Assert(KnowledgeFacts.WornArmorOf(subject) == WornArmor.Unarmored, "an empty armor slot is Unarmored");
            }
            finally
            {
                inv.ArmorRobeSlot = worn;
            }
        }
        else
            Assert(false, "the subject has an inventory");
        Assert(view.Get(FactKey.WieldsMelee).IsKnown && view.Get(FactKey.ShieldCarried).IsKnown, "the hand facts are known");
        Assert(!subject.Tags.GetAllTags().Any(t => t.StartsWith("Class: ", StringComparison.OrdinalIgnoreCase)),
            "the unread Class: tag is gone (batch 13 step 7)");
    }

    /// <summary>
    /// The AI's single-target spell scoring penalizes a mind-affecting spell against every creature the shared rule
    /// (CharacterStats.IsImmuneToMindAffecting) makes immune: an intelligent plant (MM p.313) as well as the mindless.
    /// </summary>
    private static void TestMindAffectingImmunityScore(List<CharacterController> made)
    {
        System.Reflection.MethodInfo score = typeof(AISpellcastingStrategist).GetMethod("GetEnemyTypeScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert(score != null, "AISpellcastingStrategist.GetEnemyTypeScore exists (reflection)");
        if (score == null)
            return;
        CharacterController caster = TestHelpers.CreateCharacter(name: "Knowledge Caster", characterClass: "Wizard", level: 3);
        CharacterController plant = TestHelpers.CreateCharacter(name: "Knowledge Plant", characterClass: "Fighter", level: 3, intelligence: 12);
        CharacterController human = TestHelpers.CreateCharacter(name: "Knowledge Human", characterClass: "Fighter", level: 3);
        made.Add(caster);
        made.Add(plant);
        made.Add(human);
        plant.Stats.CreatureType = "Plant";
        plant.Stats.IsMindless = false;
        var charm = new SpellData { Name = "Test Charm", IsMindAffecting = true, EffectType = SpellEffectType.Debuff };
        float plantScore = (float)score.Invoke(null, new object[] { caster, plant, charm });
        float humanScore = (float)score.Invoke(null, new object[] { caster, human, charm });
        Assert(plantScore <= -50f && humanScore == 0f, "a mind-affecting spell scores -50 against an intelligent plant, 0 against a humanoid",
            "plant " + plantScore + " humanoid " + humanScore);
    }

    private static void TestControlFlags()
    {
        Assert(AIControlFlagRules.FromTag("AI:MagicMissileOnly") == AIControlFlags.MagicMissileOnly
            && AIControlFlagRules.FromTag(" ai:magicmissileonly ") == AIControlFlags.MagicMissileOnly, "'AI:MagicMissileOnly' sets MagicMissileOnly");
        Assert(AIControlFlagRules.FromTag("Uses Armor-Based Targeting") == AIControlFlags.ArmorPriorityTargeting, "the armor-targeting tag sets ArmorPriorityTargeting");
        Assert(AIControlFlagRules.FromTag("ScenarioOverride:OgreBattleAlly") == AIControlFlags.ScenarioOverride
            && AIControlFlagRules.FromTag("ShieldBashTestSimpleMeleeAI") == AIControlFlags.ScenarioOverride, "scenario override tags set ScenarioOverride");
        Assert(AIControlFlagRules.FromTags(new[] { "Humanoid", "Arcane", "AI:MagicMissileOnly" }) == AIControlFlags.MagicMissileOnly
            && AIControlFlagRules.FromTag("Undead") == AIControlFlags.None, "creature tags that are not control tags set nothing");

        NPCDefinition adept = NPCDatabase.Get("arcane_missile_adept");
        Assert(adept != null && AIControlFlagRules.FromTags(adept.CreatureTags) == AIControlFlags.MagicMissileOnly,
            "arcane_missile_adept's definition carries the MagicMissileOnly control tag");
    }
}
}
#endif

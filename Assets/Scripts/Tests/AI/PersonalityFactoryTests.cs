#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using DND35.AI.Knowledge;
using DND35.AI.Personality;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 4 (docs/designs/personalities_batch14.md step 4, C11-C13, Q6 and Q7 defaults): the personality data.
/// <list type="bullet">
/// <item>Only goblin_adept, orc_sadist and hobgoblin_captain carry an AIPersonality (Q7 default); their specs, Roles,
/// rules data (the adept: Adept 1 with no armor or shield, DMG p.107; the sadist: the orc warrior's rules; the captain:
/// Fighter 3 with Int 13, the Keen tier) and the personalities the factory builds for them.</item>
/// <item>The adept's Healer trait warns that the creature has no castable spell (C1) and is kept; with a spell it does
/// not warn. An empty spec gives no personality and no warning.</item>
/// <item>NPCDefinition.Clone copies the spec without touching the template.</item>
/// <item>The three encounter presets name the three creatures.</item>
/// <item>The difficulty source (C11): AIService.Difficulty feeds AiBlend.SettingsDifficulty in Play mode, so Easy blocks a
/// sadistic coup de grace and Normal allows it; in edit mode (no GameManager) the same through a test source.</item>
/// </list>
/// Either mode; the difficulty checks use the scene's AIService in Play mode.
/// </summary>
public static class PersonalityFactoryTests
{
    private static int _passed;
    private static int _failed;

    private static readonly string[] TraitCreatures = { "goblin_adept", "orc_sadist", "hobgoblin_captain" };

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        NPCDatabase.Init();
        Debug.Log("====== PERSONALITY FACTORY TESTS (batch 14 step 4) ======");

        TestOnlyTheThreeCarryTraits();
        TestGoblinAdept();
        TestOrcSadist();
        TestHobgoblinCaptain();
        TestEmptySpec();
        TestCloneCopiesSpec();
        TestPresets();
        TestDifficultySource();

        Debug.Log($"====== Personality Factory Results: {_passed} passed, {_failed} failed ======");
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

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 1e-4f;

    private static CognitionTier TierOf(NPCDefinition def) => CognitionTiers.FromIntelligence(def.INT, def.IsMindless);

    private static PersonalityProfile Build(NPCDefinition def, List<string> warnings, bool? hasCastableSpell)
        => PersonalityFactory.Create(def.AIPersonality, TierOf(def), def.CharacterAlignment, def.Id, warnings.Add, hasCastableSpell);

    private static string Describe(PersonalityProfile p)
        => p == null ? "null" : string.Join(",", p.Traits.Select(t => t.Trait.Id + ":" + t.Weight.ToString("0.##")));

    private static void TestOnlyTheThreeCarryTraits()
    {
        var carriers = NPCDatabase.AllNPCs.Where(d => d != null && !string.IsNullOrWhiteSpace(d.AIPersonality)).Select(d => d.Id).OrderBy(id => id).ToList();
        Assert(carriers.SequenceEqual(TraitCreatures.OrderBy(id => id)),
            "Q7 default: only goblin_adept, orc_sadist and hobgoblin_captain carry a personality", string.Join(",", carriers));
    }

    private static void TestGoblinAdept()
    {
        NPCDefinition def = NPCDatabase.Get("goblin_adept");
        Assert(def != null, "goblin_adept is registered");
        if (def == null)
            return;
        Assert(def.AIPersonality == "Cowardly:0.6,Healer:0.4", "goblin_adept: personality Cowardly:0.6,Healer:0.4", def.AIPersonality);
        Assert(def.AIProfileArchetype == NPCAIProfileArchetype.Healer && def.AIBehavior == NPCAIBehavior.RangedKiter,
            "goblin_adept: archetype Healer, behaviour RangedKiter");
        Assert(def.CharacterClass == "Adept" && def.Level == 1 && def.HitDice == 1 && def.BaseHitDieHP == 4,
            "goblin_adept: Adept 1, one Hit Die, 1d6+1 = 4 hp");
        Assert(def.EquipmentIds.All(e => e.Slot != EquipSlot.Armor && e.Slot != EquipSlot.LeftHand)
               && def.EquipmentIds.Any(e => e.ItemId == ItemIDs.MORNINGSTAR) && def.EquipmentIds.Any(e => e.ItemId == ItemIDs.SLING),
            "goblin_adept: no armor or shield (DMG p.107), a morningstar and a sling");
        Assert(def.KnownSpellIds == null || def.KnownSpellIds.Count == 0, "goblin_adept: no spells until batch 15 (CHR-024, ENC-021)");

        var warnings = new List<string>();
        PersonalityProfile p = Build(def, warnings, hasCastableSpell: false);
        Assert(p != null && p.Traits.Count == 2 && Near(p.WeightOf("Cowardly"), 0.6f) && Near(p.WeightOf("Healer"), 0.4f),
            "goblin_adept: the factory keeps Cowardly .6 and Healer .4 (Int 10, Average tier)", Describe(p));
        Assert(warnings.Count == 1 && warnings[0].Contains("Healer needs a castable spell") && warnings[0].Contains("goblin_adept"),
            "C1: with no castable spell, one warning that Healer needs one; the trait is kept", string.Join(" | ", warnings));

        warnings.Clear();
        PersonalityProfile withSpell = Build(def, warnings, hasCastableSpell: true);
        Assert(withSpell != null && warnings.Count == 0, "C1: with a castable spell, no warning", string.Join(" | ", warnings));
    }

    private static void TestOrcSadist()
    {
        NPCDefinition def = NPCDatabase.Get("orc_sadist");
        NPCDefinition orc = NPCDatabase.Get("orc_warrior");
        Assert(def != null && orc != null, "orc_sadist and orc_warrior are registered");
        if (def == null || orc == null)
            return;
        Assert(def.AIPersonality == "Sadistic:0.7,Berserker:0.3", "orc_sadist: personality Sadistic:0.7,Berserker:0.3", def.AIPersonality);
        Assert(string.IsNullOrEmpty(orc.AIPersonality), "orc_warrior keeps no personality (the template was not mutated)");
        bool sameRules = def.STR == orc.STR && def.DEX == orc.DEX && def.CON == orc.CON && def.INT == orc.INT && def.WIS == orc.WIS
                         && def.CHA == orc.CHA && def.BaseHitDieHP == orc.BaseHitDieHP && def.ChallengeRating == orc.ChallengeRating
                         && def.CharacterClass == orc.CharacterClass && def.CharacterAlignment == orc.CharacterAlignment
                         && def.AIProfileArchetype == orc.AIProfileArchetype && def.AIBehavior == orc.AIBehavior
                         && def.EquipmentIds.Select(e => e.ItemId).SequenceEqual(orc.EquipmentIds.Select(e => e.ItemId))
                         && def.EquipmentIds != orc.EquipmentIds;
        Assert(sameRules, "orc_sadist: the orc warrior's rules, Role and gear (its own copy of the lists)");

        var warnings = new List<string>();
        PersonalityProfile p = Build(def, warnings, hasCastableSpell: false);
        Assert(p != null && p.Traits.Count == 2 && Near(p.WeightOf("Sadistic"), 0.7f) && Near(p.WeightOf("Berserker"), 0.3f) && warnings.Count == 0,
            "orc_sadist: Sadistic .7 and Berserker .3, no warning (chaotic evil fits Sadistic's evil hint)",
            Describe(p) + " / " + string.Join(" | ", warnings));
    }

    private static void TestHobgoblinCaptain()
    {
        NPCDefinition def = NPCDatabase.Get("hobgoblin_captain");
        Assert(def != null, "hobgoblin_captain is registered");
        if (def == null)
            return;
        Assert(def.AIPersonality == "Smart:0.5", "hobgoblin_captain: personality Smart:0.5", def.AIPersonality);
        Assert(def.CharacterClass == "Fighter" && def.Level == 3 && def.INT == 13 && TierOf(def) == CognitionTier.Keen,
            "hobgoblin_captain: Fighter 3, Int 13 (Keen)");
        Assert(def.CharacterAlignment == Alignment.LawfulEvil && def.CreatureType == "Humanoid", "hobgoblin_captain: lawful evil humanoid");
        Assert(def.AIProfileArchetype == NPCAIProfileArchetype.Humanoid && def.AIBehavior == NPCAIBehavior.AggressiveMelee,
            "hobgoblin_captain: archetype Humanoid, behaviour AggressiveMelee (not the defensive routine, AI-049)");
        Assert(def.Feats.Count == 4 && def.Feats.Contains("Combat Expertise"),
            "hobgoblin_captain: four feats for Fighter 3 (PHB p.37-38), Combat Expertise with Int 13 (PHB p.92)", string.Join(",", def.Feats));

        var warnings = new List<string>();
        PersonalityProfile p = Build(def, warnings, hasCastableSpell: false);
        Assert(p != null && p.Traits.Count == 1 && Near(p.WeightOf("Smart"), 0.5f) && Near(p.TotalWeight, 0.5f) && warnings.Count == 0,
            "hobgoblin_captain: Smart at half strength (C2: the shortfall stays with the Role), no warning",
            Describe(p) + " / " + string.Join(" | ", warnings));
    }

    private static void TestEmptySpec()
    {
        var warnings = new List<string>();
        Assert(PersonalityFactory.Create(null, CognitionTier.Average, Alignment.NeutralEvil, "x", warnings.Add, false) == null
               && PersonalityFactory.Create("  ", CognitionTier.Average, Alignment.NeutralEvil, "x", warnings.Add, false) == null
               && warnings.Count == 0,
            "an empty spec: no personality and no warning (every creature without traits)");
    }

    private static void TestCloneCopiesSpec()
    {
        NPCDefinition def = NPCDatabase.Get("goblin_adept");
        if (def == null)
        {
            Assert(false, "goblin_adept is registered for the clone test");
            return;
        }
        NPCDefinition clone = def.Clone();
        Assert(clone.AIPersonality == def.AIPersonality, "NPCDefinition.Clone copies AIPersonality");
        clone.AIPersonality = "Smart:1";
        Assert(def.AIPersonality == "Cowardly:0.6,Healer:0.4", "changing a clone's spec leaves the template's alone");
    }

    private static void TestPresets()
    {
        List<EncounterPreset> presets = NPCDatabase.ListEncounterPresets();
        EncounterPreset P(string id) => presets.FirstOrDefault(x => x.Id == id);
        EncounterPreset a = P("ai_personality_test");
        EncounterPreset b = P("ai_sadist_fallen");
        EncounterPreset c = P("ai_captain_focus");
        Assert(a != null && a.NPCIds.SequenceEqual(new[] { "goblin_adept", "orc_sadist", "hobgoblin_captain" }),
            "preset ai_personality_test: the three trait creatures");
        Assert(b != null && b.NPCIds.Count > 0 && b.NPCIds.All(id => id == "orc_sadist"), "preset ai_sadist_fallen: orc sadists");
        Assert(c != null && c.NPCIds.Contains("hobgoblin_captain"), "preset ai_captain_focus: the hobgoblin captain");
        bool resolve = new[] { a, b, c }.Where(x => x != null).SelectMany(x => x.NPCIds).All(id => NPCDatabase.Get(id) != null);
        Assert(resolve, "every creature of the three presets resolves (ENC-012)");
    }

    /// <summary>C11: a sadistic coup de grace (Insist, MinDifficulty Normal) reads the game's difficulty when the context leaves it null.</summary>
    private static void TestDifficultySource()
    {
        PersonalityProfile sadist = PersonalityFactory.Create("Sadistic", CognitionTier.Average, Alignment.ChaoticEvil, "test_sadist", _ => { });
        var ctx = new JudgeContext();
        AIService ai = GameManager.Instance != null ? GameManager.Instance.GetComponent<AIService>() : null;
        if (ai != null)
        {
            AIService.AIDifficultyLevel? savedOverride = ai.DifficultyOverride;
            GameSettings settings = GameSettings.Instance;
            AIService.AIDifficultyLevel savedSetting = settings.aiDifficulty;
            try
            {
                Assert(AiBlend.SettingsDifficulty != null && AiBlend.SettingsDifficulty() == ai.Difficulty,
                    "Play mode: AiBlend.SettingsDifficulty reads AIService.Difficulty");
                // With no override the setting is read live, so a later write to GameSettings.aiDifficulty (a future menu) applies.
                ai.DifficultyOverride = null;
                settings.aiDifficulty = AIService.AIDifficultyLevel.Easy;
                Assert(ai.Difficulty == AIService.AIDifficultyLevel.Easy && AiBlend.SettingsDifficulty() == AIService.AIDifficultyLevel.Easy,
                    "no override: AIService.Difficulty follows a later GameSettings.aiDifficulty write");
                settings.aiDifficulty = savedSetting;
                ai.Difficulty = AIService.AIDifficultyLevel.Easy;
                Assert(!AiBlend.Judge(sadist, BoolDecision.CoupDeGrace, false, ctx), "AIService.Difficulty Easy: the sadistic coup de grace is blocked");
                ai.Difficulty = AIService.AIDifficultyLevel.Normal;
                Assert(AiBlend.Judge(sadist, BoolDecision.CoupDeGrace, false, ctx), "AIService.Difficulty Normal: the sadistic coup de grace is taken");
            }
            finally
            {
                settings.aiDifficulty = savedSetting;
                ai.DifficultyOverride = savedOverride;
            }
            return;
        }

        var savedSource = AiBlend.SettingsDifficulty;
        try
        {
            AiBlend.SettingsDifficulty = null;
            Assert(AiBlend.Judge(sadist, BoolDecision.CoupDeGrace, false, ctx), "edit mode, no difficulty source: Normal, the coup de grace is taken");
            AiBlend.SettingsDifficulty = () => AIService.AIDifficultyLevel.Easy;
            Assert(!AiBlend.Judge(sadist, BoolDecision.CoupDeGrace, false, ctx), "edit mode, an Easy source: the sadistic coup de grace is blocked");
            AiBlend.SettingsDifficulty = () => AIService.AIDifficultyLevel.Normal;
            Assert(AiBlend.Judge(sadist, BoolDecision.CoupDeGrace, false, ctx), "edit mode, a Normal source: the sadistic coup de grace is taken");
            // The Play branch's live-setting assert has no AIService here; Hard (plays as Normal) keeps the counts equal.
            AiBlend.SettingsDifficulty = () => AIService.AIDifficultyLevel.Hard;
            Assert(AiBlend.Judge(sadist, BoolDecision.CoupDeGrace, false, ctx), "edit mode, a Hard source: the sadistic coup de grace is taken");
        }
        finally
        {
            AiBlend.SettingsDifficulty = savedSource;
        }
    }
}
}
#endif

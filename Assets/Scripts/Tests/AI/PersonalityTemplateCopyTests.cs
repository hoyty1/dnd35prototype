#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using DND35.AI.Knowledge;
using DND35.AI.Personality;
using UnityEngine;

namespace Tests.AI
{
/// <summary>
/// Batch 14 step 4 (CRE-023 pattern; docs/designs/personalities_batch14.md C13): NPCDefinition.AIPersonality survives
/// the in-place zombie, skeleton and lycanthrope templates (each wrapper's CopyDefinitionFields copies it) and the
/// registry's ApplyTemplatesClone, without touching the database entry. A template attaches no trait of its own, and the
/// mindless undead it makes keep no trait above their tier: the orc sadist's zombie and skeleton get no personality
/// (with one warning), its werewolf (Int unchanged) keeps Sadistic and Berserker.
/// Either mode (definitions only, no creature).
/// </summary>
public static class PersonalityTemplateCopyTests
{
    private static int _passed;
    private static int _failed;

    private const string Spec = "Sadistic:0.7,Berserker:0.3";

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        NPCDatabase.Init();
        Debug.Log("====== PERSONALITY TEMPLATE COPY TESTS (batch 14 step 4, CRE-023) ======");

        TestTemplate("zombie", expectMindless: true);
        TestTemplate("skeleton", expectMindless: true);
        TestTemplate("werewolf", expectMindless: false);
        TestRegistryClone();

        Debug.Log($"====== Personality Template Copy Results: {_passed} passed, {_failed} failed ======");
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

    private static NPCDefinition SadistClone()
    {
        NPCDefinition def = NPCDatabase.Get("orc_sadist");
        return def != null ? def.Clone() : null;
    }

    private static ICreatureTemplate Template(string id)
    {
        var probe = new NPCDefinition { AppliedTemplateIds = new List<string> { id } };
        return CreatureTemplateRegistry.ResolveTemplates(probe).FirstOrDefault();
    }

    private static void TestTemplate(string templateId, bool expectMindless)
    {
        NPCDefinition def = SadistClone();
        ICreatureTemplate template = Template(templateId);
        Assert(def != null && template != null, templateId + ": orc_sadist and the template exist");
        if (def == null || template == null)
            return;

        string beforeType = def.CreatureType;
        int beforeStr = def.STR, beforeWis = def.WIS, beforeTags = def.CreatureTags.Count, beforeSpecials = def.SpecialAbilities.Count;
        template.ApplyToDefinition(def);
        bool applied = def.CreatureType != beforeType || def.Name != "Orc Sadist" || def.STR != beforeStr || def.WIS != beforeWis
                       || def.CreatureTags.Count != beforeTags || def.SpecialAbilities.Count != beforeSpecials;
        Assert(applied, templateId + ": the template changed the orc sadist", def.Name + " / " + def.CreatureType + " / Str " + def.STR + " Wis " + def.WIS);
        Assert(def.AIPersonality == Spec, templateId + ": CopyDefinitionFields keeps AIPersonality (CRE-023)", def.AIPersonality);
        Assert(NPCDatabase.Get("orc_sadist").AIPersonality == Spec && NPCDatabase.Get("orc_sadist").CreatureType == "Humanoid",
            templateId + ": the database entry is untouched");

        var warnings = new List<string>();
        CognitionTier tier = CognitionTiers.FromIntelligence(def.INT, def.IsMindless);
        PersonalityProfile p = PersonalityFactory.Create(def.AIPersonality, tier, def.CharacterAlignment, def.Id, warnings.Add, false);
        if (expectMindless)
            Assert(tier == CognitionTier.Mindless && p == null && warnings.Count == 1,
                templateId + ": a mindless result keeps no trait above its tier, so no personality, with one warning (C13)",
                tier + " / " + (p == null ? "null" : p.Source) + " / " + string.Join(" | ", warnings));
        else
            Assert(tier != CognitionTier.Mindless && p != null && p.WeightOf("Sadistic") > 0f && p.WeightOf("Berserker") > 0f,
                templateId + ": an intelligent result keeps Sadistic and Berserker", tier + " / " + string.Join(" | ", warnings));
    }

    private static void TestRegistryClone()
    {
        NPCDefinition def = SadistClone();
        if (def == null)
        {
            Assert(false, "orc_sadist exists for the registry clone");
            return;
        }
        def.AppliedTemplateIds = new List<string> { "zombie" };
        NPCDefinition result = CreatureTemplateRegistry.ApplyTemplatesClone(def);
        Assert(result != null && result != def && result.AIPersonality == Spec && CreatureTemplateRegistry.IsTemplateBaked(result, "zombie"),
            "ApplyTemplatesClone: the zombie clone keeps AIPersonality");
    }
}
}
#endif

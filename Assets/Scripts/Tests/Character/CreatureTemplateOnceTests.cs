using System.Collections.Generic;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// CRE-001: a template is added to a base creature once. The MM describes a template as one set of changes to a base
/// creature's statistics, and several templates are applied one at a time (MM p.291, p.293); none is applied twice.
/// Skeleton, zombie and lycanthrope entries built by their factories already carry their template's changes and record
/// it in <c>NPCDefinition.BakedTemplateIds</c>; <c>CreatureTemplateRegistry.ApplyTemplatesClone</c>, which the encounter
/// spawn (<c>GameManager.BuildEncounterDefinitionForSpawn</c>), the dungeon spawner and summons all call, skips baked
/// templates and bakes the ones it applies, so a second call changes nothing. MM figures: human warrior skeleton
/// Dex 13 and 1 HD (p.226), bugbear zombie 6 HD, Str 17, Dex 10 (p.267). PCs and NPCs share this one data path.
/// Pure data: no scene needed. Database templates are cloned before any change and checked unchanged afterwards.
/// </summary>
public static class CreatureTemplateOnceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        NPCDatabase.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();

        Debug.Log("====== CREATURE TEMPLATE APPLIED ONCE TESTS (CRE-001) ======");

        TestFactoryEntriesAreNotTemplatedAgain();
        TestMonsterManualFigures();
        TestRequestedTemplatesApplyOnce();
        TestRefusedTemplateIsNotBaked();
        TestCloneCopiesBakedTemplates();
        TestFactoryKeepsEarlierTemplates();

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
            Debug.LogError($"  FAIL: {testName}" + (string.IsNullOrEmpty(detail) ? "" : $" ({detail})"));
        }
    }

    /// <summary>The templates whose factories build the baked database entries (SkeletonTemplate, ZombieTemplate, LycanthropeTemplate).</summary>
    private static readonly string[] FactoryTemplateIds = { "skeleton", "zombie", "lycanthrope" };

    /// <summary>The template-relevant statistics of a definition, for before/after comparisons.</summary>
    private static string Fingerprint(NPCDefinition d)
    {
        if (d == null)
            return "null";
        string resist = "";
        if (d.DamageResistances != null)
            foreach (DamageResistanceEntry e in d.DamageResistances)
                if (e != null)
                    resist += e.Type + ":" + e.Amount + ";";
        return $"{d.Name}|HD {d.HitDice}|Lv {d.Level}|S{d.STR} D{d.DEX} C{d.CON} I{d.INT} W{d.WIS} Ch{d.CHA}"
               + $"|hp {d.BaseHitDieHP}|NA {d.NaturalArmorBonus}|BAB {d.BAB}|DR {d.DamageReductionAmount}/{d.DamageReductionBypass}"
               + $"|SR {d.SpellResistance}|type {d.CreatureType}|size {d.SizeCategory}|align {d.CharacterAlignment}|CR {d.ChallengeRating}"
               + $"|feats {(d.Feats != null ? string.Join(",", d.Feats) : "")}|attacks {(d.NaturalAttacks != null ? d.NaturalAttacks.Count : 0)}"
               + $"|resist {resist}|abilities {(d.SpecialAbilities != null ? d.SpecialAbilities.Count : 0)}"
               + $"|templates {(d.AppliedTemplateIds != null ? string.Join(",", d.AppliedTemplateIds) : "")}"
               + $"|baked {(d.BakedTemplateIds != null ? string.Join(",", d.BakedTemplateIds) : "")}";
    }

    /// <summary>Every factory-built skeleton, zombie and lycanthrope entry passes through the spawn-time registry unchanged.</summary>
    private static void TestFactoryEntriesAreNotTemplatedAgain()
    {
        Debug.Log("--- Factory-built templated entries are not templated again at spawn ---");
        int skeletons = 0, zombies = 0, lycanthropes = 0;
        var changed = new List<string>();
        var dbChanged = new List<string>();
        var notBaked = new List<string>();
        foreach (NPCDefinition entry in new List<NPCDefinition>(NPCDatabase.AllNPCs))
        {
            if (entry == null)
                continue;
            // Every entry that names a factory template (in either list) must record it as baked: such an entry carries
            // the template's changes, and an unbaked id would make the spawn apply them again.
            bool factoryTemplated = false;
            foreach (string id in FactoryTemplateIds)
            {
                bool applied = entry.AppliedTemplateIds != null && entry.AppliedTemplateIds.Contains(id);
                bool baked = CreatureTemplateRegistry.IsTemplateBaked(entry, id);
                if (!applied && !baked)
                    continue;
                factoryTemplated = true;
                if (!baked)
                    notBaked.Add(entry.Id + ":" + id);
            }
            if (!factoryTemplated)
                continue;
            if (CreatureTemplateRegistry.IsTemplateBaked(entry, "skeleton")) skeletons++;
            if (CreatureTemplateRegistry.IsTemplateBaked(entry, "zombie")) zombies++;
            if (CreatureTemplateRegistry.IsTemplateBaked(entry, "lycanthrope")) lycanthropes++;

            string before = Fingerprint(entry);
            NPCDefinition spawned = CreatureTemplateRegistry.ApplyTemplatesClone(entry);
            if (Fingerprint(spawned) != before)
                changed.Add(entry.Id + ": " + before + " -> " + Fingerprint(spawned));
            if (Fingerprint(entry) != before)
                dbChanged.Add(entry.Id);
        }

        Assert(skeletons >= 8 && zombies >= 7 && lycanthropes >= 8,
            "the database holds factory-built skeletons, zombies and lycanthropes that record their template as baked",
            $"skeletons {skeletons}, zombies {zombies}, lycanthropes {lycanthropes}");
        Assert(notBaked.Count == 0,
            "every entry that names the skeleton, zombie or lycanthrope template records it as baked",
            string.Join(", ", notBaked));
        Assert(changed.Count == 0,
            $"ApplyTemplatesClone leaves every factory-built entry unchanged ({skeletons + zombies + lycanthropes} checked; CRE-001)",
            string.Join(" || ", changed));
        Assert(dbChanged.Count == 0, "the database entries themselves are not mutated", string.Join(", ", dbChanged));
    }

    /// <summary>The spawned definition shows the MM figures of a single template application.</summary>
    private static void TestMonsterManualFigures()
    {
        Debug.Log("--- MM figures after the spawn-time registry ---");
        NPCDefinition skeleton = CreatureTemplateRegistry.ApplyTemplatesClone(NPCDatabase.Get("skeleton_human_warrior"));
        Assert(skeleton != null && skeleton.DEX == 13 && skeleton.HitDice == 1,
            "human warrior skeleton: Dex 13 (11 + 2 once) and 1 HD (MM p.226)",
            skeleton != null ? "Dex " + skeleton.DEX + ", HD " + skeleton.HitDice : "missing");

        NPCDefinition zombie = CreatureTemplateRegistry.ApplyTemplatesClone(NPCDatabase.Get("zombie_bugbear"));
        Assert(zombie != null && zombie.HitDice == 6 && zombie.STR == 17 && zombie.DEX == 10,
            "bugbear zombie: 6 HD (3 racial HD doubled once), Str 17, Dex 10 (MM p.267)",
            zombie != null ? "HD " + zombie.HitDice + ", Str " + zombie.STR + ", Dex " + zombie.DEX : "missing");

        NPCDefinition again = CreatureTemplateRegistry.ApplyTemplatesClone(zombie);
        Assert(again != null && again.HitDice == 6,
            "the bugbear zombie keeps 6 HD however often the registry runs", again != null ? "HD " + again.HitDice : "missing");
    }

    /// <summary>
    /// A requested template (an id in AppliedTemplateIds that is not baked yet, as fiendish_wolf and the dungeon rows
    /// carry) is applied by the first registry call and recorded as baked; a second call, as the encounter spawn makes
    /// for a dungeon spawn definition, changes nothing.
    /// </summary>
    private static void TestRequestedTemplatesApplyOnce()
    {
        Debug.Log("--- Requested templates apply once ---");
        int checkedEntries = 0;
        var notIdempotent = new List<string>();
        var notBaked = new List<string>();
        foreach (NPCDefinition entry in new List<NPCDefinition>(NPCDatabase.AllNPCs))
        {
            if (entry == null || entry.AppliedTemplateIds == null || entry.AppliedTemplateIds.Count == 0)
                continue;
            checkedEntries++;
            NPCDefinition once = CreatureTemplateRegistry.ApplyTemplatesClone(entry);
            NPCDefinition twice = CreatureTemplateRegistry.ApplyTemplatesClone(once);
            if (Fingerprint(once) != Fingerprint(twice))
                notIdempotent.Add(entry.Id + ": " + Fingerprint(once) + " -> " + Fingerprint(twice));
            foreach (ICreatureTemplate t in CreatureTemplateRegistry.ResolveTemplates(once))
                if (!CreatureTemplateRegistry.IsTemplateBaked(once, t.TemplateId))
                    notBaked.Add(entry.Id + ":" + t.TemplateId);
        }
        Assert(checkedEntries > 0 && notIdempotent.Count == 0,
            $"a second registry call changes no templated database entry ({checkedEntries} checked)", string.Join(" || ", notIdempotent));
        Assert(notBaked.Count == 0, "every registered template on a spawned definition is recorded as baked", string.Join(", ", notBaked));

        NPCDefinition baseWolf = NPCDatabase.Get("wolf_pack_hunter");
        NPCDefinition fiendishWolf = NPCDatabase.Get("fiendish_wolf");
        if (baseWolf == null || fiendishWolf == null)
        {
            Assert(false, "wolf_pack_hunter and fiendish_wolf exist in NPCDatabase");
            return;
        }
        Assert(!CreatureTemplateRegistry.IsTemplateBaked(fiendishWolf, "fiendish"),
            "fiendish_wolf records the fiendish template as a request, not yet baked");
        NPCDefinition wolfOnce = CreatureTemplateRegistry.ApplyTemplatesClone(fiendishWolf);
        NPCDefinition wolfTwice = CreatureTemplateRegistry.ApplyTemplatesClone(wolfOnce);
        NPCDefinition expected = baseWolf.Clone();
        expected.AppliedTemplateIds = new List<string> { "fiendish" };
        expected = CreatureTemplateRegistry.ApplyTemplatesClone(expected);
        Assert(wolfOnce != null && wolfTwice != null && expected != null
               && wolfOnce.CHA == expected.CHA && wolfTwice.CHA == expected.CHA
               && wolfTwice.DEX == expected.DEX && wolfTwice.STR == expected.STR
               && wolfTwice.GainsSmiteGood && CreatureTemplateRegistry.IsTemplateBaked(wolfOnce, "fiendish"),
            "fiendish wolf: the fiendish changes appear once, the same as one application to the base wolf (MM p.108)",
            wolfTwice != null && expected != null ? $"once Cha {wolfOnce.CHA}, twice Cha {wolfTwice.CHA}, one application Cha {expected.CHA}" : "");
    }

    /// <summary>A refused template (an illegal base, owner ruling 2026-10-11, CRE-057) is neither kept nor baked.</summary>
    private static void TestRefusedTemplateIsNotBaked()
    {
        Debug.Log("--- A refused template is not baked ---");
        NPCDefinition ogre = NPCDatabase.Get("ogre");
        if (ogre == null)
        {
            Assert(false, "ogre exists in NPCDatabase");
            return;
        }
        NPCDefinition request = ogre.Clone();
        request.AppliedTemplateIds = new List<string> { "celestial" };
        NPCDefinition result = CreatureTemplateRegistry.ApplyTemplatesClone(request);
        Assert(result != null && !CreatureTemplateRegistry.IsTemplateBaked(result, "celestial")
               && (result.AppliedTemplateIds == null || !result.AppliedTemplateIds.Contains("celestial")),
            "a celestial template refused on the CE ogre (MM p.31) is not recorded as baked",
            result != null ? "baked " + string.Join(",", result.BakedTemplateIds ?? new List<string>()) : "null");
    }

    /// <summary>NPCDefinition.Clone copies the baked list as its own list (never the database's).</summary>
    private static void TestCloneCopiesBakedTemplates()
    {
        Debug.Log("--- Clone copies BakedTemplateIds ---");
        NPCDefinition skeleton = NPCDatabase.Get("skeleton_wolf");
        if (skeleton == null)
        {
            Assert(false, "skeleton_wolf exists in NPCDatabase");
            return;
        }
        NPCDefinition clone = skeleton.Clone();
        Assert(clone.BakedTemplateIds != null && clone.BakedTemplateIds.Contains("skeleton")
               && !ReferenceEquals(clone.BakedTemplateIds, skeleton.BakedTemplateIds),
            "Clone copies BakedTemplateIds into a new list");
    }

    /// <summary>
    /// A factory template adds its ids to the base's lists instead of replacing them, so a template applied earlier
    /// (here the fiendish template of a spawned fiendish wolf) stays in AppliedTemplateIds and BakedTemplateIds, both
    /// through the factory and through the registry adapter, and is not applied again. Bookkeeping only: whether a
    /// skeleton may be made of a fiendish creature and in which order is CRE-064.
    /// </summary>
    private static void TestFactoryKeepsEarlierTemplates()
    {
        Debug.Log("--- A factory template keeps the templates applied before it ---");
        NPCDefinition fiendishWolf = NPCDatabase.Get("fiendish_wolf");
        if (fiendishWolf == null)
        {
            Assert(false, "fiendish_wolf exists in NPCDatabase");
            return;
        }
        NPCDefinition fiendishOnce = CreatureTemplateRegistry.ApplyTemplatesClone(fiendishWolf);
        string fiendishBefore = Fingerprint(fiendishOnce);

        NPCDefinition factory = SkeletonTemplate.Apply(fiendishOnce, "test_fiendish_wolf_skeleton", "Test Skeleton", hasHands: false);
        bool factoryKeeps = factory != null
            && CreatureTemplateRegistry.IsTemplateBaked(factory, "fiendish") && CreatureTemplateRegistry.IsTemplateBaked(factory, "skeleton")
            && factory.AppliedTemplateIds.Contains("fiendish") && factory.AppliedTemplateIds.Contains("skeleton");
        Assert(factoryKeeps, "SkeletonTemplate.Apply keeps the base's applied and baked fiendish template and adds skeleton",
            factory != null ? "applied " + string.Join(",", factory.AppliedTemplateIds) + ", baked " + string.Join(",", factory.BakedTemplateIds) : "null");

        NPCDefinition request = fiendishOnce.Clone();
        request.AppliedTemplateIds.Add("skeleton");
        NPCDefinition registry = CreatureTemplateRegistry.ApplyTemplatesClone(request);
        bool registryKeeps = registry != null
            && CreatureTemplateRegistry.IsTemplateBaked(registry, "fiendish") && CreatureTemplateRegistry.IsTemplateBaked(registry, "skeleton")
            && registry.AppliedTemplateIds.Contains("fiendish") && registry.AppliedTemplateIds.Contains("skeleton");
        Assert(registryKeeps, "the skeleton adapter keeps the fiendish template recorded as applied and baked",
            registry != null ? "applied " + string.Join(",", registry.AppliedTemplateIds) + ", baked " + string.Join(",", registry.BakedTemplateIds) : "null");
        Assert(Fingerprint(fiendishOnce) == fiendishBefore, "the factory and the registry leave their input unchanged");
    }
}
}

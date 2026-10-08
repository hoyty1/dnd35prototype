using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// CRE-045: the NPC data's attack-math feats and natural-attack sequences against the Monster Manual.
/// Multiattack (MM p.304) needs three or more natural weapons; a definition that lists it with fewer is wrong data,
/// since CharacterStats.GetNaturalAttackSequencePenalty reads the feat by name (secondary attacks -2 instead of -5).
/// The other checks pin the MM entries the 2026-10-08 audit corrected, so a later edit cannot quietly undo them.
/// Reads the shared NPCDatabase templates only (never mutates them); runs in edit or Play mode.
/// </summary>
public static class NpcFeatDataTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        NPCDatabase.Init();

        Debug.Log("========== NPC FEAT DATA TESTS (CRE-045) ==========");

        TestMultiattackNeedsThreeNaturalAttacks();
        TestSalamandersHaveNoMultiattack();
        TestPrimarySecondaryFlagsFollowMm();
        TestFormianTaskmasterSequence();
        TestWeaponFinesseBySize();
        TestWeaponFinesseAndPowerAttackEntries();
        TestLycanthropeAttackFeats();
        TestManeuverFeats();
        TestEtherealFilcherNotStable();

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

    /// <summary>Natural weapons in a definition: every named entry times its Count (a 0-damage tongue is still one).</summary>
    private static int NaturalWeaponCount(NPCDefinition def)
    {
        if (def.NaturalAttacks == null) return 0;
        return def.NaturalAttacks.Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name)).Sum(a => Mathf.Max(1, a.Count));
    }

    private static bool Has(NPCDefinition def, string feat) => def != null && def.Feats != null && def.Feats.Contains(feat);

    private static NPCDefinition Get(string id)
    {
        NPCDefinition def = NPCDatabase.Get(id);
        if (def == null) Assert(false, $"NPC entry {id} exists");
        return def;
    }

    // MM p.304 Multiattack, prerequisite: three or more natural weapons. Every registered definition (MM creatures,
    // custom ones, dragons, lycanthropes and summon aliases).
    private static void TestMultiattackNeedsThreeNaturalAttacks()
    {
        var bad = new List<string>();
        int withFeat = 0;
        foreach (NPCDefinition def in NPCDatabase.AllNPCs)
        {
            if (!Has(def, "Multiattack")) continue;
            withFeat++;
            int n = NaturalWeaponCount(def);
            if (n < 3) bad.Add($"{def.Id} ({n})");
        }
        Assert(withFeat > 0, "Some definitions list Multiattack (the check below is not vacuous)", $"count {withFeat}");
        Assert(bad.Count == 0, "Every definition with Multiattack has three or more natural attacks (MM p.304)",
            "fewer: " + string.Join(", ", bad));
    }

    // MM p.219 gives salamanders Multiattack without the three natural weapons, but its own attack lines take the
    // -5 on the tail slap; the owner's rule (Multiattack only with three or more natural attacks) keeps it off.
    private static void TestSalamandersHaveNoMultiattack()
    {
        foreach (string id in new[] { "flamebrother_salamander", "average_salamander" })
        {
            NPCDefinition def = Get(id);
            if (def == null) continue;
            Assert(!Has(def, "Multiattack"), $"{id} has no Multiattack (one natural weapon)");
            Assert(Has(def, "Improved Natural Attack (tail)"), $"{id} lists Improved Natural Attack (tail) (MM p.219)");
        }
    }

    private static void AssertSequence(string id, string primaryName, int primaryCount, string secondaryName, string rule)
    {
        NPCDefinition def = Get(id);
        if (def == null || def.NaturalAttacks == null) return;
        NaturalAttackDefinition first = def.NaturalAttacks.FirstOrDefault();
        NaturalAttackDefinition sec = def.NaturalAttacks.FirstOrDefault(a => a.Name == secondaryName);
        bool ok = first != null && first.Name == primaryName && first.IsPrimary && first.Count == primaryCount
            && first.BonusDamageSource == DamageBonusSource.Strength
            && sec != null && !sec.IsPrimary && sec.BonusDamageSource == DamageBonusSource.StrengthHalf;
        Assert(ok, $"{id}: {primaryCount} {primaryName} primary first, {secondaryName} secondary ({rule})",
            string.Join("; ", def.NaturalAttacks.Select(a => $"{a.Name}x{a.Count}{(a.IsPrimary ? "P" : "S")} {a.BonusDamageSource}")));
    }

    // MM p.312: the attacks listed at the full bonus are primary, the lower ones secondary (half Strength to damage).
    private static void TestPrimarySecondaryFlagsFollowMm()
    {
        AssertSequence("green_slaad", "Claw", 2, "Bite", "MM p.230");
        AssertSequence("hellcat", "Claw", 2, "Bite", "MM p.54");
        AssertSequence("rakshasa", "Claw", 2, "Bite", "MM p.211");
        AssertSequence("nightmare", "Hoof", 2, "Bite", "MM p.194");

        NPCDefinition chimera = Get("chimera");
        if (chimera != null)
        {
            var heads = chimera.NaturalAttacks.Where(a => a.Name.StartsWith("Bite") || a.Name.StartsWith("Gore")).ToList();
            NaturalAttackDefinition claw = chimera.NaturalAttacks.FirstOrDefault(a => a.Name == "Claw");
            Assert(heads.Count == 3 && heads.All(a => a.IsPrimary) && claw != null && !claw.IsPrimary && claw.Count == 2
                && Has(chimera, "Multiattack"),
                "chimera: three heads primary, 2 claws secondary, Multiattack (MM p.34)");
        }
    }

    // MM p.109: sting +10 and 2 claws +8, Dodge, Improved Initiative, Multiattack.
    private static void TestFormianTaskmasterSequence()
    {
        NPCDefinition def = Get("formian_taskmaster");
        if (def == null) return;
        NaturalAttackDefinition sting = def.NaturalAttacks.FirstOrDefault(a => a.Name == "Sting");
        NaturalAttackDefinition claw = def.NaturalAttacks.FirstOrDefault(a => a.Name == "Claw");
        Assert(sting != null && sting.IsPrimary && claw != null && !claw.IsPrimary && claw.Count == 2 && Has(def, "Multiattack"),
            "formian_taskmaster: sting primary, 2 claws secondary, Multiattack (MM p.109)");
    }

    // MM p.286-289: monstrous centipedes Tiny to Large, scorpions Tiny and Small, spiders Tiny to Medium have Weapon
    // Finesse as a bonus feat; the larger sizes list no feats.
    private static void TestWeaponFinesseBySize()
    {
        string[] sizes = { "tiny", "small", "medium", "large", "huge", "gargantuan", "colossal" };
        CheckSizes("monstrous_centipede_", sizes, 4);
        CheckSizes("monstrous_scorpion_", sizes, 2);
        CheckSizes("monstrous_spider_", sizes, 3);
    }

    private static void CheckSizes(string prefix, string[] sizes, int finesseUpTo)
    {
        for (int i = 0; i < sizes.Length; i++)
        {
            NPCDefinition def = Get(prefix + sizes[i]);
            if (def == null) continue;
            bool expected = i < finesseUpTo;
            Assert(Has(def, "Weapon Finesse") == expected,
                $"{prefix}{sizes[i]} {(expected ? "has" : "has no")} Weapon Finesse (MM p.286-289)");
        }
    }

    private static void TestWeaponFinesseAndPowerAttackEntries()
    {
        var finesse = new Dictionary<string, bool>
        {
            { "gibbering_mouther", true },   // MM p.126
            { "small_air_elemental", true },  // MM p.96
            { "small_fire_elemental", true }, // MM p.99
            { "dire_weasel", true },          // MM p.65
            { "large_viper", true },          // MM p.280
            { "small_viper", true },          // MM p.280
            { "octopus", true },              // MM p.276
            { "phasm", false },               // MM p.208: Dodge and Mobility, no Weapon Finesse
            { "shocker_lizard", false },      // MM p.225: Improved Initiative only
        };
        foreach (var kv in finesse)
        {
            NPCDefinition def = Get(kv.Key);
            if (def != null) Assert(Has(def, "Weapon Finesse") == kv.Value, $"{kv.Key} {(kv.Value ? "has" : "has no")} Weapon Finesse");
        }

        var powerAttack = new Dictionary<string, bool>
        {
            { "babau", true },                // MM p.40
            { "small_earth_elemental", true }, // MM p.97
            { "small_water_elemental", true }, // MM p.100
            { "earth_mephit", true },         // MM p.182
            { "water_mephit", true },         // MM p.185
            { "air_mephit", false },          // MM p.181
            { "blue_slaad", false },          // MM p.229: Dodge, Mobility, Multiattack
            { "red_slaad", false },           // MM p.229
            { "gorgon", false },              // MM p.137
            { "howler", false },              // MM p.154
        };
        foreach (var kv in powerAttack)
        {
            NPCDefinition def = Get(kv.Key);
            if (def != null) Assert(Has(def, "Power Attack") == kv.Value, $"{kv.Key} {(kv.Value ? "has" : "has no")} Power Attack");
        }

        NPCDefinition wraith = Get("wraith");
        if (wraith != null)
            Assert(!wraith.Feats.Any(f => f.StartsWith("Improved Natural Attack")), "wraith has no Improved Natural Attack (MM p.258)");
    }

    // MM p.171-176: the lycanthropes' attack-math feats.
    private static void TestLycanthropeAttackFeats()
    {
        NPCDefinition werebear = Get("werebear");
        if (werebear != null)
            Assert(Has(werebear, "Multiattack") && Has(werebear, "Power Attack") && NaturalWeaponCount(werebear) >= 3,
                "werebear has Multiattack and Power Attack (MM p.171)");
        NPCDefinition werewolf = Get("werewolf");
        if (werewolf != null)
            Assert(!Has(werewolf, "Power Attack") && Has(werewolf, "Weapon Focus (bite)"),
                "werewolf has Weapon Focus (bite) and no Power Attack (MM p.175)");
        NPCDefinition weretiger = Get("weretiger");
        if (weretiger != null)
            Assert(!Has(weretiger, "Power Attack") && !Has(weretiger, "Weapon Focus")
                && Has(weretiger, "Improved Natural Attack (bite)") && Has(weretiger, "Improved Natural Attack (claw)"),
                "weretiger has Improved Natural Attack (bite, claw), no Power Attack or Weapon Focus (MM p.174)");
    }

    // Maneuver feats the code reads (CharacterController bull rush and overrun, ThreatSystem AoO): MM p.229 gives the
    // blue slaad Dodge, Mobility and Multiattack (no Improved Bull Rush); MM p.121-122 give the fire and frost giants
    // Improved Overrun.
    private static void TestManeuverFeats()
    {
        NPCDefinition slaad = Get("blue_slaad");
        if (slaad != null)
            Assert(!Has(slaad, "Improved Bull Rush") && Has(slaad, "Dodge") && Has(slaad, "Mobility") && Has(slaad, "Multiattack"),
                "blue_slaad has Dodge, Mobility, Multiattack and no Improved Bull Rush (MM p.229)");
        foreach (string id in new[] { "fire_giant", "frost_giant" })
        {
            NPCDefinition def = Get(id);
            if (def != null) Assert(Has(def, "Improved Overrun"), $"{id} has Improved Overrun (MM p.121-122)");
        }
    }

    // MM p.104: the ethereal filcher balances on a single leg, so it is not exceptionally stable (PHB p.154, p.158;
    // corrects the 2026-10-07 classification; owner informed 2026-10-08).
    private static void TestEtherealFilcherNotStable()
    {
        NPCDefinition def = Get("ethereal_filcher");
        if (def != null)
            Assert(!def.IsExceptionallyStable && !def.Clone().IsExceptionallyStable, "ethereal_filcher is not exceptionally stable (one leg, MM p.104)");
    }
}
}

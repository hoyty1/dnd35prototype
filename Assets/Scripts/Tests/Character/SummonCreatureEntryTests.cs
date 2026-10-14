using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// CRE-003: the creature IDs that the Summon Monster lists (PHB p.287), Summon Nature's Ally, wondrous items and presets
/// name resolve to their own Monster Manual entries. Before the fix <c>NPCDatabase.RegisterSummonCreatureAliases</c> ran
/// last in <c>Init</c> and replaced badger, riding_dog, owl, raven and giant_bee with clones of dire_badger, dog, eagle
/// and dire_bat. Checks each entry against its MM stat block (badger p.268, riding dog p.272, owl p.277, raven p.278,
/// giant bee p.284), the one remaining alias (wolf, MM p.283), every summon option's ID, and that an alias never
/// replaces an existing entry. PCs and NPCs summon through the same <c>GameManager.SpawnSummonedCreature</c>, so one data
/// path covers both. Pure data: no scene needed; database templates are only read.
/// </summary>
public static class SummonCreatureEntryTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        NPCDatabase.Init();

        Debug.Log("====== SUMMON CREATURE ENTRY TESTS (CRE-003) ======");

        TestBadger();
        TestRidingDog();
        TestOwl();
        TestRaven();
        TestGiantBee();
        TestWolfAliasOnly();
        TestEverySummonOptionResolves();
        TestAliasNeverReplacesEntry();

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

    private static bool IsAlias(NPCDefinition def)
    {
        return def != null && def.CreatureTags != null && def.CreatureTags.Contains("SummonAlias");
    }

    /// <summary>One MM stat block: size, CR, HD and hp total, the six abilities (-1 = no score), natural armor and the first natural attack.</summary>
    private static void CheckEntry(string id, string page, SizeCategory size, string cr, int hd, int hp,
        int str, int dex, int con, int intel, int wis, int cha, int naturalArmor,
        string attackName, int attackDice, int attackCount)
    {
        NPCDefinition d = NPCDatabase.Get(id);
        Assert(d != null, $"{id} is registered ({page})");
        if (d == null)
            return;

        Assert(d.Id == id && !IsAlias(d), $"{id} is its own entry, not a summon alias ({page})",
            $"Id={d.Id}, tags={string.Join(",", d.CreatureTags ?? new List<string>())}");
        Assert(d.SizeCategory == size, $"{id} size {size} ({page})", $"got {d.SizeCategory}");
        Assert(d.ChallengeRating == cr, $"{id} CR {cr} ({page})", $"got {d.ChallengeRating}");
        Assert(d.HitDice == hd && d.BaseHitDieHP == hp, $"{id} {hd} HD, {hp} hp ({page})", $"got {d.HitDice} HD, {d.BaseHitDieHP} hp");
        string abilities = $"{d.STR}/{d.DEX}/{d.CON}/{d.INT}/{d.WIS}/{d.CHA}";
        string want = $"{str}/{dex}/{con}/{intel}/{wis}/{cha}";
        Assert(abilities == want, $"{id} Str/Dex/Con/Int/Wis/Cha {want} ({page})", $"got {abilities}");
        Assert(d.NaturalArmorBonus == naturalArmor, $"{id} natural armor +{naturalArmor} ({page})", $"got {d.NaturalArmorBonus}");

        NaturalAttackDefinition first = d.NaturalAttacks != null && d.NaturalAttacks.Count > 0 ? d.NaturalAttacks[0] : null;
        Assert(first != null && first.Name == attackName && first.DamageDice == attackDice && first.DamageCount == 1 && first.Count == attackCount,
            $"{id} {attackCount} x {attackName} 1d{attackDice} ({page})",
            first == null ? "no natural attack" : $"got {first.Count} x {first.Name} {first.DamageCount}d{first.DamageDice}");
    }

    private static bool HasFeat(NPCDefinition d, string feat) => d != null && d.Feats != null && d.Feats.Contains(feat);

    private static void TestBadger()
    {
        // MM p.268: Small, 1d8+2 (6 hp), AC 15 (+1 size, +3 Dex, +1 natural), 2 claws 1d2-1 and bite 1d3-1,
        // Str 8 Dex 17 Con 15 Int 2 Wis 12 Cha 6, feats Multiattack and Track (bonus).
        CheckEntry("badger", "MM p.268", SizeCategory.Small, "1/2", 1, 6, 8, 17, 15, 2, 12, 6, 1, "Claw", 2, 2);
        NPCDefinition d = NPCDatabase.Get("badger");
        Assert(d != null && d.NaturalAttacks.Count == 2 && d.NaturalAttacks[1].Name == "Bite" && d.NaturalAttacks[1].DamageDice == 3,
            "badger bite 1d3 (MM p.268)");
        Assert(HasFeat(d, "Multiattack") && HasFeat(d, "Track") && !HasFeat(d, "Agile"), "badger feats Multiattack and Track only (MM p.268)",
            d == null ? "" : string.Join(",", d.Feats));
    }

    private static void TestRidingDog()
    {
        // MM p.272: Medium, 2d8+4 (13 hp), AC 16 (+2 Dex, +4 natural), bite 1d6+3, Str 15 Dex 15 Con 15 Int 2 Wis 12 Cha 6.
        CheckEntry("riding_dog", "MM p.272", SizeCategory.Medium, "1", 2, 13, 15, 15, 15, 2, 12, 6, 4, "Bite", 6, 1);
    }

    private static void TestOwl()
    {
        // MM p.277: Tiny, 1d8 (4 hp), AC 17 (+2 size, +3 Dex, +2 natural), talons 1d4-3, Str 4 Dex 17 Con 10 Int 2
        // Wis 14 Cha 4, CR 1/4, feats Alertness and Weapon Finesse (bonus).
        CheckEntry("owl", "MM p.277", SizeCategory.Tiny, "1/4", 1, 4, 4, 17, 10, 2, 14, 4, 2, "Talons", 4, 1);
        NPCDefinition d = NPCDatabase.Get("owl");
        Assert(HasFeat(d, "Alertness") && HasFeat(d, "Weapon Finesse"), "owl feats Alertness and Weapon Finesse (MM p.277)");
    }

    private static void TestRaven()
    {
        // MM p.278: Tiny, 1/4 d8 (1 hp, modelled as 1 HD), AC 14 (+2 size, +2 Dex), claws 1d2-5, Str 1 Dex 15 Con 10
        // Int 2 Wis 14 Cha 6, CR 1/6, feats Alertness and Weapon Finesse (bonus).
        CheckEntry("raven", "MM p.278", SizeCategory.Tiny, "1/6", 1, 1, 1, 15, 10, 2, 14, 6, 0, "Claws", 2, 1);
        NPCDefinition d = NPCDatabase.Get("raven");
        Assert(HasFeat(d, "Alertness") && HasFeat(d, "Weapon Finesse"), "raven feats Alertness and Weapon Finesse (MM p.278)");
    }

    private static void TestGiantBee()
    {
        // MM p.284: Medium vermin, 3d8 (13 hp), AC 14 (+2 Dex, +2 natural), sting 1d4 plus poison, Str 11 Dex 14 Con 11
        // Int - Wis 12 Cha 9, no feats; poison Fort DC 11, 1d4 Con initial and secondary; vermin are mindless.
        CheckEntry("giant_bee", "MM p.284", SizeCategory.Medium, "1", 3, 13, 11, 14, 11, CharacterStats.NO_SCORE, 12, 9, 2, "Sting", 4, 1);
        NPCDefinition d = NPCDatabase.Get("giant_bee");
        Assert(d != null && d.IsMindless, "giant bee is mindless (MM p.284, vermin traits)");
        Assert(d != null && (d.Feats == null || d.Feats.Count == 0), "giant bee has no feats (MM p.284)",
            d == null || d.Feats == null ? "" : string.Join(",", d.Feats));

        string poisonId = d != null && d.NaturalAttacks.Count > 0 ? d.NaturalAttacks[0].PoisonOnHitId : null;
        PoisonData poison = PoisonDatabase.GetPoison(poisonId);
        bool conDamage = poison != null
            && poison.FortitudeDC == 11
            && poison.InitialDamage != null && poison.InitialDamage.Count == 1
            && poison.InitialDamage[0].Ability == AbilityType.CON && poison.InitialDamage[0].DamageAmount == "1d4"
            && poison.SecondaryDamage != null && poison.SecondaryDamage.Count == 1
            && poison.SecondaryDamage[0].Ability == AbilityType.CON && poison.SecondaryDamage[0].DamageAmount == "1d4";
        Assert(conDamage, "giant bee poison: Fort DC 11, 1d4 Con initial and secondary (MM p.284)",
            poison == null ? $"no poison '{poisonId}'" : $"DC {poison.FortitudeDC}, initial {poison.InitialDamage?[0].Ability} {poison.InitialDamage?[0].DamageAmount}");
    }

    private static void TestWolfAliasOnly()
    {
        // The wolf (MM p.283) is stored as wolf_pack_hunter; the summon lists name it "wolf", the only alias.
        NPCDefinition wolf = NPCDatabase.Get("wolf");
        NPCDefinition baseWolf = NPCDatabase.Get("wolf_pack_hunter");
        Assert(wolf != null && baseWolf != null && wolf != baseWolf && IsAlias(wolf)
               && wolf.HitDice == baseWolf.HitDice && wolf.STR == baseWolf.STR && wolf.BaseHitDieHP == baseWolf.BaseHitDieHP,
            "wolf is a summon alias with wolf_pack_hunter's statistics (MM p.283)");

        var aliases = new List<string>();
        foreach (NPCDefinition d in NPCDatabase.AllNPCs)
            if (IsAlias(d))
                aliases.Add(d.Id);
        Assert(aliases.Count == 1 && aliases[0] == "wolf", "wolf is the only summon alias", string.Join(",", aliases));
    }

    private static void TestEverySummonOptionResolves()
    {
        // A wizard is unrestricted by alignment (PHB p.287), so every option of every list is offered.
        CharacterStats wizard = new CharacterStats("Test Wizard", 1, "Wizard", 10, 10, 10, 10, 10, 10, 0, 0, 0, 6, 1, 0, 6, 1, 8);
        int options = 0;
        var bad = new List<string>();
        for (int level = 1; level <= 9; level++)
        {
            for (int list = 0; list < 2; list++)
            {
                foreach (SummonMonsterOption option in SummonMonsterLists.GetFilteredOptionsForListLevel(level, wizard, list == 1))
                {
                    options++;
                    NPCDefinition d = NPCDatabase.Get(option.NpcDefinitionId);
                    if (d == null || d.Id != option.NpcDefinitionId || (IsAlias(d) && option.NpcDefinitionId != "wolf"))
                        bad.Add((list == 1 ? "SNA " : "SM ") + level + " " + option.DisplayName + " -> " + option.NpcDefinitionId);
                }
            }
        }
        Assert(options > 0 && bad.Count == 0, $"every Summon Monster and Summon Nature's Ally option ({options}) names its own entry",
            string.Join("; ", bad));
    }

    private static void TestAliasNeverReplacesEntry()
    {
        MethodInfo register = typeof(NPCDatabase).GetMethod("RegisterSummonAlias", BindingFlags.NonPublic | BindingFlags.Static);
        Assert(register != null, "NPCDatabase.RegisterSummonAlias exists (reflection)");
        if (register == null)
            return;

        NPCDefinition owl = NPCDatabase.Get("owl");
        register.Invoke(null, new object[] { "owl", "eagle", "Owl" });
        NPCDefinition after = NPCDatabase.Get("owl");
        bool kept = after == owl;
        if (!kept && owl != null)
            NPCDatabase.RegisterExternal(owl); // restore the real entry for the rest of the session
        Assert(kept, "a summon alias never replaces an existing entry (owl stays the MM owl, not an eagle clone)");
    }
}
}

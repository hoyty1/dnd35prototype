using System.Collections.Generic;
using UnityEngine;

namespace Tests.Character
{
/// <summary>
/// Untrained skill checks for every creature (CHR-067 fallback, CHR-086; batch 12 step 4). PHB p.63-64 (Table 4-2,
/// Untrained Skill Checks): a creature may attempt a skill that is not trained only with its key ability modifier, and
/// cannot attempt a trained-only skill without ranks (<see cref="CharacterStats.CanAttemptSkill"/>; counterspell
/// identification and the Use Magic Device gates use it). An NPC has no <see cref="CharacterStats.Skills"/> entries (or only some), so
/// <see cref="CharacterStats.TryGetSkillOrUntrained"/> gives it the untrained skill; skill names match ignoring case;
/// <see cref="CharacterStats.GetSkillBonus"/> and <see cref="CharacterStats.RollSkillCheck"/> share one total, the
/// familiar's bonus to its master included. Bare CharacterStats only.
/// </summary>
public static class NpcSkillFallbackTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== NPC SKILL FALLBACK TESTS ======");

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();

        TestUntrainedSkillsUseAbilityModifier();
        TestUntrainedRollMatchesBonus();
        TestTrainedOnlyWithoutRanks();
        TestCaseInsensitiveNames();
        TestPartialSkillDictionary();
        TestMindlessUntrained();
        TestBlindedPenaltyReachesUntrainedSkill();
        TestFamiliarTermInBothMethods();
        TestCanAttemptSkill();
        TestSpellcraftIdentificationNeedsRanks();

        Debug.Log($"====== NPC Skill Fallback Results: {_passed} passed, {_failed} failed ======");
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

    private static void AssertEq(int actual, int expected, string testName)
        => Assert(actual == expected, testName, $"expected {expected}, got {actual}");

    /// <summary>A human NPC-like creature with no skill list: DEX 16 (+3), WIS 8 (-1), INT 18 (+4).</summary>
    private static CharacterStats BuildNpc(string name = "FallbackNpc")
    {
        CharacterStats stats = new CharacterStats(
            name: name,
            level: 1,
            characterClass: "Warrior",
            str: 12,
            dex: 16,
            con: 12,
            wis: 8,
            intelligence: 18,
            cha: 10,
            bab: 1,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 8,
            raceName: "Human");
        stats.Skills.Clear();
        return stats;
    }

    /// <summary>The same creature with a created PC's skill list (every skill at 0 ranks, its class skills marked).</summary>
    private static CharacterStats BuildPc(string name = "FallbackPc")
    {
        CharacterStats stats = BuildNpc(name);
        stats.ClassLevels = new List<ClassLevelEntry> { new ClassLevelEntry("Fighter", 1) };
        stats.InitializeSkills("Fighter", 1);
        return stats;
    }

    private static int RollWithSeed(CharacterStats stats, string skill, int seed, out int d20)
    {
        Random.State saved = Random.state;
        try
        {
            Random.InitState(seed);
            int roll = stats.RollSkillCheck(skill);
            Random.InitState(seed);
            d20 = Random.Range(1, 21);
            return roll;
        }
        finally
        {
            Random.state = saved;
        }
    }

    private static void TestUntrainedSkillsUseAbilityModifier()
    {
        CharacterStats npc = BuildNpc();
        Assert(npc.Skills.Count == 0, "The NPC has no skill entries");
        AssertEq(npc.GetRacialSkillModifier("Listen") + npc.GetRacialSkillModifier("Balance")
                 + npc.GetRacialSkillModifier("Escape Artist"), 0, "A human has no racial modifier on these skills");
        AssertEq(npc.GetSkillBonus("Listen"), npc.WISMod, "Untrained Listen is the Wisdom modifier (PHB p.64)");
        AssertEq(npc.GetSkillBonus("Balance"), npc.DEXMod, "Untrained Balance is the Dexterity modifier");
        AssertEq(npc.GetSkillBonus("Escape Artist"), npc.DEXMod, "Untrained Escape Artist is the Dexterity modifier");
        AssertEq(npc.WISMod, -1, "WIS 8 gives -1, so a negative modifier is kept");
        AssertEq(npc.DEXMod, 3, "DEX 16 gives +3");
        Assert(npc.Skills.Count == 0, "The transient untrained skill is never added to the dictionary");

        Assert(npc.TryGetSkillOrUntrained("Balance", out Skill balance) && balance != null,
            "TryGetSkillOrUntrained gives a skill the creature has no entry for");
        Assert(balance != null && balance.KeyAbility == AbilityType.DEX && balance.Ranks == 0 && !balance.IsClassSkill
               && !balance.TrainedOnly, "The untrained skill: key ability DEX, 0 ranks, not a class skill, not trained only");
        Assert(npc.TryGetSkillOrUntrained("Spellcraft", out Skill spellcraft) && spellcraft != null && spellcraft.TrainedOnly,
            "The untrained Spellcraft is marked trained only, as InitializeSkills builds it");
        Assert(!npc.TryGetSkillOrUntrained("Basket Weaving", out Skill none) && none == null,
            "A name that is no skill gives false");
        AssertEq(npc.GetSkillBonus("Basket Weaving"), 0, "No skill: GetSkillBonus 0");
        AssertEq(npc.RollSkillCheck("Basket Weaving"), -1, "No skill: RollSkillCheck -1");
        AssertEq(npc.GetSkillBonus(null), 0, "A null name: GetSkillBonus 0");
    }

    private static void TestUntrainedRollMatchesBonus()
    {
        CharacterStats npc = BuildNpc();
        foreach (string skill in new[] { "Listen", "Balance", "Escape Artist" })
        {
            int roll = RollWithSeed(npc, skill, 515151, out int d20);
            AssertEq(roll, d20 + npc.GetSkillBonus(skill), $"An NPC's untrained {skill} roll is d20 + the shared total");
        }
        Assert(npc.Skills.Count == 0, "Rolling adds no skill entries");
    }

    private static void TestTrainedOnlyWithoutRanks()
    {
        CharacterStats npc = BuildNpc();
        AssertEq(npc.RollSkillCheck("Spellcraft"), -1, "Spellcraft (trained only) without ranks: RollSkillCheck -1 (PHB p.63-64)");
        AssertEq(npc.GetSkillBonus("Spellcraft"), 0, "Spellcraft without ranks: GetSkillBonus 0 despite INT 18");
        AssertEq(npc.GetSkillBonus("Use Magic Device"), 0, "Use Magic Device (trained only) without ranks: 0");

        CharacterStats pc = BuildPc();
        Assert(pc.Skills.ContainsKey("Spellcraft") && pc.Skills["Spellcraft"].Ranks == 0, "The PC has a 0-rank Spellcraft entry");
        AssertEq(pc.GetSkillBonus("Spellcraft"), 0, "The same rule for a PC entry: trained only with 0 ranks gives 0");
        AssertEq(pc.RollSkillCheck("Spellcraft"), -1, "The PC's 0-rank Spellcraft roll is -1");
        AssertEq(pc.GetSkillBonus("Listen"), npc.GetSkillBonus("Listen"), "A PC's 0-rank Listen equals the NPC's untrained Listen");

        pc.Skills["Spellcraft"].Ranks = 2;
        AssertEq(pc.GetSkillBonus("Spellcraft"), 2 + pc.INTMod, "With ranks, Spellcraft is ranks + INT");
    }

    private static void TestCaseInsensitiveNames()
    {
        CharacterStats npc = BuildNpc();
        AssertEq(npc.GetSkillBonus("escape artist"), npc.GetSkillBonus("Escape Artist"), "A lowercase name resolves for an NPC");
        Assert(npc.TryGetSkillOrUntrained("escape artist", out Skill skill) && skill != null && skill.SkillName == "Escape Artist",
            "The untrained skill carries the canonical name");
        int roll = RollWithSeed(npc, "BALANCE", 616161, out int d20);
        AssertEq(roll, d20 + npc.DEXMod, "An uppercase name rolls the right skill");

        CharacterStats pc = BuildPc();
        pc.Skills["Move Silently"].Ranks = 4;
        AssertEq(pc.GetSkillBonus("move silently"), 4 + pc.DEXMod, "A lowercase name finds the PC's ranked entry");
        AssertEq(pc.GetSkillBonus("Move silently"), pc.GetSkillBonus("Move Silently"), "A mis-cased name gives the same total");
        Assert(pc.Skills.ContainsKey("knowledge (arcana)"), "The Skills dictionary ignores case (CHR-086)");
    }

    private static void TestPartialSkillDictionary()
    {
        CharacterStats npc = BuildNpc();
        npc.Skills["Perform"] = new Skill("Perform", AbilityType.CHA, true) { Ranks = 4 };
        AssertEq(npc.GetSkillBonus("Perform"), 4 + npc.CHAMod, "An NPC's real entry (Perform ranks) is used");
        AssertEq(npc.GetSkillBonus("Listen"), npc.WISMod, "A skill missing beside it falls back to untrained");
        AssertEq(npc.Skills.Count, 1, "Only the real entry is in the dictionary");
    }

    private static void TestMindlessUntrained()
    {
        CharacterStats npc = BuildNpc("MindlessNpc");
        npc.IsMindless = true;
        AssertEq(npc.GetSkillBonus("Appraise"), 0, "A mindless creature cannot use an INT skill untrained (GetSkillBonus 0)");
        AssertEq(npc.RollSkillCheck("Appraise"), -1, "A mindless creature's INT skill roll is -1");
        AssertEq(npc.RollSkillCheck("Sense Motive"), -1, "A mindless creature cannot use Sense Motive");
        AssertEq(npc.GetSkillBonus("Listen"), npc.WISMod, "A mindless creature still listens untrained");
    }

    private static void TestBlindedPenaltyReachesUntrainedSkill()
    {
        CharacterStats npc = BuildNpc("BlindNpc");
        int listenBefore = npc.GetConditionSkillModifier("Listen");
        int balanceBefore = npc.GetConditionSkillModifier("Balance");
        npc.ActiveConditions.Add(new StatusEffect(CombatConditionType.Blinded, "UnitTest", 3));
        int listenDelta = npc.GetConditionSkillModifier("Listen") - listenBefore;
        int balanceDelta = npc.GetConditionSkillModifier("Balance") - balanceBefore;
        AssertEq(balanceDelta - listenDelta, -4,
            "Blinded: the -4 on STR and DEX skills reaches an NPC's untrained Balance (DMG p.301), not its Listen");
    }

    private static void TestFamiliarTermInBothMethods()
    {
        CharacterStats master = BuildPc("CatMaster");
        int without = master.GetSkillBonus("Move Silently");
        master.WizardFamiliar = WizardFamiliar.Create("Cat");
        int with = master.GetSkillBonus("Move Silently");
        AssertEq(with - without, 3, "A cat familiar gives its master +3 Move Silently in GetSkillBonus (PHB p.52)");
        int roll = RollWithSeed(master, "Move Silently", 717171, out int d20);
        AssertEq(roll - d20, with, "RollSkillCheck has the same total, the familiar's +3 included (CHR-086)");

        CharacterStats npc = BuildNpc("CatNpc");
        npc.WizardFamiliar = WizardFamiliar.Create("Cat");
        AssertEq(npc.GetSkillBonus("move silently"), npc.DEXMod + 3, "An NPC master's untrained Move Silently has the cat's +3");
        int npcRoll = RollWithSeed(npc, "Move Silently", 717171, out int npcD20);
        AssertEq(npcRoll - npcD20, npc.DEXMod + 3, "The NPC master's roll has it too");
    }

    private static void TestCanAttemptSkill()
    {
        CharacterStats npc = BuildNpc();
        Assert(npc.CanAttemptSkill("Listen") && npc.CanAttemptSkill("escape artist"),
            "CanAttemptSkill: an NPC may attempt an untrained skill that is not trained only (PHB p.64)");
        Assert(!npc.CanAttemptSkill("Spellcraft") && !npc.CanAttemptSkill("Use Magic Device"),
            "CanAttemptSkill: not a trained-only skill without ranks");
        Assert(!npc.CanAttemptSkill("Basket Weaving") && !npc.CanAttemptSkill(null), "CanAttemptSkill: not a name that is no skill");
        Assert(npc.Skills.Count == 0, "CanAttemptSkill adds no skill entries");

        CharacterStats mindless = BuildNpc("MindlessAttempt");
        mindless.IsMindless = true;
        Assert(!mindless.CanAttemptSkill("Appraise") && mindless.CanAttemptSkill("Listen"),
            "CanAttemptSkill: a mindless creature cannot attempt an INT skill, still listens");

        // Ranks, not the total, decide: 1 rank of Use Magic Device at CHA 6 (-2) totals -1 and may still be attempted.
        CharacterStats pc = BuildPc("LowChaPc");
        pc.CHA = 6;
        Assert(!pc.CanAttemptSkill("Use Magic Device"), "A PC's 0-rank Use Magic Device entry cannot be attempted");
        pc.Skills["Use Magic Device"].Ranks = 1;
        Assert(pc.CanAttemptSkill("Use Magic Device") && pc.GetSkillBonus("Use Magic Device") <= 0,
            "With 1 rank and CHA 6 Use Magic Device can be attempted although its total is not above 0 (PHB p.64, p.85)",
            $"total {pc.GetSkillBonus("Use Magic Device")}");
    }

    private static void TestSpellcraftIdentificationNeedsRanks()
    {
        foreach (bool asPc in new[] { false, true })
        {
            string side = asPc ? "PC" : "NPC";
            var go = new GameObject($"NpcSkillFallback_Identify_{side}");
            try
            {
                CharacterController controller = go.AddComponent<CharacterController>();
                controller.Stats = asPc ? BuildPc("IdentifyPc") : BuildNpc("IdentifyNpc");
                bool identified = controller.RollSpellcraftIdentification(0, out int roll, out int total, out int dc);
                Assert(!identified && roll == 0 && dc == 15,
                    $"A {side} without Spellcraft ranks cannot attempt to identify a spell: it fails without a roll (PHB p.64, p.170)",
                    $"identified={identified} roll={roll} total={total}");

                if (asPc)
                    controller.Stats.Skills["Spellcraft"].Ranks = 10;
                else
                    controller.Stats.Skills["Spellcraft"] = new Skill("Spellcraft", AbilityType.INT, false, true) { Ranks = 10 };
                identified = controller.RollSpellcraftIdentification(0, out roll, out total, out dc);
                Assert(identified && roll >= 1 && total == roll + 10 + controller.Stats.INTMod,
                    $"A {side} with 10 Spellcraft ranks rolls d20 + ranks + INT and identifies a 0-level spell (DC 15, PHB p.82)",
                    $"roll={roll} total={total}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
}

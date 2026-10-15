using System.Collections.Generic;
using UnityEngine;

namespace Tests.Classes
{
/// <summary>
/// CHR-020 (bard), CHR-026: the bard's bardic music for levels 1-8 on one rules core for PCs and NPCs (BardicMusicRules;
/// PHB p.29-30). Uses per day = bard level, one per performance started (suggestion none); each ability needs a bard level
/// and Perform ranks (3/3/3, 6 for inspire competence, 9 for suggestion); a performance blocks spellcasting; inspire
/// courage +1 (+2 at 8th) to the bard and the allies who hear him, lingering 5 rounds; fascinate: a Perform check is the
/// Will DC, prevented by nearby combat, broken by an obvious threat, ended without concentration, a resisted bard cannot
/// retry for 24 hours; suggestion: Will DC 10 + 1/2 level + Cha on a fascinated creature, no use spent; countersong: the
/// Perform result replaces a lower save against sonic or language-dependent magic within 30 ft; inspire competence: +2
/// competence on one ally's skill, not the bard; a deaf bard fails 20% and spends the use; NPC bards get Perform ranks;
/// the AI's choice. Review fixes 2026-10-15: the bard is no distraction to his own fascinate; an ally who stops hearing
/// Inspire Courage keeps it 5 rounds; a re-save against fascinate keeps the enchantment context; countersong reaches a
/// harpy's sonic song and needs no hearing; a suggested creature makes no AoO; Inspire Competence leaves out Hide and Move
/// Silently; a reused slot is forgotten. Needs Play mode (GameManager.Instance for the condition service).
/// </summary>
public static class BardClassRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("========== BARD CLASS RULES TESTS (CHR-020, CHR-026) ==========");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();

        TestAbilityGates();
        TestNpcPerformRanks();
        if (GameManager.Instance == null)
        {
            Debug.Log("  [SKIP] GameManager.Instance is null; the performance tests need Play mode");
        }
        else
        {
            TestInspireCourage();
            TestFascinate();
            TestFascinateNearbyCombat();
            TestSuggestion();
            TestCountersong();
            TestInspireCompetence();
            TestDeafBard();
            TestAiChoice();
            TestBardIsNoDistraction();
            TestInspireCourageHearing();
            TestCountersongAuraAndHearing();
            TestForgetCreature();
        }
        TestFascinateResaveContext();
        TestSuggestedAndCompetenceData();

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

    private static void AssertEq(int got, int want, string testName) => Assert(got == want, testName, $"got {got}, expected {want}");

    private static CharacterStats Build(string name, int level, string cls = "Bard", int perform = -1, int cha = 16, int intelligence = 10,
        string race = "Human")
    {
        var stats = new CharacterStats(
            name: name, level: level, characterClass: cls,
            str: 12, dex: 12, con: 12, wis: 10, intelligence: intelligence, cha: cha,
            bab: 0, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: level * 8, raceName: race);
        stats.EnsureMulticlassDataInitialized();
        if (perform >= 0)
        {
            stats.Skills["Perform"] = new Skill("Perform", AbilityType.CHA, isClassSkill: true) { Ranks = perform };
            stats.EnsureBardicMusicInitialized();
        }
        return stats;
    }

    private static CharacterController Create(CharacterStats stats, Vector2Int pos, CharacterTeam team)
    {
        var go = new GameObject(stats.CharacterName + "_BardRulesTest");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        controller.Init(stats, pos, null, null);
        inventory.Init(stats);
        controller.SetTeam(team);
        controller.GridPosition = pos;
        stats.AdjustMaxHP(200);
        stats.CurrentHP += 200;
        return controller;
    }

    private static void Cleanup(params CharacterController[] controllers)
    {
        GameManager gm = GameManager.Instance;
        foreach (CharacterController c in controllers)
        {
            if (c == null)
                continue;
            gm?.RemoveAllConditions(c);
            if (gm != null && gm.Grid != null)
                gm.Grid.ClearCreatureOccupancy(c);
            Object.DestroyImmediate(c.gameObject);
        }
    }

    /// <summary>Forces the Perform check's d20, every other d20 (saves, spell resistance) and the d%.</summary>
    private static System.Func<int, string, int, int> Force(int perform, int otherD20, int percent = 100)
        => (sides, ctx, natural) => ctx == BardicMusicRules.PerformCheckContext ? perform
            : ctx == BardicMusicRules.DeafFailureContext ? percent
            : sides == 20 ? otherD20 : natural;

    // ── Gates, uses, DCs (PHB p.29) ────────────────────────────────

    private static void TestAbilityGates()
    {
        CharacterStats noRanks = Build("NoRanks", 3, perform: 2);
        Assert(!BardicMusicRules.HasBardicMusic(noRanks) && BardicMusicRules.UsesPerDay(noRanks) == 0,
            "A bard with 2 Perform ranks has no bardic music (3 needed, PHB p.29)");
        CharacterStats bard3 = Build("Bard3", 3, perform: 5);
        Assert(BardicMusicRules.Knows(bard3, BardicAbility.InspireCourage) && BardicMusicRules.Knows(bard3, BardicAbility.Fascinate)
            && BardicMusicRules.Knows(bard3, BardicAbility.Countersong), "Bard 3 with 5 ranks: countersong, fascinate, inspire courage");
        Assert(!BardicMusicRules.Knows(bard3, BardicAbility.InspireCompetence), "Inspire Competence needs 6 ranks as well as 3rd level");
        CharacterStats bard3r6 = Build("Bard3r6", 3, perform: 6);
        Assert(BardicMusicRules.Knows(bard3r6, BardicAbility.InspireCompetence), "Bard 3 with 6 ranks has Inspire Competence");
        Assert(!BardicMusicRules.Knows(Build("Bard2", 2, perform: 6), BardicAbility.InspireCompetence), "Bard 2 has no Inspire Competence (3rd level)");
        Assert(!BardicMusicRules.Knows(Build("Bard6r8", 6, perform: 8), BardicAbility.Suggestion)
            && BardicMusicRules.Knows(Build("Bard6r9", 6, perform: 9), BardicAbility.Suggestion), "Suggestion needs 6th level and 9 ranks");
        AssertEq(BardicMusicRules.UsesPerDay(bard3), 3, "Uses per day = bard level (3)");
        AssertEq(BardClass.InspireCourageBonus(7), 1, "Inspire Courage +1 at 7th");
        AssertEq(BardClass.InspireCourageBonus(8), 2, "Inspire Courage +2 at 8th");
        AssertEq(BardClass.FascinateTargets(4), 2, "Fascinate 2 creatures at 4th");
        AssertEq(BardClass.FascinateTargets(7), 3, "Fascinate 3 creatures at 7th");
        AssertEq(BardicMusicRules.SuggestionDC(Build("Bard6", 6, perform: 9, cha: 16)), 16, "Suggestion DC 10 + 3 + 3 = 16 at bard 6, Cha 16");
        AssertEq(BardClass.BardicMusicDC(6, -1), 12, "A negative Charisma modifier lowers the suggestion DC (10 + 3 - 1)");
        Assert(!BardicAbilityInfo.IsImplemented(BardicAbility.InspireGreatness), "Inspire Greatness (9th) is not offered");

        Assert(BardClass.GetQuickStartCharacter().SkillRanks.TryGetValue("Perform", out int qs) && qs == 6,
            "The Quick Start bard has 6 Perform ranks (inspire competence at 3rd)");
    }

    private static void TestNpcPerformRanks()
    {
        CharacterStats npc = Build("NpcBard", 5);
        BardicMusicRules.ApplyNpcChoices(npc);
        AssertEq(BardicMusicRules.PerformRanks(npc), 8, "An NPC bard 5 gets the class-skill maximum of Perform ranks (5 + 3)");
        Assert(BardicMusicRules.HasBardicMusic(npc), "... and so has bardic music");
        CharacterStats fighter = Build("NpcFighter", 5, "Fighter");
        BardicMusicRules.ApplyNpcChoices(fighter);
        AssertEq(BardicMusicRules.PerformRanks(fighter), 0, "A non-bard NPC gets no Perform ranks");
    }

    // ── Inspire Courage ────────────────────────────────────────────

    private static void TestInspireCourage()
    {
        CharacterController bard = null, ally = null, deaf = null, foe = null;
        try
        {
            bard = Create(Build("IcBard", 2, perform: 5), new Vector2Int(-90, -90), CharacterTeam.Player);
            ally = Create(Build("IcAlly", 2, "Fighter"), new Vector2Int(-89, -90), CharacterTeam.Player);
            deaf = Create(Build("IcDeaf", 2, "Fighter"), new Vector2Int(-88, -90), CharacterTeam.Player);
            foe = Create(Build("IcFoe", 2, "Fighter"), new Vector2Int(-80, -90), CharacterTeam.Enemy);
            GameManager.Instance.ApplyCondition(deaf, CombatConditionType.Deafened, 10);
            var all = new List<CharacterController> { bard, ally, deaf, foe };

            BardicMusicResult r = BardicMusicRules.Start(bard, BardicAbility.InspireCourage, null, null, all);
            Assert(r.Performed, "Bard 2 begins Inspire Courage", r.Refusal);
            Assert(bard.Stats.HasInspireCourageBonus && ally.Stats.HasInspireCourageBonus, "The bard and the ally who hears him are inspired");
            Assert(!deaf.Stats.HasInspireCourageBonus && !foe.Stats.HasInspireCourageBonus, "A deafened ally and a foe are not");
            AssertEq(ally.Stats.Bonuses.Total(BonusTarget.AttackRoll), 1, "+1 morale on the ally's attack rolls");
            AssertEq(bard.Stats.BardBardicMusic.RemainingUses, 1, "Starting spends one use (2/day, 1 left)");
            Assert(!bard.Actions.HasStandardAction, "Starting is a standard action");
            Assert(BardicMusicRules.BlocksSpellcasting(bard.Stats), "No spellcasting while performing (PHB p.29)");
            Assert(WandValidator.Validate(bard, new ItemData { Name = "Test Wand", IsWand = true }).FailureReason == BardicMusicRules.SpellcastingBlockedReason,
                "No wand while performing (activation by magic word, PHB p.29)");

            for (int i = 0; i < 3; i++)
                BardicMusicRules.OnBardCountReached(bard, all);
            AssertEq(bard.Stats.BardBardicMusic.RemainingUses, 1, "Singing on spends no further use");
            Assert(ally.Stats.HasInspireCourageBonus, "The song lasts while the bard keeps singing");

            BardicMusicResult stop = BardicMusicRules.Stop(bard, all);
            Assert(stop.Performed && !BardicMusicRules.IsPerforming(bard.Stats) && !BardicMusicRules.BlocksSpellcasting(bard.Stats),
                "Stopping ends the performance (no action), so the bard may cast again");
            for (int i = 0; i < 4; i++)
                BardicMusicRules.OnBardCountReached(bard, all);
            Assert(ally.Stats.HasInspireCourageBonus, "The effect lingers (4 rounds after the song stopped)");
            BardicMusicRules.OnBardCountReached(bard, all);
            Assert(!ally.Stats.HasInspireCourageBonus && !bard.Stats.HasInspireCourageBonus, "... and fades after 5 rounds");
        }
        finally
        {
            Cleanup(bard, ally, deaf, foe);
        }
    }

    // ── Fascinate ──────────────────────────────────────────────────

    private static void TestFascinate()
    {
        CharacterController bard = null, ally = null, foe1 = null, foe2 = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            bard = Create(Build("FaBard", 4, perform: 7), new Vector2Int(-90, -60), CharacterTeam.Player);
            ally = Create(Build("FaAlly", 4, "Fighter"), new Vector2Int(-91, -60), CharacterTeam.Player);
            foe1 = Create(Build("FaFoeA", 3, "Fighter"), new Vector2Int(-80, -60), CharacterTeam.Enemy);
            foe2 = Create(Build("FaFoeB", 3, "Fighter"), new Vector2Int(-80, -62), CharacterTeam.Enemy);
            var all = new List<CharacterController> { bard, ally, foe1, foe2 };

            Assert(BardicMusicRules.FascinateCandidates(bard, all).Count == 2, "Two foes 50 ft away, away from any fight, can be fascinated");
            ScenarioHooks.RollFilter = Force(perform: 15, otherD20: 2);
            BardicMusicResult r = BardicMusicRules.Start(bard, BardicAbility.Fascinate, new List<CharacterController> { foe1, foe2 }, null, all);
            Assert(r.Performed && foe1.HasCondition(CombatConditionType.Fascinated) && foe2.HasCondition(CombatConditionType.Fascinated),
                "Bard 4 fascinates two foes (Will 2 + mod against the Perform check)", r.Log ?? r.Refusal);
            int dc = bard.Stats.BardBardicMusic.LastPerformCheck;
            AssertEq(dc, 15 + bard.Stats.GetSkillBonus("Perform"), "The Will DC is the Perform check result (PHB p.29)");
            AssertEq(bard.Stats.BardBardicMusic.RemainingUses, 3, "One use for the performance (4/day)");

            // Concentration: kept this turn, owed next turn.
            Assert(BardicMusicRules.OnBardTurnEnded(bard, all) == string.Empty, "The starting turn counts as concentration");
            BardicMusicRules.OnBardCountReached(bard, all);
            bard.Actions.Reset(); // the bard's next turn
            Assert(BardicMusicRules.CanConcentrate(bard, out string owes), "The next turn owes concentration", owes);
            Assert(BardicMusicRules.Concentrate(bard).Performed && !bard.Actions.HasStandardAction, "Concentrating is a standard action");

            // An obvious threat breaks one at once.
            GameManager.Instance.BreakFascinationOnHostileAction(ally, foe1, "attack");
            Assert(!foe1.HasCondition(CombatConditionType.Fascinated) && foe2.HasCondition(CombatConditionType.Fascinated),
                "An attack on a fascinated creature breaks its fascination at once (obvious threat, PHB p.29)");

            // No concentration: the song and the other fascination end at the end of the turn.
            BardicMusicRules.OnBardCountReached(bard, all);
            string ended = BardicMusicRules.OnBardTurnEnded(bard, all);
            Assert(!BardicMusicRules.IsPerforming(bard.Stats) && !foe2.HasCondition(CombatConditionType.Fascinated),
                "A turn without concentration ends the song and the fascination", ended);

            // foe1 was attacked this round: a danger that keeps it from being fascinated now (nearby combat).
            Assert((BardicMusicRules.FascinateTargetProblem(bard, foe1, all) ?? "").Contains("attacked"),
                "A creature just attacked cannot be fascinated (another danger)");

            // A creature that saved cannot be targeted again by this bard for 24 hours (foe2 was not attacked).
            bard.Actions.Reset();
            ScenarioHooks.RollFilter = Force(perform: 1, otherD20: 20);
            BardicMusicResult retry = BardicMusicRules.Start(bard, BardicAbility.Fascinate, new List<CharacterController> { foe2 }, null, all);
            Assert(retry.Performed && !foe2.HasCondition(CombatConditionType.Fascinated) && !BardicMusicRules.IsPerforming(bard.Stats),
                "A successful save leaves no one fascinated, and the song ends", retry.Log ?? retry.Refusal);
            Assert((BardicMusicRules.FascinateTargetProblem(bard, foe2, all) ?? "").Contains("24 hours"), "The bard cannot try it again for 24 hours",
                BardicMusicRules.FascinateTargetProblem(bard, foe2, all));
            bard.Stats.BardBardicMusic.RefreshUses();
            Assert(BardicMusicRules.FascinateTargetProblem(bard, foe2, all) == null, "The day's rest clears it",
                BardicMusicRules.FascinateTargetProblem(bard, foe2, all));
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(bard, ally, foe1, foe2);
        }
    }

    private static void TestFascinateNearbyCombat()
    {
        CharacterController bard = null, ally = null, foe = null, far = null;
        try
        {
            bard = Create(Build("NcBard", 3, perform: 6), new Vector2Int(-60, -90), CharacterTeam.Player);
            ally = Create(Build("NcAlly", 3, "Fighter"), new Vector2Int(-51, -90), CharacterTeam.Player);
            foe = Create(Build("NcFoe", 3, "Fighter"), new Vector2Int(-50, -90), CharacterTeam.Enemy);
            far = Create(Build("NcFar", 3, "Fighter"), new Vector2Int(-60, -110), CharacterTeam.Enemy);
            var all = new List<CharacterController> { bard, ally, foe, far };

            Assert((BardicMusicRules.FascinateTargetProblem(bard, foe, all) ?? "").Contains("distracted"),
                "A foe in melee with the bard's ally is distracted by nearby combat (delegated RAW reading)", BardicMusicRules.FascinateTargetProblem(bard, foe, all));
            Assert((BardicMusicRules.FascinateTargetProblem(bard, far, all) ?? "").Contains("90 ft"), "A creature 100 ft away is out of range");
            far.GridPosition = new Vector2Int(-60, -105);
            Assert(BardicMusicRules.FascinateTargetProblem(bard, far, all) == null, "A foe 75 ft away and 30+ ft from any foe can be fascinated");
            GameManager.Instance.BreakFascinationOnHostileAction(ally, far, "spell attack");
            Assert((BardicMusicRules.FascinateTargetProblem(bard, far, all) ?? "").Contains("attacked"),
                "A creature attacked this round is distracted (another danger)");
            ally.GridPosition = new Vector2Int(-60, -100);
            Assert((BardicMusicRules.NearbyCombatReason(foe, all) ?? "") == "" || BardicMusicRules.NearbyCombatReason(foe, all) == null,
                "With no foe within 30 ft and no melee nearby, nothing distracts the first foe");
        }
        finally
        {
            Cleanup(bard, ally, foe, far);
        }
    }

    // ── Suggestion ─────────────────────────────────────────────────

    private static void TestSuggestion()
    {
        CharacterController bard = null, foe = null, wolf = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            bard = Create(Build("SgBard", 6, perform: 9, cha: 16), new Vector2Int(-30, -90), CharacterTeam.Player);
            foe = Create(Build("SgFoe", 3, "Fighter", intelligence: 10), new Vector2Int(-20, -90), CharacterTeam.Enemy);
            wolf = Create(Build("SgBeast", 3, "Fighter", intelligence: 2), new Vector2Int(-20, -93), CharacterTeam.Enemy);
            var all = new List<CharacterController> { bard, foe, wolf };

            Assert(BardicMusicRules.SuggestionTargetProblem(bard, foe) != null, "No suggestion on a creature the bard has not fascinated");
            ScenarioHooks.RollFilter = Force(perform: 18, otherD20: 2);
            BardicMusicRules.Start(bard, BardicAbility.Fascinate, new List<CharacterController> { foe, wolf }, null, all);
            Assert(foe.HasCondition(CombatConditionType.Fascinated) && wolf.HasCondition(CombatConditionType.Fascinated), "Both are fascinated");
            int usesBefore = bard.Stats.BardBardicMusic.RemainingUses;

            BardicMusicRules.OnBardCountReached(bard, all);
            bard.Actions.Reset();
            Assert((BardicMusicRules.SuggestionTargetProblem(bard, wolf) ?? "").Contains("language"),
                "A creature of Intelligence 2 cannot understand the suggestion (language-dependent)");
            BardicMusicResult r = BardicMusicRules.Suggest(bard, foe);
            Assert(r.Performed && foe.HasCondition(CombatConditionType.Suggested), "A failed Will save: the foe follows the suggestion", r.Log ?? r.Refusal);
            Assert(foe.HasCondition(CombatConditionType.Fascinated), "The suggestion does not end the fascination");
            AssertEq(bard.Stats.BardBardicMusic.RemainingUses, usesBefore, "A suggestion spends no daily use");
            Assert(!bard.Actions.HasStandardAction && BardicMusicRules.OnBardTurnEnded(bard, all) == string.Empty,
                "It is a standard action and counts as the round's concentration");
            Assert(BardicMusicRules.SuggestionTargetProblem(bard, foe) != null, "No second suggestion to a creature already following one");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(bard, foe, wolf);
        }
    }

    // ── Countersong ────────────────────────────────────────────────

    private static void TestCountersong()
    {
        CharacterController bard = null, near = null, far = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            bard = Create(Build("CsBard", 1, perform: 4), new Vector2Int(-90, -30), CharacterTeam.Player);
            near = Create(Build("CsNear", 1, "Fighter"), new Vector2Int(-86, -30), CharacterTeam.Player);
            far = Create(Build("CsFar", 1, "Fighter"), new Vector2Int(-80, -30), CharacterTeam.Player);
            var all = new List<CharacterController> { bard, near, far };

            ScenarioHooks.RollFilter = Force(perform: 19, otherD20: 10);
            BardicMusicResult r = BardicMusicRules.Start(bard, BardicAbility.Countersong, null, null, all);
            Assert(r.Performed && BardicMusicRules.ActiveAbility(bard.Stats) == BardicAbility.Countersong, "Bard 1 begins a countersong", r.Refusal);
            int result = bard.Stats.BardBardicMusic.LastPerformCheck;
            AssertEq(BardicMusicRules.CountersongResultFor(near, all) ?? -1, result, "A creature 20 ft away may use the countersong");
            Assert(!BardicMusicRules.CountersongResultFor(far, all).HasValue, "A creature 50 ft away may not");

            SaveContext sonic = SaveContext.ForSpellId(DND35e.Identifiers.SpellNames.SOUND_BURST);
            SaveContext command = SaveContext.ForSpellId(DND35e.Identifiers.SpellNames.COMMAND);
            SaveContext plain = SaveContext.ForSpellId(DND35e.Identifiers.SpellNames.HOLD_PERSON);
            Assert(sonic.IsSonicOrLanguageDependent && command.IsSonicOrLanguageDependent && !plain.IsSonicOrLanguageDependent,
                "Sound Burst is sonic and Command language-dependent; Hold Person is neither");
            GameManager gm = GameManager.Instance;
            gm.PCs.Add(bard); gm.PCs.Add(near);
            try
            {
                AssertEq(BardicMusicRules.ApplyCountersong(near, 5, sonic), result, "The countersong result replaces a lower save against sonic magic");
                AssertEq(BardicMusicRules.ApplyCountersong(near, 5, plain), 5, "... but not against other magic");
                AssertEq(BardicMusicRules.ApplyCountersong(near, result + 3, command), result + 3, "A higher save stands");
            }
            finally
            {
                gm.PCs.Remove(bard); gm.PCs.Remove(near);
            }

            for (int i = 0; i < 9; i++)
                BardicMusicRules.OnBardCountReached(bard, all);
            Assert(BardicMusicRules.IsPerforming(bard.Stats), "The countersong goes on for 10 rounds");
            BardicMusicRules.OnBardCountReached(bard, all);
            Assert(!BardicMusicRules.IsPerforming(bard.Stats), "... and ends after the 10th");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(bard, near, far);
        }
    }

    // ── Inspire Competence ─────────────────────────────────────────

    private static void TestInspireCompetence()
    {
        CharacterController bard = null, ally = null;
        try
        {
            bard = Create(Build("IpBard", 3, perform: 6), new Vector2Int(-90, -10), CharacterTeam.Player);
            ally = Create(Build("IpAlly", 3, "Rogue"), new Vector2Int(-88, -10), CharacterTeam.Player);
            var all = new List<CharacterController> { bard, ally };

            Assert(BardicMusicRules.CompetenceTargetProblem(bard, bard) == "not on himself", "A bard cannot inspire competence in himself");
            int before = ally.Stats.EffectSkillBonus("Use Magic Device");
            BardicMusicResult r = BardicMusicRules.Start(bard, BardicAbility.InspireCompetence, new List<CharacterController> { ally }, "Use Magic Device", all);
            Assert(r.Performed, "Bard 3 inspires competence in an ally 10 ft away", r.Refusal);
            AssertEq(ally.Stats.EffectSkillBonus("Use Magic Device") - before, 2, "+2 competence bonus on that skill");
            AssertEq(ally.Stats.EffectSkillBonus("Hide"), 0, "... and on no other");
            BardicMusicRules.OnBardCountReached(bard, all);
            BardicMusicRules.OnBardTurnEnded(bard, all);
            AssertEq(ally.Stats.EffectSkillBonus("Use Magic Device") - before, 0, "Without concentration it ends");
        }
        finally
        {
            Cleanup(bard, ally);
        }
    }

    private static void TestDeafBard()
    {
        CharacterController bard = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            bard = Create(Build("DfBard", 2, perform: 5), new Vector2Int(-70, -10), CharacterTeam.Player);
            GameManager.Instance.ApplyCondition(bard, CombatConditionType.Deafened, 10);
            ScenarioHooks.RollFilter = Force(perform: 10, otherD20: 10, percent: 20);
            BardicMusicResult r = BardicMusicRules.Start(bard, BardicAbility.InspireCourage, null, null, new List<CharacterController> { bard });
            Assert(r.Performed && !BardicMusicRules.IsPerforming(bard.Stats) && bard.Stats.BardBardicMusic.RemainingUses == 1,
                "A deaf bard who rolls 20 or less on d% fails, and the use is spent (PHB p.29)", r.Log);
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(bard);
        }
    }

    // ── AI choice ──────────────────────────────────────────────────

    private static void TestAiChoice()
    {
        CharacterController bard = null, ally = null, foeNear = null, foeA = null, foeB = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            bard = Create(Build("AiBard", 4, perform: 7), new Vector2Int(-40, -40), CharacterTeam.Player);
            ally = Create(Build("AiAlly", 4, "Fighter"), new Vector2Int(-39, -40), CharacterTeam.Player);
            foeNear = Create(Build("AiFoeNear", 4, "Fighter"), new Vector2Int(-37, -40), CharacterTeam.Enemy);
            var melee = new List<CharacterController> { bard, ally, foeNear };
            DND35.AI.BardicMusicChoice c1 = DND35.AI.AIProfile.DefaultBardicMusicChoice(bard, melee);
            Assert(c1 != null && c1.Kind == DND35.AI.BardicMusicChoice.ChoiceKind.Start && c1.Ability == BardicAbility.InspireCourage,
                "With a foe close by, the AI bard sings Inspire Courage");

            foeNear.GridPosition = new Vector2Int(-25, -40);
            foeA = Create(Build("AiFoeA", 2, "Fighter"), new Vector2Int(-25, -42), CharacterTeam.Enemy);
            foeB = Create(Build("AiFoeB", 6, "Fighter"), new Vector2Int(-25, -44), CharacterTeam.Enemy);
            var distant = new List<CharacterController> { bard, ally, foeNear, foeA, foeB };
            DND35.AI.BardicMusicChoice c2 = DND35.AI.AIProfile.DefaultBardicMusicChoice(bard, distant);
            Assert(c2 != null && c2.Ability == BardicAbility.Fascinate && c2.Targets.Count == 2 && c2.Targets[0] == foeB,
                "With foes far from the fight, it fascinates two (bard 4), the highest Hit Dice first");

            ScenarioHooks.RollFilter = Force(perform: 20, otherD20: 1);
            BardicMusicRules.Start(bard, BardicAbility.Fascinate, c2.Targets, null, distant);
            ScenarioHooks.RollFilter = saved;
            BardicMusicRules.OnBardCountReached(bard, distant);
            bard.Actions.Reset();
            Assert(BardicMusicRules.IsPerforming(bard.Stats) && foeB.HasCondition(CombatConditionType.Fascinated),
                "The forced Fascinate holds (Perform 20 against Will 1)");
            DND35.AI.BardicMusicChoice c3 = DND35.AI.AIProfile.DefaultBardicMusicChoice(bard, distant);
            Assert(c3 != null && c3.Kind == DND35.AI.BardicMusicChoice.ChoiceKind.Concentrate, "While fascinating, it concentrates");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(bard, ally, foeNear, foeA, foeB);
        }
    }

    // ── Review fixes 2026-10-15 (delegated RAW readings 2026-10-14) ───

    /// <summary>The performing bard is not "a nearby combat": he may fascinate a foe within 30 ft; a foe threatening him there is.</summary>
    private static void TestBardIsNoDistraction()
    {
        CharacterController bard = null, foe = null, other = null;
        try
        {
            bard = Create(Build("NdBard", 3, perform: 6), new Vector2Int(-100, -60), CharacterTeam.Player);
            foe = Create(Build("NdFoe", 3, "Fighter"), new Vector2Int(-96, -60), CharacterTeam.Enemy);
            var all = new List<CharacterController> { bard, foe };
            Assert(BardicMusicRules.FascinateTargetProblem(bard, foe, all) == null,
                "A foe 20 ft from the bard, with no one else about, can be fascinated (the bard is no distraction)",
                BardicMusicRules.FascinateTargetProblem(bard, foe, all));
            other = Create(Build("NdOther", 3, "Fighter"), new Vector2Int(-101, -60), CharacterTeam.Enemy);
            all.Add(other);
            Assert((BardicMusicRules.FascinateTargetProblem(bard, foe, all) ?? "").Contains("melee"),
                "A foe threatening the bard within 30 ft of the target is a melee nearby", BardicMusicRules.FascinateTargetProblem(bard, foe, all));
        }
        finally
        {
            Cleanup(bard, foe, other);
        }
    }

    /// <summary>PHB p.29: Inspire Courage lasts while the ally hears the song and 5 rounds thereafter, per ally.</summary>
    private static void TestInspireCourageHearing()
    {
        CharacterController bard = null, ally = null;
        try
        {
            bard = Create(Build("IhBard", 2, perform: 5), new Vector2Int(-100, -40), CharacterTeam.Player);
            ally = Create(Build("IhAlly", 2, "Fighter"), new Vector2Int(-99, -40), CharacterTeam.Player);
            var all = new List<CharacterController> { bard, ally };
            Assert(BardicMusicRules.Start(bard, BardicAbility.InspireCourage, null, null, all).Performed && ally.Stats.HasInspireCourageBonus,
                "The ally is inspired");
            GameManager.Instance.ApplyCondition(ally, CombatConditionType.Deafened, 20);
            for (int i = 0; i < 5; i++)
                BardicMusicRules.OnBardCountReached(bard, all);
            Assert(ally.Stats.HasInspireCourageBonus && BardicMusicRules.IsPerforming(bard.Stats),
                "Deafened, the ally keeps the bonus for 5 rounds while the bard sings on");
            BardicMusicRules.OnBardCountReached(bard, all);
            Assert(!ally.Stats.HasInspireCourageBonus && bard.Stats.HasInspireCourageBonus,
                "... then loses it; the bard, who still hears himself, keeps it");
            GameManager.Instance.RemoveCondition(ally, CombatConditionType.Deafened);
            BardicMusicRules.OnBardCountReached(bard, all);
            Assert(ally.Stats.HasInspireCourageBonus, "Hearing the song again, the ally is inspired again");
            BardicMusicRules.Stop(bard, all);
        }
        finally
        {
            Cleanup(bard, ally);
        }
    }

    /// <summary>
    /// PHB p.29: a creature within 30 ft may use the countersong in place of its save against a sonic magical attack,
    /// a monster's too (the harpy's captivating song, MM p.150), without having to hear the bard.
    /// </summary>
    private static void TestCountersongAuraAndHearing()
    {
        CharacterController bard = null, deaf = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        GameManager gm = GameManager.Instance;
        try
        {
            bard = Create(Build("CaBard", 1, perform: 4), new Vector2Int(-100, -20), CharacterTeam.Player);
            deaf = Create(Build("CaDeaf", 1, "Fighter"), new Vector2Int(-97, -20), CharacterTeam.Player);
            gm.ApplyCondition(deaf, CombatConditionType.Deafened, 10);
            var all = new List<CharacterController> { bard, deaf };
            ScenarioHooks.RollFilter = Force(perform: 19, otherD20: 10);
            Assert(BardicMusicRules.Start(bard, BardicAbility.Countersong, null, null, all).Performed, "Bard 1 begins a countersong");
            ScenarioHooks.RollFilter = saved;
            int result = bard.Stats.BardBardicMusic.LastPerformCheck;

            NPCDatabase.Init();
            NPCDefinition harpy = NPCDatabase.Get("harpy");
            AuraAbilityDefinition song = harpy != null ? harpy.AuraAbility : null;
            SaveContext ctx = AIService.AuraSaveContext(song, CombatConditionType.Fascinated);
            Assert(song != null && ctx.IsSonicOrLanguageDependent && ctx.IsCharm && ctx.IsEnchantment && song.IsMindAffecting,
                "The harpy's captivating song is a sonic mind-affecting charm (MM p.150)");
            gm.PCs.Add(bard); gm.PCs.Add(deaf);
            try
            {
                AssertEq(BardicMusicRules.ApplyCountersong(deaf, 5, ctx), result,
                    "A deafened creature 15 ft away uses the countersong against the harpy's song (no hearing needed)");
            }
            finally
            {
                gm.PCs.Remove(bard); gm.PCs.Remove(deaf);
            }
            BardicMusicRules.Stop(bard, all);
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(bard, deaf);
        }
    }

    /// <summary>CRE-046: a reused slot leaves nothing in another bard's lists.</summary>
    private static void TestForgetCreature()
    {
        CharacterController bard = null, ally = null;
        try
        {
            bard = Create(Build("FgBard", 2, perform: 5), new Vector2Int(-100, 0), CharacterTeam.Player);
            ally = Create(Build("FgAlly", 2, "Fighter"), new Vector2Int(-99, 0), CharacterTeam.Player);
            var all = new List<CharacterController> { bard, ally };
            BardicMusicRules.Start(bard, BardicAbility.InspireCourage, null, null, all);
            BardicMusicData d = bard.Stats.BardBardicMusic;
            d.FascinateResistedToday.Add(ally);
            BardicMusicRules.ForgetCreature(bard.Stats, ally);
            Assert(!d.InspiredAllies.Contains(ally) && !d.FascinateResistedToday.Contains(ally),
                "ForgetCreature drops the slot's old creature from the bard's lists");
            BardicMusicRules.Stop(bard, all);
        }
        finally
        {
            Cleanup(bard, ally);
        }
    }

    /// <summary>Every save against Fascinate is a spell-like enchantment: an elf gets his +2 on the re-save too.</summary>
    private static void TestFascinateResaveContext()
    {
        SaveContext ctx = BardicMusicRules.FascinateSaveContext();
        Assert(ctx.IsSpell && ctx.IsEnchantment && !ctx.IsSonicOrLanguageDependent, "Fascinate is a spell-like enchantment (PHB p.29)");
        CharacterStats elf = Build("ReElf", 2, "Fighter", race: "Elf");
        AssertEq(SaveRules.Modifier(elf, SavingThrowType.Will, ctx) - SaveRules.Modifier(elf, SavingThrowType.Will, SaveContext.None), 2,
            "An elf's +2 against enchantments applies to every save against Fascinate");
    }

    /// <summary>A suggested creature takes no hostile action; Inspire Competence leaves out the self-defeating skills.</summary>
    private static void TestSuggestedAndCompetenceData()
    {
        ConditionDefinition def = ConditionRules.GetDefinition(CombatConditionType.Suggested);
        Assert(def.PreventsAoO && def.PreventsThreatening, "A suggested creature leaving the fight makes no AoO and threatens no square");
        var skills = new List<string>(BardicMusicRules.CompetenceSkills);
        Assert(!skills.Contains("Move Silently") && !skills.Contains("Hide") && skills.Contains("Use Magic Device"),
            "Inspire Competence offers no Move Silently or Hide (self-defeating, PHB p.29)");
    }
}
}

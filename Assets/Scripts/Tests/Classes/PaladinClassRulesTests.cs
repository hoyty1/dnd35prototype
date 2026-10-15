using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tests.Classes
{
/// <summary>
/// CHR-020 (paladin): the paladin's class features for levels 1-8 on one rules core for PCs and NPCs (PHB p.42-45).
/// Smite Evil (SmiteRules): declared before the attack, used up by the next melee attack, hit or miss; +Charisma bonus
/// to the attack and +paladin level to damage against an evil creature, no effect (but used) against any other; a ranged
/// attack leaves it declared; an unused declaration lapses at the end of the turn; uses per day 1 + 1 per 5 levels. The
/// celestial and fiendish template smites on the same core: +Hit Dice damage (maximum +20), no attack bonus (MM p.31,
/// p.107; CMB-021, AI-056). The AI declares only against a creature it believes is a smite foe. Lay on Hands
/// (LayOnHandsRules): from 2nd level, paladin level x Charisma bonus a day, a standard action on itself or a creature
/// within touch; heals the living (not constructs), or damages undead after a melee touch attack. Divine Health: disease
/// immunity from 3rd level. Turn Undead from 4th level as a cleric of paladin level - 3, added to cleric levels (PHB
/// p.59), 3 + Charisma modifier uses with no minimum. Needs Play mode (CharacterController.Init, GameManager for the AI
/// declaration and DealDamage).
/// </summary>
public static class PaladinClassRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("========== PALADIN CLASS RULES TESTS (CHR-020) ==========");

        RaceDatabase.Init();
        ClassRegistry.Init();
        ItemDatabase.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();

        TestDailyTrackers();
        TestSmiteEvilAgainstEvil();
        TestSmiteAgainstNonEvilIsWasted();
        TestSmiteRangedAttackKeepsDeclaration();
        TestTemplateSmite();
        TestDeclareRefusalsAndLapse();
        TestAiSmiteChoice();
        TestLayOnHandsHeals();
        TestLayOnHandsHarmsUndead();
        TestLayOnHandsRefusals();
        TestAiLayOnHandsChoice();
        TestDivineHealth();
        TestTurnUndeadLevels();
        TestGrapplingPaladinUsesNoSupernaturalAction();

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

    private static CharacterStats Build(int level, string cls = "Paladin", int cha = 14, int str = 16)
    {
        var stats = new CharacterStats(
            name: cls + level, level: level, characterClass: cls,
            str: str, dex: 12, con: 12, wis: 12, intelligence: 10, cha: cha,
            bab: 0, armorBonus: 0, shieldBonus: 0,
            damageDice: 8, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: level * 10, raceName: "Human");
        stats.EnsureMulticlassDataInitialized();
        return stats;
    }

    private static CharacterController Create(string name, int level, Vector2Int pos, string cls = "Paladin", int cha = 14)
    {
        var go = new GameObject(name + "_PaladinRulesTest");
        var controller = go.AddComponent<CharacterController>();
        var inventory = go.AddComponent<InventoryComponent>();
        CharacterStats stats = Build(level, cls, cha);
        stats.CharacterName = name;
        controller.Init(stats, pos, null, null);
        inventory.Init(stats);
        controller.GridPosition = pos;
        return controller;
    }

    /// <summary>A target with plenty of hit points and a low AC.</summary>
    private static CharacterController CreateTarget(string name, Vector2Int pos, Alignment alignment)
    {
        CharacterController t = Create(name, 3, pos, "Fighter", 10);
        t.Stats.CharacterAlignment = alignment;
        t.Stats.AdjustMaxHP(500);
        t.Stats.CurrentHP += 500;
        return t;
    }

    private static void Cleanup(params CharacterController[] controllers)
    {
        GameManager gm = GameManager.Instance;
        foreach (CharacterController c in controllers)
        {
            if (c == null)
                continue;
            if (gm != null && gm.Grid != null)
                gm.Grid.ClearCreatureOccupancy(c);
            Object.DestroyImmediate(c.gameObject);
        }
    }

    /// <summary>Forces every d20 to <paramref name="d20"/> (and the lay on hands touch to <paramref name="touch"/>).</summary>
    private static System.Func<int, string, int, int> ForceD20(int d20, int touch = 20)
        => (sides, ctx, natural) => ctx == LayOnHandsRules.TouchAttackRollContext ? touch : sides == 20 ? d20 : natural;

    private static int Mod(CombatResult r) => r != null ? r.TotalRoll - r.DieRoll : int.MinValue;

    // ── Daily trackers (PHB p.44, Table 3-12) ───────────────────────

    private static void TestDailyTrackers()
    {
        AssertEq(Build(1).GetPaladinSmiteEvil().MaxUsesPerDay, 1, "Paladin 1 smites 1/day");
        AssertEq(Build(4).GetPaladinSmiteEvil().MaxUsesPerDay, 1, "Paladin 4 smites 1/day");
        AssertEq(Build(5).GetPaladinSmiteEvil().MaxUsesPerDay, 2, "Paladin 5 smites 2/day");
        Assert(Build(5, "Fighter").GetPaladinSmiteEvil() == null, "A fighter has no smite tracker");
        Assert(Build(1).GetPaladinLayOnHands() == null, "Paladin 1 has no Lay on Hands (from 2nd level)");
        AssertEq(Build(2, cha: 14).GetPaladinLayOnHands().MaxPool, 4, "Paladin 2 with Cha 14 lays on hands for 4 hp a day");
        AssertEq(Build(6, cha: 16).GetPaladinLayOnHands().MaxPool, 18, "Paladin 6 with Cha 16 lays on hands for 18 hp a day");
        AssertEq(Build(4, cha: 11).GetPaladinLayOnHands().MaxPool, 0, "Paladin 4 with Cha 11 has no pool (Charisma 12 or higher)");
        AssertEq(PaladinClass.LayOnHandsPool(1, 3), 0, "PaladinClass.LayOnHandsPool is 0 at 1st level");

        CharacterStats grown = Build(4, cha: 14);
        AssertEq(grown.GetPaladinSmiteEvil().MaxUsesPerDay, 1, "Tracker before the level-up: 1/day");
        grown.ClassLevels[0].Level = 5;
        grown.InvalidateClassLevelCache();
        AssertEq(grown.GetPaladinSmiteEvil().MaxUsesPerDay, 2, "The tracker follows a level-up (5th: 2/day)");
    }

    // ── Smite Evil ─────────────────────────────────────────────────

    /// <summary>Paladin 3, Cha 14: +2 attack and +3 damage on the smiting attack against an evil creature.</summary>
    private static void TestSmiteEvilAgainstEvil()
    {
        CharacterController p = null, t = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            ScenarioHooks.RollFilter = ForceD20(15);
            p = Create("SmitePaladin", 3, new Vector2Int(-80, -80), cha: 14);
            t = CreateTarget("EvilTarget", new Vector2Int(-79, -80), Alignment.ChaoticEvil);

            CombatResult plain = p.Attack(t, false, 0, null, null);
            string declared = SmiteRules.TryDeclare(p, out string refusal);
            Assert(declared != null && p.Stats.DeclaredSmite == SmiteSource.PaladinSmiteEvil,
                "A paladin declares Smite Evil", refusal);
            CombatResult smite = p.Attack(t, false, 0, null, null);
            AssertEq(Mod(smite) - Mod(plain), 2, "The smiting attack adds the Charisma bonus (+2) to the attack roll");
            AssertEq(smite.WeaponDamageBonus.SmiteBonus, 3, "The smiting attack adds paladin level (+3) to damage");
            Assert((smite.SpecialAttackNote ?? "").Contains("Smite Evil"), "The attack notes the smite", smite.SpecialAttackNote);
            Assert(p.Stats.DeclaredSmite == SmiteSource.None, "The attack uses up the declaration");
            AssertEq(p.Stats.GetPaladinSmiteEvil().RemainingUses, 0, "The day's one smite is spent");

            CombatResult after = p.Attack(t, false, 0, null, null);
            Assert(Mod(after) == Mod(plain) && after.WeaponDamageBonus.SmiteBonus == 0, "The next attack has no smite");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(p, t);
        }
    }

    private static void TestSmiteAgainstNonEvilIsWasted()
    {
        CharacterController p = null, t = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            ScenarioHooks.RollFilter = ForceD20(15);
            p = Create("WastePaladin", 5, new Vector2Int(-80, -84), cha: 14);
            t = CreateTarget("NeutralTarget", new Vector2Int(-79, -84), Alignment.TrueNeutral);

            CombatResult plain = p.Attack(t, false, 0, null, null);
            SmiteRules.TryDeclare(p, out _);
            CombatResult smite = p.Attack(t, false, 0, null, null);
            Assert(Mod(smite) == Mod(plain) && smite.WeaponDamageBonus.SmiteBonus == 0,
                "A smite against a creature that is not evil has no effect (PHB p.44)", $"mod {Mod(smite)} vs {Mod(plain)}");
            Assert((smite.SpecialAttackNote ?? "").Contains("no effect"), "The attack notes the wasted smite", smite.SpecialAttackNote);
            AssertEq(p.Stats.GetPaladinSmiteEvil().RemainingUses, 1, "The wasted smite is still used up (2/day, 1 left)");

            // A miss uses it up too: the use is the attempt (delegated RAW reading 2026-10-14).
            ScenarioHooks.RollFilter = ForceD20(1);
            t.Stats.CharacterAlignment = Alignment.LawfulEvil;
            SmiteRules.TryDeclare(p, out _);
            CombatResult miss = p.Attack(t, false, 0, null, null);
            Assert(miss != null && !miss.Hit && p.Stats.GetPaladinSmiteEvil().RemainingUses == 0,
                "A missed smite is used up (one attempt per use)");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(p, t);
        }
    }

    private static void TestSmiteRangedAttackKeepsDeclaration()
    {
        CharacterController p = null, t = null;
        try
        {
            p = Create("RangedPaladin", 2, new Vector2Int(-80, -88));
            t = CreateTarget("RangedTarget", new Vector2Int(-75, -88), Alignment.ChaoticEvil);
            SmiteRules.TryDeclare(p, out _);
            SmiteApplication ranged = SmiteRules.ConsumeForMeleeAttack(p, t, isRangedAttack: true);
            Assert(!ranged.Used && p.Stats.DeclaredSmite == SmiteSource.PaladinSmiteEvil && p.Stats.GetPaladinSmiteEvil().RemainingUses == 1,
                "A ranged attack neither gains nor spends a declared smite (a melee attack, PHB p.44)");
        }
        finally
        {
            Cleanup(p, t);
        }
    }

    /// <summary>MM p.31, p.107: +HD damage (max +20) against an evil (good) foe, no attack bonus; once a day.</summary>
    private static void TestTemplateSmite()
    {
        CharacterController c = null, t = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            ScenarioHooks.RollFilter = ForceD20(15);
            c = Create("CelestialFighter", 4, new Vector2Int(-80, -92), "Fighter", 16);
            c.Stats.HasTemplateSmiteEvil = true;
            t = CreateTarget("EvilFoe", new Vector2Int(-79, -92), Alignment.NeutralEvil);

            CombatResult plain = c.Attack(t, false, 0, null, null);
            Assert(SmiteRules.TryDeclare(c, out _) != null && c.Stats.DeclaredSmite == SmiteSource.TemplateSmiteEvil,
                "A celestial creature declares its template Smite Evil");
            CombatResult smite = c.Attack(t, false, 0, null, null);
            AssertEq(Mod(smite) - Mod(plain), 0, "A template smite adds nothing to the attack roll (MM p.31), whatever the Charisma");
            AssertEq(smite.WeaponDamageBonus.SmiteBonus, c.Stats.GetHitDice(), "A template smite adds Hit Dice to damage");
            Assert(c.Stats.TemplateSmiteUsed && SmiteRules.UsesLeft(c.Stats) == 0, "The template smite is used for the day");

            CharacterStats big = Build(24, "Fighter");
            big.HasTemplateSmiteGood = true;
            AssertEq(SmiteRules.DamageBonus(big, SmiteSource.TemplateSmiteGood), 20, "A template smite's damage is capped at +20");
            Assert(SmiteRules.IsSmiteFoe(SmiteSource.TemplateSmiteGood, Build(1, "Fighter").WithAlignment(Alignment.LawfulGood))
                && !SmiteRules.IsSmiteFoe(SmiteSource.TemplateSmiteGood, Build(1, "Fighter").WithAlignment(Alignment.ChaoticEvil)),
                "Smite Good strikes good creatures only (MM p.107)");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(c, t);
        }
    }

    private static void TestDeclareRefusalsAndLapse()
    {
        CharacterController p = null, f = null;
        try
        {
            p = Create("DeclarePaladin", 1, new Vector2Int(-80, -96));
            f = Create("DeclareFighter", 1, new Vector2Int(-78, -96), "Fighter");

            Assert(!SmiteRules.CanDeclare(f, out _, out string fighterReason) && fighterReason == "No smite ability",
                "A creature without a smite cannot declare one", fighterReason);
            Assert(SmiteRules.TryDeclare(p, out _) != null, "Paladin 1 declares its smite");
            Assert(!SmiteRules.CanDeclare(p, out _, out string twice) && twice == "Already declared", "A second declaration is refused", twice);

            Assert(SmiteRules.LapseAtTurnEnd(p.Stats) && p.Stats.DeclaredSmite == SmiteSource.None
                && p.Stats.GetPaladinSmiteEvil().RemainingUses == 1,
                "An unused declaration lapses at the end of the turn without spending a use (delegated RAW reading 2026-10-14)");

            p.Stats.GetPaladinSmiteEvil().ExpendSmite();
            Assert(!SmiteRules.CanDeclare(p, out _, out string spent) && spent == "No uses left today", "No declaration with no use left", spent);
            p.Stats.RefreshPaladinDailyUses();
            Assert(SmiteRules.CanDeclare(p, out _, out _), "A full rest restores the day's smites");
        }
        finally
        {
            Cleanup(p, f);
        }
    }

    private static void TestAiSmiteChoice()
    {
        CharacterController p = null, evil = null, neutral = null, fiend = null, good = null;
        try
        {
            p = Create("AiPaladin", 3, new Vector2Int(-80, -100));
            evil = CreateTarget("AiEvil", new Vector2Int(-79, -100), Alignment.ChaoticEvil);
            neutral = CreateTarget("AiNeutral", new Vector2Int(-81, -100), Alignment.TrueNeutral);
            fiend = Create("AiFiend", 3, new Vector2Int(-80, -103), "Fighter");
            fiend.Stats.HasTemplateSmiteGood = true;
            good = CreateTarget("AiGood", new Vector2Int(-79, -103), Alignment.LawfulGood);

            Assert(DND35.AI.AIProfile.DefaultShouldDeclareSmite(p, evil), "The AI paladin smites an evil foe");
            Assert(!DND35.AI.AIProfile.DefaultShouldDeclareSmite(p, neutral), "The AI paladin never smites a creature it does not believe evil");
            Assert(DND35.AI.AIProfile.DefaultShouldDeclareSmite(fiend, good) && !DND35.AI.AIProfile.DefaultShouldDeclareSmite(fiend, evil),
                "A fiendish creature smites good foes only");

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                Assert(!gm.TryNPCDeclareSmiteForAI(p, neutral) && p.Stats.DeclaredSmite == SmiteSource.None,
                    "The AI declaration refuses a neutral target");
                Assert(gm.TryNPCDeclareSmiteForAI(p, evil) && p.Stats.DeclaredSmite == SmiteSource.PaladinSmiteEvil,
                    "The AI declaration goes through the shared SmiteRules declaration");
                SmiteRules.CancelDeclaration(p.Stats);
            }
            else
            {
                Debug.Log("  [SKIP] GameManager.Instance is null; the AI declaration wrapper needs Play mode");
            }
        }
        finally
        {
            Cleanup(p, evil, neutral, fiend, good);
        }
    }

    // ── Lay on Hands ───────────────────────────────────────────────

    /// <summary>Paladin 4, Cha 14: 8 points a day; heals itself 5, then an adjacent ally only 3 (the rest of the pool).</summary>
    private static void TestLayOnHandsHeals()
    {
        CharacterController p = null, ally = null, far = null;
        try
        {
            p = Create("HealPaladin", 4, new Vector2Int(-80, -110), cha: 14);
            ally = Create("HealAlly", 3, new Vector2Int(-79, -110), "Fighter");
            far = Create("FarAlly", 3, new Vector2Int(-75, -110), "Fighter");
            p.Stats.CurrentHP -= 5;
            ally.Stats.CurrentHP -= 10;
            far.Stats.CurrentHP -= 10;

            Assert(!LayOnHandsRules.IsValidTarget(p, far, out _, out string farReason) && farReason == "Not within touch",
                "Lay on Hands reaches only creatures within touch", farReason);

            int hpBefore = p.Stats.CurrentHP;
            LayOnHandsResult self = LayOnHandsRules.Resolve(p, p);
            Assert(self.Performed && self.PointsSpent == 5 && p.Stats.CurrentHP == hpBefore + 5 && !p.Actions.HasStandardAction,
                "Lay on Hands on itself heals the 5 damage with its standard action", self.Log ?? self.Refusal);
            AssertEq(LayOnHandsRules.RemainingPool(p.Stats), 3, "3 of the day's 8 points are left");

            Assert(!LayOnHandsRules.CanUse(p, out string noAction) && noAction == "Standard action used",
                "A second use the same turn needs another standard action", noAction);

            p.StartNewTurn();
            int allyBefore = ally.Stats.CurrentHP;
            LayOnHandsResult other = LayOnHandsRules.Resolve(p, ally);
            Assert(other.Performed && other.PointsSpent == 3 && ally.Stats.CurrentHP == allyBefore + 3,
                "Lay on Hands on an adjacent ally heals what is left of the pool (3)", other.Log ?? other.Refusal);
            Assert(!LayOnHandsRules.CanUse(p, out string empty) && empty == "Pool spent today", "The pool is spent", empty);

            p.Stats.RefreshPaladinDailyUses();
            AssertEq(LayOnHandsRules.RemainingPool(p.Stats), 8, "A full rest restores the pool");
        }
        finally
        {
            Cleanup(p, ally, far);
        }
    }

    /// <summary>A hit with the melee touch deals the points the undead needs (no save); a miss spends none.</summary>
    private static void TestLayOnHandsHarmsUndead()
    {
        CharacterController p = null, undead = null;
        System.Func<int, string, int, int> saved = ScenarioHooks.RollFilter;
        try
        {
            p = Create("HarmPaladin", 6, new Vector2Int(-80, -114), cha: 16); // 18 points
            undead = Create("Zombie", 2, new Vector2Int(-79, -114), "Fighter");
            undead.Stats.CreatureType = "Undead";
            undead.Stats.CharacterAlignment = Alignment.NeutralEvil;
            undead.SetTeam(CharacterTeam.Enemy);

            ScenarioHooks.RollFilter = ForceD20(10, touch: 1);
            LayOnHandsResult miss = LayOnHandsRules.Resolve(p, undead);
            Assert(miss.Performed && miss.HarmsUndead && !miss.TouchHit && miss.PointsSpent == 0 && LayOnHandsRules.RemainingPool(p.Stats) == 18,
                "A missed touch on an undead creature spends the action but no points", miss.Log);

            p.StartNewTurn();
            ScenarioHooks.RollFilter = ForceD20(10, touch: 20);
            undead.Stats.CurrentHP = 7;
            LayOnHandsResult hit = LayOnHandsRules.Resolve(p, undead);
            Assert(hit.Performed && hit.TouchHit && hit.PointsSpent == 7 && hit.DamageDealt == 7 && undead.Stats.CurrentHP <= 0,
                "A hit deals the points the undead creature has left (7) as damage", hit.Log);
            AssertEq(LayOnHandsRules.RemainingPool(p.Stats), 11, "11 of the 18 points are left");
        }
        finally
        {
            ScenarioHooks.RollFilter = saved;
            Cleanup(p, undead);
        }
    }

    private static void TestLayOnHandsRefusals()
    {
        CharacterController p1 = null, lowCha = null, p = null, golem = null, whole = null;
        try
        {
            p1 = Create("Paladin1", 1, new Vector2Int(-80, -118));
            lowCha = Create("LowCha", 4, new Vector2Int(-77, -118), cha: 10);
            p = Create("RefusePaladin", 4, new Vector2Int(-80, -121));
            golem = Create("Golem", 3, new Vector2Int(-79, -121), "Fighter");
            golem.Stats.CreatureType = "Construct";
            golem.Stats.CurrentHP -= 5;
            whole = Create("Unhurt", 3, new Vector2Int(-81, -121), "Fighter");

            Assert(!LayOnHandsRules.CanUse(p1, out string r1) && r1 == "Paladin level 2 required", "No Lay on Hands at 1st level", r1);
            Assert(!LayOnHandsRules.CanUse(lowCha, out string r2) && r2 == "Charisma 12 or higher required", "No Lay on Hands with Charisma 10", r2);
            Assert(!LayOnHandsRules.IsValidTarget(p, golem, out _, out string r3) && r3 == "A construct is not healed", "A construct is not healed", r3);
            Assert(!LayOnHandsRules.IsValidTarget(p, whole, out _, out string r4) && r4 == "Not hurt", "An unhurt creature is no target", r4);
        }
        finally
        {
            Cleanup(p1, lowCha, p, golem, whole);
        }
    }

    private static void TestAiLayOnHandsChoice()
    {
        CharacterController p = null, scratched = null, bleeding = null, undead = null;
        try
        {
            p = Create("AiLohPaladin", 4, new Vector2Int(-80, -126), cha: 14); // 8 points
            p.SetTeam(CharacterTeam.Enemy);
            scratched = Create("Scratched", 3, new Vector2Int(-79, -126), "Fighter");
            scratched.SetTeam(CharacterTeam.Enemy);
            scratched.Stats.CurrentHP -= 2;
            var all = new List<CharacterController> { p, scratched };
            Assert(DND35.AI.AIProfile.DefaultLayOnHandsTarget(p, all) == null, "The AI does not lay on hands while everyone is above half");

            bleeding = Create("Bleeding", 3, new Vector2Int(-81, -126), "Fighter");
            bleeding.SetTeam(CharacterTeam.Enemy);
            bleeding.Stats.CurrentHP = 2;
            all.Add(bleeding);
            Assert(DND35.AI.AIProfile.DefaultLayOnHandsTarget(p, all) == bleeding, "The AI heals an ally within touch at half its hit points or less");

            bleeding.Stats.CurrentHP = bleeding.Stats.TotalMaxHP;
            undead = Create("AiZombie", 2, new Vector2Int(-80, -125), "Fighter");
            undead.Stats.CreatureType = "Undead";
            undead.SetTeam(CharacterTeam.Player);
            undead.Stats.CurrentHP = 6;
            all.Add(undead);
            Assert(DND35.AI.AIProfile.DefaultLayOnHandsTarget(p, all) == undead, "The AI touches an undead foe the pool reduces to 0 hit points");
            undead.Stats.CurrentHP = 20;
            Assert(DND35.AI.AIProfile.DefaultLayOnHandsTarget(p, all) == null, "Not an undead foe the pool cannot reduce to 0 hit points");
        }
        finally
        {
            Cleanup(p, scratched, bleeding, undead);
        }
    }

    // ── Divine Health and Turn Undead ──────────────────────────────

    private static void TestDivineHealth()
    {
        Assert(!Build(2).IsImmuneToDisease(), "Paladin 2 is not immune to disease");
        Assert(Build(3).IsImmuneToDisease(), "Paladin 3 is immune to disease (Divine Health, PHB p.44)");

        CharacterController p = null;
        try
        {
            p = Create("DiseasePaladin", 3, new Vector2Int(-80, -130));
            p.ExposeToDisease(DiseaseType.MummyRot);
            AssertEq(p.ActiveDiseases.Count, 0, "Mummy rot does not take hold of a paladin 3");
        }
        finally
        {
            Cleanup(p);
        }
    }

    private static void TestTurnUndeadLevels()
    {
        Assert(!Build(3).CanEverTurnUndead, "Paladin 3 cannot turn undead");
        AssertEq(Build(4).TurnUndeadEffectiveLevel, 1, "Paladin 4 turns as a 1st-level cleric");
        AssertEq(Build(8).TurnUndeadEffectiveLevel, 5, "Paladin 8 turns as a 5th-level cleric");
        AssertEq(Build(4, cha: 14).MaxTurnUndeadAttemptsPerDay, 5, "Paladin 4 with Cha 14 turns 3 + 2 = 5 times a day");

        CharacterStats multi = Build(5, cha: 10);
        multi.ClassLevels.Add(new ClassLevelEntry("Cleric", 4));
        multi.HitDice = 0;
        multi.InvalidateClassLevelCache();
        multi.EnsureMulticlassDataInitialized();
        AssertEq(multi.TurnUndeadEffectiveLevel, 6, "A paladin 5/cleric 4 turns as a 6th-level cleric (PHB p.59)");

        AssertEq(Build(3, "Cleric", cha: 3).MaxTurnUndeadAttemptsPerDay, 0, "A cleric with Cha 3 (-4) has no turning attempts (3 + Cha modifier, no minimum)");
        AssertEq(Build(5, "Fighter").MaxTurnUndeadAttemptsPerDay, 0, "A fighter has no turning attempts");
    }

    /// <summary>
    /// A grappling paladin neither lays on hands nor turns undead: both are supernatural standard actions, which are not
    /// among the actions a grappler may take (PHB p.156; delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1).
    /// </summary>
    private static void TestGrapplingPaladinUsesNoSupernaturalAction()
    {
        GameManager gm = GameManager.Instance;
        CharacterController p = null, foe = null;
        try
        {
            p = Create("GrapplePaladin", 4, new Vector2Int(-80, -140));
            foe = Create("GrappleFoe", 3, new Vector2Int(-79, -140), "Fighter");
            p.Stats.CurrentHP -= 5;
            Assert(LayOnHandsRules.CanUse(p, out string before), "A hurt paladin 4 may lay on hands before grappling", before);
            if (gm != null)
                Assert(gm.CanUseTurnUndead(p, out string turnBefore), "A paladin 4 may turn undead before grappling", turnBefore);

            MethodInfo establish = typeof(CharacterController).GetMethod("EstablishGrappleWith", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert(establish != null, "CharacterController.EstablishGrappleWith exists (TST-006)");
            if (establish == null)
                return;
            establish.Invoke(p, new object[] { foe });
            Assert(p.IsGrappling(), "The paladin is grappling");
            Assert(!LayOnHandsRules.CanUse(p, out string loh) && loh.StartsWith("Grappling"), "A grappling paladin cannot lay on hands", loh);
            if (gm != null)
                Assert(!gm.CanUseTurnUndead(p, out string turn) && turn.StartsWith("Grappling"), "A grappling paladin cannot turn undead", turn);
            else
                Debug.Log("  [SKIP] GameManager.Instance is null; the turn undead grapple check needs Play mode");
        }
        finally
        {
            Cleanup(p, foe);
        }
    }
}

internal static class PaladinTestExtensions
{
    public static CharacterStats WithAlignment(this CharacterStats stats, Alignment alignment)
    {
        stats.CharacterAlignment = alignment;
        return stats;
    }
}
}

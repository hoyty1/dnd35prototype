#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.AI
{
/// <summary>
/// Batch 11 step 25 (docs/designs/cast_core_batch11.md, npc-area-casting; AI-001 area half): the AI's aim of an area
/// spell (AreaAimSearch) on fixed layouts, so its scores and its chosen origin are deterministic.
/// <list type="bullet">
/// <item>A 5-ft. burst (a test spell, range 30 squares) among three heroes and the caster's ally: each origin's score is
/// the enemies it affects less twice the allies (+(10,11) and +(11,10) take two heroes for 2, +(11,11) three heroes and
/// the ally for 1), and the best is +(10,11), the nearer of the two best (counted from the caster's space), every time.</item>
/// <item>A 10-ft. cone (a test spell) from a hound's square: the best cone takes both heroes and spares the kobold ally,
/// and the same search twice gives the same start, direction and squares.</item>
/// <item>A 60-ft. line through two heroes in a row takes both.</item>
/// <item>A 20-ft. burst at a hero with the caster's ally beside it: the search reaches the origins on the hero's far side
/// that spare the ally (+(10,37) counts 4 to the hero's far corner and 5 to the ally's, PHB p.175), so the best aim takes
/// the hero alone. No worthwhile aim: a 5-ft. burst at a hero between two allies takes an ally from every origin.</item>
/// <item>Prayer (PHB p.264) is aimed like Bless from the caster's corners: every ally and foe in it counts.</item>
/// <item>A beneficial burst centred on the caster (Bless's filter, allies only) counts the allies it helps, never an enemy.</item>
/// <item>Sleep (PHB p.280) affects no creature of 5 or more Hit Dice, so a 5th-level hero is worth nothing.</item>
/// <item>A spell with no judged value (a wall) has no aims; the search scores at most 200 aims.</item>
/// <item>The AI decider cannot fill a wall's ends, a swarm's square or Grease on an object (ICastDecider.CanFill).</item>
/// </list>
/// Play mode (CharacterController.Init needs its Awake, TST-009); no grid: every square exists.
/// </summary>
public static class AreaAimSearchTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== AREA AIM SEARCH TESTS (batch 11 step 25, AI-001 area half) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();

        var made = new List<CharacterController>();
        try
        {
            TestBurstScoresAndOrigin(made);
            TestConeSparesAlly(made);
            TestLineThroughTwo(made);
            TestNoWorthwhileAim(made);
            TestBeneficialBurst(made);
            TestPrayerMixed(made);
            TestSleepHitDice(made);
            TestNoValueAndCap(made);
            TestDeciderCapability();
        }
        finally
        {
            foreach (CharacterController c in made)
                if (c != null)
                    Object.DestroyImmediate(c.gameObject);
            AreaAimSearch.ClearCache();
        }

        Debug.Log($"====== Area Aim Search Results: {_passed} passed, {_failed} failed ======");
    }

    private static CharacterController Make(List<CharacterController> made, string name, CharacterTeam team, int x, int y, int level = 1)
    {
        CharacterController c = TestHelpers.CreateCharacter(name: name, characterClass: "Fighter", level: level);
        c.SetTeam(team);
        TestHelpers.SetGridPosition(c, x, y);
        made.Add(c);
        return c;
    }

    private static SpellData TestSpell(string id, AoEShape shape, int size, int range, SpellEffectType effect, AoETargetFilter filter = AoETargetFilter.All)
    {
        return new SpellData
        {
            SpellId = id,
            Name = id,
            School = "Evocation",
            TargetType = SpellTargetType.Area,
            AoEShapeType = shape,
            AoESizeSquares = size,
            AoERangeSquares = range,
            AoEFilter = filter,
            EffectType = effect,
            DamageCount = 1,
            DamageDice = 6,
            DamageType = "fire"
        };
    }

    private static AreaPlacement Find(List<AreaPlacement> aims, int x, int y)
    {
        foreach (AreaPlacement p in aims)
            if (p.Origin.HasValue && p.Origin.Value == new GridIntersection(x, y))
                return p;
        return null;
    }

    // ── A burst among three heroes and an ally ────────────────────────────────────────────────────────────

    private static void TestBurstScoresAndOrigin(List<CharacterController> made)
    {
        AreaAimSearch.ClearCache();
        CharacterController wizard = Make(made, "Aim Wizard", CharacterTeam.Enemy, 2, 10);
        CharacterController h1 = Make(made, "Hero 1", CharacterTeam.Player, 10, 10);
        CharacterController h2 = Make(made, "Hero 2", CharacterTeam.Player, 11, 10);
        CharacterController h3 = Make(made, "Hero 3", CharacterTeam.Player, 10, 11);
        CharacterController ally = Make(made, "Wizard's Ally", CharacterTeam.Enemy, 11, 11);
        var all = new List<CharacterController> { wizard, h1, h2, h3, ally };
        SpellData burst = TestSpell("test_burst_5", AoEShape.Burst, 1, 30, SpellEffectType.Damage);

        List<AreaPlacement> aims = AreaAimSearch.Search(burst, wizard, all, null);
        AreaPlacement a1011 = Find(aims, 10, 11), a1110 = Find(aims, 11, 10), a1111 = Find(aims, 11, 11), a1112 = Find(aims, 11, 12);
        Assert(a1011 != null && a1011.EnemiesHit == 2 && a1011.AlliesHarmed == 0 && Mathf.Approximately(a1011.Score, 2f),
            "+(10,11) takes heroes 1 and 3: score 2", a1011 != null ? a1011.ToString() : "missing");
        Assert(a1110 != null && a1110.EnemiesHit == 2 && Mathf.Approximately(a1110.Score, 2f),
            "+(11,10) takes heroes 1 and 2: score 2", a1110 != null ? a1110.ToString() : "missing");
        Assert(a1111 != null && a1111.EnemiesHit == 3 && a1111.AlliesHarmed == 1 && Mathf.Approximately(a1111.Score, 1f),
            "+(11,11) takes all three heroes and the ally: 3 - 2 x 1 = 1", a1111 != null ? a1111.ToString() : "missing");
        Assert(a1112 != null && a1112.EnemiesHit == 1 && a1112.AlliesHarmed == 1 && Mathf.Approximately(a1112.Score, -1f),
            "+(11,12) takes hero 3 and the ally: 1 - 2 = -1", a1112 != null ? a1112.ToString() : "missing");
        Assert(a1011 != null && a1110 != null && a1011.Order < a1110.Order,
            "the origins are searched nearest the caster first (+(10,11) is 7 squares from the caster's space, +(11,10) 8)");

        AreaPlacement best = AreaAimSearch.FindBest(burst, wizard, all, null);
        Assert(best != null && best.Origin == new GridIntersection(10, 11) && best.EnemiesHit == 2 && best.AlliesHit == 0,
            "the best burst is +(10,11): the higher score, the ally spared, the nearer of the equals", best != null ? best.ToString() : "none");
        AreaAimSearch.ClearCache();
        AreaPlacement again = AreaAimSearch.FindBest(burst, wizard, all, null);
        Assert(again != null && best != null && again.Origin == best.Origin && Mathf.Approximately(again.Score, best.Score)
               && again.Cells.SetEquals(best.Cells),
            "the same layout gives the same aim and score again (deterministic)");

        // A tighter ally weight changes nothing here; a profile's own test can veto an aim.
        AreaPlacement vetoed = AreaAimSearch.FindBest(burst, wizard, all, null, new AreaAimOptions { Accept = p => p.Origin != new GridIntersection(10, 11) });
        Assert(vetoed != null && vetoed.Origin == new GridIntersection(11, 10),
            "an aim the options' test refuses passes to the next best (+(11,10))", vetoed != null ? vetoed.ToString() : "none");
        Clear(made);
    }

    // ── A cone from a hound's square ──────────────────────────────────────────────────────────────────────

    private static void TestConeSparesAlly(List<CharacterController> made)
    {
        AreaAimSearch.ClearCache();
        CharacterController hound = Make(made, "Hound", CharacterTeam.Enemy, 5, 20);
        CharacterController hero1 = Make(made, "Cone Hero 1", CharacterTeam.Player, 7, 20);
        CharacterController hero2 = Make(made, "Cone Hero 2", CharacterTeam.Player, 6, 19);
        CharacterController kobold = Make(made, "Kobold", CharacterTeam.Enemy, 7, 21);
        var all = new List<CharacterController> { hound, hero1, hero2, kobold };
        SpellData cone = TestSpell("test_cone_10", AoEShape.Cone, 2, 0, SpellEffectType.Damage);

        AreaPlacement best = AreaAimSearch.FindBest(cone, hound, all, null);
        Assert(best != null && best.Cells.Contains(hero1.GridPosition) && best.Cells.Contains(hero2.GridPosition)
               && !best.Cells.Contains(kobold.GridPosition) && best.EnemiesHit == 2 && best.AlliesHit == 0 && best.DirIndex >= 0,
            "the best 10-ft. cone takes both heroes and spares the kobold", best != null ? best.ToString() : "none");
        Assert(best != null && best.Origin.HasValue && AoESystem.GetSpaceEdgeIntersections(hound.GridPosition, 1).Contains(best.Origin.Value),
            "the cone starts at a corner of the hound's square (PHB p.175)", best != null ? "" + best.Origin : "none");
        AreaAimSearch.ClearCache();
        AreaPlacement again = AreaAimSearch.FindBest(cone, hound, all, null);
        Assert(best != null && again != null && again.Origin == best.Origin && again.DirIndex == best.DirIndex && again.Cells.SetEquals(best.Cells),
            "the same layout gives the same cone again (deterministic)", again != null ? again.ToString() : "none");
        Clear(made);
    }

    // ── A line through two heroes ─────────────────────────────────────────────────────────────────────────

    private static void TestLineThroughTwo(List<CharacterController> made)
    {
        AreaAimSearch.ClearCache();
        CharacterController caster = Make(made, "Line Caster", CharacterTeam.Enemy, 2, 30);
        CharacterController near = Make(made, "Near Hero", CharacterTeam.Player, 5, 31);
        CharacterController far = Make(made, "Far Hero", CharacterTeam.Player, 8, 32);
        var all = new List<CharacterController> { caster, near, far };
        SpellData line = TestSpell("test_line_60", AoEShape.Line, 12, 0, SpellEffectType.Damage);
        AreaPlacement best = AreaAimSearch.FindBest(line, caster, all, null);
        Assert(best != null && best.EnemiesHit == 2 && best.Cells.Contains(near.GridPosition) && best.Cells.Contains(far.GridPosition),
            "the best 60-ft. line runs through both heroes", best != null ? best.ToString() : "none");
        Clear(made);
    }

    // ── No worthwhile aim ─────────────────────────────────────────────────────────────────────────────────

    private static void TestNoWorthwhileAim(List<CharacterController> made)
    {
        AreaAimSearch.ClearCache();
        CharacterController wizard = Make(made, "Careful Wizard", CharacterTeam.Enemy, 2, 40);
        CharacterController hero = Make(made, "Lone Hero", CharacterTeam.Player, 10, 40);
        CharacterController ally = Make(made, "Adjacent Ally", CharacterTeam.Enemy, 10, 41);
        var all = new List<CharacterController> { wizard, hero, ally };
        SpellData burst = TestSpell("test_burst_20", AoEShape.Burst, 4, 30, SpellEffectType.Damage);
        List<AreaPlacement> aims = AreaAimSearch.Search(burst, wizard, all, null);
        AreaPlacement far = Find(aims, 10, 37);
        Assert(far != null && far.EnemiesHit == 1 && far.AlliesHarmed == 0 && Mathf.Approximately(far.Score, 1f),
            "+(10,37), on the hero's far side, takes the hero (count 4 to its far corner) and spares the ally (count 5): score 1",
            far != null ? far.ToString() : "not searched");
        AreaPlacement best = AreaAimSearch.FindBest(burst, wizard, all, null);
        Assert(best != null && best.EnemiesHit == 1 && best.AlliesHarmed == 0 && Mathf.Approximately(best.Score, 1f)
               && best.Cells.Contains(hero.GridPosition) && !best.Cells.Contains(ally.GridPosition),
            "the best 20-ft. burst takes the hero and spares the ally beside it", best != null ? best.ToString() : "none");
        Clear(made);

        AreaAimSearch.ClearCache();
        wizard = Make(made, "Careful Wizard", CharacterTeam.Enemy, 2, 40);
        hero = Make(made, "Flanked Hero", CharacterTeam.Player, 10, 40);
        CharacterController west = Make(made, "West Ally", CharacterTeam.Enemy, 9, 40);
        CharacterController east = Make(made, "East Ally", CharacterTeam.Enemy, 11, 40);
        all = new List<CharacterController> { wizard, hero, west, east };
        SpellData small = TestSpell("test_burst_5_flanked", AoEShape.Burst, 1, 30, SpellEffectType.Damage);
        best = AreaAimSearch.FindBest(small, wizard, all, null);
        Assert(best == null, "every 5-ft. burst that reaches a hero between two allies reaches one of them (1 - 2 < 0): no aim, the spell is dropped",
            best != null ? best.ToString() : "none");
        aims = AreaAimSearch.Search(small, wizard, all, null);
        bool allHitBoth = aims.Count > 0;
        foreach (AreaPlacement p in aims)
            if (p.EnemiesHit > 0 && p.AlliesHarmed == 0)
                allHitBoth = false;
        Assert(allHitBoth, "no searched aim takes the hero without an ally", "aims " + aims.Count);
        Clear(made);
    }

    // ── A beneficial burst on the caster ──────────────────────────────────────────────────────────────────

    private static void TestBeneficialBurst(List<CharacterController> made)
    {
        AreaAimSearch.ClearCache();
        CharacterController priest = Make(made, "Priest", CharacterTeam.Enemy, 20, 10);
        CharacterController a1 = Make(made, "Guard 1", CharacterTeam.Enemy, 21, 10);
        CharacterController a2 = Make(made, "Guard 2", CharacterTeam.Enemy, 19, 11);
        CharacterController foe = Make(made, "Intruder", CharacterTeam.Player, 22, 11);
        var all = new List<CharacterController> { priest, a1, a2, foe };
        SpellData bless = TestSpell("test_bless", AoEShape.Burst, 10, 0, SpellEffectType.Buff, AoETargetFilter.AlliesOnly);
        Assert(AreaAimSearch.PolarityOf(bless) == AreaAimPolarity.Beneficial, "a buff whose filter takes only allies helps (Bless)");
        AreaPlacement best = AreaAimSearch.FindBest(bless, priest, all, null);
        Assert(best != null && best.AlliesBenefited == 3 && best.EnemiesHit == 0 && Mathf.Approximately(best.Score, 3f)
               && AoESystem.GetCreatureCentredOrigins(priest).Contains(best.Origin.Value),
            "the 50-ft. burst on the priest helps the priest and both guards (3) and counts no foe", best != null ? best.ToString() : "none");
        Clear(made);
    }

    // ── Prayer: allies and foes ───────────────────────────────────────────────────────────────────────────

    private static void TestPrayerMixed(List<CharacterController> made)
    {
        AreaAimSearch.ClearCache();
        SpellData prayer = SpellDatabase.GetSpell(SpellNames.PRAYER);
        CharacterController priest = Make(made, "Prayer Priest", CharacterTeam.Enemy, 20, 30);
        CharacterController guard = Make(made, "Prayer Guard", CharacterTeam.Enemy, 21, 30);
        CharacterController foe = Make(made, "Prayer Foe", CharacterTeam.Player, 23, 31);
        var all = new List<CharacterController> { priest, guard, foe };
        Assert(prayer != null && AreaAimSearch.IsAreaCastForAI(prayer) && AreaAimSearch.PolarityOf(prayer) == AreaAimPolarity.Mixed,
            "Prayer is cast as an area by the AI, as the PC's targeting routes it (AoESystem.RoutesToArea), and counts allies and foes");
        AreaPlacement best = AreaAimSearch.FindBest(prayer, priest, all, null);
        Assert(best != null && best.AlliesBenefited == 2 && best.EnemiesHit == 1 && Mathf.Approximately(best.Score, 3f)
               && AoESystem.GetCreatureCentredOrigins(priest).Contains(best.Origin.Value),
            "the 40-ft. burst on the priest helps the priest and the guard and hinders the foe: score 3", best != null ? best.ToString() : "none");
        Clear(made);
    }

    // ── Sleep's Hit Dice limit ────────────────────────────────────────────────────────────────────────────

    private static void TestSleepHitDice(List<CharacterController> made)
    {
        AreaAimSearch.ClearCache();
        SpellData sleep = SpellDatabase.GetSpell(SpellNames.SLEEP);
        CharacterController low = Make(made, "Sleepy Hero", CharacterTeam.Player, 30, 10, level: 1);
        CharacterController high = Make(made, "Veteran Hero", CharacterTeam.Player, 30, 12, level: 5);
        Assert(sleep != null && AreaAimSearch.WouldAffect(sleep, low) && !AreaAimSearch.WouldAffect(sleep, high),
            "Sleep affects a 1-HD hero but not a 5-HD one (PHB p.280: 4 HD or fewer)");
        Clear(made);
    }

    // ── No value, and the cap ─────────────────────────────────────────────────────────────────────────────

    private static void TestNoValueAndCap(List<CharacterController> made)
    {
        AreaAimSearch.ClearCache();
        CharacterController wizard = Make(made, "Cap Wizard", CharacterTeam.Enemy, 0, 0);
        var all = new List<CharacterController> { wizard };
        for (int i = 0; i < 30; i++)
            all.Add(Make(made, "Crowd " + i, CharacterTeam.Player, 4 + (i % 6) * 5, 4 + (i / 6) * 5));
        SpellData wall = TestSpell("test_wall", AoEShape.Cube, 2, 30, SpellEffectType.Wall);
        Assert(AreaAimSearch.PolarityOf(wall) == AreaAimPolarity.None && AreaAimSearch.Search(wall, wizard, all, null).Count == 0,
            "a wall has no value the AI judges: no aims");
        SpellData burst = TestSpell("test_burst_cap", AoEShape.Burst, 2, 60, SpellEffectType.Damage);
        int count = AreaAimSearch.Search(burst, wizard, all, null).Count;
        Assert(count > 0 && count <= AreaAimSearch.MaxCandidates, "the search scores at most " + AreaAimSearch.MaxCandidates + " aims", "count " + count);
        Clear(made);
    }

    // ── The AI decider's capability ───────────────────────────────────────────────────────────────────────

    private static void TestDeciderCapability()
    {
        var cells = new HashSet<Vector2Int> { Vector2Int.zero };
        Assert(!AiCastDecider.Instance.CanFill(SpellCastRequest.ForAIArea(null, SpellDatabase.GetSpell(SpellNames.WALL_OF_FIRE), cells, null, null))
               && !AiCastDecider.Instance.CanFill(SpellCastRequest.ForAIArea(null, SpellDatabase.GetSpell(SpellNames.WALL_OF_ICE), cells, null, null))
               && !AiCastDecider.Instance.CanFill(SpellCastRequest.ForAIArea(null, SpellDatabase.GetSpell(SpellNames.WALL_OF_FORCE), cells, null, null))
               && !AiCastDecider.Instance.CanFill(SpellCastRequest.ForAIArea(null, SpellDatabase.GetSpell(SpellNames.SUMMON_SWARM), cells, null, null)),
            "the AI decider cannot place a wall's ends or a summoned swarm");
        SpellCastRequest greaseObject = SpellCastRequest.ForAIGrease(null, SpellDatabase.GetSpell(SpellNames.GREASE), cells, null, null);
        greaseObject.Choices.GreaseMode = GreaseCastMode.Object;
        Assert(!AiCastDecider.Instance.CanFill(greaseObject)
               && AiCastDecider.Instance.CanFill(SpellCastRequest.ForAIGrease(null, SpellDatabase.GetSpell(SpellNames.GREASE), cells, null, null))
               && AiCastDecider.Instance.CanFill(SpellCastRequest.ForAIArea(null, SpellDatabase.GetSpell(SpellNames.FIREBALL), cells, null, null)),
            "the AI decider fills Grease's area form and a Fireball, not Grease on an object");
        Assert(AreaAimSearch.IsAreaCastForAI(SpellDatabase.GetSpell(SpellNames.FIREBALL)) && AreaAimSearch.IsAreaCastForAI(SpellDatabase.GetSpell(SpellNames.GREASE))
               && AreaAimSearch.IsAreaCastForAI(SpellDatabase.GetSpell(SpellNames.PRAYER)) && !AreaAimSearch.IsAreaCastForAI(SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE)),
            "the AI casts Area spells, Prayer's burst and Grease as areas, as the PC's targeting routes them; Magic Missile stays a single-target cast");
    }

    private static void Clear(List<CharacterController> made)
    {
        foreach (CharacterController c in made)
            if (c != null)
                Object.DestroyImmediate(c.gameObject);
        made.Clear();
    }

    private static void Assert(bool condition, string label, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log("  PASS: " + label);
        }
        else
        {
            _failed++;
            Debug.LogError("  FAIL: " + label + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
        }
    }
}
}
#endif

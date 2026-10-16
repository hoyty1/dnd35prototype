#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 23 (docs/designs/cast_core_batch11.md, area-routing-data): area spells routed by geometry, area side
/// filtering relative to the caster, and the shapeless area spells' PHB areas.
/// <list type="bullet">
/// <item>SPL-119: AoESystem.GetTargetsInArea filters with TeamUtility relative to the caster, the same for a Player-team
/// and an Enemy-team caster: AlliesOnly is the caster and its allies (Bless, PHB p.205), EnemiesOnly its enemies (Bane,
/// PHB p.203), All every living creature, Self the caster; a dead creature is never taken.</item>
/// <item>AoESystem.GetCubeCells: a cube is centred on its point of origin, a grid intersection (batch 11 step 24); an odd
/// side lies half a square north-east of it; squares off the grid are left out.</item>
/// <item>Routing (AoESystem.RoutesToArea): every spell it routes to the area targeting has an area shape (Summon Swarm,
/// an Area spell that places a swarm, is routed to its placement first), so no Area spell resolves against one clicked
/// creature; the eleven spells of SPL-046 and SPL-042 carry their PHB areas and ranges, and Passwall (no area, no
/// creature) is cast with no target.</item>
/// <item>Wall of Thorns (PHB p.300): 25 minus the AC without Dexterity and dodge bonuses, none at 25 or more, none with
/// Woodland Stride (GameManager.WallOfThornsDamage).</item>
/// <item>Consecrate and Desecrate (PHB p.212, p.218): an undead creature in the area gets -1 untyped or +1 profane on
/// attack rolls, weapon damage rolls and saves through the bonus ledger (HolyAreaRules), removed on leaving.</item>
/// <item>The alignment bursts' outsider dice: 1d6 per caster level, max 10d6 (PHB p.208, p.241, p.258, p.297).</item>
/// </list>
/// Play mode (a test grid of its own; CharacterController.Init needs the components its Awake sets up).
/// </summary>
public static class AreaRoutingTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== AREA ROUTING TESTS (batch 11 step 23, SPL-119, SPL-046, SPL-042) ======");

        TestHelpers.EnsureCoreDatabasesInitialized();
        SpellDatabase.Init();

        TestTargetFilter(CharacterTeam.Player);
        TestTargetFilter(CharacterTeam.Enemy);
        TestCubeCells();
        TestRoutingData();
        TestWallOfThornsDamage();
        TestHolyAreaModifiers();
        TestOutsiderDice();

        Debug.Log($"====== Area Routing Results: {_passed} passed, {_failed} failed ======");
    }

    // ── SPL-119 ───────────────────────────────────────────────────────────────────────────────────────────

    private static void TestTargetFilter(CharacterTeam casterTeam)
    {
        CharacterTeam otherTeam = casterTeam == CharacterTeam.Player ? CharacterTeam.Enemy : CharacterTeam.Player;
        string side = casterTeam == CharacterTeam.Player ? "Player-team caster" : "Enemy-team caster";
        SquareGrid previousGrid = SquareGrid.Instance;
        GameObject gridGo = null;
        var made = new List<CharacterController>();
        try
        {
            gridGo = new GameObject("AreaRouting_Grid");
            SquareGrid grid = gridGo.AddComponent<SquareGrid>();
            grid.Width = 10;
            grid.Height = 4;
            grid.GenerateGrid();

            CharacterController Make(string name, CharacterTeam team, int x)
            {
                CharacterController c = TestHelpers.CreateCharacter(name: name);
                c.SetTeam(team);
                TestHelpers.SetGridPosition(c, x, 1);
                grid.SetCreatureOccupancy(c, c.GridPosition, 1);
                made.Add(c);
                return c;
            }

            CharacterController caster = Make("Caster", casterTeam, 1);
            CharacterController ally = Make("Ally", casterTeam, 2);
            CharacterController foe = Make("Foe", otherTeam, 3);
            CharacterController foe2 = Make("Foe2", otherTeam, 4);
            CharacterController neutral = Make("Bystander", CharacterTeam.Neutral, 5);
            CharacterController deadAlly = Make("DeadAlly", casterTeam, 6);
            deadAlly.Stats.CurrentHP = -20;
            Assert(deadAlly.Stats.IsDead, side + ": precondition, the dead ally is dead");

            var cells = new HashSet<Vector2Int>();
            for (int x = 0; x < 10; x++)
                cells.Add(new Vector2Int(x, 1));

            string Names(List<CharacterController> list)
            {
                var names = new List<string>();
                foreach (CharacterController c in list)
                    names.Add(c.Stats.CharacterName);
                names.Sort(StringComparer.Ordinal);
                return string.Join(",", names);
            }

            void Check(AoETargetFilter filter, string want, string rule)
            {
                string got = Names(AoESystem.GetTargetsInArea(cells, caster, filter, grid));
                Assert(got == want, side + " " + filter + ": " + rule, "got [" + got + "], expected [" + want + "]");
            }

            Check(AoETargetFilter.AlliesOnly, "Ally,Caster", "the caster and its allies (PHB p.205), not its foes, the neutral or the dead");
            Check(AoETargetFilter.EnemiesOnly, "Foe,Foe2", "only the caster's enemies (PHB p.203)");
            Check(AoETargetFilter.All, "Ally,Bystander,Caster,Foe,Foe2", "every living creature in the area");
            Check(AoETargetFilter.Self, "Caster", "only the caster");

            Assert(AoESystem.IncludedByFilter(caster, ally, AoETargetFilter.AlliesOnly) && !AoESystem.IncludedByFilter(caster, foe, AoETargetFilter.AlliesOnly)
                   && AoESystem.IncludedByFilter(caster, foe, AoETargetFilter.EnemiesOnly) && !AoESystem.IncludedByFilter(caster, ally, AoETargetFilter.EnemiesOnly),
                side + ": IncludedByFilter judges by the caster's side (TeamUtility)");
        }
        finally
        {
            if (gridGo != null) Object.DestroyImmediate(gridGo);
            // SquareGrid.Awake replaced the static Instance; put the previous grid back (GRID-010).
            typeof(SquareGrid).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.SetValue(null, previousGrid);
            foreach (CharacterController c in made)
                TestHelpers.Cleanup(c != null ? c.gameObject : null);
        }
    }

    // ── Cube footprint ────────────────────────────────────────────────────────────────────────────────────

    private static void TestCubeCells()
    {
        SquareGrid previousGrid = SquareGrid.Instance;
        GameObject gridGo = null;
        try
        {
            gridGo = new GameObject("AreaRouting_CubeGrid");
            SquareGrid grid = gridGo.AddComponent<SquareGrid>();
            grid.Width = 12;
            grid.Height = 12;
            grid.GenerateGrid();

            // A cube is centred on its point of origin, a grid intersection (batch 11 step 24, PHB p.175): +(6,6) is the
            // north-east corner of square (5,5).
            var centre = new GridIntersection(6, 6);
            HashSet<Vector2Int> two = AoESystem.GetCubeCells(centre, 2, grid);
            Assert(two.Count == 4 && two.Contains(new Vector2Int(5, 5)) && two.Contains(new Vector2Int(6, 5))
                   && two.Contains(new Vector2Int(5, 6)) && two.Contains(new Vector2Int(6, 6)),
                "a 10-ft. cube (2 squares) is the four squares around its intersection (Wall of Thorns, PHB p.300)", "count " + two.Count);

            HashSet<Vector2Int> six = AoESystem.GetCubeCells(centre, 6, grid);
            bool sixSpan = six.Count == 36 && six.Contains(new Vector2Int(3, 3)) && six.Contains(new Vector2Int(8, 8))
                           && !six.Contains(new Vector2Int(2, 5)) && !six.Contains(new Vector2Int(9, 5));
            Assert(sixSpan, "a 30-ft. cube (6 squares) spans x and y 3..8 around intersection (6,6) (Order's Wrath, PHB p.258)", "count " + six.Count);

            HashSet<Vector2Int> three = AoESystem.GetCubeCells(centre, 3, grid);
            Assert(three.Count == 9 && three.Contains(new Vector2Int(5, 5)) && three.Contains(new Vector2Int(7, 7)) && !three.Contains(new Vector2Int(4, 4)),
                "a cube of an odd side (no spell has one) lies half a square north-east of its intersection", "count " + three.Count);

            HashSet<Vector2Int> corner = AoESystem.GetCubeCells(new GridIntersection(1, 1), 6, grid);
            Assert(corner.Count == 16, "squares off the grid are left out (a 30-ft. cube at the corner keeps 4 by 4)", "count " + corner.Count);

            SpellData ordersWrath = SpellDatabase.GetSpell(SpellNames.ORDERS_WRATH);
            Assert(AoESystem.GetPlacedAreaCells(ordersWrath, centre, grid).SetEquals(six),
                "GetPlacedAreaCells gives Order's Wrath its 30-ft. cube");
            SpellData holySmite = SpellDatabase.GetSpell(SpellNames.HOLY_SMITE);
            HashSet<Vector2Int> smite = AoESystem.GetPlacedAreaCells(holySmite, centre, grid);
            Assert(smite.SetEquals(AoESystem.GetBurstCells(centre, 4, grid)) && smite.Count == 44,
                "GetPlacedAreaCells gives Holy Smite its 20-ft.-radius burst (44 squares)", "count " + smite.Count);
            Assert(AoESystem.IsPlacedShape(AoEShape.Burst) && AoESystem.IsPlacedShape(AoEShape.Cube) && !AoESystem.IsPlacedShape(AoEShape.Cone)
                   && !AoESystem.IsPlacedShape(AoEShape.Line) && !AoESystem.IsPlacedShape(AoEShape.None),
                "bursts and cubes are placed at a point; cones and lines are aimed from the caster");
        }
        finally
        {
            if (gridGo != null) Object.DestroyImmediate(gridGo);
            typeof(SquareGrid).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.SetValue(null, previousGrid);
        }
    }

    // ── Routing and data ──────────────────────────────────────────────────────────────────────────────────

    private static void TestRoutingData()
    {
        int routed = 0;
        foreach (SpellData spell in SpellDatabase.GetAllSpells())
        {
            if (spell == null || !AoESystem.RoutesToArea(spell))
                continue;
            if (SummoningService.IsSummonSwarmSpell(spell))
                continue; // placed by its own prompt before the area routing
            routed++;
            Assert(spell.AoEShapeType != AoEShape.None, "an area-routed spell has an area shape: " + spell.SpellId,
                "target " + spell.TargetType + ", shape " + spell.AoEShapeType);
            // (A size of 0 is a one-square area: Call Lightning's bolt, Flaming Sphere's square.)
        }
        Assert(routed > 50, "the routing sees the database's area spells", "routed " + routed);

        SpellData swarm = SpellDatabase.GetSpell(SpellNames.SUMMON_SWARM);
        Assert(swarm != null && SummoningService.IsSummonSwarmSpell(swarm), "Summon Swarm is placed by its own prompt (routed before the area spells)");

        // (id, shape, size in squares, filter, range category, PHB)
        var expected = new (string id, AoEShape shape, int size, AoETargetFilter filter, SpellRangeCategory range, string phb)[]
        {
            (SpellNames.CHAOS_HAMMER, AoEShape.Burst, 4, AoETargetFilter.All, SpellRangeCategory.Medium, "PHB p.208: 20-ft.-radius burst, Medium"),
            (SpellNames.HOLY_SMITE, AoEShape.Burst, 4, AoETargetFilter.All, SpellRangeCategory.Medium, "PHB p.241: 20-ft.-radius burst, Medium"),
            (SpellNames.ORDERS_WRATH, AoEShape.Cube, 6, AoETargetFilter.All, SpellRangeCategory.Medium, "PHB p.258: a burst that fills a 30-ft. cube, Medium"),
            (SpellNames.UNHOLY_BLIGHT, AoEShape.Burst, 4, AoETargetFilter.All, SpellRangeCategory.Medium, "PHB p.297: 20-ft.-radius spread, Medium"),
            (SpellNames.SOUND_BURST, AoEShape.Burst, 2, AoETargetFilter.All, SpellRangeCategory.Close, "PHB p.281: 10-ft.-radius spread, Close"),
            (SpellNames.CONSECRATE, AoEShape.Burst, 4, AoETargetFilter.All, SpellRangeCategory.Close, "PHB p.212: 20-ft.-radius emanation, Close"),
            (SpellNames.DESECRATE, AoEShape.Burst, 4, AoETargetFilter.All, SpellRangeCategory.Close, "PHB p.218: 20-ft.-radius emanation, Close"),
            (SpellNames.CALM_ANIMALS, AoEShape.Burst, 3, AoETargetFilter.EnemiesOnly, SpellRangeCategory.Close, "PHB p.207: animals within 30 ft. of each other, Close"),
            (SpellNames.WALL_OF_THORNS, AoEShape.Cube, 2, AoETargetFilter.All, SpellRangeCategory.Medium, "PHB p.300: one 10-ft. cube, Medium"),
            (SpellNames.PERSISTENT_IMAGE, AoEShape.Cube, 4, AoETargetFilter.All, SpellRangeCategory.Long, "PHB p.260: a 20-ft. cube, Long"),
        };
        foreach (var row in expected)
        {
            SpellData spell = SpellDatabase.GetSpell(row.id);
            bool ok = spell != null && spell.TargetType == SpellTargetType.Area && spell.AoEShapeType == row.shape && spell.AoESizeSquares == row.size
                      && spell.AoEFilter == row.filter && spell.AoERangeSquares == 0 && spell.GetEffectiveRangeCategory() == row.range
                      && AoESystem.RoutesToArea(spell) && SpellRoutingGoldenTests.PcRoutingBranch(spell) == "aoe";
            Assert(ok, row.id + " has its PHB area and is routed to the area targeting (" + row.phb + ")",
                spell == null ? "missing" : spell.TargetType + " " + spell.AoEShapeType + " " + spell.AoESizeSquares + " " + spell.AoEFilter + " " + spell.GetEffectiveRangeCategory());
            // NPC area casting stays refused until batch 11 step 25 (AI-001).
            Assert(spell != null && SpellRoutingGoldenTests.NpcRefusesOnData(spell), row.id + ": the NPC cast path still refuses an Area spell (AI-001, step 25)");
        }

        SpellData bane = SpellDatabase.GetSpell(SpellNames.BANE);
        bool baneOk = bane != null && bane.TargetType == SpellTargetType.Area && bane.AoEShapeType == AoEShape.Burst && bane.AoESizeSquares == 10
                      && bane.AoEFilter == AoETargetFilter.EnemiesOnly && bane.AoERangeSquares == 0 && bane.GetRangeSquaresForCasterLevel(5) <= 0
                      && SpellRoutingGoldenTests.PcRoutingBranch(bane) == "aoe";
        Assert(baneOk, "Bane is the caster's enemies within a 50-ft. burst centred on the caster, as Bless's allies (PHB p.203, p.205)",
            bane == null ? "missing" : bane.TargetType + " " + bane.AoEShapeType + " " + bane.AoESizeSquares + " " + bane.AoEFilter + " range " + bane.GetRangeSquaresForCasterLevel(5));
        Assert(bane != null && bane.SpellResistanceApplies && bane.IsMindAffecting,
            "Bane allows spell resistance and is mind-affecting (PHB p.203: SR yes; [Fear, Mind-Affecting])");
        Assert(bane != null && (bane.Descriptors & (SpellDescriptor.Fear | SpellDescriptor.MindAffecting)) == (SpellDescriptor.Fear | SpellDescriptor.MindAffecting)
                && (bane.Descriptors & (SpellDescriptor.Evil | SpellDescriptor.Good | SpellDescriptor.Lawful | SpellDescriptor.Chaotic)) == 0,
            "Bane's descriptors are [Fear, Mind-Affecting] with no alignment descriptor (PHB p.203), so no domain raises its caster level",
            bane == null ? "missing" : bane.Descriptors.ToString());
        SpellData bless = SpellDatabase.GetSpell(SpellNames.BLESS);
        Assert(bless != null && bless.AoEFilter == AoETargetFilter.AlliesOnly && bless.AoESizeSquares == bane.AoESizeSquares,
            "Bless has the same burst with the allies filter (PHB p.205)");

        SpellData passwall = SpellDatabase.GetSpell(SpellNames.PASSWALL);
        Assert(passwall != null && !AoESystem.RoutesToArea(passwall) && SpellRoutingGoldenTests.PcRoutingBranch(passwall) == "self",
            "Passwall (an opening in a wall, PHB p.259) affects no creature or area: cast with no target, not routed to the area targeting",
            passwall == null ? "missing" : passwall.TargetType + " " + SpellRoutingGoldenTests.PcRoutingBranch(passwall));

        SpellData prayer = SpellDatabase.GetSpell(SpellNames.PRAYER);
        Assert(prayer != null && prayer.TargetType == SpellTargetType.Self && AoESystem.RoutesToArea(prayer) && !SpellRoutingGoldenTests.NpcRefusesOnData(prayer),
            "Prayer (Self with a burst) is routed to the area targeting for a PC; the NPC path keeps its burst resolver (rules/prayer-cast)");
    }

    // ── Wall of Thorns ────────────────────────────────────────────────────────────────────────────────────

    private static void TestWallOfThornsDamage()
    {
        CharacterStats fighter = TestHelpers.CreateStats(name: "Thorn Fighter", characterClass: "Fighter", level: 3, dex: 14);
        int dex = Mathf.Max(0, fighter.DexToArmorClass);
        Assert(dex == 2, "precondition: the fighter adds Dexterity +2 to AC", "dex to AC " + dex);
        int withoutDex = fighter.ArmorClass - dex - fighter.DodgeACBonus - Mathf.Max(0, fighter.RacialDodgeACBonus);
        Assert(fighter.ArmorClassWithoutDexAndDodge == withoutDex, "the wall's AC leaves out the Dexterity bonus (PHB p.300)",
            "AC " + fighter.ArmorClass + ", without " + fighter.ArmorClassWithoutDexAndDodge);
        AssertEq(GameManager.WallOfThornsDamage(fighter), 25 - withoutDex, "Wall of Thorns deals 25 minus the AC without Dexterity (PHB p.300)");

        fighter.HasteACBonus = 1; // a dodge bonus (PHB p.239)
        AssertEq(fighter.ArmorClassWithoutDexAndDodge, withoutDex, "a dodge bonus does not count against the thorns either (PHB p.300)");
        fighter.HasteACBonus = 0;

        fighter.NaturalArmorBonus = 30;
        AssertEq(GameManager.WallOfThornsDamage(fighter), 0, "an AC of 25 or more without Dexterity and dodge takes no damage (PHB p.300; was a minimum of 1)");
        fighter.NaturalArmorBonus = 0;

        CharacterStats clumsy = TestHelpers.CreateStats(name: "Thorn Clumsy", characterClass: "Fighter", level: 3, dex: 6);
        Assert(clumsy.ArmorClassWithoutDexAndDodge == clumsy.ArmorClass, "a Dexterity penalty still counts (only bonuses are left out)",
            "AC " + clumsy.ArmorClass + ", without " + clumsy.ArmorClassWithoutDexAndDodge);

        CharacterStats druid = TestHelpers.CreateStats(name: "Thorn Druid", characterClass: "Druid", level: 3, wis: 16);
        Assert(druid.HasWoodlandStride, "precondition: a druid 3 has Woodland Stride (PHB p.36)");
        AssertEq(GameManager.WallOfThornsDamage(druid), 0, "a creature with Woodland Stride passes through the thorns unharmed (PHB p.300)");
    }

    // ── Consecrate and Desecrate ──────────────────────────────────────────────────────────────────────────

    private static void TestHolyAreaModifiers()
    {
        CharacterStats undead = TestHelpers.CreateStats(name: "Holy Undead");
        int attack = undead.Bonuses.Total(BonusTarget.AttackRoll);
        int damage = undead.Bonuses.Total(BonusTarget.WeaponDamage);
        int saves = undead.Bonuses.Total(BonusTarget.AllSaves);

        HolyAreaRules.SetConsecrated(undead, true);
        Assert(undead.ConsecrateActive && undead.Bonuses.Total(BonusTarget.AttackRoll) == attack - 1
               && undead.Bonuses.Total(BonusTarget.WeaponDamage) == damage - 1 && undead.Bonuses.Total(BonusTarget.AllSaves) == saves - 1,
            "Consecrate: -1 on attack rolls, damage rolls and saves (PHB p.212)");
        HolyAreaRules.SetConsecrated(undead, true);
        AssertEq(undead.Bonuses.Total(BonusTarget.AttackRoll), attack - 1, "a second Consecrate does not stack (one source, PHB p.171)");
        HolyAreaRules.SetConsecrated(undead, false);
        Assert(!undead.ConsecrateActive && undead.Bonuses.Total(BonusTarget.AttackRoll) == attack && undead.Bonuses.Total(BonusTarget.AllSaves) == saves,
            "leaving the consecrated area removes the penalty");

        HolyAreaRules.SetDesecrated(undead, true);
        Assert(undead.DesecrateActive && undead.Bonuses.Total(BonusTarget.AttackRoll) == attack + 1
               && undead.Bonuses.Total(BonusTarget.WeaponDamage) == damage + 1 && undead.Bonuses.Total(BonusTarget.AllSaves) == saves + 1,
            "Desecrate: +1 profane on attack rolls, damage rolls and saves (PHB p.218)");
        HolyAreaRules.SetDesecrated(undead, false);
        Assert(!undead.DesecrateActive && undead.Bonuses.Total(BonusTarget.AttackRoll) == attack, "leaving the desecrated area removes the bonus");
    }

    // ── Alignment burst dice ──────────────────────────────────────────────────────────────────────────────

    private static void TestOutsiderDice()
    {
        SpellDice seven = CustomSpellDice.AlignmentBurstOutsider(7);
        SpellDice twelve = CustomSpellDice.AlignmentBurstOutsider(12);
        Assert(seven.Count == 7 && seven.Sides == 6 && twelve.Count == 10 && twelve.Sides == 6,
            "an outsider of the opposed alignment takes 1d6 per caster level, max 10d6 (PHB p.241)", seven + ", " + twelve);
        SpellDice burst = CustomSpellDice.AlignmentBurst(7);
        Assert(burst.Count == 3 && burst.Sides == 8, "any other creature of that alignment takes 1d8 per two caster levels (PHB p.241)", burst.ToString());
    }

    private static void AssertEq(int got, int want, string testName) => Assert(got == want, testName, $"got {got}, expected {want}");

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
}
}
#endif

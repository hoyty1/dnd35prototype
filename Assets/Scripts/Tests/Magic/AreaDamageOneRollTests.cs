using System;
using System.Collections.Generic;
using System.Reflection;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 20 (docs/designs/cast_core_batch11.md, raw-area-damage-one-roll; decision D6, a delegated RAW reading of
/// 2026-10-14, SPL-180): an area damage effect rolls its damage once and every creature in the area takes that one total
/// before its own spell resistance, save (half, rounded down), Evasion or Improved Evasion and mitigation.
/// <list type="bullet">
/// <item>AreaDamageRoll rolls on its first use only and hands every later caller the same roll.</item>
/// <item>Fireball (GameManager.TryResolveScaledAoEDamageSpell, PHB p.231) by a wizard 5 over three creatures: five d6
/// drawn (not fifteen), the failing creature takes the total, the saving one half of the same total, a rogue that saves
/// none (Evasion, PHB p.50); each creature rolls its own save. A creature whose spell resistance stops the spell draws
/// no damage die, and a cast whose every creature resists draws none.</item>
/// <item>An empowered Fireball multiplies the one roll by one and a half (PHB p.93): both creatures take 15 + 7.</item>
/// <item>The generic area loop's SpellCaster.Cast with a shared AreaDamageRoll (Burning Hands, PHB p.207): one 5d4 for
/// four creatures; a saving rogue takes none, a failing monk 9 half (Improved Evasion, PHB p.42).</item>
/// <item>Cone of Cold, Ice Storm's two parts, Shout, Sunburst and Earthquake's debris: one roll for two creatures.</item>
/// <item>A Wall of Ice section in a Fireball takes the creatures' same total (PHB p.231, objects "also take this damage"),
/// and the handler's log shows the one roll.</item>
/// <item>A Wall of Fire evoked onto two creatures rolls one 2d6 + CL for both (PHB p.298, one release); a creature that
/// passes through later rolls its own (SPL-181).</item>
/// <item>Halving rounds down (PHB p.177, p.304): Improved Evasion halves a 1-point roll to 0, in the one Evasion rule
/// (SpellSaveResolver.ApplyEvasion) and through SpellCaster.Cast, whose log shows the damage after Evasion.</item>
/// </list>
/// Every non-d20 die is forced to a rising sequence (1, 2, 3, ...) so separate rolls would differ, and every d20 to 10;
/// saves are decided with +-30. Play mode only (the handlers are the scene GameManager's).
/// </summary>
public static class AreaDamageOneRollTests
{
    private static int _passed;
    private static int _failed;

    private static int _sequence;
    private static readonly Dictionary<int, int> Draws = new Dictionary<int, int>();

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== AREA DAMAGE ONE ROLL TESTS (batch 11 step 20, D6, SPL-180) ======");

        SpellDatabase.Init();
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Assert(false, "A scene GameManager exists (Play mode)");
            Debug.Log($"====== Area Damage One Roll Results: {_passed} passed, {_failed} failed ======");
            return;
        }

        var objects = new List<GameObject>();
        Func<int, string, int, int> oldFilter = ScenarioHooks.RollFilter;
        try
        {
            CharacterController Make(string name, string cls, int level, int saveTweak, Vector2Int pos)
            {
                CharacterController c = TestHelpers.CreateCharacter(name, cls, level: level, gridPosition: pos);
                objects.Add(c.gameObject);
                c.SetTeam(CharacterTeam.Enemy);
                c.Stats.Bonuses.Set("test:save-tweak", BonusTarget.AllSaves, BonusType.Untyped, saveTweak, "test save tweak");
                c.Stats.AdjustMaxHP(200);
                c.Stats.CurrentHP += 200;
                return c;
            }

            CharacterController wizard = TestHelpers.CreateCharacter("ADRWizard", "Wizard", level: 5, intelligence: 16, gridPosition: new Vector2Int(70, 70));
            objects.Add(wizard.gameObject);
            wizard.SetTeam(CharacterTeam.Player);

            TestAreaDamageRollOnce();
            TestFireball(gm, wizard, Make);
            TestEmpoweredFireball(gm, wizard, Make);
            TestGenericLoop(wizard, Make);
            TestOtherHandlers(gm, wizard, Make);
            TestEarthquake(gm, wizard, Make);
            TestWallOfIceSharesRoll(gm, wizard, Make, objects);
            TestWallOfFireInitialCatch(Make, objects);
            TestImprovedEvasionRoundsDown(Make, objects);
        }
        catch (Exception e)
        {
            Assert(false, "No exception", e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
        }
        finally
        {
            ScenarioHooks.RollFilter = oldFilter;
            foreach (GameObject go in objects)
                if (go != null)
                    Object.DestroyImmediate(go);
        }

        Debug.Log($"====== Area Damage One Roll Results: {_passed} passed, {_failed} failed ======");
    }

    private delegate CharacterController Maker(string name, string cls, int level, int saveTweak, Vector2Int pos);

    /// <summary>Every d20 is 10; every other die the next value of a rising sequence (1, 2, 3, ... wrapped to its sides), counted by sides.</summary>
    private static void ForceDice()
    {
        _sequence = 0;
        Draws.Clear();
        ScenarioHooks.RollFilter = (sides, ctx, natural) =>
        {
            Draws[sides] = DrawsOf(sides) + 1;
            if (sides == 20)
                return 10;
            int value = _sequence % sides + 1;
            _sequence++;
            return value;
        };
    }

    private static int DrawsOf(int sides) => Draws.TryGetValue(sides, out int n) ? n : 0;

    private static SpellData Spell(string id) => SpellDatabase.GetSpell(id).Clone();

    private static bool InvokeArea(GameManager gm, string method, CharacterController caster, SpellData spell, List<CharacterController> targets)
        => InvokeArea(gm, method, caster, spell, targets, null, out _);

    private static bool InvokeArea(GameManager gm, string method, CharacterController caster, SpellData spell, List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells, out string log)
    {
        log = null;
        MethodInfo handler = typeof(GameManager).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert(handler != null, method + " exists");
        if (handler == null)
            return false;
        object[] args = { caster, spell, targets, aoeCells, null };
        bool handled = (bool)handler.Invoke(gm, args);
        log = args[4] as string;
        return handled;
    }

    private static void SetNonPublicProperty(object target, Type declaring, string name, object value)
    {
        PropertyInfo prop = declaring.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo setter = prop != null ? prop.GetSetMethod(true) : null;
        Assert(setter != null, declaring.Name + "." + name + " has a setter");
        setter?.Invoke(target, new[] { value });
    }

    private static Dictionary<CharacterController, int> Hp(params CharacterController[] cs)
    {
        var d = new Dictionary<CharacterController, int>();
        foreach (CharacterController c in cs)
            d[c] = c.Stats.CurrentHP;
        return d;
    }

    private static int Lost(Dictionary<CharacterController, int> before, CharacterController c) => before[c] - c.Stats.CurrentHP;

    private static void TestAreaDamageRollOnce()
    {
        ForceDice();
        var roll = new AreaDamageRoll();
        Assert(!roll.HasRolled, "A new AreaDamageRoll has not rolled");
        SpellDiceRoll first = roll.GetOrRoll(SpellDice.Of(3, 6), null, "test area die");
        SpellDiceRoll second = roll.GetOrRoll(SpellDice.Of(3, 6), null, "test area die");
        AssertEq(DrawsOf(6), 3, "AreaDamageRoll: three d6 drawn for two uses of a 3d6 roll");
        AssertEq(first.Total, 6, "AreaDamageRoll: the forced 1 + 2 + 3");
        AssertEq(second.Total, first.Total, "AreaDamageRoll: the second use gets the same total");
        Assert(roll.HasRolled, "AreaDamageRoll: rolled after its first use");
        ScenarioHooks.RollFilter = null;
    }

    private static void TestFireball(GameManager gm, CharacterController wizard, Maker make)
    {
        CharacterController fails = make("ADRFails", "Fighter", 5, -30, new Vector2Int(72, 70));
        CharacterController saves = make("ADRSaves", "Fighter", 5, 30, new Vector2Int(73, 70));
        CharacterController rogue = make("ADRRogue", "Rogue", 3, 30, new Vector2Int(74, 70));
        Assert(rogue.Stats.HasEvasion, "Precondition: a rogue 3 has Evasion (PHB p.50)");

        ForceDice();
        var before = Hp(fails, saves, rogue);
        bool handled = InvokeArea(gm, "TryResolveScaledAoEDamageSpell", wizard, Spell(SpellNames.FIREBALL),
            new List<CharacterController> { fails, saves, rogue });
        Assert(handled, "Fireball's handler claims the cast");
        AssertEq(DrawsOf(6), 5, "Fireball (CL 5) over three creatures draws one 5d6, not one per creature");
        AssertEq(DrawsOf(20), 3, "Fireball: each creature rolls its own Reflex save");
        AssertEq(Lost(before, fails), 15, "Fireball: the failing creature takes the one total (1+2+3+4+5)");
        AssertEq(Lost(before, saves), 7, "Fireball: the saving creature takes half of the same total, rounded down");
        AssertEq(Lost(before, rogue), 0, "Fireball: a rogue with Evasion that saves takes none (PHB p.50)");

        // Spell resistance stays per creature: a creature that resists draws no damage die, the next one rolls the total.
        CharacterController warded = make("ADRWarded", "Fighter", 5, -30, new Vector2Int(75, 70));
        warded.Stats.SpellResistance = 100;
        CharacterController second = make("ADRSecond", "Fighter", 5, -30, new Vector2Int(76, 70));
        ForceDice();
        before = Hp(warded, second);
        InvokeArea(gm, "TryResolveScaledAoEDamageSpell", wizard, Spell(SpellNames.FIREBALL),
            new List<CharacterController> { warded, second });
        AssertEq(Lost(before, warded), 0, "Fireball: a creature whose spell resistance stops it takes nothing");
        AssertEq(DrawsOf(6), 5, "Fireball: the one 5d6 is rolled at the first creature the spell affects");
        AssertEq(Lost(before, second), 15, "Fireball: that creature takes the total");

        // Every creature resists: no damage die at all.
        second.Stats.SpellResistance = 100;
        ForceDice();
        InvokeArea(gm, "TryResolveScaledAoEDamageSpell", wizard, Spell(SpellNames.FIREBALL),
            new List<CharacterController> { warded, second });
        AssertEq(DrawsOf(6), 0, "Fireball: no damage die when every creature resists");
        second.Stats.SpellResistance = 0;
        ScenarioHooks.RollFilter = null;
    }

    private static void TestEmpoweredFireball(GameManager gm, CharacterController wizard, Maker make)
    {
        CharacterController a = make("ADREmpA", "Fighter", 5, -30, new Vector2Int(72, 72));
        CharacterController b = make("ADREmpB", "Fighter", 5, -30, new Vector2Int(73, 72));
        var empower = new MetamagicData();
        empower.Toggle(MetamagicFeatId.EmpowerSpell);
        SpellData fireball = Spell(SpellNames.FIREBALL);
        SpellCaster.ApplyMetamagicToSpellData(fireball, empower);

        ForceDice();
        var before = Hp(a, b);
        InvokeArea(gm, "TryResolveScaledAoEDamageSpell", wizard, fireball, new List<CharacterController> { a, b });
        AssertEq(DrawsOf(6), 5, "Empowered Fireball over two creatures draws one 5d6");
        AssertEq(Lost(before, a), 22, "Empowered Fireball: the one roll 15 plus half, 22 (PHB p.93)");
        AssertEq(Lost(before, b), 22, "Empowered Fireball: the second creature takes the same 22");
        ScenarioHooks.RollFilter = null;
    }

    private static void TestGenericLoop(CharacterController wizard, Maker make)
    {
        CharacterController fails = make("ADRBhFails", "Fighter", 5, -30, new Vector2Int(72, 74));
        CharacterController saves = make("ADRBhSaves", "Fighter", 5, 30, new Vector2Int(73, 74));
        CharacterController rogue = make("ADRBhRogue", "Rogue", 3, 30, new Vector2Int(74, 74));
        CharacterController monk = make("ADRBhMonk", "Monk", 9, -30, new Vector2Int(75, 74));
        Assert(monk.Stats.HasImprovedEvasion, "Precondition: a monk 9 has Improved Evasion (PHB p.42)");

        SpellData hands = Spell(SpellNames.BURNING_HANDS);
        var shared = new AreaDamageRoll();
        ForceDice();
        var before = Hp(fails, saves, rogue, monk);
        var results = new List<SpellResult>();
        foreach (CharacterController t in new[] { fails, saves, rogue, monk })
            results.Add(SpellCaster.Cast(hands, wizard.Stats, t.Stats, null, false, false, wizard, t, areaDamageRoll: shared));
        AssertEq(DrawsOf(4), 5, "Burning Hands (CL 5) through SpellCaster.Cast with one AreaDamageRoll draws one 5d4 for four creatures");
        Assert(results.TrueForAll(r => r.DamageRolled == 11), "Burning Hands: every creature's rolled damage is the one total 11 (1+2+3+4+1)",
            string.Join(",", results.ConvertAll(r => r.DamageRolled.ToString())));
        AssertEq(Lost(before, fails), 11, "Burning Hands: the failing creature takes 11");
        AssertEq(Lost(before, saves), 5, "Burning Hands: the saving creature takes half, 5");
        AssertEq(Lost(before, rogue), 0, "Burning Hands: a saving rogue takes none (Evasion in SpellCaster.Cast, SPL-090)");
        Assert(!string.IsNullOrEmpty(results[2].EvasionNote), "Burning Hands: the rogue's result carries the Evasion note");
        AssertEq(Lost(before, monk), 5, "Burning Hands: a failing monk 9 takes half (Improved Evasion, PHB p.42)");

        // Without a shared roll each call rolls its own (a single-target cast).
        ForceDice();
        SpellCaster.Cast(hands, wizard.Stats, fails.Stats, null, false, false, wizard, fails);
        SpellCaster.Cast(hands, wizard.Stats, saves.Stats, null, false, false, wizard, saves);
        AssertEq(DrawsOf(4), 10, "SpellCaster.Cast without an AreaDamageRoll rolls per call");
        ScenarioHooks.RollFilter = null;
    }

    private static void TestOtherHandlers(GameManager gm, CharacterController wizard, Maker make)
    {
        void Pair(string label, string method, string spellId, int saveTweak, int expectedDraws, int sides, bool half)
        {
            CharacterController a = make("ADR" + label + "A", "Fighter", 5, saveTweak, new Vector2Int(72, 76));
            CharacterController b = make("ADR" + label + "B", "Fighter", 5, saveTweak, new Vector2Int(73, 76));
            ForceDice();
            var before = Hp(a, b);
            bool handled = InvokeArea(gm, method, wizard, Spell(spellId), new List<CharacterController> { a, b });
            Assert(handled, label + ": its handler claims the cast");
            AssertEq(DrawsOf(sides), expectedDraws, label + ": one roll for two creatures (" + expectedDraws + " d" + sides + ")");
            Assert(Lost(before, a) == Lost(before, b) && Lost(before, a) > 0,
                label + ": both creatures take the same damage" + (half ? " (both saved for half)" : ""),
                Lost(before, a) + " vs " + Lost(before, b));
            ScenarioHooks.RollFilter = null;
            a.Stats.CurrentHP = a.Stats.TotalMaxHP;
            b.Stats.CurrentHP = b.Stats.TotalMaxHP;
        }

        Pair("ConeOfCold", "TryResolveConeOfColdSpell", SpellNames.CONE_OF_COLD, -30, 5, 6, false);
        Pair("IceStorm", "TryResolveIceStormAoE", SpellNames.ICE_STORM, -30, 5, 6, false);
        Pair("Shout", "TryResolveShoutAoE", SpellNames.SHOUT, 30, 5, 6, true);
        Pair("Sunburst", "TryResolveSunburstSpell", SpellNames.SUNBURST, -30, 6, 6, false);
    }

    private static void TestEarthquake(GameManager gm, CharacterController wizard, Maker make)
    {
        CharacterController a = make("ADRQuakeA", "Fighter", 5, -30, new Vector2Int(72, 80));
        CharacterController b = make("ADRQuakeB", "Fighter", 5, -30, new Vector2Int(73, 80));
        ForceDice();
        var before = Hp(a, b);
        bool handled = InvokeArea(gm, "TryResolveEarthquakeSpell", wizard, Spell(SpellNames.EARTHQUAKE), new List<CharacterController> { a, b });
        Assert(handled, "Earthquake: its handler claims the cast");
        AssertEq(DrawsOf(6), 1, "Earthquake: one debris d6 for two creatures that fail their saves");
        AssertEq(Lost(before, a), 1, "Earthquake: the first creature takes the one debris roll (forced 1)");
        AssertEq(Lost(before, b), 1, "Earthquake: the second creature takes the same debris roll");
        ScenarioHooks.RollFilter = null;
    }

    private static void TestWallOfIceSharesRoll(GameManager gm, CharacterController wizard, Maker make, List<GameObject> objects)
    {
        CharacterController c = make("ADRIceWallFoe", "Fighter", 5, -30, new Vector2Int(72, 82));
        var wallCell = new Vector2Int(73, 82);
        var go = new GameObject("ADRWallOfIce_Test");
        objects.Add(go);
        WallOfIceAreaEffect wall = go.AddComponent<WallOfIceAreaEffect>();
        wall.CasterLevel = 20;
        SetNonPublicProperty(wall, typeof(PersistentAreaEffect), "AffectedCells", new HashSet<Vector2Int> { wallCell });
        SetNonPublicProperty(wall, typeof(WallOfIceAreaEffect), "CellMaxHP", 60);
        wall.WallMaxHP = 60;
        wall.WallHP = 60;
        MethodInfo init = typeof(WallOfIceAreaEffect).GetMethod("InitializeCellHitPoints", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert(init != null, "WallOfIceAreaEffect.InitializeCellHitPoints exists");
        init?.Invoke(wall, null);
        AreaEffectManager.Instance.RegisterAreaEffect(wall);
        try
        {
            AssertEq(wall.GetCellHP(wallCell), 60, "Precondition: the test Wall of Ice section has 60 hp");
            ForceDice();
            var before = Hp(c);
            InvokeArea(gm, "TryResolveScaledAoEDamageSpell", wizard, Spell(SpellNames.FIREBALL), new List<CharacterController> { c },
                new HashSet<Vector2Int> { c.GridPosition, wallCell }, out string log);
            AssertEq(DrawsOf(6), 5, "Fireball over a creature and a Wall of Ice section draws one 5d6 in all");
            AssertEq(Lost(before, c), 15, "Fireball + Wall of Ice: the creature takes the one total 15");
            AssertEq(60 - wall.GetCellHP(wallCell), 15, "Fireball + Wall of Ice: the section takes the same 15 (PHB p.231)");
            Assert(log != null && log.Contains("Damage roll (one for the area): 5d6"), "Fireball's log shows its one damage roll", log);
            ScenarioHooks.RollFilter = null;
        }
        finally
        {
            if (AreaEffectManager.HasInstance)
                AreaEffectManager.Instance.UnregisterAreaEffect(wall);
        }
    }

    private static void TestWallOfFireInitialCatch(Maker make, List<GameObject> objects)
    {
        CharacterController a = make("ADRFireWallA", "Fighter", 5, 0, new Vector2Int(72, 84));
        CharacterController b = make("ADRFireWallB", "Fighter", 5, 0, new Vector2Int(73, 84));
        CharacterController later = make("ADRFireWallLater", "Fighter", 5, 0, new Vector2Int(74, 84));
        var go = new GameObject("ADRWallOfFire_Test");
        objects.Add(go);
        WallOfFireAreaEffect wall = go.AddComponent<WallOfFireAreaEffect>();
        wall.CasterLevel = 5;
        SetNonPublicProperty(wall, typeof(PersistentAreaEffect), "AffectedCells",
            new HashSet<Vector2Int> { a.GridPosition, b.GridPosition, later.GridPosition });
        MethodInfo enters = typeof(WallOfFireAreaEffect).GetMethod("OnCreatureEntersArea", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert(enters != null, "WallOfFireAreaEffect.OnCreatureEntersArea exists");
        if (enters == null)
            return;

        ForceDice();
        var before = Hp(a, b, later);
        enters.Invoke(wall, new object[] { a, true });
        enters.Invoke(wall, new object[] { b, true });
        AssertEq(DrawsOf(6), 2, "Wall of Fire evoked onto two creatures rolls one 2d6 + CL (PHB p.298; D6)");
        AssertEq(Lost(before, a), 8, "Wall of Fire's catch: the first creature takes 1 + 2 + 5");
        AssertEq(Lost(before, b), 8, "Wall of Fire's catch: the second creature takes the same 8");
        enters.Invoke(wall, new object[] { later, false });
        AssertEq(DrawsOf(6), 4, "Wall of Fire: a creature passing through later rolls its own 2d6 (SPL-181)");
        AssertEq(Lost(before, later), 12, "Wall of Fire: the later pass takes its own 3 + 4 + 5");
        ScenarioHooks.RollFilter = null;
    }

    private static void TestImprovedEvasionRoundsDown(Maker make, List<GameObject> objects)
    {
        CharacterController monk = make("ADRIEMonk", "Monk", 9, -30, new Vector2Int(72, 86));
        CharacterController rogue = make("ADRIERogue", "Rogue", 3, 30, new Vector2Int(73, 86));
        Assert(monk.Stats.HasImprovedEvasion, "Precondition: a monk 9 has Improved Evasion (PHB p.42)");

        AssertEq(SpellSaveResolver.ApplyEvasion(1, monk.Stats, false, out string note), 0, "Improved Evasion halves 1 point to 0 (rounded down, PHB p.304)");
        Assert(note != null && note.Contains("Improved Evasion"), "Improved Evasion: the halving is noted", note);
        AssertEq(SpellSaveResolver.ApplyEvasion(7, monk.Stats, false, out _), 3, "Improved Evasion halves 7 to 3");
        AssertEq(SpellSaveResolver.ApplyEvasion(7, monk.Stats, true, out _), 0, "Evasion (a monk 9 has it too): a made save takes none");
        AssertEq(SpellSaveResolver.ApplyEvasion(7, rogue.Stats, false, out string none), 7, "Evasion alone does not halve a failed save");
        Assert(none == null, "Evasion alone on a failed save adds no note", none);

        // Through SpellCaster.Cast: a caster level 1 Burning Hands rolling 1 on its 1d4 leaves a failing monk 9 unhurt.
        CharacterController apprentice = TestHelpers.CreateCharacter("ADRApprentice", "Wizard", level: 1, intelligence: 16, gridPosition: new Vector2Int(70, 86));
        objects.Add(apprentice.gameObject);
        apprentice.SetTeam(CharacterTeam.Player);
        ForceDice();
        int before = monk.Stats.CurrentHP;
        SpellResult result = SpellCaster.Cast(Spell(SpellNames.BURNING_HANDS), apprentice.Stats, monk.Stats, null, false, false, apprentice, monk);
        AssertEq(result.DamageRolled, 1, "Burning Hands (CL 1): the forced 1d4 rolls 1");
        AssertEq(before - monk.Stats.CurrentHP, 0, "Burning Hands: the failing monk 9 halves 1 to 0 (Improved Evasion, rounded down)");
        string summary = result.GetDetailedSummary();
        Assert(summary != null && summary.Contains("= 0 raw"), "Burning Hands: the cast log's raw damage line shows the damage after Improved Evasion", summary);
        ScenarioHooks.RollFilter = null;
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
}
}

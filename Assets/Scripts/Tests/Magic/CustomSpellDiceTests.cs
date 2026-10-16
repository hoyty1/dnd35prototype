using System;
using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 19 (SPL-038, SPL-097): the dice spec of every custom resolver that rolls its own spell dice
/// (CustomSpellDice), each rolled through the one dice rule SpellDiceRules.RollWith without metamagic, Empowered,
/// Maximized and with both, over a die source that always rolls 3 and counts the dice it rolls:
/// plain = the dice at 3 + the fixed term, one die rolled per die of the spec (so a plain cast draws what it drew before);
/// Empower = that result + half of it, rounded down (PHB p.93, p.304); Maximize = every die at its maximum + the fixed
/// term, no die rolled (PHB p.97); both = the maximum + half of the normal result (PHB p.97-98).
/// The specs are checked against the dice the PHB gives each spell (the page in each row), and Searing Light against
/// the handler's own dice (SPL-177). Pure: runs in edit or Play mode.
/// </summary>
public static class CustomSpellDiceTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== CUSTOM SPELL DICE TESTS (batch 11 step 19, SPL-038, SPL-097) ======");

        foreach (Row row in Rows())
            CheckRow(row);

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
    }

    private struct Row
    {
        public string Name;
        public SpellDice Dice;
        public int Count, Sides, Bonus;
    }

    private static Row R(string name, SpellDice dice, int count, int sides, int bonus = 0)
        => new Row { Name = name, Dice = dice, Count = count, Sides = sides, Bonus = bonus };

    private static IEnumerable<Row> Rows()
    {
        yield return R("Fireball CL 5 (PHB p.231: 1d6/level)", CustomSpellDice.FireballOrLightningBolt(5), 5, 6);
        yield return R("Fireball CL 12 (PHB p.231: max 10d6)", CustomSpellDice.FireballOrLightningBolt(12), 10, 6);
        yield return R("Lightning Bolt CL 1 (PHB p.248: at least 1d6)", CustomSpellDice.FireballOrLightningBolt(1), 1, 6);
        yield return R("Call Lightning bolt (PHB p.207: 3d6)", CustomSpellDice.CallLightningBolt(), 3, 6);
        yield return R("Ice Storm bludgeoning (PHB p.243: 3d6)", CustomSpellDice.IceStormBludgeoning(), 3, 6);
        yield return R("Ice Storm cold (PHB p.243: 2d6)", CustomSpellDice.IceStormCold(), 2, 6);
        yield return R("Shout (PHB p.279: 5d6 sonic)", CustomSpellDice.Shout(), 5, 6);
        yield return R("Shout deafness (PHB p.279: 2d6 rounds)", CustomSpellDice.ShoutDeafenedRounds(), 2, 6);
        yield return R("Cone of Cold CL 17 (PHB p.212: max 15d6)", CustomSpellDice.ConeOfCold(17), 15, 6);
        yield return R("Chain Lightning CL 11 (PHB p.208: 1d6/level)", CustomSpellDice.ChainLightning(11), 11, 6);
        yield return R("Circle of Death CL 11 (PHB p.209: 1d4 HD/level)", CustomSpellDice.CircleOfDeathHitDice(11), 11, 4);
        yield return R("Sunburst (PHB p.289: 6d6)", CustomSpellDice.Sunburst(15, false), 6, 6);
        yield return R("Sunburst on undead CL 15 (PHB p.289: 1d6/level)", CustomSpellDice.Sunburst(15, true), 15, 6);
        yield return R("Searing Light CL 5 (handler: 1d8 per two levels)", CustomSpellDice.SearingLight(5, false), 2, 8);
        yield return R("Searing Light on a construct CL 12 (PHB p.275: 1d6 per two levels, max 5d6)", CustomSpellDice.SearingLight(12, true), 5, 6);
        yield return R("Vampiric Touch CL 7 (PHB p.298: 1d6 per two levels)", CustomSpellDice.VampiricTouch(7), 3, 6);
        yield return R("Vampiric Touch CL 25 (PHB p.298: max 10d6)", CustomSpellDice.VampiricTouch(25), 10, 6);
        yield return R("Melf's Acid Arrow later round (PHB p.253: 2d4)", CustomSpellDice.MelfsAcidArrowRound(), 2, 4);
        yield return R("Produce Flame CL 3 (PHB p.265: 1d6 + 1/level)", CustomSpellDice.ProduceFlame(3), 1, 6, 3);
        yield return R("Produce Flame CL 9 (PHB p.265: max +5)", CustomSpellDice.ProduceFlame(9), 1, 6, 5);
        yield return R("Heat Metal round 2 (PHB p.239: hot, 1d4)", CustomSpellDice.HeatMetalRound(2), 1, 4);
        yield return R("Heat Metal round 3 (PHB p.239: searing, 2d4)", CustomSpellDice.HeatMetalRound(3), 2, 4);
        yield return R("Holy Smite CL 7 (PHB p.241: 1d8 per two levels)", CustomSpellDice.AlignmentBurst(7), 3, 8);
        yield return R("Unholy Blight CL 14 (PHB p.297: max 5d8)", CustomSpellDice.AlignmentBurst(14), 5, 8);
        yield return R("Disintegrate CL 11 (PHB p.222: 2d6/level)", CustomSpellDice.Disintegrate(11, false), 22, 6);
        yield return R("Disintegrate saved (PHB p.222: 5d6)", CustomSpellDice.Disintegrate(11, true), 5, 6);
        yield return R("Phantasmal Killer save made (PHB p.260: 3d6)", CustomSpellDice.PhantasmalKiller(), 3, 6);
        yield return R("Spiritual Weapon CL 9 (PHB p.283: 1d8 + 1 per three levels)", CustomSpellDice.SpiritualWeapon(9), 1, 8, 3);
        yield return R("Spiritual Weapon CL 20 (PHB p.283: max +5)", CustomSpellDice.SpiritualWeapon(20), 1, 8, 5);
        yield return R("Calm Animals CL 4 (PHB p.207: 2d4 + level HD)", CustomSpellDice.CalmAnimalsHitDice(4), 2, 4, 4);
        yield return R("Hypnotism HD (PHB p.242: 2d4)", CustomSpellDice.HypnotismHitDice(), 2, 4);
        yield return R("Hypnotism rounds (PHB p.242: 2d4)", CustomSpellDice.HypnotismRounds(), 2, 4);
        yield return R("Fire Shield CL 7 (PHB p.230: 1d6 + 1/level)", CustomSpellDice.FireShieldRetribution(7), 1, 6, 7);
        yield return R("Fire Shield CL 18 (PHB p.230: max +15)", CustomSpellDice.FireShieldRetribution(18), 1, 6, 15);
        yield return R("Wall of Fire passing through, CL 8 (PHB p.298: 2d6 + 1/level)", CustomSpellDice.WallOfFirePassThrough(8), 2, 6, 8);
        yield return R("Black Tentacles (PHB p.228: 1d6+4)", CustomSpellDice.BlackTentaclesCrush(), 1, 6, 4);
        yield return R("False Life CL 6 (PHB p.229: 1d10 + 1/level)", CustomSpellDice.FalseLife(6), 1, 10, 6);
        yield return R("False Life CL 14 (PHB p.229: max +10)", CustomSpellDice.FalseLife(14), 1, 10, 10);
        yield return R("Spell Turning (PHB p.282: 1d4+6 levels)", CustomSpellDice.SpellTurningLevels(), 1, 4, 6);
    }

    private static MetamagicData Meta(params MetamagicFeatId[] feats)
    {
        var mm = new MetamagicData();
        foreach (MetamagicFeatId feat in feats)
            mm.AppliedMetamagic.Add(feat);
        return mm;
    }

    private static void CheckRow(Row row)
    {
        SpellDice d = row.Dice;
        Assert(d.Count == row.Count && d.Sides == row.Sides && d.Bonus == row.Bonus,
            $"{row.Name}: {row.Count}d{row.Sides}{(row.Bonus != 0 ? "+" + row.Bonus : "")}", $"got {d}");

        int normal = row.Count * 3 + row.Bonus;
        int maximum = row.Count * row.Sides + row.Bonus;
        int rolled = 0;
        Func<int, int> threes = sides => { rolled++; return Mathf.Min(3, sides); };
        if (row.Sides < 3)
            normal = row.Count * row.Sides + row.Bonus;

        rolled = 0;
        SpellDiceRoll plain = SpellDiceRules.RollWith(d, null, threes);
        Assert(plain.Total == normal && rolled == row.Count, $"{row.Name}, plain: {normal}, {row.Count} dice rolled", $"got {plain.Total}, {rolled} rolled");

        rolled = 0;
        SpellDiceRoll emp = SpellDiceRules.RollWith(d, Meta(MetamagicFeatId.EmpowerSpell), threes);
        Assert(emp.Total == normal + normal / 2 && rolled == row.Count, $"{row.Name}, Empowered: {normal + normal / 2}", $"got {emp.Total}, {rolled} rolled");

        rolled = 0;
        SpellDiceRoll max = SpellDiceRules.RollWith(d, Meta(MetamagicFeatId.MaximizeSpell), threes);
        Assert(max.Total == maximum && rolled == 0, $"{row.Name}, Maximized: {maximum}, no die rolled", $"got {max.Total}, {rolled} rolled");

        rolled = 0;
        SpellDiceRoll both = SpellDiceRules.RollWith(d, Meta(MetamagicFeatId.EmpowerSpell, MetamagicFeatId.MaximizeSpell), threes);
        Assert(both.Total == maximum + normal / 2 && rolled == row.Count,
            $"{row.Name}, Empowered and Maximized: {maximum} + {normal / 2}", $"got {both.Total}, {rolled} rolled");
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
            Debug.LogError($"  [CustomSpellDice] FAIL: {testName} {detail}");
        }
    }
}
}

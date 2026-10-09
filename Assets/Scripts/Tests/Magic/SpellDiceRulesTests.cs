using System;
using DND35e.Identifiers;
using UnityEngine;

namespace Tests.Magic
{
/// <summary>
/// SPL-005: spell dice that scale with caster level, and Cure and Inflict on undead (<see cref="SpellDiceRules"/>,
/// <see cref="SpellCaster.Cast"/>).
/// Rules (checked 2026-10-09 in the owner's PHB): p.207 Burning Hands 1d4/level (max 5d4); p.279 Shocking Grasp
/// 1d6/level (max 5d6); p.231 Flame Strike 1d6/level (max 15d6); p.215-216 Cure Light/Moderate/Serious/Critical Wounds
/// 1d8/2d8/3d8/4d8 + 1/level (max +5/+10/+15/+20), Cure Minor Wounds 1 point; on an undead a cure deals that damage,
/// spell resistance applies and a Will save halves; p.244 Inflict Light/Moderate/Serious/Critical Wounds with the same
/// dice and caps, Will half, SR yes; Inflict Minor Wounds 1 point, Will negates; an inflict spell cures an undead.
/// MM p.307: constructs are immune to necromancy effects (inflict); a cure channels energy into a living creature
/// (PHB p.215), so it does nothing to a construct.
/// Builds CharacterStats only (no controllers), so it runs in edit or Play mode. Every die is forced to its maximum
/// through ScenarioHooks.RollFilter (so a touch attack is a natural 20 and every save roll is 20); saves are decided
/// with a +/-30 untyped save modifier in CharacterStats.Bonuses.
/// </summary>
public static class SpellDiceRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        SpellDatabase.Init();

        Debug.Log("====== SPELL DICE RULES TESTS (SPL-005) ======");

        TestScalingHelpers();
        TestCureAndInflictData();
        TestScaledDamageData();
        TestEnergyOutcomes();

        Func<int, string, int, int> previous = ScenarioHooks.RollFilter;
        ScenarioHooks.RollFilter = (sides, ctx, natural) => sides;
        try
        {
            TestCasterLevel();
            TestCastScaledDamage();
            TestCastCureAndInflictOnLiving();
            TestCastCureOnUndead();
            TestCastInflictOnUndead();
            TestCastOnConstruct();
            TestMinorSpells();
            TestSpellResistance();
            TestItemPipelineCasterLevel();
        }
        finally
        {
            ScenarioHooks.RollFilter = previous;
        }

        TestBakeForCasterLevel();
        TestFactoryHealDice();

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
            Debug.LogError($"  [SpellDiceRules] FAIL: {testName} {detail}");
        }
    }

    private static void AssertEq(int actual, int expected, string testName)
        => Assert(actual == expected, testName, $"expected {expected}, got {actual}");

    private static SpellData Spell(string id)
    {
        SpellData template = SpellDatabase.GetSpell(id);
        return template != null ? template.Clone() : null;
    }

    private static CharacterStats Caster(string cls, int level)
    {
        return new CharacterStats(
            name: cls + " " + level, level: level, characterClass: cls,
            str: 10, dex: 10, con: 10, wis: 16, intelligence: 16, cha: 10,
            bab: level / 2, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 6 * level, raceName: "Human");
    }

    /// <summary>A 200-HP target of <paramref name="type"/> whose saves always fail (save &lt; 0) or succeed (save &gt; 0).</summary>
    private static CharacterStats Target(string name, string type, int saveBonus, int damageTaken = 0)
    {
        var stats = new CharacterStats(
            name: name, level: 5, characterClass: "Fighter",
            str: 12, dex: 10, con: 12, wis: 10, intelligence: 10, cha: 10,
            bab: 5, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 200, raceName: "Human");
        stats.CreatureType = type;
        stats.Bonuses.Set("test:save", BonusTarget.AllSaves, BonusType.Untyped, saveBonus);
        stats.CurrentHP = stats.TotalMaxHP - damageTaken;
        return stats;
    }

    // ========== PURE RULES ==========

    private static void TestScalingHelpers()
    {
        AssertEq(SpellDiceRules.ScaledCount(3, 0, 0, 9), 3, "No scaling keeps the base dice count");
        AssertEq(SpellDiceRules.ScaledCount(1, 1, 5, 0), 1, "Scaled dice are at least 1 (CL 0)");
        AssertEq(SpellDiceRules.ScaledCount(1, 2, 10, 7), 3, "One die per 2 levels at CL 7 is 3");
        AssertEq(SpellDiceRules.LevelBonus(1, 5, 3), 3, "+1/level at CL 3 is +3");
        AssertEq(SpellDiceRules.LevelBonus(1, 5, 12), 5, "+1/level (max +5) at CL 12 is +5");
        AssertEq(SpellDiceRules.LevelBonus(0, 0, 12), 0, "No level bonus when none is set");
    }

    private static void TestCureAndInflictData()
    {
        var table = new (string id, int dice, int cap, SpellEnergy energy)[]
        {
            (SpellNames.CURE_LIGHT_WOUNDS, 1, 5, SpellEnergy.Positive),
            (SpellNames.CURE_MODERATE_WOUNDS, 2, 10, SpellEnergy.Positive),
            (SpellNames.CURE_SERIOUS_WOUNDS, 3, 15, SpellEnergy.Positive),
            (SpellNames.CURE_CRITICAL_WOUNDS, 4, 20, SpellEnergy.Positive),
            (SpellNames.INFLICT_LIGHT_WOUNDS, 1, 5, SpellEnergy.Negative),
            (SpellNames.INFLICT_MODERATE_WOUNDS, 2, 10, SpellEnergy.Negative),
            (SpellNames.INFLICT_SERIOUS_WOUNDS, 3, 15, SpellEnergy.Negative),
            (SpellNames.INFLICT_CRITICAL_WOUNDS, 4, 20, SpellEnergy.Negative),
        };

        foreach (var row in table)
        {
            SpellData spell = Spell(row.id);
            if (spell == null)
            {
                Assert(false, row.id + " is registered");
                continue;
            }

            Assert(spell.Energy == row.energy, spell.Name + " channels " + row.energy + " energy");
            foreach (int cl in new[] { 1, 3, row.cap, row.cap + 5 })
            {
                SpellDice dice = SpellDiceRules.Effect(spell, cl);
                int expectedBonus = Mathf.Min(cl, row.cap);
                Assert(dice.Count == row.dice && dice.Sides == 8 && dice.Bonus == expectedBonus,
                    $"{spell.Name} at CL {cl} is {row.dice}d8+{expectedBonus} (PHB p.215-216, p.244)", "got " + dice);
            }

            if (row.energy == SpellEnergy.Negative)
            {
                Assert(spell.AllowsSavingThrow && spell.SavingThrowType == "Will" && spell.SaveHalves, spell.Name + " allows a Will save for half");
                Assert(spell.SpellResistanceApplies, spell.Name + " allows spell resistance");
            }
        }

        SpellData cureMinor = Spell(SpellNames.CURE_MINOR_WOUNDS);
        SpellData inflictMinor = Spell(SpellNames.INFLICT_MINOR_WOUNDS);
        Assert(cureMinor != null && cureMinor.Energy == SpellEnergy.Positive && SpellDiceRules.Effect(cureMinor, 9).Maximum == 1,
            "Cure Minor Wounds cures 1 point at any caster level, with positive energy (PHB p.216)");
        Assert(inflictMinor != null && inflictMinor.Energy == SpellEnergy.Negative && SpellDiceRules.Effect(inflictMinor, 9).Maximum == 1
            && inflictMinor.AllowsSavingThrow && !inflictMinor.SaveHalves,
            "Inflict Minor Wounds deals 1 point, Will negates (PHB p.244)");
    }

    private static void TestScaledDamageData()
    {
        var table = new (string id, int sides, int cap)[]
        {
            (SpellNames.BURNING_HANDS, 4, 5),
            (SpellNames.SHOCKING_GRASP, 6, 5),
            (SpellNames.FLAME_STRIKE, 6, 15),
        };

        foreach (var row in table)
        {
            SpellData spell = Spell(row.id);
            if (spell == null)
            {
                Assert(false, row.id + " is registered");
                continue;
            }

            foreach (int cl in new[] { 1, 3, row.cap, row.cap + 4 })
            {
                SpellDice dice = SpellDiceRules.Damage(spell, cl);
                int expected = Mathf.Min(cl, row.cap);
                Assert(dice.Count == expected && dice.Sides == row.sides && dice.Bonus == 0,
                    $"{spell.Name} at CL {cl} is {expected}d{row.sides} (1d{row.sides}/level, max {row.cap}d{row.sides})", "got " + dice);
            }
        }

        SpellData fixedDice = Spell(SpellNames.ACID_SPLASH);
        Assert(fixedDice != null && SpellDiceRules.Damage(fixedDice, 9).ToString() == "1d3", "Acid Splash stays 1d3 at CL 9 (no scaling)");
    }

    private static void TestEnergyOutcomes()
    {
        SpellData cure = Spell(SpellNames.CURE_LIGHT_WOUNDS);
        SpellData inflict = Spell(SpellNames.INFLICT_LIGHT_WOUNDS);
        SpellData fire = Spell(SpellNames.BURNING_HANDS);
        CharacterStats living = Target("Living", "Humanoid", 0);
        CharacterStats undead = Target("Undead", "Undead", 0);
        CharacterStats construct = Target("Construct", "Construct", 0);

        Assert(SpellDiceRules.OutcomeFor(cure, living) == SpellEnergyOutcome.Normal, "Cure on a living creature works as written");
        Assert(SpellDiceRules.OutcomeFor(cure, undead) == SpellEnergyOutcome.HarmsUndead, "Cure harms an undead (PHB p.215)");
        Assert(SpellDiceRules.OutcomeFor(inflict, undead) == SpellEnergyOutcome.HealsUndead, "Inflict cures an undead (PHB p.244)");
        Assert(SpellDiceRules.OutcomeFor(cure, construct) == SpellEnergyOutcome.NoEffect, "Cure has no effect on a construct");
        Assert(SpellDiceRules.OutcomeFor(inflict, construct) == SpellEnergyOutcome.NoEffect, "Inflict has no effect on a construct (MM p.307)");
        Assert(SpellDiceRules.OutcomeFor(fire, undead) == SpellEnergyOutcome.Normal, "A non-energy spell is unchanged on an undead");
        Assert(SpellDiceRules.Heals(inflict, undead) && !SpellDiceRules.Heals(cure, undead) && SpellDiceRules.Harms(cure, undead),
            "Heals/Harms follow the energy outcome");
    }

    // ========== SpellCaster.Cast (shared by the PC and NPC pipelines) ==========

    private static void TestCasterLevel()
    {
        SpellData cure = Spell(SpellNames.CURE_LIGHT_WOUNDS);
        AssertEq(SpellDiceRules.CasterLevelFor(Caster("Cleric", 3), cure), 3, "A 3rd-level cleric casts at caster level 3");
        AssertEq(SpellDiceRules.CasterLevelFor(null, cure), 1, "No caster casts at caster level 1");
    }

    private static int Taken(CharacterStats target, Func<SpellResult> cast, out SpellResult result)
    {
        int before = target.CurrentHP;
        result = cast();
        return before - target.CurrentHP;
    }

    private static void TestCastScaledDamage()
    {
        CharacterStats wiz5 = Caster("Wizard", 5);
        CharacterStats wiz9 = Caster("Wizard", 9);
        CharacterStats cleric9 = Caster("Cleric", 9);

        CharacterStats t = Target("Target", "Humanoid", -30);
        AssertEq(Taken(t, () => SpellCaster.Cast(Spell(SpellNames.BURNING_HANDS), wiz5, t), out _), 20, "Burning Hands at CL 5 deals 5d4 (max 20)");
        t = Target("Target", "Humanoid", -30);
        AssertEq(Taken(t, () => SpellCaster.Cast(Spell(SpellNames.BURNING_HANDS), wiz9, t), out _), 20, "Burning Hands at CL 9 stays 5d4 (cap)");
        t = Target("Target", "Humanoid", -30);
        AssertEq(Taken(t, () => SpellCaster.Cast(Spell(SpellNames.SHOCKING_GRASP), wiz9, t), out SpellResult grasp), 30, "Shocking Grasp at CL 9 deals 5d6 (cap)");
        Assert(grasp.DiceCount == 5 && grasp.CasterLevel == 9, "The result records 5 dice at CL 9", $"dice {grasp.DiceCount} CL {grasp.CasterLevel}");
        t = Target("Target", "Humanoid", -30);
        AssertEq(Taken(t, () => SpellCaster.Cast(Spell(SpellNames.FLAME_STRIKE), cleric9, t), out _), 54, "Flame Strike at CL 9 deals 9d6");
        t = Target("Saver", "Humanoid", 30);
        AssertEq(Taken(t, () => SpellCaster.Cast(Spell(SpellNames.BURNING_HANDS), wiz5, t), out _), 10, "Burning Hands: a Reflex save halves 20 to 10");
    }

    private static void TestCastCureAndInflictOnLiving()
    {
        CharacterStats cleric3 = Caster("Cleric", 3);
        CharacterStats cleric9 = Caster("Cleric", 9);

        CharacterStats hurt = Target("Hurt", "Humanoid", -30, damageTaken: 50);
        AssertEq(-Taken(hurt, () => SpellCaster.Cast(Spell(SpellNames.CURE_LIGHT_WOUNDS), cleric3, hurt, null, true, true), out _), 11,
            "Cure Light Wounds at CL 3 cures 1d8+3 (11)");
        hurt = Target("Hurt", "Humanoid", -30, damageTaken: 50);
        AssertEq(-Taken(hurt, () => SpellCaster.Cast(Spell(SpellNames.CURE_CRITICAL_WOUNDS), cleric9, hurt, null, true, true), out _), 41,
            "Cure Critical Wounds at CL 9 cures 4d8+9 (41)");

        CharacterStats foe = Target("Foe", "Humanoid", -30);
        AssertEq(Taken(foe, () => SpellCaster.Cast(Spell(SpellNames.INFLICT_LIGHT_WOUNDS), cleric9, foe), out SpellResult ilw), 13,
            "Inflict Light Wounds at CL 9 deals 1d8+5 (the +5 cap)");
        Assert(ilw.RequiredAttackRoll && ilw.RequiredSave && ilw.SaveType == "Will", "Inflict on a living foe needs a touch attack and allows a Will save");
        foe = Target("Saver", "Humanoid", 30);
        AssertEq(Taken(foe, () => SpellCaster.Cast(Spell(SpellNames.INFLICT_SERIOUS_WOUNDS), cleric9, foe), out _), 16,
            "Inflict Serious Wounds at CL 9: 3d8+9 = 33, Will half 16");
    }

    private static void TestCastCureOnUndead()
    {
        CharacterStats cleric3 = Caster("Cleric", 3);
        CharacterStats cleric5 = Caster("Cleric", 5);

        CharacterStats zombie = Target("Zombie", "Undead", -30);
        AssertEq(Taken(zombie, () => SpellCaster.Cast(Spell(SpellNames.CURE_LIGHT_WOUNDS), cleric3, zombie), out SpellResult r), 11,
            "Cure Light Wounds deals 1d8+3 to an undead (PHB p.215)");
        Assert(r.EnergyOutcome == SpellEnergyOutcome.HarmsUndead && r.IsDamageEffect && !r.IsHealingEffect, "The result reads as damage");
        Assert(r.RequiredAttackRoll, "Curing an unwilling undead needs a touch attack");
        Assert(r.RequiredSave && r.SaveType == "Will" && !r.SaveSucceeded, "The undead rolls a Will save (and fails at -30)");
        Assert(r.DamageType == "positive", "The damage is positive energy", r.DamageType);

        zombie = Target("Zombie", "Undead", 30);
        AssertEq(Taken(zombie, () => SpellCaster.Cast(Spell(SpellNames.CURE_MODERATE_WOUNDS), cleric5, zombie), out _), 10,
            "Cure Moderate Wounds at CL 5 on an undead: 2d8+5 = 21, Will half 10");

        // An undead ally: no touch attack, but it never forgoes its save against positive energy.
        zombie = Target("Undead ally", "Undead", 30);
        AssertEq(Taken(zombie, () => SpellCaster.Cast(Spell(SpellNames.CURE_LIGHT_WOUNDS), cleric5, zombie, null, true, true), out r), 6,
            "Cure Light Wounds on a willing undead ally: 13, its Will save still halves to 6");
        Assert(!r.RequiredAttackRoll && r.RequiredSave && r.SaveSucceeded, "Friendly touch: no attack roll, the save is rolled");
    }

    private static void TestCastInflictOnUndead()
    {
        CharacterStats cleric5 = Caster("Cleric", 5);
        CharacterStats ghoul = Target("Ghoul", "Undead", -30, damageTaken: 50);
        int healed = -Taken(ghoul, () => SpellCaster.Cast(Spell(SpellNames.INFLICT_LIGHT_WOUNDS), cleric5, ghoul), out SpellResult r);
        AssertEq(healed, 13, "Inflict Light Wounds at CL 5 cures an undead 1d8+5 (PHB p.244)");
        Assert(r.EnergyOutcome == SpellEnergyOutcome.HealsUndead && r.IsHealingEffect && !r.IsDamageEffect && r.HealingDone == 13,
            "The result reads as healing");
        Assert(!r.RequiredAttackRoll && !r.RequiredSave, "The undead accepts the cure: no touch attack, no save");

        CharacterStats full = Target("Unhurt ghoul", "Undead", -30);
        AssertEq(-Taken(full, () => SpellCaster.Cast(Spell(SpellNames.INFLICT_CRITICAL_WOUNDS), cleric5, full), out _), 0,
            "Inflict on an unhurt undead cures nothing past full HP");
    }

    private static void TestCastOnConstruct()
    {
        CharacterStats cleric5 = Caster("Cleric", 5);
        CharacterStats golem = Target("Golem", "Construct", -30, damageTaken: 20);
        int changed = Taken(golem, () => SpellCaster.Cast(Spell(SpellNames.CURE_SERIOUS_WOUNDS), cleric5, golem, null, true, true), out SpellResult cure);
        Assert(changed == 0 && !cure.Success && !string.IsNullOrEmpty(cure.NoEffectReason), "A cure has no effect on a construct", "changed " + changed);
        changed = Taken(golem, () => SpellCaster.Cast(Spell(SpellNames.INFLICT_SERIOUS_WOUNDS), cleric5, golem), out SpellResult inflict);
        Assert(changed == 0 && !inflict.Success, "An inflict spell has no effect on a construct (MM p.307)", "changed " + changed);
    }

    private static void TestMinorSpells()
    {
        CharacterStats cleric1 = Caster("Cleric", 1);
        CharacterStats zombie = Target("Zombie", "Undead", -30);
        AssertEq(Taken(zombie, () => SpellCaster.Cast(Spell(SpellNames.CURE_MINOR_WOUNDS), cleric1, zombie), out _), 1,
            "Cure Minor Wounds deals 1 to an undead");
        CharacterStats foe = Target("Saver", "Humanoid", 30);
        AssertEq(Taken(foe, () => SpellCaster.Cast(Spell(SpellNames.INFLICT_MINOR_WOUNDS), cleric1, foe), out _), 0,
            "Inflict Minor Wounds: a Will save negates");
        foe = Target("Foe", "Humanoid", -30);
        AssertEq(Taken(foe, () => SpellCaster.Cast(Spell(SpellNames.INFLICT_MINOR_WOUNDS), cleric1, foe), out _), 1,
            "Inflict Minor Wounds deals 1 on a failed save");
    }

    private static void TestSpellResistance()
    {
        CharacterStats cleric5 = Caster("Cleric", 5);
        CharacterStats undead = Target("Warded undead", "Undead", -30);
        undead.SpellResistance = 100;
        int taken = Taken(undead, () => SpellCaster.Cast(Spell(SpellNames.CURE_LIGHT_WOUNDS), cleric5, undead), out SpellResult r);
        Assert(taken == 0 && r.SpellResistanceChecked && !r.SpellResistancePassed, "An undead applies spell resistance against a cure (PHB p.215)");

        CharacterStats living = Target("Warded foe", "Humanoid", -30);
        living.SpellResistance = 100;
        taken = Taken(living, () => SpellCaster.Cast(Spell(SpellNames.INFLICT_LIGHT_WOUNDS), cleric5, living), out r);
        Assert(taken == 0 && r.SpellResistanceChecked && !r.SpellResistancePassed, "Spell resistance applies to Inflict Light Wounds (PHB p.244)");
    }

    // ========== CONSUMABLES ==========

    private static void TestBakeForCasterLevel()
    {
        SpellData template = SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS);
        SpellData clone = template.Clone();
        SpellDiceRules.BakeForCasterLevel(clone, 1);
        Assert(clone.HealCount == 1 && clone.BonusHealing == 1 && clone.LevelBonusPerLevels == 0,
            "A CL 1 item's Cure Light Wounds bakes to 1d8+1 (DMG p.229 potion)", $"{clone.HealCount}d8+{clone.BonusHealing}");
        Assert(template.LevelBonusPerLevels == 1 && template.LevelBonusMax == 5 && template.BonusHealing == 0,
            "Baking a clone leaves the database template unchanged");

        SpellData burning = SpellDatabase.GetSpell(SpellNames.BURNING_HANDS).Clone();
        SpellDiceRules.BakeForCasterLevel(burning, 9);
        Assert(burning.DamageCount == 5 && burning.ScalingDicePerLevels == 0, "A CL 9 Burning Hands bakes to 5d4", burning.DamageCount + "d4");
    }

    /// <summary>
    /// The combat scroll and wand pipelines (GameManager.BuildScrollPipelineSpell / BuildWandPipelineSpell, which
    /// InitiateScrollCastThroughPipeline and InitiateWandCastThroughPipeline use) fix the dice at the item's caster
    /// level, not the user's (DMG p.213 wands, p.237 scrolls).
    /// </summary>
    private static void TestItemPipelineCasterLevel()
    {
        SpellData cureTemplate = SpellDatabase.GetSpell(SpellNames.CURE_LIGHT_WOUNDS);
        var wand = new ItemData { Name = "Test wand of Cure Light Wounds", IsWand = true, WandCasterLevel = 1, Wand = WandData.Create(cureTemplate, 1, false, 750) };
        SpellData wandSpell = GameManager.BuildWandPipelineSpell(wand, cureTemplate, out _, out int wandCL);
        AssertEq(wandCL, 1, "A CL 1 wand casts at caster level 1");
        CharacterStats cleric5 = Caster("Cleric", 5);
        CharacterStats ally = Target("Wounded ally", "Humanoid", -30, damageTaken: 50);
        AssertEq(-Taken(ally, () => SpellCaster.Cast(wandSpell, cleric5, ally, null, true, true), out _), 9,
            "A CL 1 wand of Cure Light Wounds used by a 5th-level cleric cures 1d8+1 (max 9), not 1d8+5");
        Assert(cureTemplate.LevelBonusPerLevels == 1 && cureTemplate.BonusHealing == 0, "Building the wand spell leaves the template unchanged");

        SpellData burningTemplate = SpellDatabase.GetSpell(SpellNames.BURNING_HANDS);
        var scroll = new ItemData { Name = "Test scroll of Burning Hands", IsScroll = true, Scroll = ScrollData.Create(burningTemplate, 1, true, 25) };
        SpellData scrollSpell = GameManager.BuildScrollPipelineSpell(scroll, burningTemplate, out _, out int scrollCL);
        AssertEq(scrollCL, 1, "A CL 1 scroll casts at caster level 1");
        CharacterStats wiz7 = Caster("Wizard", 7);
        CharacterStats foe = Target("Foe", "Humanoid", -30);
        AssertEq(Taken(foe, () => SpellCaster.Cast(scrollSpell, wiz7, foe), out _), 4,
            "A CL 1 scroll of Burning Hands read by a 7th-level wizard deals 1d4 (max 4), not 5d4");
    }

    /// <summary>Factory-made potion and wand heal dice at the item's caster level; Cure Minor Wounds is a flat 1 point (PHB p.216). The hand-registered Cure Light Wounds potion is a spell-effect potion (BuildConsumableSpellVariant).</summary>
    private static void TestFactoryHealDice()
    {
        ItemDatabase.Init();
        ItemData minor = ItemDatabase.GetItem(PotionFactory.GeneratePotionId(SpellNames.CURE_MINOR_WOUNDS));
        Assert(minor != null && minor.HealDiceCount == 0 && minor.HealAmount == 1 && minor.HealBonus == 0,
            "A potion of Cure Minor Wounds heals a flat 1 point, not 1d1+1",
            minor == null ? "no potion" : $"{minor.HealDiceCount}d{minor.HealDiceSides}+{minor.HealBonus}, flat {minor.HealAmount}");
        ItemData moderate = ItemDatabase.GetItem(PotionFactory.GeneratePotionId(SpellNames.CURE_MODERATE_WOUNDS));
        Assert(moderate != null && moderate.ConsumableMinimumCasterLevel == 3 && moderate.HealDiceCount == 2 && moderate.HealDiceSides == 8 && moderate.HealBonus == 3,
            "A CL 3 potion of Cure Moderate Wounds heals 2d8+3 (DMG p.229)",
            moderate == null ? "no potion" : $"CL {moderate.ConsumableMinimumCasterLevel}: {moderate.HealDiceCount}d{moderate.HealDiceSides}+{moderate.HealBonus}");
        ItemData wand = ItemDatabase.GetItem(WandFactory.GenerateWandId(SpellNames.CURE_LIGHT_WOUNDS));
        Assert(wand != null && wand.HealDiceCount == 1 && wand.HealDiceSides == 8 && wand.HealBonus == 1,
            "A CL 1 wand of Cure Light Wounds heals 1d8+1 (DMG p.213)",
            wand == null ? "no wand" : $"{wand.HealDiceCount}d{wand.HealDiceSides}+{wand.HealBonus}");
    }
}
}

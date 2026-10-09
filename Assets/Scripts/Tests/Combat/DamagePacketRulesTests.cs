using UnityEngine;
using Tests.Utilities;

namespace Tests.Combat
{
/// <summary>
/// SPL-004: the packets for damage that is not a weapon hit (<see cref="DamagePackets"/>) through
/// <see cref="CharacterStats.ApplyIncomingDamage"/>. RAW: damage reduction does not reduce energy damage, spells,
/// spell-like or supernatural abilities (MM p.307), but does reduce a swarm's nonmagical attack (MM p.316); resistance
/// to energy ignores its amount of each hit of that type (MM p.314); immunity negates it; Fire Shield halves the opposed
/// energy and negates it on a successful Reflex save for half (PHB p.230). Stats only, no controllers or scene.
/// </summary>
public static class DamagePacketRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        Debug.Log("====== DAMAGE PACKET RULES TESTS (SPL-004) ======");

        RaceDatabase.Init();
        ClassRegistry.Init();

        TestSpellIgnoresDamageReduction();
        TestSpellTakesResistanceAndImmunity();
        TestSupernaturalIgnoresDamageReduction();
        TestCreatureAttackPhysicalTakesDamageReduction();
        TestCreatureAttackEnergyIgnoresDamageReduction();
        TestFireShieldNegatesReflexHalf();
        TestGenericSpellCastFireShieldReflexHalf();
        TestHandlerOwnedDamageSkipsGenericBranch();
        TestTemporaryHitPointsAbsorbFirst();

        Debug.Log($"====== Damage Packet Results: {_passed} passed, {_failed} failed ======");
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

    private static CharacterStats Target(string name)
    {
        var stats = new CharacterStats(
            name: name, level: 5, characterClass: "Fighter",
            str: 12, dex: 10, con: 12, wis: 10, intelligence: 10, cha: 10,
            bab: 5, armorBonus: 0, shieldBonus: 0,
            damageDice: 6, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 100, raceName: "Human");
        stats.CurrentHP = 100;
        return stats;
    }

    private static int Deal(CharacterStats target, int raw, DamagePacket packet)
    {
        int before = target.CurrentHP;
        target.ApplyIncomingDamage(raw, packet);
        return before - target.CurrentHP;
    }

    private static void TestSpellIgnoresDamageReduction()
    {
        CharacterStats t = Target("DR target");
        t.AddDamageReduction(10, DamageBypassTag.Magic);
        Assert(Deal(t, 20, DamagePackets.Spell("Test fire spell", DamageType.Fire)) == 20, "A fire spell ignores DR 10/magic (MM p.307)");
        Assert(Deal(t, 20, DamagePackets.Spell("Test bludgeoning spell", DamageType.Bludgeoning)) == 20, "A bludgeoning spell (Ice Storm's hail) ignores DR 10/magic (MM p.307)");
        Assert(Deal(t, 20, DamagePackets.Spell("Test force spell", DamageType.Force)) == 20, "Force damage that strikes as a spell ignores DR (PHB p.283)");
    }

    private static void TestSpellTakesResistanceAndImmunity()
    {
        CharacterStats t = Target("Resist target");
        t.AddDamageResistance(DamageType.Cold, 10);
        Assert(Deal(t, 25, DamagePackets.Spell("Cone of Cold", DamageType.Cold)) == 15, "Cold resistance 10 reduces a 25-point cold spell to 15 (MM p.314)");
        Assert(Deal(t, 25, DamagePackets.Spell("Fireball", DamageType.Fire)) == 25, "Cold resistance does not reduce fire");
        Assert(Deal(t, 6, DamagePackets.Spell("Cone of Cold", DamageType.Cold)) == 0, "Resistance cannot take damage below 0");

        CharacterStats immune = Target("Immune target");
        immune.AddDamageImmunity(DamageType.Fire);
        Assert(Deal(immune, 40, DamagePackets.Spell("Fireball", DamageType.Fire)) == 0, "Fire immunity negates a fire spell");
        Assert(Deal(immune, 12, DamagePackets.Spell("Disintegrate", DamageType.Untyped)) == 12, "Fire immunity does not reduce untyped spell damage");
    }

    private static void TestSupernaturalIgnoresDamageReduction()
    {
        CharacterStats t = Target("Breath target");
        t.AddDamageReduction(5, DamageBypassTag.None);
        t.AddDamageResistance(DamageType.Fire, 10);
        DamagePacket breath = DamagePackets.Supernatural("Red dragon breath", DamageType.Fire);
        Assert(breath.Source != AttackSource.Weapon && breath.Source != AttackSource.Natural, "A breath weapon packet is not a weapon or natural attack");
        Assert(Deal(t, 20, breath) == 10, "A breath weapon takes fire resistance 10 but not DR 5/- (MM p.307, p.314)");
    }

    private static void TestCreatureAttackPhysicalTakesDamageReduction()
    {
        CharacterStats t = Target("Swarm victim");
        t.AddDamageReduction(5, DamageBypassTag.None);
        DamagePacket swarm = DamagePackets.CreatureAttack("Rat swarm", DamageType.Piercing);
        Assert(swarm.Source == AttackSource.Natural, "A physical swarm attack counts as a natural attack");
        Assert(Deal(t, 7, swarm) == 2, "DR 5/- reduces a 7-point swarm attack to 2 (MM p.316)");

        CharacterStats skeleton = Target("DR bludgeoning");
        skeleton.AddDamageReduction(5, DamageBypassTag.Bludgeoning);
        Assert(Deal(skeleton, 7, DamagePackets.CreatureAttack("Piercing swarm", DamageType.Piercing)) == 2, "DR 5/bludgeoning reduces piercing swarm damage");
        Assert(Deal(skeleton, 7, DamagePackets.CreatureAttack("Bludgeoning attack", DamageType.Bludgeoning)) == 7, "Bludgeoning damage bypasses DR 5/bludgeoning");
    }

    private static void TestCreatureAttackEnergyIgnoresDamageReduction()
    {
        CharacterStats t = Target("Spittle victim");
        t.AddDamageReduction(5, DamageBypassTag.None);
        t.AddDamageResistance(DamageType.Acid, 2);
        DamagePacket spittle = DamagePackets.CreatureAttack("Spittle", DamageType.Acid, true);
        Assert(spittle.Source == AttackSource.Other, "An acid spittle attack is not a weapon or natural attack");
        Assert(Deal(t, 4, spittle) == 2, "Acid spittle takes acid resistance 2 but not DR 5/- (MM p.307)");
    }

    private static void TestFireShieldNegatesReflexHalf()
    {
        CharacterStats t = Target("Chill shield");
        t.FireShieldActive = true;
        t.FireShieldIsWarm = false; // chill shield: half fire damage
        Assert(Deal(t, 20, DamagePackets.Spell("Fireball", DamageType.Fire)) == 10, "Chill Shield halves fire damage (PHB p.230)");
        Assert(Deal(t, 10, DamagePackets.Spell("Fireball", DamageType.Fire, true)) == 0, "Chill Shield negates fire after a successful Reflex save for half (PHB p.230)");
        Assert(Deal(t, 10, DamagePackets.Spell("Cone of Cold", DamageType.Cold, true)) == 10, "Chill Shield does not reduce cold");
        t.FireShieldActive = false;
    }

    /// <summary>
    /// The generic damage branch of <see cref="SpellCaster.Cast"/> (spells without a custom handler, e.g. Burning
    /// Hands) marks a successful Reflex save for half on its packet, so Chill Shield negates it as for Fireball.
    /// </summary>
    private static void TestGenericSpellCastFireShieldReflexHalf()
    {
        SpellDatabase.Init();
        SpellData template = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.BURNING_HANDS);
        if (template == null)
        {
            Assert(false, "Burning Hands is registered for the generic Fire Shield check");
            return;
        }

        CharacterStats caster = TestHelpers.CreateStats(name: "Wizard", characterClass: "Wizard", level: 5, intelligence: 10, bab: 2);
        int savedCasts = 0;
        int savedWithDamage = 0;
        for (int i = 0; i < 60; i++)
        {
            CharacterStats t = Target("Chill shield (generic)");
            t.FireShieldActive = true;
            t.FireShieldIsWarm = false;
            int before = t.CurrentHP;
            SpellResult result = SpellCaster.Cast(template.Clone(), caster, t);
            if (result == null || !result.RequiredSave || !result.SaveSucceeded)
                continue;
            savedCasts++;
            if (t.CurrentHP != before)
                savedWithDamage++;
        }

        Assert(savedCasts > 0, "Burning Hands rolled at least one successful Reflex save in 60 casts", $"saved={savedCasts}");
        Assert(savedWithDamage == 0,
            "Chill Shield negates generic Burning Hands damage after a successful Reflex save for half (PHB p.230)",
            $"saved={savedCasts} savedButDamaged={savedWithDamage}");
    }

    /// <summary>
    /// SPL-124: Searing Light's handler deals its damage by creature type, so the generic damage branch of
    /// <see cref="SpellCaster.Cast"/> must deal none (it used to deal a hidden 2d8 first).
    /// </summary>
    private static void TestHandlerOwnedDamageSkipsGenericBranch()
    {
        SpellDatabase.Init();
        SpellData template = SpellDatabase.GetSpell(DND35e.Identifiers.SpellNames.SEARING_LIGHT);
        if (template == null)
        {
            Assert(false, "Searing Light is registered for the generic-branch check");
            return;
        }

        CharacterStats caster = TestHelpers.CreateStats(name: "Cleric", characterClass: "Cleric", level: 5, wis: 16, bab: 3);
        int hits = 0;
        int damagedByGeneric = 0;
        for (int i = 0; i < 30; i++)
        {
            CharacterStats t = Target("Searing Light target");
            int before = t.CurrentHP;
            SpellResult result = SpellCaster.Cast(template.Clone(), caster, t);
            if (result == null || !result.AttackHit || !result.Success)
                continue;
            hits++;
            if (t.CurrentHP != before || result.DamageDealt != 0)
                damagedByGeneric++;
        }

        Assert(template.DamageResolvedByHandler, "Searing Light's damage belongs to its handler (DamageResolvedByHandler)");
        Assert(hits > 0, "Searing Light's ranged touch hit at least once in 30 casts", $"hits={hits}");
        Assert(damagedByGeneric == 0,
            "SpellCaster.Cast deals no generic damage for Searing Light; its handler deals it (SPL-124)",
            $"hits={hits} damagedByGeneric={damagedByGeneric}");
    }

    private static void TestTemporaryHitPointsAbsorbFirst()
    {
        CharacterStats t = Target("Temp HP");
        t.TempHP = 5;
        int lost = Deal(t, 8, DamagePackets.Spell("Spike Stones", DamageType.Piercing));
        Assert(t.TempHP == 0 && lost == 3, "Temporary hit points absorb spell damage first (Spike Stones and Black Tentacles wrote CurrentHP directly)", $"tempHP={t.TempHP} lost={lost}");
    }
}
}

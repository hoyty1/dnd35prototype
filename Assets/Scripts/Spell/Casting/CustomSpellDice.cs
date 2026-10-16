using UnityEngine;

/// <summary>
/// The dice of the spells whose custom resolver rolls them itself instead of SpellCaster.Cast's generic branch (batch 11
/// step 19, SPL-038): one spec per resolver, each rolled through SpellDiceRules.Roll with the cast's metamagic
/// (ctx.RollSpellDice, SpellDiceRules.RollFor), so Empower, Maximize and both apply (PHB p.93, p.97) to every caster's
/// cast. The specs keep each resolver's dice as the code rolled them before this step, so a plain cast draws the same
/// dice; a spec that differs from the PHB is filed (Searing Light, SPL-177). Tests.Magic.CustomSpellDiceTests checks
/// each spec with and without the metamagic.
/// The specs cover every variable, numeric effect a spell rolls (PHB p.93, p.97), not only damage and healing: rolled
/// durations, Hit Dice pools, counts, ability damage, temporary hit points and a lasting effect's later dice; a number that
/// is not the spell's own effect (a summoned swarm's attacks, Spectral Hand's cost, a disease's damage) is not one
/// (delegated RAW reading 2026-10-14, RULES_COVERAGE 4.1).
/// </summary>
public static class CustomSpellDice
{
    /// <summary>Fireball and Lightning Bolt: 1d6 per caster level, max 10d6 (PHB p.231, p.248).</summary>
    public static SpellDice FireballOrLightningBolt(int casterLevel) => SpellDice.Of(Mathf.Clamp(casterLevel, 1, 10), 6);

    /// <summary>Call Lightning: 3d6 per bolt (PHB p.207).</summary>
    public static SpellDice CallLightningBolt() => SpellDice.Of(3, 6);

    /// <summary>Ice Storm: 3d6 bludgeoning (PHB p.243).</summary>
    public static SpellDice IceStormBludgeoning() => SpellDice.Of(3, 6);

    /// <summary>Ice Storm: 2d6 cold (PHB p.243).</summary>
    public static SpellDice IceStormCold() => SpellDice.Of(2, 6);

    /// <summary>Shout: 5d6 sonic (PHB p.279).</summary>
    public static SpellDice Shout() => SpellDice.Of(5, 6);

    /// <summary>Shout: deafened for 2d6 rounds on a failed save (PHB p.279).</summary>
    public static SpellDice ShoutDeafenedRounds() => SpellDice.Of(2, 6);

    /// <summary>Cone of Cold: 1d6 per caster level, max 15d6 (PHB p.212).</summary>
    public static SpellDice ConeOfCold(int casterLevel) => SpellDice.Of(Mathf.Clamp(casterLevel, 1, 15), 6);

    /// <summary>Chain Lightning's primary bolt: 1d6 per caster level, max 20d6 (PHB p.208); the secondary bolts take half.</summary>
    public static SpellDice ChainLightning(int casterLevel) => SpellDice.Of(Mathf.Clamp(casterLevel, 1, 20), 6);

    /// <summary>Circle of Death: 1d4 Hit Dice of creatures per caster level, max 20d4 (PHB p.209).</summary>
    public static SpellDice CircleOfDeathHitDice(int casterLevel) => SpellDice.Of(Mathf.Clamp(casterLevel, 1, 20), 4);

    /// <summary>Sunburst: 6d6, an undead 1d6 per caster level, max 25d6 (PHB p.289).</summary>
    public static SpellDice Sunburst(int casterLevel, bool undead) => SpellDice.Of(undead ? Mathf.Clamp(casterLevel, 1, 25) : 6, 6);

    /// <summary>
    /// Searing Light as the handler rolls it: 1d8 per two caster levels (max 5d8), a construct 1d6 per two levels (max
    /// 5d6); a living creature takes half the roll. PHB p.275 gives 1d8 per two levels to any creature and 1d6 per level
    /// (max 10d6) to an undead (SPL-177).
    /// </summary>
    public static SpellDice SearingLight(int casterLevel, bool construct) => SpellDice.Of(Mathf.Clamp(casterLevel / 2, 1, 5), construct ? 6 : 8);

    /// <summary>Vampiric Touch: 1d6 per two caster levels, max 10d6 (PHB p.298).</summary>
    public static SpellDice VampiricTouch(int casterLevel) => SpellDice.Of(Mathf.Clamp(casterLevel / 2, 1, 10), 6);

    /// <summary>Melf's Acid Arrow's later rounds: 2d4 acid each (PHB p.253); the first round's 2d4 is SpellCaster.Cast's generic damage.</summary>
    public static SpellDice MelfsAcidArrowRound() => SpellDice.Of(2, 4);

    /// <summary>Produce Flame: 1d6 + 1 per caster level, max +5 (PHB p.265).</summary>
    public static SpellDice ProduceFlame(int casterLevel) => SpellDice.Of(1, 6, Mathf.Min(casterLevel, 5));

    /// <summary>Heat Metal's round <paramref name="round"/> (1-7): warm 0, hot 1d4, searing 2d4 for three rounds, hot 1d4... as the handler's schedule gives it (PHB p.239).</summary>
    public static SpellDice HeatMetalRound(int round)
    {
        switch (round)
        {
            case 2: return SpellDice.Of(1, 4);
            case 3:
            case 4:
            case 5: return SpellDice.Of(2, 4);
            case 6: return SpellDice.Of(1, 4);
            default: return SpellDice.Of(0, 4);
        }
    }

    /// <summary>Chaos Hammer, Holy Smite, Order's Wrath and Unholy Blight: 1d8 per two caster levels, max 5d8 (PHB p.208, p.241, p.258, p.297).</summary>
    public static SpellDice AlignmentBurst(int casterLevel) => SpellDice.Of(Mathf.Clamp(casterLevel / 2, 1, 5), 8);

    /// <summary>Disintegrate: 2d6 per caster level, max 40d6; 5d6 after a successful save (PHB p.222).</summary>
    public static SpellDice Disintegrate(int casterLevel, bool saved) => SpellDice.Of(GameManager.DisintegrateDiceCount(casterLevel, saved), 6);

    /// <summary>Phantasmal Killer: 3d6 when the Fortitude save succeeds (PHB p.260).</summary>
    public static SpellDice PhantasmalKiller() => SpellDice.Of(3, 6);

    /// <summary>Spiritual Weapon: 1d8 + 1 per three caster levels, max +5, force (PHB p.283).</summary>
    public static SpellDice SpiritualWeapon(int casterLevel) => SpellDice.Of(1, 8, Mathf.Min(5, casterLevel / 3));

    /// <summary>Calm Animals: 2d4 + caster level Hit Dice of animals (PHB p.207).</summary>
    public static SpellDice CalmAnimalsHitDice(int casterLevel) => SpellDice.Of(2, 4, casterLevel);

    /// <summary>Hypnotism: 2d4 Hit Dice of creatures, and 2d4 rounds (PHB p.242).</summary>
    public static SpellDice HypnotismHitDice() => SpellDice.Of(2, 4);

    /// <inheritdoc cref="HypnotismHitDice"/>
    public static SpellDice HypnotismRounds() => SpellDice.Of(2, 4);

    /// <summary>Fire Shield's retribution: 1d6 + 1 per caster level, max +15 (PHB p.230).</summary>
    public static SpellDice FireShieldRetribution(int casterLevel) => SpellDice.Of(1, 6, Mathf.Min(casterLevel, 15));

    /// <summary>
    /// Wall of Fire: a creature passing through takes 2d6 + 1 per caster level, max +20 (PHB p.298); the 2d4 (within 10 ft)
    /// and 1d4 (within 20 ft) heat bands are rolled in WallOfFireAreaEffect.
    /// </summary>
    public static SpellDice WallOfFirePassThrough(int bonus) => SpellDice.Of(2, 6, bonus);

    /// <summary>Black Tentacles: 1d6+4 bludgeoning to a grappled creature each round (PHB p.228).</summary>
    public static SpellDice BlackTentaclesCrush() => SpellDice.Of(1, 6, 4);

    /// <summary>False Life: 1d10 + 1 per caster level (max +10) temporary hit points (PHB p.229).</summary>
    public static SpellDice FalseLife(int casterLevel) => SpellDice.Of(1, 10, Mathf.Min(casterLevel, 10));

    /// <summary>Spell Turning: 1d4+6 spell levels (PHB p.282).</summary>
    public static SpellDice SpellTurningLevels() => SpellDice.Of(1, 4, 6);
}

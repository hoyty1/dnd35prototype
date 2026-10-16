using UnityEngine;

/// <summary>
/// The energy a Cure or Inflict spell channels. Positive energy cures the living and harms undead; negative energy
/// harms the living and cures undead (PHB p.215 Cure Light Wounds, p.244 Inflict Light Wounds; SPL-005).
/// </summary>
public enum SpellEnergy
{
    None,
    Positive,
    Negative,
}

/// <summary>How a <see cref="SpellEnergy"/> spell affects one target.</summary>
public enum SpellEnergyOutcome
{
    /// <summary>No energy spell, or a living target: the spell works as written.</summary>
    Normal,
    /// <summary>Positive energy on an undead: the cure dice are damage, Will half, spell resistance applies (PHB p.215).</summary>
    HarmsUndead,
    /// <summary>Negative energy on an undead: the inflict dice cure it (PHB p.244).</summary>
    HealsUndead,
    /// <summary>A construct (or other nonliving, non-undead creature): no effect (PHB p.215 cures a living creature; MM p.307 constructs are immune to necromancy).</summary>
    NoEffect,
}

/// <summary>The dice one cast rolls: Count d Sides + Bonus at CasterLevel.</summary>
public struct SpellDice
{
    public int Count;
    public int Sides;
    public int Bonus;
    public int CasterLevel;

    public bool HasDice => Count > 0 && Sides > 0;
    public float Average => (HasDice ? Count * (Sides + 1) * 0.5f : 0f) + Bonus;
    public int Maximum => (HasDice ? Count * Sides : 0) + Bonus;

    /// <summary>Count d Sides + Bonus (a custom resolver's dice: Fireball's 1d6 per level is Of(n, 6)).</summary>
    public static SpellDice Of(int count, int sides, int bonus = 0)
        => new SpellDice { Count = count, Sides = sides, Bonus = bonus };

    public override string ToString()
    {
        if (!HasDice)
            return Bonus.ToString();
        if (Bonus == 0)
            return Count + "d" + Sides;
        return Count + "d" + Sides + (Bonus > 0 ? "+" : "") + Bonus;
    }
}

/// <summary>
/// One roll of a spell's dice with the cast's metamagic (<see cref="SpellDiceRules.Roll(SpellDice, MetamagicData, string)"/>).
/// The result of the dice is the dice plus the spell's own fixed term (<see cref="SpellDice.Bonus"/>), as PHB p.93
/// multiplies a magic missile's whole 1d4+1.
/// </summary>
public struct SpellDiceRoll
{
    /// <summary>The dice as they count: every die at its maximum when maximized, else the dice rolled.</summary>
    public int[] Rolls;
    /// <summary>The dice actually rolled: the same as <see cref="Rolls"/> without Maximize; with Maximize and Empower the normal roll whose half Empower adds; empty when maximized only.</summary>
    public int[] NormalRolls;
    public int Bonus;
    /// <summary>The result before Empower: the dice that count plus the bonus, at least 0.</summary>
    public int Base;
    /// <summary>Empower's addition: half of the normally rolled result (rounded down, PHB p.304), 0 without Empower.</summary>
    public int EmpowerBonus;
    /// <summary>The roll's final result: <see cref="Base"/> + <see cref="EmpowerBonus"/>.</summary>
    public int Total;
    public bool Maximized;
    public bool Empowered;

    public static implicit operator int(SpellDiceRoll roll) => roll.Total;
}

/// <summary>
/// One rule for the dice a spell rolls at a caster level and for the Cure and Inflict energy on undead (SPL-005).
/// <see cref="SpellCaster.Cast"/> (the PC single-target and area pipelines, the NPC cast path, charmed creatures,
/// Flaming Sphere), the consumable and staff healing paths, the AI's estimates and the UI all read it.
///
/// Data on <see cref="SpellData"/>: the base dice (DamageCount/DamageDice/BonusDamage for a Damage spell,
/// HealCount/HealDice/BonusHealing for a Healing spell), <see cref="SpellData.ScalingDicePerLevels"/> and
/// <see cref="SpellData.ScalingDiceMax"/> (one die per N caster levels, capped: Burning Hands 1d4/level, max 5d4),
/// <see cref="SpellData.LevelBonusPerLevels"/> and <see cref="SpellData.LevelBonusMax"/> (+1 per N caster levels,
/// capped: Cure Light Wounds +1/level, max +5), and <see cref="SpellData.Energy"/>.
/// Spells whose custom handler rolls the damage (Fireball, Lightning Bolt, Cone of Cold, Vampiric Touch, ...) scale
/// in that handler and leave these fields unset.
/// </summary>
public static class SpellDiceRules
{
    /// <summary>DiceService context of every damage die rolled by the generic branch of SpellCaster.Cast.</summary>
    public const string DamageDieContext = "Spell damage die";
    /// <summary>DiceService context of every healing die rolled by the generic branch of SpellCaster.Cast.</summary>
    public const string HealingDieContext = "Spell healing die";

    /// <summary>
    /// The caster level of a cast by a caster known by its stats: CasterLevelRules.ForStats (the casting class's caster
    /// level with the domain boost, half for a paladin or ranger, or the Hit Dice of a creature with no casting class), at
    /// least 1; the rule SpellDurationRules.CasterLevelFor and SpellCastingHelper.GetEffectiveCasterLevel use (batch 11
    /// step 14, SPL-017). A scroll's or wand's dice are already fixed at its caster level (BakeForCasterLevel).
    /// </summary>
    public static int CasterLevelFor(CharacterStats caster, SpellData spell)
        => CasterLevelRules.ForStats(caster, spell);

    /// <summary>Dice count: the base count, or one die per <paramref name="perLevels"/> caster levels (at least 1, at most <paramref name="max"/>) when scaling is set.</summary>
    public static int ScaledCount(int baseCount, int perLevels, int max, int casterLevel)
    {
        if (perLevels <= 0)
            return Mathf.Max(0, baseCount);

        int count = Mathf.Max(1, Mathf.Max(1, casterLevel) / perLevels);
        return max > 0 ? Mathf.Min(count, max) : count;
    }

    /// <summary>The caster-level bonus: +1 per <paramref name="perLevels"/> caster levels, at most <paramref name="max"/> (0 when not set).</summary>
    public static int LevelBonus(int perLevels, int max, int casterLevel)
    {
        if (perLevels <= 0)
            return 0;

        int bonus = Mathf.Max(0, casterLevel) / perLevels;
        return max > 0 ? Mathf.Min(bonus, max) : bonus;
    }

    /// <summary>The damage dice of a spell at a caster level (DamageCount/DamageDice/BonusDamage plus the scaling fields).</summary>
    public static SpellDice Damage(SpellData spell, int casterLevel)
    {
        if (spell == null)
            return default;

        return new SpellDice
        {
            Count = ScaledCount(spell.DamageCount, spell.ScalingDicePerLevels, spell.ScalingDiceMax, casterLevel),
            Sides = Mathf.Max(0, spell.DamageDice),
            Bonus = spell.BonusDamage + LevelBonus(spell.LevelBonusPerLevels, spell.LevelBonusMax, casterLevel),
            CasterLevel = casterLevel,
        };
    }

    /// <summary>The healing dice of a spell at a caster level (HealCount/HealDice/BonusHealing plus the scaling fields).</summary>
    public static SpellDice Healing(SpellData spell, int casterLevel)
    {
        if (spell == null)
            return default;

        return new SpellDice
        {
            Count = ScaledCount(spell.HealCount, spell.ScalingDicePerLevels, spell.ScalingDiceMax, casterLevel),
            Sides = Mathf.Max(0, spell.HealDice),
            Bonus = spell.BonusHealing + LevelBonus(spell.LevelBonusPerLevels, spell.LevelBonusMax, casterLevel),
            CasterLevel = casterLevel,
        };
    }

    /// <summary>The spell's own dice: healing dice for a Healing spell, damage dice otherwise (a cure's dice stay cure dice when they harm an undead).</summary>
    public static SpellDice Effect(SpellData spell, int casterLevel)
        => spell != null && spell.EffectType == SpellEffectType.Healing ? Healing(spell, casterLevel) : Damage(spell, casterLevel);

    // ========== METAMAGIC ON THE DICE (batch 11 step 19, SPL-038, SPL-097) ==========

    /// <summary>
    /// The metamagic a cast's dice use: the cast's own (<paramref name="metamagic"/>) when it applies any feat, else the
    /// one stamped on the cast's spell clone (<see cref="SpellData.MetamagicDataRef"/>: SpellCaster.ApplyMetamagicToSpellData
    /// stamps it, a prepared metamagic slot's spell carries its slot's), else the (empty or null) request metamagic. An
    /// empty request metamagic (the scroll, wand and F12 paths pass one) never hides the stamp, so every reader agrees:
    /// SpellSaveDCRules and CastComponentRules use this fallback too.
    /// </summary>
    public static MetamagicData MetamagicOf(SpellData spell, MetamagicData metamagic = null)
        => metamagic != null && metamagic.HasAnyMetamagic ? metamagic : (spell?.MetamagicDataRef ?? metamagic);

    /// <summary>
    /// Rolls a spell's dice with the cast's metamagic, the one dice rule of every cast path and custom resolver:
    /// each die rolled is one DiceService.RollDie with <paramref name="context"/> (so a scenario can force it, and a
    /// plain cast draws exactly the dice it drew before this rule). Maximize (PHB p.97): every die at its maximum, none
    /// rolled. Empower (PHB p.93): the result (dice + <see cref="SpellDice.Bonus"/>) increased by one-half, rounded down
    /// (PHB p.304). Both (PHB p.97-98, SPL-097): the maximum result plus one-half of the normally rolled result, so the
    /// dice are rolled for the Empower half. That Empower multiplies the fixed term with the dice and that a result with
    /// no die is unchanged is a delegated RAW reading 2026-10-14 (RULES_COVERAGE 4.1).
    /// </summary>
    public static SpellDiceRoll Roll(SpellDice dice, MetamagicData metamagic, string context)
        => RollWith(dice, metamagic, sides => DiceService.RollDie(sides, context));

    /// <summary>
    /// As <see cref="Roll(SpellDice, MetamagicData, string)"/>, for the few spell dice that have always been drawn
    /// straight from UnityEngine.Random without the scenario filter (Black Tentacles' grapple damage, Spike Stones):
    /// the same draws, so their plain casts keep their traces.
    /// </summary>
    public static SpellDiceRoll RollUnfiltered(SpellDice dice, MetamagicData metamagic)
        => RollWith(dice, metamagic, sides => UnityEngine.Random.Range(1, sides + 1));

    /// <summary>The cast's dice for <paramref name="spell"/>: <see cref="Roll(SpellDice, MetamagicData, string)"/> with the metamagic stamped on the spell.</summary>
    public static SpellDiceRoll RollFor(SpellData spell, SpellDice dice, string context = null)
        => Roll(dice, MetamagicOf(spell), context);

    /// <summary>
    /// The pure rule of <see cref="Roll(SpellDice, MetamagicData, string)"/> over a die source (static tests use it without
    /// DiceService). Empower multiplies the fixed term with the dice, and a dieless result is unchanged (delegated RAW
    /// reading 2026-10-14, RULES_COVERAGE 4.1).
    /// </summary>
    public static SpellDiceRoll RollWith(SpellDice dice, MetamagicData metamagic, System.Func<int, int> rollDie)
    {
        bool maximize = metamagic != null && metamagic.Has(MetamagicFeatId.MaximizeSpell);
        bool empower = metamagic != null && metamagic.Has(MetamagicFeatId.EmpowerSpell);

        // A result with no die has no random variable, which neither feat changes (PHB p.93, p.97).
        if (!dice.HasDice)
        {
            maximize = false;
            empower = false;
        }

        var roll = new SpellDiceRoll { Bonus = dice.Bonus, Maximized = maximize, Empowered = empower };
        int count = dice.HasDice ? dice.Count : 0;

        // The dice rolled: every die without Maximize; with Maximize only when Empower needs the normal roll.
        int[] normal = new int[maximize && !empower ? 0 : count];
        for (int i = 0; i < normal.Length; i++)
            normal[i] = rollDie(dice.Sides);
        roll.NormalRolls = normal;

        int normalResult = dice.Bonus;
        for (int i = 0; i < normal.Length; i++)
            normalResult += normal[i];
        normalResult = Mathf.Max(0, normalResult);

        if (maximize)
        {
            roll.Rolls = new int[count];
            for (int i = 0; i < count; i++)
                roll.Rolls[i] = dice.Sides;
            roll.Base = Mathf.Max(0, (count > 0 ? count * dice.Sides : 0) + dice.Bonus);
        }
        else
        {
            roll.Rolls = normal;
            roll.Base = normalResult;
        }

        roll.EmpowerBonus = empower ? normalResult / 2 : 0;
        roll.Total = roll.Base + roll.EmpowerBonus;
        return roll;
    }

    /// <summary>Average of a spell's own dice at a caster level (AI estimates).</summary>
    public static float AverageEffect(SpellData spell, int casterLevel) => Effect(spell, casterLevel).Average;

    /// <summary>
    /// The expected result of <paramref name="dice"/> rolled with <paramref name="metamagic"/> by <see cref="RollWith"/>'s
    /// rule (AI estimates; batch 11 step 28): the average; with Maximize the maximum (PHB p.97); with Empower one and a half
    /// times the average (PHB p.93); with both the maximum plus half the average (PHB p.97-98). The rounding down of the
    /// Empower half and the floor at 0 are left out, so it is the mean to within half a point. A result with no die is
    /// unchanged by either feat.
    /// </summary>
    public static float ExpectedTotal(SpellDice dice, MetamagicData metamagic)
    {
        float average = dice.Average;
        if (!dice.HasDice || metamagic == null)
            return average;
        bool maximize = metamagic.Has(MetamagicFeatId.MaximizeSpell);
        bool empower = metamagic.Has(MetamagicFeatId.EmpowerSpell);
        float total = maximize ? dice.Maximum : average;
        if (empower)
            total += average * 0.5f;
        return total;
    }

    /// <summary>The spell's dice for display: "1d8 +1/level (max +5)", "1d4/level (max 5d4)", "2d6".</summary>
    public static string Describe(SpellData spell)
    {
        if (spell == null)
            return string.Empty;

        bool healing = spell.EffectType == SpellEffectType.Healing;
        int count = healing ? spell.HealCount : spell.DamageCount;
        int sides = healing ? spell.HealDice : spell.DamageDice;
        int bonus = healing ? spell.BonusHealing : spell.BonusDamage;

        string text;
        if (spell.ScalingDicePerLevels > 0 && sides > 0)
        {
            string per = spell.ScalingDicePerLevels == 1 ? "level" : spell.ScalingDicePerLevels + " levels";
            text = "1d" + sides + "/" + per + (spell.ScalingDiceMax > 0 ? " (max " + spell.ScalingDiceMax + "d" + sides + ")" : "");
        }
        else if (count > 0 && sides > 0)
        {
            text = count + "d" + sides;
        }
        else
        {
            text = spell.LevelBonusPerLevels > 0 ? string.Empty : bonus.ToString();
        }

        if (bonus != 0 && (count > 0 || spell.ScalingDicePerLevels > 0) && sides > 0)
            text += (bonus > 0 ? "+" : "") + bonus;

        if (spell.LevelBonusPerLevels > 0)
        {
            string per = spell.LevelBonusPerLevels == 1 ? "level" : spell.LevelBonusPerLevels + " levels";
            string levelPart = "+1/" + per + (spell.LevelBonusMax > 0 ? " (max +" + spell.LevelBonusMax + ")" : "");
            text = string.IsNullOrEmpty(text) ? levelPart : text + " " + levelPart;
        }

        return text;
    }

    /// <summary>Writes the dice at <paramref name="casterLevel"/> into a clone's base fields and clears its scaling, so code that reads the base fields (a scroll's metamagic, a staff) rolls the item's caster level.</summary>
    public static void BakeForCasterLevel(SpellData clone, int casterLevel)
    {
        if (clone == null)
            return;

        SpellDice dice = Effect(clone, casterLevel);
        if (clone.EffectType == SpellEffectType.Healing)
        {
            clone.HealCount = dice.Count;
            clone.BonusHealing = dice.Bonus;
        }
        else
        {
            clone.DamageCount = dice.Count;
            clone.BonusDamage = dice.Bonus;
        }

        clone.ScalingDicePerLevels = 0;
        clone.ScalingDiceMax = 0;
        clone.LevelBonusPerLevels = 0;
        clone.LevelBonusMax = 0;
    }

    // ========== POSITIVE AND NEGATIVE ENERGY ==========

    public static bool IsUndead(CharacterStats stats)
        => stats != null && string.Equals((stats.CreatureType ?? string.Empty).Trim(), "Undead", System.StringComparison.OrdinalIgnoreCase);

    public static bool IsConstruct(CharacterStats stats)
        => stats != null && string.Equals((stats.CreatureType ?? string.Empty).Trim(), "Construct", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// How an energy spell affects <paramref name="target"/>. Undead: positive energy harms and negative energy cures
    /// (PHB p.215, p.244). Constructs: a cure channels energy into a living creature (PHB p.215) and an inflict spell is
    /// a necromancy effect, to which constructs are immune (MM p.307), so neither has any effect. The other necromancy
    /// spells do not check this yet (SPL-129).
    /// </summary>
    public static SpellEnergyOutcome OutcomeFor(SpellData spell, CharacterStats target)
    {
        if (spell == null || target == null || spell.Energy == SpellEnergy.None)
            return SpellEnergyOutcome.Normal;

        if (IsUndead(target))
            return spell.Energy == SpellEnergy.Positive ? SpellEnergyOutcome.HarmsUndead : SpellEnergyOutcome.HealsUndead;

        if (IsConstruct(target))
            return SpellEnergyOutcome.NoEffect;

        return SpellEnergyOutcome.Normal;
    }

    /// <summary>True when the spell would hurt <paramref name="target"/>: a damage spell on a living target, or a cure on an undead.</summary>
    public static bool Harms(SpellData spell, CharacterStats target)
    {
        if (spell == null)
            return false;

        SpellEnergyOutcome outcome = OutcomeFor(spell, target);
        if (outcome == SpellEnergyOutcome.HarmsUndead)
            return true;
        if (outcome == SpellEnergyOutcome.HealsUndead || outcome == SpellEnergyOutcome.NoEffect)
            return false;
        return spell.EffectType == SpellEffectType.Damage;
    }

    /// <summary>True when the spell would cure <paramref name="target"/>: a healing spell on a living target, or an inflict spell on an undead.</summary>
    public static bool Heals(SpellData spell, CharacterStats target)
    {
        if (spell == null)
            return false;

        SpellEnergyOutcome outcome = OutcomeFor(spell, target);
        if (outcome == SpellEnergyOutcome.HealsUndead)
            return true;
        if (outcome == SpellEnergyOutcome.HarmsUndead || outcome == SpellEnergyOutcome.NoEffect)
            return false;
        return spell.EffectType == SpellEffectType.Healing;
    }
}

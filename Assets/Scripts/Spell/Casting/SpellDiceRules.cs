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
    /// The caster level of a cast: the domain-boosted caster level, else the character level, at least 1 (the rule
    /// SpellDurationRules.CasterLevelFor and SpellCastingHelper.GetEffectiveCasterLevel use).
    /// </summary>
    public static int CasterLevelFor(CharacterStats caster, SpellData spell)
    {
        if (caster == null)
            return 1;

        int casterLevel = caster.GetDomainBoostedCasterLevel(spell);
        if (casterLevel <= 0)
            casterLevel = caster.Level;
        return Mathf.Max(1, casterLevel);
    }

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

    /// <summary>Rolls the dice (each die through DiceService with <paramref name="context"/>, so a scenario can force it); maximized dice take their highest face.</summary>
    public static int[] Roll(SpellDice dice, bool maximize, string context)
    {
        if (!dice.HasDice)
            return new int[0];

        var rolls = new int[dice.Count];
        for (int i = 0; i < dice.Count; i++)
            rolls[i] = maximize ? dice.Sides : DiceService.RollDie(dice.Sides, context);
        return rolls;
    }

    /// <summary>Average of a spell's own dice at a caster level (AI estimates).</summary>
    public static float AverageEffect(SpellData spell, int casterLevel) => Effect(spell, casterLevel).Average;

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

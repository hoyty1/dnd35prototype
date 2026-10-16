using System.Text;
using UnityEngine;

/// <summary>
/// One damage roll per area damage effect, shared by every creature and unattended object it reaches (decision D6 of
/// docs/designs/cast_core_batch11.md, a judge panel of 2026-10-15; delegated RAW reading 2026-10-14, applied 2026-10-16 in
/// batch 11 step 20, SPL-180; RULES_COVERAGE 4.1). Each time an area effect is released (one Fireball detonation, one
/// Lightning Bolt, one Cone of Cold, one breath, one acid spray) its damage is rolled once, with the cast's Empower and
/// Maximize (SpellDiceRules.Roll), and every target takes that same total before its own spell resistance, save (half,
/// rounded down), Evasion or Improved Evasion, immunity, resistance and damage reduction. Effects whose own text makes
/// them separate keep separate rolls (each Magic Missile missile; Chain Lightning's secondary bolts take half of the
/// primary's one total).
/// <para>The roll is made the first time the effect reaches a creature that its spell resistance does not stop (or an
/// object), so an effect whose every creature resists rolls no damage and a one-creature area draws its dice exactly
/// where it always did. One instance per damage part: Ice Storm's bludgeoning and cold, and Sunburst's living and undead
/// dice, are each one roll.</para>
/// <para>Basis: PHB p.231 (Fireball: unattended objects "also take this damage"), p.248 (Lightning Bolt), p.208 (Chain
/// Lightning: half "as much" as the primary), MM p.137 (the iron golem heals by a fireball's damage total), PHB p.177
/// (an item takes "whatever damage the attack deals"); SR, the save and Evasion stay per creature (PHB p.177, p.50).</para>
/// </summary>
public sealed class AreaDamageRoll
{
    private SpellDiceRoll _roll;

    /// <summary>True once the shared roll was made.</summary>
    public bool HasRolled { get; private set; }

    /// <summary>The dice the shared roll was made with (default until <see cref="HasRolled"/>).</summary>
    public SpellDice Dice { get; private set; }

    /// <summary>The shared roll (default until <see cref="HasRolled"/>).</summary>
    public SpellDiceRoll Rolled => _roll;

    /// <summary>
    /// The effect's one roll: rolled now with <paramref name="dice"/> and <paramref name="metamagic"/> under
    /// <paramref name="context"/> (SpellDiceRules.Roll) the first time, the same roll every later time. Every caller of
    /// one instance asks for the same dice; a later call with other dice still gets the first roll (logged).
    /// </summary>
    public SpellDiceRoll GetOrRoll(SpellDice dice, MetamagicData metamagic, string context)
    {
        if (!HasRolled)
        {
            _roll = SpellDiceRules.Roll(dice, metamagic, context);
            Dice = dice;
            HasRolled = true;
        }
        else if (dice.Count != Dice.Count || dice.Sides != Dice.Sides || dice.Bonus != Dice.Bonus)
        {
            Debug.LogWarning($"[AreaDamage] One area roll asked for {dice} after it was rolled as {Dice}; the first roll is kept.");
        }
        return _roll;
    }

    /// <summary>As <see cref="GetOrRoll(SpellDice, MetamagicData, string)"/> with the metamagic stamped on <paramref name="spell"/> (SpellDiceRules.RollFor).</summary>
    public SpellDiceRoll RollFor(SpellData spell, SpellDice dice, string context = null)
        => GetOrRoll(dice, SpellDiceRules.MetamagicOf(spell), context);

    /// <summary>
    /// As <see cref="RollFor(SpellData, SpellDice, string)"/>, and the first time (when the shared roll is made) appends
    /// one line with the roll to <paramref name="log"/>, so the log shows the effect's one damage roll (D6).
    /// </summary>
    public SpellDiceRoll RollAndLog(SpellData spell, SpellDice dice, StringBuilder log, string label = null)
    {
        bool first = !HasRolled;
        SpellDiceRoll roll = RollFor(spell, dice);
        if (first)
            log?.AppendLine($"  {Describe(label)}");
        return roll;
    }

    /// <summary>The shared roll as a log line: "Damage roll (one for the area): 5d6 [3,4,1,6,2] = 16", with Maximize and Empower.</summary>
    public string Describe(string label = null)
    {
        if (!HasRolled)
            return "Damage roll (one for the area): not rolled";
        var sb = new StringBuilder();
        sb.Append("Damage roll (one for the area").Append(string.IsNullOrEmpty(label) ? "" : ", " + label).Append("): ").Append(Dice);
        if (_roll.Rolls != null && _roll.Rolls.Length > 0)
            sb.Append(" [").Append(string.Join(",", _roll.Rolls)).Append(']');
        if (_roll.Maximized)
            sb.Append(" maximized");
        if (_roll.Empowered && _roll.EmpowerBonus > 0)
            sb.Append(" + ").Append(_roll.EmpowerBonus).Append(" empowered");
        sb.Append(" = ").Append(_roll.Total);
        return sb.ToString();
    }
}

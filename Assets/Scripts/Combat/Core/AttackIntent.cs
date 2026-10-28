/// <summary>How an attack reaches its target, for the reach and range test of <see cref="AttackLegality"/>.</summary>
public enum AttackMode
{
    Melee,
    Ranged,
    Thrown
}

/// <summary>
/// What an attacker means to do with one attack, as far as the legality gate needs it (batch 12 step 12). This is the
/// minimal first cut: the attack mode and weapon for the reach or range test, and the maneuver for a
/// <see cref="DirectAttackKind.Maneuver"/>. Step 13 (docs/designs/pc_npc_parity_batch12.md) extends it to the full intent
/// (hand, Rapid Shot, flurry, two-weapon, fighting defensively, the held-charge touch, AoO, charge) and adds the ForUi and
/// ForAI builders. Null means "derive melee or ranged from the attacker's main weapon".
/// </summary>
public sealed class AttackIntent
{
    /// <summary>Melee, ranged or thrown; null derives it from the main weapon.</summary>
    public AttackMode? Mode;

    /// <summary>The weapon used (a thrown weapon, an off-hand weapon); null means the main weapon or an unarmed or natural attack.</summary>
    public ItemData Weapon;

    /// <summary>The maneuver of a <see cref="DirectAttackKind.Maneuver"/>.</summary>
    public SpecialAttackType? Maneuver;

    /// <summary>
    /// True when <see cref="Weapon"/> names the hand the attacker chose (a PC's main- or off-hand disarm or sunder): the
    /// weapon used, or null for an unarmed or natural attack. False: the main weapon (AttackLegality.IsInManeuverReach).
    /// </summary>
    public bool WeaponChosen;

    public static AttackIntent ForMode(AttackMode mode, ItemData weapon = null) => new AttackIntent { Mode = mode, Weapon = weapon };

    public static AttackIntent ForManeuver(SpecialAttackType type) => new AttackIntent { Maneuver = type };

    /// <summary>The spell of a <see cref="DirectAttackKind.Spell"/> (its hostility decides whether a ward applies).</summary>
    public SpellData Spell;

    public static AttackIntent ForSpell(SpellData spell) => new AttackIntent { Spell = spell };

    /// <summary>A maneuver made with <paramref name="weapon"/> (null: unarmed or natural), the hand the attacker chose.</summary>
    public static AttackIntent ForManeuverWith(SpecialAttackType type, ItemData weapon)
        => new AttackIntent { Maneuver = type, Weapon = weapon, WeaponChosen = true };
}

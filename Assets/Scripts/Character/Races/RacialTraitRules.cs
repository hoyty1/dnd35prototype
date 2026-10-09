using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The combat racial traits of <see cref="RaceData"/> (PHB p.14-21, MM p.92, p.103, p.132; CHR-019), for every creature
/// with a race, PC or NPC (CRE-038). Actor-agnostic: the PC attack path, the AI and the spell paths call the same
/// methods (docs/systems/PC_NPC_PARITY.md).
/// <list type="bullet">
/// <item>Attack: the racial bonus against a kind of creature (dwarf +1 against orcs and goblinoids, gnome +1 against
/// kobolds and goblinoids) and the halfling's +1 with thrown weapons and slings (<see cref="AttackBonus"/>, added by
/// <c>CharacterController.BuildAttackBonus</c> and by the touch attacks of <c>SpellCaster</c>).</item>
/// <item>AC: the dwarf's and gnome's +4 dodge bonus against the giant type (<see cref="KindDodgeACBonusAgainst"/>, added
/// to the AC an attack is rolled against) and the svirfneblin's +4 dodge against all creatures
/// (<see cref="CharacterStats.RacialDodgeACBonus"/>, in <see cref="CharacterStats.ArmorClass"/>). A dodge bonus is lost
/// whenever the creature loses its Dexterity bonus to AC (PHB p.15, p.136).</item>
/// <item>NPC races: <see cref="AttachNpcRace"/> gives an NPC of a PC race (or an MM subrace) its <see cref="RaceData"/>
/// without changing its MM ability scores, size or speed.</item>
/// </list>
/// Saves are in <see cref="SaveRules"/> (CHR-018), sleep immunity in <c>SpellUtilities.IsImmuneToSleepEffects</c>,
/// stability in <c>CharacterController.GetManeuverStabilityBonus</c>, the illusion DC in <c>SpellSaveDCRules</c>,
/// racial skill bonuses in <see cref="CharacterStats.GetSkillBonus"/>.
/// </summary>
public static class RacialTraitRules
{
    /// <summary>
    /// True when <paramref name="creature"/> is of <paramref name="kind"/>: a creature type (its
    /// <see cref="CharacterStats.CreatureType"/>, e.g. "Giant": ogres, trolls, ettins and giants, MM p.310) or a humanoid
    /// subtype ("Orc", "Goblinoid", "Kobold", "Elf", ...) read from the creature tags, the race and the race it counts as
    /// (a half-orc is an orc, PHB p.19; a half-elf an elf, p.18). Goblinoids include goblins, hobgoblins and bugbears
    /// (PHB p.15). A subtype needs the humanoid type: an orc zombie is undead and has lost its kind subtype (MM p.266).
    /// </summary>
    public static bool IsOfKind(CharacterStats creature, string kind)
    {
        if (creature == null || string.IsNullOrWhiteSpace(kind))
            return false;

        string type = string.IsNullOrWhiteSpace(creature.CreatureType) ? "Humanoid" : creature.CreatureType.Trim();
        if (string.Equals(type, kind, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.Equals(type, "Humanoid", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (string alias in KindAliases(kind))
        {
            if (HasTag(creature, alias))
                return true;
            RaceData race = creature.Race;
            if (race != null && (NameIs(race.RaceName, alias) || NameIs(race.CountsAsRace, alias)))
                return true;
        }
        return false;
    }

    private static IEnumerable<string> KindAliases(string kind)
    {
        yield return kind;
        if (NameIs(kind, "Goblinoid"))
        {
            yield return "Goblin";
            yield return "Hobgoblin";
            yield return "Bugbear";
        }
        else if (NameIs(kind, "Orc"))
        {
            yield return "Half-Orc";
        }
    }

    private static bool HasTag(CharacterStats creature, string tag)
    {
        List<string> tags = creature.CreatureTags;
        if (tags == null)
            return false;
        for (int i = 0; i < tags.Count; i++)
            if (NameIs(tags[i], tag))
                return true;
        return false;
    }

    private static bool NameIs(string a, string b)
        => !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The racial bonus on attack rolls against <paramref name="target"/> (dwarf +1 against orcs and goblinoids, PHB
    /// p.15; gnome +1 against kobolds and goblinoids, p.17). One kind can match per target; the best applies.
    /// </summary>
    public static int AttackBonusAgainst(CharacterStats attacker, CharacterStats target)
    {
        RaceData race = attacker != null ? attacker.Race : null;
        if (race == null || target == null || race.RacialAttackBonuses == null)
            return 0;
        int best = 0;
        foreach (KeyValuePair<string, int> kvp in race.RacialAttackBonuses)
            if (kvp.Value > best && IsOfKind(target, kvp.Key))
                best = kvp.Value;
        return best;
    }

    /// <summary>
    /// The halfling's +1 racial bonus on attack rolls with thrown weapons and slings (PHB p.20): a ranged attack with a
    /// sling, or with a weapon that is thrown (a thrown weapon, or a melee weapon with the Throwing enchantment, DMG
    /// p.226). A dagger used in melee is not thrown and gets nothing: that reading is an owner question in CHR-019.
    /// </summary>
    public static int ThrownOrSlingAttackBonus(CharacterStats attacker, ItemData weapon, bool isRanged)
    {
        RaceData race = attacker != null ? attacker.Race : null;
        if (race == null || race.ThrownAndSlingAttackBonus <= 0 || weapon == null || !isRanged)
            return 0;
        bool sling = weapon.RequiresAmmoType == AmmunitionType.SlingBullet
            || string.Equals(weapon.Id, DND35e.Identifiers.ItemIDs.SLING, StringComparison.OrdinalIgnoreCase);
        return sling || weapon.CanBeThrown ? race.ThrownAndSlingAttackBonus : 0;
    }

    /// <summary>Every racial attack-roll bonus of one attack: <see cref="AttackBonusAgainst"/> plus <see cref="ThrownOrSlingAttackBonus"/>.</summary>
    public static int AttackBonus(CharacterStats attacker, CharacterStats target, ItemData weapon, bool isRanged)
        => AttackBonusAgainst(attacker, target) + ThrownOrSlingAttackBonus(attacker, weapon, isRanged);

    /// <summary>
    /// The racial dodge bonus to AC against attacks by <paramref name="attacker"/>'s kind (dwarf and gnome +4 against the
    /// giant type, PHB p.15, p.17). 0 while <paramref name="defender"/> is denied its Dexterity bonus to AC by a condition
    /// (flat-footed, stunned, helpless...): it loses its dodge bonuses with it. The weapon attack path also removes it
    /// when this attack alone denies Dexterity (an invisible attacker, a feint, a grapple).
    /// </summary>
    public static int KindDodgeACBonusAgainst(CharacterStats defender, CharacterStats attacker)
    {
        RaceData race = defender != null ? defender.Race : null;
        if (race == null || attacker == null || race.RacialACBonuses == null || defender.DeniedDexToAcByCondition)
            return 0;
        int best = 0;
        foreach (KeyValuePair<string, int> kvp in race.RacialACBonuses)
            if (kvp.Value > best && IsOfKind(attacker, kvp.Key))
                best = kvp.Value;
        return best;
    }

    /// <summary>
    /// Every racial dodge bonus <paramref name="defender"/> has against <paramref name="attacker"/>: the kind bonus plus
    /// <see cref="CharacterStats.RacialDodgeACBonus"/> (svirfneblin). Used to take both away when an attack denies the
    /// defender its Dexterity bonus.
    /// </summary>
    public static int DodgeACBonusAgainst(CharacterStats defender, CharacterStats attacker)
        => defender == null ? 0 : KindDodgeACBonusAgainst(defender, attacker) + defender.RacialDodgeACBonus;

    /// <summary>
    /// The race of an NPC built from <paramref name="def"/>: <see cref="NPCDefinition.RaceName"/> while the creature is
    /// still a humanoid. A skeleton or zombie (undead) loses the base creature's racial traits (MM p.226, p.266). Null
    /// when the entry names no race or an unknown one (logged).
    /// </summary>
    public static RaceData ResolveNpcRace(NPCDefinition def)
    {
        if (def == null || string.IsNullOrWhiteSpace(def.RaceName))
            return null;
        string type = string.IsNullOrWhiteSpace(def.CreatureType) ? "Humanoid" : def.CreatureType.Trim();
        if (!string.Equals(type, "Humanoid", StringComparison.OrdinalIgnoreCase))
            return null;
        RaceData race = RaceDatabase.GetRace(def.RaceName.Trim());
        if (race == null)
            Debug.LogWarning($"[RacialTraits] NPC '{def.Id}' names unknown race '{def.RaceName}'.");
        return race;
    }

    /// <summary>
    /// Gives an NPC its race for the racial traits (CRE-038). Only <see cref="CharacterStats.Race"/> is set: the MM entry's
    /// ability scores already include the racial adjustments, and its size and speed come from the entry.
    /// </summary>
    public static void AttachNpcRace(CharacterStats stats, NPCDefinition def)
    {
        if (stats == null)
            return;
        stats.Race = ResolveNpcRace(def);
    }
}

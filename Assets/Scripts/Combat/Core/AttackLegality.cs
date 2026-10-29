using System;
using UnityEngine;

/// <summary>What kind of direct attack <see cref="AttackLegality.CanDirectlyAttack"/> is asked about.</summary>
public enum DirectAttackKind
{
    /// <summary>A weapon attack (melee, ranged or thrown; an unarmed strike in melee).</summary>
    Weapon,

    /// <summary>A natural attack.</summary>
    Natural,

    /// <summary>A melee touch attack (a touch spell's touch is <see cref="Spell"/>).</summary>
    Touch,

    /// <summary>A special attack (trip, disarm, sunder, grapple, bull rush, coup de grace; the intent names it).</summary>
    Maneuver,

    /// <summary>An attack of opportunity.</summary>
    AoO,

    /// <summary>The attack (or bull rush) at the end of a charge.</summary>
    Charge,

    /// <summary>A spell that targets the creature (the cast core's own range test applies; the hold bar does not).</summary>
    Spell
}

/// <summary>The tests of <see cref="AttackLegality.Check"/>; callers that already run one of them elsewhere leave it out.</summary>
[Flags]
public enum AttackLegalityChecks
{
    None = 0,
    /// <summary>Otiluke's Resilient Sphere: nothing passes its boundary (PHB p.258).</summary>
    Sphere = 1,
    /// <summary>The Improved Grab one-body-part hold bar (MM p.310; CMB-163).</summary>
    HoldBar = 2,
    /// <summary>Reach or range for the kind (PHB p.137, p.155-158).</summary>
    Reach = 4,
    /// <summary>Sanctuary and Hide from Undead, read only (WardSaveLedger.Predict).</summary>
    Ward = 8,
    All = Sphere | HoldBar | Reach | Ward
}

/// <summary>
/// "May this creature directly attack that one?" (batch 12 step 12; CMB-092, CMB-093, CMB-104, AI-005), one pure answer for
/// every attacker, PC or NPC: no dice, no log. It checks, in this order:
/// 1. Otiluke's Resilient Sphere (ResilientSphereAreaEffect.DoesSphereBlockInteraction, PHB p.258), for every kind;
/// 2. the Improved Grab hold bar (CharacterController.IsBarredFromOrdinaryAttackOn / IsBarredFromSpecialAttackOn; not for
///    a spell, which is not an attack, MM p.310);
/// 3. reach and range by kind (<see cref="IsInReachOrRange"/>; a maneuver's own reach and its CanTrip / CanBullRush
///    legality, <see cref="IsInManeuverReach"/>; a spell's range is the cast core's);
/// 4. the ward ledger, read only (WardSaveLedger.Predict): a known failed save refuses, an unrolled ward gives ok with
///    <see cref="LegalityRisk.WardSaveUnrolled"/>. A spell meets a ward only when aimed at an opponent, and a maneuver only
///    when it is an attack (<see cref="IsWardedAttempt"/>).
/// The executors call it before any cost and then <see cref="CommitWard"/> when the attack is attempted; the PC highlight
/// lists and the AI's target choice call it (or <see cref="Check"/> without the tests they run themselves) and never roll.
/// </summary>
public static class AttackLegality
{
    public static Legality CanDirectlyAttack(CharacterController attacker, CharacterController target, DirectAttackKind kind, AttackIntent intent = null)
        => Check(attacker, target, kind, intent, AttackLegalityChecks.All);

    /// <summary><see cref="CanDirectlyAttack"/> limited to <paramref name="checks"/>.</summary>
    public static Legality Check(CharacterController attacker, CharacterController target, DirectAttackKind kind, AttackIntent intent, AttackLegalityChecks checks)
    {
        if (attacker == null || target == null || attacker.Stats == null || target.Stats == null)
            return Legality.Refused("no attacker or no target");
        if (attacker == target)
            return Legality.Allowed();

        SpecialAttackType? maneuver = intent != null ? intent.Maneuver : null;

        if ((checks & AttackLegalityChecks.Sphere) != 0 && ResilientSphereAreaEffect.DoesSphereBlockInteraction(attacker, target))
        {
            return Legality.Refused(ResilientSphereAreaEffect.IsCharacterInAnySphere(attacker)
                ? $"{attacker.Stats.CharacterName} is inside a Resilient Sphere: nothing passes its boundary (PHB p.258)"
                : $"{target.Stats.CharacterName} is inside a Resilient Sphere: nothing passes its boundary (PHB p.258)");
        }

        if ((checks & AttackLegalityChecks.HoldBar) != 0 && kind != DirectAttackKind.Spell)
        {
            string holdReason;
            bool barred = kind == DirectAttackKind.Maneuver && maneuver.HasValue
                ? attacker.IsBarredFromSpecialAttackOn(maneuver.Value, target, out holdReason)
                : attacker.IsBarredFromOrdinaryAttackOn(target, out holdReason);
            if (barred)
                return Legality.Refused(holdReason);
        }

        if ((checks & AttackLegalityChecks.Reach) != 0 && !IsInReachOrRange(attacker, target, kind, intent, out string reachReason))
            return Legality.Refused(reachReason);

        LegalityRisk risk = LegalityRisk.None;
        if ((checks & AttackLegalityChecks.Ward) != 0 && IsWardedAttempt(attacker, target, kind, intent))
        {
            Legality ward = WardSaveLedger.Predict(attacker, target);
            if (!ward.Ok)
                return ward;
            risk = ward.Risk;
        }

        return Legality.Allowed(risk);
    }

    /// <summary>
    /// True when the attempt is a direct attack the wards apply to (PHB p.274 "attempting to strike or otherwise directly
    /// attack the warded creature, even with a targeted spell"): every weapon, natural, touch, AoO and charge attack; a
    /// maneuver that is an attack (trip, disarm, sunder, coup de grace, bull rush; CharacterController.IsSpecialAttackAnAttack)
    /// or the grapple's melee touch attack (PHB p.155-156), not a feint, aid another or overrun; a spell only when it is
    /// hostile to the creature, whatever the teams (<see cref="IsHostileSpellAt"/>: a cure on an enemy is no attack, a
    /// confused caster's Hold Person on an ally is one); without the spell in the intent, a spell aimed at an opponent.
    /// </summary>
    public static bool IsWardedAttempt(CharacterController attacker, CharacterController target, DirectAttackKind kind, AttackIntent intent)
    {
        switch (kind)
        {
            case DirectAttackKind.Spell:
                return intent != null && intent.Spell != null
                    ? IsHostileSpellAt(intent.Spell, attacker, target)
                    : TeamUtility.IsEnemy(attacker, target);
            case DirectAttackKind.Maneuver:
                if (intent == null || !intent.Maneuver.HasValue)
                    return true;
                return CharacterController.IsSpecialAttackAnAttack(intent.Maneuver.Value) || intent.Maneuver.Value == SpecialAttackType.Grapple;
            default:
                return true;
        }
    }

    /// <summary>
    /// True when <paramref name="spell"/> aimed at <paramref name="target"/> is an attack on it in the sense of PHB p.274
    /// ("directly attack the warded creature, even with a targeted spell"): a damage, debuff or control spell, a cure
    /// spell on an undead (positive energy harms it, PHB p.215), or a dispel aimed at an opponent (it strips that creature's
    /// magic); not a buff, cure, utility or divination spell on a living creature, whoever casts it (delegated RAW reading
    /// 2026-10-28, batch 12 step 12; the spell's EffectType, as WishExecutor's harmful-effect test).
    /// </summary>
    public static bool IsHostileSpellAt(SpellData spell, CharacterController caster, CharacterController target)
    {
        if (spell == null || target == null)
            return false;
        // Dispel Magic is data-typed Debuff, but on an ally it lifts that creature's afflictions: an attack only on an opponent.
        if (spell.EffectType == SpellEffectType.Dispel || string.Equals(spell.SpellId, DND35e.Identifiers.SpellNames.DISPEL_MAGIC, StringComparison.Ordinal))
            return caster != null && TeamUtility.IsEnemy(caster, target);
        switch (spell.EffectType)
        {
            case SpellEffectType.Damage:
            case SpellEffectType.Debuff:
            case SpellEffectType.Control:
                return true;
            case SpellEffectType.Healing:
                return WardSaveLedger.IsUndead(target);
            default:
                return false;
        }
    }

    /// <summary>
    /// The commit of a direct attack's wards (<see cref="WardSaveLedger.Commit"/>) when the attempt is one they apply to
    /// (<see cref="IsWardedAttempt"/>); <see cref="WardVerdict.NoWard"/> otherwise. A <see cref="WardVerdict.Failed"/>
    /// caller spends the attempted step or action and stops (logged here).
    /// </summary>
    public static WardVerdict CommitWard(CharacterController attacker, CharacterController target, DirectAttackKind kind, AttackIntent intent = null)
    {
        if (attacker == null || target == null || attacker == target || !IsWardedAttempt(attacker, target, kind, intent))
            return WardVerdict.NoWard;
        return WardSaveLedger.Commit(attacker, target);
    }

    /// <summary>
    /// The AI's score adjustment for a target behind a ward it has not saved against yet (AI-005): the commit may lose the
    /// attack, so such a target ranks lower than an unwarded one. 0 for an unwarded target or a passed save.
    /// </summary>
    public static float WardTargetScoreAdjustment(CharacterController attacker, CharacterController target)
        => WardSaveLedger.Predict(attacker, target).Risk == LegalityRisk.WardSaveUnrolled ? UnrolledWardScorePenalty : 0f;

    /// <summary>The flat score penalty of <see cref="WardTargetScoreAdjustment"/>.</summary>
    public const float UnrolledWardScorePenalty = -10f;

    /// <summary>
    /// Reach or range for <paramref name="kind"/>: a weapon attack by its mode (melee reach of the weapon, PHB p.137, p.113;
    /// a projectile's ten increments, a thrown weapon's five, PHB p.158), a natural or melee touch attack within natural
    /// reach, an AoO or a charge's attack within melee reach, a maneuver by <see cref="IsInManeuverReach"/>. A spell's range
    /// is the cast core's test (CheckCastRangeLegality), so it always passes here.
    /// </summary>
    public static bool IsInReachOrRange(CharacterController attacker, CharacterController target, DirectAttackKind kind, AttackIntent intent, out string reason)
    {
        reason = string.Empty;
        int distance = attacker.GetMinimumDistanceToTarget(target, chebyshev: true);
        bool ok;
        switch (kind)
        {
            case DirectAttackKind.Spell:
                return true;

            case DirectAttackKind.Maneuver:
                if (intent != null && intent.Maneuver.HasValue)
                    return IsInManeuverReach(attacker, target, intent.Maneuver.Value, intent, out reason);
                ok = attacker.CanMeleeAttackDistance(distance);
                break;

            case DirectAttackKind.Natural:
            case DirectAttackKind.Touch:
                ok = distance <= UnarmedReachSquares(attacker);
                break;

            case DirectAttackKind.AoO:
            case DirectAttackKind.Charge:
                ok = attacker.CanMeleeAttackDistance(distance, MeleeWeaponOrNull(attacker));
                break;

            default:
                AttackMode? mode = intent != null ? intent.Mode : null;
                if (mode == AttackMode.Thrown)
                    ok = attacker.IsTargetInThrownWeaponRange(target, intent.Weapon);
                else if (mode == AttackMode.Melee)
                    // A swarm attacks only creatures in its own squares (MM p.316): the weapon-range rule the sequence's
                    // stage 3 applies (IsTargetInCurrentWeaponRange), so the AI guard and the executor agree.
                    ok = attacker.Stats.IsSwarm
                        ? attacker.IsTargetInCurrentWeaponRange(target)
                        : attacker.CanMeleeAttackDistance(distance, intent.Weapon);
                else
                    ok = attacker.IsTargetInCurrentWeaponRange(target);
                break;
        }

        if (!ok)
            reason = $"{target.Stats.CharacterName} is out of {attacker.Stats.CharacterName}'s reach or range";
        return ok;
    }

    /// <summary>
    /// One reach rule for maneuvers, PC and NPC alike (CMB-104; PHB p.155-158): a trip and a grapple are unarmed melee touch
    /// attacks, so they reach as far as the creature's natural reach (a longspear's reach does not help, and does not stop
    /// an adjacent trip), and a trip with a trip weapon (ItemData.CanMakeTripAttacks: a guisarme, halberd, spiked chain or
    /// whip, PHB p.115-122) also as far as that weapon reaches (<see cref="IsInTripReach"/>); disarm and sunder are melee
    /// attacks with the weapon used, so that weapon's reach: the hand the attacker chose
    /// (<see cref="AttackIntent.WeaponChosen"/>: the PC's main- or off-hand choice, null for unarmed or natural), else the
    /// main weapon (natural reach without a melee one); a bull rush needs an adjacent defender and the shared bull rush
    /// legality (CanBullRush, PHB p.154); a trip also the shared trip legality (CanTrip); a coup de grace the shared coup
    /// de grace reach; a feint an adjacent foe. An overrun is a move through the defender's square (OverrunSystem) and
    /// passes here.
    /// </summary>
    public static bool IsInManeuverReach(CharacterController attacker, CharacterController target, SpecialAttackType type, out string reason)
        => IsInManeuverReach(attacker, target, type, null, out reason);

    /// <summary><see cref="IsInManeuverReach(CharacterController, CharacterController, SpecialAttackType, out string)"/> for <paramref name="intent"/>'s weapon.</summary>
    public static bool IsInManeuverReach(CharacterController attacker, CharacterController target, SpecialAttackType type, AttackIntent intent, out string reason)
    {
        reason = string.Empty;
        int distance = attacker.GetMinimumDistanceToTarget(target, chebyshev: true);
        bool ok;
        switch (type)
        {
            case SpecialAttackType.Trip:
                if (!attacker.CanTrip(target, out reason))
                    return false;
                ok = IsInTripReach(attacker, distance, intent);
                break;
            case SpecialAttackType.Grapple:
                ok = distance <= UnarmedReachSquares(attacker);
                break;
            case SpecialAttackType.Disarm:
            case SpecialAttackType.Sunder:
                if (intent != null && intent.WeaponChosen)
                    ok = intent.Weapon != null && intent.Weapon.WeaponCat == WeaponCategory.Melee
                        ? attacker.CanMeleeAttackDistance(distance, intent.Weapon)
                        : distance <= UnarmedReachSquares(attacker);
                else
                    ok = attacker.CanMeleeAttackDistance(distance, MeleeWeaponOrNull(attacker));
                break;
            case SpecialAttackType.BullRushAttack:
                return attacker.CanBullRush(target, false, out reason);
            case SpecialAttackType.BullRushCharge:
                return attacker.CanBullRush(target, true, out reason);
            case SpecialAttackType.CoupDeGrace:
                ok = attacker.IsInCoupDeGraceReach(target);
                break;
            case SpecialAttackType.Feint:
                ok = distance <= 1;
                break;
            case SpecialAttackType.Overrun:
                return true;
            default:
                ok = attacker.CanMeleeAttackDistance(distance);
                break;
        }

        if (!ok)
            reason = $"{target.Stats.CharacterName} is out of reach for {attacker.Stats.CharacterName}'s {type} (PHB p.155-158)";
        return ok;
    }

    /// <summary>
    /// The reach of a trip at <paramref name="distance"/> squares: the unarmed touch at natural reach, or a trip weapon's
    /// reach (ItemData.CanMakeTripAttacks, PHB p.115-122): the weapon the intent chose, else a wielded melee trip weapon in
    /// either hand. A reach-only trip weapon (a guisarme or halberd) trips no adjacent foe, but the unarmed trip does.
    /// The PC highlight and the shared gate read it.
    /// </summary>
    public static bool IsInTripReach(CharacterController attacker, int distance, AttackIntent intent = null)
    {
        if (attacker == null)
            return false;
        if (distance <= UnarmedReachSquares(attacker))
            return true;
        if (intent != null && intent.WeaponChosen)
            return IsTripWeaponInReach(attacker, intent.Weapon, distance);
        return IsTripWeaponInReach(attacker, MeleeWeaponOrNull(attacker), distance)
               || IsTripWeaponInReach(attacker, attacker.GetOffHandAttackWeapon(), distance);
    }

    private static bool IsTripWeaponInReach(CharacterController attacker, ItemData weapon, int distance)
        => weapon != null && weapon.WeaponCat == WeaponCategory.Melee && weapon.CanMakeTripAttacks
           && attacker.CanMeleeAttackDistance(distance, weapon);

    /// <summary>The reach of an unarmed or natural attack: the creature's natural reach, at least adjacent (PHB p.149).</summary>
    public static int UnarmedReachSquares(CharacterController c)
        => c != null && c.Stats != null ? Mathf.Max(1, c.Stats.NaturalReachSquares) : 1;

    /// <summary>The main weapon when it is a melee weapon, else null (an unarmed or natural attack at natural reach).</summary>
    private static ItemData MeleeWeaponOrNull(CharacterController c)
    {
        ItemData main = c.GetEquippedMainWeapon();
        return main != null && main.WeaponCat == WeaponCategory.Melee ? main : null;
    }
}

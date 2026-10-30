using System.Collections.Generic;

/// <summary>
/// The AI attack guard (batch 13 step 5; AI-075, AI-073; decision D1, docs/systems/RULES_COVERAGE.md 4.1): "may this
/// AI-run creature declare a weapon attack on that target now?", asked before every AI attack declaration, NPC and
/// AI-run party member alike (owner ruling 2026-10-29: AI creatures never waste an attack on something they cannot hit).
/// It adds no rule: it asks the shared predicates, as the PC's target highlighting does, and is pure (no dice, no log,
/// no state change). In order:
/// 1. the direct-attack legality, AttackLegality.CanDirectlyAttack (the Resilient Sphere, PHB p.258; the Improved Grab
///    hold bar, MM p.310; reach and range by the intent's mode, AttackLegality.IsInReachOrRange: the reach of the melee
///    weapon in hand and a reach weapon's dead zone, PHB p.112, p.137 (a spiked chain also strikes adjacent foes, p.115);
///    five range increments thrown and ten projectile, PHB p.134, p.139; a known failed ward save, read only). A reach
///    or range refusal carries <see cref="LegalityBar.Reach"/> or <see cref="LegalityBar.Range"/>;
/// 2. ammunition for a projectile weapon (PHB p.113; Inventory.HasAmmo, as CombatFlowService.CheckAmmoAvailable reads it,
///    without its log): <see cref="LegalityBar.Ammunition"/>;
/// 3. a crossbow that is loaded, or can be reloaded with a free action or the move action still left this turn
///    (GameManager.CanReloadEquippedWeapon's rules, CharacterController.GetEffectiveReloadAction; PHB p.113-114), so a
///    shot can follow: <see cref="LegalityBar.Ammunition"/>.
/// Not counted (D1): a ward save still to roll (the answer is allowed with LegalityRisk.WardSaveUnrolled: the save may
/// succeed), miss chances and concealment, spell range (it never widens a weapon's range, the AI-075 cause), and line of
/// effect (no weapon attack tests it on either side, GRID-009). The target is read where it truly is (an omniscient
/// read until batch 17 swaps the knowledge provider). When nothing passes, the creature's profile picks the alternative
/// (AIProfile.GetOutOfReachAlternatives, AiOutOfReach.Run; design choice C1).
/// </summary>
public static class AiAttackGuard
{
    /// <summary>The guard for <paramref name="attacker"/>'s attack on <paramref name="target"/> with <paramref name="intent"/> (null: the main weapon's default mode).</summary>
    public static Legality Check(CharacterController attacker, CharacterController target, AttackIntent intent)
    {
        Legality onTarget = CheckTarget(attacker, target, intent);
        if (!onTarget.Ok)
            return onTarget;
        Legality weapon = CheckWeapon(attacker, intent);
        return weapon.Ok ? onTarget : weapon;
    }

    /// <summary>The guard with the AI's own intent for the weapon in hand (AttackIntent.ForAI).</summary>
    public static Legality Check(CharacterController attacker, CharacterController target)
        => Check(attacker, target, IntentFor(attacker, target));

    /// <summary>True when <see cref="Check(CharacterController, CharacterController)"/> allows the attack.</summary>
    public static bool Passes(CharacterController attacker, CharacterController target)
        => Check(attacker, target).Ok;

    /// <summary>The AI's intent for an attack on <paramref name="target"/> with the weapon in hand (AttackIntent.ForAI, pure).</summary>
    public static AttackIntent IntentFor(CharacterController attacker, CharacterController target)
        => AttackIntent.ForAI(GameManager.Instance, attacker, target);

    /// <summary>
    /// <see cref="IntentFor(CharacterController, CharacterController)"/>; with <paramref name="allowThrow"/> false a throw
    /// (AttackIntent.ChooseAiMode's Thrown for a dagger or spear beyond its melee reach) becomes a melee probe, so the guard
    /// answers for the melee reach alone. The melee routines (aggressive, defensive, dragon), a summon's attack, the
    /// concentration turn's "can attack now" and the compulsion controllers ask it so (design choice C1, 2026-10-29: a
    /// melee creature does not throw its only weapon; it closes in, as before step 5); the kiter asks with throws allowed.
    /// </summary>
    public static AttackIntent IntentFor(CharacterController attacker, CharacterController target, bool allowThrow)
    {
        AttackIntent intent = IntentFor(attacker, target);
        if (!allowThrow && intent != null && intent.Mode == AttackMode.Thrown)
        {
            intent.Mode = AttackMode.Melee;
            intent.RapidShot = attacker != null && attacker.RapidShotEnabled;
        }
        return intent;
    }

    /// <summary>The guard with the AI's intent, a throw allowed or not (<see cref="IntentFor(CharacterController, CharacterController, bool)"/>).</summary>
    public static Legality Check(CharacterController attacker, CharacterController target, bool allowThrow)
        => Check(attacker, target, IntentFor(attacker, target, allowThrow));

    /// <summary>True when <see cref="Check(CharacterController, CharacterController, bool)"/> allows the attack.</summary>
    public static bool Passes(CharacterController attacker, CharacterController target, bool allowThrow)
        => Check(attacker, target, allowThrow).Ok;

    /// <summary>
    /// Part 1 of the guard alone: the direct-attack legality on <paramref name="target"/>, reach and range included,
    /// with a reach refusal's bar (<see cref="LegalityBar.Reach"/> for a melee attack, <see cref="LegalityBar.Range"/>
    /// for a ranged or thrown one). A dead or missing target is refused with <see cref="LegalityBar.Target"/>.
    /// </summary>
    public static Legality CheckTarget(CharacterController attacker, CharacterController target, AttackIntent intent)
    {
        if (attacker == null || attacker.Stats == null || target == null || target.Stats == null)
            return Legality.Refused(LegalityBar.Target, "no attacker or no target");
        if (target.Stats.IsDead)
            return Legality.Refused(LegalityBar.Target, "the target is gone");

        Legality legality = AttackLegality.CanDirectlyAttack(attacker, target, DirectAttackKind.Weapon, intent);
        if (legality.Ok)
            return legality;
        // Which bar refused: reach or range when the reach test alone fails, else the legality's own (sphere, hold, ward).
        Legality withoutReach = AttackLegality.Check(attacker, target, DirectAttackKind.Weapon, intent,
            AttackLegalityChecks.All & ~AttackLegalityChecks.Reach);
        if (!withoutReach.Ok)
        {
            // The charm link keeps its own bar (PHB p.209, D14).
            if (withoutReach.Bar == LegalityBar.Charm)
                return withoutReach;
            return Legality.Refused(IsWardOrBarrier(attacker, target) ? LegalityBar.Ward : LegalityBar.Other, withoutReach.Reason);
        }
        bool melee = IsMeleeIntent(attacker, intent);
        return Legality.Refused(melee ? LegalityBar.Reach : LegalityBar.Range, legality.Reason);
    }

    /// <summary>
    /// Parts 2 and 3 of the guard alone: the weapon of <paramref name="intent"/> (else the main weapon) can shoot now: a
    /// projectile weapon has ammunition, and a crossbow is loaded or can be reloaded with a free action or the move action
    /// left (so the shot can follow). Allowed for every melee, thrown or natural attack.
    /// </summary>
    public static Legality CheckWeapon(CharacterController attacker, AttackIntent intent)
        => CheckWeapon(attacker, intent, anyReload: false);

    /// <summary>
    /// <see cref="CheckWeapon(CharacterController, AttackIntent)"/>; with <paramref name="anyReload"/> an unloaded
    /// crossbow also passes when it can be reloaded at all this turn (a full-round reload too): the NPC attack
    /// prelude's own reload branch (GameManager.NPCPerformAttack, the catalog's reload entry) reloads, and the standard action
    /// left, if any, makes the attack (AI-057, D4).
    /// </summary>
    public static Legality CheckWeapon(CharacterController attacker, AttackIntent intent, bool anyReload)
    {
        if (attacker == null || attacker.Stats == null)
            return Legality.Refused(LegalityBar.Target, "no attacker");
        ItemData weapon = intent != null && intent.Weapon != null ? intent.Weapon : attacker.GetEquippedMainWeapon();
        if (weapon == null || !weapon.IsProjectileWeapon || (intent != null && intent.Mode == AttackMode.Melee))
            return Legality.Allowed();

        string name = attacker.Stats.CharacterName;
        global::Inventory inventory = attacker.InventoryComp != null ? attacker.InventoryComp.CharacterInventory : null;
        if (inventory != null && !inventory.HasAmmo(weapon.RequiresAmmoType))
            return Legality.Refused(LegalityBar.Ammunition, $"{name} has no {weapon.RequiresAmmoType} ammunition for its {weapon.Name} (PHB p.113)");

        if (weapon.RequiresReload && !weapon.IsLoaded && !CanReloadInTime(attacker, weapon, anyReload))
            return Legality.Refused(LegalityBar.Ammunition, $"{name}'s {weapon.Name} is not loaded and cannot be reloaded in time to shoot this turn (PHB p.113-114)");

        return Legality.Allowed();
    }

    /// <summary>True when the guard allows <paramref name="attacker"/>'s attack on some living enemy among <paramref name="candidates"/>.</summary>
    public static bool AnyPasses(CharacterController attacker, IList<CharacterController> candidates, bool allowThrow = true)
    {
        if (attacker == null || candidates == null)
            return false;
        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController c = candidates[i];
            if (c == null || c == attacker || c.Stats == null || c.Stats.IsDead || !TeamUtility.IsEnemy(attacker, c))
                continue;
            if (Passes(attacker, c, allowThrow))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when <paramref name="attacker"/> has a ranged attack the guard allows on some enemy among
    /// <paramref name="candidates"/>: a ranged weapon in hand, or a throwable melee weapon thrown (PHB p.113), with its
    /// ammunition and load (AI-039: a kiter retreats only when it can still fight from range).
    /// </summary>
    public static bool HasRangedAttackOnAnyEnemy(CharacterController attacker, IList<CharacterController> candidates)
    {
        if (attacker == null || attacker.Stats == null || candidates == null)
            return false;
        ItemData weapon = attacker.GetEquippedMainWeapon();
        AttackMode mode;
        if (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged)
            mode = AttackMode.Ranged;
        else if (GameManager.IsThrowableWeapon(weapon, meleeOnly: true))
            mode = AttackMode.Thrown;
        else
            return false;

        AttackIntent intent = AttackIntent.ForMode(mode, weapon);
        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterController c = candidates[i];
            if (c == null || c == attacker || c.Stats == null || c.Stats.IsDead || !TeamUtility.IsEnemy(attacker, c))
                continue;
            if (Check(attacker, c, intent).Ok)
                return true;
        }
        return false;
    }

    private static bool CanReloadInTime(CharacterController attacker, ItemData weapon, bool anyReload)
    {
        if (attacker.HasCondition(CombatConditionType.Pinned) || attacker.Actions == null)
            return false;
        switch (attacker.GetEffectiveReloadAction(weapon))
        {
            case ReloadActionType.FreeAction:
                return true;
            case ReloadActionType.MoveAction:
                // The move action reloads and the standard action shoots; converting the standard action leaves no shot.
                return attacker.Actions.HasMoveAction || (anyReload && attacker.Actions.CanConvertStandardToMove);
            case ReloadActionType.FullRound:
                return anyReload && attacker.Actions.HasFullRoundAction;
            default:
                return false;
        }
    }

    private static bool IsMeleeIntent(CharacterController attacker, AttackIntent intent)
    {
        if (intent != null && intent.Mode.HasValue)
            return intent.Mode.Value == AttackMode.Melee;
        ItemData main = attacker.GetEquippedMainWeapon();
        return main == null || main.WeaponCat != WeaponCategory.Ranged;
    }

    /// <summary>A Resilient Sphere between the two, or a known failed ward save (the <see cref="LegalityBar.Ward"/> bars).</summary>
    private static bool IsWardOrBarrier(CharacterController attacker, CharacterController target)
        => ResilientSphereAreaEffect.DoesSphereBlockInteraction(attacker, target) || !WardSaveLedger.Predict(attacker, target).Ok;
}

/// <summary>How an attack reaches its target, for the reach and range test of <see cref="AttackLegality"/>.</summary>
public enum AttackMode
{
    Melee,
    Ranged,
    Thrown
}

/// <summary>
/// What an attacker means to do with one attack (batch 12 steps 12 and 13, docs/designs/pc_npc_parity_batch12.md): the
/// mode (melee, ranged, thrown), the hand, the weapon, the round's declarations (Rapid Shot, flurry, two-weapon fighting,
/// fighting defensively), the held-charge touch, an AoO, and the charge. The legality gate (<see cref="AttackLegality"/>)
/// reads the mode, the weapon and the maneuver; the range math (CombatFlowService.CalculateRangeInfo) and the thrown-mode
/// test (GameManager.IsUsingThrownAttackMode) read the mode, and the post-attack hook (GameManager.OnAttackResolved) the
/// weapon and, for the thrown drop, <see cref="IsThrowOf"/>.
/// Builders: <see cref="ForUi"/> is the attack paths' one reader of the PC UI's attack type (GameManager._currentAttackType,
/// which the UI keeps writing); <see cref="ForAI"/> chooses the NPC's mode from its own weapon and the target's distance
/// (batch 12 step 17, CMB-096: it no longer copies the PC selection). ForUi also records _pendingAttackMode, but the UI's
/// own dispatch (CombatFlowService.OnAttackTargetSelected, the GameManager.CombatActions button handlers) still reads that
/// global directly. A null intent passed to the gate means "derive melee or ranged from the attacker's main weapon"; the
/// mode tests (GameManager.IsUsingThrownAttackMode, IsAttackModeRanged, CombatFlowService.CalculateRangeInfo) read only the
/// intent they are given (a null one throws nothing).
/// </summary>
public sealed class AttackIntent
{
    /// <summary>
    /// Melee, ranged or thrown; null derives it from the main weapon (the legality gate, AttackLegality.IsInReachOrRange).
    /// Both builders set it: <see cref="ForUi"/> from the UI selection (a ranged weapon on the Attack button's "melee"
    /// selection is Ranged), <see cref="ForAI"/> from the NPC's weapon and the target's distance (<see cref="ChooseAiMode"/>).
    /// </summary>
    public AttackMode? Mode;

    /// <summary>
    /// The PC UI's attack type as the UI selected it (ForUi only; null otherwise), for the code that still takes
    /// GameManager.AttackType (<see cref="UiAttackType"/>): the Attack button selects "Melee" for a bow too.
    /// </summary>
    public GameManager.AttackType? UiType;

    /// <summary>The weapon used (a thrown weapon, an off-hand weapon); null means the main weapon or an unarmed or natural attack.</summary>
    public ItemData Weapon;

    /// <summary>The maneuver of a <see cref="DirectAttackKind.Maneuver"/>.</summary>
    public SpecialAttackType? Maneuver;

    /// <summary>
    /// True when <see cref="Weapon"/> names the hand the attacker chose (a PC's main- or off-hand disarm or sunder): the
    /// weapon used, or null for an unarmed or natural attack. False: the main weapon (AttackLegality.IsInManeuverReach).
    /// </summary>
    public bool WeaponChosen;

    /// <summary>The spell of a <see cref="DirectAttackKind.Spell"/> (its hostility decides whether a ward applies).</summary>
    public SpellData Spell;

    /// <summary>The attack step's hand: a main-hand (iterative) step, a natural-sequence step or an off-hand step.</summary>
    public AttackStepKind Hand = AttackStepKind.MainHand;

    /// <summary>Rapid Shot wanted for the round: the PC toggle (CharacterController.RapidShotEnabled), or the AI's choice (ForAI); the executor declares it before the first shot (D9).</summary>
    public bool RapidShot;

    /// <summary>A flurry of blows declared (CharacterController.IsUsingFlurryOfBlows).</summary>
    public bool Flurry;

    /// <summary>
    /// Two-weapon fighting (PHB p.160): the PC UI's dual-wield state (ForUi), the Dual Wield full attack, or the AI's choice
    /// (ForAI). A request whose steps interleave off-hand steps (AttackSequenceRequest.InterleaveOffHand) reads it.
    /// </summary>
    public bool TwoWeapon;

    /// <summary>Fighting defensively (CharacterController.IsFightingDefensively).</summary>
    public bool FightDefensively;

    /// <summary>
    /// The touch that delivers a held charge after its casting round (batch 12 step 18, SPL-156): a melee touch attack
    /// made in place of a regular attack, one step of the attack sequence (AttackSequenceRequest.ForHeldChargeTouch).
    /// </summary>
    public bool HeldChargeTouch;

    /// <summary>An attack of opportunity.</summary>
    public bool IsAoO;

    /// <summary>The attack (or full natural attack) at the end of a charge.</summary>
    public bool IsCharge;

    /// <summary>A charge's pounce: the full attack at the end of the charge (MM p.313).</summary>
    public bool IsPounce;

    /// <summary>
    /// The UI's pending attack mode when the intent was read (ForUi only; null otherwise). Recorded for the later batch 12
    /// steps; nothing reads it yet (the UI dispatch still reads GameManager._pendingAttackMode).
    /// </summary>
    public GameManager.PendingAttackMode? UiPendingMode;

    /// <summary>The PC UI's attack type (<see cref="UiType"/>), else the mode as one (Melee when unset), for the code that still takes GameManager.AttackType.</summary>
    public GameManager.AttackType UiAttackType => UiType ?? ToAttackType(Mode);

    /// <summary>True when the mode is Thrown (the declared throw; the thrown-mode test also needs a throwable melee weapon).</summary>
    public bool ThrownSelected => Mode == AttackMode.Thrown;

    /// <summary>A copy for one step of an attack sequence (GameManager.RunAttackSequence), which sets its own weapon and hand.</summary>
    public AttackIntent CloneForStep() => (AttackIntent)MemberwiseClone();

    /// <summary>
    /// True when this attack throws <paramref name="weapon"/> (batch 12 steps 14 and 17, CMB-174, CMB-096): a weapon with
    /// IsThrown and a range increment (GameManager.IsThrowableWeapon) used with the mode Thrown (a PC's or an NPC's declared
    /// throw), or a ranged thrown weapon (a javelin, a dart) in any mode but Melee (it has no other use).
    /// </summary>
    public bool IsThrowOf(ItemData weapon)
    {
        if (!GameManager.IsThrowableWeapon(weapon))
            return false;
        if (Mode == AttackMode.Thrown)
            return true;
        return weapon.WeaponCat == WeaponCategory.Ranged && Mode != AttackMode.Melee;
    }

    public static AttackIntent ForMode(AttackMode mode, ItemData weapon = null) => new AttackIntent { Mode = mode, Weapon = weapon };

    public static AttackIntent ForManeuver(SpecialAttackType type) => new AttackIntent { Maneuver = type };

    public static AttackIntent ForSpell(SpellData spell) => new AttackIntent { Spell = spell };

    /// <summary>A maneuver made with <paramref name="weapon"/> (null: unarmed or natural), the hand the attacker chose.</summary>
    public static AttackIntent ForManeuverWith(SpecialAttackType type, ItemData weapon)
        => new AttackIntent { Maneuver = type, Weapon = weapon, WeaponChosen = true };

    /// <summary>
    /// The PC UI's intent for <paramref name="attacker"/>'s next attack: the one read of the UI's attack selection
    /// (GameManager._currentAttackType through Combat_GetCurrentAttackType, and _pendingAttackMode). The weapon is the
    /// thrown weapon in hand for a throw (GameManager._equippedWeapon, else the main weapon), else the main weapon, as the
    /// PC attack flow picks it. Read it once per attack step and pass it on.
    /// </summary>
    public static AttackIntent ForUi(GameManager gm, CharacterController attacker)
    {
        GameManager.AttackType type = gm != null ? gm.Combat_GetCurrentAttackType() : GameManager.AttackType.Melee;
        ItemData main = attacker != null ? attacker.GetEquippedMainWeapon() : null;
        ItemData weapon = type == GameManager.AttackType.Thrown && gm != null
            ? (gm.Combat_GetEquippedWeapon() ?? main)
            : main;
        // The Attack button selects "Melee" for every weapon: with a ranged weapon in hand the attack is a ranged one (and
        // a javelin thrown from it is dropped by the post-attack hook, IsThrowOf).
        AttackMode mode = type == GameManager.AttackType.Melee && weapon != null && weapon.WeaponCat == WeaponCategory.Ranged
            ? AttackMode.Ranged
            : ToMode(type);
        var intent = new AttackIntent
        {
            Mode = mode,
            UiType = type,
            Weapon = weapon,
            UiPendingMode = gm != null ? gm.Combat_GetPendingAttackMode() : (GameManager.PendingAttackMode?)null,
            TwoWeapon = gm != null && gm.Combat_IsDualWielding(),
        };
        CopyDeclarations(intent, attacker);
        return intent;
    }

    /// <summary>
    /// An NPC's intent for an attack on <paramref name="target"/> with its main weapon (batch 12 step 17, CMB-096): the mode
    /// is the NPC's own (<see cref="ChooseAiMode"/>), never the PC UI's selection; <paramref name="gm"/> is not read (kept
    /// for the builders' common shape). Its attack modes (batch 12 step 18, AI-055; owner decision Q2, 2026-10-23) are the
    /// AI's choice by the attack entries' AiModeValuation (batch 13 step 6, C2: the stopgap's expected-hits and
    /// expected-damage formula, unchanged; an interim rule, replaced by batch 22's evaluator): Rapid Shot for a ranged
    /// attack (or the creature's standing toggle), two-weapon fighting for a melee one. The rules of both are the PC's (the executor declares Rapid Shot before the
    /// first shot, D9; the off-hand steps and penalties, PHB p.160).
    /// </summary>
    public static AttackIntent ForAI(GameManager gm, CharacterController actor, CharacterController target)
    {
        ItemData weapon = actor != null ? actor.GetEquippedMainWeapon() : null;
        var intent = new AttackIntent
        {
            Mode = ChooseAiMode(actor, weapon, target),
            Weapon = weapon,
        };
        CopyDeclarations(intent, actor);
        // The mode chooser is the attack entries' AiModeValuation (batch 13 step 6, design choice C2): the stopgap formula,
        // unchanged, an interim rule replaced by batch 22's evaluator.
        AiModeValuation valuation = actor != null && actor.Stats != null && target != null ? AiModeValuation.ForAttack : null;
        if (valuation != null)
        {
            // Rapid Shot works with any ranged attack, a thrown weapon's too (PHB p.99, p.113).
            if ((intent.Mode == AttackMode.Ranged || intent.Mode == AttackMode.Thrown) && !intent.RapidShot)
                intent.RapidShot = valuation.RapidShot != null && valuation.RapidShot(actor, target, weapon);
            if (intent.Mode == AttackMode.Melee)
                intent.TwoWeapon = valuation.TwoWeapon != null && valuation.TwoWeapon(actor, target);
        }
        return intent;
    }

    /// <summary>
    /// The NPC's attack mode with <paramref name="weapon"/> against <paramref name="target"/> (PHB p.113: throwing is the
    /// wielder's choice for each attack): Ranged with a ranged weapon (a bow, a crossbow, a javelin); Thrown with a melee
    /// weapon that can be thrown (a dagger, a spear, a handaxe) when the target is beyond its melee reach and within its
    /// thrown range; Melee otherwise. AI stopgap (batch 13 catalog): it throws only at a target it cannot reach, and never
    /// weighs a throw against closing in.
    /// </summary>
    public static AttackMode ChooseAiMode(CharacterController actor, ItemData weapon, CharacterController target)
    {
        if (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged)
            return AttackMode.Ranged;
        if (actor != null && target != null && GameManager.IsThrowableWeapon(weapon, meleeOnly: true)
            && !actor.IsTargetInCurrentWeaponRange(target) && actor.IsTargetInThrownWeaponRange(target, weapon))
            return AttackMode.Thrown;
        return AttackMode.Melee;
    }

    private static void CopyDeclarations(AttackIntent intent, CharacterController attacker)
    {
        if (attacker == null)
            return;
        intent.RapidShot = attacker.RapidShotEnabled;
        intent.Flurry = attacker.IsUsingFlurryOfBlows;
        intent.FightDefensively = attacker.IsFightingDefensively;
    }

    public static AttackMode ToMode(GameManager.AttackType type)
    {
        switch (type)
        {
            case GameManager.AttackType.Thrown: return AttackMode.Thrown;
            case GameManager.AttackType.Ranged: return AttackMode.Ranged;
            default: return AttackMode.Melee;
        }
    }

    public static GameManager.AttackType ToAttackType(AttackMode? mode)
    {
        switch (mode)
        {
            case AttackMode.Thrown: return GameManager.AttackType.Thrown;
            case AttackMode.Ranged: return GameManager.AttackType.Ranged;
            default: return GameManager.AttackType.Melee;
        }
    }
}

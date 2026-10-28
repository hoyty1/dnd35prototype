/// <summary>How an attack reaches its target, for the reach and range test of <see cref="AttackLegality"/>.</summary>
public enum AttackMode
{
    Melee,
    Ranged,
    Thrown
}

/// <summary>
/// Named differences in what an attack intent reads (batch 12 step 13, the <see cref="PostAttackQuirk"/> pattern): each
/// bit is a legacy read that a later step removes.
/// </summary>
[System.Flags]
public enum AttackIntentQuirk
{
    None = 0,

    /// <summary>
    /// The NPC intent copies the PC UI's attack-type selection (GameManager._currentAttackType) into
    /// <see cref="AttackIntent.LegacyUiMode"/>, as the NPC range math read it through CalculateRangeInfo before the intent
    /// existed: an NPC with a throwable melee weapon gets thrown range math while the PC's last selection was Thrown
    /// (CMB-096). Only <see cref="AttackIntent.ThrownSelected"/> reads that copy; <see cref="AttackIntent.Mode"/> stays null,
    /// so the legality gate derives the mode from the NPC's weapon. Set by <see cref="AttackIntent.ForAI"/>.
    /// </summary>
    NpcIntentReadsUiThrownGlobal = 1 << 0,
}

/// <summary>
/// What an attacker means to do with one attack (batch 12 steps 12 and 13, docs/designs/pc_npc_parity_batch12.md): the
/// mode (melee, ranged, thrown), the hand, the weapon, the round's declarations (Rapid Shot, flurry, two-weapon fighting,
/// fighting defensively), the held-charge touch, an AoO, and the charge. The legality gate (<see cref="AttackLegality"/>)
/// reads the mode, the weapon and the maneuver; the range math (CombatFlowService.CalculateRangeInfo), the thrown-mode
/// test (GameManager.IsUsingThrownAttackMode) and the thrown drop (GameManager.ResolveThrownWeaponAfterAttack) read the
/// mode, and the post-attack hook (GameManager.OnAttackResolved) the weapon.
/// Builders: <see cref="ForUi"/> is the attack paths' one reader of the PC UI's attack type (GameManager._currentAttackType,
/// which the UI keeps writing; <see cref="ForAI"/> still copies it under <see cref="AttackIntentQuirk.NpcIntentReadsUiThrownGlobal"/>).
/// ForUi also records _pendingAttackMode, but the UI's own dispatch (CombatFlowService.OnAttackTargetSelected, the
/// GameManager.CombatActions button handlers) still reads that global directly. A null intent passed to the gate means
/// "derive melee or ranged from the attacker's main weapon".
/// </summary>
public sealed class AttackIntent
{
    /// <summary>
    /// Melee, ranged or thrown; null derives it from the main weapon (the legality gate, AttackLegality.IsInReachOrRange).
    /// <see cref="ForAI"/> leaves it null: its copy of the PC selection is <see cref="LegacyUiMode"/>.
    /// </summary>
    public AttackMode? Mode;

    /// <summary>
    /// The PC UI's attack-type selection copied into an NPC intent (<see cref="AttackIntentQuirk.NpcIntentReadsUiThrownGlobal"/>,
    /// CMB-096); null otherwise. Read only by <see cref="ThrownSelected"/> while <see cref="Mode"/> is null.
    /// </summary>
    public AttackMode? LegacyUiMode;

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

    /// <summary>Rapid Shot declared for the round (CharacterController.RapidShotEnabled).</summary>
    public bool RapidShot;

    /// <summary>A flurry of blows declared (CharacterController.IsUsingFlurryOfBlows).</summary>
    public bool Flurry;

    /// <summary>Two-weapon fighting (the PC UI's dual-wield state).</summary>
    public bool TwoWeapon;

    /// <summary>Fighting defensively (CharacterController.IsFightingDefensively).</summary>
    public bool FightDefensively;

    /// <summary>The touch that delivers a held charge.</summary>
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

    /// <summary>The legacy reads this intent carries (<see cref="AttackIntentQuirk"/>).</summary>
    public AttackIntentQuirk Quirks;

    /// <summary>The mode as the PC UI's attack type (Melee when unset), for the code that still takes GameManager.AttackType.</summary>
    public GameManager.AttackType UiAttackType => ToAttackType(Mode);

    /// <summary>
    /// True when the mode is Thrown (the declared throw; the thrown-mode test also needs a throwable melee weapon). With no
    /// mode, an NPC intent's <see cref="LegacyUiMode"/> under its quirk bit (CMB-096).
    /// </summary>
    public bool ThrownSelected => Mode == AttackMode.Thrown
        || (Mode == null && (Quirks & AttackIntentQuirk.NpcIntentReadsUiThrownGlobal) != 0 && LegacyUiMode == AttackMode.Thrown);

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
        var intent = new AttackIntent
        {
            Mode = ToMode(type),
            Weapon = weapon,
            UiPendingMode = gm != null ? gm.Combat_GetPendingAttackMode() : (GameManager.PendingAttackMode?)null,
            TwoWeapon = gm != null && gm.Combat_IsDualWielding(),
        };
        CopyDeclarations(intent, attacker);
        return intent;
    }

    /// <summary>
    /// An NPC's intent for an attack on <paramref name="target"/> with its main weapon. It copies what the NPC paths read
    /// before the intent existed: the PC UI's attack-type selection goes into <see cref="LegacyUiMode"/>
    /// (<see cref="AttackIntentQuirk.NpcIntentReadsUiThrownGlobal"/>, CMB-096), which only the thrown-mode test and the range
    /// math read through <see cref="ThrownSelected"/>. <see cref="Mode"/> stays null, so the intent is safe to hand to the
    /// legality gate (it derives melee or ranged from the weapon).
    /// </summary>
    public static AttackIntent ForAI(GameManager gm, CharacterController actor, CharacterController target)
    {
        GameManager.AttackType uiType = gm != null ? gm.Combat_GetCurrentAttackType() : GameManager.AttackType.Melee;
        var intent = new AttackIntent
        {
            LegacyUiMode = ToMode(uiType),
            Weapon = actor != null ? actor.GetEquippedMainWeapon() : null,
            Quirks = AttackIntentQuirk.NpcIntentReadsUiThrownGlobal,
        };
        CopyDeclarations(intent, actor);
        return intent;
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

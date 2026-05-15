/// <summary>
/// Unified combat condition catalog.
/// Includes all D&D 3.5e SRD conditions plus existing project-specific tactical states.
/// </summary>
public enum CombatConditionType
{
    None = 0,

    // D&D 3.5e SRD conditions
    Blinded,
    BlownAway,
    Checked,
    Confused,
    Cowering,
    Dazed,
    Dazzled,
    Dead,
    Deafened,
    Disabled,
    Dying,
    EnergyDrained,
    Entangled,
    Exhausted,
    Fascinated,
    Charmed,
    Asleep,
    Fatigued,
    FlatFooted,
    Frightened,
    Grappling,
    Helpless,
    HideousLaughter,
    Incorporeal,
    Invisible,
    KnockedDown,
    Nauseated,
    Panicked,
    Paralyzed,
    Petrified,
    Pinned,
    Prone,
    Shaken,
    Sickened,
    Stable,
    Staggered,
    Stunned,
    Turned,
    Unconscious,

    Blinking,

    // Bestow Curse conditions (permanent until Remove Curse)
    BestowCurseGeneralPenalty,
    BestowCurseActionLoss,

    // Existing project tactical / status states
    Poisoned,
    Bleeding,
    Grappled,
    Disarmed,
    Feinted,
    ChargePenalty,
    Flanked,
    LostShieldAC
}

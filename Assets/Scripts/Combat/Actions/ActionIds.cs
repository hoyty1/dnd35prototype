/// <summary>
/// The ids of the action catalog's entries (batch 13 step 3), one constant per combat action in scope. ActionCatalogTests
/// checks that each constant has exactly one entry.
/// </summary>
public static class ActionIds
{
    // Attacks (PHB p.139-141, p.143, p.154)
    public const string Attack = "attack";
    public const string FullAttack = "full-attack";
    public const string FlurryOfBlows = "flurry-of-blows";
    public const string Charge = "charge";
    public const string DeliverHeldCharge = "deliver-held-charge";
    public const string AttackWall = "attack-wall";

    // Maneuvers (PHB p.153-160); one per SpecialAttackType
    public const string Trip = "trip";
    public const string Disarm = "disarm";
    public const string Grapple = "grapple";
    public const string Sunder = "sunder";
    public const string BullRush = "bull-rush";
    public const string BullRushCharge = "bull-rush-charge";
    public const string Overrun = "overrun";
    public const string Feint = "feint";
    public const string CoupDeGrace = "coup-de-grace";
    public const string AidAnother = "aid-another";
    public const string WakeAlly = "wake-ally";
    public const string TurnUndead = "turn-undead";

    // Defense
    public const string TotalDefense = "total-defense";

    // Magic and items
    public const string CastSpell = "cast-spell";
    public const string MaintainConcentration = "maintain-concentration";
    public const string UseItem = "use-item";
    public const string RetrieveItem = "retrieve-item";

    // Movement and posture (PHB p.143-144, p.311)
    public const string Move = "move";
    public const string Withdraw = "withdraw";
    public const string FiveFootStep = "five-foot-step";
    public const string Run = "run";
    public const string Crawl = "crawl";
    public const string StandUp = "stand-up";
    public const string DropProne = "drop-prone";

    // Class abilities
    public const string Smite = "smite";
    public const string LayOnHands = "lay-on-hands";
    public const string WildShape = "wild-shape";
    public const string BardicMusic = "bardic-music";
    public const string Rage = "rage";
    public const string DomainPower = "domain-power";

    // Equipment (PHB p.141-142, p.115, p.123)
    public const string Reload = "reload";
    public const string DrawWeapon = "draw-weapon";
    public const string SheatheWeapon = "sheathe-weapon";
    public const string ReadyShield = "ready-shield";
    public const string DonArmor = "don-armor";
    public const string PickUpItem = "pick-up-item";
    public const string DropItem = "drop-item";

    // Monster abilities (MM p.306-315); per creature
    public const string BreathWeapon = "breath-weapon";
    public const string Spittle = "spittle";
    public const string Engulf = "engulf";
    public const string RangedSpecial = "ranged-special";
    public const string Gaze = "gaze";

    // Turn order (PHB p.160)
    public const string Ready = "ready";
    public const string Delay = "delay";
}

/// <summary>Grease's three forms (PHB p.237): an area, a held object or worn armor.</summary>
public enum GreaseCastMode
{
    None,
    Area,
    Object,
    Armor
}

/// <summary>Wall of Fire's shape (PHB p.298): a straight wall or a ring.</summary>
public enum WallOfFireMode
{
    Line,   // Straight wall: pick start and end points
    Ring    // Circular ring: pick center and radius
}

/// <summary>Wall of Ice's shape (PHB p.299): a plane or a hemisphere.</summary>
public enum WallOfIceMode
{
    Line,   // Straight wall: pick start and end points
    Circle  // Hemisphere: pick center and radius
}

/// <summary>
/// The caster's own choices for one cast (batch 11, cast_core_batch11.md). The PC prompts fill them for a Ui cast; the
/// AI request build fills them through <see cref="AiCastDecider"/> for an NPC cast. They live on the cast's
/// <see cref="SpellCastRequest"/>, so a choice made for a cancelled cast cannot reach a later one (SPL-112, SPL-113).
/// GameManager still reads them through its old <c>_pending*</c> names, which are forwarding properties onto the cast
/// on top of its cast stack.
/// </summary>
public sealed class CastChoices
{
    /// <summary>The energy type of Resist Energy (PHB p.272) or Protection from Energy (PHB p.266).</summary>
    public ResistEnergyType? Energy;

    /// <summary>Fire Shield's form (PHB p.230): true warm, false chill, null not chosen.</summary>
    public bool? FireShieldWarm;

    /// <summary>The race Disguise Self shows (PHB p.222); null keeps the caster's own.</summary>
    public string DisguiseRace;

    /// <summary>The weapon Magic Weapon enchants (PHB p.251).</summary>
    public ItemData MagicWeaponItem;

    /// <summary>The weapon or ammunition Keen Edge affects (PHB p.246).</summary>
    public ItemData KeenEdgeItem;

    /// <summary>True when the Keen Edge pick is ammunition.</summary>
    public bool KeenEdgeIsAmmo;

    /// <summary>The weapon Greater Magic Weapon enchants (PHB p.241).</summary>
    public ItemData GreaterMagicWeaponItem;

    /// <summary>The rope Animate Rope animates (PHB p.199).</summary>
    public ItemData AnimateRopeItem;

    /// <summary>Grease's form (PHB p.237).</summary>
    public GreaseCastMode GreaseMode = GreaseCastMode.None;

    /// <summary>Wall of Fire's shape; null not chosen.</summary>
    public WallOfFireMode? WallOfFireMode;

    /// <summary>Wall of Ice's shape; null not chosen.</summary>
    public WallOfIceMode? WallOfIceMode;

    /// <summary>The creature a Summon Monster cast places (PHB p.285-287).</summary>
    public SummonMonsterOption SummonSelection;

    /// <summary>The Summon Monster list level the creature comes from (0 none).</summary>
    public int SummonListLevel;

    /// <summary>The creature count rule of that list level.</summary>
    public SummonCreatureCountInfo SummonCountInfo;

    /// <summary>The swarm a Summon Swarm cast places (PHB p.289).</summary>
    public string SummonSwarmNpcId;

    /// <summary>
    /// A shallow copy (the chosen items are shared references, as on the request): a held touch charge keeps the choices
    /// of its hold, and each delivery starts from a copy of them (batch 11 step 15).
    /// </summary>
    public CastChoices Clone() => (CastChoices)MemberwiseClone();
}

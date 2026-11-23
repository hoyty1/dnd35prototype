namespace DND35.AI.Knowledge
{
    /// <summary>
    /// The typed facts an AI reads about another creature (batch 13 step 7, tags v2; the vocabulary of
    /// docs/designs/enemy_ai_knowledge_and_personalities.md 3.1). Target scoring reads them through
    /// <see cref="KnowledgeService.Provider"/> (<see cref="ITargetView"/>), never the display tags of CharacterTags.
    /// </summary>
    public enum FactKey
    {
        /// <summary>The race or kind the creature shows (CharacterController.DisplayedRace: a disguise or wild shape form).</summary>
        Species,
        /// <summary>The creature type (MM p.305-317), one CreatureTypeId, parsed from CharacterStats.CreatureType.</summary>
        CreatureType,
        /// <summary>The creature's subtypes and descriptors (CharacterStats.CreatureTags), matched whole: several values.</summary>
        Subtype,
        /// <summary>The current size category (CharacterStats.CurrentSizeCategory).</summary>
        Size,
        /// <summary>The worn armor's category (<see cref="WornArmor"/>).</summary>
        ArmorCategory,
        /// <summary>A shield in either hand.</summary>
        ShieldCarried,
        /// <summary>A melee weapon in either hand (not the virtual unarmed strike).</summary>
        WieldsMelee,
        /// <summary>A ranged weapon in either hand.</summary>
        WieldsRanged,
        /// <summary>A reach weapon in either hand (PHB p.112).</summary>
        WieldsReach,
        /// <summary>How hurt the creature is (<see cref="WoundBand"/>), from its hit points.</summary>
        WoundBand,
        /// <summary>The PHB p.145 hit point state (HPState: disabled, dying, staggered by nonlethal damage, ...).</summary>
        HpState,
        /// <summary>The creature's active conditions (CombatConditionType names): several values.</summary>
        Condition,
        /// <summary>The creature casts spells from a class (CharacterStats.IsSpellcaster) or has a spellcasting component.</summary>
        Caster,
        /// <summary>The creature is mindless (no Intelligence score; immune to mind-affecting effects, MM p.313).</summary>
        Mindless,

        // Batch 14 step 2 (docs/designs/personalities_batch14.md, "New facts"): the facts the personality considerations
        // read. Appended, so the values above keep their numbers. The relational ones (from Distance on) need an observer
        // and are Unknown without one.

        /// <summary>
        /// On the floor and out of the fight: CharacterController.IsUnconscious (dying, stable, unconscious, or dead; PHB
        /// p.145). True for a dead subject too, unlike <see cref="Helpless"/>.
        /// Not WoundBand.Down, which is only 0 hit points or fewer: a disabled creature at 0 still acts, and a prone one is
        /// not down.
        /// </summary>
        Down,
        /// <summary>Helpless for a coup de grace (CharacterController.IsHelplessForCoupDeGrace: unconscious, or a helpless-like condition; PHB p.153, DMG p.301).</summary>
        Helpless,
        /// <summary>Visibly fleeing: withdrawing this turn (PHB p.143), frightened or panicked (DMG p.301).</summary>
        Fleeing,
        /// <summary>A healer: a cleric, a paladin with Lay on Hands (2nd level, PHB p.44), or a prepared (unspent) or known Healing-effect spell.</summary>
        Healer,
        /// <summary>The distance in squares from the observer, between the nearest squares of their spaces (PHB p.147 diagonals; Number).</summary>
        Distance,
        /// <summary>It threatens a square of the observer's space (ThreatSystem.GetThreatenedSquares; a Resilient Sphere blocks it).</summary>
        ThreatensObserver,
        /// <summary>It threatens an ally of the observer (not the observer) that is below 70% of its full hit points and not dead.</summary>
        ThreatensAllyInNeed,
        /// <summary>A standing ally of the observer (not the observer) is adjacent to it.</summary>
        EngagedByAlly,
        /// <summary>How many other foes of the observer, not dead, are within 2 squares of it (Number).</summary>
        FoesNear,
        /// <summary>The observer flanks it now (CombatUtils.IsAttackerFlanking: PHB p.153).</summary>
        FlankingNow,
        /// <summary>
        /// It damaged the observer since the start of the observer's previous turn (AiDamageMemory: everything taken
        /// between that turn start and now, ongoing damage at this turn's start included; Number: the damage).
        /// </summary>
        DamagedObserver,
        /// <summary>Its Armor Class (CharacterStats.ArmorClass; Number).</summary>
        AcEstimate,
        /// <summary>Its touch Armor Class (CharacterStats.TouchArmorClass; Number).</summary>
        TouchAcEstimate,
        /// <summary>Its current hit points (Number).</summary>
        HpCurrent,
        /// <summary>Its full hit points (CharacterStats.TotalMaxHP; Number).</summary>
        HpMax,
        /// <summary>
        /// The damage it can be expected to deal the observer in a round (AttackValue.ExpectedRoundDamage against the
        /// observer's AC; Amount), 0 when it cannot attack (incapacitated, helpless, or a condition that bars attacks).
        /// </summary>
        ThreatPerRound
    }

    /// <summary>How a fact is known (the AI design doc 3.1): an omniscient provider gives every fact at Confirmed.</summary>
    public enum KnowledgeLevel
    {
        Unknown = 0,
        /// <summary>Seen or heard now, no check: armor category, size, prone, wound band.</summary>
        Apparent = 1,
        /// <summary>Reasoned from behaviour (saw it cast, so a spellcaster); the only level that can be wrong.</summary>
        Inferred = 2,
        /// <summary>Made a Knowledge, Spellcraft or Sense Motive DC.</summary>
        Recalled = 3,
        /// <summary>A divination or a measured outcome; the truth.</summary>
        Confirmed = 4
    }

    /// <summary>
    /// How hurt a creature looks, from its hit points (the AI design doc's wound bands): Unhurt at full hit points, Hurt at
    /// half or more, BadlyHurt below half and above 0, Down at 0 or less. Not an HPState: staggered (PHB p.313) is
    /// nonlethal damage equal to current hit points, not a low total.
    /// </summary>
    public enum WoundBand
    {
        Unhurt,
        Hurt,
        BadlyHurt,
        Down
    }

    /// <summary>The worn armor's category as a fact: none, or light, medium or heavy armor (PHB p.123 Table 7-6).</summary>
    public enum WornArmor
    {
        Unarmored,
        Light,
        Medium,
        Heavy,
        /// <summary>Armor in the armor slot whose category is not Light, Medium or Heavy.</summary>
        Other
    }

    /// <summary>
    /// One fact's value as a view gives it: how it is known, and the value as a flag, a number (an enum's value) or text.
    /// Multi-valued facts (Subtype, Condition) are read with <see cref="ITargetView.Has"/> instead.
    /// </summary>
    public readonly struct FactValue
    {
        public readonly KnowledgeLevel Level;
        public readonly bool Flag;
        public readonly int Number;
        public readonly string Text;
        /// <summary>The value as a real number: the fact's measure for a quantity (ThreatPerRound), else <see cref="Number"/>.</summary>
        public readonly float Amount;

        public FactValue(KnowledgeLevel level, bool flag, int number, string text)
            : this(level, flag, number, text, number)
        {
        }

        public FactValue(KnowledgeLevel level, bool flag, int number, string text, float amount)
        {
            Level = level;
            Flag = flag;
            Number = number;
            Text = text;
            Amount = amount;
        }

        public bool IsKnown => Level != KnowledgeLevel.Unknown;

        public static readonly FactValue Unknown = new FactValue(KnowledgeLevel.Unknown, false, 0, null);

        /// <summary>A count or measure (Distance, HpCurrent, FoesNear): Flag is true when it is not 0.</summary>
        public static FactValue OfNumber(KnowledgeLevel level, int number)
            => new FactValue(level, number != 0, number, number.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>A real quantity (ThreatPerRound): Number is it rounded, Flag is true when it is above 0.</summary>
        public static FactValue OfAmount(KnowledgeLevel level, float amount)
            => new FactValue(level, amount > 0f, UnityEngine.Mathf.RoundToInt(amount),
                amount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), amount);

        public static FactValue Of(KnowledgeLevel level, bool flag) => new FactValue(level, flag, flag ? 1 : 0, flag ? "true" : "false");

        public static FactValue Of(KnowledgeLevel level, int number, string text) => new FactValue(level, number != 0, number, text);

        public static FactValue Of(KnowledgeLevel level, string text) => new FactValue(level, !string.IsNullOrEmpty(text), 0, text);

        public override string ToString() => Level + ":" + (Text ?? Number.ToString());
    }
}

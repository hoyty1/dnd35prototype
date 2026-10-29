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
        Mindless
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

        public FactValue(KnowledgeLevel level, bool flag, int number, string text)
        {
            Level = level;
            Flag = flag;
            Number = number;
            Text = text;
        }

        public bool IsKnown => Level != KnowledgeLevel.Unknown;

        public static readonly FactValue Unknown = new FactValue(KnowledgeLevel.Unknown, false, 0, null);

        public static FactValue Of(KnowledgeLevel level, bool flag) => new FactValue(level, flag, flag ? 1 : 0, flag ? "true" : "false");

        public static FactValue Of(KnowledgeLevel level, int number, string text) => new FactValue(level, number != 0, number, text);

        public static FactValue Of(KnowledgeLevel level, string text) => new FactValue(level, !string.IsNullOrEmpty(text), 0, text);

        public override string ToString() => Level + ":" + (Text ?? Number.ToString());
    }
}

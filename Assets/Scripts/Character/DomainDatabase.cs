using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Static database of D&D 3.5e cleric domains.
/// Each domain has a granted power and domain spells by level.
/// </summary>
public static class DomainDatabase
{
    private static Dictionary<string, DomainData> _domains = new Dictionary<string, DomainData>();
    private static bool _initialized = false;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        // Core domains from PHB
        // Note: domain spells reference canonical spell IDs when available.
        Register(new DomainData("Good",
            "Cast good spells at +1 caster level.",
            new Dictionary<int, string> { { 1, SpellNames.PROTECTION_FROM_EVIL }, { 2, SpellNames.AID } }));

        Register(new DomainData("Healing",
            "Cast healing spells at +1 caster level.",
            new Dictionary<int, string> { { 1, SpellNames.CURE_LIGHT_WOUNDS }, { 2, SpellNames.CURE_MODERATE_WOUNDS } }));

        Register(new DomainData("Law",
            "Cast law spells at +1 caster level.",
            new Dictionary<int, string> { { 1, SpellNames.PROTECTION_FROM_CHAOS }, { 2, SpellNames.CALM_EMOTIONS } }));

        Register(new DomainData("War",
            "Free Martial Weapon Proficiency and Weapon Focus with deity's favored weapon.",
            new Dictionary<int, string> { { 1, SpellNames.MAGIC_WEAPON }, { 2, SpellNames.SPIRITUAL_WEAPON } }));

        Register(new DomainData("Magic",
            "Use scrolls, staves, and wands as a wizard of one level higher.",
            new Dictionary<int, string> { { 1, SpellNames.NYSTULS_MAGIC_AURA }, { 2, SpellNames.IDENTIFY } }));

        Register(new DomainData("Knowledge",
            "Add all Knowledge skills to your list of class skills. You cast divination spells at +1 caster level.",
            new Dictionary<int, string> { { 1, SpellNames.DOMAIN_DETECT_SECRET_DOORS }, { 2, SpellNames.DETECT_THOUGHTS } }));

        Register(new DomainData("Protection",
            "You can generate a protective ward as a supernatural ability. Grant +1 resistance bonus to next saving throw, 1/day.",
            new Dictionary<int, string> { { 1, SpellNames.SANCTUARY }, { 2, SpellNames.SHIELD_OTHER } }));

        Register(new DomainData("Strength",
            "You can perform a feat of strength as a supernatural ability. You gain an enhancement bonus to Strength equal to your cleric level for 1 round, 1/day.",
            new Dictionary<int, string> { { 1, SpellNames.ENLARGE_PERSON }, { 2, SpellNames.BULLS_STRENGTH } }));

        Register(new DomainData("Trickery",
            "Add Bluff, Disguise, and Hide to your list of class skills.",
            new Dictionary<int, string> { { 1, SpellNames.DISGUISE_SELF }, { 2, SpellNames.INVISIBILITY } }));

        Register(new DomainData("Death",
            "You may use a death touch once per day. Roll 1d6 per cleric level; if the total equals or exceeds the target's current hit points, it dies.",
            new Dictionary<int, string> { { 1, SpellNames.CAUSE_FEAR }, { 2, SpellNames.DEATH_KNELL } }));

        Register(new DomainData("Evil",
            "Cast evil spells at +1 caster level.",
            new Dictionary<int, string> { { 1, SpellNames.PROTECTION_FROM_GOOD }, { 2, SpellNames.DOMAIN_DESECRATE } }));

        Register(new DomainData("Chaos",
            "Cast chaos spells at +1 caster level.",
            new Dictionary<int, string> { { 1, SpellNames.PROTECTION_FROM_LAW }, { 2, SpellNames.SHATTER } }));

        Register(new DomainData("Destruction",
            "You gain the smite power. Once per day, make a single melee attack with +4 on attack rolls and bonus damage equal to your cleric level.",
            new Dictionary<int, string> { { 1, SpellNames.INFLICT_LIGHT_WOUNDS }, { 2, SpellNames.SHATTER } }));

        Register(new DomainData("Sun",
            "Once per day, you can perform a greater turning against undead. The undead so turned are destroyed.",
            new Dictionary<int, string> { { 1, SpellNames.ENDURE_ELEMENTS }, { 2, SpellNames.DOMAIN_HEAT_METAL } }));

        Register(new DomainData("Luck",
            "You gain the power of good fortune: reroll one roll per day before the DM declares success or failure. You must take the reroll result.",
            new Dictionary<int, string> { { 1, SpellNames.ENTROPIC_SHIELD }, { 2, SpellNames.AID } }));

        Register(new DomainData("Air",
            "Turn or destroy earth creatures as a good cleric turns undead. Rebuke, command, or bolster air creatures as an evil cleric rebukes undead.",
            new Dictionary<int, string> { { 1, SpellNames.OBSCURING_MIST }, { 2, SpellNames.DOMAIN_WIND_WALL } }));

        Register(new DomainData("Animal",
            "You can use speak with animals once per day as a spell-like ability. Knowledge (nature) is a class skill.",
            new Dictionary<int, string> { { 1, SpellNames.DOMAIN_CALM_ANIMALS }, { 2, SpellNames.DOMAIN_HOLD_ANIMAL } }));

        Register(new DomainData("Earth",
            "Turn or destroy air creatures as a good cleric turns undead. Rebuke, command, or bolster earth creatures as an evil cleric rebukes undead.",
            new Dictionary<int, string> { { 1, SpellNames.DOMAIN_MAGIC_STONE }, { 2, SpellNames.DOMAIN_SOFTEN_EARTH } }));

        Register(new DomainData("Fire",
            "Turn or destroy water creatures as a good cleric turns undead. Rebuke, command, or bolster fire creatures as an evil cleric rebukes undead.",
            new Dictionary<int, string> { { 1, SpellNames.BURNING_HANDS }, { 2, SpellNames.DOMAIN_PRODUCE_FLAME } }));

        Register(new DomainData("Plant",
            "Rebuke or command plant creatures as an evil cleric rebukes or commands undead. Knowledge (nature) is a class skill.",
            new Dictionary<int, string> { { 1, SpellNames.DOMAIN_ENTANGLE }, { 2, SpellNames.DOMAIN_BARKSKIN } }));

        Register(new DomainData("Water",
            "Turn or destroy fire creatures as a good cleric turns undead. Rebuke, command, or bolster water creatures as an evil cleric rebukes undead.",
            new Dictionary<int, string> { { 1, SpellNames.OBSCURING_MIST }, { 2, SpellNames.FOG_CLOUD } }));

        Register(new DomainData("Travel",
            "For a total time per day of 1 round per cleric level, you can act normally regardless of magical effects that impede movement. Survival is a class skill.",
            new Dictionary<int, string> { { 1, SpellNames.DOMAIN_LONGSTRIDER }, { 2, SpellNames.LOCATE_OBJECT } }));

        Debug.Log($"[DomainDatabase] Initialized {_domains.Count} domains.");
    }

    private static void Register(DomainData domain)
    {
        _domains[domain.Name] = domain;
    }

    /// <summary>Get a domain by name.</summary>
    public static DomainData GetDomain(string domainName)
    {
        if (!_initialized) Init();
        return _domains.TryGetValue(domainName, out DomainData domain) ? domain : null;
    }

    /// <summary>Get all domains.</summary>
    public static List<DomainData> GetAllDomains()
    {
        if (!_initialized) Init();
        return new List<DomainData>(_domains.Values);
    }

    /// <summary>Get all domain spells for given domains at a specific level.</summary>
    public static List<string> GetDomainSpellIds(List<string> domainNames, int spellLevel)
    {
        if (!_initialized) Init();
        var result = new List<string>();
        foreach (string name in domainNames)
        {
            DomainData domain = GetDomain(name);
            if (domain != null)
            {
                string spellId = domain.GetDomainSpellId(spellLevel);
                if (spellId != null && !result.Contains(spellId))
                    result.Add(spellId);
            }
        }
        return result;
    }

    /// <summary>Total number of registered domains.</summary>
    public static int Count => _domains.Count;
}

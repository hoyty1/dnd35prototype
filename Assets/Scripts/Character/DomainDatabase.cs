using System.Collections.Generic;
using UnityEngine;

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
            new Dictionary<int, string> { { 1, "protection_from_evil" }, { 2, "aid" } }));

        Register(new DomainData("Healing",
            "Cast healing spells at +1 caster level.",
            new Dictionary<int, string> { { 1, "cure_light_wounds" }, { 2, "cure_moderate_wounds" } }));

        Register(new DomainData("Law",
            "Cast law spells at +1 caster level.",
            new Dictionary<int, string> { { 1, "domain_protection_from_chaos" }, { 2, "calm_emotions" } }));

        Register(new DomainData("War",
            "Free Martial Weapon Proficiency and Weapon Focus with deity's favored weapon.",
            new Dictionary<int, string> { { 1, "magic_weapon" }, { 2, "spiritual_weapon" } }));

        Register(new DomainData("Magic",
            "Use scrolls, staves, and wands as a wizard of one level higher.",
            new Dictionary<int, string> { { 1, "nystuls_magic_aura" }, { 2, "identify" } }));

        Register(new DomainData("Knowledge",
            "Add all Knowledge skills to your list of class skills. You cast divination spells at +1 caster level.",
            new Dictionary<int, string> { { 1, "domain_detect_secret_doors" }, { 2, "detect_thoughts" } }));

        Register(new DomainData("Protection",
            "You can generate a protective ward as a supernatural ability. Grant +1 resistance bonus to next saving throw, 1/day.",
            new Dictionary<int, string> { { 1, "sanctuary" }, { 2, "shield_other" } }));

        Register(new DomainData("Strength",
            "You can perform a feat of strength as a supernatural ability. You gain an enhancement bonus to Strength equal to your cleric level for 1 round, 1/day.",
            new Dictionary<int, string> { { 1, "enlarge_person" }, { 2, "bulls_strength" } }));

        Register(new DomainData("Trickery",
            "Add Bluff, Disguise, and Hide to your list of class skills.",
            new Dictionary<int, string> { { 1, "disguise_self" }, { 2, "invisibility" } }));

        Register(new DomainData("Death",
            "You may use a death touch once per day. Roll 1d6 per cleric level; if the total equals or exceeds the target's current hit points, it dies.",
            new Dictionary<int, string> { { 1, "cause_fear" }, { 2, "death_knell" } }));

        Register(new DomainData("Evil",
            "Cast evil spells at +1 caster level.",
            new Dictionary<int, string> { { 1, "domain_protection_from_good" }, { 2, "domain_desecrate" } }));

        Register(new DomainData("Chaos",
            "Cast chaos spells at +1 caster level.",
            new Dictionary<int, string> { { 1, "domain_protection_from_law" }, { 2, "shatter" } }));

        Register(new DomainData("Destruction",
            "You gain the smite power. Once per day, make a single melee attack with +4 on attack rolls and bonus damage equal to your cleric level.",
            new Dictionary<int, string> { { 1, "inflict_light_wounds" }, { 2, "shatter" } }));

        Register(new DomainData("Sun",
            "Once per day, you can perform a greater turning against undead. The undead so turned are destroyed.",
            new Dictionary<int, string> { { 1, "endure_elements" }, { 2, "domain_heat_metal" } }));

        Register(new DomainData("Luck",
            "You gain the power of good fortune: reroll one roll per day before the DM declares success or failure. You must take the reroll result.",
            new Dictionary<int, string> { { 1, "entropic_shield" }, { 2, "aid" } }));

        Register(new DomainData("Air",
            "Turn or destroy earth creatures as a good cleric turns undead. Rebuke, command, or bolster air creatures as an evil cleric rebukes undead.",
            new Dictionary<int, string> { { 1, "obscuring_mist" }, { 2, "domain_wind_wall" } }));

        Register(new DomainData("Animal",
            "You can use speak with animals once per day as a spell-like ability. Knowledge (nature) is a class skill.",
            new Dictionary<int, string> { { 1, "domain_calm_animals" }, { 2, "domain_hold_animal" } }));

        Register(new DomainData("Earth",
            "Turn or destroy air creatures as a good cleric turns undead. Rebuke, command, or bolster earth creatures as an evil cleric rebukes undead.",
            new Dictionary<int, string> { { 1, "domain_magic_stone" }, { 2, "domain_soften_earth" } }));

        Register(new DomainData("Fire",
            "Turn or destroy water creatures as a good cleric turns undead. Rebuke, command, or bolster fire creatures as an evil cleric rebukes undead.",
            new Dictionary<int, string> { { 1, "burning_hands" }, { 2, "domain_produce_flame" } }));

        Register(new DomainData("Plant",
            "Rebuke or command plant creatures as an evil cleric rebukes or commands undead. Knowledge (nature) is a class skill.",
            new Dictionary<int, string> { { 1, "domain_entangle" }, { 2, "domain_barkskin" } }));

        Register(new DomainData("Water",
            "Turn or destroy fire creatures as a good cleric turns undead. Rebuke, command, or bolster water creatures as an evil cleric rebukes undead.",
            new Dictionary<int, string> { { 1, "obscuring_mist" }, { 2, "fog_cloud" } }));

        Register(new DomainData("Travel",
            "For a total time per day of 1 round per cleric level, you can act normally regardless of magical effects that impede movement. Survival is a class skill.",
            new Dictionary<int, string> { { 1, "domain_longstrider" }, { 2, "locate_object" } }));

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

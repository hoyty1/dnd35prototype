using System;
using System.Collections.Generic;

namespace DND35.AI.Knowledge
{
    /// <summary>
    /// One typed test of a target's facts, parsed from a profile's TagPriority string (batch 13 step 7, tags v2). Profiles
    /// keep their strings ("HP State: Disabled", "Armor: Heavy Armor", "Race: Halfling"); <see cref="Parse"/> turns each into
    /// a query on one <see cref="FactKey"/>, matched against an <see cref="ITargetView"/> whole, never as a substring: "Elf"
    /// is not "Half-Elf" (AI-078; until batch 13 step 7 a request matched any tag that contained it or that it contained).
    /// </summary>
    public sealed class FactQuery
    {
        public enum QueryKind
        {
            /// <summary>The fact equals <see cref="Value"/> (ITargetView.Has).</summary>
            Equals,
            /// <summary>The boolean fact is true.</summary>
            IsTrue,
            /// <summary>
            /// No manufactured weapon in either hand: neither WieldsMelee nor WieldsRanged ("Wielding: Unarmed", the old
            /// display tag's meaning kept 1:1). This is not "unarmed" in the PHB p.139 sense: a monk, a creature with
            /// Improved Unarmed Strike or natural weapons, and a caster holding a touch charge are armed but match it.
            /// </summary>
            NoManufacturedWeapon
        }

        public FactKey Key { get; private set; }
        public string Value { get; private set; }
        public QueryKind Kind { get; private set; }
        /// <summary>The string this query was parsed from.</summary>
        public string Source { get; private set; }
        /// <summary>
        /// True when this query came from the bare-word fallback (a bare word that names no other fact is read as a species).
        /// A shipped profile string should never need it: a misspelled weight would parse as a species no creature has and
        /// silently match nothing (KnowledgeFactsTests checks the shipped strings).
        /// </summary>
        public bool IsSpeciesFallback { get; private set; }

        private FactQuery(FactKey key, string value, QueryKind kind, string source, bool speciesFallback = false)
        {
            Key = key;
            Value = value;
            Kind = kind;
            Source = source;
            IsSpeciesFallback = speciesFallback;
        }

        public override string ToString() => Kind + " " + Key + (Value != null ? "=" + Value : "");

        /// <summary>True when the view knows a fact that satisfies this query.</summary>
        public bool Matches(ITargetView view)
        {
            if (view == null)
                return false;
            switch (Kind)
            {
                case QueryKind.IsTrue:
                {
                    FactValue f = view.Get(Key);
                    return f.IsKnown && f.Flag;
                }
                case QueryKind.NoManufacturedWeapon:
                {
                    FactValue melee = view.Get(FactKey.WieldsMelee);
                    FactValue ranged = view.Get(FactKey.WieldsRanged);
                    return melee.IsKnown && ranged.IsKnown && !melee.Flag && !ranged.Flag;
                }
                default:
                    return view.Has(Key, Value);
            }
        }

        private static readonly Dictionary<string, FactQuery> Cache = new Dictionary<string, FactQuery>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Unparsable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The query of a TagPriority string, or null when it names no fact. Forms: "Race: &lt;species&gt;", "Type: &lt;creature
        /// type&gt;", "Subtype: &lt;subtype&gt;", "Size: &lt;size&gt;", "Armor: Unarmored|Light Armor|Medium Armor|Heavy Armor",
        /// "Wielding: Unarmed|Melee|Ranged|Reach|Shield", "HP State: &lt;HPState&gt;", "Wound: Unhurt|Hurt|Badly Hurt|Down",
        /// "Condition: &lt;condition&gt;", and the bare words "Unarmored", "Light Armor", "Medium Armor", "Heavy Armor",
        /// "Unarmed", "Shield Equipped", "Caster" and "Mindless". Any other bare word is a species name
        /// (<see cref="IsSpeciesFallback"/>). "HP State: Staggered"
        /// reads the BadlyHurt wound band (see the hp state case).
        /// </summary>
        public static FactQuery Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            string source = text.Trim();
            if (Cache.TryGetValue(source, out FactQuery cached))
                return cached;
            if (Unparsable.Contains(source))
                return null;

            FactQuery q = ParseUncached(source);
            if (q == null)
                Unparsable.Add(source);
            else
                Cache[source] = q;
            return q;
        }

        private static FactQuery ParseUncached(string source)
        {
            int colon = source.IndexOf(':');
            if (colon < 0)
                return ParseBare(source, source);

            string prefix = source.Substring(0, colon).Trim();
            string value = source.Substring(colon + 1).Trim();
            if (value.Length == 0)
                return null;

            switch (prefix.ToLowerInvariant())
            {
                case "race":
                case "species":
                    return new FactQuery(FactKey.Species, value, QueryKind.Equals, source);
                case "type":
                    return CreatureTypeProgressionDatabase.TryParseCreatureType(value, out CreatureTypeId type)
                        ? new FactQuery(FactKey.CreatureType, KnowledgeFacts.CreatureTypeName(type), QueryKind.Equals, source)
                        : null;
                case "subtype":
                    return new FactQuery(FactKey.Subtype, value, QueryKind.Equals, source);
                case "size":
                    return Enum.TryParse(value.Replace(" ", ""), true, out SizeCategory size)
                        ? new FactQuery(FactKey.Size, size.ToString(), QueryKind.Equals, source)
                        : null;
                case "armor":
                    return ParseArmor(value, source);
                case "wielding":
                    return ParseWielding(value, source);
                case "hp state":
                    if (!Enum.TryParse(value.Replace(" ", ""), true, out HPState state))
                        return null;
                    // The profiles' "HP State: Staggered" rows mean a badly wounded target to finish off; staggered is
                    // nonlethal damage equal to current hit points (PHB p.313), so the row reads the wound band (AI-078).
                    // Disabled and Dying are hit point states and stay one.
                    return state == HPState.Staggered
                        ? new FactQuery(FactKey.WoundBand, WoundBand.BadlyHurt.ToString(), QueryKind.Equals, source)
                        : new FactQuery(FactKey.HpState, state.ToString(), QueryKind.Equals, source);
                case "wound":
                case "wound band":
                    return Enum.TryParse(value.Replace(" ", ""), true, out WoundBand band)
                        ? new FactQuery(FactKey.WoundBand, band.ToString(), QueryKind.Equals, source)
                        : null;
                case "condition":
                case "status":
                    return Enum.TryParse(value.Replace(" ", ""), true, out CombatConditionType condition)
                        ? new FactQuery(FactKey.Condition, condition.ToString(), QueryKind.Equals, source)
                        : null;
                default:
                    return null;
            }
        }

        private static FactQuery ParseBare(string word, string source)
        {
            FactQuery armor = ParseArmor(word, source);
            if (armor != null)
                return armor;
            switch (word.ToLowerInvariant())
            {
                case "unarmed":
                    return new FactQuery(FactKey.WieldsMelee, null, QueryKind.NoManufacturedWeapon, source);
                case "shield equipped":
                    return new FactQuery(FactKey.ShieldCarried, null, QueryKind.IsTrue, source);
                case "caster":
                    return new FactQuery(FactKey.Caster, null, QueryKind.IsTrue, source);
                case "mindless":
                    return new FactQuery(FactKey.Mindless, null, QueryKind.IsTrue, source);
                default:
                    return new FactQuery(FactKey.Species, word, QueryKind.Equals, source, speciesFallback: true);
            }
        }

        private static FactQuery ParseArmor(string value, string source)
        {
            switch (value.ToLowerInvariant())
            {
                case "unarmored":
                case "none":
                    return new FactQuery(FactKey.ArmorCategory, WornArmor.Unarmored.ToString(), QueryKind.Equals, source);
                case "light armor":
                case "light":
                    return new FactQuery(FactKey.ArmorCategory, WornArmor.Light.ToString(), QueryKind.Equals, source);
                case "medium armor":
                case "medium":
                    return new FactQuery(FactKey.ArmorCategory, WornArmor.Medium.ToString(), QueryKind.Equals, source);
                case "heavy armor":
                case "heavy":
                    return new FactQuery(FactKey.ArmorCategory, WornArmor.Heavy.ToString(), QueryKind.Equals, source);
                default:
                    return null;
            }
        }

        private static FactQuery ParseWielding(string value, string source)
        {
            switch (value.ToLowerInvariant())
            {
                case "unarmed":
                    return new FactQuery(FactKey.WieldsMelee, null, QueryKind.NoManufacturedWeapon, source);
                case "melee":
                    return new FactQuery(FactKey.WieldsMelee, null, QueryKind.IsTrue, source);
                case "ranged":
                    return new FactQuery(FactKey.WieldsRanged, null, QueryKind.IsTrue, source);
                case "reach":
                    return new FactQuery(FactKey.WieldsReach, null, QueryKind.IsTrue, source);
                case "shield":
                    return new FactQuery(FactKey.ShieldCarried, null, QueryKind.IsTrue, source);
                default:
                    return null;
            }
        }
    }
}

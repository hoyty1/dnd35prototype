using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ranger's combat class features (PHB p.46-48; CHR-020), one actor-agnostic set of rules that the PC attack and
/// skill paths, the AI, the level-up flow and NPC spawning all call (docs/systems/PC_NPC_PARITY.md).
/// <list type="bullet">
/// <item>Favored Enemy (Ex): the selections due by ranger level (1st, 5th, 10th, 15th, 20th), the +2 increase that comes
/// with every selection after the first, the matching of an entry against a creature's real type and subtypes
/// (<see cref="MatchesFavoredEnemy"/>), and the bonus on weapon damage rolls (added by
/// <c>CharacterController.PerformSingleAttackWithCritCore</c>, coup de grace and grapple damage) and on Bluff, Listen,
/// Sense Motive, Spot and Survival checks against such creatures (<see cref="CharacterStats.GetSkillBonusAgainst"/>).
/// When several entries match one creature the best applies; they do not stack (PHB p.47).</item>
/// <item>Combat Style (Ex), Improved Combat Style, Combat Style Mastery: the ranger is treated as having the style's
/// feats (<see cref="CombatStyleTreatsAsHavingFeat"/>, read by <see cref="CharacterStats.HasFeat"/>) only while he
/// wears light or no armor.</item>
/// <item>Track (1st) and Endurance (3rd) are bonus feats by ranger level (<see cref="GrantAutomaticFeats"/>).</item>
/// <item>NPC rangers take their choices from their definition, or the DMG p.122 sample ranger's
/// (<see cref="ApplyNpcChoices"/>).</item>
/// </list>
/// Wild empathy, the animal companion (PHB p.48: effective druid level one-half the ranger level), spells and
/// woodland stride have no rules path yet (CHR-020, SPL-039).
/// </summary>
public static class RangerRules
{
    /// <summary>The label of the favored enemy term in weapon damage breakdowns and the scenario trace.</summary>
    public const string FavoredEnemyDamageLabel = "Favored enemy";

    /// <summary>The skills the favored enemy bonus applies to (PHB p.47).</summary>
    public static readonly string[] FavoredEnemySkills = { "Bluff", "Listen", "Sense Motive", "Spot", "Survival" };

    private static readonly HashSet<string> CombatStyleFeatNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "Rapid Shot", "Manyshot", "Improved Precise Shot",
        "Two-Weapon Fighting", "Improved Two-Weapon Fighting", "Greater Two-Weapon Fighting"
    };

    /// <summary>Ranger class level (0 for a creature without ranger levels).</summary>
    public static int RangerLevel(CharacterStats stats) => stats != null ? stats.GetClassLevel("Ranger") : 0;

    // ─────────────────────────────────────────────
    // Favored Enemy (PHB p.47)
    // ─────────────────────────────────────────────

    /// <summary>Favored enemy selections a ranger of <paramref name="rangerLevel"/> has made: 1 at 1st, +1 at 5th, 10th, 15th, 20th.</summary>
    public static int FavoredEnemySelectionsDue(int rangerLevel) => FavoredEnemyTypes.GetTotalSelectionsAtLevel(Mathf.Max(0, rangerLevel));

    /// <summary>Selections still to make: due by level less those made.</summary>
    public static int PendingFavoredEnemySelections(FavoredEnemyData favored, int rangerLevel)
    {
        int made = favored != null ? favored.SelectionsMade : 0;
        return Mathf.Max(0, FavoredEnemySelectionsDue(rangerLevel) - made);
    }

    /// <summary>
    /// Whether the next selection also raises one favored enemy's bonus by 2: every selection after the first (PHB p.47:
    /// from 5th level each new selection comes with a +2 to any one favored enemy, the new one included).
    /// </summary>
    public static bool NextSelectionGrantsIncrease(FavoredEnemyData favored) => favored != null && favored.SelectionsMade >= 1;

    /// <summary>Whether <paramref name="creatureType"/> is a choice of PHB Table 3-14 (case-insensitive).</summary>
    public static bool IsValidFavoredEnemyType(string creatureType) => CanonicalFavoredEnemyType(creatureType) != null;

    /// <summary>The Table 3-14 spelling of <paramref name="creatureType"/>, or null when it is not a choice.</summary>
    public static string CanonicalFavoredEnemyType(string creatureType)
    {
        if (string.IsNullOrWhiteSpace(creatureType))
            return null;
        string key = NormalizeTypeName(creatureType);
        string[] all = FavoredEnemyTypes.AllTypes;
        for (int i = 0; i < all.Length; i++)
            if (NormalizeTypeName(all[i]) == key)
                return all[i];
        return null;
    }

    /// <summary>
    /// Makes one favored enemy selection (PHB p.47): <paramref name="newType"/> is added at +2, and from the second
    /// selection on <paramref name="increaseType"/> (an existing favored enemy or the new one) gains +2. Refused when no
    /// selection is due at <paramref name="rangerLevel"/>, the type is not on Table 3-14 or already chosen, or the
    /// increase is missing or names a type that is not a favored enemy.
    /// </summary>
    public static bool TryApplyFavoredEnemySelection(FavoredEnemyData favored, int rangerLevel, string newType,
        string increaseType, out string reason)
    {
        reason = null;
        if (favored == null)
        {
            reason = "no favored enemy data";
            return false;
        }
        if (PendingFavoredEnemySelections(favored, rangerLevel) <= 0)
        {
            reason = $"no favored enemy selection is due at ranger level {rangerLevel}";
            return false;
        }
        string canonical = CanonicalFavoredEnemyType(newType);
        if (canonical == null)
        {
            reason = $"'{newType}' is not a favored enemy type of PHB Table 3-14";
            return false;
        }
        if (favored.Has(canonical))
        {
            reason = $"{canonical} is already a favored enemy";
            return false;
        }

        bool grantsIncrease = NextSelectionGrantsIncrease(favored);
        string increaseCanonical = null;
        if (grantsIncrease)
        {
            increaseCanonical = CanonicalFavoredEnemyType(increaseType);
            if (increaseCanonical == null || (!favored.Has(increaseCanonical) && increaseCanonical != canonical))
            {
                reason = "this selection also raises one favored enemy's bonus by 2: choose an existing favored enemy or the new one";
                return false;
            }
        }

        favored.AddNew(canonical);
        if (grantsIncrease)
            favored.IncreaseBonus(increaseCanonical);
        return true;
    }

    /// <summary>
    /// Whether <paramref name="creature"/> is a creature of the favored enemy <paramref name="entryType"/> by its real
    /// type and subtypes: the type must equal <see cref="CharacterStats.CreatureType"/> (spacing and case ignored, so
    /// "MagicalBeast" is "Magical Beast"); a humanoid subtype matches the creature tags, the race and the race it counts
    /// as (<see cref="RacialTraitRules.IsOfKind"/>: a half-orc is an orc, PHB p.19; goblins, hobgoblins and bugbears are
    /// goblinoids); an outsider subtype matches a creature tag (Evil, Lawful, Native, Fire...). A creature whose type
    /// changed loses the old one (an orc zombie is undead, not an orc, MM p.266). Disguise and illusion do not change
    /// what a creature is, so the bonus follows the real creature (delegated RAW reading 2026-10-14, CHR-020).
    /// </summary>
    public static bool MatchesFavoredEnemy(string entryType, CharacterStats creature)
    {
        if (creature == null || string.IsNullOrWhiteSpace(entryType))
            return false;

        string entry = entryType.Trim();
        string baseType = entry;
        string subtype = null;
        int open = entry.IndexOf('(');
        if (open >= 0)
        {
            baseType = entry.Substring(0, open).Trim();
            int close = entry.IndexOf(')', open + 1);
            subtype = (close > open ? entry.Substring(open + 1, close - open - 1) : entry.Substring(open + 1)).Trim();
        }

        string creatureType = string.IsNullOrWhiteSpace(creature.CreatureType) ? "Humanoid" : creature.CreatureType;
        if (NormalizeTypeName(baseType) != NormalizeTypeName(creatureType))
            return false;
        if (string.IsNullOrEmpty(subtype))
            return true;

        if (NormalizeTypeName(baseType) == "humanoid")
            return RacialTraitRules.IsOfKind(creature, subtype);
        return HasTag(creature, subtype);
    }

    /// <summary>
    /// The favored enemy bonus of <paramref name="ranger"/> against <paramref name="creature"/>: the best bonus among
    /// the entries the creature matches (PHB p.47: a creature in two categories gives the higher bonus, they do not
    /// stack), 0 for a creature of no favored type or a ranger without favored enemies.
    /// </summary>
    public static int FavoredEnemyBonus(CharacterStats ranger, CharacterStats creature)
    {
        FavoredEnemyData favored = ranger != null ? ranger.RangerFavoredEnemies : null;
        if (favored == null || creature == null || ReferenceEquals(ranger, creature) || RangerLevel(ranger) <= 0)
            return 0;
        int best = 0;
        for (int i = 0; i < favored.Enemies.Count; i++)
        {
            FavoredEnemyEntry entry = favored.Enemies[i];
            if (entry != null && entry.Bonus > best && MatchesFavoredEnemy(entry.CreatureType, creature))
                best = entry.Bonus;
        }
        return best;
    }

    /// <summary>
    /// The favored enemy bonus on a weapon damage roll against <paramref name="target"/> (PHB p.47): every weapon
    /// damage roll, melee or ranged, with a manufactured or natural weapon or an unarmed strike. It is a flat bonus, so a
    /// critical hit multiplies it (PHB p.140).
    /// </summary>
    public static int FavoredEnemyDamageBonus(CharacterStats attacker, CharacterStats target) => FavoredEnemyBonus(attacker, target);

    /// <summary>The favored enemy bonus on a <paramref name="skillName"/> check against <paramref name="creature"/>: Bluff, Listen, Sense Motive, Spot and Survival only.</summary>
    public static int FavoredEnemySkillBonus(CharacterStats ranger, CharacterStats creature, string skillName)
    {
        if (!IsFavoredEnemySkill(skillName))
            return 0;
        return FavoredEnemyBonus(ranger, creature);
    }

    /// <summary>Whether the favored enemy bonus applies to checks with <paramref name="skillName"/>.</summary>
    public static bool IsFavoredEnemySkill(string skillName)
    {
        if (string.IsNullOrEmpty(skillName))
            return false;
        for (int i = 0; i < FavoredEnemySkills.Length; i++)
            if (string.Equals(FavoredEnemySkills[i], skillName, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static bool HasTag(CharacterStats creature, string tag)
    {
        List<string> tags = creature.CreatureTags;
        if (tags == null)
            return false;
        for (int i = 0; i < tags.Count; i++)
            if (tags[i] != null && string.Equals(tags[i].Trim(), tag, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string NormalizeTypeName(string name)
        => string.IsNullOrEmpty(name) ? string.Empty : name.Replace(" ", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();

    // ─────────────────────────────────────────────
    // Combat Style (PHB p.48)
    // ─────────────────────────────────────────────

    /// <summary>Whether a ranger of <paramref name="rangerLevel"/> still has to choose a combat style (from 2nd level).</summary>
    public static bool NeedsCombatStyleChoice(CombatStyleData style, int rangerLevel)
        => rangerLevel >= 2 && (style == null || !style.HasStyleSelected);

    /// <summary>
    /// Light or no armor (PHB p.48): no armor, or armor whose effective category is light. Mithral armor counts one
    /// category lighter for movement and other limitations of armor (DMG p.284, which names the barbarian's fast movement
    /// as an example), so a mithral breastplate keeps the style. A shield and the load carried do not matter: the text names only armor (delegated RAW reading 2026-10-14,
    /// CHR-020).
    /// </summary>
    public static bool WearsLightOrNoArmor(CharacterStats stats)
    {
        if (stats == null)
            return false;
        if (!stats.IsWearingArmor)
            return true;
        return MaterialProperties.GetEffectiveArmorCategory(stats.EquippedArmorItem) == ArmorCategory.Light;
    }

    /// <summary>The combat style feats due at the ranger's level, whatever the armor (empty without a style or below 2nd).</summary>
    public static List<string> CombatStyleFeats(CharacterStats stats)
    {
        int level = RangerLevel(stats);
        CombatStyleData style = stats != null ? stats.RangerCombatStyle : null;
        if (level < 2 || style == null || !style.HasStyleSelected)
            return new List<string>();
        return style.GetAllFeatsUpToLevel(level);
    }

    /// <summary>Whether the ranger's combat style benefits apply now: a style chosen, ranger level 2+, light or no armor.</summary>
    public static bool CombatStyleApplies(CharacterStats stats)
    {
        if (RangerLevel(stats) < 2)
            return false;
        CombatStyleData style = stats.RangerCombatStyle;
        return style != null && style.HasStyleSelected && WearsLightOrNoArmor(stats);
    }

    /// <summary>
    /// Whether the combat style treats the ranger as having <paramref name="featName"/> now (PHB p.48: he counts as having
    /// the style's feats without their prerequisites, but only in light or no armor). The ranger then
    /// meets that feat as a prerequisite of other feats too while the style applies (delegated RAW reading 2026-10-14,
    /// CHR-020). Cheap for every other feat name: <see cref="CharacterStats.HasFeat"/> calls it on each check.
    /// </summary>
    public static bool CombatStyleTreatsAsHavingFeat(CharacterStats stats, string featName)
    {
        if (stats == null || featName == null || !CombatStyleFeatNames.Contains(featName))
            return false;
        if (!CombatStyleApplies(stats))
            return false;
        return stats.RangerCombatStyle.GetAllFeatsUpToLevel(RangerLevel(stats)).Contains(featName);
    }

    // Feats whose prerequisite chain names a combat style feat, built once from FeatDefinitions; the cheap first test of
    // LosesFeatToMissingStylePrerequisite, which CharacterStats.HasFeat runs for every feat a creature took.
    private static HashSet<string> _styleDependentFeats;

    private static HashSet<string> StyleDependentFeats
    {
        get
        {
            if (_styleDependentFeats != null)
                return _styleDependentFeats;
            var set = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, FeatDefinition> all = FeatDefinitions.AllFeats;
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (KeyValuePair<string, FeatDefinition> pair in all)
                {
                    if (pair.Value == null || set.Contains(pair.Key))
                        continue;
                    List<FeatPrerequisite> prereqs = pair.Value.Prerequisites;
                    for (int i = 0; prereqs != null && i < prereqs.Count; i++)
                    {
                        FeatPrerequisite p = prereqs[i];
                        if (p != null && p.Type == PrerequisiteType.Feat && p.ParamString != null
                            && (CombatStyleFeatNames.Contains(p.ParamString) || set.Contains(p.ParamString)))
                        {
                            set.Add(pair.Key);
                            grew = true;
                            break;
                        }
                    }
                }
            }
            _styleDependentFeats = set;
            return set;
        }
    }

    /// <summary>
    /// Whether a feat the creature took (<paramref name="featName"/> is in <see cref="CharacterStats.Feats"/>) is
    /// unusable now because one of its feat prerequisites, directly or down the chain, is a combat style feat the
    /// ranger has only through his style and the style does not apply (medium or heavy armor). PHB p.87: a character who
    /// loses a prerequisite cannot use the feat that needs it until he regains it. Example: a two-weapon ranger who took
    /// Two-Weapon Defense or Improved Two-Weapon Fighting on the style's Two-Weapon Fighting loses them in a breastplate,
    /// and keeps them when he took Two-Weapon Fighting himself. Only the style chain is checked here; other lost
    /// prerequisites (ability damage, a lost class feature) are not (CHR-084).
    /// </summary>
    public static bool LosesFeatToMissingStylePrerequisite(CharacterStats stats, string featName)
        => LosesFeatToMissingStylePrerequisite(stats, featName, 0);

    private static bool LosesFeatToMissingStylePrerequisite(CharacterStats stats, string featName, int depth)
    {
        if (stats == null || featName == null || depth > 6)
            return false;
        CombatStyleData style = stats.RangerCombatStyle;
        if (style == null || !style.HasStyleSelected || !StyleDependentFeats.Contains(featName))
            return false;
        FeatDefinition def = FeatDefinitions.GetFeat(featName);
        if (def == null || def.Prerequisites == null)
            return false;

        for (int i = 0; i < def.Prerequisites.Count; i++)
        {
            FeatPrerequisite p = def.Prerequisites[i];
            if (p == null || p.Type != PrerequisiteType.Feat || string.IsNullOrEmpty(p.ParamString))
                continue;
            string prereq = p.ParamString;
            if (stats.Feats.Contains(prereq))
            {
                if (LosesFeatToMissingStylePrerequisite(stats, prereq, depth + 1))
                    return true;
                continue;
            }
            // Not taken: a style feat the ranger's style grants at his level, which counts only while the style applies.
            if (CombatStyleFeatNames.Contains(prereq)
                && CombatStyleFeats(stats).Contains(prereq)
                && !CombatStyleTreatsAsHavingFeat(stats, prereq))
                return true;
        }
        return false;
    }

    /// <summary>"Archery (Rapid Shot)", "... - inactive: medium or heavy armor", or "None selected".</summary>
    public static string CombatStyleSummary(CharacterStats stats)
    {
        CombatStyleData style = stats != null ? stats.RangerCombatStyle : null;
        if (style == null || !style.HasStyleSelected)
            return "None selected";
        List<string> feats = CombatStyleFeats(stats);
        string text = style.GetStyleName() + (feats.Count > 0 ? " (" + string.Join(", ", feats) + ")" : string.Empty);
        if (feats.Count > 0 && !WearsLightOrNoArmor(stats))
            text += " - inactive in medium or heavy armor";
        return text;
    }

    /// <summary>Selects the combat style once, from 2nd ranger level (PHB p.48).</summary>
    public static bool TryChooseCombatStyle(CombatStyleData style, int rangerLevel, RangerCombatStyle choice, out string reason)
    {
        reason = null;
        if (style == null)
        {
            reason = "no combat style data";
            return false;
        }
        if (rangerLevel < 2)
        {
            reason = "a ranger selects his combat style at 2nd level";
            return false;
        }
        if (style.HasStyleSelected)
        {
            reason = "the combat style is already chosen";
            return false;
        }
        if (choice == RangerCombatStyle.None)
        {
            reason = "choose archery or two-weapon combat";
            return false;
        }
        style.SelectStyle(choice);
        return true;
    }

    // ─────────────────────────────────────────────
    // Automatic features, NPC choices
    // ─────────────────────────────────────────────

    /// <summary>
    /// Track at 1st and Endurance at 3rd ranger level, as bonus feats (PHB p.47-48), by ranger level rather than
    /// character level. Idempotent; called by RangerClass.InitFeats and after every ranger level-up.
    /// </summary>
    public static void GrantAutomaticFeats(CharacterStats stats)
    {
        int level = RangerLevel(stats);
        if (level <= 0)
            return;
        if (!stats.HasOwnFeat("Track"))
            stats.Feats.Add("Track");
        if (level >= 3 && !stats.HasOwnFeat("Endurance"))
            stats.Feats.Add("Endurance");
    }

    /// <summary>
    /// The favored enemies of the DMG p.122 sample NPC ranger, in the order chosen with the bonus each selection raises
    /// (the 5th-level sample: elves +4, humans +2; the 15th-level sample adds magical beasts and fey and has elves +6,
    /// humans +4). Used for an NPC ranger whose definition names no choice.
    /// </summary>
    private static readonly string[][] DmgSampleFavoredEnemySelections =
    {
        new[] { "Humanoid (Elf)", null },
        new[] { "Humanoid (Human)", "Humanoid (Elf)" },
        new[] { "Magical Beast", "Humanoid (Human)" },
        new[] { "Fey", "Humanoid (Elf)" }
    };

    /// <summary>
    /// Gives an NPC ranger its combat style and favored enemies: the definition's choices
    /// (<see cref="NPCDefinition.RangerStyle"/>, <see cref="NPCDefinition.RangerFavoredEnemies"/>), or, where the
    /// definition names none, the DMG p.122 sample NPC ranger's (archery; elves, then humans with +2 on elves, then
    /// magical beasts and fey). A definition's favored enemies are taken as listed (their bonuses included).
    /// </summary>
    public static void ApplyNpcChoices(CharacterStats stats, NPCDefinition def)
    {
        int level = RangerLevel(stats);
        if (level <= 0)
            return;

        RangerCombatStyle styleChoice = def != null && def.RangerStyle != RangerCombatStyle.None
            ? def.RangerStyle
            : RangerCombatStyle.Archery;
        stats.RangerCombatStyle = level >= 2 ? CombatStyleData.Create(styleChoice) : new CombatStyleData();

        var favored = new FavoredEnemyData();
        if (def != null && def.RangerFavoredEnemies != null && def.RangerFavoredEnemies.Count > 0)
        {
            for (int i = 0; i < def.RangerFavoredEnemies.Count; i++)
            {
                FavoredEnemyEntry entry = def.RangerFavoredEnemies[i];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.CreatureType))
                    favored.Enemies.Add(new FavoredEnemyEntry(CanonicalFavoredEnemyType(entry.CreatureType) ?? entry.CreatureType.Trim(), entry.Bonus));
            }
        }
        else
        {
            int due = FavoredEnemySelectionsDue(level);
            for (int i = 0; i < due && i < DmgSampleFavoredEnemySelections.Length; i++)
                TryApplyFavoredEnemySelection(favored, level, DmgSampleFavoredEnemySelections[i][0], DmgSampleFavoredEnemySelections[i][1], out _);
        }
        stats.RangerFavoredEnemies = favored;
    }

    /// <summary>
    /// Gives a PC ranger the choices made at creation (copies, so the record is not shared). A choice not yet made stays
    /// pending for the level-up flow (CharacterCreationManager).
    /// </summary>
    public static void ApplyCreationChoices(CharacterStats stats, FavoredEnemyData favored, RangerCombatStyle style)
    {
        if (RangerLevel(stats) <= 0)
            return;
        stats.RangerFavoredEnemies = favored != null ? favored.Clone() : new FavoredEnemyData();
        stats.RangerCombatStyle = CombatStyleData.Create(style);
    }

    /// <summary>Whether a ranger PC still has a favored enemy selection or the combat style to choose.</summary>
    public static bool HasPendingChoices(CharacterStats stats)
    {
        int level = RangerLevel(stats);
        if (level <= 0)
            return false;
        return PendingFavoredEnemySelections(stats.RangerFavoredEnemies, level) > 0
            || NeedsCombatStyleChoice(stats.RangerCombatStyle, level);
    }
}

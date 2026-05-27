using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DND35e.Identifiers;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Metadata for one summonable creature choice in a Summon Monster spell list.
/// Keeps rules/data concerns out of GameManager.
/// </summary>
public sealed class SummonMonsterOption
{
    public string DisplayName;
    public string NpcDefinitionId;
    public string TemplateId; // "celestial", "fiendish", or null
    public bool ClericOnly;
    public SummonAlignmentRequirement AlignmentRequirement = SummonAlignmentRequirement.Any;
    public Alignment SummonedCreatureAlignment = Alignment.None;

    public bool IsAvailableTo(CharacterStats caster)
    {
        if (caster == null)
            return false;

        if (ClericOnly && !caster.IsCleric)
            return false;

        // D&D 3.5e: Summon Monster alignment restrictions apply to clerics.
        // Arcane casters (e.g., wizard/sorcerer) are unrestricted and can summon
        // any listed creature regardless of the caster's own alignment.
        if (!caster.IsCleric)
            return true;

        // Cleric rule: summon option must be within one alignment step.
        // This exactly matches the 3x3 alignment grid behavior and includes
        // the True Neutral special case (within one step of every alignment).
        if (SummonedCreatureAlignment != Alignment.None)
            return AlignmentHelper.IsWithinOneStep(caster.CharacterAlignment, SummonedCreatureAlignment);

        // Backward-compatible fallback for entries that do not yet carry
        // explicit summoned creature alignment metadata.
        bool casterIsNeutralOnGoodEvilAxis = AlignmentHelper.IsNeutralGE(caster.CharacterAlignment);
        switch (AlignmentRequirement)
        {
            case SummonAlignmentRequirement.Good:
                return AlignmentHelper.IsGood(caster.CharacterAlignment) || casterIsNeutralOnGoodEvilAxis;
            case SummonAlignmentRequirement.Evil:
                return AlignmentHelper.IsEvil(caster.CharacterAlignment) || casterIsNeutralOnGoodEvilAxis;
            default:
                return true;
        }
    }

    public string BuildUiLabel()
    {
        if (string.IsNullOrEmpty(TemplateId))
            return DisplayName;

        string templateTag = TemplateId.Equals("celestial", StringComparison.OrdinalIgnoreCase)
            ? "Celestial"
            : TemplateId.Equals("fiendish", StringComparison.OrdinalIgnoreCase)
                ? "Fiendish"
                : TemplateId;

        return $"{DisplayName} ({templateTag})";
    }
}

public enum SummonAlignmentRequirement
{
    Any,
    Good,
    Evil
}

public sealed class SummonCreatureCountInfo
{
    public int SpellLevel;
    public int SelectedListLevel;
    public int LevelDifference;
    public int MinCount;
    public int MaxCount;
    public string DiceExpression;
    public string RangeText;
    public bool RequiresRoll;
}

/// <summary>
/// Centralized Summon Monster option table + filtering helpers.
/// This supports class/alignment restrictions and keeps summon choice lists deterministic.
/// </summary>
public static class SummonMonsterLists
{
    private static readonly Regex SummonMonsterLevelRegex =
        new Regex(@"summon_monster_(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SummonNaturesAllyLevelRegex =
        new Regex(@"summon_natures_ally_(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<int, List<SummonMonsterOption>> OptionsByLevel =
        new Dictionary<int, List<SummonMonsterOption>>
        {
            { 1, GetSummonMonsterIOptions() },
            { 2, GetSummonMonsterIIOptions() },
            { 3, GetSummonMonsterIIIOptions() },
            { 4, GetSummonMonsterIVOptions() },
            // Higher level list definitions can be added incrementally.
            // Keeping empty entries allows UI level selection rules to stay consistent.
            { 5, new List<SummonMonsterOption>() }
        };

    // ── Summon Nature's Ally creature tables (D&D 3.5e SRD/PHB p.288) ──
    // SNA summons regular animals (no celestial/fiendish template) plus some
    // fey and elementals at higher levels. No alignment restrictions.
    private static readonly Dictionary<int, List<SummonMonsterOption>> NaturesAllyOptionsByLevel =
        new Dictionary<int, List<SummonMonsterOption>>
        {
            { 1, GetSummonNaturesAllyIOptions() },
            { 2, GetSummonNaturesAllyIIOptions() },
            { 3, GetSummonNaturesAllyIIIOptions() },
            { 4, GetSummonNaturesAllyIVOptions() }
        };

    /// <summary>
    /// Summon Monster I creature list (D&D 3.5e SRD)
    /// </summary>
    private static List<SummonMonsterOption> GetSummonMonsterIOptions()
    {
        return new List<SummonMonsterOption>
        {
            // Celestial (Good)
            new SummonMonsterOption { DisplayName = "Dog", NpcDefinitionId = "dog", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.LawfulGood },
            new SummonMonsterOption { DisplayName = "Owl", NpcDefinitionId = "owl", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.LawfulGood },
            new SummonMonsterOption { DisplayName = "Giant Fire Beetle", NpcDefinitionId = "giant_fire_beetle", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.NeutralGood },
            new SummonMonsterOption { DisplayName = "Badger", NpcDefinitionId = "badger", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.ChaoticGood },
            new SummonMonsterOption { DisplayName = "Monkey", NpcDefinitionId = "monkey", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.ChaoticGood },

            // Fiendish (Evil)
            new SummonMonsterOption { DisplayName = "Dire Rat", NpcDefinitionId = "dire_rat", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.LawfulEvil },
            new SummonMonsterOption { DisplayName = "Raven", NpcDefinitionId = "raven", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.LawfulEvil },
            new SummonMonsterOption { DisplayName = "Monstrous Centipede, Medium", NpcDefinitionId = "monstrous_centipede_medium", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.NeutralEvil },
            new SummonMonsterOption { DisplayName = "Monstrous Scorpion, Small", NpcDefinitionId = "monstrous_scorpion_small", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.NeutralEvil },
            new SummonMonsterOption { DisplayName = "Hawk", NpcDefinitionId = "hawk", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil },
            new SummonMonsterOption { DisplayName = "Monstrous Spider, Small", NpcDefinitionId = "monstrous_spider_small", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil },
            new SummonMonsterOption { DisplayName = "Snake, Small Viper", NpcDefinitionId = "viper_small", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil }
        };
    }

    /// <summary>
    /// Summon Monster II creature list (D&D 3.5e SRD)
    /// </summary>
    private static List<SummonMonsterOption> GetSummonMonsterIIOptions()
    {
        return new List<SummonMonsterOption>
        {
            // Celestial (Good)
            new SummonMonsterOption { DisplayName = "Giant Bee", NpcDefinitionId = "giant_bee", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.LawfulGood },
            new SummonMonsterOption { DisplayName = "Giant Bombardier Beetle", NpcDefinitionId = "giant_bombardier_beetle", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.NeutralGood },
            new SummonMonsterOption { DisplayName = "Riding Dog", NpcDefinitionId = "riding_dog", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.NeutralGood },
            new SummonMonsterOption { DisplayName = "Eagle", NpcDefinitionId = "eagle", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.ChaoticGood },

            // Evil (Lawful/Neutral/Chaotic)
            new SummonMonsterOption { DisplayName = "Lemure", NpcDefinitionId = "lemure", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.LawfulEvil },
            new SummonMonsterOption { DisplayName = "Wolf", NpcDefinitionId = "wolf", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.LawfulEvil },
            new SummonMonsterOption { DisplayName = "Monstrous Centipede, Large", NpcDefinitionId = "monstrous_centipede_large", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.NeutralEvil },
            new SummonMonsterOption { DisplayName = "Monstrous Scorpion, Large", NpcDefinitionId = "monstrous_scorpion_large", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.NeutralEvil },
            new SummonMonsterOption { DisplayName = "Monstrous Spider, Medium", NpcDefinitionId = "monstrous_spider_medium", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil },
            new SummonMonsterOption { DisplayName = "Snake, Medium Viper", NpcDefinitionId = "viper_medium", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil }
        };
    }

    /// <summary>
    /// Summon Monster III creature list (D&D 3.5e SRD/MM)
    /// All 19 creatures with proper celestial/fiendish templates and alignment restrictions.
    /// </summary>
    private static List<SummonMonsterOption> GetSummonMonsterIIIOptions()
    {
        return new List<SummonMonsterOption>
        {
            // Celestial (Good) — Animals & Magical Beasts
            new SummonMonsterOption { DisplayName = "Black Bear", NpcDefinitionId = "black_bear", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.LawfulGood },
            new SummonMonsterOption { DisplayName = "Bison", NpcDefinitionId = "bison", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.NeutralGood },
            new SummonMonsterOption { DisplayName = "Dire Badger", NpcDefinitionId = "dire_badger", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.ChaoticGood },
            new SummonMonsterOption { DisplayName = "Hippogriff", NpcDefinitionId = "hippogriff", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.ChaoticGood },

            // Fiendish (Evil) — Lawful Evil
            new SummonMonsterOption { DisplayName = "Ape", NpcDefinitionId = "ape", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.LawfulEvil },
            new SummonMonsterOption { DisplayName = "Dire Weasel", NpcDefinitionId = "dire_weasel", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.LawfulEvil },
            new SummonMonsterOption { DisplayName = "Constrictor Snake", NpcDefinitionId = "constrictor_snake", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.LawfulEvil },

            // Fiendish (Evil) — Neutral Evil
            new SummonMonsterOption { DisplayName = "Boar", NpcDefinitionId = "boar", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.NeutralEvil },
            new SummonMonsterOption { DisplayName = "Dire Bat", NpcDefinitionId = "dire_bat", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.NeutralEvil },
            new SummonMonsterOption { DisplayName = "Huge Monstrous Centipede", NpcDefinitionId = "huge_monstrous_centipede", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.NeutralEvil },

            // Fiendish (Evil) — Chaotic Evil
            new SummonMonsterOption { DisplayName = "Crocodile", NpcDefinitionId = "crocodile", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil },
            new SummonMonsterOption { DisplayName = "Large Viper", NpcDefinitionId = "large_viper", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil },
            new SummonMonsterOption { DisplayName = "Wolverine", NpcDefinitionId = "wolverine", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil },

            // Elementals — Neutral, available to all casters
            new SummonMonsterOption { DisplayName = "Small Air Elemental", NpcDefinitionId = "small_air_elemental" },
            new SummonMonsterOption { DisplayName = "Small Earth Elemental", NpcDefinitionId = "small_earth_elemental" },
            new SummonMonsterOption { DisplayName = "Small Fire Elemental", NpcDefinitionId = "small_fire_elemental" },
            new SummonMonsterOption { DisplayName = "Small Water Elemental", NpcDefinitionId = "small_water_elemental" },

            // Evil outsiders — Evil alignment only
            new SummonMonsterOption { DisplayName = "Hell Hound", NpcDefinitionId = "hell_hound", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.LawfulEvil },
            new SummonMonsterOption { DisplayName = "Dretch (Demon)", NpcDefinitionId = "dretch", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil }
        };
    }

    /// <summary>
    /// Summon Monster IV creature list (D&D 3.5e SRD/MM)
    /// All 12 creature entries (plus 10 mephit sub-types) with proper templates and alignment restrictions.
    /// </summary>
    private static List<SummonMonsterOption> GetSummonMonsterIVOptions()
    {
        return new List<SummonMonsterOption>
        {
            // ── Good-aligned creatures ──

            // Lantern Archon (LG) — base outsider, no template needed
            new SummonMonsterOption { DisplayName = "Lantern Archon", NpcDefinitionId = "lantern_archon", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.LawfulGood },

            // Celestial Giant Owl (LG)
            new SummonMonsterOption { DisplayName = "Giant Owl", NpcDefinitionId = "giant_owl", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.LawfulGood },

            // Celestial Giant Eagle (CG)
            new SummonMonsterOption { DisplayName = "Giant Eagle", NpcDefinitionId = "giant_eagle", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.ChaoticGood },

            // Celestial Lion (CG)
            new SummonMonsterOption { DisplayName = "Lion", NpcDefinitionId = "lion", TemplateId = "celestial", AlignmentRequirement = SummonAlignmentRequirement.Good, SummonedCreatureAlignment = Alignment.ChaoticGood },

            // ── Neutral creatures — available to all casters ──

            // Mephit (any) (N) — all 10 mephit sub-types
            new SummonMonsterOption { DisplayName = "Air Mephit", NpcDefinitionId = "air_mephit" },
            new SummonMonsterOption { DisplayName = "Dust Mephit", NpcDefinitionId = "dust_mephit" },
            new SummonMonsterOption { DisplayName = "Earth Mephit", NpcDefinitionId = "earth_mephit" },
            new SummonMonsterOption { DisplayName = "Fire Mephit", NpcDefinitionId = "fire_mephit" },
            new SummonMonsterOption { DisplayName = "Ice Mephit", NpcDefinitionId = "ice_mephit" },
            new SummonMonsterOption { DisplayName = "Magma Mephit", NpcDefinitionId = "magma_mephit" },
            new SummonMonsterOption { DisplayName = "Ooze Mephit", NpcDefinitionId = "ooze_mephit" },
            new SummonMonsterOption { DisplayName = "Salt Mephit", NpcDefinitionId = "salt_mephit" },
            new SummonMonsterOption { DisplayName = "Steam Mephit", NpcDefinitionId = "steam_mephit" },
            new SummonMonsterOption { DisplayName = "Water Mephit", NpcDefinitionId = "water_mephit" },

            // ── Evil-aligned creatures ──

            // Fiendish Dire Wolf (LE)
            new SummonMonsterOption { DisplayName = "Dire Wolf", NpcDefinitionId = "dire_wolf", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.LawfulEvil },

            // Fiendish Giant Wasp (LE)
            new SummonMonsterOption { DisplayName = "Giant Wasp", NpcDefinitionId = "giant_wasp", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.LawfulEvil },

            // Fiendish Giant Praying Mantis (NE)
            new SummonMonsterOption { DisplayName = "Giant Praying Mantis", NpcDefinitionId = "giant_praying_mantis", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.NeutralEvil },

            // Yeth Hound (NE) — base outsider, no template needed
            new SummonMonsterOption { DisplayName = "Yeth Hound", NpcDefinitionId = "yeth_hound", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.NeutralEvil },

            // Fiendish Monstrous Spider, Large (CE)
            new SummonMonsterOption { DisplayName = "Monstrous Spider, Large", NpcDefinitionId = "monstrous_spider_large", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil },

            // Fiendish Snake, Huge Viper (CE)
            new SummonMonsterOption { DisplayName = "Snake, Huge Viper", NpcDefinitionId = "viper_huge", TemplateId = "fiendish", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil },

            // Howler (CE) — base outsider, no template needed
            new SummonMonsterOption { DisplayName = "Howler", NpcDefinitionId = "howler", AlignmentRequirement = SummonAlignmentRequirement.Evil, SummonedCreatureAlignment = Alignment.ChaoticEvil }
        };
    }

    public static List<SummonMonsterOption> GetFilteredOptions(string spellId, CharacterStats caster)
    {
        int level = GetSummonMonsterSpellLevel(spellId);
        bool isSNA = IsSummonNaturesAllySpell(spellId);
        return GetFilteredOptionsForListLevel(level, caster, isSNA);
    }

    public static List<SummonMonsterOption> GetFilteredOptionsForListLevel(int listLevel, CharacterStats caster, bool isNaturesAlly = false)
    {
        var lookupDict = isNaturesAlly ? NaturesAllyOptionsByLevel : OptionsByLevel;

        if (listLevel <= 0 || !lookupDict.TryGetValue(listLevel, out var rawOptions) || rawOptions == null)
            return new List<SummonMonsterOption>();

        return rawOptions
            .Where(o => o != null && o.IsAvailableTo(caster))
            .Select(CloneOption)
            .ToList();
    }

    public static List<int> GetAvailableListLevelsForSpell(string spellId)
    {
        int spellLevel = GetSummonMonsterSpellLevel(spellId);
        if (spellLevel <= 0)
            return new List<int>();

        var levels = new List<int>(spellLevel);
        for (int level = 1; level <= spellLevel; level++)
            levels.Add(level);

        return levels;
    }

    public static string GetSummonRestrictionHint(CharacterStats caster)
    {
        if (caster == null)
            return string.Empty;

        if (caster.IsCleric)
            return "As a cleric, you can only summon creatures within one step of your alignment.";

        if (caster.IsWizard)
            return "As a wizard, you can summon any creature regardless of alignment.";

        return "Alignment restrictions apply only to clerics. Your class can summon any listed creature.";
    }

    public static int GetSummonMonsterSpellLevel(string spellId)
    {
        if (string.IsNullOrWhiteSpace(spellId))
            return 0;

        Match match = SummonMonsterLevelRegex.Match(spellId);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int parsedLevel))
            return Mathf.Max(0, parsedLevel);

        // Also check Summon Nature's Ally pattern
        Match snaMatch = SummonNaturesAllyLevelRegex.Match(spellId);
        if (snaMatch.Success && int.TryParse(snaMatch.Groups[1].Value, out int snaLevel))
            return Mathf.Max(0, snaLevel);

        return 0;
    }

    /// <summary>
    /// Returns true if the spell ID matches a Summon Nature's Ally pattern.
    /// </summary>
    public static bool IsSummonNaturesAllySpell(string spellId)
    {
        if (string.IsNullOrWhiteSpace(spellId))
            return false;
        return SummonNaturesAllyLevelRegex.IsMatch(spellId);
    }

    public static string ToRomanLevel(int level)
    {
        switch (level)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            case 6: return "VI";
            case 7: return "VII";
            case 8: return "VIII";
            case 9: return "IX";
            default: return level.ToString();
        }
    }

    public static SummonCreatureCountInfo GetCreatureCountInfo(int spellLevel, int selectedListLevel)
    {
        int levelDifference = Mathf.Max(0, spellLevel - selectedListLevel);

        var info = new SummonCreatureCountInfo
        {
            SpellLevel = spellLevel,
            SelectedListLevel = selectedListLevel,
            LevelDifference = levelDifference
        };

        if (levelDifference <= 0)
        {
            info.MinCount = 1;
            info.MaxCount = 1;
            info.DiceExpression = "1";
            info.RangeText = "1 creature";
            info.RequiresRoll = false;
            return info;
        }

        if (levelDifference == 1)
        {
            info.MinCount = 1;
            info.MaxCount = 3;
            info.DiceExpression = "1d3";
            info.RangeText = "1d3 creatures (1-3)";
            info.RequiresRoll = true;
            return info;
        }

        info.MinCount = 2;
        info.MaxCount = 5;
        info.DiceExpression = "1d4+1";
        info.RangeText = "1d4+1 creatures (2-5)";
        info.RequiresRoll = true;
        return info;
    }

    public static int CalculateCreatureCount(int spellLevel, int selectedListLevel)
    {
        int levelDifference = spellLevel - selectedListLevel;

        if (levelDifference <= 0)
            return 1;

        if (levelDifference == 1)
            return UnityEngine.Random.Range(1, 4); // 1d3 = 1-3

        return DiceRoller.D4() + 1; // 1d4+1 = 2-5
    }

    /// <summary>
    /// Summon Monster swarm detection helper used by runtime control logic.
    /// This intentionally combines list metadata + NPC database metadata so future
    /// list entries (for example "Celestial Centipede Swarm") are auto-detected
    /// without requiring manual per-entry wiring.
    /// </summary>
    public static bool IsSwarmOption(SummonMonsterOption option)
    {
        if (option == null)
            return false;

        if (ContainsSwarmText(option.DisplayName) || ContainsSwarmText(option.NpcDefinitionId))
            return true;

        NPCDefinition def = NPCDatabase.Get(option.NpcDefinitionId);
        if (def == null)
            return false;

        if (def.IsSwarm || (def.SwarmTraits != null && def.SwarmTraits.IsSwarm))
            return true;

        if (ContainsSwarmText(def.Name) || ContainsSwarmText(def.CreatureType))
            return true;

        if (def.CreatureTags != null)
        {
            for (int i = 0; i < def.CreatureTags.Count; i++)
            {
                if (ContainsSwarmText(def.CreatureTags[i]))
                    return true;
            }
        }

        return false;
    }

    private static bool ContainsSwarmText(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.IndexOf("swarm", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static SummonMonsterOption CloneOption(SummonMonsterOption source)
    {
        return new SummonMonsterOption
        {
            DisplayName = source.DisplayName,
            NpcDefinitionId = source.NpcDefinitionId,
            TemplateId = source.TemplateId,
            ClericOnly = source.ClericOnly,
            AlignmentRequirement = source.AlignmentRequirement,
            SummonedCreatureAlignment = source.SummonedCreatureAlignment
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  SUMMON NATURE'S ALLY creature tables (D&D 3.5e PHB p.288-289)
    //  Animals are summoned as-is (no celestial/fiendish template).
    //  No alignment restrictions apply.
    //  Reuses existing NPCDatabase creature definitions where available.
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Summon Nature's Ally I — 1st-level druid/ranger summons (PHB p.288).
    /// Animals are summoned as-is (no celestial/fiendish template).
    /// </summary>
    private static List<SummonMonsterOption> GetSummonNaturesAllyIOptions()
    {
        return new List<SummonMonsterOption>
        {
            new SummonMonsterOption { DisplayName = "Dire Rat", NpcDefinitionId = "dire_rat" },
            new SummonMonsterOption { DisplayName = "Eagle", NpcDefinitionId = "eagle" },
            new SummonMonsterOption { DisplayName = "Monkey", NpcDefinitionId = "monkey" },
            new SummonMonsterOption { DisplayName = "Octopus", NpcDefinitionId = "octopus" },              // Aquatic
            new SummonMonsterOption { DisplayName = "Owl", NpcDefinitionId = "owl" },                       // Note: owl alias uses eagle stats — fix in Phase 6
            // MISSING: Porpoise — needs NPCDatabase entry (aquatic, swim-only)
            new SummonMonsterOption { DisplayName = "Snake, Small Viper", NpcDefinitionId = "viper_small" },
            new SummonMonsterOption { DisplayName = "Wolf", NpcDefinitionId = "wolf" },                     // Alias → wolf_pack_hunter
        };
    }

    /// <summary>
    /// Summon Nature's Ally II — 2nd-level druid summons (PHB p.288).
    /// Includes first elemental options (Small elementals).
    /// </summary>
    private static List<SummonMonsterOption> GetSummonNaturesAllyIIOptions()
    {
        return new List<SummonMonsterOption>
        {
            new SummonMonsterOption { DisplayName = "Bear, Black", NpcDefinitionId = "black_bear" },
            new SummonMonsterOption { DisplayName = "Crocodile", NpcDefinitionId = "crocodile" },
            new SummonMonsterOption { DisplayName = "Dire Badger", NpcDefinitionId = "dire_badger" },
            new SummonMonsterOption { DisplayName = "Dire Bat", NpcDefinitionId = "dire_bat" },
            new SummonMonsterOption { DisplayName = "Elemental, Small Air", NpcDefinitionId = "small_air_elemental" },
            new SummonMonsterOption { DisplayName = "Elemental, Small Earth", NpcDefinitionId = "small_earth_elemental" },
            new SummonMonsterOption { DisplayName = "Elemental, Small Fire", NpcDefinitionId = "small_fire_elemental" },
            new SummonMonsterOption { DisplayName = "Elemental, Small Water", NpcDefinitionId = "small_water_elemental" },
            new SummonMonsterOption { DisplayName = "Hippogriff", NpcDefinitionId = "hippogriff" },
            // MISSING: Shark, Medium — needs NPCDatabase entry (aquatic, swim-only)
            new SummonMonsterOption { DisplayName = "Snake, Medium Viper", NpcDefinitionId = "viper_medium" },
            // MISSING: Squid — needs NPCDatabase entry (aquatic, swim-only)
            new SummonMonsterOption { DisplayName = "Wolverine", NpcDefinitionId = "wolverine" },
        };
    }

    /// <summary>
    /// Summon Nature's Ally III — 3rd-level druid summons (PHB p.288).
    /// Includes fey (Satyr) and elemental creatures (Thoqqua).
    /// Giant Eagle and Giant Owl have alignment restrictions (NG only).
    /// </summary>
    private static List<SummonMonsterOption> GetSummonNaturesAllyIIIOptions()
    {
        return new List<SummonMonsterOption>
        {
            new SummonMonsterOption { DisplayName = "Ape", NpcDefinitionId = "ape" },
            new SummonMonsterOption { DisplayName = "Dire Weasel", NpcDefinitionId = "dire_weasel" },
            new SummonMonsterOption { DisplayName = "Dire Wolf", NpcDefinitionId = "dire_wolf" },
            new SummonMonsterOption { DisplayName = "Eagle, Giant", NpcDefinitionId = "giant_eagle" },      // PHB: NG alignment restriction
            new SummonMonsterOption { DisplayName = "Lion", NpcDefinitionId = "lion" },
            new SummonMonsterOption { DisplayName = "Owl, Giant", NpcDefinitionId = "giant_owl" },          // PHB: NG alignment restriction
            new SummonMonsterOption { DisplayName = "Satyr (without pipes)", NpcDefinitionId = "satyr" },
            new SummonMonsterOption { DisplayName = "Shark, Large", NpcDefinitionId = "large_shark" },      // Aquatic
            new SummonMonsterOption { DisplayName = "Snake, Constrictor", NpcDefinitionId = "constrictor_snake" },
            new SummonMonsterOption { DisplayName = "Snake, Large Viper", NpcDefinitionId = "viper_large" },
            new SummonMonsterOption { DisplayName = "Thoqqua", NpcDefinitionId = "thoqqua" },
        };
    }

    /// <summary>
    /// Summon Nature's Ally IV — 4th-level druid summons (PHB p.289).
    /// Includes Medium elementals, outsiders, and magical beasts.
    /// Unicorn has CG alignment restriction.
    /// </summary>
    private static List<SummonMonsterOption> GetSummonNaturesAllyIVOptions()
    {
        return new List<SummonMonsterOption>
        {
            new SummonMonsterOption { DisplayName = "Arrowhawk, Juvenile", NpcDefinitionId = "arrowhawk_juvenile" },
            new SummonMonsterOption { DisplayName = "Bear, Brown (Grizzly)", NpcDefinitionId = "brown_bear" },
            new SummonMonsterOption { DisplayName = "Crocodile, Giant", NpcDefinitionId = "giant_crocodile" },
            new SummonMonsterOption { DisplayName = "Deinonychus", NpcDefinitionId = "deinonychus" },
            new SummonMonsterOption { DisplayName = "Dire Ape", NpcDefinitionId = "dire_ape" },
            new SummonMonsterOption { DisplayName = "Dire Boar", NpcDefinitionId = "dire_boar" },
            new SummonMonsterOption { DisplayName = "Dire Wolverine", NpcDefinitionId = "dire_wolverine" },
            new SummonMonsterOption { DisplayName = "Elemental, Medium Air", NpcDefinitionId = "medium_air_elemental" },
            new SummonMonsterOption { DisplayName = "Elemental, Medium Earth", NpcDefinitionId = "medium_earth_elemental" },
            new SummonMonsterOption { DisplayName = "Elemental, Medium Fire", NpcDefinitionId = "medium_fire_elemental" },
            new SummonMonsterOption { DisplayName = "Elemental, Medium Water", NpcDefinitionId = "medium_water_elemental" },
            new SummonMonsterOption { DisplayName = "Salamander, Flamebrother", NpcDefinitionId = "flamebrother_salamander" },
            // MISSING: Sea Cat — needs NPCDatabase entry (Magical Beast [Aquatic], CR 4)
            // MISSING: Shark, Huge — needs NPCDatabase entry (Animal [Aquatic], CR 4)
            new SummonMonsterOption { DisplayName = "Snake, Huge Viper", NpcDefinitionId = "viper_huge" },
            new SummonMonsterOption { DisplayName = "Tiger", NpcDefinitionId = "tiger" },
            // MISSING: Tojanida, Juvenile — needs NPCDatabase entry (Outsider [Water], CR 3, aquatic)
            new SummonMonsterOption { DisplayName = "Unicorn", NpcDefinitionId = "unicorn", SummonedCreatureAlignment = Alignment.ChaoticGood },  // CG alignment restriction
            new SummonMonsterOption { DisplayName = "Xorn, Minor", NpcDefinitionId = "minor_xorn" },
        };
    }
}

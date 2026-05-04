using System;
using System.Collections.Generic;
using System.Linq;
using DND35e.Identifiers;

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

/// <summary>
/// Centralized Summon Monster option table + filtering helpers.
/// This supports class/alignment restrictions and keeps summon choice lists deterministic.
/// </summary>
public static class SummonMonsterLists
{
    private static readonly Dictionary<int, List<SummonMonsterOption>> OptionsByLevel =
        new Dictionary<int, List<SummonMonsterOption>>
        {
            { 1, GetSummonMonsterIOptions() },
            { 2, GetSummonMonsterIIOptions() },
            {
                3, new List<SummonMonsterOption>
                {
                    new SummonMonsterOption { DisplayName = "Black Bear", NpcDefinitionId = "black_bear", TemplateId = "celestial", ClericOnly = true, AlignmentRequirement = SummonAlignmentRequirement.Good },
                    new SummonMonsterOption { DisplayName = "Dire Wolf", NpcDefinitionId = "dire_wolf", TemplateId = "fiendish", ClericOnly = true, AlignmentRequirement = SummonAlignmentRequirement.Evil },
                    new SummonMonsterOption { DisplayName = "Ape", NpcDefinitionId = "ape" },
                    new SummonMonsterOption { DisplayName = "Dire Badger", NpcDefinitionId = "dire_badger" },
                    new SummonMonsterOption { DisplayName = "Large Shark", NpcDefinitionId = "large_shark" },
                    new SummonMonsterOption { DisplayName = "Constrictor Snake", NpcDefinitionId = "constrictor_snake" }
                }
            }
        };

    /// <summary>
    /// Summon Monster I creature list (D&D 3.5e SRD)
    ///
    /// Each creature has:
    /// - Base creature ID (from NPCDatabase)
    /// - Template (Celestial for good, Fiendish for evil)
    /// - Alignment (restricts which casters can summon)
    ///
    /// Good Casters (LG, NG, CG): Can summon Celestial creatures only
    /// Evil Casters (LE, NE, CE): Can summon Fiendish creatures only
    /// Neutral Casters (LN, N, CN): Can choose from either list
    ///
    /// SRD Reference: Summon Monster I spell description
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
    ///
    /// Alignment buckets are intentionally explicit to keep the summon picker
    /// deterministic and easy to review against the SRD table.
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

    public static List<SummonMonsterOption> GetFilteredOptions(string spellId, CharacterStats caster)
    {
        int level = GetSummonMonsterSpellLevel(spellId);
        if (level <= 0 || !OptionsByLevel.TryGetValue(level, out var rawOptions) || rawOptions == null)
            return new List<SummonMonsterOption>();

        return rawOptions
            .Where(o => o != null && o.IsAvailableTo(caster))
            .Select(CloneOption)
            .ToList();
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

        if (spellId.StartsWith(SpellNames.SUMMON_MONSTER_1, StringComparison.Ordinal)) return 1;
        if (spellId.StartsWith(SpellNames.SUMMON_MONSTER_2, StringComparison.Ordinal)) return 2;
        if (spellId.StartsWith("summon_monster_3", StringComparison.Ordinal)) return 3;

        return 0;
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
}

using System;
using System.Collections.Generic;
using System.Linq;

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

    public bool IsAvailableTo(CharacterStats caster)
    {
        if (caster == null)
            return false;

        if (ClericOnly && !caster.IsCleric)
            return false;

        switch (AlignmentRequirement)
        {
            case SummonAlignmentRequirement.Good:
                return AlignmentHelper.IsGood(caster.CharacterAlignment);
            case SummonAlignmentRequirement.Evil:
                return AlignmentHelper.IsEvil(caster.CharacterAlignment);
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
            {
                1, new List<SummonMonsterOption>
                {
                    new SummonMonsterOption { DisplayName = "Dog", NpcDefinitionId = "summon_dog", TemplateId = "celestial", ClericOnly = true, AlignmentRequirement = SummonAlignmentRequirement.Good },
                    new SummonMonsterOption { DisplayName = "Wolf", NpcDefinitionId = "wolf_pack_hunter", TemplateId = "fiendish", ClericOnly = true, AlignmentRequirement = SummonAlignmentRequirement.Evil },
                    new SummonMonsterOption { DisplayName = "Dire Rat", NpcDefinitionId = "summon_dire_rat" },
                    new SummonMonsterOption { DisplayName = "Eagle", NpcDefinitionId = "summon_eagle" },
                    new SummonMonsterOption { DisplayName = "Octopus", NpcDefinitionId = "summon_octopus" },
                    new SummonMonsterOption { DisplayName = "Small Viper", NpcDefinitionId = "summon_small_viper" }
                }
            },
            {
                2, new List<SummonMonsterOption>
                {
                    new SummonMonsterOption { DisplayName = "Eagle", NpcDefinitionId = "summon_eagle", TemplateId = "celestial", ClericOnly = true, AlignmentRequirement = SummonAlignmentRequirement.Good },
                    new SummonMonsterOption { DisplayName = "Dire Bat", NpcDefinitionId = "summon_dire_bat", TemplateId = "fiendish", ClericOnly = true, AlignmentRequirement = SummonAlignmentRequirement.Evil },
                    new SummonMonsterOption { DisplayName = "Small Air Elemental", NpcDefinitionId = "summon_small_air_elemental" },
                    new SummonMonsterOption { DisplayName = "Small Fire Elemental", NpcDefinitionId = "summon_small_fire_elemental" },
                    new SummonMonsterOption { DisplayName = "Wolf", NpcDefinitionId = "wolf_pack_hunter" },
                    new SummonMonsterOption { DisplayName = "Crocodile", NpcDefinitionId = "summon_crocodile" }
                }
            },
            {
                3, new List<SummonMonsterOption>
                {
                    new SummonMonsterOption { DisplayName = "Black Bear", NpcDefinitionId = "summon_black_bear", TemplateId = "celestial", ClericOnly = true, AlignmentRequirement = SummonAlignmentRequirement.Good },
                    new SummonMonsterOption { DisplayName = "Dire Wolf", NpcDefinitionId = "dire_wolf", TemplateId = "fiendish", ClericOnly = true, AlignmentRequirement = SummonAlignmentRequirement.Evil },
                    new SummonMonsterOption { DisplayName = "Ape", NpcDefinitionId = "summon_ape" },
                    new SummonMonsterOption { DisplayName = "Dire Badger", NpcDefinitionId = "summon_dire_badger" },
                    new SummonMonsterOption { DisplayName = "Large Shark", NpcDefinitionId = "summon_large_shark" },
                    new SummonMonsterOption { DisplayName = "Constrictor Snake", NpcDefinitionId = "summon_constrictor_snake" }
                }
            }
        };

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

    public static int GetSummonMonsterSpellLevel(string spellId)
    {
        if (string.IsNullOrWhiteSpace(spellId))
            return 0;

        if (spellId.StartsWith("summon_monster_1", StringComparison.Ordinal)) return 1;
        if (spellId.StartsWith("summon_monster_2", StringComparison.Ordinal)) return 2;
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
            AlignmentRequirement = source.AlignmentRequirement
        };
    }
}

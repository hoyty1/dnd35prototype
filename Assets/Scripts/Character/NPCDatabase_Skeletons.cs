using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skeleton template creature registrations and encounter presets.
/// Uses SkeletonTemplate.Apply() and SkeletonFactory to generate
/// pre-defined skeleton variants from base creature blueprints.
///
/// D&D 3.5e Monster Manual p.226.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Skeletons()
    {
        // ── Standard skeleton variants (MM p.226) ──
        Register(SkeletonFactory.HumanWarriorSkeleton());
        Register(SkeletonFactory.WolfSkeleton());
        Register(SkeletonFactory.OwlbearSkeleton());
        Register(SkeletonFactory.MinotaurSkeleton());
        Register(SkeletonFactory.MegaraptorSkeleton());
        Register(SkeletonFactory.HorseSkeleton());
        Register(SkeletonFactory.TrollSkeleton());

        Debug.Log("[NPCDatabase] Registered 7 skeleton template variants.");
    }

    /// <summary>
    /// Encounter presets featuring skeleton template creatures.
    /// </summary>
    public static List<EncounterPreset> GetSkeletonEncounterPresets()
    {
        return new List<EncounterPreset>
        {
            new EncounterPreset(
                "skeleton_horde",
                "💀 Skeleton Horde",
                "A mix of human warrior skeletons and a wolf skeleton. Classic low-level dungeon encounter (EL 2).",
                new List<string> { "skeleton_human_warrior", "skeleton_human_warrior", "skeleton_human_warrior", "skeleton_wolf" }),

            new EncounterPreset(
                "skeleton_guardians",
                "💀 Skeleton Guardians",
                "An owlbear skeleton and minotaur skeleton guard a crypt entrance. Mid-level undead (EL 4).",
                new List<string> { "skeleton_owlbear", "skeleton_minotaur" }),

            new EncounterPreset(
                "skeleton_menagerie",
                "💀 Skeleton Menagerie",
                "A necromancer's collection: human warrior, wolf, owlbear, troll, and warhorse skeletons. Template showcase (EL 5).",
                new List<string> { "skeleton_human_warrior", "skeleton_wolf", "skeleton_owlbear", "skeleton_troll", "skeleton_warhorse" }),

            new EncounterPreset(
                "skeleton_megaraptor_boss",
                "💀 Megaraptor Skeleton Boss",
                "A Huge megaraptor skeleton (CR 4) with skeletal wolf escorts. Dangerous crypt guardian (EL 5).",
                new List<string> { "skeleton_megaraptor", "skeleton_wolf", "skeleton_wolf" }),

            new EncounterPreset(
                "skeleton_cavalry",
                "💀 Skeleton Cavalry",
                "Skeleton warriors mounted on skeletal warhorses, led by a minotaur skeleton. Death rides forth (EL 5).",
                new List<string> { "skeleton_human_warrior", "skeleton_human_warrior", "skeleton_warhorse", "skeleton_warhorse", "skeleton_minotaur" })
        };
    }
}

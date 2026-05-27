using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zombie template creature registrations and encounter presets.
/// Uses ZombieTemplate.Apply() and ZombieFactory to generate
/// pre-defined zombie variants from base creature blueprints.
///
/// D&D 3.5e Monster Manual p.265-266.
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Zombies()
    {
        // ── Standard zombie variants (MM p.265-266) ──
        Register(ZombieFactory.HumanCommonerZombie());
        Register(ZombieFactory.HumanWarriorZombie());
        Register(ZombieFactory.TroglodyteZombie());
        Register(ZombieFactory.OgreZombie());
        Register(ZombieFactory.MinotaurZombie());
        Register(ZombieFactory.OwlbearZombie());
        Register(ZombieFactory.BugbearZombie());

        Debug.Log("[NPCDatabase] Registered 7 zombie template variants.");
    }

    /// <summary>
    /// Encounter presets featuring zombie template creatures.
    /// </summary>
    public static List<EncounterPreset> GetZombieEncounterPresets()
    {
        return new List<EncounterPreset>
        {
            new EncounterPreset(
                "zombie_shamble",
                "🧟 Zombie Shamble",
                "A group of human commoner zombies shamble towards the party. Classic low-level undead (EL 2).",
                new List<string> { "zombie_human_commoner", "zombie_human_commoner", "zombie_human_commoner", "zombie_human_commoner" }),

            new EncounterPreset(
                "zombie_ogre_smash",
                "🧟 Ogre Zombie Smash",
                "An ogre zombie with bugbear zombie escorts. Heavy hitters with single actions only (EL 5).",
                new List<string> { "zombie_ogre", "zombie_bugbear", "zombie_bugbear" }),

            new EncounterPreset(
                "zombie_menagerie",
                "🧟 Zombie Menagerie",
                "A necromancer's collection: human, troglodyte, bugbear, owlbear, and minotaur zombies. Template showcase (EL 7).",
                new List<string> { "zombie_human_commoner", "zombie_troglodyte", "zombie_bugbear", "zombie_owlbear", "zombie_minotaur" }),

            new EncounterPreset(
                "mixed_undead_horde",
                "💀🧟 Mixed Undead Horde",
                "Skeletons and zombies together! Human warrior skeletons provide Improved Initiative, zombie ogre brings raw power (EL 5).",
                new List<string> { "skeleton_human_warrior", "skeleton_human_warrior", "skeleton_human_warrior", "zombie_human_commoner", "zombie_human_commoner", "zombie_ogre" }),

            new EncounterPreset(
                "zombie_minotaur_crypt",
                "🧟 Minotaur Zombie Crypt",
                "A minotaur zombie guards a crypt entrance with zombie escorts. High HP, single actions only (EL 6).",
                new List<string> { "zombie_minotaur", "zombie_troglodyte", "zombie_troglodyte" })
        };
    }
}

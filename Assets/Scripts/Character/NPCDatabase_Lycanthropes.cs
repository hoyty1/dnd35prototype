using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lycanthrope template creature registrations and encounter presets.
/// Uses LycanthropeTemplate.Apply() and LycanthropeFactory to generate
/// pre-defined lycanthrope variants in hybrid form.
///
/// D&D 3.5e Monster Manual p.170-178.
///
/// Registered variants:
///   - Werewolf (Human Warrior 1 + Wolf) — CR 3
///   - Werewolf Lord (Human Fighter 10 + Dire Wolf) — CR 14
///   - Wererat (Human Rogue 1 + Dire Rat) — CR 2
///   - Wereboar (Human Barbarian 1 + Boar) — CR 4
///   - Weretiger (Human Noble 4 + Tiger) — CR 8
///   - Werebear (Human Commoner 1 + Brown Bear) — CR 5
///   - Dire Wereboar (Human Barbarian 1 + Dire Boar) — CR 7
///   - Afflicted Werewolf (weaker, no curse) — CR 2
/// </summary>
public static partial class NPCDatabase
{
    private static void RegisterCreatures_Lycanthropes()
    {
        // ── Standard lycanthrope variants (MM p.170-178) ──
        Register(LycanthropeFactory.Werewolf());
        Register(LycanthropeFactory.WerewolfLord());
        Register(LycanthropeFactory.Wererat());
        Register(LycanthropeFactory.Wereboar());
        Register(LycanthropeFactory.Weretiger());
        Register(LycanthropeFactory.Werebear());
        Register(LycanthropeFactory.DireWereboar());
        Register(LycanthropeFactory.AfflictedWerewolf());

        Debug.Log("[NPCDatabase] Registered 8 lycanthrope template variants.");
    }

    /// <summary>
    /// Encounter presets featuring lycanthrope template creatures.
    /// </summary>
    public static List<EncounterPreset> GetLycanthropeEncounterPresets()
    {
        return new List<EncounterPreset>
        {
            new EncounterPreset(
                "werewolf_pack",
                "🐺 Werewolf Pack",
                "A pack of werewolves in hybrid form ambush the party under a full moon. " +
                "Natural lycanthropes with DR 10/silver and curse-spreading bites (EL 5).",
                new List<string> { "werewolf", "werewolf", "werewolf" }),

            new EncounterPreset(
                "wererat_ambush",
                "🐀 Wererat Ambush",
                "Wererats strike from the shadows of a sewer or alley. " +
                "Nimble and deceptive with rapiers and supernatural agility (EL 5).",
                new List<string> { "wererat", "wererat", "wererat", "wererat" }),

            new EncounterPreset(
                "wereboar_rampage",
                "🐗 Wereboar Rampage",
                "A pair of wereboars in a rage-fueled frenzy. " +
                "Tough hide, savage tusks, and relentless fury (EL 6).",
                new List<string> { "wereboar", "wereboar" }),

            new EncounterPreset(
                "weretiger_hunt",
                "🐅 Weretiger Hunt",
                "A solitary weretiger stalks the party through the jungle. " +
                "Large, powerful, with pounce for devastating charge attacks (EL 8).",
                new List<string> { "weretiger" }),

            new EncounterPreset(
                "lycanthrope_menagerie",
                "🌕 Lycanthrope Menagerie",
                "A gathering of different lycanthropes: werewolf, wererat, wereboar, and werebear. " +
                "Template showcase with varying CR and fighting styles (EL 8).",
                new List<string> { "werewolf", "wererat", "wereboar", "werebear" }),

            new EncounterPreset(
                "werewolf_lord_encounter",
                "🐺👑 Werewolf Lord",
                "A terrifying werewolf lord with dire wolf form leads a pack. " +
                "CR 14 boss encounter with wolf escorts (EL 15).",
                new List<string> { "werewolf_lord", "werewolf", "werewolf" }),

            new EncounterPreset(
                "cursed_villagers",
                "🌑 Cursed Villagers",
                "Afflicted werewolves — recently cursed commoners who cannot control the beast. " +
                "Weaker than natural lycanthropes with DR 5/silver (EL 4).",
                new List<string> { "werewolf_afflicted", "werewolf_afflicted", "werewolf_afflicted" })
        };
    }
}

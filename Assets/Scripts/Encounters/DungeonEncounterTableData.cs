using System.Collections.Generic;

/// <summary>
/// Hardcoded DMG 3.5e dungeon encounter table data for levels 1-8.
/// Built from the CSV creature list organized by Challenge Rating into
/// eight dungeon level tables, each with d% ranges and cascade entries.
///
/// Creature IDs reference NPCDatabase entries. Creatures from the CSV that
/// do not exist in NPCDatabase are noted in comments but omitted from tables
/// to avoid runtime errors.
///
/// Phase 3: DMG Encounter Tables.
/// </summary>
public static class DungeonEncounterTableData
{
    /// <summary>
    /// Build all eight encounter tables with cascade entries and d% ranges.
    /// Each table covers d% 01-100:
    ///   01-10  → Cascade easier (re-roll on level N-1)
    ///   11-90  → Actual encounter entries for this level
    ///   91-100 → Cascade harder (re-roll on level N+1)
    /// </summary>
    public static Dictionary<int, DungeonEncounterTable> BuildAllTables()
    {
        var tables = new Dictionary<int, DungeonEncounterTable>();
        tables[1] = BuildTable1();
        tables[2] = BuildTable2();
        tables[3] = BuildTable3();
        tables[4] = BuildTable4();
        tables[5] = BuildTable5();
        tables[6] = BuildTable6();
        tables[7] = BuildTable7();
        tables[8] = BuildTable8();
        return tables;
    }

    // =========================================================================
    //  TABLE 1 — Dungeon Level 1 (EL 1)
    //  Creatures: CR 1/8 to CR 1
    // =========================================================================
    private static DungeonEncounterTable BuildTable1()
    {
        var t = new DungeonEncounterTable(1);

        // Cascade easier (level 1 wraps to self)
        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        // CR 1/8 – CR 1/4 creatures
        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 15, 1,
            "monstrous_centipede_medium", 2, "2x Medium Monstrous Centipede"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(16, 20, 1,
            "dire_rat", 2, "2x Dire Rat"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(21, 25, 1,
            "giant_fire_beetle", 3, "3x Giant Fire Beetle"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(26, 30, 1,
            "monstrous_scorpion_small", 2, "2x Small Monstrous Scorpion"));

        // CR 1/2 creatures
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 35, 1,
            "dwarf_warrior", 1, "1x Dwarf Warrior"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(36, 40, 1,
            "elf_warrior", 1, "1x Elf Warrior"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(41, 44, 1,
            "goblin", 2, "2x Goblin"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(45, 48, 1,
            "hobgoblin", 1, "1x Hobgoblin"));

        // CR 1 creatures
        t.Entries.Add(DungeonEncounterTableEntry.Basic(49, 53, 1,
            "krenshar", 1, "1x Krenshar"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(54, 58, 1,
            "lemure", 1, "1x Lemure"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 63, 1,
            "stirge", 2, "2x Stirge"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(64, 68, 1,
            "spider_swarm", 1, "1x Spider Swarm"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(69, 73, 1,
            "lantern_archon", 1, "1x Lantern Archon"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(74, 78, 1,
            "halfling_warrior", 1, "1x Halfling Warrior"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 83, 1,
            "fiendish_dire_rat", 1, "1x Fiendish Dire Rat"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(84, 87, 1,
            "bat_swarm", 1, "1x Bat Swarm"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(88, 90, 1,
            "rat_swarm", 1, "1x Rat Swarm"));

        // Cascade harder
        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 2 — Dungeon Level 2 (EL 2)
    //  Creatures: CR 1 to CR 2
    // =========================================================================
    private static DungeonEncounterTable BuildTable2()
    {
        var t = new DungeonEncounterTable(2);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        // CR 1 creatures (pairs or singles for EL 2)
        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 15, 2,
            "bugbear", 1, "1x Bugbear"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(16, 20, 2,
            "choker", 1, "1x Choker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(21, 25, 2,
            "dretch", 1, "1x Dretch"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(26, 29, 2,
            "quasit", 1, "1x Quasit"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(30, 33, 2,
            "imp", 1, "1x Imp"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(34, 37, 2,
            "dire_bat", 1, "1x Dire Bat"));

        // CR 2 creatures
        t.Entries.Add(DungeonEncounterTableEntry.Basic(38, 42, 2,
            "formian_worker", 2, "2x Formian Worker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 47, 2,
            "shocker_lizard", 2, "2x Shocker Lizard"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(48, 52, 2,
            "worg", 1, "1x Worg"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(53, 57, 2,
            "constrictor_snake", 1, "1x Constrictor Snake"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(58, 62, 2,
            "huge_monstrous_centipede", 1, "1x Huge Monstrous Centipede"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(63, 67, 2,
            "gnoll", 2, "2x Gnoll"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(68, 72, 2,
            "lizardfolk", 1, "1x Lizardfolk"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(73, 77, 2,
            "troglodyte", 2, "2x Troglodyte"));

        // Swarms & misc
        t.Entries.Add(DungeonEncounterTableEntry.Basic(78, 82, 2,
            "locust_swarm", 1, "1x Locust Swarm"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 87, 2,
            "small_viper", 3, "3x Small Viper Snake"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(88, 90, 2,
            "dire_weasel", 1, "1x Dire Weasel"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 3 — Dungeon Level 3 (EL 3)
    //  Creatures: CR 1 to CR 3
    // =========================================================================
    private static DungeonEncounterTable BuildTable3()
    {
        var t = new DungeonEncounterTable(3);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        // CR 2-3 singles, CR 1 groups
        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 15, 3,
            "allip", 1, "1x Allip"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(16, 20, 3,
            "cockatrice", 1, "1x Cockatrice"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(21, 25, 3,
            "doppelganger", 1, "1x Doppelganger"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(26, 29, 3,
            "drow", 2, "2x Drow"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(30, 33, 3,
            "ethereal_filcher", 1, "1x Ethereal Filcher"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(34, 37, 3,
            "ethereal_marauder", 1, "1x Ethereal Marauder"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(38, 41, 3,
            "ettercap", 1, "1x Ettercap"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(42, 45, 3,
            "violet_fungus", 2, "2x Violet Fungus"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(46, 49, 3,
            "ghast", 1, "1x Ghast"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(50, 53, 3,
            "grick", 1, "1x Grick"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(54, 57, 3,
            "hell_hound", 1, "1x Hell Hound"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(58, 61, 3,
            "howler", 1, "1x Howler"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(62, 65, 3,
            "ogre", 1, "1x Ogre"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(66, 69, 3,
            "gelatinous_cube", 1, "1x Gelatinous Cube"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(70, 73, 3,
            "phantom_fungus", 1, "1x Phantom Fungus"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(74, 77, 3,
            "rust_monster", 1, "1x Rust Monster"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(78, 81, 3,
            "shadow", 1, "1x Shadow"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(82, 85, 3,
            "yuan_ti_pureblood", 1, "1x Yuan-ti Pureblood"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(86, 88, 3,
            "giant_praying_mantis", 1, "1x Giant Praying Mantis"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(89, 90, 3,
            "viper_medium", 2, "2x Medium Viper Snake"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 4 — Dungeon Level 4 (EL 4)
    //  Creatures: CR 2 to CR 4
    // =========================================================================
    private static DungeonEncounterTable BuildTable4()
    {
        var t = new DungeonEncounterTable(4);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 14, 4,
            "barghest", 1, "1x Barghest"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(15, 18, 4,
            "hound_archon", 1, "1x Hound Archon"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(19, 22, 4,
            "carrion_crawler", 1, "1x Carrion Crawler"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(23, 26, 4,
            "displacer_beast", 1, "1x Displacer Beast"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(27, 30, 4,
            "gargoyle", 1, "1x Gargoyle"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 34, 4,
            "janni", 1, "1x Janni"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(35, 38, 4,
            "ghoul", 3, "3x Ghoul"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(39, 42, 4,
            "svirfneblin", 2, "2x Svirfneblin"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 46, 4,
            "grimlock", 3, "3x Grimlock"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(47, 50, 4,
            "harpy", 1, "1x Harpy"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(51, 54, 4,
            "mimic", 1, "1x Mimic"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(55, 58, 4,
            "minotaur", 1, "1x Minotaur"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 62, 4,
            "gray_ooze", 1, "1x Grey Ooze"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(63, 66, 4,
            "otyugh", 1, "1x Otyugh"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(67, 70, 4,
            "owlbear", 1, "1x Owlbear"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(71, 74, 4,
            "centipede_swarm", 1, "1x Centipede Swarm"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(75, 78, 4,
            "vampire_spawn", 1, "1x Vampire Spawn"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 82, 4,
            "duergar", 2, "2x Duergar"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 86, 4,
            "viper_large", 1, "1x Large Viper Snake"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(87, 90, 4,
            "monstrous_spider_small", 4, "4x Small Monstrous Spider"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 5 — Dungeon Level 5 (EL 5)
    //  Creatures: CR 3 to CR 5
    // =========================================================================
    private static DungeonEncounterTable BuildTable5()
    {
        var t = new DungeonEncounterTable(5);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 14, 5,
            "basilisk", 1, "1x Basilisk"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(15, 18, 5,
            "greater_barghest", 1, "1x Greater Barghest"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(19, 22, 5,
            "celestial_lion", 1, "1x Celestial Lion"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(23, 26, 5,
            "cloaker", 1, "1x Cloaker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(27, 30, 5,
            "bearded_devil", 1, "1x Bearded Devil"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 34, 5,
            "djinni", 1, "1x Djinni"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(35, 38, 5,
            "gibbering_mouther", 1, "1x Gibbering Mouther"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(39, 42, 5,
            "hell_hound", 2, "2x Hell Hound"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 46, 5,
            "manticore", 1, "1x Manticore"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(47, 50, 5,
            "mummy", 1, "1x Mummy"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(51, 54, 5,
            "ochre_jelly", 1, "1x Ochre Jelly"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(55, 58, 5,
            "phase_spider", 1, "1x Phase Spider"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 62, 5,
            "shadow_mastiff", 2, "2x Shadow Mastiff"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(63, 66, 5,
            "skum", 3, "3x Skum"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(67, 70, 5,
            "troll", 1, "1x Troll"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(71, 74, 5,
            "vargouille", 3, "3x Vargouille"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(75, 78, 5,
            "wraith", 1, "1x Wraith"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 82, 5,
            "yuan_ti_halfblood", 1, "1x Yuan-ti Halfblood"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 86, 5,
            "giant_constrictor_snake", 1, "1x Giant Constrictor Snake"));
        t.Entries.Add(DungeonEncounterTableEntry.Classed(87, 90, 5,
            "hobgoblin", "Fighter", 5, 1, "1x Hobgoblin Fighter 5"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 6 — Dungeon Level 6 (EL 6)
    //  Creatures: CR 4 to CR 6
    // =========================================================================
    private static DungeonEncounterTable BuildTable6()
    {
        var t = new DungeonEncounterTable(6);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 14, 6,
            "babau", 1, "1x Babau"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(15, 18, 6,
            "derro", 3, "3x Derro"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(19, 22, 6,
            "chain_devil", 1, "1x Chain Devil"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(23, 26, 6,
            "digester", 1, "1x Digester"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(27, 30, 6,
            "displacer_beast", 2, "2x Displacer Beast"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 34, 6,
            "bralani", 1, "1x Bralani"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(35, 38, 6,
            "ettin", 1, "1x Ettin"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(39, 42, 6,
            "formian_worker", 4, "4x Formian Worker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 46, 6,
            "gargoyle", 2, "2x Gargoyle"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(47, 50, 6,
            "ghast", 3, "3x Ghast"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(51, 54, 6,
            "grick", 3, "3x Grick"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(55, 58, 6,
            "harpy", 2, "2x Harpy"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 62, 6,
            "howler", 2, "2x Howler"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(63, 66, 6,
            "shadow", 3, "3x Shadow"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(67, 70, 6,
            "shocker_lizard", 4, "4x Shocker Lizard"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(71, 74, 6,
            "xill", 1, "1x Xill"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(75, 78, 6,
            "minor_xorn", 1, "1x Minor Xorn"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 82, 6,
            "yuan_ti_pureblood", 2, "2x Yuan-ti Pureblood"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 86, 6,
            "giant_bombardier_beetle", 2, "2x Giant Bombardier Beetle"));
        t.Entries.Add(DungeonEncounterTableEntry.Classed(87, 90, 6,
            "lizardfolk", "Druid", 5, 1, "1x Lizardfolk Druid 5"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 7 — Dungeon Level 7 (EL 7)
    //  Creatures: CR 5 to CR 7
    // =========================================================================
    private static DungeonEncounterTable BuildTable7()
    {
        var t = new DungeonEncounterTable(7);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 14, 7,
            "aboleth", 1, "1x Aboleth"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(15, 18, 7,
            "chaos_beast", 1, "1x Chaos Beast"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(19, 22, 7,
            "chuul", 1, "1x Chuul"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(23, 26, 7,
            "succubus", 1, "1x Succubus"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(27, 30, 7,
            "hellcat", 1, "1x Hellcat"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 34, 7,
            "drider", 1, "1x Drider"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(35, 38, 7,
            "shrieker", 4, "4x Shrieker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(39, 42, 7,
            "hill_giant", 1, "1x Hill Giant"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 46, 7,
            "flesh_golem", 1, "1x Flesh Golem"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(47, 50, 7,
            "invisible_stalker", 1, "1x Invisible Stalker"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(51, 54, 7,
            "manticore", 2, "2x Manticore"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(55, 58, 7,
            "medusa", 1, "1x Medusa"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 62, 7,
            "minotaur", 2, "2x Minotaur"));
        t.Entries.Add(DungeonEncounterTableEntry.Classed(63, 66, 7,
            "ogre", "Barbarian", 4, 1, "1x Ogre Barbarian 4"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(67, 70, 7,
            "black_pudding", 1, "1x Black Pudding"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(71, 74, 7,
            "phasm", 1, "1x Phasm"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(75, 78, 7,
            "shadow_mastiff", 3, "3x Shadow Mastiff"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 82, 7,
            "red_slaad", 1, "1x Red Slaad"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 86, 7,
            "spectre", 1, "1x Spectre"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(87, 90, 7,
            "umber_hulk", 1, "1x Umber Hulk"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }

    // =========================================================================
    //  TABLE 8 — Dungeon Level 8 (EL 8)
    //  Creatures: CR 5 to CR 8+
    // =========================================================================
    private static DungeonEncounterTable BuildTable8()
    {
        var t = new DungeonEncounterTable(8);

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(1, 10, CascadeDirection.Easier));

        t.Entries.Add(DungeonEncounterTableEntry.Basic(11, 14, 8,
            "hound_archon", 2, "2x Hound Archon"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(15, 18, 8,
            "behir", 1, "1x Behir"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(19, 22, 8,
            "bodak", 1, "1x Bodak"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(23, 26, 8,
            "destrachan", 1, "1x Destrachan"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(27, 30, 8,
            "erinyes", 1, "1x Erinyes"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(31, 34, 8,
            "bralani", 2, "2x Bralani"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(35, 38, 8,
            "ettin", 2, "2x Ettin"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(39, 42, 8,
            "formian_taskmaster", 1, "1x Formian Taskmaster"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(43, 46, 8,
            "noble_djinni", 1, "1x Noble Djinni"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(47, 50, 8,
            "efreeti", 1, "1x Efreeti"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(51, 54, 8,
            "stone_giant", 1, "1x Stone Giant"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(55, 58, 8,
            "gorgon", 1, "1x Gorgon"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(59, 62, 8,
            "mind_flayer", 1, "1x Mind Flayer"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(63, 66, 8,
            "mohrg", 1, "1x Mohrg"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(67, 70, 8,
            "mummy", 2, "2x Mummy"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(71, 74, 8,
            "dark_naga", 1, "1x Dark Naga"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(75, 78, 8,
            "ogre_mage", 1, "1x Ogre Mage"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(79, 82, 8,
            "greater_shadow", 1, "1x Greater Shadow"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(83, 86, 8,
            "blue_slaad", 1, "1x Blue Slaad"));
        t.Entries.Add(DungeonEncounterTableEntry.Basic(87, 90, 8,
            "yuan_ti_halfblood", 2, "2x Yuan-ti Halfblood"));

        t.Entries.Add(DungeonEncounterTableEntry.CascadeEntry(91, 100, CascadeDirection.Harder));

        return t;
    }
}

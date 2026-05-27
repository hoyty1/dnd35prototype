namespace DND35e.Identifiers
{
    /// <summary>
    /// Centralized ring identifier constants for all DMG 3.5e magic rings.
    /// Covers all 33 distinct ring types (47 variants including Protection +1–+5 and Energy Resistance tiers).
    /// Source: D&D 3.5e Dungeon Master's Guide pp. 229–233, core rules only.
    /// </summary>
    public static class RingNames
    {
        // ════════════════════════════════════════════════════════════
        //  TIER 1 — Passive Stat Rings (Sprint 1)
        // ════════════════════════════════════════════════════════════

        // --- Protection (+1 to +5 deflection bonus to AC) ---
        public const string RING_OF_PROTECTION_1 = "ring_of_protection_1";
        public const string RING_OF_PROTECTION_2 = "ring_of_protection_2";
        public const string RING_OF_PROTECTION_3 = "ring_of_protection_3";
        public const string RING_OF_PROTECTION_4 = "ring_of_protection_4";
        public const string RING_OF_PROTECTION_5 = "ring_of_protection_5";

        // --- Resistance (resistance bonus to all saves) ---
        public const string RING_OF_RESISTANCE_1 = "ring_of_resistance_1";
        public const string RING_OF_RESISTANCE_2 = "ring_of_resistance_2";
        public const string RING_OF_RESISTANCE_3 = "ring_of_resistance_3";
        public const string RING_OF_RESISTANCE_4 = "ring_of_resistance_4";
        public const string RING_OF_RESISTANCE_5 = "ring_of_resistance_5";

        // --- Energy Resistance — Acid ---
        public const string RING_OF_ENERGY_RESISTANCE_ACID_MINOR   = "ring_of_energy_resistance_acid_minor";
        public const string RING_OF_ENERGY_RESISTANCE_ACID_MAJOR   = "ring_of_energy_resistance_acid_major";
        public const string RING_OF_ENERGY_RESISTANCE_ACID_GREATER = "ring_of_energy_resistance_acid_greater";

        // --- Energy Resistance — Cold ---
        public const string RING_OF_ENERGY_RESISTANCE_COLD_MINOR   = "ring_of_energy_resistance_cold_minor";
        public const string RING_OF_ENERGY_RESISTANCE_COLD_MAJOR   = "ring_of_energy_resistance_cold_major";
        public const string RING_OF_ENERGY_RESISTANCE_COLD_GREATER = "ring_of_energy_resistance_cold_greater";

        // --- Energy Resistance — Electricity ---
        public const string RING_OF_ENERGY_RESISTANCE_ELECTRICITY_MINOR   = "ring_of_energy_resistance_electricity_minor";
        public const string RING_OF_ENERGY_RESISTANCE_ELECTRICITY_MAJOR   = "ring_of_energy_resistance_electricity_major";
        public const string RING_OF_ENERGY_RESISTANCE_ELECTRICITY_GREATER = "ring_of_energy_resistance_electricity_greater";

        // --- Energy Resistance — Fire ---
        public const string RING_OF_ENERGY_RESISTANCE_FIRE_MINOR   = "ring_of_energy_resistance_fire_minor";
        public const string RING_OF_ENERGY_RESISTANCE_FIRE_MAJOR   = "ring_of_energy_resistance_fire_major";
        public const string RING_OF_ENERGY_RESISTANCE_FIRE_GREATER = "ring_of_energy_resistance_fire_greater";

        // --- Energy Resistance — Sonic ---
        public const string RING_OF_ENERGY_RESISTANCE_SONIC_MINOR   = "ring_of_energy_resistance_sonic_minor";
        public const string RING_OF_ENERGY_RESISTANCE_SONIC_MAJOR   = "ring_of_energy_resistance_sonic_major";
        public const string RING_OF_ENERGY_RESISTANCE_SONIC_GREATER = "ring_of_energy_resistance_sonic_greater";

        // --- Special Ability Rings ---
        public const string RING_OF_FORCE_SHIELD          = "ring_of_force_shield";
        public const string RING_OF_EVASION               = "ring_of_evasion";
        public const string RING_OF_FREEDOM_OF_MOVEMENT   = "ring_of_freedom_of_movement";
        public const string RING_OF_FEATHER_FALLING        = "ring_of_feather_falling";

        // --- Skill Bonus Rings ---
        public const string RING_OF_SWIMMING              = "ring_of_swimming";
        public const string RING_OF_CLIMBING              = "ring_of_climbing";
        public const string RING_OF_JUMPING               = "ring_of_jumping";

        // --- Utility Rings ---
        public const string RING_OF_WATER_WALKING         = "ring_of_water_walking";
        public const string RING_OF_SUSTENANCE            = "ring_of_sustenance";
        public const string RING_OF_MIND_SHIELDING        = "ring_of_mind_shielding";
        public const string RING_OF_WARMTH                = "ring_of_warmth";
        public const string RING_OF_CHAMELEON_POWER       = "ring_of_chameleon_power";

        // ════════════════════════════════════════════════════════════
        //  TIER 2+ — Active/Complex Rings (Future Sprints)
        // ════════════════════════════════════════════════════════════

        // --- Command-Word Activation ---
        public const string RING_OF_INVISIBILITY          = "ring_of_invisibility";
        public const string RING_OF_BLINKING              = "ring_of_blinking";
        public const string RING_OF_COUNTERSPELLS         = "ring_of_counterspells";
        public const string RING_OF_ANIMAL_FRIENDSHIP     = "ring_of_animal_friendship";

        // --- Wizardry (bonus spell slots) ---
        public const string RING_OF_WIZARDRY_I            = "ring_of_wizardry_i";
        public const string RING_OF_WIZARDRY_II           = "ring_of_wizardry_ii";
        public const string RING_OF_WIZARDRY_III          = "ring_of_wizardry_iii";
        public const string RING_OF_WIZARDRY_IV           = "ring_of_wizardry_iv";

        // --- Charge-Based ---
        public const string RING_OF_RAM                   = "ring_of_ram";
        public const string RING_OF_TELEKINESIS           = "ring_of_telekinesis";

        // --- Spell Storage ---
        public const string RING_OF_SPELL_STORING_MINOR   = "ring_of_spell_storing_minor";
        public const string RING_OF_SPELL_STORING         = "ring_of_spell_storing";
        public const string RING_OF_SPELL_STORING_MAJOR   = "ring_of_spell_storing_major";

        // --- Complex ---
        public const string RING_OF_REGENERATION          = "ring_of_regeneration";
        public const string RING_OF_SPELL_TURNING         = "ring_of_spell_turning";
        public const string RING_OF_FRIEND_SHIELD         = "ring_of_friend_shield";
        public const string RING_OF_SHOOTING_STARS        = "ring_of_shooting_stars";
        public const string RING_OF_X_RAY_VISION          = "ring_of_x_ray_vision";

        // --- Legendary ---
        public const string RING_OF_THREE_WISHES          = "ring_of_three_wishes";
        public const string RING_OF_DJINNI_CALLING        = "ring_of_djinni_calling";
        public const string RING_OF_ELEMENTAL_COMMAND_AIR   = "ring_of_elemental_command_air";
        public const string RING_OF_ELEMENTAL_COMMAND_EARTH = "ring_of_elemental_command_earth";
        public const string RING_OF_ELEMENTAL_COMMAND_FIRE  = "ring_of_elemental_command_fire";
        public const string RING_OF_ELEMENTAL_COMMAND_WATER = "ring_of_elemental_command_water";
    }
}

using System.Collections.Generic;

/// <summary>
/// Weapon and armor proficiency of a creature built from an <see cref="NPCDefinition"/> (CHR-072). Sets the
/// creature-type fields and the entry weapons on <see cref="CharacterStats"/>; the checks themselves are the same
/// <see cref="CharacterStats.IsProficientWithWeapon"/> and <see cref="CharacterStats.IsProficientWithArmor"/> a PC
/// uses, which add the class tables (NPC classes from DMG p.108-109) to what this sets.
///
/// Rules applied (MM chapter 7 type traits, p.305-317, checked 2026-10-08):
/// - Weapons. Humanoid: all simple weapons, or by character class (a humanoid with real class levels gets its class's
///   proficiencies instead, unless it keeps more than 1 racial HD beside the class,
///   <see cref="NPCDefinition.HasRacialHumanoidHitDice"/>). Giant and outsider: all simple and martial weapons. Fey,
///   monstrous humanoid, undead and the shapechanger subtype (MM p.314): all simple weapons. Aberration, elemental and
///   dragon: all simple weapons only when generally humanoid in form (<see cref="NPCDefinition.IsHumanoidForm"/>).
///   Animal, construct, magical beast, ooze, plant and vermin: no simple weapons.
/// - Entry weapons. Fey, monstrous humanoid, outsider, undead, shapechanger, and humanoid-shaped aberration, elemental,
///   dragon and construct: also the weapons the entry describes it using. For humanoids whose levels are racial HD and
///   for giants the type text does not say so, but the printed attack bonuses carry no non-proficiency penalty (gnoll
///   battleaxe +3, MM p.130), so they get them too. The entry's weapons are those the definition carries
///   (<see cref="NPCDefinition.EquipmentIds"/>, <see cref="NPCDefinition.BackpackItemIds"/>). Every creature also gets
///   the explicit <see cref="NPCDefinition.EntryWeaponIds"/>, which is where a classed humanoid's racial weapon
///   proficiencies and weapon familiarity belong (NPCs have no RaceData). A humanoid with real class levels, an animal,
///   magical beast, ooze, plant or vermin, and an aberration, elemental, dragon or construct not humanoid in form get no
///   proficiency from what they carry, so a classed NPC with a weapon outside its class list takes the -4 a PC takes.
///   A real commoner is proficient with one simple weapon (DMG p.109): the first simple weapon it carries.
/// - Armor. Aberration, elemental, fey, giant, humanoid whose levels are racial HD, monstrous humanoid, outsider,
///   shapechanger and undead: the armor the entry describes it wearing and all lighter types, and shields when
///   proficient with any armor. Animal (unless trained for war, not modelled: no creature wears barding), construct,
///   dragon, magical beast, ooze, plant and vermin: none. A humanoid with real class levels gets armor by class. The worn
///   armor is read from the definition, so a data error in its gear becomes a proficiency (hound_archon's full plate,
///   CRE-053).
/// - Skeletons and zombies keep the base creature's weapon proficiencies (MM p.226, p.266); the templates drop the
///   class, so only the weapons the definition carries and the undead type's simple weapons remain, not the base
///   creature's class proficiencies (CRE-054).
/// </summary>
public static class CreatureProficiency
{
    /// <summary>
    /// Applies <paramref name="def"/>'s type and entry proficiencies to <paramref name="stats"/>, which must be the
    /// fresh stats built from that definition. Called by GameManager.InitializeNPCFromDefinition for every spawn.
    /// </summary>
    public static void ApplyFromDefinition(CharacterStats stats, NPCDefinition def)
    {
        if (stats == null || def == null)
            return;

        // Also set by CreatureTypeProgressionDatabase.ApplyToStats; the same value, so the order of the two calls is free.
        stats.RacialHitDiceStandInClass = def.ResolveRacialHitDiceStandInClass();
        bool standIn = stats.RacialHitDiceStandInClass != null;

        if (!CreatureTypeProgressionDatabase.TryParseCreatureType(def.CreatureType, out CreatureTypeId type))
            type = CreatureTypeId.Humanoid;

        bool simple = false;
        bool martial = false;
        bool wornArmor = false;
        bool entryWeapons = false;
        switch (type)
        {
            case CreatureTypeId.Humanoid:
                // Humanoid type (MM p.310): simple weapons and worn armor, or by character class, so the type's own
                // proficiencies come only with racial HD: a stand-in class, or racial HD kept beside a real class.
                bool racial = standIn || def.HasRacialHumanoidHitDice;
                simple = racial;
                wornArmor = racial;
                entryWeapons = racial;
                break;
            case CreatureTypeId.Giant:
            case CreatureTypeId.Outsider:
                simple = true;
                martial = true;
                wornArmor = true;
                entryWeapons = true;
                break;
            case CreatureTypeId.Fey:
            case CreatureTypeId.MonstrousHumanoid:
            case CreatureTypeId.Undead:
                simple = true;
                wornArmor = true;
                entryWeapons = true;
                break;
            case CreatureTypeId.Aberration:
            case CreatureTypeId.Elemental:
                simple = def.IsHumanoidForm;
                wornArmor = true;
                entryWeapons = def.IsHumanoidForm;
                break;
            case CreatureTypeId.Dragon:
                simple = def.IsHumanoidForm;
                entryWeapons = def.IsHumanoidForm;
                break;
            case CreatureTypeId.Construct:
                // Natural weapons only unless humanoid in form, then the weapons in its entry, not all simple (MM p.307).
                entryWeapons = def.IsHumanoidForm;
                break;
            default:
                // Animal, magical beast, ooze, plant, vermin: natural weapons only, no armor.
                break;
        }

        // Shapechanger subtype (MM p.314): simple weapons, the weapons in its description and the armor it wears.
        if (HasTag(def, "Shapechanger"))
        {
            simple = true;
            wornArmor = true;
            entryWeapons = true;
        }

        stats.CreatureTypeSimpleWeaponProficiency = simple;
        stats.CreatureTypeMartialWeaponProficiency = martial;

        ArmorCategory worn = wornArmor ? HeaviestEntryArmor(def) : ArmorCategory.None;
        stats.CreatureArmorProficiency = worn;
        stats.CreatureShieldProficiency = worn != ArmorCategory.None || def.EntryShieldProficiency;

        List<string> granted = entryWeapons ? EntryWeaponIds(def) : ExplicitEntryWeaponIds(def);
        foreach (string weaponId in granted)
            AddWeaponProficiency(stats, weaponId);

        // A commoner is proficient with one simple weapon (DMG p.109): the first simple weapon it carries.
        if (!standIn && string.Equals(def.CharacterClass, "Commoner", System.StringComparison.OrdinalIgnoreCase))
        {
            string commonerWeapon = FirstCarriedSimpleWeapon(def);
            if (commonerWeapon != null)
                AddWeaponProficiency(stats, commonerWeapon);
        }
    }

    /// <summary>The heaviest armor the entry describes: the armor the definition carries or <see cref="NPCDefinition.EntryArmorCategory"/>.</summary>
    public static ArmorCategory HeaviestEntryArmor(NPCDefinition def)
    {
        ArmorCategory heaviest = IsBodyArmorCategory(def.EntryArmorCategory) ? def.EntryArmorCategory : ArmorCategory.None;
        if (def.EquipmentIds == null)
            return heaviest;

        for (int i = 0; i < def.EquipmentIds.Count; i++)
        {
            EquipmentSlotPair eq = def.EquipmentIds[i];
            if (eq == null || string.IsNullOrWhiteSpace(eq.ItemId))
                continue;
            ItemData item = ItemDatabase.GetItem(eq.ItemId);
            if (item == null || !item.IsArmor || !IsBodyArmorCategory(item.ArmorCat))
                continue;
            if (item.ArmorCat > heaviest)
                heaviest = item.ArmorCat;
        }

        return heaviest;
    }

    /// <summary>Item ids of every weapon the entry describes: carried (equipped or in the pack) and <see cref="NPCDefinition.EntryWeaponIds"/>.</summary>
    public static List<string> EntryWeaponIds(NPCDefinition def)
    {
        List<string> ids = CarriedWeaponIds(def);
        foreach (string id in ExplicitEntryWeaponIds(def))
        {
            if (!ids.Contains(id))
                ids.Add(id);
        }

        return ids;
    }

    /// <summary>Item ids of the weapons the definition carries, equipped first and then in the pack.</summary>
    public static List<string> CarriedWeaponIds(NPCDefinition def)
    {
        var ids = new List<string>();
        if (def.EquipmentIds != null)
        {
            for (int i = 0; i < def.EquipmentIds.Count; i++)
            {
                EquipmentSlotPair eq = def.EquipmentIds[i];
                if (eq != null)
                    AddIfWeapon(ids, eq.ItemId);
            }
        }

        if (def.BackpackItemIds != null)
        {
            for (int i = 0; i < def.BackpackItemIds.Count; i++)
                AddIfWeapon(ids, def.BackpackItemIds[i]);
        }

        return ids;
    }

    /// <summary>The first simple weapon the definition carries (equipped, then in the pack), or null.</summary>
    public static string FirstCarriedSimpleWeapon(NPCDefinition def)
    {
        List<string> carried = CarriedWeaponIds(def);
        for (int i = 0; i < carried.Count; i++)
        {
            ItemData item = ItemDatabase.GetItem(carried[i]);
            if (item != null && item.Proficiency == WeaponProficiency.Simple)
                return carried[i];
        }

        return null;
    }

    private static List<string> ExplicitEntryWeaponIds(NPCDefinition def)
    {
        var ids = new List<string>();
        if (def.EntryWeaponIds == null)
            return ids;
        for (int i = 0; i < def.EntryWeaponIds.Count; i++)
        {
            string id = def.EntryWeaponIds[i];
            if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
                ids.Add(id);
        }

        return ids;
    }

    private static bool HasTag(NPCDefinition def, string tag)
    {
        if (def.CreatureTags == null)
            return false;
        for (int i = 0; i < def.CreatureTags.Count; i++)
        {
            if (string.Equals(def.CreatureTags[i], tag, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void AddIfWeapon(List<string> ids, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || ids.Contains(itemId))
            return;
        ItemData item = ItemDatabase.GetItem(itemId);
        if (item != null && item.IsWeapon)
            ids.Add(itemId);
    }

    private static void AddWeaponProficiency(CharacterStats stats, string weaponId)
    {
        if (stats.ExtraWeaponProficiencies == null)
            stats.ExtraWeaponProficiencies = new List<string>();

        // The display name matches masterwork and special-material copies too (they keep the base name).
        ItemData item = ItemDatabase.GetItem(weaponId);
        string key = item != null && !string.IsNullOrWhiteSpace(item.Name) ? item.Name : weaponId;
        if (!stats.ExtraWeaponProficiencies.Contains(key))
            stats.ExtraWeaponProficiencies.Add(key);
        if (!stats.ExtraWeaponProficiencies.Contains(weaponId))
            stats.ExtraWeaponProficiencies.Add(weaponId);
    }

    private static bool IsBodyArmorCategory(ArmorCategory category)
    {
        return category == ArmorCategory.Light || category == ArmorCategory.Medium || category == ArmorCategory.Heavy;
    }
}

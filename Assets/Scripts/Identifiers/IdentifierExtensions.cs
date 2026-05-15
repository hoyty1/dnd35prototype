using System;
using System.Collections.Generic;

namespace DND35e.Identifiers
{
    /// <summary>
    /// Enum/string conversion helpers for backward-compatible identifier migration.
    /// </summary>
    public static class IdentifierExtensions
    {
        private static readonly Dictionary<ItemID, string> ItemIdToString = new Dictionary<ItemID, string>
        {
            { ItemID.PotionCureLightWounds, ItemIDs.POTION_CURE_LIGHT_WOUNDS },
            { ItemID.PotionCureModerateWounds, "potion_cure_moderate_wounds" },
            { ItemID.PotionCureSeriousWounds, "potion_cure_serious_wounds" },
            { ItemID.PotionBullsStrength, "potion_bulls_strength" },
            { ItemID.PotionCatsGrace, "potion_cats_grace" },
            { ItemID.PotionBearsEndurance, "potion_bears_endurance" },
            { ItemID.PotionInvisibility, "potion_invisibility" },
            { ItemID.PotionHaste, "potion_haste" },
            { ItemID.PotionShieldOfFaith, ItemIDs.POTION_SHIELD_OF_FAITH },

            { ItemID.WeaponDagger, ItemIDs.DAGGER },
            { ItemID.WeaponQuarterstaff, ItemIDs.QUARTERSTAFF },
            { ItemID.WeaponClub, ItemIDs.CLUB },
            { ItemID.WeaponMaceLight, ItemIDs.MACE_LIGHT },
            { ItemID.WeaponMaceHeavy, ItemIDs.MACE_HEAVY },
            { ItemID.WeaponSpear, ItemIDs.SPEAR },
            { ItemID.WeaponTorch, ItemIDs.TORCH },

            { ItemID.WeaponLongsword, ItemIDs.LONGSWORD },
            { ItemID.WeaponShortsword, ItemIDs.SHORT_SWORD },
            { ItemID.WeaponGreatsword, ItemIDs.GREATSWORD },
            { ItemID.WeaponBattleaxe, ItemIDs.BATTLEAXE },
            { ItemID.WeaponGreataxe, ItemIDs.GREATAXE },
            { ItemID.WeaponWarhammer, ItemIDs.WARHAMMER },
            { ItemID.WeaponRapier, ItemIDs.RAPIER },
            { ItemID.WeaponScimitar, ItemIDs.SCIMITAR },
            { ItemID.WeaponFalchion, ItemIDs.FALCHION },
            { ItemID.WeaponFlailHeavy, ItemIDs.FLAIL_HEAVY },
            { ItemID.WeaponLance, ItemIDs.LANCE },
            { ItemID.WeaponMorningstar, ItemIDs.MORNINGSTAR },
            { ItemID.WeaponJavelin, ItemIDs.JAVELIN },

            { ItemID.WeaponShortbow, ItemIDs.SHORTBOW },
            { ItemID.WeaponLongbow, ItemIDs.LONGBOW },
            { ItemID.WeaponCrossbowLight, ItemIDs.CROSSBOW_LIGHT },
            { ItemID.WeaponCrossbowHeavy, ItemIDs.CROSSBOW_HEAVY },
            { ItemID.WeaponSling, ItemIDs.SLING },

            { ItemID.ArmorPadded, ItemIDs.PADDED_ARMOR },
            { ItemID.ArmorLeather, ItemIDs.LEATHER_ARMOR },
            { ItemID.ArmorStuddedLeather, ItemIDs.STUDDED_LEATHER },
            { ItemID.ArmorChainShirt, ItemIDs.CHAIN_SHIRT },

            { ItemID.ArmorHide, ItemIDs.HIDE_ARMOR },
            { ItemID.ArmorScaleMail, ItemIDs.SCALE_MAIL },
            { ItemID.ArmorChainMail, ItemIDs.CHAINMAIL },
            { ItemID.ArmorBreastplate, ItemIDs.BREASTPLATE },

            { ItemID.ArmorSplintMail, ItemIDs.SPLINT_MAIL },
            { ItemID.ArmorBandedMail, ItemIDs.BANDED_MAIL },
            { ItemID.ArmorHalfPlate, ItemIDs.HALF_PLATE },
            { ItemID.ArmorPlate, ItemIDs.FULL_PLATE },

            { ItemID.ShieldBuckler, ItemIDs.BUCKLER },
            { ItemID.ShieldLightWooden, ItemIDs.SHIELD_LIGHT_WOODEN },
            { ItemID.ShieldLightSteel, ItemIDs.SHIELD_LIGHT_STEEL },
            { ItemID.ShieldHeavyWooden, ItemIDs.SHIELD_HEAVY_WOODEN },
            { ItemID.ShieldHeavySteel, ItemIDs.SHIELD_HEAVY_STEEL },
            { ItemID.ShieldTower, ItemIDs.TOWER_SHIELD },

            { ItemID.AmmoArrow, ItemIDs.AMMO_ARROW },
            { ItemID.AmmoBolt, ItemIDs.AMMO_BOLT },
            { ItemID.AmmoSlingBullet, ItemIDs.AMMO_SLING_BULLET },
            { ItemID.AmmoCrossbowBolts20, ItemIDs.CROSSBOW_BOLTS_20 },

            { ItemID.ScrollMagicMissile, "scroll_magic_missile" },
            { ItemID.ScrollCureLightWounds, "scroll_cure_light_wounds" },
            { ItemID.ScrollFireball, "scroll_fireball" },
            { ItemID.ScrollLightningBolt, "scroll_lightning_bolt" },
            { ItemID.ScrollHaste, "scroll_haste" },

            { ItemID.GearBackpack, "backpack" },
            { ItemID.GearBedroll, "bedroll" },
            { ItemID.GearRope, ItemIDs.ROPE },
            { ItemID.GearTorch, ItemIDs.TORCH },
            { ItemID.GearRations, "rations" },
            { ItemID.GearWaterskin, "waterskin" },
            { ItemID.GearRopeHemp, ItemIDs.ROPE_HEMP },
            { ItemID.GearRopeSilk, ItemIDs.ROPE_SILK }
        };

        private static readonly Dictionary<string, ItemID> StringToItemId = new Dictionary<string, ItemID>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<SpellID, string> SpellIdToString = new Dictionary<SpellID, string>
        {
            { SpellID.AcidSplash, SpellNames.ACID_SPLASH },
            { SpellID.DetectMagic, SpellNames.DETECT_MAGIC_WIZ },
            { SpellID.Light, SpellNames.LIGHT },
            { SpellID.MageHand, SpellNames.MAGE_HAND },
            { SpellID.RayOfFrost, SpellNames.RAY_OF_FROST },
            { SpellID.ReadMagic, SpellNames.READ_MAGIC },
            { SpellID.Resistance, SpellNames.RESISTANCE_WIZ },

            { SpellID.BurningHands, SpellNames.BURNING_HANDS },
            { SpellID.CureLightWounds, SpellNames.CURE_LIGHT_WOUNDS },
            { SpellID.MagicMissile, SpellNames.MAGIC_MISSILE },
            { SpellID.Shield, SpellNames.SHIELD },
            { SpellID.MageArmor, SpellNames.MAGE_ARMOR },
            { SpellID.EnlargePerson, SpellNames.ENLARGE_PERSON },
            { SpellID.Grease, SpellNames.GREASE },
            { SpellID.ColorSpray, SpellNames.COLOR_SPRAY },
            { SpellID.Sleep, SpellNames.SLEEP },

            { SpellID.CureModerateWounds, SpellNames.CURE_MODERATE_WOUNDS },
            { SpellID.ScorchingRay, SpellNames.SCORCHING_RAY },
            { SpellID.BullsStrength, SpellNames.BULLS_STRENGTH },
            { SpellID.CatsGrace, SpellNames.CATS_GRACE },
            { SpellID.BearsEndurance, SpellNames.BEARS_ENDURANCE },
            { SpellID.FoxsCunning, SpellNames.FOXS_CUNNING },
            { SpellID.OwlsWisdom, SpellNames.OWLS_WISDOM },
            { SpellID.EaglesSplendor, SpellNames.EAGLES_SPLENDOR },
            { SpellID.Invisibility, SpellNames.INVISIBILITY },
            { SpellID.MirrorImage, SpellNames.MIRROR_IMAGE },
            { SpellID.Web, SpellNames.WEB },

            { SpellID.CureSeriousWounds, "cure_serious_wounds" },
            { SpellID.Fireball, "fireball" },
            { SpellID.LightningBolt, "lightning_bolt" },
            { SpellID.Haste, "haste" },
            { SpellID.Slow, "slow" },
            { SpellID.DispelMagic, "dispel_magic" },
            { SpellID.Fly, "fly" },
            { SpellID.HoldPerson, SpellNames.HOLD_PERSON },

            { SpellID.CureCriticalWounds, "cure_critical_wounds" },
            { SpellID.IceStorm, "ice_storm" },
            { SpellID.GreaterInvisibility, "greater_invisibility" },
            { SpellID.Stoneskin, "stoneskin" },
            { SpellID.MassEnlargePerson, SpellNames.MASS_ENLARGE_PERSON },
            { SpellID.MassReducePerson, SpellNames.MASS_REDUCE_PERSON },

            { SpellID.ConeOfCold, "cone_of_cold" },
            { SpellID.Teleport, "teleport" },
            { SpellID.Cloudkill, "cloudkill" }
        };

        private static readonly Dictionary<string, SpellID> StringToSpellId = new Dictionary<string, SpellID>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<DamageType, string> DamageTypeToString = new Dictionary<DamageType, string>
        {
            { DamageType.Slashing, GameConstants.DAMAGE_SLASHING },
            { DamageType.Piercing, GameConstants.DAMAGE_PIERCING },
            { DamageType.Bludgeoning, GameConstants.DAMAGE_BLUDGEONING },
            { DamageType.Fire, GameConstants.DAMAGE_FIRE },
            { DamageType.Cold, GameConstants.DAMAGE_COLD },
            { DamageType.Electricity, GameConstants.DAMAGE_ELECTRICITY },
            { DamageType.Acid, GameConstants.DAMAGE_ACID },
            { DamageType.Force, GameConstants.DAMAGE_FORCE },
            { DamageType.Sonic, "sonic" }
        };

        private static readonly Dictionary<string, DamageType> StringToDamageType = new Dictionary<string, DamageType>(StringComparer.OrdinalIgnoreCase);

        static IdentifierExtensions()
        {
            foreach (var kvp in ItemIdToString)
            {
                // Preserve first-registered mapping for storage IDs that intentionally have legacy aliases
                // (e.g., torch -> WeaponTorch while GearTorch remains a backward-compatible enum alias).
                if (!StringToItemId.ContainsKey(kvp.Value))
                    StringToItemId[kvp.Value] = kvp.Key;
            }

            foreach (var kvp in SpellIdToString)
                StringToSpellId[kvp.Value] = kvp.Key;

            foreach (var kvp in DamageTypeToString)
            {
                StringToDamageType[kvp.Value] = kvp.Key;
                StringToDamageType[ToPascalCase(kvp.Value)] = kvp.Key;
            }
        }

        public static string ToStorageString(this ItemID id)
        {
            if (ItemIdToString.TryGetValue(id, out string result))
                return result;

            // Dynamic support for enhanced enum values (e.g., WeaponLongswordPlus1 -> longsword_plus1).
            if (TryBuildEnhancedStorageId(id, out string enhancedStorageId))
                return enhancedStorageId;

            return string.Empty;
        }

        public static ItemID ToItemID(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return ItemID.None;

            if (StringToItemId.TryGetValue(str, out ItemID result))
                return result;

            // Dynamic support for enhanced storage ids (e.g., longsword_plus1 -> WeaponLongswordPlus1).
            if (TryParseEnhancedStorageId(str, out ItemID enhancedResult))
                return enhancedResult;

            return ItemID.None;
        }

        public static string ToStorageString(this SpellID id)
        {
            return SpellIdToString.TryGetValue(id, out string result) ? result : string.Empty;
        }

        public static SpellID ToSpellID(this string str)
        {
            return string.IsNullOrWhiteSpace(str)
                ? SpellID.None
                : (StringToSpellId.TryGetValue(str, out SpellID result) ? result : SpellID.None);
        }

        public static string ToDisplayString(this DamageType damageType)
        {
            return DamageTypeToString.TryGetValue(damageType, out string result) ? result : string.Empty;
        }

        public static DamageType ToDamageType(this string str)
        {
            return string.IsNullOrWhiteSpace(str)
                ? DamageType.Slashing
                : (StringToDamageType.TryGetValue(str, out DamageType result) ? result : DamageType.Slashing);
        }

        public static string ToDisplayString(this AbilityScore ability)
        {
            return ability.ToString();
        }

        public static AbilityScore ToAbilityScore(this string str)
        {
            return Enum.TryParse(str, true, out AbilityScore result) ? result : AbilityScore.Strength;
        }

        private static bool TryBuildEnhancedStorageId(ItemID enhancedId, out string storageId)
        {
            storageId = string.Empty;

            string enumName = enhancedId.ToString();
            if (string.IsNullOrWhiteSpace(enumName))
                return false;

            if (!enumName.EndsWith("Plus1", StringComparison.Ordinal) && !enumName.EndsWith("Plus2", StringComparison.Ordinal))
                return false;

            int plusValue = enumName.EndsWith("Plus1", StringComparison.Ordinal) ? 1 : 2;
            string baseEnumName = enumName.Substring(0, enumName.Length - "Plus1".Length);
            if (!Enum.TryParse(baseEnumName, out ItemID baseId))
                return false;

            if (!ItemIdToString.TryGetValue(baseId, out string baseStorageId) || string.IsNullOrWhiteSpace(baseStorageId))
                return false;

            storageId = $"{baseStorageId}_plus{plusValue}";
            return true;
        }

        private static bool TryParseEnhancedStorageId(string input, out ItemID itemId)
        {
            itemId = ItemID.None;

            string trimmed = input?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
                return false;

            int plusSuffixIndex = trimmed.LastIndexOf("_plus", StringComparison.OrdinalIgnoreCase);
            if (plusSuffixIndex <= 0 || plusSuffixIndex >= trimmed.Length - 1)
                return false;

            string baseStorageId = trimmed.Substring(0, plusSuffixIndex);
            string plusSuffix = trimmed.Substring(plusSuffixIndex + "_plus".Length);
            if (!int.TryParse(plusSuffix, out int plusValue) || (plusValue != 1 && plusValue != 2))
                return false;

            if (!StringToItemId.TryGetValue(baseStorageId, out ItemID baseId) || baseId == ItemID.None)
                return false;

            string enhancedEnumName = $"{baseId}Plus{plusValue}";
            return Enum.TryParse(enhancedEnumName, out itemId);
        }

        private static string ToPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string[] parts = value.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
            }
            return string.Join(string.Empty, parts);
        }
    }
}

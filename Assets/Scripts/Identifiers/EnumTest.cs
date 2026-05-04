#if UNITY_EDITOR
using UnityEngine;

namespace DND35e.Identifiers
{
    /// <summary>
    /// Smoke tests for enum/string conversion helpers used in Phase 2 migration.
    /// </summary>
    public class EnumTest : MonoBehaviour
    {
        [ContextMenu("Test Item Enum Conversion")]
        public void TestItemEnumConversion()
        {
            ItemID itemID = ItemID.PotionCureLightWounds;
            string storageKey = itemID.ToStorageString();
            Debug.Log($"ItemID.PotionCureLightWounds -> '{storageKey}'");
            Debug.Assert(storageKey == ItemIDs.POTION_CURE_LIGHT_WOUNDS, "Item enum->string conversion failed");

            ItemID converted = ItemIDs.POTION_CURE_LIGHT_WOUNDS.ToItemID();
            Debug.Log($"'{ItemIDs.POTION_CURE_LIGHT_WOUNDS}' -> ItemID.{converted} ({(int)converted})");
            Debug.Assert(converted == ItemID.PotionCureLightWounds, "Item string->enum conversion failed");
        }

        [ContextMenu("Test Spell Enum Conversion")]
        public void TestSpellEnumConversion()
        {
            SpellID spellID = SpellID.MagicMissile;
            string storageKey = spellID.ToStorageString();
            Debug.Log($"SpellID.MagicMissile -> '{storageKey}'");
            Debug.Assert(storageKey == SpellNames.MAGIC_MISSILE, "Spell enum->string conversion failed");

            SpellID converted = SpellNames.MAGIC_MISSILE.ToSpellID();
            Debug.Log($"'{SpellNames.MAGIC_MISSILE}' -> SpellID.{converted} ({(int)converted})");
            Debug.Assert(converted == SpellID.MagicMissile, "Spell string->enum conversion failed");
        }

        [ContextMenu("Test DamageType Conversion")]
        public void TestDamageTypeConversion()
        {
            foreach (DamageType type in System.Enum.GetValues(typeof(DamageType)))
            {
                string display = type.ToDisplayString();
                DamageType reparsed = display.ToDamageType();
                Debug.Log($"DamageType.{type} -> '{display}' -> {reparsed}");
            }
        }

        [ContextMenu("Test AbilityScore Conversion")]
        public void TestAbilityScoreConversion()
        {
            foreach (AbilityScore score in System.Enum.GetValues(typeof(AbilityScore)))
            {
                string display = score.ToDisplayString();
                AbilityScore reparsed = display.ToAbilityScore();
                Debug.Log($"AbilityScore.{score} -> '{display}' -> {reparsed}");
            }
        }
    }
}
#endif

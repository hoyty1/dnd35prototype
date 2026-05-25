using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages AI awareness and usage of consumable items (potions, scrolls, wands).
///
/// Attached to NPC CharacterControllers at runtime. Tracks available consumables
/// from the NPC's template equipment list and provides decision-making logic
/// for when the AI should use them during combat.
///
/// D&D 3.5e Rules:
/// - Using a potion is a standard action that provokes an AoO
/// - Using a scroll requires a caster level check if CL is too low
/// - Using a wand requires UMD check for classes not on the spell list
/// </summary>
public class AIConsumableManager : MonoBehaviour
{
    [Header("Available Consumables")]
    public List<string> AvailablePotions = new List<string>();
    public List<string> AvailableScrolls = new List<string>();
    public List<string> AvailableWands = new List<string>();

    [Header("AI Thresholds")]
    [Range(0f, 1f)]
    [Tooltip("HP percentage below which AI will try to use healing potions")]
    public float HealingPotionThreshold = 0.4f;

    [Tooltip("Whether the AI has used buff consumables this combat")]
    public bool HasBuffedThisCombat = false;

    private CharacterController _character;

    void Awake()
    {
        _character = GetComponent<CharacterController>();
    }

    /// <summary>
    /// Initialize consumable lists from an NPCDefinition's backpack items.
    /// Called after spawning an NPC from a template.
    /// </summary>
    public void InitFromBackpackItems(List<string> backpackItemIds)
    {
        if (backpackItemIds == null) return;

        AvailablePotions.Clear();
        AvailableScrolls.Clear();
        AvailableWands.Clear();

        foreach (string itemId in backpackItemIds)
        {
            if (string.IsNullOrEmpty(itemId)) continue;

            string lower = itemId.ToLower();
            if (lower.StartsWith("potion") || lower.Contains("potion"))
                AvailablePotions.Add(itemId);
            else if (lower.StartsWith("scroll") || lower.Contains("scroll"))
                AvailableScrolls.Add(itemId);
            else if (lower.StartsWith("wand") || lower.Contains("wand"))
                AvailableWands.Add(itemId);
        }
    }

    /// <summary>
    /// Initialize consumable lists directly from template equipment.
    /// </summary>
    public void InitFromTemplateEquipment(List<EquipmentItem> equipment)
    {
        if (equipment == null) return;

        AvailablePotions.Clear();
        AvailableScrolls.Clear();
        AvailableWands.Clear();

        foreach (EquipmentItem item in equipment)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemName)) continue;

            string lower = item.ItemName.ToLower();
            if (lower.Contains("potion"))
                AvailablePotions.Add(item.ItemName);
            else if (lower.Contains("scroll"))
                AvailableScrolls.Add(item.ItemName);
            else if (lower.Contains("wand"))
                AvailableWands.Add(item.ItemName);
        }
    }

    /// <summary>
    /// Evaluate whether the AI should use a consumable this turn.
    /// Returns the item name to use, or null if no consumable should be used.
    /// Called by AIService during NPC turn processing.
    /// </summary>
    public string EvaluateConsumableUse()
    {
        if (_character == null || _character.Stats == null) return null;

        // Priority 1: Healing potions when low HP
        if (ShouldUseHealingPotion())
        {
            string potion = GetBestHealingPotion();
            if (potion != null) return potion;
        }

        // Priority 2: Buff potions at start of combat (if not already buffed)
        if (!HasBuffedThisCombat && AvailablePotions.Count > 0)
        {
            string buff = GetBestBuffPotion();
            if (buff != null)
            {
                HasBuffedThisCombat = true;
                return buff;
            }
        }

        return null;
    }

    /// <summary>
    /// Check if the AI should use a healing potion based on current HP.
    /// </summary>
    public bool ShouldUseHealingPotion()
    {
        if (_character == null || _character.Stats == null) return false;
        if (AvailablePotions.Count == 0) return false;

        float hpPercent = _character.Stats.TotalMaxHP > 0
            ? (float)_character.Stats.CurrentHP / _character.Stats.TotalMaxHP
            : 1f;

        return hpPercent <= HealingPotionThreshold;
    }

    /// <summary>
    /// Get the best healing potion available (highest level cure spell).
    /// </summary>
    public string GetBestHealingPotion()
    {
        string best = null;
        int bestPriority = -1;

        foreach (string potion in AvailablePotions)
        {
            string lower = potion.ToLower();
            int priority = 0;

            if (lower.Contains("cure serious") || lower.Contains("cure_serious"))
                priority = 3;
            else if (lower.Contains("cure moderate") || lower.Contains("cure_moderate"))
                priority = 2;
            else if (lower.Contains("cure light") || lower.Contains("cure_light"))
                priority = 1;
            else if (lower.Contains("heal") || lower.Contains("cure"))
                priority = 1;

            if (priority > bestPriority)
            {
                bestPriority = priority;
                best = potion;
            }
        }

        return best;
    }

    /// <summary>
    /// Get the best buff potion (e.g., Bull's Strength, Cat's Grace).
    /// </summary>
    public string GetBestBuffPotion()
    {
        foreach (string potion in AvailablePotions)
        {
            string lower = potion.ToLower();
            if (lower.Contains("strength") || lower.Contains("grace") ||
                lower.Contains("endurance") || lower.Contains("shield of faith") ||
                lower.Contains("bull") || lower.Contains("cat") ||
                lower.Contains("bear") || lower.Contains("fox") ||
                lower.Contains("owl") || lower.Contains("eagle"))
            {
                return potion;
            }
        }

        return null;
    }

    /// <summary>
    /// Check if the character can use wands (caster class or UMD).
    /// </summary>
    public bool CanUseWands()
    {
        if (_character == null || _character.Stats == null) return false;

        string className = _character.Stats.CharacterClass;
        if (string.IsNullOrEmpty(className)) return false;

        string lower = className.ToLower();
        return lower == "wizard" || lower == "sorcerer" || lower == "bard" ||
               lower == "cleric" || lower == "druid" || lower == "adept" ||
               lower == "ranger" || lower == "paladin";
    }

    /// <summary>
    /// Remove a consumed item from available consumables.
    /// Called after an item is used.
    /// </summary>
    public void ConsumeItem(string itemName)
    {
        if (AvailablePotions.Remove(itemName)) return;
        if (AvailableScrolls.Remove(itemName)) return;
        AvailableWands.Remove(itemName); // Wands have charges, not removed on use
    }

    /// <summary>
    /// Reset combat state (called at start of each combat encounter).
    /// </summary>
    public void ResetCombatState()
    {
        HasBuffedThisCombat = false;
    }

    /// <summary>
    /// Get a summary of available consumables for debugging.
    /// </summary>
    public string GetSummary()
    {
        return $"Potions: {AvailablePotions.Count}, Scrolls: {AvailableScrolls.Count}, Wands: {AvailableWands.Count}";
    }

    /// <summary>Total number of consumable items tracked.</summary>
    public int TotalConsumables => AvailablePotions.Count + AvailableScrolls.Count + AvailableWands.Count;
}

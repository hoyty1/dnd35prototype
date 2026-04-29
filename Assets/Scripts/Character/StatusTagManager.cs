using System;
using System.Collections.Generic;

/// <summary>
/// Manages dynamic combat/display tags for a character.
/// Keeps identity, equipment, HP-state, and active-status tags synchronized.
/// </summary>
public class StatusTagManager
{
    private const string RacePrefix = "Race: ";
    private const string ClassPrefix = "Class: ";
    private const string HpStatePrefix = "HP State: ";
    private const string StatusPrefix = "Status: ";
    private const string WieldingPrefix = "Wielding: ";
    private const string ArmorPrefix = "Armor: ";

    private readonly CharacterController _character;
    private readonly HashSet<string> _appliedArmorTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _appliedWieldingTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _appliedStatusEffectTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public StatusTagManager(CharacterController character)
    {
        _character = character;
    }

    public void RefreshAllTags()
    {
        if (_character == null)
            return;

        CharacterTags tags = _character.Tags;
        if (tags == null)
            return;

        UpdateIdentityTags(tags);
        UpdateHPStateTags(_character.CurrentHPState);
        UpdateStatusEffectTags(_character.GetActiveConditionsDirect());
        RefreshEquipmentTags();
    }

    public void RefreshEquipmentTags()
    {
        if (_character == null)
            return;

        CharacterTags tags = _character.Tags;
        if (tags == null)
            return;

        Inventory inv = _character.Inventory != null ? _character.Inventory.GetInventory() : null;
        ItemData armor = inv != null ? inv.ArmorRobeSlot : null;

        UpdateArmorTags(tags, armor);
        UpdateWieldingTags(inv != null ? inv.LeftHandSlot : null, inv != null ? inv.RightHandSlot : null);
    }

    public void UpdateWieldingTags(ItemData leftHandItem, ItemData rightHandItem)
    {
        if (_character == null)
            return;

        CharacterTags tags = _character.Tags;
        if (tags == null)
            return;

        ClearTagSet(tags, _appliedWieldingTags);

        bool hasRightWeapon = rightHandItem != null && rightHandItem.IsWeapon;
        bool hasLeftWeapon = leftHandItem != null && leftHandItem.IsWeapon;
        bool hasShield = (leftHandItem != null && leftHandItem.IsShield) || (rightHandItem != null && rightHandItem.IsShield);

        if (hasRightWeapon && hasLeftWeapon)
            AddTrackedTag(tags, _appliedWieldingTags, "Dual-Wielding");
        else if (hasRightWeapon || hasLeftWeapon)
            AddTrackedTag(tags, _appliedWieldingTags, "Single Weapon");
        else
            AddTrackedTag(tags, _appliedWieldingTags, "Unarmed");

        if (hasShield)
            AddTrackedTag(tags, _appliedWieldingTags, "Shield Equipped");

        if ((rightHandItem != null && rightHandItem.IsTwoHanded) || (leftHandItem != null && leftHandItem.IsTwoHanded))
            AddTrackedTag(tags, _appliedWieldingTags, "Two-Handed Weapon");

        // Explicit tooltip-facing equipment tags.
        if (rightHandItem != null && !string.IsNullOrWhiteSpace(rightHandItem.Name))
            AddTrackedTag(tags, _appliedWieldingTags, $"{WieldingPrefix}{rightHandItem.Name}");

        if (leftHandItem != null && !string.IsNullOrWhiteSpace(leftHandItem.Name))
            AddTrackedTag(tags, _appliedWieldingTags, $"{WieldingPrefix}{leftHandItem.Name}");

        if (rightHandItem == null && leftHandItem == null)
            AddTrackedTag(tags, _appliedWieldingTags, $"{WieldingPrefix}Unarmed");
    }

    public void UpdateStatusEffectTags(IEnumerable<StatusEffect> activeEffects)
    {
        if (_character == null)
            return;

        CharacterTags tags = _character.Tags;
        if (tags == null)
            return;

        ClearDynamicStatusTags(tags);

        if (activeEffects == null)
            return;

        foreach (StatusEffect effect in activeEffects)
        {
            if (effect == null)
                continue;

            ConditionDefinition def = ConditionRules.GetDefinition(effect.Type);
            string name = string.IsNullOrWhiteSpace(def.DisplayName) ? effect.Type.ToString() : def.DisplayName;
            AddTrackedTag(tags, _appliedStatusEffectTags, $"{StatusPrefix}{name}");
        }
    }

    public void ClearDynamicStatusTags(CharacterTags tags)
    {
        if (tags == null)
            return;

        ClearTagSet(tags, _appliedStatusEffectTags);
    }

    public void UpdateHPStateTags(HPState hpState)
    {
        if (_character == null)
            return;

        CharacterTags tags = _character.Tags;
        if (tags == null)
            return;

        tags.RemoveTagsByPrefix(HpStatePrefix);
        tags.AddTag($"{HpStatePrefix}{hpState}");
    }

    private void UpdateIdentityTags(CharacterTags tags)
    {
        tags.RemoveTagsByPrefix(RacePrefix);
        tags.RemoveTagsByPrefix(ClassPrefix);

        CharacterStats stats = _character != null ? _character.Stats : null;
        string race = _character != null ? _character.DisplayedRace : "Unknown";
        if (string.IsNullOrWhiteSpace(race))
            race = "Unknown";

        string characterClass = (stats != null && !string.IsNullOrWhiteSpace(stats.CharacterClass)) ? stats.CharacterClass : "Unknown";

        tags.AddTag($"{RacePrefix}{race}");
        tags.AddTag($"{ClassPrefix}{characterClass}");
    }

    private void UpdateArmorTags(CharacterTags tags, ItemData armor)
    {
        ClearTagSet(tags, _appliedArmorTags);

        if (armor != null && armor.VisualTags != null && armor.VisualTags.Count > 0)
        {
            foreach (string armorTag in armor.VisualTags)
            {
                AddTrackedTag(tags, _appliedArmorTags, armorTag);
                AddTrackedTag(tags, _appliedArmorTags, $"{ArmorPrefix}{armorTag}");
            }

            tags.RemoveTag("Unarmored");
            tags.RemoveTag($"{ArmorPrefix}Unarmored");
            return;
        }

        AddTrackedTag(tags, _appliedArmorTags, "Unarmored");
        AddTrackedTag(tags, _appliedArmorTags, $"{ArmorPrefix}Unarmored");
    }

    private static void ClearTagSet(CharacterTags tags, HashSet<string> tracked)
    {
        foreach (string tag in tracked)
            tags.RemoveTag(tag);
        tracked.Clear();
    }

    private static void AddTrackedTag(CharacterTags tags, HashSet<string> tracked, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return;

        tags.AddTag(tag);
        tracked.Add(tag);
    }
}

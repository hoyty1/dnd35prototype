// ============================================================================
// GameManager_Spells_K.cs — Spell resolution methods starting with "K".
//
// Part of the GameManager partial class.
// D&D 3.5e PHB rules.
// ============================================================================
using DND35e.Identifiers;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;

public partial class GameManager
{
    // ================================================================
    //  KEEN EDGE — PHB 3.5e p.246
    //  Transmutation. Sor/Wiz 3.
    //  Doubles the threat range of one slashing/piercing weapon.
    //  Duration: 10 min/level.
    // ================================================================

    private static bool IsKeenEdgeSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.KEEN_EDGE, StringComparison.Ordinal);
    }

    private ItemData _pendingKeenEdgeItem;
    private bool _pendingKeenEdgeIsAmmo; // true when user chose the ammo path

    private bool TryHandleKeenEdgeWeaponSelection(CharacterController caster, CharacterController target)
    {
        if (!IsKeenEdgeSpell(_pendingSpell))
        {
            _pendingKeenEdgeItem = null;
            _pendingKeenEdgeIsAmmo = false;
            return false;
        }

        if (target == null || target.Stats == null)
            return false;

        if (_pendingKeenEdgeItem != null || _pendingKeenEdgeIsAmmo)
            return false;

        // Gather weapon options from target
        TryGetKeenEdgeWeaponOptions(target, out List<ItemData> weaponOptions, out List<string> weaponLabels);

        // Also check for ammunition in the CASTER's inventory (like Flame Arrow)
        bool hasAmmo = false;
        if (caster != null)
        {
            var casterInv = Combat_GetCharacterInventory(caster);
            if (casterInv != null && casterInv.GeneralSlots != null)
            {
                foreach (var item in casterInv.GeneralSlots)
                {
                    if (item != null && item.Type == ItemType.Ammunition && item.HasAmmoRemaining && !item.IsThrown)
                    {
                        hasAmmo = true;
                        break;
                    }
                }
            }
        }

        if (weaponOptions.Count == 0 && !hasAmmo)
        {
            CombatUI?.ShowCombatLog($"⚠ {target.Stats.CharacterName} has no eligible slashing/piercing weapon and {(caster != null ? caster.Stats.CharacterName : "caster")} has no ammunition for Keen Edge.");
            _pendingSpell = null;
            _pendingKeenEdgeItem = null;
            _pendingKeenEdgeIsAmmo = false;
            ShowActionChoices();
            return true;
        }

        // If only weapons and exactly one, auto-select
        if (weaponOptions.Count == 1 && !hasAmmo)
        {
            _pendingKeenEdgeItem = weaponOptions[0];
            return false;
        }

        // If only ammo and no weapons, auto-select ammo path
        if (weaponOptions.Count == 0 && hasAmmo)
        {
            _pendingKeenEdgeIsAmmo = true;
            return false;
        }

        // Build combined option list: weapons + ammo option
        var allOptions = new List<ItemData>(weaponOptions);
        var allLabels = new List<string>(weaponLabels);

        if (hasAmmo)
        {
            allOptions.Add(null); // sentinel for ammo path
            allLabels.Add("🏹 Enchant Ammunition (up to 50 projectiles)");
        }

        CombatUI?.ShowPickUpItemSelection(
            actorName: caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster",
            itemOptions: allLabels,
            onSelect: selectedIndex =>
            {
                if (selectedIndex < 0 || selectedIndex >= allOptions.Count)
                {
                    _pendingSpell = null;
                    _pendingKeenEdgeItem = null;
                    _pendingKeenEdgeIsAmmo = false;
                    ShowActionChoices();
                    return;
                }

                if (allOptions[selectedIndex] == null)
                {
                    // Ammo path selected
                    _pendingKeenEdgeIsAmmo = true;
                    _pendingKeenEdgeItem = null;
                }
                else
                {
                    _pendingKeenEdgeItem = allOptions[selectedIndex];
                    _pendingKeenEdgeIsAmmo = false;
                }
                PerformSpellCast(caster, target);
            },
            onCancel: () =>
            {
                _pendingSpell = null;
                _pendingKeenEdgeItem = null;
                _pendingKeenEdgeIsAmmo = false;
                ShowActionChoices();
            },
            titleOverride: "Keen Edge - Select Target",
            bodyOverride: $"Choose a weapon or ammunition to enchant with Keen Edge.",
            optionButtonColorOverride: new Color(0.24f, 0.34f, 0.56f, 1f));
        return true;
    }

    private static bool TryGetKeenEdgeWeaponOptions(CharacterController target, out List<ItemData> weapons, out List<string> labels)
    {
        weapons = new List<ItemData>();
        labels = new List<string>();

        var inventory = target.GetComponent<InventoryComponent>()?.CharacterInventory;
        if (inventory == null)
            return false;

        TryAddKeenEdgeOption(inventory.RightHandSlot, "Right Hand", weapons, labels);
        TryAddKeenEdgeOption(inventory.LeftHandSlot, "Left Hand", weapons, labels);
        TryAddKeenEdgeOption(inventory.HandsSlot, "Hands", weapons, labels);

        if (inventory.GeneralSlots != null)
        {
            for (int i = 0; i < inventory.GeneralSlots.Length; i++)
            {
                ItemData item = inventory.GeneralSlots[i];
                if (item == null) continue;
                TryAddKeenEdgeOption(item, $"Backpack Slot {i + 1}", weapons, labels);
            }
        }

        return weapons.Count > 0;
    }

    private static void TryAddKeenEdgeOption(ItemData item, string locationLabel, List<ItemData> weapons, List<string> labels)
    {
        if (item == null || !item.IsWeapon || weapons == null || labels == null)
            return;

        // Keen Edge only works on slashing or piercing weapons
        string dmgType = item.DamageType != null ? item.DamageType.ToLowerInvariant() : "";
        bool isSlashing = dmgType.Contains("slashing");
        bool isPiercing = dmgType.Contains("piercing");
        if (!isSlashing && !isPiercing)
            return;

        int currentThreat = item.CritThreatMin > 0 ? item.CritThreatMin : 20;
        weapons.Add(item);
        labels.Add($"{item.Name} ({locationLabel}, threat {currentThreat}-20)");
    }

    private bool TryApplyKeenEdgeToPendingItem(CharacterController caster, CharacterController target, SpellData spell)
    {
        if (!IsKeenEdgeSpell(spell))
            return false;

        // ── Ammo path: enchant up to 50 projectiles (like Flame Arrow) ──
        if (_pendingKeenEdgeIsAmmo)
        {
            _pendingKeenEdgeIsAmmo = false;
            _pendingKeenEdgeItem = null;
            return TryApplyKeenEdgeToAmmo(caster, spell);
        }

        ItemData weapon = _pendingKeenEdgeItem;
        _pendingKeenEdgeItem = null;

        if (weapon == null)
        {
            CombatUI?.ShowCombatLog("⚠ Keen Edge failed: no weapon selected.");
            return true;
        }

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetCasterLevel()) : 1;
        int rounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name;

        // Calculate threat range doubling: the "threat range" is (21 - CritThreatMin).
        // E.g. 19-20 = range 2, doubled = range 4, new min = 17.
        // E.g. 20 = range 1, doubled = range 2, new min = 19.
        int baseThreatMin = weapon.CritThreatMin > 0 ? weapon.CritThreatMin : 20;
        int threatRange = 21 - baseThreatMin; // how many values threaten (e.g. 2 for 19-20)
        int doubledRange = threatRange * 2;
        int newThreatMin = 21 - doubledRange;
        int critModifier = newThreatMin - baseThreatMin; // negative number to lower the min

        var effect = new ItemSpellEffect(spell.SpellId, spell.Name, casterName, casterLevel, rounds)
        {
            CritThreatRangeModifier = critModifier
        };

        weapon.AddOrReplaceItemSpellEffect(effect);

        string recipientName = target != null && target.Stats != null ? target.Stats.CharacterName : "target";
        CombatUI?.ShowCombatLog($"<color=#88FFEE>🗡 {spell.Name} sharpens {recipientName}'s {weapon.Name}: threat range doubled to {newThreatMin}-20 [{effect.GetDurationDisplayString()}].</color>");
        Debug.Log($"[GameManager] Keen Edge: {weapon.Name} threat {baseThreatMin}-20 → {newThreatMin}-20 (modifier {critModifier}), CL {casterLevel}");

        UpdateAllStatsUI();
        return true;
    }

    /// <summary>
    /// Keen Edge ammo path: enchant up to 50 projectiles in the caster's inventory with
    /// doubled threat range, mirroring the Flame Arrow pattern.
    /// Excludes versatile throwing weapons (same as Flame Arrow).
    /// </summary>
    private bool TryApplyKeenEdgeToAmmo(CharacterController caster, SpellData spell)
    {
        if (caster == null || caster.Stats == null)
        {
            CombatUI?.ShowCombatLog("⚠ Keen Edge failed: no caster.");
            return true;
        }

        var inventory = Combat_GetCharacterInventory(caster);
        if (inventory == null)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} has no inventory for Keen Edge.");
            return true;
        }

        // Find all ammunition stacks (exclude versatile throwing weapons, same as Flame Arrow)
        var ammoStacks = new List<ItemData>();
        if (inventory.GeneralSlots != null)
        {
            foreach (var item in inventory.GeneralSlots)
            {
                if (item != null && item.Type == ItemType.Ammunition && item.HasAmmoRemaining && !item.IsThrown)
                    ammoStacks.Add(item);
            }
        }

        if (ammoStacks.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {caster.Stats.CharacterName} has no projectiles in inventory to enchant with Keen Edge.");
            return true;
        }

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, ActiveSpellEffect.CalculateDurationRounds(spell, casterLevel));
        string casterName = caster.Stats.CharacterName;

        int totalEnchanted = 0;
        int maxProjectiles = 50;

        // Ammo base threat is 20/x2 → doubled to 19-20 → modifier = -1
        // (Most ammunition has 20/x2 crit profile)
        int baseThreatMin = 20;
        int threatRange = 21 - baseThreatMin; // 1
        int doubledRange = threatRange * 2;   // 2
        int newThreatMin = 21 - doubledRange; // 19
        int critModifier = newThreatMin - baseThreatMin; // -1

        foreach (var ammo in ammoStacks)
        {
            if (totalEnchanted >= maxProjectiles)
                break;

            // Use ammo-specific threat range if it has one
            int ammoBaseThreat = ammo.CritThreatMin > 0 ? ammo.CritThreatMin : 20;
            int ammoThreatRange = 21 - ammoBaseThreat;
            int ammoDoubledRange = ammoThreatRange * 2;
            int ammoNewThreatMin = 21 - ammoDoubledRange;
            int ammoCritModifier = ammoNewThreatMin - ammoBaseThreat;

            int toEnchant = Mathf.Min(ammo.Quantity, maxProjectiles - totalEnchanted);

            var effect = new ItemSpellEffect(spell.SpellId, spell.Name, casterName, casterLevel, durationRounds)
            {
                CritThreatRangeModifier = ammoCritModifier,
                EnchantedAmmoRemaining = toEnchant
            };

            ammo.AddOrReplaceItemSpellEffect(effect);
            totalEnchanted += toEnchant;
        }

        CombatUI?.ShowCombatLog($"<color=#88FFEE>🗡 {casterName} casts Keen Edge — {totalEnchanted} projectiles now have doubled threat range (19-20) [{durationRounds} rounds].</color>");
        Debug.Log($"[GameManager] Keen Edge (Ammo): {casterName} enchanted {totalEnchanted} projectiles with doubled threat range, CL {casterLevel}, {durationRounds} rounds");

        UpdateAllStatsUI();
        return true;
    }

}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Encapsulates character equipment and weapon state logic used by <see cref="CharacterController"/>.
/// </summary>
public class CharacterEquipment : MonoBehaviour
{
    private CharacterController _character;

    // Shield bash AC suppression state:
    // while active, this character's equipped shield bonus is removed until next StartNewTurn().
    private bool _shieldBashAcSuppressed;
    private int _suppressedShieldBonusAmount;

    public void Initialize(CharacterController character)
    {
        _character = character;
    }

    private CharacterStats Stats => _character != null ? _character.Stats : null;

    private Inventory GetInventory()
    {
        if (_character == null)
            return null;

        InventoryComponent invComp = _character.GetComponent<InventoryComponent>();
        return invComp != null ? invComp.CharacterInventory : null;
    }

    // ========== DUAL WIELD HELPERS ==========

    public bool CanDualWield()
    {
        if (_character == null)
            return false;

        // D&D 3.5: You can't attack with two weapons while grappling,
        // even if both weapons are light.
        if (_character.HasCondition(CombatConditionType.Grappled))
            return false;

        return TryGetDualWieldWeapons(out _, out _, out _);
    }

    public ItemData GetDualWieldMainWeapon()
    {
        return TryGetDualWieldWeapons(out ItemData mainWeapon, out _, out _) ? mainWeapon : null;
    }

    public ItemData GetDualWieldOffHandWeapon()
    {
        return TryGetDualWieldWeapons(out _, out ItemData offWeapon, out _) ? offWeapon : null;
    }

    public bool HasOffHandWeaponEquipped()
    {
        return TryGetDualWieldWeapons(out _, out ItemData offWeapon, out _) && offWeapon != null;
    }

    public bool HasThrowableOffHandWeaponEquipped()
    {
        ItemData offWeapon = GetOffHandAttackWeapon();
        bool isThrowable = offWeapon != null
            && offWeapon.IsWeapon
            && offWeapon.IsThrown
            && offWeapon.RangeIncrement > 0;

        Debug.Log($"[Character] {Stats?.CharacterName ?? name} has throwable off-hand weapon: {isThrowable} ({offWeapon?.Name ?? "none"})");
        return isThrowable;
    }

    public ItemData GetOffHandAttackWeapon()
    {
        return GetDualWieldOffHandWeapon();
    }

    public bool IsDualWieldOffHandSpikedGauntlet()
    {
        return TryGetDualWieldWeapons(out _, out _, out bool offHandFromSpikedGauntlet) && offHandFromSpikedGauntlet;
    }

    public bool IsDualWieldOffHandShieldBash()
    {
        return TryGetDualWieldWeapons(out _, out ItemData offWeapon, out _) && IsShieldBashWeapon(offWeapon);
    }

    public bool IsOffHandWeaponLight()
    {
        if (!TryGetDualWieldWeapons(out _, out ItemData offHandItem, out _))
            return false;

        return IsWeaponLightForWielder(offHandItem);
    }

    public bool IsWeaponLightForWielder(ItemData weapon)
    {
        if (weapon == null)
            return false;

        // In this prototype we use item category/size metadata.
        return weapon.IsLightWeapon || weapon.WeaponSize == WeaponSizeCategory.Light;
    }

    public (int mainPenalty, int offPenalty, bool lightOffHand) GetDualWieldPenalties()
    {
        if (!TryGetDualWieldWeapons(out _, out ItemData offHandItem, out _))
            return (-6, -10, false);

        bool lightOffHand = IsWeaponLightForWielder(offHandItem);
        bool hasTWF = Stats != null && Stats.HasFeat("Two-Weapon Fighting");

        (int mainPen, int offPen) = Stats != null
            ? FeatManager.GetTWFPenalties(Stats, lightOffHand)
            : (lightOffHand ? (-4, -8) : (-6, -10));

        Debug.Log($"[DualWield] {Stats?.CharacterName ?? "Unknown"}: TWF feat={hasTWF}, light off-hand={lightOffHand}, penalties={mainPen}/{offPen}");
        return (mainPen, offPen, lightOffHand);
    }

    public string GetDualWieldDescription()
    {
        if (!TryGetDualWieldWeapons(out ItemData mainWeapon, out ItemData offWeapon, out bool offHandFromSpikedGauntlet))
            return "";

        var (mainPen, offPen, lightOff) = GetDualWieldPenalties();

        string offSource = offHandFromSpikedGauntlet
            ? " (Hands slot)"
            : IsShieldBashWeapon(offWeapon)
                ? " (Shield Bash)"
                : "";
        string lightStr = lightOff ? " (light)" : "";
        return $"Dual Wield: {mainWeapon.Name} / {offWeapon.Name}{offSource}{lightStr}\n" +
               $"Penalties: Main {mainPen}, Off-hand {offPen}";
    }

    private bool TryGetDualWieldWeapons(out ItemData mainWeapon, out ItemData offWeapon, out bool offHandFromSpikedGauntlet)
    {
        mainWeapon = null;
        offWeapon = null;
        offHandFromSpikedGauntlet = false;

        Inventory inv = GetInventory();
        if (inv == null)
            return false;

        bool hasHeldWeapon = (inv.RightHandSlot != null && inv.RightHandSlot.IsWeapon)
            || (inv.LeftHandSlot != null && inv.LeftHandSlot.IsWeapon);

        // Off-hand options only apply when another weapon is actively equipped.
        if (!hasHeldWeapon)
            return false;

        mainWeapon = GetEquippedMainWeapon();
        if (mainWeapon == null)
            return false;

        // D&D 3.5e: A two-handed main weapon occupies both hands.
        // This blocks all off-hand attack options (including shield bash and spiked gauntlet).
        if (IsTwoHanding(mainWeapon))
            return false;

        if (inv.LeftHandSlot != null && inv.LeftHandSlot.IsWeapon && inv.LeftHandSlot != mainWeapon)
        {
            offWeapon = inv.LeftHandSlot;
            return true;
        }

        bool canShieldBashOffHand = inv.RightHandSlot != null
            && inv.RightHandSlot == mainWeapon
            && !mainWeapon.IsTwoHanded
            && IsShieldBashWeapon(inv.LeftHandSlot);
        if (canShieldBashOffHand)
        {
            offWeapon = inv.LeftHandSlot;
            return true;
        }

        ItemData handsItem = inv.HandsSlot;
        if (IsSpikedGauntletItem(handsItem))
        {
            offWeapon = handsItem;
            offHandFromSpikedGauntlet = true;
            return true;
        }

        return false;
    }

    // ========== EQUIPPED WEAPON / ARMOR QUERIES ==========

    public ItemData GetEquippedWeapon()
    {
        return GetEquippedMainWeapon();
    }

    public ItemData GetEquippedMainWeapon()
    {
        Inventory inv = GetInventory();
        if (inv == null) return null;

        ItemData rightHand = inv.RightHandSlot;
        if (rightHand != null && rightHand.IsWeapon) return rightHand;

        ItemData leftHand = inv.LeftHandSlot;
        if (leftHand != null && leftHand.IsWeapon) return leftHand;

        ItemData handsGauntlet = GetEquippedHandsSpikedGauntlet();
        if (handsGauntlet != null) return handsGauntlet;

        return null; // Unarmed
    }

    public ItemData GetEquippedMainHandWeapon()
    {
        Inventory inv = GetInventory();
        if (inv == null)
            return null;

        ItemData rightHand = inv.RightHandSlot;
        return rightHand != null && rightHand.IsWeapon ? rightHand : null;
    }

    public bool HasWeaponEquipped()
    {
        return GetEquippedMainWeapon() != null;
    }

    /// <summary>
    /// Returns true if the currently resolved main weapon requires two hands to wield.
    /// </summary>
    public bool IsTwoHanding()
    {
        return IsTwoHanding(GetEquippedMainWeapon());
    }

    private static bool IsTwoHanding(ItemData weapon)
    {
        return CharacterController.IsWeaponTwoHanded(weapon)
            || (weapon != null && weapon.WeaponSize == WeaponSizeCategory.TwoHanded);
    }

    public bool HasDisarmableWeaponEquipped()
    {
        return GetDisarmableHeldItemOptions().Count > 0;
    }

    public List<DisarmableHeldItemOption> GetDisarmableHeldItemOptions()
    {
        var options = new List<DisarmableHeldItemOption>(2);
        Inventory inv = GetInventory();
        if (inv == null)
            return options;

        if (inv.RightHandSlot != null && !IsSpikedGauntletItem(inv.RightHandSlot))
            options.Add(new DisarmableHeldItemOption(EquipSlot.RightHand, inv.RightHandSlot));

        if (inv.LeftHandSlot != null && !IsSpikedGauntletItem(inv.LeftHandSlot))
            options.Add(new DisarmableHeldItemOption(EquipSlot.LeftHand, inv.LeftHandSlot));

        return options;
    }

    public bool HasSunderableItemEquipped()
    {
        return GetSunderableItemOptions().Count > 0;
    }

    public List<SunderableItemOption> GetSunderableItemOptions()
    {
        var options = new List<SunderableItemOption>(4);
        Inventory inv = GetInventory();
        if (inv == null)
            return options;

        if (inv.RightHandSlot != null && inv.RightHandSlot.IsSunderable)
            options.Add(new SunderableItemOption(EquipSlot.RightHand, inv.RightHandSlot, SunderTargetKind.MainHand));

        if (inv.LeftHandSlot != null && inv.LeftHandSlot.IsSunderable)
        {
            SunderTargetKind kind = inv.LeftHandSlot.IsShield ? SunderTargetKind.Shield : SunderTargetKind.OffHand;
            options.Add(new SunderableItemOption(EquipSlot.LeftHand, inv.LeftHandSlot, kind));
        }

        if (inv.ArmorRobeSlot != null && inv.ArmorRobeSlot.IsArmor)
            options.Add(new SunderableItemOption(EquipSlot.ArmorRobe, inv.ArmorRobeSlot, SunderTargetKind.Armor));

        return options;
    }

    public List<DisarmableHeldItemOption> GetEquippedLightHandWeaponOptions()
    {
        var options = new List<DisarmableHeldItemOption>(2);
        Inventory inv = GetInventory();
        if (inv == null)
            return options;

        if (IsLightHandWeapon(inv.RightHandSlot))
            options.Add(new DisarmableHeldItemOption(EquipSlot.RightHand, inv.RightHandSlot));

        if (IsLightHandWeapon(inv.LeftHandSlot))
            options.Add(new DisarmableHeldItemOption(EquipSlot.LeftHand, inv.LeftHandSlot));

        return options;
    }

    public bool CanAttackWithLightWeaponWhileGrappling(out ItemData mainHandLightWeapon, out string reason)
    {
        reason = string.Empty;
        mainHandLightWeapon = GetEquippedMainHandWeapon();

        if (mainHandLightWeapon == null)
        {
            reason = "No main-hand weapon equipped.";
            return false;
        }

        bool isLight = mainHandLightWeapon.IsLightWeapon || mainHandLightWeapon.WeaponSize == WeaponSizeCategory.Light;
        if (!isLight)
        {
            reason = $"{mainHandLightWeapon.Name} is not a light weapon.";
            return false;
        }

        if (mainHandLightWeapon.RequiresReload && !mainHandLightWeapon.IsLoaded)
        {
            reason = $"{mainHandLightWeapon.Name} is unloaded and must be reloaded.";
            return false;
        }

        return true;
    }

    // ========== HANDS SLOT HELPERS ==========

    public bool HasSpikedGauntletEquipped()
    {
        return GetEquippedHandsSpikedGauntlet() != null;
    }

    public bool HasGauntletEquipped()
    {
        return IsGauntletItem(GetEquippedHandsItem());
    }

    private ItemData GetEquippedHandsItem()
    {
        Inventory inv = GetInventory();
        if (inv == null)
            return null;

        return inv.HandsSlot;
    }

    private ItemData GetEquippedHandsSpikedGauntlet()
    {
        ItemData handsItem = GetEquippedHandsItem();
        return IsSpikedGauntletItem(handsItem) ? handsItem : null;
    }

    // ========== RELOAD / WEAPON STATE ==========

    public bool IsCrossbowUnloaded(ItemData weapon = null)
    {
        weapon ??= GetEquippedMainWeapon();
        return weapon != null && weapon.RequiresReload && !weapon.IsLoaded;
    }

    public bool HasRapidReloadForWeapon(ItemData weapon)
    {
        if (Stats == null || weapon == null || !weapon.RequiresReload) return false;

        string featName = weapon.GetRapidReloadFeatName();
        if (string.IsNullOrEmpty(featName)) return false;
        return Stats.HasFeat(featName);
    }

    public ReloadActionType GetEffectiveReloadAction(ItemData weapon)
    {
        if (weapon == null || !weapon.RequiresReload) return ReloadActionType.None;
        bool hasRapidReload = HasRapidReloadForWeapon(weapon);
        return weapon.GetEffectiveReloadAction(hasRapidReload);
    }

    public string OnWeaponFired(ItemData weapon)
    {
        if (weapon == null || !weapon.RequiresReload)
            return string.Empty;

        weapon.IsLoaded = false;

        ReloadActionType effectiveReload = GetEffectiveReloadAction(weapon);
        if (effectiveReload == ReloadActionType.FreeAction)
        {
            weapon.IsLoaded = true;
            return $"{weapon.Name} is reloaded (free action via Rapid Reload).";
        }

        return $"{weapon.Name} is now unloaded and must be reloaded.";
    }

    public bool ReloadWeapon(ItemData weapon)
    {
        if (weapon == null || !weapon.RequiresReload) return false;
        if (weapon.IsLoaded) return false;
        weapon.IsLoaded = true;
        return true;
    }

    public string GetWeaponLoadStateLabel(ItemData weapon = null)
    {
        weapon ??= GetEquippedMainWeapon();
        if (weapon == null || !weapon.RequiresReload) return string.Empty;

        string state = weapon.IsLoaded ? "LOADED" : "UNLOADED";
        ReloadActionType action = GetEffectiveReloadAction(weapon);
        string actionLabel = CharacterController.GetReloadActionLabel(action);
        return $"{weapon.Name}: {state} (Reload: {actionLabel})";
    }

    public bool IsEquippedWeaponRanged()
    {
        ItemData weapon = GetEquippedMainWeapon();
        if (weapon == null) return false;
        return weapon.WeaponCat == WeaponCategory.Ranged;
    }

    public bool HasThrowableWeaponEquipped()
    {
        ItemData weapon = GetEquippedWeapon();
        if (weapon == null)
            return false;

        return weapon.WeaponCat == WeaponCategory.Melee
            && weapon.IsThrown
            && weapon.RangeIncrement > 0;
    }

    public bool HasMeleeWeaponEquipped()
    {
        Inventory inv = GetInventory();
        if (inv == null)
            return true; // fall back to unarmed threat when inventory is unavailable

        bool hasAnyEquippedWeapon = false;

        ItemData rightHand = inv.RightHandSlot;
        if (rightHand != null && rightHand.IsWeapon)
        {
            hasAnyEquippedWeapon = true;
            if (rightHand.WeaponCat == WeaponCategory.Melee)
                return true;
        }

        ItemData leftHand = inv.LeftHandSlot;
        if (leftHand != null && leftHand.IsWeapon)
        {
            hasAnyEquippedWeapon = true;
            if (leftHand.WeaponCat == WeaponCategory.Melee)
                return true;
        }

        ItemData hands = inv.HandsSlot;
        if (hands != null && hands.IsWeapon)
        {
            hasAnyEquippedWeapon = true;
            if (hands.WeaponCat == WeaponCategory.Melee)
                return true;
        }

        // If no weapon is equipped at all, unarmed still counts as melee in this prototype.
        if (!hasAnyEquippedWeapon)
            return true;

        // At least one weapon is equipped, but all equipped weapons are ranged-only.
        return false;
    }

    // ========== SHIELD BASH AC SUPPRESSION ==========

    public void SuppressShieldBonusForShieldBash(ItemData shield)
    {
        if (!IsShieldBashWeapon(shield) || Stats == null)
            return;

        if (FeatManager.HasImprovedShieldBash(Stats))
        {
            _shieldBashAcSuppressed = false;
            _suppressedShieldBonusAmount = 0;
            Debug.Log($"[ShieldBash] {Stats.CharacterName}: Improved Shield Bash active, shield AC bonus retained.");
            GameManager.Instance?.CombatUI?.ShowCombatLog($"🛡 {Stats.CharacterName} uses Improved Shield Bash and keeps shield AC while bashing.");
            return;
        }

        int shieldBonus = Mathf.Max(0, shield.ShieldBonus);
        _shieldBashAcSuppressed = shieldBonus > 0;
        _suppressedShieldBonusAmount = shieldBonus;

        if (_shieldBashAcSuppressed)
        {
            Stats.ShieldBonus = 0;
            Debug.Log($"[ShieldBash] {Stats.CharacterName}: Shield bonus suppressed (+{_suppressedShieldBonusAmount} AC) until next turn.");
        }
    }

    public void RestoreShieldBonusAfterShieldBash()
    {
        if (!_shieldBashAcSuppressed || Stats == null)
            return;

        Inventory inv = GetInventory();
        ItemData leftHandShield = inv != null ? inv.LeftHandSlot : null;
        int restoredShieldBonus = IsShieldBashWeapon(leftHandShield) ? Mathf.Max(0, leftHandShield.ShieldBonus) : 0;

        Stats.ShieldBonus = restoredShieldBonus;
        _shieldBashAcSuppressed = false;
        _suppressedShieldBonusAmount = 0;

        Debug.Log($"[ShieldBash] {Stats.CharacterName}: Shield bonus restored (+{restoredShieldBonus} AC).");
    }

    // ========== ITEM TYPE HELPERS ==========

    private static bool IsLightHandWeapon(ItemData item)
    {
        if (item == null || !item.IsWeapon)
            return false;

        return item.IsLightWeapon || item.WeaponSize == WeaponSizeCategory.Light;
    }

    public static bool IsSpikedGauntletItem(ItemData item)
    {
        if (item == null)
            return false;

        string id = (item.Id ?? string.Empty).ToLowerInvariant();
        string name = (item.Name ?? string.Empty).ToLowerInvariant();
        return id == "spiked_gauntlet" || name.Contains("spiked gauntlet");
    }

    public static bool IsShieldBashWeapon(ItemData item)
    {
        return item != null
            && item.IsShield
            && item.DamageDice > 0
            && item.DamageCount > 0;
    }

    public static bool IsGauntletItem(ItemData item)
    {
        if (item == null)
            return false;

        string id = (item.Id ?? string.Empty).ToLowerInvariant();
        string name = (item.Name ?? string.Empty).ToLowerInvariant();
        return id == "gauntlet" || name == "gauntlet";
    }
}

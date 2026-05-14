using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized economy service managing gold transactions, party stash, store interactions,
/// and buy/sell price calculations. Extracted from GameManager as a TEMPLATE example
/// for the service extraction pattern used in this project.
///
/// PATTERN NOTES (for future service extractions):
/// 1. The service is a MonoBehaviour attached to the GameManager GameObject.
/// 2. Initialize() is called by GameManager after Awake(), passing required dependencies.
/// 3. The service owns its own state (gold, stash) and exposes it via properties.
/// 4. GameManager delegates economy operations to this service but retains coordination logic.
/// 5. Events (OnGoldChanged) allow UI and other systems to react without tight coupling.
/// </summary>
public class EconomyService : MonoBehaviour
{
    // ==================== STATE ====================

    private int _partyGold = 1000;
    private bool _partyStashSeeded;
    private PartyStash _partyStash;

    // Cached references set during Initialize().
    private GameManager _gameManager;
    private Func<CombatUI> _combatUIProvider;

    // ==================== EVENTS ====================

    /// <summary>Fired whenever party gold changes. Payload = new gold total.</summary>
    public event Action<int> OnGoldChanged;

    // ==================== PROPERTIES ====================

    /// <summary>Current party gold. Clamped to >= 0.</summary>
    public int PartyGold
    {
        get => _partyGold;
        set
        {
            int clamped = Mathf.Max(0, value);
            if (_partyGold == clamped)
                return;

            _partyGold = clamped;
            Debug.Log($"[Economy][Gold] Party gold is now {_partyGold} gp");
            OnGoldChanged?.Invoke(_partyGold);
        }
    }

    /// <summary>Shared party stash (session-only for now).</summary>
    public PartyStash PartyStash
    {
        get => _partyStash;
        set => _partyStash = value;
    }

    // ==================== LIFECYCLE ====================

    /// <summary>
    /// Called by GameManager after Awake to inject dependencies.
    /// Follow this pattern when creating new services.
    /// </summary>
    public void Initialize(GameManager gameManager, Func<CombatUI> combatUIProvider, int startingGold = 1000)
    {
        _gameManager = gameManager;
        _combatUIProvider = combatUIProvider;
        _partyGold = startingGold;
        _partyStash = new PartyStash();
        _partyStashSeeded = false;

        Debug.Log($"[Economy] EconomyService initialized | startingGold={startingGold}");
    }

    /// <summary>Clean up on destruction or combat end.</summary>
    public void Cleanup()
    {
        // Nothing to clean up currently, but follow the pattern.
        Debug.Log("[Economy] EconomyService cleaned up");
    }

    // ==================== GOLD TRANSACTIONS ====================

    /// <summary>
    /// Attempt to spend gold. Returns true if the party had enough gold.
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (amount <= 0)
            return true;

        if (_partyGold >= amount)
        {
            PartyGold -= amount;
            Debug.Log($"[Economy][Gold] Spent {amount} gp. Remaining: {_partyGold} gp");
            return true;
        }

        Debug.LogWarning($"[Economy][Gold] Not enough gold! Need {amount} gp, have {_partyGold} gp");
        return false;
    }

    /// <summary>
    /// Add gold to party treasury.
    /// </summary>
    public void AddGold(int amount)
    {
        if (amount <= 0)
            return;

        PartyGold += amount;
        Debug.Log($"[Economy][Gold] Gained {amount} gp. Total: {_partyGold} gp");
    }

    /// <summary>
    /// Check whether the party can afford a given amount.
    /// </summary>
    public bool CanAfford(int amount)
    {
        return amount <= 0 || _partyGold >= amount;
    }

    // ==================== PARTY STASH ====================

    /// <summary>
    /// Ensure the party stash is initialized and seeded with defaults if necessary.
    /// </summary>
    public void EnsurePartyStashInitialized()
    {
        _partyStash ??= new PartyStash();

        if (!_partyStashSeeded)
        {
            _partyStash.SeedDefaultItemsIfEmpty();
            _partyStashSeeded = true;
        }
    }

    /// <summary>
    /// Unlock the stash (typically when outside combat or in pre-combat menus).
    /// </summary>
    public void UnlockStash()
    {
        EnsurePartyStashInitialized();
        _partyStash?.Unlock();
    }

    /// <summary>
    /// Lock the stash (typically when combat starts).
    /// </summary>
    public void LockStash()
    {
        _partyStash?.Lock();
    }

    // ==================== STORE / MERCHANT ====================

    /// <summary>
    /// Ensure the StoreInventory singleton component exists on the GameManager object.
    /// </summary>
    public StoreInventory EnsureStoreInventoryInitialized()
    {
        if (_gameManager == null)
        {
            Debug.LogWarning("[Economy] Cannot initialize store: GameManager reference is null.");
            return null;
        }

        StoreInventory storeInventory = StoreInventory.Instance;
        if (storeInventory == null)
            storeInventory = _gameManager.GetComponent<StoreInventory>() ?? _gameManager.gameObject.AddComponent<StoreInventory>();

        return storeInventory;
    }

    // ==================== BUY / SELL PRICE CALCULATIONS ====================

    /// <summary>
    /// Calculate the buy price for an item (full base price).
    /// D&D 3.5e: Items are purchased at full listed price.
    /// </summary>
    public int GetBuyPrice(ItemData item)
    {
        if (item == null)
            return 0;

        return Mathf.Max(0, item.BasePriceGp);
    }

    /// <summary>
    /// Calculate the sell price for an item (half base price, per D&D 3.5e rules).
    /// D&D 3.5e: Items sell for half their listed price.
    /// </summary>
    public int GetSellPrice(ItemData item)
    {
        if (item == null)
            return 0;

        return Mathf.Max(0, item.BasePriceGp / 2);
    }

    /// <summary>
    /// Process selling an item: remove from stash and add gold.
    /// Returns true on success.
    /// </summary>
    public bool SellItem(ItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[Economy] Cannot sell null item.");
            return false;
        }

        int sellPrice = GetSellPrice(item);
        bool removed = _partyStash != null && _partyStash.RemoveItem(item);

        if (!removed)
        {
            Debug.LogWarning($"[Economy] Failed to remove '{item.Name}' from stash for selling.");
            return false;
        }

        AddGold(sellPrice);
        Debug.Log($"[Economy] Sold '{item.Name}' for {sellPrice} gp");

        CombatUI ui = _combatUIProvider?.Invoke();
        ui?.ShowCombatLog($"💰 Sold {item.Name} for {sellPrice} gp");

        return true;
    }

    /// <summary>
    /// Process buying an item from the store: deduct gold and add to stash.
    /// Returns true on success.
    /// </summary>
    public bool BuyItem(ItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("[Economy] Cannot buy null item.");
            return false;
        }

        int buyPrice = GetBuyPrice(item);

        if (!CanAfford(buyPrice))
        {
            Debug.LogWarning($"[Economy] Cannot afford '{item.Name}' ({buyPrice} gp). Have {_partyGold} gp.");
            CombatUI ui = _combatUIProvider?.Invoke();
            ui?.ShowCombatLog($"⚠ Not enough gold to buy {item.Name} ({buyPrice} gp)");
            return false;
        }

        if (!SpendGold(buyPrice))
            return false;

        // Clone item so store retains its copy.
        ItemData purchased = !string.IsNullOrEmpty(item.Id)
            ? ItemDatabase.CloneItem(item.Id)
            : item; // Fallback: use item directly if no ID.
        if (purchased == null)
            purchased = item;
        bool added = _partyStash != null && _partyStash.AddItem(purchased);

        if (!added)
        {
            // Refund if stash is full.
            AddGold(buyPrice);
            Debug.LogWarning($"[Economy] Stash full — refunded {buyPrice} gp for '{item.Name}'.");
            return false;
        }

        Debug.Log($"[Economy] Bought '{item.Name}' for {buyPrice} gp. Remaining: {_partyGold} gp");

        CombatUI ui2 = _combatUIProvider?.Invoke();
        ui2?.ShowCombatLog($"🛒 Bought {item.Name} for {buyPrice} gp");

        return true;
    }

    // ==================== LOOT VALUE HELPERS ====================

    /// <summary>
    /// Calculate total gold value of a list of items (at sell price).
    /// Useful for "Sell All" summaries.
    /// </summary>
    public int CalculateTotalSellValue(IEnumerable<ItemData> items)
    {
        if (items == null)
            return 0;

        int total = 0;
        foreach (ItemData item in items)
        {
            if (item != null && !item.IsDestroyed)
                total += GetSellPrice(item);
        }

        return total;
    }

    /// <summary>
    /// Award gold from combat loot (e.g., coin drops from enemies).
    /// Logs the award to the combat log.
    /// </summary>
    public void AwardCombatGold(int amount, string source)
    {
        if (amount <= 0)
            return;

        AddGold(amount);
        Debug.Log($"[Economy] Combat gold award: +{amount} gp from {source}");

        CombatUI ui = _combatUIProvider?.Invoke();
        ui?.ShowCombatLog($"💰 +{amount} gp from {source}");
    }
}

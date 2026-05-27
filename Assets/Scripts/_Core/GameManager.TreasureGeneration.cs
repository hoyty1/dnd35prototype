// ============================================================================
// GameManager.TreasureGeneration.cs — Integration of D&D 3.5e treasure
// generation system with the post-combat loot collection flow.
// Generates random treasure based on Encounter Level after combat victory.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using DND35e.Treasure;

public partial class GameManager
{
    [Header("Treasure Generation")]
    [Tooltip("Reference to the TreasureUI component. Auto-created if null.")]
    public TreasureUI TreasureUIComponent;

    /// <summary>
    /// The last treasure result generated after combat.
    /// Available for inspection or further processing.
    /// </summary>
    public TreasureResult LastGeneratedTreasure { get; private set; }

    /// <summary>
    /// Generate random treasure for the current encounter based on defeated enemies.
    /// Calculates Encounter Level from the enemies and uses DMG 3.5e Table 3-5.
    /// </summary>
    /// <returns>The generated treasure result, or null if generation fails.</returns>
    public TreasureResult GeneratePostCombatTreasure()
    {
        // Gather defeated enemies
        List<CharacterController> enemies = new List<CharacterController>();
        if (NPCs != null)
        {
            for (int i = 0; i < NPCs.Count; i++)
            {
                var npc = NPCs[i];
                if (npc != null && npc.Stats != null && npc.Team == CharacterTeam.Enemy && npc.Stats.CurrentHP <= 0)
                    enemies.Add(npc);
            }
        }

        if (enemies.Count == 0)
        {
            Debug.Log("[TreasureGen] No defeated enemies found. No treasure generated.");
            return null;
        }

        // Calculate Encounter Level
        int el = EncounterService.CalculateEncounterLevel(enemies);
        el = Mathf.Clamp(el, 1, 20);

        Debug.Log($"[TreasureGen] Generating treasure for {enemies.Count} defeated enemies, EL={el}");

        // Generate treasure
        TreasureResult result = DND35e.Treasure.TreasureGenerator.Generate(el);
        LastGeneratedTreasure = result;

        Debug.Log($"[TreasureGen] Generated: {result.TotalGPValue:N0} gp total | " +
                  $"coins={result.CoinsGPValue} gems={result.Gems.Count} art={result.ArtObjects.Count} " +
                  $"mundane={result.MundaneItems.Count} magic={result.MagicItems.Count}");

        // Log details for debugging
        foreach (string line in result.Log)
            Debug.Log($"[TreasureGen] {line}");

        return result;
    }

    /// <summary>
    /// Show the treasure UI for a given result. If TreasureUI is not assigned,
    /// attempts to find or create one on the main canvas.
    /// </summary>
    /// <param name="result">Treasure result to display</param>
    /// <param name="onClosed">Callback when the treasure UI is closed</param>
    public void ShowTreasureUI(TreasureResult result, System.Action onClosed = null)
    {
        if (result == null || result.IsEmpty)
        {
            Debug.Log("[TreasureGen] No treasure to display.");
            onClosed?.Invoke();
            return;
        }

        EnsureTreasureUICreated();

        if (TreasureUIComponent == null)
        {
            Debug.LogWarning("[TreasureGen] Could not create TreasureUI. Skipping treasure display.");
            // Log treasure to combat log as fallback
            CombatUI?.ShowCombatLog($"💰 Treasure found: {result.TotalGPValue:N0} gp (see console for details)");
            onClosed?.Invoke();
            return;
        }

        TreasureUIComponent.ShowResult(result, onClosed);
    }

    /// <summary>
    /// Convenience: generate and immediately show treasure for the current encounter.
    /// </summary>
    /// <param name="onClosed">Callback when the treasure UI is closed</param>
    public void GenerateAndShowTreasure(System.Action onClosed = null)
    {
        TreasureResult result = GeneratePostCombatTreasure();
        if (result != null && !result.IsEmpty)
        {
            ShowTreasureUI(result, onClosed);
        }
        else
        {
            CombatUI?.ShowCombatLog("📭 No treasure was found.");
            onClosed?.Invoke();
        }
    }

    /// <summary>
    /// Convert generated treasure coins into a gold-piece string for the combat log.
    /// </summary>
    public string GetTreasureCoinSummary()
    {
        if (LastGeneratedTreasure == null) return "No treasure";
        var r = LastGeneratedTreasure;
        var parts = new List<string>();
        if (r.CopperPieces > 0) parts.Add($"{r.CopperPieces:N0} cp");
        if (r.SilverPieces > 0) parts.Add($"{r.SilverPieces:N0} sp");
        if (r.GoldPieces > 0) parts.Add($"{r.GoldPieces:N0} gp");
        if (r.PlatinumPieces > 0) parts.Add($"{r.PlatinumPieces:N0} pp");
        return parts.Count > 0 ? string.Join(", ", parts) : "No coins";
    }

    private void EnsureTreasureUICreated()
    {
        if (TreasureUIComponent != null) return;

        // Try to find an existing one
        TreasureUIComponent = FindObjectOfType<TreasureUI>();
        if (TreasureUIComponent != null) return;

        // Create on the main canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[TreasureGen] No Canvas found to parent TreasureUI.");
            return;
        }

        GameObject treasureObj = new GameObject("TreasureUI", typeof(RectTransform));
        treasureObj.transform.SetParent(canvas.transform, false);

        var rt = treasureObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TreasureUIComponent = treasureObj.AddComponent<TreasureUI>();
        Debug.Log("[TreasureGen] TreasureUI created and parented to canvas.");
    }
}

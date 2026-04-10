using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// D&D 3.5 Initiative System.
/// At combat start, each character (PCs and NPCs) rolls 1d20 + DEX modifier for initiative.
/// Turn order is determined by initiative rolls (highest goes first).
/// Ties: higher DEX modifier wins; if still tied, re-roll.
/// </summary>
public static class InitiativeSystem
{
    /// <summary>
    /// Represents a single combatant's initiative entry.
    /// </summary>
    [System.Serializable]
    public class InitiativeEntry
    {
        public CharacterController Character;
        public int Roll;           // d20 roll
        public int Modifier;       // DEX mod + feat bonuses (e.g., Improved Initiative)
        public int Total;          // Roll + Modifier
        public bool IsPC;

        public InitiativeEntry(CharacterController character, bool isPC)
        {
            Character = character;
            IsPC = isPC;
            Modifier = character.Stats.InitiativeModifier;
            Roll = Random.Range(1, 21); // 1d20
            Total = Roll + Modifier;
        }

        public override string ToString()
        {
            string modStr = Modifier >= 0 ? $"+{Modifier}" : $"{Modifier}";
            return $"{Character.Stats.CharacterName}: {Total} (d20={Roll} {modStr})";
        }
    }

    /// <summary>
    /// Roll initiative for all combatants and return sorted turn order (highest first).
    /// D&D 3.5 tie-breaking: higher DEX modifier wins. If still tied, re-roll tied entries.
    /// </summary>
    public static List<InitiativeEntry> RollInitiative(List<CharacterController> pcs, List<CharacterController> npcs)
    {
        var entries = new List<InitiativeEntry>();

        // Roll for all PCs
        foreach (var pc in pcs)
        {
            if (pc != null && !pc.Stats.IsDead)
            {
                var entry = new InitiativeEntry(pc, true);
                entries.Add(entry);
                Debug.Log($"[Initiative] {entry}");
            }
        }

        // Roll for all NPCs
        foreach (var npc in npcs)
        {
            if (npc != null && !npc.Stats.IsDead)
            {
                var entry = new InitiativeEntry(npc, false);
                entries.Add(entry);
                Debug.Log($"[Initiative] {entry}");
            }
        }

        // Sort: highest total first, then by DEX modifier for ties
        entries.Sort((a, b) =>
        {
            // Primary: total initiative (descending)
            int cmp = b.Total.CompareTo(a.Total);
            if (cmp != 0) return cmp;

            // Tie-breaker 1: higher DEX modifier
            cmp = b.Modifier.CompareTo(a.Modifier);
            if (cmp != 0) return cmp;

            // Tie-breaker 2: re-roll (simulate with random)
            // In practice, we just randomize among still-tied entries
            return Random.Range(0, 2) == 0 ? -1 : 1;
        });

        // Log final order
        Debug.Log("[Initiative] ===== INITIATIVE ORDER =====");
        for (int i = 0; i < entries.Count; i++)
        {
            string tag = entries[i].IsPC ? "PC" : "NPC";
            Debug.Log($"[Initiative] #{i + 1}: [{tag}] {entries[i]}");
        }
        Debug.Log("[Initiative] =============================");

        return entries;
    }

    /// <summary>
    /// Get a formatted string showing the full initiative order for UI display.
    /// </summary>
    public static string GetInitiativeOrderString(List<InitiativeEntry> entries)
    {
        if (entries == null || entries.Count == 0) return "No combatants";

        var parts = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            string name = e.Character.Stats.CharacterName;
            string tag = e.IsPC ? "⚔" : "💀";
            parts.Add($"{tag}{name}({e.Total})");
        }
        return string.Join(" → ", parts);
    }

    /// <summary>
    /// Get a compact initiative string for the turn indicator showing current turn highlighted.
    /// </summary>
    public static string GetInitiativeDisplayString(List<InitiativeEntry> entries, int currentIndex)
    {
        if (entries == null || entries.Count == 0) return "";

        var parts = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Character.Stats.IsDead) continue;

            string name = e.Character.Stats.CharacterName;
            if (i == currentIndex)
                parts.Add($"<color=#FFDD44><b>▶{name}</b></color>");
            else
                parts.Add(e.IsPC ? $"<color=#66FF66>{name}</color>" : $"<color=#FF6666>{name}</color>");
        }
        return "Init: " + string.Join(" → ", parts);
    }
}

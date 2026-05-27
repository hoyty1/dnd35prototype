using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;

// ════════════════════════════════════════════════════════════════════════════
//  Spell Storage Manager — D&D 3.5e Sprint 3 Ring of Spell Storing System
//
//  DMG p.232: "A Ring of Spell Storing contains up to [3/5] levels of
//  spells that the wearer can cast. Each spell has a caster level equal to
//  the minimum level needed to cast it. [...] Any character can cast a
//  spell stored in the ring, provided they can activate magic items."
//
//  Key rules:
//  - Minor: up to 3 spell levels. Major: up to 5 spell levels.
//  - Any spellcaster can store spells (consumes their spell slot).
//  - Anyone wearing the ring can cast stored spells.
//  - Casting uses the ORIGINAL caster's CL and save DC.
//  - Activation time = spell's normal casting time.
//  - No components/focus/XP needed to cast from ring.
//  - No arcane spell failure for armor.
// ════════════════════════════════════════════════════════════════════════════

public static class SpellStorageManager
{
    // ── Query: remaining capacity ──

    /// <summary>
    /// Get total spell levels currently stored in the ring.
    /// </summary>
    public static int GetUsedSpellLevels(ItemData ring)
    {
        if (ring == null || ring.StoredSpells == null) return 0;
        return ring.StoredSpells.Sum(s => s.SpellLevel);
    }

    /// <summary>
    /// Get remaining spell level capacity in the ring.
    /// </summary>
    public static int GetAvailableSpellLevels(ItemData ring)
    {
        if (ring == null) return 0;
        return ring.MaxStoredSpellLevels - GetUsedSpellLevels(ring);
    }

    /// <summary>
    /// Check if a spell of the given level can be stored in the ring.
    /// </summary>
    public static bool CanStoreSpell(ItemData ring, int spellLevel)
    {
        if (ring == null || spellLevel < 1) return false;
        return GetAvailableSpellLevels(ring) >= spellLevel;
    }

    // ── Store a spell ──

    /// <summary>
    /// Store a spell into the Ring of Spell Storing. The caster's spell slot is
    /// consumed externally (this method doesn't consume slots).
    /// Returns true on success.
    /// </summary>
    public static bool StoreSpell(ItemData ring, SpellData spell, CharacterController caster)
    {
        if (ring == null || spell == null || caster == null) return false;
        if (ring.MaxStoredSpellLevels <= 0) return false;

        int spellLevel = spell.SpellLevel;
        if (spellLevel < 1)
        {
            Debug.Log("[SpellStorage] Cannot store cantrips (level 0).");
            if (GameManager.Instance != null)
                GameManager.Instance.CombatUI?.ShowCombatLog(CombatLogHelper.Failure("❌", "Cannot store cantrips in Ring of Spell Storing."));
            return false;
        }

        if (!CanStoreSpell(ring, spellLevel))
        {
            int available = GetAvailableSpellLevels(ring);
            Debug.Log($"[SpellStorage] Not enough capacity: need {spellLevel}, have {available}.");
            if (GameManager.Instance != null)
                GameManager.Instance.CombatUI?.ShowCombatLog(CombatLogHelper.Failure("❌", $"Ring has {available} spell level(s) free, but {spell.Name} requires {spellLevel}."));
            return false;
        }

        // Calculate caster level and save DC at time of storage
        int casterLevel = Mathf.Max(1, caster.Stats != null ? caster.Stats.GetCasterLevel() : 1);
        int saveDC = CalculateStoredSpellDC(spell, caster);

        // Create stored spell entry
        var stored = new StoredSpell(
            spell.SpellId,
            spell.Name,
            casterLevel,
            saveDC,
            spellLevel,
            caster.Stats?.CharacterName ?? "Unknown"
        );

        if (ring.StoredSpells == null)
            ring.StoredSpells = new List<StoredSpell>();

        ring.StoredSpells.Add(stored);

        string ringName = ring.MaxStoredSpellLevels <= 3 ? "Ring of Spell Storing, Minor" : "Ring of Spell Storing, Major";
        string msg = $"💍 {caster.Stats?.CharacterName} stores {stored} in {ringName}. " +
                     $"({GetUsedSpellLevels(ring)}/{ring.MaxStoredSpellLevels} levels used)";
        Debug.Log($"[SpellStorage] {msg}");
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(msg);

        return true;
    }

    // ── Cast a stored spell ──

    /// <summary>
    /// Cast a stored spell from the ring. Anyone wearing the ring can cast it.
    /// The spell uses the original caster's CL and save DC.
    /// Returns true on success.
    /// </summary>
    public static bool CastStoredSpell(ItemData ring, int storedSpellIndex, CharacterController wearer, CharacterController target, out string resultMessage)
    {
        resultMessage = "";
        if (ring == null || wearer == null) return false;
        if (ring.StoredSpells == null || storedSpellIndex < 0 || storedSpellIndex >= ring.StoredSpells.Count)
        {
            resultMessage = "Invalid stored spell selection.";
            return false;
        }

        StoredSpell stored = ring.StoredSpells[storedSpellIndex];
        SpellData spellData = SpellDatabase.GetSpell(stored.SpellId);

        if (spellData == null)
        {
            resultMessage = $"Spell '{stored.SpellName}' not found in database.";
            Debug.LogWarning($"[SpellStorage] Spell {stored.SpellId} not found in SpellDatabase.");
            return false;
        }

        // Remove from ring BEFORE casting (prevents re-use if cast fails)
        ring.StoredSpells.RemoveAt(storedSpellIndex);

        string wearerName = wearer.Stats?.CharacterName ?? "Unknown";
        string ringName = ring.MaxStoredSpellLevels <= 3 ? "Ring of Spell Storing, Minor" : "Ring of Spell Storing, Major";

        var sb = new StringBuilder();
        sb.AppendLine($"💍 {wearerName} casts {stored.SpellName} from {ringName}!");
        sb.AppendLine($"  (Original caster: {stored.StoredBy}, CL {stored.CasterLevel}, DC {stored.SaveDC})");
        sb.AppendLine($"  Ring: {GetUsedSpellLevels(ring)}/{ring.MaxStoredSpellLevels} levels remaining");

        resultMessage = sb.ToString();
        Debug.Log($"[SpellStorage] {wearerName} casts stored {stored.SpellName} (CL {stored.CasterLevel}, DC {stored.SaveDC})");

        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);

        // NOTE: The actual spell resolution would go through GameManager's spell casting pipeline
        // with overridden CL and DC. For the prototype, we log the activation.
        // Full integration would call GameManager.Instance.CastSpellFromRing(wearer, target, spellData, stored.CasterLevel, stored.SaveDC);

        return true;
    }

    // ── Remove a stored spell (without casting) ──

    /// <summary>
    /// Remove a stored spell from the ring without casting it.
    /// </summary>
    public static bool RemoveStoredSpell(ItemData ring, int storedSpellIndex)
    {
        if (ring == null || ring.StoredSpells == null) return false;
        if (storedSpellIndex < 0 || storedSpellIndex >= ring.StoredSpells.Count) return false;

        StoredSpell removed = ring.StoredSpells[storedSpellIndex];
        ring.StoredSpells.RemoveAt(storedSpellIndex);

        Debug.Log($"[SpellStorage] Removed {removed.SpellName} from ring. Capacity: {GetUsedSpellLevels(ring)}/{ring.MaxStoredSpellLevels}");
        return true;
    }

    // ── Display helpers ──

    /// <summary>
    /// Get a formatted string showing all stored spells and capacity.
    /// </summary>
    public static string GetStorageDisplayString(ItemData ring)
    {
        if (ring == null || ring.MaxStoredSpellLevels <= 0) return "";

        var sb = new StringBuilder();
        int used = GetUsedSpellLevels(ring);
        sb.AppendLine($"Spell Storage: {used}/{ring.MaxStoredSpellLevels} levels");

        if (ring.StoredSpells != null && ring.StoredSpells.Count > 0)
        {
            for (int i = 0; i < ring.StoredSpells.Count; i++)
            {
                var spell = ring.StoredSpells[i];
                sb.AppendLine($"  [{i + 1}] {spell}");
            }
        }
        else
        {
            sb.AppendLine("  (empty)");
        }

        return sb.ToString().TrimEnd();
    }

    // ── Internal helpers ──

    /// <summary>
    /// Calculate the save DC for a stored spell based on the caster's stats at time of storage.
    /// D&D 3.5e: DC = 10 + spell level + relevant ability modifier.
    /// </summary>
    private static int CalculateStoredSpellDC(SpellData spell, CharacterController caster)
    {
        if (caster == null || caster.Stats == null || spell == null) return 10 + spell.SpellLevel;

        // Determine casting stat modifier
        int abilityMod = 0;
        if (caster.Stats.HasClass("Wizard"))
            abilityMod = caster.Stats.INTMod;
        else if (caster.Stats.HasClass("Cleric") || caster.Stats.HasClass("Druid") || caster.Stats.HasClass("Ranger"))
            abilityMod = caster.Stats.WISMod;
        else if (caster.Stats.HasClass("Sorcerer") || caster.Stats.HasClass("Bard") || caster.Stats.HasClass("Paladin"))
            abilityMod = caster.Stats.CHAMod;
        else
            abilityMod = Mathf.Max(caster.Stats.INTMod, Mathf.Max(caster.Stats.WISMod, caster.Stats.CHAMod));

        return 10 + spell.SpellLevel + abilityMod;
    }
}

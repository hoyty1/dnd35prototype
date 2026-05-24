using System;
using System.Collections.Generic;
using DND35e.Identifiers;
using UnityEngine;

/// <summary>
/// Reclassifies spells from the original 4-type system (Damage/Healing/Buff/Debuff)
/// into the expanded 13-type system for AI tactical awareness.
/// Called once during spell database initialization.
///
/// Classification rules:
/// - Control: spells that deny actions (Hold Person, Web, Sleep, Color Spray, etc.)
/// - Summon: Summon Monster/Nature's Ally, Summon Swarm
/// - Utility: non-combat spells (Detect Magic, Identify, Knock)
/// - Escape: movement/retreat spells (Dimension Door, Gaseous Form)
/// - Dispel: counter-magic (Dispel Magic, Remove Curse, Break Enchantment)
/// - Wall: area denial barriers (Wall of Fire, Wall of Ice, Wind Wall)
/// - Illusion: concealment/deception (Invisibility, Mirror Image, Displacement, Blur)
/// - Divination: information spells (Detect Evil, See Invisibility, True Seeing)
/// - Existing Damage/Healing/Buff/Debuff categories are preserved for unmatched spells.
/// </summary>
public static class SpellCategoryClassifier
{
    /// <summary>
    /// Reclassify all spells in the database to use expanded effect types.
    /// Call this after SpellDatabase.Init() completes.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public static void ReclassifyAll()
    {
        int reclassified = 0;

        // Iterate all registered spells
        var allSpells = SpellDatabase.GetAllSpells();
        if (allSpells == null) return;

        foreach (var spell in allSpells)
        {
            if (spell == null) continue;

            SpellEffectType newType = ClassifySpell(spell);
            if (newType != spell.EffectType)
            {
                spell.EffectType = newType;
                reclassified++;
            }
        }

        Debug.Log($"[SpellCategoryClassifier] Reclassified {reclassified} spells to expanded effect types.");
    }

    /// <summary>
    /// Determine the correct expanded SpellEffectType for a spell.
    /// Uses spell ID matching first, then heuristic fallbacks.
    /// </summary>
    public static SpellEffectType ClassifySpell(SpellData spell)
    {
        if (spell == null) return SpellEffectType.Damage;

        string id = spell.SpellId ?? "";

        // ── Explicit ID-based classification (highest priority) ──

        // Control spells — action denial
        if (IsControlSpell(id)) return SpellEffectType.Control;

        // Summon spells
        if (IsSummonSpell(id)) return SpellEffectType.Summon;

        // Dispel/counter-magic
        if (IsDispelSpell(id)) return SpellEffectType.Dispel;

        // Wall/area denial
        if (IsWallSpell(id)) return SpellEffectType.Wall;

        // Illusion/concealment
        if (IsIllusionSpell(id)) return SpellEffectType.Illusion;

        // Escape/mobility
        if (IsEscapeSpell(id)) return SpellEffectType.Escape;

        // Divination/information
        if (IsDivinationSpell(id)) return SpellEffectType.Divination;

        // Utility (non-combat)
        if (IsUtilitySpell(id)) return SpellEffectType.Utility;

        // ── Heuristic fallbacks for spells not matched by ID ──

        // Keep existing classification for damage/healing/buff
        if (spell.EffectType == SpellEffectType.Healing) return SpellEffectType.Healing;
        if (spell.EffectType == SpellEffectType.Damage) return SpellEffectType.Damage;
        if (spell.EffectType == SpellEffectType.Buff) return SpellEffectType.Buff;

        // Debuffs: check if they're really control spells
        if (spell.EffectType == SpellEffectType.Debuff)
        {
            // If the spell has no damage and targets Will save → likely control
            if (spell.DamageCount <= 0 && spell.MissileCount <= 0 &&
                !string.IsNullOrEmpty(spell.SavingThrowType) &&
                spell.SavingThrowType.IndexOf("Will", StringComparison.OrdinalIgnoreCase) >= 0 &&
                spell.IsMindAffecting)
                return SpellEffectType.Control;

            return SpellEffectType.Debuff;
        }

        return spell.EffectType;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CLASSIFICATION SETS
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly HashSet<string> ControlSpells = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SpellNames.HOLD_PERSON,
        SpellNames.WEB,
        SpellNames.GREASE,
        SpellNames.STINKING_CLOUD,
        SpellNames.SLEEP,
        SpellNames.DEEP_SLUMBER,
        SpellNames.COLOR_SPRAY,
        SpellNames.COMMAND,
        SpellNames.HIDEOUS_LAUGHTER,
        SpellNames.GLITTERDUST,
        SpellNames.SLOW,
        SpellNames.CONFUSION,
        SpellNames.FEAR,
        SpellNames.CAUSE_FEAR,
        SpellNames.SCARE,
        SpellNames.SILENCE,
        SpellNames.CHARM_PERSON,
        SpellNames.DOMINATE_ANIMAL,
        SpellNames.PHANTASMAL_KILLER,
        SpellNames.BESTOW_CURSE,
        SpellNames.COMMAND_UNDEAD,
        SpellNames.COMMAND_PLANTS,
        SpellNames.DOMAIN_ENTANGLE,
    };

    private static readonly HashSet<string> SummonSpells = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SpellNames.SUMMON_MONSTER_1,
        SpellNames.SUMMON_MONSTER_2,
        SpellNames.SUMMON_MONSTER_3,
        SpellNames.SUMMON_MONSTER_4,
        SpellNames.SUMMON_SWARM,
    };

    private static readonly HashSet<string> DispelSpells = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SpellNames.DISPEL_MAGIC,
        SpellNames.REMOVE_CURSE,
        SpellNames.REMOVE_BLINDNESS_DEAFNESS,
        SpellNames.REMOVE_FEAR,
    };

    private static readonly HashSet<string> WallSpells = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SpellNames.WALL_OF_FIRE,
        SpellNames.WALL_OF_ICE,
        SpellNames.OBSCURING_MIST,
        SpellNames.FOG_CLOUD,
    };

    private static readonly HashSet<string> IllusionSpells = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SpellNames.INVISIBILITY,
        SpellNames.MIRROR_IMAGE,
        SpellNames.DISPLACEMENT,
        SpellNames.INVISIBILITY_SPHERE,
        SpellNames.INVISIBILITY_PURGE,
    };

    private static readonly HashSet<string> EscapeSpells = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SpellNames.DIMENSION_DOOR,
    };

    private static readonly HashSet<string> DivinationSpells = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SpellNames.DETECT_EVIL,
        SpellNames.DETECT_MAGIC_WIZ,
        SpellNames.SEE_INVISIBILITY,
    };

    private static readonly HashSet<string> UtilitySpells = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SpellNames.IDENTIFY,
        SpellNames.KNOCK,
    };

    private static bool IsControlSpell(string id) => ControlSpells.Contains(id);
    private static bool IsSummonSpell(string id) => SummonSpells.Contains(id);
    private static bool IsDispelSpell(string id) => DispelSpells.Contains(id);
    private static bool IsWallSpell(string id) => WallSpells.Contains(id);
    private static bool IsIllusionSpell(string id) => IllusionSpells.Contains(id);
    private static bool IsEscapeSpell(string id) => EscapeSpells.Contains(id);
    private static bool IsDivinationSpell(string id) => DivinationSpells.Contains(id);
    private static bool IsUtilitySpell(string id) => UtilitySpells.Contains(id);
}

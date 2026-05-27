using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// ══════════════════════════════════════════════════════════════════════
//  D&D 3.5e CREATURE TRAPPING SYSTEM — DMG pp. 259–265
//  Supports Iron Flask, Mirror of Life Trapping, Efreeti Bottle,
//  and Stone of Controlling Earth Elementals.
//
//  Creatures are serialized into TrappedCreature snapshots and stored
//  on the containing ItemData. Release restores them to the world.
// ══════════════════════════════════════════════════════════════════════

/// <summary>
/// Snapshot of a creature captured by a trapping item.
/// Stores enough data to display info and restore the creature on release.
/// </summary>
[Serializable]
public class TrappedCreature
{
    // ── Identity ──
    public string CreatureName;
    public string CreatureID;       // NPC database ID or unique identifier
    public int CreatureLevel;
    public string CreatureType;     // "Outsider", "Elemental", "Humanoid", etc.
    public string CreatureSubType;  // "Fire", "Earth", etc.

    // ── Stats (snapshot at capture) ──
    public int MaxHP;
    public int CurrentHP;
    public int AC;
    public int AttackBonus;

    // ── Ability Scores ──
    public int Strength;
    public int Dexterity;
    public int Constitution;
    public int Intelligence;
    public int Wisdom;
    public int Charisma;

    // ── Saves ──
    public int FortitudeSave;
    public int ReflexSave;
    public int WillSave;

    // ── Status ──
    public List<string> ActiveEffects;     // Names of active buffs/debuffs
    public bool IsHostile;                 // Attitude on release
    public float TimeTrapped;              // Time.time when trapped

    // ── Service Tracking ──
    public bool IsServingOwner;
    public float ServiceDurationSeconds;   // Total service (default 3600 = 1 hour)
    public float ServiceTimeRemaining;     // Seconds left in service

    // ── Combat info ──
    public int SpellResistance;
    public int ChallengeRating;
    public string Size;                    // "Small", "Medium", "Large", etc.

    /// <summary>Get a short info string for UI display.</summary>
    public string GetDisplayInfo()
    {
        string typeStr = !string.IsNullOrEmpty(CreatureType) ? CreatureType : "Unknown";
        return $"{CreatureName} ({typeStr}, CR {ChallengeRating})\n" +
               $"HP: {CurrentHP}/{MaxHP}  AC: {AC}\n" +
               $"Str {Strength} Dex {Dexterity} Con {Constitution} Int {Intelligence} Wis {Wisdom} Cha {Charisma}\n" +
               $"Saves: Fort {FortitudeSave} Ref {ReflexSave} Will {WillSave}" +
               (SpellResistance > 0 ? $"  SR {SpellResistance}" : "");
    }

    /// <summary>Get a compact one-line summary.</summary>
    public string GetSummary()
    {
        string typeStr = !string.IsNullOrEmpty(CreatureType) ? CreatureType : "???";
        return $"{CreatureName} (CR {ChallengeRating} {typeStr}, HP {CurrentHP}/{MaxHP})";
    }
}

/// <summary>
/// Result of a trapping attempt (used for UI/logging).
/// </summary>
public class TrapAttemptResult
{
    public bool Success;
    public string CreatureName;
    public int SaveRoll;          // d20 roll
    public int SaveTotal;         // Roll + modifier
    public int SaveDC;
    public string SaveType;       // "Will", "Reflex", etc.
    public string FailureReason;  // "Save succeeded", "Item full", "Type restricted"
    public string LogMessage;
}

/// <summary>
/// Result of releasing a creature from a trap.
/// </summary>
public class ReleaseResult
{
    public bool Success;
    public TrappedCreature ReleasedCreature;
    public bool IsHostile;
    public float ServiceDurationMinutes;
    public string LogMessage;
}

/// <summary>
/// Manager for creature trapping mechanics.
/// Singleton — created by GameManager on startup.
/// </summary>
public class CreatureTrapSystem : MonoBehaviour
{
    public static CreatureTrapSystem Instance { get; private set; }

    /// <summary>All active service timers (creature name → remaining seconds).</summary>
    private Dictionary<string, float> _activeServiceTimers = new Dictionary<string, float>();

    // ════════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log("[CreatureTrapSystem] Initialized.");
    }

    // ════════════════════════════════════════════════════════════
    //  TRAP CREATURE — Core trapping method
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempt to trap a creature in a trapping item.
    /// Performs saving throw, checks capacity and type restrictions,
    /// serializes creature state, and stores in item.
    /// </summary>
    /// <param name="target">The CharacterController of the creature to trap.</param>
    /// <param name="trapItem">The ItemData of the trapping item.</param>
    /// <param name="saveDC">The DC for the saving throw.</param>
    /// <param name="saveType">"Will" or "Reflex".</param>
    /// <param name="allowedTypes">Creature types that can be trapped. Null = any type.</param>
    /// <param name="rangeFeet">Maximum range in feet (0 = unlimited).</param>
    /// <param name="userPosition">Position of the user (for range check).</param>
    /// <returns>TrapAttemptResult with success/failure details.</returns>
    public TrapAttemptResult TrapCreature(
        CharacterController target,
        ItemData trapItem,
        int saveDC,
        string saveType = "Will",
        string[] allowedTypes = null,
        float rangeFeet = 0f,
        Vector3? userPosition = null)
    {
        var result = new TrapAttemptResult
        {
            CreatureName = target?.Stats?.CharacterName ?? "Unknown",
            SaveDC = saveDC,
            SaveType = saveType,
        };

        // ── Null checks ──
        if (target == null || target.Stats == null)
        {
            result.Success = false;
            result.FailureReason = "Invalid target.";
            result.LogMessage = "Trap attempt failed: invalid target.";
            return result;
        }

        if (trapItem == null)
        {
            result.Success = false;
            result.FailureReason = "No trapping item specified.";
            result.LogMessage = "Trap attempt failed: no item.";
            return result;
        }

        // ── Capacity check ──
        if (trapItem.WondrousTrappedCreatures == null)
            trapItem.WondrousTrappedCreatures = new List<TrappedCreature>();

        if (trapItem.WondrousTrappedCreatures.Count >= trapItem.WondrousMaxTrappedCreatures)
        {
            result.Success = false;
            result.FailureReason = $"{trapItem.Name} is full ({trapItem.WondrousTrappedCreatures.Count}/{trapItem.WondrousMaxTrappedCreatures}).";
            result.LogMessage = $"Cannot trap {result.CreatureName}: {trapItem.Name} is already full!";
            Debug.Log($"[CreatureTrap] {result.LogMessage}");
            return result;
        }

        // ── Type restriction check ──
        if (allowedTypes != null && allowedTypes.Length > 0)
        {
            string creatureType = target.Stats.CreatureType ?? "Unknown";
            bool typeAllowed = false;
            foreach (string allowed in allowedTypes)
            {
                if (string.Equals(creatureType, allowed, StringComparison.OrdinalIgnoreCase))
                {
                    typeAllowed = true;
                    break;
                }
            }
            if (!typeAllowed)
            {
                result.Success = false;
                result.FailureReason = $"Cannot trap {creatureType} creatures in this item (allowed: {string.Join(", ", allowedTypes)}).";
                result.LogMessage = $"Cannot trap {result.CreatureName}: {result.FailureReason}";
                Debug.Log($"[CreatureTrap] {result.LogMessage}");
                return result;
            }
        }

        // ── Range check ──
        if (rangeFeet > 0f && userPosition.HasValue)
        {
            float distance = Vector3.Distance(userPosition.Value, target.transform.position);
            // Approximate: 1 Unity unit ≈ 5 ft in grid
            float rangeCells = rangeFeet / 5f;
            if (distance > rangeCells)
            {
                result.Success = false;
                result.FailureReason = $"Target is out of range ({rangeFeet} ft).";
                result.LogMessage = $"Cannot trap {result.CreatureName}: out of range!";
                Debug.Log($"[CreatureTrap] {result.LogMessage}");
                return result;
            }
        }

        // ── Saving throw ──
        int saveBonus = 0;
        switch (saveType)
        {
            case "Will": saveBonus = target.Stats.WillSave; break;
            case "Reflex": saveBonus = target.Stats.ReflexSave; break;
            case "Fort": saveBonus = target.Stats.FortitudeSave; break;
        }

        int saveRoll = DiceRoller.D20();
        int saveTotal = saveRoll + saveBonus;
        result.SaveRoll = saveRoll;
        result.SaveTotal = saveTotal;

        if (saveRoll != 1 && (saveRoll == 20 || saveTotal >= saveDC))
        {
            result.Success = false;
            result.FailureReason = "Saving throw succeeded.";
            result.LogMessage = $"{result.CreatureName} resists trapping! {saveType} save: d20({saveRoll}) + {saveBonus} = {saveTotal} vs DC {saveDC} — PASSED";
            Debug.Log($"[CreatureTrap] {result.LogMessage}");
            return result;
        }

        // ── Success! Serialize and trap ──
        TrappedCreature trapped = SerializeCreature(target);
        trapItem.WondrousTrappedCreatures.Add(trapped);

        // Remove creature from game world
        target.gameObject.SetActive(false);

        result.Success = true;
        result.LogMessage = $"{result.CreatureName} is TRAPPED in {trapItem.Name}! {saveType} save: d20({saveRoll}) + {saveBonus} = {saveTotal} vs DC {saveDC} — FAILED ({trapItem.WondrousTrappedCreatures.Count}/{trapItem.WondrousMaxTrappedCreatures})";
        Debug.Log($"[CreatureTrap] {result.LogMessage}");
        return result;
    }

    // ════════════════════════════════════════════════════════════
    //  SERIALIZE CREATURE — Capture complete state
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Create a TrappedCreature snapshot from a living CharacterController.
    /// Captures all stats, saves, HP, ability scores, effects.
    /// </summary>
    public TrappedCreature SerializeCreature(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return null;

        var stats = character.Stats;
        var trapped = new TrappedCreature
        {
            CreatureName = stats.CharacterName ?? character.name,
            CreatureID = character.name, // Use GameObject name as fallback ID
            CreatureLevel = stats.Level,
            CreatureType = stats.CreatureType ?? "Unknown",
            CreatureSubType = "", // Could be extended later

            MaxHP = stats.MaxHP,
            CurrentHP = stats.CurrentHP,
            AC = stats.ArmorClass,
            AttackBonus = stats.BaseAttackBonus,

            Strength = stats.STR,
            Dexterity = stats.DEX,
            Constitution = stats.CON,
            Intelligence = stats.INT,
            Wisdom = stats.WIS,
            Charisma = stats.CHA,

            FortitudeSave = stats.FortitudeSave,
            ReflexSave = stats.ReflexSave,
            WillSave = stats.WillSave,

            ActiveEffects = new List<string>(), // Could capture actual effects
            IsHostile = false,
            TimeTrapped = Time.time,

            IsServingOwner = false,
            ServiceDurationSeconds = 3600f, // Default 1 hour
            ServiceTimeRemaining = 0f,

            SpellResistance = stats.SpellResistance,
            Size = stats.SizeCategoryName ?? "Medium",
        };

        // Try to parse CR from stats
        if (!string.IsNullOrEmpty(stats.ChallengeRating))
        {
            int.TryParse(stats.ChallengeRating, out trapped.ChallengeRating);
        }

        Debug.Log($"[CreatureTrap] Serialized: {trapped.CreatureName} (CR {trapped.ChallengeRating}, HP {trapped.CurrentHP}/{trapped.MaxHP})");
        return trapped;
    }

    // ════════════════════════════════════════════════════════════
    //  RELEASE CREATURE — Restore from trap
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Release a trapped creature from an item.
    /// </summary>
    /// <param name="trapItem">The containing item.</param>
    /// <param name="creatureIndex">Index in the TrappedCreatures list.</param>
    /// <param name="isHostile">If true, creature attacks immediately. If false, serves owner.</param>
    /// <param name="serviceDurationMinutes">Service duration (0 = no service, just release).</param>
    /// <param name="removeFromItem">If true, remove from item's list after release.</param>
    /// <returns>ReleaseResult with details.</returns>
    public ReleaseResult ReleaseCreature(
        ItemData trapItem,
        int creatureIndex,
        bool isHostile,
        float serviceDurationMinutes = 60f,
        bool removeFromItem = true)
    {
        var result = new ReleaseResult { IsHostile = isHostile, ServiceDurationMinutes = serviceDurationMinutes };

        if (trapItem == null || trapItem.WondrousTrappedCreatures == null)
        {
            result.Success = false;
            result.LogMessage = "No trapping item or empty creature list.";
            return result;
        }

        if (creatureIndex < 0 || creatureIndex >= trapItem.WondrousTrappedCreatures.Count)
        {
            result.Success = false;
            result.LogMessage = $"Invalid creature index ({creatureIndex}).";
            return result;
        }

        TrappedCreature trapped = trapItem.WondrousTrappedCreatures[creatureIndex];
        result.ReleasedCreature = trapped;

        // Set attitude
        trapped.IsHostile = isHostile;

        if (!isHostile && serviceDurationMinutes > 0)
        {
            trapped.IsServingOwner = true;
            trapped.ServiceDurationSeconds = serviceDurationMinutes * 60f;
            trapped.ServiceTimeRemaining = trapped.ServiceDurationSeconds;

            // Track service timer
            _activeServiceTimers[trapped.CreatureName] = trapped.ServiceTimeRemaining;
        }

        // Remove from item (for single-capacity items, or individual release from Mirror)
        if (removeFromItem)
        {
            trapItem.WondrousTrappedCreatures.RemoveAt(creatureIndex);
        }

        string attitude = isHostile ? "HOSTILE" : $"friendly (serves {serviceDurationMinutes} min)";
        result.Success = true;
        result.LogMessage = $"{trapped.CreatureName} released from {trapItem.Name} — {attitude}! " +
                           $"({trapItem.WondrousTrappedCreatures.Count}/{trapItem.WondrousMaxTrappedCreatures} remaining)";
        Debug.Log($"[CreatureTrap] {result.LogMessage}");
        return result;
    }

    /// <summary>
    /// Release ALL creatures from an item (Mirror of Life Trapping "break mirror" scenario).
    /// </summary>
    /// <param name="trapItem">The item containing creatures.</param>
    /// <param name="isHostile">Whether all released creatures are hostile.</param>
    /// <returns>List of release results.</returns>
    public List<ReleaseResult> ReleaseAllCreatures(ItemData trapItem, bool isHostile)
    {
        var results = new List<ReleaseResult>();
        if (trapItem?.WondrousTrappedCreatures == null) return results;

        int count = trapItem.WondrousTrappedCreatures.Count;
        // Release backwards to avoid index shifting
        for (int i = count - 1; i >= 0; i--)
        {
            var result = ReleaseCreature(trapItem, i, isHostile, 0f, true);
            results.Add(result);
        }

        Debug.Log($"[CreatureTrap] Released ALL {count} creatures from {trapItem.Name} ({(isHostile ? "HOSTILE" : "friendly")})!");
        return results;
    }

    // ════════════════════════════════════════════════════════════
    //  CREATURE INFO — Display and query
    // ════════════════════════════════════════════════════════════

    /// <summary>Get detailed info about a trapped creature (for UI display).</summary>
    public string GetTrappedCreatureInfo(TrappedCreature creature)
    {
        if (creature == null) return "Empty";
        return creature.GetDisplayInfo();
    }

    /// <summary>Get summary list of all trapped creatures in an item (for tooltips).</summary>
    public string GetTrappedCreatureList(ItemData item)
    {
        if (item?.WondrousTrappedCreatures == null || item.WondrousTrappedCreatures.Count == 0)
            return "Empty";

        var lines = new List<string>();
        for (int i = 0; i < item.WondrousTrappedCreatures.Count; i++)
        {
            lines.Add($"  {i + 1}. {item.WondrousTrappedCreatures[i].GetSummary()}");
        }
        return string.Join("\n", lines);
    }

    // ════════════════════════════════════════════════════════════
    //  SERVICE TIMER TRACKING
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Update service timers each frame. Call from Update if service tracking is needed.
    /// In practice, D&D 3.5e tracks in-game time, not real time. This provides a framework.
    /// </summary>
    public void UpdateServiceTimers(float deltaTimeSeconds)
    {
        if (_activeServiceTimers.Count == 0) return;

        var expired = new List<string>();
        var keys = new List<string>(_activeServiceTimers.Keys);

        foreach (var name in keys)
        {
            _activeServiceTimers[name] -= deltaTimeSeconds;
            if (_activeServiceTimers[name] <= 0f)
            {
                expired.Add(name);
            }
        }

        foreach (var name in expired)
        {
            _activeServiceTimers.Remove(name);
            Debug.Log($"[CreatureTrap] {name}'s service has ended! Creature departs or becomes hostile.");
        }
    }

    /// <summary>Check if a creature is currently in service.</summary>
    public bool IsCreatureServing(string creatureName)
    {
        return _activeServiceTimers.ContainsKey(creatureName);
    }

    /// <summary>Get remaining service time in minutes.</summary>
    public float GetServiceTimeRemaining(string creatureName)
    {
        if (_activeServiceTimers.ContainsKey(creatureName))
            return _activeServiceTimers[creatureName] / 60f;
        return 0f;
    }

    /// <summary>End a creature's service early (e.g., efreeti returns to bottle).</summary>
    public void EndService(string creatureName)
    {
        if (_activeServiceTimers.Remove(creatureName))
            Debug.Log($"[CreatureTrap] {creatureName}'s service ended early.");
    }

    // ════════════════════════════════════════════════════════════
    //  SUMMONING HELPERS — For Stone of Controlling Earth Elementals
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Create a pre-built TrappedCreature representing an Elder Earth Elemental (24 HD).
    /// Used when the Stone of Controlling Earth Elementals summons from scratch.
    /// DMG p.267: Elder Earth Elemental has 24 HD, 228 HP, AC 22, etc.
    /// </summary>
    public static TrappedCreature CreateElderEarthElementalData()
    {
        return new TrappedCreature
        {
            CreatureName = "Elder Earth Elemental",
            CreatureID = "elder_earth_elemental",
            CreatureLevel = 24,
            CreatureType = "Elemental",
            CreatureSubType = "Earth",
            MaxHP = 228,
            CurrentHP = 228,
            AC = 22,
            AttackBonus = 26,
            Strength = 33,
            Dexterity = 8,
            Constitution = 21,
            Intelligence = 10,
            Wisdom = 11,
            Charisma = 11,
            FortitudeSave = 19,
            ReflexSave = 5,
            WillSave = 8,
            SpellResistance = 0,
            ChallengeRating = 11,
            Size = "Huge",
            ActiveEffects = new List<string> { "Earth Mastery", "Push", "Earth Glide" },
            IsHostile = false,
            TimeTrapped = 0f,
            IsServingOwner = false,
            ServiceDurationSeconds = 3600f,
            ServiceTimeRemaining = 3600f,
        };
    }

    /// <summary>
    /// Create a pre-built TrappedCreature representing an Efreeti.
    /// DMG p.254: Efreeti has 10 HD, 65 HP, AC 18, etc.
    /// </summary>
    public static TrappedCreature CreateEfreetiData()
    {
        return new TrappedCreature
        {
            CreatureName = "Efreeti",
            CreatureID = "efreeti",
            CreatureLevel = 10,
            CreatureType = "Outsider",
            CreatureSubType = "Fire",
            MaxHP = 65,
            CurrentHP = 65,
            AC = 18,
            AttackBonus = 15,
            Strength = 23,
            Dexterity = 17,
            Constitution = 16,
            Intelligence = 12,
            Wisdom = 15,
            Charisma = 15,
            FortitudeSave = 10,
            ReflexSave = 10,
            WillSave = 9,
            SpellResistance = 0,
            ChallengeRating = 8,
            Size = "Large",
            ActiveEffects = new List<string> { "Plane Shift", "Scorching Ray 3/day", "Wall of Fire 1/day", "Grant Wishes" },
            IsHostile = false,
            TimeTrapped = 0f,
            IsServingOwner = false,
            ServiceDurationSeconds = 3600f,
            ServiceTimeRemaining = 3600f,
        };
    }

    // ════════════════════════════════════════════════════════════
    //  CONTROL EARTH ELEMENTALS — Stone ability
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempt to control all earth elementals within range.
    /// Will save DC 18 to resist. Control lasts 1 hour.
    /// Returns list of controlled creature names.
    /// </summary>
    public List<string> ControlEarthElementals(Vector3 userPosition, float rangeFeet, int saveDC)
    {
        var controlled = new List<string>();
        float rangeCells = rangeFeet / 5f;

        // Find all CharacterControllers in the scene
        var allCharacters = FindObjectsOfType<CharacterController>();
        foreach (var character in allCharacters)
        {
            if (character == null || character.Stats == null) continue;
            if (!character.gameObject.activeInHierarchy) continue;

            string creatureType = character.Stats.CreatureType ?? "";
            if (!creatureType.Equals("Elemental", StringComparison.OrdinalIgnoreCase)) continue;

            // Check if earth elemental (subtype check via name or tags)
            string charName = character.Stats.CharacterName?.ToLower() ?? "";
            bool isEarth = charName.Contains("earth");
            if (!isEarth) continue;

            // Range check
            float distance = Vector3.Distance(userPosition, character.transform.position);
            if (distance > rangeCells) continue;

            // Will save
            int saveRoll = DiceRoller.D20();
            int saveTotal = saveRoll + character.Stats.WillSave;

            if (saveRoll != 20 && (saveRoll == 1 || saveTotal < saveDC))
            {
                controlled.Add(character.Stats.CharacterName);
                Debug.Log($"[CreatureTrap] {character.Stats.CharacterName} is now under control! Will save: {saveTotal} vs DC {saveDC} — FAILED");
            }
            else
            {
                Debug.Log($"[CreatureTrap] {character.Stats.CharacterName} resisted control! Will save: {saveTotal} vs DC {saveDC} — PASSED");
            }
        }

        if (controlled.Count > 0)
            Debug.Log($"[CreatureTrap] Controlled {controlled.Count} earth elemental(s) for 1 hour.");
        else
            Debug.Log("[CreatureTrap] No earth elementals were controlled.");

        return controlled;
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Manages spellcasting for a character: known/prepared spells, spell slots, and casting.
/// Attached to a character GameObject alongside CharacterController and InventoryComponent.
///
/// D&D 3.5e Spell Preparation System:
///   Both Wizards and Clerics use slot-based preparation.
///   Each slot holds ONE specific spell. The same spell can fill multiple slots.
///   Example: Level 3 wizard with 3 level-1 slots: [Magic Missile, Magic Missile, Shield]
///   Casting consumes a specific prepared slot. After rest, slots are restored.
///   Cantrips (level 0) are UNLIMITED — they never consume slots when cast.
///
///   Wizards prepare from their spellbook (limited selection).
///   Clerics prepare from the FULL list of cleric spells at each level.
///
/// D&D 3.5e Spell Slots at Level 3:
///   Wizard: 4/2+bonus/1+bonus (cantrips/1st/2nd)
///   Cleric: 4/2+1domain+bonus/1+1domain+bonus (cantrips/1st/2nd)
///
/// Bonus spell slots from ability score:
///   Wizard: INT modifier determines bonus slots
///   Cleric: WIS modifier determines bonus slots
///   Bonus = 1 extra slot per spell level where (ability mod >= spell level)
/// </summary>
public class SpellcastingComponent : MonoBehaviour
{
    /// <summary>Reference to the character stats.</summary>
    public CharacterStats Stats { get; private set; }

    /// <summary>List of all spells this character knows/has in spellbook.</summary>
    public List<SpellData> KnownSpells = new List<SpellData>();

    /// <summary>
    /// List of spells currently prepared for casting (backward compatibility).
    /// For both Wizards and Clerics, the primary system is SpellSlots.
    /// This list is synced from SpellSlots for UI and combat system backward compatibility.
    /// </summary>
    public List<SpellData> PreparedSpells { get; private set; } = new List<SpellData>();

    /// <summary>
    /// D&D 3.5e Spell Slots for both Wizards and Clerics.
    /// Each slot holds a specific spell and tracks whether it's been cast.
    /// Wizards fill slots from their spellbook. Clerics fill from the full cleric spell list.
    /// The same spell can be prepared in multiple slots.
    /// Cantrip (level 0) slots are never marked as used — cantrips are unlimited.
    /// </summary>
    public List<SpellSlot> SpellSlots { get; private set; } = new List<SpellSlot>();

    /// <summary>
    /// Spell slots remaining per spell level. Index = spell level.
    /// [0] = cantrip slots, [1] = 1st level slots, [2] = 2nd level slots, etc.
    /// Derived from SpellSlots for both Wizards and Clerics.
    /// </summary>
    public int[] SlotsRemaining;

    /// <summary>Maximum spell slots per level.</summary>
    public int[] SlotsMax;

    /// <summary>Active buffs from spells (spell ID → rounds remaining, -1 = permanent/long duration).</summary>
    public Dictionary<string, int> ActiveBuffs = new Dictionary<string, int>();

    /// <summary>Mage Armor AC bonus currently active.</summary>
    public int MageArmorACBonus { get; set; }

    /// <summary>Whether Mage Armor buff is currently active.</summary>
    public bool MageArmorActive { get; set; }

    /// <summary>
    /// D&D 3.5e Rule: Only one quickened spell per round.
    /// Tracks whether this character has already cast a quickened spell this round.
    /// Reset at the start of each new round (when PC1's turn begins after NPC turn).
    /// </summary>
    public bool HasCastQuickenedSpellThisRound { get; set; }


    // ========== HELD TOUCH SPELL CHARGE ==========
    /// <summary>Currently held melee touch spell charge (if any).</summary>
    public SpellData HeldTouchSpell { get; private set; }

    /// <summary>Metamagic data that was applied when the held touch spell was cast.</summary>
    public MetamagicData HeldTouchMetamagic { get; private set; }

    /// <summary>Whether this character is currently holding a melee touch spell charge.</summary>
    public bool HasHeldTouchCharge => HeldTouchSpell != null;
    /// <summary>Whether this character has any supported spellcasting ability.</summary>
    public bool CanCastSpells => UsesPreparedSlotSystem;

    private static bool IsDruidClass(CharacterStats stats)
    {
        return stats != null && stats.HasClass("Druid");
    }

    private bool UsesPreparedSlotSystem => Stats != null && (Stats.IsWizard || Stats.IsCleric || IsDruidClass(Stats));

    private string GetActivePreparedCasterClass(CharacterStats stats)
    {
        if (stats == null)
            return string.Empty;

        string currentClass = stats.CharacterClass ?? string.Empty;
        if (string.Equals(currentClass, "Wizard", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentClass, "Cleric", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentClass, "Druid", System.StringComparison.OrdinalIgnoreCase))
            return currentClass;

        string[] preparedClasses = { "Wizard", "Cleric", "Druid" };
        string best = string.Empty;
        int bestLevel = -1;
        for (int i = 0; i < preparedClasses.Length; i++)
        {
            int classLevel = stats.GetClassLevel(preparedClasses[i]);
            if (classLevel > bestLevel)
            {
                best = preparedClasses[i];
                bestLevel = classLevel;
            }
        }

        return bestLevel > 0 ? best : string.Empty;
    }

    private int GetActivePreparedCasterLevel(CharacterStats stats, string className)
    {
        if (stats == null || string.IsNullOrWhiteSpace(className))
            return 0;

        int classLevel = stats.GetClassLevel(className);
        return Mathf.Max(1, classLevel);
    }

    private readonly Dictionary<string, List<SpellData>> _knownSpellsByClass = new Dictionary<string, List<SpellData>>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int[]> _slotsMaxByClass = new Dictionary<string, int[]>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int[]> _slotsRemainingByClass = new Dictionary<string, int[]>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int[]> _regularSlotsMaxByClass = new Dictionary<string, int[]>(System.StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int[]> _domainSlotsMaxByClass = new Dictionary<string, int[]>(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _preparedCasterClasses = new List<string>();
    private string _activePreparationClassName = string.Empty;

    public List<string> GetPreparedCasterClassNames()
    {
        if (_preparedCasterClasses.Count > 0)
            return new List<string>(_preparedCasterClasses);

        var result = new List<string>();
        if (Stats == null)
            return result;

        if (Stats.GetClassLevel("Wizard") > 0) result.Add("Wizard");
        if (Stats.GetClassLevel("Cleric") > 0) result.Add("Cleric");
        if (Stats.GetClassLevel("Druid") > 0) result.Add("Druid");
        return result;
    }

    public bool HasPreparedCasterClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return false;
        return GetPreparedCasterClassNames().Any(c => string.Equals(c, className, System.StringComparison.OrdinalIgnoreCase));
    }

    public bool HasPreparedSpellForClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return false;

        return SpellSlots.Any(slot =>
            slot != null &&
            string.Equals(slot.CasterClassName, className, System.StringComparison.OrdinalIgnoreCase) &&
            slot.HasSpell &&
            !slot.DisabledByNegativeLevel);
    }

    public List<string> GetUnpreparedCasterClassNames()
    {
        List<string> result = new List<string>();
        foreach (string className in GetPreparedCasterClassNames())
        {
            if (!HasPreparedSpellForClass(className))
                result.Add(className);
        }

        return result;
    }

    public List<SpellData> GetKnownSpellsForClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return new List<SpellData>();

        if (_knownSpellsByClass.TryGetValue(className, out List<SpellData> spells) && spells != null)
            return spells.Where(s => s != null).ToList();

        return KnownSpells.Where(s => s != null).ToList();
    }

    public List<SpellSlot> GetSlotsForClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return new List<SpellSlot>();

        return SpellSlots
            .Where(s => s != null && string.Equals(s.CasterClassName, className, System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Level)
            .ThenBy(s => s.IsDomainSlot ? 2 : s.IsSpecialistSlot ? 1 : 0)
            .ToList();
    }

    public List<SpellSlot> GetSlotsForClassAtLevel(string className, int level)
    {
        return GetSlotsForClass(className)
            .Where(s => s.Level == level)
            .ToList();
    }

    public int[] GetSlotsMaxForClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return new int[0];

        if (_slotsMaxByClass.TryGetValue(className, out int[] slots) && slots != null)
            return (int[])slots.Clone();

        return new int[0];
    }

    public int[] GetSlotsRemainingForClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return new int[0];

        if (_slotsRemainingByClass.TryGetValue(className, out int[] slots) && slots != null)
            return (int[])slots.Clone();

        return new int[0];
    }

    public int GetMaxSpellSlotsAtLevelForClass(string className, int spellLevel)
    {
        if (string.IsNullOrWhiteSpace(className))
            return 0;

        if (!_regularSlotsMaxByClass.TryGetValue(className, out int[] regular) || regular == null)
            return 0;

        if (spellLevel < 0 || spellLevel >= regular.Length)
            return 0;

        return regular[spellLevel];
    }

    public int GetMaxDomainSlotsAtLevelForClass(string className, int spellLevel)
    {
        if (string.IsNullOrWhiteSpace(className))
            return 0;

        if (!_domainSlotsMaxByClass.TryGetValue(className, out int[] domain) || domain == null)
            return 0;

        if (spellLevel < 0 || spellLevel >= domain.Length)
            return 0;

        return domain[spellLevel];
    }

    private bool IsSpellKnownByClass(string className, SpellData spell)
    {
        if (spell == null || string.IsNullOrWhiteSpace(className))
            return false;

        List<SpellData> classKnown = GetKnownSpellsForClass(className);
        return classKnown.Any(s => s != null && s.SpellId == spell.SpellId);
    }

    public bool ClassKnowsAllSpells(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return false;

        return string.Equals(className, "Cleric", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(className, "Druid", System.StringComparison.OrdinalIgnoreCase);
    }

    public void ResetKnownSpellsForClass(string className, bool keepCantrips)
    {
        if (string.IsNullOrWhiteSpace(className))
            return;

        if (!_knownSpellsByClass.TryGetValue(className, out List<SpellData> classKnown) || classKnown == null)
            classKnown = new List<SpellData>();

        if (keepCantrips)
        {
            SpellDatabase.Init();
            classKnown = SpellDatabase.GetSpellsForClassAtLevel(className, 0)
                .Where(s => s != null)
                .GroupBy(s => s.SpellId)
                .Select(g => g.First())
                .ToList();
        }
        else
        {
            classKnown = new List<SpellData>();
        }

        _knownSpellsByClass[className] = classKnown;

        KnownSpells.Clear();
        foreach (var kvp in _knownSpellsByClass)
        {
            if (kvp.Value == null)
                continue;

            for (int i = 0; i < kvp.Value.Count; i++)
            {
                SpellData spell = kvp.Value[i];
                if (spell != null && !KnownSpells.Any(s => s != null && s.SpellId == spell.SpellId))
                    KnownSpells.Add(spell);
            }
        }
    }


    private void SyncDomainsFromStats()
    {
        Domains.Clear();
        if (Stats?.ChosenDomains == null)
            return;

        for (int i = 0; i < Stats.ChosenDomains.Count; i++)
        {
            string domain = Stats.ChosenDomains[i];
            if (!string.IsNullOrWhiteSpace(domain) && !Domains.Contains(domain))
                Domains.Add(domain);
        }

        if (Domains.Count > 0)
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Domains loaded [{string.Join(", ", Domains)}]");
    }

    /// <summary>
    /// SpellIds selected during character creation.
    /// For Wizards: includes cantrips + 1st/2nd level spells for the spellbook.
    /// For Clerics: includes selected orisons only (higher-level spells are always available).
    /// If set before Init(), only these spells will be known (plus all higher-level Cleric spells).
    /// If null, all available spells are added (backwards compatibility).
    /// </summary>
    public List<string> SelectedSpellIds { get; set; }

    /// <summary>
    /// Spell IDs prepared in each slot during character creation (in slot order).
    /// If set before Init(), these will override auto-preparation for both Wizards and Clerics.
    /// Empty strings indicate empty slots.
    /// Order: [cantrip slots, then level 1 slots, then level 2 slots, ...]
    /// </summary>
    public List<string> PreparedSpellSlotIds { get; set; }

    /// <summary>
    /// Cleric domain names for this character (e.g., "Good", "Healing").
    /// Derived from CharacterStats.ChosenDomains when available.
    /// </summary>
    public List<string> Domains { get; private set; } = new List<string>();

    /// <summary>Regular (non-domain) slots by spell level.</summary>
    private int[] _regularSlotsMax = new int[0];

    /// <summary>Domain slots by spell level (typically 1 at each level 1+ that the cleric can cast).</summary>
    private int[] _domainSlotsMax = new int[0];

    /// <summary>
    /// Initialize spellcasting for a character based on their class and level.
    /// Supports multiclass prepared casters by tracking slots/spells per class.
    /// </summary>
    public void Init(CharacterStats stats)
    {
        Stats = stats;
        SyncDomainsFromStats();
        SpellDatabase.Init();

        KnownSpells.Clear();
        PreparedSpells.Clear();
        SpellSlots.Clear();
        _knownSpellsByClass.Clear();
        _slotsMaxByClass.Clear();
        _slotsRemainingByClass.Clear();
        _regularSlotsMaxByClass.Clear();
        _domainSlotsMaxByClass.Clear();
        _preparedCasterClasses.Clear();

        string[] classOrder = { "Wizard", "Cleric", "Druid" };
        for (int i = 0; i < classOrder.Length; i++)
        {
            string className = classOrder[i];
            int classLevel = GetActivePreparedCasterLevel(stats, className);
            if (classLevel <= 0)
                continue;

            if (stats == null || stats.GetClassLevel(className) <= 0)
                continue;

            InitializePreparedCasterClass(className, classLevel);
            _preparedCasterClasses.Add(className);
        }

        _activePreparationClassName = _preparedCasterClasses.Count > 0
            ? _preparedCasterClasses[0]
            : string.Empty;

        if (_preparedCasterClasses.Count == 1 && PreparedSpellSlotIds != null)
        {
            ApplyPreparedSpellSlotIds();
            Debug.Log($"[Spellcasting] {stats.CharacterName}: Applied explicit preparation data ({PreparedSpellSlotIds.Count} entries). Auto-prepare skipped.");
        }
        else
        {
            for (int i = 0; i < _preparedCasterClasses.Count; i++)
            {
                string className = _preparedCasterClasses[i];
                if (string.Equals(className, "Wizard", System.StringComparison.OrdinalIgnoreCase))
                    AutoPrepareWizardSlots(className);
                else if (string.Equals(className, "Cleric", System.StringComparison.OrdinalIgnoreCase))
                    AutoPrepareClericSlots(className);
                else if (string.Equals(className, "Druid", System.StringComparison.OrdinalIgnoreCase))
                    AutoPrepareDruidSlots(className);
            }
        }

        ApplyNegativeLevelSlotLoss();
        SyncPreparedSpellsFromSlots();
        SyncPrimaryClassLegacyView();

        string classSummary = _preparedCasterClasses.Count > 0 ? string.Join("/", _preparedCasterClasses) : "None";
        Debug.Log($"[Spellcasting] {stats.CharacterName} ({classSummary}): {KnownSpells.Count} known, {PreparedSpells.Count} prepared, slots: {GetSlotSummary()}");

        if (_preparedCasterClasses.Count > 0)
            Debug.Log($"[Spellcasting] {stats.CharacterName} slot details: {GetSlotDetails()}");
    }

    private void InitializePreparedCasterClass(string className, int level)
    {
        if (string.IsNullOrWhiteSpace(className))
            return;

        if (!_knownSpellsByClass.ContainsKey(className))
            _knownSpellsByClass[className] = new List<SpellData>();

        if (string.Equals(className, "Wizard", System.StringComparison.OrdinalIgnoreCase))
            InitWizard(level, className);
        else if (string.Equals(className, "Cleric", System.StringComparison.OrdinalIgnoreCase))
            InitCleric(level, className);
        else if (string.Equals(className, "Druid", System.StringComparison.OrdinalIgnoreCase))
            InitDruid(level, className);
    }

    private void SyncPrimaryClassLegacyView()
    {
        if (_preparedCasterClasses.Count == 0)
        {
            SlotsMax = new int[0];
            SlotsRemaining = new int[0];
            _regularSlotsMax = new int[0];
            _domainSlotsMax = new int[0];
            return;
        }

        string className = _preparedCasterClasses[0];
        _activePreparationClassName = className;
        SlotsMax = GetSlotsMaxForClass(className);
        SlotsRemaining = GetSlotsRemainingForClass(className);
        _regularSlotsMax = _regularSlotsMaxByClass.TryGetValue(className, out int[] regular) ? (int[])regular.Clone() : new int[0];
        _domainSlotsMax = _domainSlotsMaxByClass.TryGetValue(className, out int[] domain) ? (int[])domain.Clone() : new int[0];
    }

    private void AddKnownSpellForClass(string className, SpellData spell)
    {
        if (string.IsNullOrWhiteSpace(className) || spell == null)
            return;

        if (!_knownSpellsByClass.TryGetValue(className, out List<SpellData> classKnown) || classKnown == null)
        {
            classKnown = new List<SpellData>();
            _knownSpellsByClass[className] = classKnown;
        }

        if (!classKnown.Any(s => s != null && s.SpellId == spell.SpellId))
            classKnown.Add(spell);

        if (!KnownSpells.Any(s => s != null && s.SpellId == spell.SpellId))
            KnownSpells.Add(spell);
    }

    private void AppendSpellSlotsForClass(string className)
    {
        int[] slotsMax = GetSlotsMaxForClass(className);
        if (slotsMax == null || slotsMax.Length == 0)
            return;

        bool isWizardSpecialist = string.Equals(className, "Wizard", System.StringComparison.OrdinalIgnoreCase)
            && Stats != null && Stats.IsWizardSpecialist;

        for (int spellLevel = 0; spellLevel < slotsMax.Length; spellLevel++)
        {
            int regularSlots = GetMaxSpellSlotsAtLevelForClass(className, spellLevel);
            int bonusSlots = GetMaxDomainSlotsAtLevelForClass(className, spellLevel);

            for (int i = 0; i < regularSlots; i++)
                SpellSlots.Add(new SpellSlot(spellLevel, isDomainSlot: false, casterClassName: className));

            for (int i = 0; i < bonusSlots; i++)
            {
                if (isWizardSpecialist)
                {
                    // Wizard specialist bonus slots: marked as specialist, not domain
                    SpellSlots.Add(new SpellSlot(spellLevel, isDomainSlot: false, casterClassName: className, isSpecialistSlot: true));
                }
                else
                {
                    // Cleric/Druid domain slots
                    SpellSlots.Add(new SpellSlot(spellLevel, isDomainSlot: true, casterClassName: className));
                }
            }
        }
    }

    /// <summary>
    /// Initialize Wizard spellcasting with D&D 3.5e slot-based preparation.
    /// Creates individual SpellSlot objects for each available slot.
    /// </summary>
    private void InitWizard(int level, string className = "Wizard")
    {
        int safeLevel = Mathf.Max(1, level);
        int[] slots = GetWizardSlotsForLevel(safeLevel);
        _slotsMaxByClass[className] = (int[])slots.Clone();
        _slotsRemainingByClass[className] = (int[])slots.Clone();
        _regularSlotsMaxByClass[className] = (int[])_regularSlotsMax.Clone();
        _domainSlotsMaxByClass[className] = (int[])_domainSlotsMax.Clone();
        AppendSpellSlotsForClass(className);

        Debug.Log($"[Spellcasting] {className} created {GetSlotsForClass(className).Count} spell slots at level {safeLevel}: {GetSlotArrayDebugString(slots)}");

        if (SelectedSpellIds != null && SelectedSpellIds.Count > 0)
        {
            int loadedCount = 0;
            foreach (string spellId in SelectedSpellIds)
            {
                SpellData spell = SpellDatabase.GetSpell(spellId);
                if (spell == null || !spell.IsAvailableFor("Wizard", spell.SpellLevel))
                    continue;

                AddKnownSpellForClass(className, spell);
                loadedCount++;
            }
            Debug.Log($"[Spellcasting] {className} loaded {loadedCount} selected wizard spells into spellbook.");
        }
        else
        {
            // Always grant wizard cantrips.
            foreach (SpellData spell in SpellDatabase.GetSpellsForClassAtLevel("Wizard", 0))
                AddKnownSpellForClass(className, spell);

            bool hasOtherPreparedCaster = Stats != null &&
                ((Stats.GetClassLevel("Cleric") > 0) || (Stats.GetClassLevel("Druid") > 0));

            if (!hasOtherPreparedCaster)
            {
                // Backward-compatible fallback for single-class wizard initialization only.
                foreach (SpellData spell in SpellDatabase.GetSpellsForClassAtLevel("Wizard", 1)) AddKnownSpellForClass(className, spell);
                foreach (SpellData spell in SpellDatabase.GetSpellsForClassAtLevel("Wizard", 2)) AddKnownSpellForClass(className, spell);
                Debug.Log("[Spellcasting] Wizard: no spell selection found, added fallback level 1-2 spells for single-class initialization.");
            }
            else
            {
                Debug.Log("[Spellcasting] Wizard multiclass init: cantrips granted, higher-level spells must be learned via wizard progression.");
            }
        }
    }

    /// <summary>
    /// Initialize Cleric spellcasting with D&D 3.5e slot-based preparation.
    /// Clerics can prepare ANY cleric spell of appropriate level (no spellbook restriction).
    /// </summary>
    private void InitCleric(int level, string className = "Cleric")
    {
        int safeLevel = Mathf.Max(1, level);
        int[] slots = GetClericSlotsForLevel(safeLevel);
        _slotsMaxByClass[className] = (int[])slots.Clone();
        _slotsRemainingByClass[className] = (int[])slots.Clone();
        _regularSlotsMaxByClass[className] = (int[])_regularSlotsMax.Clone();
        _domainSlotsMaxByClass[className] = (int[])_domainSlotsMax.Clone();
        AppendSpellSlotsForClass(className);

        Debug.Log($"[Spellcasting] {className} created {GetSlotsForClass(className).Count} spell slots at level {safeLevel}: {GetSlotArrayDebugString(slots)}");

        if (SelectedSpellIds != null && SelectedSpellIds.Count > 0)
        {
            int loadedOrisons = 0;
            foreach (string spellId in SelectedSpellIds)
            {
                SpellData spell = SpellDatabase.GetSpell(spellId);
                if (spell != null && spell.SpellLevel == 0 && spell.IsAvailableFor("Cleric", spell.SpellLevel))
                {
                    AddKnownSpellForClass(className, spell);
                    loadedOrisons++;
                }
            }
            Debug.Log($"[Spellcasting] {className} loaded {loadedOrisons} selected orisons.");
        }
        else
        {
            foreach (SpellData spell in SpellDatabase.GetSpellsForClassAtLevel("Cleric", 0)) AddKnownSpellForClass(className, spell);
            Debug.Log("[Spellcasting] Cleric: no orison selection found, added all orisons.");
        }

        foreach (SpellData spell in SpellDatabase.GetSpellsForClassAtLevel("Cleric", 1)) AddKnownSpellForClass(className, spell);
        foreach (SpellData spell in SpellDatabase.GetSpellsForClassAtLevel("Cleric", 2)) AddKnownSpellForClass(className, spell);
    }

    private void InitDruid(int level, string className = "Druid")
    {
        int safeLevel = Mathf.Max(1, level);
        int[] slots = GetDruidSlotsForLevel(safeLevel);
        _slotsMaxByClass[className] = (int[])slots.Clone();
        _slotsRemainingByClass[className] = (int[])slots.Clone();
        _regularSlotsMaxByClass[className] = (int[])_regularSlotsMax.Clone();
        _domainSlotsMaxByClass[className] = (int[])_domainSlotsMax.Clone();
        AppendSpellSlotsForClass(className);

        Debug.Log($"[Spellcasting] {className} created {GetSlotsForClass(className).Count} spell slots at level {safeLevel}: {GetSlotArrayDebugString(slots)}");

        if (SelectedSpellIds != null && SelectedSpellIds.Count > 0)
        {
            foreach (string spellId in SelectedSpellIds)
            {
                SpellData spell = SpellDatabase.GetSpell(spellId);
                if (spell != null)
                    AddKnownSpellForClass(className, spell);
            }
            Debug.Log($"[Spellcasting] {className} loaded {SelectedSpellIds.Count} selected spells.");
        }
        else
        {
            foreach (SpellData spell in SpellDatabase.GetSpellsForClassAtLevel("Druid", 0)) AddKnownSpellForClass(className, spell);
            foreach (SpellData spell in SpellDatabase.GetSpellsForClassAtLevel("Druid", 1)) AddKnownSpellForClass(className, spell);
            foreach (SpellData spell in SpellDatabase.GetSpellsForClassAtLevel("Druid", 2)) AddKnownSpellForClass(className, spell);
            Debug.Log("[Spellcasting] Druid: no spell selection found, added all available spells (Lv0-2).");
        }
    }

    public void RefreshSpellSlots()
    {
        if (Stats == null)
        {
            Debug.LogWarning("[Spellcasting] RefreshSpellSlots called with null Stats.");
            return;
        }

        if (!(Stats.IsWizard || Stats.IsCleric || IsDruidClass(Stats)))
        {
            Debug.Log($"[Spellcasting] RefreshSpellSlots skipped for non-prepared caster {Stats.CharacterClass}.");
            return;
        }

        string characterName = !string.IsNullOrWhiteSpace(Stats.CharacterName) ? Stats.CharacterName : gameObject.name;
        SyncDomainsFromStats();

        var previousSlots = SpellSlots
            .Where(slot => slot != null)
            .Select(slot => new SpellSlotSnapshot
            {
                ClassName = slot.CasterClassName,
                Level = slot.Level,
                PreparedSpell = slot.PreparedSpell,
                IsUsed = slot.IsUsed,
                DisabledByNegativeLevel = slot.DisabledByNegativeLevel,
                IsDomainSlot = slot.IsDomainSlot,
                IsSpecialistSlot = slot.IsSpecialistSlot
            })
            .ToList();

        var previousKnownByClass = new Dictionary<string, List<SpellData>>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _knownSpellsByClass)
            previousKnownByClass[kvp.Key] = kvp.Value != null ? kvp.Value.Where(s => s != null).ToList() : new List<SpellData>();

        KnownSpells.Clear();
        PreparedSpells.Clear();
        SpellSlots.Clear();
        _knownSpellsByClass.Clear();
        _slotsMaxByClass.Clear();
        _slotsRemainingByClass.Clear();
        _regularSlotsMaxByClass.Clear();
        _domainSlotsMaxByClass.Clear();
        _preparedCasterClasses.Clear();

        string[] classOrder = { "Wizard", "Cleric", "Druid" };
        for (int i = 0; i < classOrder.Length; i++)
        {
            string className = classOrder[i];
            int classLevel = GetActivePreparedCasterLevel(Stats, className);
            if (classLevel <= 0 || Stats.GetClassLevel(className) <= 0)
                continue;

            InitializePreparedCasterClass(className, classLevel);
            _preparedCasterClasses.Add(className);

            if (previousKnownByClass.TryGetValue(className, out List<SpellData> prevKnown) && prevKnown != null && prevKnown.Count > 0)
            {
                _knownSpellsByClass[className] = prevKnown
                    .Where(s => s != null)
                    .GroupBy(s => s.SpellId)
                    .Select(g => g.First())
                    .ToList();
            }
        }

        KnownSpells.Clear();
        foreach (var kvp in _knownSpellsByClass)
        {
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                SpellData spell = kvp.Value[i];
                if (spell != null && !KnownSpells.Any(s => s != null && s.SpellId == spell.SpellId))
                    KnownSpells.Add(spell);
            }
        }

        RestoreSlotStateFromSnapshot(previousSlots);
        SyncPreparedSpellsFromSlots();
        ApplyNegativeLevelSlotLoss();
        SyncPrimaryClassLegacyView();

        Debug.Log($"[Spellcasting] Refresh complete for {characterName}: {GetSlotSummary()}");
    }

    private int[] GetWizardSlotsForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        int intMod = Mathf.Max(0, Stats != null ? Stats.INTMod : 0);

        int baseCantrips;
        int baseFirst;
        int baseSecond;

        switch (safeLevel)
        {
            case 1:
                baseCantrips = 3;
                baseFirst = 1;
                baseSecond = 0;
                break;
            case 2:
                baseCantrips = 4;
                baseFirst = 2;
                baseSecond = 0;
                break;
            case 3:
                baseCantrips = 4;
                baseFirst = 2;
                baseSecond = 1;
                break;
            default:
                // Level 4+ currently capped to implemented prototype progression tier.
                baseCantrips = 4;
                baseFirst = 3;
                baseSecond = 2;
                break;
        }

        // D&D 3.5e: bonus slots never grant access to a spell level you cannot already cast.
        int bonusFirst = baseFirst > 0 && intMod >= 1 ? 1 : 0;
        int bonusSecond = baseSecond > 0 && intMod >= 2 ? 1 : 0;

        _regularSlotsMax = new[] { baseCantrips, baseFirst + bonusFirst, baseSecond + bonusSecond };

        // D&D 3.5e PHB: Specialist wizards get +1 bonus slot per spell level they can cast.
        // These bonus slots can ONLY be filled with spells from the specialist school.
        // Works like cleric domain slots but for the specialist school.
        bool isSpecialist = Stats != null && Stats.IsWizardSpecialist;
        _domainSlotsMax = new int[_regularSlotsMax.Length];
        if (isSpecialist)
        {
            for (int spellLevel = 0; spellLevel < _regularSlotsMax.Length; spellLevel++)
            {
                // Grant +1 specialist slot at each level the wizard can cast (has base slots > 0)
                _domainSlotsMax[spellLevel] = _regularSlotsMax[spellLevel] > 0 ? 1 : 0;
            }
        }

        int[] totalSlots = new int[_regularSlotsMax.Length];
        for (int i = 0; i < totalSlots.Length; i++)
            totalSlots[i] = _regularSlotsMax[i] + _domainSlotsMax[i];

        if (isSpecialist)
        {
            Debug.Log($"[Spellcasting] Wizard specialist ({Stats.WizardSpecialization.specializationSchool}) bonus slots: " +
                      string.Join(", ", _domainSlotsMax.Select((s, i) => $"L{i}={s}")));
        }

        return totalSlots;
    }

    private int[] GetClericSlotsForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        int wisMod = Mathf.Max(0, Stats != null ? Stats.WISMod : 0);

        int[] baseRegularSlots = GetClericBaseRegularSlotsForCasterLevel(safeLevel);
        _regularSlotsMax = (int[])baseRegularSlots.Clone();

        // Bonus spells apply to regular spell slots (not to domain slots).
        for (int spellLevel = 1; spellLevel < _regularSlotsMax.Length; spellLevel++)
        {
            if (_regularSlotsMax[spellLevel] <= 0)
                continue;

            if (wisMod >= spellLevel)
                _regularSlotsMax[spellLevel] += 1;
        }

        _domainSlotsMax = new int[_regularSlotsMax.Length];
        bool hasAnyDomains = Domains != null && Domains.Count > 0;
        for (int spellLevel = 1; spellLevel < _regularSlotsMax.Length; spellLevel++)
        {
            // D&D 3.5e: one domain slot at each spell level the cleric can cast (1+).
            _domainSlotsMax[spellLevel] = hasAnyDomains && _regularSlotsMax[spellLevel] > 0 ? 1 : 0;
        }

        int[] totalSlots = new int[_regularSlotsMax.Length];
        for (int i = 0; i < totalSlots.Length; i++)
            totalSlots[i] = _regularSlotsMax[i] + _domainSlotsMax[i];

        Debug.Log($"[Spellcasting] Calculating Cleric spell slots for level {safeLevel}");
        for (int spellLevel = 0; spellLevel < totalSlots.Length; spellLevel++)
        {
            if (totalSlots[spellLevel] <= 0)
                continue;

            int regular = GetMaxSpellSlotsAtLevel(spellLevel);
            int domain = GetMaxDomainSlotsAtLevel(spellLevel);
            Debug.Log($"[Spellcasting] Level {spellLevel}: {regular} regular slots");
            if (domain > 0)
                Debug.Log($"[Spellcasting] Level {spellLevel}: {domain} domain slot");
        }

        return totalSlots;
    }

    private int[] GetClericBaseRegularSlotsForCasterLevel(int level)
    {
        // D&D 3.5e PHB cleric spells/day progression (base, no bonus spells, no domain slots).
        // Prototype currently supports prepared spells up to level 2, but keep progression data up to level 9 for correctness.
        switch (Mathf.Clamp(level, 1, 9))
        {
            case 1: return new[] { 3, 1, 0, 0, 0, 0 };
            case 2: return new[] { 4, 2, 0, 0, 0, 0 };
            case 3: return new[] { 4, 2, 1, 0, 0, 0 };
            case 4: return new[] { 5, 3, 2, 0, 0, 0 };
            case 5: return new[] { 5, 3, 2, 1, 0, 0 };
            case 6: return new[] { 5, 3, 3, 2, 0, 0 };
            case 7: return new[] { 6, 4, 3, 2, 1, 0 };
            case 8: return new[] { 6, 4, 3, 3, 2, 0 };
            default: return new[] { 6, 4, 4, 3, 2, 1 }; // level 9
        }
    }

    private int[] GetDruidSlotsForLevel(int level)
    {
        int safeLevel = Mathf.Max(1, level);
        int wisMod = Mathf.Max(0, Stats != null ? Stats.WISMod : 0);

        int baseCantrips;
        int baseFirst;
        int baseSecond;

        switch (safeLevel)
        {
            case 1:
                baseCantrips = 3;
                baseFirst = 1;
                baseSecond = 0;
                break;
            case 2:
                baseCantrips = 4;
                baseFirst = 2;
                baseSecond = 0;
                break;
            default:
                // Preserve existing prototype behavior at level 3+.
                baseCantrips = 4;
                baseFirst = 2;
                baseSecond = 1;
                break;
        }

        // D&D 3.5e: bonus slots never grant access to a spell level you cannot already cast.
        int bonusFirst = baseFirst > 0 && wisMod >= 1 ? 1 : 0;
        int bonusSecond = baseSecond > 0 && wisMod >= 2 ? 1 : 0;

        _regularSlotsMax = new[] { baseCantrips, baseFirst + bonusFirst, baseSecond + bonusSecond };
        _domainSlotsMax = new int[_regularSlotsMax.Length];
        return (int[])_regularSlotsMax.Clone();
    }

    private void InitializeSpellSlotCollection()
    {
        SpellSlots.Clear();
        if (SlotsMax == null)
            return;

        for (int spellLevel = 0; spellLevel < SlotsMax.Length; spellLevel++)
        {
            int regularSlots = GetMaxSpellSlotsAtLevel(spellLevel);
            int domainSlots = GetMaxDomainSlotsAtLevel(spellLevel);

            for (int i = 0; i < regularSlots; i++)
                SpellSlots.Add(new SpellSlot(spellLevel, isDomainSlot: false));

            // Keep domain slots last within each level for stable indexing and clear UI labeling.
            for (int i = 0; i < domainSlots; i++)
                SpellSlots.Add(new SpellSlot(spellLevel, isDomainSlot: true));
        }
    }

    private void RestoreSlotStateFromSnapshot(List<SpellSlotSnapshot> previousSlots)
    {
        if (previousSlots == null || previousSlots.Count == 0)
            return;

        var previousByClassLevel = previousSlots
            .GroupBy(s => $"{s.ClassName}|{s.Level}|{(s.IsDomainSlot ? "D" : s.IsSpecialistSlot ? "S" : "R")}")
            .ToDictionary(g => g.Key, g => g.ToList());

        var currentByClassLevel = SpellSlots
            .Where(s => s != null)
            .GroupBy(s => $"{s.CasterClassName}|{s.Level}|{(s.IsDomainSlot ? "D" : s.IsSpecialistSlot ? "S" : "R")}")
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var kvp in currentByClassLevel)
        {
            if (!previousByClassLevel.TryGetValue(kvp.Key, out List<SpellSlotSnapshot> previousGroup))
                continue;

            List<SpellSlot> currentGroup = kvp.Value;
            int sharedCount = Mathf.Min(currentGroup.Count, previousGroup.Count);
            for (int i = 0; i < sharedCount; i++)
            {
                SpellSlot current = currentGroup[i];
                SpellSlotSnapshot previous = previousGroup[i];

                current.PreparedSpell = previous.PreparedSpell;
                current.IsUsed = previous.IsUsed;
                current.DisabledByNegativeLevel = previous.DisabledByNegativeLevel;
            }
        }
    }

    private void PrepareAnyEmptySlotsAfterRefresh()
    {
        if (KnownSpells == null || KnownSpells.Count == 0)
            return;

        for (int level = 0; level < SlotsMax.Length; level++)
        {
            List<SpellData> spellsAtLevel = KnownSpells
                .Where(s => s != null && s.SpellLevel == level)
                .ToList();

            List<SpellData> domainSpellsAtLevel = GetAvailableDomainSpells(level);

            int nextRegularSpellIndex = 0;
            int nextDomainSpellIndex = 0;

            foreach (SpellSlot slot in SpellSlots.Where(s => s != null && s.Level == level && !s.HasSpell))
            {
                if (slot.IsDomainSlot)
                {
                    if (domainSpellsAtLevel.Count == 0)
                        continue;

                    slot.Prepare(domainSpellsAtLevel[nextDomainSpellIndex % domainSpellsAtLevel.Count]);
                    nextDomainSpellIndex++;
                    continue;
                }

                if (spellsAtLevel.Count == 0)
                    continue;

                slot.Prepare(spellsAtLevel[nextRegularSpellIndex % spellsAtLevel.Count]);
                nextRegularSpellIndex++;
            }
        }
    }

    private string GetSlotArrayDebugString(int[] slots)
    {
        if (slots == null || slots.Length == 0)
            return "none";

        var parts = new List<string>();
        for (int i = 0; i < slots.Length; i++)
            parts.Add($"L{i}={slots[i]}");

        return string.Join(", ", parts);
    }

    private sealed class SpellSlotSnapshot
    {
        public string ClassName;
        public int Level;
        public SpellData PreparedSpell;
        public bool IsUsed;
        public bool DisabledByNegativeLevel;
        public bool IsDomainSlot;
        public bool IsSpecialistSlot;
    }

    // ========== SPELL SLOT PREPARATION ==========

    /// <summary>
    /// Auto-prepare wizard spells into slots. Distributes known spells across available slots.
    /// Fills all cantrip slots first, then level 1, then level 2.
    /// If there are more slots than unique spells at a level, the same spell fills multiple slots.
    /// This is the default auto-preparation used at initialization.
    /// </summary>

    /// <summary>
    /// Apply prepared spell slot IDs from character creation data.
    /// Maps spell IDs from PreparedSpellSlotIds to SpellSlots in order.
    /// </summary>
    private void ApplyPreparedSpellSlotIds()
    {
        if (PreparedSpellSlotIds == null) return;

        for (int i = 0; i < SpellSlots.Count && i < PreparedSpellSlotIds.Count; i++)
        {
            string spellId = PreparedSpellSlotIds[i];
            if (string.IsNullOrEmpty(spellId))
                continue;

            SpellData spell = SpellDatabase.GetSpell(spellId);
            if (spell == null)
                continue;

            if (!PrepareSpellInSlot(i, spell))
            {
                Debug.LogWarning($"[Spellcasting] Ignored invalid prepared spell '{spellId}' for slot index {i}.");
            }
        }
    }

    public void AutoPrepareWizardSlots()
    {
        AutoPrepareWizardSlots("Wizard");
    }

    public void AutoPrepareWizardSlots(string className)
    {
        if (Stats == null || Stats.GetClassLevel(className) <= 0) return;

        int[] classSlots = GetSlotsMaxForClass(className);
        List<SpellData> classKnown = GetKnownSpellsForClass(className);
        bool isSpecialist = Stats.IsWizardSpecialist;

        for (int level = 0; level < classSlots.Length; level++)
        {
            var slotsAtLevel = GetSlotsForClassAtLevel(className, level);
            var regularSlotsAtLevel = slotsAtLevel.Where(s => s != null && !s.IsSpecialistSlot).ToList();
            var specialistSlotsAtLevel = slotsAtLevel.Where(s => s != null && s.IsSpecialistSlot).ToList();

            var spellsAtLevel = classKnown.Where(s => s.SpellLevel == level).ToList();

            // Auto-prepare regular slots
            if (spellsAtLevel.Count == 0)
            {
                foreach (var slot in regularSlotsAtLevel)
                    slot.Clear();
            }
            else
            {
                for (int i = 0; i < regularSlotsAtLevel.Count; i++)
                    regularSlotsAtLevel[i].Prepare(spellsAtLevel[i % spellsAtLevel.Count]);
            }

            // Auto-prepare specialist slots with specialist school spells only
            if (isSpecialist && specialistSlotsAtLevel.Count > 0)
            {
                List<SpellData> specialistSpells = GetAvailableSpecialistSpells(level);
                if (specialistSpells.Count == 0)
                {
                    foreach (var slot in specialistSlotsAtLevel)
                        slot.Clear();
                }
                else
                {
                    for (int i = 0; i < specialistSlotsAtLevel.Count; i++)
                        specialistSlotsAtLevel[i].Prepare(specialistSpells[i % specialistSpells.Count]);
                }
            }

            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Auto-prepared {className} level {level} slots " +
                      $"(regular={regularSlotsAtLevel.Count}, specialist={specialistSlotsAtLevel.Count})");
        }

        SyncSlotsRemainingFromSpellSlots();
        Debug.Log($"[Spellcasting] {Stats.CharacterName}: Auto-prepared {className} spell slots");
    }

    /// <summary>
    /// Auto-prepare cleric spells into slots. Fills only as many slots as available,
    /// selecting from known spells at each level. Prioritizes functional (non-placeholder) spells.
    /// Clerics can prepare ANY cleric spell of appropriate level from the full spell list.
    /// If there are more slots than unique functional spells at a level, fills remaining
    /// slots by cycling through available spells.
    /// </summary>
    public void AutoPrepareClericSlots()
    {
        AutoPrepareClericSlots("Cleric");
    }

    public void AutoPrepareClericSlots(string className)
    {
        if (Stats == null || Stats.GetClassLevel(className) <= 0) return;

        int[] classSlots = GetSlotsMaxForClass(className);
        List<SpellData> classKnown = GetKnownSpellsForClass(className);
        for (int level = 0; level < classSlots.Length; level++)
        {
            var slotsAtLevel = GetSlotsForClassAtLevel(className, level);
            var regularSlotsAtLevel = slotsAtLevel.Where(s => s != null && !s.IsDomainSlot).ToList();
            var domainSlotsAtLevel = slotsAtLevel.Where(s => s != null && s.IsDomainSlot).ToList();

            var spellsAtLevel = classKnown.Where(s => s != null && s.SpellLevel == level).ToList();
            var functional = spellsAtLevel.Where(s => !s.IsPlaceholder).ToList();
            var regularCandidates = functional.Count > 0 ? functional : spellsAtLevel;

            if (regularCandidates.Count == 0)
            {
                foreach (var slot in regularSlotsAtLevel)
                    slot.Clear();
            }
            else
            {
                for (int i = 0; i < regularSlotsAtLevel.Count; i++)
                    regularSlotsAtLevel[i].Prepare(regularCandidates[i % regularCandidates.Count]);
            }

            List<SpellData> domainCandidates = GetAvailableDomainSpells(level);
            if (domainCandidates.Count == 0)
            {
                foreach (var slot in domainSlotsAtLevel)
                    slot.Clear();
            }
            else
            {
                for (int i = 0; i < domainSlotsAtLevel.Count; i++)
                    domainSlotsAtLevel[i].Prepare(domainCandidates[i % domainCandidates.Count]);
            }

            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Auto-prepared {className} level {level} slots " +
                      $"(regular={regularSlotsAtLevel.Count}, domain={domainSlotsAtLevel.Count}, regularCandidates={regularCandidates.Count}, domainCandidates={domainCandidates.Count})");
        }

        SyncSlotsRemainingFromSpellSlots();
        Debug.Log($"[Spellcasting] {Stats.CharacterName}: Auto-prepared {className} spell slots");
    }

    public void AutoPrepareDruidSlots()
    {
        AutoPrepareDruidSlots("Druid");
    }

    public void AutoPrepareDruidSlots(string className)
    {
        if (Stats == null || Stats.GetClassLevel(className) <= 0) return;

        int[] classSlots = GetSlotsMaxForClass(className);
        List<SpellData> classKnown = GetKnownSpellsForClass(className);
        for (int level = 0; level < classSlots.Length; level++)
        {
            var slotsAtLevel = GetSlotsForClassAtLevel(className, level);
            var spellsAtLevel = classKnown.Where(s => s.SpellLevel == level).ToList();

            if (spellsAtLevel.Count == 0)
            {
                foreach (var slot in slotsAtLevel)
                    slot.Clear();
                continue;
            }

            var functional = spellsAtLevel.Where(s => !s.IsPlaceholder).ToList();
            var candidates = functional.Count > 0 ? functional : spellsAtLevel;

            for (int i = 0; i < slotsAtLevel.Count; i++)
                slotsAtLevel[i].Prepare(candidates[i % candidates.Count]);

            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Auto-prepared {slotsAtLevel.Count} level-{level} {className} slots " +
                      $"(from {spellsAtLevel.Count} known, {functional.Count} functional)");
        }

        SyncSlotsRemainingFromSpellSlots();
        Debug.Log($"[Spellcasting] {Stats.CharacterName}: Auto-prepared {className} spell slots");
    }

    private bool IsDomainSpellForCharacter(SpellData spell)
    {
        if (spell == null || Domains == null || Domains.Count == 0)
            return false;

        for (int i = 0; i < Domains.Count; i++)
        {
            if (SpellDatabase.IsSpellInDomain(spell.SpellId, Domains[i]))
                return true;
        }

        return false;
    }

    private bool IsValidSpellForSlot(SpellData spell, SpellSlot slot, bool logOnFailure)
    {
        if (slot == null || spell == null)
            return true;

        if (spell.SpellLevel != slot.Level)
        {
            if (logOnFailure)
                Debug.LogWarning($"[Spellcasting] Cannot prepare level {spell.SpellLevel} spell in level {slot.Level} slot!");
            return false;
        }

        if (slot.IsDomainSlot)
        {
            bool validDomainSpell = IsDomainSpellForCharacter(spell);
            if (!validDomainSpell)
            {
                if (logOnFailure)
                    Debug.LogWarning($"[Spellcasting] {spell.Name} is not in any chosen domain for domain slot Lv{slot.Level}.");
                return false;
            }
        }

        // Wizard specialist slots: can only prepare spells from the specialist school.
        if (slot.IsSpecialistSlot)
        {
            bool validSpecialistSpell = IsSpecialistSchoolSpell(spell);
            if (!validSpecialistSpell)
            {
                string school = Stats != null && Stats.IsWizardSpecialist ? Stats.WizardSpecialization.specializationSchool : "?";
                if (logOnFailure)
                    Debug.LogWarning($"[Spellcasting] {spell.Name} ({spell.School}) is not from specialist school ({school}) for specialist slot Lv{slot.Level}.");
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(slot.CasterClassName) && !IsSpellKnownByClass(slot.CasterClassName, spell))
        {
            if (logOnFailure)
                Debug.LogWarning($"[Spellcasting] {spell.Name} is not available for {slot.CasterClassName} slots.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true if the spell belongs to the wizard's specialist school.
    /// </summary>
    private bool IsSpecialistSchoolSpell(SpellData spell)
    {
        if (spell == null || Stats == null || !Stats.IsWizardSpecialist)
            return false;

        string specSchool = Stats.WizardSpecialization.specializationSchool;
        if (string.IsNullOrWhiteSpace(specSchool))
            return false;

        return string.Equals(spell.School, specSchool, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get available spells for specialist slots at a given level.
    /// Returns only spells from the wizard's specialization school that are in the spellbook.
    /// </summary>
    public List<SpellData> GetAvailableSpecialistSpells(int spellLevel)
    {
        if (Stats == null || !Stats.IsWizardSpecialist)
            return new List<SpellData>();

        string specSchool = Stats.WizardSpecialization.specializationSchool;
        List<SpellData> known = GetKnownSpellsForClass("Wizard");

        return known
            .Where(s => s != null && s.SpellLevel == spellLevel &&
                        string.Equals(s.School, specSchool, System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Name)
            .ToList();
    }

    /// <summary>
    /// Prepare a specific spell into a specific slot.
    /// For Wizards: spell must be in the spellbook. For Clerics: any cleric spell of matching level.
    /// Domain slots can only contain domain spells.
    /// </summary>
    public bool PrepareSpellInSlot(int slotIndex, SpellData spell)
    {
        if (slotIndex < 0 || slotIndex >= SpellSlots.Count) return false;
        var slot = SpellSlots[slotIndex];

        if (!IsValidSpellForSlot(spell, slot, logOnFailure: true))
            return false;

        // Enforce class-specific availability (wizard spellbook vs class full-list casters).
        if (spell != null && !string.IsNullOrWhiteSpace(slot.CasterClassName) && !IsSpellKnownByClass(slot.CasterClassName, spell))
        {
            Debug.LogWarning($"[Spellcasting] {spell.Name} is not in {slot.CasterClassName}'s available spell list!");
            return false;
        }

        slot.Prepare(spell);
        SyncSlotsRemainingFromSpellSlots();
        SyncPreparedSpellsFromSlots();
        return true;
    }

    public List<SpellData> GetAllAvailableSpells()
    {
        SpellDatabase.Init();

        return KnownSpells
            .Where(s => s != null)
            .GroupBy(s => s.SpellId)
            .Select(g => g.First())
            .OrderBy(s => s.SpellLevel)
            .ThenBy(s => s.Name)
            .ToList();
    }

    /// <summary>
    /// Returns all known spell IDs in the spellbook grouped across spell levels.
    /// </summary>
    public List<string> GetAllKnownSpells()
    {
        Debug.Log("[Spellcasting] Getting all known spells");

        var allSpells = new List<string>();
        for (int level = 0; level <= 9; level++)
        {
            List<string> spellsAtLevel = GetKnownSpellsAtLevel(level);
            if (spellsAtLevel.Count > 0)
            {
                allSpells.AddRange(spellsAtLevel);
                Debug.Log($"[Spellcasting] Level {level}: {spellsAtLevel.Count} spells");
            }
        }

        Debug.Log($"[Spellcasting] Total: {allSpells.Count} spells known");
        return allSpells;
    }

    /// <summary>
    /// Returns known spell IDs at the requested spell level.
    /// </summary>
    public List<string> GetKnownSpellsAtLevel(int spellLevel)
    {
        return KnownSpells
            .Where(s => s != null && s.SpellLevel == spellLevel && !string.IsNullOrWhiteSpace(s.SpellId))
            .GroupBy(s => s.SpellId)
            .Select(g => g.First().SpellId)
            .ToList();
    }

    /// <summary>
    /// Learns a new spell for the currently active primary class.
    /// Accepts spell ID (preferred) or spell name.
    /// </summary>
    public void LearnSpell(string spellIdOrName)
    {
        LearnSpellForClass(_activePreparationClassName, spellIdOrName);
    }

    /// <summary>
    /// Learns a new spell for a specific caster class (e.g. Wizard during wizard level-up).
    /// </summary>
    public void LearnSpellForClass(string className, string spellIdOrName)
    {
        if (string.IsNullOrWhiteSpace(spellIdOrName))
            return;

        SpellDatabase.Init();

        SpellData spell = SpellDatabase.GetSpell(spellIdOrName);
        if (spell == null)
        {
            spell = SpellDatabase.GetAllSpells()
                .FirstOrDefault(s => s != null &&
                                     string.Equals(s.Name, spellIdOrName, System.StringComparison.OrdinalIgnoreCase));
        }

        if (spell == null)
        {
            Debug.LogError($"[Spellcasting] Spell not found: {spellIdOrName}");
            return;
        }

        string targetClass = className;
        if (string.IsNullOrWhiteSpace(targetClass))
        {
            targetClass = GetPreparedCasterClassNames().FirstOrDefault(c => spell.IsAvailableFor(c, spell.SpellLevel));
        }

        if (string.IsNullOrWhiteSpace(targetClass))
        {
            Debug.LogWarning($"[Spellcasting] No eligible caster class found to learn {spell.Name}.");
            return;
        }

        if (!spell.IsAvailableFor(targetClass, spell.SpellLevel))
        {
            Debug.LogWarning($"[Spellcasting] {spell.Name} is not on the {targetClass} spell list.");
            return;
        }

        bool alreadyKnownForClass = IsSpellKnownByClass(targetClass, spell);
        if (!alreadyKnownForClass)
            AddKnownSpellForClass(targetClass, spell);

        if (SelectedSpellIds == null)
            SelectedSpellIds = new List<string>();
        if (!SelectedSpellIds.Contains(spell.SpellId))
            SelectedSpellIds.Add(spell.SpellId);

        Debug.Log($"[Spellcasting] ✓ Added {spell.Name} (level {spell.SpellLevel}) to {targetClass} spell list");

        if (string.Equals(targetClass, "Wizard", System.StringComparison.OrdinalIgnoreCase))
            AutoPrepareNewWizardSpell(spell);

        SyncPreparedSpellsFromSlots();
    }

    private void AutoPrepareNewWizardSpell(SpellData spell)
    {
        if (spell == null || SpellSlots == null || SpellSlots.Count == 0)
            return;

        SpellSlot emptySlot = SpellSlots.FirstOrDefault(s =>
            s != null &&
            string.Equals(s.CasterClassName, "Wizard", System.StringComparison.OrdinalIgnoreCase) &&
            s.Level == spell.SpellLevel && !s.HasSpell && !s.DisabledByNegativeLevel);

        if (emptySlot == null)
            return;

        emptySlot.Prepare(spell);
        Debug.Log($"[Spellcasting] Auto-prepared {spell.Name}");
    }

    public List<SpellData> GetPreparedSpellsAtLevel(int level)
    {
        if (SpellSlots == null || SpellSlots.Count == 0)
            return new List<SpellData>();

        return SpellSlots
            .Where(s => s != null && s.Level == level && s.HasSpell)
            .Select(s => s.PreparedSpell)
            .ToList();
    }

    public int GetSpellSlotsPerDay(int level)
    {
        if (level < 0)
            return 0;

        return GetSlotsForLevel(level).Count;
    }

    public void PrepareSpell(SpellData spell, int level, int slotIndex)
    {
        if (spell == null)
        {
            Debug.LogWarning("[SpellPrep] Cannot prepare null spell.");
            return;
        }

        var slotsAtLevel = GetSlotsForLevel(level);
        if (slotIndex < 0 || slotIndex >= slotsAtLevel.Count)
        {
            Debug.LogWarning($"[SpellPrep] Invalid slot index {slotIndex} for level {level} (slots={slotsAtLevel.Count}).");
            return;
        }

        if (spell.SpellLevel != level)
        {
            Debug.LogWarning($"[SpellPrep] Spell level mismatch for {spell.Name}: spell is Lv{spell.SpellLevel}, slot is Lv{level}.");
            return;
        }

        if (!GetAllAvailableSpells().Any(s => s.SpellId == spell.SpellId))
        {
            Debug.LogWarning($"[SpellPrep] Spell {spell.Name} is not available for {Stats?.CharacterName}.");
            return;
        }

        SpellSlot slot = slotsAtLevel[slotIndex];
        if (!IsValidSpellForSlot(spell, slot, logOnFailure: true))
            return;

        Debug.Log($"[SpellPrep] Preparing {spell.Name} in level {level} slot {slotIndex} (domain={slot.IsDomainSlot})");

        slot.Prepare(spell);
        SyncSlotsRemainingFromSpellSlots();
        SyncPreparedSpellsFromSlots();
    }

    public void ClearPreparedSpells()
    {
        if (SpellSlots == null)
            return;

        for (int i = 0; i < SpellSlots.Count; i++)
            SpellSlots[i]?.Clear();

        PreparedSpells.Clear();
        SyncSlotsRemainingFromSpellSlots();
        SyncPreparedSpellsFromSlots();
    }

    /// <summary>
    /// Clear all prepared spells from wizard slots (but keep the slots).
    /// </summary>
    public void ClearAllWizardSlots()
    {
        foreach (var slot in SpellSlots.Where(s => s != null && string.Equals(s.CasterClassName, "Wizard", System.StringComparison.OrdinalIgnoreCase)))
            slot.Clear();
        SyncSlotsRemainingFromSpellSlots();
        SyncPreparedSpellsFromSlots();
    }

    /// <summary>
    /// Get all spell slots for a specific spell level.
    /// Domain slots are always placed last in the returned list for stable indexing.
    /// </summary>
    public List<SpellSlot> GetSlotsForLevel(int level)
    {
        return SpellSlots
            .Where(s => s != null && s.Level == level)
            .OrderBy(s => s.IsDomainSlot ? 2 : s.IsSpecialistSlot ? 1 : 0)
            .ToList();
    }

    /// <summary>Get max regular (non-domain) slots for a spell level.</summary>
    public int GetMaxSpellSlotsAtLevel(int spellLevel)
    {
        if (_regularSlotsMax == null || spellLevel < 0 || spellLevel >= _regularSlotsMax.Length)
            return 0;
        return _regularSlotsMax[spellLevel];
    }

    /// <summary>Get max domain slots for a spell level.</summary>
    public int GetMaxDomainSlotsAtLevel(int spellLevel)
    {
        if (_domainSlotsMax == null || spellLevel < 0 || spellLevel >= _domainSlotsMax.Length)
            return 0;
        return _domainSlotsMax[spellLevel];
    }

    /// <summary>
    /// Returns true if a level-local slot index points to a domain slot.
    /// Domain slots are the last slot(s) at each spell level.
    /// </summary>
    public bool IsDomainSlot(int spellLevel, int slotIndex)
    {
        List<SpellSlot> slotsAtLevel = GetSlotsForLevel(spellLevel);
        if (slotIndex < 0 || slotIndex >= slotsAtLevel.Count)
            return false;

        SpellSlot slot = slotsAtLevel[slotIndex];
        return slot != null && slot.IsDomainSlot;
    }

    /// <summary>
    /// Returns true if a level-local slot index points to a specialist slot.
    /// Specialist slots are bonus slots for wizard specialists, restricted to specialist school spells.
    /// </summary>
    public bool IsSpecialistSlot(int spellLevel, int slotIndex)
    {
        List<SpellSlot> slotsAtLevel = GetSlotsForLevel(spellLevel);
        if (slotIndex < 0 || slotIndex >= slotsAtLevel.Count)
            return false;

        SpellSlot slot = slotsAtLevel[slotIndex];
        return slot != null && slot.IsSpecialistSlot;
    }

    /// <summary>Get the prepared spell name at the provided level-local slot index.</summary>
    public string GetPreparedSpellAtSlot(int spellLevel, int slotIndex)
    {
        List<SpellSlot> slotsAtLevel = GetSlotsForLevel(spellLevel);
        if (slotIndex < 0 || slotIndex >= slotsAtLevel.Count)
            return string.Empty;

        SpellData prepared = slotsAtLevel[slotIndex]?.PreparedSpell;
        return prepared != null ? prepared.Name : string.Empty;
    }

    /// <summary>Prepare spell by display name at the provided level-local slot index.</summary>
    public bool PrepareSpellAtSlot(int spellLevel, int slotIndex, string spellName)
    {
        if (string.IsNullOrWhiteSpace(spellName))
            return false;

        SpellData spell = SpellDatabase.GetSpellByName(spellName);
        if (spell == null)
            return false;

        List<SpellSlot> slotsAtLevel = GetSlotsForLevel(spellLevel);
        if (slotIndex < 0 || slotIndex >= slotsAtLevel.Count)
            return false;

        int globalSlotIndex = SpellSlots.IndexOf(slotsAtLevel[slotIndex]);
        if (globalSlotIndex < 0)
            return false;

        bool success = PrepareSpellInSlot(globalSlotIndex, spell);
        if (success)
            Debug.Log($"[Spellcasting] Prepared {spell.Name} in level {spellLevel} slot {slotIndex}");
        return success;
    }

    /// <summary>
    /// Returns all domain spells available for this cleric at a spell level.
    /// </summary>
    public List<SpellData> GetAvailableDomainSpells(int spellLevel)
    {
        var result = new List<SpellData>();

        if (Stats == null || !Stats.IsCleric)
            return result;

        Debug.Log($"[Spellcasting] Getting available domain spells for level {spellLevel}");

        for (int i = 0; i < Domains.Count; i++)
        {
            string domain = Domains[i];
            List<SpellData> domainSpells = SpellDatabase.GetDomainSpells(domain, spellLevel);
            Debug.Log($"[Spellcasting] Domain '{domain}' level {spellLevel}: {domainSpells.Count} spells");
            result.AddRange(domainSpells);
        }

        List<SpellData> deduped = result
            .Where(s => s != null)
            .GroupBy(s => s.SpellId)
            .Select(g => g.First())
            .OrderBy(s => s.Name)
            .ToList();

        Debug.Log($"[Spellcasting] Total domain spells available: {deduped.Count}");
        return deduped;
    }

    /// <summary>
    /// Get available (not used, has spell) slots for a specific level.
    /// </summary>
    public List<SpellSlot> GetAvailableSlotsForLevel(int level)
    {
        return SpellSlots.Where(s => s.Level == level && s.CanCast).ToList();
    }

    /// <summary>
    /// Count how many times a specific spell is prepared and available (not used) in spell slots.
    /// For cantrips (level 0): returns a special value (999) to indicate unlimited use.
    /// </summary>
    public int CountAvailablePreparedSpell(SpellData spell)
    {
        if (spell == null) return 0;

        // Cantrips are unlimited — if prepared, always available
        if (spell.SpellLevel == 0)
        {
            bool isPrepared = SpellSlots.Any(s => s.PreparedSpell != null &&
                                                   s.PreparedSpell.SpellId == spell.SpellId &&
                                                   !s.DisabledByNegativeLevel);
            return isPrepared ? 999 : 0; // 999 = unlimited
        }

        return SpellSlots.Count(s => s.PreparedSpell != null &&
                                     s.PreparedSpell.SpellId == spell.SpellId && !s.IsUsed && !s.DisabledByNegativeLevel);
    }

    /// <summary>
    /// Count how many times a specific spell is prepared (total, regardless of used status).
    /// </summary>
    public int CountTotalPreparedSpell(SpellData spell)
    {
        if (spell == null) return 0;
        return SpellSlots.Count(s => s.PreparedSpell != null &&
                                     s.PreparedSpell.SpellId == spell.SpellId);
    }

    /// <summary>
    /// Get unique spells that are prepared and available for casting (slot-based system).
    /// Each spell appears once with its available count tracked separately.
    /// For cantrips: includes all prepared cantrips regardless of IsUsed (they're unlimited).
    /// Works for both Wizards and Clerics.
    /// </summary>
    public List<SpellData> GetUniqueAvailableSpells(int level)
    {
        var seen = new HashSet<string>();
        var result = new List<SpellData>();

        foreach (var slot in SpellSlots)
        {
            if (slot.Level != level || !slot.HasSpell) continue;

            // Cantrips are available if prepared and not disabled by negative levels.
            bool isAvailable = (level == 0) ? !slot.DisabledByNegativeLevel : slot.CanCast;

            if (isAvailable && !seen.Contains(slot.PreparedSpell.SpellId))
            {
                seen.Add(slot.PreparedSpell.SpellId);
                result.Add(slot.PreparedSpell);
            }
        }

        return result;
    }

    /// <summary>Backward compat alias — calls GetUniqueAvailableSpells.</summary>
    public List<SpellData> GetUniqueAvailableWizardSpells(int level)
    {
        return GetUniqueAvailableSpells(level);
    }

    private string GetPreferredCastingClassForSpell(SpellData spell)
    {
        if (spell == null)
            return string.Empty;

        List<string> candidates = new List<string>();
        foreach (string className in GetPreparedCasterClassNames())
        {
            bool classHasPrepared = SpellSlots.Any(s =>
                s != null &&
                string.Equals(s.CasterClassName, className, System.StringComparison.OrdinalIgnoreCase) &&
                s.PreparedSpell != null &&
                s.PreparedSpell.SpellId == spell.SpellId &&
                ((spell.SpellLevel == 0 && !s.DisabledByNegativeLevel) || (spell.SpellLevel > 0 && !s.IsUsed && !s.DisabledByNegativeLevel)));

            if (classHasPrepared)
                candidates.Add(className);
        }

        if (candidates.Count == 0)
            return string.Empty;

        if (candidates.Count == 1)
            return candidates[0];

        foreach (string className in candidates)
        {
            if (spell.IsAvailableFor(className, spell.SpellLevel))
                return className;
        }

        return candidates[0];
    }

    /// <summary>
    /// Cast a spell by consuming the first available slot with that spell.
    /// Cantrips (level 0) are unlimited — the slot is NOT consumed.
    /// Works for both Wizards and Clerics.
    /// Returns true if successful.
    /// </summary>
    public bool CastSpellFromSlot(SpellData spell)
    {
        if (spell == null) return false;

        string preferredClass = GetPreferredCastingClassForSpell(spell);

        // Cantrips are unlimited — just check if prepared, don't consume
        if (spell.SpellLevel == 0)
        {
            bool isPrepared = SpellSlots.Any(s =>
                s.PreparedSpell != null &&
                s.PreparedSpell.SpellId == spell.SpellId &&
                !s.DisabledByNegativeLevel &&
                (string.IsNullOrWhiteSpace(preferredClass) || string.Equals(s.CasterClassName, preferredClass, System.StringComparison.OrdinalIgnoreCase)));

            if (!isPrepared)
            {
                Debug.LogWarning($"[Spellcasting] {spell.Name} is not prepared!");
                return false;
            }

            Debug.Log($"[Spellcasting] {Stats.CharacterName} cast cantrip {spell.Name} ({preferredClass}) (unlimited, no slot consumed)");
            return true;
        }

        // Level 1+ spells consume a slot
        var slot = SpellSlots.FirstOrDefault(s =>
            s.PreparedSpell != null &&
            s.PreparedSpell.SpellId == spell.SpellId && !s.IsUsed &&
            (string.IsNullOrWhiteSpace(preferredClass) || string.Equals(s.CasterClassName, preferredClass, System.StringComparison.OrdinalIgnoreCase)));

        if (slot == null)
        {
            Debug.LogWarning($"[Spellcasting] No available slot for {spell.Name}!");
            return false;
        }

        slot.Cast();
        SyncSlotsRemainingFromSpellSlots();
        SyncPreparedSpellsFromSlots();

        Debug.Log($"[Spellcasting] {Stats.CharacterName} cast {spell.Name} from slot " +
                  $"(Lv{slot.Level}). Remaining at Lv{slot.Level}: " +
                  $"{GetAvailableSlotsForLevel(slot.Level).Count}/{GetSlotsForLevel(slot.Level).Count}");

        return true;
    }

    // ========== SPONTANEOUS CASTING (Cleric D&D 3.5e PHB p.32) ==========

    /// <summary>
    /// Whether this cleric can spontaneously cast at the given spell level.
    /// Requires: is a cleric, has spontaneous casting type set, has an unused slot at that level,
    /// and a valid spontaneous spell exists at that level.
    /// Cantrips (level 0) can always be spontaneously cast if prepared (unlimited use).
    /// </summary>
    public bool CanSpontaneousCast(int spellLevel)
    {
        if (Stats == null || !Stats.IsCleric) return false;
        if (Stats.SpontaneousCasting == SpontaneousCastingType.None) return false;

        string spontSpellId = SpontaneousCastingHelper.GetSpontaneousSpellId(Stats.SpontaneousCasting, spellLevel);
        if (string.IsNullOrEmpty(spontSpellId)) return false;

        SpellData spontSpell = SpellDatabase.GetSpell(spontSpellId);
        if (spontSpell == null) return false;

        // For cantrips: always available if there are any cantrip slots (cantrips are unlimited)
        if (spellLevel == 0)
        {
            return SpellSlots.Any(s => string.Equals(s.CasterClassName, "Cleric", System.StringComparison.OrdinalIgnoreCase) && s.Level == 0 && s.HasSpell && !s.DisabledByNegativeLevel);
        }

        // For level 1+: need at least one unused non-domain slot at this level.
        return SpellSlots.Any(s => string.Equals(s.CasterClassName, "Cleric", System.StringComparison.OrdinalIgnoreCase) && s.Level == spellLevel && !s.IsUsed && s.HasSpell && !s.IsDomainSlot);
    }

    /// <summary>
    /// Get the SpellData for the spontaneous spell at a given level.
    /// Returns null if spontaneous casting is not available.
    /// </summary>
    public SpellData GetSpontaneousSpell(int spellLevel)
    {
        if (Stats == null || !Stats.IsCleric) return null;
        if (Stats.SpontaneousCasting == SpontaneousCastingType.None) return null;

        string spontSpellId = SpontaneousCastingHelper.GetSpontaneousSpellId(Stats.SpontaneousCasting, spellLevel);
        if (string.IsNullOrEmpty(spontSpellId)) return null;

        return SpellDatabase.GetSpell(spontSpellId);
    }

    /// <summary>
    /// Perform a spontaneous cast: consume any unused slot at the given level and
    /// cast the corresponding cure/inflict spell instead.
    /// Returns true if successful.
    /// </summary>
    public bool SpontaneousCastFromSlot(int spellLevel)
    {
        if (!CanSpontaneousCast(spellLevel))
        {
            Debug.LogWarning($"[Spellcasting] Cannot spontaneously cast at level {spellLevel}!");
            return false;
        }

        // Cantrips are unlimited — just verify a slot exists, don't consume
        if (spellLevel == 0)
        {
            SpellData spontSpell = GetSpontaneousSpell(spellLevel);
            Debug.Log($"[Spellcasting] {Stats.CharacterName} spontaneously cast cantrip {spontSpell?.Name} (unlimited, no slot consumed)");
            return true;
        }

        // Level 1+: consume an unused non-domain slot at this level.
        var slot = SpellSlots.FirstOrDefault(s => string.Equals(s.CasterClassName, "Cleric", System.StringComparison.OrdinalIgnoreCase) && s.Level == spellLevel && !s.IsUsed && s.HasSpell && !s.IsDomainSlot);
        if (slot == null)
        {
            Debug.LogWarning($"[Spellcasting] No available slot at level {spellLevel} for spontaneous casting!");
            return false;
        }

        SpellData spontSpell2 = GetSpontaneousSpell(spellLevel);
        string replacedSpell = slot.PreparedSpell?.Name ?? "empty";
        slot.Cast();
        SyncSlotsRemainingFromSpellSlots();
        SyncPreparedSpellsFromSlots();

        Debug.Log($"[Spellcasting] {Stats.CharacterName} spontaneously converted {replacedSpell} → {spontSpell2?.Name} " +
                  $"(Lv{spellLevel}). Remaining at Lv{spellLevel}: " +
                  $"{GetAvailableSlotsForLevel(spellLevel).Count}/{GetSlotsForLevel(spellLevel).Count}");

        return true;
    }

    /// <summary>
    /// Perform a spontaneous cast by sacrificing a SPECIFIC prepared spell slot.
    /// D&D 3.5e: Clerics can convert a specific prepared spell (except domain spells)
    /// into a cure/inflict spell of the same level.
    /// Finds a slot with the given spell ID and consumes it.
    /// Returns true if successful.
    /// </summary>
    public bool SpontaneousCastFromSpecificSpell(string sacrificedSpellId)
    {
        if (Stats == null || !Stats.IsCleric) return false;
        if (Stats.SpontaneousCasting == SpontaneousCastingType.None) return false;
        if (string.IsNullOrEmpty(sacrificedSpellId)) return false;

        // Find the specific slot with this spell that is unused
        var slot = SpellSlots.FirstOrDefault(s =>
            string.Equals(s.CasterClassName, "Cleric", System.StringComparison.OrdinalIgnoreCase) &&
            s.HasSpell && !s.IsUsed && !s.IsDomainSlot &&
            s.PreparedSpell.SpellId == sacrificedSpellId);

        if (slot == null)
        {
            Debug.LogWarning($"[Spellcasting] No available slot with spell '{sacrificedSpellId}' for spontaneous conversion!");
            return false;
        }

        int spellLevel = slot.Level;

        // Cantrips are unlimited — just verify, don't consume
        if (spellLevel == 0)
        {
            SpellData spontSpell = GetSpontaneousSpell(spellLevel);
            Debug.Log($"[Spellcasting] {Stats.CharacterName} spontaneously cast cantrip {spontSpell?.Name} " +
                      $"(unlimited, no slot consumed, sacrificed {slot.PreparedSpell.Name})");
            return true;
        }

        SpellData spontSpell2 = GetSpontaneousSpell(spellLevel);
        string replacedSpell = slot.PreparedSpell?.Name ?? "empty";
        slot.Cast();
        SyncSlotsRemainingFromSpellSlots();
        SyncPreparedSpellsFromSlots();

        Debug.Log($"[Spellcasting] {Stats.CharacterName} sacrificed {replacedSpell} → {spontSpell2?.Name} " +
                  $"(Lv{spellLevel}). Remaining at Lv{spellLevel}: " +
                  $"{GetAvailableSlotsForLevel(spellLevel).Count}/{GetSlotsForLevel(spellLevel).Count}");

        return true;
    }

    /// <summary>
    /// Check if a specific prepared spell can be spontaneously converted.
    /// Domain spell slots cannot be converted (D&D 3.5e cleric spontaneous casting rule).
    /// The spell must have an available (unused) slot.
    /// </summary>
    public bool CanConvertSpellToSpontaneous(SpellData spell)
    {
        if (spell == null) return false;
        if (Stats == null || !Stats.IsCleric) return false;
        if (Stats.SpontaneousCasting == SpontaneousCastingType.None) return false;

        // Check that a spontaneous spell exists at this level
        SpellData spontSpell = GetSpontaneousSpell(spell.SpellLevel);
        if (spontSpell == null) return false;

        // For cantrips: always available if prepared (unlimited)
        if (spell.SpellLevel == 0)
        {
            return SpellSlots.Any(s => string.Equals(s.CasterClassName, "Cleric", System.StringComparison.OrdinalIgnoreCase) && s.Level == 0 && s.HasSpell && s.PreparedSpell.SpellId == spell.SpellId && !s.DisabledByNegativeLevel);
        }

        // Domain slots cannot be converted (D&D 3.5e rule).
        // If the same spell appears in both regular and domain slots, conversion is allowed only when
        // at least one regular prepared slot exists and is unused.
        return SpellSlots.Any(s =>
            string.Equals(s.CasterClassName, "Cleric", System.StringComparison.OrdinalIgnoreCase) &&
            s.Level == spell.SpellLevel && !s.IsUsed && s.HasSpell && !s.IsDomainSlot &&
            s.PreparedSpell.SpellId == spell.SpellId);
    }

    /// <summary>Backward compat alias — calls CastSpellFromSlot.</summary>
    public bool CastWizardSpellFromSlot(SpellData spell)
    {
        return CastSpellFromSlot(spell);
    }

    /// <summary>
    /// Cast a wizard spell with metamagic by consuming a slot at the effective level.
    /// The spell occupies the slot at its effective (metamagic-adjusted) level.
    /// Returns true if successful.
    /// </summary>
    public bool CastWizardSpellWithMetamagic(SpellData spell, MetamagicData metamagic)
    {
        if (spell == null) return false;
        if (metamagic == null || !metamagic.HasAnyMetamagic)
            return CastWizardSpellFromSlot(spell);

        int effectiveLevel = metamagic.GetEffectiveSpellLevel(spell.SpellLevel);

        // Find an available slot at the effective level
        // For metamagic, we consume ANY unused slot at the effective level
        var slot = SpellSlots.FirstOrDefault(s =>
            s.Level == effectiveLevel && !s.IsUsed && s.HasSpell);

        if (slot == null)
        {
            Debug.LogWarning($"[Spellcasting] No available level {effectiveLevel} slot for metamagic {spell.Name}!");
            return false;
        }

        slot.Cast();
        SyncSlotsRemainingFromSpellSlots();
        SyncPreparedSpellsFromSlots();

        Debug.Log($"[Spellcasting] {Stats.CharacterName} cast metamagic {spell.Name} " +
                  $"(effective Lv{effectiveLevel}) from slot.");

        return true;
    }

    /// <summary>
    /// Sync the SlotsRemaining array from actual SpellSlot states.
    /// Cantrip slots are always counted as "remaining" (unlimited use).
    /// Called after any slot state change.
    /// </summary>
    private void SyncSlotsRemainingFromSpellSlots()
    {
        foreach (string className in GetPreparedCasterClassNames())
        {
            if (!_slotsMaxByClass.TryGetValue(className, out int[] classSlotsMax) || classSlotsMax == null)
                continue;

            int[] remaining = new int[classSlotsMax.Length];
            for (int level = 0; level < classSlotsMax.Length; level++)
            {
                if (level == 0)
                {
                    remaining[level] = SpellSlots.Count(s =>
                        s != null &&
                        string.Equals(s.CasterClassName, className, System.StringComparison.OrdinalIgnoreCase) &&
                        s.Level == 0 && s.HasSpell && !s.DisabledByNegativeLevel);
                }
                else
                {
                    remaining[level] = SpellSlots.Count(s =>
                        s != null &&
                        string.Equals(s.CasterClassName, className, System.StringComparison.OrdinalIgnoreCase) &&
                        s.Level == level && s.CanCast);
                }
            }

            _slotsRemainingByClass[className] = remaining;
        }

        if (_preparedCasterClasses.Count > 0)
        {
            string primary = _preparedCasterClasses[0];
            SlotsMax = GetSlotsMaxForClass(primary);
            SlotsRemaining = GetSlotsRemainingForClass(primary);
            _regularSlotsMax = _regularSlotsMaxByClass.TryGetValue(primary, out int[] regular) ? (int[])regular.Clone() : new int[0];
            _domainSlotsMax = _domainSlotsMaxByClass.TryGetValue(primary, out int[] domain) ? (int[])domain.Clone() : new int[0];
        }
    }

    /// <summary>
    /// Sync PreparedSpells list from SpellSlots for backward compatibility.
    /// PreparedSpells contains unique spells that have at least one available slot.
    /// Also syncs SlotsRemaining array from actual slot states.
    /// </summary>
    public void SyncPreparedSpellsFromSlots()
    {
        SyncSlotsRemainingFromSpellSlots();

        PreparedSpells.Clear();
        var seen = new HashSet<string>();

        foreach (var slot in SpellSlots)
        {
            if (!slot.HasSpell) continue;

            // Cantrips are always available if not disabled by negative levels.
            bool isAvailable = (slot.Level == 0) ? !slot.DisabledByNegativeLevel : slot.CanCast;

            if (isAvailable && !seen.Contains(slot.PreparedSpell.SpellId))
            {
                seen.Add(slot.PreparedSpell.SpellId);
                PreparedSpells.Add(slot.PreparedSpell);
            }
        }
    }

    public void ApplyNegativeLevelSlotLoss()
    {
        if (SpellSlots == null || SpellSlots.Count == 0)
            return;

        int toDisable = Stats != null ? Mathf.Max(0, Stats.NegativeLevelCount) : 0;

        for (int i = 0; i < SpellSlots.Count; i++)
            SpellSlots[i].DisabledByNegativeLevel = false;

        if (toDisable > 0)
        {
            // Lose highest-level slots first (D&D 3.5e negative levels).
            for (int level = 9; level >= 0 && toDisable > 0; level--)
            {
                for (int i = 0; i < SpellSlots.Count && toDisable > 0; i++)
                {
                    SpellSlot slot = SpellSlots[i];
                    if (slot == null || slot.Level != level || slot.DisabledByNegativeLevel)
                        continue;

                    slot.DisabledByNegativeLevel = true;
                    toDisable--;
                }
            }
        }

        SyncPreparedSpellsFromSlots();
    }

    /// <summary>
    /// Get a detailed string showing spell slot preparation.
    /// Works for both Wizards and Clerics.
    /// </summary>
    public string GetSlotDetails()
    {
        if (SpellSlots == null || SpellSlots.Count == 0) return "No spell slots";

        var parts = new List<string>();
        int slotNum = 1;
        foreach (var slot in SpellSlots)
        {
            string status;
            if (slot.Level == 0)
                status = "∞"; // Cantrips are unlimited
            else
                status = slot.IsUsed ? "✗" : "✓";

            string spellName = slot.HasSpell ? slot.PreparedSpell.Name : "(empty)";
            string slotLabel = slot.IsDomainSlot
                ? $"DomainSlot{slotNum}(L{slot.Level})"
                : slot.IsSpecialistSlot
                    ? $"SpecSlot{slotNum}(L{slot.Level})"
                    : $"Slot{slotNum}(L{slot.Level})";
            parts.Add($"{status} {slotLabel}: {spellName}");
            slotNum++;
        }
        return string.Join(", ", parts);
    }

    /// <summary>Backward compat alias — calls GetSlotDetails.</summary>
    public string GetWizardSlotDetails()
    {
        return GetSlotDetails();
    }

    // ========== CLERIC SPELL PREPARATION (LEGACY COMPAT) ==========

    /// <summary>
    /// Prepare spells for Cleric casting (legacy compat — now routes to slot system).
    /// </summary>
    public void PrepareSpellsCleric()
    {
        AutoPrepareClericSlots();
        SyncPreparedSpellsFromSlots();
        Debug.Log($"[Spellcasting] {(Stats != null ? Stats.CharacterName : "?")} (Cleric) prepared {PreparedSpells.Count} spells via slot system");
    }

    /// <summary>
    /// Prepare spells for casting. Routes to appropriate system based on class.
    /// Both Wizards and Clerics now use slot-based preparation.
    /// </summary>
    public void PrepareSpells()
    {
        if (Stats != null && Stats.IsWizard)
        {
            AutoPrepareWizardSlots();
            SyncPreparedSpellsFromSlots();
        }
        else if (Stats != null && Stats.IsCleric)
        {
            AutoPrepareClericSlots();
            SyncPreparedSpellsFromSlots();
        }
    }

    /// <summary>
    /// Get prepared spells filtered by spell level.
    /// Uses slot-based system for both Wizards and Clerics.
    /// Returns unique spells that have available slots at that level.
    /// Cantrips are always included if prepared (unlimited use).
    /// </summary>
    public List<SpellData> GetPreparedSpellsByLevel(int level)
    {
        if (UsesPreparedSlotSystem && SpellSlots.Count > 0)
        {
            return GetUniqueAvailableSpells(level);
        }
        return PreparedSpells.Where(s => s.SpellLevel == level).ToList();
    }

    /// <summary>
    /// Check if a specific spell can be cast right now.
    /// Uses slot-based system for both Wizards and Clerics.
    /// Cantrips are always castable if prepared (unlimited use).
    /// </summary>
    public bool CanCastSpell(SpellData spell)
    {
        if (spell == null || SlotsRemaining == null) return false;
        int level = spell.SpellLevel;
        if (level >= SlotsRemaining.Length) return false;

        if (UsesPreparedSlotSystem && SpellSlots.Count > 0)
        {
            return CountAvailablePreparedSpell(spell) > 0;
        }

        return PreparedSpells.Contains(spell) && SlotsRemaining[level] > 0;
    }

    /// <summary>
    /// Get list of prepared spells that can currently be cast (have slots available).
    /// This is the primary method used by the spell casting UI during combat.
    /// Cantrips are always included if prepared (unlimited use).
    /// </summary>
    public List<SpellData> GetCastablePreparedSpells()
    {
        var castable = new List<SpellData>();

        if (UsesPreparedSlotSystem && SpellSlots.Count > 0)
        {
            var seen = new HashSet<string>();
            foreach (var slot in SpellSlots)
            {
                if (!slot.HasSpell) continue;

                // Cantrips available if prepared and not disabled by negative levels.
                bool available = (slot.Level == 0) ? !slot.DisabledByNegativeLevel : slot.CanCast;

                if (available && !seen.Contains(slot.PreparedSpell.SpellId))
                {
                    seen.Add(slot.PreparedSpell.SpellId);
                    castable.Add(slot.PreparedSpell);
                }
            }
        }
        else
        {
            foreach (var spell in PreparedSpells)
            {
                if (CanCastSpell(spell))
                    castable.Add(spell);
            }
        }
        return castable;
    }

    /// <summary>
    /// Check if the character has at least one prepared spell they can cast.
    /// </summary>
    public bool HasAnyCastablePreparedSpell()
    {
        if (UsesPreparedSlotSystem && SpellSlots.Count > 0)
        {
            // Any cantrip prepared OR any level 1+ slot with spell and not used
            return SpellSlots.Any(s => s.HasSpell && ((s.Level == 0 && !s.DisabledByNegativeLevel) || s.CanCast));
        }
        return GetCastablePreparedSpells().Count > 0;
    }

    // ========== SLOT MANAGEMENT ==========

    /// <summary>
    /// Check if the character can cast a specific spell (has slots remaining).
    /// Cantrips are always castable if prepared (unlimited use).
    /// </summary>
    public bool CanCast(SpellData spell)
    {
        if (spell == null || SlotsRemaining == null) return false;
        if (spell.SpellLevel >= SlotsRemaining.Length) return false;

        if (UsesPreparedSlotSystem && SpellSlots.Count > 0)
        {
            return CountAvailablePreparedSpell(spell) > 0;
        }

        return SlotsRemaining[spell.SpellLevel] > 0;
    }

    /// <summary>
    /// Check if the character can cast a spell with metamagic applied
    /// (has a slot at the required effective level).
    /// </summary>
    public bool CanCastWithMetamagic(SpellData spell, MetamagicData metamagic)
    {
        if (spell == null || SlotsRemaining == null) return false;
        if (metamagic == null || !metamagic.HasAnyMetamagic)
            return CanCast(spell);

        int effectiveLevel = metamagic.GetEffectiveSpellLevel(spell.SpellLevel);
        if (effectiveLevel >= SlotsRemaining.Length) return false;

        // Cantrips with metamagic still need higher level slots
        return SlotsRemaining[effectiveLevel] > 0;
    }

    /// <summary>
    /// Get the highest spell slot level available to this caster.
    /// </summary>
    public int GetHighestSlotLevel()
    {
        if (SpellSlots == null || SpellSlots.Count == 0)
            return -1;

        int highest = -1;
        for (int i = 0; i < SpellSlots.Count; i++)
        {
            SpellSlot slot = SpellSlots[i];
            if (slot == null)
                continue;
            highest = Mathf.Max(highest, slot.Level);
        }

        return highest;
    }

    /// <summary>
    /// Check if a slot at a specific level is available.
    /// </summary>
    public bool HasSlotAtLevel(int level)
    {
        if (level < 0)
            return false;

        return SpellSlots.Any(s => s != null && s.Level == level && (level == 0 ? s.HasSpell && !s.DisabledByNegativeLevel : s.CanCast));
    }

    /// <summary>
    /// Get the list of metamagic feats the character knows (from CharacterStats.Feats).
    /// </summary>
    public List<MetamagicFeatId> GetKnownMetamagicFeats()
    {
        var result = new List<MetamagicFeatId>();
        if (Stats == null || Stats.Feats == null) return result;

        foreach (var mmId in MetamagicData.AllMetamagicFeats)
        {
            string featName = MetamagicData.GetFeatName(mmId);
            if (Stats.HasFeat(featName))
                result.Add(mmId);
        }
        return result;
    }

    /// <summary>
    /// Check if the character knows any metamagic feats.
    /// </summary>
    public bool HasAnyMetamagicFeat()
    {
        return GetKnownMetamagicFeats().Count > 0;
    }

    /// <summary>
    /// Consume a spell slot for the given spell level.
    /// Cantrips (level 0) are unlimited — this is a no-op that returns true.
    /// For both Wizards and Clerics: uses slot-based system.
    /// Returns true if successful.
    /// </summary>
    public bool ConsumeSlot(int spellLevel)
    {
        // Cantrips are unlimited — always succeed without consuming
        if (spellLevel == 0) return true;

        if (spellLevel >= SlotsRemaining.Length || SlotsRemaining[spellLevel] <= 0)
            return false;

        if (SpellSlots.Count > 0)
        {
            // Use slot-based system for both Wizards and Clerics
            var slot = SpellSlots.FirstOrDefault(s => s.Level == spellLevel && s.CanCast);
            if (slot == null) return false;
            slot.Cast();
            SyncSlotsRemainingFromSpellSlots();
            SyncPreparedSpellsFromSlots();
            return true;
        }

        SlotsRemaining[spellLevel]--;
        return true;
    }

    /// <summary>
    /// Get the spell save DC for this caster.
    /// DC = 10 + spell level + casting ability modifier.
    /// Wizard uses INT, Cleric uses WIS.
    /// </summary>
    public int GetSpellDC(SpellData spell)
    {
        string castingClass = GetPreferredCastingClassForSpell(spell);
        int castingMod = string.Equals(castingClass, "Wizard", System.StringComparison.OrdinalIgnoreCase)
            ? Stats.INTMod
            : Stats.WISMod;
        return 10 + spell.SpellLevel + castingMod;
    }

    /// <summary>
    /// Get the ranged touch attack bonus for spells.
    /// = BAB + DEX modifier + size modifier.
    /// </summary>
    public int GetRangedTouchAttackBonus()
    {
        return Stats.BaseAttackBonus + Stats.DEXMod + Stats.SizeModifier;
    }

    /// <summary>
    /// Get the melee touch attack bonus for spells.
    /// = BAB + STR modifier + size modifier.
    /// </summary>
    public int GetMeleeTouchAttackBonus()
    {
        return Stats.BaseAttackBonus + Stats.STRMod + Stats.SizeModifier;
    }

    /// <summary>
    /// Get touch AC of a target.
    /// Touch AC excludes armor/shield bonuses but keeps DEX, size, deflection,
    /// dodge-style feat AC, monk AC bonus, and rage AC modifiers.
    /// </summary>
    public static int GetTouchAC(CharacterStats target)
    {
        int dexToAC = target.DEXMod;
        if (target.MaxDexBonus >= 0 && dexToAC > target.MaxDexBonus)
            dexToAC = target.MaxDexBonus;

        return 10 + dexToAC + target.SizeModifier + target.DeflectionBonus
               + target.FeatACBonus + target.MonkACBonus + target.RageACPenalty;
    }

    /// <summary>
    /// Apply a buff from a spell (e.g., Mage Armor, Shield, stat buffs).
    /// </summary>
    public void ApplyBuff(SpellData spell)
    {
        int duration = spell.BuffDurationRounds;

        if (spell.SpellId == SpellNames.MAGE_ARMOR)
        {
            MageArmorACBonus = spell.BuffACBonus;
            ActiveBuffs[SpellNames.MAGE_ARMOR] = duration;
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Mage Armor active, +{spell.BuffACBonus} AC (armor)");
        }
        else if (spell.SpellId == SpellNames.SHIELD)
        {
            ActiveBuffs[SpellNames.SHIELD] = duration;
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Shield active, +{spell.BuffShieldBonus} shield AC");
        }
        else if (!string.IsNullOrEmpty(spell.BuffStatName) && spell.BuffStatBonus != 0)
        {
            // Stat buff (Bull's Strength, Cat's Grace, etc.)
            ActiveBuffs[spell.SpellId] = duration;
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: {spell.Name} active, " +
                      $"+{spell.BuffStatBonus} {spell.BuffStatName} for {duration} rounds");
        }
        else if (spell.BuffAttackBonus != 0 || spell.BuffSaveBonus != 0 || spell.BuffDamageBonus != 0)
        {
            // Combat buff (Bless, Divine Favor, etc.)
            ActiveBuffs[spell.SpellId] = duration;
            string bonuses = "";
            if (spell.BuffAttackBonus != 0) bonuses += $"attack {spell.BuffAttackBonus:+#;-#} ";
            if (spell.BuffDamageBonus != 0) bonuses += $"damage {spell.BuffDamageBonus:+#;-#} ";
            if (spell.BuffSaveBonus != 0) bonuses += $"saves {spell.BuffSaveBonus:+#;-#} ";
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: {spell.Name} active, {bonuses.Trim()}");
        }
        else if (spell.BuffDeflectionBonus != 0 || spell.BuffACBonus != 0)
        {
            ActiveBuffs[spell.SpellId] = duration;
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: {spell.Name} active");
        }
        else if (spell.BuffTempHP > 0)
        {
            ActiveBuffs[spell.SpellId] = duration;
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: {spell.Name} active, +{spell.BuffTempHP} temp HP");
        }
        else
        {
            // Generic buff tracking
            ActiveBuffs[spell.SpellId] = duration;
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: {spell.Name} active");
        }
    }

    /// <summary>
    /// Apply Mage Armor to a target character's SpellcastingComponent.
    /// </summary>
    public static void ApplyMageArmor(CharacterController target, SpellData spell)
    {
        var sc = target.GetComponent<SpellcastingComponent>();
        if (sc != null)
        {
            sc.MageArmorACBonus = spell.BuffACBonus;
            sc.ActiveBuffs[SpellNames.MAGE_ARMOR] = -1;
        }
        else
        {
            // Target doesn't have SpellcastingComponent, add one just for the buff
            sc = target.gameObject.AddComponent<SpellcastingComponent>();
            sc.Stats = target.Stats;
            sc.SlotsMax = new int[0];
            sc.SlotsRemaining = new int[0];
            sc.MageArmorACBonus = spell.BuffACBonus;
            sc.ActiveBuffs[SpellNames.MAGE_ARMOR] = -1;
        }
        Debug.Log($"[Spellcasting] Mage Armor applied to {target.Stats.CharacterName}: +{spell.BuffACBonus} AC");
    }

    /// <summary>
    /// Tick buff durations at the start of the caster's turn.
    /// Removes expired buffs.
    /// </summary>
    public void TickBuffs()
    {
        var expired = new List<string>();
        var keys = new List<string>(ActiveBuffs.Keys);
        foreach (string key in keys)
        {
            if (ActiveBuffs[key] > 0)
            {
                ActiveBuffs[key]--;
                if (ActiveBuffs[key] <= 0)
                    expired.Add(key);
            }
            // -1 = indefinite (hours/level), don't tick
        }

        foreach (string key in expired)
        {
            RemoveBuff(key);
        }
    }

    /// <summary>Remove a specific buff.</summary>
    public void RemoveBuff(string spellId)
    {
        if (spellId == SpellNames.MAGE_ARMOR)
        {
            MageArmorACBonus = 0;
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Mage Armor expired");
        }
        else
        {
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: {spellId} buff expired");
        }
        ActiveBuffs.Remove(spellId);
    }

    /// <summary>
    /// Store a held melee touch spell charge on this caster.
    /// </summary>
    public void SetHeldTouchCharge(SpellData spell, MetamagicData metamagic)
    {
        HeldTouchSpell = spell;
        HeldTouchMetamagic = metamagic;

        if (HeldTouchSpell != null)
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: holding touch charge ({HeldTouchSpell.Name})");
    }

    /// <summary>
    /// Clear any held melee touch charge.
    /// </summary>
    public void ClearHeldTouchCharge(string reason = null)
    {
        if (HeldTouchSpell != null)
        {
            string reasonText = string.IsNullOrEmpty(reason) ? "" : $" ({reason})";
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: held touch charge cleared{reasonText}");
        }

        HeldTouchSpell = null;
        HeldTouchMetamagic = null;
    }

    /// <summary>
    /// Get the number of Magic Missile missiles at the current caster level.
    /// D&D 3.5: 1 missile at CL1, +1 per 2 CL above 1st (max 5 at CL9).
    /// CL3 = 2 missiles.
    /// </summary>
    public int GetMagicMissileCount()
    {
        int cl = Mathf.Max(1, Stats != null ? Stats.GetCasterLevel("Wizard") : 1);
        return Mathf.Min(5, 1 + (cl - 1) / 2);
    }

    /// <summary>Get a summary string of remaining spell slots.</summary>
    public string GetSlotSummary()
    {
        List<string> classNames = GetPreparedCasterClassNames();
        if (classNames.Count == 0)
            return "None";

        var classParts = new List<string>();
        for (int c = 0; c < classNames.Count; c++)
        {
            string className = classNames[c];
            int[] classMax = GetSlotsMaxForClass(className);
            int[] classRemaining = GetSlotsRemainingForClass(className);
            if (classMax == null || classRemaining == null || classMax.Length == 0)
                continue;

            var parts = new List<string>();
            for (int i = 0; i < classMax.Length; i++)
            {
                string label = i == 0 ? "L0" : $"L{i}";
                if (i == 0)
                    parts.Add($"{label}:∞");
                else
                    parts.Add($"{label}:{classRemaining[i]}/{classMax[i]}");
            }

            classParts.Add($"{className}[{string.Join(", ", parts)}]");
        }

        return classParts.Count > 0 ? string.Join(" | ", classParts) : "None";
    }

    /// <summary>
    /// Get list of spells the character can currently cast (has slots for).
    /// </summary>
    public List<SpellData> GetCastableSpells()
    {
        var castable = new List<SpellData>();
        foreach (var spell in KnownSpells)
        {
            if (CanCast(spell))
                castable.Add(spell);
        }
        return castable;
    }

    /// <summary>Whether the character has at least one spell they can currently cast.</summary>
    public bool HasAnyCastableSpell()
    {
        return GetCastableSpells().Count > 0;
    }

    /// <summary>Get remaining slots for a specific spell level.</summary>
    public int GetSlotsRemaining(int level)
    {
        if (level < 0)
            return 0;

        int total = 0;
        foreach (string className in GetPreparedCasterClassNames())
        {
            int[] classRemaining = GetSlotsRemainingForClass(className);
            if (classRemaining != null && level < classRemaining.Length)
                total += classRemaining[level];
        }

        return total;
    }

    /// <summary>Get maximum slots for a specific spell level.</summary>
    public int GetMaxSlots(int level)
    {
        if (level < 0)
            return 0;

        int total = 0;
        foreach (string className in GetPreparedCasterClassNames())
        {
            int[] classMax = GetSlotsMaxForClass(className);
            if (classMax != null && level < classMax.Length)
                total += classMax[level];
        }

        return total;
    }

    /// <summary>
    /// Reset the quickened spell tracking for a new round.
    /// Called at the start of each combat round (when PC1's turn begins).
    /// </summary>
    public void ResetQuickenedSpellTracking()
    {
        HasCastQuickenedSpellThisRound = false;
    }

    /// <summary>
    /// Record that this character has cast a quickened spell this round.
    /// </summary>
    public void MarkQuickenedSpellCast()
    {
        HasCastQuickenedSpellThisRound = true;
        Debug.Log($"[Spellcasting] {Stats.CharacterName}: Quickened spell cast this round (limit reached)");
    }

    /// <summary>
    /// Check if this character can use Quicken Spell metamagic this round.
    /// D&D 3.5e: Only one quickened spell per round.
    /// </summary>
    public bool CanUseQuickenSpell()
    {
        return !HasCastQuickenedSpellThisRound;
    }

    /// <summary>
    /// Rest to restore all spell slots to maximum.
    /// For both Wizards and Clerics: restores all SpellSlot.IsUsed to false.
    /// Prepared spells stay the same unless re-prepared.
    /// </summary>
    public void RestoreAllSlots()
    {
        if (SpellSlots.Count > 0)
        {
            foreach (var slot in SpellSlots)
            {
                slot.Rest(); // Marks as not used, keeps prepared spell
            }
            SyncSlotsRemainingFromSpellSlots();
            SyncPreparedSpellsFromSlots();
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Spell slots restored - {GetSlotSummary()}");
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Prepared spells unchanged: {GetSlotDetails()}");
        }
        else
        {
            if (SlotsMax == null) return;
            SlotsRemaining = (int[])SlotsMax.Clone();
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: All spell slots restored - {GetSlotSummary()}");
        }
    }

    /// <summary>
    /// Calculate the effective spell slot level cost for a spell with metamagic.
    /// Used by CombatUI for displaying slot cost.
    /// </summary>
    public int CalculateSpellSlotCost(SpellData spell, MetamagicData metamagic)
    {
        if (metamagic == null || !metamagic.HasAnyMetamagic) return spell.SpellLevel;
        return metamagic.GetEffectiveSpellLevel(spell.SpellLevel);
    }
}
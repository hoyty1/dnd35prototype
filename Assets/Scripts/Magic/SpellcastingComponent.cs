using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages spellcasting for a character: known/prepared spells, spell slots, and casting.
/// Attached to a character GameObject alongside CharacterController and InventoryComponent.
///
/// D&D 3.5e Spell Slots at Level 3:
/// Wizard: 4/2+bonus/1+bonus (cantrips/1st/2nd)
/// Cleric: 4/2+1domain+bonus/1+1domain+bonus (cantrips/1st/2nd)
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

    /// <summary>List of all spells this character knows/has prepared.</summary>
    public List<SpellData> KnownSpells = new List<SpellData>();

    /// <summary>
    /// Spell slots remaining per spell level. Index = spell level.
    /// [0] = cantrip slots, [1] = 1st level slots, [2] = 2nd level slots, etc.
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

    /// <summary>Whether this character has any spellcasting ability.</summary>
    public bool CanCastSpells => Stats != null && (Stats.IsWizard || Stats.IsCleric);

    /// <summary>
    /// Wizard spellbook: SpellIds selected during character creation.
    /// If set before Init(), only these spells (plus all cantrips) will be known.
    /// If null, all available spells are added (backwards compatibility).
    /// </summary>
    public List<string> SelectedSpellIds { get; set; }

    /// <summary>
    /// Initialize spellcasting for a character based on their class and level.
    /// </summary>
    public void Init(CharacterStats stats)
    {
        Stats = stats;
        SpellDatabase.Init();

        if (stats.IsWizard)
        {
            InitWizard(stats.Level);
        }
        else if (stats.IsCleric)
        {
            InitCleric(stats.Level);
        }

        Debug.Log($"[Spellcasting] {stats.CharacterName} ({stats.CharacterClass}): " +
                  $"{KnownSpells.Count} spells, slots: {GetSlotSummary()}");
    }

    /// <summary>
    /// Initialize Wizard spellcasting.
    /// D&D 3.5 Wizard Level 3: 4/2+bonus/1+bonus (cantrips/1st/2nd)
    /// Wizards know all cantrips. 1st/2nd level spells come from spellbook.
    /// Bonus spell slots from INT: +1 slot at spell level N if INT mod >= N
    /// </summary>
    private void InitWizard(int level)
    {
        int intMod = Mathf.Max(0, Stats.INTMod);

        // D&D 3.5e Wizard spell slots at level 3:
        // Base: 4 cantrips, 2 first, 1 second
        // Bonus: +1 per level where intMod >= level
        int bonus1st = intMod >= 1 ? 1 : 0;
        int bonus2nd = intMod >= 2 ? 1 : 0;

        SlotsMax = new int[] { 4, 2 + bonus1st, 1 + bonus2nd };
        SlotsRemaining = (int[])SlotsMax.Clone();

        // Wizard knows ALL cantrips
        KnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Wizard", 0));

        // For 1st and 2nd level spells, use selected spells if available
        if (SelectedSpellIds != null && SelectedSpellIds.Count > 0)
        {
            foreach (string spellId in SelectedSpellIds)
            {
                SpellData spell = SpellDatabase.GetSpell(spellId);
                if (spell != null && spell.SpellLevel > 0)
                {
                    KnownSpells.Add(spell);
                }
            }
            Debug.Log($"[Spellcasting] Wizard loaded {SelectedSpellIds.Count} selected spells from spellbook.");
        }
        else
        {
            // Backwards compatibility: if no selection made, know all spells
            KnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Wizard", 1));
            KnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Wizard", 2));
            Debug.Log("[Spellcasting] Wizard: no spell selection found, added all available spells.");
        }
    }

    /// <summary>
    /// Initialize Cleric spellcasting.
    /// D&D 3.5 Cleric Level 3: 4/2+1domain+bonus/1+1domain+bonus
    /// Clerics have access to entire cleric spell list (prepared caster).
    /// Bonus spell slots from WIS: +1 slot at spell level N if WIS mod >= N
    /// </summary>
    private void InitCleric(int level)
    {
        int wisMod = Mathf.Max(0, Stats.WISMod);

        // D&D 3.5e Cleric spell slots at level 3:
        // Base: 4 cantrips, 2 first + 1 domain, 1 second + 1 domain
        // Bonus: +1 per level where wisMod >= level
        int bonus1st = wisMod >= 1 ? 1 : 0;
        int bonus2nd = wisMod >= 2 ? 1 : 0;

        SlotsMax = new int[] { 4, 3 + bonus1st, 2 + bonus2nd }; // 2 base + 1 domain + bonus for each
        SlotsRemaining = (int[])SlotsMax.Clone();

        // Cleric knows ALL cleric spells (prepared from full list)
        KnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 0));
        KnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 1));
        KnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 2));
    }

    /// <summary>
    /// Check if the character can cast a specific spell (has slots remaining).
    /// </summary>
    public bool CanCast(SpellData spell)
    {
        if (spell == null || SlotsRemaining == null) return false;
        if (spell.SpellLevel >= SlotsRemaining.Length) return false;
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
        if (SlotsRemaining == null) return -1;
        return SlotsRemaining.Length - 1;
    }

    /// <summary>
    /// Check if a slot at a specific level is available.
    /// </summary>
    public bool HasSlotAtLevel(int level)
    {
        if (SlotsRemaining == null || level < 0 || level >= SlotsRemaining.Length) return false;
        return SlotsRemaining[level] > 0;
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
    /// Returns true if successful.
    /// </summary>
    public bool ConsumeSlot(int spellLevel)
    {
        if (spellLevel >= SlotsRemaining.Length || SlotsRemaining[spellLevel] <= 0)
            return false;
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
        int castingMod = Stats.IsWizard ? Stats.INTMod : Stats.WISMod;
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
    /// Get touch AC of a target (10 + DEX + size + deflection, NO armor/shield/natural).
    /// Simplified: 10 + DEX mod + size modifier.
    /// </summary>
    public static int GetTouchAC(CharacterStats target)
    {
        return 10 + target.DEXMod + target.SizeModifier;
    }

    /// <summary>
    /// Apply a buff from a spell (e.g., Mage Armor, Shield, stat buffs).
    /// </summary>
    public void ApplyBuff(SpellData spell)
    {
        int duration = spell.BuffDurationRounds;

        if (spell.SpellId == "mage_armor")
        {
            MageArmorACBonus = spell.BuffACBonus;
            ActiveBuffs["mage_armor"] = duration;
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Mage Armor active, +{spell.BuffACBonus} AC (armor)");
        }
        else if (spell.SpellId == "shield")
        {
            ActiveBuffs["shield"] = duration;
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
            sc.ActiveBuffs["mage_armor"] = -1;
        }
        else
        {
            // Target doesn't have SpellcastingComponent, add one just for the buff
            sc = target.gameObject.AddComponent<SpellcastingComponent>();
            sc.Stats = target.Stats;
            sc.SlotsMax = new int[0];
            sc.SlotsRemaining = new int[0];
            sc.MageArmorACBonus = spell.BuffACBonus;
            sc.ActiveBuffs["mage_armor"] = -1;
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
        if (spellId == "mage_armor")
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
    /// Get the number of Magic Missile missiles at the current caster level.
    /// D&D 3.5: 1 missile at CL1, +1 per 2 CL above 1st (max 5 at CL9).
    /// CL3 = 2 missiles.
    /// </summary>
    public int GetMagicMissileCount()
    {
        int cl = Stats.Level;
        return Mathf.Min(5, 1 + (cl - 1) / 2);
    }

    /// <summary>Get a summary string of remaining spell slots.</summary>
    public string GetSlotSummary()
    {
        if (SlotsRemaining == null || SlotsRemaining.Length == 0) return "None";
        var parts = new List<string>();
        for (int i = 0; i < SlotsRemaining.Length; i++)
        {
            string label = i == 0 ? "Cantrips" : $"Lv{i}";
            parts.Add($"{label}: {SlotsRemaining[i]}/{SlotsMax[i]}");
        }
        return string.Join(", ", parts);
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
        if (SlotsRemaining == null || level >= SlotsRemaining.Length) return 0;
        return SlotsRemaining[level];
    }

    /// <summary>Get maximum slots for a specific spell level.</summary>
    public int GetMaxSlots(int level)
    {
        if (SlotsMax == null || level >= SlotsMax.Length) return 0;
        return SlotsMax[level];
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
    /// </summary>
    public void RestoreAllSlots()
    {
        if (SlotsMax == null) return;
        SlotsRemaining = (int[])SlotsMax.Clone();
        Debug.Log($"[Spellcasting] {Stats.CharacterName}: All spell slots restored - {GetSlotSummary()}");
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

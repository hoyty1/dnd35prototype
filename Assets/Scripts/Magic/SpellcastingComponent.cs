using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages spellcasting for a character: known/prepared spells, spell slots, and casting.
/// Attached to a character GameObject alongside CharacterController and InventoryComponent.
///
/// D&D 3.5e Spell Slots at Level 3:
/// Wizard: 4/2/1 (cantrips/1st/2nd) - but we only have cantrips and 1st, so 4/2
/// Cleric: 4/2+1 (cantrips/1st+domain) - simplified to 4/3 (including domain slot)
///
/// Cantrips (Level 0): Unlimited uses per day (D&D 3.5 still uses slots, but for simplicity
/// we make cantrips unlimited as they're very weak).
/// Actually in D&D 3.5 cantrips use slots, so we'll give the proper number but they're weak enough.
/// Let's follow RAW: Wizard gets 4 cantrip slots, Cleric gets 4 cantrip slots.
/// </summary>
public class SpellcastingComponent : MonoBehaviour
{
    /// <summary>Reference to the character stats.</summary>
    public CharacterStats Stats { get; private set; }

    /// <summary>List of all spells this character knows/has prepared.</summary>
    public List<SpellData> KnownSpells = new List<SpellData>();

    /// <summary>
    /// Spell slots remaining per spell level. Index = spell level.
    /// [0] = cantrip slots, [1] = 1st level slots, etc.
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

    /// <summary>Whether this character has any spellcasting ability.</summary>
    public bool CanCastSpells => Stats != null && (Stats.IsWizard || Stats.IsCleric);

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
    /// D&D 3.5 Wizard Level 3: 4 cantrip slots, 2 1st-level slots (+1 bonus from INT 16+).
    /// Wizards know all cantrips and prepare from spellbook.
    /// </summary>
    private void InitWizard(int level)
    {
        // Spell slots: base + bonus from INT
        // Level 3 Wizard base: 4/2 (cantrips/1st)
        // Bonus spells from INT: INT 16 (+3 mod) = +1 1st level slot
        int intMod = Mathf.Max(0, Stats.INTMod);
        int bonus1st = intMod >= 1 ? 1 : 0;

        SlotsMax = new int[] { 4, 2 + bonus1st };
        SlotsRemaining = new int[] { SlotsMax[0], SlotsMax[1] };

        // Wizard knows all cantrips and selected 1st level spells
        KnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Wizard", 0));
        KnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Wizard", 1));
    }

    /// <summary>
    /// Initialize Cleric spellcasting.
    /// D&D 3.5 Cleric Level 3: 4 cantrip slots, 2 1st-level slots (+1 domain + bonus from WIS).
    /// Clerics have access to entire cleric spell list (prepared caster).
    /// </summary>
    private void InitCleric(int level)
    {
        // Spell slots: base + bonus from WIS + domain
        // Level 3 Cleric base: 4/2+1domain (cantrips/1st)
        // Bonus spells from WIS: WIS 16 (+3 mod) = +1 1st level slot
        int wisMod = Mathf.Max(0, Stats.WISMod);
        int bonus1st = wisMod >= 1 ? 1 : 0;

        SlotsMax = new int[] { 4, 3 + bonus1st }; // 2 base + 1 domain + WIS bonus
        SlotsRemaining = new int[] { SlotsMax[0], SlotsMax[1] };

        // Cleric knows all cleric spells (prepared from full list)
        KnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 0));
        KnownSpells.AddRange(SpellDatabase.GetSpellsForClassAtLevel("Cleric", 1));
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
    /// Apply a buff from a spell (e.g., Mage Armor).
    /// </summary>
    public void ApplyBuff(SpellData spell)
    {
        if (spell.SpellId == "mage_armor")
        {
            MageArmorACBonus = spell.BuffACBonus;
            // Duration: -1 means hours/level, effectively whole combat
            ActiveBuffs["mage_armor"] = -1;
            Debug.Log($"[Spellcasting] {Stats.CharacterName}: Mage Armor active, +{spell.BuffACBonus} AC (armor)");
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
    /// Rest to restore all spell slots to maximum.
    /// </summary>
    public void RestoreAllSlots()
    {
        if (SlotsMax == null) return;
        SlotsRemaining = (int[])SlotsMax.Clone();
        Debug.Log($"[Spellcasting] {Stats.CharacterName}: All spell slots restored - {GetSlotSummary()}");
    }
}

using System.Collections.Generic;

/// <summary>
/// Interface defining the contract for all D&D 3.5 character class definitions.
/// Each class implements this to provide its specific properties and behaviors.
/// </summary>
public interface ICharacterClass
{
    // ========== IDENTITY ==========

    /// <summary>Display name of the class (e.g., "Fighter", "Rogue").</summary>
    string ClassName { get; }

    /// <summary>Short description of the class for UI display.</summary>
    string Description { get; }

    // ========== CORE STATS ==========

    /// <summary>Hit die sides (e.g., 10 for d10).</summary>
    int HitDie { get; }

    /// <summary>Base Attack Bonus at level 3. Full=3, 3/4=2, 1/2=1.</summary>
    int BABAtLevel3 { get; }

    /// <summary>Base skill points per level (before INT modifier).</summary>
    int SkillPointsPerLevel { get; }

    // ========== SAVE PROGRESSIONS ==========

    /// <summary>Whether Fortitude is a good save for this class.</summary>
    bool GoodFortitude { get; }

    /// <summary>Whether Reflex is a good save for this class.</summary>
    bool GoodReflex { get; }

    /// <summary>Whether Will is a good save for this class.</summary>
    bool GoodWill { get; }

    // ========== SKILLS ==========

    /// <summary>Set of class skill names for this class.</summary>
    HashSet<string> ClassSkills { get; }

    // ========== STARTING EQUIPMENT ==========

    /// <summary>Default armor bonus for character creation.</summary>
    int DefaultArmorBonus { get; }

    /// <summary>Default shield bonus for character creation.</summary>
    int DefaultShieldBonus { get; }

    /// <summary>Default weapon damage dice sides for character creation.</summary>
    int DefaultDamageDice { get; }

    /// <summary>
    /// Set up starting equipment on the given inventory component.
    /// Called during character creation to equip PHB starting packages.
    /// </summary>
    void SetupStartingEquipment(InventoryComponent inv);

    // ========== SPELLCASTING ==========

    /// <summary>Whether this class is a spellcasting class.</summary>
    bool IsSpellcaster { get; }

    // ========== UI ==========

    /// <summary>Title color for class selection UI panel.</summary>
    UnityEngine.Color TitleColor { get; }

    /// <summary>Button color for class selection UI.</summary>
    UnityEngine.Color ButtonColor { get; }

    /// <summary>Info text displayed in the class selection panel.</summary>
    string InfoText { get; }

    // ========== CLASS FEATURES ==========

    /// <summary>
    /// Initialize AUTOMATIC feats for this class on the given character stats.
    /// Called during character creation via CharacterStats constructor.
    /// 
    /// D&D 3.5e PHB Rules — Only the following classes grant automatic feats:
    ///   - Monk: Improved Unarmed Strike + Stunning Fist at 1st level (PHB p.40)
    ///   - All other classes: NO automatic feats
    /// 
    /// IMPORTANT: Bonus feat SELECTIONS (Fighter bonus feats, Monk bonus feat choices,
    /// Wizard bonus feats, etc.) are NOT automatic — they are handled by the
    /// character creation UI / level-up system, not by this method.
    /// </summary>
    void InitFeats(CharacterStats stats);
}

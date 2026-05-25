// ============================================================================
// D&D 3.5e Animal Companion System (PHB p.36/p.48)
// Shared by Druid (effective level = Druid level) and Ranger (level - 3).
// Companion scales HD, natural armor, Str/Dex bonuses, tricks, and feats.
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines an available animal companion species with base stats.
/// D&D 3.5e PHB p.36, Table 3-8.
/// </summary>
[Serializable]
public class AnimalCompanionTemplate
{
    public string Name;
    public int BaseHD;           // Base hit dice
    public int BaseNaturalArmor; // Base natural armor bonus
    public int BaseSTR;
    public int BaseDEX;
    public int BaseCON;
    public int BaseINT;          // Typically 1-2 for animals
    public int BaseWIS;
    public int BaseCHA;
    public int BaseSpeed;        // In 5-ft squares
    public int BaseAttackBonus;
    public string AttackType;    // e.g., "Bite", "Claw"
    public string DamageDice;    // e.g., "1d6+2"
    public string Size;          // Tiny, Small, Medium, Large

    public AnimalCompanionTemplate(string name, int hd, int nat, int str, int dex, int con,
        int intel, int wis, int cha, int speed, int bab, string atkType, string dmg, string size)
    {
        Name = name; BaseHD = hd; BaseNaturalArmor = nat;
        BaseSTR = str; BaseDEX = dex; BaseCON = con;
        BaseINT = intel; BaseWIS = wis; BaseCHA = cha;
        BaseSpeed = speed; BaseAttackBonus = bab;
        AttackType = atkType; DamageDice = dmg; Size = size;
    }
}

/// <summary>
/// D&D 3.5e Animal Companion progression bonuses by effective druid level (PHB p.36).
/// </summary>
public static class AnimalCompanionProgression
{
    // Columns: BonusHD, BonusNaturalArmor, StrDexBonus, BonusTricks, Special
    // Index = effective druid level - 1 (0-based), levels 1-20
    private static readonly int[,] Progression = new int[20, 4]
    {
        //  HD  NArmor  Str/Dex  Tricks
        {  0,    0,      0,      1 },  // EDL 1
        {  1,    0,      0,      1 },  // EDL 2
        {  2,    2,      1,      2 },  // EDL 3
        {  3,    2,      1,      2 },  // EDL 4
        {  4,    4,      2,      3 },  // EDL 5
        {  5,    4,      2,      3 },  // EDL 6
        {  6,    6,      3,      4 },  // EDL 7
        {  7,    6,      3,      4 },  // EDL 8
        {  8,    8,      4,      5 },  // EDL 9
        {  9,    8,      4,      5 },  // EDL 10
        { 10,   10,      5,      6 },  // EDL 11
        { 11,   10,      5,      6 },  // EDL 12
        { 12,   12,      6,      7 },  // EDL 13
        { 13,   12,      6,      7 },  // EDL 14
        { 14,   14,      7,      8 },  // EDL 15
        { 15,   14,      7,      8 },  // EDL 16
        { 16,   16,      8,      9 },  // EDL 17
        { 17,   16,      8,      9 },  // EDL 18
        { 18,   18,      9,     10 },  // EDL 19
        { 19,   18,      9,     10 }, // EDL 20
    };

    public static int GetBonusHD(int effectiveLevel) =>
        effectiveLevel >= 1 && effectiveLevel <= 20 ? Progression[effectiveLevel - 1, 0] : 0;

    public static int GetBonusNaturalArmor(int effectiveLevel) =>
        effectiveLevel >= 1 && effectiveLevel <= 20 ? Progression[effectiveLevel - 1, 1] : 0;

    public static int GetStrDexBonus(int effectiveLevel) =>
        effectiveLevel >= 1 && effectiveLevel <= 20 ? Progression[effectiveLevel - 1, 2] : 0;

    public static int GetBonusTricks(int effectiveLevel) =>
        effectiveLevel >= 1 && effectiveLevel <= 20 ? Progression[effectiveLevel - 1, 3] : 0;

    /// <summary>
    /// Special abilities by effective druid level (PHB p.36).
    /// Link at L1, Share Spells at L1, Evasion at L3, Devotion at L5,
    /// Multiattack at L9, Improved Evasion at L15.
    /// </summary>
    public static bool HasLink(int edl) => edl >= 1;
    public static bool HasShareSpells(int edl) => edl >= 1;
    public static bool HasEvasion(int edl) => edl >= 3;
    public static bool HasDevotion(int edl) => edl >= 5;
    public static bool HasMultiattack(int edl) => edl >= 9;
    public static bool HasImprovedEvasion(int edl) => edl >= 15;
}

/// <summary>
/// Manages an individual animal companion's current stats and abilities.
/// Pure data class — no MonoBehaviour dependency.
/// </summary>
public class AnimalCompanionData
{
    /// <summary>Name of this companion (e.g., "Wolf").</summary>
    public string CompanionName;

    /// <summary>The template this companion was created from.</summary>
    public AnimalCompanionTemplate Template;

    /// <summary>Effective druid level for scaling.</summary>
    public int EffectiveDruidLevel;

    /// <summary>Custom name given by the player.</summary>
    public string CustomName;

    /// <summary>Whether this companion is currently alive.</summary>
    public bool IsAlive = true;

    /// <summary>Current HP.</summary>
    public int CurrentHP;

    /// <summary>Maximum HP.</summary>
    public int MaxHP;

    // Calculated stats (updated by RecalculateStats)
    public int TotalHD;
    public int NaturalArmor;
    public int STR;
    public int DEX;
    public int CON;
    public int INT;
    public int WIS;
    public int CHA;
    public int AttackBonus;
    public int BonusTricks;

    // Special abilities
    public bool HasLink;
    public bool HasShareSpells;
    public bool HasEvasion;
    public bool HasDevotion;
    public bool HasMultiattack;
    public bool HasImprovedEvasion;

    /// <summary>
    /// Initialize a companion from a template at a given effective druid level.
    /// </summary>
    public void Initialize(AnimalCompanionTemplate template, int effectiveDruidLevel, string customName = null)
    {
        Template = template;
        CompanionName = template.Name;
        CustomName = customName ?? template.Name;
        EffectiveDruidLevel = Mathf.Max(1, effectiveDruidLevel);
        RecalculateStats();
    }

    /// <summary>
    /// Recalculate all companion stats based on effective druid level.
    /// </summary>
    public void RecalculateStats()
    {
        if (Template == null) return;

        int edl = EffectiveDruidLevel;

        TotalHD = Template.BaseHD + AnimalCompanionProgression.GetBonusHD(edl);
        NaturalArmor = Template.BaseNaturalArmor + AnimalCompanionProgression.GetBonusNaturalArmor(edl);

        int strDexBonus = AnimalCompanionProgression.GetStrDexBonus(edl);
        STR = Template.BaseSTR + strDexBonus;
        DEX = Template.BaseDEX + strDexBonus;
        CON = Template.BaseCON;
        INT = Template.BaseINT;
        WIS = Template.BaseWIS;
        CHA = Template.BaseCHA;

        BonusTricks = AnimalCompanionProgression.GetBonusTricks(edl);
        AttackBonus = Template.BaseAttackBonus + AnimalCompanionProgression.GetBonusHD(edl);

        HasLink = AnimalCompanionProgression.HasLink(edl);
        HasShareSpells = AnimalCompanionProgression.HasShareSpells(edl);
        HasEvasion = AnimalCompanionProgression.HasEvasion(edl);
        HasDevotion = AnimalCompanionProgression.HasDevotion(edl);
        HasMultiattack = AnimalCompanionProgression.HasMultiattack(edl);
        HasImprovedEvasion = AnimalCompanionProgression.HasImprovedEvasion(edl);

        // Calculate HP: d8 per HD, average = 4.5/HD, use CON mod
        int conMod = (CON - 10) / 2;
        MaxHP = Mathf.Max(1, TotalHD * 4 + TotalHD * conMod); // Average HP
        if (CurrentHP <= 0 && IsAlive) CurrentHP = MaxHP;

        Debug.Log($"[Companion] {CustomName} (EDL {edl}): HD={TotalHD}, HP={CurrentHP}/{MaxHP}, " +
                  $"STR={STR}, DEX={DEX}, NArmor={NaturalArmor}");
    }

    /// <summary>Get a display summary of the companion.</summary>
    public string GetSummary()
    {
        if (Template == null) return "No companion";
        return $"{CustomName} ({CompanionName}): HD {TotalHD}, HP {CurrentHP}/{MaxHP}, " +
               $"STR {STR}, DEX {DEX}, AC {10 + NaturalArmor + (DEX - 10) / 2}";
    }
}

/// <summary>
/// Database of available animal companion templates (PHB p.36, Table 3-8).
/// </summary>
public static class AnimalCompanionTemplates
{
    private static List<AnimalCompanionTemplate> _templates;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        _templates = new List<AnimalCompanionTemplate>
        {
            //                       Name            HD  NAr STR DEX CON INT WIS CHA Spd BAB AtkType   Dmg       Size
            new AnimalCompanionTemplate("Badger",     1,  1, 8,  17, 15, 2,  12, 6,  6,  0, "Claw",   "1d2-1", "Small"),
            new AnimalCompanionTemplate("Camel",      3,  1, 18, 16, 14, 2,  11, 4,  10, 2, "Bite",   "1d4+4", "Large"),
            new AnimalCompanionTemplate("Dire Rat",   1,  3, 10, 17, 12, 1,  12, 4,  8,  0, "Bite",   "1d4",   "Small"),
            new AnimalCompanionTemplate("Dog",        1,  1, 13, 17, 15, 2,  12, 6,  8,  0, "Bite",   "1d4+1", "Small"),
            new AnimalCompanionTemplate("Riding Dog", 2,  1, 15, 15, 15, 2,  12, 6,  8,  1, "Bite",   "1d6+3", "Medium"),
            new AnimalCompanionTemplate("Eagle",      1,  1, 10, 15, 12, 2,  14, 6,  16, 0, "Talons", "1d4",   "Small"),
            new AnimalCompanionTemplate("Hawk",       1,  1, 6,  17, 10, 2,  14, 6,  12, 0, "Talons", "1d4-2", "Tiny"),
            new AnimalCompanionTemplate("Horse, Light",3, 1, 14, 13, 15, 2,  12, 6,  12, 2, "Hoof",   "1d4+1", "Large"),
            new AnimalCompanionTemplate("Owl",        1,  1, 6,  17, 10, 2,  14, 6,  12, 0, "Talons", "1d4-2", "Tiny"),
            new AnimalCompanionTemplate("Pony",       2,  1, 13, 13, 12, 2,  11, 4,  8,  1, "Hoof",   "1d3+1", "Medium"),
            new AnimalCompanionTemplate("Snake, Small Viper", 1, 3, 6, 17, 11, 1, 12, 2, 4, 0, "Bite", "1d2-2", "Tiny"),
            new AnimalCompanionTemplate("Snake, Medium Viper", 2, 3, 8, 17, 11, 1, 12, 2, 4, 1, "Bite", "1d4-1", "Medium"),
            new AnimalCompanionTemplate("Wolf",       2,  2, 13, 15, 15, 2,  12, 6,  10, 1, "Bite",   "1d6+1", "Medium"),
        };

        Debug.Log($"[AnimalCompanionTemplates] Initialized {_templates.Count} companion templates.");
    }

    /// <summary>Get all available companion templates.</summary>
    public static List<AnimalCompanionTemplate> GetAll()
    {
        Init();
        return new List<AnimalCompanionTemplate>(_templates);
    }

    /// <summary>Find a companion template by name.</summary>
    public static AnimalCompanionTemplate GetByName(string name)
    {
        Init();
        for (int i = 0; i < _templates.Count; i++)
        {
            if (string.Equals(_templates[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return _templates[i];
        }
        return null;
    }
}

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// D&D 3.5 edition character stats system.
/// Holds the six core ability scores and derives all combat stats from them.
/// Now supports racial modifiers applied to base ability scores.
/// </summary>
[System.Serializable]
public class CharacterStats
{
    // ========== IDENTITY ==========
    public string CharacterName;
    public int Level;
    public string CharacterClass; // e.g., "Fighter", "Rogue", "Warrior"

    /// <summary>Whether this character is a Rogue (eligible for sneak attack).</summary>
    public bool IsRogue => CharacterClass == "Rogue";

    // ========== FEATS (D&D 3.5) ==========
    /// <summary>Set of feats this character has. Auto-granted based on class.</summary>
    public HashSet<string> Feats = new HashSet<string>();

    /// <summary>Check if this character has a specific feat.</summary>
    public bool HasFeat(string featName) => Feats.Contains(featName);

    /// <summary>
    /// Auto-grant feats based on character class.
    /// Fighter: Power Attack. Rogue: Rapid Shot, Point Blank Shot.
    /// </summary>
    public void InitFeats()
    {
        Feats.Clear();
        if (CharacterClass == "Fighter")
        {
            Feats.Add("Power Attack");
        }
        else if (CharacterClass == "Rogue")
        {
            Feats.Add("Point Blank Shot");
            Feats.Add("Rapid Shot");
        }
    }

    // ========== RACE ==========
    /// <summary>The character's race data (Dwarf, Elf, Human, etc.).</summary>
    public RaceData Race;

    /// <summary>Creature type tags for this character (e.g., "Goblinoid", "Orc"). Used for racial attack bonuses.</summary>
    public List<string> CreatureTags = new List<string>();

    // ========== BASE ABILITY SCORES (before racial modifiers) ==========
    /// <summary>Base ability scores BEFORE racial modifiers are applied.</summary>
    public int BaseSTR, BaseDEX, BaseCON, BaseWIS, BaseINT, BaseCHA;

    // ========== CORE ABILITY SCORES (D&D 3.5, with racial modifiers applied) ==========
    public int STR; // Strength
    public int DEX; // Dexterity
    public int CON; // Constitution
    public int WIS; // Wisdom
    public int INT; // Intelligence
    public int CHA; // Charisma

    // ========== EQUIPMENT / BONUSES ==========
    public int ArmorBonus;      // From worn armor (e.g., chain shirt = +4)
    public int ShieldBonus;     // From shield
    public int BaseAttackBonus; // BAB from class/level (fighter gets level, rogue gets 3/4)
    public int BaseDamageDice;  // Number of sides on weapon damage die (e.g., 8 for longsword d8)
    public int BaseDamageCount; // Number of damage dice (usually 1)
    public int BonusDamage;     // Extra flat damage (magic weapon, etc.)
    public int AttackRange;     // Square tiles for attack reach (1 = melee)
    public int BaseSpeed;       // Base movement speed in squares
    public int CritThreatMin;   // Minimum natural d20 roll for crit threat (from equipped weapon, default 20)
    public int CritMultiplier;  // Crit damage multiplier (from equipped weapon, default 2)

    // ========== ARMOR PROPERTIES (D&D 3.5) ==========
    public int MaxDexBonus;         // Max DEX bonus to AC from armor (-1 = no limit)
    public int ArmorCheckPenalty;   // Total armor check penalty (armor + shield, stored positive)
    public int ArcaneSpellFailure;  // Total arcane spell failure % (armor + shield)

    // ========== DERIVED STATS (calculated) ==========

    /// <summary>D&D 3.5 modifier: (score - 10) / 2, rounded down.</summary>
    public static int GetModifier(int abilityScore)
    {
        return Mathf.FloorToInt((abilityScore - 10) / 2f);
    }

    public int STRMod => GetModifier(STR);
    public int DEXMod => GetModifier(DEX);
    public int CONMod => GetModifier(CON);
    public int WISMod => GetModifier(WIS);
    public int INTMod => GetModifier(INT);
    public int CHAMod => GetModifier(CHA);

    /// <summary>Max HP = Base hit die HP + (CON modifier × level).</summary>
    public int MaxHP { get; private set; }

    /// <summary>Current HP, clamped between 0 and MaxHP.</summary>
    public int CurrentHP;

    /// <summary>
    /// Size modifier to AC and attack rolls from race (Small = +1, Medium = 0, etc.)
    /// </summary>
    public int SizeModifier => Race != null ? Race.SizeACAndAttackModifier : 0;

    /// <summary>
    /// AC = 10 + effective DEX modifier (capped by armor's Max Dex Bonus) + armor bonus + shield bonus + size modifier.
    /// D&D 3.5: MaxDexBonus of -1 means no limit; 0+ caps the DEX bonus to AC.
    /// Size bonus: Small +1, Medium 0, Large -1, etc.
    /// </summary>
    public int ArmorClass
    {
        get
        {
            int dexToAC = DEXMod;
            if (MaxDexBonus >= 0 && dexToAC > MaxDexBonus)
                dexToAC = MaxDexBonus;
            return 10 + dexToAC + ArmorBonus + ShieldBonus + SizeModifier;
        }
    }

    /// <summary>Total attack bonus = BAB + STR modifier (melee) + size modifier.</summary>
    public int AttackBonus => BaseAttackBonus + STRMod + SizeModifier;

    /// <summary>Movement speed in squares per turn.</summary>
    public int MoveRange => BaseSpeed;

    public bool IsDead => CurrentHP <= 0;

    // ========== CONSTRUCTOR ==========

    /// <summary>
    /// Create a character with full D&D 3.5 ability scores and racial modifiers.
    /// Ability scores passed in are BASE scores (before racial modifiers).
    /// Racial modifiers are applied automatically if a race is specified.
    /// </summary>
    /// <param name="name">Character name</param>
    /// <param name="level">Character level</param>
    /// <param name="characterClass">Character class (e.g., "Fighter", "Rogue")</param>
    /// <param name="str">Base Strength score (before racial modifier)</param>
    /// <param name="dex">Base Dexterity score (before racial modifier)</param>
    /// <param name="con">Base Constitution score (before racial modifier)</param>
    /// <param name="wis">Base Wisdom score (before racial modifier)</param>
    /// <param name="intelligence">Base Intelligence score (before racial modifier)</param>
    /// <param name="cha">Base Charisma score (before racial modifier)</param>
    /// <param name="bab">Base Attack Bonus</param>
    /// <param name="armorBonus">Armor bonus to AC</param>
    /// <param name="shieldBonus">Shield bonus to AC</param>
    /// <param name="damageDice">Sides on weapon damage die (e.g. 8 for d8)</param>
    /// <param name="damageCount">Number of weapon damage dice</param>
    /// <param name="bonusDamage">Flat bonus damage</param>
    /// <param name="baseSpeed">Movement range in squares (overridden by race if set)</param>
    /// <param name="atkRange">Attack range in squares</param>
    /// <param name="baseHitDieHP">Base HP from hit dice (before CON)</param>
    /// <param name="raceName">Race name (e.g., "Dwarf", "Elf", "Human"). Null for no race.</param>
    public CharacterStats(string name, int level, string characterClass,
        int str, int dex, int con, int wis, int intelligence, int cha,
        int bab, int armorBonus, int shieldBonus,
        int damageDice, int damageCount, int bonusDamage,
        int baseSpeed, int atkRange, int baseHitDieHP,
        string raceName = null)
    {
        CharacterName = name;
        Level = level;
        CharacterClass = characterClass;

        // Store base ability scores (before racial modifiers)
        BaseSTR = str;
        BaseDEX = dex;
        BaseCON = con;
        BaseWIS = wis;
        BaseINT = intelligence;
        BaseCHA = cha;

        // Apply racial modifiers if a race is specified
        RaceDatabase.Init();
        Race = raceName != null ? RaceDatabase.GetRace(raceName) : null;

        if (Race != null)
        {
            STR = str + Race.STRModifier;
            DEX = dex + Race.DEXModifier;
            CON = con + Race.CONModifier;
            WIS = wis + Race.WISModifier;
            INT = intelligence + Race.INTModifier;
            CHA = cha + Race.CHAModifier;

            // Use racial speed (in squares)
            BaseSpeed = Race.BaseSpeedSquares;
        }
        else
        {
            STR = str;
            DEX = dex;
            CON = con;
            WIS = wis;
            INT = intelligence;
            CHA = cha;
            BaseSpeed = baseSpeed;
        }

        BaseAttackBonus = bab;
        ArmorBonus = armorBonus;
        ShieldBonus = shieldBonus;
        BaseDamageDice = damageDice;
        BaseDamageCount = damageCount;
        BonusDamage = bonusDamage;
        AttackRange = atkRange;

        // Default crit stats (can be overridden by equipped weapons via Inventory.RecalculateStats)
        CritThreatMin = 20;   // Only natural 20 threatens by default
        CritMultiplier = 2;   // ×2 by default

        // Default armor properties (no armor = no limit on DEX to AC)
        MaxDexBonus = -1;       // -1 = no limit
        ArmorCheckPenalty = 0;
        ArcaneSpellFailure = 0;

        // Calculate MaxHP: base + CON mod × level (minimum 1 HP per level)
        // Uses final CON (with racial modifier applied)
        int conModPerLevel = Mathf.Max(1, GetModifier(CON));
        MaxHP = baseHitDieHP + (conModPerLevel * level);
        // Ensure at least baseHitDieHP if CON mod is negative
        if (MaxHP < 1) MaxHP = 1;
        CurrentHP = MaxHP;

        // Auto-grant feats based on class
        InitFeats();
    }

    // ========== COMBAT METHODS ==========

    /// <summary>
    /// Get the list of iterative attack bonuses for a Full Attack based on BAB.
    /// BAB +6 → [+6, +1], BAB +11 → [+11, +6, +1], etc.
    /// </summary>
    public int[] GetIterativeAttackBonuses()
    {
        var bonuses = new System.Collections.Generic.List<int>();
        int bab = BaseAttackBonus;
        while (bab > 0)
        {
            bonuses.Add(bab + STRMod + SizeModifier);
            bab -= 5;
        }
        // Always have at least one attack
        if (bonuses.Count == 0)
            bonuses.Add(BaseAttackBonus + STRMod + SizeModifier);
        return bonuses.ToArray();
    }

    /// <summary>
    /// Number of iterative attacks this character gets on a full attack.
    /// </summary>
    public int IterativeAttackCount => Mathf.Max(1, 1 + (BaseAttackBonus - 1) / 5);

    /// <summary>
    /// Roll a d20 + total attack bonus vs target AC.
    /// Natural 20 always hits, natural 1 always misses (D&D 3.5 rules).
    /// </summary>
    public (bool hit, int roll, int total) RollToHit(int targetAC)
    {
        int roll = Random.Range(1, 21); // 1-20
        int total = roll + AttackBonus;

        // Natural 20 always hits, natural 1 always misses
        bool hit;
        if (roll == 20) hit = true;
        else if (roll == 1) hit = false;
        else hit = total >= targetAC;

        return (hit, roll, total);
    }

    /// <summary>
    /// Roll a d20 + total attack bonus + flanking bonus vs target AC.
    /// </summary>
    public (bool hit, int roll, int total) RollToHitWithFlanking(int targetAC, int flankingBonus)
    {
        int roll = Random.Range(1, 21);
        int total = roll + AttackBonus + flankingBonus;

        bool hit;
        if (roll == 20) hit = true;
        else if (roll == 1) hit = false;
        else hit = total >= targetAC;

        return (hit, roll, total);
    }

    /// <summary>
    /// Roll d20 + a specific total attack modifier (for iterative attacks, TWF, etc.) vs target AC.
    /// </summary>
    /// <param name="totalAttackMod">The total attack modifier to add to the d20 roll</param>
    /// <param name="targetAC">Target's Armor Class</param>
    public (bool hit, int roll, int total) RollToHitWithMod(int totalAttackMod, int targetAC)
    {
        int roll = Random.Range(1, 21);
        int total = roll + totalAttackMod;

        bool hit;
        if (roll == 20) hit = true;
        else if (roll == 1) hit = false;
        else hit = total >= targetAC;

        return (hit, roll, total);
    }

    /// <summary>
    /// Roll weapon damage: (BaseDamageCount)d(BaseDamageDice) + STR modifier + BonusDamage.
    /// Minimum 1 damage on a hit (D&D 3.5 rule).
    /// </summary>
    public int RollDamage()
    {
        int total = 0;
        for (int i = 0; i < BaseDamageCount; i++)
        {
            total += Random.Range(1, BaseDamageDice + 1);
        }
        total += STRMod + BonusDamage;
        return Mathf.Max(1, total); // Minimum 1 damage on a hit
    }

    /// <summary>
    /// Roll damage for a specific weapon with a specific STR modifier fraction.
    /// Used for off-hand attacks (half STR) and two-handed (1.5x STR).
    /// </summary>
    /// <param name="damageDice">Sides of the damage die</param>
    /// <param name="damageCount">Number of dice</param>
    /// <param name="bonusDamage">Flat bonus damage from weapon</param>
    /// <param name="strMultiplier">STR mod multiplier (1.0 for main hand, 0.5 for off-hand)</param>
    public int RollDamageWithWeapon(int damageDice, int damageCount, int bonusDamage, float strMultiplier)
    {
        int total = 0;
        for (int i = 0; i < damageCount; i++)
        {
            total += Random.Range(1, damageDice + 1);
        }
        int strBonus = Mathf.FloorToInt(STRMod * strMultiplier);
        total += strBonus + bonusDamage;
        return Mathf.Max(1, total);
    }

    // ========== CRITICAL HIT METHODS (D&D 3.5) ==========

    /// <summary>
    /// Check if a natural d20 roll threatens a critical hit given the weapon's threat range.
    /// Natural 20 is always a threat regardless of weapon.
    /// </summary>
    /// <param name="naturalRoll">The natural d20 roll (1-20)</param>
    /// <param name="critThreatMin">Minimum roll to threaten (e.g. 19 for 19-20 range, 20 for 20 only)</param>
    public static bool IsCritThreat(int naturalRoll, int critThreatMin)
    {
        if (naturalRoll == 20) return true; // Natural 20 always threatens
        int threatMin = critThreatMin > 0 ? critThreatMin : 20;
        return naturalRoll >= threatMin;
    }

    /// <summary>
    /// Roll a confirmation roll for a critical hit threat.
    /// Uses the same attack modifier as the original attack, rolled against the same AC.
    /// </summary>
    /// <param name="totalAttackMod">Total attack modifier (same as original attack)</param>
    /// <param name="targetAC">Target's Armor Class</param>
    /// <returns>(confirmed, naturalRoll, total) - confirmed is true if the crit is confirmed</returns>
    public (bool confirmed, int roll, int total) RollCritConfirmation(int totalAttackMod, int targetAC)
    {
        int roll = Random.Range(1, 21);
        int total = roll + totalAttackMod;

        // Natural 20 on confirmation always confirms; natural 1 always fails confirmation
        bool confirmed;
        if (roll == 20) confirmed = true;
        else if (roll == 1) confirmed = false;
        else confirmed = total >= targetAC;

        return (confirmed, roll, total);
    }

    /// <summary>
    /// Roll critical hit damage: multiply only the weapon dice by the crit multiplier,
    /// then add static bonuses (STR, magic, etc.) once. D&D 3.5 rules.
    /// Example: Longsword 1d8+3 with ×2 crit = 2d8 + 3
    /// </summary>
    /// <param name="damageDice">Sides of the damage die</param>
    /// <param name="damageCount">Base number of dice</param>
    /// <param name="bonusDamage">Flat bonus damage from weapon</param>
    /// <param name="strMultiplier">STR mod multiplier (1.0 main hand, 0.5 off-hand)</param>
    /// <param name="critMultiplier">Crit damage multiplier (2, 3, or 4)</param>
    public int RollCritDamage(int damageDice, int damageCount, int bonusDamage, float strMultiplier, int critMultiplier)
    {
        int mult = critMultiplier > 0 ? critMultiplier : 2;
        // Roll weapon dice × multiplier
        int diceTotal = 0;
        int totalDice = damageCount * mult;
        for (int i = 0; i < totalDice; i++)
        {
            diceTotal += Random.Range(1, damageDice + 1);
        }
        // Add static bonuses once (NOT multiplied per D&D 3.5)
        int strBonus = Mathf.FloorToInt(STRMod * strMultiplier);
        int total = diceTotal + strBonus + bonusDamage;
        return Mathf.Max(1, total);
    }

    /// <summary>
    /// Apply damage, clamping HP to 0.
    /// </summary>
    public void TakeDamage(int amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
    }

    // ========== DISPLAY HELPERS ==========

    /// <summary>Format a modifier as "+X" or "-X".</summary>
    public static string FormatMod(int mod)
    {
        return mod >= 0 ? $"+{mod}" : $"{mod}";
    }

    /// <summary>Get a formatted string for an ability score, e.g. "STR 16 (+3)".</summary>
    public string GetAbilityString(string abilityName, int score)
    {
        return $"{abilityName} {score} ({FormatMod(GetModifier(score))})";
    }

    // ========== RACIAL HELPERS ==========

    /// <summary>
    /// Get formatted ability score string with racial modifier annotation.
    /// E.g., "CON 16 (+3) [+2 racial]" for a Dwarf with base CON 14.
    /// </summary>
    public string GetAbilityStringWithRacial(string abilityName, int finalScore, int racialMod)
    {
        string baseStr = $"{abilityName} {finalScore}({FormatMod(GetModifier(finalScore))})";
        if (racialMod != 0)
            baseStr += $"<size=10>[{FormatMod(racialMod)}]</size>";
        return baseStr;
    }

    /// <summary>Get the racial modifier for a specific ability, or 0 if no race.</summary>
    public int GetRacialModifier(string ability)
    {
        if (Race == null) return 0;
        switch (ability.ToUpper())
        {
            case "STR": return Race.STRModifier;
            case "DEX": return Race.DEXModifier;
            case "CON": return Race.CONModifier;
            case "WIS": return Race.WISModifier;
            case "INT": return Race.INTModifier;
            case "CHA": return Race.CHAModifier;
            default: return 0;
        }
    }

    /// <summary>
    /// Get racial attack bonus against a specific target's creature tags.
    /// </summary>
    public int GetRacialAttackBonus(CharacterStats target)
    {
        if (Race == null || target == null || target.CreatureTags == null) return 0;
        return Race.GetRacialAttackBonus(target.CreatureTags);
    }

    /// <summary>
    /// Check if this character has racial proficiency with a weapon (by item ID).
    /// </summary>
    public bool HasRacialWeaponProficiency(string weaponId)
    {
        if (Race == null) return false;
        return Race.HasRacialProficiency(weaponId);
    }

    /// <summary>
    /// Whether this character's race prevents armor from reducing speed.
    /// </summary>
    public bool SpeedNotReducedByArmor => Race != null && Race.SpeedNotReducedByArmor;

    /// <summary>Race name string for display (or "" if no race set).</summary>
    public string RaceName => Race != null ? Race.RaceName : "";

    /// <summary>Size category string for display (e.g., "Small", "Medium").</summary>
    public string SizeCategory => Race != null ? Race.SizeName : "Medium";

    /// <summary>Whether this character is Small size.</summary>
    public bool IsSmallSize => Race != null && Race.IsSmall;

    /// <summary>Size grapple modifier (for future grapple system).</summary>
    public int SizeGrappleModifier => Race != null ? Race.SizeGrappleModifier : 0;

    /// <summary>Size hide modifier (for future skill system).</summary>
    public int SizeHideModifier => Race != null ? Race.SizeHideModifier : 0;

    /// <summary>Speed in feet for display purposes.</summary>
    public int SpeedInFeet => Race != null ? Race.BaseSpeedFeet : BaseSpeed * 5;

    // ========== DAMAGE MODIFIER HELPERS (D&D 3.5) ==========

    /// <summary>
    /// Calculate the STR-based damage bonus for a weapon based on its DamageModifierType.
    /// Returns the integer bonus to add to damage (can be negative for low STR).
    /// </summary>
    /// <param name="weapon">The weapon being used (null = unarmed, uses full STR)</param>
    /// <param name="isOffHand">True if this is an off-hand attack (overrides to 0.5× STR)</param>
    public int GetWeaponDamageModifier(ItemData weapon, bool isOffHand = false)
    {
        if (isOffHand)
        {
            // Off-hand always uses 0.5× STR, regardless of weapon type
            return Mathf.FloorToInt(STRMod * 0.5f);
        }

        if (weapon == null)
        {
            // Unarmed: full STR
            return STRMod;
        }

        switch (weapon.DmgModType)
        {
            case DamageModifierType.None:
                return 0;

            case DamageModifierType.Strength:
                return STRMod;

            case DamageModifierType.StrengthOneAndHalf:
                return Mathf.FloorToInt(STRMod * 1.5f);

            case DamageModifierType.StrengthHalf:
                return Mathf.FloorToInt(STRMod * 0.5f);

            case DamageModifierType.Composite:
                // Add STR up to the composite rating (minimum 0 — negative STR still applies fully)
                if (STRMod <= 0) return STRMod; // Negative STR always applies
                return Mathf.Min(STRMod, weapon.CompositeRating);

            default:
                return STRMod;
        }
    }

    /// <summary>
    /// Get a descriptive string for the damage modifier type applied by a weapon.
    /// Used in combat log display.
    /// </summary>
    /// <param name="weapon">The weapon being used</param>
    /// <param name="isOffHand">True if off-hand attack</param>
    public string GetDamageModifierDescription(ItemData weapon, bool isOffHand = false)
    {
        if (isOffHand) return "0.5× STR";

        if (weapon == null) return "STR";

        switch (weapon.DmgModType)
        {
            case DamageModifierType.None:
                return "";
            case DamageModifierType.Strength:
                if (weapon.IsThrown) return "thrown, STR";
                return "STR";
            case DamageModifierType.StrengthOneAndHalf:
                return "1.5× STR";
            case DamageModifierType.StrengthHalf:
                return "0.5× STR";
            case DamageModifierType.Composite:
                return $"composite +{weapon.CompositeRating}";
            default:
                return "STR";
        }
    }

    /// <summary>
    /// Roll just the weapon damage dice (no modifiers). Used for combat log breakdown.
    /// </summary>
    public int RollBaseDamage(int damageDice, int damageCount)
    {
        int total = 0;
        for (int i = 0; i < damageCount; i++)
        {
            total += Random.Range(1, damageDice + 1);
        }
        return total;
    }

    /// <summary>
    /// Roll damage using the weapon's DamageModifierType system instead of a raw strMultiplier.
    /// </summary>
    public int RollDamageWithModType(int damageDice, int damageCount, int bonusDamage, int damageModifier)
    {
        int total = 0;
        for (int i = 0; i < damageCount; i++)
        {
            total += Random.Range(1, damageDice + 1);
        }
        total += damageModifier + bonusDamage;
        return Mathf.Max(1, total);
    }

    /// <summary>
    /// Roll critical damage using the weapon's DamageModifierType system.
    /// Multiplies weapon dice; adds static bonuses (STR + bonus) once.
    /// </summary>
    public int RollCritDamageWithModType(int damageDice, int damageCount, int bonusDamage, int damageModifier, int critMultiplier)
    {
        int mult = critMultiplier > 0 ? critMultiplier : 2;
        int diceTotal = 0;
        int totalDice = damageCount * mult;
        for (int i = 0; i < totalDice; i++)
        {
            diceTotal += Random.Range(1, damageDice + 1);
        }
        int total = diceTotal + damageModifier + bonusDamage;
        return Mathf.Max(1, total);
    }
}

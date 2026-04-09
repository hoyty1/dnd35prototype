using UnityEngine;

/// <summary>
/// D&D 3.5 edition character stats system.
/// Holds the six core ability scores and derives all combat stats from them.
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

    // ========== CORE ABILITY SCORES (D&D 3.5) ==========
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
    public int AttackRange;     // Hex tiles for attack reach (1 = melee)
    public int BaseSpeed;       // Base movement speed in hexes

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

    /// <summary>AC = 10 + DEX modifier + armor bonus + shield bonus.</summary>
    public int ArmorClass => 10 + DEXMod + ArmorBonus + ShieldBonus;

    /// <summary>Total attack bonus = BAB + STR modifier (melee).</summary>
    public int AttackBonus => BaseAttackBonus + STRMod;

    /// <summary>Movement speed in hexes per turn.</summary>
    public int MoveRange => BaseSpeed;

    public bool IsDead => CurrentHP <= 0;

    // ========== CONSTRUCTOR ==========

    /// <summary>
    /// Create a character with full D&D 3.5 ability scores.
    /// </summary>
    /// <param name="name">Character name</param>
    /// <param name="level">Character level</param>
    /// <param name="characterClass">Character class (e.g., "Fighter", "Rogue")</param>
    /// <param name="str">Strength score</param>
    /// <param name="dex">Dexterity score</param>
    /// <param name="con">Constitution score</param>
    /// <param name="wis">Wisdom score</param>
    /// <param name="intelligence">Intelligence score</param>
    /// <param name="cha">Charisma score</param>
    /// <param name="bab">Base Attack Bonus</param>
    /// <param name="armorBonus">Armor bonus to AC</param>
    /// <param name="shieldBonus">Shield bonus to AC</param>
    /// <param name="damageDice">Sides on weapon damage die (e.g. 8 for d8)</param>
    /// <param name="damageCount">Number of weapon damage dice</param>
    /// <param name="bonusDamage">Flat bonus damage</param>
    /// <param name="baseSpeed">Movement range in hexes</param>
    /// <param name="atkRange">Attack range in hexes</param>
    /// <param name="baseHitDieHP">Base HP from hit dice (before CON)</param>
    public CharacterStats(string name, int level, string characterClass,
        int str, int dex, int con, int wis, int intelligence, int cha,
        int bab, int armorBonus, int shieldBonus,
        int damageDice, int damageCount, int bonusDamage,
        int baseSpeed, int atkRange, int baseHitDieHP)
    {
        CharacterName = name;
        Level = level;
        CharacterClass = characterClass;
        STR = str;
        DEX = dex;
        CON = con;
        WIS = wis;
        INT = intelligence;
        CHA = cha;
        BaseAttackBonus = bab;
        ArmorBonus = armorBonus;
        ShieldBonus = shieldBonus;
        BaseDamageDice = damageDice;
        BaseDamageCount = damageCount;
        BonusDamage = bonusDamage;
        BaseSpeed = baseSpeed;
        AttackRange = atkRange;

        // Calculate MaxHP: base + CON mod × level (minimum 1 HP per level)
        int conModPerLevel = Mathf.Max(1, GetModifier(con));
        MaxHP = baseHitDieHP + (conModPerLevel * level);
        // Ensure at least baseHitDieHP if CON mod is negative
        if (MaxHP < 1) MaxHP = 1;
        CurrentHP = MaxHP;
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
            bonuses.Add(bab + STRMod);
            bab -= 5;
        }
        // Always have at least one attack
        if (bonuses.Count == 0)
            bonuses.Add(BaseAttackBonus + STRMod);
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
}

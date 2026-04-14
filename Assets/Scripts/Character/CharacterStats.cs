using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
    public Alignment CharacterAlignment = Alignment.None;

    /// <summary>Full alignment name for display (e.g., "Lawful Good").</summary>
    public string AlignmentName => AlignmentHelper.GetFullName(CharacterAlignment);

    /// <summary>Alignment abbreviation (e.g., "LG").</summary>
    public string AlignmentAbbr => AlignmentHelper.GetAbbreviation(CharacterAlignment);

    // ========== DEITY & DOMAINS ==========

    /// <summary>ID of the character's chosen deity (e.g., "pelor"). Empty if none.</summary>
    public string DeityId = "";

    /// <summary>Names of the cleric's chosen domains (e.g., ["Healing", "Good"]). Empty for non-clerics.</summary>
    public System.Collections.Generic.List<string> ChosenDomains = new System.Collections.Generic.List<string>();

    /// <summary>
    /// D&D 3.5e Spontaneous Casting type for clerics.
    /// Cure = can convert prepared spells to cure spells.
    /// Inflict = can convert prepared spells to inflict spells.
    /// None = not a cleric or not set.
    /// </summary>
    public SpontaneousCastingType SpontaneousCasting = SpontaneousCastingType.None;

    /// <summary>Get the DeityData for this character's deity, or null.</summary>
    public DeityData Deity
    {
        get
        {
            if (string.IsNullOrEmpty(DeityId)) return null;
            DeityDatabase.Init();
            return DeityDatabase.GetDeity(DeityId);
        }
    }

    /// <summary>Display name of the deity, or "None".</summary>
    public string DeityName
    {
        get
        {
            var d = Deity;
            return d != null ? d.Name : "None";
        }
    }

    /// <summary>Domains display string (e.g., "Healing, Good").</summary>
    public string DomainsDisplay => ChosenDomains.Count > 0 ? string.Join(", ", ChosenDomains) : "None";

    /// <summary>Whether this character is a Rogue (eligible for sneak attack).</summary>
    public bool IsRogue => CharacterClass == "Rogue";

    /// <summary>Whether this character is a Monk.</summary>
    public bool IsMonk => CharacterClass == "Monk";

    /// <summary>Whether this character is a Barbarian.</summary>
    public bool IsBarbarian => CharacterClass == "Barbarian";

    /// <summary>Whether this character is a Wizard.</summary>
    public bool IsWizard => CharacterClass == "Wizard";

    /// <summary>Whether this character is a Cleric.</summary>
    public bool IsCleric => CharacterClass == "Cleric";

    /// <summary>Whether this character is a spellcaster. Delegates to ClassRegistry.</summary>
    public bool IsSpellcaster
    {
        get
        {
            ClassRegistry.Init();
            ICharacterClass classDef = ClassRegistry.GetClass(CharacterClass);
            return classDef != null && classDef.IsSpellcaster;
        }
    }

    // ========== MONK CLASS FEATURES (D&D 3.5) ==========

    /// <summary>
    /// Monk AC Bonus: Add WIS modifier to AC when unarmored and unencumbered.
    /// D&D 3.5: Monk adds WIS mod (if positive) to AC when wearing no armor and carrying no shield.
    /// </summary>
    public int MonkACBonus
    {
        get
        {
            if (!IsMonk) return 0;
            // Only applies when unarmored (ArmorBonus == 0) and no shield (ShieldBonus == 0)
            if (ArmorBonus > 0 || ShieldBonus > 0) return 0;
            return Mathf.Max(0, WISMod);
        }
    }

    /// <summary>
    /// Monk unarmed damage die at current level.
    /// Level 1-3: 1d6, Level 4-7: 1d8, Level 8-11: 1d10, etc.
    /// For our level 3 prototype: always 1d6.
    /// </summary>
    public int MonkUnarmedDamageDie => IsMonk ? 6 : 0;

    /// <summary>
    /// Monk Fast Movement: +10 ft speed when unarmored.
    /// At level 3: +10 ft (2 squares). Requires no armor.
    /// </summary>
    public int MonkFastMovementBonus
    {
        get
        {
            if (!IsMonk || Level < 1) return 0;
            if (ArmorBonus > 0) return 0; // Must be unarmored
            return 2; // +10 ft = 2 squares
        }
    }

    /// <summary>
    /// Still Mind: +2 bonus on saving throws against enchantment spells and effects.
    /// Gained at Monk level 3.
    /// </summary>
    public int StillMindBonus => (IsMonk && Level >= 3) ? 2 : 0;

    /// <summary>
    /// Evasion: On a successful Reflex save for half damage, take no damage instead.
    /// Monk gains this at level 2. Rogue also gains at level 2.
    /// </summary>
    public bool HasEvasion => (IsMonk && Level >= 2) || (IsRogue && Level >= 2);

    /// <summary>
    /// Flurry of Blows attack bonuses at current level.
    /// Level 1: -2/-2, Level 2: -1/-1, Level 3: +0/+0, etc.
    /// Returns array of total attack modifiers (BAB + STR + flurry penalty + size).
    /// </summary>
    public int[] GetFlurryOfBlowsBonuses()
    {
        if (!IsMonk) return new int[0];
        // Flurry penalty by level: Lv1=-2, Lv2=-1, Lv3+=0
        int flurryPenalty = Level >= 3 ? 0 : (Level >= 2 ? -1 : -2);
        int bonus = BaseAttackBonus + STRMod + SizeModifier + flurryPenalty;
        return new int[] { bonus, bonus }; // Two attacks at same bonus
    }

    // ========== BARBARIAN CLASS FEATURES (D&D 3.5) ==========

    /// <summary>Whether the barbarian is currently raging.</summary>
    public bool IsRaging;

    /// <summary>Rounds of rage remaining in current rage.</summary>
    public int RageRoundsRemaining;

    /// <summary>Number of times rage has been used today.</summary>
    public int RagesUsedToday;

    /// <summary>Maximum rages per day. Level 1-3: 1/day.</summary>
    public int MaxRagesPerDay => IsBarbarian ? 1 : 0;

    /// <summary>Whether the barbarian is fatigued (after rage ends).</summary>
    public bool IsFatigued;

    // ========== SPECIAL COMBAT CONDITIONS ==========

    /// <summary>Active special maneuver conditions (prone, grappled, disarmed).</summary>
    public List<StatusEffect> ActiveConditions = new List<StatusEffect>();

    /// <summary>Quick flags for common conditions.</summary>
    public bool IsProne => ActiveConditions.Any(c => c.Type == CombatConditionType.Prone);
    public bool IsGrappled => ActiveConditions.Any(c => c.Type == CombatConditionType.Grappled);
    public bool IsDisarmed => ActiveConditions.Any(c => c.Type == CombatConditionType.Disarmed);
    public bool HasChargePenalty => ActiveConditions.Any(c => c.Type == CombatConditionType.ChargePenalty);

    /// <summary>Penalty to attack rolls from conditions.</summary>
    public int ConditionAttackPenalty => (IsProne ? -4 : 0) + (IsGrappled ? -2 : 0) + (IsDisarmed ? -4 : 0);

    /// <summary>Penalty to AC from conditions.</summary>
    public int ConditionACPenalty => (IsProne ? -4 : 0)
        + (ActiveConditions.Any(c => c.Type == CombatConditionType.Feinted) ? -2 : 0)
        + (HasChargePenalty ? -2 : 0);

    /// <summary>Movement override from conditions (grappled cannot move normally).</summary>
    public bool MovementBlockedByCondition => IsGrappled;

    /// <summary>
    /// Barbarian Fast Movement: +10 ft speed in medium or lighter armor.
    /// Always active (not lost when raging).
    /// </summary>
    public int BarbarianFastMovementBonus
    {
        get
        {
            if (!IsBarbarian) return 0;
            // +10 ft = 2 squares, only in medium or lighter armor (MaxDexBonus >= 2 or no armor)
            // Heavy armor has MaxDexBonus of 0 or 1
            if (MaxDexBonus >= 0 && MaxDexBonus < 2) return 0; // Heavy armor
            return 2;
        }
    }

    /// <summary>
    /// Uncanny Dodge: Cannot be caught flat-footed, retains DEX bonus to AC.
    /// Gained at Barbarian level 2.
    /// </summary>
    public bool HasUncannyDodge => IsBarbarian && Level >= 2;

    /// <summary>
    /// Trap Sense: Bonus on Reflex saves vs traps and dodge bonus to AC vs traps.
    /// +1 at level 3, +2 at level 6, etc.
    /// </summary>
    public int TrapSenseBonus => (IsBarbarian && Level >= 3) ? 1 + (Level - 3) / 3 : 0;

    /// <summary>
    /// Activate Barbarian Rage. Lasts 3 + CON modifier rounds.
    /// +4 STR, +4 CON, +2 Will saves, -2 AC. Fatigued after rage ends.
    /// </summary>
    public bool ActivateRage()
    {
        if (!IsBarbarian || IsRaging || IsFatigued || RagesUsedToday >= MaxRagesPerDay)
        {
            Debug.Log($"[Barbarian] {CharacterName}: Cannot rage - " +
                      $"IsBarbarian={IsBarbarian}, IsRaging={IsRaging}, IsFatigued={IsFatigued}, " +
                      $"RagesUsed={RagesUsedToday}/{MaxRagesPerDay}");
            return false;
        }

        IsRaging = true;
        RageRoundsRemaining = 3 + Mathf.Max(0, CONMod); // Use current CON mod before rage
        RagesUsedToday++;

        // Apply rage bonuses: +4 STR, +4 CON
        STR += 4;
        CON += 4;

        // Recalculate HP from CON increase (+2 CON mod × level = +2 HP per level at level 3 = +6 HP)
        int hpGain = Level * 1; // +2 CON mod means +1 HP/level extra (since CON mod goes up by 2)
        // Actually +4 CON = +2 to CON mod = +2 HP per level
        hpGain = Level * 2;
        MaxHP += hpGain;
        CurrentHP += hpGain;

        Debug.Log($"[Barbarian] {CharacterName}: RAGE ACTIVATED! STR {STR}, CON {CON}, " +
                  $"+{hpGain} HP (now {CurrentHP}/{MaxHP}), " +
                  $"Duration: {RageRoundsRemaining} rounds, -2 AC penalty");
        return true;
    }

    /// <summary>
    /// End Barbarian Rage. Remove bonuses, apply fatigue.
    /// Fatigue: -2 STR, -2 DEX, can't charge or run.
    /// </summary>
    public void DeactivateRage()
    {
        if (!IsRaging) return;

        IsRaging = false;

        // Remove rage bonuses: -4 STR, -4 CON
        STR -= 4;
        CON -= 4;

        // Remove HP from CON decrease
        int hpLoss = Level * 2;
        MaxHP -= hpLoss;
        if (CurrentHP > MaxHP) CurrentHP = MaxHP;
        if (CurrentHP < 0) CurrentHP = 0;

        // Apply fatigue: -2 STR, -2 DEX
        IsFatigued = true;
        STR -= 2;
        DEX -= 2;

        Debug.Log($"[Barbarian] {CharacterName}: Rage ended! Now FATIGUED. " +
                  $"STR {STR}, DEX {DEX}, CON {CON}, HP {CurrentHP}/{MaxHP}");
    }

    /// <summary>
    /// Tick rage duration at start of barbarian's turn. Deactivates if expired.
    /// </summary>

    // ========== CONDITION MANAGEMENT ==========

    /// <summary>
    /// Apply (or refresh) a combat condition.
    /// If same condition exists, longer duration wins.
    /// </summary>
    public void ApplyCondition(CombatConditionType type, int rounds, string sourceName)
    {
        var existing = ActiveConditions.FirstOrDefault(c => c.Type == type);
        if (existing == null)
        {
            ActiveConditions.Add(new StatusEffect(type, sourceName, rounds));
            return;
        }

        if (existing.RemainingRounds < 0) return; // existing indefinite stays
        if (rounds < 0 || rounds > existing.RemainingRounds)
            existing.RemainingRounds = rounds;
    }

    /// <summary>
    /// Remove a specific combat condition.
    /// </summary>
    public bool RemoveCondition(CombatConditionType type)
    {
        int idx = ActiveConditions.FindIndex(c => c.Type == type);
        if (idx < 0) return false;
        ActiveConditions.RemoveAt(idx);
        return true;
    }

    /// <summary>
    /// Tick all condition durations and return expired effects.
    /// </summary>
    public List<StatusEffect> TickConditions()
    {
        var expired = new List<StatusEffect>();
        for (int i = ActiveConditions.Count - 1; i >= 0; i--)
        {
            var cond = ActiveConditions[i];
            if (cond.Tick())
            {
                expired.Add(cond);
                ActiveConditions.RemoveAt(i);
            }
        }
        return expired;
    }

    /// <summary>
    /// Get compact condition display for UI.
    /// </summary>
    public string GetConditionSummary()
    {
        if (ActiveConditions.Count == 0) return "";

        var parts = new List<string>();
        foreach (var c in ActiveConditions)
        {
            string color = c.Type == CombatConditionType.Grappled ? "#FFAA44"
                : c.Type == CombatConditionType.Prone ? "#FF7777"
                : c.Type == CombatConditionType.Feinted ? "#66CCFF"
                : c.Type == CombatConditionType.ChargePenalty ? "#FF9966"
                : "#FFFF66";
            parts.Add($"<color={color}>{c.Type}</color>({c.GetDurationLabel()})");
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Tick rage duration each round; auto-ends rage at 0 rounds.
    /// </summary>
    public void TickRage()
    {
        if (!IsRaging) return;
        RageRoundsRemaining--;
        Debug.Log($"[Barbarian] {CharacterName}: Rage tick - {RageRoundsRemaining} rounds remaining");
        if (RageRoundsRemaining <= 0)
        {
            Debug.Log($"[Barbarian] {CharacterName}: Rage expired!");
            DeactivateRage();
        }
    }

    /// <summary>Rage AC penalty (-2 while raging).</summary>
    public int RageACPenalty => IsRaging ? -2 : 0;

    /// <summary>Rage Will save bonus (+2 while raging).</summary>
    public int RageWillBonus => IsRaging ? 2 : 0;

    // ========== CLASS-BASED SAVE BONUSES (D&D 3.5) ==========

    /// <summary>
    /// Class-based Fortitude save bonus (good save progression).
    /// Good: +2 + level/2. Poor: level/3.
    /// Delegates to ClassRegistry for which saves are good/poor per class.
    /// At level 3: Good=+3, Poor=+1.
    /// </summary>
    public int ClassFortSave
    {
        get
        {
            ClassRegistry.Init();
            ICharacterClass classDef = ClassRegistry.GetClass(CharacterClass);
            bool goodFort = classDef != null && classDef.GoodFortitude;
            return goodFort ? (2 + Level / 2) : (Level / 3);
        }
    }

    /// <summary>
    /// Class-based Reflex save bonus.
    /// Delegates to ClassRegistry for which saves are good/poor per class.
    /// </summary>
    public int ClassRefSave
    {
        get
        {
            ClassRegistry.Init();
            ICharacterClass classDef = ClassRegistry.GetClass(CharacterClass);
            bool goodRef = classDef != null && classDef.GoodReflex;
            return goodRef ? (2 + Level / 2) : (Level / 3);
        }
    }

    /// <summary>
    /// Class-based Will save bonus.
    /// Delegates to ClassRegistry for which saves are good/poor per class.
    /// </summary>
    public int ClassWillSave
    {
        get
        {
            ClassRegistry.Init();
            ICharacterClass classDef = ClassRegistry.GetClass(CharacterClass);
            bool goodWill = classDef != null && classDef.GoodWill;
            return goodWill ? (2 + Level / 2) : (Level / 3);
        }
    }

    /// <summary>Total Fortitude save: CON mod + class base + feat bonus + morale bonus.</summary>
    public int FortitudeSave => CONMod + ClassFortSave + FeatFortitudeBonus + MoraleSaveBonus;

    /// <summary>Total Reflex save: DEX mod + class base + feat bonus + morale bonus.</summary>
    public int ReflexSave => DEXMod + ClassRefSave + FeatReflexBonus + MoraleSaveBonus;

    /// <summary>Total Will save: WIS mod + class base + feat bonus + rage bonus + morale bonus.</summary>
    public int WillSave => WISMod + ClassWillSave + FeatWillBonus + RageWillBonus + MoraleSaveBonus;

    // ========== FEATS (D&D 3.5) ==========
    /// <summary>Set of feats this character has.</summary>
    public HashSet<string> Feats = new HashSet<string>();

    /// <summary>Check if this character has a specific feat.</summary>
    public bool HasFeat(string featName) => Feats.Contains(featName);

    /// <summary>Chosen weapon for Weapon Focus/Specialization/Improved Critical.</summary>
    public string WeaponFocusChoice;

    /// <summary>Chosen skill for Skill Focus.</summary>
    public string SkillFocusChoice;

    /// <summary>Combat Expertise value (0 to 5): trade attack for AC.</summary>
    public int CombatExpertiseValue;

    // ========== ATTACKS OF OPPORTUNITY (D&D 3.5) ==========

    /// <summary>Number of AoOs used this round.</summary>
    public int AttacksOfOpportunityUsed;

    /// <summary>Maximum AoOs per round (1 default, 1+DEX mod with Combat Reflexes).</summary>
    public int MaxAttacksOfOpportunity = 1;

    /// <summary>
    /// Reset AoO counters at the start of this character's turn.
    /// Recalculates MaxAttacksOfOpportunity based on Combat Reflexes feat.
    /// </summary>
    public void ResetAttacksOfOpportunity()
    {
        AttacksOfOpportunityUsed = 0;
        MaxAttacksOfOpportunity = FeatManager.GetMaxAoOPerRound(this);
        Debug.Log($"[CharacterStats] {CharacterName} AoO reset: {MaxAttacksOfOpportunity} max" +
                  (HasFeat("Combat Reflexes") ? $" (Combat Reflexes: 1 + {Mathf.Max(0, DEXMod)} DEX)" : ""));
    }

    /// <summary>Whether this character has remaining AoOs this round.</summary>
    public bool HasRemainingAoO => AttacksOfOpportunityUsed < MaxAttacksOfOpportunity;

    /// <summary>
    /// Initialize feats from a list of feat names (from character creation).
    /// Does NOT clear existing feats - adds to them.
    /// </summary>
    public void AddFeats(List<string> featNames)
    {
        foreach (string feat in featNames)
        {
            Feats.Add(feat);
            Debug.Log($"[Feats] {CharacterName} gained feat: {feat}");
        }
    }

    /// <summary>
    /// Auto-grant ONLY legitimate automatic feats based on character class.
    /// Delegates to the class definition from ClassRegistry.
    /// 
    /// Per D&D 3.5e PHB p.40, only Monk gets an automatic feat (Improved Unarmed Strike
    /// at 1st level). All other classes have NO automatic feats.
    /// Note: Stunning Fist is NOT automatic — it's a bonus feat choice for Monks.
    /// Bonus feat selections (Fighter, Wizard, Monk choices) are handled separately
    /// by the character creation UI / level-up system.
    /// </summary>
    public void InitFeats()
    {
        Feats.Clear();
        ClassRegistry.Init();
        ICharacterClass classDef = ClassRegistry.GetClass(CharacterClass);
        if (classDef != null)
        {
            classDef.InitFeats(this);
        }
    }

    // ========== FEAT-DERIVED STATS ==========

    /// <summary>Total initiative modifier: DEX mod + feat bonuses (Improved Initiative).</summary>
    public int InitiativeModifier => DEXMod + FeatManager.GetInitiativeBonus(this);

    /// <summary>Fortitude save bonus from feats (Great Fortitude).</summary>
    public int FeatFortitudeBonus => FeatManager.GetFortitudeSaveBonus(this);

    /// <summary>Reflex save bonus from feats (Lightning Reflexes).</summary>
    public int FeatReflexBonus => FeatManager.GetReflexSaveBonus(this);

    /// <summary>Will save bonus from feats (Iron Will).</summary>
    public int FeatWillBonus => FeatManager.GetWillSaveBonus(this);

    /// <summary>HP bonus from feats (Toughness).</summary>
    public int FeatHPBonus => FeatManager.GetTotalHPBonus(this);

    /// <summary>Total Max HP including feat bonuses and spell bonuses.</summary>
    public int TotalMaxHP => MaxHP + FeatHPBonus + BonusMaxHP;

    /// <summary>AC bonus from Dodge feat.</summary>
    public int FeatACBonus => FeatManager.GetACBonus(this);

    /// <summary>Attack bonus from Weapon Focus/Greater Weapon Focus.</summary>
    public int WeaponFocusAttackBonus => FeatManager.GetWeaponFocusBonus(this, WeaponFocusChoice ?? "");

    /// <summary>Damage bonus from Weapon Specialization/Greater Weapon Specialization.</summary>
    public int WeaponSpecDamageBonus => FeatManager.GetWeaponSpecializationBonus(this, WeaponFocusChoice ?? "");

    /// <summary>Get feat bonus for a specific skill.</summary>
    public int GetFeatSkillBonus(string skillName) => FeatManager.GetSkillFeatBonus(this, skillName);

    // ========== RACE ==========
    /// <summary>The character's race data (Dwarf, Elf, Human, etc.).</summary>
    public RaceData Race;

    /// <summary>Creature type tags for this character (e.g., "Goblinoid", "Orc"). Used for racial attack bonuses.</summary>
    public List<string> CreatureTags = new List<string>();


    // ========== DAMAGE MITIGATION ==========
    /// <summary>Damage Reduction entries (e.g., DR 5/bludgeoning, DR 10/magic).</summary>
    public List<DamageReductionEntry> DamageReductions = new List<DamageReductionEntry>();

    /// <summary>Typed damage resistances (e.g., Resist Fire 10).</summary>
    public List<DamageResistanceEntry> DamageResistances = new List<DamageResistanceEntry>();

    /// <summary>Typed immunities (e.g., Immune Cold).</summary>
    public List<DamageType> DamageImmunities = new List<DamageType>();
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
    /// <summary>
    /// Bonus to AC from active spells (e.g., Mage Armor grants +4 armor bonus).
    /// This acts as an armor bonus and does NOT stack with regular ArmorBonus — 
    /// only the higher value applies per D&D 3.5 rules.
    /// </summary>
    public int SpellACBonus;

    /// <summary>Deflection bonus to AC from spells (e.g., Shield of Faith).</summary>
    public int DeflectionBonus;

    /// <summary>Morale bonus to attack rolls from spells (e.g., Bless).</summary>
    public int MoraleAttackBonus;

    /// <summary>Morale bonus to damage rolls from spells (e.g., Divine Favor).</summary>
    public int MoraleDamageBonus;

    /// <summary>Morale bonus to saving throws from spells (e.g., Bless).</summary>
    public int MoraleSaveBonus;

    /// <summary>Temporary hit points from spells (e.g., False Life).</summary>
    public int TempHP;

    /// <summary>Bonus max HP from spell effects (e.g., CON buff retroactive HP).</summary>
    public int BonusMaxHP;

    public int ArmorClass
    {
        get
        {
            int dexToAC = DEXMod;
            if (MaxDexBonus >= 0 && dexToAC > MaxDexBonus)
                dexToAC = MaxDexBonus;
            // Mage Armor is an armor bonus — it doesn't stack with worn armor.
            // Use the higher of ArmorBonus (from equipment) or SpellACBonus (from spells).
            int effectiveArmorBonus = Mathf.Max(ArmorBonus, SpellACBonus);
            return 10 + dexToAC + effectiveArmorBonus + ShieldBonus + SizeModifier
                   + MonkACBonus + FeatACBonus + RageACPenalty + DeflectionBonus + ConditionACPenalty;
        }
    }

    /// <summary>Total attack bonus = BAB + STR modifier (melee) + size modifier + morale bonus + condition penalties.</summary>
    public int AttackBonus => BaseAttackBonus + STRMod + SizeModifier + MoraleAttackBonus + ConditionAttackPenalty;

    /// <summary>Movement speed in squares per turn (includes class fast movement bonuses).</summary>
    public int MoveRange => MovementBlockedByCondition ? 0 : BaseSpeed + MonkFastMovementBonus + BarbarianFastMovementBonus;

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
        total += STRMod + BonusDamage + MoraleDamageBonus;
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
    /// Apply incoming damage through the full mitigation pipeline:
    /// immunity -> typed resistance -> DR (weapon damage only) -> HP/Temp HP.
    /// </summary>
    public DamageResolutionResult ApplyIncomingDamage(int amount, DamagePacket packet)
    {
        var result = new DamageResolutionResult
        {
            RawDamage = Mathf.Max(0, amount),
            DamageAfterImmunity = Mathf.Max(0, amount),
            DamageAfterResistance = Mathf.Max(0, amount),
            FinalDamage = Mathf.Max(0, amount)
        };

        if (result.RawDamage <= 0)
            return result;

        if (packet == null)
            packet = new DamagePacket
            {
                RawDamage = result.RawDamage,
                Source = AttackSource.Other,
                IsRanged = false,
                SourceName = "damage"
            };

        if (packet.Types == null || packet.Types.Count == 0)
            packet.Types = new HashSet<DamageType> { DamageType.Untyped };

        // 1) Immunity check: any matching type negates all damage
        foreach (var type in packet.Types)
        {
            if (DamageImmunities.Contains(type))
            {
                result.ImmunityTriggered = true;
                result.ImmunityType = type;
                result.DamageAfterImmunity = 0;
                result.DamageAfterResistance = 0;
                result.FinalDamage = 0;
                TakeDamage(0);
                return result;
            }
        }

        // 2) Resistance check: use highest resistance among matching damage types
        int resistance = 0;
        foreach (var type in packet.Types)
        {
            for (int i = 0; i < DamageResistances.Count; i++)
            {
                var entry = DamageResistances[i];
                if (entry != null && entry.Type == type)
                    resistance = Mathf.Max(resistance, entry.Amount);
            }
        }

        result.ResistanceApplied = Mathf.Min(result.RawDamage, Mathf.Max(0, resistance));
        result.DamageAfterResistance = Mathf.Max(0, result.RawDamage - result.ResistanceApplied);

        // 3) DR check: applies only to physical weapon-like attacks (weapon or natural)
        int drApplied = 0;
        bool sourceCountsAsWeapon = packet.Source == AttackSource.Weapon || packet.Source == AttackSource.Natural;
        bool hasRangedOnlyDr = false;
        bool bestDrWasRangedOnly = false;

        if (result.DamageAfterResistance > 0)
        {
            int bestApplicableDr = 0;
            for (int i = 0; i < DamageReductions.Count; i++)
            {
                var dr = DamageReductions[i];
                if (dr == null || dr.Amount <= 0) continue;

                if (dr.AppliesToRangedOnly)
                {
                    hasRangedOnlyDr = true;
                    if (!(packet.IsRanged && sourceCountsAsWeapon))
                        continue;
                }
                else if (!sourceCountsAsWeapon)
                {
                    continue;
                }

                bool bypassed = (dr.BypassAnyTag != DamageBypassTag.None) && ((packet.AttackTags & dr.BypassAnyTag) != 0);
                if (!bypassed && dr.Amount > bestApplicableDr)
                {
                    bestApplicableDr = dr.Amount;
                    bestDrWasRangedOnly = dr.AppliesToRangedOnly;
                }
            }

            drApplied = Mathf.Min(result.DamageAfterResistance, bestApplicableDr);
        }

        if (hasRangedOnlyDr && packet.IsRanged && packet.Source == AttackSource.Spell)
        {
            string sourceName = string.IsNullOrWhiteSpace(packet.SourceName) ? "Spell attack" : packet.SourceName;
            result.Notes.Add($"{sourceName} bypasses Protection from Arrows (spell attack)");
        }
        else if (drApplied > 0 && bestDrWasRangedOnly)
        {
            string sourceName = string.IsNullOrWhiteSpace(packet.SourceName) ? "ranged attack" : packet.SourceName;
            result.Notes.Add($"Protection from Arrows blocked {drApplied} damage from {sourceName}!");
        }

        result.DamageReductionApplied = drApplied;
        result.FinalDamage = Mathf.Max(0, result.DamageAfterResistance - drApplied);

        TakeDamage(result.FinalDamage);
        return result;
    }

    /// <summary>
    /// Apply raw HP damage (already mitigated), clamping HP to 0.
    /// </summary>
    public void TakeDamage(int amount)
    {
        // Temp HP absorbs damage first
        if (TempHP > 0 && amount > 0)
        {
            if (amount <= TempHP)
            {
                TempHP -= amount;
                return;
            }
            else
            {
                amount -= TempHP;
                TempHP = 0;
            }
        }
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
    }

    public void AddDamageResistance(DamageType type, int amount)
    {
        if (amount <= 0) return;
        DamageResistances.Add(new DamageResistanceEntry { Type = type, Amount = amount });
    }

    public void RemoveDamageResistance(DamageType type, int amount)
    {
        for (int i = 0; i < DamageResistances.Count; i++)
        {
            var entry = DamageResistances[i];
            if (entry != null && entry.Type == type && entry.Amount == amount)
            {
                DamageResistances.RemoveAt(i);
                return;
            }
        }
    }

    public void AddDamageImmunity(DamageType type)
    {
        if (type == DamageType.Untyped) return;
        if (!DamageImmunities.Contains(type))
            DamageImmunities.Add(type);
    }

    public void RemoveDamageImmunity(DamageType type)
    {
        DamageImmunities.Remove(type);
    }

    public void AddDamageReduction(int amount, DamageBypassTag bypassTags, bool rangedOnly = false)
    {
        if (amount <= 0) return;
        DamageReductions.Add(new DamageReductionEntry
        {
            Amount = amount,
            BypassAnyTag = bypassTags,
            AppliesToRangedOnly = rangedOnly
        });
    }

    public void RemoveDamageReduction(int amount, DamageBypassTag bypassTags, bool rangedOnly = false)
    {
        for (int i = 0; i < DamageReductions.Count; i++)
        {
            var dr = DamageReductions[i];
            if (dr == null) continue;
            if (dr.Amount == amount && dr.BypassAnyTag == bypassTags && dr.AppliesToRangedOnly == rangedOnly)
            {
                DamageReductions.RemoveAt(i);
                return;
            }
        }
    }

    public string GetMitigationSummaryString()
    {
        var parts = new List<string>();

        if (DamageReductions.Count > 0)
        {
            for (int i = 0; i < DamageReductions.Count; i++)
            {
                var dr = DamageReductions[i];
                if (dr != null && dr.Amount > 0)
                    parts.Add(dr.GetDisplayString());
            }
        }

        if (DamageResistances.Count > 0)
        {
            for (int i = 0; i < DamageResistances.Count; i++)
            {
                var r = DamageResistances[i];
                if (r != null && r.Amount > 0)
                    parts.Add(r.GetDisplayString());
            }
        }

        if (DamageImmunities.Count > 0)
        {
            for (int i = 0; i < DamageImmunities.Count; i++)
                parts.Add($"Immune {DamageTextUtils.GetDamageTypeDisplay(DamageImmunities[i])}");
        }

        return string.Join(", ", parts);
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

    /// <summary>Speed in feet for display purposes (includes fast movement bonuses).</summary>
    public int SpeedInFeet => (Race != null ? Race.BaseSpeedFeet : BaseSpeed * 5) + (MonkFastMovementBonus + BarbarianFastMovementBonus) * 5;

    // ========== SKILLS SYSTEM (D&D 3.5) ==========

    /// <summary>Dictionary of all skills this character has, keyed by skill name.</summary>
    public Dictionary<string, Skill> Skills = new Dictionary<string, Skill>();

    /// <summary>Number of unspent skill points available for allocation.</summary>
    public int AvailableSkillPoints;

    /// <summary>Total skill points this character has been granted.</summary>
    public int TotalSkillPoints;

    /// <summary>
    /// Initialize all skills for this character based on class and level.
    /// Creates all skill entries and calculates available skill points.
    /// </summary>
    /// <param name="characterClass">Character's class name (e.g., "Fighter", "Rogue")</param>
    /// <param name="level">Character level</param>
    public void InitializeSkills(string characterClass, int level)
    {
        Skills.Clear();
        HashSet<string> classSkills = ClassSkillDefinitions.GetClassSkills(characterClass);

        foreach (var skillDef in ClassSkillDefinitions.AllSkills)
        {
            bool isClass = classSkills.Contains(skillDef.name);
            Skill skill = new Skill(skillDef.name, skillDef.ability, isClass, skillDef.trainedOnly);
            Skills[skillDef.name] = skill;
        }

        TotalSkillPoints = ClassSkillDefinitions.CalculateSkillPoints(characterClass, level, INTMod);
        AvailableSkillPoints = TotalSkillPoints;

        Debug.Log($"[Skills] {CharacterName} ({characterClass} Lv{level}): {TotalSkillPoints} skill points " +
                  $"({ClassSkillDefinitions.GetBaseSkillPointsPerLevel(characterClass)} + {INTMod} INT mod" +
                  (level == 1 ? " × 4 at level 1)" : $" × {level} levels)"));
    }

    /// <summary>
    /// Add one rank to a skill.
    /// D&D 3.5 costs: Class skills = 1 skill point per rank, Cross-class = 2 skill points per rank.
    /// Returns false if: not enough points, max ranks reached, or skill not found.
    /// </summary>
    public bool AddSkillRank(string skillName)
    {
        if (!Skills.ContainsKey(skillName))
        {
            Debug.LogWarning($"[Skills] Skill '{skillName}' not found.");
            return false;
        }

        Skill skill = Skills[skillName];
        int cost = skill.SkillPointCost;

        if (AvailableSkillPoints < cost)
        {
            Debug.Log($"[Skills] Not enough skill points to add rank to {skillName} (need {cost}, have {AvailableSkillPoints}).");
            return false;
        }

        int maxRanks = skill.GetMaxRanks(Level);
        if (skill.Ranks >= maxRanks)
        {
            Debug.Log($"[Skills] {skillName} already at max ranks ({maxRanks}) for level {Level}.");
            return false;
        }

        skill.Ranks++;
        AvailableSkillPoints -= cost;
        string costLabel = skill.IsClassSkill ? "class" : "cross-class";
        Debug.Log($"[Skills] Added rank to {skillName} ({costLabel}, cost {cost}): now {skill.Ranks}/{maxRanks} ({AvailableSkillPoints} points remaining)");
        return true;
    }

    /// <summary>
    /// Remove one rank from a skill. Refunds the correct amount of skill points.
    /// D&D 3.5: Class skills refund 1 point, Cross-class skills refund 2 points.
    /// Returns false if skill has 0 ranks or skill not found.
    /// </summary>
    public bool RemoveSkillRank(string skillName)
    {
        if (!Skills.ContainsKey(skillName))
        {
            Debug.LogWarning($"[Skills] Skill '{skillName}' not found.");
            return false;
        }

        Skill skill = Skills[skillName];
        if (skill.Ranks <= 0)
        {
            Debug.Log($"[Skills] {skillName} already has 0 ranks.");
            return false;
        }

        int refund = skill.SkillPointCost;
        skill.Ranks--;
        AvailableSkillPoints += refund;
        Debug.Log($"[Skills] Removed rank from {skillName} (refunded {refund}): now {skill.Ranks} ({AvailableSkillPoints} points remaining)");
        return true;
    }

    /// <summary>
    /// Get the total bonus for a skill (ranks + ability mod + class skill bonus + feat bonuses).
    /// Returns 0 if skill not found.
    /// </summary>
    public int GetSkillBonus(string skillName)
    {
        if (!Skills.ContainsKey(skillName)) return 0;
        Skill skill = Skills[skillName];
        int baseBonus = skill.GetTotalBonus(GetAbilityModForSkill(skill));
        int featBonus = GetFeatSkillBonus(skillName);
        return baseBonus + featBonus;
    }

    /// <summary>
    /// Roll a skill check for a specific skill.
    /// Returns d20 + total bonus, or -1 if trained only and untrained.
    /// Logs full breakdown to console.
    /// </summary>
    public int RollSkillCheck(string skillName)
    {
        if (!Skills.ContainsKey(skillName))
        {
            Debug.LogWarning($"[Skills] Skill '{skillName}' not found for {CharacterName}.");
            return -1;
        }

        Skill skill = Skills[skillName];
        int abilityMod = GetAbilityModForSkill(skill);

        if (skill.TrainedOnly && skill.Ranks == 0)
        {
            Debug.Log($"[Skills] {CharacterName} cannot use {skillName} - requires training (0 ranks)");
            return -1;
        }

        int d20 = Random.Range(1, 21);
        int totalBonus = skill.GetTotalBonus(abilityMod);
        int featBonus = GetFeatSkillBonus(skillName);
        int total = d20 + totalBonus + featBonus;

        string classStr = skill.ClassSkillBonus > 0 ? $" + {skill.ClassSkillBonus}(class)" : "";
        string featStr = featBonus > 0 ? $" + {featBonus}(feat)" : "";
        Debug.Log($"[Skills] {CharacterName} rolls {skillName}: d20({d20}) + {skill.Ranks}(ranks) + {abilityMod}({skill.KeyAbility}){classStr}{featStr} = {total}");

        return total;
    }

    /// <summary>
    /// Get the ability modifier that applies to a skill based on its key ability.
    /// </summary>
    public int GetAbilityModForSkill(Skill skill)
    {
        switch (skill.KeyAbility)
        {
            case AbilityType.STR: return STRMod;
            case AbilityType.DEX: return DEXMod;
            case AbilityType.CON: return CONMod;
            case AbilityType.WIS: return WISMod;
            case AbilityType.INT: return INTMod;
            case AbilityType.CHA: return CHAMod;
            default: return 0;
        }
    }

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
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls a character on the square grid (both PC and NPC).
/// Supports D&D 3.5 action economy, full attacks, dual wielding, and critical hits.
/// Enhanced with detailed combat log breakdown fields.
/// </summary>
public class CharacterController : MonoBehaviour
{
    [Header("Character Setup")]
    public bool IsPlayerControlled;

    [Header("Sprites")]
    public Sprite AliveSprite;
    public Sprite DeadSprite;

    [HideInInspector] public CharacterStats Stats;
    [HideInInspector] public Vector2Int GridPosition;
    [HideInInspector] public bool HasMovedThisTurn;
    [HideInInspector] public bool HasAttackedThisTurn;

    /// <summary>Action economy tracker for the current turn.</summary>
    public ActionEconomy Actions = new ActionEconomy();

    // ========== FEAT PROPERTIES ==========

    /// <summary>
    /// Power Attack value: subtract from melee attack rolls, add to melee damage.
    /// Valid range: 0 to BAB. Two-handed weapons get 2× damage bonus.
    /// </summary>
    public int PowerAttackValue { get; private set; }

    /// <summary>
    /// Whether Rapid Shot is enabled. When active during a full attack with a ranged weapon,
    /// grants one extra attack at highest BAB but all attacks take -2 penalty.
    /// </summary>
    public bool RapidShotEnabled { get; private set; }

    /// <summary>Set Power Attack value, clamped to 0..BAB.</summary>
    public void SetPowerAttack(int value)
    {
        if (Stats == null) { PowerAttackValue = 0; return; }
        PowerAttackValue = Mathf.Clamp(value, 0, Mathf.Max(1, Stats.BaseAttackBonus));
    }

    /// <summary>Toggle Rapid Shot on/off.</summary>
    public void SetRapidShot(bool enabled)
    {
        RapidShotEnabled = enabled;
        Debug.Log($"[RapidShot] {(Stats != null ? Stats.CharacterName : "unknown")}: SetRapidShot({enabled}) → RapidShotEnabled = {RapidShotEnabled}");
    }

    /// <summary>Check if the given weapon is two-handed.</summary>
    public static bool IsWeaponTwoHanded(ItemData weapon)
    {
        if (weapon == null) return false;
        if (weapon.IsTwoHanded) return true;
        // Also check by name for common two-handed weapons
        string name = weapon.Name.ToLower();
        return name.Contains("greatsword") || name.Contains("greataxe") || name.Contains("greatclub")
            || name.Contains("longbow") || name.Contains("heavy crossbow") || name.Contains("quarterstaff")
            || name.Contains("longspear") || name.Contains("glaive") || name.Contains("halberd")
            || name.Contains("ranseur") || name.Contains("scythe") || name.Contains("falchion");
    }

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
            _sr = gameObject.AddComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Initialize the character with stats and place on grid.
    /// </summary>
    public void Init(CharacterStats stats, Vector2Int startPos, Sprite alive, Sprite dead)
    {
        Stats = stats;
        AliveSprite = alive;
        DeadSprite = dead;
        GridPosition = startPos;

        _sr.sprite = AliveSprite;
        _sr.sortingOrder = 10;

        // Position in world
        transform.position = SquareGridUtils.GridToWorld(startPos);

        // Register on grid
        SquareCell cell = GameManager.Instance.Grid.GetCell(startPos);
        if (cell != null)
        {
            cell.IsOccupied = true;
            cell.Occupant = this;
        }
    }

    /// <summary>
    /// Move the character to a new square cell.
    /// </summary>
    public void MoveToCell(SquareCell targetCell)
    {
        // Clear old cell
        SquareCell oldCell = GameManager.Instance.Grid.GetCell(GridPosition);
        if (oldCell != null)
        {
            oldCell.IsOccupied = false;
            oldCell.Occupant = null;
        }

        // Update position
        GridPosition = targetCell.Coords;
        transform.position = SquareGridUtils.GridToWorld(targetCell.X, targetCell.Y);

        // Register on new cell
        targetCell.IsOccupied = true;
        targetCell.Occupant = this;

        HasMovedThisTurn = true;
    }

    // ========== SINGLE ATTACK (Standard Action) ==========

    /// <summary>
    /// Perform a single attack against another character (standard action).
    /// Returns a CombatResult with details including critical hit info.
    /// </summary>
    public CombatResult Attack(CharacterController target)
    {
        return Attack(target, false, 0, null, null);
    }

    /// <summary>
    /// Perform a single attack with flanking context and optional range info.
    /// Includes full D&D 3.5 critical hit mechanics, racial attack bonuses, and feat effects.
    /// Uses weapon's DamageModifierType for correct STR bonus to damage.
    /// Integrates: Power Attack, Point Blank Shot, Weapon Focus, Weapon Specialization,
    /// Weapon Finesse, Combat Expertise, Improved Critical, Dodge.
    /// </summary>
    public CombatResult Attack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null)
    {
        // Calculate racial attack bonus against target
        int racialAtkBonus = Stats.GetRacialAttackBonus(target.Stats);
        int rangePenalty = (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange) ? rangeInfo.Penalty : 0;

        // Get equipped weapon for damage modifier and feat calculations
        ItemData equippedWeapon = GetEquippedMainWeapon();
        bool isRanged = (equippedWeapon != null && (equippedWeapon.WeaponCat == WeaponCategory.Ranged || equippedWeapon.RangeIncrement > 0))
                        && rangeInfo != null && !rangeInfo.IsMelee;
        bool isMelee = !isRanged;

        // === FEAT: Power Attack (melee only) ===
        int powerAtkPenalty = 0;
        int powerAtkDmgBonus = 0;
        if (isMelee && Stats.HasFeat("Power Attack") && PowerAttackValue > 0)
        {
            powerAtkPenalty = -PowerAttackValue;
            powerAtkDmgBonus = IsWeaponTwoHanded(equippedWeapon) ? PowerAttackValue * 2 : PowerAttackValue;
        }

        // === FEAT: Point Blank Shot (ranged, within 30 ft / 6 squares) ===
        bool pointBlankActive = false;
        int pbsAtkBonus = 0;
        int pbsDmgBonus = 0;
        if (isRanged && Stats.HasFeat("Point Blank Shot") && rangeInfo != null && rangeInfo.DistanceFeet <= 30)
        {
            pointBlankActive = true;
            pbsAtkBonus = 1;
            pbsDmgBonus = 1;
        }

        // === FEAT: Weapon Focus (+1 attack) & Greater Weapon Focus (+1 attack) ===
        int weaponFocusBonus = Stats.WeaponFocusAttackBonus;

        // === FEAT: Weapon Specialization (+2 damage) & Greater (+2 damage) ===
        int weaponSpecBonus = Stats.WeaponSpecDamageBonus;

        // === FEAT: Weapon Finesse (DEX instead of STR for attack with light weapons) ===
        int abilityMod = Stats.STRMod;
        string abilityName = "STR";
        if (isRanged)
        {
            abilityMod = Stats.DEXMod;
            abilityName = "DEX";
        }
        else if (FeatManager.ShouldUseWeaponFinesse(Stats, equippedWeapon))
        {
            abilityMod = Stats.DEXMod;
            abilityName = "DEX(Finesse)";
            Debug.Log($"[Feats] {Stats.CharacterName}: Weapon Finesse active, using DEX {Stats.DEXMod} for attack");
        }

        // === FEAT: Combat Expertise (trade attack for AC) ===
        int combatExpertisePenalty = 0;
        if (isMelee && Stats.HasFeat("Combat Expertise") && Stats.CombatExpertiseValue > 0)
        {
            combatExpertisePenalty = -Stats.CombatExpertiseValue;
            Debug.Log($"[Feats] {Stats.CharacterName}: Combat Expertise -{Stats.CombatExpertiseValue} attack, +{Stats.CombatExpertiseValue} AC");
        }

        int totalAtkMod = Stats.BaseAttackBonus + abilityMod + Stats.SizeModifier
                          + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                          + powerAtkPenalty + pbsAtkBonus + weaponFocusBonus + combatExpertisePenalty;

        int critThreatMin = Stats.CritThreatMin > 0 ? Stats.CritThreatMin : 20;
        // === FEAT: Improved Critical (double threat range) ===
        critThreatMin = FeatManager.GetAdjustedCritThreatMin(Stats, critThreatMin);
        int critMult = Stats.CritMultiplier > 0 ? Stats.CritMultiplier : 2;

        int totalFeatDmgBonus = powerAtkDmgBonus + pbsDmgBonus + weaponSpecBonus;

        // Record HP before attack
        int hpBefore = target.Stats.CurrentHP;

        var result = PerformSingleAttackWithCrit(target, totalAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            Stats.BaseDamageDice, Stats.BaseDamageCount, Stats.BonusDamage, critThreatMin, critMult,
            equippedWeapon, false, totalFeatDmgBonus);

        result.RacialAttackBonus = racialAtkBonus;
        result.SizeAttackBonus = Stats.SizeModifier;
        result.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
        result.PowerAttackDamageBonus = powerAtkDmgBonus;
        result.PointBlankShotActive = pointBlankActive;
        result.FeatDamageBonus = totalFeatDmgBonus;
        result.WeaponFocusBonus = weaponFocusBonus;
        result.WeaponSpecBonus = weaponSpecBonus;
        result.CombatExpertisePenalty = combatExpertisePenalty;

        // Breakdown fields for detailed logging
        result.BreakdownBAB = Stats.BaseAttackBonus;
        result.BreakdownAbilityMod = abilityMod;
        result.BreakdownAbilityName = abilityName;

        // Store range info on result
        if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
        {
            result.IsRangedAttack = true;
            result.RangeDistanceFeet = rangeInfo.DistanceFeet;
            result.RangeDistanceSquares = rangeInfo.SquareDistance;
            result.RangeIncrementNumber = rangeInfo.IncrementNumber;
            result.RangePenalty = rangeInfo.Penalty;
        }
        if (equippedWeapon != null)
        {
            result.WeaponName = equippedWeapon.Name;
            result.BaseDamageDiceStr = $"{Stats.BaseDamageCount}d{Stats.BaseDamageDice}";
        }

        // HP tracking
        result.DefenderHPBefore = hpBefore;
        result.DefenderHPAfter = target.Stats.CurrentHP;

        HasAttackedThisTurn = true;
        return result;
    }

    // ========== FULL ATTACK (Full-Round Action) ==========

    /// <summary>
    /// Perform a Full Attack action - all iterative attacks based on BAB.
    /// Each attack can independently threaten and confirm a critical hit.
    /// Includes racial attack bonuses, range penalties, and feat effects.
    /// Rapid Shot: extra attack at highest BAB, -2 to all ranged attacks.
    /// Power Attack: penalty to melee attack, bonus to melee damage.
    /// Point Blank Shot: +1 atk/dmg for ranged within 30 ft.
    /// </summary>
    public FullAttackResult FullAttack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null)
    {
        var result = new FullAttackResult();
        result.Type = FullAttackResult.AttackType.FullAttack;
        result.Attacker = this;
        result.Defender = target;
        result.DefenderHPBefore = target.Stats.CurrentHP;

        int[] attackBonuses = Stats.GetIterativeAttackBonuses();
        int critThreatMin = Stats.CritThreatMin > 0 ? Stats.CritThreatMin : 20;
        int critMult = Stats.CritMultiplier > 0 ? Stats.CritMultiplier : 2;
        int racialAtkBonus = Stats.GetRacialAttackBonus(target.Stats);
        int rangePenalty = (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange) ? rangeInfo.Penalty : 0;

        // Get equipped weapon for damage modifier and feat calculations
        ItemData equippedWeapon = GetEquippedMainWeapon();
        bool isRanged = (equippedWeapon != null && (equippedWeapon.WeaponCat == WeaponCategory.Ranged || equippedWeapon.RangeIncrement > 0))
                        && rangeInfo != null && !rangeInfo.IsMelee;
        bool isMelee = !isRanged;

        // Store weapon name on result
        if (equippedWeapon != null)
            result.MainWeaponName = equippedWeapon.Name;

        // === FEAT: Power Attack (melee only) ===
        int powerAtkPenalty = 0;
        int powerAtkDmgBonus = 0;
        if (isMelee && Stats.HasFeat("Power Attack") && PowerAttackValue > 0)
        {
            powerAtkPenalty = -PowerAttackValue;
            powerAtkDmgBonus = IsWeaponTwoHanded(equippedWeapon) ? PowerAttackValue * 2 : PowerAttackValue;
        }

        // === FEAT: Point Blank Shot (ranged, within 30 ft) ===
        bool pointBlankActive = false;
        int pbsAtkBonus = 0;
        int pbsDmgBonus = 0;
        if (isRanged && Stats.HasFeat("Point Blank Shot") && rangeInfo != null && rangeInfo.DistanceFeet <= 30)
        {
            pointBlankActive = true;
            pbsAtkBonus = 1;
            pbsDmgBonus = 1;
        }

        // === FEAT: Weapon Focus & Greater Weapon Focus ===
        int weaponFocusBonus = Stats.WeaponFocusAttackBonus;

        // === FEAT: Weapon Specialization & Greater ===
        int weaponSpecBonus = Stats.WeaponSpecDamageBonus;

        // === FEAT: Weapon Finesse ===
        int baseAbilityMod = Stats.STRMod;
        string baseAbilityName = "STR";
        if (isRanged)
        {
            baseAbilityMod = Stats.DEXMod;
            baseAbilityName = "DEX";
        }
        else if (FeatManager.ShouldUseWeaponFinesse(Stats, equippedWeapon))
        {
            baseAbilityMod = Stats.DEXMod;
            baseAbilityName = "DEX(Finesse)";
        }

        // === FEAT: Combat Expertise ===
        int combatExpertisePenalty = 0;
        if (isMelee && Stats.HasFeat("Combat Expertise") && Stats.CombatExpertiseValue > 0)
        {
            combatExpertisePenalty = -Stats.CombatExpertiseValue;
        }

        // === FEAT: Improved Critical ===
        critThreatMin = FeatManager.GetAdjustedCritThreatMin(Stats, critThreatMin);

        // === FEAT: Rapid Shot (ranged, full attack only) ===
        bool hasRapidShotFeat = Stats.HasFeat("Rapid Shot");
        bool rapidShotActive = isRanged && hasRapidShotFeat && RapidShotEnabled;
        int rapidShotPenalty = rapidShotActive ? -2 : 0;

        int totalFeatDmgBonus = powerAtkDmgBonus + pbsDmgBonus + weaponSpecBonus;

        // === Debug Logging ===
        Debug.Log($"[FullAttack] {Stats.CharacterName}: FullAttack() called");
        Debug.Log($"[FullAttack] Weapon: {(equippedWeapon != null ? equippedWeapon.Name : "(unarmed)")}, Ranged: {isRanged}");
        Debug.Log($"[FullAttack] Feats: WF={weaponFocusBonus}, WS={weaponSpecBonus}, PA={powerAtkDmgBonus}, CE={combatExpertisePenalty}");
        if (rapidShotActive) Debug.Log($"[FullAttack] Rapid Shot active: -2 penalty, +1 extra attack");

        // Build the list of attack bonuses, inserting Rapid Shot extra attack
        var allAttackBonuses = new List<int>(attackBonuses);
        int baseAttackCount = allAttackBonuses.Count;

        if (rapidShotActive)
        {
            allAttackBonuses.Insert(0, attackBonuses[0]);
            Debug.Log($"[FullAttack] Rapid Shot: attack count {baseAttackCount} → {allAttackBonuses.Count}");
        }
        else if (RapidShotEnabled && hasRapidShotFeat && !isRanged)
        {
            Debug.LogWarning($"[FullAttack] {Stats.CharacterName}: Rapid Shot ON but weapon is not ranged");
        }

        for (int i = 0; i < allAttackBonuses.Count; i++)
        {
            if (target.Stats.IsDead)
            {
                Debug.Log($"[FullAttack] Target is dead, stopping at attack {i + 1}");
                break;
            }

            int baseBonus = allAttackBonuses[i];
            int atkMod = baseBonus + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                         + powerAtkPenalty + pbsAtkBonus + weaponFocusBonus + combatExpertisePenalty + rapidShotPenalty;

            // Recalculate with correct ability mod (iterative uses BAB-based bonus, not STRMod again)
            // The baseBonus from GetIterativeAttackBonuses already includes STRMod+SizeModifier
            // So we need to add other feat/situational modifiers
            atkMod = baseBonus + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                     + powerAtkPenalty + pbsAtkBonus + weaponFocusBonus + combatExpertisePenalty + rapidShotPenalty;
            // Note: Weapon Finesse already handled in GetIterativeAttackBonuses if needed
            // For now, the base bonus includes STRMod; if Finesse, we adjust
            if (!isRanged && FeatManager.ShouldUseWeaponFinesse(Stats, equippedWeapon))
            {
                // Remove STR, add DEX (base bonus already has STRMod from GetIterativeAttackBonuses)
                atkMod += (Stats.DEXMod - Stats.STRMod);
            }

            string label;
            if (rapidShotActive && i == 0)
                label = $"Attack 1 (Rapid Shot, {CharacterStats.FormatMod(baseBonus)})";
            else
                label = $"Attack {i + 1} ({CharacterStats.FormatMod(baseBonus)})";

            int hpBeforeAtk = target.Stats.CurrentHP;

            CombatResult atk = PerformSingleAttackWithCrit(target, atkMod, isFlanking, flankingBonus, flankingPartnerName,
                Stats.BaseDamageDice, Stats.BaseDamageCount, Stats.BonusDamage, critThreatMin, critMult,
                equippedWeapon, false, totalFeatDmgBonus);

            atk.RacialAttackBonus = racialAtkBonus;
            atk.SizeAttackBonus = Stats.SizeModifier;
            atk.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
            atk.PowerAttackDamageBonus = powerAtkDmgBonus;
            atk.RapidShotActive = rapidShotActive;
            atk.PointBlankShotActive = pointBlankActive;
            atk.FeatDamageBonus = totalFeatDmgBonus;
            atk.WeaponFocusBonus = weaponFocusBonus;
            atk.WeaponSpecBonus = weaponSpecBonus;
            atk.CombatExpertisePenalty = combatExpertisePenalty;

            // Breakdown fields
            atk.BreakdownBAB = baseBonus;
            atk.BreakdownAbilityMod = baseAbilityMod;
            atk.BreakdownAbilityName = baseAbilityName;

            // Store range info on each attack result
            if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
            {
                atk.IsRangedAttack = true;
                atk.RangeDistanceFeet = rangeInfo.DistanceFeet;
                atk.RangeDistanceSquares = rangeInfo.SquareDistance;
                atk.RangeIncrementNumber = rangeInfo.IncrementNumber;
                atk.RangePenalty = rangeInfo.Penalty;
            }
            if (equippedWeapon != null)
            {
                atk.WeaponName = equippedWeapon.Name;
                atk.BaseDamageDiceStr = $"{Stats.BaseDamageCount}d{Stats.BaseDamageDice}";
            }

            atk.DefenderHPBefore = hpBeforeAtk;
            atk.DefenderHPAfter = target.Stats.CurrentHP;

            result.Attacks.Add(atk);
            result.AttackLabels.Add(label);
        }

        result.DefenderHPAfter = target.Stats.CurrentHP;
        result.TargetKilled = target.Stats.IsDead;
        HasAttackedThisTurn = true;
        return result;
    }

    // ========== DUAL WIELD ATTACK (Full-Round Action) ==========

    /// <summary>
    /// Check if this character has weapons in both hands (can dual wield).
    /// Two-handed weapons cannot be dual-wielded.
    /// </summary>
    public bool CanDualWield()
    {
        var inv = GetComponent<InventoryComponent>();
        if (inv == null || inv.CharacterInventory == null) return false;
        return inv.CharacterInventory.CanDualWield();
    }

    /// <summary>
    /// Get the dual wield penalty information.
    /// Returns (mainHandPenalty, offHandPenalty, isLightOffHand).
    /// Uses FeatManager to account for Two-Weapon Fighting feat.
    /// Without TWF feat: -6/-10 (normal) or -4/-8 (light off-hand).
    /// With TWF feat: -4/-4 (normal) or -2/-2 (light off-hand).
    /// </summary>
    public (int mainPenalty, int offPenalty, bool lightOffHand) GetDualWieldPenalties()
    {
        var inv = GetComponent<InventoryComponent>();
        if (inv == null) return (-6, -10, false);

        var offHandItem = inv.CharacterInventory.LeftHandSlot;
        bool lightOffHand = offHandItem != null && offHandItem.IsLightWeapon;

        var (mainPen, offPen) = FeatManager.GetTWFPenalties(Stats, lightOffHand);
        Debug.Log($"[TWF] {Stats.CharacterName}: TWF penalties = {mainPen}/{offPen}" +
                  (Stats.HasFeat("Two-Weapon Fighting") ? " (TWF feat)" : " (no TWF feat)") +
                  (lightOffHand ? " (light off-hand)" : ""));
        return (mainPen, offPen, lightOffHand);
    }

    /// <summary>
    /// Perform a Dual Wield attack - main hand and off-hand attacks.
    /// Each hand uses its own weapon's critical hit properties.
    /// Includes feat effects (Power Attack for melee, Point Blank Shot for ranged).
    /// </summary>
    public FullAttackResult DualWieldAttack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null)
    {
        var result = new FullAttackResult();
        result.Type = FullAttackResult.AttackType.DualWield;
        result.Attacker = this;
        result.Defender = target;
        result.DefenderHPBefore = target.Stats.CurrentHP;

        var inv = GetComponent<InventoryComponent>();
        if (inv == null) return result;

        var mainWeapon = inv.CharacterInventory.RightHandSlot;
        var offWeapon = inv.CharacterInventory.LeftHandSlot;

        if (mainWeapon == null || offWeapon == null) return result;

        // Store weapon names on result
        result.MainWeaponName = mainWeapon.Name;
        result.OffWeaponName = offWeapon.Name;

        var (mainPenalty, offPenalty, lightOff) = GetDualWieldPenalties();

        // Racial attack bonus and range penalty
        int racialAtkBonus = Stats.GetRacialAttackBonus(target.Stats);
        int rangePenalty = (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange) ? rangeInfo.Penalty : 0;

        bool isRanged = rangeInfo != null && !rangeInfo.IsMelee;
        bool isMelee = !isRanged;

        // === FEAT: Power Attack (melee only) ===
        int powerAtkPenalty = 0;
        int powerAtkDmgBonus = 0;
        if (isMelee && Stats.HasFeat("Power Attack") && PowerAttackValue > 0)
        {
            powerAtkPenalty = -PowerAttackValue;
            // Dual wielding is one-handed, no 2× bonus
            powerAtkDmgBonus = PowerAttackValue;
        }

        // === FEAT: Point Blank Shot (ranged, within 30 ft) ===
        bool pointBlankActive = false;
        int pbsAtkBonus = 0;
        int pbsDmgBonus = 0;
        if (isRanged && Stats.HasFeat("Point Blank Shot") && rangeInfo != null && rangeInfo.DistanceFeet <= 30)
        {
            pointBlankActive = true;
            pbsAtkBonus = 1;
            pbsDmgBonus = 1;
        }

        // === FEAT: Weapon Focus (+1 attack with chosen weapon) ===
        int mainWFBonus = FeatManager.GetWeaponFocusBonus(Stats, mainWeapon?.Name ?? "Unarmed");
        int offWFBonus = FeatManager.GetWeaponFocusBonus(Stats, offWeapon?.Name ?? "Unarmed");

        // === FEAT: Weapon Specialization (+2 damage with chosen weapon) ===
        int mainWSBonus = FeatManager.GetWeaponSpecializationBonus(Stats, mainWeapon?.Name ?? "Unarmed");
        int offWSBonus = FeatManager.GetWeaponSpecializationBonus(Stats, offWeapon?.Name ?? "Unarmed");

        // === FEAT: Weapon Finesse (use DEX instead of STR for light/finesse melee) ===
        int finesseAtkAdjust = 0;
        string abilityName = isRanged ? "DEX" : "STR";
        int abilityMod = isRanged ? Stats.DEXMod : Stats.STRMod;
        if (isMelee && FeatManager.ShouldUseWeaponFinesse(Stats, mainWeapon))
        {
            finesseAtkAdjust = Stats.DEXMod - Stats.STRMod;
            abilityName = "DEX";
            abilityMod = Stats.DEXMod;
        }

        // === FEAT: Combat Expertise (trade attack for AC) ===
        int combatExpertisePenalty = 0;
        if (isMelee && Stats.HasFeat("Combat Expertise") && Stats.CombatExpertiseValue > 0)
        {
            int maxCE = FeatManager.GetMaxCombatExpertise(Stats);
            combatExpertisePenalty = -Mathf.Min(Stats.CombatExpertiseValue, maxCE);
        }

        // Main hand attack
        int mainAtkMod = Stats.AttackBonus + mainPenalty + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                         + powerAtkPenalty + pbsAtkBonus + mainWFBonus + finesseAtkAdjust + combatExpertisePenalty;
        string mainLabel = $"Attack 1 - Main Hand ({mainWeapon.Name})";

        int mainCritMin = mainWeapon.CritThreatMin > 0 ? mainWeapon.CritThreatMin : 20;
        int mainCritMult = mainWeapon.CritMultiplier > 0 ? mainWeapon.CritMultiplier : 2;

        // === FEAT: Improved Critical (double threat range) ===
        mainCritMin = FeatManager.GetAdjustedCritThreatMin(Stats, mainCritMin);

        int totalMainFeatDmg = powerAtkDmgBonus + (pointBlankActive ? 1 : 0) + mainWSBonus;

        int hpBeforeMain = target.Stats.CurrentHP;
        CombatResult mainAtk = PerformSingleAttackWithCrit(target, mainAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            mainWeapon.DamageDice, mainWeapon.DamageCount, mainWeapon.BonusDamage, mainCritMin, mainCritMult,
            mainWeapon, false, totalMainFeatDmg);
        mainAtk.RacialAttackBonus = racialAtkBonus;
        mainAtk.SizeAttackBonus = Stats.SizeModifier;
        mainAtk.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
        mainAtk.PowerAttackDamageBonus = powerAtkDmgBonus;
        mainAtk.PointBlankShotActive = pointBlankActive;
        mainAtk.FeatDamageBonus = totalMainFeatDmg;
        mainAtk.WeaponFocusBonus = mainWFBonus;
        mainAtk.WeaponSpecBonus = mainWSBonus;
        mainAtk.CombatExpertisePenalty = combatExpertisePenalty;
        mainAtk.WeaponName = mainWeapon.Name;
        mainAtk.BaseDamageDiceStr = $"{mainWeapon.DamageCount}d{mainWeapon.DamageDice}";
        mainAtk.IsDualWieldAttack = true;
        mainAtk.IsOffHandAttack = false;
        mainAtk.BreakdownBAB = Stats.BaseAttackBonus;
        mainAtk.BreakdownAbilityMod = abilityMod;
        mainAtk.BreakdownAbilityName = abilityName;
        mainAtk.BreakdownDualWieldPenalty = mainPenalty;
        mainAtk.DefenderHPBefore = hpBeforeMain;
        mainAtk.DefenderHPAfter = target.Stats.CurrentHP;

        if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
        {
            mainAtk.IsRangedAttack = true;
            mainAtk.RangeDistanceFeet = rangeInfo.DistanceFeet;
            mainAtk.RangeDistanceSquares = rangeInfo.SquareDistance;
            mainAtk.RangeIncrementNumber = rangeInfo.IncrementNumber;
            mainAtk.RangePenalty = rangeInfo.Penalty;
        }
        result.Attacks.Add(mainAtk);
        result.AttackLabels.Add(mainLabel);

        // Off-hand attack (only if target still alive)
        if (!target.Stats.IsDead)
        {
            int offAtkMod = Stats.AttackBonus + offPenalty + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                            + powerAtkPenalty + pbsAtkBonus + offWFBonus + finesseAtkAdjust + combatExpertisePenalty;
            string offLabel = $"Attack 2 - Off Hand ({offWeapon.Name})";

            int offCritMin = offWeapon.CritThreatMin > 0 ? offWeapon.CritThreatMin : 20;
            int offCritMult = offWeapon.CritMultiplier > 0 ? offWeapon.CritMultiplier : 2;

            // === FEAT: Improved Critical (double threat range) ===
            offCritMin = FeatManager.GetAdjustedCritThreatMin(Stats, offCritMin);

            int totalOffFeatDmg = powerAtkDmgBonus + (pointBlankActive ? 1 : 0) + offWSBonus;

            int hpBeforeOff = target.Stats.CurrentHP;
            CombatResult offAtk = PerformSingleAttackWithCrit(target, offAtkMod, isFlanking, flankingBonus, flankingPartnerName,
                offWeapon.DamageDice, offWeapon.DamageCount, offWeapon.BonusDamage, offCritMin, offCritMult,
                offWeapon, true, totalOffFeatDmg);
            offAtk.RacialAttackBonus = racialAtkBonus;
            offAtk.SizeAttackBonus = Stats.SizeModifier;
            offAtk.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
            offAtk.PowerAttackDamageBonus = powerAtkDmgBonus;
            offAtk.PointBlankShotActive = pointBlankActive;
            offAtk.FeatDamageBonus = totalOffFeatDmg;
            offAtk.WeaponFocusBonus = offWFBonus;
            offAtk.WeaponSpecBonus = offWSBonus;
            offAtk.CombatExpertisePenalty = combatExpertisePenalty;
            offAtk.WeaponName = offWeapon.Name;
            offAtk.BaseDamageDiceStr = $"{offWeapon.DamageCount}d{offWeapon.DamageDice}";
            offAtk.IsDualWieldAttack = true;
            offAtk.IsOffHandAttack = true;
            offAtk.BreakdownBAB = Stats.BaseAttackBonus;
            offAtk.BreakdownAbilityMod = abilityMod;
            offAtk.BreakdownAbilityName = abilityName;
            offAtk.BreakdownDualWieldPenalty = offPenalty;
            offAtk.DefenderHPBefore = hpBeforeOff;
            offAtk.DefenderHPAfter = target.Stats.CurrentHP;

            if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
            {
                offAtk.IsRangedAttack = true;
                offAtk.RangeDistanceFeet = rangeInfo.DistanceFeet;
                offAtk.RangeDistanceSquares = rangeInfo.SquareDistance;
                offAtk.RangeIncrementNumber = rangeInfo.IncrementNumber;
                offAtk.RangePenalty = rangeInfo.Penalty;
            }
            result.Attacks.Add(offAtk);
            result.AttackLabels.Add(offLabel);
        }

        result.DefenderHPAfter = target.Stats.CurrentHP;
        result.TargetKilled = target.Stats.IsDead;
        HasAttackedThisTurn = true;
        return result;
    }

    // ========== INTERNAL: Single attack with critical hit support ==========

    /// <summary>
    /// Perform a single attack with full D&D 3.5 critical hit mechanics.
    /// Uses the weapon's DamageModifierType to determine STR bonus to damage.
    /// Step 1: Roll d20. Check if in threat range.
    /// Step 2: If threat, roll confirmation vs same AC with same bonus.
    /// Step 3: If confirmed, multiply weapon dice (not static bonuses or sneak attack).
    /// </summary>
    /// <param name="weapon">The weapon being used (null = unarmed)</param>
    /// <param name="isOffHand">True if this is an off-hand attack (overrides to 0.5× STR)</param>
    /// <param name="featDamageBonus">Extra flat damage from feats (Power Attack, Point Blank Shot)</param>
    private CombatResult PerformSingleAttackWithCrit(CharacterController target, int totalAtkMod,
        bool isFlanking, int flankingBonus, string flankingPartnerName,
        int damageDice, int damageCount, int bonusDamage,
        int critThreatMin, int critMultiplier,
        ItemData weapon, bool isOffHand, int featDamageBonus = 0)
    {
        var result = new CombatResult();
        result.Attacker = this;
        result.Defender = target;
        result.IsFlanking = isFlanking;
        result.FlankingBonus = isFlanking ? flankingBonus : 0;
        result.FlankingPartnerName = flankingPartnerName ?? "";

        // Store weapon crit properties on result for display
        result.CritThreatMin = critThreatMin;
        result.CritMultiplier = critMultiplier;

        // Store base damage dice string
        result.BaseDamageDiceStr = $"{damageCount}d{damageDice}";

        // Calculate the damage modifier based on weapon's DamageModifierType
        int damageModifier = Stats.GetWeaponDamageModifier(weapon, isOffHand);
        string damageModDesc = Stats.GetDamageModifierDescription(weapon, isOffHand);
        result.DamageModifier = damageModifier;
        result.DamageModifierDesc = damageModDesc;

        // Step 1: Roll to hit
        var (hit, roll, total) = Stats.RollToHitWithMod(totalAtkMod, target.Stats.ArmorClass);
        result.DieRoll = roll;
        result.TotalRoll = total;
        result.TargetAC = target.Stats.ArmorClass;
        result.Hit = hit;
        result.NaturalTwenty = (roll == 20);
        result.NaturalOne = (roll == 1);

        // Step 2: Check for critical threat (only if the attack hit)
        bool isThreat = false;
        bool critConfirmed = false;
        int confirmRoll = 0;
        int confirmTotal = 0;

        if (hit)
        {
            isThreat = CharacterStats.IsCritThreat(roll, critThreatMin);
            result.IsCritThreat = isThreat;

            if (isThreat)
            {
                // Roll confirmation with the same attack modifier
                var (confirmed, confRoll, confTotal) = Stats.RollCritConfirmation(totalAtkMod, target.Stats.ArmorClass);
                critConfirmed = confirmed;
                confirmRoll = confRoll;
                confirmTotal = confTotal;
                result.CritConfirmed = critConfirmed;
                result.ConfirmationRoll = confirmRoll;
                result.ConfirmationTotal = confirmTotal;
            }

            // Step 3: Roll damage (feat bonus added as flat bonus, not multiplied on crit)
            int damage;
            int baseDmgRoll;
            if (critConfirmed)
            {
                // Critical damage: multiply weapon dice, add static bonuses (STR + bonus) once
                int totalCritDice = damageCount * critMultiplier;
                baseDmgRoll = Stats.RollBaseDamage(damageDice, totalCritDice);
                damage = baseDmgRoll + damageModifier + bonusDamage;
                damage += featDamageBonus; // Feat bonus added after crit multiplication
                result.CritDamageDice = $"{totalCritDice}d{damageDice}+{damageModifier + bonusDamage}";
            }
            else
            {
                // Normal damage - roll weapon dice separately for breakdown
                baseDmgRoll = Stats.RollBaseDamage(damageDice, damageCount);
                damage = baseDmgRoll + damageModifier + bonusDamage + featDamageBonus;
            }
            damage = Mathf.Max(1, damage); // Ensure minimum 1 damage
            result.Damage = damage;
            result.BaseDamageRoll = baseDmgRoll;

            // Sneak attack: applies if attacker is Rogue and is flanking
            // Sneak attack is NOT multiplied on critical hits (D&D 3.5 rule)
            if (Stats.IsRogue && isFlanking)
            {
                int sneakDice = CombatUtils.GetSneakAttackDice(Stats.Level);
                int sneakDmg = CombatUtils.RollSneakAttackDamage(Stats.Level);
                result.SneakAttackApplied = true;
                result.SneakAttackDice = sneakDice;
                result.SneakAttackDamage = sneakDmg;
            }

            // Apply total damage to target
            target.Stats.TakeDamage(result.TotalDamage);

            if (target.Stats.IsDead)
            {
                target.OnDeath();
                result.TargetKilled = true;
            }
        }

        return result;
    }

    // ========== WEAPON HELPERS ==========

    /// <summary>
    /// Check if the equipped main weapon is a ranged weapon.
    /// Used by UI to determine if Rapid Shot can actually apply.
    /// </summary>
    public bool IsEquippedWeaponRanged()
    {
        ItemData weapon = GetEquippedMainWeapon();
        if (weapon == null) return false;
        return weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0;
    }

    /// <summary>
    /// Get the equipped main-hand weapon (right hand first, then left hand).
    /// Returns null if no weapon equipped (unarmed).
    /// </summary>
    public ItemData GetEquippedMainWeapon()
    {
        var inv = GetComponent<InventoryComponent>();
        if (inv == null || inv.CharacterInventory == null) return null;

        var rightHand = inv.CharacterInventory.RightHandSlot;
        if (rightHand != null && rightHand.IsWeapon) return rightHand;

        var leftHand = inv.CharacterInventory.LeftHandSlot;
        if (leftHand != null && leftHand.IsWeapon) return leftHand;

        return null; // Unarmed
    }

    // ========== DUAL WIELD INFO ==========

    /// <summary>
    /// Get a description of dual wield status for UI display.
    /// </summary>
    public string GetDualWieldDescription()
    {
        if (!CanDualWield()) return "";

        var inv = GetComponent<InventoryComponent>();
        var mainWeapon = inv.CharacterInventory.RightHandSlot;
        var offWeapon = inv.CharacterInventory.LeftHandSlot;
        var (mainPen, offPen, lightOff) = GetDualWieldPenalties();

        string lightStr = lightOff ? " (light)" : "";
        return $"Dual Wield: {mainWeapon.Name} / {offWeapon.Name}{lightStr}\n" +
               $"Penalties: Main {mainPen}, Off-hand {offPen}";
    }

    // ========== LIFECYCLE ==========

    /// <summary>
    /// Called when this character reaches 0 HP.
    /// </summary>
    public void OnDeath()
    {
        if (_sr != null && DeadSprite != null)
            _sr.sprite = DeadSprite;
    }

    /// <summary>
    /// Reset turn flags and action economy.
    /// Power Attack and Rapid Shot settings persist between turns (player choice).
    /// </summary>
    public void StartNewTurn()
    {
        HasMovedThisTurn = false;
        HasAttackedThisTurn = false;
        Actions.Reset();
        // Note: PowerAttackValue and RapidShotEnabled persist between turns
        // They are player-controlled and reset only when the player changes them
    }
}

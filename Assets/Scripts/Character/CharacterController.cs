using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls a character on the square grid (both PC and NPC).
/// Supports D&D 3.5 action economy, full attacks, dual wielding, and critical hits.
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

        int totalAtkMod = Stats.AttackBonus + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                          + powerAtkPenalty + pbsAtkBonus;

        int critThreatMin = Stats.CritThreatMin > 0 ? Stats.CritThreatMin : 20;
        int critMult = Stats.CritMultiplier > 0 ? Stats.CritMultiplier : 2;

        var result = PerformSingleAttackWithCrit(target, totalAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            Stats.BaseDamageDice, Stats.BaseDamageCount, Stats.BonusDamage, critThreatMin, critMult,
            equippedWeapon, false, powerAtkDmgBonus + pbsDmgBonus);

        result.RacialAttackBonus = racialAtkBonus;
        result.SizeAttackBonus = Stats.SizeModifier;
        result.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
        result.PowerAttackDamageBonus = powerAtkDmgBonus;
        result.PointBlankShotActive = pointBlankActive;

        // Store range info on result
        if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
        {
            result.IsRangedAttack = true;
            result.RangeDistanceFeet = rangeInfo.DistanceFeet;
            result.RangeIncrementNumber = rangeInfo.IncrementNumber;
            result.RangePenalty = rangeInfo.Penalty;
        }
        if (equippedWeapon != null)
            result.WeaponName = equippedWeapon.Name;

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

        // === FEAT: Rapid Shot (ranged, full attack only) ===
        bool rapidShotActive = isRanged && Stats.HasFeat("Rapid Shot") && RapidShotEnabled;
        int rapidShotPenalty = rapidShotActive ? -2 : 0;

        // Build the list of attack bonuses, inserting Rapid Shot extra attack
        var allAttackBonuses = new List<int>(attackBonuses);
        var allLabels = new List<string>();

        if (rapidShotActive)
        {
            // Insert extra attack at highest BAB (index 0)
            allAttackBonuses.Insert(0, attackBonuses[0]);
        }

        for (int i = 0; i < allAttackBonuses.Count; i++)
        {
            if (target.Stats.IsDead) break;

            int baseBonus = allAttackBonuses[i];
            int atkMod = baseBonus + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                         + powerAtkPenalty + pbsAtkBonus + rapidShotPenalty;

            string label;
            if (rapidShotActive && i == 0)
                label = $"Rapid Shot ({CharacterStats.FormatMod(baseBonus)})";
            else
            {
                int displayIdx = rapidShotActive ? i : i + 1;
                label = (displayIdx <= 1) ? $"Attack ({CharacterStats.FormatMod(baseBonus)})" :
                    $"Attack {displayIdx} ({CharacterStats.FormatMod(baseBonus)})";
            }

            CombatResult atk = PerformSingleAttackWithCrit(target, atkMod, isFlanking, flankingBonus, flankingPartnerName,
                Stats.BaseDamageDice, Stats.BaseDamageCount, Stats.BonusDamage, critThreatMin, critMult,
                equippedWeapon, false, powerAtkDmgBonus + pbsDmgBonus);

            atk.RacialAttackBonus = racialAtkBonus;
            atk.SizeAttackBonus = Stats.SizeModifier;
            atk.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
            atk.PowerAttackDamageBonus = powerAtkDmgBonus;
            atk.RapidShotActive = rapidShotActive;
            atk.PointBlankShotActive = pointBlankActive;

            // Store range info on each attack result
            if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
            {
                atk.IsRangedAttack = true;
                atk.RangeDistanceFeet = rangeInfo.DistanceFeet;
                atk.RangeIncrementNumber = rangeInfo.IncrementNumber;
                atk.RangePenalty = rangeInfo.Penalty;
            }
            if (equippedWeapon != null) atk.WeaponName = equippedWeapon.Name;

            result.Attacks.Add(atk);
            result.AttackLabels.Add(label);
        }

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
    /// Without TWF feat: -6/-10 (normal) or -4/-8 (light off-hand).
    /// </summary>
    public (int mainPenalty, int offPenalty, bool lightOffHand) GetDualWieldPenalties()
    {
        var inv = GetComponent<InventoryComponent>();
        if (inv == null) return (-6, -10, false);

        var offHandItem = inv.CharacterInventory.LeftHandSlot;
        bool lightOffHand = offHandItem != null && offHandItem.IsLightWeapon;

        // D&D 3.5 TWF penalties without the TWF feat
        if (lightOffHand)
            return (-4, -8, true);
        else
            return (-6, -10, false);
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

        var inv = GetComponent<InventoryComponent>();
        if (inv == null) return result;

        var mainWeapon = inv.CharacterInventory.RightHandSlot;
        var offWeapon = inv.CharacterInventory.LeftHandSlot;

        if (mainWeapon == null || offWeapon == null) return result;

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

        // Main hand attack
        int mainAtkMod = Stats.AttackBonus + mainPenalty + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                         + powerAtkPenalty + pbsAtkBonus;
        string mainLabel = $"Main Hand ({mainWeapon.Name}, {CharacterStats.FormatMod(Stats.AttackBonus + mainPenalty)})";

        int mainCritMin = mainWeapon.CritThreatMin > 0 ? mainWeapon.CritThreatMin : 20;
        int mainCritMult = mainWeapon.CritMultiplier > 0 ? mainWeapon.CritMultiplier : 2;

        CombatResult mainAtk = PerformSingleAttackWithCrit(target, mainAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            mainWeapon.DamageDice, mainWeapon.DamageCount, mainWeapon.BonusDamage, mainCritMin, mainCritMult,
            mainWeapon, false, powerAtkDmgBonus + pbsDmgBonus);
        mainAtk.RacialAttackBonus = racialAtkBonus;
        mainAtk.SizeAttackBonus = Stats.SizeModifier;
        mainAtk.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
        mainAtk.PowerAttackDamageBonus = powerAtkDmgBonus;
        mainAtk.PointBlankShotActive = pointBlankActive;
        mainAtk.WeaponName = mainWeapon.Name;
        if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
        {
            mainAtk.IsRangedAttack = true;
            mainAtk.RangeDistanceFeet = rangeInfo.DistanceFeet;
            mainAtk.RangeIncrementNumber = rangeInfo.IncrementNumber;
            mainAtk.RangePenalty = rangeInfo.Penalty;
        }
        result.Attacks.Add(mainAtk);
        result.AttackLabels.Add(mainLabel);

        // Off-hand attack (only if target still alive)
        if (!target.Stats.IsDead)
        {
            int offAtkMod = Stats.AttackBonus + offPenalty + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                            + powerAtkPenalty + pbsAtkBonus;
            string offLabel = $"Off-Hand ({offWeapon.Name}, {CharacterStats.FormatMod(Stats.AttackBonus + offPenalty)})";

            int offCritMin = offWeapon.CritThreatMin > 0 ? offWeapon.CritThreatMin : 20;
            int offCritMult = offWeapon.CritMultiplier > 0 ? offWeapon.CritMultiplier : 2;

            CombatResult offAtk = PerformSingleAttackWithCrit(target, offAtkMod, isFlanking, flankingBonus, flankingPartnerName,
                offWeapon.DamageDice, offWeapon.DamageCount, offWeapon.BonusDamage, offCritMin, offCritMult,
                offWeapon, true, powerAtkDmgBonus + pbsDmgBonus);
            offAtk.RacialAttackBonus = racialAtkBonus;
            offAtk.SizeAttackBonus = Stats.SizeModifier;
            offAtk.PowerAttackValue = (powerAtkPenalty != 0) ? PowerAttackValue : 0;
            offAtk.PowerAttackDamageBonus = powerAtkDmgBonus;
            offAtk.PointBlankShotActive = pointBlankActive;
            offAtk.WeaponName = offWeapon.Name;
            if (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange)
            {
                offAtk.IsRangedAttack = true;
                offAtk.RangeDistanceFeet = rangeInfo.DistanceFeet;
                offAtk.RangeIncrementNumber = rangeInfo.IncrementNumber;
                offAtk.RangePenalty = rangeInfo.Penalty;
            }
            result.Attacks.Add(offAtk);
            result.AttackLabels.Add(offLabel);
        }

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
            if (critConfirmed)
            {
                // Critical damage: multiply weapon dice, add static bonuses (STR + bonus) once
                damage = Stats.RollCritDamageWithModType(damageDice, damageCount, bonusDamage, damageModifier, critMultiplier);
                damage += featDamageBonus; // Feat bonus added after crit multiplication
                result.CritDamageDice = $"{damageCount * critMultiplier}d{damageDice}+{damageModifier + bonusDamage}";
            }
            else
            {
                // Normal damage
                damage = Stats.RollDamageWithModType(damageDice, damageCount, bonusDamage, damageModifier);
                damage += featDamageBonus;
            }
            damage = Mathf.Max(1, damage); // Ensure minimum 1 damage
            result.Damage = damage;

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

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
    /// Includes full D&D 3.5 critical hit mechanics and racial attack bonuses.
    /// Uses weapon's DamageModifierType for correct STR bonus to damage.
    /// </summary>
    public CombatResult Attack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null)
    {
        // Calculate racial attack bonus against target
        int racialAtkBonus = Stats.GetRacialAttackBonus(target.Stats);
        int rangePenalty = (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange) ? rangeInfo.Penalty : 0;
        int totalAtkMod = Stats.AttackBonus + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty;
        int critThreatMin = Stats.CritThreatMin > 0 ? Stats.CritThreatMin : 20;
        int critMult = Stats.CritMultiplier > 0 ? Stats.CritMultiplier : 2;

        // Get equipped weapon for damage modifier calculation
        ItemData equippedWeapon = GetEquippedMainWeapon();

        var result = PerformSingleAttackWithCrit(target, totalAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            Stats.BaseDamageDice, Stats.BaseDamageCount, Stats.BonusDamage, critThreatMin, critMult,
            equippedWeapon, false);

        result.RacialAttackBonus = racialAtkBonus;
        result.SizeAttackBonus = Stats.SizeModifier;

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
    /// Includes racial attack bonuses and range penalties.
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

        // Get equipped weapon for damage modifier calculation
        ItemData equippedWeapon = GetEquippedMainWeapon();

        for (int i = 0; i < attackBonuses.Length; i++)
        {
            if (target.Stats.IsDead) break;

            int atkMod = attackBonuses[i] + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty;
            string label = (i == 0) ? $"Attack ({CharacterStats.FormatMod(attackBonuses[i])})" :
                $"Attack {i + 1} ({CharacterStats.FormatMod(attackBonuses[i])})";

            CombatResult atk = PerformSingleAttackWithCrit(target, atkMod, isFlanking, flankingBonus, flankingPartnerName,
                Stats.BaseDamageDice, Stats.BaseDamageCount, Stats.BonusDamage, critThreatMin, critMult,
                equippedWeapon, false);

            atk.RacialAttackBonus = racialAtkBonus;
            atk.SizeAttackBonus = Stats.SizeModifier;

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

        // Main hand attack: BAB + STR + penalty + flanking + racial + range, uses main weapon's crit stats
        int mainAtkMod = Stats.AttackBonus + mainPenalty + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty;
        string mainLabel = $"Main Hand ({mainWeapon.Name}, {CharacterStats.FormatMod(Stats.AttackBonus + mainPenalty)})";

        int mainCritMin = mainWeapon.CritThreatMin > 0 ? mainWeapon.CritThreatMin : 20;
        int mainCritMult = mainWeapon.CritMultiplier > 0 ? mainWeapon.CritMultiplier : 2;

        CombatResult mainAtk = PerformSingleAttackWithCrit(target, mainAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            mainWeapon.DamageDice, mainWeapon.DamageCount, mainWeapon.BonusDamage, mainCritMin, mainCritMult,
            mainWeapon, false);
        mainAtk.RacialAttackBonus = racialAtkBonus;
        mainAtk.SizeAttackBonus = Stats.SizeModifier;
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
            int offAtkMod = Stats.AttackBonus + offPenalty + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty;
            string offLabel = $"Off-Hand ({offWeapon.Name}, {CharacterStats.FormatMod(Stats.AttackBonus + offPenalty)})";

            int offCritMin = offWeapon.CritThreatMin > 0 ? offWeapon.CritThreatMin : 20;
            int offCritMult = offWeapon.CritMultiplier > 0 ? offWeapon.CritMultiplier : 2;

            CombatResult offAtk = PerformSingleAttackWithCrit(target, offAtkMod, isFlanking, flankingBonus, flankingPartnerName,
                offWeapon.DamageDice, offWeapon.DamageCount, offWeapon.BonusDamage, offCritMin, offCritMult,
                offWeapon, true);
            offAtk.RacialAttackBonus = racialAtkBonus;
            offAtk.SizeAttackBonus = Stats.SizeModifier;
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
    private CombatResult PerformSingleAttackWithCrit(CharacterController target, int totalAtkMod,
        bool isFlanking, int flankingBonus, string flankingPartnerName,
        int damageDice, int damageCount, int bonusDamage,
        int critThreatMin, int critMultiplier,
        ItemData weapon, bool isOffHand)
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

            // Step 3: Roll damage
            int damage;
            if (critConfirmed)
            {
                // Critical damage: multiply weapon dice, add static bonuses (STR + bonus) once
                damage = Stats.RollCritDamageWithModType(damageDice, damageCount, bonusDamage, damageModifier, critMultiplier);
                result.CritDamageDice = $"{damageCount * critMultiplier}d{damageDice}+{damageModifier + bonusDamage}";
            }
            else
            {
                // Normal damage
                damage = Stats.RollDamageWithModType(damageDice, damageCount, bonusDamage, damageModifier);
            }
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
    /// </summary>
    public void StartNewTurn()
    {
        HasMovedThisTurn = false;
        HasAttackedThisTurn = false;
        Actions.Reset();
    }
}

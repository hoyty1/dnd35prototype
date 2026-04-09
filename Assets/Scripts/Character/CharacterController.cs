using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls a character on the hex grid (both PC and NPC).
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
        transform.position = HexUtils.AxialToWorld(startPos.x, startPos.y);

        // Register on grid
        HexCell cell = GameManager.Instance.Grid.GetCell(startPos);
        if (cell != null)
        {
            cell.IsOccupied = true;
            cell.Occupant = this;
        }
    }

    /// <summary>
    /// Move the character to a new hex cell.
    /// </summary>
    public void MoveToCell(HexCell targetCell)
    {
        // Clear old cell
        HexCell oldCell = GameManager.Instance.Grid.GetCell(GridPosition);
        if (oldCell != null)
        {
            oldCell.IsOccupied = false;
            oldCell.Occupant = null;
        }

        // Update position
        GridPosition = targetCell.Coords;
        transform.position = HexUtils.AxialToWorld(targetCell.Q, targetCell.R);

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
        return Attack(target, false, 0, null);
    }

    /// <summary>
    /// Perform a single attack with flanking context.
    /// Includes full D&D 3.5 critical hit mechanics.
    /// </summary>
    public CombatResult Attack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName)
    {
        int totalAtkMod = Stats.AttackBonus + (isFlanking ? flankingBonus : 0);
        int critThreatMin = Stats.CritThreatMin > 0 ? Stats.CritThreatMin : 20;
        int critMult = Stats.CritMultiplier > 0 ? Stats.CritMultiplier : 2;

        var result = PerformSingleAttackWithCrit(target, totalAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            Stats.BaseDamageDice, Stats.BaseDamageCount, Stats.BonusDamage, 1.0f, critThreatMin, critMult);

        HasAttackedThisTurn = true;
        return result;
    }

    // ========== FULL ATTACK (Full-Round Action) ==========

    /// <summary>
    /// Perform a Full Attack action - all iterative attacks based on BAB.
    /// Each attack can independently threaten and confirm a critical hit.
    /// </summary>
    public FullAttackResult FullAttack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName)
    {
        var result = new FullAttackResult();
        result.Type = FullAttackResult.AttackType.FullAttack;
        result.Attacker = this;
        result.Defender = target;

        int[] attackBonuses = Stats.GetIterativeAttackBonuses();
        int critThreatMin = Stats.CritThreatMin > 0 ? Stats.CritThreatMin : 20;
        int critMult = Stats.CritMultiplier > 0 ? Stats.CritMultiplier : 2;

        for (int i = 0; i < attackBonuses.Length; i++)
        {
            if (target.Stats.IsDead) break;

            int atkMod = attackBonuses[i] + (isFlanking ? flankingBonus : 0);
            string label = (i == 0) ? $"Attack ({CharacterStats.FormatMod(attackBonuses[i])})" :
                $"Attack {i + 1} ({CharacterStats.FormatMod(attackBonuses[i])})";

            CombatResult atk = PerformSingleAttackWithCrit(target, atkMod, isFlanking, flankingBonus, flankingPartnerName,
                Stats.BaseDamageDice, Stats.BaseDamageCount, Stats.BonusDamage, 1.0f, critThreatMin, critMult);

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
    public FullAttackResult DualWieldAttack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName)
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

        // Main hand attack: BAB + STR + penalty + flanking, uses main weapon's crit stats
        int mainAtkMod = Stats.AttackBonus + mainPenalty + (isFlanking ? flankingBonus : 0);
        string mainLabel = $"Main Hand ({mainWeapon.Name}, {CharacterStats.FormatMod(Stats.AttackBonus + mainPenalty)})";

        int mainCritMin = mainWeapon.CritThreatMin > 0 ? mainWeapon.CritThreatMin : 20;
        int mainCritMult = mainWeapon.CritMultiplier > 0 ? mainWeapon.CritMultiplier : 2;

        CombatResult mainAtk = PerformSingleAttackWithCrit(target, mainAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            mainWeapon.DamageDice, mainWeapon.DamageCount, mainWeapon.BonusDamage, 1.0f, mainCritMin, mainCritMult);
        result.Attacks.Add(mainAtk);
        result.AttackLabels.Add(mainLabel);

        // Off-hand attack (only if target still alive)
        if (!target.Stats.IsDead)
        {
            int offAtkMod = Stats.AttackBonus + offPenalty + (isFlanking ? flankingBonus : 0);
            string offLabel = $"Off-Hand ({offWeapon.Name}, {CharacterStats.FormatMod(Stats.AttackBonus + offPenalty)})";

            int offCritMin = offWeapon.CritThreatMin > 0 ? offWeapon.CritThreatMin : 20;
            int offCritMult = offWeapon.CritMultiplier > 0 ? offWeapon.CritMultiplier : 2;

            CombatResult offAtk = PerformSingleAttackWithCrit(target, offAtkMod, isFlanking, flankingBonus, flankingPartnerName,
                offWeapon.DamageDice, offWeapon.DamageCount, offWeapon.BonusDamage, 0.5f, offCritMin, offCritMult);
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
    /// Step 1: Roll d20. Check if in threat range.
    /// Step 2: If threat, roll confirmation vs same AC with same bonus.
    /// Step 3: If confirmed, multiply weapon dice (not static bonuses or sneak attack).
    /// </summary>
    private CombatResult PerformSingleAttackWithCrit(CharacterController target, int totalAtkMod,
        bool isFlanking, int flankingBonus, string flankingPartnerName,
        int damageDice, int damageCount, int bonusDamage, float strMultiplier,
        int critThreatMin, int critMultiplier)
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
                // Critical damage: multiply weapon dice, add static bonuses once
                damage = Stats.RollCritDamage(damageDice, damageCount, bonusDamage, strMultiplier, critMultiplier);
                int strBonus = Mathf.FloorToInt(Stats.STRMod * strMultiplier);
                result.CritDamageDice = $"{damageCount * critMultiplier}d{damageDice}+{strBonus + bonusDamage}";
            }
            else
            {
                // Normal damage
                damage = Stats.RollDamageWithWeapon(damageDice, damageCount, bonusDamage, strMultiplier);
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

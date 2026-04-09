using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls a character on the hex grid (both PC and NPC).
/// Supports D&D 3.5 action economy, full attacks, and dual wielding.
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
    /// Returns a CombatResult with details.
    /// </summary>
    public CombatResult Attack(CharacterController target)
    {
        return Attack(target, false, 0, null);
    }

    /// <summary>
    /// Perform a single attack with flanking context.
    /// </summary>
    public CombatResult Attack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName)
    {
        var result = new CombatResult();
        result.Attacker = this;
        result.Defender = target;

        // Flanking info
        result.IsFlanking = isFlanking;
        result.FlankingBonus = isFlanking ? flankingBonus : 0;
        result.FlankingPartnerName = flankingPartnerName ?? "";

        // Roll to hit with flanking bonus included
        var (hit, roll, total) = Stats.RollToHitWithFlanking(target.Stats.ArmorClass, result.FlankingBonus);
        result.DieRoll = roll;
        result.TotalRoll = total;
        result.TargetAC = target.Stats.ArmorClass;
        result.Hit = hit;
        result.NaturalTwenty = (roll == 20);
        result.NaturalOne = (roll == 1);

        if (hit)
        {
            // Roll base weapon damage
            int damage = Stats.RollDamage();
            result.Damage = damage;

            // Sneak attack: applies if attacker is a Rogue and is flanking
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

        HasAttackedThisTurn = true;
        return result;
    }

    // ========== FULL ATTACK (Full-Round Action) ==========

    /// <summary>
    /// Perform a Full Attack action - all iterative attacks based on BAB.
    /// BAB +6 gets 2 attacks at +6/+1, BAB +11 gets 3 at +11/+6/+1, etc.
    /// This is a Full-Round Action.
    /// </summary>
    public FullAttackResult FullAttack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName)
    {
        var result = new FullAttackResult();
        result.Type = FullAttackResult.AttackType.FullAttack;
        result.Attacker = this;
        result.Defender = target;

        int[] attackBonuses = Stats.GetIterativeAttackBonuses();

        for (int i = 0; i < attackBonuses.Length; i++)
        {
            if (target.Stats.IsDead) break;

            int atkMod = attackBonuses[i] + (isFlanking ? flankingBonus : 0);
            string label = (i == 0) ? $"Attack ({CharacterStats.FormatMod(attackBonuses[i])})" :
                $"Attack {i + 1} ({CharacterStats.FormatMod(attackBonuses[i])})";

            CombatResult atk = PerformSingleAttackWithMod(target, atkMod, isFlanking, flankingBonus, flankingPartnerName,
                Stats.BaseDamageDice, Stats.BaseDamageCount, Stats.BonusDamage, 1.0f);

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
    /// </summary>
    public bool CanDualWield()
    {
        var inv = GetComponent<InventoryComponent>();
        if (inv == null || inv.CharacterInventory == null) return false;

        var leftItem = inv.CharacterInventory.LeftHandSlot;
        var rightItem = inv.CharacterInventory.RightHandSlot;

        return leftItem != null && leftItem.IsWeapon && rightItem != null && rightItem.IsWeapon;
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
    /// Full-Round Action. Main hand gets full STR to damage, off-hand gets half STR.
    /// Both can benefit from flanking and sneak attack.
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

        // Main hand attack: BAB + STR + penalty + flanking
        int mainAtkMod = Stats.AttackBonus + mainPenalty + (isFlanking ? flankingBonus : 0);
        string mainLabel = $"Main Hand ({mainWeapon.Name}, {CharacterStats.FormatMod(Stats.AttackBonus + mainPenalty)})";

        CombatResult mainAtk = PerformSingleAttackWithMod(target, mainAtkMod, isFlanking, flankingBonus, flankingPartnerName,
            mainWeapon.DamageDice, mainWeapon.DamageCount, mainWeapon.BonusDamage, 1.0f);
        result.Attacks.Add(mainAtk);
        result.AttackLabels.Add(mainLabel);

        // Off-hand attack (only if target still alive)
        if (!target.Stats.IsDead)
        {
            int offAtkMod = Stats.AttackBonus + offPenalty + (isFlanking ? flankingBonus : 0);
            string offLabel = $"Off-Hand ({offWeapon.Name}, {CharacterStats.FormatMod(Stats.AttackBonus + offPenalty)})";

            CombatResult offAtk = PerformSingleAttackWithMod(target, offAtkMod, isFlanking, flankingBonus, flankingPartnerName,
                offWeapon.DamageDice, offWeapon.DamageCount, offWeapon.BonusDamage, 0.5f); // Half STR for off-hand
            result.Attacks.Add(offAtk);
            result.AttackLabels.Add(offLabel);
        }

        result.TargetKilled = target.Stats.IsDead;
        HasAttackedThisTurn = true;
        return result;
    }

    // ========== INTERNAL: Single attack with specific modifier ==========

    /// <summary>
    /// Perform a single attack roll with a specific total attack modifier and weapon stats.
    /// Used internally by FullAttack and DualWieldAttack.
    /// </summary>
    private CombatResult PerformSingleAttackWithMod(CharacterController target, int totalAtkMod,
        bool isFlanking, int flankingBonus, string flankingPartnerName,
        int damageDice, int damageCount, int bonusDamage, float strMultiplier)
    {
        var result = new CombatResult();
        result.Attacker = this;
        result.Defender = target;
        result.IsFlanking = isFlanking;
        result.FlankingBonus = isFlanking ? flankingBonus : 0;
        result.FlankingPartnerName = flankingPartnerName ?? "";

        // Roll to hit
        var (hit, roll, total) = Stats.RollToHitWithMod(totalAtkMod, target.Stats.ArmorClass);
        result.DieRoll = roll;
        result.TotalRoll = total;
        result.TargetAC = target.Stats.ArmorClass;
        result.Hit = hit;
        result.NaturalTwenty = (roll == 20);
        result.NaturalOne = (roll == 1);

        if (hit)
        {
            // Roll weapon damage with appropriate STR multiplier
            int damage = Stats.RollDamageWithWeapon(damageDice, damageCount, bonusDamage, strMultiplier);
            result.Damage = damage;

            // Sneak attack applies to each attack if flanking and attacker is Rogue
            if (Stats.IsRogue && isFlanking)
            {
                int sneakDice = CombatUtils.GetSneakAttackDice(Stats.Level);
                int sneakDmg = CombatUtils.RollSneakAttackDamage(Stats.Level);
                result.SneakAttackApplied = true;
                result.SneakAttackDice = sneakDice;
                result.SneakAttackDamage = sneakDmg;
            }

            // Apply damage
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

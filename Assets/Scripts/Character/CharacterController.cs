using UnityEngine;
using System.Collections.Generic;

public enum SpecialAttackType
{
    Trip,
    Disarm,
    Grapple,
    Sunder,
    BullRush,
    Overrun,
    Feint
}
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
    [HideInInspector] public bool HasTakenFiveFootStep;
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

    /// <summary>
    /// D&D 3.5: Fighting Defensively stance.
    /// While active: -4 attack rolls, +2 dodge AC until start of next turn.
    /// </summary>
    public bool IsFightingDefensively { get; private set; }

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

    /// <summary>Toggle Fighting Defensively stance for this turn.</summary>
    public void SetFightingDefensively(bool enabled)
    {
        IsFightingDefensively = enabled;
        if (Stats != null)
        {
            Debug.Log($"[Defensive] {Stats.CharacterName}: Fighting Defensively {(enabled ? "ON" : "OFF")}");
        }
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

        // Battlefield status indicators (condition badges above token).
        if (GetComponent<StatusEffectIndicator>() == null)
            gameObject.AddComponent<StatusEffectIndicator>();
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
    /// <param name="targetCell">Destination grid cell.</param>
    /// <param name="markAsMoved">Whether this movement should count as normal movement for turn tracking.</param>
    public void MoveToCell(SquareCell targetCell, bool markAsMoved = true)
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

        if (markAsMoved)
            HasMovedThisTurn = true;
    }

    /// <summary>
    /// Returns a snapshot list of currently active combat conditions on this character.
    /// </summary>
    public List<StatusEffect> GetActiveConditions()
    {
        if (Stats == null || Stats.ActiveConditions == null)
            return new List<StatusEffect>();

        return new List<StatusEffect>(Stats.ActiveConditions);
    }

    /// <summary>
    /// True if this character currently has the specified combat condition.
    /// </summary>
    public bool HasCondition(CombatConditionType type)
    {
        return Stats != null
            && Stats.ActiveConditions != null
            && Stats.ActiveConditions.Exists(c => c.Type == type);
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
        if (!CanAttackWithWeapon(equippedWeapon, out string cannotAttackReason))
        {
            Debug.LogWarning($"[Combat] {Stats.CharacterName} cannot attack: {cannotAttackReason}");
            return new CombatResult
            {
                Attacker = this,
                Defender = target,
                WeaponName = equippedWeapon != null ? equippedWeapon.Name : "Unarmed",
                Hit = false,
                DieRoll = 0,
                TotalRoll = 0,
                TargetAC = target != null && target.Stats != null ? target.Stats.ArmorClass : 0,
                DefenderHPBefore = target != null && target.Stats != null ? target.Stats.CurrentHP : 0,
                DefenderHPAfter = target != null && target.Stats != null ? target.Stats.CurrentHP : 0
            };
        }

        if (!IsTargetInCurrentWeaponRange(target))
        {
            Debug.LogWarning($"[Combat] {Stats.CharacterName} cannot attack {target?.Stats?.CharacterName}: target out of weapon range.");
            return new CombatResult
            {
                Attacker = this,
                Defender = target,
                WeaponName = equippedWeapon != null ? equippedWeapon.Name : "Unarmed",
                Hit = false,
                DieRoll = 0,
                TotalRoll = 0,
                TargetAC = target != null && target.Stats != null ? target.Stats.ArmorClass : 0,
                DefenderHPBefore = target != null && target.Stats != null ? target.Stats.CurrentHP : 0,
                DefenderHPAfter = target != null && target.Stats != null ? target.Stats.CurrentHP : 0
            };
        }

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

        // Prone: melee attacks take -4.
        int proneAttackPenalty = GetProneAttackModifier(isMelee);

        // Fighting Defensively: -4 attack rolls while stance is active.
        int fightingDefensivelyPenalty = IsFightingDefensively ? -4 : 0;

        // Shooting into melee: -4 for ranged attacks against targets engaged with attacker allies.
        bool preciseShotNegated = false;
        int shootingIntoMeleePenalty = GetShootingIntoMeleePenalty(this, target, isRanged, out preciseShotNegated);

        int totalAtkMod = Stats.BaseAttackBonus + abilityMod + Stats.SizeModifier
                          + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                          + powerAtkPenalty + pbsAtkBonus + weaponFocusBonus + combatExpertisePenalty
                          + proneAttackPenalty + fightingDefensivelyPenalty + shootingIntoMeleePenalty;

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
        result.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
        result.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
        result.PreciseShotNegated = preciseShotNegated;
        result.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;

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

        if (equippedWeapon != null && equippedWeapon.RequiresReload)
        {
            string reloadStateMessage = OnWeaponFired(equippedWeapon);
            if (!string.IsNullOrEmpty(reloadStateMessage))
                Debug.Log($"[Reload] {Stats.CharacterName}: {reloadStateMessage}");
        }

        HasAttackedThisTurn = true;
        return result;
    }

    /// <summary>
    /// Returns how many attacks this character can make during a full attack right now,
    /// including Rapid Shot when active with a ranged weapon.
    /// </summary>
    public int GetPlannedFullAttackCount(RangeInfo rangeInfo = null)
    {
        int[] attackBonuses = Stats.GetIterativeAttackBonuses();
        int count = attackBonuses != null ? attackBonuses.Length : 0;

        ItemData equippedWeapon = GetEquippedMainWeapon();
        bool isRanged = (equippedWeapon != null && (equippedWeapon.WeaponCat == WeaponCategory.Ranged || equippedWeapon.RangeIncrement > 0))
                        && rangeInfo != null && !rangeInfo.IsMelee;

        bool hasRapidShotFeat = Stats.HasFeat("Rapid Shot");
        bool rapidShotActive = isRanged && hasRapidShotFeat && RapidShotEnabled;
        if (rapidShotActive)
            count += 1;

        return Mathf.Max(0, count);
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
    public FullAttackResult FullAttack(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null, int startAttackIndex = 0, int maxAttacks = int.MaxValue)
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
        if (!CanAttackWithWeapon(equippedWeapon, out string cannotAttackReason))
        {
            Debug.LogWarning($"[FullAttack] {Stats.CharacterName}: {cannotAttackReason}");
            result.DefenderHPAfter = target.Stats.CurrentHP;
            result.TargetKilled = target.Stats.IsDead;
            return result;
        }

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

        // Prone: melee attacks take -4.
        int proneAttackPenalty = GetProneAttackModifier(isMelee);

        // === FEAT: Improved Critical ===
        critThreatMin = FeatManager.GetAdjustedCritThreatMin(Stats, critThreatMin);

        // === FEAT: Rapid Shot (ranged, full attack only) ===
        bool hasRapidShotFeat = Stats.HasFeat("Rapid Shot");
        bool rapidShotActive = isRanged && hasRapidShotFeat && RapidShotEnabled;
        int rapidShotPenalty = rapidShotActive ? -2 : 0;

        // Fighting Defensively: -4 attack while active.
        int fightingDefensivelyPenalty = IsFightingDefensively ? -4 : 0;

        // Shooting into melee: -4 unless Precise Shot negates it.
        bool preciseShotNegated = false;
        int shootingIntoMeleePenalty = GetShootingIntoMeleePenalty(this, target, isRanged, out preciseShotNegated);

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

        if (startAttackIndex < 0)
            startAttackIndex = 0;

        int attacksExecuted = 0;
        for (int i = startAttackIndex; i < allAttackBonuses.Count; i++)
        {
            if (attacksExecuted >= maxAttacks)
                break;

            if (target.Stats.IsDead)
            {
                Debug.Log($"[FullAttack] Target is dead, stopping at attack {i + 1}");
                break;
            }

            int baseBonus = allAttackBonuses[i];
            int atkMod = baseBonus + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                         + powerAtkPenalty + pbsAtkBonus + weaponFocusBonus + combatExpertisePenalty
                         + rapidShotPenalty + proneAttackPenalty + fightingDefensivelyPenalty + shootingIntoMeleePenalty;

            // Recalculate with correct ability mod (iterative uses BAB-based bonus, not STRMod again)
            // The baseBonus from GetIterativeAttackBonuses already includes STRMod+SizeModifier
            // So we need to add other feat/situational modifiers
            atkMod = baseBonus + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                     + powerAtkPenalty + pbsAtkBonus + weaponFocusBonus + combatExpertisePenalty
                     + rapidShotPenalty + proneAttackPenalty + fightingDefensivelyPenalty + shootingIntoMeleePenalty;
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
            atk.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
            atk.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
            atk.PreciseShotNegated = preciseShotNegated;
            atk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;

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
            attacksExecuted++;
        }

        result.DefenderHPAfter = target.Stats.CurrentHP;
        result.TargetKilled = target.Stats.IsDead;

        if (equippedWeapon != null && equippedWeapon.RequiresReload && result.Attacks.Count > 0)
        {
            string reloadStateMessage = OnWeaponFired(equippedWeapon);
            if (!string.IsNullOrEmpty(reloadStateMessage))
                Debug.Log($"[Reload] {Stats.CharacterName}: {reloadStateMessage}");
        }

        HasAttackedThisTurn = result.Attacks.Count > 0;
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

        bool canMainAttack = CanAttackWithWeapon(mainWeapon, out string mainBlockedReason);
        bool canOffAttack = CanAttackWithWeapon(offWeapon, out string offBlockedReason);

        result.MainWeaponName = mainWeapon.Name;
        result.OffWeaponName = offWeapon.Name;

        var (mainPenalty, offPenalty, lightOff) = GetDualWieldPenalties();

        int racialAtkBonus = Stats.GetRacialAttackBonus(target.Stats);
        int rangePenalty = (rangeInfo != null && !rangeInfo.IsMelee && rangeInfo.IsInRange) ? rangeInfo.Penalty : 0;

        bool isRanged = rangeInfo != null && !rangeInfo.IsMelee;
        bool isMelee = !isRanged;

        int powerAtkPenalty = 0;
        int powerAtkDmgBonus = 0;
        if (isMelee && Stats.HasFeat("Power Attack") && PowerAttackValue > 0)
        {
            powerAtkPenalty = -PowerAttackValue;
            powerAtkDmgBonus = PowerAttackValue; // one-handed while dual-wielding
        }

        bool pointBlankActive = false;
        int pbsAtkBonus = 0;
        int pbsDmgBonus = 0;
        if (isRanged && Stats.HasFeat("Point Blank Shot") && rangeInfo != null && rangeInfo.DistanceFeet <= 30)
        {
            pointBlankActive = true;
            pbsAtkBonus = 1;
            pbsDmgBonus = 1;
        }

        int mainWFBonus = FeatManager.GetWeaponFocusBonus(Stats, mainWeapon?.Name ?? "Unarmed");
        int offWFBonus = FeatManager.GetWeaponFocusBonus(Stats, offWeapon?.Name ?? "Unarmed");
        int mainWSBonus = FeatManager.GetWeaponSpecializationBonus(Stats, mainWeapon?.Name ?? "Unarmed");
        int offWSBonus = FeatManager.GetWeaponSpecializationBonus(Stats, offWeapon?.Name ?? "Unarmed");

        int finesseAtkAdjust = 0;
        string abilityName = isRanged ? "DEX" : "STR";
        int abilityMod = isRanged ? Stats.DEXMod : Stats.STRMod;
        if (isMelee && FeatManager.ShouldUseWeaponFinesse(Stats, mainWeapon))
        {
            finesseAtkAdjust = Stats.DEXMod - Stats.STRMod;
            abilityName = "DEX";
            abilityMod = Stats.DEXMod;
        }

        int combatExpertisePenalty = 0;
        if (isMelee && Stats.HasFeat("Combat Expertise") && Stats.CombatExpertiseValue > 0)
        {
            int maxCE = FeatManager.GetMaxCombatExpertise(Stats);
            combatExpertisePenalty = -Mathf.Min(Stats.CombatExpertiseValue, maxCE);
        }

        int proneAttackPenalty = GetProneAttackModifier(isMelee);
        int fightingDefensivelyPenalty = IsFightingDefensively ? -4 : 0;
        bool preciseShotNegated = false;
        int shootingIntoMeleePenalty = GetShootingIntoMeleePenalty(this, target, isRanged, out preciseShotNegated);

        // Main-hand attack
        if (canMainAttack)
        {
            int mainAtkMod = Stats.AttackBonus + mainPenalty + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                             + powerAtkPenalty + pbsAtkBonus + mainWFBonus + finesseAtkAdjust + combatExpertisePenalty
                             + proneAttackPenalty + fightingDefensivelyPenalty + shootingIntoMeleePenalty;
            string mainLabel = $"Attack 1 - Main Hand ({mainWeapon.Name})";

            int mainCritMin = FeatManager.GetAdjustedCritThreatMin(Stats, mainWeapon.CritThreatMin > 0 ? mainWeapon.CritThreatMin : 20);
            int mainCritMult = mainWeapon.CritMultiplier > 0 ? mainWeapon.CritMultiplier : 2;
            int totalMainFeatDmg = powerAtkDmgBonus + pbsDmgBonus + mainWSBonus;

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
            mainAtk.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
            mainAtk.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
            mainAtk.PreciseShotNegated = preciseShotNegated;
            mainAtk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;
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

            if (mainWeapon.RequiresReload)
            {
                string reloadStateMessage = OnWeaponFired(mainWeapon);
                if (!string.IsNullOrEmpty(reloadStateMessage))
                    Debug.Log($"[Reload] {Stats.CharacterName}: {reloadStateMessage}");
            }
        }
        else
        {
            Debug.LogWarning($"[DualWield] {Stats.CharacterName} main-hand attack skipped: {mainBlockedReason}");
        }

        // Off-hand attack
        if (!target.Stats.IsDead && canOffAttack)
        {
            int offAtkMod = Stats.AttackBonus + offPenalty + (isFlanking ? flankingBonus : 0) + racialAtkBonus + rangePenalty
                            + powerAtkPenalty + pbsAtkBonus + offWFBonus + finesseAtkAdjust + combatExpertisePenalty
                            + proneAttackPenalty + fightingDefensivelyPenalty + shootingIntoMeleePenalty;
            string offLabel = $"Attack 2 - Off Hand ({offWeapon.Name})";

            int offCritMin = FeatManager.GetAdjustedCritThreatMin(Stats, offWeapon.CritThreatMin > 0 ? offWeapon.CritThreatMin : 20);
            int offCritMult = offWeapon.CritMultiplier > 0 ? offWeapon.CritMultiplier : 2;
            int totalOffFeatDmg = powerAtkDmgBonus + pbsDmgBonus + offWSBonus;

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
            offAtk.FightingDefensivelyAttackPenalty = fightingDefensivelyPenalty;
            offAtk.ShootingIntoMeleePenalty = shootingIntoMeleePenalty;
            offAtk.PreciseShotNegated = preciseShotNegated;
            offAtk.FightingDefensivelyACBonus = target != null && target.IsFightingDefensively ? 2 : 0;
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

            if (offWeapon.RequiresReload)
            {
                string reloadStateMessage = OnWeaponFired(offWeapon);
                if (!string.IsNullOrEmpty(reloadStateMessage))
                    Debug.Log($"[Reload] {Stats.CharacterName}: {reloadStateMessage}");
            }
        }
        else if (!canOffAttack)
        {
            Debug.LogWarning($"[DualWield] {Stats.CharacterName} off-hand attack skipped: {offBlockedReason}");
        }

        result.DefenderHPAfter = target.Stats.CurrentHP;
        result.TargetKilled = target.Stats.IsDead;
        HasAttackedThisTurn = result.Attacks.Count > 0;
        return result;
    }

    private int GetProneAttackModifier(bool isMeleeAttack)
    {
        if (!HasCondition(CombatConditionType.Prone)) return 0;
        return isMeleeAttack ? -4 : 0;
    }

    private static List<CharacterController> GetAllCombatCharactersSnapshot()
    {
        var all = new List<CharacterController>();
        var gm = GameManager.Instance;
        if (gm == null) return all;

        if (gm.PCs != null)
        {
            for (int i = 0; i < gm.PCs.Count; i++)
            {
                var pc = gm.PCs[i];
                if (pc != null) all.Add(pc);
            }
        }

        if (gm.NPCs != null)
        {
            for (int i = 0; i < gm.NPCs.Count; i++)
            {
                var npc = gm.NPCs[i];
                if (npc != null) all.Add(npc);
            }
        }

        return all;
    }

    private static bool IsTargetEngagedInMeleeWithAttackerAllies(CharacterController target, CharacterController attacker)
    {
        if (target == null || attacker == null || target.Stats == null || attacker.Stats == null) return false;

        // Use threat map: target is engaged in melee if a threatening enemy of target
        // (excluding attacker) is on the attacker's team.
        List<CharacterController> all = GetAllCombatCharactersSnapshot();
        if (all.Count == 0) return false;

        List<CharacterController> threateningEnemies = ThreatSystem.GetThreateningEnemies(target.GridPosition, target, all);
        for (int i = 0; i < threateningEnemies.Count; i++)
        {
            CharacterController threatener = threateningEnemies[i];
            if (threatener == null || threatener == attacker) continue;
            if (threatener.Stats == null || threatener.Stats.IsDead) continue;

            if (threatener.IsPlayerControlled == attacker.IsPlayerControlled)
                return true;
        }

        return false;
    }

    private static int GetShootingIntoMeleePenalty(CharacterController attacker, CharacterController target, bool isRangedAttack, out bool preciseShotNegated)
    {
        preciseShotNegated = false;

        if (!isRangedAttack || attacker == null || target == null)
            return 0;

        bool engaged = IsTargetEngagedInMeleeWithAttackerAllies(target, attacker);
        if (!engaged)
            return 0;

        if (attacker.Stats != null && attacker.Stats.HasFeat("Precise Shot"))
        {
            preciseShotNegated = true;
            return 0;
        }

        return -4;
    }

    private static int GetSituationalTargetArmorClass(CharacterController target, bool isRangedAttack)
    {
        if (target == null || target.Stats == null)
            return 10;

        int targetAC = target.Stats.ArmorClass;
        if (target.HasCondition(CombatConditionType.Prone))
            targetAC += isRangedAttack ? 4 : -4;

        if (target.IsFightingDefensively)
            targetAC += 2; // Dodge bonus

        return targetAC;
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

        bool isRangedAttack = weapon != null && (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0);
        int targetAC = GetSituationalTargetArmorClass(target, isRangedAttack);

        // Step 1: Roll to hit
        var (hit, roll, total) = Stats.RollToHitWithMod(totalAtkMod, targetAC);
        result.DieRoll = roll;
        result.TotalRoll = total;
        result.TargetAC = targetAC;
        result.Hit = hit;
        result.IsRangedAttack = isRangedAttack;
        result.NaturalTwenty = (roll == 20);
        result.NaturalOne = (roll == 1);

        // Step 2: Check for critical threat (only if the attack hit)
        bool isThreat = false;
        bool critConfirmed = false;
        int confirmRoll = 0;
        int confirmTotal = 0;

        if (hit)
        {
            bool whipArmorBlocked = IsTargetImmuneToWhipDamage(target, weapon);
            if (whipArmorBlocked)
            {
                result.IsCritThreat = false;
                result.CritConfirmed = false;
                result.Damage = 0;
                result.BaseDamageRoll = 0;
                result.RawTotalDamage = 0;
                result.FinalDamageDealt = 0;
                result.ImmunityPrevented = true;
                result.MitigationSummary = "Whip cannot harm targets with armor/natural armor bonus +1 or higher.";
                result.DamageTypeSummary = "nonlethal slashing";
                return result;
            }

            isThreat = CharacterStats.IsCritThreat(roll, critThreatMin);
            result.IsCritThreat = isThreat;

            if (isThreat)
            {
                // Roll confirmation with the same attack modifier
                var (confirmed, confRoll, confTotal) = Stats.RollCritConfirmation(totalAtkMod, targetAC);
                critConfirmed = confirmed;
                confirmRoll = confRoll;
                confirmTotal = confTotal;
                result.CritConfirmed = critConfirmed;
                result.ConfirmationRoll = confirmRoll;
                result.ConfirmationTotal = confirmTotal;
            }

            // Step 3: Roll weapon damage (feat bonus added as flat bonus, not multiplied on crit)
            int rawWeaponDamage;
            int baseDmgRoll;
            if (critConfirmed)
            {
                // Critical damage: multiply weapon dice, add static bonuses (STR + bonus) once
                int totalCritDice = damageCount * critMultiplier;
                baseDmgRoll = Stats.RollBaseDamage(damageDice, totalCritDice);
                rawWeaponDamage = baseDmgRoll + damageModifier + bonusDamage;
                rawWeaponDamage += featDamageBonus; // Feat bonus added after crit multiplication
                result.CritDamageDice = $"{totalCritDice}d{damageDice}+{damageModifier + bonusDamage}";
            }
            else
            {
                // Normal damage - roll weapon dice separately for breakdown
                baseDmgRoll = Stats.RollBaseDamage(damageDice, damageCount);
                rawWeaponDamage = baseDmgRoll + damageModifier + bonusDamage + featDamageBonus;
            }
            rawWeaponDamage = Mathf.Max(1, rawWeaponDamage); // Weapon hit always deals at least 1 before mitigation
            result.Damage = rawWeaponDamage;
            result.BaseDamageRoll = baseDmgRoll;

            // Sneak attack: applies if attacker is Rogue and is flanking
            // Sneak attack is NOT multiplied on critical hits (D&D 3.5 rule)
            int rawSneakDamage = 0;
            if (Stats.IsRogue && isFlanking)
            {
                int sneakDice = CombatUtils.GetSneakAttackDice(Stats.Level);
                rawSneakDamage = CombatUtils.RollSneakAttackDamage(Stats.Level);
                result.SneakAttackApplied = true;
                result.SneakAttackDice = sneakDice;
                result.SneakAttackDamage = rawSneakDamage;
            }

            int rawTotalDamage = rawWeaponDamage + rawSneakDamage;
            result.RawTotalDamage = rawTotalDamage;

            // Build damage packet for DR/resistance/immunity resolution
            var damageTypes = weapon != null
                ? weapon.GetDamageTypes()
                : new System.Collections.Generic.HashSet<DamageType> { DamageType.Bludgeoning };

            DamageBypassTag attackTags = weapon != null ? weapon.GetBypassTags() : DamageBypassTag.Bludgeoning;
            if (weapon != null && (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0))
                attackTags |= DamageBypassTag.Ranged;

            var packet = new DamagePacket
            {
                RawDamage = rawTotalDamage,
                Types = damageTypes,
                AttackTags = attackTags,
                IsRanged = result.IsRangedAttack,
                Source = AttackSource.Weapon,
                SourceName = string.IsNullOrEmpty(result.WeaponName) ? "attack" : result.WeaponName,
            };

            DamageResolutionResult mitigation = target.Stats.ApplyIncomingDamage(rawTotalDamage, packet);
            result.FinalDamageDealt = mitigation.FinalDamage;
            result.ResistancePrevented = mitigation.ResistanceApplied;
            result.DRPrevented = mitigation.DamageReductionApplied;
            result.ImmunityPrevented = mitigation.ImmunityTriggered;
            result.MitigationSummary = mitigation.GetMitigationSummary();
            result.DamageTypeSummary = DamageTextUtils.FormatDamageTypes(damageTypes);

            if (weapon != null && weapon.DealsNonlethalDamage)
            {
                result.DamageTypeSummary = string.IsNullOrEmpty(result.DamageTypeSummary)
                    ? "nonlethal"
                    : $"{result.DamageTypeSummary}, nonlethal";

                if (string.IsNullOrEmpty(result.MitigationSummary))
                    result.MitigationSummary = "Nonlethal damage is tracked as normal HP damage in this prototype.";
            }

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
    /// Returns true if the provided weapon (or current main weapon) is a reload-based crossbow and currently unloaded.
    /// </summary>
    public bool IsCrossbowUnloaded(ItemData weapon = null)
    {
        weapon ??= GetEquippedMainWeapon();
        return weapon != null && weapon.RequiresReload && !weapon.IsLoaded;
    }

    /// <summary>
    /// Check whether this character can currently attack with the equipped main weapon.
    /// </summary>
    public bool CanAttackWithEquippedWeapon(out string reason)
    {
        return CanAttackWithWeapon(GetEquippedMainWeapon(), out reason);
    }

    /// <summary>
    /// Check whether this character can currently attack with the specified weapon.
    /// </summary>
    public bool CanAttackWithWeapon(ItemData weapon, out string reason)
    {
        reason = string.Empty;
        if (weapon == null) return true;

        if (weapon.RequiresReload && !weapon.IsLoaded)
        {
            reason = $"{weapon.Name} is unloaded and must be reloaded.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true if the character has the matching Rapid Reload feat for the given crossbow.
    /// </summary>
    public bool HasRapidReloadForWeapon(ItemData weapon)
    {
        if (Stats == null || weapon == null || !weapon.RequiresReload) return false;

        string featName = weapon.GetRapidReloadFeatName();
        if (string.IsNullOrEmpty(featName)) return false;
        return Stats.HasFeat(featName);
    }

    /// <summary>
    /// Get effective reload action for a weapon after applying Rapid Reload, if present.
    /// </summary>
    public ReloadActionType GetEffectiveReloadAction(ItemData weapon)
    {
        if (weapon == null || !weapon.RequiresReload) return ReloadActionType.None;
        bool hasRapidReload = HasRapidReloadForWeapon(weapon);
        return weapon.GetEffectiveReloadAction(hasRapidReload);
    }

    /// <summary>
    /// Mark weapon as fired and apply automatic free-action reload if available.
    /// Returns a short status string for combat log purposes.
    /// </summary>
    public string OnWeaponFired(ItemData weapon)
    {
        if (weapon == null || !weapon.RequiresReload)
            return string.Empty;

        weapon.IsLoaded = false;

        ReloadActionType effectiveReload = GetEffectiveReloadAction(weapon);
        if (effectiveReload == ReloadActionType.FreeAction)
        {
            weapon.IsLoaded = true;
            return $"{weapon.Name} is reloaded (free action via Rapid Reload).";
        }

        return $"{weapon.Name} is now unloaded and must be reloaded.";
    }

    /// <summary>
    /// Reload weapon state immediately (action-economy checks are handled by GameManager/UI).
    /// </summary>
    public bool ReloadWeapon(ItemData weapon)
    {
        if (weapon == null || !weapon.RequiresReload) return false;
        if (weapon.IsLoaded) return false;
        weapon.IsLoaded = true;
        return true;
    }

    /// <summary>
    /// Human-readable label for reload action type.
    /// </summary>
    public static string GetReloadActionLabel(ReloadActionType action)
    {
        switch (action)
        {
            case ReloadActionType.FreeAction: return "Free";
            case ReloadActionType.MoveAction: return "Move";
            case ReloadActionType.FullRound: return "Full-round";
            default: return "None";
        }
    }

    /// <summary>
    /// Build a short weapon load-state label for UI.
    /// </summary>
    public string GetWeaponLoadStateLabel(ItemData weapon = null)
    {
        weapon ??= GetEquippedMainWeapon();
        if (weapon == null || !weapon.RequiresReload) return string.Empty;

        string state = weapon.IsLoaded ? "LOADED" : "UNLOADED";
        ReloadActionType action = GetEffectiveReloadAction(weapon);
        string actionLabel = GetReloadActionLabel(action);
        return $"{weapon.Name}: {state} (Reload: {actionLabel})";
    }
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
    /// Check if the character has a melee weapon equipped (or is unarmed, which counts as melee).
    /// D&D 3.5 Rule: Only characters with melee weapons (including natural/unarmed) threaten squares.
    /// Ranged-only characters do NOT threaten any squares and cannot make Attacks of Opportunity.
    /// </summary>
    public bool HasMeleeWeaponEquipped()
    {
        ItemData weapon = GetEquippedMainWeapon();

        // Unarmed counts as melee — unarmed strikes threaten in D&D 3.5
        // (characters always have at least an unarmed strike available)
        if (weapon == null) return true;

        // Explicitly check weapon category
        if (weapon.WeaponCat == WeaponCategory.Melee) return true;

        // Ranged weapons do NOT grant melee threat
        // Even thrown weapons (javelin, dagger) use ranged rules when equipped as primary
        if (weapon.WeaponCat == WeaponCategory.Ranged) return false;

        // Fallback: if weapon has no category set, check AttackRange
        // AttackRange 1 = melee, >1 with RangeIncrement = ranged
        if (weapon.RangeIncrement > 0) return false;

        return true; // Default to melee if unclear
    }


    /// <summary>
    /// Get minimum melee distance (in squares) this character can attack with current weapon.
    /// Most melee weapons: 1. Reach-only weapons: 2. Whip: 2 (with max 3).
    /// </summary>
    public int GetMeleeMinAttackDistance(ItemData weapon = null)
    {
        weapon ??= GetEquippedMainWeapon();

        // Unarmed attack is always adjacent.
        if (weapon == null || weapon.WeaponCat != WeaponCategory.Melee)
            return 1;

        bool canAttackAdjacent = weapon.CanAttackAdjacent;
        int maxReach = GetMeleeMaxAttackDistance(weapon);

        if (canAttackAdjacent) return 1;
        return maxReach >= 2 ? 2 : 1;
    }

    /// <summary>
    /// Get maximum melee distance (in squares) this character can attack with current weapon.
    /// </summary>
    public int GetMeleeMaxAttackDistance(ItemData weapon = null)
    {
        weapon ??= GetEquippedMainWeapon();

        if (weapon == null || weapon.WeaponCat != WeaponCategory.Melee)
            return 1;

        int reach = weapon.ReachSquares > 0 ? weapon.ReachSquares : weapon.AttackRange;
        return Mathf.Max(1, reach);
    }

    /// <summary>
    /// Returns true if the specified square distance is legal for this character's current melee weapon.
    /// </summary>
    public bool CanMeleeAttackDistance(int squareDistance, ItemData weapon = null)
    {
        if (squareDistance <= 0) return false;
        int minDist = GetMeleeMinAttackDistance(weapon);
        int maxDist = GetMeleeMaxAttackDistance(weapon);
        return squareDistance >= minDist && squareDistance <= maxDist;
    }

    /// <summary>
    /// Check if this target is in range of the currently equipped weapon.
    /// </summary>
    public bool IsTargetInCurrentWeaponRange(CharacterController target)
    {
        if (target == null || target.Stats == null || Stats == null) return false;

        ItemData weapon = GetEquippedMainWeapon();
        int distance = SquareGridUtils.GetDistance(GridPosition, target.GridPosition);

        bool isRanged = weapon != null && (weapon.WeaponCat == WeaponCategory.Ranged || weapon.RangeIncrement > 0);
        if (isRanged)
        {
            int rangeIncrement = weapon.RangeIncrement;
            bool isThrownWeapon = weapon.IsThrown;

            if (rangeIncrement > 0)
            {
                int distFeet = RangeCalculator.SquaresToFeet(distance);
                return RangeCalculator.IsWithinMaxRange(distFeet, rangeIncrement, isThrownWeapon);
            }

            return distance <= Mathf.Max(1, Stats.AttackRange);
        }

        return CanMeleeAttackDistance(distance, weapon);
    }

    /// <summary>
    /// D&D 3.5 whip rule: standard whip cannot harm creatures with armor bonus +1 or natural armor +1.
    /// In this prototype, ArmorBonus is used as the tracked armor/natural armor bucket.
    /// </summary>
    public bool IsTargetImmuneToWhipDamage(CharacterController target, ItemData weapon)
    {
        if (target == null || target.Stats == null || weapon == null) return false;
        if (!weapon.WhipLikeArmorRestriction) return false;

        int armorLikeBonus = Mathf.Max(0, target.Stats.ArmorBonus);
        return armorLikeBonus >= 1;
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

        // Break concentration on death (D&D 3.5e: concentration ends if killed/unconscious)
        var concMgr = GetComponent<ConcentrationManager>();
        if (concMgr != null && concMgr.IsConcentrating)
        {
            concMgr.OnCharacterIncapacitated();
        }

        // Held touch charges are lost if the caster dies/unconscious.
        var spellComp = GetComponent<SpellcastingComponent>();
        if (spellComp != null && spellComp.HasHeldTouchCharge)
        {
            spellComp.ClearHeldTouchCharge("caster incapacitated");
        }
    }

    /// <summary>
    /// Reset turn flags and action economy.
    /// Power Attack and Rapid Shot settings persist between turns (player choice).
    /// Also resets Attacks of Opportunity counters for the new round.
    /// </summary>
    public void StartNewTurn()
    {
        HasMovedThisTurn = false;
        HasTakenFiveFootStep = false;
        HasAttackedThisTurn = false;
        IsFightingDefensively = false; // lasts until start of this character's next turn
        Actions.Reset();
        // Note: PowerAttackValue and RapidShotEnabled persist between turns
        // They are player-controlled and reset only when the player changes them

        // Reset AoO counters for the new round
        ThreatSystem.ResetAoOForTurn(this);
    }

    // ========== SPECIAL ATTACK MANEUVERS ==========

    public SpecialAttackResult ExecuteSpecialAttack(SpecialAttackType type, CharacterController target)
    {
        if (target == null || target.Stats == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = type.ToString(),
                Success = false,
                Log = $"{Stats.CharacterName} cannot perform {type}: no valid target."
            };
        }

        switch (type)
        {
            case SpecialAttackType.Trip: return ResolveTrip(target);
            case SpecialAttackType.Disarm: return ResolveDisarm(target);
            case SpecialAttackType.Grapple: return ResolveGrapple(target);
            case SpecialAttackType.Sunder: return ResolveSunder(target);
            case SpecialAttackType.BullRush: return ResolveBullRush(target);
            case SpecialAttackType.Overrun: return ResolveOverrun(target);
            case SpecialAttackType.Feint: return ResolveFeint(target);
            default:
                return new SpecialAttackResult
                {
                    ManeuverName = type.ToString(),
                    Success = false,
                    Log = $"{Stats.CharacterName} tries an unknown maneuver."
                };
        }
    }

    private SpecialAttackResult ResolveTrip(CharacterController target)
    {
        int atkRoll = Random.Range(1, 21);
        int defRoll = Random.Range(1, 21);
        int atkTotal = atkRoll + Stats.BaseAttackBonus + Stats.STRMod + Stats.SizeModifier + (Stats.HasFeat("Improved Trip") ? 4 : 0);
        int defAbility = Mathf.Max(target.Stats.STRMod, target.Stats.DEXMod);
        int defTotal = defRoll + target.Stats.BaseAttackBonus + defAbility + target.Stats.SizeModifier + (target.Stats.HasFeat("Improved Trip") ? 4 : 0);

        bool success = atkTotal >= defTotal;
        if (success)
            target.Stats.ApplyCondition(CombatConditionType.Prone, -1, Stats.CharacterName);

        return new SpecialAttackResult
        {
            ManeuverName = "Trip",
            Success = success,
            CheckRoll = atkRoll,
            CheckTotal = atkTotal,
            OpposedRoll = defRoll,
            OpposedTotal = defTotal,
            Log = success
                ? $"{Stats.CharacterName} trips {target.Stats.CharacterName}! ({atkTotal} vs {defTotal}) → PRONE (until standing)."
                : $"{Stats.CharacterName} fails to trip {target.Stats.CharacterName}. ({atkTotal} vs {defTotal})"
        };
    }

    private SpecialAttackResult ResolveDisarm(CharacterController target)
    {
        ItemData targetWeapon = target.GetEquippedMainWeapon();
        if (targetWeapon == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Disarm",
                Success = false,
                Log = $"{target.Stats.CharacterName} has no weapon to disarm."
            };
        }

        int atkWeaponMod = GetDisarmWeaponModifier(this);
        int defWeaponMod = GetDisarmWeaponModifier(target);

        int atkRoll = Random.Range(1, 21);
        int defRoll = Random.Range(1, 21);
        int atkTotal = atkRoll + Stats.BaseAttackBonus + Stats.STRMod + Stats.SizeModifier + atkWeaponMod + (Stats.HasFeat("Improved Disarm") ? 4 : 0);
        int defTotal = defRoll + target.Stats.BaseAttackBonus + target.Stats.STRMod + target.Stats.SizeModifier + defWeaponMod + (target.Stats.HasFeat("Improved Disarm") ? 4 : 0);

        bool success = atkTotal >= defTotal;
        if (success)
        {
            DestroyEquippedMainWeapon(target);
            target.Stats.ApplyCondition(CombatConditionType.Disarmed, 2, Stats.CharacterName);
        }

        return new SpecialAttackResult
        {
            ManeuverName = "Disarm",
            Success = success,
            CheckRoll = atkRoll,
            CheckTotal = atkTotal,
            OpposedRoll = defRoll,
            OpposedTotal = defTotal,
            Log = success
                ? $"{Stats.CharacterName} disarms {target.Stats.CharacterName}! ({atkTotal} vs {defTotal}) {targetWeapon.Name} knocked away."
                : $"{Stats.CharacterName} fails to disarm {target.Stats.CharacterName}. ({atkTotal} vs {defTotal})"
        };
    }

    private SpecialAttackResult ResolveGrapple(CharacterController target)
    {
        int touchRoll = Random.Range(1, 21);
        int touchTotal = touchRoll + Stats.BaseAttackBonus + Stats.STRMod + Stats.SizeModifier;
        int touchAC = 10 + target.Stats.DEXMod + target.Stats.SizeModifier;
        if (touchTotal < touchAC)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Grapple",
                Success = false,
                CheckRoll = touchRoll,
                CheckTotal = touchTotal,
                OpposedTotal = touchAC,
                Log = $"{Stats.CharacterName} misses grapple touch attack ({touchTotal} vs touch AC {touchAC})."
            };
        }

        int atkRoll = Random.Range(1, 21);
        int defRoll = Random.Range(1, 21);
        int atkTotal = atkRoll + GetGrappleModifier();
        int defTotal = defRoll + target.GetGrappleModifier();

        bool success = atkTotal >= defTotal;
        if (success)
        {
            Stats.ApplyCondition(CombatConditionType.Grappled, 2, target.Stats.CharacterName);
            target.Stats.ApplyCondition(CombatConditionType.Grappled, 2, Stats.CharacterName);
        }

        return new SpecialAttackResult
        {
            ManeuverName = "Grapple",
            Success = success,
            CheckRoll = atkRoll,
            CheckTotal = atkTotal,
            OpposedRoll = defRoll,
            OpposedTotal = defTotal,
            Log = success
                ? $"{Stats.CharacterName} grapples {target.Stats.CharacterName}! ({atkTotal} vs {defTotal}) Both are GRAPPLED (2 rounds)."
                : $"{Stats.CharacterName} fails to secure grapple on {target.Stats.CharacterName}. ({atkTotal} vs {defTotal})"
        };
    }

    private SpecialAttackResult ResolveSunder(CharacterController target)
    {
        ItemData targetWeapon = target.GetEquippedMainWeapon();
        if (targetWeapon == null)
        {
            return new SpecialAttackResult
            {
                ManeuverName = "Sunder",
                Success = false,
                Log = $"{target.Stats.CharacterName} has no weapon to sunder."
            };
        }

        int atkRoll = Random.Range(1, 21);
        int defRoll = Random.Range(1, 21);
        int atkTotal = atkRoll + Stats.BaseAttackBonus + Stats.STRMod + Stats.SizeModifier + GetDisarmWeaponModifier(this) + (Stats.HasFeat("Improved Sunder") ? 4 : 0);
        int defTotal = defRoll + target.Stats.BaseAttackBonus + target.Stats.STRMod + target.Stats.SizeModifier + GetDisarmWeaponModifier(target);

        bool success = atkTotal >= defTotal;
        int damage = 0;
        if (success)
        {
            int dmgRoll = 0;
            for (int i = 0; i < Mathf.Max(1, Stats.BaseDamageCount); i++)
                dmgRoll += Random.Range(1, Stats.BaseDamageDice + 1);
            damage = dmgRoll + Mathf.Max(0, Stats.STRMod) + Stats.BonusDamage;

            // Simplified object HP threshold for wielded weapons.
            int breakThreshold = targetWeapon.IsTwoHanded ? 12 : 8;
            if (damage >= breakThreshold)
            {
                DestroyEquippedMainWeapon(target);
                target.Stats.ApplyCondition(CombatConditionType.Disarmed, 2, Stats.CharacterName);
                return new SpecialAttackResult
                {
                    ManeuverName = "Sunder",
                    Success = true,
                    CheckRoll = atkRoll,
                    CheckTotal = atkTotal,
                    OpposedRoll = defRoll,
                    OpposedTotal = defTotal,
                    DamageDealt = damage,
                    Log = $"{Stats.CharacterName} sunders {target.Stats.CharacterName}'s {targetWeapon.Name}! ({atkTotal} vs {defTotal}, {damage} object dmg)"
                };
            }
        }

        return new SpecialAttackResult
        {
            ManeuverName = "Sunder",
            Success = false,
            CheckRoll = atkRoll,
            CheckTotal = atkTotal,
            OpposedRoll = defRoll,
            OpposedTotal = defTotal,
            DamageDealt = damage,
            Log = success
                ? $"{Stats.CharacterName} hits {target.Stats.CharacterName}'s {targetWeapon.Name} but fails to break it ({damage} dmg)."
                : $"{Stats.CharacterName} fails to sunder {target.Stats.CharacterName}'s weapon. ({atkTotal} vs {defTotal})"
        };
    }

    private SpecialAttackResult ResolveBullRush(CharacterController target)
    {
        int atkRoll = Random.Range(1, 21);
        int defRoll = Random.Range(1, 21);
        int atkTotal = atkRoll + Stats.STRMod + Stats.SizeModifier + (Stats.HasFeat("Improved Bull Rush") ? 4 : 0);
        int defTotal = defRoll + target.Stats.STRMod + target.Stats.SizeModifier;
        bool success = atkTotal >= defTotal;

        return new SpecialAttackResult
        {
            ManeuverName = "Bull Rush",
            Success = success,
            CheckRoll = atkRoll,
            CheckTotal = atkTotal,
            OpposedRoll = defRoll,
            OpposedTotal = defTotal,
            Log = success
                ? $"{Stats.CharacterName} wins Bull Rush against {target.Stats.CharacterName} ({atkTotal} vs {defTotal})."
                : $"{Stats.CharacterName} fails Bull Rush against {target.Stats.CharacterName} ({atkTotal} vs {defTotal})."
        };
    }

    private SpecialAttackResult ResolveOverrun(CharacterController target)
    {
        int atkRoll = Random.Range(1, 21);
        int defRoll = Random.Range(1, 21);
        int atkTotal = atkRoll + Stats.STRMod + Stats.SizeModifier + (Stats.HasFeat("Improved Overrun") ? 4 : 0);
        int defTotal = defRoll + target.Stats.STRMod + target.Stats.SizeModifier;
        bool success = atkTotal >= defTotal;

        if (success)
            target.Stats.ApplyCondition(CombatConditionType.Prone, 1, Stats.CharacterName);

        return new SpecialAttackResult
        {
            ManeuverName = "Overrun",
            Success = success,
            CheckRoll = atkRoll,
            CheckTotal = atkTotal,
            OpposedRoll = defRoll,
            OpposedTotal = defTotal,
            Log = success
                ? $"{Stats.CharacterName} overruns {target.Stats.CharacterName}! ({atkTotal} vs {defTotal}) Target knocked PRONE."
                : $"{Stats.CharacterName} fails to overrun {target.Stats.CharacterName}. ({atkTotal} vs {defTotal})"
        };
    }

    private SpecialAttackResult ResolveFeint(CharacterController target)
    {
        int bluffRoll = Random.Range(1, 21);
        int senseRoll = Random.Range(1, 21);
        int bluffBonus = Stats.GetSkillBonus("Bluff") + (Stats.HasFeat("Improved Feint") ? 4 : 0);
        int senseBonus = target.Stats.GetSkillBonus("Sense Motive");

        int bluffTotal = bluffRoll + bluffBonus;
        int senseTotal = senseRoll + senseBonus;
        bool success = bluffTotal >= senseTotal;

        if (success)
            target.Stats.ApplyCondition(CombatConditionType.Feinted, 1, Stats.CharacterName);

        return new SpecialAttackResult
        {
            ManeuverName = "Feint",
            Success = success,
            CheckRoll = bluffRoll,
            CheckTotal = bluffTotal,
            OpposedRoll = senseRoll,
            OpposedTotal = senseTotal,
            Log = success
                ? $"{Stats.CharacterName} feints {target.Stats.CharacterName} ({bluffTotal} vs {senseTotal}). Target is off-balance (-2 AC, 1 round)."
                : $"{Stats.CharacterName}'s feint fails against {target.Stats.CharacterName} ({bluffTotal} vs {senseTotal})."
        };
    }

    public int GetGrappleModifier()
    {
        return Stats.BaseAttackBonus + Stats.STRMod + Stats.SizeModifier + (Stats.HasFeat("Improved Grapple") ? 4 : 0);
    }

    private static int GetDisarmWeaponModifier(CharacterController character)
    {
        ItemData weapon = character.GetEquippedMainWeapon();
        if (weapon == null) return 0;
        if (weapon.IsTwoHanded) return 4;
        if (weapon.IsLightWeapon) return -4;
        return 0;
    }

    private static void DestroyEquippedMainWeapon(CharacterController target)
    {
        var invComp = target.GetComponent<InventoryComponent>();
        if (invComp == null || invComp.CharacterInventory == null) return;

        var inv = invComp.CharacterInventory;
        if (inv.RightHandSlot != null && inv.RightHandSlot.IsWeapon)
            inv.RightHandSlot = null;
        else if (inv.LeftHandSlot != null && inv.LeftHandSlot.IsWeapon)
            inv.LeftHandSlot = null;

        inv.RecalculateStats();
    }

    // ========== 5-FOOT STEP ==========

    /// <summary>
    /// Perform a 5-foot step (1 square move that does NOT provoke AoOs).
    /// D&D 3.5: 5-foot step can be taken if no other movement this turn.
    /// </summary>
    /// <param name="targetCell">The adjacent cell to step to.</param>
    /// <returns>True if the 5-foot step was successful.</returns>
    public bool FiveFootStep(SquareCell targetCell)
    {
        if (targetCell == null) return false;

        // Must be adjacent (1 square away)
        if (!SquareGridUtils.IsAdjacent(GridPosition, targetCell.Coords))
        {
            Debug.Log($"[5ftStep] {Stats.CharacterName}: Target not adjacent, cannot 5-foot step");
            return false;
        }

        // Must not have moved this turn
        if (HasMovedThisTurn)
        {
            Debug.Log($"[5ftStep] {Stats.CharacterName}: Already moved this turn, cannot 5-foot step");
            return false;
        }

        // Can only take one 5-foot step per turn
        if (HasTakenFiveFootStep)
        {
            Debug.Log($"[5ftStep] {Stats.CharacterName}: Already used 5-foot step this turn");
            return false;
        }

        if (HasCondition(CombatConditionType.Prone) || HasCondition(CombatConditionType.Grappled))
        {
            Debug.Log($"[5ftStep] {Stats.CharacterName}: Invalid condition for 5-foot step");
            return false;
        }

        if (targetCell.IsOccupied)
        {
            Debug.Log($"[5ftStep] {Stats.CharacterName}: Target cell occupied, cannot 5-foot step");
            return false;
        }

        Debug.Log($"[5ftStep] {Stats.CharacterName} takes a 5-foot step to ({targetCell.Coords.x},{targetCell.Coords.y}) - NO AoO provoked");

        // Move without consuming normal movement/action economy.
        MoveToCell(targetCell, markAsMoved: false);
        HasTakenFiveFootStep = true;
        return true;
    }

    // ========== MONK: FLURRY OF BLOWS (Full-Round Action) ==========

    /// <summary>
    /// Perform a Flurry of Blows attack (Monk only).
    /// D&D 3.5: Two attacks at reduced BAB. At level 3: +0/+0.
    /// Must use unarmed strike or special monk weapon.
    /// </summary>
    public FullAttackResult FlurryOfBlows(CharacterController target, bool isFlanking, int flankingBonus, string flankingPartnerName, RangeInfo rangeInfo = null)
    {
        var result = new FullAttackResult();
        result.Type = FullAttackResult.AttackType.FullAttack;
        result.Attacker = this;
        result.Defender = target;
        result.DefenderHPBefore = target.Stats.CurrentHP;

        if (!Stats.IsMonk)
        {
            Debug.LogWarning($"[Monk] {Stats.CharacterName}: Cannot use Flurry of Blows - not a Monk!");
            return result;
        }

        // Get flurry attack bonuses
        int[] flurryBonuses = Stats.GetFlurryOfBlowsBonuses();
        Debug.Log($"[Monk] {Stats.CharacterName}: Flurry of Blows! {flurryBonuses.Length} attacks at " +
                  $"{string.Join("/", System.Array.ConvertAll(flurryBonuses, b => CharacterStats.FormatMod(b)))}");

        // Use monk unarmed damage (1d6 at level 3) or equipped monk weapon
        int damageDice = Stats.MonkUnarmedDamageDie > 0 ? Stats.MonkUnarmedDamageDie : Stats.BaseDamageDice;
        int damageCount = 1;
        int bonusDamage = Stats.BonusDamage;

        // Check for equipped weapon (quarterstaff is a monk weapon)
        var inv = GetComponent<InventoryComponent>();
        ItemData equippedWeapon = inv != null ? inv.CharacterInventory.RightHandSlot : null;
        int critThreatMin = 20;
        int critMult = 2;

        if (equippedWeapon != null)
        {
            // Use weapon stats if equipped
            damageDice = equippedWeapon.DamageDice;
            damageCount = equippedWeapon.DamageCount;
            bonusDamage = equippedWeapon.BonusDamage;
            critThreatMin = equippedWeapon.CritThreatMin;
            critMult = equippedWeapon.CritMultiplier;
            Debug.Log($"[Monk] Using weapon: {equippedWeapon.Name} ({damageCount}d{damageDice})");
        }
        else
        {
            Debug.Log($"[Monk] Using unarmed strike: 1d{damageDice}");
        }

        int racialAtkBonus = Stats.GetRacialAttackBonus(target.Stats);
        int proneAttackPenalty = GetProneAttackModifier(isMeleeAttack: true);

        for (int i = 0; i < flurryBonuses.Length; i++)
        {
            if (target.Stats.IsDead)
            {
                Debug.Log($"[Monk] Target is dead, stopping at attack {i + 1}");
                break;
            }

            int atkMod = flurryBonuses[i] + (isFlanking ? flankingBonus : 0) + racialAtkBonus + proneAttackPenalty;

            string label = $"Flurry {i + 1} ({CharacterStats.FormatMod(flurryBonuses[i])})";
            int hpBefore = target.Stats.CurrentHP;

            CombatResult atk = PerformSingleAttackWithCrit(target, atkMod, isFlanking, flankingBonus, flankingPartnerName,
                damageDice, damageCount, bonusDamage, critThreatMin, critMult,
                equippedWeapon, false, 0);

            atk.RacialAttackBonus = racialAtkBonus;
            atk.SizeAttackBonus = Stats.SizeModifier;
            if (equippedWeapon != null)
            {
                atk.WeaponName = equippedWeapon.Name;
                atk.BaseDamageDiceStr = $"{damageCount}d{damageDice}";
            }
            else
            {
                atk.WeaponName = "Unarmed Strike";
                atk.BaseDamageDiceStr = $"1d{damageDice}";
            }
            atk.DefenderHPBefore = hpBefore;
            atk.DefenderHPAfter = target.Stats.CurrentHP;

            result.Attacks.Add(atk);
            result.AttackLabels.Add(label);
        }

        result.DefenderHPAfter = target.Stats.CurrentHP;
        result.TargetKilled = target.Stats.IsDead;
        HasAttackedThisTurn = true;

        Debug.Log($"[Monk] {Stats.CharacterName}: Flurry of Blows complete - " +
                  $"{result.Attacks.Count} attacks, target HP: {result.DefenderHPAfter}");
        return result;
    }

    // ========== BARBARIAN: RAGE (Free Action) ==========

    /// <summary>
    /// Activate Barbarian Rage. This is a free action that can be done at the start of the turn.
    /// </summary>
    public bool ActivateRage()
    {
        if (Stats == null || !Stats.IsBarbarian)
        {
            Debug.LogWarning($"[Barbarian] Cannot rage - not a Barbarian!");
            return false;
        }

        bool success = Stats.ActivateRage();
        if (success)
        {
            Debug.Log($"[Barbarian] {Stats.CharacterName}: Rage activated via CharacterController! " +
                      $"AC now {Stats.ArmorClass} (rage -2 penalty applied)");
        }
        return success;
    }
}
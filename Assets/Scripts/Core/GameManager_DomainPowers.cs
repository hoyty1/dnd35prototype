using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// D&D 3.5e Domain Powers — activated abilities for cleric domains.
/// Implementation:
///   Strength: Enhancement bonus to STR (+CL) for 1 round, 1/day
///   Destruction: Smite — single melee attack at +4 attack, +cleric level damage, 1/day
///   Death: Death Touch — roll 1d6 per cleric level, target dies if roll >= current HP (no save), 1/day
///   Sun: Greater Turning — next turn undead at +2 effective level, +1d10 turning damage, 1/day
///   Travel: Freedom of Movement for 1 round per cleric level, 1/day (personal)
///   Plant: Rebuke/Command plant creatures (uses Turn Undead attempts pool)
///   Air/Earth/Fire/Water: Turn or rebuke elementals of matching subtype (uses Turn Undead attempts pool)
///     Good clerics turn/destroy; evil clerics rebuke/command.
/// </summary>
public partial class GameManager
{
    // ==================== VALIDATION ====================

    /// <summary>
    /// Check if a cleric can activate a specific domain power.
    /// </summary>
    public bool CanActivateDomainPower(CharacterController cleric, string domain, out string reason)
    {
        reason = "";

        if (cleric == null || cleric.Stats == null)
        {
            reason = "Invalid character";
            return false;
        }

        if (!cleric.Stats.IsCleric)
        {
            reason = "Must be a Cleric";
            return false;
        }

        if (cleric.Stats.ChosenDomains == null || !cleric.Stats.ChosenDomains.Contains(domain))
        {
            reason = $"Does not have {domain} domain";
            return false;
        }

        if (cleric.Stats.CurrentHP <= 0)
        {
            reason = "Character is dead or dying";
            return false;
        }

        switch (domain)
        {
            case "Strength":
                if (cleric.Stats.StrengthDomainUsesToday >= 1)
                {
                    reason = "Already used Feat of Strength today";
                    return false;
                }
                break;
            case "Destruction":
                if (cleric.Stats.DestructionDomainUsesToday >= 1)
                {
                    reason = "Already used Smite today";
                    return false;
                }
                break;
            case "Death":
                if (cleric.Stats.DeathDomainUsesToday >= 1)
                {
                    reason = "Already used Death Touch today";
                    return false;
                }
                break;
            case "Sun":
                if (cleric.Stats.SunDomainUsesToday >= 1)
                {
                    reason = "Already used Greater Turning today";
                    return false;
                }
                // Greater Turning still requires a Turn Undead attempt
                if (cleric.Stats.TurnUndeadAttemptsUsedToday >= cleric.Stats.MaxTurnUndeadAttemptsPerDay)
                {
                    reason = "No Turn Undead attempts remaining";
                    return false;
                }
                break;
            case "Travel":
                if (cleric.Stats.TravelDomainUsesToday >= 1)
                {
                    reason = "Already used Freedom of Movement today";
                    return false;
                }
                break;
            case "Plant":
                // Plant domain rebuke uses the Turn Undead attempts pool
                if (cleric.Stats.TurnUndeadAttemptsUsedToday >= cleric.Stats.MaxTurnUndeadAttemptsPerDay)
                {
                    reason = "No Turn Undead attempts remaining";
                    return false;
                }
                break;
            case "Luck":
                if (cleric.Stats.LuckDomainUsesToday >= 1)
                {
                    reason = "Already used Luck reroll today";
                    return false;
                }
                if (cleric.Stats.LuckRerollPending)
                {
                    reason = "Luck reroll is already armed";
                    return false;
                }
                break;
            case "Air":
            case "Earth":
            case "Fire":
            case "Water":
                // Elemental domains use the Turn Undead attempts pool
                if (cleric.Stats.TurnUndeadAttemptsUsedToday >= cleric.Stats.MaxTurnUndeadAttemptsPerDay)
                {
                    reason = "No Turn Undead attempts remaining";
                    return false;
                }
                break;
            default:
                reason = $"{domain} domain has no activated ability";
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a list of domain powers available for activation by this character.
    /// </summary>
    public List<(string domain, string label)> GetAvailableDomainPowers(CharacterController cleric)
    {
        var result = new List<(string domain, string label)>();
        if (cleric == null || cleric.Stats == null || cleric.Stats.ChosenDomains == null)
            return result;

        foreach (string domain in cleric.Stats.ChosenDomains)
        {
            if (string.IsNullOrEmpty(domain)) continue;
            string reason;
            if (CanActivateDomainPower(cleric, domain, out reason))
            {
                switch (domain)
                {
                    case "Strength":
                        result.Add((domain, "Feat of Strength"));
                        break;
                    case "Destruction":
                        result.Add((domain, "Smite"));
                        break;
                    case "Death":
                        result.Add((domain, "Death Touch"));
                        break;
                    case "Sun":
                        result.Add((domain, "Greater Turning"));
                        break;
                    case "Travel":
                        result.Add((domain, "Freedom of Movement"));
                        break;
                    case "Plant":
                        result.Add((domain, "Rebuke Plants"));
                        break;
                    case "Luck":
                        result.Add((domain, "Luck Reroll"));
                        break;
                    case "Air":
                    case "Earth":
                    case "Fire":
                    case "Water":
                        result.Add((domain, GetElementalTurningLabel(cleric, domain)));
                        break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns appropriate label for elemental turning based on cleric alignment.
    /// Good clerics turn; evil clerics rebuke; neutral clerics default to turn.
    /// </summary>
    private string GetElementalTurningLabel(CharacterController cleric, string domain)
    {
        bool isEvil = AlignmentHelper.IsEvil(cleric.Stats.CharacterAlignment);
        string action = isEvil ? "Rebuke" : "Turn";
        return $"{action} {domain} Creatures";
    }

    // ==================== ACTIVATION METHODS ====================

    /// <summary>
    /// Strength Domain: Feat of Strength — enhancement bonus to STR equal to cleric level for 1 round.
    /// D&D 3.5e: "You can perform a feat of strength as a supernatural ability. You gain an
    /// enhancement bonus to Strength equal to your cleric level. Activating the power is a free
    /// action, the power lasts 1 round, and it is usable once per day."
    /// </summary>
    public void ActivateStrengthDomain(CharacterController cleric)
    {
        string reason;
        if (!CanActivateDomainPower(cleric, "Strength", out reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Feat of Strength: {reason}.");
            return;
        }

        int clericLevel = cleric.Stats.GetClassLevel("Cleric");
        int enhBonus = Mathf.Max(1, clericLevel);

        cleric.Stats.StrengthDomainUsesToday++;
        cleric.Stats.StrengthDomainBonusRounds = 1;

        // Apply enhancement bonus to STR (tracked as temporary bonus)
        // Enhancement bonuses don't stack with magic item enhancement bonuses to STR
        cleric.Stats.TemporarySTRBonus += enhBonus;

        var sb = new StringBuilder();
        sb.AppendLine($"<color=#FFD700>💪 {cleric.Stats.CharacterName} activates Feat of Strength!</color>");
        sb.AppendLine($"   Enhancement bonus to STR: +{enhBonus} for 1 round");
        sb.AppendLine($"   STR: {cleric.Stats.STR} (effective {cleric.Stats.STR + enhBonus})");
        CombatUI?.ShowCombatLog(sb.ToString().TrimEnd());
    }

    /// <summary>
    /// Destruction Domain: Smite — single melee attack with +4 attack and +cleric level damage.
    /// D&D 3.5e: "Once per day, you may make a single melee attack with a +4 bonus on attack rolls
    /// and a bonus on damage rolls equal to your cleric level (if you hit). You must declare the
    /// smite before making the attack."
    /// </summary>
    public void ActivateDestructionSmite(CharacterController cleric)
    {
        string reason;
        if (!CanActivateDomainPower(cleric, "Destruction", out reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Smite: {reason}.");
            return;
        }

        cleric.Stats.DestructionDomainUsesToday++;
        cleric.Stats.DestructionSmiteActive = true;

        CombatUI?.ShowCombatLog($"<color=#FF4444>⚔️ {cleric.Stats.CharacterName} invokes Destruction Smite!</color>");
        CombatUI?.ShowCombatLog($"   Next melee attack: +4 attack, +{cleric.Stats.GetClassLevel("Cleric")} damage");
    }

    /// <summary>
    /// Death Domain: Death Touch — supernatural ability, melee touch attack.
    /// D&D 3.5e PHB: "You can use a death touch once per day. Your death touch is a supernatural ability
    /// that produces a death effect. You must succeed on a melee touch attack against a living creature
    /// (using the rules for touch spells). When you touch, roll 1d6 per cleric level you possess.
    /// If the total at least equals the creature's current hit points, it dies (no save)."
    /// 
    /// Key rules: 1d6 per cleric level (no cap), no damage on failure, no save,
    /// death effect (does not affect undead, constructs, or creatures immune to death effects).
    /// </summary>
    public void ActivateDeathTouch(CharacterController cleric, CharacterController target)
    {
        string reason;
        if (!CanActivateDomainPower(cleric, "Death", out reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Death Touch: {reason}.");
            return;
        }

        if (target == null || target.Stats == null || target.Stats.CurrentHP <= 0)
        {
            CombatUI?.ShowCombatLog($"⚠ Invalid target for Death Touch.");
            return;
        }

        // Death effect: does not affect undead, constructs, or creatures immune to death effects
        string creatureType = target.Stats.CreatureType ?? "";
        if (creatureType == "Undead" || creatureType == "Construct")
        {
            CombatUI?.ShowCombatLog($"⚠ Death Touch has no effect on {target.Stats.CharacterName} ({creatureType} creatures are immune to death effects).");
            return;
        }

        // Consume the ability
        cleric.Stats.DeathDomainUsesToday++;

        // Consume standard action
        if (!cleric.CommitStandardAction())
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Death Touch: standard action unavailable.");
            return;
        }

        int clericLevel = cleric.Stats.GetClassLevel("Cleric");
        int numDice = Mathf.Max(1, clericLevel); // 1d6 per cleric level, no cap

        // Melee touch attack
        int attackRoll = Random.Range(1, 21);
        int attackMod = cleric.Stats.BaseAttackBonus + cleric.Stats.STRMod;
        int attackTotal = attackRoll + attackMod;
        int targetTouchAC = target.Stats.TouchArmorClass;
        bool hit = (attackRoll == 20) || (attackRoll != 1 && attackTotal >= targetTouchAC);

        var sb = new StringBuilder();
        sb.AppendLine($"<color=#8B008B>💀 {cleric.Stats.CharacterName} reaches out with Death Touch against {target.Stats.CharacterName}!</color>");
        sb.AppendLine($"   Melee Touch: d20({attackRoll})+{attackMod}={attackTotal} vs Touch AC {targetTouchAC} → {(hit ? "HIT" : "MISS")}");

        if (!hit)
        {
            CombatUI?.ShowCombatLog(sb.ToString().TrimEnd());
            return;
        }

        // Roll 1d6 per cleric level
        int totalRoll = 0;
        var diceResults = new List<int>();
        for (int i = 0; i < numDice; i++)
        {
            int roll = Random.Range(1, 7);
            diceResults.Add(roll);
            totalRoll += roll;
        }

        string diceStr = string.Join("+", diceResults);
        sb.AppendLine($"   Death Touch: {numDice}d6 = [{diceStr}] = {totalRoll} vs {target.Stats.CurrentHP} HP");

        if (totalRoll >= target.Stats.CurrentHP)
        {
            // Instant death — no save
            int hpBefore = target.Stats.CurrentHP;
            target.Stats.CurrentHP = -10;
            sb.AppendLine($"<color=#FF0000>   {target.Stats.CharacterName} is slain! ({totalRoll} ≥ {hpBefore} HP — no save)</color>");
        }
        else
        {
            // Death Touch fails — no damage dealt per PHB
            sb.AppendLine($"   Death Touch fails — {totalRoll} < {target.Stats.CurrentHP} HP. No effect.");
        }

        CombatUI?.ShowCombatLog(sb.ToString().TrimEnd());
    }

    /// <summary>
    /// Sun Domain: Greater Turning — the next turn undead attempt is enhanced.
    /// D&D 3.5e: "Once per day, you can perform a greater turning against undead in place of
    /// a regular turning (or rebuking) attempt. Undead that would be turned are destroyed instead."
    /// 
    /// Implementation: Sets a flag that TurnUndeadSystem checks. The next turn undead attempt
    /// destroys turned undead instead of just turning them.
    /// </summary>
    public void ActivateGreaterTurning(CharacterController cleric)
    {
        string reason;
        if (!CanActivateDomainPower(cleric, "Sun", out reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Greater Turning: {reason}.");
            return;
        }

        cleric.Stats.SunDomainUsesToday++;

        // Set flag for TurnUndeadSystem to pick up
        cleric.Stats.GreaterTurningActive = true;

        CombatUI?.ShowCombatLog($"<color=#FFD700>☀️ {cleric.Stats.CharacterName} channels the power of the Sun for Greater Turning!</color>");
        CombatUI?.ShowCombatLog($"   Next Turn Undead will DESTROY affected undead instead of turning them.");
    }

    /// <summary>
    /// Travel Domain: Freedom of Movement — personal, for 1 round per cleric level.
    /// D&D 3.5e: "You can act normally regardless of magical effects that impede movement as if
    /// you were affected by the spell freedom of movement. This is a supernatural ability that
    /// lasts a number of rounds equal to your cleric level. It is usable once per day."
    /// </summary>
    public void ActivateTravelFreedom(CharacterController cleric)
    {
        string reason;
        if (!CanActivateDomainPower(cleric, "Travel", out reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Freedom of Movement: {reason}.");
            return;
        }

        int clericLevel = cleric.Stats.GetClassLevel("Cleric");
        int durationRounds = Mathf.Max(1, clericLevel);

        cleric.Stats.TravelDomainUsesToday++;
        cleric.Stats.TravelDomainFreedomRounds = durationRounds;

        // Freedom of Movement: immune to movement-impairing effects
        // This is checked in grapple, web, entangle, hold person, etc.
        CombatUI?.ShowCombatLog($"<color=#00BFFF>🗺️ {cleric.Stats.CharacterName} activates Freedom of Movement!</color>");
        CombatUI?.ShowCombatLog($"   Duration: {durationRounds} rounds. Immune to movement-impairing effects.");
    }

    // ==================== ROUND TICK ====================

    /// <summary>
    /// Called at the start of each character's turn to tick down domain power durations.
    /// </summary>
    public void TickDomainPowerDurations(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return;

        // Strength Domain: tick down enhancement bonus
        if (character.Stats.StrengthDomainBonusRounds > 0)
        {
            character.Stats.StrengthDomainBonusRounds--;
            if (character.Stats.StrengthDomainBonusRounds <= 0)
            {
                int clericLevel = character.Stats.GetClassLevel("Cleric");
                character.Stats.TemporarySTRBonus -= Mathf.Max(1, clericLevel);
                if (character.Stats.TemporarySTRBonus < 0) character.Stats.TemporarySTRBonus = 0;
                CombatUI?.ShowCombatLog($"<color=#AAAAAA>💪 {character.Stats.CharacterName}'s Feat of Strength ends.</color>");
            }
        }

        // Travel Domain: tick down freedom of movement
        if (character.Stats.TravelDomainFreedomRounds > 0)
        {
            character.Stats.TravelDomainFreedomRounds--;
            if (character.Stats.TravelDomainFreedomRounds <= 0)
            {
                CombatUI?.ShowCombatLog($"<color=#AAAAAA>🗺️ {character.Stats.CharacterName}'s Freedom of Movement ends.</color>");
            }
        }
    }

    // ==================== DESTRUCTION SMITE HELPERS ====================

    /// <summary>
    /// Returns the smite attack bonus if Destruction Smite is active, else 0.
    /// Called by attack resolution code.
    /// </summary>
    public static int GetDestructionSmiteAttackBonus(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null || !attacker.Stats.DestructionSmiteActive)
            return 0;
        return 4;
    }

    /// <summary>
    /// Returns the smite damage bonus if Destruction Smite is active, else 0.
    /// Called by attack resolution code.
    /// </summary>
    public static int GetDestructionSmiteDamageBonus(CharacterController attacker)
    {
        if (attacker == null || attacker.Stats == null || !attacker.Stats.DestructionSmiteActive)
            return 0;
        return Mathf.Max(1, attacker.Stats.GetClassLevel("Cleric"));
    }

    /// <summary>
    /// Consume the Destruction Smite after a melee attack resolves (hit or miss).
    /// </summary>
    public static void ConsumeDestructionSmite(CharacterController attacker)
    {
        if (attacker != null && attacker.Stats != null)
            attacker.Stats.DestructionSmiteActive = false;
    }

    // ==================== LUCK DOMAIN ====================

    /// <summary>
    /// Luck Domain: Arm the reroll — the next d20 roll (attack, save, skill check, ability check)
    /// will be rolled twice, taking the better result. Once per day.
    /// </summary>
    public void ActivateLuckReroll(CharacterController cleric)
    {
        string reason;
        if (!CanActivateDomainPower(cleric, "Luck", out reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Luck Reroll: {reason}.");
            return;
        }

        cleric.Stats.LuckRerollPending = true;

        CombatUI?.ShowCombatLog($"<color=#00CC66>🍀 {cleric.Stats.CharacterName} invokes the Luck domain — next d20 roll will be rerolled!</color>");
    }

    /// <summary>
    /// Deactivate (disarm) Luck reroll without consuming the daily use.
    /// Called when the player toggles off or when combat ends with it still armed.
    /// </summary>
    public void DeactivateLuckReroll(CharacterController cleric)
    {
        if (cleric == null || cleric.Stats == null) return;

        if (cleric.Stats.LuckRerollPending)
        {
            cleric.Stats.LuckRerollPending = false;
            CombatUI?.ShowCombatLog($"<color=#00CC66>🍀 {cleric.Stats.CharacterName} disarms the Luck reroll (not consumed).</color>");
        }
    }

    /// <summary>
    /// If a Luck reroll was triggered during the last d20 roll, emit the combat log and clear the flag.
    /// Call this after any d20 roll that may have triggered a reroll (attack, save, skill).
    /// </summary>
    public void LogLuckRerollIfTriggered(CharacterStats stats)
    {
        if (stats == null || !stats.LastLuckRerollTriggered) return;

        string usedLabel = stats.LastLuckRerollUsed == stats.LastLuckRerollOriginal ? "original" : "reroll";
        CombatUI?.ShowCombatLog(
            $"<color=#00CC66>🍀 Luck reroll on {stats.LastLuckRerollContext}: " +
            $"original {stats.LastLuckRerollOriginal}, reroll {stats.LastLuckRerollSecond} → " +
            $"using {usedLabel} ({stats.LastLuckRerollUsed}). Luck domain power expended for today.</color>");

        stats.LastLuckRerollTriggered = false;
    }

    // ==================== UI BUTTON HANDLER ====================

    /// <summary>
    /// Called when the Domain Power button is pressed in combat.
    /// Shows available domain powers and activates the chosen one.
    /// For targeted abilities (Death Touch), enters targeting mode.
    /// For self-buffs (Strength, Travel), activates immediately.
    /// For Destruction Smite, sets the flag for the next melee attack.
    /// For Greater Turning, sets the flag and then invokes Turn Undead.
    /// For Luck, arms the reroll toggle for the next d20 roll.
    /// </summary>
    public void OnDomainPowerButtonPressed()
    {
        CharacterController activePC = ActivePC;
        if (activePC == null || activePC.Stats == null)
            return;

        // Toggle off Luck reroll if it is currently armed
        if (activePC.Stats.LuckRerollPending)
        {
            DeactivateLuckReroll(activePC);
            return;
        }

        var powers = GetAvailableDomainPowers(activePC);
        if (powers.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {activePC.Stats.CharacterName} has no domain powers available.");
            return;
        }

        // If only one power available, activate it directly
        if (powers.Count == 1)
        {
            ActivateDomainPowerByName(activePC, powers[0].domain);
            return;
        }

        // Multiple powers: show selection in combat log and use the first available
        // (In a full UI implementation, this would show a popup menu)
        var sb = new StringBuilder();
        sb.AppendLine($"<color=#CC99FF>Domain Powers available for {activePC.Stats.CharacterName}:</color>");
        for (int i = 0; i < powers.Count; i++)
        {
            sb.AppendLine($"  {i + 1}. {powers[i].label} ({powers[i].domain} domain)");
        }
        sb.AppendLine("<color=#CC99FF>Activating: " + powers[0].label + "</color>");
        CombatUI?.ShowCombatLog(sb.ToString().TrimEnd());

        ActivateDomainPowerByName(activePC, powers[0].domain);
    }

    private void ActivateDomainPowerByName(CharacterController cleric, string domain)
    {
        switch (domain)
        {
            case "Strength":
                ActivateStrengthDomain(cleric);
                break;
            case "Destruction":
                ActivateDestructionSmite(cleric);
                break;
            case "Death":
                // Death Touch needs a target — store pending and let player click an adjacent enemy
                CombatUI?.ShowCombatLog($"<color=#8B008B>💀 Select an adjacent target for Death Touch (click an enemy).</color>");
                _pendingDomainPower = "Death";
                _pendingDomainPowerCleric = cleric;
                // Target selection handled by existing click-on-character system
                break;
            case "Sun":
                ActivateGreaterTurning(cleric);
                // After activating, immediately trigger Turn Undead
                OnTurnUndeadButtonPressed();
                break;
            case "Travel":
                ActivateTravelFreedom(cleric);
                break;
            case "Plant":
                // Plant domain: rebuke/command plants uses Turn Undead system
                ActivatePlantRebuke(cleric);
                break;
            case "Luck":
                ActivateLuckReroll(cleric);
                break;
            case "Air":
            case "Earth":
            case "Fire":
            case "Water":
                // Elemental domains: turn or rebuke elementals of matching subtype
                ActivateElementalTurning(cleric, domain);
                break;
        }
    }

    // Pending domain power state for targeted abilities
    private string _pendingDomainPower;
    private CharacterController _pendingDomainPowerCleric;

    /// <summary>
    /// Called when a target is selected for a domain power (e.g., Death Touch).
    /// </summary>
    public void ResolveDomainPowerOnTarget(CharacterController target)
    {
        if (_pendingDomainPower == "Death" && _pendingDomainPowerCleric != null)
        {
            ActivateDeathTouch(_pendingDomainPowerCleric, target);
        }

        _pendingDomainPower = null;
        _pendingDomainPowerCleric = null;
    }

    // ==================== PLANT DOMAIN ====================

    /// <summary>
    /// Plant Domain: Rebuke/Command plant creatures.
    /// D&D 3.5e: "Rebuke or command plant creatures as an evil cleric rebukes or commands undead."
    /// Uses Turn Undead attempts pool. Checks for plant creatures in range.
    /// </summary>
    public void ActivatePlantRebuke(CharacterController cleric)
    {
        string reason;
        if (!CanActivateDomainPower(cleric, "Plant", out reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Rebuke Plants: {reason}.");
            return;
        }

        // Consume standard action
        if (!cleric.CommitStandardAction())
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use Rebuke Plants: standard action unavailable.");
            return;
        }

        cleric.Stats.TurnUndeadAttemptsUsedToday++;
        int attemptsRemaining = cleric.Stats.MaxTurnUndeadAttemptsPerDay - cleric.Stats.TurnUndeadAttemptsUsedToday;

        // Find plant creatures in 60ft range
        var plants = GetPlantCreaturesInRange(cleric, 12); // 60ft = 12 squares
        if (plants.Count == 0)
        {
            CombatUI?.ShowCombatLog($"🌿 {cleric.Stats.CharacterName} channels energy to rebuke plants, but none are in range.");
            CombatUI?.ShowCombatLog($"   Remaining attempts today: {attemptsRemaining}");
            return;
        }

        // Turning check (like evil cleric rebuke)
        int checkRoll = Random.Range(1, 21);
        int checkTotal = checkRoll + cleric.Stats.CHAMod;
        int clericLevel = cleric.Stats.GetClassLevel("Cleric");
        int maxHD = GetMaxTurnableHD(checkTotal, clericLevel);

        // Turning damage: 2d6 + cleric level + CHA mod
        int turnDamageRoll = Random.Range(1, 7) + Random.Range(1, 7);
        int turnPoolHd = turnDamageRoll + clericLevel + cleric.Stats.CHAMod;
        if (turnPoolHd < 0) turnPoolHd = 0;

        var sb = new StringBuilder();
        sb.AppendLine($"<color=#228B22>🌿 {cleric.Stats.CharacterName} channels energy to rebuke plant creatures!</color>");
        sb.AppendLine($"   Rebuke Check: d20({checkRoll}) + CHA {CharacterStats.FormatMod(cleric.Stats.CHAMod)} = {checkTotal} → affects plants up to {maxHD} HD");
        sb.AppendLine($"   Rebuke Pool: 2d6({turnDamageRoll}) + level {clericLevel} + CHA {CharacterStats.FormatMod(cleric.Stats.CHAMod)} = {turnPoolHd} total HD");

        // Apply rebuke to plants (by HD, lowest first)
        plants.Sort((a, b) => a.Stats.HitDice.CompareTo(b.Stats.HitDice));
        int hdUsed = 0;
        int rebuked = 0;
        foreach (var plant in plants)
        {
            int plantHD = plant.Stats.HitDice;
            if (plantHD > maxHD)
            {
                sb.AppendLine($"   {plant.Stats.CharacterName} ({plantHD} HD) is too powerful to rebuke.");
                continue;
            }
            if (hdUsed + plantHD > turnPoolHd)
            {
                sb.AppendLine($"   Insufficient HD pool for {plant.Stats.CharacterName} ({plantHD} HD).");
                break;
            }
            hdUsed += plantHD;
            rebuked++;

            // Rebuke: plant cowers for 10 rounds (like evil rebuke of undead)
            sb.AppendLine($"<color=#00CC00>   {plant.Stats.CharacterName} ({plantHD} HD) is rebuked! (Cowers for 10 rounds)</color>");
            // Apply cowering condition via the standard condition system
            plant.ApplyCondition(CombatConditionType.Cowering, 10, cleric.Stats.CharacterName);
        }

        if (rebuked == 0)
            sb.AppendLine("   No plant creatures were affected.");

        sb.AppendLine($"   Remaining attempts today: {attemptsRemaining}");
        CombatUI?.ShowCombatLog(sb.ToString().TrimEnd());
    }

    /// <summary>
    /// Find plant creatures within range (in grid squares).
    /// </summary>
    private List<CharacterController> GetPlantCreaturesInRange(CharacterController turner, int rangeSquares)
    {
        var result = new List<CharacterController>();
        if (turner == null) return result;

        Vector2Int turnerPos = turner.GridPosition;
        var allCharacters = GetAllCharacters();
        if (allCharacters == null) return result;

        foreach (var character in allCharacters)
        {
            if (character == turner || character == null || character.Stats == null)
                continue;
            if (character.Stats.CurrentHP <= 0)
                continue;
            if (!IsPlantCreature(character))
                continue;

            int dist = Mathf.Max(Mathf.Abs(character.GridPosition.x - turnerPos.x),
                                 Mathf.Abs(character.GridPosition.y - turnerPos.y));
            if (dist <= rangeSquares)
                result.Add(character);
        }

        return result;
    }

    /// <summary>
    /// Check if a character is a plant creature.
    /// </summary>
    public static bool IsPlantCreature(CharacterController character)
    {
        if (character == null || character.Stats == null)
            return false;
        if (character.Stats.CreatureType == "Plant")
            return true;
        if (character.Stats.CreatureTags != null && character.Stats.CreatureTags.Contains("Plant"))
            return true;
        return false;
    }

    /// <summary>
    /// Get the maximum HD of creature that can be affected by a turning check result.
    /// D&D 3.5e Table 8-16: Turning Undead.
    /// </summary>
    private int GetMaxTurnableHD(int checkTotal, int turnerLevel)
    {
        // Simplified version of the Turn Undead table
        if (checkTotal <= 0) return turnerLevel - 4;
        if (checkTotal <= 3) return turnerLevel - 3;
        if (checkTotal <= 6) return turnerLevel - 2;
        if (checkTotal <= 9) return turnerLevel - 1;
        if (checkTotal <= 12) return turnerLevel;
        if (checkTotal <= 15) return turnerLevel + 1;
        if (checkTotal <= 18) return turnerLevel + 2;
        if (checkTotal <= 21) return turnerLevel + 3;
        return turnerLevel + 4;
    }

    // ==================== ELEMENTAL DOMAIN TURNING ====================

    /// <summary>
    /// Air/Earth/Fire/Water Domain: Turn or rebuke elementals of the matching subtype.
    /// D&D 3.5e PHB:
    ///   "Turn or destroy [element] creatures as a good cleric turns undead.
    ///    Rebuke, command, or bolster [element] creatures as an evil cleric rebukes undead."
    /// Uses the Turn Undead attempts pool. Good clerics turn (destroy on 2x HD or less);
    /// evil clerics rebuke (command on 2x HD or less). Neutral clerics default to turning.
    /// </summary>
    public void ActivateElementalTurning(CharacterController cleric, string domain)
    {
        string reason;
        if (!CanActivateDomainPower(cleric, domain, out reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use {domain} domain power: {reason}.");
            return;
        }

        // Consume standard action
        if (!cleric.CommitStandardAction())
        {
            CombatUI?.ShowCombatLog($"⚠ {cleric.Stats.CharacterName} cannot use {domain} domain power: standard action unavailable.");
            return;
        }

        bool isEvil = AlignmentHelper.IsEvil(cleric.Stats.CharacterAlignment);
        string actionVerb = isEvil ? "rebuke" : "turn";
        string actionVerbCap = isEvil ? "Rebuke" : "Turn";
        string elementalSubtype = domain; // "Air", "Earth", "Fire", "Water"

        cleric.Stats.TurnUndeadAttemptsUsedToday++;
        int attemptsRemaining = cleric.Stats.MaxTurnUndeadAttemptsPerDay - cleric.Stats.TurnUndeadAttemptsUsedToday;

        // Find matching elemental creatures in 60ft range
        var elementals = GetElementalCreaturesInRange(cleric, 12, elementalSubtype); // 60ft = 12 squares
        if (elementals.Count == 0)
        {
            string emoji = GetElementalEmoji(domain);
            CombatUI?.ShowCombatLog($"{emoji} {cleric.Stats.CharacterName} channels energy to {actionVerb} {domain.ToLower()} creatures, but none are in range.");
            CombatUI?.ShowCombatLog($"   Remaining attempts today: {attemptsRemaining}");
            return;
        }

        // Turning check: d20 + CHA mod
        int checkRoll = Random.Range(1, 21);
        int checkTotal = checkRoll + cleric.Stats.CHAMod;
        int clericLevel = cleric.Stats.GetClassLevel("Cleric");
        int maxHD = GetMaxTurnableHD(checkTotal, clericLevel);

        // Turning damage: 2d6 + cleric level + CHA mod
        int turnDamageRoll = Random.Range(1, 7) + Random.Range(1, 7);
        int turnPoolHd = turnDamageRoll + clericLevel + cleric.Stats.CHAMod;
        if (turnPoolHd < 0) turnPoolHd = 0;

        string emoji2 = GetElementalEmoji(domain);
        string color = GetElementalColor(domain);
        var sb = new StringBuilder();
        sb.AppendLine($"<color={color}>{emoji2} {cleric.Stats.CharacterName} channels energy to {actionVerb} {domain.ToLower()} creatures!</color>");
        sb.AppendLine($"   {actionVerbCap} Check: d20({checkRoll}) + CHA {CharacterStats.FormatMod(cleric.Stats.CHAMod)} = {checkTotal} → affects creatures up to {maxHD} HD");
        sb.AppendLine($"   {actionVerbCap} Pool: 2d6({turnDamageRoll}) + level {clericLevel} + CHA {CharacterStats.FormatMod(cleric.Stats.CHAMod)} = {turnPoolHd} total HD");

        // Apply turning/rebuking to elementals (by HD, lowest first)
        elementals.Sort((a, b) => a.Stats.HitDice.CompareTo(b.Stats.HitDice));
        int hdUsed = 0;
        int affected = 0;
        foreach (var elemental in elementals)
        {
            int elemHD = elemental.Stats.HitDice;
            if (elemHD > maxHD)
            {
                sb.AppendLine($"   {elemental.Stats.CharacterName} ({elemHD} HD) is too powerful to {actionVerb}.");
                continue;
            }
            if (hdUsed + elemHD > turnPoolHd)
            {
                sb.AppendLine($"   Insufficient HD pool for {elemental.Stats.CharacterName} ({elemHD} HD).");
                break;
            }
            hdUsed += elemHD;
            affected++;

            if (isEvil)
            {
                // Rebuke: creature cowers for 10 rounds
                // Command: if HD <= clericLevel / 2, creature is commanded instead
                if (elemHD <= clericLevel / 2)
                {
                    sb.AppendLine($"<color={color}>   {elemental.Stats.CharacterName} ({elemHD} HD) is commanded! (Under cleric's control)</color>");
                    elemental.ApplyCondition(CombatConditionType.Commanded, 100, cleric.Stats.CharacterName);
                }
                else
                {
                    sb.AppendLine($"<color={color}>   {elemental.Stats.CharacterName} ({elemHD} HD) is rebuked! (Cowers for 10 rounds)</color>");
                    elemental.ApplyCondition(CombatConditionType.Cowering, 10, cleric.Stats.CharacterName);
                }
            }
            else
            {
                // Turn: creature flees for 10 rounds
                // Destroy: if HD <= clericLevel / 2, creature is destroyed instead
                if (elemHD <= clericLevel / 2)
                {
                    int hpBefore = elemental.Stats.CurrentHP;
                    elemental.Stats.CurrentHP = -10;
                    sb.AppendLine($"<color=#FF0000>   {elemental.Stats.CharacterName} ({elemHD} HD) is destroyed! ({hpBefore} HP → -10)</color>");
                }
                else
                {
                    sb.AppendLine($"<color={color}>   {elemental.Stats.CharacterName} ({elemHD} HD) is turned! (Flees for 10 rounds)</color>");
                    elemental.ApplyCondition(CombatConditionType.Turned, 10, cleric.Stats.CharacterName);
                }
            }
        }

        if (affected == 0)
            sb.AppendLine($"   No {domain.ToLower()} creatures were affected.");

        sb.AppendLine($"   Remaining attempts today: {attemptsRemaining}");
        CombatUI?.ShowCombatLog(sb.ToString().TrimEnd());
    }

    /// <summary>
    /// Find elemental creatures of a specific subtype within range (in grid squares).
    /// Matches creatures with CreatureType "Elemental" AND the matching subtype tag in CreatureTags.
    /// </summary>
    private List<CharacterController> GetElementalCreaturesInRange(CharacterController turner, int rangeSquares, string elementalSubtype)
    {
        var result = new List<CharacterController>();
        if (turner == null) return result;

        Vector2Int turnerPos = turner.GridPosition;
        var allCharacters = GetAllCharacters();
        if (allCharacters == null) return result;

        foreach (var character in allCharacters)
        {
            if (character == turner || character == null || character.Stats == null)
                continue;
            if (character.Stats.CurrentHP <= 0)
                continue;
            if (!IsElementalOfSubtype(character, elementalSubtype))
                continue;

            int dist = Mathf.Max(Mathf.Abs(character.GridPosition.x - turnerPos.x),
                                 Mathf.Abs(character.GridPosition.y - turnerPos.y));
            if (dist <= rangeSquares)
                result.Add(character);
        }

        return result;
    }

    /// <summary>
    /// Check if a character is an elemental creature of the specified subtype.
    /// Checks for CreatureType "Elemental" with the subtype in CreatureTags,
    /// or a CreatureType that directly matches (e.g., "Air Elemental").
    /// </summary>
    public static bool IsElementalOfSubtype(CharacterController character, string subtype)
    {
        if (character == null || character.Stats == null || string.IsNullOrEmpty(subtype))
            return false;

        string creatureType = character.Stats.CreatureType ?? "";

        // Direct match: CreatureType is "Elemental" and has subtype tag
        if (creatureType == "Elemental")
        {
            if (character.Stats.CreatureTags != null && character.Stats.CreatureTags.Contains(subtype))
                return true;
        }

        // Also match compound type like "Air Elemental", "Fire Elemental", etc.
        if (creatureType.Equals($"{subtype} Elemental", System.StringComparison.OrdinalIgnoreCase))
            return true;

        // Check tags for both "Elemental" type and subtype
        if (character.Stats.CreatureTags != null)
        {
            bool hasElementalTag = character.Stats.CreatureTags.Contains("Elemental");
            bool hasSubtypeTag = character.Stats.CreatureTags.Contains(subtype);
            if (hasElementalTag && hasSubtypeTag)
                return true;
        }

        return false;
    }

    /// <summary>Get a thematic emoji for an elemental domain.</summary>
    private static string GetElementalEmoji(string domain)
    {
        switch (domain)
        {
            case "Air":   return "🌪️";
            case "Earth": return "🪨";
            case "Fire":  return "🔥";
            case "Water": return "🌊";
            default:      return "✨";
        }
    }

    /// <summary>Get a thematic color for an elemental domain's combat log messages.</summary>
    private static string GetElementalColor(string domain)
    {
        switch (domain)
        {
            case "Air":   return "#87CEEB"; // sky blue
            case "Earth": return "#CD853F"; // peru/brown
            case "Fire":  return "#FF6347"; // tomato red
            case "Water": return "#4169E1"; // royal blue
            default:      return "#FFFFFF";
        }
    }
}

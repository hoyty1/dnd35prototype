using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;

// ════════════════════════════════════════════════════════════════════════════
//  Ring Activation Manager — D&D 3.5e Sprint 2 Active Ring System
//  Central hub for activating ring abilities via command word.
//  Handles all 9 Sprint 2 active rings:
//    1. Ring of Invisibility (at will, CL 3)
//    2. Ring of Blinking (at will, CL 7)
//    3. Ring of Telekinesis (at will, CL 9)
//    4. Ring of Animal Friendship (3/day, DC 11)
//    5. Ring of the Ram (50 charges, regen 1d10/day)
//    6. Ring of X-Ray Vision (at will, Con damage on repeated use)
//    7. Ring of Shooting Stars (5 abilities, mixed frequency)
//    8. Ring of Spell Turning (automatic on equip, 1d4+6 pool)
//    9. Ring of Djinni Calling (1/week, summon Noble Djinni)
//
//  DMG pp. 229–233: Core 3.5e ring mechanics.
//  Follows TryUseStaff() pattern from GameManager.cs:5792.
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Central activation manager for all Sprint 2 active rings.
/// Called from GameManager's item use flow (TryUseConsumableFromInventory).
/// </summary>
public static class RingActivationManager
{
    // ════════════════════════════════════════════════════
    //  Main Entry Point
    // ════════════════════════════════════════════════════

    /// <summary>
    /// Attempt to activate a ring's ability. Main entry from GameManager item use flow.
    /// Returns true if activation was initiated (action consumed or panel opened).
    /// Returns false if activation failed (no action, no uses, invalid state).
    /// </summary>
    public static bool TryActivateRing(CharacterController actor, ItemData ring, out string resultMessage)
    {
        resultMessage = string.Empty;

        if (actor == null || actor.Stats == null || ring == null || !ring.IsRing)
        {
            resultMessage = "Invalid ring or character.";
            return false;
        }

        if (ring.RingAbilities == null || ring.RingAbilities.Count == 0)
        {
            resultMessage = $"💍 {ring.Name} has no active abilities.";
            return false;
        }

        string charName = actor.Stats.CharacterName;

        // Single-ability rings: activate directly
        if (ring.RingAbilities.Count == 1)
        {
            return TryExecuteAbility(actor, ring, ring.RingAbilities[0], out resultMessage);
        }

        // Multi-ability rings: show selection (Ring of Shooting Stars, Telekinesis)
        return ShowAbilitySelection(actor, ring, out resultMessage);
    }

    /// <summary>
    /// Check if a ring has any active abilities (is an active Sprint 2 ring).
    /// </summary>
    public static bool HasActiveAbilities(ItemData ring)
    {
        return ring != null && ring.IsRing && ring.RingAbilities != null && ring.RingAbilities.Count > 0;
    }

    // ════════════════════════════════════════════════════
    //  Ability Execution
    // ════════════════════════════════════════════════════

    /// <summary>
    /// Execute a specific ring ability. Validates action economy, uses, and conditions.
    /// </summary>
    public static bool TryExecuteAbility(CharacterController actor, ItemData ring, RingAbility ability, out string resultMessage)
    {
        resultMessage = string.Empty;
        string charName = actor.Stats.CharacterName;
        string ringInstanceId = GetRingInstanceId(ring);

        // 1. Check restrictions (outdoors/night)
        if (ability.RequiresOutdoorsNight)
        {
            // Prototype: always allow but log the restriction
            Debug.Log($"[RingActivation] {ability.DisplayName} normally requires outdoors at night — allowing in prototype.");
        }

        // 2. Check use frequency
        switch (ability.Frequency)
        {
            case RingUseFrequency.PerDay:
                if (!RingUseTracker.Instance.HasDailyUsesRemaining(ringInstanceId, ability.AbilityId))
                {
                    resultMessage = $"💍 {ring.Name}: {ability.DisplayName} — no uses remaining today.";
                    return false;
                }
                break;
            case RingUseFrequency.PerWeek:
                if (!RingUseTracker.Instance.HasWeeklyUsesRemaining(ringInstanceId, ability.AbilityId))
                {
                    resultMessage = $"💍 {ring.Name}: {ability.DisplayName} — no uses remaining this week.";
                    return false;
                }
                break;
            case RingUseFrequency.Charged:
                int cost = ability.ChargeCost > 0 ? ability.ChargeCost : 1;
                if (!RingChargeManager.HasCharges(ring, cost))
                {
                    resultMessage = $"💍 {ring.Name}: not enough charges ({ring.RingCurrentCharges}/{ring.RingMaxCharges}).";
                    return false;
                }
                break;
        }

        // 3. Route to specific ring handler
        bool success = false;
        switch (ring.RingId)
        {
            case RingNames.RING_OF_INVISIBILITY:
                success = ActivateInvisibility(actor, ring, ability, out resultMessage);
                break;
            case RingNames.RING_OF_BLINKING:
                success = ActivateBlinking(actor, ring, ability, out resultMessage);
                break;
            case RingNames.RING_OF_TELEKINESIS:
                success = ActivateTelekinesis(actor, ring, ability, out resultMessage);
                break;
            case RingNames.RING_OF_ANIMAL_FRIENDSHIP:
                success = ActivateAnimalFriendship(actor, ring, ability, out resultMessage);
                break;
            case RingNames.RING_OF_RAM:
                success = ActivateRam(actor, ring, ability, out resultMessage);
                break;
            case RingNames.RING_OF_X_RAY_VISION:
                success = ActivateXRayVision(actor, ring, ability, out resultMessage);
                break;
            case RingNames.RING_OF_SHOOTING_STARS:
                success = ActivateShootingStarsAbility(actor, ring, ability, out resultMessage);
                break;
            case RingNames.RING_OF_DJINNI_CALLING:
                success = ActivateDjinniCalling(actor, ring, ability, out resultMessage);
                break;
            default:
                resultMessage = $"💍 {ring.Name}: unknown ring — no activation handler.";
                return false;
        }

        // 4. Consume uses on success
        if (success)
        {
            switch (ability.Frequency)
            {
                case RingUseFrequency.PerDay:
                    RingUseTracker.Instance.ConsumeDailyUse(ringInstanceId, ability.AbilityId);
                    break;
                case RingUseFrequency.PerWeek:
                    RingUseTracker.Instance.ConsumeWeeklyUse(ringInstanceId, ability.AbilityId);
                    break;
                // Charged: consumed inside the handler (variable cost)
            }
        }

        return success;
    }

    // ════════════════════════════════════════════════════
    //  Multi-Ability Selection
    // ════════════════════════════════════════════════════

    /// <summary>
    /// Show ability selection for multi-ability rings (Shooting Stars, Telekinesis).
    /// Builds a summary message and activates the first available ability as default.
    /// In a full UI implementation, this would open a selection panel.
    /// </summary>
    private static bool ShowAbilitySelection(CharacterController actor, ItemData ring, out string resultMessage)
    {
        // For prototype: Log available abilities and let GameManager handle UI panel
        // The GameManager_Rings partial class will handle the actual panel display
        resultMessage = "";
        string charName = actor.Stats.CharacterName;
        string ringInstanceId = GetRingInstanceId(ring);
        var tracker = RingUseTracker.Instance;

        var sb = new StringBuilder();
        sb.AppendLine($"💍 {charName} activates {ring.Name} — select an ability:");

        int availableCount = 0;
        RingAbility firstAvailable = null;

        foreach (var ability in ring.RingAbilities)
        {
            string usesStr = ability.GetUsesDisplayString(tracker, ringInstanceId);
            bool available = IsAbilityAvailable(ring, ability, ringInstanceId);
            string marker = available ? "✦" : "✗";

            if (available && firstAvailable == null)
                firstAvailable = ability;
            if (available)
                availableCount++;

            sb.AppendLine($"  [{marker}] {ability.DisplayName} ({usesStr})");
        }

        if (availableCount == 0)
        {
            resultMessage = $"💍 {ring.Name}: no abilities available at this time.";
            return false;
        }

        // Prototype: auto-select first available ability for simplicity
        // A full implementation would open a UI panel here
        resultMessage = sb.ToString();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CombatUI?.ShowCombatLog(sb.ToString());
        }

        // For multi-ability rings, we store the ring reference and let GameManager handle
        // the panel interaction. Return true to indicate activation initiated.
        // The GameManager_Rings partial class provides SelectRingAbility() for this.
        _pendingRingActivation = ring;
        _pendingRingActor = actor;
        return true;
    }

    // Pending activation state for multi-ability rings
    private static ItemData _pendingRingActivation;
    private static CharacterController _pendingRingActor;

    /// <summary>Get the pending ring for ability selection (used by UI).</summary>
    public static ItemData GetPendingRing() => _pendingRingActivation;

    /// <summary>Get the pending actor for ability selection (used by UI).</summary>
    public static CharacterController GetPendingActor() => _pendingRingActor;

    /// <summary>
    /// Complete a pending multi-ability ring activation with the selected ability index.
    /// Called from UI after player selects an ability.
    /// </summary>
    public static bool CompletePendingActivation(int abilityIndex, out string resultMessage)
    {
        resultMessage = "";
        if (_pendingRingActivation == null || _pendingRingActor == null)
        {
            resultMessage = "No pending ring activation.";
            return false;
        }

        if (abilityIndex < 0 || abilityIndex >= _pendingRingActivation.RingAbilities.Count)
        {
            resultMessage = "Invalid ability selection.";
            ClearPendingActivation();
            return false;
        }

        var ring = _pendingRingActivation;
        var actor = _pendingRingActor;
        var ability = ring.RingAbilities[abilityIndex];
        ClearPendingActivation();

        return TryExecuteAbility(actor, ring, ability, out resultMessage);
    }

    /// <summary>Cancel pending activation.</summary>
    public static void ClearPendingActivation()
    {
        _pendingRingActivation = null;
        _pendingRingActor = null;
    }

    // ════════════════════════════════════════════════════
    //  Availability Check
    // ════════════════════════════════════════════════════

    private static bool IsAbilityAvailable(ItemData ring, RingAbility ability, string ringInstanceId)
    {
        switch (ability.Frequency)
        {
            case RingUseFrequency.PerDay:
                return RingUseTracker.Instance.HasDailyUsesRemaining(ringInstanceId, ability.AbilityId);
            case RingUseFrequency.PerWeek:
                return RingUseTracker.Instance.HasWeeklyUsesRemaining(ringInstanceId, ability.AbilityId);
            case RingUseFrequency.Charged:
                int cost = ability.ChargeCost > 0 ? ability.ChargeCost : 1;
                return RingChargeManager.HasCharges(ring, cost);
            default:
                return true;
        }
    }

    // ════════════════════════════════════════════════════
    //  Ring Instance ID Helper
    // ════════════════════════════════════════════════════

    /// <summary>
    /// Get or generate a unique instance ID for a ring.
    /// Uses RingInstanceId if set, falls back to RingId.
    /// </summary>
    public static string GetRingInstanceId(ItemData ring)
    {
        if (!string.IsNullOrEmpty(ring.RingInstanceId))
            return ring.RingInstanceId;
        if (!string.IsNullOrEmpty(ring.RingId))
            return ring.RingId;
        return ring.Id ?? ring.Name ?? "unknown_ring";
    }

    // ════════════════════════════════════════════════════════════════
    //  RING HANDLERS — Individual Ring Activation Logic
    // ════════════════════════════════════════════════════════════════

    // ── Ring of Invisibility (DMG p.232) ──
    // At will, CL 3, standard action
    // Casts Invisibility on wearer: 3 min/level = 30 rounds at CL 3
    private static bool ActivateInvisibility(CharacterController actor, ItemData ring, RingAbility ability, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;
        int durationRounds = 30; // CL 3 × 10 rounds/level (Invisibility: 1 min/level)

        actor.ApplyInvisibilityEffect(durationRounds, actor);

        resultMessage = $"💍 {charName} speaks a command word and fades from sight. (Invisibility, {durationRounds} rounds)";
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);

        return true;
    }

    // ── Ring of Blinking (DMG p.230) ──
    // At will, CL 7, standard action
    // Casts Blink on wearer: 1 round/level = 7 rounds at CL 7
    private static bool ActivateBlinking(CharacterController actor, ItemData ring, RingAbility ability, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;
        int durationRounds = 7; // CL 7 × 1 round/level

        // Apply Blink via StatusEffectManager (same path as the Blink spell)
        var statusMgr = actor.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var blinkSpell = CreateRingSpellData(SpellNames.BLINK, "Blink", durationRounds);
            statusMgr.AddEffect(blinkSpell, "Ring of Blinking", 7);
        }

        resultMessage = $"💍 {charName} activates the Ring of Blinking. (Blink, {durationRounds} rounds — 50% miss chance incoming, 20% miss on own attacks)";
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);

        return true;
    }

    // ── Ring of Telekinesis (DMG p.233) ──
    // At will, CL 9, standard action
    // Three modes: Violent Thrust (primary), Combat Maneuver, Sustained Force
    private static bool ActivateTelekinesis(CharacterController actor, ItemData ring, RingAbility ability, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;

        // Route by ability sub-type
        if (ability.AbilityId == "telekinesis_violent_thrust")
        {
            return ActivateTelekinesisViolentThrust(actor, ring, out resultMessage);
        }
        else if (ability.AbilityId == "telekinesis_combat_maneuver")
        {
            return ActivateTelekinesisCombatManeuver(actor, ring, out resultMessage);
        }
        else if (ability.AbilityId == "telekinesis_sustained_force")
        {
            resultMessage = $"💍 {charName} telekinetically manipulates objects in the area. (Sustained Force — narrative effect)";
            if (GameManager.Instance != null)
                GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);
            return true;
        }

        // Default: violent thrust
        return ActivateTelekinesisViolentThrust(actor, ring, out resultMessage);
    }

    private static bool ActivateTelekinesisViolentThrust(CharacterController actor, ItemData ring, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;

        // Violent Thrust: hurl objects at targets
        // CL 9: up to 225 lbs (25 lbs/level), up to 9 objects (one per CL), 1d6 per 25 lbs
        // Simplified for prototype: deal 5d6 bludgeoning damage to target, Reflex DC 19 half
        // DC = 10 + spell level (5) + Int mod (assume +4 from item) = 19
        // Actually for items, DC is fixed: spell level 5 → DC = 10 + 5 + relevant mod
        // For prototype simplicity, use flat damage

        int damage = 0;
        for (int i = 0; i < 5; i++)
            damage += DiceRoller.D6(); // 5d6

        resultMessage = $"💍 {charName} hurls objects telekinetically! (Violent Thrust: {damage} bludgeoning damage, CL 9)";
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);

        return true;
    }

    private static bool ActivateTelekinesisCombatManeuver(CharacterController actor, ItemData ring, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;

        // Combat Maneuver: opposed CL check vs target for bull rush/disarm/trip
        // CL 9 + d20 vs target's relevant check
        int clCheck = DiceRoller.D20() + 9; // d20 + CL 9

        resultMessage = $"💍 {charName} uses telekinetic force! (Combat Maneuver: check {clCheck}, CL 9)";
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);

        return true;
    }

    // ── Ring of Animal Friendship (DMG p.229) ──
    // 3/day, CL 1, standard action
    // Charm Animal: Will DC 11, target must be Animal type
    // Max 12 HD of charmed animals simultaneously
    private static bool ActivateAnimalFriendship(CharacterController actor, ItemData ring, RingAbility ability, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;

        // In prototype: Apply charm effect as a status
        // Target validation (animal type) would be checked in targeting mode
        // For now, log the activation and apply a charm status effect

        int willSaveDC = 11;
        int durationRounds = 600; // 1 hour at CL 1 (1 hour/level)

        resultMessage = $"💍 {charName} commands the Ring of Animal Friendship! (Charm Animal: Will DC {willSaveDC}, 1 hour duration)";
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);

        // The actual targeting and save would be handled by the combat system
        // This logs the activation; targeting mode would be entered by GameManager

        return true;
    }

    // ── Ring of the Ram (DMG p.233) ──
    // Charge-based: 50 charges, expend 1–3 per use, regen 1d10/day
    // Force bolt: ranged touch attack, 1d6 per charge, bull rush Str 25 + charges
    private static bool ActivateRam(CharacterController actor, ItemData ring, RingAbility ability, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;
        int chargeCost = ability.ChargeCost > 0 ? ability.ChargeCost : 1;

        // Consume charges
        if (!RingChargeManager.ConsumeCharges(ring, chargeCost))
        {
            resultMessage = $"💍 Ring of the Ram: not enough charges!";
            return false;
        }

        // Calculate damage: 1d6 per charge
        int damage = 0;
        for (int i = 0; i < chargeCost; i++)
            damage += DiceRoller.D6();

        // Bull rush: Str 25 (+7 mod) + charges spent
        int bullRushBonus = 7 + chargeCost; // Str 25 mod + charge bonus
        int bullRushCheck = DiceRoller.D20() + bullRushBonus;

        var sb = new StringBuilder();
        sb.AppendLine($"💍 {charName} points the Ring of the Ram! ({chargeCost} charge{(chargeCost > 1 ? "s" : "")})");
        sb.AppendLine($"  Force bolt: {damage} force damage (ranged touch attack)");
        sb.AppendLine($"  Bull rush check: {bullRushCheck} (d20 + {bullRushBonus})");
        sb.Append($"  Charges remaining: {ring.RingCurrentCharges}/{ring.RingMaxCharges}");

        resultMessage = sb.ToString();
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);

        return true;
    }

    // ── Ring of X-Ray Vision (DMG p.233) ──
    // At will, CL 5, standard action
    // See through solid matter 20 ft, 10 rounds duration
    // Con damage on 2nd+ use per rest
    private static bool ActivateXRayVision(CharacterController actor, ItemData ring, RingAbility ability, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;
        string ringInstanceId = GetRingInstanceId(ring);
        int durationRounds = 10; // 1 minute

        // Track uses for Con damage
        int useCount = RingUseTracker.Instance.IncrementXRayUse(ringInstanceId);

        var sb = new StringBuilder();
        sb.AppendLine($"💍 {charName} peers through barriers with X-Ray vision! ({durationRounds} rounds)");
        sb.AppendLine($"  Reveals hidden/invisible creatures within 20 ft");
        sb.AppendLine($"  Penetrates: 20 ft stone/wood/dirt, 10 ft iron");

        // Apply See Invisibility-like effect
        var statusMgr = actor.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var xraySpell = CreateRingSpellData("xray_vision", "X-Ray Vision", durationRounds);
            statusMgr.AddEffect(xraySpell, "Ring of X-Ray Vision", 5);
        }

        // Con damage on repeated use (2nd+ per rest)
        if (useCount > 1)
        {
            int conDamage = DiceRoller.D4(); // 1d4 Con damage per DMG errata (simplified to 1 in some interpretations)
            // Apply ability damage
            if (actor.Stats != null)
            {
                actor.Stats.ApplyAbilityDamage(AbilityType.CON, conDamage);
                sb.AppendLine($"  ⚠ Strain from X-Ray use: {conDamage} Constitution damage! (use #{useCount} this rest)");
            }
        }

        resultMessage = sb.ToString();
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);

        return true;
    }

    // ── Ring of Shooting Stars (DMG p.233) ──
    // 5 abilities with mixed frequency tracking
    private static bool ActivateShootingStarsAbility(CharacterController actor, ItemData ring, RingAbility ability, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;

        switch (ability.AbilityId)
        {
            case "shooting_stars_dancing_lights":
                return ActivateDancingLights(actor, out resultMessage);
            case "shooting_stars_light":
                return ActivateLight(actor, out resultMessage);
            case "shooting_stars_ball_lightning":
                return ActivateBallLightning(actor, out resultMessage);
            case "shooting_stars_shooting_stars":
                return ActivateShootingStars(actor, out resultMessage);
            case "shooting_stars_faerie_fire":
                return ActivateFaerieFire(actor, out resultMessage);
            default:
                resultMessage = $"💍 Unknown Shooting Stars ability: {ability.AbilityId}";
                return false;
        }
    }

    private static bool ActivateDancingLights(CharacterController actor, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;
        int durationRounds = 10; // 1 minute (CL 12, but Dancing Lights is 1 min regardless)

        resultMessage = $"💍 {charName} conjures Dancing Lights from the Ring of Shooting Stars! ({durationRounds} rounds)";
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);
        return true;
    }

    private static bool ActivateLight(CharacterController actor, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;
        int durationRounds = 1200; // 120 minutes at CL 12 (10 min/level)

        resultMessage = $"💍 {charName} casts Light from the Ring of Shooting Stars! ({durationRounds / 10} minutes)";
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);
        return true;
    }

    private static bool ActivateBallLightning(CharacterController actor, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;

        // Choose 1–4 balls (prototype: auto-select for max damage efficiency)
        // 1 ball = 4d6, 2 = 3d6 each, 3 = 2d6 each, 4 = 1d6+1 each
        // For prototype: create 2 balls (3d6 each = 6d6 total, best average)
        int numBalls = 2;
        int dicePerBall = 3; // 3d6 for 2 balls

        var sb = new StringBuilder();
        sb.AppendLine($"💍 {charName} conjures {numBalls} balls of lightning from the Ring of Shooting Stars!");

        int totalDamage = 0;
        for (int b = 0; b < numBalls; b++)
        {
            int ballDamage = 0;
            for (int d = 0; d < dicePerBall; d++)
                ballDamage += DiceRoller.D6();
            totalDamage += ballDamage;
            sb.AppendLine($"  Ball {b + 1}: {ballDamage} electricity damage (Reflex DC 13 half)");
        }
        sb.Append($"  Total: {totalDamage} electricity damage");

        resultMessage = sb.ToString();
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);
        return true;
    }

    private static bool ActivateShootingStars(CharacterController actor, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;

        // Fire 1–3 shooting stars, each dealing 12 fire damage (flat) in 5-ft radius
        // Reflex DC 13 half
        int numStars = 3; // Prototype: fire all 3

        var sb = new StringBuilder();
        sb.AppendLine($"💍 {charName} fires {numStars} shooting stars from the Ring of Shooting Stars!");
        for (int s = 0; s < numStars; s++)
        {
            sb.AppendLine($"  Star {s + 1}: 12 fire damage in 5-ft radius (Reflex DC 13 half)");
        }
        sb.Append($"  Total: {numStars * 12} fire damage (if all hit)");

        resultMessage = sb.ToString();
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);
        return true;
    }

    private static bool ActivateFaerieFire(CharacterController actor, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;
        int durationRounds = 120; // 12 minutes at CL 12 (1 min/level)

        resultMessage = $"💍 {charName} casts Faerie Fire from the Ring of Shooting Stars! (Targets outlined, -20 Hide, invisible creatures revealed, {durationRounds / 10} minutes)";
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);
        return true;
    }

    // ── Ring of Spell Turning (DMG p.233) ──
    // Automatic — applied on equip, not via activation
    // Pool: 1d4+6 spell levels, refreshes on rest

    /// <summary>
    /// Apply Spell Turning effect when Ring of Spell Turning is equipped.
    /// Rolls 1d4+6 for the spell level pool.
    /// </summary>
    public static int ApplySpellTurningOnEquip(CharacterController wearer, ItemData ring)
    {
        if (wearer == null || ring == null) return 0;

        int turningLevels = DiceRoller.D4() + 6; // 1d4 + 6 = 7–10
        ring.RingSpellTurningPool = turningLevels;

        // Apply via StatusEffectManager using existing Spell Turning tag format
        var statusMgr = wearer.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var turningSpell = CreateRingSpellData(SpellNames.SPELL_TURNING, "Spell Turning", -1); // -1 = permanent
            turningSpell.DurationType = DurationType.Permanent;
            turningSpell.DurationValue = 0;
            var effect = statusMgr.AddEffect(turningSpell, "Ring of Spell Turning", 13);
            if (effect != null)
                effect.CustomTag = $"SpellTurning:{turningLevels}";
        }

        string msg = $"💍 {wearer.Stats?.CharacterName} dons the Ring of Spell Turning. ({turningLevels} spell levels of reflection)";
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(msg);

        Debug.Log($"[RingActivation] Spell Turning applied: {turningLevels} levels");
        return turningLevels;
    }

    /// <summary>
    /// Remove Spell Turning effect when Ring of Spell Turning is unequipped.
    /// </summary>
    public static void RemoveSpellTurningOnUnequip(CharacterController wearer)
    {
        if (wearer == null) return;

        var statusMgr = wearer.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            statusMgr.RemoveEffectsBySpellId(SpellNames.SPELL_TURNING);
        }

        Debug.Log("[RingActivation] Spell Turning removed on unequip.");
    }

    /// <summary>
    /// Refresh Spell Turning pool on rest (re-roll 1d4+6).
    /// </summary>
    public static void RefreshSpellTurningOnRest(CharacterController wearer, ItemData ring)
    {
        if (wearer == null || ring == null) return;
        if (ring.RingId != RingNames.RING_OF_SPELL_TURNING) return;

        RemoveSpellTurningOnUnequip(wearer);
        ApplySpellTurningOnEquip(wearer, ring);
    }

    // ── Ring of Djinni Calling (DMG p.232) ──
    // 1/week, CL 17, full-round action
    // Summon Noble Djinni for 1 hour (600 rounds)
    private static bool ActivateDjinniCalling(CharacterController actor, ItemData ring, RingAbility ability, out string resultMessage)
    {
        string charName = actor.Stats.CharacterName;

        // Check if djinni was slain (ring becomes permanently inert)
        if (ring.RingDjinniSlain)
        {
            resultMessage = $"💍 The Ring of Djinni Calling is inert — its bound djinni was slain.";
            if (GameManager.Instance != null)
                GameManager.Instance.CombatUI?.ShowCombatLog(resultMessage);
            return false;
        }

        int durationRounds = 600; // 1 hour

        // Attempt to spawn the Djinni via NPC database
        var gm = GameManager.Instance;
        if (gm == null)
        {
            resultMessage = "💍 Cannot summon — GameManager not available.";
            return false;
        }

        // Create summoning option for Noble Djinni
        var djinniOption = new SummonMonsterOption
        {
            DisplayName = "Noble Djinni",
            NpcDefinitionId = "noble_djinni",
            TemplateId = null,
            ClericOnly = false,
            SummonedCreatureAlignment = Alignment.ChaoticGood
        };

        // Find spawn position adjacent to caster
        Vector2Int spawnCell = FindAdjacentEmptyCell(actor);

        var sb = new StringBuilder();
        sb.AppendLine($"💍 {charName} calls forth the Noble Djinni from the ring!");
        sb.AppendLine($"  The Djinni appears to serve for 1 hour ({durationRounds} rounds).");
        sb.AppendLine($"  HP 45, AC 16, 2 slams +10 (1d8+6), Fly 60 ft");
        sb.Append($"  ⚠ If the Djinni is slain, the ring becomes permanently inert!");

        resultMessage = sb.ToString();
        if (gm.CombatUI != null)
            gm.CombatUI.ShowCombatLog(resultMessage);

        // Note: actual creature spawning is handled by GameManager_Rings partial class
        // which has access to SpawnSummonedCreature and the NPC spawning infrastructure
        ring.RingDjinniSummoned = true;

        return true;
    }

    /// <summary>
    /// Called when the summoned Djinni is killed. Marks the ring as permanently inert.
    /// </summary>
    public static void OnDjinniSlain(ItemData ring)
    {
        if (ring == null || ring.RingId != RingNames.RING_OF_DJINNI_CALLING) return;

        ring.RingDjinniSlain = true;
        ring.RingDjinniSummoned = false;

        string msg = "💍 The Noble Djinni is destroyed! The Ring of Djinni Calling becomes permanently inert.";
        if (GameManager.Instance != null)
            GameManager.Instance.CombatUI?.ShowCombatLog(msg);

        Debug.Log("[RingActivation] Djinni slain — Ring of Djinni Calling is now inert.");
    }

    // ════════════════════════════════════════════════════
    //  Utility Helpers
    // ════════════════════════════════════════════════════

    /// <summary>Find an adjacent empty cell for summoning.</summary>
    private static Vector2Int FindAdjacentEmptyCell(CharacterController actor)
    {
        // Simple: offset from actor position
        Vector2Int pos = actor.GridPosition;
        Vector2Int[] offsets = new Vector2Int[]
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(-1, -1),
            new Vector2Int(1, -1), new Vector2Int(-1, 1)
        };

        foreach (var offset in offsets)
        {
            Vector2Int candidate = pos + offset;
            // In a full implementation, check if cell is empty and passable
            return candidate;
        }

        return pos + Vector2Int.right; // fallback
    }

    // ════════════════════════════════════════════════════
    //  Rest Handler
    // ════════════════════════════════════════════════════

    /// <summary>
    /// Called on rest to reset ring daily/weekly uses and regenerate charges.
    /// Should be called from GameManager's rest handler.
    /// </summary>
    public static void OnRest(List<CharacterController> partyMembers)
    {
        // 1. Reset daily and weekly uses
        RingUseTracker.Instance.OnRest();

        // 2. Regenerate charges and refresh Spell Turning for all equipped rings
        if (partyMembers == null) return;

        foreach (var pc in partyMembers)
        {
            if (pc == null) continue;
            var invComp = pc.GetComponent<InventoryComponent>();
            if (invComp == null || invComp.CharacterInventory == null) continue;

            var inventory = invComp.CharacterInventory;
            ProcessRingOnRest(pc, inventory.LeftRingSlot);
            ProcessRingOnRest(pc, inventory.RightRingSlot);
        }

        Debug.Log("[RingActivation] Ring rest processing complete.");
    }

    private static void ProcessRingOnRest(CharacterController wearer, ItemData ring)
    {
        if (ring == null || !ring.IsRing) return;

        // Regenerate charges (Ring of the Ram)
        if (ring.RingMaxCharges > 0 && ring.RingChargesPerDay > 0)
        {
            RingChargeManager.RegenerateCharges(ring);
        }

        // Refresh Spell Turning pool
        if (ring.RingId == RingNames.RING_OF_SPELL_TURNING)
        {
            RefreshSpellTurningOnRest(wearer, ring);
        }

        // Reset djinni summoned flag
        if (ring.RingId == RingNames.RING_OF_DJINNI_CALLING)
        {
            ring.RingDjinniSummoned = false;
        }

        // Ring of Regeneration: apply hourly healing (simulated 8 hours of rest = 8× heal)
        if (ring.RingHasRegeneration)
        {
            var regenEffect = wearer.GetComponent<RegenerationEffect>();
            if (regenEffect != null)
            {
                // Rest = 8 hours, so apply 8 hourly heals
                int totalHealed = 0;
                for (int hour = 0; hour < 8; hour++)
                {
                    totalHealed += regenEffect.ApplyHourlyRegeneration();
                }
                if (totalHealed > 0 && GameManager.Instance != null)
                {
                    GameManager.Instance.CombatUI?.ShowCombatLog(
                        $"<color=#00FF88>💍 {wearer.Stats?.CharacterName}'s Ring of Regeneration heals {totalHealed} HP during rest.</color>");
                }
            }
        }
    }

    // ════════════════════════════════════════════════════
    //  Equip / Unequip Hooks
    // ════════════════════════════════════════════════════

    /// <summary>
    /// Called when an active ring is equipped. Registers use tracking and applies automatic effects.
    /// </summary>
    public static void OnRingEquipped(CharacterController wearer, ItemData ring)
    {
        if (ring == null || !ring.IsRing) return;

        string ringInstanceId = GetRingInstanceId(ring);

        // Register abilities for use tracking
        if (ring.RingAbilities != null)
        {
            foreach (var ability in ring.RingAbilities)
            {
                switch (ability.Frequency)
                {
                    case RingUseFrequency.PerDay:
                        RingUseTracker.Instance.RegisterDailyAbility(ringInstanceId, ability.AbilityId, ability.MaxUsesPerPeriod);
                        break;
                    case RingUseFrequency.PerWeek:
                        RingUseTracker.Instance.RegisterWeeklyAbility(ringInstanceId, ability.AbilityId, ability.MaxUsesPerPeriod);
                        break;
                }
            }
        }

        // Apply automatic effects — Sprint 2
        if (ring.RingId == RingNames.RING_OF_SPELL_TURNING)
        {
            ApplySpellTurningOnEquip(wearer, ring);
        }

        // Sprint 3: Ring of Regeneration — add MonoBehaviour component
        if (ring.RingHasRegeneration)
        {
            var existing = wearer.GetComponent<RegenerationEffect>();
            if (existing == null)
            {
                var regen = wearer.gameObject.AddComponent<RegenerationEffect>();
                regen.IsActive = true;
            }
            else
            {
                existing.IsActive = true;
            }
            Debug.Log($"[RingActivation] Regeneration effect applied to {wearer.Stats?.CharacterName}");
        }

        // Sprint 3: Ring of Wizardry — refresh spell slots to pick up doubled slots
        if (ring.RingWizardryLevel > 0)
        {
            var spellComp = wearer.GetComponent<SpellcastingComponent>();
            if (spellComp != null)
            {
                spellComp.RefreshSpellSlots();
                Debug.Log($"[RingActivation] Spell slots refreshed for Ring of Wizardry {ring.RingWizardryLevel}");
            }
        }

        Debug.Log($"[RingActivation] Ring equipped: {ring.Name} (instance: {ringInstanceId})");
    }

    /// <summary>
    /// Called when an active ring is unequipped. Cleans up tracking and removes automatic effects.
    /// </summary>
    public static void OnRingUnequipped(CharacterController wearer, ItemData ring)
    {
        if (ring == null || !ring.IsRing) return;

        string ringInstanceId = GetRingInstanceId(ring);

        // Remove automatic effects — Sprint 2
        if (ring.RingId == RingNames.RING_OF_SPELL_TURNING)
        {
            RemoveSpellTurningOnUnequip(wearer);
        }

        // Sprint 3: Ring of Regeneration — disable regeneration
        if (ring.RingHasRegeneration)
        {
            var regen = wearer.GetComponent<RegenerationEffect>();
            if (regen != null)
            {
                regen.IsActive = false;
                Object.Destroy(regen);
            }
            Debug.Log($"[RingActivation] Regeneration effect removed from {wearer.Stats?.CharacterName}");
        }

        // Sprint 3: Ring of Wizardry — refresh spell slots to remove doubled slots
        if (ring.RingWizardryLevel > 0)
        {
            var spellComp = wearer.GetComponent<SpellcastingComponent>();
            if (spellComp != null)
            {
                spellComp.RefreshSpellSlots();
                Debug.Log($"[RingActivation] Spell slots refreshed after removing Ring of Wizardry {ring.RingWizardryLevel}");
            }
        }

        // Unregister from use tracker
        RingUseTracker.Instance.UnregisterRing(ringInstanceId);

        Debug.Log($"[RingActivation] Ring unequipped: {ring.Name}");
    }

    // ══════════════════════════════════════════════════════════════════════
    //  UTILITY: Synthetic SpellData for ring-granted status effects
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a lightweight SpellData object for ring-granted effects so they
    /// can flow through the existing StatusEffectManager.AddEffect() pipeline.
    /// Duration is specified directly in rounds (DurationType.Rounds).
    /// </summary>
    private static SpellData CreateRingSpellData(string spellId, string displayName, int durationRounds)
    {
        var spell = new SpellData
        {
            SpellId = spellId,
            Name = displayName,
            Description = $"Effect granted by a magic ring ({displayName})",
            SpellLevel = 0,
            School = "Transmutation",
            DurationType = DurationType.Rounds,
            DurationValue = durationRounds,
            DurationScalesWithLevel = false,
            BuffDurationRounds = durationRounds
        };
        return spell;
    }
}

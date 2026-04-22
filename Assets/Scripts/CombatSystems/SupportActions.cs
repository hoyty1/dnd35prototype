using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SupportActions : BaseCombatManeuver
{
}

public partial class GameManager
{
    public enum AidType
    {
        Defense,
        Offense
    }

    [Serializable]
    public class AidBonus
    {
        public CharacterController Aider;
        public CharacterController Beneficiary;
        public CharacterController Target;
        public AidType Type;
        public int Bonus;
        public int RoundGranted;
        // 2 turn-starts means: usable during the beneficiary's next turn, then expires on the following turn start if unused.
        public int BeneficiaryTurnStartsRemainingBeforeExpiry = 2;

        public bool IsInvalid()
        {
            return Aider == null
                || Beneficiary == null
                || Target == null
                || Beneficiary.Stats == null
                || Target.Stats == null
                || Beneficiary.Stats.IsDead
                || Target.Stats.IsDead;
        }
    }

    public readonly List<AidBonus> ActiveAidBonuses = new List<AidBonus>();

    // Aid Another multi-step selection state
    private CharacterController _aidAnotherSelectedEnemy;
    private AidType? _aidAnotherSelectedType;

    // Pending charge state
    private CharacterController _chargeTarget;
    private List<Vector2Int> _pendingChargePath = new List<Vector2Int>();
    private bool _pendingChargeBullRush;

    private bool CanAttemptAidAnotherTouch(CharacterController actor, CharacterController enemy)
    {
        if (actor == null || enemy == null || actor.Stats == null || enemy.Stats == null)
            return false;
        if (!IsEnemyTeam(actor, enemy) || enemy.Stats.IsDead)
            return false;

        ItemData weapon = actor.GetEquippedWeapon();
        return actor.ThreatensWith(enemy, weapon);
    }

    /// <summary>
    /// Get all allies of the given character (excluding dead and self).
    /// </summary>
    private List<CharacterController> GetAllAllies(CharacterController character)
    {
        var allies = new List<CharacterController>();
        if (character == null)
            return allies;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == character || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsAllyTeam(character, candidate))
                continue;

            allies.Add(candidate);
        }

        return allies;
    }

    /// <summary>
    /// Get all enemies of the given character (excluding dead).
    /// </summary>
    private List<CharacterController> GetAllEnemies(CharacterController character)
    {
        var enemies = new List<CharacterController>();
        if (character == null)
            return enemies;

        foreach (CharacterController candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == character || candidate.Stats == null || candidate.Stats.IsDead)
                continue;
            if (!IsEnemyTeam(character, candidate))
                continue;

            enemies.Add(candidate);
        }

        return enemies;
    }

    /// <summary>
    /// Get all enemies threatened by the initiator with the currently equipped melee weapon (or unarmed).
    /// </summary>
    private List<CharacterController> GetThreatenedEnemies(CharacterController initiator)
    {
        var threatenedEnemies = new List<CharacterController>();
        if (initiator == null)
            return threatenedEnemies;

        ItemData weapon = initiator.GetEquippedWeapon();
        List<CharacterController> allEnemies = GetAllEnemies(initiator);
        for (int i = 0; i < allEnemies.Count; i++)
        {
            CharacterController enemy = allEnemies[i];
            if (initiator.ThreatensWith(enemy, weapon))
                threatenedEnemies.Add(enemy);
        }

        string initiatorName = initiator.Stats != null ? initiator.Stats.CharacterName : "Unknown";
        string weaponName = weapon != null ? weapon.Name : "unarmed";
        Debug.Log($"[AidAnother] {initiatorName} threatens {threatenedEnemies.Count} enemies with {weaponName}");
        return threatenedEnemies;
    }

    /// <summary>
    /// Get all allies (excluding initiator) that are within melee threat range of the selected enemy.
    /// </summary>
    private List<CharacterController> GetAlliesInMeleeRange(CharacterController target, CharacterController excludeInitiator)
    {
        var alliesInRange = new List<CharacterController>();
        if (target == null)
            return alliesInRange;

        List<CharacterController> allAllies = GetAllAllies(excludeInitiator);
        for (int i = 0; i < allAllies.Count; i++)
        {
            CharacterController ally = allAllies[i];
            if (ally == null || ally == excludeInitiator)
                continue;

            ItemData allyWeapon = ally.GetEquippedWeapon();
            if (ally.ThreatensWith(target, allyWeapon))
                alliesInRange.Add(ally);
        }

        string targetName = target.Stats != null ? target.Stats.CharacterName : "Unknown";
        Debug.Log($"[AidAnother] {alliesInRange.Count} allies in melee range of {targetName}");
        return alliesInRange;
    }

    private static int GetAidAnotherTouchAttackModifier(CharacterController actor)
    {
        if (actor == null || actor.Stats == null)
            return 0;

        return actor.Stats.BaseAttackBonus + actor.Stats.STRMod + actor.Stats.SizeModifier + actor.Stats.ConditionAttackPenalty;
    }

    private static string GetCombatantName(CharacterController c)
    {
        return c != null && c.Stats != null ? c.Stats.CharacterName : "Unknown";
    }

    private static string GetAidTypeLabel(AidType type)
    {
        return type == AidType.Defense ? "defense" : "offense";
    }

    private string DescribeAidBonus(AidBonus bonus)
    {
        if (bonus == null)
            return "<null>";

        return $"type={bonus.Type}, aider={GetCombatantName(bonus.Aider)}, beneficiary={GetCombatantName(bonus.Beneficiary)}, target={GetCombatantName(bonus.Target)}, amount=+{bonus.Bonus}, roundGranted={bonus.RoundGranted}, startsRemaining={bonus.BeneficiaryTurnStartsRemainingBeforeExpiry}";
    }

    private void LogAidBonusSnapshot(string context)
    {
        Debug.Log($"[AidBonus][{context}] Active bonuses tracked: {ActiveAidBonuses.Count}");
        for (int i = 0; i < ActiveAidBonuses.Count; i++)
            Debug.Log($"[AidBonus][{context}] Bonus[{i}] {DescribeAidBonus(ActiveAidBonuses[i])}");
    }
    private void PruneInvalidAidBonuses()
    {
        int before = ActiveAidBonuses.Count;
        ActiveAidBonuses.RemoveAll(b => b == null || b.IsInvalid() || b.Bonus <= 0);
        int removed = before - ActiveAidBonuses.Count;
        if (removed > 0)
            Debug.Log($"[AidBonus][Prune] Removed {removed} invalid/expired entries.");
    }

    private void NotifyAidBonusStateChanged(params CharacterController[] affectedCharacters)
    {
        if (affectedCharacters == null || affectedCharacters.Length == 0)
            return;

        CharacterSheetUI sheet = CharacterSheetUI;
        if (sheet == null || !sheet.IsOpen)
            return;

        var uniqueCharacters = new HashSet<CharacterController>();
        for (int i = 0; i < affectedCharacters.Length; i++)
        {
            CharacterController c = affectedCharacters[i];
            if (c != null)
                uniqueCharacters.Add(c);
        }

        if (uniqueCharacters.Count > 0)
            sheet.RefreshIfOpen();
    }

    /// <summary>
    /// Get all active Aid Another bonuses where the given character is the beneficiary.
    /// </summary>
    public List<AidBonus> GetAidBonusesForCharacter(CharacterController character)
    {
        var bonuses = new List<AidBonus>();
        if (character == null)
            return bonuses;

        PruneInvalidAidBonuses();

        for (int i = 0; i < ActiveAidBonuses.Count; i++)
        {
            AidBonus bonus = ActiveAidBonuses[i];
            if (bonus != null && bonus.Beneficiary == character)
                bonuses.Add(bonus);
        }

        Debug.Log($"[AidBonus][Query] {GetCombatantName(character)} has {bonuses.Count} active Aid Another bonuses.");
        return bonuses;
    }

    /// <summary>
    /// Get active offense Aid Another bonuses for attacker against the specific target.
    /// </summary>
    public List<AidBonus> GetAidAnotherAttackBonuses(CharacterController attacker, CharacterController target)
    {
        var bonuses = new List<AidBonus>();
        if (attacker == null || target == null)
            return bonuses;

        PruneInvalidAidBonuses();

        for (int i = 0; i < ActiveAidBonuses.Count; i++)
        {
            AidBonus bonus = ActiveAidBonuses[i];
            if (bonus == null)
                continue;
            if (bonus.Type == AidType.Offense && bonus.Beneficiary == attacker && bonus.Target == target)
                bonuses.Add(bonus);
        }

        return bonuses;
    }

    /// <summary>
    /// Get active defense Aid Another bonuses for defender against a specific attacker.
    /// </summary>
    public List<AidBonus> GetAidAnotherDefenseBonuses(CharacterController defender, CharacterController attacker)
    {
        var bonuses = new List<AidBonus>();
        if (defender == null || attacker == null)
            return bonuses;

        PruneInvalidAidBonuses();

        for (int i = 0; i < ActiveAidBonuses.Count; i++)
        {
            AidBonus bonus = ActiveAidBonuses[i];
            if (bonus == null)
                continue;
            if (bonus.Type == AidType.Defense && bonus.Beneficiary == defender && bonus.Target == attacker)
                bonuses.Add(bonus);
        }

        return bonuses;
    }

    public bool CanUseAidAnother(CharacterController actor, out string reason)
    {
        reason = "Unavailable";
        if (actor == null || actor.Stats == null)
            return false;

        if (!actor.Actions.HasStandardAction)
        {
            reason = "Used";
            return false;
        }

        // D&D 3.5e: Aid Another is a standard action and is allowed while prone.
        // Intentionally no prone block here.
        if (actor.HasCondition(CombatConditionType.Prone))
            Debug.Log($"[AidAnother][Availability] {actor.Stats.CharacterName} is prone but still allowed to attempt Aid Another.");

        if (actor.HasCondition(CombatConditionType.Pinned))
        {
            reason = "Pinned";
            return false;
        }

        if (GetAllAllies(actor).Count == 0)
        {
            reason = "No ally";
            return false;
        }

        if (GetThreatenedEnemies(actor).Count == 0)
        {
            reason = "No target";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void AddAidBonus(CharacterController aider, CharacterController beneficiary, CharacterController target, AidType aidType)
    {
        var aidBonus = new AidBonus
        {
            Aider = aider,
            Beneficiary = beneficiary,
            Target = target,
            Type = aidType,
            Bonus = 2,
            RoundGranted = _currentRound,
            BeneficiaryTurnStartsRemainingBeforeExpiry = 2
        };

        ActiveAidBonuses.Add(aidBonus);

        string aiderName = GetCombatantName(aider);
        string beneficiaryName = GetCombatantName(beneficiary);
        string targetName = GetCombatantName(target);
        Debug.Log($"[AidBonus][Grant] {aiderName} grants {aidType} to {beneficiaryName} vs {targetName}");
        LogAidBonusSnapshot("Grant");

        int totalForPair = 0;
        var names = new List<string>();

        for (int i = 0; i < ActiveAidBonuses.Count; i++)
        {
            AidBonus b = ActiveAidBonuses[i];
            if (b == null || b.Beneficiary != beneficiary || b.Target != target || b.Type != aidType)
                continue;

            totalForPair += b.Bonus;
            if (b.Aider != null && b.Aider.Stats != null)
                names.Add(b.Aider.Stats.CharacterName);
        }

        string sourceList = names.Count > 0 ? string.Join(", ", names) : "unknown";
        string expiryHint = $"expires if unused by the start of {beneficiaryName}'s following turn";
        if (aidType == AidType.Defense)
            CombatUI?.ShowCombatLog($"✅ {beneficiaryName} gains +{totalForPair} AC vs {targetName} (from {sourceList}; {expiryHint}).");
        else
            CombatUI?.ShowCombatLog($"✅ {beneficiaryName} gains +{totalForPair} attack vs {targetName} (from {sourceList}; {expiryHint}).");

        NotifyAidBonusStateChanged(beneficiary);
    }

    public int ConsumeAidAnotherAttackBonus(CharacterController attacker, CharacterController defender)
    {
        if (attacker == null || defender == null)
            return 0;

        Debug.Log($"[AidBonus][Attack] Checking offense bonuses for {GetCombatantName(attacker)} attacking {GetCombatantName(defender)}");
        PruneInvalidAidBonuses();
        LogAidBonusSnapshot("Attack-PreConsume");

        int totalBonus = 0;
        var consumed = new List<AidBonus>();
        for (int i = 0; i < ActiveAidBonuses.Count; i++)
        {
            AidBonus bonus = ActiveAidBonuses[i];
            Debug.Log($"[AidBonus][Attack] Inspect Bonus[{i}] {DescribeAidBonus(bonus)}");
            if (bonus.Beneficiary == attacker && bonus.Target == defender && bonus.Type == AidType.Offense)
            {
                totalBonus += Mathf.Max(0, bonus.Bonus);
                consumed.Add(bonus);
                Debug.Log($"[AidBonus][Attack] MATCH! Applying +{bonus.Bonus} from {GetCombatantName(bonus.Aider)}");
            }
        }

        if (totalBonus > 0)
        {
            string from = string.Join(", ", consumed.ConvertAll(b => GetCombatantName(b.Aider)));
            CombatUI?.ShowCombatLog($"🤝 Aid offense consumed: +{totalBonus} attack for {attacker.Stats.CharacterName} vs {defender.Stats.CharacterName} (from {from}).");
            for (int i = 0; i < consumed.Count; i++)
                ActiveAidBonuses.Remove(consumed[i]);
            Debug.Log($"[AidBonus][Attack] Consumed {consumed.Count} offense bonuses; total applied +{totalBonus}");
            NotifyAidBonusStateChanged(attacker);
        }
        else
        {
            Debug.Log("[AidBonus][Attack] No matching offense bonuses found.");
        }

        LogAidBonusSnapshot("Attack-PostConsume");
        return totalBonus;
    }

    public int ConsumeAidAnotherAcBonus(CharacterController attacker, CharacterController defender)
    {
        if (attacker == null || defender == null)
            return 0;

        Debug.Log($"[AidBonus][Defense] Checking defense bonuses for {GetCombatantName(defender)} vs attacker {GetCombatantName(attacker)}");
        PruneInvalidAidBonuses();

        int totalBonus = 0;
        var consumed = new List<AidBonus>();
        for (int i = 0; i < ActiveAidBonuses.Count; i++)
        {
            AidBonus bonus = ActiveAidBonuses[i];
            Debug.Log($"[AidBonus][Defense] Inspect Bonus[{i}] {DescribeAidBonus(bonus)}");
            if (bonus.Beneficiary == defender && bonus.Target == attacker && bonus.Type == AidType.Defense)
            {
                totalBonus += Mathf.Max(0, bonus.Bonus);
                consumed.Add(bonus);
                Debug.Log($"[AidBonus][Defense] MATCH! Applying +{bonus.Bonus} from {GetCombatantName(bonus.Aider)}");
            }
        }

        if (totalBonus > 0)
        {
            string from = string.Join(", ", consumed.ConvertAll(b => GetCombatantName(b.Aider)));
            CombatUI?.ShowCombatLog($"🛡 Aid defense consumed: +{totalBonus} AC for {defender.Stats.CharacterName} vs {attacker.Stats.CharacterName} (from {from}).");
            for (int i = 0; i < consumed.Count; i++)
                ActiveAidBonuses.Remove(consumed[i]);
            Debug.Log($"[AidBonus][Defense] Consumed {consumed.Count} defense bonuses; total applied +{totalBonus}");
            NotifyAidBonusStateChanged(defender);
        }
        else
        {
            Debug.Log("[AidBonus][Defense] No matching defense bonuses found.");
        }

        LogAidBonusSnapshot("Defense-PostConsume");
        return totalBonus;
    }

    private void ExpireAidBonusesAtTurnStart(CharacterController beneficiary)
    {
        if (beneficiary == null)
            return;

        Debug.Log($"[AidBonus][Expire] Turn start for {GetCombatantName(beneficiary)} - evaluating expirations");
        PruneInvalidAidBonuses();

        var expiring = new List<AidBonus>();
        for (int i = 0; i < ActiveAidBonuses.Count; i++)
        {
            AidBonus bonus = ActiveAidBonuses[i];
            if (bonus == null || bonus.Beneficiary != beneficiary)
                continue;

            bonus.BeneficiaryTurnStartsRemainingBeforeExpiry = Mathf.Max(0, bonus.BeneficiaryTurnStartsRemainingBeforeExpiry - 1);
            Debug.Log($"[AidBonus][Expire] Decremented Bonus[{i}] -> {DescribeAidBonus(bonus)}");
            if (bonus.BeneficiaryTurnStartsRemainingBeforeExpiry <= 0)
                expiring.Add(bonus);
        }

        for (int i = 0; i < expiring.Count; i++)
        {
            AidBonus bonus = expiring[i];
            CombatUI?.ShowCombatLog($"⌛ Aid {GetAidTypeLabel(bonus.Type)} bonus expired: {GetCombatantName(bonus.Aider)} → {GetCombatantName(bonus.Beneficiary)} vs {GetCombatantName(bonus.Target)} (+{bonus.Bonus}).");
            Debug.Log($"[AidBonus][Expire] Removing expired bonus: {DescribeAidBonus(bonus)}");
            ActiveAidBonuses.Remove(bonus);
        }

        if (expiring.Count > 0)
            NotifyAidBonusStateChanged(beneficiary);

        LogAidBonusSnapshot("Expire-PostTurnStart");
    }

    public void DisplayActiveAidBonuses(CharacterController character)
    {
        if (character == null)
            return;

        PruneInvalidAidBonuses();

        var bonuses = new List<AidBonus>();
        for (int i = 0; i < ActiveAidBonuses.Count; i++)
        {
            AidBonus bonus = ActiveAidBonuses[i];
            if (bonus.Beneficiary == character)
                bonuses.Add(bonus);
        }

        if (bonuses.Count == 0)
            return;

        CombatUI?.ShowCombatLog($"📋 {character.Stats.CharacterName}'s active Aid Another bonuses:");

        for (int i = 0; i < bonuses.Count; i++)
        {
            AidBonus bonus = bonuses[i];
            if (bonus.Type == AidType.Defense)
                CombatUI?.ShowCombatLog($"   Defense: +{bonus.Bonus} AC vs {GetCombatantName(bonus.Target)} (from {GetCombatantName(bonus.Aider)})");
            else
                CombatUI?.ShowCombatLog($"   Offense: +{bonus.Bonus} attack vs {GetCombatantName(bonus.Target)} (from {GetCombatantName(bonus.Aider)})");
        }
    }

    public void OnAidAnotherButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null)
            return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "Aid Another"))
            return;

        if (!CanUseAidAnother(pc, out string reason))
        {
            CombatUI?.ShowCombatLog($"⚠ {pc.Stats.CharacterName} cannot Aid Another: {reason}.");
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        _aidAnotherSelectedEnemy = null;
        _aidAnotherSelectedType = null;
        Debug.Log($"[AidAnother] {pc.Stats.CharacterName} initiating Aid Another");

        // New D&D 3.5e flow: Enemy -> Aid Type -> Ally
        ShowAidAnotherEnemySelection(pc);
    }

    private void ShowAidAnotherEnemySelection(CharacterController initiator)
    {
        if (initiator == null || initiator.Stats == null)
        {
            ShowActionChoices();
            return;
        }

        List<CharacterController> threatenedEnemies = GetThreatenedEnemies(initiator);
        if (threatenedEnemies.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ {initiator.Stats.CharacterName} doesn't threaten any enemies!");
            ShowActionChoices();
            return;
        }

        Debug.Log($"[AidAnother] Showing enemy selection for {initiator.Stats.CharacterName} | threatenedCount={threatenedEnemies.Count}");

        CombatUI?.ShowCharacterSelectionUI(
            title: "Aid Another - Select Enemy",
            body: $"{initiator.Stats.CharacterName}, choose an enemy you threaten:",
            characters: threatenedEnemies,
            onSelect: enemy => OnAidAnotherEnemySelected(initiator, enemy),
            onCancel: () =>
            {
                Debug.Log("[AidAnother] Enemy selection cancelled");
                ShowActionChoices();
            },
            optionButtonColorOverride: new Color(0.6f, 0.2f, 0.2f, 1f));
    }

    private void OnAidAnotherEnemySelected(CharacterController initiator, CharacterController enemy)
    {
        if (initiator == null || enemy == null)
        {
            ShowActionChoices();
            return;
        }

        _aidAnotherSelectedEnemy = enemy;
        Debug.Log($"[AidAnother] Enemy selected: {enemy.Stats?.CharacterName ?? "Unknown"}");

        ShowAidAnotherTypeSelection(initiator, enemy);
    }

    private void ShowAidAnotherTypeSelection(CharacterController initiator, CharacterController enemy)
    {
        if (initiator == null || enemy == null)
        {
            ShowActionChoices();
            return;
        }

        Debug.Log($"[AidAnother] Showing aid type selection for {initiator.Stats.CharacterName} vs {enemy.Stats.CharacterName}");

        var options = new List<string>
        {
            "Aid Defense (+2 AC)",
            "Aid Offense (+2 Attack)"
        };

        CombatUI?.ShowPickUpItemSelection(
            initiator.Stats.CharacterName,
            options,
            onSelect: selectedIndex =>
            {
                AidType selectedType = selectedIndex == 0 ? AidType.Defense : AidType.Offense;
                OnAidAnotherTypeSelected(initiator, enemy, selectedType);
            },
            onCancel: () =>
            {
                Debug.Log("[AidAnother] Aid type selection cancelled");
                ShowActionChoices();
            },
            titleOverride: $"Aid Another - {enemy.Stats.CharacterName}",
            bodyOverride: "Choose aid type:",
            optionButtonColorOverride: new Color(0.3f, 0.4f, 0.6f, 1f));
    }

    private void OnAidAnotherTypeSelected(CharacterController initiator, CharacterController enemy, AidType aidType)
    {
        if (initiator == null || enemy == null)
        {
            ShowActionChoices();
            return;
        }

        _aidAnotherSelectedType = aidType;
        Debug.Log($"[AidAnother] Aid type selected: {aidType}");

        ShowAidAnotherAllySelection(initiator, enemy, aidType);
    }

    private void ShowAidAnotherAllySelection(CharacterController initiator, CharacterController enemy, AidType aidType)
    {
        if (initiator == null || enemy == null)
        {
            ShowActionChoices();
            return;
        }

        List<CharacterController> alliesInRange = GetAlliesInMeleeRange(enemy, initiator);
        if (alliesInRange.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ No allies in melee range of {enemy.Stats.CharacterName}!");
            ShowActionChoices();
            return;
        }

        Debug.Log($"[AidAnother] Showing ally selection for {initiator.Stats.CharacterName} | alliesInRange={alliesInRange.Count}");

        string body = aidType == AidType.Defense
            ? $"Choose ally to gain +2 AC against {enemy.Stats.CharacterName}."
            : $"Choose ally to gain +2 attack against {enemy.Stats.CharacterName}.";

        CombatUI?.ShowCharacterSelectionUI(
            title: $"Aid {aidType} - Select Ally",
            body: body,
            characters: alliesInRange,
            onSelect: ally => OnAidAnotherAllySelected(initiator, ally, enemy, aidType),
            onCancel: () =>
            {
                Debug.Log("[AidAnother] Ally selection cancelled");
                ShowActionChoices();
            },
            optionButtonColorOverride: new Color(0.34f, 0.28f, 0.56f, 1f));
    }

    private void OnAidAnotherAllySelected(CharacterController initiator, CharacterController ally, CharacterController enemy, AidType aidType)
    {
        if (initiator == null || ally == null || enemy == null)
        {
            ShowActionChoices();
            return;
        }

        Debug.Log($"[AidAnother] Ally selected: {ally.Stats?.CharacterName ?? "Unknown"} | enemy={enemy.Stats?.CharacterName ?? "Unknown"} | type={aidType}");
        ExecuteAidAnother(initiator, ally, enemy, aidType);
    }

    public void ExecuteAidAnother(CharacterController aider, CharacterController ally, CharacterController enemy, AidType aidType)
    {
        if (aider == null || ally == null || enemy == null)
        {
            ShowActionChoices();
            return;
        }

        if (!CanAttemptAidAnotherTouch(aider, enemy))
        {
            CombatUI?.ShowCombatLog($"⚠ {GetCombatantName(enemy)} is not in melee reach for Aid Another.");
            ShowActionChoices();
            return;
        }

        if (!ally.ThreatensWith(enemy, ally.GetEquippedWeapon()))
        {
            CombatUI?.ShowCombatLog($"⚠ {GetCombatantName(ally)} is no longer in melee range of {GetCombatantName(enemy)}.");
            ShowActionChoices();
            return;
        }

        CombatUI?.ShowCombatLog($"🤝 {aider.Stats.CharacterName} aids {ally.Stats.CharacterName}'s {GetAidTypeLabel(aidType)} vs {enemy.Stats.CharacterName}.");

        int touchAtkMod = GetAidAnotherTouchAttackModifier(aider);
        (bool hit, int roll, int total) = aider.Stats.RollToHitWithMod(touchAtkMod, 10);

        CombatUI?.ShowCombatLog("Melee touch attack vs AC 10:");
        CombatUI?.ShowCombatLog($"  Roll: 1d20 = {roll}");
        CombatUI?.ShowCombatLog($"  Modifier: {CharacterStats.FormatMod(touchAtkMod)}");
        CombatUI?.ShowCombatLog($"  Total: {total}");

        aider.CommitStandardAction();

        if (!hit)
        {
            CombatUI?.ShowCombatLog("❌ Aid Another failed (needed 10).");
            UpdateAllStatsUI();
            StartCoroutine(AfterAttackDelay(aider, 0.9f));
            return;
        }

        CombatUI?.ShowCombatLog("✅ Aid Another success!");
        AddAidBonus(aider, ally, enemy, aidType);
        DisplayActiveAidBonuses(ally);

        UpdateAllStatsUI();
        StartCoroutine(AfterAttackDelay(aider, 0.9f));
    }

    public void OnChargeButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        if (RedirectPinnedCharacterToGrappleMenu(pc, "charging"))
            return;

        if (IsActionBlockedByTurnedCondition(pc, "charge"))
        {
            CombatUI?.UpdateActionButtons(pc);
            return;
        }

        _pendingChargeBullRush = false;
        EnterChargeMode(pc);
    }

    private void UpdateChargeHoverPreview()
    {
        if (_mainCam == null || ActivePC == null) return;
        if (CurrentSubPhase != PlayerSubPhase.SelectingChargeTarget) return;

        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Vector3 mouseScreenPos = Vector3.zero;
#if ENABLE_LEGACY_INPUT_MANAGER

        mouseScreenPos = Input.mousePosition;
#endif
#if ENABLE_INPUT_SYSTEM
        var mouseDev = UnityEngine.InputSystem.Mouse.current;
        if (mouseDev != null)
            mouseScreenPos = mouseDev.position.ReadValue();
#endif

        Vector2 worldPoint = _mainCam.ScreenToWorldPoint(mouseScreenPos);
        Vector2Int gridCoord = SquareGridUtils.WorldToGrid(worldPoint);
        SquareCell hovered = Grid.GetCell(gridCoord);
        if (hovered == null || !hovered.IsOccupied || hovered.Occupant == null || hovered.Occupant == ActivePC)
            return;

        CharacterController target = hovered.Occupant;
        if (!CanChargeTarget(ActivePC, target, logFailures: false))
            return;

        var previewPath = GetChargePath(ActivePC, target);
        int pathCost = SquareGridUtils.CalculatePathCost(ActivePC.GridPosition, previewPath);
        CombatUI.SetTurnIndicator($"CHARGE ready: {target.Stats.CharacterName} ({pathCost * 5} ft). Click target to preview and confirm.");
    }

    public bool HasAnyValidChargeTarget(CharacterController charger)
    {
        if (charger == null) return false;
        var validTargets = GetValidChargeTargets(charger, false);
        return validTargets.Count > 0;
    }

    private void EnterBullRushChargeMode(CharacterController charger)
    {
        _pendingChargeBullRush = true;
        EnterChargeMode(charger);
    }

    public void EnterChargeMode(CharacterController charger)
    {
        if (charger == null) return;

        if (charger.HasTakenFiveFootStep)
        {
            CombatUI?.ShowCombatLog($"⚠ {charger.Stats.CharacterName} cannot charge after taking a 5-foot step.");
            return;
        }

        if (!charger.Actions.HasFullRoundAction)
        {
            CombatUI?.ShowCombatLog($"⚠ {charger.Stats.CharacterName} needs a full-round action to charge.");
            return;
        }

        if (!charger.HasMeleeWeaponEquipped())
        {
            CombatUI?.ShowCombatLog($"⚠ {charger.Stats.CharacterName} needs a melee weapon (or natural/unarmed attack) to charge.");
            return;
        }

        var validTargets = GetValidChargeTargets(charger, true);
        if (validTargets.Count == 0)
        {
            CombatUI?.ShowCombatLog($"⚠ No valid charge targets for {charger.Stats.CharacterName}.");
            return;
        }

        _chargeTarget = null;
        _pendingChargePath.Clear();

        CurrentSubPhase = PlayerSubPhase.SelectingChargeTarget;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        foreach (var t in validTargets)
        {
            HighlightCharacterFootprint(t, HighlightType.Attack, addToSelectableCells: true);
        }

        HighlightCharacterFootprint(charger, HighlightType.Selected);

        CombatUI.SetTurnIndicator(_pendingChargeBullRush
            ? "BULL RUSH (CHARGE): Select a target. Right-click/Esc to cancel."
            : "CHARGE: Select a target. Right-click/Esc to cancel.");
    }

    private List<CharacterController> GetValidChargeTargets(CharacterController charger, bool logFailures)
    {
        var list = new List<CharacterController>();
        if (charger == null) return list;

        foreach (var candidate in GetAllCharacters())
        {
            if (candidate == null || candidate == charger || candidate.Stats == null || candidate.Stats.IsDead)
                continue;

            if (!IsEnemyTeam(charger, candidate)) continue;

            if (CanChargeTarget(charger, candidate, logFailures: false))
                list.Add(candidate);
        }

        if (logFailures && list.Count == 0)
            CombatUI?.ShowCombatLog("⚠ No enemies meet charge requirements (distance, clear path, reachable endpoint).");

        return list;
    }

    private enum ChargePathFailureReason
    {
        None,
        NoPath,
        TooClose,
        TooFar
    }

    public bool CanChargeTarget(CharacterController charger, CharacterController target, bool logFailures = true)
    {
        if (charger == null || target == null || charger == target) return false;
        if (charger.Stats == null || target.Stats == null || target.Stats.IsDead) return false;

        if (!IsEnemyTeam(charger, target))
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ You can only charge an enemy target.");
            return false;
        }

        if (!charger.Actions.HasFullRoundAction)
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Need full-round action to charge.");
            return false;
        }

        if (charger.HasTakenFiveFootStep)
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Cannot charge after taking a 5-foot step.");
            return false;
        }

        if (!charger.HasMeleeWeaponEquipped())
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Need a melee weapon (or natural/unarmed attack) to charge.");
            return false;
        }

        if (charger.Stats.IsFatigued)
        {
            if (logFailures) CombatUI?.ShowCombatLog("⚠ Fatigued creatures cannot charge.");
            return false;
        }

        if (!TryBuildChargePath(charger, target, out _, out _, out ChargePathFailureReason failureReason))
        {
            if (logFailures)
            {
                int maxDistanceFeet = Mathf.Max(2, charger.Stats.MoveRange * 2) * 5;
                switch (failureReason)
                {
                    case ChargePathFailureReason.TooClose:
                        CombatUI?.ShowCombatLog("⚠ Target is too close to charge (minimum 10 ft / 2 squares).");
                        break;
                    case ChargePathFailureReason.TooFar:
                        CombatUI?.ShowCombatLog($"⚠ Target is too far to charge (max {maxDistanceFeet} ft).");
                        break;
                    default:
                        CombatUI?.ShowCombatLog("⚠ No valid charge path to target.");
                        break;
                }
            }
            return false;
        }

        return true;
    }

    public List<Vector2Int> GetChargePath(CharacterController charger, CharacterController target)
    {
        if (TryBuildChargePath(charger, target, out List<Vector2Int> path, out _, out _))
            return path;

        return new List<Vector2Int>();
    }

    private bool TryBuildChargePath(
        CharacterController charger,
        CharacterController target,
        out List<Vector2Int> bestPath,
        out int bestPathCost,
        out ChargePathFailureReason failureReason)
    {
        bestPath = null;
        bestPathCost = int.MaxValue;
        failureReason = ChargePathFailureReason.NoPath;

        if (charger == null || target == null || charger.Stats == null || target.Stats == null || Grid == null)
            return false;

        int minDistance = 2; // 10 feet
        int maxDistance = Mathf.Max(minDistance, charger.Stats.MoveRange * 2);
        int moverSizeSquares = Mathf.Max(1, charger.GetVisualSquaresOccupied());
        int searchRange = Mathf.Max(maxDistance, (Grid.Width + Grid.Height) * 2);

        bool foundReachableEndpoint = false;
        int shortestReachableCost = int.MaxValue;
        Vector2Int bestEndpoint = default;

        foreach (Vector2Int endpoint in GetChargeEndpointCandidates(charger, target, moverSizeSquares))
        {
            AoOPathResult pathResult = Grid.FindPathAoOAware(
                charger.GridPosition,
                endpoint,
                threatenedSquares: null,
                maxRange: searchRange,
                moverSizeSquares: moverSizeSquares,
                mover: charger,
                allowThroughAllies: true);

            if (pathResult == null || pathResult.Path == null || pathResult.Path.Count == 0)
                continue;

            List<Vector2Int> candidatePath = pathResult.Path;
            if (candidatePath[candidatePath.Count - 1] != endpoint)
                continue;

            if (ContainsChargeBlockingTerrain(candidatePath))
                continue;

            int pathCost = SquareGridUtils.CalculatePathCost(charger.GridPosition, candidatePath);
            foundReachableEndpoint = true;
            if (pathCost < shortestReachableCost)
                shortestReachableCost = pathCost;

            if (pathCost < minDistance || pathCost > maxDistance)
                continue;

            bool isBetterPath = bestPath == null
                || pathCost < bestPathCost
                || (pathCost == bestPathCost && IsBetterChargeEndpoint(endpoint, bestEndpoint, target.GridPosition));

            if (!isBetterPath)
                continue;

            bestPath = new List<Vector2Int>(candidatePath);
            bestPathCost = pathCost;
            bestEndpoint = endpoint;
        }

        if (bestPath != null)
        {
            failureReason = ChargePathFailureReason.None;
            return true;
        }

        if (!foundReachableEndpoint)
        {
            failureReason = ChargePathFailureReason.NoPath;
            return false;
        }

        if (shortestReachableCost < minDistance)
            failureReason = ChargePathFailureReason.TooClose;
        else if (shortestReachableCost > maxDistance)
            failureReason = ChargePathFailureReason.TooFar;
        else
            failureReason = ChargePathFailureReason.NoPath;

        return false;
    }

    private IEnumerable<Vector2Int> GetChargeEndpointCandidates(CharacterController charger, CharacterController target, int moverSizeSquares)
    {
        if (charger == null || target == null || Grid == null)
            yield break;

        foreach (var kvp in Grid.Cells)
        {
            Vector2Int endpoint = kvp.Key;

            if (endpoint == charger.GridPosition)
                continue;

            if (!CombatUtils.CanThreatenTargetFromPosition(charger, endpoint, target))
                continue;

            if (!Grid.CanTraversePathNode(endpoint, moverSizeSquares, charger, isDestinationNode: true, allowThroughAllies: true))
                continue;

            yield return endpoint;
        }
    }

    private static bool IsBetterChargeEndpoint(Vector2Int candidate, Vector2Int currentBest, Vector2Int targetPos)
    {
        int candidateChebyshev = SquareGridUtils.GetChebyshevDistance(candidate, targetPos);
        int currentChebyshev = SquareGridUtils.GetChebyshevDistance(currentBest, targetPos);

        if (candidateChebyshev != currentChebyshev)
            return candidateChebyshev < currentChebyshev;

        if (candidate.y != currentBest.y)
            return candidate.y < currentBest.y;

        return candidate.x < currentBest.x;
    }

    private bool ContainsChargeBlockingTerrain(List<Vector2Int> path)
    {
        if (path == null || path.Count == 0)
            return true;

        for (int i = 0; i < path.Count; i++)
        {
            if (IsDifficultTerrain(path[i]))
                return true;
        }

        return false;
    }

    private bool IsDifficultTerrain(Vector2Int pos)
    {
        // Difficult terrain is not data-driven in this prototype yet.
        // Keep hook for future implementation.
        return false;
    }


    private void HandleChargeTargetClick(CharacterController charger, SquareCell cell)
    {
        if (charger == null || cell == null) return;

        if (!cell.IsOccupied || cell.Occupant == charger || cell.Occupant.Stats.IsDead)
            return;

        CharacterController target = cell.Occupant;
        if (!CanChargeTarget(charger, target, logFailures: true))
            return;

        _chargeTarget = target;
        _pendingChargePath = GetChargePath(charger, target);

        CurrentSubPhase = PlayerSubPhase.ConfirmingChargePath;
        ShowChargePathPreview(charger, target);
    }

    private void HandleChargeConfirmationClick(CharacterController charger, SquareCell cell)
    {
        if (charger == null || _chargeTarget == null || _pendingChargePath == null || _pendingChargePath.Count == 0)
        {
            CancelChargeTargeting();
            return;
        }

        bool clickedTarget = cell != null && cell.IsOccupied && cell.Occupant == _chargeTarget;
        bool clickedFinal = cell != null && cell.Coords == _pendingChargePath[_pendingChargePath.Count - 1];

        if (!clickedTarget && !clickedFinal)
        {
            // Allow switching target during confirm step.
            if (cell != null && cell.IsOccupied && cell.Occupant != charger)
            {
                HandleChargeTargetClick(charger, cell);
            }
            return;
        }

        StartCoroutine(ExecuteCharge(charger, _chargeTarget));
    }

    private void ShowChargePathPreview(CharacterController charger, CharacterController target)
    {
        if (charger == null || target == null) return;
        if (!CanChargeTarget(charger, target, logFailures: false)) return;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        foreach (Vector2Int pos in _pendingChargePath)
        {
            SquareCell c = Grid.GetCell(pos);
            if (c != null)
            {
                c.SetHighlight(HighlightType.Move);
                _highlightedCells.Add(c);
            }
        }

        HighlightCharacterFootprint(charger, HighlightType.Selected);
        HighlightCharacterFootprint(target, HighlightType.Attack);

        CombatUI.SetTurnIndicator(_pendingChargeBullRush
            ? $"BULL RUSH CHARGE: {target.Stats.CharacterName} | +2 check, -2 AC until next turn. Click target/endpoint to confirm."
            : $"CHARGE: {target.Stats.CharacterName} | +2 attack, -2 AC until next turn. Click target/endpoint to confirm.");
    }

    private IEnumerator ExecuteCharge(CharacterController charger, CharacterController target)
    {
        if (!CanChargeTarget(charger, target, logFailures: true))
        {
            ShowActionChoices();
            yield break;
        }

        CurrentSubPhase = PlayerSubPhase.Animating;
        List<Vector2Int> path = GetChargePath(charger, target);
        if (path == null || path.Count == 0)
        {
            CombatUI?.ShowCombatLog("⚠ Charge aborted: invalid path.");
            ShowActionChoices();
            yield break;
        }

        CombatUI?.ShowCombatLog(_pendingChargeBullRush
            ? $"🏇 {charger.Stats.CharacterName} charges and attempts a bull rush on {target.Stats.CharacterName}!"
            : $"🏇 {charger.Stats.CharacterName} charges {target.Stats.CharacterName}!");

        // Resolve provoked AoOs during charge movement.
        var provokedAoOs = ThreatSystem.AnalyzePathForAoOs(charger, path, GetAllCharacters());
        foreach (var aooInfo in provokedAoOs)
        {
            if (charger.Stats.IsDead) break;
            if (aooInfo == null || aooInfo.Threatener == null || aooInfo.Threatener.Stats.IsDead) continue;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(aooInfo.Threatener, charger);
            if (aooResult == null) continue;

            CombatUI.ShowCombatLog($"⚔ AoO during charge: {aooResult.GetDetailedSummary()}");
            UpdateAllStatsUI();

            if (aooResult.Hit && aooResult.TotalDamage > 0)
                CheckConcentrationOnDamage(charger, aooResult.TotalDamage);

            yield return new WaitForSeconds(0.8f);
        }

        if (charger.Stats.IsDead)
        {
            CombatUI?.ShowCombatLog($"{charger.Stats.CharacterName} falls before completing the charge.");
            UpdateAllStatsUI();
            if (IsPlayerTurn) EndActivePCTurn();
            yield break;
        }

        yield return StartCoroutine(charger.MoveAlongPath(path, PlayerMoveSecondsPerStep, markAsMoved: true));
        CheckTurnUndeadProximityBreakingForMover(charger);
        PruneTurnUndeadTrackers();

        InvalidatePreviewThreats();

        if (_pendingChargeBullRush)
        {
            SpecialAttackResult bullRushResult = charger.ExecuteSpecialAttack(
                SpecialAttackType.BullRushCharge,
                target,
                bullRushChargeBonusOverride: 2);
            CombatUI.ShowCombatLog($"⚡ Charge Bull Rush (+2): {bullRushResult.Log}");

            if (bullRushResult.Success)
                yield return StartCoroutine(ResolveBullRushPushAndFollowCoroutine(charger, target, bullRushResult));
        }
        else
        {
            // Apply +2 charge attack bonus to this attack only.
            charger.Stats.MoraleAttackBonus += 2;
            CombatResult result;
            try
            {
                ProcessTurnUndeadMeleeFearBreak(charger, target, isMeleeAttack: true);
                result = charger.Attack(target, false, 0, null, null);
            }
            finally
            {
                charger.Stats.MoraleAttackBonus -= 2;
            }

            if (result != null)
            {
                CombatUI.ShowCombatLog($"⚡ Charge Attack (+2): {result.GetDetailedSummary()}");
                if (result.Hit && result.TotalDamage > 0)
                    CheckConcentrationOnDamage(target, result.TotalDamage);
            }
        }

        // Apply AC penalty until next turn.
        charger.ApplyCondition(CombatConditionType.ChargePenalty, 1, charger.Stats.CharacterName);
        CombatUI.ShowCombatLog($"🛡 {charger.Stats.CharacterName} is charging: -2 AC until next turn.");

        charger.Actions.UseFullRoundAction();

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        _chargeTarget = null;
        _pendingChargePath.Clear();
        _pendingChargeBullRush = false;

        UpdateAllStatsUI();

        if (target.Stats.IsDead && !target.IsPlayerControlled && AreAllNPCsDead())
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! All enemies defeated!");
            CombatUI.SetActionButtonsVisible(false);
            yield break;
        }

        StartCoroutine(AfterAttackDelay(charger, 1.0f));
    }

    private void CancelChargeTargeting()
    {
        _chargeTarget = null;
        _pendingChargePath.Clear();
        _pendingChargeBullRush = false;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        ShowActionChoices();
    }

    private bool ShouldNPCUseCharge(CharacterController npc, CharacterController target)
    {
        if (npc == null || target == null) return false;
        if (!npc.Actions.HasFullRoundAction) return false;
        if (!npc.HasMeleeWeaponEquipped()) return false;
        if (target.Stats == null || target.Stats.IsDead) return false;

        int dist = npc.GetMinimumDistanceToTarget(target, chebyshev: true);
        // Prefer charge when out of melee reach but still reachable via a valid charge path.
        if (npc.CanMeleeAttackDistance(dist)) return false;

        return CanChargeTarget(npc, target, logFailures: false);
    }

    private IEnumerator NPCExecuteCharge(CharacterController npc, CharacterController target)
    {
        if (!CanChargeTarget(npc, target, logFailures: false))
            yield break;

        List<Vector2Int> path = GetChargePath(npc, target);
        if (path == null || path.Count == 0)
            yield break;

        CombatUI.ShowCombatLog($"🏇 {npc.Stats.CharacterName} charges {target.Stats.CharacterName}!");

        var provokedAoOs = ThreatSystem.AnalyzePathForAoOs(npc, path, GetAllCharacters());
        foreach (var aooInfo in provokedAoOs)
        {
            if (npc.Stats.IsDead) break;
            if (aooInfo == null || aooInfo.Threatener == null || aooInfo.Threatener.Stats.IsDead) continue;

            CombatResult aooResult = ThreatSystem.ExecuteAoO(aooInfo.Threatener, npc);
            if (aooResult == null) continue;

            CombatUI.ShowCombatLog($"⚔ AoO vs {npc.Stats.CharacterName}: {aooResult.GetDetailedSummary()}");
            UpdateAllStatsUI();

            if (aooResult.Hit && aooResult.TotalDamage > 0)
                CheckConcentrationOnDamage(npc, aooResult.TotalDamage);

            yield return new WaitForSeconds(0.5f);
        }

        if (npc.Stats.IsDead)
            yield break;

        yield return StartCoroutine(npc.MoveAlongPath(path, NpcChargeMoveSecondsPerStep, markAsMoved: true));
        CheckTurnUndeadProximityBreakingForMover(npc);
        PruneTurnUndeadTrackers();

        CharacterController flankPartner;
        bool isFlankingCharge = CombatUtils.IsAttackerFlanking(npc, target, GetAllCharacters(), out flankPartner);
        int flankingBonus = isFlankingCharge ? CombatUtils.FlankingAttackBonus : 0;

        npc.Stats.MoraleAttackBonus += 2;
        CombatResult result;
        try
        {
            ProcessTurnUndeadMeleeFearBreak(npc, target, isMeleeAttack: true);
            result = npc.Attack(target, isFlankingCharge, flankingBonus,
                flankPartner != null ? flankPartner.Stats.CharacterName : null, null);
        }
        finally
        {
            npc.Stats.MoraleAttackBonus -= 2;
        }

        if (result != null)
        {
            string flankText = isFlankingCharge ? " + Flanking" : "";
            CombatUI.ShowCombatLog($"☠ Charge Attack (+2{flankText}): {result.GetDetailedSummary()}");
            if (result.Hit && result.TotalDamage > 0)
                CheckConcentrationOnDamage(target, result.TotalDamage);
        }

        npc.ApplyCondition(CombatConditionType.ChargePenalty, 1, npc.Stats.CharacterName);
        npc.Actions.UseFullRoundAction();
        UpdateAllStatsUI();

        yield return new WaitForSeconds(0.8f);
    }
}

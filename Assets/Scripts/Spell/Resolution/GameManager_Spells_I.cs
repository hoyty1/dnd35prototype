// ============================================================================
// GameManager_Spells_I.cs — Spell resolution methods starting with "I".
//
// Part of the GameManager partial class.
// D&D 3.5e PHB rules.
// ============================================================================
using DND35e.Identifiers;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using UnityEngine;

public partial class GameManager
{
    // ================================================================
    //  IMBUE WITH SPELL ABILITY  (PHB p.243)
    // ================================================================
    // Cleric 4 — Touch, Permanent until discharged
    // Transfer up to 3 prepared 1st/2nd-level spells to a willing
    // non-spellcaster. Caster loses those spell slots until target
    // uses all transferred spells or the spell is dismissed.

    /// <summary>
    /// Resolves the Imbue with Spell Ability spell.
    /// This triggers the spell selection UI for the caster to pick
    /// which prepared spells to transfer.
    /// </summary>
    private bool TryResolveImbueWithSpellAbilitySpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.IMBUE_WITH_SPELL_ABILITY)
            return false;

        if (caster == null || caster.Stats == null)
            return false;

        if (!result.Success)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Damage("✖", "Imbue with Spell Ability failed (spell did not succeed)."));
            return true;
        }

        if (target == null || target.Stats == null)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Damage("✖", "Imbue with Spell Ability: No valid target."));
            return true;
        }

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";

        // Validate target
        var (isValid, reason) = ImbueWithSpellAbilityManager.ValidateTarget(caster, target);
        if (!isValid)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Damage("✖", $"Imbue with Spell Ability failed: {reason}"));
            Debug.Log($"[ImbueWithSpellAbility] Validation failed for {targetName}: {reason}");
            return true;
        }

        // Determine max level target can receive
        int maxLevel = ImbueWithSpellAbilityManager.GetMaxImbuableLevel(target);

        // Get transferable slots
        var transferableSlots = ImbueWithSpellAbilityManager.GetTransferableSlots(caster, maxLevel);
        if (transferableSlots.Count == 0)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Damage("✖", $"Imbue with Spell Ability: {casterName} has no prepared 1st{(maxLevel >= 2 ? "/2nd" : "")}-level spells to transfer."));
            Debug.Log($"[ImbueWithSpellAbility] No transferable slots for {casterName}");
            return true;
        }

        Debug.Log($"[ImbueWithSpellAbility] {casterName} → {targetName}: {transferableSlots.Count} transferable slot(s), max level {maxLevel}");

        // Show the spell selection UI
        ShowImbueSpellSelectionUI(caster, target, transferableSlots, maxLevel, spell);

        return true;
    }

    /// <summary>
    /// Displays the UI for the caster to select which prepared spells to transfer.
    /// Uses a simple modal panel approach consistent with existing UI patterns.
    /// </summary>
    private void ShowImbueSpellSelectionUI(
        CharacterController caster,
        CharacterController target,
        List<(SpellSlot slot, int index)> transferableSlots,
        int maxLevel,
        SpellData imbueSpell)
    {
        string casterName = caster.Stats.CharacterName ?? "Unknown";
        string targetName = target.Stats.CharacterName ?? "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        // Build the selection list: group by spell name for clarity
        var selectedIndices = new List<int>();

        // Create UI panel
        var panelGO = new GameObject("ImbueSpellSelectionPanel");
        var canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
            panelGO.transform.SetParent(canvas.transform, false);

        var rectTransform = panelGO.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.2f, 0.15f);
        rectTransform.anchorMax = new Vector2(0.8f, 0.85f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        var bgImage = panelGO.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.12f, 0.14f, 0.2f, 0.95f);

        var layout = panelGO.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 15, 15);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // Title
        var titleText = UIFactory.CreateLabel(panelGO.transform,
            "IMBUE WITH SPELL ABILITY",
            24, null, new Color(1f, 0.85f, 0.2f), "ImbueTitle");
        var titleLayout = titleText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        titleLayout.preferredHeight = 35;

        // Subtitle
        var subtitleText = UIFactory.CreateLabel(panelGO.transform,
            $"Select up to 3 spells to transfer from {casterName} to {targetName}\n" +
            $"(Max spell level: {maxLevel}{(maxLevel >= 2 ? " — target WIS ≥ 13" : "")})",
            14, null, new Color(0.8f, 0.8f, 0.8f), "ImbueSubtitle");
        var subLayout = subtitleText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        subLayout.preferredHeight = 40;

        // Scroll area for spell slots
        var scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(panelGO.transform, false);
        var scrollRect = scrollGO.AddComponent<UnityEngine.UI.ScrollRect>();
        var scrollLayout = scrollGO.AddComponent<UnityEngine.UI.LayoutElement>();
        scrollLayout.flexibleHeight = 1;
        var scrollRectTransform = scrollGO.GetComponent<RectTransform>();

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(scrollGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);

        var contentLayout = contentGO.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        contentLayout.spacing = 4;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.padding = new RectOffset(5, 5, 5, 5);

        var contentFitter = contentGO.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        contentFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRT;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;

        // Track toggle states
        var toggles = new List<(UnityEngine.UI.Toggle toggle, int slotIndex)>();

        foreach (var (slot, index) in transferableSlots)
        {
            var rowGO = new GameObject($"SpellRow_{index}");
            rowGO.transform.SetParent(contentGO.transform, false);
            var rowLayout = rowGO.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            rowLayout.spacing = 10;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var rowLayoutElem = rowGO.AddComponent<UnityEngine.UI.LayoutElement>();
            rowLayoutElem.preferredHeight = 30;

            // Toggle (checkbox)
            var toggleGO = new GameObject("Toggle");
            toggleGO.transform.SetParent(rowGO.transform, false);
            var toggle = toggleGO.AddComponent<UnityEngine.UI.Toggle>();
            var toggleRT = toggleGO.GetComponent<RectTransform>();
            toggleRT.sizeDelta = new Vector2(25, 25);
            var toggleLayoutElem = toggleGO.AddComponent<UnityEngine.UI.LayoutElement>();
            toggleLayoutElem.preferredWidth = 25;
            toggleLayoutElem.preferredHeight = 25;

            // Toggle background
            var bgToggle = new GameObject("Background");
            bgToggle.transform.SetParent(toggleGO.transform, false);
            var bgImg = bgToggle.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.3f, 0.3f, 0.3f);
            var bgRT = bgToggle.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;

            // Toggle checkmark
            var checkGO = new GameObject("Checkmark");
            checkGO.transform.SetParent(bgToggle.transform, false);
            var checkImg = checkGO.AddComponent<UnityEngine.UI.Image>();
            checkImg.color = new Color(0.2f, 0.8f, 0.2f);
            var checkRT = checkGO.GetComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0.15f, 0.15f);
            checkRT.anchorMax = new Vector2(0.85f, 0.85f);
            checkRT.offsetMin = Vector2.zero;
            checkRT.offsetMax = Vector2.zero;

            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = false;

            // Enforce max 3 selections
            int capturedIndex = index;
            toggle.onValueChanged.AddListener((isOn) =>
            {
                if (isOn)
                {
                    int currentSelected = toggles.Count(t => t.toggle.isOn);
                    if (currentSelected > 3)
                    {
                        toggle.isOn = false; // revert
                    }
                }
            });

            toggles.Add((toggle, index));

            // Spell label
            string domainTag = slot.IsDomainSlot ? " [D]" : "";
            string spellLabel = $"Lv{slot.Level}{domainTag}: {slot.PreparedSpell.Name}";
            var labelText = UIFactory.CreateLabel(rowGO.transform,
                spellLabel, 16, null, Color.white, $"Label_{index}");
            var labelLayout = labelText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            labelLayout.flexibleWidth = 1;
            labelLayout.preferredHeight = 25;
        }

        // Bottom buttons row
        var btnRowGO = new GameObject("ButtonRow");
        btnRowGO.transform.SetParent(panelGO.transform, false);
        var btnRowLayout = btnRowGO.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        btnRowLayout.spacing = 20;
        btnRowLayout.childAlignment = TextAnchor.MiddleCenter;
        btnRowLayout.childForceExpandWidth = false;
        btnRowLayout.childForceExpandHeight = false;
        var btnRowLayoutElem = btnRowGO.AddComponent<UnityEngine.UI.LayoutElement>();
        btnRowLayoutElem.preferredHeight = 40;

        // Confirm button
        var confirmButton = UIFactory.CreateButton(btnRowGO.transform,
            "IMBUE SELECTED", null, new Vector2(180, 35), new Color(0.2f, 0.7f, 0.3f), "ConfirmBtn");
        var confirmLayout = confirmButton.gameObject.GetComponent<UnityEngine.UI.LayoutElement>();
        if (confirmLayout == null) confirmLayout = confirmButton.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        confirmLayout.preferredWidth = 180;
        confirmLayout.preferredHeight = 35;

        // Cancel button
        var cancelButton = UIFactory.CreateButton(btnRowGO.transform,
            "CANCEL", null, new Vector2(120, 35), new Color(0.7f, 0.25f, 0.25f), "CancelBtn");
        var cancelLayout = cancelButton.gameObject.GetComponent<UnityEngine.UI.LayoutElement>();
        if (cancelLayout == null) cancelLayout = cancelButton.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        cancelLayout.preferredWidth = 120;
        cancelLayout.preferredHeight = 35;

        // Wire confirm button
        confirmButton?.onClick.AddListener(() =>
        {
            var chosen = toggles.Where(t => t.toggle.isOn).Select(t => t.slotIndex).ToList();
            if (chosen.Count == 0)
            {
                CombatUI?.ShowCombatLog(CombatLogHelper.Warning("⚠", "No spells selected for imbuing. Select at least one spell."));
                return;
            }

            // Perform the transfer
            ImbueWithSpellAbilityManager.TransferSpells(caster, target, chosen);

            // Add status effects on both characters
            var casterStatusMgr = caster.StatusEffectManager;
            if (casterStatusMgr != null)
            {
                var effect = casterStatusMgr.AddEffect(imbueSpell, casterName, casterLevel);
                if (effect != null)
                    effect.RemainingRounds = -1; // permanent until discharged
            }

            var targetStatusMgr = target.StatusEffectManager;
            if (targetStatusMgr != null)
            {
                var effect = targetStatusMgr.AddEffect(imbueSpell, casterName, casterLevel);
                if (effect != null)
                    effect.RemainingRounds = -1; // permanent until discharged
            }

            // Log
            var sb = new StringBuilder();
            sb.AppendLine($"<color=#88CCFF>✨ {casterName} imbues {targetName} with spell ability!</color>");
            foreach (var idx in chosen)
            {
                var spellComp = caster.Spellcasting;
                if (spellComp != null && idx >= 0 && idx < spellComp.SpellSlots.Count)
                {
                    var slot = spellComp.SpellSlots[idx];
                    sb.AppendLine($"<color=#AADDFF>  📜 {slot.PreparedSpell?.Name ?? "?"} (Lv{slot.Level})</color>");
                }
            }
            sb.AppendLine($"<color=#AADDFF>  {targetName} can now cast {chosen.Count} imbued spell(s) using {casterName}'s caster level.</color>");
            sb.Append($"<color=#AADDFF>  {casterName}'s spell slots are locked until spells are used or dismissed.</color>");
            CombatUI?.ShowCombatLog(sb.ToString());

            UpdateAllStatsUI();
            Destroy(panelGO);
        });

        // Wire cancel button
        cancelButton?.onClick.AddListener(() =>
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Info("", "Imbue with Spell Ability cancelled."));
            Debug.Log("[ImbueWithSpellAbility] Spell selection cancelled by user.");
            Destroy(panelGO);
        });

        Debug.Log($"[ImbueWithSpellAbility] Showing spell selection UI with {transferableSlots.Count} available slots");
    }

    /// <summary>
    /// Handles a target casting one of their imbued spells.
    /// Called from the combat flow when an imbued spell is selected from the action menu.
    /// Sets up the pending spell for the standard resolution pipeline.
    /// The target acts as the "caster" for targeting purposes but uses the
    /// original caster's level and DC.
    /// </summary>
    public void ResolveImbuedSpellCast(CharacterController target, string spellId)
    {
        if (target == null || target.Stats == null) return;

        // Find the entry (don't consume it yet — it will be consumed when spell actually resolves)
        var entry = target.Stats.ImbuedSpells.FirstOrDefault(
            e => e.Spell != null && e.Spell.SpellId == spellId);
        if (entry == null)
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Damage("✖", "Failed to cast imbued spell — not found."));
            return;
        }

        string targetName = target.Stats.CharacterName ?? "Unknown";
        CombatUI?.ShowCombatLog(CombatLogHelper.Defensive("✨", $"{targetName} begins casting imbued {entry.Spell.Name}! (CL {entry.CasterLevel}, DC {entry.SaveDC})"));
        Debug.Log($"[ImbueWithSpellAbility] {targetName} initiating imbued spell cast: {entry.Spell.Name} at CL {entry.CasterLevel}, DC {entry.SaveDC}");

        // Set the pending spell for the resolution pipeline
        _pendingSpell = entry.Spell;
        _imbueSpellCastInProgress = true;
        _imbueSpellEntry = entry;

        // The standard spell resolution pipeline will pick this up.
        // For self/ally targeting spells, resolve immediately.
        // For targeted spells, the target selection flow will handle it.

        // Consume the imbued spell and update tracking
        ImbueWithSpellAbilityManager.CastImbuedSpell(target, spellId);

        int remaining = target.Stats.ImbuedSpells.Count;
        if (remaining > 0)
            CombatUI?.ShowCombatLog(CombatLogHelper.PaleBlue("", $" {remaining} imbued spell(s) remaining."));
        else
            CombatUI?.ShowCombatLog(CombatLogHelper.PaleBlue("", " All imbued spells discharged. Imbue with Spell Ability ends."));

        UpdateAllStatsUI();
    }

    /// <summary>True when a character is casting a spell via Imbue with Spell Ability.</summary>
    private bool _imbueSpellCastInProgress;

    /// <summary>The ImbueSpellEntry being cast, for CL/DC overrides during resolution.</summary>
    private ImbueSpellEntry _imbueSpellEntry;

    /// <summary>
    /// Returns the effective caster level for the current spell being cast.
    /// If an imbued spell is being cast, returns the original caster's level.
    /// </summary>
    public int GetEffectiveCasterLevelForImbue()
    {
        if (_imbueSpellCastInProgress && _imbueSpellEntry != null)
            return _imbueSpellEntry.CasterLevel;
        return -1; // indicates no override
    }

    /// <summary>
    /// Returns the effective save DC for the current imbued spell being cast.
    /// Returns -1 if no imbue override is active.
    /// </summary>
    public int GetEffectiveSaveDCForImbue()
    {
        if (_imbueSpellCastInProgress && _imbueSpellEntry != null)
            return _imbueSpellEntry.SaveDC;
        return -1;
    }

    /// <summary>
    /// Clears the imbue spell cast state after the spell has been resolved.
    /// Called at the end of spell resolution.
    /// </summary>
    public void ClearImbueSpellCastState()
    {
        _imbueSpellCastInProgress = false;
        _imbueSpellEntry = null;
    }

    /// <summary>
    /// Called when the "Cast Imbued Spell" button is pressed in the action menu.
    /// Shows a selection popup of the target's available imbued spells.
    /// </summary>
    public void OnUseImbuedSpellButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || pc.Stats == null) return;

        if (!ImbueWithSpellAbilityManager.HasImbuedSpells(pc))
        {
            CombatUI?.ShowCombatLog(CombatLogHelper.Color("No imbued spells available.", CombatLogHelper.ColorBrightRed));
            return;
        }

        var entries = ImbueWithSpellAbilityManager.GetImbuedSpells(pc);
        Debug.Log($"[ImbueWithSpellAbility] Showing imbued spell selection for {pc.Stats.CharacterName}: {entries.Count} spell(s)");

        // Create selection popup
        ShowImbuedSpellCastSelectionUI(pc, entries);
    }

    /// <summary>
    /// Shows a popup for the target to select which imbued spell to cast.
    /// </summary>
    private void ShowImbuedSpellCastSelectionUI(CharacterController target, List<ImbueSpellEntry> entries)
    {
        var panelGO = new GameObject("ImbuedSpellCastPanel");
        var canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
            panelGO.transform.SetParent(canvas.transform, false);

        var rectTransform = panelGO.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.25f, 0.25f);
        rectTransform.anchorMax = new Vector2(0.75f, 0.75f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        var bgImage = panelGO.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.12f, 0.14f, 0.2f, 0.95f);

        var layout = panelGO.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 15, 15);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // Title
        var titleText = UIFactory.CreateLabel(panelGO.transform,
            "CAST IMBUED SPELL", 22, null, new Color(0.4f, 1f, 0.6f), "ImbuedCastTitle");
        var titleLayout = titleText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        titleLayout.preferredHeight = 30;

        string casterName = entries.Count > 0 && entries[0].CasterName != null ? entries[0].CasterName : "?";
        var subText = UIFactory.CreateLabel(panelGO.transform,
            $"Imbued by {casterName} — select a spell to cast:", 14, null, new Color(0.8f, 0.8f, 0.8f), "ImbuedCastSub");
        var subLayout = subText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        subLayout.preferredHeight = 25;

        foreach (var entry in entries)
        {
            string label = $"{entry.Spell.Name} (Lv{entry.Spell.SpellLevel}, CL {entry.CasterLevel}, DC {entry.SaveDC})";
            var btn = UIFactory.CreateButton(panelGO.transform,
                label, null, null, new Color(0.2f, 0.55f, 0.7f), $"ImbuedCast_{entry.Spell.SpellId}");
            var btnLayout = btn.gameObject.GetComponent<UnityEngine.UI.LayoutElement>();
            if (btnLayout == null) btnLayout = btn.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            btnLayout.preferredHeight = 35;

            string capturedId = entry.Spell.SpellId;
            btn?.onClick.AddListener(() =>
            {
                Destroy(panelGO);
                ResolveImbuedSpellCast(target, capturedId);
            });
        }

        // Cancel button
        var cancelButton = UIFactory.CreateButton(panelGO.transform,
            "CANCEL", null, null, new Color(0.5f, 0.25f, 0.25f), "CancelBtn");
        var cancelLayout = cancelButton.gameObject.GetComponent<UnityEngine.UI.LayoutElement>();
        if (cancelLayout == null) cancelLayout = cancelButton.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        cancelLayout.preferredHeight = 35;

        cancelButton?.onClick.AddListener(() =>
        {
            Debug.Log("[ImbueWithSpellAbility] Imbued spell cast cancelled.");
            Destroy(panelGO);
        });
    }

    // ================================================================
    //  INVISIBILITY PURGE  (PHB p.245)
    // ================================================================
    // 5-ft/level emanation centered on caster. 1 min/level.
    // Dispels invisibility on any creature entering or within the area.
    // This implementation: sets InvisibilityPurgeActive on caster and
    // strips Invisible condition from enemies in radius at cast time.

    private bool TryResolveInvisibilityPurgeSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.INVISIBILITY_PURGE)
            return false;

        if (caster == null || caster.Stats == null)
            return false;

        if (!result.Success)
            return true;

        string casterName = caster.Stats.CharacterName ?? "Unknown";
        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);
        int durationRounds = casterLevel * 10; // 1 min/level = 10 rounds/level
        int radiusSquares = Mathf.Max(1, casterLevel); // 5 ft/level = 1 square/level

        // Set caster state
        caster.Stats.InvisibilityPurgeActive = true;
        caster.Stats.InvisibilityPurgeRoundsRemaining = durationRounds;

        // Track via StatusEffectManager
        var statusMgr = caster.StatusEffectManager;
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, casterName, casterLevel);
            if (effect != null)
                effect.RemainingRounds = durationRounds;
        }

        // Purge invisibility from all characters in radius
        int purgeCount = 0;
        List<CharacterController> allChars = GetAllCharacters();
        foreach (var ch in allChars)
        {
            if (ch == null || ch.Stats == null || ch.Stats.IsDead) continue;
            if (ch == caster) continue;

            int dist = SquareGridUtils.GetDistance(caster.GridPosition, ch.GridPosition);
            if (dist > radiusSquares) continue;

            if (ch.HasCondition(CombatConditionType.Invisible))
            {
                ch.RemoveCondition(CombatConditionType.Invisible);
                purgeCount++;
                CombatUI?.ShowCombatLog(CombatLogHelper.Color($"  👁 {ch.Stats.CharacterName}'s invisibility is dispelled!", "CCDDFF"));
            }
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.PaleBlue("👁✨", $"Invisibility Purge! {casterName} radiates a {radiusSquares * 5}-ft anti-invisibility field for {durationRounds} rounds. {purgeCount} creature(s) revealed."));
        Debug.Log($"[InvisibilityPurge] {casterName}: radius {radiusSquares} sq, duration {durationRounds} rounds, purged {purgeCount}");

        return true;
    }

    // ================================================================
    //  INVISIBILITY SPHERE — Mobile Emanation (PHB p.245)
    // ================================================================

    /// <summary>
    /// Applies the Invisibility Sphere spell (Bard 3, Sorcerer/Wizard 3).
    /// Per PHB p.245: 10-ft-radius emanation centered on the recipient.
    ///   - All creatures within the emanation at cast time become invisible.
    ///   - The area moves with the recipient (mobile emanation).
    ///   - Creatures that LEAVE the emanation become visible immediately.
    ///   - Creatures that ENTER the emanation later do NOT become invisible.
    ///   - If a creature OTHER THAN the recipient attacks, only that creature
    ///     becomes visible.
    ///   - If the RECIPIENT attacks, the entire spell ends.
    ///
    /// Called from ApplySpellBuff when spell.SpellId == INVISIBILITY_SPHERE.
    /// </summary>
    private ActiveSpellEffect ApplyInvisibilitySphere(CharacterController caster, CharacterController target, SpellData spell, SpellcastingComponent spellComp)
    {
        CharacterController recipient = target ?? caster;
        if (recipient == null || recipient.Stats == null || spell == null)
            return null;

        // Track the spell on the recipient so duration/dispel/dismiss
        // work via the standard StatusEffectManager path.
        StatusEffectManager recipientStatusMgr = recipient.StatusEffectManager;
        if (recipientStatusMgr == null)
            recipientStatusMgr = recipient.gameObject.AddComponent<StatusEffectManager>();
        recipientStatusMgr.Init(recipient.Stats);

        int casterLevel = caster != null && caster.Stats != null ? Mathf.Max(1, caster.Stats.GetDomainBoostedCasterLevel(spell)) : 1;
        ActiveSpellEffect effect = recipientStatusMgr.AddEffect(
            spell,
            caster != null && caster.Stats != null ? caster.Stats.CharacterName : spell.Name,
            casterLevel);

        if (effect == null)
        {
            UpdateAllStatsUI();
            return null;
        }

        SpellcastingComponent recipientSpellComp = recipient.Spellcasting;
        if (recipientSpellComp != null)
            recipientSpellComp.ActiveBuffs[spell.SpellId] = effect.RemainingRounds;

        int durationRounds = Mathf.Max(1, effect.RemainingRounds);

        // Build & register the emanation
        var sphere = InvisibilitySphereEffect.Create(recipient, caster, durationRounds, casterLevel);
        RegisterEmanation(sphere);

        // Capture initial affected creatures (everyone within 10 ft of recipient)
        List<CharacterController> all = GetAllCharacters();
        sphere.ApplyInitialAffectedCreatures(all);

        // Logging
        string casterName = caster != null && caster.Stats != null ? caster.Stats.CharacterName : "Caster";
        bool selfCast = recipient == caster;
        string castLine = selfCast
            ? $"<color=#88CCFF>👻 {casterName} casts Invisibility Sphere on self!</color>"
            : $"<color=#88CCFF>👻 {casterName} casts Invisibility Sphere on {recipient.Stats.CharacterName}!</color>";
        CombatUI?.ShowCombatLog(castLine);

        int affectedCount = sphere.InitiallyAffectedCreatures != null ? sphere.InitiallyAffectedCreatures.Count : 0;
        CombatUI?.ShowCombatLog(CombatLogHelper.IceBlue("", $"  A 10-ft emanation forms around {recipient.Stats.CharacterName}; {affectedCount} creature(s) become invisible."));

        if (affectedCount > 0)
        {
            var sb = new StringBuilder("<color=#A6F3FF>   Affected: ");
            for (int i = 0; i < sphere.InitiallyAffectedCreatures.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var c = sphere.InitiallyAffectedCreatures[i];
                sb.Append(c != null && c.Stats != null ? c.Stats.CharacterName : "?");
            }
            sb.Append("</color>");
            CombatUI?.ShowCombatLog(sb.ToString());
        }

        CombatUI?.ShowCombatLog(CombatLogHelper.IceBlue("", $"  Duration: {effect.GetDurationDisplayString()}. Leaving the sphere or attacking ends invisibility."));

        UpdateEnemyLastKnownPositionForInvisibility(recipient);
        UpdateAllStatsUI();
        return effect;
    }

    /// <summary>
    /// Per-round refresh of all active Invisibility Sphere emanations.
    /// Removes invisibility from creatures who have stepped out of the sphere.
    /// Called from TickEmanations (and may be invoked after movement actions).
    /// </summary>
    public void RefreshInvisibilitySpheres()
    {
        var spheres = GetActiveEmanationsOfType<InvisibilitySphereEffect>();
        for (int i = 0; i < spheres.Count; i++)
        {
            spheres[i]?.RefreshMembership();
        }
    }

    /// <summary>
    /// Ends an Invisibility Sphere centered on the given recipient (e.g. when
    /// the recipient attacks, the spell expires/is dismissed, or is dispelled).
    /// All initially-affected creatures become visible at once.
    /// </summary>
    /// <param name="recipient">The creature on whom the sphere is centered.</param>
    /// <param name="reason">Free-text reason shown in the combat log.</param>
    public void EndInvisibilitySphereForRecipient(CharacterController recipient, string reason = "spell ended")
    {
        if (recipient == null) return;

        var spheres = GetActiveEmanationsOfType<InvisibilitySphereEffect>();
        for (int i = 0; i < spheres.Count; i++)
        {
            var s = spheres[i];
            if (s == null || s.HasEnded) continue;
            if (s.CenterCreature != recipient) continue;

            s.EndForAll(reason);
        }
    }

    /// <summary>
    /// Returns the active Invisibility Sphere this creature is currently
    /// invisible from, or null if none.
    /// </summary>
    public InvisibilitySphereEffect GetInvisibilitySphereAffecting(CharacterController creature)
    {
        if (creature == null) return null;

        var spheres = GetActiveEmanationsOfType<InvisibilitySphereEffect>();
        for (int i = 0; i < spheres.Count; i++)
        {
            var s = spheres[i];
            if (s == null || s.HasEnded) continue;
            if (s.IsCreatureAffected(creature))
                return s;
        }
        return null;
    }

    /// <summary>
    /// Handles an attack made by a creature that is invisible due to an
    /// Invisibility Sphere. Per PHB p.245:
    ///   - If the attacker is the recipient → ALL affected creatures become visible.
    ///   - Otherwise → only that one creature becomes visible.
    /// Returns true if a sphere matched and was processed (so the standard
    /// invisibility-on-attack flow can be skipped for this attacker).
    /// </summary>
    public bool TryHandleInvisibilitySphereAttack(CharacterController attacker, string reason = "attacked")
    {
        if (attacker == null) return false;

        var sphere = GetInvisibilitySphereAffecting(attacker);
        if (sphere == null) return false;

        if (sphere.CenterCreature == attacker)
        {
            sphere.EndForAll(reason);

            // Also clean the recipient's tracking ActiveSpellEffect so the duration
            // bar / dismiss UI clears on the same round as the sphere ending.
            StatusEffectManager mgr = attacker.StatusEffectManager;
            mgr?.RemoveEffectsBySpellId(SpellNames.INVISIBILITY_SPHERE);
        }
        else
        {
            sphere.EndForCreature(attacker, reason);
        }
        return true;
    }

    // ================================================================
    //  ICE STORM — AoE Damage, No Save (PHB p.243)
    // ================================================================

    private static bool IsIceStormSpell(SpellData spell)
    {
        return spell != null && string.Equals(spell.SpellId, SpellNames.ICE_STORM, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolves Ice Storm: 3d6 bludgeoning + 2d6 cold (no save), SR: Yes.
    /// Area becomes difficult terrain for 1 round (logged for awareness).
    /// PHB p.243
    /// </summary>
    private bool TryResolveIceStormAoE(
        CharacterController caster,
        SpellData spell,
        List<CharacterController> targets,
        HashSet<Vector2Int> aoeCells,
        out string log)
    {
        log = string.Empty;
        if (caster == null || caster.Stats == null || spell == null)
            return false;

        if (!IsIceStormSpell(spell))
            return false;

        int casterLevel = SpellCastingHelper.GetEffectiveCasterLevel(caster, spell);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"❄ {caster.Stats.CharacterName} casts Ice Storm! (20-ft radius cylinder)");
        sb.AppendLine($"  [Level {spell.SpellLevel}] {spell.School}");
        sb.AppendLine($"  Damage: 3d6 bludgeoning + 2d6 cold (NO SAVE)");
        sb.AppendLine($"  SR: Yes | Area becomes icy difficult terrain for 1 round");
        sb.AppendLine($"  Targets: {(targets != null ? targets.Count : 0)} creature(s)");
        sb.AppendLine();

        if (targets == null || targets.Count == 0)
        {
            sb.AppendLine($"  No valid targets in area!");
        }
        else
        {
            int targetIndex = 0;
            foreach (CharacterController target in targets)
            {
                if (target == null || target.Stats == null || target.Stats.IsDead)
                    continue;

                targetIndex++;
                sb.AppendLine($"  --- Target {targetIndex}: {target.Stats.CharacterName} ---");

                // Check Spell Resistance
                var srResult = SpellSaveResolver.RollSpellResistance(caster, target, casterLevel);
                srResult.AppendToLog(sb);
                if (!srResult.Overcame)
                {
                    sb.AppendLine($"  {target.Stats.CharacterName} resists Ice Storm via Spell Resistance!");
                    sb.AppendLine();
                    continue;
                }

                // Roll 3d6 bludgeoning
                int bludgeoningDamage = 0;
                for (int i = 0; i < 3; i++)
                    bludgeoningDamage += DiceRoller.D6();

                // Roll 2d6 cold
                int coldDamage = 0;
                for (int i = 0; i < 2; i++)
                    coldDamage += DiceRoller.D6();

                int totalDamage = bludgeoningDamage + coldDamage;

                // D&D 3.5e PHB p.206: Blinking creatures take half damage from area attacks
                bool targetIsBlinking = target.HasActiveBlinkEffect;
                if (targetIsBlinking)
                    totalDamage = Mathf.Max(1, totalDamage / 2);

                sb.AppendLine($"  Damage: {bludgeoningDamage} bludgeoning + {coldDamage} cold = {totalDamage} total (no save)");
                if (targetIsBlinking)
                    sb.AppendLine($"  Blink: area damage halved");

                int hpBefore = target.Stats.CurrentHP;
                target.Stats.TakeDamage(totalDamage);
                int hpAfter = target.Stats.CurrentHP;

                sb.AppendLine($"  {target.Stats.CharacterName}: {hpBefore} → {hpAfter} HP");

                CheckConcentrationOnDamage(target, totalDamage);

                if (target.Stats.IsDead)
                {
                    target.OnDeath();
                    HandleSummonDeathCleanup(target);
                    sb.AppendLine($"  💀 {target.Stats.CharacterName} has been slain!");
                }

                sb.AppendLine();
            }
        }

        // ── WALL OF ICE DAMAGE FROM ICE STORM ──
        // Only damage the specific wall sections that overlap the Ice Storm AoE.
        if (aoeCells != null && AreaEffectManager.HasInstance)
        {
            var wallOverlap = new Dictionary<WallOfIceAreaEffect, HashSet<Vector2Int>>();
            foreach (Vector2Int cell in aoeCells)
            {
                WallOfIceAreaEffect wall = WallOfIceAreaEffect.GetWallAtCell(cell);
                if (wall != null)
                {
                    if (!wallOverlap.ContainsKey(wall))
                        wallOverlap[wall] = new HashSet<Vector2Int>();
                    wallOverlap[wall].Add(cell);
                }
            }

            foreach (var kvp in wallOverlap)
            {
                WallOfIceAreaEffect wall = kvp.Key;
                HashSet<Vector2Int> overlapCells = kvp.Value;
                if (wall == null || wall.WallHP <= 0)
                    continue;

                // 3d6 bludgeoning + 2d6 cold (no save for objects)
                int wallBludg = 0;
                for (int i = 0; i < 3; i++) wallBludg += DiceRoller.D6();
                int wallCold = 0;
                for (int i = 0; i < 2; i++) wallCold += DiceRoller.D6();
                int wallTotal = wallBludg + wallCold;

                sb.AppendLine($"  --- Wall of Ice ({overlapCells.Count} section(s) hit) ---");
                sb.AppendLine($"  Bludgeoning + cold damage to overlapping sections: {wallTotal}");

                bool destroyed = wall.DealDamageToOverlappingCells(wallTotal, overlapCells, false);

                if (destroyed)
                    sb.AppendLine($"  💥 The Wall of Ice is destroyed by Ice Storm!");
                else
                    sb.AppendLine($"  Wall HP: {wall.WallHP}/{wall.WallMaxHP}");

                sb.AppendLine();
            }
        }

        // Note about difficult terrain
        sb.AppendLine("  ❄ Area is covered in ice (difficult terrain for 1 round)");
        sb.Append("═══════════════════════════════════");
        log = sb.ToString();
        return true;
    }

}

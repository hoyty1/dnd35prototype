using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dedicated panel/controller for character information display in CombatUI.
/// Owns party + NPC stat rendering, buffs, active highlights, and portrait/icon assignment.
/// </summary>
public class CharacterInfoPanel : MonoBehaviour
{
    private CombatUI _combatUI;

    public void Initialize(CombatUI combatUI)
    {
        _combatUI = combatUI;
    }

    public void UpdateAllStats(CharacterController pc1, CharacterController pc2, CharacterController npc)
    {
        if (_combatUI == null)
            return;

        UpdateCharacterStats(pc1, _combatUI.PC1NameText, _combatUI.PC1HPText, _combatUI.PC1ACText, _combatUI.PC1AtkText, _combatUI.PC1SpeedText, _combatUI.PC1AbilityText, _combatUI.PC1HPBar);
        UpdateCharacterStats(pc2, _combatUI.PC2NameText, _combatUI.PC2HPText, _combatUI.PC2ACText, _combatUI.PC2AtkText, _combatUI.PC2SpeedText, _combatUI.PC2AbilityText, _combatUI.PC2HPBar);
        UpdateCharacterStats(npc, _combatUI.NPCNameText, _combatUI.NPCHPText, _combatUI.NPCACText, _combatUI.NPCAtkText, _combatUI.NPCSpeedText, _combatUI.NPCAbilityText, _combatUI.NPCHPBar);
    }

    public void UpdateAllStats4PC(List<CharacterController> pcs, List<CharacterController> npcs)
    {
        if (_combatUI == null)
            return;

        pcs ??= new List<CharacterController>();
        npcs ??= new List<CharacterController>();

        if (pcs.Count > 0) UpdateCharacterStats(pcs[0], _combatUI.PC1NameText, _combatUI.PC1HPText, _combatUI.PC1ACText, _combatUI.PC1AtkText, _combatUI.PC1SpeedText, _combatUI.PC1AbilityText, _combatUI.PC1HPBar);
        if (pcs.Count > 1) UpdateCharacterStats(pcs[1], _combatUI.PC2NameText, _combatUI.PC2HPText, _combatUI.PC2ACText, _combatUI.PC2AtkText, _combatUI.PC2SpeedText, _combatUI.PC2AbilityText, _combatUI.PC2HPBar);
        if (pcs.Count > 2) UpdateCharacterStats(pcs[2], _combatUI.PC3NameText, _combatUI.PC3HPText, _combatUI.PC3ACText, _combatUI.PC3AtkText, _combatUI.PC3SpeedText, _combatUI.PC3AbilityText, _combatUI.PC3HPBar);
        if (pcs.Count > 3) UpdateCharacterStats(pcs[3], _combatUI.PC4NameText, _combatUI.PC4HPText, _combatUI.PC4ACText, _combatUI.PC4AtkText, _combatUI.PC4SpeedText, _combatUI.PC4AbilityText, _combatUI.PC4HPBar);

        if (pcs.Count > 0) UpdateBuffDisplay(pcs[0], _combatUI.PC1BuffText);
        if (pcs.Count > 1) UpdateBuffDisplay(pcs[1], _combatUI.PC2BuffText);
        if (pcs.Count > 2) UpdateBuffDisplay(pcs[2], _combatUI.PC3BuffText);
        if (pcs.Count > 3) UpdateBuffDisplay(pcs[3], _combatUI.PC4BuffText);

        for (int i = 0; i < _combatUI.NPCPanels.Count && i < npcs.Count; i++)
        {
            NPCPanelUI p = _combatUI.NPCPanels[i];
            UpdateCharacterStats(npcs[i], p.NameText, p.HPText, p.ACText, p.AtkText, p.SpeedText, p.AbilityText, p.HPBar);
        }

        if (npcs.Count > 0)
            UpdateCharacterStats(npcs[0], _combatUI.NPCNameText, _combatUI.NPCHPText, _combatUI.NPCACText, _combatUI.NPCAtkText, _combatUI.NPCSpeedText, _combatUI.NPCAbilityText, _combatUI.NPCHPBar);
    }

    public void UpdateAllStatsMultiNPC(CharacterController pc1, CharacterController pc2, List<CharacterController> npcs)
    {
        if (_combatUI == null)
            return;

        npcs ??= new List<CharacterController>();

        UpdateCharacterStats(pc1, _combatUI.PC1NameText, _combatUI.PC1HPText, _combatUI.PC1ACText, _combatUI.PC1AtkText, _combatUI.PC1SpeedText, _combatUI.PC1AbilityText, _combatUI.PC1HPBar);
        UpdateCharacterStats(pc2, _combatUI.PC2NameText, _combatUI.PC2HPText, _combatUI.PC2ACText, _combatUI.PC2AtkText, _combatUI.PC2SpeedText, _combatUI.PC2AbilityText, _combatUI.PC2HPBar);

        for (int i = 0; i < _combatUI.NPCPanels.Count && i < npcs.Count; i++)
        {
            NPCPanelUI p = _combatUI.NPCPanels[i];
            UpdateCharacterStats(npcs[i], p.NameText, p.HPText, p.ACText, p.AtkText, p.SpeedText, p.AbilityText, p.HPBar);
        }

        if (npcs.Count > 0)
            UpdateCharacterStats(npcs[0], _combatUI.NPCNameText, _combatUI.NPCHPText, _combatUI.NPCACText, _combatUI.NPCAtkText, _combatUI.NPCSpeedText, _combatUI.NPCAbilityText, _combatUI.NPCHPBar);
    }

    public void SetActivePC(int pcNumber)
    {
        if (_combatUI == null)
            return;

        if (_combatUI.PC1ActiveIndicator != null) _combatUI.PC1ActiveIndicator.enabled = (pcNumber == 1);
        if (_combatUI.PC2ActiveIndicator != null) _combatUI.PC2ActiveIndicator.enabled = (pcNumber == 2);
        if (_combatUI.PC3ActiveIndicator != null) _combatUI.PC3ActiveIndicator.enabled = (pcNumber == 3);
        if (_combatUI.PC4ActiveIndicator != null) _combatUI.PC4ActiveIndicator.enabled = (pcNumber == 4);

        SetPanelActiveState(_combatUI.PC1Panel, pcNumber == 1, new Color(0.1f, 0.3f, 0.5f, 0.95f), new Color(0.1f, 0.2f, 0.4f, 0.65f));
        SetPanelActiveState(_combatUI.PC2Panel, pcNumber == 2, new Color(0.1f, 0.2f, 0.5f, 0.95f), new Color(0.1f, 0.15f, 0.35f, 0.65f));
        SetPanelActiveState(_combatUI.PC3Panel, pcNumber == 3, new Color(0.15f, 0.3f, 0.4f, 0.95f), new Color(0.1f, 0.2f, 0.3f, 0.65f));
        SetPanelActiveState(_combatUI.PC4Panel, pcNumber == 4, new Color(0.3f, 0.15f, 0.15f, 0.95f), new Color(0.25f, 0.1f, 0.1f, 0.65f));
    }

    public void SetActiveNPC(int npcIndex)
    {
        if (_combatUI == null)
            return;

        for (int i = 0; i < _combatUI.NPCPanels.Count; i++)
        {
            NPCPanelUI p = _combatUI.NPCPanels[i];
            bool isActive = i == npcIndex;

            if (p.ActiveIndicator != null)
                p.ActiveIndicator.enabled = isActive;

            SetPanelActiveState(p.Panel, isActive,
                new Color(0.4f, 0.15f, 0.15f, 0.95f),
                new Color(0.3f, 0.1f, 0.1f, 0.65f));
        }
    }

    public void SetPCIcon(int pcNumber, Sprite icon)
    {
        if (_combatUI == null)
            return;

        Image target = null;
        switch (pcNumber)
        {
            case 1: target = _combatUI.PC1Icon; break;
            case 2: target = _combatUI.PC2Icon; break;
            case 3: target = _combatUI.PC3Icon; break;
            case 4: target = _combatUI.PC4Icon; break;
        }

        if (target != null && icon != null)
        {
            target.sprite = icon;
            target.enabled = true;
        }
    }

    public void SetNPCIcon(int npcIndex, Sprite icon)
    {
        if (_combatUI == null)
            return;

        if (npcIndex < _combatUI.NPCPanels.Count && _combatUI.NPCPanels[npcIndex].IconImage != null)
        {
            _combatUI.NPCPanels[npcIndex].IconImage.sprite = icon;
            _combatUI.NPCPanels[npcIndex].IconImage.enabled = icon != null;
        }
    }

    private void UpdateCharacterStats(
        CharacterController ch,
        Text nameText,
        Text hpText,
        Text acText,
        Text atkText,
        Text speedText,
        Text abilityText,
        Image hpBar)
    {
        if (ch == null || ch.Stats == null)
            return;

        CharacterStats s = ch.Stats;

        if (nameText != null)
        {
            string shownRace = ch != null ? ch.DisplayedRace : s.RaceName;
            string raceStr = !string.IsNullOrEmpty(shownRace) ? $"{shownRace} " : "";
            string sizeStr = (s.SizeCategoryName != "Medium") ? $" [{s.SizeCategoryName}]" : "";
            string displayName = s.CharacterName;
            if (GameManager.Instance != null)
                displayName = GameManager.Instance.GetSummonDisplayName(ch);

            nameText.text = $"{displayName} (Lv {s.Level} {raceStr}{s.CharacterClass}){sizeStr}";
            if (ch.CurrentHPState == HPState.Dead)
                nameText.text += " (DEAD)";
        }

        int totalMax = s.TotalMaxHP;
        if (totalMax <= 0)
            totalMax = 1;

        if (hpText != null)
        {
            string hpStateTag = ch.CurrentHPState == HPState.Healthy ? string.Empty : $" [{ch.CurrentHPState}]";
            hpText.text = $"HP: {s.CurrentHP}/{totalMax}{hpStateTag}";
        }

        if (acText != null)
        {
            int effectiveDex = s.DEXMod;
            if (s.MaxDexBonus >= 0 && effectiveDex > s.MaxDexBonus)
                effectiveDex = s.MaxDexBonus;

            string dexLabel = (s.MaxDexBonus >= 0 && s.DEXMod > s.MaxDexBonus) ? "DEX*" : "DEX";
            string sizeDetail = FormatBonusDetail(s.SizeModifier, "Size");
            string defensiveDetail = ch.IsFightingDefensively ? " +2 Fighting Defensively" : "";
            string acDetails = $"AC: {s.ArmorClass + (ch.IsFightingDefensively ? 2 : 0)} (10{FormatBonusDetail(effectiveDex, dexLabel)}{FormatBonusDetail(s.ArmorBonus, "Armor")}{FormatBonusDetail(s.ShieldBonus, "Shield")}{FormatBonusDetail(s.NaturalArmorBonus, "Natural")}{sizeDetail}{defensiveDetail})";
            if (s.ArmorCheckPenalty > 0)
                acDetails += $" ACP:-{s.ArmorCheckPenalty}";
            acText.text = acDetails;
        }

        if (atkText != null)
        {
            string sizeAtkStr = s.SizeModifier != 0 ? $" {CharacterStats.FormatMod(s.SizeModifier)} Size" : "";
            atkText.text = $"Atk: {CharacterStats.FormatMod(s.AttackBonus)} (BAB {CharacterStats.FormatMod(s.BaseAttackBonus)} {CharacterStats.FormatMod(s.STRMod)} STR{sizeAtkStr})";
        }

        if (speedText != null)
        {
            string speedExtra = $" | Load: {s.EncumbranceSummary}";
            if (s.LandSpeedEnhancementBonusFeet > 0)
                speedExtra += $" | ER +{s.LandSpeedEnhancementBonusFeet} ft";
            if (s.SpeedNotReducedByArmor)
                speedExtra += " (no armor speed reduction)";
            speedText.text = $"Speed: {s.MoveRange} sq ({s.SpeedInFeet} ft){speedExtra}";
        }

        if (abilityText != null)
        {
            abilityText.supportRichText = true;
            abilityText.text =
                $"{s.GetAbilityStringWithRacial("STR", s.STR, s.GetRacialModifier("STR"))} " +
                $"{s.GetAbilityStringWithRacial("DEX", s.DEX, s.GetRacialModifier("DEX"))} " +
                $"{s.GetAbilityStringWithRacial("CON", s.CON, s.GetRacialModifier("CON"))}\n" +
                $"{s.GetAbilityStringWithRacial("WIS", s.WIS, s.GetRacialModifier("WIS"))} " +
                $"{s.GetAbilityStringWithRacial("INT", s.INT, s.GetRacialModifier("INT"))} " +
                $"{s.GetAbilityStringWithRacial("CHA", s.CHA, s.GetRacialModifier("CHA"))}";
        }

        if (hpBar != null)
        {
            float hpPercent = totalMax > 0 ? Mathf.Clamp01((float)Mathf.Max(0, s.CurrentHP) / totalMax) : 0f;
            hpBar.fillAmount = hpPercent;

            Color hpColor;
            if (hpPercent > 0.5f)
                hpColor = new Color(0.2f, 0.8f, 0.2f, 1f);
            else if (hpPercent > 0.25f)
                hpColor = new Color(0.8f, 0.8f, 0.2f, 1f);
            else
                hpColor = new Color(0.8f, 0.2f, 0.2f, 1f);

            hpBar.color = hpColor;
        }
    }

    private void UpdateBuffDisplay(CharacterController ch, Text buffText)
    {
        if (buffText == null)
            return;

        if (ch == null || ch.Stats == null)
        {
            buffText.text = "";
            return;
        }

        StatusEffectManager statusMgr = ch.GetComponent<StatusEffectManager>();
        ConcentrationManager concMgr = ch.GetComponent<ConcentrationManager>();
        SpellcastingComponent spellComp = ch.GetComponent<SpellcastingComponent>();

        string buffStr = (statusMgr != null && statusMgr.ActiveEffectCount > 0)
            ? statusMgr.GetBuffSummaryString()
            : "";
        string concStr = (concMgr != null && concMgr.IsConcentrating)
            ? concMgr.GetConcentrationDisplayString()
            : "";
        string heldStr = (spellComp != null && spellComp.HasHeldTouchCharge && spellComp.HeldTouchSpell != null)
            ? $"✋ Holding: {spellComp.HeldTouchSpell.Name}"
            : "";
        string condStr = ch.Stats.GetConditionSummary();
        string diseaseStr = ch.GetActiveDiseaseSummary();
        string poisonStr = ch.GetActivePoisonSummary();

        if (string.IsNullOrEmpty(buffStr)
            && string.IsNullOrEmpty(concStr)
            && string.IsNullOrEmpty(heldStr)
            && string.IsNullOrEmpty(condStr)
            && string.IsNullOrEmpty(diseaseStr)
            && string.IsNullOrEmpty(poisonStr))
        {
            buffText.text = "";
            buffText.gameObject.SetActive(false);
            return;
        }

        buffText.gameObject.SetActive(true);
        buffText.supportRichText = true;

        List<string> parts = new List<string>();
        if (!string.IsNullOrEmpty(concStr)) parts.Add(concStr);
        if (!string.IsNullOrEmpty(heldStr)) parts.Add(heldStr);
        if (!string.IsNullOrEmpty(condStr)) parts.Add(condStr);
        if (!string.IsNullOrEmpty(diseaseStr)) parts.Add($"🦠 {diseaseStr}");
        if (!string.IsNullOrEmpty(poisonStr)) parts.Add($"☠ {poisonStr}");
        if (!string.IsNullOrEmpty(buffStr)) parts.Add(buffStr);
        buffText.text = string.Join(" | ", parts);
    }

    private static string FormatBonusDetail(int value, string label)
    {
        if (value == 0)
            return "";

        return value > 0 ? $"+{value} {label}" : $"{value} {label}";
    }

    private static void SetPanelActiveState(GameObject panel, bool active, Color activeColor, Color inactiveColor)
    {
        if (panel == null)
            return;

        Image panelImg = panel.GetComponent<Image>();
        if (panelImg != null)
            panelImg.color = active ? activeColor : inactiveColor;
    }
}

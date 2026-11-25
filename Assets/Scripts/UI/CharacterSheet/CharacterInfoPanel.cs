using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DND35e.Identifiers;

/// <summary>
/// Dedicated panel/controller for character information display in CombatUI.
/// Owns party + NPC stat rendering, buffs, active highlights, and portrait/icon assignment.
/// The stat update is split into Capture (the values of every row write, read at call time) and Apply (the writes),
/// so the presentation layer can show a snapshot later (grouped AI turns step 8; Passthrough applies it at once).
/// </summary>
public class CharacterInfoPanel : MonoBehaviour
{
    private CombatUI _combatUI;

    public void Initialize(CombatUI combatUI)
    {
        _combatUI = combatUI;
    }

    /// <summary>Captures and applies at once (CombatUI routes through Capture and the presentation facade).</summary>
    public void UpdateAllStats(CharacterController pc1, CharacterController pc2, CharacterController npc)
    {
        Apply(CaptureAllStats(pc1, pc2, npc));
    }

    /// <summary>Captures and applies at once (CombatUI routes through Capture and the presentation facade).</summary>
    public void UpdateAllStats4PC(List<CharacterController> pcs, List<CharacterController> npcs)
    {
        Apply(CaptureAllStats4PC(pcs, npcs));
    }

    /// <summary>Captures and applies at once (CombatUI routes through Capture and the presentation facade).</summary>
    public void UpdateAllStatsMultiNPC(CharacterController pc1, CharacterController pc2, List<CharacterController> npcs)
    {
        Apply(CaptureAllStatsMultiNPC(pc1, pc2, npcs));
    }

    // ── Capture: every row write's values, read at call time in the order the update wrote them. Captured and then
    // applied at once, this is exactly the former update. ──

    /// <summary>The rows of the legacy two-PC, one-NPC update (null without its CombatUI).</summary>
    public PanelSnapshot CaptureAllStats(CharacterController pc1, CharacterController pc2, CharacterController npc)
    {
        if (_combatUI == null)
            return null;

        var snap = new PanelSnapshot();
        CaptureCharacterStats(snap, "PC1", pc1, _combatUI.PC1NameText, _combatUI.PC1HPText, _combatUI.PC1ACText, _combatUI.PC1AtkText, _combatUI.PC1SpeedText, _combatUI.PC1AbilityText, _combatUI.PC1HPBar);
        CaptureCharacterStats(snap, "PC2", pc2, _combatUI.PC2NameText, _combatUI.PC2HPText, _combatUI.PC2ACText, _combatUI.PC2AtkText, _combatUI.PC2SpeedText, _combatUI.PC2AbilityText, _combatUI.PC2HPBar);
        CaptureCharacterStats(snap, "NPC", npc, _combatUI.NPCNameText, _combatUI.NPCHPText, _combatUI.NPCACText, _combatUI.NPCAtkText, _combatUI.NPCSpeedText, _combatUI.NPCAbilityText, _combatUI.NPCHPBar);
        return snap;
    }

    /// <summary>The rows of the four-PC, multi-NPC update (null without its CombatUI).</summary>
    public PanelSnapshot CaptureAllStats4PC(List<CharacterController> pcs, List<CharacterController> npcs)
    {
        if (_combatUI == null)
            return null;

        pcs ??= new List<CharacterController>();
        npcs ??= new List<CharacterController>();

        var snap = new PanelSnapshot();
        if (pcs.Count > 0) CaptureCharacterStats(snap, "PC1", pcs[0], _combatUI.PC1NameText, _combatUI.PC1HPText, _combatUI.PC1ACText, _combatUI.PC1AtkText, _combatUI.PC1SpeedText, _combatUI.PC1AbilityText, _combatUI.PC1HPBar);
        if (pcs.Count > 1) CaptureCharacterStats(snap, "PC2", pcs[1], _combatUI.PC2NameText, _combatUI.PC2HPText, _combatUI.PC2ACText, _combatUI.PC2AtkText, _combatUI.PC2SpeedText, _combatUI.PC2AbilityText, _combatUI.PC2HPBar);
        if (pcs.Count > 2) CaptureCharacterStats(snap, "PC3", pcs[2], _combatUI.PC3NameText, _combatUI.PC3HPText, _combatUI.PC3ACText, _combatUI.PC3AtkText, _combatUI.PC3SpeedText, _combatUI.PC3AbilityText, _combatUI.PC3HPBar);
        if (pcs.Count > 3) CaptureCharacterStats(snap, "PC4", pcs[3], _combatUI.PC4NameText, _combatUI.PC4HPText, _combatUI.PC4ACText, _combatUI.PC4AtkText, _combatUI.PC4SpeedText, _combatUI.PC4AbilityText, _combatUI.PC4HPBar);

        if (pcs.Count > 0) CaptureBuffDisplay(snap, "PC1", pcs[0], _combatUI.PC1BuffText);
        if (pcs.Count > 1) CaptureBuffDisplay(snap, "PC2", pcs[1], _combatUI.PC2BuffText);
        if (pcs.Count > 2) CaptureBuffDisplay(snap, "PC3", pcs[2], _combatUI.PC3BuffText);
        if (pcs.Count > 3) CaptureBuffDisplay(snap, "PC4", pcs[3], _combatUI.PC4BuffText);

        for (int i = 0; i < _combatUI.NPCPanels.Count && i < npcs.Count; i++)
        {
            NPCPanelUI p = _combatUI.NPCPanels[i];
            CaptureCharacterStats(snap, "NPC" + i, npcs[i], p.NameText, p.HPText, p.ACText, p.AtkText, p.SpeedText, p.AbilityText, p.HPBar);
        }

        if (npcs.Count > 0)
            CaptureCharacterStats(snap, "NPC", npcs[0], _combatUI.NPCNameText, _combatUI.NPCHPText, _combatUI.NPCACText, _combatUI.NPCAtkText, _combatUI.NPCSpeedText, _combatUI.NPCAbilityText, _combatUI.NPCHPBar);
        return snap;
    }

    /// <summary>The rows of the legacy two-PC, multi-NPC update (null without its CombatUI).</summary>
    public PanelSnapshot CaptureAllStatsMultiNPC(CharacterController pc1, CharacterController pc2, List<CharacterController> npcs)
    {
        if (_combatUI == null)
            return null;

        npcs ??= new List<CharacterController>();

        var snap = new PanelSnapshot();
        CaptureCharacterStats(snap, "PC1", pc1, _combatUI.PC1NameText, _combatUI.PC1HPText, _combatUI.PC1ACText, _combatUI.PC1AtkText, _combatUI.PC1SpeedText, _combatUI.PC1AbilityText, _combatUI.PC1HPBar);
        CaptureCharacterStats(snap, "PC2", pc2, _combatUI.PC2NameText, _combatUI.PC2HPText, _combatUI.PC2ACText, _combatUI.PC2AtkText, _combatUI.PC2SpeedText, _combatUI.PC2AbilityText, _combatUI.PC2HPBar);

        for (int i = 0; i < _combatUI.NPCPanels.Count && i < npcs.Count; i++)
        {
            NPCPanelUI p = _combatUI.NPCPanels[i];
            CaptureCharacterStats(snap, "NPC" + i, npcs[i], p.NameText, p.HPText, p.ACText, p.AtkText, p.SpeedText, p.AbilityText, p.HPBar);
        }

        if (npcs.Count > 0)
            CaptureCharacterStats(snap, "NPC", npcs[0], _combatUI.NPCNameText, _combatUI.NPCHPText, _combatUI.NPCACText, _combatUI.NPCAtkText, _combatUI.NPCSpeedText, _combatUI.NPCAbilityText, _combatUI.NPCHPBar);
        return snap;
    }

    /// <summary>
    /// Writes a captured update (the Apply half): every row in capture order, to the targets captured with it; a
    /// destroyed target is skipped. Each row also goes to the presentation trace while it records (unhashed).
    /// </summary>
    public static void Apply(PanelSnapshot snap)
    {
        if (snap == null)
            return;
        for (int i = 0; i < snap.Rows.Count; i++)
        {
            PanelRowSnapshot row = snap.Rows[i];
            if (row.IsBuffRow)
                ApplyBuffDisplay(row);
            else
                ApplyCharacterStats(row);
            if (PresentationTrace.Recording)
                PresentationTrace.State((row.IsBuffRow ? "buff:" : "panel:") + row.Slot, row.Encode());
        }
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

    /// <summary>
    /// The Capture half of the former UpdateCharacterStats: the same reads in the same order, kept for each target that
    /// exists (no row without a creature and its stats).
    /// </summary>
    private void CaptureCharacterStats(
        PanelSnapshot snap,
        string slot,
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
        var row = new PanelRowSnapshot
        {
            Slot = slot,
            Creature = ch,
            NameText = nameText,
            HpText = hpText,
            AcText = acText,
            AtkText = atkText,
            SpeedText = speedText,
            AbilityText = abilityText,
            HpBar = hpBar,
        };

        if (nameText != null)
        {
            string shownRace = ch != null ? ch.DisplayedRace : s.RaceName;
            string raceStr = !string.IsNullOrEmpty(shownRace) ? $"{shownRace} " : "";
            string sizeStr = (s.SizeCategoryName != "Medium") ? $" [{s.SizeCategoryName}]" : "";
            string displayName = s.CharacterName;
            if (GameManager.Instance != null)
                displayName = GameManager.Instance.GetSummonDisplayName(ch);

            string crText = string.IsNullOrWhiteSpace(s.ChallengeRating) ? string.Empty : $" | CR {s.ChallengeRatingDisplay}";
            row.Name = $"{displayName} (Lv {s.Level} {raceStr}{s.ClassSummary}{crText}){sizeStr}";
            if (ch.CurrentHPState == HPState.Dead)
            {
                row.Name += " (DEAD)";
                row.Dead = true;
            }
        }

        int totalMax = s.TotalMaxHP;
        if (totalMax <= 0)
            totalMax = 1;

        if (hpText != null)
        {
            string hpStateTag = ch.CurrentHPState == HPState.Healthy ? string.Empty : $" [{ch.CurrentHPState}]";
            row.Hp = $"HP: {s.CurrentHP}/{totalMax}{hpStateTag}";
        }

        if (acText != null)
        {
            int effectiveDex = s.DEXMod;
            if (s.MaxDexBonus >= 0 && effectiveDex > s.MaxDexBonus)
                effectiveDex = s.MaxDexBonus;

            string dexLabel = (s.MaxDexBonus >= 0 && s.DEXMod > s.MaxDexBonus) ? "DEX*" : "DEX";
            string sizeDetail = FormatBonusDetail(s.SizeModifier, "Size");
            string defensiveDetail = ch.IsFightingDefensively ? " +2 Fighting Defensively" : "";
            string acDetails = $"AC: {s.ArmorClass + (ch.IsFightingDefensively ? 2 : 0)} (10{FormatBonusDetail(effectiveDex, dexLabel)}{FormatBonusDetail(s.ArmorBonus, "Armor")}{FormatBonusDetail(s.ShieldBonus, "Shield")}{FormatBonusDetail(s.NaturalArmorBonus, "Natural")}{FormatBonusDetail(s.NaturalArmorEnhancementBonus, "Natural enh")}{sizeDetail}{defensiveDetail})";
            if (s.ArmorCheckPenalty > 0)
                acDetails += $" ACP:-{s.ArmorCheckPenalty}";
            row.Ac = acDetails;
        }

        if (atkText != null)
        {
            string sizeAtkStr = s.SizeModifier != 0 ? $" {CharacterStats.FormatMod(s.SizeModifier)} Size" : "";
            row.Atk = $"Atk: {CharacterStats.FormatMod(s.AttackBonus)} (BAB {CharacterStats.FormatMod(s.BaseAttackBonus)} {CharacterStats.FormatMod(s.STRMod)} STR{sizeAtkStr})";
        }

        if (speedText != null)
        {
            string speedExtra = $" | Load: {s.EncumbranceSummary}";
            if (s.LandSpeedEnhancementBonusFeet > 0)
                speedExtra += $" | ER +{s.LandSpeedEnhancementBonusFeet} ft";
            if (s.SpeedNotReducedByArmor)
                speedExtra += " (no armor speed reduction)";
            row.Speed = $"Speed: {s.MoveRange} sq ({s.SpeedInFeet} ft){speedExtra}";
        }

        if (abilityText != null)
        {
            row.Ability =
                $"STR {s.GetAbilityScoreDisplay(AbilityType.STR)} ({s.GetAbilityModifierDisplay(AbilityType.STR)}) " +
                $"DEX {s.GetAbilityScoreDisplay(AbilityType.DEX)} ({s.GetAbilityModifierDisplay(AbilityType.DEX)}) " +
                $"CON {s.GetAbilityScoreDisplay(AbilityType.CON)} ({s.GetAbilityModifierDisplay(AbilityType.CON)})\n" +
                $"WIS {s.GetAbilityScoreDisplay(AbilityType.WIS)} ({s.GetAbilityModifierDisplay(AbilityType.WIS)}) " +
                $"INT {s.GetAbilityScoreDisplay(AbilityType.INT)} ({s.GetAbilityModifierDisplay(AbilityType.INT)}) " +
                $"CHA {s.GetAbilityScoreDisplay(AbilityType.CHA)} ({s.GetAbilityModifierDisplay(AbilityType.CHA)})";
        }

        if (hpBar != null)
        {
            float hpPercent = totalMax > 0 ? Mathf.Clamp01((float)Mathf.Max(0, s.CurrentHP) / totalMax) : 0f;
            row.BarFill = hpPercent;

            Color hpColor;
            if (hpPercent > 0.5f)
                hpColor = new Color(0.2f, 0.8f, 0.2f, 1f);
            else if (hpPercent > 0.25f)
                hpColor = new Color(0.8f, 0.8f, 0.2f, 1f);
            else
                hpColor = new Color(0.8f, 0.2f, 0.2f, 1f);

            row.BarColor = hpColor;
        }

        snap.Rows.Add(row);
    }

    /// <summary>The Apply half of the former UpdateCharacterStats: writes each captured field to its target.</summary>
    private static void ApplyCharacterStats(PanelRowSnapshot row)
    {
        if (row.NameText != null && row.Name != null)
            row.NameText.text = row.Name;
        if (row.HpText != null && row.Hp != null)
            row.HpText.text = row.Hp;
        if (row.AcText != null && row.Ac != null)
            row.AcText.text = row.Ac;
        if (row.AtkText != null && row.Atk != null)
            row.AtkText.text = row.Atk;
        if (row.SpeedText != null && row.Speed != null)
            row.SpeedText.text = row.Speed;
        if (row.AbilityText != null && row.Ability != null)
        {
            row.AbilityText.supportRichText = true;
            row.AbilityText.text = row.Ability;
        }
        if (row.HpBar != null)
        {
            row.HpBar.fillAmount = row.BarFill;
            row.HpBar.color = row.BarColor;
        }
    }

    /// <summary>The Capture half of the former UpdateBuffDisplay (no row without its text target).</summary>
    private void CaptureBuffDisplay(PanelSnapshot snap, string slot, CharacterController ch, Text buffText)
    {
        if (buffText == null)
            return;

        var row = new PanelRowSnapshot { Slot = slot, IsBuffRow = true, Creature = ch, BuffText = buffText, Buff = "" };
        snap.Rows.Add(row);

        if (ch == null || ch.Stats == null)
        {
            row.BuffShown = null; // the text is cleared, the row's active state is left as it is
            return;
        }

        StatusEffectManager statusMgr = ch.StatusEffectManager;
        ConcentrationManager concMgr = ch.Concentration;
        SpellcastingComponent spellComp = ch.Spellcasting;

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
            row.BuffShown = false;
            return;
        }

        List<string> parts = new List<string>();
        if (!string.IsNullOrEmpty(concStr)) parts.Add(concStr);
        if (!string.IsNullOrEmpty(heldStr)) parts.Add(heldStr);
        if (!string.IsNullOrEmpty(condStr)) parts.Add(condStr);
        if (!string.IsNullOrEmpty(diseaseStr)) parts.Add($"🦠 {diseaseStr}");
        if (!string.IsNullOrEmpty(poisonStr)) parts.Add($"☠ {poisonStr}");
        if (!string.IsNullOrEmpty(buffStr)) parts.Add(buffStr);
        row.Buff = string.Join(" | ", parts);
        row.BuffShown = true;
    }

    /// <summary>The Apply half of the former UpdateBuffDisplay.</summary>
    private static void ApplyBuffDisplay(PanelRowSnapshot row)
    {
        Text buffText = row.BuffText;
        if (buffText == null)
            return;

        if (row.BuffShown == null)
        {
            buffText.text = "";
            return;
        }

        if (row.BuffShown == false)
        {
            buffText.text = "";
            buffText.gameObject.SetActive(false);
            return;
        }

        buffText.gameObject.SetActive(true);
        buffText.supportRichText = true;
        buffText.text = row.Buff;
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

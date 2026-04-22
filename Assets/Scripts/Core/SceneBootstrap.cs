using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Bootstraps the entire game scene programmatically.
/// Attach this to a single empty GameObject in the scene.
/// Creates all required GameObjects, components, and UI at runtime.
///
/// NEW LAYOUT (reorganized):
///   ┌──────────────────────────────────────────────────┐
///   │  [Turn Indicator / Initiative - top center]       │
///   ├──────┬───────────────────────────────┬────────────┤
///   │ Party│                               │  NPC Info  │
///   │ Panel│      HEX GRID AREA            │  (stacked) │
///   │ (L)  │      (center)                 │  (R)       │
///   ├──────┴───────────────────────────────┴────────────┤
///   │  Combat Log (left 60%)  │  Action Buttons (40%)   │
///   └──────────────────────────────────────────────────┘
/// </summary>
public class SceneBootstrap : MonoBehaviour
{
    private void Awake()
    {
        SetupCamera();
        SquareGrid grid = CreateSquareGrid();
        var (pcs, npc, npcList) = CreateCharacters();
        CombatUI combatUI = CreateUI();
        SetupGameManager(grid, pcs, npc, combatUI, npcList);
    }

    // ========== CAMERA ==========
    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGO = new GameObject("Main Camera");
            cam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.orthographicSize = 10f;
        cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        cam.clearFlags = CameraClearFlags.SolidColor;

        // Add camera zoom controller for battlefield navigation.
        CameraController cameraController = cam.GetComponent<CameraController>();
        if (cameraController == null)
            cameraController = cam.gameObject.AddComponent<CameraController>();

        cameraController.minZoom = 0.5f;
        cameraController.maxZoom = 2.0f;
        cameraController.zoomSpeed = 0.1f;
        cameraController.zoomSmoothness = 5f;
    }

    // ========== SQUARE GRID ==========
    private SquareGrid CreateSquareGrid()
    {
        GameObject gridGO = new GameObject("SquareGrid");
        SquareGrid grid = gridGO.AddComponent<SquareGrid>();
        grid.cellSprite = CreateSquareSprite();
        return grid;
    }

    private Sprite CreateSquareSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color fill = Color.white;
        Color outline = new Color(0.4f, 0.5f, 0.4f, 1f);

        Color[] pixels = new Color[size * size];
        int border = 2;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x < border || x >= size - border || y < border || y >= size - border)
                    pixels[y * size + x] = outline;
                else
                    pixels[y * size + x] = fill;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    // ========== CHARACTERS ==========
    private (CharacterController[] pcs, CharacterController npc, List<CharacterController> npcList) CreateCharacters()
    {
        CharacterController[] pcs = new CharacterController[4];
        string[] pcNames = { "PC_Hero1", "PC_Hero2", "PC_Hero3", "PC_Hero4" };

        for (int i = 0; i < 4; i++)
        {
            GameObject pcGO = new GameObject(pcNames[i]);
            pcGO.AddComponent<SpriteRenderer>();
            pcs[i] = pcGO.AddComponent<CharacterController>();
            pcs[i].IsPlayerControlled = true;
        }

        // Legacy NPC (first enemy)
        GameObject npcGO = new GameObject("NPC_Enemy_0");
        npcGO.AddComponent<SpriteRenderer>();
        CharacterController npc = npcGO.AddComponent<CharacterController>();
        npc.IsPlayerControlled = false;

        var npcList = new List<CharacterController>();
        npcList.Add(npc);

        string[] enemyNames = { "NPC_Enemy_1", "NPC_Enemy_2" };
        for (int i = 0; i < enemyNames.Length; i++)
        {
            GameObject go = new GameObject(enemyNames[i]);
            go.AddComponent<SpriteRenderer>();
            CharacterController cc = go.AddComponent<CharacterController>();
            cc.IsPlayerControlled = false;
            npcList.Add(cc);
        }

        Debug.Log($"[SceneBootstrap] Created 4 PC and {npcList.Count} NPC GameObjects for encounter.");
        return (pcs, npc, npcList);
    }

    // ========== LAYOUT CONSTANTS ==========
    private const float PartyPanelWidth = 220f;
    private const float BottomPanelHeight = 160f;
    private const float NPCPanelWidth = 220f;
    private const float IconSize = 32f;
    private const float PartyEntryHeight = 140f;
    private const float PartyEntrySpacing = 8f;

    // ========== UI ==========
    private CombatUI CreateUI()
    {
        // Create Canvas
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        // EventSystem
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        CombatUI combatUI = canvasGO.AddComponent<CombatUI>();

        // --- Turn Indicator (top center) ---
        combatUI.TurnIndicatorText = CreateText(canvasGO.transform, "TurnIndicator",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -10), new Vector2(800, 50),
            "", 24, Color.white, TextAnchor.MiddleCenter);

        AddBackground(combatUI.TurnIndicatorText.transform, new Color(0, 0, 0, 0.7f), new Vector2(820, 55));

        // --- Initiative Order Display (below turn indicator) ---
        CreateInitiativePanel(canvasGO.transform, combatUI);

        // --- Party Panel (left side, vertical) ---
        CreatePartyPanel(canvasGO.transform, combatUI);

        // --- NPC Panels (right side, stacked from top) ---
        CreateNPCPanelsRight(canvasGO.transform, combatUI, 3);

        // --- Combat Data Panel (bottom, full width) ---
        CreateCombatDataPanel(canvasGO.transform, combatUI);

        // --- Feat Controls (above bottom panel, right side) ---
        CreateFeatControls(canvasGO.transform, combatUI);

        // Legacy NPC panel fields (hidden, for backward compatibility)
        CreateLegacyNPCFields(canvasGO.transform, combatUI);

        return combatUI;
    }

    // ========== INITIATIVE PANEL (top center) ==========
    private void CreateInitiativePanel(Transform parent, CombatUI combatUI)
    {
        GameObject panel = CreatePanel(parent, "InitiativePanel",
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -68), new Vector2(900, 30),
            new Color(0, 0, 0, 0.6f));
        combatUI.InitiativePanel = panel;

        combatUI.InitiativeOrderText = CreateText(panel.transform, "InitiativeText",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 2), new Vector2(880, 26),
            "", 13, new Color(0.9f, 0.9f, 0.8f), TextAnchor.MiddleCenter);

        panel.SetActive(false);
    }

    // ========== PARTY PANEL (left side) ==========
    /// <summary>
    /// Creates the left-side party panel with vertical list of all PC entries.
    /// Each entry shows name, icon, HP bar, and active-turn indicator.
    /// Anchored: left edge, from 20% bottom to top (leaving space for bottom panel).
    /// </summary>
    private void CreatePartyPanel(Transform parent, CombatUI combatUI)
    {
        // Container panel — left edge, stretches from just above bottom panel to near top
        GameObject partyPanel = new GameObject("PartyPanel");
        partyPanel.transform.SetParent(parent, false);
        RectTransform ppRT = partyPanel.AddComponent<RectTransform>();
        // anchorMin.y = ratio of bottomPanelHeight to ref height (160/1080 ≈ 0.148)
        float bottomRatio = BottomPanelHeight / 1080f;
        ppRT.anchorMin = new Vector2(0, bottomRatio);
        ppRT.anchorMax = new Vector2(0, 1);
        ppRT.pivot = new Vector2(0, 1);
        ppRT.anchoredPosition = new Vector2(8, -100); // 8px from left, 100px from top (below initiative)
        ppRT.sizeDelta = new Vector2(PartyPanelWidth, 0); // width fixed, height stretches

        Image ppBg = partyPanel.AddComponent<Image>();
        ppBg.color = new Color(0.08f, 0.08f, 0.14f, 0.88f);

        // Add vertical layout group
        VerticalLayoutGroup vlg = partyPanel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = PartyEntrySpacing;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Create 4 PC entries inside the party panel
        CreatePartyEntry(partyPanel.transform, combatUI, 1,
            new Color(0.1f, 0.2f, 0.4f, 0.9f), new Color(0.3f, 0.9f, 0.3f), new Color(0f, 1f, 0.3f));
        CreatePartyEntry(partyPanel.transform, combatUI, 2,
            new Color(0.1f, 0.15f, 0.35f, 0.9f), new Color(0.5f, 0.7f, 1f), new Color(0.3f, 0.5f, 1f));
        CreatePartyEntry(partyPanel.transform, combatUI, 3,
            new Color(0.1f, 0.25f, 0.25f, 0.9f), new Color(0.4f, 0.9f, 0.7f), new Color(0.2f, 0.8f, 0.6f));
        CreatePartyEntry(partyPanel.transform, combatUI, 4,
            new Color(0.3f, 0.1f, 0.1f, 0.9f), new Color(1f, 0.6f, 0.4f), new Color(0.9f, 0.3f, 0.2f));

        combatUI.PartyPanelGO = partyPanel;
    }

    /// <summary>
    /// Creates a single party member entry inside the party panel.
    /// Layout (top to bottom, within 140px height, using bottom-left origin):
    ///   [136-140] Active indicator bar (top edge)
    ///   [104-134] Icon (32x30, left side)
    ///   [118-132] Name text (right of icon)
    ///   [78-102]  Ability scores (two lines: STR/DEX/CON + WIS/INT/CHA)
    ///   [60-74]   AC line
    ///   [44-58]   Atk line
    ///   [28-40]   HP text label
    ///   [4-24]    HP bar (BOTTOM, 20px tall)
    /// </summary>
    private void CreatePartyEntry(Transform parent, CombatUI combatUI, int pcIndex,
        Color panelColor, Color nameColor, Color indicatorColor)
    {
        float entryW = PartyPanelWidth - 16; // account for padding
        float H = PartyEntryHeight; // 140

        GameObject entry = new GameObject($"PartyEntry_PC{pcIndex}");
        entry.transform.SetParent(parent, false);
        RectTransform entryRT = entry.AddComponent<RectTransform>();
        entryRT.sizeDelta = new Vector2(entryW, H);

        Image entryBg = entry.AddComponent<Image>();
        entryBg.color = panelColor;

        // ── Active indicator bar at top (y=136, h=4) ──
        Image indicator = CreateActiveIndicator(entry.transform, $"PC{pcIndex}Active",
            new Vector2(0, H - 4), new Vector2(entryW, 4), indicatorColor);

        // ── Icon (left side, y=104, h=30, 32x30) ──
        Image icon = CreateIconImage(entry.transform, $"PC{pcIndex}Icon",
            new Vector2(4, 104), new Vector2(IconSize, 30));

        // ── Name (right of icon, y=118, h=14) ──
        Text nameText = CreateText(entry.transform, $"PC{pcIndex}Name",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(IconSize + 10, 118), new Vector2(entryW - IconSize - 16, 14),
            $"Hero {pcIndex}", 12, nameColor, TextAnchor.MiddleLeft);

        // ── Ability scores: 2 lines (y=78, h=24) ──
        // Line 1: STR DEX CON | Line 2: WIS INT CHA
        Text abilityText = CreateText(entry.transform, $"PC{pcIndex}Abilities",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(4, 78), new Vector2(entryW - 8, 24),
            "STR -- DEX -- CON --\nWIS -- INT -- CHA --", 9,
            new Color(0.8f, 0.8f, 0.6f), TextAnchor.UpperLeft);

        // ── AC line (y=60, h=14) ──
        Text acText = CreateText(entry.transform, $"PC{pcIndex}AC",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(4, 60), new Vector2(entryW - 8, 14),
            "AC: --", 10, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleLeft);

        // ── Atk line (y=44, h=14) ──
        Text atkText = CreateText(entry.transform, $"PC{pcIndex}Atk",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(4, 44), new Vector2(entryW - 8, 14),
            "Atk: --", 10, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleLeft);

        // ── HP text (y=28, h=12) ── BOTTOM AREA
        Text hpText = CreateText(entry.transform, $"PC{pcIndex}HP",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(4, 28), new Vector2(entryW - 8, 12),
            "HP: --/--", 11, Color.white, TextAnchor.MiddleLeft);

        // ── HP bar at BOTTOM (y=4, h=20) ──
        Image hpBar = CreateHPBar(entry.transform, $"PC{pcIndex}HPBar",
            new Vector2(4, 4), new Vector2(entryW - 8, 20), nameColor);

        // ── Buff display text (overlaid on HP bar, right-aligned, small) ──
        Text buffText = CreateText(entry.transform, $"PC{pcIndex}Buffs",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(4, 4), new Vector2(entryW - 8, 20),
            "", 7, new Color(0.5f, 1f, 0.5f, 0.9f), TextAnchor.MiddleRight);
        buffText.supportRichText = true;
        buffText.gameObject.SetActive(false); // Hidden until buffs are active

        // Speed text (hidden, kept for data)
        Text speedText = CreateText(entry.transform, $"PC{pcIndex}Speed",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(4, -200), new Vector2(entryW - 8, 12),
            "Speed: -- sq", 8, Color.white, TextAnchor.MiddleLeft);
        speedText.gameObject.SetActive(false);

        // Assign to CombatUI fields
        switch (pcIndex)
        {
            case 1:
                combatUI.PC1Panel = entry; combatUI.PC1ActiveIndicator = indicator;
                combatUI.PC1NameText = nameText; combatUI.PC1AbilityText = abilityText;
                combatUI.PC1HPText = hpText; combatUI.PC1HPBar = hpBar;
                combatUI.PC1ACText = acText; combatUI.PC1AtkText = atkText;
                combatUI.PC1SpeedText = speedText; combatUI.PC1Icon = icon;
                combatUI.PC1BuffText = buffText;
                break;
            case 2:
                combatUI.PC2Panel = entry; combatUI.PC2ActiveIndicator = indicator;
                combatUI.PC2NameText = nameText; combatUI.PC2AbilityText = abilityText;
                combatUI.PC2HPText = hpText; combatUI.PC2HPBar = hpBar;
                combatUI.PC2ACText = acText; combatUI.PC2AtkText = atkText;
                combatUI.PC2SpeedText = speedText; combatUI.PC2Icon = icon;
                combatUI.PC2BuffText = buffText;
                break;
            case 3:
                combatUI.PC3Panel = entry; combatUI.PC3ActiveIndicator = indicator;
                combatUI.PC3NameText = nameText; combatUI.PC3AbilityText = abilityText;
                combatUI.PC3HPText = hpText; combatUI.PC3HPBar = hpBar;
                combatUI.PC3ACText = acText; combatUI.PC3AtkText = atkText;
                combatUI.PC3SpeedText = speedText; combatUI.PC3Icon = icon;
                combatUI.PC3BuffText = buffText;
                break;
            case 4:
                combatUI.PC4Panel = entry; combatUI.PC4ActiveIndicator = indicator;
                combatUI.PC4NameText = nameText; combatUI.PC4AbilityText = abilityText;
                combatUI.PC4HPText = hpText; combatUI.PC4HPBar = hpBar;
                combatUI.PC4ACText = acText; combatUI.PC4AtkText = atkText;
                combatUI.PC4SpeedText = speedText; combatUI.PC4Icon = icon;
                combatUI.PC4BuffText = buffText;
                break;
        }
    }

    // ========== NPC PANELS (right side, stacked from top) ==========
    /// <summary>
    /// Create NPC stat panels stacked vertically on the right side, from the top down.
    /// Matches the same layout structure as PC party entries (140px height).
    /// Layout (top to bottom, within 140px height, using bottom-left origin):
    ///   [136-140] Active indicator bar (top edge, red)
    ///   [104-134] Icon (32x30, left side)
    ///   [118-132] Name text (right of icon)
    ///   [78-102]  Ability scores (two lines: STR/DEX/CON + WIS/INT/CHA)
    ///   [60-74]   AC line
    ///   [44-58]   Atk line
    ///   [28-40]   HP text label
    ///   [4-24]    HP bar (BOTTOM, 20px tall)
    /// </summary>
    private void CreateNPCPanelsRight(Transform parent, CombatUI combatUI, int count)
    {
        float H = PartyEntryHeight; // 140, same as PC panels
        float spacing = PartyEntrySpacing; // 8, same as PC panels
        float entryW = NPCPanelWidth - 16; // account for padding, same approach as PC

        for (int i = 0; i < count; i++)
        {
            // Stack from top: first panel starts at y = -100 (below turn indicator + initiative)
            float yOffset = -(100f + i * (H + spacing));

            NPCPanelUI panelUI = new NPCPanelUI();

            Color panelColor = new Color(0.3f, 0.1f, 0.1f, 0.85f);

            GameObject panel = new GameObject($"NPCPanel_{i}");
            panel.transform.SetParent(parent, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(1, 1);
            panelRT.anchorMax = new Vector2(1, 1);
            panelRT.pivot = new Vector2(1, 1);
            panelRT.anchoredPosition = new Vector2(-8, yOffset);
            panelRT.sizeDelta = new Vector2(NPCPanelWidth, H);

            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = panelColor;
            panelUI.Panel = panel;

            // ── Active indicator bar at top (y=136, h=4) ──
            // NPC panels don't track active indicator individually but we create a red bar for visual consistency
            Image npcIndicator = CreateActiveIndicator(panel.transform, $"NPC{i}Active",
                new Vector2(0, H - 4), new Vector2(NPCPanelWidth, 4), new Color(1f, 0.3f, 0.3f));
            panelUI.ActiveIndicator = npcIndicator;

            // ── Icon (left side, y=104, h=30, 32x30) ── same as PC
            panelUI.IconImage = CreateIconImage(panel.transform, $"NPCIcon_{i}",
                new Vector2(4, 104), new Vector2(IconSize, 30));

            // ── Name (right of icon, y=118, h=14) ── same as PC
            panelUI.NameText = CreateText(panel.transform, $"NPCName_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(IconSize + 10, 118), new Vector2(entryW - IconSize - 16, 14),
                $"Enemy {i + 1}", 12, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleLeft);

            // ── Ability scores: 2 lines (y=78, h=24) ── same as PC
            // Line 1: STR DEX CON | Line 2: WIS INT CHA
            panelUI.AbilityText = CreateText(panel.transform, $"NPCAbilities_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(4, 78), new Vector2(entryW - 8, 24),
                "STR -- DEX -- CON --\nWIS -- INT -- CHA --", 9,
                new Color(0.8f, 0.8f, 0.6f), TextAnchor.UpperLeft);

            // ── AC line (y=60, h=14) ── same as PC
            panelUI.ACText = CreateText(panel.transform, $"NPCAC_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(4, 60), new Vector2(entryW - 8, 14),
                "AC: --", 10, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleLeft);

            // ── Atk line (y=44, h=14) ── same as PC
            panelUI.AtkText = CreateText(panel.transform, $"NPCAtk_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(4, 44), new Vector2(entryW - 8, 14),
                "Atk: --", 10, new Color(1f, 0.9f, 0.7f), TextAnchor.MiddleLeft);

            // ── HP text (y=28, h=12) ── BOTTOM AREA, same as PC
            panelUI.HPText = CreateText(panel.transform, $"NPCHP_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(4, 28), new Vector2(entryW - 8, 12),
                "HP: --/--", 11, Color.white, TextAnchor.MiddleLeft);

            // ── HP bar at BOTTOM (y=4, h=20) ── same as PC
            panelUI.HPBar = CreateHPBar(panel.transform, $"NPCHPBar_{i}",
                new Vector2(4, 4), new Vector2(entryW - 8, 20), new Color(0.8f, 0.2f, 0.2f));

            // Speed text (hidden, kept for data) ── same as PC
            panelUI.SpeedText = CreateText(panel.transform, $"NPCSpeed_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(4, -200), new Vector2(entryW - 8, 12),
                "Speed: -- sq", 8, Color.white, TextAnchor.MiddleLeft);
            panelUI.SpeedText.gameObject.SetActive(false);

            combatUI.NPCPanels.Add(panelUI);
        }

        Debug.Log($"[SceneBootstrap] Created {count} NPC stat panels (right side, top-down, 140px layout matching PC panels).");
    }

    /// <summary>Create hidden legacy NPC text fields for backward compatibility.</summary>
    private void CreateLegacyNPCFields(Transform parent, CombatUI combatUI)
    {
        // Hidden container for legacy single-NPC fields
        GameObject legacy = new GameObject("LegacyNPCFields");
        legacy.transform.SetParent(parent, false);
        legacy.SetActive(false);

        combatUI.NPCNameText = CreateText(legacy.transform, "NPCName",
            Vector2.zero, Vector2.zero, Vector2.zero,
            Vector2.zero, new Vector2(100, 20),
            "Enemy", 12, Color.red, TextAnchor.MiddleLeft);
        combatUI.NPCHPText = CreateText(legacy.transform, "NPCHP",
            Vector2.zero, Vector2.zero, Vector2.zero,
            Vector2.zero, new Vector2(100, 20),
            "HP: --/--", 12, Color.white, TextAnchor.MiddleLeft);
        combatUI.NPCACText = CreateText(legacy.transform, "NPCAC",
            Vector2.zero, Vector2.zero, Vector2.zero,
            Vector2.zero, new Vector2(100, 20),
            "AC: --", 12, Color.white, TextAnchor.MiddleLeft);
        combatUI.NPCAtkText = CreateText(legacy.transform, "NPCAtk",
            Vector2.zero, Vector2.zero, Vector2.zero,
            Vector2.zero, new Vector2(100, 20),
            "Atk: --", 12, Color.white, TextAnchor.MiddleLeft);
        combatUI.NPCSpeedText = CreateText(legacy.transform, "NPCSpeed",
            Vector2.zero, Vector2.zero, Vector2.zero,
            Vector2.zero, new Vector2(100, 20),
            "Speed: --", 12, Color.white, TextAnchor.MiddleLeft);
        combatUI.NPCAbilityText = CreateText(legacy.transform, "NPCAbility",
            Vector2.zero, Vector2.zero, Vector2.zero,
            Vector2.zero, new Vector2(100, 20),
            "", 12, Color.white, TextAnchor.MiddleLeft);

        // Create a dummy HP bar for legacy
        combatUI.NPCHPBar = CreateHPBar(legacy.transform, "NPCHPBar",
            Vector2.zero, new Vector2(100, 8), Color.red);
    }

    // ========== COMBAT DATA PANEL (bottom) ==========
    /// <summary>
    /// Creates the bottom combat data panel containing:
    /// - Combat log (left ~55%)
    /// - Action buttons (right ~45%)
    /// Anchored to bottom of screen, full width.
    /// </summary>
    private void CreateCombatDataPanel(Transform parent, CombatUI combatUI)
    {
        // Keep a subtle bottom background strip for readability, but make the two sections
        // independent floating windows so they can be dragged and resized.
        GameObject bottomPanel = new GameObject("CombatDataPanelBackdrop");
        bottomPanel.transform.SetParent(parent, false);
        RectTransform bpRT = bottomPanel.AddComponent<RectTransform>();
        bpRT.anchorMin = new Vector2(0, 0);
        bpRT.anchorMax = new Vector2(1, 0);
        bpRT.pivot = new Vector2(0.5f, 0);
        bpRT.anchoredPosition = Vector2.zero;
        bpRT.sizeDelta = new Vector2(0, BottomPanelHeight + 20f);

        Image bpBg = bottomPanel.AddComponent<Image>();
        bpBg.color = new Color(0.06f, 0.06f, 0.1f, 0.45f);

        combatUI.CombatDataPanelGO = bottomPanel;

        // Floating windows are parented directly to canvas root.
        CreateCombatLogSection(parent, combatUI);
        CreateActionButtonsSection(parent, combatUI);
    }

    /// <summary>Floating combat log window with draggable title bar + resizable edges/corners.</summary>
    private void CreateCombatLogSection(Transform parent, CombatUI combatUI)
    {
        GameObject logSection = new GameObject("CombatLogSection");
        logSection.transform.SetParent(parent, false);
        RectTransform lsRT = logSection.AddComponent<RectTransform>();
        Vector2 defaultLogPosition = new Vector2(-360f, -420f);
        Vector2 defaultLogSize = new Vector2(900f, 220f);
        lsRT.anchorMin = new Vector2(0.5f, 0.5f);
        lsRT.anchorMax = new Vector2(0.5f, 0.5f);
        lsRT.pivot = new Vector2(0.5f, 0.5f);
        lsRT.anchoredPosition = defaultLogPosition;
        lsRT.sizeDelta = defaultLogSize;

        Image lsBg = logSection.AddComponent<Image>();
        lsBg.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);

        // Title bar (drag handle)
        CreateWindowTitleBar(
            logSection.transform,
            "CombatLogTitleBar",
            "── COMBAT LOG ──",
            new Color(0.16f, 0.16f, 0.28f, 0.95f),
            "ui_window_combat_log",
            lsRT);

        // --- Scroll Area (holds ScrollRect) ---
        GameObject scrollArea = new GameObject("CombatLogScrollArea");
        scrollArea.transform.SetParent(logSection.transform, false);
        RectTransform scrollAreaRT = scrollArea.AddComponent<RectTransform>();
        scrollAreaRT.anchorMin = new Vector2(0, 0);
        scrollAreaRT.anchorMax = new Vector2(1, 1);
        scrollAreaRT.offsetMin = new Vector2(0, 0);
        scrollAreaRT.offsetMax = new Vector2(0, -24); // Leave room for title bar

        ScrollRect scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;

        // --- Viewport (masks content) ---
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        RectTransform viewportRT = viewport.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(4, 2);
        viewportRT.offsetMax = new Vector2(-4, -2);

        // Mask requires an Image - use white but mask hides it
        Image viewportImg = viewport.AddComponent<Image>();
        viewportImg.color = Color.white;
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        // --- Content (grows vertically with messages) ---
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 2;
        vlg.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // --- Link ScrollRect ---
        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;

        // --- Scrollbar via helper ---
        ScrollbarHelper.CreateVerticalScrollbar(scrollRect, scrollArea.transform, 14f);

        // --- Draggable + resizable behavior ---
        ResizableWindow logResizable = logSection.AddComponent<ResizableWindow>();
        logResizable.WindowRect = lsRT;
        logResizable.MinSize = new Vector2(360f, 140f);
        logResizable.MaxSize = new Vector2(1400f, 720f);
        logResizable.PersistenceKey = "ui_window_combat_log";
        logResizable.SavePositionToPlayerPrefs = true;
        logResizable.SaveSizeToPlayerPrefs = true;

        combatUI.CombatLogWindowRect = lsRT;
        combatUI.CombatLogResizable = logResizable;
        combatUI.DefaultCombatLogWindowPosition = defaultLogPosition;
        combatUI.DefaultCombatLogWindowSize = defaultLogSize;

        // --- Store references in CombatUI ---
        combatUI.CombatLogContent = content;
        combatUI.CombatLogScrollRect = scrollRect;

        // Add initial welcome message
        combatUI.ShowCombatLog("Combat begins! Choose your actions.");
    }

    /// <summary>Floating action button window with draggable title bar + resizable edges/corners.</summary>
    private void CreateActionButtonsSection(Transform parent, CombatUI combatUI)
    {
        GameObject actionSection = new GameObject("ActionPanel");
        actionSection.transform.SetParent(parent, false);
        RectTransform asRT = actionSection.AddComponent<RectTransform>();
        Vector2 defaultActionPosition = new Vector2(450f, -410f);
        Vector2 defaultActionSize = new Vector2(760f, 240f);
        asRT.anchorMin = new Vector2(0.5f, 0.5f);
        asRT.anchorMax = new Vector2(0.5f, 0.5f);
        asRT.pivot = new Vector2(0.5f, 0.5f);
        asRT.anchoredPosition = defaultActionPosition;
        asRT.sizeDelta = defaultActionSize;

        Image asBg = actionSection.AddComponent<Image>();
        asBg.color = new Color(0.12f, 0.12f, 0.2f, 0.9f);

        combatUI.ActionPanel = actionSection;

        // Title bar (drag handle)
        CreateWindowTitleBar(
            actionSection.transform,
            "ActionPanelTitleBar",
            "── ACTIONS ──",
            new Color(0.2f, 0.2f, 0.32f, 0.95f),
            "ui_window_action_panel",
            asRT);

        // Reset floating windows button.
        GameObject resetBtnGO = new GameObject("ResetUILayoutButton");
        resetBtnGO.transform.SetParent(actionSection.transform, false);
        RectTransform resetBtnRT = resetBtnGO.AddComponent<RectTransform>();
        resetBtnRT.anchorMin = new Vector2(1f, 1f);
        resetBtnRT.anchorMax = new Vector2(1f, 1f);
        resetBtnRT.pivot = new Vector2(1f, 1f);
        resetBtnRT.anchoredPosition = new Vector2(-6f, -2f);
        resetBtnRT.sizeDelta = new Vector2(86f, 18f);

        Image resetBtnImage = resetBtnGO.AddComponent<Image>();
        Color resetColor = new Color(0.36f, 0.2f, 0.2f, 0.95f);
        resetBtnImage.color = resetColor;

        Button resetButton = resetBtnGO.AddComponent<Button>();
        ColorBlock resetColors = resetButton.colors;
        resetColors.normalColor = resetColor;
        resetColors.highlightedColor = resetColor * 1.15f;
        resetColors.pressedColor = resetColor * 0.85f;
        resetButton.colors = resetColors;
        combatUI.ResetUILayoutButton = resetButton;

        Text resetLabel = CreateText(resetBtnGO.transform, "ResetUILayoutLabel",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            "Reset UI", 10, new Color(1f, 0.9f, 0.9f), TextAnchor.MiddleCenter);
        resetLabel.raycastTarget = false;

        // Action Status text (top of action section)
        combatUI.ActionStatusText = CreateText(actionSection.transform, "ActionStatus",
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -24), new Vector2(0, 18),
            "[Move] [Standard]", 10, new Color(0.7f, 0.9f, 0.7f), TextAnchor.MiddleCenter);
        RectTransform statusRT = combatUI.ActionStatusText.GetComponent<RectTransform>();
        statusRT.anchorMin = new Vector2(0, 1);
        statusRT.anchorMax = new Vector2(1, 1);
        statusRT.offsetMin = new Vector2(4, -42);
        statusRT.offsetMax = new Vector2(-4, -24);

        // Button grid container
        GameObject btnGrid = new GameObject("ButtonGrid");
        btnGrid.transform.SetParent(actionSection.transform, false);
        RectTransform gridRT = btnGrid.AddComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0, 0);
        gridRT.anchorMax = new Vector2(1, 1);
        gridRT.offsetMin = new Vector2(6, 30);
        gridRT.offsetMax = new Vector2(-6, -48);

        GridLayoutGroup grid = btnGrid.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(170, 26);
        grid.spacing = new Vector2(6, 3);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;

        // Create action buttons inside the grid
        combatUI.MoveButton = CreateGridButton(btnGrid.transform, "MoveBtn",
            "Move", new Color(0.2f, 0.5f, 0.2f));
        combatUI.FiveFootStepButton = CreateGridButton(btnGrid.transform, "FiveFootStepBtn",
            "5-Foot Step", new Color(0.2f, 0.45f, 0.72f));
        combatUI.DropProneButton = CreateGridButton(btnGrid.transform, "DropProneBtn",
            "Drop Prone", new Color(0.52f, 0.46f, 0.12f));
        combatUI.StandUpButton = CreateGridButton(btnGrid.transform, "StandUpBtn",
            "Stand Up", new Color(0.48f, 0.33f, 0.12f));
        combatUI.CrawlButton = CreateGridButton(btnGrid.transform, "CrawlBtn",
            "Crawl", new Color(0.22f, 0.38f, 0.65f));
        combatUI.AttackButton = CreateGridButton(btnGrid.transform, "AttackBtn",
            "Attack", new Color(0.7f, 0.2f, 0.2f));
        combatUI.AttackThrownButton = CreateGridButton(btnGrid.transform, "AttackThrownBtn",
            "Attack (Thrown)", new Color(0.65f, 0.22f, 0.22f));
        combatUI.AttackOffHandButton = CreateGridButton(btnGrid.transform, "AttackOffHandBtn",
            "Attack (Off-Hand)", new Color(0.68f, 0.24f, 0.32f));
        combatUI.AttackOffHandThrownButton = CreateGridButton(btnGrid.transform, "AttackOffHandThrownBtn",
            "Attack (Off-Hand Thrown)", new Color(0.72f, 0.28f, 0.34f));
        combatUI.AttackDefensivelyButton = CreateGridButton(btnGrid.transform, "AttackDefensivelyBtn",
            "Fighting Defensively (Std)", new Color(0.22f, 0.38f, 0.62f));
        combatUI.SpecialAttackButton = CreateGridButton(btnGrid.transform, "SpecialAttackBtn",
            "Special Attack", new Color(0.55f, 0.35f, 0.15f));
        combatUI.TurnUndeadButton = CreateGridButton(btnGrid.transform, "TurnUndeadBtn",
            "Turn Undead", new Color(0.72f, 0.68f, 0.2f));
        combatUI.GrappleActionsButton = CreateGridButton(btnGrid.transform, "GrappleActionsBtn",
            "Grapple Actions (Legacy)", new Color(0.45f, 0.24f, 0.6f));
        combatUI.GrappleDamageButton = CreateGridButton(btnGrid.transform, "GrappleDamageBtn",
            "Grapple: Damage", new Color(0.55f, 0.22f, 0.45f));
        combatUI.GrappleLightWeaponAttackButton = CreateGridButton(btnGrid.transform, "GrappleLightWeaponAttackBtn",
            "Grapple: Attack Light Weapon", new Color(0.6f, 0.24f, 0.42f));
        combatUI.GrappleUnarmedAttackButton = CreateGridButton(btnGrid.transform, "GrappleUnarmedAttackBtn",
            "Grapple: Attack Unarmed", new Color(0.62f, 0.2f, 0.36f));
        combatUI.GrapplePinButton = CreateGridButton(btnGrid.transform, "GrapplePinBtn",
            "Grapple: Pin Opponent", new Color(0.5f, 0.24f, 0.56f));
        combatUI.GrappleBreakPinButton = CreateGridButton(btnGrid.transform, "GrappleBreakPinBtn",
            "Grapple: Break Pin", new Color(0.44f, 0.3f, 0.58f));
        combatUI.GrappleEscapeArtistButton = CreateGridButton(btnGrid.transform, "GrappleEscapeArtistBtn",
            "Grapple: Escape Artist", new Color(0.35f, 0.34f, 0.62f));
        combatUI.GrappleEscapeCheckButton = CreateGridButton(btnGrid.transform, "GrappleEscapeCheckBtn",
            "Grapple: Escape Check", new Color(0.3f, 0.3f, 0.65f));
        combatUI.GrappleMoveButton = CreateGridButton(btnGrid.transform, "GrappleMoveBtn",
            "Grapple: Move", new Color(0.28f, 0.42f, 0.6f));
        combatUI.GrappleUseOpponentWeaponButton = CreateGridButton(btnGrid.transform, "GrappleUseWeaponBtn",
            "Grapple: Use Opp Weapon", new Color(0.58f, 0.3f, 0.32f));
        combatUI.GrappleDisarmSmallObjectButton = CreateGridButton(btnGrid.transform, "GrappleDisarmSmallObjectBtn",
            "Grapple: Disarm Small Object", new Color(0.52f, 0.28f, 0.2f));
        combatUI.GrappleReleasePinnedButton = CreateGridButton(btnGrid.transform, "GrappleReleasePinBtn",
            "Grapple: Release Pin", new Color(0.4f, 0.44f, 0.2f));
        combatUI.AidAnotherButton = CreateGridButton(btnGrid.transform, "AidAnotherBtn",
            "Aid Another", new Color(0.42f, 0.3f, 0.58f));
        combatUI.OverrunButton = CreateGridButton(btnGrid.transform, "OverrunBtn",
            "Overrun", new Color(0.58f, 0.34f, 0.2f));
        combatUI.ChargeButton = CreateGridButton(btnGrid.transform, "ChargeBtn",
            "Charge", new Color(0.78f, 0.45f, 0.15f));
        // Full Attack buttons are intentionally omitted from the main actions window
        // while multi-attack flow is being reworked.
        combatUI.DualWieldButton = CreateGridButton(btnGrid.transform, "DualWieldBtn",
            "Dual Wield", new Color(0.5f, 0.25f, 0.5f));
        combatUI.FlurryOfBlowsButton = CreateGridButton(btnGrid.transform, "FlurryBtn",
            "Flurry of Blows", new Color(0.2f, 0.5f, 0.5f));
        combatUI.RageButton = CreateGridButton(btnGrid.transform, "RageBtn",
            "Rage", new Color(0.6f, 0.2f, 0.1f));
        combatUI.CastSpellButton = CreateGridButton(btnGrid.transform, "CastSpellBtn",
            "Cast Spell", new Color(0.4f, 0.2f, 0.6f));
        combatUI.ReloadButton = CreateGridButton(btnGrid.transform, "ReloadBtn",
            "Reload", new Color(0.38f, 0.44f, 0.16f));
        combatUI.DropEquippedItemButton = CreateGridButton(btnGrid.transform, "DropEquippedBtn",
            "Drop Equipped", new Color(0.45f, 0.18f, 0.18f));
        combatUI.PickUpItemButton = CreateGridButton(btnGrid.transform, "PickUpItemBtn",
            "Pick Up Item", new Color(0.2f, 0.45f, 0.28f));
        combatUI.DamageModeToggleButton = CreateGridButton(btnGrid.transform, "DamageModeToggleBtn",
            "Damage: Lethal", new Color(0.65f, 0.2f, 0.2f));
        combatUI.EndTurnButton = CreateGridButton(btnGrid.transform, "EndTurnBtn",
            "End Turn", new Color(0.3f, 0.3f, 0.55f));

        // Rage Status Text (below buttons area - create outside grid)
        combatUI.RageStatusText = CreateText(actionSection.transform, "RageStatus",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 2), new Vector2(0, 16),
            "", 9, new Color(1f, 0.6f, 0.3f), TextAnchor.MiddleCenter);
        RectTransform rageRT = combatUI.RageStatusText.GetComponent<RectTransform>();
        rageRT.anchorMin = new Vector2(0, 0);
        rageRT.anchorMax = new Vector2(1, 0);
        rageRT.offsetMin = new Vector2(4, 2);
        rageRT.offsetMax = new Vector2(-4, 16);
        combatUI.RageStatusText.gameObject.SetActive(false);

        // Spell Slots Text (next to rage status)
        combatUI.SpellSlotsText = CreateText(actionSection.transform, "SpellSlots",
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 16), new Vector2(0, 14),
            "", 8, new Color(0.8f, 0.7f, 1f), TextAnchor.MiddleCenter);
        RectTransform spellRT = combatUI.SpellSlotsText.GetComponent<RectTransform>();
        spellRT.anchorMin = new Vector2(0, 0);
        spellRT.anchorMax = new Vector2(1, 0);
        spellRT.offsetMin = new Vector2(4, 16);
        spellRT.offsetMax = new Vector2(-4, 30);
        combatUI.SpellSlotsText.gameObject.SetActive(false);

        // Draggable + resizable behavior with PlayerPrefs persistence.
        ResizableWindow actionResizable = actionSection.AddComponent<ResizableWindow>();
        actionResizable.WindowRect = asRT;
        actionResizable.MinSize = new Vector2(420f, 180f);
        actionResizable.MaxSize = new Vector2(1500f, 760f);
        actionResizable.PersistenceKey = "ui_window_action_panel";
        actionResizable.SavePositionToPlayerPrefs = true;
        actionResizable.SaveSizeToPlayerPrefs = true;

        combatUI.ActionWindowRect = asRT;
        combatUI.ActionWindowResizable = actionResizable;
        combatUI.DefaultActionWindowPosition = defaultActionPosition;
        combatUI.DefaultActionWindowSize = defaultActionSize;

        // Reflow button grid whenever the action panel is resized.
        actionResizable.OnResized += _ => ReflowActionButtonGrid(grid, gridRT);
        ReflowActionButtonGrid(grid, gridRT);

        actionSection.SetActive(false);
    }

    private void CreateWindowTitleBar(Transform windowParent, string objectName, string label, Color barColor, string persistenceKey, RectTransform windowRect)
    {
        GameObject titleBar = new GameObject(objectName);
        titleBar.transform.SetParent(windowParent, false);

        RectTransform titleRT = titleBar.AddComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = Vector2.zero;
        titleRT.sizeDelta = new Vector2(0f, 22f);

        Image titleBg = titleBar.AddComponent<Image>();
        titleBg.color = barColor;

        Text titleText = CreateText(titleBar.transform, objectName + "Label",
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            label, 10, new Color(0.8f, 0.85f, 1f), TextAnchor.MiddleCenter);
        titleText.raycastTarget = false;

        DraggableWindow draggable = titleBar.AddComponent<DraggableWindow>();
        draggable.WindowRect = windowRect;
        draggable.ClampToCanvasBounds = true;
        draggable.PersistenceKey = persistenceKey;
        draggable.SavePositionToPlayerPrefs = true;
    }

    private void ReflowActionButtonGrid(GridLayoutGroup grid, RectTransform gridRect)
    {
        if (grid == null || gridRect == null)
            return;

        float width = Mathf.Max(300f, gridRect.rect.width);
        float height = Mathf.Max(120f, gridRect.rect.height);

        int columns;
        if (width >= 940f) columns = 4;
        else if (width >= 700f) columns = 3;
        else if (width >= 500f) columns = 2;
        else columns = 1;

        int buttonCount = gridRect.childCount;
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)buttonCount / columns));

        float spacingX = 6f;
        float spacingY = 3f;

        float availableWidth = width - ((columns - 1) * spacingX);
        float availableHeight = height - ((rows - 1) * spacingY);

        float cellWidth = Mathf.Clamp((availableWidth / columns), 125f, 240f);
        float cellHeight = Mathf.Clamp((availableHeight / rows), 24f, 34f);

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.spacing = new Vector2(spacingX, spacingY);
        grid.cellSize = new Vector2(cellWidth, cellHeight);
    }

    // ========== FEAT CONTROLS (above bottom panel, right side) ==========
    private void CreateFeatControls(Transform canvasTransform, CombatUI combatUI)
    {
        float featPanelW = 240f;
        float featPanelH = 50f;

        // Power Attack panel - above bottom panel, right side
        GameObject paPanel = CreatePanel(canvasTransform, "PowerAttackPanel",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-8, BottomPanelHeight + 8), new Vector2(featPanelW, featPanelH),
            new Color(0.3f, 0.15f, 0.1f, 0.9f));
        combatUI.PowerAttackPanel = paPanel;

        combatUI.PowerAttackLabel = CreateText(paPanel.transform, "PALabel",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, featPanelH - 22), new Vector2(featPanelW - 20, 20),
            "Power Attack: OFF", 12, new Color(1f, 0.8f, 0.5f), TextAnchor.MiddleLeft);

        combatUI.PowerAttackSlider = CreateSlider(paPanel.transform, "PASlider",
            new Vector2(10, 5), new Vector2(featPanelW - 20, 20),
            0, 3, 0);

        paPanel.SetActive(false);

        // Rapid Shot panel - above Power Attack
        GameObject rsPanel = CreatePanel(canvasTransform, "RapidShotPanel",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-8, BottomPanelHeight + featPanelH + 12), new Vector2(featPanelW, 36f),
            new Color(0.1f, 0.2f, 0.3f, 0.9f));
        combatUI.RapidShotPanel = rsPanel;

        combatUI.RapidShotToggle = CreateGridButton(rsPanel.transform, "RSToggle",
            "Rapid Shot: OFF", new Color(0.5f, 0.5f, 0.5f));
        // Override the grid button positioning to fill the panel
        RectTransform rsToggleRT = combatUI.RapidShotToggle.GetComponent<RectTransform>();
        rsToggleRT.anchorMin = Vector2.zero;
        rsToggleRT.anchorMax = Vector2.one;
        rsToggleRT.offsetMin = new Vector2(4, 3);
        rsToggleRT.offsetMax = new Vector2(-4, -3);
        rsToggleRT.sizeDelta = Vector2.zero;
        combatUI.RapidShotLabel = combatUI.RapidShotToggle.GetComponentInChildren<Text>();

        rsPanel.SetActive(false);
    }

    // ========== GAME MANAGER ==========
    private void SetupGameManager(SquareGrid grid, CharacterController[] pcs,
        CharacterController npc, CombatUI combatUI,
        List<CharacterController> npcList = null)
    {
        GameManager gm = gameObject.AddComponent<GameManager>();
        gm.Grid = grid;
        gm.PC1 = pcs[0];
        gm.PC2 = pcs[1];
        gm.PC3 = pcs[2];
        gm.PC4 = pcs[3];
        gm.NPC = npc;
        gm.CombatUI = combatUI;

        gm.PCs = new List<CharacterController>(pcs);

        if (npcList != null)
            gm.NPCs = npcList;
        else
            gm.NPCs = new List<CharacterController> { npc };

        Canvas canvas = combatUI.GetComponent<Canvas>();
        if (canvas != null)
        {
            InventoryUI invUI = canvas.gameObject.AddComponent<InventoryUI>();
            invUI.BuildUI(canvas);
            gm.InventoryUI = invUI;

            CharacterSheetUI csUI = canvas.gameObject.AddComponent<CharacterSheetUI>();
            csUI.BuildUI(canvas);
            gm.CharacterSheetUI = csUI;

            // Embed the InventoryUI into the CharacterSheetUI's right panel
            csUI.IntegrateInventoryUI(invUI);
        }

        if (canvas != null)
        {
            RaceDatabase.Init();
            ItemDatabase.Init();
            FeatDefinitions.Init();

            CharacterCreationUI ccUI = canvas.gameObject.AddComponent<CharacterCreationUI>();
            ccUI.BuildUI(canvas);
            gm.CharacterCreationUI = ccUI;

            SkillsUIPanel skillsUI = canvas.gameObject.AddComponent<SkillsUIPanel>();
            skillsUI.BuildUI(canvas);
            gm.SkillsUI = skillsUI;
            ccUI.SkillsUI = skillsUI;

            FeatSelectionUI featUI = canvas.gameObject.AddComponent<FeatSelectionUI>();
            featUI.BuildUI(canvas);
            ccUI.FeatUI = featUI;

            SpellDatabase.Init();
            SpellSelectionUI spellUI = canvas.gameObject.AddComponent<SpellSelectionUI>();
            spellUI.BuildUI(canvas);
            ccUI.SpellUI = spellUI;

            // Spell preparation UI for Wizards (D&D 3.5e slot-based preparation)
            SpellPreparationUI spellPrepUI = canvas.gameObject.AddComponent<SpellPreparationUI>();
            spellPrepUI.BuildUI(canvas);
            gm.SpellPreparationUI = spellPrepUI;
            ccUI.SpellPrepUI = spellPrepUI;
        }

        StartCoroutine(WireButtons(combatUI));
    }

    private System.Collections.IEnumerator WireButtons(CombatUI ui)
    {
        yield return null;

        if (ui.MoveButton != null)
            ui.MoveButton.onClick.AddListener(() => GameManager.Instance.OnMoveButtonPressed());
        if (ui.FiveFootStepButton != null)
            ui.FiveFootStepButton.onClick.AddListener(() => GameManager.Instance.OnFiveFootStepButtonPressed());
        if (ui.DropProneButton != null)
            ui.DropProneButton.onClick.AddListener(() => GameManager.Instance.OnDropProneButtonPressed());
        if (ui.StandUpButton != null)
            ui.StandUpButton.onClick.AddListener(() => GameManager.Instance.OnStandUpButtonPressed());
        if (ui.CrawlButton != null)
            ui.CrawlButton.onClick.AddListener(() => GameManager.Instance.OnCrawlButtonPressed());
        if (ui.AttackButton != null)
        {
            ui.AttackButton.onClick.RemoveAllListeners();
            ui.AttackButton.onClick.AddListener(() => GameManager.Instance.OnAttackButtonPressed());
        }
        if (ui.AttackThrownButton != null)
        {
            ui.AttackThrownButton.onClick.RemoveAllListeners();
            ui.AttackThrownButton.onClick.AddListener(() => GameManager.Instance.OnThrownAttackButtonPressed());
        }
        if (ui.AttackOffHandButton != null)
        {
            ui.AttackOffHandButton.onClick.RemoveAllListeners();
            ui.AttackOffHandButton.onClick.AddListener(() => GameManager.Instance.OnOffHandAttackButtonPressed());
        }
        if (ui.AttackOffHandThrownButton != null)
        {
            ui.AttackOffHandThrownButton.onClick.RemoveAllListeners();
            ui.AttackOffHandThrownButton.onClick.AddListener(() => GameManager.Instance.OnOffHandThrownAttackButtonPressed());
        }
        if (ui.AttackDefensivelyButton != null)
            ui.AttackDefensivelyButton.onClick.AddListener(() => GameManager.Instance.OnAttackDefensivelyButtonPressed());
        if (ui.SpecialAttackButton != null)
            ui.SpecialAttackButton.onClick.AddListener(() => GameManager.Instance.OnSpecialAttackButtonPressed());
        if (ui.TurnUndeadButton != null)
            ui.TurnUndeadButton.onClick.AddListener(() => GameManager.Instance.OnTurnUndeadButtonPressed());
        if (ui.GrappleActionsButton != null)
        {
            ui.GrappleActionsButton.onClick.RemoveAllListeners();
            ui.GrappleActionsButton.gameObject.SetActive(false); // legacy menu button disabled
        }
        if (ui.GrappleDamageButton != null)
            ui.GrappleDamageButton.onClick.AddListener(() => GameManager.Instance.OnGrappleDamageButtonPressed());
        if (ui.GrappleLightWeaponAttackButton != null)
            ui.GrappleLightWeaponAttackButton.onClick.AddListener(() => GameManager.Instance.OnGrappleLightWeaponAttackButtonPressed());
        if (ui.GrappleUnarmedAttackButton != null)
            ui.GrappleUnarmedAttackButton.onClick.AddListener(() => GameManager.Instance.OnGrappleUnarmedAttackButtonPressed());
        if (ui.GrapplePinButton != null)
            ui.GrapplePinButton.onClick.AddListener(() => GameManager.Instance.OnGrapplePinButtonPressed());
        if (ui.GrappleBreakPinButton != null)
            ui.GrappleBreakPinButton.onClick.AddListener(() => GameManager.Instance.OnGrappleBreakPinButtonPressed());
        if (ui.GrappleEscapeArtistButton != null)
            ui.GrappleEscapeArtistButton.onClick.AddListener(() => GameManager.Instance.OnGrappleEscapeArtistButtonPressed());
        if (ui.GrappleEscapeCheckButton != null)
            ui.GrappleEscapeCheckButton.onClick.AddListener(() => GameManager.Instance.OnGrappleEscapeCheckButtonPressed());
        if (ui.GrappleMoveButton != null)
            ui.GrappleMoveButton.onClick.AddListener(() => GameManager.Instance.OnGrappleMoveButtonPressed());
        if (ui.GrappleUseOpponentWeaponButton != null)
            ui.GrappleUseOpponentWeaponButton.onClick.AddListener(() => GameManager.Instance.OnGrappleUseOpponentWeaponButtonPressed());
        if (ui.GrappleDisarmSmallObjectButton != null)
            ui.GrappleDisarmSmallObjectButton.onClick.AddListener(() => GameManager.Instance.OnGrappleDisarmSmallObjectButtonPressed());
        if (ui.GrappleReleasePinnedButton != null)
            ui.GrappleReleasePinnedButton.onClick.AddListener(() => GameManager.Instance.OnGrappleReleasePinButtonPressed());
        if (ui.AidAnotherButton != null)
        {
            ui.AidAnotherButton.onClick.RemoveAllListeners();
            ui.AidAnotherButton.onClick.AddListener(() => GameManager.Instance.OnAidAnotherButtonPressed());
        }
        if (ui.OverrunButton != null)
        {
            ui.OverrunButton.onClick.RemoveAllListeners();
            ui.OverrunButton.onClick.AddListener(() => GameManager.Instance.OnOverrunButtonPressed());
        }
        if (ui.ChargeButton != null)
            ui.ChargeButton.onClick.AddListener(() => GameManager.Instance.OnChargeButtonPressed());
        // Full Attack actions are intentionally not wired from the main action window.
        if (ui.DualWieldButton != null)
            ui.DualWieldButton.onClick.AddListener(() => GameManager.Instance.OnDualWieldButtonPressed());
        if (ui.FlurryOfBlowsButton != null)
            ui.FlurryOfBlowsButton.onClick.AddListener(() => GameManager.Instance.OnFlurryOfBlowsButtonPressed());
        if (ui.RageButton != null)
            ui.RageButton.onClick.AddListener(() => GameManager.Instance.OnRageButtonPressed());
        if (ui.CastSpellButton != null)
            ui.CastSpellButton.onClick.AddListener(() => GameManager.Instance.OnCastSpellButtonPressed());
        if (ui.ReloadButton != null)
            ui.ReloadButton.onClick.AddListener(() => GameManager.Instance.OnReloadButtonPressed());
        if (ui.DropEquippedItemButton != null)
            ui.DropEquippedItemButton.onClick.AddListener(() => GameManager.Instance.OnDropEquippedItemButtonPressed());
        if (ui.PickUpItemButton != null)
        {
            ui.PickUpItemButton.onClick.RemoveAllListeners();
            ui.PickUpItemButton.onClick.AddListener(() => GameManager.Instance.OnPickUpItemButtonPressed());
        }
        if (ui.DamageModeToggleButton != null)
        {
            ui.DamageModeToggleButton.onClick.RemoveAllListeners();
            ui.DamageModeToggleButton.onClick.AddListener(() => GameManager.Instance.OnDamageModeTogglePressed());
        }
        if (ui.EndTurnButton != null)
            ui.EndTurnButton.onClick.AddListener(() => GameManager.Instance.OnEndTurnButtonPressed());

        if (ui.ResetUILayoutButton != null)
        {
            ui.ResetUILayoutButton.onClick.RemoveAllListeners();
            ui.ResetUILayoutButton.onClick.AddListener(() => ui.ResetFloatingWindowLayout());
        }

        if (ui.PowerAttackSlider != null)
            ui.PowerAttackSlider.onValueChanged.AddListener((val) => GameManager.Instance.OnPowerAttackSliderChanged(val));
        if (ui.RapidShotToggle != null)
            ui.RapidShotToggle.onClick.AddListener(() => GameManager.Instance.OnRapidShotTogglePressed());
    }

    // ========== UI HELPER METHODS ==========

    private Text CreateText(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string text, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Text t = go.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = alignment;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (t.font == null) t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);

        return t;
    }

    private GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Image img = go.AddComponent<Image>();
        img.color = bgColor;

        return go;
    }

    private Image CreateHPBar(Transform parent, string name,
        Vector2 anchoredPos, Vector2 sizeDelta, Color barColor)
    {
        GameObject bgGO = new GameObject(name + "BG");
        bgGO.transform.SetParent(parent, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.zero;
        bgRT.pivot = Vector2.zero;
        bgRT.anchoredPosition = anchoredPos;
        bgRT.sizeDelta = sizeDelta;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f);

        GameObject fillGO = new GameObject(name + "Fill");
        fillGO.transform.SetParent(bgGO.transform, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.pivot = new Vector2(0, 0.5f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = barColor;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;

        return fillImg;
    }

    private Image CreateActiveIndicator(Transform parent, string name,
        Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.enabled = false;

        return img;
    }

    private Image CreateIconImage(Transform parent, string name, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Image img = go.AddComponent<Image>();
        img.color = Color.white;
        img.preserveAspect = true;
        img.enabled = false;

        return img;
    }

    private void AddBackground(Transform textTransform, Color color, Vector2 size)
    {
        GameObject bgGO = new GameObject("BG");
        bgGO.transform.SetParent(textTransform.parent, false);
        bgGO.transform.SetSiblingIndex(textTransform.GetSiblingIndex());

        RectTransform rt = bgGO.AddComponent<RectTransform>();
        RectTransform textRT = textTransform.GetComponent<RectTransform>();
        rt.anchorMin = textRT.anchorMin;
        rt.anchorMax = textRT.anchorMax;
        rt.pivot = textRT.pivot;
        rt.anchoredPosition = textRT.anchoredPosition;
        rt.sizeDelta = size;

        Image img = bgGO.AddComponent<Image>();
        img.color = color;

        textTransform.SetAsLastSibling();
    }

    private Slider CreateSlider(Transform parent, string name,
        Vector2 anchoredPos, Vector2 sizeDelta,
        float minValue, float maxValue, float defaultValue)
    {
        GameObject sliderGO = new GameObject(name);
        sliderGO.transform.SetParent(parent, false);

        RectTransform rt = sliderGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.25f);
        bgRT.anchorMax = new Vector2(1, 0.75f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1, 0.75f);
        fillAreaRT.offsetMin = new Vector2(5, 0);
        fillAreaRT.offsetMax = new Vector2(-5, 0);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        RectTransform fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.8f, 0.4f, 0.2f, 1f);

        GameObject handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(10, 0);
        handleAreaRT.offsetMax = new Vector2(-10, 0);

        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        RectTransform handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(20, 0);
        handleRT.anchorMin = new Vector2(0, 0);
        handleRT.anchorMax = new Vector2(0, 1);
        Image handleImg = handleGO.AddComponent<Image>();
        handleImg.color = new Color(1f, 0.7f, 0.3f, 1f);

        Slider slider = sliderGO.AddComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.wholeNumbers = true;
        slider.value = defaultValue;

        return slider;
    }

    /// <summary>Create a button suitable for grid layout (no explicit anchoring needed).</summary>
    private Button CreateGridButton(Transform parent, string name, string label, Color bgColor)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);

        // RectTransform will be managed by GridLayoutGroup
        btnGO.AddComponent<RectTransform>();

        Image img = btnGO.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        // Label text child - stretches to fill button
        GameObject txtGO = new GameObject(name + "Label");
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(4, 2);
        txtRT.offsetMax = new Vector2(-4, -2);

        Text txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = 12;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontStyle = FontStyle.Bold;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (txt.font == null) txt.font = Font.CreateDynamicFontFromOSFont("Arial", 12);

        return btn;
    }
}
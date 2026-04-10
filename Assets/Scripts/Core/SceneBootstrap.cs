using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bootstraps the entire game scene programmatically.
/// Attach this to a single empty GameObject in the scene.
/// Creates all required GameObjects, components, and UI at runtime.
/// Supports two PC characters (PC1, PC2) and multiple NPC enemies with full D&D 3.5 stats display.
/// </summary>
public class SceneBootstrap : MonoBehaviour
{
    private void Awake()
    {
        // Build the entire scene
        SetupCamera();
        SquareGrid grid = CreateSquareGrid();
        var (pc1, pc2, npc, npcList) = CreateCharacters();
        CombatUI combatUI = CreateUI();
        SetupGameManager(grid, pc1, pc2, npc, combatUI, npcList);
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

        int border = 2; // border thickness in pixels

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Draw a square with border
                if (x < border || x >= size - border || y < border || y >= size - border)
                {
                    pixels[y * size + x] = outline;
                }
                else
                {
                    pixels[y * size + x] = fill;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    // ========== CHARACTERS ==========
    private (CharacterController pc1, CharacterController pc2, CharacterController npc, System.Collections.Generic.List<CharacterController> npcList) CreateCharacters()
    {
        // PC1
        GameObject pc1GO = new GameObject("PC_Hero1");
        pc1GO.AddComponent<SpriteRenderer>();
        CharacterController pc1 = pc1GO.AddComponent<CharacterController>();
        pc1.IsPlayerControlled = true;

        // PC2
        GameObject pc2GO = new GameObject("PC_Hero2");
        pc2GO.AddComponent<SpriteRenderer>();
        CharacterController pc2 = pc2GO.AddComponent<CharacterController>();
        pc2.IsPlayerControlled = true;

        // Legacy NPC (first enemy — kept for backward compatibility)
        GameObject npcGO = new GameObject("NPC_Enemy_0");
        npcGO.AddComponent<SpriteRenderer>();
        CharacterController npc = npcGO.AddComponent<CharacterController>();
        npc.IsPlayerControlled = false;

        // Create additional NPC GameObjects for the encounter
        // Default encounter: Skeleton Archer, Orc Berserker, Hobgoblin Sergeant
        var npcList = new System.Collections.Generic.List<CharacterController>();
        npcList.Add(npc); // first NPC reuses the legacy field

        string[] enemyNames = { "NPC_Enemy_1", "NPC_Enemy_2" };
        for (int i = 0; i < enemyNames.Length; i++)
        {
            GameObject go = new GameObject(enemyNames[i]);
            go.AddComponent<SpriteRenderer>();
            CharacterController cc = go.AddComponent<CharacterController>();
            cc.IsPlayerControlled = false;
            npcList.Add(cc);
        }

        Debug.Log($"[SceneBootstrap] Created {npcList.Count} NPC GameObjects for encounter.");
        return (pc1, pc2, npc, npcList);
    }

    // ========== UI ==========
    // Panel dimensions — taller and wider to accommodate ability scores + racial info
    private const float PanelWidth = 310f;
    private const float PanelHeight = 220f;

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

        // CombatUI component
        CombatUI combatUI = canvasGO.AddComponent<CombatUI>();

        // --- Turn Indicator (top center) ---
        combatUI.TurnIndicatorText = CreateText(canvasGO.transform, "TurnIndicator",
            new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -10), new Vector2(800, 50),
            "", 24, Color.white, TextAnchor.MiddleCenter);

        AddBackground(combatUI.TurnIndicatorText.transform, new Color(0, 0, 0, 0.7f), new Vector2(820, 55));

        // --- PC1 Stats Panel (bottom left) ---
        CreatePC1Panel(canvasGO.transform, combatUI);

        // --- PC2 Stats Panel (next to PC1) ---
        CreatePC2Panel(canvasGO.transform, combatUI);

        // --- NPC Stats Panels (right side, stacked for each enemy) ---
        CreateNPCPanel(canvasGO.transform, combatUI); // Legacy single NPC panel (index 0)
        CreateMultiNPCPanels(canvasGO.transform, combatUI, 3); // 3 enemy panels

        // --- Combat Log (bottom center, raised above panels) ---
        // Taller to accommodate multi-attack results (full attack / dual wield)
        GameObject logPanel = CreatePanel(canvasGO.transform, "CombatLogPanel",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, PanelHeight + 20), new Vector2(540, 140),
            new Color(0, 0, 0, 0.75f));

        combatUI.CombatLogText = CreateText(logPanel.transform, "CombatLog",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 5), new Vector2(520, 130),
            "Combat begins! Choose your actions.", 14, Color.yellow, TextAnchor.MiddleCenter);

        // --- Action Panel (right side, middle) - D&D 3.5 Action Economy ---
        float actionPanelHeight = 310f;
        float actionPanelWidth = 240f;
        GameObject actionPanel = CreatePanel(canvasGO.transform, "ActionPanel",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-10, 0), new Vector2(actionPanelWidth, actionPanelHeight),
            new Color(0.15f, 0.15f, 0.25f, 0.9f));
        combatUI.ActionPanel = actionPanel;

        float btnW = actionPanelWidth - 20f;
        float btnH = 35f;
        float y = actionPanelHeight - 10f;

        // Title
        CreateText(actionPanel.transform, "ActionsTitle",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y - 25), new Vector2(btnW, 25),
            "ACTIONS", 18, Color.white, TextAnchor.MiddleCenter);
        y -= 30f;

        // Action Status text
        combatUI.ActionStatusText = CreateText(actionPanel.transform, "ActionStatus",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y - 22), new Vector2(btnW, 22),
            "[Move] [Standard]", 11, new Color(0.7f, 0.9f, 0.7f), TextAnchor.MiddleCenter);
        y -= 28f;

        // Move Button (Move Action)
        combatUI.MoveButton = CreateButton(actionPanel.transform, "MoveBtn",
            new Vector2(10, y - btnH), new Vector2(btnW, btnH),
            "Move (Move Action)", new Color(0.2f, 0.5f, 0.2f), Color.white);
        y -= btnH + 5f;

        // Attack Button (Standard Action)
        combatUI.AttackButton = CreateButton(actionPanel.transform, "AttackBtn",
            new Vector2(10, y - btnH), new Vector2(btnW, btnH),
            "Attack (Standard)", new Color(0.8f, 0.2f, 0.2f), Color.white);
        y -= btnH + 5f;

        // Full Attack Button (Full-Round Action)
        combatUI.FullAttackButton = CreateButton(actionPanel.transform, "FullAttackBtn",
            new Vector2(10, y - btnH), new Vector2(btnW, btnH),
            "Full Attack (Full-Round)", new Color(0.7f, 0.15f, 0.15f), Color.white);
        y -= btnH + 5f;

        // Dual Wield Button (Full-Round Action)
        combatUI.DualWieldButton = CreateButton(actionPanel.transform, "DualWieldBtn",
            new Vector2(10, y - btnH), new Vector2(btnW, btnH),
            "Dual Wield (Full-Round)", new Color(0.6f, 0.3f, 0.6f), Color.white);
        y -= btnH + 5f;

        // Flurry of Blows Button (Full-Round Action, Monk only)
        combatUI.FlurryOfBlowsButton = CreateButton(actionPanel.transform, "FlurryBtn",
            new Vector2(10, y - btnH), new Vector2(btnW, btnH),
            "Flurry of Blows", new Color(0.2f, 0.6f, 0.6f), Color.white);
        y -= btnH + 5f;

        // Rage Button (Free Action, Barbarian only)
        combatUI.RageButton = CreateButton(actionPanel.transform, "RageBtn",
            new Vector2(10, y - btnH), new Vector2(btnW, btnH),
            "Rage", new Color(0.7f, 0.2f, 0.1f), Color.white);
        y -= btnH + 3f;

        // Rage Status Text
        combatUI.RageStatusText = CreateText(actionPanel.transform, "RageStatus",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y - 18), new Vector2(btnW, 18),
            "", 10, new Color(1f, 0.6f, 0.3f), TextAnchor.MiddleCenter);
        combatUI.RageStatusText.gameObject.SetActive(false);
        y -= 22f;

        // End Turn Button
        combatUI.EndTurnButton = CreateButton(actionPanel.transform, "EndTurnBtn",
            new Vector2(10, y - btnH), new Vector2(btnW, btnH),
            "End Turn", new Color(0.3f, 0.3f, 0.6f), Color.white);
        y -= btnH + 10f;

        actionPanel.SetActive(false);

        // --- Feat Controls (positioned above action panel) ---
        CreateFeatControls(canvasGO.transform, combatUI, actionPanelWidth);

        return combatUI;
    }

    /// <summary>Create PC1 stats panel with D&D 3.5 ability scores.</summary>
    private void CreatePC1Panel(Transform parent, CombatUI combatUI)
    {
        GameObject panel = CreatePanel(parent, "PC1StatsPanel",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(10, 10), new Vector2(PanelWidth, PanelHeight),
            new Color(0.1f, 0.2f, 0.4f, 0.85f));
        combatUI.PC1Panel = panel;

        // Active indicator
        combatUI.PC1ActiveIndicator = CreateActiveIndicator(panel.transform, "PC1Active",
            new Vector2(0, PanelHeight - 6), new Vector2(PanelWidth, 6), new Color(0f, 1f, 0.3f));

        float y = PanelHeight - 18;

        // Name
        combatUI.PC1NameText = CreateText(panel.transform, "PC1Name",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 22),
            "Aldric (Lv 3 Dwarf Fighter)", 16, new Color(0.3f, 0.9f, 0.3f), TextAnchor.MiddleLeft);
        y -= 22;

        // Ability scores (2 rows of 3)
        combatUI.PC1AbilityText = CreateText(panel.transform, "PC1Abilities",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y - 28), new Vector2(PanelWidth - 20, 38),
            "STR 16(+3) DEX 12(+1) CON 14(+2)\nWIS 10(+0) INT 10(+0) CHA 13(+1)", 12,
            new Color(0.8f, 0.8f, 0.6f), TextAnchor.UpperLeft);
        y -= 44;

        // Separator line (thin panel)
        CreatePanel(panel.transform, "PC1Sep",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 1),
            new Color(1, 1, 1, 0.2f));
        y -= 6;

        // HP
        combatUI.PC1HPText = CreateText(panel.transform, "PC1HP",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 20),
            "HP: 28/28", 16, Color.white, TextAnchor.MiddleLeft);
        y -= 18;

        // HP Bar
        combatUI.PC1HPBar = CreateHPBar(panel.transform, "PC1HPBar",
            new Vector2(10, y), new Vector2(PanelWidth - 20, 12),
            new Color(0.2f, 0.8f, 0.2f));
        y -= 20;

        // AC
        combatUI.PC1ACText = CreateText(panel.transform, "PC1AC",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 18),
            "AC: 17", 14, Color.white, TextAnchor.MiddleLeft);
        y -= 20;

        // Attack Bonus
        combatUI.PC1AtkText = CreateText(panel.transform, "PC1Atk",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 18),
            "Atk: +6", 14, Color.white, TextAnchor.MiddleLeft);
        y -= 20;

        // Speed
        combatUI.PC1SpeedText = CreateText(panel.transform, "PC1Speed",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 18),
            "Speed: 4 sq (20 ft) (no armor penalty)", 12, Color.white, TextAnchor.MiddleLeft);
    }

    /// <summary>Create PC2 stats panel with D&D 3.5 ability scores.</summary>
    private void CreatePC2Panel(Transform parent, CombatUI combatUI)
    {
        GameObject panel = CreatePanel(parent, "PC2StatsPanel",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(PanelWidth + 20, 10), new Vector2(PanelWidth, PanelHeight),
            new Color(0.1f, 0.15f, 0.35f, 0.85f));
        combatUI.PC2Panel = panel;

        combatUI.PC2ActiveIndicator = CreateActiveIndicator(panel.transform, "PC2Active",
            new Vector2(0, PanelHeight - 6), new Vector2(PanelWidth, 6), new Color(0.3f, 0.5f, 1f));

        float y = PanelHeight - 18;

        combatUI.PC2NameText = CreateText(panel.transform, "PC2Name",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 22),
            "Lyra (Lv 3 Elf Rogue)", 16, new Color(0.5f, 0.7f, 1f), TextAnchor.MiddleLeft);
        y -= 22;

        combatUI.PC2AbilityText = CreateText(panel.transform, "PC2Abilities",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y - 28), new Vector2(PanelWidth - 20, 38),
            "STR 12(+1) DEX 17(+3) CON 12(+1)\nWIS 13(+1) INT 14(+2) CHA 10(+0)", 12,
            new Color(0.8f, 0.8f, 0.6f), TextAnchor.UpperLeft);
        y -= 44;

        CreatePanel(panel.transform, "PC2Sep",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 1),
            new Color(1, 1, 1, 0.2f));
        y -= 6;

        combatUI.PC2HPText = CreateText(panel.transform, "PC2HP",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 20),
            "HP: 18/18", 16, Color.white, TextAnchor.MiddleLeft);
        y -= 18;

        combatUI.PC2HPBar = CreateHPBar(panel.transform, "PC2HPBar",
            new Vector2(10, y), new Vector2(PanelWidth - 20, 12),
            new Color(0.3f, 0.5f, 1f));
        y -= 20;

        combatUI.PC2ACText = CreateText(panel.transform, "PC2AC",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 18),
            "AC: 15", 14, Color.white, TextAnchor.MiddleLeft);
        y -= 20;

        combatUI.PC2AtkText = CreateText(panel.transform, "PC2Atk",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 18),
            "Atk: +3", 14, Color.white, TextAnchor.MiddleLeft);
        y -= 20;

        combatUI.PC2SpeedText = CreateText(panel.transform, "PC2Speed",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 18),
            "Speed: 6 sq (30 ft)", 12, Color.white, TextAnchor.MiddleLeft);
    }

    /// <summary>Create NPC stats panel with D&D 3.5 ability scores.</summary>
    private void CreateNPCPanel(Transform parent, CombatUI combatUI)
    {
        GameObject panel = CreatePanel(parent, "NPCStatsPanel",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-10, 10), new Vector2(PanelWidth, PanelHeight),
            new Color(0.4f, 0.1f, 0.1f, 0.85f));

        float y = PanelHeight - 18;

        combatUI.NPCNameText = CreateText(panel.transform, "NPCName",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 22),
            "Goblin Warchief (Lv 2 Warrior)", 16, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleLeft);
        y -= 22;

        combatUI.NPCAbilityText = CreateText(panel.transform, "NPCAbilities",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y - 28), new Vector2(PanelWidth - 20, 38),
            "STR 14(+2) DEX 15(+2) CON 13(+1)\nWIS 10(+0) INT 10(+0) CHA 8(-1)", 12,
            new Color(0.8f, 0.8f, 0.6f), TextAnchor.UpperLeft);
        y -= 44;

        CreatePanel(panel.transform, "NPCSep",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 1),
            new Color(1, 1, 1, 0.2f));
        y -= 6;

        combatUI.NPCHPText = CreateText(panel.transform, "NPCHP",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 20),
            "HP: 14/14", 16, Color.white, TextAnchor.MiddleLeft);
        y -= 18;

        combatUI.NPCHPBar = CreateHPBar(panel.transform, "NPCHPBar",
            new Vector2(10, y), new Vector2(PanelWidth - 20, 12),
            new Color(0.8f, 0.2f, 0.2f));
        y -= 20;

        combatUI.NPCACText = CreateText(panel.transform, "NPCAC",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 18),
            "AC: 16", 14, Color.white, TextAnchor.MiddleLeft);
        y -= 20;

        combatUI.NPCAtkText = CreateText(panel.transform, "NPCAtk",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 18),
            "Atk: +4", 14, Color.white, TextAnchor.MiddleLeft);
        y -= 20;

        combatUI.NPCSpeedText = CreateText(panel.transform, "NPCSpeed",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, y), new Vector2(PanelWidth - 20, 18),
            "Speed: 3 sq (15 ft)", 12, Color.white, TextAnchor.MiddleLeft);
    }

    /// <summary>
    /// Create multiple NPC stat panels stacked vertically on the right side.
    /// Each panel is a compact version of the single-NPC panel, sized to fit
    /// multiple enemies on screen simultaneously.
    /// </summary>
    private void CreateMultiNPCPanels(Transform parent, CombatUI combatUI, int count)
    {
        // Compact panel dimensions for multi-enemy display
        float compactWidth = 260f;
        float compactHeight = 130f;
        float spacing = 5f;

        // Hide the legacy single-NPC panel — we'll use multi-panels instead
        // (Legacy panel kept for backward compat but hidden in multi-NPC mode)

        for (int i = 0; i < count; i++)
        {
            float yOffset = 10f + i * (compactHeight + spacing);

            NPCPanelUI panelUI = new NPCPanelUI();

            // Panel anchored to bottom-right, stacked upward
            Color panelColor = new Color(0.3f, 0.1f, 0.1f, 0.85f);
            GameObject panel = CreatePanel(parent, $"NPCPanel_{i}",
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-10, yOffset), new Vector2(compactWidth, compactHeight),
                panelColor);
            panelUI.Panel = panel;

            float y = compactHeight - 14;

            // Name
            panelUI.NameText = CreateText(panel.transform, $"NPCName_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(8, y), new Vector2(compactWidth - 16, 18),
                $"Enemy {i + 1}", 13, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleLeft);
            y -= 18;

            // Ability scores (compact single line)
            panelUI.AbilityText = CreateText(panel.transform, $"NPCAbilities_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(8, y - 10), new Vector2(compactWidth - 16, 20),
                "STR -- DEX -- CON -- WIS -- INT -- CHA --", 9,
                new Color(0.8f, 0.8f, 0.6f), TextAnchor.UpperLeft);
            y -= 22;

            // Separator
            CreatePanel(panel.transform, $"NPCSep_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(8, y), new Vector2(compactWidth - 16, 1),
                new Color(1, 1, 1, 0.2f));
            y -= 4;

            // HP text
            panelUI.HPText = CreateText(panel.transform, $"NPCHP_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(8, y), new Vector2(compactWidth - 16, 16),
                "HP: --/--", 13, Color.white, TextAnchor.MiddleLeft);
            y -= 14;

            // HP bar
            panelUI.HPBar = CreateHPBar(panel.transform, $"NPCHPBar_{i}",
                new Vector2(8, y), new Vector2(compactWidth - 16, 8),
                new Color(0.8f, 0.2f, 0.2f));
            y -= 14;

            // AC and Atk on same row
            panelUI.ACText = CreateText(panel.transform, $"NPCAC_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(8, y), new Vector2(120, 16),
                "AC: --", 12, Color.white, TextAnchor.MiddleLeft);

            panelUI.AtkText = CreateText(panel.transform, $"NPCAtk_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(130, y), new Vector2(120, 16),
                "Atk: --", 12, Color.white, TextAnchor.MiddleLeft);
            y -= 16;

            // Speed
            panelUI.SpeedText = CreateText(panel.transform, $"NPCSpeed_{i}",
                Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(8, y), new Vector2(compactWidth - 16, 14),
                "Speed: -- sq", 10, Color.white, TextAnchor.MiddleLeft);

            combatUI.NPCPanels.Add(panelUI);
        }

        Debug.Log($"[SceneBootstrap] Created {count} NPC stat panels.");
    }

    /// <summary>Create feat control panels (Power Attack slider, Rapid Shot toggle).</summary>
    private void CreateFeatControls(Transform canvasTransform, CombatUI combatUI, float actionPanelWidth)
    {
        float featPanelW = actionPanelWidth;
        float featPanelH = 50f;

        // Power Attack Panel (positioned above the action panel on the right side)
        GameObject paPanel = CreatePanel(canvasTransform, "PowerAttackPanel",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-10, 175), new Vector2(featPanelW, featPanelH),
            new Color(0.3f, 0.15f, 0.1f, 0.9f));
        combatUI.PowerAttackPanel = paPanel;

        // Power Attack Label
        combatUI.PowerAttackLabel = CreateText(paPanel.transform, "PALabel",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, featPanelH - 22), new Vector2(featPanelW - 20, 20),
            "Power Attack: OFF", 13, new Color(1f, 0.8f, 0.5f), TextAnchor.MiddleLeft);

        // Power Attack Slider
        combatUI.PowerAttackSlider = CreateSlider(paPanel.transform, "PASlider",
            new Vector2(10, 5), new Vector2(featPanelW - 20, 20),
            0, 3, 0);

        paPanel.SetActive(false);

        // Rapid Shot Panel (positioned below Power Attack)
        GameObject rsPanel = CreatePanel(canvasTransform, "RapidShotPanel",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-10, 230), new Vector2(featPanelW, 40f),
            new Color(0.1f, 0.2f, 0.3f, 0.9f));
        combatUI.RapidShotPanel = rsPanel;

        // Rapid Shot Toggle Button
        combatUI.RapidShotToggle = CreateButton(rsPanel.transform, "RSToggle",
            new Vector2(5, 5), new Vector2(featPanelW - 10, 30f),
            "Rapid Shot: OFF", new Color(0.5f, 0.5f, 0.5f), Color.white);

        // Rapid Shot Label (use the button's text)
        combatUI.RapidShotLabel = combatUI.RapidShotToggle.GetComponentInChildren<Text>();

        rsPanel.SetActive(false);
    }

    // ========== GAME MANAGER ==========
    private void SetupGameManager(SquareGrid grid, CharacterController pc1, CharacterController pc2, CharacterController npc, CombatUI combatUI, System.Collections.Generic.List<CharacterController> npcList = null)
    {
        GameManager gm = gameObject.AddComponent<GameManager>();
        gm.Grid = grid;
        gm.PC1 = pc1;
        gm.PC2 = pc2;
        gm.NPC = npc;
        gm.CombatUI = combatUI;

        // Set up multi-NPC list
        if (npcList != null)
            gm.NPCs = npcList;
        else
        {
            gm.NPCs = new System.Collections.Generic.List<CharacterController> { npc };
        }

        // Create Inventory UI on the canvas
        Canvas canvas = combatUI.GetComponent<Canvas>();
        if (canvas != null)
        {
            InventoryUI invUI = canvas.gameObject.AddComponent<InventoryUI>();
            invUI.BuildUI(canvas);
            gm.InventoryUI = invUI;
        }

        // Create Character Creation UI on the canvas
        if (canvas != null)
        {
            // Initialize race database early for character creation
            RaceDatabase.Init();
            ItemDatabase.Init();
            FeatDefinitions.Init();

            CharacterCreationUI ccUI = canvas.gameObject.AddComponent<CharacterCreationUI>();
            ccUI.BuildUI(canvas);
            gm.CharacterCreationUI = ccUI;

            // Create Skills UI Panel
            SkillsUIPanel skillsUI = canvas.gameObject.AddComponent<SkillsUIPanel>();
            skillsUI.BuildUI(canvas);
            gm.SkillsUI = skillsUI;
            ccUI.SkillsUI = skillsUI;

            // Create Feat Selection UI Panel
            FeatSelectionUI featUI = canvas.gameObject.AddComponent<FeatSelectionUI>();
            featUI.BuildUI(canvas);
            ccUI.FeatUI = featUI;
        }

        // Wire up button events (deferred to next frame so GameManager.Instance is set)
        StartCoroutine(WireButtons(combatUI));
    }

    private System.Collections.IEnumerator WireButtons(CombatUI ui)
    {
        yield return null; // wait one frame

        if (ui.MoveButton != null)
            ui.MoveButton.onClick.AddListener(() => GameManager.Instance.OnMoveButtonPressed());
        if (ui.AttackButton != null)
            ui.AttackButton.onClick.AddListener(() => GameManager.Instance.OnAttackButtonPressed());
        if (ui.FullAttackButton != null)
            ui.FullAttackButton.onClick.AddListener(() => GameManager.Instance.OnFullAttackButtonPressed());
        if (ui.DualWieldButton != null)
            ui.DualWieldButton.onClick.AddListener(() => GameManager.Instance.OnDualWieldButtonPressed());
        if (ui.FlurryOfBlowsButton != null)
            ui.FlurryOfBlowsButton.onClick.AddListener(() => GameManager.Instance.OnFlurryOfBlowsButtonPressed());
        if (ui.RageButton != null)
            ui.RageButton.onClick.AddListener(() => GameManager.Instance.OnRageButtonPressed());
        if (ui.EndTurnButton != null)
            ui.EndTurnButton.onClick.AddListener(() => GameManager.Instance.OnEndTurnButtonPressed());

        // Feat controls
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
        if (t.font == null)
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (t.font == null)
            t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);

        return t;
    }

    private GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        Color bgColor)
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

    /// <summary>
    /// Create a small colored bar at the top of a panel to indicate the active PC.
    /// </summary>
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
        img.enabled = false; // hidden by default

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

    /// <summary>Create a UI Slider programmatically.</summary>
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

        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        RectTransform bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.25f);
        bgRT.anchorMax = new Vector2(1, 0.75f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Fill Area
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

        // Handle Slide Area
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

        // Create the Slider component
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

    private Button CreateButton(Transform parent, string name,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string label, Color bgColor, Color textColor)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);

        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Image img = btnGO.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        CreateText(btnGO.transform, name + "Label",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            label, 18, textColor, TextAnchor.MiddleCenter);

        return btn;
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bootstraps the entire game scene programmatically.
/// Attach this to a single empty GameObject in the scene.
/// Creates all required GameObjects, components, and UI at runtime.
/// Now supports two PC characters (PC1, PC2) and one NPC with full D&D 3.5 stats display.
/// </summary>
public class SceneBootstrap : MonoBehaviour
{
    private void Awake()
    {
        // Build the entire scene
        SetupCamera();
        HexGrid grid = CreateHexGrid();
        var (pc1, pc2, npc) = CreateCharacters();
        CombatUI combatUI = CreateUI();
        SetupGameManager(grid, pc1, pc2, npc, combatUI);
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

    // ========== HEX GRID ==========
    private HexGrid CreateHexGrid()
    {
        GameObject gridGO = new GameObject("HexGrid");
        HexGrid grid = gridGO.AddComponent<HexGrid>();
        grid.hexSprite = CreateHexSprite();
        return grid;
    }

    private Sprite CreateHexSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color transparent = new Color(0, 0, 0, 0);
        Color fill = Color.white;
        Color outline = new Color(0.4f, 0.5f, 0.4f, 1f);

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = transparent;

        float cx = size / 2f;
        float cy = size / 2f;
        float outerR = size / 2f - 2f;

        Vector2[] verts = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i - 30f);
            verts[i] = new Vector2(cx + outerR * Mathf.Cos(angle), cy + outerR * Mathf.Sin(angle));
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (IsPointInHex(x, y, verts))
                {
                    bool nearEdge = false;
                    for (int dx = -1; dx <= 1 && !nearEdge; dx++)
                        for (int dy = -1; dy <= 1 && !nearEdge; dy++)
                            if (!IsPointInHex(x + dx * 2, y + dy * 2, verts))
                                nearEdge = true;

                    pixels[y * size + x] = nearEdge ? outline : fill;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    private bool IsPointInHex(float px, float py, Vector2[] verts)
    {
        bool inside = false;
        int j = verts.Length - 1;
        for (int i = 0; i < verts.Length; i++)
        {
            if ((verts[i].y > py) != (verts[j].y > py) &&
                px < (verts[j].x - verts[i].x) * (py - verts[i].y) / (verts[j].y - verts[i].y) + verts[i].x)
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }

    // ========== CHARACTERS ==========
    private (CharacterController pc1, CharacterController pc2, CharacterController npc) CreateCharacters()
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

        // NPC
        GameObject npcGO = new GameObject("NPC_Goblin");
        npcGO.AddComponent<SpriteRenderer>();
        CharacterController npc = npcGO.AddComponent<CharacterController>();
        npc.IsPlayerControlled = false;

        return (pc1, pc2, npc);
    }

    // ========== UI ==========
    // Panel dimensions — taller to accommodate ability scores
    private const float PanelWidth = 280f;
    private const float PanelHeight = 210f;

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

        // --- NPC Stats Panel (bottom right) ---
        CreateNPCPanel(canvasGO.transform, combatUI);

        // --- Combat Log (bottom center, raised above panels) ---
        GameObject logPanel = CreatePanel(canvasGO.transform, "CombatLogPanel",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, PanelHeight + 20), new Vector2(500, 100),
            new Color(0, 0, 0, 0.75f));

        combatUI.CombatLogText = CreateText(logPanel.transform, "CombatLog",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 5), new Vector2(480, 90),
            "Combat begins! Move your heroes.", 16, Color.yellow, TextAnchor.MiddleCenter);

        // --- Action Panel (right side, middle) ---
        GameObject actionPanel = CreatePanel(canvasGO.transform, "ActionPanel",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-10, 0), new Vector2(200, 130),
            new Color(0.15f, 0.15f, 0.25f, 0.9f));
        combatUI.ActionPanel = actionPanel;

        CreateText(actionPanel.transform, "ActionsTitle",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 95), new Vector2(180, 30),
            "ACTIONS", 20, Color.white, TextAnchor.MiddleCenter);

        combatUI.AttackButton = CreateButton(actionPanel.transform, "AttackBtn",
            new Vector2(10, 50), new Vector2(180, 35),
            "Attack", new Color(0.8f, 0.2f, 0.2f), Color.white);

        combatUI.EndTurnButton = CreateButton(actionPanel.transform, "EndTurnBtn",
            new Vector2(10, 10), new Vector2(180, 35),
            "End Turn", new Color(0.3f, 0.3f, 0.6f), Color.white);

        actionPanel.SetActive(false);

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
            "Aldric (Lv 3)", 18, new Color(0.3f, 0.9f, 0.3f), TextAnchor.MiddleLeft);
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
            "Speed: 4 hexes", 14, Color.white, TextAnchor.MiddleLeft);
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
            "Lyra (Lv 3)", 18, new Color(0.5f, 0.7f, 1f), TextAnchor.MiddleLeft);
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
            "Speed: 5 hexes", 14, Color.white, TextAnchor.MiddleLeft);
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
            "Goblin Warchief (Lv 2)", 18, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleLeft);
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
            "Speed: 3 hexes", 14, Color.white, TextAnchor.MiddleLeft);
    }

    // ========== GAME MANAGER ==========
    private void SetupGameManager(HexGrid grid, CharacterController pc1, CharacterController pc2, CharacterController npc, CombatUI combatUI)
    {
        GameManager gm = gameObject.AddComponent<GameManager>();
        gm.Grid = grid;
        gm.PC1 = pc1;
        gm.PC2 = pc2;
        gm.NPC = npc;
        gm.CombatUI = combatUI;

        // Create Inventory UI on the canvas
        Canvas canvas = combatUI.GetComponent<Canvas>();
        if (canvas != null)
        {
            InventoryUI invUI = canvas.gameObject.AddComponent<InventoryUI>();
            invUI.BuildUI(canvas);
            gm.InventoryUI = invUI;
        }

        // Wire up button events (deferred to next frame so GameManager.Instance is set)
        StartCoroutine(WireButtons(combatUI));
    }

    private System.Collections.IEnumerator WireButtons(CombatUI ui)
    {
        yield return null; // wait one frame

        if (ui.AttackButton != null)
            ui.AttackButton.onClick.AddListener(() => GameManager.Instance.OnAttackButtonPressed());
        if (ui.EndTurnButton != null)
            ui.EndTurnButton.onClick.AddListener(() => GameManager.Instance.OnEndTurnButtonPressed());
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

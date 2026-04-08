using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bootstraps the entire game scene programmatically.
/// Attach this to a single empty GameObject in the scene.
/// Creates all required GameObjects, components, and UI at runtime.
/// </summary>
public class SceneBootstrap : MonoBehaviour
{
    private void Awake()
    {
        // Build the entire scene
        SetupCamera();
        HexGrid grid = CreateHexGrid();
        var (pc, npc) = CreateCharacters();
        CombatUI combatUI = CreateUI();
        SetupGameManager(grid, pc, npc, combatUI);
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

        // Create a simple hex sprite procedurally
        grid.hexSprite = CreateHexSprite();

        return grid;
    }

    /// <summary>
    /// Generate a hexagonal sprite texture procedurally.
    /// </summary>
    private Sprite CreateHexSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color transparent = new Color(0, 0, 0, 0);
        Color fill = Color.white;
        Color outline = new Color(0.4f, 0.5f, 0.4f, 1f);

        // Fill with transparent
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = transparent;

        // Draw pointy-top hexagon
        float cx = size / 2f;
        float cy = size / 2f;
        float outerR = size / 2f - 2f;

        // Hex vertices (pointy-top)
        Vector2[] verts = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i - 30f);
            verts[i] = new Vector2(cx + outerR * Mathf.Cos(angle), cy + outerR * Mathf.Sin(angle));
        }

        // Fill hex using scanline
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (IsPointInHex(x, y, verts))
                {
                    // Check if near edge for outline
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
        // Ray casting algorithm
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
    private (CharacterController pc, CharacterController npc) CreateCharacters()
    {
        GameObject pcGO = new GameObject("PC_Hero");
        pcGO.AddComponent<SpriteRenderer>();
        CharacterController pc = pcGO.AddComponent<CharacterController>();
        pc.IsPlayerControlled = true;

        GameObject npcGO = new GameObject("NPC_Goblin");
        npcGO.AddComponent<SpriteRenderer>();
        CharacterController npc = npcGO.AddComponent<CharacterController>();
        npc.IsPlayerControlled = false;

        return (pc, npc);
    }

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

        // CombatUI component
        CombatUI combatUI = canvasGO.AddComponent<CombatUI>();

        // --- Turn Indicator (top center) ---
        combatUI.TurnIndicatorText = CreateText(canvasGO.transform, "TurnIndicator",
            new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -10), new Vector2(800, 50),
            "", 24, Color.white, TextAnchor.MiddleCenter);

        // Add background to turn indicator
        AddBackground(combatUI.TurnIndicatorText.transform, new Color(0, 0, 0, 0.7f), new Vector2(820, 55));

        // --- PC Stats Panel (bottom left) ---
        GameObject pcPanel = CreatePanel(canvasGO.transform, "PCStatsPanel",
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(10, 10), new Vector2(280, 120),
            new Color(0.1f, 0.2f, 0.4f, 0.85f));

        combatUI.PCNameText = CreateText(pcPanel.transform, "PCName",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 85), new Vector2(260, 30),
            "Hero", 20, new Color(0.3f, 0.9f, 0.3f), TextAnchor.MiddleLeft);

        combatUI.PCHPText = CreateText(pcPanel.transform, "PCHP",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 55), new Vector2(200, 25),
            "HP: 30/30", 18, Color.white, TextAnchor.MiddleLeft);

        combatUI.PCACText = CreateText(pcPanel.transform, "PCAC",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 30), new Vector2(200, 25),
            "AC: 14", 18, Color.white, TextAnchor.MiddleLeft);

        combatUI.PCHPBar = CreateHPBar(pcPanel.transform, "PCHPBar",
            new Vector2(10, 10), new Vector2(260, 15),
            new Color(0.2f, 0.8f, 0.2f));

        // --- NPC Stats Panel (bottom right) ---
        GameObject npcPanel = CreatePanel(canvasGO.transform, "NPCStatsPanel",
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-10, 10), new Vector2(280, 120),
            new Color(0.4f, 0.1f, 0.1f, 0.85f));

        combatUI.NPCNameText = CreateText(npcPanel.transform, "NPCName",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 85), new Vector2(260, 30),
            "Goblin", 20, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleLeft);

        combatUI.NPCHPText = CreateText(npcPanel.transform, "NPCHP",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 55), new Vector2(200, 25),
            "HP: 20/20", 18, Color.white, TextAnchor.MiddleLeft);

        combatUI.NPCACText = CreateText(npcPanel.transform, "NPCAC",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 30), new Vector2(200, 25),
            "AC: 12", 18, Color.white, TextAnchor.MiddleLeft);

        combatUI.NPCHPBar = CreateHPBar(npcPanel.transform, "NPCHPBar",
            new Vector2(10, 10), new Vector2(260, 15),
            new Color(0.8f, 0.2f, 0.2f));

        // --- Combat Log (bottom center) ---
        GameObject logPanel = CreatePanel(canvasGO.transform, "CombatLogPanel",
            new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
            new Vector2(0, 10), new Vector2(500, 100),
            new Color(0, 0, 0, 0.75f));

        combatUI.CombatLogText = CreateText(logPanel.transform, "CombatLog",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 5), new Vector2(480, 90),
            "Combat begins! Move your hero.", 16, Color.yellow, TextAnchor.MiddleCenter);

        // --- Action Panel (right side, middle) ---
        GameObject actionPanel = CreatePanel(canvasGO.transform, "ActionPanel",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-10, 0), new Vector2(200, 130),
            new Color(0.15f, 0.15f, 0.25f, 0.9f));
        combatUI.ActionPanel = actionPanel;

        // Title
        CreateText(actionPanel.transform, "ActionsTitle",
            Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(10, 95), new Vector2(180, 30),
            "ACTIONS", 20, Color.white, TextAnchor.MiddleCenter);

        // Attack button
        combatUI.AttackButton = CreateButton(actionPanel.transform, "AttackBtn",
            new Vector2(10, 50), new Vector2(180, 35),
            "Attack", new Color(0.8f, 0.2f, 0.2f), Color.white);

        // End Turn button
        combatUI.EndTurnButton = CreateButton(actionPanel.transform, "EndTurnBtn",
            new Vector2(10, 10), new Vector2(180, 35),
            "End Turn", new Color(0.3f, 0.3f, 0.6f), Color.white);

        actionPanel.SetActive(false);

        return combatUI;
    }

    // ========== GAME MANAGER ==========
    private void SetupGameManager(HexGrid grid, CharacterController pc, CharacterController npc, CombatUI combatUI)
    {
        GameManager gm = gameObject.AddComponent<GameManager>();
        gm.Grid = grid;
        gm.PC = pc;
        gm.NPC = npc;
        gm.CombatUI = combatUI;

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
        // Unity 6+ uses LegacyRuntime.ttf; Arial.ttf is no longer a built-in resource
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null)
        {
            // Fallback for older Unity versions
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (t.font == null)
        {
            t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        }

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
        // Background
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

        // Fill bar
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

        // Move text in front
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

        // Button label
        CreateText(btnGO.transform, name + "Label",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero,
            label, 18, textColor, TextAnchor.MiddleCenter);

        return btn;
    }
}

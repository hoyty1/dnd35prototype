using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central game manager handling turn flow with D&D 3.5 action economy.
/// Supports two PC characters and one NPC with intelligent targeting.
/// Turn order: PC1 → PC2 → NPC → repeat
///
/// Action Economy per turn:
/// - 1 Move Action + 1 Standard Action (in any order)
/// - OR 1 Full-Round Action (uses both - e.g., Full Attack, Dual Wield)
/// - Standard can be converted to a second Move Action
/// - Plus 1 Swift Action per turn (simplified for now)
/// - Plus unlimited Free Actions
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>Enable/disable Debug.Log output of all attack rolls to the Unity Console.</summary>
    public static bool LogAttacksToConsole = true;
    public static GameManager Instance { get; private set; }

    [Header("Grid")]
    public SquareGrid Grid;

    [Header("Characters")]
    public CharacterController PC1;
    public CharacterController PC2;
    public CharacterController NPC;

    // Legacy alias
    public CharacterController PC { get => PC1; set => PC1 = value; }

    [Header("UI")]
    public CombatUI CombatUI;
    public InventoryUI InventoryUI;
    public CharacterCreationUI CharacterCreationUI;
    public SkillsUIPanel SkillsUI;

    /// <summary>Whether the game is waiting for character creation to complete.</summary>
    public bool WaitingForCharacterCreation { get; private set; }

    // Game state - simplified phases
    public enum TurnPhase { PC1Turn, PC2Turn, NPCTurn, CombatOver }

    // Sub-states for player turns
    public enum PlayerSubPhase { ChoosingAction, Moving, SelectingAttackTarget, Animating }

    public TurnPhase CurrentPhase { get; private set; }
    public PlayerSubPhase CurrentSubPhase { get; private set; }

    /// <summary>Returns the PC whose turn it currently is.</summary>
    public CharacterController ActivePC
    {
        get
        {
            if (CurrentPhase == TurnPhase.PC1Turn) return PC1;
            if (CurrentPhase == TurnPhase.PC2Turn) return PC2;
            return null;
        }
    }

    public bool IsPlayerTurn => ActivePC != null;

    // Current attack mode being selected for
    private enum PendingAttackMode { Single, FullAttack, DualWield, FlurryOfBlows, CastSpell }
    private PendingAttackMode _pendingAttackMode;
    private SpellData _pendingSpell; // Spell selected for casting

    private List<SquareCell> _highlightedCells = new List<SquareCell>();
    private string _lastCombatLog = "";
    private Camera _mainCam;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        Grid.GenerateGrid();
        CenterCamera();
        _mainCam = Camera.main;

        // Check if character creation UI exists
        if (CharacterCreationUI != null)
        {
            WaitingForCharacterCreation = true;
            CharacterCreationUI.OnCreationComplete = OnCharacterCreationComplete;
            Debug.Log("[GameManager] Waiting for character creation...");
        }
        else
        {
            // No creation UI - use default characters
            SetupCharacters();
            StartPCTurn(PC1);
            Debug.Log("[GameManager] Initialization complete (default characters). Phase: " + CurrentPhase);
        }
    }

    /// <summary>
    /// Called when both characters have been created through the character creation UI.
    /// </summary>
    private void OnCharacterCreationComplete(CharacterCreationData pc1Data, CharacterCreationData pc2Data)
    {
        WaitingForCharacterCreation = false;
        Debug.Log($"[GameManager] Character creation complete: {pc1Data.CharacterName} ({pc1Data.RaceName} {pc1Data.ClassName}), " +
                  $"{pc2Data.CharacterName} ({pc2Data.RaceName} {pc2Data.ClassName})");

        SetupCreatedCharacters(pc1Data, pc2Data);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);
        StartPCTurn(PC1);
    }

    /// <summary>
    /// Set up characters from character creation data.
    /// </summary>
    private void SetupCreatedCharacters(CharacterCreationData pc1Data, CharacterCreationData pc2Data)
    {
        RaceDatabase.Init();
        ItemDatabase.Init();

        // ===== PC1 =====
        pc1Data.ComputeFinalStats();
        int pc1ArmorBonus, pc1ShieldBonus, pc1DamageDice;
        GetClassDefaults(pc1Data.ClassName, out pc1ArmorBonus, out pc1ShieldBonus, out pc1DamageDice);

        CharacterStats pc1Stats = new CharacterStats(
            name: pc1Data.CharacterName,
            level: 3,
            characterClass: pc1Data.ClassName,
            str: pc1Data.STR, dex: pc1Data.DEX, con: pc1Data.CON,
            wis: pc1Data.WIS, intelligence: pc1Data.INT, cha: pc1Data.CHA,
            bab: pc1Data.BAB,
            armorBonus: pc1ArmorBonus,
            shieldBonus: pc1ShieldBonus,
            damageDice: pc1DamageDice,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: pc1Data.BaseSpeed,
            atkRange: 1,
            baseHitDieHP: pc1Data.HP,
            raceName: pc1Data.RaceName
        );

        Sprite pcAlive = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        Vector2Int pc1Start = new Vector2Int(3, 8);
        PC1.Init(pc1Stats, pc1Start, pcAlive, pcDead);

        var pc1Inv = PC1.gameObject.AddComponent<InventoryComponent>();
        pc1Inv.Init(pc1Stats);
        SetupStartingEquipment(pc1Inv, pc1Data.ClassName);

        // Initialize skills (skill allocation was done during character creation)
        pc1Stats.InitializeSkills(pc1Data.ClassName, 3);
        // Apply pre-allocated skill ranks from character creation
        if (pc1Data.SkillRanks != null)
        {
            foreach (var kvp in pc1Data.SkillRanks)
            {
                for (int i = 0; i < kvp.Value; i++)
                    pc1Stats.AddSkillRank(kvp.Key);
            }
        }

        // Apply feats from character creation
        FeatDefinitions.Init();
        if (pc1Data.SelectedFeats != null && pc1Data.SelectedFeats.Count > 0)
        {
            pc1Stats.AddFeats(pc1Data.SelectedFeats);
            Debug.Log($"[GameManager] {pc1Data.CharacterName} general feats: {string.Join(", ", pc1Data.SelectedFeats)}");
        }
        if (pc1Data.BonusFeats != null && pc1Data.BonusFeats.Count > 0)
        {
            pc1Stats.AddFeats(pc1Data.BonusFeats);
            Debug.Log($"[GameManager] {pc1Data.CharacterName} bonus feats: {string.Join(", ", pc1Data.BonusFeats)}");
        }
        if (!string.IsNullOrEmpty(pc1Data.WeaponFocusChoice))
            pc1Stats.WeaponFocusChoice = pc1Data.WeaponFocusChoice;
        if (!string.IsNullOrEmpty(pc1Data.SkillFocusChoice))
            pc1Stats.SkillFocusChoice = pc1Data.SkillFocusChoice;
        FeatManager.ApplyPassiveFeats(pc1Stats);

        Debug.Log($"[GameManager] {pc1Data.CharacterName} ({pc1Data.RaceName} {pc1Data.ClassName}): " +
                  $"STR {pc1Stats.STR} DEX {pc1Stats.DEX} CON {pc1Stats.CON} " +
                  $"HP {pc1Stats.MaxHP} AC {pc1Stats.ArmorClass} Atk {CharacterStats.FormatMod(pc1Stats.AttackBonus)} " +
                  $"Feats: {pc1Stats.Feats.Count}");

        // Initialize spellcasting if applicable
        if (pc1Stats.IsSpellcaster)
        {
            SpellDatabase.Init();
            var spellComp = PC1.gameObject.AddComponent<SpellcastingComponent>();
            spellComp.Init(pc1Stats);
            Debug.Log($"[GameManager] {pc1Data.CharacterName}: Spellcasting initialized - {spellComp.GetSlotSummary()}");
        }

        // ===== PC2 =====
        pc2Data.ComputeFinalStats();
        int pc2ArmorBonus, pc2ShieldBonus, pc2DamageDice;
        GetClassDefaults(pc2Data.ClassName, out pc2ArmorBonus, out pc2ShieldBonus, out pc2DamageDice);

        CharacterStats pc2Stats = new CharacterStats(
            name: pc2Data.CharacterName,
            level: 3,
            characterClass: pc2Data.ClassName,
            str: pc2Data.STR, dex: pc2Data.DEX, con: pc2Data.CON,
            wis: pc2Data.WIS, intelligence: pc2Data.INT, cha: pc2Data.CHA,
            bab: pc2Data.BAB,
            armorBonus: pc2ArmorBonus,
            shieldBonus: pc2ShieldBonus,
            damageDice: pc2DamageDice,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: pc2Data.BaseSpeed,
            atkRange: 1,
            baseHitDieHP: pc2Data.HP,
            raceName: pc2Data.RaceName
        );

        Vector2Int pc2Start = new Vector2Int(3, 12);
        PC2.Init(pc2Stats, pc2Start, pcAlive, pcDead);

        SpriteRenderer pc2SR = PC2.GetComponent<SpriteRenderer>();
        if (pc2SR != null)
            pc2SR.color = new Color(0.6f, 0.7f, 1f, 1f);

        var pc2Inv = PC2.gameObject.AddComponent<InventoryComponent>();
        pc2Inv.Init(pc2Stats);
        SetupStartingEquipment(pc2Inv, pc2Data.ClassName);

        // Initialize skills for PC2
        pc2Stats.InitializeSkills(pc2Data.ClassName, 3);
        if (pc2Data.SkillRanks != null)
        {
            foreach (var kvp in pc2Data.SkillRanks)
            {
                for (int i = 0; i < kvp.Value; i++)
                    pc2Stats.AddSkillRank(kvp.Key);
            }
        }

        // Apply feats from character creation
        if (pc2Data.SelectedFeats != null && pc2Data.SelectedFeats.Count > 0)
        {
            pc2Stats.AddFeats(pc2Data.SelectedFeats);
            Debug.Log($"[GameManager] {pc2Data.CharacterName} general feats: {string.Join(", ", pc2Data.SelectedFeats)}");
        }
        if (pc2Data.BonusFeats != null && pc2Data.BonusFeats.Count > 0)
        {
            pc2Stats.AddFeats(pc2Data.BonusFeats);
            Debug.Log($"[GameManager] {pc2Data.CharacterName} bonus feats: {string.Join(", ", pc2Data.BonusFeats)}");
        }
        if (!string.IsNullOrEmpty(pc2Data.WeaponFocusChoice))
            pc2Stats.WeaponFocusChoice = pc2Data.WeaponFocusChoice;
        if (!string.IsNullOrEmpty(pc2Data.SkillFocusChoice))
            pc2Stats.SkillFocusChoice = pc2Data.SkillFocusChoice;
        FeatManager.ApplyPassiveFeats(pc2Stats);

        Debug.Log($"[GameManager] {pc2Data.CharacterName} ({pc2Data.RaceName} {pc2Data.ClassName}): " +
                  $"STR {pc2Stats.STR} DEX {pc2Stats.DEX} CON {pc2Stats.CON} " +
                  $"HP {pc2Stats.MaxHP} AC {pc2Stats.ArmorClass} Atk {CharacterStats.FormatMod(pc2Stats.AttackBonus)} " +
                  $"Feats: {pc2Stats.Feats.Count}");

        // Initialize spellcasting if applicable
        if (pc2Stats.IsSpellcaster)
        {
            SpellDatabase.Init();
            var spellComp = PC2.gameObject.AddComponent<SpellcastingComponent>();
            spellComp.Init(pc2Stats);
            Debug.Log($"[GameManager] {pc2Data.CharacterName}: Spellcasting initialized - {spellComp.GetSlotSummary()}");
        }

        // ===== NPC =====
        CharacterStats npcStats = new CharacterStats(
            name: "Goblin Warchief",
            level: 2,
            characterClass: "Warrior",
            str: 14, dex: 15, con: 13, wis: 10, intelligence: 10, cha: 8,
            bab: 2,
            armorBonus: 3,
            shieldBonus: 1,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 3,
            atkRange: 1,
            baseHitDieHP: 12
        );
        npcStats.CreatureTags.Add("Goblinoid");

        Sprite npcAlive = LoadSprite("Sprites/npc_enemy_alive");
        Sprite npcDead = LoadSprite("Sprites/npc_enemy_dead");

        Vector2Int npcStart = new Vector2Int(16, 10);
        NPC.Init(npcStats, npcStart, npcAlive, npcDead);
    }

    /// <summary>
    /// Get default armor bonus, shield bonus, and damage dice for a class.
    /// Used when setting up characters from character creation data.
    /// </summary>
    private void GetClassDefaults(string className, out int armorBonus, out int shieldBonus, out int damageDice)
    {
        switch (className)
        {
            case "Fighter":
                armorBonus = 4; shieldBonus = 2; damageDice = 8; break;
            case "Rogue":
                armorBonus = 2; shieldBonus = 0; damageDice = 6; break;
            case "Monk":
                armorBonus = 0; shieldBonus = 0; damageDice = 6; break; // Monk: unarmored, unarmed 1d6
            case "Barbarian":
                armorBonus = 3; shieldBonus = 0; damageDice = 12; break; // Barbarian: hide armor, greataxe 1d12
            case "Wizard":
                armorBonus = 0; shieldBonus = 0; damageDice = 6; break;  // Wizard: no armor, quarterstaff 1d6
            case "Cleric":
                armorBonus = 4; shieldBonus = 2; damageDice = 8; break;  // Cleric: chain shirt+shield, heavy mace 1d8
            default:
                armorBonus = 0; shieldBonus = 0; damageDice = 6; break;
        }
    }

    /// <summary>
    /// Set up starting equipment based on class (PHB starting packages).
    /// </summary>
    private void SetupStartingEquipment(InventoryComponent inv, string className)
    {
        ItemDatabase.Init();

        if (className == "Fighter")
        {
            // Fighter Starting Package: Scale Mail, Heavy Wooden Shield, Longsword, Shortbow
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("scale_mail"), EquipSlot.Armor);
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("longsword"), EquipSlot.RightHand);
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_wooden"), EquipSlot.LeftHand);

            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("shortbow"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("torch"));
            Debug.Log("[GameManager] Fighter equipment: Scale Mail, Heavy Shield, Longsword, Shortbow");
        }
        else if (className == "Rogue")
        {
            // Rogue Starting Package: Leather Armor, Rapier, Shortbow
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("leather_armor"), EquipSlot.Armor);
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("rapier"), EquipSlot.RightHand);

            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("shortbow"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("dagger"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("rope"));
            Debug.Log("[GameManager] Rogue equipment: Leather Armor, Rapier, Shortbow, Dagger");
        }
        else if (className == "Monk")
        {
            // Monk Starting Package: No armor (AC from WIS), Quarterstaff, Sling
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);

            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("sling"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            Debug.Log("[GameManager] Monk equipment: Quarterstaff, Sling (unarmored for WIS AC bonus)");
        }
        else if (className == "Barbarian")
        {
            // Barbarian Starting Package: Hide Armor, Greataxe, Javelins
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("hide_armor"), EquipSlot.Armor);
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("greataxe"), EquipSlot.RightHand);

            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("javelin"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("javelin"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("javelin"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            Debug.Log("[GameManager] Barbarian equipment: Hide Armor, Greataxe, 3x Javelin");
        }
        else if (className == "Wizard")
        {
            // Wizard Starting Package: No armor, Quarterstaff, Light Crossbow
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("quarterstaff"), EquipSlot.RightHand);

            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("crossbow_light"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("dagger"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            Debug.Log("[GameManager] Wizard equipment: Quarterstaff, Light Crossbow, Dagger (no armor)");
        }
        else if (className == "Cleric")
        {
            // Cleric Starting Package: Chain Shirt, Heavy Shield, Heavy Mace
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("chain_shirt"), EquipSlot.Armor);
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("mace_heavy"), EquipSlot.RightHand);
            inv.CharacterInventory.DirectEquip(ItemDatabase.CloneItem("shield_heavy_wooden"), EquipSlot.LeftHand);

            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("crossbow_light"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            inv.CharacterInventory.AddItem(ItemDatabase.CloneItem("potion_healing"));
            Debug.Log("[GameManager] Cleric equipment: Chain Shirt, Heavy Shield, Heavy Mace, Light Crossbow");
        }

        inv.CharacterInventory.RecalculateStats();
    }

    /// <summary>
    /// Handle input every frame - inventory toggle and cell clicks.
    /// </summary>
    private void Update()
    {
        // Skip all game input during character creation
        if (WaitingForCharacterCreation) return;

        HandleInventoryInput();
        HandleSkillsInput();

        if (!IsPlayerTurn) return;
        if (CurrentSubPhase == PlayerSubPhase.Animating) return;
        if (_waitingForAoOConfirmation) return; // Block input during AoO confirmation dialog
        if (InventoryUI != null && InventoryUI.IsOpen) return;
        if (SkillsUI != null && SkillsUI.IsOpen)
        {
            // Debug: log only once per open state to avoid spam
            return;
        }

        bool clicked = false;
        Vector3 mouseScreenPos = Vector3.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            clicked = true;
            mouseScreenPos = Input.mousePosition;
        }
#endif

#if ENABLE_INPUT_SYSTEM
        if (!clicked)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                clicked = true;
                mouseScreenPos = mouse.position.ReadValue();
            }
        }
#endif

        if (!clicked || _mainCam == null) return;

        // Check if pointer is over a UI element (buttons, panels, etc.)
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("[Grid] Click blocked by UI element (IsPointerOverGameObject)");
            return;
        }

        Vector2 worldPoint = _mainCam.ScreenToWorldPoint(mouseScreenPos);
        RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

        if (hit.collider != null)
        {
            SquareCell cell = hit.collider.GetComponent<SquareCell>();
            if (cell != null)
            {
                Debug.Log($"[Grid] Raycast hit cell at ({cell.X}, {cell.Y}) Phase={CurrentPhase} Sub={CurrentSubPhase}");
                OnCellClicked(cell);
            }
        }
        else
        {
            Debug.Log("[Grid] Click detected but no cell hit by raycast");
        }
    }

    private void HandleInventoryInput()
    {
        bool iPressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.I))
            iPressed = true;
#endif

#if ENABLE_INPUT_SYSTEM
        if (!iPressed)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
                iPressed = true;
        }
#endif

        if (iPressed && InventoryUI != null)
        {
            if (InventoryUI.IsOpen)
            {
                InventoryUI.Close();
                // Refresh action buttons after inventory changes (might affect dual wield)
                if (IsPlayerTurn && ActivePC != null && CurrentSubPhase == PlayerSubPhase.ChoosingAction)
                    ShowActionChoices();
            }
            else if (IsPlayerTurn && ActivePC != null)
            {
                InventoryUI.Toggle(ActivePC);
            }
        }
    }

    private void HandleSkillsInput()
    {
        bool kPressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.K))
            kPressed = true;
#endif

#if ENABLE_INPUT_SYSTEM
        if (!kPressed)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.kKey.wasPressedThisFrame)
                kPressed = true;
        }
#endif

        if (kPressed && SkillsUI != null)
        {
            if (SkillsUI.IsOpen)
            {
                Debug.Log("[UI] K pressed - closing Skills panel");
                SkillsUI.Close();
            }
            else if (IsPlayerTurn && ActivePC != null)
            {
                Debug.Log("[UI] K pressed - opening Skills panel");
                SkillsUI.OpenForDisplay(ActivePC.Stats);
            }
        }
    }

    private void CloseInventoryIfOpen()
    {
        if (InventoryUI != null && InventoryUI.IsOpen)
            InventoryUI.Close();
    }

    private void SetupCharacters()
    {
        // Initialize race database
        RaceDatabase.Init();

        // ==========================================
        // PC1: "Aldric" - Dwarf Fighter (Level 3)
        // Base scores: STR 16, DEX 12, CON 14, WIS 10, INT 10, CHA 13
        // Dwarf racial: CON +2 = 16, CHA -2 = 11
        // Dwarf speed: 20 ft (4 squares), NOT reduced by armor
        // ==========================================
        CharacterStats pc1Stats = new CharacterStats(
            name: "Aldric",
            level: 3,
            characterClass: "Fighter",
            str: 16, dex: 12, con: 14, wis: 10, intelligence: 10, cha: 13,
            bab: 3,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 4,  // overridden by race
            atkRange: 1,
            baseHitDieHP: 22,
            raceName: "Dwarf"
        );

        Debug.Log($"[GameManager] Aldric (Dwarf Fighter): STR {pc1Stats.STR} DEX {pc1Stats.DEX} CON {pc1Stats.CON} " +
                  $"WIS {pc1Stats.WIS} INT {pc1Stats.INT} CHA {pc1Stats.CHA} | " +
                  $"HP {pc1Stats.MaxHP} | Speed {pc1Stats.MoveRange} squares ({pc1Stats.SpeedInFeet} ft)");

        Sprite pcAlive = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        Vector2Int pc1Start = new Vector2Int(3, 8);
        PC1.Init(pc1Stats, pc1Start, pcAlive, pcDead);

        var pc1Inv = PC1.gameObject.AddComponent<InventoryComponent>();
        pc1Inv.Init(pc1Stats);
        pc1Inv.SetupAldric();

        // Initialize skills for Aldric (Fighter)
        pc1Stats.InitializeSkills("Fighter", 3);
        // Default skill allocation: Climb 4, Intimidate 4, Jump 3, Swim 3
        for (int i = 0; i < 4; i++) pc1Stats.AddSkillRank("Climb");
        for (int i = 0; i < 4; i++) pc1Stats.AddSkillRank("Intimidate");
        for (int i = 0; i < 3; i++) pc1Stats.AddSkillRank("Jump");
        for (int i = 0; i < 3; i++) pc1Stats.AddSkillRank("Swim");

        // ==========================================
        // PC2: "Lyra" - Elf Rogue (Level 3)
        // Base scores: STR 12, DEX 17, CON 12, WIS 13, INT 14, CHA 10
        // Elf racial: DEX +2 = 19, CON -2 = 10
        // Elf speed: 30 ft (6 squares)
        // Elf weapon proficiencies: longsword, rapier, longbow, shortbow
        // ==========================================
        CharacterStats pc2Stats = new CharacterStats(
            name: "Lyra",
            level: 3,
            characterClass: "Rogue",
            str: 12, dex: 17, con: 12, wis: 13, intelligence: 14, cha: 10,
            bab: 2,
            armorBonus: 2,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 5,  // overridden by race
            atkRange: 1,
            baseHitDieHP: 15,
            raceName: "Elf"
        );

        Debug.Log($"[GameManager] Lyra (Elf Rogue): STR {pc2Stats.STR} DEX {pc2Stats.DEX} CON {pc2Stats.CON} " +
                  $"WIS {pc2Stats.WIS} INT {pc2Stats.INT} CHA {pc2Stats.CHA} | " +
                  $"HP {pc2Stats.MaxHP} | Speed {pc2Stats.MoveRange} squares ({pc2Stats.SpeedInFeet} ft)");

        Vector2Int pc2Start = new Vector2Int(3, 12);
        PC2.Init(pc2Stats, pc2Start, pcAlive, pcDead);

        var pc2Inv = PC2.gameObject.AddComponent<InventoryComponent>();
        pc2Inv.Init(pc2Stats);
        pc2Inv.SetupLyra();

        // Initialize skills for Lyra (Rogue) - INT 14 (+2), so (8+2)×4 = 40 at level 1, +10×2 = 60 total
        pc2Stats.InitializeSkills("Rogue", 3);
        // Default skill allocation for Lyra
        for (int i = 0; i < 6; i++) pc2Stats.AddSkillRank("Hide");
        for (int i = 0; i < 6; i++) pc2Stats.AddSkillRank("Move Silently");
        for (int i = 0; i < 6; i++) pc2Stats.AddSkillRank("Spot");
        for (int i = 0; i < 6; i++) pc2Stats.AddSkillRank("Listen");
        for (int i = 0; i < 5; i++) pc2Stats.AddSkillRank("Disable Device");
        for (int i = 0; i < 5; i++) pc2Stats.AddSkillRank("Open Lock");
        for (int i = 0; i < 5; i++) pc2Stats.AddSkillRank("Search");
        for (int i = 0; i < 4; i++) pc2Stats.AddSkillRank("Tumble");
        for (int i = 0; i < 4; i++) pc2Stats.AddSkillRank("Bluff");
        for (int i = 0; i < 4; i++) pc2Stats.AddSkillRank("Diplomacy");
        for (int i = 0; i < 4; i++) pc2Stats.AddSkillRank("Climb");
        for (int i = 0; i < 3; i++) pc2Stats.AddSkillRank("Balance");
        for (int i = 0; i < 2; i++) pc2Stats.AddSkillRank("Sleight of Hand");

        SpriteRenderer pc2SR = PC2.GetComponent<SpriteRenderer>();
        if (pc2SR != null)
            pc2SR.color = new Color(0.6f, 0.7f, 1f, 1f);

        // ==========================================
        // NPC: "Goblin Warchief" - Goblinoid creature
        // Goblinoid tag: triggers Dwarf's +1 racial attack bonus
        // ==========================================
        CharacterStats npcStats = new CharacterStats(
            name: "Goblin Warchief",
            level: 2,
            characterClass: "Warrior",
            str: 14, dex: 15, con: 13, wis: 10, intelligence: 10, cha: 8,
            bab: 2,
            armorBonus: 3,
            shieldBonus: 1,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 3,
            atkRange: 1,
            baseHitDieHP: 12
        );
        // Tag the goblin as a Goblinoid for racial attack bonus purposes
        npcStats.CreatureTags.Add("Goblinoid");

        Sprite npcAlive = LoadSprite("Sprites/npc_enemy_alive");
        Sprite npcDead = LoadSprite("Sprites/npc_enemy_dead");

        Vector2Int npcStart = new Vector2Int(16, 10);
        NPC.Init(npcStats, npcStart, npcAlive, npcDead);

        CombatUI.UpdateAllStats(PC1, PC2, NPC);
    }

    private Sprite LoadSprite(string path)
    {
        Sprite s = Resources.Load<Sprite>(path);
        if (s == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 64f);
            }
        }
        return s;
    }

    private void CenterCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 center = Grid.GetGridCenter();
            cam.transform.position = new Vector3(center.x, center.y, -10f);
            cam.orthographicSize = 10f;
        }
    }

    // ========== TURN MANAGEMENT WITH ACTION ECONOMY ==========

    /// <summary>
    /// Begin a PC's turn with full action economy.
    /// </summary>
    public void StartPCTurn(CharacterController pc)
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        CloseInventoryIfOpen();

        // If this PC is dead, advance
        if (pc.Stats.IsDead)
        {
            if (pc == PC1) { StartPCTurn(PC2); return; }
            if (pc == PC2) { StartCoroutine(NPCTurnCoroutine()); return; }
        }

        // Tick Barbarian Rage at start of turn
        if (pc.Stats.IsBarbarian && pc.Stats.IsRaging)
        {
            pc.Stats.TickRage();
            if (!pc.Stats.IsRaging)
            {
                CombatUI.ShowCombatLog($"😫 {pc.Stats.CharacterName}'s rage has ended! Now fatigued.");
                CombatUI.UpdateAllStats(PC1, PC2, NPC);
            }
            else
            {
                CombatUI.ShowCombatLog($"⚡ {pc.Stats.CharacterName}: Rage - {pc.Stats.RageRoundsRemaining} rounds remaining");
            }
        }

        pc.StartNewTurn();

        CurrentPhase = (pc == PC1) ? TurnPhase.PC1Turn : TurnPhase.PC2Turn;
        CurrentSubPhase = PlayerSubPhase.ChoosingAction;

        string pcLabel = (pc == PC1) ? "Hero 1" : "Hero 2";
        CombatUI.SetActivePC(pc == PC1 ? 1 : 2);

        ShowActionChoices();
    }

    // Legacy helper
    public void StartPlayerTurn() => StartPCTurn(PC1);

    /// <summary>
    /// Show the action choice UI for the current PC.
    /// </summary>
    private void ShowActionChoices()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        CurrentSubPhase = PlayerSubPhase.ChoosingAction;

        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        // Highlight current position
        SquareCell current = Grid.GetCell(pc.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);

        // Update action buttons based on action economy
        CombatUI.SetActionButtonsVisible(true);
        CombatUI.UpdateActionButtons(pc);

        // Update feat controls (Power Attack slider, Rapid Shot toggle)
        CombatUI.UpdateFeatControls(pc);

        // Build status message with feat info
        string pcName = pc.Stats.CharacterName;
        string actionInfo = pc.Actions.GetStatusString();

        // Check if dual wield is possible and add info
        string dwInfo = "";
        if (pc.CanDualWield())
            dwInfo = "\n" + pc.GetDualWieldDescription();

        // Show active feats
        string featInfo = "";
        if (pc.Stats.Feats.Count > 0)
            featInfo = $"\nFeats: {string.Join(", ", pc.Stats.Feats)}";

        // Show spell info for spellcasters
        string spellInfo = "";
        if (pc.Stats.IsSpellcaster)
        {
            var spellComp = pc.GetComponent<SpellcastingComponent>();
            if (spellComp != null)
                spellInfo = $"\n✦ Spells: {spellComp.GetSlotSummary()}";
        }

        CombatUI.SetTurnIndicator($"{pcName}'s Turn - Choose an action  [I] Inventory  [K] Skills\n{actionInfo}{dwInfo}{featInfo}{spellInfo}");

        // Auto-end turn if no actions left
        if (!pc.Actions.HasAnyActionLeft)
        {
            CombatUI.SetTurnIndicator($"{pcName}'s Turn - No actions remaining");
            StartCoroutine(DelayedEndActivePCTurn(1.0f));
        }
    }

    // ========== ACTION BUTTON HANDLERS ==========

    /// <summary>Called when Move button is pressed.</summary>
    public void OnMoveButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;

        // Determine if this uses move action or converted standard
        if (pc.Actions.HasMoveAction)
        {
            // Normal move action
        }
        else if (pc.Actions.CanConvertStandardToMove)
        {
            // Will convert standard to move when actually moving
        }
        else
        {
            return; // Can't move
        }

        CurrentSubPhase = PlayerSubPhase.Moving;
        ShowMovementRange(pc);
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Click a tile to move (or own tile to cancel)");
    }

    /// <summary>Called when Attack (Standard Action) button is pressed.</summary>
    public void OnAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasStandardAction) return;

        _pendingAttackMode = PendingAttackMode.Single;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
    }

    /// <summary>Called when Full Attack button is pressed.</summary>
    public void OnFullAttackButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasFullRoundAction) return;

        _pendingAttackMode = PendingAttackMode.FullAttack;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
        ShowAttackTargets(pc);
    }

    /// <summary>Called when Dual Wield button is pressed.</summary>
    public void OnDualWieldButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Actions.HasFullRoundAction || !pc.CanDualWield()) return;

        _pendingAttackMode = PendingAttackMode.DualWield;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;

        var (mainPen, offPen, lightOff) = pc.GetDualWieldPenalties();
        string penaltyInfo = lightOff ? $"(light off-hand: {mainPen}/{offPen})" : $"(penalties: {mainPen}/{offPen})";

        ShowAttackTargets(pc);
        CombatUI.SetTurnIndicator($"DUAL WIELD: Select target {penaltyInfo}");
    }

    /// <summary>Called when End Turn button is pressed.</summary>
    public void OnEndTurnButtonPressed()
    {
        if (!IsPlayerTurn) return;
        EndActivePCTurn();
    }

    /// <summary>Called when Power Attack slider value changes.</summary>
    public void OnPowerAttackSliderChanged(float value)
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;
        pc.SetPowerAttack((int)value);
        CombatUI.UpdatePowerAttackLabel(pc);
    }

    /// <summary>Called when Rapid Shot toggle button is pressed.</summary>
    public void OnRapidShotTogglePressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null) return;
        bool oldValue = pc.RapidShotEnabled;
        pc.SetRapidShot(!pc.RapidShotEnabled);
        Debug.Log($"[RapidShot] Rapid Shot toggle clicked, new value: {pc.RapidShotEnabled} (was {oldValue}) for {pc.Stats.CharacterName}");
        CombatUI.UpdateRapidShotLabel(pc);
        // Refresh action buttons so Full Attack label reflects Rapid Shot state + weapon type
        CombatUI.UpdateActionButtons(pc);
    }

    /// <summary>Called when Flurry of Blows button is pressed (Monk only, Full-Round Action).</summary>
    public void OnFlurryOfBlowsButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Stats.IsMonk || !pc.Actions.HasFullRoundAction) return;

        _pendingAttackMode = PendingAttackMode.FlurryOfBlows;
        CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;

        int[] bonuses = pc.Stats.GetFlurryOfBlowsBonuses();
        string bonusStr = string.Join("/", System.Array.ConvertAll(bonuses, b => CharacterStats.FormatMod(b)));
        Debug.Log($"[Monk] {pc.Stats.CharacterName}: Flurry of Blows selected - {bonuses.Length} attacks at {bonusStr}");

        ShowAttackTargets(pc);
        CombatUI.SetTurnIndicator($"FLURRY OF BLOWS: Select target ({bonusStr})");
    }

    /// <summary>Called when Rage button is pressed (Barbarian only, Free Action).</summary>
    public void OnRageButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Stats.IsBarbarian) return;

        bool success = pc.ActivateRage();
        if (success)
        {
            CombatUI.ShowCombatLog($"⚡ {pc.Stats.CharacterName} enters a BARBARIAN RAGE! " +
                                  $"+4 STR, +4 CON, +2 Will, -2 AC for {pc.Stats.RageRoundsRemaining} rounds!");
            CombatUI.UpdateAllStats(PC1, PC2, NPC);
            CombatUI.UpdateActionButtons(pc);
            Debug.Log($"[GameManager] {pc.Stats.CharacterName} activated Rage via button");
        }
        else
        {
            string reason = pc.Stats.IsRaging ? "already raging" :
                           pc.Stats.IsFatigued ? "fatigued" : "no rages left today";
            CombatUI.ShowCombatLog($"{pc.Stats.CharacterName} cannot rage: {reason}");
            Debug.Log($"[GameManager] {pc.Stats.CharacterName} failed to activate Rage: {reason}");
        }
    }

    // ========== SPELLCASTING ==========

    /// <summary>Called when Cast Spell button is pressed (Standard Action, spellcasters only).</summary>
    public void OnCastSpellButtonPressed()
    {
        CharacterController pc = ActivePC;
        if (pc == null || !pc.Stats.IsSpellcaster || !pc.Actions.HasStandardAction) return;

        var spellComp = pc.GetComponent<SpellcastingComponent>();
        if (spellComp == null || !spellComp.HasAnyCastableSpell()) return;

        // Show spell selection panel
        CombatUI.SetActionButtonsVisible(false);
        CombatUI.ShowSpellSelection(spellComp, OnSpellSelected, OnSpellSelectionCancelled);
    }

    /// <summary>Called when a spell is chosen from the spell selection panel.</summary>
    private void OnSpellSelected(SpellData spell)
    {
        CharacterController pc = ActivePC;
        if (pc == null) { ShowActionChoices(); return; }

        _pendingSpell = spell;

        // Determine targeting based on spell type
        if (spell.TargetType == SpellTargetType.Self)
        {
            // Self-targeting spells (Mage Armor) cast immediately
            PerformSpellCast(pc, pc);
        }
        else if (spell.TargetType == SpellTargetType.SingleAlly)
        {
            // Ally targeting (Cure Light Wounds) - show ally targets
            _pendingAttackMode = PendingAttackMode.CastSpell;
            CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
            ShowSpellTargets(pc, spell);
        }
        else if (spell.TargetType == SpellTargetType.SingleEnemy)
        {
            // Enemy targeting (damage spells) - show enemy targets in range
            _pendingAttackMode = PendingAttackMode.CastSpell;
            CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
            ShowSpellTargets(pc, spell);
        }
        else
        {
            // Default: show targets
            _pendingAttackMode = PendingAttackMode.CastSpell;
            CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
            ShowSpellTargets(pc, spell);
        }
    }

    /// <summary>Called when spell selection is cancelled.</summary>
    private void OnSpellSelectionCancelled()
    {
        ShowActionChoices();
    }

    /// <summary>
    /// Highlight valid targets for a spell based on its range and target type.
    /// </summary>
    private void ShowSpellTargets(CharacterController caster, SpellData spell)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        int range = spell.RangeSquares;
        if (range <= 0) range = 1; // Touch spells = adjacent (1 square)

        List<SquareCell> allCells = Grid.GetCellsInRange(caster.GridPosition, range);
        bool hasTarget = false;

        foreach (var cell in allCells)
        {
            if (!cell.IsOccupied || cell.Occupant.Stats.IsDead) continue;
            if (cell.Occupant == caster) continue; // Can't target self here (self spells are handled separately)

            int sqDist = SquareGridUtils.GetDistance(caster.GridPosition, cell.Coords);
            if (sqDist > range) continue;

            bool validTarget = false;
            if (spell.TargetType == SpellTargetType.SingleEnemy && cell.Occupant != caster)
            {
                // For enemies: NPC is enemy to PCs, PCs are enemies to NPC
                bool isEnemy = (caster == PC1 || caster == PC2) ? (cell.Occupant == NPC) : (cell.Occupant == PC1 || cell.Occupant == PC2);
                validTarget = isEnemy;
            }
            else if (spell.TargetType == SpellTargetType.SingleAlly)
            {
                // For allies: PCs are allies to each other
                bool isAlly = (caster == PC1 && cell.Occupant == PC2) || (caster == PC2 && cell.Occupant == PC1);
                // Also allow targeting self for healing (cell won't match since we skipped self above)
                validTarget = isAlly;
            }

            if (validTarget)
            {
                if (spell.EffectType == SpellEffectType.Healing || spell.EffectType == SpellEffectType.Buff)
                    cell.SetHighlight(HighlightType.Move); // Green for friendly spells
                else
                    cell.SetHighlight(HighlightType.Attack); // Red for damage spells
                _highlightedCells.Add(cell);
                hasTarget = true;
            }
        }

        // For SingleAlly spells, also allow self-targeting by clicking own tile
        if (spell.TargetType == SpellTargetType.SingleAlly)
        {
            SquareCell selfCell = Grid.GetCell(caster.GridPosition);
            if (selfCell != null)
            {
                selfCell.SetHighlight(HighlightType.Selected);
                _highlightedCells.Add(selfCell);
                hasTarget = true;
            }
        }

        if (hasTarget)
        {
            string targetMsg = spell.TargetType == SpellTargetType.SingleAlly
                ? "Click an ally (or self) to cast"
                : "Click an enemy to cast";
            CombatUI.SetTurnIndicator($"✦ {spell.Name}: {targetMsg} (Range: {spell.RangeSquares} sq)");
        }
        else
        {
            CombatUI.SetTurnIndicator($"No valid targets for {spell.Name}!");
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.5f));
        }
    }

    /// <summary>
    /// Execute a spell cast from caster to target.
    /// </summary>
    private void PerformSpellCast(CharacterController caster, CharacterController target)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;

        // Consume standard action
        caster.Actions.UseStandardAction();

        // Get spellcasting component
        var spellComp = caster.GetComponent<SpellcastingComponent>();
        if (spellComp == null)
        {
            Debug.LogError("[GameManager] PerformSpellCast: No SpellcastingComponent!");
            ShowActionChoices();
            return;
        }

        // Consume spell slot (cantrips are free)
        if (_pendingSpell.SpellLevel > 0)
        {
            if (!spellComp.ConsumeSlot(_pendingSpell.SpellLevel))
            {
                Debug.LogError($"[GameManager] Failed to consume level {_pendingSpell.SpellLevel} spell slot!");
                ShowActionChoices();
                return;
            }
        }

        // Resolve the spell
        SpellResult result = SpellCaster.Cast(_pendingSpell, caster.Stats, target.Stats);

        // Apply Mage Armor AC bonus
        if (_pendingSpell.SpellId == "mage_armor" && result.Success)
        {
            target.Stats.SpellACBonus = _pendingSpell.BuffACBonus;
            spellComp.MageArmorActive = true;
            spellComp.MageArmorACBonus = _pendingSpell.BuffACBonus;
            Debug.Log($"[GameManager] Mage Armor applied: +{_pendingSpell.BuffACBonus} AC to {target.Stats.CharacterName}");
        }

        // Handle death if target was killed
        if (result.TargetKilled && target != null)
        {
            target.OnDeath();
        }

        // Build combat log
        _lastCombatLog = result.GetFormattedLog();

        if (GameManager.LogAttacksToConsole)
            Debug.Log("[Spell] " + _lastCombatLog);

        CombatUI.ShowCombatLog(_lastCombatLog);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);

        Grid.ClearAllHighlights();

        // Check for NPC kill
        if (result.TargetKilled && target == NPC)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! Enemy defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        _pendingSpell = null;

        // After standard action, check for remaining actions
        StartCoroutine(AfterAttackDelay(caster, 1.5f));
    }

    // ========== MOVEMENT ==========

    // AoO confirmation state
    private bool _waitingForAoOConfirmation;
    private AoOPathResult _pendingAoOPath;
    private SquareCell _pendingMoveTarget;

    private void ShowMovementRange(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();

        List<SquareCell> moveCells = Grid.GetCellsInRange(pc.GridPosition, pc.Stats.MoveRange);
        foreach (var cell in moveCells)
        {
            if (!cell.IsOccupied)
            {
                cell.SetHighlight(HighlightType.Move);
                _highlightedCells.Add(cell);
            }
        }

        SquareCell current = Grid.GetCell(pc.GridPosition);
        if (current != null)
            current.SetHighlight(HighlightType.Selected);
    }

    /// <summary>
    /// Get all characters in combat for AoO threat calculations.
    /// </summary>
    private List<CharacterController> GetAllCharacters()
    {
        var all = new List<CharacterController>();
        if (PC1 != null) all.Add(PC1);
        if (PC2 != null) all.Add(PC2);
        if (NPC != null) all.Add(NPC);
        return all;
    }

    // ========== ATTACK TARGET SELECTION ==========

    private void ShowAttackTargets(CharacterController pc)
    {
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        CharacterController otherPC = (pc == PC1) ? PC2 : PC1;

        // Determine the equipped weapon's range increment
        ItemData weapon = pc.GetEquippedMainWeapon();
        int rangeIncrement = (weapon != null) ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = (weapon != null) && weapon.IsThrown;
        bool isRangedWeapon = (weapon != null && weapon.WeaponCat == WeaponCategory.Ranged) || rangeIncrement > 0;

        // Calculate effective attack range in squares
        int maxRangeSquares;
        if (isRangedWeapon && rangeIncrement > 0)
        {
            maxRangeSquares = RangeCalculator.GetMaxRangeSquares(rangeIncrement, isThrownWeapon);
        }
        else
        {
            // Melee weapon: use AttackRange from stats (typically 1, or 2 for reach)
            maxRangeSquares = pc.Stats.AttackRange;
        }

        // Show range zone highlights for ranged weapons
        if (isRangedWeapon && rangeIncrement > 0)
        {
            ShowRangeZoneHighlights(pc, rangeIncrement, maxRangeSquares, isThrownWeapon);
        }

        // Find all valid targets within range
        List<SquareCell> allCells = Grid.GetCellsInRange(pc.GridPosition, maxRangeSquares);
        bool hasTarget = false;
        bool anyFlanking = false;

        foreach (var cell in allCells)
        {
            if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead)
            {
                int sqDist = SquareGridUtils.GetDistance(pc.GridPosition, cell.Coords);

                // Check if within max range
                if (isRangedWeapon && rangeIncrement > 0)
                {
                    int distFeet = RangeCalculator.SquaresToFeet(sqDist);
                    if (!RangeCalculator.IsWithinMaxRange(distFeet, rangeIncrement, isThrownWeapon))
                        continue; // Beyond max range, skip
                }
                else
                {
                    if (sqDist > pc.Stats.AttackRange)
                        continue;
                }

                bool flanking = !otherPC.Stats.IsDead &&
                    CombatUtils.IsFlanking(pc.GridPosition, otherPC.GridPosition, cell.Occupant.GridPosition);

                if (flanking)
                {
                    cell.SetHighlight(HighlightType.Flanking);
                    anyFlanking = true;
                }
                else
                {
                    cell.SetHighlight(HighlightType.Attack);
                }
                _highlightedCells.Add(cell);
                hasTarget = true;
            }
        }

        if (hasTarget)
        {
            string flankMsg = anyFlanking ? " (FLANKING available! +2 to hit)" : "";
            string modeStr = "";
            switch (_pendingAttackMode)
            {
                case PendingAttackMode.Single: modeStr = "ATTACK"; break;
                case PendingAttackMode.FullAttack: modeStr = "FULL ATTACK"; break;
                case PendingAttackMode.DualWield: modeStr = "DUAL WIELD"; break;
                case PendingAttackMode.FlurryOfBlows: modeStr = "FLURRY OF BLOWS"; break;
                case PendingAttackMode.CastSpell: modeStr = "CAST SPELL"; break;
            }

            // Build range info string
            string rangeMsg = "";
            if (isRangedWeapon && rangeIncrement > 0)
            {
                int incSquares = RangeCalculator.GetRangeIncrementSquares(rangeIncrement);
                int maxRange = RangeCalculator.GetMaxRangeFeet(rangeIncrement, isThrownWeapon);
                rangeMsg = $"\n{weapon.Name}: {rangeIncrement} ft increment ({incSquares} sq), max {maxRange} ft";
            }

            if (CombatUI.TurnIndicatorText != null && !CombatUI.TurnIndicatorText.text.Contains("DUAL WIELD"))
                CombatUI.SetTurnIndicator($"{modeStr}: Click an enemy to attack!{flankMsg}{rangeMsg}");
        }
        else
        {
            string noRangeMsg = isRangedWeapon ? "No enemies within maximum range!" : "No enemies in range!";
            CombatUI.SetTurnIndicator(noRangeMsg);
            StartCoroutine(ReturnToActionChoicesAfterDelay(1.5f));
        }
    }

    /// <summary>
    /// Show range zone highlights on the grid for a ranged weapon.
    /// Green = 1st increment (no penalty), Yellow = moderate, Orange = far.
    /// Thrown weapons: max 5 increments. Projectile weapons: max 10 increments.
    /// </summary>
    private void ShowRangeZoneHighlights(CharacterController pc, int rangeIncrement, int maxRangeSquares, bool isThrownWeapon = false)
    {
        List<SquareCell> allCells = Grid.GetCellsInRange(pc.GridPosition, maxRangeSquares);
        foreach (var cell in allCells)
        {
            if (cell.Coords == pc.GridPosition) continue;

            int sqDist = SquareGridUtils.GetDistance(pc.GridPosition, cell.Coords);
            int zone = RangeCalculator.GetRangeZone(sqDist, rangeIncrement, isThrownWeapon);

            switch (zone)
            {
                case 1: cell.SetHighlight(HighlightType.RangeClose); break;
                case 2: cell.SetHighlight(HighlightType.RangeMedium); break;
                case 3: cell.SetHighlight(HighlightType.RangeFar); break;
                // zone 0 = out of range, don't highlight
            }
        }
    }

    private IEnumerator ReturnToActionChoicesAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsPlayerTurn)
            ShowActionChoices();
    }

    // ========== CELL CLICK HANDLING ==========

    public void OnCellClicked(SquareCell cell)
    {
        if (CurrentPhase == TurnPhase.CombatOver) return;

        CharacterController pc = ActivePC;
        if (pc == null) return;

        switch (CurrentSubPhase)
        {
            case PlayerSubPhase.Moving:
                HandleMovementClick(pc, cell);
                break;

            case PlayerSubPhase.SelectingAttackTarget:
                HandleAttackTargetClick(pc, cell);
                break;

            case PlayerSubPhase.ChoosingAction:
                // Clicking on own tile when choosing does nothing special
                break;
        }
    }

    private void HandleMovementClick(CharacterController pc, SquareCell cell)
    {
        // If waiting for AoO confirmation, ignore clicks on the grid
        if (_waitingForAoOConfirmation) return;

        if (cell.Coords == pc.GridPosition)
        {
            // Click own tile to cancel movement and return to action choices
            ShowActionChoices();
            return;
        }

        if (_highlightedCells.Contains(cell) && !cell.IsOccupied)
        {
            // Check for AoOs before executing movement
            var allCharacters = GetAllCharacters();
            var pathResult = Grid.FindSafePath(pc.GridPosition, cell.Coords, pc, allCharacters);

            if (pathResult.ProvokesAoOs)
            {
                // Movement would provoke AoOs - show warning and wait for confirmation
                Debug.Log($"[GameManager] Movement to ({cell.Coords.x},{cell.Coords.y}) would provoke {pathResult.ProvokedAoOs.Count} AoO(s)!");
                _pendingAoOPath = pathResult;
                _pendingMoveTarget = cell;
                _waitingForAoOConfirmation = true;

                // Show AoO warning dialog
                var enemyNames = pathResult.GetThreateningEnemyNames();
                CombatUI.ShowAoOWarning(enemyNames, OnAoOConfirmed, OnAoOCancelled);
            }
            else
            {
                // No AoOs - execute movement immediately
                ExecuteMovement(pc, cell);
            }
        }
    }

    /// <summary>
    /// Called when player confirms movement despite AoO warning.
    /// </summary>
    private void OnAoOConfirmed()
    {
        _waitingForAoOConfirmation = false;

        CharacterController pc = ActivePC;
        if (pc == null || _pendingMoveTarget == null)
        {
            ShowActionChoices();
            return;
        }

        Debug.Log($"[GameManager] Player confirmed movement - resolving AoOs");

        // Resolve each AoO
        if (_pendingAoOPath != null && _pendingAoOPath.ProvokesAoOs)
        {
            StartCoroutine(ResolveAoOsAndMove(pc, _pendingMoveTarget, _pendingAoOPath));
        }
        else
        {
            ExecuteMovement(pc, _pendingMoveTarget);
        }

        _pendingAoOPath = null;
        _pendingMoveTarget = null;
    }

    /// <summary>
    /// Called when player cancels movement due to AoO warning.
    /// </summary>
    private void OnAoOCancelled()
    {
        _waitingForAoOConfirmation = false;
        _pendingAoOPath = null;
        _pendingMoveTarget = null;

        Debug.Log("[GameManager] Player cancelled movement to avoid AoOs");

        CharacterController pc = ActivePC;
        if (pc != null)
        {
            CurrentSubPhase = PlayerSubPhase.Moving;
            ShowMovementRange(pc);
            CombatUI.SetActionButtonsVisible(false);
            CombatUI.SetTurnIndicator($"{pc.Stats.CharacterName} - Click a tile to move (or own tile to cancel)");
        }
    }

    /// <summary>
    /// Resolve AoOs one by one, then execute the movement if the mover is still alive.
    /// </summary>
    private IEnumerator ResolveAoOsAndMove(CharacterController pc, SquareCell targetCell, AoOPathResult pathResult)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;

        foreach (var aooInfo in pathResult.ProvokedAoOs)
        {
            if (pc.Stats.IsDead) break; // Mover was killed by an AoO

            var threatener = aooInfo.Threatener;
            if (threatener.Stats.IsDead) continue; // Threatener already dead

            CombatResult aooResult = ThreatSystem.ExecuteAoO(threatener, pc);
            if (aooResult != null)
            {
                string aooLog = $"⚔ AoO: {aooResult.GetDetailedSummary()}";
                CombatUI.ShowCombatLog(aooLog);
                CombatUI.UpdateAllStats(PC1, PC2, NPC);

                if (LogAttacksToConsole)
                    Debug.Log("[Combat] " + aooLog);

                yield return new WaitForSeconds(1.0f);
            }
        }

        // If still alive, complete the movement
        if (!pc.Stats.IsDead)
        {
            ExecuteMovement(pc, targetCell);
        }
        else
        {
            Debug.Log($"[GameManager] {pc.Stats.CharacterName} was slain by AoO during movement!");
            CombatUI.ShowCombatLog($"{pc.Stats.CharacterName} was slain during movement!");
            CombatUI.UpdateAllStats(PC1, PC2, NPC);

            // Check if both PCs are dead
            if (PC1.Stats.IsDead && PC2.Stats.IsDead)
            {
                CurrentPhase = TurnPhase.CombatOver;
                CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                CombatUI.SetActionButtonsVisible(false);
                yield break;
            }

            // End this PC's turn
            yield return new WaitForSeconds(1.0f);
            EndActivePCTurn();
        }
    }

    /// <summary>
    /// Execute the actual movement (consume action, move character, update UI).
    /// </summary>
    private void ExecuteMovement(CharacterController pc, SquareCell cell)
    {
        // Consume the appropriate action
        if (pc.Actions.HasMoveAction)
        {
            pc.Actions.UseMoveAction();
        }
        else if (pc.Actions.CanConvertStandardToMove)
        {
            pc.Actions.ConvertStandardToMove();
        }

        pc.MoveToCell(cell);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);

        // Return to action choices (player can still use standard action if available)
        ShowActionChoices();
    }

    private void HandleAttackTargetClick(CharacterController pc, SquareCell cell)
    {
        // ===== SPELL CASTING MODE =====
        if (_pendingAttackMode == PendingAttackMode.CastSpell && _pendingSpell != null)
        {
            // For ally spells, clicking own tile = self-target
            if (cell.Coords == pc.GridPosition && _pendingSpell.TargetType == SpellTargetType.SingleAlly)
            {
                PerformSpellCast(pc, pc);
                return;
            }

            // Cancel if clicking non-highlighted cell
            if (!_highlightedCells.Contains(cell))
            {
                _pendingSpell = null;
                ShowActionChoices();
                return;
            }

            // Valid target click
            if (cell.IsOccupied && !cell.Occupant.Stats.IsDead)
            {
                PerformSpellCast(pc, cell.Occupant);
                return;
            }

            // Fallback cancel
            _pendingSpell = null;
            ShowActionChoices();
            return;
        }

        // ===== NORMAL ATTACK MODE =====
        // Allow clicking own tile or empty tile to cancel
        if (!cell.IsOccupied || cell.Occupant == pc || cell.Occupant.Stats.IsDead)
        {
            if (cell.Coords == pc.GridPosition || !_highlightedCells.Contains(cell))
            {
                // Cancel attack selection
                ShowActionChoices();
                return;
            }
        }

        if (cell.IsOccupied && cell.Occupant != pc && !cell.Occupant.Stats.IsDead && _highlightedCells.Contains(cell))
        {
            PerformPlayerAttack(pc, cell.Occupant);
        }
    }

    // ========== ATTACK EXECUTION ==========

    private void PerformPlayerAttack(CharacterController attacker, CharacterController target)
    {
        CurrentSubPhase = PlayerSubPhase.Animating;

        // Check for flanking
        var allies = new List<CharacterController>();
        if (attacker == PC1) allies.Add(PC2);
        else if (attacker == PC2) allies.Add(PC1);

        CharacterController flankPartner;
        bool isFlanking = CombatUtils.IsAttackerFlanking(attacker, target, allies, out flankPartner);
        int flankBonus = isFlanking ? CombatUtils.FlankingAttackBonus : 0;
        string partnerName = flankPartner != null ? flankPartner.Stats.CharacterName : "";

        // Calculate range info for ranged weapons
        RangeInfo rangeInfo = CalculateRangeInfo(attacker, target);

        switch (_pendingAttackMode)
        {
            case PendingAttackMode.Single:
                PerformSingleAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;

            case PendingAttackMode.FullAttack:
                PerformFullAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;

            case PendingAttackMode.DualWield:
                PerformDualWieldAttack(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;

            case PendingAttackMode.FlurryOfBlows:
                PerformFlurryOfBlows(attacker, target, isFlanking, flankBonus, partnerName, rangeInfo);
                break;
        }
    }

    /// <summary>
    /// Calculate range info for an attack between two characters.
    /// Returns a RangeInfo with distance, increment, and penalty data.
    /// Uses D&D 3.5 square grid distance (with diagonal costs).
    /// Correctly uses 5 max increments for thrown weapons, 10 for projectile weapons.
    /// </summary>
    private RangeInfo CalculateRangeInfo(CharacterController attacker, CharacterController target)
    {
        ItemData weapon = attacker.GetEquippedMainWeapon();
        int rangeIncrement = (weapon != null) ? weapon.RangeIncrement : 0;
        bool isThrownWeapon = (weapon != null) && weapon.IsThrown;
        int sqDist = SquareGridUtils.GetDistance(attacker.GridPosition, target.GridPosition);
        return RangeCalculator.GetRangeInfo(sqDist, rangeIncrement, isThrownWeapon);
    }

    private void PerformSingleAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        // Standard Action
        attacker.Actions.UseStandardAction();

        CombatResult result = attacker.Attack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        _lastCombatLog = result.GetDetailedSummary();

        // Log to Unity Console for debugging
        if (LogAttacksToConsole)
            Debug.Log("[Combat] " + _lastCombatLog);

        CombatUI.ShowCombatLog(_lastCombatLog);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);

        Grid.ClearAllHighlights();

        if (result.TargetKilled && target == NPC)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! Enemy defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        // After standard action, check if there are more actions available
        StartCoroutine(AfterAttackDelay(attacker, 1.5f));
    }

    private void PerformFullAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        // Full-Round Action
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.FullAttack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        _lastCombatLog = result.GetFullSummary();

        // Log detailed per-attack breakdown to Unity Console
        if (LogAttacksToConsole)
            LogFullAttackToConsole(result);

        CombatUI.ShowCombatLog(_lastCombatLog);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);

        Grid.ClearAllHighlights();

        if (result.TargetKilled && target == NPC)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! Enemy defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        // Full-round action ends the turn
        StartCoroutine(DelayedEndActivePCTurn(2.0f));
    }

    private void PerformDualWieldAttack(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        // Full-Round Action
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.DualWieldAttack(target, isFlanking, flankBonus, partnerName, rangeInfo);
        _lastCombatLog = result.GetFullSummary();

        // Log detailed per-attack breakdown to Unity Console
        if (LogAttacksToConsole)
            LogFullAttackToConsole(result);

        CombatUI.ShowCombatLog(_lastCombatLog);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);

        Grid.ClearAllHighlights();

        if (result.TargetKilled && target == NPC)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! Enemy defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        // Full-round action ends the turn
        StartCoroutine(DelayedEndActivePCTurn(2.0f));
    }

    /// <summary>
    /// Perform a Flurry of Blows attack (Monk only, Full-Round Action).
    /// </summary>
    private void PerformFlurryOfBlows(CharacterController attacker, CharacterController target,
        bool isFlanking, int flankBonus, string partnerName, RangeInfo rangeInfo = null)
    {
        // Full-Round Action
        attacker.Actions.UseFullRoundAction();

        FullAttackResult result = attacker.FlurryOfBlows(target, isFlanking, flankBonus, partnerName, rangeInfo);
        _lastCombatLog = result.GetFullSummary();

        // Log detailed per-attack breakdown to Unity Console
        if (LogAttacksToConsole)
            LogFullAttackToConsole(result);

        CombatUI.ShowCombatLog(_lastCombatLog);
        CombatUI.UpdateAllStats(PC1, PC2, NPC);

        Grid.ClearAllHighlights();

        if (result.TargetKilled && target == NPC)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("VICTORY! Enemy defeated!");
            CombatUI.SetActionButtonsVisible(false);
            return;
        }

        // Full-round action ends the turn
        StartCoroutine(DelayedEndActivePCTurn(2.0f));
    }

    /// <summary>
    /// After a standard action attack, return to action choices if more actions available,
    /// otherwise end turn.
    /// </summary>
    private IEnumerator AfterAttackDelay(CharacterController pc, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (CurrentPhase == TurnPhase.CombatOver) yield break;

        // Check if the PC still has actions (e.g., unused move action)
        if (pc.Actions.HasAnyActionLeft)
        {
            ShowActionChoices();
        }
        else
        {
            EndActivePCTurn();
        }
    }

    // ========== TURN ENDING ==========

    private void EndActivePCTurn()
    {
        CharacterController pc = ActivePC;
        Grid.ClearAllHighlights();
        _highlightedCells.Clear();
        CombatUI.SetActionButtonsVisible(false);

        if (CurrentPhase == TurnPhase.CombatOver) return;

        if (pc == PC1)
        {
            StartPCTurn(PC2);
        }
        else
        {
            StartCoroutine(NPCTurnCoroutine());
        }
    }

    private IEnumerator DelayedEndActivePCTurn(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (CurrentPhase != TurnPhase.CombatOver)
            EndActivePCTurn();
    }

    // ========== NPC AI TURN ==========

    private IEnumerator NPCTurnCoroutine()
    {
        CurrentPhase = TurnPhase.NPCTurn;
        CombatUI.SetTurnIndicator("Enemy Turn...");
        CombatUI.SetActivePC(0);
        CombatUI.SetActionButtonsVisible(false);

        if (NPC.Stats.IsDead)
        {
            yield return new WaitForSeconds(0.5f);
            StartPCTurn(PC1);
            yield break;
        }

        NPC.StartNewTurn();
        yield return new WaitForSeconds(0.8f);

        CharacterController closestPC = GetClosestAlivePC();

        if (closestPC == null)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
            CombatUI.SetActionButtonsVisible(false);
            yield break;
        }

        int distToTarget = SquareGridUtils.GetDistance(NPC.GridPosition, closestPC.GridPosition);

        // NPC uses simple action economy: move then attack
        if (distToTarget > NPC.Stats.AttackRange)
        {
            SquareCell bestCell = FindBestMoveToward(NPC, closestPC.GridPosition);
            if (bestCell != null)
            {
                NPC.MoveToCell(bestCell);
                NPC.Actions.UseMoveAction();
                CombatUI.ShowCombatLog($"{NPC.Stats.CharacterName} moves toward {closestPC.Stats.CharacterName}!");
                yield return new WaitForSeconds(0.6f);
            }
        }

        // Re-evaluate
        closestPC = GetClosestAlivePC();
        if (closestPC == null)
        {
            CurrentPhase = TurnPhase.CombatOver;
            CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
            yield break;
        }

        distToTarget = SquareGridUtils.GetDistance(NPC.GridPosition, closestPC.GridPosition);

        if (distToTarget <= NPC.Stats.AttackRange && !closestPC.Stats.IsDead)
        {
            NPC.Actions.UseStandardAction();
            RangeInfo npcRangeInfo = CalculateRangeInfo(NPC, closestPC);
            CombatResult result = NPC.Attack(closestPC, false, 0, null, npcRangeInfo);
            _lastCombatLog = result.GetDetailedSummary();

            // Log NPC attack to Unity Console for debugging
            if (LogAttacksToConsole)
                Debug.Log("[Combat] " + _lastCombatLog);

            CombatUI.ShowCombatLog(_lastCombatLog);
            CombatUI.UpdateAllStats(PC1, PC2, NPC);

            if (result.TargetKilled)
            {
                if (PC1.Stats.IsDead && PC2.Stats.IsDead)
                {
                    CurrentPhase = TurnPhase.CombatOver;
                    CombatUI.SetTurnIndicator("DEFEAT! All heroes have fallen!");
                    CombatUI.SetActionButtonsVisible(false);
                    yield break;
                }
                else
                {
                    CombatUI.ShowCombatLog(_lastCombatLog + $"\n{closestPC.Stats.CharacterName} has fallen, but the fight continues!");
                }
            }

            yield return new WaitForSeconds(1.2f);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        StartPCTurn(PC1);
    }

    private CharacterController GetClosestAlivePC()
    {
        bool pc1Alive = !PC1.Stats.IsDead;
        bool pc2Alive = !PC2.Stats.IsDead;

        if (!pc1Alive && !pc2Alive) return null;
        if (pc1Alive && !pc2Alive) return PC1;
        if (!pc1Alive && pc2Alive) return PC2;

        int dist1 = SquareGridUtils.GetDistance(NPC.GridPosition, PC1.GridPosition);
        int dist2 = SquareGridUtils.GetDistance(NPC.GridPosition, PC2.GridPosition);
        return dist1 <= dist2 ? PC1 : PC2;
    }

    private SquareCell FindBestMoveToward(CharacterController mover, Vector2Int targetPos)
    {
        List<SquareCell> moveCells = Grid.GetCellsInRange(mover.GridPosition, mover.Stats.MoveRange);
        SquareCell bestCell = null;
        int bestDist = int.MaxValue;

        foreach (var cell in moveCells)
        {
            if (cell.IsOccupied) continue;

            int dist = SquareGridUtils.GetDistance(cell.Coords, targetPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    // ========== DETAILED CONSOLE LOGGING ==========

    /// <summary>
    /// Log a FullAttackResult to the Unity Console with detailed per-attack breakdowns.
    /// Each attack is logged separately with [Combat] prefix for easy filtering.
    /// </summary>
    private void LogFullAttackToConsole(FullAttackResult result)
    {
        string attackerName = result.Attacker.Stats.CharacterName;
        string defenderName = result.Defender.Stats.CharacterName;

        // ═══ HEADER ═══
        Debug.Log("[Combat] ═══════════════════════════════════════");

        string actionLabel = result.Type == FullAttackResult.AttackType.FullAttack
            ? "full attacks"
            : result.Type == FullAttackResult.AttackType.DualWield
                ? "dual wields against"
                : "attacks";
        Debug.Log($"[Combat] {attackerName} {actionLabel} {defenderName}");

        // Weapon info
        if (result.Type == FullAttackResult.AttackType.DualWield
            && !string.IsNullOrEmpty(result.MainWeaponName)
            && !string.IsNullOrEmpty(result.OffWeaponName))
        {
            Debug.Log($"[Combat] Main Hand: {result.MainWeaponName}");
            Debug.Log($"[Combat] Off Hand: {result.OffWeaponName}");
        }
        else if (!string.IsNullOrEmpty(result.MainWeaponName))
        {
            bool isRanged = result.Attacks.Count > 0 && result.Attacks[0].IsRangedAttack;
            string wpnType = isRanged ? "ranged" : "melee";
            Debug.Log($"[Combat] Weapon: {result.MainWeaponName} ({wpnType})");
        }

        // Active feats
        if (result.Attacks.Count > 0)
        {
            var first = result.Attacks[0];
            var feats = new List<string>();
            if (first.PowerAttackValue > 0) feats.Add($"Power Attack (-{first.PowerAttackValue} atk/+{first.PowerAttackDamageBonus} dmg)");
            if (first.RapidShotActive) feats.Add("Rapid Shot");
            if (first.PointBlankShotActive) feats.Add("Point Blank Shot");
            if (feats.Count > 0)
                Debug.Log($"[Combat] Active Feats: {string.Join(", ", feats)}");

            // Flanking
            if (first.IsFlanking)
                Debug.Log($"[Combat] Flanking: Yes (with {first.FlankingPartnerName}, +{first.FlankingBonus})");

            // Range info
            if (first.IsRangedAttack)
            {
                string penaltyStr = first.RangePenalty == 0 ? "no penalty" : $"{first.RangePenalty} penalty";
                Debug.Log($"[Combat] Range: {first.RangeDistanceFeet} ft ({first.RangeDistanceSquares} sq) - Increment {first.RangeIncrementNumber}, {penaltyStr}");
            }
        }

        Debug.Log("[Combat]");

        // ═══ EACH ATTACK ═══
        for (int i = 0; i < result.Attacks.Count; i++)
        {
            Debug.Log("[Combat] ─────────────────────────────────────");

            CombatResult atk = result.Attacks[i];

            // Build the attack label
            string label = (i < result.AttackLabels.Count) ? result.AttackLabels[i] : $"Attack {i + 1}";
            Debug.Log($"[Combat] {label}:");

            // --- ATTACK ROLL ---
            Debug.Log("[Combat]   ATTACK ROLL:");
            Debug.Log($"[Combat]     d20 roll: {atk.DieRoll}");

            if (atk.BreakdownBAB != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.BreakdownBAB, "BAB")}");

            string abilName = !string.IsNullOrEmpty(atk.BreakdownAbilityName) ? atk.BreakdownAbilityName : "STR";
            if (atk.BreakdownAbilityMod != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.BreakdownAbilityMod, $"{abilName} modifier")}");

            if (atk.SizeAttackBonus != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.SizeAttackBonus, "size")}");

            if (atk.IsFlanking && atk.FlankingBonus != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.FlankingBonus, "flanking")}");

            if (atk.RacialAttackBonus != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.RacialAttackBonus, "racial")}");

            if (atk.PowerAttackValue > 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(-atk.PowerAttackValue, "Power Attack")}");

            if (atk.RapidShotActive)
                Debug.Log($"[Combat]     {FormatConsoleModLine(-2, "Rapid Shot")}");

            if (atk.PointBlankShotActive)
                Debug.Log($"[Combat]     {FormatConsoleModLine(1, "Point Blank Shot")}");

            if (atk.IsRangedAttack && atk.RangePenalty != 0)
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.RangePenalty, "range")}");

            if (atk.IsDualWieldAttack && atk.BreakdownDualWieldPenalty != 0)
            {
                string dwLabel = atk.IsOffHandAttack ? "off-hand penalty" : "dual wield penalty";
                Debug.Log($"[Combat]     {FormatConsoleModLine(atk.BreakdownDualWieldPenalty, dwLabel)}");
            }

            // Result line
            string critNote = "";
            if (atk.NaturalTwenty) critNote = " (NATURAL 20!)";
            else if (atk.NaturalOne) critNote = " (NATURAL 1!)";
            string hitMiss = atk.Hit ? "HIT!" : "MISS!";
            Debug.Log($"[Combat]     = {atk.TotalRoll} vs AC {atk.TargetAC} - {hitMiss}{critNote}");

            // Critical threat
            if (atk.IsCritThreat)
            {
                string threatRange = atk.CritThreatMin < 20 ? $"{atk.CritThreatMin}-20" : "20";
                string confModStr = CharacterStats.FormatMod(atk.ConfirmationTotal - atk.ConfirmationRoll);
                if (atk.CritConfirmed)
                    Debug.Log($"[Combat]   *** CRITICAL THREAT ({threatRange})! Confirm: {atk.ConfirmationRoll} {confModStr} = {atk.ConfirmationTotal} vs AC {atk.TargetAC} - CONFIRMED! (×{atk.CritMultiplier}) ***");
                else
                    Debug.Log($"[Combat]   *** Critical Threat ({threatRange})! Confirm: {atk.ConfirmationRoll} {confModStr} = {atk.ConfirmationTotal} vs AC {atk.TargetAC} - Not confirmed ***");
            }

            // --- DAMAGE ROLL ---
            if (atk.Hit)
            {
                Debug.Log("[Combat]   DAMAGE ROLL:");
                string diceStr = !string.IsNullOrEmpty(atk.BaseDamageDiceStr) ? atk.BaseDamageDiceStr : "?";

                if (atk.CritConfirmed)
                {
                    Debug.Log($"[Combat]     CRITICAL HIT! (×{atk.CritMultiplier})");
                    Debug.Log($"[Combat]     {atk.CritDamageDice} = {atk.Damage - atk.FeatDamageBonus} (crit weapon + mods)");
                }
                else
                {
                    Debug.Log($"[Combat]     {diceStr} roll: {atk.BaseDamageRoll}");

                    if (atk.DamageModifier != 0)
                    {
                        string dmgModLabel = !string.IsNullOrEmpty(atk.DamageModifierDesc) ? atk.DamageModifierDesc : abilName;
                        Debug.Log($"[Combat]     {FormatConsoleModLine(atk.DamageModifier, dmgModLabel)}");
                    }
                }

                if (atk.PowerAttackDamageBonus > 0)
                    Debug.Log($"[Combat]     {FormatConsoleModLine(atk.PowerAttackDamageBonus, "Power Attack")}");

                if (atk.PointBlankShotActive)
                    Debug.Log($"[Combat]     {FormatConsoleModLine(1, "Point Blank Shot")}");

                Debug.Log($"[Combat]     = {atk.Damage} damage");

                if (atk.SneakAttackApplied)
                {
                    Debug.Log($"[Combat]     + {atk.SneakAttackDamage} sneak attack ({atk.SneakAttackDice}d6)");
                    Debug.Log($"[Combat]     = {atk.TotalDamage} total damage");
                }
            }

            Debug.Log("[Combat]");
        }

        // ═══ SUMMARY ═══
        Debug.Log("[Combat] ─────────────────────────────────────");
        string critSummary = result.CritCount > 0 ? $", {result.CritCount} critical(s)!" : "";
        Debug.Log($"[Combat] SUMMARY: {result.HitCount}/{result.Attacks.Count} hits{critSummary}, {result.TotalDamageDealt} total damage");
        Debug.Log($"[Combat] {defenderName}: {result.DefenderHPBefore} → {result.DefenderHPAfter} HP");

        if (result.TargetKilled)
            Debug.Log($"[Combat] {defenderName} has been slain!");

        Debug.Log("[Combat] ═══════════════════════════════════════");
    }

    /// <summary>Format a modifier for console output like "+ 3 (STR)" or "- 2 (Rapid Shot)".</summary>
    private static string FormatConsoleModLine(int value, string label)
    {
        if (value >= 0)
            return $"+ {value} ({label})";
        else
            return $"- {-value} ({label})";
    }
}

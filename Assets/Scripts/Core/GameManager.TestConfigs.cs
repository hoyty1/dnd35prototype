using System.Collections.Generic;
using System.Linq;
using DND35.Magic;
using DND35e.Identifiers;
using UnityEngine;

/// <summary>
/// GameManager partial class: Test Encounter Configurations
/// 
/// Contains all Configure*TestParty methods used for development and testing.
/// These methods set up specific encounter scenarios for verifying game mechanics
/// like grappling, spellcasting, special attacks, etc.
/// 
/// Also contains helper methods:
/// - AutoPopulateAndPrepareAllImplementedClassSpells
/// - EnsureSpellSlotArrayCapacity
/// - PrepareSummonMonsterTestSpellSlots
/// - RestoreStandardPartyLayout
/// - SetPCActiveState
/// 
/// Extracted from main GameManager.cs to reduce file size.
/// </summary>
public partial class GameManager
{
    // ═══════════════════════════════════════════════════════════════════
    //  TEST ENCOUNTER CONFIGURATIONS
    // ═══════════════════════════════════════════════════════════════════

    private void ConfigureGrappleTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats fighterStats = new CharacterStats(
            name: "Grapple Tester",
            level: 6,
            characterClass: "Fighter",
            str: 17, dex: 12, con: 14, wis: 10, intelligence: 10, cha: 10,
            bab: 6,
            armorBonus: 4,
            shieldBonus: 1,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 45,
            raceName: "Human"
        );

        fighterStats.CharacterAlignment = Alignment.LawfulNeutral;

        Vector2Int fighterStart = new Vector2Int(9, 9);
        Sprite fighterAlive = IconLoader.GetToken("Fighter") ?? pcAliveFallback;
        PC1.Init(fighterStats, fighterStart, fighterAlive, pcDead);

        var fighterInventory = PC1.gameObject.GetComponent<InventoryComponent>();
        if (fighterInventory == null)
            fighterInventory = PC1.gameObject.AddComponent<InventoryComponent>();
        fighterInventory.Init(fighterStats);
        SetupStartingEquipment(fighterInventory, "Fighter");

        // Grapple test loadout: equip a greatsword for two-handed weapon grapple validation.
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.GREATSWORD), EquipSlot.RightHand);

        // Greatsword is two-handed; explicitly clear off-hand to avoid stale setup state.
        fighterInventory.CharacterInventory.LeftHandSlot = null;

        fighterInventory.CharacterInventory.RecalculateStats();

        // Keep only one player combatant active for a focused grapple scenario.
        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🧪 Grapple Test: Fighter and target start adjacent. Use Special Attack -> Grapple.");
    }

    private void ConfigureGreaseTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Greasy Greg",
            level: 5,
            characterClass: "Wizard",
            str: 10, dex: 14, con: 12, wis: 13, intelligence: 18, cha: 10,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 22,
            raceName: "Human"
        );
        wizardStats.CharacterAlignment = Alignment.TrueNeutral;

        CharacterStats fighterStats = new CharacterStats(
            name: "Slippery Sam",
            level: 5,
            characterClass: "Fighter",
            str: 18, dex: 14, con: 14, wis: 12, intelligence: 10, cha: 10,
            bab: 5,
            armorBonus: 4,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 47,
            raceName: "Human"
        );
        fighterStats.CharacterAlignment = Alignment.LawfulNeutral;

        Vector2Int wizardStart = new Vector2Int(5, 5);
        Vector2Int fighterStart = new Vector2Int(7, 5);

        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        Sprite fighterAlive = IconLoader.GetToken("Fighter") ?? pcAliveFallback;

        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);
        PC2.Init(fighterStats, fighterStart, fighterAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        InventoryComponent fighterInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        fighterInventory.Init(fighterStats);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        fighterInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            SpellNames.DETECT_MAGIC_WIZ, SpellNames.READ_MAGIC, SpellNames.GREASE, SpellNames.MAGE_ARMOR
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            SpellNames.GREASE, SpellNames.GREASE, SpellNames.GREASE, SpellNames.GREASE
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        StatusEffectManager fighterStatusMgr = PC2.gameObject.GetComponent<StatusEffectManager>() ?? PC2.gameObject.AddComponent<StatusEffectManager>();
        fighterStatusMgr.Init(fighterStats);

        ConcentrationManager wizardConcentrationMgr = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC1);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("╔═══════════════════════════════════════════════════════╗");
        CombatUI?.ShowCombatLog("║          🧪 GREASE MECHANICS TEST SCENARIO           ║");
        CombatUI?.ShowCombatLog("╚═══════════════════════════════════════════════════════╝");
        CombatUI?.ShowCombatLog("This scenario tests all three Grease modes and grapple defense timing.");
        CombatUI?.ShowCombatLog("  • Greasy Greg (Wizard 5): Grease prepared x4 (DC 15). ");
        CombatUI?.ShowCombatLog("  • Slippery Sam (Fighter 5): NO pre-applied grease; must be buffed by spell.");
        CombatUI?.ShowCombatLog("  • Enemies: 4 low-Reflex grapplers clustered for 10-ft area testing.");
        CombatUI?.ShowCombatLog("");
        CombatUI?.ShowCombatLog("WIZARD ACTIONS:");
        CombatUI?.ShowCombatLog("  1. Cast Grease (Armor) on Slippery Sam (+10 grapple defense, 5 rounds).");
        CombatUI?.ShowCombatLog("  2. Cast Grease (Area) on enemy cluster to force Reflex saves/prone.");
        CombatUI?.ShowCombatLog("  3. Cast Grease (Object) on enemy weapon to force drops.");
        CombatUI?.ShowCombatLog("FIGHTER ACTIONS:");
        CombatUI?.ShowCombatLog("  1. Wait for Grease (Armor), then absorb enemy grapple attempts.");
        CombatUI?.ShowCombatLog("  2. If grappled, test escape checks with the +10 circumstance bonus.");
        CombatUI?.ShowCombatLog("ENEMY BEHAVIOR:");
        CombatUI?.ShowCombatLog("  • All grapplers prioritize Slippery Sam first.");

        Debug.Log($"[GreaseTest] Party ready. Wizard at {wizardStart}, Fighter at {fighterStart}. Grease prepared: 4.");
    }

    private void ConfigureFeintSneakTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats rogueStats = new CharacterStats(
            name: "Shadow",
            level: 6,
            characterClass: "Rogue",
            str: 10, dex: 18, con: 14, wis: 10, intelligence: 12, cha: 14,
            bab: 4,
            armorBonus: 3,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 1,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 36,
            raceName: "Human"
        );

        rogueStats.CharacterAlignment = Alignment.ChaoticNeutral;
        rogueStats.InitFeats();
        rogueStats.AddFeats(new List<string> { "Weapon Finesse", "Combat Expertise", "Improved Feint", "Dodge" });

        rogueStats.InitializeSkills("Rogue", 6);
        for (int i = 0; i < 9; i++) rogueStats.AddSkillRank("Bluff");
        for (int i = 0; i < 9; i++) rogueStats.AddSkillRank("Hide");
        for (int i = 0; i < 9; i++) rogueStats.AddSkillRank("Move Silently");
        for (int i = 0; i < 9; i++) rogueStats.AddSkillRank("Tumble");
        for (int i = 0; i < 2; i++) rogueStats.AddSkillRank("Sense Motive");

        Vector2Int rogueStart = new Vector2Int(9, 9);
        Sprite rogueAlive = IconLoader.GetToken("Rogue") ?? pcAliveFallback;
        PC1.Init(rogueStats, rogueStart, rogueAlive, pcDead);

        InventoryComponent rogueInventory = PC1.gameObject.GetComponent<InventoryComponent>();
        if (rogueInventory == null)
            rogueInventory = PC1.gameObject.AddComponent<InventoryComponent>();
        rogueInventory.Init(rogueStats);

        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.RAPIER), EquipSlot.RightHand);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.DAGGER), EquipSlot.LeftHand);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.STUDDED_LEATHER), EquipSlot.Armor);

        rogueInventory.CharacterInventory.RecalculateStats();

        // Focus this scenario on one rogue actor.
        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🗡️ Feint Test: Shadow (Rogue 6) starts adjacent to a goblin. Use Special Attack -> Feint, then attack for sneak damage.");
    }

    private void ConfigureTurnUndeadTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats clericStats = new CharacterStats(
            name: "Brother Marcus",
            level: 6,
            characterClass: "Cleric",
            str: 12, dex: 10, con: 14, wis: 16, intelligence: 10, cha: 16,
            bab: 4,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 1,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 42,
            raceName: "Human"
        );

        clericStats.CharacterAlignment = Alignment.LawfulGood;

        Vector2Int clericStart = new Vector2Int(9, 9);
        Sprite clericAlive = IconLoader.GetToken("Cleric") ?? pcAliveFallback;
        PC1.Init(clericStats, clericStart, clericAlive, pcDead);

        InventoryComponent clericInventory = PC1.gameObject.GetComponent<InventoryComponent>();
        if (clericInventory == null)
            clericInventory = PC1.gameObject.AddComponent<InventoryComponent>();
        clericInventory.Init(clericStats);

        // Turn Undead test loadout:
        // - Light crossbow equipped for ranged validation
        // - Heavy mace in inventory as melee backup
        // - 20 bolts as a placeholder ammo bundle (display/logging)
        ItemData lightCrossbow = ItemDatabase.CloneItem(ItemIDs.CROSSBOW_LIGHT);
        if (lightCrossbow != null)
            clericInventory.CharacterInventory.DirectEquip(lightCrossbow, EquipSlot.RightHand);

        ItemData heavyMace = ItemDatabase.CloneItem(ItemIDs.MACE_HEAVY);
        if (heavyMace != null)
            clericInventory.CharacterInventory.AddItem(heavyMace);

        ItemData crossbowBolts = ItemDatabase.CloneItem(ItemIDs.AMMO_BOLT);
        if (crossbowBolts != null)
            clericInventory.CharacterInventory.AddItem(crossbowBolts);

        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        clericInventory.CharacterInventory.RecalculateStats();

        CharacterStats fighterStats = new CharacterStats(
            name: "Gareth",
            level: 6,
            characterClass: "Fighter",
            str: 18, dex: 14, con: 16, wis: 12, intelligence: 10, cha: 8,
            bab: 6,
            armorBonus: 5,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 5,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 58,
            raceName: "Human"
        );

        fighterStats.CharacterAlignment = Alignment.LawfulGood;

        Vector2Int fighterStart = new Vector2Int(9, 7); // 10 feet south of Brother Marcus.
        Sprite fighterAlive = IconLoader.GetToken("Fighter") ?? pcAliveFallback;
        PC2.Init(fighterStats, fighterStart, fighterAlive, pcDead);

        InventoryComponent fighterInventory = PC2.gameObject.GetComponent<InventoryComponent>();
        if (fighterInventory == null)
            fighterInventory = PC2.gameObject.AddComponent<InventoryComponent>();
        fighterInventory.Init(fighterStats);

        ItemData longsword = ItemDatabase.CloneItem(ItemIDs.LONGSWORD);
        if (longsword != null)
            fighterInventory.CharacterInventory.DirectEquip(longsword, EquipSlot.RightHand);

        ItemData chainmail = ItemDatabase.CloneItem(ItemIDs.CHAINMAIL);
        if (chainmail != null)
            fighterInventory.CharacterInventory.DirectEquip(chainmail, EquipSlot.Armor);

        ItemData heavyShield = ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL)
            ?? ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_WOODEN)
            ?? ItemDatabase.CloneItem(ItemIDs.SHIELD_LIGHT_WOODEN);
        if (heavyShield != null)
            fighterInventory.CharacterInventory.DirectEquip(heavyShield, EquipSlot.LeftHand);

        fighterInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("✝️ Turn Undead Test (Expanded): Brother Marcus (Cleric 6) + Gareth (Fighter 6) vs 12 skeletons + 3 wights (24 HD total).");
        CombatUI?.ShowCombatLog("   Turn HD pool at L6 cleric + CHA 16 averages ~15 (range 10-20), so the HD selection menu should appear consistently.");
        CombatUI?.ShowCombatLog("   Goals: validate HD pool target selection, destruction vs turning choices, and that fighter attacks do NOT break Turn Undead.");
    }

    private void ConfigureArmorTargetingTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Aria",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 13, intelligence: 18, cha: 10,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human"
        );

        CharacterStats rogueStats = new CharacterStats(
            name: "Shade",
            level: 5,
            characterClass: "Rogue",
            str: 12, dex: 18, con: 14, wis: 10, intelligence: 13, cha: 12,
            bab: 3,
            armorBonus: 2,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 1,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 34,
            raceName: "Human"
        );

        CharacterStats fighterStats = new CharacterStats(
            name: "Brom",
            level: 5,
            characterClass: "Fighter",
            str: 18, dex: 12, con: 16, wis: 12, intelligence: 10, cha: 8,
            bab: 5,
            armorBonus: 8,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 4,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 52,
            raceName: "Human"
        );

        PC1.Init(wizardStats, new Vector2Int(6, 8), IconLoader.GetToken("Wizard") ?? pcAliveFallback, pcDead);
        PC2.Init(rogueStats, new Vector2Int(8, 8), IconLoader.GetToken("Rogue") ?? pcAliveFallback, pcDead);
        PC3.Init(fighterStats, new Vector2Int(10, 8), IconLoader.GetToken("Fighter") ?? pcAliveFallback, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        InventoryComponent rogueInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        rogueInventory.Init(rogueStats);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LEATHER_ARMOR), EquipSlot.Armor);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHORT_SWORD), EquipSlot.RightHand);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHORT_SWORD), EquipSlot.LeftHand);
        rogueInventory.CharacterInventory.RecalculateStats();

        InventoryComponent fighterInventory = PC3.gameObject.GetComponent<InventoryComponent>() ?? PC3.gameObject.AddComponent<InventoryComponent>();
        fighterInventory.Init(fighterStats);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.FULL_PLATE), EquipSlot.Armor);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        fighterInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, true, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        PC1.DebugPrintTags();
        PC2.DebugPrintTags();
        PC3.DebugPrintTags();

        CombatUI?.ShowCombatLog("🏹 Armor Targeting Test: Skeleton archers prioritize Unarmored > Light > Medium > Heavy when targets are in range.");
        CombatUI?.ShowCombatLog($"   {PC1.Stats.CharacterName}: {PC1.GetArmorTag()} | {PC2.Stats.CharacterName}: {PC2.GetArmorTag()} | {PC3.Stats.CharacterName}: {PC3.GetArmorTag()}");
    }

    private void ConfigureTigerHuntTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats fighterStats = new CharacterStats(
            name: "Test Fighter",
            level: 5,
            characterClass: "Fighter",
            str: 18, dex: 12, con: 16, wis: 10, intelligence: 10, cha: 8,
            bab: 5,
            armorBonus: 8,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 4,
            baseSpeed: 4,
            atkRange: 1,
            baseHitDieHP: 38,
            raceName: "Human"
        );

        CharacterStats rogueStats = new CharacterStats(
            name: "Test Rogue",
            level: 5,
            characterClass: "Rogue",
            str: 12, dex: 18, con: 14, wis: 10, intelligence: 13, cha: 12,
            bab: 3,
            armorBonus: 2,
            shieldBonus: 0,
            damageDice: 6,
            damageCount: 1,
            bonusDamage: 1,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 28,
            raceName: "Human"
        );

        CharacterStats wizardStats = new CharacterStats(
            name: "Test Wizard",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 13, intelligence: 18, cha: 10,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 18,
            raceName: "Human"
        );

        fighterStats.CharacterAlignment = Alignment.LawfulNeutral;
        rogueStats.CharacterAlignment = Alignment.ChaoticNeutral;
        wizardStats.CharacterAlignment = Alignment.NeutralGood;

        PC1.Init(fighterStats, new Vector2Int(6, 12), IconLoader.GetToken("Fighter") ?? pcAliveFallback, pcDead);
        PC2.Init(rogueStats, new Vector2Int(8, 10), IconLoader.GetToken("Rogue") ?? pcAliveFallback, pcDead);
        PC3.Init(wizardStats, new Vector2Int(9, 7), IconLoader.GetToken("Wizard") ?? pcAliveFallback, pcDead);

        InventoryComponent fighterInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        fighterInventory.Init(fighterStats);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.FULL_PLATE), EquipSlot.Armor);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        fighterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        fighterInventory.CharacterInventory.RecalculateStats();

        InventoryComponent rogueInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        rogueInventory.Init(rogueStats);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LEATHER_ARMOR), EquipSlot.Armor);
        rogueInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHORT_SWORD), EquipSlot.RightHand);
        rogueInventory.CharacterInventory.RecalculateStats();

        InventoryComponent wizardInventory = PC3.gameObject.GetComponent<InventoryComponent>() ?? PC3.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        // Predator-priority target setup: start rogue wounded and wizard invisible.
        rogueStats.CurrentHP = Mathf.Clamp(12, 1, rogueStats.TotalMaxHP);
        PC3.ApplyCondition(CombatConditionType.Invisible, -1, "Tiger Hunt Test");

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, true, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🐅 Tiger Hunt Test: Fighter, wounded rogue, and invisible wizard face a tiger in open terrain.");
        CombatUI?.ShowCombatLog("   Verify: Pounce charge + rake sequence, Improved Grab follow-up, scent targeting on invisible wizard, and predator target choice.");
        CombatUI?.ShowCombatLog("   Optional: focus fire tiger below 30% HP to verify animal withdraw/flee behavior.");
    }

    private void ConfigureOgreBattleTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Aria",
            level: 6,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 13, intelligence: 18, cha: 10,
            bab: 3,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human"
        );

        wizardStats.CharacterAlignment = Alignment.NeutralGood;

        PC1.Init(wizardStats, new Vector2Int(6, 10), IconLoader.GetToken("Wizard") ?? pcAliveFallback, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🧙 Ogre Battle: Aria (Wizard 6) fights alongside a controllable dire tiger against two ogres.");
        CombatUI?.ShowCombatLog("   Validate: multi-character player turns, ally tiger control, and berserk ogre pressure.");
    }

    private void ConfigureShieldBashTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats shielderStats = new CharacterStats(
            name: "Shielder",
            level: 5,
            characterClass: "Fighter",
            str: 16, dex: 14, con: 14, wis: 10, intelligence: 10, cha: 10,
            bab: 5,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 3,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 44,
            raceName: "Human"
        );

        shielderStats.InitFeats();
        shielderStats.AddFeats(new List<string> { "Improved Shield Bash" });

        CharacterStats basherStats = new CharacterStats(
            name: "Basher",
            level: 5,
            characterClass: "Fighter",
            str: 16, dex: 14, con: 14, wis: 10, intelligence: 10, cha: 10,
            bab: 5,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 3,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 44,
            raceName: "Human"
        );

        basherStats.InitFeats();

        Vector2Int shielderStart = new Vector2Int(6, 9);
        Vector2Int basherStart = new Vector2Int(12, 9);

        PC1.Init(shielderStats, shielderStart, IconLoader.GetToken("Fighter") ?? pcAliveFallback, pcDead);
        PC2.Init(basherStats, basherStart, IconLoader.GetToken("Fighter") ?? pcAliveFallback, pcDead);

        InventoryComponent shielderInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        shielderInventory.Init(shielderStats);
        shielderInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        shielderInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        shielderInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAIN_SHIRT), EquipSlot.Armor);
        shielderInventory.CharacterInventory.RecalculateStats();

        InventoryComponent basherInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        basherInventory.Init(basherStats);
        basherInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        basherInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        basherInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAIN_SHIRT), EquipSlot.Armor);
        basherInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🛡️ Shield Bash Test: Shielder (Improved Shield Bash) vs Basher (no feat).");
        CombatUI?.ShowCombatLog("   Both use longsword + heavy shield + chain shirt. Expected base AC: 18 each.");
        CombatUI?.ShowCombatLog("   After shield bash: Shielder keeps AC 18; Basher drops to AC 16 until next turn.");
    }

    private void ConfigureCelestialTemplateTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats clericStats = new CharacterStats(
            name: "Lysara",
            level: 5,
            characterClass: "Cleric",
            str: 12, dex: 10, con: 14, wis: 18, intelligence: 10, cha: 14,
            bab: 3,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 1,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 36,
            raceName: "Human"
        );

        clericStats.CharacterAlignment = Alignment.LawfulGood;

        Vector2Int clericStart = new Vector2Int(3, 7);
        Sprite clericAlive = IconLoader.GetToken("Cleric") ?? pcAliveFallback;
        PC1.Init(clericStats, clericStart, clericAlive, pcDead);

        InventoryComponent clericInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        clericInventory.Init(clericStats);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.MACE_HEAVY), EquipSlot.RightHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        clericInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("✨ Celestial Template Test: Lysara (Cleric 5) commands celestial wolf + celestial dire bear allies.");
        CombatUI?.ShowCombatLog("   Verify: templates are applied at spawn time (Magical Beast type, resistances, DR/SR scaling, Smite Evil). ");
        CombatUI?.ShowCombatLog("   Opposing undead should remain evil-aligned to validate celestial smite targeting.");
    }

    private void ConfigureFiendishTemplateTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats necromancerStats = new CharacterStats(
            name: "Malakai",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 18, cha: 14,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 28,
            raceName: "Human"
        );

        necromancerStats.CharacterAlignment = Alignment.NeutralEvil;
        necromancerStats.AddSpecialAbility("Necromancy Focus");

        Vector2Int necromancerStart = new Vector2Int(3, 7);
        Sprite necromancerAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(necromancerStats, necromancerStart, necromancerAlive, pcDead);

        InventoryComponent necromancerInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        necromancerInventory.Init(necromancerStats);
        necromancerInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        necromancerInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🔥 Fiendish Template Test: Malakai (NE Wizard 5) commands fiendish wolf + fiendish dire bear allies.");
        CombatUI?.ShowCombatLog("   Verify Fiendish scaling: darkvision, Resist Cold/Fire, Smite Good, DR 10/magic at 12 HD, and SR 22 on the dire bear.");
        CombatUI?.ShowCombatLog("   Targets are good-aligned human paladin + cleric to validate Smite Good selection and damage bonuses.");
    }

    private void ConfigureSummonMonsterTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats clericStats = new CharacterStats(
            name: "Ilyra",
            level: 5,
            characterClass: "Cleric",
            str: 10, dex: 12, con: 14, wis: 18, intelligence: 10, cha: 14,
            bab: 3,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 34,
            raceName: "Human"
        );
        clericStats.CharacterAlignment = Alignment.NeutralGood;

        CharacterStats wizardStats = new CharacterStats(
            name: "Theron",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 18, cha: 10,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human"
        );
        wizardStats.CharacterAlignment = Alignment.TrueNeutral;

        Vector2Int clericStart = new Vector2Int(4, 9);
        Vector2Int wizardStart = new Vector2Int(3, 10);

        Sprite clericAlive = IconLoader.GetToken("Cleric") ?? pcAliveFallback;
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;

        PC1.Init(clericStats, clericStart, clericAlive, pcDead);
        PC2.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent clericInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        clericInventory.Init(clericStats);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.MACE_HEAVY), EquipSlot.RightHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        clericInventory.CharacterInventory.RecalculateStats();

        InventoryComponent wizardInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent clericSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        clericSpellComp.KnownSpells.Clear();
        clericSpellComp.SelectedSpellIds = new List<string> { "detect_magic", SpellNames.GUIDANCE, SpellNames.LIGHT, "resistance" };
        clericSpellComp.PreparedSpellSlotIds = null;
        clericSpellComp.Init(clericStats);
        PrepareSummonMonsterTestSpellSlots(
            clericSpellComp,
            summonOneSpellId: "summon_monster_1_clr",
            summonTwoSpellId: "summon_monster_2_clr",
            levelOneFallbackAId: SpellNames.BLESS,
            levelOneFallbackBId: SpellNames.SHIELD_OF_FAITH,
            levelTwoFallbackAId: SpellNames.HOLD_PERSON,
            levelTwoFallbackBId: SpellNames.CURE_MODERATE_WOUNDS);

        SpellcastingComponent wizardSpellComp = PC2.gameObject.GetComponent<SpellcastingComponent>() ?? PC2.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string> { "detect_magic", SpellNames.RAY_OF_FROST, SpellNames.ACID_SPLASH, SpellNames.READ_MAGIC };
        wizardSpellComp.PreparedSpellSlotIds = null;
        wizardSpellComp.Init(wizardStats);
        PrepareSummonMonsterTestSpellSlots(
            wizardSpellComp,
            summonOneSpellId: SpellNames.SUMMON_MONSTER_1,
            summonTwoSpellId: SpellNames.SUMMON_MONSTER_2,
            levelOneFallbackAId: SpellNames.MAGIC_MISSILE,
            levelOneFallbackBId: SpellNames.MAGE_ARMOR,
            levelTwoFallbackAId: SpellNames.MIRROR_IMAGE,
            levelTwoFallbackBId: SpellNames.INVISIBILITY);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🌀 Summon Monster Test: Ilyra (Cleric 5) and Theron (Wizard 5) both have Summon Monster I/II prepared.");
        CombatUI?.ShowCombatLog("   Flow validation: choose creature first, then pick a legal placement tile.");
        CombatUI?.ShowCombatLog("   Alignment validation: cleric sees celestial/fiendish cleric-locked options based on alignment; wizard sees class-agnostic options.");
    }

    private void ConfigureNpcMagicMissileTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Theron",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 18, cha: 10,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human"
        );

        Vector2Int wizardStart = new Vector2Int(3, 9);

        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;

        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            SpellNames.DETECT_MAGIC_WIZ, SpellNames.RAY_OF_FROST, SpellNames.ACID_SPLASH, SpellNames.READ_MAGIC,
            SpellNames.SHIELD, SpellNames.MAGIC_MISSILE, SpellNames.MAGE_ARMOR, SpellNames.MIRROR_IMAGE
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            SpellNames.DETECT_MAGIC_WIZ, SpellNames.RAY_OF_FROST, SpellNames.ACID_SPLASH, SpellNames.READ_MAGIC,
            SpellNames.SHIELD, SpellNames.MAGIC_MISSILE, SpellNames.MAGE_ARMOR,
            SpellNames.MIRROR_IMAGE, SpellNames.INVISIBILITY
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        ConcentrationManager wizardConcentrationMgr = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC1);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🧪 NPC Magic Missile Test: Theron (Wizard 5) has Shield prepared for direct counter-testing.");
        CombatUI?.ShowCombatLog("   Cast Shield on Theron, then end turn to verify Arcane Missile Adept cannot damage him with Magic Missile.");
        CombatUI?.ShowCombatLog("   Scenario is now focused to two combatants only: player wizard vs enemy Arcane Missile Adept.");
    }

    private void ConfigureProtectionFromEvilTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Warded Theron",
            level: 10,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 14, wis: 14, intelligence: 20, cha: 10,
            bab: 5,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 58,
            raceName: "Human"
        );
        wizardStats.CharacterAlignment = Alignment.TrueNeutral;

        Vector2Int wizardStart = new Vector2Int(3, 9);
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            SpellNames.DETECT_MAGIC_WIZ, SpellNames.READ_MAGIC, SpellNames.PROTECTION_FROM_EVIL, SpellNames.SHIELD, SpellNames.MAGIC_MISSILE
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            SpellNames.PROTECTION_FROM_EVIL, SpellNames.PROTECTION_FROM_EVIL, SpellNames.SHIELD, SpellNames.MAGIC_MISSILE, SpellNames.MAGIC_MISSILE
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        SpellData protectionFromEvil = SpellDatabase.GetSpell(SpellNames.PROTECTION_FROM_EVIL);
        if (protectionFromEvil != null)
        {
            wizardStatusMgr.AddEffect(protectionFromEvil, "Scenario Setup", casterLevel: wizardStats.Level);
            CombatUI?.ShowCombatLog("🛡️ Warded Theron starts with Protection from Evil active.");
        }
        else
        {
            Debug.LogError("[ProtectionFromEvilTest] Missing spell definition: protection_from_evil");
        }

        ConcentrationManager wizardConcentrationMgr = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC1);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🧪 Protection from Evil Test: Warded Theron (Wizard 10) vs evil + non-evil controls.");
        CombatUI?.ShowCombatLog("This scenario tests SIX mechanics:");
        CombatUI?.ShowCombatLog("  1. Mental Control Immunity (Charm Person blocked)");
        CombatUI?.ShowCombatLog("  2. Summoned Barrier (Fiendish Wolf can't touch)");
        CombatUI?.ShowCombatLog("  3. AC Bonus vs Evil (+2 vs Evil Goblin)");
        CombatUI?.ShowCombatLog("  4. NO AC Bonus vs Non-Evil (normal AC vs Neutral Bandit)");
        CombatUI?.ShowCombatLog("  5. Save Bonus vs Evil (+2 vs Evil Acolyte's Daze)");
        CombatUI?.ShowCombatLog("  6. NO Save Bonus vs Non-Evil (normal save vs Neutral Mage's Daze)");
        CombatUI?.ShowCombatLog("");
    }

    private void ConfigureWindDispersionTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        // Player caster #1: Wizard focused on repeat Obscuring Mist coverage.
        CharacterStats druidStats = new CharacterStats(
            name: "Zephyr Windcaller",
            level: 5,
            characterClass: "Wizard",
            str: 10, dex: 12, con: 14, wis: 12, intelligence: 16, cha: 12,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 22,
            raceName: "Human"
        );

        Vector2Int druidStart = new Vector2Int(5, 5);
        Sprite druidAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(druidStats, druidStart, druidAlive, pcDead);

        InventoryComponent druidInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        druidInventory.Init(druidStats);
        druidInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        druidInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent druidSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        druidSpellComp.KnownSpells.Clear();
        druidSpellComp.SelectedSpellIds = new List<string>
        {
            SpellNames.OBSCURING_MIST
        };
        druidSpellComp.PreparedSpellSlotIds = new List<string>
        {
            SpellNames.OBSCURING_MIST, SpellNames.OBSCURING_MIST, SpellNames.OBSCURING_MIST
        };
        druidSpellComp.Init(druidStats);

        StatusEffectManager druidStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        druidStatusMgr.Init(druidStats);

        ConcentrationManager druidConcentrationMgr = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        druidConcentrationMgr.Init(druidStats, PC1);

        // Player caster #2: Wizard for additional mist + ranged control spell.
        CharacterStats wizardStats = new CharacterStats(
            name: "Misty Veilweaver",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 18, cha: 10,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 20,
            raceName: "Elf"
        );

        Vector2Int wizardStart = new Vector2Int(7, 5);
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC2.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC2.gameObject.GetComponent<SpellcastingComponent>() ?? PC2.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            SpellNames.OBSCURING_MIST, SpellNames.MAGIC_MISSILE
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            SpellNames.OBSCURING_MIST, SpellNames.MAGIC_MISSILE
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC2.gameObject.GetComponent<StatusEffectManager>() ?? PC2.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        ConcentrationManager wizardConcentrationMgr = PC2.gameObject.GetComponent<ConcentrationManager>() ?? PC2.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC2);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("╔══════════════════════════════════════════════════════╗");
        CombatUI?.ShowCombatLog("║           OBSCURING MIST TEST SCENARIO              ║");
        CombatUI?.ShowCombatLog("╚══════════════════════════════════════════════════════╝");
        CombatUI?.ShowCombatLog("Party: Zephyr Windcaller (Wizard 5) + Misty Veilweaver (Wizard 5)");
        CombatUI?.ShowCombatLog("Enemy line: Small + Medium + Medium (high Fort) + Large + off-line Archer");
        CombatUI?.ShowCombatLog("Phase 1: Cast Obscuring Mist and verify 20% miss chance for adjacent attackers.");
        CombatUI?.ShowCombatLog("Phase 2: Separate attacker/target by >5 ft inside mist and verify 50% total concealment.");
        CombatUI?.ShowCombatLog("Phase 3: Recast mist in a second lane and verify persistent area indicators remain visible.");
    }

    private void ConfigureObscuringMistRangedOnlyTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats fighter1Stats = new CharacterStats(
            name: "Thoran Ironshield",
            level: 4,
            characterClass: "Fighter",
            str: 18, dex: 12, con: 18, wis: 10, intelligence: 10, cha: 8,
            bab: 4,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 4,
            atkRange: 1,
            baseHitDieHP: 38,
            raceName: "Dwarf"
        );

        Vector2Int fighter1Start = new Vector2Int(7, 8);
        Sprite fighter1Alive = IconLoader.GetToken("Fighter") ?? pcAliveFallback;
        PC1.Init(fighter1Stats, fighter1Start, fighter1Alive, pcDead);

        InventoryComponent fighter1Inventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        fighter1Inventory.Init(fighter1Stats);
        fighter1Inventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.WARHAMMER), EquipSlot.RightHand);
        fighter1Inventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_WOODEN), EquipSlot.LeftHand);
        fighter1Inventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        fighter1Inventory.CharacterInventory.RecalculateStats();

        CharacterStats fighter2Stats = new CharacterStats(
            name: "Valdor the Brave",
            level: 4,
            characterClass: "Fighter",
            str: 16, dex: 14, con: 16, wis: 10, intelligence: 10, cha: 12,
            bab: 4,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 4,
            atkRange: 1,
            baseHitDieHP: 36,
            raceName: "Human"
        );

        Vector2Int fighter2Start = new Vector2Int(9, 8);
        Sprite fighter2Alive = IconLoader.GetToken("Fighter") ?? pcAliveFallback;
        PC2.Init(fighter2Stats, fighter2Start, fighter2Alive, pcDead);

        InventoryComponent fighter2Inventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        fighter2Inventory.Init(fighter2Stats);
        fighter2Inventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        fighter2Inventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_WOODEN), EquipSlot.LeftHand);
        fighter2Inventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        fighter2Inventory.CharacterInventory.RecalculateStats();

        CharacterStats wizardStats = new CharacterStats(
            name: "Mira Veilbinder",
            level: 4,
            characterClass: "Wizard",
            str: 10, dex: 14, con: 14, wis: 12, intelligence: 17, cha: 10,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 24,
            raceName: "Human"
        );

        Vector2Int wizardStart = new Vector2Int(8, 7);
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC3.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC3.gameObject.GetComponent<InventoryComponent>() ?? PC3.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC3.gameObject.GetComponent<SpellcastingComponent>() ?? PC3.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            SpellNames.OBSCURING_MIST, SpellNames.MAGIC_MISSILE, SpellNames.SHIELD
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            SpellNames.OBSCURING_MIST, SpellNames.OBSCURING_MIST, SpellNames.MAGIC_MISSILE, SpellNames.SHIELD
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC3.gameObject.GetComponent<StatusEffectManager>() ?? PC3.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        ConcentrationManager wizardConcentrationMgr = PC3.gameObject.GetComponent<ConcentrationManager>() ?? PC3.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC3);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, true, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("╔═══════════════════════════════════════════════════════════════╗");
        CombatUI?.ShowCombatLog("║       OBSCURING MIST - RANGED COMBAT ONLY TEST               ║");
        CombatUI?.ShowCombatLog("╚═══════════════════════════════════════════════════════════════╝");
        CombatUI?.ShowCombatLog("");
        CombatUI?.ShowCombatLog("PARTY (center lane - intended to fight inside mist):");
        CombatUI?.ShowCombatLog("  • Thoran Ironshield (Fighter 4)");
        CombatUI?.ShowCombatLog("  • Valdor the Brave (Fighter 4)");
        CombatUI?.ShowCombatLog("  • Mira Veilbinder (Wizard 4, casts Obscuring Mist)");
        CombatUI?.ShowCombatLog("");
        CombatUI?.ShowCombatLog("RANGED ENEMIES (6-direction surround):");
        CombatUI?.ShowCombatLog("  NORTH:     Aelindra Swiftarrow (Longbow)");
        CombatUI?.ShowCombatLog("  NORTHEAST: Marcus Longshot (Longbow)");
        CombatUI?.ShowCombatLog("  EAST:      Garrick Strongbow (Composite Longbow)");
        CombatUI?.ShowCombatLog("  SOUTHEAST: Pip Quickfingers (Shortbow)");
        CombatUI?.ShowCombatLog("  SOUTH:     Borlin Ironbolt (Heavy Crossbow)");
        CombatUI?.ShowCombatLog("  WEST:      Kira Windrunner (Shortbow)");
        CombatUI?.ShowCombatLog("");
        CombatUI?.ShowCombatLog("BATTLEFIELD LAYOUT:");
        CombatUI?.ShowCombatLog("═══════════════════════════════════════════════════════════════");
        CombatUI?.ShowCombatLog("                    Aelindra (Longbow)");
        CombatUI?.ShowCombatLog("                             N");
        CombatUI?.ShowCombatLog("               Marcus      |      Pip");
        CombatUI?.ShowCombatLog("                  NE       |       SE");
        CombatUI?.ShowCombatLog("                            ");
        CombatUI?.ShowCombatLog("     Kira (W)  ←      [ PARTY IN MIST ]      →  Garrick (E)");
        CombatUI?.ShowCombatLog("                            ");
        CombatUI?.ShowCombatLog("                             S");
        CombatUI?.ShowCombatLog("                   Borlin (Crossbow)");
        CombatUI?.ShowCombatLog("═══════════════════════════════════════════════════════════════");
        CombatUI?.ShowCombatLog("");
        CombatUI?.ShowCombatLog("HOW TO TEST:");
        CombatUI?.ShowCombatLog("  1) Cast Obscuring Mist with Mira on party center.");
        CombatUI?.ShowCombatLog("  2) Verify ranged attacks against misted targets resolve with concealment checks.");
        CombatUI?.ShowCombatLog("  3) Move targets inside mist to validate last-known-position attacks and misses.");
        CombatUI?.ShowCombatLog("  4) Move one target outside mist; verify archers prioritize visible target.");
        CombatUI?.ShowCombatLog("  5) Confirm AI remains ranged-focused (no melee maneuvers/disarm/trip/grapple attempts).");
        CombatUI?.ShowCombatLog("  6) Compare longbow/shortbow/composite/heavy-crossbow behavior and damage output.");
        CombatUI?.ShowCombatLog("");
    }

    private void ConfigureDisruptUndeadTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Necromancer Theron",
            level: 3,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 16, cha: 10,
            bab: 1,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 18,
            raceName: "Human"
        );

        Vector2Int wizardStart = new Vector2Int(3, 9);
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            SpellNames.DETECT_MAGIC_WIZ, SpellNames.RAY_OF_FROST, SpellNames.ACID_SPLASH, SpellNames.DISRUPT_UNDEAD, SpellNames.READ_MAGIC
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            SpellNames.DISRUPT_UNDEAD, SpellNames.DISRUPT_UNDEAD, SpellNames.DISRUPT_UNDEAD, SpellNames.DISRUPT_UNDEAD, SpellNames.DISRUPT_UNDEAD
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        ConcentrationManager wizardConcentrationMgr = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC1);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("☀️ Disrupt Undead Test: Necromancer Theron (Wizard 3) prepared 5× Disrupt Undead.");
        CombatUI?.ShowCombatLog("   Test Mode - Easy to Hit is active for all enemies (very low AC / Touch AC).");
        CombatUI?.ShowCombatLog("   Procedure: use ranged touch attacks vs skeletons/zombie and verify 1d6 positive damage.");
        CombatUI?.ShowCombatLog("   Validation: cast at the living orc as a control target — Disrupt Undead should report no effect.");
    }

    private void PrepareSummonMonsterTestSpellSlots(
        SpellcastingComponent spellComp,
        string summonOneSpellId,
        string summonTwoSpellId,
        string levelOneFallbackAId,
        string levelOneFallbackBId,
        string levelTwoFallbackAId,
        string levelTwoFallbackBId)
    {
        if (spellComp == null || spellComp.SpellSlots == null || spellComp.SpellSlots.Count == 0)
            return;

        SpellData summonOne = SpellDatabase.GetSpell(summonOneSpellId);
        SpellData summonTwo = SpellDatabase.GetSpell(summonTwoSpellId);
        SpellData levelOneFallbackA = SpellDatabase.GetSpell(levelOneFallbackAId);
        SpellData levelOneFallbackB = SpellDatabase.GetSpell(levelOneFallbackBId);
        SpellData levelTwoFallbackA = SpellDatabase.GetSpell(levelTwoFallbackAId);
        SpellData levelTwoFallbackB = SpellDatabase.GetSpell(levelTwoFallbackBId);

        int levelOneSummonCount = 0;
        int levelTwoSummonCount = 0;

        for (int i = 0; i < spellComp.SpellSlots.Count; i++)
        {
            SpellSlot slot = spellComp.SpellSlots[i];
            if (slot == null)
                continue;

            SpellData toPrepare = null;

            if (slot.Level == 1)
            {
                if (summonOne != null && levelOneSummonCount < 2)
                {
                    toPrepare = summonOne;
                    levelOneSummonCount++;
                }
                else
                {
                    toPrepare = levelOneFallbackA ?? levelOneFallbackB;
                }
            }
            else if (slot.Level == 2)
            {
                if (summonTwo != null && levelTwoSummonCount < 2)
                {
                    toPrepare = summonTwo;
                    levelTwoSummonCount++;
                }
                else
                {
                    toPrepare = levelTwoFallbackA ?? levelTwoFallbackB;
                }
            }

            if (toPrepare != null)
                spellComp.PrepareSpellInSlot(i, toPrepare);
        }

        spellComp.SyncPreparedSpellsFromSlots();
        Debug.Log($"[SummonTest] Prepared summon loadout for {spellComp.Stats?.CharacterName}: {spellComp.GetSlotDetails()}");
    }

    private void ConfigureWizardSpellTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Archmage Theron",
            level: 20,
            characterClass: "Wizard",
            str: 8, dex: 16, con: 14, wis: 12, intelligence: 24, cha: 10,
            bab: 10,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 110,
            raceName: "Human"
        );
        wizardStats.CharacterAlignment = Alignment.TrueNeutral;

        Vector2Int wizardStart = new Vector2Int(3, 9);
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = null;
        wizardSpellComp.PreparedSpellSlotIds = null;
        wizardSpellComp.Init(wizardStats);
        AutoPopulateAndPrepareAllImplementedClassSpells(wizardSpellComp, "Wizard");

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("📘 Wizard Spell Test: Archmage Theron auto-prepared all implemented wizard spells.");
        CombatUI?.ShowCombatLog("   Target Dummy has AC 1, HP 50, and severe save penalties for deterministic spell validation.");
    }

    private void ConfigureTrueStrikeTestParty()
    {
        ConfigureWizardSpellTestParty();
        CombatUI?.ShowCombatLog("🎯 True Strike Test: Cast True Strike, then make one attack to verify +20 insight, concealment bypass, and one-use consumption.");
        CombatUI?.ShowCombatLog("   If you end your next turn without attacking, True Strike should expire automatically.");
    }

    private void ConfigureCharmPersonTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats enchanterStats = new CharacterStats(
            name: "Selene the Enchanter",
            level: 5,
            characterClass: "Wizard",
            str: 8, dex: 14, con: 12, wis: 12, intelligence: 18, cha: 12,
            bab: 2,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 26,
            raceName: "Human"
        );

        Vector2Int enchanterStart = new Vector2Int(3, 9);
        Sprite enchanterAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(enchanterStats, enchanterStart, enchanterAlive, pcDead);

        InventoryComponent enchanterInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        enchanterInventory.Init(enchanterStats);
        enchanterInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        enchanterInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent enchanterSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        enchanterSpellComp.KnownSpells.Clear();
        enchanterSpellComp.SelectedSpellIds = new List<string>
        {
            SpellNames.DETECT_MAGIC_WIZ, SpellNames.READ_MAGIC, SpellNames.CHARM_PERSON, SpellNames.MAGIC_MISSILE
        };
        enchanterSpellComp.PreparedSpellSlotIds = new List<string>
        {
            SpellNames.CHARM_PERSON, SpellNames.CHARM_PERSON, SpellNames.MAGIC_MISSILE, SpellNames.MAGIC_MISSILE
        };
        enchanterSpellComp.Init(enchanterStats);

        // Start slightly injured so charmed healers can immediately demonstrate supportive behavior.
        enchanterStats.TakeDamage(8);

        StatusEffectManager enchanterStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        enchanterStatusMgr.Init(enchanterStats);

        ConcentrationManager enchanterConcentration = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        enchanterConcentration.Init(enchanterStats, PC1);

        CharacterStats guardStats = new CharacterStats(
            name: "Rook the Guard",
            level: 5,
            characterClass: "Fighter",
            str: 16, dex: 12, con: 14, wis: 10, intelligence: 10, cha: 10,
            bab: 5,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 44,
            raceName: "Human"
        );

        Vector2Int guardStart = new Vector2Int(5, 9);
        Sprite guardAlive = IconLoader.GetToken("Fighter") ?? pcAliveFallback;
        PC2.Init(guardStats, guardStart, guardAlive, pcDead);

        InventoryComponent guardInventory = PC2.gameObject.GetComponent<InventoryComponent>() ?? PC2.gameObject.AddComponent<InventoryComponent>();
        guardInventory.Init(guardStats);
        guardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.LONGSWORD), EquipSlot.RightHand);
        guardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        guardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        guardInventory.CharacterInventory.RecalculateStats();

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("💞 Charm Person Test: Selene (Wizard 5) and Rook (Fighter 5) vs humanoid targets.");
        CombatUI?.ShowCombatLog("   Validation goals: humanoid-only + 4 HD cap, +5 Will if threatened by caster side, and charmed AI non-hostility/support behavior.");
        CombatUI?.ShowCombatLog("   Selene begins lightly injured so charmed heal-capable targets can demonstrate emergency aid.");
    }

    private void ConfigureSleepSpellTestParty()
    {
        ConfigureWizardSpellTestParty();

        if (PC1 != null && PC1.GetComponent<SpellcastingComponent>() is SpellcastingComponent spellComp)
        {
            spellComp.KnownSpells.Clear();
            spellComp.SelectedSpellIds = new List<string>
            {
                SpellNames.DETECT_MAGIC_WIZ,
                SpellNames.READ_MAGIC,
                SpellNames.SLEEP,
                SpellNames.MAGIC_MISSILE
            };
            spellComp.PreparedSpellSlotIds = new List<string>
            {
                SpellNames.SLEEP,
                SpellNames.SLEEP,
                SpellNames.MAGIC_MISSILE,
                SpellNames.MAGIC_MISSILE
            };
            spellComp.Init(PC1.Stats);
        }

        CombatUI?.ShowCombatLog("💤 Sleep Spell Test: Cast Sleep on clustered enemies to validate 4d4 HD pool, lowest-HD-first, 4 HD cap, and wake conditions.");
        CombatUI?.ShowCombatLog("   Use Aid Another → Wake Sleeping Ally to test manual wake action.");
    }

    private void ConfigureMirrorImageTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats wizardStats = new CharacterStats(
            name: "Mirror Mage Theron",
            level: 5,
            characterClass: "Wizard",
            str: 8,
            dex: 14,
            con: 12,
            wis: 12,
            intelligence: 16,
            cha: 10,
            bab: 2,
            armorBonus: 2,
            shieldBonus: 0,
            damageDice: 4,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 30,
            raceName: "Human"
        );

        Vector2Int wizardStart = new Vector2Int(9, 8);
        Sprite wizardAlive = IconLoader.GetToken("Wizard") ?? pcAliveFallback;
        PC1.Init(wizardStats, wizardStart, wizardAlive, pcDead);

        InventoryComponent wizardInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        wizardInventory.Init(wizardStats);
        wizardInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.QUARTERSTAFF), EquipSlot.RightHand);
        wizardInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent wizardSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        wizardSpellComp.KnownSpells.Clear();
        wizardSpellComp.SelectedSpellIds = new List<string>
        {
            SpellNames.DETECT_MAGIC_WIZ,
            SpellNames.READ_MAGIC,
            SpellNames.MAGIC_MISSILE,
            SpellNames.MIRROR_IMAGE,
            SpellNames.MAGE_ARMOR
        };
        wizardSpellComp.PreparedSpellSlotIds = new List<string>
        {
            SpellNames.MIRROR_IMAGE,
            SpellNames.MIRROR_IMAGE,
            SpellNames.MAGIC_MISSILE,
            SpellNames.MAGE_ARMOR
        };
        wizardSpellComp.Init(wizardStats);

        StatusEffectManager wizardStatusMgr = PC1.gameObject.GetComponent<StatusEffectManager>() ?? PC1.gameObject.AddComponent<StatusEffectManager>();
        wizardStatusMgr.Init(wizardStats);

        ConcentrationManager wizardConcentrationMgr = PC1.gameObject.GetComponent<ConcentrationManager>() ?? PC1.gameObject.AddComponent<ConcentrationManager>();
        wizardConcentrationMgr.Init(wizardStats, PC1);

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("🪞 Mirror Image Test Arena loaded: Mirror Mage Theron (Wizard 5, HP 30, AC 14).");
        CombatUI?.ShowCombatLog("   Turn 1 prompt: cast Mirror Image to spawn 1d4+1 clones around the caster.");
        CombatUI?.ShowCombatLog("   End-turn flow: use swap prompt to reposition with adjacent clone and watch target redirection.");
        CombatUI?.ShowCombatLog("   Validate outcomes: clone dissipates on hit, real caster can still be struck, and status icon/log tracks remaining images + duration.");
    }

    private void ConfigureClericSpellTestParty()
    {
        RaceDatabase.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Sprite pcAliveFallback = LoadSprite("Sprites/pc_alive");
        Sprite pcDead = LoadSprite("Sprites/pc_dead");

        CharacterStats clericStats = new CharacterStats(
            name: "High Priestess Ilyra",
            level: 20,
            characterClass: "Cleric",
            str: 14, dex: 12, con: 16, wis: 24, intelligence: 12, cha: 16,
            bab: 15,
            armorBonus: 4,
            shieldBonus: 2,
            damageDice: 8,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: 6,
            atkRange: 1,
            baseHitDieHP: 150,
            raceName: "Human"
        );
        clericStats.CharacterAlignment = Alignment.NeutralGood;

        Vector2Int clericStart = new Vector2Int(3, 9);
        Sprite clericAlive = IconLoader.GetToken("Cleric") ?? pcAliveFallback;
        PC1.Init(clericStats, clericStart, clericAlive, pcDead);

        InventoryComponent clericInventory = PC1.gameObject.GetComponent<InventoryComponent>() ?? PC1.gameObject.AddComponent<InventoryComponent>();
        clericInventory.Init(clericStats);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.MACE_HEAVY), EquipSlot.RightHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.SHIELD_HEAVY_STEEL), EquipSlot.LeftHand);
        clericInventory.CharacterInventory.DirectEquip(ItemDatabase.CloneItem(ItemIDs.CHAINMAIL), EquipSlot.Armor);
        clericInventory.CharacterInventory.RecalculateStats();

        SpellcastingComponent clericSpellComp = PC1.gameObject.GetComponent<SpellcastingComponent>() ?? PC1.gameObject.AddComponent<SpellcastingComponent>();
        clericSpellComp.KnownSpells.Clear();
        clericSpellComp.SelectedSpellIds = null;
        clericSpellComp.PreparedSpellSlotIds = null;
        clericSpellComp.Init(clericStats);
        AutoPopulateAndPrepareAllImplementedClassSpells(clericSpellComp, "Cleric");

        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, false, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, false, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, false, CombatUI != null ? CombatUI.PC4Panel : null);

        CombatUI?.ShowCombatLog("📖 Cleric Spell Test: High Priestess Ilyra auto-prepared all implemented cleric spells.");
        CombatUI?.ShowCombatLog("   Target Dummy has AC 1, HP 50, and severe save penalties for deterministic spell validation.");
    }

    private void AutoPopulateAndPrepareAllImplementedClassSpells(SpellcastingComponent spellComp, string className)
    {
        if (spellComp == null || spellComp.Stats == null)
            return;

        List<SpellData> implementedSpells = SpellDatabase.GetImplementedSpellsForClass(className);
        if (implementedSpells == null || implementedSpells.Count == 0)
            implementedSpells = SpellDatabase.GetSpellsForClass(className);

        spellComp.KnownSpells.Clear();
        if (implementedSpells != null)
            spellComp.KnownSpells.AddRange(implementedSpells);

        for (int i = 0; i < spellComp.SpellSlots.Count; i++)
        {
            SpellSlot existingSlot = spellComp.SpellSlots[i];
            if (existingSlot != null)
                existingSlot.Clear();
        }

        int maxSpellLevel = 0;
        for (int i = 0; i < spellComp.KnownSpells.Count; i++)
        {
            SpellData spell = spellComp.KnownSpells[i];
            if (spell != null && spell.SpellLevel > maxSpellLevel)
                maxSpellLevel = spell.SpellLevel;
        }

        EnsureSpellSlotArrayCapacity(spellComp, maxSpellLevel + 1);

        for (int level = 0; level <= maxSpellLevel; level++)
        {
            List<SpellData> spellsAtLevel = new List<SpellData>();
            for (int i = 0; i < spellComp.KnownSpells.Count; i++)
            {
                SpellData spell = spellComp.KnownSpells[i];
                if (spell != null && spell.SpellLevel == level)
                    spellsAtLevel.Add(spell);
            }

            int existingSlotCount = 0;
            for (int i = 0; i < spellComp.SpellSlots.Count; i++)
            {
                SpellSlot slot = spellComp.SpellSlots[i];
                if (slot != null && slot.Level == level)
                    existingSlotCount++;
            }

            int requiredSlots = Mathf.Max(existingSlotCount, spellsAtLevel.Count);
            while (existingSlotCount < requiredSlots)
            {
                spellComp.SpellSlots.Add(new SpellSlot(level));
                existingSlotCount++;
            }

            spellComp.SlotsMax[level] = requiredSlots;
            spellComp.SlotsRemaining[level] = requiredSlots;

            if (spellsAtLevel.Count == 0)
                continue;

            int cursor = 0;
            for (int slotIndex = 0; slotIndex < spellComp.SpellSlots.Count; slotIndex++)
            {
                SpellSlot slot = spellComp.SpellSlots[slotIndex];
                if (slot == null || slot.Level != level)
                    continue;

                SpellData toPrepare = spellsAtLevel[cursor % spellsAtLevel.Count];
                spellComp.PrepareSpellInSlot(slotIndex, toPrepare);
                cursor++;
            }
        }

        spellComp.SyncPreparedSpellsFromSlots();
        Debug.Log($"[SpellTest] Auto-populated {spellComp.KnownSpells.Count} implemented {className} spells for {spellComp.Stats.CharacterName}. Slots: {spellComp.GetSlotSummary()}");
    }

    private static void EnsureSpellSlotArrayCapacity(SpellcastingComponent spellComp, int requiredLength)
    {
        int targetLength = Mathf.Max(1, requiredLength);

        if (spellComp.SlotsMax == null || spellComp.SlotsMax.Length < targetLength)
        {
            int[] resizedMax = new int[targetLength];
            if (spellComp.SlotsMax != null)
            {
                for (int i = 0; i < spellComp.SlotsMax.Length; i++)
                    resizedMax[i] = spellComp.SlotsMax[i];
            }
            spellComp.SlotsMax = resizedMax;
        }

        if (spellComp.SlotsRemaining == null || spellComp.SlotsRemaining.Length < targetLength)
        {
            int[] resizedRemaining = new int[targetLength];
            if (spellComp.SlotsRemaining != null)
            {
                for (int i = 0; i < spellComp.SlotsRemaining.Length; i++)
                    resizedRemaining[i] = spellComp.SlotsRemaining[i];
            }
            spellComp.SlotsRemaining = resizedRemaining;
        }
    }

    private void RestoreStandardPartyLayout()
    {
        SetPCActiveState(PC1, true, CombatUI != null ? CombatUI.PC1Panel : null);
        SetPCActiveState(PC2, true, CombatUI != null ? CombatUI.PC2Panel : null);
        SetPCActiveState(PC3, true, CombatUI != null ? CombatUI.PC3Panel : null);
        SetPCActiveState(PC4, true, CombatUI != null ? CombatUI.PC4Panel : null);
    }

    private static void SetPCActiveState(CharacterController pc, bool active, GameObject panel)
    {
        if (pc != null && pc.gameObject != null)
            pc.gameObject.SetActive(active);

        if (panel != null)
            panel.SetActive(active);
    }

    // ========== DEFAULT CHARACTER SETUP (Quick Start / No Creation UI) ==========
}

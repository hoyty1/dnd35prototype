using System;
using System.Collections.Generic;
using System.Linq;
using DND35.AI;
using DND35.AI.Profiles;
using DND35.Magic;
using UnityEngine;
using UnityEngine.UI;
using DND35e.Identifiers;

/// <summary>
/// GameManager partial class: NPC/Enemy Setup &amp; Initialization
/// 
/// Contains all NPC spawning and configuration logic:
/// - SetupEnemyEncounter: Spawns enemies from encounter definition
/// - SetupNPCIcons: Creates visual representations for NPCs
/// - InitializeNPCFromDefinition: Configures NPC from database definition
/// - ApplyScenarioSpawnOverrides: Test-specific NPC overrides
/// - AI profile assignment
/// 
/// Extracted from main GameManager.cs to reduce file size.
/// </summary>
public partial class GameManager
{
    // ═══════════════════════════════════════════════════════════════════
    //  NPC/ENEMY SETUP &amp; INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════

    private void SetupEnemyEncounter(List<string> enemyIds)
    {
        NPCDatabase.Init();
        ItemDatabase.Init();

        _npcAIBehaviors.Clear();
        _activeTurnUndeadTrackers.Clear();

        Sprite npcAliveFallback = LoadSprite("Sprites/npc_enemy_alive");
        Sprite npcDead = LoadSprite("Sprites/npc_enemy_dead");

        int spawnCount = enemyIds != null ? Mathf.Min(enemyIds.Count, NPCs.Count) : 0;

        for (int i = 0; i < NPCs.Count; i++)
        {
            CharacterController npc = NPCs[i];
            if (npc == null) continue;

            if (i >= spawnCount)
            {
                npc.gameObject.SetActive(false);
                if (CombatUI != null && i < CombatUI.NPCPanels.Count && CombatUI.NPCPanels[i].Panel != null)
                    CombatUI.NPCPanels[i].Panel.SetActive(false);
                continue;
            }

            string enemyId = enemyIds[i];
            NPCDefinition sourceDef = NPCDatabase.Get(enemyId);
            NPCDefinition def = BuildEncounterDefinitionForSpawn(enemyId, sourceDef, i);
            if (def == null)
            {
                Debug.LogError($"[GameManager] Unknown enemy ID: {enemyId}");
                npc.gameObject.SetActive(false);
                continue;
            }

            npc.gameObject.SetActive(true);
            if (CombatUI != null && i < CombatUI.NPCPanels.Count && CombatUI.NPCPanels[i].Panel != null)
                CombatUI.NPCPanels[i].Panel.SetActive(true);

            Vector2Int pos;
            if ((_isGrappleTestEncounter || _isFeintSneakTestEncounter) && i == 0 && PC1 != null)
            {
                // Spawn adjacent in dedicated mechanics test encounters.
                pos = PC1.GridPosition + Vector2Int.right;
            }
            else if (_isGreaseTestEncounter && i < GreaseTestSpawnPositions.Length)
            {
                // Cluster all enemies into a tight 2x2 for 10-ft grease area and repeated grapple attempts.
                pos = GreaseTestSpawnPositions[i];
            }
            else if (_isTurnUndeadTestEncounter && i < TurnUndeadTestSpawnPositions.Length)
            {
                // Explicit 15-undead test formation (front skeletons, mid wights, back skeletons).
                pos = TurnUndeadTestSpawnPositions[i];
            }
            else if (_isArmorTargetingTestEncounter && i < ArmorTargetingTestSpawnPositions.Length)
            {
                // Position skeleton archers at range so armor-priority targeting is easy to observe.
                pos = ArmorTargetingTestSpawnPositions[i];
            }
            else if (_isTigerHuntTestEncounter && i < TigerHuntTestSpawnPositions.Length)
            {
                // Place tiger with enough lane length to charge wounded prey and trigger pounce behavior.
                pos = TigerHuntTestSpawnPositions[i];
            }
            else if (_isOgreBattleTestEncounter && i < OgreBattleTestSpawnPositions.Length)
            {
                // Spawn controllable dire tiger near the wizard with both ogres advancing from the far side.
                pos = OgreBattleTestSpawnPositions[i];
            }
            else if (_isShieldBashTestEncounter && i < ShieldBashTestSpawnPositions.Length)
            {
                // Keep one melee enemy adjacent to each test fighter so shield-bash AC differences are obvious.
                pos = ShieldBashTestSpawnPositions[i];
            }
            else if (_isCelestialTemplateTestEncounter && i < CelestialTemplateTestSpawnPositions.Length)
            {
                // Keep celestial allies close to the cleric and undead on the opposite side.
                pos = CelestialTemplateTestSpawnPositions[i];
            }
            else if (_isFiendishTemplateTestEncounter && i < FiendishTemplateTestSpawnPositions.Length)
            {
                // Keep fiendish allies near the necromancer with good enemies opposite for Smite Good demonstrations.
                pos = FiendishTemplateTestSpawnPositions[i];
            }
            else if (_isSummonMonsterTestEncounter && i < SummonMonsterTestSpawnPositions.Length)
            {
                // Keep targets spread so summon placement and command behavior can be observed.
                pos = SummonMonsterTestSpawnPositions[i];
            }
            else if (_isProtectionFromEvilTestEncounter && i < ProtectionFromEvilTestSpawnPositions.Length)
            {
                // Place enemies so all three protection clauses are exercised quickly (charm spell, summoned contact, regular melee).
                pos = ProtectionFromEvilTestSpawnPositions[i];
            }
            else if (_isWindDispersionTestEncounter && i < WindDispersionTestSpawnPositions.Length)
            {
                // Build a straight wind lane + one off-axis archer to validate line-of-effect and concealment interactions.
                pos = WindDispersionTestSpawnPositions[i];
            }
            else if (_isObscuringMistRangedOnlyTestEncounter && i < ObscuringMistRangedOnlySpawnPositions.Length)
            {
                // Place six ranged attackers around the central mist zone to test concealed-target ranged AI behavior.
                pos = ObscuringMistRangedOnlySpawnPositions[i];
            }
            else if (_isWizardSpellTestEncounter && i < WizardSpellTestSpawnPositions.Length)
            {
                // Keep the dummy in a clean line with the wizard for single-target + AoE validation.
                pos = WizardSpellTestSpawnPositions[i];
            }
            else if (_isClericSpellTestEncounter && i < ClericSpellTestSpawnPositions.Length)
            {
                // Mirror wizard test spacing so cleric spell coverage can be compared directly.
                pos = ClericSpellTestSpawnPositions[i];
            }
            else if (_isMirrorImageTestEncounter && i < MirrorImageTestSpawnPositions.Length)
            {
                // Cardinal ring around the central wizard (≈25 ft) keeps all archers in LOS for clone-target validation.
                pos = MirrorImageTestSpawnPositions[i];
            }
            else
            {
                pos = (i < EncounterSpawnPositions.Length)
                    ? EncounterSpawnPositions[i]
                    : new Vector2Int(15 + i, 10);
            }

            // Try class-specific monster token; fallback to generic NPC sprite
            string monsterType = IconLoader.DetermineMonsterType(def.Name);
            Sprite npcAlive = null;
            if (!string.IsNullOrEmpty(monsterType))
                npcAlive = IconLoader.GetToken(monsterType);
            if (npcAlive == null)
                npcAlive = npcAliveFallback;

            InitializeNPCFromDefinition(npc, def, pos, npcAlive, npcDead);

            if (npc.Stats != null && string.Equals(enemyId, "target_dummy", StringComparison.Ordinal))
            {
                // Force extremely low saves for deterministic save-or-suck / save-for-half spell validation.
                npc.Stats.MoraleSaveBonus = -10;
                Debug.Log($"[SpellTest] Applied target dummy save penalty: F={npc.Stats.FortitudeSave}, R={npc.Stats.ReflexSave}, W={npc.Stats.WillSave}");
            }

            _npcAIBehaviors.Add(def.AIBehavior);

            ApplyScenarioSpawnOverrides(enemyId, npc, i);
            ApplyDisruptUndeadTestEasyHitOverrides(enemyId, npc);

            if (_isArmorTargetingTestEncounter && string.Equals(enemyId, "skeleton_archer", StringComparison.Ordinal))
            {
                npc.aiProfile = ScriptableObject.CreateInstance<RangedAIProfile>();
                npc.Tags.AddTag("Uses Armor-Based Targeting");
                Debug.Log($"[ArmorTargetingTest] Overriding {npc.Stats.CharacterName} to Ranged profile for armor-priority targeting validation.");
            }

            if (_isShieldBashTestEncounter)
            {
                // Keep shield bash validation deterministic: basic melee pressure only, no trip/disarm/grapple maneuver selection.
                npc.aiProfile = ScriptableObject.CreateInstance<UndeadMindlessAIProfile>();
                npc.Tags.AddTag("ShieldBashTestSimpleMeleeAI");
                Debug.Log($"[ShieldBashTest] Overriding {npc.Stats.CharacterName} to simple melee-only AI profile.");
            }

            // Only apply color tint if using the generic fallback sprite
            SpriteRenderer sr = npc.GetComponent<SpriteRenderer>();
            if (sr != null && npcAlive == npcAliveFallback) sr.color = def.SpriteColor;

            if (i < CombatUI.NPCPanels.Count)
            {
                var panelUI = CombatUI.NPCPanels[i];
                if (panelUI.Panel != null)
                {
                    Image panelImg = panelUI.Panel.GetComponent<UnityEngine.UI.Image>();
                    if (panelImg != null) panelImg.color = def.PanelColor;
                }
                if (panelUI.NameText != null) panelUI.NameText.color = def.NameColor;
            }

            string templateLog = (def.AppliedTemplateIds != null && def.AppliedTemplateIds.Count > 0)
                ? $" | Templates: {string.Join(",", def.AppliedTemplateIds)}"
                : string.Empty;
            Debug.Log($"[GameManager] Spawned NPC {i}: {def.Name} (Lv {def.Level} {def.CharacterClass}) at ({pos.x},{pos.y}) — AI: {def.AIBehavior}{templateLog}");
            if (!string.IsNullOrWhiteSpace(def.AITargetPriority))
                CombatUI?.ShowCombatLog($"  {npc.Stats.CharacterName} priority target: {def.AITargetPriority}");

            if (_isGreaseTestEncounter && npc.Stats != null)
            {
                int grappleMod = npc.GetGrappleModifier();
                string weaponLabel = "unarmed";
                if (def.EquipmentIds != null)
                {
                    for (int eqIndex = 0; eqIndex < def.EquipmentIds.Count; eqIndex++)
                    {
                        EquipmentSlotPair eq = def.EquipmentIds[eqIndex];
                        if (eq != null && eq.Slot == EquipSlot.RightHand && !string.IsNullOrWhiteSpace(eq.ItemId))
                        {
                            weaponLabel = eq.ItemId;
                            break;
                        }
                    }
                }

                CombatUI?.ShowCombatLog($"✓ {npc.Stats.CharacterName}: Grapple {CharacterStats.FormatMod(grappleMod)}, Reflex {CharacterStats.FormatMod(npc.Stats.ReflexSave)}, Weapon {weaponLabel}");
            }
        }

        if (_isGreaseTestEncounter)
        {
            CombatUI?.ShowCombatLog("🧪 Grease scenario loaded: enemies are clustered in a 2x2 square (12,5) to (13,6).");
            CombatUI?.ShowCombatLog("   Use Grease (Armor) on Slippery Sam first, then validate Area and Object modes.");
            CombatUI?.ShowCombatLog("   Enemies are scripted to prioritize Slippery Sam for grapple pressure.");
        }

        if (_isMirrorImageTestEncounter)
        {
            CombatUI?.ShowCombatLog("🪞 Mirror Image Test Arena enemy ring: 4 goblin archers spawned at N/E/S/W ~25 ft from center.");
            CombatUI?.ShowCombatLog("   Expected flow: cast Mirror Image, observe clone visuals, end-turn swap prompt, and clone-target redirection in combat log.");
            CombatUI?.ShowCombatLog("   Repeat until all clones dissipate, then confirm real caster hits and remaining duration behavior.");
        }

        if (_isProtectionFromEvilTestEncounter)
        {
            CombatUI?.ShowCombatLog("╔═══════════════════════════════════════════════════════╗");
            CombatUI?.ShowCombatLog("║   CONTROL TESTS (Non-Evil + Evil Save Comparison)    ║");
            CombatUI?.ShowCombatLog("╚═══════════════════════════════════════════════════════╝");
            CombatUI?.ShowCombatLog("");
            CombatUI?.ShowCombatLog("✓ Neutral Bandit (TRUE NEUTRAL): NO AC bonus from ward expected.");
            CombatUI?.ShowCombatLog("✓ Neutral Mage (TRUE NEUTRAL): Daze allows normal save (no +2 bonus).");
            CombatUI?.ShowCombatLog("✓ Evil Acolyte (NEUTRAL EVIL): Daze allows save with +2 protection bonus.");
            CombatUI?.ShowCombatLog("");
            CombatUI?.ShowCombatLog("AC BONUS TEST:");
            CombatUI?.ShowCombatLog("  Evil Goblin      → Player AC includes +2 deflection");
            CombatUI?.ShowCombatLog("  Neutral Bandit   → Player AC has no protection deflection bonus");
            CombatUI?.ShowCombatLog("");
            CombatUI?.ShowCombatLog("SAVE BONUS TEST:");
            CombatUI?.ShowCombatLog("  Evil Acolyte Daze   → Will save gains +2 resistance bonus");
            CombatUI?.ShowCombatLog("  Neutral Mage Daze   → Will save remains base value (no +2)");
            CombatUI?.ShowCombatLog("");
            CombatUI?.ShowCombatLog("MENTAL CONTROL TEST:");
            CombatUI?.ShowCombatLog("  Evil Enchanter Charm Person → BLOCKED completely by protection");
            CombatUI?.ShowCombatLog("  Daze (both evil/neutral casters) → NOT blocked, only save mechanics apply");
            CombatUI?.ShowCombatLog("");
        }

        // Legacy NPC field points to first active enemy
        NPC = null;
        for (int i = 0; i < NPCs.Count; i++)
        {
            if (NPCs[i] != null && NPCs[i].gameObject.activeSelf)
            {
                NPC = NPCs[i];
                break;
            }
        }

        // Hide legacy single-NPC panel since we're using multi-panels
        if (CombatUI.NPCNameText != null)
            CombatUI.NPCNameText.transform.parent.gameObject.SetActive(false);
    }

    private void ApplyDisruptUndeadTestEasyHitOverrides(string enemyId, CharacterController npc)
    {
        if (!_isDisruptUndeadTestEncounter || npc == null || npc.Stats == null)
            return;

        CharacterStats stats = npc.Stats;

        // Test-only override: lower all defenses so Disrupt Undead ranged touch attacks land consistently.
        stats.BaseDEX = 1;
        stats.DEX = 1;
        stats.ArmorBonus = 0;
        stats.ShieldBonus = 0;
        stats.SpellACBonus = 0;
        stats.DeflectionBonus = 0;
        stats.NaturalArmorBonus = 1;

        int loweredAC = stats.ArmorClass;
        int loweredTouchAC = SpellcastingComponent.GetTouchAC(stats);

        string enemyLabel = string.IsNullOrEmpty(stats.CharacterName) ? enemyId : stats.CharacterName;
        CombatUI?.ShowCombatLog($"🧪 Test Mode - Easy to Hit: {enemyLabel} defenses lowered (AC {loweredAC}, Touch AC {loweredTouchAC}).");
        Debug.Log($"[DisruptUndeadTest] Easy-hit override applied to {enemyLabel}: AC={loweredAC}, TouchAC={loweredTouchAC}");
    }

    private void ApplyScenarioSpawnOverrides(string enemyId, CharacterController npc, int spawnIndex)
    {
        if (npc == null)
            return;

        if (_isOgreBattleTestEncounter
            && spawnIndex == 0
            && string.Equals(enemyId, "dire_tiger", StringComparison.Ordinal))
        {
            // IMPORTANT: Keep NPCDatabase definitions scenario-agnostic.
            // Ogre Battle needs an allied/controllable tiger, so we override allegiance/control at spawn time
            // instead of baking scenario flags (IsAlly/IsControllable) into the shared NPC record.
            npc.ConfigureTeamControl(CharacterTeam.Player, controllable: true);
            npc.Tags.AddTag("ScenarioOverride:OgreBattleAlly");
            Debug.Log("[OgreBattleTest] Applied spawn-time override for dire_tiger: Team=Player, IsControllable=true.");
        }

        if (_isCelestialTemplateTestEncounter)
        {
            if (spawnIndex == 0 || spawnIndex == 1)
            {
                npc.ConfigureTeamControl(CharacterTeam.Player, controllable: true);
                npc.Stats.CharacterAlignment = Alignment.NeutralGood;
                npc.Tags.AddTag("ScenarioOverride:CelestialTemplateAlly");
                Debug.Log($"[CelestialTemplateTest] Ally override applied to {enemyId}: Team=Player, IsControllable=true, Alignment=NeutralGood.");
            }
            else
            {
                npc.ConfigureTeamControl(CharacterTeam.Enemy, controllable: false);
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:CelestialTemplateUndeadEnemy");
                Debug.Log($"[CelestialTemplateTest] Enemy override applied to {enemyId}: Team=Enemy, Alignment=NeutralEvil.");
            }
        }

        if (_isFiendishTemplateTestEncounter)
        {
            if (spawnIndex == 0 || spawnIndex == 1)
            {
                npc.ConfigureTeamControl(CharacterTeam.Player, controllable: true);
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:FiendishTemplateAlly");
                Debug.Log($"[FiendishTemplateTest] Ally override applied to {enemyId}: Team=Player, IsControllable=true, Alignment=NeutralEvil.");
            }
            else
            {
                npc.ConfigureTeamControl(CharacterTeam.Enemy, controllable: false);
                npc.Stats.CharacterAlignment = spawnIndex == 2 ? Alignment.LawfulGood : Alignment.NeutralGood;
                npc.Tags.AddTag("ScenarioOverride:FiendishTemplateGoodEnemy");
                Debug.Log($"[FiendishTemplateTest] Enemy override applied to {enemyId}: Team=Enemy, Alignment={npc.Stats.CharacterAlignment}.");
            }
        }

        if (_isSummonMonsterTestEncounter)
        {
            npc.ConfigureTeamControl(CharacterTeam.Enemy, controllable: false);

            Alignment summonTestAlignment = Alignment.NeutralEvil;
            if (string.Equals(enemyId, "orc_berserker", StringComparison.Ordinal))
                summonTestAlignment = Alignment.ChaoticEvil;
            else if (string.Equals(enemyId, "goblin_warchief", StringComparison.Ordinal))
                summonTestAlignment = Alignment.LawfulEvil;
            else if (string.Equals(enemyId, "skeleton_archer", StringComparison.Ordinal))
                summonTestAlignment = Alignment.NeutralEvil;

            npc.Stats.CharacterAlignment = summonTestAlignment;
            npc.Tags.AddTag("ScenarioOverride:SummonMonsterTestEvilEnemy");
            Debug.Log($"[SummonMonsterTest] Enemy override applied to {enemyId}: Team=Enemy, Alignment={npc.Stats.CharacterAlignment}.");
        }

        if (_isProtectionFromEvilTestEncounter)
        {
            npc.ConfigureTeamControl(CharacterTeam.Enemy, controllable: false);

            if (string.Equals(enemyId, "evil_enchanter_test", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilEnchanter");
                Debug.Log("[ProtectionFromEvilTest] Enchanter override applied: Team=Enemy, Alignment=NeutralEvil, Charm Person loadout active.");
            }
            else if (string.Equals(enemyId, "fiendish_wolf", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilSummonedFiend");

                CharacterController summonCaster = null;
                if (NPCs != null && NPCs.Count > 0)
                    summonCaster = NPCs[0];
                if (summonCaster == null)
                    summonCaster = npc;

                RegisterScenarioSummonedCreature(npc, summonCaster, durationRounds: 50, sourceSpellId: "scenario_setup_protection_from_evil_test");
                Debug.Log("[ProtectionFromEvilTest] Fiendish wolf registered as summoned creature for barrier validation.");
            }
            else if (string.Equals(enemyId, "evil_goblin_test", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilMeleeEnemy");
            }
            else if (string.Equals(enemyId, "neutral_bandit_test", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.TrueNeutral;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilNeutralMeleeControl");
                Debug.Log("[ProtectionFromEvilTest] Neutral bandit control applied: no deflection bonus should trigger.");
            }
            else if (string.Equals(enemyId, "neutral_mage_test", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.TrueNeutral;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilNeutralCasterControl");
                Debug.Log("[ProtectionFromEvilTest] Neutral mage control applied: Daze should not grant +2 protection save bonus.");
            }
            else if (string.Equals(enemyId, "evil_acolyte_test", StringComparison.Ordinal))
            {
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilEvilCasterControl");
                Debug.Log("[ProtectionFromEvilTest] Evil acolyte control applied: Daze should grant +2 protection save bonus.");
            }
            else
            {
                npc.Stats.CharacterAlignment = Alignment.NeutralEvil;
                npc.Tags.AddTag("ScenarioOverride:ProtectionFromEvilFallbackEnemy");
            }
        }
    }

    private void InitializeNPCFromDefinition(CharacterController npc, NPCDefinition def,
        Vector2Int pos, Sprite alive, Sprite dead)
    {
        int hitDice = Mathf.Max(1, def.HitDice > 0 ? def.HitDice : def.Level);
        CreatureTypeProgression creatureProgression = CreatureTypeProgressionDatabase.GetFromString(def.CreatureType);

        BABProgression babProgression = def.BABOverride ?? creatureProgression.BAB;
        SaveProgression fortitudeProgression = def.FortitudeSaveOverride ?? creatureProgression.Fortitude;
        SaveProgression reflexProgression = def.ReflexSaveOverride ?? creatureProgression.Reflex;
        SaveProgression willProgression = def.WillSaveOverride ?? creatureProgression.Will;

        int computedBab = ProgressionCalculator.CalculateBAB(babProgression, hitDice);
        int resolvedBab = def.BaseAttackBonusOverride ?? computedBab;
        int computedBaseHitDieHp = def.BaseHitDieHP > 0
            ? def.BaseHitDieHP
            : ProgressionCalculator.CalculateAverageHpFromHitDice(creatureProgression.HitDie, hitDice);

        CharacterStats stats = new CharacterStats(
            name: def.Name,
            level: def.Level,
            characterClass: def.CharacterClass,
            str: def.STR, dex: def.DEX, con: def.CON,
            wis: def.WIS, intelligence: def.INT, cha: def.CHA,
            bab: resolvedBab,
            armorBonus: 0,
            shieldBonus: 0,
            damageDice: 0,
            damageCount: 1,
            bonusDamage: 0,
            baseSpeed: def.BaseSpeed,
            atkRange: 1,
            baseHitDieHP: computedBaseHitDieHp
        );

        stats.SetNaturalAttacks(def.NaturalAttacks);

        stats.HitDice = hitDice;
        stats.UseCreatureTypeProgression = true;
        stats.CreatureBABProgression = babProgression;
        stats.BaseAttackBonusOverride = def.BaseAttackBonusOverride;
        stats.CreatureFortitudeProgression = fortitudeProgression;
        stats.CreatureReflexProgression = reflexProgression;
        stats.CreatureWillProgression = willProgression;

        foreach (string tag in def.CreatureTags)
            stats.CreatureTags.Add(tag);

        foreach (string featName in def.Feats)
        {
            if (!stats.HasFeat(featName))
                stats.Feats.Add(featName);
        }
        if (!string.IsNullOrEmpty(def.WeaponFocusChoice))
            stats.WeaponFocusChoice = def.WeaponFocusChoice;

        FeatManager.ApplyPassiveFeats(stats);
        stats.SourceNpcDefinitionId = def.Id;
        stats.ChallengeRating = def.ChallengeRating;
        stats.CreatureType = string.IsNullOrEmpty(def.CreatureType) ? "Humanoid" : def.CreatureType;
        stats.MaterialComposition = def.MaterialComposition;
        stats.SetBaseSizeCategory(def.SizeCategory);
        stats.IsTallCreature = def.IsTallCreature;
        stats.NaturalArmorBonus = def.NaturalArmorBonus;
        stats.HasTripAttack = def.HasTripAttack;
        stats.TripAttackCheckBonus = def.TripAttackCheckBonus;
        stats.HasImprovedGrab = def.HasImprovedGrab;
        stats.ImprovedGrabTriggerAttackName = def.ImprovedGrabTriggerAttackName;
        stats.HasPounce = def.HasPounce;
        stats.HasRake = def.HasRake;
        stats.HasScent = def.HasScent;
        stats.SetRakeAttack(def.RakeAttack);

        // Ensure size-derived natural reach is respected for creatures larger than Medium.
        if (stats.AttackRange < stats.NaturalReachSquares)
            stats.AttackRange = stats.NaturalReachSquares;

        // Apply innate mitigation profile from enemy definition
        if (def.DamageReductionAmount > 0)
            stats.AddDamageReduction(def.DamageReductionAmount, def.DamageReductionBypass, def.DamageReductionRangedOnly);

        if (def.DamageResistances != null)
        {
            foreach (var res in def.DamageResistances)
            {
                if (res != null && res.Amount > 0)
                    stats.AddDamageResistance(res.Type, res.Amount);
            }
        }

        if (def.DamageImmunities != null)
        {
            foreach (var imm in def.DamageImmunities)
                stats.AddDamageImmunity(imm);
        }

        if (def.Immunities != null)
            stats.Immunities.MergeFrom(def.Immunities);

        stats.ApplyMindlessTrait(def.IsMindless);
        stats.IsSwarm = def.IsSwarm || (def.SwarmTraits != null && def.SwarmTraits.IsSwarm);
        stats.SwarmTraits = def.SwarmTraits != null
            ? new SwarmTraits
            {
                IsSwarm = def.SwarmTraits.IsSwarm,
                SwarmDamage = def.SwarmTraits.SwarmDamage,
                SwarmDamageDice = def.SwarmTraits.SwarmDamageDice,
                DistractionDC = def.SwarmTraits.DistractionDC,
                HasPoison = def.SwarmTraits.HasPoison,
                HasDisease = def.SwarmTraits.HasDisease,
                HasWounding = def.SwarmTraits.HasWounding,
                SwarmDamageType = def.SwarmTraits.SwarmDamageType,
                PoisonId = def.SwarmTraits.PoisonId,
                PoisonDcModifier = def.SwarmTraits.PoisonDcModifier,
                DiseaseType = def.SwarmTraits.DiseaseType,
                DiseaseDcModifier = def.SwarmTraits.DiseaseDcModifier
            }
            : new SwarmTraits();

        if (stats.IsSwarm)
            stats.SwarmTraits.IsSwarm = true;

        stats.CanMakeAttacksOfOpportunity = def.CanMakeAttacksOfOpportunity;

        stats.SpellResistance = Mathf.Max(0, def.SpellResistance);

        bool hasCelestialTemplate = false;
        bool hasFiendishTemplate = false;
        if (def.AppliedTemplateIds != null)
        {
            for (int i = 0; i < def.AppliedTemplateIds.Count; i++)
            {
                string templateId = def.AppliedTemplateIds[i];
                if (string.IsNullOrWhiteSpace(templateId))
                    continue;

                if (string.Equals(templateId, "celestial", StringComparison.OrdinalIgnoreCase))
                    hasCelestialTemplate = true;
                else if (string.Equals(templateId, "fiendish", StringComparison.OrdinalIgnoreCase))
                    hasFiendishTemplate = true;
            }
        }

        stats.IsCelestialTemplate = hasCelestialTemplate;
        stats.IsFiendishTemplate = hasFiendishTemplate;
        stats.HasTemplateSmiteEvil = def.GainsSmiteEvil;
        stats.HasTemplateSmiteGood = def.GainsSmiteGood;
        stats.TemplateSmiteUsed = false;

        if (def.SpecialAbilities != null)
        {
            for (int i = 0; i < def.SpecialAbilities.Count; i++)
                stats.AddSpecialAbility(def.SpecialAbilities[i]);
        }

        npc.Init(stats, pos, alive, dead);
        npc.ConfigureBombardierAcidSprayCooldown(0);
        npc.ConfigureRegeneration(def.RegenerationAmount, def.RegenerationSuppressedBy);

        // Monster special abilities (Tiers 1-3)
        if (def.IsIncorporeal)
            npc.ConfigureIncorporeal(true);
        if (def.BreathWeapon != null)
            npc.ConfigureBreathWeapon(def.BreathWeapon);
        if (def.Engulf != null)
            npc.ConfigureEngulf(def.Engulf);
        if (def.StenchAuraDC > 0)
            npc.ConfigureStenchAura(def.StenchAuraDC, def.StenchAuraRange);
        if (def.AuraAbility != null)
            npc.ConfigureAuraAbility(def.AuraAbility);

        CharacterTeam npcTeam = def.IsAlly ? CharacterTeam.Player : CharacterTeam.Enemy;
        npc.ConfigureTeamControl(npcTeam, def.IsControllable);

        InventoryComponent inv = npc.gameObject.GetComponent<InventoryComponent>();
        if (inv == null) inv = npc.gameObject.AddComponent<InventoryComponent>();
        inv.Init(stats);

        foreach (var eq in def.EquipmentIds)
        {
            ItemData item = ItemDatabase.CloneItem(eq.ItemId);
            if (item != null)
                inv.CharacterInventory.DirectEquip(item, eq.Slot);
            else
                Debug.LogWarning($"[GameManager] Item not found: {eq.ItemId} for {def.Name}");
        }

        foreach (string itemId in def.BackpackItemIds)
        {
            ItemData item = ItemDatabase.CloneItem(itemId);
            if (item != null)
                inv.CharacterInventory.AddItem(item);
        }

        inv.CharacterInventory.RecalculateStats();

        bool shouldInitSpellcasting = stats.IsSpellcaster
            && ((def.KnownSpellIds != null && def.KnownSpellIds.Count > 0)
                || (def.PreparedSpellSlotIds != null && def.PreparedSpellSlotIds.Count > 0));

        if (shouldInitSpellcasting)
        {
            SpellcastingComponent spellComp = npc.gameObject.GetComponent<SpellcastingComponent>()
                ?? npc.gameObject.AddComponent<SpellcastingComponent>();
            spellComp.KnownSpells.Clear();
            spellComp.SelectedSpellIds = def.KnownSpellIds != null && def.KnownSpellIds.Count > 0
                ? new List<string>(def.KnownSpellIds)
                : null;
            spellComp.PreparedSpellSlotIds = def.PreparedSpellSlotIds != null && def.PreparedSpellSlotIds.Count > 0
                ? new List<string>(def.PreparedSpellSlotIds)
                : null;
            spellComp.Init(stats);

            Debug.Log($"[GameManager] Initialized NPC spellcasting for {def.Name}: {spellComp.GetSlotSummary()}");
        }

        // Initialize StatusEffectManager for NPC duration tracking
        var statusMgr = npc.gameObject.GetComponent<StatusEffectManager>();
        if (statusMgr == null)
            statusMgr = npc.gameObject.AddComponent<StatusEffectManager>();
        statusMgr.Init(stats);

        // Initialize ConcentrationManager for NPC concentration tracking
        var concMgr = npc.gameObject.GetComponent<ConcentrationManager>();
        if (concMgr == null)
            concMgr = npc.gameObject.AddComponent<ConcentrationManager>();
        concMgr.Init(stats, npc);

        npc.aiProfile = BuildRuntimeAIProfile(def);
        npc.EnemyUseCoupDeGraceOverride = def.UseCoupDeGrace;
        npc.PriorityTargetName = string.IsNullOrWhiteSpace(def.AITargetPriority) ? null : def.AITargetPriority;

        Debug.Log($"[GameManager] {def.Name}: HP {stats.MaxHP} AC {stats.ArmorClass} " +
                  $"Atk {CharacterStats.FormatMod(stats.AttackBonus)} Speed {stats.MoveRange}sq " +
                  $"Type={stats.CreatureType} HD={stats.HitDice} BABProg={stats.CreatureBABProgression} " +
                  $"Saves(F/R/W)={stats.ClassFortSave}/{stats.ClassRefSave}/{stats.ClassWillSave}");
    }

    private DND35.AI.AIProfile BuildRuntimeAIProfile(NPCDefinition def)
    {
        if (def == null)
            return null;

        NPCAIProfileArchetype archetype = def.AIProfileArchetype;

        // Legacy fallback for old definitions that don't explicitly set an archetype.
        if (archetype == NPCAIProfileArchetype.None
            && string.Equals(def.CreatureType, "Animal", StringComparison.OrdinalIgnoreCase))
        {
            archetype = NPCAIProfileArchetype.Animal;
        }

        switch (archetype)
        {
            case NPCAIProfileArchetype.Animal:
                return ScriptableObject.CreateInstance<AnimalAIProfile>();
            case NPCAIProfileArchetype.Humanoid:
                return ScriptableObject.CreateInstance<HumanoidAIProfile>();
            case NPCAIProfileArchetype.Berserk:
                return ScriptableObject.CreateInstance<BerserkAIProfile>();
            case NPCAIProfileArchetype.Grappler:
                return ScriptableObject.CreateInstance<GrapplerAIProfile>();
            case NPCAIProfileArchetype.Ranged:
                return ScriptableObject.CreateInstance<RangedAIProfile>();
            case NPCAIProfileArchetype.Healer:
                return ScriptableObject.CreateInstance<HealerAIProfile>();
            case NPCAIProfileArchetype.Spellcaster:
                return ScriptableObject.CreateInstance<SpellcasterAIProfile>();
            case NPCAIProfileArchetype.Evoker:
                return ScriptableObject.CreateInstance<EvokerAIProfile>();
            case NPCAIProfileArchetype.Abjurer:
                return ScriptableObject.CreateInstance<AbjurerAIProfile>();
            case NPCAIProfileArchetype.Necromancer:
                return ScriptableObject.CreateInstance<NecromancerAIProfile>();
            case NPCAIProfileArchetype.UndeadMindless:
                return ScriptableObject.CreateInstance<UndeadMindlessAIProfile>();
            case NPCAIProfileArchetype.Swarm:
                return ScriptableObject.CreateInstance<SwarmAI>();
            case NPCAIProfileArchetype.IndiscriminateSwarm:
                return ScriptableObject.CreateInstance<IndiscriminateSwarmAI>();
            case NPCAIProfileArchetype.Dragon:
                return ScriptableObject.CreateInstance<DragonAIProfile>();
            default:
                return null;
        }
    }

}

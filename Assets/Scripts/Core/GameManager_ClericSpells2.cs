// ============================================================================
// GameManager_ClericSpells2.cs — Resolution logic for 2nd-level Cleric spells:
//   Death Knell, Shield Other, Silence, Sound Burst, Spiritual Weapon,
//   Consecrate, Desecrate, and Align Weapon (PHB 3.5e).
// Part of the GameManager partial class.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;

public partial class GameManager
{
    // ================================================================
    //  Active area effect tracking for Consecrate / Desecrate
    // ================================================================

    /// <summary>Tracks a Consecrate or Desecrate area effect on the grid.</summary>
    private class HolyAreaEffect
    {
        public string SpellId;           // SpellNames.CONSECRATE or DOMAIN_DESECRATE
        public CharacterController Caster;
        public int CasterLevel;
        public HashSet<Vector2Int> AffectedCells;
        public int RoundsRemaining;      // -1 = effectively permanent for combat duration
        public bool IsConsecrate => SpellId == SpellNames.CONSECRATE;
    }

    private readonly List<HolyAreaEffect> _activeHolyAreas = new List<HolyAreaEffect>();

    // ================================================================
    //  DEATH KNELL — PHB p.217
    //  Necromancy. Cleric 2. V, S.
    //  Range: Touch. Target: Living creature with -1 or fewer HP.
    //  Duration: Instantaneous / 10 min per HD of subject.
    //  Saving Throw: Will negates. Spell Resistance: Yes.
    //  Kills dying creature. Caster gains 1d8 temp HP, +2 STR, +1 CL
    //  for 10 min/HD of slain creature.
    // ================================================================

    /// <summary>
    /// Resolves Death Knell. Called from ApplySpellBuff when the target
    /// has already failed their Will save and the spell hits.
    /// The target should be a dying creature (HP <= -1).
    /// </summary>
    private bool TryResolveDeathKnellSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.DEATH_KNELL)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        // Per PHB: target must be a living creature with -1 or fewer HP
        // In combat prototype, we check if target is dying (HP <= -1 and not dead)
        int targetHP = target.Stats.CurrentHP;
        bool isDying = targetHP <= -1 && targetHP > -10;

        if (!isDying)
        {
            // Target is not dying — spell has no valid target
            CombatUI?.ShowCombatLog($"<color=#FF6666>☠ Death Knell fizzles — {target.Stats.CharacterName} is not dying (HP: {targetHP}).</color>");
            Debug.Log($"[DeathKnell] Target {target.Stats.CharacterName} HP={targetHP}, not in dying range (-1 to -9)");
            return true; // Handled, but no effect
        }

        // Kill the target
        target.Stats.CurrentHP = -10;
        Debug.Log($"[DeathKnell] {target.Stats.CharacterName} killed by Death Knell (HP set to -10)");

        // Calculate buff duration: 10 min/HD of slain creature
        // 10 minutes = 100 rounds; per HD of the killed creature
        int targetHD = Mathf.Max(1, target.Stats.Level);
        int buffRounds = targetHD * 100; // 10 min * 10 rounds/min * HD

        // Caster gains 1d8 temporary HP
        int tempHP = DiceService.D8("Death Knell temp HP 1d8");
        caster.Stats.TempHP = Mathf.Max(caster.Stats.TempHP, tempHP); // Don't stack, use higher

        // Caster gains +2 enhancement bonus to STR
        caster.Stats.DeathKnellActive = true;
        caster.Stats.DeathKnellStrBonus = 2;
        caster.Stats.DeathKnellCLBonus = 1;
        caster.Stats.DeathKnellRoundsRemaining = buffRounds;

        // Apply the STR bonus
        caster.Stats.STR += 2;

        // Track via StatusEffectManager for display/cleanup
        var statusMgr = caster.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            int cl = caster.Stats.GetCasterLevel();
            var effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, cl);
            if (effect != null)
            {
                effect.RemainingRounds = buffRounds;
                effect.AppliedStatName = "STR";
                effect.AppliedStatBonus = 2;
                effect.AppliedTempHP = tempHP;
            }
        }

        CombatUI?.ShowCombatLog($"<color=#CC33FF>☠ Death Knell! {caster.Stats.CharacterName} kills {target.Stats.CharacterName} and gains {tempHP} temp HP, +2 STR, +1 caster level for {buffRounds} rounds ({targetHD * 10} minutes)!</color>");
        Debug.Log($"[DeathKnell] {caster.Stats.CharacterName} gains {tempHP} temp HP, +2 STR, +1 CL for {buffRounds} rounds");

        return true;
    }

    // ================================================================
    //  SHIELD OTHER — PHB p.278
    //  Abjuration. Cleric 2 / Paladin 2. V, S, F (pair of platinum rings).
    //  Range: Close. Target: One creature.
    //  Duration: 1 hr/level (D). Saving Throw: Will negates (harmless).
    //  +1 deflection AC, +1 resistance bonus on saves.
    //  Caster takes half of the subject's damage (transferred via empathic link).
    //  NOTE: The +1 deflection/resistance is handled by SpellData buffs.
    //  The damage sharing is handled in CharacterStats.TakeDamage.
    //  This handler sets up the Shield Other link between caster and target.
    // ================================================================

    private bool TryResolveShieldOtherSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.SHIELD_OTHER)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
            return true; // Spell was cast but failed (shouldn't happen for harmless)

        // Clear any existing Shield Other on the target
        ClearShieldOtherLink(target);
        // Clear any existing protection the caster is providing to someone else
        ClearShieldOtherProtectorLink(caster);

        // Establish the empathic link
        target.Stats.ShieldOtherProtectedActive = true;
        target.Stats.ShieldOtherProtector = caster;
        caster.Stats.ShieldOtherProtectorActive = true;
        caster.Stats.ShieldOtherProtected = target;

        CombatUI?.ShowCombatLog($"<color=#66CCFF>🛡 Shield Other! {caster.Stats.CharacterName} protects {target.Stats.CharacterName} (+1 deflection AC, +1 resistance saves, damage shared).</color>");
        Debug.Log($"[ShieldOther] Empathic link: {caster.Stats.CharacterName} (protector) ↔ {target.Stats.CharacterName} (protected)");

        return false; // Return false so the normal buff application (deflection/resistance) still runs
    }

    /// <summary>Clears Shield Other protection FROM a target (the protected character).</summary>
    private void ClearShieldOtherLink(CharacterController protectedChar)
    {
        if (protectedChar?.Stats == null) return;

        if (protectedChar.Stats.ShieldOtherProtectedActive && protectedChar.Stats.ShieldOtherProtector != null)
        {
            var oldProtector = protectedChar.Stats.ShieldOtherProtector;
            if (oldProtector.Stats != null)
            {
                oldProtector.Stats.ShieldOtherProtectorActive = false;
                oldProtector.Stats.ShieldOtherProtected = null;
            }
        }

        protectedChar.Stats.ShieldOtherProtectedActive = false;
        protectedChar.Stats.ShieldOtherProtector = null;
    }

    /// <summary>Clears Shield Other protection FROM a caster (the protector).</summary>
    private void ClearShieldOtherProtectorLink(CharacterController protector)
    {
        if (protector?.Stats == null) return;

        if (protector.Stats.ShieldOtherProtectorActive && protector.Stats.ShieldOtherProtected != null)
        {
            var oldProtected = protector.Stats.ShieldOtherProtected;
            if (oldProtected.Stats != null)
            {
                oldProtected.Stats.ShieldOtherProtectedActive = false;
                oldProtected.Stats.ShieldOtherProtector = null;
            }
        }

        protector.Stats.ShieldOtherProtectorActive = false;
        protector.Stats.ShieldOtherProtected = null;
    }

    // ================================================================
    //  SILENCE — PHB p.279
    //  Illusion (Glamer). Cleric 2 / Bard 2. V, S.
    //  Range: Long. Area: 20-ft-radius emanation centered on a creature/object/point.
    //  Duration: 1 round/level (D). Saving Throw: Will negates (creature-targeted) or None (area).
    //  Spell Resistance: Yes.
    //  Negates all sound in the area. Creatures in the area cannot cast
    //  spells with verbal components. Counters/dispels sound-based effects.
    // ================================================================

    private bool TryResolveSilenceSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.SILENCE)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
        {
            CombatUI?.ShowCombatLog($"<color=#AAAAAA>🔇 {target.Stats.CharacterName} resists {caster.Stats.CharacterName}'s Silence!</color>");
            return true;
        }

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Max(1, casterLevel); // 1 round/level

        target.Stats.SilenceActive = true;
        target.Stats.SilenceRoundsRemaining = durationRounds;

        // Track via StatusEffectManager
        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);
            if (effect != null)
                effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#99CCFF>🔇 Silence! {target.Stats.CharacterName} is silenced for {durationRounds} round(s)! Cannot cast spells with verbal components.</color>");
        Debug.Log($"[Silence] Applied to {target.Stats.CharacterName} for {durationRounds} rounds (CL {casterLevel})");

        return true;
    }

    // ================================================================
    //  SOUND BURST — PHB p.281
    //  Evocation [Sonic]. Cleric 2. V, S, DF.
    //  Range: Close. Area: 10-ft-radius spread.
    //  Duration: Instantaneous.
    //  Saving Throw: Fortitude partial. Spell Resistance: Yes.
    //  1d8 sonic damage to all creatures in area. Creatures that fail
    //  Fort save are stunned for 1 round.
    //  NOTE: The damage is already handled by the existing AoE/damage system.
    //  This handler adds the stun effect on failed Fort save.
    // ================================================================

    private bool TryResolveSoundBurstStunEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.SOUND_BURST)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        // Sound Burst damage is handled by normal spell damage resolution.
        // Here we check for the stun: Fort save or stunned for 1 round.
        int saveDC = 10 + spell.SpellLevel + GetSpellSaveAbilityModifier(caster, spell);
        int fortSave = DiceService.D20("Sound Burst Fort save") + target.Stats.FortitudeSave;

        Debug.Log($"[SoundBurst] Fort save: {target.Stats.CharacterName} rolled {fortSave} vs DC {saveDC}");

        if (fortSave < saveDC)
        {
            // Failed — stunned for 1 round
            if (_conditionService != null)
            {
                _conditionService.ApplyCondition(
                    target,
                    CombatConditionType.Stunned,
                    1,
                    source: caster,
                    sourceNameOverride: spell.Name,
                    sourceCategory: "Spell",
                    sourceId: spell.SpellId);
            }
            else
            {
                string sourceName = caster.Stats.CharacterName;
                target.ApplyCondition(CombatConditionType.Stunned, 1, sourceName);
            }

            CombatUI?.ShowCombatLog($"<color=#FF9933>💥🔔 {target.Stats.CharacterName} is stunned by Sound Burst! (Fort {fortSave} vs DC {saveDC})</color>");
            Debug.Log($"[SoundBurst] {target.Stats.CharacterName} STUNNED for 1 round (Fort {fortSave} < DC {saveDC})");
        }
        else
        {
            CombatUI?.ShowCombatLog($"<color=#CCCCCC>🔔 {target.Stats.CharacterName} resists Sound Burst's stun (Fort {fortSave} vs DC {saveDC}).</color>");
            Debug.Log($"[SoundBurst] {target.Stats.CharacterName} resisted stun (Fort {fortSave} >= DC {saveDC})");
        }

        return false; // Let normal damage resolution continue
    }

    // ================================================================
    //  SPIRITUAL WEAPON — PHB p.283
    //  Evocation [Force]. Cleric 2. V, S, DF.
    //  Range: Medium (100 ft + 10 ft/level). Effect: Magic weapon of force.
    //  Duration: 1 round/level (D, max 10 rounds).
    //  Saving Throw: None. Spell Resistance: Yes.
    //  Weapon attacks designated foe each round using caster's BAB + WIS mod.
    //  Deals 1d8 + 1/3 caster levels (max +5) force damage.
    //  Cannot be attacked or harmed. No flanking. No AoO.
    //  Moves up to target automatically each round.
    //  On initial cast, makes an attack immediately. Subsequent attacks
    //  happen at start of caster's turn (free action, no AoO).
    // ================================================================

    private bool TryResolveSpiritualWeaponSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.SPIRITUAL_WEAPON)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = Mathf.Min(casterLevel, 10); // Max 10 rounds at CL 10+

        // Set up the spiritual weapon tracking on the caster
        caster.Stats.SpiritualWeaponActive = true;
        caster.Stats.SpiritualWeaponTarget = target;
        caster.Stats.SpiritualWeaponCasterLevel = casterLevel;
        caster.Stats.SpiritualWeaponRoundsRemaining = durationRounds;

        // Track via StatusEffectManager
        var statusMgr = caster.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);
            if (effect != null)
                effect.RemainingRounds = durationRounds;
        }

        // Make the initial attack on casting
        string attackLog = ResolveSpiritualWeaponAttack(caster, target, casterLevel);

        CombatUI?.ShowCombatLog($"<color=#CCAAFF>⚔🌟 Spiritual Weapon! {caster.Stats.CharacterName} conjures a force weapon for {durationRounds} round(s)!\n{attackLog}</color>");
        Debug.Log($"[SpiritualWeapon] Created for {caster.Stats.CharacterName}, target={target.Stats.CharacterName}, CL={casterLevel}, duration={durationRounds}");

        return true;
    }

    /// <summary>
    /// Resolves a single Spiritual Weapon attack roll and damage.
    /// Uses caster's BAB + WIS mod for attack, 1d8 + floor(CL/3) force damage (max +5).
    /// </summary>
    private string ResolveSpiritualWeaponAttack(
        CharacterController caster, CharacterController target, int casterLevel)
    {
        if (caster?.Stats == null || target?.Stats == null)
            return "";

        // Attack roll: caster's BAB + WIS modifier
        int bab = caster.Stats.BaseAttackBonus;
        int wisMod = caster.Stats.WISMod;
        int attackRoll = DiceService.D20("Spiritual Weapon attack");
        int totalAttack = attackRoll + bab + wisMod;
        int targetAC = target.Stats.ArmorClass;

        StringBuilder sb = new StringBuilder();

        bool isHit = (attackRoll == 20) || (attackRoll != 1 && totalAttack >= targetAC);

        if (isHit)
        {
            // Damage: 1d8 + floor(CL/3), max bonus of +5
            int damageBonus = Mathf.Min(5, casterLevel / 3);
            int damage = DiceService.D8("Spiritual Weapon damage 1d8") + damageBonus;
            damage = Mathf.Max(1, damage);

            target.Stats.TakeDamage(damage);

            sb.Append($"  ⚔ Spiritual Weapon attacks {target.Stats.CharacterName}: {attackRoll}+{bab}+{wisMod}={totalAttack} vs AC {targetAC} — HIT for {damage} force damage!");
            Debug.Log($"[SpiritualWeapon] Hit {target.Stats.CharacterName}: roll={attackRoll}+BAB{bab}+WIS{wisMod}={totalAttack} vs AC{targetAC}, damage={damage}");
        }
        else
        {
            sb.Append($"  ⚔ Spiritual Weapon attacks {target.Stats.CharacterName}: {attackRoll}+{bab}+{wisMod}={totalAttack} vs AC {targetAC} — MISS!");
            Debug.Log($"[SpiritualWeapon] Miss {target.Stats.CharacterName}: roll={attackRoll}+BAB{bab}+WIS{wisMod}={totalAttack} vs AC{targetAC}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Called at the start of a character's turn to process their Spiritual Weapon.
    /// Should be called from the turn processing loop.
    /// </summary>
    public void ProcessSpiritualWeaponTurnStart(CharacterController character)
    {
        if (character?.Stats == null || !character.Stats.SpiritualWeaponActive)
            return;

        // Decrement duration
        if (character.Stats.SpiritualWeaponRoundsRemaining > 0)
        {
            character.Stats.SpiritualWeaponRoundsRemaining--;

            if (character.Stats.SpiritualWeaponRoundsRemaining <= 0)
            {
                // Spiritual weapon expires
                character.Stats.SpiritualWeaponActive = false;
                character.Stats.SpiritualWeaponTarget = null;
                character.Stats.SpiritualWeaponCasterLevel = 0;
                CombatUI?.ShowCombatLog($"<color=#AAAAAA>🌟 {character.Stats.CharacterName}'s Spiritual Weapon fades away.</color>");
                Debug.Log($"[SpiritualWeapon] Expired for {character.Stats.CharacterName}");
                return;
            }
        }

        // Make the automatic attack
        var target = character.Stats.SpiritualWeaponTarget;
        if (target == null || target.Stats == null || target.Stats.CurrentHP <= -10)
        {
            // Target is dead — weapon remains but doesn't attack
            // Per PHB: caster can redirect as a move action (simplified: auto-retarget nearest enemy)
            var newTarget = FindNearestLivingEnemy(character);
            if (newTarget != null)
            {
                character.Stats.SpiritualWeaponTarget = newTarget;
                target = newTarget;
                Debug.Log($"[SpiritualWeapon] Retargeted to {target.Stats.CharacterName}");
            }
            else
            {
                CombatUI?.ShowCombatLog($"<color=#AAAAAA>🌟 {character.Stats.CharacterName}'s Spiritual Weapon has no valid target.</color>");
                return;
            }
        }

        string attackLog = ResolveSpiritualWeaponAttack(character, target, character.Stats.SpiritualWeaponCasterLevel);
        CombatUI?.ShowCombatLog($"<color=#CCAAFF>{attackLog}</color>");
        UpdateAllStatsUI();
    }

    /// <summary>Finds the nearest living enemy to a character for auto-retargeting.</summary>
    private CharacterController FindNearestLivingEnemy(CharacterController self)
    {
        if (self == null) return null;
        var allChars = GetAllCharacters();
        if (allChars == null) return null;

        CharacterController nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var ch in allChars)
        {
            if (ch == null || ch == self || ch.Stats == null) continue;
            if (ch.Stats.CurrentHP <= -10) continue; // Dead
            if (ch.IsPlayerControlled == self.IsPlayerControlled) continue; // Same side

            float dist = Vector2Int.Distance(self.GridPosition, ch.GridPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ch;
            }
        }

        return nearest;
    }

    // ================================================================
    //  CONSECRATE — PHB p.212
    //  Evocation [Good]. Cleric 2. V, S, M (vial of holy water), DF.
    //  Area: 20-ft-radius emanation. Duration: 2 hr/level.
    //  Undead in area: -1 profane penalty to attacks, damage, saves.
    //  Turning checks in area: +3 sacred bonus.
    //  Counters/dispels Desecrate.
    // ================================================================

    private bool TryResolveConsecrateAreaEffect(
        CharacterController caster, SpellData spell,
        HashSet<Vector2Int> aoeCells, List<CharacterController> targets, out string log)
    {
        log = string.Empty;
        if (spell == null || spell.SpellId != SpellNames.CONSECRATE)
            return false;

        if (caster == null || caster.Stats == null || aoeCells == null)
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        // Remove any existing Desecrate areas that overlap
        RemoveOverlappingHolyAreas(aoeCells, SpellNames.DOMAIN_DESECRATE);

        // Create the area effect
        var area = new HolyAreaEffect
        {
            SpellId = SpellNames.CONSECRATE,
            Caster = caster,
            CasterLevel = casterLevel,
            AffectedCells = new HashSet<Vector2Int>(aoeCells),
            RoundsRemaining = -1 // 2 hr/level — effectively permanent for combat
        };
        _activeHolyAreas.Add(area);

        // Apply effects to undead currently in the area
        int affectedCount = 0;
        StringBuilder sb = new StringBuilder();
        sb.Append($"<color=#FFFF99>✨ {caster.Stats.CharacterName} casts Consecrate! Positive energy fills the area.</color>\n");

        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t?.Stats == null) continue;
                if (IsUndead(t))
                {
                    ApplyConsecrateDebuffs(t);
                    affectedCount++;
                    sb.Append($"  💀 {t.Stats.CharacterName} (undead) suffers -1 to attacks, damage, and saves!\n");
                }
            }
        }

        sb.Append($"  +3 sacred bonus to turning checks in the area.");
        log = sb.ToString();

        Debug.Log($"[Consecrate] Area created by {caster.Stats.CharacterName}, {aoeCells.Count} cells, {affectedCount} undead affected");
        return true;
    }

    // ================================================================
    //  DESECRATE — PHB p.218
    //  Evocation [Evil]. Cleric 2. V, S, M (vial of unholy water), DF.
    //  Area: 20-ft-radius emanation. Duration: 2 hr/level.
    //  Undead in area: +1 profane bonus to attacks, damage, saves.
    //  Turning checks in area: -3 profane penalty.
    //  Undead created in area gain +1 HP per HD.
    //  Counters/dispels Consecrate.
    // ================================================================

    private bool TryResolveDesecrateAreaEffect(
        CharacterController caster, SpellData spell,
        HashSet<Vector2Int> aoeCells, List<CharacterController> targets, out string log)
    {
        log = string.Empty;
        if (spell == null || spell.SpellId != SpellNames.DOMAIN_DESECRATE)
            return false;

        if (caster == null || caster.Stats == null || aoeCells == null)
            return false;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());

        // Remove any existing Consecrate areas that overlap
        RemoveOverlappingHolyAreas(aoeCells, SpellNames.CONSECRATE);

        // Create the area effect
        var area = new HolyAreaEffect
        {
            SpellId = SpellNames.DOMAIN_DESECRATE,
            Caster = caster,
            CasterLevel = casterLevel,
            AffectedCells = new HashSet<Vector2Int>(aoeCells),
            RoundsRemaining = -1 // 2 hr/level — effectively permanent for combat
        };
        _activeHolyAreas.Add(area);

        // Apply effects to undead currently in the area
        int affectedCount = 0;
        StringBuilder sb = new StringBuilder();
        sb.Append($"<color=#CC66FF>💀 {caster.Stats.CharacterName} casts Desecrate! Negative energy fills the area.</color>\n");

        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t?.Stats == null) continue;
                if (IsUndead(t))
                {
                    ApplyDesecrateBuffs(t);
                    affectedCount++;
                    sb.Append($"  💀 {t.Stats.CharacterName} (undead) gains +1 to attacks, damage, and saves!\n");
                }
            }
        }

        sb.Append($"  -3 profane penalty to turning checks in the area.");
        log = sb.ToString();

        Debug.Log($"[Desecrate] Area created by {caster.Stats.CharacterName}, {aoeCells.Count} cells, {affectedCount} undead affected");
        return true;
    }

    /// <summary>Applies Consecrate debuffs to an undead character: -1 attacks, damage, saves.</summary>
    private void ApplyConsecrateDebuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        target.Stats.ConsecrateActive = true;
        // The penalties are tracked via the flag; attack/damage/save code checks ConsecrateActive
    }

    /// <summary>Removes Consecrate debuffs from a character.</summary>
    private void RemoveConsecrateDebuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        target.Stats.ConsecrateActive = false;
    }

    /// <summary>Applies Desecrate buffs to an undead character: +1 attacks, damage, saves.</summary>
    private void ApplyDesecrateBuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        target.Stats.DesecrateActive = true;
        // The bonuses are tracked via the flag; attack/damage/save code checks DesecrateActive
    }

    /// <summary>Removes Desecrate buffs from a character.</summary>
    private void RemoveDesecrateBuffs(CharacterController target)
    {
        if (target?.Stats == null) return;
        target.Stats.DesecrateActive = false;
    }

    /// <summary>Removes overlapping holy areas of a given type from the active list.</summary>
    private void RemoveOverlappingHolyAreas(HashSet<Vector2Int> newCells, string spellIdToRemove)
    {
        for (int i = _activeHolyAreas.Count - 1; i >= 0; i--)
        {
            if (_activeHolyAreas[i].SpellId != spellIdToRemove) continue;

            // Check if any cells overlap
            bool overlaps = _activeHolyAreas[i].AffectedCells.Overlaps(newCells);
            if (overlaps)
            {
                // Remove buffs/debuffs from characters in the removed area
                CleanupHolyAreaCharacters(_activeHolyAreas[i]);
                _activeHolyAreas.RemoveAt(i);
                Debug.Log($"[HolyArea] Removed overlapping {spellIdToRemove} area");
            }
        }
    }

    /// <summary>Removes Consecrate/Desecrate effects from all characters in a holy area.</summary>
    private void CleanupHolyAreaCharacters(HolyAreaEffect area)
    {
        var allChars = GetAllCharacters();
        if (allChars == null) return;

        foreach (var ch in allChars)
        {
            if (ch?.Stats == null) continue;
            if (!area.AffectedCells.Contains(ch.GridPosition)) continue;

            if (area.IsConsecrate)
                RemoveConsecrateDebuffs(ch);
            else
                RemoveDesecrateBuffs(ch);
        }
    }

    /// <summary>
    /// Checks whether a character is undead based on their CreatureType.
    /// </summary>
    private bool IsUndead(CharacterController character)
    {
        if (character?.Stats == null) return false;
        string ct = character.Stats.CreatureType;
        return !string.IsNullOrWhiteSpace(ct) &&
               ct.Trim().Equals("Undead", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Called when a character moves to update Consecrate/Desecrate area effects.
    /// Should be called after any character movement.
    /// </summary>
    public void UpdateHolyAreaEffectsForCharacter(CharacterController character)
    {
        if (character?.Stats == null || _activeHolyAreas.Count == 0) return;

        bool inConsecrate = false;
        bool inDesecrate = false;

        foreach (var area in _activeHolyAreas)
        {
            if (area.AffectedCells.Contains(character.GridPosition))
            {
                if (area.IsConsecrate)
                    inConsecrate = true;
                else
                    inDesecrate = true;
            }
        }

        // Apply/remove Consecrate effects for undead
        if (IsUndead(character))
        {
            if (inConsecrate && !character.Stats.ConsecrateActive)
            {
                ApplyConsecrateDebuffs(character);
                CombatUI?.ShowCombatLog($"<color=#FFFF99>✨ {character.Stats.CharacterName} enters consecrated ground (undead: -1 attacks/damage/saves).</color>");
            }
            else if (!inConsecrate && character.Stats.ConsecrateActive)
            {
                RemoveConsecrateDebuffs(character);
                CombatUI?.ShowCombatLog($"<color=#AAAAAA>✨ {character.Stats.CharacterName} leaves consecrated ground.</color>");
            }

            if (inDesecrate && !character.Stats.DesecrateActive)
            {
                ApplyDesecrateBuffs(character);
                CombatUI?.ShowCombatLog($"<color=#CC66FF>💀 {character.Stats.CharacterName} enters desecrated ground (undead: +1 attacks/damage/saves).</color>");
            }
            else if (!inDesecrate && character.Stats.DesecrateActive)
            {
                RemoveDesecrateBuffs(character);
                CombatUI?.ShowCombatLog($"<color=#AAAAAA>💀 {character.Stats.CharacterName} leaves desecrated ground.</color>");
            }
        }
    }

    /// <summary>
    /// Gets the turning check modifier for a character's current position.
    /// Returns +3 in Consecrate areas, -3 in Desecrate areas, 0 otherwise.
    /// </summary>
    public int GetTurningCheckModifierAtPosition(Vector2Int position)
    {
        int modifier = 0;
        foreach (var area in _activeHolyAreas)
        {
            if (area.AffectedCells.Contains(position))
            {
                if (area.IsConsecrate)
                    modifier += 3; // +3 sacred bonus
                else
                    modifier -= 3; // -3 profane penalty
            }
        }
        return modifier;
    }

    // ================================================================
    //  ALIGN WEAPON — PHB p.197
    //  Transmutation. Cleric 2. V, S, DF.
    //  Range: Touch. Target: Weapon touched or 50 projectiles.
    //  Duration: 1 min/level. Saving Throw: Will negates (harmless, object).
    //  Spell Resistance: Yes (harmless, object).
    //  Makes weapon good/evil/lawful/chaotic-aligned to bypass DR.
    //  Cannot make a weapon aligned to the opposite of the caster's alignment.
    // ================================================================

    private bool TryResolveAlignWeaponSpellEffect(
        CharacterController caster, CharacterController target,
        SpellData spell, SpellResult result)
    {
        if (spell == null || spell.SpellId != SpellNames.ALIGN_WEAPON)
            return false;

        if (caster == null || caster.Stats == null || target == null || target.Stats == null)
            return false;

        if (!result.Success)
            return true;

        int casterLevel = Mathf.Max(1, caster.Stats.GetCasterLevel());
        int durationRounds = casterLevel * 10; // 1 min/level = 10 rounds/level

        // Determine alignment to apply
        // For simplicity, use "good" as default. Caster's alignment determines options:
        // Cannot make weapon opposed to caster's alignment.
        // AI casters: pick "good" for good-aligned, "evil" for evil-aligned, etc.
        string alignment = DetermineAlignWeaponAlignment(caster);

        target.Stats.AlignWeaponActive = true;
        target.Stats.AlignWeaponAlignment = alignment;
        target.Stats.AlignWeaponRoundsRemaining = durationRounds;

        // Track via StatusEffectManager
        var statusMgr = target.GetComponent<StatusEffectManager>();
        if (statusMgr != null)
        {
            var effect = statusMgr.AddEffect(spell, caster.Stats.CharacterName, casterLevel);
            if (effect != null)
                effect.RemainingRounds = durationRounds;
        }

        CombatUI?.ShowCombatLog($"<color=#FFCC33>⚔✨ Align Weapon! {target.Stats.CharacterName}'s weapon is now {alignment}-aligned for {durationRounds} rounds ({casterLevel} minutes). Bypasses DR/{alignment}.</color>");
        Debug.Log($"[AlignWeapon] {target.Stats.CharacterName}'s weapon aligned as '{alignment}' for {durationRounds} rounds");

        return true;
    }

    /// <summary>
    /// Determines which alignment to apply to Align Weapon based on caster's alignment.
    /// Per PHB: caster cannot choose an alignment component opposite to their own.
    /// Good casters pick "good", evil pick "evil", lawful pick "lawful", chaotic pick "chaotic".
    /// Neutral casters default to "good".
    /// </summary>
    private string DetermineAlignWeaponAlignment(CharacterController caster)
    {
        if (caster?.Stats != null)
        {
            Alignment a = caster.Stats.CharacterAlignment;
            if (AlignmentHelper.IsGood(a)) return "good";
            if (AlignmentHelper.IsEvil(a)) return "evil";
            if (AlignmentHelper.IsLawful(a)) return "lawful";
            if (AlignmentHelper.IsChaotic(a)) return "chaotic";
        }
        return "good"; // Default for True Neutral or unknown
    }

    // ================================================================
    //  ROUND TICK / CLEANUP — Process spell durations at start of round
    // ================================================================

    /// <summary>
    /// Called at the start of each combat round to tick cleric spell durations.
    /// </summary>
    public void TickClericSpell2Durations(CharacterController character)
    {
        if (character?.Stats == null) return;

        // ── Death Knell tick ──
        if (character.Stats.DeathKnellActive)
        {
            if (character.Stats.DeathKnellRoundsRemaining > 0)
            {
                character.Stats.DeathKnellRoundsRemaining--;
                if (character.Stats.DeathKnellRoundsRemaining <= 0)
                {
                    // Remove Death Knell buffs
                    character.Stats.STR -= character.Stats.DeathKnellStrBonus;
                    character.Stats.DeathKnellActive = false;
                    character.Stats.DeathKnellStrBonus = 0;
                    character.Stats.DeathKnellCLBonus = 0;

                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>☠ {character.Stats.CharacterName}'s Death Knell buff fades.</color>");
                    Debug.Log($"[DeathKnell] Buff expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Silence tick ──
        if (character.Stats.SilenceActive)
        {
            if (character.Stats.SilenceRoundsRemaining > 0)
            {
                character.Stats.SilenceRoundsRemaining--;
                if (character.Stats.SilenceRoundsRemaining <= 0)
                {
                    character.Stats.SilenceActive = false;
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>🔇 {character.Stats.CharacterName}'s Silence ends.</color>");
                    Debug.Log($"[Silence] Expired on {character.Stats.CharacterName}");
                }
            }
        }

        // ── Spiritual Weapon tick — handled by ProcessSpiritualWeaponTurnStart ──

        // ── Align Weapon tick ──
        if (character.Stats.AlignWeaponActive)
        {
            if (character.Stats.AlignWeaponRoundsRemaining > 0)
            {
                character.Stats.AlignWeaponRoundsRemaining--;
                if (character.Stats.AlignWeaponRoundsRemaining <= 0)
                {
                    character.Stats.AlignWeaponActive = false;
                    character.Stats.AlignWeaponAlignment = null;
                    CombatUI?.ShowCombatLog($"<color=#AAAAAA>⚔ {character.Stats.CharacterName}'s Align Weapon fades.</color>");
                    Debug.Log($"[AlignWeapon] Expired on {character.Stats.CharacterName}");
                }
            }
        }
    }

    /// <summary>
    /// Checks if a character has an aligned weapon that can bypass a given DR alignment type.
    /// </summary>
    public static bool CanBypassAlignmentDR(CharacterController attacker, string drAlignment)
    {
        if (attacker?.Stats == null || !attacker.Stats.AlignWeaponActive)
            return false;

        if (string.IsNullOrWhiteSpace(drAlignment) || string.IsNullOrWhiteSpace(attacker.Stats.AlignWeaponAlignment))
            return false;

        return attacker.Stats.AlignWeaponAlignment.Equals(drAlignment, StringComparison.OrdinalIgnoreCase);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lion's Shield (SRD / DMG p.220): +2 heavy steel shield with a lion head embossed.
/// 3/day the lion head can animate and bite an opponent within 5 feet, attacking
/// independently using the wielder's BAB. The bite deals 2d6 damage.
///
/// Lion's Shield, Greater: +2 heavy steel shield with an enhanced magical lion head.
/// 3/day bite dealing 2d8+2 damage, AND 1/day can summon a dire lion ally for 10 rounds.
/// The summoned dire lion fights on the wielder's side and disappears after the duration.
/// </summary>
public class LionsShieldBehavior : SpecificItemBehavior
{
    // ── Configuration ──
    private const int MaxBitesPerDay = 3;
    private const int MaxSummonsPerDay = 1;
    private const int SummonDurationRounds = 10;

    private readonly bool _isGreater;

    // Standard: 2d6; Greater: 2d8+2
    private readonly int _biteDice;
    private readonly int _biteDieSides;
    private readonly int _biteFlatBonus;

    private readonly string _summonNpcId; // "dire_lion" for Greater

    // ── State ──
    private int _bitesRemaining;
    private int _summonsRemaining;

    public LionsShieldBehavior(bool isGreater = false)
    {
        _isGreater = isGreater;

        if (_isGreater)
        {
            _biteDice = 2;
            _biteDieSides = 8;
            _biteFlatBonus = 2;
            _summonNpcId = "dire_lion";
        }
        else
        {
            _biteDice = 2;
            _biteDieSides = 6;
            _biteFlatBonus = 0;
            _summonNpcId = null;
        }
    }

    private string ShieldName => _isGreater ? "Lion's Shield, Greater" : "Lion's Shield";

    // ────────────────────────────────────────────
    //  Lifecycle
    // ────────────────────────────────────────────

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _bitesRemaining = MaxBitesPerDay;
        _summonsRemaining = _isGreater ? MaxSummonsPerDay : 0;
    }

    public override void OnLongRest()
    {
        _bitesRemaining = MaxBitesPerDay;
        _summonsRemaining = _isGreater ? MaxSummonsPerDay : 0;
    }

    // ────────────────────────────────────────────
    //  Activate — lion bite (default) or summon
    // ────────────────────────────────────────────

    public override bool CanActivate()
    {
        return IsEquipped && (_bitesRemaining > 0 || _summonsRemaining > 0);
    }

    public override string GetActivateDescription()
    {
        string desc = $"Lion head bites adjacent foe: wielder BAB + {_biteDice}d{_biteDieSides}";
        if (_biteFlatBonus > 0) desc += $"+{_biteFlatBonus}";
        desc += $" damage. ({_bitesRemaining}/{MaxBitesPerDay} bites)";

        if (_isGreater)
        {
            desc += $"\nOr summon a Dire Lion ally for {SummonDurationRounds} rounds. ({_summonsRemaining}/{MaxSummonsPerDay} summon)";
        }

        return desc;
    }

    public override string GetUsesDisplay()
    {
        string display = $"{_bitesRemaining}/{MaxBitesPerDay} bites";
        if (_isGreater)
        {
            display += $", {_summonsRemaining}/{MaxSummonsPerDay} summon";
        }
        return display;
    }

    /// <summary>
    /// Activate the lion bite against an adjacent target.
    /// If the target is null and this is the Greater variant with summons remaining,
    /// attempt to summon the dire lion instead.
    /// </summary>
    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        // If no target provided and Greater variant has summons, summon instead
        if (target == null && _isGreater && _summonsRemaining > 0)
        {
            return ActivateSummon(logNotes);
        }

        // ── Lion Bite ──
        if (_bitesRemaining <= 0)
        {
            // Fall through to summon if Greater
            if (_isGreater && _summonsRemaining > 0)
            {
                return ActivateSummon(logNotes);
            }

            logNotes.Add($"{ShieldName}: no uses remaining today.");
            return false;
        }

        if (target == null || target.Stats == null)
        {
            logNotes.Add($"{ShieldName}: no valid target for lion bite.");
            return false;
        }

        _bitesRemaining--;

        // Attack roll using wielder's BAB (the shield head biting, not wielder's Str)
        int bab = Wielder?.Stats?.BaseAttackBonus ?? 0;
        int attackRoll = DiceService.D20($"{ShieldName} bite attack");
        int totalAttack = attackRoll + bab;
        int targetAC = target.Stats.ArmorClass;

        if (attackRoll == 1 || (attackRoll != 20 && totalAttack < targetAC))
        {
            logNotes.Add($"{ShieldName} bite: d20({attackRoll})+{bab}={totalAttack} vs AC {targetAC} — MISS ({_bitesRemaining}/{MaxBitesPerDay} bites left)");
            Log($"Bite misses {target.Stats.CharacterName} ({totalAttack} vs AC {targetAC})");
            return true;
        }

        // Hit — roll damage
        int damage = DiceService.RollMultiple(_biteDice, _biteDieSides, $"{ShieldName} bite damage");
        damage += _biteFlatBonus;
        damage = Mathf.Max(1, damage);

        string damageStr = _biteFlatBonus > 0
            ? $"{_biteDice}d{_biteDieSides}+{_biteFlatBonus}={damage}"
            : $"{_biteDice}d{_biteDieSides}={damage}";

        logNotes.Add($"{ShieldName} bite: d20({attackRoll})+{bab}={totalAttack} vs AC {targetAC} — HIT for {damageStr} damage! ({_bitesRemaining}/{MaxBitesPerDay} bites left)");
        Log($"Bite hits {target.Stats.CharacterName} for {damage} damage");

        // Apply damage with proper DamagePacket
        var packet = new DamagePacket
        {
            RawDamage = damage,
            Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Piercing },
            AttackTags = DamageBypassTag.Magic | DamageBypassTag.Piercing,
            Source = AttackSource.Other,
            SourceName = ShieldName
        };
        target.Stats.ApplyIncomingDamage(damage, packet);

        if (target.Stats.IsDead)
        {
            target.OnDeath();
            logNotes.Add($"{ShieldName}: {target.Stats.CharacterName} is slain by the lion bite!");
        }

        return true;
    }

    // ────────────────────────────────────────────
    //  Summon Dire Lion (Greater only)
    // ────────────────────────────────────────────

    /// <summary>
    /// Summons a dire lion ally that fights for the wielder for 10 rounds, through GameManager's own summon path
    /// (GameManager.SpawnItemSummon: the spawn every summon spell's creature takes, InitializeNPCFromDefinition, so the lion
    /// has its feats, DR, SR, immunities and AI behaviour entry, AI-015; a place in the initiative after the wielder; the
    /// summon tracker that counts its rounds and removes it; CRE-005). The use is spent only when the lion appears.
    /// </summary>
    private bool ActivateSummon(List<string> logNotes)
    {
        if (_summonsRemaining <= 0)
        {
            logNotes.Add($"{ShieldName}: no summons remaining today.");
            return false;
        }

        if (Wielder == null)
        {
            logNotes.Add($"{ShieldName}: no wielder to anchor summon.");
            return false;
        }

        NPCDefinition npcDef = NPCDatabase.Get(_summonNpcId);
        if (npcDef == null)
        {
            logNotes.Add($"{ShieldName}: summon failed — '{_summonNpcId}' not found in NPC database.");
            Log($"ERROR: NPC definition '{_summonNpcId}' missing from database.");
            return false;
        }

        GameManager gm = GameManager.Instance;
        CharacterController summon = gm != null ? gm.SpawnItemSummon(Wielder, _summonNpcId, SummonDurationRounds) : null;
        if (summon == null)
        {
            logNotes.Add($"{ShieldName}: the {npcDef.Name} cannot appear (no free space beside {Wielder.Stats?.CharacterName}).");
            return false;
        }

        _summonsRemaining--;

        logNotes.Add($"{ShieldName}: summoned a {npcDef.Name} ally for {SummonDurationRounds} rounds! ({_summonsRemaining}/{MaxSummonsPerDay} summons left)");
        Log($"Summoned {npcDef.Name} at ({summon.GridPosition.x},{summon.GridPosition.y}) for {SummonDurationRounds} rounds");

        return true;
    }
}

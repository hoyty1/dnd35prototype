using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lion's Shield (SRD): +2 heavy steel shield with a lion head embossed on it.
/// 3/day the lion head can animate and bite an opponent within 5 feet, attacking
/// independently using the wielder's BAB. The bite deals 2d6 damage.
/// The shield does NOT summon a lion — the shield head itself bites.
/// </summary>
public class LionsShieldBehavior : SpecificItemBehavior
{
    private const int MaxUsesPerDay = 3;
    private const int BiteDice = 2;
    private const int BiteDieSides = 6;

    private int _usesRemaining;

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _usesRemaining = MaxUsesPerDay;
    }

    public override bool CanActivate()
    {
        return IsEquipped && _usesRemaining > 0;
    }

    public override string GetActivateDescription()
    {
        return $"Lion head bites adjacent foe: wielder BAB + {BiteDice}d{BiteDieSides} damage. ({_usesRemaining}/{MaxUsesPerDay} uses)";
    }

    /// <summary>
    /// Activate the lion bite against an adjacent target.
    /// Uses the wielder's BAB for the attack roll, deals 2d6 damage.
    /// </summary>
    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (_usesRemaining <= 0)
        {
            logNotes.Add("Lion's Shield: no bites remaining today.");
            return false;
        }

        if (target == null || target.Stats == null)
        {
            logNotes.Add("Lion's Shield: no valid target for lion bite.");
            return false;
        }

        _usesRemaining--;

        // Attack roll using wielder's BAB (no Str mod — it's the shield head biting)
        int bab = Wielder?.Stats?.BaseAttackBonus ?? 0;
        int attackRoll = DiceService.D20("Lion's Shield bite attack");
        int totalAttack = attackRoll + bab;
        int targetAC = target.Stats.ArmorClass;

        if (attackRoll == 1 || (attackRoll != 20 && totalAttack < targetAC))
        {
            logNotes.Add($"Lion's Shield bite: d20({attackRoll})+{bab}={totalAttack} vs AC {targetAC} — MISS ({_usesRemaining}/{MaxUsesPerDay} uses left)");
            Log($"Bite misses {target.Stats.CharacterName} ({totalAttack} vs AC {targetAC})");
            return true;
        }

        // Hit — roll damage
        int damage = DiceService.RollMultiple(BiteDice, BiteDieSides, "Lion's Shield bite damage");
        damage = Mathf.Max(1, damage);

        logNotes.Add($"Lion's Shield bite: d20({attackRoll})+{bab}={totalAttack} vs AC {targetAC} — HIT for {damage} damage! ({_usesRemaining}/{MaxUsesPerDay} uses left)");
        Log($"Bite hits {target.Stats.CharacterName} for {damage} damage");

        // Apply damage
        var packet = new DamagePacket
        {
            RawDamage = damage,
            Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Piercing },
            AttackTags = DamageBypassTag.Magic | DamageBypassTag.Piercing,
            Source = AttackSource.Other,
            SourceName = "Lion's Shield"
        };
        target.Stats.ApplyIncomingDamage(damage, packet);

        if (target.Stats.IsDead)
        {
            target.OnDeath();
            logNotes.Add($"Lion's Shield: {target.Stats.CharacterName} is slain by the lion bite!");
        }

        return true;
    }

    public override void OnLongRest()
    {
        _usesRemaining = MaxUsesPerDay;
    }

    public override string GetUsesDisplay()
    {
        return $"{_usesRemaining}/{MaxUsesPerDay} lion bites";
    }
}

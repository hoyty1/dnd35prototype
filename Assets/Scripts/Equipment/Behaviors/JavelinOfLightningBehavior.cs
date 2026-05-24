using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Javelin of Lightning (SRD): Once thrown, transforms into a 5-ft wide, 120-ft long
/// lightning bolt dealing 5d6 electricity damage (Reflex DC 14 half).
/// Functions as a +2 javelin when used in melee or after its lightning is expended.
/// Single use — once the bolt is discharged, it becomes a normal +2 javelin.
/// </summary>
public class JavelinOfLightningBehavior : SpecificItemBehavior
{
    private const int BoltDC = 14;
    private const int BoltDice = 5;
    private const int BoltDieSides = 6;

    private bool _lightningExpended;

    public override void Initialize(ItemData item)
    {
        base.Initialize(item);
        _lightningExpended = false;
    }

    public override bool CanActivate()
    {
        return !_lightningExpended && IsEquipped;
    }

    public override string GetActivateDescription()
    {
        if (_lightningExpended) return "Lightning bolt already expended.";
        return $"Throw to create a 120-ft lightning bolt dealing {BoltDice}d{BoltDieSides} electricity (Reflex DC {BoltDC} half). Single use.";
    }

    public override bool Activate(CharacterController target, List<string> logNotes)
    {
        if (_lightningExpended)
        {
            logNotes.Add("Javelin of Lightning: lightning bolt already expended.");
            return false;
        }

        _lightningExpended = true;

        int damage = DiceService.RollMultiple(BoltDice, BoltDieSides, "Javelin of Lightning bolt");
        Log($"Lightning bolt deals {damage} electricity damage");

        if (target != null && target.Stats != null)
        {
            var save = SavingThrowResolver.ResolveReflexSave(target.Stats, BoltDC, "Javelin of Lightning");

            if (save.Succeeded)
            {
                int halfDamage = damage / 2;
                logNotes.Add($"Javelin of Lightning: {BoltDice}d{BoltDieSides}={damage} electricity → {target.Stats.CharacterName} saves (Reflex DC {BoltDC}: {save.Total}), takes {halfDamage}");
                damage = halfDamage;
            }
            else
            {
                logNotes.Add($"Javelin of Lightning: {BoltDice}d{BoltDieSides}={damage} electricity → {target.Stats.CharacterName} fails save (Reflex DC {BoltDC}: {save.Total}), takes full {damage}");
            }

            // Apply electricity damage
            var packet = new DamagePacket
            {
                RawDamage = damage,
                Types = new System.Collections.Generic.HashSet<DamageType> { DamageType.Electricity },
                AttackTags = DamageBypassTag.Magic,
                Source = AttackSource.Other,
                SourceName = "Javelin of Lightning"
            };
            target.Stats.ApplyIncomingDamage(damage, packet);

            if (target.Stats.IsDead)
            {
                target.OnDeath();
                logNotes.Add($"{target.Stats.CharacterName} is slain by the lightning bolt!");
            }
        }

        return true;
    }

    public override string GetUsesDisplay()
    {
        return _lightningExpended ? "Lightning bolt expended" : "Lightning bolt available";
    }
}

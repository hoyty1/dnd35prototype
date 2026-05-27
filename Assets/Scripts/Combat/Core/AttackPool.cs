using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ProgressiveAttackMode
{
    None,
    StandardAttackCommitted,
    FullAttackCommitted
}

public enum ProgressiveAttackKind
{
    MainHand,
    OffHand,
    Natural,
    Special
}

[Serializable]
public sealed class ProgressiveAttackOption
{
    public string Id;
    public string DisplayName;
    public ProgressiveAttackKind Kind;
    public int AttackBonus;
    public bool IsPrimaryNatural;
    public int NaturalSequenceIndex;
    public bool IsUsed;

    public ProgressiveAttackOption Clone()
    {
        return new ProgressiveAttackOption
        {
            Id = Id,
            DisplayName = DisplayName,
            Kind = Kind,
            AttackBonus = AttackBonus,
            IsPrimaryNatural = IsPrimaryNatural,
            NaturalSequenceIndex = NaturalSequenceIndex,
            IsUsed = IsUsed
        };
    }
}

/// <summary>
/// Turn-scoped progressive attack pool used by house-rule iterative attacks.
/// First committed weapon attack consumes Standard action.
/// Second committed weapon attack transitions to Full Attack and consumes Move action.
/// </summary>
[Serializable]
public sealed class AttackPool
{
    private readonly List<ProgressiveAttackOption> _all = new List<ProgressiveAttackOption>();

    public ProgressiveAttackMode Mode { get; private set; } = ProgressiveAttackMode.None;
    public int AttacksCommitted { get; private set; }

    public IReadOnlyList<ProgressiveAttackOption> AllAttacks => _all;
    public IEnumerable<ProgressiveAttackOption> RemainingAttacks => _all.Where(a => a != null && !a.IsUsed);

    public void Clear()
    {
        _all.Clear();
        Mode = ProgressiveAttackMode.None;
        AttacksCommitted = 0;
    }

    public void SetAttacks(IEnumerable<ProgressiveAttackOption> attacks)
    {
        _all.Clear();
        if (attacks != null)
            _all.AddRange(attacks.Where(a => a != null).Select(a => a.Clone()));

        Mode = ProgressiveAttackMode.None;
        AttacksCommitted = 0;
    }

    public bool TryConsume(string id, out ProgressiveAttackOption consumed)
    {
        consumed = null;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        ProgressiveAttackOption option = _all.FirstOrDefault(a => !a.IsUsed && string.Equals(a.Id, id, StringComparison.Ordinal));
        if (option == null)
            return false;

        option.IsUsed = true;
        AttacksCommitted++;
        if (AttacksCommitted <= 0)
            Mode = ProgressiveAttackMode.None;
        else if (AttacksCommitted == 1)
            Mode = ProgressiveAttackMode.StandardAttackCommitted;
        else
            Mode = ProgressiveAttackMode.FullAttackCommitted;

        consumed = option;
        return true;
    }

    public int CountRemainingByKind(ProgressiveAttackKind kind)
    {
        return _all.Count(a => a != null && !a.IsUsed && a.Kind == kind);
    }

    public bool HasRemainingAttacks()
    {
        return _all.Any(a => a != null && !a.IsUsed);
    }
}

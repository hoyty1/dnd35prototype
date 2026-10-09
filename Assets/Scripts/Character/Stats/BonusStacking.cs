using System;
using System.Collections.Generic;
using System.Text;

/// <summary>What a typed bonus or penalty in a <see cref="BonusLedger"/> applies to.</summary>
public enum BonusTarget
{
    /// <summary>Every attack roll (weapon, natural, unarmed).</summary>
    AttackRoll,
    /// <summary>Weapon damage rolls (PHB p.264: Prayer's "weapon damage rolls"; Inspire Courage, Divine Favor).</summary>
    WeaponDamage,
    /// <summary>All three saving throws.</summary>
    AllSaves,
    Fortitude,
    Reflex,
    Will,
    /// <summary>
    /// Every saving throw against a fear effect only (Bane's -1, PHB p.203); SaveRules adds it for a fear save, stacked
    /// with that save's other modifiers (<see cref="CharacterStats.FearOnlySaveModifier"/>).
    /// </summary>
    FearSaves,
    /// <summary>Every skill check.</summary>
    AllSkills,
    /// <summary>One skill check; the entry's detail names the skill (case-insensitive).</summary>
    Skill,
    /// <summary>Land speed in feet (Longstrider, Expeditious Retreat, Haste: enhancement bonuses).</summary>
    LandSpeedFeet,
    Strength,
    Dexterity,
    Constitution,
    Intelligence,
    Wisdom,
    Charisma,
    /// <summary>Armor Class (one entry per type: armor, dodge, ...).</summary>
    ArmorClass
}

/// <summary>
/// One bonus (positive) or penalty (negative) with its type and source. <see cref="Source"/> identifies the effect for
/// the same-source rule: two modifiers from one source never stack (the same spell cast twice, PHB p.171). A null or
/// empty source counts as a source of its own.
/// </summary>
public struct TypedBonus
{
    public BonusType Type;
    public int Value;
    public string Source;
    public string Label;

    public TypedBonus(BonusType type, int value, string source, string label = null)
    {
        Type = type;
        Value = value;
        Source = source;
        Label = label;
    }
}

/// <summary>
/// The D&D 3.5e stacking rules for bonuses and penalties to one roll or statistic, shared by every bonus source (spells,
/// bardic music, items, class features, the charge) and by PCs and NPCs alike.
///
/// Rules applied (PHB p.171 "Bonus Types" and p.171-172 "Combining Magical Effects"; PHB glossary p.305 "bonus",
/// p.306 "circumstance bonus", p.307 "dodge bonus", p.310 "luck bonus", "modifier" and "morale bonus", p.313 "stack";
/// DMG p.21 "Bonus Types"):
/// <list type="number">
/// <item>Modifiers from the same source do not stack: of one source, only the best bonus and the worst penalty apply
/// (two Bless spells give +1, two Divine Favors the better one).</item>
/// <item>Bonuses of the same type do not stack: only the highest applies. Exceptions: dodge bonuses stack, circumstance
/// bonuses stack unless they arise from the same circumstance (the same source), and bonuses without a type stack with
/// every bonus unless they come from the same source (<see cref="BonusTypeHelper.DoesStack"/>). Luck, morale,
/// enhancement, insight, sacred, profane, resistance, competence, deflection and the other named types never stack with
/// themselves.</item>
/// <item>Penalties follow the same principle (PHB p.171: "a character taking two or more penalties of the same type
/// applies only the worst one"; glossary "modifier"): typed penalties of one type, only the worst; untyped, dodge and
/// circumstance penalties from different sources add up.</item>
/// <item>Bonuses and penalties of different types always stack, and a bonus and a penalty of the same type both apply.</item>
/// </list>
/// Racial bonuses follow <see cref="BonusTypeHelper.DoesStack"/> (they do not stack); whether PHB p.171 (racial
/// bonuses stack) or DMG p.21 governs is an open owner question (CHR-019).
/// </summary>
public static class BonusStacking
{
    /// <summary>The total of <paramref name="modifiers"/> under the stacking rules.</summary>
    public static int Combine(IList<TypedBonus> modifiers) => Combine(modifiers, null);

    /// <summary>
    /// The total of <paramref name="modifiers"/> under the stacking rules; <paramref name="applied"/> (optional) receives
    /// the modifiers that count, in input order of their first appearance.
    /// </summary>
    public static int Combine(IList<TypedBonus> modifiers, List<TypedBonus> applied)
    {
        if (modifiers == null || modifiers.Count == 0)
            return 0;

        // 1. Same source: the best bonus and the worst penalty of each source.
        var perSource = new List<TypedBonus>(modifiers.Count);
        for (int i = 0; i < modifiers.Count; i++)
        {
            TypedBonus m = modifiers[i];
            if (m.Value == 0)
                continue;
            int existing = string.IsNullOrEmpty(m.Source) ? -1 : FindSameSource(perSource, m);
            if (existing < 0)
                perSource.Add(m);
            else if (Math.Abs(m.Value) > Math.Abs(perSource[existing].Value))
                perSource[existing] = m;
        }

        // 2. Per type and sign: stacking types add up; any other type keeps its best bonus and its worst penalty.
        int total = 0;
        var bestIndex = new Dictionary<long, int>();
        var kept = new List<TypedBonus>(perSource.Count);
        for (int i = 0; i < perSource.Count; i++)
        {
            TypedBonus m = perSource[i];
            if (BonusTypeHelper.DoesStack(m.Type))
            {
                kept.Add(m);
                continue;
            }
            long key = ((long)m.Type << 1) | (m.Value > 0 ? 1L : 0L);
            if (bestIndex.TryGetValue(key, out int at))
            {
                if (Math.Abs(m.Value) > Math.Abs(kept[at].Value))
                    kept[at] = m;
            }
            else
            {
                bestIndex[key] = kept.Count;
                kept.Add(m);
            }
        }

        for (int i = 0; i < kept.Count; i++)
        {
            total += kept[i].Value;
            applied?.Add(kept[i]);
        }
        return total;
    }

    /// <summary>The highest bonus of <paramref name="type"/> among <paramref name="modifiers"/> (0 when there is none).</summary>
    public static int BestBonusOfType(IList<TypedBonus> modifiers, BonusType type)
    {
        int best = 0;
        if (modifiers == null)
            return 0;
        for (int i = 0; i < modifiers.Count; i++)
            if (modifiers[i].Type == type && modifiers[i].Value > best)
                best = modifiers[i].Value;
        return best;
    }

    /// <summary>"Bless +1, Divine Favor +2": the modifiers that count, by label (or source), for logs.</summary>
    public static string Describe(IList<TypedBonus> modifiers)
    {
        var applied = new List<TypedBonus>();
        Combine(modifiers, applied);
        if (applied.Count == 0)
            return string.Empty;
        var sb = new StringBuilder();
        for (int i = 0; i < applied.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            string name = !string.IsNullOrEmpty(applied[i].Label) ? applied[i].Label : (applied[i].Source ?? "bonus");
            sb.Append(name).Append(' ').Append(applied[i].Value > 0 ? "+" : string.Empty).Append(applied[i].Value);
        }
        return sb.ToString();
    }

    private static int FindSameSource(List<TypedBonus> list, TypedBonus m)
    {
        bool positive = m.Value > 0;
        for (int i = 0; i < list.Count; i++)
        {
            if ((list[i].Value > 0) != positive)
                continue;
            if (string.Equals(list[i].Source, m.Source, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }
}

/// <summary>
/// The typed bonuses and penalties a creature has from effects (spells, bardic music, the charge, ...), each recorded
/// with its owner (the effect instance that registered it, so removing the effect removes exactly its entries), its
/// target, its type and its stacking source. Every total is computed on read with <see cref="BonusStacking"/>, so the
/// order in which effects begin and end never matters and a weaker effect applies again when a stronger one ends
/// (PHB p.171-172). One per <see cref="CharacterStats"/> (<see cref="CharacterStats.Bonuses"/>).
/// </summary>
public sealed class BonusLedger
{
    private sealed class Entry
    {
        public object Owner;
        public BonusTarget Target;
        public string Detail;
        public TypedBonus Bonus;
    }

    private readonly List<Entry> _entries = new List<Entry>();

    /// <summary>
    /// Totals already computed since the last change (attack, damage, skill and speed totals are read in AI scoring and
    /// movement loops); cleared whenever an entry is added or removed, so every read still reflects the current entries.
    /// </summary>
    private readonly Dictionary<CacheKey, int> _cache = new Dictionary<CacheKey, int>();
    private readonly List<TypedBonus> _scratch = new List<TypedBonus>();

    private const int KindTotal = -1;

    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        private readonly int _kind; // KindTotal, or the BonusType of a best-of-type read
        private readonly BonusTarget _target;
        private readonly int _also;
        private readonly string _detail;

        public CacheKey(int kind, BonusTarget target, BonusTarget? also, string detail)
        {
            _kind = kind;
            _target = target;
            _also = also.HasValue ? (int)also.Value : -1;
            _detail = string.IsNullOrEmpty(detail) ? null : detail;
        }

        public bool Equals(CacheKey other)
        {
            return _kind == other._kind && _target == other._target && _also == other._also
                && string.Equals(_detail, other._detail, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => obj is CacheKey other && Equals(other);

        public override int GetHashCode()
        {
            int h = (_kind * 397) ^ ((int)_target * 31) ^ _also;
            return _detail == null ? h : (h * 17) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(_detail);
        }
    }

    /// <summary>Raised after any entry is added or removed.</summary>
    public event Action Changed;

    private void NotifyChanged()
    {
        _cache.Clear();
        Changed?.Invoke();
    }

    /// <summary>Number of entries (for tests and logs).</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Records a modifier owned by <paramref name="owner"/>. <paramref name="detail"/> names the skill for
    /// <see cref="BonusTarget.Skill"/>. A zero value is ignored.
    /// </summary>
    public void Add(object owner, BonusTarget target, BonusType type, int value, string source, string label = null, string detail = null)
    {
        if (owner == null || value == 0)
            return;
        _entries.Add(new Entry
        {
            Owner = owner,
            Target = target,
            Detail = detail,
            Bonus = new TypedBonus(type, value, source, label)
        });
        NotifyChanged();
    }

    /// <summary>
    /// Sets the one modifier that the effect named <paramref name="key"/> gives to <paramref name="target"/> (the key is
    /// both owner and stacking source), replacing a previous value; 0 removes it.
    /// </summary>
    public void Set(string key, BonusTarget target, BonusType type, int value, string label = null, string detail = null)
    {
        if (string.IsNullOrEmpty(key))
            return;
        bool removed = RemoveWhere(e => Equals(e.Owner, key) && e.Target == target && DetailMatches(e.Detail, detail, exact: true));
        if (value != 0)
            Add(key, target, type, value, key, label, detail);
        else if (removed)
            NotifyChanged();
    }

    /// <summary>Removes every modifier of <paramref name="owner"/>. Returns true when one was removed.</summary>
    public bool RemoveOwner(object owner)
    {
        if (owner == null)
            return false;
        bool removed = RemoveWhere(e => Equals(e.Owner, owner));
        if (removed)
            NotifyChanged();
        return removed;
    }

    /// <summary>True when at least one modifier applies to <paramref name="target"/>.</summary>
    public bool HasTarget(BonusTarget target)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].Target == target)
                return true;
        return false;
    }

    /// <summary>True when <paramref name="owner"/> has at least one modifier here.</summary>
    public bool HasOwner(object owner)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (Equals(_entries[i].Owner, owner))
                return true;
        return false;
    }

    /// <summary>The modifiers <paramref name="owner"/> gives to <paramref name="target"/>, summed (0 when none).</summary>
    public int OwnerValue(object owner, BonusTarget target)
    {
        int sum = 0;
        for (int i = 0; i < _entries.Count; i++)
            if (Equals(_entries[i].Owner, owner) && _entries[i].Target == target)
                sum += _entries[i].Bonus.Value;
        return sum;
    }

    /// <summary>
    /// Appends to <paramref name="into"/> the modifiers for <paramref name="target"/> and, when given, for
    /// <paramref name="alsoTarget"/> (the umbrella target, such as AllSaves for Will). For <see cref="BonusTarget.Skill"/>
    /// only entries whose detail matches <paramref name="detail"/> are taken.
    /// </summary>
    public void Collect(BonusTarget target, List<TypedBonus> into, string detail = null, BonusTarget? alsoTarget = null)
    {
        if (into == null)
            return;
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];
            bool match = e.Target == target && (target != BonusTarget.Skill || DetailMatches(e.Detail, detail, exact: false));
            if (!match && alsoTarget.HasValue && e.Target == alsoTarget.Value)
                match = alsoTarget.Value != BonusTarget.Skill || DetailMatches(e.Detail, detail, exact: false);
            if (match)
                into.Add(e.Bonus);
        }
    }

    /// <summary>The stacked total for <paramref name="target"/> (and <paramref name="alsoTarget"/>).</summary>
    public int Total(BonusTarget target, string detail = null, BonusTarget? alsoTarget = null)
    {
        if (_entries.Count == 0)
            return 0;
        var key = new CacheKey(KindTotal, target, alsoTarget, detail);
        if (_cache.TryGetValue(key, out int cached))
            return cached;
        _scratch.Clear();
        Collect(target, _scratch, detail, alsoTarget);
        int total = BonusStacking.Combine(_scratch);
        _scratch.Clear();
        _cache[key] = total;
        return total;
    }

    /// <summary>The highest bonus of <paramref name="type"/> for <paramref name="target"/> (and <paramref name="alsoTarget"/>).</summary>
    public int BestOfType(BonusTarget target, BonusType type, string detail = null, BonusTarget? alsoTarget = null)
    {
        if (_entries.Count == 0)
            return 0;
        var key = new CacheKey((int)type, target, alsoTarget, detail);
        if (_cache.TryGetValue(key, out int cached))
            return cached;
        _scratch.Clear();
        Collect(target, _scratch, detail, alsoTarget);
        int best = BonusStacking.BestBonusOfType(_scratch, type);
        _scratch.Clear();
        _cache[key] = best;
        return best;
    }

    /// <summary>The modifiers for <paramref name="target"/> (and <paramref name="alsoTarget"/>) that count after stacking.</summary>
    public List<TypedBonus> AppliedModifiers(BonusTarget target, string detail = null, BonusTarget? alsoTarget = null)
    {
        var list = new List<TypedBonus>();
        Collect(target, list, detail, alsoTarget);
        var applied = new List<TypedBonus>();
        BonusStacking.Combine(list, applied);
        return applied;
    }

    /// <summary>"Bless, Divine Favor": the names of the modifiers that count for <paramref name="target"/>, for log labels.</summary>
    public string DescribeSources(BonusTarget target, string detail = null, BonusTarget? alsoTarget = null)
    {
        List<TypedBonus> applied = AppliedModifiers(target, detail, alsoTarget);
        var names = new List<string>();
        for (int i = 0; i < applied.Count; i++)
        {
            string name = !string.IsNullOrEmpty(applied[i].Label) ? applied[i].Label : (applied[i].Source ?? "bonus");
            if (!names.Contains(name))
                names.Add(name);
        }
        return string.Join(", ", names);
    }

    /// <summary>"Bless +1, Divine Favor +2" for <paramref name="target"/>: the modifiers that count, for logs.</summary>
    public string Describe(BonusTarget target, string detail = null, BonusTarget? alsoTarget = null)
    {
        var list = new List<TypedBonus>();
        Collect(target, list, detail, alsoTarget);
        return BonusStacking.Describe(list);
    }

    /// <summary>Removes every entry (a creature rebuilt from scratch).</summary>
    public void Clear()
    {
        if (_entries.Count == 0)
            return;
        _entries.Clear();
        NotifyChanged();
    }

    private bool RemoveWhere(Predicate<Entry> match)
    {
        return _entries.RemoveAll(match) > 0;
    }

    private static bool DetailMatches(string entryDetail, string wanted, bool exact)
    {
        if (string.IsNullOrEmpty(entryDetail) && string.IsNullOrEmpty(wanted))
            return true;
        return string.Equals(entryDetail, wanted, StringComparison.OrdinalIgnoreCase);
    }
}

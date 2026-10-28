using System;
using System.Collections.Generic;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>The wards an attacker must save against before it may directly attack the warded creature.</summary>
public enum WardKind
{
    /// <summary>Sanctuary (PHB p.274): every opponent, Will negates, one save per casting.</summary>
    Sanctuary,

    /// <summary>
    /// Hide from Undead (PHB p.241): undead observers only; mindless undead act as though the warded creature is not
    /// there, an intelligent undead that fails its one save cannot see, hear or smell it (total concealment).
    /// </summary>
    HideFromUndead
}

/// <summary>The commit of <see cref="WardSaveLedger.Commit"/>.</summary>
public enum WardVerdict
{
    /// <summary>The target carries no ward this attacker must save against.</summary>
    NoWard,

    /// <summary>Every ward's save succeeded (now or at an earlier commit): the attack goes on.</summary>
    Passed,

    /// <summary>A Sanctuary save failed (now or earlier), or a mindless undead met Hide from Undead: the attempt is lost.</summary>
    Failed
}

/// <summary>
/// The Sanctuary and Hide from Undead attacker saves, rolled once per (attacker, ward casting) for every creature, PC or
/// NPC (batch 12 step 12, decision D6; CMB-093, AI-005). <see cref="Predict"/> never rolls: target choice, the PC
/// highlights and AI scoring read it. <see cref="Commit"/> is called by the attack, maneuver, charge, AoO and cast
/// executors at the moment the attack is attempted: it rolls the Will save the first time (SaveContext.ForSpellId), logs
/// it once and keeps the result for the rest of that casting's duration.
/// - Sanctuary (PHB p.274): on a success the opponent is unaffected by that casting; on a failure the attempted attack is
///   lost and it cannot directly attack the warded creature while that casting lasts.
/// - Hide from Undead (PHB p.241) is a perception ward, not an attack bar: undead cannot see, hear or smell the warded
///   creature, and no sense of theirs locates it. A mindless undead acts as though it is not there (no save; Predict
///   refuses, Commit loses the attempt). An intelligent undead makes one Will save per casting, rolled the first time it
///   would perceive the creature: its turn-start perception step (<see cref="RollHideFromUndeadSaves"/>, from
///   PerceptionRules.ProcessTurnStart) or an attack it attempts before that (Commit). A success lets it perceive the
///   creature without ending the spell; until then, and after a failure, the creature is unseen by it
///   (<see cref="IsHiddenFromUndead"/>: total concealment, its 50% miss chance, no AoO, no pinpoint by hearing), and it may
///   still attack the square it believes the creature is in, as for any unseen foe (PHB p.152). A failure loses no attack
///   (delegated RAW reading 2026-10-28, RULES_COVERAGE 4.1 D6). The spell ends only through the warded creature's own acts
///   (CombatFlowService.BreakProtectiveWardsOnAttack).
/// A ward casting is keyed by its ActiveSpellEffect (CharacterStats.ActiveSanctuaryEffect / ActiveHideFromUndeadEffect;
/// a fixture that sets only the flag falls back to the warded creature's stats). Entries go when the effect ends
/// (StatusEffectManager, <see cref="ForgetWard"/>) and when the attacker's slot is reused (<see cref="ForgetAttacker"/>,
/// CharacterController.ResetForNewCreature and GameManager.ResetCharacterSlotForSpawn).
/// </summary>
public static class WardSaveLedger
{
    private readonly struct Key : IEquatable<Key>
    {
        public readonly CharacterController Attacker;
        public readonly object Ward;
        public readonly WardKind Kind;

        public Key(CharacterController attacker, object ward, WardKind kind)
        {
            Attacker = attacker;
            Ward = ward;
            Kind = kind;
        }

        public bool Equals(Key other)
            => ReferenceEquals(Attacker, other.Attacker) && ReferenceEquals(Ward, other.Ward) && Kind == other.Kind;

        public override bool Equals(object obj) => obj is Key k && Equals(k);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Attacker != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Attacker) : 0;
                h = h * 397 ^ (Ward != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Ward) : 0);
                return h * 397 ^ (int)Kind;
            }
        }
    }

    private struct Ward
    {
        public WardKind Kind;
        public object Token;
        public int Dc;
    }

    /// <summary>true = the attacker's save against that casting succeeded; false = it failed.</summary>
    private static readonly Dictionary<Key, bool> _saves = new Dictionary<Key, bool>();

    /// <summary>Diagnostics: Will saves rolled by <see cref="Commit"/> since the domain loaded (Tests.Combat.AttackLegalityTests).</summary>
    public static int SavesRolled { get; private set; }

    /// <summary>The ward saves recorded now (diagnostics).</summary>
    public static int Count => _saves.Count;

    private static readonly List<Ward> _scratch = new List<Ward>(2);

    private static List<Ward> WardsAgainst(CharacterController attacker, CharacterController target)
    {
        _scratch.Clear();
        if (attacker == null || target == null || attacker == target || attacker.Stats == null || target.Stats == null)
            return _scratch;

        CharacterStats t = target.Stats;
        if (t.SanctuaryActive)
            _scratch.Add(new Ward { Kind = WardKind.Sanctuary, Token = (object)t.ActiveSanctuaryEffect ?? t, Dc = t.SanctuaryDC });
        if (t.HideFromUndeadActive && IsUndead(attacker))
            _scratch.Add(new Ward { Kind = WardKind.HideFromUndead, Token = (object)t.ActiveHideFromUndeadEffect ?? t, Dc = t.HideFromUndeadDC });
        return _scratch;
    }

    /// <summary>Undead by creature type or tag, the test Turn Undead uses (GameManager.IsUndeadCharacter).</summary>
    public static bool IsUndead(CharacterController c) => GameManager.IsUndeadCharacter(c);

    private static bool IsMindless(CharacterController c) => c != null && c.Stats != null && c.Stats.IsMindless;

    /// <summary>
    /// What <paramref name="attacker"/> knows about directly attacking <paramref name="target"/> through its wards: not ok
    /// after a failed Sanctuary save against a casting still active (or for a mindless undead against Hide from Undead);
    /// ok with <see cref="LegalityRisk.WardSaveUnrolled"/> when a Sanctuary save is still to roll; plain ok otherwise
    /// (Hide from Undead hides the creature from an intelligent undead instead, <see cref="IsHiddenFromUndead"/>).
    /// Never rolls, never logs.
    /// </summary>
    public static Legality Predict(CharacterController attacker, CharacterController target)
    {
        List<Ward> wards = WardsAgainst(attacker, target);
        if (wards.Count == 0)
            return Legality.Allowed();

        LegalityRisk risk = LegalityRisk.None;
        for (int i = 0; i < wards.Count; i++)
        {
            Ward w = wards[i];
            if (w.Kind == WardKind.HideFromUndead)
            {
                // Mindless: the warded creature is not there for it. Intelligent: a perception ward only (concealment,
                // IsHiddenFromUndead), never a refusal or a risk of losing the attack.
                if (IsMindless(attacker))
                    return Legality.Refused($"{Name(attacker)} is mindless undead and acts as though {Name(target)} is not there (Hide from Undead, PHB p.241)");
                continue;
            }

            if (_saves.TryGetValue(new Key(attacker, w.Token, w.Kind), out bool passed))
            {
                if (!passed)
                    return Legality.Refused($"{Name(attacker)} failed its save against {Name(target)}'s Sanctuary and cannot attack it (PHB p.274)");
                continue;
            }

            risk = LegalityRisk.WardSaveUnrolled;
        }

        return Legality.Allowed(risk);
    }

    /// <summary>True when <see cref="Predict"/> refuses: the attacker already knows it cannot directly attack the target.</summary>
    public static bool HasKnownFailure(CharacterController attacker, CharacterController target) => !Predict(attacker, target).Ok;

    /// <summary>
    /// The attempt of a direct attack by <paramref name="attacker"/> on <paramref name="target"/>: rolls each ward's Will
    /// save not yet rolled for its casting (once, logged once), and returns whether the attack may follow through. A
    /// caller that gets <see cref="WardVerdict.Failed"/> spends the attempted step or action (a spell keeps its slot, D6)
    /// and stops that attack; this method logs the failure.
    /// </summary>
    public static WardVerdict Commit(CharacterController attacker, CharacterController target)
    {
        List<Ward> wards = new List<Ward>(WardsAgainst(attacker, target));
        if (wards.Count == 0)
            return WardVerdict.NoWard;

        for (int i = 0; i < wards.Count; i++)
        {
            Ward w = wards[i];
            if (w.Kind == WardKind.HideFromUndead)
            {
                if (IsMindless(attacker))
                {
                    Log(CombatLogHelper.Failure("", $"👻 {Name(attacker)} cannot perceive {Name(target)}: Hide from Undead (mindless, no save; PHB p.241). The attack is lost."));
                    return WardVerdict.Failed;
                }
                // An intelligent undead attacking before its perception step saves now; either way the attack goes on,
                // against an unseen target after a failure (total concealment, IsHiddenFromUndead).
                RollHideFromUndeadSave(attacker, target, w);
                continue;
            }

            var key = new Key(attacker, w.Token, w.Kind);
            if (_saves.TryGetValue(key, out bool known))
            {
                if (!known)
                {
                    Log(CombatLogHelper.Failure("🛡", $"{Name(attacker)} cannot attack {Name(target)}: it already failed its save against this {Label(w.Kind)}. The attack is lost."));
                    return WardVerdict.Failed;
                }
                continue;
            }

            SavingThrowResolver.SaveResult save = RollSave(attacker, target, w);
            _saves[key] = save.Succeeded;

            if (save.Succeeded)
            {
                Log(CombatLogHelper.Defensive("🛡", $"{Name(attacker)} overcomes {Name(target)}'s Sanctuary (Will {save.Total} vs DC {w.Dc}{save.NaturalSuffix}) and is unaffected by it from now on."));
                continue;
            }

            Log(CombatLogHelper.Defensive("🛡", $"{Name(attacker)} cannot follow through against {Name(target)}: Sanctuary! (Will {save.Total} vs DC {w.Dc}{save.NaturalSuffix}) The attack is lost, and it cannot attack {Name(target)} while the spell lasts (PHB p.274)."));
            return WardVerdict.Failed;
        }

        return WardVerdict.Passed;
    }

    /// <summary>
    /// The turn-start step of <paramref name="observer"/> (PerceptionRules.ProcessTurnStart): an intelligent undead makes
    /// its one Will save against each Hide from Undead casting on another creature it has not saved against yet (PHB
    /// p.241; delegated RAW reading 2026-10-28, D6). A mindless undead makes none. Logged once per casting.
    /// </summary>
    public static void RollHideFromUndeadSaves(CharacterController observer, IList<CharacterController> all)
    {
        if (observer == null || observer.Stats == null || all == null || IsMindless(observer) || !IsUndead(observer))
            return;
        for (int i = 0; i < all.Count; i++)
        {
            CharacterController warded = all[i];
            if (warded == null || warded == observer || warded.Stats == null || warded.Stats.IsDead || !warded.Stats.HideFromUndeadActive)
                continue;
            CharacterStats t = warded.Stats;
            RollHideFromUndeadSave(observer, warded, new Ward { Kind = WardKind.HideFromUndead, Token = (object)t.ActiveHideFromUndeadEffect ?? t, Dc = t.HideFromUndeadDC });
        }
    }

    /// <summary>
    /// True when <paramref name="observer"/> cannot perceive <paramref name="target"/> through its Hide from Undead (PHB
    /// p.241): the target carries the ward, the observer is undead, and it is mindless, has not saved yet, or failed its
    /// save against that casting. CharacterController.GetMissChance gives such a target total concealment (50%) from that
    /// observer and CanVisuallyPerceive is false; LastKnownPositionTracker pinpoints it by no sense. Never rolls.
    /// </summary>
    public static bool IsHiddenFromUndead(CharacterController observer, CharacterController target)
    {
        if (observer == null || target == null || observer == target || target.Stats == null || !target.Stats.HideFromUndeadActive)
            return false;
        if (observer.Stats == null || !IsUndead(observer))
            return false;
        if (IsMindless(observer))
            return true;
        CharacterStats t = target.Stats;
        return !(_saves.TryGetValue(new Key(observer, (object)t.ActiveHideFromUndeadEffect ?? t, WardKind.HideFromUndead), out bool passed) && passed);
    }

    private static void RollHideFromUndeadSave(CharacterController observer, CharacterController target, Ward w)
    {
        var key = new Key(observer, w.Token, WardKind.HideFromUndead);
        if (_saves.ContainsKey(key))
            return;
        SavingThrowResolver.SaveResult save = RollSave(observer, target, w);
        _saves[key] = save.Succeeded;
        Log(save.Succeeded
            ? CombatLogHelper.Info("", $"👻 {Name(observer)} sees through {Name(target)}'s Hide from Undead (Will {save.Total} vs DC {w.Dc}{save.NaturalSuffix}); the spell goes on for the others.")
            : CombatLogHelper.Info("", $"👻 {Name(observer)} cannot see, hear or smell {Name(target)}: Hide from Undead (Will {save.Total} vs DC {w.Dc}{save.NaturalSuffix}). {Name(target)} is unseen by it while the spell lasts (PHB p.241)."));
    }

    private static SavingThrowResolver.SaveResult RollSave(CharacterController attacker, CharacterController target, Ward w)
    {
        string spellId = w.Kind == WardKind.Sanctuary ? SpellNames.SANCTUARY : SpellNames.HIDE_FROM_UNDEAD;
        SavingThrowResolver.SaveResult save = SavingThrowResolver.ResolveWillSave(attacker.Stats, w.Dc, Label(w.Kind), SaveContext.ForSpellId(spellId));
        SavesRolled++;
        Debug.Log($"[Ward] {Name(attacker)} vs {Name(target)}'s {Label(w.Kind)}: Will d20 {save.Roll} {CharacterStats.FormatMod(save.Modifier)} = {save.Total} vs DC {w.Dc} -> {(save.Succeeded ? "pass" : "fail")}");
        return save;
    }

    /// <summary>
    /// Drops <paramref name="attacker"/>'s Sanctuary save against <paramref name="target"/>'s current casting: a cast whose
    /// caster backed out at the AoO prompt attempted nothing, so its save is not spent (the cast core's cancel, SPL-152).
    /// </summary>
    public static void ForgetSanctuarySave(CharacterController attacker, CharacterController target)
    {
        if (attacker == null || target == null || target.Stats == null || _saves.Count == 0)
            return;
        CharacterStats t = target.Stats;
        _saves.Remove(new Key(attacker, (object)t.ActiveSanctuaryEffect ?? t, WardKind.Sanctuary));
    }

    /// <summary>Drops every save made against the ward casting <paramref name="wardToken"/> (its effect ended).</summary>
    public static void ForgetWard(object wardToken)
    {
        if (wardToken == null || _saves.Count == 0)
            return;
        RemoveWhere(k => ReferenceEquals(k.Ward, wardToken));
    }

    /// <summary>Drops every save <paramref name="attacker"/> made (its slot is reused for another creature).</summary>
    public static void ForgetAttacker(CharacterController attacker)
    {
        if (attacker == null || _saves.Count == 0)
            return;
        RemoveWhere(k => ReferenceEquals(k.Attacker, attacker));
    }

    /// <summary>Drops every entry (tests; a new encounter).</summary>
    public static void Clear() => _saves.Clear();

    private static void RemoveWhere(Predicate<Key> match)
    {
        List<Key> doomed = null;
        foreach (Key k in _saves.Keys)
            if (match(k))
                (doomed ??= new List<Key>()).Add(k);
        if (doomed == null)
            return;
        for (int i = 0; i < doomed.Count; i++)
            _saves.Remove(doomed[i]);
    }

    private static string Label(WardKind kind) => kind == WardKind.Sanctuary ? "Sanctuary" : "Hide from Undead";

    private static string Name(CharacterController c) => c != null && c.Stats != null ? c.Stats.CharacterName : "Someone";

    private static void Log(string line) => GameManager.Instance?.CombatUI?.ShowCombatLog(line);
}

using System;
using System.Collections.Generic;
using System.Linq;
using DND35.Magic;
using DND35e.Identifiers;
using UnityEngine;

/// <summary>
/// GameManager partial class: Dispel Magic &amp; Counterspell Systems
/// 
/// All logic has been extracted to <see cref="DispelMagicService"/>.
/// This file retains thin delegate wrappers for backward compatibility:
///   - Static methods delegate to DispelMagicService static equivalents
///   - Instance methods delegate to _dispelMagicService instance
///   - Tests calling GameManager.GetDispelDC / GameManager.Instance.PerformTargetedDispel
///     continue to work unchanged
/// </summary>
public partial class GameManager
{
    // ═══════════════════════════════════════════════════════════════════
    //  DISPEL MAGIC & COUNTERSPELL — delegates to DispelMagicService
    // ═══════════════════════════════════════════════════════════════════

    [SerializeField] private DispelMagicService _dispelMagicService;

    /// <summary>Public accessor for the dispel magic service.</summary>
    public DispelMagicService DispelMagic => _dispelMagicService;

    // ── Static delegates ──────────────────────────────────────────────

    /// <summary>Delegates to <see cref="DispelMagicService.PerformDispelCheck"/>.</summary>
    public static bool PerformDispelCheck(int casterLevel, int targetSpellCasterLevel, bool isOwnSpell)
        => DispelMagicService.PerformDispelCheck(casterLevel, targetSpellCasterLevel, isOwnSpell);

    /// <summary>Delegates to <see cref="DispelMagicService.RollDispelCheck"/>.</summary>
    public static int RollDispelCheck(int casterLevel)
        => DispelMagicService.RollDispelCheck(casterLevel);

    /// <summary>Delegates to <see cref="DispelMagicService.GetDispelDC"/>.</summary>
    public static int GetDispelDC(int targetSpellCasterLevel)
        => DispelMagicService.GetDispelDC(targetSpellCasterLevel);

    /// <summary>Delegates to <see cref="DispelMagicService.PerformCounterspellDispelCheck"/>.</summary>
    public static bool PerformCounterspellDispelCheck(int counterCL, int enemyCL, int maxCLBonus = 10)
        => DispelMagicService.PerformCounterspellDispelCheck(counterCL, enemyCL, maxCLBonus);

    // ── Instance delegates ────────────────────────────────────────────

    /// <summary>Delegates to <see cref="DispelMagicService.PerformTargetedDispel"/>.</summary>
    public void PerformTargetedDispel(CharacterController caster, CharacterController target)
        => _dispelMagicService?.PerformTargetedDispel(caster, target);

    /// <summary>Delegates to <see cref="DispelMagicService.PerformAreaDispel"/>.</summary>
    public void PerformAreaDispel(CharacterController caster, List<CharacterController> targets)
        => _dispelMagicService?.PerformAreaDispel(caster, targets);

    /// <summary>Delegates to <see cref="DispelMagicService.TryResolveCounterspell"/>.</summary>
    public CounterspellResult TryResolveCounterspell(CharacterController caster, SpellData spell, bool isSpellLikeAbility = false)
        => _dispelMagicService?.TryResolveCounterspell(caster, spell, isSpellLikeAbility);

    /// <summary>Delegates to <see cref="DispelMagicService.ExpireReadiedCounterspell"/>.</summary>
    public void ExpireReadiedCounterspell(CharacterController character)
        => _dispelMagicService?.ExpireReadiedCounterspell(character);

    /// <summary>
    /// Get the current combat round number. Used for counterspell expiration tracking.
    /// Alias for CurrentRound from the turn service.
    /// </summary>
    public int CurrentRoundNumber => CurrentRound;
}

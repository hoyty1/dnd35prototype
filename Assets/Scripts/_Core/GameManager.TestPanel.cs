using UnityEngine;

/// <summary>
/// GameManager partial class: SpellTestingPanel Integration
/// 
/// Provides a clean entry-point for the F12 dev panel to cast spells
/// without requiring a real PC turn.  The normal cast flow checks
/// ActivePC, which needs CurrentPhase==PCTurn AND a valid CurrentCharacter
/// — neither is true when the panel is used outside combat.
///
/// This partial class:
///   1. Stores a "_testPanelCaster" so OnCellClicked can route through
///      the existing targeting code even when ActivePC is null.
///   2. Exposes TestCastSpellFromPanel() which sets up all the internal
///      pending-spell state and kicks off BeginPendingSpellTargeting.
///   3. Exposes GetTestPanelCaster() so OnCellClicked can fall back to it.
/// </summary>
public partial class GameManager
{
    // ── Test-panel state ──────────────────────────────────────────
    private CharacterController _testPanelCaster;
    private bool _testPanelCastActive;

    /// <summary>
    /// Returns the test-panel caster when a test cast is active, null otherwise.
    /// Used by OnCellClicked as a fallback when ActivePC is null.
    /// </summary>
    public CharacterController GetTestPanelCaster()
    {
        return _testPanelCastActive ? _testPanelCaster : null;
    }

    /// <summary>
    /// Entry-point called by SpellTestingPanel.  Sets up all internal
    /// pending-spell fields and calls BeginPendingSpellTargeting, bypassing
    /// the normal ActivePC / turn-phase guards.
    /// </summary>
    public void TestCastSpellFromPanel(CharacterController caster, SpellData spell,
                                        bool infiniteSlots, MetamagicData metamagic = null)
    {
        Debug.Log($"[TestPanel] ▶ TestCastSpellFromPanel  spell={spell?.Name}  caster={caster?.Stats?.CharacterName}  infinite={infiniteSlots}");

        if (caster == null)
        {
            Debug.LogWarning("[TestPanel] Caster is null – aborting test cast.");
            return;
        }
        if (spell == null)
        {
            Debug.LogWarning("[TestPanel] Spell is null – aborting test cast.");
            return;
        }

        // ── 1. Store test-panel caster so OnCellClicked can find it ──
        _testPanelCaster = caster;
        _testPanelCastActive = true;
        Debug.Log($"[TestPanel]   _testPanelCastActive = true, caster stored: {caster.Stats?.CharacterName}");

        // ── 2. Ensure CurrentPhase allows the sub-phase switch ──
        //       (We force PCTurn; it will be reverted after the cast resolves
        //        inside CleanupTestPanelCast or if user cancels.)
        TurnPhase prevPhase = CurrentPhase;
        CurrentPhase = TurnPhase.PCTurn;
        Debug.Log($"[TestPanel]   CurrentPhase forced from {prevPhase} → {CurrentPhase}");

        // ── 3. Populate pending-spell fields (mirrors OnSpellSelectedWithMetamagic) ──
        _pendingSpell = spell;
        _pendingMetamagic = metamagic ?? new MetamagicData();
        _pendingSpellFromHeldCharge = false;
        _pendingAnimateRopeItem = null;
        _pendingResistEnergyType = null;
        _pendingProtectionFromEnergyType = null;
        _pendingMagicWeaponItem = null;
        _pendingKeenEdgeItem = null;
        _pendingKeenEdgeIsAmmo = false;
        _pendingGreaterMagicWeaponItem = null;
        _pendingDisguiseSelfRace = null;
        _pendingSummonSelection = null;
        _pendingSummonListLevel = 0;
        _pendingSummonCountInfo = null;
        _pendingSummonSwarmNpcId = null;
        Debug.Log($"[TestPanel]   Pending fields set: spell={_pendingSpell.Name}");

        // ── 4. Apply metamagic clone if needed ──
        if (metamagic != null && metamagic.HasAnyMetamagic)
        {
            _pendingSpell = spell.Clone();
            SpellCaster.ApplyMetamagicToSpellData(_pendingSpell, metamagic);
            Debug.Log($"[TestPanel]   Metamagic applied: {metamagic.GetSummary(spell.SpellLevel)}");
        }

        // ── 5. Kick off normal targeting pipeline ──
        Debug.Log($"[TestPanel]   Calling BeginPendingSpellTargeting(caster={caster.Stats?.CharacterName})");
        BeginPendingSpellTargeting(caster);

        Debug.Log($"[TestPanel]   After BeginPendingSpellTargeting: SubPhase={CurrentSubPhase}, AttackMode={_pendingAttackMode}");
    }

    /// <summary>
    /// Resets the test-panel override state.  Called after a test-panel cast
    /// resolves (or is cancelled) so normal combat flow is not polluted.
    /// </summary>
    public void CleanupTestPanelCast()
    {
        if (!_testPanelCastActive) return;
        Debug.Log("[TestPanel] 🧹 CleanupTestPanelCast – resetting test-panel state.");
        _testPanelCastActive = false;
        _testPanelCaster = null;
    }
}

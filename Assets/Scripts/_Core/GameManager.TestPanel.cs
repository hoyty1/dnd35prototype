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
///   2. Exposes TestCastSpellFromPanel() which opens the cast's own context
///      (a TestPanel source with its bypass flags, batch 11 step 4) and kicks
///      off BeginPendingSpellTargeting.
///   3. Exposes GetTestPanelCaster() so OnCellClicked can fall back to it.
/// The F12 privileges (no component pouch or costly components, no arcane
/// spell failure, unlimited slots requested) are TestPanelBypass flags on the
/// cast's CastSource: they hold through the whole cast, single-target casts
/// included, and end with it (SPL-036).
/// </summary>
public partial class GameManager
{
    // ── Test-panel state ──────────────────────────────────────────
    private CharacterController _testPanelCaster;
    // _testPanelCastActive: the open cast's source is the test panel (GameManager.CastContext.cs, batch 11 step 4).

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

        // ── 1. (The test-panel caster is stored after the cast opens, step 3: BeginCast closes an F12 cast the
        //       panel left open, and closing a TestPanel cast forgets the caster.) ──

        // ── 2. Ensure CurrentPhase allows the sub-phase switch ──
        //       (We force PCTurn; it will be reverted after the cast resolves
        //        inside CleanupTestPanelCast or if user cancels.)
        TurnPhase prevPhase = CurrentPhase;
        CurrentPhase = TurnPhase.PCTurn;
        Debug.Log($"[TestPanel]   CurrentPhase forced from {prevPhase} → {CurrentPhase}");

        // ── 3. Open the cast's own context (mirrors OnSpellSelectedWithMetamagic): a clone of the template, never the
        //       database instance, with the metamagic's pre-cast changes, and the test panel's privileges as explicit flags ──
        var source = new CastSource
        {
            Kind = CastSourceKind.TestPanel,
            Bypass = TestPanelBypass.Components | TestPanelBypass.ArcaneFailure | (infiniteSlots ? TestPanelBypass.Slots : TestPanelBypass.None)
        };
        BeginCast(SpellCastRequest.ForUi(caster, spell, metamagic ?? new MetamagicData(), source));
        Debug.Log($"[TestPanel]   Cast opened: spell={_pendingSpell.Name}  bypass={source.Bypass}");

        // Store the test-panel caster so OnCellClicked and SpellTestingPanel can find it. Set after BeginCast, which
        // closes a test-panel cast still open from an earlier F12 click and so clears the caster.
        _testPanelCaster = caster;
        Debug.Log($"[TestPanel]   test-panel caster stored: {caster.Stats?.CharacterName}");
        if (metamagic != null && metamagic.HasAnyMetamagic)
            Debug.Log($"[TestPanel]   Metamagic applied: {metamagic.GetSummary(spell.SpellLevel)}");

        // ── 4. (The metamagic clone is part of the request build above.) ──

        // ── 5. Kick off normal targeting pipeline ──
        Debug.Log($"[TestPanel]   Calling BeginPendingSpellTargeting(caster={caster.Stats?.CharacterName})");
        BeginPendingSpellTargeting(caster);

        Debug.Log($"[TestPanel]   After BeginPendingSpellTargeting: SubPhase={CurrentSubPhase}, AttackMode={_pendingAttackMode}");
    }

    /// <summary>
    /// Ends an open test-panel cast and forgets the panel's caster. The cast's own end does the same
    /// (GameManager.CloseCastContext), so this is only the targeting cancel's explicit reset; it no longer runs when a
    /// test-panel spell starts resolving, which used to drop its bypass for single-target casts (SPL-036).
    /// </summary>
    public void CleanupTestPanelCast()
    {
        if (_testPanelCastActive)
        {
            Debug.Log("[TestPanel] 🧹 CleanupTestPanelCast – ending the test-panel cast.");
            EndPendingCast();
        }
        _testPanelCaster = null;
    }
}

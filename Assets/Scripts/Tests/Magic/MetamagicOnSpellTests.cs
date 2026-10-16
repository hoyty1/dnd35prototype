using System.Collections.Generic;
using System.Linq;
using DND35e.Identifiers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 19 (SPL-038): the cast's spell clone carries its metamagic (SpellCaster.ApplyMetamagicToSpellData stamps
/// SpellData.MetamagicDataRef). The stamp must never change what the spell menu counts: only a prepared metamagic slot's
/// spell (SpellData.IsPreparedMetamagicSlotSpell, from SpellcastingComponent.GetUniqueAvailableSpells) counts against the
/// slots of its metamagic (SpellcastingComponent.CountAvailablePreparedSpell, CanCast, CanCastSpell); a clone stamped at the
/// cast counts as the plain spell, exactly as the unstamped clone did before this step. Also the globe's level: a
/// heightened clone counts at its heightened level (PHB p.95, p.236; SPL-141). Builds a wizard GameObject with its
/// components, so it runs in edit or Play mode.
/// </summary>
public static class MetamagicOnSpellTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        RaceDatabase.Init();
        ClassRegistry.Init();
        FeatDefinitions.Init();
        ItemDatabase.Init();
        SpellDatabase.Init();

        Debug.Log("====== METAMAGIC ON THE SPELL TESTS (batch 11 step 19, SPL-038) ======");

        TestMenuCountsIgnoreTheCastStamp();
        TestGlobeLevelIsTheHeightenedLevel();

        // Batch 11 step 21: slot selection (SPL-012) and the AI's prepared metamagic
        TestSlotSelection();
        TestAiCandidatesCarryPreparedMetamagic();
        TestHeightenedSlotsAreDistinct();
        TestThreatenedAiKeepsQuickenedCandidate();

        Debug.Log($"====== Results: {_passed} passed, {_failed} failed ======");
    }

    private static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            _passed++;
            Debug.Log($"  PASS: {testName}");
        }
        else
        {
            _failed++;
            Debug.LogError($"  [MetamagicOnSpell] FAIL: {testName} {detail}");
        }
    }

    private static MetamagicData Meta(params MetamagicFeatId[] feats)
    {
        var mm = new MetamagicData();
        foreach (MetamagicFeatId feat in feats)
            mm.AppliedMetamagic.Add(feat);
        return mm;
    }

    private static CharacterController CreateWizard(int level)
    {
        var go = new GameObject($"MetamagicOnSpellTest_Wizard{level}");
        var controller = go.AddComponent<CharacterController>();
        var stats = new CharacterStats(
            name: "Wizard " + level, level: level, characterClass: "Wizard",
            str: 10, dex: 14, con: 12, wis: 10, intelligence: 18, cha: 10,
            bab: level / 2, armorBonus: 0, shieldBonus: 0,
            damageDice: 4, damageCount: 1, bonusDamage: 0,
            baseSpeed: 6, atkRange: 1, baseHitDieHP: 4 * level, raceName: "Human");
        stats.Feats.Add("Silent Spell");
        controller.Stats = stats;
        var inventory = go.AddComponent<InventoryComponent>();
        inventory.Init(stats);
        SpellComponentKit.Give(stats, inventory.CharacterInventory);
        var spellComp = go.AddComponent<SpellcastingComponent>();
        spellComp.Init(stats);
        return controller;
    }

    private static int SlotIndex(SpellcastingComponent sc, int level, int skip)
    {
        int seen = 0;
        for (int i = 0; i < sc.SpellSlots.Count; i++)
        {
            SpellSlot slot = sc.SpellSlots[i];
            if (slot == null || slot.Level != level || slot.IsDomainSlot || slot.IsSpecialistSlot)
                continue;
            if (seen++ == skip)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// A wizard 5 prepares Magic Missile in two 1st-level slots and a Silent Magic Missile in a 2nd-level slot
    /// (PHB p.88, p.98). The template counts the plain slots; the menu's metamagic instance counts its one metamagic slot;
    /// a clone the cast stamps with Empower (or Maximize) counts the plain slots, as before step 19; the cast's copy of the
    /// menu instance (SpellCastRequest.CloneForCast) counts the metamagic slot.
    /// </summary>
    private static void TestMenuCountsIgnoreTheCastStamp()
    {
        CharacterController wizard = null;
        try
        {
            wizard = CreateWizard(5);
            SpellcastingComponent sc = wizard.Spellcasting;
            if (sc == null || sc.SpellSlots == null || sc.SpellSlots.Count == 0 || sc.IsSpontaneousCaster)
            {
                Assert(false, "Wizard 5 has prepared spell slots");
                return;
            }

            SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
            int a = SlotIndex(sc, 1, 0), b = SlotIndex(sc, 1, 1), c = SlotIndex(sc, 2, 0);
            bool prepared = a >= 0 && b >= 0 && c >= 0
                && sc.PrepareSpellInSlot(a, missile.Clone())
                && sc.PrepareSpellInSlot(b, missile.Clone())
                && sc.PrepareSpellInSlotWithMetamagic(c, missile.Clone(), Meta(MetamagicFeatId.SilentSpell));
            Assert(prepared, "Wizard 5 prepares Magic Missile twice and a Silent Magic Missile in a 2nd-level slot");
            if (!prepared)
                return;

            int plainSlots = sc.SpellSlots.Count(s => s != null && s.PreparedSpell != null && s.PreparedSpell.SpellId == SpellNames.MAGIC_MISSILE
                && !s.IsUsed && !s.DisabledByNegativeLevel && !s.HasMetamagic);
            int templateCount = sc.CountAvailablePreparedSpell(missile);
            Assert(templateCount == plainSlots && plainSlots >= 2, $"The template counts the {plainSlots} plain Magic Missile slots", $"got {templateCount}");

            SpellData menuEmpowered = sc.GetUniqueAvailableSpells(2).FirstOrDefault(s => s != null && s.SpellId == SpellNames.MAGIC_MISSILE && s.MetamagicDataRef != null);
            Assert(menuEmpowered != null && menuEmpowered.IsPreparedMetamagicSlotSpell, "The 2nd-level menu lists the Silent Magic Missile as a prepared-slot spell");
            if (menuEmpowered != null)
            {
                Assert(sc.CountAvailablePreparedSpell(menuEmpowered) == 1, "The menu's Silent Magic Missile counts its one metamagic slot",
                    $"got {sc.CountAvailablePreparedSpell(menuEmpowered)}");
                SpellData castCopy = SpellCastRequest.CloneForCast(menuEmpowered);
                Assert(sc.CountAvailablePreparedSpell(castCopy) == 1 && sc.CanCast(castCopy),
                    "The cast's copy of the menu instance (CloneForCast) still counts the metamagic slot");
            }

            foreach (MetamagicFeatId feat in new[] { MetamagicFeatId.EmpowerSpell, MetamagicFeatId.MaximizeSpell })
            {
                SpellData stamped = missile.Clone();
                SpellCaster.ApplyMetamagicToSpellData(stamped, Meta(feat));
                Assert(stamped.MetamagicDataRef != null && !stamped.IsPreparedMetamagicSlotSpell, $"A {feat} cast clone carries its stamp, not the prepared-slot flag");
                Assert(sc.CountAvailablePreparedSpell(stamped) == templateCount, $"A clone stamped with {feat} counts the plain slots, as before step 19",
                    $"got {sc.CountAvailablePreparedSpell(stamped)}, template {templateCount}");
                Assert(sc.CanCast(stamped) == sc.CanCast(missile) && sc.CanCastSpell(stamped) == sc.CanCastSpell(missile),
                    $"CanCast and CanCastSpell read a {feat}-stamped clone as the plain spell");
            }

            // The menu's counts after a plain cast: the stamp of a later cast changes nothing.
            Assert(sc.CastSpellFromSlot(missile.Clone()), "A plain Magic Missile spends a plain slot");
            Assert(sc.CountAvailablePreparedSpell(missile) == templateCount - 1, "One plain slot fewer");
            Assert(menuEmpowered == null || sc.CountAvailablePreparedSpell(menuEmpowered) == 1, "The Silent slot is untouched");
        }
        finally
        {
            if (wizard != null && wizard.gameObject != null)
                Object.DestroyImmediate(wizard.gameObject);
        }
    }

    /// <summary>PHB p.95, p.236: a globe compares the heightened level; without Heighten, the spell's level (SPL-141).</summary>
    private static void TestGlobeLevelIsTheHeightenedLevel()
    {
        SpellData plain = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
        Assert(LesserGlobeOfInvulnerabilityAreaEffect.EffectiveSpellLevel(plain) == 1, "Magic Missile meets a globe at 1st level");

        SpellData heightened = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
        MetamagicData heighten = Meta(MetamagicFeatId.HeightenSpell);
        heighten.HeightenToLevel = 4;
        SpellCaster.ApplyMetamagicToSpellData(heightened, heighten);
        Assert(LesserGlobeOfInvulnerabilityAreaEffect.EffectiveSpellLevel(heightened) == 4, "Magic Missile heightened to 4th meets a globe at 4th (PHB p.95)",
            $"got {LesserGlobeOfInvulnerabilityAreaEffect.EffectiveSpellLevel(heightened)}");

        SpellData fireball = SpellDatabase.GetSpell(SpellNames.FIREBALL).Clone();
        MetamagicData heighten5 = Meta(MetamagicFeatId.HeightenSpell);
        heighten5.HeightenToLevel = 5;
        SpellCaster.ApplyMetamagicToSpellData(fireball, heighten5);
        Assert(LesserGlobeOfInvulnerabilityAreaEffect.EffectiveSpellLevel(fireball) == 5, "Fireball heightened to 5th is above the globe's 4th level");

        SpellData empowered = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE).Clone();
        SpellCaster.ApplyMetamagicToSpellData(empowered, Meta(MetamagicFeatId.EmpowerSpell));
        Assert(LesserGlobeOfInvulnerabilityAreaEffect.EffectiveSpellLevel(empowered) == 1,
            "Empower uses a higher slot but leaves the spell's level (PHB p.93): the globe still sees 1st");
    }

    /// <summary>
    /// Batch 11 step 21, SPL-012: a plain cast spends only a slot that can be cast and holds the plain spell (SpellSlot.CanCast:
    /// not locked by Imbue with Spell Ability, PHB p.243, not lost to a negative level, DMG p.293; a slot prepared with
    /// metamagic holds a different, higher-level spell, PHB p.88); a metamagic cast spends only the slot prepared with that
    /// spell and that metamagic, never a plain slot or another spell's slot of the effective level.
    /// </summary>
    private static void TestSlotSelection()
    {
        CharacterController wizard = null;
        try
        {
            wizard = CreateWizard(5);
            SpellcastingComponent sc = wizard.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
            {
                Assert(false, "Wizard 5 has prepared spell slots");
                return;
            }
            SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
            SpellData shield = SpellDatabase.GetSpell(SpellNames.SHIELD);
            sc.SpellSlots.Clear();
            var locked = new SpellSlot(1, missile.Clone(), false, "Wizard") { LockedByImbue = true };
            var drained = new SpellSlot(1, missile.Clone(), false, "Wizard") { DisabledByNegativeLevel = true };
            var silentSlot = new SpellSlot(2, false, "Wizard");
            silentSlot.PrepareWithMetamagic(missile.Clone(), Meta(MetamagicFeatId.SilentSpell));
            var otherSecond = new SpellSlot(2, shield.Clone(), false, "Wizard");
            var plain = new SpellSlot(1, missile.Clone(), false, "Wizard");
            sc.SpellSlots.AddRange(new[] { locked, drained, silentSlot, otherSecond, plain });

            Assert(sc.CountAvailablePreparedSpell(missile) == 1, "SPL-012: the menu counts only the castable plain Magic Missile",
                $"got {sc.CountAvailablePreparedSpell(missile)}");
            Assert(sc.CastSpellFromSlot(missile.Clone()) && plain.IsUsed && !locked.IsUsed && !drained.IsUsed && !silentSlot.IsUsed,
                "SPL-012: a plain Magic Missile spends the plain slot, not the locked, drained or Silent one");
            Assert(!sc.CastSpellFromSlot(missile.Clone()) && !locked.IsUsed && !drained.IsUsed && !silentSlot.IsUsed,
                "SPL-012: with the plain slot spent a plain Magic Missile is refused; the locked, drained and Silent slots stay");

            Assert(!sc.CastWizardSpellWithMetamagic(missile.Clone(), Meta(MetamagicFeatId.StillSpell)) && !otherSecond.IsUsed && !silentSlot.IsUsed,
                "SPL-012: a Still Magic Missile has no slot prepared with it: refused, and no other 2nd-level slot is spent");
            Assert(sc.CanCastWithMetamagic(missile, Meta(MetamagicFeatId.SilentSpell)) && !sc.CanCastWithMetamagic(missile, Meta(MetamagicFeatId.StillSpell)),
                "CanCastWithMetamagic: the Silent slot is there, no Still one");
            Assert(sc.CastWizardSpellWithMetamagic(missile.Clone(), Meta(MetamagicFeatId.SilentSpell)) && silentSlot.IsUsed && !otherSecond.IsUsed,
                "A Silent Magic Missile spends the slot prepared with it (PHB p.88)");
            Assert(!sc.CastWizardSpellWithMetamagic(missile.Clone(), Meta(MetamagicFeatId.SilentSpell)) && !otherSecond.IsUsed,
                "SPL-012: with the Silent slot spent no other 2nd-level slot is spent for it (no last resort)");

            silentSlot.IsUsed = false;
            silentSlot.LockedByImbue = true;
            Assert(!sc.CastWizardSpellWithMetamagic(missile.Clone(), Meta(MetamagicFeatId.SilentSpell)) && !silentSlot.IsUsed,
                "SPL-012: a metamagic slot locked by Imbue with Spell Ability is not spent");
        }
        finally
        {
            if (wizard != null && wizard.gameObject != null)
                Object.DestroyImmediate(wizard.gameObject);
        }
    }

    /// <summary>
    /// Batch 11 step 21: the AI's candidates (GetCastablePreparedSpells) list a prepared metamagic slot's spell with its
    /// metamagic, as the spell menu does, and the NPC request carries it (SpellCastRequest.ForAI: the clone with the
    /// metamagic's changes, the metamagic in the request); a plain candidate carries none, a Scripted step's metamagic wins.
    /// </summary>
    private static void TestAiCandidatesCarryPreparedMetamagic()
    {
        CharacterController wizard = null;
        try
        {
            wizard = CreateWizard(5);
            SpellcastingComponent sc = wizard.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
            {
                Assert(false, "Wizard 5 has prepared spell slots");
                return;
            }
            SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
            sc.SpellSlots.Clear();
            var plain = new SpellSlot(1, missile.Clone(), false, "Wizard");
            var silentSlot = new SpellSlot(2, false, "Wizard");
            silentSlot.PrepareWithMetamagic(missile.Clone(), Meta(MetamagicFeatId.SilentSpell));
            sc.SpellSlots.AddRange(new[] { plain, silentSlot });

            List<SpellData> castable = sc.GetCastablePreparedSpells().Where(s => s != null && s.SpellId == SpellNames.MAGIC_MISSILE).ToList();
            SpellData plainCandidate = castable.FirstOrDefault(s => !s.IsPreparedMetamagicSlotSpell);
            SpellData silentCandidate = castable.FirstOrDefault(s => s.IsPreparedMetamagicSlotSpell);
            Assert(castable.Count == 2 && plainCandidate != null && silentCandidate != null
                   && silentCandidate.MetamagicDataRef != null && silentCandidate.MetamagicDataRef.Has(MetamagicFeatId.SilentSpell),
                "The AI's candidates list the plain and the Silent Magic Missile, as the spell menu does", $"got {castable.Count}");
            if (plainCandidate == null || silentCandidate == null)
                return;

            Assert(SpellCastRequest.AiMetamagicFor(plainCandidate) == null && SpellCastRequest.AiMetamagicFor(silentCandidate) != null,
                "AiMetamagicFor: the prepared metamagic slot's spell carries its metamagic, the plain one none");
            SpellCastRequest silentReq = SpellCastRequest.ForAI(wizard, wizard, silentCandidate);
            Assert(silentReq.Metamagic != null && silentReq.Metamagic.Has(MetamagicFeatId.SilentSpell) && !silentReq.Spell.HasVerbalComponent
                   && !silentReq.Spell.IsPreparedMetamagicSlotSpell,
                "ForAI: the Silent candidate's request carries Silent Spell and its clone has no verbal component (PHB p.100)");
            SpellCastRequest plainReq = SpellCastRequest.ForAI(wizard, wizard, plainCandidate);
            Assert(plainReq.Metamagic == null && plainReq.Spell.HasVerbalComponent, "ForAI: the plain candidate's request has no metamagic");
            MetamagicData scripted = Meta(MetamagicFeatId.StillSpell);
            SpellCastRequest scriptedReq = SpellCastRequest.ForAI(wizard, wizard, plainCandidate, scripted);
            Assert(scriptedReq.Metamagic != null && scriptedReq.Metamagic.Has(MetamagicFeatId.StillSpell) && !scriptedReq.Spell.HasSomaticComponent,
                "ForAI: a Scripted step's metamagic goes with the cast");
        }
        finally
        {
            if (wizard != null && wizard.gameObject != null)
                Object.DestroyImmediate(wizard.gameObject);
        }
    }

    /// <summary>
    /// Batch 11 step 21 review: a Magic Missile heightened to 3rd (in a 3rd-level slot) and one heightened to 4th (in a
    /// 4th-level slot) are two entries for the menu and the AI, each counting and spending only its own slot
    /// (MetamagicData.GetSlotKey: the feats and a Heighten's target level; PHB p.95).
    /// </summary>
    private static void TestHeightenedSlotsAreDistinct()
    {
        CharacterController wizard = null;
        try
        {
            wizard = CreateWizard(7);
            SpellcastingComponent sc = wizard.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
            {
                Assert(false, "Wizard 7 has prepared spell slots");
                return;
            }
            SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
            MetamagicData to3 = Meta(MetamagicFeatId.HeightenSpell);
            to3.HeightenToLevel = 3;
            MetamagicData to4 = Meta(MetamagicFeatId.HeightenSpell);
            to4.HeightenToLevel = 4;
            sc.SpellSlots.Clear();
            var third = new SpellSlot(3, false, "Wizard");
            third.PrepareWithMetamagic(missile.Clone(), to3);
            var fourth = new SpellSlot(4, false, "Wizard");
            fourth.PrepareWithMetamagic(missile.Clone(), to4);
            sc.SpellSlots.AddRange(new[] { third, fourth });

            List<SpellData> castable = sc.GetCastablePreparedSpells().Where(s => s != null && s.SpellId == SpellNames.MAGIC_MISSILE).ToList();
            Assert(castable.Count == 2 && castable.Any(s => s.MetamagicDataRef.HeightenToLevel == 3) && castable.Any(s => s.MetamagicDataRef.HeightenToLevel == 4),
                "The AI's candidates list Magic Missile heightened to 3rd and to 4th as two entries", $"got {castable.Count}");
            SpellData menu3 = sc.GetUniqueAvailableSpells(3).FirstOrDefault(s => s != null && s.SpellId == SpellNames.MAGIC_MISSILE);
            SpellData menu4 = sc.GetUniqueAvailableSpells(4).FirstOrDefault(s => s != null && s.SpellId == SpellNames.MAGIC_MISSILE);
            Assert(menu3 != null && menu4 != null && sc.CountAvailablePreparedSpell(menu3) == 1 && sc.CountAvailablePreparedSpell(menu4) == 1,
                "Each heightened menu entry counts only its own slot",
                $"got {(menu3 != null ? sc.CountAvailablePreparedSpell(menu3) : -1)} and {(menu4 != null ? sc.CountAvailablePreparedSpell(menu4) : -1)}");
            Assert(sc.CastWizardSpellWithMetamagic(missile.Clone(), to4) && fourth.IsUsed && !third.IsUsed,
                "A Magic Missile heightened to 4th spends the 4th-level slot only");
            Assert(menu3 == null || sc.CountAvailablePreparedSpell(menu3) == 1, "The 3rd-level heightened slot still counts 1");
        }
        finally
        {
            if (wizard != null && wizard.gameObject != null)
                Object.DestroyImmediate(wizard.gameObject);
        }
    }

    /// <summary>
    /// Batch 11 step 21 review: a quickened cast provokes no attack of opportunity (PHB p.88, p.98), so a threatened AI
    /// wizard keeps its prepared Quickened candidate (AISpellcastingStrategist.EvaluateDefensiveCasting gives 2, cast
    /// normally), while its plain candidate is weighed for defensive casting.
    /// </summary>
    private static void TestThreatenedAiKeepsQuickenedCandidate()
    {
        CharacterController wizard = null;
        GameObject enemyGo = null;
        try
        {
            wizard = CreateWizard(9);
            SpellcastingComponent sc = wizard.Spellcasting;
            if (sc == null || sc.SpellSlots == null)
            {
                Assert(false, "Wizard 9 has prepared spell slots");
                return;
            }
            wizard.SetTeam(CharacterTeam.Enemy);
            wizard.GridPosition = new Vector2Int(4, 4);
            enemyGo = new GameObject("MetamagicOnSpellTest_Fighter");
            CharacterController fighter = enemyGo.AddComponent<CharacterController>();
            fighter.Stats = new CharacterStats(
                name: "Fighter", level: 1, characterClass: "Fighter",
                str: 14, dex: 10, con: 12, wis: 10, intelligence: 10, cha: 10,
                bab: 1, armorBonus: 0, shieldBonus: 0,
                damageDice: 8, damageCount: 1, bonusDamage: 0,
                baseSpeed: 6, atkRange: 1, baseHitDieHP: 10, raceName: "Human");
            fighter.SetTeam(CharacterTeam.Player);
            fighter.GridPosition = new Vector2Int(5, 4);

            SpellData missile = SpellDatabase.GetSpell(SpellNames.MAGIC_MISSILE);
            sc.SpellSlots.Clear();
            var plain = new SpellSlot(1, missile.Clone(), false, "Wizard");
            var quickSlot = new SpellSlot(5, false, "Wizard");
            quickSlot.PrepareWithMetamagic(missile.Clone(), Meta(MetamagicFeatId.QuickenSpell));
            sc.SpellSlots.AddRange(new[] { plain, quickSlot });

            List<SpellData> castable = sc.GetCastablePreparedSpells().Where(s => s != null && s.SpellId == SpellNames.MAGIC_MISSILE).ToList();
            SpellData plainCandidate = castable.FirstOrDefault(s => !s.IsPreparedMetamagicSlotSpell);
            SpellData quickCandidate = castable.FirstOrDefault(s => s.IsPreparedMetamagicSlotSpell);
            var combatants = new List<CharacterController> { wizard, fighter };
            Assert(plainCandidate != null && AISpellcastingStrategist.EvaluateDefensiveCasting(wizard, plainCandidate, combatants) != 2,
                "A threatened AI wizard's plain Magic Missile is weighed for defensive casting (it provokes, PHB p.140)");
            Assert(quickCandidate != null && AISpellcastingStrategist.EvaluateDefensiveCasting(wizard, quickCandidate, combatants) == 2,
                "A threatened AI wizard keeps its prepared Quickened Magic Missile: it provokes no attack of opportunity (PHB p.98)");
        }
        finally
        {
            if (wizard != null && wizard.gameObject != null)
                Object.DestroyImmediate(wizard.gameObject);
            if (enemyGo != null)
                Object.DestroyImmediate(enemyGo);
        }
    }
}
}

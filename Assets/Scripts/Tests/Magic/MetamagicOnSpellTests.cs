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
}
}

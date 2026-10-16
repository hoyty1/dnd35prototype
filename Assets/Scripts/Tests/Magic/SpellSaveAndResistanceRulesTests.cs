using System;
using System.Collections.Generic;
using DND35e.Identifiers;
using Tests.Utilities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Magic
{
/// <summary>
/// Batch 11 step 18 (docs/designs/cast_core_batch11.md, raw-saves-and-sr): the voluntary save, the one spell resistance
/// check and the spell-and-caster save bonuses, for every caster and target alike (PC_NPC_PARITY).
/// <list type="bullet">
/// <item>PHB p.177 (voluntarily giving up a save; SPL-149): SpellSaveRules.IsHarmlessOrBeneficialTo, which both deciders
/// answer with, gives up a save only against a spell of the creature's own side that is not Damage, Debuff, Control or
/// Wall (the runtime EffectType after SpellCategoryClassifier.ReclassifyAll; Healing only on a living creature; Remove
/// Curse and Invisibility are "(harmless)"), never against a foe's spell; a charmed or confused creature keeps only the
/// Buff case. A forgone save rolls no die and fails.</item>
/// <item>PHB p.177 and p.100 (spell resistance, Spell Penetration; SPL-122): one d20 + caster level + Spell Penetration
/// against SR, under the caller's dice label, skipped with no die when the target has no SR; SpellSaveRules.RollSpellResistance
/// uses it (the old SpellResolutionService wrapper went with SPL-056, batch 11 step 29).</item>
/// <item>SPL-018: SaveRules gives the bonuses that depend on the spell and its caster to any save whose context carries
/// them (SaveContext.ForSpellCast): the protection ward (PHB p.266), Hideous Laughter's +4 (PHB p.292), Lullaby's -2
/// against sleep effects (PHB p.249) and Charm Person's and Charm Monster's +5 when attacked (PHB p.209); SpellSaveRules.RollSave uses the
/// same modifier.</item>
/// </list>
/// Builds characters with TestHelpers.CreateCharacter and forces dice through ScenarioHooks.RollFilter (restored after);
/// Play mode only (the Charm Person check reads the live combatants).
/// </summary>
public static class SpellSaveAndResistanceRulesTests
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;
        Debug.Log("====== SPELL SAVE AND RESISTANCE RULES TESTS (batch 11 step 18) ======");

        SpellDatabase.Init();
        var objects = new List<GameObject>();
        Func<int, string, int, int> oldFilter = ScenarioHooks.RollFilter;
        try
        {
            CharacterController Make(string name, string cls, CharacterTeam team, Vector2Int pos)
            {
                CharacterController c = TestHelpers.CreateCharacter(name, cls, level: 5, gridPosition: pos);
                objects.Add(c.gameObject);
                c.SetTeam(team);
                StatusEffectManager sem = c.StatusEffectManager != null ? c.StatusEffectManager : c.gameObject.AddComponent<StatusEffectManager>();
                sem.Init(c.Stats);
                return c;
            }

            CharacterController caster = Make("SSRCaster", "Wizard", CharacterTeam.Player, new Vector2Int(80, 80));
            CharacterController ally = Make("SSRAlly", "Fighter", CharacterTeam.Player, new Vector2Int(82, 80));
            CharacterController foe = Make("SSRFoe", "Fighter", CharacterTeam.Enemy, new Vector2Int(90, 90));

            TestVoluntarySave(caster, ally, foe);
            TestHarmlessSpellAudit(caster, ally);
            TestSpellResistance(caster, foe);
            TestSpellAndCasterBonuses(caster, ally, foe);
        }
        catch (Exception ex)
        {
            Assert(false, "The suite ran without exceptions", ex.ToString());
        }
        finally
        {
            ScenarioHooks.RollFilter = oldFilter;
            foreach (GameObject go in objects)
                if (go != null)
                    Object.DestroyImmediate(go);
        }

        Debug.Log($"====== Spell Save and Resistance Rules Results: {_passed} passed, {_failed} failed ======");
    }

    private static SpellData Spell(string id) => SpellDatabase.GetSpell(id).Clone();

    /// <summary>Forces every d20 to <paramref name="value"/> and counts them (with their labels) while it is set.</summary>
    private static void ForceD20(int value, List<string> labels)
    {
        ScenarioHooks.RollFilter = (sides, ctx, natural) =>
        {
            if (sides != 20)
                return natural;
            labels?.Add(ctx ?? "(none)");
            return value;
        };
    }

    private static void TestVoluntarySave(CharacterController caster, CharacterController ally, CharacterController foe)
    {
        SpellData bulls = Spell(SpellNames.BULLS_STRENGTH);
        SpellData cure = Spell(SpellNames.CURE_LIGHT_WOUNDS);
        SpellData hands = Spell(SpellNames.BURNING_HANDS);
        SpellData hold = Spell(SpellNames.HOLD_PERSON);
        SpellData daze = Spell(SpellNames.DAZE);

        // The decision reads the runtime EffectType (after ReclassifyAll): pin the types these checks rely on.
        Assert(bulls.EffectType == SpellEffectType.Buff, "Bull's Strength is a Buff at runtime", "" + bulls.EffectType);
        Assert(cure.EffectType == SpellEffectType.Healing, "Cure Light Wounds is a Healing spell at runtime", "" + cure.EffectType);
        Assert(hands.EffectType == SpellEffectType.Damage, "Burning Hands is a Damage spell at runtime", "" + hands.EffectType);
        Assert(hold.EffectType == SpellEffectType.Control || hold.EffectType == SpellEffectType.Debuff,
            "Hold Person is a Control or Debuff spell at runtime", "" + hold.EffectType);

        Assert(SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, bulls), "An ally gives up its save against its side's Buff (PHB p.177)");
        Assert(SpellSaveRules.IsHarmlessOrBeneficialTo(caster, caster, bulls), "The caster gives up its save against its own Buff");
        Assert(SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, cure), "A living ally gives up its save against its side's healing");
        Assert(!SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, hands), "An ally rolls its save against its side's Damage spell (SPL-149)");
        Assert(!SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, hold), "An ally rolls its save against its side's Control or Debuff spell");
        Assert(!SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, daze), "An ally rolls its save against its side's Daze");
        SpellData removeCurse = Spell(SpellNames.REMOVE_CURSE);
        SpellData invisibility = Spell(SpellNames.INVISIBILITY);
        SpellData wallOfFire = Spell(SpellNames.WALL_OF_FIRE);
        Assert(removeCurse.EffectType == SpellEffectType.Dispel && invisibility.EffectType == SpellEffectType.Illusion
               && wallOfFire.EffectType == SpellEffectType.Wall,
            "Remove Curse is a Dispel, Invisibility an Illusion and Wall of Fire a Wall spell at runtime",
            removeCurse.EffectType + " " + invisibility.EffectType + " " + wallOfFire.EffectType);
        Assert(SpellSaveRules.IsHarmlessOrBeneficialTo(caster, caster, removeCurse),
            "The caster gives up its save against its own Remove Curse (Will negates (harmless), PHB p.271)");
        Assert(SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, invisibility),
            "An ally gives up its save against its side's Invisibility (Will negates (harmless), PHB p.245)");
        Assert(!SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, wallOfFire), "An ally rolls its save against its side's Wall of Fire");
        Assert(!SpellSaveRules.IsHarmlessOrBeneficialTo(foe, caster, bulls), "A foe rolls its save even against a Buff (it does not trust the spell)");
        Assert(!SpellSaveRules.IsHarmlessOrBeneficialTo(foe, caster, cure), "A foe rolls its save against a healing spell");

        string type = ally.Stats.CreatureType;
        ally.Stats.CreatureType = "Undead";
        Assert(!SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, cure), "An undead ally rolls its save against healing (it harms the undead, PHB p.215)");
        Assert(SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, bulls), "An undead ally still gives up its save against a Buff");
        ally.Stats.CreatureType = type;

        ally.ApplyCondition(CombatConditionType.Charmed, 5, "Tester");
        bool charmed = ally.HasCondition(CombatConditionType.Charmed);
        Assert(!charmed || SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, bulls), "A charmed ally still gives up its save against a Buff");
        Assert(charmed && !SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, cure) && !SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, invisibility),
            "A charmed ally rolls its save against healing and Invisibility (today's Buff-only rule)", "charmed " + charmed);
        ally.RemoveCondition(CombatConditionType.Charmed);

        Assert(UiCastDecider.Instance.ForgoSave(ally, caster, bulls) && AiCastDecider.Instance.ForgoSave(ally, caster, bulls)
               && !UiCastDecider.Instance.ForgoSave(ally, caster, hands) && !AiCastDecider.Instance.ForgoSave(ally, caster, hands),
            "Both deciders answer the same (PC and NPC follow identical rules)");
        bool controllable = ally.IsControllable;
        ally.IsControllable = true;
        bool uiDecider = CastDeciders.For(ally) == UiCastDecider.Instance;
        ally.IsControllable = false;
        bool aiDecider = CastDeciders.For(ally) == AiCastDecider.Instance;
        ally.IsControllable = controllable;
        Assert(uiDecider && aiDecider, "A creature's own decider answers: the PC decider for a controllable creature, the AI decider otherwise");

        var labels = new List<string>();
        ForceD20(20, labels);
        SaveResult forgone = SpellSaveRules.RollSave(caster, ally, bulls, SaveType.Will, 15, "test save");
        Assert(forgone.Forgone && !forgone.Saved && labels.Count == 0,
            "A forgone save rolls no die and fails, even when the die would have been a 20", "dice " + labels.Count);
        SaveResult rolled = SpellSaveRules.RollSave(caster, ally, hands, SaveType.Reflex, 15, "test save");
        Assert(!rolled.Forgone && rolled.Roll == 20 && rolled.Saved && labels.Count == 1 && labels[0] == "test save",
            "An ally's save against a Damage spell is one d20 under the caller's label", rolled.Roll + " " + string.Join(",", labels));
        ScenarioHooks.RollFilter = null;
    }

    /// <summary>
    /// The spells, among those that allow a save, whose save an own-side target gives up under
    /// SpellSaveRules.IsHarmlessOrBeneficialTo (any runtime EffectType but Damage, Debuff, Control and Wall). Each is one
    /// a creature of the caster's side accepts: the save line is marked harmless or the subject is a willing recipient
    /// (the stat and defence buffs, Haste, Heroism, Stoneskin, the magic circles, Blur, Displacement, Invisibility,
    /// Remove Curse and the weapon buffs); the size spells, whose save line is a plain Fortitude negates but whose
    /// subject wants the change; Sanctuary, whose save belongs to whoever attacks the warded creature; the figments and
    /// sound illusions the caster's side knows to be false (PHB p.173: proof that an illusion is not real needs no
    /// save); and Detect Thoughts, which does an ally no harm. A new spell that would join them must be added here
    /// after its PHB save line is checked, so a harmful spell left at Utility, Illusion or Buff cannot let allies skip
    /// their saves unnoticed.
    /// </summary>
    private static readonly HashSet<string> AcceptedFromOwnSide = new HashSet<string>(StringComparer.Ordinal)
    {
        "align_weapon", "bears_endurance", "blur", "bulls_strength", "cats_grace", "eagles_splendor", "foxs_cunning",
        "owls_wisdom", "greater_magic_weapon", "keen_edge", "heroism", "haste", "imbue_with_spell_ability", "levitate",
        "magic_circle_against_evil", "magic_circle_against_good", "magic_circle_against_law", "magic_circle_against_chaos",
        "protection_from_energy", "remove_curse", "stoneskin", "displacement", "invisibility", "invisibility_sphere",
        "enlarge_person", "reduce_person", "mass_enlarge_person", "mass_reduce_person",
        "sanctuary",
        "ghost_sound", "silent_image", "minor_image", "persistent_image", "ventriloquism",
        "detect_thoughts",
    };

    private static void TestHarmlessSpellAudit(CharacterController caster, CharacterController ally)
    {
        var unexpected = new List<string>();
        int accepted = 0;
        foreach (SpellData spell in SpellDatabase.GetAllSpells())
        {
            if (spell == null || !spell.AllowsSavingThrow || string.IsNullOrEmpty(spell.SpellId))
                continue;
            if (!SpellSaveRules.IsHarmlessOrBeneficialTo(ally, caster, spell))
                continue;
            accepted++;
            if (!AcceptedFromOwnSide.Contains(spell.SpellId))
                unexpected.Add(spell.SpellId + " (" + spell.EffectType + ")");
        }
        Assert(unexpected.Count == 0,
            "Audit: every spell with a save that an ally gives up is on the checked list (harmless or wanted by its subject)",
            string.Join(", ", unexpected));
        Assert(accepted > 0, "Audit: the database has spells an ally accepts", "" + accepted);
    }

    private static void TestSpellResistance(CharacterController caster, CharacterController foe)
    {
        var labels = new List<string>();
        ForceD20(10, labels);
        SRResult met = SpellSaveResolver.RollSpellResistance(5, 2, 17, "test SR");
        SRResult missed = SpellSaveResolver.RollSpellResistance(5, 2, 18, "test SR");
        Assert(met.Overcame && met.Total == 17 && met.PenetrationBonus == 2 && !missed.Overcame && missed.Total == 17,
            "SR: d20 10 + CL 5 + 2 = 17 meets SR 17 and not SR 18 (PHB p.177, p.100)", met.Total + "/" + missed.Total);
        Assert(labels.Count == 2 && labels.TrueForAll(l => l == "test SR"), "SR: one d20 per check, under the caller's label", string.Join(",", labels));

        labels.Clear();
        SRResult none = SpellSaveResolver.RollSpellResistance(5, 2, 0, "test SR");
        Assert(none.Skipped && none.Overcame && labels.Count == 0, "SR: a target with no SR is skipped with no die");

        // Spell Penetration through the cast rule: the caster's own caster level plus the feat.
        labels.Clear();
        int oldSr = foe.Stats.SpellResistance;
        bool hadFeat = caster.Stats.HasFeat("Spell Penetration");
        if (!hadFeat)
            caster.Stats.Feats.Add("Spell Penetration");
        SpellData sleep = Spell(SpellNames.SLEEP);
        int cl = CasterLevelRules.ForCaster(caster, sleep);
        foe.Stats.SpellResistance = 10 + cl + 2;
        SRResult withFeat = SpellSaveRules.RollSpellResistance(caster, foe, sleep, "test SR");
        foe.Stats.SpellResistance = 10 + cl + 3;
        SRResult short1 = SpellSaveRules.RollSpellResistance(caster, foe, sleep, "test SR");
        if (!hadFeat)
            caster.Stats.Feats.Remove("Spell Penetration");
        SRResult withoutFeat = SpellSaveRules.RollSpellResistance(caster, foe, sleep, "test SR");
        foe.Stats.SpellResistance = oldSr;
        Assert(withFeat.Overcame && withFeat.CasterLevel == cl && withFeat.PenetrationBonus == 2 && !short1.Overcame,
            "SpellSaveRules: d20 + the cast's caster level + Spell Penetration (+2) meets SR exactly, one more is not met",
            withFeat.Total + " vs " + (10 + cl + 2));
        Assert(hadFeat || (withoutFeat.PenetrationBonus == 0 && withoutFeat.Total == 10 + cl),
            "SpellSaveRules: without the feat the check is d20 + caster level", "" + withoutFeat.Total);
        ScenarioHooks.RollFilter = null;
    }

    private static void TestSpellAndCasterBonuses(CharacterController caster, CharacterController ally, CharacterController foe)
    {
        SpellData fireball = Spell(SpellNames.FIREBALL);
        SpellData laughter = Spell(SpellNames.HIDEOUS_LAUGHTER);
        SpellData sleep = Spell(SpellNames.SLEEP);
        SpellData daze = Spell(SpellNames.DAZE);
        CharacterStats f = foe.Stats;

        int Plain(SavingThrowType save, SpellData spell) => SaveRules.Modifier(f, save, SaveContext.ForSpell(spell));
        int Cast(SavingThrowType save, SpellData spell) => SaveRules.Modifier(f, save, SaveContext.ForSpellCast(spell, caster));

        // The protection ward: +2 resistance against an evil creature's effect (PHB p.266).
        Alignment oldAlignment = caster.Stats.CharacterAlignment;
        ActiveSpellEffect ward = foe.StatusEffectManager.AddEffect(Spell(SpellNames.PROTECTION_FROM_EVIL), "Tester", 5, 10);
        caster.Stats.CharacterAlignment = Alignment.ChaoticEvil;
        int fireballWard = Cast(SavingThrowType.Reflex, fireball) - Plain(SavingThrowType.Reflex, fireball);
        int effectWard = SaveRules.Modifier(f, SavingThrowType.Fortitude, SaveContext.ForSpell(null, caster.Stats))
                         - SaveRules.Modifier(f, SavingThrowType.Fortitude, SaveContext.ForSpell(null));
        int noneWard = SaveRules.Modifier(f, SavingThrowType.Reflex, SaveContext.None) - f.GetSaveTotal(SavingThrowType.Reflex);
        caster.Stats.CharacterAlignment = Alignment.TrueNeutral;
        int neutralWard = Cast(SavingThrowType.Reflex, fireball) - Plain(SavingThrowType.Reflex, fireball);
        caster.Stats.CharacterAlignment = oldAlignment;
        foe.StatusEffectManager.RemoveEffect(ward);
        AssertEq(fireballWard, 2, "Protection from Evil: +2 on a Reflex save against an evil caster's Fireball (PHB p.266; SPL-018)");
        AssertEq(effectWard, 2, "Protection from Evil: +2 on any save against an evil caster's effect");
        AssertEq(noneWard, 0, "Protection from Evil: nothing for a save the site knows nothing about (SaveContext.None)");
        AssertEq(neutralWard, 0, "Protection from Evil: nothing against a neutral caster");

        // Hideous Laughter: +4 for a creature of another type (PHB p.292).
        string type = f.CreatureType;
        AssertEq(Cast(SavingThrowType.Will, laughter) - Plain(SavingThrowType.Will, laughter), 0,
            "Hideous Laughter: no bonus for a creature of the caster's type");
        f.CreatureType = "Monstrous Humanoid";
        AssertEq(Cast(SavingThrowType.Will, laughter) - Plain(SavingThrowType.Will, laughter), 4,
            "Hideous Laughter: +4 for a creature of another type (PHB p.292; SPL-018)");
        f.CreatureType = type;

        // Lullaby: -2 on Will saves against sleep effects (PHB p.249); the caster does not matter.
        ActiveSpellEffect lullaby = foe.StatusEffectManager.AddEffect(Spell(SpellNames.LULLABY), "Tester", 5, 10);
        int sleepWill = Plain(SavingThrowType.Will, sleep) - f.GetSaveTotal(SavingThrowType.Will);
        int dazeWill = Cast(SavingThrowType.Will, daze) - SaveRules.Modifier(f, SavingThrowType.Will, SaveContext.ForSpell(daze));
        int sleepReflex = SaveRules.Modifier(f, SavingThrowType.Reflex, SaveContext.ForSpell(sleep))
                          - SaveRules.Modifier(f, SavingThrowType.Reflex, SaveContext.ForSpell(null));
        foe.StatusEffectManager.RemoveEffect(lullaby);
        int sleepWillAfter = Plain(SavingThrowType.Will, sleep) - f.GetSaveTotal(SavingThrowType.Will);
        AssertEq(sleepWill - sleepWillAfter, -2, "Lullaby: -2 on the Will save against Sleep (PHB p.249; SPL-018)");
        AssertEq(dazeWill, 0, "Lullaby: nothing against a spell that is no sleep effect (the caster adds nothing either)");
        AssertEq(sleepReflex, 0, "Lullaby: nothing on a Reflex save");

        // Charm Person: +5 when the caster's side is attacking the target (PHB p.209): a foe already hurt.
        SpellData charm = Spell(SpellNames.CHARM_PERSON);
        int hp = f.CurrentHP;
        int calm = Cast(SavingThrowType.Will, charm) - Plain(SavingThrowType.Will, charm);
        f.CurrentHP = Mathf.Max(1, f.TotalMaxHP - 3);
        bool attacked = SpellCaster.IsBeingThreatenedBy(foe, caster);
        int hurt = Cast(SavingThrowType.Will, charm) - Plain(SavingThrowType.Will, charm);
        int hurtReflex = Cast(SavingThrowType.Reflex, charm) - Plain(SavingThrowType.Reflex, charm);
        SpellData charmMonster = Spell(SpellNames.CHARM_MONSTER);
        int hurtMonster = Cast(SavingThrowType.Will, charmMonster) - Plain(SavingThrowType.Will, charmMonster);
        f.CurrentHP = hp;
        Assert(GameManager.Instance == null || (attacked && hurtMonster == 5),
            "Charm Monster: the same +5 on the Will save of a foe the caster's side has attacked (PHB p.209: it works as Charm Person)",
            "attacked " + attacked + " bonus " + hurtMonster);
        Assert(!attacked || calm == 0, "Charm Person: no bonus for a foe the caster's side is not attacking", "" + calm);
        Assert(GameManager.Instance == null || (attacked && hurt == 5 && hurtReflex == 0),
            "Charm Person: +5 on the Will save of a foe the caster's side has attacked (PHB p.209; SPL-018)",
            "attacked " + attacked + " bonus " + hurt + " reflex " + hurtReflex);

        // The cast's save uses the same modifier (SpellSaveRules.RollSave, CastContext.RollSave).
        ActiveSpellEffect ward2 = foe.StatusEffectManager.AddEffect(Spell(SpellNames.PROTECTION_FROM_EVIL), "Tester", 5, 10);
        caster.Stats.CharacterAlignment = Alignment.ChaoticEvil;
        ForceD20(10, null);
        SaveResult save = SpellSaveRules.RollSave(caster, foe, fireball, SaveType.Reflex, 30);
        int want = Cast(SavingThrowType.Reflex, fireball);
        ScenarioHooks.RollFilter = null;
        caster.Stats.CharacterAlignment = oldAlignment;
        foe.StatusEffectManager.RemoveEffect(ward2);
        Assert(save.Roll == 10 && save.Modifier == want && save.Total == 10 + want,
            "SpellSaveRules.RollSave: d20 + SaveRules.Modifier for the spell and its caster (the ward included)",
            save.Modifier + " vs " + want);
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
            Debug.LogError($"  FAIL: {testName} {detail}");
        }
    }

    private static void AssertEq(int got, int want, string testName) => Assert(got == want, testName, $"got {got}, expected {want}");
}
}

using System.Collections.Generic;
using DND35e.Identifiers;

/// <summary>
/// The AI's answers to a cast's caster choices (batch 11 step 22, cast_core_batch11.md; SPL-112, SPL-113), read by
/// <see cref="AiCastDecider.FillChoices"/>. Tactics only: the rules code reads the choice from the request and never asks
/// how it was made, so a PC and an NPC choice resolve the same.
///
/// The energy pick reads only what the caster could know: the attacks of the enemies it can see (a breath weapon, a
/// natural attack's energy damage, a weapon's energy enchantment), then the energy damage the recipient or the caster
/// last took. It never reads an enemy's spell list or an unseen creature.
/// </summary>
public static class AiCastChoiceHeuristics
{
    /// <summary>The five energy types of Resist Energy (PHB p.272) and Protection from Energy (PHB p.266), in tie order.</summary>
    private static readonly global::DamageType[] EnergyOrder =
    {
        global::DamageType.Fire, global::DamageType.Cold, global::DamageType.Acid, global::DamageType.Electricity, global::DamageType.Sonic
    };

    /// <summary>
    /// The energy type to guard <paramref name="recipient"/> against: the type of the visible enemies' known attacks with
    /// the largest expected damage (<see cref="VisibleEnemyEnergyThreat"/>); with none, the energy damage the recipient
    /// last took, then the caster's (CharacterStats.LastEnergyDamageTaken); else fire, the old default.
    /// </summary>
    public static ResistEnergyType ChooseProtectionEnergy(CharacterController caster, CharacterController recipient, IList<CharacterController> all)
    {
        Dictionary<global::DamageType, float> threat = VisibleEnemyEnergyThreat(caster, all);
        global::DamageType? best = Strongest(threat);
        if (!best.HasValue && recipient != null && recipient.Stats != null)
            best = recipient.Stats.LastEnergyDamageTaken;
        if (!best.HasValue && caster != null && caster.Stats != null)
            best = caster.Stats.LastEnergyDamageTaken;
        return best.HasValue && TryToResistEnergyType(best.Value, out ResistEnergyType type) ? type : ResistEnergyType.Fire;
    }

    /// <summary>
    /// Fire Shield's form (PHB p.230): the chill shield guards against fire, the warm shield against cold. Chill when the
    /// fire the caster faces (visible enemies' attacks, else the energy it last took) outweighs the cold; warm otherwise,
    /// the old default.
    /// </summary>
    public static bool ChooseFireShieldWarm(CharacterController caster, IList<CharacterController> all)
    {
        Dictionary<global::DamageType, float> threat = VisibleEnemyEnergyThreat(caster, all);
        threat.TryGetValue(global::DamageType.Fire, out float fire);
        threat.TryGetValue(global::DamageType.Cold, out float cold);
        if (fire <= 0f && cold <= 0f && caster != null && caster.Stats != null && caster.Stats.LastEnergyDamageTaken.HasValue)
            return caster.Stats.LastEnergyDamageTaken.Value != global::DamageType.Fire;
        return !(fire > cold);
    }

    /// <summary>
    /// The expected energy damage per type of the known attacks of every living enemy the caster can see
    /// (TeamUtility.IsEnemy, CharacterController.CanSee): a breath weapon's dice and bonus (the bombardier beetle's acid spray counts too, MM p.285), each natural attack's bonus energy dice
    /// times its count, and the energy enchantments of the weapons in its hands. Average dice, no hit chance.
    /// </summary>
    public static Dictionary<global::DamageType, float> VisibleEnemyEnergyThreat(CharacterController caster, IList<CharacterController> all)
    {
        var threat = new Dictionary<global::DamageType, float>();
        if (caster == null || all == null)
            return threat;

        for (int i = 0; i < all.Count; i++)
        {
            CharacterController enemy = all[i];
            if (enemy == null || enemy == caster || enemy.Stats == null || enemy.Stats.IsDead)
                continue;
            if (!TeamUtility.IsEnemy(caster, enemy) || !caster.CanSee(enemy))
                continue;

            BreathWeaponDefinition breath = enemy.GetBreathWeaponDefinition();
            if (breath != null)
                Add(threat, breath.DamageType, Average(breath.DamageCount, breath.DamageDice) + breath.DamageBonus);

            List<NaturalAttackDefinition> natural = enemy.Stats.GetValidNaturalAttacks();
            for (int n = 0; n < natural.Count; n++)
            {
                NaturalAttackDefinition attack = natural[n];
                if (attack != null && attack.BonusElementalDamageCount > 0 && attack.BonusElementalDamageDice > 0)
                    Add(threat, attack.BonusElementalDamageType,
                        Average(attack.BonusElementalDamageCount, attack.BonusElementalDamageDice) * System.Math.Max(1, attack.Count));
            }

            AddWeaponEnergy(threat, enemy.GetEquippedMainWeapon());
        }

        return threat;
    }

    private static void AddWeaponEnergy(Dictionary<global::DamageType, float> threat, ItemData weapon)
    {
        if (weapon == null || !weapon.IsWeapon || weapon.Enchantment == null || weapon.Enchantment.Abilities == null)
            return;
        for (int i = 0; i < weapon.Enchantment.Abilities.Count; i++)
        {
            EnchantmentStats stats = EnchantmentProperties.Get(weapon.Enchantment.Abilities[i]);
            if (stats != null && stats.ExtraDamageDice > 0 && stats.ExtraDamageDieSides > 0)
                Add(threat, stats.ExtraDamageType, Average(stats.ExtraDamageDice, stats.ExtraDamageDieSides));
        }
    }

    private static float Average(int count, int sides) => count <= 0 || sides <= 0 ? 0f : count * (sides + 1) / 2f;

    private static void Add(Dictionary<global::DamageType, float> threat, global::DamageType type, float amount)
    {
        if (amount <= 0f || !TryToResistEnergyType(type, out _))
            return;
        threat.TryGetValue(type, out float sum);
        threat[type] = sum + amount;
    }

    /// <summary>The energy type with the largest threat, ties in <see cref="EnergyOrder"/>; null when none.</summary>
    private static global::DamageType? Strongest(Dictionary<global::DamageType, float> threat)
    {
        global::DamageType? best = null;
        float bestAmount = 0f;
        for (int i = 0; i < EnergyOrder.Length; i++)
        {
            if (threat.TryGetValue(EnergyOrder[i], out float amount) && amount > bestAmount)
            {
                bestAmount = amount;
                best = EnergyOrder[i];
            }
        }
        return best;
    }

    /// <summary>The Resist Energy type of an energy damage type (acid, cold, electricity, fire, sonic); false for any other.</summary>
    public static bool TryToResistEnergyType(global::DamageType damage, out ResistEnergyType type)
    {
        switch (damage)
        {
            case global::DamageType.Acid: type = ResistEnergyType.Acid; return true;
            case global::DamageType.Cold: type = ResistEnergyType.Cold; return true;
            case global::DamageType.Electricity: type = ResistEnergyType.Electricity; return true;
            case global::DamageType.Fire: type = ResistEnergyType.Fire; return true;
            case global::DamageType.Sonic: type = ResistEnergyType.Sonic; return true;
            default: type = ResistEnergyType.Fire; return false;
        }
    }

    /// <summary>
    /// The weapon the AI enchants with Magic Weapon (PHB p.251), Greater Magic Weapon (PHB p.241) or Keen Edge (PHB
    /// p.246): the recipient's wielded primary weapon (CharacterController.GetEquippedMainWeapon), a weapon item; for
    /// Keen Edge only a piercing or slashing one (GameManager.IsKeenEdgeWeapon, the PC picker's test). Null when the
    /// recipient wields none that qualifies, or when the cast would add nothing to it (<see cref="AddsNothing"/>).
    /// <paramref name="caster"/> gives Greater Magic Weapon's caster level (null: the recipient's).
    /// </summary>
    public static ItemData ChooseWeapon(CharacterController recipient, SpellData spell, CharacterController caster = null)
    {
        if (recipient == null || spell == null)
            return null;
        ItemData weapon = recipient.GetEquippedMainWeapon();
        if (weapon == null || !weapon.IsWeapon)
            return null;
        if (spell.SpellId == SpellNames.KEEN_EDGE && !GameManager.IsKeenEdgeWeapon(weapon))
            return null;
        if (AddsNothing(weapon, spell, caster ?? recipient))
            return null;
        return weapon;
    }

    /// <summary>
    /// Whether casting <paramref name="spell"/> on <paramref name="weapon"/> would change nothing, counting only the
    /// weapon's own enchantment and the spell effects on it that outlast this round (an effect with one round left ends
    /// before the recipient's next turn, so re-casting then is not wasted):
    /// the same spell already running on it; for Magic Weapon (+1) and Greater Magic Weapon (+1 per four caster levels,
    /// at most +5) an enhancement bonus no higher than the weapon's best enhancement bonus already, item or spell
    /// (ItemData.ResolveEnhancementBonus and ItemSpellEffect.EnhancementBonusAttack/Damage, the fields
    /// ItemData.GetEnhancementAttackBonus and GetEnhancementDamageBonus give the attack code), as enhancement bonuses do
    /// not stack (PHB p.171); for Keen Edge a weapon with the keen ability or a running threat-range spell, as multiple
    /// effects that increase a weapon's threat range do not stack (PHB p.246). Tactics only: the cast itself still applies.
    /// </summary>
    public static bool AddsNothing(ItemData weapon, SpellData spell, CharacterController caster)
    {
        if (weapon == null || spell == null)
            return false;

        int bestEnhancement = System.Math.Max(0, weapon.ResolveEnhancementBonus());
        bool threatRaised = weapon.Enchantment != null && weapon.Enchantment.HasAbility(EnchantmentType.Keen);
        if (weapon.ActiveSpellEffects != null)
            for (int i = 0; i < weapon.ActiveSpellEffects.Count; i++)
            {
                ItemSpellEffect effect = weapon.ActiveSpellEffects[i];
                if (effect == null || !(effect.RemainingRounds > 1 || effect.RemainingRounds < 0))
                    continue;
                if (effect.SpellId == spell.SpellId)
                    return true;
                bestEnhancement = System.Math.Max(bestEnhancement, System.Math.Max(effect.EnhancementBonusAttack, effect.EnhancementBonusDamage));
                if (effect.CritThreatRangeModifier != 0)
                    threatRaised = true;
            }

        if (spell.SpellId == SpellNames.MAGIC_WEAPON)
            return bestEnhancement >= 1;
        if (spell.SpellId == SpellNames.GREATER_MAGIC_WEAPON)
            return bestEnhancement >= GreaterMagicWeaponBonus(caster, spell);
        if (spell.SpellId == SpellNames.KEEN_EDGE)
            return threatRaised;
        return false;
    }

    /// <summary>Greater Magic Weapon's enhancement bonus: +1 per four caster levels, at least +1, at most +5 (PHB p.241), as its resolver gives it.</summary>
    private static int GreaterMagicWeaponBonus(CharacterController caster, SpellData spell)
    {
        int casterLevel = caster != null && caster.Stats != null ? CasterLevelRules.ForCaster(caster, spell) : 0;
        return System.Math.Min(5, System.Math.Max(1, casterLevel / 4));
    }

    /// <summary>True for the three spells whose caster picks a weapon: Magic Weapon, Greater Magic Weapon, Keen Edge.</summary>
    public static bool IsWeaponChoiceSpell(SpellData spell)
        => spell != null && (spell.SpellId == SpellNames.MAGIC_WEAPON || spell.SpellId == SpellNames.GREATER_MAGIC_WEAPON
                             || spell.SpellId == SpellNames.KEEN_EDGE);

    /// <summary>
    /// The creature an AI summon calls (batch 11 step 27, SPL-091): among <paramref name="options"/> (the list of the
    /// slot's own level the caster may call, SummonMonsterLists.GetFilteredOptionsForListLevel, so one creature per cast,
    /// PHB p.285), the one most useful to its side: a creature that fights (one with a definition and at least one
    /// natural attack or a piece of equipment; a swarm option is kept last) with the highest challenge rating, then the most Hit Dice;
    /// equal ones keep the list's order. Null when there is none. Tactics only: the Summon resolve places whatever the
    /// request carries.
    /// </summary>
    public static SummonMonsterOption ChooseSummonOption(IList<SummonMonsterOption> options)
    {
        if (options == null)
            return null;
        SummonMonsterOption best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < options.Count; i++)
        {
            SummonMonsterOption option = options[i];
            NPCDefinition def = option != null ? NPCDatabase.Get(option.NpcDefinitionId) : null;
            if (def == null)
                continue;
            bool fights = (def.NaturalAttacks != null && def.NaturalAttacks.Count > 0) || (def.EquipmentIds != null && def.EquipmentIds.Count > 0);
            ChallengeRatingUtils.TryParse(def.ChallengeRating, out float cr);
            float score = (fights ? 1000f : 0f) - (SummonMonsterLists.IsSwarmOption(option) ? 500f : 0f) + cr * 10f + def.HitDice * 0.1f;
            if (score > bestScore)
            {
                bestScore = score;
                best = option;
            }
        }
        return best;
    }
}

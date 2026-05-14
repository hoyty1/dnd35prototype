using UnityEngine;

[System.Serializable]
public class ItemSpellEffect
{
    public string SpellId;
    public string SpellName;
    public string CasterName;
    public int CasterLevel;
    public int RemainingRounds;

    public BonusType BonusType = BonusType.Untyped;
    public int EnhancementBonusAttack;
    public int EnhancementBonusDamage;
    public bool CountsAsMagicForBypass;

    // --- Ammunition enchantment properties (Flame Arrow, Keen Edge, etc.) ---
    /// <summary>Bonus damage dice expression added per arrow (e.g. "1d6" for Flame Arrow).</summary>
    public string BonusDamageDice;
    /// <summary>Damage type of the bonus dice (e.g. "fire", "acid", "poison").</summary>
    public string BonusDamageType;
    /// <summary>Modifier to critical threat range minimum (e.g. -1 means 20→19, or 19-20→18-20 for Keen Edge).</summary>
    public int CritThreatRangeModifier;
    /// <summary>Number of enchanted ammunition rounds remaining for this spell effect.</summary>
    public int EnchantedAmmoRemaining;

    public ItemSpellEffect() { }

    public ItemSpellEffect(string spellId, string spellName, string casterName, int casterLevel, int remainingRounds)
    {
        SpellId = spellId;
        SpellName = spellName;
        CasterName = casterName;
        CasterLevel = casterLevel;
        RemainingRounds = remainingRounds;
    }

    /// <summary>True if this spell effect has ammo enchantment properties (Flame Arrow, etc.).</summary>
    public bool IsAmmoEnchantment => EnchantedAmmoRemaining > 0 || !string.IsNullOrEmpty(BonusDamageDice) || CritThreatRangeModifier != 0;

    /// <summary>Consume one enchanted ammo charge. Returns true if charges remain after consumption.</summary>
    public bool ConsumeOneEnchantedAmmo()
    {
        if (EnchantedAmmoRemaining <= 0)
            return false;
        EnchantedAmmoRemaining--;
        return EnchantedAmmoRemaining > 0;
    }

    public bool Tick()
    {
        if (RemainingRounds < 0)
            return false;

        if (RemainingRounds <= 0)
            return true;

        RemainingRounds--;
        return RemainingRounds <= 0;
    }

    public string GetDurationDisplayString()
    {
        if (RemainingRounds < 0)
            return "Permanent";

        if (RemainingRounds <= 0)
            return "Expired";

        if (RemainingRounds >= 20)
        {
            int minutes = RemainingRounds / 10;
            int rounds = RemainingRounds % 10;
            return rounds > 0 ? $"{minutes}m {rounds}rd" : $"{minutes}m";
        }

        return $"{RemainingRounds}rd";
    }
}

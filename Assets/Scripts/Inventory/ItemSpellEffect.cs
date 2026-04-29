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

    public ItemSpellEffect() { }

    public ItemSpellEffect(string spellId, string spellName, string casterName, int casterLevel, int remainingRounds)
    {
        SpellId = spellId;
        SpellName = spellName;
        CasterName = casterName;
        CasterLevel = casterLevel;
        RemainingRounds = remainingRounds;
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

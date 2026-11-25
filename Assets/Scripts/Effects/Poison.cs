using System;
using System.Collections.Generic;

/// <summary>
/// D&D 3.5e poison delivery methods.
/// </summary>
public enum PoisonType
{
    Contact,
    Ingested,
    Inhaled,
    Injury
}

/// <summary>
/// Static poison definition data.
/// </summary>
[Serializable]
public class PoisonData
{
    public string Id;
    public string Name;
    public PoisonType Type;
    public int FortitudeDC;
    public List<AbilityDamageEffect> InitialDamage;
    public List<AbilityDamageEffect> SecondaryDamage;
    public List<PoisonSpecialEffect> InitialSpecialEffects;
    public List<PoisonSpecialEffect> SecondarySpecialEffects;
    public int PriceInGold;
    public string Description;

    public PoisonData()
    {
        InitialDamage = new List<AbilityDamageEffect>();
        SecondaryDamage = new List<AbilityDamageEffect>();
        InitialSpecialEffects = new List<PoisonSpecialEffect>();
        SecondarySpecialEffects = new List<PoisonSpecialEffect>();
    }

    /// <summary>
    /// Rounds from exposure to the secondary save: DMG p.296-297 times every poison's secondary damage 1 minute after
    /// exposure, whatever its delivery type (contact, ingested, inhaled or injury; the Ingested paragraph gives no other
    /// timing), and a minute is 10 rounds (PHB p.138). Counted at an initiative count like any duration (PoisonTiming,
    /// CMB-007), never in real time.
    /// </summary>
    public int GetSecondaryDamageDelayRounds()
    {
        return PoisonTiming.SecondaryDelayRounds;
    }
}

/// <summary>
/// Runtime state for poison currently affecting a character. The secondary save comes after
/// <see cref="RoundsUntilSecondary"/> rounds, counted at <see cref="DurationAnchor"/>'s initiative count (the creature
/// whose count was current at exposure, as for every duration: TurnDurations, CMB-006; PoisonTiming, CMB-007).
/// </summary>
[Serializable]
public class ActivePoison
{
    public PoisonData PoisonData;
    /// <summary>Rounds left before the secondary save; it rolls when this reaches 0 at the anchor's count.</summary>
    public int RoundsUntilSecondary;
    /// <summary>
    /// The creature at whose initiative count the delay loses a round (TurnDurations.CurrentAnchor at exposure); null
    /// (exposure outside any count) or a creature no longer in the initiative order ticks at the round boundary.
    /// </summary>
    [NonSerialized] public CharacterController DurationAnchor;
    /// <summary>
    /// The Fortitude DC of both saves, figured once at exposure (a wild-shaped druid's DC is figured for her,
    /// WildShapeRules.OnHitSaveDcModifier): DMG p.296 gives a poison one DC, for the initial and the secondary save.
    /// </summary>
    public int SaveDC;
    public bool InitialSaveSucceeded;
    public bool SecondarySaveSucceeded;
    public bool SecondaryResolved;

    public ActivePoison(PoisonData data)
    {
        PoisonData = data;
        RoundsUntilSecondary = data != null ? data.GetSecondaryDamageDelayRounds() : PoisonTiming.SecondaryDelayRounds;
        DurationAnchor = TurnDurations.CurrentAnchor;
        SaveDC = data != null ? data.FortitudeDC : 0;
    }

    public string GetStatusSummary()
    {
        if (PoisonData == null)
            return "Unknown poison";

        if (!SecondaryResolved)
        {
            int rounds = Math.Max(0, RoundsUntilSecondary);
            return $"{PoisonData.Name} (secondary in {rounds} round{(rounds == 1 ? "" : "s")})";
        }

        return $"{PoisonData.Name} (resolved)";
    }
}

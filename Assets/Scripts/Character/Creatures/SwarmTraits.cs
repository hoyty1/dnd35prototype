using System;

/// <summary>
/// D&D 3.5e swarm trait payload for creatures that attack by occupying shared space.
/// </summary>
[Serializable]
public class SwarmTraits
{
    public bool IsSwarm;
    public int SwarmDamage;
    public string SwarmDamageDice = "1d6";
    public int DistractionDC = 11;
    public bool HasPoison;
    public bool HasDisease;
    public bool HasWounding;
    public DamageType SwarmDamageType = DamageType.Piercing;

    /// <summary>
    /// Existing poison database id (for example: "medium_spider_poison").
    /// </summary>
    public string PoisonId;

    /// <summary>
    /// Modifier applied to the poison's base Fortitude DC.
    /// </summary>
    public int PoisonDcModifier;

    /// <summary>
    /// Disease applied on successful swarm contact when HasDisease is true.
    /// </summary>
    public DiseaseType DiseaseType = DiseaseType.FilthFever;

    /// <summary>
    /// Modifier applied to the disease's base Fortitude DC.
    /// </summary>
    public int DiseaseDcModifier;
}

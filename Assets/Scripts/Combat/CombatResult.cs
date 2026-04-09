/// <summary>
/// Holds the result of a single attack action using D&D 3.5 mechanics.
/// Attack roll: d20 + BAB + STR mod + flanking bonus vs AC (10 + DEX mod + armor + shield)
/// Damage roll: weapon dice + STR mod + bonus damage + sneak attack (if applicable)
/// Now includes full critical hit tracking per D&D 3.5 rules.
/// </summary>
public class CombatResult
{
    public CharacterController Attacker;
    public CharacterController Defender;
    public int DieRoll;       // Raw d20 roll
    public int TotalRoll;     // Roll + total attack bonus (BAB + STR mod + flanking)
    public int TargetAC;      // Defender's AC (10 + DEX mod + armor + shield)
    public bool Hit;          // Whether the attack hit
    public int Damage;        // Base weapon damage dealt (0 if miss)
    public bool TargetKilled; // Whether the target died
    public bool NaturalTwenty;  // Natural 20 (auto-hit)
    public bool NaturalOne;     // Natural 1 (auto-miss)

    // Flanking fields
    public bool IsFlanking;               // Whether the attacker is flanking
    public int FlankingBonus;             // +2 if flanking, 0 otherwise
    public string FlankingPartnerName;    // Name of the flanking ally

    // Sneak Attack fields
    public bool SneakAttackApplied;       // Whether sneak attack damage was added
    public int SneakAttackDice;           // Number of d6 rolled (e.g., 2 for 2d6)
    public int SneakAttackDamage;         // Total sneak attack damage rolled

    // Critical Hit fields (D&D 3.5)
    public bool IsCritThreat;             // Whether the natural roll was in the weapon's threat range
    public bool CritConfirmed;            // Whether the confirmation roll succeeded
    public int ConfirmationRoll;          // Natural d20 of the confirmation roll
    public int ConfirmationTotal;         // Total of confirmation roll (roll + attack mod)
    public int CritMultiplier;            // Weapon's crit multiplier (×2, ×3, etc.)
    public int CritThreatMin;             // Weapon's threat range minimum (e.g. 19 for 19-20)
    public string CritDamageDice;         // Description of crit damage dice (e.g. "2d8+3")

    // Racial bonus fields
    public int RacialAttackBonus;         // Racial attack bonus applied (e.g., Dwarf +1 vs Goblinoids)

    // Size bonus fields
    public int SizeAttackBonus;           // Size modifier to attack (Small = +1, Medium = 0)

    // Damage modifier fields (D&D 3.5 weapon damage modifiers)
    public int DamageModifier;            // The actual STR-based damage bonus applied (e.g., +6 for 1.5× STR)
    public string DamageModifierDesc;     // Description for combat log (e.g., "1.5× STR", "composite +2")

    // Range increment fields (D&D 3.5 ranged attack rules)
    public bool IsRangedAttack;           // Whether this was a ranged attack
    public int RangeDistanceFeet;         // Distance to target in feet
    public int RangeIncrementNumber;      // Which range increment (1 = first, no penalty)
    public int RangePenalty;              // Attack penalty from range (-2 per increment beyond first)
    public string WeaponName;             // Name of weapon used (for combat log)

    // Feat fields (D&D 3.5)
    public int PowerAttackValue;          // Power Attack penalty/bonus value (0 = not active)
    public int PowerAttackDamageBonus;    // Actual damage bonus from Power Attack (may be 2× for two-handed)
    public bool RapidShotActive;          // Whether Rapid Shot was active for this attack
    public bool PointBlankShotActive;     // Whether Point Blank Shot bonus was applied

    /// <summary>Total damage dealt including sneak attack and feat bonuses.</summary>
    public int TotalDamage => Damage + SneakAttackDamage;

    public string GetSummary()
    {
        string attackerName = Attacker.Stats.CharacterName;
        string defenderName = Defender.Stats.CharacterName;
        int atkBonus = Attacker.Stats.AttackBonus;
        string atkBonusStr = CharacterStats.FormatMod(atkBonus);

        string critNote = "";
        if (NaturalTwenty) critNote = " (NATURAL 20!)";
        else if (NaturalOne) critNote = " (NATURAL 1!)";

        // Weapon name for ranged attacks
        string weaponNote = "";
        if (!string.IsNullOrEmpty(WeaponName))
            weaponNote = $" with {WeaponName}";

        // Active feats note
        string featsNote = "";
        var activeFeats = new System.Collections.Generic.List<string>();
        if (PowerAttackValue > 0) activeFeats.Add($"Power Attack -{PowerAttackValue}");
        if (RapidShotActive) activeFeats.Add("Rapid Shot");
        if (PointBlankShotActive) activeFeats.Add("Point Blank Shot");
        if (activeFeats.Count > 0)
            featsNote = $" ({string.Join(", ", activeFeats)})";

        // Range info note
        string rangeNote = "";
        if (IsRangedAttack)
        {
            if (RangePenalty == 0)
                rangeNote = $" at {RangeDistanceFeet} ft (increment {RangeIncrementNumber}, no penalty)";
            else
                rangeNote = $" at {RangeDistanceFeet} ft (increment {RangeIncrementNumber}, {RangePenalty} penalty)";
        }

        // Flanking note for the attack line
        string flankNote = "";
        if (IsFlanking)
            flankNote = $" [FLANKING with {FlankingPartnerName}, +{FlankingBonus}]";

        // Racial bonus note
        string racialNote = "";
        if (RacialAttackBonus > 0)
            racialNote = $" [Racial +{RacialAttackBonus} vs {Defender.Stats.CharacterName}]";

        // Size bonus note
        string sizeNote = "";
        if (SizeAttackBonus != 0)
            sizeNote = $" [Size: {Attacker.Stats.SizeCategory}]";

        if (Hit)
        {
            // Build the attack roll breakdown
            string rollBreakdown;
            string racialStr = RacialAttackBonus > 0 ? $" +{RacialAttackBonus} racial" : "";
            string sizeStr = SizeAttackBonus != 0 ? $" {CharacterStats.FormatMod(SizeAttackBonus)} size" : "";
            string rangeStr = (IsRangedAttack && RangePenalty != 0) ? $" {RangePenalty} range" : "";
            string powerAtkStr = PowerAttackValue > 0 ? $" -{PowerAttackValue} Power Attack" : "";
            string rapidShotStr = RapidShotActive ? " -2 Rapid Shot" : "";
            string pbsAtkStr = PointBlankShotActive ? " +1 PBS" : "";
            if (IsFlanking)
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr}{sizeStr} +{FlankingBonus} flanking{racialStr}{rangeStr}{powerAtkStr}{rapidShotStr}{pbsAtkStr} = {TotalRoll} vs AC {TargetAC} - HIT!{critNote}";
            else
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr}{sizeStr}{racialStr}{rangeStr}{powerAtkStr}{rapidShotStr}{pbsAtkStr} = {TotalRoll} vs AC {TargetAC} - HIT!{critNote}";

            // Critical hit info
            string critInfo = "";
            if (IsCritThreat)
            {
                string threatRange = CritThreatMin < 20 ? $"{CritThreatMin}-20" : "20";
                critInfo = $"\n*** Critical Threat! (threat range {threatRange}) ***";
                string confModStr = CharacterStats.FormatMod(ConfirmationTotal - ConfirmationRoll);
                if (CritConfirmed)
                {
                    critInfo += $"\nConfirmation: {ConfirmationRoll} {confModStr} = {ConfirmationTotal} vs AC {TargetAC} - CONFIRMED! (×{CritMultiplier})";
                }
                else
                {
                    critInfo += $"\nConfirmation: {ConfirmationRoll} {confModStr} = {ConfirmationTotal} vs AC {TargetAC} - Not confirmed, normal hit";
                }
            }

            // Build the damage line with proper damage modifier description
            string dmgModNote = "";
            if (!string.IsNullOrEmpty(DamageModifierDesc))
                dmgModNote = $" ({DamageModifierDesc} {CharacterStats.FormatMod(DamageModifier)})";

            // Feat damage bonuses
            string featDmgNote = "";
            var featDmgParts = new System.Collections.Generic.List<string>();
            if (PowerAttackDamageBonus > 0) featDmgParts.Add($"+{PowerAttackDamageBonus} Power Attack");
            if (PointBlankShotActive) featDmgParts.Add("+1 PBS");
            if (featDmgParts.Count > 0) featDmgNote = $" [{string.Join(", ", featDmgParts)}]";

            string damageStr;
            if (CritConfirmed)
            {
                if (SneakAttackApplied)
                    damageStr = $"CRITICAL HIT! {CritDamageDice} = {Damage} damage{dmgModNote}{featDmgNote} + {SneakAttackDamage} sneak attack ({SneakAttackDice}d6) = {TotalDamage} total!";
                else
                    damageStr = $"CRITICAL HIT! {CritDamageDice} = {Damage} damage!{dmgModNote}{featDmgNote}";
            }
            else
            {
                if (SneakAttackApplied)
                    damageStr = $"Deals {Damage} damage{dmgModNote}{featDmgNote} + {SneakAttackDamage} sneak attack ({SneakAttackDice}d6) = {TotalDamage} total!";
                else
                    damageStr = $"Deals {Damage} damage!{dmgModNote}{featDmgNote}";
            }

            string msg = $"{attackerName} attacks {defenderName}{weaponNote}{featsNote}{rangeNote}!{flankNote}{racialNote}{sizeNote}\n{rollBreakdown}{critInfo}\n{damageStr}";

            if (TargetKilled)
                msg += $"\n{defenderName} has been slain!";
            return msg;
        }
        else
        {
            string rollBreakdown;
            string racialStr = RacialAttackBonus > 0 ? $" +{RacialAttackBonus} racial" : "";
            string sizeStr = SizeAttackBonus != 0 ? $" {CharacterStats.FormatMod(SizeAttackBonus)} size" : "";
            string rangeStr = (IsRangedAttack && RangePenalty != 0) ? $" {RangePenalty} range" : "";
            string powerAtkStr = PowerAttackValue > 0 ? $" -{PowerAttackValue} Power Attack" : "";
            string rapidShotStr = RapidShotActive ? " -2 Rapid Shot" : "";
            string pbsAtkStr = PointBlankShotActive ? " +1 PBS" : "";
            if (IsFlanking)
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr}{sizeStr} +{FlankingBonus} flanking{racialStr}{rangeStr}{powerAtkStr}{rapidShotStr}{pbsAtkStr} = {TotalRoll} vs AC {TargetAC} - MISS!{critNote}";
            else
                rollBreakdown = $"Roll: {DieRoll} {atkBonusStr}{sizeStr}{racialStr}{rangeStr}{powerAtkStr}{rapidShotStr}{pbsAtkStr} = {TotalRoll} vs AC {TargetAC} - MISS!{critNote}";

            return $"{attackerName} attacks {defenderName}{weaponNote}{featsNote}{rangeNote}!{flankNote}{racialNote}{sizeNote}\n{rollBreakdown}";
        }
    }
}

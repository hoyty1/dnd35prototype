using UnityEngine;

/// <summary>
/// Static utility that centralises combat-log colour constants and common
/// formatting helpers.  Every public method returns a ready-to-display
/// rich-text string — callers still push the result through
/// <c>CombatUI?.ShowCombatLog(...)</c>.
///
/// Phase 4F extraction — proof-of-concept covering ~40 call sites.
/// The remaining ~1 000 ShowCombatLog calls can be migrated incrementally.
/// </summary>
public static class CombatLogHelper
{
    // ─────────────────────────────────────────────
    //  Named colour constants  (hex WITHOUT the #)
    // ─────────────────────────────────────────────

    // --- Primary semantic colours ---
    public const string ColorGold       = "FFD700";  // special / notable events
    public const string ColorGray       = "AAAAAA";  // neutral / no-effect info
    public const string ColorOrange     = "FFAA44";  // warnings / contested rolls
    public const string ColorRed        = "FF6666";  // failures / bad outcomes
    public const string ColorDarkRed    = "FF4444";  // critical failures / destruction
    public const string ColorBrightRed  = "FF8888";  // damage / errors
    public const string ColorGreen      = "88FF88";  // success / healing / resists
    public const string ColorCyan       = "88FFEE";  // spell-cast / buff applied
    public const string ColorLightBlue  = "88CCFF";  // concentration / defensive
    public const string ColorYellow     = "FFCC66";  // buffs / enhancements
    public const string ColorAmber      = "FF9966";  // secondary warnings

    // --- Supplementary colours ---
    public const string ColorDimRed     = "FF6644";  // damage variants
    public const string ColorSkyBlue    = "66E8FF";  // aura / area info
    public const string ColorPaleBlue   = "AADDFF";  // misc info
    public const string ColorIceBlue    = "A6F3FF";  // cold / water effects
    public const string ColorLavender   = "AAAAFF";  // arcane / misc
    public const string ColorSoftGreen  = "66CC66";  // minor heals
    public const string ColorBrightGreen= "66FF66";  // strong heals
    public const string ColorDeepRed    = "FF0000";  // death / critical
    public const string ColorRoyalBlue  = "4169E1";  // divine / special
    public const string ColorDimGray    = "888888";  // de-emphasised text
    public const string ColorSoftYellow = "FFDD44";  // minor highlights
    public const string ColorSteelBlue  = "99CCFF";  // misc cool info
    public const string ColorAqua       = "44CCFF";  // water / cold
    public const string ColorDarkCrimson= "8B0000";  // necrotic / dark

    // ─────────────────────────────────────────────
    //  Core formatting helpers
    // ─────────────────────────────────────────────

    /// <summary>Wrap <paramref name="text"/> in a Unity rich-text colour tag.</summary>
    public static string Color(string text, string hex)
        => $"<color=#{hex}>{text}</color>";

    // ─────────────────────────────────────────────
    //  Spell-cast / buff messages  (#88FFEE cyan)
    // ─────────────────────────────────────────────

    /// <summary>Generic spell-effect line:  "✨ Spell does X to Target (duration)."</summary>
    public static string SpellEffect(string emoji, string message)
        => Color($"{emoji} {message}", ColorCyan);

    /// <summary>Generic buff-applied line with caster context.</summary>
    public static string BuffApplied(string emoji, string targetName, string description)
        => Color($"{emoji} {targetName} {description}", ColorCyan);

    // ─────────────────────────────────────────────
    //  Damage / negative outcomes  (#FF8888 bright red)
    // ─────────────────────────────────────────────

    /// <summary>Generic damage or bad-outcome line.</summary>
    public static string Damage(string emoji, string message)
        => Color($"{emoji} {message}", ColorBrightRed);

    /// <summary>Formatted damage with HP tracking:  "💥 Target takes N type [before→after HP]"</summary>
    public static string DamageWithHP(string emoji, string targetName, int damage, string damageType, int hpBefore, int hpAfter)
        => Color($"{emoji} {targetName} takes {damage} {damageType} damage [{hpBefore}→{hpAfter} HP]", ColorBrightRed);

    // ─────────────────────────────────────────────
    //  Failures (#FF6666 red  /  #FF4444 dark red)
    // ─────────────────────────────────────────────

    /// <summary>Spell failure / fizzle line.</summary>
    public static string Failure(string emoji, string message)
        => Color($"{emoji} {message}", ColorRed);

    /// <summary>Critical / destructive failure.</summary>
    public static string CriticalFailure(string emoji, string message)
        => Color($"{emoji} {message}", ColorDarkRed);

    // ─────────────────────────────────────────────
    //  Success / healing / resist  (#88FF88 green)
    // ─────────────────────────────────────────────

    /// <summary>Generic success / positive-outcome line.</summary>
    public static string Success(string emoji, string message)
        => Color($"{emoji} {message}", ColorGreen);

    /// <summary>Healing message with HP delta.</summary>
    public static string Healing(string targetName, int healed, int hpBefore, int hpAfter)
        => Color($"💚 {targetName} healed for {healed} HP [{hpBefore}→{hpAfter}]", ColorGreen);

    /// <summary>Target resists a spell.</summary>
    public static string SpellResisted(string targetName, string spellName)
        => Color($"🛡 {targetName} resists {spellName}!", ColorGreen);

    // ─────────────────────────────────────────────
    //  Neutral / informational  (#AAAAAA gray)
    // ─────────────────────────────────────────────

    /// <summary>Neutral information line (no significant game effect).</summary>
    public static string Info(string emoji, string message)
        => Color($"{emoji} {message}", ColorGray);

    /// <summary>"No effect" / unaffected line.</summary>
    public static string NoEffect(string emoji, string spellName, string targetName, string reason)
        => Color($"{emoji} {spellName}: {targetName} is unaffected ({reason}).", ColorGray);

    // ─────────────────────────────────────────────
    //  Warnings / contested  (#FFAA44 orange)
    // ─────────────────────────────────────────────

    /// <summary>Warning or contested-roll line.</summary>
    public static string Warning(string emoji, string message)
        => Color($"{emoji} {message}", ColorOrange);

    // ─────────────────────────────────────────────
    //  Gold / special  (#FFD700)
    // ─────────────────────────────────────────────

    /// <summary>Gold-coloured notable event.</summary>
    public static string Special(string emoji, string message)
        => Color($"{emoji} {message}", ColorGold);

    // ─────────────────────────────────────────────
    //  Yellow / buff enhancement  (#FFCC66)
    // ─────────────────────────────────────────────

    /// <summary>Buff / enhancement line.</summary>
    public static string Buff(string emoji, string message)
        => Color($"{emoji} {message}", ColorYellow);

    // ─────────────────────────────────────────────
    //  Save-result one-liner
    // ─────────────────────────────────────────────

    /// <summary>
    /// Formats a saving-throw result line.
    /// Green on success, red on failure.
    /// </summary>
    public static string SaveResult(string targetName, bool success, string saveType, int roll, int dc)
    {
        if (success)
            return Color($"🛡 {targetName} makes {saveType} save! (roll {roll} vs DC {dc})", ColorGreen);
        else
            return Color($"❌ {targetName} fails {saveType} save! (roll {roll} vs DC {dc})", ColorRed);
    }

    // ─────────────────────────────────────────────
    //  Condition / status
    // ─────────────────────────────────────────────

    /// <summary>A condition or status effect applied to a target.</summary>
    public static string ConditionApplied(string emoji, string targetName, string condition, string duration)
        => Color($"{emoji} {targetName} is {condition} for {duration}!", ColorOrange);

    /// <summary>A condition fading / expiring (gray).</summary>
    public static string ConditionFaded(string emoji, string characterName, string effect)
        => Color($"{emoji} {characterName}'s {effect} fades.", ColorGray);

    // ─────────────────────────────────────────────
    //  Expiration / timer  (#FFAA44 orange)
    // ─────────────────────────────────────────────

    /// <summary>Spell or effect expiration line (orange timer).</summary>
    public static string Expired(string emoji, string message)
        => Color($"{emoji} {message}", ColorOrange);

    // ─────────────────────────────────────────────
    //  Summon / creature  (#66E8FF sky-blue)
    // ─────────────────────────────────────────────

    /// <summary>Summon action or creature info (sky-blue).</summary>
    public static string Summon(string emoji, string message)
        => Color($"{emoji} {message}", ColorSkyBlue);

    /// <summary>Summon action (sky-blue, no emoji prefix).</summary>
    public static string SummonRaw(string message)
        => Color(message, ColorSkyBlue);

    // ─────────────────────────────────────────────
    //  Spell Resistance  (#AAAAFF lavender)
    // ─────────────────────────────────────────────

    /// <summary>Spell-resistance related message (lavender).</summary>
    public static string SpellResistance(string emoji, string message)
        => Color($"{emoji} {message}", ColorLavender);

    // ─────────────────────────────────────────────
    //  Condition immune / resist  (#66CC66 soft green)
    // ─────────────────────────────────────────────

    /// <summary>Condition immunity or soft resist (soft green).</summary>
    public static string Immune(string emoji, string message)
        => Color($"{emoji} {message}", ColorSoftGreen);

    // ─────────────────────────────────────────────
    //  Casting interrupt  (#FF6644 dim red)
    // ─────────────────────────────────────────────

    /// <summary>Casting interrupted or charge lost (dim red).</summary>
    public static string Interrupted(string emoji, string message)
        => Color($"{emoji} {message}", ColorDimRed);

    /// <summary>Casting interrupted (dim red, no emoji prefix — for pre-formatted strings).</summary>
    public static string InterruptedRaw(string message)
        => Color(message, ColorDimRed);

    // ─────────────────────────────────────────────
    //  Amber / debuff condition  (#FF9966)
    // ─────────────────────────────────────────────

    /// <summary>Debuff or crowd-control condition applied (amber).</summary>
    public static string Debuff(string emoji, string message)
        => Color($"{emoji} {message}", ColorAmber);

    // ─────────────────────────────────────────────
    //  Light-blue / defensive  (#88CCFF)
    // ─────────────────────────────────────────────

    /// <summary>Defensive/concentration action (light blue).</summary>
    public static string Defensive(string emoji, string message)
        => Color($"{emoji} {message}", ColorLightBlue);
}

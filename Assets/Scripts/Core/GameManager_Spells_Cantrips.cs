// ============================================================================
// GameManager_Spells_Cantrips.cs — Phase 2 Cantrip/Orison Effect Handlers
//
// Part of the GameManager partial class.
// Implements simple utility cantrips using the "always-succeeds + log message"
// pattern. These spells have minimal combat impact but provide flavour and
// class identity for Bard, Druid, and other partial casters.
//
// All spells follow D&D 3.5e PHB core rules ONLY.
// ============================================================================
using DND35e.Identifiers;
using System.Text;
using UnityEngine;

public partial class GameManager
{
    // ================================================================
    //  GHOST SOUND — PHB p.235
    //  Illusion (Figment)
    //  Level: Brd 0, Sor/Wiz 0
    //  Components: V, S, M (a bit of wool or a small lump of wax)
    //  Range: Close (25 ft. + 5 ft./2 levels)
    //  Duration: 1 round/level (D)
    //  Saving Throw: Will disbelief (if interacted with)
    //  Ghost sound allows you to create a volume of sound that rises,
    //  recedes, approaches, or remains at a fixed place.
    // ================================================================

    private ActiveSpellEffect ApplyGhostSoundEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"👻 {casterName} casts Ghost Sound!");
        sb.AppendLine($"  School: Illusion (Figment) | Level: 0 (Cantrip)");
        sb.AppendLine($"  Illusory sounds echo through the area — footsteps, voices, rattling chains...");
        sb.AppendLine($"  Will disbelief (if interacted with). Duration: {spell.BuffDurationRounds} rounds.");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        Debug.Log($"[Cantrip] Ghost Sound cast by {casterName}");
        return null;
    }

    // ================================================================
    //  CREATE WATER — PHB p.215
    //  Conjuration (Creation) [Water]
    //  Level: Clr 0, Drd 0, Pal 1
    //  Components: V, S
    //  Range: Close (25 ft. + 5 ft./2 levels)
    //  Duration: Instantaneous
    //  This spell generates wholesome, drinkable water (up to 2 gallons
    //  per caster level).
    // ================================================================

    private ActiveSpellEffect ApplyCreateWaterEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats != null ? caster.Stats.CasterLevel : 1);
        int gallons = casterLevel * 2;

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💧 {casterName} casts Create Water!");
        sb.AppendLine($"  School: Conjuration (Creation) | Level: 0 (Orison)");
        sb.AppendLine($"  {gallons} gallons of pure, drinkable water spring into existence.");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        Debug.Log($"[Cantrip] Create Water cast by {casterName} — {gallons} gallons created");
        return null;
    }

    // ================================================================
    //  MESSAGE — PHB p.253
    //  Transmutation [Language-Dependent]
    //  Level: Brd 0, Sor/Wiz 0
    //  Components: V, S, F (a short piece of copper wire)
    //  Range: Medium (100 ft. + 10 ft./level)
    //  Duration: 10 min./level
    //  You can whisper messages and receive whispered replies with
    //  little chance of being overheard.
    // ================================================================

    private ActiveSpellEffect ApplyMessageEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"💬 {casterName} casts Message!");
        sb.AppendLine($"  School: Transmutation | Level: 0 (Cantrip)");
        sb.AppendLine($"  Whispered messages can now be delivered across the battlefield.");
        sb.AppendLine($"  Duration: {spell.BuffDurationRounds} rounds. Targets can whisper back.");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        Debug.Log($"[Cantrip] Message cast by {casterName}");
        return null;
    }

    // ================================================================
    //  PRESTIDIGITATION — PHB p.264
    //  Universal
    //  Level: Brd 0, Sor/Wiz 0
    //  Components: V, S
    //  Range: 10 ft.
    //  Duration: 1 hour
    //  Prestidigitations are minor tricks that novice spellcasters use
    //  for practice. Once cast, a prestidigitation can perform one of
    //  several minor effects for 1 hour.
    // ================================================================

    private ActiveSpellEffect ApplyPrestidigitationEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"✨ {casterName} casts Prestidigitation!");
        sb.AppendLine($"  School: Universal | Level: 0 (Cantrip)");
        sb.AppendLine($"  A minor magical effect manifests — colours shift, objects warm or chill,");
        sb.AppendLine($"  small items are cleaned or soiled. Duration: 1 hour.");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        Debug.Log($"[Cantrip] Prestidigitation cast by {casterName}");
        return null;
    }

    // ================================================================
    //  PURIFY FOOD AND DRINK — PHB p.267
    //  Transmutation
    //  Level: Clr 0, Drd 0
    //  Components: V, S
    //  Range: 10 ft.
    //  Duration: Instantaneous
    //  This spell makes spoiled, rotten, poisonous, or otherwise
    //  contaminated food and water pure and suitable for eating and
    //  drinking. This spell does not prevent subsequent natural decay
    //  or spoilage.
    // ================================================================

    private ActiveSpellEffect ApplyPurifyFoodAndDrinkEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        int casterLevel = Mathf.Max(1, caster.Stats != null ? caster.Stats.CasterLevel : 1);

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🍞 {casterName} casts Purify Food and Drink!");
        sb.AppendLine($"  School: Transmutation | Level: 0 (Orison)");
        sb.AppendLine($"  Up to {casterLevel} cu. ft. of food and water is purified and made safe to consume.");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        Debug.Log($"[Cantrip] Purify Food and Drink cast by {casterName}");
        return null;
    }

    // ================================================================
    //  MENDING — PHB p.253
    //  Transmutation
    //  Level: Brd 0, Clr 0, Drd 0, Sor/Wiz 0
    //  Components: V, S
    //  Range: 10 ft.
    //  Duration: Instantaneous
    //  Mending repairs small breaks or tears in objects (but not warps,
    //  such as might be caused by a warp wood spell). It will weld
    //  broken metallic objects such as a ring, a chain link, a
    //  medallion, or a slender dagger, providing but one break exists.
    //  Ceramics, wooden objects, and cloth can also be repaired.
    // ================================================================

    private ActiveSpellEffect ApplyMendingEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🔧 {casterName} casts Mending!");
        sb.AppendLine($"  School: Transmutation | Level: 0 (Cantrip)");
        sb.AppendLine($"  A nearby item is magically repaired — cracks seal, tears mend, links re-forge.");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        Debug.Log($"[Cantrip] Mending cast by {casterName}");
        return null;
    }

    // ================================================================
    //  KNOW DIRECTION — PHB p.246
    //  Divination
    //  Level: Brd 0, Drd 0
    //  Components: V, S
    //  Range: Personal
    //  Target: You
    //  Duration: Instantaneous
    //  You instantly know the direction of north from your current
    //  position.
    // ================================================================

    private ActiveSpellEffect ApplyKnowDirectionEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || spell == null) return null;

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Unknown";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🧭 {casterName} casts Know Direction!");
        sb.AppendLine($"  School: Divination | Level: 0 (Cantrip)");
        sb.AppendLine($"  {casterName} instantly knows which way is north.");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        Debug.Log($"[Cantrip] Know Direction cast by {casterName}");
        return null;
    }

    // ================================================================
    //  LULLABY — PHB p.249
    //  Enchantment (Compulsion) [Mind-Affecting]
    //  Level: Brd 0
    //  Components: V, S
    //  Range: Medium (100 ft. + 10 ft./level)
    //  Duration: Concentration + 1 round/level (D)
    //  Saving Throw: Will negates
    //  Spell Resistance: Yes
    //
    //  Target takes –5 penalty on Listen checks and –2 penalty on
    //  Will saves against sleep effects while the lullaby is in effect.
    // ================================================================

    private ActiveSpellEffect ApplyLullabyEffect(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp)
    {
        if (caster == null || target == null || spell == null) return null;

        string casterName = caster.Stats != null ? caster.Stats.CharacterName : "Unknown";
        string targetName = target.Stats != null ? target.Stats.CharacterName : "Unknown";

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine($"🎵 {casterName} casts Lullaby on {targetName}!");
        sb.AppendLine($"  School: Enchantment (Compulsion) | Level: 0 (Cantrip)");
        sb.AppendLine($"  {targetName} feels drowsy... –5 on Listen checks, –2 on Will saves vs sleep.");
        sb.AppendLine($"  Duration: {spell.BuffDurationRounds} rounds.");
        sb.Append("═══════════════════════════════════");
        CombatUI?.ShowCombatLog(sb.ToString());
        Debug.Log($"[Cantrip] Lullaby cast by {casterName} on {targetName}");
        return null;
    }

    // ================================================================
    //  Cantrip Dispatch — called from ApplySpellBuff
    //  Returns true if the spell was handled as a utility cantrip.
    // ================================================================

    private bool TryApplyUtilityCantrip(
        CharacterController caster,
        CharacterController target,
        SpellData spell,
        SpellcastingComponent spellComp,
        out ActiveSpellEffect effect)
    {
        effect = null;
        if (spell == null) return false;

        if (spell.SpellId == SpellNames.GHOST_SOUND)
        {
            effect = ApplyGhostSoundEffect(caster, target, spell, spellComp);
            return true;
        }
        if (spell.SpellId == SpellNames.CREATE_WATER)
        {
            effect = ApplyCreateWaterEffect(caster, target, spell, spellComp);
            return true;
        }
        if (spell.SpellId == SpellNames.MESSAGE)
        {
            effect = ApplyMessageEffect(caster, target, spell, spellComp);
            return true;
        }
        if (spell.SpellId == SpellNames.PRESTIDIGITATION)
        {
            effect = ApplyPrestidigitationEffect(caster, target, spell, spellComp);
            return true;
        }
        if (spell.SpellId == SpellNames.PURIFY_FOOD_DRINK)
        {
            effect = ApplyPurifyFoodAndDrinkEffect(caster, target, spell, spellComp);
            return true;
        }
        if (spell.SpellId == SpellNames.MENDING)
        {
            effect = ApplyMendingEffect(caster, target, spell, spellComp);
            return true;
        }
        if (spell.SpellId == SpellNames.KNOW_DIRECTION)
        {
            effect = ApplyKnowDirectionEffect(caster, target, spell, spellComp);
            return true;
        }
        if (spell.SpellId == SpellNames.LULLABY)
        {
            effect = ApplyLullabyEffect(caster, target, spell, spellComp);
            return true;
        }

        return false;
    }
}

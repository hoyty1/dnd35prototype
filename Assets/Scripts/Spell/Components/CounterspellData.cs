using System;
using System.Collections.Generic;
using UnityEngine;

namespace DND35.Magic
{
    /// <summary>
    /// Tracks a character's readied counterspell state per D&D 3.5e PHB rules.
    /// A character uses their standard action to ready a counterspell on their turn.
    /// The readied action triggers when the specified enemy (or any enemy) begins casting a spell.
    /// </summary>
    [Serializable]
    public class CounterspellData
    {
        /// <summary>The character who readied the counterspell action.</summary>
        public CharacterController Counterspeller;

        /// <summary>
        /// Specific enemy caster being watched. If null, watches any enemy caster.
        /// PHB: "You select a specific opponent as the target of your counterspell."
        /// </summary>
        public CharacterController WatchedCaster;

        /// <summary>
        /// If true, triggers on any enemy spell cast (not just the watched caster).
        /// Default PHB behavior is to watch a specific opponent.
        /// </summary>
        public bool WatchAnyEnemy;

        /// <summary>
        /// The Spellcraft bonus of the counterspeller (INT mod + ranks + feat bonuses).
        /// Used for spell identification: DC = 15 + spell level.
        /// </summary>
        public int SpellcraftBonus;

        /// <summary>
        /// Whether to prefer using Dispel Magic over same-spell counter.
        /// AI uses this for decision-making; player always gets the choice.
        /// </summary>
        public bool PreferDispelMagic;

        /// <summary>
        /// Whether this readied action has already been used this round.
        /// A readied action can only trigger once.
        /// </summary>
        public bool HasTriggered;

        /// <summary>
        /// The round number when this counterspell was readied.
        /// Expires at the start of the counterspeller's next turn.
        /// </summary>
        public int ReadiedOnRound;

        /// <summary>Is this counterspell data active and not yet triggered?</summary>
        public bool IsActive => Counterspeller != null && !HasTriggered;

        /// <summary>
        /// Check if this readied counterspell should trigger for a given caster.
        /// </summary>
        public bool ShouldTriggerFor(CharacterController spellCaster)
        {
            if (!IsActive) return false;
            if (spellCaster == null) return false;
            if (spellCaster == Counterspeller) return false; // Can't counterspell yourself

            if (WatchAnyEnemy)
            {
                // Trigger on any enemy — different team
                return spellCaster.Team != Counterspeller.Team;
            }

            // Trigger only on the specific watched caster
            return WatchedCaster != null && spellCaster == WatchedCaster;
        }

        /// <summary>Mark this readied counterspell as having been triggered.</summary>
        public void MarkTriggered()
        {
            HasTriggered = true;
        }

        /// <summary>Clear all counterspell state.</summary>
        public void Clear()
        {
            Counterspeller = null;
            WatchedCaster = null;
            WatchAnyEnemy = false;
            SpellcraftBonus = 0;
            PreferDispelMagic = false;
            HasTriggered = false;
            ReadiedOnRound = -1;
        }
    }

    /// <summary>
    /// Result of a counterspell attempt, used for logging and game state updates.
    /// </summary>
    [Serializable]
    public class CounterspellResult
    {
        /// <summary>Whether the counterspell successfully negated the enemy spell.</summary>
        public bool Success;

        /// <summary>Method used: "SameSpell", "DesignatedCounter", or "DispelMagic".</summary>
        public string Method;

        /// <summary>The spell used to counter (e.g., the same spell or Dispel Magic).</summary>
        public SpellData CounterSpellUsed;

        /// <summary>The enemy spell that was (attempted to be) countered.</summary>
        public SpellData EnemySpell;

        /// <summary>Whether the Spellcraft identification check succeeded.</summary>
        public bool SpellIdentified;

        /// <summary>The Spellcraft check roll (d20 + bonus).</summary>
        public int SpellcraftRoll;

        /// <summary>The Spellcraft DC (15 + spell level).</summary>
        public int SpellcraftDC;

        /// <summary>For Dispel Magic: the dispel check total (d20 + CL, capped).</summary>
        public int DispelCheckTotal;

        /// <summary>For Dispel Magic: the DC to beat (11 + enemy CL).</summary>
        public int DispelCheckDC;

        /// <summary>The counterspeller.</summary>
        public CharacterController Counterspeller;

        /// <summary>The original caster whose spell was targeted.</summary>
        public CharacterController OriginalCaster;

        /// <summary>Detailed log message for the combat log.</summary>
        public string LogMessage;
    }

    /// <summary>
    /// Designated counter spell pairs per PHB — specific spells that automatically counter each other.
    /// </summary>
    public static class DesignatedCounterPairs
    {
        private static readonly Dictionary<string, string> _pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "haste", "slow" },
            { "slow", "haste" },
            { "bless", "bane" },
            { "bane", "bless" },
            { "enlarge_person", "reduce_person" },
            { "reduce_person", "enlarge_person" },
            { "mass_enlarge_person", "mass_reduce_person" },
            { "mass_reduce_person", "mass_enlarge_person" },
        };

        /// <summary>
        /// Get the designated counter for a spell, if one exists.
        /// Returns null if no designated counter exists.
        /// </summary>
        public static string GetDesignatedCounter(string spellId)
        {
            if (string.IsNullOrEmpty(spellId)) return null;
            return _pairs.TryGetValue(spellId, out string counter) ? counter : null;
        }

        /// <summary>
        /// Check if two spells are designated counter pairs.
        /// </summary>
        public static bool AreDesignatedCounters(string spellIdA, string spellIdB)
        {
            if (string.IsNullOrEmpty(spellIdA) || string.IsNullOrEmpty(spellIdB)) return false;
            string counter = GetDesignatedCounter(spellIdA);
            return counter != null && string.Equals(counter, spellIdB, StringComparison.OrdinalIgnoreCase);
        }
    }
}

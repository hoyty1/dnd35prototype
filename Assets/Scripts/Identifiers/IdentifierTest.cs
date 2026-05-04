#if UNITY_EDITOR
using UnityEngine;

namespace DND35e.Identifiers
{
    /// <summary>
    /// Simple sanity checks for identifier constants.
    /// </summary>
    public class IdentifierTest : MonoBehaviour
    {
        [ContextMenu("Test Item IDs")]
        public void TestItemIDs()
        {
            Debug.Log($"Item constant sample: {ItemIDs.LONGSWORD}, {ItemIDs.POTION_CURE_LIGHT_WOUNDS}, {ItemIDs.SHIELD_HEAVY_STEEL}");
        }

        [ContextMenu("Test Spell Names")]
        public void TestSpellNames()
        {
            Debug.Log($"Spell constant sample: {SpellNames.MAGIC_MISSILE}, {SpellNames.SHIELD}, {SpellNames.GLITTERDUST}");
        }
    }
}
#endif

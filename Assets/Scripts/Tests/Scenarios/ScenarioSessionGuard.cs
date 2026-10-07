#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Tests.Scenarios
{
    /// <summary>
    /// Cleans up scenario-harness state when Play mode ends. Enter Play Mode Options are off, so Unity
    /// reloads the domain when Play starts but not when it ends: static state set during Play survives
    /// into edit mode. On ExitingPlayMode and again on EnteredEditMode this restores the
    /// <see cref="ScenarioFastMode"/> settings and clears every <see cref="ScenarioHooks"/> member, and
    /// logs one warning naming whatever was still set, so an aborted run, a throwing MCP command or a
    /// manual stop cannot leave forced dice, scripted turns or a filtered log behind.
    /// </summary>
    [InitializeOnLoad]
    public static class ScenarioSessionGuard
    {
        static ScenarioSessionGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.EnteredEditMode)
                CleanUp(change.ToString());
        }

        /// <summary>Restores fast mode and clears the hooks; returns what was still set, or null.</summary>
        public static string CleanUp(string reason)
        {
            string hooks = ScenarioHooks.DescribeSet();
            string mode = ScenarioFastMode.HasSavedSettings ? (ScenarioFastMode.CurrentMode ?? "saved") : null;
            if (hooks == null && mode == null)
                return null;

            ScenarioFastMode.Restore();
            ScenarioHooks.ClearAll();

            string leftover = (hooks != null ? "hooks: " + hooks : "")
                + (hooks != null && mode != null ? "; " : "")
                + (mode != null ? "fast mode: " + mode : "");
            // Logged after Restore, so the filter no longer hides warnings.
            Debug.LogWarning("[ScenarioSessionGuard] " + reason + ": cleared leftover scenario state (" + leftover + ").");
            return leftover;
        }
    }
}
#endif

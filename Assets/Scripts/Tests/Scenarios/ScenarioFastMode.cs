#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Scenarios
{
    /// <summary>
    /// Speeds up Play mode for scenario harness runs without touching game code. Game code never
    /// writes Time.timeScale and paces itself with scaled WaitForSeconds, so a high timeScale plus a
    /// fixed captureFramerate shortens every animation and pause. <see cref="Apply"/> saves the
    /// current settings once; <see cref="Restore"/> puts them back. <see cref="StartProbe"/> measures
    /// how many frames typical waits take under fast mode (docs/TESTING.md 3.3). ScenarioSessionGuard
    /// calls <see cref="Restore"/> when Play mode ends, so an aborted run cannot leave the settings
    /// changed in edit mode. QualitySettings are never written, so nothing can leak into ProjectSettings.
    /// </summary>
    public static class ScenarioFastMode
    {
        private struct Saved
        {
            public float TimeScale;
            public int CaptureFramerate;
            public float MaximumDeltaTime;
            public int TargetFrameRate;
            public bool RunInBackground;
            public LogType FilterLogType;
            public bool DiceLogging;
        }

        private static Saved _saved;
        private static bool _hasSaved;
        private static ProbeBehaviour _probe;
        private static bool _probeOwnsMode;
        private static string _modeBeforeProbe;

        /// <summary>The mode last applied ("fast", "watch"), or null when the settings are the saved ones.</summary>
        public static string CurrentMode { get; private set; }

        /// <summary>True while settings saved by <see cref="Apply"/> are waiting to be restored.</summary>
        public static bool HasSavedSettings => _hasSaved;

        /// <summary>
        /// "fast": timeScale 30, captureFramerate 30, maximumDeltaTime 1, uncapped frame rate,
        /// run in background, logs below Warning filtered, dice logging off.
        /// "watch": real time (timeScale 1, captureFramerate 0) with run in background, and every other
        /// setting back to its saved value, so [Tag] diagnostics and dice logging show again.
        /// Any other mode is refused (returns false, nothing changes).
        /// </summary>
        public static bool Apply(string mode)
        {
            if (mode != "fast" && mode != "watch")
            {
                Debug.LogError("[ScenarioFastMode] Unknown mode '" + mode + "'; use \"fast\" or \"watch\".");
                return false;
            }

            if (!_hasSaved)
            {
                _saved = new Saved
                {
                    TimeScale = Time.timeScale,
                    CaptureFramerate = Time.captureFramerate,
                    MaximumDeltaTime = Time.maximumDeltaTime,
                    TargetFrameRate = Application.targetFrameRate,
                    RunInBackground = Application.runInBackground,
                    FilterLogType = Debug.unityLogger.filterLogType,
                    DiceLogging = DiceService.EnableLogging
                };
                _hasSaved = true;
            }

            if (mode == "watch")
            {
                Time.maximumDeltaTime = _saved.MaximumDeltaTime;
                Application.targetFrameRate = _saved.TargetFrameRate;
                Debug.unityLogger.filterLogType = _saved.FilterLogType;
                DiceService.EnableLogging = _saved.DiceLogging;
                Time.timeScale = 1f;
                Time.captureFramerate = 0;
                Application.runInBackground = true;
                CurrentMode = "watch";
                return true;
            }

            Time.captureFramerate = 30;
            Time.timeScale = 30f;
            Time.maximumDeltaTime = 1f;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            Debug.unityLogger.filterLogType = LogType.Warning;
            DiceService.EnableLogging = false;
            CurrentMode = "fast";
            return true;
        }

        /// <summary>Restores the settings saved by the first <see cref="Apply"/> call.</summary>
        public static void Restore()
        {
            if (!_hasSaved)
                return;

            Time.timeScale = _saved.TimeScale;
            Time.captureFramerate = _saved.CaptureFramerate;
            Time.maximumDeltaTime = _saved.MaximumDeltaTime;
            Application.targetFrameRate = _saved.TargetFrameRate;
            Application.runInBackground = _saved.RunInBackground;
            Debug.unityLogger.filterLogType = _saved.FilterLogType;
            DiceService.EnableLogging = _saved.DiceLogging;
            _hasSaved = false;
            _probeOwnsMode = false;
            CurrentMode = null;
        }

        /// <summary>
        /// Starts the fast-mode probe (Play mode only): applies fast mode, then a DontDestroyOnLoad
        /// object waits WaitForSeconds(0.8f) and WaitForSeconds(0.08f) 20 times each and records the
        /// frames, scaled seconds and real seconds each wait took, and whether the editor had focus.
        /// Poll <see cref="ProbeResult"/>. When a mode was already applied (by the harness) the probe
        /// puts that mode back afterwards instead of restoring the saved settings.
        /// </summary>
        public static string StartProbe()
        {
            if (!Application.isPlaying)
                return "{\"error\":\"Play mode required\"}";
            if (_probe != null && !_probe.Done)
                return "{\"status\":\"already running\"}";
            if (_probe != null)
                Object.Destroy(_probe.gameObject);

            _probeOwnsMode = CurrentMode == null;
            _modeBeforeProbe = CurrentMode;
            if (CurrentMode != "fast")
                Apply("fast");
            var go = new GameObject("ScenarioFastModeProbe");
            Object.DontDestroyOnLoad(go);
            _probe = go.AddComponent<ProbeBehaviour>();
            return "{\"status\":\"started\",\"frame\":" + Time.frameCount + "}";
        }

        /// <summary>
        /// The probe's JSON result. While it runs this returns a progress line; once it is done the
        /// probe object is destroyed and, if the probe applied fast mode itself, the settings are restored.
        /// </summary>
        public static string ProbeResult()
        {
            if (_probe == null)
                return "{\"error\":\"no probe\"}";
            if (!_probe.Done)
                return "{\"status\":\"running\",\"completedWaits\":" + _probe.Samples.Count + ",\"frame\":" + Time.frameCount + "}";

            string json = _probe.ToJson();
            Object.Destroy(_probe.gameObject);
            _probe = null;
            if (_probeOwnsMode)
                Restore();
            else if (_modeBeforeProbe == "watch")
                Apply("watch");
            _probeOwnsMode = false;
            _modeBeforeProbe = null;
            return json;
        }

        private sealed class ProbeBehaviour : MonoBehaviour
        {
            public struct Sample
            {
                public float Wait;
                public int Frames;
                public float Scaled;
                public float Real;
            }

            public readonly List<Sample> Samples = new List<Sample>();
            public bool Done;
            private int _focusedFrames;
            private int _unfocusedFrames;
            private int _startFrame;
            private float _startReal;
            private int _endFrame;
            private float _endReal;

            private void Start()
            {
                StartCoroutine(Run());
            }

            private void Update()
            {
                if (Done)
                    return;
                if (UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                    _focusedFrames++;
                else
                    _unfocusedFrames++;
            }

            private IEnumerator Run()
            {
                _startFrame = Time.frameCount;
                _startReal = Time.realtimeSinceStartup;
                yield return null;

                foreach (float wait in new[] { 0.8f, 0.08f })
                {
                    for (int i = 0; i < 20; i++)
                    {
                        int frame = Time.frameCount;
                        float scaled = Time.time;
                        float real = Time.realtimeSinceStartup;
                        yield return new WaitForSeconds(wait);
                        Samples.Add(new Sample
                        {
                            Wait = wait,
                            Frames = Time.frameCount - frame,
                            Scaled = Time.time - scaled,
                            Real = Time.realtimeSinceStartup - real
                        });
                    }
                }

                _endFrame = Time.frameCount;
                _endReal = Time.realtimeSinceStartup;
                Done = true;
            }

            public string ToJson()
            {
                CultureInfo ci = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                sb.Append("{\"mode\":\"fast\"");
                sb.Append(",\"totalFrames\":").Append(_endFrame - _startFrame);
                sb.Append(",\"totalRealSeconds\":").Append((_endReal - _startReal).ToString("0.###", ci));
                sb.Append(",\"editorFocusedFrames\":").Append(_focusedFrames);
                sb.Append(",\"editorUnfocusedFrames\":").Append(_unfocusedFrames);
                foreach (float wait in new[] { 0.8f, 0.08f })
                {
                    int minF = int.MaxValue, maxF = 0, sumF = 0, n = 0;
                    float sumReal = 0f, sumScaled = 0f;
                    foreach (Sample s in Samples)
                    {
                        if (!Mathf.Approximately(s.Wait, wait))
                            continue;
                        minF = Mathf.Min(minF, s.Frames);
                        maxF = Mathf.Max(maxF, s.Frames);
                        sumF += s.Frames;
                        sumReal += s.Real;
                        sumScaled += s.Scaled;
                        n++;
                    }

                    if (n == 0)
                        continue;
                    string key = wait.ToString("0.##", ci);
                    sb.Append(",\"wait").Append(key).Append("\":{");
                    sb.Append("\"n\":").Append(n);
                    sb.Append(",\"framesMin\":").Append(minF);
                    sb.Append(",\"framesMax\":").Append(maxF);
                    sb.Append(",\"framesAvg\":").Append(((float)sumF / n).ToString("0.##", ci));
                    sb.Append(",\"scaledAvg\":").Append((sumScaled / n).ToString("0.###", ci));
                    sb.Append(",\"realAvgMs\":").Append((sumReal / n * 1000f).ToString("0.#", ci));
                    sb.Append('}');
                }
                sb.Append('}');
                return sb.ToString();
            }
        }
    }
}
#endif

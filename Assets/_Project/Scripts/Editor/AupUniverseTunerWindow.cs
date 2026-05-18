#if UNITY_EDITOR
using Hecton8.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class AupUniverseTunerWindow : EditorWindow
    {
        private const float ThresholdMinMeters = 2000f;
        private const float ThresholdMaxMeters = 8000f;
        private const double CsvPollIntervalSeconds = 1.0d;
        private float _thresholdMeters = 4000f;
        private double _nextCsvPollTime;
        private bool _lastCsvReloadApplied;

        [MenuItem("Hecton8/AUP Universe Tuner")]
        public static void Open()
        {
            GetWindow<AupUniverseTunerWindow>("AUP Universe Tuner");
        }

        private void OnGUI()
        {
            bool hasSnapshot = HectonFloatingOrigin.TryGetAupUniverseTunerSnapshot(out AupUniverseTunerSnapshot snapshot);
            using (new EditorGUI.DisabledScope(!hasSnapshot))
            {
                if (hasSnapshot)
                {
                    _thresholdMeters = snapshot.RebaseThresholdMeters;
                    EditorGUILayout.Vector3Field("Global Position", ToVector3(snapshot.GlobalPosition));
                    EditorGUILayout.Vector3Field("Local Position", ToVector3(snapshot.LocalPosition));
                    EditorGUILayout.FloatField("Sector Size", snapshot.SectorSizeMeters);
                    EditorGUILayout.IntField("Sector Hash", unchecked((int)snapshot.SectorHash));
                    EditorGUILayout.IntField("Shift Sequence", unchecked((int)snapshot.ShiftSequence));
                    EditorGUILayout.IntField("Pending", snapshot.IsOriginShiftPending);
                    EditorGUILayout.IntField("Time Slice Active", snapshot.TimeSliceActive);
                }

                EditorGUI.BeginChangeCheck();
                _thresholdMeters = EditorGUILayout.Slider("Rebase Threshold", _thresholdMeters, ThresholdMinMeters, ThresholdMaxMeters);
                if (EditorGUI.EndChangeCheck())
                    HectonFloatingOrigin.SetRebaseThresholdForTuner(_thresholdMeters);

                if (GUILayout.Button("FORCE REBASE NOW"))
                    HectonFloatingOrigin.ForceRebaseNowForTuner();
            }

            if (EditorApplication.isPlaying)
            {
                double now = EditorApplication.timeSinceStartup;
                if (now >= _nextCsvPollTime)
                {
                    _nextCsvPollTime = now + CsvPollIntervalSeconds;
                    _lastCsvReloadApplied = HectonFloatingOrigin.ReloadAupConstantsForTuner();
                }

                if (GUILayout.Button("RELOAD aup_constants.csv"))
                    _lastCsvReloadApplied = HectonFloatingOrigin.ReloadAupConstantsForTuner();

                EditorGUILayout.Toggle("CSV Reload Applied", _lastCsvReloadApplied);
            }

            if (!hasSnapshot)
                EditorGUILayout.HelpBox("Play Mode AUP vault state is not available.", MessageType.Info);

            if (EditorApplication.isPlaying)
                Repaint();
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
#endif

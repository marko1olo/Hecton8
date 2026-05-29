#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.IO;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Dev
{
    /// <summary>
    /// Dev-only smoke coverage for narrative progression hardening invariants.
    /// </summary>
    public static class NarrativeProgressionSmokeTester
    {
        private const int SearchCycleLimit = 512;
        private const string AudioLogSystemPath = "Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs";
        private const string AtlasSignalSystemPath = "Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs";
        private const string PdaMapTabPath = "Assets/_Project/Scripts/UI/PDAMapTab.cs";

        /// <summary>
        /// Executes deterministic source-level smoke checks for narrative progression hardening.
        /// </summary>
        /// <param name="json">Compact JSON result payload for batch logs and artifact capture.</param>
        /// <returns>True when all smoke checks pass.</returns>
        public static bool Execute(out string json)
        {
            bool ghostDepthGatePass = RunGhostDepthGateSmoke();
            bool ghostDeterminismPass = RunGhostDeterminismSmoke(out int ghostCycleIndex, out float ghostIntensity);
            bool sourceAuditPass = RunSourceAudit(
                out int singletonResidue,
                out int queueTokenCount,
                out int telemetryTokenCount,
                out int decompositionTokenCount);

            bool pass = ghostDepthGatePass &&
                        ghostDeterminismPass &&
                        sourceAuditPass;

            json = "{"
                   + "\"tester\":\"NarrativeProgressionSmokeTester\","
                   + "\"status\":\"" + (pass ? "PASS" : "FAIL") + "\","
                   + "\"ghostDepthGate\":{\"pass\":" + ToJsonBool(ghostDepthGatePass) + "},"
                   + "\"ghostDeterminism\":{\"pass\":" + ToJsonBool(ghostDeterminismPass)
                   + ",\"cycleIndex\":" + ghostCycleIndex
                   + ",\"intensity\":" + ToJsonFixed3(ghostIntensity) + "},"
                   + "\"sourceAudit\":{\"pass\":" + ToJsonBool(sourceAuditPass)
                   + ",\"singletonResidue\":" + singletonResidue
                   + ",\"queueTokenCount\":" + queueTokenCount
                   + ",\"telemetryTokenCount\":" + telemetryTokenCount
                   + ",\"decompositionTokenCount\":" + decompositionTokenCount + "}"
                   + "}";
            return pass;
        }

        private static bool RunGhostDepthGateSmoke()
        {
            return !GhostSignalUtility.TryResolvePing(
                42,
                1f,
                GhostSignalUtility.MinimumDepthMeters - 1f,
                0f,
                out _);
        }

        private static bool RunGhostDeterminismSmoke(out int cycleIndex, out float intensity)
        {
            cycleIndex = -1;
            intensity = 0f;
            for (int i = 0; i < SearchCycleLimit; i++)
            {
                float timeSeconds = (i * GhostSignalUtility.CycleSeconds) + 1f;
                if (!GhostSignalUtility.TryResolvePing(1337, timeSeconds, 900f, 0f, out Vector4 firstPing))
                    continue;

                if (!GhostSignalUtility.TryResolvePing(1337, timeSeconds, 900f, 0f, out Vector4 secondPing))
                    return false;

                cycleIndex = i;
                intensity = firstPing.w;
                return firstPing == secondPing &&
                       firstPing.w > 0f &&
                       firstPing.w <= 1f;
            }

            return false;
        }

        private static bool RunSourceAudit(
            out int singletonResidue,
            out int queueTokenCount,
            out int telemetryTokenCount,
            out int decompositionTokenCount)
        {
            singletonResidue = CountProjectFileContains(AudioLogSystemPath, "AudioLogSystem Instance") +
                               CountProjectFileContains(AtlasSignalSystemPath, "AtlasSignalSystem Instance");
            queueTokenCount = CountProjectFileContains(AudioLogSystemPath, "EnqueuePlayback(logHash);") +
                              CountProjectFileContains(AudioLogSystemPath, "TryStartNextQueuedLog();") +
                              CountProjectFileContains(AudioLogSystemPath, "_QueueFullWarningHash");
            telemetryTokenCount = CountProjectFileContains(AudioLogSystemPath, "PublishPerformanceWarning(_QueueFullWarningHash") +
                                  CountProjectFileContains(AudioLogSystemPath, "PublishPerformanceWarning(_LookupMissWarningHash") +
                                  CountProjectFileContains(AtlasSignalSystemPath, "_EncryptedLogFallbackWarningHash") +
                                  CountProjectFileContains(PdaMapTabPath, "_GhostSignalRejectedWarningHash");
            decompositionTokenCount = CountProjectFileContains(PdaMapTabPath, "GhostSignalUtility.TryResolveCandidate") +
                                      CountProjectFileContains(PdaMapTabPath, "ResolvePlayerDepthMeters") +
                                      CountProjectFileContains(PdaMapTabPath, "TryPublishGhostSignalRejected");

            return singletonResidue == 0 &&
                   queueTokenCount >= 3 &&
                   telemetryTokenCount >= 4 &&
                   decompositionTokenCount >= 3;
        }

        private static int CountProjectFileContains(string relativePath, string token)
        {
            if (string.IsNullOrEmpty(relativePath) || string.IsNullOrEmpty(token))
                return 0;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absolutePath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
                return 0;

            foreach (string line in File.ReadLines(absolutePath))
            {
                if (line.IndexOf(token, System.StringComparison.Ordinal) >= 0)
                    return 1;
            }

            return 0;
        }

        private static string ToJsonBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string ToJsonFixed3(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = 0f;

            int milli = Mathf.Clamp(Mathf.RoundToInt(value * 1000f), 0, 1000);
            int whole = milli / 1000;
            int fraction = milli - whole * 1000;
            return string.Create(5, (whole, fraction), static (buffer, state) =>
            {
                buffer[0] = (char)('0' + state.whole % 10);
                buffer[1] = '.';
                buffer[2] = (char)('0' + (state.fraction / 100) % 10);
                buffer[3] = (char)('0' + (state.fraction / 10) % 10);
                buffer[4] = (char)('0' + state.fraction % 10);
            });
        }
    }
}
#endif

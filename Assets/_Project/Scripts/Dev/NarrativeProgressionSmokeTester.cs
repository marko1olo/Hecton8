#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
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
                   + ",\"intensity\":" + ghostIntensity.ToString("0.000", CultureInfo.InvariantCulture) + "},"
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
            string audioLogSystem = ReadProjectFile(AudioLogSystemPath);
            string atlasSignalSystem = ReadProjectFile(AtlasSignalSystemPath);
            string pdaMapTab = ReadProjectFile(PdaMapTabPath);

            singletonResidue = CountContains(audioLogSystem, "AudioLogSystem Instance") +
                               CountContains(atlasSignalSystem, "AtlasSignalSystem Instance");
            queueTokenCount = CountContains(audioLogSystem, "EnqueuePlayback(logHash);") +
                              CountContains(audioLogSystem, "TryStartNextQueuedLog();") +
                              CountContains(audioLogSystem, "_QueueFullWarningHash");
            telemetryTokenCount = CountContains(audioLogSystem, "PublishPerformanceWarning(_QueueFullWarningHash") +
                                  CountContains(audioLogSystem, "PublishPerformanceWarning(_LookupMissWarningHash") +
                                  CountContains(atlasSignalSystem, "_EncryptedLogFallbackWarningHash") +
                                  CountContains(pdaMapTab, "_GhostSignalRejectedWarningHash");
            decompositionTokenCount = CountContains(pdaMapTab, "GhostSignalUtility.TryResolveCandidate") +
                                      CountContains(pdaMapTab, "ResolvePlayerDepthMeters") +
                                      CountContains(pdaMapTab, "TryPublishGhostSignalRejected");

            return singletonResidue == 0 &&
                   queueTokenCount >= 3 &&
                   telemetryTokenCount >= 4 &&
                   decompositionTokenCount >= 3;
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absolutePath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
        }

        private static int CountContains(string value, string token)
        {
            return string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token) || value.Contains(token)
                ? string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token) ? 0 : 1
                : 0;
        }

        private static string ToJsonBool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
#endif

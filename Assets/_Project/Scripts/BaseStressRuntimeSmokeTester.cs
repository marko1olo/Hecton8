// ============================================================================
// HECTON-8 - BaseStressRuntimeSmokeTester.cs
// Dev-only runtime smoke for habitat stress/power graph voltage relaxation.
// Verifies the scheduled Burst Jacobi solver can brown out a distant load
// through high branch resistance even when total generation exceeds demand.
// ============================================================================

using System.Globalization;
using System.Threading;
using Hecton8.Power;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Base Stress Runtime Smoke Tester")]
    public sealed class BaseStressRuntimeSmokeTester : MonoBehaviour
    {
        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private float startupDelay = 0f;
        [SerializeField] private int maxWaitFrames = 90;
        [SerializeField] private bool verboseLogging = false;

        [Header("Jacobi Probe")]
        [SerializeField] private float generatorWatts = 160f;
        [SerializeField] private float nearDemandWatts = 40f;
        [SerializeField] private float farDemandWatts = 40f;
        [SerializeField] private float nearBranchResistance = 0.5f;
        [SerializeField] private float farBranchResistance = 85f;

        private bool _isRunning;

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!runOnStart || _isRunning)
                return;

            _ = RunSmokePassAsync(destroyCancellationToken);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            maxWaitFrames = math.max(1, maxWaitFrames);
            startupDelay = math.max(0f, startupDelay);
            generatorWatts = math.max(1f, generatorWatts);
            nearDemandWatts = math.max(1f, nearDemandWatts);
            farDemandWatts = math.max(1f, farDemandWatts);
            nearBranchResistance = math.max(0.0001f, nearBranchResistance);
            farBranchResistance = math.max(nearBranchResistance, farBranchResistance);
        }
#endif

        [ContextMenu("Run Base Stress Runtime Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (_isRunning)
                return;

            _ = RunSmokePassAsync(destroyCancellationToken);
        }

        private async Awaitable RunSmokePassAsync(CancellationToken cancellationToken)
        {
            if (_isRunning)
                return;

            _isRunning = true;
            LogisticsNetworkGraph graph = new LogisticsNetworkGraph(4, 4, 2);
            try
            {
                if (startupDelay > 0f)
                    await Awaitable.WaitForSecondsAsync(startupDelay, cancellationToken: cancellationToken);

                if (cancellationToken.IsCancellationRequested || this == null)
                    return;

                BuildVoltageRelaxationProbe(graph);
                graph.ScheduleEvaluation();

                int waitedFrames = 0;
                while (!graph.TryCompleteEvaluation())
                {
                    if (cancellationToken.IsCancellationRequested || this == null)
                        return;

                    waitedFrames++;
                    if (waitedFrames > maxWaitFrames)
                    {
                        LogTimeoutFailure();
                        return;
                    }

                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: cancellationToken);
                }

                LogisticsNetworkGraph.DistributionSummary summary = graph.GetScheduledDistributionSummary();
                bool generationWasEnough = summary.TotalGeneration + 0.01f >= summary.TotalConsumption;
                bool nearLoadPowered = graph.IsConsumerPowered(0);
                bool farLoadPowered = graph.IsConsumerPowered(1);
                bool voltageBrownoutOccurred = nearLoadPowered &&
                                                !farLoadPowered &&
                                                summary.ServedDemand > 0.01f &&
                                                summary.UnservedDemand > 0.01f &&
                                                summary.BrownoutTier != LogisticsBrownoutTier.None;

                if (!generationWasEnough || !voltageBrownoutOccurred)
                {
                    LogVoltageRelaxationFailure(summary, nearLoadPowered, farLoadPowered);
                    return;
                }

                if (verboseLogging)
                    LogVoltageRelaxationPass(summary);
            }
            finally
            {
                graph.Dispose();
                _isRunning = false;
            }
        }

        private void BuildVoltageRelaxationProbe(LogisticsNetworkGraph graph)
        {
            graph.BeginBuild(LogisticsNetworkType.PowerDc, 3, 4, 2);
            int generatorNode = graph.AddNode(1u, generatorWatts, 0.15f, 0, LogisticsNodeFlags.Active, 0);
            int nearLoadNode = graph.AddNode(2u, nearDemandWatts, 0.15f, 1, LogisticsNodeFlags.Active, 0);
            int farLoadNode = graph.AddNode(3u, farDemandWatts, 0.15f, 2, LogisticsNodeFlags.Active, 0);

            graph.AddEdge(generatorNode, nearLoadNode, nearBranchResistance);
            graph.AddEdge(nearLoadNode, generatorNode, nearBranchResistance);
            graph.AddEdge(nearLoadNode, farLoadNode, farBranchResistance);
            graph.AddEdge(farLoadNode, nearLoadNode, farBranchResistance);

            graph.AddProducer(generatorNode, generatorWatts);
            graph.AddConsumer(nearLoadNode, nearDemandWatts, 50, 1, LogisticsConsumerFlags.Essential);
            graph.AddConsumer(farLoadNode, farDemandWatts, 50, 2, LogisticsConsumerFlags.AmbientLighting);
            graph.FinalizeBuild();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogTimeoutFailure()
        {
            Debug.LogError("[BaseStressSmoke] FAIL Jacobi evaluation did not complete inside wait window.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVoltageRelaxationFailure(
            LogisticsNetworkGraph.DistributionSummary summary,
            bool nearLoadPowered,
            bool farLoadPowered)
        {
            Debug.LogError(
                "[BaseStressSmoke] FAIL Jacobi voltage relaxation did not isolate the high-resistance load. " +
                "gen=" + summary.TotalGeneration.ToString("0.###", CultureInfo.InvariantCulture) +
                " demand=" + summary.TotalConsumption.ToString("0.###", CultureInfo.InvariantCulture) +
                " served=" + summary.ServedDemand.ToString("0.###", CultureInfo.InvariantCulture) +
                " unserved=" + summary.UnservedDemand.ToString("0.###", CultureInfo.InvariantCulture) +
                " nearPowered=" + nearLoadPowered +
                " farPowered=" + farLoadPowered +
                " tier=" + summary.BrownoutTier);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogVoltageRelaxationPass(LogisticsNetworkGraph.DistributionSummary summary)
        {
            Hecton8.Core.H8Debug.Log(
                "[BaseStressSmoke] PASS Jacobi voltage relaxation. " +
                "gen=" + summary.TotalGeneration.ToString("0.###", CultureInfo.InvariantCulture) +
                " demand=" + summary.TotalConsumption.ToString("0.###", CultureInfo.InvariantCulture) +
                " served=" + summary.ServedDemand.ToString("0.###", CultureInfo.InvariantCulture) +
                " unserved=" + summary.UnservedDemand.ToString("0.###", CultureInfo.InvariantCulture) +
                " tier=" + summary.BrownoutTier);
        }
    }
}

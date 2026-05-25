using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/World Generative Geology Runtime Smoke Tester")]
    public sealed class WorldGenerativeGeologyRuntimeSmokeTester : MonoBehaviour
    {
        private const string SeamRootName = "__GEOLOGY_SEAM";

        [Header("References")]
        [SerializeField] private WorldProceduralScatterDirector scatterDirector;
        [SerializeField] private WorldGenerativeGeologyIntegrationDirector integrationDirector;
        [SerializeField] private WorldGenerativeGeologySeamExecutionDirector seamExecutionDirector;
        [SerializeField] private WorldGenerativeGeologyTerrainSeamApplier terrainSeamApplier;
        [SerializeField] private WorldGenerativeGeologyVoxelBridgeDirector voxelBridgeDirector;
        [SerializeField] private WorldProceduralStateRegistry proceduralStateRegistry;

        [Header("Execution")]
        [SerializeField] private bool runOnStart;
        [SerializeField] private float startupDelay = 0.75f;
        [SerializeField] private float operationTimeout = 12f;
        [SerializeField] private float settleDelay = 0.2f;
        [SerializeField] private bool preferVoxelBlend = true;
        [SerializeField] private bool preferTerrainBlend = true;
        [SerializeField] private bool testSuppressionAndRestore = true;
        [SerializeField] private bool verboseLogging;

        [Header("Diagnostics")]
        [SerializeField] private int _debugRunCount;
        [SerializeField] private string _debugLastPhase = "Idle";
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private long _debugSelectedRuntimeKey;
        [SerializeField] private string _debugSelectedFamilyId = string.Empty;
        [SerializeField] private bool _debugSelectedRequiresTerrain;
        [SerializeField] private bool _debugSelectedRequiresVoxel;
        [SerializeField] private bool _debugLastPass;

        private bool _isRunning;

        public bool IsRunning => _isRunning;
        public int DebugRunCount => _debugRunCount;
        public string DebugLastPhase => _debugLastPhase;
        public string DebugLastIssue => _debugLastIssue;
        public long DebugSelectedRuntimeKey => _debugSelectedRuntimeKey;
        public bool DebugLastPass => _debugLastPass;

        private void Awake()
        {
            AutoResolve();
        }

        private void Start()
        {
            // Dev helper objects created from editor tooling should only run via explicit command.
            if (gameObject.name.StartsWith("__DEV_", StringComparison.Ordinal))
            {
                runOnStart = false;
                return;
            }

            if (!runOnStart || _isRunning)
                return;

            _ = RunSmokePassAsync(destroyCancellationToken);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AutoResolve();
            operationTimeout = Mathf.Clamp(operationTimeout, 2f, 60f);
            settleDelay = Mathf.Clamp(settleDelay, 0f, 3f);
        }
#endif

        [ContextMenu("Run World Generative Geology Smoke Pass")]
        public void RunFromContextMenu()
        {
            if (_isRunning)
                return;

            _ = RunSmokePassAsync(destroyCancellationToken);
        }

        public void ConfigureForDevRun(
            bool enableVerboseLogging = true,
            bool enableSuppressionRestore = true,
            float timeoutSeconds = 18f,
            float startupDelaySeconds = 0.35f,
            float settleDelaySeconds = 0.2f,
            bool preferVoxel = true,
            bool preferTerrain = true)
        {
            runOnStart = false;
            verboseLogging = enableVerboseLogging;
            testSuppressionAndRestore = enableSuppressionRestore;
            operationTimeout = Mathf.Clamp(timeoutSeconds, 2f, 60f);
            startupDelay = Mathf.Clamp(startupDelaySeconds, 0f, 5f);
            settleDelay = Mathf.Clamp(settleDelaySeconds, 0f, 3f);
            preferVoxelBlend = preferVoxel;
            preferTerrainBlend = preferTerrain;
            AutoResolve();
        }

        public bool TryRunImmediately()
        {
            if (_isRunning)
                return false;

            AutoResolve();
            _ = RunSmokePassAsync(destroyCancellationToken);
            return true;
        }

        public string DescribeStatus()
        {
            string issue = string.IsNullOrWhiteSpace(_debugLastIssue) ? "none" : _debugLastIssue;
            return
                $"running={_isRunning} run={_debugRunCount} phase={_debugLastPhase} " +
                $"runtimeKey={_debugSelectedRuntimeKey} pass={_debugLastPass} issue={issue}";
        }

        private async Awaitable RunSmokePassAsync(CancellationToken cancellationToken)
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _debugRunCount++;
            _debugLastPhase = "Startup";
            _debugLastIssue = string.Empty;
            _debugSelectedRuntimeKey = 0L;
            _debugSelectedFamilyId = string.Empty;
            _debugSelectedRequiresTerrain = false;
            _debugSelectedRequiresVoxel = false;
            _debugLastPass = false;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (startupDelay > 0f)
                    await DelayRealtimeAsync(startupDelay, cancellationToken);

                AutoResolve();
                if (!HasRequiredReferences())
                {
                    Fail($"Missing references refs={DescribeRefs()}");
                    return;
                }

                _debugLastPhase = "Warmup";
                await WaitForConditionAsync(
                    () =>
                    {
                        PulsePipeline();
                        return TrySelectTarget(out _, out _);
                    },
                    "Resolve active generated geology target",
                    cancellationToken);
                if (!_isRunning)
                    return;

                if (!TrySelectTarget(out WorldGenerativeGeologySeamPlan targetPlan, out WorldGenerativeGeologyBinding targetBinding))
                {
                    Fail("No active generated geology target was found after warmup.");
                    return;
                }

                long runtimeKey = targetPlan.runtimeKey;
                _debugSelectedRuntimeKey = runtimeKey;
                _debugSelectedFamilyId = targetPlan.familyId ?? string.Empty;
                _debugSelectedRequiresTerrain = targetPlan.RequiresTerrainBlend;
                _debugSelectedRequiresVoxel = targetPlan.RequiresVoxelBlend;
                LogVerbose($"Selected runtimeKey={runtimeKey} family={_debugSelectedFamilyId} terrain={_debugSelectedRequiresTerrain} voxel={_debugSelectedRequiresVoxel}");

                _debugLastPhase = "Seam";
                await WaitForConditionAsync(
                    () =>
                    {
                        PulsePipeline();
                        return TryFindBinding(runtimeKey, out WorldGenerativeGeologyBinding currentBinding)
                            && currentBinding != null
                            && currentBinding.transform.Find(SeamRootName) != null;
                    },
                    "Wait for seam root",
                    cancellationToken);
                if (!_isRunning)
                    return;

                if (_debugSelectedRequiresVoxel)
                {
                    _debugLastPhase = "Voxel";
                    await WaitForConditionAsync(
                        () =>
                        {
                            PulsePipeline();
                            return FindVoxelRuntime(runtimeKey) != null;
                        },
                        "Wait for voxel seam runtime",
                        cancellationToken);
                    if (!_isRunning)
                        return;
                }

                if (testSuppressionAndRestore)
                {
                    _debugLastPhase = "Suppress";
                    if (proceduralStateRegistry == null)
                    {
                        Fail("Suppression test requested but WorldProceduralStateRegistry is missing.");
                        return;
                    }

                    proceduralStateRegistry.SuppressPlacement(runtimeKey);
                    if (settleDelay > 0f)
                        await DelayRealtimeAsync(settleDelay, cancellationToken);

                    await WaitForConditionAsync(
                        () =>
                        {
                            PulsePipeline();
                            bool bindingGone = !TryFindBinding(runtimeKey, out _);
                            bool planGone = integrationDirector == null || !integrationDirector.TryGetPlan(runtimeKey, out _);
                            bool voxelGone = !_debugSelectedRequiresVoxel || FindVoxelRuntime(runtimeKey) == null;
                            return bindingGone && planGone && voxelGone;
                        },
                        "Wait for suppression teardown",
                        cancellationToken);
                    if (!_isRunning)
                        return;

                    _debugLastPhase = "Restore";
                    proceduralStateRegistry.RestorePlacement(runtimeKey);
                    if (settleDelay > 0f)
                        await DelayRealtimeAsync(settleDelay, cancellationToken);

                    await WaitForConditionAsync(
                        () =>
                        {
                            PulsePipeline();
                            if (!TryFindBinding(runtimeKey, out WorldGenerativeGeologyBinding restoredBinding) || restoredBinding == null)
                                return false;

                            if (restoredBinding.transform.Find(SeamRootName) == null)
                                return false;

                            if (integrationDirector == null || !integrationDirector.TryGetPlan(runtimeKey, out WorldGenerativeGeologySeamPlan restoredPlan))
                                return false;

                            if (restoredPlan.RequiresVoxelBlend && FindVoxelRuntime(runtimeKey) == null)
                                return false;

                            return true;
                        },
                        "Wait for restored geology runtime",
                        cancellationToken);
                    if (!_isRunning)
                        return;
                }

                _debugLastPhase = "Complete";
                _debugLastPass = true;
                Hecton8.Core.H8Debug.Log(
                    $"[GeologySmoke] PASS run={_debugRunCount} runtimeKey={_debugSelectedRuntimeKey} " +
                    $"family={_debugSelectedFamilyId} terrain={_debugSelectedRequiresTerrain} " +
                    $"voxel={_debugSelectedRequiresVoxel} pass={_debugLastPass}");
            }
            catch (OperationCanceledException)
            {
                _debugLastPhase = "Cancelled";
                _debugLastIssue = "Cancellation requested.";
                _debugLastPass = false;
                LogVerbose("World generative geology smoke pass cancelled.");
            }
            catch (Exception exception)
            {
                Fail($"Unhandled exception: {exception.Message}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Awaitable<bool> WaitForConditionAsync(Func<bool> predicate, string label, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, operationTimeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AutoResolve();
                bool passed = false;
                try
                {
                    passed = predicate != null && predicate();
                }
                catch (Exception ex)
                {
                    Fail($"{label} exception: {ex.Message}");
                    return false;
                }

                if (passed)
                    return true;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }

            Fail($"{label} timed out.");
            return false;
        }

        private static async Awaitable DelayRealtimeAsync(float duration, CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, duration);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }
        }

        private void PulsePipeline()
        {
            scatterDirector?.RebuildScatterPreview();
            integrationDirector?.RebuildIntegrationPlans();
            seamExecutionDirector?.ReconcileExecutedSeams();
            terrainSeamApplier?.ReconcileTerrainSeams();
            voxelBridgeDirector?.ReconcileVoxelRequests();
        }

        private bool TrySelectTarget(out WorldGenerativeGeologySeamPlan selectedPlan, out WorldGenerativeGeologyBinding selectedBinding)
        {
            selectedPlan = default;
            selectedBinding = null;

            if (integrationDirector == null)
                return false;

            IReadOnlyList<WorldGenerativeGeologySeamPlan> plans = integrationDirector.ActivePlans;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < plans.Count; i++)
            {
                WorldGenerativeGeologySeamPlan plan = plans[i];
                if (!TryFindBinding(plan.runtimeKey, out WorldGenerativeGeologyBinding binding) || binding == null)
                    continue;

                float score = plan.planWeight;
                if (preferTerrainBlend && plan.RequiresTerrainBlend)
                    score += 0.18f;
                if (preferVoxelBlend && plan.RequiresVoxelBlend)
                    score += 0.22f;
                if (plan.hasTerrainSample)
                    score += 0.05f;

                if (score <= bestScore)
                    continue;

                bestScore = score;
                selectedPlan = plan;
                selectedBinding = binding;
            }

            return selectedBinding != null && selectedPlan.runtimeKey != 0L;
        }

        private static bool TryFindBinding(long runtimeKey, out WorldGenerativeGeologyBinding binding)
        {
            binding = null;
            if (runtimeKey == 0L)
                return false;

            if (WorldGenerativeGeologyBinding.TryGetActiveBinding(runtimeKey, out binding))
                return true;

            return false;
        }

        private static WorldGenerativeGeologyVoxelRuntime FindVoxelRuntime(long runtimeKey)
        {
            if (runtimeKey == 0L)
                return null;

            return WorldGenerativeGeologyVoxelRuntime.TryGetActiveRuntime(runtimeKey, out WorldGenerativeGeologyVoxelRuntime runtime)
                ? runtime
                : null;
        }

        private bool HasRequiredReferences()
        {
            return scatterDirector != null
                && integrationDirector != null
                && seamExecutionDirector != null
                && terrainSeamApplier != null
                && voxelBridgeDirector != null;
        }

        private void AutoResolve()
        {
            if (scatterDirector == null)
                scatterDirector = WorldProceduralScatterDirector.ActiveRuntimeInstance;
            if (integrationDirector == null)
                integrationDirector = WorldGenerativeGeologyIntegrationDirector.ActiveRuntimeInstance;
            if (seamExecutionDirector == null)
                seamExecutionDirector = WorldGenerativeGeologySeamExecutionDirector.ActiveRuntimeInstance;
            if (terrainSeamApplier == null)
                terrainSeamApplier = WorldGenerativeGeologyTerrainSeamApplier.ActiveRuntimeInstance;
            if (voxelBridgeDirector == null)
                voxelBridgeDirector = WorldGenerativeGeologyVoxelBridgeDirector.ActiveRuntimeInstance;
            if (proceduralStateRegistry == null)
                proceduralStateRegistry = WorldProceduralStateRegistry.ActiveRuntimeInstance;
        }

        private string DescribeRefs()
        {
            return $"scatter={(scatterDirector != null ? "Y" : "N")} " +
                   $"integration={(integrationDirector != null ? "Y" : "N")} " +
                   $"seam={(seamExecutionDirector != null ? "Y" : "N")} " +
                   $"terrain={(terrainSeamApplier != null ? "Y" : "N")} " +
                   $"voxel={(voxelBridgeDirector != null ? "Y" : "N")} " +
                   $"state={(proceduralStateRegistry != null ? "Y" : "N")}";
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
                Hecton8.Core.H8Debug.Log($"[GeologySmoke] {message}");
        }

        private void Fail(string issue)
        {
            string failedPhase = _debugLastPhase;
            _debugLastPhase = "Failed";
            _debugLastIssue = issue ?? "Unknown issue";
            _debugLastPass = false;
            Debug.LogWarning(
                $"[GeologySmoke] FAIL run={_debugRunCount} phase={failedPhase} " +
                $"runtimeKey={_debugSelectedRuntimeKey} issue={_debugLastIssue} pass={_debugLastPass}");
            _isRunning = false;
        }
    }
}

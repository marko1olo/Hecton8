using Hecton8.Crafting;
using Hecton8.Core;
using Hecton8.Core.Memory;
using UnityEngine;

namespace Hecton8.Debugging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Debug/Crafting Runtime Smoke Tester")]
    public sealed class CraftingRuntimeSmokeTester : MonoBehaviour
    {
        private const long BatchFallbackVaultBytes = 16L * 1024L * 1024L;

        [SerializeField] private bool runOnStart;
        [SerializeField, Min(0.001f)] private float taskDurationSeconds = 1f;
        [SerializeField, Range(0.05f, 1f)] private float thermalThrottleMultiplier = 1f;

#pragma warning disable CS0414
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private float _debugFirstMockProgress;
        [SerializeField] private float _debugLastMockProgress;
        [SerializeField] private float _debugAverageMockProgress;
#pragma warning restore CS0414

        private void Start()
        {
            if (runOnStart)
                RunSmokePass();
        }

        [ContextMenu("Run Fabrication Vault Smoke Pass")]
        public void RunSmokePass()
        {
            _debugLastPass = RunFabricationVaultSmoke(
                taskDurationSeconds,
                thermalThrottleMultiplier,
                out _debugFirstMockProgress,
                out _debugLastMockProgress,
                out _debugAverageMockProgress);
        }

        public static bool RunFabricationVaultSmoke(
            float durationSeconds,
            float thermalThrottleMultiplier,
            out float firstMockProgress,
            out float lastMockProgress,
            out float averageMockProgress)
        {
            firstMockProgress = 0f;
            lastMockProgress = 0f;
            averageMockProgress = 0f;
            _ = durationSeconds;
            _ = thermalThrottleMultiplier;

            if (!EnsureBatchVaultRegistered())
                return false;

            if (!FabricationAssemblerRuntime.EnsureRuntime() ||
                !FabricationAssemblerRuntime.GenerateMockFabricationJobs())
                return false;

            if (!FabricationAssemblerRuntime.TryReadSnapshot(0, out FabricationRuntimeSnapshot firstSnapshot))
                return false;

            int lastSlot = FabricationAssemblerRuntime.MockFabricationJobCount - 1;
            if (!FabricationAssemblerRuntime.TryReadSnapshot(lastSlot, out FabricationRuntimeSnapshot lastSnapshot))
                return false;

            if (!FabricationAssemblerRuntime.TryGetEditorStats(out FabricationEditorStats stats))
                return false;

            firstMockProgress = Mathf.Clamp01(firstSnapshot.Progress01);
            lastMockProgress = Mathf.Clamp01(lastSnapshot.Progress01);
            averageMockProgress = Mathf.Clamp01(stats.AverageProgress01);

            return stats.ActiveJobs >= FabricationAssemblerRuntime.MockFabricationJobCount &&
                   firstSnapshot.TargetPrefabHash != 0u &&
                   lastSnapshot.TargetPrefabHash != 0u &&
                   averageMockProgress >= 0f &&
                   averageMockProgress <= 1f;
        }

        private static bool EnsureBatchVaultRegistered()
        {
            if (GlobalRegistry.DataVault != null)
                return true;

            if (!Application.isBatchMode)
                return false;

            GlobalDataVault vault = GlobalDataVault.Create(64, BatchFallbackVaultBytes);
            GlobalRegistry.RegisterDataVault(vault);
            return GlobalRegistry.DataVault != null;
        }
    }
}

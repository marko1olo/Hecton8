// ============================================================================
// HECTON-8 - VisualBudgetSmokeTester.cs
// Dev-only smoke coverage for MX350 visual memory budgets.
// ============================================================================

using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using Hecton8.Optimization;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Visual Budget Smoke Tester")]
    public sealed class VisualBudgetSmokeTester : MonoBehaviour
    {
        private const long BytesPerMegabyte = 1024L * 1024L;
        private const int Mx350HardBudgetMb = 1800;
        private const float VramGuardRatio = 0.90f;
        private const long RtDepthBudgetBytes = 320L * BytesPerMegabyte;
        private const long PostFxBudgetBytes = 96L * BytesPerMegabyte;
        private const long UiRtBudgetBytes = 180L * BytesPerMegabyte;
        private const long VisorRtBudgetBytes = 64L * BytesPerMegabyte;

        [Header("Execution")]
        [Tooltip("Run the visual budget smoke pass once when the component starts.")]
        [SerializeField] private bool runOnStart = false;
        [Tooltip("Emit a pass log with sampled graphics and RenderTexture budget values.")]
        [SerializeField] private bool verboseLogging = false;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private int _debugRunCount;
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private float _debugGraphicsDriverMemoryMb;
        [SerializeField] private float _debugGraphicsBudgetMb;
        [SerializeField] private float _debugTrackedRenderTextureMemoryMb;
        [SerializeField] private float _debugVisorRenderTextureMemoryMb;
        [SerializeField] private float _debugPostFxRenderTextureMemoryMb;
        [SerializeField] private float _debugUiRenderTextureMemoryMb;
#pragma warning restore CS0414

        // COLD ALLOC: List<RenderTextureAllocationRecord>[64] — visual budget Visor RT query — owner: VisualBudgetSmokeTester
        private readonly List<RenderTextureAllocationRecord> _visorRtRecords = new List<RenderTextureAllocationRecord>(64);

        // COLD ALLOC: List<RenderTextureAllocationRecord>[64] — visual budget Camera RT query — owner: VisualBudgetSmokeTester
        private readonly List<RenderTextureAllocationRecord> _cameraRtRecords = new List<RenderTextureAllocationRecord>(64);

        // COLD ALLOC: List<RenderTextureAllocationRecord>[64] — visual budget PostFX RT query — owner: VisualBudgetSmokeTester
        private readonly List<RenderTextureAllocationRecord> _postFxRtRecords = new List<RenderTextureAllocationRecord>(64);

        // COLD ALLOC: List<RenderTextureAllocationRecord>[64] — visual budget UI RT query — owner: VisualBudgetSmokeTester
        private readonly List<RenderTextureAllocationRecord> _uiRtRecords = new List<RenderTextureAllocationRecord>(64);

        // COLD ALLOC: List<RenderTextureAllocationRecord>[64] — visual budget uncategorized RT query — owner: VisualBudgetSmokeTester
        private readonly List<RenderTextureAllocationRecord> _otherRtRecords = new List<RenderTextureAllocationRecord>(64);

        // COLD ALLOC: StringBuilder[512] — visual budget smoke report — owner: VisualBudgetSmokeTester
        private readonly StringBuilder _reportBuilder = new StringBuilder(512);

        private void Start()
        {
            if (!runOnStart)
                return;

            RunSmokePass();
        }

        [ContextMenu("Run Visual Budget Smoke Pass")]
        public void RunFromContextMenu()
        {
            RunSmokePass();
        }

        /// <summary>
        /// Runs a one-shot visual memory budget smoke pass against MX350 limits.
        /// </summary>
        /// <returns>True when all sampled visual memory buckets remain under budget.</returns>
        public bool RunSmokePass()
        {
            _debugRunCount++;
            _debugLastPass = false;
            _debugLastIssue = string.Empty;

            long graphicsDriverBytes = ReadGraphicsDriverMemoryBytes();
            long graphicsBudgetBytes = ResolveGraphicsBudgetBytes();
            long trackedRtBytes = CaptureRenderTextureBudgets(
                out long visorRtBytes,
                out long postFxRtBytes,
                out long uiRtBytes);

            _debugGraphicsDriverMemoryMb = BytesToMegabytes(graphicsDriverBytes);
            _debugGraphicsBudgetMb = BytesToMegabytes(graphicsBudgetBytes);
            _debugTrackedRenderTextureMemoryMb = BytesToMegabytes(trackedRtBytes);
            _debugVisorRenderTextureMemoryMb = BytesToMegabytes(visorRtBytes);
            _debugPostFxRenderTextureMemoryMb = BytesToMegabytes(postFxRtBytes);
            _debugUiRenderTextureMemoryMb = BytesToMegabytes(uiRtBytes);

            if (graphicsDriverBytes > 0L && graphicsDriverBytes > graphicsBudgetBytes)
                return Fail("graphics-driver-vram-hard-ceiling");

            if (graphicsDriverBytes > 0L && graphicsDriverBytes > (long)(graphicsBudgetBytes * VramGuardRatio))
                return Fail("graphics-driver-vram-guard-ratio");

            if (trackedRtBytes > RtDepthBudgetBytes)
                return Fail("render-texture-depth-budget");

            if (visorRtBytes > VisorRtBudgetBytes)
                return Fail("visor-rt-budget");

            if (postFxRtBytes > PostFxBudgetBytes)
                return Fail("postfx-rt-budget");

            if (uiRtBytes > UiRtBudgetBytes)
                return Fail("ui-rt-budget");

            _debugLastPass = true;
            LogPass(graphicsDriverBytes, graphicsBudgetBytes, trackedRtBytes, visorRtBytes, postFxRtBytes, uiRtBytes);
            return true;
        }

        private long CaptureRenderTextureBudgets(
            out long visorRtBytes,
            out long postFxRtBytes,
            out long uiRtBytes)
        {
            visorRtBytes = 0L;
            postFxRtBytes = 0L;
            uiRtBytes = 0L;

            RenderTextureLifecycleTracker tracker = GlobalRegistry.RenderTextureLifecycle;
            if (tracker == null)
            {
                VisorRTManager visorManager = GlobalRegistry.VisorRT;
                PostFXRTManager postFxManager = GlobalRegistry.PostFXRT;
                visorRtBytes = visorManager != null ? visorManager.VisorRTMemoryBytes : 0L;
                postFxRtBytes = postFxManager != null ? postFxManager.PostFXRTMemoryBytes : 0L;
                return visorRtBytes + postFxRtBytes;
            }

            tracker.GetAllocationsByCategory("Visor", _visorRtRecords);
            tracker.GetAllocationsByCategory("Camera", _cameraRtRecords);
            tracker.GetAllocationsByCategory("PostFX", _postFxRtRecords);
            tracker.GetAllocationsByCategory("UI", _uiRtRecords);
            tracker.GetAllocationsByCategory("Other", _otherRtRecords);

            visorRtBytes = SumActiveRenderTextureBytes(_visorRtRecords);
            long cameraRtBytes = SumActiveRenderTextureBytes(_cameraRtRecords);
            postFxRtBytes = SumActiveRenderTextureBytes(_postFxRtRecords);
            uiRtBytes = SumActiveRenderTextureBytes(_uiRtRecords);
            long otherRtBytes = SumActiveRenderTextureBytes(_otherRtRecords);

            return visorRtBytes + cameraRtBytes + postFxRtBytes + uiRtBytes + otherRtBytes;
        }

        private static long SumActiveRenderTextureBytes(List<RenderTextureAllocationRecord> records)
        {
            if (records == null)
                return 0L;

            long total = 0L;
            for (int i = 0; i < records.Count; i++)
            {
                RenderTextureAllocationRecord record = records[i];
                if (!record.IsDisposed)
                    total += record.MemoryBytes;
            }

            return total;
        }

        private static long ReadGraphicsDriverMemoryBytes()
        {
            long graphicsDriverBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
            return graphicsDriverBytes > 0L ? graphicsDriverBytes : 0L;
        }

        private static long ResolveGraphicsBudgetBytes()
        {
            int reportedGraphicsMemoryMb = Mathf.Max(0, SystemInfo.graphicsMemorySize);
            int budgetMb = reportedGraphicsMemoryMb > 0
                ? Mathf.Min(reportedGraphicsMemoryMb, Mx350HardBudgetMb)
                : Mx350HardBudgetMb;
            return (long)budgetMb * BytesPerMegabyte;
        }

        private bool Fail(string issue)
        {
            _debugLastIssue = issue;
            _debugLastPass = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _reportBuilder.Clear();
            _reportBuilder.Append("[VisualBudgetSmoke] FAIL issue=").Append(issue)
                .Append(" graphics=").Append(_debugGraphicsDriverMemoryMb.ToString("0.0")).Append("/")
                .Append(_debugGraphicsBudgetMb.ToString("0.0")).Append("MB")
                .Append(" rt=").Append(_debugTrackedRenderTextureMemoryMb.ToString("0.0")).Append("MB")
                .Append(" visor=").Append(_debugVisorRenderTextureMemoryMb.ToString("0.0")).Append("MB")
                .Append(" postfx=").Append(_debugPostFxRenderTextureMemoryMb.ToString("0.0")).Append("MB")
                .Append(" ui=").Append(_debugUiRenderTextureMemoryMb.ToString("0.0")).Append("MB");
            Debug.LogError(_reportBuilder.ToString(), this);
#endif
            return false;
        }

        private void LogPass(
            long graphicsDriverBytes,
            long graphicsBudgetBytes,
            long trackedRtBytes,
            long visorRtBytes,
            long postFxRtBytes,
            long uiRtBytes)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!verboseLogging)
                return;

            _reportBuilder.Clear();
            _reportBuilder.Append("[VisualBudgetSmoke] PASS graphics=")
                .Append(BytesToMegabytes(graphicsDriverBytes).ToString("0.0")).Append("/")
                .Append(BytesToMegabytes(graphicsBudgetBytes).ToString("0.0")).Append("MB")
                .Append(" rt=").Append(BytesToMegabytes(trackedRtBytes).ToString("0.0")).Append("MB")
                .Append(" visor=").Append(BytesToMegabytes(visorRtBytes).ToString("0.0")).Append("MB")
                .Append(" postfx=").Append(BytesToMegabytes(postFxRtBytes).ToString("0.0")).Append("MB")
                .Append(" ui=").Append(BytesToMegabytes(uiRtBytes).ToString("0.0")).Append("MB");
            Debug.Log(_reportBuilder.ToString(), this);
#endif
        }

        private static float BytesToMegabytes(long bytes)
        {
            return bytes > 0L ? bytes / (float)BytesPerMegabyte : 0f;
        }
    }
}

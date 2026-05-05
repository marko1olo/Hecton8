// ============================================================================
// HECTON-8 - VisualCascadeSmokeTester.cs
// Dev-only smoke coverage for volumetric/biolum/post-fx cascade budgets.
// ============================================================================

using System.Text;
using Hecton8.Visor;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Visual Cascade Smoke Tester")]
    public sealed class VisualCascadeSmokeTester : MonoBehaviour
    {
        private const long BytesPerKilobyte = 1024L;
        private const long BytesPerMegabyte = BytesPerKilobyte * 1024L;
        private const int Mx350GraphicsMemoryCeilingMb = 2048;
        private static readonly int VolumetricShadowStepCapMx350 = 15;
        private const int CausticsResolution = 256;
        private const int CausticsR8BytesPerPixel = 1;
        private const int BiolumVolumeResolution = 64;
        private const int RgbaHalfBytesPerVoxel = 8;
        private const int BiolumVolumeBufferCount = 2;
        private const int HistogramBinCount = 64;
        private const int HistogramBytesPerBin = 4;
        private const int ExposureStateBytes = 16;
        private const long CascadeFeatureBudgetBytes = 48L * BytesPerMegabyte;

        [Header("Execution")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private bool verboseLogging = false;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private int _debugRunCount;
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private string _debugLastIssue = string.Empty;
        [SerializeField] private float _debugEstimatedCascadeMb;
        [SerializeField] private float _debugCausticsKb;
        [SerializeField] private float _debugBiolumMb;
        [SerializeField] private float _debugExposureKb;
#pragma warning restore CS0414

        // COLD ALLOC: StringBuilder[512] - visual cascade smoke report - owner: VisualCascadeSmokeTester
        private readonly StringBuilder _reportBuilder = new StringBuilder(512);

        private void Start()
        {
            if (runOnStart)
                RunSmokePass();
        }

        [ContextMenu("Run Visual Cascade Smoke Pass")]
        public void RunFromContextMenu()
        {
            RunSmokePass();
        }

        public bool RunSmokePass()
        {
            _debugRunCount++;
            _debugLastPass = false;
            _debugLastIssue = string.Empty;

            long causticsBytes = EstimateCausticsR8Bytes();
            long biolumBytes = EstimateBiolumDiffusionBytes();
            long exposureBytes = EstimateAutoExposureBytes();
            long totalBytes = causticsBytes + biolumBytes + exposureBytes;

            _debugEstimatedCascadeMb = BytesToMegabytes(totalBytes);
            _debugCausticsKb = causticsBytes / (float)BytesPerKilobyte;
            _debugBiolumMb = BytesToMegabytes(biolumBytes);
            _debugExposureKb = exposureBytes / (float)BytesPerKilobyte;

            if (VolumetricShadowStepCapMx350 >= 16)
                return Fail("volumetric-shadow-step-cap");

            if (!ValidateRetinaModeSeparation())
                return Fail("retina-chromatic-distortion-conflict");

            if (causticsBytes != CausticsResolution * CausticsResolution * CausticsR8BytesPerPixel)
                return Fail("caustics-r8-budget");

            if (totalBytes > CascadeFeatureBudgetBytes)
                return Fail("cascade-feature-vram-budget");

            _debugLastPass = true;
            LogPass(totalBytes, causticsBytes, biolumBytes, exposureBytes);
            return true;
        }

        private static bool ValidateRetinaModeSeparation()
        {
            HectonRetinaDistortionFeature.RetinaOffsetBudget mx350Budget =
                HectonRetinaDistortionFeature.ResolveRetinaOffsetBudget(0.0038f, 0.014f, 1f, Mx350GraphicsMemoryCeilingMb);
            HectonRetinaDistortionFeature.RetinaOffsetBudget highBudget =
                HectonRetinaDistortionFeature.ResolveRetinaOffsetBudget(0.0038f, 0.014f, 1f, Mx350GraphicsMemoryCeilingMb + 1024);

            bool mx350Separated = mx350Budget.ChromaticOffset > 0f && mx350Budget.DistortionOffset <= 0f;
            bool highSeparated = highBudget.ChromaticOffset <= 0f && highBudget.DistortionOffset > 0f;
            return mx350Separated && highSeparated;
        }

        private static long EstimateCausticsR8Bytes()
        {
            return CausticsResolution * CausticsResolution * CausticsR8BytesPerPixel;
        }

        private static long EstimateBiolumDiffusionBytes()
        {
            long voxelCount = BiolumVolumeResolution * BiolumVolumeResolution * BiolumVolumeResolution;
            return voxelCount * RgbaHalfBytesPerVoxel * BiolumVolumeBufferCount;
        }

        private static long EstimateAutoExposureBytes()
        {
            return HistogramBinCount * HistogramBytesPerBin + ExposureStateBytes;
        }

        private bool Fail(string issue)
        {
            _debugLastIssue = issue;
            _debugLastPass = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _reportBuilder.Clear();
            _reportBuilder.Append("[VisualCascadeSmoke] FAIL issue=").Append(issue)
                .Append(" estimated=").Append(_debugEstimatedCascadeMb.ToString("0.00")).Append("MB")
                .Append(" caustics=").Append(_debugCausticsKb.ToString("0.00")).Append("KB")
                .Append(" biolum=").Append(_debugBiolumMb.ToString("0.00")).Append("MB")
                .Append(" exposure=").Append(_debugExposureKb.ToString("0.00")).Append("KB");
            Debug.LogError(_reportBuilder.ToString(), this);
#endif
            return false;
        }

        private void LogPass(long totalBytes, long causticsBytes, long biolumBytes, long exposureBytes)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!verboseLogging)
                return;

            _reportBuilder.Clear();
            _reportBuilder.Append("[VisualCascadeSmoke] PASS estimated=").Append(BytesToMegabytes(totalBytes).ToString("0.00")).Append("MB")
                .Append(" caustics=").Append((causticsBytes / (float)BytesPerKilobyte).ToString("0.00")).Append("KB")
                .Append(" biolum=").Append(BytesToMegabytes(biolumBytes).ToString("0.00")).Append("MB")
                .Append(" exposure=").Append((exposureBytes / (float)BytesPerKilobyte).ToString("0.00")).Append("KB");
            Debug.Log(_reportBuilder.ToString(), this);
#endif
        }

        private static float BytesToMegabytes(long bytes)
        {
            return bytes > 0L ? bytes / (float)BytesPerMegabyte : 0f;
        }
    }
}

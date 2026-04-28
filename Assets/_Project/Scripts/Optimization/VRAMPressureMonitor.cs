using Hecton8.Core;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Applies graduated VRAM pressure responses against the MX350 hard ceiling.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8007)]
    public sealed class VRAMPressureMonitor : MonoBehaviour, ITickable, IUpdatable
    {
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const float DefaultRestoreFraction = 1.40f / 1.80f;
        private const int DefaultSampleIntervalFrames = 90;
        private const float RamWarningFraction = 0.75f;
        private const float RamEmergencyFraction = 0.90f;

        private static VRAMPressureMonitor _instance;

        [Header("VRAM Pressure Thresholds")]
        [Tooltip("Preventive mip downgrade threshold against the 1.8 GB MX350 ceiling.")]
        [SerializeField, Range(0.5f, 1f)] private float warningVramFraction = 0.85f;

        [Tooltip("Emergency eviction threshold against the 1.8 GB MX350 ceiling.")]
        [SerializeField, Range(0.5f, 1f)] private float emergencyVramFraction = 0.95f;

        [Tooltip("Recovery threshold that restores the baseline mip residency.")]
        [SerializeField, Range(0.25f, 1f)] private float restoreVramFraction = DefaultRestoreFraction;

        [Tooltip("Frames between pressure samples. 90 frames is the streaming mandate cadence.")]
        [SerializeField, Range(1, 180)] private int sampleIntervalFrames = DefaultSampleIntervalFrames;

        [Tooltip("Maximum number of forced evictions performed in a single emergency pass.")]
        [SerializeField, Range(1, 8)] private int maxEmergencyEvictionsPerPass = 4;

        private bool _registeredTick;
        private int _framesUntilSample;
        private int _baselineMipLimit;
        private int _activeMipLimit;

        internal static VRAMPressureMonitor Instance => _instance;
        internal bool HasSample { get; private set; }
        internal float VramPressureFactor { get; private set; }
        internal float RamPressureFactor { get; private set; }
        internal float PressureFactor { get; private set; }
        internal float LastStreamingMipBudgetMb { get; private set; }
        internal int EmergencyEvictionCount { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _baselineMipLimit = QualitySettings.globalTextureMipmapLimit;
            _activeMipLimit = _baselineMipLimit;
            _framesUntilSample = Mathf.Max(1, sampleIntervalFrames);
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (_instance == this)
                _instance = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            _framesUntilSample--;
            if (_framesUntilSample > 0)
                return;

            _framesUntilSample = Mathf.Max(1, sampleIntervalFrames);
            SampleAndRespond();
        }

        internal void ForceImmediateSampleAndResponse()
        {
            _framesUntilSample = Mathf.Max(1, sampleIntervalFrames);
            SampleAndRespond();
        }

        private void TryRegister()
        {
            if (_registeredTick)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = false;
        }

        private void SampleAndRespond()
        {
            VRAMMonitor monitor = VRAMMonitor.Instance;
            if (monitor != null)
                monitor.SlowTick();

            AssetLifecycleGovernor governor = AssetLifecycleGovernor.Instance;
            long maxSystemRamBytes = (long)SystemInfo.systemMemorySize * 1024L * 1024L;
            long currentReservedBytes = Profiler.GetTotalReservedMemoryLong();
            if (governor != null)
                currentReservedBytes += governor.NativeHeapEstimateBytes;

            VRAMBudgetThresholds thresholds = VRAMBudgetThresholds.Default;
            long vramBudgetBytes = thresholds.TotalVRAMBudgetBytes;
            long usedVramBytes = monitor != null ? monitor.TotalVRAMBytes : 0L;

            VramPressureFactor = vramBudgetBytes > 0L ? usedVramBytes / (float)vramBudgetBytes : 0f;
            RamPressureFactor = maxSystemRamBytes > 0L ? currentReservedBytes / (float)maxSystemRamBytes : 0f;
            PressureFactor = Mathf.Max(VramPressureFactor, RamPressureFactor);
            HasSample = true;

            ApplyStreamingMipBudget(monitor, thresholds);
            ApplyMipBias();
            RunPressureEviction(governor, monitor);
        }

        private void ApplyStreamingMipBudget(VRAMMonitor monitor, VRAMBudgetThresholds thresholds)
        {
            long nonTextureBytes = 0L;
            if (monitor != null)
            {
                nonTextureBytes = monitor.TotalVRAMBytes - monitor.TextureMemoryBytes;
                if (nonTextureBytes < 0L)
                    nonTextureBytes = 0L;
            }

            long streamingBudgetBytes = thresholds.TotalVRAMBudgetBytes - nonTextureBytes;
            if (streamingBudgetBytes < 128L * 1024L * 1024L)
                streamingBudgetBytes = 128L * 1024L * 1024L;

            LastStreamingMipBudgetMb = streamingBudgetBytes / BytesPerMegabyte;
            QualitySettings.streamingMipmapsMemoryBudget = LastStreamingMipBudgetMb;
        }

        private void ApplyMipBias()
        {
            int targetMipLimit = _activeMipLimit;

            if (VramPressureFactor >= emergencyVramFraction)
                targetMipLimit = Mathf.Max(_baselineMipLimit, 2);
            else if (VramPressureFactor >= warningVramFraction)
                targetMipLimit = Mathf.Max(_baselineMipLimit, 1);
            else if (VramPressureFactor <= restoreVramFraction)
                targetMipLimit = _baselineMipLimit;

            if (targetMipLimit == _activeMipLimit)
                return;

            QualitySettings.globalTextureMipmapLimit = targetMipLimit;
            _activeMipLimit = targetMipLimit;
        }

        private void RunPressureEviction(AssetLifecycleGovernor governor, VRAMMonitor monitor)
        {
            EmergencyEvictionCount = 0;
            if (governor == null)
                return;

            if (VramPressureFactor >= emergencyVramFraction || RamPressureFactor >= RamEmergencyFraction)
            {
                governor.ForceDrainPendingReleaseQueue();
                EmergencyEvictionCount = governor.EvictLowestPriorityUnusedAssets(
                    maxEmergencyEvictionsPerPass,
                    AssetPriorityTier.Tier4MidRange);

                if (monitor != null && monitor.RenderTextureBudgetUtilization >= 1f)
                {
                    RenderTexturePool pool = RenderTexturePool.Instance;
                    if (pool != null)
                        pool.ClearAllPools();
                }

                return;
            }

            if (VramPressureFactor >= warningVramFraction || RamPressureFactor >= RamWarningFraction)
            {
                governor.ForceDrainPendingReleaseQueue();
                governor.EvictLowestPriorityUnusedAssets(1, AssetPriorityTier.Tier5DistantHlod);
            }
        }
    }
}

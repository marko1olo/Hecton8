using Hecton8.Core;
using Hecton8.SaveSystem;
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
        private const float DefaultWarningVramFraction = 1600f / 1800f;
        private const float DefaultRestoreFraction = 1.40f / 1.80f;
        private const int DefaultSampleIntervalFrames = 90;
        private const float RamWarningFraction = 0.75f;
        private const float RamEmergencyFraction = 0.90f;
        private const long SoftVramPressureBytes = 1600L * 1024L * 1024L;
        private const long ForcedHalfResolutionVramBytes = 1400L * 1024L * 1024L;
        private const long VRForcedHalfResolutionVramBytes = 1200L * 1024L * 1024L;
        private const long RedZoneVramPressureBytes = 1800L * 1024L * 1024L;
        private const long RestoreFullResolutionVramBytes = 1200L * 1024L * 1024L;
        private const long VRRestoreFullResolutionVramBytes = 900L * 1024L * 1024L;
        private const long LODAggressionVramBytes = 1700L * 1024L * 1024L;
        private const float LODAggressionMultiplier = 0.5f;
        private const long MinimumHardwareHeadroomBytes = 200L * 1024L * 1024L;
        private const int WorldPrefabEvictionIdleFrames = 180;

        [Header("VRAM Pressure Thresholds")]
        [Tooltip("Preventive mip downgrade threshold: 1.6 GB against the 1.8 GB MX350 ceiling. Forced half-res begins at 1.4 GB, or 1.2 GB while XR is active.")]
        [SerializeField, Range(0.5f, 1f)] private float warningVramFraction = DefaultWarningVramFraction;

        [Tooltip("Emergency eviction threshold against the 1.8 GB MX350 ceiling.")]
        [SerializeField, Range(0.5f, 1f)] private float emergencyVramFraction = 0.95f;

        [Tooltip("Recovery threshold that restores the baseline mip residency. XR recovery is hard-gated at 0.9 GB to prevent mip thrash.")]
        [SerializeField, Range(0.25f, 1f)] private float restoreVramFraction = DefaultRestoreFraction;

        [Tooltip("Frames between pressure samples. 90 frames is the streaming mandate cadence.")]
        [SerializeField, Range(1, 180)] private int sampleIntervalFrames = DefaultSampleIntervalFrames;

        [Tooltip("Maximum number of forced evictions performed in a single emergency pass.")]
        [SerializeField, Range(1, 8)] private int maxEmergencyEvictionsPerPass = 4;

        private bool _registeredTick;
        private bool _registeredService;
        private int _framesUntilSample;
        private int _baselineMipLimit;
        private int _activeMipLimit;
        private float _baselineLodBias;
        private bool _lodAggressionActive;

        internal static float BrgLodDistanceScalar { get; private set; } = 1f;

        internal bool HasSample { get; private set; }
        internal float VramPressureFactor { get; private set; }
        internal float RamPressureFactor { get; private set; }
        internal float PressureFactor { get; private set; }
        internal float LastStreamingMipBudgetMb { get; private set; }
        internal long LastUsedVramBytes { get; private set; }
        internal int EmergencyEvictionCount { get; private set; }

        private void Awake()
        {
            BrgLodDistanceScalar = 1f;
            _baselineMipLimit = QualitySettings.globalTextureMipmapLimit;
            _activeMipLimit = _baselineMipLimit;
            _baselineLodBias = QualitySettings.lodBias;
            _framesUntilSample = Mathf.Max(1, sampleIntervalFrames);
        }

        private void OnEnable()
        {
            if (TryRegisterService())
                TryRegister();
        }

        private void Start()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            if (_lodAggressionActive)
                BrgLodDistanceScalar = 1f;

            TryUnregister();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            if (_lodAggressionActive)
                BrgLodDistanceScalar = 1f;

            TryUnregister();
            TryUnregisterService();
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
            if (_registeredTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.VRAMPressure, this))
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = GlobalRegistry.Updatables.Contains(this);
        }

        private bool TryRegisterService()
        {
            if (_registeredService)
                return true;
            if (!Application.isPlaying)
                return false;

            VRAMPressureMonitor registered = GlobalRegistry.VRAMPressure;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterVRAMPressureRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.VRAMPressure, this);
            return _registeredService;
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = false;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterVRAMPressureRuntime(this);
            _registeredService = false;
        }

        private void SampleAndRespond()
        {
            VRAMMonitor monitor = GlobalRegistry.VRAMMonitor;
            if (monitor != null)
                monitor.SlowTick();

            AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
            long maxSystemRamBytes = (long)SystemInfo.systemMemorySize * 1024L * 1024L;
            long currentReservedBytes = Profiler.GetTotalReservedMemoryLong();
            if (governor != null)
                currentReservedBytes += governor.NativeHeapEstimateBytes;

            VRAMBudgetThresholds thresholds = VRAMBudgetThresholds.Default;
            long vramBudgetBytes = thresholds.TotalVRAMBudgetBytes;
            long usedVramBytes = monitor != null ? monitor.TotalVRAMBytes : 0L;
            LastUsedVramBytes = usedVramBytes;

            VramPressureFactor = vramBudgetBytes > 0L ? usedVramBytes / (float)vramBudgetBytes : 0f;
            RamPressureFactor = maxSystemRamBytes > 0L ? currentReservedBytes / (float)maxSystemRamBytes : 0f;
            PressureFactor = Mathf.Max(VramPressureFactor, RamPressureFactor);
            HasSample = true;

            ApplyStreamingMipBudget(monitor, thresholds);
            ApplyMipBias();
            ApplyLodAggression();
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
            bool softVramPressure = IsSoftVramPressureActive();
            long forcedMipThresholdBytes = ResolveForcedMipDropThresholdBytes();
            long restoreMipThresholdBytes = ResolveFullResolutionRestoreThresholdBytes();
            bool allowFractionRestore = !HectonXRRuntimeState.IsXRActive;

            if (LastUsedVramBytes >= forcedMipThresholdBytes)
                targetMipLimit = Mathf.Max(_baselineMipLimit, 1);
            else if (LastUsedVramBytes <= restoreMipThresholdBytes)
                targetMipLimit = _baselineMipLimit;
            else if (softVramPressure)
                targetMipLimit = Mathf.Max(_baselineMipLimit, 1);
            else if (allowFractionRestore && VramPressureFactor <= restoreVramFraction)
                targetMipLimit = _baselineMipLimit;

            if (targetMipLimit == _activeMipLimit)
                return;

            QualitySettings.globalTextureMipmapLimit = targetMipLimit;
#pragma warning disable CS0618
            QualitySettings.masterTextureLimit = targetMipLimit;
#pragma warning restore CS0618
            _activeMipLimit = targetMipLimit;
        }

        private void ApplyLodAggression()
        {
            bool shouldCollapseLods = LastUsedVramBytes >= LODAggressionVramBytes;
            bool shouldRestoreLods = LastUsedVramBytes <= ResolveFullResolutionRestoreThresholdBytes() ||
                                     (!HectonXRRuntimeState.IsXRActive && VramPressureFactor <= restoreVramFraction);

            if (shouldCollapseLods)
            {
                float targetLodBias = Mathf.Max(0.05f, _baselineLodBias * LODAggressionMultiplier);
                if (!_lodAggressionActive || !Mathf.Approximately(QualitySettings.lodBias, targetLodBias))
                    QualitySettings.lodBias = targetLodBias;

                BrgLodDistanceScalar = LODAggressionMultiplier;
                _lodAggressionActive = true;
                return;
            }

            if (!_lodAggressionActive || !shouldRestoreLods)
                return;

            QualitySettings.lodBias = _baselineLodBias;
            BrgLodDistanceScalar = 1f;
            _lodAggressionActive = false;
        }

        private bool IsSoftVramPressureActive()
        {
            return LastUsedVramBytes >= SoftVramPressureBytes || VramPressureFactor >= warningVramFraction;
        }

        private static long ResolveForcedMipDropThresholdBytes()
        {
            return HectonXRRuntimeState.IsXRActive
                ? VRForcedHalfResolutionVramBytes
                : ForcedHalfResolutionVramBytes;
        }

        private static long ResolveFullResolutionRestoreThresholdBytes()
        {
            return HectonXRRuntimeState.IsXRActive
                ? VRRestoreFullResolutionVramBytes
                : RestoreFullResolutionVramBytes;
        }

        private void RunPressureEviction(AssetLifecycleGovernor governor, VRAMMonitor monitor)
        {
            EmergencyEvictionCount = 0;
            ItemCatalog itemCatalog = GlobalRegistry.PlayerInventory != null && GlobalRegistry.PlayerInventory.Inventory != null
                ? GlobalRegistry.PlayerInventory.Inventory.ItemCatalog
                : null;

            long hardwareHeadroomBytes = long.MaxValue;
            if (monitor != null)
            {
                hardwareHeadroomBytes = ((long)SystemInfo.graphicsMemorySize * 1024L * 1024L) - monitor.TotalVRAMBytes;
                if (hardwareHeadroomBytes < 0L)
                    hardwareHeadroomBytes = 0L;
            }

            bool redZoneVramPressure = LastUsedVramBytes >= RedZoneVramPressureBytes || VramPressureFactor >= 1f;
            if (redZoneVramPressure || VramPressureFactor >= emergencyVramFraction || RamPressureFactor >= RamEmergencyFraction)
            {
                if (redZoneVramPressure)
                    SystemDispatcher.RequestVisualStaticGlitch();

                if (governor != null)
                {
                    governor.ForceDrainPendingReleaseQueue();
                    EmergencyEvictionCount = governor.EvictLowestPriorityUnusedAssets(
                        maxEmergencyEvictionsPerPass,
                        AssetPriorityTier.Tier4MidRange);
                }

                if (redZoneVramPressure || (monitor != null && monitor.RenderTextureBudgetUtilization >= 1f))
                {
                    RenderTexturePool pool = GlobalRegistry.RenderTexturePool;
                    if (pool != null)
                        pool.ClearAllPools();
                }

                if (itemCatalog != null && hardwareHeadroomBytes < MinimumHardwareHeadroomBytes)
                {
                    EmergencyEvictionCount += itemCatalog.EvictLeastRecentlyUsedWorldPrefabs(
                        maxEmergencyEvictionsPerPass,
                        WorldPrefabEvictionIdleFrames);
                }

                return;
            }

            if (IsSoftVramPressureActive() || RamPressureFactor >= RamWarningFraction)
            {
                if (governor != null)
                {
                    governor.ForceDrainPendingReleaseQueue();
                    governor.EvictLowestPriorityUnusedAssets(1, AssetPriorityTier.Tier5DistantHlod);
                }

                if (itemCatalog != null && hardwareHeadroomBytes < MinimumHardwareHeadroomBytes)
                    itemCatalog.EvictLeastRecentlyUsedWorldPrefabs(1, WorldPrefabEvictionIdleFrames);
            }
        }
    }
}

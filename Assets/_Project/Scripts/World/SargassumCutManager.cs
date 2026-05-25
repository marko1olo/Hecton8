using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Bootstrap;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Owns the global sargassum cut mask render texture and stamps world-space cuts from active player tools.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-103)]
    public sealed class SargassumCutManager : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, ISargassumCutWriteService
    {
        private const int StampCommandCapacity = 16;
        private const int StampThreadGroupSize = 8;
        private const float DefaultWaterLevel = 4900f;
        private const int RecentStampCapacity = 16;
        private const int DamageVolumeStampCapacity = 16;
        private const int DamageVolumeThreadGroupSize = 4;
        private const int DamageVolumeResolutionStep = 16;
        private const int DamageVolumeDepthStep = 8;
        private const float DamageVolumeQualityHysteresis = 0.08f;
        private const float DamageVolumeEnergyEpsilon = 0.0001f;
        private const float PlasmaCutThermalEventLifetimeSeconds = 1.5f;
        private const float PlasmaCutThermalDeltaCelsius = 220f;
        private const SystemID VaultOwnerSystemId = SystemID.WorldSargassum;
        private const BufferID StampCommandsBufferId = BufferID.SargassumCutStampCommands;
        private const BufferID DamageVolumeStampCommandsBufferId = BufferID.SargassumCutDamageVolumeStampCommands;
#if UNITY_EDITOR
        private const string StampComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_SargassumCutMask.compute";
        private const string DamageVolumeComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_TerrainDamageVolume.compute";
#endif

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct StampCommand
        {
            [FieldOffset(0)]
            public Vector4 UvRadiusStrength;
        }

        private struct RecentCutStamp
        {
            public Vector3 PositionWS;
            public float RadiusWS;
            public float Strength;
            public float RemainingLifetime;
        }

        private struct RecentCutHeatStamp
        {
            public Vector3 PositionWS;
            public float RadiusWS;
            public float Strength;
            public float StartTime;
            public float Lifetime;
        }

        private struct PendingDebrisBurst
        {
            public Vector3 PositionWS;
            public Vector3 DirectionWS;
            public float CutStrength;
            public float BubbleWeight;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct DamageVolumeStampCommand
        {
            [FieldOffset(0)]
            public Vector4 PositionRadius;
            [FieldOffset(16)]
            public Vector4 StrengthPadding;
        }

        private static readonly int _CutMaskTextureId = Shader.PropertyToID("_SargassumCutMaskRT");
        private static readonly int _CutMaskWorldRectId = Shader.PropertyToID("_SargassumCutMaskWorldRect");
        private static readonly int _CutMaskActiveId = Shader.PropertyToID("_SargassumCutMaskActive");
        private static readonly int _StampUvRadiusStrengthId = Shader.PropertyToID("_StampUvRadiusStrength");
        private static readonly int _ScrollUvOffsetId = Shader.PropertyToID("_ScrollUvOffset");
        private static readonly int _RecoveryId = Shader.PropertyToID("_Recovery");
        private static readonly int _MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int _ResultId = Shader.PropertyToID("_Result");
        private static readonly int _StampCommandsId = Shader.PropertyToID("_StampCommands");
        private static readonly int _StampCountId = Shader.PropertyToID("_StampCount");
        private static readonly int _TexelSizeId = Shader.PropertyToID("_TexelSize");
        private static readonly int _RecentCutHeatCountId = Shader.PropertyToID("_HectonRecentCutHeatCount");
        private static readonly int _RecentCutHeatPositionRadiusId = Shader.PropertyToID("_HectonRecentCutHeatPositionRadius");
        private static readonly int _RecentCutHeatStrengthTimeId = Shader.PropertyToID("_HectonRecentCutHeatStrengthTime");
        private static readonly int _DamageVolumeTextureId = Shader.PropertyToID("_HectonDamageVolumeTex");
        private static readonly int _DamageVolumeActiveId = Shader.PropertyToID("_HectonDamageVolumeActive");
        private static readonly int _DamageVolumeWorldMinId = Shader.PropertyToID("_HectonDamageVolumeWorldMin");
        private static readonly int _DamageVolumeInvSizeId = Shader.PropertyToID("_HectonDamageVolumeInvSize");
        private static readonly int _DamageVolumeSourceId = Shader.PropertyToID("_HectonDamageVolumeSource");
        private static readonly int _DamageVolumeResultId = Shader.PropertyToID("_HectonDamageVolumeResult");
        private static readonly int _DamageVolumeStampCommandsId = Shader.PropertyToID("_HectonDamageVolumeStampCommands");
        private static readonly int _DamageVolumeStampCountId = Shader.PropertyToID("_HectonDamageVolumeStampCount");
        private static readonly int _DamageVolumeRecoveryId = Shader.PropertyToID("_HectonDamageVolumeRecovery");
        private static readonly int _DamageVolumeWorldMinParamId = Shader.PropertyToID("_HectonDamageVolumeWorldMinParam");
        private static readonly int _DamageVolumeInvSizeParamId = Shader.PropertyToID("_HectonDamageVolumeInvSizeParam");
        private static readonly int _DamageVolumeResolutionId = Shader.PropertyToID("_HectonDamageVolumeResolution");

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Primary runtime source for the active floating sargassum residency bounds.")]
        private HectonMapMagicVegetationBridge mapMagicVegetationBridge;

        [SerializeField]
        [Tooltip("Optional direct player override used when bootstrap has not resolved the runtime player yet.")]
        private Transform playerTransformOverride;

        [SerializeField]
        [Tooltip("Optional direct PlayerToolManager override for isolated validation scenes.")]
        private PlayerToolManager playerToolManagerOverride;

        [SerializeField]
        [Tooltip("Optional hidden blit shader override used to stamp into the global cut mask render texture.")]
        private Shader stampShaderOverride;

        [SerializeField]
        [Tooltip("Optional compute shader override used to update the global cut mask in one dispatch per frame.")]
        private ComputeShader stampComputeOverride;

        [SerializeField]
        [Tooltip("Optional compute shader override used to stamp the 3D thermal damage volume consumed by terrain shaders.")]
        private ComputeShader damageVolumeComputeOverride;

        [SerializeField]
        [Tooltip("Optional debris burst emitter triggered whenever a global cut stamp is written.")]
        private SargassumDebrisParticleSystem debrisParticleSystem;

        [Header("── Cut Mask RT ──────────────────")]
        [SerializeField, Range(512, 2048)]
        [Tooltip("Resolution of the square world-space cut mask render texture.")]
        private int maskResolution = 512;

        [SerializeField, Min(128f)]
        [Tooltip("Fixed square world coverage represented by the player-centered cut mask render texture.")]
        private float minimumMaskWorldSize = 384f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Quantization step multiplier applied to the cut-mask pixel size while following the player.")]
        private float centerSnapPixelStride = 1f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Recovery speed in mask-value units per second. Lower values keep cuts open for longer.")]
        private float recoveryPerSecond = 0.15f;

        [Header("── Damage Volume ──────────────────")]
        [SerializeField, Range(32, 128)]
        [Tooltip("Resolution of the cubic XZ damage volume texture sampled by terrain shaders.")]
        private int damageVolumeResolution = 64;

        [SerializeField, Range(16, 96)]
        [Tooltip("Resolution of the vertical damage volume slices.")]
        private int damageVolumeDepth = 32;

        [SerializeField, Min(8f)]
        [Tooltip("Vertical world-space height covered by the 3D terrain damage volume.")]
        private float damageVolumeHeight = 24f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Per-update recovery applied to the 3D damage volume.")]
        private float damageVolumeRecoveryPerSecond = 0.04f;

        [Header("── Scooter Cutting ──────────────────")]
        [SerializeField, Range(0.1f, 6f)]
        [Tooltip("World-space radius carved by an active Manta scooter propeller.")]
        private float scooterCutRadius = 2.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized strength written into the cut mask by an active Manta scooter.")]
        private float scooterCutStrength = 0.95f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Forward offset applied to the active Manta scooter transform before stamping.")]
        private float scooterForwardOffset = 0.75f;

        [Header("── Knife Cutting ──────────────────")]
        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("World-space radius carved by a knife swing.")]
        private float knifeCutRadius = 0.85f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized strength written into the cut mask by a knife swing.")]
        private float knifeCutStrength = 0.72f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Forward offset applied to the knife transform before stamping.")]
        private float knifeForwardOffset = 1.05f;

        [SerializeField, Range(0.02f, 0.5f)]
        [Tooltip("Cooldown between consecutive knife cut stamps while primary attack input is held.")]
        private float knifeStampCooldown = 0.14f;

        [SerializeField, Range(0.1f, 6f)]
        [Tooltip("How long a recent cut stamp remains queryable by debris and fauna after it was written into the scrolling mask.")]
        private float recentCutLifetime = 1.4f;

        [SerializeField, Range(5f, 10f)]
        [Tooltip("How long freshly cut rock scars stay thermally active before they settle into the cold charred mask.")]
        private float shaderScarLifetime = 8f;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField]
        [Tooltip("Approximate remaining cut energy. When it decays to zero, the manager stops blitting until a new cut arrives.")]
        private float _debugMaskEnergy;

        [SerializeField]
        [Tooltip("Current world-space square represented by the active cut mask render texture.")]
        private Vector4 _debugMaskWorldRect;

        [SerializeField]
        [Tooltip("Last world-space stamp position written into the mask.")]
        private Vector3 _debugLastStampPosition;

        [SerializeField]
        [Tooltip("Number of stamps emitted during the latest frame tick.")]
        private int _debugLastStampCount;

        private Transform _playerTransform;
        private IPlayerRuntimeContext _playerContext;
        private IInputService _inputService;
        private PlayerToolManager _playerToolManager;
        private RenderTexture _maskRead;
        private RenderTexture _maskWrite;
        private ComputeShader _stampCompute;
        private int _stampKernel = -1;
        private GraphicsBuffer _stampCommandBufferA;
        private GraphicsBuffer _stampCommandBufferB;
        private GraphicsBuffer _activeStampCommandBuffer;
        private RenderTexture _damageVolumeRead;
        private RenderTexture _damageVolumeWrite;
        private ComputeShader _damageVolumeCompute;
        private int _damageVolumeKernel = -1;
        private GraphicsBuffer _damageVolumeStampCommandBufferA;
        private GraphicsBuffer _damageVolumeStampCommandBufferB;
        private GraphicsBuffer _activeDamageVolumeStampCommandBuffer;
        private bool _serviceRegistered;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _registeredHotSwap;
        private bool _globalsDirty;
        private bool _qualityResourceRefreshRequested;
        private bool _pendingHeatRefresh;
        private int _stampCommandUploadIndex;
        private int _damageVolumeStampCommandUploadIndex;
        private bool _maskClearRequested;
        private bool _damageVolumeClearRequested;
        private float _pendingDamageVolumeDeltaTime;
        private float _maskEnergy;
        private float _knifeStampCooldownRemaining;
        private Vector4 _maskWorldRect;
        private float _maskWorldSize;
        private Vector2 _maskCenterXZ;
        private Vector2 _pendingScrollUv;
        private float _pendingRecovery;
        private int _queuedStampCount;
        private int _stampOverflowCoalesceCount;
        private int _lastMaskDispatchFrame = -1;
        private int _maskRuntimeResolution;
        private int _damageVolumeRuntimeResolution;
        private int _damageVolumeRuntimeDepth;
        private float _resourceQualityWeight = -1f;
        private VaultGenerationHandle<StampCommand> _queuedStampCommandsHandle;
        private VaultGenerationHandle<DamageVolumeStampCommand> _queuedDamageVolumeStampCommandsHandle;
        private IDataVault _dataVault;
        private int _queuedDamageVolumeStampCount;
        private int _damageVolumeStampOverflowCoalesceCount;
        private Vector3 _damageVolumeWorldMin;
        private Vector3 _damageVolumeWorldSize;
        private int _lastDamageVolumeDispatchFrame = -1;
        private float _damageVolumeEnergy;
        // COLD ALLOC: RecentCutStamp[16] - CPU mirror of the newest cut stamps for zero-readback gameplay queries - owner: SargassumCutManager
        private readonly RecentCutStamp[] _recentCutStamps = new RecentCutStamp[RecentStampCapacity];
        // COLD ALLOC: RecentCutHeatStamp[16] - timestamped cut heat stamps for voxel rock thermal scarring - owner: SargassumCutManager
        private readonly RecentCutHeatStamp[] _recentCutHeatStamps = new RecentCutHeatStamp[RecentStampCapacity];
        // COLD ALLOC: Vector4[16] - packed recent-cut thermal positions/radii published to shaders - owner: SargassumCutManager
        private readonly Vector4[] _recentCutHeatPositionRadius = new Vector4[RecentStampCapacity];
        // COLD ALLOC: Vector4[16] - packed recent-cut thermal strength/start/lifetime payload published to shaders - owner: SargassumCutManager
        private readonly Vector4[] _recentCutHeatStrengthTime = new Vector4[RecentStampCapacity];
        // COLD ALLOC: PendingDebrisBurst[16] - visual particle bursts flushed only in LateFrameTick - owner: SargassumCutManager
        private readonly PendingDebrisBurst[] _pendingDebrisBursts = new PendingDebrisBurst[StampCommandCapacity];
        private int _pendingDebrisBurstCount;
        private int _recentCutHeatCount;
        private int _publishedRecentCutHeatCount = -1;
        private bool _recentCutHeatDirty;

        /// <summary>
        /// Active registry-owned instance.
        /// </summary>
        public static SargassumCutManager Instance => GlobalRegistry.SargassumCut;

        /// <summary>
        /// Current cut mask texture used by shaders and GPU fauna.
        /// </summary>
        public RenderTexture CutMaskTexture => _maskRead;

        /// <summary>
        /// World-space cut mask rect encoded as minX, minZ, invSizeX, invSizeZ.
        /// </summary>
        public Vector4 CutMaskWorldRect => _maskWorldRect;

        /// <summary>
        /// True when the scrolling cut mask owns a valid world rect and source texture.
        /// </summary>
        public bool HasActiveMask => _maskRead != null && _maskWorldSize > 0f;

        /// <summary>
        /// Returns the active cut mask texture and world rect.
        /// </summary>
        /// <param name="texture">Current cut mask texture.</param>
        /// <param name="worldRect">Current world rect encoded as minX, minZ, invSizeX, invSizeZ.</param>
        /// <returns>True when the mask is valid.</returns>
        public bool TryGetCutMask(out RenderTexture texture, out Vector4 worldRect)
        {
            texture = _maskRead;
            worldRect = _maskWorldRect;
            return HasActiveMask;
        }

        /// <summary>
        /// Samples the CPU mirror of recent cut stamps at the given world position.
        /// </summary>
        /// <param name="positionWS">World-space position to test.</param>
        /// <param name="radiusWS">Additional test radius around the position.</param>
        /// <param name="cut01">Resolved normalized recent-cut weight.</param>
        /// <returns>True when the position intersects at least one recent cut stamp.</returns>
        public bool SampleRecentCut01(Vector3 positionWS, float radiusWS, out float cut01)
        {
            cut01 = 0f;
            float effectiveRadius = Mathf.Max(0f, radiusWS);
            bool hasCut = false;

            for (int i = 0; i < RecentStampCapacity; i++)
            {
                RecentCutStamp stamp = _recentCutStamps[i];
                if (stamp.RemainingLifetime <= 0f || stamp.Strength <= 0f || stamp.RadiusWS <= 0f)
                    continue;

                float combinedRadius = stamp.RadiusWS + effectiveRadius;
                float deltaX = positionWS.x - stamp.PositionWS.x;
                float deltaZ = positionWS.z - stamp.PositionWS.z;
                float combinedRadiusSq = combinedRadius * combinedRadius;
                float distanceSq = deltaX * deltaX + deltaZ * deltaZ;
                if (distanceSq > combinedRadiusSq)
                    continue;

                float radialFalloff = 1f - distanceSq / Mathf.Max(combinedRadiusSq, 0.000001f);
                float temporalFalloff = Mathf.Clamp01(stamp.RemainingLifetime / Mathf.Max(0.01f, recentCutLifetime));
                float influence = stamp.Strength * radialFalloff * temporalFalloff;
                if (influence <= cut01)
                    continue;

                cut01 = influence;
                hasCut = true;
            }

            return hasCut;
        }

        /// <summary>
        /// Estimates recent cut coverage around a world-space point by accumulating the weighted area of overlapping cut stamps.
        /// </summary>
        /// <param name="positionWS">World-space position to test.</param>
        /// <param name="radiusWS">Additional query radius around the sampled point.</param>
        /// <param name="accumulatedAreaWS">Weighted cut area in world-space square meters.</param>
        /// <param name="strongestCut01">Strongest overlapping normalized cut weight.</param>
        /// <returns>True when at least one recent cut overlaps the query.</returns>
        public bool SampleRecentCutArea(Vector3 positionWS, float radiusWS, out float accumulatedAreaWS, out float strongestCut01)
        {
            accumulatedAreaWS = 0f;
            strongestCut01 = 0f;
            float effectiveRadius = Mathf.Max(0f, radiusWS);
            bool hasCut = false;

            for (int i = 0; i < RecentStampCapacity; i++)
            {
                RecentCutStamp stamp = _recentCutStamps[i];
                if (stamp.RemainingLifetime <= 0f || stamp.Strength <= 0f || stamp.RadiusWS <= 0f)
                    continue;

                float combinedRadius = stamp.RadiusWS + effectiveRadius;
                float deltaX = positionWS.x - stamp.PositionWS.x;
                float deltaZ = positionWS.z - stamp.PositionWS.z;
                float combinedRadiusSq = combinedRadius * combinedRadius;
                float distanceSq = deltaX * deltaX + deltaZ * deltaZ;
                if (distanceSq > combinedRadiusSq)
                    continue;

                float radialFalloff = 1f - distanceSq / Mathf.Max(combinedRadiusSq, 0.000001f);
                float temporalFalloff = Mathf.Clamp01(stamp.RemainingLifetime / Mathf.Max(0.01f, recentCutLifetime));
                float influence = stamp.Strength * radialFalloff * temporalFalloff;
                if (influence <= 0f)
                    continue;

                accumulatedAreaWS += Mathf.PI * stamp.RadiusWS * stamp.RadiusWS * influence;
                if (influence > strongestCut01)
                    strongestCut01 = influence;

                hasCut = true;
            }

            return hasCut;
        }

        /// <summary>
        /// Writes a non-player cut stamp into the global cut mask and CPU mirror.
        /// </summary>
        /// <param name="positionWS">World-space center of the cut.</param>
        /// <param name="radiusWS">World-space cut radius.</param>
        /// <param name="strength">Normalized mask strength.</param>
        /// <param name="directionWS">Preferred outward burst direction for debris.</param>
        /// <param name="bubbleWeight">Bubble weighting forwarded to the debris burst emitter.</param>
        /// <returns>True when the world-space cut was written into the active scrolling mask.</returns>
        public bool RegisterExternalCut(Vector3 positionWS, float radiusWS, float strength, Vector3 directionWS, float bubbleWeight = 1f)
        {
            float clampedRadius = Mathf.Max(0.05f, radiusWS);
            float clampedStrength = Mathf.Clamp01(strength);
            if (clampedStrength <= 0f)
                return false;

            ResolveDependencies();
            RefreshMaskWorldRect();

            bool wroteMask = false;
            if (_maskRead != null && _maskWrite != null && _stampCompute != null && _activeStampCommandBuffer != null && IsInsideMaskWorldRect(positionWS))
            {
                ExecuteStampPass(positionWS, clampedRadius, clampedStrength, 0f);
                _maskEnergy = Mathf.Max(_maskEnergy, clampedStrength);
                QueueGlobalPublish();
                wroteMask = true;
            }

            QueueDamageVolumeStamp(positionWS, clampedRadius, clampedStrength);
            QueueDamageVolumeVisualSync(0f);

            RegisterRecentCutStamp(positionWS, clampedRadius, clampedStrength);
            RegisterRecentCutHeatStamp(positionWS, clampedRadius, clampedStrength);

            Vector3 burstDirection = NormalizeVector3Fast(directionWS, Vector3.up);
            QueueDebrisBurst(positionWS, burstDirection, clampedStrength, bubbleWeight);
            _debugLastStampPosition = positionWS;
            _debugLastStampCount++;
            return wroteMask;
        }

        bool ISargassumCutWriteService.TryRegisterExternalCut(Vector3 positionWS, float radiusWS, float strength01, Vector3 directionWS, float bubbleWeight)
        {
            return RegisterExternalCut(positionWS, radiusWS, strength01, directionWS, bubbleWeight);
        }

        private void Awake()
        {
            SargassumCutManager registered = GlobalRegistry.SargassumCut;
            if (registered != null && registered != this)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SargassumCutManager] Duplicate instance detected. Destroying the newer component.", this);
#endif
                Destroy(this);
                return;
            }

            TryRegisterService();
            maskResolution = Mathf.Clamp(maskResolution, 512, 2048);
            centerSnapPixelStride = Mathf.Max(0.1f, centerSnapPixelStride);
            damageVolumeResolution = Mathf.Clamp(damageVolumeResolution, 32, 128);
            damageVolumeDepth = Mathf.Clamp(damageVolumeDepth, 16, 96);
            damageVolumeHeight = Mathf.Max(8f, damageVolumeHeight);
            damageVolumeRecoveryPerSecond = Mathf.Clamp01(damageVolumeRecoveryPerSecond);
            InitializeRuntimeResourceBudgets(force: true);
            CacheRegistryServicesCold();
            CreateResources();
            PublishGlobals();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CacheRegistryServicesCold();
            CreateResources();
            PublishGlobals();
            TryRegisterService();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregisterService();
            TryUnregister();
            TryUnregisterHotSwapListener();
            Shader.SetGlobalFloat(_CutMaskActiveId, 0f);
            Shader.SetGlobalFloat(_DamageVolumeActiveId, 0f);
            PublishRecentCutHeatCount(0);
        }

        private void OnDestroy()
        {
            TryUnregisterService();
            TryUnregister();
            TryUnregisterHotSwapListener();
            ReleaseResources();
        }

        /// <summary>
        /// Updates the cut mask by decaying existing values and stamping the currently active player tools.
        /// </summary>
        /// <param name="deltaTime">Gameplay frame delta time.</param>
        public void Tick(float deltaTime)
        {
            ResolveDependencies();
            if (_maskRead == null || _maskWrite == null || _stampCompute == null || _activeStampCommandBuffer == null)
                return;

            DecayRecentCutStamps(deltaTime);

            RefreshMaskWorldRect();
            _debugLastStampCount = 0;

            if (_knifeStampCooldownRemaining > 0f)
            {
                _knifeStampCooldownRemaining -= deltaTime;
                if (_knifeStampCooldownRemaining < 0f)
                    _knifeStampCooldownRemaining = 0f;
            }

            bool needsRecoveryPass = _maskEnergy > 0.0001f;
            bool wrotePass = false;
            float strongestStampThisFrame = 0f;

            if (TryResolveScooterStamp(out Vector3 scooterStampPosition, out Vector3 scooterStampDirection))
            {
                ExecuteStampPass(scooterStampPosition, scooterCutRadius, scooterCutStrength, !wrotePass ? deltaTime : 0f);
                RegisterRecentCutStamp(scooterStampPosition, scooterCutRadius, scooterCutStrength);
                RegisterRecentCutHeatStamp(scooterStampPosition, scooterCutRadius, scooterCutStrength);
                QueueDebrisBurst(scooterStampPosition, scooterStampDirection, scooterCutStrength, 1f);
                wrotePass = true;
                _debugLastStampCount++;
                strongestStampThisFrame = Mathf.Max(strongestStampThisFrame, scooterCutStrength);
            }

            if (TryResolveKnifeStamp(out Vector3 knifeStampPosition, out Vector3 knifeStampDirection))
            {
                ExecuteStampPass(knifeStampPosition, knifeCutRadius, knifeCutStrength, !wrotePass ? deltaTime : 0f);
                RegisterRecentCutStamp(knifeStampPosition, knifeCutRadius, knifeCutStrength);
                RegisterRecentCutHeatStamp(knifeStampPosition, knifeCutRadius, knifeCutStrength);
                QueueDebrisBurst(knifeStampPosition, knifeStampDirection, knifeCutStrength, 0.45f);
                wrotePass = true;
                _debugLastStampCount++;
                _knifeStampCooldownRemaining = knifeStampCooldown;
                strongestStampThisFrame = Mathf.Max(strongestStampThisFrame, knifeCutStrength);
            }

            if (!wrotePass && needsRecoveryPass)
                ExecuteStampPass(Vector3.zero, 0f, 0f, deltaTime);

            bool hasDamageVolumeWork = _queuedDamageVolumeStampCount > 0 || _damageVolumeEnergy > DamageVolumeEnergyEpsilon;
            if (!wrotePass && !needsRecoveryPass && !HasPendingMaskUpdate() && !hasDamageVolumeWork)
                return;

            float recoveredEnergy = Mathf.Max(0f, _maskEnergy - recoveryPerSecond * deltaTime);
            _maskEnergy = wrotePass ? Mathf.Max(recoveredEnergy, strongestStampThisFrame) : recoveredEnergy;
            _debugMaskEnergy = _maskEnergy;
            if (hasDamageVolumeWork)
                QueueDamageVolumeVisualSync(deltaTime);
            QueueGlobalPublish();
        }

        /// <summary>
        /// Re-evaluates residency bounds and recenters the cut mask if the streamed sargassum field moved significantly.
        /// </summary>
        public void SlowTick()
        {
            ResolveDependencies();
            _qualityResourceRefreshRequested = true;
            RefreshMaskWorldRect();
            QueueDamageVolumeVisualSync(0f);
            QueueGlobalPublish(forceHeatRefresh: true);
        }

        /// <summary>
        /// Flushes sargassum cut-mask and damage-volume shader state after simulation writes are complete.
        /// </summary>
        public void LateFrameTick()
        {
            bool hasVisualWork =
                _maskClearRequested ||
                _damageVolumeClearRequested ||
                HasPendingMaskUpdate() ||
                _queuedDamageVolumeStampCount > 0 ||
                _pendingDamageVolumeDeltaTime > 0f ||
                _pendingDebrisBurstCount > 0 ||
                _globalsDirty ||
                _qualityResourceRefreshRequested;

            if (!hasVisualWork)
                return;

            if (_qualityResourceRefreshRequested)
            {
                _qualityResourceRefreshRequested = false;
                RefreshQualityDependentResourcesIfNeeded();
            }

            FlushPendingTextureClears();
            ProcessQueuedMaskUpdate();
            ProcessQueuedDamageVolumeUpdate(_pendingDamageVolumeDeltaTime);
            _pendingDamageVolumeDeltaTime = 0f;
            ResolveVisualDependencies();
            FlushDebrisBursts();

            if (_globalsDirty)
            {
                PublishGlobals(_pendingHeatRefresh);
                _globalsDirty = false;
                _pendingHeatRefresh = false;
            }
        }

        private void ResolveDependencies()
        {
            if (mapMagicVegetationBridge == null)
                mapMagicVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;

            Transform runtimePlayerTransform = BootstrapState.CurrentPlayerTransform;
            if (runtimePlayerTransform == null && _playerContext != null)
                runtimePlayerTransform = _playerContext.PlayerTransform;

            _playerTransform = runtimePlayerTransform != null ? runtimePlayerTransform : playerTransformOverride;
            if (_playerToolManager == null)
            {
                _playerToolManager = playerToolManagerOverride;
                if (_playerToolManager == null && _playerContext != null)
                    _playerToolManager = _playerContext.ToolManager;

                if (_playerToolManager == null && _playerTransform != null && !_playerTransform.TryGetComponent(out _playerToolManager))
                    _playerToolManager = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<PlayerToolManager>(_playerTransform);
            }

        }

        private void ResolveVisualDependencies()
        {
            if (debrisParticleSystem == null)
                debrisParticleSystem = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<SargassumDebrisParticleSystem>(transform);
        }

        private void CacheRegistryServicesCold()
        {
            _playerContext = GlobalRegistry.Player;
            _inputService = GlobalRegistry.Input;
            ResolveDependencies();
            ResolveVisualDependencies();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                if (playerToolManagerOverride == null)
                    _playerToolManager = null;

                _playerContext = currentService as IPlayerRuntimeContext;
                ResolveDependencies();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Input)
                _inputService = currentService as IInputService;
        }

        private void CreateResources()
        {
#if UNITY_EDITOR
            TryAutoAssignAssets();
#endif

            InitializeRuntimeResourceBudgets(force: false);

            if (_maskRead == null)
                _maskRead = CreateMaskTexture("__SargassumCutMask_A");

            if (_maskWrite == null)
                _maskWrite = CreateMaskTexture("__SargassumCutMask_B");

            if (_damageVolumeRead == null)
                _damageVolumeRead = CreateDamageVolumeTexture("__SargassumDamageVolume_A");

            if (_damageVolumeWrite == null)
                _damageVolumeWrite = CreateDamageVolumeTexture("__SargassumDamageVolume_B");

            EnsureVaultBuffer(ref _queuedStampCommandsHandle, StampCommandsBufferId, StampCommandCapacity);

            if (_stampCommandBufferA == null || _stampCommandBufferB == null)
            {
                ReleaseGraphicsBuffer(ref _stampCommandBufferA);
                ReleaseGraphicsBuffer(ref _stampCommandBufferB);
                _stampCommandBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<StampCommand>(StampCommandCapacity); // COLD ALLOC: GraphicsBuffer[16] - staged cut-mask stamp command buffer A - owner: SargassumCutManager
                _stampCommandBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<StampCommand>(StampCommandCapacity); // COLD ALLOC: GraphicsBuffer[16] - staged cut-mask stamp command buffer B - owner: SargassumCutManager
                _activeStampCommandBuffer = _stampCommandBufferA;
                _stampCommandUploadIndex = 0;
            }

            EnsureVaultBuffer(ref _queuedDamageVolumeStampCommandsHandle, DamageVolumeStampCommandsBufferId, DamageVolumeStampCapacity);

            if (_damageVolumeStampCommandBufferA == null || _damageVolumeStampCommandBufferB == null)
            {
                ReleaseGraphicsBuffer(ref _damageVolumeStampCommandBufferA);
                ReleaseGraphicsBuffer(ref _damageVolumeStampCommandBufferB);
                _damageVolumeStampCommandBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DamageVolumeStampCommand>(DamageVolumeStampCapacity); // COLD ALLOC: GraphicsBuffer[16] - staged 3D terrain-damage volume stamp command buffer A - owner: SargassumCutManager
                _damageVolumeStampCommandBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DamageVolumeStampCommand>(DamageVolumeStampCapacity); // COLD ALLOC: GraphicsBuffer[16] - staged 3D terrain-damage volume stamp command buffer B - owner: SargassumCutManager
                _activeDamageVolumeStampCommandBuffer = _damageVolumeStampCommandBufferA;
                _damageVolumeStampCommandUploadIndex = 0;
            }

            if (_stampCompute == null)
            {
                _stampCompute = stampComputeOverride;
                if (_stampCompute == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[SargassumCutManager] Missing cut-mask compute shader. Expected Hecton_SargassumCutMask.compute.", this);
#endif
                    enabled = false;
                    return;
                }

                _stampKernel = _stampCompute.FindKernel("CSMain");
            }

            if (_damageVolumeCompute == null)
            {
                _damageVolumeCompute = damageVolumeComputeOverride;
                if (_damageVolumeCompute == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[SargassumCutManager] Missing terrain damage-volume compute shader. Expected Hecton_TerrainDamageVolume.compute.", this);
#endif
                    enabled = false;
                    return;
                }

                _damageVolumeKernel = _damageVolumeCompute.FindKernel("StampDamageVolume");
            }

            ResetQueuedMaskUpdateState();
            RefreshMaskWorldRect(forceClear: true);
            RefreshDamageVolumeBounds(forceClear: true);
        }

        private void RefreshQualityDependentResourcesIfNeeded()
        {
            float qualityWeight = ResolveGlobalQualityWeight01();
            int desiredMaskResolution = ResolveMaskResolutionForQualityWeight(qualityWeight);
            ResolveDamageVolumeDimensions(
                qualityWeight,
                out int desiredDamageResolution,
                out int desiredDamageDepth);

            bool maskChanged = _maskRuntimeResolution != desiredMaskResolution;
            bool damageVolumeChanged =
                _damageVolumeRuntimeResolution != desiredDamageResolution ||
                _damageVolumeRuntimeDepth != desiredDamageDepth;

            if (!maskChanged && !damageVolumeChanged)
                return;

            if (HasActiveCutOrDamageTextureWork())
                return;

            if (_resourceQualityWeight >= 0f &&
                Mathf.Abs(qualityWeight - _resourceQualityWeight) < DamageVolumeQualityHysteresis)
            {
                return;
            }

            _resourceQualityWeight = qualityWeight;
            if (maskChanged)
            {
                _maskRuntimeResolution = desiredMaskResolution;
                ResetQueuedMaskUpdateState();
                ReleaseMaskTexture(ref _maskRead);
                ReleaseMaskTexture(ref _maskWrite);
            }

            if (damageVolumeChanged)
            {
                _damageVolumeRuntimeResolution = desiredDamageResolution;
                _damageVolumeRuntimeDepth = desiredDamageDepth;
                RequestDamageVolumeClear(resetQueuedState: true);
                ReleaseDamageVolumeTexture(ref _damageVolumeRead);
                ReleaseDamageVolumeTexture(ref _damageVolumeWrite);
            }

            CreateResources();
            QueueGlobalPublish(forceHeatRefresh: true);
        }

        private bool HasActiveCutOrDamageTextureWork()
        {
            return _queuedStampCount > 0 ||
                   _queuedDamageVolumeStampCount > 0 ||
                   _pendingDamageVolumeDeltaTime > 0f ||
                   _maskEnergy > DamageVolumeEnergyEpsilon ||
                   _damageVolumeEnergy > DamageVolumeEnergyEpsilon ||
                   _maskClearRequested ||
                   _damageVolumeClearRequested;
        }

        private void InitializeRuntimeResourceBudgets(bool force)
        {
            if (!force &&
                _maskRuntimeResolution > 0 &&
                _damageVolumeRuntimeResolution > 0 &&
                _damageVolumeRuntimeDepth > 0)
            {
                return;
            }

            float qualityWeight = ResolveGlobalQualityWeight01();
            _resourceQualityWeight = qualityWeight;
            _maskRuntimeResolution = ResolveMaskResolutionForQualityWeight(qualityWeight);
            ResolveDamageVolumeDimensions(
                qualityWeight,
                out _damageVolumeRuntimeResolution,
                out _damageVolumeRuntimeDepth);
        }

        private int ResolveMaskResolutionForQualityWeight(float qualityWeight)
        {
            float q = Smooth01(qualityWeight);
            int maxResolution = Mathf.Clamp(maskResolution, 512, 2048);
            int target = Mathf.RoundToInt(Mathf.Lerp(512f, maxResolution, q));
            return AlignTextureDimension(target, 64, 512, maxResolution);
        }

        private void ResolveDamageVolumeDimensions(float qualityWeight, out int resolution, out int depth)
        {
            float q = Smooth01(qualityWeight);
            int maxResolution = AlignTextureDimension(Mathf.Clamp(damageVolumeResolution, 32, 128), DamageVolumeResolutionStep, 32, 128);
            int maxDepth = AlignTextureDimension(Mathf.Clamp(damageVolumeDepth, 16, 96), DamageVolumeDepthStep, 16, 96);
            resolution = AlignTextureDimension(Mathf.RoundToInt(Mathf.Lerp(32f, maxResolution, q)), DamageVolumeResolutionStep, 32, maxResolution);
            depth = AlignTextureDimension(Mathf.RoundToInt(Mathf.Lerp(16f, maxDepth, q)), DamageVolumeDepthStep, 16, maxDepth);
        }

        private static int AlignTextureDimension(int value, int step, int minValue, int maxValue)
        {
            int clamped = Mathf.Clamp(value, minValue, maxValue);
            int aligned = ((clamped + step - 1) / step) * step;
            return Mathf.Clamp(aligned, minValue, maxValue);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float Smooth01(float value)
        {
            float q = math.saturate(math.isfinite(value) ? value : 1f);
            return q * q * (3f - 2f * q);
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool EnsureVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsVaultHandleCreated(in handle) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            if (IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);

            return IsVaultHandleCreated(in handle) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredLength;
        }

        private bool TryAcquireVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultHandleCreated(in handle) ||
                !vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated && buffer.Length >= requiredLength)
                return true;

            vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
            buffer = default;
            return false;
        }

        private void ReleaseVaultWrite<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            _dataVault?.ReleaseWriteLock(in handle, VaultOwnerSystemId);
        }

        private void ReleaseVaultBuffer<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void ReleaseResources()
        {
            ResetQueuedMaskUpdateState();
            ReleaseMaskTexture(ref _maskRead);
            ReleaseMaskTexture(ref _maskWrite);

            ReleaseGraphicsBuffer(ref _stampCommandBufferA);
            ReleaseGraphicsBuffer(ref _stampCommandBufferB);
            _activeStampCommandBuffer = null;
            _stampCommandUploadIndex = 0;

            ReleaseGraphicsBuffer(ref _damageVolumeStampCommandBufferA);
            ReleaseGraphicsBuffer(ref _damageVolumeStampCommandBufferB);
            _activeDamageVolumeStampCommandBuffer = null;
            _damageVolumeStampCommandUploadIndex = 0;

            ReleaseVaultBuffer(ref _queuedStampCommandsHandle);
            ReleaseVaultBuffer(ref _queuedDamageVolumeStampCommandsHandle);

            ReleaseDamageVolumeTexture(ref _damageVolumeRead);
            ReleaseDamageVolumeTexture(ref _damageVolumeWrite);
            _damageVolumeEnergy = 0f;

            _stampCompute = null;
            _stampKernel = -1;
            _damageVolumeCompute = null;
            _damageVolumeKernel = -1;
            _lastMaskDispatchFrame = -1;
            _lastDamageVolumeDispatchFrame = -1;

            PublishRecentCutHeatCount(0);
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private GraphicsBuffer ResolveStampCommandWriteBuffer()
        {
            GraphicsBuffer preferred = (_stampCommandUploadIndex & 1) == 0
                ? _stampCommandBufferB
                : _stampCommandBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _stampCommandBufferA != null && _stampCommandBufferA.IsValid()
                ? _stampCommandBufferA
                : _stampCommandBufferB;
        }

        private GraphicsBuffer ResolveDamageVolumeStampCommandWriteBuffer()
        {
            GraphicsBuffer preferred = (_damageVolumeStampCommandUploadIndex & 1) == 0
                ? _damageVolumeStampCommandBufferB
                : _damageVolumeStampCommandBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _damageVolumeStampCommandBufferA != null && _damageVolumeStampCommandBufferA.IsValid()
                ? _damageVolumeStampCommandBufferA
                : _damageVolumeStampCommandBufferB;
        }

        private void RefreshMaskWorldRect(bool forceClear = false)
        {
            float desiredWorldSize = Mathf.Max(minimumMaskWorldSize, 128f);
            float snapWorldStride = ResolveSnapWorldStride(desiredWorldSize);
            Vector2 desiredCenterXZ = _playerTransform != null
                ? QuantizeCenter(new Vector2(_playerTransform.position.x, _playerTransform.position.z), snapWorldStride)
                : (_maskWorldSize > 0f ? _maskCenterXZ : Vector2.zero);

            bool mustClear = forceClear || _maskWorldSize <= 0f || Mathf.Abs(desiredWorldSize - _maskWorldSize) > 0.001f;
            Vector2 centerDelta = desiredCenterXZ - _maskCenterXZ;
            if (!mustClear && centerDelta.sqrMagnitude <= 0.000001f)
                return;

            _maskCenterXZ = desiredCenterXZ;
            _maskWorldSize = desiredWorldSize;
            UpdateWorldRect(desiredCenterXZ, desiredWorldSize);
            RefreshDamageVolumeBounds(forceClear: mustClear);

            if (mustClear)
            {
                RequestMaskClear(resetQueuedState: true);
                _maskEnergy = 0f;
                _debugMaskEnergy = 0f;
                return;
            }

            ScrollMaskTextures(centerDelta);
        }

        private bool TryResolveScooterStamp(out Vector3 stampPositionWS, out Vector3 stampDirectionWS)
        {
            stampPositionWS = default;
            stampDirectionWS = default;
            if (_playerToolManager == null || _playerToolManager.IsSwapping)
                return false;

            if (!(_playerToolManager.CurrentTool is MantaScooter scooter) || !scooter.IsTransportActive)
                return false;

            Transform scooterTransform = scooter.transform;
            stampDirectionWS = scooterTransform.forward;
            stampPositionWS = scooterTransform.position;
            if (scooterForwardOffset > 0.0001f)
                stampPositionWS += stampDirectionWS * scooterForwardOffset;
            stampPositionWS.y = ResolveMaskWaterLevel(stampPositionWS.y);
            return IsInsideMaskWorldRect(stampPositionWS);
        }

        private bool TryResolveKnifeStamp(out Vector3 stampPositionWS, out Vector3 stampDirectionWS)
        {
            stampPositionWS = default;
            stampDirectionWS = default;
            if (_knifeStampCooldownRemaining > 0f || _playerToolManager == null || _playerToolManager.IsSwapping)
                return false;

            if (!(_playerToolManager.CurrentTool is KnifeTool knife) || !knife.IsEquipped)
                return false;

            IInputService inputService = _inputService;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            if (!inputState.HasAction(PlayerInputAction.PrimaryFire))
                return false;

            Transform knifeTransform = knife.transform;
            stampDirectionWS = knifeTransform.forward;
            stampPositionWS = knifeTransform.position + stampDirectionWS * knifeForwardOffset;
            stampPositionWS.y = ResolveMaskWaterLevel(stampPositionWS.y);
            return IsInsideMaskWorldRect(stampPositionWS);
        }

        private float ResolveMaskWaterLevel(float fallbackY)
        {
            if (mapMagicVegetationBridge != null && mapMagicVegetationBridge.ActiveSurfaceInstanceCount > 0)
                return mapMagicVegetationBridge.ActiveSurfaceDrawBounds.center.y;

            return Mathf.Max(DefaultWaterLevel, fallbackY);
        }

        private void ExecuteStampPass(Vector3 positionWS, float radiusWS, float strength, float deltaTime)
        {
            float recovery = Mathf.Max(0f, recoveryPerSecond * Mathf.Max(0f, deltaTime));
            if (recovery > _pendingRecovery)
                _pendingRecovery = recovery;

            float clampedStrength = Mathf.Clamp01(strength);
            if (clampedStrength <= 0f || radiusWS <= 0f)
                return;

            Vector2 uvCenter = new Vector2(
                (positionWS.x - _maskWorldRect.x) * _maskWorldRect.z,
                (positionWS.z - _maskWorldRect.y) * _maskWorldRect.w);
            float uvRadius = radiusWS * _maskWorldRect.z;
            if (_queuedStampCount >= StampCommandCapacity)
            {
                TryCoalesceOverflowStamp(uvCenter, uvRadius, clampedStrength, positionWS);
                return;
            }

            if (!TryAcquireVaultBuffer(in _queuedStampCommandsHandle, StampCommandCapacity, out NativeArray<StampCommand> queuedStampCommands))
            {
                return;
            }

            try
            {
                queuedStampCommands[_queuedStampCount] = new StampCommand
                {
                    UvRadiusStrength = new Vector4(uvCenter.x, uvCenter.y, uvRadius, clampedStrength)
                };
                _queuedStampCount++;
                _debugLastStampPosition = positionWS;
            }
            finally
            {
                ReleaseVaultWrite(in _queuedStampCommandsHandle);
            }
        }

        private bool TryCoalesceOverflowStamp(Vector2 uvCenter, float uvRadius, float strength, Vector3 positionWS)
        {
            if (_queuedStampCount <= 0 ||
                !TryAcquireVaultBuffer(in _queuedStampCommandsHandle, StampCommandCapacity, out NativeArray<StampCommand> queuedStampCommands))
            {
                return false;
            }

            try
            {
                int index = math.min(_queuedStampCount - 1, StampCommandCapacity - 1);
                StampCommand existing = queuedStampCommands[index];
                Vector4 payload = existing.UvRadiusStrength;
                float2 existingCenter = new float2(payload.x, payload.y);
                float coverageRadius = math.distance(existingCenter, new float2(uvCenter.x, uvCenter.y)) + uvRadius;
                payload.z = math.max(payload.z, coverageRadius);
                payload.w = math.max(payload.w, strength);
                queuedStampCommands[index] = new StampCommand
                {
                    UvRadiusStrength = payload
                };
                _stampOverflowCoalesceCount++;
                _debugLastStampPosition = positionWS;
                return true;
            }
            finally
            {
                ReleaseVaultWrite(in _queuedStampCommandsHandle);
            }
        }

        private void DecayRecentCutStamps(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            for (int i = 0; i < RecentStampCapacity; i++)
            {
                if (_recentCutStamps[i].RemainingLifetime <= 0f)
                    continue;

                _recentCutStamps[i].RemainingLifetime = Mathf.Max(0f, _recentCutStamps[i].RemainingLifetime - deltaTime);
            }
        }

        private void RegisterRecentCutStamp(Vector3 positionWS, float radiusWS, float strength)
        {
            int targetIndex = -1;
            float weakestScore = float.MaxValue;

            for (int i = 0; i < RecentStampCapacity; i++)
            {
                RecentCutStamp stamp = _recentCutStamps[i];
                if (stamp.RemainingLifetime <= 0f)
                {
                    targetIndex = i;
                    break;
                }

                float score = stamp.RemainingLifetime * stamp.Strength;
                if (score < weakestScore)
                {
                    weakestScore = score;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0)
                targetIndex = 0;

            _recentCutStamps[targetIndex] = new RecentCutStamp
            {
                PositionWS = positionWS,
                RadiusWS = Mathf.Max(0.05f, radiusWS),
                Strength = Mathf.Clamp01(strength),
                RemainingLifetime = recentCutLifetime
            };
        }

        private void RegisterRecentCutHeatStamp(Vector3 positionWS, float radiusWS, float strength)
        {
            float currentTime = ResolveThermalShaderClockSeconds();
            float lifetime = Mathf.Max(0.01f, shaderScarLifetime);
            int targetIndex = -1;
            float weakestScore = float.MaxValue;

            for (int i = 0; i < RecentStampCapacity; i++)
            {
                RecentCutHeatStamp stamp = _recentCutHeatStamps[i];
                float remainingLifetime = (stamp.StartTime + stamp.Lifetime) - currentTime;
                if (remainingLifetime <= 0f)
                {
                    targetIndex = i;
                    break;
                }

                float score = remainingLifetime * stamp.Strength;
                if (score < weakestScore)
                {
                    weakestScore = score;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0)
                targetIndex = 0;

            _recentCutHeatStamps[targetIndex] = new RecentCutHeatStamp
            {
                PositionWS = positionWS,
                RadiusWS = Mathf.Max(0.05f, radiusWS),
                Strength = Mathf.Clamp01(strength),
                StartTime = currentTime,
                Lifetime = lifetime
            };
            WorldSpatialHashGrid.RegisterTransientEvent(
                positionWS,
                Mathf.Max(0.05f, radiusWS),
                Mathf.Clamp01(strength),
                PlasmaCutThermalEventLifetimeSeconds,
                SpatialTransientEventType.ThermalGradient,
                SpatialInteractionFlags.ThermalReceiver,
                FieldTargetRole.Generic,
                0,
                PlasmaCutThermalDeltaCelsius * Mathf.Clamp01(strength));
            _recentCutHeatDirty = true;
        }

        private static float ResolveThermalShaderClockSeconds()
        {
            return Time.timeSinceLevelLoad;
        }

        private void QueueDebrisBurst(Vector3 positionWS, Vector3 directionWS, float cutStrength, float bubbleWeight)
        {
            if (debrisParticleSystem == null || _pendingDebrisBurstCount >= _pendingDebrisBursts.Length)
                return;

            _pendingDebrisBursts[_pendingDebrisBurstCount++] = new PendingDebrisBurst
            {
                PositionWS = positionWS,
                DirectionWS = directionWS,
                CutStrength = cutStrength,
                BubbleWeight = bubbleWeight
            };
        }

        private void FlushDebrisBursts()
        {
            if (debrisParticleSystem == null)
            {
                _pendingDebrisBurstCount = 0;
                return;
            }

            for (int i = 0; i < _pendingDebrisBurstCount; i++)
            {
                PendingDebrisBurst burst = _pendingDebrisBursts[i];
                debrisParticleSystem.EmitBurst(burst.PositionWS, burst.DirectionWS, burst.CutStrength, burst.BubbleWeight);
            }

            _pendingDebrisBurstCount = 0;
        }

        private void QueueDamageVolumeVisualSync(float deltaTime)
        {
            if (_queuedDamageVolumeStampCount <= 0 && _damageVolumeEnergy <= DamageVolumeEnergyEpsilon)
                return;

            if (deltaTime > _pendingDamageVolumeDeltaTime)
                _pendingDamageVolumeDeltaTime = Mathf.Max(0f, deltaTime);
        }

        private bool IsInsideMaskWorldRect(Vector3 positionWS)
        {
            if (_maskWorldSize <= 0f)
                return false;

            float maxX = _maskWorldRect.x + _maskWorldSize;
            float maxZ = _maskWorldRect.y + _maskWorldSize;
            return positionWS.x >= _maskWorldRect.x &&
                   positionWS.x <= maxX &&
                   positionWS.z >= _maskWorldRect.y &&
                   positionWS.z <= maxZ;
        }

        private void UpdateWorldRect(Vector2 centerXZ, float worldSize)
        {
            float halfSize = worldSize * 0.5f;
            _maskWorldRect = new Vector4(
                centerXZ.x - halfSize,
                centerXZ.y - halfSize,
                1f / Mathf.Max(worldSize, 0.001f),
                1f / Mathf.Max(worldSize, 0.001f));
            _debugMaskWorldRect = _maskWorldRect;
        }

        private void ScrollMaskTextures(Vector2 centerDelta)
        {
            if (_maskRead == null || _maskWrite == null)
                return;

            float uvOffsetX = centerDelta.x / Mathf.Max(_maskWorldSize, 0.001f);
            float uvOffsetY = centerDelta.y / Mathf.Max(_maskWorldSize, 0.001f);
            if (Mathf.Abs(uvOffsetX) >= 1f || Mathf.Abs(uvOffsetY) >= 1f)
            {
                RequestMaskClear(resetQueuedState: true);
                _maskEnergy = 0f;
                _debugMaskEnergy = 0f;
                return;
            }

            _pendingScrollUv.x += uvOffsetX;
            _pendingScrollUv.y += uvOffsetY;
        }

        private float ResolveSnapWorldStride(float worldSize)
        {
            float pixelWorldSize = worldSize / Mathf.Max(_maskRuntimeResolution, 1);
            return pixelWorldSize * Mathf.Max(0.1f, centerSnapPixelStride);
        }

        private static Vector2 QuantizeCenter(Vector2 centerXZ, float stride)
        {
            if (stride <= 0.0001f)
                return centerXZ;

            return new Vector2(
                Mathf.Round(centerXZ.x / stride) * stride,
                Mathf.Round(centerXZ.y / stride) * stride);
        }

        private static Vector3 NormalizeVector3Fast(Vector3 vector, Vector3 fallback)
        {
            float magnitudeSq = vector.sqrMagnitude;
            return magnitudeSq > 0.0001f ? vector * math.rsqrt(magnitudeSq) : fallback;
        }

        private void RequestMaskClear(bool resetQueuedState)
        {
            _maskClearRequested = true;
            if (resetQueuedState)
                ResetQueuedMaskUpdateState();
        }

        private void ClearMaskTextures(bool resetQueuedState)
        {
            if (_maskRead == null || _maskWrite == null)
                return;

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = _maskRead;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = _maskWrite;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = active;
            if (resetQueuedState)
                ResetQueuedMaskUpdateState();
        }

        private RenderTexture CreateDamageVolumeTexture(string textureName)
        {
            int runtimeResolution = Mathf.Max(32, _damageVolumeRuntimeResolution);
            int runtimeDepth = Mathf.Max(16, _damageVolumeRuntimeDepth);
            RenderTexture texture = new RenderTexture(runtimeResolution, runtimeResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = textureName,
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = runtimeDepth,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[1] - persistent 3D terrain-damage splat volume - owner: SargassumCutManager
            texture.Create();
            return texture;
        }

        private static void ReleaseDamageVolumeTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private void RequestDamageVolumeClear(bool resetQueuedState)
        {
            _damageVolumeClearRequested = true;
            if (resetQueuedState)
            {
                _queuedDamageVolumeStampCount = 0;
                _damageVolumeEnergy = 0f;
            }
        }

        private void ClearDamageVolumeTextures(bool resetQueuedState)
        {
            if (_damageVolumeRead == null || _damageVolumeWrite == null)
                return;

            UnityEngine.Graphics.SetRenderTarget(_damageVolumeRead, 0, CubemapFace.Unknown, -1);
            GL.Clear(false, true, Color.clear);
            UnityEngine.Graphics.SetRenderTarget(_damageVolumeWrite, 0, CubemapFace.Unknown, -1);
            GL.Clear(false, true, Color.clear);
            UnityEngine.Graphics.SetRenderTarget(null);
            if (resetQueuedState)
            {
                _queuedDamageVolumeStampCount = 0;
                _damageVolumeEnergy = 0f;
            }
        }

        private void FlushPendingTextureClears()
        {
            if (_maskClearRequested)
            {
                ClearMaskTextures(resetQueuedState: false);
                _maskClearRequested = false;
            }

            if (_damageVolumeClearRequested)
            {
                ClearDamageVolumeTextures(resetQueuedState: false);
                _damageVolumeClearRequested = false;
            }
        }

        private void ResetQueuedMaskUpdateState()
        {
            _pendingScrollUv = Vector2.zero;
            _pendingRecovery = 0f;
            _queuedStampCount = 0;
            _stampOverflowCoalesceCount = 0;
        }

        private bool HasPendingMaskUpdate()
        {
            return _queuedStampCount > 0 ||
                   _pendingRecovery > 0.000001f ||
                   _pendingScrollUv.sqrMagnitude > 0.0000001f;
        }

        private void ProcessQueuedMaskUpdate()
        {
            if (_maskRead == null ||
                _maskWrite == null ||
                _stampCompute == null ||
                _stampKernel < 0 ||
                _activeStampCommandBuffer == null ||
                !HasPendingMaskUpdate() ||
                _lastMaskDispatchFrame == Time.frameCount)
            {
                return;
            }

            int uploadedStampCount = 0;
            if (_queuedStampCount > 0)
            {
                if (!TryAcquireVaultBuffer(in _queuedStampCommandsHandle, StampCommandCapacity, out NativeArray<StampCommand> queuedStampCommands))
                    return;

                try
                {
                    GraphicsBuffer stampWriteBuffer = ResolveStampCommandWriteBuffer();
                    if (stampWriteBuffer == null)
                        return;

                    int safeQueuedStampCount = math.min(_queuedStampCount, math.min(queuedStampCommands.Length, StampCommandCapacity));
                    if (safeQueuedStampCount <= 0)
                        return;

                    GraphicsBufferUploadUtility.UploadNativeArray(stampWriteBuffer, queuedStampCommands, safeQueuedStampCount);
                    _activeStampCommandBuffer = stampWriteBuffer;
                    _stampCommandUploadIndex ^= 1;
                    uploadedStampCount = safeQueuedStampCount;
                }
                finally
                {
                    ReleaseVaultWrite(in _queuedStampCommandsHandle);
                }
            }

            _stampCompute.SetTexture(_stampKernel, _MainTexId, _maskRead);
            _stampCompute.SetTexture(_stampKernel, _ResultId, _maskWrite);
            _stampCompute.SetBuffer(_stampKernel, _StampCommandsId, _activeStampCommandBuffer);
            _stampCompute.SetInt(_StampCountId, uploadedStampCount);
            _stampCompute.SetVector(_ScrollUvOffsetId, new Vector4(_pendingScrollUv.x, _pendingScrollUv.y, 0f, 0f));
            _stampCompute.SetFloat(_RecoveryId, _pendingRecovery);
            _stampCompute.SetVector(
                _TexelSizeId,
                new Vector4(
                    1f / Mathf.Max(_maskRuntimeResolution, 1),
                    1f / Mathf.Max(_maskRuntimeResolution, 1),
                    _maskRuntimeResolution,
                    _maskRuntimeResolution));

            int groupCount = Mathf.Max(1, Mathf.CeilToInt(_maskRuntimeResolution / (float)StampThreadGroupSize));
            _stampCompute.Dispatch(_stampKernel, groupCount, groupCount, 1);

            RenderTexture temp = _maskRead;
            _maskRead = _maskWrite;
            _maskWrite = temp;
            _lastMaskDispatchFrame = Time.frameCount;
            ResetQueuedMaskUpdateState();
        }

        private void RefreshDamageVolumeBounds(bool forceClear = false)
        {
            if (_damageVolumeRead == null || _damageVolumeWrite == null)
                return;

            float worldSize = Mathf.Max(minimumMaskWorldSize, 128f);
            float halfSize = worldSize * 0.5f;
            float minX = _maskCenterXZ.x - halfSize;
            float minZ = _maskCenterXZ.y - halfSize;
            float minY = ResolveMaskWaterLevel(_playerTransform != null ? _playerTransform.position.y : DefaultWaterLevel) - damageVolumeHeight;
            Vector3 desiredWorldMin = new Vector3(minX, minY, minZ);
            Vector3 desiredWorldSize = new Vector3(worldSize, damageVolumeHeight, worldSize);

            bool boundsChanged =
                (_damageVolumeWorldSize - desiredWorldSize).sqrMagnitude > 0.0001f ||
                (_damageVolumeWorldMin - desiredWorldMin).sqrMagnitude > 0.0001f;

            _damageVolumeWorldMin = desiredWorldMin;
            _damageVolumeWorldSize = desiredWorldSize;

            if (forceClear || boundsChanged)
                RequestDamageVolumeClear(resetQueuedState: true);
        }

        private void QueueDamageVolumeStamp(Vector3 positionWS, float radiusWS, float strength)
        {
            RefreshDamageVolumeBounds();
            if (_damageVolumeRead == null ||
                _damageVolumeWrite == null)
            {
                return;
            }

            Vector3 maxBounds = _damageVolumeWorldMin + _damageVolumeWorldSize;
            if (positionWS.x < _damageVolumeWorldMin.x || positionWS.x > maxBounds.x ||
                positionWS.y < _damageVolumeWorldMin.y || positionWS.y > maxBounds.y ||
                positionWS.z < _damageVolumeWorldMin.z || positionWS.z > maxBounds.z)
            {
                return;
            }

            if (_queuedDamageVolumeStampCount >= DamageVolumeStampCapacity)
            {
                TryCoalesceOverflowDamageVolumeStamp(positionWS, radiusWS, strength);
                return;
            }

            if (!TryAcquireVaultBuffer(
                    in _queuedDamageVolumeStampCommandsHandle,
                    DamageVolumeStampCapacity,
                    out NativeArray<DamageVolumeStampCommand> queuedDamageVolumeStampCommands))
            {
                return;
            }

            try
            {
                queuedDamageVolumeStampCommands[_queuedDamageVolumeStampCount] = new DamageVolumeStampCommand
                {
                    PositionRadius = new Vector4(positionWS.x, positionWS.y, positionWS.z, Mathf.Max(0.05f, radiusWS)),
                    StrengthPadding = new Vector4(Mathf.Clamp01(strength), 0f, 0f, 0f)
                };
                _damageVolumeEnergy = Mathf.Max(_damageVolumeEnergy, Mathf.Clamp01(strength));
                _queuedDamageVolumeStampCount++;
            }
            finally
            {
                ReleaseVaultWrite(in _queuedDamageVolumeStampCommandsHandle);
            }
        }

        private bool TryCoalesceOverflowDamageVolumeStamp(Vector3 positionWS, float radiusWS, float strength)
        {
            if (_queuedDamageVolumeStampCount <= 0 ||
                !TryAcquireVaultBuffer(
                    in _queuedDamageVolumeStampCommandsHandle,
                    DamageVolumeStampCapacity,
                    out NativeArray<DamageVolumeStampCommand> queuedDamageVolumeStampCommands))
            {
                return false;
            }

            try
            {
                int index = math.min(_queuedDamageVolumeStampCount - 1, DamageVolumeStampCapacity - 1);
                DamageVolumeStampCommand existing = queuedDamageVolumeStampCommands[index];
                Vector4 positionRadius = existing.PositionRadius;
                Vector4 strengthPadding = existing.StrengthPadding;
                float3 existingCenter = new float3(positionRadius.x, positionRadius.y, positionRadius.z);
                float clampedRadius = math.max(0.05f, radiusWS);
                float coverageRadius = math.distance(existingCenter, new float3(positionWS.x, positionWS.y, positionWS.z)) + clampedRadius;
                positionRadius.w = math.max(positionRadius.w, coverageRadius);
                strengthPadding.x = math.max(strengthPadding.x, Mathf.Clamp01(strength));
                queuedDamageVolumeStampCommands[index] = new DamageVolumeStampCommand
                {
                    PositionRadius = positionRadius,
                    StrengthPadding = strengthPadding
                };
                _damageVolumeEnergy = Mathf.Max(_damageVolumeEnergy, strengthPadding.x);
                _damageVolumeStampOverflowCoalesceCount++;
                return true;
            }
            finally
            {
                ReleaseVaultWrite(in _queuedDamageVolumeStampCommandsHandle);
            }
        }

        private void ProcessQueuedDamageVolumeUpdate(float deltaTime)
        {
            if (_damageVolumeRead == null ||
                _damageVolumeWrite == null ||
                _damageVolumeCompute == null ||
                _damageVolumeKernel < 0 ||
                _activeDamageVolumeStampCommandBuffer == null ||
                (_queuedDamageVolumeStampCount <= 0 && deltaTime <= 0f) ||
                _lastDamageVolumeDispatchFrame == Time.frameCount)
            {
                return;
            }

            int uploadedDamageVolumeStampCount = 0;
            if (_queuedDamageVolumeStampCount > 0)
            {
                if (!TryAcquireVaultBuffer(
                        in _queuedDamageVolumeStampCommandsHandle,
                        DamageVolumeStampCapacity,
                        out NativeArray<DamageVolumeStampCommand> queuedDamageVolumeStampCommands))
                {
                    return;
                }

                try
                {
                    GraphicsBuffer damageWriteBuffer = ResolveDamageVolumeStampCommandWriteBuffer();
                    if (damageWriteBuffer == null)
                        return;

                    int safeQueuedDamageVolumeStampCount = math.min(
                        _queuedDamageVolumeStampCount,
                        math.min(queuedDamageVolumeStampCommands.Length, DamageVolumeStampCapacity));
                    if (safeQueuedDamageVolumeStampCount <= 0)
                        return;

                    GraphicsBufferUploadUtility.UploadNativeArray(
                        damageWriteBuffer,
                        queuedDamageVolumeStampCommands,
                        safeQueuedDamageVolumeStampCount);
                    _activeDamageVolumeStampCommandBuffer = damageWriteBuffer;
                    _damageVolumeStampCommandUploadIndex ^= 1;
                    uploadedDamageVolumeStampCount = safeQueuedDamageVolumeStampCount;
                }
                finally
                {
                    ReleaseVaultWrite(in _queuedDamageVolumeStampCommandsHandle);
                }
            }

            _damageVolumeCompute.SetTexture(_damageVolumeKernel, _DamageVolumeSourceId, _damageVolumeRead);
            _damageVolumeCompute.SetTexture(_damageVolumeKernel, _DamageVolumeResultId, _damageVolumeWrite);
            _damageVolumeCompute.SetBuffer(_damageVolumeKernel, _DamageVolumeStampCommandsId, _activeDamageVolumeStampCommandBuffer);
            _damageVolumeCompute.SetInt(_DamageVolumeStampCountId, uploadedDamageVolumeStampCount);
            _damageVolumeCompute.SetFloat(_DamageVolumeRecoveryId, Mathf.Max(0f, damageVolumeRecoveryPerSecond * Mathf.Max(0f, deltaTime)));
            _damageVolumeCompute.SetVector(
                _DamageVolumeWorldMinParamId,
                new Vector4(_damageVolumeWorldMin.x, _damageVolumeWorldMin.y, _damageVolumeWorldMin.z, 0f));
            _damageVolumeCompute.SetVector(
                _DamageVolumeInvSizeParamId,
                new Vector4(
                    1f / Mathf.Max(_damageVolumeWorldSize.x, 0.001f),
                    1f / Mathf.Max(_damageVolumeWorldSize.y, 0.001f),
                    1f / Mathf.Max(_damageVolumeWorldSize.z, 0.001f),
                    0f));
            int runtimeResolution = Mathf.Max(32, _damageVolumeRuntimeResolution);
            int runtimeDepth = Mathf.Max(16, _damageVolumeRuntimeDepth);
            _damageVolumeCompute.SetInts(_DamageVolumeResolutionId, runtimeResolution, runtimeDepth, runtimeResolution);

            int groupCountX = Mathf.Max(1, Mathf.CeilToInt(runtimeResolution / (float)DamageVolumeThreadGroupSize));
            int groupCountY = Mathf.Max(1, Mathf.CeilToInt(runtimeDepth / (float)DamageVolumeThreadGroupSize));
            int groupCountZ = Mathf.Max(1, Mathf.CeilToInt(runtimeResolution / (float)DamageVolumeThreadGroupSize));
            _damageVolumeCompute.Dispatch(_damageVolumeKernel, groupCountX, groupCountY, groupCountZ);

            _damageVolumeEnergy = Mathf.Max(
                0f,
                _damageVolumeEnergy - Mathf.Max(0f, damageVolumeRecoveryPerSecond * Mathf.Max(0f, deltaTime)));
            RenderTexture temp = _damageVolumeRead;
            _damageVolumeRead = _damageVolumeWrite;
            _damageVolumeWrite = temp;
            _lastDamageVolumeDispatchFrame = Time.frameCount;
            _queuedDamageVolumeStampCount = 0;
            _damageVolumeStampOverflowCoalesceCount = 0;
        }

        private void QueueGlobalPublish(bool forceHeatRefresh = false)
        {
            _globalsDirty = true;
            _pendingHeatRefresh |= forceHeatRefresh;
        }

        private void PublishGlobals(bool forceHeatRefresh = false)
        {
            if (_maskRead == null)
            {
                Shader.SetGlobalFloat(_CutMaskActiveId, 0f);
                PublishRecentCutHeatCount(0);
                Shader.SetGlobalFloat(_DamageVolumeActiveId, 0f);
                return;
            }

            Shader.SetGlobalTexture(_CutMaskTextureId, _maskRead);
            Shader.SetGlobalVector(_CutMaskWorldRectId, _maskWorldRect);
            Shader.SetGlobalFloat(_CutMaskActiveId, _maskWorldSize > 0f ? 1f : 0f);
            if (_damageVolumeRead != null)
            {
                Shader.SetGlobalTexture(_DamageVolumeTextureId, _damageVolumeRead);
                Shader.SetGlobalFloat(_DamageVolumeActiveId, 1f);
                Shader.SetGlobalVector(
                    _DamageVolumeWorldMinId,
                    new Vector4(_damageVolumeWorldMin.x, _damageVolumeWorldMin.y, _damageVolumeWorldMin.z, 0f));
                Shader.SetGlobalVector(
                    _DamageVolumeInvSizeId,
                    new Vector4(
                        1f / Mathf.Max(_damageVolumeWorldSize.x, 0.001f),
                        1f / Mathf.Max(_damageVolumeWorldSize.y, 0.001f),
                        1f / Mathf.Max(_damageVolumeWorldSize.z, 0.001f),
                        0f));
            }
            else
            {
                Shader.SetGlobalFloat(_DamageVolumeActiveId, 0f);
            }

            if (!forceHeatRefresh && !_recentCutHeatDirty)
                return;

            _recentCutHeatCount = 0;
            float currentTime = ResolveThermalShaderClockSeconds();
            for (int i = 0; i < RecentStampCapacity; i++)
            {
                RecentCutHeatStamp stamp = _recentCutHeatStamps[i];
                float remainingLifetime = (stamp.StartTime + stamp.Lifetime) - currentTime;
                if (remainingLifetime <= 0f || stamp.Strength <= 0f || stamp.RadiusWS <= 0f)
                    continue;

                _recentCutHeatPositionRadius[_recentCutHeatCount] = new Vector4(
                    stamp.PositionWS.x,
                    stamp.PositionWS.y,
                    stamp.PositionWS.z,
                    stamp.RadiusWS);
                _recentCutHeatStrengthTime[_recentCutHeatCount] = new Vector4(
                    stamp.Strength,
                    stamp.StartTime,
                    stamp.Lifetime,
                    0f);
                _recentCutHeatCount++;
            }

            PublishRecentCutHeatCount(_recentCutHeatCount);
            if (_recentCutHeatCount > 0)
            {
                Shader.SetGlobalVectorArray(_RecentCutHeatPositionRadiusId, _recentCutHeatPositionRadius);
                Shader.SetGlobalVectorArray(_RecentCutHeatStrengthTimeId, _recentCutHeatStrengthTime);
            }

            _recentCutHeatDirty = false;
        }

        private void PublishRecentCutHeatCount(int count)
        {
            if (_publishedRecentCutHeatCount == count)
                return;

            Shader.SetGlobalInt(_RecentCutHeatCountId, count);
            _publishedRecentCutHeatCount = count;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredTick)
            {
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredLateFrameTick)
            {
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            SargassumCutManager registered = GlobalRegistry.SargassumCut;
            if (registered != null && registered != this)
                return;

            GlobalRegistry.RegisterSargassumCutRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.SargassumCut, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSargassumCutRuntime(this);
            _serviceRegistered = false;
        }

        private void TryUnregister()
        {

            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (!Application.isPlaying || _registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private RenderTexture CreateMaskTexture(string textureName)
        {
            bool supportsR8RandomWrite = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) &&
                                         SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.R8);
            RenderTextureFormat format = supportsR8RandomWrite
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
            RenderTexture texture = new RenderTexture(_maskRuntimeResolution, _maskRuntimeResolution, 0, format, RenderTextureReadWrite.Linear)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[1] - global sargassum cut mask ping-pong target - owner: SargassumCutManager
            texture.Create();
            return texture;
        }

        private static void ReleaseMaskTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Destroy(texture);
            texture = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoAssignAssets();
        }

        private void TryAutoAssignAssets()
        {
            if (stampComputeOverride == null)
                stampComputeOverride = AssetDatabase.LoadAssetAtPath<ComputeShader>(StampComputeAssetPath);

            if (damageVolumeComputeOverride == null)
                damageVolumeComputeOverride = AssetDatabase.LoadAssetAtPath<ComputeShader>(DamageVolumeComputeAssetPath);
        }
#endif
    }
}

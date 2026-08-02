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
        private const float DefaultWaterLevel = 14.02f;
        private const int RecentStampCapacity = 16;
        private const int DamageVolumeStampCapacity = 16;
        private const int DamageVolumeThreadGroupSize = 4;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const int DamageVolumeResolutionStep = 16;
        private const int DamageVolumeDepthStep = 8;
        private const float DamageVolumeQualityHysteresis = 0.08f;
        private const float DamageVolumeEnergyEpsilon = 0.0001f;
        private const float PlasmaCutThermalEventLifetimeSeconds = 1.5f;
        private const float PlasmaCutThermalDeltaCelsius = 220f;
        private const uint DebrisBurstOverflowWarningHash = 0x5343444Fu;
        private const uint DebrisBurstContextHash = 0x53434442u;
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

        [Header("в”Ђв”Ђ Runtime Wiring в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ")]
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

        [Header("в”Ђв”Ђ Cut Mask RT в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ")]
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

        [Header("в”Ђв”Ђ Damage Volume в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ")]
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

        [Header("в”Ђв”Ђ Scooter Cutting в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ")]
        [SerializeField, Range(0.1f, 6f)]
        [Tooltip("World-space radius carved by an active Manta scooter propeller.")]
        private float scooterCutRadius = 2.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized strength written into the cut mask by an active Manta scooter.")]
        private float scooterCutStrength = 0.95f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Forward offset applied to the active Manta scooter transform before stamping.")]
        private float scooterForwardOffset = 0.75f;

        [Header("в”Ђв”Ђ Knife Cutting в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ")]
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

        [Header("в”Ђв”Ђ Diagnostics в”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђв”Ђ")]
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
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private PlayerToolManager _playerToolManager;
        private bool _playerDependencyRefreshRequested = true;
        private RenderTexture _maskRead;
        private RenderTexture _maskWrite;
        private ComputeShader _stampCompute;
        private int _stampKernel = -1;
        private int _stampThreadGroupSizeX;
        private int _stampThreadGroupSizeY;
        private GraphicsBuffer _stampCommandBufferA;
        private GraphicsBuffer _stampCommandBufferB;
        private GraphicsBuffer _activeStampCommandBuffer;
        private RenderTexture _damageVolumeRead;
        private RenderTexture _damageVolumeWrite;
        private ComputeShader _damageVolumeCompute;
        private int _damageVolumeKernel = -1;
        private int _damageVolumeThreadGroupSizeX;
        private int _damageVolumeThreadGroupSizeY;
        private int _damageVolumeThreadGroupSizeZ;
        private GraphicsBuffer _damageVolumeStampCommandBufferA;
        private GraphicsBuffer _damageVolumeStampCommandBufferB;
        private GraphicsBuffer _activeDamageVolumeStampCommandBuffer;
        private bool _serviceRegistered;
        private bool _runtimeRoutesRetiredAfterOwnershipLoss;
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
        private int _debrisBurstOverflowCount;
        private int _lastDebrisBurstOverflowTelemetryFrame = -1;
        private Vector3 _damageVolumeWorldMin;
        private Vector3 _damageVolumeWorldSize;
        private int _lastDamageVolumeDispatchFrame = -1;
        private float _damageVolumeEnergy;
        private bool _supportsComputeShadersCold;
        private bool _supportsR8RandomWriteCutMaskCold;
        // COLD ALLOC: RecentCutStamp[16] - CPU mirror of the newest cut stamps for zero-readback gameplay queries - owner: SargassumCutManager
        private readonly RecentCutStamp[] _recentCutStamps = new RecentCutStamp[RecentStampCapacity];
        // COLD ALLOC: RecentCutHeatStamp[16] - timestamped cut heat stamps for voxel rock thermal scarring - owner: SargassumCutManager
        private readonly RecentCutHeatStamp[] _recentCutHeatStamps = new RecentCutHeatStamp[RecentStampCapacity];
        // COLD ALLOC: Vector4[16] - packed recent-cut thermal positions/radii published to shaders - owner: SargassumCutManager
        private readonly Vector4[] _recentCutHeatPositionRadius = new Vector4[RecentStampCapacity];
        // COLD ALLOC: Vector4[16] - packed recent-cut thermal strength/start/lifetime payload published to shaders - owner: SargassumCutManager
        private readonly Vector4[] _recentCutHeatStrengthTime = new Vector4[RecentStampCapacity];
        // COLD ALLOC: int[3] - reusable damage-volume resolution upload payload; avoids ComputeShader.SetInts params allocation in LateFrameTick.
        private readonly int[] _damageVolumeResolutionUpload = new int[3];
        // COLD ALLOC: PendingDebrisBurst[16] - visual particle bursts flushed only in LateFrameTick - owner: SargassumCutManager
        private readonly PendingDebrisBurst[] _pendingDebrisBursts = new PendingDebrisBurst[StampCommandCapacity];
        private int _pendingDebrisBurstCount;
        private int _recentCutHeatCount;
        private int _publishedRecentCutHeatCount = -1;
        private bool _recentCutHeatDirty;

        private static SargassumCutManager s_activeRuntimeInstance;

        /// <summary>
        /// Active owner-published runtime instance.
        /// </summary>
        public static SargassumCutManager Instance => s_activeRuntimeInstance;

        /// <summary>
        /// Resolve-or-create the sole SargassumCutManager owner for GlobalRegistry.SargassumCut.
        /// Script GUID ff5d403710d1d0e4bb43e3210c59df5c has ZERO live scene/prefab hits; without this
        /// path ISargassumCutWriteService / cut-mask consumers stay permanent null.
        /// </summary>
        public static SargassumCutManager EnsureRuntimeInstance()
        {
            SargassumCutManager registered = GlobalRegistry.SargassumCut;
            if (IsSargassumCutRuntimeUsable(registered))
                return registered;

            SargassumCutManager active = s_activeRuntimeInstance;
            if (IsSargassumCutRuntimeUsable(active))
                return active;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterSargassumCutRuntime(registered);
                if (registered != null)
                    registered._serviceRegistered = false;
            }

            if (!ReferenceEquals(active, null) && active == null)
            {
                s_activeRuntimeInstance = null;
            }
            else if (!ReferenceEquals(active, null) && !IsSargassumCutRuntimeUsable(active))
            {
                if (ReferenceEquals(s_activeRuntimeInstance, active))
                    s_activeRuntimeInstance = null;
                if (active != null)
                    active._serviceRegistered = false;
            }

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            GameObject runtimeRoot = new GameObject("[SargassumCutManager]"); // COLD ALLOC
            return runtimeRoot.AddComponent<SargassumCutManager>();
        }



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
            if (!IsFiniteVector3(positionWS) || !math.isfinite(radiusWS))
                return false;

            float effectiveRadius = Mathf.Max(0f, radiusWS);
            float lifetime = math.isfinite(recentCutLifetime) ? Mathf.Max(0.01f, recentCutLifetime) : 0.01f;
            bool hasCut = false;

            for (int i = 0; i < RecentStampCapacity; i++)
            {
                RecentCutStamp stamp = _recentCutStamps[i];
                if (!IsFiniteRecentCutStamp(in stamp))
                {
                    _recentCutStamps[i] = default;
                    continue;
                }

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
                float temporalFalloff = Mathf.Clamp01(stamp.RemainingLifetime / lifetime);
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
            if (!IsFiniteVector3(positionWS) || !math.isfinite(radiusWS))
                return false;

            float effectiveRadius = Mathf.Max(0f, radiusWS);
            float lifetime = math.isfinite(recentCutLifetime) ? Mathf.Max(0.01f, recentCutLifetime) : 0.01f;
            bool hasCut = false;

            for (int i = 0; i < RecentStampCapacity; i++)
            {
                RecentCutStamp stamp = _recentCutStamps[i];
                if (!IsFiniteRecentCutStamp(in stamp))
                {
                    _recentCutStamps[i] = default;
                    continue;
                }

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
                float temporalFalloff = Mathf.Clamp01(stamp.RemainingLifetime / lifetime);
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
            if (!IsFiniteVector3(positionWS) ||
                !math.isfinite(radiusWS) ||
                !math.isfinite(strength))
            {
                return false;
            }

            float clampedRadius = Mathf.Max(0.05f, radiusWS);
            float clampedStrength = Mathf.Clamp01(strength);
            if (clampedStrength <= 0f)
                return false;
            float safeBubbleWeight = math.isfinite(bubbleWeight) ? Mathf.Max(0f, bubbleWeight) : 1f;

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
            QueueDebrisBurst(positionWS, burstDirection, clampedStrength, safeBubbleWeight);
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
            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            maskResolution = Mathf.Clamp(maskResolution, 512, 2048);
            centerSnapPixelStride = Mathf.Max(0.1f, centerSnapPixelStride);
            damageVolumeResolution = Mathf.Clamp(damageVolumeResolution, 32, 128);
            damageVolumeDepth = Mathf.Clamp(damageVolumeDepth, 16, 96);
            damageVolumeHeight = Mathf.Max(8f, damageVolumeHeight);
            damageVolumeRecoveryPerSecond = Mathf.Clamp01(damageVolumeRecoveryPerSecond);
            InitializeRuntimeResourceBudgets(force: true);
            CacheGraphicsCapabilitiesCold();
            CacheRegistryServicesCold();
            CreateResources();
            PublishGlobals();
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            if (!_serviceRegistered)
                return;

            TryRegisterHotSwapListener();
            CacheGraphicsCapabilitiesCold();
            CacheRegistryServicesCold();
            CreateResources();
            PublishGlobals();
            TryRegister();
            _runtimeRoutesRetiredAfterOwnershipLoss = false;
        }

        private void OnDisable()
        {
            TryUnregisterService();
            TryUnregister();
            TryUnregisterHotSwapListener();
            ResetTransientRuntimeQueues();
            Shader.SetGlobalFloat(_CutMaskActiveId, 0f);
            Shader.SetGlobalFloat(_DamageVolumeActiveId, 0f);
            PublishRecentCutHeatCount(0);
            _oceanKinematicsService = null;
        }

        private void OnDestroy()
        {
            TryUnregisterService();
            TryUnregister();
            TryUnregisterHotSwapListener();
            _oceanKinematicsService = null;
            ReleaseResources();
        }

        /// <summary>
        /// Updates the cut mask by decaying existing values and stamping the currently active player tools.
        /// </summary>
        /// <param name="deltaTime">Gameplay frame delta time.</param>
        public void Tick(float deltaTime)
        {
            deltaTime = math.isfinite(deltaTime) ? Mathf.Max(0f, deltaTime) : 0f;
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
                _knifeStampCooldownRemaining = math.isfinite(knifeStampCooldown) ? Mathf.Max(0f, knifeStampCooldown) : 0f;
                strongestStampThisFrame = Mathf.Max(strongestStampThisFrame, knifeCutStrength);
            }

            if (!wrotePass && needsRecoveryPass)
                ExecuteStampPass(Vector3.zero, 0f, 0f, deltaTime);

            bool hasDamageVolumeWork = _queuedDamageVolumeStampCount > 0 || _damageVolumeEnergy > DamageVolumeEnergyEpsilon;
            if (!wrotePass && !needsRecoveryPass && !HasPendingMaskUpdate() && !hasDamageVolumeWork)
                return;

            float recoveryRate = math.isfinite(recoveryPerSecond) ? Mathf.Max(0f, recoveryPerSecond) : 0f;
            float recoveredEnergy = Mathf.Max(0f, _maskEnergy - recoveryRate * deltaTime);
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
            if (_playerDependencyRefreshRequested || _playerTransform == null || _playerToolManager == null)
                ResolveDependenciesCold(allowComponentLookup: false);
            else
                ResolveDependencies();
            _qualityResourceRefreshRequested = false;
            RefreshQualityDependentResourcesIfNeeded();
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
                _globalsDirty;

            if (!hasVisualWork)
                return;

            FlushPendingTextureClears();
            ProcessQueuedMaskUpdate();
            float damageVolumeDeltaTime = math.isfinite(_pendingDamageVolumeDeltaTime)
                ? Mathf.Max(0f, _pendingDamageVolumeDeltaTime)
                : 0f;
            ProcessQueuedDamageVolumeUpdate(damageVolumeDeltaTime);
            _pendingDamageVolumeDeltaTime = 0f;
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
            if (mapMagicVegetationBridge == null || !mapMagicVegetationBridge.isActiveAndEnabled)
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref mapMagicVegetationBridge);

            Transform runtimePlayerTransform = _playerContext != null
                ? _playerContext.PlayerTransform
                : null;

            if (runtimePlayerTransform != null)
                _playerTransform = runtimePlayerTransform;
            else if (_playerTransform == null)
            {
                _playerTransform = playerTransformOverride;
                if (_playerTransform == null)
                    _playerDependencyRefreshRequested = true;
            }

            if (_playerToolManager == null)
            {
                _playerToolManager = playerToolManagerOverride;
                if (_playerToolManager == null && _playerContext != null)
                    _playerToolManager = _playerContext.ToolManager;

            }
        }

        private void ResolveDependenciesCold(bool allowComponentLookup)
        {
            if (mapMagicVegetationBridge == null || !mapMagicVegetationBridge.isActiveAndEnabled)
                WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref mapMagicVegetationBridge);

            _playerDependencyRefreshRequested = false;
            Transform runtimePlayerTransform = _playerContext != null
                ? _playerContext.PlayerTransform
                : null;
            if (runtimePlayerTransform == null)
                runtimePlayerTransform = BootstrapState.CurrentPlayerTransform;

            _playerTransform = runtimePlayerTransform != null ? runtimePlayerTransform : playerTransformOverride;
            if (_playerToolManager == null)
            {
                _playerToolManager = playerToolManagerOverride;
                if (_playerToolManager == null && _playerContext != null)
                    _playerToolManager = _playerContext.ToolManager;

                if (allowComponentLookup &&
                    _playerToolManager == null &&
                    _playerTransform != null &&
                    !_playerTransform.TryGetComponent(out _playerToolManager))
                {
                    _playerToolManager = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<PlayerToolManager>(_playerTransform);
                }
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
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            CacheDataVaultCold();
            ResolveDependenciesCold(allowComponentLookup: true);
            ResolveVisualDependencies();
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsComputeShadersCold = SystemInfo.supportsComputeShaders;
            _supportsR8RandomWriteCutMaskCold =
                SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8) &&
                SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.R8);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.SargassumCutRuntime)
            {
                ReconcileRuntimeOwnerFromRegistryReplacement(previousService, currentService);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                if (playerToolManagerOverride == null)
                    _playerToolManager = null;

                _playerContext = currentService as IPlayerRuntimeContext;
                ResolveDependenciesCold(allowComponentLookup: true);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Input)
            {
                _inputService = currentService as IInputService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)
            {
                _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : _dataVault;
                IDataVault nextVault = currentService as IDataVault;
                BindDataVaultForLifecycle(nextVault, previousVault);
                _qualityResourceRefreshRequested = isActiveAndEnabled;
            }
        }

        private void CreateResources()
        {
#if UNITY_EDITOR
            TryAutoAssignAssets();
#endif

            if (!_supportsComputeShadersCold)
            {
                enabled = false;
                return;
            }

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

            if (!IsGraphicsBufferReady(_stampCommandBufferA) || !IsGraphicsBufferReady(_stampCommandBufferB))
            {
                ReleaseGraphicsBuffer(ref _stampCommandBufferA);
                ReleaseGraphicsBuffer(ref _stampCommandBufferB);
                _stampCommandBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<StampCommand>(StampCommandCapacity); // COLD ALLOC: GraphicsBuffer[16] - staged cut-mask stamp command buffer A - owner: SargassumCutManager
                _stampCommandBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<StampCommand>(StampCommandCapacity); // COLD ALLOC: GraphicsBuffer[16] - staged cut-mask stamp command buffer B - owner: SargassumCutManager
                _activeStampCommandBuffer = _stampCommandBufferA;
                _stampCommandUploadIndex = 0;
            }

            EnsureVaultBuffer(ref _queuedDamageVolumeStampCommandsHandle, DamageVolumeStampCommandsBufferId, DamageVolumeStampCapacity);

            if (!IsGraphicsBufferReady(_damageVolumeStampCommandBufferA) || !IsGraphicsBufferReady(_damageVolumeStampCommandBufferB))
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
                    Hecton8.Core.H8Debug.LogError("[SargassumCutManager] Missing cut-mask compute shader. Expected Hecton_SargassumCutMask.compute.", this);
#endif
                    enabled = false;
                    return;
                }

                _stampKernel = ResolveKernel(_stampCompute, "CSMain");
                if (_stampKernel < 0)
                {
                    enabled = false;
                    return;
                }
                ResolveKernelThreadGroupSizes(
                    _stampCompute,
                    _stampKernel,
                    out _stampThreadGroupSizeX,
                    out _stampThreadGroupSizeY,
                    out _);
            }

            if (_damageVolumeCompute == null)
            {
                _damageVolumeCompute = damageVolumeComputeOverride;
                if (_damageVolumeCompute == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError("[SargassumCutManager] Missing terrain damage-volume compute shader. Expected Hecton_TerrainDamageVolume.compute.", this);
#endif
                    enabled = false;
                    return;
                }

                _damageVolumeKernel = ResolveKernel(_damageVolumeCompute, "StampDamageVolume");
                if (_damageVolumeKernel < 0)
                {
                    enabled = false;
                    return;
                }
                ResolveKernelThreadGroupSizes(
                    _damageVolumeCompute,
                    _damageVolumeKernel,
                    out _damageVolumeThreadGroupSizeX,
                    out _damageVolumeThreadGroupSizeY,
                    out _damageVolumeThreadGroupSizeZ);
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
                BindDataVaultForLifecycle(GlobalRegistry.DataVault);

            return _dataVault;
        }

        private void BindDataVaultForLifecycle(IDataVault nextVault, IDataVault previousVault = null)
        {
            IDataVault releaseVault = previousVault ?? _dataVault;
            if (!ReferenceEquals(_dataVault, nextVault))
                ReleaseVaultBuffers(releaseVault);

            _dataVault = nextVault;
        }

        private bool EnsureVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            IDataVault vault = _dataVault;
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
            out NativeArray<T> buffer,
            out IDataVault writeVault) where T : struct
        {
            buffer = default;
            writeVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultHandleCreated(in handle) ||
                !vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
            {
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    writeVault = vault;
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
            }
        }

        private static void ReleaseVaultWrite<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            vault?.ReleaseWriteLock(in handle, VaultOwnerSystemId);
        }

        private void ReleaseVaultBuffer<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            ReleaseVaultBuffer(_dataVault, ref handle);
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ReleaseVaultBuffers(IDataVault vault)
        {
            ReleaseVaultBuffer(vault, ref _queuedStampCommandsHandle);
            ReleaseVaultBuffer(vault, ref _queuedDamageVolumeStampCommandsHandle);
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void ReleaseResources()
        {
            ResetTransientRuntimeQueues();
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
            _stampThreadGroupSizeX = 0;
            _stampThreadGroupSizeY = 0;
            _damageVolumeThreadGroupSizeX = 0;
            _damageVolumeThreadGroupSizeY = 0;
            _damageVolumeThreadGroupSizeZ = 0;
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

        private static bool IsGraphicsBufferReady(GraphicsBuffer buffer)
        {
            return buffer != null && buffer.IsValid();
        }

        private int ResolveKernel(ComputeShader compute, string kernelName)
        {
            if (compute == null || !_supportsComputeShadersCold)
                return -1;

            try
            {
                if (!compute.HasKernel(kernelName))
                    return -1;

                int kernel = compute.FindKernel(kernelName);
                return kernel >= 0 ? kernel : -1;
            }
            catch (System.ObjectDisposedException)
            {
                return -1;
            }
            catch (System.InvalidOperationException)
            {
                return -1;
            }
            catch (System.ArgumentException)
            {
                return -1;
            }
            catch (MissingReferenceException)
            {
                return -1;
            }
            catch (UnityException)
            {
                return -1;
            }
        }

        private void ResolveKernelThreadGroupSizes(
            ComputeShader compute,
            int kernel,
            out int sizeX,
            out int sizeY,
            out int sizeZ)
        {
            sizeX = 0;
            sizeY = 0;
            sizeZ = 0;
            if (compute == null || kernel < 0 || !_supportsComputeShadersCold)
                return;

            uint queryX;
            uint queryY;
            uint queryZ;
            try
            {
                if (!compute.IsSupported(kernel))
                    return;

                compute.GetKernelThreadGroupSizes(kernel, out queryX, out queryY, out queryZ);
            }
            catch (System.ObjectDisposedException)
            {
                return;
            }
            catch (System.InvalidOperationException)
            {
                return;
            }
            catch (System.ArgumentException)
            {
                return;
            }
            catch (MissingReferenceException)
            {
                return;
            }
            catch (UnityException)
            {
                return;
            }
            if (queryX == 0u || queryY == 0u || queryZ == 0u ||
                queryX > int.MaxValue || queryY > int.MaxValue || queryZ > int.MaxValue)
            {
                return;
            }

            ulong xyThreads = queryX * (ulong)queryY;
            if (xyThreads > PortableMaxComputeThreadsPerGroup ||
                queryZ > PortableMaxComputeThreadsPerGroup / xyThreads)
            {
                return;
            }

            sizeX = (int)queryX;
            sizeY = (int)queryY;
            sizeZ = (int)queryZ;
        }

        private static int CeilDividePositive(int value, int divisor)
        {
            const int MaxDispatchGroupsPerDimension = 65535;
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private GraphicsBuffer ResolveStampCommandWriteBuffer()
        {
            GraphicsBuffer preferred = (_stampCommandUploadIndex & 1) == 0
                ? _stampCommandBufferB
                : _stampCommandBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            if (_stampCommandBufferA != null && _stampCommandBufferA.IsValid())
                return _stampCommandBufferA;

            return _stampCommandBufferB != null && _stampCommandBufferB.IsValid()
                ? _stampCommandBufferB
                : null;
        }

        private GraphicsBuffer ResolveDamageVolumeStampCommandWriteBuffer()
        {
            GraphicsBuffer preferred = (_damageVolumeStampCommandUploadIndex & 1) == 0
                ? _damageVolumeStampCommandBufferB
                : _damageVolumeStampCommandBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            if (_damageVolumeStampCommandBufferA != null && _damageVolumeStampCommandBufferA.IsValid())
                return _damageVolumeStampCommandBufferA;

            return _damageVolumeStampCommandBufferB != null && _damageVolumeStampCommandBufferB.IsValid()
                ? _damageVolumeStampCommandBufferB
                : null;
        }

        private void RefreshMaskWorldRect(bool forceClear = false)
        {
            float desiredWorldSize = Mathf.Max(minimumMaskWorldSize, 128f);
            if (!math.isfinite(desiredWorldSize))
                desiredWorldSize = 128f;

            float snapWorldStride = ResolveSnapWorldStride(desiredWorldSize);
            Vector2 desiredCenterXZ = _playerTransform != null
                ? QuantizeCenter(new Vector2(_playerTransform.position.x, _playerTransform.position.z), snapWorldStride)
                : (_maskWorldSize > 0f ? _maskCenterXZ : Vector2.zero);
            if (!math.all(math.isfinite(new float2(desiredCenterXZ.x, desiredCenterXZ.y))))
                desiredCenterXZ = _maskWorldSize > 0f && math.all(math.isfinite(new float2(_maskCenterXZ.x, _maskCenterXZ.y)))
                    ? _maskCenterXZ
                    : Vector2.zero;

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
            if (TryResolveOceanWaterLevel(out float oceanWaterLevel))
                return oceanWaterLevel;

            if (mapMagicVegetationBridge != null &&
                mapMagicVegetationBridge.ActiveSurfaceInstanceCount > 0 &&
                TryResolveWaterLevel(mapMagicVegetationBridge.ActiveSurfaceDrawBounds.center.y, out float vegetationWaterLevel))
            {
                return vegetationWaterLevel;
            }

            MapMagicBridge terrainBridge = null;
            if (WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref terrainBridge) &&
                TryResolveWaterLevel(terrainBridge.WaterSurfaceLevel, out float terrainWaterLevel))
            {
                return terrainWaterLevel;
            }

            return TryResolveWaterLevel(fallbackY, out float fallbackWaterLevel)
                ? math.max(DefaultWaterLevel, fallbackWaterLevel)
                : DefaultWaterLevel;
        }

        private bool TryResolveOceanWaterLevel(out float waterLevel)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterLevel(oceanKinematics.SeaLevel, out waterLevel))
            {
                return true;
            }

            waterLevel = DefaultWaterLevel;
            return false;
        }

        private static bool TryResolveOceanWaterLevel(float candidateWaterLevel, out float waterLevel)
        {
            if (math.isfinite(candidateWaterLevel) &&
                math.abs(candidateWaterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterLevel = candidateWaterLevel;
                return true;
            }

            waterLevel = DefaultWaterLevel;
            return false;
        }

        private static bool TryResolveWaterLevel(float candidateWaterLevel, out float waterLevel)
        {
            if (math.isfinite(candidateWaterLevel) &&
                math.abs(candidateWaterLevel) > 0.0001f &&
                math.abs(candidateWaterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterLevel = candidateWaterLevel;
                return true;
            }

            waterLevel = DefaultWaterLevel;
            return false;
        }

        private void ExecuteStampPass(Vector3 positionWS, float radiusWS, float strength, float deltaTime)
        {
            if (!IsFiniteVector3(positionWS) ||
                !math.isfinite(radiusWS) ||
                !math.isfinite(strength) ||
                !math.isfinite(deltaTime))
            {
                return;
            }

            float recoveryRate = math.isfinite(recoveryPerSecond) ? Mathf.Max(0f, recoveryPerSecond) : 0f;
            float recovery = Mathf.Max(0f, recoveryRate * Mathf.Max(0f, deltaTime));
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

            if (!TryAcquireVaultBuffer(
                    in _queuedStampCommandsHandle,
                    StampCommandCapacity,
                    out NativeArray<StampCommand> queuedStampCommands,
                    out IDataVault queuedStampCommandsVault))
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
                ReleaseVaultWrite(queuedStampCommandsVault, in _queuedStampCommandsHandle);
            }
        }

        private bool TryCoalesceOverflowStamp(Vector2 uvCenter, float uvRadius, float strength, Vector3 positionWS)
        {
            if (_queuedStampCount <= 0 ||
                !math.all(math.isfinite(new float2(uvCenter.x, uvCenter.y))) ||
                !math.isfinite(uvRadius) ||
                !math.isfinite(strength) ||
                !IsFiniteVector3(positionWS) ||
                !TryAcquireVaultBuffer(
                    in _queuedStampCommandsHandle,
                    StampCommandCapacity,
                    out NativeArray<StampCommand> queuedStampCommands,
                    out IDataVault queuedStampCommandsVault))
            {
                return false;
            }

            try
            {
                int index = math.min(_queuedStampCount - 1, StampCommandCapacity - 1);
                StampCommand existing = queuedStampCommands[index];
                Vector4 payload = existing.UvRadiusStrength;
                if (!IsFiniteVector4(payload))
                    payload = new Vector4(uvCenter.x, uvCenter.y, Mathf.Max(0.0001f, uvRadius), Mathf.Clamp01(strength));

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
                ReleaseVaultWrite(queuedStampCommandsVault, in _queuedStampCommandsHandle);
            }
        }

        private void DecayRecentCutStamps(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return;

            for (int i = 0; i < RecentStampCapacity; i++)
            {
                if (!IsFiniteRecentCutStamp(in _recentCutStamps[i]))
                {
                    _recentCutStamps[i] = default;
                    continue;
                }

                if (_recentCutStamps[i].RemainingLifetime <= 0f)
                    continue;

                _recentCutStamps[i].RemainingLifetime = Mathf.Max(0f, _recentCutStamps[i].RemainingLifetime - deltaTime);
            }
        }

        private void RegisterRecentCutStamp(Vector3 positionWS, float radiusWS, float strength)
        {
            if (!IsFiniteVector3(positionWS) ||
                !math.isfinite(radiusWS) ||
                !math.isfinite(strength))
            {
                return;
            }

            float clampedRadius = Mathf.Max(0.05f, radiusWS);
            float clampedStrength = Mathf.Clamp01(strength);
            float lifetime = math.isfinite(recentCutLifetime) ? Mathf.Max(0.01f, recentCutLifetime) : 0.01f;
            int targetIndex = -1;
            float weakestScore = float.MaxValue;

            for (int i = 0; i < RecentStampCapacity; i++)
            {
                RecentCutStamp stamp = _recentCutStamps[i];
                if (!IsFiniteRecentCutStamp(in stamp))
                {
                    targetIndex = i;
                    break;
                }

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
                RadiusWS = clampedRadius,
                Strength = clampedStrength,
                RemainingLifetime = lifetime
            };
        }

        private void RegisterRecentCutHeatStamp(Vector3 positionWS, float radiusWS, float strength)
        {
            if (!IsFiniteVector3(positionWS) ||
                !math.isfinite(radiusWS) ||
                !math.isfinite(strength))
            {
                return;
            }

            float currentTime = ResolveThermalShaderClockSeconds();
            if (!math.isfinite(currentTime))
                return;

            float clampedRadius = Mathf.Max(0.05f, radiusWS);
            float clampedStrength = Mathf.Clamp01(strength);
            float lifetime = math.isfinite(shaderScarLifetime) ? Mathf.Max(0.01f, shaderScarLifetime) : 0.01f;
            int targetIndex = -1;
            float weakestScore = float.MaxValue;

            for (int i = 0; i < RecentStampCapacity; i++)
            {
                RecentCutHeatStamp stamp = _recentCutHeatStamps[i];
                if (!IsFiniteRecentCutHeatStamp(in stamp))
                {
                    targetIndex = i;
                    break;
                }

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
                RadiusWS = clampedRadius,
                Strength = clampedStrength,
                StartTime = currentTime,
                Lifetime = lifetime
            };
            WorldSpatialHashGrid.RegisterTransientEvent(
                positionWS,
                clampedRadius,
                clampedStrength,
                PlasmaCutThermalEventLifetimeSeconds,
                SpatialTransientEventType.ThermalGradient,
                SpatialInteractionFlags.ThermalReceiver,
                FieldTargetRole.Generic,
                0,
                PlasmaCutThermalDeltaCelsius * clampedStrength);
            _recentCutHeatDirty = true;
        }

        private static float ResolveThermalShaderClockSeconds()
        {
            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        private void QueueDebrisBurst(Vector3 positionWS, Vector3 directionWS, float cutStrength, float bubbleWeight)
        {
            if (!IsFiniteVector3(positionWS) ||
                !IsFiniteVector3(directionWS) ||
                !math.isfinite(cutStrength) ||
                !math.isfinite(bubbleWeight))
            {
                return;
            }

            if (_pendingDebrisBurstCount >= _pendingDebrisBursts.Length)
            {
                TryCoalesceOverflowDebrisBurst(positionWS, directionWS, cutStrength, bubbleWeight);
                ReportDebrisBurstOverflow();
                return;
            }

            _pendingDebrisBursts[_pendingDebrisBurstCount++] = new PendingDebrisBurst
            {
                PositionWS = positionWS,
                DirectionWS = directionWS,
                CutStrength = cutStrength,
                BubbleWeight = bubbleWeight
            };
        }

        private bool TryCoalesceOverflowDebrisBurst(Vector3 positionWS, Vector3 directionWS, float cutStrength, float bubbleWeight)
        {
            if (_pendingDebrisBursts == null || _pendingDebrisBursts.Length == 0)
                return false;

            int targetIndex = -1;
            float weakestScore = float.MaxValue;
            bool replacingInvalidSlot = false;
            int activeCount = math.min(_pendingDebrisBurstCount, _pendingDebrisBursts.Length);
            for (int i = 0; i < activeCount; i++)
            {
                PendingDebrisBurst burst = _pendingDebrisBursts[i];
                if (!IsFinitePendingDebrisBurst(in burst))
                {
                    targetIndex = i;
                    replacingInvalidSlot = true;
                    break;
                }

                float score = burst.CutStrength * Mathf.Max(0.1f, burst.BubbleWeight);
                if (score < weakestScore)
                {
                    weakestScore = score;
                    targetIndex = i;
                }
            }

            if (targetIndex < 0)
                return false;

            float incomingScore = cutStrength * Mathf.Max(0.1f, bubbleWeight);
            if (!replacingInvalidSlot && incomingScore < weakestScore)
                return false;

            _pendingDebrisBursts[targetIndex] = new PendingDebrisBurst
            {
                PositionWS = positionWS,
                DirectionWS = directionWS,
                CutStrength = cutStrength,
                BubbleWeight = bubbleWeight
            };
            return true;
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
                if (!IsFinitePendingDebrisBurst(in burst))
                    continue;

                debrisParticleSystem.EmitBurst(burst.PositionWS, burst.DirectionWS, burst.CutStrength, burst.BubbleWeight);
            }

            _pendingDebrisBurstCount = 0;
        }

        private static bool IsFinitePendingDebrisBurst(in PendingDebrisBurst burst)
        {
            return IsFiniteVector3(burst.PositionWS) &&
                   IsFiniteVector3(burst.DirectionWS) &&
                   math.isfinite(burst.CutStrength) &&
                   math.isfinite(burst.BubbleWeight) &&
                   burst.CutStrength >= 0f &&
                   burst.BubbleWeight >= 0f;
        }

        private static bool IsFiniteRecentCutStamp(in RecentCutStamp stamp)
        {
            return stamp.RemainingLifetime <= 0f ||
                   (IsFiniteVector3(stamp.PositionWS) &&
                    math.isfinite(stamp.RadiusWS) &&
                    math.isfinite(stamp.Strength) &&
                    math.isfinite(stamp.RemainingLifetime) &&
                    stamp.RadiusWS > 0f &&
                    stamp.Strength >= 0f &&
                    stamp.RemainingLifetime >= 0f);
        }

        private static bool IsFiniteRecentCutHeatStamp(in RecentCutHeatStamp stamp)
        {
            return stamp.Lifetime <= 0f ||
                   (IsFiniteVector3(stamp.PositionWS) &&
                    math.isfinite(stamp.RadiusWS) &&
                    math.isfinite(stamp.Strength) &&
                    math.isfinite(stamp.StartTime) &&
                    math.isfinite(stamp.Lifetime) &&
                    stamp.RadiusWS > 0f &&
                    stamp.Strength >= 0f &&
                    stamp.Lifetime >= 0f);
        }

        private void ReportDebrisBurstOverflow()
        {
            _debrisBurstOverflowCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastDebrisBurstOverflowTelemetryFrame == frame)
                return;

            _lastDebrisBurstOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                DebrisBurstOverflowWarningHash,
                DebrisBurstContextHash,
                Mathf.Max(1, _debrisBurstOverflowCount));
        }

        private void QueueDamageVolumeVisualSync(float deltaTime)
        {
            if (_queuedDamageVolumeStampCount <= 0 && _damageVolumeEnergy <= DamageVolumeEnergyEpsilon)
                return;

            if (!math.isfinite(deltaTime))
                deltaTime = 0f;

            if (deltaTime > _pendingDamageVolumeDeltaTime)
                _pendingDamageVolumeDeltaTime = Mathf.Max(0f, deltaTime);
        }

        private bool IsInsideMaskWorldRect(Vector3 positionWS)
        {
            if (!IsFiniteVector3(positionWS) ||
                !math.isfinite(_maskWorldSize) ||
                !IsFiniteVector4(_maskWorldRect) ||
                _maskWorldSize <= 0f ||
                _maskWorldRect.z <= 0f ||
                _maskWorldRect.w <= 0f)
            {
                return false;
            }

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

        private static bool IsFiniteVector3(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFiniteVector4(Vector4 value)
        {
            return math.all(math.isfinite(new float4(value.x, value.y, value.z, value.w)));
        }

        private static bool IsFiniteDamageVolumeStampCommand(in DamageVolumeStampCommand command)
        {
            return IsFiniteVector4(command.PositionRadius) &&
                   IsFiniteVector4(command.StrengthPadding) &&
                   command.PositionRadius.w > 0f &&
                   command.StrengthPadding.x >= 0f;
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

        private void ResetTransientRuntimeQueues()
        {
            ResetQueuedMaskUpdateState();
            _queuedDamageVolumeStampCount = 0;
            _damageVolumeStampOverflowCoalesceCount = 0;
            _pendingDamageVolumeDeltaTime = 0f;
            _damageVolumeEnergy = 0f;
            _pendingDebrisBurstCount = 0;
            _debrisBurstOverflowCount = 0;
            _maskClearRequested = false;
            _damageVolumeClearRequested = false;
            _globalsDirty = false;
            _pendingHeatRefresh = false;
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
                !EnsureActiveStampCommandBufferReady() ||
                !HasPendingMaskUpdate() ||
                _lastMaskDispatchFrame == SystemDispatcher.CurrentFrameIndex)
            {
                return;
            }

            int groupCountX = CeilDividePositive(_maskRuntimeResolution, _stampThreadGroupSizeX);
            int groupCountY = CeilDividePositive(_maskRuntimeResolution, _stampThreadGroupSizeY);
            if (groupCountX <= 0 || groupCountY <= 0)
                return;

            int uploadedStampCount = 0;
            if (_queuedStampCount > 0)
            {
                if (!TryAcquireVaultBuffer(
                        in _queuedStampCommandsHandle,
                        StampCommandCapacity,
                        out NativeArray<StampCommand> queuedStampCommands,
                        out IDataVault queuedStampCommandsVault))
                    return;

                try
                {
                    GraphicsBuffer stampWriteBuffer = ResolveStampCommandWriteBuffer();
                    if (stampWriteBuffer == null)
                    {
                        RequestStampGraphicsBufferRefresh();
                        return;
                    }

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
                    ReleaseVaultWrite(queuedStampCommandsVault, in _queuedStampCommandsHandle);
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

            _stampCompute.Dispatch(_stampKernel, groupCountX, groupCountY, 1);

            RenderTexture temp = _maskRead;
            _maskRead = _maskWrite;
            _maskWrite = temp;
            _lastMaskDispatchFrame = SystemDispatcher.CurrentFrameIndex;
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
            if (!IsFiniteVector3(desiredWorldMin) || !IsFiniteVector3(desiredWorldSize))
                return;

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
            if (!IsFiniteVector3(positionWS) ||
                !math.isfinite(radiusWS) ||
                !math.isfinite(strength))
            {
                return;
            }

            RefreshDamageVolumeBounds();
            if (_damageVolumeRead == null ||
                _damageVolumeWrite == null ||
                !IsFiniteVector3(_damageVolumeWorldMin) ||
                !IsFiniteVector3(_damageVolumeWorldSize))
            {
                return;
            }

            float clampedRadius = Mathf.Max(0.05f, radiusWS);
            float clampedStrength = Mathf.Clamp01(strength);

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
                    out NativeArray<DamageVolumeStampCommand> queuedDamageVolumeStampCommands,
                    out IDataVault queuedDamageVolumeStampCommandsVault))
            {
                return;
            }

            try
            {
                queuedDamageVolumeStampCommands[_queuedDamageVolumeStampCount] = new DamageVolumeStampCommand
                {
                    PositionRadius = new Vector4(positionWS.x, positionWS.y, positionWS.z, clampedRadius),
                    StrengthPadding = new Vector4(clampedStrength, 0f, 0f, 0f)
                };
                _damageVolumeEnergy = Mathf.Max(_damageVolumeEnergy, clampedStrength);
                _queuedDamageVolumeStampCount++;
            }
            finally
            {
                ReleaseVaultWrite(queuedDamageVolumeStampCommandsVault, in _queuedDamageVolumeStampCommandsHandle);
            }
        }

        private bool TryCoalesceOverflowDamageVolumeStamp(Vector3 positionWS, float radiusWS, float strength)
        {
            if (_queuedDamageVolumeStampCount <= 0 ||
                !IsFiniteVector3(positionWS) ||
                !math.isfinite(radiusWS) ||
                !math.isfinite(strength) ||
                !TryAcquireVaultBuffer(
                    in _queuedDamageVolumeStampCommandsHandle,
                    DamageVolumeStampCapacity,
                    out NativeArray<DamageVolumeStampCommand> queuedDamageVolumeStampCommands,
                    out IDataVault queuedDamageVolumeStampCommandsVault))
            {
                return false;
            }

            try
            {
                int index = math.min(_queuedDamageVolumeStampCount - 1, DamageVolumeStampCapacity - 1);
                DamageVolumeStampCommand existing = queuedDamageVolumeStampCommands[index];
                float clampedRadius = math.max(0.05f, radiusWS);
                float clampedStrength = Mathf.Clamp01(strength);
                if (!IsFiniteDamageVolumeStampCommand(in existing))
                {
                    queuedDamageVolumeStampCommands[index] = new DamageVolumeStampCommand
                    {
                        PositionRadius = new Vector4(positionWS.x, positionWS.y, positionWS.z, clampedRadius),
                        StrengthPadding = new Vector4(clampedStrength, 0f, 0f, 0f)
                    };
                    _damageVolumeEnergy = Mathf.Max(_damageVolumeEnergy, clampedStrength);
                    _damageVolumeStampOverflowCoalesceCount++;
                    return true;
                }

                Vector4 positionRadius = existing.PositionRadius;
                Vector4 strengthPadding = existing.StrengthPadding;
                float3 existingCenter = new float3(positionRadius.x, positionRadius.y, positionRadius.z);
                float coverageRadius = math.distance(existingCenter, new float3(positionWS.x, positionWS.y, positionWS.z)) + clampedRadius;
                positionRadius.w = math.max(positionRadius.w, coverageRadius);
                strengthPadding.x = math.max(strengthPadding.x, clampedStrength);
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
                ReleaseVaultWrite(queuedDamageVolumeStampCommandsVault, in _queuedDamageVolumeStampCommandsHandle);
            }
        }

        private void ProcessQueuedDamageVolumeUpdate(float deltaTime)
        {
            deltaTime = math.isfinite(deltaTime) ? Mathf.Max(0f, deltaTime) : 0f;
            if (_damageVolumeRead == null ||
                _damageVolumeWrite == null ||
                _damageVolumeCompute == null ||
                _damageVolumeKernel < 0 ||
                !EnsureActiveDamageVolumeStampCommandBufferReady() ||
                (_queuedDamageVolumeStampCount <= 0 && deltaTime <= 0f) ||
                _lastDamageVolumeDispatchFrame == SystemDispatcher.CurrentFrameIndex)
            {
                return;
            }

            int runtimeResolution = Mathf.Max(32, _damageVolumeRuntimeResolution);
            int runtimeDepth = Mathf.Max(16, _damageVolumeRuntimeDepth);
            int groupCountX = CeilDividePositive(runtimeResolution, _damageVolumeThreadGroupSizeX);
            int groupCountY = CeilDividePositive(runtimeDepth, _damageVolumeThreadGroupSizeY);
            int groupCountZ = CeilDividePositive(runtimeResolution, _damageVolumeThreadGroupSizeZ);
            if (groupCountX <= 0 || groupCountY <= 0 || groupCountZ <= 0)
                return;

            int uploadedDamageVolumeStampCount = 0;
            if (_queuedDamageVolumeStampCount > 0)
            {
                if (!TryAcquireVaultBuffer(
                        in _queuedDamageVolumeStampCommandsHandle,
                        DamageVolumeStampCapacity,
                        out NativeArray<DamageVolumeStampCommand> queuedDamageVolumeStampCommands,
                        out IDataVault queuedDamageVolumeStampCommandsVault))
                {
                    return;
                }

                try
                {
                    GraphicsBuffer damageWriteBuffer = ResolveDamageVolumeStampCommandWriteBuffer();
                    if (damageWriteBuffer == null)
                    {
                        RequestStampGraphicsBufferRefresh();
                        return;
                    }

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
                    ReleaseVaultWrite(queuedDamageVolumeStampCommandsVault, in _queuedDamageVolumeStampCommandsHandle);
                }
            }

            _damageVolumeCompute.SetTexture(_damageVolumeKernel, _DamageVolumeSourceId, _damageVolumeRead);
            _damageVolumeCompute.SetTexture(_damageVolumeKernel, _DamageVolumeResultId, _damageVolumeWrite);
            _damageVolumeCompute.SetBuffer(_damageVolumeKernel, _DamageVolumeStampCommandsId, _activeDamageVolumeStampCommandBuffer);
            _damageVolumeCompute.SetInt(_DamageVolumeStampCountId, uploadedDamageVolumeStampCount);
            float damageVolumeRecoveryRate = math.isfinite(damageVolumeRecoveryPerSecond) ? Mathf.Max(0f, damageVolumeRecoveryPerSecond) : 0f;
            _damageVolumeCompute.SetFloat(_DamageVolumeRecoveryId, Mathf.Max(0f, damageVolumeRecoveryRate * Mathf.Max(0f, deltaTime)));
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
            _damageVolumeResolutionUpload[0] = runtimeResolution;
            _damageVolumeResolutionUpload[1] = runtimeDepth;
            _damageVolumeResolutionUpload[2] = runtimeResolution;
            _damageVolumeCompute.SetInts(_DamageVolumeResolutionId, _damageVolumeResolutionUpload);

            _damageVolumeCompute.Dispatch(_damageVolumeKernel, groupCountX, groupCountY, groupCountZ);

            _damageVolumeEnergy = Mathf.Max(
                0f,
                _damageVolumeEnergy - Mathf.Max(0f, damageVolumeRecoveryRate * Mathf.Max(0f, deltaTime)));
            RenderTexture temp = _damageVolumeRead;
            _damageVolumeRead = _damageVolumeWrite;
            _damageVolumeWrite = temp;
            _lastDamageVolumeDispatchFrame = SystemDispatcher.CurrentFrameIndex;
            _queuedDamageVolumeStampCount = 0;
            _damageVolumeStampOverflowCoalesceCount = 0;
        }

        private bool EnsureActiveStampCommandBufferReady()
        {
            if (IsGraphicsBufferReady(_activeStampCommandBuffer))
                return true;

            RequestStampGraphicsBufferRefresh();
            return false;
        }

        private bool EnsureActiveDamageVolumeStampCommandBufferReady()
        {
            if (IsGraphicsBufferReady(_activeDamageVolumeStampCommandBuffer))
                return true;

            RequestStampGraphicsBufferRefresh();
            return false;
        }

        private void RequestStampGraphicsBufferRefresh()
        {
            _qualityResourceRefreshRequested = true;
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
            bool damageVolumeActive =
                _damageVolumeRead != null &&
                (_damageVolumeEnergy > DamageVolumeEnergyEpsilon ||
                 _queuedDamageVolumeStampCount > 0 ||
                 _pendingDamageVolumeDeltaTime > 0f);

            if (damageVolumeActive)
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
            if (!Application.isPlaying || !_serviceRegistered || GlobalRegistry.Dispatcher == null)
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

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterSargassumCutRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.SargassumCut, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            _serviceRegistered = false;

            if (ReferenceEquals(GlobalRegistry.SargassumCut, this))
                GlobalRegistry.UnregisterSargassumCutRuntime(this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            SargassumCutManager registered = GlobalRegistry.SargassumCut;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                if (IsSargassumCutRuntimeUsable(registered))
                {
                    s_activeRuntimeInstance = registered;
                    Destroy(this);
                    return true;
                }

                if (ReferenceEquals(s_activeRuntimeInstance, registered))
                    s_activeRuntimeInstance = null;
                GlobalRegistry.UnregisterSargassumCutRuntime(registered);
            }

            SargassumCutManager active = s_activeRuntimeInstance;
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            if (IsSargassumCutRuntimeUsable(active))
            {
                GlobalRegistry.RegisterSargassumCutRuntime(active);
                s_activeRuntimeInstance = active;
                Destroy(this);
                return true;
            }

            if (ReferenceEquals(s_activeRuntimeInstance, active))
                s_activeRuntimeInstance = null;
            GlobalRegistry.UnregisterSargassumCutRuntime(active);

            return false;
        }

        private static bool IsSargassumCutRuntimeUsable(SargassumCutManager manager)
        {
            return manager != null && manager._serviceRegistered && manager.isActiveAndEnabled;
        }

        private void ReconcileRuntimeOwnerFromRegistryReplacement(object previousService, object currentService)
        {
            if (currentService is SargassumCutManager currentRuntime)
            {
                s_activeRuntimeInstance = currentRuntime;
                bool ownsRuntime = ReferenceEquals(currentRuntime, this);
                _serviceRegistered = ownsRuntime;
                if (ownsRuntime)
                {
                    if (_runtimeRoutesRetiredAfterOwnershipLoss)
                        RestoreRuntimeRoutesAfterOwnershipGain();
                    return;
                }

                if (ReferenceEquals(previousService, this))
                    RetireRuntimeRoutesAfterOwnershipLoss();
                return;
            }

            if (ReferenceEquals(previousService, this))
            {
                _serviceRegistered = false;
                if (ReferenceEquals(s_activeRuntimeInstance, this))
                    s_activeRuntimeInstance = null;
                RetireRuntimeRoutesAfterOwnershipLoss();
            }
        }

        private void RetireRuntimeRoutesAfterOwnershipLoss()
        {
            if (_runtimeRoutesRetiredAfterOwnershipLoss)
                return;

            ResetTransientRuntimeQueues();
            TryUnregister();
            _runtimeRoutesRetiredAfterOwnershipLoss = true;
        }

        private void RestoreRuntimeRoutesAfterOwnershipGain()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            CacheRegistryServicesCold();
            TryRegister();
            _runtimeRoutesRetiredAfterOwnershipLoss = false;
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
            RenderTextureFormat format = _supportsR8RandomWriteCutMaskCold
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

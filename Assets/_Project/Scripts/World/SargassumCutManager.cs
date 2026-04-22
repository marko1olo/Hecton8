using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Input;
using Hecton8.Bootstrap;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Owns the global sargassum cut mask render texture and stamps world-space cuts from active player tools.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-103)]
    public sealed class SargassumCutManager : MonoBehaviour, ITickable, ISlowTickable
    {
        private const string StampShaderName = "Hidden/Hecton8/SargassumCutMaskStamp";
        private const float DefaultWaterLevel = 4900f;
        private const int RecentStampCapacity = 16;

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

        private static readonly int _CutMaskTextureId = Shader.PropertyToID("_SargassumCutMaskRT");
        private static readonly int _CutMaskWorldRectId = Shader.PropertyToID("_SargassumCutMaskWorldRect");
        private static readonly int _CutMaskActiveId = Shader.PropertyToID("_SargassumCutMaskActive");
        private static readonly int _StampUvRadiusStrengthId = Shader.PropertyToID("_StampUvRadiusStrength");
        private static readonly int _ScrollUvOffsetId = Shader.PropertyToID("_ScrollUvOffset");
        private static readonly int _RecoveryId = Shader.PropertyToID("_Recovery");
        private static readonly int _MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int _RecentCutHeatCountId = Shader.PropertyToID("_HectonRecentCutHeatCount");
        private static readonly int _RecentCutHeatPositionRadiusId = Shader.PropertyToID("_HectonRecentCutHeatPositionRadius");
        private static readonly int _RecentCutHeatStrengthTimeId = Shader.PropertyToID("_HectonRecentCutHeatStrengthTime");

        private static SargassumCutManager _instance;

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
        private PlayerToolManager _playerToolManager;
        private RenderTexture _maskRead;
        private RenderTexture _maskWrite;
        private Material _stampMaterial;
        private bool _registeredTick;
        private bool _registeredSlowTick;
        private float _maskEnergy;
        private float _knifeStampCooldownRemaining;
        private Vector4 _maskWorldRect;
        private float _maskWorldSize;
        private Vector2 _maskCenterXZ;
        private int _maskRuntimeResolution;
        private int _maskQualityLevel = -1;
        // COLD ALLOC: RecentCutStamp[16] - CPU mirror of the newest cut stamps for zero-readback gameplay queries - owner: SargassumCutManager
        private readonly RecentCutStamp[] _recentCutStamps = new RecentCutStamp[RecentStampCapacity];
        // COLD ALLOC: RecentCutHeatStamp[16] - timestamped cut heat stamps for voxel rock thermal scarring - owner: SargassumCutManager
        private readonly RecentCutHeatStamp[] _recentCutHeatStamps = new RecentCutHeatStamp[RecentStampCapacity];
        // COLD ALLOC: Vector4[16] - packed recent-cut thermal positions/radii published to shaders - owner: SargassumCutManager
        private readonly Vector4[] _recentCutHeatPositionRadius = new Vector4[RecentStampCapacity];
        // COLD ALLOC: Vector4[16] - packed recent-cut thermal strength/start/lifetime payload published to shaders - owner: SargassumCutManager
        private readonly Vector4[] _recentCutHeatStrengthTime = new Vector4[RecentStampCapacity];
        private int _recentCutHeatCount;
        private bool _recentCutHeatDirty;

        /// <summary>
        /// Active singleton instance.
        /// </summary>
        public static SargassumCutManager Instance => _instance;

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

                float distance = Mathf.Sqrt(distanceSq);
                float radialFalloff = 1f - distance / Mathf.Max(combinedRadius, 0.001f);
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

                float distance = Mathf.Sqrt(distanceSq);
                float radialFalloff = 1f - distance / Mathf.Max(combinedRadius, 0.001f);
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
            if (_maskRead != null && _maskWrite != null && _stampMaterial != null && IsInsideMaskWorldRect(positionWS))
            {
                ExecuteStampPass(positionWS, clampedRadius, clampedStrength, 0f);
                _maskEnergy = Mathf.Max(_maskEnergy, clampedStrength);
                PublishGlobals();
                wroteMask = true;
            }

            RegisterRecentCutStamp(positionWS, clampedRadius, clampedStrength);
            RegisterRecentCutHeatStamp(positionWS, clampedRadius, clampedStrength);

            Vector3 burstDirection = directionWS.sqrMagnitude > 0.0001f ? directionWS.normalized : Vector3.up;
            EmitDebrisBurst(positionWS, burstDirection, clampedStrength, bubbleWeight);
            _debugLastStampPosition = positionWS;
            _debugLastStampCount++;
            return wroteMask;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError("[SargassumCutManager] Duplicate instance detected. Destroying the newer component.", this);
                Destroy(this);
                return;
            }

            _instance = this;
            maskResolution = Mathf.Clamp(maskResolution, 512, 2048);
            centerSnapPixelStride = Mathf.Max(0.1f, centerSnapPixelStride);
            _maskQualityLevel = QualitySettings.GetQualityLevel();
            _maskRuntimeResolution = ResolveMaskResolutionForQuality(_maskQualityLevel);
            ResolveDependencies();
            CreateResources();
            PublishGlobals();
        }

        private void OnEnable()
        {
            CreateResources();
            PublishGlobals();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            Shader.SetGlobalFloat(_CutMaskActiveId, 0f);
            Shader.SetGlobalInt(_RecentCutHeatCountId, 0);
        }

        private void OnDestroy()
        {
            TryUnregister();
            ReleaseResources();

            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Updates the cut mask by decaying existing values and stamping the currently active player tools.
        /// </summary>
        /// <param name="deltaTime">Gameplay frame delta time.</param>
        public void Tick(float deltaTime)
        {
            ResolveDependencies();
            if (_maskRead == null || _maskWrite == null || _stampMaterial == null)
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
                EmitDebrisBurst(scooterStampPosition, scooterStampDirection, scooterCutStrength, 1f);
                wrotePass = true;
                _debugLastStampCount++;
                strongestStampThisFrame = Mathf.Max(strongestStampThisFrame, scooterCutStrength);
            }

            if (TryResolveKnifeStamp(out Vector3 knifeStampPosition, out Vector3 knifeStampDirection))
            {
                ExecuteStampPass(knifeStampPosition, knifeCutRadius, knifeCutStrength, !wrotePass ? deltaTime : 0f);
                RegisterRecentCutStamp(knifeStampPosition, knifeCutRadius, knifeCutStrength);
                RegisterRecentCutHeatStamp(knifeStampPosition, knifeCutRadius, knifeCutStrength);
                EmitDebrisBurst(knifeStampPosition, knifeStampDirection, knifeCutStrength, 0.45f);
                wrotePass = true;
                _debugLastStampCount++;
                _knifeStampCooldownRemaining = knifeStampCooldown;
                strongestStampThisFrame = Mathf.Max(strongestStampThisFrame, knifeCutStrength);
            }

            if (!wrotePass && needsRecoveryPass)
                ExecuteStampPass(Vector3.zero, 0f, 0f, deltaTime);

            if (!wrotePass && !needsRecoveryPass)
                return;

            float recoveredEnergy = Mathf.Max(0f, _maskEnergy - recoveryPerSecond * deltaTime);
            _maskEnergy = wrotePass ? Mathf.Max(recoveredEnergy, strongestStampThisFrame) : recoveredEnergy;
            _debugMaskEnergy = _maskEnergy;
            PublishGlobals();
        }

        /// <summary>
        /// Re-evaluates residency bounds and recenters the cut mask if the streamed sargassum field moved significantly.
        /// </summary>
        public void SlowTick()
        {
            ResolveDependencies();
            RefreshQualityDependentResourcesIfNeeded();
            RefreshMaskWorldRect();
            PublishGlobals(forceHeatRefresh: true);
        }

        private void ResolveDependencies()
        {
            if (mapMagicVegetationBridge == null)
                mapMagicVegetationBridge = FindAnyObjectByType<HectonMapMagicVegetationBridge>(FindObjectsInactive.Exclude);

            Transform runtimePlayerTransform = BootstrapState.CurrentPlayerTransform;
            _playerTransform = runtimePlayerTransform != null ? runtimePlayerTransform : playerTransformOverride;
            if (_playerToolManager == null)
            {
                _playerToolManager = playerToolManagerOverride;
                if (_playerToolManager == null && _playerTransform != null && !_playerTransform.TryGetComponent(out _playerToolManager))
                    _playerToolManager = _playerTransform.GetComponentInChildren<PlayerToolManager>(true);
            }

            if (debrisParticleSystem == null)
                debrisParticleSystem = GetComponentInChildren<SargassumDebrisParticleSystem>(true);
        }

        private void CreateResources()
        {
            if (_maskRead == null)
                _maskRead = CreateMaskTexture("__SargassumCutMask_A");

            if (_maskWrite == null)
                _maskWrite = CreateMaskTexture("__SargassumCutMask_B");

            if (_stampMaterial == null)
            {
                Shader stampShader = stampShaderOverride != null ? stampShaderOverride : Shader.Find(StampShaderName);
                if (stampShader == null)
                {
                    Debug.LogError("[SargassumCutManager] Missing stamp shader. Expected Hidden/Hecton8/SargassumCutMaskStamp.", this);
                    enabled = false;
                    return;
                }

                _stampMaterial = new Material(stampShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                }; // COLD ALLOC: Material[1] - fullscreen blit material for global sargassum cut mask stamping - owner: SargassumCutManager
            }

            RefreshMaskWorldRect(forceClear: true);
        }

        private void RefreshQualityDependentResourcesIfNeeded()
        {
            int qualityLevel = QualitySettings.GetQualityLevel();
            if (_maskQualityLevel == qualityLevel)
                return;

            _maskQualityLevel = qualityLevel;
            int desiredResolution = ResolveMaskResolutionForQuality(qualityLevel);
            if (_maskRuntimeResolution == desiredResolution)
                return;

            _maskRuntimeResolution = desiredResolution;
            ReleaseMaskTexture(ref _maskRead);
            ReleaseMaskTexture(ref _maskWrite);
            CreateResources();
            PublishGlobals();
        }

        private int ResolveMaskResolutionForQuality(int qualityLevel)
        {
            string[] qualityNames = QualitySettings.names;
            string qualityName = qualityLevel >= 0 && qualityLevel < qualityNames.Length ? qualityNames[qualityLevel] : string.Empty;
            if (qualityName.IndexOf("Low", System.StringComparison.OrdinalIgnoreCase) >= 0 || qualityLevel <= 0)
                return Mathf.Min(maskResolution, 512);

            if (qualityName.IndexOf("Medium", System.StringComparison.OrdinalIgnoreCase) >= 0 || qualityLevel == 1)
                return Mathf.Min(maskResolution, 1024);

            return Mathf.Min(maskResolution, 2048);
        }

        private void ReleaseResources()
        {
            ReleaseMaskTexture(ref _maskRead);
            ReleaseMaskTexture(ref _maskWrite);

            if (_stampMaterial != null)
            {
                Destroy(_stampMaterial);
                _stampMaterial = null;
            }

            Shader.SetGlobalInt(_RecentCutHeatCountId, 0);
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

            if (mustClear)
            {
                ClearMaskTextures();
                _maskEnergy = 0f;
                _debugMaskEnergy = 0f;
                PublishGlobals();
                return;
            }

            ScrollMaskTextures(centerDelta);
            PublishGlobals();
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

            InputManager inputManager = InputManager.Instance;
            if (inputManager == null || !inputManager.IsPrimaryActionHeld)
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
            Vector2 uvCenter = new Vector2(
                (positionWS.x - _maskWorldRect.x) * _maskWorldRect.z,
                (positionWS.z - _maskWorldRect.y) * _maskWorldRect.w);
            float uvRadius = radiusWS * _maskWorldRect.z;

            _stampMaterial.SetTexture(_MainTexId, _maskRead);
            _stampMaterial.SetVector(_StampUvRadiusStrengthId, new Vector4(uvCenter.x, uvCenter.y, uvRadius, Mathf.Clamp01(strength)));
            _stampMaterial.SetFloat(_RecoveryId, recovery);
            Graphics.Blit(_maskRead, _maskWrite, _stampMaterial, 0);

            RenderTexture temp = _maskRead;
            _maskRead = _maskWrite;
            _maskWrite = temp;
            _debugLastStampPosition = positionWS;
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
            float currentTime = Time.time;
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
            _recentCutHeatDirty = true;
        }

        private void EmitDebrisBurst(Vector3 positionWS, Vector3 directionWS, float cutStrength, float bubbleWeight)
        {
            if (debrisParticleSystem == null)
                return;

            debrisParticleSystem.EmitBurst(positionWS, directionWS, cutStrength, bubbleWeight);
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
                ClearMaskTextures();
                _maskEnergy = 0f;
                _debugMaskEnergy = 0f;
                return;
            }

            _stampMaterial.SetTexture(_MainTexId, _maskRead);
            _stampMaterial.SetVector(_ScrollUvOffsetId, new Vector4(uvOffsetX, uvOffsetY, 0f, 0f));
            Graphics.Blit(_maskRead, _maskWrite, _stampMaterial, 1);

            RenderTexture temp = _maskRead;
            _maskRead = _maskWrite;
            _maskWrite = temp;
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

        private void ClearMaskTextures()
        {
            if (_maskRead == null || _maskWrite == null)
                return;

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = _maskRead;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = _maskWrite;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = active;
        }

        private void PublishGlobals(bool forceHeatRefresh = false)
        {
            if (_maskRead == null)
            {
                Shader.SetGlobalFloat(_CutMaskActiveId, 0f);
                Shader.SetGlobalInt(_RecentCutHeatCountId, 0);
                return;
            }

            Shader.SetGlobalTexture(_CutMaskTextureId, _maskRead);
            Shader.SetGlobalVector(_CutMaskWorldRectId, _maskWorldRect);
            Shader.SetGlobalFloat(_CutMaskActiveId, _maskWorldSize > 0f ? 1f : 0f);

            if (!forceHeatRefresh && !_recentCutHeatDirty)
                return;

            _recentCutHeatCount = 0;
            float currentTime = Time.time;
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

            Shader.SetGlobalInt(_RecentCutHeatCountId, _recentCutHeatCount);
            Shader.SetGlobalVectorArray(_RecentCutHeatPositionRadiusId, _recentCutHeatPositionRadius);
            Shader.SetGlobalVectorArray(_RecentCutHeatStrengthTimeId, _recentCutHeatStrengthTime);
            _recentCutHeatDirty = false;
        }

        private void TryRegister()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (!_registeredTick)
            {
                tickManager.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                tickManager.Register((ISlowTickable)this);
                _registeredSlowTick = true;
            }
        }

        private void TryUnregister()
        {
            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            if (_registeredTick)
            {
                tickManager.Unregister((ITickable)this);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                tickManager.Unregister((ISlowTickable)this);
                _registeredSlowTick = false;
            }
        }

        private RenderTexture CreateMaskTexture(string textureName)
        {
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8
                : RenderTextureFormat.ARGB32;
            RenderTexture texture = new RenderTexture(_maskRuntimeResolution, _maskRuntimeResolution, 0, format, RenderTextureReadWrite.Linear)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = false,
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
    }
}

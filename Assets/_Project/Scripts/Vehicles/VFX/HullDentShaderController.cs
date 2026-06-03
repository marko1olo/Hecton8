using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Physics.Vehicles;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Vehicles.VFX
{
    /// <summary>
    /// Publishes immutable baked hull deformation assets to shaders.
    /// Runtime CPU mesh dents were removed for Agent 1722; damage shape now comes from offline displacement maps.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Vehicles/Hull Dent Shader Controller")]
    public sealed class HullDentShaderController : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static readonly int H8HullAlbedoMapId = Shader.PropertyToID("_H8HullAlbedoMap");
        private static readonly int H8HullMraoMapId = Shader.PropertyToID("_H8HullMraoMap");
        private static readonly int H8HullDisplacementMapId = Shader.PropertyToID("_H8HullDisplacementMap");
        private static readonly int H8HullCavitationFlipbookId = Shader.PropertyToID("_H8HullCavitationFlipbook");
        private static readonly int H8HullBakeParamsId = Shader.PropertyToID("_H8HullBakeParams");
        private static readonly int H8HullBakeUvParamsId = Shader.PropertyToID("_H8HullBakeUvParams");
        private static readonly int H8HullCavitationParamsId = Shader.PropertyToID("_H8HullCavitationParams");
        private static readonly int H8HullCavitationUvParamsId = Shader.PropertyToID("_H8HullCavitationUvParams");
        private static readonly int HullDentParamsId = Shader.PropertyToID("_HectonHullDentParams");
        private static readonly int VesselCareParamsId = Shader.PropertyToID("_HectonVesselCareParams");
        private static readonly int VesselCareMaskId = Shader.PropertyToID("_HectonVesselCareMask");

        private static readonly ProfilerMarker LateFrameMarker = new ProfilerMarker("HullDentShaderController1722.LateFrame");
        private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("HullDentShaderController1722.UploadShaderGlobals");
        private static readonly ProfilerMarker TelemetryMarker = new ProfilerMarker("HullDentShaderController1722.TelemetryRead");

        private const byte MinQualityByte = 1;
        private const float DefaultDisplacementMeters = 0.18f;
        private const float DefaultCavitationRateHz = 18f;
        private const int DefaultFlipbookFrames = 64;
        private const int DefaultFlipbookTiles = 8;
        private const int TelemetrySampleMask = 15;
        private const int TelemetryRebindMask = 63;
        private const int BlackBoxCapacity = 300;
        private const float InverseHullMaintenancePanels = 1f / 64f;

        [StructLayout(LayoutKind.Sequential)]
        private struct HullDentBlackBoxEntry
        {
            public uint Frame;
            public uint Flags;
            public float Cleanliness;
            public float CareTone;
            public float BallastHealth;
            public float QualityWeight;
            public float CavitationPhase;
            public float ScarScalar;
            public float DisplacementMeters;
            private uint _pad0;
        }

        [Header("Baked Hull Maps")]
        [SerializeField] private Texture2D hullAlbedoMap;
        [SerializeField] private Texture2D hullMraoMask;
        [SerializeField] private Texture2D hullDisplacementMap;
        [SerializeField] private Texture2D cavitationFlipbook;

        [Header("Baked Displacement")]
        [SerializeField, Min(0f)] private float displacementStrengthMeters = DefaultDisplacementMeters;
        [SerializeField, Range(0f, 1f)] private float bakedScarBlend = 1f;
        [SerializeField] private Vector2 bakedUvScale = Vector2.one;
        [SerializeField] private Vector2 bakedUvOffset;

        [Header("Cavitation")]
        [SerializeField, Range(0f, 1f)] private float cavitationIntensity = 1f;
        [SerializeField, Min(0f)] private float cavitationPhaseRateHz = DefaultCavitationRateHz;
        [SerializeField, Min(1)] private int cavitationFlipbookFrames = DefaultFlipbookFrames;
        [SerializeField, Min(1)] private int cavitationFlipbookTiles = DefaultFlipbookTiles;
        [SerializeField] private Vector2 cavitationUvScale = new Vector2(4f, 2f);
        [SerializeField] private Vector2 cavitationUvOffset = new Vector2(-3f, -0.5f);

        [Header("Registry")]
        [SerializeField] private bool bindTexturesOnEnable = true;

        private IDataVault _dataVault;
        private VaultGenerationHandle<VesselTelemetryEntry> _vesselTelemetryHandle;
        private bool _hasTelemetryHandle;
        private bool _isRegistered;
        private bool _registryListenerRegistered;
        private bool _warnedMissingTextures;
        private float _cleanliness = 1f;
        private float _careTone;
        private float _ballastHealth = 1f;
        private float _qualityWeight = 1f;
        private float _cavitationPhase01;
        private int _frameCounter;
        private int _blackBoxCursor;
        private HullDentBlackBoxEntry[] _blackBox;
        private bool _blackBoxFaulted;
        private bool _shaderGlobalsDirty = true;

        public bool IsTickEnabled => isActiveAndEnabled;

        private bool HasBakedHullTextures => hullAlbedoMap != null && hullMraoMask != null && hullDisplacementMap != null;
        private bool HasCavitationFlipbook => cavitationFlipbook != null;

        private void OnEnable()
        {
            SanitizeSerializedFields();
            if (!IsBlackBoxEntryLayoutAligned())
            {
                enabled = false;
                return;
            }

            CacheRegistryServicesCold();
            EnsureBlackBoxAllocated();
            RegisterRegistryListenerCold();
            TryRegisterTick();

            if (bindTexturesOnEnable)
            {
                UploadBakedTextureGlobalsCold();
            }

            UploadStaticShaderGlobalsFromLocal();
            UploadCavitationShaderGlobalsFromLocal();
            _shaderGlobalsDirty = false;
            ReportBlackBoxState();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            UnregisterRegistryListenerCold();
            _blackBoxCursor = 0;

            Shader.SetGlobalVector(H8HullBakeParamsId, Vector4.zero);
            Shader.SetGlobalVector(H8HullCavitationParamsId, Vector4.zero);
            Shader.SetGlobalVector(H8HullCavitationUvParamsId, Vector4.zero);
            Shader.SetGlobalVector(HullDentParamsId, Vector4.zero);
        }

        private void OnValidate()
        {
            SanitizeSerializedFields();
            _shaderGlobalsDirty = true;

            if (isActiveAndEnabled)
            {
                UploadBakedTextureGlobalsCold();
                UploadStaticShaderGlobalsFromLocal();
                UploadCavitationShaderGlobalsFromLocal();
                _shaderGlobalsDirty = false;
            }
        }

        public void LateFrameTick()
        {
            using (LateFrameMarker.Auto())
            {
                ++_frameCounter;

                bool telemetryChanged = false;
                if (!_hasTelemetryHandle || ((_frameCounter & TelemetrySampleMask) == 0))
                {
                    telemetryChanged = TryRefreshVesselTelemetry();
                }

                bool qualityChanged = RefreshQualityWeight();

                bool cavitationChanged = RefreshCavitationPhase(SystemDispatcher.CurrentFrameDeltaTime);
                if (telemetryChanged || qualityChanged)
                {
                    _shaderGlobalsDirty = true;
                }

                if (_shaderGlobalsDirty)
                {
                    UploadStaticShaderGlobalsFromLocal();
                    _shaderGlobalsDirty = false;
                }

                if (cavitationChanged)
                {
                    UploadCavitationShaderGlobalsFromLocal();
                }

                ReportBlackBoxState();
            }
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot slot, object previous, object current)
        {
            if (slot == GlobalRegistryServiceSlot.DataVault)
            {
                _dataVault = current as IDataVault;
                _vesselTelemetryHandle = default;
                _hasTelemetryHandle = false;
                TryBindVesselTelemetryHandle();
                if (TryRefreshVesselTelemetry())
                {
                    _shaderGlobalsDirty = true;
                }

                UploadStaticShaderGlobalsFromLocal();
                _shaderGlobalsDirty = false;
                return;
            }

            if (slot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterTick();
                TryRegisterTick();
            }
        }

        private void CacheRegistryServicesCold()
        {
            if (_dataVault == null)
            {
                _dataVault = GlobalRegistry.DataVault;
            }

            TryBindVesselTelemetryHandle();
        }

        private void RegisterRegistryListenerCold()
        {
            if (_registryListenerRegistered || !Application.isPlaying)
            {
                return;
            }

            if (GlobalRegistry.TryRegisterHotSwapListener(this))
            {
                _registryListenerRegistered = true;
            }
        }

        private void UnregisterRegistryListenerCold()
        {
            if (!_registryListenerRegistered)
            {
                return;
            }

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registryListenerRegistered = false;
        }

        private void TryRegisterTick()
        {
            if (_isRegistered || !Application.isPlaying)
            {
                return;
            }

            _isRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterTick()
        {
            if (!_isRegistered)
            {
                _isRegistered = false;
                return;
            }

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _isRegistered = false;
        }

        private void TryBindVesselTelemetryHandle()
        {
            if (_hasTelemetryHandle || _dataVault == null)
            {
                return;
            }

            _hasTelemetryHandle = _dataVault.TryGetGenerationHandle<VesselTelemetryEntry>(
                SubmarineBallastBufferIds.VesselTelemetry,
                out _vesselTelemetryHandle) &&
                IsVesselTelemetryHandle(in _vesselTelemetryHandle);
        }

        private bool TryRefreshVesselTelemetry()
        {
            using (TelemetryMarker.Auto())
            {
                if (_dataVault == null)
                {
                    return false;
                }

                if (!_hasTelemetryHandle || ((_frameCounter & TelemetryRebindMask) == 0))
                {
                    _hasTelemetryHandle = false;
                    TryBindVesselTelemetryHandle();
                }

                if (!_hasTelemetryHandle)
                {
                    return false;
                }

                if (_dataVault.IsCompactionFenceActive)
                {
                    return false;
                }

                if (!_dataVault.TryReadOnlyHandle(in _vesselTelemetryHandle, out NativeArray<VesselTelemetryEntry>.ReadOnly telemetry) || !telemetry.IsCreated || telemetry.Length == 0)
                {
                    return false;
                }

                VesselTelemetryEntry entry = telemetry[0];
                float nextCleanliness = ResolveCleanliness01(entry.HullCleanlinessMask);
                float nextCareTone = math.saturate(VesselTelemetryEntry.ResolveToneWeight01(entry.TotalCareActionsCount));
                float nextBallastHealth = ResolveBallastHealth01(entry.CurrentBallastRatio);

                bool changed =
                    math.abs(nextCleanliness - _cleanliness) > 0.002f ||
                    math.abs(nextCareTone - _careTone) > 0.002f ||
                    math.abs(nextBallastHealth - _ballastHealth) > 0.002f;

                _cleanliness = nextCleanliness;
                _careTone = nextCareTone;
                _ballastHealth = nextBallastHealth;
                return changed;
            }
        }

        private bool RefreshQualityWeight()
        {
            float nextQuality = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(nextQuality))
            {
                nextQuality = 1f;
            }

            nextQuality = math.saturate(nextQuality);
            if (math.abs(nextQuality - _qualityWeight) <= 0.002f)
            {
                return false;
            }

            _qualityWeight = nextQuality;
            return true;
        }

        private bool RefreshCavitationPhase(float deltaTime)
        {
            if (!HasCavitationFlipbook || cavitationIntensity <= 0f || cavitationPhaseRateHz <= 0f)
            {
                return false;
            }

            float dt = math.max(0f, deltaTime);
            if (dt <= 0f)
            {
                return false;
            }

            float previous = _cavitationPhase01;
            float visualRate = cavitationPhaseRateHz * math.lerp(0.35f, 1.35f, _qualityWeight);
            _cavitationPhase01 = math.frac(_cavitationPhase01 + dt * visualRate);
            return math.abs(_cavitationPhase01 - previous) > 0.000001f;
        }

        private void UploadBakedTextureGlobalsCold()
        {
            if (!HasBakedHullTextures && !_warnedMissingTextures)
            {
                Debug.LogWarning("HullDentShaderController1722: no baked displacement texture assigned. Runtime CPU dents remain disabled.", this);
                _warnedMissingTextures = true;
            }

            if (hullAlbedoMap != null)
            {
                Shader.SetGlobalTexture(H8HullAlbedoMapId, hullAlbedoMap);
            }

            if (hullMraoMask != null)
            {
                Shader.SetGlobalTexture(H8HullMraoMapId, hullMraoMask);
            }

            if (hullDisplacementMap != null)
            {
                Shader.SetGlobalTexture(H8HullDisplacementMapId, hullDisplacementMap);
            }

            if (cavitationFlipbook != null)
            {
                Shader.SetGlobalTexture(H8HullCavitationFlipbookId, cavitationFlipbook);
            }
        }

        private void UploadStaticShaderGlobalsFromLocal()
        {
            using (UploadMarker.Auto())
            {
                float bakedEnabled = HasBakedHullTextures ? 1f : 0f;
                float scarScalar = ResolveHullScarScalar();

                Shader.SetGlobalVector(
                    H8HullBakeParamsId,
                    new Vector4(bakedEnabled, displacementStrengthMeters, scarScalar, _qualityWeight));

                Shader.SetGlobalVector(
                    H8HullBakeUvParamsId,
                    new Vector4(bakedUvScale.x, bakedUvScale.y, bakedUvOffset.x, bakedUvOffset.y));

                Shader.SetGlobalVector(
                    HullDentParamsId,
                    new Vector4(0f, 1f, scarScalar, ResolveQualityWeightByte()));

                Shader.SetGlobalVector(
                    VesselCareParamsId,
                    new Vector4(_cleanliness, _careTone, _ballastHealth, 0f));

                Shader.SetGlobalVector(
                    VesselCareMaskId,
                    new Vector4(_cleanliness, _careTone, _ballastHealth, 1f));
            }
        }

        private void UploadCavitationShaderGlobalsFromLocal()
        {
            float cavitationEnabled = HasCavitationFlipbook ? cavitationIntensity : 0f;
            Shader.SetGlobalVector(
                H8HullCavitationParamsId,
                new Vector4(cavitationEnabled, _cavitationPhase01, cavitationFlipbookFrames, cavitationFlipbookTiles));
            Shader.SetGlobalVector(
                H8HullCavitationUvParamsId,
                new Vector4(cavitationUvScale.x, cavitationUvScale.y, cavitationUvOffset.x, cavitationUvOffset.y));
        }

        private float ResolveHullScarScalar()
        {
            float grimeScar = (1f - _cleanliness) * 0.35f;
            float careScar = _careTone * 0.12f;
            float ballastScar = (1f - _ballastHealth) * 0.18f;
            return math.saturate(bakedScarBlend * math.lerp(0.35f, 1f, _qualityWeight) + grimeScar + careScar + ballastScar);
        }

        private float ResolveQualityWeightByte()
        {
            int byteValue = Mathf.Clamp(Mathf.RoundToInt(_qualityWeight * 255f), MinQualityByte, 255);
            return byteValue;
        }

        private void ReportBlackBoxState()
        {
            if (_blackBox == null || _blackBox.Length == 0)
            {
                return;
            }

            uint flags = BuildTelemetryFlags();
            uint currentFrame = SystemDispatcher.CurrentFrameId;
            HullDentBlackBoxEntry entry = new HullDentBlackBoxEntry
            {
                Frame = currentFrame != 0u ? currentFrame : (uint)Mathf.Max(_frameCounter, 0),
                Flags = flags,
                Cleanliness = _cleanliness,
                CareTone = _careTone,
                BallastHealth = _ballastHealth,
                QualityWeight = _qualityWeight,
                CavitationPhase = _cavitationPhase01,
                ScarScalar = ResolveHullScarScalar(),
                DisplacementMeters = displacementStrengthMeters
            };

            _blackBox[_blackBoxCursor] = entry;
            _blackBoxCursor++;
            if (_blackBoxCursor >= _blackBox.Length)
            {
                _blackBoxCursor = 0;
            }

            if (!_blackBoxFaulted &&
                !IsFinite(
                    entry.Cleanliness,
                    entry.CareTone,
                    entry.BallastHealth,
                    entry.QualityWeight,
                    entry.CavitationPhase,
                    entry.ScarScalar,
                    entry.DisplacementMeters))
            {
                _blackBoxFaulted = true;
                ResetPresentationStateAfterNonFinite();
            }
        }

        private uint BuildTelemetryFlags()
        {
            uint qualityByte = (uint)ResolveQualityWeightByte();
            uint cleanlinessByte = (uint)Mathf.Clamp(Mathf.RoundToInt(_cleanliness * 255f), 0, 255);
            uint scarByte = (uint)Mathf.Clamp(Mathf.RoundToInt(ResolveHullScarScalar() * 255f), 0, 255);
            uint state = qualityByte | (cleanlinessByte << 8) | (scarByte << 16);

            if (HasBakedHullTextures)
            {
                state |= 1u << 24;
            }

            if (HasCavitationFlipbook)
            {
                state |= 1u << 25;
            }

            return state;
        }

        private static float FiniteOrDefault(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float FiniteNonNegativeOrZero(float value, float fallback)
        {
            return math.isfinite(value) && value >= 0f ? value : fallback;
        }

        private static float FiniteAtLeast(float value, float minimum, float fallback)
        {
            return math.isfinite(value) && value >= minimum ? value : fallback;
        }

        private static float ResolveCleanliness01(ulong hullCleanlinessMask)
        {
            return math.saturate(CountSetBits64(hullCleanlinessMask) * InverseHullMaintenancePanels);
        }

        private static float ResolveBallastHealth01(float currentBallastRatio)
        {
            float safeRatio = math.saturate(math.select(0.5f, currentBallastRatio, math.isfinite(currentBallastRatio)));
            return math.saturate(1f - math.abs(safeRatio - 0.5f) * 0.65f);
        }

        private static bool IsVesselTelemetryHandle(in VaultGenerationHandle<VesselTelemetryEntry> handle)
        {
            return handle.BufferID == unchecked((uint)(int)SubmarineBallastBufferIds.VesselTelemetry) &&
                   handle.Generation != 0u;
        }

        private static int CountSetBits64(ulong value)
        {
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            return (int)((((value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL) * 0x0101010101010101UL) >> 56);
        }

        private void SanitizeSerializedFields()
        {
            displacementStrengthMeters = FiniteNonNegativeOrZero(displacementStrengthMeters, DefaultDisplacementMeters);
            bakedScarBlend = Mathf.Clamp01(FiniteNonNegativeOrZero(bakedScarBlend, 1f));
            cavitationIntensity = Mathf.Clamp01(FiniteNonNegativeOrZero(cavitationIntensity, 1f));
            cavitationPhaseRateHz = FiniteNonNegativeOrZero(cavitationPhaseRateHz, DefaultCavitationRateHz);
            cavitationFlipbookFrames = Mathf.Max(1, cavitationFlipbookFrames);
            cavitationFlipbookTiles = Mathf.Max(1, cavitationFlipbookTiles);
            cavitationUvScale.x = FiniteAtLeast(cavitationUvScale.x, 0.0001f, 4f);
            cavitationUvScale.y = FiniteAtLeast(cavitationUvScale.y, 0.0001f, 2f);
            cavitationUvOffset.x = FiniteOrDefault(cavitationUvOffset.x, -3f);
            cavitationUvOffset.y = FiniteOrDefault(cavitationUvOffset.y, -0.5f);
            bakedUvScale.x = FiniteAtLeast(bakedUvScale.x, 0.0001f, 1f);
            bakedUvScale.y = FiniteAtLeast(bakedUvScale.y, 0.0001f, 1f);
            bakedUvOffset.x = FiniteOrDefault(bakedUvOffset.x, 0f);
            bakedUvOffset.y = FiniteOrDefault(bakedUvOffset.y, 0f);
        }

        private void EnsureBlackBoxAllocated()
        {
            if (_blackBox != null && _blackBox.Length == BlackBoxCapacity)
            {
                return;
            }

            // COLD ALLOC: HullDentBlackBoxEntry[300] - presentation state ring for finite-state diagnosis - owner: HullDentShaderController
            _blackBox = new HullDentBlackBoxEntry[BlackBoxCapacity];
            _blackBoxCursor = 0;
        }

        private void ResetPresentationStateAfterNonFinite()
        {
            _cleanliness = 1f;
            _careTone = 0f;
            _ballastHealth = 1f;
            _qualityWeight = 1f;
            _cavitationPhase01 = 0f;
            _shaderGlobalsDirty = true;
            UploadStaticShaderGlobalsFromLocal();
            UploadCavitationShaderGlobalsFromLocal();
            _shaderGlobalsDirty = false;
        }

        private static bool IsBlackBoxEntryLayoutAligned()
        {
            return (UnsafeUtility.SizeOf<HullDentBlackBoxEntry>() & 7) == 0;
        }

        private static bool IsFinite(
            float a,
            float b,
            float c,
            float d,
            float e,
            float f,
            float g)
        {
            return
                math.isfinite(a) &&
                math.isfinite(b) &&
                math.isfinite(c) &&
                math.isfinite(d) &&
                math.isfinite(e) &&
                math.isfinite(f) &&
                math.isfinite(g);
        }
    }
}

using Hecton8.Core;
using Hecton8.UI;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Input
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30988)]
    public sealed class AccessibilitySettings : MonoBehaviour, IDispatcherSystem, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        public const uint SystemHash = 0x41313332u;
        public const float DefaultTextScale = 1f;
        public const float MinimumTextScale = 0.78f;
        public const float MaximumTextScale = 1.35f;
        public const float DefaultUiMotionScale = 1f;
        public const float MinimumUiMotionScale = 0f;
        public const float MaximumUiMotionScale = 1f;

        [Header("Color Filter")]
        [SerializeField] private AccessibilityColorFilterMode colorFilterMode = AccessibilityColorFilterMode.Off;
        [SerializeField, Range(0f, 1f)] private float filterStrength01 = 1f;
        [SerializeField, Range(0f, 1f)] private float globalQualityWeight = 1f;

        [Header("Text Scale")]
        [SerializeField, Range(MinimumTextScale, MaximumTextScale)] private float textScale = DefaultTextScale;

        [Header("Motion Comfort")]
        [SerializeField, Range(MinimumUiMotionScale, MaximumUiMotionScale)] private float uiMotionScale = DefaultUiMotionScale;

        private static readonly int AccessibilityCBufferId = Shader.PropertyToID("HectonAccessibilityConfig");
        private static readonly int AccessibilityParamsId = Shader.PropertyToID("_HectonAccessibilityParams");

        private GraphicsBuffer _configBufferA;
        private GraphicsBuffer _configBufferB;
        private GraphicsBuffer _activeConfigBuffer;
        private AccessibilityConfigDTO _currentConfig;
        private AccessibilityConfigDTO _lastUploadedConfig;
        private int _writeBufferIndex;
        private bool _registered;
        private bool _registeredHotSwap;
        private bool _dirty = true;
        private bool _uploaded;
        private bool _duplicateInstance;
        private bool _serviceShutdownComplete;
        private bool _supportsConstantBuffers;
        private bool _textScaleDirty = true;
        private float _lastPublishedTextScale = -1f;
        private bool _uiMotionScaleDirty = true;
        private float _lastPublishedUiMotionScale = -1f;

        internal static AccessibilitySettings ActiveRuntimeInstance { get; private set; }

        internal static bool TryResolveActiveRuntime(ref AccessibilitySettings target)
        {
            AccessibilitySettings active = ActiveRuntimeInstance;
            if (!IsLiveRuntimeOwner(active))
            {
                target = null;
                return false;
            }

            if (!ReferenceEquals(target, active))
                target = active;

            return true;
        }

        public uint GetSystemIdHash() => SystemHash;
        public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.VisualSync;
        public byte GetBucketId() => 0;
        public int GetDependencyCount() => 0;
        public uint GetDependencyHash(int dependencyIndex) => 0u;

        private void Awake()
        {
            if (!TryClaimRuntimeInstance())
                return;

            CacheGraphicsCapabilitiesCold();
            TryColdBootstrapBuffers();
            RebuildConfig();
        }

        private void OnEnable()
        {
            if (!TryClaimRuntimeInstance())
                return;

            _serviceShutdownComplete = false;
            CacheGraphicsCapabilitiesCold();
            TryColdBootstrapBuffers();
            RebuildConfig();
            _textScaleDirty = true;
            _uiMotionScaleDirty = true;
            TryRegisterHotSwapListener();
            TryRegisterDispatcherSystem();
        }

        private void OnDisable()
        {
            OnServiceShutdown();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        private void OnValidate()
        {
            float safeTextScale = SanitizeTextScale(textScale);
            if (math.abs(textScale - safeTextScale) > 0.0001f)
                textScale = safeTextScale;

            float safeUiMotionScale = SanitizeUiMotionScale(uiMotionScale);
            if (math.abs(uiMotionScale - safeUiMotionScale) > 0.0001f)
                uiMotionScale = safeUiMotionScale;

            _textScaleDirty = true;
            _uiMotionScaleDirty = true;
            RebuildConfig();
        }

        public void OnServiceShutdown()
        {
            if (_duplicateInstance || _serviceShutdownComplete)
                return;

            _serviceShutdownComplete = true;
            TryUnregisterHotSwapListener();
            if (_registered)
            {
                GlobalRegistry.UnregisterDispatcherSystem(this);
                _registered = false;
            }

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            PublishDisabledConfig();
            ReleaseBuffer(ref _configBufferA);
            ReleaseBuffer(ref _configBufferB);
            _activeConfigBuffer = null;
            _uploaded = false;
            _writeBufferIndex = 0;
            _textScaleDirty = true;
            _lastPublishedTextScale = -1f;
            _uiMotionScaleDirty = true;
            _lastPublishedUiMotionScale = -1f;
            UIScreenShake.SetGlobalMotionScale(DefaultUiMotionScale);
        }

        public void SetColorFilter(AccessibilityColorFilterMode mode, float strength01, float qualityWeight01)
        {
            colorFilterMode = mode;
            filterStrength01 = Sanitize01(strength01);
            globalQualityWeight = Sanitize01(qualityWeight01);
            RebuildConfig();
        }

        /// <summary>
        /// Queues a continuous text scale update for diegetic UI and PDA presentation.
        /// </summary>
        public void SetTextScale(float scale)
        {
            float safeScale = SanitizeTextScale(scale);
            if (math.abs(textScale - safeScale) <= 0.0001f)
                return;

            textScale = safeScale;
            _textScaleDirty = true;
        }

        /// <summary>
        /// Queues a continuous motion comfort update for UI-only presentation effects.
        /// </summary>
        public void SetUiMotionScale(float scale)
        {
            float safeScale = SanitizeUiMotionScale(scale);
            if (math.abs(uiMotionScale - safeScale) <= 0.0001f)
                return;

            uiMotionScale = safeScale;
            _uiMotionScaleDirty = true;
        }

        public AccessibilityConfigDTO ReadCurrentConfig()
        {
            return _currentConfig;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            TryUnregisterDispatcherSystem();
            if (currentService != null && isActiveAndEnabled)
                TryRegisterDispatcherSystem();
        }

        public void PreSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            return dependsOn;
        }

        public void PostSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (!_dirty && _uploaded && ConfigEquals(in _currentConfig, in _lastUploadedConfig))
            {
                PublishTextScaleIfNeededVisualSync();
                PublishUiMotionScaleIfNeededVisualSync();
                return;
            }

            Vector4 fallback = new Vector4(
                _currentConfig.ColorMode,
                _currentConfig.Flags,
                _currentConfig.FilterStrength01,
                _currentConfig.GlobalQualityWeight);
            Shader.SetGlobalVector(AccessibilityParamsId, fallback);

            if (!_supportsConstantBuffers || !HasValidBuffers())
            {
                _lastUploadedConfig = _currentConfig;
                _uploaded = true;
                _dirty = false;
                PublishTextScaleIfNeededVisualSync();
                PublishUiMotionScaleIfNeededVisualSync();
                return;
            }

            GraphicsBuffer writeBuffer = ResolveNextBuffer();
            NativeArray<AccessibilityConfigDTO> mapped = writeBuffer.LockBufferForWrite<AccessibilityConfigDTO>(0, 1);
            try
            {
                mapped[0] = _currentConfig;
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<AccessibilityConfigDTO>(1);
            }

            _activeConfigBuffer = writeBuffer;
            Shader.SetGlobalConstantBuffer(
                AccessibilityCBufferId,
                _activeConfigBuffer,
                0,
                InputBindingContractLayout.AccessibilityConfigStrideBytes);
            _lastUploadedConfig = _currentConfig;
            _uploaded = true;
            _dirty = false;
            PublishTextScaleIfNeededVisualSync();
            PublishUiMotionScaleIfNeededVisualSync();
        }

        private void RebuildConfig()
        {
            float strength = Sanitize01(filterStrength01);
            float quality = Sanitize01(globalQualityWeight);
            float safeTextScale = SanitizeTextScale(textScale);
            float safeUiMotionScale = SanitizeUiMotionScale(uiMotionScale);
            if (math.abs(textScale - safeTextScale) > 0.0001f)
            {
                textScale = safeTextScale;
                _textScaleDirty = true;
            }

            if (math.abs(uiMotionScale - safeUiMotionScale) > 0.0001f)
            {
                uiMotionScale = safeUiMotionScale;
                _uiMotionScaleDirty = true;
            }

            uint mode = (uint)colorFilterMode;
            uint flags = (uint)AccessibilityConfigFlags.ContinuousQualityWeight;
            if (mode != 0u && strength > 0.0001f)
                flags |= (uint)AccessibilityConfigFlags.Enabled;

            AccessibilityConfigDTO next = default;
            next.ColorMode = math.min(mode, (uint)AccessibilityColorFilterMode.Tritanopia);
            next.Flags = flags;
            next.FilterStrength01 = strength;
            next.GlobalQualityWeight = quality;
            if (!ConfigEquals(in _currentConfig, in next))
            {
                _currentConfig = next;
                _dirty = true;
            }
        }

        private void PublishTextScaleIfNeededVisualSync()
        {
            float safeScale = SanitizeTextScale(textScale);
            if (math.abs(textScale - safeScale) > 0.0001f)
            {
                textScale = safeScale;
                _textScaleDirty = true;
            }

            if (!_textScaleDirty && math.abs(_lastPublishedTextScale - safeScale) <= 0.0001f)
                return;

            if (!FontStreamingManager.RequestAccessibilityTextScale(safeScale))
                return;

            _lastPublishedTextScale = safeScale;
            _textScaleDirty = false;
        }

        private void PublishUiMotionScaleIfNeededVisualSync()
        {
            float safeScale = SanitizeUiMotionScale(uiMotionScale);
            if (math.abs(uiMotionScale - safeScale) > 0.0001f)
            {
                uiMotionScale = safeScale;
                _uiMotionScaleDirty = true;
            }

            if (!_uiMotionScaleDirty && math.abs(_lastPublishedUiMotionScale - safeScale) <= 0.0001f)
                return;

            UIScreenShake.SetGlobalMotionScale(safeScale);
            _lastPublishedUiMotionScale = safeScale;
            _uiMotionScaleDirty = false;
        }

        private void TryColdBootstrapBuffers()
        {
            if (!_supportsConstantBuffers)
            {
                ReleaseBuffer(ref _configBufferA);
                ReleaseBuffer(ref _configBufferB);
                _activeConfigBuffer = null;
                _writeBufferIndex = 0;
                _uploaded = false;
                _dirty = true;
                return;
            }

            if (HasValidBuffers())
                return;

            ReleaseBuffer(ref _configBufferA);
            ReleaseBuffer(ref _configBufferB);
            _configBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, InputBindingContractLayout.AccessibilityConfigStrideBytes); // COLD ALLOC: GraphicsBuffer[1] - accessibility cbuffer A - owner: AccessibilitySettings
            _configBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Constant, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, InputBindingContractLayout.AccessibilityConfigStrideBytes); // COLD ALLOC: GraphicsBuffer[1] - accessibility cbuffer B - owner: AccessibilitySettings
            _activeConfigBuffer = null;
            _writeBufferIndex = 0;
            _uploaded = false;
            _dirty = true;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsConstantBuffers = SystemInfo.supportsSetConstantBuffer;
        }

        private bool TryClaimRuntimeInstance()
        {
            if (_duplicateInstance)
                return false;

            AccessibilitySettings active = ActiveRuntimeInstance;
            if (active != null && !ReferenceEquals(active, this))
            {
                if (IsLiveRuntimeOwner(active))
                {
                    _duplicateInstance = true;
                    _serviceShutdownComplete = true;
                    Destroy(gameObject);
                    return false;
                }

                ActiveRuntimeInstance = null;
            }

            ActiveRuntimeInstance = this;
            return true;
        }

        private static bool IsLiveRuntimeOwner(AccessibilitySettings settings)
        {
            return settings != null &&
                   settings.isActiveAndEnabled &&
                   !settings._duplicateInstance &&
                   !settings._serviceShutdownComplete;
        }

        private bool HasValidBuffers()
        {
            return _configBufferA != null &&
                   _configBufferA.IsValid() &&
                   _configBufferB != null &&
                   _configBufferB.IsValid();
        }

        private void TryRegisterDispatcherSystem()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterDispatcherSystem(this);
        }

        private void TryUnregisterDispatcherSystem()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterDispatcherSystem(this);
            _registered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
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

        private GraphicsBuffer ResolveNextBuffer()
        {
            _writeBufferIndex ^= 1;
            return _writeBufferIndex == 0 ? _configBufferA : _configBufferB;
        }

        private void PublishDisabledConfig()
        {
            AccessibilityConfigDTO disabled = default;
            Shader.SetGlobalVector(AccessibilityParamsId, Vector4.zero);

            if (_supportsConstantBuffers && HasValidBuffers())
            {
                GraphicsBuffer writeBuffer = _activeConfigBuffer != null && _activeConfigBuffer.IsValid()
                    ? _activeConfigBuffer
                    : _configBufferA;
                NativeArray<AccessibilityConfigDTO> mapped = writeBuffer.LockBufferForWrite<AccessibilityConfigDTO>(0, 1);
                try
                {
                    mapped[0] = disabled;
                }
                finally
                {
                    writeBuffer.UnlockBufferAfterWrite<AccessibilityConfigDTO>(1);
                }

                Shader.SetGlobalConstantBuffer(
                    AccessibilityCBufferId,
                    writeBuffer,
                    0,
                    InputBindingContractLayout.AccessibilityConfigStrideBytes);
            }

            _currentConfig = disabled;
            _lastUploadedConfig = disabled;
            _dirty = false;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static bool ConfigEquals(in AccessibilityConfigDTO a, in AccessibilityConfigDTO b)
        {
            return a.ColorMode == b.ColorMode &&
                   a.Flags == b.Flags &&
                   math.abs(a.FilterStrength01 - b.FilterStrength01) <= 0.000001f &&
                   math.abs(a.GlobalQualityWeight - b.GlobalQualityWeight) <= 0.000001f;
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizeTextScale(float scale)
        {
            if (!math.isfinite(scale) || scale <= 0f)
                return DefaultTextScale;

            return math.clamp(scale, MinimumTextScale, MaximumTextScale);
        }

        private static float SanitizeUiMotionScale(float scale)
        {
            if (!math.isfinite(scale))
                return DefaultUiMotionScale;

            return math.clamp(scale, MinimumUiMotionScale, MaximumUiMotionScale);
        }
    }
}

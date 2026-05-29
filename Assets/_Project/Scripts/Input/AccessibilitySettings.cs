using Hecton8.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Input
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30988)]
    public sealed class AccessibilitySettings : MonoBehaviour, IDispatcherSystem, IServiceShutdown
    {
        public const uint SystemHash = 0x41313332u;

        [Header("Color Filter")]
        [SerializeField] private AccessibilityColorFilterMode colorFilterMode = AccessibilityColorFilterMode.Off;
        [SerializeField, Range(0f, 1f)] private float filterStrength01 = 1f;
        [SerializeField, Range(0f, 1f)] private float globalQualityWeight = 1f;

        private static readonly int AccessibilityCBufferId = Shader.PropertyToID("HectonAccessibilityConfig");
        private static readonly int AccessibilityParamsId = Shader.PropertyToID("_HectonAccessibilityParams");

        private GraphicsBuffer _configBufferA;
        private GraphicsBuffer _configBufferB;
        private GraphicsBuffer _activeConfigBuffer;
        private AccessibilityConfigDTO _currentConfig;
        private AccessibilityConfigDTO _lastUploadedConfig;
        private int _writeBufferIndex;
        private bool _registered;
        private bool _dirty = true;
        private bool _uploaded;
        private bool _duplicateInstance;
        private bool _serviceShutdownComplete;
        private bool _supportsConstantBuffers;

        internal static AccessibilitySettings ActiveRuntimeInstance { get; private set; }

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
            _registered = GlobalRegistry.TryRegisterDispatcherSystem(this);
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
            RebuildConfig();
        }

        public void OnServiceShutdown()
        {
            if (_duplicateInstance || _serviceShutdownComplete)
                return;

            _serviceShutdownComplete = true;
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
        }

        public void SetColorFilter(AccessibilityColorFilterMode mode, float strength01, float qualityWeight01)
        {
            colorFilterMode = mode;
            filterStrength01 = Mathf.Clamp01(strength01);
            globalQualityWeight = Mathf.Clamp01(qualityWeight01);
            RebuildConfig();
        }

        public AccessibilityConfigDTO ReadCurrentConfig()
        {
            return _currentConfig;
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
                return;

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
        }

        private void RebuildConfig()
        {
            float strength = Mathf.Clamp01(filterStrength01);
            float quality = Mathf.Clamp01(globalQualityWeight);
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

            if (ActiveRuntimeInstance != null && !ReferenceEquals(ActiveRuntimeInstance, this))
            {
                _duplicateInstance = true;
                _serviceShutdownComplete = true;
                Destroy(gameObject);
                return false;
            }

            ActiveRuntimeInstance = this;
            return true;
        }

        private bool HasValidBuffers()
        {
            return _configBufferA != null &&
                   _configBufferA.IsValid() &&
                   _configBufferB != null &&
                   _configBufferB.IsValid();
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
    }
}

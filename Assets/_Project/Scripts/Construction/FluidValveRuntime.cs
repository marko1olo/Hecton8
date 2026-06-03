using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Interaction;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ValveVisualStateDTO
    {
        [FieldOffset(0)]
        public uint StableNodeHash;
        [FieldOffset(4)]
        public float VisualLoad01;
        [FieldOffset(8)]
        public float EmissionStrength;
        [FieldOffset(12)]
        public float Pulse01;
        [FieldOffset(16)]
        public float GlobalQualityWeight;
        [FieldOffset(20)]
        public ushort StatusRendererCount;
        [FieldOffset(22)]
        public byte Flags;
        [FieldOffset(23)]
        private byte _pad0;
        [FieldOffset(24)]
        private ulong _pad1;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/Fluid Valve Runtime")]
    public sealed class FluidValveRuntime : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static readonly Renderer[] s_emptyRenderers = Array.Empty<Renderer>();
        private const int MinimumVisualStride = 1;
        private const int MaximumVisualStride = 30;
        private const float SurvivalBypassThreshold = 0.075f;

        [Header("Cold Metadata")]
        [SerializeField] private NetworkNodeData networkNodeData;
        [SerializeField] private ValveMetadata valveMetadata;
        [SerializeField] private VRValveWheelHandle valveWheel;

        [Header("Status Visuals")]
        [SerializeField] private Renderer[] statusRenderers = s_emptyRenderers;
        [SerializeField, Range(0f, 1f)] private float initialVisualLoad01;
        [SerializeField, Min(0f)] private float baseEmissionStrength = 0.35f;
        [SerializeField, Min(0f)] private float maxEmissionStrength = 2.5f;

        private ValveVisualStateDTO _visualState;
        private float _visualLoad01;
        private float _lastResolvedLoad01 = -1f;
        private int _frameCounter;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _dispatcherAvailable;
        private bool _visualDirty;

        public NetworkNodeData NetworkNodeData => networkNodeData;
        public ValveMetadata ValveMetadata => valveMetadata;
        public VRValveWheelHandle ValveWheel => valveWheel;
        public float VisualLoad01 => _visualLoad01;
        public int StatusRendererCount => statusRenderers != null ? statusRenderers.Length : 0;

        private void Awake()
        {
            _visualLoad01 = Sanitize01(initialVisualLoad01);
            CacheColdReferences();
            ResolveVisualState(ResolveGlobalQualityWeight());
        }

        private void OnEnable()
        {
            _dispatcherAvailable = GlobalRegistry.Dispatcher != null;
            CacheColdReferences();
            TryRegisterHotSwapListener();
            _visualDirty = true;
            TryRegisterLateFrameTick();
        }

        private void OnDisable()
        {
            TryUnregisterLateFrameTick();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            _dispatcherAvailable = currentService != null;
            _registeredLateFrame = false;
            _visualDirty = true;
            if (_dispatcherAvailable && isActiveAndEnabled)
                TryRegisterLateFrameTick();
        }

        public void SetVisualLoad01(float load01)
        {
            float sanitized = Sanitize01(load01);
            if (math.abs(sanitized - _visualLoad01) <= 0.0001f)
                return;

            _visualLoad01 = sanitized;
            _visualDirty = true;
            TryRegisterLateFrameTick();
        }

        public void LateFrameTick()
        {
            SyncVisualLoadFromValveWheel();
            float quality = ResolveGlobalQualityWeight();
            _frameCounter++;
            int stride = ResolveVisualStride(quality);
            if (!_visualDirty && (_frameCounter % stride) != 0)
                return;

            ResolveVisualState(quality);
            _visualDirty = false;
        }

        private void CacheColdReferences()
        {
            if (networkNodeData == null)
                TryGetComponent(out networkNodeData);
            if (valveMetadata == null)
                TryGetComponent(out valveMetadata);
            if (valveWheel == null)
                TryGetComponent(out valveWheel);
        }

        private void SyncVisualLoadFromValveWheel()
        {
            VRValveWheelHandle wheel = valveWheel;
            if (wheel == null)
                return;

            float wheelLoad01 = Sanitize01(wheel.IsOpen01);
            if (math.abs(wheelLoad01 - _visualLoad01) <= 0.0001f)
                return;

            _visualLoad01 = wheelLoad01;
            _visualDirty = true;
        }

        public bool TryReadVisualState(out ValveVisualStateDTO state)
        {
            state = _visualState;
            return state.StatusRendererCount > 0;
        }

        private void ResolveVisualState(float globalQualityWeight)
        {
            float q = Smooth01(globalQualityWeight);
            float load = _visualLoad01;
            if (!_visualDirty && math.abs(load - _lastResolvedLoad01) <= 0.0001f && q <= SurvivalBypassThreshold)
                return;

            float overload01 = math.saturate((load - 0.82f) * 5.555555f);
            float pulse01 = q <= SurvivalBypassThreshold
                ? 0f
                : ResolveTrianglePulse01(Time.unscaledTime * math.lerp(0.25f, 1.4f, q));
            float strength = math.lerp(baseEmissionStrength, maxEmissionStrength, load * math.lerp(0.35f, 1f, q));
            strength += pulse01 * q * load * 0.35f;

            _visualState = new ValveVisualStateDTO
            {
                StableNodeHash = networkNodeData != null ? networkNodeData.StableNodeHash : 0u,
                VisualLoad01 = load,
                EmissionStrength = strength,
                Pulse01 = pulse01,
                GlobalQualityWeight = q,
                StatusRendererCount = (ushort)math.min(StatusRendererCount, ushort.MaxValue),
                Flags = (byte)(overload01 > 0f ? 1 : 0)
            };
            _lastResolvedLoad01 = load;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrame || !Application.isPlaying || !_dispatcherAvailable || StatusRendererCount <= 0)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
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

        private static int ResolveVisualStride(float globalQualityWeight)
        {
            float q = Smooth01(globalQualityWeight);
            int stride = (int)math.round(math.lerp(MaximumVisualStride, MinimumVisualStride, q));
            return math.clamp(stride, MinimumVisualStride, MaximumVisualStride);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return Sanitize01(quality);
        }

        private static float Smooth01(float value)
        {
            float q = Sanitize01(value);
            return q * q * (3f - 2f * q);
        }

        private static float ResolveTrianglePulse01(float phase)
        {
            if (!math.isfinite(phase))
                return 0f;

            float wrapped = phase - math.floor(phase);
            return 1f - math.abs((wrapped * 2f) - 1f);
        }

        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        public static bool ValidateUnmanagedLayout(out int visualStateBytes)
        {
            visualStateBytes = UnsafeUtility.SizeOf<ValveVisualStateDTO>();
            return visualStateBytes == 32 &&
                   (visualStateBytes & 7) == 0 &&
                   OffsetOf<ValveVisualStateDTO>(nameof(ValveVisualStateDTO.StableNodeHash)) == 0 &&
                   OffsetOf<ValveVisualStateDTO>(nameof(ValveVisualStateDTO.VisualLoad01)) == 4 &&
                   OffsetOf<ValveVisualStateDTO>(nameof(ValveVisualStateDTO.EmissionStrength)) == 8 &&
                   OffsetOf<ValveVisualStateDTO>(nameof(ValveVisualStateDTO.Pulse01)) == 12 &&
                   OffsetOf<ValveVisualStateDTO>(nameof(ValveVisualStateDTO.GlobalQualityWeight)) == 16 &&
                   OffsetOf<ValveVisualStateDTO>(nameof(ValveVisualStateDTO.StatusRendererCount)) == 20 &&
                   OffsetOf<ValveVisualStateDTO>(nameof(ValveVisualStateDTO.Flags)) == 22;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }

#if UNITY_EDITOR
        public void ConfigureEditorBake(
            NetworkNodeData bakedNodeData,
            ValveMetadata bakedValveMetadata,
            VRValveWheelHandle bakedValveWheel,
            Renderer[] bakedStatusRenderers,
            float bakedInitialVisualLoad01,
            float bakedBaseEmissionStrength,
            float bakedMaxEmissionStrength)
        {
            networkNodeData = bakedNodeData;
            valveMetadata = bakedValveMetadata;
            valveWheel = bakedValveWheel;
            statusRenderers = bakedStatusRenderers != null ? bakedStatusRenderers : s_emptyRenderers;
            initialVisualLoad01 = Sanitize01(bakedInitialVisualLoad01);
            baseEmissionStrength = math.max(0f, bakedBaseEmissionStrength);
            maxEmissionStrength = math.max(baseEmissionStrength, bakedMaxEmissionStrength);
            _visualLoad01 = initialVisualLoad01;
            _visualDirty = true;
            ResolveVisualState(ResolveGlobalQualityWeight());
        }
#endif
    }
}

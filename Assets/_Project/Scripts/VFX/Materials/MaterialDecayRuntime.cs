using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.VFX.Materials
{
    /// <summary>
    /// Drains durability/stress signals into global shader uniforms for material-only wear presentation.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-86)]
    [AddComponentMenu("Hecton8/VFX/Material Decay Runtime")]
    public sealed class MaterialDecayRuntime : MonoBehaviour,
        ILateFrameTickable,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener
    {
        private static int s_x001MaterialDecayRuntimeSignalPushDropCount;
        private const int TelemetryCapacity = 300;
        private const int MaterialDecayStateSizeBytes = 32;
        private const float RustPomGate = 0.3f;
        private const float WetnessFadeSeconds = 5f;
        private const float ShaderUniformEpsilon = 0.0005f;
        private const uint MaterialDecayToolHash = 0x4D445043u; // MDPC
        private const byte TelemetryFlagRustActive = 1 << 0;
        private const byte TelemetryFlagWet = 1 << 1;
        private const byte TelemetryFlagBlood = 1 << 2;

        private static readonly int HectonEquipmentRust01Id = Shader.PropertyToID("_HectonEquipmentRust01");
        private static readonly int HectonMaterialDecayRuntimeId = Shader.PropertyToID("_HectonMaterialDecayRuntime");
        private static readonly int HectonPlayerBloodSplatterId = Shader.PropertyToID("_HectonPlayerBloodSplatter");
        private static readonly int RustDetailMapId = Shader.PropertyToID("_RustDetailMap");
        private static readonly int RustDetailMapStId = Shader.PropertyToID("_RustDetailMap_ST");

        private static MaterialDecayRuntime s_runtimeInstance;

        [SerializeField]
        private Texture2D rustDetailAtlas;

        [SerializeField, Range(0f, 1f)]
        private float defaultRust01;

        [SerializeField, Range(0f, 1f)]
        private float bloodGlossBoost = 0.78f;

        [SerializeField, Range(0f, 1f)]
        private float rustAcousticIntensity = 0.38f;

        private IDataVault _dataVault;
        private VaultGenerationHandle<MaterialDecayState> _blackBoxHandle;
        private ITickDispatcher _tickDispatcher;
        private Vector4 _lastRuntimeVector = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
        private Vector4 _lastBloodVector = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
        private float _lastUploadedRust01 = float.NaN;
        private float _rust01;
        private float _wetnessFadeRemaining;
        private float _stress01;
        private float _healthDamage01;
        private int _blackBoxCursor;
        private int _lastStressSequence;
        private int _lastAcousticFrame = int.MinValue;
        private uint _lastItemHash;
        private ushort _lastSlotIndex;
        private byte _lastReason;
        private byte _qualityWeightByte = 255;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private bool _dispatcherReady;
        private bool _hasDurabilitySignal;
        private bool _shaderGlobalsDirty;
#pragma warning disable CS0414
        private bool _blackBoxReady;
#pragma warning restore CS0414
        private bool _dumpedFault;
        private float _globalQualityWeight01 = 1f;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_runtimeInstance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneRuntime()
        {
            if (!Application.isPlaying)
                return;

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "00_BOOTSTRAP" || sceneName == "01_MAIN_MENU" || sceneName == "01_ORBIT")
                return;

            if (s_runtimeInstance != null)
                return;

            // Hecton8.Core.H8Debug.LogError("[MaterialDecayRuntime] Missing authored scene instance. Runtime component creation is forbidden.");
        }

        private void Awake()
        {
            if (s_runtimeInstance != null && !ReferenceEquals(s_runtimeInstance, this))
            {
                enabled = false;
                return;
            }

            s_runtimeInstance = this;
            if (!HasAuthoredRustAtlas())
                return;

            _rust01 = SanitizeUnit(defaultRust01);
            RefreshQualityState();
            EnsureBlackBox();
            BindRustAtlas();
        }

        private void OnEnable()
        {
            if (s_runtimeInstance != null && !ReferenceEquals(s_runtimeInstance, this))
            {
                enabled = false;
                return;
            }

            s_runtimeInstance = this;
            if (!HasAuthoredRustAtlas())
                return;

            TryRegisterHotSwapListener();
            RefreshQualityState();
            EnsureBlackBox();
            BindRustAtlas();
            UploadShaderGlobals(force: true);
            TryRegisterTick();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
            _tickDispatcher = null;
            _dispatcherReady = false;
            if (Application.isPlaying)
            {
                UploadZeroState();
                ReleaseBlackBoxBuffer();
            }
            else
            {
                ClearBlackBoxDescriptor();
            }
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;

            if (Application.isPlaying)
                ReleaseBlackBoxBuffer();
            else
                ClearBlackBoxDescriptor();
        }

        private void AdvanceMaterialDecayState(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime < 0f)
            {
                DumpBlackBox(reason: 1);
                return;
            }

            ConsumeDurabilitySignals();
            ConsumePlayerState();
            RefreshQualityState();

            if (_wetnessFadeRemaining > 0f)
                _wetnessFadeRemaining = math.max(0f, _wetnessFadeRemaining - deltaTime);

            _shaderGlobalsDirty = true;
            PushBlackBox();
        }

        public void LateFrameTick()
        {
            AdvanceMaterialDecayState(SystemDispatcher.CurrentFrameDeltaTime);

            if (!_shaderGlobalsDirty)
                return;

            _shaderGlobalsDirty = false;
            UploadShaderGlobals(force: false);
        }

        private void TryRegisterTick()
        {
            if (!Application.isPlaying || !_dispatcherReady)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTick()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

        }

        private void TryRegisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);

            RefreshCachedRegistryServices();
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        private void RefreshCachedRegistryServices()
        {
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistry.TickDispatcher);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DataVault, GlobalRegistry.DataVault);
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                ITickDispatcher tickDispatcher = currentService as ITickDispatcher;
                if (!ReferenceEquals(_tickDispatcher, tickDispatcher))
                {
                    TryUnregisterTick();
                    _tickDispatcher = tickDispatcher;
                }

                _dispatcherReady = tickDispatcher != null;
                if (_dispatcherReady)
                    TryRegisterTick();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                BindDataVault(currentService is IDataVault currentVault ? currentVault : null);
        }

        private void ConsumeDurabilitySignals()
        {
            ReadOnlySpan<ItemDurabilityChangedSignal> signals = SignalBus<ItemDurabilityChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ItemDurabilityChangedSignal signal = signals[i];
                float averageDurability01 = SanitizeUnit(signal.AverageEquippedDurability01);
                float durability01 = SanitizeUnit(signal.Durability01);
                float resolvedDurability01 = signal.ItemHash == 0u ? averageDurability01 : math.min(averageDurability01, durability01);
                float rust01 = math.saturate(1f - resolvedDurability01);
                if (!math.isfinite(rust01))
                {
                    DumpBlackBox(reason: 2);
                    continue;
                }

                _hasDurabilitySignal = true;
                _rust01 = rust01;
                _lastItemHash = signal.ItemHash;
                _lastSlotIndex = signal.SlotIndex;
                _lastReason = signal.Reason;

                if (signal.Reason == ItemDurabilityChangedSignal.ReasonCorrosion && rust01 > 0.001f)
                    _wetnessFadeRemaining = WetnessFadeSeconds;

                PublishRustAcoustic(in signal, rust01);
            }
        }

        private void ConsumePlayerState()
        {
            ReadOnlySpan<PlayerStressSignal> stressSignals = SignalBus<PlayerStressSignal>.GetFrameSnapshot();
            for (int i = 0; i < stressSignals.Length; i++)
            {
                PlayerStressSignal stressSignal = stressSignals[i];
                int sequence = unchecked((int)stressSignal.Frame);
                if (sequence == _lastStressSequence)
                    continue;

                _lastStressSequence = sequence;
                _stress01 = SanitizeUnit(stressSignal.Stress01);
            }

            float health01 = UIStateStore.ReadValueOrDefault(UIValueSlotId.Health01, 1f);
            _healthDamage01 = 1f - SanitizeUnit(health01);
        }

        private void PublishRustAcoustic(in ItemDurabilityChangedSignal signal, float rust01)
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (rust01 <= RustPomGate || signal.ItemHash == 0u || frame == _lastAcousticFrame)
                return;

            _lastAcousticFrame = frame;
            ToolAcousticSignal acousticSignal = new ToolAcousticSignal
            {
                ToolHash = MaterialDecayToolHash,
                TargetHash = signal.ItemHash,
                Progress01 = rust01,
                PitchScale = math.lerp(1f, 0.70f, rust01),
                Intensity01 = math.saturate(rust01 * rustAcousticIntensity),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                State = signal.Reason,
                Flags = signal.Flags
            };
            SignalBus<ToolAcousticSignal>.TryPushTracked(in acousticSignal, ref s_x001MaterialDecayRuntimeSignalPushDropCount);
        }

        private void UploadShaderGlobals(bool force)
        {
            float rust01 = _hasDurabilitySignal ? SanitizeUnit(_rust01) : SanitizeUnit(defaultRust01);
            float wetness01 = math.saturate(_wetnessFadeRemaining * (1f / WetnessFadeSeconds));
            float qualityPressure01 = ResolveQualityPressure01();
            float stableSeed01 = (_lastItemHash & 0x3FFu) * (1f / 1023f);
            Vector4 runtimeVector = new Vector4(rust01, wetness01, qualityPressure01, stableSeed01);
            float bloodActive01 = math.saturate(math.max(_stress01, _healthDamage01));
            Vector4 bloodVector = new Vector4(_stress01, _healthDamage01, bloodGlossBoost, bloodActive01);

            if (force || math.abs(_lastUploadedRust01 - rust01) > ShaderUniformEpsilon)
            {
                Shader.SetGlobalFloat(HectonEquipmentRust01Id, rust01);
                _lastUploadedRust01 = rust01;
            }

            if (force || HasVectorChanged(runtimeVector, _lastRuntimeVector))
            {
                Shader.SetGlobalVector(HectonMaterialDecayRuntimeId, runtimeVector);
                _lastRuntimeVector = runtimeVector;
            }

            if (force || HasVectorChanged(bloodVector, _lastBloodVector))
            {
                Shader.SetGlobalVector(HectonPlayerBloodSplatterId, bloodVector);
                _lastBloodVector = bloodVector;
            }
        }

        private void UploadZeroState()
        {
            Shader.SetGlobalVector(HectonMaterialDecayRuntimeId, Vector4.zero);
            Shader.SetGlobalVector(HectonPlayerBloodSplatterId, Vector4.zero);
            _lastRuntimeVector = Vector4.zero;
            _lastBloodVector = Vector4.zero;
            _lastUploadedRust01 = float.NaN;
        }

        private void BindRustAtlas()
        {
            if (!HasAuthoredRustAtlas())
                return;

            Shader.SetGlobalTexture(RustDetailMapId, rustDetailAtlas);
            Shader.SetGlobalVector(RustDetailMapStId, new Vector4(1f, 1f, 0f, 0f));
        }

        private bool HasAuthoredRustAtlas()
        {
            if (rustDetailAtlas != null)
                return true;

            Hecton8.Core.H8Debug.LogError("[MaterialDecayRuntime] Missing authored rust detail atlas. Runtime atlas generation is forbidden.", this);
            enabled = false;
            return false;
        }

        private void BindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseBlackBoxBuffer();
            _dataVault = vault;
            _blackBoxReady = false;
        }

        private bool EnsureBlackBox()
        {
            if (!ValidateNativeLayout())
            {
                ReleaseBlackBoxBuffer();
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ClearBlackBoxDescriptor();
                return false;
            }
            if (vault.IsCompactionFenceActive)
            {
                ClearBlackBoxDescriptor();
                return false;
            }

            if (IsMaterialDecayBlackBoxHandle(in _blackBoxHandle) &&
                vault.TryResolveHandle(in _blackBoxHandle, out NativeArray<MaterialDecayState> currentBlackBox) &&
                currentBlackBox.IsCreated &&
                currentBlackBox.Length >= TelemetryCapacity)
            {
                _blackBoxReady = true;
                return true;
            }

            ClearBlackBoxDescriptor();
            if (vault.TryGetGenerationHandle(
                    BufferID.MaterialDecayBlackBox,
                    out VaultGenerationHandle<MaterialDecayState> existing) &&
                IsMaterialDecayBlackBoxHandle(in existing) &&
                vault.TryResolveHandle(in existing, out NativeArray<MaterialDecayState> existingBlackBox) &&
                existingBlackBox.IsCreated &&
                existingBlackBox.Length >= TelemetryCapacity)
            {
                _blackBoxHandle = existing;
                _blackBoxReady = true;
                return true;
            }

            if (vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<MaterialDecayState> acquired = vault.EnsureGenerationHandle<MaterialDecayState>(
                BufferID.MaterialDecayBlackBox,
                TelemetryCapacity,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            if (!IsMaterialDecayBlackBoxHandle(in acquired) ||
                !vault.TryResolveHandle(in acquired, out NativeArray<MaterialDecayState> acquiredBlackBox) ||
                !acquiredBlackBox.IsCreated ||
                acquiredBlackBox.Length < TelemetryCapacity)
            {
                ReleaseVaultBuffer(vault, ref acquired);
                ClearBlackBoxDescriptor();
                return false;
            }

            _blackBoxHandle = acquired;
            _blackBoxReady = true;
            return true;
        }

        private bool TryResolveBlackBox(out NativeArray<MaterialDecayState> blackBox)
        {
            blackBox = default;
            if (!EnsureBlackBox())
                return false;

            if (_dataVault == null ||
                !IsMaterialDecayBlackBoxHandle(in _blackBoxHandle) ||
                !_dataVault.TryResolveHandle(in _blackBoxHandle, out blackBox) ||
                !blackBox.IsCreated ||
                blackBox.Length < TelemetryCapacity)
            {
                ClearBlackBoxDescriptor();
                return false;
            }

            return true;
        }

        private void ClearBlackBoxDescriptor()
        {
            _blackBoxHandle = default;
            _blackBoxReady = false;
        }

        private void ReleaseBlackBoxBuffer()
        {
            ReleaseVaultBuffer(_dataVault, ref _blackBoxHandle);
            _blackBoxReady = false;
        }

        private static bool ValidateNativeLayout()
        {
            return UnsafeUtility.SizeOf<MaterialDecayState>() == MaterialDecayStateSizeBytes;
        }

        private static bool IsMaterialDecayBlackBoxHandle(in VaultGenerationHandle<MaterialDecayState> handle)
        {
            return handle.BufferID == unchecked((uint)(int)BufferID.MaterialDecayBlackBox) &&
                   handle.SystemID == (uint)SystemID.Vfx &&
                   handle.Generation != 0u;
        }

        private static void ReleaseVaultBuffer(IDataVault vault, ref VaultGenerationHandle<MaterialDecayState> handle)
        {
            if (vault != null && IsMaterialDecayBlackBoxHandle(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void PushBlackBox()
        {
            if (!TryResolveBlackBox(out var blackBox))
                return;

            float rust01 = _hasDurabilitySignal ? SanitizeUnit(_rust01) : SanitizeUnit(defaultRust01);
            float wetness01 = math.saturate(_wetnessFadeRemaining * (1f / WetnessFadeSeconds));
            byte flags = 0;
            if (rust01 > RustPomGate) flags |= TelemetryFlagRustActive;
            if (wetness01 > 0.001f) flags |= TelemetryFlagWet;
            if (math.max(_stress01, _healthDamage01) > 0.001f) flags |= TelemetryFlagBlood;

            blackBox[_blackBoxCursor] = new MaterialDecayState
            {
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                ItemHash = _lastItemHash,
                Rust01 = rust01,
                Wetness01 = wetness01,
                Blood01 = math.saturate(math.max(_stress01, _healthDamage01)),
                SlotIndex = _lastSlotIndex,
                Reason = _lastReason,
                QualityWeightByte = _qualityWeightByte,
                Flags = flags,
                StateHash = Mix(_lastItemHash ^ (uint)(_lastSlotIndex << 16) ^ (uint)(_lastReason << 8) ^ ((uint)_qualityWeightByte << 24) ^ flags)
            };

            _blackBoxCursor++;
            if (_blackBoxCursor >= TelemetryCapacity)
                _blackBoxCursor = 0;
        }

        private void DumpBlackBox(byte reason)
        {
            if (_dumpedFault || !TryResolveBlackBox(out var blackBox))
                return;

            NativeArray<byte> payload = default;
            try
            {
                const string dumpPath = "Docs/AgentLogs/Dump_MATERIAL_DECAY_ARTIST.bin";
                const int headerBytes = 13;
                const int rowBytes = 29;
                int byteCount = headerBytes + blackBox.Length * rowBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(MaterialDecayRuntime),
                    "MaterialDecayTelemetryDumpPayload");
                unsafe
                {
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    WriteUInt32LittleEndian(target, 0, 0x4D445350u);
                    target[4] = reason;
                    WriteInt32LittleEndian(target, 5, _blackBoxCursor);
                    WriteInt32LittleEndian(target, 9, blackBox.Length);
                    int offset = headerBytes;
                    for (int i = 0; i < blackBox.Length; i++)
                    {
                        MaterialDecayState state = blackBox[(_blackBoxCursor + i) % blackBox.Length];
                        WriteUInt32LittleEndian(target, offset, state.Frame);
                        WriteUInt32LittleEndian(target, offset + 4, state.ItemHash);
                        WriteFloatLittleEndian(target, offset + 8, state.Rust01);
                        WriteFloatLittleEndian(target, offset + 12, state.Wetness01);
                        WriteFloatLittleEndian(target, offset + 16, state.Blood01);
                        WriteUInt16LittleEndian(target, offset + 20, state.SlotIndex);
                        target[offset + 22] = state.Reason;
                        target[offset + 23] = state.QualityWeightByte;
                        target[offset + 24] = state.Flags;
                        WriteUInt32LittleEndian(target, offset + 25, state.StateHash);
                        offset += rowBytes;
                    }
                }

                _dumpedFault = NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, byteCount);
            }
            catch (Exception)
            {
                _dumpedFault = false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(MaterialDecayRuntime),
                    "MaterialDecayTelemetryDumpPayload");
            }
        }

        private static bool HasVectorChanged(Vector4 a, Vector4 b)
        {
            return math.abs(a.x - b.x) > ShaderUniformEpsilon ||
                   math.abs(a.y - b.y) > ShaderUniformEpsilon ||
                   math.abs(a.z - b.z) > ShaderUniformEpsilon ||
                   math.abs(a.w - b.w) > ShaderUniformEpsilon;
        }

        private static float SanitizeUnit(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private void RefreshQualityState()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            _globalQualityWeight01 = math.isfinite(weight) ? math.saturate(weight) : 1f;
            _qualityWeightByte = EncodeQualityWeightByte(_globalQualityWeight01);
        }

        private float ResolveQualityPressure01()
        {
            float pressure = 1f - _globalQualityWeight01;
            return math.smoothstep(0f, 1f, pressure);
        }

        private static byte EncodeQualityWeightByte(float qualityWeight01)
        {
            int encoded = (int)math.round(SanitizeUnit(qualityWeight01) * 255f);
            return (byte)math.clamp(encoded, 0, 255);
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static unsafe void WriteUInt16LittleEndian(byte* target, int offset, ushort value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
        }

        private static unsafe void WriteInt32LittleEndian(byte* target, int offset, int value)
        {
            WriteUInt32LittleEndian(target, offset, unchecked((uint)value));
        }

        private static unsafe void WriteFloatLittleEndian(byte* target, int offset, float value)
        {
            WriteUInt32LittleEndian(target, offset, math.asuint(value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        [StructLayout(LayoutKind.Explicit, Size = MaterialDecayStateSizeBytes)]
        private struct MaterialDecayState
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint ItemHash;
            [FieldOffset(8)] public float Rust01;
            [FieldOffset(12)] public float Wetness01;
            [FieldOffset(16)] public float Blood01;
            [FieldOffset(20)] public ushort SlotIndex;
            [FieldOffset(22)] public byte Reason;
            [FieldOffset(23)] public byte QualityWeightByte;
            [FieldOffset(24)] public byte Flags;
            [FieldOffset(25)] private byte _pad0;
            [FieldOffset(26)] private ushort _pad1;
            [FieldOffset(28)] public uint StateHash;
        }
    }
}

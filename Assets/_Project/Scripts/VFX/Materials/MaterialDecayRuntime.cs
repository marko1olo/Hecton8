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
    public sealed class MaterialDecayRuntime : MonoBehaviour, IUpdatable
    {
        private const int TelemetryCapacity = 300;
        private const int MaterialDecayStateSizeBytes = 28;
        private const int RustAtlasSize = 512;
        private const float RustPomGate = 0.3f;
        private const float WetnessFadeSeconds = 5f;
        private const float ShaderUniformEpsilon = 0.0005f;
        private const uint MaterialDecayToolHash = 0x4D445043u; // MDPC
        private const byte TelemetryFlagLowTier = 1 << 0;
        private const byte TelemetryFlagRustActive = 1 << 1;
        private const byte TelemetryFlagWet = 1 << 2;
        private const byte TelemetryFlagBlood = 1 << 3;

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
        private VaultBufferHandle<MaterialDecayState> _blackBoxHandle;
        private Texture2D _runtimeFallbackAtlas;
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
        private bool _registered;
        private bool _hasDurabilitySignal;
        private bool _blackBoxReady;
        private bool _dumpedFault;

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

            if (s_runtimeInstance != null)
                return;

            // COLD ALLOC: one scene-local bridge only when authoring has not placed the component.
            GameObject host = new GameObject("H8_MaterialDecayRuntime");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<MaterialDecayRuntime>();
        }

        private void Awake()
        {
            if (s_runtimeInstance != null && !ReferenceEquals(s_runtimeInstance, this))
            {
                enabled = false;
                return;
            }

            s_runtimeInstance = this;
            _rust01 = SanitizeUnit(defaultRust01);
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
            EnsureBlackBox();
            BindRustAtlas();
            UploadShaderGlobals(force: true);
            TryRegisterTick();
        }

        private void Start()
        {
            TryRegisterTick();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            UploadZeroState();
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;

            ClearBlackBoxLease();

            if (_runtimeFallbackAtlas != null)
            {
                Destroy(_runtimeFallbackAtlas);
                _runtimeFallbackAtlas = null;
            }
        }

        public void Tick(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime < 0f)
            {
                DumpBlackBox(reason: 1);
                return;
            }

            ConsumeDurabilitySignals();
            ConsumePlayerState();

            if (_wetnessFadeRemaining > 0f)
                _wetnessFadeRemaining = math.max(0f, _wetnessFadeRemaining - deltaTime);

            UploadShaderGlobals(force: false);
            PushBlackBox();
        }

        private void TryRegisterTick()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
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
            if (GlobalSignals.TryGetLatestPlayerStressSignal(out PlayerStressSignal stressSignal, out int sequence) &&
                sequence != _lastStressSequence)
            {
                _lastStressSequence = sequence;
                _stress01 = SanitizeUnit(stressSignal.Stress01);
            }

            float health01 = UIStateStore.ReadValueOrDefault(UIValueSlotId.Health01, 1f);
            _healthDamage01 = 1f - SanitizeUnit(health01);
        }

        private void PublishRustAcoustic(in ItemDurabilityChangedSignal signal, float rust01)
        {
            int frame = Time.frameCount;
            if (rust01 <= RustPomGate || signal.ItemHash == 0u || frame == _lastAcousticFrame)
                return;

            _lastAcousticFrame = frame;
            GlobalSignals.Publish(new ToolAcousticSignal
            {
                ToolHash = MaterialDecayToolHash,
                TargetHash = signal.ItemHash,
                Progress01 = rust01,
                PitchScale = math.lerp(1f, 0.70f, rust01),
                Intensity01 = math.saturate(rust01 * rustAcousticIntensity),
                Frame = (uint)math.max(0, frame),
                State = signal.Reason,
                Flags = signal.Flags
            });
        }

        private void UploadShaderGlobals(bool force)
        {
            float rust01 = _hasDurabilitySignal ? SanitizeUnit(_rust01) : SanitizeUnit(defaultRust01);
            float wetness01 = math.saturate(_wetnessFadeRemaining * (1f / WetnessFadeSeconds));
            float lowTier01 = IsLowTier(GlobalRegistry.ScalabilityTier) ? 1f : 0f;
            float stableSeed01 = (_lastItemHash & 0x3FFu) * (1f / 1023f);
            Vector4 runtimeVector = new Vector4(rust01, wetness01, lowTier01, stableSeed01);
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
            Texture2D atlas = rustDetailAtlas != null ? rustDetailAtlas : ResolveFallbackAtlas();
            if (atlas != null)
            {
                Shader.SetGlobalTexture(RustDetailMapId, atlas);
                Shader.SetGlobalVector(RustDetailMapStId, new Vector4(1f, 1f, 0f, 0f));
            }
        }

        private Texture2D ResolveFallbackAtlas()
        {
            if (_runtimeFallbackAtlas != null)
                return _runtimeFallbackAtlas;

            _runtimeFallbackAtlas = CreateFallbackRustAtlas();
            return _runtimeFallbackAtlas;
        }

        private static Texture2D CreateFallbackRustAtlas()
        {
            Texture2D texture = new Texture2D(RustAtlasSize, RustAtlasSize, TextureFormat.RGBA32, mipChain: true, linear: true)
            {
                name = "TX_Runtime_MaterialDecay_RustDetail_512",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1,
                hideFlags = HideFlags.DontSave
            };

            var pixels = texture.GetRawTextureData<Color32>();
            for (int y = 0; y < RustAtlasSize; y++)
            {
                for (int x = 0; x < RustAtlasSize; x++)
                {
                    uint hash = Mix((uint)x * 0x9E3779B9u ^ (uint)y * 0x85EBCA6Bu);
                    uint hashB = Mix(hash ^ 0xC2B2AE35u);
                    float ridge = math.saturate(((hash & 0xFFu) * (1f / 255f) - 0.28f) * 1.55f);
                    byte height = (byte)math.clamp(math.round(35f + ridge * 210f), 0f, 255f);
                    byte normalX = (byte)math.clamp(128 + (int)((sbyte)(hashB & 0x7Fu) - 63) / 3, 0, 255);
                    byte normalY = (byte)math.clamp(128 + (int)((sbyte)((hashB >> 8) & 0x7Fu) - 63) / 3, 0, 255);
                    byte roughness = (byte)math.clamp(184 + (int)((hash >> 16) & 0x47u), 0, 255);
                    pixels[y * RustAtlasSize + x] = new Color32(height, normalX, normalY, roughness);
                }
            }

            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        private bool EnsureBlackBox()
        {
            if (!ValidateNativeLayout())
            {
                ClearBlackBoxLease();
                return false;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                ClearBlackBoxLease();
                return false;
            }

            if (!ReferenceEquals(_dataVault, vault))
            {
                _dataVault = vault;
                _blackBoxHandle = default;
                _blackBoxReady = false;
            }

            if (!vault.TryGetBufferHandle(BufferID.MaterialDecayBlackBox, out _blackBoxHandle) ||
                !_blackBoxHandle.IsCreated)
            {
                _blackBoxHandle = vault.GetBufferHandle<MaterialDecayState>(
                    BufferID.MaterialDecayBlackBox,
                    TelemetryCapacity,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            _blackBoxReady = _blackBoxHandle.IsCreated && _blackBoxHandle.Length >= TelemetryCapacity;
            return _blackBoxReady;
        }

        private bool TryResolveBlackBox(out NativeArray<MaterialDecayState> blackBox)
        {
            blackBox = default;
            if (!EnsureBlackBox())
                return false;

            if (!_dataVault.TryGetBufferHandle(BufferID.MaterialDecayBlackBox, out _blackBoxHandle) ||
                !_blackBoxHandle.IsCreated)
            {
                ClearBlackBoxLease();
                return false;
            }

            blackBox = _blackBoxHandle.Resolve(_dataVault);
            return blackBox.IsCreated && blackBox.Length >= TelemetryCapacity;
        }

        private void ClearBlackBoxLease()
        {
            _dataVault = null;
            _blackBoxHandle = default;
            _blackBoxReady = false;
        }

        private static bool ValidateNativeLayout()
        {
            return UnsafeUtility.SizeOf<MaterialDecayState>() == MaterialDecayStateSizeBytes;
        }

        private void PushBlackBox()
        {
            if (!TryResolveBlackBox(out var blackBox))
                return;

            float rust01 = _hasDurabilitySignal ? SanitizeUnit(_rust01) : SanitizeUnit(defaultRust01);
            float wetness01 = math.saturate(_wetnessFadeRemaining * (1f / WetnessFadeSeconds));
            byte flags = 0;
            if (IsLowTier(GlobalRegistry.ScalabilityTier)) flags |= TelemetryFlagLowTier;
            if (rust01 > RustPomGate) flags |= TelemetryFlagRustActive;
            if (wetness01 > 0.001f) flags |= TelemetryFlagWet;
            if (math.max(_stress01, _healthDamage01) > 0.001f) flags |= TelemetryFlagBlood;

            blackBox[_blackBoxCursor] = new MaterialDecayState
            {
                Frame = (uint)math.max(0, Time.frameCount),
                ItemHash = _lastItemHash,
                Rust01 = rust01,
                Wetness01 = wetness01,
                Blood01 = math.saturate(math.max(_stress01, _healthDamage01)),
                SlotIndex = _lastSlotIndex,
                Reason = _lastReason,
                Flags = flags,
                StateHash = Mix(_lastItemHash ^ (uint)(_lastSlotIndex << 16) ^ (uint)(_lastReason << 8) ^ flags)
            };

            _blackBoxCursor++;
            if (_blackBoxCursor >= TelemetryCapacity)
                _blackBoxCursor = 0;
        }

        private void DumpBlackBox(byte reason)
        {
            if (_dumpedFault || !TryResolveBlackBox(out var blackBox))
                return;

            _dumpedFault = true;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
                Directory.CreateDirectory(logDirectory);
                string dumpPath = Path.Combine(logDirectory, "Dump_MATERIAL_DECAY_ARTIST.bin");
                using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write((uint)0x4D445350u); // MDSP
                writer.Write(reason);
                writer.Write(_blackBoxCursor);
                writer.Write(blackBox.Length);
                for (int i = 0; i < blackBox.Length; i++)
                {
                    MaterialDecayState state = blackBox[(_blackBoxCursor + i) % blackBox.Length];
                    writer.Write(state.Frame);
                    writer.Write(state.ItemHash);
                    writer.Write(state.Rust01);
                    writer.Write(state.Wetness01);
                    writer.Write(state.Blood01);
                    writer.Write(state.SlotIndex);
                    writer.Write(state.Reason);
                    writer.Write(state.Flags);
                    writer.Write(state.StateHash);
                }
            }
            catch (Exception)
            {
                _dumpedFault = false;
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

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = MaterialDecayStateSizeBytes)]
        private struct MaterialDecayState
        {
            public uint Frame;
            public uint ItemHash;
            public float Rust01;
            public float Wetness01;
            public float Blood01;
            public ushort SlotIndex;
            public byte Reason;
            public byte Flags;
            public uint StateHash;
        }
    }
}

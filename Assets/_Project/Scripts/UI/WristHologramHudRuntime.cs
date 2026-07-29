using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using ToxicityExposureSignal = Hecton8.Atmosphere.ToxicityExposureSignal;

namespace Hecton8.UI
{
    [StructLayout(LayoutKind.Explicit, Size = 112)]
    public struct WristHudQuadTransformDTO
    {
        [FieldOffset(0)]
        public float4x4 Matrix;
        [FieldOffset(64)]
        public float4 Color;
        [FieldOffset(80)]
        public float4 UVRect;
        [FieldOffset(96)]
        public uint CharacterCode;
        [FieldOffset(100)]
        public float GlitchIntensity;
        [FieldOffset(104)]
        private byte _pad104;
        [FieldOffset(105)]
        private byte _pad105;
        [FieldOffset(106)]
        private byte _pad106;
        [FieldOffset(107)]
        private byte _pad107;
        [FieldOffset(108)]
        private byte _pad108;
        [FieldOffset(109)]
        private byte _pad109;
        [FieldOffset(110)]
        private byte _pad110;
        [FieldOffset(111)]
        private byte _pad111;
    }

    [StructLayout(LayoutKind.Explicit, Size = 248)]
    public struct WristHudStateDTO
    {
        [FieldOffset(0)]
        public float4 WristPositionAndDistance;
        [FieldOffset(16)]
        public float4 WristRotation;
        [FieldOffset(32)]
        public float4 HeadPositionAndO2;
        [FieldOffset(48)]
        public float4 HeadForwardAndDepth;
        [FieldOffset(64)]
        public float4 WristRightAndSafeDepth;
        [FieldOffset(80)]
        public float4 WristUpAndPressure;
        [FieldOffset(96)]
        public float4 WristForwardAndRadiation;
        [FieldOffset(112)]
        public float4 LowColor;
        [FieldOffset(128)]
        public float4 MidColor;
        [FieldOffset(144)]
        public float4 DangerColor;
        [FieldOffset(160)]
        public float4 PdaGridCenterAndCell;
        [FieldOffset(176)]
        public float4 CompassAndVitals;
        [FieldOffset(192)]
        public int FrameIndex;
        [FieldOffset(196)]
        public int ActiveQuadCount;
        [FieldOffset(200)]
        public int GlyphQuadCount;
        [FieldOffset(204)]
        public int BarQuadCount;
        [FieldOffset(208)]
        public int PdaGridQuadCount;
        [FieldOffset(212)]
        public int RadarQuadCount;
        [FieldOffset(216)]
        public int Culled;
        [FieldOffset(220)]
        public int Flags;
        [FieldOffset(224)]
        public int TelemetryCursor;
        [FieldOffset(228)]
        public int LastJobMicrosecondsQ16;
        [FieldOffset(232)]
        public int QualityWeightQ8;
        [FieldOffset(236)]
        public int MathLodPressureQ8;
        [FieldOffset(240)]
        private byte _pad240;
        [FieldOffset(241)]
        private byte _pad241;
        [FieldOffset(242)]
        private byte _pad242;
        [FieldOffset(243)]
        private byte _pad243;
        [FieldOffset(244)]
        private byte _pad244;
        [FieldOffset(245)]
        private byte _pad245;
        [FieldOffset(246)]
        private byte _pad246;
        [FieldOffset(247)]
        private byte _pad247;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct WristHudFontGlyphDTO
    {
        [FieldOffset(0)]
        public float4 UVRect;
        [FieldOffset(16)]
        public float Advance;
        [FieldOffset(20)]
        public float BearingX;
        [FieldOffset(24)]
        public float BearingY;
        [FieldOffset(28)]
        public uint CharacterCode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WristHudTelemetryEntry
    {
        [FieldOffset(0)]
        public uint FrameIndex;
        [FieldOffset(4)]
        public uint StateHash;
        [FieldOffset(8)]
        public uint Flags;
        [FieldOffset(12)]
        public uint ActiveQuadCount;
        [FieldOffset(16)]
        public uint GlyphQuadCount;
        [FieldOffset(20)]
        public uint RadarCount;
        [FieldOffset(24)]
        public uint JobMicrosecondsQ16;
        [FieldOffset(28)]
        public uint TelemetryCursor;
        [FieldOffset(32)]
        public float Oxygen01;
        [FieldOffset(36)]
        public float DepthMeters;
        [FieldOffset(40)]
        public float SafeDepthMeters;
        [FieldOffset(44)]
        public float Radiation01;
        [FieldOffset(48)]
        public float Toxemia01;
        [FieldOffset(52)]
        public float AttentionDot;
        [FieldOffset(56)]
        public float HeadingDegrees;
        [FieldOffset(60)]
        public float PdaOpen01;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct WristHudBlackBoxDumpHeader
    {
        [FieldOffset(0)]
        public uint Magic;
        [FieldOffset(4)]
        public uint Version;
        [FieldOffset(8)]
        public uint FrameIndex;
        [FieldOffset(12)]
        public uint Flags;
        [FieldOffset(16)]
        public int TelemetryCapacity;
        [FieldOffset(20)]
        public int TelemetryCursor;
        [FieldOffset(24)]
        public int TelemetryEntrySizeBytes;
        [FieldOffset(28)]
        public int PayloadBytes;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct PlayerVitalsSignal
    {
        [FieldOffset(0)]
        public float Oxygen01;
        [FieldOffset(4)]
        public float Health01;
        [FieldOffset(8)]
        public float Power01;
        [FieldOffset(12)]
        public float DepthMeters;
        [FieldOffset(16)]
        public float SafeDepthMeters;
        [FieldOffset(20)]
        public float Radiation01;
        [FieldOffset(24)]
        public float Toxemia01;
        [FieldOffset(28)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public partial struct O2LevelChangedSignal
    {
        [FieldOffset(0)]
        public float Oxygen01;
        [FieldOffset(4)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct PdaOpenedSignal
    {
        [FieldOffset(0)]
        public uint IsOpen;
        [FieldOffset(4)]
        public int ActiveTab;
        [FieldOffset(8)]
        public uint Sequence;
        [FieldOffset(12)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct AcousticEchoTap
    {
        [FieldOffset(0)]
        public float3 RelativePositionMeters;
        [FieldOffset(12)]
        public float Amplitude01;
        [FieldOffset(16)]
        public uint StableId;
        [FieldOffset(20)]
        public float AgeSeconds;
        [FieldOffset(24)]
        public uint Flags;
        [FieldOffset(28)]
        private byte _pad28;
        [FieldOffset(29)]
        private byte _pad29;
        [FieldOffset(30)]
        private byte _pad30;
        [FieldOffset(31)]
        private byte _pad31;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Wrist Hologram HUD Runtime")]
    public sealed unsafe partial class WristHologramHudRuntime : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int StateCapacity = 1;
        private const int GlyphCapacity = 128;
        private const int TelemetryCapacity = 300;
        private const int CounterCapacity = 16;
        private const int MaxDefaultQuadCapacity = 512;
        private const int MaxDrawMeshInstancedBatch = 1023;
        private const int VitalsQueueCapacity = 64;
        private const int PdaQueueCapacity = 16;
        private const int DefaultAcousticCapacity = 128;
        private const int CsvOverrideMaxBytes = 8192;
        private const float AttentionDotThreshold = 0.70710678f;
        private const float JobWarningMicroseconds = 200f;
        private const float TransientHazardVitalsDecayPerSecond = 2.75f;
        private const float TransientHazardVitalsClearThreshold = 0.001f;
        private const string DefaultShaderName = "Hecton8/UI/WristHudSDF";
        private const string LegacyPaletteFileName = "ui_color_palettes.h8bin";
        private const string LegacyFontMetricsFileName = "font_atlas_metrics.bin";
        private const string CsvOverrideFileName = "font_metrics_override.csv";

        private const uint SpecialDepthBarCode = 0xFFFFFF01u;
        private const uint SpecialPdaGridCode = 0xFFFFFF02u;
        private const uint SpecialVignetteCode = 0xFFFFFF03u;
        private const uint SpecialRadarBlipCode = 0xFFFFFF04u;
        private const uint SpecialCompassCode = 0xFFFFFF05u;
        private const uint BlackBoxDumpMagic = 0x44554853u; // SHUD
        private const uint BlackBoxDumpVersion = 1u;
        private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;

        private const int StateFlagCulled = 1 << 0;
        private const int StateFlagPdaOpen = 1 << 1;
        private const int StateFlagJobOverBudget = 1 << 3;
        private const int StateFlagNaNDetected = 1 << 4;
        private const int StateFlagCsvLoaded = 1 << 5;
        private const int StateFlagLegacyMissing = 1 << 6;
        private const int StateFlagGpuUploadFault = 1 << 7;

        private static readonly int WristHudQuadsId = Shader.PropertyToID("_WristHudQuads");
        private static readonly int FontAtlasId = Shader.PropertyToID("_FontAtlas");
        private static readonly int BaseIntensityId = Shader.PropertyToID("_BaseIntensity");
        private static readonly int GlitchMultiplierId = Shader.PropertyToID("_GlitchMultiplier");
        [Header("Anchors")]
        [SerializeField] private Transform leftWristAnchor;
        [SerializeField] private Transform headsetAnchor;
        [SerializeField] private Camera renderCamera;

        [Header("Projection")]
        [SerializeField, Min(0.02f)] private float hologramDistanceFromWrist = 0.14f;
        [SerializeField, Min(0.002f)] private float textScale = 0.014f;
        [SerializeField, Range(0f, 6f)] private float glitchMultiplier = 1.15f;
        [SerializeField, Range(0.05f, 1f)] private float pdaGridCellSizeMeters = 0.045f;
        [SerializeField, Range(0.02f, 0.6f)] private float pdaGridDistanceMeters = 0.30f;
        [SerializeField, Min(64)] private int quadCapacity = MaxDefaultQuadCapacity;

        [Header("Palette")]
        [SerializeField] private Color lowColor = new Color(0.16f, 0.88f, 0.76f, 0.74f);
        [SerializeField] private Color midColor = new Color(0.42f, 0.96f, 0.92f, 0.88f);
        [SerializeField] private Color dangerColor = new Color(1.0f, 0.12f, 0.05f, 0.95f);

        [Header("Rendering")]
        [SerializeField] private Mesh quadMesh;
        [SerializeField] private Material sdfMaterial;
        [SerializeField] private Texture2D fontAtlasTexture;
        [SerializeField] private int renderLayer;
        [SerializeField, Range(0.1f, 8f)] private float baseIntensity = 1.65f;

        [Header("Runtime Inputs")]
        [SerializeField] private bool useDataVaultBuffers = true;
        [SerializeField] private bool enableMockSignals = true;
        [SerializeField] private bool enableCsvHotReload = true;
        [SerializeField, Range(0.05f, 2f)] private float csvPollIntervalSeconds = 0.5f;

        private IDataVault _vault;
        private IDataVault _cachedDataVault;
        private VaultGenerationHandle<WristHudStateDTO> _stateHandle;
        private VaultGenerationHandle<WristHudQuadTransformDTO> _quadHandle;
        private VaultGenerationHandle<WristHudFontGlyphDTO> _fontAtlasHandle;
        private VaultGenerationHandle<WristHudTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<uint> _counterHandle;
        private VaultGenerationHandle<AcousticEchoTap> _acousticTapHandle;
        private FixedList4096Bytes<PlayerVitalsSignal> _vitalsSignals;
        private FixedList512Bytes<PdaOpenedSignal> _pdaSignals;
        private int _vitalsQueueCount;
        private int _pdaQueueCount;

        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _registeredHotSwapListener;
        private bool _blackBoxDumpQueued;
        private bool _blackBoxDumped;
        private bool _csvLoaded;
        private bool _legacyMissing;
        private bool _fontAtlasGenerated;
        private bool _nativeResourcesDirty;
        private bool _hudTextJobDirty = true;
        private bool _materialColdStateDirty = true;
        private bool _wristHudGpuUploadFaulted;
        private int _lastUploadCount = -1;
        private int _lastUploadedFrameIndex = -1;
        private int _frontQuadCount;
        private int _quadBufferCapacity;
        private WristHudQuadTransformDTO[] _quadScratch = Array.Empty<WristHudQuadTransformDTO>();
        private readonly WristHudStateDTO[] _stateScratch = new WristHudStateDTO[StateCapacity];
        private readonly WristHudTelemetryEntry[] _telemetryScratch = new WristHudTelemetryEntry[TelemetryCapacity];
        private readonly uint[] _counterScratch = new uint[CounterCapacity];
        private readonly AcousticEchoTap[] _acousticTapScratch = new AcousticEchoTap[DefaultAcousticCapacity];
        private int _acousticTapScratchCount;
        private int _acousticTapWriteCursor;
        private float _lastHudDeltaTime;
        private float _cachedQualityWeight01 = 1f;
        private PlayerVitalsSignal _latestVitals;
        private PdaOpenedSignal _latestPdaSignal;
#if UNITY_EDITOR
        private readonly byte[] _csvReadBuffer = new byte[CsvOverrideMaxBytes]; // COLD ALLOC: byte[8192] - editor/manual font metrics CSV scratch - owner: WristHologramHudRuntime
#endif
        private FixedString64Bytes _o2Text;
        private FixedString64Bytes _depthText;
        private FixedString64Bytes _headingText;
        private FixedString64Bytes _loadText;
        private Matrix4x4[] _drawMatrices;
        private GraphicsBuffer _quadGpuBufferA;
        private GraphicsBuffer _quadGpuBufferB;
        private GraphicsBuffer _activeQuadGpuBuffer;
        private Material _runtimeMaterial;
        private MaterialPropertyBlock _materialProperties;
        private Mesh _runtimeQuadMesh;
        private bool _missingHudDrawAssetsAnnounced;
        private DateTime _lastCsvWriteUtc;
        private float _csvPollTimer;
        private float _globalSystemPressure01;
        private int _survivalPressureHoldFrames;
        private GameObject _playerToxicityTargetObject;
        private uint _playerToxicityTargetHash = PlayerToxicityFallbackEntityHash;
        private uint _playerSurvivalVitalsSourceId;
        private int _lastToxicityExposureSnapshotGeneration;
        private string _projectRoot;
        private string _csvOverridePath;

        public bool TryGetPdaGridGizmo(out Matrix4x4 matrix, out Vector3 size)
        {
            matrix = default;
            size = default;
            if (!TryReadWristHudState(0, out WristHudStateDTO state))
                return false;

            float cell = math.max(0.01f, state.PdaGridCenterAndCell.w);
            if (cell <= 0f)
                cell = pdaGridCellSizeMeters;

            matrix = Matrix4x4.TRS(
                new Vector3(state.PdaGridCenterAndCell.x, state.PdaGridCenterAndCell.y, state.PdaGridCenterAndCell.z),
                ResolveWristRotation(),
                Vector3.one);
            size = new Vector3(cell * 6f, cell * 4f, cell * 0.35f);
            return state.PdaGridQuadCount > 0 || (_latestPdaSignal.IsOpen != 0u);
        }

        public void InjectVitalsSignal(in PlayerVitalsSignal signal)
        {
            if (_vitalsSignals.Length < VitalsQueueCapacity)
            {
                _vitalsSignals.Add(signal);
                _vitalsQueueCount = _vitalsSignals.Length;
            }
        }

        public void InjectO2Signal(in O2LevelChangedSignal signal)
        {
            PlayerVitalsSignal vitals = _latestVitals;
            vitals.Oxygen01 = FiniteSaturate(signal.Oxygen01);
            vitals.Flags |= signal.Flags;
            InjectVitalsSignal(in vitals);
        }

        public void InjectPdaOpenedSignal(in PdaOpenedSignal signal)
        {
            if (_pdaSignals.Length < PdaQueueCapacity)
            {
                _pdaSignals.Add(signal);
                _pdaQueueCount = _pdaSignals.Length;
            }
        }

        public void InjectAcousticEchoTap(in AcousticEchoTap tap)
        {
            StoreAcousticTapScratch(in tap);

            if (EnsureNativeBuffers())
                FlushAcousticTapScratchToVault();
        }

        private void StoreAcousticTapScratch(in AcousticEchoTap tap)
        {
            int capacity = _acousticTapScratch.Length;
            if (capacity <= 0)
                return;

            int index = _acousticTapWriteCursor;
            if ((uint)index >= (uint)capacity)
                index = 0;

            _acousticTapScratch[index] = tap;
            index++;
            if (index >= capacity)
                index = 0;

            _acousticTapWriteCursor = index;
            if (_acousticTapScratchCount < capacity)
                _acousticTapScratchCount++;

            _counterScratch[2] = (uint)_acousticTapScratchCount;
        }

        public void ApplyTunerSettings(float distance, float scale, float glitch, Color low, Color mid, Color danger)
        {
            hologramDistanceFromWrist = math.max(0.02f, distance);
            textScale = math.max(0.002f, scale);
            glitchMultiplier = math.max(0f, glitch);
            lowColor = low;
            midColor = mid;
            dangerColor = danger;

            if (EnsureNativeBuffers())
            {
                if (TryAcquireWristHudVaultBuffer(in _stateHandle, BufferID.WristHudState, StateCapacity, out IDataVault stateWriteVault, out NativeArray<WristHudStateDTO> states))
                {
                    try
                    {
                        WristHudStateDTO state = states[0];
                        state.WristPositionAndDistance.w = hologramDistanceFromWrist;
                        state.LowColor = ToFloat4(lowColor);
                        state.MidColor = ToFloat4(midColor);
                        state.DangerColor = ToFloat4(dangerColor);
                        state.CompassAndVitals.w = glitchMultiplier;
                        states[0] = state;
                    }
                    finally
                    {
                        ReleaseWristHudVaultWriteBuffer(stateWriteVault, in _stateHandle, BufferID.WristHudState);
                    }
                }
            }

            _materialColdStateDirty = true;
        }

#if UNITY_EDITOR
        public bool TryReloadFontMetricsOverride()
        {
            if (!EnsureNativeBuffers())
                return false;

            string path = GetCsvOverridePath();
            bool loaded = TryParseFontMetricsCsv(path);
            if (loaded)
            {
                _csvLoaded = true;
                _lastCsvWriteUtc = File.GetLastWriteTimeUtc(path);
                if (TryAcquireWristHudVaultBuffer(in _stateHandle, BufferID.WristHudState, StateCapacity, out IDataVault stateWriteVault, out NativeArray<WristHudStateDTO> states))
                {
                    try
                    {
                        WristHudStateDTO state = states[0];
                        state.Flags |= StateFlagCsvLoaded;
                        states[0] = state;
                    }
                    finally
                    {
                        ReleaseWristHudVaultWriteBuffer(stateWriteVault, in _stateHandle, BufferID.WristHudState);
                    }
                }
            }

            return loaded;
        }
#endif

        private void OnEnable()
        {
            RefreshCachedRegistryServices();
            _lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
            TryRegisterHotSwapListener();
            ColdSanityCheckLayout();
            EnsureNativeBuffers();
            EnsureSignalBuffers();
            EnsureGraphicsResources();
            SeedInitialState();
            PdaProjectorOnEnable();
            TryRegisterTickLanes();
        }

        private void Start()
        {
            RefreshCachedRegistryServices();
            EnsureNativeBuffers();
            EnsureSignalBuffers();
            EnsureGraphicsResources();
            TryRegisterTickLanes();
        }

        private void OnDisable()
        {
            TryUnregisterTickLanes();
            TryUnregisterHotSwapListener();
            _frontQuadCount = 0;
            _lastUploadCount = -1;
            _lastUploadedFrameIndex = -1;
            _playerSurvivalVitalsSourceId = 0u;
            PdaProjectorOnDisable();
        }

        private void OnDestroy()
        {
            TryUnregisterTickLanes();
            TryUnregisterHotSwapListener();
            FlushQueuedBlackBoxDump();
            PdaProjectorOnDestroy();
            ReleaseGraphicsResources();
            DisposeNativeState();
            ClearSignalBuffers();
        }

        private void AdvanceHudFrameState(float deltaTime)
        {
            if (!HasRequiredVaultHandles())
            {
                _nativeResourcesDirty = true;
                return;
            }

            if (!HasSignalBuffers())
            {
                _nativeResourcesDirty = true;
                return;
            }

            if (!HasRequiredVaultHandles())
                return;

            DrainSignalQueues(deltaTime);
            PdaProjectorTick(deltaTime);
            RefreshUiStateStoreInputs();
            _lastHudDeltaTime = math.max(0f, deltaTime);
            _hudTextJobDirty = true;
        }

        public void LateFrameTick()
        {
            AdvanceHudFrameState(math.max(0f, SystemDispatcher.CurrentFrameDeltaTime));

            if (_nativeResourcesDirty || !HasRequiredVaultHandles() || !HasSignalBuffers())
            {
                _nativeResourcesDirty = false;
                return;
            }

            if (_materialColdStateDirty && _runtimeMaterial != null)
            {
                _materialColdStateDirty = false;
                ApplyMaterialColdState();
            }

            if (_hudTextJobDirty)
            {
                _hudTextJobDirty = false;
                BuildFixedTexts();
                BuildTextQuadsOwnerPhase(_lastHudDeltaTime);
            }

            UploadAndDraw();
            PdaProjectorLateFrameTick();
        }

        public void SlowTick()
        {
#if UNITY_EDITOR
            PollCsvOverride(_lastHudDeltaTime);
#endif
            FlushQueuedBlackBoxDump();
        }

        private bool EnsureNativeBuffers()
        {
            if (HasRequiredVaultHandles())
                return true;

            if (!useDataVaultBuffers)
            {
                ReleaseNativeStateHandles();
                return false;
            }

            IDataVault vault = _cachedDataVault;
            if (vault == null)
            {
                ReleaseNativeStateHandles();
                return false;
            }

            if (!ReferenceEquals(_vault, vault))
            {
                ReleaseNativeStateHandles();
                _vault = vault;
            }

            int safeQuadCapacity = math.clamp(quadCapacity, 64, MaxDrawMeshInstancedBatch);
            _stateHandle = vault.EnsureGenerationHandle<WristHudStateDTO>(BufferID.WristHudState, StateCapacity, SystemID.UI, NativeArrayOptions.ClearMemory);
            _quadHandle = vault.EnsureGenerationHandle<WristHudQuadTransformDTO>(BufferID.WristHudQuads, safeQuadCapacity, SystemID.UI, NativeArrayOptions.ClearMemory);
            _fontAtlasHandle = vault.EnsureGenerationHandle<WristHudFontGlyphDTO>(BufferID.WristHudFontAtlas, GlyphCapacity, SystemID.UI, NativeArrayOptions.ClearMemory);
            _telemetryHandle = vault.EnsureGenerationHandle<WristHudTelemetryEntry>(BufferID.WristHudTelemetryRing, TelemetryCapacity, SystemID.UI, NativeArrayOptions.ClearMemory);
            _counterHandle = vault.EnsureGenerationHandle<uint>(BufferID.WristHudCounters, CounterCapacity, SystemID.UI, NativeArrayOptions.ClearMemory);
            _acousticTapHandle = vault.EnsureGenerationHandle<AcousticEchoTap>(BufferID.WristHudAcousticTaps, DefaultAcousticCapacity, SystemID.UI, NativeArrayOptions.ClearMemory);

            if (!TryReadOnlyQuadBuffer(out NativeArray<WristHudQuadTransformDTO>.ReadOnly quads))
                return false;

            _quadBufferCapacity = quads.Length;
            if (_quadScratch.Length != _quadBufferCapacity)
                _quadScratch = new WristHudQuadTransformDTO[_quadBufferCapacity]; // COLD ALLOC: WristHudQuadTransformDTO[quadCapacity] - owner-phase scratch before one-lock DataVault publish - owner: WristHologramHudRuntime

            if (!_fontAtlasGenerated)
            {
                GenerateMockFontAtlas();
#if UNITY_EDITOR
                TryLoadLegacyFontMetrics();
                TryReloadFontMetricsOverride();
#endif
            }

            if (_drawMatrices == null || _drawMatrices.Length != _quadBufferCapacity)
                _drawMatrices = new Matrix4x4[_quadBufferCapacity]; // COLD ALLOC: Matrix4x4[quadCapacity] - DrawMeshInstanced retained payload - owner: WristHologramHudRuntime

            return true;
        }

        private void EnsureSignalBuffers()
        {
            if (_vitalsSignals.Length > VitalsQueueCapacity)
                _vitalsSignals.Length = VitalsQueueCapacity;
            if (_pdaSignals.Length > PdaQueueCapacity)
                _pdaSignals.Length = PdaQueueCapacity;

            _vitalsQueueCount = _vitalsSignals.Length;
            _pdaQueueCount = _pdaSignals.Length;
        }

        private bool HasSignalBuffers()
        {
            return _vitalsSignals.Capacity >= VitalsQueueCapacity &&
                   _pdaSignals.Capacity >= PdaQueueCapacity;
        }

        private void DisposeNativeState()
        {
            ReleaseNativeStateHandles();
            _fontAtlasGenerated = false;
        }

        private void ReleaseNativeStateHandles()
        {
            IDataVault vault = _vault;
            ReleaseWristHudVaultHandle(vault, ref _stateHandle, BufferID.WristHudState);
            ReleaseWristHudVaultHandle(vault, ref _quadHandle, BufferID.WristHudQuads);
            ReleaseWristHudVaultHandle(vault, ref _fontAtlasHandle, BufferID.WristHudFontAtlas);
            ReleaseWristHudVaultHandle(vault, ref _telemetryHandle, BufferID.WristHudTelemetryRing);
            ReleaseWristHudVaultHandle(vault, ref _counterHandle, BufferID.WristHudCounters);
            ReleaseWristHudVaultHandle(vault, ref _acousticTapHandle, BufferID.WristHudAcousticTaps);
            _quadBufferCapacity = 0;
            _vault = null;
        }

        private static void ReleaseWristHudVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : unmanaged
        {
            if (vault != null && IsExactVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearSignalBuffers()
        {
            _vitalsSignals.Clear();
            _pdaSignals.Clear();
            _vitalsQueueCount = 0;
            _pdaQueueCount = 0;
            _playerSurvivalVitalsSourceId = 0u;
        }

        private bool HasRequiredVaultHandles()
        {
            return _vault != null &&
                   IsExactVaultHandle(in _stateHandle, BufferID.WristHudState) &&
                   IsExactVaultHandle(in _quadHandle, BufferID.WristHudQuads) &&
                   IsExactVaultHandle(in _fontAtlasHandle, BufferID.WristHudFontAtlas) &&
                   IsExactVaultHandle(in _telemetryHandle, BufferID.WristHudTelemetryRing) &&
                   IsExactVaultHandle(in _counterHandle, BufferID.WristHudCounters) &&
                   IsExactVaultHandle(in _acousticTapHandle, BufferID.WristHudAcousticTaps);
        }

        private bool TryReadWristHudState(int index, out WristHudStateDTO state)
        {
            state = default;
            if ((uint)index >= StateCapacity ||
                !TryReadOnlyWristHudVaultBuffer(in _stateHandle, BufferID.WristHudState, StateCapacity, out NativeArray<WristHudStateDTO>.ReadOnly states))
            {
                return false;
            }

            state = states[index];
            return true;
        }

        private bool TryReadOnlyQuadBuffer(out NativeArray<WristHudQuadTransformDTO>.ReadOnly quads)
        {
            return TryReadOnlyWristHudVaultBuffer(in _quadHandle, BufferID.WristHudQuads, 1, out quads);
        }

        private bool TryAcquireWristHudVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out IDataVault writeVault,
            out NativeArray<T> buffer) where T : unmanaged
        {
            writeVault = null;
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsExactVaultHandle(in handle, expectedBufferId) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.UI, out buffer))
            {
                buffer = default;
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (!vault.IsCompactionFenceActive &&
                    buffer.IsCreated &&
                    buffer.Length >= requiredLength)
                {
                    releaseOnExit = false;
                    writeVault = vault;
                    return true;
                }

                return false;
            }
            finally
            {
                if (releaseOnExit)
                {
                    vault.ReleaseWriteLock(in handle, SystemID.UI);
                    buffer = default;
                }
            }
        }

        private static void ReleaseWristHudVaultWriteBuffer<T>(
            IDataVault writeVault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : unmanaged
        {
            if (writeVault != null && IsExactVaultHandle(in handle, expectedBufferId))
                writeVault.ReleaseWriteLock(in handle, SystemID.UI);
        }

        private bool TryReadOnlyWristHudVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : unmanaged
        {
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !IsExactVaultHandle(in handle, expectedBufferId) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                buffer.Length < requiredLength ||
                vault.IsCompactionFenceActive)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)SystemID.UI &&
                   handle.Generation != 0u;
        }

        /// <summary>
        /// Builds the quad GPU buffers and resolves the authored draw pair, or reports the gap once without
        /// throwing.
        /// </summary>
        /// <remarks>
        /// The two <c>UnityEngine.Assertions.Assert</c> calls that used to sit at the TOP of this method THREW -
        /// nothing under Assets sets <c>Assert.raiseExceptions = false</c> - and they were positioned so that an
        /// unassigned inspector slot destroyed far more than the draw. Everything below them in this method was
        /// unreachable: the two quad <c>GraphicsBuffer</c>s were never created, <c>_activeQuadGpuBuffer</c> stayed
        /// null, and the <c>MaterialPropertyBlock</c> was never allocated.
        ///
        /// It also escaped all three callers mid-sequence. <see cref="OnEnable"/> (:544) never reached
        /// <c>SeedInitialState()</c>, <c>PdaProjectorOnEnable()</c> or <c>TryRegisterTickLanes()</c> (:545-547),
        /// so the wrist HUD never joined a tick lane and could not recover once an asset was assigned;
        /// <see cref="Start"/> (:555) never reached its own <c>TryRegisterTickLanes()</c> (:556); and the
        /// <c>GlobalRegistry</c> hot-swap callback at :2416 threw inside a shared notification loop on every
        /// DataVault rebind.
        ///
        /// The asserts guarded nothing. Leaving <c>_runtimeQuadMesh</c>/<c>_runtimeMaterial</c> null is the
        /// designed degradation path and it is already handled: the draw site null-checks both at :1479-1480 and
        /// <see cref="ApplyMaterialColdState"/> returns on a null material (:894).
        /// </remarks>
        private void EnsureGraphicsResources()
        {
            bool meshAssigned = quadMesh != null;
            bool materialAssigned = sdfMaterial != null;
            bool authoredMeshValid = meshAssigned && quadMesh.subMeshCount > 0 && quadMesh.GetIndexCount(0) > 0u;
            bool authoredMaterialValid = materialAssigned &&
                                         sdfMaterial.shader != null &&
                                         sdfMaterial.enableInstancing;
            _runtimeQuadMesh = authoredMeshValid && authoredMaterialValid ? quadMesh : null;
            _runtimeMaterial = authoredMeshValid && authoredMaterialValid ? sdfMaterial : null;

            int quadCount = math.max(1, _quadBufferCapacity > 0 ? _quadBufferCapacity : math.clamp(quadCapacity, 64, MaxDrawMeshInstancedBatch));
            EnsureGraphicsBuffer(ref _quadGpuBufferA, quadCount);
            EnsureGraphicsBuffer(ref _quadGpuBufferB, quadCount);
            if (_activeQuadGpuBuffer == null || !_activeQuadGpuBuffer.IsValid())
                _activeQuadGpuBuffer = _quadGpuBufferA;

            if (_materialProperties == null)
                _materialProperties = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - instanced UI shader payload - owner: WristHologramHudRuntime

            ApplyMaterialColdState();

            // Report LAST and once. Every buffer this component owns is built above and each caller continues to
            // its own registration work after this returns, so a future re-introduced throw here can no longer
            // strand the tick lanes or the GPU buffers.
            if ((authoredMeshValid && authoredMaterialValid) || _missingHudDrawAssetsAnnounced)
                return;

            _missingHudDrawAssetsAnnounced = true;
            LogInvalidWristHudDrawAssets(
                meshAssigned,
                authoredMeshValid,
                materialAssigned,
                sdfMaterial != null && sdfMaterial.shader != null,
                authoredMaterialValid);
        }

        /// <summary>
        /// One-shot report of an unusable authored wrist-HUD draw pair. The latch guarantees single emission and
        /// every parameter is a primitive, so no string work and no allocation reaches a tick cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidWristHudDrawAssets(
            bool meshAssigned,
            bool authoredMeshValid,
            bool materialAssigned,
            bool materialHasShader,
            bool authoredMaterialValid)
        {
            if (!meshAssigned)
            {
                Hecton8.Core.H8Debug.LogError("WristHologramHudRuntime: serialized field 'quadMesh' is unassigned. The entire wrist hologram HUD renders nothing this session - no vitals, no O2, no toxicity readout - because the instanced quad draw is null-guarded and skipped. The quad GPU buffers, native buffers and every tick lane stay live. Assign the authored HUD quad mesh in the inspector.");
            }
            else if (!authoredMeshValid)
            {
                Hecton8.Core.H8Debug.LogError("WristHologramHudRuntime: the mesh assigned to 'quadMesh' has no indexed submesh 0 (subMeshCount is 0 or GetIndexCount(0) is 0), so the instanced HUD draw would submit no triangles. The entire wrist hologram HUD renders nothing this session. Reimport or replace that mesh asset with one that carries an index buffer.");
            }

            if (!materialAssigned)
            {
                Hecton8.Core.H8Debug.LogError("WristHologramHudRuntime: serialized field 'sdfMaterial' is unassigned. The entire wrist hologram HUD renders nothing this session - no vitals, no O2, no toxicity readout. Every tick lane and GPU buffer stays live. Runtime material generation is forbidden: assign the authored SDF glyph material in the inspector.");
                return;
            }

            if (!materialHasShader)
            {
                Hecton8.Core.H8Debug.LogError("WristHologramHudRuntime: the material assigned to 'sdfMaterial' has a null shader - the shader asset it referenced is missing or failed to compile. The entire wrist hologram HUD renders nothing this session. Repair that material's shader reference.");
                return;
            }

            if (!authoredMaterialValid)
            {
                Hecton8.Core.H8Debug.LogError("WristHologramHudRuntime: the material assigned to 'sdfMaterial' has Enable GPU Instancing OFF, which the instanced SDF glyph draw requires. The entire wrist hologram HUD renders nothing this session. Tick 'Enable GPU Instancing' on that material asset.");
            }
        }

        private void ReleaseGraphicsResources()
        {
            ReleaseGraphicsBuffer(ref _quadGpuBufferA);
            ReleaseGraphicsBuffer(ref _quadGpuBufferB);
            _activeQuadGpuBuffer = null;
            _runtimeMaterial = null;
            _runtimeQuadMesh = null;
        }

        private void ApplyMaterialColdState()
        {
            if (_runtimeMaterial == null)
                return;

            if (_materialProperties == null)
                return;

            _materialProperties.Clear();
            if (_activeQuadGpuBuffer != null && _activeQuadGpuBuffer.IsValid())
                _materialProperties.SetBuffer(WristHudQuadsId, _activeQuadGpuBuffer);
            if (fontAtlasTexture != null)
                _materialProperties.SetTexture(FontAtlasId, fontAtlasTexture);
            _materialProperties.SetFloat(BaseIntensityId, baseIntensity);
            _materialProperties.SetFloat(GlitchMultiplierId, glitchMultiplier);
        }

        private void TryRegisterTickLanes()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);

            if (!_registeredSlowTick)
                _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        private void TryUnregisterTickLanes()
        {
            if (_registeredLateFrame)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }

            if (_registeredSlowTick)
            {
                SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
                _registeredSlowTick = false;
            }
        }

        private void SeedInitialState()
        {
            if (!EnsureNativeBuffers())
                return;

            if (TryAcquireWristHudVaultBuffer(in _stateHandle, BufferID.WristHudState, StateCapacity, out IDataVault stateWriteVault, out NativeArray<WristHudStateDTO> states))
            {
                try
                {
                    WristHudStateDTO state = states[0];
                    state.WristPositionAndDistance.w = hologramDistanceFromWrist;
                    state.LowColor = ToFloat4(lowColor);
                    state.MidColor = ToFloat4(midColor);
                    state.DangerColor = ToFloat4(dangerColor);
                    state.PdaGridCenterAndCell.w = pdaGridCellSizeMeters;
                    state.CompassAndVitals.w = glitchMultiplier;
                    state.QualityWeightQ8 = EncodeQualityWeightQ8(_cachedQualityWeight01);
                    state.MathLodPressureQ8 = EncodeUnitWeightQ8(ResolveMathLodPressure01(), 0f);
                    if (_legacyMissing)
                        state.Flags |= StateFlagLegacyMissing;
                    if (_csvLoaded)
                        state.Flags |= StateFlagCsvLoaded;
                    states[0] = state;
                }
                finally
                {
                    ReleaseWristHudVaultWriteBuffer(stateWriteVault, in _stateHandle, BufferID.WristHudState);
                }
            }

            _latestVitals.Oxygen01 = 1f;
            _latestVitals.Health01 = 1f;
            _latestVitals.Power01 = 1f;
            _latestVitals.SafeDepthMeters = 200f;
        }

        private void DrainSignalQueues(float deltaTime)
        {
            float previousMathLodPressure = ResolveMathLodPressure01();
            RefreshQualityPolicy();
            DecayTransientHazardVitals(deltaTime);
            RefreshPlayerToxicityTargetHash(GlobalRegistry.Player);
            RefreshPlayerSurvivalVitalsSourceId(GlobalRegistry.Player);
            DrainGlobalSignalSnapshots();

            if (enableMockSignals)
                RunMockSignalInjector(deltaTime);

            if (_survivalPressureHoldFrames > 0)
                _survivalPressureHoldFrames--;
            if (math.abs(previousMathLodPressure - ResolveMathLodPressure01()) > 0.02f)
                _materialColdStateDirty = true;

            int vitalsCount = math.min(_vitalsSignals.Length, VitalsQueueCapacity);
            for (int i = 0; i < vitalsCount; i++)
            {
                _latestVitals = SanitizeVitals(_vitalsSignals[i]);
            }

            _vitalsSignals.Clear();
            _vitalsQueueCount = 0;

            int pdaCount = math.min(_pdaSignals.Length, PdaQueueCapacity);
            for (int i = 0; i < pdaCount; i++)
            {
                _latestPdaSignal = _pdaSignals[i];
            }

            _pdaSignals.Clear();
            _pdaQueueCount = 0;
        }

        private void DecayTransientHazardVitals(float deltaTime)
        {
            float safeDelta = math.max(0f, math.isfinite(deltaTime) ? deltaTime : 0f);
            float decay01 = math.saturate(1f - safeDelta * TransientHazardVitalsDecayPerSecond);
            _latestVitals.Radiation01 = DecayTransientHazardVital01(_latestVitals.Radiation01, decay01);
            _latestVitals.Toxemia01 = DecayTransientHazardVital01(_latestVitals.Toxemia01, decay01);
        }

        private static float DecayTransientHazardVital01(float value, float decay01)
        {
            float next = FiniteSaturate(value) * math.saturate(decay01);
            return next <= TransientHazardVitalsClearThreshold ? 0f : next;
        }

        private void DrainGlobalSignalSnapshots()
        {
            uint survivalVitalsSourceId = _playerSurvivalVitalsSourceId;
            if (survivalVitalsSourceId != 0u)
            {
                ReadOnlySpan<SurvivalVitalsChangedSignal> vitals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot();
                for (int i = 0; i < vitals.Length; i++)
                {
                    SurvivalVitalsChangedSignal signal = vitals[i];
                    if (signal.SourceId != survivalVitalsSourceId)
                        continue;

                    if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Oxygen) != 0u)
                        _latestVitals.Oxygen01 = FiniteSaturate(signal.Oxygen01);
                    if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Energy) != 0u)
                        _latestVitals.Power01 = FiniteSaturate(signal.Energy01);
                    if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Integrity) != 0u)
                        _latestVitals.Health01 = FiniteSaturate(signal.Integrity01);
                }
            }

            ReadOnlySpan<RadiationDoseSignal> doses = SignalBus<RadiationDoseSignal>.GetFrameSnapshot();
            for (int i = 0; i < doses.Length; i++)
            {
                RadiationDoseSignal signal = doses[i];
                float radiationIntensity01 = FiniteSaturate(signal.Intensity01);
                float radiationDoseToxemia01 = RadiationDoseSignal.DoseToUnit01(signal.Dose);
                _latestVitals.Radiation01 = math.max(_latestVitals.Radiation01, radiationIntensity01);
                _latestVitals.Toxemia01 = math.max(_latestVitals.Toxemia01, radiationDoseToxemia01);
            }

            int toxicitySnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
            if (toxicitySnapshotGeneration != _lastToxicityExposureSnapshotGeneration)
            {
                _lastToxicityExposureSnapshotGeneration = toxicitySnapshotGeneration;
                ReadOnlySpan<ToxicityExposureSignal> toxicities = SignalBus<ToxicityExposureSignal>.GetFrameSnapshot();
                uint playerToxicityTargetHash = _playerToxicityTargetHash != 0u ? _playerToxicityTargetHash : PlayerToxicityFallbackEntityHash;
                for (int i = 0; i < toxicities.Length; i++)
                {
                    ToxicityExposureSignal signal = toxicities[i];
                    if (signal.EntityId == 0u)
                        continue;
                    if (signal.EntityId != playerToxicityTargetHash && signal.EntityId != PlayerToxicityFallbackEntityHash)
                        continue;

                    float exposure01 = FiniteSaturate(signal.Exposure01);
                    float toxemiaDelta01 = FiniteSaturate(signal.ToxemiaDelta);
                    _latestVitals.Toxemia01 = math.max(_latestVitals.Toxemia01, math.max(exposure01, toxemiaDelta01));
                }
            }

            ReadOnlySpan<SystemHealthIndexSignal> health = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = 0; i < health.Length; i++)
            {
                SystemHealthIndexSignal signal = health[i];
                _globalSystemPressure01 = math.saturate(signal.Pressure01);
                if (signal.State >= SystemHealthIndexSignal.StateCritical || _globalSystemPressure01 >= 0.8f)
                    _survivalPressureHoldFrames = math.max(_survivalPressureHoldFrames, 300);
            }

            ReadOnlySpan<PdaExchangeStateChangedSignal> pdaChanges = SignalBus<PdaExchangeStateChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < pdaChanges.Length; i++)
            {
                PdaExchangeStateChangedSignal signal = pdaChanges[i];
                _latestPdaSignal.Flags |= (uint)signal.Flags;
            }
        }

        private void RunMockSignalInjector(float deltaTime)
        {
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            PlayerVitalsSignal vitals = BuildMockVitalsSignal(_latestVitals, math.max(0f, deltaTime), now);
            InjectVitalsSignal(in vitals);

            if ((Hecton8.Core.SystemDispatcher.CurrentFrameIndex & 127) == 0)
            {
                PdaOpenedSignal pda = _latestPdaSignal;
                pda.IsOpen = pda.IsOpen == 0u ? 1u : 0u;
                pda.Sequence++;
                InjectPdaOpenedSignal(in pda);
            }

            GenerateMockAcousticTaps();
        }

        private static PlayerVitalsSignal BuildMockVitalsSignal(in PlayerVitalsSignal previous, float deltaTime, float timeSeconds)
        {
            float pulse = HudTriangle01(timeSeconds * 0.1114f);
            PlayerVitalsSignal next = previous;
            next.Oxygen01 = math.saturate(math.lerp(previous.Oxygen01 <= 0f ? 1f : previous.Oxygen01, 0.12f + pulse * 0.88f, deltaTime * 0.05f));
            next.Health01 = math.saturate(0.62f + HudTriangle01(timeSeconds * 0.0302f) * 0.36f);
            next.Power01 = math.saturate(0.45f + HudTriangle01(timeSeconds * 0.0175f) * 0.5f);
            next.DepthMeters = math.max(0f, 120f + TriangleWave(timeSeconds * 0.0207f) * 145f);
            next.SafeDepthMeters = 220f;
            next.Radiation01 = HudTriangle01(timeSeconds * 0.0366f);
            next.Toxemia01 = HudTriangle01(timeSeconds * 0.0271f + 0.21f);
            next.Flags = 1u;
            return next;
        }

        private static float HudTriangle01(float phase)
        {
            return math.abs(math.frac(phase) * 2f - 1f);
        }

        private static float TriangleWave(float phase)
        {
            return HudTriangle01(phase) * 2f - 1f;
        }

        private void GenerateMockAcousticTaps()
        {
            int max = ResolveMockAcousticTapCapacity();
            int count = math.min(max, _acousticTapScratch.Length);
            float t = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            for (int i = 0; i < count; i++)
            {
                float phase = t * (0.0477f + i * 0.0027f) + i * 0.6180339f;
                float radius = 8f + ((i * 13) % 27);
                _acousticTapScratch[i] = new AcousticEchoTap
                {
                    RelativePositionMeters = new float3(
                        TriangleWaveSigned(phase) * radius,
                        TriangleWaveSigned(phase * 0.37f + 0.11f) * 3f,
                        TriangleWaveSigned(phase + 0.25f) * radius),
                    Amplitude01 = math.saturate(0.25f + HudTriangle01(phase * 2.17f) * 0.45f),
                    StableId = (uint)(0xA700u + i),
                    AgeSeconds = math.frac(t + i * 0.13f),
                    Flags = 1u
                };
            }

            _acousticTapScratchCount = count;
            _acousticTapWriteCursor = count >= _acousticTapScratch.Length ? 0 : count;
            _counterScratch[2] = (uint)count;
            FlushAcousticTapScratchToVault();
        }

        private bool FlushAcousticTapScratchToVault()
        {
            int count = math.min(_acousticTapScratchCount, _acousticTapScratch.Length);
            if (count <= 0)
                return true;
            if (!TryAcquireWristHudVaultBuffer(in _acousticTapHandle, BufferID.WristHudAcousticTaps, 1, out IDataVault acousticWriteVault, out NativeArray<AcousticEchoTap> acousticTaps))
                return false;

            try
            {
                int writeCount = math.min(count, acousticTaps.Length);
                for (int i = 0; i < writeCount; i++)
                    acousticTaps[i] = _acousticTapScratch[i];
                return true;
            }
            finally
            {
                ReleaseWristHudVaultWriteBuffer(acousticWriteVault, in _acousticTapHandle, BufferID.WristHudAcousticTaps);
            }
        }

        private static float TriangleWaveSigned(float phase)
        {
            return HudTriangle01(phase) * 2f - 1f;
        }

        private void RefreshUiStateStoreInputs()
        {
            float oxygen = UIStateStore.ReadValueOrDefault(UIValueSlotId.Oxygen01, _latestVitals.Oxygen01);
            float health = UIStateStore.ReadValueOrDefault(UIValueSlotId.Health01, _latestVitals.Health01);
            float power = UIStateStore.ReadValueOrDefault(UIValueSlotId.Power01, _latestVitals.Power01);
            float depth = UIStateStore.ReadValueOrDefault(UIValueSlotId.DepthMeters, _latestVitals.DepthMeters);
            float safeDepth = UIStateStore.ReadValueOrDefault(UIValueSlotId.SafeDepthMeters, _latestVitals.SafeDepthMeters <= 0f ? 200f : _latestVitals.SafeDepthMeters);
            _latestVitals.Oxygen01 = FiniteSaturate(oxygen);
            _latestVitals.Health01 = FiniteSaturate(health);
            _latestVitals.Power01 = FiniteSaturate(power);
            _latestVitals.DepthMeters = FiniteNonNegative(depth);
            _latestVitals.SafeDepthMeters = math.max(1f, FiniteNonNegative(safeDepth));

            UIStateData pda = UIStateStore.GetPDAState();
            if (pda.Version != 0u)
            {
                _latestPdaSignal.IsOpen = (pda.Flags & (ushort)UIStateFlags.PDAOpen) != 0 ? 1u : 0u;
                _latestPdaSignal.ActiveTab = pda.ActiveTab;
            }
        }

#if UNITY_EDITOR
        private void PollCsvOverride(float deltaTime)
        {
            if (!enableCsvHotReload)
                return;

            _csvPollTimer += math.max(0f, deltaTime);
            if (_csvPollTimer < csvPollIntervalSeconds)
                return;

            _csvPollTimer = 0f;
            string path = GetCsvOverridePath();
            if (!File.Exists(path))
                return;

            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (writeUtc == _lastCsvWriteUtc)
                return;

            TryReloadFontMetricsOverride();
        }
#endif

        private void BuildFixedTexts()
        {
            ClearFixedString(ref _o2Text);
            AppendAscii(ref _o2Text, 'O');
            AppendAscii(ref _o2Text, '2');
            AppendAscii(ref _o2Text, ':');
            AppendAscii(ref _o2Text, ' ');
            AppendUInt(ref _o2Text, (uint)math.round(math.saturate(_latestVitals.Oxygen01) * 100f));
            AppendAscii(ref _o2Text, '%');

            ClearFixedString(ref _depthText);
            AppendAscii(ref _depthText, 'D');
            AppendAscii(ref _depthText, 'E');
            AppendAscii(ref _depthText, 'P');
            AppendAscii(ref _depthText, ':');
            AppendAscii(ref _depthText, ' ');
            AppendUInt(ref _depthText, (uint)math.round(math.max(0f, _latestVitals.DepthMeters)));
            AppendAscii(ref _depthText, 'm');

            float heading = ResolveHeadingDegrees();
            ClearFixedString(ref _headingText);
            AppendAscii(ref _headingText, 'H');
            AppendAscii(ref _headingText, 'D');
            AppendAscii(ref _headingText, 'G');
            AppendAscii(ref _headingText, ':');
            AppendAscii(ref _headingText, ' ');
            AppendUInt(ref _headingText, (uint)math.round(heading));

            float load01 = UIStateStore.ReadValueOrDefault(UIValueSlotId.InventoryLoad01, 0f);
            ClearFixedString(ref _loadText);
            AppendAscii(ref _loadText, 'L');
            AppendAscii(ref _loadText, 'D');
            AppendAscii(ref _loadText, ':');
            AppendAscii(ref _loadText, ' ');
            AppendUInt(ref _loadText, (uint)math.round(math.saturate(load01) * 100f));
            AppendAscii(ref _loadText, '%');
        }

        private void BuildTextQuadsOwnerPhase(float deltaTime)
        {
            if (!HasRequiredVaultHandles() ||
                _quadScratch.Length <= 0 ||
                !TryReadOnlyWristHudVaultBuffer(in _fontAtlasHandle, BufferID.WristHudFontAtlas, GlyphCapacity, out NativeArray<WristHudFontGlyphDTO>.ReadOnly fontAtlas))
            {
                return;
            }

            bool dumpBlackBox = false;
            Transform wrist = leftWristAnchor != null ? leftWristAnchor : transform;
            Transform head = ResolveHeadsetTransform();
            Quaternion wristRotation = wrist.rotation;
            Quaternion headRotation = head.rotation;
            Vector3 wristPosition = wrist.position;
            Vector3 headPosition = head.position;

            float3 headForward = (float3)(headRotation * Vector3.forward);
            float3 headUp = (float3)(headRotation * Vector3.up);
            float3 wristNormal = (float3)(wristRotation * Vector3.back);
            float3 wristRight = (float3)(wristRotation * Vector3.right);
            float3 wristUp = (float3)(wristRotation * Vector3.up);
            float3 wristForward = (float3)(wristRotation * Vector3.forward);
            float3 visorPosition = (float3)headPosition + headForward * 0.35f;
            int acousticTapCount = math.min(_acousticTapScratchCount, _acousticTapScratch.Length);

            TextToQuadsBuilder builder = new TextToQuadsBuilder
            {
                States = _stateScratch.AsSpan(0, StateCapacity),
                Quads = _quadScratch.AsSpan(0, math.min(_quadScratch.Length, _quadBufferCapacity)),
                FontAtlas = fontAtlas,
                Telemetry = _telemetryScratch.AsSpan(0, TelemetryCapacity),
                Counters = _counterScratch.AsSpan(0, CounterCapacity),
                AcousticTaps = _acousticTapScratch.AsSpan(0, acousticTapCount),
                O2Text = _o2Text,
                DepthText = _depthText,
                HeadingText = _headingText,
                LoadText = _loadText,
                CurrentWristPosition = (float3)wristPosition,
                CurrentWristRotation = new float4(wristRotation.x, wristRotation.y, wristRotation.z, wristRotation.w),
                CurrentHeadPosition = (float3)headPosition,
                CurrentHeadRotation = new float4(headRotation.x, headRotation.y, headRotation.z, headRotation.w),
                HeadForward = NormalizeSafe(headForward, new float3(0f, 0f, 1f)),
                HeadUp = NormalizeSafe(headUp, new float3(0f, 1f, 0f)),
                WristNormal = NormalizeSafe(wristNormal, new float3(0f, 0f, -1f)),
                WristRight = NormalizeSafe(wristRight, new float3(1f, 0f, 0f)),
                WristUp = NormalizeSafe(wristUp, new float3(0f, 1f, 0f)),
                WristForward = NormalizeSafe(wristForward, new float3(0f, 0f, 1f)),
                VisorPosition = visorPosition,
                Oxygen01 = FiniteSaturate(_latestVitals.Oxygen01),
                Health01 = FiniteSaturate(_latestVitals.Health01),
                Power01 = FiniteSaturate(_latestVitals.Power01),
                DepthMeters = FiniteNonNegative(_latestVitals.DepthMeters),
                SafeDepthMeters = math.max(1f, FiniteNonNegative(_latestVitals.SafeDepthMeters)),
                Radiation01 = FiniteSaturate(_latestVitals.Radiation01),
                Toxemia01 = FiniteSaturate(_latestVitals.Toxemia01),
                InventoryLoad01 = UIStateStore.ReadValueOrDefault(UIValueSlotId.InventoryLoad01, 0f),
                HologramDistance = hologramDistanceFromWrist,
                TextScale = textScale,
                PdaGridCellSize = pdaGridCellSizeMeters,
                PdaGridDistance = pdaGridDistanceMeters,
                GlitchMultiplier = glitchMultiplier,
                TimeSeconds = (float)SystemDispatcher.CurrentUnscaledTimeSeconds,
                DeltaTime = math.max(0f, deltaTime),
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameIndex,
                QualityWeight01 = _cachedQualityWeight01,
                MathLodPressure01 = ResolveMathLodPressure01(),
                PdaOpen = _latestPdaSignal.IsOpen,
                AcousticTapCount = acousticTapCount,
                LowColor = ToFloat4(lowColor),
                MidColor = ToFloat4(midColor),
                DangerColor = ToFloat4(dangerColor)
            };

            long startTicks = Stopwatch.GetTimestamp();
            builder.Execute();
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            int elapsedQ16 = (int)math.round((float)elapsedTicks * 1000000f * 16f / Stopwatch.Frequency);
            WristHudStateDTO state = _stateScratch[0];
            state.LastJobMicrosecondsQ16 = elapsedQ16;
            if (elapsedQ16 > (int)(JobWarningMicroseconds * 16f))
                state.Flags |= StateFlagJobOverBudget;
            _stateScratch[0] = state;

            PatchLatestTelemetryJobCost(ref state, _telemetryScratch.AsSpan(0, TelemetryCapacity), (uint)elapsedQ16);
            _stateScratch[0] = state;
            PublishWristHudScratch(state.ActiveQuadCount);
            dumpBlackBox = (state.Flags & StateFlagNaNDetected) != 0;

            if (dumpBlackBox)
                QueueBlackBoxDump();
        }

        private void PublishWristHudScratch(int activeQuadCount)
        {
            int quadCount = math.clamp(activeQuadCount, 0, math.min(_quadScratch.Length, _quadBufferCapacity));
            if (!FlushWristHudQuadScratch(quadCount))
            {
                WristHudStateDTO safeState = _stateScratch[0];
                safeState.ActiveQuadCount = 0;
                safeState.Flags |= StateFlagGpuUploadFault;
                _stateScratch[0] = safeState;
            }

            FlushWristHudTelemetryScratch(ResolveLatestTelemetryIndex(_stateScratch[0]));
            FlushWristHudCounterScratch();
            FlushWristHudStateScratch();
        }

        private bool FlushWristHudStateScratch()
        {
            if (!TryAcquireWristHudVaultBuffer(in _stateHandle, BufferID.WristHudState, StateCapacity, out IDataVault stateWriteVault, out NativeArray<WristHudStateDTO> states))
                return false;

            try
            {
                states[0] = _stateScratch[0];
                return true;
            }
            finally
            {
                ReleaseWristHudVaultWriteBuffer(stateWriteVault, in _stateHandle, BufferID.WristHudState);
            }
        }

        private bool FlushWristHudQuadScratch(int count)
        {
            if (count <= 0)
                return true;
            if (!TryAcquireWristHudVaultBuffer(in _quadHandle, BufferID.WristHudQuads, 1, out IDataVault quadWriteVault, out NativeArray<WristHudQuadTransformDTO> quads))
                return false;

            try
            {
                int writeCount = math.min(count, quads.Length);
                for (int i = 0; i < writeCount; i++)
                    quads[i] = _quadScratch[i];
                return true;
            }
            finally
            {
                ReleaseWristHudVaultWriteBuffer(quadWriteVault, in _quadHandle, BufferID.WristHudQuads);
            }
        }

        private bool FlushWristHudTelemetryScratch(int index)
        {
            if ((uint)index >= TelemetryCapacity)
                return false;
            if (!TryAcquireWristHudVaultBuffer(in _telemetryHandle, BufferID.WristHudTelemetryRing, TelemetryCapacity, out IDataVault telemetryWriteVault, out NativeArray<WristHudTelemetryEntry> telemetry))
                return false;

            try
            {
                if (index < telemetry.Length)
                    telemetry[index] = _telemetryScratch[index];
                return true;
            }
            finally
            {
                ReleaseWristHudVaultWriteBuffer(telemetryWriteVault, in _telemetryHandle, BufferID.WristHudTelemetryRing);
            }
        }

        private bool FlushWristHudCounterScratch()
        {
            if (!TryAcquireWristHudVaultBuffer(in _counterHandle, BufferID.WristHudCounters, CounterCapacity, out IDataVault counterWriteVault, out NativeArray<uint> counters))
                return false;

            try
            {
                int count = math.min(counters.Length, _counterScratch.Length);
                for (int i = 0; i < count; i++)
                    counters[i] = _counterScratch[i];
                return true;
            }
            finally
            {
                ReleaseWristHudVaultWriteBuffer(counterWriteVault, in _counterHandle, BufferID.WristHudCounters);
            }
        }

        private static int ResolveLatestTelemetryIndex(in WristHudStateDTO state)
        {
            int index = state.TelemetryCursor - 1;
            return index < 0 ? TelemetryCapacity - 1 : index;
        }

        private static void PatchLatestTelemetryJobCost(
            ref WristHudStateDTO state,
            Span<WristHudTelemetryEntry> telemetry,
            uint elapsedQ16)
        {
            if (telemetry.Length == 0)
                return;

            int index = state.TelemetryCursor - 1;
            if (index < 0)
                index += telemetry.Length;
            if ((uint)index >= (uint)telemetry.Length)
                return;

            WristHudTelemetryEntry entry = telemetry[index];
            entry.JobMicrosecondsQ16 = elapsedQ16;
            if (elapsedQ16 > (uint)(JobWarningMicroseconds * 16f))
                entry.Flags |= (uint)StateFlagJobOverBudget;
            telemetry[index] = entry;
        }

        private void UploadAndDraw()
        {
            if (_runtimeMaterial == null ||
                _runtimeQuadMesh == null ||
                !HasValidQuadGpuBuffers() ||
                !TryReadWristHudState(0, out WristHudStateDTO state) ||
                !TryReadOnlyQuadBuffer(out NativeArray<WristHudQuadTransformDTO>.ReadOnly quads))
            {
                return;
            }

            int count = math.clamp(state.ActiveQuadCount, 0, math.min(quads.Length, MaxDrawMeshInstancedBatch));
            _frontQuadCount = count;
            if (count <= 0 || state.Culled != 0)
                return;

            UploadQuads(count, quads, state.FrameIndex);
            FillDrawMatrices(count, quads);
            Camera camera = ResolveRenderCamera();
            if (camera == null)
                return;

            UnityEngine.Graphics.DrawMeshInstanced(
                _runtimeQuadMesh,
                0,
                _runtimeMaterial,
                _drawMatrices,
                count,
                _materialProperties,
                ShadowCastingMode.Off,
                false,
                renderLayer,
                camera,
                LightProbeUsage.Off);
        }

        private void UploadQuads(int count, NativeArray<WristHudQuadTransformDTO>.ReadOnly quads, int frameIndex)
        {
            if (count <= 0)
                return;

            if (frameIndex == _lastUploadedFrameIndex && count == _lastUploadCount)
                return;

            GraphicsBuffer writeBuffer = GetQuadWriteBuffer();
            if (writeBuffer == null || !writeBuffer.IsValid())
                return;

            try
            {
                NativeArray<WristHudQuadTransformDTO> mapped = writeBuffer.LockBufferForWrite<WristHudQuadTransformDTO>(0, count);
                try
                {
                    for (int i = 0; i < count; i++)
                        mapped[i] = quads[i];
                }
                finally
                {
                    writeBuffer.UnlockBufferAfterWrite<WristHudQuadTransformDTO>(count);
                }

                PromoteQuadGpuBuffer(writeBuffer);
                _lastUploadCount = count;
                _lastUploadedFrameIndex = frameIndex;
                ClearWristHudGpuUploadFault();
            }
            catch (ObjectDisposedException)
            {
                ReportWristHudGpuUploadFaultClosed();
            }
            catch (InvalidOperationException)
            {
                ReportWristHudGpuUploadFaultClosed();
            }
            catch (ArgumentException)
            {
                ReportWristHudGpuUploadFaultClosed();
            }
            catch (NotSupportedException)
            {
                ReportWristHudGpuUploadFaultClosed();
            }
            catch (UnityException)
            {
                ReportWristHudGpuUploadFaultClosed();
            }
        }

        private void FillDrawMatrices(int count, NativeArray<WristHudQuadTransformDTO>.ReadOnly quads)
        {
            for (int i = 0; i < count; i++)
                _drawMatrices[i] = ToMatrix4x4(quads[i].Matrix);
        }

        private void ReportWristHudGpuUploadFaultClosed()
        {
            _lastUploadCount = -1;
            _wristHudGpuUploadFaulted = true;
            if (!TryAcquireWristHudVaultBuffer(in _stateHandle, BufferID.WristHudState, StateCapacity, out IDataVault stateWriteVault, out NativeArray<WristHudStateDTO> states))
                return;

            try
            {
                WristHudStateDTO state = states[0];
                state.Flags |= StateFlagGpuUploadFault;
                states[0] = state;
            }
            finally
            {
                ReleaseWristHudVaultWriteBuffer(stateWriteVault, in _stateHandle, BufferID.WristHudState);
            }
        }

        private void ClearWristHudGpuUploadFault()
        {
            if (!_wristHudGpuUploadFaulted)
                return;

            if (!TryAcquireWristHudVaultBuffer(in _stateHandle, BufferID.WristHudState, StateCapacity, out IDataVault stateWriteVault, out NativeArray<WristHudStateDTO> states))
                return;

            try
            {
                WristHudStateDTO state = states[0];
                state.Flags &= ~StateFlagGpuUploadFault;
                states[0] = state;
                _wristHudGpuUploadFaulted = false;
            }
            finally
            {
                ReleaseWristHudVaultWriteBuffer(stateWriteVault, in _stateHandle, BufferID.WristHudState);
            }
        }

        private Camera ResolveRenderCamera()
        {
            Camera camera = GlobalRenderContext.CurrentCamera;
            if (IsValidRenderCamera(camera))
                return camera;

            if (renderCamera != null && IsValidRenderCamera(renderCamera))
                return renderCamera;

            return null;
        }

        private Transform ResolveHeadsetTransform()
        {
            if (headsetAnchor != null)
                return headsetAnchor;

            Camera camera = ResolveRenderCamera();
            return camera != null ? camera.transform : transform;
        }

        private Quaternion ResolveWristRotation()
        {
            Transform wrist = leftWristAnchor != null ? leftWristAnchor : transform;
            return wrist.rotation;
        }

        private float ResolveHeadingDegrees()
        {
            Transform head = ResolveHeadsetTransform();
            Vector3 forward = head.rotation * Vector3.forward;
            float2 planar = new float2(forward.x, forward.z);
            if (math.lengthsq(planar) < 0.0001f)
                return 0f;

            float angle = math.degrees(MathLodApproximation.ApproxAtan2Fast(planar.x, planar.y));
            return angle < 0f ? angle + 360f : angle;
        }

        private static bool IsValidRenderCamera(Camera camera)
        {
            return camera != null &&
                   camera.isActiveAndEnabled &&
                   camera.cameraType != CameraType.Preview &&
                   camera.cameraType != CameraType.Reflection;
        }

        private void GenerateMockFontAtlas()
        {
            if (!TryAcquireWristHudVaultBuffer(in _fontAtlasHandle, BufferID.WristHudFontAtlas, GlyphCapacity, out IDataVault fontWriteVault, out NativeArray<WristHudFontGlyphDTO> fontAtlas))
                return;

            try
            {
                const float columns = 16f;
                const float rows = 8f;
                float invColumns = 1f / columns;
                float invRows = 1f / rows;
                for (int i = 0; i < fontAtlas.Length; i++)
                {
                    int x = i & 15;
                    int y = i >> 4;
                    fontAtlas[i] = new WristHudFontGlyphDTO
                    {
                        UVRect = new float4(x * invColumns, y * invRows, invColumns, invRows),
                        Advance = i == 32 ? 0.45f : 0.72f,
                        BearingX = 0f,
                        BearingY = 0f,
                        CharacterCode = (uint)i
                    };
                }

                _fontAtlasGenerated = true;
            }
            finally
            {
                ReleaseWristHudVaultWriteBuffer(fontWriteVault, in _fontAtlasHandle, BufferID.WristHudFontAtlas);
            }
        }

#if UNITY_EDITOR
        private void TryLoadLegacyFontMetrics()
        {
            try
            {
                string archivePath = Path.Combine(ResolveProjectRoot(), "Docs", "Archive");
                if (!Directory.Exists(archivePath))
                {
                    _legacyMissing = true;
                    return;
                }

                string metricsPath = FindFirstFile(archivePath, LegacyFontMetricsFileName);
                string palettePath = FindFirstFile(archivePath, LegacyPaletteFileName);
                if (metricsPath == null && palettePath == null)
                {
                    _legacyMissing = true;
                    return;
                }

                if (metricsPath != null)
                    TryLoadBinaryFontMetrics(metricsPath);
                if (palettePath != null)
                    TryReadBinaryPalette(palettePath);
            }
            catch (IOException)
            {
                FailLegacyFontAtlasDiscovery();
            }
            catch (UnauthorizedAccessException)
            {
                FailLegacyFontAtlasDiscovery();
            }
            catch (ObjectDisposedException)
            {
                FailLegacyFontAtlasDiscovery();
            }
            catch (InvalidOperationException)
            {
                FailLegacyFontAtlasDiscovery();
            }
            catch (ArgumentException)
            {
                FailLegacyFontAtlasDiscovery();
            }
            catch (NotSupportedException)
            {
                FailLegacyFontAtlasDiscovery();
            }
        }

        private void FailLegacyFontAtlasDiscovery()
        {
            _legacyMissing = true;
            Hecton8.Core.H8Debug.LogWarning("[Agent1335] Legacy font atlas discovery failed.");
            GenerateMockFontAtlas();
        }

        private static string FindFirstFile(string root, string fileName)
        {
            using IEnumerator<string> paths = Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).GetEnumerator();
            if (paths.MoveNext())
                return paths.Current;

            return null;
        }

        private bool TryLoadBinaryFontMetrics(string path)
        {
            if (!TryReadWholeFileIntoBuffer(path, _csvReadBuffer, out int byteCount))
                return false;

            byte[] bytes = _csvReadBuffer;
            const int recordSize = 24;
            if (byteCount < recordSize)
                return false;

            if (!TryAcquireWristHudVaultBuffer(in _fontAtlasHandle, BufferID.WristHudFontAtlas, GlyphCapacity, out IDataVault fontWriteVault, out NativeArray<WristHudFontGlyphDTO> fontAtlas))
                return false;

            try
            {
                int count = math.min(byteCount / recordSize, fontAtlas.Length);
                for (int i = 0; i < count; i++)
                {
                    int offset = i * recordSize;
                    uint code = BitConverter.ToUInt32(bytes, offset);
                    if (code >= GlyphCapacity)
                        continue;

                    fontAtlas[(int)code] = new WristHudFontGlyphDTO
                    {
                        CharacterCode = code,
                        UVRect = new float4(
                            BitConverter.ToSingle(bytes, offset + 4),
                            BitConverter.ToSingle(bytes, offset + 8),
                            BitConverter.ToSingle(bytes, offset + 12),
                            BitConverter.ToSingle(bytes, offset + 16)),
                        Advance = math.max(0.05f, BitConverter.ToSingle(bytes, offset + 20)),
                        BearingX = 0f,
                        BearingY = 0f
                    };
                }

                return true;
            }
            finally
            {
                ReleaseWristHudVaultWriteBuffer(fontWriteVault, in _fontAtlasHandle, BufferID.WristHudFontAtlas);
            }
        }

        private bool TryReadBinaryPalette(string path)
        {
            if (!TryReadWholeFileIntoBuffer(path, _csvReadBuffer, out int byteCount) || byteCount < 48)
                return false;

            byte[] bytes = _csvReadBuffer;
            lowColor = ReadColor(bytes, byteCount, 0, lowColor);
            midColor = ReadColor(bytes, byteCount, 16, midColor);
            dangerColor = ReadColor(bytes, byteCount, 32, dangerColor);
            ApplyTunerSettings(hologramDistanceFromWrist, textScale, glitchMultiplier, lowColor, midColor, dangerColor);
            return true;
        }

        private static Color ReadColor(byte[] bytes, int length, int offset, Color fallback)
        {
            if (bytes == null || length < offset + 16)
                return fallback;

            return new Color(
                BitConverter.ToSingle(bytes, offset),
                BitConverter.ToSingle(bytes, offset + 4),
                BitConverter.ToSingle(bytes, offset + 8),
                BitConverter.ToSingle(bytes, offset + 12));
        }

        private static bool TryReadWholeFileIntoBuffer(string path, byte[] buffer, out int bytesRead)
        {
            bytesRead = 0;
            if (buffer == null || string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length <= 0L || stream.Length > buffer.Length)
                        return false;

                    int length = (int)stream.Length;
                    while (bytesRead < length)
                    {
                        int delta = stream.Read(buffer, bytesRead, length - bytesRead);
                        if (delta <= 0)
                            break;
                        bytesRead += delta;
                    }

                    return bytesRead == length;
                }
            }
            catch (IOException)
            {
                bytesRead = 0;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                bytesRead = 0;
                return false;
            }
        }

        private bool TryParseFontMetricsCsv(string path)
        {
            if (!File.Exists(path))
                return false;

            int byteCount = TryReadCsvBytes(path, _csvReadBuffer);
            if (byteCount <= 0)
                return false;

            ReadOnlySpan<byte> span = _csvReadBuffer.AsSpan(0, byteCount);
            int cursor = 0;
            bool any = false;
            Span<WristHudFontGlyphDTO> parsed = stackalloc WristHudFontGlyphDTO[GlyphCapacity];
            Span<byte> parsedSet = stackalloc byte[GlyphCapacity];
            while (cursor < span.Length)
            {
                ReadOnlySpan<byte> line = ReadLine(span, ref cursor);
                if (line.Length == 0 || line[0] == '#')
                    continue;

                if (!TryParseCsvLine(line, out WristHudFontGlyphDTO glyph))
                    continue;

                if (glyph.CharacterCode < GlyphCapacity)
                {
                    int glyphIndex = (int)glyph.CharacterCode;
                    parsed[glyphIndex] = glyph;
                    parsedSet[glyphIndex] = 1;
                    any = true;
                }
            }

            if (!any ||
                !TryAcquireWristHudVaultBuffer(in _fontAtlasHandle, BufferID.WristHudFontAtlas, GlyphCapacity, out IDataVault fontWriteVault, out NativeArray<WristHudFontGlyphDTO> fontAtlas))
            {
                return false;
            }

            try
            {
                for (int i = 0; i < parsed.Length && i < fontAtlas.Length; i++)
                {
                    if (parsedSet[i] != 0)
                        fontAtlas[i] = parsed[i];
                }

                return true;
            }
            finally
            {
                ReleaseWristHudVaultWriteBuffer(fontWriteVault, in _fontAtlasHandle, BufferID.WristHudFontAtlas);
            }
        }

        private static int TryReadCsvBytes(string path, byte[] buffer)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length <= 0L || stream.Length > buffer.Length)
                        return -1;

                    int length = (int)stream.Length;
                    int read = 0;
                    while (read < length)
                    {
                        int delta = stream.Read(buffer, read, length - read);
                        if (delta <= 0)
                            break;
                        read += delta;
                    }

                    return read == length ? read : -1;
                }
            }
            catch (IOException)
            {
                return -1;
            }
            catch (UnauthorizedAccessException)
            {
                return -1;
            }
        }

        private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> span, ref int cursor)
        {
            int start = cursor;
            while (cursor < span.Length && span[cursor] != '\n')
                cursor++;

            int end = cursor;
            if (cursor < span.Length && span[cursor] == '\n')
                cursor++;
            if (end > start && span[end - 1] == '\r')
                end--;
            return TrimAscii(span.Slice(start, end - start));
        }

        private static bool TryParseCsvLine(ReadOnlySpan<byte> line, out WristHudFontGlyphDTO glyph)
        {
            glyph = default;
            if (line.Length >= 3 && line[0] == 0xEF && line[1] == 0xBB && line[2] == 0xBF)
                line = line.Slice(3);

            int cursor = 0;
            if (!TryReadCsvUInt(line, ref cursor, out uint code) ||
                !TryReadCsvFloat(line, ref cursor, out float u) ||
                !TryReadCsvFloat(line, ref cursor, out float v) ||
                !TryReadCsvFloat(line, ref cursor, out float w) ||
                !TryReadCsvFloat(line, ref cursor, out float h) ||
                !TryReadCsvFloat(line, ref cursor, out float advance))
            {
                return false;
            }

            glyph.CharacterCode = code;
            glyph.UVRect = new float4(u, v, w, h);
            glyph.Advance = math.max(0.05f, advance);
            glyph.BearingX = TryReadCsvFloat(line, ref cursor, out float bearingX) ? bearingX : 0f;
            glyph.BearingY = TryReadCsvFloat(line, ref cursor, out float bearingY) ? bearingY : 0f;
            return true;
        }

        private static bool TryReadCsvUInt(ReadOnlySpan<byte> line, ref int cursor, out uint value)
        {
            value = 0u;
            ReadOnlySpan<byte> token = ReadCsvToken(line, ref cursor);
            if (token.Length == 0)
                return false;

            uint result = 0u;
            for (int i = 0; i < token.Length; i++)
            {
                byte c = token[i];
                if (c < '0' || c > '9')
                    return false;
                result = result * 10u + (uint)(c - '0');
            }

            value = result;
            return true;
        }

        private static bool TryReadCsvFloat(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            ReadOnlySpan<byte> token = ReadCsvToken(line, ref cursor);
            return TryParseAsciiFloat(token, out value);
        }

        private static ReadOnlySpan<byte> ReadCsvToken(ReadOnlySpan<byte> line, ref int cursor)
        {
            while (cursor < line.Length && IsAsciiWhitespace(line[cursor]))
                cursor++;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != ',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == ',')
                cursor++;
            return TrimAscii(line.Slice(start, end - start));
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length == 0)
                return false;

            int cursor = 0;
            bool negative = false;
            if (token[cursor] == '-' || token[cursor] == '+')
            {
                negative = token[cursor] == '-';
                cursor++;
            }

            float result = 0f;
            int digitCount = 0;
            while (cursor < token.Length)
            {
                byte c = token[cursor];
                if (c < '0' || c > '9')
                    break;

                result = result * 10f + (c - '0');
                cursor++;
                digitCount++;
            }

            if (cursor < token.Length && token[cursor] == '.')
            {
                cursor++;
                float scale = 0.1f;
                while (cursor < token.Length)
                {
                    byte c = token[cursor];
                    if (c < '0' || c > '9')
                        break;

                    result += (c - '0') * scale;
                    scale *= 0.1f;
                    cursor++;
                    digitCount++;
                }
            }

            if (digitCount == 0 || cursor != token.Length)
                return false;

            value = negative ? -result : result;
            return math.isfinite(value);
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length;
            while (start < end && IsAsciiWhitespace(value[start]))
                start++;
            while (end > start && IsAsciiWhitespace(value[end - 1]))
                end--;
            return value.Slice(start, end - start);
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == ' ' || value == '\t';
        }
#endif

        private void QueueBlackBoxDump()
        {
            if (!_blackBoxDumped)
                _blackBoxDumpQueued = true;
        }

        private void FlushQueuedBlackBoxDump()
        {
            if (!_blackBoxDumpQueued)
                return;

            _blackBoxDumpQueued = false;
            DumpBlackBoxOnce();
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped ||
                !TryReadWristHudState(0, out WristHudStateDTO state))
            {
                _blackBoxDumpQueued = !_blackBoxDumped;
                return;
            }

            if (!TryReadOnlyWristHudVaultBuffer(in _telemetryHandle, BufferID.WristHudTelemetryRing, TelemetryCapacity, out NativeArray<WristHudTelemetryEntry>.ReadOnly telemetry))
            {
                _blackBoxDumpQueued = true;
                return;
            }

            int entrySize = UnsafeUtility.SizeOf<WristHudTelemetryEntry>();
            int telemetryCapacity = telemetry.Length;
            int payloadBytes = telemetryCapacity * entrySize;
            WristHudBlackBoxDumpHeader header = new WristHudBlackBoxDumpHeader
            {
                Magic = BlackBoxDumpMagic,
                Version = BlackBoxDumpVersion,
                FrameIndex = (uint)state.FrameIndex,
                Flags = (uint)state.Flags,
                TelemetryCapacity = telemetryCapacity,
                TelemetryCursor = state.TelemetryCursor,
                TelemetryEntrySizeBytes = entrySize,
                PayloadBytes = payloadBytes
            };

            int headerBytes = UnsafeUtility.SizeOf<WristHudBlackBoxDumpHeader>();
            int byteCount = headerBytes + payloadBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(WristHologramHudRuntime),
                    "wristHudBlackBoxDumpPayload",
                    NativeArrayOptions.ClearMemory);
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                UnsafeUtility.MemCpy(destination, UnsafeUtility.AddressOf(ref header), headerBytes);
                byte* rowDestination = destination + headerBytes;
                for (int i = 0; i < telemetryCapacity; i++)
                {
                    WristHudTelemetryEntry row = telemetry[i];
                    UnsafeUtility.MemCpy(rowDestination + i * entrySize, UnsafeUtility.AddressOf(ref row), entrySize);
                }

                telemetry = default;

                if (NativeFaultDumpWriter.TryWriteAll("Docs/AgentLogs/Dump_1335_WristHologramHud.bin", payload, byteCount))
                    _blackBoxDumped = true;
            }
            catch (IOException)
            {
                Hecton8.Core.H8Debug.LogError("Agent1335 wrist HUD blackbox dump failed.");
            }
            catch (UnauthorizedAccessException)
            {
                Hecton8.Core.H8Debug.LogError("Agent1335 wrist HUD blackbox dump failed.");
            }
            catch (ObjectDisposedException)
            {
                Hecton8.Core.H8Debug.LogError("Agent1335 wrist HUD blackbox dump failed.");
            }
            catch (InvalidOperationException)
            {
                Hecton8.Core.H8Debug.LogError("Agent1335 wrist HUD blackbox dump failed.");
            }
            catch (ArgumentException)
            {
                Hecton8.Core.H8Debug.LogError("Agent1335 wrist HUD blackbox dump failed.");
            }
            catch (NotSupportedException)
            {
                Hecton8.Core.H8Debug.LogError("Agent1335 wrist HUD blackbox dump failed.");
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(WristHologramHudRuntime),
                    "wristHudBlackBoxDumpPayload");
            }
        }

        private bool TryReadWristHudTelemetryRow(int index, out WristHudTelemetryEntry row)
        {
            row = default;
            if ((uint)index >= TelemetryCapacity ||
                !TryReadOnlyWristHudVaultBuffer(in _telemetryHandle, BufferID.WristHudTelemetryRing, TelemetryCapacity, out NativeArray<WristHudTelemetryEntry>.ReadOnly telemetry))
            {
                return false;
            }

            row = telemetry[index];
            return true;
        }

        private static PlayerVitalsSignal SanitizeVitals(in PlayerVitalsSignal signal)
        {
            PlayerVitalsSignal sanitized = signal;
            sanitized.Oxygen01 = FiniteSaturate(signal.Oxygen01);
            sanitized.Health01 = FiniteSaturate(signal.Health01);
            sanitized.Power01 = FiniteSaturate(signal.Power01);
            sanitized.DepthMeters = FiniteNonNegative(signal.DepthMeters);
            sanitized.SafeDepthMeters = math.max(1f, FiniteNonNegative(signal.SafeDepthMeters));
            sanitized.Radiation01 = FiniteSaturate(signal.Radiation01);
            sanitized.Toxemia01 = FiniteSaturate(signal.Toxemia01);
            return sanitized;
        }

        private static void ColdSanityCheckLayout()
        {
            if (UnsafeUtility.SizeOf<WristHudQuadTransformDTO>() != 112)
                Hecton8.Core.H8Debug.LogError("WristHudQuadTransformDTO stride mismatch.");
            if (UnsafeUtility.SizeOf<WristHudStateDTO>() != 248)
                Hecton8.Core.H8Debug.LogError("WristHudStateDTO stride mismatch.");
            if (UnsafeUtility.SizeOf<WristHudFontGlyphDTO>() != 32)
                Hecton8.Core.H8Debug.LogError("WristHudFontGlyphDTO stride mismatch.");
            if (UnsafeUtility.SizeOf<WristHudTelemetryEntry>() != 64)
                Hecton8.Core.H8Debug.LogError("WristHudTelemetryEntry stride mismatch.");
            if (UnsafeUtility.SizeOf<WristHudBlackBoxDumpHeader>() != 32)
                Hecton8.Core.H8Debug.LogError("WristHudBlackBoxDumpHeader stride mismatch.");
            if (UnsafeUtility.SizeOf<PlayerVitalsSignal>() != 32)
                Hecton8.Core.H8Debug.LogError("PlayerVitalsSignal stride mismatch.");
            if (UnsafeUtility.SizeOf<O2LevelChangedSignal>() != 8)
                Hecton8.Core.H8Debug.LogError("O2LevelChangedSignal stride mismatch.");
            if (UnsafeUtility.SizeOf<PdaOpenedSignal>() != 16)
                Hecton8.Core.H8Debug.LogError("PdaOpenedSignal stride mismatch.");
            if (UnsafeUtility.SizeOf<AcousticEchoTap>() != 32)
                Hecton8.Core.H8Debug.LogError("AcousticEchoTap stride mismatch.");
        }

        private void EnsureGraphicsBuffer(ref GraphicsBuffer buffer, int count)
        {
            if (buffer != null && buffer.IsValid() && buffer.count == count)
                return;

            ReleaseGraphicsBuffer(ref buffer);
            buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<WristHudQuadTransformDTO>()); // COLD ALLOC: GraphicsBuffer[quadCapacity] - double-buffered SDF HUD quad DTO upload - owner: WristHologramHudRuntime
            _lastUploadCount = -1;
            _lastUploadedFrameIndex = -1;
        }

        private bool HasValidQuadGpuBuffers()
        {
            return _quadGpuBufferA != null &&
                   _quadGpuBufferB != null &&
                   _quadGpuBufferA.IsValid() &&
                   _quadGpuBufferB.IsValid();
        }

        private GraphicsBuffer GetQuadWriteBuffer()
        {
            if (!HasValidQuadGpuBuffers())
                return null;

            return ReferenceEquals(_activeQuadGpuBuffer, _quadGpuBufferA) ? _quadGpuBufferB : _quadGpuBufferA;
        }

        private void PromoteQuadGpuBuffer(GraphicsBuffer buffer)
        {
            _activeQuadGpuBuffer = buffer;
            if (_materialProperties != null && buffer != null && buffer.IsValid())
                _materialProperties.SetBuffer(WristHudQuadsId, buffer);
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static void ClearFixedString(ref FixedString64Bytes value)
        {
            value.Length = 0;
        }

        private static void AppendAscii(ref FixedString64Bytes value, char c)
        {
            value.Append(c);
        }

        private static void AppendUInt(ref FixedString64Bytes value, uint number)
        {
            if (number >= 1000u)
                AppendAscii(ref value, (char)('0' + (number / 1000u) % 10u));
            if (number >= 100u)
                AppendAscii(ref value, (char)('0' + (number / 100u) % 10u));
            if (number >= 10u)
                AppendAscii(ref value, (char)('0' + (number / 10u) % 10u));
            AppendAscii(ref value, (char)('0' + number % 10u));
        }

        private static float4 ToFloat4(Color color)
        {
            return new float4(color.r, color.g, color.b, color.a);
        }

        private static Matrix4x4 ToMatrix4x4(float4x4 matrix)
        {
            Matrix4x4 result = default;
            result.SetColumn(0, new Vector4(matrix.c0.x, matrix.c0.y, matrix.c0.z, matrix.c0.w));
            result.SetColumn(1, new Vector4(matrix.c1.x, matrix.c1.y, matrix.c1.z, matrix.c1.w));
            result.SetColumn(2, new Vector4(matrix.c2.x, matrix.c2.y, matrix.c2.z, matrix.c2.w));
            result.SetColumn(3, new Vector4(matrix.c3.x, matrix.c3.y, matrix.c3.z, matrix.c3.w));
            return result;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) && math.lengthsq(value) > 0.0001f
                ? math.normalize(value)
                : fallback;
        }

        private static float FiniteSaturate(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float FiniteNonNegative(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static float SmoothStep01(float value)
        {
            float saturated = math.saturate(value);
            return saturated * saturated * (3f - 2f * saturated);
        }

        private static int EncodeQualityWeightQ8(float qualityWeight01)
        {
            return EncodeUnitWeightQ8(qualityWeight01, 1f);
        }

        private static int EncodeUnitWeightQ8(float value01, float fallback01)
        {
            float safeValue = math.select(fallback01, value01, math.isfinite(value01));
            return (int)math.round(math.saturate(safeValue) * 255f);
        }

        private void RefreshQualityPolicy()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            _cachedQualityWeight01 = math.saturate(math.select(_cachedQualityWeight01, quality, math.isfinite(quality)));
        }

        private float ResolveMathLodPressure01()
        {
            float qualityPressure = 1f - SmoothStep01(_cachedQualityWeight01);
            float stressPressure = SmoothStep01(math.saturate((_globalSystemPressure01 - 0.62f) * math.rcp(0.38f)));
            float holdPressure = math.select(0f, 1f, _survivalPressureHoldFrames > 0);
            return math.saturate(math.max(math.max(qualityPressure, stressPressure), holdPressure));
        }

        private int ResolveMockAcousticTapCapacity()
        {
            float visualBudget01 = math.saturate(SmoothStep01(_cachedQualityWeight01) * (1f - ResolveMathLodPressure01() * 0.75f));
            return math.clamp((int)math.round(math.lerp(12f, 36f, visualBudget01)), 12, 36);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                IPlayerRuntimeContext playerContext = currentService as IPlayerRuntimeContext;
                RefreshPlayerToxicityTargetHash(playerContext);
                RefreshPlayerSurvivalVitalsSourceId(playerContext);
                _lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
                PdaProjectorRebindPlayerRuntimeContext(playerContext);
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            _cachedDataVault = currentService is IDataVault currentVault ? currentVault : null;
            if (!ReferenceEquals(_vault, _cachedDataVault))
            {
                PdaProjectorReleaseNativeStateHandles();
                ReleaseNativeStateHandles();
                _fontAtlasGenerated = false;
                PdaProjectorOnDataVaultServiceReplaced();
                if (isActiveAndEnabled)
                {
                    EnsureNativeBuffers();
                    EnsureSignalBuffers();
                    EnsureGraphicsResources();
                    SeedInitialState();
                    _lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
                }
            }
        }

        private void RefreshCachedRegistryServices()
        {
            CacheDataVaultCold(GlobalRegistry.DataVault);
            RefreshPlayerToxicityTargetHash(GlobalRegistry.Player);
            RefreshPlayerSurvivalVitalsSourceId(GlobalRegistry.Player);
            PdaProjectorRebindPlayerRuntimeContext(GlobalRegistry.Player);
            RefreshQualityPolicy();
        }

        private void CacheDataVaultCold(IDataVault vault)
        {
            _cachedDataVault = vault;
        }

        private void RefreshPlayerToxicityTargetHash(IPlayerRuntimeContext playerContext)
        {
            GameObject playerObject = playerContext != null ? playerContext.PlayerObject : null;
            if (playerObject == null)
                playerObject = BootstrapState.CurrentPlayerObject;

            if (ReferenceEquals(playerObject, _playerToxicityTargetObject) && _playerToxicityTargetHash != 0u)
                return;

            _playerToxicityTargetObject = playerObject;
            uint targetHash = playerObject != null ? unchecked((uint)EntityId.ToULong(playerObject.GetEntityId())) : 0u;
            _playerToxicityTargetHash = targetHash != 0u ? targetHash : PlayerToxicityFallbackEntityHash;
        }

        private void RefreshPlayerSurvivalVitalsSourceId(IPlayerRuntimeContext playerContext)
        {
            var survival = playerContext != null && playerContext.IsInitialized
                ? playerContext.SurvivalSystem
                : null;
            _playerSurvivalVitalsSourceId = survival != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(survival.GetEntityId()))
                : 0u;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private static string ResolveProjectRoot()
        {
            string root = Application.dataPath;
            return string.IsNullOrEmpty(root) ? Directory.GetCurrentDirectory() : Path.GetFullPath(Path.Combine(root, ".."));
        }

        private string GetProjectRoot()
        {
            if (string.IsNullOrEmpty(_projectRoot))
                _projectRoot = ResolveProjectRoot();
            return _projectRoot;
        }

        private string GetCsvOverridePath()
        {
            if (string.IsNullOrEmpty(_csvOverridePath))
                _csvOverridePath = Path.Combine(GetProjectRoot(), CsvOverrideFileName);
            return _csvOverridePath;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (TryGetPdaGridGizmo(out Matrix4x4 matrix, out Vector3 size))
            {
                Gizmos.color = new Color(midColor.r, midColor.g, midColor.b, 0.85f);
                Gizmos.matrix = matrix;
                Gizmos.DrawWireCube(Vector3.zero, size);
                Gizmos.matrix = Matrix4x4.identity;
            }

            PdaProjectorOnDrawGizmosSelected();
        }

        private void OnValidate()
        {
            hologramDistanceFromWrist = math.max(0.02f, hologramDistanceFromWrist);
            textScale = math.max(0.002f, textScale);
            glitchMultiplier = math.max(0f, glitchMultiplier);
            quadCapacity = math.clamp(quadCapacity, 64, MaxDrawMeshInstancedBatch);
            pdaGridCellSizeMeters = math.clamp(pdaGridCellSizeMeters, 0.05f, 1f);
            pdaGridDistanceMeters = math.clamp(pdaGridDistanceMeters, 0.02f, 0.6f);
            baseIntensity = math.clamp(baseIntensity, 0.1f, 8f);
            csvPollIntervalSeconds = math.clamp(csvPollIntervalSeconds, 0.05f, 2f);
            PdaProjectorOnValidate();
        }
#endif

        private ref struct TextToQuadsBuilder
        {
            public Span<WristHudStateDTO> States;
            public Span<WristHudQuadTransformDTO> Quads;
            public NativeArray<WristHudFontGlyphDTO>.ReadOnly FontAtlas;
            public Span<WristHudTelemetryEntry> Telemetry;
            public Span<uint> Counters;
            public ReadOnlySpan<AcousticEchoTap> AcousticTaps;

            public FixedString64Bytes O2Text;
            public FixedString64Bytes DepthText;
            public FixedString64Bytes HeadingText;
            public FixedString64Bytes LoadText;

            public float3 CurrentWristPosition;
            public float4 CurrentWristRotation;
            public float3 CurrentHeadPosition;
            public float4 CurrentHeadRotation;
            public float3 HeadForward;
            public float3 HeadUp;
            public float3 WristNormal;
            public float3 WristRight;
            public float3 WristUp;
            public float3 WristForward;
            public float3 VisorPosition;
            public float Oxygen01;
            public float Health01;
            public float Power01;
            public float DepthMeters;
            public float SafeDepthMeters;
            public float Radiation01;
            public float Toxemia01;
            public float InventoryLoad01;
            public float HologramDistance;
            public float TextScale;
            public float PdaGridCellSize;
            public float PdaGridDistance;
            public float GlitchMultiplier;
            public float TimeSeconds;
            public float DeltaTime;
            public int FrameIndex;
            public float QualityWeight01;
            public float MathLodPressure01;
            public uint PdaOpen;
            public int AcousticTapCount;
            public float4 LowColor;
            public float4 MidColor;
            public float4 DangerColor;

            public void Execute()
            {
                if (States.Length == 0 || Quads.Length == 0)
                    return;

                WristHudStateDTO state = States[0];
                state.FrameIndex = FrameIndex;
                float qualityWeight01 = math.saturate(math.select(1f, QualityWeight01, math.isfinite(QualityWeight01)));
                float mathLodPressure01 = math.saturate(math.select(0f, MathLodPressure01, math.isfinite(MathLodPressure01)));
                float visualBudget01 = math.saturate(SmoothStep01(qualityWeight01) * (1f - mathLodPressure01 * 0.75f));
                state.QualityWeightQ8 = EncodeUnitWeightQ8(qualityWeight01, 1f);
                state.MathLodPressureQ8 = EncodeUnitWeightQ8(mathLodPressure01, 0f);
                state.LowColor = LowColor;
                state.MidColor = MidColor;
                state.DangerColor = DangerColor;
                state.HeadPositionAndO2 = new float4(CurrentHeadPosition, Oxygen01);
                state.HeadForwardAndDepth = new float4(HeadForward, DepthMeters);
                state.WristRightAndSafeDepth = new float4(WristRight, SafeDepthMeters);
                state.WristUpAndPressure = new float4(WristUp, Power01);
                state.WristForwardAndRadiation = new float4(WristForward, Radiation01);
                state.CompassAndVitals.w = GlitchMultiplier;
                state.Flags &= ~(StateFlagCulled | StateFlagPdaOpen | StateFlagNaNDetected);

                float attentionDot = math.dot(HeadForward, WristNormal);
                bool culled = attentionDot < AttentionDotThreshold;
                if (culled)
                {
                    state.ActiveQuadCount = 0;
                    state.Culled = 1;
                    state.Flags |= StateFlagCulled;
                    WriteTelemetry(ref state, 0, 0, 0, attentionDot, 0f);
                    States[0] = state;
                    if (Counters.Length > 0)
                        Counters[0] = 0u;
                    return;
                }

                float3 smoothedPosition = CurrentWristPosition;
                quaternion wristRotation = new quaternion(CurrentWristRotation);
                bool hasPreviousWristRotation = math.lengthsq(state.WristRotation) > 0.25f;
                if (hasPreviousWristRotation)
                {
                    float alpha = math.lerp(1f, math.saturate(DeltaTime * 14f), visualBudget01);
                    smoothedPosition = math.lerp(state.WristPositionAndDistance.xyz, CurrentWristPosition, alpha);
                    quaternion previous = new quaternion(state.WristRotation);
                    float4 targetValue = CurrentWristRotation;
                    if (math.dot(previous.value, targetValue) < 0f)
                        targetValue = -targetValue;
                    wristRotation = math.normalize(new quaternion(math.lerp(previous.value, targetValue, alpha)));
                }

                state.WristPositionAndDistance = new float4(smoothedPosition, HologramDistance);
                state.WristRotation = wristRotation.value;

                float3 anchorPosition = smoothedPosition + WristNormal * HologramDistance;
                float4x4 wristMatrix = BuildBasisMatrix(anchorPosition, WristRight, WristUp, WristNormal);
                float4x4 visorMatrix = BuildBasisMatrix(VisorPosition, math.cross(HeadUp, HeadForward), HeadUp, HeadForward);

                int quadIndex = 0;
                int glyphCount = 0;
                int barCount = 0;
                int gridCount = 0;
                int radarCount = 0;
                float hazard = math.saturate(math.max(Radiation01, Toxemia01) * GlitchMultiplier);
                float depth01 = math.saturate(DepthMeters / math.max(1f, SafeDepthMeters));
                float4 vitalsColor = math.lerp(DangerColor, MidColor, Oxygen01);

                AppendText(ref quadIndex, ref glyphCount, wristMatrix, O2Text, new float2(-0.11f, 0.065f), TextScale, vitalsColor, hazard);
                AppendText(ref quadIndex, ref glyphCount, wristMatrix, DepthText, new float2(-0.11f, 0.035f), TextScale, math.lerp(MidColor, DangerColor, depth01), hazard);
                AppendText(ref quadIndex, ref glyphCount, wristMatrix, HeadingText, new float2(-0.11f, 0.005f), TextScale * 0.86f, LowColor, hazard * 0.55f);
                AppendText(ref quadIndex, ref glyphCount, wristMatrix, LoadText, new float2(-0.11f, -0.025f), TextScale * 0.86f, math.lerp(LowColor, DangerColor, InventoryLoad01), hazard * 0.45f);
                AppendDepthBar(ref quadIndex, ref barCount, wristMatrix, depth01, hazard, visualBudget01);
                AppendCompass(ref quadIndex, wristMatrix, hazard);
                if (PdaOpen != 0u)
                {
                    state.Flags |= StateFlagPdaOpen;
                    AppendPdaGrid(ref quadIndex, ref gridCount, wristMatrix, ref state);
                }

                if (Oxygen01 < 0.2f)
                    AppendVignette(ref quadIndex, visorMatrix, math.saturate((0.2f - Oxygen01) * 5f), hazard);

                AppendRadar(ref quadIndex, ref radarCount, wristMatrix, hazard, visualBudget01);

                state.ActiveQuadCount = quadIndex;
                state.GlyphQuadCount = glyphCount;
                state.BarQuadCount = barCount;
                state.PdaGridQuadCount = gridCount;
                state.RadarQuadCount = radarCount;
                state.Culled = 0;
                if (!AllFinite(state.WristPositionAndDistance) || !AllFinite(state.WristRotation))
                    state.Flags |= StateFlagNaNDetected;

                WriteTelemetry(ref state, quadIndex, glyphCount, radarCount, attentionDot, ResolveHeadingDegrees(HeadForward));
                States[0] = state;
                if (Counters.Length > 1)
                {
                    Counters[0] = (uint)quadIndex;
                    Counters[1] = (uint)state.Flags;
                }
            }

            private void AppendText(ref int quadIndex, ref int glyphCount, float4x4 anchor, FixedString64Bytes text, float2 origin, float scale, float4 color, float glitch)
            {
                float x = origin.x;
                for (int i = 0; i < text.Length && quadIndex < Quads.Length; i++)
                {
                    byte code = text[i];
                    int glyphIndex = code < FontAtlas.Length ? code : 63;
                    WristHudFontGlyphDTO glyph = FontAtlas[glyphIndex];
                    if (code == 32)
                    {
                        x += scale * glyph.Advance;
                        continue;
                    }

                    float width = scale * math.max(0.35f, glyph.Advance);
                    float height = scale * 1.65f;
                    float2 local = new float2(x + width * 0.5f + glyph.BearingX * scale, origin.y + glyph.BearingY * scale);
                    Quads[quadIndex++] = new WristHudQuadTransformDTO
                    {
                        Matrix = BuildQuadMatrix(anchor, local, new float2(width, height), 0f),
                        Color = color,
                        UVRect = glyph.UVRect,
                        CharacterCode = code,
                        GlitchIntensity = glitch
                    };
                    glyphCount++;
                    x += scale * glyph.Advance;
                }
            }

            private void AppendDepthBar(ref int quadIndex, ref int barCount, float4x4 anchor, float depth01, float hazard, float visualBudget01)
            {
                const int segments = 20;
                float filled = depth01 * segments;
                for (int i = 0; i < segments && quadIndex < Quads.Length; i++)
                {
                    float fill01 = math.saturate(filled - i);
                    float critical = math.saturate((depth01 - 0.82f) * 5.555555f);
                    float wave = TriangleWave(TimeSeconds * math.lerp(7f, 22f, visualBudget01) + i * math.lerp(0.17f, 0.73f, visualBudget01));
                    float shiver = 1f + critical * wave * 0.12f;
                    float4 color = math.lerp(LowColor, DangerColor, math.saturate((float)i / (segments - 1f) + critical * 0.6f));
                    color.w *= math.lerp(0.18f, 1f, fill01);
                    float x = -0.105f + i * 0.011f;
                    Quads[quadIndex++] = new WristHudQuadTransformDTO
                    {
                        Matrix = BuildQuadMatrix(anchor, new float2(x, -0.058f), new float2(0.008f, 0.018f * shiver), 0.0005f),
                        Color = color,
                        UVRect = new float4(fill01, depth01, critical, 0f),
                        CharacterCode = SpecialDepthBarCode,
                        GlitchIntensity = hazard + critical
                    };
                    barCount++;
                }
            }

            private void AppendPdaGrid(ref int quadIndex, ref int gridCount, float4x4 anchor, ref WristHudStateDTO state)
            {
                float cell = math.max(0.01f, PdaGridCellSize);
                float3 center = anchor.c3.xyz + anchor.c1.xyz * 0.13f + anchor.c2.xyz * PdaGridDistance;
                float4x4 gridAnchor = BuildBasisMatrix(center, anchor.c0.xyz, anchor.c1.xyz, anchor.c2.xyz);
                state.PdaGridCenterAndCell = new float4(center, cell);
                for (int y = 0; y < 4 && quadIndex < Quads.Length; y++)
                {
                    for (int x = 0; x < 6 && quadIndex < Quads.Length; x++)
                    {
                        float2 local = new float2((x - 2.5f) * cell, (1.5f - y) * cell);
                        Quads[quadIndex++] = new WristHudQuadTransformDTO
                        {
                            Matrix = BuildQuadMatrix(gridAnchor, local, new float2(cell * 0.78f, cell * 0.78f), 0f),
                            Color = new float4(0.14f, 0.9f, 0.82f, 0.36f),
                            UVRect = new float4(x, y, 6f, 4f),
                            CharacterCode = SpecialPdaGridCode,
                            GlitchIntensity = 0.08f
                        };
                        gridCount++;
                    }
                }
            }

            private void AppendVignette(ref int quadIndex, float4x4 visorMatrix, float intensity, float hazard)
            {
                if (quadIndex >= Quads.Length)
                    return;

                Quads[quadIndex++] = new WristHudQuadTransformDTO
                {
                    Matrix = BuildQuadMatrix(visorMatrix, new float2(0f, 0f), new float2(1.2f, 0.72f), 0f),
                    Color = new float4(0.02f, 0.06f, 0.07f, intensity * 0.82f),
                    UVRect = new float4(intensity, Oxygen01, 0f, 0f),
                    CharacterCode = SpecialVignetteCode,
                    GlitchIntensity = hazard + intensity * 0.45f
                };
            }

            private void AppendCompass(ref int quadIndex, float4x4 anchor, float hazard)
            {
                if (quadIndex >= Quads.Length)
                    return;

                float heading = ResolveHeadingDegrees(HeadForward);
                Quads[quadIndex++] = new WristHudQuadTransformDTO
                {
                    Matrix = BuildQuadMatrix(anchor, new float2(0f, 0.094f), new float2(0.24f, 0.018f), 0f),
                    Color = LowColor,
                    UVRect = new float4(heading / 360f, 0f, 1f, 1f),
                    CharacterCode = SpecialCompassCode,
                    GlitchIntensity = hazard * 0.4f
                };
            }

            private void AppendRadar(ref int quadIndex, ref int radarCount, float4x4 anchor, float hazard, float visualBudget01)
            {
                int max = math.clamp((int)math.round(math.lerp(12f, 100f, visualBudget01)), 12, 100);
                int count = math.min(math.min(AcousticTapCount, max), AcousticTaps.Length);
                for (int i = 0; i < count && quadIndex < Quads.Length; i++)
                {
                    AcousticEchoTap tap = AcousticTaps[i];
                    if (tap.Amplitude01 <= 0.01f)
                        continue;

                    float2 local = new float2(tap.RelativePositionMeters.x, tap.RelativePositionMeters.z) * 0.0025f;
                    float lenSq = math.lengthsq(local);
                    if (lenSq > 0.0036f)
                        local *= math.rsqrt(lenSq) * 0.06f;

                    float size = 0.006f + math.saturate(tap.Amplitude01) * 0.012f;
                    Quads[quadIndex++] = new WristHudQuadTransformDTO
                    {
                        Matrix = BuildQuadMatrix(anchor, new float2(0.081f + local.x, -0.028f + local.y), new float2(size, size), 0.001f),
                        Color = math.lerp(MidColor, DangerColor, tap.Amplitude01),
                        UVRect = new float4(local.x, local.y, tap.Amplitude01, tap.AgeSeconds),
                        CharacterCode = SpecialRadarBlipCode,
                        GlitchIntensity = hazard + tap.Amplitude01 * 0.25f
                    };
                    radarCount++;
                }
            }

            private void WriteTelemetry(ref WristHudStateDTO state, int quadCount, int glyphCount, int radarCount, float attentionDot, float heading)
            {
                if (Telemetry.Length == 0)
                    return;

                int cursor = state.TelemetryCursor;
                if ((uint)cursor >= (uint)Telemetry.Length)
                    cursor = 0;

                WristHudTelemetryEntry entry = new WristHudTelemetryEntry
                {
                    FrameIndex = (uint)FrameIndex,
                    StateHash = HashState(quadCount, glyphCount, radarCount, state.Flags),
                    Flags = (uint)state.Flags,
                    ActiveQuadCount = (uint)math.max(0, quadCount),
                    GlyphQuadCount = (uint)math.max(0, glyphCount),
                    RadarCount = (uint)math.max(0, radarCount),
                    JobMicrosecondsQ16 = 0u,
                    TelemetryCursor = (uint)cursor,
                    Oxygen01 = Oxygen01,
                    DepthMeters = DepthMeters,
                    SafeDepthMeters = SafeDepthMeters,
                    Radiation01 = Radiation01,
                    Toxemia01 = Toxemia01,
                    AttentionDot = attentionDot,
                    HeadingDegrees = heading,
                    PdaOpen01 = PdaOpen != 0u ? 1f : 0f
                };
                Telemetry[cursor] = entry;
                cursor++;
                if (cursor >= Telemetry.Length)
                    cursor = 0;
                state.TelemetryCursor = cursor;
            }

            private static float4x4 BuildBasisMatrix(float3 position, float3 right, float3 up, float3 forward)
            {
                return new float4x4(
                    new float4(right, 0f),
                    new float4(up, 0f),
                    new float4(forward, 0f),
                    new float4(position, 1f));
            }

            private static float4x4 BuildQuadMatrix(float4x4 anchor, float2 local, float2 size, float zOffset)
            {
                float3 right = anchor.c0.xyz;
                float3 up = anchor.c1.xyz;
                float3 forward = anchor.c2.xyz;
                float3 position = anchor.c3.xyz + right * local.x + up * local.y + forward * zOffset;
                return new float4x4(
                    new float4(right * size.x, 0f),
                    new float4(up * size.y, 0f),
                    new float4(forward * 0.001f, 0f),
                    new float4(position, 1f));
            }

            private static float ResolveHeadingDegrees(float3 forward)
            {
                float2 planar = new float2(forward.x, forward.z);
                if (math.lengthsq(planar) < 0.0001f)
                    return 0f;

                float angle = math.degrees(MathLodApproximation.ApproxAtan2Fast(planar.x, planar.y));
                return angle < 0f ? angle + 360f : angle;
            }

            private static float TriangleWave(float x)
            {
                return math.abs(math.frac(x) * 2f - 1f) * 2f - 1f;
            }

            private static float SmoothStep01(float value)
            {
                float saturated = math.saturate(value);
                return saturated * saturated * (3f - 2f * saturated);
            }

            private static uint HashState(int quadCount, int glyphCount, int radarCount, int flags)
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)quadCount) * 16777619u;
                hash = (hash ^ (uint)glyphCount) * 16777619u;
                hash = (hash ^ (uint)radarCount) * 16777619u;
                hash = (hash ^ (uint)flags) * 16777619u;
                return hash;
            }

            private static bool AllFinite(float4 value)
            {
                return math.all(math.isfinite(value));
            }
        }
    }
}

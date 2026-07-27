using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Data;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    public unsafe sealed partial class TerminalOsRuntime : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001DirectSignalPushDropCount_TerminalOsRuntime;

        private const int ActiveRuntimeCapacity = 4;
        private const int MaxQualityResolution = 512;
        private const int MinQualityResolution = 256;
        private const int MaxIdleDecryptionStride = 6;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const float AttentionCullDistanceMeters = 20f;
        private const float AttentionCullDistanceSq = AttentionCullDistanceMeters * AttentionCullDistanceMeters;
        private const uint FaultLayoutMismatch = 1u << 0;
        private const uint FaultFormatBudget = 1u << 1;
        private const uint FaultNonFinite = 1u << 2;
        private const uint FaultVaultUnavailable = 1u << 3;
        private const uint FaultDecryptionBudget = 1u << 4;
        private const uint FaultDecryptionNonFinite = 1u << 5;
        private const uint FaultDecryptionDumpBackpressure = 1u << 6;
        private const uint FaultAppliedLorePreviewMiss = 1u << 7;
        private const uint FaultAppliedLorePreviewDrop = 1u << 8;
        private const ushort DialogueDecisionSaveFacilityMask = SaveBinaryStorage.PlayerDialogueChoiceSaveFacilityMask;
        private const uint DecryptionDumpBackpressureHash = 0x53483237u; // SH27
        private const uint DecryptionDumpContextHash = 0x54444457u; // TDDW
        private const string NativeOwner = nameof(TerminalOsRuntime);
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1309_TerminalOS.bin";
        private const string DumpMirrorRelativePath = "Docs/AgentLogs/Dump_1309_TerminalOSMirror.h8dump";
        private const string DecryptionDumpRelativePath = "Docs/AgentLogs/Dump_1309_TerminalDecryption.bin";
        private const uint TerminalDumpMagic = 0x544F5338u; // 8SOT
        private const uint TerminalDumpVersion = 1u;
        private const int TerminalDumpHeaderBytes = 32;
        private const uint SourceHash = 0x544F5331u; // 1SOT
        private const BufferID TerminalStatesBufferId = BufferID.DiegeticGlitchSurgeonRuntime_TerminalOsStateBridgeBufferId;
        private const BufferID ScreenCommandsBufferId = BufferID.TerminalOsRuntime_ScreenCommandsBufferId;
        private const BufferID GlyphUvsBufferId = BufferID.TerminalOsRuntime_GlyphUvsBufferId;
        private const BufferID TerminalPositionsBufferId = BufferID.TerminalOsRuntime_TerminalPositionsBufferId;
        private const BufferID TerminalForwardsBufferId = BufferID.TerminalOsRuntime_TerminalForwardsBufferId;
        private const BufferID DirtyIndicesBufferId = BufferID.TerminalOsRuntime_DirtyIndicesBufferId;
        private const BufferID TelemetryRingBufferId = BufferID.TerminalOsRuntime_TelemetryRingBufferId;
        private const BufferID MockPowerBufferId = BufferID.TerminalOsRuntime_MockPowerBufferId;
        private const BufferID MockDamageBufferId = BufferID.TerminalOsRuntime_MockDamageBufferId;
        private const BufferID MockPowerStatusBufferId = BufferID.TerminalOsRuntime_MockPowerStatusBufferId;
        private const BufferID ButtonAabbBufferId = BufferID.TerminalOsRuntime_ButtonAabbBufferId;
        private const BufferID PanelInstancesBufferId = BufferID.TerminalOsRuntime_PanelInstancesBufferId;
        private const BufferID TerminalClickScratchBufferId = BufferID.TerminalOsRuntime_TerminalClickScratchBufferId;
        private const BufferID TerminalPlanesBufferId = BufferID.TerminalOsRuntime_TerminalPlanesBufferId;
        private const BufferID GazeRayBufferId = BufferID.TerminalOsRuntime_GazeRayBufferId;
        private const BufferID TerminalInteractionsBufferId = BufferID.TerminalOsRuntime_TerminalInteractionsBufferId;
        private const BufferID DecryptionPuzzlesBufferId = BufferID.TerminalDecryptionPuzzles;
        private const BufferID DecryptionTerminalsBufferId = BufferID.TerminalDecryptionTerminals;
        private const BufferID DecryptionKnobInputBufferId = BufferID.TerminalDecryptionKnobInput;
        private const BufferID DecryptionTelemetryRingBufferId = BufferID.TerminalDecryptionTelemetryRing;
        private static TerminalOsRuntime s_activeRuntime0;
        private static TerminalOsRuntime s_activeRuntime1;
        private static TerminalOsRuntime s_activeRuntime2;
        private static TerminalOsRuntime s_activeRuntime3;
        private static int s_activeRuntimeCount;
        private static TerminalStateDTO s_invalidTerminalStateRef;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimesForSubsystemRegistration()
        {
            s_activeRuntime0 = null;
            s_activeRuntime1 = null;
            s_activeRuntime2 = null;
            s_activeRuntime3 = null;
            s_activeRuntimeCount = 0;
        }

        private static readonly int TerminalTextureArrayId = Shader.PropertyToID("_TerminalTextureArray");
        private static readonly int TerminalPanelInstancesId = Shader.PropertyToID("_TerminalPanelInstances");
        private static readonly int TerminalStatesId = Shader.PropertyToID("_TerminalStates");
        private static readonly int ScreenCommandsId = Shader.PropertyToID("_ScreenCommands");
        private static readonly int DirtyTerminalIndicesId = Shader.PropertyToID("_DirtyTerminalIndices");
        private static readonly int GlyphUvsId = Shader.PropertyToID("_GlyphUvs");
        private static readonly int FontSdfAtlasId = Shader.PropertyToID("_FontSdfAtlas");
        private static readonly int FontAtlasReadyId = Shader.PropertyToID("_FontAtlasReady");
        private static readonly int TerminalResolutionXId = Shader.PropertyToID("_TerminalResolutionX");
        private static readonly int TerminalResolutionYId = Shader.PropertyToID("_TerminalResolutionY");
        private static readonly int DirtyTerminalCountId = Shader.PropertyToID("_DirtyTerminalCount");
        private static readonly int TimeSeedId = Shader.PropertyToID("_TimeSeed");
        private static readonly int HectonDiegeticGlitchQualityWeightId = Shader.PropertyToID("_HectonDiegeticGlitchQualityWeight");
        private static readonly int HectonDecryptionNoiseDensityId = Shader.PropertyToID("_HectonDecryptionNoiseDensity");
        private static readonly int HectonTerminalInstancedModeId = Shader.PropertyToID("_HectonTerminalInstancedMode");
        private static readonly int TerminalOsStyleColorId = Shader.PropertyToID("_TerminalOsStyleColor");
        private static readonly int TerminalOsStyleBlendId = Shader.PropertyToID("_TerminalOsStyleBlend");
        private static readonly int TerminalOsStyleFlagsId = Shader.PropertyToID("_TerminalOsStyleFlags");
        private static readonly int GlobalDecryptionPuzzlesId = Shader.PropertyToID("_GlobalDecryptionPuzzles");
        private static readonly int GlobalDecryptionPuzzleCountId = Shader.PropertyToID("_GlobalDecryptionPuzzleCount");
        private static readonly int TerminalScreenAlbedoAtlasId = Shader.PropertyToID("_TerminalScreenAlbedoAtlas");
        private static readonly int TerminalScreenProjectionLutId = Shader.PropertyToID("_TerminalScreenProjectionLut");
        private static readonly int TerminalScreenPackedMraoId = Shader.PropertyToID("_TerminalScreenPackedMrao");
        private static readonly int TerminalScreenBakedProjectionReadyId = Shader.PropertyToID("_TerminalScreenBakedProjectionReady");
        private static readonly int TerminalScreenBakedProjectionWeightId = Shader.PropertyToID("_TerminalScreenBakedProjectionWeight");
        private static readonly int TerminalScreenBurnInWeightId = Shader.PropertyToID("_TerminalScreenBurnInWeight");
        private static readonly int TerminalScreenGlassWeightId = Shader.PropertyToID("_TerminalScreenGlassWeight");

        /// <summary>
        /// Canonical location of the terminal blit kernel. Unlike the other GPU owners in this project this
        /// component had no resolve at all, so terminalBlitCompute was null in the editor AND in builds and
        /// the compute blit path never ran anywhere - TryPrepareTerminalBlit just returned false forever.
        /// </summary>
        private const string TerminalBlitComputeAssetPath = "Assets/_Project/Art/Shaders/TerminalBlit.compute";

        [Header("GPU")]
        [SerializeField] private ComputeShader terminalBlitCompute;
        [SerializeField] private Texture2D fontSdfAtlas;
        [SerializeField] private Material terminalArrayMaterial;
        [SerializeField] private Mesh terminalPanelMesh;

        [Header("Baked CRT Projection 1721")]
        [SerializeField] private bool bakedCrtProjectionEnabled = true;
        [SerializeField] private Texture2D bakedScreenAlbedoAtlas;
        [SerializeField] private Texture2D bakedScreenProjectionLut;
        [SerializeField] private Texture2D bakedScreenPackedMrao;
        [SerializeField, Range(0f, 1f)] private float bakedScreenProjectionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float bakedScreenBurnInWeight = 0.68f;
        [SerializeField, Range(0f, 1f)] private float bakedScreenGlassWeight = 0.42f;

        [Header("Scene Binding")]
        [SerializeField] private Camera attentionCameraOverride;
        [SerializeField] private Renderer[] terminalRenderers;
        [SerializeField] private Transform[] terminalTransforms;
        [SerializeField] private bool drawPanelsInstanced = true;

        [Header("Cold Data")]
        [SerializeField] private bool mockGeneratorEnabled = true;
#if UNITY_EDITOR
        [SerializeField] private string layoutCsvRelativePath = "Assets/_SourceData/UI/TerminalOS/terminal_ui_layouts.csv";
#endif

        [Header("Interaction Solver")]
        [SerializeField, Range(0.5f, 30f)] private float interactionMaxDistanceMeters = 10f;
        [SerializeField, Range(-0.5f, 0.95f)] private float interactionViewConeCos = -0.05f;
        [SerializeField, Range(0f, 1f)] private float hologramDistortionIntensity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float minimumQualityWeight;
        [SerializeField] private bool drawTerminalDebugGizmos;

        [Header("Decryption")]
        [SerializeField] private bool decryptionEnabled = true;
        [SerializeField, Range(0.1f, 12f)] private float decryptionBaseFrequency = 3.25f;
        [SerializeField, Range(0.001f, 0.2f)] private float decryptionSnapTolerance01 = 0.02f;
        [SerializeField, Range(0f, 2f)] private float decryptionNoiseDensity = 1f;
        [SerializeField, Range(-1f, 1f)] private float decryptionGlobalQualityOverride = -1f;
        [SerializeField, Range(0.05f, 1f)] private float decryptionFrequencyWeight = 0.22f;
        [SerializeField, Range(0.01f, 0.5f)] private float decryptionPhaseWeight = 0.08f;
        [SerializeField, Range(0.001f, 0.5f)] private float decryptionFrequencySensitivity = 0.035f;
        [SerializeField, Range(0.001f, 0.5f)] private float decryptionPhaseSensitivity = 0.08f;
#if UNITY_EDITOR
        [SerializeField] private string decryptionCsvRelativePath = "Assets/_SourceData/UI/TerminalOS/decryption_puzzles.csv";
#endif

        private IDataVault _vault;
        private VaultGenerationHandle<TerminalStateDTO> _terminalStatesHandle;
        private VaultGenerationHandle<ScreenCommandDTO> _screenCommandsHandle;
        private VaultGenerationHandle<float4> _glyphUvsHandle;
        private VaultGenerationHandle<float4> _terminalPositionsHandle;
        private VaultGenerationHandle<float4> _terminalForwardHandle;
        private VaultGenerationHandle<int> _dirtyIndicesHandle;
        private VaultGenerationHandle<TerminalTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<MockPowerStateSignal> _mockPowerSignalHandle;
        private VaultGenerationHandle<MockDamageScalarSignal> _mockDamageSignalHandle;
        private VaultGenerationHandle<MockPowerStatusSignal> _mockPowerStatusSignalHandle;
        private VaultGenerationHandle<ButtonAABBDTO> _buttonAabbHandle;
        private VaultGenerationHandle<TerminalPanelInstanceDTO> _panelInstancesHandle;
        private VaultGenerationHandle<TerminalClickSignal> _clickScratchHandle;
        private VaultGenerationHandle<TerminalPlaneDTO> _terminalPlanesHandle;
        private VaultGenerationHandle<GazeRayDTO> _gazeRayHandle;
        private VaultGenerationHandle<TerminalInteractionDTO> _terminalInteractionsHandle;
        private VaultGenerationHandle<DecryptionPuzzleDTO> _decryptionPuzzlesHandle;
        private VaultGenerationHandle<DecryptionTerminalDTO> _decryptionTerminalsHandle;
        private VaultGenerationHandle<DecryptionKnobInputDTO> _decryptionKnobInputHandle;
        private VaultGenerationHandle<DecryptionTelemetryEntry> _decryptionTelemetryRingHandle;

        private GraphicsBuffer _stateBuffer0;
        private GraphicsBuffer _stateBuffer1;
        private GraphicsBuffer _screenCommandBuffer;
        private GraphicsBuffer _glyphUvBuffer;
        private GraphicsBuffer _dirtyIndexBufferA;
        private GraphicsBuffer _dirtyIndexBufferB;
        private GraphicsBuffer _activeDirtyIndexBuffer;
        private GraphicsBuffer _panelInstanceBuffer;
        private GraphicsBuffer _decryptionPuzzleBuffer0;
        private GraphicsBuffer _decryptionPuzzleBuffer1;
        private GraphicsBuffer _decryptionPuzzleBuffer;
        private RenderTexture _terminalTextureArray;
        private Camera _attentionCameraCache;
        private Bounds _panelRenderBounds;

        private JobHandle _formatHandle;
        private JobHandle _clickResolveHandle;
        private JobHandle _terminalInteractionHandle;
        private JobHandle _decryptionHandle;
        private bool _formatScheduled;
        private bool _clickResolveScheduled;
        private bool _terminalInteractionScheduled;
        private bool _decryptionScheduled;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _nativeResourcesReady;
        private bool _graphicsResourcesReady;
        private bool _pendingGraphicsResourceRebuild;
        private bool _layoutUploadDirty;
        private bool _glyphUploadDirty;
        private int _dirtyIndexUploadBufferIndex;
        private bool _bindingsDirty;
        private bool _bakedCrtBindingDirty = true;
        private bool _bakedCrtProjectionReady;
        private int _bakedCrtBindingHash;
        private bool _panelInstanceUploadDirty;
        private bool _decryptionBufferUploadDirty;
        private bool _blackBoxDumped;
        private bool _terminalBlackBoxDumpQueued;
        private uint _queuedTerminalBlackBoxFaultFlags;
        private bool _decryptionBlackBoxDumpQueued;
        private uint _queuedDecryptionBlackBoxFaultFlags;
        private bool _decryptionBlackBoxDumped;
        private bool _decryptionDumpBackpressureReported;
        private bool _decryptionDumpWriterBootAttempted;
        private bool _inputPressedLastFrame;
        private bool _supportsTerminalComputeCold;
        private int _terminalCount;
        private int _buttonCount;
        private int _writeBufferIndex;
        private int _decryptionPuzzleWriteBufferIndex;
        private int _decryptionPuzzleUploadCount;
        private int _textureResolution;
        private int _blitKernel = -1;
        private int _groupsX;
        private int _groupsY;
        private int _threadsX;
        private int _threadsY;
        private int _telemetryCursor;
        private int _decryptionTelemetryCursor;
#if UNITY_EDITOR
        private int _csvProbeFrame;
        private int _decryptionCsvProbeFrame;
#endif
        private int _nextQualityRefreshFrame;
        private int _nextCameraResolveFrame;
        private int _nextNativeResourceRetryFrame;
        private int _nextLateFrameRegisterRetryFrame;
        private int _lastDirtyCount;
        private int _lastDispatchedCount;
        private int _framesBetweenUpdates = 1;
        private int _decryptionFramesBetweenEvaluations = 1;
        private int _lastDecryptionTelemetryFrame = -1;
        private int _lastEvaluatedTerminalCount;
        private uint _lastHoveredTerminalHash;
        private uint _lastFaultFlags;
        private uint _lastDecryptionFaultFlags;
        private long _interactionScheduleTicks;
        private long _decryptionScheduleTicks;
        private uint _decryptionScheduleFrame;
        private float _lastFormatMainThreadMilliseconds;
        private float _lastUploadMicroseconds;
        private float _lastDispatchMicroseconds;
        private float _lastIntersectionMicroseconds;
        private float _lastDecryptionBurstMicroseconds;
        private float _lastPower01;
        private float _lastDamage01;
        private float _lastDiegeticGlitchIntensity;
        private float _lastPanelInstanceQualityWeight = -1f;
        private float _lastPanelInstanceGlitchIntensity = -1f;
        private float _globalQualityWeight = 1f;
        private float _dialogueChoiceVisualBlend = -1f;
        private float _lastUploadedDialogueChoiceVisualBlend = -1f;
        private ushort _lastUploadedDialogueChoiceFlags = ushort.MaxValue;
        private AbsoluteUniversePosition _cachedRuntimeOriginAup;
        private IInputService _input;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private SaveManager _cachedSaveManager;
        private bool _registeredHotSwapListener;
#if UNITY_EDITOR
        private string _csvFullPath;
        private string _decryptionCsvFullPath;
#endif
        private string _dumpFullPath;
        private string _dumpMirrorFullPath;
        private string _decryptionDumpFullPath;
        private DecryptionBlackBoxDumpWriter _decryptionDumpWriter;
#if UNITY_EDITOR
        private DateTime _csvLastWriteUtc;
        private DateTime _decryptionCsvLastWriteUtc;
#endif
        private bool _coldPathsReady;

        public void LateFrameTick()
        {
            if (!_nativeResourcesReady)
                return;

            int ownerFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            bool hasSimulationFrame = TryResolveSimulationFrame(out int simulationFrame);
            RefreshRuntimeOriginSnapshotForOwner();
            RefreshScalabilityPolicy();
            TryFinalizeClickResolveJob();
            TryFinalizeTerminalInteractionJob();
            TryFinalizeDecryptionJob();
            UpdateDialogueChoiceVisualSync();
            uint terminalPreviewFaultFlags = ConsumeAppliedLoreTerminalPreviewSignals();
            RefreshBakedCrtBindingDirtyState();

            bool visualPipelineBlocked = false;
            if (_formatScheduled)
            {
                if (!TryFinalizeCompletedJob(ref _formatHandle))
                {
                    visualPipelineBlocked = true;
                }
                else
                {
                    _formatScheduled = false;
                }
            }
            if (!visualPipelineBlocked && !FlushPendingGraphicsResourceRebuild())
                visualPipelineBlocked = true;

            int dirtyCount = 0;
            int dispatchedCount = 0;
            if (!visualPipelineBlocked)
            {
                dirtyCount = BuildDirtyList(ownerFrame);
                if (dirtyCount > 0)
                {
                    bool uploaded = UploadDirtyPayloads(dirtyCount);
                    if (uploaded)
                        dispatchedCount = DispatchDirtyScreens(dirtyCount, ownerFrame);
                    if (dispatchedCount == dirtyCount)
                        ClearDirtyFlags(dirtyCount);
                }
            }

            _lastDirtyCount = dirtyCount;
            _lastDispatchedCount = dispatchedCount;
            if (hasSimulationFrame)
            {
                if (!_decryptionScheduled && _lastDecryptionTelemetryFrame != simulationFrame)
                    RecordDecryptionTelemetry(simulationFrame, _lastDecryptionFaultFlags);
                TryScheduleDecryptionPipeline(simulationFrame);
            }
            if (!visualPipelineBlocked)
                UpdatePanelInstancesIfNeeded();
            if (!visualPipelineBlocked && _bindingsDirty)
                BindTerminalRenderers();
            TryScheduleTerminalInteractionPipeline(ownerFrame, !visualPipelineBlocked);
            TryScheduleClickResolveJob();
            if (!visualPipelineBlocked)
                TryScheduleFormatJob(ownerFrame);
            RenderInstancedPanels();
            uint faultFlags = _lastFaultFlags | terminalPreviewFaultFlags;
            if (_terminalCount >= TerminalOsConstants.ActiveTargetTerminals && _lastFormatMainThreadMilliseconds > 0.5f)
                faultFlags |= FaultFormatBudget;
            if (faultFlags != 0u)
                QueueTerminalBlackBoxDump(faultFlags);
            RecordTelemetry(ownerFrame, dirtyCount, dispatchedCount, faultFlags);
            RecordTerminalInputTelemetry(ownerFrame, faultFlags);
        }

        public bool QueueClick(in TerminalClickSignal signal)
        {
            EnsureRuntimeReady();
            if (!math.all(math.isfinite(signal.LocalUv)))
                return false;

            return SignalBus<TerminalClickSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_TerminalOsRuntime);
        }

        public void SetAttentionCamera(Camera camera)
        {
            attentionCameraOverride = camera;
            _attentionCameraCache = camera;
        }

        public bool TryDequeueCommand(out TerminalCommandSignal command)
        {
            command = default;
            if (_clickResolveScheduled)
                return false;

            return SignalBus<TerminalCommandSignal>.TryConsumeFrame(out command);
        }

        public bool TryDequeueTerminalUnlock(out TerminalUnlockedSignal signal)
        {
            signal = default;
            return SignalBus<TerminalUnlockedSignal>.TryConsumeFrame(out signal);
        }

        public RenderTexture GetTerminalTextureArray()
        {
            return _terminalTextureArray;
        }

        public int GetTerminalCount()
        {
            return _terminalCount;
        }

        public int GetFramesBetweenUpdates()
        {
            return _framesBetweenUpdates;
        }

        public int GetLastEvaluatedTerminalCount()
        {
            return _lastEvaluatedTerminalCount;
        }

        public uint GetLastHoveredTerminalHash()
        {
            return _lastHoveredTerminalHash;
        }

        public float GetGlobalQualityWeight()
        {
            return _globalQualityWeight;
        }

        public float GetLastIntersectionMicroseconds()
        {
            return _lastIntersectionMicroseconds;
        }

        public bool IsDecryptionJobScheduled()
        {
            return _decryptionScheduled;
        }

        public float GetLastDecryptionBurstMicroseconds()
        {
            return _lastDecryptionBurstMicroseconds;
        }

        public float GetDecryptionBaseFrequency()
        {
            return decryptionBaseFrequency;
        }

        public float GetDecryptionSnapTolerance01()
        {
            return decryptionSnapTolerance01;
        }

        public float GetDecryptionNoiseDensity()
        {
            return decryptionNoiseDensity;
        }

        public float GetDecryptionQualityOverride()
        {
            return decryptionGlobalQualityOverride;
        }

        public bool TryGetTerminalInteractionCopy(int index, out TerminalInteractionDTO interaction)
        {
            if (!TryReadVaultBuffer(in _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO>.ReadOnly interactions) ||
                index < 0 ||
                index >= math.min(_terminalCount, interactions.Length))
            {
                interaction = default;
                return false;
            }

            interaction = interactions[index];
            return true;
        }

        public bool TryGetDecryptionPuzzleCopy(int index, out DecryptionPuzzleDTO puzzle)
        {
            if (_decryptionScheduled ||
                !TryReadVaultBuffer(in _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO>.ReadOnly puzzles) ||
                index < 0 ||
                index >= math.min(_terminalCount, puzzles.Length))
            {
                puzzle = default;
                return false;
            }

            puzzle = puzzles[index];
            return true;
        }

        public bool TryGetLatestDecryptionTelemetryCopy(out DecryptionTelemetryEntry entry)
        {
            if (!TryReadVaultBuffer(in _decryptionTelemetryRingHandle, out NativeArray<DecryptionTelemetryEntry>.ReadOnly telemetryRing) ||
                telemetryRing.Length == 0)
            {
                entry = default;
                return false;
            }

            int index = _decryptionTelemetryCursor - 1;
            if (index < 0)
                index += telemetryRing.Length;

            entry = telemetryRing[index];
            return true;
        }

        public void ApplyDecryptionEditorTuning(float frequencyWeight, float phaseWeight, float frequencySensitivity, float phaseSensitivity)
        {
            ApplyDecryptionEditorTuning(
                frequencyWeight,
                phaseWeight,
                frequencySensitivity,
                phaseSensitivity,
                decryptionBaseFrequency,
                decryptionSnapTolerance01,
                decryptionNoiseDensity,
                decryptionGlobalQualityOverride);
        }

        public bool ApplyDecryptionEditorTuning(
            float frequencyWeight,
            float phaseWeight,
            float frequencySensitivity,
            float phaseSensitivity,
            float baseFrequency,
            float snapTolerance01,
            float noiseDensity,
            float globalQualityOverride)
        {
            decryptionFrequencyWeight = math.clamp(math.isfinite(frequencyWeight) ? frequencyWeight : 0.22f, 0.05f, 1f);
            decryptionPhaseWeight = math.clamp(math.isfinite(phaseWeight) ? phaseWeight : 0.08f, 0.01f, 0.5f);
            decryptionFrequencySensitivity = math.clamp(math.isfinite(frequencySensitivity) ? frequencySensitivity : 0.035f, 0.001f, 0.5f);
            decryptionPhaseSensitivity = math.clamp(math.isfinite(phaseSensitivity) ? phaseSensitivity : 0.08f, 0.001f, 0.5f);
            float previousBaseFrequency = decryptionBaseFrequency;
            decryptionBaseFrequency = math.clamp(math.isfinite(baseFrequency) ? baseFrequency : 3.25f, 0.1f, 12f);
            decryptionSnapTolerance01 = math.clamp(math.isfinite(snapTolerance01) ? snapTolerance01 : 0.02f, 0.001f, 0.2f);
            decryptionNoiseDensity = math.clamp(math.isfinite(noiseDensity) ? noiseDensity : 1f, 0f, 2f);
            decryptionGlobalQualityOverride = math.isfinite(globalQualityOverride)
                ? math.clamp(globalQualityOverride, -1f, 1f)
                : -1f;
            _bindingsDirty = true;
            _nextQualityRefreshFrame = 0;
            RefreshScalabilityPolicy();

            if (math.abs(previousBaseFrequency - decryptionBaseFrequency) <= 0.0001f)
                return true;

            return TryApplyDecryptionBaseFrequency(decryptionBaseFrequency);
        }

        public bool TrySetDecryptionTarget(int index, float targetFrequency, float targetPhase)
        {
            if (_decryptionScheduled ||
                !TryOpenVaultBuffer(ref _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO> puzzles) ||
                index < 0 ||
                index >= math.min(_terminalCount, puzzles.Length))
            {
                return false;
            }

            DecryptionPuzzleDTO puzzle = puzzles[index];
            puzzle.TargetFrequency = math.clamp(math.isfinite(targetFrequency) ? targetFrequency : puzzle.TargetFrequency, 0.1f, 12f);
            puzzle.TargetPhase = WrapPhase(math.isfinite(targetPhase) ? targetPhase : puzzle.TargetPhase);
            puzzle.Flags |= TerminalOsConstants.DecryptionFlagActive | TerminalOsConstants.DecryptionFlagInitialized;
            puzzle.Flags &= ~TerminalOsConstants.DecryptionFlagSolved;
            puzzles[index] = puzzle;
            _decryptionBufferUploadDirty = true;
            _bindingsDirty = true;
            return true;
        }

        private bool TryApplyDecryptionBaseFrequency(float baseFrequency)
        {
            if (_decryptionScheduled ||
                !TryOpenVaultBuffer(ref _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO> puzzles))
            {
                return false;
            }

            int count = math.min(_terminalCount, puzzles.Length);
            float safeBaseFrequency = math.clamp(math.isfinite(baseFrequency) ? baseFrequency : 3.25f, 0.1f, 12f);
            for (int i = 0; i < count; i++)
            {
                DecryptionPuzzleDTO puzzle = puzzles[i];
                if ((puzzle.Flags & TerminalOsConstants.DecryptionFlagActive) == 0u)
                    continue;

                puzzle.PlayerFrequency = safeBaseFrequency;
                puzzle.Flags &= ~(TerminalOsConstants.DecryptionFlagSolved | TerminalOsConstants.DecryptionHoldFrameMask);
                puzzles[i] = puzzle;
            }

            _decryptionBufferUploadDirty = true;
            _bindingsDirty = true;
            return true;
        }

        public void ApplyEditorTuning(float maxDistanceMeters, float viewConeCos, float distortionIntensity, float minQuality)
        {
            interactionMaxDistanceMeters = math.clamp(math.isfinite(maxDistanceMeters) ? maxDistanceMeters : 10f, 0.5f, 30f);
            interactionViewConeCos = math.clamp(math.isfinite(viewConeCos) ? viewConeCos : -0.05f, -0.5f, 0.95f);
            hologramDistortionIntensity = math.saturate(math.isfinite(distortionIntensity) ? distortionIntensity : 0.35f);
            minimumQualityWeight = math.saturate(math.isfinite(minQuality) ? minQuality : 0f);
            _nextQualityRefreshFrame = 0;
            RefreshScalabilityPolicy();
        }

        public bool TryGetTerminalStateCopy(int index, out TerminalStateDTO state)
        {
            if (!TryReadVaultBuffer(in _terminalStatesHandle, out NativeArray<TerminalStateDTO>.ReadOnly terminalStates) ||
                index < 0 ||
                index >= math.min(_terminalCount, terminalStates.Length))
            {
                state = default;
                return false;
            }

            state = terminalStates[index];
            return true;
        }

        public bool TryGetScreenCommandCopy(int index, out ScreenCommandDTO command)
        {
            if (!TryReadVaultBuffer(in _screenCommandsHandle, out NativeArray<ScreenCommandDTO>.ReadOnly screenCommands) ||
                index < 0 ||
                index >= math.min(_terminalCount, screenCommands.Length))
            {
                command = default;
                return false;
            }

            command = screenCommands[index];
            return true;
        }

        private ref TerminalStateDTO OpenTerminalStateRefForOwner(int index)
        {
            if (!TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) ||
                index < 0 ||
                index >= math.min(_terminalCount, terminalStates.Length))
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                s_invalidTerminalStateRef = default;
                return ref s_invalidTerminalStateRef;
            }

            return ref OpenTerminalStateRefForOwnerUnchecked(index);
        }

        public bool TrySetTerminalMockState(int index, float value1, float value2)
        {
            if (!TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) ||
                index < 0 ||
                index >= math.min(_terminalCount, terminalStates.Length))
            {
                return false;
            }

            TerminalStateDTO state = terminalStates[index];
            state.Value1 = math.saturate(math.isfinite(value1) ? value1 : 0f);
            state.Value2 = math.saturate(math.isfinite(value2) ? value2 : 0f);
            state.IsDirty = 1;
            terminalStates[index] = state;
            ForceDirty(index);
            return true;
        }

        private bool ApplyTerminalAppliedLoreLine(
            int index,
            uint expectedTerminalHash,
            uint packetHash,
            uint localeHash,
            H8AppliedLoreSurface surface = H8AppliedLoreSurface.Title)
        {
            if (packetHash == 0u ||
                !TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) ||
                !TryResolveTerminalPreviewIndex(
                    terminalStates,
                    index,
                    expectedTerminalHash,
                    out int resolvedIndex) ||
                !TryGetTerminalPreviewAppliedLoreUtf8(packetHash, localeHash, surface, out ReadOnlySpan<byte> utf8Bytes))
            {
                return false;
            }

            TerminalStateDTO state = terminalStates[resolvedIndex];
            WriteUtf8PreviewLine(ref state.TextLine, utf8Bytes);
            state.IsDirty = 1;
            terminalStates[resolvedIndex] = state;
            ForceDirty(resolvedIndex);
            return true;
        }

        private uint ConsumeAppliedLoreTerminalPreviewSignals()
        {
            uint faultFlags = SignalBus<AppliedLoreTerminalPreviewSignal>.DroppedLastFlush > 0
                ? FaultAppliedLorePreviewDrop
                : 0u;
            ReadOnlySpan<AppliedLoreTerminalPreviewSignal> signals =
                SignalBus<AppliedLoreTerminalPreviewSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                AppliedLoreTerminalPreviewSignal signal = signals[i];
                if (signal.PacketHash == 0u)
                {
                    faultFlags |= FaultAppliedLorePreviewMiss;
                    continue;
                }

                if (!ApplyTerminalAppliedLoreLine(
                    signal.TerminalIndex,
                    signal.TerminalHash,
                    signal.PacketHash,
                    signal.LocaleHash,
                    ResolveAppliedLorePreviewSurface(signal.Surface)))
                {
                    faultFlags |= FaultAppliedLorePreviewMiss;
                }
            }

            return faultFlags;
        }

        private bool TryResolveTerminalPreviewIndex(
            NativeArray<TerminalStateDTO> terminalStates,
            int index,
            uint expectedTerminalHash,
            out int resolvedIndex)
        {
            resolvedIndex = -1;
            int count = math.min(_terminalCount, terminalStates.Length);
            bool indexValid = (uint)index < (uint)count;
            if (expectedTerminalHash == 0u)
            {
                if (!indexValid)
                    return false;

                resolvedIndex = index;
                return true;
            }

            if (indexValid && ResolveTerminalHash(terminalStates, index) == expectedTerminalHash)
            {
                resolvedIndex = index;
                return true;
            }

            for (int i = 0; i < count; i++)
            {
                if (ResolveTerminalHash(terminalStates, i) != expectedTerminalHash)
                    continue;

                resolvedIndex = i;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveTerminalHash(NativeArray<TerminalStateDTO> terminalStates, int index)
        {
            uint terminalHash = terminalStates[index].TerminalHash;
            return terminalHash != 0u ? terminalHash : TerminalOsHash.HashIndex(index);
        }

        private static H8AppliedLoreSurface ResolveAppliedLorePreviewSurface(byte surface)
        {
            switch ((H8AppliedLoreSurface)surface)
            {
                case H8AppliedLoreSurface.Title:
                case H8AppliedLoreSurface.Scanner:
                case H8AppliedLoreSurface.Terminal:
                case H8AppliedLoreSurface.Audio:
                case H8AppliedLoreSurface.InGameWiki:
                case H8AppliedLoreSurface.ExternalSite:
                case H8AppliedLoreSurface.FieldNote:
                    return (H8AppliedLoreSurface)surface;
                default:
                    return H8AppliedLoreSurface.Terminal;
            }
        }

        private static bool TryGetTerminalPreviewAppliedLoreUtf8(
            uint packetHash,
            uint localeHash,
            H8AppliedLoreSurface surface,
            out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = ReadOnlySpan<byte>.Empty;
            uint resolvedLocale = localeHash != 0u ? localeHash : H8AppliedLoreRuntime.DefaultLocaleHash;
            bool resolvedIsDefault = resolvedLocale == H8AppliedLoreRuntime.DefaultLocaleHash;
            if (H8AppliedLoreRuntime.TryGetUtf8(packetHash, resolvedLocale, surface, out utf8Bytes))
            {
                if (resolvedIsDefault || IsTerminalPreviewAsciiCompatible(utf8Bytes))
                    return true;
            }
            else if (resolvedIsDefault)
            {
                return false;
            }

            if (H8AppliedLoreRuntime.TryGetUtf8(
                    packetHash,
                    H8AppliedLoreRuntime.DefaultLocaleHash,
                    surface,
                    out ReadOnlySpan<byte> fallbackUtf8) &&
                fallbackUtf8.Length > 0)
            {
                utf8Bytes = fallbackUtf8;
                return true;
            }

            utf8Bytes = ReadOnlySpan<byte>.Empty;
            return false;
        }

        public void SetScreenCommand(int index, float2 position, float scale)
        {
            if (!TryOpenVaultBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands) ||
                index < 0 ||
                index >= math.min(_terminalCount, screenCommands.Length))
                return;

            ScreenCommandDTO command = screenCommands[index];
            command.Position = SanitizeUv01(position);
            command.Scale = SanitizeScale(scale);
            screenCommands[index] = command;
            _layoutUploadDirty = true;
            ForceDirty(index);
        }

        public void SetTerminalAvailability(int index, float power01, float submerged01)
        {
            if (!TryOpenVaultBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes) ||
                index < 0 ||
                index >= math.min(_terminalCount, terminalPlanes.Length))
                return;

            TerminalPlaneDTO plane = terminalPlanes[index];
            float safePower = math.saturate(math.isfinite(power01) ? power01 : 0f);
            float safeSubmerged = math.saturate(math.isfinite(submerged01) ? submerged01 : 0f);
            uint flags = plane.Flags | TerminalOsConstants.PlaneFlagActive;
            if (safePower > 0.001f)
                flags |= TerminalOsConstants.PlaneFlagPowered;
            else
                flags &= ~TerminalOsConstants.PlaneFlagPowered;

            if (safeSubmerged > 0.5f)
                flags |= TerminalOsConstants.PlaneFlagSubmerged;
            else
                flags &= ~TerminalOsConstants.PlaneFlagSubmerged;

            plane.Power01 = safePower;
            plane.Submerged01 = safeSubmerged;
            plane.Flags = flags;
            terminalPlanes[index] = plane;
            RefreshDecryptionTerminalFromPlane(index, in plane);
            if (TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) &&
                index < terminalStates.Length)
            {
                TerminalStateDTO state = terminalStates[index];
                state.Value1 = safePower;
                state.BackgroundColor = safePower > 0.001f && safeSubmerged <= 0.5f
                    ? (state.BackgroundColor == 0u ? 0x00061418u : state.BackgroundColor)
                    : 0u;
                state.IsDirty = 1;
                terminalStates[index] = state;
            }
            ForceDirty(index);
        }

        internal bool TryWritePowerLevelTokenLine(
            ReadOnlySpan<byte> templateBytes,
            int terminalIndex,
            out CharBufferPool.Lease lease,
            out int length)
        {
            lease = default;
            length = 0;
            if (!TryGetTerminalStateCopy(terminalIndex, out TerminalStateDTO state) ||
                !CharBufferPool.TryAcquire(out lease))
            {
                return false;
            }

            Span<char> destination = lease.Buffer.AsSpan();
            int powerPercent = math.clamp((int)math.round(math.saturate(state.Value1) * 100f), 0, 100);
            for (int i = 0; i < templateBytes.Length && length < destination.Length; i++)
            {
                if (MatchesPowerLevelToken(templateBytes, i))
                {
                    if (!powerPercent.TryFormat(destination.Slice(length), out int written))
                    {
                        CharBufferPool.Release(in lease);
                        lease = default;
                        length = 0;
                        return false;
                    }

                    length += written;
                    if (length < destination.Length)
                        destination[length++] = '%';
                    i += 12;
                    continue;
                }

                byte value = templateBytes[i];
                if (value == 0)
                    break;
                if (value == (byte)'\r')
                    continue;
                destination[length++] = value < 128 ? (char)value : '?';
            }

            return true;
        }

        private void ForceDirty(int index)
        {
            if (!TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) ||
                index < 0 ||
                index >= math.min(_terminalCount, terminalStates.Length))
                return;

            TerminalStateDTO state = terminalStates[index];
            state.IsDirty = 1;
            terminalStates[index] = state;
        }

        private static void WriteUtf8PreviewLine(ref FixedString32Bytes line, ReadOnlySpan<byte> utf8Bytes)
        {
            line.Clear();
            for (int i = 0; i < utf8Bytes.Length && line.Length < TerminalOsConstants.MaxFixedStringPayloadBytes; i++)
            {
                byte value = utf8Bytes[i];
                if (value == 0u)
                    break;

                if (value == (byte)'\r' || value == (byte)'\n' || value == (byte)'\t')
                {
                    line.Add((byte)' ');
                    continue;
                }

                if (value >= 0x20 && value < 0x7F)
                {
                    line.Add(value);
                    continue;
                }

                if ((value & 0xC0) == 0x80)
                    continue;

                line.Add((byte)'?');
            }
        }

        private static bool IsTerminalPreviewAsciiCompatible(ReadOnlySpan<byte> utf8Bytes)
        {
            for (int i = 0; i < utf8Bytes.Length; i++)
            {
                byte value = utf8Bytes[i];
                if (value == 0u)
                    return true;
                if (value == (byte)'\r' || value == (byte)'\n' || value == (byte)'\t')
                    continue;
                if (value < 0x20 || value >= 0x7F)
                    return false;
            }

            return utf8Bytes.Length > 0;
        }

        private void ForceAllDirty()
        {
            if (!TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates))
                return;

            int count = math.min(_terminalCount, terminalStates.Length);
            for (int i = 0; i < count; i++)
            {
                TerminalStateDTO state = terminalStates[i];
                state.IsDirty = 1;
                terminalStates[i] = state;
            }
        }

        public static void ApplyDiegeticGlitchToActiveRuntimes(float intensity01)
        {
            float safeIntensity = math.saturate(math.isfinite(intensity01) ? intensity01 : 0f);
            int count = math.min(s_activeRuntimeCount, ActiveRuntimeCapacity);
            for (int i = 0; i < count; i++)
            {
                TerminalOsRuntime runtime = GetActiveRuntimeSlot(i);
                if (runtime != null && runtime.isActiveAndEnabled)
                    runtime.ApplyDiegeticGlitchIntensity(safeIntensity);
            }
        }

        private void ApplyDiegeticGlitchIntensity(float intensity01)
        {
            if (!TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates))
            {
                _lastDiegeticGlitchIntensity = intensity01;
                _panelInstanceUploadDirty = true;
                _bindingsDirty = true;
                return;
            }

            int count = math.min(_terminalCount, terminalStates.Length);
            for (int i = 0; i < count; i++)
            {
                TerminalStateDTO state = terminalStates[i];
                float current = math.isfinite(state.Value2) ? state.Value2 : 0f;
                float preservedExternal = current <= _lastDiegeticGlitchIntensity + 0.001f
                    ? 0f
                    : current;
                float next = math.saturate(math.max(preservedExternal, intensity01));
                if (math.abs(current - next) > 0.0005f || next > 0.001f)
                {
                    state.Value2 = next;
                    state.IsDirty = 1;
                    terminalStates[i] = state;
                }
            }

            _lastDiegeticGlitchIntensity = intensity01;
            _panelInstanceUploadDirty = true;
            _bindingsDirty = true;
        }

        private void RegisterActiveRuntime()
        {
            for (int i = 0; i < s_activeRuntimeCount; i++)
            {
                if (ReferenceEquals(GetActiveRuntimeSlot(i), this))
                    return;
            }

            if (s_activeRuntimeCount >= ActiveRuntimeCapacity)
                return;

            SetActiveRuntimeSlot(s_activeRuntimeCount++, this);
        }

        private void UnregisterActiveRuntime()
        {
            for (int i = 0; i < s_activeRuntimeCount; i++)
            {
                if (!ReferenceEquals(GetActiveRuntimeSlot(i), this))
                    continue;

                int last = s_activeRuntimeCount - 1;
                SetActiveRuntimeSlot(i, GetActiveRuntimeSlot(last));
                SetActiveRuntimeSlot(last, null);
                s_activeRuntimeCount = math.max(0, last);
                return;
            }
        }

        private static TerminalOsRuntime GetActiveRuntimeSlot(int index)
        {
            switch (index)
            {
                case 0: return s_activeRuntime0;
                case 1: return s_activeRuntime1;
                case 2: return s_activeRuntime2;
                case 3: return s_activeRuntime3;
                default: return null;
            }
        }

        private static void SetActiveRuntimeSlot(int index, TerminalOsRuntime runtime)
        {
            switch (index)
            {
                case 0:
                    s_activeRuntime0 = runtime;
                    break;
                case 1:
                    s_activeRuntime1 = runtime;
                    break;
                case 2:
                    s_activeRuntime2 = runtime;
                    break;
                case 3:
                    s_activeRuntime3 = runtime;
                    break;
            }
        }

        private void Awake()
        {
            _supportsTerminalComputeCold = SystemInfo.supportsComputeShaders;
            EnsureColdPaths();
            CacheRegistryServicesCold();
            ValidateLayouts();
            EnsureRuntimeReady();
        }

        private void OnEnable()
        {
            _supportsTerminalComputeCold = SystemInfo.supportsComputeShaders;
            _nextNativeResourceRetryFrame = 0;
            _nextLateFrameRegisterRetryFrame = 0;
            _bakedCrtBindingDirty = true;
            _bindingsDirty = true;
            EnsureColdPaths();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterSlowTick();
            EnsureRuntimeReady();
            RegisterActiveRuntime();
            TryRegisterLateFrame();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _bakedCrtBindingDirty = true;
            _bindingsDirty = true;

            // Resolve the blit kernel at AUTHOR time and persist it, so the reference is SERIALIZED and
            // therefore ships in a player build, and so any new scene carrying this component wires itself
            // without anyone remembering to drag the asset into the Inspector.
            if (!Application.isPlaying && terminalBlitCompute == null)
            {
                terminalBlitCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(TerminalBlitComputeAssetPath);
                if (terminalBlitCompute != null)
                    EditorUtility.SetDirty(this);
            }
        }
#endif

        private void OnDisable()
        {
            UnregisterActiveRuntime();
            TryUnregisterLateFrame();
            TryUnregisterSlowTick();
            TryUnregisterHotSwapListener();
            CompleteJobsForTeardown();
            FlushQueuedBlackBoxDumps();
            DisposeGraphicsResources();
            DisposeNativeResources();
            DisposeDecryptionDumpWriter();
        }

        private void OnDestroy()
        {
            UnregisterActiveRuntime();
            TryUnregisterLateFrame();
            TryUnregisterSlowTick();
            TryUnregisterHotSwapListener();
            CompleteJobsForTeardown();
            FlushQueuedBlackBoxDumps();
            DisposeGraphicsResources();
            DisposeNativeResources();
            DisposeDecryptionDumpWriter();
        }

        private void EnsureRuntimeReady()
        {
            EnsureColdPaths();
            RefreshScalabilityPolicy();
            EnsureNativeResources();
            EnsureGraphicsResources();
            TryRegisterLateFrame();
        }

        private void EnsureColdPaths()
        {
            if (_attentionCameraCache == null && attentionCameraOverride != null)
                _attentionCameraCache = attentionCameraOverride;

            if (!_coldPathsReady)
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#if UNITY_EDITOR
                _csvFullPath = Path.GetFullPath(Path.Combine(projectRoot, layoutCsvRelativePath));
                _decryptionCsvFullPath = Path.GetFullPath(Path.Combine(projectRoot, decryptionCsvRelativePath));
#endif
                _dumpFullPath = DumpRelativePath;
                _dumpMirrorFullPath = DumpMirrorRelativePath;
                _decryptionDumpFullPath = DecryptionDumpRelativePath;
                EnsureTerminalProjectionColdPaths(projectRoot);
                _coldPathsReady = true;
            }

            EnsureDecryptionDumpWriterCold();
        }

        private void EnsureDecryptionDumpWriterCold()
        {
            if (_decryptionDumpWriter != null ||
                _decryptionDumpWriterBootAttempted ||
                string.IsNullOrEmpty(_decryptionDumpFullPath))
                return;

            _decryptionDumpWriterBootAttempted = true;
            try
            {
                _decryptionDumpWriter = new DecryptionBlackBoxDumpWriter(_decryptionDumpFullPath);
                _decryptionDumpWriter.Start();
            }
            catch (IOException exception)
            {
                _decryptionDumpWriter = null;
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                _decryptionDumpWriter = null;
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (NotSupportedException exception)
            {
                _decryptionDumpWriter = null;
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (ArgumentException exception)
            {
                _decryptionDumpWriter = null;
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (ObjectDisposedException exception)
            {
                _decryptionDumpWriter = null;
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (InvalidOperationException exception)
            {
                _decryptionDumpWriter = null;
                Hecton8.Core.H8Debug.LogException(exception);
            }
        }

        private void CacheRegistryServicesCold()
        {
            _input = GlobalRegistry.Input;
            BindDataVaultForLifecycle(GlobalRegistry.DataVault, _vault);
            _cachedSaveManager = GlobalRegistry.SaveRuntime;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _cachedPlayerContext = playerContext;
            RefreshRuntimeOriginSnapshotForOwner();

            if (attentionCameraOverride == null && playerContext != null && playerContext.PlayerCamera != null)
            {
                _attentionCameraCache = playerContext.PlayerCamera;
            }
        }

        private void RefreshRuntimeOriginSnapshotForOwner()
        {
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            _cachedRuntimeOriginAup = math.all(math.isfinite(origin))
                ? AbsoluteUniversePosition.FromAbsolutePosition(origin)
                : default;
        }

        private void ValidateLayouts()
        {
            _lastFaultFlags &= ~FaultLayoutMismatch;
            if (UnsafeUtility.SizeOf<TerminalStateDTO>() != TerminalOsConstants.TerminalStateStrideBytes ||
                UnsafeUtility.SizeOf<ScreenCommandDTO>() != TerminalOsConstants.ScreenCommandStrideBytes ||
                UnsafeUtility.SizeOf<TerminalInteractionDTO>() != TerminalOsConstants.TerminalInteractionStrideBytes ||
                UnsafeUtility.SizeOf<TerminalPlaneDTO>() != TerminalOsConstants.TerminalPlaneStrideBytes ||
                UnsafeUtility.SizeOf<GazeRayDTO>() != TerminalOsConstants.GazeRayStrideBytes ||
                UnsafeUtility.SizeOf<ButtonAABBDTO>() != TerminalOsConstants.ButtonAabbStrideBytes ||
                UnsafeUtility.SizeOf<TerminalPanelInstanceDTO>() != 80 ||
                UnsafeUtility.SizeOf<TerminalTelemetryEntry>() != 64 ||
                UnsafeUtility.SizeOf<DecryptionPuzzleDTO>() != TerminalOsConstants.DecryptionPuzzleStrideBytes ||
                UnsafeUtility.SizeOf<DecryptionTerminalDTO>() != TerminalOsConstants.DecryptionTerminalStrideBytes ||
                UnsafeUtility.SizeOf<DecryptionKnobInputDTO>() != TerminalOsConstants.DecryptionKnobInputStrideBytes ||
                UnsafeUtility.SizeOf<TerminalUnlockedSignal>() != 32 ||
                UnsafeUtility.SizeOf<DecryptionTelemetryEntry>() != TerminalOsConstants.DecryptionTelemetryStrideBytes ||
                !TerminalOsSelfAudit.ValidateLayoutAndRayPlaneMath())
            {
                _lastFaultFlags |= FaultLayoutMismatch;
            }
        }

        private void RefreshScalabilityPolicy()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            float quality = math.max(ResolveGlobalQualityWeight01(), math.saturate(minimumQualityWeight));
            float previousQuality = _globalQualityWeight;
            _globalQualityWeight = quality;
            _framesBetweenUpdates = math.clamp((int)math.round(math.lerp(1f, 15f, 1f - quality)), 1, 15);
            _decryptionFramesBetweenEvaluations = math.clamp(
                (int)math.round(math.lerp((float)MaxIdleDecryptionStride, 1f, Smooth01(quality))),
                1,
                MaxIdleDecryptionStride);
            if (math.abs(previousQuality - quality) > 0.0005f)
            {
                _panelInstanceUploadDirty = true;
                _bindingsDirty = true;
            }

            if (_textureResolution > 0 && frame < _nextQualityRefreshFrame)
                return;

            _nextQualityRefreshFrame = frame + math.clamp((int)math.round(math.lerp(30f, 120f, 1f - quality)), 30, 120);
            float resolutionCurve = Smooth01(quality);
            int targetResolution = AlignResolution((int)math.round(math.lerp(MinQualityResolution, MaxQualityResolution, resolutionCurve)));
            bool resolutionChanged = _textureResolution != targetResolution;

            _textureResolution = targetResolution;
            if (resolutionChanged)
                QueueGraphicsResourceRebuild();
        }

        public void SlowTick()
        {
            int ownerFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            TryMonitorLayoutCsv(ownerFrame);
            TryMonitorDecryptionCsv(ownerFrame);
            FlushQueuedBlackBoxDumps();
        }

        private void QueueGraphicsResourceRebuild()
        {
            _pendingGraphicsResourceRebuild = true;
            _graphicsResourcesReady = false;
            _bindingsDirty = true;
        }

        private bool FlushPendingGraphicsResourceRebuild()
        {
            if (!_pendingGraphicsResourceRebuild)
                return true;

            if (_formatScheduled ||
                _clickResolveScheduled ||
                _terminalInteractionScheduled ||
                _decryptionScheduled)
            {
                return false;
            }

            if (!_nativeResourcesReady)
            {
                EnsureNativeResources();
                if (!_nativeResourcesReady)
                    return false;
            }

            _pendingGraphicsResourceRebuild = false;
            ReleaseRenderTexture();
            _graphicsResourcesReady = false;
            EnsureGraphicsResources();
            ForceAllDirty();
            if (!_graphicsResourcesReady)
                _pendingGraphicsResourceRebuild = true;
            return _graphicsResourcesReady;
        }

        private float ResolveGlobalQualityWeight01()
        {
            if (decryptionGlobalQualityOverride >= 0f)
                return math.saturate(decryptionGlobalQualityOverride);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(weight))
                return math.saturate(weight);

            return math.saturate(_globalQualityWeight);
        }

        private static bool IsSaveManagerUsable(SaveManager saveManager)
        {
            return saveManager != null && saveManager.IsInitialized;
        }

        private void UpdateDialogueChoiceVisualSync()
        {
            SaveManager saveManager = _cachedSaveManager;
            ushort flags = IsSaveManagerUsable(saveManager) ? saveManager.PlayerDialogueChoiceFlags : (ushort)0;
            bool facilitySaved = (flags & DialogueDecisionSaveFacilityMask) != 0;
            float targetBlend = math.select(0f, 1f, facilitySaved);
            if (_dialogueChoiceVisualBlend < 0f)
            {
                _dialogueChoiceVisualBlend = targetBlend;
            }
            else
            {
                float quality = math.saturate(_globalQualityWeight);
                float step = math.lerp(1f, 0.055f, quality);
                _dialogueChoiceVisualBlend = math.lerp(_dialogueChoiceVisualBlend, targetBlend, step);
            }

            if (flags == _lastUploadedDialogueChoiceFlags &&
                math.abs(_dialogueChoiceVisualBlend - _lastUploadedDialogueChoiceVisualBlend) <= 0.0005f)
            {
                return;
            }

            float4 cold = new float4(0.82f, 0.95f, 1f, 1f);
            float4 facilitySavedColor = new float4(1f, 0.46f, 0.16f, 1f);
            float4 color = math.lerp(cold, facilitySavedColor, _dialogueChoiceVisualBlend);
            Shader.SetGlobalVector(
                TerminalOsStyleColorId,
                new Vector4(color.x, color.y, color.z, color.w));
            Shader.SetGlobalFloat(TerminalOsStyleBlendId, _dialogueChoiceVisualBlend);
            Shader.SetGlobalFloat(TerminalOsStyleFlagsId, flags);
            _lastUploadedDialogueChoiceFlags = flags;
            _lastUploadedDialogueChoiceVisualBlend = _dialogueChoiceVisualBlend;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                    _input = currentService as IInputService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    if (attentionCameraOverride == null)
                    {
                        _attentionCameraCache = _cachedPlayerContext != null
                            ? _cachedPlayerContext.PlayerCamera
                            : null;
                        _nextCameraResolveFrame = 0;
                    }
                    break;
                case GlobalRegistryServiceSlot.Save:
                    _cachedSaveManager = currentService as SaveManager;
                    _lastUploadedDialogueChoiceFlags = ushort.MaxValue;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault nextVault = currentService is IDataVault dataVault ? dataVault : null;
                    if (!ReferenceEquals(_vault, nextVault))
                    {
                        CompleteJobsForTeardown();
                        IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : null;
                        BindDataVaultForLifecycle(nextVault, previousVault);
                        _bindingsDirty = true;
                    }
                    break;
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AlignResolution(int value)
        {
            int clamped = math.clamp(value, MinQualityResolution, MaxQualityResolution);
            return (clamped + 7) & ~7;
        }

        private static bool TryResolveSimulationFrame(out int frame)
        {
            uint frameId = SystemDispatcher.CurrentFrameId;
            if (frameId == 0u)
            {
                frame = 0;
                return false;
            }

            frame = frameId > 2147483647u ? int.MaxValue : (int)frameId;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveScheduledDecryptionFrameForOwner()
        {
            uint frame = _decryptionScheduleFrame;
            return frame > 2147483647u ? int.MaxValue : (int)frame;
        }

        private void EnsureNativeResources()
        {
            if (_nativeResourcesReady)
                return;

            int frame = Application.isPlaying ? Hecton8.Core.SystemDispatcher.CurrentFrameIndex : 0;
            if (Application.isPlaying && frame < _nextNativeResourceRetryFrame)
                return;

            _terminalCount = TerminalOsConstants.TerminalCapacity;
            IDataVault vault = _vault;
            if (vault == null)
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                _terminalCount = 0;
                ScheduleNativeResourceRetry(frame);
                return;
            }

            _vault = vault;
            OpenNativeBufferForOwner(vault, TerminalStatesBufferId, _terminalCount, NativeArrayOptions.ClearMemory, out _terminalStatesHandle);
            OpenNativeBufferForOwner(vault, ScreenCommandsBufferId, _terminalCount, NativeArrayOptions.ClearMemory, out _screenCommandsHandle);
            OpenNativeBufferForOwner(vault, GlyphUvsBufferId, TerminalOsConstants.GlyphCount, NativeArrayOptions.UninitializedMemory, out _glyphUvsHandle);
            OpenNativeBufferForOwner(vault, TerminalPositionsBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _terminalPositionsHandle);
            OpenNativeBufferForOwner(vault, TerminalForwardsBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _terminalForwardHandle);
            OpenNativeBufferForOwner(vault, DirtyIndicesBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _dirtyIndicesHandle);
            OpenNativeBufferForOwner(vault, TelemetryRingBufferId, TerminalOsConstants.BlackBoxFrameCount, NativeArrayOptions.ClearMemory, out _telemetryRingHandle);
            OpenNativeBufferForOwner(vault, MockPowerBufferId, 1, NativeArrayOptions.ClearMemory, out _mockPowerSignalHandle);
            OpenNativeBufferForOwner(vault, MockDamageBufferId, 1, NativeArrayOptions.ClearMemory, out _mockDamageSignalHandle);
            OpenNativeBufferForOwner(vault, MockPowerStatusBufferId, 1, NativeArrayOptions.ClearMemory, out _mockPowerStatusSignalHandle);
            OpenNativeBufferForOwner(vault, ButtonAabbBufferId, TerminalOsConstants.ButtonAabbCapacity, NativeArrayOptions.UninitializedMemory, out _buttonAabbHandle);
            OpenNativeBufferForOwner(vault, PanelInstancesBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _panelInstancesHandle);
            OpenNativeBufferForOwner(vault, TerminalClickScratchBufferId, TerminalOsConstants.MaxQueuedClicks, NativeArrayOptions.UninitializedMemory, out _clickScratchHandle);
            OpenNativeBufferForOwner(vault, TerminalPlanesBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _terminalPlanesHandle);
            OpenNativeBufferForOwner(vault, GazeRayBufferId, 1, NativeArrayOptions.UninitializedMemory, out _gazeRayHandle);
            OpenNativeBufferForOwner(vault, TerminalInteractionsBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _terminalInteractionsHandle);
            OpenTerminalProjectionNativeBuffers(vault);
            OpenNativeBufferForOwner(vault, DecryptionPuzzlesBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _decryptionPuzzlesHandle);
            OpenNativeBufferForOwner(vault, DecryptionTerminalsBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _decryptionTerminalsHandle);
            OpenNativeBufferForOwner(vault, DecryptionKnobInputBufferId, 1, NativeArrayOptions.UninitializedMemory, out _decryptionKnobInputHandle);
            OpenNativeBufferForOwner(vault, DecryptionTelemetryRingBufferId, TerminalOsConstants.BlackBoxFrameCount, NativeArrayOptions.ClearMemory, out _decryptionTelemetryRingHandle);
            ConfigureSignalLanes();

            if (!ValidateNativeBuffers())
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                DisposeNativeResources();
                ScheduleNativeResourceRetry(frame);
                return;
            }

            InitializeTerminalState();
            InitializeTerminalProjectionState();
            InitializeDecryptionState();
            GenerateEmergencyMockFont();
            _layoutUploadDirty = true;
            _glyphUploadDirty = true;
            _panelInstanceUploadDirty = true;
            _decryptionBufferUploadDirty = true;
            _bindingsDirty = true;
            _nativeResourcesReady = true;
            _nextNativeResourceRetryFrame = 0;
        }

        private void ScheduleNativeResourceRetry(int frame)
        {
            if (!Application.isPlaying)
                return;

            _nextNativeResourceRetryFrame = frame + ResolveColdRegistryRetryStride();
        }

        private static void ConfigureSignalLanes()
        {
            SignalBus<TerminalClickSignal>.Configure(
                TerminalClickSignal.ExpectedCapacity,
                TerminalClickSignal.MaxFrameSignals,
                TerminalClickSignal.LowTierFrameSignals,
                TerminalClickSignal.LaneHash);
            SignalBus<TerminalClickSignal>.EnsureInitialized();
            SignalBus<TerminalCommandSignal>.Configure(
                TerminalCommandSignal.ExpectedCapacity,
                TerminalCommandSignal.MaxFrameSignals,
                TerminalCommandSignal.LowTierFrameSignals,
                TerminalCommandSignal.LaneHash);
            SignalBus<TerminalCommandSignal>.EnsureInitialized();
            SignalBus<TerminalUnlockedSignal>.Configure(
                TerminalUnlockedSignal.ExpectedCapacity,
                TerminalUnlockedSignal.MaxFrameSignals,
                TerminalUnlockedSignal.LowTierFrameSignals,
                TerminalUnlockedSignal.LaneHash);
            SignalBus<TerminalUnlockedSignal>.EnsureInitialized();
            SignalBus<AppliedLoreTerminalPreviewSignal>.Configure(
                AppliedLoreTerminalPreviewSignal.ExpectedCapacity,
                AppliedLoreTerminalPreviewSignal.MaxFrameSignals,
                AppliedLoreTerminalPreviewSignal.LowTierFrameSignals,
                AppliedLoreTerminalPreviewSignal.LaneHash);
            SignalBus<AppliedLoreTerminalPreviewSignal>.EnsureInitialized();
            SignalBus<InteractionUiSignal>.EnsureInitialized();
        }

        private static void OpenNativeBufferForOwner<T>(
            IDataVault vault,
            BufferID bufferId,
            int length,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle) where T : unmanaged
        {
            handle = default;
            if (vault == null)
                return;

            handle = vault.EnsureGenerationHandle<T>(bufferId, length, SystemID.UI, options);
            if (!IsExactVaultHandle(in handle, bufferId))
                handle = default;
        }

        private bool ValidateNativeBuffers()
        {
            if (!TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) ||
                !TryOpenVaultBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands) ||
                !TryOpenVaultBuffer(ref _glyphUvsHandle, out NativeArray<float4> glyphUvs) ||
                !TryOpenVaultBuffer(ref _terminalPositionsHandle, out NativeArray<float4> terminalPositions) ||
                !TryOpenVaultBuffer(ref _terminalForwardHandle, out NativeArray<float4> terminalForward) ||
                !TryOpenVaultBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices) ||
                !TryOpenVaultBuffer(ref _telemetryRingHandle, out NativeArray<TerminalTelemetryEntry> telemetryRing) ||
                !TryOpenVaultBuffer(ref _mockPowerSignalHandle, out NativeArray<MockPowerStateSignal> mockPowerSignal) ||
                !TryOpenVaultBuffer(ref _mockDamageSignalHandle, out NativeArray<MockDamageScalarSignal> mockDamageSignal) ||
                !TryOpenVaultBuffer(ref _mockPowerStatusSignalHandle, out NativeArray<MockPowerStatusSignal> mockPowerStatusSignal) ||
                !TryOpenVaultBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttonAabbs) ||
                !TryOpenVaultBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> panelInstances) ||
                !TryOpenVaultBuffer(ref _clickScratchHandle, out NativeArray<TerminalClickSignal> clickScratch) ||
                !TryOpenVaultBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes) ||
                !TryOpenVaultBuffer(ref _gazeRayHandle, out NativeArray<GazeRayDTO> gazeRays) ||
                !TryOpenVaultBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> interactions) ||
                !TryOpenVaultBuffer(ref _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO> decryptionPuzzles) ||
                !TryOpenVaultBuffer(ref _decryptionTerminalsHandle, out NativeArray<DecryptionTerminalDTO> decryptionTerminals) ||
                !TryOpenVaultBuffer(ref _decryptionKnobInputHandle, out NativeArray<DecryptionKnobInputDTO> decryptionKnobInput) ||
                !TryOpenVaultBuffer(ref _decryptionTelemetryRingHandle, out NativeArray<DecryptionTelemetryEntry> decryptionTelemetryRing))
            {
                return false;
            }

            return terminalStates.Length >= _terminalCount &&
                   screenCommands.Length >= _terminalCount &&
                   glyphUvs.Length >= TerminalOsConstants.GlyphCount &&
                   terminalPositions.Length >= _terminalCount &&
                   terminalForward.Length >= _terminalCount &&
                   dirtyIndices.Length >= _terminalCount &&
                   telemetryRing.Length >= TerminalOsConstants.BlackBoxFrameCount &&
                   mockPowerSignal.Length >= 1 &&
                   mockDamageSignal.Length >= 1 &&
                   mockPowerStatusSignal.Length >= 1 &&
                   buttonAabbs.Length >= TerminalOsConstants.ButtonAabbCapacity &&
                   panelInstances.Length >= _terminalCount &&
                   clickScratch.Length >= TerminalOsConstants.MaxQueuedClicks &&
                   terminalPlanes.Length >= _terminalCount &&
                   gazeRays.Length >= 1 &&
                   interactions.Length >= _terminalCount &&
                   ValidateTerminalProjectionNativeBuffers() &&
                   decryptionPuzzles.Length >= _terminalCount &&
                   decryptionTerminals.Length >= _terminalCount &&
                   decryptionKnobInput.Length >= 1 &&
                   decryptionTelemetryRing.Length >= TerminalOsConstants.BlackBoxFrameCount;
        }

        private bool TryOpenVaultBuffer<T>(ref VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : unmanaged
        {
            buffer = default;
            if (_vault == null || _vault.IsCompactionFenceActive || !IsTerminalOsVaultHandle(in handle))
                return false;

            return _vault.TryResolveHandle(in handle, out buffer) &&
                   !_vault.IsCompactionFenceActive &&
                   buffer.IsCreated;
        }

        private bool TryReadVaultBuffer<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer) where T : unmanaged
        {
            buffer = default;
            if (_vault == null || _vault.IsCompactionFenceActive || !IsTerminalOsVaultHandle(in handle))
                return false;

            return _vault.TryReadOnlyHandle(in handle, out buffer) &&
                   !_vault.IsCompactionFenceActive &&
                   buffer.IsCreated;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : unmanaged
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)SystemID.UI &&
                   handle.Generation != 0u;
        }

        private static bool IsTerminalOsVaultHandle<T>(in VaultGenerationHandle<T> handle) where T : unmanaged
        {
            return handle.SystemID == (uint)SystemID.UI &&
                   handle.Generation != 0u &&
                   IsTerminalOsBufferId(handle.BufferID);
        }

        private static bool IsTerminalOsBufferId(uint bufferId)
        {
            switch (bufferId)
            {
                case unchecked((uint)(int)TerminalStatesBufferId):
                case unchecked((uint)(int)ScreenCommandsBufferId):
                case unchecked((uint)(int)GlyphUvsBufferId):
                case unchecked((uint)(int)TerminalPositionsBufferId):
                case unchecked((uint)(int)TerminalForwardsBufferId):
                case unchecked((uint)(int)DirtyIndicesBufferId):
                case unchecked((uint)(int)TelemetryRingBufferId):
                case unchecked((uint)(int)MockPowerBufferId):
                case unchecked((uint)(int)MockDamageBufferId):
                case unchecked((uint)(int)MockPowerStatusBufferId):
                case unchecked((uint)(int)ButtonAabbBufferId):
                case unchecked((uint)(int)PanelInstancesBufferId):
                case unchecked((uint)(int)TerminalClickScratchBufferId):
                case unchecked((uint)(int)TerminalPlanesBufferId):
                case unchecked((uint)(int)GazeRayBufferId):
                case unchecked((uint)(int)TerminalInteractionsBufferId):
                case unchecked((uint)(int)DecryptionPuzzlesBufferId):
                case unchecked((uint)(int)DecryptionTerminalsBufferId):
                case unchecked((uint)(int)DecryptionKnobInputBufferId):
                case unchecked((uint)(int)DecryptionTelemetryRingBufferId):
                case unchecked((uint)(int)TerminalInputStatesBufferId):
                case unchecked((uint)(int)TerminalInputTelemetryRingBufferId):
                case unchecked((uint)(int)TerminalInputTuningBufferId):
                case unchecked((uint)(int)TerminalInputRowHashesBufferId):
                    return true;
                default:
                    return false;
            }
        }

        private void InitializeTerminalState()
        {
            if (!TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) ||
                !TryOpenVaultBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands) ||
                !TryOpenVaultBuffer(ref _terminalPositionsHandle, out NativeArray<float4> terminalPositions) ||
                !TryOpenVaultBuffer(ref _terminalForwardHandle, out NativeArray<float4> terminalForward) ||
                !TryOpenVaultBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> panelInstances) ||
                !TryOpenVaultBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttonAabbs) ||
                !TryOpenVaultBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes) ||
                !TryOpenVaultBuffer(ref _gazeRayHandle, out NativeArray<GazeRayDTO> gazeRays) ||
                !TryOpenVaultBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> interactions))
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            if (gazeRays.Length <= 0)
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            _buttonCount = 0;
            gazeRays[0] = default;
            int count = math.min(
                _terminalCount,
                math.min(
                    math.min(terminalStates.Length, screenCommands.Length),
                    math.min(
                        math.min(terminalPositions.Length, terminalForward.Length),
                        math.min(math.min(panelInstances.Length, terminalPlanes.Length), interactions.Length))));
            for (int i = 0; i < count; i++)
            {
                uint terminalHash = TerminalOsHash.HashIndex(i);
                TerminalStateDTO state = default;
                state.TerminalHash = terminalHash;
                state.BackgroundColor = 0x00061418u;
                state.Value1 = 0.75f;
                state.Value2 = 0f;
                TerminalAsciiFormatter.WritePowerLine(ref state.TextLine, 75, 0, true);
                state.IsDirty = 1;
                terminalStates[i] = state;

                int col = i & 7;
                int row = i >> 3;
                screenCommands[i] = new ScreenCommandDTO
                {
                    FontAtlasUV_Packed = 0u,
                    Position = new float2(0.055f, 0.63f - ((row & 1) * 0.015f)),
                    Scale = 0.075f
                };

                terminalPositions[i] = new float4((col - 3.5f) * 2.25f, 1.35f + ((row & 3) * 0.18f), 4.5f + row * 0.85f, 0f);
                terminalForward[i] = new float4(0f, 0f, -1f, 0f);
                float4x4 matrix = float4x4.TRS(terminalPositions[i].xyz, quaternion.identity, new float3(1.25f, 0.72f, 1f));
                panelInstances[i] = new TerminalPanelInstanceDTO
                {
                    LocalToWorld = matrix,
                    SliceFlags = new float4(i, 0f, 0f, 0f)
                };
                uint firstButton = (uint)_buttonCount;
                AddButtonAabb(buttonAabbs, terminalHash, TerminalOsConstants.CommandOpenDoor, new float4(0.08f, 0.08f, 0.34f, 0.18f));
                AddButtonAabb(buttonAabbs, terminalHash, TerminalOsConstants.CommandAcknowledge, new float4(0.66f, 0.08f, 0.92f, 0.18f));
                terminalPlanes[i] = BuildTerminalPlane(matrix, terminalHash, firstButton, 2u, 1f, 0f);
                interactions[i] = default;
            }

            RecalculatePanelRenderBounds();
        }

        private void InitializeDecryptionState()
        {
            if (!TryOpenVaultBuffer(ref _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO> puzzles) ||
                !TryOpenVaultBuffer(ref _decryptionTerminalsHandle, out NativeArray<DecryptionTerminalDTO> decryptionTerminals) ||
                !TryOpenVaultBuffer(ref _decryptionKnobInputHandle, out NativeArray<DecryptionKnobInputDTO> knobInput) ||
                !TryOpenVaultBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes))
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            if (knobInput.Length > 0)
                knobInput[0] = default;
            int clearCount = math.min(_terminalCount, puzzles.Length);
            if (clearCount > 0)
            {
                DecryptionPuzzleDTO* puzzlePtr = (DecryptionPuzzleDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(puzzles);
                ClearDecryptionFlagsJob clearJob = new ClearDecryptionFlagsJob
                {
                    Puzzles = puzzlePtr,
                    PuzzleCount = clearCount
                };
                for (int index = 0; index < clearCount; index++)
                    clearJob.Execute(index);
            }

            int generateCount = math.min(clearCount, decryptionTerminals.Length);
            if (generateCount > 0)
            {
                GenerateMockPuzzleDataJob generateJob = new GenerateMockPuzzleDataJob
                {
                    Puzzles = puzzles,
                    Terminals = decryptionTerminals,
                    Planes = terminalPlanes,
                    PuzzleCount = generateCount,
                    BasePlayerFrequency = decryptionBaseFrequency
                };
                for (int index = 0; index < generateCount; index++)
                    generateJob.Execute(index);
            }
            _decryptionTelemetryCursor = 0;
            _lastDecryptionTelemetryFrame = -1;
            _lastDecryptionFaultFlags = 0u;
            _decryptionDumpBackpressureReported = false;
            _decryptionBufferUploadDirty = true;
            _bindingsDirty = true;
        }

        private void AddButtonAabb(NativeArray<ButtonAABBDTO> buttons, uint terminalHash, uint commandHash, float4 rectUv)
        {
            if (_buttonCount >= buttons.Length)
                return;

            buttons[_buttonCount++] = new ButtonAABBDTO
            {
                RectUv = rectUv,
                TerminalHash = terminalHash,
                CommandHash = commandHash,
                Flags = TerminalOsConstants.ButtonFlagEnabled
            };
        }

        private TerminalPlaneDTO BuildTerminalPlane(
            in float4x4 matrix,
            uint terminalHash,
            uint firstButton,
            uint buttonCount,
            float power01,
            float submerged01)
        {
            float3 rightAxis = matrix.c0.xyz;
            float3 upAxis = matrix.c1.xyz;
            float3 normalAxis = -matrix.c2.xyz;
            float width = SafeVectorLength(rightAxis, 0.001f);
            float height = SafeVectorLength(upAxis, 0.001f);
            float safePower = math.saturate(math.isfinite(power01) ? power01 : 0f);
            float safeSubmerged = math.saturate(math.isfinite(submerged01) ? submerged01 : 0f);
            uint flags = TerminalOsConstants.PlaneFlagActive;
            flags |= safePower > 0.001f ? TerminalOsConstants.PlaneFlagPowered : 0u;
            flags |= safeSubmerged > 0.5f ? TerminalOsConstants.PlaneFlagSubmerged : 0u;
            TryResolveAupFromRuntimeOrigin(ToVector3(matrix.c3.xyz), out AbsoluteUniversePosition centerAup);

            return new TerminalPlaneDTO
            {
                CenterAup = centerAup,
                Normal = math.normalizesafe(normalAxis, new float3(0f, 0f, -1f)),
                Up = math.normalizesafe(upAxis, new float3(0f, 1f, 0f)),
                Right = math.normalizesafe(rightAxis, new float3(1f, 0f, 0f)),
                Width = width,
                Height = height,
                TerminalHash = terminalHash,
                Flags = flags,
                LayoutFirstButton = firstButton,
                LayoutButtonCount = buttonCount,
                Power01 = safePower,
                Submerged01 = safeSubmerged
            };
        }

        private void GenerateEmergencyMockFont()
        {
            if (!TryOpenVaultBuffer(ref _glyphUvsHandle, out NativeArray<float4> glyphUvs))
                return;

            const float invGrid = 1f / 16f;
            for (int i = 0; i < TerminalOsConstants.GlyphCount; i++)
            {
                int col = i & 15;
                int row = i >> 4;
                float2 uv0 = new float2(col * invGrid, row * invGrid);
                float2 uv1 = uv0 + new float2(invGrid, invGrid);
                glyphUvs[i] = new float4(uv0.x, uv0.y, uv1.x, uv1.y);
            }
        }

        private void EnsureGraphicsResources()
        {
            if (!TryReadVaultBuffer(in _terminalStatesHandle, out NativeArray<TerminalStateDTO>.ReadOnly _))
                return;

            EnsureTextureArray();
            if (_stateBuffer0 == null)
                _stateBuffer0 = CreateStructuredLockBuffer<TerminalStateDTO>(_terminalCount);
            if (_stateBuffer1 == null)
                _stateBuffer1 = CreateStructuredLockBuffer<TerminalStateDTO>(_terminalCount);
            if (_screenCommandBuffer == null)
                _screenCommandBuffer = CreateStructuredLockBuffer<ScreenCommandDTO>(_terminalCount);
            if (_glyphUvBuffer == null)
                _glyphUvBuffer = CreateStructuredLockBuffer<float4>(TerminalOsConstants.GlyphCount);
            if (_dirtyIndexBufferA == null)
                _dirtyIndexBufferA = CreateStructuredLockBuffer<int>(_terminalCount);
            if (_dirtyIndexBufferB == null)
                _dirtyIndexBufferB = CreateStructuredLockBuffer<int>(_terminalCount);
            if (_activeDirtyIndexBuffer == null)
                _activeDirtyIndexBuffer = _dirtyIndexBufferA;
            if (_panelInstanceBuffer == null)
                _panelInstanceBuffer = CreateStructuredLockBuffer<TerminalPanelInstanceDTO>(_terminalCount);
            EnsureTerminalProjectionGraphicsResources();
            if (_decryptionPuzzleBuffer0 == null)
                _decryptionPuzzleBuffer0 = CreateStructuredLockBuffer<DecryptionPuzzleDTO>(_terminalCount);
            if (_decryptionPuzzleBuffer1 == null)
                _decryptionPuzzleBuffer1 = CreateStructuredLockBuffer<DecryptionPuzzleDTO>(_terminalCount);
            if (_decryptionPuzzleBuffer == null)
                _decryptionPuzzleBuffer = _decryptionPuzzleBuffer0;

            if (_layoutUploadDirty)
                UploadScreenCommands();
            if (_glyphUploadDirty)
                UploadGlyphUvs();
            if (_panelInstanceUploadDirty)
                UploadPanelInstances();
            if (_terminalInputUploadDirty)
                UploadTerminalInputStates();
            if (_decryptionBufferUploadDirty)
                UploadDecryptionPuzzles();

            bool computeReady = EnsureComputeKernelForOwner() && RefreshDispatchGroupCounts();
            _graphicsResourcesReady = _terminalTextureArray != null &&
                                       _stateBuffer0 != null &&
                                      _stateBuffer1 != null &&
                                      _screenCommandBuffer != null &&
                                      _glyphUvBuffer != null &&
                                      _dirtyIndexBufferA != null &&
                                      _dirtyIndexBufferB != null &&
                                      _activeDirtyIndexBuffer != null &&
                                      _panelInstanceBuffer != null &&
                                      TerminalProjectionGraphicsReady() &&
                                      _decryptionPuzzleBuffer0 != null &&
                                       _decryptionPuzzleBuffer1 != null &&
                                       _decryptionPuzzleBuffer != null &&
                                       computeReady;
            if (_graphicsResourcesReady)
                _pendingGraphicsResourceRebuild = false;
        }

        private void EnsureTextureArray()
        {
            int resolution = _textureResolution > 0
                ? _textureResolution
                : AlignResolution((int)math.round(math.lerp(MinQualityResolution, MaxQualityResolution, Smooth01(_globalQualityWeight))));
            if (_terminalTextureArray != null &&
                _terminalTextureArray.width == resolution &&
                _terminalTextureArray.height == resolution &&
                _terminalTextureArray.volumeDepth == TerminalOsConstants.TerminalCapacity)
                return;

            ReleaseRenderTexture();
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.R8G8B8A8_UNorm, 0)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = TerminalOsConstants.TerminalCapacity,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
            };

            _terminalTextureArray = new RenderTexture(descriptor)
            {
                name = "H8_TerminalOS_Array",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _terminalTextureArray.Create();
            _bindingsDirty = true;
        }

        private static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : unmanaged
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private GraphicsBuffer SelectStateBuffer(int index)
        {
            return index == 0 ? _stateBuffer0 : _stateBuffer1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private GraphicsBuffer SelectDecryptionPuzzleUploadBuffer()
        {
            return _decryptionPuzzleWriteBufferIndex == 0 ? _decryptionPuzzleBuffer0 : _decryptionPuzzleBuffer1;
        }

        private bool EnsureComputeKernelForOwner()
        {
            if (terminalBlitCompute == null || !_supportsTerminalComputeCold)
            {
                ResetTerminalBlitComputeState();
                return false;
            }

            if (_blitKernel >= 0 && _threadsX > 0 && _threadsY > 0)
                return true;

            if (!TryFindTerminalBlitKernel(terminalBlitCompute, "KTerminalBlit", out _blitKernel))
            {
                ResetTerminalBlitComputeState();
                return false;
            }

            uint x;
            uint y;
            uint z;
            try
            {
                if (!terminalBlitCompute.IsSupported(_blitKernel))
                {
                    ResetTerminalBlitComputeState();
                    return false;
                }

                terminalBlitCompute.GetKernelThreadGroupSizes(_blitKernel, out x, out y, out z);
            }
            catch (System.ObjectDisposedException)
            {
                ResetTerminalBlitComputeState();
                return false;
            }
            catch (System.InvalidOperationException)
            {
                ResetTerminalBlitComputeState();
                return false;
            }
            catch (System.ArgumentException)
            {
                ResetTerminalBlitComputeState();
                return false;
            }
            catch (UnityEngine.MissingReferenceException)
            {
                ResetTerminalBlitComputeState();
                return false;
            }
            catch (UnityEngine.UnityException)
            {
                ResetTerminalBlitComputeState();
                return false;
            }
            if (x == 0u || y == 0u || z != 1u)
            {
                ResetTerminalBlitComputeState();
                return false;
            }

            if (x > PortableMaxComputeThreadsPerGroup ||
                y > PortableMaxComputeThreadsPerGroup)
            {
                ResetTerminalBlitComputeState();
                return false;
            }

            uint xyThreads = x * y;
            if (xyThreads > PortableMaxComputeThreadsPerGroup)
            {
                ResetTerminalBlitComputeState();
                return false;
            }

            _threadsX = (int)x;
            _threadsY = (int)y;
            return true;
        }

        private static bool TryFindTerminalBlitKernel(ComputeShader computeShader, string kernelName, out int kernel)
        {
            kernel = -1;
            if (computeShader == null)
                return false;

            try
            {
                if (!computeShader.HasKernel(kernelName))
                    return false;

                kernel = computeShader.FindKernel(kernelName);
                return kernel >= 0;
            }
            catch (System.ObjectDisposedException)
            {
                kernel = -1;
                return false;
            }
            catch (System.InvalidOperationException)
            {
                kernel = -1;
                return false;
            }
            catch (System.ArgumentException)
            {
                kernel = -1;
                return false;
            }
            catch (UnityEngine.MissingReferenceException)
            {
                kernel = -1;
                return false;
            }
            catch (UnityEngine.UnityException)
            {
                kernel = -1;
                return false;
            }
        }

        private bool RefreshDispatchGroupCounts()
        {
            int resolution = math.max(1, _textureResolution);
            _groupsX = ResolveDispatchGroups(resolution, _threadsX);
            _groupsY = ResolveDispatchGroups(resolution, _threadsY);
            return _groupsX > 0 && _groupsY > 0;
        }

        private static int ResolveDispatchGroups(int value, int divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            long numerator = (long)value + divisor - 1L;
            long groups = numerator / divisor;
            if (groups <= 0L || groups > MaxDispatchGroupsPerDimension)
                return 0;

            return (int)groups;
        }

        private void ResetTerminalBlitComputeState()
        {
            _blitKernel = -1;
            _threadsX = 0;
            _threadsY = 0;
            _groupsX = 0;
            _groupsY = 0;
        }

        private int BuildDirtyList(int ownerFrame)
        {
            if (!TryOpenVaultBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices) ||
                !TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates))
                return 0;

            bool hasCamera = TryCaptureCameraFrameForOwner(ownerFrame, out float3 cameraPosition, out float3 cameraForward);
            int dirtyCount = 0;
            _lastFaultFlags &= ~FaultNonFinite;
            int count = math.min(_terminalCount, math.min(dirtyIndices.Length, terminalStates.Length));
            for (int i = 0; i < count; i++)
            {
                TerminalStateDTO state = terminalStates[i];
                if (state.IsDirty == 0)
                    continue;

                if (hasCamera && !PassesAttentionCull(i, cameraPosition, cameraForward))
                {
                    continue;
                }

                if (!math.isfinite(state.Value1) || !math.isfinite(state.Value2))
                {
                    state.IsDirty = 0;
                    terminalStates[i] = state;
                    _lastFaultFlags |= FaultNonFinite;
                    continue;
                }

                dirtyIndices[dirtyCount++] = i;
            }

            return dirtyCount;
        }

        private bool TryCaptureCameraFrameForOwner(int ownerFrame, out float3 cameraPosition, out float3 cameraForward)
        {
            Camera camera = RefreshAttentionCameraForOwner(ownerFrame);

            if (camera == null)
            {
                cameraPosition = default;
                cameraForward = default;
                return false;
            }

            Transform cameraTransform = camera.transform;
            Vector3 position = cameraTransform.position;
            Vector3 forward = cameraTransform.forward;
            cameraPosition = new float3(position.x, position.y, position.z);
            cameraForward = math.normalizesafe(new float3(forward.x, forward.y, forward.z), new float3(0f, 0f, 1f));
            bool finite = math.all(math.isfinite(cameraPosition)) && math.all(math.isfinite(cameraForward));
            if (!finite)
            {
                _lastFaultFlags |= FaultNonFinite;
                cameraPosition = default;
                cameraForward = default;
            }

            return finite;
        }

        private Camera RefreshAttentionCameraForOwner(int frame)
        {
            if (attentionCameraOverride != null)
            {
                _attentionCameraCache = attentionCameraOverride;
                return attentionCameraOverride;
            }

            if (_attentionCameraCache != null)
                return _attentionCameraCache;

            if (frame < _nextCameraResolveFrame)
                return null;

            _nextCameraResolveFrame = frame + math.clamp((int)math.round(math.lerp(15f, 60f, 1f - _globalQualityWeight)), 15, 60);
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.PlayerCamera != null)
                _attentionCameraCache = playerContext.PlayerCamera;

            return _attentionCameraCache;
        }

        private bool PassesAttentionCull(int index, float3 cameraPosition, float3 cameraForward)
        {
            float3 terminalPosition = ResolveTerminalPosition(index);
            if (!math.all(math.isfinite(terminalPosition)) ||
                !math.all(math.isfinite(cameraPosition)) ||
                !math.all(math.isfinite(cameraForward)))
            {
                _lastFaultFlags |= FaultNonFinite;
                return false;
            }

            float3 toTerminal = terminalPosition - cameraPosition;
            float distanceSq = math.lengthsq(toTerminal);
            if (!math.isfinite(distanceSq))
            {
                _lastFaultFlags |= FaultNonFinite;
                return false;
            }

            if (distanceSq > AttentionCullDistanceSq)
                return false;

            if (distanceSq <= 0.0001f)
                return true;

            float3 direction = toTerminal * math.rsqrt(distanceSq);
            return math.dot(cameraForward, direction) > 0f;
        }

        private float3 ResolveTerminalPosition(int index)
        {
            Transform terminal = ResolveTerminalTransform(index);
            if (terminal != null)
            {
                Vector3 position = terminal.position;
                return new float3(position.x, position.y, position.z);
            }

            return TryReadVaultBuffer(in _terminalPositionsHandle, out NativeArray<float4>.ReadOnly terminalPositions) && index < terminalPositions.Length
                ? terminalPositions[index].xyz
                : default;
        }

        private int ResolveBoundPanelCount()
        {
            int transformCount = terminalTransforms != null ? terminalTransforms.Length : 0;
            int rendererCount = terminalRenderers != null ? terminalRenderers.Length : 0;
            return math.max(transformCount, rendererCount);
        }

        private Transform ResolveTerminalTransform(int index)
        {
            if (terminalTransforms != null && index >= 0 && index < terminalTransforms.Length && terminalTransforms[index] != null)
                return terminalTransforms[index];

            if (terminalRenderers != null && index >= 0 && index < terminalRenderers.Length && terminalRenderers[index] != null)
                return terminalRenderers[index].transform;

            return null;
        }

        private bool UploadDirtyPayloads(int dirtyCount)
        {
            if (!_graphicsResourcesReady || dirtyCount <= 0)
                return false;

            long start = Stopwatch.GetTimestamp();
            bool uploaded = UploadDirtyIndices(dirtyCount) &&
                UploadDirtyStates(dirtyCount, SelectStateBuffer(_writeBufferIndex));
            _lastUploadMicroseconds = ElapsedMicroseconds(start);
            return uploaded;
        }

        private bool UploadDirtyIndices(int dirtyCount)
        {
            GraphicsBuffer dirtyIndexWriteBuffer = (_dirtyIndexUploadBufferIndex & 1) == 0 ? _dirtyIndexBufferA : _dirtyIndexBufferB;
            if (dirtyIndexWriteBuffer == null ||
                !TryOpenVaultBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices))
                return false;

            bool copied = false;
            NativeArray<int> mapped = dirtyIndexWriteBuffer.LockBufferForWrite<int>(0, dirtyCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(dirtyIndices);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<int>() * dirtyCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<int>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                dirtyIndexWriteBuffer.UnlockBufferAfterWrite<int>(dirtyCount);
            }

            if (copied)
            {
                _activeDirtyIndexBuffer = dirtyIndexWriteBuffer;
                _dirtyIndexUploadBufferIndex ^= 1;
            }

            return copied;
        }

        private bool UploadDirtyStates(int dirtyCount, GraphicsBuffer buffer)
        {
            if (buffer == null ||
                dirtyCount <= 0 ||
                !TryOpenVaultBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices))
                return false;

            bool uploaded = true;
            int runStart = dirtyIndices[0];
            int runEnd = runStart;
            for (int i = 1; i < dirtyCount; i++)
            {
                int index = dirtyIndices[i];
                if (index == runEnd + 1)
                {
                    runEnd = index;
                    continue;
                }

                uploaded &= UploadStateRun(buffer, runStart, runEnd - runStart + 1);
                runStart = index;
                runEnd = index;
            }

            uploaded &= UploadStateRun(buffer, runStart, runEnd - runStart + 1);
            return uploaded;
        }

        private bool UploadStateRun(GraphicsBuffer buffer, int startIndex, int count)
        {
            if (buffer == null || count <= 0)
                return false;

            bool copied = false;
            NativeArray<TerminalStateDTO> mapped = buffer.LockBufferForWrite<TerminalStateDTO>(startIndex, count);
            try
            {
                byte* sourceBase = (byte*)OpenTerminalStatePointerForOwner();
                if (sourceBase == null)
                {
                    _lastFaultFlags |= FaultVaultUnavailable;
                    return false;
                }

                void* sourcePtr = sourceBase + (startIndex * UnsafeUtility.SizeOf<TerminalStateDTO>());
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<TerminalStateDTO>() * count;
                long destinationBytes = (long)UnsafeUtility.SizeOf<TerminalStateDTO>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                buffer.UnlockBufferAfterWrite<TerminalStateDTO>(count);
            }

            return copied;
        }

        private bool UploadScreenCommands()
        {
            if (_screenCommandBuffer == null ||
                !TryOpenVaultBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands))
                return false;

            int uploadCount = math.min(_terminalCount, math.min(screenCommands.Length, _screenCommandBuffer.count));
            if (uploadCount <= 0)
                return false;

            bool copied = false;
            NativeArray<ScreenCommandDTO> mapped = _screenCommandBuffer.LockBufferForWrite<ScreenCommandDTO>(0, uploadCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(screenCommands);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<ScreenCommandDTO>() * uploadCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<ScreenCommandDTO>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                _screenCommandBuffer.UnlockBufferAfterWrite<ScreenCommandDTO>(uploadCount);
            }

            if (copied)
                _layoutUploadDirty = false;
            return copied;
        }

        private bool UploadGlyphUvs()
        {
            if (_glyphUvBuffer == null ||
                !TryOpenVaultBuffer(ref _glyphUvsHandle, out NativeArray<float4> glyphUvs))
                return false;

            bool copied = false;
            NativeArray<float4> mapped = _glyphUvBuffer.LockBufferForWrite<float4>(0, TerminalOsConstants.GlyphCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(glyphUvs);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<float4>() * TerminalOsConstants.GlyphCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<float4>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                _glyphUvBuffer.UnlockBufferAfterWrite<float4>(TerminalOsConstants.GlyphCount);
            }

            if (copied)
                _glyphUploadDirty = false;
            return copied;
        }

        private bool UploadPanelInstances()
        {
            if (_panelInstanceBuffer == null ||
                !TryOpenVaultBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> panelInstances))
                return false;

            int uploadCount = math.min(_terminalCount, math.min(panelInstances.Length, _panelInstanceBuffer.count));
            if (uploadCount <= 0)
                return false;

            bool copied = false;
            NativeArray<TerminalPanelInstanceDTO> mapped = _panelInstanceBuffer.LockBufferForWrite<TerminalPanelInstanceDTO>(0, uploadCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(panelInstances);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<TerminalPanelInstanceDTO>() * uploadCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<TerminalPanelInstanceDTO>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                _panelInstanceBuffer.UnlockBufferAfterWrite<TerminalPanelInstanceDTO>(uploadCount);
            }

            if (copied)
                _panelInstanceUploadDirty = false;
            return copied;
        }

        private bool UploadDecryptionPuzzles()
        {
            GraphicsBuffer uploadBuffer = SelectDecryptionPuzzleUploadBuffer();
            if (uploadBuffer == null ||
                !TryOpenVaultBuffer(ref _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO> puzzles))
            {
                ClearDecryptionPuzzleUploadBindingForOwner();
                return false;
            }

            int uploadCount = math.min(_terminalCount, math.min(puzzles.Length, uploadBuffer.count));
            if (uploadCount <= 0)
            {
                ClearDecryptionPuzzleUploadBindingForOwner();
                return false;
            }

            bool copied = false;
            NativeArray<DecryptionPuzzleDTO> mapped = uploadBuffer.LockBufferForWrite<DecryptionPuzzleDTO>(0, uploadCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(puzzles);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<DecryptionPuzzleDTO>() * uploadCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<DecryptionPuzzleDTO>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                uploadBuffer.UnlockBufferAfterWrite<DecryptionPuzzleDTO>(uploadCount);
            }

            if (copied)
            {
                _decryptionPuzzleBuffer = uploadBuffer;
                _decryptionPuzzleWriteBufferIndex = 1 - _decryptionPuzzleWriteBufferIndex;
                _decryptionPuzzleUploadCount = uploadCount;
                _decryptionBufferUploadDirty = false;
                _bindingsDirty = true;
            }
            else
            {
                ClearDecryptionPuzzleUploadBindingForOwner();
            }
            return copied;
        }

        private void ClearDecryptionPuzzleUploadBindingForOwner()
        {
            if (_decryptionPuzzleUploadCount == 0 && _decryptionPuzzleBuffer == null)
                return;

            _decryptionPuzzleUploadCount = 0;
            _decryptionPuzzleBuffer = null;
            _bindingsDirty = true;
        }

        private int DispatchDirtyScreens(int dirtyCount, int ownerFrame)
        {
            if (terminalBlitCompute == null ||
                !_supportsTerminalComputeCold ||
                _blitKernel < 0 ||
                _terminalTextureArray == null ||
                dirtyCount <= 0 ||
                dirtyCount > MaxDispatchGroupsPerDimension ||
                _groupsX <= 0 ||
                _groupsY <= 0)
            {
                return 0;
            }

            long start = Stopwatch.GetTimestamp();
            GraphicsBuffer stateBuffer = SelectStateBuffer(_writeBufferIndex);
            if (stateBuffer == null)
                return 0;
            terminalBlitCompute.SetTexture(_blitKernel, TerminalTextureArrayId, _terminalTextureArray);
            terminalBlitCompute.SetBuffer(_blitKernel, TerminalStatesId, stateBuffer);
            terminalBlitCompute.SetBuffer(_blitKernel, ScreenCommandsId, _screenCommandBuffer);
            terminalBlitCompute.SetBuffer(_blitKernel, DirtyTerminalIndicesId, _activeDirtyIndexBuffer);
            terminalBlitCompute.SetBuffer(_blitKernel, GlyphUvsId, _glyphUvBuffer);
            if (fontSdfAtlas != null)
                terminalBlitCompute.SetTexture(_blitKernel, FontSdfAtlasId, fontSdfAtlas);
            terminalBlitCompute.SetInt(FontAtlasReadyId, fontSdfAtlas != null ? 1 : 0);
            terminalBlitCompute.SetInt(TerminalResolutionXId, _textureResolution);
            terminalBlitCompute.SetInt(TerminalResolutionYId, _textureResolution);
            terminalBlitCompute.SetInt(DirtyTerminalCountId, dirtyCount);
            terminalBlitCompute.SetFloat(TimeSeedId, ownerFrame * HectonPhysicsContract.FixedDeltaTimeSeconds);
            terminalBlitCompute.SetFloat(HectonDiegeticGlitchQualityWeightId, _globalQualityWeight);
            terminalBlitCompute.Dispatch(_blitKernel, _groupsX, _groupsY, dirtyCount);
            _writeBufferIndex = 1 - _writeBufferIndex;
            _lastDispatchMicroseconds = ElapsedMicroseconds(start);
            return dirtyCount;
        }

        private void ClearDirtyFlags(int dirtyCount)
        {
            if (!TryOpenVaultBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices) ||
                !TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates))
                return;

            for (int i = 0; i < dirtyCount; i++)
            {
                int index = dirtyIndices[i];
                if (index < 0 || index >= math.min(_terminalCount, terminalStates.Length))
                    continue;

                TerminalStateDTO state = terminalStates[index];
                state.IsDirty = 0;
                terminalStates[index] = state;
            }
        }

        private void TryScheduleFormatJob(int frame)
        {
            if (_formatScheduled ||
                !mockGeneratorEnabled ||
                !TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates))
                return;

            if (frame % _framesBetweenUpdates != 0)
                return;

            UpdateMockSignals((uint)frame);
            if (!TryOpenVaultBuffer(ref _mockPowerSignalHandle, out NativeArray<MockPowerStateSignal> mockPowerSignal) ||
                !TryOpenVaultBuffer(ref _mockDamageSignalHandle, out NativeArray<MockDamageScalarSignal> mockDamageSignal) ||
                !TryOpenVaultBuffer(ref _mockPowerStatusSignalHandle, out NativeArray<MockPowerStatusSignal> mockPowerStatusSignal))
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            long start = Stopwatch.GetTimestamp();
            int formatCount = math.min(_terminalCount, terminalStates.Length);
            if (formatCount <= 0)
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            TerminalStateDTO* statePtr = (TerminalStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(terminalStates);
            _formatHandle = new UpdateTerminalTextJob
            {
                States = statePtr,
                PowerSignals = mockPowerSignal,
                DamageSignals = mockDamageSignal,
                PowerStatusSignals = mockPowerStatusSignal,
                TerminalCount = formatCount,
                Frame = (uint)frame
            }.Schedule(formatCount, 16);
            _formatScheduled = true;
            _lastFormatMainThreadMilliseconds = ElapsedMilliseconds(start);
        }

        private void UpdateMockSignals(uint frame)
        {
            if (!TryOpenVaultBuffer(ref _mockPowerSignalHandle, out NativeArray<MockPowerStateSignal> mockPowerSignal) ||
                !TryOpenVaultBuffer(ref _mockDamageSignalHandle, out NativeArray<MockDamageScalarSignal> mockDamageSignal) ||
                !TryOpenVaultBuffer(ref _mockPowerStatusSignalHandle, out NativeArray<MockPowerStatusSignal> mockPowerStatusSignal))
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            MockTerminalDataGenerator generator = default;
            float power01 = generator.ResolvePower01(frame);
            float damage01 = generator.ResolveDamage01(frame);
            mockPowerSignal[0] = new MockPowerStateSignal { Frame = frame, MockPowerLevel = power01 * 100f };
            mockDamageSignal[0] = new MockDamageScalarSignal { Frame = frame, Damage01 = damage01 };
            mockPowerStatusSignal[0] = generator.ResolvePowerStatus(frame, power01);
            _lastPower01 = power01;
            _lastDamage01 = damage01;
        }

        private void TryScheduleClickResolveJob()
        {
            if (_clickResolveScheduled ||
                !TryOpenVaultBuffer(ref _clickScratchHandle, out NativeArray<TerminalClickSignal> clickScratch) ||
                !TryOpenVaultBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttons))
                return;

            int count = math.min(
                math.min(SignalBus<TerminalClickSignal>.SnapshotCount, TerminalOsConstants.MaxQueuedClicks),
                clickScratch.Length);
            if (count <= 0)
                return;

            NativeArray<TerminalClickSignal>.ReadOnly snapshot = SignalBus<TerminalClickSignal>.GetFrameSnapshotArray();
            for (int i = 0; i < count; i++)
                clickScratch[i] = snapshot[i];

            _clickResolveHandle = new TerminalClickResolveJob
            {
                Clicks = clickScratch.AsReadOnly(),
                ClickCount = count,
                Buttons = buttons,
                ButtonCount = math.min(_buttonCount, buttons.Length),
                Commands = SignalBus<TerminalCommandSignal>.OpenParallelWriter(),
                CommandsBudget = SignalBus<TerminalCommandSignal>.ParallelWriterBudget
            }.Schedule(count, 1);
            _clickResolveScheduled = true;
        }

        private void TryFinalizeClickResolveJob()
        {
            if (!_clickResolveScheduled)
                return;

            if (TryFinalizeCompletedJob(ref _clickResolveHandle))
                _clickResolveScheduled = false;
        }

        private void TryScheduleTerminalInteractionPipeline(int frame, bool refreshAvailabilityFromStates)
        {
            if (_terminalInteractionScheduled ||
                _decryptionScheduled ||
                _terminalCount <= 0 ||
                !TryOpenVaultBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes) ||
                !TryOpenVaultBuffer(ref _gazeRayHandle, out NativeArray<GazeRayDTO> gazeRays) ||
                !TryOpenVaultBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> interactions) ||
                !TryOpenVaultBuffer(ref _terminalInputStatesHandle, out NativeArray<TerminalInputStateDTO> inputStates) ||
                !TryOpenVaultBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttons))
            {
                return;
            }

            int terminalJobCount = math.min(_terminalCount, math.min(terminalPlanes.Length, math.min(interactions.Length, inputStates.Length)));
            if (terminalJobCount <= 0 || gazeRays.Length <= 0)
                return;

            if (refreshAvailabilityFromStates)
                RefreshTerminalPlaneAvailability(terminalPlanes);
            CaptureGazeInputForOwner(
                frame,
                out AbsoluteUniversePosition originAup,
                out float3 forward,
                out float2 scrollDelta,
                out uint interactionFlags);
            RefreshTerminalProjectionTuningFromVault();

            int batchSize = math.clamp((int)math.round(math.lerp(1f, 32f, _globalQualityWeight)), 1, 32);
            float maxDistance = math.max(0.5f, interactionMaxDistanceMeters);
            float viewCone = math.clamp(interactionViewConeCos, -0.5f, 0.95f);
            float quality01 = math.saturate(_globalQualityWeight);
            float curveBlend = math.saturate((math.clamp(_terminalProjectionQualityCurvePower, 0.25f, 4f) - 0.25f) * (1f / 3.75f));
            float shapedQuality = math.lerp(quality01, quality01 * quality01 * quality01, curveBlend);
            float qualityRadius = math.lerp(
                math.max(0.5f, _terminalProjectionLowRadiusMeters),
                math.max(_terminalProjectionLowRadiusMeters, _terminalProjectionUltraRadiusMeters),
                shapedQuality);
            _lastTerminalProjectionEvalRadiusMeters = math.min(maxDistance, qualityRadius);
            _interactionScheduleTicks = Stopwatch.GetTimestamp();

            WriteGazeRaysForOwner(
                gazeRays,
                in originAup,
                forward,
                scrollDelta,
                interactionFlags,
                (uint)frame,
                math.lerp(0.0125f, 0.0005f, _globalQualityWeight) * math.saturate(hologramDistortionIntensity));

            TerminalInputStateDTO* inputStatePtr = (TerminalInputStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(inputStates);
            TerminalInteractionDTO* interactionPtr = (TerminalInteractionDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(interactions);
            _terminalInteractionHandle = new EvaluateTerminalGazeJob
            {
                Planes = terminalPlanes,
                GazeRays = gazeRays,
                Buttons = buttons,
                InputStates = inputStatePtr,
                Interactions = interactionPtr,
                TerminalCount = terminalJobCount,
                ButtonCount = math.min(_buttonCount, buttons.Length),
                GlobalQualityWeight = _globalQualityWeight,
                MaxDistanceMeters = maxDistance,
                ViewConeCos = viewCone,
                CursorSnappingTolerance = terminalCursorSnappingTolerance,
                RaycastThickness = terminalRaycastThickness,
                QualityCurvePower = _terminalProjectionQualityCurvePower,
                LowRadiusMeters = _terminalProjectionLowRadiusMeters,
                UltraRadiusMeters = _terminalProjectionUltraRadiusMeters,
                Frame = (uint)frame,
                Commands = SignalBus<TerminalCommandSignal>.OpenParallelWriter(),
                CommandsBudget = SignalBus<TerminalCommandSignal>.ParallelWriterBudget,
                UiSignals = SignalBus<InteractionUiSignal>.OpenParallelWriter(),
                UiSignalsBudget = SignalBus<InteractionUiSignal>.ParallelWriterBudget
            }.Schedule(terminalJobCount, batchSize);
            _terminalInteractionScheduled = true;
            CompleteTerminalInteractionJobForOwner();
        }

        private static void WriteGazeRaysForOwner(
            NativeArray<GazeRayDTO> gazeRays,
            in AbsoluteUniversePosition originAup,
            float3 forward,
            float2 scrollDelta,
            uint interactionFlags,
            uint frame,
            float microSwayRadians)
        {
            if (!gazeRays.IsCreated || gazeRays.Length <= 0)
                return;

            float3 safeForward = math.normalizesafe(forward, new float3(0f, 0f, 1f));
            float3 side = math.normalizesafe(math.cross(new float3(0f, 1f, 0f), safeForward), new float3(1f, 0f, 0f));
            float3 up = math.normalizesafe(math.cross(safeForward, side), new float3(0f, 1f, 0f));
            float swayScale = math.max(0f, microSwayRadians);
            int count = gazeRays.Length;
            for (int index = 0; index < count; index++)
            {
                float framePhase = ((frame + (uint)(index * 37)) & 2047u) * (1f / 2048f);
                float swayX = ResolveTriangleWave(framePhase) * swayScale;
                float swayY = ResolveTriangleWave(framePhase * 1.6180339f + 0.37f) * swayScale * 0.5f;
                float3 direction = math.normalizesafe(safeForward + side * swayX + up * swayY, safeForward);
                gazeRays[index] = new GazeRayDTO
                {
                    OriginAup = originAup,
                    Direction = direction,
                    InteractionFlags = interactionFlags,
                    Frame = frame,
                    ScrollDelta = scrollDelta
                };
            }
        }

        private static float ResolveTriangleWave(float phase)
        {
            return math.abs(math.frac(phase) * 2f - 1f) * 2f - 1f;
        }

        private void TryFinalizeTerminalInteractionJob()
        {
            if (!_terminalInteractionScheduled)
                return;

            if (!TryFinalizeCompletedJob(ref _terminalInteractionHandle))
                return;

            CompleteTerminalInteractionFinalizeForOwner();
        }

        private void CompleteTerminalInteractionJobForOwner()
        {
            if (!_terminalInteractionScheduled)
                return;

            // OWNER-PHASE FENCE: GazeRays is a shared DataVault read source for other gameplay systems.
            // Keep this presentation/input pipeline inside LateFrameTick so the next simulation frame never sees
            // a pending writer dependency on the gaze buffer.
            bool completed;
            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                completed = DispatcherJobFence.TryComplete(ref _terminalInteractionHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
            }

            if (!completed)
                return;

            CompleteTerminalInteractionFinalizeForOwner();
        }

        private void CompleteTerminalInteractionFinalizeForOwner()
        {
            _terminalInteractionScheduled = false;
            _lastIntersectionMicroseconds = _interactionScheduleTicks > 0
                ? ElapsedMicroseconds(_interactionScheduleTicks)
                : 0f;
            _interactionScheduleTicks = 0;
            AuditLatestInteractions();
            OnTerminalProjectionFinalized();
        }

        private void TryScheduleDecryptionPipeline(int simulationFrame)
        {
            if (!decryptionEnabled ||
                _decryptionScheduled ||
                _terminalCount <= 0 ||
                !TryOpenVaultBuffer(ref _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO> puzzles) ||
                !TryOpenVaultBuffer(ref _decryptionTerminalsHandle, out NativeArray<DecryptionTerminalDTO> decryptionTerminals) ||
                !TryOpenVaultBuffer(ref _decryptionKnobInputHandle, out NativeArray<DecryptionKnobInputDTO> knobInput))
            {
                return;
            }

            if (knobInput.Length == 0)
                return;

            CaptureDecryptionKnobInputForOwner(simulationFrame, knobInput);
            DecryptionKnobInputDTO capturedInput = knobInput.Length > 0 ? knobInput[0] : default;
            bool inputGrabbed =
                (capturedInput.Flags & (TerminalOsConstants.DecryptionKnobFlagActive | TerminalOsConstants.DecryptionKnobFlagGrab)) ==
                (TerminalOsConstants.DecryptionKnobFlagActive | TerminalOsConstants.DecryptionKnobFlagGrab);
            int evaluationStride = inputGrabbed ? 1 : math.max(1, _decryptionFramesBetweenEvaluations);
            if (evaluationStride > 1 && simulationFrame % evaluationStride != 0)
                return;

            int puzzleCount = math.min(_terminalCount, math.min(puzzles.Length, decryptionTerminals.Length));
            if (puzzleCount <= 0)
                return;

            DecryptionPuzzleDTO* puzzlePtr = (DecryptionPuzzleDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(puzzles);
            _decryptionScheduleTicks = Stopwatch.GetTimestamp();
            _decryptionScheduleFrame = (uint)simulationFrame;

            _decryptionHandle = new EvaluateDecryptionPipelineJob
            {
                Puzzles = puzzlePtr,
                Terminals = decryptionTerminals,
                Inputs = knobInput,
                PuzzleCount = puzzleCount,
                FrequencySensitivity = decryptionFrequencySensitivity,
                PhaseSensitivity = decryptionPhaseSensitivity,
                FreqWeight = decryptionFrequencyWeight,
                PhaseWeight = decryptionPhaseWeight,
                SolveThreshold01 = 1f - math.clamp(decryptionSnapTolerance01, 0.001f, 0.2f),
                Frame = (uint)simulationFrame,
                StepFrames = (uint)evaluationStride,
                UnlockedSignals = SignalBus<TerminalUnlockedSignal>.OpenParallelWriter(),
                UnlockedSignalsBudget = SignalBus<TerminalUnlockedSignal>.ParallelWriterBudget
            }.Schedule();
            _decryptionScheduled = true;
            CompleteDecryptionJobForOwner();
        }

        private void TryFinalizeDecryptionJob()
        {
            if (!_decryptionScheduled)
                return;

            if (!TryFinalizeCompletedJob(ref _decryptionHandle))
                return;

            CompleteDecryptionFinalizeForOwner();
        }

        private void CompleteDecryptionJobForOwner()
        {
            if (!_decryptionScheduled)
                return;

            // OWNER-PHASE FENCE: decryption reads terminal input, gaze, and terminal lanes captured from
            // the settled presentation state. Leaving it pending lets the next gaze writer collide with
            // those shared DataVault buffers, so this tiny puzzle pass closes inside LateFrameTick.
            bool completed;
            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                completed = DispatcherJobFence.TryComplete(ref _decryptionHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
            }

            if (!completed)
                return;

            CompleteDecryptionFinalizeForOwner();
        }

        private void CompleteDecryptionFinalizeForOwner()
        {
            _decryptionScheduled = false;
            int simulationFrame = ResolveScheduledDecryptionFrameForOwner();
            _lastDecryptionBurstMicroseconds = _decryptionScheduleTicks > 0
                ? ElapsedMicroseconds(_decryptionScheduleTicks)
                : 0f;
            _decryptionScheduleTicks = 0;
            _decryptionScheduleFrame = 0u;
            uint faultFlags = 0u;
            if (_lastDecryptionBurstMicroseconds > 100f)
                faultFlags |= FaultDecryptionBudget;
            if (ScanDecryptionNonFiniteFault())
                faultFlags |= FaultDecryptionNonFinite;

            _lastDecryptionFaultFlags = faultFlags;
            RecordSolvedDecryptionDialogueChoice();
            RecordDecryptionTelemetry(simulationFrame, faultFlags);
            _decryptionBufferUploadDirty = true;
            UploadDecryptionPuzzles();
            if ((faultFlags & (FaultDecryptionBudget | FaultDecryptionNonFinite)) != 0u)
                QueueDecryptionBlackBoxDump(faultFlags);
        }

        private void RecordSolvedDecryptionDialogueChoice()
        {
            SaveManager saveManager = _cachedSaveManager;
            if (!IsSaveManagerUsable(saveManager) ||
                (saveManager.PlayerDialogueChoiceFlags & DialogueDecisionSaveFacilityMask) != 0)
            {
                return;
            }

            if (!TryReadVaultBuffer(in _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO>.ReadOnly puzzles))
                return;

            int count = math.min(_terminalCount, puzzles.Length);
            for (int i = 0; i < count; i++)
            {
                if ((puzzles[i].Flags & TerminalOsConstants.DecryptionFlagSolved) == 0u)
                    continue;

                saveManager.RecordPlayerDialogueChoiceFlag(DialogueDecisionSaveFacilityMask);
                _lastUploadedDialogueChoiceFlags = ushort.MaxValue;
                return;
            }
        }

        private void CaptureDecryptionKnobInputForOwner(int simulationFrame, NativeArray<DecryptionKnobInputDTO> knobInput)
        {
            if (!knobInput.IsCreated || knobInput.Length == 0)
                return;

            DecryptionKnobInputDTO input = new DecryptionKnobInputDTO
            {
                Flags = TerminalOsConstants.DecryptionKnobFlagActive,
                DeltaTime = HectonPhysicsContract.FixedDeltaTimeSeconds,
                Frame = (uint)simulationFrame
            };

            float2 analogDeltaSource = default;
            if (TryReadVaultBuffer(in _gazeRayHandle, out NativeArray<GazeRayDTO>.ReadOnly gazeRays) && gazeRays.Length > 0)
            {
                GazeRayDTO gaze = gazeRays[0];
                input.PlayerAupMeters = gaze.OriginAup.ToAbsoluteDouble3();
                analogDeltaSource = gaze.ScrollDelta;
            }
            else
            {
                knobInput[0] = input;
                return;
            }

            if (!TryFindHoveredTerminalForDecryption(out TerminalInteractionDTO interaction))
            {
                knobInput[0] = input;
                return;
            }

            float analogDelta = math.abs(analogDeltaSource.y) > 0.000001f
                ? analogDeltaSource.y
                : analogDeltaSource.x;
            bool held = (interaction.InteractionFlags & (TerminalOsConstants.InteractionFlagHold |
                                                         TerminalOsConstants.InteractionFlagPress |
                                                         TerminalOsConstants.InteractionFlagScroll)) != 0u;
            if (held && math.abs(analogDelta) > 0.000001f)
            {
                input.Flags |= TerminalOsConstants.DecryptionKnobFlagGrab;
                if (interaction.LocalHitUV.x < 0.5f)
                {
                    input.Flags |= TerminalOsConstants.DecryptionKnobFlagFrequency;
                    input.FrequencyDelta = analogDelta;
                }
                else
                {
                    input.Flags |= TerminalOsConstants.DecryptionKnobFlagPhase;
                    input.PhaseDelta = analogDelta;
                }
            }

            input.TerminalHash = interaction.TerminalHash;
            knobInput[0] = input;
        }

        private bool TryFindHoveredTerminalForDecryption(out TerminalInteractionDTO bestInteraction)
        {
            bestInteraction = default;
            if (!TryOpenVaultBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> interactions))
                return false;

            float closest = float.MaxValue;
            int count = math.min(_terminalCount, interactions.Length);
            for (int i = 0; i < count; i++)
            {
                TerminalInteractionDTO interaction = interactions[i];
                if ((interaction.InteractionFlags & TerminalOsConstants.InteractionFlagHover) == 0u ||
                    interaction.TerminalHash == 0u ||
                    !math.isfinite(interaction.Distance) ||
                    interaction.Distance >= closest)
                {
                    continue;
                }

                closest = interaction.Distance;
                bestInteraction = interaction;
            }

            return bestInteraction.TerminalHash != 0u;
        }

        private bool ScanDecryptionNonFiniteFault()
        {
            if (!TryOpenVaultBuffer(ref _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO> puzzles))
                return false;

            int count = math.min(_terminalCount, puzzles.Length);
            for (int i = 0; i < count; i++)
            {
                DecryptionPuzzleDTO puzzle = puzzles[i];
                if ((puzzle.Flags & TerminalOsConstants.DecryptionFlagNonFinite) != 0u ||
                    !math.isfinite(puzzle.PlayerFrequency) ||
                    !math.isfinite(puzzle.PlayerPhase) ||
                    !math.isfinite(puzzle.TargetFrequency) ||
                    !math.isfinite(puzzle.TargetPhase) ||
                    !math.isfinite(puzzle.AlignmentAccuracy01))
                {
                    return true;
                }
            }

            return false;
        }

        private void AuditLatestInteractions()
        {
            _lastHoveredTerminalHash = 0u;
            _lastEvaluatedTerminalCount = 0;
            if (!TryOpenVaultBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> interactions))
                return;

            float closest = float.MaxValue;
            int count = math.min(_terminalCount, interactions.Length);
            for (int i = 0; i < count; i++)
            {
                TerminalInteractionDTO interaction = interactions[i];
                if ((interaction.InteractionFlags & TerminalOsConstants.InteractionFlagNonFinite) != 0u)
                    _lastFaultFlags |= FaultNonFinite;

                if ((interaction.InteractionFlags & TerminalOsConstants.InteractionFlagHover) == 0u)
                    continue;

                _lastEvaluatedTerminalCount++;
                if (interaction.Distance < closest)
                {
                    closest = interaction.Distance;
                    _lastHoveredTerminalHash = interaction.TerminalHash;
                }
            }
        }

        private void RefreshTerminalPlaneAvailability(NativeArray<TerminalPlaneDTO> terminalPlanes)
        {
            if (!TryReadVaultBuffer(in _terminalStatesHandle, out NativeArray<TerminalStateDTO>.ReadOnly terminalStates))
                return;

            int count = math.min(_terminalCount, math.min(terminalPlanes.Length, terminalStates.Length));
            for (int i = 0; i < count; i++)
            {
                TerminalPlaneDTO plane = terminalPlanes[i];
                TerminalStateDTO state = terminalStates[i];
                float statePower01 = math.saturate(math.isfinite(state.Value1) ? state.Value1 : 0f);
                float routedPower01 = math.saturate(math.isfinite(plane.Power01) ? plane.Power01 : statePower01);
                float power01 = math.min(statePower01, routedPower01);
                uint flags = plane.Flags | TerminalOsConstants.PlaneFlagActive;
                if (power01 > 0.001f)
                    flags |= TerminalOsConstants.PlaneFlagPowered;
                else
                    flags &= ~TerminalOsConstants.PlaneFlagPowered;

                if (plane.Submerged01 > 0.5f)
                    flags |= TerminalOsConstants.PlaneFlagSubmerged;
                else
                    flags &= ~TerminalOsConstants.PlaneFlagSubmerged;

                plane.TerminalHash = state.TerminalHash != 0u ? state.TerminalHash : plane.TerminalHash;
                plane.Power01 = power01;
                plane.Flags = flags;
                terminalPlanes[i] = plane;
            }
        }

        private void CaptureGazeInputForOwner(
            int frame,
            out AbsoluteUniversePosition originAup,
            out float3 forward,
            out float2 scrollDelta,
            out uint interactionFlags)
        {
            CaptureGazePoseForOwner(frame, out originAup, out forward);
            interactionFlags = 0u;
            scrollDelta = default;

            if (_input == null || !_input.IsInitialized)
                return;

            PlayerInputState state = _input.GetState();
            Vector2 scroll = state.ScrollDelta;
            scrollDelta = SanitizeVector2(scroll);
            bool pressed = state.HasAction(PlayerInputAction.Interact) || state.HasAction(PlayerInputAction.PrimaryFire);
            if (pressed && !_inputPressedLastFrame)
                interactionFlags |= TerminalOsConstants.InteractionFlagPress;
            if (pressed)
                interactionFlags |= TerminalOsConstants.InteractionFlagHold;
            if (!pressed && _inputPressedLastFrame)
                interactionFlags |= TerminalOsConstants.InteractionFlagRelease;
            if (math.lengthsq(scrollDelta) > 0.000001f)
                interactionFlags |= TerminalOsConstants.InteractionFlagScroll;
            _inputPressedLastFrame = pressed;
        }

        private void CaptureGazePoseForOwner(int frame, out AbsoluteUniversePosition originAup, out float3 forward)
        {
            Camera camera = RefreshAttentionCameraForOwner(frame);
            if (camera != null)
            {
                Transform cameraTransform = camera.transform;
                Vector3 position = cameraTransform.position;
                Vector3 direction = cameraTransform.forward;
                if (VectorFinite(position) && VectorFinite(direction))
                {
                    if (TryResolveAupFromRuntimeOrigin(position, out originAup))
                    {
                        forward = math.normalizesafe((float3)(direction), new float3(0f, 0f, 1f));
                        return;
                    }
                }

                _lastFaultFlags |= FaultNonFinite;
            }

            Vector3 fallbackPosition = transform.position;
            Vector3 fallbackForward = transform.forward;
            Vector3 safeFallbackPosition = VectorFinite(fallbackPosition) ? fallbackPosition : Vector3.zero;
            if (!TryResolveAupFromRuntimeOrigin(safeFallbackPosition, out originAup))
                originAup = default;
            forward = VectorFinite(fallbackForward)
                ? math.normalizesafe((float3)(fallbackForward), new float3(0f, 0f, 1f))
                : new float3(0f, 0f, 1f);
        }

        private void TryMonitorLayoutCsv(int frame)
        {
#if !UNITY_EDITOR
            return;
#else
            if (string.IsNullOrEmpty(_csvFullPath))
                return;

            if (frame < _csvProbeFrame)
                return;

            _csvProbeFrame = frame + math.clamp((int)math.round(math.lerp(30f, 120f, 1f - _globalQualityWeight)), 30, 120);
            if (!File.Exists(_csvFullPath))
                return;

            DateTime writeUtc = File.GetLastWriteTimeUtc(_csvFullPath);
            if (writeUtc <= _csvLastWriteUtc)
                return;

            _csvLastWriteUtc = writeUtc;
            Span<byte> csvScratch = stackalloc byte[8192];
            int bytesRead;
            using (FileStream stream = new FileStream(_csvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = stream.Read(csvScratch);
            }

            if (bytesRead > 0 && ParseLayoutCsv(csvScratch.Slice(0, bytesRead)))
            {
                _layoutUploadDirty = true;
                ForceAllDirty();
            }
#endif
        }

        private void TryMonitorDecryptionCsv(int frame)
        {
#if !UNITY_EDITOR
            return;
#else
            if (string.IsNullOrEmpty(_decryptionCsvFullPath))
                return;

            if (frame < _decryptionCsvProbeFrame)
                return;

            _decryptionCsvProbeFrame = frame + math.clamp((int)math.round(math.lerp(30f, 120f, 1f - _globalQualityWeight)), 30, 120);
            if (!File.Exists(_decryptionCsvFullPath))
                return;

            DateTime writeUtc = File.GetLastWriteTimeUtc(_decryptionCsvFullPath);
            if (writeUtc <= _decryptionCsvLastWriteUtc)
                return;

            _decryptionCsvLastWriteUtc = writeUtc;
            Span<byte> csvScratch = stackalloc byte[8192];
            int bytesRead;
            using (FileStream stream = new FileStream(_decryptionCsvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = stream.Read(csvScratch);
            }

            if (bytesRead > 0 && ParseDecryptionPuzzleCsv(csvScratch.Slice(0, bytesRead)))
            {
                _decryptionBufferUploadDirty = true;
                _bindingsDirty = true;
                UploadDecryptionPuzzles();
            }
#endif
        }

#if UNITY_EDITOR
        private bool ParseDecryptionPuzzleCsv(ReadOnlySpan<byte> bytes)
        {
            bool changed = false;
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                bool lineEnd = i == bytes.Length || bytes[i] == (byte)'\n' || bytes[i] == (byte)'\r';
                if (!lineEnd)
                    continue;

                if (i > lineStart)
                    changed |= TryParseDecryptionPuzzleLine(bytes, lineStart, i);
                lineStart = i + 1;
            }

            return changed;
        }

        private bool TryParseDecryptionPuzzleLine(ReadOnlySpan<byte> bytes, int start, int end)
        {
            int terminalEnd = FindCsvComma(bytes, start, end);
            if (terminalEnd <= start)
                return false;

            int frequencyEnd = FindCsvComma(bytes, terminalEnd + 1, end);
            if (frequencyEnd <= terminalEnd)
                return false;

            int phaseEnd = FindCsvComma(bytes, frequencyEnd + 1, end);
            int targetPhaseEnd = phaseEnd > frequencyEnd ? phaseEnd : end;
            uint terminalHash = ParseHashOrName(bytes, start, terminalEnd);
            if (terminalHash == 0u ||
                !TryParseFloat(bytes, terminalEnd + 1, frequencyEnd, out float targetFrequency) ||
                !TryParseFloat(bytes, frequencyEnd + 1, targetPhaseEnd, out float targetPhase))
            {
                return false;
            }

            int index = FindTerminalIndex(terminalHash);
            if (index < 0 || !TryOpenVaultBuffer(ref _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO> puzzles))
                return false;

            DecryptionPuzzleDTO puzzle = puzzles[index];
            puzzle.TargetFrequency = math.clamp(targetFrequency, 0.1f, 12f);
            puzzle.TargetPhase = WrapPhase(targetPhase);
            if (phaseEnd > frequencyEnd)
            {
                int playerFrequencyEnd = FindCsvComma(bytes, phaseEnd + 1, end);
                int playerPhaseEnd = playerFrequencyEnd > phaseEnd ? FindCsvComma(bytes, playerFrequencyEnd + 1, end) : -1;
                int playerFrequencyFieldEnd = playerFrequencyEnd > phaseEnd ? playerFrequencyEnd : end;
                if (TryParseFloat(bytes, phaseEnd + 1, playerFrequencyFieldEnd, out float playerFrequency))
                    puzzle.PlayerFrequency = math.clamp(playerFrequency, 0.1f, 12f);

                if (playerFrequencyEnd > phaseEnd)
                {
                    int playerPhaseFieldEnd = playerPhaseEnd > playerFrequencyEnd ? playerPhaseEnd : end;
                    if (TryParseFloat(bytes, playerFrequencyEnd + 1, playerPhaseFieldEnd, out float playerPhase))
                        puzzle.PlayerPhase = WrapPhase(playerPhase);
                }
            }

            puzzle.Flags |= TerminalOsConstants.DecryptionFlagActive | TerminalOsConstants.DecryptionFlagInitialized;
            puzzle.Flags &= ~(TerminalOsConstants.DecryptionFlagSolved |
                              TerminalOsConstants.DecryptionFlagNonFinite |
                              TerminalOsConstants.DecryptionHoldFrameMask);
            puzzles[index] = puzzle;
            return true;
        }

        private bool ParseLayoutCsv(ReadOnlySpan<byte> bytes)
        {
            bool changed = false;
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                bool lineEnd = i == bytes.Length || bytes[i] == (byte)'\n' || bytes[i] == (byte)'\r';
                if (!lineEnd)
                    continue;

                if (i > lineStart)
                    changed |= TryParseLayoutLine(bytes, lineStart, i);
                lineStart = i + 1;
            }

            return changed;
        }

        private bool TryParseLayoutLine(ReadOnlySpan<byte> bytes, int start, int end)
        {
            int a = FindCsvComma(bytes, start, end);
            if (a <= start)
                return false;
            int b = FindCsvComma(bytes, a + 1, end);
            int c = FindCsvComma(bytes, b + 1, end);
            if (b <= a || c <= b)
                return false;

            int d = FindCsvComma(bytes, c + 1, end);
            int e = d > c ? FindCsvComma(bytes, d + 1, end) : -1;
            if (d > c && e > d)
                return TryParseButtonLayoutLine(bytes, start, a, b, c, d, e, end);

            uint hash = ParseHashOrName(bytes, start, a);
            if (!TryParseFloat(bytes, a + 1, b, out float x) ||
                !TryParseFloat(bytes, b + 1, c, out float y) ||
                !TryParseFloat(bytes, c + 1, end, out float scale))
                return false;

            int index = FindTerminalIndex(hash);
            if (index < 0)
                return false;

            if (!TryOpenVaultBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands))
                return false;

            ScreenCommandDTO command = screenCommands[index];
            command.Position = SanitizeUv01(new float2(x, y));
            command.Scale = SanitizeScale(scale);
            screenCommands[index] = command;
            return true;
        }

        private bool TryParseButtonLayoutLine(ReadOnlySpan<byte> bytes, int start, int terminalEnd, int xEnd, int yEnd, int widthEnd, int heightEnd, int end)
        {
            uint terminalHash = ParseHashOrName(bytes, start, terminalEnd);
            uint actionHash = ParseHashOrName(bytes, heightEnd + 1, end);
            if (!TryParseFloat(bytes, terminalEnd + 1, xEnd, out float x) ||
                !TryParseFloat(bytes, xEnd + 1, yEnd, out float y) ||
                !TryParseFloat(bytes, yEnd + 1, widthEnd, out float width) ||
                !TryParseFloat(bytes, widthEnd + 1, heightEnd, out float height) ||
                actionHash == 0u)
            {
                return false;
            }

            if (!TryOpenVaultBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttons))
                return false;

            float2 min = SanitizeUv01(new float2(x, y));
            float2 max = SanitizeUv01(new float2(x + math.max(0.001f, width), y + math.max(0.001f, height)));
            float4 rect = new float4(
                math.min(min.x, max.x),
                math.min(min.y, max.y),
                math.max(min.x, max.x),
                math.max(min.y, max.y));
            int fallbackIndex = -1;
            for (int i = 0; i < _buttonCount; i++)
            {
                ButtonAABBDTO button = buttons[i];
                if (button.TerminalHash != terminalHash)
                    continue;

                if (button.CommandHash == actionHash)
                {
                    button.RectUv = rect;
                    button.Flags |= TerminalOsConstants.ButtonFlagEnabled;
                    buttons[i] = button;
                    return true;
                }

                if (fallbackIndex < 0)
                    fallbackIndex = i;
            }

            if (fallbackIndex < 0)
                return false;

            ButtonAABBDTO fallback = buttons[fallbackIndex];
            fallback.RectUv = rect;
            fallback.CommandHash = actionHash;
            fallback.Flags |= TerminalOsConstants.ButtonFlagEnabled;
            buttons[fallbackIndex] = fallback;
            return true;
        }

        private static int FindCsvComma(ReadOnlySpan<byte> bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (bytes[i] == (byte)',')
                    return i;
            }

            return -1;
        }

        private static uint ParseHashOrName(ReadOnlySpan<byte> bytes, int start, int end)
        {
            TrimCsvField(bytes, ref start, ref end);
            uint numeric = 0u;
            bool numericOnly = end > start;
            for (int i = start; i < end; i++)
            {
                byte value = bytes[i];
                if (value < (byte)'0' || value > (byte)'9')
                {
                    numericOnly = false;
                    break;
                }

                numeric = numeric * 10u + (uint)(value - (byte)'0');
            }

            if (numericOnly)
                return numeric;

            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte value = bytes[i];
                if (value > 32)
                    hash = TerminalOsHash.Fnv1A(value, hash);
            }

            return hash == 0u ? 1u : hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            TrimCsvField(bytes, ref start, ref end);
            if (end <= start)
                return false;

            int i = start;
            bool negative = false;
            if (bytes[i] == (byte)'-')
            {
                negative = true;
                i++;
            }

            float result = 0f;
            bool any = false;
            for (; i < end; i++)
            {
                byte c = bytes[i];
                if (c == (byte)'.')
                {
                    i++;
                    break;
                }

                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                result = result * 10f + (c - (byte)'0');
                any = true;
            }

            float decimalScale = 0.1f;
            for (; i < end; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                result += (c - (byte)'0') * decimalScale;
                decimalScale *= 0.1f;
                any = true;
            }

            if (!any)
                return false;

            value = negative ? -result : result;
            return math.isfinite(value);
        }

        private static void TrimCsvField(ReadOnlySpan<byte> bytes, ref int start, ref int end)
        {
            while (start < end && bytes[start] <= 32)
                start++;
            while (end > start && bytes[end - 1] <= 32)
                end--;
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapPhase(float value)
        {
            const float twoPi = math.PI * 2f;
            float safe = math.isfinite(value) ? value : 0f;
            return safe - math.floor(safe * math.rcp(twoPi)) * twoPi;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SafeVectorLength(float3 value, float fallback)
        {
            float lengthSq = math.dot(value, value);
            return math.select(
                fallback,
                math.max(fallback, lengthSq * math.rsqrt(math.max(lengthSq, 0.000001f))),
                math.isfinite(lengthSq) && lengthSq > 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 SanitizeUv01(float2 value)
        {
            return math.all(math.isfinite(value)) ? math.saturate(value) : new float2(0.055f, 0.63f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeScale(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0.025f, 0.25f) : 0.075f;
        }

        private static bool MatchesPowerLevelToken(ReadOnlySpan<byte> bytes, int offset)
        {
            return offset + 12 < bytes.Length &&
                   bytes[offset] == (byte)'^' &&
                   bytes[offset + 1] == (byte)'P' &&
                   bytes[offset + 2] == (byte)'O' &&
                   bytes[offset + 3] == (byte)'W' &&
                   bytes[offset + 4] == (byte)'E' &&
                   bytes[offset + 5] == (byte)'R' &&
                   bytes[offset + 6] == (byte)'_' &&
                   bytes[offset + 7] == (byte)'L' &&
                   bytes[offset + 8] == (byte)'E' &&
                   bytes[offset + 9] == (byte)'V' &&
                   bytes[offset + 10] == (byte)'E' &&
                   bytes[offset + 11] == (byte)'L' &&
                   bytes[offset + 12] == (byte)'^';
        }

        private int FindTerminalIndex(uint hash)
        {
            if (!TryReadVaultBuffer(in _terminalStatesHandle, out NativeArray<TerminalStateDTO>.ReadOnly terminalStates))
                return -1;

            int count = math.min(_terminalCount, terminalStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (terminalStates[i].TerminalHash == hash)
                    return i;
            }

            return -1;
        }

        private void UpdatePanelInstancesIfNeeded()
        {
            if (!drawPanelsInstanced ||
                !TryOpenVaultBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> panelInstances))
                return;

            bool hasPlanes = TryOpenVaultBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes);
            bool hasStates = TryReadVaultBuffer(in _terminalStatesHandle, out NativeArray<TerminalStateDTO>.ReadOnly terminalStates);
            float quality = _globalQualityWeight;
            float glitch = _lastDiegeticGlitchIntensity;
            bool scalarChanged = math.abs(_lastPanelInstanceQualityWeight - quality) > 0.0005f ||
                                 math.abs(_lastPanelInstanceGlitchIntensity - glitch) > 0.0005f;
            bool forceRewrite = _panelInstanceUploadDirty || scalarChanged;
            bool changed = forceRewrite;
            int count = math.min(_terminalCount, math.min(ResolveBoundPanelCount(), panelInstances.Length));
            for (int i = 0; i < count; i++)
            {
                Transform terminal = ResolveTerminalTransform(i);
                if (terminal == null)
                    continue;

                float4x4 matrix = ToFloat4x4(terminal.localToWorldMatrix);
                if (!MatrixFinite(matrix))
                {
                    _lastFaultFlags |= FaultNonFinite;
                    continue;
                }

                if (MatrixEquals(panelInstances[i].LocalToWorld, matrix) && !forceRewrite)
                    continue;

                panelInstances[i] = new TerminalPanelInstanceDTO
                {
                    LocalToWorld = matrix,
                    SliceFlags = new float4(i, 1f - quality, quality, glitch)
                };
                if (hasPlanes && i < terminalPlanes.Length)
                {
                    TerminalPlaneDTO previous = terminalPlanes[i];
                    TerminalStateDTO state = hasStates && i < terminalStates.Length ? terminalStates[i] : default;
                    uint terminalHash = state.TerminalHash != 0u ? state.TerminalHash : TerminalOsHash.HashIndex(i);
                    float statePower01 = state.TerminalHash != 0u ? state.Value1 : previous.Power01;
                    float power01 = math.min(
                        math.saturate(math.isfinite(statePower01) ? statePower01 : 0f),
                        math.saturate(math.isfinite(previous.Power01) ? previous.Power01 : 1f));
                    TerminalPlaneDTO refreshedPlane = BuildTerminalPlane(
                        matrix,
                        terminalHash,
                        previous.LayoutFirstButton,
                        previous.LayoutButtonCount,
                        power01,
                        previous.Submerged01);
                    terminalPlanes[i] = refreshedPlane;
                    RefreshDecryptionTerminalFromPlane(i, in refreshedPlane);
                }
                changed = true;
            }

            if (!changed)
                return;

            RecalculatePanelRenderBounds();
            if (UploadPanelInstances())
            {
                _lastPanelInstanceQualityWeight = quality;
                _lastPanelInstanceGlitchIntensity = glitch;
            }
        }

        private void RefreshDecryptionTerminalFromPlane(int index, in TerminalPlaneDTO plane)
        {
            if (!TryOpenVaultBuffer(ref _decryptionTerminalsHandle, out NativeArray<DecryptionTerminalDTO> decryptionTerminals) ||
                index < 0 ||
                index >= decryptionTerminals.Length)
            {
                return;
            }

            DecryptionTerminalDTO terminal = decryptionTerminals[index];
            terminal.TerminalAupMeters = plane.CenterAup.ToAbsoluteDouble3();
            terminal.TerminalHash = plane.TerminalHash != 0u ? plane.TerminalHash : terminal.TerminalHash;
            terminal.InteractionRadiusMeters = math.max(0.1f, terminal.InteractionRadiusMeters);
            terminal.Flags |= TerminalOsConstants.DecryptionFlagActive | TerminalOsConstants.DecryptionFlagInitialized;
            decryptionTerminals[index] = terminal;
        }

        private void RenderInstancedPanels()
        {
            if (!drawPanelsInstanced ||
                terminalArrayMaterial == null ||
                terminalPanelMesh == null ||
                _panelInstanceBuffer == null ||
                _terminalTextureArray == null ||
                _terminalCount <= 0)
            {
                return;
            }

            RenderParams renderParams = new RenderParams(terminalArrayMaterial)
            {
                worldBounds = _panelRenderBounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer
            };
            UnityEngine.Graphics.RenderMeshPrimitives(renderParams, terminalPanelMesh, 0, _terminalCount);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawTerminalDebugGizmos ||
                !TryReadVaultBuffer(in _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO>.ReadOnly terminalPlanes) ||
                !TryReadVaultBuffer(in _buttonAabbHandle, out NativeArray<ButtonAABBDTO>.ReadOnly buttons))
            {
                return;
            }

            TryReadVaultBuffer(in _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO>.ReadOnly interactions);
            TryReadVaultBuffer(in _terminalInputStatesHandle, out NativeArray<TerminalInputStateDTO>.ReadOnly inputStates);
            TryReadVaultBuffer(in _gazeRayHandle, out NativeArray<GazeRayDTO>.ReadOnly gazeRays);
            TryReadVaultBuffer(in _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO>.ReadOnly puzzles);
            int count = math.min(_terminalCount, terminalPlanes.Length);
            for (int i = 0; i < count; i++)
            {
                TerminalInteractionDTO interaction = interactions.IsCreated && i < interactions.Length ? interactions[i] : default;
                TerminalInputStateDTO inputState = inputStates.IsCreated && i < inputStates.Length ? inputStates[i] : default;
                TerminalPlaneDTO plane = terminalPlanes[i];
                DrawTerminalPlaneGizmo(in plane, buttons, in interaction);
                DrawTerminalInputProjectionGizmo(in plane, in inputState, gazeRays);
                if (puzzles.IsCreated && i < puzzles.Length)
                {
                    DecryptionPuzzleDTO puzzle = puzzles[i];
                    DrawDecryptionWaveGizmo(in plane, in puzzle);
                }
            }
        }

        private static void DrawTerminalPlaneGizmo(
            in TerminalPlaneDTO plane,
            NativeArray<ButtonAABBDTO>.ReadOnly buttons,
            in TerminalInteractionDTO interaction)
        {
            float3 center = ResolveRuntimeLocalPosition(plane.CenterAup, default);
            float3 right = math.normalizesafe(plane.Right, new float3(1f, 0f, 0f));
            float3 up = math.normalizesafe(plane.Up, new float3(0f, 1f, 0f));
            float3 halfRight = right * (math.max(0.001f, plane.Width) * 0.5f);
            float3 halfUp = up * (math.max(0.001f, plane.Height) * 0.5f);

            Gizmos.color = (plane.Flags & TerminalOsConstants.PlaneFlagPowered) != 0u
                ? new Color(0f, 0.85f, 1f, 0.85f)
                : new Color(0.35f, 0.35f, 0.35f, 0.65f);
            DrawGizmoRect(center, halfRight, halfUp);

            uint first = plane.LayoutFirstButton;
            uint last = math.min(first + plane.LayoutButtonCount, (uint)buttons.Length);
            Gizmos.color = new Color(1f, 0.82f, 0.14f, 0.85f);
            for (uint i = first; i < last; i++)
            {
                ButtonAABBDTO button = buttons[(int)i];
                if ((button.Flags & TerminalOsConstants.ButtonFlagEnabled) == 0u)
                    continue;

                float4 rect = button.RectUv;
                float3 buttonCenter = center +
                    right * (((rect.x + rect.z) * 0.5f) - 0.5f) * plane.Width +
                    up * (((rect.y + rect.w) * 0.5f) - 0.5f) * plane.Height;
                float3 buttonHalfRight = right * (math.max(0.001f, rect.z - rect.x) * plane.Width * 0.5f);
                float3 buttonHalfUp = up * (math.max(0.001f, rect.w - rect.y) * plane.Height * 0.5f);
                DrawGizmoRect(buttonCenter, buttonHalfRight, buttonHalfUp);
            }

            if ((interaction.InteractionFlags & TerminalOsConstants.InteractionFlagHover) == 0u)
                return;

            Gizmos.color = Color.magenta;
            float2 uv = interaction.LocalHitUV;
            float3 hit = center + right * ((uv.x - 0.5f) * plane.Width) + up * ((uv.y - 0.5f) * plane.Height);
            Gizmos.DrawWireSphere(ToVector3(hit), 0.035f);
        }

        private static void DrawDecryptionWaveGizmo(in TerminalPlaneDTO plane, in DecryptionPuzzleDTO puzzle)
        {
            if ((puzzle.Flags & TerminalOsConstants.DecryptionFlagActive) == 0u)
                return;

            float3 center = ResolveRuntimeLocalPosition(plane.CenterAup, default);
            float3 right = math.normalizesafe(plane.Right, new float3(1f, 0f, 0f));
            float3 up = math.normalizesafe(plane.Up, new float3(0f, 1f, 0f));
            Gizmos.color = new Color(1f, 0.55f, 0.16f, 0.7f);
            DrawWaveGizmo(center, right, up, plane.Width, plane.Height, puzzle.TargetFrequency, puzzle.TargetPhase);
            Gizmos.color = new Color(0.18f, 0.92f, 1f, 0.85f);
            DrawWaveGizmo(center, right, up, plane.Width, plane.Height, puzzle.PlayerFrequency, puzzle.PlayerPhase);
        }

        private static void DrawWaveGizmo(float3 center, float3 right, float3 up, float width, float height, float frequency, float phase)
        {
            const int segmentCount = 24;
            float safeFrequency = math.clamp(math.isfinite(frequency) ? frequency : 1f, 0.1f, 12f);
            float safePhase = math.isfinite(phase) ? phase : 0f;
            Vector3 previous = default;
            for (int i = 0; i <= segmentCount; i++)
            {
                float x01 = i * (1f / segmentCount);
                float wave = MathLodApproximation.ApproxSinBhaskara((x01 * safeFrequency + safePhase) * math.PI * 2f) * 0.16f;
                float3 point = center +
                    right * ((x01 - 0.5f) * width) +
                    up * ((0.46f + wave - 0.5f) * height);
                Vector3 current = ToVector3(point);
                if (i > 0)
                    Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }

        private static void DrawGizmoRect(float3 center, float3 halfRight, float3 halfUp)
        {
            Vector3 a = ToVector3(center - halfRight - halfUp);
            Vector3 b = ToVector3(center + halfRight - halfUp);
            Vector3 c = ToVector3(center + halfRight + halfUp);
            Vector3 d = ToVector3(center - halfRight + halfUp);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
#endif

        private void RecalculatePanelRenderBounds()
        {
            if (!TryReadVaultBuffer(in _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO>.ReadOnly panelInstances) ||
                _terminalCount <= 0)
            {
                _panelRenderBounds = new Bounds(transform.position, Vector3.one);
                return;
            }

            float3 minBounds = new float3(float.MaxValue);
            float3 maxBounds = new float3(float.MinValue);
            int validCount = 0;
            int count = math.min(_terminalCount, panelInstances.Length);
            for (int i = 0; i < count; i++)
            {
                float3 center = panelInstances[i].LocalToWorld.c3.xyz;
                if (!math.all(math.isfinite(center)))
                    continue;

                minBounds = math.min(minBounds, center);
                maxBounds = math.max(maxBounds, center);
                validCount++;
            }

            if (validCount == 0)
            {
                _lastFaultFlags |= FaultNonFinite;
                _panelRenderBounds = new Bounds(transform.position, Vector3.one);
                return;
            }

            float3 size = math.max(maxBounds - minBounds, new float3(1f, 1f, 1f)) + new float3(2f, 2f, 2f);
            float3 centerBounds = (minBounds + maxBounds) * 0.5f;
            _panelRenderBounds = new Bounds(
                new Vector3(centerBounds.x, centerBounds.y, centerBounds.z),
                new Vector3(size.x, size.y, size.z));
        }

        private static float4x4 ToFloat4x4(Matrix4x4 matrix)
        {
            return new float4x4(
                new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
                new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
                new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
                new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33));
        }

        private static bool MatrixEquals(in float4x4 lhs, in float4x4 rhs)
        {
            return math.all(lhs.c0 == rhs.c0) &&
                   math.all(lhs.c1 == rhs.c1) &&
                   math.all(lhs.c2 == rhs.c2) &&
                   math.all(lhs.c3 == rhs.c3);
        }

        private static bool MatrixFinite(in float4x4 matrix)
        {
            return math.all(math.isfinite(matrix.c0)) &&
                   math.all(math.isfinite(matrix.c1)) &&
                   math.all(math.isfinite(matrix.c2)) &&
                   math.all(math.isfinite(matrix.c3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 SanitizeVector2(Vector2 value)
        {
            return math.all(math.isfinite(new float2(value.x, value.y)))
                ? new float2(value.x, value.y)
                : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool VectorFinite(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!VectorFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = _cachedRuntimeOriginAup;
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in positionAup);
        }

        private void BindTerminalRenderers()
        {
            if (_terminalTextureArray == null)
                return;

            Shader.SetGlobalTexture(TerminalTextureArrayId, _terminalTextureArray);
            BindBakedCrtProjectionGlobals(_bakedCrtProjectionReady);
            if (terminalArrayMaterial != null)
            {
                terminalArrayMaterial.SetTexture(TerminalTextureArrayId, _terminalTextureArray);
                terminalArrayMaterial.SetFloat(HectonDiegeticGlitchQualityWeightId, _globalQualityWeight);
                terminalArrayMaterial.SetFloat(HectonDecryptionNoiseDensityId, decryptionNoiseDensity);
                terminalArrayMaterial.SetFloat(HectonTerminalInstancedModeId, drawPanelsInstanced && _panelInstanceBuffer != null ? 1f : 0f);
                int decryptionPuzzleCount = _decryptionPuzzleBuffer != null
                    ? math.min(_decryptionPuzzleUploadCount, _terminalCount)
                    : 0;
                terminalArrayMaterial.SetFloat(GlobalDecryptionPuzzleCountId, decryptionPuzzleCount);
                if (_decryptionPuzzleBuffer != null && decryptionPuzzleCount > 0)
                    terminalArrayMaterial.SetBuffer(GlobalDecryptionPuzzlesId, _decryptionPuzzleBuffer);
                if (_panelInstanceBuffer != null)
                    terminalArrayMaterial.SetBuffer(TerminalPanelInstancesId, _panelInstanceBuffer);
                BindBakedCrtProjectionMaterial(terminalArrayMaterial, _bakedCrtProjectionReady);
                BindTerminalProjectionBuffers();
            }

            _bindingsDirty = false;
        }

        private void BindBakedCrtProjectionGlobals(bool ready)
        {
            float ready01 = ready ? 1f : 0f;
            Shader.SetGlobalFloat(TerminalScreenBakedProjectionReadyId, ready01);
            Shader.SetGlobalFloat(TerminalScreenBakedProjectionWeightId, ready ? math.saturate(bakedScreenProjectionWeight) : 0f);
            Shader.SetGlobalFloat(TerminalScreenBurnInWeightId, ready ? math.saturate(bakedScreenBurnInWeight) : 0f);
            Shader.SetGlobalFloat(TerminalScreenGlassWeightId, ready ? math.saturate(bakedScreenGlassWeight) : 0f);
            if (!ready)
                return;

            Shader.SetGlobalTexture(TerminalScreenAlbedoAtlasId, bakedScreenAlbedoAtlas);
            Shader.SetGlobalTexture(TerminalScreenProjectionLutId, bakedScreenProjectionLut);
            Shader.SetGlobalTexture(TerminalScreenPackedMraoId, bakedScreenPackedMrao);
        }

        private void BindBakedCrtProjectionMaterial(Material material, bool ready)
        {
            if (material == null)
                return;

            material.SetFloat(TerminalScreenBakedProjectionReadyId, ready ? 1f : 0f);
            material.SetFloat(TerminalScreenBakedProjectionWeightId, ready ? math.saturate(bakedScreenProjectionWeight) : 0f);
            material.SetFloat(TerminalScreenBurnInWeightId, ready ? math.saturate(bakedScreenBurnInWeight) : 0f);
            material.SetFloat(TerminalScreenGlassWeightId, ready ? math.saturate(bakedScreenGlassWeight) : 0f);
            if (!ready)
                return;

            material.SetTexture(TerminalScreenAlbedoAtlasId, bakedScreenAlbedoAtlas);
            material.SetTexture(TerminalScreenProjectionLutId, bakedScreenProjectionLut);
            material.SetTexture(TerminalScreenPackedMraoId, bakedScreenPackedMrao);
        }

        private bool ResolveBakedCrtProjectionReady()
        {
            if (!bakedCrtProjectionEnabled ||
                bakedScreenAlbedoAtlas == null ||
                bakedScreenProjectionLut == null ||
                bakedScreenPackedMrao == null)
                return false;

            int albedoWidth = bakedScreenAlbedoAtlas.width;
            int albedoHeight = bakedScreenAlbedoAtlas.height;
            int mraoWidth = bakedScreenPackedMrao.width;
            int mraoHeight = bakedScreenPackedMrao.height;
            int lutWidth = bakedScreenProjectionLut.width;
            int lutHeight = bakedScreenProjectionLut.height;
            return albedoWidth > 1 &&
                   albedoHeight > 1 &&
                   lutWidth > 1 &&
                   lutHeight > 1 &&
                   albedoWidth == albedoHeight &&
                   mraoWidth == albedoWidth &&
                   mraoHeight == albedoHeight &&
                   lutWidth == lutHeight;
        }

        private void RefreshBakedCrtBindingDirtyState()
        {
            if (!_bakedCrtBindingDirty)
                return;

            bool ready = ResolveBakedCrtProjectionReady();
            int hash = ResolveBakedCrtBindingHash(ready);
            if (hash == _bakedCrtBindingHash)
            {
                _bakedCrtProjectionReady = ready;
                _bakedCrtBindingDirty = false;
                return;
            }

            _bakedCrtBindingHash = hash;
            _bakedCrtProjectionReady = ready;
            _bakedCrtBindingDirty = false;
            _bindingsDirty = true;
        }

        private int ResolveBakedCrtBindingHash(bool ready)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (bakedCrtProjectionEnabled ? 1 : 0);
                hash = hash * 31 + (ready ? 1 : 0);
                hash = hash * 31 + FoldUnityObjectEntityId(bakedScreenAlbedoAtlas);
                hash = hash * 31 + FoldUnityObjectEntityId(bakedScreenProjectionLut);
                hash = hash * 31 + FoldUnityObjectEntityId(bakedScreenPackedMrao);
                hash = hash * 31 + math.asint(math.saturate(bakedScreenProjectionWeight));
                hash = hash * 31 + math.asint(math.saturate(bakedScreenBurnInWeight));
                hash = hash * 31 + math.asint(math.saturate(bakedScreenGlassWeight));
                return hash;
            }
        }

        private static int FoldUnityObjectEntityId(UnityEngine.Object value)
        {
            return value != null ? unchecked((int)UnityEngine.EntityId.ToULong(value.GetEntityId())) : 0;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame < _nextLateFrameRegisterRetryFrame)
                return;

            _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
            if (_registeredLateFrame)
            {
                _nextLateFrameRegisterRetryFrame = 0;
            }
            else
            {
                ScheduleLateFrameRegisterRetry(frame);
            }
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
            _registeredSlowTick = false;
        }

        private void ScheduleLateFrameRegisterRetry(int frame)
        {
            _nextLateFrameRegisterRetryFrame = frame + ResolveColdRegistryRetryStride();
        }

        private int ResolveColdRegistryRetryStride()
        {
            float quality = math.saturate(math.isfinite(_globalQualityWeight) ? _globalQualityWeight : 1f);
            return math.clamp((int)math.round(math.lerp(120f, 30f, Smooth01(quality))), 30, 120);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registeredLateFrame = false;
        }

        private void RecordTelemetry(int frame, int dirtyCount, int dispatchedCount, uint faultFlags)
        {
            int terminalCountSnapshot = math.min(_terminalCount, TerminalOsConstants.TerminalCapacity);
            uint layoutHashSnapshot = ComputeLayoutHash();

            TerminalTelemetryEntry entry = new TerminalTelemetryEntry
            {
                Frame = frame,
                TerminalCount = terminalCountSnapshot,
                DirtyCount = dirtyCount,
                DispatchedCount = dispatchedCount,
                FormatMainThreadMilliseconds = _lastFormatMainThreadMilliseconds,
                UploadMicroseconds = _lastUploadMicroseconds,
                DispatchMicroseconds = _lastDispatchMicroseconds,
                FaultFlags = faultFlags,
                LayoutHash = layoutHashSnapshot,
                HoveredTerminalHash = _lastHoveredTerminalHash,
                LastPower01 = _lastPower01,
                LastDamage01 = _lastDamage01,
                EvaluatedTerminals = _lastEvaluatedTerminalCount,
                FramesBetweenUpdates = _framesBetweenUpdates,
                IntersectionMicroseconds = _lastIntersectionMicroseconds,
                GlobalQualityWeight = _globalQualityWeight
            };
            TryWriteTerminalTelemetryEntry(in entry);
        }

        private void RecordDecryptionTelemetry(int simulationFrame, uint faultFlags)
        {
            if (!TryReadVaultBuffer(in _decryptionPuzzlesHandle, out NativeArray<DecryptionPuzzleDTO>.ReadOnly puzzles) ||
                !TryReadVaultBuffer(in _decryptionTerminalsHandle, out NativeArray<DecryptionTerminalDTO>.ReadOnly decryptionTerminals) ||
                puzzles.Length == 0)
            {
                return;
            }

            int puzzleIndex = FindTerminalIndex(_lastHoveredTerminalHash);
            if (puzzleIndex < 0 || puzzleIndex >= puzzles.Length)
                puzzleIndex = 0;

            DecryptionPuzzleDTO puzzle = puzzles[puzzleIndex];
            DecryptionTerminalDTO terminal = decryptionTerminals.IsCreated && puzzleIndex < decryptionTerminals.Length
                ? decryptionTerminals[puzzleIndex]
                : default;

            DecryptionTelemetryEntry entry = new DecryptionTelemetryEntry
            {
                Frame = (uint)simulationFrame,
                PuzzleID = puzzle.PuzzleID,
                PlayerFrequency = puzzle.PlayerFrequency,
                PlayerPhase = puzzle.PlayerPhase,
                TargetFrequency = puzzle.TargetFrequency,
                TargetPhase = puzzle.TargetPhase,
                AlignmentAccuracy01 = puzzle.AlignmentAccuracy01,
                BurstMicroseconds = _lastDecryptionBurstMicroseconds,
                Flags = puzzle.Flags,
                NodeHash = terminal.NodeHash,
                TerminalHash = terminal.TerminalHash,
                FaultFlags = faultFlags
            };
            if (TryWriteDecryptionTelemetryEntry(in entry))
                _lastDecryptionTelemetryFrame = simulationFrame;
        }

        private bool TryWriteTerminalTelemetryEntry(in TerminalTelemetryEntry entry)
        {
            IDataVault vault = _vault;
            NativeArray<TerminalTelemetryEntry> telemetryRing = default;
            bool locked = false;
            try
            {
                if (vault == null ||
                    vault.IsCompactionFenceActive ||
                    !IsTerminalOsVaultHandle(in _telemetryRingHandle) ||
                    !vault.TryAcquireWriteLock(in _telemetryRingHandle, SystemID.UI, out telemetryRing))
                {
                    return false;
                }

                locked = true;
                if (!telemetryRing.IsCreated || telemetryRing.Length == 0)
                    return false;

                int telemetryIndex = math.clamp(_telemetryCursor, 0, telemetryRing.Length - 1);
                telemetryRing[telemetryIndex] = entry;
                _telemetryCursor = (telemetryIndex + 1) % telemetryRing.Length;
                return true;
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in _telemetryRingHandle, SystemID.UI);
            }
        }

        private bool TryWriteDecryptionTelemetryEntry(in DecryptionTelemetryEntry entry)
        {
            IDataVault vault = _vault;
            NativeArray<DecryptionTelemetryEntry> telemetryRing = default;
            bool locked = false;
            try
            {
                if (vault == null ||
                    vault.IsCompactionFenceActive ||
                    !IsTerminalOsVaultHandle(in _decryptionTelemetryRingHandle) ||
                    !vault.TryAcquireWriteLock(in _decryptionTelemetryRingHandle, SystemID.UI, out telemetryRing))
                {
                    return false;
                }

                locked = true;
                if (!telemetryRing.IsCreated || telemetryRing.Length == 0)
                    return false;

                int telemetryIndex = math.clamp(_decryptionTelemetryCursor, 0, telemetryRing.Length - 1);
                telemetryRing[telemetryIndex] = entry;
                _decryptionTelemetryCursor = (telemetryIndex + 1) % telemetryRing.Length;
                return true;
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in _decryptionTelemetryRingHandle, SystemID.UI);
            }
        }

        private uint ComputeLayoutHash()
        {
            if (!TryReadVaultBuffer(in _screenCommandsHandle, out NativeArray<ScreenCommandDTO>.ReadOnly screenCommands))
                return 0u;

            uint hash = 2166136261u;
            int count = math.min(_terminalCount, screenCommands.Length);
            for (int i = 0; i < count; i++)
            {
                ScreenCommandDTO command = screenCommands[i];
                hash = (hash ^ math.asuint(command.Position.x)) * 16777619u;
                hash = (hash ^ math.asuint(command.Position.y)) * 16777619u;
                hash = (hash ^ math.asuint(command.Scale)) * 16777619u;
            }

            if (TryReadVaultBuffer(in _buttonAabbHandle, out NativeArray<ButtonAABBDTO>.ReadOnly buttons))
            {
                int buttonCount = math.min(_buttonCount, buttons.Length);
                for (int i = 0; i < buttonCount; i++)
                {
                    ButtonAABBDTO button = buttons[i];
                    hash = (hash ^ button.TerminalHash) * 16777619u;
                    hash = (hash ^ button.CommandHash) * 16777619u;
                    hash = (hash ^ math.asuint(button.RectUv.x)) * 16777619u;
                    hash = (hash ^ math.asuint(button.RectUv.y)) * 16777619u;
                    hash = (hash ^ math.asuint(button.RectUv.z)) * 16777619u;
                    hash = (hash ^ math.asuint(button.RectUv.w)) * 16777619u;
                }
            }

            return hash;
        }

        private void QueueTerminalBlackBoxDump(uint faultFlags)
        {
            if (faultFlags == 0u || _blackBoxDumped)
                return;

            _queuedTerminalBlackBoxFaultFlags |= faultFlags;
            _terminalBlackBoxDumpQueued = true;
        }

        private void QueueDecryptionBlackBoxDump(uint faultFlags)
        {
            if (faultFlags == 0u || _decryptionBlackBoxDumped)
                return;

            _queuedDecryptionBlackBoxFaultFlags |= faultFlags;
            _decryptionBlackBoxDumpQueued = true;
        }

        private void FlushQueuedBlackBoxDumps()
        {
            if (_terminalBlackBoxDumpQueued)
            {
                uint faultFlags = _queuedTerminalBlackBoxFaultFlags;
                _terminalBlackBoxDumpQueued = false;
                _queuedTerminalBlackBoxFaultFlags = 0u;
                TryDumpBlackBox(faultFlags);
            }

            if (!_decryptionBlackBoxDumpQueued)
                return;

            uint decryptionFaultFlags = _queuedDecryptionBlackBoxFaultFlags;
            _decryptionBlackBoxDumpQueued = false;
            _queuedDecryptionBlackBoxFaultFlags = 0u;
            TryDumpDecryptionBlackBox(decryptionFaultFlags);
        }

        private void TryDumpBlackBox(uint faultFlags)
        {
            if (_blackBoxDumped ||
                string.IsNullOrEmpty(_dumpFullPath) ||
                !TryReadTerminalTelemetryDumpShape(out int telemetryLength, out int telemetryRingLength, out int telemetryCursor))
                return;

            try
            {
                bool primaryWritten = WriteBlackBoxDump(_dumpFullPath, faultFlags, telemetryLength, telemetryRingLength, telemetryCursor);
                if (primaryWritten && !string.IsNullOrEmpty(_dumpMirrorFullPath))
                    WriteBlackBoxDump(_dumpMirrorFullPath, faultFlags, telemetryLength, telemetryRingLength, telemetryCursor);

                _blackBoxDumped = primaryWritten;
            }
            catch (IOException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (NotSupportedException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (ArgumentException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (ObjectDisposedException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch (InvalidOperationException exception)
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
        }

        private bool TryReadTerminalTelemetryDumpShape(out int telemetryLength, out int telemetryRingLength, out int telemetryCursor)
        {
            telemetryLength = 0;
            telemetryRingLength = 0;
            telemetryCursor = 0;
            if (!TryReadVaultBuffer(in _telemetryRingHandle, out NativeArray<TerminalTelemetryEntry>.ReadOnly telemetryRing) ||
                telemetryRing.Length == 0)
            {
                return false;
            }

            telemetryRingLength = telemetryRing.Length;
            telemetryLength = math.min(TerminalOsConstants.BlackBoxFrameCount, telemetryRing.Length);
            telemetryCursor = _telemetryCursor;
            if (telemetryCursor < 0)
                telemetryCursor = 0;
            if (telemetryCursor >= telemetryRing.Length)
                telemetryCursor %= telemetryRing.Length;

            return telemetryLength > 0;
        }

        private bool TryReadTerminalTelemetryDumpEntry(int index, out TerminalTelemetryEntry entry)
        {
            entry = default;
            if (!TryReadVaultBuffer(in _telemetryRingHandle, out NativeArray<TerminalTelemetryEntry>.ReadOnly telemetryRing) ||
                (uint)index >= (uint)telemetryRing.Length)
            {
                return false;
            }

            entry = telemetryRing[index];
            return _vault != null && !_vault.IsCompactionFenceActive;
        }

        private unsafe bool WriteBlackBoxDump(string path, uint faultFlags, int telemetryLength, int telemetryRingLength, int telemetryCursor)
        {
            if (string.IsNullOrEmpty(path) ||
                telemetryLength <= 0 ||
                telemetryRingLength <= 0 ||
                !TryReadVaultBuffer(in _telemetryRingHandle, out NativeArray<TerminalTelemetryEntry>.ReadOnly telemetryRing) ||
                !telemetryRing.IsCreated ||
                telemetryRing.Length <= 0)
            {
                return false;
            }

            int entrySize = UnsafeUtility.SizeOf<TerminalTelemetryEntry>();
            int ringLength = math.min(telemetryRingLength, telemetryRing.Length);
            int count = math.min(math.min(telemetryLength, ringLength), TerminalOsConstants.BlackBoxFrameCount);
            if (entrySize != 64 || count <= 0)
                return false;

            int byteCount = TerminalDumpHeaderBytes + (count * entrySize);
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(TerminalOsRuntime),
                    "terminalTelemetryBlackBoxPayload");
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(target, 0, TerminalDumpMagic);
                WriteUInt32LittleEndian(target, 4, TerminalDumpVersion);
                WriteUInt32LittleEndian(target, 8, faultFlags);
                WriteInt32LittleEndian(target, 12, count);
                WriteInt32LittleEndian(target, 16, ringLength);
                WriteInt32LittleEndian(target, 20, telemetryCursor);
                WriteInt32LittleEndian(target, 24, entrySize);
                WriteUInt32LittleEndian(target, 28, SourceHash);

                int start = telemetryCursor - count;
                while (start < 0)
                    start += ringLength;
                if (start >= ringLength)
                    start %= ringLength;

                int offset = TerminalDumpHeaderBytes;
                for (int i = 0; i < count; i++)
                {
                    int index = start + i;
                    if (index >= ringLength)
                        index -= ringLength;

                    WriteTerminalTelemetryEntry(target, offset, telemetryRing[index]);
                    offset += entrySize;
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(TerminalOsRuntime),
                    "terminalTelemetryBlackBoxPayload");
            }
        }

        private static void WriteUInt32LittleEndian(Span<byte> destination, int offset, uint value)
        {
            destination[offset] = unchecked((byte)value);
            destination[offset + 1] = unchecked((byte)(value >> 8));
            destination[offset + 2] = unchecked((byte)(value >> 16));
            destination[offset + 3] = unchecked((byte)(value >> 24));
        }

        private static unsafe void WriteTerminalTelemetryEntry(byte* destination, int offset, in TerminalTelemetryEntry entry)
        {
            WriteInt32LittleEndian(destination, offset, entry.Frame);
            WriteInt32LittleEndian(destination, offset + 4, entry.TerminalCount);
            WriteInt32LittleEndian(destination, offset + 8, entry.DirtyCount);
            WriteInt32LittleEndian(destination, offset + 12, entry.DispatchedCount);
            WriteFloatLittleEndian(destination, offset + 16, entry.FormatMainThreadMilliseconds);
            WriteFloatLittleEndian(destination, offset + 20, entry.UploadMicroseconds);
            WriteFloatLittleEndian(destination, offset + 24, entry.DispatchMicroseconds);
            WriteUInt32LittleEndian(destination, offset + 28, entry.FaultFlags);
            WriteUInt32LittleEndian(destination, offset + 32, entry.LayoutHash);
            WriteUInt32LittleEndian(destination, offset + 36, entry.HoveredTerminalHash);
            WriteFloatLittleEndian(destination, offset + 40, entry.LastPower01);
            WriteFloatLittleEndian(destination, offset + 44, entry.LastDamage01);
            WriteInt32LittleEndian(destination, offset + 48, entry.EvaluatedTerminals);
            WriteInt32LittleEndian(destination, offset + 52, entry.FramesBetweenUpdates);
            WriteFloatLittleEndian(destination, offset + 56, entry.IntersectionMicroseconds);
            WriteFloatLittleEndian(destination, offset + 60, entry.GlobalQualityWeight);
        }

        private static unsafe void WriteFloatLittleEndian(byte* destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)BitConverter.SingleToInt32Bits(value)));
        }

        private static unsafe void WriteInt32LittleEndian(byte* destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, int offset, uint value)
        {
            destination[offset] = unchecked((byte)value);
            destination[offset + 1] = unchecked((byte)(value >> 8));
            destination[offset + 2] = unchecked((byte)(value >> 16));
            destination[offset + 3] = unchecked((byte)(value >> 24));
        }

        private void TryDumpDecryptionBlackBox(uint faultFlags)
        {
            if (_decryptionBlackBoxDumped ||
                !TryOpenVaultBuffer(ref _decryptionTelemetryRingHandle, out NativeArray<DecryptionTelemetryEntry> telemetryRing) ||
                string.IsNullOrEmpty(_decryptionDumpFullPath))
            {
                return;
            }

            EnsureDecryptionDumpWriterCold();
            if (_decryptionDumpWriter != null &&
                _decryptionDumpWriter.TryEnqueue(faultFlags, _decryptionTelemetryCursor, telemetryRing))
            {
                _decryptionBlackBoxDumped = true;
                _decryptionDumpBackpressureReported = false;
                return;
            }

            _lastDecryptionFaultFlags |= FaultDecryptionDumpBackpressure;
            if (!_decryptionDumpBackpressureReported)
            {
                _decryptionDumpBackpressureReported = true;
                GlobalTelemetryBus.PublishPerformanceWarning(DecryptionDumpBackpressureHash, DecryptionDumpContextHash, 1f);
            }
        }

        private void CompleteJobsForTeardown()
        {
            if (_formatScheduled)
            {
                ForceCompleteJobForTeardown(ref _formatHandle);
                _formatScheduled = false;
            }

            if (_clickResolveScheduled)
            {
                ForceCompleteJobForTeardown(ref _clickResolveHandle);
                _clickResolveScheduled = false;
            }

            if (_terminalInteractionScheduled)
            {
                ForceCompleteJobForTeardown(ref _terminalInteractionHandle);
                _terminalInteractionScheduled = false;
            }

            if (_decryptionScheduled)
            {
                ForceCompleteJobForTeardown(ref _decryptionHandle);
                _decryptionScheduled = false;
            }
        }

        private static bool TryFinalizeCompletedJob(ref JobHandle handle)
        {
            if (!handle.IsCompleted)
                return false;

            return DispatcherJobFence.TryFinalizeCompleted(ref handle);
        }

        private static void ForceCompleteJobForTeardown(ref JobHandle handle)
        {
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
        }

        private void DisposeGraphicsResources()
        {
            if (terminalArrayMaterial != null)
            {
                terminalArrayMaterial.SetFloat(GlobalDecryptionPuzzleCountId, 0f);
                terminalArrayMaterial.SetFloat(TerminalScreenBakedProjectionReadyId, 0f);
            }
            Shader.SetGlobalFloat(TerminalScreenBakedProjectionReadyId, 0f);
            _bakedCrtProjectionReady = false;
            _bakedCrtBindingDirty = true;

            ReleaseBuffer(ref _stateBuffer0);
            ReleaseBuffer(ref _stateBuffer1);
            ReleaseBuffer(ref _screenCommandBuffer);
            ReleaseBuffer(ref _glyphUvBuffer);
            ReleaseBuffer(ref _dirtyIndexBufferA);
            ReleaseBuffer(ref _dirtyIndexBufferB);
            _activeDirtyIndexBuffer = null;
            _dirtyIndexUploadBufferIndex = 0;
            ReleaseBuffer(ref _panelInstanceBuffer);
            DisposeTerminalProjectionGraphicsResources();
            ReleaseBuffer(ref _decryptionPuzzleBuffer0);
            ReleaseBuffer(ref _decryptionPuzzleBuffer1);
            _decryptionPuzzleBuffer = null;
            _decryptionPuzzleWriteBufferIndex = 0;
            _decryptionPuzzleUploadCount = 0;
            ReleaseRenderTexture();
            _graphicsResourcesReady = false;
            ResetTerminalBlitComputeState();
        }

        private void ReleaseRenderTexture()
        {
            if (_terminalTextureArray == null)
                return;

            _terminalTextureArray.Release();
            Destroy(_terminalTextureArray);
            _terminalTextureArray = null;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void DisposeNativeResources()
        {
            ReleaseVaultHandles(_vault);
            ClearVaultHandles();
            _nativeResourcesReady = false;
            _vault = null;
            _terminalCount = 0;
            _buttonCount = 0;
            _nextNativeResourceRetryFrame = 0;
        }

        private void BindDataVaultForLifecycle(IDataVault nextVault, IDataVault previousVault)
        {
            if (ReferenceEquals(_vault, nextVault))
                return;

            IDataVault releaseVault = _vault ?? previousVault;
            ReleaseVaultHandles(releaseVault);
            ClearVaultHandles();
            _nativeResourcesReady = false;
            _vault = nextVault;
            _terminalCount = 0;
            _buttonCount = 0;
            _nextNativeResourceRetryFrame = 0;
        }

        private void DisposeDecryptionDumpWriter()
        {
            DecryptionBlackBoxDumpWriter writer = _decryptionDumpWriter;
            _decryptionDumpWriter = null;
            _decryptionDumpWriterBootAttempted = false;
            _decryptionDumpBackpressureReported = false;
            if (writer != null)
                writer.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref TerminalStateDTO OpenTerminalStateRefForOwnerUnchecked(int index)
        {
            void* basePtr = OpenTerminalStatePointerForOwner();
            if (basePtr == null)
                FatalMemoryException.ThrowStaleVaultHandle();

            return ref UnsafeUtility.AsRef<TerminalStateDTO>((byte*)basePtr + index * UnsafeUtility.SizeOf<TerminalStateDTO>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TerminalStateDTO* OpenTerminalStatePointerForOwner()
        {
            if (TryOpenVaultBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates))
                return (TerminalStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(terminalStates);

            return null;
        }

        private void ClearVaultHandles()
        {
            _terminalStatesHandle = default;
            _screenCommandsHandle = default;
            _glyphUvsHandle = default;
            _terminalPositionsHandle = default;
            _terminalForwardHandle = default;
            _dirtyIndicesHandle = default;
            _telemetryRingHandle = default;
            _mockPowerSignalHandle = default;
            _mockDamageSignalHandle = default;
            _mockPowerStatusSignalHandle = default;
            _buttonAabbHandle = default;
            _panelInstancesHandle = default;
            _clickScratchHandle = default;
            _terminalPlanesHandle = default;
            _gazeRayHandle = default;
            _terminalInteractionsHandle = default;
            ClearTerminalProjectionVaultHandles();
            _decryptionPuzzlesHandle = default;
            _decryptionTerminalsHandle = default;
            _decryptionKnobInputHandle = default;
            _decryptionTelemetryRingHandle = default;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _terminalStatesHandle, TerminalStatesBufferId);
            ReleaseVaultHandle(vault, ref _screenCommandsHandle, ScreenCommandsBufferId);
            ReleaseVaultHandle(vault, ref _glyphUvsHandle, GlyphUvsBufferId);
            ReleaseVaultHandle(vault, ref _terminalPositionsHandle, TerminalPositionsBufferId);
            ReleaseVaultHandle(vault, ref _terminalForwardHandle, TerminalForwardsBufferId);
            ReleaseVaultHandle(vault, ref _dirtyIndicesHandle, DirtyIndicesBufferId);
            ReleaseVaultHandle(vault, ref _telemetryRingHandle, TelemetryRingBufferId);
            ReleaseVaultHandle(vault, ref _mockPowerSignalHandle, MockPowerBufferId);
            ReleaseVaultHandle(vault, ref _mockDamageSignalHandle, MockDamageBufferId);
            ReleaseVaultHandle(vault, ref _mockPowerStatusSignalHandle, MockPowerStatusBufferId);
            ReleaseVaultHandle(vault, ref _buttonAabbHandle, ButtonAabbBufferId);
            ReleaseVaultHandle(vault, ref _panelInstancesHandle, PanelInstancesBufferId);
            ReleaseVaultHandle(vault, ref _clickScratchHandle, TerminalClickScratchBufferId);
            ReleaseVaultHandle(vault, ref _terminalPlanesHandle, TerminalPlanesBufferId);
            ReleaseVaultHandle(vault, ref _gazeRayHandle, GazeRayBufferId);
            ReleaseVaultHandle(vault, ref _terminalInteractionsHandle, TerminalInteractionsBufferId);
            ReleaseTerminalProjectionVaultHandles(vault);
            ReleaseVaultHandle(vault, ref _decryptionPuzzlesHandle, DecryptionPuzzlesBufferId);
            ReleaseVaultHandle(vault, ref _decryptionTerminalsHandle, DecryptionTerminalsBufferId);
            ReleaseVaultHandle(vault, ref _decryptionKnobInputHandle, DecryptionKnobInputBufferId);
            ReleaseVaultHandle(vault, ref _decryptionTelemetryRingHandle, DecryptionTelemetryRingBufferId);
        }

        private static void ReleaseVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : unmanaged
        {
            if (vault != null && IsExactVaultHandle(in handle, expectedBufferId))
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ElapsedMilliseconds(long startTicks)
        {
            return (float)((Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ElapsedMicroseconds(long startTicks)
        {
            return (float)((Stopwatch.GetTimestamp() - startTicks) * 1000000.0 / Stopwatch.Frequency);
        }

        private sealed class DecryptionBlackBoxDumpWriter : IDisposable
        {
            private const uint DumpMagic = 0x44484348u; // HCHD
            private const uint DumpVersion = 3u;
            private const int DumpHeaderBytes = 24;

            private readonly object _gate = new object();
            private readonly DecryptionTelemetryEntry[] _records = new DecryptionTelemetryEntry[TerminalOsConstants.BlackBoxFrameCount];
            private readonly DecryptionTelemetryEntry[] _writeRecords = new DecryptionTelemetryEntry[TerminalOsConstants.BlackBoxFrameCount];
            private readonly string _path;
            private bool _hasPending;
            private bool _running;
            private uint _pendingFaultFlags;
            private int _pendingCursor;
            private int _pendingCount;

            public DecryptionBlackBoxDumpWriter(string path)
            {
                _path = path;
            }

            public void Start()
            {
                _running = true;
            }

            public bool TryEnqueue(uint faultFlags, int cursor, NativeArray<DecryptionTelemetryEntry> telemetryRing)
            {
                if (!_running || !telemetryRing.IsCreated || telemetryRing.Length <= 0)
                    return false;

                bool lockTaken = false;
                try
                {
                    System.Threading.Monitor.Enter(_gate, ref lockTaken);
                    if (_hasPending)
                        return false;

                    int count = math.min(TerminalOsConstants.BlackBoxFrameCount, telemetryRing.Length);
                    int start = cursor;
                    if (start < 0)
                        start = 0;
                    if (start >= telemetryRing.Length)
                        start %= telemetryRing.Length;

                    for (int i = 0; i < count; i++)
                    {
                        int index = start + i;
                        if (index >= telemetryRing.Length)
                            index -= telemetryRing.Length;
                        _records[i] = telemetryRing[index];
                    }

                    for (int i = count; i < _records.Length; i++)
                        _records[i] = default;

                    _pendingFaultFlags = faultFlags;
                    _pendingCursor = cursor;
                    _pendingCount = count;
                    _hasPending = true;
                }
                finally
                {
                    if (lockTaken)
                        System.Threading.Monitor.Exit(_gate);
                }

                return DrainPending();
            }

            public void Dispose()
            {
                _running = false;
                DrainPending();
            }

            private bool DrainPending()
            {
                uint faultFlags;
                int cursor;
                int count;
                lock (_gate)
                {
                    if (!_hasPending)
                        return false;

                    faultFlags = _pendingFaultFlags;
                    cursor = _pendingCursor;
                    count = math.min(_pendingCount, _records.Length);
                    for (int i = 0; i < count; i++)
                    {
                        _writeRecords[i] = _records[i];
                    }

                    _hasPending = false;
                    _pendingFaultFlags = 0u;
                    _pendingCursor = 0;
                    _pendingCount = 0;
                }

                try
                {
                    return WritePendingUnsafe(faultFlags, cursor, count, _writeRecords);
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (NotSupportedException)
                {
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            private unsafe bool WritePendingUnsafe(
                uint faultFlags,
                int cursor,
                int count,
                DecryptionTelemetryEntry[] records)
            {
                count = math.min(count, records.Length);
                if (count <= 0 || string.IsNullOrEmpty(_path))
                    return false;

                int entrySize = TerminalOsConstants.DecryptionTelemetryStrideBytes;
                int byteCount = DumpHeaderBytes + (count * entrySize);
                NativeArray<byte> payload = default;
                try
                {
                    payload = NativeFaultDumpWriter.CreateTransientPayload(
                        byteCount,
                        nameof(TerminalOsRuntime),
                        "terminalDecryptionBlackBoxPayload");
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    WriteUInt32LittleEndian(target, 0, DumpMagic);
                    WriteUInt32LittleEndian(target, 4, DumpVersion);
                    WriteUInt32LittleEndian(target, 8, faultFlags);
                    WriteInt32LittleEndian(target, 12, cursor);
                    WriteInt32LittleEndian(target, 16, count);
                    WriteInt32LittleEndian(target, 20, entrySize);

                    int offset = DumpHeaderBytes;
                    for (int i = 0; i < count; i++)
                    {
                        WriteDecryptionTelemetryEntry(target, offset, records[i]);
                        offset += entrySize;
                    }

                    return NativeFaultDumpWriter.TryWriteAll(_path, payload, byteCount);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(TerminalOsRuntime),
                        "terminalDecryptionBlackBoxPayload");
                }
            }

            private static unsafe void WriteDecryptionTelemetryEntry(byte* destination, int offset, in DecryptionTelemetryEntry entry)
            {
                WriteUInt32LittleEndian(destination, offset, entry.Frame);
                WriteUInt32LittleEndian(destination, offset + 4, entry.PuzzleID);
                WriteFloatLittleEndian(destination, offset + 8, entry.PlayerFrequency);
                WriteFloatLittleEndian(destination, offset + 12, entry.PlayerPhase);
                WriteFloatLittleEndian(destination, offset + 16, entry.TargetFrequency);
                WriteFloatLittleEndian(destination, offset + 20, entry.TargetPhase);
                WriteFloatLittleEndian(destination, offset + 24, entry.AlignmentAccuracy01);
                WriteFloatLittleEndian(destination, offset + 28, entry.BurstMicroseconds);
                WriteUInt32LittleEndian(destination, offset + 32, entry.Flags);
                WriteUInt32LittleEndian(destination, offset + 36, entry.NodeHash);
                WriteUInt32LittleEndian(destination, offset + 40, entry.TerminalHash);
                WriteUInt32LittleEndian(destination, offset + 44, entry.FaultFlags);
                WriteUInt32LittleEndian(destination, offset + 48, 0u);
                WriteUInt32LittleEndian(destination, offset + 52, 0u);
                WriteUInt32LittleEndian(destination, offset + 56, 0u);
                WriteUInt32LittleEndian(destination, offset + 60, 0u);
            }

            private static unsafe void WriteFloatLittleEndian(byte* destination, int offset, float value)
            {
                WriteUInt32LittleEndian(destination, offset, unchecked((uint)BitConverter.SingleToInt32Bits(value)));
            }

            private static unsafe void WriteInt32LittleEndian(byte* destination, int offset, int value)
            {
                WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
            }

            private static unsafe void WriteUInt32LittleEndian(byte* destination, int offset, uint value)
            {
                destination[offset] = unchecked((byte)value);
                destination[offset + 1] = unchecked((byte)(value >> 8));
                destination[offset + 2] = unchecked((byte)(value >> 16));
                destination[offset + 3] = unchecked((byte)(value >> 24));
            }
        }
    }
}

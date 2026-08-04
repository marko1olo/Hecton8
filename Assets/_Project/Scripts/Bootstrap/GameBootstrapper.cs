using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Stopwatch = System.Diagnostics.Stopwatch;
using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Audio.Propagation;
using Hecton8.Biolum;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Bucketing;
using Hecton8.Core.Contracts;
using Hecton8.Core.Database;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Dev;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Input;
using Hecton8.Optimization;
using Hecton8.Modding;
using Hecton8.Power;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.Systems.AI;
using Hecton8.UI;
using Hecton8.Visor;
using Hecton8.World;
using TMPro;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CoreAudioEvent = Hecton8.Core.AudioEvent;
using GlobalPhysicsStateManager = Hecton8.Physics.GlobalPhysicsStateManager;
using PhysicsApplySystem = Hecton8.Physics.PhysicsApplySystem;
using PhysicsEventBus = Hecton8.Physics.PhysicsEventBus;

namespace Hecton8.Bootstrap
{
    public enum GameBootstrapperEventType : byte
    {
        GameReady = 0,
        BootstrapFailed = 1
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct GameBootstrapperEventPayload : ISignal
    {
        public const int ExpectedCapacity = 12;
        public const int MaxFrameSignals = 12;
        public const int LowTierFrameSignals = 12;
        public const uint LaneHash = 0x47425450u; // GBTP

        [FieldOffset(0)] public uint ErrorHash;
        [FieldOffset(4)] public ushort EventType;
        [FieldOffset(6)] public ushort Reserved;
        [FieldOffset(8)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BootstrapTelemetryEntry
    {
        [FieldOffset(0)] public long TimestampTicks;
        [FieldOffset(8)] public long DurationMicroseconds;
        [FieldOffset(16)] public ulong ContextHash;
        [FieldOffset(24)] public uint FrameIndex;
        [FieldOffset(28)] public uint EventHash;
        [FieldOffset(32)] public uint CollectionIndex;
        [FieldOffset(36)] public uint ShaderIndex;
        [FieldOffset(40)] public uint VariantCount;
        [FieldOffset(44)] public uint WarmedVariantCount;
        [FieldOffset(48)] public float QualityWeight01;
        [FieldOffset(52)] public ushort Phase;
        [FieldOffset(54)] public ushort Flags;
        [FieldOffset(56)] public ushort ErrorCode;
        [FieldOffset(58)] public ushort Reserved;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;
    }

    public interface IGameBootstrapperEventListener
    {
        void OnGameBootstrapperEvent(in GameBootstrapperEventPayload payload);
    }

    /// <summary>
    /// Deterministic bootstrap owner for the GlobalRegistry core and guarded scene routing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-29980)]
    public sealed class GameBootstrapper : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private static bool HectonForceSandboxScene = false;

        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string MainMenuSceneName = "01_MAIN_MENU";
        private const string DefaultGameplaySceneName = "02_HECTON_WORLD";
        private const string OrbitSceneName = "01_ORBIT";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string MainMenuScenePath = "Assets/_Project/Scenes/01_MAIN_MENU.unity";
        private const string DefaultGameplayScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string OrbitScenePath = "Assets/_Project/Scenes/01_ORBIT.unity";
        private const string FatalBootCrashFileName = "fatal_boot_crash.log";
        private const string BootStateFileName = "boot.bin";
        private const string PersistentRootName = "[PROJECT_PERSISTENT_ROOT]";
        private const string BootstrapAudioListenerRuntimeName = "[BootstrapAudioListener]";
        private const string BootstrapPresentationRootName = "[BOOT_PRESENTATION_FALLBACK]";
        private const string BootstrapPresentationCameraName = "[BOOT_PRESENTATION_CAMERA]";
        private const string PrefabRegistryRuntimeName = "[PrefabRegistry]";
        private const string PersistentWorldRegistryRuntimeName = "[PersistentWorldRegistry]";
        private const string RuntimePerformanceProfilerRuntimeName = "[RuntimePerformanceProfiler]";
        private const string CrashTelemetryRuntimeName = "[CrashTelemetryBuffer]";
        private const string RuntimeWatchdogRuntimeName = "[RuntimeWatchdog]";
        private const string GCMonitorRuntimeName = "[GCMonitor]";
        private const string PluginsAssemblyName = "Hecton8.Plugins";
        private const string CrestBridgeAssemblyName = "Hecton8.Crest.Bridge";
        private const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";
        private const string HectonHeadlessCommandLineArg = "-h8headless";
        private const string HeadlessCommandLineArg = "-headless";
        private const string AllowMissingDataMonolithEditorCommandLineArg = "-h8AllowMissingStaticDataMonolith";
        private const string AllowMissingDataMonolithEditorEnvironmentVariable = "H8_ALLOW_MISSING_STATIC_DATA_MONOLITH_EDITOR";
#if UNITY_EDITOR
        private const string AllowMissingDataMonolithEditorPrefsKey = "Hecton8.AllowMissingStaticDataMonolith.EditorOnly";
#endif
        private const string TierLowAddressableLabel = "Tier_Low";
        private const string TierHighAddressableLabel = "Tier_High";
        private const int OptionalServiceTimeoutMilliseconds = 5000;
        private const int ShaderWarmupBaseTimeoutMilliseconds = 5000;
        private const int ShaderWarmupMaxTimeoutMilliseconds = 60000;
        private const int ShaderWarmupPerShaderAttemptTimeoutMilliseconds = 20;
        private const int ShaderWarmupPerGraphicsStateCollectionTimeoutMilliseconds = 4000;
        private const int ShaderWarmupLowQualityTimeoutPaddingMilliseconds = 8000;
        private const int ShaderWarmupLowQualityFrameCadenceMilliseconds = 34;
        private const int ShaderWarmupHighQualityFrameCadenceMilliseconds = 17;
        private const int DataMonolithBootstrapMaxAttempts = 3;
        private const int SuspiciousGraphicsMemoryFallbackThresholdMb = 256;
        private const int UltraTierProcessorCount = 12;
        private const double BootstrapRunStartGraceSeconds = 2.0d;
        private const double ObjectPoolWarmupFrameBudgetMilliseconds = 8.0d;
        private const int BootStateRecordBytes = 32;
        private const int FatalBootCrashMessageByteCount = 66;
        private const uint BootStateMagic = 0x38484248u; // HBH8
        private const ushort BootStateVersion = 1;
        private const int PendingEventCapacity = 12;
        private const int SceneRootGraphLimit = 512;
        private const int WarmupBatchSize = 8;
        private const int BootstrapShaderWarmupTelemetryCapacity = 300;
        private const int BootstrapTelemetryEntrySizeBytes = 64;
        private const int BootstrapShaderWarmupDumpBytes = BootstrapShaderWarmupTelemetryCapacity * BootstrapTelemetryEntrySizeBytes;
        private const BufferID BootstrapShaderWarmupTelemetryRingBufferId = BufferID.GameBootstrapper_BootstrapShaderWarmupTelemetryRingBufferId;
        private const string BootstrapShaderWarmupDumpFileName = "Dump_1336_Bootstrapper.bin";
        private const string GraphicsStateCollectionExtension = ".graphicsstate";
        private const string AssetsPathPrefix = "Assets/";
        private const string ProjectSettingsPathPrefix = "ProjectSettings/";
        private const string StreamingAssetsProjectPathPrefix = "Assets/StreamingAssets/";
        private const string ShaderWarmupFailureReason = "BOOTSTRAP_SHADER_WARMUP_FAILED";
        private const string ShaderWarmupFailureOverlayMessage =
            "BIOS ERROR 0xSHADER\nEXPECTED: PRECOMPILED VARIANT WARMUP\nACTION: BOOT HALTED";
        private const float WorldReadyPollIntervalSec = 0.1f;
        private const int WorldReadyThreshold = 100;
        private const int WorldReadyStagnationPollLimit = 40;
        private const float GroundCheckPollIntervalSec = 0.2f;
        private const float GroundCheckRayOffset = 2f;
        private const float GroundCheckRayLength = 1000f;
        private const float GroundCheckLogIntervalSec = 5f;
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const int LowMemorySystemThresholdMb = 8192;
        private const int LowMemoryVramThresholdMb = 2048;
        private const int MinimalTierTargetFrameRate = 30;
        private const int DefaultTargetFrameRate = 60;
        private const int BackgroundDomainHandshakeIdle = 0;
        private const int BackgroundDomainHandshakeRunning = 1;
        private const int BackgroundDomainHandshakeComplete = 2;
        private const int BackgroundDomainHandshakeFailed = 3;
        private const int BackgroundDomainHandshakeFailureNone = 0;
        private const int BackgroundDomainHandshakeFailureInvalidPath = 1;
        private const int BackgroundDomainHandshakeFailureIo = 2;
        private const int BackgroundDomainHandshakeFailureUnauthorized = 3;
        private const int BackgroundDomainHandshakeFailureUnsupported = 4;
        private const int SurvivalAsyncUploadBufferMb = 64;
        private const int MidTierAsyncUploadBufferMb = 128;
        private const int HighTierAsyncUploadBufferMb = 256;
        private const int SurvivalAsyncUploadTimeSliceMs = 1;
        private const int MidTierAsyncUploadTimeSliceMs = 2;
        private const int HighTierAsyncUploadTimeSliceMs = 4;
        private const int SurfaceMediumQualityIndex = 0;
        private const int AbyssLowQualityIndex = 1;
        private const int OrbitHighQualityIndex = 2;
        private const int QuestVrQualityIndex = 3;
        private const int HandheldUmaQualityIndex = 4;
        private const int CompactPcQualityIndex = 5;
        private const int LeviathanUltraQualityIndex = 6;
        private const string SurfaceMediumQualityName = "Surface (Medium)";
        private const string AbyssLowQualityName = "Abyss (Low)";
        private const string OrbitHighQualityName = "Orbit (High)";
        private const string QuestVrQualityName = "Quest (VR)";
        private const string HandheldUmaQualityName = "Handheld (UMA)";
        private const string CompactPcQualityName = "Compact PC";
        private const string LeviathanUltraQualityName = "Leviathan (Ultra)";
        private const int HeartbeatFreezeSlowTickLimit = 3;
        private const int BootstrapHeartbeatRebindCadenceFrames = 8;
        private const double ServiceHeartbeatPollIntervalSeconds = 60.0d;
        private const double BootstrapSceneLoadWatchdogSeconds = 10.0d;
        private const double BootstrapCompletedHandoffWatchdogSeconds = 60.0d;
        private const double BootstrapJobWaitWatchdogSeconds = 10.0d;
        // Diagnostic log cadence only, never a control-flow bound. The gameplay handoff scene load is bounded by
        // the AsyncOperation itself; a wall clock may not abandon a live, activation-enabled, uncancellable load.
        private const double BootstrapGameplayHandoffStallLogIntervalSeconds = 10.0d;
        private const int BootstrapSceneRootScratchCapacity = 256;
        private const int BootstrapTransformScratchCapacity = 4096;
        private const double BootstrapAddressablePrewarmSoftTimeoutSeconds = 2.5d;
        private const double BootstrapRequiredAddressableGateTimeoutSeconds = 15.0d;
#if UNITY_INCLUDE_TESTS
        private static readonly bool _isUnityTestRunnerProcess = ResolveUnityTestRunnerProcess();
#endif
        // COLD ALLOC: List<GameObject>[256] - bootstrap scene-root traversal scratch without scene-wide array allocation - owner: GameBootstrapper
        private static readonly List<GameObject> _bootstrapSceneRootScratch = new List<GameObject>(BootstrapSceneRootScratchCapacity);
        // COLD ALLOC: List<Transform>[4096] - bootstrap transform traversal scratch without recursive iterator allocation - owner: GameBootstrapper
        private static readonly List<Transform> _bootstrapTransformScratch = new List<Transform>(BootstrapTransformScratchCapacity);
        // COLD ALLOC: List<ProfilerRecorderHandle>[256] - reused bootstrap memory metric scanner; no per-call list allocation - owner: GameBootstrapper
        private static readonly List<ProfilerRecorderHandle> _profilerRecorderHandleScratch = new List<ProfilerRecorderHandle>(256);
        private const string BiosRouteErrorMessage =
            "BIOS ERROR 0xBOOT\nEXPECTED: 00_BOOTSTRAP [0]\nACTION: FORCED RECOVERY";
        private const string FatalBootOverlayMessage =
            "BIOS ERROR 0xBOOT_FATAL\nACTION: SEE fatal_boot_crash.log";
        private const float BootstrapPresentationCameraDepth = 4096f;

        private enum ShaderWarmupTelemetryPhase : ushort
        {
            Start = 1,
            CollectionStart = 2,
            ShaderComplete = 3,
            CollectionComplete = 4,
            Complete = 5,
            Failure = 6,
            Timeout = 7,
            DumpQueued = 9,
            GraphicsStateCollectionStart = 10,
            GraphicsStateCollectionComplete = 11
        }

        [Flags]
        private enum ShaderWarmupTelemetryFlags : ushort
        {
            None = 0,
            Headless = 1 << 0,
            MissingManifest = 1 << 2,
            Timeout = 1 << 4,
            Failure = 1 << 5,
            DumpQueued = 1 << 6,
            MissingCollections = 1 << 7,
            GraphicsStateCollection = 1 << 8,
            GraphicsStateIncompatible = 1 << 9,
            Deferred = 1 << 10
        }

        private enum ShaderWarmupErrorCode : ushort
        {
            None = 0,
            MissingTelemetryRing = 1,
            MissingShaderManifest = 2,
            Timeout = 3,
            InvalidCollectionSet = 4,
            WarmupApiFailure = 5,
            MissingShaderCollections = 6,
            GraphicsStateWarmupFailure = 7,
            GraphicsStateCompatibilityFailure = 8,
            MissingGraphicsStateCollections = 9
        }

        private static readonly VertexAttributeDescriptor[] _shaderWarmupMeshVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
        };

        private static readonly VertexAttributeDescriptor[] _shaderWarmupPositionUvVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
        };

        private static readonly VertexAttributeDescriptor[] _shaderWarmupPositionNormalVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3)
        };

        private static readonly VertexAttributeDescriptor[] _shaderWarmupPositionNormalUvVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
        };

        private static readonly VertexAttributeDescriptor[] _shaderWarmupUiVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
        };

        private static readonly VertexAttributeDescriptor[] _shaderWarmupFloatColorVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2)
        };

        private static readonly VertexAttributeDescriptor[] _shaderWarmupVoxelVertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord4, VertexAttributeFormat.UNorm8, 4)
        };

        private static readonly ShaderWarmupSetup[] _shaderWarmupSetups =
        {
            new ShaderWarmupSetup { vdecl = _shaderWarmupMeshVertexLayout },
            new ShaderWarmupSetup { vdecl = _shaderWarmupPositionUvVertexLayout },
            new ShaderWarmupSetup { vdecl = _shaderWarmupPositionNormalVertexLayout },
            new ShaderWarmupSetup { vdecl = _shaderWarmupPositionNormalUvVertexLayout },
            new ShaderWarmupSetup { vdecl = _shaderWarmupUiVertexLayout },
            new ShaderWarmupSetup { vdecl = _shaderWarmupFloatColorVertexLayout },
            new ShaderWarmupSetup { vdecl = _shaderWarmupVoxelVertexLayout }
        };
        private struct ListenerSlot
        {
            public IGameBootstrapperEventListener Listener;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear()
            {
                Listener = null;
            }
        }

        private struct FailureReasonSlot
        {
            public uint ErrorHash;
            public string Reason;
            public byte IsValid;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear()
            {
                ErrorHash = 0u;
                Reason = null;
                IsValid = 0;
            }
        }

        // COLD ALLOC: ListenerSlot[12] - bootstrap listeners drained on dispatcher LateUpdate without interface array dispatch - owner: GameBootstrapper
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[PendingEventCapacity];
        // COLD ALLOC: ListenerSlot[12] - deferred listener additions during bootstrap event dispatch - owner: GameBootstrapper
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[PendingEventCapacity];
        // COLD ALLOC: ListenerSlot[12] - deferred listener removals during bootstrap event dispatch - owner: GameBootstrapper
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[PendingEventCapacity];
        // COLD ALLOC: FailureReasonSlot[8] - fixed hashed bootstrap failure reason sidecar; no managed map growth - owner: GameBootstrapper
        private static readonly FailureReasonSlot[] _failureReasonSlots = new FailureReasonSlot[8];
        private static GlobalDataVault _globalDataVault;
        private static H8MacroDatabaseService _macroDatabaseService;
        private static BurstTokenBucketJobAdmissionService _jobAdmissionService;
        private static JobAdmissionTelemetryBridge _jobAdmissionTelemetryBridge;
        private static ModuloSimulationBucketer _simulationBucketerService;
        private static int _listenerCount;
        private static int _deferredRegisterListenerCount;
        private static int _deferredUnregisterListenerCount;
        private static int _failureReasonSlotCount;
        private static int _droppedGameBootstrapperEventCount;
        private static int _droppedGameBootstrapperListenerMutationCount;
        private static bool _isDispatchingGameBootstrapperEvents;
        private static bool _h8MemoryFatalLogHooked;
        private static bool _h8MemoryFatalDumpWritten;
        /// <summary>
        /// True when the <c>DebrisManager</c> bootstrap node found <see cref="GlobalRegistry.Debris"/> empty and
        /// therefore passed under the recorded not-installed exemption instead of a real readiness result.
        /// Assigned (never OR-ed) once per boot by <see cref="ReportDebrisManagerBootstrapNodeState"/>, which is
        /// what writes the loud record, so readiness can never silently report ready.
        /// </summary>
        private static bool _debrisManagerBootstrapNodeNotInstalled;
        /// <summary>
        /// True when the <c>SpatialAudioManager</c> bootstrap node fell back to <see cref="NoOpAudioService"/> and
        /// therefore passed under the recorded stub exemption instead of a real readiness result.
        /// </summary>
        /// <remarks>
        /// This exists because <see cref="NoOpAudioService"/> now reports <c>IsInitialized == false</c> and
        /// <c>IsAudioRuntimeReady == false</c> - it holds no audio, so it must not claim readiness to the consumers
        /// that gate on those two properties. That honesty would otherwise be fatal to the whole boot rather than
        /// to audio alone: <see cref="IsBootstrapAudioServiceUsable"/> is what
        /// <see cref="IsBootstrapDependencyNodeReady(BootstrapDependencyNode, object)"/> and
        /// <see cref="IsBootstrapDependencyHeartbeatReady"/> consult for this node, and a failed Environment-phase
        /// node abandons the Player phase, the UI phase, the CoreReady marker, <c>GlobalRegistry.LockReady</c> and
        /// scene activation (the same reasoning written out at <see cref="ReportDebrisManagerBootstrapNodeState"/>).
        /// Audio is optional; silence must not cost the session.
        /// <para>
        /// Assigned (never OR-ed) by <see cref="TryRegisterNoOpAudioFallback"/>, which is what writes the loud
        /// record, so an unrecorded stub can never pass. Cleared the moment a real owner claims the slot, so the
        /// exemption cannot survive from an earlier boot into a boot where audio genuinely works.
        /// </para>
        /// </remarks>
        private static bool _audioBootstrapNodeStubbed;
        private static string _lastDataMonolithBootstrapStatus = "none";
#if UNITY_EDITOR
        private static string _pendingDirtySceneReloadPath;
        private static readonly List<GameObject> _dontDestroyRootScratch = new List<GameObject>(32); // COLD ALLOC: List<GameObject>[32] - editor-only DDOL residue scan scratch - owner: GameBootstrapper
        private static bool _editorEnteredPlayMode;
        private static bool _editorBootstrapDeferredUntilEnteredPlayMode;
        private static bool _editorBootstrapDelayCallRegistered;
#endif
        private static readonly string[] _TextureMemoryCandidates =
        {
            "Texture Memory",
            "Texture Used Memory"
        };
        private static readonly string[] _TotalReservedMemoryCandidates =
        {
            "Total Reserved Memory",
            "System Used Memory",
            "Total Used Memory"
        };

        private enum BootstrapPhase : byte
        {
            HardwareCheck = 0,
            MemoryPreWarm = 1,
            CoreServices = 2,
            Environment = 3,
            Player = 4,
            UI = 5,
            SceneActivate = 6,
            Complete = 7,
            Fatal = 8,
        }

        private enum BootStateMarker : byte
        {
            Unknown = 0,
            Started = 1,
            PhaseStarted = 2,
            ServiceStarted = 3,
            CoreReady = 4,
            WorldGen = 5,
            Complete = 6,
            Fatal = 7,
        }

        private enum BootstrapDependencyNode : byte
        {
            SystemDispatcher = 0,
            GameTickManager = 1,
            SaveManager = 2,
            ObjectPoolManager = 3,
            RenderDispatcher = 4,
            SceneRuntimeService = 5,
            EquipmentInteractionHandler = 6,
            HectonFloatingOrigin = 7,
            GlobalPhysicsStateManager = 8,
            PhysicsApplySystem = 9,
            DebrisManager = 10,
            EnvironmentRuntimeContextService = 11,
            OceanKinematicsRuntimeService = 12,
            EcosystemDirector = 13,
            FaunaSimulation = 14,
            SpatialAudioManager = 15,
            NativeInputManager = 16,
            InputDispatcher = 17,
            PlayerRuntimeContextService = 18,
            PlayerInventoryManager = 19,
            PlayerActionRuntime = 20,
            PlayerSensoryManager = 21,
            PowerGridManager = 22,
            ConstructionManager = 23,
            ConnectionSplineBatchRenderer = 24,
            BeaconNetworkSystem = 25,
            ModWorldPersistenceManager = 26,
            Count = 27,
        }

        private static readonly string[] _bootstrapDependencyNodeNames =
        {
            "SystemDispatcher",
            "GameTickManager",
            "SaveManager",
            "ObjectPoolManager",
            "RenderDispatcher",
            "SceneRuntimeService",
            "EquipmentInteractionHandler",
            "HectonFloatingOrigin",
            "GlobalPhysicsStateManager",
            "PhysicsApplySystem",
            "DebrisManager",
            "EnvironmentRuntimeContextService",
            "OceanKinematicsRuntimeService",
            "EcosystemDirector",
            "FaunaSimulation",
            "SpatialAudioManager",
            "NativeInputManager",
            "InputDispatcher",
            "PlayerRuntimeContextService",
            "PlayerInventoryManager",
            "PlayerActionRuntime",
            "PlayerSensoryManager",
            "PowerGridManager",
            "ConstructionManager",
            "ConnectionSplineBatchRenderer",
            "BeaconNetworkSystem",
            "ModWorldPersistenceManager",
        };

        private static readonly object _bootstrapDependencyScratchLock = new object();
        // COLD ALLOC: object[1] - bootstrap reflection argument scratch for isolated optional services - owner: GameBootstrapper
        private static readonly object[] _bootstrapReflectionSingleArgumentScratch = new object[1];
        // COLD ALLOC: GlobalRegistryServiceSlot[bootstrap-node-count] - registry dependency execution order scratch - owner: GameBootstrapper
        private static readonly GlobalRegistryServiceSlot[] _bootstrapRegistryExecutionOrderScratch =
            new GlobalRegistryServiceSlot[(int)BootstrapDependencyNode.Count];
        private static readonly uint _BootstrapTotalBootTimeHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Bootstrap.TotalBootTimeMs"));
        private static readonly uint _GameBootstrapperContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("GameBootstrapper"));
        private static readonly uint _ServiceHeartbeatFreezeHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SERVICE_HEARTBEAT_FREEZE"));
        private static readonly uint _ShaderWarmupStartHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Bootstrap.ShaderWarmup.Start"));
        private static readonly uint _ShaderWarmupCollectionHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Bootstrap.ShaderWarmup.Collection"));
        private static readonly uint _ShaderWarmupShaderHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Bootstrap.ShaderWarmup.Shader"));
        private static readonly uint _ShaderWarmupCompleteHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Bootstrap.ShaderWarmup.Complete"));
        private static readonly uint _ShaderWarmupFailureHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Bootstrap.ShaderWarmup.Failure"));
        private static readonly uint _ShaderWarmupTimeoutHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Bootstrap.ShaderWarmup.Timeout"));
        private static readonly WaitCallback _shaderWarmupDumpCallback = WriteBootstrapShaderWarmupDumpOnWorker;

        /// <summary>Telemetry subject for a bootstrap that is looping through entry recovery instead of completing.</summary>
        private static readonly uint _EntryRecoveryLoopHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Bootstrap.EntryRecovery.Loop"));
        private static bool _isBootstrapComplete;
        private static bool _sceneGuardRegistered;
        private static bool _entryRecoveryIssued;

        /// <summary>
        /// How many times entry recovery has reloaded the bootstrap scene THIS SESSION.
        ///
        /// <see cref="_entryRecoveryIssued"/> only guards re-entrancy inside ONE attempt: it is cleared
        /// by the full bootstrap state reset, which is exactly what a recovery reload triggers. So a boot
        /// that cannot finish loops - activation fails, _isBootstrapComplete stays false, the next scene
        /// load sees a non-bootstrap scene and reloads bootstrap Single, the reset clears the flag, and
        /// the whole thing repeats with nothing counting the repeats. That is consistent with the
        /// measured "one run in three comes up a shell world" this project has been living with.
        /// This counter is NOT cleared by the state reset. It survives until the play session ends.
        /// </summary>
        private static int _entryRecoveryAttempts;

        /// <summary>
        /// Recoveries allowed before the loop is broken. Two, not one: a single legitimate recovery is
        /// the feature working - a session that genuinely started outside 00_BOOTSTRAP gets put back on
        /// the route - and one retry absorbs a transient. A third means the boot is not recovering, it
        /// is looping, and reloading again cannot help.
        /// </summary>
        private const int MaxEntryRecoveryAttempts = 2;

        /// <summary>
        /// Session-scoped counters that the bootstrap state reset must NOT clear. Domain reload is
        /// disabled on this project (ProjectSettings/EditorSettings.asset m_EnterPlayModeOptions: 1), so
        /// without this hook the attempt count carries into the next play session and the very first
        /// recovery there would be refused.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEntryRecoveryAttempts()
        {
            _entryRecoveryAttempts = 0;
        }

        private static bool _bootstrapGameplayHandoffOwnsSceneLoad;
        private static string _bootstrapGameplayHandoffExpectedScenePath;
        // Cold single-allocation completion delegate. Owns the scene-runtime publication gate once the awaiting
        // bootstrap frame unwinds while Unity is still loading/activating the gameplay scene.
        private static readonly Action<AsyncOperation> _bootstrapHandoffSceneLoadCompletedCallback =
            ReleaseDeferredScenePublicationGate;
        private static int _bootstrapHandoffDeferredPublicationGateCount;
        private static BootstrapPhase _currentPhase;
        private static InputManager _bootstrapInputManager;
        private static bool _headlessBootMode;
        private static bool _preWarmAssetsReady;
        private static bool _bootstrapDurationTelemetryPublished;
        private static long _bootstrapStartTimestamp;
        private static uint _registryCoreReadyChecksum;
        private static bool _bootStateSafeModeRequested;

        [Header("Bootstrap Prewarm")]
        [Tooltip("Shader variant collections warmed during MemoryPreWarm before scene or player activation.")]
        [SerializeField] private ShaderVariantCollection[] shaderVariantCollections;
        [Tooltip("Explicit shader manifest used by ShaderWarmup.WarmupShaderFromCollection. Must cover every shader inside the configured collections.")]
        [SerializeField] private Shader[] shaderWarmupShaders;
        [Tooltip("Bootstrap-owned shader reference catalog. Replaces the legacy runtime Resources fallback for first-party shader lookup.")]
        [SerializeField] private RuntimeShaderReferenceCatalog runtimeShaderReferenceCatalog;
        [Tooltip("Optional Unity 6 PSO trace files. Use StreamingAssets-relative paths for players; Assets/ProjectSettings paths are editor-only.")]
        [SerializeField] private string[] shaderGraphicsStateCollectionPaths;
        [Tooltip("Optional human-authored GlobalDataVault sizing facade. If absent, legacy binary archaeology or mock config drives the vault.")]
        [SerializeField] private VaultConfigurationAsset vaultConfigurationAsset;
        [SerializeField] private uint expectedBiosRegistryFnv1a;
#if UNITY_ADDRESSABLES_EXIST
        [Tooltip("Addressable groups loaded sequentially before dependent services consume them.")]
        [SerializeField] private AssetLabelReference[] addressableDependencyGroups;
#endif

        [Header("Scene Activation")]
        [Tooltip("If true, always start a new game and ignore the handoff context.")]
        [SerializeField] private bool forceNewGame;
        [SerializeField] private bool prewarmProceduralScatterBeforePlayerActivation = true;
        [SerializeField, Range(1, 4)] private int scatterBootstrapPrimePasses = 2;
        [SerializeField] private List<WarmupEntry> warmupEntries = new List<WarmupEntry>();
        [SerializeField] private MonoBehaviour playerSpawner;
        [SerializeField] private Vector3 fallbackSpawnPosition = new Vector3(0f, 10f, 0f);
        [SerializeField] private GameObject playerObject;
        [SerializeField] private MonoBehaviour playerController;
        [SerializeField] private HectonUnderwaterVisuals underwaterVisuals;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private float worldGenWaitTime = 2f;
        [SerializeField] private float bootstrapTimeout = 30f;
        [SerializeField] private float groundReadyTimeout = 15f;
        [SerializeField] private LayerMask groundReadyLayerMask = HectonLayerMasks.SeamProbeLayerMask;
        [SerializeField] private bool verboseSceneActivationLogging = true;
#pragma warning disable CS0414
        [SerializeField] private string _debugSceneActivationStep = "Not started";
        [SerializeField] private bool _debugSceneActivationCompleted;
        [SerializeField] private float _debugStartupTextureMemoryMb;
        [SerializeField] private float _debugStartupReservedMemoryMb;
        [SerializeField] private string _debugStartupTextureMetric = "Unresolved";
        [SerializeField] private string _debugStartupReservedMetric = "Unresolved";
#pragma warning restore CS0414

#if UNITY_ADDRESSABLES_EXIST
        [Header("Bootstrap UI")]
        [Tooltip("Addressable HUD/PDA prefabs that must instantiate before UI bootstrap can complete.")]
        [SerializeField] private AssetReferenceGameObject[] uiAddressablePrefabs;
#endif

        private bool _bootstrapRunInProgress;
        private bool _sceneActivationRunInProgress;
        private bool _sceneActivationRequested;
        private bool _sceneActivationStarted;

        /// <summary>
        /// Deadline source for scene activation, held so <see cref="SetSceneActivationStep"/> can push it
        /// forward on every observed step.
        ///
        /// It used to be a single CancelAfter over the WHOLE phase, which made one wall clock bound
        /// singleton verification, pool warmup, world generation, the scene gate and the graph guard
        /// together. On a voxel plus MapMagic world that budget is not a hang detector, it is a coin
        /// flip - and AGENTS.md:195 bans exactly this shape: "Time-based coroutine timeouts for loading
        /// are banned", which is why the Kinematic Arrest Gate waits for WorldChunkPhysicsBakedSignal
        /// instead of a clock. Measured consequence: a headless run logged "bootstrap failed and scatter
        /// fallback was enabled. Reason: Bootstrap timed out during scene activation.", so
        /// _isBootstrapComplete was never set, AreAllSystemsReady() stayed false, MainMenuController
        /// disabled itself in Awake, and the game could not be entered at all.
        ///
        /// Now the budget is per STEP, not per phase: each of the 18 SetSceneActivationStep calls
        /// reschedules the deadline. A genuinely stuck boot still fails, and fails at a named step. Slow
        /// but progressing work is no longer killed for being slow.
        /// </summary>
        private CancellationTokenSource _sceneActivationDeadline;
        private ulong _sceneActivationSceneHandle = ulong.MaxValue;
        private bool _isLoadingSave;
        private bool _slowTickableRegistered;
        private bool _hotSwapRegistered;
        private bool _bootstrapStartWatchdogActive;
        private bool _runtimeOwnerAborted;
        private double _nextServiceHeartbeatPollTime;
        private WorldProceduralScatterDirector _worldProceduralScatterDirector;
        private int _backgroundDomainHandshakeState;
        private string _backgroundDomainHandshakePath;
        private int _backgroundDomainHandshakeFailureCode;
        private readonly List<GameObject> _shippingCleanupRootObjects = new List<GameObject>(64); // COLD ALLOC: List<GameObject>[64] - root cache for one-shot shipping scene cleanup - owner: GameBootstrapper
        private readonly List<Transform> _shippingCleanupTraversalStack = new List<Transform>(256); // COLD ALLOC: List<Transform>[256] - traversal stack for one-shot shipping scene cleanup - owner: GameBootstrapper
#if UNITY_ADDRESSABLES_EXIST
        private AsyncOperationHandle<GameObject>[] _uiPrefabInstanceHandles;
#endif
        private VaultGenerationHandle<BootstrapTelemetryEntry> _shaderWarmupTelemetryHandle;
        private bool _shaderWarmupTelemetryReady;
        private int _shaderWarmupTelemetryCursor;
        private int _shaderWarmupDumpQueued;
        // COLD ALLOC: byte[19200] - fatal shader warmup black-box snapshot - owner: GameBootstrapper
        private readonly byte[] _shaderWarmupDumpScratch = new byte[BootstrapShaderWarmupDumpBytes];
        private string _shaderWarmupDumpPath;
        private string _shaderWarmupDumpTempPath;
        private bool _shaderWarmupDumpPathCacheAttempted;
        private int _shaderWarmupDumpByteCount;
        // COLD ALLOC: BootstrapDependencyNode[bootstrap-node-count] - cached Kahn topological service execution order - owner: GameBootstrapper
        private readonly BootstrapDependencyNode[] _bootstrapExecutionOrder = new BootstrapDependencyNode[(int)BootstrapDependencyNode.Count];
        private readonly int[] _heartbeatTickSamples = new int[(int)BootstrapDependencyNode.Count];
        private readonly byte[] _heartbeatFrozenSamples = new byte[(int)BootstrapDependencyNode.Count];
        private int _bootstrapExecutionOrderCount;

        [Serializable]
        public struct WarmupEntry
        {
            public GameObject prefab;
            [Min(1)]
            public int count;
            public string label;
        }

        /// <summary>
        /// True once the bootstrap core finished its ordered initialization phases.
        /// </summary>
        public static bool IsBootstrapComplete => _isBootstrapComplete;

        public static bool IsGameReady => BootstrapState.IsGameReady;

        public static bool HasActiveInstance => BootstrapState.HasActiveInstance;

        private static GameBootstrapper s_activeRuntimeInstance;

        public static GameBootstrapper ActiveInstance => ResolveUsableRuntime();

        public static GameObject CurrentPlayerObject => BootstrapState.CurrentPlayerObject;

        public static Transform CurrentPlayerTransform => BootstrapState.CurrentPlayerTransform;

        public static int PendingEventCount => SignalBus<GameBootstrapperEventPayload>.SnapshotCount;

        /// <summary>
        /// True when boot is running in data-only server/testing mode.
        /// </summary>
        public static bool IsHeadlessBootMode => _headlessBootMode;

        /// <summary>
        /// True once bootstrap shader and residency prewarm gates have completed.
        /// </summary>
        public static bool ArePreWarmAssetsReady => _preWarmAssetsReady;

        internal static bool HasRuntimeInstance => ActiveInstance != null;

        /// <summary>
        /// True when all mandatory core services are registered and scene routing may proceed.
        /// </summary>
        public static bool AreAllSystemsReady()
        {
            return _isBootstrapComplete &&
                   GlobalRegistry.Dispatcher != null &&
                   GlobalRegistry.TickManager != null &&
                   GlobalRegistry.Save != null &&
                   GlobalRegistry.ObjectPool != null;
        }

        public static bool TryValidateSceneRootBudget(string sceneName, string context)
        {
            sceneName = NormalizeSceneLoadName(sceneName);
            if (sceneName.Length == 0)
                return true;

            Scene scene = SceneManager.GetSceneByName(sceneName);
            return TryValidateSceneRootBudget(scene, context);
        }

        public static bool TryValidateSceneRootBudget(Scene scene, string context)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return true;

            int rootCount = scene.rootCount;
            if (rootCount <= SceneRootGraphLimit)
                return true;

            Debug.LogError(
                "[GameBootstrapper] SCENE_GRAPH_CORRUPTION_GUARD abort. context=" +
                context +
                " scene=" +
                scene.name +
                " rootCount=" +
                rootCount +
                " limit=" +
                SceneRootGraphLimit);
            return false;
        }

        public static bool TryGetCurrentPlayerTransform(out Transform playerTransform)
        {
            return BootstrapState.TryGetCurrentPlayerTransform(out playerTransform);
        }

        public static bool RegisterBiolumDirector(HectonBiolumManager director)
        {
            if (!Application.isPlaying || director == null)
                return false;

            EnsureRuntimeInstance();
            PersistRuntimeService(director);

            HectonBiolumManager registered = GlobalRegistry.BiolumManager;
            if (registered != null && !ReferenceEquals(registered, director))
                return false;

            GlobalRegistry.RegisterBiolumManagerRuntime(director);
            return ReferenceEquals(GlobalRegistry.BiolumManager, director);
        }

        public static void UnregisterBiolumDirector(HectonBiolumManager director)
        {
            if (director == null)
                return;

            GlobalRegistry.UnregisterBiolumManagerRuntime(director);
        }

        public static void Register(IGameBootstrapperEventListener listener)
        {
            if (listener == null)
                return;

            EnsureGameBootstrapperEventLaneInitialized();
            if (_isDispatchingGameBootstrapperEvents)
            {
                QueueDeferredGameBootstrapperRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        public static void Unregister(IGameBootstrapperEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatchingGameBootstrapperEvents)
            {
                QueueDeferredGameBootstrapperUnregister(listener);
                return;
            }

            TryUnregisterImmediate(listener);
        }

        public static void FlushPendingEvents()
        {
            ReadOnlySpan<GameBootstrapperEventPayload> events = SignalBus<GameBootstrapperEventPayload>.GetFrameSnapshot();
            int eventCount = events.Length;
            if (eventCount <= 0 || _listenerCount <= 0)
                return;

            int scanBudget = eventCount;
            for (int eventIndex = 0; eventIndex < eventCount && scanBudget-- > 0; eventIndex++)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                GameBootstrapperEventPayload payload = events[eventIndex];
                int count = _listenerCount;
                _isDispatchingGameBootstrapperEvents = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IGameBootstrapperEventListener listener = _listeners[i].Listener;
                        if (listener != null && !IsDeferredGameBootstrapperUnregisterPending(listener))
                            listener.OnGameBootstrapperEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatchingGameBootstrapperEvents = false;
                    ApplyDeferredGameBootstrapperListenerMutations();
                }
            }
        }

        public static bool TryResolveBootstrapFailureReason(uint errorHash, out string reason)
        {
            for (int i = 0; i < _failureReasonSlotCount; i++)
            {
                if (_failureReasonSlots[i].IsValid != 0 && _failureReasonSlots[i].ErrorHash == errorHash)
                {
                    reason = _failureReasonSlots[i].Reason;
                    return true;
                }
            }

            reason = null;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            GameBootstrapper previousRuntime = s_activeRuntimeInstance;
            if (previousRuntime != null)
                previousRuntime.ResetTransientRuntimeStateForReloadDisabledPlayMode();

            ResetBootstrapEventState();
            s_activeRuntimeInstance = null;
            GlobalRegistry.ClearBootstrapperRuntime(null);
            _isBootstrapComplete = false;
            _entryRecoveryIssued = false;
            _bootstrapGameplayHandoffOwnsSceneLoad = false;
            _bootstrapGameplayHandoffExpectedScenePath = null;
            _bootstrapHandoffDeferredPublicationGateCount = 0;
            _currentPhase = BootstrapPhase.HardwareCheck;
            _bootstrapInputManager = null;
            _headlessBootMode = false;
            _preWarmAssetsReady = false;
            _bootstrapDurationTelemetryPublished = false;
            _bootstrapStartTimestamp = 0L;
            _registryCoreReadyChecksum = 0u;
            _lastDataMonolithBootstrapStatus = "none";
            _bootStateSafeModeRequested = false;
            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            BootstrapState.Reset();
            RemoveH8MemoryFatalDumpHook();
            ShutdownGlobalDataVaultForBootstrapTeardown();
            H8Memory.Shutdown();
            if (_sceneGuardRegistered)
            {
                SceneManager.sceneLoaded -= HandleSceneLoadedGuard;
                _sceneGuardRegistered = false;
            }

            BootstrapBiosErrorOverlay.Hide();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorBootstrapPlayModeGate()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorBootstrapPlayModeStateChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DisposeSessionNativeStateForShutdown;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorBootstrapPlayModeStateChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeSessionNativeStateForShutdown;
        }

        private static void HandleEditorBootstrapPlayModeStateChanged(UnityEditor.PlayModeStateChange stateChange)
        {
            if (stateChange == UnityEditor.PlayModeStateChange.ExitingEditMode)
            {
                _editorEnteredPlayMode = false;
                _editorBootstrapDeferredUntilEnteredPlayMode = false;
                _editorBootstrapDelayCallRegistered = false;
                UnityEditor.EditorApplication.delayCall -= RunDeferredEditorBootstrap;
                return;
            }

            if (stateChange == UnityEditor.PlayModeStateChange.EnteredPlayMode)
            {
                _editorEnteredPlayMode = true;
                EnsureRuntimeInstance()?.EnsureBootstrapProgressAfterLifecycleResume();
                if (_editorBootstrapDeferredUntilEnteredPlayMode)
                    QueueDeferredEditorBootstrap();
                return;
            }

            if (stateChange == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                stateChange == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                if (stateChange == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                    DisposeSessionNativeStateForShutdown();

                _editorEnteredPlayMode = false;
                _editorBootstrapDeferredUntilEnteredPlayMode = false;
                _editorBootstrapDelayCallRegistered = false;
                UnityEditor.EditorApplication.delayCall -= RunDeferredEditorBootstrap;
            }
        }

        private static bool ShouldDeferBootstrapUntilEditorEnteredPlayMode()
        {
            if (!Application.isPlaying)
                return false;

            if (UnityEditor.EditorApplication.isPlaying)
            {
                _editorEnteredPlayMode = true;
                return false;
            }

            return !_editorEnteredPlayMode;
        }

        private static void QueueDeferredEditorBootstrap()
        {
            if (_editorBootstrapDelayCallRegistered)
                return;

            _editorBootstrapDelayCallRegistered = true;
            UnityEditor.EditorApplication.delayCall += RunDeferredEditorBootstrap;
        }

        private static void RunDeferredEditorBootstrap()
        {
            _editorBootstrapDelayCallRegistered = false;
            if (!Application.isPlaying || !_editorBootstrapDeferredUntilEnteredPlayMode)
                return;

            _editorBootstrapDeferredUntilEnteredPlayMode = false;
            _editorEnteredPlayMode = true;
            EnsureRuntimeInstance()?.BeginBootstrap();
        }
#endif

        private static void ResetBootstrapEventState()
        {
            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterListenerCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterListenerCount);
            _listenerCount = 0;
            _deferredRegisterListenerCount = 0;
            _deferredUnregisterListenerCount = 0;
            ClearFailureReasonSlots();
            _droppedGameBootstrapperEventCount = 0;
            _droppedGameBootstrapperListenerMutationCount = 0;
            _isDispatchingGameBootstrapperEvents = false;
            ConfigureGameBootstrapperEventLane();
        }

        private static void ClearFailureReasonSlots()
        {
            for (int i = 0; i < _failureReasonSlotCount; i++)
                _failureReasonSlots[i].Clear();

            _failureReasonSlotCount = 0;
        }

        private static void TryRegisterFailureReason(uint errorHash, string reason)
        {
            if (errorHash == 0u || string.IsNullOrWhiteSpace(reason))
                return;

            for (int i = 0; i < _failureReasonSlotCount; i++)
            {
                if (_failureReasonSlots[i].IsValid != 0 && _failureReasonSlots[i].ErrorHash == errorHash)
                    return;
            }

            if (_failureReasonSlotCount >= _failureReasonSlots.Length)
                return;

            _failureReasonSlots[_failureReasonSlotCount++] = new FailureReasonSlot
            {
                ErrorHash = errorHash,
                Reason = reason,
                IsValid = 1
            };
        }

        private static void RegisterImmediate(IGameBootstrapperEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= PendingEventCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        private static void QueueDeferredGameBootstrapperRegister(IGameBootstrapperEventListener listener)
        {
            if (IsGameBootstrapperListenerRegistered(listener))
            {
                CancelDeferredGameBootstrapperUnregister(listener);
                return;
            }

            if (IsDeferredGameBootstrapperRegisterPending(listener))
                return;

            if (_deferredRegisterListenerCount >= _deferredRegisterListeners.Length)
            {
                IncrementGameBootstrapperListenerMutationDropCounter();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterListenerCount++].Listener = listener;
        }

        private static void QueueDeferredGameBootstrapperUnregister(IGameBootstrapperEventListener listener)
        {
            if (CancelDeferredGameBootstrapperRegister(listener))
                return;

            if (!IsGameBootstrapperListenerRegistered(listener))
                return;

            if (IsDeferredGameBootstrapperUnregisterPending(listener))
                return;

            if (_deferredUnregisterListenerCount >= _deferredUnregisterListeners.Length)
            {
                IncrementGameBootstrapperListenerMutationDropCounter();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterListenerCount++].Listener = listener;
        }

        private static bool IsGameBootstrapperListenerRegistered(IGameBootstrapperEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredGameBootstrapperRegisterPending(IGameBootstrapperEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterListenerCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredGameBootstrapperUnregisterPending(IGameBootstrapperEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterListenerCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool CancelDeferredGameBootstrapperRegister(IGameBootstrapperEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterListenerCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterListenerCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterListenerCount];
                _deferredRegisterListeners[_deferredRegisterListenerCount].Clear();
                return true;
            }

            return false;
        }

        private static bool CancelDeferredGameBootstrapperUnregister(IGameBootstrapperEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterListenerCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterListenerCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterListenerCount];
                _deferredUnregisterListeners[_deferredUnregisterListenerCount].Clear();
                return true;
            }

            return false;
        }

        private static void ApplyDeferredGameBootstrapperListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterListenerCount; i++)
            {
                IGameBootstrapperEventListener listener = _deferredUnregisterListeners[i].Listener;
                if (listener != null)
                    TryUnregisterImmediate(listener);

                _deferredUnregisterListeners[i].Clear();
            }

            _deferredUnregisterListenerCount = 0;

            for (int i = 0; i < _deferredRegisterListenerCount; i++)
            {
                IGameBootstrapperEventListener listener = _deferredRegisterListeners[i].Listener;
                if (listener != null)
                    RegisterImmediate(listener);

                _deferredRegisterListeners[i].Clear();
            }

            _deferredRegisterListenerCount = 0;
        }

        private static void IncrementGameBootstrapperListenerMutationDropCounter()
        {
            int current = Volatile.Read(ref _droppedGameBootstrapperListenerMutationCount);
            if (current < int.MaxValue)
                Interlocked.Increment(ref _droppedGameBootstrapperListenerMutationCount);
        }

        private static bool TryUnregisterImmediate(IGameBootstrapperEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                _listenerCount--;
                _listeners[i] = _listeners[_listenerCount];
                _listeners[_listenerCount].Clear();
                return true;
            }

            return false;
        }

        private static void EnsureGameBootstrapperEventLaneInitialized()
        {
            ConfigureGameBootstrapperEventLane();
            SignalBus<GameBootstrapperEventPayload>.EnsureInitialized();
        }

        private static void ConfigureGameBootstrapperEventLane()
        {
            SignalBus<GameBootstrapperEventPayload>.Configure(
                GameBootstrapperEventPayload.ExpectedCapacity,
                GameBootstrapperEventPayload.MaxFrameSignals,
                GameBootstrapperEventPayload.LowTierFrameSignals,
                GameBootstrapperEventPayload.LaneHash);
        }

        private static void RaiseGameReadyEvent()
        {
            EnsureGameBootstrapperEventLaneInitialized();

            GameBootstrapperEventPayload payload = new GameBootstrapperEventPayload
            {
                ErrorHash = 0u,
                EventType = (ushort)GameBootstrapperEventType.GameReady,
                Reserved = 0
            };

            EnqueueBootstrapEvent(in payload);
        }

        private static void RaiseBootstrapFailedEvent(string error)
        {
            uint errorHash = string.IsNullOrWhiteSpace(error)
                ? 0u
                : unchecked((uint)Hecton.Localization.LocHash.Compute(error));
            EnsureGameBootstrapperEventLaneInitialized();

            TryRegisterFailureReason(errorHash, error);

            GameBootstrapperEventPayload payload = new GameBootstrapperEventPayload
            {
                ErrorHash = errorHash,
                EventType = (ushort)GameBootstrapperEventType.BootstrapFailed,
                Reserved = 0
            };

            EnqueueBootstrapEvent(in payload);
        }

        private static void EnqueueBootstrapEvent(in GameBootstrapperEventPayload payload)
        {
            SignalBus<GameBootstrapperEventPayload>.TryPushTracked(
                in payload,
                ref _droppedGameBootstrapperEventCount);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void GuardInitialSceneEntry()
        {
#if UNITY_INCLUDE_TESTS
            if (_isUnityTestRunnerProcess)
                return;
#endif
            if (!Application.isPlaying || _isBootstrapComplete)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (TryRecoverEntryVector(activeScene, true) && IsBootstrapScene(activeScene))
                EnsureRuntimeInstance()?.BeginBootstrap();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void GuardEntryVectorBeforeSceneLoad()
        {
            return;
        }

        /// <summary>
        /// Ensures a runtime bootstrap owner exists on the current bootstrap shell object.
        /// </summary>
        /// <param name="owner">Bootstrap shell owner.</param>
        /// <returns>Live bootstrap component.</returns>
        public static GameBootstrapper EnsureRuntimeInstance()
        {
            GameBootstrapper bootstrapper = ActiveInstance;
            if (bootstrapper != null)
            {
                if (!bootstrapper._bootstrapRunInProgress &&
                    !_isBootstrapComplete &&
                    TryResolveBootstrapControllerOwner(out GameObject controllerOwner) &&
                    controllerOwner.TryGetComponent(out BootstrapController bootstrapController))
                {
                    bootstrapController.ApplySerializedShaderVariantCollections(bootstrapper);
                }

                return bootstrapper;
            }

            if (TryResolveBootstrapControllerOwner(out GameObject bootstrapOwner))
                return EnsureRuntimeInstance(bootstrapOwner);

            Scene activeScene = SceneManager.GetActiveScene();
            if (TryResolveSceneComponent(activeScene, includeInactive: false, out GameBootstrapper existingBootstrapper))
                return EnsureRuntimeInstance(existingBootstrapper.gameObject);

            GameObject runtimeRoot = new GameObject(PersistentRootName); // COLD ALLOC: GameObject[1] - bootstrap authority root when scene authoring omitted it - owner: GameBootstrapper
            if (activeScene.IsValid())
                SceneManager.MoveGameObjectToScene(runtimeRoot, activeScene);

            return runtimeRoot.AddComponent<GameBootstrapper>(); // COLD ALLOC: GameBootstrapper[1] - unified bootstrap authority - owner: GameBootstrapper
        }

        /// <summary>
        /// Ensures a runtime bootstrap owner exists on the current bootstrap shell object.
        /// </summary>
        /// <param name="owner">Bootstrap shell owner.</param>
        /// <returns>Live bootstrap component.</returns>
        public static GameBootstrapper EnsureRuntimeInstance(GameObject owner)
        {
            GameBootstrapper runtimeBootstrapper = ActiveInstance;
            if (runtimeBootstrapper != null)
            {
                if (!runtimeBootstrapper._bootstrapRunInProgress &&
                    !_isBootstrapComplete &&
                    owner != null &&
                    owner.TryGetComponent(out BootstrapController activeBootstrapController))
                {
                    activeBootstrapController.ApplySerializedShaderVariantCollections(runtimeBootstrapper);
                }

                return runtimeBootstrapper;
            }

            if (owner == null)
                return EnsureRuntimeInstance();

            if (!owner.TryGetComponent(out GameBootstrapper bootstrapper))
                bootstrapper = owner.AddComponent<GameBootstrapper>(); // COLD ALLOC: GameBootstrapper[1] - deterministic bootstrap owner on 00_BOOTSTRAP shell - owner: BootstrapController

            ClaimRuntimeBootstrapInstance(bootstrapper);

            if (owner.TryGetComponent(out BootstrapController bootstrapController))
                bootstrapController.ApplySerializedShaderVariantCollections(bootstrapper);

            return bootstrapper;
        }

        private static bool TryResolveBootstrapControllerOwner(out GameObject owner)
        {
            owner = null;
            Scene activeScene = SceneManager.GetActiveScene();
            if (!Application.isPlaying || !IsBootstrapScene(activeScene))
                return false;

            if (!TryResolveSceneComponent(activeScene, includeInactive: false, out BootstrapController bootstrapController) ||
                bootstrapController == null ||
                bootstrapController.gameObject.scene != activeScene)
                return false;

            owner = bootstrapController.gameObject;
            return owner != null;
        }

        private static bool TryResolveSceneComponent<T>(
            Scene scene,
            bool includeInactive,
            out T component)
            where T : Component
        {
            component = null;
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            scene.GetRootGameObjects(_bootstrapSceneRootScratch);

            for (int i = 0; i < _bootstrapSceneRootScratch.Count; i++)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null)
                    continue;

                if (!includeInactive && !root.activeInHierarchy)
                    continue;

                _bootstrapTransformScratch.Add(root.transform);
            }

            while (_bootstrapTransformScratch.Count > 0)
            {
                int lastIndex = _bootstrapTransformScratch.Count - 1;
                Transform current = _bootstrapTransformScratch[lastIndex];
                _bootstrapTransformScratch.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                GameObject currentObject = current.gameObject;
                if ((includeInactive || currentObject.activeInHierarchy) &&
                    currentObject.TryGetComponent(out component) &&
                    component != null)
                {
                    _bootstrapSceneRootScratch.Clear();
                    _bootstrapTransformScratch.Clear();
                    return true;
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    _bootstrapTransformScratch.Add(current.GetChild(i));
            }

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            component = null;
            return false;
        }

        private static bool TryResolveSceneTaggedObject(
            Scene scene,
            string tag,
            out GameObject taggedObject)
        {
            taggedObject = null;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(tag))
                return false;

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            scene.GetRootGameObjects(_bootstrapSceneRootScratch);

            for (int i = 0; i < _bootstrapSceneRootScratch.Count; i++)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null || !root.activeInHierarchy)
                    continue;

                _bootstrapTransformScratch.Add(root.transform);
            }

            while (_bootstrapTransformScratch.Count > 0)
            {
                int lastIndex = _bootstrapTransformScratch.Count - 1;
                Transform current = _bootstrapTransformScratch[lastIndex];
                _bootstrapTransformScratch.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                GameObject currentObject = current.gameObject;
                if (currentObject.activeInHierarchy && currentObject.CompareTag(tag))
                {
                    taggedObject = currentObject;
                    _bootstrapSceneRootScratch.Clear();
                    _bootstrapTransformScratch.Clear();
                    return true;
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    _bootstrapTransformScratch.Add(current.GetChild(i));
            }

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            return false;
        }

        /// <summary>
        /// Executes the ordered bootstrap phases once.
        /// </summary>
        public bool InitializeBootstrap()
        {
            BeginBootstrap();
            return _isBootstrapComplete || _bootstrapRunInProgress;
        }

        internal void SetBootstrapShaderVariantCollections(ShaderVariantCollection[] collections)
        {
            if (collections == null || collections.Length == 0 || _isBootstrapComplete)
                return;

            shaderVariantCollections = collections;
        }

        internal void SetBootstrapShaderWarmupShaders(Shader[] shaders)
        {
            if (shaders == null || shaders.Length == 0 || _isBootstrapComplete)
                return;

            shaderWarmupShaders = shaders;
        }

        internal void SetBootstrapRuntimeShaderReferenceCatalog(RuntimeShaderReferenceCatalog catalog)
        {
            if (catalog == null || _isBootstrapComplete)
                return;

            runtimeShaderReferenceCatalog = catalog;
        }

        internal void SetBootstrapShaderGraphicsStateCollectionPaths(string[] paths)
        {
            if (paths == null || paths.Length == 0 || _isBootstrapComplete)
                return;

            shaderGraphicsStateCollectionPaths = paths;
        }

        private void Awake()
        {
            GameBootstrapper runtimeBootstrapper = ResolveUsableRuntime();
            if (!ReferenceEquals(runtimeBootstrapper, null) && !ReferenceEquals(runtimeBootstrapper, this))
            {
                AbortDuplicateRuntimeOwner(destroyComponent: true);
                return;
            }

            if (!ClaimRuntimeBootstrapInstance(this))
                return;

            RuntimeShaderReferenceCatalog.Register(runtimeShaderReferenceCatalog);
            CacheBootstrapShaderWarmupDumpPathCold();

            if (Application.isPlaying)
            {
                EnsureBootstrapPresentationFallbackCold();
                gameObject.name = PersistentRootName;
                if (transform.parent != null)
                    transform.SetParent(null, true);

                MarkProjectPersistentRoot();
                EnforceProjectPersistentRoot();
            }
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted)
                return;

            _nextServiceHeartbeatPollTime = 0d;
            TryRegisterHotSwapListener();
            TryRegisterBootstrapSlowTickable();
            EnsureBootstrapProgressAfterLifecycleResume();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            _bootstrapStartWatchdogActive = false;
            if (!_slowTickableRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _slowTickableRegistered = false;
        }

        private void Start()
        {
            if (_runtimeOwnerAborted)
                return;

            EnsureBootstrapProgressAfterLifecycleResume();
        }

        private void EnsureBootstrapProgressAfterLifecycleResume()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!Application.isPlaying)
                return;

            RecoverReloadDisabledStaleBootstrapRun();
            if (_bootstrapRunInProgress || _isBootstrapComplete)
                return;

            Scene activeScene = gameObject.scene;
            if (!activeScene.IsValid() || !IsBootstrapScene(activeScene))
                activeScene = SceneManager.GetActiveScene();

            if (IsBootstrapScene(activeScene))
                BeginBootstrap();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
            {
                ClearRuntimeMirrorIfOwnedBy(this);
                return;
            }

            TryUnregisterHotSwapListener();
#if UNITY_ADDRESSABLES_EXIST
            ReleaseAddressableUIPrefabs();
#endif
            ShutdownServicesInReverseBootstrapOrder();
            BootstrapState.ClearCurrentPlayerObject(playerObject);
            BootstrapState.PublishBootstrapPresence(false);
            if (Application.isPlaying)
                BootstrapState.PublishGameReady(false);
            DisposeSessionNativeStateForShutdown();
            RuntimeShaderReferenceCatalog.Unregister(runtimeShaderReferenceCatalog);
            GlobalRegistry.ClearBootstrapperRuntime(this);
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
        }

        public void SlowTick()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!_isBootstrapComplete || _bootstrapExecutionOrderCount <= 0)
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextServiceHeartbeatPollTime)
                return;

            _nextServiceHeartbeatPollTime = now + ServiceHeartbeatPollIntervalSeconds;
            for (int index = 0; index < _bootstrapExecutionOrderCount; index++)
            {
                BootstrapDependencyNode node = _bootstrapExecutionOrder[index];
                object service = ResolveBootstrapDependencyService(node);
                IServiceHeartbeat heartbeat = service as IServiceHeartbeat;
                ISystem system = service as ISystem;
                if (heartbeat == null && system == null)
                    continue;

                if (heartbeat != null &&
                    (!heartbeat.IsServiceReady ||
                     heartbeat.HeartbeatState == ServiceHeartbeatState.Failed ||
                     heartbeat.HeartbeatState == ServiceHeartbeatState.Shutdown))
                {
                    continue;
                }

                int tickCount = system != null ? system.TickCount : heartbeat.TickCount;
                if (tickCount != _heartbeatTickSamples[index])
                {
                    _heartbeatTickSamples[index] = tickCount;
                    _heartbeatFrozenSamples[index] = 0;
                    continue;
                }

                if (_heartbeatFrozenSamples[index] < byte.MaxValue)
                    _heartbeatFrozenSamples[index]++;

                if (_heartbeatFrozenSamples[index] == HeartbeatFreezeSlowTickLimit)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[GameBootstrapper] SERVICE_HEARTBEAT_FREEZE");
#endif
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _ServiceHeartbeatFreezeHash,
                        _GameBootstrapperContextHash,
                        tickCount);
                    CrashTelemetryBuffer.ReportRuntimeWatchdogStall((uint)index, unchecked((uint)tickCount));
                }
            }
        }

        private void TryRegisterBootstrapSlowTickable()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!Application.isPlaying || _slowTickableRegistered || GlobalRegistry.Dispatcher == null)
                return;

            _slowTickableRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindBootstrapSchedulerVaults(currentService as IDataVault);
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher ||
                currentService == null ||
                !isActiveAndEnabled)
            {
                return;
            }

            if (_slowTickableRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _slowTickableRegistered = false;
            }

            if (currentService == null || !isActiveAndEnabled)
                return;

            TryRegisterBootstrapSlowTickable();
        }

        private static void RebindBootstrapSchedulerVaults(IDataVault currentVault)
        {
            ISimulationBucketer bucketer = GlobalRegistry.SimulationBucketer;
            if (bucketer is ModuloSimulationBucketer moduloBucketer)
            {
                moduloBucketer.Initialize(SimulationBucketConstants.DefaultEntityCapacity, currentVault);
            }
            else if (bucketer == null && currentVault != null)
            {
                EnsureSimulationBucketerRegistered();
            }
            else if (bucketer != null && !bucketer.IsInitialized && currentVault != null)
            {
                bucketer.Initialize(SimulationBucketConstants.DefaultEntityCapacity);
            }

            IJobAdmissionService admission = GlobalRegistry.JobAdmission;
            if (_jobAdmissionTelemetryBridge == null && (admission != null || currentVault != null))
                _jobAdmissionTelemetryBridge = new JobAdmissionTelemetryBridge(); // COLD ALLOC: JobAdmissionTelemetryBridge[1] - scheduler telemetry bridge - owner: GameBootstrapper

            if (admission is BurstTokenBucketJobAdmissionService burstAdmissionService)
            {
                burstAdmissionService.Initialize(_jobAdmissionTelemetryBridge, currentVault);
                JobAdmissionSchedulerBridge.SetService(burstAdmissionService);
            }
            else if (admission == null && currentVault != null)
            {
                EnsureJobAdmissionServiceRegistered();
            }
            else if (admission != null && !admission.IsInitialized && currentVault != null)
            {
                admission.Initialize(_jobAdmissionTelemetryBridge);
                JobAdmissionSchedulerBridge.SetService(admission);
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private static void DisposeSessionNativeStateForShutdown()
        {
            ShutdownSystemDispatcherForBootstrapTeardown();
            ShutdownSimulationBucketerServiceForBootstrapTeardown();
            ShutdownJobAdmissionServiceForBootstrapTeardown();
            Hecton8.Modding.ModLoader.ResetStaticState();
            Hecton8.Modding.ModRegistryEvents.ResetStaticState();
            BootstrapEvents.ResetStaticState();
            ResetBootstrapEventState();
            SaveEvents.ResetStaticState();
            Hecton.Localization.LocalizationEvents.ResetStaticState();
            ObjectPoolDiagnostics.ResetStaticState();
            UIStateStore.Shutdown();
            HighPressureEvents.Shutdown();
            FatalPressureImplosionEvents.Shutdown();
            AcousticZoneEvents.ResetStaticState();
            BaseAirlockEvents.ResetStaticState();
            Hecton8.Interaction.InteractionEvents.ResetStaticState();
            Hecton8.Crafting.CraftingEvents.ResetStaticState();
            Hecton8.Power.PowerGridTelemetryEvents.ResetStaticState();
            PhysicsEventBus.Shutdown();
            FrameTimeWatchdog.Shutdown();
            MathGuard.Dispose();
            SignalCorridorRuntime.Dispose();
            LogisticsPipeTransportScheduler.Shutdown();
            WorldSpatialHashGrid.ClearRuntimeState();
            global::Hecton8.Data.H8StaticDataArena.Shutdown();
            PreInitAssetIdMap.Shutdown();
            NativeArenaAllocator.Shutdown();
            ShutdownGlobalDataVaultForBootstrapTeardown();
            H8Memory.Shutdown();
            GlobalRegistry.DisposeServiceReboundQueuesForShutdown();
        }

        private static void ShutdownSystemDispatcherForBootstrapTeardown()
        {
            SystemDispatcher dispatcher = GlobalRegistry.Dispatcher;
            if (dispatcher == null)
                dispatcher = SystemDispatcher.ActiveRuntimeInstance;

            if (dispatcher != null)
            {
                dispatcher.OnServiceShutdown();
                return;
            }

            SystemDispatcher.ClearAllLanes();
        }

        private static void ShutdownJobAdmissionServiceForBootstrapTeardown()
        {
            IJobAdmissionService service = GlobalRegistry.JobAdmission;
            if (service != null)
            {
                JobAdmissionSchedulerBridge.ClearService(service);
                GlobalRegistry.UnregisterJobAdmissionRuntime(service);
                service.Dispose();
            }

            _jobAdmissionService = null;
            _jobAdmissionTelemetryBridge = null;
        }

        private static void ShutdownSimulationBucketerServiceForBootstrapTeardown()
        {
            ISimulationBucketer service = GlobalRegistry.SimulationBucketer;
            if (service != null && ReferenceEquals(service, _simulationBucketerService))
            {
                GlobalRegistry.UnregisterSimulationBucketerRuntime(service);
                service.Dispose();
            }

            _simulationBucketerService = null;
        }

        private static void ShutdownGlobalDataVaultForBootstrapTeardown()
        {
            ShutdownMacroDatabaseForBootstrapTeardown();

            if (_globalDataVault == null)
                return;

            if (ReferenceEquals(GlobalRegistry.DataVault, _globalDataVault))
                GlobalRegistry.UnregisterDataVault(_globalDataVault);

            _globalDataVault.Dispose();
            _globalDataVault = null;
        }

        private static void ShutdownMacroDatabaseForBootstrapTeardown()
        {
            if (_macroDatabaseService == null)
                return;

            if (ReferenceEquals(GlobalRegistry.MacroDatabase, _macroDatabaseService))
                GlobalRegistry.UnregisterMacroDatabase(_macroDatabaseService);

            _macroDatabaseService.Shutdown();
            _macroDatabaseService = null;
        }

        /// <summary>
        /// Starts the unified Awaitable bootstrap state machine if it has not already run.
        /// </summary>
        public void BeginBootstrap()
        {
            if (_runtimeOwnerAborted)
                return;

            if (!ClaimRuntimeBootstrapInstance(this))
                return;

            RecoverReloadDisabledStaleBootstrapRun();
            EnsureBootstrapPresentationFallbackCold();

            if (_bootstrapRunInProgress)
                return;

            if (_isBootstrapComplete)
            {
                TryStartCompletedBootstrapHandoff();
                return;
            }

#if UNITY_EDITOR
            if (ShouldDeferBootstrapUntilEditorEnteredPlayMode())
            {
                _editorBootstrapDeferredUntilEnteredPlayMode = true;
                return;
            }
#endif

            GlobalRegistry.BeginRegistration();
            EnsureCrashTelemetryBufferRegistered();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureRuntimeWatchdogRegistered();
            EnsureGCMonitorRegistered();
#endif
            _bootstrapRunInProgress = true;
            _bootstrapStartTimestamp = Stopwatch.GetTimestamp();
            _ = RunBootstrapStateMachineAsync(destroyCancellationToken);
            StartBootstrapRunStartWatchdog();
        }

        private void StartBootstrapRunStartWatchdog()
        {
            if (_bootstrapStartWatchdogActive)
                return;

            _bootstrapStartWatchdogActive = true;
            _ = RunBootstrapRunStartWatchdogAsync(destroyCancellationToken);
        }

        private async Awaitable RunBootstrapRunStartWatchdogAsync(CancellationToken ownerToken)
        {
            bool restartIssued = false;
            try
            {
                while (Application.isPlaying &&
                       _bootstrapRunInProgress &&
                       !_isBootstrapComplete &&
                       !BootstrapStatus.BootStarted)
                {
                    if (_bootstrapStartTimestamp > 0L)
                    {
                        double elapsedSeconds =
                            (Stopwatch.GetTimestamp() - _bootstrapStartTimestamp) / (double)Stopwatch.Frequency;
                        if (elapsedSeconds >= BootstrapRunStartGraceSeconds)
                            break;
                    }

                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ownerToken);
                }

                if (!Application.isPlaying ||
                    ownerToken.IsCancellationRequested ||
                    _isBootstrapComplete ||
                    BootstrapStatus.BootStarted)
                {
                    return;
                }

                RecoverReloadDisabledStaleBootstrapRun();
                _bootstrapStartWatchdogActive = false;
                restartIssued = true;
                EnsureBootstrapProgressAfterLifecycleResume();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException("BootstrapStartWatchdog", exception);
            }
            finally
            {
                if (!restartIssued)
                    _bootstrapStartWatchdogActive = false;
            }
        }

        private void EnsureBootstrapPresentationFallbackCold()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (!Application.isPlaying || Application.isBatchMode || _headlessBootMode)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!IsBootstrapScene(activeScene))
                return;

            if (HasBootstrapPresentationRoot(activeScene))
                return;

            GameObject root = new GameObject(BootstrapPresentationRootName); // COLD ALLOC: bootstrap presentation fallback root prevents black no-camera frame.
            if (activeScene.IsValid())
                SceneManager.MoveGameObjectToScene(root, activeScene);

            Transform rootTransform = root.transform;
            rootTransform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rootTransform.localScale = Vector3.one;

            BootstrapPresentationFallbackRuntime materialOwner = root.AddComponent<BootstrapPresentationFallbackRuntime>();
            Material abyss = CreateBootstrapPresentationMaterial(
                "MAT_Bootstrap_AbyssPlate",
                new Color(0.004f, 0.014f, 0.024f, 1f),
                new Color(0.000f, 0.060f, 0.090f, 1f),
                0.22f);
            Material hull = CreateBootstrapPresentationMaterial(
                "MAT_Bootstrap_PressureHull",
                new Color(0.055f, 0.073f, 0.078f, 1f),
                new Color(0.000f, 0.025f, 0.035f, 1f),
                0.15f);
            Material cyan = CreateBootstrapPresentationMaterial(
                "MAT_Bootstrap_CyanInstrument",
                new Color(0.030f, 0.220f, 0.260f, 1f),
                new Color(0.070f, 0.900f, 1.000f, 1f),
                1.85f);
            Material amber = CreateBootstrapPresentationMaterial(
                "MAT_Bootstrap_AmberWarning",
                new Color(0.320f, 0.155f, 0.020f, 1f),
                new Color(1.000f, 0.440f, 0.060f, 1f),
                1.35f);
            Material glass = CreateBootstrapPresentationMaterial(
                "MAT_Bootstrap_DirtyGlass",
                new Color(0.020f, 0.080f, 0.095f, 0.72f),
                new Color(0.020f, 0.240f, 0.280f, 1f),
                0.52f);
            materialOwner.Register(abyss, hull, cyan, amber, glass);

            GameObject cameraObject = new GameObject(BootstrapPresentationCameraName, typeof(Camera)); // COLD ALLOC: bootstrap-only camera; scene-owned and destroyed on scene transition.
            cameraObject.transform.SetParent(rootTransform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.28f, -6.7f);
            cameraObject.transform.localRotation = Quaternion.Euler(3.5f, 0f, 0f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.001f, 0.007f, 0.014f, 1f);
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 80f;
            camera.depth = BootstrapPresentationCameraDepth;
            camera.allowHDR = true;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            try { cameraObject.tag = "MainCamera"; }
            catch (UnityException ex) { Debug.LogWarning($"[GameBootstrapper] Failed to set MainCamera tag on presentation camera: {ex.Message}"); }

            GameObject keyLight = new GameObject("BOOT_PRESENTATION_CYAN_KEY", typeof(Light)); // COLD ALLOC: one bootstrap key light; no runtime polling.
            keyLight.transform.SetParent(rootTransform, false);
            keyLight.transform.localPosition = new Vector3(-2.4f, 3.2f, -2.1f);
            Light key = keyLight.GetComponent<Light>();
            key.type = LightType.Point;
            key.color = new Color(0.26f, 0.92f, 1f, 1f);
            key.intensity = 4.2f;
            key.range = 8.5f;
            key.shadows = LightShadows.None;

            GameObject fillLight = new GameObject("BOOT_PRESENTATION_AMBER_FILL", typeof(Light)); // COLD ALLOC: warm silhouette cue for boot chamber.
            fillLight.transform.SetParent(rootTransform, false);
            fillLight.transform.localPosition = new Vector3(2.9f, 1.4f, -1.0f);
            Light fill = fillLight.GetComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.46f, 0.10f, 1f);
            fill.intensity = 1.8f;
            fill.range = 6.0f;
            fill.shadows = LightShadows.None;

            CreateBootstrapPresentationBlock(rootTransform, "BOOT_BACKDROP_PRESSURE_GLASS", new Vector3(0f, 1.02f, 2.4f), new Vector3(8.8f, 3.8f, 0.18f), Quaternion.identity, abyss);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_VIEWPORT_GLASS", new Vector3(0f, 1.05f, 1.95f), new Vector3(5.9f, 2.25f, 0.08f), Quaternion.identity, glass);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_TOP_RAIL", new Vector3(0f, 2.34f, 1.72f), new Vector3(6.5f, 0.09f, 0.16f), Quaternion.identity, cyan);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_BOTTOM_RAIL", new Vector3(0f, -0.26f, 1.72f), new Vector3(6.5f, 0.09f, 0.16f), Quaternion.identity, cyan);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_LEFT_RAIL", new Vector3(-3.28f, 1.04f, 1.72f), new Vector3(0.09f, 2.55f, 0.16f), Quaternion.identity, cyan);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_RIGHT_RAIL", new Vector3(3.28f, 1.04f, 1.72f), new Vector3(0.09f, 2.55f, 0.16f), Quaternion.identity, cyan);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_DECK_PLATE", new Vector3(0f, -1.22f, -0.05f), new Vector3(7.8f, 0.18f, 5.0f), Quaternion.identity, hull);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_DECK_WARN_LEFT", new Vector3(-2.35f, -1.08f, -1.65f), new Vector3(1.35f, 0.045f, 0.13f), Quaternion.Euler(0f, 32f, 0f), amber);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_DECK_WARN_RIGHT", new Vector3(2.35f, -1.08f, -1.65f), new Vector3(1.35f, 0.045f, 0.13f), Quaternion.Euler(0f, -32f, 0f), amber);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_SIDE_CONSOLE", new Vector3(-3.8f, -0.35f, 0.10f), new Vector3(0.42f, 1.25f, 1.1f), Quaternion.identity, hull);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_CONSOLE_CYAN_CELL_A", new Vector3(-3.56f, 0.12f, -0.52f), new Vector3(0.035f, 0.32f, 0.46f), Quaternion.identity, cyan);
            CreateBootstrapPresentationBlock(rootTransform, "BOOT_CONSOLE_CYAN_CELL_B", new Vector3(-3.55f, -0.34f, -0.32f), new Vector3(0.035f, 0.18f, 0.78f), Quaternion.identity, cyan);

            CreateBootstrapPresentationText(
                rootTransform,
                "BOOT_LABEL_HECTON",
                "HECTON-8",
                new Vector3(0f, 1.76f, 1.58f),
                0.72f,
                new Color(0.62f, 1f, 0.96f, 1f));
            CreateBootstrapPresentationText(
                rootTransform,
                "BOOT_LABEL_PHASE",
                "BOOTSTRAP LINK / PRESSURE SYSTEMS ONLINE",
                new Vector3(0f, 1.18f, 1.54f),
                0.19f,
                new Color(1f, 0.60f, 0.18f, 1f));
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool HasBootstrapPresentationRoot(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            _bootstrapSceneRootScratch.Clear();
            scene.GetRootGameObjects(_bootstrapSceneRootScratch);

            for (int i = 0; i < _bootstrapSceneRootScratch.Count; i++)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null || !string.Equals(root.name, BootstrapPresentationRootName, StringComparison.Ordinal))
                    continue;

                _bootstrapSceneRootScratch.Clear();
                return true;
            }

            _bootstrapSceneRootScratch.Clear();
            return false;
        }

        private static Material CreateBootstrapPresentationMaterial(
            string materialName,
            Color baseColor,
            Color emissionColor,
            float emissionStrength)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader) { name = materialName }; // COLD ALLOC: bootstrap presentation material; released by BootstrapPresentationFallbackRuntime.
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", baseColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", baseColor);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.58f);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0.18f);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", baseColor.a < 0.98f ? 1f : 0f);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emissionColor * math.max(0f, emissionStrength));
            }

            return material;
        }

        private static void CreateBootstrapPresentationBlock(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            Hecton8.World.WorldGeneratedPrimitiveFactory.CreatePrimitiveVisual(
                parent,
                PrimitiveType.Cube,
                objectName,
                localPosition,
                localRotation,
                localScale,
                material);
        }

        private static void CreateBootstrapPresentationText(
            Transform parent,
            string objectName,
            string textValue,
            Vector3 localPosition,
            float fontSize,
            Color color)
        {
            GameObject textObject = new GameObject(objectName, typeof(TextMeshPro)); // COLD ALLOC: bootstrap label; not present after scene handoff.
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one;

            TextMeshPro text = textObject.GetComponent<TextMeshPro>();
            text.text = textValue;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = color;
            text.raycastTarget = false;
        }
#endif

        private static bool ClaimRuntimeBootstrapInstance(GameBootstrapper instance)
        {
            if (instance == null)
                return false;

            if (GlobalRegistry.Phase == GlobalRegistry.RegistryPhase.Uninitialized)
                GlobalRegistry.BeginRegistration();

            GameBootstrapper registeredBootstrapper = ResolveUsableRuntime();
            if (!ReferenceEquals(registeredBootstrapper, null) &&
                !ReferenceEquals(registeredBootstrapper, instance) &&
                ReferenceEquals(registeredBootstrapper.gameObject, instance.gameObject))
            {
                registeredBootstrapper.AbortDuplicateRuntimeOwner(destroyComponent: false);
                ClearRuntimeMirrorIfOwnedBy(registeredBootstrapper);
                registeredBootstrapper = null;
            }

            if (!ReferenceEquals(registeredBootstrapper, null) &&
                !ReferenceEquals(registeredBootstrapper, instance))
            {
                s_activeRuntimeInstance = registeredBootstrapper;
                instance.AbortDuplicateRuntimeOwner(destroyComponent: false);
                return false;
            }

            registeredBootstrapper = GlobalRegistry.BootstrapperRuntime;
            if (!ReferenceEquals(registeredBootstrapper, null) &&
                !ReferenceEquals(registeredBootstrapper, instance))
            {
                registeredBootstrapper.AbortDuplicateRuntimeOwner(destroyComponent: false);
                ClearRuntimeMirrorIfOwnedBy(registeredBootstrapper);
            }

            instance._runtimeOwnerAborted = false;
            s_activeRuntimeInstance = instance;
            if (GlobalRegistry.Phase == GlobalRegistry.RegistryPhase.Registering)
                GlobalRegistry.RegisterBootstrapperRuntime(instance);

            bool ownsRuntime =
                ReferenceEquals(s_activeRuntimeInstance, instance) &&
                (ReferenceEquals(GlobalRegistry.BootstrapperRuntime, instance) ||
                 GlobalRegistry.Phase != GlobalRegistry.RegistryPhase.Registering);
            instance._runtimeOwnerAborted = !ownsRuntime;
            if (instance._runtimeOwnerAborted)
                instance.AbortDuplicateRuntimeOwner(destroyComponent: false);
            return ownsRuntime;
        }

        private void AbortDuplicateRuntimeOwner(bool destroyComponent)
        {
            _runtimeOwnerAborted = true;
            _bootstrapRunInProgress = false;
            _sceneActivationRunInProgress = false;
            _sceneActivationRequested = false;
            _sceneActivationStarted = false;
            _slowTickableRegistered = false;
            _bootstrapStartWatchdogActive = false;
            enabled = false;
            if (destroyComponent)
                Destroy(this);
        }

        private static GameBootstrapper ResolveUsableRuntime()
        {
            GameBootstrapper runtime = s_activeRuntimeInstance;
            if (IsBootstrapperRuntimeUsable(runtime))
                return runtime;

            ClearRuntimeMirrorIfOwnedBy(runtime);

            runtime = GlobalRegistry.BootstrapperRuntime;
            if (IsBootstrapperRuntimeUsable(runtime))
            {
                s_activeRuntimeInstance = runtime;
                return runtime;
            }

            ClearRuntimeMirrorIfOwnedBy(runtime);
            return null;
        }

        private static bool IsBootstrapperRuntimeUsable(GameBootstrapper runtime)
        {
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
        }

        private static void ClearRuntimeMirrorIfOwnedBy(GameBootstrapper runtime)
        {
            if (ReferenceEquals(runtime, null))
                return;

            GlobalRegistry.ClearBootstrapperRuntime(runtime);
            if (ReferenceEquals(s_activeRuntimeInstance, runtime))
                s_activeRuntimeInstance = null;
        }

        private void RecoverReloadDisabledStaleBootstrapRun()
        {
            if (!_bootstrapRunInProgress || _isBootstrapComplete)
                return;

            if (_bootstrapStartTimestamp > 0L)
            {
                if (BootstrapStatus.BootStarted)
                    return;

                double elapsedSeconds = (Stopwatch.GetTimestamp() - _bootstrapStartTimestamp) / (double)Stopwatch.Frequency;
                if (elapsedSeconds < BootstrapRunStartGraceSeconds)
                    return;
            }

            _bootstrapRunInProgress = false;
            _currentPhase = BootstrapPhase.HardwareCheck;
            _bootstrapDurationTelemetryPublished = false;
            _sceneActivationRunInProgress = false;
            _sceneActivationRequested = false;
            _sceneActivationStarted = false;
            _sceneActivationSceneHandle = ulong.MaxValue;
            _debugSceneActivationStep = "Not started";
            _debugSceneActivationCompleted = false;
            _bootstrapStartWatchdogActive = false;
        }

        private void ResetTransientRuntimeStateForReloadDisabledPlayMode()
        {
            _bootstrapRunInProgress = false;
            _sceneActivationRunInProgress = false;
            _sceneActivationRequested = false;
            _sceneActivationStarted = false;
            _sceneActivationSceneHandle = ulong.MaxValue;
            _isLoadingSave = false;
            _slowTickableRegistered = false;
            _hotSwapRegistered = false;
            _bootstrapStartWatchdogActive = false;
            _nextServiceHeartbeatPollTime = 0d;
            _backgroundDomainHandshakeState = 0;
            _backgroundDomainHandshakePath = null;
            _backgroundDomainHandshakeFailureCode = 0;
            _debugSceneActivationStep = "Not started";
            _debugSceneActivationCompleted = false;
            _bootstrapExecutionOrderCount = 0;
            Array.Clear(_heartbeatTickSamples, 0, _heartbeatTickSamples.Length);
            Array.Clear(_heartbeatFrozenSamples, 0, _heartbeatFrozenSamples.Length);
        }

        private bool TryStartCompletedBootstrapHandoff()
        {
            if (!Application.isPlaying || _sceneActivationRunInProgress)
                return false;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!IsBootstrapScene(activeScene))
                return false;

            if (!TryResolveBootstrapGameplayHandoffScene(out string gameplaySceneName))
                return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[GameBootstrapper] Completed bootstrap handoff loading pending target scene '" + gameplaySceneName + "'.");
#endif
            _sceneActivationRunInProgress = true;
            _ = RunCompletedBootstrapHandoffAsync(gameplaySceneName);
            return true;
        }

        /// <summary>
        /// Requests gameplay scene activation through the unified bootstrap owner.
        /// </summary>
        public static void RequestSceneActivation()
        {
            Debug.Log("[GameBootstrapper-DEBUG] RequestSceneActivation called! StackTrace: " + StackTraceUtility.ExtractStackTrace());
            GameBootstrapper bootstrapper = EnsureRuntimeInstance();
            if (bootstrapper == null)
                return;

            bootstrapper.ScheduleSceneActivation();
        }

        public static void RequestSceneActivation(MonoBehaviour ignoredOwner)
        {
            RequestSceneActivation();
        }

        private void ScheduleSceneActivation()
        {
            _sceneActivationRequested = true;
            Scene activeScene = SceneManager.GetActiveScene();
            ulong activeSceneHandle = activeScene.handle.GetRawData();
            if (_sceneActivationSceneHandle != activeSceneHandle)
            {
                _sceneActivationSceneHandle = activeSceneHandle;
                _sceneActivationStarted = false;
                _debugSceneActivationCompleted = false;
            }

            if (!_isBootstrapComplete || _sceneActivationRunInProgress)
                return;

            _sceneActivationRunInProgress = true;
            if (GlobalRegistry.Phase != GlobalRegistry.RegistryPhase.Ready)
                GlobalRegistry.BeginRegistration();
            _ = RunSceneActivationAsync(destroyCancellationToken);
        }

        private async Awaitable<bool> RunBootstrapStateMachineAsync(CancellationToken ownerToken)
        {
            BootstrapStatus.BeginBoot();
            WriteBootStateRecord(BootStateMarker.Started, BootstrapPhase.HardwareCheck, GlobalRegistryServiceSlot.Unknown);
            _bootstrapStartTimestamp = Stopwatch.GetTimestamp();
            _bootstrapDurationTelemetryPublished = false;
            CancellationToken ct = ownerToken;

            try
            {
                if (!await RunBootstrapPhaseAsync(BootstrapPhase.HardwareCheck, BootstrapStepToken.HardwareCheck, InitializeHardwareCheckPhaseAsync, ct))
                    return false;
                if (!await RunBootstrapPhaseAsync(BootstrapPhase.MemoryPreWarm, BootstrapStepToken.MemoryPreWarm, InitializeMemoryPreWarmPhaseAsync, ct))
                    return false;
                if (!await RunBootstrapPhaseAsync(BootstrapPhase.CoreServices, BootstrapStepToken.CoreServices, InitializeCoreServicesPhaseAsync, ct))
                    return false;
                if (!await RunBootstrapPhaseAsync(BootstrapPhase.Environment, BootstrapStepToken.Environment, InitializeEnvironmentPhaseAsync, ct))
                    return false;
                if (!_headlessBootMode &&
                    !await RunBootstrapPhaseAsync(BootstrapPhase.Player, BootstrapStepToken.Player, InitializePlayerPhaseAsync, ct))
                {
                    return false;
                }

                if (!_headlessBootMode &&
                    !await RunBootstrapPhaseAsync(BootstrapPhase.UI, BootstrapStepToken.UI, InitializeUIPhaseAsync, ct))
                {
                    return false;
                }

                EnsureExtendedRegistryCoverageForActiveScene();
                _isBootstrapComplete = true;
                _registryCoreReadyChecksum = CalculateRegistryActiveServiceTypeHash();
                if (expectedBiosRegistryFnv1a != 0u && _registryCoreReadyChecksum != expectedBiosRegistryFnv1a)
                {
                    HandleFatalBootstrapException(
                        "BIOSIntegrityChecksum",
                        new InvalidOperationException("[GameBootstrapper] BIOS integrity checksum mismatch."));
                    return false;
                }

                WriteBootStateRecord(BootStateMarker.CoreReady, BootstrapPhase.CoreServices, GlobalRegistryServiceSlot.Unknown);
                BootstrapBiosErrorOverlay.Hide();
                DisableGarbageCollectorAfterCoreReady();
                BootstrapEvents.TryNotifyBootstrapComplete();

                if (!await RunBootstrapPhaseAsync(BootstrapPhase.SceneActivate, BootstrapStepToken.SceneActivate, InitializeSceneActivatePhaseAsync, ct))
                    return false;

                GlobalRegistry.LockReady();
                PublishTotalBootTimeTelemetry();
                _currentPhase = BootstrapPhase.Complete;
                WriteBootStateRecord(BootStateMarker.Complete, BootstrapPhase.Complete, GlobalRegistryServiceSlot.Unknown);
                return true;
            }
            catch (OperationCanceledException)
            {
                _currentPhase = BootstrapPhase.Fatal;
                return false;
            }
            catch (Exception exception)
            {
                _currentPhase = BootstrapPhase.Fatal;
                WriteBootStateRecord(BootStateMarker.Fatal, BootstrapPhase.Fatal, GlobalRegistryServiceSlot.Unknown);
                HandleFatalBootstrapException("BootstrapEntry", exception);
                return false;
            }
            finally
            {
                _bootstrapRunInProgress = false;
            }
        }

        private static void PublishTotalBootTimeTelemetry()
        {
            if (_bootstrapDurationTelemetryPublished || _bootstrapStartTimestamp <= 0L)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - _bootstrapStartTimestamp;
            float elapsedMilliseconds = (float)(elapsedTicks * 1000d / Stopwatch.Frequency);
            GlobalTelemetryBus.PublishBootstrapDuration(
                _BootstrapTotalBootTimeHash,
                _GameBootstrapperContextHash,
                elapsedMilliseconds);
            _bootstrapDurationTelemetryPublished = true;
        }

        private async Awaitable<bool> RunBootstrapPhaseAsync(
            BootstrapPhase phase,
            BootstrapStepToken stepToken,
            Func<CancellationToken, Awaitable<bool>> phaseAction,
            CancellationToken ct)
        {
            _currentPhase = phase;
            WriteBootStateRecord(BootStateMarker.PhaseStarted, phase, GlobalRegistryServiceSlot.Unknown);
            BootstrapStatus.BeginStep(stepToken);
            long phaseStartTimestamp = Stopwatch.GetTimestamp();
            try
            {
                bool phaseComplete = phaseAction == null || await phaseAction(ct);
                if (!phaseComplete)
                {
                    LogBootstrapPhaseFailure(phase);
                    _currentPhase = BootstrapPhase.Fatal;
                }

                return phaseComplete;
            }
            catch (OperationCanceledException)
            {
                _currentPhase = BootstrapPhase.Fatal;
                throw;
            }
            catch (Exception exception)
            {
                _currentPhase = BootstrapPhase.Fatal;
                HandleFatalBootstrapException(ResolveBootstrapPhaseName(phase), exception);
                return false;
            }
            finally
            {
                double elapsedMilliseconds =
                    (Stopwatch.GetTimestamp() - phaseStartTimestamp) * 1000.0 / Stopwatch.Frequency;
                CrashTelemetryBuffer.RecordBootstrapPhaseDuration(stepToken, elapsedMilliseconds);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                BootstrapHealthMonitor.RecordPhaseDuration(stepToken, elapsedMilliseconds);
#endif
                BootstrapStatus.EndStep(stepToken);
            }
        }

        private static string ResolveBootstrapPhaseName(BootstrapPhase phase)
        {
            switch (phase)
            {
                case BootstrapPhase.HardwareCheck:
                    return nameof(BootstrapPhase.HardwareCheck);
                case BootstrapPhase.MemoryPreWarm:
                    return nameof(BootstrapPhase.MemoryPreWarm);
                case BootstrapPhase.CoreServices:
                    return nameof(BootstrapPhase.CoreServices);
                case BootstrapPhase.Environment:
                    return nameof(BootstrapPhase.Environment);
                case BootstrapPhase.Player:
                    return nameof(BootstrapPhase.Player);
                case BootstrapPhase.UI:
                    return nameof(BootstrapPhase.UI);
                case BootstrapPhase.SceneActivate:
                    return nameof(BootstrapPhase.SceneActivate);
                case BootstrapPhase.Complete:
                    return nameof(BootstrapPhase.Complete);
                case BootstrapPhase.Fatal:
                    return nameof(BootstrapPhase.Fatal);
                default:
                    return "Unknown";
            }
        }

        private static BootstrapStepToken ResolveBootstrapStepToken(BootstrapPhase phase)
        {
            switch (phase)
            {
                case BootstrapPhase.HardwareCheck:
                    return BootstrapStepToken.HardwareCheck;
                case BootstrapPhase.MemoryPreWarm:
                    return BootstrapStepToken.MemoryPreWarm;
                case BootstrapPhase.CoreServices:
                    return BootstrapStepToken.CoreServices;
                case BootstrapPhase.Environment:
                    return BootstrapStepToken.Environment;
                case BootstrapPhase.Player:
                    return BootstrapStepToken.Player;
                case BootstrapPhase.UI:
                    return BootstrapStepToken.UI;
                case BootstrapPhase.SceneActivate:
                    return BootstrapStepToken.SceneActivate;
                default:
                    return BootstrapStepToken.None;
            }
        }

        private async Awaitable<bool> InitializeHardwareCheckPhaseAsync(CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                _headlessBootMode = IsHeadlessBootRequested();
                InspectPreviousBootState();
                global::Hecton8.Core.HectonHardwareProfile hardwareProfile = CaptureHardwareProfile();
                GlobalRegistry.RegisterHardwareProfile(in hardwareProfile);
                ApplyMemoryGate(in hardwareProfile);
                ApplyScalabilityMatrix(in hardwareProfile);
                ValidateOceanKinematicsPluginContract();
                StartBackgroundDomainHandshake();

                Scene activeScene = SceneManager.GetActiveScene();
                if (!TryRecoverEntryVector(activeScene, false))
                    return false;

                RegisterSceneLoadGuard();
                if (!_headlessBootMode)
                    EnsureBootstrapAudioListener(activeScene);

                bool dependencyGraphValid = TryBuildBootstrapDependencyExecutionOrder(
                    _bootstrapExecutionOrder,
                    out _bootstrapExecutionOrderCount);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return dependencyGraphValid;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> InitializeMemoryPreWarmPhaseAsync(CancellationToken ct)
        {
            try
            {
                _preWarmAssetsReady = false;
                InitializeBootstrapAllocators();
                BootstrapStatus.PulseActiveStep(BootstrapStepToken.MemoryPreWarm);
                BinaryLayoutManifest.VerifyColdBoot();
                BootstrapStatus.PulseActiveStep(BootstrapStepToken.MemoryPreWarm);
                uint appVersionHash = global::Hecton8.Data.H8DataHash.ComputeFnv1A32(Application.version.AsSpan());
                if (!await InitializeBootstrapDataMonolithAsync(appVersionHash, ct))
                {
                    Debug.LogError("[GameBootstrapper] Data Monolith boot validation failed. status=" + _lastDataMonolithBootstrapStatus);
                    return false;
                }

                BootstrapStatus.PulseActiveStep(BootstrapStepToken.MemoryPreWarm);
                InitializeBootstrapEventBuses();
                BootstrapStatus.PulseActiveStep(BootstrapStepToken.MemoryPreWarm);
                InitializeBootstrapMmfStorage();
                BootstrapStatus.PulseActiveStep(BootstrapStepToken.MemoryPreWarm);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> InitializePresentationBootstrapAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                LogBootstrapCoreServicesSubstepFailure("presentation_cancelled_before_start");
                return false;
            }

            if (_headlessBootMode)
            {
                _preWarmAssetsReady = true;
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
                return !ct.IsCancellationRequested;
            }

            WarmMathLodShaderKeywords();
            VRAMEnforcer.InitializeRuntimeBudget();
            VRAMOptimizationBootstrap.EnsureRuntimeManagers();
            SceneInstantiationGate gate = SceneInstantiationGate.EnsureRuntimeInstance();
            PersistRuntimeService(gate);
            EnsureBootstrapShaderWarmupTelemetryRing();
            BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);

#if UNITY_EDITOR
            // Editor no-domain-reload PlayMode can spend several seconds in Unity's scene-backup integration
            // before the first bootstrap await. Restart this phase timer at the code-owned presentation gate.
            BootstrapStatus.BeginStep(BootstrapStepToken.CoreServices);
#endif

#if UNITY_ADDRESSABLES_EXIST
            if (!await PreWarmTierAddressableTextureGroupAsync(ct))
            {
                LogBootstrapCoreServicesSubstepFailure("tier_addressable_texture_prewarm");
                return false;
            }
            BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
#endif

            if (!await WarmConfiguredShaderVariantCollectionsAsync(ct))
            {
                LogBootstrapCoreServicesSubstepFailure("shader_variant_warmup");
                return false;
            }

            BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
            if (ct.IsCancellationRequested)
            {
                LogBootstrapCoreServicesSubstepFailure("presentation_cancelled_after_shader_warmup");
                return false;
            }

            _preWarmAssetsReady = true;
            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
            bool completed = !ct.IsCancellationRequested;
            if (!completed)
                LogBootstrapCoreServicesSubstepFailure("presentation_cancelled_after_frame_yield");

            return completed;
        }

        private void InitializeBootstrapAllocators()
        {
            byte scalabilityProfile = ResolveBootstrapScalabilityProfileByte();
            VaultMemoryLayoutConfig memoryLayoutConfig = vaultConfigurationAsset != null
                ? vaultConfigurationAsset.BuildRuntimeConfig(scalabilityProfile)
                : VaultMemoryMath.BuildMockConfig(scalabilityProfile);
            long vaultArenaLimitBytes = memoryLayoutConfig.ArenaLimitBytes > 0L
                ? memoryLayoutConfig.ArenaLimitBytes
                : GlobalDataVault.ResolveArenaCapacityLimit(scalabilityProfile);
            H8Memory.Initialize(poolCapBytes: vaultArenaLimitBytes);
            H8Memory.ConfigurePoolCap(vaultArenaLimitBytes);
            InstallH8MemoryFatalDumpHook();
            EnsureGlobalDataVaultRegistered(vaultArenaLimitBytes, memoryLayoutConfig.BufferCapacity, in memoryLayoutConfig, vaultConfigurationAsset != null);
            NativeArenaAllocator.Initialize();
        }

        private static void InstallH8MemoryFatalDumpHook()
        {
            Application.logMessageReceived -= HandleH8MemoryFatalLog;
            Application.logMessageReceived += HandleH8MemoryFatalLog;
            _h8MemoryFatalLogHooked = true;
            _h8MemoryFatalDumpWritten = false;
        }

        private static void RemoveH8MemoryFatalDumpHook()
        {
            if (!_h8MemoryFatalLogHooked)
                return;

            Application.logMessageReceived -= HandleH8MemoryFatalLog;
            _h8MemoryFatalLogHooked = false;
            _h8MemoryFatalDumpWritten = false;
        }

        private static void HandleH8MemoryFatalLog(string condition, string stackTrace, LogType type)
        {
            if (_h8MemoryFatalDumpWritten)
                return;

            if (type != LogType.Exception &&
                type != LogType.Assert &&
                (type != LogType.Error || !IsFatalMemoryDumpCandidate(condition)))
            {
                return;
            }

            _h8MemoryFatalDumpWritten = true;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(logDirectory);
            string path = Path.Combine(logDirectory, "Dump_CORE_DATA_VAULT_WARDEN.txt");
            H8Memory.DumpAllocationTableText(path);
        }

        private static bool IsFatalMemoryDumpCandidate(string condition)
        {
            if (string.IsNullOrEmpty(condition))
                return false;

            return condition.IndexOf("fatal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                condition.IndexOf("crash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                condition.IndexOf("nan", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsureGlobalDataVaultRegistered(
            long arenaCapacityLimitBytes,
            int bufferCapacity,
            in VaultMemoryLayoutConfig authoredConfig,
            bool hasAuthoredConfig)
        {
            if (_globalDataVault == null)
                _globalDataVault = GlobalDataVault.Create(bufferCapacity, arenaCapacityLimitBytes);

            if (!ReferenceEquals(GlobalRegistry.DataVault, _globalDataVault))
                GlobalRegistry.RegisterDataVault(_globalDataVault);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            VaultLegacyBinaryArchaeology.TryBootstrapMemoryLayout(
                _globalDataVault,
                projectRoot,
                ResolveBootstrapScalabilityProfileByte(),
                out _);
            if (hasAuthoredConfig)
                VaultLegacyBinaryArchaeology.WriteMemoryLayoutConfig(_globalDataVault, in authoredConfig);

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(projectRoot))
            {
                string overrideCsvPath = Path.Combine(projectRoot, "memory_overrides.csv");
                VaultLegacyBinaryArchaeology.TryApplyMemoryOverridesCsv(_globalDataVault, overrideCsvPath);
            }
#endif

            PreallocateDataVaultPrimaryBuffers(_globalDataVault, in authoredConfig);
        }

        private static void PreallocateDataVaultPrimaryBuffers(IDataVault vault, in VaultMemoryLayoutConfig memoryLayoutConfig)
        {
            if (vault == null)
                return;

            PrewarmVaultLane<double>(
                vault,
                BufferID.H8Time,
                (int)H8TimeSlot.Count,
                SystemID.SystemDispatcher);
            PrewarmVaultLane<double3>(
                vault,
                BufferID.RigidbodyAUPs,
                512,
                SystemID.GlobalPhysicsStateManager);
            VaultSovereigntyMaintenance.PrewarmBuffers(
                vault,
                memoryLayoutConfig.HotEntityCapacity > 0
                    ? memoryLayoutConfig.HotEntityCapacity
                    : VaultSovereigntyMaintenance.DefaultHotEntityCapacity);
        }

        private static bool PrewarmVaultLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID ownerSystemId) where T : struct
        {
            if (vault == null)
                return false;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                ownerSystemId,
                NativeArrayOptions.ClearMemory);
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static void InitializeBootstrapEventBuses()
        {
            GlobalTelemetryBus.Initialize();
            SignalCorridorRuntime.EnsureInitialized();
            EnsureSimulationBucketerRegistered();
            EnsureJobAdmissionServiceRegistered();
        }

        private static void InitializeBootstrapMmfStorage()
        {
            PreInitAssetIdMap.Initialize();
            EnsureMacroDatabaseRegistered();
        }

        private static void EnsureMacroDatabaseRegistered()
        {
            if (_macroDatabaseService == null)
                _macroDatabaseService = new H8MacroDatabaseService();

            if (ReferenceEquals(GlobalRegistry.MacroDatabase, _macroDatabaseService))
                return;

            string databaseDirectory = Path.Combine(Application.persistentDataPath, "H8_MacroDB");
            string databasePath = Path.Combine(databaseDirectory, "macro_world.h8db");
            MacroDatabaseConfig config = MacroDatabaseConfig.Default;
            MacroDatabaseSignalBridge signalBridge = new MacroDatabaseSignalBridge();
            if (!_macroDatabaseService.Initialize(databasePath, in config, _globalDataVault, signalBridge))
            {
                _macroDatabaseService.Shutdown();
                _macroDatabaseService = null;
                return;
            }

            GlobalRegistry.RegisterMacroDatabase(_macroDatabaseService);
        }

        private static async Awaitable<bool> InitializeBootstrapDataMonolithAsync(uint appVersionHash, CancellationToken ct)
        {
            bool failIfMissing = true;
            bool allowEditorMissingOverride = IsEditorMissingDataMonolithOverrideEnabled();
            global::Hecton8.Data.H8DataBlobLoadResult result = default;
            for (int attempt = 0; attempt < DataMonolithBootstrapMaxAttempts; attempt++)
            {
                result = await global::Hecton8.Data.H8StaticDataArena.TryInitializeFromStreamingAssetsAsync(
                    _globalDataVault,
                    0u,
                    appVersionHash,
                    failIfMissing,
                    ct);

                if (result.Loaded)
                {
                    _lastDataMonolithBootstrapStatus = ResolveDataMonolithStatusLabel(result.Status);
                    return true;
                }

                if (result.Status == global::Hecton8.Data.H8DataBlobLoadStatus.ReadyLocked &&
                    global::Hecton8.Data.H8StaticDataArena.IsLoaded)
                {
                    _lastDataMonolithBootstrapStatus = ResolveDataMonolithStatusLabel(result.Status);
                    return true;
                }

                if (result.Status == global::Hecton8.Data.H8DataBlobLoadStatus.Missing)
                {
                    _lastDataMonolithBootstrapStatus = allowEditorMissingOverride
                        ? "missing_editor_local_override"
                        : ResolveDataMonolithStatusLabel(result.Status);
                    if (allowEditorMissingOverride)
                    {
                        RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
                        RuntimeDiagnosticsTrace.WriteEvent(
                            "bootstrap.datamonolith.editor_missing_override",
                            _lastDataMonolithBootstrapStatus);
                        return true;
                    }

                    return false;
                }

                if (ct.IsCancellationRequested ||
                    result.Status != global::Hecton8.Data.H8DataBlobLoadStatus.ReadFailed)
                {
                    break;
                }

                BootstrapStatus.PulseActiveStep(BootstrapStepToken.MemoryPreWarm);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            }

            _lastDataMonolithBootstrapStatus = ResolveDataMonolithStatusLabel(result.Status);
            RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
            RuntimeDiagnosticsTrace.WriteEvent(
                "bootstrap.datamonolith.failed",
                _lastDataMonolithBootstrapStatus);
            return false;
        }

        private static bool IsEditorMissingDataMonolithOverrideEnabled()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(AllowMissingDataMonolithEditorPrefsKey, false))
                return true;

            string environmentValue = global::System.Environment.GetEnvironmentVariable(AllowMissingDataMonolithEditorEnvironmentVariable);
            if (IsExplicitTrue(environmentValue))
                return true;

            string[] arguments = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], AllowMissingDataMonolithEditorCommandLineArg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
#endif
            return false;
        }

        private static bool IsExplicitTrue(string value)
        {
            return string.Equals(value, "1", StringComparison.Ordinal) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDataMonolithStatusLabel(global::Hecton8.Data.H8DataBlobLoadStatus status)
        {
            switch (status)
            {
                case global::Hecton8.Data.H8DataBlobLoadStatus.None: return "none";
                case global::Hecton8.Data.H8DataBlobLoadStatus.Loaded: return "loaded";
                case global::Hecton8.Data.H8DataBlobLoadStatus.Missing: return "missing";
                case global::Hecton8.Data.H8DataBlobLoadStatus.FileTooSmall: return "file_too_small";
                case global::Hecton8.Data.H8DataBlobLoadStatus.FileTooLarge: return "file_too_large";
                case global::Hecton8.Data.H8DataBlobLoadStatus.ReadFailed: return "read_failed";
                case global::Hecton8.Data.H8DataBlobLoadStatus.BadMagic: return "bad_magic";
                case global::Hecton8.Data.H8DataBlobLoadStatus.UnsupportedVersion: return "unsupported_version";
                case global::Hecton8.Data.H8DataBlobLoadStatus.BadChecksum: return "bad_checksum";
                case global::Hecton8.Data.H8DataBlobLoadStatus.HeaderMismatch: return "header_mismatch";
                case global::Hecton8.Data.H8DataBlobLoadStatus.InvalidSectionTable: return "invalid_section_table";
                case global::Hecton8.Data.H8DataBlobLoadStatus.ReadyLocked: return "ready_locked";
                default: return "unknown";
            }
        }

        private async Awaitable<bool> InitializeCoreServicesPhaseAsync(CancellationToken ct)
        {
            try
            {
                if (!await JoinBackgroundDomainHandshakeAsync(ct))
                {
                    LogBootstrapCoreServicesSubstepFailure("background_domain_handshake");
                    return false;
                }

                bool initialized = await InitializeCoreLayerAsync(ct);
                BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
                if (!initialized)
                {
                    LogBootstrapCoreServicesSubstepFailure("core_layer");
                    return false;
                }

                if (!await WarmObjectPoolPresetsAsync(ct))
                {
                    LogBootstrapCoreServicesSubstepFailure("object_pool_preset_warmup");
                    return false;
                }
                BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
                if (!await InitializePresentationBootstrapAsync(ct))
                {
                    LogBootstrapCoreServicesSubstepFailure("presentation_bootstrap");
                    return false;
                }

                BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                LogBootstrapCoreServicesSubstepFailure("core_services_operation_cancelled");
                return false;
            }
        }

        private static async Awaitable<bool> WarmObjectPoolPresetsAsync(CancellationToken ct)
        {
            ObjectPoolManager objectPoolManager = null;
            ObjectPoolManager.TryResolveActiveRuntime(ref objectPoolManager);

            if (objectPoolManager == null || objectPoolManager.AreWarmupPresetsCompleted)
                return true;

            return await objectPoolManager.WarmupPresetsAsync(
                ObjectPoolWarmupFrameBudgetMilliseconds,
                ct);
        }

        private async Awaitable<bool> InitializeEnvironmentPhaseAsync(CancellationToken ct)
        {
            try
            {
                bool initialized = await InitializeEnvironmentLayerAsync(ct);
                if (initialized && !await WarmEnvironmentObjectPoolsAsync(ct))
                    return false;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return initialized;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static async Awaitable<bool> WarmEnvironmentObjectPoolsAsync(CancellationToken ct)
        {
            ObjectPoolManager objectPoolManager = GlobalRegistry.ObjectPool;
            RandomEventSystem randomEvents = GlobalRegistry.RandomEvents;
            if (objectPoolManager == null || randomEvents == null)
                return true;

            return await randomEvents.WarmMeteorSplashPoolAsync(
                objectPoolManager,
                ObjectPoolWarmupFrameBudgetMilliseconds,
                ct);
        }

        private async Awaitable<bool> InitializePlayerPhaseAsync(CancellationToken ct)
        {
            try
            {
                bool initialized = await InitializePlayerLayerAsync(ct);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return initialized;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> InitializeUIPhaseAsync(CancellationToken ct)
        {
            try
            {
                if (!await InitializeUILayerAsync(ct))
                    return false;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> InitializeSceneActivatePhaseAsync(CancellationToken ct)
        {
            try
            {
                Scene activeScene = SceneManager.GetActiveScene();
                // Headless ecology/batch runs stay on 00_BOOTSTRAP. A stale PlayerPrefs handoff
                // (GameStartContextHolder, up to 900s TTL) used to win before the headless short-circuit
                // and enter LoadGameplaySceneFromBootstrapHandoffAsync, which awaits NextFrameAsync until
                // scene load completes. In -batchmode that await is Task.Yield, not a player-loop tick,
                // so progress never advances → BATCH_TIMEOUT with no MarkMainMenu / no ecology samples.
                // Headless must win first: clear any cold handoff and mark menu reached without loading.
                Debug.Log(
                    $"[GameBootstrapper-DEBUG] InitializeSceneActivatePhaseAsync: activeScene={activeScene.name} headless={_headlessBootMode}");
                if (IsBootstrapScene(activeScene))
                {
                    if (_headlessBootMode)
                    {
                        if (TryResolveBootstrapGameplayHandoffScene(out string ignoredHeadlessHandoffScene))
                        {
                            Debug.Log(
                                "[GameBootstrapper-DEBUG] Headless SceneActivate ignoring stale gameplay handoff '" +
                                ignoredHeadlessHandoffScene +
                                "' (remain on bootstrap; ecology runner does not need 01_MAIN_MENU / gameplay scene).");
                        }

                        GameStartContextHolder.Reset();
                        BootstrapStatus.MarkMainMenuReached();
                        // Headless short-circuit previously only MarkMainMenuReached.
                        // SystemDispatcher.ShouldSkipLaneDuringBootstrap skips PriorityLayer.Player
                        // while !BootstrapState.IsGameReady. LateFrame biomass drain is on Player
                        // (HeadlessSimulationRunner), so ecology never advances without this.
                        // Full ExecuteSceneActivationAsync publishes GameReady~7749; headless must mirror.
                        BootstrapState.PublishGameReady(true);
                        BootstrapState.PublishBootstrapPresence(false);
                        Debug.Log("[GameBootstrapper-DEBUG] Headless SceneActivate short-circuit: MarkMainMenuReached + PublishGameReady on bootstrap");
                        return true;
                    }

                    if (TryResolveBootstrapGameplayHandoffScene(out string gameplaySceneName))
                    {
                        Debug.Log("[GameBootstrapper-DEBUG] LoadGameplaySceneFromBootstrapHandoffAsync");
                        return await LoadGameplaySceneFromBootstrapHandoffAsync(gameplaySceneName, ct);
                    }

                    GameStartContextHolder.Reset();

#if UNITY_EDITOR
                    if (HectonForceSandboxScene)
                    {
                        return await LoadSandboxSceneAsync(ct);
                    }
#endif
                    return await LoadMainMenuAsync(ct);
                }

                if (!_sceneActivationRequested && BootstrapState.IsGameReady)
                {
                    Debug.Log("[GameBootstrapper-DEBUG] Scene not bootstrap, IsGameReady=true");
                    return true;
                }

                Debug.Log("[GameBootstrapper-DEBUG] Calling ExecuteSceneActivationAsync from InitializeSceneActivatePhaseAsync");
                _sceneActivationRequested = true;
                BootstrapState.PublishBootstrapPresence(true);
                return await ExecuteSceneActivationAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> RunSceneActivationAsync(CancellationToken ownerToken)
        {
            try
            {
                bool activated = await ExecuteSceneActivationAsync(ownerToken);
                if (activated)
                    GlobalRegistry.LockReady();

                return activated;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException(nameof(BootstrapPhase.SceneActivate), exception);
                return false;
            }
            finally
            {
                _sceneActivationRequested = false;
                _sceneActivationRunInProgress = false;
            }
        }

        private async Awaitable RunCompletedBootstrapHandoffAsync(string sceneName)
        {
            using CancellationTokenSource handoffTimeout = new CancellationTokenSource();
            handoffTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(
                BootstrapCompletedHandoffWatchdogSeconds,
                bootstrapTimeout + BootstrapSceneLoadWatchdogSeconds)));

            try
            {
                await LoadGameplaySceneFromBootstrapHandoffAsync(sceneName, handoffTimeout.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException(nameof(BootstrapPhase.SceneActivate), exception);
            }
            finally
            {
                _sceneActivationRequested = false;
                _sceneActivationRunInProgress = false;
            }
        }

        private async Awaitable<bool> ExecuteSceneActivationAsync(CancellationToken ct)
        {
            try
            {
                return await ExecuteSceneReadinessGatesAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

#if UNITY_EDITOR
        private static async Awaitable<bool> LoadSandboxSceneAsync(CancellationToken ct)
        {
            try
            {
                Debug.Log("[GameBootstrapper-DEBUG] Bypassing Main Menu and forcing 020_RENDER_SANDBOX");
                string sandboxPath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";
                AsyncOperation loadOperation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    sandboxPath,
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));

                if (loadOperation == null)
                    return false;

                while (!loadOperation.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                BootstrapStatus.MarkMainMenuReached();
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
#endif

        private static async Awaitable<bool> LoadMainMenuAsync(CancellationToken ct)
        {
            AsyncOperation loadOperation = null;
            try
            {
                loadOperation = LoadProductionSceneAsync(MainMenuScenePath, LoadSceneMode.Single);
                if (loadOperation == null)
                    return false;

                loadOperation.allowSceneActivation = false;
                int waitFrames = 0;
                long waitStartTimestamp = Stopwatch.GetTimestamp();
                while (loadOperation.progress < 0.9f)
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, BootstrapSceneLoadWatchdogSeconds, out double elapsedSeconds))
                    {
                        LogBootstrapSceneLoadWatchdog("main-menu load", loadOperation.progress, waitFrames, elapsedSeconds);
                        return false;
                    }

                    waitFrames++;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                if (!await WaitForBootstrapActivationGatesAsync(ct))
                    return false;

                if (!TryValidateSceneRootBudget(MainMenuSceneName, "bootstrap-main-menu-preactivation"))
                    return false;

                SceneRuntimeService.ReleaseSceneActivation(loadOperation);
                waitFrames = 0;
                waitStartTimestamp = Stopwatch.GetTimestamp();
                while (!loadOperation.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, BootstrapSceneLoadWatchdogSeconds, out double elapsedSeconds))
                    {
                        LogBootstrapSceneLoadWatchdog("main-menu activation", loadOperation.progress, waitFrames, elapsedSeconds);
                        return false;
                    }

                    waitFrames++;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                BootstrapStatus.MarkMainMenuReached();
                return true;
            }
            catch (OperationCanceledException)
            {
                if (loadOperation != null && !loadOperation.isDone)
                    SceneRuntimeService.ReleaseSceneActivation(loadOperation);

                return false;
            }
        }

        private async Awaitable<bool> LoadGameplaySceneFromBootstrapHandoffAsync(string sceneName, CancellationToken ct)
        {
            AsyncOperation loadOperation = null;
            bool scenePublicationGateOpen = false;
            try
            {
                sceneName = ResolveBootstrapGameplaySceneName(sceneName);

                _sceneActivationStarted = false;
                _debugSceneActivationCompleted = false;
                _sceneActivationSceneHandle = ulong.MaxValue;
                SetSceneActivationStep($"Step 0: Loading {sceneName}");

                GlobalRegistry.BeginSceneRuntimePublicationGate();
                scenePublicationGateOpen = true;

                string sceneLoadPath = ResolveSceneLoadPath(sceneName);
                BeginBootstrapGameplayHandoffSceneLoad(sceneLoadPath);
                loadOperation = LoadProductionSceneAsync(sceneLoadPath, LoadSceneMode.Single);
                if (loadOperation == null)
                    return false;

                loadOperation.allowSceneActivation = true;
                int waitFrames = 0;
                long waitStartTimestamp = Stopwatch.GetTimestamp();
                double nextStallLogSeconds = BootstrapGameplayHandoffStallLogIntervalSeconds;
                while (!loadOperation.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, nextStallLogSeconds, out double elapsedSeconds))
                    {
                        LogBootstrapGameplayHandoffSceneLoadStall(loadOperation.progress, waitFrames, elapsedSeconds, sceneLoadPath);
                        nextStallLogSeconds = elapsedSeconds + BootstrapGameplayHandoffStallLogIntervalSeconds;
                    }

                    waitFrames++;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                Scene activeGameplayScene = SceneManager.GetActiveScene();
                SetSceneActivationStep($"Step 0.5: Loaded {activeGameplayScene.name}");
                if (!IsExpectedScenePath(activeGameplayScene, sceneLoadPath))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(
                        "[GameBootstrapper] Scene route resolved to unexpected active scene. expectedPath=" +
                        sceneLoadPath +
                        " activePath=" +
                        activeGameplayScene.path +
                        " activeName=" +
                        activeGameplayScene.name);
#endif
                    return false;
                }

                if (!TryValidateSceneRootBudget(activeGameplayScene, "bootstrap-gameplay-postactivation"))
                    return false;

                if (IsOrbitScene(activeGameplayScene))
                    return CompleteIntroSceneActivation(activeGameplayScene);

                _sceneActivationRequested = true;
                BootstrapState.PublishBootstrapPresence(true);
                return await ExecuteSceneActivationAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                EndBootstrapGameplayHandoffSceneLoad();
                if (scenePublicationGateOpen && !TryDeferScenePublicationGateToSceneLoad(loadOperation))
                    GlobalRegistry.EndSceneRuntimePublicationGate();
            }
        }

        private static bool TryResolveBootstrapGameplayHandoffScene(out string sceneName)
        {
            sceneName = null;
            if (!GameStartContextHolder.TryConsumePendingTargetSceneName(out string pendingSceneName))
                return false;

            sceneName = ResolveBootstrapGameplaySceneName(pendingSceneName);
            return true;
        }

        private bool CompleteIntroSceneActivation(Scene scene)
        {
            SetSceneActivationStep($"Intro scene ready: {scene.name}");
            _sceneActivationRequested = false;
            _debugSceneActivationCompleted = true;
            BootstrapState.PublishGameReady(false);
            BootstrapState.PublishBootstrapPresence(false);
            return true;
        }

        private static string ResolveSceneLoadPath(string sceneName)
        {
            sceneName = NormalizeSceneLoadName(sceneName);
            if (string.Equals(sceneName, BootstrapSceneName, StringComparison.Ordinal))
                return BootstrapScenePath;
            if (string.Equals(sceneName, MainMenuSceneName, StringComparison.Ordinal))
                return MainMenuScenePath;
            if (string.Equals(sceneName, DefaultGameplaySceneName, StringComparison.Ordinal))
                return DefaultGameplayScenePath;
            if (string.Equals(sceneName, OrbitSceneName, StringComparison.Ordinal))
                return OrbitScenePath;
            return sceneName;
        }

        private static string ResolveBootstrapGameplaySceneName(string sceneName)
        {
            sceneName = NormalizeSceneLoadName(sceneName);
            if (sceneName.Length == 0 ||
                string.Equals(sceneName, BootstrapSceneName, StringComparison.Ordinal) ||
                string.Equals(sceneName, MainMenuSceneName, StringComparison.Ordinal))
            {
                return DefaultGameplaySceneName;
            }

            return sceneName;
        }

        private static string NormalizeSceneLoadName(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName.Trim();
        }

        private static AsyncOperation LoadProductionSceneAsync(string scenePath, LoadSceneMode mode)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[GameBootstrapper] Refusing to load a production scene from an empty scene path.");
#endif
                return null;
            }

            scenePath = scenePath.Trim();
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            return buildIndex >= 0
                ? SceneManager.LoadSceneAsync(buildIndex, mode)
                : SceneManager.LoadSceneAsync(scenePath, mode);
        }

        private static void BeginBootstrapGameplayHandoffSceneLoad(string expectedScenePath)
        {
            _bootstrapGameplayHandoffExpectedScenePath = expectedScenePath;
            _bootstrapGameplayHandoffOwnsSceneLoad = true;
        }

        private static void EndBootstrapGameplayHandoffSceneLoad()
        {
            _bootstrapGameplayHandoffOwnsSceneLoad = false;
            _bootstrapGameplayHandoffExpectedScenePath = null;
        }

        /// <summary>
        /// Transfers an open scene-runtime publication gate from the awaiting bootstrap frame to the scene load that
        /// is still running. Unity activates the scene, and therefore runs every scene-owned runtime service's
        /// registration, on the <see cref="AsyncOperation"/>'s clock. A gate whose lifetime ends with the awaiting
        /// stack frame cannot cover that window, so the frame hands the gate over instead of closing it.
        /// </summary>
        /// <param name="loadOperation">Scene load operation that still owns the pending activation.</param>
        /// <returns>True when the gate was handed to the operation and the caller must not close it inline.</returns>
        private static bool TryDeferScenePublicationGateToSceneLoad(AsyncOperation loadOperation)
        {
            if (loadOperation == null || loadOperation.isDone)
                return false;

            Interlocked.Increment(ref _bootstrapHandoffDeferredPublicationGateCount);
            loadOperation.completed += _bootstrapHandoffSceneLoadCompletedCallback;
            return true;
        }

        /// <summary>
        /// Closes exactly one publication gate that was handed to a scene load, after Unity reports the load done.
        /// </summary>
        /// <param name="loadOperation">Completed scene load operation that owned the deferred gate.</param>
        private static void ReleaseDeferredScenePublicationGate(AsyncOperation loadOperation)
        {
            if (loadOperation != null)
                loadOperation.completed -= _bootstrapHandoffSceneLoadCompletedCallback;

            if (Interlocked.Decrement(ref _bootstrapHandoffDeferredPublicationGateCount) < 0)
            {
                Interlocked.Exchange(ref _bootstrapHandoffDeferredPublicationGateCount, 0);
                return;
            }

            GlobalRegistry.EndSceneRuntimePublicationGate();
        }

        private static bool IsExpectedScenePath(Scene scene, string expectedPath)
        {
            if (string.IsNullOrWhiteSpace(expectedPath) || !expectedPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                return true;

            return string.Equals(scene.path, expectedPath, StringComparison.OrdinalIgnoreCase);
        }

        private static void LogBootstrapSceneLoadWatchdog(string stageName, float progress, int waitFrames, double elapsedSeconds)
        {
            LogBootstrapSceneLoadWatchdog(stageName, progress, waitFrames, elapsedSeconds, MainMenuSceneName);
        }

        private static void LogBootstrapSceneLoadWatchdog(string stageName, float progress, int waitFrames, double elapsedSeconds, string targetSceneName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Scene load watchdog tripped during {stageName}. progress={progress:0.000} frames={waitFrames} elapsed={elapsedSeconds:0.000}s target={targetSceneName}.");
#endif
        }

        /// <summary>
        /// Reports a slow gameplay handoff scene load without abandoning it. The load keeps running; only the
        /// diagnostic is periodic.
        /// </summary>
        private static void LogBootstrapGameplayHandoffSceneLoadStall(float progress, int waitFrames, double elapsedSeconds, string targetScenePath)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[GameBootstrapper] Bootstrap gameplay handoff scene load still running. progress={progress:0.000} frames={waitFrames} elapsed={elapsedSeconds:0.000}s target={targetScenePath}. Waiting on the AsyncOperation, not a wall clock.");
#endif
        }

        private async Awaitable<bool> InitializeCoreLayerAsync(CancellationToken ct)
        {
            EnsureCrashTelemetryBufferRegistered();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureRuntimeWatchdogRegistered();
            EnsureGCMonitorRegistered();
            EnsureRuntimePerformanceProfilerRegistered();
#endif
            ThreadSafeCommandQueue.Initialize();
            EnsurePrefabRegistry();
            EnsurePersistentWorldRegistry();
            return await InitializeBootstrapLayerNodesAsync(BootstrapPhase.CoreServices, ct);
        }

        private async Awaitable<bool> InitializeEnvironmentLayerAsync(CancellationToken ct)
        {
            return await InitializeBootstrapLayerNodesAsync(BootstrapPhase.Environment, ct);
        }

        private async Awaitable<bool> InitializePlayerLayerAsync(CancellationToken ct)
        {
            if (!InputManager.TryValidateRuntimeConfiguration(out string inputConfigurationError))
            {
                BootstrapBiosErrorOverlay.Show(inputConfigurationError);
                return false;
            }

            InputManager inputManager = ResolveRegisteredNativeInputManager();
            if (inputManager == null)
                inputManager = ResolveBootstrapInputManager(gameObject.scene);
            if (inputManager == null)
            {
                GameObject inputRoot = new GameObject("[InputManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned native input owner - owner: GameBootstrapper
                inputManager = inputRoot.AddComponent<InputManager>();
            }

            if (inputManager == null)
            {
                BootstrapBiosErrorOverlay.Show(
                    "BIOS ERROR 0xINPUT\nEXPECTED: Runtime InputManager instance\nDETECTED: explicit bootstrap input owner resolution failed\nACTION: Repair the bootstrap input owner before boot.");
                return false;
            }

            if (!inputManager.TryValidateRuntimeActions(out string inputActionsError))
            {
                BootstrapBiosErrorOverlay.Show(inputActionsError);
                return false;
            }

            _bootstrapInputManager = inputManager;

            if (!ReferenceEquals(GlobalRegistry.NativeInputRuntime, inputManager))
                GlobalRegistry.RegisterNativeInputManagerRuntime(inputManager);

            PersistRuntimeService(inputManager);
            UserOptionsPersistence userOptionsPersistence = GlobalRegistry.UserOptions;
            if (userOptionsPersistence == null)
            {
                GameObject userOptionsRoot = new GameObject("[UserOptionsPersistence]"); // COLD ALLOC: GameObject[1] - bootstrap-owned user options persistence root - owner: GameBootstrapper
                userOptionsPersistence = userOptionsRoot.AddComponent<UserOptionsPersistence>();
            }

            PersistRuntimeService(userOptionsPersistence);
            if (userOptionsPersistence != null && !ReferenceEquals(GlobalRegistry.UserOptions, userOptionsPersistence))
                GlobalRegistry.RegisterUserOptionsRuntime(userOptionsPersistence);

            RebindingManager rebindingManager = null;
            if (!RebindingManager.TryResolveActiveRuntime(ref rebindingManager))
            {
                GameObject rebindingRoot = new GameObject("[RebindingManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned input binding service - owner: GameBootstrapper
                rebindingManager = rebindingRoot.AddComponent<RebindingManager>();
            }

            rebindingManager.BindNativeInputManager(inputManager);
            PersistRuntimeService(rebindingManager);

            AccessibilitySettings accessibilitySettings = null;
            if (!AccessibilitySettings.TryResolveActiveRuntime(ref accessibilitySettings))
            {
                GameObject accessibilityRoot = new GameObject("[AccessibilitySettings]"); // COLD ALLOC: GameObject[1] - bootstrap-owned accessibility cbuffer bridge - owner: GameBootstrapper
                accessibilitySettings = accessibilityRoot.AddComponent<AccessibilitySettings>();
            }

            PersistRuntimeService(accessibilitySettings);

            ContextualPhysicalIkRuntime contextualIkRuntime = ContextualPhysicalIkRuntime.EnsureRuntimeInstance();
            PersistRuntimeService(contextualIkRuntime);
            VRSomaticRuntimeBootstrap vrSomaticRuntime = VRSomaticRuntimeBootstrap.EnsureRegisteredByBootstrap();
            PersistRuntimeService(vrSomaticRuntime);
            if (!await InitializeBootstrapLayerNodesAsync(BootstrapPhase.Player, ct))
                return false;

            PlayerRuntimeContextService playerContextService = PlayerRuntimeContextService.EnsureRuntimeInstance();
            playerContextService.RefreshRuntimeContext();
            return true;
        }

        private static InputManager ResolveBootstrapInputManager(Scene scene)
        {
            if (_bootstrapInputManager != null)
                return _bootstrapInputManager;

            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            scene.GetRootGameObjects(_bootstrapSceneRootScratch);

            for (int i = 0; i < _bootstrapSceneRootScratch.Count; i++)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null)
                    continue;

                _bootstrapTransformScratch.Add(root.transform);
            }

            while (_bootstrapTransformScratch.Count > 0)
            {
                int lastIndex = _bootstrapTransformScratch.Count - 1;
                Transform current = _bootstrapTransformScratch[lastIndex];
                _bootstrapTransformScratch.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                if (current.TryGetComponent(out InputManager inputManager) &&
                    inputManager != null)
                {
                    _bootstrapSceneRootScratch.Clear();
                    _bootstrapTransformScratch.Clear();
                    return inputManager;
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    _bootstrapTransformScratch.Add(current.GetChild(i));
            }

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            return null;
        }

        private async Awaitable<bool> InitializeUILayerAsync(CancellationToken ct)
        {
            EnsureSettingsRuntimeRegistered();
            UIStateStore.EnsureInitialized();
#if UNITY_ADDRESSABLES_EXIST
            if (!await LoadAddressableDependencyChainAsync(ct))
                return false;

            if (!await LoadAddressableUIPrefabsAsync(ct))
                return false;
#endif
            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            return true;
        }

        private static SettingsManager EnsureSettingsRuntimeRegistered()
        {
            // Resolve-or-create is owned by SettingsManager.EnsureRuntimeInstance
            // (static slot + GlobalRegistry.Settings + scene scan + player-build AddComponent).
            // Bootstrap no longer duplicates the construction path.
            SettingsManager settingsManager = SettingsManager.EnsureRuntimeInstance();
            if (settingsManager == null)
                return null;

            PersistRuntimeService(settingsManager);
            if (!ReferenceEquals(GlobalRegistry.Settings, settingsManager))
                GlobalRegistry.RegisterSettingsRuntime(settingsManager);

            settingsManager.RefreshPersistenceFromRegistry();
            return settingsManager;
        }


        private async Awaitable<bool> LoadAddressableUIPrefabsAsync(CancellationToken ct)
        {
#if UNITY_ADDRESSABLES_EXIST
            int prefabCount = uiAddressablePrefabs != null ? uiAddressablePrefabs.Length : 0;
            if (prefabCount <= 0)
                return true;

            if (_uiPrefabInstanceHandles == null || _uiPrefabInstanceHandles.Length != prefabCount)
            {
                ReleaseAddressableUIPrefabs();
                _uiPrefabInstanceHandles = new AsyncOperationHandle<GameObject>[prefabCount]; // COLD ALLOC: AsyncOperationHandle<GameObject>[uiAddressablePrefabs.Length] - UI bootstrap readiness handles - owner: GameBootstrapper
            }

            for (int i = 0; i < prefabCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                AssetReferenceGameObject prefabReference = uiAddressablePrefabs[i];
                if (prefabReference == null || !prefabReference.RuntimeKeyIsValid())
                    continue;

                AsyncOperationHandle<GameObject> existingHandle = _uiPrefabInstanceHandles[i];
                if (existingHandle.IsValid())
                    continue;

                _uiPrefabInstanceHandles[i] = prefabReference.InstantiateAsync(transform);
            }

            for (int i = 0; i < prefabCount; i++)
            {
                AsyncOperationHandle<GameObject> handle = _uiPrefabInstanceHandles[i];
                if (!handle.IsValid())
                    continue;

                long waitStartTimestamp = Stopwatch.GetTimestamp();
                while (!handle.IsDone)
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, BootstrapRequiredAddressableGateTimeoutSeconds, out double elapsedSeconds))
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogError(
                            "[GameBootstrapper] UI addressable prefab timed out during bootstrap UI gate. index=" +
                            i +
                            " elapsed=" +
                            elapsedSeconds.ToString("0.000"));
#endif
                        if (handle.IsValid())
                            Addressables.ReleaseInstance(handle);
                        _uiPrefabInstanceHandles[i] = default;
                        return false;
                    }

                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                    handle = _uiPrefabInstanceHandles[i];
                }

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[GameBootstrapper] UI addressable prefab failed during bootstrap UI gate.");
#endif
                    return false;
                }
            }

            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            return true;
#else
            ct.ThrowIfCancellationRequested();
            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            return true;
#endif
        }

#if UNITY_ADDRESSABLES_EXIST
        private static async Awaitable<bool> PreWarmTierAddressableTextureGroupAsync(CancellationToken ct)
        {
            string label = ResolveTierAddressableTextureLabel();
#if UNITY_EDITOR
            if (ShouldSkipEditorTierAddressablePrewarm(out string skipReason))
            {
                RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
                RuntimeDiagnosticsTrace.WriteEvent("bootstrap.addressables.tier_prewarm.skipped_editor", skipReason);
                return true;
            }

            if (!HasEditorAddressablesRuntimeSettingsFile())
            {
                RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
                RuntimeDiagnosticsTrace.WriteEvent("bootstrap.addressables.tier_prewarm.skipped_missing_runtime_settings", label);
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return true;
            }
#endif
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(label, false);
            long waitStartTimestamp = Stopwatch.GetTimestamp();
            while (!handle.IsDone)
            {
                ct.ThrowIfCancellationRequested();
                if (HasWatchdogElapsed(waitStartTimestamp, BootstrapAddressablePrewarmSoftTimeoutSeconds, out double elapsedSeconds))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[GameBootstrapper] Tier Addressables prewarm timed out; continuing bootstrap. label=" + label + " elapsed=" + elapsedSeconds.ToString("0.000"));
#endif
                    TryReleaseBootstrapDependencyHandle(handle);
                    return true;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            }

            bool succeeded = handle.Status == AsyncOperationStatus.Succeeded;
            if (succeeded)
                PublishAddressableDependencyGroupLoaded(-1, label, handle);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
                Debug.LogWarning("[GameBootstrapper] Tier Addressables prewarm failed; continuing bootstrap. label=" + label);
#endif
            TryReleaseBootstrapDependencyHandle(handle);
            return true;
        }

#if UNITY_EDITOR
        private static bool ShouldSkipEditorTierAddressablePrewarm(out string reason)
        {
            reason = Application.isPlaying
                ? "editor_playmode_optional_texture_prewarm"
                : "editor_optional_texture_prewarm";
            return true;
        }

        private static bool HasEditorAddressablesRuntimeSettingsFile()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string settingsPath = Path.Combine(projectRoot, "Library", "com.unity.addressables", "aa", "Windows", "settings.json");
            return File.Exists(settingsPath);
        }

#endif

        private static string ResolveTierAddressableTextureLabel()
        {
            return GlobalRegistry.MathPrecision == MathPrecisionLevel.High
                ? TierHighAddressableLabel
                : TierLowAddressableLabel;
        }

        private async Awaitable<bool> LoadAddressableDependencyChainAsync(CancellationToken ct)
        {
            int groupCount = addressableDependencyGroups != null ? addressableDependencyGroups.Length : 0;
            for (int i = 0; i < groupCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                AssetLabelReference group = addressableDependencyGroups[i];
                if (group == null || string.IsNullOrEmpty(group.labelString))
                    continue;

                AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(group.labelString, false);
                long waitStartTimestamp = Stopwatch.GetTimestamp();
                while (!handle.IsDone)
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, BootstrapRequiredAddressableGateTimeoutSeconds, out double elapsedSeconds))
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogError(
                            "[GameBootstrapper] Addressables dependency group timed out during bootstrap. label=" +
                            group.labelString +
                            " elapsed=" +
                            elapsedSeconds.ToString("0.000"));
#endif
                        TryReleaseBootstrapDependencyHandle(handle);
                        return false;
                    }

                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    TryReleaseBootstrapDependencyHandle(handle);
                    return false;
                }

                PublishAddressableDependencyGroupLoaded(i, group.labelString, handle);
                if (!TryReleaseBootstrapDependencyHandle(handle))
                    return false;
            }

            return true;
        }

        private static bool TryReleaseBootstrapDependencyHandle(AsyncOperationHandle handle)
        {
            if (!handle.IsValid())
                return true;

            AssetLifecycleGovernor lifecycleGovernor = GlobalRegistry.AssetLifecycle;
            if (lifecycleGovernor != null)
                return lifecycleGovernor.TryReleaseExternalAddressableFault(handle);

            Addressables.Release(handle);
            return true;
        }

        private static void PublishAddressableDependencyGroupLoaded(
            int dependencyIndex,
            string label,
            AsyncOperationHandle handle)
        {
            uint groupHash = ComputeAddressableGroupHash(label);
            AssetLifecycleGovernor lifecycleGovernor = GlobalRegistry.AssetLifecycle;
            if (lifecycleGovernor != null)
                lifecycleGovernor.MarkAddressableDependencyGroupLoaded(groupHash, dependencyIndex, handle);

            AssetLoadDispatcher dispatcher = GlobalRegistry.AssetLoadDispatcher;
            if (dispatcher != null)
                dispatcher.MarkAddressableDependencyGroupReady(groupHash, dependencyIndex, handle);
        }

        private static uint ComputeAddressableGroupHash(string label)
        {
            if (string.IsNullOrEmpty(label))
                return 0u;

            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= 16777619u;
                }

                return hash != 0u ? hash : 1u;
            }
        }
#endif

        private void ReleaseAddressableUIPrefabs()
        {
#if UNITY_ADDRESSABLES_EXIST
            if (_uiPrefabInstanceHandles == null)
                return;

            for (int i = 0; i < _uiPrefabInstanceHandles.Length; i++)
            {
                AsyncOperationHandle<GameObject> handle = _uiPrefabInstanceHandles[i];
                if (handle.IsValid())
                    Addressables.ReleaseInstance(handle);

                _uiPrefabInstanceHandles[i] = default;
            }
#endif
        }

        private async Awaitable<bool> WarmConfiguredShaderVariantCollectionsAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return false;

            if (_headlessBootMode || Application.isBatchMode)
            {
                RecordBootstrapShaderWarmupTelemetry(
                    ShaderWarmupTelemetryPhase.Complete,
                    ShaderWarmupTelemetryFlags.Headless,
                    ShaderWarmupErrorCode.None,
                    _ShaderWarmupCompleteHash,
                    -1,
                    -1,
                    0,
                    0,
                    0L);
                return true;
            }

            int collectionCount = shaderVariantCollections != null ? shaderVariantCollections.Length : 0;
            bool telemetryReady = EnsureBootstrapShaderWarmupTelemetryRing();
            if (ShouldDeferBlockingBootstrapShaderWarmup())
            {
                if (telemetryReady)
                {
                    RecordBootstrapShaderWarmupTelemetry(
                        ShaderWarmupTelemetryPhase.Complete,
                        ShaderWarmupTelemetryFlags.Deferred,
                        ShaderWarmupErrorCode.None,
                        _ShaderWarmupCompleteHash,
                        -1,
                        -1,
                        collectionCount,
                        0,
                        0L);
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
                return !ct.IsCancellationRequested;
            }

            if (!telemetryReady)
            {
                return FailBootstrapShaderWarmup(
                    ShaderWarmupErrorCode.MissingTelemetryRing,
                    _ShaderWarmupFailureHash,
                    -1,
                    -1,
                    0,
                    0L);
            }

            if (collectionCount <= 0)
            {
                return FailBootstrapShaderWarmup(
                    ShaderWarmupErrorCode.MissingShaderCollections,
                    _ShaderWarmupFailureHash,
                    -1,
                    -1,
                    0,
                    0L);
            }

            int invalidCollectionIndex = FindInvalidShaderVariantCollectionIndex(shaderVariantCollections);
            if (invalidCollectionIndex >= 0)
            {
                return FailBootstrapShaderWarmup(
                    ShaderWarmupErrorCode.MissingShaderCollections,
                    _ShaderWarmupFailureHash,
                    invalidCollectionIndex,
                    -1,
                    0,
                    0L);
            }

            int emptyCollectionIndex = FindEmptyShaderVariantCollectionIndex(shaderVariantCollections);
            if (emptyCollectionIndex >= 0)
            {
                return FailBootstrapShaderWarmup(
                    ShaderWarmupErrorCode.InvalidCollectionSet,
                    _ShaderWarmupFailureHash,
                    emptyCollectionIndex,
                    -1,
                    0,
                    0L);
            }

            int shaderCount = shaderWarmupShaders != null ? shaderWarmupShaders.Length : 0;
            if (shaderCount <= 0)
            {
                return FailBootstrapShaderWarmup(
                    ShaderWarmupErrorCode.MissingShaderManifest,
                    _ShaderWarmupFailureHash,
                    -1,
                    -1,
                    0,
                    0L);
            }

            int validShaderCount = CountValidShaderWarmupShaders(shaderWarmupShaders);
            if (validShaderCount != shaderCount)
            {
                int invalidShaderIndex = FindInvalidShaderWarmupShaderIndex(shaderWarmupShaders);
                return FailBootstrapShaderWarmup(
                    ShaderWarmupErrorCode.MissingShaderManifest,
                    _ShaderWarmupFailureHash,
                    -1,
                    invalidShaderIndex,
                    0,
                    0L);
            }

            long warmupStart = Stopwatch.GetTimestamp();
            int validCollectionCount = 0;
            int warmupAttemptCount = 0;
            int warmupBatchCounter = 0;
            int shaderBatchSize = ResolveShaderWarmupBatchSize();
            int shaderWarmupSetupCount = _shaderWarmupSetups.Length;
            int graphicsStateCollectionCount = shaderGraphicsStateCollectionPaths != null ? shaderGraphicsStateCollectionPaths.Length : 0;
            if (RequiresGraphicsStateCollectionsForCurrentApi() && graphicsStateCollectionCount <= 0)
            {
                RecordBootstrapShaderWarmupTelemetry(
                    ShaderWarmupTelemetryPhase.Failure,
                    ShaderWarmupTelemetryFlags.GraphicsStateCollection | ShaderWarmupTelemetryFlags.MissingCollections,
                    ShaderWarmupErrorCode.MissingGraphicsStateCollections,
                    _ShaderWarmupFailureHash,
                    -1,
                    -1,
                    0,
                    0,
                    warmupStart);
                RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
                RuntimeDiagnosticsTrace.WriteEvent(
                    "bootstrap.shader.graphics_state.missing_continue",
                    SystemInfo.graphicsDeviceType.ToString());
            }

            int shaderWarmupTimeoutMilliseconds = ResolveShaderWarmupTimeoutMilliseconds(
                collectionCount,
                validShaderCount,
                graphicsStateCollectionCount,
                shaderWarmupSetupCount);
            RecordBootstrapShaderWarmupTelemetry(
                ShaderWarmupTelemetryPhase.Start,
                ShaderWarmupTelemetryFlags.None,
                ShaderWarmupErrorCode.None,
                _ShaderWarmupStartHash,
                -1,
                -1,
                collectionCount,
                validShaderCount,
                warmupStart);

            if (!await WarmConfiguredGraphicsStateCollectionsAsync(ct, warmupStart, shaderWarmupTimeoutMilliseconds))
                return false;
            BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);

            for (int i = 0; i < collectionCount; i++)
            {
                if (ct.IsCancellationRequested)
                    return false;

                ShaderVariantCollection collection = shaderVariantCollections[i];
                if (collection == null)
                {
                    return FailBootstrapShaderWarmup(
                        ShaderWarmupErrorCode.MissingShaderCollections,
                        _ShaderWarmupFailureHash,
                        i,
                        -1,
                        0,
                        warmupStart);
                }

                int variantCount = collection.variantCount;
                validCollectionCount++;
                RecordBootstrapShaderWarmupTelemetry(
                    ShaderWarmupTelemetryPhase.CollectionStart,
                    ShaderWarmupTelemetryFlags.None,
                    ShaderWarmupErrorCode.None,
                    _ShaderWarmupCollectionHash,
                    i,
                    -1,
                    variantCount,
                    warmupAttemptCount,
                    warmupStart);

                for (int shaderIndex = 0; shaderIndex < shaderCount; shaderIndex++)
                {
                    if (ct.IsCancellationRequested)
                        return false;

                    Shader shader = shaderWarmupShaders[shaderIndex];
                    if (shader == null)
                    {
                        return FailBootstrapShaderWarmup(
                            ShaderWarmupErrorCode.MissingShaderManifest,
                            _ShaderWarmupFailureHash,
                            i,
                            shaderIndex,
                            variantCount,
                            warmupStart);
                    }

                    for (int setupIndex = 0; setupIndex < shaderWarmupSetupCount; setupIndex++)
                    {
                        if (ct.IsCancellationRequested)
                            return false;

                        long shaderStart = Stopwatch.GetTimestamp();
                        if (!TryWarmShaderVariant(collection, shader, _shaderWarmupSetups[setupIndex]))
                        {
                            return FailBootstrapShaderWarmup(
                                ShaderWarmupErrorCode.WarmupApiFailure,
                                _ShaderWarmupFailureHash,
                                i,
                                shaderIndex,
                                variantCount,
                                warmupStart);
                        }

                        warmupAttemptCount++;
                        warmupBatchCounter++;
                        BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
                        RecordBootstrapShaderWarmupTelemetry(
                            ShaderWarmupTelemetryPhase.ShaderComplete,
                            ShaderWarmupTelemetryFlags.None,
                            ShaderWarmupErrorCode.None,
                            _ShaderWarmupShaderHash,
                            i,
                            shaderIndex,
                            variantCount,
                            warmupAttemptCount,
                            shaderStart);

                        if (HasShaderWarmupTimedOut(warmupStart, shaderWarmupTimeoutMilliseconds))
                        {
                            return FailBootstrapShaderWarmup(
                                ShaderWarmupErrorCode.Timeout,
                                _ShaderWarmupTimeoutHash,
                                i,
                                shaderIndex,
                                variantCount,
                                warmupStart);
                        }

                        if (warmupBatchCounter >= shaderBatchSize)
                        {
                            warmupBatchCounter = 0;
                            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
                            if (ct.IsCancellationRequested)
                                return false;
                        }
                    }
                }

                RecordBootstrapShaderWarmupTelemetry(
                    ShaderWarmupTelemetryPhase.CollectionComplete,
                    ShaderWarmupTelemetryFlags.None,
                    ShaderWarmupErrorCode.None,
                    _ShaderWarmupCollectionHash,
                    i,
                    -1,
                    variantCount,
                    warmupAttemptCount,
                    warmupStart);

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
                BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
                if (ct.IsCancellationRequested)
                    return false;
            }

            if (validCollectionCount <= 0)
            {
                return FailBootstrapShaderWarmup(
                    ShaderWarmupErrorCode.InvalidCollectionSet,
                    _ShaderWarmupFailureHash,
                    -1,
                    -1,
                    collectionCount,
                    warmupStart);
            }

            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
            BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
            if (ct.IsCancellationRequested)
                return false;

            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
            BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
            if (ct.IsCancellationRequested)
                return false;
            RecordBootstrapShaderWarmupTelemetry(
                ShaderWarmupTelemetryPhase.Complete,
                ShaderWarmupTelemetryFlags.None,
                ShaderWarmupErrorCode.None,
                _ShaderWarmupCompleteHash,
                -1,
                -1,
                collectionCount,
                warmupAttemptCount,
                warmupStart);
            return true;
        }

        private static bool ShouldDeferBlockingBootstrapShaderWarmup()
        {
#if UNITY_EDITOR
            return !Application.isBatchMode;
#else
            return false;
#endif
        }

        private bool EnsureBootstrapShaderWarmupTelemetryRing()
        {
            if (_shaderWarmupTelemetryReady)
                return true;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            _shaderWarmupTelemetryHandle = vault.EnsureGenerationHandle<BootstrapTelemetryEntry>(
                BootstrapShaderWarmupTelemetryRingBufferId,
                BootstrapShaderWarmupTelemetryCapacity,
                SystemID.Bootstrap,
                NativeArrayOptions.ClearMemory);

            if (_shaderWarmupTelemetryHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in _shaderWarmupTelemetryHandle, SystemID.Bootstrap, out NativeArray<BootstrapTelemetryEntry> ring))
            {
                return false;
            }

            try
            {
                _shaderWarmupTelemetryReady = ring.IsCreated && ring.Length >= BootstrapShaderWarmupTelemetryCapacity;
                return _shaderWarmupTelemetryReady;
            }
            finally
            {
                vault.ReleaseWriteLock(in _shaderWarmupTelemetryHandle, SystemID.Bootstrap);
            }
        }

        private void RecordBootstrapShaderWarmupTelemetry(
            ShaderWarmupTelemetryPhase phase,
            ShaderWarmupTelemetryFlags flags,
            ShaderWarmupErrorCode errorCode,
            uint eventHash,
            int collectionIndex,
            int shaderIndex,
            int variantCount,
            int warmedVariantCount,
            long startTimestamp)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                _shaderWarmupTelemetryHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in _shaderWarmupTelemetryHandle, SystemID.Bootstrap, out NativeArray<BootstrapTelemetryEntry> ring))
            {
                return;
            }

            try
            {
                if (!ring.IsCreated || ring.Length <= 0)
                    return;

                int writeIndex = _shaderWarmupTelemetryCursor;
                if ((uint)writeIndex >= (uint)ring.Length)
                    writeIndex = 0;

                long now = Stopwatch.GetTimestamp();
                ring[writeIndex] = new BootstrapTelemetryEntry
                {
                    TimestampTicks = DateTime.UtcNow.Ticks,
                    DurationMicroseconds = startTimestamp > 0L
                        ? (long)((now - startTimestamp) * 1000000.0d / Stopwatch.Frequency)
                        : 0L,
                    ContextHash = _GameBootstrapperContextHash,
                    FrameIndex = unchecked((uint)Time.frameCount),
                    EventHash = eventHash,
                    CollectionIndex = collectionIndex >= 0 ? (uint)collectionIndex : uint.MaxValue,
                    ShaderIndex = shaderIndex >= 0 ? (uint)shaderIndex : uint.MaxValue,
                    VariantCount = variantCount > 0 ? (uint)variantCount : 0u,
                    WarmedVariantCount = warmedVariantCount > 0 ? (uint)warmedVariantCount : 0u,
                    QualityWeight01 = ResolveBootstrapQualityWeight01(),
                    Phase = (ushort)phase,
                    Flags = (ushort)flags,
                    ErrorCode = (ushort)errorCode,
                    Reserved = 0
                };

                writeIndex++;
                _shaderWarmupTelemetryCursor = writeIndex >= ring.Length ? 0 : writeIndex;
            }
            finally
            {
                vault.ReleaseWriteLock(in _shaderWarmupTelemetryHandle, SystemID.Bootstrap);
            }
        }

        private bool FailBootstrapShaderWarmup(
            ShaderWarmupErrorCode errorCode,
            uint eventHash,
            int collectionIndex,
            int shaderIndex,
            int variantCount,
            long startTimestamp)
        {
            ShaderWarmupTelemetryFlags flags = ShaderWarmupTelemetryFlags.Failure;
            ShaderWarmupTelemetryPhase phase = ShaderWarmupTelemetryPhase.Failure;
            if (errorCode == ShaderWarmupErrorCode.Timeout)
            {
                flags |= ShaderWarmupTelemetryFlags.Timeout;
                phase = ShaderWarmupTelemetryPhase.Timeout;
            }
            else if (errorCode == ShaderWarmupErrorCode.MissingShaderManifest)
            {
                flags |= ShaderWarmupTelemetryFlags.MissingManifest;
            }
            else if (errorCode == ShaderWarmupErrorCode.MissingShaderCollections)
            {
                flags |= ShaderWarmupTelemetryFlags.MissingCollections;
            }
            else if (errorCode == ShaderWarmupErrorCode.GraphicsStateWarmupFailure)
            {
                flags |= ShaderWarmupTelemetryFlags.GraphicsStateCollection;
            }
            else if (errorCode == ShaderWarmupErrorCode.GraphicsStateCompatibilityFailure)
            {
                flags |= ShaderWarmupTelemetryFlags.GraphicsStateCollection | ShaderWarmupTelemetryFlags.GraphicsStateIncompatible;
            }
            else if (errorCode == ShaderWarmupErrorCode.MissingGraphicsStateCollections)
            {
                flags |= ShaderWarmupTelemetryFlags.GraphicsStateCollection | ShaderWarmupTelemetryFlags.MissingCollections;
            }

            RecordBootstrapShaderWarmupTelemetry(
                phase,
                flags,
                errorCode,
                eventHash,
                collectionIndex,
                shaderIndex,
                variantCount,
                0,
                startTimestamp);
            QueueBootstrapShaderWarmupTelemetryDump();
            RaiseBootstrapFailedEvent(ShaderWarmupFailureReason);
            BootstrapBiosErrorOverlay.Show(ShaderWarmupFailureOverlayMessage);
            return false;
        }

        private bool HasShaderWarmupTimedOut(long startTimestamp, int timeoutMilliseconds)
        {
            if (startTimestamp <= 0L)
                return false;

            long elapsedMilliseconds = (long)((Stopwatch.GetTimestamp() - startTimestamp) * 1000.0d / Stopwatch.Frequency);
            return elapsedMilliseconds > math.max(timeoutMilliseconds, ShaderWarmupBaseTimeoutMilliseconds);
        }

        private static int ResolveShaderWarmupBatchSize()
        {
            float quality = ResolveBootstrapQualityWeight01();
            if (GlobalRegistry.MathPrecision == MathPrecisionLevel.Low)
                quality = math.min(quality, 0.12f);

            int scaledBatchSize = 1 + (int)math.floor(math.saturate(quality) * (WarmupBatchSize - 1));
            return math.clamp(scaledBatchSize, 1, WarmupBatchSize);
        }

        private static int ResolveShaderWarmupTimeoutMilliseconds(
            int collectionCount,
            int shaderCount,
            int graphicsStateCollectionCount,
            int setupCount)
        {
            int safeCollectionCount = math.max(collectionCount, 0);
            int safeShaderCount = math.max(shaderCount, 0);
            int safeGraphicsStateCollectionCount = math.max(graphicsStateCollectionCount, 0);
            int safeSetupCount = math.max(setupCount, 1);
            float quality = ResolveBootstrapQualityWeight01();
            if (GlobalRegistry.MathPrecision == MathPrecisionLevel.Low)
                quality = math.min(quality, 0.12f);

            int shaderBatchSize = ResolveShaderWarmupBatchSize();
            long shaderAttemptCount = (long)safeCollectionCount * safeShaderCount * safeSetupCount;
            long shaderAttemptBudgetMilliseconds =
                shaderAttemptCount * ShaderWarmupPerShaderAttemptTimeoutMilliseconds;
            long shaderFrameSlices = shaderBatchSize > 0
                ? (shaderAttemptCount + shaderBatchSize - 1L) / shaderBatchSize
                : shaderAttemptCount;
            int frameCadenceMilliseconds = (int)math.ceil(math.lerp(
                ShaderWarmupLowQualityFrameCadenceMilliseconds,
                ShaderWarmupHighQualityFrameCadenceMilliseconds,
                math.saturate(quality)));
            long yieldBudgetMilliseconds =
                (shaderFrameSlices + safeCollectionCount + 2L) * frameCadenceMilliseconds;
            long graphicsStateBudgetMilliseconds =
                (long)safeGraphicsStateCollectionCount * ShaderWarmupPerGraphicsStateCollectionTimeoutMilliseconds;
            int qualityPaddingMilliseconds = (int)math.ceil(
                (1.0f - math.saturate(quality)) * ShaderWarmupLowQualityTimeoutPaddingMilliseconds);
            long timeoutMilliseconds = ShaderWarmupBaseTimeoutMilliseconds +
                                       shaderAttemptBudgetMilliseconds +
                                       yieldBudgetMilliseconds +
                                       graphicsStateBudgetMilliseconds +
                                       qualityPaddingMilliseconds;
            if (timeoutMilliseconds <= ShaderWarmupBaseTimeoutMilliseconds)
                return ShaderWarmupBaseTimeoutMilliseconds;

            return timeoutMilliseconds >= ShaderWarmupMaxTimeoutMilliseconds
                ? ShaderWarmupMaxTimeoutMilliseconds
                : (int)timeoutMilliseconds;
        }

        private static int ResolveGraphicsStateWarmupBatchSize()
        {
            int shaderBatchSize = ResolveShaderWarmupBatchSize();
            return math.clamp(shaderBatchSize * 4, 4, 32);
        }

        private static bool RequiresGraphicsStateCollectionsForCurrentApi()
        {
            GraphicsDeviceType deviceType = SystemInfo.graphicsDeviceType;
            return deviceType == GraphicsDeviceType.Direct3D12 ||
                   deviceType == GraphicsDeviceType.Vulkan ||
                   deviceType == GraphicsDeviceType.Metal;
        }

        private async Awaitable<bool> WarmConfiguredGraphicsStateCollectionsAsync(
            CancellationToken ct,
            long warmupStart,
            int timeoutMilliseconds)
        {
            int collectionCount = shaderGraphicsStateCollectionPaths != null ? shaderGraphicsStateCollectionPaths.Length : 0;
            if (collectionCount <= 0)
                return true;

            int graphicsStateBatchSize = ResolveGraphicsStateWarmupBatchSize();
            for (int i = 0; i < collectionCount; i++)
            {
                if (ct.IsCancellationRequested)
                    return false;

                BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
                if (!TryLoadGraphicsStateCollection(shaderGraphicsStateCollectionPaths[i], out GraphicsStateCollection collection))
                {
                    return FailBootstrapShaderWarmup(
                        ShaderWarmupErrorCode.GraphicsStateWarmupFailure,
                        _ShaderWarmupFailureHash,
                        i,
                        -1,
                        0,
                        warmupStart);
                }

                if (!IsGraphicsStateCollectionCompatible(collection))
                {
                    UnityEngine.Object.Destroy(collection);
                    return FailBootstrapShaderWarmup(
                        ShaderWarmupErrorCode.GraphicsStateCompatibilityFailure,
                        _ShaderWarmupFailureHash,
                        i,
                        -1,
                        0,
                        warmupStart);
                }

                bool collectionWarmed;
                try
                {
                    collectionWarmed = await WarmLoadedGraphicsStateCollectionAsync(
                        collection,
                        i,
                        graphicsStateBatchSize,
                        ct,
                        warmupStart,
                        timeoutMilliseconds);
                }
                finally
                {
                    UnityEngine.Object.Destroy(collection);
                }

                if (!collectionWarmed)
                    return false;
            }

            return true;
        }

        private async Awaitable<bool> WarmLoadedGraphicsStateCollectionAsync(
            GraphicsStateCollection collection,
            int collectionIndex,
            int graphicsStateBatchSize,
            CancellationToken ct,
            long warmupStart,
            int timeoutMilliseconds)
        {
            int graphicsStateCount = ClampWarmupCountToInt(collection.totalGraphicsStateCount);
            if (graphicsStateCount <= 0 || collection.isWarmedUp)
                return true;

            RecordBootstrapShaderWarmupTelemetry(
                ShaderWarmupTelemetryPhase.GraphicsStateCollectionStart,
                ShaderWarmupTelemetryFlags.GraphicsStateCollection,
                ShaderWarmupErrorCode.None,
                _ShaderWarmupCollectionHash,
                collectionIndex,
                -1,
                graphicsStateCount,
                ClampWarmupCountToInt(collection.completedWarmupCount),
                warmupStart);

            while (!collection.isWarmedUp)
            {
                if (ct.IsCancellationRequested)
                    return false;

                BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
                if (HasShaderWarmupTimedOut(warmupStart, timeoutMilliseconds))
                {
                    return FailBootstrapShaderWarmup(
                        ShaderWarmupErrorCode.Timeout,
                        _ShaderWarmupTimeoutHash,
                        collectionIndex,
                        -1,
                        graphicsStateCount,
                        warmupStart);
                }

                long completedBefore = collection.completedWarmupCount;
                if (!TryScheduleGraphicsStateWarmup(collection, graphicsStateBatchSize, out JobHandle warmupHandle))
                {
                    return FailBootstrapShaderWarmup(
                        ShaderWarmupErrorCode.GraphicsStateWarmupFailure,
                        _ShaderWarmupFailureHash,
                        collectionIndex,
                        -1,
                        graphicsStateCount,
                        warmupStart);
                }

                if (!await WaitForShaderWarmupJobAsync(
                        warmupHandle,
                        ct,
                        warmupStart,
                        timeoutMilliseconds,
                        collectionIndex,
                        -1,
                        graphicsStateCount))
                    return false;

                long completedAfter = collection.completedWarmupCount;
                if (completedAfter <= completedBefore && !collection.isWarmedUp)
                {
                    return FailBootstrapShaderWarmup(
                        ShaderWarmupErrorCode.GraphicsStateWarmupFailure,
                        _ShaderWarmupFailureHash,
                        collectionIndex,
                        -1,
                        graphicsStateCount,
                        warmupStart);
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
                BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
            }

            RecordBootstrapShaderWarmupTelemetry(
                ShaderWarmupTelemetryPhase.GraphicsStateCollectionComplete,
                ShaderWarmupTelemetryFlags.GraphicsStateCollection,
                ShaderWarmupErrorCode.None,
                _ShaderWarmupCollectionHash,
                collectionIndex,
                -1,
                graphicsStateCount,
                ClampWarmupCountToInt(collection.completedWarmupCount),
                warmupStart);
            return true;
        }

        private static bool TryLoadGraphicsStateCollection(string configuredPath, out GraphicsStateCollection collection)
        {
            collection = null;
            if (string.IsNullOrWhiteSpace(configuredPath))
                return false;

            if (!TryResolveGraphicsStateCollectionPath(configuredPath, out string resolvedPath))
                return false;

            if (!File.Exists(resolvedPath))
                return false;

            try
            {
                collection = new GraphicsStateCollection(resolvedPath);
                if (collection.totalGraphicsStateCount > 0)
                    return true;

                UnityEngine.Object.Destroy(collection);
                collection = null;
                return false;
            }
            catch (ArgumentException)
            {
                if (collection != null)
                    UnityEngine.Object.Destroy(collection);
                collection = null;
                return false;
            }
            catch (InvalidOperationException)
            {
                if (collection != null)
                    UnityEngine.Object.Destroy(collection);
                collection = null;
                return false;
            }
            catch (IOException)
            {
                if (collection != null)
                    UnityEngine.Object.Destroy(collection);
                collection = null;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                if (collection != null)
                    UnityEngine.Object.Destroy(collection);
                collection = null;
                return false;
            }
            catch (NotSupportedException)
            {
                if (collection != null)
                    UnityEngine.Object.Destroy(collection);
                collection = null;
                return false;
            }
            catch (UnityException)
            {
                if (collection != null)
                    UnityEngine.Object.Destroy(collection);
                collection = null;
                return false;
            }
        }

        private static bool IsGraphicsStateCollectionCompatible(GraphicsStateCollection collection)
        {
            if (collection == null)
                return false;

            if (collection.graphicsDeviceType != SystemInfo.graphicsDeviceType)
                return false;

            if (collection.runtimePlatform != Application.platform)
                return false;

            string collectionQuality = collection.qualityLevelName;
            if (string.IsNullOrEmpty(collectionQuality))
                return true;

            // Legacy collection labels are informational; runtime quality is continuous and owned by HomeostasisBrain.
            return true;
        }

        private static bool TryResolveGraphicsStateCollectionPath(string configuredPath, out string resolvedPath)
        {
            resolvedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(configuredPath))
                return false;

            if (IsUrlLikePath(configuredPath))
                return false;

            if (!configuredPath.EndsWith(GraphicsStateCollectionExtension, StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                if (Path.IsPathRooted(configuredPath))
                {
#if UNITY_EDITOR
                    string absolutePath = Path.GetFullPath(configuredPath);
                    if (!TryGetProjectFileSystemRoot(out string projectRoot))
                        return false;

                    if (!absolutePath.StartsWith(projectRoot, ResolvePathStringComparison()))
                        return false;

                    resolvedPath = absolutePath;
                    return true;
#else
                    string absolutePath = Path.GetFullPath(configuredPath);
                    if (!TryGetStreamingAssetsFileSystemRoot(out string streamingAssetsRoot))
                        return false;

                    if (!absolutePath.StartsWith(streamingAssetsRoot, ResolvePathStringComparison()))
                        return false;

                    resolvedPath = absolutePath;
                    return true;
#endif
                }

                string normalizedPath = configuredPath.Replace('\\', '/');
                if (HasParentPathSegment(normalizedPath))
                    return false;

                if (normalizedPath.StartsWith(StreamingAssetsProjectPathPrefix, StringComparison.Ordinal))
                {
                    string relativeStreamingPath = normalizedPath.Substring(StreamingAssetsProjectPathPrefix.Length);
                    return TryResolveStreamingAssetsPath(relativeStreamingPath, out resolvedPath);
                }

                if (normalizedPath.StartsWith(AssetsPathPrefix, StringComparison.Ordinal) ||
                    normalizedPath.StartsWith(ProjectSettingsPathPrefix, StringComparison.Ordinal))
                {
#if UNITY_EDITOR
                    if (!TryGetProjectFileSystemRoot(out string projectRoot))
                        return false;

                    resolvedPath = Path.GetFullPath(Path.Combine(projectRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (!resolvedPath.StartsWith(projectRoot, ResolvePathStringComparison()))
                        return false;

                    return true;
#else
                    return false;
#endif
                }

                return TryResolveStreamingAssetsPath(normalizedPath, out resolvedPath);
            }
            catch (ArgumentException)
            {
                resolvedPath = string.Empty;
                return false;
            }
            catch (IOException)
            {
                resolvedPath = string.Empty;
                return false;
            }
            catch (NotSupportedException)
            {
                resolvedPath = string.Empty;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                resolvedPath = string.Empty;
                return false;
            }
        }

        private static bool TryResolveStreamingAssetsPath(string relativePath, out string resolvedPath)
        {
            resolvedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            string normalizedRelativePath = relativePath.Replace('\\', '/');
            if (IsUrlLikePath(normalizedRelativePath))
                return false;

            if (HasParentPathSegment(normalizedRelativePath))
                return false;

            if (!TryGetStreamingAssetsFileSystemRoot(out string streamingAssetsRoot))
                return false;

            string absolutePath = Path.GetFullPath(Path.Combine(
                streamingAssetsRoot,
                normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolutePath.StartsWith(streamingAssetsRoot, ResolvePathStringComparison()))
                return false;

            resolvedPath = absolutePath;
            return true;
        }

        private static bool TryGetStreamingAssetsFileSystemRoot(out string streamingAssetsRoot)
        {
            streamingAssetsRoot = string.Empty;
            string streamingAssetsPath = Application.streamingAssetsPath;
            if (string.IsNullOrWhiteSpace(streamingAssetsPath) || IsUrlLikePath(streamingAssetsPath))
                return false;

            streamingAssetsRoot = Path.GetFullPath(streamingAssetsPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return true;
        }

        private static bool TryGetProjectFileSystemRoot(out string projectRoot)
        {
            projectRoot = string.Empty;
            string dataPath = Application.dataPath;
            if (string.IsNullOrWhiteSpace(dataPath) || IsUrlLikePath(dataPath))
                return false;

            projectRoot = Path.GetFullPath(Path.Combine(dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return true;
        }

        private static bool IsUrlLikePath(string path)
        {
            return path.IndexOf("://", StringComparison.Ordinal) >= 0 ||
                   path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase);
        }

        private static StringComparison ResolvePathStringComparison()
        {
            RuntimePlatform platform = Application.platform;
            return platform == RuntimePlatform.WindowsEditor || platform == RuntimePlatform.WindowsPlayer
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        private static bool HasParentPathSegment(string normalizedPath)
        {
            return normalizedPath == ".." ||
                   normalizedPath.StartsWith("../", StringComparison.Ordinal) ||
                   normalizedPath.EndsWith("/..", StringComparison.Ordinal) ||
                   normalizedPath.Contains("/../");
        }

        private static int ClampWarmupCountToInt(long count)
        {
            if (count <= 0L)
                return 0;

            return count >= int.MaxValue ? int.MaxValue : (int)count;
        }

        private static int CountValidShaderWarmupShaders(Shader[] shaders)
        {
            if (shaders == null)
                return 0;

            int count = 0;
            for (int i = 0; i < shaders.Length; i++)
            {
                if (shaders[i] != null)
                    count++;
            }

            return count;
        }

        private static int FindInvalidShaderVariantCollectionIndex(ShaderVariantCollection[] collections)
        {
            if (collections == null)
                return -1;

            for (int i = 0; i < collections.Length; i++)
            {
                if (collections[i] == null)
                    return i;
            }

            return -1;
        }

        private static int FindEmptyShaderVariantCollectionIndex(ShaderVariantCollection[] collections)
        {
            if (collections == null)
                return -1;

            for (int i = 0; i < collections.Length; i++)
            {
                ShaderVariantCollection collection = collections[i];
                if (collection != null && collection.variantCount <= 0)
                    return i;
            }

            return -1;
        }

        private static int FindInvalidShaderWarmupShaderIndex(Shader[] shaders)
        {
            if (shaders == null)
                return -1;

            for (int i = 0; i < shaders.Length; i++)
            {
                if (shaders[i] == null)
                    return i;
            }

            return -1;
        }

        private async Awaitable<bool> WaitForShaderWarmupJobAsync(
            JobHandle handle,
            CancellationToken ct,
            long warmupStart,
            int timeoutMilliseconds,
            int collectionIndex,
            int shaderIndex,
            int variantCount)
        {
            while (!handle.IsCompleted)
            {
                if (ct.IsCancellationRequested)
                {
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                    return false;
                }

                BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
                if (HasShaderWarmupTimedOut(warmupStart, timeoutMilliseconds))
                {
                    DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
                    return FailBootstrapShaderWarmup(
                        ShaderWarmupErrorCode.Timeout,
                        _ShaderWarmupTimeoutHash,
                        collectionIndex,
                        shaderIndex,
                        variantCount,
                        warmupStart);
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
                BootstrapStatus.PulseActiveStep(BootstrapStepToken.CoreServices);
            }

            DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            return true;
        }

        private static bool TryScheduleGraphicsStateWarmup(GraphicsStateCollection collection, int count, out JobHandle handle)
        {
            try
            {
                handle = collection.WarmUpProgressively(count, default);
                return true;
            }
            catch (ArgumentException)
            {
                handle = default;
                return false;
            }
            catch (InvalidOperationException)
            {
                handle = default;
                return false;
            }
            catch (UnityException)
            {
                handle = default;
                return false;
            }
        }

        private static bool TryWarmShaderVariant(ShaderVariantCollection collection, Shader shader, ShaderWarmupSetup setup)
        {
            try
            {
                ShaderWarmup.WarmupShaderFromCollection(collection, shader, setup);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private unsafe void QueueBootstrapShaderWarmupTelemetryDump()
        {
            if (Interlocked.Exchange(ref _shaderWarmupDumpQueued, 1) != 0)
                return;

            RecordBootstrapShaderWarmupTelemetry(
                ShaderWarmupTelemetryPhase.DumpQueued,
                ShaderWarmupTelemetryFlags.DumpQueued,
                ShaderWarmupErrorCode.None,
                _ShaderWarmupFailureHash,
                -1,
                -1,
                0,
                0,
                0L);

            CacheBootstrapShaderWarmupDumpPathCold();
            string absolutePath = _shaderWarmupDumpPath;
            string tempPath = _shaderWarmupDumpTempPath;
            if (string.IsNullOrEmpty(absolutePath) || string.IsNullOrEmpty(tempPath))
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                _shaderWarmupTelemetryHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in _shaderWarmupTelemetryHandle, SystemID.Bootstrap, out NativeArray<BootstrapTelemetryEntry> ring))
            {
                WriteBootstrapShaderWarmupFallbackDumpHeader(_shaderWarmupDumpScratch);
                _shaderWarmupDumpPath = absolutePath;
                _shaderWarmupDumpTempPath = tempPath;
                _shaderWarmupDumpByteCount = BootstrapTelemetryEntrySizeBytes;
                ThreadPool.QueueUserWorkItem(_shaderWarmupDumpCallback, this);
                return;
            }

            int byteCount = 0;
            bool snapshotCopied = false;
            try
            {
                if (ring.IsCreated && ring.Length > 0)
                {
                    void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(ring);
                    byteCount = math.min(ring.Length * BootstrapTelemetryEntrySizeBytes, BootstrapShaderWarmupDumpBytes);
                    fixed (byte* target = _shaderWarmupDumpScratch)
                    {
                        UnsafeUtility.MemCpy(target, source, byteCount);
                    }

                    snapshotCopied = true;
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in _shaderWarmupTelemetryHandle, SystemID.Bootstrap);
            }

            if (!snapshotCopied)
            {
                WriteBootstrapShaderWarmupFallbackDumpHeader(_shaderWarmupDumpScratch);
                byteCount = BootstrapTelemetryEntrySizeBytes;
            }

            _shaderWarmupDumpPath = absolutePath;
            _shaderWarmupDumpTempPath = tempPath;
            _shaderWarmupDumpByteCount = byteCount;
            ThreadPool.QueueUserWorkItem(_shaderWarmupDumpCallback, this);
        }

        private static unsafe void WriteBootstrapShaderWarmupFallbackDumpHeader(byte[] target)
        {
            if (target == null || target.Length < BootstrapTelemetryEntrySizeBytes)
                return;

            Array.Clear(target, 0, target.Length);
            BootstrapTelemetryEntry fallbackEntry = new BootstrapTelemetryEntry
            {
                TimestampTicks = DateTime.UtcNow.Ticks,
                DurationMicroseconds = 0L,
                ContextHash = _GameBootstrapperContextHash,
                FrameIndex = unchecked((uint)Time.frameCount),
                EventHash = _ShaderWarmupFailureHash,
                CollectionIndex = uint.MaxValue,
                ShaderIndex = uint.MaxValue,
                VariantCount = 0u,
                WarmedVariantCount = 0u,
                QualityWeight01 = ResolveBootstrapQualityWeight01(),
                Phase = (ushort)ShaderWarmupTelemetryPhase.Failure,
                Flags = (ushort)(ShaderWarmupTelemetryFlags.Failure | ShaderWarmupTelemetryFlags.DumpQueued),
                ErrorCode = (ushort)ShaderWarmupErrorCode.MissingTelemetryRing,
                Reserved = 0
            };

            fixed (byte* targetPtr = target)
            {
                UnsafeUtility.CopyStructureToPtr(ref fallbackEntry, targetPtr);
            }
        }

        private static void WriteBootstrapShaderWarmupDumpOnWorker(object state)
        {
            if (state is GameBootstrapper bootstrapper)
                bootstrapper.WriteBootstrapShaderWarmupDumpOnWorker();
        }

        private unsafe void WriteBootstrapShaderWarmupDumpOnWorker()
        {
            byte[] scratch = _shaderWarmupDumpScratch;
            string path = _shaderWarmupDumpPath;
            string tempPath = _shaderWarmupDumpTempPath;
            int byteCount = _shaderWarmupDumpByteCount;
            if (scratch == null ||
                string.IsNullOrEmpty(path) ||
                string.IsNullOrEmpty(tempPath) ||
                byteCount <= 0 ||
                byteCount > scratch.Length)
            {
                return;
            }

            if (!TryEnsureBootstrapShaderWarmupDumpDirectoryCold(path))
                return;

            try
            {
                fixed (byte* source = scratch)
                {
                    if (!AsyncWriteManager.WriteAll(tempPath, source, byteCount, out _))
                        return;
                }

                if (!AsyncWriteManager.TryGetFileLength(tempPath, out long tempDumpBytes, out _) ||
                    tempDumpBytes != byteCount ||
                    !AsyncWriteManager.FlushCriticalSavePath(tempPath, tempDumpBytes, out _))
                {
                    return;
                }

                TryPromoteBootstrapShaderWarmupDump(tempPath, path, tempDumpBytes);
            }
            finally
            {
                TryDeleteBootstrapShaderWarmupDumpTemp(tempPath);
            }
        }

        private void CacheBootstrapShaderWarmupDumpPathCold()
        {
            if (_shaderWarmupDumpPathCacheAttempted)
                return;

            _shaderWarmupDumpPathCacheAttempted = true;
            try
            {
                string absolutePath = ResolveBootstrapShaderWarmupDumpPath();
                _shaderWarmupDumpPath = absolutePath;
                _shaderWarmupDumpTempPath = ResolveBootstrapShaderWarmupTempDumpPath(absolutePath);
            }
            catch (ArgumentException)
            {
                _shaderWarmupDumpPath = string.Empty;
                _shaderWarmupDumpTempPath = string.Empty;
            }
            catch (IOException)
            {
                _shaderWarmupDumpPath = string.Empty;
                _shaderWarmupDumpTempPath = string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                _shaderWarmupDumpPath = string.Empty;
                _shaderWarmupDumpTempPath = string.Empty;
            }
            catch (NotSupportedException)
            {
                _shaderWarmupDumpPath = string.Empty;
                _shaderWarmupDumpTempPath = string.Empty;
            }
        }

        private static bool TryEnsureBootstrapShaderWarmupDumpDirectoryCold(string finalPath)
        {
            try
            {
                HectonPersistentPathPolicy.EnsureParentDirectoryCold(finalPath);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static string ResolveBootstrapShaderWarmupDumpPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Docs", "AgentLogs", BootstrapShaderWarmupDumpFileName);
        }

        private static string ResolveBootstrapShaderWarmupTempDumpPath(string finalPath)
        {
            return string.IsNullOrEmpty(finalPath) ? string.Empty : finalPath + ".tmp";
        }

        private static bool TryPromoteBootstrapShaderWarmupDump(string tempPath, string finalPath, long expectedByteCount)
        {
            if (string.IsNullOrEmpty(tempPath) || string.IsNullOrEmpty(finalPath) || expectedByteCount <= 0L || !File.Exists(tempPath))
                return false;

            try
            {
                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                AsyncWriteManager.InvalidateCachedReadWindows(finalPath);
                try
                {
                    if (File.Exists(finalPath))
                        File.Replace(tempPath, finalPath, null);
                    else
                        File.Move(tempPath, finalPath);
                }
                finally
                {
                    AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
                    AsyncWriteManager.InvalidateCachedReadWindows(finalPath);
                }

                if (!AsyncWriteManager.TryGetFileLength(finalPath, out long promotedDumpBytes, out _) ||
                    promotedDumpBytes != expectedByteCount ||
                    !AsyncWriteManager.FlushCriticalSavePath(finalPath, promotedDumpBytes, out _))
                {
                    return false;
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        private static void TryDeleteBootstrapShaderWarmupDumpTemp(string tempPath)
        {
            if (string.IsNullOrEmpty(tempPath))
                return;

            AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }
            finally
            {
                AsyncWriteManager.InvalidateCachedReadWindows(tempPath);
            }
        }

        private static void WarmMathLodShaderKeywords()
        {
            float qualityWeight01 = ResolveBootstrapQualityWeight01();
            if (GlobalRegistry.MathPrecision == MathPrecisionLevel.Low)
                qualityWeight01 = math.min(qualityWeight01, 0.35f);
            DistanceMath.PushShaderMathLod(qualityWeight01);
        }

        private static byte ResolveBootstrapScalabilityProfileByte()
        {
            float quality = ResolveBootstrapQualityWeight01();
            return (byte)math.clamp((int)math.round(quality * byte.MaxValue), 0, byte.MaxValue);
        }

        private static float ResolveBootstrapQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality) ? math.saturate(quality) : 1f;
        }

        private static bool AreBootstrapActivationGatesReady()
        {
            if (!_preWarmAssetsReady)
                return false;

            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            return registry != null && registry.AreResidentWorldPrefabPoolsReady();
        }

        private static bool HasWatchdogElapsed(long startTimestamp, double timeoutSeconds, out double elapsedSeconds)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;
            return elapsedSeconds >= timeoutSeconds;
        }

        private static async Awaitable<bool> WaitForBootstrapActivationGatesAsync(CancellationToken ct)
        {
            try
            {
                int waitFrames = 0;
                long waitStartTimestamp = Stopwatch.GetTimestamp();
                while (!AreBootstrapActivationGatesReady())
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, BootstrapJobWaitWatchdogSeconds, out double elapsedSeconds))
                    {
                        LogBootstrapSceneLoadWatchdog("asset activation gates", 0.9f, waitFrames, elapsedSeconds);
                        return false;
                    }

                    waitFrames++;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Awaitable<bool> InitializeBootstrapLayerNodesAsync(BootstrapPhase phase, CancellationToken ct)
        {
            if (_bootstrapExecutionOrderCount != (int)BootstrapDependencyNode.Count &&
                !TryBuildBootstrapDependencyExecutionOrder(_bootstrapExecutionOrder, out _bootstrapExecutionOrderCount))
            {
                LogBootstrapDependencyGraphFailure(phase);
                return false;
            }

            BootstrapStepToken phaseStepToken = ResolveBootstrapStepToken(phase);
            for (int orderIndex = 0; orderIndex < _bootstrapExecutionOrderCount; orderIndex++)
            {
                BootstrapDependencyNode node = _bootstrapExecutionOrder[orderIndex];
                if (ResolveBootstrapNodePhase(node) != phase)
                    continue;

                BootstrapStatus.PulseActiveStep(phaseStepToken);
                WriteBootStateRecord(BootStateMarker.ServiceStarted, phase, ResolveRegistrySlotForBootstrapNode(node));
                long serviceStartTimestamp = Stopwatch.GetTimestamp();
                try
                {
                    UnityEngine.Debug.Log($"[GameBootstrapper] TryInitializeBootstrapDependencyNodeWithFallback for node {node}");
                    if (!TryInitializeBootstrapDependencyNodeWithFallback(node))
                    {
                        LogBootstrapDependencyFailure(phase, node);
                        return false;
                    }

                    UnityEngine.Debug.Log($"[GameBootstrapper] Waiting for heartbeat for node {node}");

                    BootstrapStatus.PulseActiveStep(phaseStepToken);
                    if (!await WaitForBootstrapDependencyHeartbeatAsync(node, ct))
                    {
                        LogBootstrapDependencyFailure(phase, node);
                        return false;
                    }

                    BootstrapStatus.PulseActiveStep(phaseStepToken);
                }
                finally
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    double elapsedMilliseconds =
                        (Stopwatch.GetTimestamp() - serviceStartTimestamp) * 1000.0 / Stopwatch.Frequency;
                    BootstrapHealthMonitor.RecordServiceDuration((int)node, elapsedMilliseconds);
#endif
                }
            }

            return true;
        }

        private static async Awaitable<bool> WaitForBootstrapDependencyHeartbeatAsync(
            BootstrapDependencyNode node,
            CancellationToken ct)
        {
            int waitFrames = 0;
            long waitStartTimestamp = Stopwatch.GetTimestamp();
            while (!IsBootstrapDependencyHeartbeatReady(node))
            {
                ct.ThrowIfCancellationRequested();
                TryRefreshBootstrapDependencyHeartbeat(node, waitFrames);
                if (IsBootstrapDependencyHeartbeatReady(node))
                    return true;

                if (HasWatchdogElapsed(waitStartTimestamp, OptionalServiceTimeoutMilliseconds * 0.001d, out double elapsedSeconds))
                {
                    LogBootstrapHeartbeatFailure(node, waitFrames, elapsedSeconds);
                    TriggerServiceEmergencyReset(node);
                    return false;
                }

                waitFrames++;
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            }

            return true;
        }

        private static void TryRefreshBootstrapDependencyHeartbeat(BootstrapDependencyNode node, int waitFrames)
        {
            if ((waitFrames & (BootstrapHeartbeatRebindCadenceFrames - 1)) != 0)
                return;

            try
            {
                switch (node)
                {
                    case BootstrapDependencyNode.EcosystemDirector:
                    {
                        if (GlobalRegistry.DataVault == null)
                            return;

                        EcosystemDirector director = null;
                        WorldRuntimeReferenceUtility.TryResolveEcosystemDirector(ref director);
                        if (director == null)
                            director = EnsureEcosystemDirectorRegistered();

                        if (director != null && !director.IsServiceReady)
                            director.InitializeService();

                        break;
                    }
                }
            }
            catch (Exception)
            {
                RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
                RuntimeDiagnosticsTrace.WriteEvent(
                    "bootstrap.heartbeat.rebind.exception",
                    ResolveBootstrapDependencyNodeName(node));
            }
        }

        private static bool IsBootstrapDependencyHeartbeatReady(BootstrapDependencyNode node)
        {
            object service = ResolveBootstrapDependencyService(node);
            if (node == BootstrapDependencyNode.SpatialAudioManager)
                return IsSpatialAudioBootstrapNodeReady(service);

            if (service is IServiceHeartbeat heartbeat)
                return heartbeat.IsServiceReady && heartbeat.HeartbeatState != ServiceHeartbeatState.Failed;

            return IsBootstrapDependencyNodeReady(node, service);
        }

        private static bool IsBootstrapDependencyNodeReady(BootstrapDependencyNode node)
        {
            return IsBootstrapDependencyNodeReady(node, ResolveBootstrapDependencyService(node));
        }

        private static bool IsBootstrapDependencyNodeReady(BootstrapDependencyNode node, object service)
        {
            switch (node)
            {
                case BootstrapDependencyNode.RenderDispatcher:
                    return _headlessBootMode || service != null;
                case BootstrapDependencyNode.HectonFloatingOrigin:
                    return service != null && !HectonFloatingOrigin.IsShiftInProgress;
                case BootstrapDependencyNode.ConnectionSplineBatchRenderer:
                    return _headlessBootMode || service != null;
                case BootstrapDependencyNode.DebrisManager:
                    return IsDebrisManagerBootstrapNodeReady(service);
                case BootstrapDependencyNode.FaunaSimulation:
                    return service is IFaunaSim faunaSimulation && faunaSimulation.IsReady;
                case BootstrapDependencyNode.SpatialAudioManager:
                    return IsSpatialAudioBootstrapNodeReady(service);
                case BootstrapDependencyNode.ConstructionManager:
                    return service != null || GlobalRegistry.Logistics == null;
                default:
                    return service != null;
            }
        }

        private static object ResolveBootstrapDependencyService(BootstrapDependencyNode node)
        {
            switch (node)
            {
                case BootstrapDependencyNode.SystemDispatcher: return GlobalRegistry.Dispatcher;
                case BootstrapDependencyNode.GameTickManager: return GlobalRegistry.TickManager;
                case BootstrapDependencyNode.SaveManager: return GlobalRegistry.Save;
                case BootstrapDependencyNode.ObjectPoolManager: return GlobalRegistry.ObjectPool;
                case BootstrapDependencyNode.RenderDispatcher: return GlobalRegistry.RenderDispatcher;
                case BootstrapDependencyNode.SceneRuntimeService: return GlobalRegistry.Scene;
                case BootstrapDependencyNode.EquipmentInteractionHandler: return GlobalRegistry.InteractionSignals;
                case BootstrapDependencyNode.HectonFloatingOrigin: return GlobalRegistry.FloatingOrigin;
                case BootstrapDependencyNode.ConnectionSplineBatchRenderer: return GlobalRegistry.ConnectionSplineBatchRenderer;
                case BootstrapDependencyNode.GlobalPhysicsStateManager: return GlobalRegistry.PhysicsStateManager;
                case BootstrapDependencyNode.PhysicsApplySystem: return GlobalRegistry.Physics;
                // The startup graph declares this node as GlobalRegistryServiceSlot.Debris
                // (BootstrapRegistryCycleValidator._startupNodes), and IDebrisService is what DebrisManager
                // registers. DebrisCompute is a different slot owned by CarveDebrisComputeRenderer and is not
                // in the startup graph at all, so resolving it here made the node blind to its own service.
                case BootstrapDependencyNode.DebrisManager: return GlobalRegistry.Debris;
                case BootstrapDependencyNode.EnvironmentRuntimeContextService: return GlobalRegistry.Environment;
                case BootstrapDependencyNode.OceanKinematicsRuntimeService: return GlobalRegistry.OceanKinematics;
                case BootstrapDependencyNode.EcosystemDirector: return GlobalRegistry.EcosystemDirector;
                case BootstrapDependencyNode.FaunaSimulation: return GlobalRegistry.FaunaSimulation;
                case BootstrapDependencyNode.SpatialAudioManager: return GlobalRegistry.Audio;
                case BootstrapDependencyNode.NativeInputManager: return GlobalRegistry.NativeInputRuntime;
                case BootstrapDependencyNode.InputDispatcher: return GlobalRegistry.RegisteredInput;
                case BootstrapDependencyNode.PlayerRuntimeContextService: return GlobalRegistry.Player;
                case BootstrapDependencyNode.PlayerInventoryManager: return GlobalRegistry.PlayerInventory;
                case BootstrapDependencyNode.PlayerActionRuntime: return GlobalRegistry.PlayerActions;
                case BootstrapDependencyNode.PlayerSensoryManager: return GlobalRegistry.PlayerSensory;
                case BootstrapDependencyNode.PowerGridManager: return GlobalRegistry.PowerGrid;
                case BootstrapDependencyNode.ConstructionManager: return GlobalRegistry.ConstructionRuntime;
                case BootstrapDependencyNode.BeaconNetworkSystem: return GlobalRegistry.BeaconNetwork;
                case BootstrapDependencyNode.ModWorldPersistenceManager: return GlobalRegistry.ModWorldPersistence;
                default: return null;
            }
        }

        private static BootstrapPhase ResolveBootstrapNodePhase(BootstrapDependencyNode node)
        {
            switch (node)
            {
                case BootstrapDependencyNode.SystemDispatcher:
                case BootstrapDependencyNode.GameTickManager:
                case BootstrapDependencyNode.SaveManager:
                case BootstrapDependencyNode.ObjectPoolManager:
                case BootstrapDependencyNode.RenderDispatcher:
                case BootstrapDependencyNode.SceneRuntimeService:
                case BootstrapDependencyNode.EquipmentInteractionHandler:
                case BootstrapDependencyNode.ModWorldPersistenceManager:
                    return BootstrapPhase.CoreServices;

                case BootstrapDependencyNode.HectonFloatingOrigin:
                case BootstrapDependencyNode.GlobalPhysicsStateManager:
                case BootstrapDependencyNode.PhysicsApplySystem:
                case BootstrapDependencyNode.DebrisManager:
                case BootstrapDependencyNode.ConnectionSplineBatchRenderer:
                case BootstrapDependencyNode.EnvironmentRuntimeContextService:
                case BootstrapDependencyNode.OceanKinematicsRuntimeService:
                case BootstrapDependencyNode.EcosystemDirector:
                case BootstrapDependencyNode.FaunaSimulation:
                case BootstrapDependencyNode.SpatialAudioManager:
                case BootstrapDependencyNode.PowerGridManager:
                case BootstrapDependencyNode.ConstructionManager:
                    return BootstrapPhase.Environment;

                case BootstrapDependencyNode.NativeInputManager:
                case BootstrapDependencyNode.InputDispatcher:
                case BootstrapDependencyNode.PlayerRuntimeContextService:
                case BootstrapDependencyNode.PlayerInventoryManager:
                case BootstrapDependencyNode.PlayerActionRuntime:
                case BootstrapDependencyNode.PlayerSensoryManager:
                case BootstrapDependencyNode.BeaconNetworkSystem:
                    return BootstrapPhase.Player;

                default:
                    return BootstrapPhase.Fatal;
            }
        }

        private static bool TryInitializeBootstrapDependencyNodeWithFallback(BootstrapDependencyNode node)
        {
            try
            {
                return InitializeBootstrapDependencyNode(node);
            }
            catch (Exception exception)
            {
                if (TryRegisterStableFallbackForBootstrapNode(node, exception))
                    return true;

                RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
                RuntimeDiagnosticsTrace.WriteEvent(
                    "bootstrap.service.init.exception",
                    ResolveBootstrapDependencyNodeName(node));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[GameBootstrapper] Bootstrap dependency exception. node=" +
                    ResolveBootstrapDependencyNodeName(node) +
                    " exception=" +
                    exception);
#endif
                return false;
            }
        }

        private static bool TryRegisterStableFallbackForBootstrapNode(
            BootstrapDependencyNode node,
            Exception exception)
        {
            if (node == BootstrapDependencyNode.SpatialAudioManager)
                return TryRegisterNoOpAudioFallback("SpatialAudioManager init exception", exception);

            GlobalRegistryServiceSlot slot = ResolveRegistrySlotForBootstrapNode(node);
            if (GlobalRegistry.TryReplaceBootstrapServiceWithStableProxy(slot))
            {
                LogOptionalBootstrapWarning("Injected stable bootstrap proxy for " + ResolveBootstrapDependencyNodeName(node));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Records the real installation state of the <c>DebrisManager</c> bootstrap node and reports whether
        /// boot may proceed past it.
        /// </summary>
        /// <remarks>
        /// This node is enumerated, ordered, phase-mapped to <see cref="BootstrapPhase.Environment"/> and
        /// heartbeat-probed exactly like a wired dependency. The only implementer of <c>IDebrisService</c>
        /// is <c>DebrisManager</c> (Assets/_Project/Scripts/Gameplay/DebrisManager.cs). The node initializer
        /// now calls <c>DebrisManager.EnsureRuntimeInstance</c> before this reporter so a healthy boot
        /// installs the owner and returns READY via the non-null path below.
        /// <para>
        /// If the factory still leaves the registry empty, returning <c>false</c> is deliberately NOT the
        /// refusal used here: a false return from a bootstrap node is fatal to the entire game (Environment
        /// phase abort → no Player/UI/CoreReady/LockReady). Nothing in the startup graph depends on the
        /// Debris slot, so an empty slot after construction remains a loud named exemption rather than a
        /// boot-killer. It is never a silent success, and it never claims to be ready.
        /// </para>
        /// Cold path: runs once per boot from the node initializer, so the message construction here costs
        /// nothing at runtime cadence.
        /// </remarks>
        /// <returns>Always <c>true</c>, so boot survives an uninstalled debris subsystem.</returns>
        private static bool ReportDebrisManagerBootstrapNodeState()
        {
            object debrisService = ResolveBootstrapDependencyService(BootstrapDependencyNode.DebrisManager);

            // Assigned, not OR-ed: the exemption must never survive from an earlier boot into a boot where the
            // service is genuinely present.
            _debrisManagerBootstrapNodeNotInstalled = debrisService == null;
            if (!_debrisManagerBootstrapNodeNotInstalled)
                return true;

            RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
            RuntimeDiagnosticsTrace.WriteEvent(
                "bootstrap.node.declared_but_not_installed",
                ResolveBootstrapDependencyNodeName(BootstrapDependencyNode.DebrisManager));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "[GameBootstrapper] DebrisManager.EnsureRuntimeInstance was called but GlobalRegistry.Debris " +
                "is still empty (no debris owner this session; every IDebrisService.SpawnBurst call is dropped). " +
                "This node is EXEMPT, not ready. Boot continues on purpose: nothing in the startup graph " +
                "depends on the Debris slot, and failing the node would abort the whole boot.");
#endif
            return true;
        }


        /// <summary>
        /// Reports the real readiness of the <c>DebrisManager</c> bootstrap node.
        /// </summary>
        /// <remarks>
        /// An installed service is gated on its own heartbeat, so the node genuinely works or genuinely fails.
        /// An empty slot passes only through the exemption that
        /// <see cref="ReportDebrisManagerBootstrapNodeState"/> has already recorded loudly; if that record was
        /// never written, an empty slot reports NOT ready rather than inventing readiness.
        /// <para>
        /// Allocation-free and log-free by contract: one reference test, one interface type check, two property
        /// reads, one enum compare and one static bool read. <see cref="WaitForBootstrapDependencyHeartbeatAsync"/>
        /// polls this every frame while the node is pending, so it must not format strings or log - the single
        /// loud record is written once per boot by the initializer instead.
        /// </para>
        /// </remarks>
        private static bool IsDebrisManagerBootstrapNodeReady(object service)
        {
            if (service is IServiceHeartbeat heartbeat)
                return heartbeat.IsServiceReady && heartbeat.HeartbeatState != ServiceHeartbeatState.Failed;

            return service != null || _debrisManagerBootstrapNodeNotInstalled;
        }

        /// <summary>
        /// Reports whether boot may proceed past the <c>SpatialAudioManager</c> node.
        /// </summary>
        /// <remarks>
        /// A real owner is gated on its own <c>IsAudioRuntimeReady</c>, so the node genuinely works or genuinely
        /// fails. A stubbed slot passes only through the exemption that
        /// <see cref="TryRegisterNoOpAudioFallback"/> has already recorded loudly; if that record was never written,
        /// an unusable slot reports NOT ready rather than inventing readiness.
        /// <para>
        /// The exemption term is what keeps <see cref="NoOpAudioService"/> honest without killing the session.
        /// <see cref="NoOpAudioService.IsInitialized"/> and <see cref="NoOpAudioService.IsAudioRuntimeReady"/> are
        /// now <c>false</c>, so before this term existed the stub would have failed this predicate, failed the
        /// Environment phase and aborted the whole boot - a strictly worse outcome than silent audio.
        /// </para>
        /// <para>
        /// Allocation-free and log-free by contract: <see cref="WaitForBootstrapDependencyHeartbeatAsync"/> polls
        /// this every frame while the node is pending, so it must not format strings or log. The single loud record
        /// is written once per boot by the fallback registrar instead.
        /// </para>
        /// </remarks>
        private static bool IsSpatialAudioBootstrapNodeReady(object service)
        {
            return _headlessBootMode ||
                   IsBootstrapAudioServiceUsable(service as IAudioService) ||
                   _audioBootstrapNodeStubbed;
        }

        private static bool InitializeBootstrapDependencyNode(BootstrapDependencyNode node)
        {
            switch (node)
            {
                case BootstrapDependencyNode.SystemDispatcher:
                    return EnsureSystemDispatcherRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.GameTickManager:
                    return EnsureGameTickManagerRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.SaveManager:
                    return EnsureSaveServiceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.ObjectPoolManager:
                    return EnsureObjectPoolServiceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.RenderDispatcher:
                    if (_headlessBootMode)
                        return true;

                    return EnsureRenderDispatcherRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.SceneRuntimeService:
                {
                    SceneRuntimeService sceneRuntimeService = SceneRuntimeService.EnsureRuntimeInstance();
                    if (sceneRuntimeService == null)
                        return false;

                    PersistRuntimeService(sceneRuntimeService);
                    sceneRuntimeService.InitializeService();
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.EquipmentInteractionHandler:
                    EquipmentInteractionHandler interactionHandler = EnsureEquipmentInteractionServiceRegistered();
                    EnsureAuxiliaryEquipmentRouterRegistered();
                    return interactionHandler != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.HectonFloatingOrigin:
                    return EnsureFloatingOriginRegistered() != null && GlobalRegistry.FloatingOrigin != null;

                case BootstrapDependencyNode.ConnectionSplineBatchRenderer:
                    if (_headlessBootMode)
                        return true;

                    return EnsureConnectionSplineBatchRendererRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.GlobalPhysicsStateManager:
                    return EnsureGlobalPhysicsStateManagerRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.PhysicsApplySystem:
                {
                    PhysicsApplySystem physicsApplySystem = EnsurePhysicsApplySystemRegistered();
                    if (physicsApplySystem == null)
                        return false;

                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.DebrisManager:
                {
                    // DebrisManager is declared as a bootstrap dependency and GlobalRegistry has a
                    // real Debris slot. Factory EnsureRuntimeInstance already exists (resolve-or-
                    // create + InitializeService for heartbeat). Wire the construction site here so
                    // the Environment-phase node is actually READY instead of permanently EXEMPT.
                    Hecton8.Gameplay.DebrisManager.EnsureRuntimeInstance();

                    // WorldChunkResidencyManager (IStreamingBackpressureService) — sole owner.
                    // StreamingBackpressureRuntime is NOT a scene hot-swap slot. Must publish before
                    // LockReady; post-Ready EnsureRuntimeInstance + TryRegister throws
                    // CriticalBootException (ready-lock). WorldRuntimeInstaller deliberately skips
                    // construction; this Environment-phase site is the pre-Ready construction path.
                    Hecton8.World.WorldChunkResidencyManager.EnsureRuntimeInstance();
                    return ReportDebrisManagerBootstrapNodeState();
                }



                case BootstrapDependencyNode.EnvironmentRuntimeContextService:
                {
                    EnvironmentRuntimeContextService environmentContextService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
                    if (environmentContextService == null)
                        return false;

                    PersistRuntimeService(environmentContextService);
                    environmentContextService.InitializeService();
                    HectonSeismicTideDirector seismicTideDirector = HectonSeismicTideDirector.EnsureRuntimeInstance();
                    if (seismicTideDirector != null)
                    {
                        PersistRuntimeService(seismicTideDirector);
                        seismicTideDirector.InitializeService();
                    }

                    return GlobalRegistry.Environment != null && GlobalRegistry.SeismicDirector != null;
                }

                case BootstrapDependencyNode.OceanKinematicsRuntimeService:
                {
                    OceanKinematicsRuntimeService oceanKinematicsRuntimeService = OceanKinematicsRuntimeService.EnsureRuntimeInstance();
                    if (oceanKinematicsRuntimeService == null)
                        return false;

                    PersistRuntimeService(oceanKinematicsRuntimeService);
                    oceanKinematicsRuntimeService.InitializeService();
                    // Result is intentionally not fatal to boot - caustics are cosmetic and have no startup-graph
                    // node of their own. TryEnsureDeferredCausticsRegistered logs its own named failure, so this
                    // is a reported degrade rather than a discarded bool. Do not convert it into a false return.
                    if (!_headlessBootMode)
                    {
                        try
                        {
                            TryEnsureDeferredCausticsRegistered();
                        }
                        catch (Exception causticsException)
                        {
                            LogDeferredCausticsWiringFailure(
                                "TryEnsureDeferredCausticsRegistered threw: " + causticsException);
                        }
                    }
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.EcosystemDirector:
                    return EnsureEcosystemDirectorRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.FaunaSimulation:
                    return EnsureFaunaSimulationRegistered();

                case BootstrapDependencyNode.SpatialAudioManager:
                    if (_headlessBootMode)
                        return true;

                    return InitializeSpatialAudioBootstrapNode();

                case BootstrapDependencyNode.ConstructionManager:
                {
                    ConstructionManager constructionManager = EnsureConstructionServiceRegistered();
                    return constructionManager == null || GlobalRegistry.ConstructionRuntime != null;
                }

                case BootstrapDependencyNode.NativeInputManager:
                    return EnsureNativeInputManagerRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.InputDispatcher:
                    return EnsureInputDispatcherRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.PlayerRuntimeContextService:
                {
                    PlayerRuntimeContextService playerContextService = PlayerRuntimeContextService.EnsureRuntimeInstance();
                    if (playerContextService == null)
                        return false;

                    PersistRuntimeService(playerContextService);
                    playerContextService.InitializeServiceDeferredSync();
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.PlayerInventoryManager:
                {
                    PlayerInventoryManager playerInventoryManager = PlayerInventoryManager.EnsureRuntimeInstance();
                    if (playerInventoryManager == null)
                        return false;

                    PersistRuntimeService(playerInventoryManager);
                    playerInventoryManager.InitializeService();
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.PlayerActionRuntime:
                {
                    PlayerActionController playerActionController = PlayerActionController.EnsureRuntimeInstance();
                    if (playerActionController == null)
                        return false;

                    PersistRuntimeService(playerActionController);
                    playerActionController.InitializeService();
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.PlayerSensoryManager:
                {
                    PlayerSensoryManager playerSensoryManager = PlayerSensoryManager.EnsureRuntimeInstance();
                    if (playerSensoryManager == null)
                        return false;

                    PersistRuntimeService(playerSensoryManager);
                    playerSensoryManager.InitializeService();
                    return IsBootstrapDependencyNodeReady(node);
                }

                case BootstrapDependencyNode.BeaconNetworkSystem:
                    return EnsureBeaconNetworkServiceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.ModWorldPersistenceManager:
                    return EnsureModWorldPersistenceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                case BootstrapDependencyNode.PowerGridManager:
                    return EnsurePowerGridServiceRegistered() != null && IsBootstrapDependencyNodeReady(node);

                default:
                    return false;
            }
        }

        private static SystemDispatcher EnsureSystemDispatcherRegistered()
        {
            EnsureSimulationBucketerRegistered();
            EnsureJobAdmissionServiceRegistered();

            SystemDispatcher dispatcher = GlobalRegistry.Dispatcher;
            if (dispatcher == null)
                dispatcher = SystemDispatcher.ActiveRuntimeInstance;

            if (dispatcher == null)
            {
                GameObject runtimeRoot = new GameObject("[SystemDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned gameplay dispatcher root - owner: GameBootstrapper
                dispatcher = runtimeRoot.AddComponent<SystemDispatcher>();
            }

            PersistRuntimeService(dispatcher);

            dispatcher.InitializeService();
            ActiveInstance?.TryRegisterBootstrapSlowTickable();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureRuntimeWatchdogRegistered();
            EnsureGCMonitorRegistered();
#endif
            return dispatcher;
        }

        private static ISimulationBucketer EnsureSimulationBucketerRegistered()
        {
            ISimulationBucketer registered = GlobalRegistry.SimulationBucketer;
            if (registered != null)
            {
                if (registered is ModuloSimulationBucketer moduloBucketer)
                    moduloBucketer.Initialize(SimulationBucketConstants.DefaultEntityCapacity, GlobalRegistry.DataVault);
                else if (!registered.IsInitialized)
                    registered.Initialize(SimulationBucketConstants.DefaultEntityCapacity);

                return registered;
            }

            if (_simulationBucketerService == null)
                _simulationBucketerService = new ModuloSimulationBucketer(); // COLD ALLOC: ModuloSimulationBucketer[1] - bootstrap-owned simulation cadence slicer - owner: GameBootstrapper

            _simulationBucketerService.Initialize(SimulationBucketConstants.DefaultEntityCapacity, GlobalRegistry.DataVault);
            GlobalRegistry.RegisterSimulationBucketerRuntime(_simulationBucketerService);
            return _simulationBucketerService;
        }

        private static IJobAdmissionService EnsureJobAdmissionServiceRegistered()
        {
            IJobAdmissionService registered = GlobalRegistry.JobAdmission;
            if (registered != null)
            {
                if (_jobAdmissionTelemetryBridge == null)
                    _jobAdmissionTelemetryBridge = new JobAdmissionTelemetryBridge(); // COLD ALLOC: JobAdmissionTelemetryBridge[1] - scheduler telemetry bridge - owner: GameBootstrapper

                if (registered is BurstTokenBucketJobAdmissionService burstAdmissionService)
                    burstAdmissionService.Initialize(_jobAdmissionTelemetryBridge, GlobalRegistry.DataVault);
                else if (!registered.IsInitialized)
                {
                    registered.Initialize(_jobAdmissionTelemetryBridge);
                }

                JobAdmissionSchedulerBridge.SetService(registered);
                return registered;
            }

            if (_jobAdmissionTelemetryBridge == null)
                _jobAdmissionTelemetryBridge = new JobAdmissionTelemetryBridge(); // COLD ALLOC: JobAdmissionTelemetryBridge[1] - scheduler telemetry bridge - owner: GameBootstrapper

            if (_jobAdmissionService == null)
                _jobAdmissionService = new BurstTokenBucketJobAdmissionService(); // COLD ALLOC: BurstTokenBucketJobAdmissionService[1] - bootstrap-owned job admission gate - owner: GameBootstrapper

            _jobAdmissionService.Initialize(_jobAdmissionTelemetryBridge, GlobalRegistry.DataVault);
            GlobalRegistry.RegisterJobAdmissionRuntime(_jobAdmissionService);
            JobAdmissionSchedulerBridge.SetService(_jobAdmissionService);
            return _jobAdmissionService;
        }

        /// <summary>
        /// Wires the deferred caustics runtime through reflection-by-string.
        /// </summary>
        /// <remarks>
        /// Every lookup below is a string the compiler does not check, so renaming the type, renaming either
        /// method, or moving the type into its own asmdef (as the sibling <c>Rendering/*</c> folders already do)
        /// turns this whole function into a no-op. It previously returned <c>false</c> silently at three separate
        /// points into a discarded result, so a rename would have cost the player all caustics with nothing in the
        /// log. Each failure now names the exact broken string.
        /// Cold path: one call per boot from the OceanKinematics node.
        /// </remarks>
        private static bool TryEnsureDeferredCausticsRegistered()
        {
            if (GlobalRegistry.Caustics != null)
                return true;

            Type serviceType = Type.GetType("Hecton8.Rendering.AbyssalDeferredCausticsRuntime, Hecton8.Core", false) ??
                               Type.GetType("Hecton8.Rendering.AbyssalDeferredCausticsRuntime, Assembly-CSharp", false);
            if (serviceType == null)
                return LogDeferredCausticsWiringFailure(
                    "Type.GetType could not resolve 'Hecton8.Rendering.AbyssalDeferredCausticsRuntime' in either " +
                    "Hecton8.Core or Assembly-CSharp. The type was renamed, moved namespace, or moved into another " +
                    "assembly.");

            MethodInfo ensureMethod = serviceType.GetMethod("EnsureRuntimeInstance", BindingFlags.Public | BindingFlags.Static);
            if (ensureMethod == null)
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime resolved, but it has no public static 'EnsureRuntimeInstance' " +
                    "method. The factory was renamed or its signature changed.");

            Component serviceComponent;
            try
            {
                serviceComponent = ensureMethod.Invoke(null, null) as Component;
            }
            catch (Exception invokeException)
            {
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime.EnsureRuntimeInstance threw: " + invokeException);
            }

            if (serviceComponent == null)
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime.EnsureRuntimeInstance returned no Component, so no caustics " +
                    "runtime owner was created.");

            PersistRuntimeService(serviceComponent);
            MethodInfo initializeMethod = serviceType.GetMethod("InitializeService", BindingFlags.Public | BindingFlags.Instance);
            if (initializeMethod == null)
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime has no public instance 'InitializeService' method, so the " +
                    "created owner was never initialized.");

            try
            {
                initializeMethod.Invoke(serviceComponent, null);
            }
            catch (Exception invokeException)
            {
                return LogDeferredCausticsWiringFailure(
                    "AbyssalDeferredCausticsRuntime.InitializeService threw: " + invokeException);
            }
            if (GlobalRegistry.Caustics != null)
                return true;

            return LogDeferredCausticsWiringFailure(
                "AbyssalDeferredCausticsRuntime.InitializeService ran but GlobalRegistry.Caustics is still empty, " +
                "so the service never registered itself.");
        }

        /// <summary>
        /// Reports a broken link in the reflection-by-string caustics wiring.
        /// </summary>
        /// <returns>Always <c>false</c>, so callers can <c>return</c> this directly.</returns>
        private static bool LogDeferredCausticsWiringFailure(string reason)
        {
            RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
            RuntimeDiagnosticsTrace.WriteEvent("bootstrap.caustics.wiring.broken", reason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[GameBootstrapper] Deferred caustics wiring is broken, caustics will not render. " + reason);
#endif
            return false;
        }

        internal static void PersistRuntimeService(Component component)
        {
            if (!Application.isPlaying || component == null)
                return;

            GameBootstrapper bootstrapper = ActiveInstance;
            if (bootstrapper == null)
                return;

            Transform bootstrapTransform = bootstrapper.transform;
            Transform componentTransform = component.transform;
            if (componentTransform == bootstrapTransform)
                return;

            if (componentTransform.parent != bootstrapTransform)
                componentTransform.SetParent(bootstrapTransform, true);

            EnforceProjectPersistentRoot();
        }

        /// <summary>
        /// Draw order for the notification canvas. Below <c>HardwareErrorCanvas</c>'s overlay, which is a
        /// terminal BIOS-style failure screen and must never be covered by a transient warning.
        /// </summary>
        private const int HudNotificationSortingOrder = 4000;

        /// <summary>
        /// Constructs the player-facing notification surface if nothing has placed one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="HUDNotification"/> is the single surface runtime warnings reach the player through, and
        /// the project contained ZERO instances of it: its script guid
        /// <c>ff6d72424ae97784796abc35905d32bc</c> occurred in exactly one file in the whole tree — its own
        /// <c>.cs.meta</c>. Not a scene, not a prefab, not a runtime construction. Every consumer resolves it
        /// lazily and correctly via <see cref="HUDNotification.TryGetActive"/>, so every consumer was silently
        /// getting nothing: <c>EnvironmentalAnalyzerTool</c> at three sites,
        /// <c>PlayerRuntimeContextService.SyncRuntimeContextAndPublish</c>, and through that the
        /// <c>HudNotification</c> property of <c>PlayerRuntimeContext</c> and <c>PlayerSensoryManager</c>.
        /// The consumers were never the defect. Nothing built the object.
        /// </para>
        /// <para>
        /// The shape is a canvas ROOT with the notification on a CHILD, and every part of that is load-bearing
        /// rather than stylistic:
        /// </para>
        /// <list type="number">
        /// <item><description>
        /// <c>HUDNotification.EnsureBuilt</c> opens with <c>transform as RectTransform; if (self == null)
        /// return;</c> — a SILENT early return. Put it on a plain <c>new GameObject(...)</c>, which carries a
        /// plain <c>Transform</c>, and it publishes itself through <c>TryGetActive</c>, reports as present to
        /// every consumer, and builds no UI at all. That is worse than the absence it replaces, because an
        /// absent surface at least reads as absent.
        /// </description></item>
        /// <item><description>
        /// It builds its own <c>CanvasGroup</c>, <c>Image</c> and child <c>TextMeshProUGUI</c>, but never a
        /// <see cref="Canvas"/>. UGUI draws nothing without a Canvas ancestor, so one has to be supplied.
        /// </description></item>
        /// <item><description>
        /// It cannot share the GameObject with the Canvas, even though that would hand it a RectTransform for
        /// free: <c>EnsureBuilt</c> anchors itself top-centre at 420x36, which is an ELEMENT's geometry.
        /// Applying that to a canvas root fights the canvas system, which drives that RectTransform itself.
        /// </description></item>
        /// <item><description>
        /// <see cref="PersistRuntimeService"/> reparents its argument under the bootstrapper transform. Hand it
        /// the notification and the notification leaves the canvas and stops rendering — the same invisible
        /// failure by a different route. So the CANVAS is persisted and the notification rides along as its
        /// child. <c>ScreenSpaceOverlay</c> renders wherever the canvas is parented, which is why
        /// <c>HardwareErrorCanvas</c> persists itself the same way.
        /// </description></item>
        /// </list>
        /// <para>
        /// Idempotent through the same active-instance check every sibling Ensure method uses, so a scene that
        /// legitimately authors a notification surface later wins and no duplicate is built.
        /// </para>
        /// </remarks>
        internal static HUDNotification EnsureHudNotificationRegistered()
        {
            if (HUDNotification.TryGetActive(out HUDNotification registeredNotification))
                return registeredNotification;

            GameObject canvasRoot = new GameObject("[HUDNotificationCanvas]"); // COLD ALLOC: GameObject[1] - bootstrap-owned notification canvas root - owner: GameBootstrapper
            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = HudNotificationSortingOrder;
            canvasRoot.AddComponent<CanvasScaler>();

            GameObject notificationRoot = new GameObject("Notification"); // COLD ALLOC: GameObject[1] - notification element node under the bootstrap notification canvas - owner: GameBootstrapper
            notificationRoot.transform.SetParent(canvasRoot.transform, false);
            notificationRoot.AddComponent<RectTransform>();

            HUDNotification notification = notificationRoot.AddComponent<HUDNotification>();
            PersistRuntimeService(canvas);
            return notification;
        }

        private static GameTickManager EnsureGameTickManagerRegistered()
        {
            GameTickManager tickManager = GlobalRegistry.TickManager;
            if (tickManager == null)
                tickManager = GameTickManager.ActiveRuntimeInstance;

            if (tickManager == null)
            {
                GameObject runtimeRoot = new GameObject("[GameTickManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned tick manager root - owner: GameBootstrapper
                tickManager = runtimeRoot.AddComponent<GameTickManager>();
            }

            PersistRuntimeService(tickManager);

            tickManager.InitializeService();
            return tickManager;
        }

        private static SaveManager EnsureSaveServiceRegistered()
        {
            SaveManager saveManager = GlobalRegistry.Save as SaveManager;

            if (saveManager == null)
            {
                GameObject runtimeRoot = new GameObject("[SaveManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned save manager root - owner: GameBootstrapper
                saveManager = runtimeRoot.AddComponent<SaveManager>();
            }

            PersistRuntimeService(saveManager);

            saveManager.InitializeService();
            return saveManager;
        }

        private static bool IsSaveManagerUsable(SaveManager saveManager)
        {
            return saveManager != null && saveManager.IsInitialized;
        }

        private static ObjectPoolManager EnsureObjectPoolServiceRegistered()
        {
            ObjectPoolManager objectPoolManager = null;
            ObjectPoolManager.TryResolveActiveRuntime(ref objectPoolManager);

            if (objectPoolManager == null)
            {
                GameObject runtimeRoot = new GameObject("[ObjectPoolManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned object pool root - owner: GameBootstrapper
                objectPoolManager = runtimeRoot.AddComponent<ObjectPoolManager>();
            }

            PersistRuntimeService(objectPoolManager);

            objectPoolManager.InitializeService();
            return objectPoolManager;
        }

        private static ModWorldPersistenceManager EnsureModWorldPersistenceRegistered()
        {
            ModWorldPersistenceManager manager = GlobalRegistry.ModWorldPersistence;
            if (manager == null)
            {
                GameObject runtimeRoot = new GameObject("[ModWorldPersistenceManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned mod world persistence root - owner: GameBootstrapper
                manager = runtimeRoot.AddComponent<ModWorldPersistenceManager>();
            }

            PersistRuntimeService(manager);
            manager.InitializeService();
            return manager;
        }

        private static RenderDispatcher EnsureRenderDispatcherRegistered()
        {
            RenderDispatcher dispatcher = GlobalRegistry.RenderDispatcher;
            if (dispatcher == null)
                dispatcher = RenderDispatcher.ActiveRuntimeInstance;

            if (dispatcher == null)
            {
                GameObject runtimeRoot = new GameObject("[RenderDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned SRP render dispatcher root - owner: GameBootstrapper
                dispatcher = runtimeRoot.AddComponent<RenderDispatcher>();
            }

            PersistRuntimeService(dispatcher);
            dispatcher.InitializeService();
            return dispatcher;
        }

        private static EquipmentInteractionHandler EnsureEquipmentInteractionServiceRegistered()
        {
            if (GlobalRegistry.InteractionSignals is EquipmentInteractionHandler registeredHandler)
                return registeredHandler;

            EquipmentInteractionHandler interactionHandler = EquipmentInteractionHandler.ActiveRuntimeInstance;
            if (interactionHandler == null)
            {
                GameObject runtimeRoot = new GameObject("[EquipmentInteractionHandler]"); // COLD ALLOC: GameObject[1] - bootstrap-owned interaction signal root - owner: GameBootstrapper
                interactionHandler = runtimeRoot.AddComponent<EquipmentInteractionHandler>();
            }

            interactionHandler.InitializeService();
            return interactionHandler;
        }

        private static Hecton8.Equipment.Auxiliary.AuxiliaryEquipmentRouterRuntime EnsureAuxiliaryEquipmentRouterRegistered()
        {
            if (Hecton8.Equipment.Auxiliary.AuxiliaryEquipmentRouterRuntime.TryGetActiveRuntime(
                    out Hecton8.Equipment.Auxiliary.AuxiliaryEquipmentRouterRuntime registered))
            {
                registered.InitializeService(GlobalRegistry.DataVault);
                return registered;
            }

            GameObject runtimeRoot = new GameObject("[AuxiliaryEquipmentRouterRuntime]"); // COLD ALLOC: GameObject[1] - bootstrap-owned auxiliary equipment router root - owner: GameBootstrapper
            Hecton8.Equipment.Auxiliary.AuxiliaryEquipmentRouterRuntime runtime =
                runtimeRoot.AddComponent<Hecton8.Equipment.Auxiliary.AuxiliaryEquipmentRouterRuntime>();
            runtime.InitializeService(GlobalRegistry.DataVault);
            PersistRuntimeService(runtime);
            return runtime;
        }

        private static CrashTelemetryBuffer EnsureCrashTelemetryBufferRegistered()
        {
            // Resolve-or-create is owned by CrashTelemetryBuffer.EnsureRuntimeInstance
            // (GlobalRegistry.CrashTelemetry + scene scan + player-build AddComponent).
            // Bootstrap no longer duplicates the construction path.
            CrashTelemetryBuffer telemetry = CrashTelemetryBuffer.EnsureRuntimeInstance();
            if (telemetry == null)
                return null;

            if (Application.isPlaying)
                PersistRuntimeService(telemetry);

            BootstrapStatus.RegisterSafeHaltTelemetryReporter(CrashTelemetryBuffer.ReportBootstrapSafeHalt);
            return telemetry;
        }


        private static Hecton8.Core.RuntimeWatchdog EnsureRuntimeWatchdogRegistered()
        {
            // Resolve-or-create is owned by RuntimeWatchdog.EnsureRuntimeInstance
            // (GlobalRegistry.RuntimeWatchdog + player-build AddComponent + InitializeService).
            // Bootstrap no longer duplicates the construction path.
            Hecton8.Core.RuntimeWatchdog watchdog = Hecton8.Core.RuntimeWatchdog.EnsureRuntimeInstance();
            if (watchdog == null)
                return null;

            PersistRuntimeService(watchdog);
            watchdog.InitializeService();
            return watchdog;
        }


        private static Hecton8.Core.GCMonitor EnsureGCMonitorRegistered()
        {
            // Resolve-or-create is owned by GCMonitor.EnsureRuntimeInstance
            // (GlobalRegistry.GCMonitorRuntime + player-build AddComponent).
            // Bootstrap no longer duplicates the construction path.
            Hecton8.Core.GCMonitor monitor = Hecton8.Core.GCMonitor.EnsureRuntimeInstance();
            if (monitor == null)
                return null;

            PersistRuntimeService(monitor);
            monitor.InitializeService();
            return monitor;
        }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static RuntimePerformanceProfiler EnsureRuntimePerformanceProfilerRegistered()
        {
            RuntimePerformanceProfiler profiler = RuntimePerformanceProfiler.ActiveRuntime;
            bool activateAfterConfigure = false;
            if (profiler == null)
            {
                GameObject runtimeRoot = new GameObject(RuntimePerformanceProfilerRuntimeName); // COLD ALLOC: GameObject[1] - development performance profiler root - owner: GameBootstrapper
                runtimeRoot.SetActive(false);
                profiler = runtimeRoot.AddComponent<RuntimePerformanceProfiler>();
                activateAfterConfigure = true;
            }

            profiler.ConfigureForDevRun(
                autoStartOnEnable: true,
                enableBudgetViolationLogging: true,
                enableWindowLogging: false,
                autoStartNewGame: false,
                sampleWindow: 2f);

            PersistRuntimeService(profiler);
            if (activateAfterConfigure)
                profiler.gameObject.SetActive(true);

            return profiler;
        }
#endif

        private static PrefabRegistry EnsurePrefabRegistry()
        {
            // Resolve-or-create is owned by PrefabRegistry.EnsureRuntimeInstance
            // (active runtime slot + player-build AddComponent).
            // Bootstrap no longer duplicates the construction path.
            PrefabRegistry registry = PrefabRegistry.EnsureRuntimeInstance();
            if (registry == null)
                return null;

            PersistRuntimeService(registry);
            return registry;
        }

        private static PersistentWorldRegistry EnsurePersistentWorldRegistry()
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null)
                return registry;

            GameObject runtimeRoot = new GameObject(PersistentWorldRegistryRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-owned persistent world registry fallback - owner: GameBootstrapper
            PersistentWorldRegistry createdRegistry = runtimeRoot.AddComponent<PersistentWorldRegistry>();
            PersistRuntimeService(createdRegistry);
            return createdRegistry;
        }

        private static HectonFloatingOrigin EnsureFloatingOriginRegistered()
        {
            HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
            if (origin == null)
                origin = HectonFloatingOrigin.EnsureRuntimeInstance();

            PersistRuntimeService(origin);

            origin.InitializeService();
            return origin;
        }

        private static ConnectionSplineBatchRenderer EnsureConnectionSplineBatchRendererRegistered()
        {
            ConnectionSplineBatchRenderer renderer = GlobalRegistry.ConnectionSplineBatchRenderer;
            if (renderer == null)
            {
                GameObject runtimeRoot = new GameObject("[ConnectionSplineBatchRenderer]"); // COLD ALLOC: GameObject[1] - bootstrap-owned shader-bent connection renderer root - owner: GameBootstrapper
                renderer = runtimeRoot.AddComponent<ConnectionSplineBatchRenderer>();
            }

            PersistRuntimeService(renderer);
            renderer.InitializeService();
            return renderer;
        }

        private static BeaconNetworkSystem EnsureBeaconNetworkServiceRegistered()
        {
            BeaconNetworkSystem beaconNetwork = GlobalRegistry.BeaconNetwork;
            if (beaconNetwork == null)
            {
                GameObject runtimeRoot = new GameObject("[BeaconNetworkSystem]"); // COLD ALLOC: GameObject[1] - bootstrap-owned beacon network root - owner: GameBootstrapper
                beaconNetwork = runtimeRoot.AddComponent<BeaconNetworkSystem>();
            }

            PersistRuntimeService(beaconNetwork);
            if (!ReferenceEquals(GlobalRegistry.BeaconNetwork, beaconNetwork))
                GlobalRegistry.RegisterBeaconNetworkRuntime(beaconNetwork);

            return beaconNetwork;
        }

        private static GlobalPhysicsStateManager EnsureGlobalPhysicsStateManagerRegistered()
        {
            GlobalPhysicsStateManager manager = GlobalRegistry.PhysicsStateManager;

            if (manager == null)
            {
                GameObject runtimeRoot = new GameObject("[GlobalPhysicsStateManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned global physics-state manager root - owner: GameBootstrapper
                manager = runtimeRoot.AddComponent<GlobalPhysicsStateManager>();
            }

            PersistRuntimeService(manager);
            manager.InitializeService();
            return manager;
        }

        private static PhysicsApplySystem EnsurePhysicsApplySystemRegistered()
        {
            // Resolve-or-create is owned by PhysicsApplySystem.EnsureRuntimeInstance
            // (static slot + GlobalRegistry.Physics + scene scan + player-build AddComponent).
            // Bootstrap no longer duplicates the construction path.
            PhysicsApplySystem physicsApplySystem = PhysicsApplySystem.EnsureRuntimeInstance();
            if (physicsApplySystem == null)
                return null;

            PersistRuntimeService(physicsApplySystem);
            physicsApplySystem.InitializeService();
            return physicsApplySystem;
        }


        private static EcosystemDirector EnsureEcosystemDirectorRegistered()
        {
            EcosystemDirector director = null;
            WorldRuntimeReferenceUtility.TryResolveEcosystemDirector(ref director);
            if (director == null)
            {
                GameObject runtimeRoot = new GameObject("[EcosystemDirector]"); // COLD ALLOC: GameObject[1] - bootstrap-owned data-only ecosystem simulation owner - owner: GameBootstrapper
                director = runtimeRoot.AddComponent<EcosystemDirector>();
            }

            PersistRuntimeService(director);

            director.InitializeService();
            return director;
        }

        private static bool EnsureFaunaSimulationRegistered()
        {
            IFaunaSim registeredFaunaSimulation = GlobalRegistry.FaunaSimulation;
            if (registeredFaunaSimulation != null && registeredFaunaSimulation.IsReady)
                return true;

            FaunaDirector faunaDirector = null;
            WorldRuntimeReferenceUtility.TryResolveFaunaDirector(ref faunaDirector);
            if (faunaDirector != null)
                faunaDirector.InitializeService();

            registeredFaunaSimulation = GlobalRegistry.FaunaSimulation;
            if (registeredFaunaSimulation != null)
                return registeredFaunaSimulation.IsReady;

            GlobalRegistry.RegisterFaunaSimulationService(DemiurgeFaunaSimulationService.Shared);
            registeredFaunaSimulation = GlobalRegistry.FaunaSimulation;
            return registeredFaunaSimulation != null && registeredFaunaSimulation.IsReady;
        }

        private static InputDispatcher EnsureInputDispatcherRegistered()
        {
            if (GlobalRegistry.RegisteredInput is InputDispatcher registeredDispatcher)
            {
                registeredDispatcher.BindNativeInputManager(_bootstrapInputManager);
                return registeredDispatcher;
            }

            InputDispatcher dispatcher = null;
            if (!InputDispatcher.TryResolveActiveRuntime(ref dispatcher))
            {
                GameObject runtimeRoot = new GameObject("[InputDispatcher]"); // COLD ALLOC: GameObject[1] - bootstrap-owned input dispatcher root - owner: GameBootstrapper
                dispatcher = runtimeRoot.AddComponent<InputDispatcher>();
            }

            dispatcher.BindNativeInputManager(_bootstrapInputManager);
            dispatcher.InitializeService();
            return dispatcher;
        }

        private static InputManager EnsureNativeInputManagerRegistered()
        {
            InputManager registeredInputManager = ResolveRegisteredNativeInputManager();
            if (_bootstrapInputManager == null)
                _bootstrapInputManager = registeredInputManager;

            if (_bootstrapInputManager == null)
                return null;

            if (!ReferenceEquals(registeredInputManager, _bootstrapInputManager))
                GlobalRegistry.RegisterNativeInputManagerRuntime(_bootstrapInputManager);

            PersistRuntimeService(_bootstrapInputManager);
            return _bootstrapInputManager;
        }

        private static InputManager ResolveRegisteredNativeInputManager()
        {
            return InputManager.ActiveRuntimeInstance;
        }

        private static PowerGridManager EnsurePowerGridServiceRegistered()
        {
            if (GlobalRegistry.PowerGrid is PowerGridManager registeredPowerGrid)
                return registeredPowerGrid;

            PowerGridManager powerGridManager = PowerGridManager.ActiveRuntimeInstance;
            if (powerGridManager == null)
            {
                GameObject runtimeRoot = new GameObject("[PowerGridManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned power grid runtime root - owner: GameBootstrapper
                powerGridManager = runtimeRoot.AddComponent<PowerGridManager>();
            }

            powerGridManager.InitializeService();
            return powerGridManager;
        }

        private static ConstructionManager EnsureConstructionServiceRegistered()
        {
            if (GlobalRegistry.Logistics is ConstructionManager registeredConstruction)
            {
                EnsureInternalFloodWaterlineRuntimeRegistered();
                return registeredConstruction;
            }

            ConstructionManager constructionManager = ConstructionManager.ActiveRuntimeInstance;
            if (constructionManager == null)
            {
                GameObject runtimeRoot = new GameObject("[ConstructionManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned construction/logistics service root - owner: GameBootstrapper
                constructionManager = runtimeRoot.AddComponent<ConstructionManager>();
            }

            PersistRuntimeService(constructionManager);
            constructionManager.InitializeService();
            EnsureInternalFloodWaterlineRuntimeRegistered();
            return constructionManager;
        }

        private static WorldStateManager EnsureWorldStateServiceRegistered()
        {
            WorldStateManager registeredWorldState = GlobalRegistry.WorldState;
            if (registeredWorldState != null)
                return registeredWorldState;

            GameObject runtimeRoot = new GameObject("[WorldStateManager]"); // COLD ALLOC: GameObject[1] - bootstrap-owned world-state persistence root - owner: GameBootstrapper
            WorldStateManager worldStateManager = runtimeRoot.AddComponent<WorldStateManager>();
            PersistRuntimeService(worldStateManager);
            return worldStateManager;
        }

        private static InternalFloodWaterlineRuntime EnsureInternalFloodWaterlineRuntimeRegistered()
        {
            InternalFloodWaterlineRuntime runtime = InternalFloodWaterlineRuntime.EnsureRuntimeInstance();
            PersistRuntimeService(runtime);
            runtime.InitializeService();
            return runtime;
        }

        private static SpatialAudioManager EnsureAudioServiceRegistered()
        {
            if (GlobalRegistry.Audio is SpatialAudioManager registeredAudioService)
                return registeredAudioService;

            SpatialAudioManager sceneAudioService = ResolveAuthoredSpatialAudioManager();
            if (sceneAudioService != null)
            {
                PersistRuntimeService(sceneAudioService);
                return sceneAudioService;
            }

            // Prefab PFB_SpatialAudioManagerRoot exists but is not parented under GameBootstrapper
            // in player builds; authored child walk returns null and the node used to fall through
            // to NoOpAudio. Factory constructs the sole Audio owner so InitializeService can run.
            SpatialAudioManager constructed = SpatialAudioManager.EnsureRuntimeInstance();
            if (constructed != null)
            {
                PersistRuntimeService(constructed);
                return constructed;
            }

            return null;
        }


        private static SpatialAudioManager ResolveAuthoredSpatialAudioManager()
        {
            SpatialAudioManager activeAudioService = SpatialAudioManager.ActiveRuntimeInstance;
            if (activeAudioService != null)
                return activeAudioService;

            GameBootstrapper bootstrapper = ActiveInstance;
            if (bootstrapper == null)
                return null;

            Transform root = bootstrapper.transform;
            _bootstrapTransformScratch.Clear();
            _bootstrapTransformScratch.Add(root);

            while (_bootstrapTransformScratch.Count > 0)
            {
                int lastIndex = _bootstrapTransformScratch.Count - 1;
                Transform current = _bootstrapTransformScratch[lastIndex];
                _bootstrapTransformScratch.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                if (current.TryGetComponent(out SpatialAudioManager spatialAudioManager) &&
                    spatialAudioManager != null)
                {
                    _bootstrapTransformScratch.Clear();
                    return spatialAudioManager;
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    _bootstrapTransformScratch.Add(current.GetChild(i));
            }

            _bootstrapTransformScratch.Clear();
            return null;
        }

        private static bool InitializeSpatialAudioBootstrapNode()
        {
            long serviceStartTimestamp = Stopwatch.GetTimestamp();
            try
            {
                SpatialAudioManager spatialAudioManager = EnsureAudioServiceRegistered();
                if (spatialAudioManager == null)
                    return TryRegisterNoOpAudioFallback("SpatialAudioManager missing", null);

                spatialAudioManager.InitializeService();
                long elapsedMilliseconds =
                    (Stopwatch.GetTimestamp() - serviceStartTimestamp) * 1000L / Stopwatch.Frequency;
                if (elapsedMilliseconds > OptionalServiceTimeoutMilliseconds)
                    LogOptionalBootstrapWarning("SpatialAudioManager exceeded the optional-service bootstrap budget.");

                IAudioService initializedAudioService = GlobalRegistry.Audio;
                if (IsBootstrapAudioServiceUsable(initializedAudioService))
                {
                    // A real owner holds the slot and is runtime-ready. Drop any exemption from a previous attempt so
                    // it can never mask a later regression.
                    _audioBootstrapNodeStubbed = false;
                    return true;
                }

                // The old single message here said "did not register IAudioService" for all three of these states,
                // which is false in two of them and sent the reader hunting a registration bug that did not exist.
                // IsBootstrapAudioServiceUsable failing does NOT imply registration failed: SpatialAudioManager
                // registers itself inside InitializeService and then gates IsAudioRuntimeReady on a five-term
                // conjunction (slot ownership of BOTH the audio and virtualization slots, isActiveAndEnabled,
                // IsInitialized, and IsVirtualizationReady - which is itself six vault-backed buffer handles).
                // Any one of those can be the live cause and they demand different fixes, so name which state we are
                // actually in.
                string usabilityCause;
                if (initializedAudioService == null)
                    usabilityCause = "SpatialAudioManager left the IAudioService slot empty";
                else if (!ReferenceEquals(initializedAudioService, spatialAudioManager))
                    usabilityCause = "IAudioService slot holds a different owner than the initialized SpatialAudioManager";
                else if (!initializedAudioService.IsInitialized)
                    usabilityCause = "SpatialAudioManager registered but IsInitialized is false (runtime owner aborted or init returned early)";
                else if (!initializedAudioService.IsAudioRuntimeReady)
                    usabilityCause = "SpatialAudioManager registered and initialized but IsAudioRuntimeReady is false (check slot co-ownership of IAudioVirtualizationService and the vault-backed IsVirtualizationReady buffers)";
                else
                    usabilityCause = "SpatialAudioManager reports ready but its Behaviour is not active and enabled";

                return TryRegisterNoOpAudioFallback(usabilityCause, null);
            }
            catch (Exception exception)
            {
                return TryRegisterNoOpAudioFallback("SpatialAudioManager init exception", exception);
            }
        }

        /// <summary>
        /// Installs <see cref="NoOpAudioService"/> in the <c>IAudioService</c> slot, records the substitution loudly
        /// once with its cause, and reports that boot may proceed past the audio node.
        /// </summary>
        /// <param name="reason">
        /// Which of the three distinct causes fired. This parameter used to be accepted and never read - all three
        /// causes collapsed into one message, and the three demand opposite fixes (author the component, fix its
        /// registration, fix the throw). It is now in the record.
        /// </param>
        /// <param name="exception">
        /// The exception that aborted audio init, or <c>null</c> when the cause was not a throw. Previously
        /// destroyed by a <c>catch (Exception)</c> that bound no variable.
        /// </param>
        /// <returns>
        /// Always <c>true</c>. The return value answers "may boot continue", not "is audio ready" - those were the
        /// same bit while the stub claimed readiness, and conflating them is what hid this for a whole session.
        /// Audio readiness is now answered honestly (and negatively) by
        /// <see cref="IsBootstrapAudioServiceUsable"/>; boot survival is answered here.
        /// </returns>
        private static bool TryRegisterNoOpAudioFallback(string reason, Exception exception)
        {
            IAudioService audioService = GlobalRegistry.Audio;
            if (ReferenceEquals(audioService, NoOpAudioService.Shared))
            {
                // Already recorded this boot. Do not log again; keep the exemption asserted.
                _audioBootstrapNodeStubbed = true;
                return true;
            }

            if (audioService == null)
                GlobalRegistry.RegisterAudioService(NoOpAudioService.Shared);
            else
                GlobalRegistry.ReplaceAudioServiceForBootstrap(NoOpAudioService.Shared);

            // Assigned, not OR-ed, and only after the slot swap actually happened.
            _audioBootstrapNodeStubbed = ReferenceEquals(GlobalRegistry.Audio, NoOpAudioService.Shared);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogOptionalBootstrapWarning("Injected NoOp audio service.");
#endif
            ReportAudioBootstrapStubInstalled(reason, exception);

            // Deliberately NOT IsBootstrapAudioServiceUsable(GlobalRegistry.Audio) when the stub is in the slot: that
            // predicate is now correctly false for the stub, and returning false here would fail the Environment
            // phase and kill the entire boot over an optional subsystem.
            if (_audioBootstrapNodeStubbed)
                return true;

            // The registry refused the swap, so the exemption does not apply and the slot still holds whatever was
            // there before. Answer honestly about that owner rather than inheriting the stub's verdict.
            return IsBootstrapAudioServiceUsable(GlobalRegistry.Audio);
        }

        /// <summary>
        /// Writes the one loud, named, player-reachable record that the audio subsystem is a placeholder.
        /// </summary>
        /// <remarks>
        /// The <see cref="RuntimeDiagnosticsTrace"/> write is deliberately outside the editor/development guard.
        /// The previous message went only through <see cref="LogOptionalBootstrapWarning"/>, which is
        /// <c>[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]</c> and was additionally wrapped in
        /// <c>#if</c> at its call site - so in a release player the whole audio subsystem could be replaced by a
        /// stub and not one byte was logged. Cold path: runs at most once per boot from the node initializer.
        /// </remarks>
        private static void ReportAudioBootstrapStubInstalled(string reason, Exception exception)
        {
            string cause = string.IsNullOrEmpty(reason) ? "unspecified" : reason;

            RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
            RuntimeDiagnosticsTrace.WriteEvent("bootstrap.audio.stub_installed", cause);
            if (exception != null)
            {
                RuntimeDiagnosticsTrace.WriteEvent(
                    "bootstrap.audio.stub_installed.exception",
                    exception.GetType().Name + ": " + exception.Message);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError(
                "[GameBootstrapper] AUDIO IS A STUB: IAudioService slot holds NoOpAudioService, which discards " +
                "every queued event. No SFX, no ambience, no music, no vocal warnings, no acoustic zones for this " +
                "entire session. cause=" + cause + " This node is EXEMPT, not ready: the stub reports " +
                "IsInitialized=false and IsAudioRuntimeReady=false, so consumers that gate on either will now " +
                "correctly refuse instead of queueing into silence, and boot continues on purpose because audio " +
                "is optional and failing the node would abort the whole session. ACTION: fix the SpatialAudioManager " +
                "node for the cause named above - author the missing component, fix its IAudioService/" +
                "IAudioVirtualizationService registration and IsAudioRuntimeReady conjunction, or fix the throw " +
                "reported next.");
            if (exception != null)
            {
                Debug.LogError(
                    "[GameBootstrapper] SpatialAudioManager.InitializeService threw " +
                    exception.GetType().Name + ": " + exception.Message + " - audio was replaced by a silent stub. " +
                    exception.StackTrace);
            }
#endif
        }

        private static bool IsBootstrapAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogOptionalBootstrapWarning(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[GameBootstrapper] {message}");
#endif
        }

        private static void LogBootstrapDependencyGraphFailure(BootstrapPhase phase)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Bootstrap dependency graph invalid. phase={phase}");
#endif
        }

        private static void LogBootstrapDependencyFailure(BootstrapPhase phase, BootstrapDependencyNode node)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Bootstrap dependency failed. phase={phase} node={node}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogBootstrapCoreServicesSubstepFailure(string substep)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[GameBootstrapper] CoreServices substep failed. substep=" + substep);
#endif
        }

        private static void LogBootstrapHeartbeatFailure(
            BootstrapDependencyNode node,
            int waitFrames,
            double elapsedSeconds)
        {
            RuntimeDiagnosticsTrace.EnsureSession("bootstrap_blackbox");
            RuntimeDiagnosticsTrace.WriteEvent("bootstrap.heartbeat.timeout", ResolveBootstrapDependencyNodeName(node));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Service heartbeat timeout. node={node} frames={waitFrames} elapsed={elapsedSeconds:0.000}s");
#endif
        }

        private static void TriggerServiceEmergencyReset(BootstrapDependencyNode node)
        {
            object service = ResolveBootstrapDependencyService(node);
            if (service is RuntimeWatchdog.IEmergencyResetTarget resetTarget)
                resetTarget.ServiceEmergencyReset();
        }

        private static void LogBootstrapPhaseFailure(BootstrapPhase phase)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[GameBootstrapper] Bootstrap phase failed. phase={phase}");
#endif
        }

        private static bool IsHeadlessBootRequested()
        {
            return HasCommandLineArg(HectonHeadlessCommandLineArg) || HasCommandLineArg(HeadlessCommandLineArg);
        }

        private static bool HasCommandLineArg(string commandLineArg)
        {
            if (string.IsNullOrEmpty(commandLineArg))
                return false;

            string[] args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], commandLineArg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void ValidateOceanKinematicsPluginContract()
        {
            Type oceanKinematicsContract = typeof(IOceanKinematics);
            bool foundProvider = false;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                AssemblyName assemblyName = assembly.GetName();
                if (assemblyName == null ||
                    (!string.Equals(assemblyName.Name, PluginsAssemblyName, StringComparison.Ordinal) &&
                     !string.Equals(assemblyName.Name, CrestBridgeAssemblyName, StringComparison.Ordinal)))
                {
                    continue;
                }

                Type[] pluginTypes;
                try
                {
                    pluginTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    pluginTypes = exception.Types;
                }

                if (pluginTypes == null)
                    continue;

                for (int typeIndex = 0; typeIndex < pluginTypes.Length; typeIndex++)
                {
                    Type pluginType = pluginTypes[typeIndex];
                    if (pluginType == null ||
                        pluginType.IsInterface ||
                        pluginType.IsAbstract ||
                        !oceanKinematicsContract.IsAssignableFrom(pluginType))
                    {
                        continue;
                    }

                    foundProvider = true;
                    break;
                }

                if (foundProvider)
                    return;
            }

            throw new InvalidOperationException("[GameBootstrapper] No Hecton8.Plugins or Hecton8.Crest.Bridge IOceanKinematics implementation found. World load is blocked.");
        }

        private static global::Hecton8.Core.HectonHardwareProfile CaptureHardwareProfile()
        {
            global::Hecton8.Optimization.HardwareProfiler.HardwareProfilerSnapshot snapshot =
                global::Hecton8.Optimization.HardwareProfiler.CaptureSystemInfoSnapshot();
            int graphicsMemoryMb = snapshot.GraphicsMemoryMegabytes;
            int systemMemoryMb = snapshot.SystemMemoryMegabytes;
            int processorCount = snapshot.ProcessorCount;
            double biosPhysicsMillisecondsPerStep = global::Hecton8.Optimization.HardwareProfiler.RunBiosPhysicsBenchmarkMillisecondsPerStep();
            float startupQualityWeight01 = global::Hecton8.Optimization.HardwareProfiler.ResolveStartupQualityWeight01(
                in snapshot,
                biosPhysicsMillisecondsPerStep);
            global::Hecton8.Core.HectonQualityTier qualityTier = ResolveBenchmarkScalabilityTier(
                graphicsMemoryMb,
                systemMemoryMb,
                processorCount,
                startupQualityWeight01);
            global::Hecton8.Core.MathPrecisionLevel mathPrecisionLevel = ResolveMathPrecisionLevel(
                graphicsMemoryMb,
                systemMemoryMb,
                processorCount,
                startupQualityWeight01);
            int startupHardwareScore = math.clamp((int)math.round(startupQualityWeight01 * 100f), 0, 100);

            return new global::Hecton8.Core.HectonHardwareProfile(
                graphicsMemoryMb,
                systemMemoryMb,
                processorCount,
                qualityTier,
                biosPhysicsMillisecondsPerStep,
                startupHardwareScore,
                mathPrecisionLevel);
        }

        private static global::Hecton8.Core.HectonQualityTier ResolveBenchmarkScalabilityTier(
            int graphicsMemoryMb,
            int systemMemoryMb,
            int processorCount,
            float startupQualityWeight01)
        {
            float q = math.saturate(startupQualityWeight01);
            if (graphicsMemoryMb < SuspiciousGraphicsMemoryFallbackThresholdMb ||
                systemMemoryMb < 7000 ||
                q < 0.18f)
            {
                return global::Hecton8.Core.HectonQualityTier.Low;
            }

            if (q < 0.38f)
                return global::Hecton8.Core.HectonQualityTier.CompactPc;

            if (q < 0.62f)
                return global::Hecton8.Core.HectonQualityTier.Mid;

            return q >= 0.88f && processorCount > UltraTierProcessorCount && systemMemoryMb >= 32000
                ? global::Hecton8.Core.HectonQualityTier.Ultra
                : global::Hecton8.Core.HectonQualityTier.High;
        }

        private static global::Hecton8.Core.MathPrecisionLevel ResolveMathPrecisionLevel(
            int graphicsMemoryMb,
            int systemMemoryMb,
            int processorCount,
            float startupQualityWeight01)
        {
            float q = math.saturate(startupQualityWeight01);
            if (q < 0.35f)
                return global::Hecton8.Core.MathPrecisionLevel.Low;

            if (q >= 0.70f)
                return global::Hecton8.Core.MathPrecisionLevel.High;

            return graphicsMemoryMb >= 4200 && systemMemoryMb >= 12000 && processorCount > 4
                ? global::Hecton8.Core.MathPrecisionLevel.High
                : global::Hecton8.Core.MathPrecisionLevel.Low;
        }

        private static void ApplyScalabilityMatrix(in HectonHardwareProfile hardwareProfile)
        {
            HardwareTierDetector.EnsureInitialized();
            ApplyUnityQualityEnvelope(in hardwareProfile);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = ResolveTargetFrameRate(in hardwareProfile);
            QualitySettings.maximumLODLevel = ResolveMaximumLodLevel(in hardwareProfile);
            QualitySettings.streamingMipmapsMemoryBudget = ResolveStreamingMipBudgetMb(in hardwareProfile);
            QualitySettings.asyncUploadBufferSize = ResolveAsyncUploadBufferSizeMb(in hardwareProfile);
            QualitySettings.asyncUploadTimeSlice = ResolveAsyncUploadTimeSliceMs(in hardwareProfile);
            QualitySettings.asyncUploadPersistentBuffer = true;
            ConfigureJobWorkerThreads(in hardwareProfile);
        }

        private static void ApplyUnityQualityEnvelope(in HectonHardwareProfile hardwareProfile)
        {
            int qualityIndex = ResolveUnityQualityIndex(in hardwareProfile);
            string[] qualityNames = QualitySettings.names;
            int qualityCount = qualityNames != null ? qualityNames.Length : 0;
            if (qualityIndex < 0 || qualityIndex >= qualityCount)
                return;

            if (QualitySettings.GetQualityLevel() == qualityIndex)
                return;

            QualitySettings.SetQualityLevel(qualityIndex, true);
        }

        private static int ResolveUnityQualityIndex(in HectonHardwareProfile hardwareProfile)
        {
            if (Application.platform == RuntimePlatform.Android || HardwareTierDetector.IsQuest3Like)
                return FindUnityQualityIndex(QuestVrQualityName, QuestVrQualityIndex);

            if (HardwareTierDetector.IsSteamDeckLike)
                return FindUnityQualityIndex(HandheldUmaQualityName, HandheldUmaQualityIndex);

            if (HardwareTierDetector.SharedMemoryModeActive &&
                (int)hardwareProfile.QualityTier <= (int)HectonQualityTier.Mid)
            {
                return FindUnityQualityIndex(HandheldUmaQualityName, HandheldUmaQualityIndex);
            }

            float qualityWeight = ResolveBootQualityWeight01(in hardwareProfile);
            if (hardwareProfile.GraphicsMemoryMegabytes < SuspiciousGraphicsMemoryFallbackThresholdMb ||
                hardwareProfile.SystemMemoryMegabytes < 7000 ||
                qualityWeight < 0.18f)
            {
                return FindUnityQualityIndex(AbyssLowQualityName, AbyssLowQualityIndex);
            }

            if (qualityWeight < 0.38f)
                return FindUnityQualityIndex(CompactPcQualityName, CompactPcQualityIndex);

            if (qualityWeight < 0.62f)
                return FindUnityQualityIndex(SurfaceMediumQualityName, SurfaceMediumQualityIndex);

            if (qualityWeight >= 0.88f &&
                hardwareProfile.ProcessorCount > UltraTierProcessorCount &&
                hardwareProfile.SystemMemoryMegabytes >= 32000)
            {
                return FindUnityQualityIndex(LeviathanUltraQualityName, LeviathanUltraQualityIndex);
            }

            return FindUnityQualityIndex(OrbitHighQualityName, OrbitHighQualityIndex);
        }

        private static int FindUnityQualityIndex(string qualityName, int fallbackIndex)
        {
            string[] names = QualitySettings.names;
            if (names == null || names.Length == 0)
                return -1;

            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], qualityName, StringComparison.Ordinal))
                    return i;
            }

            return fallbackIndex >= 0 && fallbackIndex < names.Length
                ? fallbackIndex
                : QualitySettings.GetQualityLevel();
        }

        private static void DisableGarbageCollectorAfterCoreReady()
        {
#if UNITY_EDITOR
            return;
#else
            if (UnityEngine.Scripting.GarbageCollector.GCMode == UnityEngine.Scripting.GarbageCollector.Mode.Disabled)
                return;

            UnityEngine.Scripting.GarbageCollector.GCMode = UnityEngine.Scripting.GarbageCollector.Mode.Disabled;
#endif
        }

        private static int ResolveTargetFrameRate(in HectonHardwareProfile hardwareProfile)
        {
            if (HardwareTierDetector.IsQuest3Like)
                return HardwareProfileCatalog.Quest3TargetFps;
            if (HardwareTierDetector.IsSteamDeckLike)
                return HardwareProfileCatalog.SteamDeckLcdTargetFps;

            return DefaultTargetFrameRate;
        }

        private static int ResolveMaximumLodLevel(in HectonHardwareProfile hardwareProfile)
        {
            float qualityWeight = ResolveBootQualityWeight01(in hardwareProfile);
            return math.clamp(2 - (int)math.floor(qualityWeight * 4f + 0.0001f), 0, 2);
        }

        private static float ResolveStreamingMipBudgetMb(in HectonHardwareProfile hardwareProfile)
        {
            if (HardwareTierDetector.IsQuest3Like)
                return HardwareProfileCatalog.Quest3TextureBudgetMegabytes;
            if (HardwareTierDetector.IsSteamDeckLike)
                return HardwareProfileCatalog.SteamDeckLcdTextureBudgetMegabytes;

            if (HardwareTierDetector.SharedMemoryModeActive)
            {
                return math.clamp(
                    HardwareTierDetector.RecommendedVramBudgetMegabytes * 0.5f,
                    512f,
                    1536f);
            }

            return ResolveBootQualityCurve(
                ResolveBootQualityWeight01(in hardwareProfile),
                512f,
                768f,
                1024f,
                1536f,
                2048f);
        }

        private static int ResolveAsyncUploadBufferSizeMb(in HectonHardwareProfile hardwareProfile)
        {
            float budget = ResolveBootQualityCurve(
                ResolveBootQualityWeight01(in hardwareProfile),
                SurvivalAsyncUploadBufferMb,
                SurvivalAsyncUploadBufferMb,
                MidTierAsyncUploadBufferMb,
                HighTierAsyncUploadBufferMb,
                HighTierAsyncUploadBufferMb);
            return math.max(1, (int)math.round(budget));
        }

        private static int ResolveAsyncUploadTimeSliceMs(in HectonHardwareProfile hardwareProfile)
        {
            float budget = ResolveBootQualityCurve(
                ResolveBootQualityWeight01(in hardwareProfile),
                SurvivalAsyncUploadTimeSliceMs,
                SurvivalAsyncUploadTimeSliceMs,
                MidTierAsyncUploadTimeSliceMs,
                HighTierAsyncUploadTimeSliceMs,
                HighTierAsyncUploadTimeSliceMs);
            return math.max(1, (int)math.round(budget));
        }

        private static float ResolveBootQualityWeight01(in HectonHardwareProfile hardwareProfile)
        {
            if (hardwareProfile.HardwareScore > 0)
                return math.saturate(hardwareProfile.HardwareScore * 0.01f);

            int tierIndex = (int)hardwareProfile.QualityTier - (int)HectonQualityTier.Low;
            return math.saturate(tierIndex * 0.25f);
        }

        private static float ResolveBootQualityCurve(
            float qualityWeight,
            float low,
            float compact,
            float middle,
            float high,
            float ultra)
        {
            float q = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 0f);
            return low +
                (compact - low) * math.smoothstep(0f, 0.25f, q) +
                (middle - compact) * math.smoothstep(0.25f, 0.5f, q) +
                (high - middle) * math.smoothstep(0.5f, 0.75f, q) +
                (ultra - high) * math.smoothstep(0.75f, 1f, q);
        }

        private static void ConfigureJobWorkerThreads(in HectonHardwareProfile hardwareProfile)
        {
            int requestedWorkerCount = ResolveJobWorkerBudget(in hardwareProfile);
            JobsUtility.JobWorkerCount = math.min(requestedWorkerCount, JobsUtility.JobWorkerMaximumCount);
        }

        private static int ResolveJobWorkerBudget(in HectonHardwareProfile hardwareProfile)
        {
            if (HardwareTierDetector.IsQuest3Like)
                return HardwareProfileCatalog.Quest3JobWorkerBudget;
            if (HardwareTierDetector.IsSteamDeckLike)
                return HardwareProfileCatalog.SteamDeckLcdJobWorkerBudget;

            if (HardwareTierDetector.SharedMemoryModeActive)
                return math.max(1, math.min(hardwareProfile.ProcessorCount - 2, 6));

            return math.max(1, hardwareProfile.ProcessorCount - 1);
        }

        private static bool TryRunBootstrapStep(BootstrapStepToken stepToken, string phaseName, Action initializeAction)
        {
            BootstrapStatus.BeginStep(stepToken);
            try
            {
                initializeAction?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException(phaseName, exception);
                return false;
            }
            finally
            {
                BootstrapStatus.EndStep(stepToken);
            }
        }

        private static bool TryRunBootstrapStep(BootstrapStepToken stepToken, string phaseName, Func<bool> initializeAction)
        {
            BootstrapStatus.BeginStep(stepToken);
            try
            {
                return initializeAction == null || initializeAction.Invoke();
            }
            catch (Exception exception)
            {
                HandleFatalBootstrapException(phaseName, exception);
                return false;
            }
            finally
            {
                BootstrapStatus.EndStep(stepToken);
            }
        }

        private static void RegisterSceneLoadGuard()
        {
            if (_sceneGuardRegistered)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoadedGuard;
            SceneManager.sceneLoaded += HandleSceneLoadedGuard;
            _sceneGuardRegistered = true;
        }

        private void MarkProjectPersistentRoot()
        {
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
        }

        private static void EnforceProjectPersistentRoot()
        {
            GameBootstrapper bootstrapper = ActiveInstance;
            if (bootstrapper == null)
                return;

            Scene dontDestroyScene = SceneManager.GetSceneByName(DontDestroyOnLoadSceneName);
            if (!dontDestroyScene.IsValid() || !dontDestroyScene.isLoaded)
                return;

            _bootstrapSceneRootScratch.Clear();
            dontDestroyScene.GetRootGameObjects(_bootstrapSceneRootScratch);
            Transform persistentRoot = bootstrapper.transform;
            GameObject persistentRootObject = bootstrapper.gameObject;

            for (int i = _bootstrapSceneRootScratch.Count - 1; i >= 0; i--)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null || root == persistentRootObject)
                    continue;

                Transform rootTransform = root.transform;
                if (rootTransform == persistentRoot || rootTransform.IsChildOf(persistentRoot))
                    continue;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[GameBootstrapper] Foreign DontDestroyOnLoad root destroyed. name=" + root.name);
#endif
                UnityEngine.Object.Destroy(root);
            }

            _bootstrapSceneRootScratch.Clear();
        }

        private static bool TryBuildBootstrapDependencyExecutionOrder(
            BootstrapDependencyNode[] executionOrder,
            out int executionOrderCount)
        {
            executionOrderCount = 0;
            const int nodeCount = (int)BootstrapDependencyNode.Count;
            if (executionOrder == null || executionOrder.Length < nodeCount)
                return false;

            lock (_bootstrapDependencyScratchLock)
            {
                if (!global::Hecton8.Bootstrap.BootstrapRegistryCycleValidator.TryBuildStartupExecutionOrderOrThrow(
                        _bootstrapRegistryExecutionOrderScratch,
                        out int registryOrderCount))
                {
                    return false;
                }

                if (registryOrderCount != nodeCount)
                    return false;

                for (int i = 0; i < registryOrderCount; i++)
                {
                    if (!TryResolveBootstrapDependencyNode(_bootstrapRegistryExecutionOrderScratch[i], out BootstrapDependencyNode node))
                    {
                        executionOrderCount = 0;
                        return false;
                    }

                    executionOrder[executionOrderCount++] = node;
                }

                return executionOrderCount == nodeCount;
            }
        }

        private static bool TryValidateBootstrapRegistryStartupGraph()
        {
            return global::Hecton8.Bootstrap.BootstrapRegistryCycleValidator.TryValidateStartupGraph();
        }

        private static bool TryResolveBootstrapDependencyNode(
            GlobalRegistryServiceSlot serviceSlot,
            out BootstrapDependencyNode node)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    node = BootstrapDependencyNode.SystemDispatcher;
                    return true;
                case GlobalRegistryServiceSlot.TickManager:
                    node = BootstrapDependencyNode.GameTickManager;
                    return true;
                case GlobalRegistryServiceSlot.Save:
                    node = BootstrapDependencyNode.SaveManager;
                    return true;
                case GlobalRegistryServiceSlot.ObjectPool:
                    node = BootstrapDependencyNode.ObjectPoolManager;
                    return true;
                case GlobalRegistryServiceSlot.RenderDispatcher:
                    node = BootstrapDependencyNode.RenderDispatcher;
                    return true;
                case GlobalRegistryServiceSlot.Scene:
                    node = BootstrapDependencyNode.SceneRuntimeService;
                    return true;
                case GlobalRegistryServiceSlot.InteractionSignals:
                    node = BootstrapDependencyNode.EquipmentInteractionHandler;
                    return true;
                case GlobalRegistryServiceSlot.FloatingOriginRuntime:
                    node = BootstrapDependencyNode.HectonFloatingOrigin;
                    return true;
                case GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime:
                    node = BootstrapDependencyNode.ConnectionSplineBatchRenderer;
                    return true;
                case GlobalRegistryServiceSlot.PhysicsStateManager:
                    node = BootstrapDependencyNode.GlobalPhysicsStateManager;
                    return true;
                case GlobalRegistryServiceSlot.Physics:
                    node = BootstrapDependencyNode.PhysicsApplySystem;
                    return true;
                case GlobalRegistryServiceSlot.Debris:
                case GlobalRegistryServiceSlot.DebrisComputeRuntime:
                    node = BootstrapDependencyNode.DebrisManager;
                    return true;
                case GlobalRegistryServiceSlot.Environment:
                    node = BootstrapDependencyNode.EnvironmentRuntimeContextService;
                    return true;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    node = BootstrapDependencyNode.OceanKinematicsRuntimeService;
                    return true;
                case GlobalRegistryServiceSlot.EcosystemDirector:
                    node = BootstrapDependencyNode.EcosystemDirector;
                    return true;
                case GlobalRegistryServiceSlot.FaunaSimulation:
                    node = BootstrapDependencyNode.FaunaSimulation;
                    return true;
                case GlobalRegistryServiceSlot.Audio:
                    node = BootstrapDependencyNode.SpatialAudioManager;
                    return true;
                case GlobalRegistryServiceSlot.PowerGrid:
                    node = BootstrapDependencyNode.PowerGridManager;
                    return true;
                case GlobalRegistryServiceSlot.Logistics:
                    node = BootstrapDependencyNode.ConstructionManager;
                    return true;
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime:
                    node = BootstrapDependencyNode.NativeInputManager;
                    return true;
                case GlobalRegistryServiceSlot.Input:
                    node = BootstrapDependencyNode.InputDispatcher;
                    return true;
                case GlobalRegistryServiceSlot.Player:
                    node = BootstrapDependencyNode.PlayerRuntimeContextService;
                    return true;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    node = BootstrapDependencyNode.PlayerInventoryManager;
                    return true;
                case GlobalRegistryServiceSlot.PlayerActionRuntime:
                    node = BootstrapDependencyNode.PlayerActionRuntime;
                    return true;
                case GlobalRegistryServiceSlot.PlayerSensory:
                    node = BootstrapDependencyNode.PlayerSensoryManager;
                    return true;
                case GlobalRegistryServiceSlot.BeaconNetworkRuntime:
                    node = BootstrapDependencyNode.BeaconNetworkSystem;
                    return true;
                case GlobalRegistryServiceSlot.ModWorldPersistenceRuntime:
                    node = BootstrapDependencyNode.ModWorldPersistenceManager;
                    return true;
                default:
                    node = default;
                    return false;
            }
        }

        private static string ResolveBootstrapDependencyNodeName(BootstrapDependencyNode node)
        {
            int index = (int)node;
            return index >= 0 && index < _bootstrapDependencyNodeNames.Length
                ? _bootstrapDependencyNodeNames[index]
                : "Unknown";
        }

        private static void HandleSceneLoadedGuard(Scene scene, LoadSceneMode mode)
        {
#if UNITY_INCLUDE_TESTS
            if (_isUnityTestRunnerProcess)
                return;
#endif
            if (!Application.isPlaying)
                return;

            if (_bootstrapGameplayHandoffOwnsSceneLoad)
                return;

            if (_isBootstrapComplete)
            {
                EnsureExtendedRegistryCoverageForActiveScene();
                bool req = RequiresGameplaySceneActivation(scene);
                Debug.Log($"[GameBootstrapper-DEBUG] HandleSceneLoadedGuard: _isBootstrapComplete=true, scene={scene.name}, RequiresGameplaySceneActivation={req}");
                if (req)
                    RequestSceneActivation();

                BootstrapBiosErrorOverlay.Hide();
                return;
            }

            TryRecoverEntryVector(scene, true);
        }

        private static bool TryRecoverEntryVector(Scene scene, bool allowRecovery)
        {
            if (IsBootstrapScene(scene))
                return true;

            if (!allowRecovery)
                return false;

            // Recovery schedules an async single-mode load of the bootstrap scene. A second
            // non-bootstrap scene event arriving while that load is still in flight would stack
            // another bootstrap load on top of it. This flag was already being set here and reset
            // by the full bootstrap state reset for exactly that purpose, but nothing ever read
            // it, so the re-entrancy guard it was written for never actually existed.
            if (_entryRecoveryIssued)
                return false;

            // The reload below restarts bootstrap, and that restart clears _entryRecoveryIssued - so the
            // flag above cannot bound anything across attempts. Without this cap a boot that never
            // completes reloads the bootstrap scene forever, and every reload destroys the scene it
            // arrived in, along with whatever that scene had already spawned.
            if (_entryRecoveryAttempts >= MaxEntryRecoveryAttempts)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _EntryRecoveryLoopHash,
                    0u,
                    _entryRecoveryAttempts);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[GameBootstrapper] Entry recovery refused after " +
                    _entryRecoveryAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " attempts - bootstrap is not recovering, it is looping. Reloading again cannot help. " +
                    "The real failure is upstream: read the last scene-activation step in this log.");
#endif
                return false;
            }

            _entryRecoveryAttempts++;
            _entryRecoveryIssued = true;
            if (!GameStartContextHolder.TryGetPendingTargetSceneName(out _))
            {
                GameStartContext context = GameStartContext.CreateNewGame();
                GameStartContextHolder.SetCurrent(context, scene.name);
            }

            AsyncOperation operation = LoadProductionSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            if (operation == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[GameBootstrapper] Failed to schedule async bootstrap entry recovery load.");
#endif
            }

            return false;
        }

        private async Awaitable<bool> ExecuteSceneReadinessGatesAsync(CancellationToken ownerToken)
        {
            if (_sceneActivationStarted)
                return _debugSceneActivationCompleted;

            _sceneActivationStarted = true;
            _debugSceneActivationCompleted = false;
            BootstrapState.PublishGameReady(false);
            BootstrapState.PublishBootstrapPresence(true);

            Scene activeScene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            if (RejectDirtyEditorSceneAndReloadFromDisk(activeScene))
                return false;
#endif
            SceneInstantiationGate.ActiveRuntime?.BeginSceneLoad(activeScene.name);
            ResolveSceneActivationReferences(activeScene);
            DisablePlayer();
            ApplyShippingSceneCleanup(activeScene);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                ownerToken,
                destroyCancellationToken);

            // bootstrapTimeout is now the NO-PROGRESS budget for a single activation step, not the total
            // for the whole phase. SetSceneActivationStep pushes this deadline forward each time the
            // phase reaches a new named step; see the _sceneActivationDeadline field comment for why the
            // total-duration form was wrong. The field is cleared in the finally below, because
            // CancelAfter on a disposed source throws.
            _sceneActivationDeadline = cts;
            cts.CancelAfter(TimeSpan.FromSeconds(bootstrapTimeout));
            CancellationToken ct = cts.Token;

            try
            {
                SetSceneActivationStep("Step 1: Verifying Singletons");
                if (!VerifySingletons())
                {
                    FailSceneActivation("Critical singletons missing. Bootstrap aborted.");
                    return false;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 2: Pool Warmup");
                await WarmupPoolsAsync(ct);

                SetSceneActivationStep("Step 3: World Generation");
                WriteBootStateRecord(BootStateMarker.WorldGen, BootstrapPhase.SceneActivate, GlobalRegistryServiceSlot.WorldGen);
                StartWorldGeneration();
                if (worldGenWaitTime > 0f)
                    await DelaySecondsByNextFramesAsync(worldGenWaitTime, ct);
                else
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);

                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 4: Save/Load");
                await LoadOrNewGameAsync();

                // Re-read the active scene instead of reusing the one captured before this sequence began.
                // The captured handle is wrong twice over, and it is why the player gate never opens.
                //
                // Activation is kicked off from HandleSceneLoadedGuard during the ADDITIVE world load's
                // sceneLoaded callback, so the capture happens before SceneRuntimeService reaches
                // SetActiveScene (SceneRuntimeService.cs:838). A run logged
                // "RequiresGameplaySceneActivation: 01_MAIN_MENU -> isValid=True" from that path - the
                // outgoing menu scene. By the time this line is reached the same struct has gone stale
                // entirely: the identical call logged "-> isValid=False, isLoaded=False" a few hundred lines
                // later, because the menu scene it named had been unloaded.
                //
                // ResolveSceneActivationReferences searches that handle for the things Step 7 needs:
                // TryResolveSceneComponent(scene, ... out HectonPlayerSpawner) and
                // TryResolveSceneTaggedObject(scene, "Player", ...). Pointed at the menu scene and then at an
                // invalid handle, neither can ever see anything in 02_HECTON_WORLD. So playerSpawner stays
                // null, SpawnPlayerAsync cannot take its spawner route and cannot re-instantiate the player
                // that was destroyed during the transition, MarkPlayerInstantiated stores PLAYER_NULL, the
                // SceneInstantiationGate never opens, and ActivatePlayer never runs - which is also why
                // DisablePlayer's SetActive(false) is never undone.
                //
                // Observed severity: on the menu route this produced "Bootstrap timed out during scene
                // activation" while the probe still scored Boot=PASS; on the bootstrap-handoff route it
                // failed the whole phase - "Bootstrap phase failed. phase=SceneActivate" - so
                // _isBootstrapComplete never became true and the main menu could never load at all.
                //
                // activeScene is also handed to ApplyShippingSceneCleanup and TryValidateSceneRootBudget
                // further down, both of which were operating on the menu or invalid scene for the same
                // reason. One re-read fixes all three consumers.
                activeScene = SceneManager.GetActiveScene();
                ResolveSceneActivationReferences(activeScene);
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 5: World-Ready Check");
                await WaitForWorldReadyAsync(ct);

                SetSceneActivationStep("Step 6: Ground-Ready Check");
                await WaitForGroundReadyAsync(ct);

                SetSceneActivationStep("Step 7: Player Spawn");
                await SpawnPlayerAsync(ct);
                SceneInstantiationGate.ActiveRuntime?.MarkPlayerInstantiated(playerObject);
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 8: Runtime World Prime");
                await PrimeRuntimeWorldAsync(ct);
                SceneInstantiationGate.ActiveRuntime?.MarkWorldPrimed();
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 8.5: Cold Cleanup + Memory Snapshot");
                await RunColdCleanupAndCaptureMemorySnapshotAsync(ct);
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 8.75: Resident World Prefab Gate");
                if (!await WaitForResidentWorldPrefabPoolsReadyAsync(ct))
                    return false;
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 8.9: Scene Gate Verification");
                await WaitForSceneInstantiationGateAsync(ct);
                ct.ThrowIfCancellationRequested();

                SetSceneActivationStep("Step 8.95: Scene Graph Guard");
                if (!TryValidateSceneRootBudget(activeScene, "game-bootstrapper-scene-activation"))
                {
                    FailSceneActivation("Scene graph corruption guard aborted activation.");
                    return false;
                }

                ActivatePlayer();

                SetSceneActivationStep("Complete");
                _debugSceneActivationCompleted = true;
                BootstrapState.PublishGameReady(true);
                BootstrapState.PublishBootstrapPresence(false);
                RaiseGameReadyEvent();
                return true;
            }
            catch (OperationCanceledException)
            {
                BootstrapState.PublishGameReady(false);
                BootstrapState.PublishBootstrapPresence(false);
                if (this == null || destroyCancellationToken.IsCancellationRequested)
                    return false;

                FailSceneActivation("Bootstrap timed out during scene activation.");
                return false;
            }
            catch (Exception exception)
            {
                BootstrapState.PublishGameReady(false);
                BootstrapState.PublishBootstrapPresence(false);
                if (this == null)
                    return false;

                FailSceneActivation("Bootstrap failed during scene activation.");
                HandleFatalBootstrapException(_debugSceneActivationStep, exception);
                return false;
            }
            finally
            {
                // The `using` disposes cts as this scope unwinds, so the field must stop pointing at it
                // first. A later SetSceneActivationStep would otherwise CancelAfter a disposed source.
                _sceneActivationDeadline = null;
            }
        }

        private void ResolveSceneActivationReferences(Scene scene)
        {
            if (playerObject == null &&
                TryAcceptProductionPlayerAuthority(BootstrapState.CurrentPlayerObject, out GameObject publishedPlayer))
            {
                playerObject = publishedPlayer;
            }
            else if (!TryAcceptProductionPlayerAuthority(playerObject, out _))
            {
                playerObject = null;
            }

            if (playerObject == null)
            {
                TryResolveSceneTaggedObject(scene, "Player", out GameObject taggedPlayer);
                if (TryAcceptProductionPlayerAuthority(taggedPlayer, out GameObject productionTaggedPlayer))
                {
                    playerObject = productionTaggedPlayer;
                }
                else if (taggedPlayer != null)
                {
                    LogSceneActivation("[PlayerAuthority] Rejected tagged Player without production movement/interaction/physics authority.");
                }
            }

            if (playerSpawner == null)
            {
                TryResolveSceneComponent(scene, includeInactive: true, out HectonPlayerSpawner spawner);
                if (spawner != null && !IsTemporaryRuntimeShellObject(spawner.gameObject))
                    playerSpawner = spawner;
            }

            if (playerRigidbody == null && playerObject != null)
                playerObject.TryGetComponent(out playerRigidbody);

            if (playerController == null && playerObject != null)
                playerObject.TryGetComponent(out playerController);

            PublishPlayerRuntimeReference();
            TryPublishUnderwaterVisualsRuntimeReference(scene);
        }

        private void TryPublishUnderwaterVisualsRuntimeReference(Scene scene)
        {
            if (!Application.isPlaying || !RequiresGameplaySceneActivation(scene))
                return;

            if (!IsPublishableUnderwaterVisualsRuntime(underwaterVisuals, scene) &&
                TryResolveSceneComponent(scene, includeInactive: true, out HectonUnderwaterVisuals resolvedUnderwaterVisuals) &&
                IsPublishableUnderwaterVisualsRuntime(resolvedUnderwaterVisuals, scene))
            {
                underwaterVisuals = resolvedUnderwaterVisuals;
            }

            if (!IsPublishableUnderwaterVisualsRuntime(underwaterVisuals, scene))
                return;

            bool runtimePublicationGateOpen = false;
            try
            {
                GlobalRegistry.BeginSceneRuntimePublicationGate();
                runtimePublicationGateOpen = true;

                if (GlobalRegistry.UnderwaterVisuals != underwaterVisuals)
                    GlobalRegistry.RegisterUnderwaterVisualsRuntime(underwaterVisuals);
            }
            finally
            {
                if (runtimePublicationGateOpen)
                    GlobalRegistry.EndSceneRuntimePublicationGate();
            }
        }

        private static bool IsPublishableUnderwaterVisualsRuntime(
            HectonUnderwaterVisuals candidate,
            Scene scene)
        {
            if (candidate == null || IsTemporaryRuntimeShellObject(candidate.gameObject))
                return false;

            Scene candidateScene = candidate.gameObject.scene;
            return candidateScene.IsValid() &&
                   candidateScene.isLoaded &&
                   RequiresGameplaySceneActivation(candidateScene) &&
                   string.Equals(candidateScene.path, scene.path, StringComparison.Ordinal);
        }

        private bool VerifySingletons()
        {
            bool allCritical = true;
            EnsureWorldStateServiceRegistered();
            EnsureConstructionServiceRegistered();

            if (GlobalRegistry.Dispatcher == null)
            {
                Debug.LogError("[GameBootstrapper] SystemDispatcher not found.");
                allCritical = false;
            }

            if (GlobalRegistry.ObjectPool == null)
            {
                Debug.LogError("[GameBootstrapper] ObjectPoolManager not found.");
                allCritical = false;
            }

            PrefabRegistry prefabRegistry = null;
            if (!PrefabRegistry.TryResolveActiveRuntime(ref prefabRegistry))
            {
                Debug.LogError("[GameBootstrapper] PrefabRegistry not found.");
                allCritical = false;
            }

            if (!IsSaveManagerUsable(GlobalRegistry.Save as SaveManager))
            {
                Debug.LogError("[GameBootstrapper] SaveManager not found or not initialized.");
                allCritical = false;
            }

            if (GlobalRegistry.WorldState == null)
                Debug.LogWarning("[GameBootstrapper] WorldStateManager not found.");

            if (GlobalRegistry.ConstructionRuntime == null)
                Debug.LogWarning("[GameBootstrapper] ConstructionManager not found.");

            return allCritical;
        }

        private async Awaitable WarmupPoolsAsync(CancellationToken ct)
        {
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null || warmupEntries == null)
                return;

            for (int i = 0, entryCount = warmupEntries.Count; i < entryCount; i++)
            {
                WarmupEntry entry = warmupEntries[i];
                if (entry.prefab == null || entry.count <= 0)
                    continue;

                string label = string.IsNullOrEmpty(entry.label) ? entry.prefab.name : entry.label;
                for (int created = 0; created < entry.count;)
                {
                    int batch = Mathf.Min(WarmupBatchSize, entry.count - created);
                    pool.Warmup(entry.prefab, batch);
                    created += batch;
                    SetSceneActivationStep("Warming Pool");
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                    ct.ThrowIfCancellationRequested();
                }
            }
        }

        private void StartWorldGeneration()
        {
            ITerrainProvider terrainProvider = GlobalRegistry.Terrain;
            if (terrainProvider != null && terrainProvider.IsAvailable)
                return;

            global::HectonWorldGenerator legacyWorldGenerator =
                GlobalRegistry.WorldSeedProvider as global::HectonWorldGenerator;
            if (legacyWorldGenerator != null && !IsTemporaryRuntimeShellObject(legacyWorldGenerator.gameObject))
                return;
        }

        private async Awaitable LoadOrNewGameAsync()
        {
            SaveManager save = GlobalRegistry.Save as SaveManager;
            if (!IsSaveManagerUsable(save))
            {
                _isLoadingSave = false;
                InitNewGame();
                return;
            }

            GameStartContext context;
            if (!GameStartContextHolder.TryGetCurrentOrRestore(out context))
                context = GameStartContext.CreateNewGame();

            GameStartContextHolder.ClearPersistedHandoff();
            if (forceNewGame)
                context = GameStartContext.CreateNewGame();

            GameStartContextHolder.Current = context;
            if (context.StartMode == GameStartMode.NewGame || string.IsNullOrEmpty(context.TargetSaveSlot))
            {
                _isLoadingSave = false;
                InitNewGame();
                return;
            }

            if (!SaveManager.TryResolveSafeSlotName(context.TargetSaveSlot, out string targetSaveSlot))
            {
                StartNewGameFromRejectedLoadContext();
                return;
            }

            if (!save.SaveExists(targetSaveSlot))
            {
                StartNewGameFromRejectedLoadContext();
                return;
            }

            try
            {
                await save.LoadGameAsync(targetSaveSlot);
                if (!save.LastOperationSucceeded)
                {
                    StartNewGameFromRejectedLoadContext();
                    return;
                }

                _isLoadingSave = true;
            }
            catch (Exception)
            {
                Debug.LogError("[GameBootstrapper] Save load failed.");
                StartNewGameFromRejectedLoadContext();
            }
        }

        private void StartNewGameFromRejectedLoadContext()
        {
            GameStartContextHolder.Current = GameStartContext.CreateNewGame();
            _isLoadingSave = false;
            InitNewGame();
        }

        private void InitNewGame()
        {
            WorldStateManager worldStateManager = GlobalRegistry.WorldState;
            if (worldStateManager != null)
                worldStateManager.ClearAll();

            ILogisticsService logistics = GlobalRegistry.Logistics;
            if (logistics != null)
                logistics.ClearAllModules();
        }

        private async Awaitable WaitForWorldReadyAsync(CancellationToken ct)
        {
            ScavengePopulator populator = null;
            if (!WorldRuntimeReferenceUtility.TryResolveScavengePopulator(ref populator))
                return;

            int lastPendingCount = int.MaxValue;
            int stagnantPollCount = 0;
            while (WorldRuntimeReferenceUtility.TryResolveScavengePopulator(ref populator))
            {
                int pendingCount = populator.PendingSpawnCount;
                if (pendingCount <= WorldReadyThreshold)
                    return;

                if (pendingCount < lastPendingCount)
                {
                    lastPendingCount = pendingCount;
                    stagnantPollCount = 0;
                }
                else if (++stagnantPollCount >= WorldReadyStagnationPollLimit)
                {
                    Debug.LogWarning("[GameBootstrapper] World-ready queue stalled. Continuing bootstrap.");
                    return;
                }

                await DelaySecondsByNextFramesAsync(WorldReadyPollIntervalSec, ct);
                ct.ThrowIfCancellationRequested();
            }
        }

        private async Awaitable WaitForGroundReadyAsync(CancellationToken ct)
        {
            if (!_isLoadingSave || playerObject == null)
                return;

            Vector3 playerPosition = playerObject.transform.position;
            float elapsed = 0f;
            while (elapsed < groundReadyTimeout)
            {
                ct.ThrowIfCancellationRequested();
                int groundMask = groundReadyLayerMask.value != 0
                    ? groundReadyLayerMask.value
                    : HectonLayerMasks.SeamProbeLayerMask;

                if (TryResolveCachedGroundReady(playerPosition, groundMask))
                {
                    return;
                }

                await DelaySecondsByNextFramesAsync(GroundCheckPollIntervalSec, ct);
                elapsed += GroundCheckPollIntervalSec;
            }

            Debug.LogWarning("[GameBootstrapper] Ground-ready timed out. Activating player without collider confirmation.");
        }

        private static async Awaitable DelaySecondsByNextFramesAsync(float seconds, CancellationToken ct)
        {
            float safeSeconds = math.isfinite(seconds) ? math.max(0f, seconds) : 0f;
            if (safeSeconds <= 0f)
            {
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                return;
            }

            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < safeSeconds)
            {
                ct.ThrowIfCancellationRequested();
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            }
        }

        private static bool TryResolveCachedGroundReady(Vector3 playerPosition, int groundMask)
        {
            if (!IsFinite(playerPosition))
                return false;

            Vector3 rayOrigin = playerPosition + Vector3.up * GroundCheckRayOffset;
            return TryResolveCachedTerrainGroundReady(rayOrigin, groundMask) ||
                   TryResolveCachedVoxelGroundReady(rayOrigin, groundMask);
        }

        private static bool TryResolveCachedTerrainGroundReady(Vector3 rayOrigin, int groundMask)
        {
            if (!IncludesAnyLayer(groundMask, HectonLayerMasks.TerrainLayerMask))
                return false;

            ITerrainProvider terrainProvider = GlobalRegistry.Terrain;
            if (terrainProvider == null ||
                !terrainProvider.IsAvailable ||
                !terrainProvider.TryGetHeight(rayOrigin.x, rayOrigin.z, out float terrainHeight) ||
                !math.isfinite(terrainHeight))
            {
                return false;
            }

            float distance = rayOrigin.y - terrainHeight;
            return math.isfinite(distance) && distance >= 0f && distance <= GroundCheckRayLength;
        }

        private static bool TryResolveCachedVoxelGroundReady(Vector3 rayOrigin, int groundMask)
        {
            if (!IncludesAnyLayer(groundMask, HectonLayerMasks.VoxelCaveLayerMask | HectonLayerMasks.VoxelProxyLayerMask))
                return false;

            IVoxelSonarSdfReadModel readModel = GlobalRegistry.VoxelSonarSdf;
            if (readModel == null)
                return false;

            if (!VoxelSonarSdfMath.TryResolveNearestSdfSurface(
                    readModel,
                    math.float3(rayOrigin.x, rayOrigin.y, rayOrigin.z),
                    math.float3(0f, -1f, 0f),
                    GroundCheckRayLength,
                    ResolveGroundReadySdfStepMeters(),
                    out VoxelSonarSdfRaycastHit hit) ||
                (hit.Flags & VoxelSonarSdfRaycastHit.FlagHit) == 0u ||
                !math.all(math.isfinite(hit.Point)) ||
                !math.isfinite(hit.Distance))
            {
                return false;
            }

            return hit.Distance >= 0f && hit.Distance <= GroundCheckRayLength;
        }

        private static float ResolveGroundReadySdfStepMeters()
        {
            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            return math.lerp(1.25f, 0.25f, quality);
        }

        private static bool IncludesAnyLayer(int queryMask, int requiredMask)
        {
            return queryMask == -1 || (queryMask & requiredMask) != 0;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private async Awaitable SpawnPlayerAsync(CancellationToken ct)
        {
            if (_isLoadingSave)
                return;

            if (playerSpawner != null && playerSpawner.TryGetComponent(out HectonPlayerSpawner spawner))
            {
                await spawner.SpawnPlayerAsync(ct);
                ResolveSceneActivationReferences(SceneManager.GetActiveScene());
                return;
            }

            if (playerObject != null)
            {
                if (TryAcceptProductionPlayerAuthority(playerObject, out GameObject productionPlayerObject))
                {
                    playerObject = productionPlayerObject;
                }
                else
                {
                    playerObject = null;
                    Debug.LogWarning("[GameBootstrapper] Existing player reference rejected: production movement/interaction/physics authority missing.");
                }
            }

            PublishPlayerRuntimeReference();
            if (playerObject != null)
            {
                if (!IsPlayerAuthoredInActiveScene(playerObject))
                    playerObject.transform.position = fallbackSpawnPosition;
                return;
            }

            Debug.LogWarning("[GameBootstrapper] No player spawner or owned player reference is available.");
        }

        private static bool IsPlayerAuthoredInActiveScene(GameObject candidate)
        {
            if (candidate == null)
                return false;

            Scene playerScene = candidate.scene;
            Scene activeScene = SceneManager.GetActiveScene();
            return playerScene.IsValid() &&
                   activeScene.IsValid() &&
                   string.Equals(playerScene.path, activeScene.path, StringComparison.Ordinal);
        }

        private async Awaitable PrimeRuntimeWorldAsync(CancellationToken ct)
        {
            if (!prewarmProceduralScatterBeforePlayerActivation)
                return;

            if (!TryResolveProductionScatterDirector(out _worldProceduralScatterDirector))
                return;

            int passCount = Mathf.Clamp(scatterBootstrapPrimePasses, 1, 4);
            if (_worldProceduralScatterDirector.TryPrewarmBootstrapSamplingPipeline())
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);

            for (int i = 0; i < passCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (!_worldProceduralScatterDirector.TryPrimeBootstrapScatterPass())
                    return;

                if (!_worldProceduralScatterDirector.HasBootstrapPrimeWork)
                    return;

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            }
        }

        private async Awaitable RunColdCleanupAndCaptureMemorySnapshotAsync(CancellationToken ct)
        {
            AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
            if (governor != null)
                governor.ForceDrainPendingReleaseQueue();

            await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
            ct.ThrowIfCancellationRequested();

            IVramPressureSampleSink pressureMonitor = GlobalRegistry.VRAMPressureSampleSink;
            if (pressureMonitor != null)
                pressureMonitor.ForceImmediateSampleAndResponse();

            CaptureStartupMemorySnapshot();
            float totalVramMb = 0f;
            VRAMMonitor vramMonitor = GlobalRegistry.VRAMMonitor;
            if (vramMonitor != null)
                totalVramMb = vramMonitor.TotalVRAMBytes / BytesPerMegabyte;

            SceneInstantiationGate.ActiveRuntime?.CaptureMemorySnapshot(
                _debugStartupTextureMemoryMb,
                _debugStartupReservedMemoryMb,
                totalVramMb);
        }

        private async Awaitable WaitForSceneInstantiationGateAsync(CancellationToken ct)
        {
            SceneInstantiationGate gate = SceneInstantiationGate.ActiveRuntime;
            if (gate == null)
                return;

            try
            {
                await gate.WaitForOpenAsync(ct);
            }
            catch (OperationCanceledException)
            {
                Hecton8.Core.H8Debug.LogError(
                    "[GameBootstrapper] SceneInstantiationGate wait cancelled. reason=" +
                    gate.LastFailureReason);
                throw;
            }
        }

        private async Awaitable<bool> WaitForResidentWorldPrefabPoolsReadyAsync(CancellationToken ct)
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
            {
                FailSceneActivation("PersistentWorldRegistry missing.");
                return false;
            }

            while (Application.isPlaying && BootstrapState.HasActiveInstance && !registry.AreResidentWorldPrefabPoolsReady())
            {
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                ct.ThrowIfCancellationRequested();
            }

            return true;
        }

        private void DisablePlayer()
        {
            PublishPlayerRuntimeReference();
            if (playerRigidbody == null && playerObject != null)
                playerObject.TryGetComponent(out playerRigidbody);

            SetLegacyPlayerRigidbodyKinematic(playerRigidbody, true);

            if (playerObject != null && playerObject.activeSelf)
                playerObject.SetActive(false);

            if (playerController != null)
                playerController.enabled = false;
        }

        private void ActivatePlayer()
        {
            bool runtimePublicationGateOpen = false;
            try
            {
                GlobalRegistry.BeginSceneRuntimePublicationGate();
                runtimePublicationGateOpen = true;

                PublishPlayerRuntimeReference();
                if (playerObject != null)
                    playerObject.SetActive(true);

                SetLegacyPlayerRigidbodyKinematic(playerRigidbody, false);

                if (playerController != null)
                    playerController.enabled = true;

                // L19 hop2 LIVE: SoftMute after ActivatePlayer - player AudioListener
                // re-enables on SetActive; keep-one muted sink before next audio tick.
                if (Application.isBatchMode || _headlessBootMode)
                    SoftMuteAudioForBatchProbe();
            }
            finally
            {
                if (runtimePublicationGateOpen)
                    GlobalRegistry.EndSceneRuntimePublicationGate();
            }
        }

        private void PublishPlayerRuntimeReference()
        {
            GameObject productionPlayerObject;
            if (playerObject != null)
            {
                if (TryAcceptProductionPlayerAuthority(playerObject, out productionPlayerObject))
                    playerObject = productionPlayerObject;
                else
                    playerObject = null;
            }

            if (playerObject == null &&
                playerRigidbody != null &&
                TryAcceptProductionPlayerAuthority(playerRigidbody.gameObject, out productionPlayerObject))
            {
                playerObject = productionPlayerObject;
            }
            else if (playerObject == null && playerRigidbody != null)
            {
                playerRigidbody = null;
            }

            if (playerObject == null &&
                playerController != null &&
                TryAcceptProductionPlayerAuthority(playerController.gameObject, out productionPlayerObject))
            {
                playerObject = productionPlayerObject;
            }
            else if (playerObject == null && playerController != null)
            {
                playerController = null;
            }

            // The installers below AddComponent the scene-owned service layer, so they are the calls
            // that actually publish into the registry - and they run more than once per activation.
            //
            // Without this gate the second pass is rejected wholesale. A headless run of
            // 02_HECTON_WORLD reported "REGISTRYLOCK 10 services were REJECTED by the ready-locked
            // registry. This world is a shell": IProfileService, DynamicDifficultyDirector,
            // RunModifierController, IMetaCampaignService, ScrapManager, ResourceScarcityDirector,
            // EnvironmentalStrainManager, FaunaGeneticsManager, EcosystemHealthDirector and
            // MigrationDirector - the entire meta, economy and ecosystem layer.
            //
            // The sequence that produces it: the menu->world load is additive, so pass 1 runs inside
            // SceneRuntimeService.LoadSceneAsync's gate while the active scene is still 01_MAIN_MENU;
            // its runtime roots land in the menu scene and are destroyed when that scene is unloaded.
            // Pass 2 runs at readiness Step 4, seconds later, long after that gate's finally closed -
            // so it re-creates every component and every registration throws CriticalBootException.
            //
            // The throw is not merely a failed registration. It escapes the owner's OnEnable, so every
            // statement after the registration call is skipped: FaunaGeneticsManager never reaches
            // TryRegisterHotSwapListener, TryRegisterSaveParticipant or TryRegisterWorldSeedProvider,
            // and it is the only live IWorldSeedProvider - which is why the same run reported
            // "WORLDSEED provider=NULL - every procedural generator is running on seed 0". Nothing
            // re-invites a rejected service; the slot stays dead for the session.
            //
            // Gating the construction block rather than a later call makes this independent of which
            // pass builds the components, of frame timing, and of whether the transition is additive.
            // ActivatePlayer already holds a gate when it calls this method; the depth is Interlocked
            // counted, so nesting is safe. The block contains no await, so the window is synchronous
            // and only the installers' own Awake/OnEnable can publish inside it. The ready-lock still
            // denies every core BIOS slot - IsSceneRuntimeHotSwapSlot hard-denies Input, Physics,
            // Audio, Save, Dispatcher, DataVault and the rest, gate depth or not.
            bool installerPublicationGateOpen = false;
            try
            {
                GlobalRegistry.BeginSceneRuntimePublicationGate();
                installerPublicationGateOpen = true;

                Hecton8.World.WorldRuntimeInstaller.EnsureRuntimeSystems();
                Hecton8.Meta.MetaRuntimeInstaller.EnsureRuntimeSystems();
                Hecton8.Economy.EconomyRuntimeInstaller.EnsureRuntimeSystems();
                Hecton8.Ecosystem.EcosystemRuntimeInstaller.EnsureRuntimeSystems();
                Hecton8.PDA.PDARuntimeInstaller.EnsurePlayerSystems(playerObject);
                Hecton8.Progression.ProgressionRuntimeInstaller.EnsurePlayerSystems(playerObject);
                Hecton8.Narrative.NarrativeRuntimeInstaller.EnsurePlayerSystems(playerObject);
                Hecton8.Audio.AtmosphericAudioRuntimeInstaller.EnsurePlayerSystems(playerObject);

                // AcousticZoneController is the sole owner of GlobalRegistry.AcousticZone /
                // AcousticZoneReadModel / AcousticZoneMadnessCueSink / ToolAcousticCues and had no
                // construction site of any kind (no AddComponent, no scene/prefab GUID
                // 46c4f463f7190a04b9285cb2b4cc7f63). Four live consumers cached the permanent null:
                // HectonSurfaceWeatherDirector.cs:836, DeepPsychosisController.cs:340,
                // HectonMusicDirector.cs:1573, MantaScooter.cs:2608. Surface/interior/underwater
                // snapshot transitions and tool acoustic cues never armed.
                Hecton8.Audio.AtmosphericAudioRuntimeInstaller.EnsureRuntimeSystems();

                // GlobalWeatherDirector is the only IWeatherService in the project and was never
                // constructed, so GlobalRegistry.Weather was permanently null and TEN live consumers
                // cached that null: HectonCelestialEngine.cs:2096, SolarPanel.cs:296,
                // SomaticKinematicsRuntime.cs:2609, HectonFluidEngine.cs:2505,
                // HectonUnderwaterVisuals.cs:2417, AbyssalDeferredCausticsRuntime.cs:1740,
                // SpatialAudioManager.cs:5352, TetherManager.cs:878, HectonMarineSnowRenderer.cs:1002,
                // HectonMapMagicVegetationBridge.cs:2839. There was no storm, wind, precipitation or
                // weather-driven vegetation and audio response anywhere in the world.
                Hecton8.Environment.EnvironmentRuntimeInstaller.EnsureRuntimeSystems();

                // ToolDurabilitySystem was never constructed either, so GlobalRegistry.ToolDurabilityService
                // was permanently null and seven read sites silently skipped every wear, repair and
                // durability-display path - the laser cutter, scanner, drill and welder ran at full
                // condition forever. Reachability of the consumers is proven, not assumed:
                // PlayerToolManager's script GUID is authored onto Player.prefab and PlayerTool is the base
                // of BuilderTool, whose GUID is on Tool_Builder_Held.prefab.
                Hecton8.Tools.ToolsRuntimeInstaller.EnsureRuntimeSystems();

                // The autonomous extractor already shipped its own resolve-or-create factory,
                // AutonomousExtractorSystem.TryEnsureRuntimeOwner at :252 - complete with the
                // inactive-root handling at :262-269 and a registry read-back check. It had ZERO callers.
                // Meanwhile PlayerBuilder.cs:1515 caches the permanently null registry slot and hands it
                // to placement at :2139, where ValidatePlacementWithRuntime returns false unconditionally
                // on a null runtime (:1165-1170) - so placing an extractor was refused outright in the
                // shipped build. No new installer file for this one: wrapping an existing public static
                // factory in another static method is pure indirection.
                Hecton8.Construction.AutonomousExtractorSystem.TryEnsureRuntimeOwner(out _);

                // LODSystemManager: sole GlobalRegistry.LODSystem owner (distance LOD coordinator).
                // Script GUID e0f5a77c84ce58b40b9c6e6871d1c469 — ZERO live scene/prefab hits.
                // Factory already exists; this is the missing construction site for player builds.
                Hecton8.World.LODSystemManager.EnsureRuntimeInstance();

                // WorldChunkResidencyManager is the sole GlobalRegistry.StreamingBackpressure owner
                // (IStreamingBackpressureService). WorldRuntimeInstaller deliberately skips install
                // (hot-swap token denied for StreamingBackpressureRuntime). ZERO live scene/prefab
                // hits; OnEnable-only registration never runs without a construction site.
                // Pre-Ready bootstrap lane (this gated block) is the correct owner path.
                Hecton8.World.WorldChunkResidencyManager.EnsureRuntimeInstance();


                // SubtitleManager is the sole GlobalRegistry.Subtitles owner. Zero scene/prefab
                // GUID hits (2007393d93d7376438891f11d8ec3a10), including Suit_HUD_Canvas.prefab.
                // Construction previously sat behind UNITY_EDITOR || DEVELOPMENT_BUILD, so player
                // builds never AddComponent'd the owner and vocal/Babel/audio-log cues stayed mute.
                // Factory parents under suit-HUD canvas (overlay / named canvas fallback).
                Hecton8.UI.SubtitleManager.EnsureRuntimeInstance();

                // DebrisManager: sole IDebrisService owner. Primary construction is the Environment
                // bootstrap node (TryResolveBootstrapNode). Defense-in-depth here covers player-
                // publish path if Environment node was skipped/exempted earlier in the same boot.
                Hecton8.Gameplay.DebrisManager.EnsureRuntimeInstance();

                // ChemicalInfluenceGrid: sole chemical read-model / scent grid owner.
                // Script GUID 67189d92acf53ae4786558c89ccd2210 — ZERO live scene/prefab hits.
                // Construction previously sat behind UNITY_EDITOR || DEVELOPMENT_BUILD, so player
                // AI frames and chemical queue APIs never got a grid.
                Hecton8.World.ChemicalInfluenceGrid.EnsureRuntimeInstance();

                // World interaction / FX / narrative owners with EnsureRuntimeInstance factories
                // already pinned by soft-FAIL validators, but ZERO bootstrap construction sites.
                // Scene/prefab GUID hits are only self/validator references for several of these;
                // OnEnable registration never runs without a construction site in player builds.
                Hecton8.World.SargassumCutManager.EnsureRuntimeInstance();
                Hecton8.World.DestructibleOrganicManager.EnsureRuntimeInstance();
                Hecton8.World.AbyssalFluidDecalManager.EnsureRuntimeInstance();
                Hecton8.Gameplay.MissionManager.EnsureRuntimeInstance();
                Hecton8.VFX.CameraJuiceSystem.EnsureRuntimeInstance();
                Hecton8.Narrative.AudioLogSystem.EnsureRuntimeInstance();

                // Second wave: factories + soft-FAIL validators already present; bootstrap wire
                // was the missing construction site (scene/prefab hits absent or self-only).
                Hecton8.Gameplay.HazardZoneManager.EnsureRuntimeInstance();
                Hecton8.AtlasSignal.AtlasSignalSystem.EnsureRuntimeInstance();
                Hecton8.AtlasSignal.AtlasSignalDecoder.EnsureRuntimeInstance();
                Hecton8.World.SargassumGlobalDragManager.EnsureRuntimeInstance();
                // AmbientBiotaDirector lives in Hecton8.AI.Ambient (references Core).
                // Direct call would form a Core↔Ambient cycle; reflect the factory instead.
                TryEnsureRuntimeServiceByReflection(
                    "Hecton8.AI.Ambient.AmbientBiotaDirector, Hecton8.AI.Ambient");
                Hecton8.Vehicles.Automation.DockingAutopilotService.EnsureRuntimeInstance();
                Hecton8.Construction.FluidPipeGraphRuntime.EnsureRuntimeInstance();
                Hecton8.Atmosphere.GasDynamicsSolver.EnsureRuntimeInstance();
                Hecton8.Gameplay.HectonDiscoveryManager.EnsureRuntimeInstance();
                Hecton8.Narrative.LoreDatabaseManager.EnsureRuntimeInstance();

                // Tools owners: ModularEquipmentEngine (IModularEquipmentService) and
                // ToolHapticsRuntime (GlobalRegistry.ToolHaptics). Zero live scene/prefab GUID
                // hits; factories existed with no construction site.
                Hecton8.Tools.ModularEquipmentEngine.EnsureRuntimeInstance();
                Hecton8.Tools.ToolHapticsRuntime.EnsureRuntimeInstance();
            }





            finally
            {
                if (installerPublicationGateOpen)
                    GlobalRegistry.EndSceneRuntimePublicationGate();
            }


            BootstrapState.PublishCurrentPlayerObject(playerObject);
            // HUD canvas may finish claiming ActiveRuntimeInstance after player publish;
            // second resolve is cheap (usable-instance early-out) and covers that race.
            Hecton8.UI.SubtitleManager.EnsureRuntimeInstance();

            // DynamicMusicGranularSynthesizer lives in Hecton8.Audio.Synthesis.DynamicMusic
            // (references Core). Direct call would form a Core↔Synthesis cycle; reflect instead.
            // Player.prefab authors the component on a GO that is not the AudioListener host.
            // Post-player-publish covers the race where AfterSceneLoad ran before player OnEnable
            // published the camera/listener.
            TryEnsureRuntimeServiceByReflection(
                "Hecton8.Audio.Synthesis.DynamicMusicGranularSynthesizer, Hecton8.Audio.Synthesis.DynamicMusic");

        }

        /// <summary>
        /// Player-build-safe reflection ensure for factories that live outside Hecton8.Core.
        /// The editor/dev overload above is ifdef-gated; this path must compile in player builds
        /// so AmbientBiota / DynamicMusic cold ensure does not form a Core↔feature asmdef cycle.
        /// </summary>
        private static bool TryEnsureRuntimeServiceByReflection(string assemblyQualifiedTypeName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedTypeName))
                return false;

            Type serviceType = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
            if (serviceType == null)
                return false;

            MethodInfo ensureMethod = serviceType.GetMethod(
                "EnsureRuntimeInstance",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (ensureMethod == null)
                return false;

            try
            {
                object instance = ensureMethod.Invoke(null, null);
                if (instance is Component component)
                    PersistRuntimeService(component);
                return instance != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ApplyShippingSceneCleanup(Scene scene)
        {
            int suppressedCount = WorldShippingContentFilter.DeactivateSuppressedSceneObjects(
                scene,
                _shippingCleanupRootObjects,
                _shippingCleanupTraversalStack);

            if (suppressedCount > 0)
                LogSceneActivation("[ShippingCleanup] Deactivated " + suppressedCount + " dev/trial scene objects.");
        }

        private void CaptureStartupMemorySnapshot()
        {
            TryReadMemoryMetricMegabytes(_TextureMemoryCandidates, out _debugStartupTextureMemoryMb, out _debugStartupTextureMetric);
            TryReadMemoryMetricMegabytes(_TotalReservedMemoryCandidates, out _debugStartupReservedMemoryMb, out _debugStartupReservedMetric);
        }

        private static bool TryReadMemoryMetricMegabytes(
            string[] candidates,
            out float megabytes,
            out string resolvedMetric)
        {
            megabytes = 0f;
            resolvedMetric = "Unresolved";

            lock (_profilerRecorderHandleScratch)
            {
                _profilerRecorderHandleScratch.Clear();
                ProfilerRecorderHandle.GetAvailable(_profilerRecorderHandleScratch);

                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    string candidate = candidates[candidateIndex];
                    for (int handleIndex = 0; handleIndex < _profilerRecorderHandleScratch.Count; handleIndex++)
                    {
                        ProfilerRecorderDescription description =
                            ProfilerRecorderHandle.GetDescription(_profilerRecorderHandleScratch[handleIndex]);

                        if (!string.Equals(description.Name, candidate, StringComparison.OrdinalIgnoreCase))
                            continue;

                        ProfilerRecorder recorder = default;
                        try
                        {
                            recorder = ProfilerRecorder.StartNew(
                                description.Category,
                                description.Name,
                                1,
                                ProfilerRecorderOptions.Default);

                            if (!recorder.Valid)
                                continue;

                            megabytes = recorder.LastValue / BytesPerMegabyte;
                            resolvedMetric = description.Name;
                            return true;
                        }
                        catch (ArgumentException)
                        {
                        }
                        finally
                        {
                            if (recorder.Valid)
                                recorder.Dispose();
                        }
                    }
                }

                _profilerRecorderHandleScratch.Clear();
            }

            return false;
        }

        private static bool IsTemporaryRuntimeShellObject(GameObject candidate)
        {
            if (candidate == null)
                return false;

            Transform current = candidate.transform;
            while (current != null)
            {
                if (IsTemporaryRuntimeShellName(current.name))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private static bool TryAcceptProductionPlayerAuthority(GameObject candidate, out GameObject productionPlayerObject)
        {
            productionPlayerObject = null;
            if (candidate == null ||
                IsTemporaryRuntimeShellObject(candidate) ||
                !ProductionPlayerAuthorityUtility.IsProductionPlayerAuthorityObject(candidate))
            {
                return false;
            }

            productionPlayerObject = candidate;
            return true;
        }

        private static bool IsTemporaryRuntimeShellName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return name.StartsWith("__", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("temp", StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf("_trial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("_staging", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("_preview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("_smoke", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryResolveProductionScatterDirector(out WorldProceduralScatterDirector director)
        {
            director = null;
            WorldRuntimeReferenceUtility.TryResolveWorldProceduralScatterDirector(ref director);
            if (director != null && !IsTemporaryRuntimeShellObject(director.gameObject))
                return true;

            int registeredDirectorCount = WorldProceduralScatterDirector.RegisteredDirectorCount;
            for (int i = 0; i < registeredDirectorCount; i++)
            {
                WorldProceduralScatterDirector candidate = WorldProceduralScatterDirector.GetRegisteredDirectorAt(i);
                if (candidate == null ||
                    !candidate.isActiveAndEnabled ||
                    IsTemporaryRuntimeShellObject(candidate.gameObject))
                    continue;

                director = candidate;
                return true;
            }

            director = null;
            return false;
        }

        private void FailSceneActivation(string error)
        {
            Debug.LogError("[GameBootstrapper] " + error);
            RaiseBootstrapFailedEvent(error);
        }

        private void LogSceneActivation(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent("game-bootstrapper.scene", message);
#endif
            if (verboseSceneActivationLogging)
                Hecton8.Core.H8Debug.Log("[GameBootstrapper] " + message);
        }

        private void SetSceneActivationStep(string step)
        {
            _debugSceneActivationStep = step;

            // Reaching a new step IS the progress signal, so the no-progress deadline restarts here.
            // CancelAfter on an already-cancelled source is a no-op, and on a DISPOSED one it throws -
            // the field is nulled in the activation phase's finally, and the catch keeps a late call
            // during teardown from turning into a boot failure of its own.
            CancellationTokenSource deadline = _sceneActivationDeadline;
            if (deadline != null)
            {
                try
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(bootstrapTimeout));
                }
                catch (ObjectDisposedException)
                {
                    _sceneActivationDeadline = null;
                }
            }

            LogSceneActivation(step);
        }

#if UNITY_EDITOR
        private static bool RejectDirtyEditorSceneAndReloadFromDisk(Scene scene)
        {
            if (!Application.isEditor || !scene.IsValid() || !scene.isDirty)
                return false;

            string scenePath = scene.path;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                Debug.LogError("[GameBootstrapper] Dirty editor scene rejected, but scene has no disk path.");
                return true;
            }

            Debug.LogError("[GameBootstrapper] Dirty editor scene rejected; reloading from disk: " + scenePath);
            _pendingDirtySceneReloadPath = scenePath;
            UnityEditor.EditorApplication.delayCall -= ProcessDirtySceneReloadFromDisk;
            UnityEditor.EditorApplication.delayCall += ProcessDirtySceneReloadFromDisk;
            return true;
        }

        private static void ProcessDirtySceneReloadFromDisk()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEditor.EditorApplication.ExitPlaymode();
                UnityEditor.EditorApplication.delayCall -= ProcessDirtySceneReloadFromDisk;
                UnityEditor.EditorApplication.delayCall += ProcessDirtySceneReloadFromDisk;
                return;
            }

            string scenePath = _pendingDirtySceneReloadPath;
            _pendingDirtySceneReloadPath = null;
            if (!string.IsNullOrWhiteSpace(scenePath))
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }
#endif

        private void StartBackgroundDomainHandshake()
        {
            if (Volatile.Read(ref _backgroundDomainHandshakeState) != BackgroundDomainHandshakeIdle)
                return;

            _backgroundDomainHandshakePath = HectonPersistentPathPolicy.CombineDirectory("Telemetry");
            _backgroundDomainHandshakeFailureCode = BackgroundDomainHandshakeFailureNone;
            string capturedPath = _backgroundDomainHandshakePath;
            Volatile.Write(ref _backgroundDomainHandshakeState, BackgroundDomainHandshakeRunning);
            _ = RunBackgroundDomainHandshakeAsync(capturedPath);
        }

        private async Awaitable RunBackgroundDomainHandshakeAsync(string telemetryPath)
        {
            await Awaitable.BackgroundThreadAsync();
            int failureCode = TryPrepareBackgroundDomainHandshake(telemetryPath);
            int finalState = failureCode == BackgroundDomainHandshakeFailureNone
                ? BackgroundDomainHandshakeComplete
                : BackgroundDomainHandshakeFailed;

            await Awaitable.MainThreadAsync();
            _backgroundDomainHandshakeFailureCode = failureCode;
            Volatile.Write(ref _backgroundDomainHandshakeState, finalState);
        }

        private static int TryPrepareBackgroundDomainHandshake(string telemetryPath)
        {
            if (string.IsNullOrEmpty(telemetryPath))
                return BackgroundDomainHandshakeFailureInvalidPath;

            try
            {
                Directory.CreateDirectory(telemetryPath);
                return BackgroundDomainHandshakeFailureNone;
            }
            catch (ArgumentException)
            {
                return BackgroundDomainHandshakeFailureInvalidPath;
            }
            catch (IOException)
            {
                return BackgroundDomainHandshakeFailureIo;
            }
            catch (UnauthorizedAccessException)
            {
                return BackgroundDomainHandshakeFailureUnauthorized;
            }
            catch (NotSupportedException)
            {
                return BackgroundDomainHandshakeFailureUnsupported;
            }
        }

        private async Awaitable<bool> JoinBackgroundDomainHandshakeAsync(CancellationToken ct)
        {
            int state = Volatile.Read(ref _backgroundDomainHandshakeState);
            if (state == BackgroundDomainHandshakeIdle)
                return true;

            float watchdogStartTime = Time.realtimeSinceStartup;

            while (state == BackgroundDomainHandshakeRunning)
            {
                if (ct.IsCancellationRequested)
                    return false;

                if (Time.realtimeSinceStartup - watchdogStartTime > 2f)
                {
                    Debug.LogWarning("[GameBootstrapper] BackgroundDomainHandshake timeout. Bypassing to prevent deadlock.");
                    Volatile.Write(ref _backgroundDomainHandshakeState, BackgroundDomainHandshakeFailed);
                    return true;
                }

                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync();
                state = Volatile.Read(ref _backgroundDomainHandshakeState);
            }

            if (state == BackgroundDomainHandshakeFailed)
            {
                int failureCode = Volatile.Read(ref _backgroundDomainHandshakeFailureCode);
                if (failureCode == BackgroundDomainHandshakeFailureNone)
                    return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                switch (failureCode)
                {
                    case BackgroundDomainHandshakeFailureInvalidPath:
                        Debug.LogError("[GameBootstrapper] Background domain handshake invalid telemetry path.");
                        break;
                    case BackgroundDomainHandshakeFailureIo:
                        Debug.LogError("[GameBootstrapper] Background domain handshake IO failure.");
                        break;
                    case BackgroundDomainHandshakeFailureUnauthorized:
                        Debug.LogError("[GameBootstrapper] Background domain handshake unauthorized.");
                        break;
                    case BackgroundDomainHandshakeFailureUnsupported:
                        Debug.LogError("[GameBootstrapper] Background domain handshake unsupported path.");
                        break;
                    default:
                        Debug.LogError("[GameBootstrapper] Background domain handshake failed.");
                        break;
                }
#endif
                return false;
            }

            return true;
        }

        private static void ApplyMemoryGate(in HectonHardwareProfile hardwareProfile)
        {
            if (hardwareProfile.SystemMemoryMegabytes < LowMemorySystemThresholdMb ||
                hardwareProfile.GraphicsMemoryMegabytes <= LowMemoryVramThresholdMb)
            {
                GlobalRegistry.FlagFallbackLowMemoryProfile();
            }
        }

        private static void InspectPreviousBootState()
        {
            string path = HectonPersistentPathPolicy.CombineFile(BootStateFileName);
            if (!File.Exists(path))
                return;

            Span<byte> record = stackalloc byte[BootStateRecordBytes];
            int bytesRead = 0;
            try
            {
                using FileStream stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    BootStateRecordBytes,
                    FileOptions.SequentialScan);

                while (bytesRead < BootStateRecordBytes)
                {
                    int read = stream.Read(record.Slice(bytesRead, BootStateRecordBytes - bytesRead));
                    if (read <= 0)
                        return;

                    bytesRead += read;
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            uint magic = ReadUInt32(record, 0);
            ushort version = ReadUInt16(record, 4);
            if (magic != BootStateMagic || version != BootStateVersion)
                return;

            BootStateMarker marker = (BootStateMarker)record[6];
            if (marker == BootStateMarker.Complete)
                return;

            _bootStateSafeModeRequested = true;
            GlobalRegistry.RequestSafeModeBoot();
        }

        private static unsafe void WriteBootStateRecord(
            BootStateMarker marker,
            BootstrapPhase phase,
            GlobalRegistryServiceSlot serviceSlot)
        {
            string absolutePath = HectonPersistentPathPolicy.CombineFile(BootStateFileName);
            HectonPersistentPathPolicy.EnsureParentDirectory(absolutePath);
            byte* data = stackalloc byte[BootStateRecordBytes];
            UnsafeUtility.MemClear(data, BootStateRecordBytes);
            WriteUInt32(data, 0, BootStateMagic);
            WriteUInt16(data, 4, BootStateVersion);
            data[6] = (byte)marker;
            data[7] = (byte)phase;
            data[8] = serviceSlot == GlobalRegistryServiceSlot.Unknown ? byte.MaxValue : (byte)serviceSlot;
            WriteUInt32(data, 12, _registryCoreReadyChecksum);
            WriteUInt32(data, 16, GlobalRegistry.ActiveServiceTypeHash);
            WriteUInt64(data, 20, unchecked((ulong)DateTime.UtcNow.Ticks));
            data[28] = _bootStateSafeModeRequested ? (byte)1 : (byte)0;
            data[29] = (byte)GlobalRegistry.ActiveBootProfile;
            if (AsyncWriteManager.WriteAll(absolutePath, data, BootStateRecordBytes, out _))
                _ = AsyncWriteManager.FlushCriticalSavePath(absolutePath, BootStateRecordBytes, out _);
        }

        private static uint CalculateRegistryActiveServiceTypeHash()
        {
            uint hash = GlobalRegistry.CalculateActiveServiceTypeFnv1a();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hash == 0u)
                Debug.LogError("[GameBootstrapper] BIOS integrity checksum resolved to zero.");
#endif
            return hash;
        }

        private void ShutdownServicesInReverseBootstrapOrder()
        {
            if (_bootstrapExecutionOrderCount <= 0)
                TryBuildBootstrapDependencyExecutionOrder(_bootstrapExecutionOrder, out _bootstrapExecutionOrderCount);

            for (int index = _bootstrapExecutionOrderCount - 1; index >= 0; index--)
            {
                GlobalRegistryServiceSlot slot = ResolveRegistrySlotForBootstrapNode(_bootstrapExecutionOrder[index]);
                if (slot != GlobalRegistryServiceSlot.Unknown)
                    GlobalRegistry.ShutdownRegisteredServiceSlot(slot);
            }

            GlobalRegistry.ShutdownRegisteredServicesInReverseSlotOrder();
        }

        private static GlobalRegistryServiceSlot ResolveRegistrySlotForBootstrapNode(BootstrapDependencyNode node)
        {
            switch (node)
            {
                case BootstrapDependencyNode.SystemDispatcher: return GlobalRegistryServiceSlot.Dispatcher;
                case BootstrapDependencyNode.GameTickManager: return GlobalRegistryServiceSlot.TickManager;
                case BootstrapDependencyNode.SaveManager: return GlobalRegistryServiceSlot.Save;
                case BootstrapDependencyNode.ObjectPoolManager: return GlobalRegistryServiceSlot.ObjectPool;
                case BootstrapDependencyNode.RenderDispatcher: return GlobalRegistryServiceSlot.RenderDispatcher;
                case BootstrapDependencyNode.SceneRuntimeService: return GlobalRegistryServiceSlot.Scene;
                case BootstrapDependencyNode.EquipmentInteractionHandler: return GlobalRegistryServiceSlot.InteractionSignals;
                case BootstrapDependencyNode.HectonFloatingOrigin: return GlobalRegistryServiceSlot.FloatingOriginRuntime;
                case BootstrapDependencyNode.GlobalPhysicsStateManager: return GlobalRegistryServiceSlot.PhysicsStateManager;
                case BootstrapDependencyNode.PhysicsApplySystem: return GlobalRegistryServiceSlot.Physics;
                // Debris, not DebrisComputeRuntime: the startup graph node this bootstrap node was built from is
                // GlobalRegistryServiceSlot.Debris (BootstrapRegistryCycleValidator._startupNodes). Reporting the
                // compute slot here wrote boot-state records and reverse-order shutdown against a slot that is
                // not in the startup graph.
                case BootstrapDependencyNode.DebrisManager: return GlobalRegistryServiceSlot.Debris;
                case BootstrapDependencyNode.EnvironmentRuntimeContextService: return GlobalRegistryServiceSlot.Environment;
                case BootstrapDependencyNode.OceanKinematicsRuntimeService: return GlobalRegistryServiceSlot.OceanKinematics;
                case BootstrapDependencyNode.EcosystemDirector: return GlobalRegistryServiceSlot.EcosystemDirector;
                case BootstrapDependencyNode.FaunaSimulation: return GlobalRegistryServiceSlot.FaunaSimulation;
                case BootstrapDependencyNode.SpatialAudioManager: return GlobalRegistryServiceSlot.Audio;
                case BootstrapDependencyNode.NativeInputManager: return GlobalRegistryServiceSlot.NativeInputManagerRuntime;
                case BootstrapDependencyNode.InputDispatcher: return GlobalRegistryServiceSlot.Input;
                case BootstrapDependencyNode.PlayerRuntimeContextService: return GlobalRegistryServiceSlot.Player;
                case BootstrapDependencyNode.PlayerInventoryManager: return GlobalRegistryServiceSlot.PlayerInventory;
                case BootstrapDependencyNode.PlayerActionRuntime: return GlobalRegistryServiceSlot.PlayerActionRuntime;
                case BootstrapDependencyNode.PlayerSensoryManager: return GlobalRegistryServiceSlot.PlayerSensory;
                case BootstrapDependencyNode.PowerGridManager: return GlobalRegistryServiceSlot.PowerGrid;
                case BootstrapDependencyNode.ConstructionManager: return GlobalRegistryServiceSlot.Logistics;
                case BootstrapDependencyNode.ConnectionSplineBatchRenderer: return GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime;
                case BootstrapDependencyNode.BeaconNetworkSystem: return GlobalRegistryServiceSlot.BeaconNetworkRuntime;
                case BootstrapDependencyNode.ModWorldPersistenceManager: return GlobalRegistryServiceSlot.ModWorldPersistenceRuntime;
                default: return GlobalRegistryServiceSlot.Unknown;
            }
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
        {
            return (uint)(data[offset] |
                          (data[offset + 1] << 8) |
                          (data[offset + 2] << 16) |
                          (data[offset + 3] << 24));
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static unsafe void WriteUInt16(byte* data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static unsafe void WriteUInt32(byte* data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteUInt64(byte* data, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
                data[offset + i] = (byte)(value >> (i * 8));
        }

#if UNITY_INCLUDE_TESTS
        private static bool ResolveUnityTestRunnerProcess()
        {
            string[] args = System.Environment.GetCommandLineArgs(); // COLD ALLOC: string[] — Unity Test Framework process probe — owner: GameBootstrapper
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, "-runTests", StringComparison.Ordinal) ||
                    string.Equals(arg, "-runEditorTests", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
#endif

        /// <summary>
        /// Re-resolves the six extended service slots that no installer owns, publishing any owner it finds.
        /// Holds the scene-runtime publication gate across the whole pass.
        /// </summary>
        /// <remarks>
        /// The gate is owned here rather than left to the caller. Each of the six helpers ends in a
        /// GlobalRegistry.RegisterX call, so each one is a publication, and a publication past
        /// RegistryPhase.Ready throws CriticalBootException without a token. Of the two call sites only the
        /// one at the end of the bootstrap sequence runs before Ready; the other fires from
        /// HandleSceneLoadedGuard behind an _isBootstrapComplete check, which is by definition after it.
        ///
        /// That second site did not throw only because SceneRuntimeService.LoadSceneAsync happens to hold a
        /// gate across the sceneLoaded callback it arrives on. Depending on an unrelated caller's incidental
        /// gate is the same implicit coupling that left ten meta/economy/ecosystem owners unregistered until
        /// the installer block got a gate of its own, and it breaks the moment a scene loads through a route
        /// that opens no gate.
        ///
        /// The cascade is what makes this worth guarding instead of documenting: the helpers run in sequence
        /// with no try/catch between them, so the first slot whose owner actually exists throws and the
        /// remaining helpers never run. A pass that covers zero to five of six slots depending on which
        /// owners happen to be alive is not coverage.
        ///
        /// Gate depth is Interlocked-counted, so nesting inside a caller that already holds one is safe. The
        /// ready-lock invariant is untouched: IsSceneRuntimeHotSwapSlot still hard-denies every core BIOS
        /// slot regardless of depth, and none of these six is one.
        /// </remarks>
        private static void EnsureExtendedRegistryCoverageForActiveScene()
        {
            bool extendedCoverageGateOpen = false;
            try
            {
                GlobalRegistry.BeginSceneRuntimePublicationGate();
                extendedCoverageGateOpen = true;

                TryEnsureThermodynamicsRegistryCoverage();
                TryEnsureLogisticsRegistryCoverage();
                TryEnsureWorldGenRegistryCoverage();
                TryEnsureEncounterDirectorRegistryCoverage();
                TryEnsureQuestRegistryCoverage();
                TryEnsureProceduralSwayRegistryCoverage();
            }
            finally
            {
                if (extendedCoverageGateOpen)
                    GlobalRegistry.EndSceneRuntimePublicationGate();
            }
        }

        private static void TryEnsureThermodynamicsRegistryCoverage()
        {
            if (GlobalRegistry.ThermodynamicsService != null)
                return;

            AbyssalThermalManager manager = GlobalRegistry.Thermodynamics;
            if (manager != null)
                GlobalRegistry.RegisterThermodynamicsRuntime(manager);
        }

        private static void TryEnsureLogisticsRegistryCoverage()
        {
            if (GlobalRegistry.Logistics != null)
                return;

            ConstructionManager manager = ConstructionManager.ActiveRuntimeInstance;
            if (manager != null)
                GlobalRegistry.RegisterLogisticsService(manager);
        }

        private static void TryEnsureWorldGenRegistryCoverage()
        {
            if (GlobalRegistry.WorldGen != null)
                return;

            WorldProceduralScatterDirector director = null;
            WorldRuntimeReferenceUtility.TryResolveWorldProceduralScatterDirector(ref director);
            if (director != null)
                GlobalRegistry.RegisterWorldGenService(director);
        }

        private static void TryEnsureEncounterDirectorRegistryCoverage()
        {
            if (GlobalRegistry.EncounterDirector != null)
                return;

            HectonDirectorAI director = null;
            HectonDirectorAI.TryResolveActiveRuntime(ref director);
            if (director != null)
                GlobalRegistry.RegisterEncounterDirectorService(director);
        }

        private static void TryEnsureQuestRegistryCoverage()
        {
            if (GlobalRegistry.QuestSystem != null)
                return;

            QuestManager questManager = QuestManager.ActiveRuntimeInstance;
            if (questManager != null)
                GlobalRegistry.RegisterQuestRuntime(questManager);
        }

        /// <summary>
        /// Asks the live FloraInteractionManager to re-publish itself when the IProceduralSwayDirector slot
        /// is empty. Coverage only; the owner holds the registration.
        /// </summary>
        /// <remarks>
        /// This is the only one of the six helpers that does NOT call GlobalRegistry.RegisterX itself, and
        /// deliberately so. TryResolveFloraInteractionManager returns FloraInteractionManager
        /// .ActiveRuntimeInstance, i.e. the same object that already publishes itself from its own OnEnable,
        /// so a direct RegisterProceduralSwayDirector call here was a second registration door onto one slot.
        /// The owner tracks whether the registry currently holds it, and that flag drives its OnDisable and
        /// OnDestroy release; a slot filled behind its back leaves the flag false and strands a destroyed
        /// MonoBehaviour in the registry. Publishing through the owner's own door keeps register and
        /// unregister on one code path. Coverage semantics are unchanged: the owner's method is idempotent
        /// and re-publishes when the slot is empty, which is the only state this helper is reached in.
        /// </remarks>
        private static void TryEnsureProceduralSwayRegistryCoverage()
        {
            if (GlobalRegistry.ProceduralSwayDirector != null)
                return;

            FloraInteractionManager manager = null;
            WorldRuntimeReferenceUtility.TryResolveFloraInteractionManager(ref manager);
            if (manager != null)
                manager.TryRegisterProceduralSwayDirectorService();
        }


        // L19 hop2 LIVE: FMOD updateChannels AV under dual-listener headless batch.
        // Soft-mute engine audio so PostLateUpdate audio tick cannot hard-fault the probe.
        private static void SoftMuteAudioForBatchProbe()
        {
            if (!(Application.isBatchMode || _headlessBootMode))
                return;

            // L19 hop2 LIVE: FMOD updateChannels AV under batch when zero listeners
            // (disable-all) OR dual active listeners. Keep exactly ONE muted listener
            // enabled so Unity audio has a valid sink; pause+volume=0; stop sources.
            // Do NOT AudioSettings.Reset under batch (re-inits WASAPI/FMOD mono-fatal).
            AudioListener.pause = true;
            AudioListener.volume = 0f;

            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            AudioListener kept = null;
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener == null)
                    continue;
                if (kept == null)
                {
                    kept = listener;
                    listener.enabled = true;
                    continue;
                }

                listener.enabled = false;
            }

            if (kept == null)
            {
                GameObject go = new GameObject("[H8_BatchMuteListener]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                kept = go.AddComponent<AudioListener>();
                kept.enabled = true;
            }

            AudioSource[] sources = UnityEngine.Object.FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource src = sources[i];
                if (src == null)
                    continue;
                src.Stop();
                src.mute = true;
                src.volume = 0f;
                src.enabled = false;
            }
        }

        private static void EnsureBootstrapAudioListener(Scene bootstrapScene)
        {
            // L19 hop2 LIVE: dual AudioListener (bootstrap + player) drives FMOD
            // DSPFilter::read / updateChannels AV under headless batch after STARTERGRANT.
            // Probe moment census does not need a bootstrap listener - skip under batchmode.
            if (Application.isBatchMode || _headlessBootMode)
            {
                SoftMuteAudioForBatchProbe();
                return;
            }

            if (HasActiveAudioListener(bootstrapScene))
                return;

            GameObject listenerObject = new GameObject(BootstrapAudioListenerRuntimeName); // COLD ALLOC: GameObject[1] - bootstrap-only audio listener before menu handoff - owner: GameBootstrapper
            if (bootstrapScene.IsValid())
                SceneManager.MoveGameObjectToScene(listenerObject, bootstrapScene);

            listenerObject.AddComponent<AudioListener>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeDiagnosticsTrace.WriteEvent("bootstrap.audio", "created bootstrap-only listener");
#endif
        }

        private static bool HasActiveAudioListener(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            scene.GetRootGameObjects(_bootstrapSceneRootScratch);

            for (int i = 0; i < _bootstrapSceneRootScratch.Count; i++)
            {
                GameObject root = _bootstrapSceneRootScratch[i];
                if (root == null || !root.activeInHierarchy)
                    continue;

                _bootstrapTransformScratch.Add(root.transform);
            }

            while (_bootstrapTransformScratch.Count > 0)
            {
                int lastIndex = _bootstrapTransformScratch.Count - 1;
                Transform current = _bootstrapTransformScratch[lastIndex];
                _bootstrapTransformScratch.RemoveAt(lastIndex);

                if (current == null)
                    continue;

                GameObject currentObject = current.gameObject;
                if (currentObject.activeInHierarchy &&
                    currentObject.TryGetComponent(out AudioListener listener) &&
                    listener != null &&
                    listener.enabled)
                {
                    _bootstrapSceneRootScratch.Clear();
                    _bootstrapTransformScratch.Clear();
                    return true;
                }

                int childCount = current.childCount;
                for (int i = 0; i < childCount; i++)
                    _bootstrapTransformScratch.Add(current.GetChild(i));
            }

            _bootstrapSceneRootScratch.Clear();
            _bootstrapTransformScratch.Clear();
            return false;
        }

        private static async Awaitable WaitForJobCompletionAsync(JobHandle handle, CancellationToken ct)
        {
            int waitFrames = 0;
            long waitStartTimestamp = Stopwatch.GetTimestamp();
            try
            {
                while (!handle.IsCompleted)
                {
                    ct.ThrowIfCancellationRequested();
                    if (HasWatchdogElapsed(waitStartTimestamp, BootstrapJobWaitWatchdogSeconds, out double elapsedSeconds))
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogError($"[GameBootstrapper] Job wait watchdog tripped after {waitFrames} frames ({elapsedSeconds:0.000}s). Forcing completion as cleanup barrier.");
#endif
                        break;
                    }

                    waitFrames++;
                    await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            finally
            {
                DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            }
        }

        private static void HandleFatalBootstrapException(string phaseName, Exception exception)
        {
            if (exception == null)
                return;

            WriteFatalBootstrapLog();
            BootstrapBiosErrorOverlay.Show(FatalBootOverlayMessage);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogException(exception);
#endif
        }

        private static void SetLegacyPlayerRigidbodyKinematic(Rigidbody body, bool isKinematic)
        {
            if (body == null)
                return;

            if (body.TryGetComponent(out HectonPlayerMotor playerMotor) &&
                playerMotor.HydrodynamicKccOwnsCollisionAuthority)
            {
                return;
            }

            body.isKinematic = isKinematic;
        }

        private static unsafe void WriteFatalBootstrapLog()
        {
            const int byteCount = FatalBootCrashMessageByteCount;
            string absolutePath = HectonPersistentPathPolicy.CombineFile(FatalBootCrashFileName);
            HectonPersistentPathPolicy.EnsureParentDirectory(absolutePath);
            byte* scratch = stackalloc byte[byteCount];
            WriteFatalBootstrapMarker(scratch);

            if (AsyncWriteManager.WriteAll(absolutePath, scratch, byteCount, out _))
                _ = AsyncWriteManager.FlushCriticalSavePath(absolutePath, byteCount, out _);
        }

        private static unsafe void WriteFatalBootstrapMarker(byte* scratch)
        {
            scratch[0] = 72;
            scratch[1] = 69;
            scratch[2] = 67;
            scratch[3] = 84;
            scratch[4] = 79;
            scratch[5] = 78;
            scratch[6] = 45;
            scratch[7] = 56;
            scratch[8] = 32;
            scratch[9] = 70;
            scratch[10] = 65;
            scratch[11] = 84;
            scratch[12] = 65;
            scratch[13] = 76;
            scratch[14] = 32;
            scratch[15] = 66;
            scratch[16] = 79;
            scratch[17] = 79;
            scratch[18] = 84;
            scratch[19] = 32;
            scratch[20] = 67;
            scratch[21] = 82;
            scratch[22] = 65;
            scratch[23] = 83;
            scratch[24] = 72;
            scratch[25] = 10;
            scratch[26] = 65;
            scratch[27] = 67;
            scratch[28] = 84;
            scratch[29] = 73;
            scratch[30] = 79;
            scratch[31] = 78;
            scratch[32] = 58;
            scratch[33] = 32;
            scratch[34] = 83;
            scratch[35] = 69;
            scratch[36] = 69;
            scratch[37] = 32;
            scratch[38] = 85;
            scratch[39] = 78;
            scratch[40] = 73;
            scratch[41] = 84;
            scratch[42] = 89;
            scratch[43] = 32;
            scratch[44] = 76;
            scratch[45] = 79;
            scratch[46] = 71;
            scratch[47] = 32;
            scratch[48] = 65;
            scratch[49] = 78;
            scratch[50] = 68;
            scratch[51] = 32;
            scratch[52] = 66;
            scratch[53] = 79;
            scratch[54] = 79;
            scratch[55] = 84;
            scratch[56] = 32;
            scratch[57] = 66;
            scratch[58] = 76;
            scratch[59] = 65;
            scratch[60] = 67;
            scratch[61] = 75;
            scratch[62] = 66;
            scratch[63] = 79;
            scratch[64] = 88;
            scratch[65] = 10;
        }

        private static bool IsBootstrapScene(Scene scene)
        {
            return scene.IsValid() &&
                   scene.buildIndex == 0 &&
                   string.Equals(scene.name, BootstrapSceneName, System.StringComparison.Ordinal);
        }

        private static bool IsMainMenuScene(Scene scene)
        {
            return scene.IsValid() &&
                   scene.isLoaded &&
                   string.Equals(scene.name, MainMenuSceneName, System.StringComparison.Ordinal);
        }

        private static bool IsOrbitScene(Scene scene)
        {
            return scene.IsValid() &&
                   scene.isLoaded &&
                   string.Equals(scene.name, OrbitSceneName, System.StringComparison.Ordinal);
        }

        private static bool RequiresGameplaySceneActivation(Scene scene)
        {
            bool isValid = scene.IsValid();
            bool isLoaded = scene.isLoaded;
            bool isBootstrap = IsBootstrapScene(scene);
            bool isMainMenu = IsMainMenuScene(scene);
            bool isOrbit = IsOrbitScene(scene);
            Debug.Log($"[GameBootstrapper-DEBUG] RequiresGameplaySceneActivation: {scene.name} -> isValid={isValid}, isLoaded={isLoaded}, isBootstrap={isBootstrap}, isMainMenu={isMainMenu}, isOrbit={isOrbit}");
            return isValid && isLoaded && !isBootstrap && !isMainMenu && !isOrbit;
        }

    }

    /// <summary>
    /// Silent audio fallback used when an optional audio bootstrap owner cannot initialize.
    /// </summary>
    /// <remarks>
    /// This type must never report readiness. It holds no mixer groups, no voice pool, no acoustic data and no event
    /// queue, so any consumer that believes it is ready queues audio into a black hole and gets no diagnostic. Both
    /// readiness properties are hardcoded <c>false</c> on purpose; do not "fix" a consumer that started refusing by
    /// flipping them back. Boot survival past the audio node is handled separately and explicitly by
    /// <c>GameBootstrapper._audioBootstrapNodeStubbed</c>, so honesty here costs no session.
    /// </remarks>
    internal sealed class NoOpAudioService : IAudioService
    {
        // COLD ALLOC: NoOpAudioService[1] - non-critical audio fallback for deterministic bootstrap progress - owner: GameBootstrapper
        internal static readonly NoOpAudioService Shared = new NoOpAudioService();

        /// <summary>
        /// Always <c>false</c>. This object holds no mixer groups, no voice pool and no event queue: every
        /// <c>Queue*</c> method below returns <c>false</c> and every <c>Play*</c> method is empty.
        /// </summary>
        /// <remarks>
        /// This used to be hardcoded <c>true</c>, which made the placeholder answer yes to "is audio ready" and
        /// let every consumer gate of the form <c>audioService == null || !audioService.IsInitialized</c> pass -
        /// so callers queued SFX, ambience, music and vocal warnings into methods that dropped them and reported
        /// nothing. A null object must fail a null check's intent, not satisfy its letter.
        /// <para>
        /// Bootstrap progress does NOT depend on this bit any more: the node passes through the recorded
        /// <see cref="GameBootstrapper"/> stub exemption instead. See
        /// <c>GameBootstrapper._audioBootstrapNodeStubbed</c>.
        /// </para>
        /// </remarks>
        public bool IsInitialized => false;

        /// <summary>
        /// Always <c>false</c>, overridden explicitly rather than inherited.
        /// </summary>
        /// <remarks>
        /// <c>IAudioService</c> declares a default interface implementation <c>IsAudioRuntimeReady => IsInitialized</c>
        /// (<c>Core/GlobalRegistryContracts.cs</c>). Inheriting it would make one placeholder answer the readiness
        /// question under two different property names, and a future edit to <see cref="IsInitialized"/> would move
        /// both. The real owner, <c>SpatialAudioManager</c>, overrides the same property with a five-term
        /// conjunction, so the name means "five invariants hold" there and must not mean "yes, always" here.
        /// </remarks>
        public bool IsAudioRuntimeReady => false;

        /// <inheritdoc />
        public AudioMixerGroup InterfaceGroup => null;

        /// <inheritdoc />
        public AudioMixerGroup AmbientGroup => null;

        /// <inheritdoc />
        public void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
        }

        /// <inheritdoc />
        public void PlayAtPoint(AudioClip clip, Vector3 position, float volume, float pitch, AudioMixerGroup mixerGroup)
        {
        }

        /// <inheritdoc />
        public bool QueueAudioEvent(in CoreAudioEvent audioEvent)
        {
            return false;
        }

        /// <inheritdoc />
        public bool QueuePrologueAudioTransition(in AudioTransitionState state)
        {
            return false;
        }

        /// <inheritdoc />
        public bool QueueSoundEmissionSignal(in SoundEmissionSignal signal)
        {
            return false;
        }

        /// <inheritdoc />
        public bool QueueHullStressSignal(in HullStressSignal signal)
        {
            return false;
        }

        /// <inheritdoc />
        public bool QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal)
        {
            return false;
        }

        /// <inheritdoc />
        public void PlayStatic2D(AudioClip clip, float volume = 1f)
        {
        }

        /// <inheritdoc />
        public void PlayStatic2D(AudioClip clip, float volume, AudioMixerGroup mixerGroup)
        {
        }

        /// <summary>
        /// Silent fallback route for callers that still request one-shot playback while audio bootstrap is unavailable.
        /// </summary>
        public void PlayOneShot(AudioClip clip)
        {
        }

        /// <summary>
        /// Silent fallback route for callers that still request one-shot playback while audio bootstrap is unavailable.
        /// </summary>
        public void PlayOneShot(AudioClip clip, float volume)
        {
        }

        /// <inheritdoc />
        public bool TryGetAcousticRadarPayload(out NativeArray<float>.ReadOnly radialIntensityBins, out int radialResolution)
        {
            radialIntensityBins = default;
            radialResolution = 0;
            return false;
        }

        /// <inheritdoc />
        public bool TryUploadAcousticRadarPayload(Texture2D destination, out int uploadedSampleCount, out float peakIntensity)
        {
            uploadedSampleCount = 0;
            peakIntensity = 0f;
            return false;
        }

        /// <inheritdoc />
        public bool TryGetAcousticRadarGridPayload(
            out NativeArray<float>.ReadOnly energyGrid,
            out int azimuthBins,
            out int elevationBins,
            out GraphicsBuffer gridBuffer)
        {
            energyGrid = default;
            azimuthBins = 0;
            elevationBins = 0;
            gridBuffer = null;
            return false;
        }

        /// <inheritdoc />
        public bool TryEmitModAcousticPing(UnityEngine.Vector3 runtimePosition, float intensity01)
        {
            return false;
        }

        /// <inheritdoc />
        public void StopAll()
        {
        }
    }

    /// <summary>
    /// Data-only fauna simulation sentinel for headless boots before world fauna presentation exists.
    /// </summary>
    internal sealed class DemiurgeFaunaSimulationService : IFaunaSim, IServiceHeartbeat, IServiceShutdown
    {
        // COLD ALLOC: DemiurgeFaunaSimulationService[1] - headless data-only fauna simulation sentinel - owner: GameBootstrapper
        internal static readonly DemiurgeFaunaSimulationService Shared = new DemiurgeFaunaSimulationService();

        /// <inheritdoc />
        public bool IsReady => true;

        /// <inheritdoc />
        public int ResidentSlotCapacity => 0;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => ServiceHeartbeatState.Ready;

        /// <inheritdoc />
        public bool IsServiceReady => true;

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
        }
    }

    internal static class BootstrapBiosErrorOverlay
    {
        internal static bool Show(string message)
        {
            return HardwareErrorCanvas.Show(message);
        }

        internal static void Hide()
        {
            HardwareErrorCanvas.Hide();
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal sealed class BootstrapPresentationFallbackRuntime : MonoBehaviour
    {
        private Material _abyss;
        private Material _hull;
        private Material _cyan;
        private Material _amber;
        private Material _glass;

        internal void Register(
            Material abyss,
            Material hull,
            Material cyan,
            Material amber,
            Material glass)
        {
            _abyss = abyss;
            _hull = hull;
            _cyan = cyan;
            _amber = amber;
            _glass = glass;
        }

        private void OnDestroy()
        {
            DestroyMaterial(_abyss);
            DestroyMaterial(_hull);
            DestroyMaterial(_cyan);
            DestroyMaterial(_amber);
            DestroyMaterial(_glass);
            _abyss = null;
            _hull = null;
            _cyan = null;
            _amber = null;
            _glass = null;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }
    }
#endif

    [DisallowMultipleComponent]
    internal sealed class HardwareErrorCanvas : MonoBehaviour
    {
        private const string OverlayRootName = "[HardwareErrorCanvas]";
        private const int OverlaySortingOrder = 32767;

        private static HardwareErrorCanvas _runtimeOverlay;

        private TMP_Text _messageText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _runtimeOverlay = null;
        }

        internal static bool Show(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[BIOS ERROR OVERLAY] " + message);
#endif

            if (GameBootstrapper.IsHeadlessBootMode || Application.isBatchMode)
            {
                // One-time critical init failure; headless cannot render the BIOS canvas.
                return false;
            }

            try
            {
                HardwareErrorCanvas overlay = EnsureInstance();
                if (overlay == null)
                    return false;

                return overlay.ApplyMessage(message);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
                return false;
            }
        }

        internal static void Hide()
        {
            if (_runtimeOverlay == null)
                return;

            GameObject root = _runtimeOverlay.gameObject;
            _runtimeOverlay = null;

            if (root == null)
                return;

            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);
        }

        private static HardwareErrorCanvas EnsureInstance()
        {
            if (_runtimeOverlay != null)
                return _runtimeOverlay;

            GameObject runtimeRoot = new GameObject(OverlayRootName); // COLD ALLOC: GameObject[1] - hardware-error BIOS fallback overlay root - owner: HardwareErrorCanvas
            GameBootstrapper.EnsureRuntimeInstance();
            HardwareErrorCanvas overlay = runtimeRoot.AddComponent<HardwareErrorCanvas>();
            GameBootstrapper.PersistRuntimeService(overlay);
            return overlay;
        }

        private void Awake()
        {
            if (_runtimeOverlay != null && _runtimeOverlay != this)
            {
                Destroy(gameObject);
                return;
            }

            _runtimeOverlay = this;

            if (Application.isPlaying)
                GameBootstrapper.PersistRuntimeService(this);

            BuildVisualTree();
        }

        private bool ApplyMessage(string message)
        {
            if (_messageText == null)
                BuildVisualTree();

            if (_messageText == null)
                return false;

            TmpTextNoAlloc.Set(_messageText, message);
            return true;
        }

        private void BuildVisualTree()
        {
            if (_messageText != null)
                return;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            gameObject.AddComponent<CanvasScaler>();

            Image background = gameObject.AddComponent<Image>();
            background.color = new Color(0.002f, 0.012f, 0.018f, 0.96f);

            GameObject textRoot = new GameObject("Message"); // COLD ALLOC: GameObject[1] - hardware-error BIOS message node - owner: HardwareErrorCanvas
            textRoot.transform.SetParent(transform, false);

            RectTransform rectTransform = textRoot.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(72f, 72f);
            rectTransform.offsetMax = new Vector2(-72f, -72f);

            TextMeshProUGUI text = textRoot.AddComponent<TextMeshProUGUI>();
            text.fontStyle = FontStyles.Bold;
            text.fontSize = 28f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = new Color(0.74f, 1.00f, 0.96f, 1f);
            text.richText = false;
            text.raycastTarget = false;

            _messageText = text;
        }
    }
}

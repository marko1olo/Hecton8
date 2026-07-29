using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Meta;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Decryption Spectrogram Panel")]
    public sealed class PDADecryptionSpectrogramPanel : MonoBehaviour, ISlowTickable, ILateFrameTickable, IDisposable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001PDADecryptionSpectrogramPanelSignalPushDropCount;
        private const int HighPointCount = 128;
        private const int LowPointCount = 32;
        private const int StageCount = 3;
        private const int TelemetryCapacity = 300;
        private const float UnlockErrorThreshold = 0.05f;
        private const float UnlockHoldSeconds = 2f;
        private const float Hash24ToUnit = 0.00000005960464833f;
        private const uint DefaultArtifactHash = 0x534F5648u; // SOVH
        private const uint DefaultBlueprintHash = 0x46485455u; // FHTU
        private const uint ToolHash = 0x53434E52u; // SCNR
        private const float ShaderFloatEpsilon = 0.0001f;
        private const string TelemetryDumpPath = "Docs/AgentLogs/Dump_MINIGAME_FREQUENCY_TUNING.bin";
        private const string TelemetryDumpPayloadLabel = "pdaDecryptionTelemetryDumpPayload";
        private const SystemID VaultOwnerSystemId = SystemID.UI;

        private static readonly int LocalToWorldId = Shader.PropertyToID("_HectonFrequencyTuningLocalToWorld");
        private static readonly int TubeRadiusId = Shader.PropertyToID("_HectonFrequencyTuningTubeRadius");
        private static readonly int TimeErrorStageId = Shader.PropertyToID("_HectonFrequencyTuningTimeErrorStage");
        private static readonly int WaveScalarsId = Shader.PropertyToID("_HectonFrequencyTuningWaveScalars");
        private static readonly int WaveLayoutId = Shader.PropertyToID("_HectonFrequencyTuningWaveLayout");
        private static readonly int ErrorGlobalId = Shader.PropertyToID("_HectonFrequencyTuningError01");
        [Header("PDA Surface")]
        [SerializeField] private Transform surfaceAnchor;
        [SerializeField] private Vector3 localSurfaceOffset = new Vector3(0f, 0f, -0.002f);
        [SerializeField] private Vector2 localSurfaceSize = new Vector2(0.22f, 0.085f);
        [SerializeField, Min(0.0005f)] private float tubeRadius = 0.003f;
        [SerializeField] private int renderLayer;

        [Header("Renderer")]
        [SerializeField] private Material waveMaterial;
        [SerializeField] private Mesh waveMesh;
        [FormerlySerializedAs("lowTierVideoMemoryMb")]
        [SerializeField, Min(256)] private int minimumQualityVideoMemoryMb = 2048;

        [Header("Input")]
        [SerializeField, Min(0.1f)] private float inputLerpSpeed = 8f;
        [SerializeField, Min(0.1f)] private float playerFrequencyMin = 0.85f;
        [SerializeField, Min(0.1f)] private float playerFrequencyMax = 4.25f;
        [SerializeField, Range(0.05f, 1f)] private float playerAmplitudeMin = 0.15f;
        [SerializeField, Range(0.05f, 1f)] private float playerAmplitudeMax = 0.95f;

        [Header("Difficulty")]
        [SerializeField, Min(0f)] private float hardDriftAmplitude = 0.18f;
        [SerializeField, Min(0.01f)] private float hardDriftFrequency = 0.17f;
        [SerializeField, Min(0.02f)] private float feedbackIntervalSeconds = 0.1f;

        private VaultGenerationHandle<FrequencyTuningStageTarget> _stageTargetsHandle;
        private VaultGenerationHandle<FrequencyTuningTelemetryEntry> _telemetryRingHandle;
        private GraphicsBuffer _argsBuffer;
        private GraphicsBuffer _argsBufferA;
        private GraphicsBuffer _argsBufferB;
        private MaterialPropertyBlock _waveMaterialProperties;
        private IDataVault _cachedDataVault;
        private IInputService _cachedInputService;
        private Material _resolvedMaterial;
        private Mesh _resolvedMesh;
        private int _pointCount = HighPointCount;
        private int _waveSegmentCount = HighPointCount - 1;
        private int _gpuSegmentCapacity = (HighPointCount - 1) * 2;
        private int _lastArgsInstanceCount = -1;
        private int _argsBufferWriteIndex;
        private int _telemetryCursor;
        private int _stageIndex;
        private int _lockedStageMask;
        private int _lastScannerToolSignalSequence;
        private uint _artifactHash = DefaultArtifactHash;
        private uint _blueprintHash = DefaultBlueprintHash;
        private uint _stageSeed = DefaultArtifactHash;
        private float _playerFrequency = 1.6f;
        private float _playerAmplitude = 0.55f;
        private float _targetFrequency = 1.6f;
        private float _targetAmplitude = 0.55f;
        private float _currentError01 = 1f;
        private float _holdTimerSeconds;
        private float _lastTickDeltaTime;
        private float _lastTickUnscaledTime;
        private float _nextFeedbackTime;
        private float _lastShaderError = float.PositiveInfinity;
        private float _pendingWaveError01 = 1f;
        private uint _lastTickFrame;
        private bool _scannerActive;
        private bool _unlocked;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _nativeReady;
        private bool _graphicsReady;
        private bool _disposed;
        private bool _registeredHotSwapListener;
        private bool _telemetryDumpQueued;
        private bool _presentationFeedbackClearRequested;
        private bool _waveResultDirty;
        private bool _nativeResourcesDirty;
        private bool _graphicsResourcesDirty;
        private bool _missingWaveDrawAssetsAnnounced;
        private float _cachedQualityWeight01 = 1f;
        private float _cachedVideoMemoryQualityClamp01 = 1f;

        private void Awake()
        {
            CacheGraphicsCapabilitiesCold();
            if (surfaceAnchor == null)
                surfaceAnchor = transform;
        }

        private void OnEnable()
        {
            _disposed = false;
            CacheGraphicsCapabilitiesCold();
            RefreshCachedRegistryServices();
            TryRegisterHotSwapListener();
            EnsureNativeResources();
            EnsureGraphicsResources();
            ResetRuntimeState(_artifactHash, _blueprintHash);
            TryRegisterTickHandlers();
        }

        private void OnDisable()
        {
            TryUnregisterTickHandlers();
            TryUnregisterHotSwapListener();
            ClearPresentationFeedback();
            FlushQueuedTelemetryDump();
            DisposeNativeResources();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            TryUnregisterTickHandlers();
            TryUnregisterHotSwapListener();
            FlushQueuedTelemetryDump();
            DisposeNativeResources();
            DisposeGraphicsResources();
        }

        private void AdvanceDecryptionPresentationState(float deltaTime)
        {
            RefreshCachedQualityPolicy(rebuildResourcesOnPointChange: false);

            if (!_nativeReady)
            {
                _nativeResourcesDirty = true;
                return;
            }

            if (!_graphicsReady)
                _graphicsResourcesDirty = true;
            if (!_graphicsReady)
                return;

            DrainScannerToolSignals();
            if (!_scannerActive || _unlocked)
            {
                QueuePresentationFeedbackClear();
                return;
            }

            float safeDeltaTime = SanitizePositive(deltaTime, 0f);
            _lastTickDeltaTime = safeDeltaTime;
            _lastTickUnscaledTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            _lastTickFrame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            SampleInputState(safeDeltaTime);
            ResolveTargetForCurrentStage(out _targetFrequency, out _targetAmplitude);
            QueueWaveResult(EvaluateScalarWaveError());
        }

        public void LateFrameTick()
        {
            AdvanceDecryptionPresentationState(SystemDispatcher.CurrentFrameDeltaTime);

            if (_nativeResourcesDirty || !_nativeReady)
            {
                _nativeResourcesDirty = false;
                return;
            }

            if (!_nativeReady)
                return;

            if (_graphicsResourcesDirty || !_graphicsReady)
            {
                _graphicsResourcesDirty = false;
                return;
            }

            if (!_graphicsReady)
                return;

            if (_presentationFeedbackClearRequested)
                ClearPresentationFeedback();

            if (_waveResultDirty)
            {
                _waveResultDirty = false;
                CommitWaveResult(_lastTickDeltaTime, _pendingWaveError01);
            }

            if (_scannerActive && !_unlocked)
                RenderWaveMesh();
        }

        public void SlowTick()
        {
            FlushQueuedTelemetryDump();
        }

        public void SubmitNormalized(float frequency01, float amplitude01)
        {
            float safeFrequency01 = Sanitize01(frequency01);
            float safeAmplitude01 = Sanitize01(amplitude01);
            _playerFrequency = math.lerp(playerFrequencyMin, math.max(playerFrequencyMin, playerFrequencyMax), safeFrequency01);
            _playerAmplitude = math.lerp(playerAmplitudeMin, math.max(playerAmplitudeMin, playerAmplitudeMax), safeAmplitude01);
        }

        private void EnsureNativeResources()
        {
            if (_nativeReady)
                return;

            _pointCount = ResolvePointCount();
            _waveSegmentCount = math.max(1, _pointCount - 1);
            _gpuSegmentCapacity = _waveSegmentCount * 2;

            if (!TryAcquireStageTargetsWrite(out IDataVault stageTargetsWriteVault, out NativeArray<FrequencyTuningStageTarget> stageTargets))
                return;

            try
            {
                ClearStageTargets(stageTargets);
            }
            finally
            {
                ReleaseVaultWriteBuffer(stageTargetsWriteVault, in _stageTargetsHandle, BufferID.PdaFrequencyStageTargets);
            }

            if (!TryAcquireTelemetryRingWrite(out IDataVault telemetryWriteVault, out NativeArray<FrequencyTuningTelemetryEntry> telemetryRing))
                return;

            try
            {
                ClearTelemetryRing(telemetryRing);
                _nativeReady = true;
            }
            finally
            {
                ReleaseVaultWriteBuffer(telemetryWriteVault, in _telemetryRingHandle, BufferID.PdaFrequencyTelemetryRing);
            }
        }

        private bool TryAcquireStageTargetsWrite(out IDataVault writeVault, out NativeArray<FrequencyTuningStageTarget> stageTargets)
        {
            return TryAcquireVaultWriteBuffer(
                ref _stageTargetsHandle,
                BufferID.PdaFrequencyStageTargets,
                StageCount,
                NativeArrayOptions.ClearMemory,
                out writeVault,
                out stageTargets);
        }

        private bool TryAcquireTelemetryRingWrite(out IDataVault writeVault, out NativeArray<FrequencyTuningTelemetryEntry> telemetryRing)
        {
            return TryAcquireVaultWriteBuffer(
                ref _telemetryRingHandle,
                BufferID.PdaFrequencyTelemetryRing,
                TelemetryCapacity,
                NativeArrayOptions.ClearMemory,
                out writeVault,
                out telemetryRing);
        }

        private bool TryAcquireVaultWriteBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out IDataVault writeVault,
            out NativeArray<T> buffer) where T : unmanaged
        {
            writeVault = null;
            buffer = default;
            IDataVault vault = _cachedDataVault;
            if (vault == null || requiredLength <= 0 || vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (IsExactVaultHandle(in handle, bufferId) &&
                TryAcquireExistingVaultWriteBuffer(vault, in handle, requiredLength, out writeVault, out buffer))
            {
                return true;
            }

            if (IsExactVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            if (vault.IsCompactionFenceActive)
            {
                handle = default;
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, VaultOwnerSystemId, options);
            return IsExactVaultHandle(in handle, bufferId) &&
                   TryAcquireExistingVaultWriteBuffer(vault, in handle, requiredLength, out writeVault, out buffer);
        }

        private static bool TryAcquireExistingVaultWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out IDataVault writeVault,
            out NativeArray<T> buffer) where T : unmanaged
        {
            writeVault = null;
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out buffer))
            {
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
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
                    buffer = default;
                }
            }
        }

        private static void ReleaseVaultWriteBuffer<T>(
            IDataVault writeVault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : unmanaged
        {
            if (writeVault != null && IsExactVaultHandle(in handle, bufferId))
                writeVault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
        }

        private bool TryReadStageTarget(int index, out FrequencyTuningStageTarget target)
        {
            target = default;
            IDataVault vault = _cachedDataVault;
            if (vault == null ||
                index < 0 ||
                index >= StageCount ||
                !IsExactVaultHandle(in _stageTargetsHandle, BufferID.PdaFrequencyStageTargets) ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in _stageTargetsHandle, out NativeArray<FrequencyTuningStageTarget>.ReadOnly stageTargets) ||
                vault.IsCompactionFenceActive ||
                !stageTargets.IsCreated ||
                stageTargets.Length <= index)
            {
                return false;
            }

            target = stageTargets[index];
            return true;
        }

        private bool TryReadTelemetryEntry(int index, out FrequencyTuningTelemetryEntry entry)
        {
            entry = default;
            IDataVault vault = _cachedDataVault;
            if (vault == null ||
                index < 0 ||
                index >= TelemetryCapacity ||
                !IsExactVaultHandle(in _telemetryRingHandle, BufferID.PdaFrequencyTelemetryRing) ||
                vault.IsCompactionFenceActive ||
                !vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<FrequencyTuningTelemetryEntry>.ReadOnly telemetryRing) ||
                vault.IsCompactionFenceActive ||
                !telemetryRing.IsCreated ||
                telemetryRing.Length <= index)
            {
                return false;
            }

            entry = telemetryRing[index];
            return true;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : unmanaged
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : unmanaged
        {
            if (vault != null && IsExactVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static void ClearStageTargets(NativeArray<FrequencyTuningStageTarget> stageTargets)
        {
            for (int i = 0; i < stageTargets.Length; i++)
                stageTargets[i] = default;
        }

        private static void ClearTelemetryRing(NativeArray<FrequencyTuningTelemetryEntry> telemetryRing)
        {
            for (int i = 0; i < telemetryRing.Length; i++)
                telemetryRing[i] = default;
        }

        /// <summary>
        /// Brings the indirect-draw lane up, or reports the authoring gap once without throwing.
        /// </summary>
        /// <remarks>
        /// The four <c>UnityEngine.Assertions.Assert</c> calls removed from the middle of this method THREW -
        /// nothing under Assets sets <c>Assert.raiseExceptions = false</c> - and the only caller reaches here
        /// mid-<see cref="OnEnable"/> (:134), before <see cref="ResetRuntimeState"/> (:135) and
        /// <see cref="TryRegisterTickHandlers"/> (:136). An unassigned inspector slot therefore threw out of the
        /// Unity message and left the decryption panel with un-reset runtime state and no late-frame or slow
        /// tick registration at all, so the whole frequency-tuning minigame was inert for the session rather
        /// than merely unrendered.
        ///
        /// The asserts guarded nothing the surrounding code did not already handle: <c>_graphicsReady</c> stays
        /// false on this branch and both tick entry points bail on it -
        /// <see cref="AdvanceDecryptionPresentationState"/> at :176-179 and <see cref="LateFrameTick"/> at
        /// :210-217 - which is the complete degradation path the throw made unreachable.
        /// </remarks>
        private void EnsureGraphicsResources()
        {
            EnsureWaveMaterialPropertiesCold();
            if (_graphicsReady &&
                _argsBufferA != null &&
                _argsBufferB != null &&
                _resolvedMaterial != null &&
                _resolvedMesh != null)
            {
                return;
            }

            bool materialAssigned = waveMaterial != null;
            bool meshAssigned = waveMesh != null;
            bool authoredMaterialValid = materialAssigned && waveMaterial.enableInstancing;
            bool authoredMeshValid = meshAssigned && waveMesh.subMeshCount > 0 && waveMesh.GetIndexCount(0) > 0u;
            if (!authoredMaterialValid || !authoredMeshValid)
            {
                _resolvedMaterial = null;
                _resolvedMesh = null;
                _graphicsReady = false;

                // Report LAST and once. OnEnable continues to ResetRuntimeState and TryRegisterTickHandlers
                // after this returns, so a future re-introduced throw here can no longer strand the panel.
                if (!_missingWaveDrawAssetsAnnounced)
                {
                    _missingWaveDrawAssetsAnnounced = true;
                    LogInvalidWaveDrawAssets(
                        materialAssigned,
                        authoredMaterialValid,
                        meshAssigned,
                        authoredMeshValid);
                }

                return;
            }

            _resolvedMaterial = waveMaterial;
            _resolvedMesh = waveMesh;

            if (_argsBufferA == null)
                _argsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - PDA wave indirect args A - owner: PDADecryptionSpectrogramPanel

            if (_argsBufferB == null)
                _argsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - PDA wave indirect args B - owner: PDADecryptionSpectrogramPanel

            if (_argsBuffer == null)
                _argsBuffer = _argsBufferA;
            UpdateDrawArgs(_gpuSegmentCapacity);
            _graphicsReady = _resolvedMaterial != null && _resolvedMesh != null && _argsBufferA != null && _argsBufferB != null;
        }

        /// <summary>
        /// One-shot report of an unusable authored wave draw pair. The latch guarantees single emission and
        /// every parameter is a primitive, so no string work and no allocation reaches a tick cadence.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidWaveDrawAssets(
            bool materialAssigned,
            bool authoredMaterialValid,
            bool meshAssigned,
            bool authoredMeshValid)
        {
            if (!materialAssigned)
            {
                Hecton8.Core.H8Debug.LogError("PDADecryptionSpectrogramPanel: serialized field 'waveMaterial' is unassigned. The frequency-tuning wave renders nothing this session - LateFrameTick bails on !_graphicsReady - but the panel still ticks, still drains scanner signals and still tracks tuning state. Runtime material generation is forbidden: assign the authored PDA frequency tuning wave material in the inspector.");
            }
            else if (!authoredMaterialValid)
            {
                Hecton8.Core.H8Debug.LogError("PDADecryptionSpectrogramPanel: the material assigned to 'waveMaterial' has Enable GPU Instancing OFF, which the indirect wave draw requires. The frequency-tuning wave renders nothing this session. Tick 'Enable GPU Instancing' on that material asset.");
            }

            if (!meshAssigned)
            {
                Hecton8.Core.H8Debug.LogError("PDADecryptionSpectrogramPanel: serialized field 'waveMesh' is unassigned. The frequency-tuning wave renders nothing this session - LateFrameTick bails on !_graphicsReady - but the panel still ticks and still tracks tuning state. Assign the authored wave segment mesh in the inspector.");
                return;
            }

            if (!authoredMeshValid)
            {
                Hecton8.Core.H8Debug.LogError("PDADecryptionSpectrogramPanel: the mesh assigned to 'waveMesh' has no indexed submesh 0 (subMeshCount is 0 or GetIndexCount(0) is 0), which DrawMeshInstancedIndirect requires for its index count. The frequency-tuning wave renders nothing this session. Reimport or replace that mesh asset with one that carries an index buffer.");
            }
        }

        private void EnsureWaveMaterialPropertiesCold()
        {
            if (_waveMaterialProperties != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] - PDA frequency tuning wave shader payload - owner: PDADecryptionSpectrogramPanel.
            _waveMaterialProperties = new MaterialPropertyBlock();
        }

        private void ResetRuntimeState(uint artifactHash, uint blueprintHash)
        {
            _artifactHash = artifactHash != 0u ? artifactHash : DefaultArtifactHash;
            _blueprintHash = blueprintHash != 0u ? blueprintHash : DefaultBlueprintHash;
            _stageSeed = _artifactHash != 0u ? _artifactHash : DefaultArtifactHash;
            _stageIndex = 0;
            _lockedStageMask = 0;
            _holdTimerSeconds = 0f;
            _currentError01 = 1f;
            _lastTickDeltaTime = 0f;
            _lastTickUnscaledTime = 0f;
            _lastTickFrame = 0u;
            _unlocked = false;
            _playerFrequency = 1.4f;
            _playerAmplitude = 0.5f;
            BuildStageTargets(_stageSeed);
            ResolveTargetForCurrentStage(out _targetFrequency, out _targetAmplitude);
        }

        private void DrainScannerToolSignals()
        {
            bool hadSignal = false;
            ScannerToolActiveSignal latest = default;
            ReadOnlySpan<ScannerToolActiveSignal> signals = SignalBus<ScannerToolActiveSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                latest = signals[i];
                hadSignal = true;
            }

            if (!hadSignal)
            {
                if (!ScannerSignalRoute.TryGetLatestActive(out latest, out int latestSequence) ||
                    latestSequence == _lastScannerToolSignalSequence)
                {
                    return;
                }

                _lastScannerToolSignalSequence = latestSequence;
            }
            else if (ScannerSignalRoute.TryGetLatestActive(out _, out int latestSequence))
            {
                _lastScannerToolSignalSequence = latestSequence;
            }

            bool active = latest.Active != 0;
            bool targetChanged =
                latest.ArtifactHash != 0u &&
                (latest.ArtifactHash != _artifactHash || latest.BlueprintHash != _blueprintHash);
            _scannerActive = active;
            if (!active)
                return;

            if (targetChanged || _unlocked)
                ResetRuntimeState(latest.ArtifactHash, latest.BlueprintHash);
        }

        private void SampleInputState(float deltaTime)
        {
            IInputService input = _cachedInputService;
            PlayerInputState inputState = input != null ? input.GetState() : default;
            float amplitude01 = Sanitize01((inputState.MoveDelta.y + 1f) * 0.5f);
            float frequency01 = Sanitize01((inputState.LookDelta.x + 1f) * 0.5f);
            float targetAmplitude = math.lerp(playerAmplitudeMin, math.max(playerAmplitudeMin, playerAmplitudeMax), amplitude01);
            float targetFrequency = math.lerp(playerFrequencyMin, math.max(playerFrequencyMin, playerFrequencyMax), frequency01);
            float lerpAlpha = ResolveDampedLerpAlpha(inputLerpSpeed, deltaTime);
            _playerAmplitude = math.lerp(_playerAmplitude, targetAmplitude, lerpAlpha);
            _playerFrequency = math.lerp(_playerFrequency, targetFrequency, lerpAlpha);
        }

        private float EvaluateScalarWaveError()
        {
            float frequencyRange = math.max(0.0001f, playerFrequencyMax - playerFrequencyMin);
            float amplitudeRange = math.max(0.0001f, playerAmplitudeMax - playerAmplitudeMin);
            float frequencyError = math.abs(_targetFrequency - _playerFrequency) * math.rcp(frequencyRange);
            float amplitudeError = math.abs(_targetAmplitude - _playerAmplitude) * math.rcp(amplitudeRange);
            return math.saturate(frequencyError * 0.62f + amplitudeError * 0.38f);
        }

        private void QueueWaveResult(float error01)
        {
            _pendingWaveError01 = Sanitize01(error01);
            _waveResultDirty = true;
        }

        private void CommitWaveResult(float deltaTime, float rawError)
        {
            if (!math.isfinite(rawError))
            {
                QueueTelemetryDump();
                _currentError01 = 1f;
                _holdTimerSeconds = 0f;
                return;
            }

            float error01 = Sanitize01(rawError);
            _currentError01 = error01;
            if (error01 < UnlockErrorThreshold)
            {
                _holdTimerSeconds += deltaTime;
                if (_holdTimerSeconds >= UnlockHoldSeconds)
                    LockCurrentStage();
            }
            else
            {
                _holdTimerSeconds = 0f;
            }

            RecordTelemetry();
            PushFeedback(error01);
        }

        private void LockCurrentStage()
        {
            _lockedStageMask |= 1 << _stageIndex;
            _holdTimerSeconds = 0f;
            if (_stageIndex < StageCount - 1)
            {
                _stageIndex++;
                ResolveTargetForCurrentStage(out _targetFrequency, out _targetAmplitude);
                return;
            }

            EmitBlueprintUnlock();
        }

        private void EmitBlueprintUnlock()
        {
            if (_unlocked)
                return;

            _unlocked = true;
            BlueprintUnlockedSignal signal = default;
            signal.EntityHash = _artifactHash;
            signal.BlueprintHash = _blueprintHash != 0u ? _blueprintHash : DefaultBlueprintHash;
            signal.SourceId = ToolHash;
            signal.Frame = _lastTickFrame;
            signal.Category = 1;
            signal.Flags = 1;
            SignalBus<BlueprintUnlockedSignal>.TryPushTracked(in signal, ref s_x001PDADecryptionSpectrogramPanelSignalPushDropCount);
        }

        private void RenderWaveMesh()
        {
            if (_resolvedMaterial == null || _resolvedMesh == null || _argsBuffer == null)
                return;

            Transform anchor = surfaceAnchor != null ? surfaceAnchor : transform;
            Matrix4x4 localToWorld = anchor.localToWorldMatrix * Matrix4x4.Translate(localSurfaceOffset);
            Vector4 origin = localToWorld.GetColumn(3);
            Vector3 worldCenter = new Vector3(origin.x, origin.y, origin.z);
            _waveMaterialProperties.SetMatrix(LocalToWorldId, localToWorld);
            _waveMaterialProperties.SetFloat(TubeRadiusId, math.max(0.0005f, tubeRadius));
            Vector4 waveScalars = default;
            waveScalars.x = _targetFrequency;
            waveScalars.y = _targetAmplitude;
            waveScalars.z = _playerFrequency;
            waveScalars.w = _playerAmplitude;
            _waveMaterialProperties.SetVector(WaveScalarsId, waveScalars);
            Vector4 waveLayout = default;
            waveLayout.x = math.max(1, _waveSegmentCount);
            waveLayout.y = math.max(0.01f, localSurfaceSize.x);
            waveLayout.z = math.max(0.01f, localSurfaceSize.y);
            waveLayout.w = _pointCount;
            _waveMaterialProperties.SetVector(WaveLayoutId, waveLayout);
            Vector4 timeErrorStage = default;
            timeErrorStage.x = _lastTickUnscaledTime;
            timeErrorStage.y = _currentError01;
            timeErrorStage.z = _stageIndex;
            timeErrorStage.w = _holdTimerSeconds * math.rcp(UnlockHoldSeconds);
            _waveMaterialProperties.SetVector(TimeErrorStageId, timeErrorStage);
            UpdateDrawArgs(_gpuSegmentCapacity);

            Bounds bounds = new Bounds(worldCenter, Vector3.one * math.max(localSurfaceSize.x, localSurfaceSize.y) * 2f);
            RenderParams renderParams = new RenderParams(_resolvedMaterial)
            {
                matProps = _waveMaterialProperties,
                worldBounds = bounds,
                layer = renderLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, _resolvedMesh, _argsBuffer, 1, 0);
        }

        private void UpdateDrawArgs(int instanceCount)
        {
            if ((_argsBufferA == null && _argsBufferB == null) || _resolvedMesh == null)
                return;

            int safeInstanceCount = math.max(0, instanceCount);
            if (_lastArgsInstanceCount == safeInstanceCount)
                return;

            GraphicsBuffer target = _argsBufferWriteIndex == 0 ? _argsBufferA : _argsBufferB;
            if (target == null)
                return;

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                target.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            try
            {
                GraphicsBuffer.IndirectDrawIndexedArgs drawArgs = default;
                drawArgs.indexCountPerInstance = _resolvedMesh.GetIndexCount(0);
                drawArgs.instanceCount = (uint)safeInstanceCount;
                drawArgs.startIndex = _resolvedMesh.GetIndexStart(0);
                drawArgs.baseVertexIndex = (uint)Mathf.Max(0, _resolvedMesh.GetBaseVertex(0));
                drawArgs.startInstance = 0u;
                argsWrite[0] = drawArgs;
            }
            finally
            {
                target.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            }
            _argsBuffer = target;
            _argsBufferWriteIndex ^= 1;
            _lastArgsInstanceCount = safeInstanceCount;
        }

        private void PushFeedback(float error01)
        {
            _presentationFeedbackClearRequested = false;
            float safeError = Sanitize01(error01);
            if (math.abs(_lastShaderError - safeError) > ShaderFloatEpsilon)
            {
                Shader.SetGlobalFloat(ErrorGlobalId, safeError);
                _lastShaderError = safeError;
            }

            if (_lastTickUnscaledTime < _nextFeedbackTime)
                return;

            float match01 = math.saturate(1f - safeError);
            ToolAcousticSignal signal = default;
            signal.ToolHash = ToolHash;
            signal.TargetHash = _artifactHash;
            signal.Progress01 = match01;
            signal.PitchScale = math.lerp(0.62f, 1.12f, match01);
            signal.Intensity01 = safeError;
            signal.Frame = _lastTickFrame;
            signal.State = 2;
            signal.Flags = 0;
            SignalBus<ToolAcousticSignal>.TryPushTracked(in signal, ref s_x001PDADecryptionSpectrogramPanelSignalPushDropCount);
            PlayerSignalEvents.TryRaiseInteractionSignal(new PlayerInteractionStressSignal(
                safeError * 0.15f,
                math.saturate(0.25f + safeError * 0.65f),
                math.lerp(0.62f, 1.12f, match01),
                match01));
            if (match01 > 0.05f)
            {
                Hecton8.Tools.ToolHapticsRuntime.TryEnqueueSinusoidalCommand(
                    match01 * 0.10f,
                    match01 * 0.26f,
                    0.08f,
                    22f + match01 * 38f,
                    2,
                    0x03);
            }

            _nextFeedbackTime = _lastTickUnscaledTime + math.max(0.02f, feedbackIntervalSeconds);
        }

        private void QueuePresentationFeedbackClear()
        {
            _presentationFeedbackClearRequested = true;
        }

        private void ClearPresentationFeedback()
        {
            _presentationFeedbackClearRequested = false;
            if (math.abs(_lastShaderError) <= ShaderFloatEpsilon)
                return;

            Shader.SetGlobalFloat(ErrorGlobalId, 0f);
            _lastShaderError = 0f;
        }

        private void RecordTelemetry()
        {
            float4 finiteProbe = default;
            finiteProbe.x = _targetFrequency;
            finiteProbe.y = _targetAmplitude;
            finiteProbe.z = _playerFrequency;
            finiteProbe.w = _playerAmplitude;
            if (!math.all(math.isfinite(finiteProbe)) ||
                !math.isfinite(_currentError01))
            {
                QueueTelemetryDump();
                return;
            }

            if (!TryAcquireTelemetryRingWrite(out IDataVault telemetryWriteVault, out NativeArray<FrequencyTuningTelemetryEntry> telemetryRing))
                return;

            try
            {
                FrequencyTuningTelemetryEntry telemetry = default;
                telemetry.Frame = _lastTickFrame;
                telemetry.ArtifactHash = _artifactHash;
                telemetry.TargetFrequency = _targetFrequency;
                telemetry.TargetAmplitude = _targetAmplitude;
                telemetry.PlayerFrequency = _playerFrequency;
                telemetry.PlayerAmplitude = _playerAmplitude;
                telemetry.Error01 = _currentError01;
                telemetry.HoldPermille = (ushort)math.clamp((int)math.round(_holdTimerSeconds * math.rcp(UnlockHoldSeconds) * 1000f), 0, 1000);
                telemetry.Stage = (byte)_stageIndex;
                telemetry.Flags = (byte)_lockedStageMask;
                telemetryRing[_telemetryCursor] = telemetry;
                _telemetryCursor++;
                if (_telemetryCursor >= TelemetryCapacity)
                    _telemetryCursor = 0;
            }
            finally
            {
                ReleaseVaultWriteBuffer(telemetryWriteVault, in _telemetryRingHandle, BufferID.PdaFrequencyTelemetryRing);
            }
        }

        private void QueueTelemetryDump()
        {
            _telemetryDumpQueued = true;
        }

        private void FlushQueuedTelemetryDump()
        {
            if (!_telemetryDumpQueued)
                return;

            _telemetryDumpQueued = false;
            DumpTelemetryCold();
        }

        private unsafe void DumpTelemetryCold()
        {
            if (!IsExactVaultHandle(in _telemetryRingHandle, BufferID.PdaFrequencyTelemetryRing))
            {
                _telemetryDumpQueued = true;
                return;
            }

            try
            {
                const int headerBytes = 8;
                const int rowBytes = 32;
                int byteCount = headerBytes + TelemetryCapacity * rowBytes;
                NativeArray<byte> payload = default;
                try
                {
                    payload = NativeFaultDumpWriter.CreateTransientPayload(
                        byteCount,
                        nameof(PDADecryptionSpectrogramPanel),
                        TelemetryDumpPayloadLabel,
                        NativeArrayOptions.UninitializedMemory);
                    byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    Span<byte> header = new Span<byte>(destination, headerBytes);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(0, 4), TelemetryCapacity);
                    BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), _telemetryCursor);

                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        if (!TryReadTelemetryEntry(i, out FrequencyTuningTelemetryEntry entry))
                        {
                            _telemetryDumpQueued = true;
                            return;
                        }

                        Span<byte> row = new Span<byte>(destination + headerBytes + i * rowBytes, rowBytes);
                        WriteFrequencyTuningTelemetryEntry(row, in entry);
                    }

                    if (!NativeFaultDumpWriter.TryWriteAll(TelemetryDumpPath, payload, byteCount))
                        _telemetryDumpQueued = true;
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(PDADecryptionSpectrogramPanel),
                        TelemetryDumpPayloadLabel);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        private void BuildStageTargets(uint seed)
        {
            if (!TryAcquireStageTargetsWrite(out IDataVault stageTargetsWriteVault, out NativeArray<FrequencyTuningStageTarget> stageTargets))
                return;

            try
            {
                uint state = seed != 0u ? seed : DefaultArtifactHash;
                for (int i = 0; i < StageCount; i++)
                {
                    float r0 = Next01(ref state);
                    float r1 = Next01(ref state);
                    FrequencyTuningStageTarget target = default;
                    target.Frequency = math.clamp(1.05f + i * 0.8f + r0 * 0.35f, playerFrequencyMin, playerFrequencyMax);
                    target.Amplitude = math.clamp(0.32f + i * 0.12f + r1 * 0.32f, playerAmplitudeMin, playerAmplitudeMax);
                    stageTargets[i] = target;
                }
            }
            finally
            {
                ReleaseVaultWriteBuffer(stageTargetsWriteVault, in _stageTargetsHandle, BufferID.PdaFrequencyStageTargets);
            }
        }

        private void ResolveTargetForCurrentStage(out float frequency, out float amplitude)
        {
            int safeStage = math.clamp(_stageIndex, 0, StageCount - 1);
            FrequencyTuningStageTarget target = TryReadStageTarget(safeStage, out FrequencyTuningStageTarget resolvedTarget)
                ? resolvedTarget
                : default;
            frequency = target.Frequency > 0f ? target.Frequency : 1.5f;
            amplitude = target.Amplitude > 0f ? target.Amplitude : 0.55f;

            if (!ResolveHardDifficultyActive())
                return;

            float driftFrequency = ResolveTriangleDriftSigned(_stageSeed, safeStage, _lastTickUnscaledTime, hardDriftFrequency, 0.00037f, 1f);
            float driftAmplitude = ResolveTriangleDriftSigned(_stageSeed, safeStage, _lastTickUnscaledTime, hardDriftFrequency, 0.00053f, 0.73f);
            frequency = math.clamp(frequency + driftFrequency * hardDriftAmplitude, playerFrequencyMin, playerFrequencyMax);
            amplitude = math.clamp(amplitude + driftAmplitude * hardDriftAmplitude * 0.5f, playerAmplitudeMin, playerAmplitudeMax);
        }

        private static float ResolveTriangleDriftSigned(
            uint seed,
            int stage,
            float timeSeconds,
            float frequency,
            float seedScale,
            float timeScale)
        {
            float phase = math.frac(seed * seedScale + stage * 0.173f + timeSeconds * math.max(0f, frequency) * timeScale);
            float triangle01 = 1f - math.abs(phase * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static bool ResolveHardDifficultyActive()
        {
            if (RunModifierController.IsNightmareModeActive)
                return true;

            DifficultyModifierData modifiers = DynamicDifficultyDirector.Current;
            return modifiers.DamageMultiplier >= 1.15f || modifiers.PredatorAggressionScale >= 1.35f;
        }

        private int ResolvePointCount()
        {
            float quality01 = math.min(_cachedQualityWeight01, _cachedVideoMemoryQualityClamp01);
            float curve = SmoothStep01(quality01);
            int count = (int)math.round(math.lerp(LowPointCount, HighPointCount, curve));
            return math.clamp(count, LowPointCount, HighPointCount);
        }

        private void RefreshCachedQualityPolicy(bool rebuildResourcesOnPointChange)
        {
            int previousPointCount = _pointCount;
            float quality = HomeostasisBrain.GlobalQualityWeight;
            _cachedQualityWeight01 = math.saturate(math.isfinite(quality) ? quality : 1f);

            int resolvedPointCount = ResolvePointCount();
            if (!rebuildResourcesOnPointChange || resolvedPointCount == previousPointCount)
                return;

            _nativeReady = false;
            _graphicsReady = false;
            _nativeResourcesDirty = true;
            _graphicsResourcesDirty = true;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _cachedVideoMemoryQualityClamp01 = ResolveVideoMemoryQualityClamp01Cold();
        }

        private float ResolveVideoMemoryQualityClamp01Cold()
        {
            int memoryMb = SystemInfo.graphicsMemorySize;
            if (memoryMb <= 0)
                return 1f;

            float denominator = math.max(1f, 6144f - minimumQualityVideoMemoryMb);
            float memory01 = math.saturate((memoryMb - minimumQualityVideoMemoryMb) / denominator);
            return math.lerp(0.18f, 1f, SmoothStep01(memory01));
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault previousVault = previousService is IDataVault oldVault ? oldVault : _cachedDataVault;
                    IDataVault nextVault = currentService is IDataVault currentVault ? currentVault : null;
                    BindDataVaultForLifecycle(nextVault, previousVault);
                    _nativeReady = false;
                    _nativeResourcesDirty = false;
                    EnsureNativeResources();
                    break;
                case GlobalRegistryServiceSlot.Input:
                    _cachedInputService = currentService as IInputService;
                    break;
            }
        }

        private void RefreshCachedRegistryServices()
        {
            BindDataVaultForLifecycle(GlobalRegistry.DataVault);
            _cachedInputService = GlobalRegistry.Input;
            RefreshCachedQualityPolicy(rebuildResourcesOnPointChange: false);
        }

        private void BindDataVaultForLifecycle(IDataVault nextVault, IDataVault previousVault = null)
        {
            IDataVault releaseVault = previousVault ?? _cachedDataVault;
            if (!ReferenceEquals(_cachedDataVault, nextVault))
                ReleaseNativeBuffers(releaseVault);

            _cachedDataVault = nextVault;
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

        private void TryRegisterTickHandlers()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);

            if (!_registeredSlowTick)
                _registeredSlowTick = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
        }

        private void TryUnregisterTickHandlers()
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

        private void DisposeNativeResources()
        {
            ReleaseNativeBuffers(_cachedDataVault);
            _nativeReady = false;
        }

        private void ReleaseNativeBuffers(IDataVault vault)
        {
            ReleaseVaultBuffer(vault, ref _stageTargetsHandle, BufferID.PdaFrequencyStageTargets);
            ReleaseVaultBuffer(vault, ref _telemetryRingHandle, BufferID.PdaFrequencyTelemetryRing);
        }

        private void DisposeGraphicsResources()
        {
            _argsBufferA?.Dispose();
            _argsBufferA = null;
            _argsBufferB?.Dispose();
            _argsBufferB = null;
            _argsBuffer = null;
            _resolvedMaterial = null;
            _resolvedMesh = null;
            _graphicsReady = false;
            _lastArgsInstanceCount = -1;
            _argsBufferWriteIndex = 0;
        }

        private static float ResolveDampedLerpAlpha(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return x * math.rcp(1f + x);
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SmoothStep01(float value)
        {
            float x = Sanitize01(value);
            return x * x * (3f - 2f * x);
        }

        private static void WriteFrequencyTuningTelemetryEntry(Span<byte> destination, in FrequencyTuningTelemetryEntry entry)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), entry.ArtifactHash);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.TargetFrequency);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.TargetAmplitude);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.PlayerFrequency);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.PlayerAmplitude);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.Error01);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(28, 2), entry.HoldPermille);
            destination[30] = entry.Stage;
            destination[31] = entry.Flags;
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) ? math.max(0f, value) : fallback;
        }

        private static float Next01(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state & 0x00FFFFFFu) * Hash24ToUnit;
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct FrequencyTuningStageTarget
        {
            [FieldOffset(0)]
            public float Frequency;
            [FieldOffset(4)]
            public float Amplitude;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct FrequencyTuningTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint ArtifactHash;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float TargetFrequency;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float TargetAmplitude;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float PlayerFrequency;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public float PlayerAmplitude;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float Error01;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public ushort HoldPermille;
            [System.Runtime.InteropServices.FieldOffset(30)]
            public byte Stage;
            [System.Runtime.InteropServices.FieldOffset(31)]
            public byte Flags;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(33)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(34)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(35)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(36)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(37)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(38)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(39)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad23;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad24;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad25;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad26;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad27;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad28;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad29;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad30;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad31;
        }
    }
}

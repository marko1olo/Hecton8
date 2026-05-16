using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Meta;
using Hecton8.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Decryption Spectrogram Panel")]
    public sealed class PDADecryptionSpectrogramPanel : MonoBehaviour, IUpdatable, ILateFrameTickable, IDisposable
    {
        internal const string WaveShaderPath = "Assets/_Project/Art/Shaders/Hecton_PDA_FrequencyTuningWave.shader";
        private const int HighPointCount = 128;
        private const int LowPointCount = 32;
        private const int StageCount = 3;
        private const int TelemetryCapacity = 300;
        private const float UnlockErrorThreshold = 0.05f;
        private const float UnlockHoldSeconds = 2f;
        private const float TwoPi = math.PI * 2f;
        private const float Hash24ToUnit = 0.00000005960464833f;
        private const uint DefaultArtifactHash = 0x534F5648u; // SOVH
        private const uint DefaultBlueprintHash = 0x46485455u; // FHTU
        private const uint ToolHash = 0x53434E52u; // SCNR
        private const float ShaderFloatEpsilon = 0.0001f;
        private const string TelemetryDumpPath = "Docs/AgentLogs/Dump_MINIGAME_FREQUENCY_TUNING.bin";

        private static readonly int SegmentsId = Shader.PropertyToID("_HectonFrequencyTuningSegments");
        private static readonly int LocalToWorldId = Shader.PropertyToID("_HectonFrequencyTuningLocalToWorld");
        private static readonly int TubeRadiusId = Shader.PropertyToID("_HectonFrequencyTuningTubeRadius");
        private static readonly int TimeErrorStageId = Shader.PropertyToID("_HectonFrequencyTuningTimeErrorStage");
        private static readonly int ErrorGlobalId = Shader.PropertyToID("_HectonFrequencyTuningError01");

        [Header("PDA Surface")]
        [SerializeField] private Transform surfaceAnchor;
        [SerializeField] private Vector3 localSurfaceOffset = new Vector3(0f, 0f, -0.002f);
        [SerializeField] private Vector2 localSurfaceSize = new Vector2(0.22f, 0.085f);
        [SerializeField, Min(0.0005f)] private float tubeRadius = 0.003f;
        [SerializeField] private int renderLayer;

        [Header("Renderer")]
        [SerializeField] private Material waveMaterial;
        [SerializeField] private Shader waveShader;
        [SerializeField] private Mesh waveMesh;
        [SerializeField, Min(256)] private int lowTierVideoMemoryMb = 2048;

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

        private NativeArray<float> _targetWave;
        private NativeArray<float> _playerWave;
        private NativeArray<float> _errorOutput;
        private NativeArray<FrequencyTuningWaveGpuSegment> _gpuSegments;
        private NativeArray<FrequencyTuningStageTarget> _stageTargets;
        private NativeArray<FrequencyTuningTelemetryEntry> _telemetryRing;
        private GraphicsBuffer _segmentBuffer;
        private GraphicsBuffer _argsBuffer;
        private Material _runtimeMaterial;
        private Material _runtimeSourceMaterial;
        private Mesh _resolvedMesh;
        private Mesh _runtimeQuadMesh;
        private JobHandle _waveJobHandle;
        private int _pointCount = HighPointCount;
        private int _waveSegmentCount = HighPointCount - 1;
        private int _gpuSegmentCapacity = (HighPointCount - 1) * 2;
        private int _lastArgsInstanceCount = -1;
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
        private uint _lastTickFrame;
        private bool _scannerActive;
        private bool _unlocked;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _nativeReady;
        private bool _graphicsReady;
        private bool _waveJobScheduled;
        private bool _disposed;
        private bool _materialBufferBound;

        private void Awake()
        {
            if (surfaceAnchor == null)
                surfaceAnchor = transform;

#if UNITY_EDITOR
            if (waveShader == null)
                waveShader = AssetDatabase.LoadAssetAtPath<Shader>(WaveShaderPath);
#endif
        }

        private void OnEnable()
        {
            _disposed = false;
            EnsureNativeResources();
            EnsureGraphicsResources();
            ResetRuntimeState(_artifactHash, _blueprintHash);
            TryRegisterTickHandlers();
        }

        private void OnDisable()
        {
            TryUnregisterTickHandlers();
            CompleteWaveJobForTeardown();
            ClearPresentationFeedback();
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
            CompleteWaveJobForTeardown();
            DisposeNativeResources();
            DisposeGraphicsResources();
        }

        public void Tick(float deltaTime)
        {
            if (!_nativeReady || !_graphicsReady)
                return;

            DrainScannerToolSignals();
            if (!_scannerActive || _unlocked)
            {
                ClearPresentationFeedback();
                return;
            }

            float safeDeltaTime = SanitizePositive(deltaTime, 0f);
            _lastTickDeltaTime = safeDeltaTime;
            _lastTickUnscaledTime = Time.unscaledTime;
            _lastTickFrame = unchecked((uint)Time.frameCount);
            SampleInputState(safeDeltaTime);
            ResolveTargetForCurrentStage(out _targetFrequency, out _targetAmplitude);

            if (!_waveJobScheduled)
                ScheduleWaveJobs();
        }

        public void LateFrameTick()
        {
            if (!_nativeReady)
                return;

            if (_waveJobScheduled)
            {
                _waveJobHandle.Complete();
                _waveJobScheduled = false;
                CommitWaveResult(_lastTickDeltaTime);
                UploadGpuWave();
            }

            if (_scannerActive && !_unlocked)
                RenderWaveMesh();
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
            _targetWave = new NativeArray<float>(_pointCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[32/128] - target frequency wave - owner: PDADecryptionSpectrogramPanel
            _playerWave = new NativeArray<float>(_pointCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[32/128] - player frequency wave - owner: PDADecryptionSpectrogramPanel
            _errorOutput = new NativeArray<float>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[1] - Burst error output - owner: PDADecryptionSpectrogramPanel
            _gpuSegments = new NativeArray<FrequencyTuningWaveGpuSegment>(_gpuSegmentCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<FrequencyTuningWaveGpuSegment>[62/254] - PDA wave GPU upload - owner: PDADecryptionSpectrogramPanel
            _stageTargets = new NativeArray<FrequencyTuningStageTarget>(StageCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<FrequencyTuningStageTarget>[3] - deterministic stage targets - owner: PDADecryptionSpectrogramPanel
            _telemetryRing = new NativeArray<FrequencyTuningTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<FrequencyTuningTelemetryEntry>[300] - black box ring - owner: PDADecryptionSpectrogramPanel
            RegisterNativeArray(_targetWave, nameof(_targetWave));
            RegisterNativeArray(_playerWave, nameof(_playerWave));
            RegisterNativeArray(_errorOutput, nameof(_errorOutput));
            RegisterNativeArray(_gpuSegments, nameof(_gpuSegments));
            RegisterNativeArray(_stageTargets, nameof(_stageTargets));
            RegisterNativeArray(_telemetryRing, nameof(_telemetryRing));
            _nativeReady = true;
        }

        private void EnsureGraphicsResources()
        {
            if (_graphicsReady && _segmentBuffer != null && _argsBuffer != null && _resolvedMesh != null && _segmentBuffer.count >= _gpuSegmentCapacity)
                return;

            _resolvedMesh = waveMesh != null ? waveMesh : ResolveRuntimeQuadMesh();
            if (_segmentBuffer != null && _segmentBuffer.count < _gpuSegmentCapacity)
            {
                _segmentBuffer.Dispose();
                _segmentBuffer = null;
                _materialBufferBound = false;
            }

            if (_segmentBuffer == null)
                _segmentBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FrequencyTuningWaveGpuSegment>(_gpuSegmentCapacity); // COLD ALLOC: GraphicsBuffer[62/254] - PDA wave segment buffer - owner: PDADecryptionSpectrogramPanel

            if (_argsBuffer == null)
                _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - PDA wave indirect args - owner: PDADecryptionSpectrogramPanel

            ResolveRuntimeMaterial();
            UpdateDrawArgs(_gpuSegmentCapacity);
            _graphicsReady = _resolvedMesh != null && _segmentBuffer != null && _argsBuffer != null;
        }

        private void ResolveRuntimeMaterial()
        {
            if (waveMaterial != null)
            {
                if (_runtimeMaterial != null && ReferenceEquals(_runtimeSourceMaterial, waveMaterial))
                    return;

                DestroyRuntimeMaterial();
                _runtimeSourceMaterial = waveMaterial;
                _runtimeMaterial = new Material(waveMaterial)
                {
                    hideFlags = HideFlags.DontSave,
                    enableInstancing = true
                }; // COLD ALLOC: Material[1] - PDA frequency tuning buffer-bound draw material - owner: PDADecryptionSpectrogramPanel
                _materialBufferBound = false;
                return;
            }

            if (_runtimeMaterial != null)
                return;

            Shader resolvedShader = waveShader;
#if UNITY_EDITOR
            if (resolvedShader == null)
                resolvedShader = AssetDatabase.LoadAssetAtPath<Shader>(WaveShaderPath);
#endif
            if (resolvedShader == null)
                return;

            _runtimeMaterial = new Material(resolvedShader)
            {
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            }; // COLD ALLOC: Material[1] - editor fallback PDA frequency tuning material - owner: PDADecryptionSpectrogramPanel
            _runtimeSourceMaterial = null;
            _materialBufferBound = false;
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
                if (!GlobalSignals.TryGetLatestScannerToolActiveSignal(out latest, out int latestSequence) ||
                    latestSequence == _lastScannerToolSignalSequence)
                {
                    return;
                }

                _lastScannerToolSignalSequence = latestSequence;
            }
            else if (GlobalSignals.TryGetLatestScannerToolActiveSignal(out _, out int latestSequence))
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
            IInputService input = GlobalRegistry.Input;
            PlayerInputState inputState = input != null ? input.GetState() : default;
            float amplitude01 = Sanitize01((inputState.MoveDelta.y + 1f) * 0.5f);
            float frequency01 = Sanitize01((inputState.LookDelta.x + 1f) * 0.5f);
            float targetAmplitude = math.lerp(playerAmplitudeMin, math.max(playerAmplitudeMin, playerAmplitudeMax), amplitude01);
            float targetFrequency = math.lerp(playerFrequencyMin, math.max(playerFrequencyMin, playerFrequencyMax), frequency01);
            float lerpAlpha = ResolveDampedLerpAlpha(inputLerpSpeed, deltaTime);
            _playerAmplitude = math.lerp(_playerAmplitude, targetAmplitude, lerpAlpha);
            _playerFrequency = math.lerp(_playerFrequency, targetFrequency, lerpAlpha);
        }

        private void ScheduleWaveJobs()
        {
            int safePointCount = math.clamp(_pointCount, LowPointCount, HighPointCount);
            FrequencyWaveGenerateJob generateJob = new FrequencyWaveGenerateJob
            {
                TargetWave = _targetWave,
                PlayerWave = _playerWave,
                GpuSegments = _gpuSegments,
                PointCount = safePointCount,
                SegmentCount = math.min(_waveSegmentCount, math.max(1, safePointCount - 1)),
                TargetFrequency = _targetFrequency,
                TargetAmplitude = _targetAmplitude,
                PlayerFrequency = _playerFrequency,
                PlayerAmplitude = _playerAmplitude,
                LocalWidth = math.max(0.01f, localSurfaceSize.x),
                LocalHeight = math.max(0.01f, localSurfaceSize.y),
                StageIndex = _stageIndex
            };
            JobHandle generateHandle = generateJob.Schedule(safePointCount, 32);
            FrequencyWaveErrorJob errorJob = new FrequencyWaveErrorJob
            {
                TargetWave = _targetWave,
                PlayerWave = _playerWave,
                ErrorOutput = _errorOutput,
                PointCount = safePointCount
            };
            _waveJobHandle = errorJob.Schedule(generateHandle);
            _waveJobScheduled = true;
        }

        private void CommitWaveResult(float deltaTime)
        {
            float rawError = _errorOutput[0];
            if (!math.isfinite(rawError))
            {
                DumpTelemetryCold();
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
            GlobalSignals.Publish(new BlueprintUnlockedSignal
            {
                EntityHash = _artifactHash,
                BlueprintHash = _blueprintHash != 0u ? _blueprintHash : DefaultBlueprintHash,
                SourceId = ToolHash,
                Frame = _lastTickFrame,
                Category = 1,
                Flags = 1
            });
        }

        private void UploadGpuWave()
        {
            if (_segmentBuffer == null || !_gpuSegments.IsCreated)
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(_segmentBuffer, _gpuSegments, _gpuSegmentCapacity);
        }

        private void RenderWaveMesh()
        {
            ResolveRuntimeMaterial();
            if (_runtimeMaterial == null || _resolvedMesh == null || _segmentBuffer == null || _argsBuffer == null)
                return;

            if (!_materialBufferBound)
            {
                _runtimeMaterial.SetBuffer(SegmentsId, _segmentBuffer);
                _materialBufferBound = true;
            }

            Transform anchor = surfaceAnchor != null ? surfaceAnchor : transform;
            Matrix4x4 localToWorld = anchor.localToWorldMatrix * Matrix4x4.Translate(localSurfaceOffset);
            Vector4 origin = localToWorld.GetColumn(3);
            Vector3 worldCenter = new Vector3(origin.x, origin.y, origin.z);
            _runtimeMaterial.SetMatrix(LocalToWorldId, localToWorld);
            _runtimeMaterial.SetFloat(TubeRadiusId, math.max(0.0005f, tubeRadius));
            _runtimeMaterial.SetVector(
                TimeErrorStageId,
                new Vector4(_lastTickUnscaledTime, _currentError01, _stageIndex, _holdTimerSeconds * math.rcp(UnlockHoldSeconds)));
            UpdateDrawArgs(_gpuSegmentCapacity);

            Bounds bounds = new Bounds(worldCenter, Vector3.one * math.max(localSurfaceSize.x, localSurfaceSize.y) * 2f);
            RenderParams renderParams = new RenderParams(_runtimeMaterial)
            {
                worldBounds = bounds,
                layer = renderLayer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };
            Graphics.RenderMeshIndirect(renderParams, _resolvedMesh, _argsBuffer, 1, 0);
        }

        private void UpdateDrawArgs(int instanceCount)
        {
            if (_argsBuffer == null || _resolvedMesh == null)
                return;

            int safeInstanceCount = math.max(0, instanceCount);
            if (_lastArgsInstanceCount == safeInstanceCount)
                return;

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _argsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = _resolvedMesh.GetIndexCount(0),
                instanceCount = (uint)safeInstanceCount,
                startIndex = _resolvedMesh.GetIndexStart(0),
                baseVertexIndex = (uint)Mathf.Max(0, _resolvedMesh.GetBaseVertex(0)),
                startInstance = 0u
            };
            _argsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            _lastArgsInstanceCount = safeInstanceCount;
        }

        private void PushFeedback(float error01)
        {
            float safeError = Sanitize01(error01);
            if (math.abs(_lastShaderError - safeError) > ShaderFloatEpsilon)
            {
                Shader.SetGlobalFloat(ErrorGlobalId, safeError);
                _lastShaderError = safeError;
            }

            if (_lastTickUnscaledTime < _nextFeedbackTime)
                return;

            float match01 = math.saturate(1f - safeError);
            GlobalSignals.Publish(new ToolAcousticSignal
            {
                ToolHash = ToolHash,
                TargetHash = _artifactHash,
                Progress01 = match01,
                PitchScale = math.lerp(0.62f, 1.12f, match01),
                Intensity01 = safeError,
                Frame = _lastTickFrame,
                State = 2,
                Flags = 0
            });
            PlayerSignalEvents.RaiseInteractionSignal(new PlayerInteractionStressSignal(
                safeError * 0.15f,
                math.saturate(0.25f + safeError * 0.65f),
                math.lerp(0.62f, 1.12f, match01),
                match01));
            if (match01 > 0.05f)
            {
                ToolHapticsRuntime.EnqueueSinusoidalCommand(
                    match01 * 0.10f,
                    match01 * 0.26f,
                    0.08f,
                    22f + match01 * 38f,
                    2,
                    0x03);
            }

            _nextFeedbackTime = _lastTickUnscaledTime + math.max(0.02f, feedbackIntervalSeconds);
        }

        private void ClearPresentationFeedback()
        {
            if (math.abs(_lastShaderError) <= ShaderFloatEpsilon)
                return;

            Shader.SetGlobalFloat(ErrorGlobalId, 0f);
            _lastShaderError = 0f;
        }

        private void RecordTelemetry()
        {
            if (!_telemetryRing.IsCreated)
                return;

            if (!math.all(math.isfinite(new float4(_targetFrequency, _targetAmplitude, _playerFrequency, _playerAmplitude))) ||
                !math.isfinite(_currentError01))
            {
                DumpTelemetryCold();
                return;
            }

            _telemetryRing[_telemetryCursor] = new FrequencyTuningTelemetryEntry
            {
                Frame = _lastTickFrame,
                ArtifactHash = _artifactHash,
                TargetFrequency = _targetFrequency,
                TargetAmplitude = _targetAmplitude,
                PlayerFrequency = _playerFrequency,
                PlayerAmplitude = _playerAmplitude,
                Error01 = _currentError01,
                HoldPermille = (ushort)math.clamp((int)math.round(_holdTimerSeconds * math.rcp(UnlockHoldSeconds) * 1000f), 0, 1000),
                Stage = (byte)_stageIndex,
                Flags = (byte)_lockedStageMask
            };
            _telemetryCursor++;
            if (_telemetryCursor >= TelemetryCapacity)
                _telemetryCursor = 0;
        }

        private void DumpTelemetryCold()
        {
            if (!_telemetryRing.IsCreated)
                return;

            try
            {
                string directory = Path.GetDirectoryName(TelemetryDumpPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(TelemetryCapacity);
                writer.Write(_telemetryCursor);
                for (int i = 0; i < TelemetryCapacity; i++)
                {
                    FrequencyTuningTelemetryEntry entry = _telemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.ArtifactHash);
                    writer.Write(entry.TargetFrequency);
                    writer.Write(entry.TargetAmplitude);
                    writer.Write(entry.PlayerFrequency);
                    writer.Write(entry.PlayerAmplitude);
                    writer.Write(entry.Error01);
                    writer.Write(entry.HoldPermille);
                    writer.Write(entry.Stage);
                    writer.Write(entry.Flags);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void BuildStageTargets(uint seed)
        {
            if (!_stageTargets.IsCreated)
                return;

            uint state = seed != 0u ? seed : DefaultArtifactHash;
            for (int i = 0; i < StageCount; i++)
            {
                float r0 = Next01(ref state);
                float r1 = Next01(ref state);
                _stageTargets[i] = new FrequencyTuningStageTarget
                {
                    Frequency = math.clamp(1.05f + i * 0.8f + r0 * 0.35f, playerFrequencyMin, playerFrequencyMax),
                    Amplitude = math.clamp(0.32f + i * 0.12f + r1 * 0.32f, playerAmplitudeMin, playerAmplitudeMax)
                };
            }
        }

        private void ResolveTargetForCurrentStage(out float frequency, out float amplitude)
        {
            int safeStage = math.clamp(_stageIndex, 0, StageCount - 1);
            FrequencyTuningStageTarget target = _stageTargets.IsCreated ? _stageTargets[safeStage] : default;
            frequency = target.Frequency > 0f ? target.Frequency : 1.5f;
            amplitude = target.Amplitude > 0f ? target.Amplitude : 0.55f;

            if (!ResolveHardDifficultyActive())
                return;

            float driftFrequency = noise.cnoise(new float2(_stageSeed * 0.00037f + safeStage * 3.11f, _lastTickUnscaledTime * hardDriftFrequency));
            float driftAmplitude = noise.cnoise(new float2(_stageSeed * 0.00053f + safeStage * 5.17f, _lastTickUnscaledTime * hardDriftFrequency * 0.73f));
            frequency = math.clamp(frequency + driftFrequency * hardDriftAmplitude, playerFrequencyMin, playerFrequencyMax);
            amplitude = math.clamp(amplitude + driftAmplitude * hardDriftAmplitude * 0.5f, playerAmplitudeMin, playerAmplitudeMax);
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
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            bool lowTier = tier == HectonQualityTier.Unknown ||
                           tier == HectonQualityTier.Low ||
                           tier == HectonQualityTier.Mx350 ||
                           (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= lowTierVideoMemoryMb);
            return lowTier ? LowPointCount : HighPointCount;
        }

        private void TryRegisterTickHandlers()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterTickHandlers()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registeredUpdate = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        private void CompleteWaveJobForTeardown()
        {
            if (!_waveJobScheduled)
                return;

            _waveJobHandle.Complete();
            _waveJobScheduled = false;
        }

        private void DisposeNativeResources()
        {
            if (_waveJobScheduled)
                CompleteWaveJobForTeardown();

            DisposeNativeArray(ref _targetWave);
            DisposeNativeArray(ref _playerWave);
            DisposeNativeArray(ref _errorOutput);
            DisposeNativeArray(ref _gpuSegments);
            DisposeNativeArray(ref _stageTargets);
            DisposeNativeArray(ref _telemetryRing);
            _nativeReady = false;
        }

        private void DisposeGraphicsResources()
        {
            _segmentBuffer?.Dispose();
            _segmentBuffer = null;
            _argsBuffer?.Dispose();
            _argsBuffer = null;
            DestroyRuntimeMaterial();
            DestroyRuntimeQuadMesh();
            _resolvedMesh = null;
            _graphicsReady = false;
            _lastArgsInstanceCount = -1;
        }

        private Mesh ResolveRuntimeQuadMesh()
        {
            if (_runtimeQuadMesh != null)
                return _runtimeQuadMesh;

            Mesh mesh = new Mesh
            {
                name = "H8_FrequencyTuningQuad",
                hideFlags = HideFlags.DontSave
            }; // COLD ALLOC: Mesh[1] - PDA frequency tuning procedural quad - owner: PDADecryptionSpectrogramPanel
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            }; // COLD ALLOC: Vector3[4] - quad vertices - owner: PDADecryptionSpectrogramPanel
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            }; // COLD ALLOC: Vector2[4] - quad uvs - owner: PDADecryptionSpectrogramPanel
            mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 }; // COLD ALLOC: int[6] - quad indices - owner: PDADecryptionSpectrogramPanel
            mesh.RecalculateBounds();
            _runtimeQuadMesh = mesh;
            return _runtimeQuadMesh;
        }

        private void DestroyRuntimeQuadMesh()
        {
            if (_runtimeQuadMesh == null)
                return;

            Destroy(_runtimeQuadMesh);
            _runtimeQuadMesh = null;
        }

        private void DestroyRuntimeMaterial()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }

            _runtimeSourceMaterial = null;
            _materialBufferBound = false;
        }

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, nameof(PDADecryptionSpectrogramPanel), label, NativeAllocationLifetime.Scene);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
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

        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) ? math.max(0f, value) : fallback;
        }

        private static float Next01(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state & 0x00FFFFFFu) * Hash24ToUnit;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct FrequencyWaveGenerateJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<float> TargetWave;
            [WriteOnly] public NativeArray<float> PlayerWave;
            [NativeDisableParallelForRestriction] public NativeArray<FrequencyTuningWaveGpuSegment> GpuSegments;
            public int PointCount;
            public int SegmentCount;
            public float TargetFrequency;
            public float TargetAmplitude;
            public float PlayerFrequency;
            public float PlayerAmplitude;
            public float LocalWidth;
            public float LocalHeight;
            public int StageIndex;

            public void Execute(int index)
            {
                float invCount = math.rcp(math.max(1, PointCount - 1));
                float normalized = index * invCount;
                float x = normalized * TwoPi;
                float target = math.sin(x * TargetFrequency) * TargetAmplitude;
                float player = math.sin(x * PlayerFrequency) * PlayerAmplitude;
                TargetWave[index] = target;
                PlayerWave[index] = player;

                if (index >= SegmentCount)
                    return;

                float localX = (normalized - 0.5f) * LocalWidth;
                float targetY = 0.18f * LocalHeight + target * LocalHeight * 0.32f;
                float playerY = -0.18f * LocalHeight + player * LocalHeight * 0.32f;
                float nextNormalized = (index + 1) * invCount;
                float nextX = nextNormalized * TwoPi;
                float nextLocalX = (nextNormalized - 0.5f) * LocalWidth;
                float nextTarget = math.sin(nextX * TargetFrequency) * TargetAmplitude;
                float nextPlayer = math.sin(nextX * PlayerFrequency) * PlayerAmplitude;
                float nextTargetY = 0.18f * LocalHeight + nextTarget * LocalHeight * 0.32f;
                float nextPlayerY = -0.18f * LocalHeight + nextPlayer * LocalHeight * 0.32f;
                GpuSegments[index] = BuildSegment(
                    new float2(localX, targetY),
                    new float2(nextLocalX, nextTargetY),
                    new float4(1f, 0.08f, 0.04f, 0.92f + StageIndex * 0.02f));
                GpuSegments[SegmentCount + index] = BuildSegment(
                    new float2(localX, playerY),
                    new float2(nextLocalX, nextPlayerY),
                    new float4(0.02f, 0.82f, 1f, 0.92f));
            }

            private static FrequencyTuningWaveGpuSegment BuildSegment(float2 start, float2 end, float4 color)
            {
                float2 delta = end - start;
                float lengthSq = math.max(math.dot(delta, delta), 0.00000001f);
                float invLength = math.rsqrt(lengthSq);
                float length = lengthSq * invLength;
                float2 tangent = delta * invLength;
                float2 center = (start + end) * 0.5f;
                return new FrequencyTuningWaveGpuSegment
                {
                    CenterRadius = new float4(center.x, center.y, 0f, 1f),
                    TangentLength = new float4(tangent.x, tangent.y, 0f, length),
                    ColorStage = color
                };
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct FrequencyWaveErrorJob : IJob
        {
            [ReadOnly] public NativeArray<float> TargetWave;
            [ReadOnly] public NativeArray<float> PlayerWave;
            [WriteOnly] public NativeArray<float> ErrorOutput;
            public int PointCount;

            public void Execute()
            {
                float error = 0f;
                int count = math.max(1, PointCount);
                for (int i = 0; i < count; i++)
                    error += math.abs(TargetWave[i] - PlayerWave[i]);

                ErrorOutput[0] = math.saturate(error * math.rcp(count * 2f));
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FrequencyTuningStageTarget
        {
            public float Frequency;
            public float Amplitude;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FrequencyTuningWaveGpuSegment
        {
            public float4 CenterRadius;
            public float4 TangentLength;
            public float4 ColorStage;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct FrequencyTuningTelemetryEntry
        {
            public uint Frame;
            public uint ArtifactHash;
            public float TargetFrequency;
            public float TargetAmplitude;
            public float PlayerFrequency;
            public float PlayerAmplitude;
            public float Error01;
            public ushort HoldPermille;
            public byte Stage;
            public byte Flags;
        }
    }
}

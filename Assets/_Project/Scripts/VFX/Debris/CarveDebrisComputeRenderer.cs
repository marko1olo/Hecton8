using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.VFX.Debris
{
    /// <summary>
    /// GPU-only rock chip feedback for voxel SDF carve events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarveDebrisComputeRenderer : MonoBehaviour, IUpdatable
    {
        private const int MaxCarveDebrisCount = 4096;
        private const int ThreadGroupFallbackSize = 64;
        private const int BlackBoxCapacity = 300;
        private const int JobStateLength = 5;
        private const int JobStateActiveIndex = 0;
        private const int JobStateInjectedIndex = 1;
        private const int JobStateDirtyMinIndex = 2;
        private const int JobStateDirtyMaxIndex = 3;
        private const int JobStateFlagsIndex = 4;
        private const int LowTierParticlesPerCarve = 16;
        private const int HighTierParticlesPerCarve = 64;
        private const int MaxCarveSignalsPerFrame = 32;
        private const int TelemetryPublishStride = 30;
        private const uint TelemetryContextHash = 0x56465844u; // VFXD
        private const uint ActiveCountTelemetryHash = 0x43444252u; // CDBR
        private const uint InvalidStateFlag = 1u;
        private const uint LowTierFlag = 1u << 1;
        private const uint SdfActiveFlag = 1u << 2;
        private const uint FlowActiveFlag = 1u << 3;
        private const string DumpPath = "Docs/AgentLogs/Dump_VFX_SDF_CARVE_DEBRIS.bin";

        private static readonly int CarveDebrisReadId = Shader.PropertyToID("_CarveDebrisRead");
        private static readonly int CarveDebrisWriteId = Shader.PropertyToID("_CarveDebrisWrite");
        private static readonly int CarveDebrisVelocityReadId = Shader.PropertyToID("_CarveDebrisVelocityRead");
        private static readonly int CarveDebrisVelocityWriteId = Shader.PropertyToID("_CarveDebrisVelocityWrite");
        private static readonly int CarveDebrisVisibleIndicesId = Shader.PropertyToID("_CarveDebrisVisibleIndices");
        private static readonly int CarveDebrisIndirectArgsId = Shader.PropertyToID("_CarveDebrisIndirectArgs");
        private static readonly int CarveDebrisCountsId = Shader.PropertyToID("_CarveDebrisCounts");
        private static readonly int CarveDebrisParamsId = Shader.PropertyToID("_CarveDebrisParams");
        private static readonly int CarveDebrisForcesId = Shader.PropertyToID("_CarveDebrisForces");
        private static readonly int CarveDebrisAupShiftDeltaId = Shader.PropertyToID("_CarveDebrisAupShiftDelta");
        private static readonly int CarveDebrisCameraParamsId = Shader.PropertyToID("_CarveDebrisCameraParams");
        private static readonly int CarveDebrisDrawArgsParamsId = Shader.PropertyToID("_CarveDebrisDrawArgsParams");
        private static readonly int CarveDebrisMaterialParamsId = Shader.PropertyToID("_CarveDebrisMaterialParams");
        private static readonly int AbyssalFlowFieldResultId = Shader.PropertyToID("_AbyssalFlowFieldResult");
        private static readonly int AbyssalFlowFieldTextureId = Shader.PropertyToID("_AbyssalFlowFieldTexture");
        private static readonly int AbyssalGridResolutionId = Shader.PropertyToID("_AbyssalGridResolution");
        private static readonly int AbyssalFlowCenterId = Shader.PropertyToID("_AbyssalFlowCenter");
        private static readonly int AbyssalFlowSpacingId = Shader.PropertyToID("_AbyssalFlowSpacing");
        private static readonly int AbyssalFlowTextureParamsId = Shader.PropertyToID("_AbyssalFlowTextureParams");
        private static readonly int AbyssalFlowTextureActiveId = Shader.PropertyToID("_AbyssalFlowTextureActive");
        private static readonly int VoxelSdfTexture3DId = Shader.PropertyToID("_VoxelSdfTexture3D");
        private static readonly int VoxelSdfWorldToLocalId = Shader.PropertyToID("_VoxelSdfWorldToLocal");
        private static readonly int VoxelSdfInvDoubleHalfExtentsId = Shader.PropertyToID("_VoxelSdfInvDoubleHalfExtents");
        private static readonly int FluidAdvectionParamsId = Shader.PropertyToID("_FluidAdvectionParams");
        private static readonly int FluidAdvectionSdfParamsId = Shader.PropertyToID("_FluidAdvectionSdfParams");

        [Header("Compute")]
        [SerializeField] private ComputeShader fluidAdvectionCompute;
        [SerializeField, Min(0.1f)] private float particleLifetimeSeconds = 5f;
        [SerializeField, Min(0f)] private float spawnRadiusScale = 0.85f;
        [SerializeField, Min(0f)] private float initialVelocityMetersPerSecond = 4.5f;
        [SerializeField, Min(0f)] private float dragToFlow = 0.18f;
        [SerializeField] private Vector3 gravityMetersPerSecondSq = new Vector3(0f, -5.25f, 0f);

        [Header("SDF / Flow")]
        [SerializeField] private Texture3D voxelSdfTexture3D;
        [SerializeField] private Matrix4x4 voxelSdfWorldToLocal = Matrix4x4.identity;
        [SerializeField] private Vector4 voxelSdfInvDoubleHalfExtents = Vector4.zero;
        [SerializeField, Range(0f, 1f)] private float solidDensityThreshold = 0.5f;
        [SerializeField] private Texture3D abyssalFlowTextureOverride;

        [Header("Render")]
        [SerializeField] private Mesh debrisMesh;
        [SerializeField] private Material debrisMaterial;
        [SerializeField] private Camera renderCamera;
        [SerializeField] private Bounds drawBounds = new Bounds(Vector3.zero, new Vector3(400f, 400f, 400f));
        [SerializeField, Min(0f)] private float renderDistanceMeters = 220f;
        [SerializeField, Min(0.01f)] private float minRockScale = 0.035f;
        [SerializeField, Min(0.01f)] private float maxRockScale = 0.18f;
        [SerializeField] private int renderLayer;
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

        private NativeArray<float4> _debrisPositions;
        private NativeArray<float4> _debrisVelocities;
        private NativeArray<int> _jobState;
        private NativeArray<CarveDebrisTelemetryEntry> _blackBox;
        private GraphicsBuffer _positionBufferA;
        private GraphicsBuffer _positionBufferB;
        private GraphicsBuffer _velocityBufferA;
        private GraphicsBuffer _velocityBufferB;
        private GraphicsBuffer _visibleIndicesBuffer;
        private GraphicsBuffer _indirectArgsBuffer;
        private GraphicsBuffer _emptyFlowBuffer;
        private Texture3D _emptyTexture3D;
        private Mesh _ownedMesh;
        private Material _ownedMaterial;
        private int _advectKernel = -1;
        private int _clearArgsKernel = -1;
        private int _cullKernel = -1;
        private int _threadGroupSize = ThreadGroupFallbackSize;
        private int _bufferParity;
        private int _activeMirrorCount;
        private int _blackBoxCursor;
        private int _lastTelemetryFrame = -1;
        private uint _lastProcessedAupShiftFrameId;
        private uint _frameSequence;
        private float3 _pendingAupShift;
        private bool _registered;
        private bool _gpuReady;
        private bool _blackBoxDumped;
        private bool _cameraResolveAttempted;
        private bool _materialFallbackAttempted;

        private void OnEnable()
        {
            TryRegisterTick();
            TryEnsureGpuState();
        }

        private void OnDisable()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registered = false;
            }

            ReleaseGpuState();
        }

        private void Reset()
        {
            drawBounds = new Bounds(Vector3.zero, new Vector3(400f, 400f, 400f));
            renderLayer = gameObject.layer;
        }

        public void Tick(float deltaTime)
        {
            if (!enabled)
                return;

            if (!TryEnsureGpuState())
                return;

            float dt = math.clamp(deltaTime, 0.0001f, 0.0666667f);
            bool lowTier = IsLowTier();
            DrainAupShiftSignals();
            AgeMirror(dt);

            int queuedCarves = DrainCarveSignals(lowTier);
            DispatchGpu(dt, lowTier);
            WriteBlackBox(queuedCarves, _jobState.IsCreated ? _jobState[JobStateInjectedIndex] : 0, lowTier);
            RenderDebris();
            _frameSequence++;
        }

        private void TryRegisterTick()
        {
            if (_registered)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private bool TryEnsureGpuState()
        {
            if (_gpuReady && IsGpuStateValid())
                return true;

            if (fluidAdvectionCompute == null)
                return false;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            _advectKernel = ResolveKernel(fluidAdvectionCompute, "AdvectCarveDebris");
            _clearArgsKernel = ResolveKernel(fluidAdvectionCompute, "ClearCarveDebrisIndirectArgs");
            _cullKernel = ResolveKernel(fluidAdvectionCompute, "CullCarveDebrisForRender");
            if (_advectKernel < 0 || _clearArgsKernel < 0 || _cullKernel < 0)
                return false;

            fluidAdvectionCompute.GetKernelThreadGroupSizes(_advectKernel, out uint kernelThreads, out _, out _);
            _threadGroupSize = kernelThreads > 0u ? (int)math.min(kernelThreads, 1024u) : ThreadGroupFallbackSize;

            _debrisPositions = vault.GetBuffer<float4>(
                BufferID.CarveDebris,
                MaxCarveDebrisCount,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            _debrisVelocities = vault.GetBuffer<float4>(
                BufferID.CarveDebrisVelocity,
                MaxCarveDebrisCount,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);

            if (!_debrisPositions.IsCreated ||
                !_debrisVelocities.IsCreated ||
                _debrisPositions.Length < MaxCarveDebrisCount ||
                _debrisVelocities.Length < MaxCarveDebrisCount)
            {
                return false;
            }

            if (!_jobState.IsCreated)
                _jobState = H8Memory.Allocate<int>(JobStateLength, SystemID.Vfx, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (!_blackBox.IsCreated)
                _blackBox = H8Memory.Allocate<CarveDebrisTelemetryEntry>(BlackBoxCapacity, SystemID.Vfx, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            if (!_jobState.IsCreated || !_blackBox.IsCreated)
                return false;

            AllocateGraphicsBuffers();
            CreateEmptyResources();
            ClearMirrorsAndUpload();
            _gpuReady = IsGpuStateValid();
            return _gpuReady;
        }

        private static int ResolveKernel(ComputeShader compute, string kernelName)
        {
            return compute != null && compute.HasKernel(kernelName) ? compute.FindKernel(kernelName) : -1;
        }

        private bool IsGpuStateValid()
        {
            return _positionBufferA != null && _positionBufferA.IsValid() &&
                   _positionBufferB != null && _positionBufferB.IsValid() &&
                   _velocityBufferA != null && _velocityBufferA.IsValid() &&
                   _velocityBufferB != null && _velocityBufferB.IsValid() &&
                   _visibleIndicesBuffer != null && _visibleIndicesBuffer.IsValid() &&
                   _indirectArgsBuffer != null && _indirectArgsBuffer.IsValid() &&
                   _emptyFlowBuffer != null && _emptyFlowBuffer.IsValid() &&
                   _emptyTexture3D != null;
        }

        private void AllocateGraphicsBuffers()
        {
            if (_positionBufferA == null || !_positionBufferA.IsValid())
                _positionBufferA = CreateStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_positionBufferB == null || !_positionBufferB.IsValid())
                _positionBufferB = CreateStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_velocityBufferA == null || !_velocityBufferA.IsValid())
                _velocityBufferA = CreateStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_velocityBufferB == null || !_velocityBufferB.IsValid())
                _velocityBufferB = CreateStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_visibleIndicesBuffer == null || !_visibleIndicesBuffer.IsValid())
                _visibleIndicesBuffer = CreateStructuredBuffer<uint>(MaxCarveDebrisCount);
            if (_emptyFlowBuffer == null || !_emptyFlowBuffer.IsValid())
                _emptyFlowBuffer = CreateStructuredBuffer<float4>(1);
            if (_indirectArgsBuffer == null || !_indirectArgsBuffer.IsValid())
            {
                _indirectArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - compute-written indirect rock debris args - owner: VFX_SDF_CARVE_DEBRIS
            }
        }

        private static GraphicsBuffer CreateStructuredBuffer<T>(int count)
            where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                math.max(1, count),
                UnsafeUtility.SizeOf<T>()); // COLD ALLOC: GraphicsBuffer[count] - persistent carve debris GPU lane - owner: VFX_SDF_CARVE_DEBRIS
        }

        private void CreateEmptyResources()
        {
            if (_emptyTexture3D != null)
                return;

            _emptyTexture3D = new Texture3D(1, 1, 1, TextureFormat.RGBAFloat, false)
            {
                name = "Hecton Empty CarveDebris 3D Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            _emptyTexture3D.SetPixel(0, 0, 0, Color.clear);
            _emptyTexture3D.Apply(false, true);
        }

        private void ClearMirrorsAndUpload()
        {
            for (int i = 0; i < MaxCarveDebrisCount; i++)
            {
                _debrisPositions[i] = default;
                _debrisVelocities[i] = default;
            }

            _jobState[JobStateActiveIndex] = 0;
            _jobState[JobStateInjectedIndex] = 0;
            _jobState[JobStateDirtyMinIndex] = MaxCarveDebrisCount;
            _jobState[JobStateDirtyMaxIndex] = -1;
            _jobState[JobStateFlagsIndex] = 0;
            _activeMirrorCount = 0;
            UploadRange(_positionBufferA, _debrisPositions, 0, MaxCarveDebrisCount);
            UploadRange(_positionBufferB, _debrisPositions, 0, MaxCarveDebrisCount);
            UploadRange(_velocityBufferA, _debrisVelocities, 0, MaxCarveDebrisCount);
            UploadRange(_velocityBufferB, _debrisVelocities, 0, MaxCarveDebrisCount);
            NativeArray<float4> empty = _emptyFlowBuffer.LockBufferForWrite<float4>(0, 1);
            empty[0] = default;
            _emptyFlowBuffer.UnlockBufferAfterWrite<float4>(1);
        }

        private void AgeMirror(float dt)
        {
            if (!_debrisPositions.IsCreated || !_jobState.IsCreated)
                return;

            float lifeDelta = dt / math.max(0.001f, particleLifetimeSeconds);
            JobHandle handle = new AgeCarveDebrisMirrorJob
            {
                Positions = _debrisPositions,
                Capacity = MaxCarveDebrisCount,
                LifeDelta = lifeDelta,
                JobState = _jobState
            }.Schedule();
            handle.Complete();
            _activeMirrorCount = math.clamp(_jobState[JobStateActiveIndex], 0, MaxCarveDebrisCount);
        }

        private int DrainCarveSignals(bool lowTier)
        {
            ReadOnlySpan<VoxelCarveEvent> carveSignals = SignalBus<VoxelCarveEvent>.GetFrameSnapshot();
            int signalCount = math.min(carveSignals.Length, MaxCarveSignalsPerFrame);
            if (signalCount <= 0)
                return 0;

            int particlesPerCarve = lowTier ? LowTierParticlesPerCarve : HighTierParticlesPerCarve;
            int queuedCarves = 0;
            int injectedTotal = 0;
            for (int i = 0; i < signalCount; i++)
            {
                VoxelCarveEvent carveEvent = carveSignals[i];
                if (!IsFiniteCarveEvent(in carveEvent))
                {
                    _jobState[JobStateFlagsIndex] |= (int)InvalidStateFlag;
                    continue;
                }

                float radius = math.max(0.05f, carveEvent.RadiusMeters * spawnRadiusScale);
                float3 runtimeCenter = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                    carveEvent.AbsoluteHitPoint.x,
                    carveEvent.AbsoluteHitPoint.y,
                    carveEvent.AbsoluteHitPoint.z)).ToRuntimeFloat3();
                uint seed = BuildStableSeed(_frameSequence, in carveEvent, i);

                JobHandle handle = new CarveDebrisInjectJob
                {
                    Positions = _debrisPositions,
                    Velocities = _debrisVelocities,
                    Capacity = MaxCarveDebrisCount,
                    Center = runtimeCenter,
                    Radius = radius,
                    ParticlesToInject = particlesPerCarve,
                    InitialSpeed = initialVelocityMetersPerSecond,
                    Life = 1f,
                    Seed = seed,
                    JobState = _jobState
                }.Schedule();
                handle.Complete();

                int dirtyMin = _jobState[JobStateDirtyMinIndex];
                int dirtyMax = _jobState[JobStateDirtyMaxIndex];
                if (dirtyMax >= dirtyMin)
                    UploadInjectedRange(dirtyMin, dirtyMax - dirtyMin + 1);

                queuedCarves++;
                injectedTotal += math.max(0, _jobState[JobStateInjectedIndex]);
                _activeMirrorCount = math.clamp(_jobState[JobStateActiveIndex], 0, MaxCarveDebrisCount);
            }

            _jobState[JobStateInjectedIndex] = injectedTotal;
            return queuedCarves;
        }

        private void UploadInjectedRange(int start, int count)
        {
            int safeStart = math.clamp(start, 0, MaxCarveDebrisCount - 1);
            int safeCount = math.clamp(count, 0, MaxCarveDebrisCount - safeStart);
            if (safeCount <= 0)
                return;

            UploadRange(_positionBufferA, _debrisPositions, safeStart, safeCount);
            UploadRange(_positionBufferB, _debrisPositions, safeStart, safeCount);
            UploadRange(_velocityBufferA, _debrisVelocities, safeStart, safeCount);
            UploadRange(_velocityBufferB, _debrisVelocities, safeStart, safeCount);
        }

        private void DispatchGpu(float dt, bool lowTier)
        {
            if (_activeMirrorCount <= 0 && math.lengthsq(_pendingAupShift) <= 0.000001f)
                return;

            Mesh mesh = ResolveMesh();
            if (mesh == null || mesh.GetIndexCount(0) == 0)
                return;

            bool readA = (_bufferParity & 1) == 0;
            GraphicsBuffer positionRead = readA ? _positionBufferA : _positionBufferB;
            GraphicsBuffer positionWrite = readA ? _positionBufferB : _positionBufferA;
            GraphicsBuffer velocityRead = readA ? _velocityBufferA : _velocityBufferB;
            GraphicsBuffer velocityWrite = readA ? _velocityBufferB : _velocityBufferA;
            int dispatchGroups = (MaxCarveDebrisCount + _threadGroupSize - 1) / _threadGroupSize;
            Vector4 drawArgs = new Vector4(mesh.GetIndexCount(0), mesh.GetIndexStart(0), mesh.GetBaseVertex(0), MaxCarveDebrisCount);

            BindSharedComputeParams(dt, lowTier, drawArgs);
            fluidAdvectionCompute.SetBuffer(_clearArgsKernel, CarveDebrisIndirectArgsId, _indirectArgsBuffer);
            fluidAdvectionCompute.Dispatch(_clearArgsKernel, 1, 1, 1);

            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisReadId, positionRead);
            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisWriteId, positionWrite);
            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisVelocityReadId, velocityRead);
            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisVelocityWriteId, velocityWrite);
            fluidAdvectionCompute.Dispatch(_advectKernel, dispatchGroups, 1, 1);

            fluidAdvectionCompute.SetBuffer(_cullKernel, CarveDebrisReadId, positionWrite);
            fluidAdvectionCompute.SetBuffer(_cullKernel, CarveDebrisVisibleIndicesId, _visibleIndicesBuffer);
            fluidAdvectionCompute.SetBuffer(_cullKernel, CarveDebrisIndirectArgsId, _indirectArgsBuffer);
            fluidAdvectionCompute.Dispatch(_cullKernel, dispatchGroups, 1, 1);

            _bufferParity ^= 1;
            _pendingAupShift = default;
        }

        private void BindSharedComputeParams(float dt, bool lowTier, Vector4 drawArgs)
        {
            Camera camera = ResolveCamera();
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float renderDistanceSq = renderDistanceMeters > 0f ? renderDistanceMeters * renderDistanceMeters : 0f;
            Texture flowTexture = ResolveFlowTexture(out Vector4 flowCenter, out Vector4 flowSpacing, out Vector4 flowTextureParams, out float flowTextureActive);
            Texture sdfTexture = ResolveSdfTexture(lowTier, out float sdfActive);

            fluidAdvectionCompute.SetVector(CarveDebrisCountsId, new Vector4(MaxCarveDebrisCount, _activeMirrorCount, MaxCarveDebrisCount, _frameSequence));
            fluidAdvectionCompute.SetVector(CarveDebrisParamsId, new Vector4(dt, lowTier ? 1f : 0f, sdfActive, dragToFlow));
            fluidAdvectionCompute.SetVector(CarveDebrisForcesId, new Vector4(gravityMetersPerSecondSq.x, gravityMetersPerSecondSq.y, gravityMetersPerSecondSq.z, 1f / math.max(0.001f, particleLifetimeSeconds)));
            fluidAdvectionCompute.SetVector(CarveDebrisAupShiftDeltaId, new Vector4(_pendingAupShift.x, _pendingAupShift.y, _pendingAupShift.z, 0f));
            fluidAdvectionCompute.SetVector(CarveDebrisCameraParamsId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, renderDistanceSq));
            fluidAdvectionCompute.SetVector(CarveDebrisDrawArgsParamsId, drawArgs);
            fluidAdvectionCompute.SetBuffer(_advectKernel, AbyssalFlowFieldResultId, _emptyFlowBuffer);
            fluidAdvectionCompute.SetTexture(_advectKernel, AbyssalFlowFieldTextureId, flowTexture);
            fluidAdvectionCompute.SetTexture(_advectKernel, VoxelSdfTexture3DId, sdfTexture);
            fluidAdvectionCompute.SetVector(AbyssalGridResolutionId, Vector4.zero);
            fluidAdvectionCompute.SetVector(AbyssalFlowCenterId, flowCenter);
            fluidAdvectionCompute.SetVector(AbyssalFlowSpacingId, flowSpacing);
            fluidAdvectionCompute.SetVector(AbyssalFlowTextureParamsId, flowTextureParams);
            fluidAdvectionCompute.SetFloat(AbyssalFlowTextureActiveId, flowTextureActive);
            fluidAdvectionCompute.SetMatrix(VoxelSdfWorldToLocalId, voxelSdfWorldToLocal);
            fluidAdvectionCompute.SetVector(VoxelSdfInvDoubleHalfExtentsId, voxelSdfInvDoubleHalfExtents);
            fluidAdvectionCompute.SetVector(FluidAdvectionParamsId, new Vector4(dt, lowTier ? 1f : 0f, flowTextureActive > 0.5f ? 1f : 0f, sdfActive));
            fluidAdvectionCompute.SetVector(FluidAdvectionSdfParamsId, new Vector4(sdfActive, solidDensityThreshold, 0f, 0f));
        }

        private Texture ResolveFlowTexture(out Vector4 flowCenter, out Vector4 flowSpacing, out Vector4 flowTextureParams, out float flowTextureActive)
        {
            flowCenter = Shader.GetGlobalVector(AbyssalFlowCenterId);
            flowSpacing = Shader.GetGlobalVector(AbyssalFlowSpacingId);
            flowTextureParams = Shader.GetGlobalVector(AbyssalFlowTextureParamsId);
            Texture flowTexture = abyssalFlowTextureOverride != null
                ? abyssalFlowTextureOverride
                : Shader.GetGlobalTexture(AbyssalFlowFieldTextureId);
            flowTextureActive = flowTexture != null && flowTextureParams.w > 0f
                ? math.max(Shader.GetGlobalFloat(AbyssalFlowTextureActiveId), 1f)
                : 0f;
            return flowTexture != null ? flowTexture : _emptyTexture3D;
        }

        private Texture ResolveSdfTexture(bool lowTier, out float sdfActive)
        {
            bool hasSdf = !lowTier &&
                          voxelSdfTexture3D != null &&
                          voxelSdfInvDoubleHalfExtents.x > 0f &&
                          voxelSdfInvDoubleHalfExtents.y > 0f &&
                          voxelSdfInvDoubleHalfExtents.z > 0f;
            sdfActive = hasSdf ? 1f : 0f;
            return hasSdf ? voxelSdfTexture3D : _emptyTexture3D;
        }

        private void RenderDebris()
        {
            if (_activeMirrorCount <= 0 ||
                _visibleIndicesBuffer == null ||
                _indirectArgsBuffer == null)
            {
                return;
            }

            Mesh mesh = ResolveMesh();
            Material material = ResolveMaterial();
            if (mesh == null || material == null)
                return;

            GraphicsBuffer currentPositionBuffer = (_bufferParity & 1) == 0 ? _positionBufferA : _positionBufferB;
            material.SetBuffer(CarveDebrisReadId, currentPositionBuffer);
            material.SetBuffer(CarveDebrisVisibleIndicesId, _visibleIndicesBuffer);
            material.SetVector(CarveDebrisMaterialParamsId, new Vector4(minRockScale, math.max(minRockScale, maxRockScale), particleLifetimeSeconds, 0f));

            RenderParams renderParams = new RenderParams(material)
            {
                worldBounds = drawBounds,
                layer = renderLayer,
                shadowCastingMode = shadowCastingMode,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
            };
            Graphics.RenderMeshIndirect(renderParams, mesh, _indirectArgsBuffer, 1, 0);
        }

        private Mesh ResolveMesh()
        {
            if (debrisMesh != null)
                return debrisMesh;
            if (_ownedMesh != null)
                return _ownedMesh;

            _ownedMesh = BuildOctahedronMesh();
            return _ownedMesh;
        }

        private Material ResolveMaterial()
        {
            if (debrisMaterial != null)
                return debrisMaterial;
            if (_ownedMaterial != null)
                return _ownedMaterial;
            if (_materialFallbackAttempted)
                return null;

            _materialFallbackAttempted = true;
            Shader shader = Shader.Find("Hecton8/VFX/CarveDebrisIndirect");
            if (shader == null)
                return null;

            _ownedMaterial = new Material(shader)
            {
                name = "Hecton Runtime Carve Debris Material"
            };
            return _ownedMaterial;
        }

        private Camera ResolveCamera()
        {
            if (renderCamera != null)
                return renderCamera;
            if (_cameraResolveAttempted)
                return null;

            _cameraResolveAttempted = true;
            renderCamera = Camera.main;
            return renderCamera;
        }

        private void DrainAupShiftSignals()
        {
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                AupShiftSignal signal = shifts[i];
                if (signal.ShiftFrameId == 0u || signal.ShiftFrameId == _lastProcessedAupShiftFrameId)
                    continue;

                _lastProcessedAupShiftFrameId = signal.ShiftFrameId;
                if (!math.all(math.isfinite(signal.ShiftMeters)))
                {
                    _jobState[JobStateFlagsIndex] |= (int)InvalidStateFlag;
                    continue;
                }

                _pendingAupShift += -signal.ShiftMeters;
            }

            if (_activeMirrorCount <= 0)
                _pendingAupShift = default;
        }

        private bool IsLowTier()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return GlobalRegistry.H8_LOW_MEMORY_PROFILE ||
                   GlobalRegistry.ScalabilityTierProfileByte == 0 ||
                   tier == HectonQualityTier.Unknown ||
                   tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350;
        }

        private static bool IsFiniteCarveEvent(in VoxelCarveEvent carveEvent)
        {
            return carveEvent.RadiusMeters > 0f &&
                   math.isfinite(carveEvent.RadiusMeters) &&
                   math.all(math.isfinite(carveEvent.AbsoluteHitPoint));
        }

        private static uint BuildStableSeed(uint frame, in VoxelCarveEvent carveEvent, int eventIndex)
        {
            uint hash = 2166136261u;
            hash = (hash ^ frame) * 16777619u;
            hash = (hash ^ (uint)eventIndex) * 16777619u;
            hash = (hash ^ (uint)carveEvent.VolumeInstanceId) * 16777619u;
            hash = (hash ^ math.asuint(carveEvent.AbsoluteHitPoint.x)) * 16777619u;
            hash = (hash ^ math.asuint(carveEvent.AbsoluteHitPoint.y)) * 16777619u;
            hash = (hash ^ math.asuint(carveEvent.AbsoluteHitPoint.z)) * 16777619u;
            hash = (hash ^ math.asuint(carveEvent.RadiusMeters)) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private void WriteBlackBox(int queuedCarves, int injectedParticles, bool lowTier)
        {
            if (!_blackBox.IsCreated || _blackBox.Length == 0)
                return;

            int frame = Time.frameCount;
            if (_lastTelemetryFrame == frame)
                return;

            _lastTelemetryFrame = frame;
            uint flags = (uint)math.max(0, _jobState[JobStateFlagsIndex]);
            flags |= lowTier ? LowTierFlag : 0u;
            flags |= voxelSdfTexture3D != null && !lowTier ? SdfActiveFlag : 0u;
            flags |= abyssalFlowTextureOverride != null || Shader.GetGlobalTexture(AbyssalFlowFieldTextureId) != null ? FlowActiveFlag : 0u;
            uint hash = BuildTelemetryHash(_activeMirrorCount, queuedCarves, injectedParticles, flags);
            _blackBox[_blackBoxCursor] = new CarveDebrisTelemetryEntry
            {
                FrameIndex = (uint)frame,
                ActiveCarveDebrisCount = _activeMirrorCount,
                QueuedCarves = queuedCarves,
                InjectedParticles = injectedParticles,
                Flags = flags,
                StateHash = hash,
                PendingAupShift = _pendingAupShift
            };
            _blackBoxCursor = (_blackBoxCursor + 1) % _blackBox.Length;

            if ((frame % TelemetryPublishStride) == 0)
                GlobalTelemetryBus.PublishPerformanceWarning(ActiveCountTelemetryHash, TelemetryContextHash, _activeMirrorCount);

            if ((flags & InvalidStateFlag) != 0u)
                DumpBlackBoxOnce(flags);

            _jobState[JobStateFlagsIndex] = 0;
        }

        private static uint BuildTelemetryHash(int activeCount, int queuedCarves, int injectedParticles, uint flags)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)activeCount) * 16777619u;
            hash = (hash ^ (uint)queuedCarves) * 16777619u;
            hash = (hash ^ (uint)injectedParticles) * 16777619u;
            hash = (hash ^ flags) * 16777619u;
            return hash;
        }

        private unsafe void DumpBlackBoxOnce(uint reasonFlags)
        {
            if (_blackBoxDumped || !_blackBox.IsCreated)
                return;

            _blackBoxDumped = true;
            string path = Path.Combine(Application.dataPath, "..", DumpPath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int entrySize = UnsafeUtility.SizeOf<CarveDebrisTelemetryEntry>();
            byte[] bytes = new byte[entrySize * _blackBox.Length + sizeof(uint)];
            fixed (byte* bytesPtr = bytes)
            {
                UnsafeUtility.CopyStructureToPtr(ref reasonFlags, bytesPtr);
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_blackBox);
                UnsafeUtility.MemCpy(bytesPtr + sizeof(uint), source, entrySize * _blackBox.Length);
            }

            File.WriteAllBytes(path, bytes);
        }

        private void ReleaseGpuState()
        {
            ReleaseBuffer(ref _positionBufferA);
            ReleaseBuffer(ref _positionBufferB);
            ReleaseBuffer(ref _velocityBufferA);
            ReleaseBuffer(ref _velocityBufferB);
            ReleaseBuffer(ref _visibleIndicesBuffer);
            ReleaseBuffer(ref _indirectArgsBuffer);
            ReleaseBuffer(ref _emptyFlowBuffer);
            H8Memory.Release(ref _jobState);
            H8Memory.Release(ref _blackBox);
            if (_ownedMesh != null)
                DestroyUnityObject(_ownedMesh);
            if (_ownedMaterial != null)
                DestroyUnityObject(_ownedMaterial);
            if (_emptyTexture3D != null)
                DestroyUnityObject(_emptyTexture3D);
            _ownedMesh = null;
            _ownedMaterial = null;
            _emptyTexture3D = null;
            _debrisPositions = default;
            _debrisVelocities = default;
            _gpuReady = false;
            _activeMirrorCount = 0;
            _cameraResolveAttempted = false;
            _materialFallbackAttempted = false;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static void DestroyUnityObject(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(unityObject);
            else
                UnityEngine.Object.DestroyImmediate(unityObject);
        }

        private static Mesh BuildOctahedronMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "Hecton Carve Debris Octahedron"
            };
            Vector3[] vertices =
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(0f, 0f, -1f),
                new Vector3(0f, -1f, 0f)
            };
            int[] indices =
            {
                0, 2, 1,
                0, 3, 2,
                0, 4, 3,
                0, 1, 4,
                5, 1, 2,
                5, 2, 3,
                5, 3, 4,
                5, 4, 1
            };
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static unsafe void UploadRange<T>(GraphicsBuffer destination, NativeArray<T> source, int start, int count)
            where T : struct
        {
            if (destination == null || !destination.IsValid() || !source.IsCreated || count <= 0)
                return;

            int safeStart = math.clamp(start, 0, math.max(0, source.Length - 1));
            int safeCount = math.min(count, math.min(source.Length - safeStart, destination.count - safeStart));
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(safeStart, safeCount);
            void* sourcePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source) + UnsafeUtility.SizeOf<T>() * safeStart;
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, UnsafeUtility.SizeOf<T>() * safeCount);
            destination.UnlockBufferAfterWrite<T>(safeCount);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AgeCarveDebrisMirrorJob : IJob
        {
            public NativeArray<float4> Positions;
            public int Capacity;
            public float LifeDelta;
            public NativeArray<int> JobState;

            public void Execute()
            {
                int active = 0;
                int flags = 0;
                int count = math.min(Capacity, Positions.Length);
                for (int i = 0; i < count; i++)
                {
                    float4 particle = Positions[i];
                    if (particle.w <= 0f)
                        continue;

                    if (!math.all(math.isfinite(particle)))
                    {
                        Positions[i] = default;
                        flags |= (int)InvalidStateFlag;
                        continue;
                    }

                    particle.w = math.max(0f, particle.w - LifeDelta);
                    Positions[i] = particle;
                    active += particle.w > 0f ? 1 : 0;
                }

                JobState[JobStateActiveIndex] = active;
                JobState[JobStateInjectedIndex] = 0;
                JobState[JobStateDirtyMinIndex] = Capacity;
                JobState[JobStateDirtyMaxIndex] = -1;
                JobState[JobStateFlagsIndex] = flags;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CarveDebrisInjectJob : IJob
        {
            public NativeArray<float4> Positions;
            public NativeArray<float4> Velocities;
            public int Capacity;
            public float3 Center;
            public float Radius;
            public int ParticlesToInject;
            public float InitialSpeed;
            public float Life;
            public uint Seed;
            public NativeArray<int> JobState;

            public void Execute()
            {
                int count = math.min(Capacity, math.min(Positions.Length, Velocities.Length));
                int injected = 0;
                int active = math.clamp(JobState[JobStateActiveIndex], 0, count);
                int dirtyMin = count;
                int dirtyMax = -1;
                int flags = JobState[JobStateFlagsIndex];

                if (!math.all(math.isfinite(Center)))
                {
                    JobState[JobStateFlagsIndex] = flags | (int)InvalidStateFlag;
                    return;
                }

                Unity.Mathematics.Random random = new Unity.Mathematics.Random(Seed == 0u ? 1u : Seed);
                int requested = math.clamp(ParticlesToInject, 0, count);
                float safeRadius = math.max(0.025f, Radius);
                float safeSpeed = math.max(0f, InitialSpeed);
                for (int i = 0; i < count && injected < requested; i++)
                {
                    if (Positions[i].w > 0f)
                        continue;

                    float3 raw = new float3(
                        random.NextFloat(-1f, 1f),
                        random.NextFloat(-0.15f, 1f),
                        random.NextFloat(-1f, 1f));
                    float lengthSq = math.lengthsq(raw);
                    float3 direction = lengthSq > 0.0001f ? raw * math.rsqrt(lengthSq) : new float3(0f, 1f, 0f);
                    float radius = safeRadius * random.NextFloat(0.05f, 1f);
                    float speed = safeSpeed * random.NextFloat(0.45f, 1.15f);
                    float3 position = Center + direction * radius;
                    float3 velocity = direction * speed + new float3(0f, safeSpeed * 0.35f, 0f);
                    if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity)))
                    {
                        flags |= (int)InvalidStateFlag;
                        continue;
                    }

                    Positions[i] = new float4(position, math.max(0.001f, Life));
                    Velocities[i] = new float4(velocity, 0f);
                    dirtyMin = math.min(dirtyMin, i);
                    dirtyMax = math.max(dirtyMax, i);
                    injected++;
                    active = math.min(count, active + 1);
                }

                JobState[JobStateActiveIndex] = active;
                JobState[JobStateInjectedIndex] = injected;
                JobState[JobStateDirtyMinIndex] = dirtyMin;
                JobState[JobStateDirtyMaxIndex] = dirtyMax;
                JobState[JobStateFlagsIndex] = flags;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CarveDebrisTelemetryEntry
        {
            public uint FrameIndex;
            public int ActiveCarveDebrisCount;
            public int QueuedCarves;
            public int InjectedParticles;
            public uint Flags;
            public uint StateHash;
            public float3 PendingAupShift;
        }
    }
}

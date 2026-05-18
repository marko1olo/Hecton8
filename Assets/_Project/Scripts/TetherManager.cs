using Hecton8.Core;
using Hecton8.Core.Memory;
using System.IO;
using System.Collections.Generic;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Physics
{
    /// <summary>
    /// Player-owned tether runtime host.
    /// Physics executes in <see cref="FixedTick(float)"/> and visuals render in <see cref="LateFrameTick"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Tether Manager")]
    public sealed class TetherManager : MonoBehaviour, IFixedTickable, ILateFrameTickable, ISlowTickable, IOriginShiftListener
    {
        private const string RuntimeShaderName = "Hecton8/Physics/TetherLineStrip";
        private static readonly int _TetherPositionsId = Shader.PropertyToID("_TetherPositions");
        private static readonly int _TetherSegmentTensionsId = Shader.PropertyToID("_TetherSegmentTensions");
        private static readonly int _TetherDrawParamsId = Shader.PropertyToID("_TetherDrawParams");
        private const int TetherBlackBoxCapacity = 300;
        private const int MaxManagedTetherInstances = 64;
        private const int InitialPooledTetherInstances = 64;
        private const string TetherBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_VERLET_CABLES_MANAGER.bin";
        private const string TetherBlackBoxH8DumpRelativePath = "Docs/AgentLogs/Dump_VERLET_CABLES_MANAGER.h8dump";
        private const ulong TetherBlackBoxMagic = 0x4D47524D48544554ul;
        private const float TetherFixedClockWrapSeconds = 4096f;
        // COLD ALLOC: Plane[6] - reused camera frustum for tether upload rejection - owner: TetherManager
        private static readonly Plane[] s_TetherFrustumPlanes = new Plane[6];

        // COLD ALLOC: Vector3[6] - canonical six-vertex segment impostor mesh for RenderMeshIndirect - owner: TetherManager
        private static readonly Vector3[] s_TetherIndirectSegmentVertices =
        {
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero
        };

        // COLD ALLOC: int[6] - one triangle pair index stream preserving SV_VertexID 0..5 - owner: TetherManager
        private static readonly int[] s_TetherIndirectSegmentIndices = { 0, 1, 2, 3, 4, 5 };

        [Header("Tether Rendering")]
        [Tooltip("Optional explicit material for tether line rendering. When omitted the manager creates a runtime material from the built-in tether shader.")]
        [SerializeField] private Material tetherRenderMaterial;

        [Tooltip("Fallback tether line tint used by the procedural line-strip renderer.")]
        [SerializeField] private Color tetherRenderColor = new Color(0.22f, 0.92f, 0.96f, 0.92f);

        [Tooltip("Cheap visual overdrive tint blended in as cable tension and stress rise.")]
        [SerializeField] private Color tetherStressColor = new Color(1f, 0.38f, 0.12f, 0.96f);

        [Tooltip("Maps per-segment constraint delta into localized stress glow.")]
        [SerializeField, Range(0.1f, 8f)] private float tetherSegmentStressScale = 2.5f;

        [Tooltip("World-space half-width used by the procedural tube impostor shader.")]
        [SerializeField, Range(0.01f, 0.35f)] private float tetherRenderRadius = 0.045f;

        [Tooltip("Padding applied around per-tether bounds before the procedural draw is submitted.")]
        [SerializeField, Range(0f, 4f)] private float tetherBoundsPadding = 1.2f;

        [Tooltip("Optional explicit camera used for tether rendering. Null renders to all cameras.")]
        [SerializeField] private Camera renderCamera;

        [Tooltip("Maximum tether count allowed to use virtual bend detection and catenary rendering simultaneously.")]
        [SerializeField, Range(1, 8)] private int maxVisualizedTethers = 4;

        [Header("Tether Profiles")]
        [Tooltip("Optional authored tow-cable profile. When omitted the runtime falls back to HeavyTowWinch tuning.")]
        [SerializeField] private TetherProfileSO towCableProfile;

        [Header("Diagnostics")]
#pragma warning disable CS0414
        [SerializeField] private int _debugActiveTetherCount;
        [SerializeField] private float _debugPeakTension;
#pragma warning restore CS0414

        // COLD ALLOC: List<TetherInstance>[64] - active tether registry sized for the 50-cable SHINOBU target without gameplay resize - owner: TetherManager
        private readonly List<TetherInstance> _activeInstances = new List<TetherInstance>(MaxManagedTetherInstances);
        // COLD ALLOC: List<TetherInstance>[64] - prewarmed tether instances reused across attach/release cycles - owner: TetherManager
        private readonly List<TetherInstance> _pooledInstances = new List<TetherInstance>(MaxManagedTetherInstances);
        private MaterialPropertyBlock _renderPropertyBlock;
        private Material _runtimeRenderMaterial;
        private bool _ownsRuntimeMaterial;
        private Mesh _indirectTetherSegmentMesh;
        private GraphicsBuffer _indirectTetherArgsBuffer;
        private Mesh _indirectArgsMesh;
        private int _indirectArgsSegmentCount = -1;
        private IDataVault _dataVault;
        private bool _registeredFixedTick;
        private bool _registeredLateFrameTick;
        private bool _registeredSlowTick;
        private bool _registeredOriginShiftListener;
        private NativeArray<TetherManagerTelemetryEntry> _telemetryRing;
        private NativeArray<int> _telemetryHead;
        private VaultBufferHandle<TetherManagerTelemetryEntry> _telemetryRingHandle;
        private VaultBufferHandle<int> _telemetryHeadHandle;
        private uint _telemetryResolvedGeneration;
        private float _fixedStepClockSeconds;
        private int _fixedFrameIndex;
        private bool _telemetryDumped;
        private HectonQualityTier _cachedQualityTier = HectonQualityTier.Unknown;
        private HectonMapMagicVegetationBridge _cachedVegetationBridge;
        private HectonFluidEngine _cachedFluidEngine;
        private IWeatherService _cachedWeatherService;

        internal uint CurrentFixedFrameIndex => unchecked((uint)math.max(0, _fixedFrameIndex));
        internal HectonQualityTier CachedQualityTier => _cachedQualityTier;
        internal IDataVault CachedDataVault => _dataVault;
        internal HectonMapMagicVegetationBridge CachedVegetationBridge => _cachedVegetationBridge;
        internal HectonFluidEngine CachedFluidEngine => _cachedFluidEngine;
        internal IWeatherService CachedWeatherService => _cachedWeatherService;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 16)]
        private struct TetherManagerTelemetryEntry
        {
            public uint FrameIndex;
            public int ActiveTethers;
            public float PeakTension;
            public uint Flags;
        }

        private void Awake()
        {
            if (!VerletCableLayout.Validate())
            {
                Debug.LogError("[TETHER] Verlet DTO layout validation failed. Tether manager disabled.", this);
                enabled = false;
                return;
            }

            TetherSignals.EnsureInitialized();

            if (renderCamera == null)
            {
                Camera childCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
                if (childCamera != null)
                    renderCamera = childCamera;
            }

            _renderPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — procedural tether render binding payload — owner: TetherManager
            RefreshColdDependencyCache();
            PrewarmTetherPool(InitialPooledTetherInstances);
            EnsureTelemetry();
        }

        private void OnEnable()
        {
            RefreshColdDependencyCache();
            TryRegisterSlowTickable();
            TryRegisterFixedTickable();
            TryRegisterLateFrameTickable();

            if (!_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            }
        }

        private void OnDisable()
        {
            TryUnregisterFixedTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterSlowTickable();

            if (_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShiftListener = false;
            }

            for (int i = _activeInstances.Count - 1; i >= 0; i--)
                DetachTether(_activeInstances[i], false, true);
        }

        private void TryRegisterFixedTickable()
        {
            if (_registeredFixedTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = SystemDispatcher.GetFixedLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryUnregisterFixedTickable()
        {
            if (!_registeredFixedTick)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = SystemDispatcher.GetLateFrameLane(PriorityLayer.Player).Contains(this);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = false;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void OnDestroy()
        {
            TryUnregisterLateFrameTickable();
            TryUnregisterFixedTickable();
            TryUnregisterSlowTickable();

            if (_registeredOriginShiftListener)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShiftListener = false;
            }

            for (int i = 0; i < _pooledInstances.Count; i++)
            {
                if (_pooledInstances[i] != null)
                    _pooledInstances[i].DisposeRuntimeResources();
            }

            for (int i = 0; i < _activeInstances.Count; i++)
            {
                if (_activeInstances[i] != null)
                    _activeInstances[i].DisposeRuntimeResources();
            }

            if (_ownsRuntimeMaterial && _runtimeRenderMaterial != null)
            {
                Destroy(_runtimeRenderMaterial);
                _runtimeRenderMaterial = null;
                _ownsRuntimeMaterial = false;
            }

            ReleaseIndirectTetherRenderResources();

            _telemetryRing = default;
            _telemetryHead = default;
            _telemetryRingHandle = default;
            _telemetryHeadHandle = default;
            _telemetryResolvedGeneration = 0u;
            _dataVault = null;
            _cachedVegetationBridge = null;
            _cachedFluidEngine = null;
            _cachedWeatherService = null;
        }

        /// <summary>
        /// Creates or reuses a tow-cable runtime instance.
        /// </summary>
        public TetherInstance AttachTowCable(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody playerBody,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            if (owner == null || playerBody == null || payloadBody == null || payloadCollider == null)
                return null;

            TetherInstance instance = RentInstance();
            if (instance == null)
                return null;

            RefreshColdDependencyCache();
            instance.Configure(owner, playerMotor, playerBody, payloadBody, payloadCollider, initialDistance, _cachedQualityTier);
            if (!_activeInstances.Contains(instance))
            {
                if (_activeInstances.Count >= MaxManagedTetherInstances)
                {
                    ReturnInstanceToPool(instance);
                    return null;
                }

                _activeInstances.Add(instance);
            }

            _debugActiveTetherCount = _activeInstances.Count;
            return instance;
        }

        internal bool ExecuteFireRequest(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody playerBody,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            if (owner == null ||
                playerBody == null ||
                payloadBody == null ||
                payloadCollider == null)
            {
                return false;
            }

            if (owner.HasActiveTow)
                owner.ReleaseTow(false);

            TetherInstance instance = AttachTowCable(
                owner,
                playerMotor,
                playerBody,
                payloadBody,
                payloadCollider,
                initialDistance);
            if (instance == null)
                return false;

            return owner.CompleteSignalAttach(instance, payloadBody);
        }

        /// <summary>
        /// Releases an active tether and returns it to the local pool.
        /// </summary>
        public void DetachTether(TetherInstance instance, bool snapped, bool notifyOwner)
        {
            if (instance == null)
                return;

            int index = _activeInstances.IndexOf(instance);
            if (index >= 0)
            {
                int lastIndex = _activeInstances.Count - 1;
                _activeInstances[index] = _activeInstances[lastIndex];
                _activeInstances.RemoveAt(lastIndex);
            }

            HeavyTowWinch owner = notifyOwner ? instance.Owner : null;
            ReturnInstanceToPool(instance);

            if (notifyOwner && owner != null)
                owner.OnTetherDetached(instance, snapped);

            _debugActiveTetherCount = _activeInstances.Count;
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            float3 shiftOffsetF3 = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null || !instance.IsActive)
                    continue;

                instance.RebaseManagedRuntimeState(shiftOffset);
                if (instance.RebaseVerletRuntime(shiftOffsetF3))
                {
                    instance.CommitVisualRebaseUpload();
                    continue;
                }

                ref NativeArray<float3> visualPoints = ref instance.GetVisualSegmentPositionsRef();
                if (!visualPoints.IsCreated || visualPoints.Length == 0)
                    continue;

                for (int pointIndex = 0; pointIndex < visualPoints.Length; pointIndex++)
                {
                    visualPoints[pointIndex] = visualPoints[pointIndex] - shiftOffsetF3;
                }

                instance.CommitVisualRebaseUpload();
            }
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            _fixedStepClockSeconds = AdvanceFixedStepClock(_fixedStepClockSeconds, fixedDeltaTime);
            int fixedFrameIndex = AdvanceFixedFrameIndex();
            int activeCount = _activeInstances.Count;
            HectonQualityTier qualityTier = _cachedQualityTier;
            for (int i = activeCount - 1; i >= 0; i--)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null)
                {
                    _activeInstances.RemoveAt(i);
                    continue;
                }

                TetherLifecycleState state = instance.Simulate(fixedDeltaTime, _fixedStepClockSeconds, fixedFrameIndex, activeCount, maxVisualizedTethers, qualityTier);
                if (state == TetherLifecycleState.Alive)
                    continue;

                bool snapped = state == TetherLifecycleState.Snapped;
                DetachTether(instance, snapped, true);
                activeCount = _activeInstances.Count;
            }

            _debugActiveTetherCount = _activeInstances.Count;
            float peakTension = ResolvePeakTension();
            _debugPeakTension = peakTension;
            WriteBlackBoxSample(_debugActiveTetherCount, peakTension, 0u);
        }

        private static float AdvanceFixedStepClock(float currentSeconds, float fixedDeltaTime)
        {
            float safeCurrent = math.isfinite(currentSeconds) ? currentSeconds : 0f;
            if (fixedDeltaTime <= 0f || !math.isfinite(fixedDeltaTime))
                return safeCurrent;

            float next = safeCurrent + math.min(fixedDeltaTime, 0.05f);
            if (!math.isfinite(next))
                return 0f;

            return next >= TetherFixedClockWrapSeconds
                ? next - TetherFixedClockWrapSeconds
                : next;
        }

        private int AdvanceFixedFrameIndex()
        {
            if (_fixedFrameIndex == int.MaxValue)
            {
                _fixedFrameIndex = 0;
                return _fixedFrameIndex;
            }

            _fixedFrameIndex++;
            return _fixedFrameIndex;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            Material renderMaterial = ResolveRenderMaterial();
            if (renderMaterial == null || _activeInstances.Count == 0)
                return;

            _renderPropertyBlock.Clear();
            RenderParams renderParams = new RenderParams(renderMaterial)
            {
                matProps = _renderPropertyBlock,
                camera = renderCamera,
                layer = gameObject.layer,
                receiveShadows = false,
                shadowCastingMode = ShadowCastingMode.Off,
                motionVectorMode = MotionVectorGenerationMode.Camera,
                renderingLayerMask = 1u
            };

            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            HectonQualityTier qualityTier = _cachedQualityTier;
            int visualTier = ResolveTetherVisualTier(qualityTier);
            float crystalDensity = visualTier >= 3 ? 1f : (visualTier >= 2 ? 0.62f : 0f);
            float siltIntensity = visualTier >= 3 ? 0.55f : (visualTier >= 2 ? 0.28f : 0f);
            float visualClock = math.isfinite(_fixedStepClockSeconds) ? _fixedStepClockSeconds : 0f;
            bool hasFrustum = renderCamera != null;
            if (hasFrustum)
                GeometryUtility.CalculateFrustumPlanes(renderCamera, s_TetherFrustumPlanes);
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null || !instance.IsActive)
                    continue;

                instance.UpdateVisuals(deltaTime, qualityTier, hasFrustum ? s_TetherFrustumPlanes : null);
                if (!instance.IsVisualReady || instance.IsVisualCulled)
                    continue;

                int segmentCount = math.max(0, instance.VisualPointCount - 1);
                if (segmentCount <= 0)
                    continue;

                bool useIndirect = ShouldUseIndirectTetherRendering(qualityTier);
                if (!instance.UploadVisualDrawParams(
                        tetherRenderColor,
                        tetherStressColor,
                        tetherSegmentStressScale,
                        tetherRenderRadius,
                        instance.VisualPointCount,
                        useIndirect,
                        visualTier,
                        crystalDensity,
                        siltIntensity,
                        visualClock))
                    continue;

                _renderPropertyBlock.Clear();
                _renderPropertyBlock.SetBuffer(_TetherPositionsId, instance.VisualSegmentBuffer);
                _renderPropertyBlock.SetBuffer(_TetherSegmentTensionsId, instance.VisualSegmentTensionBuffer);
                _renderPropertyBlock.SetBuffer(_TetherDrawParamsId, instance.VisualDrawParamsBuffer);
                renderParams.worldBounds = instance.GetVisualBounds(tetherBoundsPadding);
                if (useIndirect && TryRenderIndirectTether(renderParams, segmentCount))
                    continue;

                if (useIndirect)
                {
                    if (!instance.UploadVisualDrawParams(
                            tetherRenderColor,
                            tetherStressColor,
                            tetherSegmentStressScale,
                            tetherRenderRadius,
                            instance.VisualPointCount,
                            false,
                            visualTier,
                            crystalDensity,
                            siltIntensity,
                            visualClock))
                        continue;

                    _renderPropertyBlock.SetBuffer(_TetherDrawParamsId, instance.VisualDrawParamsBuffer);
                }

                UnityEngine.Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, segmentCount * 6, 1);
            }
        }

        private static bool ShouldUseIndirectTetherRendering(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
        }

        private static int ResolveTetherVisualTier(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.High:
                    return 2;
                case HectonQualityTier.Ultra:
                    return 3;
                default:
                    return 0;
            }
        }

        private bool TryRenderIndirectTether(RenderParams renderParams, int segmentCount)
        {
            if (segmentCount <= 0)
                return false;

            Mesh mesh = ResolveIndirectTetherSegmentMesh();
            if (mesh == null)
                return false;

            EnsureIndirectTetherArgsBuffer();
            if (_indirectTetherArgsBuffer == null)
                return false;

            UploadIndirectTetherArgs(mesh, segmentCount);
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, _indirectTetherArgsBuffer, 1, 0);
            return true;
        }

        private Mesh ResolveIndirectTetherSegmentMesh()
        {
            if (_indirectTetherSegmentMesh != null)
                return _indirectTetherSegmentMesh;

            _indirectTetherSegmentMesh = new Mesh
            {
                name = "MESH_TetherIndirectSegment",
                hideFlags = HideFlags.DontSave
            }; // COLD ALLOC: Mesh[1] - canonical tether impostor segment mesh for indirect draw - owner: TetherManager
            _indirectTetherSegmentMesh.SetVertices(s_TetherIndirectSegmentVertices);
            _indirectTetherSegmentMesh.SetIndices(s_TetherIndirectSegmentIndices, MeshTopology.Triangles, 0, false);
            _indirectTetherSegmentMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            _indirectTetherSegmentMesh.UploadMeshData(false);
            return _indirectTetherSegmentMesh;
        }

        private void EnsureIndirectTetherArgsBuffer()
        {
            if (_indirectTetherArgsBuffer != null)
                return;

            _indirectTetherArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - tether RenderMeshIndirect draw args - owner: TetherManager
            _indirectArgsMesh = null;
            _indirectArgsSegmentCount = -1;
        }

        private void UploadIndirectTetherArgs(Mesh mesh, int segmentCount)
        {
            if (_indirectTetherArgsBuffer == null || mesh == null || segmentCount <= 0)
                return;

            if (_indirectArgsMesh == mesh && _indirectArgsSegmentCount == segmentCount)
                return;

            NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> argsWrite =
                _indirectTetherArgsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = mesh.GetIndexCount(0),
                instanceCount = (uint)segmentCount,
                startIndex = mesh.GetIndexStart(0),
                baseVertexIndex = (uint)Mathf.Max(0, mesh.GetBaseVertex(0)),
                startInstance = 0u
            };
            _indirectTetherArgsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            _indirectArgsMesh = mesh;
            _indirectArgsSegmentCount = segmentCount;
        }

        private void ReleaseIndirectTetherRenderResources()
        {
            if (_indirectTetherArgsBuffer != null)
            {
                _indirectTetherArgsBuffer.Release();
                _indirectTetherArgsBuffer = null;
            }

            if (_indirectTetherSegmentMesh != null)
            {
                Destroy(_indirectTetherSegmentMesh);
                _indirectTetherSegmentMesh = null;
            }

            _indirectArgsMesh = null;
            _indirectArgsSegmentCount = -1;
        }

        private TetherInstance RentInstance()
        {
            int pooledCount = _pooledInstances.Count;
            if (pooledCount > 0)
            {
                int lastIndex = pooledCount - 1;
                TetherInstance pooled = _pooledInstances[lastIndex];
                _pooledInstances.RemoveAt(lastIndex);
                if (pooled != null)
                {
                    pooled.InitializeManager(this);
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            return null;
        }

        private void PrewarmTetherPool(int requestedCount)
        {
            int targetCount = math.clamp(requestedCount, 0, MaxManagedTetherInstances);
            while (_pooledInstances.Count < targetCount)
            {
                TetherInstance instance = CreateColdPooledInstance();
                if (instance == null)
                    return;

                _pooledInstances.Add(instance);
            }
        }

        private TetherInstance CreateColdPooledInstance()
        {
            if (_activeInstances.Count + _pooledInstances.Count >= MaxManagedTetherInstances)
                return null;

            GameObject tetherObject = new GameObject("TetherInstance");
            tetherObject.transform.SetParent(transform, false);
            tetherObject.transform.localPosition = Vector3.zero;
            tetherObject.transform.localRotation = Quaternion.identity;
            tetherObject.transform.localScale = Vector3.one;
            // COLD ALLOC: TetherInstance[1] - pooled tether runtime child created only during manager prewarm - owner: TetherManager
            TetherInstance instance = tetherObject.AddComponent<TetherInstance>();
            instance.InitializeManager(this);
            tetherObject.SetActive(false);
            return instance;
        }

        private void ReturnInstanceToPool(TetherInstance instance)
        {
            if (instance == null)
                return;

            instance.Deactivate();
            if (_pooledInstances.Count >= MaxManagedTetherInstances || _pooledInstances.Contains(instance))
                return;

            _pooledInstances.Add(instance);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            RefreshColdDependencyCache();
        }

        private void RefreshColdDependencyCache()
        {
            _cachedQualityTier = SanitizeQualityTier(GlobalRegistry.ScalabilityTier);
            _cachedVegetationBridge = GlobalRegistry.MapMagicVegetation;
            _cachedFluidEngine = GlobalRegistry.Fluid;
            _cachedWeatherService = GlobalRegistry.Weather;
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        internal static HectonQualityTier SanitizeQualityTier(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.Low:
                case HectonQualityTier.Mx350:
                case HectonQualityTier.Mid:
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return tier;
                default:
                    return HectonQualityTier.Unknown;
            }
        }

        private Material ResolveRenderMaterial()
        {
            if (tetherRenderMaterial != null)
            {
                _ownsRuntimeMaterial = false;
                return tetherRenderMaterial;
            }

            if (_runtimeRenderMaterial != null)
                return _runtimeRenderMaterial;

            Shader shader = Shader.Find(RuntimeShaderName);
            if (shader == null)
                return null;

            // COLD ALLOC: Material[1] — runtime tether line-strip material fallback built from first-party shader — owner: TetherManager
            _runtimeRenderMaterial = new Material(shader)
            {
                name = "MAT_Runtime_TetherLineStrip",
                hideFlags = HideFlags.DontSave
            };
            _ownsRuntimeMaterial = true;
            return _runtimeRenderMaterial;
        }

        internal float ResolveTowSpringStiffness(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return SanitizeProfileScalar(towCableProfile.SpringStiffness, 0f, 0f);

            return owner != null ? owner.ResolveTowSpringStiffness() : 0f;
        }

        internal float ResolveTowOverDampingMultiplier(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return SanitizeProfileScalar(towCableProfile.OverDampingMultiplier, 1f, 1f);

            return owner != null ? owner.ResolveTowOverDampingMultiplier() : 1f;
        }

        internal float ResolveTowSnapTensionThreshold(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return SanitizeProfileScalar(towCableProfile.SnapTensionThreshold, 1f, 1f);

            return owner != null ? owner.ResolveSnapTensionThreshold() : 1f;
        }

        private static float SanitizeProfileScalar(float value, float minimum, float fallback)
        {
            return math.isfinite(value) ? math.max(minimum, value) : fallback;
        }

        private float ResolvePeakTension()
        {
            float peak = 0f;
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null || !instance.IsActive)
                    continue;

                peak = math.max(peak, instance.CurrentPeakTension);
            }

            return peak;
        }

        private void EnsureTelemetry()
        {
            if (_dataVault == null)
                return;

            if (_telemetryRing.IsCreated &&
                _telemetryHead.IsCreated &&
                _telemetryResolvedGeneration == _dataVault.VaultGenerationID)
            {
                return;
            }

            bool resetHead = !_telemetryHeadHandle.IsCreated;
            if (!_telemetryRingHandle.IsCreated || _telemetryRingHandle.Length < TetherBlackBoxCapacity)
            {
                _telemetryRingHandle = _dataVault.GetBufferHandle<TetherManagerTelemetryEntry>(
                    BufferID.TetherManagerBlackBox,
                    TetherBlackBoxCapacity,
                    SystemID.Physics,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_telemetryHeadHandle.IsCreated || _telemetryHeadHandle.Length < 1)
            {
                _telemetryHeadHandle = _dataVault.GetBufferHandle<int>(
                    BufferID.TetherManagerBlackBoxHead,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.ClearMemory);
            }

            _telemetryRing = _telemetryRingHandle.Resolve(_dataVault);
            _telemetryHead = _telemetryHeadHandle.Resolve(_dataVault);
            if (!_telemetryRing.IsCreated || !_telemetryHead.IsCreated)
            {
                _telemetryRing = default;
                _telemetryHead = default;
                _telemetryRingHandle = default;
                _telemetryHeadHandle = default;
                _telemetryResolvedGeneration = 0u;
                return;
            }

            if (resetHead)
                _telemetryHead[0] = 0;
            _telemetryResolvedGeneration = _dataVault.VaultGenerationID;
            _telemetryDumped = false;
        }

        private void WriteBlackBoxSample(int activeTethers, float peakTension, uint flags)
        {
            EnsureTelemetry();

            if (!_telemetryRing.IsCreated || !_telemetryHead.IsCreated)
                return;

            if (!math.isfinite(peakTension))
            {
                peakTension = 0f;
                flags |= 1u;
            }

            int head = _telemetryHead[0];
            if (head < 0 || head >= _telemetryRing.Length)
                head = 0;

            _telemetryRing[head] = new TetherManagerTelemetryEntry
            {
                FrameIndex = CurrentFixedFrameIndex,
                ActiveTethers = activeTethers,
                PeakTension = peakTension,
                Flags = flags
            };
            head++;
            if (head >= _telemetryRing.Length)
                head = 0;

            _telemetryHead[0] = head;

            if ((flags & 1u) != 0u)
                DumpBlackBoxOnce();
        }

        private void DumpBlackBoxOnce()
        {
            if (_telemetryDumped || !_telemetryRing.IsCreated || !_telemetryHead.IsCreated)
                return;

            _telemetryDumped = true;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string legacyDumpPath = Path.Combine(
                    projectRoot,
                    TetherBlackBoxDumpRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string h8DumpPath = Path.Combine(
                    projectRoot,
                    TetherBlackBoxH8DumpRelativePath.Replace('/', Path.DirectorySeparatorChar));
                TetherBlackBoxDumpWriter.WritePrimaryAndLegacy(
                    h8DumpPath,
                    legacyDumpPath,
                    TetherBlackBoxMagic,
                    _telemetryRing,
                    _telemetryHead[0],
                    1u);
            }
            catch
            {
                // Fault-path dump must not cascade into physics failure.
            }
        }
    }
}

using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using System.IO;
using System.Collections.Generic;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Physics
{
    /// <summary>
    /// Player-owned tether runtime host.
    /// Physics executes in <see cref="FixedTick(float)"/> and visuals render in <see cref="LateFrameTick"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Physics/Tether Manager")]
    public sealed class TetherManager : MonoBehaviour, IFixedTickable, ILateFrameTickable, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const string RuntimeShaderName = "Hecton8/Physics/TetherLineStrip";
#if UNITY_EDITOR
        private const string RuntimeShaderPath = "Assets/_Project/Art/Shaders/Hecton_TetherLineStrip.shader";
#endif
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

        [Tooltip("Authored tether shader reference used when no explicit material is provided. Required for release player fallback material creation.")]
        [SerializeField] private Shader tetherRenderShader;

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
        private ICablePhysics132Service _cablePhysics132Service;
        private bool _registeredFixedTick;
        private bool _registeredLateFrameTick;
        private bool _registeredSlowTick;
        private bool _registeredOriginShiftListener;
        private bool _hotSwapRegistered;
        private VaultGenerationHandle<TetherManagerTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryHeadHandle;
        private JobHandle _shinobu143AupMockHandle;
        private bool _shinobu143AupMockScheduled;
        private long _shinobu143AupMockScheduleTicks;
        private float _shinobu143LastMockElapsedUs;
        private JobHandle _shinobu132CableMockHandle;
        private bool _shinobu132CableMockScheduled;
        private long _shinobu132CableMockScheduleTicks;
        private float _shinobu132LastMockElapsedUs;
        private bool _shinobu132CableBootstrapRequested;
        private JobHandle _shinobu328TensionMockHandle;
        private bool _shinobu328TensionMockScheduled;
        private long _shinobu328TensionMockScheduleTicks;
        private float _shinobu328LastMockElapsedUs;
        private bool _shinobu328TensionBootstrapRequested;
        private int _shinobu328LastActiveTetherCount;
        private int _shinobu328LastNodesPerTether;
        private uint _shinobu328LastFrameIndex;
        private float _fixedStepClockSeconds;
        private int _fixedFrameIndex;
        private bool _telemetryDumped;
        private HectonQualityTier _cachedQualityTier = HectonQualityTier.Unknown;
        private float _cachedQualityWeight01 = 1f;
        private HectonMapMagicVegetationBridge _cachedVegetationBridge;
        private HectonFluidEngine _cachedFluidEngine;
        private HectonVoxelEngine _cachedVoxelEngineRuntime;
        private IVoxelSonarSdfReadModel _cachedVoxelSdfReadModel;
        private IWeatherService _cachedWeatherService;
        private Camera _cachedPlayerCamera;
        private HectonPlayerMovement _cachedPlayerMovement;

        internal uint CurrentFixedFrameIndex => unchecked((uint)math.max(0, _fixedFrameIndex));
        internal HectonQualityTier CachedQualityTier => _cachedQualityTier;
        internal IDataVault CachedDataVault => _dataVault;
        internal HectonMapMagicVegetationBridge CachedVegetationBridge => _cachedVegetationBridge;
        internal HectonFluidEngine CachedFluidEngine => _cachedFluidEngine;
        internal HectonVoxelEngine CachedVoxelEngineRuntime => _cachedVoxelEngineRuntime;
        internal IVoxelSonarSdfReadModel CachedVoxelSdfReadModel => _cachedVoxelSdfReadModel;
        internal IWeatherService CachedWeatherService => _cachedWeatherService;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct TetherManagerTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint FrameIndex;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public int ActiveTethers;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public float PeakTension;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public uint Flags;
            [System.Runtime.InteropServices.FieldOffset(16)]
            private ulong _pad0;
            [System.Runtime.InteropServices.FieldOffset(24)]
            private ulong _pad1;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private ulong _pad2;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private ulong _pad3;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private ulong _pad4;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private ulong _pad5;
        }

        private void Awake()
        {
            if (!VerletCableLayout.Validate())
            {
                Hecton8.Core.H8Debug.LogError("[TETHER] Verlet DTO layout validation failed. Tether manager disabled.", this);
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
            EnsureShinobu132CableBootstrap();
            EnsureShinobu143AupBootstrap();
            EnsureShinobu328TensionBootstrap();
        }

        private void OnEnable()
        {
            RefreshColdDependencyCache();
            TryRegisterHotSwapListener();
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
            CompleteShinobu328TensionMockForTeardown();
            CompleteShinobu143AupMockForTeardown();
            CompleteShinobu132CableMockForTeardown();
            TryUnregisterFixedTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();

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

            _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
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

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
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

            if (GlobalRegistry.Dispatcher == null)
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
            CompleteShinobu328TensionMockForTeardown();
            CompleteShinobu143AupMockForTeardown();
            CompleteShinobu132CableMockForTeardown();
            TryUnregisterLateFrameTickable();
            TryUnregisterFixedTickable();
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();

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

            _telemetryRingHandle = default;
            _telemetryHeadHandle = default;
            _dataVault = null;
            _cachedVegetationBridge = null;
            _cachedFluidEngine = null;
            _cachedVoxelEngineRuntime = null;
            _cachedVoxelSdfReadModel = null;
            _cachedWeatherService = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || currentService == null || !isActiveAndEnabled)
                return;

            TryUnregisterFixedTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterSlowTickable();
            TryRegisterSlowTickable();
            TryRegisterFixedTickable();
            TryRegisterLateFrameTickable();
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

        /// <summary>
        /// Creates or reuses a tow-cable runtime instance.
        /// </summary>
        public TetherInstance AttachTowCable(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody legacyAnchorBody,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            _ = legacyAnchorBody;
            return AttachTowCable(owner, playerMotor, payloadBody, payloadCollider, initialDistance);
        }

        /// <summary>
        /// Creates or reuses a tow-cable runtime instance anchored to the player motor route.
        /// </summary>
        public TetherInstance AttachTowCable(
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            if (owner == null || playerMotor == null || payloadBody == null || payloadCollider == null)
                return null;

            TetherInstance instance = RentInstance();
            if (instance == null)
                return null;

            RefreshColdDependencyCache();
            instance.Configure(owner, playerMotor, payloadBody, payloadCollider, initialDistance, _cachedQualityTier);
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
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            if (owner == null ||
                playerMotor == null ||
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

                if (instance.RebaseVisualStagingRuntime(shiftOffsetF3))
                    instance.CommitVisualRebaseUpload();
            }
        }

        /// <inheritdoc />
        public void FixedTick(float fixedDeltaTime)
        {
            _fixedStepClockSeconds = AdvanceFixedStepClock(_fixedStepClockSeconds, fixedDeltaTime);
            int fixedFrameIndex = AdvanceFixedFrameIndex();
            ScheduleShinobu132CableMock(fixedDeltaTime, unchecked((uint)fixedFrameIndex));
            ScheduleShinobu143AupMock(fixedDeltaTime, unchecked((uint)fixedFrameIndex));
            ScheduleShinobu328TensionMock(fixedDeltaTime, unchecked((uint)fixedFrameIndex));
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
            float qualityWeight01 = _cachedQualityWeight01;
            int visualTier = ResolveTetherVisualTier(qualityWeight01);
            float visualOverkill01 = Smooth01(math.saturate((qualityWeight01 - 0.55f) * 2.2222223f));
            float crystalDensity = math.lerp(0f, 1f, visualOverkill01);
            float siltIntensity = math.lerp(0f, 0.55f, visualOverkill01);
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

                bool useIndirect = ShouldUseIndirectTetherRendering(qualityWeight01);
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

        private static bool ShouldUseIndirectTetherRendering(float qualityWeight01)
        {
            return Smooth01(qualityWeight01) >= 0.62f;
        }

        private static int ResolveTetherVisualTier(float qualityWeight01)
        {
            float quality = Smooth01(qualityWeight01);
            return math.clamp((int)math.round(quality * 3f), 0, 3);
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
            try
            {
                argsWrite[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = mesh.GetIndexCount(0),
                    instanceCount = (uint)segmentCount,
                    startIndex = mesh.GetIndexStart(0),
                    baseVertexIndex = (uint)Mathf.Max(0, mesh.GetBaseVertex(0)),
                    startInstance = 0u
                };
            }
            finally
            {
                _indirectTetherArgsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            }
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
            EnsureShinobu132CableBootstrap();
            EnsureShinobu143AupBootstrap();
            EnsureShinobu328TensionBootstrap();
        }

        private void RefreshColdDependencyCache()
        {
            _cachedQualityWeight01 = ResolveGlobalQualityWeight01();
            _cachedQualityTier = ResolveQualityTierFromGlobalWeight(_cachedQualityWeight01);
            _cachedVegetationBridge = GlobalRegistry.MapMagicVegetation;
            _cachedFluidEngine = GlobalRegistry.Fluid;
            _cachedVoxelEngineRuntime = GlobalRegistry.VoxelEngine;
            _cachedVoxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
            _cachedWeatherService = GlobalRegistry.Weather;
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            if (_cablePhysics132Service == null)
                _cablePhysics132Service = GlobalRegistry.CablePhysics132;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                if (playerContext.PlayerCamera != null)
                    _cachedPlayerCamera = playerContext.PlayerCamera;
                if (playerContext.PlayerMovement != null)
                    _cachedPlayerMovement = playerContext.PlayerMovement;
            }
        }

        private void EnsureShinobu143AupBootstrap()
        {
            if (_dataVault == null)
                return;

            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(qualityWeight))
                qualityWeight = 1f;

            TetherAupVaultBootstrap.EnsureMockBuffers(
                _dataVault,
                math.saturate(qualityWeight),
                CurrentFixedFrameIndex);
        }

        private void EnsureShinobu132CableBootstrap()
        {
            ICablePhysics132Service cableService = _cablePhysics132Service;
            if (_dataVault == null || cableService == null)
                return;
            if (_shinobu132CableBootstrapRequested &&
                cableService.TryHasMockBuffers(_dataVault))
            {
                return;
            }

            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(qualityWeight))
                qualityWeight = 1f;

            cableService.EnsureMockBuffers(
                _dataVault,
                math.saturate(qualityWeight),
                CurrentFixedFrameIndex);
            _shinobu132CableBootstrapRequested = true;
        }

        private void ScheduleShinobu132CableMock(float fixedDeltaTime, uint frameIndex)
        {
            ICablePhysics132Service cableService = _cablePhysics132Service;
            if (_dataVault == null || cableService == null)
                return;

            TryFinalizeShinobu132CableMock();
            if (_shinobu132CableMockScheduled)
                return;

            if (!ResolveShinobu132CameraContext(out Vector3 cameraPosition, out double3 cameraAup))
                return;

            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(qualityWeight))
                qualityWeight = 1f;
            float safeDelta = math.isfinite(fixedDeltaTime) && fixedDeltaTime > 0f
                ? math.min(fixedDeltaTime, 0.05f)
                : 0.02f;
            float3 gravity = new float3(0f, -HectonPhysicsContract.GravityMetersPerSecondSquaredConst * 0.16f, 0f);
            float3 abyssalFlow = float3.zero;
            if (_cachedFluidEngine != null &&
                _cachedFluidEngine.TrySampleModAbyssalFlow(cameraPosition, out float3 sampledAbyssalFlow) &&
                math.all(math.isfinite(sampledAbyssalFlow)))
            {
                abyssalFlow = sampledAbyssalFlow;
            }

            _shinobu132CableMockScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!cableService.TryScheduleMockFromVault(
                    _dataVault,
                    frameIndex,
                    safeDelta,
                    gravity,
                    abyssalFlow,
                    cameraAup,
                    qualityWeight,
                    _shinobu132LastMockElapsedUs,
                    default,
                    out _shinobu132CableMockHandle))
            {
                return;
            }

            _shinobu132CableMockScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.Physics, _shinobu132CableMockHandle);
        }

        private void TryFinalizeShinobu132CableMock()
        {
            if (!_shinobu132CableMockScheduled)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _shinobu132CableMockHandle))
                return;

            FinishShinobu132CableMockCompletion();
        }

        private void CompleteShinobu132CableMockForTeardown()
        {
            if (!_shinobu132CableMockScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _shinobu132CableMockHandle, forceComplete: true))
                return;

            FinishShinobu132CableMockCompletion();
        }

        private void FinishShinobu132CableMockCompletion()
        {
            _shinobu132CableMockScheduled = false;
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _shinobu132CableMockScheduleTicks;
            _shinobu132LastMockElapsedUs = (float)math.max(
                0.0d,
                elapsedTicks * 1000000.0d / System.Diagnostics.Stopwatch.Frequency);

            _cablePhysics132Service?.TryDumpLatestFault(_dataVault);
        }

        private void EnsureShinobu328TensionBootstrap()
        {
            if (_dataVault == null)
                return;
            if (_shinobu328TensionBootstrapRequested &&
                HarpoonTensionSolver328.TryHasMockBuffers(_dataVault))
            {
                return;
            }

            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(qualityWeight))
                qualityWeight = 1f;

            HarpoonTensionSolver328.EnsureSignalLanes();
            HarpoonTensionSolver328.EnsureMockBuffers(
                _dataVault,
                math.saturate(qualityWeight),
                CurrentFixedFrameIndex);
            _shinobu328TensionBootstrapRequested = HarpoonTensionSolver328.TryHasMockBuffers(_dataVault);
        }

        private void ScheduleShinobu328TensionMock(float fixedDeltaTime, uint frameIndex)
        {
            if (_dataVault == null)
                return;

            TryFinalizeShinobu328TensionMock();
            if (_shinobu328TensionMockScheduled)
                return;

            EnsureShinobu328TensionBootstrap();
            Vector3 cameraPosition;
            if (!ResolveShinobu132CameraContext(out cameraPosition, out double3 cameraAup))
                return;

            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(qualityWeight))
                qualityWeight = 1f;
            float safeDelta = math.isfinite(fixedDeltaTime) && fixedDeltaTime > 0f
                ? math.min(fixedDeltaTime, 0.05f)
                : 0.02f;

            _shinobu328TensionMockScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!HarpoonTensionSolver328.TryScheduleMockFromVault(
                    _dataVault,
                    frameIndex,
                    safeDelta,
                    cameraAup,
                    math.saturate(qualityWeight),
                    _shinobu328LastMockElapsedUs,
                    default,
                    out HarpoonTensionSchedule328 schedule))
            {
                return;
            }

            _shinobu328TensionMockHandle = schedule.Handle;
            _shinobu328LastActiveTetherCount = schedule.ActiveTetherCount;
            _shinobu328LastNodesPerTether = HarpoonTensionSolver328Constants.MockNodesPerTether;
            _shinobu328LastFrameIndex = frameIndex;
            _shinobu328TensionMockScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.Physics, _shinobu328TensionMockHandle);
        }

        private void TryFinalizeShinobu328TensionMock()
        {
            if (!_shinobu328TensionMockScheduled)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _shinobu328TensionMockHandle))
                return;

            FinishShinobu328TensionMockCompletion();
        }

        private void CompleteShinobu328TensionMockForTeardown()
        {
            if (!_shinobu328TensionMockScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _shinobu328TensionMockHandle, forceComplete: true))
                return;

            FinishShinobu328TensionMockCompletion();
        }

        private void FinishShinobu328TensionMockCompletion()
        {
            _shinobu328TensionMockScheduled = false;
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _shinobu328TensionMockScheduleTicks;
            _shinobu328LastMockElapsedUs = (float)math.max(
                0.0d,
                elapsedTicks * 1000000.0d / System.Diagnostics.Stopwatch.Frequency);

            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(qualityWeight))
                qualityWeight = 1f;

            HarpoonTensionSolver328.TryPublishCompletedSignalsFromVault(
                _dataVault,
                _shinobu328LastActiveTetherCount,
                _shinobu328LastNodesPerTether,
                _shinobu328LastFrameIndex,
                math.saturate(qualityWeight),
                1);
            HarpoonTensionSolver328.TryDumpTelemetryIfFault(_dataVault, string.Empty, 1);
        }

        private bool ResolveShinobu132CameraContext(out Vector3 cameraPosition, out double3 cameraAup)
        {
            Camera sourceCamera = renderCamera != null ? renderCamera : _cachedPlayerCamera;
            Transform sourceTransform = sourceCamera != null ? sourceCamera.transform : transform;
            cameraPosition = sourceTransform != null ? sourceTransform.position : Vector3.zero;
            if (!math.all(math.isfinite(new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z))))
            {
                cameraAup = RuntimeOriginRoute.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
                cameraPosition = Vector3.zero;
                return math.all(math.isfinite(cameraAup));
            }

            HectonPlayerMovement movement = _cachedPlayerMovement;
            if (movement != null)
            {
                AbsoluteUniversePosition bodyAup = movement.CurrentAup;
                float3 bodyRuntime = bodyAup.ToRuntimeFloat3();
                float3 cameraRuntime = new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z);
                float3 cameraOffset = cameraRuntime - bodyRuntime;
                cameraAup = bodyAup.ToAbsoluteDouble3() + new double3(cameraOffset.x, cameraOffset.y, cameraOffset.z);
                return math.all(math.isfinite(cameraAup));
            }

            return TryResolveRuntimeAup(cameraPosition, out cameraAup);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!positionAup.IsFinite())
                return false;

            absoluteAup = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
        }

        private void ScheduleShinobu143AupMock(float fixedDeltaTime, uint frameIndex)
        {
            if (_dataVault == null)
                return;

            TryFinalizeShinobu143AupMock();
            if (_shinobu143AupMockScheduled)
                return;

            EnsureShinobu143AupBootstrap();
            if (!TryResolveShinobu143AupBuffers(
                    out NativeArray<TetherNodeDTO> nodes,
                    out NativeArray<TetherConstraintDTO> constraints,
                    out NativeArray<TetherEndpointAupDTO> endpoints,
                    out NativeArray<float> segmentTensions,
                    out NativeArray<float> solverStats,
                    out NativeArray<TetherForcePacketDTO> forcePackets,
                    out NativeArray<TetherAupTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryHead,
                    out NativeArray<double3> pinnedAups,
                    out NativeArray<byte> pinnedMask))
            {
                return;
            }

            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(qualityWeight))
                qualityWeight = 1f;
            float safeDelta = math.isfinite(fixedDeltaTime) && fixedDeltaTime > 0f
                ? math.min(fixedDeltaTime, 0.05f)
                : 0.02f;
            float damping = math.lerp(0.965f, 0.992f, Smooth01(math.saturate(qualityWeight)));
            float3 gravity = new float3(0f, -HectonPhysicsContract.GravityMetersPerSecondSquaredConst * 0.18f, 0f);
            float tensionScale = ResolveTowSpringStiffness(null);
            if (!math.isfinite(tensionScale) || tensionScale <= 0f)
                tensionScale = 24f;
            _shinobu143AupMockScheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            _shinobu143AupMockHandle = TetherAupSolverScheduler.ScheduleMock(
                nodes,
                constraints,
                endpoints,
                segmentTensions,
                solverStats,
                forcePackets,
                telemetryRing,
                telemetryHead,
                pinnedAups,
                pinnedMask,
                frameIndex,
                0x5348494Eu,
                safeDelta,
                gravity,
                float3.zero,
                damping,
                math.lerp(0.18f, 1.1f, math.saturate(qualityWeight)),
                math.max(0f, tensionScale),
                _shinobu143LastMockElapsedUs,
                qualityWeight,
                default);
            _shinobu143AupMockScheduled = true;
            H8Memory.RegisterActiveJob(SystemID.Physics, _shinobu143AupMockHandle);
        }

        private void TryFinalizeShinobu143AupMock()
        {
            if (!_shinobu143AupMockScheduled)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _shinobu143AupMockHandle))
                return;

            FinishShinobu143AupMockCompletion();
        }

        private void CompleteShinobu143AupMockForTeardown()
        {
            if (!_shinobu143AupMockScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _shinobu143AupMockHandle, forceComplete: true))
                return;

            FinishShinobu143AupMockCompletion();
        }

        private void FinishShinobu143AupMockCompletion()
        {
            _shinobu143AupMockScheduled = false;
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _shinobu143AupMockScheduleTicks;
            _shinobu143LastMockElapsedUs = (float)math.max(
                0.0d,
                elapsedTicks * 1000000.0d / System.Diagnostics.Stopwatch.Frequency);

            if (TetherAupRuntimeIntrospection.TrySampleLatestTelemetry(_dataVault, out TetherAupTelemetryEntry telemetry) &&
                (telemetry.Flags & (TetherNodeRuntimeFlags.NonFiniteRecovered | TetherNodeRuntimeFlags.ConstraintFault)) != 0u)
            {
                TetherAupRuntimeIntrospection.TryDumpCableSurgeon(_dataVault, telemetry.Flags);
            }
        }

        private bool TryResolveShinobu143AupBuffers(
            out NativeArray<TetherNodeDTO> nodes,
            out NativeArray<TetherConstraintDTO> constraints,
            out NativeArray<TetherEndpointAupDTO> endpoints,
            out NativeArray<float> segmentTensions,
            out NativeArray<float> solverStats,
            out NativeArray<TetherForcePacketDTO> forcePackets,
            out NativeArray<TetherAupTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryHead,
            out NativeArray<double3> pinnedAups,
            out NativeArray<byte> pinnedMask)
        {
            nodes = default;
            constraints = default;
            endpoints = default;
            segmentTensions = default;
            solverStats = default;
            forcePackets = default;
            telemetryRing = default;
            telemetryHead = default;
            pinnedAups = default;
            pinnedMask = default;
            if (_dataVault == null)
                return false;

            if (!TryOpenExistingPhysicsVaultBuffer(
                    _dataVault,
                    BufferID.Shinobu143TetherAupNodes,
                    TetherAupRuntimeConstants.MockNodeCapacity,
                    out nodes) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    _dataVault,
                    BufferID.Shinobu143TetherConstraints,
                    TetherAupRuntimeConstants.MockConstraintCapacity,
                    out constraints) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    _dataVault,
                    BufferID.Shinobu143TetherEndpoints,
                    TetherAupRuntimeConstants.MockTetherCount,
                    out endpoints) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    _dataVault,
                    BufferID.Shinobu143TetherSegmentTensions,
                    TetherAupRuntimeConstants.MockConstraintCapacity,
                    out segmentTensions) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    _dataVault,
                    BufferID.Shinobu143TetherSolverStats,
                    TetherAupRuntimeConstants.SolverStatsCapacity,
                    out solverStats) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    _dataVault,
                    BufferID.Shinobu143TetherForcePackets,
                    TetherAupRuntimeConstants.MockForcePacketCapacity,
                    out forcePackets) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    _dataVault,
                    BufferID.Shinobu143TetherTelemetryRing,
                    TetherAupRuntimeConstants.TelemetryCapacity,
                    out telemetryRing) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    _dataVault,
                    BufferID.Shinobu143TetherTelemetryHead,
                    1,
                    out telemetryHead) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    _dataVault,
                    BufferID.Shinobu143TetherPinnedAups,
                    TetherAupRuntimeConstants.MockNodeCapacity,
                    out pinnedAups) ||
                !TryOpenExistingPhysicsVaultBuffer(
                    _dataVault,
                    BufferID.Shinobu143TetherPinnedMask,
                    TetherAupRuntimeConstants.MockNodeCapacity,
                    out pinnedMask))
            {
                return false;
            }

            return nodes.IsCreated &&
                constraints.IsCreated &&
                endpoints.IsCreated &&
                segmentTensions.IsCreated &&
                solverStats.IsCreated &&
                forcePackets.IsCreated &&
                telemetryRing.IsCreated &&
                telemetryHead.IsCreated &&
                pinnedAups.IsCreated &&
                pinnedMask.IsCreated;
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

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(qualityWeight))
                qualityWeight = 1f;

            return math.saturate(qualityWeight);
        }

        private static HectonQualityTier ResolveQualityTierFromGlobalWeight(float qualityWeight)
        {
            qualityWeight = math.saturate(qualityWeight);
            if (qualityWeight < 0.18f)
                return HectonQualityTier.Low;
            if (qualityWeight < 0.36f)
                return HectonQualityTier.Mx350;
            if (qualityWeight < 0.62f)
                return HectonQualityTier.Mid;
            if (qualityWeight < 0.86f)
                return HectonQualityTier.High;

            return HectonQualityTier.Ultra;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
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

            Shader shader = tetherRenderShader;
#if UNITY_EDITOR
            if (shader == null)
            {
                shader = AssetDatabase.LoadAssetAtPath<Shader>(RuntimeShaderPath);
                tetherRenderShader = shader;
            }
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (shader == null)
                shader = Shader.Find(RuntimeShaderName);
#endif
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (tetherRenderShader == null)
                tetherRenderShader = AssetDatabase.LoadAssetAtPath<Shader>(RuntimeShaderPath);
        }
#endif

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

        private bool OpenOrAcquirePhysicsVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (TryOpenPhysicsVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                {
                    buffer = default;
                    return false;
                }

                return TryOpenPhysicsVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Physics,
                options);
            return TryOpenPhysicsVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenExistingPhysicsVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
            {
                return false;
            }

            return TryOpenPhysicsVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenPhysicsVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsPhysicsVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsPhysicsVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.Physics &&
                   handle.Generation != 0u;
        }

        private void EnsureTelemetry()
        {
            NativeArray<TetherManagerTelemetryEntry> telemetryRing;
            NativeArray<int> telemetryHead;
            TryResolveTelemetry(out telemetryRing, out telemetryHead);
        }

        private bool TryResolveTelemetry(
            out NativeArray<TetherManagerTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryHead)
        {
            telemetryRing = default;
            telemetryHead = default;
            if (_dataVault == null)
                return false;

            uint previousRingGeneration = _telemetryRingHandle.Generation;
            uint previousHeadGeneration = _telemetryHeadHandle.Generation;
            bool resetRing = !IsPhysicsVaultHandle(in _telemetryRingHandle, BufferID.TetherManagerBlackBox);
            bool resetHead = !IsPhysicsVaultHandle(in _telemetryHeadHandle, BufferID.TetherManagerBlackBoxHead);
            if (!OpenOrAcquirePhysicsVaultBuffer(
                    ref _telemetryRingHandle,
                    BufferID.TetherManagerBlackBox,
                    TetherBlackBoxCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out telemetryRing) ||
                !OpenOrAcquirePhysicsVaultBuffer(
                    ref _telemetryHeadHandle,
                    BufferID.TetherManagerBlackBoxHead,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    out telemetryHead))
            {
                _telemetryRingHandle = default;
                _telemetryHeadHandle = default;
                return false;
            }

            bool generationChanged =
                (previousRingGeneration != 0u && previousRingGeneration != _telemetryRingHandle.Generation) ||
                (previousHeadGeneration != 0u && previousHeadGeneration != _telemetryHeadHandle.Generation);
            if (resetRing || generationChanged)
            {
                for (int i = 0; i < telemetryRing.Length; i++)
                    telemetryRing[i] = default;
            }
            if (resetHead || generationChanged)
                telemetryHead[0] = 0;
            if (generationChanged)
                _telemetryDumped = false;
            return true;
        }

        private void WriteBlackBoxSample(int activeTethers, float peakTension, uint flags)
        {
            if (!TryResolveTelemetry(
                    out NativeArray<TetherManagerTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryHead))
            {
                return;
            }

            if (!math.isfinite(peakTension))
            {
                peakTension = 0f;
                flags |= 1u;
            }

            int head = telemetryHead[0];
            if (head < 0 || head >= telemetryRing.Length)
                head = 0;

            telemetryRing[head] = new TetherManagerTelemetryEntry
            {
                FrameIndex = CurrentFixedFrameIndex,
                ActiveTethers = activeTethers,
                PeakTension = peakTension,
                Flags = flags
            };
            head++;
            if (head >= telemetryRing.Length)
                head = 0;

            telemetryHead[0] = head;

            if ((flags & 1u) != 0u)
                DumpBlackBoxOnce();
        }

        private void DumpBlackBoxOnce()
        {
            if (_telemetryDumped ||
                !TryResolveTelemetry(
                    out NativeArray<TetherManagerTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryHead))
            {
                return;
            }

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
                    telemetryRing,
                    telemetryHead[0],
                    1u);
            }
            catch
            {
                // Fault-path dump must not cascade into physics failure.
            }
        }
    }
}

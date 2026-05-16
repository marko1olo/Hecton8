using Hecton8.Core;
using Hecton8.Core.Memory;
using System.IO;
using System.Collections.Generic;
using Hecton8.Gameplay;
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
    public sealed class TetherManager : MonoBehaviour, IFixedTickable, ILateFrameTickable, IOriginShiftListener
    {
        private const string RuntimeShaderName = "Hecton8/Physics/TetherLineStrip";
        private static readonly int _TetherPositionsId = Shader.PropertyToID("_TetherPositions");
        private static readonly int _TetherSegmentTensionsId = Shader.PropertyToID("_TetherSegmentTensions");
        private static readonly int _TetherColorId = Shader.PropertyToID("_TetherColor");
        private static readonly int _TetherStressColorId = Shader.PropertyToID("_TetherStressColor");
        private static readonly int _TetherStress01Id = Shader.PropertyToID("_TetherStress01");
        private static readonly int _TetherSegmentStressScaleId = Shader.PropertyToID("_TetherSegmentStressScale");
        private static readonly int _TetherPointCountId = Shader.PropertyToID("_TetherPointCount");
        private static readonly int _TetherRadiusId = Shader.PropertyToID("_TetherRadius");
        private static readonly int _TetherIndirectModeId = Shader.PropertyToID("_TetherIndirectMode");
        private const int TetherBlackBoxCapacity = 300;
        private const string TetherBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_VERLET_TOW_WINCH.bin";
        private const uint TetherBlackBoxMagic = 0x54455448u;

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

        // COLD ALLOC: List<TetherInstance>[4] — active tether registry owned by the player-local tether manager — owner: TetherManager
        private readonly List<TetherInstance> _activeInstances = new List<TetherInstance>(4);
        // COLD ALLOC: List<TetherInstance>[4] — pooled tether instances reused across attach/release cycles — owner: TetherManager
        private readonly List<TetherInstance> _pooledInstances = new List<TetherInstance>(4);
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
        private bool _registeredOriginShiftListener;
        private NativeArray<TetherManagerTelemetryEntry> _telemetryRing;
        private NativeArray<int> _telemetryHead;
        private bool _telemetryDumped;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1, Size = 16)]
        private struct TetherManagerTelemetryEntry
        {
            public uint FrameIndex;
            public int ActiveTethers;
            public float PeakTension;
            public uint Flags;
        }

        private void Awake()
        {
            TetherSignals.EnsureInitialized();

            if (renderCamera == null)
            {
                Camera childCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(transform);
                if (childCamera != null)
                    renderCamera = childCamera;
            }

            _renderPropertyBlock ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — procedural tether render binding payload — owner: TetherManager
            EnsureTelemetry();
        }

        private void OnEnable()
        {
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

        private void OnDestroy()
        {
            TryUnregisterLateFrameTickable();

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
            _dataVault = null;
        }

        internal void DrainTetherFiredSignals()
        {
            while (TetherSignals.TryConsumeFireForManager(this, out TetherSignals.TetherFireRequest request))
                ExecuteFireRequest(in request);
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

            instance.Configure(owner, playerMotor, playerBody, payloadBody, payloadCollider, initialDistance);
            if (!_activeInstances.Contains(instance))
                _activeInstances.Add(instance);

            _debugActiveTetherCount = _activeInstances.Count;
            return instance;
        }

        private bool ExecuteFireRequest(in TetherSignals.TetherFireRequest request)
        {
            if (request.Owner == null ||
                request.PlayerBody == null ||
                request.PayloadBody == null ||
                request.PayloadCollider == null)
            {
                return false;
            }

            if (request.Owner.HasActiveTow)
                request.Owner.ReleaseTow(false);

            TetherInstance instance = AttachTowCable(
                request.Owner,
                request.PlayerMotor,
                request.PlayerBody,
                request.PayloadBody,
                request.PayloadCollider,
                request.InitialDistance);
            if (instance == null)
                return false;

            return request.Owner.CompleteSignalAttach(instance, request.PayloadBody);
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
            instance.Deactivate();
            if (!_pooledInstances.Contains(instance))
                _pooledInstances.Add(instance);

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

                NativeArray<float3> visualPoints = instance.VisualSegmentPositions;
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
            DrainTetherFiredSignals();
            int activeCount = _activeInstances.Count;
            for (int i = activeCount - 1; i >= 0; i--)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null)
                {
                    _activeInstances.RemoveAt(i);
                    continue;
                }

                TetherLifecycleState state = instance.Simulate(fixedDeltaTime, activeCount, maxVisualizedTethers);
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
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                TetherInstance instance = _activeInstances[i];
                if (instance == null || !instance.IsActive)
                    continue;

                instance.UpdateVisuals(deltaTime);
                if (!instance.IsVisualReady)
                    continue;

                int segmentCount = math.max(0, instance.VisualPointCount - 1);
                if (segmentCount <= 0)
                    continue;

                bool useIndirect = ShouldUseIndirectTetherRendering();
                _renderPropertyBlock.Clear();
                _renderPropertyBlock.SetBuffer(_TetherPositionsId, instance.VisualSegmentBuffer);
                _renderPropertyBlock.SetBuffer(_TetherSegmentTensionsId, instance.VisualSegmentTensionBuffer);
                _renderPropertyBlock.SetColor(_TetherColorId, tetherRenderColor);
                _renderPropertyBlock.SetColor(_TetherStressColorId, tetherStressColor);
                _renderPropertyBlock.SetFloat(_TetherStress01Id, instance.VisualStress01);
                _renderPropertyBlock.SetFloat(_TetherSegmentStressScaleId, tetherSegmentStressScale);
                _renderPropertyBlock.SetInt(_TetherPointCountId, instance.VisualPointCount);
                _renderPropertyBlock.SetFloat(_TetherRadiusId, tetherRenderRadius);
                _renderPropertyBlock.SetInt(_TetherIndirectModeId, useIndirect ? 1 : 0);
                renderParams.worldBounds = instance.GetVisualBounds(tetherBoundsPadding);
                if (useIndirect && TryRenderIndirectTether(renderParams, segmentCount))
                    continue;

                _renderPropertyBlock.SetInt(_TetherIndirectModeId, 0);
                Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, segmentCount * 6, 1);
            }
        }

        private bool ShouldUseIndirectTetherRendering()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
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
            Graphics.RenderMeshIndirect(renderParams, mesh, _indirectTetherArgsBuffer, 1, 0);
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

            GameObject tetherObject = new GameObject("TetherInstance");
            tetherObject.transform.SetParent(transform, false);
            tetherObject.transform.localPosition = Vector3.zero;
            tetherObject.transform.localRotation = Quaternion.identity;
            tetherObject.transform.localScale = Vector3.one;
            // COLD ALLOC: TetherInstance[1] — pooled tether runtime child created on first demand — owner: TetherManager
            TetherInstance instance = tetherObject.AddComponent<TetherInstance>();
            instance.InitializeManager(this);
            return instance;
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
                return math.max(0f, towCableProfile.SpringStiffness);

            return owner != null ? owner.ResolveTowSpringStiffness() : 0f;
        }

        internal float ResolveTowOverDampingMultiplier(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return math.max(1f, towCableProfile.OverDampingMultiplier);

            return owner != null ? owner.ResolveTowOverDampingMultiplier() : 1f;
        }

        internal float ResolveTowSnapTensionThreshold(HeavyTowWinch owner)
        {
            if (towCableProfile != null)
                return math.max(1f, towCableProfile.SnapTensionThreshold);

            return owner != null ? owner.ResolveSnapTensionThreshold() : 1f;
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
            if (_telemetryRing.IsCreated && _telemetryHead.IsCreated)
                return;

            _dataVault = GlobalRegistry.DataVault;
            if (_dataVault == null)
                return;

            _telemetryRing = _dataVault.GetBuffer<TetherManagerTelemetryEntry>(
                BufferID.TetherManagerBlackBox,
                TetherBlackBoxCapacity,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            _telemetryHead = _dataVault.GetBuffer<int>(
                BufferID.TetherManagerBlackBoxHead,
                1,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            if (!_telemetryRing.IsCreated || !_telemetryHead.IsCreated)
                return;

            _telemetryHead[0] = 0;
            _telemetryDumped = false;
        }

        private void WriteBlackBoxSample(int activeTethers, float peakTension, uint flags)
        {
            if (!_telemetryRing.IsCreated || !_telemetryHead.IsCreated)
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
                FrameIndex = (uint)Time.frameCount,
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
                string dumpPath = Path.Combine(
                    projectRoot,
                    TetherBlackBoxDumpRelativePath.Replace('/', Path.DirectorySeparatorChar));
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(TetherBlackBoxMagic);
                    writer.Write(_telemetryRing.Length);
                    writer.Write(_telemetryHead[0]);
                    for (int i = 0; i < _telemetryRing.Length; i++)
                    {
                        TetherManagerTelemetryEntry entry = _telemetryRing[i];
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.ActiveTethers);
                        writer.Write(entry.PeakTension);
                        writer.Write(entry.Flags);
                    }
                }
            }
            catch
            {
                // Fault-path dump must not cascade into physics failure.
            }
        }
    }
}

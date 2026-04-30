using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Runtime scent field used by predator cognition to follow blood, exhaust, and fear pheromones.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6440)]
    [AddComponentMenu("Hecton8/World/Chemical Influence Grid")]
    public sealed class ChemicalInfluenceGrid : MonoBehaviour, ISlowTickable
    {
        internal enum ChemicalChannel : int
        {
            Blood = 0,
            Exhaust = 1,
            Fear = 2,
            Toxicity = 3,
        }

        private struct InfluenceWrite
        {
            public float3 WorldPosition;
            public float4 Delta;
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private struct ChemicalDiffusionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float4> Source;
            [WriteOnly] public NativeArray<float4> Target;
            public int3 Dimensions;
            public float4 DecayRates;
            public float4 DiffusionRates;
            public float MaxChannelIntensity;

            public void Execute(int index)
            {
                int width = Dimensions.x;
                int height = Dimensions.y;
                int slice = width * height;
                int z = index / slice;
                int y = (index - (z * slice)) / width;
                int x = index - (z * slice) - (y * width);

                float4 self = Source[index];
                float4 left = Source[x > 0 ? index - 1 : index];
                float4 right = Source[x + 1 < width ? index + 1 : index];
                float4 down = Source[y > 0 ? index - width : index];
                float4 up = Source[y + 1 < height ? index + width : index];
                float4 back = Source[z > 0 ? index - slice : index];
                float4 forward = Source[z + 1 < Dimensions.z ? index + slice : index];

                float4 neighborAverage = (left + right + down + up + back + forward) / 6f;
                float4 retained = self * (new float4(1f, 1f, 1f, 1f) - DecayRates);
                float4 diffused = neighborAverage * DiffusionRates;
                Target[index] = math.min(math.max(retained + diffused, float4.zero), new float4(MaxChannelIntensity));
            }
        }

        private const string RuntimeRootName = "[ChemicalInfluenceGrid]";
        private const string DefaultComputeShaderAssetPath = "Assets/_Project/Art/Shaders/ChemicalGrid.compute";
        private const int PendingWriteCapacity = 1024;
        private const float DefaultMaximumChannelIntensity = 32f;
        private const float MinimumCellDimension = 0.25f;
        private const float MinimumSubmarineVelocitySqr = 0.25f;
        private const float MinimumTransportSignal = 0.05f;
        private const float ChemicalTransientRadiusMeters = 18f;
        private const float ChemicalTransientLifetimeSeconds = 12f;

        private static readonly int _ChemicalGridSourceId = Shader.PropertyToID("_ChemicalGridSource");
        private static readonly int _ChemicalGridTargetId = Shader.PropertyToID("_ChemicalGridTarget");
        private static readonly int _ChemicalGridDimensionsId = Shader.PropertyToID("_ChemicalGridDimensions");
        private static readonly int _ChemicalGridDecayRatesId = Shader.PropertyToID("_ChemicalGridDecayRates");
        private static readonly int _ChemicalGridDiffusionRatesId = Shader.PropertyToID("_ChemicalGridDiffusionRates");
        private static readonly int _ChemicalGridMaxIntensityId = Shader.PropertyToID("_ChemicalGridMaxIntensity");

        private static ChemicalInfluenceGrid _activeRuntimeInstance;

        [Header("── Grid ──────────────────")]
        [SerializeField, Tooltip("Chemical-grid resolution used for the runtime scent field.")]
        private Vector3Int gridResolution = new Vector3Int(64, 32, 64);

        [SerializeField, Tooltip("World-space volume covered by the chemical grid.")]
        private Vector3 gridWorldSize = new Vector3(512f, 256f, 512f);

        [SerializeField, Tooltip("Maximum value allowed in one scent channel before clamping.")]
        private float maximumChannelIntensity = DefaultMaximumChannelIntensity;

        [Header("── Balance ──────────────────")]
        [SerializeField, Tooltip("Optional authored biome scent-balance profile. Fallback defaults are used when null.")]
        private EcosystemBalanceProfile ecosystemBalanceProfile;

        [SerializeField, Tooltip("Optional compute shader used to mirror the CPU scent diffusion kernel on the GPU.")]
        private ComputeShader chemicalGridCompute;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField] private Vector3Int _debugGridResolution;
        [SerializeField] private Vector3 _debugGridOrigin;
        [SerializeField] private Vector3 _debugCellSize;
        [SerializeField] private int _debugPendingWriteCount;
        [SerializeField] private bool _debugDiffusionPending;

        // COLD ALLOC: InfluenceWrite[1024] - bounded scent write ring consumed by ChemicalInfluenceGrid - owner: ChemicalInfluenceGrid
        private readonly InfluenceWrite[] _pendingWrites = new InfluenceWrite[PendingWriteCapacity];

        private NativeArray<float4> _frontGrid;
        private NativeArray<float4> _backGrid;
        private NativeArray<float4> _overlayGrid;
        private JobHandle _diffusionHandle;
        private bool _diffusionPending;
        private bool _registeredSlowTick;
        private bool _runtimeInitialized;
        private int _pendingWriteCount;
        private int _lastPublishedFrame = -1;
        private Transform _cachedPlayerTransform;
        private HectonSurvivalSystem _cachedPlayerSurvival;
        private float3 _gridOriginWS;
        private float3 _cellSizeWS;
        private GraphicsBuffer _gpuFrontBuffer;
        private GraphicsBuffer _gpuBackBuffer;
        private int _diffusionKernelIndex = -1;

        /// <summary>
        /// Active runtime instance when the scent field is live.
        /// </summary>
        public static ChemicalInfluenceGrid ActiveRuntimeInstance => _activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeRuntimeInstance = null;
        }

        /// <summary>
        /// Ensures a runtime-owned scent field exists.
        /// </summary>
        public static ChemicalInfluenceGrid EnsureRuntimeInstance()
        {
            if (_activeRuntimeInstance != null)
                return _activeRuntimeInstance;

            GameObject runtimeRoot = new GameObject(RuntimeRootName); // COLD ALLOC: GameObject[1] - runtime-owned scent-grid service root - owner: ChemicalInfluenceGrid
            return runtimeRoot.AddComponent<ChemicalInfluenceGrid>();
        }

        internal static void BeginAiFrame(int frameId)
        {
            EnsureRuntimeInstance().PublishFrame(frameId);
        }

        internal static bool TryGetPublishedSnapshot(
            out NativeArray<float4> frontGrid,
            out NativeArray<float4> overlayGrid,
            out int3 dimensions,
            out float3 origin,
            out float3 cellSize)
        {
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            instance.PublishFrame(Time.frameCount);
            frontGrid = instance._frontGrid;
            overlayGrid = instance._overlayGrid;
            dimensions = instance.ResolveDimensions();
            origin = instance._gridOriginWS;
            cellSize = instance._cellSizeWS;
            return frontGrid.IsCreated && overlayGrid.IsCreated;
        }

        internal static bool TrySampleNormalizedChannels(Vector3 worldPosition, out float4 normalizedChannels)
        {
            ChemicalInfluenceGrid instance = EnsureRuntimeInstance();
            instance.PublishFrame(Time.frameCount);
            return instance.TrySampleNormalizedChannelsInternal(
                new float3(worldPosition.x, worldPosition.y, worldPosition.z),
                out normalizedChannels);
        }

        internal static void QueueBloodScent(Vector3 worldPosition, float intensity = 1f)
        {
            float clampedIntensity = math.max(0f, intensity);
            EnsureRuntimeInstance().Enqueue(worldPosition, new float4(clampedIntensity, 0f, 0f, 0f));
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueExhaustScent(Vector3 worldPosition, float intensity = 1f)
        {
            float clampedIntensity = math.max(0f, intensity);
            EnsureRuntimeInstance().Enqueue(worldPosition, new float4(0f, clampedIntensity, 0f, 0f));
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueFearPheromone(Vector3 worldPosition, float intensity)
        {
            float clampedIntensity = math.max(0f, intensity);
            EnsureRuntimeInstance().Enqueue(worldPosition, new float4(0f, 0f, clampedIntensity, 0f));
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        internal static void QueueToxicityBurst(Vector3 worldPosition, float intensity)
        {
            float clampedIntensity = math.max(0f, intensity);
            EnsureRuntimeInstance().Enqueue(worldPosition, new float4(0f, 0f, 0f, clampedIntensity));
            RegisterChemicalTransient(worldPosition, clampedIntensity);
        }

        private static void RegisterChemicalTransient(Vector3 worldPosition, float intensity)
        {
            if (intensity <= 0f)
                return;

            WorldSpatialHashGrid.RegisterTransientEvent(
                worldPosition,
                ChemicalTransientRadiusMeters,
                math.saturate(intensity),
                ChemicalTransientLifetimeSeconds,
                SpatialTransientEventType.ChemicalCloud,
                SpatialInteractionFlags.ChemicalReceiver);
        }

        private void Awake()
        {
            EnsureSingletonOwnership();
            InitializeRuntime();
        }

        private void OnEnable()
        {
            InitializeRuntime();
            TryRegisterSlowTick();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTick();
            DisposeBuffers();
        }

        private void OnDestroy()
        {
            TryUnregisterSlowTick();
            DisposeBuffers();

            if (_activeRuntimeInstance == this)
                _activeRuntimeInstance = null;
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            InitializeRuntime();
            PublishFrame(Time.frameCount);
            CollectPersistentRuntimeEmissions();
            ApplyOverlayToFrontGrid();
            ScheduleDiffusionPass();
            DispatchGpuMirror();
            UpdateDebugState();
        }

        private void EnsureSingletonOwnership()
        {
            if (_activeRuntimeInstance != null && _activeRuntimeInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            _activeRuntimeInstance = this;
        }

        private void InitializeRuntime()
        {
            if (_runtimeInitialized)
                return;

            EnsureSingletonOwnership();
            if (_activeRuntimeInstance != this)
                return;

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }

            ResolveGridMetrics();
            InitializeBuffers();
            ResolveComputeKernel();
            _runtimeInitialized = true;
            UpdateDebugState();
        }

        private void InitializeBuffers()
        {
            int cellCount = ResolveCellCount();
            if (!_frontGrid.IsCreated)
                _frontGrid = new NativeArray<float4>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4>[cellCount] - published chemical scent front buffer - owner: ChemicalInfluenceGrid
            if (!_backGrid.IsCreated)
                _backGrid = new NativeArray<float4>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4>[cellCount] - scheduled chemical scent back buffer - owner: ChemicalInfluenceGrid
            if (!_overlayGrid.IsCreated)
                _overlayGrid = new NativeArray<float4>(cellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4>[cellCount] - immediate-frame scent overlay - owner: ChemicalInfluenceGrid
        }

        private void PublishFrame(int frameId)
        {
            InitializeRuntime();
            if (_activeRuntimeInstance != this)
                return;

            FinalizeDiffusionIfReady();
            if (_lastPublishedFrame == frameId)
                return;

            FlushPendingWritesToOverlay();
            _lastPublishedFrame = frameId;
            UpdateDebugState();
        }

        private void CollectPersistentRuntimeEmissions()
        {
            if (TryResolvePlayerSurvival(out Transform playerTransform, out HectonSurvivalSystem playerSurvival) &&
                playerTransform != null &&
                playerSurvival != null &&
                playerSurvival.IsBleeding)
            {
                Enqueue(playerTransform.position, new float4(1f, 0f, 0f, 0f));
            }

            if (NoiseSystem.TryGetPlayerSignal(out NoiseSystem.PlayerNoiseSignal playerNoise) &&
                playerNoise.TransportBoost01 >= MinimumTransportSignal)
            {
                Enqueue(playerNoise.Position, new float4(0f, 1f, 0f, 0f));
            }

            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            if (submarine != null &&
                submarine.PlatformTransform != null &&
                submarine.HullRigidbody != null &&
                submarine.HullRigidbody.linearVelocity.sqrMagnitude >= MinimumSubmarineVelocitySqr)
            {
                Enqueue(submarine.PlatformTransform.position, new float4(0f, 1f, 0f, 0f));
            }

            FlushPendingWritesToOverlay();
        }

        private bool TryResolvePlayerSurvival(out Transform playerTransform, out HectonSurvivalSystem playerSurvival)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (_cachedPlayerTransform != playerTransform)
            {
                _cachedPlayerTransform = playerTransform;
                _cachedPlayerSurvival = null;
                if (_cachedPlayerTransform != null)
                    _cachedPlayerTransform.TryGetComponent(out _cachedPlayerSurvival);
            }

            playerSurvival = _cachedPlayerSurvival;
            return playerTransform != null && playerSurvival != null;
        }

        private void Enqueue(Vector3 worldPosition, float4 delta)
        {
            if (_pendingWriteCount >= _pendingWrites.Length)
                return;

            _pendingWrites[_pendingWriteCount].WorldPosition = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            _pendingWrites[_pendingWriteCount].Delta = delta;
            _pendingWriteCount++;
        }

        private void FlushPendingWritesToOverlay()
        {
            if (!_overlayGrid.IsCreated || _pendingWriteCount <= 0)
                return;

            for (int i = 0; i < _pendingWriteCount; i++)
            {
                InfluenceWrite write = _pendingWrites[i];
                if (!TryWorldToCell(write.WorldPosition, out int3 cell))
                    continue;

                int flatIndex = Flatten(cell);
                float4 next = _overlayGrid[flatIndex] + write.Delta;
                _overlayGrid[flatIndex] = math.min(next, new float4(math.max(0.1f, maximumChannelIntensity)));
            }

            _pendingWriteCount = 0;
        }

        private void ApplyOverlayToFrontGrid()
        {
            if (!_frontGrid.IsCreated || !_overlayGrid.IsCreated)
                return;

            for (int i = 0; i < _frontGrid.Length; i++)
            {
                float4 overlay = _overlayGrid[i];
                if (math.lengthsq(overlay) <= 0f)
                    continue;

                _frontGrid[i] = math.min(_frontGrid[i] + overlay, new float4(math.max(0.1f, maximumChannelIntensity)));
                _overlayGrid[i] = float4.zero;
            }
        }

        private void ScheduleDiffusionPass()
        {
            if (_diffusionPending || !_frontGrid.IsCreated || !_backGrid.IsCreated)
                return;

            EcosystemBalanceProfile.BiomeChemicalBalance balance = ResolveRuntimeBalance();
            var job = new ChemicalDiffusionJob
            {
                Source = _frontGrid,
                Target = _backGrid,
                Dimensions = ResolveDimensions(),
                DecayRates = new float4(balance.bloodDecayRate, balance.exhaustDecayRate, balance.fearDecayRate, 0f),
                DiffusionRates = new float4(balance.bloodDiffusionRate, balance.exhaustDiffusionRate, balance.fearDiffusionRate, 0f),
                MaxChannelIntensity = math.max(0.1f, maximumChannelIntensity)
            };

            _diffusionHandle = job.Schedule(_frontGrid.Length, 64);
            _diffusionPending = true;
            _debugDiffusionPending = true;
        }

        private void FinalizeDiffusionIfReady()
        {
            if (!_diffusionPending || !_diffusionHandle.IsCompleted)
                return;

            _diffusionHandle.Complete();
            (_frontGrid, _backGrid) = (_backGrid, _frontGrid);
            _diffusionHandle = default;
            _diffusionPending = false;
            _debugDiffusionPending = false;
        }

        private void DispatchGpuMirror()
        {
            if (chemicalGridCompute == null || _diffusionKernelIndex < 0 || !_frontGrid.IsCreated)
                return;

            int cellCount = _frontGrid.Length;
            EnsureGpuBuffers(cellCount);
            if (_gpuFrontBuffer == null || _gpuBackBuffer == null)
                return;

            EcosystemBalanceProfile.BiomeChemicalBalance balance = ResolveRuntimeBalance();
            _gpuFrontBuffer.SetData(_frontGrid);
            chemicalGridCompute.SetBuffer(_diffusionKernelIndex, _ChemicalGridSourceId, _gpuFrontBuffer);
            chemicalGridCompute.SetBuffer(_diffusionKernelIndex, _ChemicalGridTargetId, _gpuBackBuffer);
            chemicalGridCompute.SetInts(_ChemicalGridDimensionsId, gridResolution.x, gridResolution.y, gridResolution.z);
            chemicalGridCompute.SetVector(_ChemicalGridDecayRatesId, new Vector4(balance.bloodDecayRate, balance.exhaustDecayRate, balance.fearDecayRate, 0f));
            chemicalGridCompute.SetVector(_ChemicalGridDiffusionRatesId, new Vector4(balance.bloodDiffusionRate, balance.exhaustDiffusionRate, balance.fearDiffusionRate, 0f));
            chemicalGridCompute.SetFloat(_ChemicalGridMaxIntensityId, math.max(0.1f, maximumChannelIntensity));

            int groupCount = Mathf.Max(1, Mathf.CeilToInt(cellCount / 64f));
            chemicalGridCompute.Dispatch(_diffusionKernelIndex, groupCount, 1, 1);
            (_gpuFrontBuffer, _gpuBackBuffer) = (_gpuBackBuffer, _gpuFrontBuffer);
        }

        private void EnsureGpuBuffers(int cellCount)
        {
            if (_gpuFrontBuffer == null)
                _gpuFrontBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, cellCount, 16); // COLD ALLOC: GraphicsBuffer[cellCount] - GPU mirror of chemical scent front field - owner: ChemicalInfluenceGrid
            if (_gpuBackBuffer == null)
                _gpuBackBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, cellCount, 16); // COLD ALLOC: GraphicsBuffer[cellCount] - GPU mirror of chemical scent back field - owner: ChemicalInfluenceGrid
        }

        private EcosystemBalanceProfile.BiomeChemicalBalance ResolveRuntimeBalance()
        {
            return ecosystemBalanceProfile != null
                ? ecosystemBalanceProfile.DefaultBiomeBalance
                : EcosystemBalanceProfile.DefaultBalance;
        }

        private void ResolveGridMetrics()
        {
            gridResolution.x = Mathf.Max(1, gridResolution.x);
            gridResolution.y = Mathf.Max(1, gridResolution.y);
            gridResolution.z = Mathf.Max(1, gridResolution.z);
            gridWorldSize.x = Mathf.Max(MinimumCellDimension, gridWorldSize.x);
            gridWorldSize.y = Mathf.Max(MinimumCellDimension, gridWorldSize.y);
            gridWorldSize.z = Mathf.Max(MinimumCellDimension, gridWorldSize.z);
            maximumChannelIntensity = Mathf.Max(0.1f, maximumChannelIntensity);

            _cellSizeWS = new float3(
                gridWorldSize.x / gridResolution.x,
                gridWorldSize.y / gridResolution.y,
                gridWorldSize.z / gridResolution.z);

            Transform anchor = GlobalRegistry.Player != null ? GlobalRegistry.Player.PlayerTransform : null;
            float3 anchorPosition = anchor != null
                ? new float3(anchor.position.x, anchor.position.y, anchor.position.z)
                : float3.zero;
            _gridOriginWS = anchorPosition - (new float3(gridWorldSize.x, gridWorldSize.y, gridWorldSize.z) * 0.5f);
        }

        private void ResolveComputeKernel()
        {
#if UNITY_EDITOR
            if (chemicalGridCompute == null)
                chemicalGridCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(DefaultComputeShaderAssetPath);
#endif

            if (chemicalGridCompute == null)
            {
                _diffusionKernelIndex = -1;
                return;
            }

            _diffusionKernelIndex = chemicalGridCompute.FindKernel("DiffuseChemicalGrid");
        }

        private int ResolveCellCount()
        {
            int3 dimensions = ResolveDimensions();
            return dimensions.x * dimensions.y * dimensions.z;
        }

        private int3 ResolveDimensions()
        {
            return new int3(math.max(1, gridResolution.x), math.max(1, gridResolution.y), math.max(1, gridResolution.z));
        }

        private bool TryWorldToCell(float3 worldPosition, out int3 cell)
        {
            float3 local = worldPosition - _gridOriginWS;
            if (local.x < 0f || local.y < 0f || local.z < 0f)
            {
                cell = int3.zero;
                return false;
            }

            float3 safeCellSize = math.max(_cellSizeWS, new float3(MinimumCellDimension));
            int3 candidate = new int3(
                (int)math.floor(local.x / safeCellSize.x),
                (int)math.floor(local.y / safeCellSize.y),
                (int)math.floor(local.z / safeCellSize.z));
            int3 dimensions = ResolveDimensions();
            if (candidate.x < 0 || candidate.y < 0 || candidate.z < 0 ||
                candidate.x >= dimensions.x || candidate.y >= dimensions.y || candidate.z >= dimensions.z)
            {
                cell = int3.zero;
                return false;
            }

            cell = candidate;
            return true;
        }

        private int Flatten(int3 cell)
        {
            int3 dimensions = ResolveDimensions();
            return cell.x + (cell.y * dimensions.x) + (cell.z * dimensions.x * dimensions.y);
        }

        private bool TrySampleNormalizedChannelsInternal(float3 worldPosition, out float4 normalizedChannels)
        {
            normalizedChannels = float4.zero;
            if (!_frontGrid.IsCreated || !_overlayGrid.IsCreated)
                return false;

            if (!TryWorldToCell(worldPosition, out int3 cell))
                return false;

            float4 combinedChannels = _frontGrid[Flatten(cell)] + _overlayGrid[Flatten(cell)];
            float inverseMaxIntensity = 1f / math.max(0.1f, maximumChannelIntensity);
            normalizedChannels = math.saturate(combinedChannels * inverseMaxIntensity);
            return true;
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = true;
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTick = false;
        }

        private void DisposeBuffers()
        {
            JobHandle disposeDependency = _diffusionPending ? _diffusionHandle : default;
            if (_frontGrid.IsCreated)
                _frontGrid.Dispose(disposeDependency);
            if (_backGrid.IsCreated)
                _backGrid.Dispose(disposeDependency);
            if (_overlayGrid.IsCreated)
                _overlayGrid.Dispose();

            _frontGrid = default;
            _backGrid = default;
            _overlayGrid = default;
            _diffusionHandle = default;
            _diffusionPending = false;
            _pendingWriteCount = 0;
            _lastPublishedFrame = -1;
            _runtimeInitialized = false;
            _cachedPlayerTransform = null;
            _cachedPlayerSurvival = null;
            _diffusionKernelIndex = -1;
            _debugDiffusionPending = false;

            _gpuFrontBuffer?.Dispose();
            _gpuBackBuffer?.Dispose();
            _gpuFrontBuffer = null;
            _gpuBackBuffer = null;
        }

        private void UpdateDebugState()
        {
            _debugGridResolution = gridResolution;
            _debugGridOrigin = new Vector3(_gridOriginWS.x, _gridOriginWS.y, _gridOriginWS.z);
            _debugCellSize = new Vector3(_cellSizeWS.x, _cellSizeWS.y, _cellSizeWS.z);
            _debugPendingWriteCount = _pendingWriteCount;
            _debugDiffusionPending = _diffusionPending;
        }
    }
}

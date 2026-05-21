using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Graphics.Culling
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-86)]
    public sealed class TBDRPipelineSurgeonRuntime : MonoBehaviour, IDisposable
    {
        private const int DefaultSortCapacity = 150000;
        private const int RadixHistogramCapacity = 256;
        private const int TelemetryCapacity = 300;

        [Header("Hard Limits")]
        [SerializeField, Min(1)]
        private int _sortCapacity = DefaultSortCapacity;

        [SerializeField, Min(1)]
        private uint _hardVertexCap = 800000u;

        [SerializeField, Min(1)]
        private int _transparentQuadLimit = 5000;

        [SerializeField, Range(0f, 15f)]
        private float _frustumSqueezeAngle = 12f;

        [SerializeField, Range(0f, 1f)]
        private float _globalQualityWeight = 1f;

        [SerializeField]
        private string _gpuBudgetCsvPath = "Data/Rendering/gpu_budgets.csv";

        [Header("Editor Debug")]
        public bool EditorShowSorting;

        public TBDRVertexBudgetVault Vault;
        public NativeArray<PoiTransformDTO> MockVisibleInstances;
        public NativeArray<PoiTransformDTO> SortScratch;
        public NativeArray<uint> MeshVertexCounts;
        public NativeArray<int> RadixHistogram;
        public NativeArray<int> VisibleCountOut;
        public NativeArray<MockQualityWeightSignal> MockQualitySignal;
        public NativeArray<MockCameraMatrix> MockCamera;
        public NativeArray<float4> SourceFrustumPlanes;
        public NativeArray<float4> SqueezedFrustumPlanes;
        public NativeArray<int> HzbVisibilityMask;
        public NativeArray<TBDRIndirectDrawArgsDTO> IndirectDrawArgs;

        private IDataVault _dataVault;
        private VaultGenerationHandle<PoiTransformDTO> _mockVisibleHandle;
        private VaultGenerationHandle<PoiTransformDTO> _sortScratchHandle;
        private VaultGenerationHandle<uint> _meshVertexCountsHandle;
        private VaultGenerationHandle<int> _radixHistogramHandle;
        private VaultGenerationHandle<int> _visibleCountOutHandle;
        private VaultGenerationHandle<MockQualityWeightSignal> _mockQualitySignalHandle;
        private VaultGenerationHandle<MockCameraMatrix> _mockCameraHandle;
        private VaultGenerationHandle<float4> _sourceFrustumPlanesHandle;
        private VaultGenerationHandle<float4> _squeezedFrustumPlanesHandle;
        private VaultGenerationHandle<int> _hzbVisibilityMaskHandle;
        private VaultGenerationHandle<TBDRIndirectDrawArgsDTO> _indirectDrawArgsHandle;
        private TBDRHardwareBudgetLimits _limits;
        private TBDRPipelineTelemetryRecorder _telemetry;
        private TBDRGpuBudgetCsvIngestor _csvIngestor;
        private int _lastSortedCount;
        private uint _lastFrame;
        private float _lastSortMs;
        private string _resolvedGpuBudgetCsvPath;
        private bool _initialized;
        private bool _usesVaultStorage;
        private bool _csvPathDirty = true;

        private void Awake()
        {
            Initialize(GlobalRegistry.DataVault);
        }

        private void OnDisable()
        {
            Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Initialize()
        {
            Initialize(GlobalRegistry.DataVault);
        }

        public void Initialize(IDataVault dataVault)
        {
            if (_initialized)
                return;

            _dataVault = dataVault;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string docsArchive = Path.Combine(projectRoot, "Docs", "Archive");
            string streamingAssets = Application.streamingAssetsPath;
            if (!TBDRLegacyBudgetArchaeology.TryLoadLegacyLimits(docsArchive, streamingAssets, out _limits))
                _limits = TBDRLegacyBudgetArchaeology.GenerateEmergencyMockLimits();

            _hardVertexCap = TBDRHardwareBudgetMath.ClampVisibleVertexCap(_limits.Quest3MaxVisibleVertices);
            _transparentQuadLimit = (int)math.max(1u, _limits.TransparentQuadLimit);
            _frustumSqueezeAngle = math.clamp(_limits.FrustumSqueezeDegrees, 0f, 15f);

            Vault = new TBDRVertexBudgetVault(_dataVault, 1, 1, 4, TelemetryCapacity);
            Vault.ApplyHardLimits(in _limits);
            AllocateNativeMockBuffers(math.max(1, _sortCapacity), _dataVault);
            _telemetry = new TBDRPipelineTelemetryRecorder();
            _telemetry.BindExternalRing(Vault.TelemetryRing);
            _telemetry.EnsureCreated();
            _csvIngestor = new TBDRGpuBudgetCsvIngestor();
            TBDRComputeDispatchLimiter.Boot();
            ResolveGpuBudgetCsvPath();
            SeedMockData();
            _initialized = true;
            PushShaderBudgetGlobals();
        }

        public void ApplyEditorLimits(uint hardVertexCap, int transparentQuadLimit, float frustumSqueezeAngle)
        {
            Initialize();
            _hardVertexCap = TBDRHardwareBudgetMath.ClampVisibleVertexCap(hardVertexCap);
            _transparentQuadLimit = math.max(1, transparentQuadLimit);
            _frustumSqueezeAngle = math.clamp(frustumSqueezeAngle, 0f, 15f);

            ref VertexBudgetDTO budget = ref Vault.BudgetRef(0);
            budget.MaxVisibleVertices = _hardVertexCap;
            budget.CurrentVisibleVertices = 0u;
            budget.TilePressure = 0f;

            if (Vault.TransparentQuadCount.IsCreated && Vault.TransparentQuadCount.Length > 0)
                Vault.TransparentQuadCount[0] = _transparentQuadLimit;

            PushShaderBudgetGlobals();
        }

        public bool PollBudgetCsvOverride()
        {
            Initialize();
            string path = ResolveGpuBudgetCsvPath();
            bool applied = _csvIngestor.Poll(path, ref Vault);
            if (applied)
            {
                _hardVertexCap = _csvIngestor.LastParsedVertexCap;
                _transparentQuadLimit = _csvIngestor.LastParsedTransparentQuadLimit;
                _frustumSqueezeAngle = _csvIngestor.LastParsedFrustumSqueezeDegrees;
                PushShaderBudgetGlobals();
            }

            return applied;
        }

        public unsafe JobHandle ScheduleTBDRProtectionPass(int requestedInstanceCount, JobHandle dependency)
        {
            return ScheduleTBDRProtectionPass(requestedInstanceCount, unchecked((uint)Time.frameCount), dependency);
        }

        public unsafe JobHandle ScheduleTBDRProtectionPass(int requestedInstanceCount, uint simulationFrame, JobHandle dependency)
        {
            Initialize();
            int instanceCount = math.min(math.max(1, requestedInstanceCount), MockVisibleInstances.Length);
            _lastFrame = simulationFrame;

            JobHandle handle = new MockQualityWeightJob
            {
                QualitySignal = MockQualitySignal,
                Frame = _lastFrame,
                SeedSalt = 0x9E3779B9u
            }.Schedule(dependency);

            handle = new DearLieFrustumSqueezeJob
            {
                BudgetPtr = Vault.BudgetPtr(0),
                QualitySignal = MockQualitySignal,
                Camera = MockCamera,
                SourcePlanes = SourceFrustumPlanes,
                SqueezedPlanes = SqueezedFrustumPlanes,
                MobileBaseVertexCap = _hardVertexCap,
                MaxSqueezeDegrees = _frustumSqueezeAngle
            }.Schedule(handle);

            handle = new DearLieFrustumVisibilityJob
            {
                Instances = MockVisibleInstances,
                FrustumPlanes = SqueezedFrustumPlanes,
                VisibilityMask = HzbVisibilityMask,
                Count = instanceCount
            }.Schedule(instanceCount, 64, handle);

            handle = new BuildDistanceSortKeysJob
            {
                Instances = MockVisibleInstances
            }.Schedule(instanceCount, 64, handle);

            if (TBDRHardwarePipelineSwitch.ShouldRunEarlyZRadixSort())
            {
                handle = new EarlyZRadixSortJob
                {
                    Source = MockVisibleInstances,
                    Scratch = SortScratch,
                    Histogram = RadixHistogram,
                    Count = instanceCount
                }.Schedule(handle);
            }

            handle = new VertexBudgetJob
            {
                BudgetPtr = Vault.BudgetPtr(0),
                TileWarningPtr = Vault.WarningPtr(0),
                MeshVertexCounts = MeshVertexCounts,
                VisibilityMask = default,
                VisibleInstances = MockVisibleInstances,
                VisibleCountOut = VisibleCountOut,
                SourceCount = instanceCount
            }.Schedule(handle);

            handle = new BuildIndirectDrawArgsJob
            {
                VisibleCountOut = VisibleCountOut,
                ArgsOut = IndirectDrawArgs,
                VertexCountPerInstance = 1u,
                StartVertex = 0u,
                StartInstance = 0u,
                StartIndex = 0u
            }.Schedule(handle);

            return handle;
        }

        public void CommitCompletedProtectionPass(float elapsedMs)
        {
            Initialize();
            _lastSortMs = math.isfinite(elapsedMs) ? math.max(0f, elapsedMs) : 0f;
            _lastSortedCount = VisibleCountOut.IsCreated && VisibleCountOut.Length > 0 ? VisibleCountOut[0] : 0;

            VertexBudgetDTO budget = Vault.BudgetRef(0);
            TileSpillWarningDTO warning = Vault.TileWarnings[0];
            _globalQualityWeight = CurrentQualityWeight();
            _telemetry.Record(
                _lastFrame,
                budget.CurrentVisibleVertices,
                budget.MaxVisibleVertices,
                warning.CulledInstanceCount > 0u ? 1u : 0u,
                _lastSortMs,
                budget.TilePressure,
                TBDRHardwarePipelineSwitch.IsMobileTBDR() ? 1u : 0u);
            PushShaderBudgetGlobals(in budget, in warning);
        }

        public unsafe bool RunMockPipelineOnce(int requestedInstanceCount)
        {
            float sortStart = Time.realtimeSinceStartup;
            JobHandle handle = ScheduleTBDRProtectionPass(requestedInstanceCount, default);
            // COLD/EDITOR SYNC FACADE: tuner-only mock pipeline proof, not dispatcher frame cadence.
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            CommitCompletedProtectionPass((Time.realtimeSinceStartup - sortStart) * 1000f);
            return true;
        }

        public bool TryGetTunerSnapshot(out TBDRTunerSnapshot snapshot)
        {
            Initialize();
            if (!Vault.IsCreated())
            {
                snapshot = default;
                return false;
            }

            VertexBudgetDTO budget = Vault.BudgetRef(0);
            TileSpillWarningDTO warning = Vault.TileWarnings[0];
            uint transparentLimit = Vault.TransparentQuadCount.IsCreated && Vault.TransparentQuadCount.Length > 0
                ? (uint)math.max(0, Vault.TransparentQuadCount[0])
                : 0u;
            snapshot = new TBDRTunerSnapshot
            {
                HardVertexCap = budget.MaxVisibleVertices,
                CurrentVisibleVertices = budget.CurrentVisibleVertices,
                TransparentQuadLimit = transparentLimit,
                TotalSubmittedVertices = budget.CurrentVisibleVertices,
                TilePressure = budget.TilePressure,
                FrustumSqueezeDegrees = _frustumSqueezeAngle,
                EstimatedVramMb = 0f,
                Flags = warning.CulledInstanceCount > 0u ? 1u : 0u
            };
            return true;
        }

        public void SetCsvPath(string path)
        {
            _gpuBudgetCsvPath = path;
            _resolvedGpuBudgetCsvPath = null;
            _csvPathDirty = true;
        }

        public string GetCsvPath()
        {
            return _gpuBudgetCsvPath;
        }

        public float LastSortComputeTimeMs()
        {
            return _lastSortMs;
        }

        public int LastSortedCount()
        {
            return _lastSortedCount;
        }

        private void PushShaderBudgetGlobals()
        {
            if (!Vault.IsCreated())
                return;

            VertexBudgetDTO budget = Vault.BudgetRef(0);
            TileSpillWarningDTO warning = Vault.TileWarnings[0];
            PushShaderBudgetGlobals(in budget, in warning);
        }

        private void PushShaderBudgetGlobals(in VertexBudgetDTO budget, in TileSpillWarningDTO warning)
        {
            float quality = CurrentQualityWeight();
            float squeezeDegrees = CurrentFrustumSqueezeDegrees(quality, budget.TilePressure);
            uint transparentLimit = Vault.TransparentQuadCount.IsCreated && Vault.TransparentQuadCount.Length > 0
                ? (uint)math.max(0, Vault.TransparentQuadCount[0])
                : 0u;
            TBDRShaderBudgetGlobalsDTO globals = new TBDRShaderBudgetGlobalsDTO
            {
                GlobalQualityWeight = quality,
                FrustumSqueezeDegrees = squeezeDegrees,
                TilePressure = budget.TilePressure,
                EstimatedVramMb = 0f,
                HardVertexCap = budget.MaxVisibleVertices,
                CurrentVisibleVertices = budget.CurrentVisibleVertices,
                TransparentQuadLimit = transparentLimit,
                Flags = warning.CulledInstanceCount > 0u ? 1u : 0u
            };
            TBDRGlobalShaderBudgetBinder.Push(in globals);
        }

        private float CurrentQualityWeight()
        {
            if (MockQualitySignal.IsCreated && MockQualitySignal.Length > 0)
            {
                float quality = MockQualitySignal[0].GlobalQualityWeight;
                if (math.isfinite(quality))
                    return math.saturate(quality);
            }

            return math.isfinite(_globalQualityWeight) ? math.saturate(_globalQualityWeight) : 1f;
        }

        private float CurrentFrustumSqueezeDegrees(float quality, float tilePressure)
        {
            float qualityStress = 1f - math.saturate(quality);
            float pressureStress = math.saturate((math.saturate(tilePressure) - 0.82f) * 5.5555553f);
            pressureStress = pressureStress * pressureStress * (3f - 2f * pressureStress);
            float stress = math.max(qualityStress, pressureStress);
            return math.clamp(_frustumSqueezeAngle * stress, 0f, _frustumSqueezeAngle);
        }

        public void Dispose()
        {
            if (!_initialized)
                return;

            if (!_usesVaultStorage && MockVisibleInstances.IsCreated)
                MockVisibleInstances.Dispose();
            if (!_usesVaultStorage && SortScratch.IsCreated)
                SortScratch.Dispose();
            if (!_usesVaultStorage && MeshVertexCounts.IsCreated)
                MeshVertexCounts.Dispose();
            if (!_usesVaultStorage && RadixHistogram.IsCreated)
                RadixHistogram.Dispose();
            if (!_usesVaultStorage && VisibleCountOut.IsCreated)
                VisibleCountOut.Dispose();
            if (!_usesVaultStorage && MockQualitySignal.IsCreated)
                MockQualitySignal.Dispose();
            if (!_usesVaultStorage && MockCamera.IsCreated)
                MockCamera.Dispose();
            if (!_usesVaultStorage && SourceFrustumPlanes.IsCreated)
                SourceFrustumPlanes.Dispose();
            if (!_usesVaultStorage && SqueezedFrustumPlanes.IsCreated)
                SqueezedFrustumPlanes.Dispose();
            if (!_usesVaultStorage && HzbVisibilityMask.IsCreated)
                HzbVisibilityMask.Dispose();
            if (!_usesVaultStorage && IndirectDrawArgs.IsCreated)
                IndirectDrawArgs.Dispose();

            MockVisibleInstances = default;
            SortScratch = default;
            MeshVertexCounts = default;
            RadixHistogram = default;
            VisibleCountOut = default;
            MockQualitySignal = default;
            MockCamera = default;
            SourceFrustumPlanes = default;
            SqueezedFrustumPlanes = default;
            HzbVisibilityMask = default;
            IndirectDrawArgs = default;
            ResetVaultHandles();
            Vault.Dispose();
            if (_telemetry != null)
                _telemetry.Dispose();
            _telemetry = null;
            _csvIngestor = null;
            _initialized = false;
            _usesVaultStorage = false;
            _dataVault = null;
            _resolvedGpuBudgetCsvPath = null;
            _csvPathDirty = true;
            _lastSortedCount = 0;
            _lastSortMs = 0f;
        }

        private string ResolveGpuBudgetCsvPath()
        {
            if (!_csvPathDirty && !string.IsNullOrEmpty(_resolvedGpuBudgetCsvPath))
                return _resolvedGpuBudgetCsvPath;

            string path = string.IsNullOrEmpty(_gpuBudgetCsvPath)
                ? "Data/Rendering/gpu_budgets.csv"
                : _gpuBudgetCsvPath;
            _resolvedGpuBudgetCsvPath = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            _csvPathDirty = false;
            return _resolvedGpuBudgetCsvPath;
        }

        private void AllocateNativeMockBuffers(int capacity, IDataVault dataVault)
        {
            _usesVaultStorage = dataVault != null;
            if (_usesVaultStorage)
            {
                _usesVaultStorage = TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _mockVisibleHandle, TBDRBufferIds.MockVisibleInstances, capacity, NativeArrayOptions.UninitializedMemory, out MockVisibleInstances) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _sortScratchHandle, TBDRBufferIds.SortScratch, capacity, NativeArrayOptions.UninitializedMemory, out SortScratch) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _meshVertexCountsHandle, TBDRBufferIds.MeshVertexCounts, 256, NativeArrayOptions.UninitializedMemory, out MeshVertexCounts) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _radixHistogramHandle, TBDRBufferIds.RadixHistogram, RadixHistogramCapacity, NativeArrayOptions.UninitializedMemory, out RadixHistogram) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _visibleCountOutHandle, TBDRBufferIds.VisibleCountOut, 1, NativeArrayOptions.UninitializedMemory, out VisibleCountOut) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _mockQualitySignalHandle, TBDRBufferIds.MockQualitySignal, 1, NativeArrayOptions.UninitializedMemory, out MockQualitySignal) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _mockCameraHandle, TBDRBufferIds.MockCamera, 1, NativeArrayOptions.UninitializedMemory, out MockCamera) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _sourceFrustumPlanesHandle, TBDRBufferIds.SourceFrustumPlanes, 6, NativeArrayOptions.UninitializedMemory, out SourceFrustumPlanes) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _squeezedFrustumPlanesHandle, TBDRBufferIds.SqueezedFrustumPlanes, 6, NativeArrayOptions.UninitializedMemory, out SqueezedFrustumPlanes) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _hzbVisibilityMaskHandle, TBDRBufferIds.HzbVisibilityMask, capacity, NativeArrayOptions.UninitializedMemory, out HzbVisibilityMask) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _indirectDrawArgsHandle, TBDRBufferIds.IndirectDrawArgs, 1, NativeArrayOptions.UninitializedMemory, out IndirectDrawArgs);
            }

            if (!_usesVaultStorage)
            {
                MockVisibleInstances = new NativeArray<PoiTransformDTO>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                SortScratch = new NativeArray<PoiTransformDTO>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                MeshVertexCounts = new NativeArray<uint>(256, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                RadixHistogram = new NativeArray<int>(RadixHistogramCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                VisibleCountOut = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                MockQualitySignal = new NativeArray<MockQualityWeightSignal>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                MockCamera = new NativeArray<MockCameraMatrix>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                SourceFrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                SqueezedFrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                HzbVisibilityMask = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                IndirectDrawArgs = new NativeArray<TBDRIndirectDrawArgsDTO>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
            }
        }

        private void ResetVaultHandles()
        {
            _mockVisibleHandle = default;
            _sortScratchHandle = default;
            _meshVertexCountsHandle = default;
            _radixHistogramHandle = default;
            _visibleCountOutHandle = default;
            _mockQualitySignalHandle = default;
            _mockCameraHandle = default;
            _sourceFrustumPlanesHandle = default;
            _squeezedFrustumPlanesHandle = default;
            _hzbVisibilityMaskHandle = default;
            _indirectDrawArgsHandle = default;
        }

        private void SeedMockData()
        {
            for (int i = 0; i < MeshVertexCounts.Length; i++)
                MeshVertexCounts[i] = (uint)(300 + (i & 15) * 64);

            float initialQuality = math.isfinite(_globalQualityWeight) ? math.saturate(_globalQualityWeight) : 1f;
            MockQualitySignal[0] = new MockQualityWeightSignal
            {
                GlobalQualityWeight = initialQuality,
                Frame = 0u,
                Seed = 0x45A11CEu,
                _pad0 = 0u
            };

            for (int i = 0; i < MockVisibleInstances.Length; i++)
            {
                float z = 4f + (i % 512) * 0.45f;
                float x = ((i * 37) & 127) - 63.5f;
                float y = ((i * 19) & 63) - 31.5f;
                MockVisibleInstances[i] = new PoiTransformDTO
                {
                    LocalToWorld = float4x4.Translate(new float3(x, y, z)),
                    CameraRelativePositionRadius = new float4(x, y, z, 1f),
                    MeshId = (uint)(i & 255),
                    InstanceId = (uint)i,
                    VertexCount = MeshVertexCounts[i & 255],
                    DistanceSq = x * x + y * y + z * z,
                    SortKey = math.asuint(x * x + y * y + z * z),
                    Flags = 0u,
                    _pad0 = 0ul,
                    _pad1 = 0ul,
                    _pad2 = 0ul
                };

                HzbVisibilityMask[i] = 1;
            }

            MockCamera[0] = new MockCameraMatrix
            {
                ViewProjection = float4x4.identity,
                PositionRadius = new float4(0f, 0f, 0f, 1f),
                ForwardFov = new float4(0f, 0f, 1f, 90f),
                Frame = 0u,
                Flags = 0u,
                _pad0 = 0ul
            };

            SourceFrustumPlanes[0] = new float4(1f, 0f, 1f, 0f);
            SourceFrustumPlanes[1] = new float4(-1f, 0f, 1f, 0f);
            SourceFrustumPlanes[2] = new float4(0f, 1f, 1f, 0f);
            SourceFrustumPlanes[3] = new float4(0f, -1f, 1f, 0f);
            SourceFrustumPlanes[4] = new float4(0f, 0f, 1f, 0.1f);
            SourceFrustumPlanes[5] = new float4(0f, 0f, -1f, 500f);
            for (int i = 0; i < SqueezedFrustumPlanes.Length; i++)
                SqueezedFrustumPlanes[i] = SourceFrustumPlanes[i];

            VisibleCountOut[0] = 0;
            IndirectDrawArgs[0] = default;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!EditorShowSorting || !MockVisibleInstances.IsCreated)
                return;

            int count = math.min(math.min(_lastSortedCount, MockVisibleInstances.Length), 128);
            if (count <= 1)
                return;

            Gizmos.color = new Color(0.2f, 0.95f, 0.55f, 0.9f);
            Vector3 previous = ToVector3(MockVisibleInstances[0].CameraRelativePositionRadius.xyz);
            for (int i = 1; i < count; i++)
            {
                Vector3 current = ToVector3(MockVisibleInstances[i].CameraRelativePositionRadius.xyz);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
#endif
    }
}

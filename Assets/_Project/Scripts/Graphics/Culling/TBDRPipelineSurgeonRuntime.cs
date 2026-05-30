using System;
using System.Diagnostics;
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
        private const string NativeMemoryOwner = nameof(TBDRPipelineSurgeonRuntime);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

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

        private readonly VertexBudgetVaultOwner _vaultOwner = new VertexBudgetVaultOwner();
        private readonly RuntimeBufferSet _buffers = new RuntimeBufferSet();

        public ref TBDRVertexBudgetVault Vault => ref _vaultOwner.Vault;

        public NativeArray<PoiTransformDTO>.ReadOnly MockVisibleInstances =>
            _buffers.MockVisibleInstances.IsCreated ? _buffers.MockVisibleInstances.AsReadOnly() : default;
        public NativeArray<PoiTransformDTO>.ReadOnly SortScratch =>
            _buffers.SortScratch.IsCreated ? _buffers.SortScratch.AsReadOnly() : default;
        public NativeArray<uint>.ReadOnly MeshVertexCounts =>
            _buffers.MeshVertexCounts.IsCreated ? _buffers.MeshVertexCounts.AsReadOnly() : default;
        public NativeArray<int>.ReadOnly RadixHistogram =>
            _buffers.RadixHistogram.IsCreated ? _buffers.RadixHistogram.AsReadOnly() : default;
        public NativeArray<int>.ReadOnly VisibleCountOut =>
            _buffers.VisibleCountOut.IsCreated ? _buffers.VisibleCountOut.AsReadOnly() : default;
        public NativeArray<TBDRMockQualityWeightSignal>.ReadOnly MockQualitySignal =>
            _buffers.MockQualitySignal.IsCreated ? _buffers.MockQualitySignal.AsReadOnly() : default;
        public NativeArray<MockCameraMatrix>.ReadOnly MockCamera =>
            _buffers.MockCamera.IsCreated ? _buffers.MockCamera.AsReadOnly() : default;
        public NativeArray<float4>.ReadOnly SourceFrustumPlanes =>
            _buffers.SourceFrustumPlanes.IsCreated ? _buffers.SourceFrustumPlanes.AsReadOnly() : default;
        public NativeArray<float4>.ReadOnly SqueezedFrustumPlanes =>
            _buffers.SqueezedFrustumPlanes.IsCreated ? _buffers.SqueezedFrustumPlanes.AsReadOnly() : default;
        public NativeArray<int>.ReadOnly HzbVisibilityMask =>
            _buffers.HzbVisibilityMask.IsCreated ? _buffers.HzbVisibilityMask.AsReadOnly() : default;
        public NativeArray<TBDRIndirectDrawArgsDTO>.ReadOnly IndirectDrawArgs =>
            _buffers.IndirectDrawArgs.IsCreated ? _buffers.IndirectDrawArgs.AsReadOnly() : default;

        private sealed class RuntimeBufferSet
        {
            public NativeArray<PoiTransformDTO> MockVisibleInstances;
            public NativeArray<PoiTransformDTO> SortScratch;
            public NativeArray<uint> MeshVertexCounts;
            public NativeArray<int> RadixHistogram;
            public NativeArray<int> VisibleCountOut;
            public NativeArray<TBDRMockQualityWeightSignal> MockQualitySignal;
            public NativeArray<MockCameraMatrix> MockCamera;
            public NativeArray<float4> SourceFrustumPlanes;
            public NativeArray<float4> SqueezedFrustumPlanes;
            public NativeArray<int> HzbVisibilityMask;
            public NativeArray<TBDRIndirectDrawArgsDTO> IndirectDrawArgs;

            public void Clear()
            {
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
            }
        }

        private sealed class VertexBudgetVaultOwner
        {
            public TBDRVertexBudgetVault Vault;
        }

        private IDataVault _dataVault;
        private VaultGenerationHandle<PoiTransformDTO> _mockVisibleHandle;
        private VaultGenerationHandle<PoiTransformDTO> _sortScratchHandle;
        private VaultGenerationHandle<uint> _meshVertexCountsHandle;
        private VaultGenerationHandle<int> _radixHistogramHandle;
        private VaultGenerationHandle<int> _visibleCountOutHandle;
        private VaultGenerationHandle<TBDRMockQualityWeightSignal> _mockQualitySignalHandle;
        private VaultGenerationHandle<MockCameraMatrix> _mockCameraHandle;
        private VaultGenerationHandle<float4> _sourceFrustumPlanesHandle;
        private VaultGenerationHandle<float4> _squeezedFrustumPlanesHandle;
        private VaultGenerationHandle<int> _hzbVisibilityMaskHandle;
        private VaultGenerationHandle<TBDRIndirectDrawArgsDTO> _indirectDrawArgsHandle;
        private TBDRHardwareBudgetLimits _limits;
        private TBDRPipelineTelemetryRecorder _telemetry;
#if UNITY_EDITOR
        private TBDRGpuBudgetCsvIngestor _csvIngestor;
#endif
        private int _lastSortedCount;
        private uint _lastFrame;
        private float _lastSortMs;
        private string _resolvedGpuBudgetCsvPath;
        private bool _initialized;
        private bool _usesVaultStorage;
        private bool _csvPathDirty = true;
        private bool _isMobileTbdrCold;
        private bool _shouldRunEarlyZRadixSortCold;

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
            CacheHardwarePipelineSnapshotCold();

            ref TBDRVertexBudgetVault vault = ref Vault;
            vault = new TBDRVertexBudgetVault(_dataVault, 1, 1, 4, TelemetryCapacity);
            vault.ApplyHardLimits(in _limits);
            AllocateNativeMockBuffers(math.max(1, _sortCapacity), _dataVault);
            _telemetry = new TBDRPipelineTelemetryRecorder();
            _telemetry.BindExternalRing(vault.TelemetryRing);
            _telemetry.EnsureCreated();
#if UNITY_EDITOR
            _csvIngestor = new TBDRGpuBudgetCsvIngestor();
#endif
            TBDRComputeDispatchLimiter.Boot();
#if UNITY_EDITOR
            ResolveGpuBudgetCsvPath();
#endif
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

#if UNITY_EDITOR
        public bool PollBudgetCsvOverride()
        {
            Initialize();
            string path = ResolveGpuBudgetCsvPath();
            ref TBDRVertexBudgetVault vault = ref Vault;
            bool applied = _csvIngestor.Poll(path, ref vault);
            if (applied)
            {
                _hardVertexCap = _csvIngestor.LastParsedVertexCap;
                _transparentQuadLimit = _csvIngestor.LastParsedTransparentQuadLimit;
                _frustumSqueezeAngle = _csvIngestor.LastParsedFrustumSqueezeDegrees;
                PushShaderBudgetGlobals();
            }

            return applied;
        }
#endif

        public unsafe JobHandle ScheduleTBDRProtectionPass(int requestedInstanceCount, JobHandle dependency)
        {
            return ScheduleTBDRProtectionPass(requestedInstanceCount, Hecton8.Core.SystemDispatcher.CurrentFrameId, dependency);
        }

        public unsafe JobHandle ScheduleTBDRProtectionPass(int requestedInstanceCount, uint simulationFrame, JobHandle dependency)
        {
            Initialize();
            int instanceCount = math.min(math.max(1, requestedInstanceCount), _buffers.MockVisibleInstances.Length);
            _lastFrame = simulationFrame;

            JobHandle handle = new MockQualityWeightJob
            {
                QualitySignal = _buffers.MockQualitySignal,
                Frame = _lastFrame,
                SeedSalt = 0x9E3779B9u
            }.Schedule(dependency);

            handle = new DearLieFrustumSqueezeJob
            {
                BudgetPtr = Vault.BudgetPtr(0),
                QualitySignal = _buffers.MockQualitySignal,
                CameraData = _buffers.MockCamera,
                SourcePlanes = _buffers.SourceFrustumPlanes,
                SqueezedPlanes = _buffers.SqueezedFrustumPlanes,
                MobileBaseVertexCap = _hardVertexCap,
                MaxSqueezeDegrees = _frustumSqueezeAngle
            }.Schedule(handle);

            handle = new DearLieFrustumVisibilityJob
            {
                Instances = _buffers.MockVisibleInstances,
                FrustumPlanes = _buffers.SqueezedFrustumPlanes,
                VisibilityMask = _buffers.HzbVisibilityMask,
                Count = instanceCount
            }.Schedule(instanceCount, 64, handle);

            handle = new BuildDistanceSortKeysJob
            {
                Instances = _buffers.MockVisibleInstances
            }.Schedule(instanceCount, 64, handle);

            if (_shouldRunEarlyZRadixSortCold)
            {
                handle = new EarlyZRadixSortJob
                {
                    Source = _buffers.MockVisibleInstances,
                    Scratch = _buffers.SortScratch,
                    Histogram = _buffers.RadixHistogram,
                    Count = instanceCount
                }.Schedule(handle);
            }

            handle = new VertexBudgetJob
            {
                BudgetPtr = Vault.BudgetPtr(0),
                TileWarningPtr = Vault.WarningPtr(0),
                MeshVertexCounts = _buffers.MeshVertexCounts,
                VisibilityMask = default,
                VisibleInstances = _buffers.MockVisibleInstances,
                VisibleCountOut = _buffers.VisibleCountOut,
                SourceCount = instanceCount
            }.Schedule(handle);

            handle = new BuildIndirectDrawArgsJob
            {
                VisibleCountOut = _buffers.VisibleCountOut,
                ArgsOut = _buffers.IndirectDrawArgs,
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
            _lastSortedCount = _buffers.VisibleCountOut.IsCreated && _buffers.VisibleCountOut.Length > 0 ? _buffers.VisibleCountOut[0] : 0;

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
                _isMobileTbdrCold ? 1u : 0u);
            PushShaderBudgetGlobals(in budget, in warning);
        }

        public unsafe bool RunMockPipelineOnce(int requestedInstanceCount)
        {
            long sortStart = Stopwatch.GetTimestamp();
            JobHandle handle = ScheduleTBDRProtectionPass(requestedInstanceCount, default);
            // COLD/EDITOR SYNC FACADE: tuner-only mock pipeline proof, not dispatcher frame cadence.
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            CommitCompletedProtectionPass((float)((Stopwatch.GetTimestamp() - sortStart) * 1000d / Stopwatch.Frequency));
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
            if (_buffers.MockQualitySignal.IsCreated && _buffers.MockQualitySignal.Length > 0)
            {
                float quality = _buffers.MockQualitySignal[0].GlobalQualityWeight;
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

        private void CacheHardwarePipelineSnapshotCold()
        {
            _isMobileTbdrCold = TBDRHardwarePipelineSwitch.IsMobileTBDR();
            _shouldRunEarlyZRadixSortCold = _isMobileTbdrCold || TBDRHardwarePipelineSwitch.ShouldRunEarlyZRadixSort();
        }

        public void Dispose()
        {
            if (!_initialized)
                return;

            if (_telemetry != null)
                _telemetry.Dispose();
            _telemetry = null;

            if (_usesVaultStorage)
                ReleaseVaultBuffers();
            else
                DisposeFallbackBuffers();

            _buffers.Clear();
            ResetVaultHandles();
            Vault.Dispose(_dataVault);
#if UNITY_EDITOR
            _csvIngestor = null;
#endif
            _initialized = false;
            _usesVaultStorage = false;
            _dataVault = null;
            _resolvedGpuBudgetCsvPath = null;
            _csvPathDirty = true;
            _lastSortedCount = 0;
            _lastSortMs = 0f;
            _isMobileTbdrCold = false;
            _shouldRunEarlyZRadixSortCold = false;
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
                _usesVaultStorage = TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _mockVisibleHandle, TBDRBufferIds.MockVisibleInstances, capacity, NativeArrayOptions.UninitializedMemory, out _buffers.MockVisibleInstances) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _sortScratchHandle, TBDRBufferIds.SortScratch, capacity, NativeArrayOptions.UninitializedMemory, out _buffers.SortScratch) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _meshVertexCountsHandle, TBDRBufferIds.MeshVertexCounts, 256, NativeArrayOptions.UninitializedMemory, out _buffers.MeshVertexCounts) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _radixHistogramHandle, TBDRBufferIds.RadixHistogram, RadixHistogramCapacity, NativeArrayOptions.UninitializedMemory, out _buffers.RadixHistogram) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _visibleCountOutHandle, TBDRBufferIds.VisibleCountOut, 1, NativeArrayOptions.UninitializedMemory, out _buffers.VisibleCountOut) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _mockQualitySignalHandle, TBDRBufferIds.MockQualitySignal, 1, NativeArrayOptions.UninitializedMemory, out _buffers.MockQualitySignal) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _mockCameraHandle, TBDRBufferIds.MockCamera, 1, NativeArrayOptions.UninitializedMemory, out _buffers.MockCamera) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _sourceFrustumPlanesHandle, TBDRBufferIds.SourceFrustumPlanes, 6, NativeArrayOptions.UninitializedMemory, out _buffers.SourceFrustumPlanes) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _squeezedFrustumPlanesHandle, TBDRBufferIds.SqueezedFrustumPlanes, 6, NativeArrayOptions.UninitializedMemory, out _buffers.SqueezedFrustumPlanes) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _hzbVisibilityMaskHandle, TBDRBufferIds.HzbVisibilityMask, capacity, NativeArrayOptions.UninitializedMemory, out _buffers.HzbVisibilityMask) &&
                                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref _indirectDrawArgsHandle, TBDRBufferIds.IndirectDrawArgs, 1, NativeArrayOptions.UninitializedMemory, out _buffers.IndirectDrawArgs);
            }

            if (!_usesVaultStorage)
            {
                _buffers.MockVisibleInstances = new NativeArray<PoiTransformDTO>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                _buffers.SortScratch = new NativeArray<PoiTransformDTO>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                _buffers.MeshVertexCounts = new NativeArray<uint>(256, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                _buffers.RadixHistogram = new NativeArray<int>(RadixHistogramCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                _buffers.VisibleCountOut = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                _buffers.MockQualitySignal = new NativeArray<TBDRMockQualityWeightSignal>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                _buffers.MockCamera = new NativeArray<MockCameraMatrix>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                _buffers.SourceFrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                _buffers.SqueezedFrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                _buffers.HzbVisibilityMask = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                _buffers.IndirectDrawArgs = new NativeArray<TBDRIndirectDrawArgsDTO>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                RegisterFallbackBuffers();
            }
        }

        private void RegisterFallbackBuffers()
        {
            RegisterFallbackNativeArray(_buffers.MockVisibleInstances, nameof(RuntimeBufferSet.MockVisibleInstances));
            RegisterFallbackNativeArray(_buffers.SortScratch, nameof(RuntimeBufferSet.SortScratch));
            RegisterFallbackNativeArray(_buffers.MeshVertexCounts, nameof(RuntimeBufferSet.MeshVertexCounts));
            RegisterFallbackNativeArray(_buffers.RadixHistogram, nameof(RuntimeBufferSet.RadixHistogram));
            RegisterFallbackNativeArray(_buffers.VisibleCountOut, nameof(RuntimeBufferSet.VisibleCountOut));
            RegisterFallbackNativeArray(_buffers.MockQualitySignal, nameof(RuntimeBufferSet.MockQualitySignal));
            RegisterFallbackNativeArray(_buffers.MockCamera, nameof(RuntimeBufferSet.MockCamera));
            RegisterFallbackNativeArray(_buffers.SourceFrustumPlanes, nameof(RuntimeBufferSet.SourceFrustumPlanes));
            RegisterFallbackNativeArray(_buffers.SqueezedFrustumPlanes, nameof(RuntimeBufferSet.SqueezedFrustumPlanes));
            RegisterFallbackNativeArray(_buffers.HzbVisibilityMask, nameof(RuntimeBufferSet.HzbVisibilityMask));
            RegisterFallbackNativeArray(_buffers.IndirectDrawArgs, nameof(RuntimeBufferSet.IndirectDrawArgs));
        }

        private void DisposeFallbackBuffers()
        {
            DisposeFallbackNativeArray(ref _buffers.MockVisibleInstances);
            DisposeFallbackNativeArray(ref _buffers.SortScratch);
            DisposeFallbackNativeArray(ref _buffers.MeshVertexCounts);
            DisposeFallbackNativeArray(ref _buffers.RadixHistogram);
            DisposeFallbackNativeArray(ref _buffers.VisibleCountOut);
            DisposeFallbackNativeArray(ref _buffers.MockQualitySignal);
            DisposeFallbackNativeArray(ref _buffers.MockCamera);
            DisposeFallbackNativeArray(ref _buffers.SourceFrustumPlanes);
            DisposeFallbackNativeArray(ref _buffers.SqueezedFrustumPlanes);
            DisposeFallbackNativeArray(ref _buffers.HzbVisibilityMask);
            DisposeFallbackNativeArray(ref _buffers.IndirectDrawArgs);
        }

        private void ReleaseVaultBuffers()
        {
            ReleaseVaultBuffer(ref _mockVisibleHandle);
            ReleaseVaultBuffer(ref _sortScratchHandle);
            ReleaseVaultBuffer(ref _meshVertexCountsHandle);
            ReleaseVaultBuffer(ref _radixHistogramHandle);
            ReleaseVaultBuffer(ref _visibleCountOutHandle);
            ReleaseVaultBuffer(ref _mockQualitySignalHandle);
            ReleaseVaultBuffer(ref _mockCameraHandle);
            ReleaseVaultBuffer(ref _sourceFrustumPlanesHandle);
            ReleaseVaultBuffer(ref _squeezedFrustumPlanesHandle);
            ReleaseVaultBuffer(ref _hzbVisibilityMaskHandle);
            ReleaseVaultBuffer(ref _indirectDrawArgsHandle);
        }

        private void ReleaseVaultBuffer<T>(ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (_dataVault != null && handle.BufferID != 0u)
                _dataVault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static void RegisterFallbackNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void DisposeFallbackNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
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
            for (int i = 0; i < _buffers.MeshVertexCounts.Length; i++)
                _buffers.MeshVertexCounts[i] = (uint)(300 + (i & 15) * 64);

            float initialQuality = math.isfinite(_globalQualityWeight) ? math.saturate(_globalQualityWeight) : 1f;
            _buffers.MockQualitySignal[0] = new TBDRMockQualityWeightSignal
            {
                GlobalQualityWeight = initialQuality,
                Frame = 0u,
                Seed = 0x45A11CEu,
                _pad0 = 0u
            };

            for (int i = 0; i < _buffers.MockVisibleInstances.Length; i++)
            {
                float z = 4f + (i % 512) * 0.45f;
                float x = ((i * 37) & 127) - 63.5f;
                float y = ((i * 19) & 63) - 31.5f;
                _buffers.MockVisibleInstances[i] = new PoiTransformDTO
                {
                    LocalToWorld = float4x4.Translate(new float3(x, y, z)),
                    CameraRelativePositionRadius = new float4(x, y, z, 1f),
                    MeshId = (uint)(i & 255),
                    InstanceId = (uint)i,
                    VertexCount = _buffers.MeshVertexCounts[i & 255],
                    DistanceSq = x * x + y * y + z * z,
                    SortKey = math.asuint(x * x + y * y + z * z),
                    Flags = 0u,
                    _pad0 = 0ul,
                    _pad1 = 0ul,
                    _pad2 = 0ul
                };

                _buffers.HzbVisibilityMask[i] = 1;
            }

            _buffers.MockCamera[0] = new MockCameraMatrix
            {
                ViewProjection = float4x4.identity,
                PositionRadius = new float4(0f, 0f, 0f, 1f),
                ForwardFov = new float4(0f, 0f, 1f, 90f),
                Frame = 0u,
                Flags = 0u,
                _pad0 = 0ul
            };

            _buffers.SourceFrustumPlanes[0] = new float4(1f, 0f, 1f, 0f);
            _buffers.SourceFrustumPlanes[1] = new float4(-1f, 0f, 1f, 0f);
            _buffers.SourceFrustumPlanes[2] = new float4(0f, 1f, 1f, 0f);
            _buffers.SourceFrustumPlanes[3] = new float4(0f, -1f, 1f, 0f);
            _buffers.SourceFrustumPlanes[4] = new float4(0f, 0f, 1f, 0.1f);
            _buffers.SourceFrustumPlanes[5] = new float4(0f, 0f, -1f, 500f);
            for (int i = 0; i < _buffers.SqueezedFrustumPlanes.Length; i++)
                _buffers.SqueezedFrustumPlanes[i] = _buffers.SourceFrustumPlanes[i];

            _buffers.VisibleCountOut[0] = 0;
            _buffers.IndirectDrawArgs[0] = default;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!EditorShowSorting || !_buffers.MockVisibleInstances.IsCreated)
                return;

            int count = math.min(math.min(_lastSortedCount, _buffers.MockVisibleInstances.Length), 128);
            if (count <= 1)
                return;

            Gizmos.color = new Color(0.2f, 0.95f, 0.55f, 0.9f);
            Vector3 previous = ToVector3(_buffers.MockVisibleInstances[0].CameraRelativePositionRadius.xyz);
            for (int i = 1; i < count; i++)
            {
                Vector3 current = ToVector3(_buffers.MockVisibleInstances[i].CameraRelativePositionRadius.xyz);
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

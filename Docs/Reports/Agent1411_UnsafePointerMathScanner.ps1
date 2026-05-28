param(
    [string]$Root = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

$targets = @(
    "Assets/_Project/Scripts/Core/SystemDispatcher.cs",
    "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs",
    "Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs",
    "Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs",
    "Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs",
    "Assets/_Project/Scripts/World/VegetationMemoryPool.cs",
    "Assets/_Project/Scripts/World/PcieBandwidthGuard1411SelfTest.cs",
    "Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs",
    "Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs",
    "Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs",
    "Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs",
    "Assets/_Project/Scripts/HectonFluidEngine.cs",
    "Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs",
    "Assets/_Project/Scripts/Construction/DroneFleetManager.cs",
    "Assets/_Project/Scripts/HectonBoidController.cs",
    "Assets/_Project/Scripts/World/GPUScatterDirector.cs",
    "Assets/_Project/Scripts/World/AbyssalThermalManager.cs"
)

function Read-Target([string]$relativePath) {
    $path = Join-Path $Root $relativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing target: $relativePath"
    }

    return Get-Content -Raw -LiteralPath $path
}

$systemDispatcher = Read-Target $targets[0]
$renderer = Read-Target $targets[1]
$contracts = Read-Target $targets[2]
$mapMagicBridge = Read-Target $targets[3]
$residencyDirector = Read-Target $targets[4]
$vegetationMemoryPool = Read-Target $targets[5]
$selfTest = Read-Target $targets[6]
$carveDebris = Read-Target $targets[7]
$instanceCulling = Read-Target $targets[8]
$gpuScatter = Read-Target $targets[9]
$vehicleCockpit = Read-Target $targets[10]
$fluidEngine = Read-Target $targets[11]
$asyncBuoyancyReadback = Read-Target $targets[12]
$droneFleet = Read-Target $targets[13]
$boidController = Read-Target $targets[14]
$gpuScatterDirector = Read-Target $targets[15]
$abyssalThermal = Read-Target $targets[16]

$checks = [ordered]@{
    UploadRangeMethodPresent = $systemDispatcher.Contains("private static void UploadNativeArrayRange<T>")
    MarkDirtyPageRangePresent = $systemDispatcher.Contains("public static void MarkDirtyPageRange(NativeArray<byte> dirtyPages")
    OffsetLockPresent = $systemDispatcher.Contains("destination.LockBufferForWrite<T>(startIndex, count)")
    LongByteOffsetPresent = $systemDispatcher.Contains("((long)startIndex * stride)")
    GuardedMemcpyPresent = $systemDispatcher.Contains("UnsafeMemoryCopyGuard.TryMemCpy")
    FinallyUnlockPresent = $systemDispatcher.Contains("finally") -and $systemDispatcher.Contains("destination.UnlockBufferAfterWrite<T>(count)")
    DirtyStatsUsesSafeCount = $systemDispatcher.Contains("ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count)")
    FirstDirtyPageByteResolverPresent = $systemDispatcher.Contains("ResolveFirstDirtyPageBytes<T>")
    DirtyTokenFieldsPresent = $contracts.Contains("MatrixDirtyPages") -and $contracts.Contains("InstanceDataDirtyPages") -and $contracts.Contains("ContentRevision")
    RendererSkipGatePresent = $renderer.Contains("CanReuseNativeUpload(in readBuffer)")
    RendererPartialUploadPresent = $renderer.Contains("BindInstanceNativeDirtyPages")
    RendererBudgetUsesQuality = $renderer.Contains("ResolveNativeUploadBudgetBytes") -and $renderer.Contains("_cachedQualityWeight01")
    RendererCombinedBudgetGatePresent = $renderer.Contains("remainingBudgetBytes >= dataFirstDirtyPageBytes") -and $renderer.Contains("dataDeferredByBudget")
    RendererAbsorbsSourceDirtyOnce = $renderer.Contains("HasAbsorbedNativeSourceDirtyPages(in readBuffer)") -and $renderer.Contains("RecordNativeSourceDirtyPagesAbsorbed(in readBuffer)")
    RendererBacklogBlocksFullFallback = $renderer.Contains("!HasUploadedWriteDirtyPageBacklog(instanceCount)") -and $renderer.Contains("HasUploadedWriteDirtyPageBacklog(readBuffer.InstanceCount)")
    MapMagicDirtyPageIdsPresent = $mapMagicBridge.Contains("SurfaceAggregateFrontMatrixDirtyPagesId = (BufferID)74607") -and $mapMagicBridge.Contains("UnderwaterAggregateBackMetadataDirtyPagesId = (BufferID)74614")
    MapMagicDirtyPageStoragePresent = $mapMagicBridge.Contains("EnsureAggregateDirtyPageBuffer(ref buffers.MatrixDirtyPagesHandle") -and $mapMagicBridge.Contains("EnsureAggregateDirtyPageBuffer(ref buffers.MetadataDirtyPagesHandle")
    MapMagicDirtyPageLocksPresent = $mapMagicBridge.Contains("TryAcquireActiveAggregateDirtyPagesForWrite") -and $mapMagicBridge.Contains("ReleaseActiveAggregateDirtyPageWriteLocks")
    MapMagicDirtyPageReleaseFinallyPresent = $mapMagicBridge.Contains("finally") -and $mapMagicBridge.Contains("ReleaseWriteLock(in buffers.MetadataDirtyPagesHandle") -and $mapMagicBridge.Contains("ReleaseWriteLock(in buffers.MatrixDirtyPagesHandle")
    MapMagicBufferIndexResolverPresent = $mapMagicBridge.Contains("ResolveSurfaceAggregateMatrixBufferId(int bufferIndex)") -and $mapMagicBridge.Contains("ResolveUnderwaterAggregateMatrixBufferId(int bufferIndex)") -and $mapMagicBridge.Contains("ResolveSurfaceAggregateMatrixDirtyPageBufferId(int bufferIndex)") -and $mapMagicBridge.Contains("ResolveUnderwaterAggregateMetadataDirtyPageBufferId(int bufferIndex)")
    MapMagicReadTokenPublishesDirtyPages = $mapMagicBridge.Contains("TryReadAggregateBuffer(in buffers.MatrixDirtyPagesHandle, dirtyPageCount, out matrixDirtyPages)") -and $mapMagicBridge.Contains("TryReadAggregateBuffer(in buffers.MetadataDirtyPagesHandle, dirtyPageCount, out metadataDirtyPages)") -and $mapMagicBridge.Contains("ActiveAggregateDirtyPageSize);")
    ResidencyUsesBufferIndexSpecificBackIds = $residencyDirector.Contains("ResolveSurfaceAggregateMatrixBufferId(_surfaceBackBufferIndex)") -and $residencyDirector.Contains("ResolveUnderwaterAggregateMatrixBufferId(_underwaterBackBufferIndex)")
    ResidencyClearsAndMarksDirtyRanges = $residencyDirector.Contains("ClearDirtyPages(surfaceMatrixDirtyPages") -and $residencyDirector.Contains("MarkDirtyPageRange(") -and $residencyDirector.Contains("ReleaseActiveAggregateDirtyPageWriteLocks")
    VegetationPoolTracksDirtyHandles = $vegetationMemoryPool.Contains("VaultGenerationHandle<byte> MatrixDirtyPagesHandle") -and $vegetationMemoryPool.Contains("VaultGenerationHandle<byte> MetadataDirtyPagesHandle") -and $vegetationMemoryPool.Contains("int DirtyPageCapacity")
    SelfTestSinglePagePresent = $selfTest.Contains("AssertSingleDirtyPageForLastMatrix")
    SelfTestCombinedBudgetPresent = $selfTest.Contains("AssertCombinedMatrixAndMetadataBudgetDoesNotOvershoot")
    SelfTestPingPongPresent = $selfTest.Contains("AssertDoubleBufferBacklogFuzzer")
    CarveDebrisUsesLockBuffer = $carveDebris.Contains("destination.LockBufferForWrite<T>(safeStart, safeCount)")
    CarveDebrisUsesLongByteOffset = $carveDebris.Contains("((long)safeStart * stride)")
    CarveDebrisUsesGuardedMemcpy = $carveDebris.Contains("UnsafeMemoryCopyGuard.TryMemCpy")
    CarveDebrisFinallyUnlocks = $carveDebris.Contains("finally") -and $carveDebris.Contains("destination.UnlockBufferAfterWrite<T>(safeCount)")
    CarveDebrisBuffersLockCapable = $carveDebris.Contains("GraphicsBufferUploadUtility.CreateStructuredLockBuffer<T>")
    InstanceCullingIndirectArgsLockCapable = $instanceCulling.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw") -and $instanceCulling.Contains("GraphicsBuffer.UsageFlags.LockBufferForWrite")
    InstanceCullingIndirectArgsMappedUpload = $instanceCulling.Contains("GraphicsBufferUploadUtility.UploadArray(_indirectArgsBuffer, _indirectArgsUpload, IndirectArgsCount)")
    GpuScatterIndirectArgsLockCapable = $gpuScatter.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw") -and $gpuScatter.Contains("GraphicsBuffer.UsageFlags.LockBufferForWrite")
    GpuScatterIndirectArgsMappedUpload = $gpuScatter.Contains("GraphicsBufferUploadUtility.UploadArray(_argsBuffer, _indirectArgsUpload, 1)")
    VehicleCockpitDamagePointBufferLockCapable = $vehicleCockpit.Contains("GraphicsBuffer.Target.Append") -and $vehicleCockpit.Contains("GraphicsBuffer.UsageFlags.LockBufferForWrite") -and $vehicleCockpit.Contains("MaxDamageHologramPoints")
    VehicleCockpitDamageArgsLockCapable = $vehicleCockpit.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw") -and $vehicleCockpit.Contains("GraphicsBuffer.UsageFlags.LockBufferForWrite") -and $vehicleCockpit.Contains("GraphicsBuffer.IndirectDrawIndexedArgs.size")
    VehicleCockpitDamageFallbackMappedUpload = $vehicleCockpit.Contains("GraphicsBufferUploadUtility.UploadArray(_damagePointBuffer, _damageFallbackPoint, FallbackDamageWarningPoints)")
    VehicleCockpitDamageArgsMappedUpload = $vehicleCockpit.Contains("GraphicsBufferUploadUtility.UploadArray(_damageArgsBuffer, _damageHologramArgsUpload, 1)")
    FluidAdvectionDirtyPageIdsPresent = $fluidEngine.Contains("FluidAdvectedSiltDirtyPagesBufferId = (BufferID)1322041") -and $fluidEngine.Contains("FluidAdvectedBubbleDirtyPagesBufferId = (BufferID)1322042") -and $fluidEngine.Contains("FluidAdvectedDebrisDirtyPagesBufferId = (BufferID)1322043")
    FluidAdvectionBuffersLockCapable = $fluidEngine.Contains("CreateStructuredLockBuffer<AdvectedSilt>(MaxAdvectedSiltCount)") -and $fluidEngine.Contains("CreateStructuredLockBuffer<AdvectedBubble>(MaxAdvectedBubbleCount)") -and $fluidEngine.Contains("CreateStructuredLockBuffer<AdvectedDebris>(MaxAdvectedDebrisCount)")
    FluidAdvectionMappedDirtyUpload = $fluidEngine.Contains("FlushFluidAdvectionDirtyLane") -and $fluidEngine.Contains("GraphicsBufferUploadUtility.UploadNativeArrayDirtyPages") -and $fluidEngine.Contains("clearUploadedPages: false") -and $fluidEngine.Contains("clearUploadedPages: true")
    FluidAdvectionDirtyLocksFinally = $fluidEngine.Contains("MarkFluidAdvectionDirtyPage") -and $fluidEngine.Contains("TryAcquireWriteLock(out NativeArray<byte> dirtyPages)") -and $fluidEngine.Contains("dirtyPagesHandle.ReleaseWriteLock()")
    FluidAdvectionUsesContinuousQualityBudget = $fluidEngine.Contains("ResolveFluidAdvectionUploadBudgetBytes") -and $fluidEngine.Contains("SmoothFluidAdvectionQuality(ResolveFluidAdvectionQualityWeight())") -and $fluidEngine.Contains("FluidAdvectionMinUploadBudgetBytes") -and $fluidEngine.Contains("FluidAdvectionMaxUploadBudgetBytes")
    FluidAdvectionNoSetDataFallbackCall = -not $fluidEngine.Contains("UploadNativeArraySetData")
    AsyncBuoyancyRequestBuffersLockCapable = $asyncBuoyancyReadback.Contains("CreateStructuredLockBuffer<ReadbackRequestDTO>(AsyncBuoyancyReadbackConstants.RequestCapacity)") -and $asyncBuoyancyReadback.Contains("GraphicsBufferUploadUtility.UploadNativeArray(requestBuffer, requests, _dispatchRequestCount)")
    DroneProceduralArgsLockCapable = $droneFleet.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw") -and $droneFleet.Contains("GraphicsBuffer.UsageFlags.LockBufferForWrite") -and $droneFleet.Contains("GraphicsBufferUploadUtility.UploadNativeArray(s_DroneProceduralArgsBuffer, proceduralArgs, 1)")
    BoidPingBuffersLockCapable = $boidController.Contains("CreateStructuredLockBuffer<BoidData>(boidCount)")
    BoidIndirectArgsLockCapable = $boidController.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw") -and $boidController.Contains("GraphicsBuffer.UsageFlags.LockBufferForWrite") -and $boidController.Contains("GraphicsBufferUploadUtility.UploadArray(_visibleIndirectArgsBuffer, _visibleIndirectArgsUpload, 1)")
    BoidSpawnResetMappedUpload = $boidController.Contains("GraphicsBufferUploadUtility.UploadArray(_boidsBufferA, _spawnUploadBuffer, safeCount)") -and $boidController.Contains("GraphicsBufferUploadUtility.UploadArray(_boidsBufferB, _spawnUploadBuffer, safeCount)")
    GpuScatterDirectorVisibilityCacheMappedClear = $gpuScatterDirector.Contains("CreateStructuredLockBuffer<uint>(requiredCapacity)") -and $gpuScatterDirector.Contains("GraphicsBufferUploadUtility.UploadArray(_visibilityCacheBuffer, _visibilityCacheClearUpload, requiredCapacity)")
    GpuScatterDirectorArgsLockCapable = $gpuScatterDirector.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw") -and $gpuScatterDirector.Contains("GraphicsBuffer.UsageFlags.LockBufferForWrite") -and $gpuScatterDirector.Contains("GraphicsBufferUploadUtility.UploadArray(_argsBuffer, _argsUpload, 1)")
    VegetationCullTelemetryMappedClear = $renderer.Contains("_cullTelemetryCountersBuffer = new GraphicsBuffer(") -and $renderer.Contains("GraphicsBuffer.UsageFlags.LockBufferForWrite") -and $renderer.Contains("GraphicsBufferUploadUtility.UploadArray(") -and $renderer.Contains("_cullTelemetryClearPayload")
    AbyssalThermalSmokeBuffersLockCapable = $abyssalThermal.Contains("CreateStructuredLockBuffer<T>(safeCount)") -and $abyssalThermal.Contains("EnsureGpuWriteBuffer<AshParticleData>")
    AbyssalThermalParticleResetMappedUpload = $abyssalThermal.Contains("GraphicsBufferUploadUtility.UploadArray(_particleBufferA, _initialParticles, smokeParticleCount)") -and $abyssalThermal.Contains("GraphicsBufferUploadUtility.UploadArray(_particleBufferB, _initialParticles, smokeParticleCount)")
    AbyssalThermalExplicitGpuLayouts = $abyssalThermal.Contains("[StructLayout(LayoutKind.Explicit, Size = 40)]") -and $abyssalThermal.Contains("[FieldOffset(32)] public Vector2 Padding") -and $abyssalThermal.Contains("[StructLayout(LayoutKind.Explicit, Size = 48)]") -and $abyssalThermal.Contains("[FieldOffset(44)] public float VentIndex")
}

$fatal = @()
foreach ($entry in $checks.GetEnumerator()) {
    if (-not $entry.Value) {
        $fatal += $entry.Key
    }
}

$report = [ordered]@{
    agentId = "1411"
    status = if ($fatal.Count -eq 0) { "PASS" } else { "FAIL" }
    checkedAtUtc = [DateTime]::UtcNow.ToString("o")
    targets = $targets
    checks = $checks
    fatalFailures = $fatal
}

$outPath = Join-Path $Root "Docs/Reports/PCIE_BANDWIDTH_AST_AUDIT_1411.json"
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outPath -Encoding UTF8

if ($fatal.Count -gt 0) {
    throw ("FatalArchitectureException: " + ($fatal -join ", "))
}

Write-Output "PASS: Agent 1411 unsafe pointer math scanner wrote $outPath"

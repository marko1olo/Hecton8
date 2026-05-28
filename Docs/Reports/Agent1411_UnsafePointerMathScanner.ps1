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
    CopyDestinationFactoryPresent = $systemDispatcher.Contains("CreateStructuredCopyDestinationBuffer<T>")
    UploadStagingFactoryPresent = $systemDispatcher.Contains("CreateStructuredUploadStagingBuffer<T>")
    RawIndirectUploadStagingFactoryPresent = $systemDispatcher.Contains("CreateRawIndirectUploadStagingBuffer")
    WholeBufferStagingCopyPresent = $systemDispatcher.Contains("UploadArrayAndCopyWholeBuffer") -and $systemDispatcher.Contains("Graphics.CopyBuffer(uploadStaging, destination)")
    GpuWriteDirtyPageSetDataFallbackPresent = $systemDispatcher.Contains("UploadNativeArrayDirtyPagesSetData")
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
    CarveDebrisGpuWriteBuffersNotLockCapable = $carveDebris.Contains("CreateStructuredCopyDestinationBuffer<T>(math.max(1, count))") -and -not $carveDebris.Contains("destination.LockBufferForWrite<T>(safeStart, safeCount)")
    CarveDebrisUsesGpuWriteSetDataRange = $carveDebris.Contains("GraphicsBufferUploadUtility.UploadNativeArraySetDataRange(destination, source, safeStart, safeStart, safeCount)")
    InstanceCullingIndirectArgsNotLockCapable = $instanceCulling.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopyDestination") -and -not $instanceCulling.Contains("GraphicsBufferUploadUtility.UploadArray(_indirectArgsBuffer, _indirectArgsUpload, IndirectArgsCount)")
    InstanceCullingIndirectArgsStagingUpload = $instanceCulling.Contains("_indirectArgsUploadBuffer") -and $instanceCulling.Contains("UploadArrayAndCopyWholeBuffer(")
    GpuScatterIndirectArgsNotLockCapable = $gpuScatter.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopyDestination") -and -not $gpuScatter.Contains("GraphicsBufferUploadUtility.UploadArray(_argsBuffer, _indirectArgsUpload, 1)")
    GpuScatterIndirectArgsStagingUpload = $gpuScatter.Contains("_argsUploadBuffer") -and $gpuScatter.Contains("UploadArrayAndCopyWholeBuffer(_argsUploadBuffer, _argsBuffer, _indirectArgsUpload, 1)")
    VehicleCockpitDamagePointBufferNotLockCapable = $vehicleCockpit.Contains("GraphicsBuffer.Target.Append | GraphicsBuffer.Target.CopyDestination") -and $vehicleCockpit.Contains("_damagePointUploadBuffer")
    VehicleCockpitDamageArgsNotLockCapable = $vehicleCockpit.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopyDestination") -and $vehicleCockpit.Contains("_damageArgsUploadBuffer")
    VehicleCockpitDamageFallbackStagingUpload = $vehicleCockpit.Contains("UploadArrayAndCopyWholeBuffer(") -and $vehicleCockpit.Contains("_damagePointUploadBuffer")
    VehicleCockpitDamageArgsStagingUpload = $vehicleCockpit.Contains("UploadArrayAndCopyWholeBuffer(") -and $vehicleCockpit.Contains("_damageArgsUploadBuffer")
    FluidAdvectionDirtyPageIdsPresent = $fluidEngine.Contains("FluidAdvectedSiltDirtyPagesBufferId = (BufferID)1322041") -and $fluidEngine.Contains("FluidAdvectedBubbleDirtyPagesBufferId = (BufferID)1322042") -and $fluidEngine.Contains("FluidAdvectedDebrisDirtyPagesBufferId = (BufferID)1322043")
    FluidAdvectionBuffersNotLockCapable = $fluidEngine.Contains("CreateStructuredCopyDestinationBuffer<AdvectedSilt>(MaxAdvectedSiltCount)") -and $fluidEngine.Contains("CreateStructuredCopyDestinationBuffer<AdvectedBubble>(MaxAdvectedBubbleCount)") -and $fluidEngine.Contains("CreateStructuredCopyDestinationBuffer<AdvectedDebris>(MaxAdvectedDebrisCount)")
    FluidAdvectionDirtyPageSetDataFallback = $fluidEngine.Contains("FlushFluidAdvectionDirtyLane") -and $fluidEngine.Contains("GraphicsBufferUploadUtility.UploadNativeArrayDirtyPagesSetData") -and $fluidEngine.Contains("clearUploadedPages: false") -and $fluidEngine.Contains("clearUploadedPages: true")
    FluidAdvectionDirtyLocksFinally = $fluidEngine.Contains("MarkFluidAdvectionDirtyPage") -and $fluidEngine.Contains("TryAcquireWriteLock(out NativeArray<byte> dirtyPages)") -and $fluidEngine.Contains("dirtyPagesHandle.ReleaseWriteLock()")
    FluidAdvectionUsesContinuousQualityBudget = $fluidEngine.Contains("ResolveFluidAdvectionUploadBudgetBytes") -and $fluidEngine.Contains("SmoothFluidAdvectionQuality(ResolveFluidAdvectionQualityWeight())") -and $fluidEngine.Contains("FluidAdvectionMinUploadBudgetBytes") -and $fluidEngine.Contains("FluidAdvectionMaxUploadBudgetBytes")
    FluidAdvectionNoFullSetDataFallbackCall = -not $fluidEngine.Contains("UploadNativeArraySetData(")
    AsyncBuoyancyRequestBuffersNotLockCapable = $asyncBuoyancyReadback.Contains("CreateStructuredCopyDestinationBuffer<ReadbackRequestDTO>(AsyncBuoyancyReadbackConstants.RequestCapacity)") -and $asyncBuoyancyReadback.Contains("_requestUploadBuffer0")
    AsyncBuoyancyRequestStagingUpload = $asyncBuoyancyReadback.Contains("GraphicsBufferUploadUtility.UploadNativeArrayAndCopyWholeBuffer(") -and $asyncBuoyancyReadback.Contains("requestUploadBuffer")
    DroneProceduralArgsNotLockCapable = $droneFleet.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopyDestination") -and $droneFleet.Contains("s_DroneProceduralArgsUploadBuffer")
    DroneProceduralArgsStagingUpload = $droneFleet.Contains("GraphicsBufferUploadUtility.UploadNativeArrayAndCopyWholeBuffer(") -and $droneFleet.Contains("s_DroneProceduralArgsUploadBuffer")
    BoidPingBuffersNotLockCapable = $boidController.Contains("CreateStructuredCopyDestinationBuffer<BoidData>(boidCount)") -and $boidController.Contains("_boidUploadStagingBuffer")
    BoidIndirectArgsNotLockCapable = $boidController.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopyDestination") -and $boidController.Contains("_visibleIndirectArgsUploadBuffer")
    BoidSpawnResetStagingUpload = $boidController.Contains("UploadArrayAndCopyWholeBuffer(_boidUploadStagingBuffer, _boidsBufferA, _spawnUploadBuffer, safeCount)") -and $boidController.Contains("UploadArrayAndCopyWholeBuffer(_boidUploadStagingBuffer, _boidsBufferB, _spawnUploadBuffer, safeCount)")
    GpuScatterDirectorVisibilityCacheStagingClear = $gpuScatterDirector.Contains("CreateStructuredCopyDestinationBuffer<uint>(requiredCapacity)") -and $gpuScatterDirector.Contains("_visibilityCacheUploadBuffer") -and $gpuScatterDirector.Contains("UploadArrayAndCopyWholeBuffer(")
    GpuScatterDirectorArgsNotLockCapable = $gpuScatterDirector.Contains("GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.CopyDestination") -and $gpuScatterDirector.Contains("_argsUploadBuffer")
    VegetationCullTelemetryStagingClear = $renderer.Contains("CreateStructuredCopyDestinationBuffer<uint>(ScatterCullTelemetryCounterCount)") -and $renderer.Contains("_cullTelemetryCountersUploadBuffer") -and $renderer.Contains("UploadArrayAndCopyWholeBuffer(")
    AbyssalThermalSmokeBuffersNotLockCapable = $abyssalThermal.Contains("CreateStructuredCopyDestinationBuffer<T>(safeCount)") -and $abyssalThermal.Contains("EnsureGpuWriteBuffer<AshParticleData>")
    AbyssalThermalParticleResetStagingUpload = $abyssalThermal.Contains("UploadArrayAndCopyWholeBuffer(_particleUploadStagingBuffer, _particleBufferA, _initialParticles, smokeParticleCount)") -and $abyssalThermal.Contains("UploadArrayAndCopyWholeBuffer(_particleUploadStagingBuffer, _particleBufferB, _initialParticles, smokeParticleCount)")
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

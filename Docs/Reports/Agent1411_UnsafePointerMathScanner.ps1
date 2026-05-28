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
    "Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs"
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

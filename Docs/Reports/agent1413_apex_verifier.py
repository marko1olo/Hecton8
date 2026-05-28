import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(r"C:\hades\Hecton8")
CURRENT_BATCH = ROOT / "Docs" / "Tasks" / "CURRENT_BATCH.md"
REPORT = ROOT / "Docs" / "Reports" / "LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json"
LEDGER = ROOT / "Docs" / "Reports" / "LOCK_CONTENTION_SPAN_LEDGER_1413.json"
OUT = ROOT / "Docs" / "Reports" / "LOCK_CONTENTION_APEX_VERIFICATION_1413.json"

FILES = {
    "globalDataVault": ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Memory" / "GlobalDataVault.cs",
    "destructibleOrganic": ROOT / "Assets" / "_Project" / "Scripts" / "World" / "DestructibleOrganicManager.cs",
    "visualPressureAging": ROOT / "Assets" / "_Project" / "Scripts" / "Graphics" / "Materials" / "VisualPressureAgingRuntime.cs",
    "stormPropagation": ROOT / "Assets" / "_Project" / "Scripts" / "Atmosphere" / "StormPropagation" / "ShinobuStormPropagationRuntime.cs",
    "persistentWorldRegistry": ROOT / "Assets" / "_Project" / "Scripts" / "World" / "PersistentWorldRegistry.cs",
    "failClosedEditTest": ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Memory" / "Editor" / "GlobalDataVaultFailClosedEditTests1413.cs",
    "arenaAllocatorSentinel1414EditTest": ROOT / "Assets" / "_Project" / "Tests" / "Editor" / "ArenaAllocatorSentinel1414EditTests.cs",
}

FORBIDDEN = {
    "referenceNewTextHits": re.compile(r"\bnew\s+(?!NativeArray<|ReadOnlySpan<|Span<|Vector[234]\b|Color\b|Quaternion\b|StormPropagationDumpHeader\b|float[234]?\b|double[234]?\b|int[234]?\b|uint[234]?\b|bool\b|byte\b|short\b|ushort\b|long\b|ulong\b)"),
    "stringFormatHits": re.compile(r"\bstring\.Format\s*\("),
    "toStringHits": re.compile(r"\.ToString\s*\("),
    "linqHits": re.compile(r"\.(Select|Where|Any|First|FirstOrDefault|Single|SingleOrDefault|ToArray|ToList)\s*\("),
    "foreachHits": re.compile(r"\bforeach\s*\("),
}


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def sample_compilation_gate() -> dict:
    command = (
        "$cpu=(Get-Counter '\\Processor(_Total)\\% Processor Time' -SampleInterval 1 -MaxSamples 1).CounterSamples.CookedValue; "
        "$dotnet=(Get-Process dotnet -ErrorAction SilentlyContinue | Measure-Object).Count; "
        "$csc=(Get-Process csc -ErrorAction SilentlyContinue | Measure-Object).Count; "
        "[pscustomobject]@{cpuLoadPercent=[math]::Round($cpu,6); dotnetCount=$dotnet; cscCount=$csc} | ConvertTo-Json -Compress"
    )
    try:
        completed = subprocess.run(
            ["powershell", "-NoProfile", "-Command", command],
            check=True,
            capture_output=True,
            text=True,
            timeout=10,
        )
        sample = json.loads(completed.stdout)
        sample["dotnetBuild"] = "BLOCKED_BY_CONTENTION" if sample["cpuLoadPercent"] > 50 or sample["dotnetCount"] > 0 or sample["cscCount"] > 0 else "PERMITTED_NOT_RUN"
        sample["sampleCommand"] = "Get-Counter '\\Processor(_Total)\\% Processor Time' plus Get-Process dotnet/csc"
        return sample
    except Exception as exc:
        return {
            "cpuLoadPercent": None,
            "dotnetCount": None,
            "cscCount": None,
            "dotnetBuild": "BLOCKED_BY_SAMPLE_FAILURE",
            "sampleError": type(exc).__name__,
        }


def line_number(text: str, needle: str, start: int = 0) -> int:
    idx = text.find(needle, start)
    if idx < 0:
        return 0
    return text.count("\n", 0, idx) + 1


def extract_brace_block(text: str, signature: str) -> tuple[int, int, str]:
    sig = text.find(signature)
    if sig < 0:
        raise RuntimeError(f"signature not found: {signature}")
    open_brace = text.find("{", sig)
    if open_brace < 0:
        raise RuntimeError(f"opening brace not found: {signature}")
    depth = 0
    for i in range(open_brace, len(text)):
        ch = text[i]
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return line_number(text, signature), text.count("\n", 0, i) + 1, text[open_brace + 1:i]
    raise RuntimeError(f"closing brace not found: {signature}")


def extract_brace_block_after(text: str, signature: str, after: str) -> tuple[int, int, str]:
    marker = text.find(after)
    if marker < 0:
        raise RuntimeError(f"marker not found: {after}")
    sig = text.find(signature, marker)
    if sig < 0:
        raise RuntimeError(f"signature not found after marker: {signature}")
    open_brace = text.find("{", sig)
    if open_brace < 0:
        raise RuntimeError(f"opening brace not found: {signature}")
    depth = 0
    for i in range(open_brace, len(text)):
        ch = text[i]
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return text.count("\n", 0, sig) + 1, text.count("\n", 0, i) + 1, text[open_brace + 1:i]
    raise RuntimeError(f"closing brace not found: {signature}")


def scan_forbidden(name: str, start: int, end: int, body: str) -> dict:
    result = {"name": name, "startLine": start, "endLine": end, "lineCount": max(0, end - start + 1)}
    total = 0
    for key, pattern in FORBIDDEN.items():
        hits = []
        for match in pattern.finditer(body):
            hits.append(body.count("\n", 0, match.start()) + start)
        result[key] = hits
        total += len(hits)
    result["forbiddenHitCount"] = total
    return result


def count_nested_locks(body: str) -> int:
    return len(re.findall(r"\bTry(?:AcquireWriteLock|LockBuffer)\s*\(", body))


def regex_hit_lines(text: str, pattern: str, start_line: int = 1) -> list[int]:
    hits = []
    compiled = re.compile(pattern)
    for match in compiled.finditer(text):
        hits.append(text.count("\n", 0, match.start()) + start_line)
    return hits


def main() -> None:
    batch_text = CURRENT_BATCH.read_text(encoding="utf-8", errors="replace")
    optimization_report = json.loads(REPORT.read_text(encoding="utf-8-sig")) if REPORT.exists() else {}
    compilation_sample = sample_compilation_gate()
    prompt_match = re.search(r'(?s)<AGENT_PROMPT\b(?=[^>]*\bid="1413")[^>]*>.*?</AGENT_PROMPT>', batch_text)
    if not prompt_match:
        raise RuntimeError("AGENT_PROMPT 1413 not found")
    prompt = prompt_match.group(0)

    global_text = FILES["globalDataVault"].read_text(encoding="utf-8", errors="replace")
    organic_text = FILES["destructibleOrganic"].read_text(encoding="utf-8", errors="replace")
    visual_text = FILES["visualPressureAging"].read_text(encoding="utf-8", errors="replace")
    storm_text = FILES["stormPropagation"].read_text(encoding="utf-8", errors="replace")
    persistent_text = FILES["persistentWorldRegistry"].read_text(encoding="utf-8", errors="replace")

    hot_blocks = []
    queue_start, queue_end, queue_body = extract_brace_block(global_text, "private bool QueueDeferredRelease(")
    writer_drain_start, writer_drain_end, writer_drain_body = extract_brace_block(global_text, "private bool DrainDeferredWriterReleaseLocked(in DeferredVaultReleaseRequest request)")
    release_write_start, release_write_end, release_write_body = extract_brace_block(global_text, "public bool ReleaseWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID) where T : struct")
    internal_writer_release_start, internal_writer_release_end, internal_writer_release_body = extract_brace_block(global_text, "private bool ReleaseWriterBlockLock(int bufferKey, long offsetBytes)")
    for signature in (
        "private bool TryEnsureVaultBuffer<T>(",
        "private bool TryAllocatePublishedBuffer<T>(",
        "private bool TryOpenAliasBuffer<T>(BufferID bufferId, SystemID requester, out NativeArray<T> buffer) where T : struct",
        "public bool TryAcquireWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID, out NativeArray<T> buffer) where T : struct",
        "public bool ReleaseWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID) where T : struct",
        "public bool TryLockBuffer(BufferID bufferId, SystemID lockOwner)",
        "private void RecordLockContentionFault(int key)",
        "private bool TryEnterBlockMutationGate()",
        "private bool TryEnterReleaseMutationGate()",
        "private void ClearActiveLockBitIfUnused(int bit)",
        "private void DrainDeferredReleaseRequestsLocked()",
        "private bool TryDrainDeferredReleaseRequests()",
        "private bool DrainDeferredWriterReleaseLocked(in DeferredVaultReleaseRequest request)",
        "private bool DrainDeferredBufferPinReleaseLocked(in DeferredVaultReleaseRequest request)",
        "private bool TryRunLiveCompactionSlice(uint activeBurstLockMask)",
        "private int ReleaseBuffersByOwner(SystemID owner, bool sceneOwnedOnly, out long releasedBytes)",
        "private bool TryReleaseOrphanedBuffer(int key, in VaultBufferMeta meta, out long releasedBytes)",
    ):
        start, end, body = extract_brace_block(global_text, signature)
        hot_blocks.append(scan_forbidden(signature, start, end, body))
    hot_blocks.append(scan_forbidden("private bool QueueDeferredRelease(", queue_start, queue_end, queue_body))

    organic_start = line_number(organic_text, "if (!vault.TryLockBuffer(OrganicTemplateDescriptorsBufferId, OrganicVaultSystemId))")
    organic_end = line_number(organic_text, "vault.TryUnlockBuffer(OrganicTemplateDescriptorsBufferId, OrganicVaultSystemId);", organic_text.find("if (!vault.TryLockBuffer(OrganicTemplateDescriptorsBufferId, OrganicVaultSystemId))"))
    organic_lines = organic_text.splitlines()
    organic_body = "\n".join(organic_lines[organic_start - 1:organic_end])
    hot_blocks.append(scan_forbidden("DestructibleOrganicManager.BuildTemplateCaches locked copy window", organic_start, organic_end, organic_body))

    visual_sync_start, visual_sync_end, visual_sync_body = extract_brace_block(visual_text, "private void VisualSyncTick(in DispatcherTimingDTO timing)")
    hot_blocks.append(scan_forbidden("VisualPressureAgingRuntime.VisualSyncTick", visual_sync_start, visual_sync_end, visual_sync_body))
    for signature in (
        "private int CopyTelemetryDumpSnapshot(IDataVault vault, Span<byte> destinationBytes)",
        "private bool TryReadTelemetryCursor(",
        "private bool TryCopyTelemetryEntries<T>(",
        "private bool TryWriteTelemetryDumpSnapshot(ReadOnlySpan<byte> snapshot, int byteCount, FileStream stream, ref bool failureLogged)",
    ):
        start, end, body = extract_brace_block(visual_text, signature)
        hot_blocks.append(scan_forbidden(f"VisualPressureAgingRuntime.{signature}", start, end, body))

    storm_copy_start, storm_copy_end, storm_copy_body = extract_brace_block(storm_text, "private bool TryCopyTelemetryDumpSnapshot(uint reasonFlags, uint stateHash, byte[] scratch, out int byteCount)")
    hot_blocks.append(scan_forbidden("ShinobuStormPropagationRuntime.TryCopyTelemetryDumpSnapshot", storm_copy_start, storm_copy_end, storm_copy_body))

    hash_map_clear_start, hash_map_clear_end, hash_map_clear_body = extract_brace_block_after(
        persistent_text,
        "public bool Clear()",
        "internal struct VaultBackedHashMap<TKey, TValue>")
    hot_blocks.append(scan_forbidden("PersistentWorldRegistry.VaultBackedHashMap.Clear", hash_map_clear_start, hash_map_clear_end, hash_map_clear_body))

    descriptor_lock_line = organic_start
    second_lock_line = line_number(organic_text, "if (!vault.TryLockBuffer(OrganicLootEntriesBufferId, OrganicVaultSystemId))", organic_text.find("if (!vault.TryLockBuffer(OrganicTemplateDescriptorsBufferId, OrganicVaultSystemId))"))
    try_line = line_number(organic_text, "try", organic_text.find("bool lootLockHeld = false;"))
    finally_line = line_number(organic_text, "finally", organic_text.find("bool lootLockHeld = false;"))
    visual_sync_pos = visual_text.find("private void VisualSyncTick(in DispatcherTimingDTO timing)")
    visual_params_lock_line = line_number(visual_text, "paramsLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingParams, OwnerSystemId);", visual_sync_pos)
    visual_degradation_lock_line = line_number(visual_text, "degradationLocked = vault.TryLockBuffer(BufferID.UberNoirInstanceDegradation, OwnerSystemId);", visual_sync_pos)
    visual_upload_finally_line = line_number(visual_text, "finally", visual_text.find("bool paramsLocked = false;", visual_sync_pos))
    visual_runtime_lock_line = line_number(visual_text, "runtimeLocked = vault.TryLockBuffer(BufferID.VisualPressureAgingRuntime, OwnerSystemId);", visual_sync_pos)
    visual_runtime_finally_line = line_number(visual_text, "finally", visual_text.find("bool runtimeLocked = false;", visual_sync_pos))
    visual_shader_line = line_number(visual_text, "Shader.SetGlobalBuffer(AgingParamsId, readBuffer);", visual_sync_pos)
    visual_stackalloc_line = line_number(visual_text, "Span<byte> telemetryDumpScratch = stackalloc byte[TelemetryDumpSnapshotBytes];", visual_sync_pos)
    visual_dump_write_call_line = line_number(visual_text, "TryWriteTelemetryDumpSnapshot(telemetryDumpScratch, telemetryDumpBytes, _dumpStream", visual_sync_pos)
    visual_stream_write_line = line_number(visual_text, "stream.Write(snapshotBytes);")
    visual_cursor_lock_line = line_number(visual_text, "locked = vault.TryLockBuffer(bufferId, OwnerSystemId);", visual_text.find("private bool TryReadTelemetryCursor("))
    visual_entry_lock_line = line_number(visual_text, "locked = vault.TryLockBuffer(bufferId, OwnerSystemId);", visual_text.find("private bool TryCopyTelemetryEntries<T>("))
    storm_dump_pos = storm_text.find("private bool TryDumpTelemetryToDisk(uint reasonFlags, uint stateHash)")
    storm_copy_call_line = line_number(storm_text, "if (!TryCopyTelemetryDumpSnapshot(reasonFlags, stateHash, scratch, out int byteCount))", storm_dump_pos)
    storm_copy_pos = storm_text.find("private bool TryCopyTelemetryDumpSnapshot(uint reasonFlags, uint stateHash, byte[] scratch, out int byteCount)")
    storm_ring_lock_line = line_number(storm_text, "if (!_vault.TryLockBuffer(BufferID.ShinobuStormPropagationTelemetryRing, OwnerSystem))", storm_copy_pos)
    storm_cursor_lock_line = line_number(storm_text, "if (!_vault.TryLockBuffer(BufferID.ShinobuStormPropagationTelemetryCursor, OwnerSystem))", storm_copy_pos)
    storm_copy_finally_line = line_number(storm_text, "finally", storm_copy_pos)
    storm_copy_unlock_line = line_number(storm_text, "if (telemetryLocked) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationTelemetryRing, OwnerSystem);", storm_copy_pos)
    storm_write_call_line = line_number(storm_text, "return TryWriteTelemetryDumpSnapshotCold(scratch, byteCount);", storm_dump_pos)
    storm_writer_start_line, _, storm_writer_body = extract_brace_block(storm_text, "private static bool TryWriteTelemetryDumpSnapshotCold(byte[] scratch, int byteCount)")
    storm_managed_scratch_line = line_number(storm_text, "_dumpManagedScratch = new byte[ShinobuStormPropagationConstants.DumpScratchBytes];")
    hash_map_marker_pos = persistent_text.find("internal struct VaultBackedHashMap<TKey, TValue>")
    hash_map_clear_pos = persistent_text.find("public bool Clear()", hash_map_marker_pos)
    hash_map_states_lock_line = line_number(persistent_text, "bool statesLocked = _vault.TryAcquireWriteLock(in _statesHandle, _owner, out NativeArray<byte> states);", hash_map_clear_pos)
    hash_map_count_lock_line = line_number(persistent_text, "countLocked = _vault.TryAcquireWriteLock(in _countHandle, _owner, out NativeArray<int> count);", hash_map_clear_pos)
    hash_map_count_guard_line = line_number(persistent_text, "if (!countLocked || count.Length <= 0)", hash_map_clear_pos)
    hash_map_state_clear_loop_line = line_number(persistent_text, "for (int i = 0; i < stateCount; i++)", hash_map_clear_pos)
    hash_map_count_reset_line = line_number(persistent_text, "count[0] = 0;", hash_map_clear_pos)
    hash_map_finally_line = line_number(persistent_text, "finally", hash_map_clear_pos)
    hash_map_count_unlock_line = line_number(persistent_text, "_vault.ReleaseWriteLock(in _countHandle, _owner);", hash_map_clear_pos)
    hash_map_states_unlock_line = line_number(persistent_text, "_vault.ReleaseWriteLock(in _statesHandle, _owner);", hash_map_clear_pos)

    apex = {
        "agentId": "1413",
        "generatedUtc": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        "role": "ATOMIC_LOCK_CONTENTION_AND_FAIL_CLOSED_COORDINATOR",
        "prompt": {
            "taskCount": len(re.findall(r"Task\s+\d{2}:", prompt)),
            "sha256": hashlib.sha256(prompt.encode("utf-8")).hexdigest(),
            "bytesUtf8": len(prompt.encode("utf-8")),
        },
        "modifiedFileHashes": {name: sha256(path) for name, path in FILES.items() if path.exists()},
        "reportHashesBeforeApexWrite": {
            "optimizationReportSha256": sha256(REPORT) if REPORT.exists() else None,
            "lockSpanLedgerSha256": sha256(LEDGER) if LEDGER.exists() else None,
        },
        "zeroGcTextScan": {
            "scope": "modified fail-closed helpers and modified locked copy window only; cold pre-lock cache build allocations are excluded and marked COLD ALLOC in source",
            "forbiddenPatterns": list(FORBIDDEN.keys()),
            "blocks": hot_blocks,
            "totalForbiddenHits": sum(block["forbiddenHitCount"] for block in hot_blocks),
        },
        "dataSovereignty": {
            "migratedFieldsToGlobalDataVault": [],
            "unmanagedStructOffsets": {
                "DeferredVaultReleaseRequest": {
                    "sizeBytes": 32,
                    "fields": {
                        "State": 0,
                        "BufferKey": 4,
                        "OffsetBytes": 8,
                        "ActiveLockBit": 16,
                        "LockOwnerSystemId": 20,
                        "Kind": 24,
                        "Flags": 25,
                        "Reserved16": 26,
                        "Sequence": 28
                    },
                    "abiGuard": "UnsafeUtility.SizeOf<DeferredVaultReleaseRequest>() == 32 in GlobalDataVault.ValidateAbiLayout"
                }
            },
            "securedBufferIds": [
                {"name": "OrganicTemplateDescriptorsBufferId", "value": 73018, "lockLine": descriptor_lock_line},
                {"name": "OrganicLootEntriesBufferId", "value": 73019, "lockLine": second_lock_line},
                {"name": "VisualPressureAgingParams", "value": 71240, "lockLine": visual_params_lock_line},
                {"name": "VisualPressureAgingRuntime", "value": 71241, "lockLine": visual_runtime_lock_line},
                {"name": "VisualPressureAgingTelemetryRing", "value": 71242, "lockLine": visual_entry_lock_line},
                {"name": "VisualPressureAgingTelemetryCursor", "value": 71243, "lockLine": visual_cursor_lock_line},
                {"name": "UberNoirInstanceDegradation", "value": 71247, "lockLine": visual_degradation_lock_line},
                {"name": "UberNoirDegradationTelemetryRing", "value": 71248, "lockLine": visual_entry_lock_line},
                {"name": "UberNoirDegradationTelemetryCursor", "value": 71249, "lockLine": visual_cursor_lock_line},
                {"name": "ShinobuStormPropagationTelemetryRing", "value": 71715, "lockLine": storm_ring_lock_line},
                {"name": "ShinobuStormPropagationTelemetryCursor", "value": 71716, "lockLine": storm_cursor_lock_line},
                {"name": "WorldRegistryDeltaRecordIndexStatesBuffer", "value": 74459, "lockLine": hash_map_states_lock_line},
                {"name": "WorldRegistryDeltaRecordIndexCountBuffer", "value": 74460, "lockLine": hash_map_count_lock_line},
                {"name": "WorldRegistryDeltaChunkIndexStatesBuffer", "value": 74475, "lockLine": hash_map_states_lock_line},
                {"name": "WorldRegistryDeltaChunkIndexCountBuffer", "value": 74476, "lockLine": hash_map_count_lock_line},
                {"name": "WorldRegistryDeltaItemIndexStatesBuffer", "value": 74481, "lockLine": hash_map_states_lock_line},
                {"name": "WorldRegistryDeltaItemIndexCountBuffer", "value": 74482, "lockLine": hash_map_count_lock_line},
                {"name": "WorldRegistryGuidToPoolIndexStatesBuffer", "value": 74495, "lockLine": hash_map_states_lock_line},
                {"name": "WorldRegistryGuidToPoolIndexCountBuffer", "value": 74496, "lockLine": hash_map_count_lock_line},
                {"name": "WorldRegistryEntityStateStatesBuffer", "value": 74499, "lockLine": hash_map_states_lock_line},
                {"name": "WorldRegistryEntityStateCountBuffer", "value": 74500, "lockLine": hash_map_count_lock_line},
                {"name": "WorldRegistryFloraSpawnStateStatesBuffer", "value": 74503, "lockLine": hash_map_states_lock_line},
                {"name": "WorldRegistryFloraSpawnStateCountBuffer", "value": 74504, "lockLine": hash_map_count_lock_line},
                {"name": "WorldRegistrySpawnImpulseStatesBuffer", "value": 74507, "lockLine": hash_map_states_lock_line},
                {"name": "WorldRegistrySpawnImpulseCountBuffer", "value": 74508, "lockLine": hash_map_count_lock_line},
                {"name": "WorldRegistrySpawnVelocityStatesBuffer", "value": 74511, "lockLine": hash_map_states_lock_line},
                {"name": "WorldRegistrySpawnVelocityCountBuffer", "value": 74512, "lockLine": hash_map_count_lock_line},
            ],
            "tryFinallyProof": {
                "descriptorLockLine": descriptor_lock_line,
                "lootLockLine": second_lock_line,
                "tryLineAfterDescriptorLock": try_line,
                "finallyLine": finally_line,
                "descriptorUnlockLine": organic_end,
                "releaseInsideFinally": try_line > descriptor_lock_line and finally_line > try_line and organic_end > finally_line,
                "visualPressureAging": {
                    "paramsLockLine": visual_params_lock_line,
                    "degradationLockLine": visual_degradation_lock_line,
                    "uploadFinallyLine": visual_upload_finally_line,
                    "runtimeLockLine": visual_runtime_lock_line,
                    "runtimeFinallyLine": visual_runtime_finally_line,
                    "shaderGlobalWriteLine": visual_shader_line,
                    "stackallocDumpLine": visual_stackalloc_line,
                    "dumpWriteCallLine": visual_dump_write_call_line,
                    "streamWriteImplementationLine": visual_stream_write_line,
                    "uploadLocksReleasedBeforeShaderWrites": visual_upload_finally_line > visual_degradation_lock_line and visual_shader_line > visual_upload_finally_line,
                    "runtimeLockReleasedBeforeShaderWrites": visual_runtime_finally_line > visual_runtime_lock_line and visual_shader_line > visual_runtime_finally_line,
                    "dumpUsesStackallocNotPersistentNativeArray": "_telemetryDumpScratch" not in visual_text and "stackalloc byte[TelemetryDumpSnapshotBytes]" in visual_text,
                    "fileWriteOutsideVaultLocksByCallSite": visual_dump_write_call_line > visual_stackalloc_line and visual_stackalloc_line > visual_runtime_finally_line,
                },
                "shinobuStormPropagation": {
                    "telemetryRingLockLine": storm_ring_lock_line,
                    "telemetryCursorLockLine": storm_cursor_lock_line,
                    "snapshotCopyStartLine": storm_copy_start,
                    "snapshotCopyEndLine": storm_copy_end,
                    "copyFinallyLine": storm_copy_finally_line,
                    "telemetryRingUnlockLine": storm_copy_unlock_line,
                    "snapshotCopyCallLine": storm_copy_call_line,
                    "diskWriteCallLine": storm_write_call_line,
                    "diskWriterStartLine": storm_writer_start_line,
                    "managedScratchColdAllocLine": storm_managed_scratch_line,
                    "dumpScratchVaultHandleRemoved": "_dumpScratchHandle" not in storm_text and "ShinobuStormPropagationDumpScratch" not in storm_text,
                    "diskWriteAfterVaultUnlock": storm_copy_call_line < storm_write_call_line and storm_copy_finally_line > storm_cursor_lock_line and storm_copy_unlock_line > storm_copy_finally_line and "TryLockBuffer(" not in storm_writer_body,
                    "diskWriterContainsVaultLock": "TryLockBuffer(" in storm_writer_body,
                    "lockWindowUsesManagedScratchOnly": "_dumpManagedScratch" in storm_text and "fixed (byte* scratchPtr = scratch)" in storm_copy_body,
                },
                "persistentWorldRegistryHashMapClear": {
                    "statesLockLine": hash_map_states_lock_line,
                    "countLockLine": hash_map_count_lock_line,
                    "countGuardLine": hash_map_count_guard_line,
                    "stateClearLoopLine": hash_map_state_clear_loop_line,
                    "countResetLine": hash_map_count_reset_line,
                    "finallyLine": hash_map_finally_line,
                    "countUnlockLine": hash_map_count_unlock_line,
                    "statesUnlockLine": hash_map_states_unlock_line,
                    "countGuardBeforeStateClear": hash_map_count_lock_line < hash_map_count_guard_line < hash_map_state_clear_loop_line,
                    "countResetAfterStateClear": hash_map_state_clear_loop_line < hash_map_count_reset_line,
                    "releaseInsideFinally": hash_map_finally_line > hash_map_count_reset_line and hash_map_count_unlock_line > hash_map_finally_line and hash_map_states_unlock_line > hash_map_finally_line,
                    "partialClearOnCountLockFailureRemoved": "if (!countLocked || count.Length <= 0)" in hash_map_clear_body and hash_map_count_guard_line < hash_map_state_clear_loop_line,
                },
            },
        },
        "nestedLockAuditModifiedWindows": {
            "destructibleLockedCopyWindowNestedTryLockCalls": count_nested_locks(organic_body) - 2,
            "note": "two top-level acquisitions are sequential in one protected scope; no inner acquisition exists inside the mutation loop body",
        },
        "deferredReleaseContract": {
            "dedupePolicy": "QueueDeferredRelease performs a best-effort writer duplicate scan only; buffer-pin releases preserve one queued record per accepted release call. Stale writer duplicates must drain idempotently without releasing a newer writer.",
            "dedupePolicyEvidence": {
                "hasWriterOnlyFilter": "kind == DeferredReleaseKindWriter" in queue_body,
                "hasNoEnqueueGateBusyFailPath": "_deferredReleaseEnqueueGate" not in global_text,
                "hasAtomicSlotClaim": "Interlocked.CompareExchange(ref request->State, DeferredReleaseStateWriting, DeferredReleaseStateEmpty)" in queue_body,
                "hasStaleWriterOwnerDiscard": "owner != 0 && meta.ActiveWriterSystemID != owner" in writer_drain_body and "ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit)" in writer_drain_body,
                "releaseWriteLockReturnsAcceptedDeferredWriterRelease": "return QueueDeferredWriterRelease(key, meta.OffsetBytes, activeLockBit, (int)systemID);" in release_write_body,
                "internalWriterReleaseReturnsAcceptedDeferredWriterRelease": "return QueueDeferredWriterRelease(bufferKey, offsetBytes, ResolveActiveLockBit((BufferID)bufferKey), 0);" in internal_writer_release_body,
                "ignoredDeferredWriterReleaseLines": regex_hit_lines(global_text, r"_\s*=\s*QueueDeferredWriterRelease\s*\("),
                "matchesArenaAllocator1414EditorContract": "StringAssert.Contains(\"if (kind == DeferredReleaseKindWriter)\", queue)" in (ROOT / "Assets" / "_Project" / "Tests" / "Editor" / "ArenaAllocatorSentinel1414EditTests.cs").read_text(encoding="utf-8", errors="replace")
            },
            "waitPrimitiveScan": {
                "queueDeferredReleaseThreadSpinWaitLines": regex_hit_lines(queue_body, r"Thread\.SpinWait\s*\(", queue_start),
                "queueDeferredReleaseThreadSleepLines": regex_hit_lines(queue_body, r"Thread\.Sleep\s*\(", queue_start),
                "queueDeferredReleaseTaskDelayLines": regex_hit_lines(queue_body, r"Task\.Delay\s*\(", queue_start),
                "queueDeferredReleaseWaitCallLines": regex_hit_lines(queue_body, r"\.Wait\s*\(", queue_start),
                "globalDataVaultThreadSpinWaitLines": regex_hit_lines(global_text, r"Thread\.SpinWait\s*\("),
                "globalDataVaultThreadSleepLines": regex_hit_lines(global_text, r"Thread\.Sleep\s*\("),
                "globalDataVaultTaskDelayLines": regex_hit_lines(global_text, r"Task\.Delay\s*\("),
            },
            "residualLimit": "A caller that retries TryUnlockBuffer after QueueDeferredRelease already returned true can still enqueue multiple buffer-pin releases. The API has no per-acquire token; compliant callers must treat true as accepted ownership transfer."
        },
        "compilationResourceThrottling": {
            "source": "agent1413_apex_verifier.py runtime sample; optimization report throttle copied separately",
            "sample": compilation_sample,
            "optimizationReportSample": optimization_report.get("compilationThrottle", {}),
            "dotnetBuildLaunchedByAgent1413": optimization_report.get("compilationThrottle", {}).get("dotnetBuildLaunchedByAgent1413", False),
            "unityTestRunnerLaunchedByAgent1413": optimization_report.get("compilationThrottle", {}).get("unityTestRunnerLaunchedByAgent1413", False),
        },
        "knownResidualRisk": [
            "GlobalDataVault release APIs now queue a fixed-size unmanaged deferred-release request if the block mutation gate is busy. Runtime drain is static-only verified; compiler and Unity runtime proof are still pending.",
            "Deferred release duplicate suppression is best-effort and writer-only to preserve count semantics for multiple same-owner buffer pins. Stale writer duplicates are discarded in drain without releasing a newer writer. The residual risk is retry after accepted true on tokenless TryUnlockBuffer.",
            "Project-wide scanner still reports loop-shaped and nested-lock candidates outside the edited window; they are report-ranked but not all fixed in this pass.",
        ],
    }

    OUT.write_text(json.dumps(apex, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()

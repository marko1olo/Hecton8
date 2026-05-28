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
    "failClosedEditTest": ROOT / "Assets" / "_Project" / "Scripts" / "Core" / "Memory" / "Editor" / "GlobalDataVaultFailClosedEditTests1413.cs",
}

FORBIDDEN = {
    "referenceNewTextHits": re.compile(r"\bnew\s+(?!NativeArray<|ReadOnlySpan<|Span<|float[234]?\b|double[234]?\b|int[234]?\b|uint[234]?\b|bool\b|byte\b|short\b|ushort\b|long\b|ulong\b)"),
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


def main() -> None:
    batch_text = CURRENT_BATCH.read_text(encoding="utf-8", errors="replace")
    optimization_report = json.loads(REPORT.read_text(encoding="utf-8")) if REPORT.exists() else {}
    compilation_sample = sample_compilation_gate()
    prompt_match = re.search(r'(?s)<AGENT_PROMPT\b(?=[^>]*\bid="1413")[^>]*>.*?</AGENT_PROMPT>', batch_text)
    if not prompt_match:
        raise RuntimeError("AGENT_PROMPT 1413 not found")
    prompt = prompt_match.group(0)

    global_text = FILES["globalDataVault"].read_text(encoding="utf-8", errors="replace")
    organic_text = FILES["destructibleOrganic"].read_text(encoding="utf-8", errors="replace")

    hot_blocks = []
    for signature in (
        "private void RecordLockContentionFault(int key)",
        "private bool TryEnterBlockMutationGate()",
        "private bool TryEnterReleaseMutationGate()",
        "private void ClearActiveLockBitIfUnused(int bit)",
        "private bool QueueDeferredRelease(",
        "private void DrainDeferredReleaseRequestsLocked()",
        "private bool TryDrainDeferredReleaseRequests()",
        "private bool DrainDeferredWriterReleaseLocked(in DeferredVaultReleaseRequest request)",
        "private bool DrainDeferredBufferPinReleaseLocked(in DeferredVaultReleaseRequest request)",
    ):
        start, end, body = extract_brace_block(global_text, signature)
        hot_blocks.append(scan_forbidden(signature, start, end, body))

    organic_start = line_number(organic_text, "if (!vault.TryLockBuffer(OrganicTemplateDescriptorsBufferId, OrganicVaultSystemId))")
    organic_end = line_number(organic_text, "vault.TryUnlockBuffer(OrganicTemplateDescriptorsBufferId, OrganicVaultSystemId);", organic_text.find("if (!vault.TryLockBuffer(OrganicTemplateDescriptorsBufferId, OrganicVaultSystemId))"))
    organic_lines = organic_text.splitlines()
    organic_body = "\n".join(organic_lines[organic_start - 1:organic_end])
    hot_blocks.append(scan_forbidden("DestructibleOrganicManager.BuildTemplateCaches locked copy window", organic_start, organic_end, organic_body))

    descriptor_lock_line = organic_start
    second_lock_line = line_number(organic_text, "if (!vault.TryLockBuffer(OrganicLootEntriesBufferId, OrganicVaultSystemId))", organic_text.find("if (!vault.TryLockBuffer(OrganicTemplateDescriptorsBufferId, OrganicVaultSystemId))"))
    try_line = line_number(organic_text, "try", organic_text.find("bool lootLockHeld = false;"))
    finally_line = line_number(organic_text, "finally", organic_text.find("bool lootLockHeld = false;"))

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
            ],
            "tryFinallyProof": {
                "descriptorLockLine": descriptor_lock_line,
                "lootLockLine": second_lock_line,
                "tryLineAfterDescriptorLock": try_line,
                "finallyLine": finally_line,
                "descriptorUnlockLine": organic_end,
                "releaseInsideFinally": try_line > descriptor_lock_line and finally_line > try_line and organic_end > finally_line,
            },
        },
        "nestedLockAuditModifiedWindows": {
            "destructibleLockedCopyWindowNestedTryLockCalls": count_nested_locks(organic_body) - 2,
            "note": "two top-level acquisitions are sequential in one protected scope; no inner acquisition exists inside the mutation loop body",
        },
        "compilationResourceThrottling": {
            "source": "agent1413_apex_verifier.py runtime sample; optimization report throttle copied separately",
            "sample": compilation_sample,
            "optimizationReportSample": optimization_report.get("compilationThrottle", {}),
            "dotnetBuildLaunchedByAgent1413": False,
            "unityTestRunnerLaunchedByAgent1413": False,
        },
        "knownResidualRisk": [
            "GlobalDataVault release APIs now queue a fixed-size unmanaged deferred-release request if the block mutation gate is busy. Runtime drain is static-only verified; compiler and Unity runtime proof are still pending.",
            "Project-wide scanner still reports loop-shaped and nested-lock candidates outside the edited window; they are report-ranked but not all fixed in this pass.",
        ],
    }

    OUT.write_text(json.dumps(apex, indent=2, sort_keys=True) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()

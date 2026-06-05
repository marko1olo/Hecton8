# Biolum Black-Box Route Decision

1. Status: PENDING VERIFICATION
   Evidence class: STATIC_TOOL_OUTPUT

2. Mandates followed:
   - .agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt
   - .agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt
   - .agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt
   - Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md
   - telemetry.md
   - systems.md
   - performance.md

3. Exact source anchors:
   - BiolumPulseSyncRuntime.cs:311-315 (SOURCE DECISION BIOLUM_BLACKBOX_OWNER_LOCAL_20260605 comment)
   - BiolumPulseSyncRuntime.cs:319 (Entries field)
   - BiolumPulseSyncRuntime.cs:336 (Entries constructor)
   - BiolumPulseSyncRuntime.cs:384 (_blackBoxDumpWriteBytes field)
   - BiolumPulseSyncRuntime.cs:3993 (_blackBoxDumpWriteBytes constructor)

4. Current route facts:
   - owner: BIOLUM_PULSE_SYNC (BlackBoxDumpSnapshotOwner)
   - capacity: 300 frames (BlackBoxFrameCount) and BlackBoxDumpByteCount
   - schema / record type: BiolumPulseTelemetryEntry and raw byte array
   - allocator: Allocator.Persistent
   - lifetime: Session
   - disposal: Explicit Dispose() methods (DisposeBlackBoxDumpSnapshot / DisposeBlackBoxDumpWriteBytes)
   - dump trigger: Crash or explicit dump request
   - hot-path allocation risk: Low (COLD NATIVE ALLOC happens once, then reused)
   - DataVault guard interaction: Flattens DataVault write locks and keeps file IO outside DataVault guards
   - file writer path: Background thread serialization to binary file

5. Decision:
   - ACCEPT_OWNER_LOCAL_PENDING_PROOF
   - This owner-local black-box mirror is acceptable as a telemetry exception because it satisfies the 300-frame limit and safely decouples file IO from DataVault locks.
   - Current source already includes the missing in-file decision fields: explicit Session lifetime, explicit owner disposal rule, explicit "no gameplay authority" clause, no cross-domain snapshot contract, and no blind DataVault migration.
   - Do not create a global route card for this purely owner-local diagnostic scratch; `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md` says owner-local code should stay owner-local.
   - Remaining proof: compile, Unity import, GC/profiler, and deterministic runtime dump artifact.

6. Rejected alternatives:
   - blind DataVault migration: would incorrectly place transient file IO scratch into global memory.
   - managed buffer: forbidden by zero-GC mandates and would allocate during faults.
   - Debug.Log proof: forbidden by performance mandates and telemetry isolation rules.
   - binary low/high quality switch: black-box telemetry must run consistently without scaling down.

7. Low / Middle / High / Ultra consequences:
   - Low: preserve 300-frame evidence without hot allocation.
   - Middle: same gameplay truth and bounded dump route.
   - High: richer VFX only after telemetry proof.
   - Ultra: extra debug density only through continuous `GlobalQualityWeight`, not authority changes.

8. Non-claims:
   - source decision fields are present by static source readback only
   - no compile proof
   - no Unity proof
   - no profiler/GC proof
   - no runtime dump proof

# Rationale_DOC_AUDIT

Agent: DOC_AUDIT
Domain: Documentation / Project Reality Audit / Editor Validation Tripwires
Current continuation: R38
Date: 2026-05-13

Previous rationale history is archived under `Docs/Archive/Batch005/AgentLogs/Rationale_DOC_AUDIT.md`.

## Decision 038 - Pager Worker Fault Accounting Must Be Closed On The Failing Command

Problem: `H8BinaryWorldPager.RunWorkerLoop()` decremented pending write/read counters only after `ProcessWrite()` or `ProcessRead()` returned normally. The inner IO methods catch normal file corruption/IO cases, but unexpected exceptions before those guarded regions can still jump to the outer worker catch, stop the worker, and leave `_pendingWriteCount` or `_pendingReadCount` inflated. That makes diagnostics lie and can make later budget checks look saturated after a fault.

Solution: move per-command processing behind small accounting wrappers. Each dequeued command now decrements its pending counter in `finally`. Unexpected command-level faults record telemetry, mark the pager fail-closed, zero exposed pending queue counters, and request worker shutdown. This keeps the failure deterministic and makes post-fault telemetry honest.

Rejected Alternatives: leaving the outer worker catch alone was rejected because it reports a global fault after the damage is already done to counters. Retrying the same command was rejected because the command may reference invalid native memory or a disposed slot; predictable fail-closed behavior is safer than hiding memory corruption. Allocating managed diagnostic payloads was rejected because this is a pager subsystem and the black-box telemetry already exists.

Scalability potential: Low devices keep the same zero-frame-cost steady path and get clearer fallback state when disk/native state breaks. Middle/High/Ultra devices do not spend extra normal-frame budget; the saved debugging time goes into reliable page streaming instead of chasing false queue saturation.

Hardware Impact: Normal path is unchanged. Failure path adds only cold background bookkeeping. Estimated low-end i3/MX350 frame impact: 0.000 ms; background failure handling remains dominated by existing disk/black-box dump cost.

## Decision 039 - WFC Outpost Persistence Contract Cannot Stay Interface-Only

Problem: Current `IAsyncPersistenceService` declared `TryPersistWfcOutpostStateSnapshot` and `TryApplyWfcOutpostStateOverride`, while `SaveManager` did not implement them. After rebuilding current `Hecton8.Core.Contracts` from live source, this became the next real compile wall. Leaving it as documentation-only would preserve a fake persistence surface: WFC outpost mutable state would appear promised by the registry but have no service path.

Solution: keep the contract inside `SaveManager` and use the current MacroDB path now present in the live file. Mutable WFC cell flags live in DataVault `BufferID.WfcOutpostGrid`, are packed through `PackWfcOutpostMutableStateJob` into fixed `NativeArray<ulong>` scratch, encoded through `SaveBinaryPayloadCodec.TryWriteWfcOutpostBitmaskPayload`, deduplicated by one-sector packed hash, and committed via `IMacroDatabaseService.MarkDirty`. Restore reads `MacroDatabasePayloadHandle` through `TryGetPayload`, decodes into restore scratch, and unpacks mutable flags back into the DataVault grid.

Rejected Alternatives: empty stubs were rejected because they would satisfy the compiler while lying to gameplay systems. A direct `FileStream` path was rejected because MacroDB is now the current service authority for this WFC payload. A managed dictionary/cache was rejected because WFC state is small enough for fixed native scratch and the project requires zero-GC persistence paths.

Scalability potential: Low = WFC outpost mutable state persists as a compact bitmask instead of full grid payload. Middle = unchanged snapshots skip pager writes via packed hash. High/Ultra = richer outpost state can add bit planes later without changing the page transport contract.

Hardware Impact: Normal frame cost is 0 unless a WFC persistence caller invokes the service or WFC state signals are drained. Persist path loops `500` cells x `4` mutable bits into `32` ulong words, then writes a small MacroDB payload. Restore path decodes only the bounded payload returned by MacroDB. Estimated low-end i3/MX350 steady frame impact: 0.000 ms outside explicit WFC save/restore signal work.

## Decision 040 - Full Core Compile Result Must Be Demoted Under Active Churn

Problem: R37 local Bee/Roslyn `Hecton8.Core` probe returned `0`, but the current workspace changed underneath it. R38 rebuilt current contract/audio dependency refs and progressed past the persistence wall, then hit unrelated active errors in `SpatialAudioManager.cs`, `ScannerTool.cs`, `ScannableTarget.cs`, `HectonFluidEngine.cs`, and `UI/ActionProgressHUD.cs`.

Solution: stable docs now say R37 full-Core success is stale for the current worktree. R38 claims only the probes that actually passed (`Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, temporary audio virtualization refs) and records full `Hecton8.Core` as blocked by unrelated active churn.

Rejected Alternatives: filtering out problem files was rejected as proof because those files are referenced by other Core files. Claiming the old R37 compile as current was rejected because it ignores active filesystem evidence. Chasing audio/fluid/UI repairs from DOC_AUDIT was rejected as cross-domain expansion after the persistence contract wall was closed.

Scalability potential: Accurate compile boundaries reduce integration time and prevent teams from spending performance work on a build state that no longer exists.

Hardware Impact: Documentation/probe correction only. Runtime cost: 0.000 ms.

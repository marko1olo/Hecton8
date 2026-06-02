# LOG_X_014

Status: PENDING VERIFICATION
Date: 2026-05-25
Agent: X_014 / MERKLE_STATE_AND_NETWORK_ROLLBACK_SCOUT

## Read-Only Network Rollback Scout Pass

What was wrong:
- `HectonNetworkManager.cs` is a mode facade, not a transport/netcode implementation.
- Runtime network Merkle root is quality-dependent through `ResolveMerkleLeafBudget(GlobalQualityWeight)` and root seed leaf budget. Same gameplay state can hash differently across peers with different quality.
- Runtime rollback path restores a snapshot and emits `MockTickCommand`; audited source does not execute a full deterministic replay loop.
- `EntityAUPs` snapshot storage is `RollbackAup48` while network Merkle hashes canonical `double3`; current `ByteOffset = 0` avoids a proven active defect, but future subrange hashing can mis-index.
- `RestoreSnapshotJob.TryCopyDestination` can silently truncate when destination capacity is smaller than serialized count.
- Save Merkle `LeafFlagCosmetic` does not exclude a leaf from the save root; it only allows later delta payload pruning.

What was done:
- Extracted `AGENT_PROMPT id="X_014"` from `Docs/Tasks/CURRENT_BATCH.md`.
- Read required mandates: net sync/reconciliation, ARM64 layout, save checksum, AUP determinism, telemetry dump, Zero-GC.
- Audited `HectonNetworkManager.cs`, `HectonRollbackNetcodeRuntime.cs`, `RollbackNetcodeContracts.cs`, `SaveStateMerkleTree.cs`, `SaveMasterHashV10.cs`, `SaveBinaryStorage.cs`, and DTO dependencies.
- Mapped network Merkle descriptors, node layout, leaf hash paths, branch/root aggregation, snapshot stride, state ring capacity, restore flow, remote hash fence, and save Merkle separation.
- Exported machine-readable report: `Docs/Reports/NETWORK_ROLLBACK_SCOUT_REPORT_X_014.json`.

Cinematic cheats used:
- None in runtime. This was a read-only audit.
- Future-cheat recommendation: keep visual correction/interpolation in `VisualStateDTO` and `VisualStateHistoryDTO` outside authoritative hash; scale presentation smoothing with `GlobalQualityWeight`, not state-truth leaves.

Exact microseconds saved:
- Measured runtime saved: 0 us. No C# runtime code changed.
- Static prevention value: not claimed as measured. Removing quality-dependent authoritative leaf selection in future implementation should prevent false rollback/resync work, but that requires profiler proof before any microsecond claim.

Verification:
- JSON syntax validated with PowerShell `ConvertFrom-Json`.
- No compile launched. No C# source edit was authorized or performed by X_014.
- Current repository is already heavily dirty from other agents; X_014 artifacts are docs/report/log only.

## T.A.R.S. APEX Override Addendum

What was wrong:
- The first report was too broad for direct desync prevention work.
- `HectonNetworkManager.cs` cannot prove Merkle behavior because it only toggles runtime mode and contains no Merkle/rollback serializer.

What was done:
- Re-read `HectonNetworkManager.cs`, `HectonRollbackNetcodeRuntime.cs`, `RollbackNetcodeContracts.cs`, `LockstepStateValidator.cs`, `InputDeterminismDtos.cs`, and `MemorySentinelContracts.cs`.
- Produced exact leaf table, hash formulas, bit shifts, branch/root aggregation, snapshot write order, restore overwrite order, and full-resim absence proof.
- Added `Docs/Reports/NETWORK_ROLLBACK_SCOUT_REPORT_X_014_APEX_ADDENDUM.md`.

Cinematic Cheats used:
- None in runtime. Addendum only identifies the existing visual correction path as presentation-only and excluded from Merkle descriptors.

Exact microseconds saved:
- Measured runtime saved: 0 us. No C# code changed.

Verification:
- Addendum is static-source evidence only. No compile launched; no C# edits.

## Document-Only Desync Prevention Proof

What was wrong:
- User explicitly rejected chat output and required the full proof in a document.
- Existing addendum had the right facts but was not titled as a desync-prevention proof artifact.

What was done:
- Re-read rollback correction source around `EvaluateInputMismatchJob`, `ApplyRemoteInputCorrectionJob`, and `HeadlessResimulationCommandJob`.
- Created `Docs/Reports/NETWORK_ROLLBACK_DESYNC_PREVENTION_PROOF_X_014.md`.
- Document includes exact Merkle leaf list, inner field offsets, presentation exclusion proof, hash formulas, bit shifts, restore overwrite commands, Vault buffer list, counterexample to quality-stable roots, and missing fast-forward executor boundary.

Cinematic Cheats used:
- None. Static proof only.

Exact microseconds saved:
- Measured runtime saved: 0 us. No C# code changed.

Verification:
- No compile launched. No C# source edit. Proof is static-source only.

## Chat Suppression Directive

What was wrong:
- User explicitly required substantive output in documents, not chat.

What was done:
- Added communication directive section to `Docs/Reports/NETWORK_ROLLBACK_DESYNC_PREVENTION_PROOF_X_014.md`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Measured runtime saved: 0 us. Documentation-only update.

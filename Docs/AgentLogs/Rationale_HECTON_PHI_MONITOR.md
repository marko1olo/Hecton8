# Rationale_HECTON_PHI_MONITOR

Status: CODE PATCHED / COMPILE BLOCKED BY EXISTING DEPENDENCIES / RUNTIME PENDING VERIFICATION

## 2026-05-14 Active Memory Rebuild After Workspace Hygiene Drift

Problem: Active H-Phi monitor files and the audit tool disappeared during a concurrent doc/worktree refresh. Continuing without disk memory would violate the anti-amnesia protocol and make later reports unverifiable.

Solution: Recreate the active `Status`, `Rationale`, `LOG`, and `Tools/Architecture/HectonPhiAudit.ps1` files in the live workspace. Re-check `Docs/Tasks/CURRENT_BATCH.md` with `rg`; no active `HECTON_PHI_MONITOR` batch prompt exists, so the user direct request remains the authority for this pass.

Rejected Alternatives: Reading archived Batch005 state as the active state was rejected because AGENTS batch hygiene says archived logs are not the current batch brain. Continuing with chat memory only was rejected because context compaction will corrupt the evidence trail.

Scalability potential: Low/MX350 benefits from a repeatable metric that exposes DataVault and save-layout pressure before it becomes runtime debt. High/Ultra only benefits if later code changes convert saved CPU/bandwidth into richer visuals; metric-only work has no visual value.

Hardware Impact: Recreating docs/tool has zero runtime cost. Static scans cost editor/CLI time only.

## 2026-05-14 Codec Guard Candidate

Problem: `SaveBinaryPayloadCodec` variable-size collection readers allocate managed containers from serialized counts. Generic struct arrays validate byte availability before allocation, but string/custom/list/dictionary readers need the same malformed-count guard.

Solution: Add minimum serialized-byte guards before allocating arrays, lists, dictionaries, and custom DTO arrays. Preserve the binary format by using existing per-field readers; no save version bump.

Rejected Alternatives: Broad DataVault migration was rejected in this pass because H-Phi says Data Sovereignty is the bottleneck, but a blind NativeArray migration without owner, `BufferID`, `SystemID`, generation, and disposal proof would be architecture debt. Marking bool-containing DTOs `[BinaryBlittableSafe]` for score gain was rejected as metric fraud.

Scalability potential: Low tier avoids pathological memory spikes from malformed saves. Middle/High/Ultra behavior is unchanged for valid saves; the guard is a cold-path resilience improvement, not a visual feature.

Hardware Impact: Hot path cost is zero. Save-load cold path gains a small O(1) preallocation check per collection and avoids large failed allocations on corrupt payloads.

Verification: Pending. A `BuildProjectReferences=false` build failed due missing types that exist on disk, so it is logged as an invalid dependency-exclusion compile probe, not as proof against the codec change.

## 2026-05-14 Codec Guard Implementation And Compile Wall

Problem: The current workspace reverted prior save hardening. `ProceduralFaunaStateDTO[]` and `HibernatedFaunaStateDTO[]` again used raw unmanaged array blits despite bool fields, and variable-size collection readers still allocated containers before proving enough serialized payload remained.

Solution: Reapplied `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]` to `ProceduralFaunaStateDTO` and `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 112)]` to `HibernatedFaunaStateDTO`. Replaced their raw array blits with explicit field codecs that preserve fixed strides. Added `BufferReader.CanConsumeBytes` and `CanConsumeCollectionItems` plus minimum serialized-byte checks before allocating string arrays, lists, dictionaries, custom DTO arrays, PDA marker/logbook arrays, and module arrays.

Rejected Alternatives: A save format version bump was rejected because field order and stride are preserved. Marking the bool DTOs `[BinaryBlittableSafe]` was rejected because bool layout is not a truth-safe unmanaged ABI guarantee. Fixing unrelated `VoxelDeltaProcessor` and generated-project assembly inclusion errors was rejected as outside the H-Phi monitor domain.

Scalability potential: Low tier avoids failed large allocations when a corrupt save claims impossible counts. Middle/High/Ultra retain the same valid-save behavior and can spend saved stability budget on richer scene persistence later; no visual path changed in this pass.

Hardware Impact: Hot path cost is zero. Save/load cold path adds O(1) remaining-payload checks per variable-size collection and explicit loops for two fauna arrays. Corrupt-save worst case avoids large managed allocation attempts before bounds failure.

Verification: Full CLI build reached Core compile and failed with 72 unrelated errors: unresolved existing types `HardwareProfileCatalog`, `SaveMasterHashV10Result`, `SaveFileHeaderV10`, `SaveMasterHashV10`, plus unrelated `VoxelDeltaProcessor` double/float and `FastFloorToInt` errors. Changed save codec files were not listed. `git diff --check` found only CRLF warnings, no whitespace errors. Unity runtime remains pending.

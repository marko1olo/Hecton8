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

## 2026-05-14 Audit Counter Correction

Problem: The restored H-Phi audit tool counted every textual `Update(`, including comments, editor `serializedObject.Update()`, and scanner string literals. That inflated Unity update-method debt from the actual 3 method declarations to 27 hits.

Solution: Tighten the `UnityUpdateMethods` regex to count method declarations only: optional access modifier, optional `static`, `void`, then `Update`/`LateUpdate`/`FixedUpdate`. Domain update counters now use the same declaration-only pattern.

Rejected Alternatives: Leaving the false-positive counter was rejected because it makes H-Phi movement noisy and punishes documentation/comments. Deleting real `Update` methods was rejected because the three real declarations are in Core dispatcher/FFR enforcement territory and require domain-owner review.

Scalability potential: Low tier gets a more honest signal about actual hot-path MonoBehaviour cadence. High/Ultra decision making benefits from the same correction because false debt no longer hides real bottlenecks such as Data Sovereignty.

Hardware Impact: Runtime cost is zero. The audit script is slower than ideal because it uses repeated source scans; this is CLI-only and should be optimized later if it becomes a CI gate.

Verification: Corrected static scan reports `UnityUpdateMethods=3`, `H-Phi_static_narrow=0.000878730`, and `H-Phi_static_risk=0.000010357`.

## 2026-05-15 Runtime H-Phi Model And Find-Debt Pass

Problem: The H-Phi monitor mixed shipped runtime debt with editor authoring debt. `Scripts/Editor` contributed 126 `Find*` hits, 253 `GetComponent*` hits, 343 `NativeArray<T>` hits, and a very low layout ratio. This made the score noisy and hid the actual runtime debt cluster. The audit tool also still used repeated regex scans and had no way to retain all-source hygiene counts separately from runtime scoring.

Solution: Reworked `Tools/Architecture/HectonPhiAudit.ps1` to emit runtime `Counts`/`Scores`, `AllSourceCounts`/`AllSourceScores`, and `EditorCounts`/`EditorScores`. Runtime scoring excludes `Scripts/Editor` and strips `#if UNITY_EDITOR` blocks inside runtime files before counting debt. The scan still retains all-source counts so editor contamination is visible instead of silently discarded.

Rejected Alternatives: Counting editor authoring scripts as player-runtime H-Phi was rejected because it violates evidence classification. Deleting editor tools or mass-moving folders was rejected because this agent does not own editor workflow cleanup. Hiding all-source debt was rejected because the editor folder still affects repo hygiene and compile/import behavior.

Scalability potential: Low/MX350 gets a cleaner runtime signal: the score now points at real runtime bottlenecks such as DataVault adoption and bootstrap scene searches. Middle/High/Ultra benefit from the same signal because optimization work can be aimed at runtime systems, not editor-only validators. Visual-overkill paths remain unaffected in this pass.

Hardware Impact: Runtime cost is 0 us. CLI audit cost is still high: last accurate scan took about 121.724 seconds on this workspace, down from the earlier 184.073 second baseline but not acceptable for a tight CI gate. Further improvement should be a compiled analyzer or cached file-hash mode, not more PowerShell regex layering.

Verification: `Tools/Architecture/HectonPhiAudit.ps1 -Json` completed. Runtime scores: `H-Phi_static_narrow=0.000991118`, `H-Phi_static_risk=0.000012480`. `git diff --check` found no whitespace errors, only CRLF normalization warnings.

## 2026-05-15 Runtime Scene Lookup Surgery

Problem: Runtime `FindAnyObjectByType` / `FindObjectOfType` calls existed in singleton/bootstrap paths where the file already had a static or registry ownership model. These scans are cold-path, but they still create scene-wide lookup debt, inflate H-Phi risk denominator, and weaken deterministic startup on low-end devices.

Solution: Removed scene-wide singleton discovery scans from:
- `HardwareThermalService`
- `AnalyticalCausticsService`
- `LootMagnetSystem`
- `InternalFloodWaterlineRuntime`
- `MaterialDecayRuntime`

The replacement is static ownership: subsystem registration clears the active static where needed, `Awake`/`OnEnable` claims ownership, and duplicate instances are destroyed or disabled through the existing ownership policy. No Tick path data structure or service lookup was added.

Rejected Alternatives: A broad GlobalRegistry rewrite was rejected because it would cross multiple owner domains and mutate startup behavior without Unity Console evidence. Leaving the scans because they were cold-path was rejected because static ownership already existed and is cheaper. Editing `GameBootstrapper` was deferred because its remaining `Find*` calls participate in bootstrap scene handoff and player-spawner recovery; that needs integrator-owned scene evidence.

Scalability potential: Low/MX350 avoids five scene-wide cold startup scans and gets less nondeterministic bootstrap behavior. Middle/High/Ultra do not gain visuals directly; the saved startup hygiene is a currency for more reliable runtime services and richer VFX systems that can initialize without scene searches.

Hardware Impact: Hot path: 0 us measured, no runtime Tick changes. Cold bootstrap: five scene-wide lookups removed. Exact microseconds saved are unmeasured because the user explicitly forbade rebuild/profiler work this pass.

Verification: `rg` runtime find-debt scan now reports the remaining textual runtime hits in `GameBootstrapper`, `HectonUrpShadowBudgetGuard`, `VRSomaticRuntimeBootstrap`, and one `HectonUnderwaterVisuals` editor-only block. H-Phi runtime counter reports `FindObjectCalls=5`. Unity Console, PlayMode, and profiler verification remain pending.

## 2026-05-15 SaveData And Contract Layout Proof Pass

Problem: Runtime memory-alignment score was still weak: `StructLayoutAttributes / StructDeclarations = 874 / 1880 = 0.464893617` in the last runtime scan. `SaveData.cs` contained public DTO structs without explicit layout metadata, and `Core/Contracts` still had public interface-facing snapshot structs without explicit layout. That leaves persistence and registry contracts dependent on implicit compiler/runtime layout assumptions.

Solution: Added `[StructLayout(LayoutKind.Sequential)]` to public save DTO structs that lacked it and to the remaining public structs in `Assets/_Project/Scripts/Core/Contracts`. The patch intentionally changed metadata only: no field order, field type, packing, explicit size, serializer stride, or registry API changed.

Rejected Alternatives: A blanket `[StructLayout(LayoutKind.Sequential, Pack = 1)]` pass was rejected because packing can change ABI/perf and must be proven per binary format. `[BinaryBlittableSafe]` was rejected for managed/reference DTOs in `SaveData.cs`. Adding `BinaryBlittableSafeAttribute` to contract files was rejected because that attribute lives in `Hecton8.Core.Memory.Layout`; pulling a memory-layer symbol into contract-only files would worsen dependency hygiene for a metric gain.

Scalability potential: Low/MX350 benefits from less ambiguous save/contract layout metadata when Burst, IL2CPP, ARM, and Steam Deck builds interpret structs. Middle/High/Ultra get no immediate visual gain; the value is preventing layout drift from consuming future budget during persistence, telemetry, and registry snapshot paths.

Hardware Impact: Runtime hot path cost is 0 us; these are metadata annotations. Cold persistence/telemetry correctness risk is reduced, but exact microseconds are unmeasured because the user explicitly forbade rebuild/profiler work.

Verification: No rebuild was run. Static checks completed: `Tools/Architecture/HectonPhiAudit.ps1 -Json`, public `SaveData.cs` layout scan, public `Core/Contracts` layout scan, and `git diff --check`. Runtime H-Phi narrow is now `0.001029525`, risk-adjusted is `0.000012970`, memory alignment is `0.483253589`. `git diff --check` reports only LF/CRLF normalization warnings.

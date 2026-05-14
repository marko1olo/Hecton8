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

## 2026-05-15 Core Boundary Layout And DataVault Metric Correction

Problem: H-Phi still underreported two architecture facts. First, native/Burst/Core payload structs outside `Core/Contracts` lacked explicit layout metadata, including callback wrappers, command payloads, lockstep hash jobs, native query wrappers, and registry/service snapshots. Second, the Data Sovereignty counter only matched literal `GlobalDataVault`, so real `IDataVault`, `VaultBufferHandle`, `GetBuffer`, and `TryGetBuffer` usage was invisible. That produced a false `GlobalDataVaultRefs=15` count and made the overseer backlog less accurate.

Solution: Added metadata-only `[StructLayout(LayoutKind.Sequential)]` to targeted Core payloads crossing native, Burst, telemetry, registry, command, and service boundaries. Updated `Tools/Architecture/HectonPhiAudit.ps1` so Data Sovereignty counts the actual Vault access surface: `IDataVault`, `VaultBufferHandle<T>`, `GetBuffer<T>`, `TryGetBuffer`, `GetBufferHandle<T>`, `TryGetBufferHandle<T>`, `ResolveBuffer<T>`, and `GlobalDataVault`. Added file-level audit output: `TopNativeArrayFiles`, `OwnerBlockedDataVaultCandidates`, `DisposeCalls`, and editor file rows.

Rejected Alternatives: Cross-domain NativeArray migrations were rejected because the top candidates require domain-owner proof: BufferID, SystemID, generation handling, disposal/relocation behavior, and restart semantics. Adding layout metadata to managed-reference structs solely for score gain was rejected as metric theater. Keeping the old DataVault counter was rejected because it materially lied about existing Vault adoption.

Scalability potential: Low/MX350 benefits from clearer ABI metadata and a better owner backlog for moving native memory into shared Vault ownership. Middle/High/Ultra benefit when that backlog lets domain owners convert siloed buffers into shared, relocatable buffers and spend saved stability/perf margin on richer visuals. This pass itself is metadata/tooling; it does not buy visuals directly.

Hardware Impact: Runtime hot path cost is 0 us; no method bodies or runtime allocations changed. CLI audit cost remains high: latest run took `123.812s`. Exact gameplay microseconds remain unmeasured because the user forbade rebuild/profiler work.

Verification: No rebuild was run. Static checks completed: `Tools/Architecture/HectonPhiAudit.ps1 -Json`, owner-blocked candidate scan, targeted layout scan, and `git diff --check`. Corrected runtime H-Phi narrow is `0.008929037`, risk-adjusted is `0.000114091`, Data Sovereignty is `0.018128162`, memory alignment is `0.495217853`. Runtime Unity evidence remains pending.

Owner-blocked DataVault candidates for Overseer: `HectonVoxelEngine.cs`, `Audio/PlayerCriticalProceduralAudioRenderer.cs`, `PlayerInventory.cs`, `Fauna/PredatorCognitionDomain.cs`, `World/HectonMapMagicVegetationBridge.cs`, `World/EcosystemDirector.cs`, `Power/LogisticsNetworkGraph.cs`, `SaveBinaryStorage.cs`, `SubmarineAtmosphereSystem.cs`, `World/DestructibleOrganicManager.cs`, `World/VegetationFlowFieldIntegrator.cs`, `VoxelDeltaProcessor.cs`, `World/VoxelDynamicNavGridRuntime.cs`, `Construction/HabitatGraphManager.cs`, `WorldProceduralFieldSampler.cs`, `World/FloraInteractionManager.cs`, `Atmosphere/GasDynamicsSolver.cs`, `SubmarineStructuralGrid.cs`, `World/WorldChunkResidencyManager.cs`, `World/VegetationMemoryPool.cs`.

## 2026-05-15 Signal Boundary Layout Cleanup

Problem: `GlobalSignals.cs` still had two public signal-boundary structs without explicit layout metadata: `SpscSignalRingBuffer<T>` and `CombatDamageSignalAupShiftTransformer`. The first wraps native signal fallback storage; the second crosses the snapshot-transform boundary for combat damage AUP shifts. Leaving them implicit violates the signal lane mandate that unmanaged/native lane payloads and boundary helpers must have explicit layout or documented unmanaged field order.

Solution: Added `[StructLayout(LayoutKind.Sequential)]` to those two structs only. The patch intentionally changed metadata only: no signal sanitizer logic, queue/ring behavior, NativeArray allocation/disposal, field order, public method body, or snapshot transform logic changed.

Rejected Alternatives: Editing concurrent `PlayerBaseEnterSignal` / `PlayerBaseExitSignal` sanitizer work was rejected because those changes were already present from another active agent and outside this patch. A broader signal refactor was rejected because it would touch runtime dispatch logic without Unity Console evidence. Cross-domain DataVault migrations remain rejected without owner BufferID/SystemID/generation/disposal proof.

Scalability potential: Low/MX350 benefits from clearer ABI metadata in native signal fallback and combat shift paths under IL2CPP/Burst/ARM assumptions. Middle/High/Ultra get no immediate visual gain; this prevents layout ambiguity from consuming future debugging/perf budget while richer signal consumers are added.

Hardware Impact: Runtime hot path cost is 0 us; attributes are metadata and no executable method body changed. Exact gameplay microseconds remain unmeasured because rebuild/profiler work is forbidden by user order.

Verification: No rebuild was run. Static checks completed: `git diff --check -- Assets/_Project/Scripts/Core/GlobalSignals.cs`, a public/internal `GlobalSignals.cs` struct-layout scan, and `Tools/Architecture/HectonPhiAudit.ps1 -Json`. `GlobalSignals.cs` layout scan reports no public/internal structs without nearby `StructLayout`. Latest runtime H-Phi narrow is `0.009035044`, risk-adjusted is `0.000121334`, Data Sovereignty is `0.018263557`, memory alignment is `0.497348887`. Runtime Unity evidence remains pending.

## 2026-05-15 Core Native Container And Job Layout Cleanup

Problem: The remaining Core layout scan still found native/job/allocation structs without explicit layout metadata: `NativeArenaArray<T>`, `StackQueue<T>`, `VaultGapAuditJob`, `HectonArenaAllocator.ArenaAllocation`, `UnsafeArenaAllocator.ArenaBlock`, and the numeric `HectonHardwareProfile`. These sit on native memory, job, allocation-record, or bootstrap hardware contract boundaries, so implicit field layout is avoidable risk.

Solution: Added `[StructLayout(LayoutKind.Sequential)]` to those targeted structs and added `System.Runtime.InteropServices` imports where required. No method body, pointer arithmetic, NativeArray lifecycle, job scheduling, allocator logic, registry dispatch, or public data flow changed.

Rejected Alternatives: `FixedCharBuffer` and `GameStartContext` were rejected because they contain managed references (`char[]`, `string`). `ServiceReboundEvent` was rejected because it carries `object` service references. `PersistenceAssemblyMarker` and `MacroDatabaseSignalBridge` were rejected as marker/bridge-only structs where layout metadata would be metric theater. Cross-domain DataVault migrations remain owner-blocked.

Scalability potential: Low/MX350 benefits from less ambiguous native container/allocation metadata under IL2CPP/Burst/ARM assumptions. Middle/High/Ultra gain stability headroom for native scratch/vault paths; this pass does not buy visuals directly, it reduces future integration risk.

Hardware Impact: Runtime hot path cost is 0 us; attributes and using directives do not change executable logic. Exact gameplay microseconds remain unmeasured because rebuild/profiler work is forbidden by user order.

Verification: No rebuild was run. Static checks completed: `git diff --check` on the touched Core layout files, a Core public/internal struct-layout scan, and compact `Tools/Architecture/HectonPhiAudit.ps1 -Json` score extraction. Remaining Core missing-layout structs are managed-reference or marker/bridge-only. Latest runtime H-Phi narrow is `0.009266939`, risk-adjusted is `0.000124428`, Data Sovereignty is `0.018593597`, memory alignment is `0.501059322`. Runtime Unity evidence remains pending.

## 2026-05-15 Typed Signal Publish Metric Correction And Summary Mode

Problem: The risk-adjusted integration model still treated typed `GlobalSignals.Publish(...)` calls and confirmed NativeQueue/SystemDispatcher-backed publish lanes as generic publish/event debt. That punished the exact signal lanes used by this codebase and made `SignalBusPush=84` / `EventPublish=450` materially misleading. The audit also forced full JSON output for routine overseer status checks, adding parsing friction after every long scan.

Solution: Updated `Tools/Architecture/HectonPhiAudit.ps1` so `SignalBusPush` includes direct `SignalBus<T>.Push`, typed `GlobalSignals.Publish(...)`, and confirmed queued publish lanes: `VehicleCommandSignalBus`, `PhysicsDeterminismSignals`, `FluidFeedbackEvents`, `LocalizationEvents`, and `VoxelChunkModifiedEvents`. Narrowed `EventPublish` to remaining legacy/direct fan-out surfaces: `HectonEventBus`, `WaterTransitionEvents`, and `SuitDamageEvents`. Added `-Summary` output for compact score/count/CoreGraph/top-owner-blocked JSON.

Rejected Alternatives: Counting every `Publish(...)` as event debt was rejected because it misclassifies typed signal wrappers and NativeQueue lanes as architectural noise. Counting every possible `*.Publish` as signal traffic was rejected because it would hide direct fan-out and generic modding event bus debt. Editing signal runtime logic was rejected because this pass is metric-domain only and no Unity Console/profiler evidence exists.

Scalability potential: Low/MX350 benefits from a more truthful map of where direct coupling remains; domain owners can now target real event/static/registry debt instead of "fixing" typed signals that already follow the lane. Middle/High/Ultra benefit when the corrected risk map preserves signal architecture while allowing richer downstream systems to consume decoupled events without being mislabeled as debt.

Hardware Impact: Runtime hot path cost is 0 us; only the CLI audit script changed. Static audit cost for the latest full source pass is about 149.8 seconds in this workspace. `-CoreGraphOnly -Summary -Json` provides a fast graph-only status path, but full H-Phi still needs a cached or compiled analyzer before CI gating.

Verification: No rebuild was run. Static checks completed: `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`, `git diff --check -- Tools/Architecture/HectonPhiAudit.ps1`, and `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`. Latest full summary reports `SignalBusPush=328`, `EventPublish=28`, `RiskIntegration=0.053947368`, `RuntimeHPhiRisk=0.000505917`, and `RuntimeHPhiNarrow=0.009377979`. Runtime Unity evidence remains pending.

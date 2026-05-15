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

## 2026-05-15 Lexical Scrub Containment And Full Scan Recovery

Problem: Static regex counters can be polluted by comments and smoke-test string literals. A first-pass lexical scrubber made the model more honest in theory, but in this PowerShell implementation it made the full source scan exceed a 420-second cap twice. Leaving that behavior as the default would make H-Phi unusable for iterative monitoring.

Solution: Gate lexical comment/string scrubbing behind explicit `-LexicalScrub` and restore the default source counter path to the proven raw-source scan. Keep `-CoreGraphOnly -Summary -Json` for fast graph evidence and default `-Summary -Json` for full H-Phi evidence. Treat lexical scrubbing as future compiled-analyzer work, not a default PowerShell path.

Rejected Alternatives: Claiming partial H-Phi output from the timed-out runs was rejected as false reporting. Leaving the slow scrubber enabled by default was rejected because it breaks the monitoring loop. Removing the false-positive concern entirely was rejected; it is now recorded as a tool accuracy backlog item.

Scalability potential: Low/MX350 benefits from a H-Phi tool that completes reliably instead of becoming another long-running local build substitute. Middle/High/Ultra benefit from the same operational discipline: architecture metrics can be checked often enough to guide actual runtime work instead of being run only after large risky batches.

Hardware Impact: Runtime hot path cost is 0 us. Tool-only impact: two lexical-scrub full scans timed out beyond 420 seconds; the restored default full summary completed at `2026-05-15 02:55:39 +04:00` with `RuntimeHPhiNarrow=0.010531061` and `RuntimeHPhiRisk=0.000560589`.

Verification: No rebuild was run. Static checks completed: `git diff --check -- Tools/Architecture/HectonPhiAudit.ps1`, `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`, and `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`. Latest current-tree summary reports `SignalBusPush=329`, `EventPublish=28`, `DataVaultRefs=151`, `NativeArrayRefs=7018`, `AupPrecisionIntegrity=0.983739837`, Core asmdef debt `25`, generated project debt `10`, source-backed bridge debt `20`, compile-bridge debt `8`, and project-reference-replacement debt `12`. Runtime Unity evidence remains pending.

## 2026-05-15 AUP Risk Stale-Score Verification And Summary Routing

Problem: The last recorded H-Phi summary still showed `AupPrecisionRisk=6`, but an exact runtime regex scan over `Assets/_Project/Scripts` found no current AUP risk hits. Editing gameplay files from that stale number would be false surgery and could collide with domain owners.

Solution: Re-ran the full `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxAupPrecisionRisk 0` gate and proved the current runtime counter is `AupPrecisionRisk=0`, with `AupPrecisionIntegrity=1.000000000`. Added `TopAupPrecisionRiskFiles` to the compact summary so future regressions show file-level evidence without forcing full JSON parsing. The file-row inclusion predicate now retains files that contain AUP precision risk even if they have no NativeArray, DataVault, or Find debt.

Rejected Alternatives: Patching AUP gameplay call sites from stale report data was rejected because the exact current scan was clean. Enabling the slow lexical scrub path was rejected because it already timed out twice. Hiding AUP risk routing in full JSON only was rejected because the overseer needs fast triage from `-Summary`.

Scalability potential: Low/MX350 benefits from preventing float/offset AUP regressions that can cause jitter, culling drift, and unstable world-space math. Middle/High/Ultra benefit because clean double-safe AUP preserves precision budget for visual overkill at large world scales instead of spending it on positional error cleanup.

Hardware Impact: Runtime hot path cost is 0 us; only static audit output changed. Tooling cost is one additional sort over already-collected file rows. Latest budgeted full summary completed in about 182.5 seconds after the patch.

Verification: No rebuild was run. Static checks completed: `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxAupPrecisionRisk 0`, `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 16 -MaxSourceBackedCompileBridgeDebtReferences 8 -MaxProjectReferenceReplacementDebtReferences 8 -Summary -Json`, and `git diff --check` on touched H-Phi files. Latest current-tree summary reports `SignalBusPush=331`, `EventPublish=28`, `DataVaultRefs=149`, `NativeArrayRefs=7023`, `RuntimeHPhiNarrow=0.010409098`, `RuntimeHPhiRisk=0.000566586`, `AupPrecisionRisk=0`, `AupPrecisionIntegrity=1.000000000`, Core asmdef debt `25`, generated project debt `10`, source-backed bridge debt `16`, compile-bridge debt `8`, and project-reference-replacement debt `8`. Runtime Unity evidence remains pending.

## 2026-05-15 Coupling-Risk Routing And Static Regression Budgets

Problem: H-Phi risk integration is dominated by `GlobalRegistrySurface`, `GetComponentCalls`, `FindObjectCalls`, and legacy event publish counts, but the compact summary only routed DataVault and AUP risk files. That made the next repair target ambiguous and encouraged blind edits in owner domains.

Solution: Extended `HectonPhiAudit.ps1` file rows with `SignalBusPush`, `GlobalRegistrySurface`, `EventPublish`, `StaticInstance`, and `CouplingRisk`. Added `TopCouplingRiskFiles` to summary output. Added full-source budget gates for `-MaxFindObjectCalls` and `-MaxLegacyEventPublish` and blocked those gates under `-CoreGraphOnly`, because they require runtime source counters. Verified the current budget line with AUP risk `0`, runtime `Find* <= 5`, legacy event publish `<= 28`, and current Core graph budgets.

Rejected Alternatives: Editing `GameBootstrapper`, `VRSomaticRuntimeBootstrap`, `PauseMenuController`, audio, fauna, or voxel files directly was rejected because those are outside the H-Phi monitor ownership boundary and require domain-owner dependency injection/runtime evidence. Treating duplicate signal names as a zero budget was rejected for this pass because the current scan reports `6` duplicate names; that is an integrator/signal-owner routing item, not a safe blind rename task.

Scalability potential: Low/MX350 benefits from faster identification of files where scene searches, registry polling, and component lookups concentrate. Middle/High/Ultra benefit because domain owners can spend integration work on actual coupling pressure instead of broad refactors that do not move the risk denominator.

Hardware Impact: Runtime hot path cost is 0 us; only static audit routing changed. Tooling cost increased because more files are retained in the file-row list and sorted for top coupling risk; latest full gated summary completed in about 265.4 seconds.

Verification: No rebuild was run. Static checks completed: `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`, parser guard for `-CoreGraphOnly -MaxFindObjectCalls`, `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxAupPrecisionRisk 0 -MaxFindObjectCalls 5 -MaxLegacyEventPublish 28 -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 14 -MaxSourceBackedCompileBridgeDebtReferences 8 -MaxProjectReferenceReplacementDebtReferences 6`, and `git diff --check -- Tools/Architecture/HectonPhiAudit.ps1`. Latest current-tree summary reports `SignalBusPush=333`, `EventPublish=28`, `DataVaultRefs=150`, `NativeArrayRefs=7026`, `RuntimeHPhiNarrow=0.010497120`, `RuntimeHPhiRisk=0.000574075`, `AupPrecisionRisk=0`, `FindObjectCalls=5`, `LegacyEventPublish=28`, duplicate signal names `6`, Core asmdef debt `25`, generated project debt `10`, source-backed bridge debt `14`, compile-bridge debt `8`, and project-reference-replacement debt `6`. Runtime Unity evidence remains pending.

## 2026-05-15 Score Floors And Concurrent-Scan Hardening

Problem: H-Phi budgets still guarded only a few discrete counters. The actual hard floors, `DataSovereignty`, `MemoryAlignment`, and risk-adjusted runtime H-Phi, could regress without failing the audit. Full scans also failed when another agent deleted a source file after enumeration but before `ReadAllText`.

Solution: Added source-only gates for `GlobalRegistrySurface`, `GetComponentCalls`, `NativeArrayRefs`, `DataSovereignty`, `MemoryAlignment`, and `RuntimeHPhiRisk`. Added CoreGraphOnly guards so source-count floors cannot be requested from graph-only mode. Hardened the duplicate-signal scan and main source loop to skip files removed during concurrent work instead of failing the whole audit.

Rejected Alternatives: Editing top NativeArray/registry owner files was rejected because the failures routed to voxel/audio/inventory/fauna/world/power/bootstrap owners and require BufferID/SystemID/generation/runtime proof. Raising scores by loosening formulas was rejected as metric fraud. Ignoring concurrent file deletion was rejected because this project explicitly runs many agents at once.

Scalability potential: Low/MX350 benefits from regression gates that stop NativeArray, registry, and component-lookup debt before it leaks into low-end frame budget. Middle/High/Ultra benefit because domain owners can spend saved CPU/memory discipline on visual overkill instead of debugging hidden coupling drift.

Hardware Impact: Runtime hot path cost is 0 us; only static tooling changed. Tooling cost remains high: the final full summary completed in about 223.4 seconds. Runtime microseconds remain unmeasured because rebuild/profiler work is forbidden by user order.

Verification: No rebuild was run. Static checks completed: CoreGraphOnly guard for `-MinDataSovereignty`, `git diff --check -- Tools/Architecture/HectonPhiAudit.ps1`, `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`, two intentionally tight full gates that failed on live churn (`NativeArrayRefs 7029 > 7026`, `GlobalRegistrySurface 5150 > 5149`), then final full gate passed at `2026-05-15 04:20:55 +04:00`. Latest current-tree summary reports `RuntimeHPhiNarrow=0.010739531`, `RuntimeHPhiRisk=0.000586338`, `DataSovereignty=0.021374686`, `MemoryAlignment=0.504232804`, `DataVaultRefs=153`, `NativeArrayRefs=7005`, `GlobalRegistrySurface=5143`, `GetComponentCalls=540`, `AupPrecisionRisk=0`, duplicate signal names `6`, Core asmdef debt `25`, generated project debt `10`, source-backed bridge debt `14`, compile-bridge debt `8`, and project-reference-replacement debt `6`. Runtime Unity evidence remains pending.

## 2026-05-15 Managed-Runtime And Job-Sync Risk Gates

Problem: H-Phi already routed coupling and DataVault debt, but it did not expose the static surface for managed runtime risks that directly map to the Zero-GC and Native Memory mandates: LINQ, coroutines, managed formatting, and job `.Complete()` synchronization. Those patterns could regress without a first-class audit budget.

Solution: Added counters, compact summary counts, file rows, top-file routing, and full-source budget gates for `LinqSurface`, `CoroutineSurface`, `ManagedFormatSurface`, and `JobCompleteSurface`. The counters are deliberately reported as static source surface, not runtime allocation proof or call-stack proof.

Rejected Alternatives: Editing the runtime files with the highest static managed-format or `.Complete()` counts was rejected because static text does not prove hot-path execution. Many hits are smoke tests, diagnostics, save/storage, or owner-domain job teardown. Changing them without call-stack evidence would be fake optimization.

Scalability potential: Low/MX350 benefits from hard regression gates around known allocation/synchronization debt classes. Middle/High/Ultra benefit because owner teams can target real hot call stacks later without losing static visibility between profiler passes.

Hardware Impact: Runtime hot path cost is 0 us; only static tooling changed. Tooling cost: full ungated summary took about 322 seconds; final gated summary took about 162.5 seconds. Runtime microseconds remain unmeasured because rebuild/profiler work is forbidden by user order.

Verification: No rebuild was run. Static checks completed: `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`, `-CoreGraphOnly -MaxLinqSurface 1` guard, `git diff --check -- Tools/Architecture/HectonPhiAudit.ps1`, full summary, and final full gated summary at `2026-05-15 04:47:08 +04:00`. Latest current-tree summary reports `RuntimeHPhiNarrow=0.010750370`, `RuntimeHPhiRisk=0.000590952`, `DataSovereignty=0.021362748`, `MemoryAlignment=0.505023797`, `LinqSurface=5`, `CoroutineSurface=0`, `ManagedFormatSurface=704`, `JobCompleteSurface=61`, `DuplicateSignalNameCount=0`, Core asmdef debt `25`, generated project debt `10`, source-backed bridge debt `14`, compile-bridge debt `8`, and project-reference-replacement debt `6`. Runtime Unity evidence remains pending.

## 2026-05-15 Primary-Runtime Managed-Risk Role Routing

Problem: Static managed-risk counts were truthful but too coarse. Smoke tests, diagnostics, persistence codecs, and UI formatting inflate the same `ManagedRuntimeRisk` surface as primary gameplay runtime files. Treating all of those as the same hot-path risk would push the next repair pass toward false optimization and cross-domain owner edits without profiler proof.

Solution: Added file-role classification to `Tools/Architecture/HectonPhiAudit.ps1`: `PrimaryRuntime`, `Instrumentation`, `Persistence`, `UI`, and `Editor`. File rows now emit `FileRole`, `ManagedRuntimeRisk`, `PrimaryManagedRuntimeRisk`, and `PrimaryJobCompleteRisk`. Compact summary now reports `ManagedRiskByRole` and `TopPrimaryManagedRuntimeRiskFiles`. Added a full-source gate `-MaxPrimaryManagedRuntimeRisk` and blocked it under `-CoreGraphOnly`.

Rejected Alternatives: Editing high-count `string.Format` or `.Complete()` owner files was rejected because static text does not prove hot-path allocation or mid-frame job stalls. Lowering the existing managed-risk budgets was rejected because concurrent agents are changing the tree and the goal here is routing accuracy before runtime surgery. Hiding diagnostic/persistence/UI risk was rejected; it remains visible by role instead of contaminating the primary-runtime budget.

Scalability potential: Low/MX350 benefits from isolating real gameplay-runtime managed-risk pressure before profiler work starts. Middle/High/Ultra benefit because owner teams can spend optimization work where it buys frame time or richer visuals instead of deleting safe diagnostic/persistence formatting.

Hardware Impact: Runtime hot path cost is 0 us; only static audit tooling changed. Tooling cost: the final primary-runtime-risk full gated summary completed in about 172 seconds. Runtime microseconds remain unmeasured because rebuild/profiler work is forbidden by user order.

Verification: No rebuild was run. Static checks completed: `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`, `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -MaxPrimaryManagedRuntimeRisk 1 -Summary -Json` guard, `git diff --check -- Tools/Architecture/HectonPhiAudit.ps1`, full summary, and final full gated summary at `2026-05-15 05:14:05 +04:00`. Latest current-tree summary reports `RuntimeHPhiNarrow=0.010800761`, `RuntimeHPhiRisk=0.000594407`, `DataSovereignty=0.021386637`, `MemoryAlignment=0.505023797`, `LinqSurface=5`, `CoroutineSurface=0`, `ManagedFormatSurface=704`, `JobCompleteSurface=61`, `PrimaryManagedRuntimeRisk=353`, `PrimaryJobCompleteRisk=44`, and duplicate signal names `0`. Runtime Unity evidence remains pending.

## 2026-05-15 DataVault Backlog Regression Gate

Problem: Data Sovereignty remains the hard H-Phi floor, but the existing audit only exposed broad `NativeArrayRefs` and top candidate files. It did not answer how much NativeArray surface is still owner-blocked by files with no Vault access, how much manual disposal pressure sits in those files, or which domains should receive the next migration brief.

Solution: Added `OwnerBlockedNativeArrayRefs`, `OwnerBlockedDisposeCalls`, and `NativeOwnershipRisk` to file rows and compact counts. `NativeOwnershipRisk` is a static routing score: owner-blocked NativeArray refs plus double-weighted dispose calls. Added `DataVaultBacklogByDomain`, `DataVaultBacklogByRole`, sorted owner-blocked candidates by native ownership risk, and added `-MaxOwnerBlockedNativeArrayRefs` as a full-source regression gate.

Rejected Alternatives: Migrating NativeArray owners directly was rejected because the top files sit in World, Audio, Gameplay, Construction, Fauna, Core, voxel, and persistence ownership boundaries. Those require BufferID, SystemID, generation, disposal ownership, restart behavior, and job-handle proof. Counting files with any Vault mention as solved was rejected because a single Vault reference can coexist with many siloed buffers.

Scalability potential: Low/MX350 benefits from a concrete migration queue for reducing manual native ownership and disposal pressure before it becomes frame or memory instability. Middle/High/Ultra benefit because future domain migrations can convert saved stability/perf budget into richer world simulation and visuals without losing the architecture map.

Hardware Impact: Runtime hot path cost is 0 us; only static audit tooling changed. Tooling cost: ungated summary completed in about 184 seconds and the final budgeted full summary completed in about 200 seconds. Runtime microseconds remain unmeasured because rebuild/profiler work is forbidden by user order.

Verification: No rebuild was run. Static checks completed: PowerShell parser OK, `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -MaxOwnerBlockedNativeArrayRefs 1 -Summary -Json` guard, full summary at `2026-05-15 12:39:29 +04:00`, and final full gated summary at `2026-05-15 12:43:12 +04:00`. Latest current-tree summary reports `RuntimeHPhiNarrow=0.010823380`, `RuntimeHPhiRisk=0.000606109`, `DataSovereignty=0.021386637`, `MemoryAlignment=0.506081438`, `OwnerBlockedNativeArrayRefs=6195`, `OwnerBlockedDisposeCalls=973`, `NativeOwnershipRisk=8141`, `PrimaryManagedRuntimeRisk=330`, and `PrimaryJobCompleteRisk=44`. Runtime Unity evidence remains pending.

## 2026-05-15 Primary DataVault Backlog Isolation

Problem: The DataVault backlog gate exposed the total owner-blocked native-memory debt, but total debt still mixed primary gameplay runtime with instrumentation, persistence, and UI roles. That makes the gate useful for repo hygiene, but not sharp enough for gameplay-frame risk routing.

Solution: Added primary-only DataVault backlog fields to file rows and summary counts: `PrimaryOwnerBlockedNativeArrayRefs`, `PrimaryOwnerBlockedDisposeCalls`, and `PrimaryNativeOwnershipRisk`. Added `-MaxPrimaryOwnerBlockedNativeArrayRefs` as a full-source gate and a `CoreGraphOnly` guard. Domain/role backlog rows now carry both total and primary-only fields, so the overseer can distinguish gameplay migration pressure from tooling/save/UI debt. The audit also snapshots source file content once and reuses it for duplicate-signal and main H-Phi scans, reducing repeated file reads in the static monitor.

Rejected Alternatives: Treating all owner-blocked NativeArray refs as gameplay debt was rejected because persistence and UI files have different performance and lifetime semantics. Editing `GlobalDataVault.cs`, `VoxelDynamicNavGridRuntime.cs`, or other concurrently changed owner files was rejected because those files are under active owner-agent work and require buffer lifetime proof before migration.

Scalability potential: Low/MX350 benefits from a primary-runtime migration queue that targets native buffers most likely to affect frame/memory stability. Middle/High/Ultra benefit because domain teams can spend native ownership cleanup where it buys simulation headroom and visual overkill, without burning time on diagnostic/persistence formatting or save-only paths first.

Hardware Impact: Runtime hot path cost is 0 us; only static audit tooling changed. The full summary completed in about 100 seconds after the current source-snapshot path, and the final budgeted full summary completed in about 174 seconds while concurrent agents were changing the tree. Runtime microseconds remain unmeasured because rebuild/profiler work is forbidden by user order.

Verification: No rebuild was run. Static checks completed: PowerShell parser OK, `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -MaxPrimaryOwnerBlockedNativeArrayRefs 1 -Summary -Json` guard, full summary at `2026-05-15 13:36:27 +04:00`, and final full gated summary at `2026-05-15 13:48:00 +04:00`. Latest current-tree summary reports `RuntimeHPhiNarrow=0.010687040`, `RuntimeHPhiRisk=0.000611619`, `DataSovereignty=0.021129678`, `MemoryAlignment=0.505783386`, `NativeArrayRefs=7088`, `OwnerBlockedNativeArrayRefs=6280`, `PrimaryOwnerBlockedNativeArrayRefs=5696`, `PrimaryOwnerBlockedDisposeCalls=850`, and `PrimaryNativeOwnershipRisk=7396`. Runtime Unity evidence remains pending.

## 2026-05-15 Runtime Lookup Debt Surgery

Problem: The remaining non-bootstrap runtime `Find*` debt included a shadow-budget fallback that enumerated every scene `Light` and a VR somatic root lookup by global object name. Both were cold paths, but they still violated the registration-first H-Phi model and left static risk integration lower than necessary.

Solution: Changed `HectonUrpShadowBudgetGuard` to enforce only the already-registered dynamic shadow-light slots. Authored/first-party lights must enter through `HectonShadowBudgetLight`, `RegisterDynamicShadowLight`, or `RegisterAuthoritativeForwardSpotlight`. Changed `VRSomaticRuntimeBootstrap` to keep the decoupled VR root in static ownership and reuse that transform instead of resolving by scene-wide name.

Rejected Alternatives: Editing the remaining `GameBootstrapper` calls was rejected because they sit in bootstrap scene-owner handoff and player/spawner recovery; removing them without scene/integrator evidence risks breaking recovery. Keeping the shadow scene enumeration was rejected because the project already has explicit light registration. Adding new runtime warnings was rejected because it would add managed diagnostic noise without profiler evidence.

Scalability potential: Low/MX350 avoids two cold scene-wide/name queries during bootstrap/XR/shadow setup and gets more deterministic registration behavior. Middle/High/Ultra preserve the same shadow and XR hot paths; saved startup hygiene can be spent by domain owners on richer lighting/XR presentation once runtime evidence exists.

Hardware Impact: Hot path cost is 0 us measured; no Tick, render loop, or job schedule changed. Cold path removes one full scene light enumeration/allocation surface and one global object-name lookup surface. Exact microseconds are unmeasured because the user explicitly forbade rebuild/profiler work.

Verification: No rebuild was run. Static checks completed: runtime `rg` lookup scan, `git diff --check -- Assets/_Project/Scripts/Core/HectonUrpShadowBudgetGuard.cs Assets/_Project/Scripts/Gameplay/VRSomaticRuntimeBootstrap.cs`, full `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`, and full budgeted gate at `2026-05-15 14:39:21 +04:00` with `-MaxFindObjectCalls 3` and `-MinRuntimeHPhiRisk 0.000623000`. Latest current-tree summary reports `FindObjectCalls=3`, `RuntimeHPhiNarrow=0.010758376`, `RuntimeHPhiRisk=0.000623300`, `DataSovereignty=0.021270718`, `MemoryAlignment=0.505783386`, `NativeArrayRefs=7086`, `DataVaultRefs=154`, `GlobalRegistrySurface=5084`, and `GetComponentCalls=348`. Runtime Unity evidence remains pending.

## 2026-05-15 Bootstrap Lookup Removal And Core Build Repair

Problem: The last H-Phi blocker in runtime lookup debt was concentrated in `GameBootstrapper`: bootstrap owner handoff still used scene-wide `FindAnyObjectByType`, player recovery used `GameObject.FindGameObjectWithTag`, and spawner recovery used `FindAnyObjectByType(...Include)`. The current build then surfaced a stale `SubscribeToInputManager` error in `PlayerPDA`; the live source had already moved PDA input off direct event subscriptions into `SignalBus<PlayerInputSignal>`, so the first repair step was to verify the actual tree instead of patching a vanished call site.

Solution: Replaced the `GameBootstrapper` scene-wide lookups with bounded active-scene traversal through the existing `_bootstrapSceneRootScratch` and `_bootstrapTransformScratch` lists. The helper checks only the supplied scene, respects active/inactive semantics per call, uses `TryGetComponent`, and clears scratch state on every return path. Verified that `PlayerPDA` now consumes `SignalBus<PlayerInputSignal>.GetFrameSnapshot()` once per UI tick and no longer references `SubscribeToInputManager`; the current Core project builds clean.

Rejected Alternatives: Keeping bootstrap `Find*` calls as "cold path" was rejected because the static H-Phi model had already isolated them as the remaining lookup debt and the file had reusable scratch storage. Restoring input event subscription in `PlayerPDA` was rejected because it would reintroduce per-tick subscription checks and tighter coupling to input service rebinding. A full Unity rebuild was not required; the user asked to fix the build, and `dotnet build` is the direct Core source gate.

Scalability potential: Low/MX350 avoids remaining cold scene-wide lookup surfaces during bootstrap recovery and gets deterministic scene-local recovery traversal. Middle/High/Ultra preserve the same runtime hot paths while keeping startup recovery predictable; saved integration hygiene can be spent later on richer bootstrap visuals and registered systems instead of scene scans.

Hardware Impact: Hot path cost is 0 us measured; no Tick/render/job schedule changed in `GameBootstrapper`. Cold path removes three scene-wide lookup APIs and replaces them with explicit traversal over already-loaded scene roots using existing scratch lists. `PlayerPDA` now reads an existing signal snapshot in its UI tick; no new managed container allocation was introduced by this pass.

Verification: `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:GenerateFullPaths=true -v:minimal` passed with `0 Warning(s)` and `0 Error(s)`. `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` with the current full budgets passed at `2026-05-15 16:13:23 +04:00`: `FindObjectCalls=0`, `RuntimeHPhiRisk=0.000627514`, `RuntimeHPhiNarrow=0.010773555`, `SignalBusPush=338`, `DataSovereignty=0.021311929`, and `MemoryAlignment=0.505517604`. Unity Console, PlayMode, Profiler, and GCMonitor evidence remain pending.

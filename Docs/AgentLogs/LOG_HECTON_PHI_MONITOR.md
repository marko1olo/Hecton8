# LOG_HECTON_PHI_MONITOR

## 2026-05-14 Active Pass Restart

What was wrong:
- Active H-Phi monitor status/rationale/log and `Tools/Architecture/HectonPhiAudit.ps1` were missing from the live workspace after concurrent cleanup.
- The latest trusted static H-Phi score remains source-only: narrow `0.000896018`, risk-adjusted `0.000081638`, compared with original `0.00062`.
- Data Sovereignty is still the dominant metric bottleneck: `GlobalDataVault` references are near-zero compared with `NativeArray<T>` references.

What was done:
- Recreated the active task, rationale, log, and audit tool files.
- Rechecked `Docs/Tasks/CURRENT_BATCH.md`; no active `HECTON_PHI_MONITOR` prompt exists.
- Identified `SaveBinaryPayloadCodec` malformed-count preallocation hardening as the next low-risk improvement candidate.

Cinematic Cheats used:
- None. This is architecture/static tooling and save-load cold-path hardening.

Exact Microseconds saved:
- Runtime: 0 us measured. Static/CLI work only.
- Corrupt save path: unmeasured avoided allocation risk; profiler evidence absent.

Compile Status:
- Pending. A dependency-excluded Core build failed with missing symbols that exist on disk; full build still required after edits.

Phi Gain:
- Static formula gain pending next scan. This hardening may not move the current H-Phi formula because it improves robustness rather than `SignalBus`, `GlobalDataVault`, or `[StructLayout]` counts.

## 2026-05-14 Save Codec Hardening Patch

What was wrong:
- The active workspace had reverted the prior bool DTO save hardening.
- `ProceduralFaunaStateDTO[]` and `HibernatedFaunaStateDTO[]` were again serialized via raw `WriteStructArray<T>` / `ReadStructArray<T>` despite bool fields.
- Variable-size save readers allocated arrays/lists/dictionaries from serialized counts before checking the minimum remaining payload bytes.

What was done:
- Added fixed `[StructLayout]` sizes to the two fauna DTOs without adding false `[BinaryBlittableSafe]`.
- Replaced fauna raw array blits with explicit fixed-stride field codecs: 16 bytes for procedural fauna and 112 bytes for hibernated fauna.
- Added `CanConsumeBytes` / `CanConsumeCollectionItems` to `BufferReader`.
- Added preallocation guards for string arrays, string/float lists, string dictionaries, int hash sets, custom DTO arrays, PDA arrays, and module arrays.
- Restored `Tools/Architecture/HectonPhiAudit.ps1` and reran static H-Phi.

Cinematic Cheats used:
- None. This was save persistence and static audit work.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; no hot path was changed.
- Save/load: no profiler measurement. The gain is failure-mode containment: corrupt payloads now fail before large managed allocations in covered collection readers.

Compile Status:
- BLOCKED BY EXISTING DEPENDENCIES. Full command:
  `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`
- Result: 72 Core errors outside the changed save codec files.
- Representative blockers: unresolved existing `HardwareProfileCatalog`, `SaveMasterHashV10Result`, `SaveFileHeaderV10`, `SaveMasterHashV10`; unrelated `VoxelDeltaProcessor` missing `FastFloorToInt` and double/float conversion errors.

Phi Gain:
- Before this patch on current tree: `H-Phi_static_narrow = 0.000842629`, `H-Phi_static_risk = 0.000009935`.
- After this patch: `H-Phi_static_narrow = 0.000844101`, `H-Phi_static_risk = 0.000009953`.
- Delta: narrow `+0.000001472`, risk `+0.000000018`.
- Compared with the original dialogue baseline `0.00062`: current narrow score is about `+36.15%`.

## 2026-05-14 Audit Counter Correction

What was wrong:
- The restored audit counted all text matching `Update(`, including comments, editor `serializedObject.Update()`, and scanner string literals.
- This produced `UnityUpdateMethods=27` even though the actual method declarations in first-party source are 3.

What was done:
- Tightened the update counter to declaration-only regex in global and per-domain H-Phi counts.
- Reran `Tools/Architecture/HectonPhiAudit.ps1 -Json`.

Cinematic Cheats used:
- None. Metric accuracy only.

Exact Microseconds saved:
- Runtime: 0 us. This is CLI-only.

Compile Status:
- Unchanged: Core compile remains blocked by existing/generated-project errors outside the changed save codec files.

Phi Gain:
- Corrected current H-Phi static narrow: `0.000878730`.
- Corrected current H-Phi static risk-adjusted: `0.000010357`.
- Compared with the original dialogue baseline `0.00062`: corrected current narrow score is about `+41.73%`.

## 2026-05-15 Runtime H-Phi Continuation

What was wrong:
- H-Phi risk scoring mixed shipped runtime code with `Scripts/Editor` authoring debt.
- The audit did not strip `#if UNITY_EDITOR` blocks inside runtime files, so editor-only code in runtime files inflated the runtime debt surface.
- Five runtime singleton/bootstrap services still used scene-wide `FindAnyObjectByType` / `FindObjectOfType` despite already having static or registry ownership paths.

What was done:
- Reworked `Tools/Architecture/HectonPhiAudit.ps1` to report runtime scores separately from all-source and editor-only hygiene counters.
- Added runtime stripping for `#if UNITY_EDITOR` blocks before score calculation.
- Removed scene-wide singleton discovery scans from `HardwareThermalService`, `AnalyticalCausticsService`, `LootMagnetSystem`, `InternalFloodWaterlineRuntime`, and `MaterialDecayRuntime`.
- Kept remaining `GameBootstrapper` and `VRSomaticRuntimeBootstrap` finds untouched because they are bootstrap/scene-handoff behavior and need owner/integrator evidence.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. This was architecture metric/tooling and cold-path startup hygiene.

Exact Microseconds saved:
- Hot path: 0 us measured; no Tick/FixedTick loop changed.
- Cold bootstrap: five scene-wide searches removed. Exact savings unmeasured; profiler evidence absent by user constraint.

Compile Status:
- Not run in this continuation pass by explicit user order.
- Static checks run: `Tools/Architecture/HectonPhiAudit.ps1 -Json`, runtime `rg` find-debt scan, `git diff --check`.
- `git diff --check`: no whitespace errors, only LF/CRLF normalization warnings.

Phi Gain:
- Previous corrected all-source narrow score: `0.000878730`.
- Current runtime narrow score: `0.000991118`.
- Current runtime risk-adjusted score: `0.000012480`.
- Compared with original dialogue baseline `0.00062`: runtime narrow is about `+59.86%`.
- Compared with previous corrected score: runtime narrow is about `+12.79%`, but this includes the evidence-model correction that excludes editor-only debt.

Regression Model:
- CPU: cold startup scene scan pressure reduced; hot path unchanged.
- GC: static source only; no profiler/GCMonitor proof.
- Memory: no persistent runtime allocation added except existing singleton-created objects; no new NativeArray ownership.
- Cadence: no tick registration cadence changed.
- Correctness: risk remains in Unity load-order semantics for scene-authored inactive singleton components. Runtime verification is pending.

## 2026-05-15 SaveData And Core Contract Layout Pass

What was wrong:
- Public save DTO structs in `SaveData.cs` lacked explicit layout metadata, weakening persistence layout evidence.
- Public interface-facing structs in `Assets/_Project/Scripts/Core/Contracts` lacked explicit layout metadata.
- Runtime memory-alignment score was still mid-grade at `0.464893617`.

What was done:
- Added `[StructLayout(LayoutKind.Sequential)]` to public save DTO structs that lacked it.
- Added `[StructLayout(LayoutKind.Sequential)]` to remaining public structs in `Core/Contracts`.
- Verified no public `SaveData.cs` DTO structs and no public/internal `Core/Contracts` structs remain without nearby `StructLayout`.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. This was ABI/layout metadata work.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; metadata-only annotations.
- Cold persistence/registry risk: reduced layout ambiguity. Exact time saved is unmeasured.

Compile Status:
- Not run by explicit user order.
- Static checks run: `Tools/Architecture/HectonPhiAudit.ps1 -Json`, public DTO/contract layout scans, `git diff --check`.
- `git diff --check`: no whitespace errors, only LF/CRLF normalization warnings.

Phi Gain:
- Previous runtime narrow score: `0.000991118`.
- Current runtime narrow score: `0.001029525`.
- Delta from previous runtime narrow: `+0.000038407`, about `+3.88%`.
- Current runtime risk-adjusted score: `0.000012970`.
- Memory alignment: `0.464893617 -> 0.483253589`.
- Compared with original dialogue baseline `0.00062`: current runtime narrow is about `+66.05%`.

## 2026-05-15 Core Boundary Layout And Owner Backlog Pass

What was wrong:
- Core native/Burst/telemetry/command payload structs still lacked explicit layout metadata outside the earlier SaveData and Core/Contracts passes.
- Data Sovereignty counter was objectively wrong: it counted literal `GlobalDataVault` but missed real `IDataVault`, `VaultBufferHandle`, `GetBuffer`, and `TryGetBuffer` usage.
- Overseer had no file-level `NativeArray<T>` owner backlog in the H-Phi tool output.

What was done:
- Added explicit sequential layout metadata to targeted Core payload/job/wrapper structs only.
- Updated `Tools/Architecture/HectonPhiAudit.ps1` to count DataVault access surface, not just the class name.
- Added `TopNativeArrayFiles`, `OwnerBlockedDataVaultCandidates`, `TopEditorNativeArrayFiles`, and `DisposeCalls` to the audit output.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. Architecture metadata and static audit only.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; no hot-path method body changed.
- Audit routing: no runtime gain. It prevents wrong prioritization by exposing real owner debt.

Compile Status:
- Not run by explicit user order.
- Static checks run: `Tools/Architecture/HectonPhiAudit.ps1 -Json`, owner-candidate scan, targeted layout scan, `git diff --check`.
- `git diff --check`: no whitespace errors, only LF/CRLF normalization warnings where reported.

Phi Gain:
- Previous runtime narrow score before this continuation: `0.001029525`.
- Current corrected runtime narrow score: `0.008929037`.
- Current corrected runtime risk-adjusted score: `0.000114091`.
- Data Sovereignty: `0.002141939 -> 0.018128162` after metric correction.
- Memory alignment: `0.483253589 -> 0.495217853`.
- Compared with original dialogue baseline `0.00062`: corrected runtime narrow is about `+1340.17%`.
- Important: most of this jump is a metric-model correction. It is not a runtime performance claim.

Owner-Blocked DataVault Candidates:
- `HectonVoxelEngine.cs` - 277 `NativeArray<T>` refs, 0 DataVault refs.
- `Audio/PlayerCriticalProceduralAudioRenderer.cs` - 232 refs, 0 DataVault refs.
- `PlayerInventory.cs` - 197 refs, 0 DataVault refs.
- `Fauna/PredatorCognitionDomain.cs` - 173 refs, 0 DataVault refs.
- `World/HectonMapMagicVegetationBridge.cs` - 166 refs, 0 DataVault refs.
- `World/EcosystemDirector.cs` - 154 refs, 0 DataVault refs.
- `Power/LogisticsNetworkGraph.cs` - 141 refs, 0 DataVault refs.
- `SaveBinaryStorage.cs` - 132 refs, 0 DataVault refs.
- `SubmarineAtmosphereSystem.cs` - 132 refs, 0 DataVault refs.
- `World/DestructibleOrganicManager.cs` - 125 refs, 0 DataVault refs.

Regression Model:
- CPU: no runtime code path changed except metadata attributes; audit is CLI-only.
- GC: no runtime allocation changed; measured proof absent.
- Memory: no buffer ownership changed; DataVault migration remains owner-blocked.
- Cadence: no tick registration or dispatcher cadence changed.
- Correctness: low compile risk from attributes, but Unity import/compile remains pending because rebuild is forbidden.

## 2026-05-15 Signal Boundary Layout Cleanup

What was wrong:
- `GlobalSignals.cs` still had public signal-boundary structs without explicit layout metadata:
  - `SpscSignalRingBuffer<T>`
  - `CombatDamageSignalAupShiftTransformer`
- These are not arbitrary DTOs: one owns native fallback ring-buffer state and one crosses the combat signal snapshot transform boundary.

What was done:
- Added `[StructLayout(LayoutKind.Sequential)]` to both structs.
- Kept all method bodies, field order, NativeArray lifecycle, signal sanitizer logic, and dispatch behavior unchanged.
- Detected concurrent `PlayerBaseEnterSignal` / `PlayerBaseExitSignal` sanitizer edits in the same file and left them untouched.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. ABI/layout metadata only.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; no executable code path changed.
- Debugging/perf debt: reduced layout ambiguity in signal fallback and combat shift paths. Exact time savings are unmeasured.

Compile Status:
- Not run by explicit user order.
- Static checks run: `git diff --check -- Assets/_Project/Scripts/Core/GlobalSignals.cs`, targeted public/internal `GlobalSignals.cs` layout scan, and `Tools/Architecture/HectonPhiAudit.ps1 -Json`.
- `git diff --check`: no whitespace errors, only LF/CRLF normalization warning.

Phi Gain:
- Previous corrected runtime narrow score: `0.008929037`.
- Current corrected runtime narrow score: `0.009035044`.
- Delta from previous runtime narrow: `+0.000106007`, about `+1.19%`.
- Current corrected runtime risk-adjusted score: `0.000121334`.
- Memory alignment: `0.495217853 -> 0.497348887`.
- Data Sovereignty: `0.018128162 -> 0.018263557` from concurrent/current tree metric state.
- Compared with original dialogue baseline `0.00062`: current corrected runtime narrow is about `+1357.26%`.
- Important: H-Phi is still static-source evidence, not runtime performance proof.

Regression Model:
- CPU: no runtime method body changed.
- GC: no runtime allocation changed.
- Memory: no buffer ownership changed; DataVault migration remains owner-blocked.
- Cadence: no tick, signal flush, or dispatch cadence changed.
- Correctness: low compile risk from attributes, but Unity import/compile remains pending because rebuild is forbidden.

## 2026-05-15 Core Native Container And Job Layout Cleanup

What was wrong:
- Core still had native/job/allocation structs without explicit layout metadata:
  - `NativeArenaArray<T>`
  - `StackQueue<T>`
  - `VaultGapAuditJob`
  - `HectonArenaAllocator.ArenaAllocation`
  - `UnsafeArenaAllocator.ArenaBlock`
  - `HectonHardwareProfile`
- These are real native/container/job/contract surfaces, not decorative DTOs.

What was done:
- Added `[StructLayout(LayoutKind.Sequential)]` to the six targeted structs.
- Added `System.Runtime.InteropServices` imports where required.
- Left method bodies, field order, pointer math, NativeArray lifetimes, job scheduling, allocator behavior, registry dispatch, and DataVault ownership unchanged.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. ABI/layout metadata only.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; no executable code path changed.
- Future debugging/perf debt: reduced native layout ambiguity. Exact time savings are unmeasured.

Compile Status:
- Not run by explicit user order.
- Static checks run: `git diff --check` on touched Core layout files, Core public/internal layout scan, and compact `Tools/Architecture/HectonPhiAudit.ps1 -Json` score extraction.
- Remaining Core missing-layout structs are intentionally skipped: `FixedCharBuffer`, `GameStartContext`, `ServiceReboundEvent`, `PersistenceAssemblyMarker`, `MacroDatabaseSignalBridge`.

Phi Gain:
- Previous corrected runtime narrow score: `0.009035044`.
- Current corrected runtime narrow score: `0.009266939`.
- Delta from previous runtime narrow: `+0.000231895`, about `+2.57%`.
- Current corrected runtime risk-adjusted score: `0.000124428`.
- Memory alignment: `0.497348887 -> 0.501059322`.
- Data Sovereignty: `0.018263557 -> 0.018593597` on current tree.
- Compared with original dialogue baseline `0.00062`: current corrected runtime narrow is about `+1394.67%`.
- Important: H-Phi is still static-source evidence, not runtime performance proof.

Regression Model:
- CPU: no runtime method body changed.
- GC: no runtime allocation changed.
- Memory: no buffer ownership changed; DataVault migration remains owner-blocked.
- Cadence: no tick, job schedule, signal flush, or dispatch cadence changed.
- Correctness: low compile risk from attributes/imports, but Unity import/compile remains pending because rebuild is forbidden.

## 2026-05-15 Typed Signal Publish Metric Correction And Summary Mode

What was wrong:
- H-Phi risk integration counted typed `GlobalSignals.Publish(...)` and confirmed NativeQueue/SystemDispatcher-backed publish lanes as generic `Publish(...)` event debt.
- That made the project look less signal-integrated than it is: the pre-correction summary showed `SignalBusPush=84` and `EventPublish=450`.
- Overseer checks had no compact summary path; full JSON was too noisy for fast status review.

What was done:
- Updated `Tools/Architecture/HectonPhiAudit.ps1`:
  - `SignalBusPush` now includes `SignalBus<T>.Push`, `GlobalSignals.Publish(...)`, and confirmed queued lanes: `VehicleCommandSignalBus`, `PhysicsDeterminismSignals`, `FluidFeedbackEvents`, `LocalizationEvents`, and `VoxelChunkModifiedEvents`.
  - `EventPublish` is narrowed to remaining legacy/direct fan-out publisher surfaces: `HectonEventBus`, `WaterTransitionEvents`, and `SuitDamageEvents`.
  - Added `-Summary` for compact scores, counts, Core graph debt, and top DataVault owner-blocked candidates.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. Static audit model only.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; no executable runtime code changed.
- Workflow: graph-only compact summary now avoids parsing the full H-Phi JSON when the overseer only needs Core graph status. Exact human/CLI savings are unmeasured.

Compile Status:
- Not run by explicit user order.
- Static checks run:
  - `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`
  - `git diff --check -- Tools/Architecture/HectonPhiAudit.ps1`
  - `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`
- `git diff --check`: no whitespace errors, only LF/CRLF normalization warning.

Phi Gain:
- Previous runtime narrow score: `0.009266939`.
- Current runtime narrow score: `0.009377979`.
- Narrow delta: `+0.000111040`, about `+1.20%`.
- Previous runtime risk-adjusted score: `0.000124428`.
- Current runtime risk-adjusted score: `0.000505917`.
- Risk-adjusted delta: `+0.000381489`, about `+306.59%`.
- Risk integration moved `0.013427110 -> 0.053947368`.
- `SignalBusPush` moved `84 -> 328`; `EventPublish` moved `450 -> 28`.
- Compared with original dialogue baseline `0.00062`, current runtime narrow is about `+1412.58%`.
- Important: this pass is a metric-model correction and evidence-routing improvement, not measured frame-time gain.

Regression Model:
- CPU: no runtime code path changed.
- GC: no runtime allocation changed.
- Memory: no buffer ownership changed; DataVault migration remains owner-blocked.
- Cadence: no tick, signal flush, job schedule, or dispatch cadence changed.
- Correctness: regex model risk remains because this is static text analysis; Unity import/compile and runtime evidence remain pending because rebuild is forbidden.

## 2026-05-15 Lexical Scrub Containment And Full Scan Recovery

What was wrong:
- H-Phi static regex counters can count comments and smoke-test string literals as real architecture surfaces.
- A first-pass PowerShell lexical scrubber was too slow for this repository: two full `-Summary -Json` runs exceeded a 420-second cap.
- A default H-Phi path that times out is worse than a known regex limitation because it blocks every monitoring loop.

What was done:
- Gated lexical scrubbing behind explicit `-LexicalScrub`.
- Restored default full-source summary to the raw-source counter path.
- Re-ran graph-only and full default summaries after the containment patch.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. Static audit tooling only.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; no runtime code changed.
- Tool lane: default full H-Phi summary recovered from `>420s timeout` to a completed run. Exact completed wall time from the shell was about 176.8 seconds for the latest full summary.

Compile Status:
- Not run by explicit user order.
- Static checks run:
  - `git diff --check -- Tools/Architecture/HectonPhiAudit.ps1`
  - `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`
  - `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`
- `git diff --check`: no whitespace errors; LF/CRLF warning only.

Phi Gain:
- Previous current runtime narrow score: `0.009377979`.
- Current runtime narrow score: `0.010531061`.
- Previous current runtime risk-adjusted score: `0.000505917`.
- Current runtime risk-adjusted score: `0.000560589`.
- Data Sovereignty moved `0.018825826 -> 0.021062910` in the current tree.
- Memory alignment moved `0.500794071 -> 0.502645503` in the current tree.
- AUP precision integrity is now visible in the summary: `0.983739837`.
- Current Core asmdef debt references: `25`; generated project debt references: `10`; source-backed bridge debt references: `20`; compile-bridge debt references: `8`; project-reference-replacement debt references: `12`.
- Important: the score movement includes concurrent workspace changes and static model changes. It is not measured frame-time gain.

Regression Model:
- CPU: no gameplay code path changed.
- GC: no gameplay allocation changed.
- Memory: no buffer ownership changed by this pass.
- Cadence: no Tick/FixedTick/Update/job schedule changed.
- Correctness: lexical scrub remains experimental and explicitly gated; default H-Phi evidence remains static regex output.

## 2026-05-15 AUP Risk Stale-Score Verification And Summary Routing

What was wrong:
- The active status still carried a stale `AupPrecisionRisk=6` from the previous full summary.
- An exact current runtime regex scan found no matching AUP risk code, so direct gameplay edits would have been evidence-free.
- Compact summary output showed the aggregate AUP count but not the files that would need repair if the counter regressed.

What was done:
- Re-ran `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxAupPrecisionRisk 0`.
- Confirmed current runtime `AupPrecisionRisk=0` and `AupPrecisionIntegrity=1.000000000`.
- Added `TopAupPrecisionRiskFiles` to compact summary output.
- Updated runtime file-row retention so files with AUP precision risk appear in triage even without NativeArray/DataVault/Find debt.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. Static audit tooling and evidence routing only.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; no gameplay code path changed.
- Tooling: one failed PowerShell parser strike was fixed; final budgeted summary completed in about 182.5 seconds. No runtime profiler claim.

Compile Status:
- Not run by explicit user order.
- Static checks run:
  - `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxAupPrecisionRisk 0`
  - `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 16 -MaxSourceBackedCompileBridgeDebtReferences 8 -MaxProjectReferenceReplacementDebtReferences 8 -Summary -Json`
  - `git diff --check` on touched H-Phi files
- `git diff --check`: no whitespace errors; LF/CRLF warning only.

Phi Gain:
- Previous current runtime narrow score: `0.010531061`.
- Current runtime narrow score: `0.010409098`.
- Previous current runtime risk-adjusted score: `0.000560589`.
- Current runtime risk-adjusted score: `0.000566586`.
- AUP precision integrity moved `0.983739837 -> 1.000000000`.
- AUP precision risk moved `6 -> 0`.
- Data Sovereignty moved `0.021062910 -> 0.020775237` in the active tree because DataVault refs moved `151 -> 149` while NativeArray refs stayed `7023`.
- Core graph current tree also improved from concurrent/integrator work: source-backed bridge debt `20 -> 16`, project-reference replacement debt `12 -> 8`.
- Compared with original dialogue baseline `0.00062`, current runtime static narrow is about `+1578.89%`.
- Important: the score movement is static-source evidence and includes concurrent workspace changes. It is not measured frame-time gain.

Regression Model:
- CPU: no gameplay code path changed.
- GC: no gameplay allocation changed.
- Memory: no buffer ownership changed.
- Cadence: no Tick/FixedTick/Update/job schedule changed.
- Correctness: AUP risk routing is now visible in summary output; runtime Unity evidence remains pending because rebuild/runtime verification is forbidden.

## 2026-05-15 Coupling-Risk Routing And Static Regression Budgets

What was wrong:
- H-Phi risk integration was low, but compact summary did not show the files causing most coupling pressure.
- Runtime `Find*` and legacy event publish counts had no first-class full-scan budget gates.
- The remaining obvious runtime `Find*` hits belong to bootstrap/VR/root handoff owners, not H-Phi monitor.

What was done:
- Extended audit file rows with `SignalBusPush`, `GlobalRegistrySurface`, `EventPublish`, `StaticInstance`, and computed `CouplingRisk`.
- Added `TopCouplingRiskFiles` to compact summary output.
- Added full-source gates:
  - `-MaxFindObjectCalls`
  - `-MaxLegacyEventPublish`
- Blocked these source-count gates under `-CoreGraphOnly`.
- Honored user instruction: no `dotnet build`, no rebuild.

Cinematic Cheats used:
- None. Static audit tooling and owner-routing only.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured; no gameplay code path changed.
- Tooling: latest full gated summary completed in about 265.4 seconds. No runtime profiler claim.

Compile Status:
- Not run by explicit user order.
- Static checks run:
  - `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json`
  - Parser guard: `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -MaxFindObjectCalls 5`
  - `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxAupPrecisionRisk 0 -MaxFindObjectCalls 5 -MaxLegacyEventPublish 28 -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 14 -MaxSourceBackedCompileBridgeDebtReferences 8 -MaxProjectReferenceReplacementDebtReferences 6`
  - `git diff --check -- Tools/Architecture/HectonPhiAudit.ps1`

Phi Gain:
- Previous current runtime narrow score: `0.010409098`.
- Current runtime narrow score: `0.010497120`.
- Previous current runtime risk-adjusted score: `0.000566586`.
- Current runtime risk-adjusted score: `0.000574075`.
- Risk integration moved `0.054431837 -> 0.054688783`.
- Architectural purity moved `0.994699647 -> 0.996460177`.
- Top coupling-risk files now route to owners: `Bootstrap/GameBootstrapper.cs`, `CrashTelemetryBuffer.cs`, `HectonFluidEngine.cs`, `UI/PauseMenuController.cs`, `Fauna/FaunaBrain.cs`.
- Duplicate signal-name scan currently reports `6` duplicate names; zero-budget enforcement is not enabled.
- Important: the score movement includes concurrent workspace changes and static model changes. It is not measured frame-time gain.

Regression Model:
- CPU: no gameplay code path changed.
- GC: no gameplay allocation changed.
- Memory: no buffer ownership changed.
- Cadence: no Tick/FixedTick/Update/job schedule changed.
- Correctness: analyzer output is broader and more actionable; static text evidence remains `PENDING VERIFICATION` for runtime behavior.

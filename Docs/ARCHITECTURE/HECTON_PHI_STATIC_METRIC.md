# Hecton Phi Static Metric

Date: 2026-05-15
Status: STATIC METRIC CONTRACT / RUNTIME PROOF REQUIRED

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

Current DOC_GLOBAL boundary (2026-05-20 R45): `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` is present and is the latest local static root/architecture R43/R44 residue, proof-artifact wording, and source-counter correction. R44 remains the prior internal-residue/exact-route-field/proof-wording correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R43_ROOT_ARCHITECTURE_ROUTE_CARD_AND_COUNTER_RESIDUE_LOCAL.md`; R42 remains the prior counter/route-boundary/proof-label correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R42_ROOT_ARCHITECTURE_COUNTER_AND_ROUTE_BOUNDARY_LOCAL.md`; R41 remains the prior global-authority/internal-residue correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R41_ROOT_ARCHITECTURE_GLOBAL_AUTHORITY_INTERNAL_RESIDUE_LOCAL.md`; R40 remains the prior R38-residue/source-counter correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R40_ROOT_ARCHITECTURE_R38_RESIDUE_AND_COUNTER_REFRESH_LOCAL.md`; R39 remains the prior authority-counter/proof-wording correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R39_ROOT_ARCHITECTURE_AUTHORITY_COUNTER_AND_PROOF_WORDING_LOCAL.md`; R38 remains the prior source-counter drift and boundary correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R38_ROOT_ARCHITECTURE_SOURCE_COUNTER_DRIFT_AND_BOUNDARY_LOCAL.md`; R37 remains the prior artifact-path/proof-wording/source-counter correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R37_ROOT_ARCHITECTURE_ARTIFACT_PATHS_AND_COUNTERS_LOCAL.md`; R36 remains the prior authority-spine/domain-map correction at `Docs/Reports/2026-05-20_DOCUMENTATION_R36_ROOT_ARCHITECTURE_AUTHORITY_SPINE_LOCAL.md`; R35 remains the prior R4/counter-residue correction at `Docs/Reports/2026-05-19_DOCUMENTATION_R35_ROOT_ARCHITECTURE_R4_AND_COUNTER_RESIDUE_LOCAL.md`; R34 remains the older source-counter and physical-line refresh, superseded by R37/R38/R39/R40/R41/R42/R43/R44/R45 where exact counts, route-card fields, AtlasCheck status, or proof wording differ. R33 remains the prior R32-residue/source-anchor correction; R32 remains the prior R4/proof-wording correction; R31 remains the prior current-boundary propagation layer; R30 remains the prior internal-currentness layer; R29 remains the prior stale-gate/global-authority layer; R28 remains the prior interior-boundary layer; and R27 is historical source-counter/index evidence superseded by R34/R37/R38/R39/R40/R41/R42/R43/R44/R45.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Evidence Class: STATIC_SOURCE
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; H?; ??-??; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

## Purpose

H-Phi is the project architecture hygiene metric for coupling, tick discipline,
data ownership, and memory layout discipline.

It is not a profiler result, compile result, Unity Console result, or visual
quality proof. It is a fast static signal that tells whether HECTON-8 is moving
toward controllable data flow or toward a fused dependency mass.

The authoritative implementation is:

```powershell
Tools/Architecture/HectonPhiAudit.ps1
```

Use dated reports only as snapshots. This file defines the metric contract.

## Why It Matters

HECTON-8 depends on hard boundaries:

- Core should not become a leaf-domain aggregator.
- Runtime systems should prefer dispatcher ticks, jobs, and signal lanes over
  ad hoc Unity loop ownership.
- Native buffers should have visible ownership through the Vault/access surface
  when they cross system boundaries.
- DTO and job payload layouts should be explicit enough for binary persistence,
  Burst-facing code, and black-box telemetry to be auditable.

On low-end hardware, high H-Phi reduces hidden compile/runtime coupling and
keeps systems easier to shed, fake, or decimate by LOD. On high-end hardware,
the same boundaries let leaf systems spend saved budget on visual overkill
without contaminating Core.

## Runtime Static Scope

The runtime score scans:

```text
Assets/_Project/Scripts/**/*.cs
```

Runtime H-Phi excludes files under `Scripts/Editor` and strips `#if UNITY_EDITOR`
blocks before runtime scoring. All-source and editor-only counters remain in the
JSON output for hygiene review, but they are not shipped-runtime score inputs.

By default the full source scan counts raw source text. `-LexicalScrub` masks
comments and string/char literals before counting, but it is experimental in the
PowerShell implementation and is not a monitoring default until a compiled or
cached analyzer replaces it.

## Coefficients

The audit computes these coefficients from static text counters.

| Coefficient | Formula | Meaning |
|---|---|---|
| `NarrowIntegration` | `SignalBusPush / (SignalBusPush + GlobalRegistryGet)` | Prompt-continuity signal-vs-registry score. |
| `RiskIntegration` | `SignalBusPush / (SignalBusPush + GlobalRegistrySurface + EventPublish + StaticInstance + FindObjectCalls + GetComponentCalls)` | Broader coupling-risk score. |
| `ArchitecturalPurity` | `(ISlowTickable + IJob) / (UnityUpdateMethods + ISlowTickable + IJob)` | Dispatcher/job discipline versus stray Unity loop ownership. The canonical dispatcher shell is reported separately and does not count as debt. |
| `ArchitecturalPurityExpanded` | `(ISlowTickable + IJob + ITickable + IFixedTickable) / (UnityUpdateMethods + ISlowTickable + IJob + ITickable + IFixedTickable)` | Tick-interface-inclusive purity. |
| `DataSovereignty` | `GlobalDataVaultRefs / (GlobalDataVaultRefs + NativeArrayRefs)` | Visible Vault/access ownership versus scattered native buffer references. |
| `MemoryAlignment` | `StructLayoutAttributes / StructDeclarations` | Explicit layout coverage for structs. |
| `BinarySafeRatio` | `BinaryBlittableSafe / StructDeclarations` | Explicit binary-safe coverage. This is diagnostic, not part of the base product. |
| `AupPrecisionIntegrity` | `AupPrecisionSafe / (AupPrecisionSafe + AupPrecisionRisk)` | Double-safe AUP bridge usage versus legacy float/offset-risk surfaces. |

The current base products are:

```text
HPhiStaticNarrow = NarrowIntegration * ArchitecturalPurity * DataSovereignty * MemoryAlignment
HPhiStaticRisk   = RiskIntegration   * ArchitecturalPurity * DataSovereignty * MemoryAlignment * AupPrecisionIntegrity
```

All ratios return `0.0` when the denominator is zero. Scores are rounded to
nine decimals by the tool.

## Counter Definitions

The tool uses regex-based static counters. Important surfaces:

- `SignalBusPush`: direct `SignalBus<T>.Push`, typed
  `GlobalSignals.Publish(...)`, and confirmed NativeQueue/SystemDispatcher-backed
  publish lanes (`VehicleCommandSignalBus`, `PhysicsDeterminismSignals`,
  `FluidFeedbackEvents`, `LocalizationEvents`, `VoxelChunkModifiedEvents`).
- `GlobalRegistryGet`: `GlobalRegistry.Get<T>`
- `GlobalRegistrySurface`: any `GlobalRegistry.`
- `EventPublish`: remaining legacy/direct fan-out publisher surfaces:
  `HectonEventBus`, `WaterTransitionEvents`, and `SuitDamageEvents`.
- `UnityUpdateMethodsRaw`: all method declarations for `Update`, `LateUpdate`,
  and `FixedUpdate`.
- `UnityLoopShellMethods`: raw Unity loop declarations inside
  `Core/SystemDispatcher.cs`. This is the bounded Unity-to-dispatcher shell
  required by Unity's player loop.
- `UnityUpdateMethods`: non-exempt runtime method declarations for `Update`,
  `LateUpdate`, and `FixedUpdate`. This is the actual H-Phi debt counter used by
  `ArchitecturalPurity`.
- `GlobalDataVaultRefs`: `GlobalDataVault`, `IDataVault`,
  `VaultBufferHandle<T>`, `GetBuffer<T>`, `TryGetBuffer`, buffer handle
  accessors, and `ResolveBuffer<T>`
- `NativeArrayRefs`: `NativeArray<T>`
- `StructDeclarations`: `struct Name`
- `StructLayoutAttributes`: `[StructLayout(...)]`
- `BinaryBlittableSafe`: `[BinaryBlittableSafe]`
- `AupPrecisionSafe`: double-safe AUP surfaces such as
  `CurrentTotalOffsetDouble`, `ToAbsoluteUniversePositionDouble3`,
  `ToUniverseSpaceDouble3`, `ToRuntimeSpaceDouble3`, `FromAbsolutePosition`,
  AUP `DistanceSq(...)`, and `ToRuntimeSpace(double3)`.
- `AupPrecisionRisk`: qualified legacy AUP bridge calls, direct committed-offset
  component reads, explicit `(float3)AUP` casts, and `Vector3` universe root
  declarations.
- `FindObjectCalls`: Unity scene/resource discovery calls such as
  `FindObjectOfType`, `FindAnyObjectByType`, `GameObject.Find`, and related
  variants
- `GetComponentCalls`: `GetComponent<T>` family calls
- `DisposeCalls`: `.Dispose(...)`

Because this is static text analysis, a score is a trend and tripwire, not proof
that a hot path is allocation-free or fast.

## 2026-05-19 Global Authority Anti-Gaming Overlay

H-Phi can be gamed. Do not do that.

The metric rewards visible `SignalBus<T>` and DataVault/access surfaces because
those are usually better than hidden direct coupling and scattered native buffer
ownership. It does not prove the new global route is the right route.

Additional interpretation rules:

- A `SignalBusPush` increase is valid only when the lane has owner, phase,
  capacity, overflow policy, retention policy, payload layout, and telemetry.
- A `DataVaultRefs` increase is valid only when the buffer has `BufferID`,
  `SystemID`, generation handling, lifetime, disposal/release behavior, and
  stale-handle behavior.
- A `GlobalRegistrySurface` decrease is valid only when dependencies moved to
  cached interfaces, typed signals, or owned snapshots, not to hidden singletons.
- A `HectonEventBus` decrease matters only when first-party hot traffic moved to
  typed lanes or direct owner interfaces. Moving mod/API events into gameplay
  signals is not an improvement.
- A high H-Phi score with no runtime/profiler/GC proof is still `PENDING
  VERIFICATION`.

Cross-reference:

- `GLOBAL_AUTHORITY_BOUNDARIES.md`
- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
- `QUALITY_GATES.md`

## Core Graph H-Phi

The same tool also audits the Core dependency graph:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly
```

The graph audit reads:

- `Assets/_Project/Scripts/Hecton8.Core.asmdef`
- `Hecton8.Core.csproj`
- `Directory.Build.props`
- `Directory.Build.targets`

It classifies Core asmdef references as:

- `CoreFamily`: Core and Core sub-assemblies.
- `MathNative`: Unity Mathematics, Burst, and Collections.
- `Contract`: contract assemblies.
- `LeafDomain`: first-party non-contract leaf assemblies.
- `PackageOrUnity`: package, UnityEngine, or third-party surface.
- `Other`: anything else.

Asmdef graph debt is:

```text
LeafDomain + PackageOrUnity + Other
```

Generated project references are classified as:

- `ContractOrCore`
- `FirstPartyLeaf`
- `PackageOrGenerated`

Generated-project graph debt is:

```text
FirstPartyLeaf + PackageOrGenerated
```

Source-backed bridge references are read from the Core item group in
`Directory.Build.targets`. They are classified with the same rules as Core
asmdef references. Bridge graph debt is:

```text
LeafDomain + PackageOrUnity + Other
```

Bridge rows also carry a lane:

- `CoreCompileBridge`: source-backed references used by the Core compile bridge.
- `ProjectReferenceReplacement`: direct assembly references used when generated
  project references are disabled for the medic lane.

The graph audit also verifies the Core medic build gate in
`Directory.Build.props`:

```text
BuildProjectReferences=false
BuildInParallel=false
Opt-in: HectonBuildProjectReferences=true
```

This gate is build-lane isolation, not runtime behavior. It keeps Core medic
verification focused when generated projects contain package/vendor references.

## Budget Gates

Use explicit budgets to prevent new Core graph debt:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 14 -MaxSourceBackedCompileBridgeDebtReferences 8 -MaxProjectReferenceReplacementDebtReferences 6
```
The numbers above are the 2026-05-15 known baseline after removing three
unused Core asmdef debt references and nine unused source-backed bridge
references from the integrator pass. The total bridge debt is 14: 8
Core-compile-bridge debt refs and 6 project-reference replacement debt refs.
They are not a target. Lower them only after staged contract extraction and
compile verification.

Duplicate signal-name debt is a hard zero-regression gate. The 2026-05-15
integrator pass removed the six known duplicate `*Signal` struct names by
renaming non-canonical payloads:

- world culling camera contract payloads now use `InstanceCulling*Signal`;
- gameplay-local combat queue payload now uses `CombatDamageRequest`;
- habitat callback damage payload now uses `HabitatDamageSignal`;
- player stress interaction payload now uses `PlayerInteractionStressSignal`;
- the Core macro-database hydration lane now uses
  `MacroDatabaseSectorHydrationSignal` while the contracts DLL-compatible sink
  keeps `SectorHydratedSignal`.

This was source-observed and CLI-compile claimed by prior report text. Treat it as compile proof only when paired with a linked command-output artifact and timestamp. It is not runtime or Unity-import proof.

Unity loop debt is also a hard zero-regression gate. A 2026-05-19 SHINOBU_02
read-only source spot check found no runtime `Update`, `LateUpdate`, or
`FixedUpdate` declarations under `Assets/_Project/Scripts`; observed declarations
were Editor-only tuner windows and the bounded `SystemDispatcher` player-loop
shell. Treat archived `UnityUpdateMethodsRaw` / `UnityLoopShellMethods` counters
as capture-time artifacts until H-Phi is rerun. New gameplay/system `Update`,
`LateUpdate`, or `FixedUpdate` methods must fail `-MaxUnityUpdateMethods 0`
unless the integrator updates this contract with a bounded dispatcher-shell
justification.

## 2026-05-15 Archived Static Baseline

Artifact: `../Archive/Batch007/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_224426_CurrentDiskBudgetGate22.json`
with exit summary
`../Archive/Batch007/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_224426_CurrentDiskBudgetGate22.exit.txt`.
Result: `EXIT=0` on a full source summary JSON budget run at
`2026-05-15 22:46:22 +04:00`.

Archived 2026-05-15 score floors passed at capture time; rerun H-Phi on the active workspace before treating these as current:

- `DataSovereignty=0.021306032`
- `MemoryAlignment=0.506309148`
- `RuntimeHPhiRisk=0.000636091`
- `RiskIntegration=0.058965935`

Archived 2026-05-15 static counters passed at capture time; these are not current compile, Unity, profiler, GC, player-build, or current source-counter proof:

- `GlobalRegistrySurface=5060/5060`
- `GetComponentCalls=321/321`
- `NativeArrayRefs=7074/7074`
- `ManagedFormatSurface=534/534`
- `JobCompleteSurface=58/58`
- `PrimaryManagedRuntimeRisk=147/147`
- `DuplicateSignalNames=0`
- `UnityUpdateMethods=0`
- `LegacyEventPublish=28/28`
- `LinqSurface=3/3`
- `CoroutineSurface=0/0`
- `AupPrecisionRisk=0`

CurrentDiskBudgetGate22 Core graph debt was at the accepted ceiling:
`CoreAsmdefDebtReferenceCount=25`, `GeneratedProjectDebtReferenceCount=10`,
`SourceBackedBridgeDebtReferenceCount=14`,
`SourceBackedCompileBridgeDebtReferenceCount=8`, and
`ProjectReferenceReplacementDebtReferenceCount=6`.

This supersedes earlier same-day MemoryAlignment failure artifacts, the interim
R47 `GlobalRegistrySurface=5076 > 5075` failure, R49, R52, R53, R54, and the
integration CurrentDiskBudgetGate19 slice as current-disk static H-Phi status.
Pair it with
`../Archive/Batch007/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_224641_CurrentDisk53.log`
for CLI compile evidence. It is not Unity import proof, Play Mode proof,
profiler proof, GC proof, player-build proof, or visual quality proof.

## 2026-05-19 HFI Static Artifact Orientation

Prior scalar HFI orientation comes from `../Archive/Batch009/AgentLogs/HPhi_SHINOBU_02_current2.json`
and is summarized by `../Reports/2026-05-19_HFI_AUDIT_H_PHI_AND_PROJECT_RISK.md`.
Archived SHINOBU_02 artifacts reached current36 for SignalCritical audit counts and
current21 for Full audit/trend counts in their capture sequence. Current37 proved only that a stale Core include
shield works before the Core compile hits external bridge errors; Current38 removed the Core-owned
runtime `Pack = 1` weather/ocean DTOs in source. Current22 through Current38 cleanup still lacks a
fresh scalar H-Phi rerun, and R39/R40 did not create a guarded H-Phi trend artifact. This is static-source/history evidence only. It is not a green compile, Unity
import, Play Mode, profiler, GCMonitor, player-build, scene-wiring, save/load,
or visual proof.

Captured at `2026-05-18 18:15:42 +04:00`:

- `NarrowIntegration=1.0`
- `RiskIntegration=0.077054795`
- `ArchitecturalPurity=1.0`
- `DataSovereignty=0.203977518`
- `MemoryAlignment=0.586269524`
- `AupPrecisionIntegrity=1.0`
- `HPhiStaticNarrow=0.119585803`
- `HPhiStaticRisk=0.009214659`

Interpretation: this scalar HFI snapshot improves data-sovereignty and
memory-alignment orientation versus the archived 2026-05-15 baseline, but the
global-authority surface is still a migration risk. Use
`GLOBAL_AUTHORITY_BOUNDARIES.md` and `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
before treating H-Phi movement as architectural acceptance.

## 2026-05-19 SHINOBU_02 Signal Audit And Trend Overlay

Evidence class: `STATIC_SOURCE_HISTORY` / `STATIC_SOURCE_CLASSIFIED`.

- `../Archive/Batch010/AgentLogs/HPhiTrend_SHINOBU_02_current21.md` scanned `131` artifacts and `393` metric series with signal-audit inclusion. It is archived history analysis only, not a fresh H-Phi run. The active `Docs/AgentLogs/...` copy is absent after Batch010 archival.
- Archived SHINOBU_02 SignalCritical audit artifact: `../Archive/Batch010/AgentLogs/SignalBusContractAuditCli_SHINOBU_02_signalcritical_current36.md` reported files `8`, shaders `66`, errors `0`, warnings `0`, infos `13`, runtime signal `Pack = 1` layouts `0`, transitive runtime signal `Pack = 1` field hits `0`, and managed event surface hits `0`. It is historical static-source evidence only; do not call it latest/current unless rerun with artifact path, command/tool, timestamp, environment, and output.
- Archived SHINOBU_02 Full audit artifact: `../Archive/Batch010/AgentLogs/SignalBusContractAuditCli_SHINOBU_02_full_current21.md` reported files `1761`, shaders `62`, errors `0`, warnings `432`, infos `454`, confirmed/probable errors at confidence `>= 90` `0`, and project-wide `Pack = 1` layouts `230`. It is historical static-source evidence only; do not call it latest/current unless rerun with artifact path, command/tool, timestamp, environment, and output.
- The same current21 trend still reports duplicate signal-name debt at `10` against the hard `MaxDuplicateSignalNames=0` budget. Do not mistake the clean SignalCritical lane for full-project duplicate closeout.
- Current22 through Current38 were source/doc-only historical movements: Core exact `Pack = 1` rows were reduced by source edits, but R39/R40 require fresh guarded audit/trend artifacts before any current counter is reported. Current36 adds a scoped SignalCritical audit artifact, and Current37 records the guarded Core compile bridge failure after the stale include shield. None of Current22-Current38 is an H-Phi/Full-audit trend point because guarded reruns were not executed.
- Do not tighten or report current H-Phi budgets from historical Current22-Current38 source/doc-only movement until the guarded audit/trend scripts run and produce fresh artifacts.

## 2026-05-15 DOC_HONEST_ANALYSIS R3 Core Graph Prune

Artifact: `../Archive/Batch006/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CoreGraphAfterGprPrune.json`
with exit summary
`../Archive/Batch006/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CoreGraphAfterGprPrune.exit.txt`.

Result: `EXIT=0` on a Core graph summary after clearing transient unused
`Hecton8.World.GPR` Core asmdef drift; the current file/index contain no such
Core reference.

Current Core graph debt after the prune:

- `CoreAsmdefDebtReferenceCount=25`
- `GeneratedProjectDebtReferenceCount=10`
- `SourceBackedBridgeDebtReferenceCount=14`
- `SourceBackedCompileBridgeDebtReferenceCount=8`
- `ProjectReferenceReplacementDebtReferenceCount=6`
- unused Core asmdef reference candidates: none reported by the optional scan

Compile follow-up:
`../Archive/Batch006/AgentLogs/Build_DOC_HONEST_ANALYSIS_R3_20260515_AfterGprAsmdefPrune_Hecton8Core.log`
reports `Hecton8.Core.csproj` CLI compile `EXIT=0`, `Build succeeded`,
`0 Warning(s)`, and `0 Error(s)`.

This is static/CLI evidence only. It is not Unity import proof, Play Mode proof,
profiler proof, GC proof, player-build proof, or visual-quality proof.

## Optional Unused Core Reference Scan

The Core graph audit can also run a static candidate scan:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -IncludeUnusedCoreReferenceScan -Json
```

This scan maps each Core asmdef debt reference to its source-backed asmdef,
collects declared type names, and searches the generated/source-backed Core
compile surface for external hits. A candidate means Core compile-surface text
did not reference the candidate assembly literal, namespace literal, or declared
types outside that assembly's own source files.

This is a pruning hint only. It is valid evidence for review order, not proof
that removing a reference is compile-safe. Removal still requires JSON/XML
parse checks and compile verification when the build lane is allowed.

## Interpreting Movement

Higher H-Phi usually means cleaner boundaries, but do not chase the number by
adding fake attributes or moving ownership blindly.

Valid improvements:

- Replace scene-wide lookup with explicit registration or existing registry
  interfaces.
- Move shared DTOs down to contract assemblies before removing leaf references.
- Add truthful `[StructLayout]` metadata where binary/job layout needs explicit
  proof and no serializer format changes.
- Move cross-system NativeArray ownership behind the DataVault/access surface
  only when BufferID, SystemID, generation, lifetime, disposal, and job handles
  are documented.

Invalid improvements:

- Adding leaf references to Core to silence compile errors.
- Adding `[BinaryBlittableSafe]` to managed/reference DTOs as metric theater.
- Removing asmdef references while Core source still directly uses leaf-owned
  types.
- Adding new global signals, registry slots, or Vault buffers only to raise the
  static score.
- Moving local scratch/native state into `GlobalDataVault` without a cross-domain
  ownership reason.
- Moving first-party hot traffic through `HectonEventBus` because it is convenient.
- Treating static H-Phi as Unity runtime, profiler, GC, player-build, or visual
  quality evidence.

## Required Evidence Language

Reports must state the evidence class:

- `STATIC_SOURCE`: H-Phi script output only.
- `CLI_COMPILE`: dotnet/Roslyn build evidence.
- `UNITY_IMPORT` / `UNITY_CONSOLE`: Unity editor evidence.
- `PLAYMODE` / `PROFILER` / `GCMONITOR` / `PLAYER_BUILD`: runtime evidence.

If only H-Phi was run, the correct claim is:

```text
Static H-Phi changed. Runtime quality remains pending verification.
```

## Commands

Full static audit:

```powershell
Tools/Architecture/HectonPhiAudit.ps1
```

JSON audit:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -Json
```

Compact summary audit:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json
```

The compact summary includes aggregate scores, Core graph debt, duplicate signal
name debt, top owner-blocked DataVault candidate files,
`TopAupPrecisionRiskFiles`, `TopCouplingRiskFiles`,
`TopPrimaryManagedRuntimeRiskFiles`, `ManagedRiskByRole`,
`DataVaultBacklogByDomain`, and `DataVaultBacklogByRole`.

Historical trend audit:

```powershell
Tools/Architecture/HectonPhiTrend.ps1
```

Optional trend including SignalBus static-audit counters:

```powershell
Tools/Architecture/HectonPhiTrend.ps1 -IncludeSignalAudit
```

The trend script reads existing `HPhi*.json` artifacts from active agent logs
and archives, flattens numeric scalar indicators, and reports first/last/delta,
min, and max per metric. It is history analysis only; it does not replace a fresh
`HectonPhiAudit.ps1` run.

Core graph only:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly
```

Compact Core graph only:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Summary -Json
```

Core graph budget gate:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 14 -MaxSourceBackedCompileBridgeDebtReferences 8 -MaxProjectReferenceReplacementDebtReferences 6
```

AUP precision budget gate:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxAupPrecisionRisk 0
```

Full H-Phi regression budget gate:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxAupPrecisionRisk 0 -MaxFindObjectCalls 0 -MaxLegacyEventPublish 28 -MaxDuplicateSignalNames 0 -MaxUnityUpdateMethods 0 -MaxGlobalRegistrySurface 5060 -MaxGetComponentCalls 321 -MaxNativeArrayRefs 7074 -MaxLinqSurface 3 -MaxCoroutineSurface 0 -MaxManagedFormatSurface 534 -MaxJobCompleteSurface 58 -MaxPrimaryManagedRuntimeRisk 147 -MaxOwnerBlockedNativeArrayRefs 6262 -MaxPrimaryOwnerBlockedNativeArrayRefs 5678 -MinDataSovereignty 0.021306000 -MinMemoryAlignment 0.506309000 -MinRuntimeHPhiRisk 0.000636000 -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 14 -MaxSourceBackedCompileBridgeDebtReferences 8 -MaxProjectReferenceReplacementDebtReferences 6
```

Source-count and score-floor gates require a full source scan. `-CoreGraphOnly`
rejects them by design so graph-only status cannot masquerade as full H-Phi
proof. The floors above are archived artifact-backed budget values from the
cited H-Phi report context; do not treat them as current R38/R39/R40 workspace
budgets until H-Phi is rerun on the active checkout. Domain owners should lower
debt and then tighten these floors.
Managed-runtime counters are static risk surfaces, not profiler/GC proof.
`PrimaryManagedRuntimeRisk` excludes editor, instrumentation, persistence, and
UI role buckets so smoke/diagnostic/save/UI debt remains visible without being
treated as the primary gameplay hot-path budget.
`OwnerBlockedNativeArrayRefs` counts NativeArray surface in runtime files with
zero Vault access surface. It is a migration backlog gate, not proof that every
NativeArray in the file should move to Vault.
`PrimaryOwnerBlockedNativeArrayRefs` applies the same backlog rule only to
primary runtime files, so UI, persistence, and instrumentation debt remains
visible without being treated as gameplay hot-path migration pressure.

Duplicate signal-name budget gate:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxDuplicateSignalNames 0
```

Unity loop debt budget gate:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxUnityUpdateMethods 0
```

Core graph with unused-reference candidates:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -IncludeUnusedCoreReferenceScan -Json
```

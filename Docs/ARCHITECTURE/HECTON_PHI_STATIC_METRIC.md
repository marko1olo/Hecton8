# Hecton Phi Static Metric

Date: 2026-05-15
Status: STATIC METRIC CONTRACT / RUNTIME PROOF REQUIRED
Evidence Class: STATIC_SOURCE

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

This was source and CLI-compile verified. It is not runtime or Unity-import
proof.

Unity loop debt is also a hard zero-regression gate. The only current raw Unity
loop declarations are the two `SystemDispatcher` shell methods that bridge
Unity's player loop into the project dispatcher. The audit reports them as
`UnityLoopShellMethods=2` and `UnityUpdateMethodsRaw=2`, but the debt counter is
`UnityUpdateMethods=0`. New gameplay/system `Update`, `LateUpdate`, or
`FixedUpdate` methods must fail `-MaxUnityUpdateMethods 0` unless the integrator
updates this contract with a bounded dispatcher-shell justification.

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
Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -MaxAupPrecisionRisk 0 -MaxFindObjectCalls 0 -MaxLegacyEventPublish 28 -MaxDuplicateSignalNames 0 -MaxUnityUpdateMethods 0 -MaxGlobalRegistrySurface 5081 -MaxGetComponentCalls 321 -MaxNativeArrayRefs 7074 -MaxLinqSurface 5 -MaxCoroutineSurface 0 -MaxManagedFormatSurface 677 -MaxJobCompleteSurface 61 -MaxPrimaryManagedRuntimeRisk 327 -MaxOwnerBlockedNativeArrayRefs 6262 -MaxPrimaryOwnerBlockedNativeArrayRefs 5678 -MinDataSovereignty 0.021306000 -MinMemoryAlignment 0.506043000 -MinRuntimeHPhiRisk 0.000628000 -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 14 -MaxSourceBackedCompileBridgeDebtReferences 8 -MaxProjectReferenceReplacementDebtReferences 6
```

Source-count and score-floor gates require a full source scan. `-CoreGraphOnly`
rejects them by design so graph-only status cannot masquerade as full H-Phi
proof. The current floors are deliberately just below the latest verified
static values; domain owners should lower debt and then tighten these floors.
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

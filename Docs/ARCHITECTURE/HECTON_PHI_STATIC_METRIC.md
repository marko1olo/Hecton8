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

## Coefficients

The audit computes these coefficients from static text counters.

| Coefficient | Formula | Meaning |
|---|---|---|
| `NarrowIntegration` | `SignalBusPush / (SignalBusPush + GlobalRegistryGet)` | Prompt-continuity signal-vs-registry score. |
| `RiskIntegration` | `SignalBusPush / (SignalBusPush + GlobalRegistrySurface + EventPublish + StaticInstance + FindObjectCalls + GetComponentCalls)` | Broader coupling-risk score. |
| `ArchitecturalPurity` | `(ISlowTickable + IJob) / (UnityUpdateMethods + ISlowTickable + IJob)` | Dispatcher/job discipline versus raw Unity loop methods. |
| `ArchitecturalPurityExpanded` | `(ISlowTickable + IJob + ITickable + IFixedTickable) / (UnityUpdateMethods + ISlowTickable + IJob + ITickable + IFixedTickable)` | Tick-interface-inclusive purity. |
| `DataSovereignty` | `GlobalDataVaultRefs / (GlobalDataVaultRefs + NativeArrayRefs)` | Visible Vault/access ownership versus scattered native buffer references. |
| `MemoryAlignment` | `StructLayoutAttributes / StructDeclarations` | Explicit layout coverage for structs. |
| `BinarySafeRatio` | `BinaryBlittableSafe / StructDeclarations` | Explicit binary-safe coverage. This is diagnostic, not part of the base product. |

The current base products are:

```text
HPhiStaticNarrow = NarrowIntegration * ArchitecturalPurity * DataSovereignty * MemoryAlignment
HPhiStaticRisk   = RiskIntegration   * ArchitecturalPurity * DataSovereignty * MemoryAlignment
```

All ratios return `0.0` when the denominator is zero. Scores are rounded to
nine decimals by the tool.

## Counter Definitions

The tool uses regex-based static counters. Important surfaces:

- `SignalBusPush`: `SignalBus<T>.Push`
- `GlobalRegistryGet`: `GlobalRegistry.Get<T>`
- `GlobalRegistrySurface`: any `GlobalRegistry.`
- `EventPublish`: `Publish(...)`
- `UnityUpdateMethods`: method declarations for `Update`, `LateUpdate`, and
  `FixedUpdate`
- `GlobalDataVaultRefs`: `GlobalDataVault`, `IDataVault`,
  `VaultBufferHandle<T>`, `GetBuffer<T>`, `TryGetBuffer`, buffer handle
  accessors, and `ResolveBuffer<T>`
- `NativeArrayRefs`: `NativeArray<T>`
- `StructDeclarations`: `struct Name`
- `StructLayoutAttributes`: `[StructLayout(...)]`
- `BinaryBlittableSafe`: `[BinaryBlittableSafe]`
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
Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 28 -MaxGeneratedProjectDebtReferences 10
```
The numbers above are the 2026-05-15 known baseline for the integrator pass.
They are not a target. Lower them only after staged contract extraction and
compile verification.

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

Core graph only:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly
```

Core graph budget gate:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 28 -MaxGeneratedProjectDebtReferences 10
```

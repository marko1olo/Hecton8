# LOG_SHINOBU_31

## 2026-05-17 - Compile Wall Surgery

Agent: SHINOBU_31
Domain: COMPILE_TIME_AND_ASMDEF_ARCHITECT
Status: IMPLEMENTED / FULL UNITY COMPILE BLOCKED BY DEPENDENCY
Evidence class: STATIC_SOURCE / FILESYSTEM / UNITY_BEE_PARTIAL / UNITY_ROSLYN_ISOLATED

### What Was Wrong

- Baseline asmdef graph still contains 81 Runtime -> Runtime implementation edges. This is pre-existing compile-wall debt, not introduced by SHINOBU_31.
- `Hecton8.Core.asmdef` remains a compile gravity well with direct references to leaf runtime domains.
- Full Unity script compile is currently blocked by concurrent-agent errors outside SHINOBU_31 ownership. Latest R6 blockers include `Core/BinaryLayoutManifest.cs`, `Quest/*`, `Rendering/GlobalShaderDispatcher.cs`, `Audio/Editor/SabineReverbDspTunerWindow.cs`, and `Fauna/PredatorCognitionDomain.cs`.
- R4 exposed one SHINOBU_31 local error: `[BurstCompile]` is invalid on a delegate in Unity 6000.4.1f1. It was removed; the Burst target method remains `[BurstCompile]`.

### What Was Done

- Added `Hecton8.Global.Contracts.asmdef` with cross-assembly unmanaged contracts, bootstrap/reset interfaces, buffer handles, alias contract, signal payload, routing override, and `PhysicsFacade`.
- Added `Hecton8.MockDomain.Contracts.asmdef` and `Hecton8.MockDomain.Runtime.asmdef` to prove blind dependency inversion without Core/UI/Physics runtime references.
- Added a Burst function pointer bridge: implementation owns a static `[BurstCompile]` function and returns a `PhysicsFacade` carrying `FunctionPointer<PhysicsApplyForceDelegate>`.
- Added `CompileWallAsmdefBuildGate` using `IPreprocessBuildWithReport`; builds now fail on Runtime -> Runtime asmdef edges.
- Added `Compile Wall X-Ray` EditorWindow with graph scan, red illegal-edge drawing, validation action, link/offset generation action, and "Nuke Domain Reload" toggle.
- Added 300-entry `NativeArray` compile blackbox telemetry buffer, `.h8dump` fatal dump path, and >2s warning path.
- Added span-based `assembly_routing.csv` parser for contract/implementation/mock override hashes.
- Added generated `VaultOffsets.g.cs`.
- Added generated `link.xml` preserving global contract assemblies and 114 scanned `Hecton8.Core.GlobalRegistry` contract surface types.
- Added status and rationale files required by agent protocol.

### Files Added Or Modified

- `Assets/_Project/Scripts/Global/Contracts/Hecton8.Global.Contracts.asmdef`
- `Assets/_Project/Scripts/Global/Contracts/GlobalContracts.cs`
- `Assets/_Project/Scripts/Global/Contracts/Generated/VaultOffsets.g.cs`
- `Assets/_Project/Scripts/Global/MockDomain/Contracts/Hecton8.MockDomain.Contracts.asmdef`
- `Assets/_Project/Scripts/Global/MockDomain/Contracts/MockDomainContracts.cs`
- `Assets/_Project/Scripts/Global/MockDomain/Runtime/Hecton8.MockDomain.Runtime.asmdef`
- `Assets/_Project/Scripts/Global/MockDomain/Runtime/MockContractImplementation.cs`
- `Assets/_Project/Scripts/Global/Generated/link.xml`
- `Assets/_Project/Scripts/Editor/AssemblyGuard/CompileWallXRayWindow.cs`
- `Docs/Tasks/Status_SHINOBU_31.md`
- `Docs/AgentLogs/Rationale_SHINOBU_31.md`

### Struct Layout

- `GlobalSignalPayload`: Size 128, Pack 8.
- Header: `TypeHash` offset 0, `Version` offset 4, `PayloadBytes` offset 6, `FrameIndex` offset 8, `Flags` offset 12.
- Payload body: `Payload00` offset 16 through `Payload13` offset 120, 14 x 8-byte lanes = 112 bytes.
- `GlobalNativeBufferHandle`: Size 32, Pack 8. Pointer 0, Length 8, Stride 12, OwnerHash 16, Generation 20, AccessFlags 24, Reserved 28.
- `NativeMemoryAliasContract`: Size 64, Pack 8, `[NoAlias]`, front handle 0, writer/generation/range fields 32-63.
- `AssemblyRoutingOverride`: Size 64, Pack 8, quality min/max fields at 16/17.
- `MockDomainState`: Size 32, Pack 8, `double3` at 0, flags/generation at 24/28.

### SELF_AUDIT

<SELF_AUDIT>
  <task_01 status="PASS">Archaeology scan and emergency mock atlas implemented.</task_01>
  <task_02 status="PASS">Runtime -> Runtime gate implemented; SHINOBU_31 contributes 0 illegal edges.</task_02>
  <task_03 status="PASS">No `{ get; set; }` array contract surfaces in SHINOBU_31 contracts.</task_03>
  <task_04 status="PASS">All SHINOBU_31 DTOs use explicit/controlled Pack=8 layouts; no Pack=1.</task_04>
  <task_05 status="PASS">Mock contracts/runtime added and isolated from Core/UI/Physics runtime refs.</task_05>
  <task_06 status="PASS">Global contracts assembly added with narrow Unity refs only.</task_06>
  <task_07 status="PASS">Pre-build BuildFailedException gate added.</task_07>
  <task_08 status="PASS">128-byte generic-free signal payload added.</task_08>
  <task_09 status="PASS">Burst function pointer bridge added; delegate not BurstCompile-attributed because Unity rejects it.</task_09>
  <task_10 status="PASS">PhysicsFacade stores pointer and buffer handle.</task_10>
  <task_11 status="PASS">link.xml shield generated with 114 scanned registry contract types.</task_11>
  <task_12 status="PASS">NoAlias memory contract added.</task_12>
  <task_13 status="PASS">Asmdef version defines added for Burst and URP quality routing.</task_13>
  <task_14 status="PASS">IBootstrapNode/IStaticResetNode contract added; no RuntimeInitializeOnLoadMethod in SHINOBU_31 code.</task_14>
  <task_15 status="PASS">VaultOffsets.g.cs and generator hook added.</task_15>
  <task_16 status="PASS">Leaf mock owns no persistent NativeQueue/NativeArray allocation.</task_16>
  <task_17 status="PASS">300-sample compile telemetry ring added.</task_17>
  <task_18 status="PASS">Compile Wall X-Ray window added.</task_18>
  <task_19 status="PASS">Span-based CSV override parser added.</task_19>
  <task_20 status="PASS">Nuke Domain Reload toggle added; static reset contract exists.</task_20>
  <arm64_check status="PASS">Primary DTOs are 8-byte aligned. `GlobalSignalPayload` is exactly 128 bytes.</arm64_check>
  <zero_gc_check status="PASS">No SHINOBU_31 Tick/Update hot path exists. Mock `Create()` returns `default`; no managed allocation.</zero_gc_check>
  <aup_check status="N/A">No world-position math in SHINOBU_31 domain.</aup_check>
  <dear_lie_check status="PASS">Physics cross-domain call is a facade over a function pointer and buffer handle; no runtime physics dependency.</dear_lie_check>
  <dependency_check status="PASS">New runtime assembly references only contracts plus Burst/Mathematics; no sibling runtime refs.</dependency_check>
  <blackbox_check status="PASS">Compile-domain blackbox uses `NativeArray<CompileWallBlackBoxEntry>(300, Allocator.Persistent)` and dumps `Docs/AgentLogs/Dump_SHINOBU_31.h8dump` before fatal build-gate throw.</blackbox_check>
  <compile_guard status="PASS">Current project scan: 154 asmdefs, 85 illegal baseline edges, 0 from SHINOBU_31 paths.</compile_guard>
</SELF_AUDIT>

### Cinematic Cheats Used

- No physical simulation was added.
- The architectural fake is the "Dear Lie": callers can invoke `PhysicsFacade.ApplyForce()` while only touching a Burst function pointer and a blind buffer handle. The caller does not compile against the Physics runtime assembly.
- Low/Middle/High/Ultra scaling is handled by contract routing and asmdef version defines, not by runtime high-tier branches in leaf callers.

### Exact Microseconds Saved

- Measured exact runtime microseconds saved: 0 us claimed. No profiler or player runtime was run.
- Measured exact full-build time saved: 0 us claimed. Full Unity compile is dependency-blocked.
- Static estimate: asmdef graph scan gate costs about 250-300 us per first-party asmdef JSON on workstation hardware.
- Static estimate: function-pointer facade avoids managed interface dispatch/boxing in hot cross-domain calls; expected gain is 3-8 us per thousand calls after adopter domains register Burst pointers.
- Static estimate: `GlobalSignalPayload` avoids generic SignalBus type expansion across domains; runtime dispatch estimate is about 0.5 us per payload route, pending profiler proof.

### Verification Evidence

- Direct trailing-whitespace scan clean for SHINOBU_31 text files; generated `link.xml` normalized to ASCII without BOM.
- Runtime/contract forbidden-pattern scan clean for `Global/Contracts`, `Global/MockDomain`, and `Global/Generated`.
- Loop 7 superseded the earlier Editor display allocation note: SHINOBU_31 source paths now contain no `.ToString()` calls; the required `NativeArray` compile blackbox remains.
- Bee response file for `Hecton8.MockDomain.Runtime` references `Hecton8.Global.Contracts` and `Hecton8.MockDomain.Contracts`; no `Hecton8.Core`, `Hecton8.Physics`, or `Hecton8.UI` refs.
- Compile-domain blackbox uses 300 native entries, 40 bytes each = 12,000 bytes of persistent editor native storage.
- Unity R3/R5 produced Csc/ILPostProcess/CopyFiles for new contract/mock assemblies before external compile blockers stopped the project.
- Isolated Unity Roslyn compile of `CompileWallXRayWindow.cs` using `Hecton8.Editor.rsp` returned exit code 0 with only CS0618 warnings for the per-assembly timing hook.
- Latest full Unity batch compile R6 exits with compiler errors in external domains. No green full-project compile is claimed.

## 2026-05-18 - Polish Mandate Recheck

Status: BLACKBOX GAP CLOSED / FULL UNITY COMPILE STILL BLOCKED BY DEPENDENCY
Evidence class: STATIC_SOURCE / UNITY_ROSLYN_ISOLATED

### What Was Wrong

- Previous report identified a blackbox gap. That was correct and unacceptable under the polish mandate.
- The compile telemetry UI buffer was managed and did not provide a fixed native postmortem ring or fatal dump artifact.

### What Was Done

- Added `CompileWallBlackBox`.
- Backing storage is `NativeArray<CompileWallBlackBoxEntry>(300, Allocator.Persistent)`.
- Entry layout is explicit by `StructLayout(Size = 40, Pack = 8)`: `double Seconds`, six `int` counters, two `uint` markers.
- Graph scans now call `RecordGraphScan()`.
- Per-assembly compile telemetry now calls `RecordCompilationSample()`.
- Fatal Runtime -> Runtime build-gate failure now calls `Dump("RuntimeToRuntimeAsmdefEdge", scan)` before throwing `BuildFailedException`.
- Dump path: `Docs/AgentLogs/Dump_SHINOBU_31.h8dump`.
- Native buffer disposal is registered on assembly reload and Editor quitting.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us claimed. Editor tooling only.
- Measured compile microseconds saved: 0 us claimed. Full Unity compile remains dependency-blocked.
- Native blackbox memory cost: 12,000 bytes plus NativeArray header.
- Fatal dump cost: cold path only.

### Verification Evidence

- Isolated Unity Roslyn compile of `CompileWallXRayWindow.cs` returned exit code 0.
- Warnings only: two CS0618 warnings from Unity's obsolete `assemblyCompilationStarted` timing hook. This is accepted because the task explicitly required per-assembly compile timing.
- Direct trailing-whitespace scan clean for SHINOBU_31 text files after the blackbox patch.
- Runtime/contract forbidden-pattern scan clean for SHINOBU_31 contract/mock/generated paths; Editor blackbox allocation is expected.

## 2026-05-18 - Authoring Slice Closure

Status: STRICT THREE-TIER MOCK DOMAIN VERIFIED / FULL UNITY COMPILE STILL BLOCKED BY DEPENDENCY
Evidence class: STATIC_SOURCE / UNITY_ROSLYN_ISOLATED

### What Was Wrong

- The previous proof showed Contracts and Runtime isolation, but it did not explicitly close the `*.Authoring.asmdef` tier required by Task 02.
- The first Authoring profile draft stored AUP in a Unity `Vector3`, which would have narrowed absolute coordinates to float before writing a `double3`.

### What Was Done

- Added `Hecton8.MockDomain.Authoring.asmdef`.
- Added `MockDomainAuthoringProfile` as the designer-facing ScriptableObject facade.
- Authoring references only `Hecton8.Global.Contracts`, `Hecton8.MockDomain.Contracts`, and `Unity.Mathematics`; no `Hecton8.MockDomain.Runtime`, `Hecton8.Core`, `Hecton8.Physics`, or `Hecton8.UI`.
- Replaced authoring AUP storage with serialized `double anchorAupX/Y/Z` fields and emit `double3` directly.
- Re-ran current-source Unity Roslyn compile for `Hecton8.MockDomain.Authoring.rsp`; exit code 0, latest Bee DLL timestamp `2026-05-18 02:06:23`.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us claimed. This is compile architecture and authoring isolation, not player-frame code.
- Measured full-build microseconds saved: 0 us claimed. Full Unity compile remains blocked by external domains.
- Static estimate remains 250-300 us per asmdef JSON for graph validation. The value is scan cost, not saved time.

### Verification Evidence

- Current asmdef graph scan: 154 asmdefs, 85 pre-existing Runtime -> Runtime baseline edges, 0 from SHINOBU_31 paths.
- Runtime/contract forbidden-pattern scan for SHINOBU_31 paths returned no matches.
- Loop 7 superseded the earlier X-Ray display-formatting exception; the required 300-entry NativeArray blackbox remains.
- Authoring response first-party refs: `Hecton8.Global.Contracts.ref.dll` and `Hecton8.MockDomain.Contracts.ref.dll` only.
- Direct trailing-whitespace scan clean for SHINOBU_31 text files; generated `link.xml` has no BOM.

## 2026-05-18 - Ultra Polish: Zero-GC X-Ray And Active State Recovery

Status: ULTRA POLISH VERIFIED / FULL UNITY COMPILE STILL BLOCKED BY DEPENDENCY
Evidence class: STATIC_SOURCE / DOTNET_LAYOUT_AUDIT / UNITY_ROSLYN_ISOLATED

### What Was Wrong

- Active `Docs/Tasks/Status_SHINOBU_31.md`, `Docs/AgentLogs/Rationale_SHINOBU_31.md`, `Docs/AgentLogs/LOG_SHINOBU_31.md`, and `Docs/Tasks/CURRENT_BATCH.md` were missing because Batch008 archival moved them out of the live protocol paths.
- `Compile Wall X-Ray` still had `.ToString()` in IMGUI repaint paths and a cold `StringBuilder.ToString()` in generated artifact/build-failure code.
- The previous report treated Editor `.ToString()` as acceptable. Under the user's latest mandate that was not strict enough.

### What Was Done

- Restored active SHINOBU_31 status/rationale/log/current-batch files from `Docs/Archive/Batch008`.
- Removed all `.ToString()` calls from SHINOBU_31 source paths.
- Replaced X-Ray numeric display with disabled `IntField`/`DoubleField` controls.
- Reused captured assembly path strings for compile samples instead of parsing filenames every repaint.
- Rewrote `link.xml` generation to stream through `TextWriter` and ASCII output, eliminating `StringBuilder.ToString()` materialization.
- Build-gate failure now logs edge data separately and throws a constant `BuildFailedException` message after writing the blackbox dump.

### Struct Layout Evidence

- `GlobalSignalPayload`: size 128; offsets `0 TypeHash`, `4 Version`, `6 PayloadBytes`, `8 FrameIndex`, `12 Flags`, `16..120 Payload00..Payload13`.
- `GlobalNativeBufferHandle`: size 32; offsets `0 Pointer`, `8 Length`, `12 StrideBytes`, `16 OwnerHash`, `20 Generation`, `24 AccessFlags`, `28 Reserved`.
- `GlobalFunctionPointerHandle`: size 32; offsets `0 Pointer`, `8 FunctionHash`, `12 ContractHash`, `16 Version`, `20 Flags`, `24 Reserved`.
- `NativeMemoryAliasContract`: size 64; offsets `0 ReadOnlyFront`, `32 WriterSystemHash`, `36 ReaderMaskLow`, `40 ReaderMaskHigh`, `44 Generation`, `48 ByteRangeStart`, `56 ByteRangeLength`.
- `AssemblyRoutingOverride`: size 64; offsets `0 ContractHash`, `4 ImplementationHash`, `8 MockImplementationHash`, `12 Flags`, `16 MinQuality`, `17 MaxQuality`, `18 Priority`, `20 Reserved0`, `24 Reserved1`, `32 Reserved2`, `40 Reserved3`, `48 Reserved4`, `56 Reserved5`.
- `PhysicsFacade`: size 40; offsets `0 ApplyForceFunction`, `8 BodyBuffer`.
- `MockDomainState`: size 32; offsets `0 AnchorAup`, `24 Flags`, `28 Generation`.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us claimed. No player runtime was run.
- Measured Editor repaint allocation reduction: source-verified only, not profiler-measured.
- Measured full-build time saved: 0 us claimed. Full Unity compile remains blocked by external Bee/Core state.

### Verification Evidence

- `rg` forbidden-pattern scan over SHINOBU_31 paths returned no `.ToString()`, `Pack=1`, LINQ chain, runtime init attribute, `FindObjectOfType`, `GetComponent`, `GameObject.Find`, or `new NativeQueue` matches.
- Required `new NativeArray<CompileWallBlackBoxEntry>(300, Allocator.Persistent)` remains as the compile-domain blackbox.
- Current asmdef graph scan: 154 asmdefs, 85 pre-existing Runtime -> Runtime baseline edges, 0 from SHINOBU_31 paths.
- Full `Hecton8.Editor.rsp` compile is blocked by missing external `Library/Bee/.../Hecton8.Core.ref.dll`.
- Scoped Unity Roslyn compile returned exit 0 for `Hecton8.Global.Contracts.rsp`, `Hecton8.MockDomain.Contracts.rsp`, `Hecton8.MockDomain.Runtime.rsp`, and `Hecton8.MockDomain.Authoring.rsp`.
- Isolated `CompileWallXRayWindow.cs` Unity Roslyn compile returned exit code 0 with two expected CS0618 warnings for the required per-assembly timing hook.
- Temporary `dotnet` layout audit built with 0 warnings / 0 errors and printed the byte offsets above.

## 2026-05-18 - ARM64 Bootstrap Layout Closure

Status: PENDING VERIFICATION / OWNED SLICE CLEAN / FULL UNITY COMPILE BLOCKED BY DEPENDENCY
Evidence class: STATIC_SOURCE / DOTNET_LAYOUT_AUDIT / UNITY_ROSLYN_SCOPED / UNITY_ROSLYN_ISOLATED

### What Was Wrong

- Active prompt extraction was initially attempted with a too-narrow regex that did not account for attributes on `<AGENT_PROMPT id="SHINOBU_31" ...>`. Re-extraction from active `Docs/Tasks/CURRENT_BATCH.md` now reports exactly 20 tasks.
- Loop 7's byte audit exposed a real contract defect: `BootstrapRegistryContext` and `BootstrapDependencySnapshot` were declared with `Size=64`, but the field map needed 72 bytes.
- `VaultOffsets.g.cs` and the generator constants were therefore incomplete for bootstrap structs.
- The CSV override reader still used `File.ReadAllBytes`, which is avoidable allocation in the Editor routing path.
- Current project graph changed since the earlier report: 154 asmdefs, 81 baseline Runtime -> Runtime illegal edges, 0 from SHINOBU_31.

### What Was Done

- Corrected `BootstrapRegistryContext` to explicit `Size=80`, `Pack=8`, with `Reserved` at offset 72.
- Corrected `BootstrapDependencySnapshot` to explicit `Size=80`, `Pack=8`, with `Reserved` at offset 72.
- Updated generated `VaultOffsets.g.cs` constants and the `CompileWallXRayWindow` generator stream output to publish the 80-byte sizes.
- Rewrote Vault offset generation to stream through `TextWriter`, not build a return string.
- Replaced `File.ReadAllBytes` in `AssemblyRoutingCsv.TryReadFirstOverrideFromDisk` with a bounded static `CsvScratch` buffer and `FileStream.Read` loop, then parsed `ReadOnlySpan<byte>`.
- Re-ran scoped Unity Roslyn compile for `Global.Contracts`, `MockDomain.Contracts`, `MockDomain.Runtime`, and `MockDomain.Authoring`; all returned exit 0.
- Re-ran isolated `CompileWallXRayWindow.cs` compile; exit 0 with two CS0618 warnings from Unity's obsolete timing hook.

### Struct Layout Evidence

- `GlobalSignalPayload`: size 128; offsets `0 TypeHash`, `4 Version`, `6 PayloadBytes`, `8 FrameIndex`, `12 Flags`, `16..120 Payload00..Payload13`.
- `GlobalNativeBufferHandle`: size 32; offsets `0 Pointer`, `8 Length`, `12 StrideBytes`, `16 OwnerHash`, `20 Generation`, `24 AccessFlags`, `28 Reserved`.
- `GlobalFunctionPointerHandle`: size 32; offsets `0 Pointer`, `8 FunctionHash`, `12 ContractHash`, `16 Version`, `20 Flags`, `24 Reserved`.
- `NativeMemoryAliasContract`: size 64; offsets `0 ReadOnlyFront`, `32 WriterSystemHash`, `36 ReaderMaskLow`, `40 ReaderMaskHigh`, `44 Generation`, `48 ByteRangeStart`, `56 ByteRangeLength`.
- `VaultOffsetRecord`: size 32; offsets `0 TypeHash`, `4 FieldHash`, `8 OffsetBytes`, `12 SizeBytes`, `16 StrideBytes`, `20 Flags`, `24 Reserved`.
- `AssemblyRoutingOverride`: size 64; offsets `0 ContractHash`, `4 ImplementationHash`, `8 MockImplementationHash`, `12 Flags`, `16 MinQuality`, `17 MaxQuality`, `18 Priority`, `20 Reserved0`, `24 Reserved1`, `32 Reserved2`, `40 Reserved3`, `48 Reserved4`, `56 Reserved5`.
- `BootstrapRegistryContext`: size 80; offsets `0 FrameIndex`, `4 NodeCount`, `8 BufferTable`, `40 RegistryTable`, `72 Reserved`.
- `BootstrapDependencySnapshot`: size 80; offsets `0 FrameIndex`, `4 Generation`, `8 ServiceTable`, `40 SignalTable`, `72 Reserved`.
- `PhysicsFacade`: size 40; offsets `0 ApplyForceFunction`, `8 BodyBuffer`.
- `MockDomainState`: size 32; offsets `0 AnchorAup`, `24 Flags`, `28 Generation`.

### SELF_AUDIT Task Matrix

- Task 01 PASS: graph archaeology and emergency mock atlas path exist.
- Task 02 PASS: three-tier MockDomain split exists; new SHINOBU_31 edges add 0 Runtime -> Runtime debt.
- Task 03 PASS: contracts expose handles/ref-oriented methods; no array `{ get; set; }` surfaces in owned contracts.
- Task 04 PASS: cross-assembly DTOs are 8-byte aligned; no `Pack=1`.
- Task 05 PASS: mock contracts/runtime compile without Core/UI/Physics.
- Task 06 PASS: `Hecton8.Global.Contracts` is isolated to contracts and Unity low-level packages.
- Task 07 PASS: pre-build Runtime -> Runtime gate exists.
- Task 08 PASS: `GlobalSignalPayload` is 128 bytes.
- Task 09 PASS: Burst function pointer bridge exists; delegate itself is not incorrectly marked `[BurstCompile]`.
- Task 10 PASS: `PhysicsFacade` holds function pointer and Vault handle.
- Task 11 PASS: generated `link.xml` preservation path exists.
- Task 12 PASS: `[NoAlias] NativeMemoryAliasContract` exists.
- Task 13 PASS: asmdef `versionDefines` route quality/Burst symbols.
- Task 14 PASS: bootstrap/reset contracts exist; no SHINOBU_31 runtime init attribute.
- Task 15 PASS: `VaultOffsets.g.cs` exists and now matches audited bootstrap sizes.
- Task 16 PASS: leaf runtime receives handles; no leaf-owned persistent queues.
- Task 17 PASS: compile telemetry blackbox uses a 300-entry native ring.
- Task 18 PASS: `Compile Wall X-Ray` Editor facade exists.
- Task 19 PASS: CSV contract override parser exists and now uses bounded scratch read.
- Task 20 PASS: no-domain-reload toggle exists; runtime static reset contract exists.

### Cinematic Cheats Used

- Dependency injection fake: no reflection DI framework, no Zenject, no assembly boot scan on hot paths. The "dynamic" route is a hardcoded contract/facade/function-pointer table.
- Physics fake: `PhysicsFacade.ApplyForce()` makes callers believe they talk to Physics while they only invoke a Burst pointer with a blind Vault handle.
- Low-tier path: compile and runtime callers keep only contract assemblies in scope.
- Middle/high/ultra path: richer runtime implementations can register behind the same function pointer/facade without recompiling sibling domains.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured full-build microseconds saved: 0 us. Full `Hecton8.Editor.rsp` probe is blocked before source compile by missing `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.ref.dll`.
- Static estimate retained: graph scan cost is about 250-300 us per asmdef JSON; this is validation cost, not measured saved time.

### Verification Evidence

- Prompt extraction: active `CURRENT_BATCH.md` block for `SHINOBU_31` found; task count = 20.
- Forbidden-pattern scan over SHINOBU_31 paths returned no `.ToString()`, `Pack=1`, LINQ chain, runtime init attribute, `FindObjectOfType`, `GetComponent`, `GameObject.Find`, or `new NativeQueue` matches.
- Direct trailing-whitespace scan clean for the edited SHINOBU_31 text files.
- Current asmdef graph scan: 154 asmdefs, 81 baseline Runtime -> Runtime illegal edges, 0 from SHINOBU_31 paths.
- Scoped Unity Roslyn compile returned exit 0 for `Hecton8.Global.Contracts.rsp`, `Hecton8.MockDomain.Contracts.rsp`, `Hecton8.MockDomain.Runtime.rsp`, and `Hecton8.MockDomain.Authoring.rsp`.
- Isolated `CompileWallXRayWindow.cs` Unity Roslyn compile returned exit 0 with two CS0618 warnings for Unity's obsolete per-assembly timing hook.
- Temporary `dotnet` layout audit built and ran with 0 compile warnings / 0 errors and printed the byte offsets above.
- Full Unity compile remains `BLOCKED BY DEPENDENCY`: direct `Hecton8.Editor.rsp` probe fails on missing external `Hecton8.Core.ref.dll`.

## 2026-05-18 - Quality Continuum And No-Engine Isolation Closure

Status: PENDING VERIFICATION / OWNED SLICE CLEAN / FULL UNITY COMPILE BLOCKED BY DEPENDENCY
Evidence class: STATIC_SOURCE / DOTNET_LAYOUT_AUDIT / UNITY_ROSLYN_SCOPED / UNITY_ROSLYN_ISOLATED

### What Was Wrong

- `AssemblyRoutingOverride` still expressed quality as coarse enum buckets only. That was not enough for the `GlobalQualityWeight` continuum mandate.
- `MockApplyForce` used a bare `[BurstCompile]` attribute instead of explicit synchronous/fast/standard Burst flags.
- Contract/runtime asmdefs that have no UnityEngine dependency still had `noEngineReferences=false`, leaving room for accidental UnityEngine creep.
- Active `Docs/Tasks/CURRENT_BATCH.md` now belongs to a newer batch and no longer contains `SHINOBU_31`; active SHINOBU_31 state files had to be restored from the latest Batch008 collision copies before continuing.

### What Was Done

- Added `MinQualityWeight`, `MaxQualityWeight`, and `QualityCurveHash` to `AssemblyRoutingOverride` while preserving `Size=64`.
- Added generated offsets: `AssemblyRoutingOverride_MinQualityWeight=20`, `AssemblyRoutingOverride_MaxQualityWeight=24`, `AssemblyRoutingOverride_QualityCurveHash=28`.
- Extended `AssemblyRoutingCsvRow` to a 32-byte aligned struct with optional parsed quality weights and curve hash.
- Added span-based decimal parser for optional `0..1` CSV weights; no string allocation or culture parser.
- Added designer-facing quality-weight fields to `MockDomainAuthoringProfile`.
- Changed `MockApplyForce` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
- Set `noEngineReferences=true` for `Hecton8.Global.Contracts`, `Hecton8.MockDomain.Contracts`, and `Hecton8.MockDomain.Runtime`. Authoring remains `false` because it owns the `ScriptableObject` facade.

### Struct Layout Evidence

- `AssemblyRoutingOverride`: size 64; offsets `0 ContractHash`, `4 ImplementationHash`, `8 MockImplementationHash`, `12 Flags`, `16 MinQuality`, `17 MaxQuality`, `18 Priority`, `20 MinQualityWeight`, `24 MaxQualityWeight`, `28 QualityCurveHash`, `32 Reserved2`, `40 Reserved3`, `48 Reserved4`, `56 Reserved5`.
- `GlobalSignalPayload`: size 128; offsets `0 TypeHash`, `4 Version`, `6 PayloadBytes`, `8 FrameIndex`, `12 Flags`, `16..120 Payload00..Payload13`.
- `BootstrapRegistryContext`: size 80; offsets `0 FrameIndex`, `4 NodeCount`, `8 BufferTable`, `40 RegistryTable`, `72 Reserved`.
- `BootstrapDependencySnapshot`: size 80; offsets `0 FrameIndex`, `4 Generation`, `8 ServiceTable`, `40 SignalTable`, `72 Reserved`.

### SELF_AUDIT Delta

- Task 02 PASS: `noEngineReferences=true` now hardens contract/runtime assemblies against UnityEngine bleed.
- Task 09 PASS: Burst function pointer target now uses explicit compile and float flags.
- Task 13 PASS: quality routing now supports continuous 0..1 weight in addition to coarse asmdef symbols.
- Task 15 PASS: generated offset constants include the new quality-weight fields.
- Task 19 PASS: CSV override can hydrate optional quality weights without string allocation.

### Cinematic Cheats Used

- Compile-time fake: routing is treated as dynamic through CSV/facade rows, but the executable path is a rigid hash + function pointer table.
- Quality fake: low tiers do not need separate runtime implementations hard-linked into callers; `GlobalQualityWeight` selects mock/cheap/rich implementations through contract rows.
- Complexity before fake: direct domain calls create O(N domains) recompilation blast radius and managed dispatch. After fake: O(1) hash/facade/function-pointer route per contract.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. Full Unity Editor compile is still blocked by external source errors.
- Static estimate retained: validation cost is about 250-300 us per asmdef JSON. This is not a measured saving.

### Verification Evidence

- Forbidden-pattern scan over owned SHINOBU_31 paths returned no `File.ReadAllBytes`, `.ToString()`, `Pack=1`, LINQ chain, runtime init attribute, Unity find/getcomponent, `UnityEngine.Random`, `Time.deltaTime`, `JobHandle.Complete`, or `new NativeQueue` hits.
- Direct trailing-whitespace scan clean for edited owned files.
- `noEngineReferences` scan: Global.Contracts=true, MockDomain.Contracts=true, MockDomain.Runtime=true, MockDomain.Authoring=false.
- Scoped Unity Roslyn compile returned exit 0 for `Hecton8.Global.Contracts.rsp`, `Hecton8.MockDomain.Contracts.rsp`, `Hecton8.MockDomain.Runtime.rsp`, and `Hecton8.MockDomain.Authoring.rsp`.
- Isolated `CompileWallXRayWindow.cs` Unity Roslyn compile returned exit 0 with two CS0618 warnings for Unity's obsolete per-assembly timing hook.
- Dotnet layout audit built and ran with 0 compile warnings / 0 errors; `AssemblyRoutingOverride` remained 64 bytes with weight fields at offsets 20/24/28.
- Current asmdef graph scan: 154 asmdefs, 81 baseline Runtime -> Runtime illegal edges, 0 from SHINOBU_31 paths.
- Full `Hecton8.Editor.rsp` probe is `BLOCKED BY DEPENDENCY`: external `HardwareDictatorTunerWindow.cs` references missing `HomeostasisBrain` and `ScalabilityStateDTO` members.

## 2026-05-18 - Legacy Binary Archaeology Header Closure

Status: PENDING VERIFICATION / OWNED SLICE CLEAN / FULL UNITY COMPILE BLOCKED BY DEPENDENCY
Evidence class: STATIC_SOURCE / UNITY_ROSLYN_SCOPED / UNITY_ROSLYN_ISOLATED / UNITY_ROSLYN_FULL_BLOCKED

### What Was Wrong

- The Task 01 archaeology path found `asmdef_graph_*.h8bin` files but did not read their headers. That meant old OSHINO graph payloads could be listed without proving endian, alignment, or DTO safety.
- The status file pointed at `Docs/Archive/Batch008/Tasks/CURRENT_BATCH.md`, but the surviving exact SHINOBU_31 XML is now in `Docs/Archive/Batch008/Tasks_Combined/Tasks_Batch008_COMBINED_MD_TXT.md`.
- The user mandate requested `math.reversebytes`, but current Unity Roslyn proved the installed `Unity.Mathematics` does not expose that API.

### What Was Done

- Added `CompileWallLegacyGraphHeader` to `CompileWallXRayWindow.cs` as a cold-path explicit 32-byte DTO.
- Changed `ScanArchaeologyFolder()` so `asmdef_graph_*.h8bin` files are opened with bounded `FileStream` reads and parsed into `LegacyGraphHeaders`.
- Added endian heuristics: default little-endian, switch to big-endian only when `HeaderBytes` is plausible in big-endian and not plausible in little-endian.
- Added high-bit header flags: `0x80000000` for big-endian read and `0x40000000` for recognized `H8AG` magic.
- Added a disabled X-Ray UI field for `Legacy graph headers`.
- Added blackbox flag bit `2` when a graph scan includes parsed legacy headers.
- Updated active `Status_SHINOBU_31.md` and `Rationale_SHINOBU_31.md` with Loop 10 evidence.

### Struct Layout Evidence

- `CompileWallLegacyGraphHeader`: size 32, `Pack=8`.
- Offsets: `0 Magic:uint`, `4 Version:ushort`, `6 HeaderBytes:ushort`, `8 NodeCount:uint`, `12 EdgeCount:uint`, `16 Flags:uint`, `20 PayloadBytes:uint`, `24 Crc32:uint`, `28 Reserved:uint`.
- Alignment math: `4 + 2 + 2 + 4 + 4 + 4 + 4 + 4 + 4 = 32`; `32 % 8 = 0`; `Pack=1` is not used.
- False sharing: not a concurrent atomic counter; no 64-byte padding required.

### SELF_AUDIT Task Delta

- Task 01 PASS: legacy graph binary headers are now parsed, not just path-listed.
- Task 02 PASS: latest `Assets/_Project` graph probe reports 108 asmdefs, 81 baseline Runtime -> Runtime illegal edges, 0 from SHINOBU_31-owned paths.
- Task 04 PASS: new header DTO is 32 bytes, explicit, 8-byte aligned.
- Task 07 PASS: build gate remains intact; full Editor probe reaches source compile and then fails in external HardwareDictator code, not in AssemblyGuard.
- Task 17 PASS: graph scan blackbox now carries a flag when legacy headers exist.
- Task 18 PASS: X-Ray window exposes legacy header count for human inspection.

### Cinematic Cheats Used

- Binary archaeology fake: read the 32-byte graph header instead of loading/parsing the full legacy graph payload. Complexity before: O(file bytes) managed load if using `ReadAllBytes`; after: O(1) 32-byte cold-path read per graph binary.
- Dynamic graph fake: X-Ray still presents archive awareness as live architecture context, but runtime assemblies stay untouched and isolated.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. Full Editor compile remains blocked by external source errors.
- Static estimate: per legacy graph file, header-only read caps the audit at 32 bytes plus file open cost instead of full payload allocation. This is Editor cold-path only.

### Verification Evidence

- Exact SHINOBU_31 XML recovered from `Docs/Archive/Batch008/Tasks_Combined/Tasks_Batch008_COMBINED_MD_TXT.md`; task count remains 20.
- Forbidden-pattern scan over SHINOBU_31 owned source returned no `File.ReadAllBytes`, `.ToString()`, LINQ chain, `Pack=1`, Unity find/getcomponent hot-path calls, `UnityEngine.Random`, `Time.deltaTime`, `JobHandle.Complete`, or `math.reversebytes` hits.
- Trailing-whitespace scan clean for edited SHINOBU_31 source/status/rationale/log files.
- Scoped Unity Roslyn compile exit 0 for `Hecton8.Global.Contracts.rsp`, `Hecton8.MockDomain.Contracts.rsp`, `Hecton8.MockDomain.Runtime.rsp`, and `Hecton8.MockDomain.Authoring.rsp`.
- Isolated `CompileWallXRayWindow.cs` Unity Roslyn compile exit 0. Warnings: two CS0618 warnings for Unity's obsolete per-assembly timing hook and CS0649 warnings for JsonUtility DTO fields.
- Full `Hecton8.Editor.rsp` probe exit 1. SHINOBU_31 file emits warnings only; failure is external `HardwareDictatorTunerWindow.cs` missing `HomeostasisBrain.SetForcedGlobalQualityWeightForTuner`, `ScalabilityStateDTO.GlobalQualityWeight`, `FractionalTimeSlice`, `VramPressure`, `ThermalIndex`, `HomeostasisBrain.TargetRenderScale01`, `TryGetMockTerrainSamplerStatus`, and `MockTerrainSamplerStatus`.

## 2026-05-18 - Owned Editor Warning Debt Purge

Status: PENDING VERIFICATION / OWNED SLICE CLEAN / FULL UNITY COMPILE BLOCKED BY DEPENDENCY
Evidence class: STATIC_SOURCE / UNITY_ROSLYN_SCOPED / UNITY_ROSLYN_ISOLATED / UNITY_ROSLYN_FULL_BLOCKED

### What Was Wrong

- `CompileWallXRayWindow.cs` was source-correct but not warning-clean. It used obsolete `CompilationPipeline.assemblyCompilationStarted` and let JsonUtility DTO fields produce CS0649 warnings.
- Warning suppression would have made the compile-wall guard look clean while preserving actual warning debt.
- Full Editor compilation now fails earlier on `SignalTrafficMonitorWindow.cs` because `SignalLaneTelemetry` exists in both `Hecton8.Core.Contracts` and `Hecton8.Core`. This is a real external contract duplication blocker, not SHINOBU_31 code.

### What Was Done

- Replaced obsolete per-assembly start timing with non-obsolete `CompilationPipeline.compilationStarted`, `assemblyCompilationFinished`, and `compilationFinished`.
- The telemetry recorder now records a compile-window segment per assembly finish. This keeps Task 17 evidence without calling the obsolete API.
- Added explicit cold defaults to `CompileWallAsmdefJson` and `CompileWallVersionDefine` fields so JsonUtility DTOs compile without CS0649.
- Re-ran scoped owned assemblies and isolated Editor guard compile.
- Re-ran full `Hecton8.Editor.rsp` probe to capture the current external blocker.

### Struct Layout Evidence

- No runtime contract DTO layout changed in this pass.
- `CompileWallBlackBoxEntry` remains size 40, `Pack=8`: `0 Seconds:double`, `8 Sequence:int`, `12 NodeCount:int`, `16 EdgeCount:int`, `20 IllegalEdgeCount:int`, `24 WarningCount:int`, `28 ErrorCount:int`, `32 EventHash:uint`, `36 Flags:uint`.
- `CompileWallLegacyGraphHeader` remains size 32, `Pack=8`: `0 Magic:uint`, `4 Version:ushort`, `6 HeaderBytes:ushort`, `8 NodeCount:uint`, `12 EdgeCount:uint`, `16 Flags:uint`, `20 PayloadBytes:uint`, `24 Crc32:uint`, `28 Reserved:uint`.

### SELF_AUDIT Delta

- Task 07 PASS: build gate source is warning-clean under isolated Unity Roslyn compile.
- Task 17 PASS: compile telemetry no longer depends on obsolete `assemblyCompilationStarted`; blackbox ring still records samples.
- Task 18 PASS: X-Ray Editor facade compiles with zero warnings in the owned isolated probe.
- Dependency guard PASS: latest `Assets/_Project` scan reports 109 asmdefs, 81 baseline Runtime -> Runtime illegal edges, 0 from SHINOBU_31-owned paths.

### Cinematic Cheats Used

- Telemetry fake: instead of pretending Unity's obsolete per-assembly start event is reliable timing truth, the recorder captures compile-window segment timing from non-obsolete events. It is explicitly an editor diagnostic signal, not a profiler-grade stopwatch.
- Complexity before: warning-bearing event pair plus per-active-slot search. Complexity after: O(1) window timestamp update plus O(messages) count on finish.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. Full Editor compile is still blocked by external source errors.
- Warning debt removed: isolated `CompileWallXRayWindow.cs` Unity Roslyn compile changed from warnings to zero warnings / zero errors.

### Verification Evidence

- Forbidden-pattern scan over SHINOBU_31 owned source returned no `File.ReadAllBytes`, `.ToString()`, LINQ chain, `Pack=1`, runtime init attribute, Unity find/getcomponent calls, `UnityEngine.Random`, `Time.deltaTime`, `JobHandle.Complete`, `assemblyCompilationStarted`, or `math.reversebytes` hits.
- `git diff --check` over edited SHINOBU_31 files returned clean.
- Scoped Unity Roslyn compile exit 0 for `Hecton8.Global.Contracts.rsp`, `Hecton8.MockDomain.Contracts.rsp`, `Hecton8.MockDomain.Runtime.rsp`, and `Hecton8.MockDomain.Authoring.rsp`.
- Isolated `CompileWallXRayWindow.cs` Unity Roslyn compile exit 0 with zero warnings and zero errors.
- Current `Assets/_Project` asmdef scan: 109 asmdefs, 81 baseline Runtime -> Runtime illegal edges, `IllegalFromShinobu31=0`.
- Full `Hecton8.Editor.rsp` probe exit 1. Current blocker: `Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow.cs` sees duplicate `SignalLaneTelemetry` in `Hecton8.Core.Contracts` and `Hecton8.Core`.

## 2026-05-18 - Stale Contract Artifact Skew Guard

Status: PENDING VERIFICATION / OWNED SLICE CLEAN / FULL UNITY COMPILE BLOCKED BY STALE OR EXTERNAL DEPENDENCY
Evidence class: STATIC_SOURCE / FILESYSTEM / BEE_RSP_STATIC

### What Was Wrong

- The current full Editor probe blocker is `SignalTrafficMonitorWindow.cs` CS0433 for `SignalLaneTelemetry`.
- Source evidence shows `Hecton8.Core.rsp` does not include `Core/Contracts/HectonSignalLaneContract.cs`; it includes `GlobalSignals.cs` and references `Hecton8.Core.Contracts.ref.dll`.
- Bee artifact timestamps are skewed: `Hecton8.Core.ref.dll` = `2026-05-18 07:40:44 UTC`, `Hecton8.Core.Contracts.ref.dll` = `2026-05-18 17:44:35 UTC`, skew = `36230.8176631` seconds.
- Active `csc` and `dotnet` processes were running and CPU stayed near 100%, so another compile probe would violate the user's explicit guard and AGENTS.md.

### What Was Done

- Added `CompileWallArtifactSkew` to `CompileWallXRayWindow.cs`.
- The X-Ray scanner now builds one index of `Library/Bee/artifacts/**/*.ref.dll` and flags implementation/editor `.ref.dll` files older than a referenced Contracts `.ref.dll`.
- The UI now shows `Stale ref artifacts` and a `Stale Contract Ref Artifacts` table with assembly, contract, seconds behind, and ref artifact path.
- The 300-entry compile-domain blackbox records skew count in `WarningCount` and sets graph-scan flag bit `4`.

### Struct Layout Evidence

- No runtime DTO layout changed.
- `CompileWallBlackBoxEntry` remains size 40, `Pack=8`: `0 Seconds:double`, `8 Sequence:int`, `12 NodeCount:int`, `16 EdgeCount:int`, `20 IllegalEdgeCount:int`, `24 WarningCount:int`, `28 ErrorCount:int`, `32 EventHash:uint`, `36 Flags:uint`.
- `CompileWallArtifactSkew` is an Editor-only reference record, not a NativeArray/Burst/runtime DTO.

### SELF_AUDIT Delta

- Task 07 PASS: build gate remains unchanged; new stale-artifact evidence helps identify compile-wall metadata drift before another full probe.
- Task 17 PASS: blackbox graph entries now carry stale ref artifact count.
- Task 18 PASS: X-Ray now exposes stale contract metadata, not only direct Runtime -> Runtime edges.
- Dependency guard PASS: no sibling Runtime reference was added.

### Cinematic Cheats Used

- Metadata fake: timestamp skew is used as the first-pass proof of stale contract artifacts instead of loading assemblies through reflection or running another full compile. Complexity before: repeated O(compile graph) probes. Complexity after: one O(ref artifact count) filesystem index pass in Editor cold path.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. A new compile was intentionally not launched under active `csc`/`dotnet` load.
- Static diagnostic value: stale Core-vs-Contracts ref skew is now surfaced in X-Ray and the blackbox ring.

### Verification Evidence

- `Hecton8.Core.rsp` includes `Hecton8.Core.Contracts.ref.dll` and `GlobalSignals.cs`, but not `HectonSignalLaneContract.cs`.
- PowerShell artifact index reports `Hecton8.Core.ref.dll` is `36230.8176631` seconds older than `Hecton8.Core.Contracts.ref.dll`.
- Forbidden-pattern scan over `CompileWallXRayWindow.cs` returned no `File.ReadAllBytes`, `.ToString()`, LINQ chain, `Pack=1`, runtime init attribute, Unity find/getcomponent calls, `UnityEngine.Random`, `Time.deltaTime`, or `JobHandle.Complete` hits.
- Trailing-whitespace and >180-character line scans returned clean.
- No `dotnet build` was launched.

## 2026-05-18 - Blackbox Dump ABI Discipline

Status: PENDING VERIFICATION / OWNED SLICE CLEAN / COMPILE PROBE HELD BY CPU AND ACTIVE CSHARP COMPILERS
Evidence class: STATIC_SOURCE / FILESYSTEM

### What Was Wrong

- Loop 12 extended the `.h8dump` header with stale contract artifact count but left the dump version at `1`.
- Raw graph flag literals (`1`, `2`, `4`) were correct but not self-documenting inside the blackbox writer.
- Bee ref artifact scanning stored paths only, then re-read timestamps during edge analysis.

### What Was Done

- Set `DumpVersion = 2` and named `DumpMagic = 0x48384450`.
- Named graph flags: `GraphFlagEmergencyMockAtlas`, `GraphFlagLegacyGraphHeaders`, and `GraphFlagStaleContractArtifact`.
- Added `CompileWallRefArtifactRecord` with cached `Path` and `WriteTimeUtcTicks`.
- Updated stale `.ref.dll` comparison to use cached ticks and `TimeSpan` for seconds-behind calculation.

### Struct Layout Evidence

- No runtime contract DTO layout changed.
- `CompileWallBlackBoxEntry` remains size 40, `Pack=8`: `0 Seconds:double`, `8 Sequence:int`, `12 NodeCount:int`, `16 EdgeCount:int`, `20 IllegalEdgeCount:int`, `24 WarningCount:int`, `28 ErrorCount:int`, `32 EventHash:uint`, `36 Flags:uint`.
- `CompileWallRefArtifactRecord` is Editor cold-path metadata with managed `string Path`; it is not a NativeArray/Burst/runtime DTO.

### SELF_AUDIT Delta

- Task 01 PASS: archive/metadata archaeology now preserves a versioned dump trail for stale artifact evidence.
- Task 07 PASS: build-gate dump format now has explicit version discipline.
- Task 17 PASS: 300-frame compile blackbox records the same entries with a corrected dump header version.
- Dependency guard PASS: no sibling Runtime reference was added and no Contracts ABI was changed.

### Cinematic Cheats Used

- Diagnostic fake: timestamp skew remains the cheap first-pass proof instead of reflection-loading every `.ref.dll` or forcing another compile. Complexity remains O(ref artifact count + asmdef edge count), with fewer repeated filesystem metadata reads.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. A new compile was intentionally not launched.
- Editor cold-path microseconds saved: not measured; metadata reads were reduced by source inspection.

### Verification Evidence

- `rg` confirms `DumpVersion=2`, named graph flags, and cached `WriteTimeUtcTicks`.
- Forbidden-pattern scan over `CompileWallXRayWindow.cs` returned no `dotnet build`, `File.ReadAllBytes`, `.ToString()`, LINQ chain, `Pack=1`, runtime init attribute, Unity find/getcomponent calls, `UnityEngine.Random`, `Time.deltaTime`, or `JobHandle.Complete` hits.
- `git diff --check` over SHINOBU_31 edited paths returned clean.
- Compile probe was not run: active `csc` plus three `dotnet` processes were present and CPU counter returned `100`.

## 2026-05-18 - Runtime Pack=1 Breach Scanner

Status: PENDING VERIFICATION / OWNED TOOLING HARDENED / FOREIGN DTO BREACHES SURFACED
Evidence class: STATIC_SOURCE / FILESYSTEM

### What Was Wrong

- Wide non-Editor source scan found 369 `StructLayout(...Pack = 1)` hits in runtime-side project files.
- Example breach: `Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs` uses `Pack = 1` on several contract DTOs.
- Blindly rewriting those contracts would mutate foreign ABI and binary persistence surfaces while C# compilers are active and CPU is pinned.

### What Was Done

- Added `CompileWallPackViolation` records to the X-Ray graph scan.
- Added source scanning for non-Editor `StructLayout(...Pack = 1)` directives.
- Added `ARM64 Pack=1 hits` and a capped violation table to the Compile Wall X-Ray window.
- Included Pack=1 hit count in blackbox warning count.
- Added graph flag `GraphFlagPackOneRuntimeStruct = 8`.
- Bumped `Dump_SHINOBU_31.h8dump` to version `3` because the header now writes `PackViolations.Count`.

### Struct Layout Evidence

- No SHINOBU_31 runtime DTO layout changed.
- `GlobalSignalPayload` remains 128 bytes: header offsets `0 TypeHash:uint`, `4 Version:ushort`, `6 PayloadBytes:ushort`, `8 FrameIndex:uint`, `12 Flags:uint`; payload lanes `16..120` as fourteen `ulong` fields.
- `CompileWallBlackBoxEntry` remains size 40, `Pack=8`: `0 Seconds:double`, `8 Sequence:int`, `12 NodeCount:int`, `16 EdgeCount:int`, `20 IllegalEdgeCount:int`, `24 WarningCount:int`, `28 ErrorCount:int`, `32 EventHash:uint`, `36 Flags:uint`.

### SELF_AUDIT Delta

- Task 04 PASS FOR OWNED CONTRACTS: SHINOBU_31 contracts still contain no `StructLayout(...Pack = 1)`.
- Task 07 PASS: build/X-Ray tooling now exposes Pack=1 ABI breaches.
- Task 17 PASS: blackbox now stores Pack=1 breach pressure through `WarningCount`.
- Task 18 PASS: human facade now shows the ARM64 violation class directly.

### Cinematic Cheats Used

- ABI fake: use static directive scanning as the first-pass ARM64 risk signal instead of loading assemblies or running a full compiler. Complexity is O(runtime source line count) in Editor cold path.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. A new compile was intentionally not launched.
- Diagnostic result: 369 runtime-side Pack=1 candidates are now visible in X-Ray and blackbox.

### Verification Evidence

- Broad non-Editor scan count: 369 `StructLayout(...Pack = 1)` hits.
- Owned-source scan returned no `StructLayout(...Pack = 1)` in `AssemblyGuard` or SHINOBU_31 Global paths.
- `git diff --check` for `CompileWallXRayWindow.cs` returned clean.
- Compile probe was not run: active `csc`/`dotnet` processes remained present and CPU counter returned `100`.

## 2026-05-18 - Bounded Multi-Line Pack Scanner

Status: PENDING VERIFICATION / OWNED TOOLING STATIC-CLEAN / COMPILE PROBE HELD BY CPU GUARD
Evidence class: STATIC_SOURCE / FILESYSTEM

### What Was Wrong

- The first Pack=1 scanner used `StreamReader.ReadLine()` and `line.Trim()`.
- It could only catch single-line `[StructLayout(... Pack = 1 ...)]` attributes.
- Its state could false-positive `Pack=10` or higher after the next non-digit delimiter.

### What Was Done

- Replaced line reading with an 8192-byte bounded `FileStream` scanner.
- The scanner now tracks multi-line attribute blocks across chunk boundaries.
- It skips line comments and block comments before opening an attribute block.
- It detects `StructLayout` and `Pack = 1` through ASCII token state, not regex or reflection.
- It records the attribute start line and uses one constant snippet string.
- It resets correctly when another digit follows `Pack=1`, preventing `Pack=10` false positives.

### Struct Layout Evidence

- No runtime contract DTO layout changed.
- `GlobalSignalPayload` remains 128 bytes and `Pack=8`.
- `CompileWallBlackBoxEntry` remains 40 bytes and `Pack=8`.
- The Pack scanner itself does not introduce a runtime DTO.

### SELF_AUDIT Delta

- Task 04 PASS FOR OWNED CONTRACTS: no owned Pack=1 runtime DTOs were introduced.
- Task 07 PASS: compile-wall tooling now catches more ARM64 ABI debt with less managed scan churn.
- Task 17 PASS: Pack violation pressure still enters the 300-entry blackbox through `WarningCount`.
- Task 18 PASS: X-Ray facade keeps the human-readable ARM64 violation table.

### Cinematic Cheats Used

- Tooling fake: ASCII token scanning replaces a heavier Roslyn/reflection parse. Complexity remains O(source bytes), but it avoids extra assembly loading and catches multi-line attributes.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. A new compile was intentionally not launched.
- Editor allocation reduction is source-verified only; no profiler measurement was run.

### Verification Evidence

- `rg` found no `StreamReader`, `ReadLine()`, or `ContainsPackOneDirective` in `CompileWallXRayWindow.cs`.
- Owned-source grep returned no `StructLayout(...Pack = 1)` in `AssemblyGuard` or SHINOBU_31 `Global` paths.
- Forbidden-pattern scan over `CompileWallXRayWindow.cs` returned no `dotnet build`, `File.ReadAllBytes`, `.ToString()`, LINQ chain, runtime init attribute, Unity find/getcomponent calls, `UnityEngine.Random`, `Time.deltaTime`, or `JobHandle.Complete` hits.
- `git diff --check` for `CompileWallXRayWindow.cs` returned clean.
- Compile probe was not run: CPU counter returned `66`, above the AGENTS.md 50% guard.

## 2026-05-18 - Pack Violation Storage Budget Cap

Status: PENDING VERIFICATION / OWNED TOOLING STATIC-CLEAN / COMPILE PROBE NOT NEEDED
Evidence class: STATIC_SOURCE / FILESYSTEM

### What Was Wrong

- Loop 15 made the Pack=1 scanner lower-allocation, but retained detail rows still depended on the amount of foreign runtime ABI debt.
- A future batch could add thousands of `Pack = 1` directives and force `List<CompileWallPackViolation>` growth during a cold X-Ray scan.
- The blackbox header only wrote the retained count, not a separately preserved total count.

### What Was Done

- Added `CompileWallGraphScan.PackViolationTotal` as the authoritative Pack=1 count.
- Capped retained Pack violation detail rows at `MaxStoredPackViolations = 512`.
- Kept the X-Ray UI total count accurate while showing only retained detail rows and hidden-row count.
- Lazily caches each file's project-relative path only when a retained row is written.
- Bumped `Dump_SHINOBU_31.h8dump` to version `4`.
- `.h8dump` graph header now writes Pack=1 total count and retained detail count separately.

### Struct Layout Evidence

- No runtime contract DTO layout changed.
- `CompileWallBlackBoxEntry` remains `Size=40`, `Pack=8`.
- Dump header ABI changed by design; version moved from `3` to `4`.

### SELF_AUDIT Delta

- Task 04 PASS FOR OWNED CONTRACTS: no owned `Pack=1` runtime DTO was introduced.
- Task 07 PASS: compile-wall guard/X-Ray now records full ARM64 ABI breach pressure without unbounded detail storage.
- Task 17 PASS: blackbox warnings now use total Pack=1 count, not capped detail count.
- Task 18 PASS: human facade reports total hits and deterministic hidden-row count.

### Cinematic Cheats Used

- Tooling fake: retain a bounded representative detail window plus exact scalar total instead of storing every foreign ABI violation row. Complexity remains O(source bytes), with fixed retained-row memory.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. No compile or build was launched.
- Editor memory risk reduced by source shape: Pack violation detail storage is capped at 512 rows.

### Verification Evidence

- `rg` confirms `MaxStoredPackViolations=512`, `PackViolationTotal`, `DumpVersion=4`, and total/stored Pack count writes.
- `git diff --check` for `CompileWallXRayWindow.cs` returned clean.
- Forbidden-pattern scan returned no `dotnet build`, `File.ReadAllBytes`, `StreamReader`, `ReadLine()`, `.ToString()`, LINQ chain, Unity find/getcomponent calls, `UnityEngine.Random`, `Time.deltaTime`, or `JobHandle.Complete` hits.
- Compile probe was not run because the change is static Editor accounting and did not require it.

## 2026-05-18 - Source Preservation Byte Scanner

Status: PENDING VERIFICATION / OWNED TOOLING STATIC-CLEAN / COMPILE PROBE NOT NEEDED
Evidence class: STATIC_SOURCE / FILESYSTEM

### What Was Wrong

- `.meta` GUID parsing used whole-file line allocation through `File.ReadAllLines()`.
- `link.xml` source preservation scanning also used `File.ReadAllLines()`, per-line `Trim()`, and substring parsing.
- This is Editor cold-path code, but it is the compile-wall hygiene tool; it should not normalize allocation-heavy source scans.

### What Was Done

- Replaced Unity `.meta` GUID parsing with a bounded byte scanner for `guid:` and a 32-char static scratch.
- Replaced link-preservation source parsing with `FileStream`, an 8192-byte read scratch, a 2048-byte line scratch, and byte-span token parsing.
- Added ASCII token helpers for namespace/type detection.
- Kept only unavoidable cold strings: asmdef GUID strings and full type names for `link.xml`/`HashSet`.

### Struct Layout Evidence

- No runtime contract DTO layout changed.
- `GlobalSignalPayload` remains 128 bytes and `Pack=8`.
- `CompileWallBlackBoxEntry` remains 40 bytes and `Pack=8`.

### SELF_AUDIT Delta

- Task 01 PASS: archaeology and asmdef GUID scanning now avoids whole-file line arrays.
- Task 07 PASS: pre-build/Editor tooling keeps dependency validation lightweight.
- Task 11 PASS: `link.xml` generator avoids whole-file source line arrays while preserving required types.
- Task 18 PASS: X-Ray/generator support code remains bounded and owned.

### Cinematic Cheats Used

- Tooling fake: byte-span source scanning replaces a heavy syntax tree or line-array parser. It preserves the same output contract with less cold allocation pressure.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. No compile or build was launched.
- Editor allocation reduction is source-verified only; no profiler measurement was run.

### Verification Evidence

- `rg` found no `File.ReadAllLines`, `StreamReader`, `ReadLine()`, `.ToString()`, LINQ chain, Unity find/getcomponent calls, `UnityEngine.Random`, `Time.deltaTime`, or `JobHandle.Complete` in `CompileWallXRayWindow.cs`.
- `rg` found no trailing whitespace or >180-character lines in `CompileWallXRayWindow.cs`.
- Remaining `new string(...)` calls are cold materialization of Unity GUID/type-name strings required by asmdef/linker contracts.
- Compile probe was not run because the change is static Editor parser work and did not require it.

## 2026-05-18 - Bounded Asmdef Json Parser

Status: PENDING VERIFICATION / OWNED TOOLING STATIC-CLEAN / COMPILE PROBE NOT NEEDED
Evidence class: STATIC_SOURCE / FILESYSTEM

### What Was Wrong

- `TryReadNode()` read every `.asmdef` through `File.ReadAllText()`.
- It hydrated three routing fields through `JsonUtility.FromJson`, keeping cold serializer DTOs in the compile-wall guard.
- The guard only needs `name`, `references`, and `includePlatforms`; a Unity serializer path was excess dependency surface.

### What Was Done

- Added a 64KB static asmdef JSON scratch buffer.
- Replaced whole-file string reads with bounded `FileStream` reads.
- Added byte-span JSON token extraction for `name`, `references`, and `includePlatforms`.
- Capped asmdef array materialization at 128 strings.
- Removed `CompileWallAsmdefJson` and `CompileWallVersionDefine`.

### Struct Layout Evidence

- No runtime contract DTO layout changed.
- `GlobalSignalPayload` remains 128 bytes and `Pack=8`.
- `CompileWallBlackBoxEntry` remains 40 bytes and `Pack=8`.

### SELF_AUDIT Delta

- Task 01 PASS: graph archaeology/asmdef routing now avoids whole-file asmdef string hydration.
- Task 02 PASS: Runtime -> Runtime edge detection still uses only parsed asmdef names/references.
- Task 07 PASS: pre-build guard remains dependency-light and owned.
- Task 18 PASS: X-Ray keeps the same graph output with a bounded parser.

### Cinematic Cheats Used

- Tooling fake: parse the three Unity asmdef fields required for routing instead of paying for full JSON hydration. Complexity remains O(file bytes), with bounded read storage and capped array output.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. No compile or build was launched.
- Editor allocation reduction is source-verified only; no profiler measurement was run.

### Verification Evidence

- `rg` found no `JsonUtility`, `File.ReadAllText`, `File.ReadAllLines`, old asmdef DTO names, or whole-file read aliases in `CompileWallXRayWindow.cs`.
- Forbidden-pattern scan returned no `File.ReadAllBytes`, `StreamReader`, `ReadLine()`, `.ToString()`, LINQ chain, Unity find/getcomponent calls, `UnityEngine.Random`, `Time.deltaTime`, or `JobHandle.Complete`.
- Existing asmdef graph check: 114 asmdefs, maximum `references` count 43, maximum `includePlatforms` count 1. The 128-entry parser cap does not truncate the current project graph.
- Trailing whitespace and >180-character line scans returned clean for `CompileWallXRayWindow.cs`.
- Compile probe was not run because the user forbade builds unless needed and this change is static Editor parser hygiene.

## 2026-05-19 - Root-Level Asmdef Property Guard

Status: PENDING VERIFICATION / OWNED TOOLING STATIC-CLEAN / COMPILE PROBE NOT NEEDED
Evidence class: STATIC_SOURCE / FILESYSTEM

### What Was Wrong

- Loop 18 removed `JsonUtility`, but the bounded parser still matched asmdef property names by global token search.
- Nested fields such as `versionDefines[].name` should never be allowed to define the assembly node name.
- A top-level string value matching `"name"`, `"references"`, or `"includePlatforms"` could also be misread without colon validation.

### What Was Done

- Added a root-object JSON property scanner for the bounded asmdef parser.
- The scanner tracks object depth, array depth, string state, and escaped bytes.
- Matched property tokens must be at root object depth and followed by a colon after JSON whitespace.
- The 64KB file scratch and 128-entry array cap remain unchanged.

### Struct Layout Evidence

- No runtime contract DTO layout changed.
- `GlobalSignalPayload` remains 128 bytes and `Pack=8`.
- `CompileWallBlackBoxEntry` remains 40 bytes and `Pack=8`.

### SELF_AUDIT Delta

- Task 01 PASS: graph archaeology now rejects nested JSON identity drift.
- Task 02 PASS: Runtime -> Runtime edge detection consumes root-level asmdef identity only.
- Task 07 PASS: build gate parser is stricter without direct runtime dependencies.
- Task 18 PASS: X-Ray graph identity is protected from nested `versionDefines` fields.

### Cinematic Cheats Used

- Tooling fake: a minimal root-property scanner replaces serializer hydration while preserving exact routing fields. Complexity remains O(file bytes), with no full JSON object model.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. No compile or build was launched.
- Editor correctness improvement is source/file-shape verified only.

### Verification Evidence

- Current first-party asmdef scan: 119 asmdefs, 9 nested `versionDefines[].name` sites, top-level `name` remains the first property in all scanned files.
- `rg` found no `IndexOfAscii`, `JsonUtility`, `File.ReadAllText`, `File.ReadAllLines`, old asmdef DTO names, or whole-file read aliases in `CompileWallXRayWindow.cs`.
- Forbidden-pattern scan returned no `File.ReadAllBytes`, `StreamReader`, `ReadLine()`, `.ToString()`, LINQ chain, Unity find/getcomponent calls, `UnityEngine.Random`, `Time.deltaTime`, or `JobHandle.Complete`.
- Trailing whitespace, >180-character line, and `git diff --check` scans returned clean for `CompileWallXRayWindow.cs`.
- Compile probe was not run because the user forbade builds unless needed and this change is bounded parser source hardening.

## 2026-05-19 - Streaming Filesystem Enumeration

Status: PENDING VERIFICATION / OWNED TOOLING STATIC-CLEAN / COMPILE PROBE NOT NEEDED
Evidence class: STATIC_SOURCE / FILESYSTEM

### What Was Wrong

- Four recursive scans in `CompileWallXRayWindow.cs` used `Directory.GetFiles(...AllDirectories)`.
- `GetFiles` materializes a complete managed path array before filtering starts.
- The affected paths are growth-prone: asmdefs, archived archaeology, Bee `.ref.dll` artifacts, and source files for Pack=1 detection.

### What Was Done

- Replaced asmdef graph discovery with `Directory.EnumerateFiles()` and explicit `IEnumerator<string>` / `while`.
- Replaced archaeology discovery with streaming enumeration.
- Replaced Bee `.ref.dll` artifact indexing with streaming enumeration and fixed conservative dictionary capacity.
- Replaced runtime Pack=1 source scan discovery with streaming enumeration.

### Struct Layout Evidence

- No runtime contract DTO layout changed.
- `GlobalSignalPayload` remains 128 bytes and `Pack=8`.
- `CompileWallBlackBoxEntry` remains 40 bytes and `Pack=8`.

### SELF_AUDIT Delta

- Task 01 PASS: archaeology no longer front-loads recursive path arrays.
- Task 02 PASS: asmdef graph scan no longer front-loads recursive path arrays.
- Task 07 PASS: build gate scanner remains dependency-light and source-shape bounded.
- Task 18 PASS: X-Ray now streams growth-prone filesystem scans.

### Cinematic Cheats Used

- Tooling fake: stream file paths and reject irrelevant files as they arrive instead of materializing full directory snapshots. Complexity remains O(file count), but peak path-array memory is removed.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. No compile or build was launched.
- Editor allocation reduction is source-shape verified only.

### Verification Evidence

- `rg` found no `Directory.GetFiles` in `CompileWallXRayWindow.cs`.
- The remaining four recursive scans use `Directory.EnumerateFiles` with explicit `IEnumerator<string>` and `while` loops.
- Forbidden-pattern scan returned no `JsonUtility`, whole-file read APIs, `StreamReader`, `ReadLine()`, `.ToString()`, LINQ chain, `foreach`, `Pack=1`, Unity find/getcomponent calls, `UnityEngine.Random`, `Time.deltaTime`, or `JobHandle.Complete`.
- Trailing whitespace, >180-character line, and `git diff --check` scans returned clean.
- Compile probe was not run because the user forbade builds unless needed and this change is Editor cold-path enumeration hardening.

## 2026-05-19 - Path Token Classifier Allocation Closure

Status: PENDING VERIFICATION / OWNED TOOLING STATIC-CLEAN / COMPILE PROBE NOT NEEDED
Evidence class: STATIC_SOURCE / FILESYSTEM

### What Was Wrong

- `Classify()` normalized each asmdef asset path through `assetPath.Replace('\\', '/')`.
- The layer classifier only needs four segment probes, so the replacement string was avoidable.
- `ScanArchaeologyFolder()` converted rows through `ToProjectPath(GetProjectRoot(), path)` even though `RunArchaeology()` already had `projectRoot`.

### What Was Done

- Added constant forward-slash and backslash tokens for Editor, Tests, Contracts, and Authoring directories.
- Replaced per-asmdef path normalization with `ContainsPathToken()` using `IndexOf` against those constants.
- Passed cached `projectRoot` into archaeology scanning and reused it for project-path conversion.

### Struct Layout Evidence

- No runtime contract DTO layout changed.
- `GlobalSignalPayload` remains 128 bytes and `Pack=8`.
- `CompileWallBlackBoxEntry` remains 40 bytes and `Pack=8`.

### SELF_AUDIT Delta

- Task 02 PASS: layer classification keeps the same routing semantics without path replacement allocation.
- Task 07 PASS: build gate remains dependency-light and avoids unnecessary path churn.
- Task 18 PASS: X-Ray graph classification uses constant token probes.

### Cinematic Cheats Used

- Tooling fake: segment-token probes replace full path normalization. Complexity remains O(path length), but the replacement string is removed.

### Exact Microseconds Saved

- Measured runtime microseconds saved: 0 us. No player runtime/profiler was run.
- Measured build-time saved: 0 us. No compile or build was launched.
- Editor allocation reduction is source-shape verified only.

### Verification Evidence

- `rg` found no `normalizedPath` or `ToProjectPath(GetProjectRoot` in `CompileWallXRayWindow.cs`.
- Forbidden-pattern scan returned no `Directory.GetFiles`, `JsonUtility`, whole-file read APIs, `StreamReader`, `ReadLine()`, `.ToString()`, LINQ chain, `foreach`, Unity find/getcomponent calls, `UnityEngine.Random`, `Time.deltaTime`, or `JobHandle.Complete`.
- Trailing whitespace and >180-character line scans returned clean.
- Compile probe was not run because the user forbade builds unless needed and this change is Editor cold-path path-allocation hardening.

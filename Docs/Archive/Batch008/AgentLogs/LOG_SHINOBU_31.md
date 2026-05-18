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
- Editor-only expected hits remain in `Editor/AssemblyGuard` for display `ToString()` calls and the required `NativeArray` compile blackbox.
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
- Editor-only expected hits remain for X-Ray display formatting and the required 300-entry NativeArray blackbox.
- Authoring response first-party refs: `Hecton8.Global.Contracts.ref.dll` and `Hecton8.MockDomain.Contracts.ref.dll` only.
- Direct trailing-whitespace scan clean for SHINOBU_31 text files; generated `link.xml` has no BOM.

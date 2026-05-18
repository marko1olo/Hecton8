# Rationale_SHINOBU_31

Date: 2026-05-18
Status: PENDING VERIFICATION / OWNED SLICE CLEAN / FULL UNITY COMPILE BLOCKED BY DEPENDENCY
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / UNITY_BEE_PARTIAL / UNITY_ROSLYN_ISOLATED.

## SELF_AUDIT

<SELF_AUDIT>
  <runtime_to_runtime_direct_refs>PASSED_FOR_NEW_WORK: current `Assets/_Project` baseline contains 81 illegal Runtime -> Runtime edges across 109 asmdefs, but SHINOBU_31 Global/MockDomain additions contribute 0. Build gate now blocks future spread.</runtime_to_runtime_direct_refs>
  <global_signal_payload_alignment>PASSED: `GlobalSignalPayload` is explicit 128 bytes, `Pack=8`, 16-byte header, 112-byte payload body.</global_signal_payload_alignment>
  <cs1612_contract_shape>PASSED_FOR_NEW_WORK: SHINOBU_31 contracts use raw handles, function pointers, and `ref MockDomainState`; no `{ get; set; }` array surfaces.</cs1612_contract_shape>
  <mock_dependencies>PASSED: mock runtime Bee response references only Global.Contracts, MockDomain.Contracts, Burst, and Mathematics; no Core/UI/Physics refs.</mock_dependencies>
  <authoring_dependencies>PASSED: mock authoring assembly compiles from current source against Global.Contracts and MockDomain.Contracts only; no Runtime/Core/UI/Physics first-party refs.</authoring_dependencies>
  <compile_wall_xray>PASSED_SYNTAX: Editor facade implemented; current isolated Unity Roslyn compile returned 0 errors after zero-GC UI polish. Full Unity import is blocked by external domain/Bee state errors.</compile_wall_xray>
  <blackbox_native_ring>PASSED_CODE_PATH: compile-domain blackbox uses a 300-entry `NativeArray<CompileWallBlackBoxEntry>` and writes `Dump_SHINOBU_31.h8dump` on fatal build-gate failure.</blackbox_native_ring>
  <zero_gc_editor_polish>PASSED_FOR_SOURCE: SHINOBU_31 source paths contain no `.ToString()` calls after Loop 7; X-Ray numeric UI uses IMGUI numeric fields and compile samples reuse captured path strings.</zero_gc_editor_polish>
  <bootstrap_layout_alignment>PASSED: Loop 8 dotnet layout audit prints `BootstrapRegistryContext=80` and `BootstrapDependencySnapshot=80`, both with explicit `Reserved` at offset 72.</bootstrap_layout_alignment>
  <quality_continuum_routing>PASSED_FOR_CONTRACT: `AssemblyRoutingOverride` now carries `MinQualityWeight`, `MaxQualityWeight`, and `QualityCurveHash` while remaining 64 bytes. CSV parsing hydrates optional 0..1 weights without string allocation.</quality_continuum_routing>
  <engine_reference_isolation>PASSED_FOR_ASMDEF: `Hecton8.Global.Contracts`, `Hecton8.MockDomain.Contracts`, and `Hecton8.MockDomain.Runtime` now set `noEngineReferences=true`; Authoring keeps UnityEngine by design.</engine_reference_isolation>
</SELF_AUDIT>

## Decision 00 - Baseline Graph First

Problem: The user asked for assembly surgery in a repository with a dirty worktree and 40+ concurrent agents. Blind edits to `Hecton8.Core.asmdef` would corrupt unrelated work.

Solution: Read `CURRENT_BATCH.md`, `AGENTS.md`, domain map, selected mandates, docs index, architecture index, and current `.asmdef` graph before writing code. Treat `Docs/DEPENDENCY_GRAPH.md` as static orientation, not proof.

Rejected Alternatives: Directly removing Core references was rejected because source still imports leaf namespaces and would create immediate compile breakage. Adding more leaf references was rejected because it increases Compile Wall debt.

Scalability potential: Low tier benefits from smaller compile/runtime surfaces; middle/high/ultra benefit from leaf systems staying independently swappable so saved CPU can buy visual overkill rather than monolithic boot cost.

Hardware Impact: Estimated low-end i3/MX350 gain is editor iteration time, not frame time. Runtime hot-path gain remains PENDING VERIFICATION until dependent systems adopt function-pointer facades.

## Decision 01 - Gate Instead Of Silent Drift

Problem: Existing `.asmdef` graph already violates the target Runtime -> Runtime rule. Without an automated gate, more agents will add direct dependencies.

Solution: Implement an Editor pre-build validator that parses first-party asmdefs, classifies Contracts/Runtime/Authoring/Editor/Test, and fails illegal Runtime -> Runtime references with explicit source/target paths.

Rejected Alternatives: Manual review and dated reports were rejected; they have already allowed Core graph debt to exist. Runtime reflection scanners were rejected; the graph is static build metadata and must be checked before build.

Scalability potential: Low tier avoids dragging high-end graphics/runtime assemblies into every build. Middle/high/ultra can compile optional domains independently and spend runtime budgets on visual detail.

Hardware Impact: Editor-only check estimated under 0.1 ms per 100 asmdef files on desktop. It prevents 10-minute rebuild cascades rather than improving frame time directly.

## Decision 02 - Global Contracts Spine

Problem: The project has Core-centered assembly gravity. Leaf runtime domains can accidentally pull each other through Core or direct asmdef references, causing compile cascades and IL2CPP reachability bloat.

Solution: Added `Hecton8.Global.Contracts.asmdef` with unmanaged DTOs, enums, `IBootstrapNode`, `IStaticResetNode`, raw buffer handles, and a `PhysicsFacade` that carries a Burst `FunctionPointer<T>` plus DataVault handle. The assembly references only Burst, Collections, and Mathematics.

Rejected Alternatives: Adding contracts into `Hecton8.Core.asmdef` was rejected because Core already owns too many leaf references. Reflection DI was rejected because boot-time assembly scans are slow and fragile under IL2CPP stripping.

Scalability potential: Low devices use the same tiny contract spine without compiling optional high-end domains. Middle/high/ultra builds can attach richer implementations behind the same facade and spend the saved compile/runtime budget on visual overkill.

Hardware Impact: Runtime frame gain is indirect. Low-end i3/MX350 impact is primarily iteration/build reduction; hot-path call shape avoids managed interface dispatch and boxing when implementations register function pointers.

## Decision 03 - Explicit Payload And Alias Layouts

Problem: Cross-assembly signal and memory aliases can corrupt ARM64/mobile targets if implicit padding differs between assemblies or if writable slices alias silently.

Solution: Defined explicit `Pack=8` layouts: `GlobalSignalPayload` = 128 bytes, `GlobalNativeBufferHandle` = 32 bytes, `NativeMemoryAliasContract` = 64 bytes, and `AssemblyRoutingOverride` = 64 bytes. Added `[NoAlias]` to the alias contract and owner/generation/range fields.

Rejected Alternatives: `Pack=1` and managed property wrappers were rejected. They are slower to reason about, easier to break with CS1612, and hostile to Burst vectorization.

Scalability potential: Low tier gets deterministic fixed-size payload dispatch. Middle/high/ultra tiers can reinterpret the same 112-byte payload body into richer visual or simulation structs without adding generic SignalBus instantiations.

Hardware Impact: Estimated low-end i3/MX350 gain is avoiding defensive copies/locks around shared memory slices. Exact microsecond gain depends on adopter domains; SHINOBU_31 code itself adds no runtime loop.

## Decision 04 - Mock Domain Isolation

Problem: The assignment required proof that a runtime implementation can compile without UI, Physics, or Core implementation references.

Solution: Added `Hecton8.MockDomain.Contracts.asmdef` and `Hecton8.MockDomain.Runtime.asmdef`. The runtime references only `Hecton8.Global.Contracts`, its own contracts, Burst, and Mathematics. The implementation is a readonly struct with static Burst function pointer target, so `Create()` returns `default` and does not allocate a managed object.

Rejected Alternatives: A sealed class mock allocation was rejected during self-audit because it would make `Create()` heap-backed. Direct references to `Hecton8.Core`, `Hecton8.Physics`, or `Hecton8.UI` were rejected and verified absent in the Bee response file.

Scalability potential: Low tier can route to cheap mocks/stubs. Middle/high/ultra can route to richer implementations without changing caller assemblies.

Hardware Impact: Low-end i3/MX350 avoids managed allocation during mock creation. Function pointer invoke cost remains pending real domain adoption, but the bridge shape is Burst-compatible.

## Decision 05 - Build Gate, X-Ray, And CSV Routing

Problem: Runtime-to-runtime debt is invisible until build time, and manual inspection does not scale with 40+ agents.

Solution: Added `CompileWallAsmdefBuildGate`, `CompileWallXRayWindow`, 300-entry `NativeArray` compile blackbox telemetry, and span-based CSV routing override parser. The window scans asmdefs, draws illegal Runtime -> Runtime edges red, and exposes the no-domain-reload toggle.

Rejected Alternatives: Text-only reports were rejected because they do not stop builds. Runtime reflection scanning was rejected because asmdef dependencies are static metadata and must be blocked before player build.

Scalability potential: Low devices benefit from smaller compiled surfaces. Middle/high/ultra builds can keep high-cost visual domains optional and visible in the graph instead of becoming hidden dependencies.

Hardware Impact: Editor-only scan cost measured by code path, not profiler. Estimated under 300 us per asmdef JSON on workstation hardware; no runtime frame cost.

## Decision 05B - Compile-Domain Blackbox

Problem: The first polish pass still treated blackbox as partial because telemetry used managed samples only and did not own a fixed native crash/dump lane.

Solution: Added `CompileWallBlackBox` with `NativeArray<CompileWallBlackBoxEntry>(300, Allocator.Persistent)`. Graph scans and compile samples write high-level state into the ring. Fatal Runtime -> Runtime build-gate failure dumps `Docs/AgentLogs/Dump_SHINOBU_31.h8dump` before throwing `BuildFailedException`.

Rejected Alternatives: Leaving the managed 300-sample UI buffer as the only evidence was rejected. Runtime `NativeArray` ownership inside leaf domains was rejected; this is editor tooling, so the persistent native buffer is scoped to the Editor assembly and disposed on assembly reload/quitting.

Scalability potential: Low-end developer machines get postmortem visibility without rerunning the full compile. Middle/high/ultra workstations get the same data and can afford richer future graph diagnostics without changing runtime domains.

Hardware Impact: Editor-only persistent native memory is 300 x 40 bytes = 12,000 bytes plus NativeArray header. Dump happens only on fatal build gate or invalid timing state; no player frame cost.

## Decision 05C - Authoring Slice Closure

Problem: The earlier mock proof covered Contracts and Runtime, but strict three-tier architecture also requires an Authoring assembly that can expose designer-editable data without depending on Runtime implementation code.

Solution: Added `Hecton8.MockDomain.Authoring.asmdef` and `MockDomainAuthoringProfile`. The Authoring assembly references only `Hecton8.Global.Contracts`, `Hecton8.MockDomain.Contracts`, and `Unity.Mathematics`; UnityEngine is present only as the ScriptableObject authoring surface. `BuildRoutingOverride()` emits contract-level route hashes. `BuildInitialState()` emits `MockDomainState` with `double3` AUP from serialized `double` fields.

Rejected Alternatives: A direct Authoring -> Runtime reference was rejected because it would force implementation recompiles when designers edit authoring code. A `Vector3` AUP authoring field was rejected during polish because it silently narrows absolute world coordinates to float before restoring them to double.

Scalability potential: Low devices can bind mock/cheap implementations from asset data without recompiling runtime domains. Middle/high/ultra tiers can swap richer implementations behind the same contract route while keeping presentation authoring separated from gameplay truth.

Hardware Impact: Runtime frame gain is 0 us because this is an authoring facade. Compile-wall protection gain is structural: current-source Roslyn proof shows the Authoring assembly has no first-party Runtime/Core/UI/Physics references.

## Decision 06 - IL2CPP Shield And Offset Generation

Problem: Late-bound contract and registry surfaces can be stripped by IL2CPP, and blind pointer math needs stable offsets without direct assembly references.

Solution: Added generated `link.xml` preserving global contract assemblies and 114 scanned `Hecton8.Core.GlobalRegistry` contract surface types. Added `VaultOffsets.g.cs` and Editor generator hooks for link/offset refresh.

Rejected Alternatives: Trusting IL2CPP reachability was rejected. Hand-maintained offset notes were rejected because they rot when structs change.

Scalability potential: Low tier avoids mobile-only stripping crashes. Middle/high/ultra can keep optional implementations stripped or preserved intentionally instead of incidentally.

Hardware Impact: Build-time only. Runtime impact is crash prevention and pointer math stability, not measurable frame-time gain in SHINOBU_31 alone.

## Decision 07 - Compile Wall Status

Problem: Full Unity compile cannot currently complete. Errors after R3-R6 are outside SHINOBU_31 ownership and move between Core, Quest, Rendering, Audio Editor, Fauna, and other concurrent-agent files.

Solution: Fixed the one local R4 error (`[BurstCompile]` is invalid on delegates in Unity 6000.4.1f1) and verified `CompileWallXRayWindow.cs` through isolated Unity Roslyn with the generated `Hecton8.Editor.rsp`. Marked full Unity compile `[BLOCKED BY DEPENDENCY]` with exact blocker files in Status.

Rejected Alternatives: Editing Quest/UI/Rendering/Fauna internals was rejected as domain sabotage. Reverting other agents' untracked files was rejected. Reporting green compile was rejected because R6 exits with compiler errors.

Scalability potential: The SHINOBU_31 layer is ready to prevent future asmdef debt, but the existing project remains blocked until external domain errors are repaired.

Hardware Impact: No runtime hardware claim is made while full project compile is blocked. Editor iteration protection is implemented; measured end-to-end compile savings require a clean baseline after dependency repair.

## Decision 08 - Active State Recovery And X-Ray Zero-GC Polish

Problem: Batch008 archival moved SHINOBU_31 status/rationale/log files out of the active protocol paths, and the X-Ray Editor UI still contained `.ToString()` formatting in repaint code. That violated the mandate's active-memory and zero-GC UI intent even though the runtime contract path was clean.

Solution: Restored active `Docs/Tasks/CURRENT_BATCH.md`, `Docs/Tasks/Status_SHINOBU_31.md`, `Docs/AgentLogs/Rationale_SHINOBU_31.md`, and `Docs/AgentLogs/LOG_SHINOBU_31.md` from Batch008 archive. Replaced numeric `LabelField(...ToString())` calls with disabled `IntField`/`DoubleField` controls, removed per-repaint `Path.GetFileNameWithoutExtension`, split illegal-edge UI into existing string fields instead of concatenating labels, and rewrote `link.xml` generation to stream through `TextWriter` instead of materializing a `StringBuilder.ToString()` buffer.

Rejected Alternatives: Leaving archive-only state was rejected because the anti-amnesia protocol requires active files. Keeping `.ToString()` in Editor-only UI was rejected because the user explicitly tightened the zero-GC UI rule. Re-running a full Unity compile was rejected after `Hecton8.Editor.rsp` failed on missing external `Hecton8.Core.ref.dll`; isolated Roslyn compile was used for the owned file instead.

Scalability potential: Low-end developer machines get fewer Editor repaint allocations from the X-Ray window and a restored active state trail. Middle/high/ultra workstations get the same graph guard without adding runtime dependencies or bloating contract assemblies.

Hardware Impact: Runtime frame gain is 0 us because the change is Editor tooling. Editor allocation reduction is source-verified, not profiler-measured. The owned assembly slice (`Global.Contracts`, `MockDomain.Contracts`, `MockDomain.Runtime`, `MockDomain.Authoring`) returned Unity Roslyn exit 0. The isolated Editor facade compile returned 0 errors; two CS0618 warnings remain because Unity marks the required per-assembly timing hook obsolete.

## Decision 09 - Bootstrap Layout Correction And Bounded CSV Read

Problem: Loop 7 exposed a real ARM64 contract defect: `BootstrapRegistryContext` and `BootstrapDependencySnapshot` were declared as 64-byte explicit structs, while the current runtime field map actually required 72 bytes. That mismatch meant the published contract size and the generated offset ledger were lying. The CSV override reader also used `File.ReadAllBytes`, creating an avoidable cold allocation in a tool that is supposed to model allocation discipline.

Solution: Changed both bootstrap DTOs to explicit `Size=80`, `Pack=8`, and added an explicit `ulong Reserved` at offset 72. Synchronized `VaultOffsets.g.cs` and the Editor generator constants to `80`. Rebuilt the temporary layout audit and verified offsets. Replaced `File.ReadAllBytes` with a bounded static `CsvScratch` buffer and `FileStream.Read` loop before parsing a `ReadOnlySpan<byte>`.

Rejected Alternatives: Keeping `Size=64` was rejected because the CLR layout audit contradicted it. Truncating the buffer tables to force a smaller struct was rejected because it would reduce bootstrap capacity for no runtime gain. Leaving `File.ReadAllBytes` was rejected because this domain is explicitly tasked with defending build/edit iteration discipline, even in Editor-only support code.

Scalability potential: Low/MX350/ARM64 devices get stable 8-byte-aligned cross-assembly contracts and fewer surprise mobile-only layout faults. Middle/high/ultra builds can safely add richer implementation nodes behind the same bootstrap tables without changing caller assemblies.

Hardware Impact: Runtime microseconds saved: 0 us measured. The gain is risk removal and compile-contract correctness. Editor allocation reduction for CSV read is source-verified only; no profiler measurement was run.

## Decision 10 - Quality Continuum And No-Engine Assembly Isolation

Problem: The routing contract still exposed only coarse `HardwareQualityRoute` enum bounds. That violated the newer continuous scalability mandate because routing could only express Low/Middle/High/Ultra buckets, not a `GlobalQualityWeight` continuum. The mock Burst target also used a bare `[BurstCompile]` attribute instead of the explicit compile/float flags demanded by the mandate. Finally, contract/runtime asmdefs that do not need `UnityEngine` were still allowing engine references by default.

Solution: Extended `AssemblyRoutingOverride` in-place with `float MinQualityWeight` at offset 20, `float MaxQualityWeight` at offset 24, and `uint QualityCurveHash` at offset 28, preserving the 64-byte ABI. Added optional span-based CSV parsing for the two weights and curve token. Added designer-facing min/max weight fields in `MockDomainAuthoringProfile` and clamped them into 0..1 with `math.clamp` and `math.max`. Changed `MockApplyForce` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. Set `noEngineReferences=true` on `Hecton8.Global.Contracts`, `Hecton8.MockDomain.Contracts`, and `Hecton8.MockDomain.Runtime`.

Rejected Alternatives: Adding a second routing DTO was rejected because it would expand public contract surface and invite parallel routing truth. Replacing the enum bounds was rejected because existing authoring and CSV rows may still need coarse gates for build-time routing. Leaving UnityEngine auto-references enabled was rejected because contracts/runtime proof should physically prevent accidental `UnityEngine` creep.

Scalability potential: Low/MX350/Quest can route mocks or cheap implementations when `GlobalQualityWeight` approaches 0.0 without binary low/high switches. Middle/high/ultra can bind richer implementations behind the same contract row as weight approaches 1.0, while preserving hysteresis and compile isolation.

Hardware Impact: Runtime microseconds saved: 0 us measured. This is ABI and compile-wall hardening. The expected gain is preventing high-tier implementation assemblies from being pulled into low-tier iteration or player builds by accident.

## Decision 11 - Legacy Graph Header Endian Audit

Problem: Task 01 required archaeology for legacy `asmdef_graph_*.h8bin` data, but the scanner only recorded matching file paths. That found the graveyard file, but it did not prove the tool could safely hydrate a legacy binary header, detect endian drift, or preserve enough blackbox evidence for a bad archive graph.

Solution: Added `CompileWallLegacyGraphHeader` as an explicit 32-byte, `Pack=8` cold-path DTO with offsets `0 Magic`, `4 Version`, `6 HeaderBytes`, `8 NodeCount`, `12 EdgeCount`, `16 Flags`, `20 PayloadBytes`, `24 Crc32`, `28 Reserved`. The archaeology scan now opens matching graph binaries with `FileStream`, reads exactly 32 bytes into a static scratch buffer, parses little-endian fields by default, and switches to big-endian only when the big-endian `HeaderBytes` value is plausible and the little-endian value is not. Parsed headers are counted in the X-Ray UI and encoded into compile blackbox flags.

Rejected Alternatives: Keeping path-only archaeology was rejected because it leaves Task 01 under-proven. `File.ReadAllBytes` was rejected because Loop 8 already removed that allocation pattern. `math.reversebytes` was attempted because the mandate named it, but Unity Roslyn proved the installed `Unity.Mathematics` package does not expose that API; a local `ReverseUInt32` bit-shift path was kept instead because it compiles and preserves the endian behavior.

Scalability potential: Low-end developer machines can inspect old batch graph payloads without loading entire binaries into managed memory. Middle/high/ultra workstations get the same cold-path archive evidence and can extend the header reader into a full graph loader later without changing runtime assemblies.

Hardware Impact: Runtime microseconds saved: 0 us measured. Editor cold-path allocation pressure is reduced relative to full-file byte reads. Header DTO is 32 bytes, aligned to 8, and not an atomic/shared counter, so 64-byte false-sharing padding is not required.

## Decision 12 - Owned Editor Warning Debt Purge

Problem: After Loop 10 the owned Editor guard compiled, but it still emitted warning debt: CS0618 from `CompilationPipeline.assemblyCompilationStarted` and CS0649 from JsonUtility DTO fields. A compile-wall guard that normalizes warnings trains engineers to ignore compiler signal, which is the same failure mode as accepting direct Runtime-to-Runtime dependencies.

Solution: Replaced the obsolete `assemblyCompilationStarted` timing hook with non-obsolete `compilationStarted`, `assemblyCompilationFinished`, and `compilationFinished`. The recorder now measures a compilation-window segment per finished assembly instead of depending on Unity's obsolete per-assembly start event. JsonUtility DTO fields now have explicit cold defaults (`string.Empty`, `Array.Empty<T>()`, and `false`) so the compiler no longer reports them as unassigned while JsonUtility can still hydrate them.

Rejected Alternatives: `#pragma warning disable` was rejected because it hides owned debt. Keeping the obsolete hook was rejected because Unity already marks that timing path as unstable. Editing external `SignalTrafficMonitorWindow.cs` after the full Editor probe exposed duplicate `SignalLaneTelemetry` was rejected as cross-domain ownership breach.

Scalability potential: Low-end developer machines receive cleaner compile output, so real errors are easier to see without opening the full Unity Console. Middle/high/ultra workstations get the same warning-free guard while preserving the lightweight 300-sample telemetry ring.

Hardware Impact: Runtime microseconds saved: 0 us measured. Editor compile warning count in `CompileWallXRayWindow.cs` changed from 2 CS0618 plus CS0649 JsonUtility warnings to zero warnings in isolated Unity Roslyn compile.

## Decision 13 - Stale Contract Artifact Skew Guard

Problem: The latest full Editor probe fails in `SignalTrafficMonitorWindow.cs` with CS0433 because `SignalLaneTelemetry` appears in both `Hecton8.Core.Contracts` and `Hecton8.Core`. Current source evidence does not show a second source definition in Core: `Hecton8.Core.rsp` includes `GlobalSignals.cs`, references `Hecton8.Core.Contracts.ref.dll`, and does not include `Core/Contracts/HectonSignalLaneContract.cs`. The remaining failure shape is stale Bee reference metadata: `Hecton8.Core.ref.dll` is older than the freshly rebuilt Contracts ref by 36230.8176631 seconds.

Solution: Added `CompileWallArtifactSkew` detection to the owned `CompileWallXRayWindow.cs` scanner. The scan now builds one indexed pass over `Library/Bee/artifacts/**/*.ref.dll`, then flags every implementation/editor assembly whose `.ref.dll` is older than a referenced `*.Contracts.ref.dll`. X-Ray displays `Stale ref artifacts`, lists the implementation assembly, contract assembly, seconds behind, and stale ref path. The compile-domain blackbox writes the skew count into `WarningCount` and sets graph-scan flag bit `4`.

Rejected Alternatives: Editing `SignalTrafficMonitorWindow.cs` was rejected because a fully qualified type name cannot disambiguate two assemblies exporting the same full type name and the file is outside SHINOBU_31 ownership. Editing `GlobalSignals.cs` was rejected because the current source already moved the low-risk DTOs into `Core.Contracts`; forcing another Core surgery would risk another agent's work. Launching `dotnet build` or a new C# compile probe was rejected because the user forbade it unless needed, active `csc`/`dotnet` processes were present, and CPU stayed near 100%. Reflection/metadata readers inside the guard were rejected because this domain should not introduce another allocation-heavy Editor dependency scanner when timestamp skew explains the immediate blocker.

Scalability potential: Low-end developer machines get an immediate visual explanation for stale contract metadata instead of burning minutes on repeated full compiles. Middle/high/ultra workstations get the same signal while retaining the strict Contracts -> Runtime separation and avoiding new sibling runtime references.

Hardware Impact: Runtime microseconds saved: 0 us measured. Editor gain is diagnostic: one indexed Bee artifact pass replaces blind repeated compile attempts. The static hot path remains unaffected.

## Decision 14 - Blackbox Dump Version Discipline

Problem: Loop 12 added `ArtifactSkews.Count` to the `.h8dump` header but left the dump version at `1`. That is binary-format drift. A future reader could parse version 1 data with the wrong field map and misdiagnose a compile-wall incident.

Solution: Added explicit `DumpMagic` and `DumpVersion = 2` constants in `CompileWallBlackBox`. Replaced raw graph flag literals with named constants: emergency mock atlas = `1`, legacy graph headers = `2`, stale contract artifact = `4`. Reworked Bee ref artifact indexing to store path plus `WriteTimeUtcTicks` in `CompileWallRefArtifactRecord`, so the stale-artifact scanner does not call `File.GetLastWriteTimeUtc` again for every dependency edge.

Rejected Alternatives: Leaving version `1` was rejected because the header has changed. Adding a managed reflection metadata reader was rejected because the timestamp evidence is enough for the current stale-artifact diagnosis and reflection would add another heavy Editor dependency surface. Launching a compile probe was rejected because active `csc`/`dotnet` processes were present and CPU measured `100`, directly violating the user and AGENTS.md build guards.

Scalability potential: Low-end developer machines avoid repeated metadata calls and get deterministic dump parsing after a fatal build gate. Middle/high/ultra machines get the same blackbox ABI discipline without widening runtime contracts or adding sibling Runtime references.

Hardware Impact: Runtime microseconds saved: 0 us measured. Editor cold-path gain is reduced file timestamp metadata pressure during X-Ray scans; no profiler claim is made.

## Decision 15 - ARM64 Pack=1 Breach Surfacing

Problem: A wider runtime-side source scan found 369 non-Editor `StructLayout(...Pack = 1)` hits, including Core contract files such as `MacroDatabaseContracts.cs`. Mass-changing those DTOs would cross domain boundaries, mutate binary/serialization contracts, and likely break concurrent agents without a clean compile lane.

Solution: Added `CompileWallPackViolation` scanning to the owned X-Ray tool. The scanner walks `Assets/_Project/Scripts`, skips Editor folders, detects `StructLayout` directives with `Pack = 1`, and records path, line, and directive snippet. The X-Ray window now displays `ARM64 Pack=1 hits` plus a capped violation table. The blackbox warning count now includes stale artifacts plus Pack=1 hits, graph flag `8` marks the ARM64 breach class, and `.h8dump` version is bumped to `3` because the dump header now writes `PackViolations.Count`.

Rejected Alternatives: Editing 369 foreign DTO sites was rejected because this agent's safe mandate is compile architecture and owned tooling, not blind rewrites of AI/Animation/Core binary contracts under 100% CPU and active C# compilers. Ignoring the Pack=1 evidence was rejected because it violates the ARM64 mandate. Hiding the count in logs only was rejected because the X-Ray facade is where architects inspect dependency and ABI debt.

Scalability potential: Low/Quest/ARM64 devices get a visible list of misalignment risks before another agent ships them into NativeArray/Burst paths. Middle/high/ultra builds get the same ABI debt inventory without adding runtime assembly references.

Hardware Impact: Runtime microseconds saved: 0 us measured. Editor diagnostic gain is measurable by source shape only: one X-Ray scan now exposes 369 known Pack=1 candidates instead of requiring repeated grep passes.

## Decision 16 - Bounded Pack Scanner State Machine

Problem: Loop 14 made Pack=1 debt visible, but the first implementation used `StreamReader.ReadLine()` and `line.Trim()`, producing avoidable per-line/per-hit managed strings in the X-Ray scan path. It also only detected single-line `StructLayout` attributes and could false-positive `Pack=10` after the following delimiter.

Solution: Replaced the line reader with a bounded 8192-byte `FileStream` scanner that tracks C# attribute blocks across line and chunk boundaries. The scanner skips line/block comments, detects `StructLayout` and `Pack = 1` as ASCII token state, records the attribute start line, uses a constant snippet string, and explicitly resets if a digit follows the `1` so `Pack=10` is not counted.

Rejected Alternatives: A Roslyn syntax parser was rejected because it would add more Editor dependency surface and more assembly load pressure to the compile-wall tool. Reflection/metadata loading was rejected for the same reason. Leaving the `ReadLine()` path was rejected because this agent is explicitly policing allocation and compile-tool hygiene.

Scalability potential: Low-end developer machines get the ARM64 ABI debt scan without per-line string churn. Middle/high/ultra workstations get broader multi-line detection while keeping the tooling independent of sibling runtime domains.

Hardware Impact: Runtime microseconds saved: 0 us measured. Editor cold-path allocation pressure is reduced by source inspection; no profiler claim is made.

## Decision 17 - Pack Violation Storage Budget Cap

Problem: The X-Ray Pack=1 scanner now exposes foreign ABI debt, but the detail list itself could grow without a fixed ceiling if another batch adds hundreds or thousands of packed runtime DTOs. That would turn an architectural diagnostic into another managed-memory pressure point.

Solution: Split Pack=1 accounting into `PackViolationTotal` for the authoritative count and `PackViolations` for a capped detail table with `MaxStoredPackViolations = 512`. The scanner increments the total for every hit, stores only the first 512 rows, lazily caches the per-file project path only when a row is retained, and the X-Ray UI reports hidden rows from the total. The blackbox warning count now uses the total count, and `.h8dump` version 4 writes both total and stored Pack counters.

Rejected Alternatives: Leaving `List<CompileWallPackViolation>` unbounded was rejected because foreign source debt should not control editor memory growth. Silently truncating the count was rejected because the architect needs the real ABI breach pressure. Launching a compile probe was rejected because this was a static Editor-tool accounting change and the user explicitly forbade builds unless needed.

Scalability potential: Low-end developer machines get bounded X-Ray memory pressure even when foreign runtime DTO debt spikes. Middle/high/ultra workstations still retain enough detail rows for triage without widening any runtime assembly dependency.

Hardware Impact: Runtime microseconds saved: 0 us measured. Editor cold-path memory growth is capped by source shape; no profiler measurement was run.

## Decision 18 - Source Preservation Byte Scanner

Problem: After the Pack scanner cleanup, the same owned Editor tool still had two whole-file line-array paths: `.meta` GUID parsing and `link.xml` source preservation scanning. `File.ReadAllLines()` plus string `Trim()` is acceptable for many Editor utilities, but this domain is explicitly the compile-wall hygiene tool and should not model allocation-heavy source scans.

Solution: Replaced `.meta` GUID parsing with a bounded byte scanner that detects `guid:` and copies exactly 32 hex characters into a static char buffer before materializing the required GUID string. Replaced link-preservation source parsing with an 8192-byte stream reader, a 2048-byte static line scratch, and `ReadOnlySpan<byte>` token parsing for namespace/type detection. The only remaining strings are the cold artifacts that must exist: asmdef GUIDs and full type names written into `link.xml`/`HashSet`.

Rejected Alternatives: Keeping `File.ReadAllLines()` was rejected because it allocates an array plus one string per line. A Roslyn syntax tree was rejected because it would add heavy editor dependencies and widen the compile-wall tool itself. Fully avoiding strings was rejected because Unity asmdef references and linker XML type names are string contracts.

Scalability potential: Low-end developer machines get lower cold allocation pressure while generating/validating compile-wall artifacts. Middle/high/ultra machines get the same deterministic linker manifest without adding runtime references or reflection metadata loading.

Hardware Impact: Runtime microseconds saved: 0 us measured. Editor cold-path allocation pressure is reduced by source shape; no profiler measurement was run.

## Decision 19 - Bounded Asmdef Json Parser

Problem: `TryReadNode()` still used `File.ReadAllText()` plus `JsonUtility.FromJson` to hydrate each `.asmdef`. That path materialized a whole-file JSON string, depended on Unity serialization for three simple routing fields, and kept cold DTO classes alive only to parse `name`, `references`, and `includePlatforms`.

Solution: Replaced the `.asmdef` read path with a bounded 64KB `FileStream` scratch buffer and byte-span token parser. The parser extracts only the fields required by the compile-wall graph, caps string-array fields at 128 entries, skips escaped bytes inside JSON strings, and returns `Array.Empty<string>()` for absent arrays. Removed the obsolete JsonUtility DTO classes.

Rejected Alternatives: Keeping `JsonUtility` was rejected because this Editor guard should not need Unity serializer hydration to inspect assembly routing. A general JSON parser was rejected because it adds more dependency and allocation surface than the graph needs. Reflection or assembly metadata loading was rejected because the task is to prevent compile-wall pressure, not introduce another heavy scanner.

Scalability potential: Low-end developer machines get lower cold allocation pressure during X-Ray scans and pre-build validation. Middle/high/ultra workstations get the same deterministic routing graph while keeping the guard independent of sibling runtime domains.

Hardware Impact: Runtime microseconds saved: 0 us measured. Editor cold-path allocation pressure is reduced by source shape only; no profiler measurement and no compile/build probe were run.

## Decision 20 - Root-Level Asmdef Property Guard

Problem: Loop 18 removed `JsonUtility`, but the initial bounded parser still searched for `"name"`, `"references"`, and `"includePlatforms"` globally. That is too loose for a compile-wall authority tool: nested `versionDefines[].name` fields or a top-level string value matching a property token could poison assembly node identity if Unity changes field ordering or an asmdef is hand-edited.

Solution: Replaced global ASCII token lookup with a root-object JSON property scanner. The scanner tracks object depth, array depth, string state, and escaped bytes, accepts only tokens at root object depth, and requires a colon after JSON whitespace. The existing bounded 64KB read and 128-entry array cap remain unchanged.

Rejected Alternatives: Trusting current Unity asmdef field ordering was rejected because the guard must survive hand-edited files and future Unity serialization changes. Reintroducing `JsonUtility` or a general JSON parser was rejected because the X-Ray tool needs three routing fields, not serializer hydration or reflection surface.

Scalability potential: Low-end developer machines get deterministic graph identity without serializer dependencies. Middle/high/ultra workstations get the same stricter guard while preserving Contracts/Runtime/Authoring isolation and no sibling Runtime coupling.

Hardware Impact: Runtime microseconds saved: 0 us measured. This is Editor cold-path correctness hardening. Static scan found 119 asmdefs and 9 nested `versionDefines[].name` sites that are now explicitly ignored by the routing-property scanner.

## Decision 21 - Streaming Filesystem Enumeration

Problem: The owned X-Ray/pre-build tooling still used `Directory.GetFiles(..., SearchOption.AllDirectories)` in four recursive scans. That API materializes every matching path into a managed array before the scanner can reject irrelevant files. For a compile-wall guard that may traverse `Assets/_Project`, `Docs/Archive`, `StreamingAssets`, and `Library/Bee/artifacts`, full path-array materialization is unnecessary cold allocation pressure.

Solution: Replaced the asmdef graph scan, archaeology scan, Bee `.ref.dll` index, and runtime Pack=1 source scan with `Directory.EnumerateFiles(...).GetEnumerator()` plus explicit `while (MoveNext())` loops. Dictionary capacities now use fixed conservative capacities instead of `files.Length`.

Rejected Alternatives: Keeping `Directory.GetFiles` was rejected because it front-loads path arrays for directories that may grow with archives and Bee artifacts. Using LINQ or `foreach` over the enumerable was rejected because this guard already models explicit allocation-aware loops.

Scalability potential: Low-end developer machines avoid full recursive path arrays when opening X-Ray or running the pre-build gate. Middle/high/ultra workstations get the same graph and ABI debt output while preserving dependency isolation and no runtime assembly coupling.

Hardware Impact: Runtime microseconds saved: 0 us measured. This is Editor cold-path allocation-pressure reduction by source shape; no profiler measurement and no compile/build probe were run.

## Decision 22 - Path Token Classifier Allocation Closure

Problem: After streaming filesystem enumeration, the asmdef layer classifier still normalized every asset path with `assetPath.Replace('\\', '/')`. That allocates a replacement string for every asmdef even though the classifier only needs to detect four directory tokens. The archaeology scanner also converted found paths through `ToProjectPath(GetProjectRoot(), path)`, recalculating the project root after `RunArchaeology` had already computed it.

Solution: Added constant forward-slash and backslash path tokens for `Editor`, `Tests`, `Contracts`, and `Authoring`, then replaced the normalization pass with direct `IndexOf` checks through `ContainsPathToken`. Passed the cached `projectRoot` into `ScanArchaeologyFolder` and used it for project-path conversion.

Rejected Alternatives: Keeping the normalization allocation was rejected because this tool is the compile-wall hygiene reference and should not model avoidable path churn. Adding a generalized path segment parser was rejected because constant token checks are enough for Unity asmdef asset paths. Launching a compile probe was rejected because this is a narrow Editor cold-path change and the user explicitly forbade builds unless needed.

Scalability potential: Low-end developer machines avoid per-asmdef path normalization allocations when opening X-Ray or running the build gate. Middle/high/ultra machines get identical graph classification without adding runtime dependencies or widening the contract surface.

Hardware Impact: Runtime microseconds saved: 0 us measured. Editor allocation reduction is source-shape verified: `normalizedPath` and `ToProjectPath(GetProjectRoot` no longer exist in `CompileWallXRayWindow.cs`.

# Rationale 1610 - FAUNA_SKINNING_AND_IK_SKELETON_FORGER

Status: POST-ARCHIVE CONTINUATION; APEX VERIFIER HARDENED FOR HOT STRING/LINQ/DELEGATE/FOREACH/STACKALLOC/MANAGED-NATIVE ALLOCATION, QUALIFIED REGISTRY, UNITY RUNTIME TRAP, AND JOB-FENCE STALL DETECTION; UNITY VALIDATION BLOCKED BY HOST CONTENTION

## Mandate Selection

Problem: Offline fauna rigging proof currently depends on source-level APEX verification because host CPU and active compiler processes block builds.
Solution: Continue with `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, and `DATA_Runtime_Struct_Layout_ARM64` as active constraints.
Rejected Alternatives: `dotnet build` under CPU >50 percent or while `dotnet` is active.
Scalability potential: Low devices avoid hot allocator drift; middle/high/ultra retain richer fauna presentation only through verified zero-GC transfer routes.
Hardware Impact: Runtime impact is 0 us from editor-only verifier work.

## Decision 001 - Live State Recreated After Archive Handoff

Problem: Active 1610 status/rationale/log files were moved out of `Docs/Tasks` and `Docs/AgentLogs` during the run, while code changes were still being applied.
Solution: Create concise live continuation files and leave archived Batch015 files untouched.
Rejected Alternatives: Patch archived logs after handoff, or continue without the mandatory live state files.
Scalability potential: No runtime effect; protects agent state consistency during parallel batch work.
Hardware Impact: 0 us runtime.

## Decision 002 - Hot Deferred Allocation Source Detection

Problem: The verifier allowed hot-reachable LINQ/deferred query syntax, lambdas, anonymous delegates, anonymous objects, `yield`, and `await` unless another known allocation token was present.
Solution: Extend `FaunaApexIntegratorVerifier1610` to classify those constructs as hot-reachable allocation/control-flow violations. Limit static string factory detection to `string`, `String`, and `System.String`.
Rejected Alternatives: Broad grep-only rejection and broad `.Create` suffix rejection. Grep is not transitive; broad `.Create` is false precision without semantic context.
Scalability potential: Low devices avoid closure/enumerator/state-machine GC drift; middle/high/ultra keep hot transfer surfaces explicit and allocation-free.
Hardware Impact: 0 us runtime; prevents profiler/build time waste on avoidable source drift.

## Decision 003 - Hot Foreach Source Detection

Problem: The verifier could not prove whether hot-reachable `foreach` iterates a safe array/list or a boxed/deferred enumerable.
Solution: Add a fail-closed AST guard for `foreach` and deconstruction `foreach` syntax. Direct hot scan found no current `foreach` hits before enabling the rule.
Rejected Alternatives: Regex type inference or waiting for semantic compilation. Both are unsuitable under current build throttle.
Scalability potential: Low devices avoid boxed enumerator spikes; higher tiers keep fauna hot loops index-based and cache-friendly.
Hardware Impact: 0 us runtime.

## Decision 004 - Fully-Qualified Allocation Type Detection

Problem: The verifier matched managed allocation constructors with short prefixes like `List<`, which misses namespace-qualified forms such as `System.Collections.Generic.List<T>` and `global::System.Text.StringBuilder`. It also did not classify hot native container constructors as allocation hazards.
Solution: Normalize allocation type names by trimming `global::`, removing generic arguments, and taking the final namespace segment before comparing against managed containers, delegates, coroutine/async helpers, exceptions, regex, and native container types.
Rejected Alternatives: Leave short-name matching or reject all object creation. Short-name matching is bypassable; rejecting all object creation would incorrectly reject struct construction such as `Vector3`.
Scalability potential: Low devices avoid hidden managed/native allocation drift; middle/high/ultra keep richer fauna logic only when hot methods use explicit preallocated storage.
Hardware Impact: 0 us runtime from verifier; prevents build/profiler time waste on source patterns already known to violate the memory model.

## Decision 005 - Qualified GlobalRegistry Source Detection

Problem: The dependency lookup verifier matched literal `GlobalRegistry.*` expressions only, so namespace-qualified or `global::` forms could bypass hot dependency lookup detection.
Solution: Normalize `GlobalRegistry` expressions by stripping `global::` and reducing namespace-qualified expressions to the `GlobalRegistry` member chain before classifying reads versus lifecycle registration routes.
Rejected Alternatives: Treat every expression ending in `GlobalRegistry` as a violation. Lifecycle registration must remain legal in cold paths, and false positives hide real hot lookup defects.
Scalability potential: Low devices keep dependencies cached cold; higher tiers cannot add richer fauna presentation by reintroducing hot registry polling through qualified syntax.
Hardware Impact: 0 us runtime from verifier; prevents future hot dependency lookup drift.

## Decision 006 - Hot Stackalloc Bounds Detection

Problem: Hot `stackalloc` is not managed heap, but project law caps gameplay frame-path stack scratch at 256 bytes and forbids unknown-size scratch surfaces. The verifier did not enforce this.
Solution: Add a stackalloc guard that estimates byte size for primitive/math/vector element types, rejects unknown type or size, rejects target-typed implicit stackalloc, and rejects constant sizes above 256 bytes.
Rejected Alternatives: Allow all stackalloc because it is stack memory, or reject all stackalloc. The first ignores stack pressure on weak devices; the second rejects valid tiny scratch spans.
Scalability potential: Low devices avoid stack pressure and hidden frame-path scratch growth; middle/high/ultra keep small deterministic stack scratch while larger work uses preallocated buffers.
Hardware Impact: 0 us runtime from verifier; prevents future hot stack scratch abuse before profiler time is spent.

## Decision 007 - Hot Unity Runtime Trap And Fence Detection

Problem: The verifier blocked hot `GlobalRegistry` and component lookup drift, but it did not block other Unity frame-path traps: `GameObject.Find`, `FindObjectOfType`, `Resources.Load`, coroutine scheduling, `Camera.main`, material-instance properties, Mesh array-copy properties, and blocking job completion calls.
Solution: Add transitive source gates for Unity runtime traps and synchronization stalls. The guard is editor-only and scans hot-reachable call chains from `Tick`, `FixedTick`, `LateFrameTick`, `VisualSync`, `VISUAL_SYNC`, `Update`, `FixedUpdate`, `LateUpdate`, and `Execute`.
Rejected Alternatives: Runtime profiler-only enforcement, broad grep-only enforcement, or banning all methods named `Complete*`. Profiler-only catches defects late; grep has no call-chain proof; broad `Complete*` would false-positive domain methods such as `CompleteCorpseSinkingKinematicsIfReady`.
Scalability potential: Low devices avoid scene scans, synchronous loads, coroutine heap state, material instantiation, Mesh array copies, and worker-thread serialization. Middle/high/ultra can spend saved time on fauna presentation only inside verified visual sync paths.
Hardware Impact: 0 us runtime from verifier; expected savings are avoidance-based. A single avoided scene search or job completion stall can exceed the 0.1 ms suspicion threshold on i3/MX350.

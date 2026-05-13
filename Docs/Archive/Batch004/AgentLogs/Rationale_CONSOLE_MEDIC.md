# Rationale_CONSOLE_MEDIC

## Decision 001

Problem: User requested Unity Console diagnosis without a matching `<AGENT_PROMPT id="...">` in the active batch.
Solution: Use isolated `CONSOLE_MEDIC` identity and integration/console domain; do not steal neighboring batch prompts.
Rejected Alternatives: Using `PIPE_LOGISTICS_ARCHITECT` because an IDE tab referenced a missing status file; using any active batch prompt ID without explicit user assignment.
Scalability potential: Low/Middle/High/Ultra unaffected until real code changes exist.
Hardware Impact: 0 us runtime; process-only guard.

## Decision 002

Problem: Console fixes can become fake reports if based on static search or partial console reads.
Solution: Apply `QA_Evidence_Text_Filter_Audit` and treat Unity Console data as the primary evidence class for this task.
Rejected Alternatives: `rg`-only diagnosis; dotnet-only proof; speculative success language.
Scalability potential: Low/Middle/High/Ultra unaffected until real code changes exist.
Hardware Impact: 0 us runtime; prevents wrong edits.

## Decision 003

Problem: Unity MCP stopped exposing an active editor instance while Unity.exe stayed alive.
Solution: Fall back to the live Unity launch log at `Logs/CodexUnityLaunch_20260512_hud_surface.log` and only edit defects that matched current source.
Rejected Alternatives: Blocking indefinitely on MCP; inventing success from stale Console pages; launching a second editor over the same project.
Scalability potential: Low/Middle/High/Ultra unaffected directly; keeps console triage evidence-based under concurrent agent churn.
Hardware Impact: 0 us runtime; prevents duplicate editor contention on low-end disks/CPUs.

## Decision 004

Problem: `AcousticPortalPropagation` passed fields from `NativeArray<AcousticPortalNode>`-derived structs into `in` parameters, producing CS1612 in Unity compile output.
Solution: Copy `AcousticAup` positions into stack locals before `DistanceMeters(in ..., in ...)`.
Rejected Alternatives: Removing `in` from AUP math; changing NativeArray storage; converting path solve to managed lists.
Scalability potential: Low uses the same cheap stack copy; Middle/High/Ultra keep deterministic AUP distance math without GC.
Hardware Impact: Estimated 0 us frame cost, likely compile-only fix; avoids managed fallback.

## Decision 005

Problem: `IFoveatedSimulationTarget` expanded, while `FaunaBrain.Foveated` lagged and no longer satisfied the contract.
Solution: Implement stable entity hash/id, tier callback, and frozen-wrap handling using existing foveated/director/voxel contracts.
Rejected Alternatives: Removing the interface from fauna; direct manager lookups from unrelated domains; blind transform teleport.
Scalability potential: Low/Middle can cold-gate far predators; High/Ultra keep combat Tier0 lock and visual overkill room.
Hardware Impact: Estimated low-end gain is preserving far-frozen predator cold paths; no new per-frame managed allocation.

## Decision 006

Problem: Hologram assembly shader used `line` as an HLSL local token and `Hecton_CoreLit.hlsl` contained ShaderLab pragmas inside an include, generating shader errors/warnings.
Solution: Rename the local to `gridLine` and remove invalid shared include pragmas; pass-local shaders already own variant pragmas.
Rejected Alternatives: Disabling the shader; moving all variants globally; suppressing warnings without fixing source.
Scalability potential: Low keeps the cheap grid fake; High/Ultra retain hologram visual density without shader compile failure.
Hardware Impact: Estimated 0 us runtime; fixes shader import path and preserves existing visual cheat.

## Decision 007

Problem: Final verification cannot be completed from the active tools: MCP reports zero Unity instances and the live log has not refreshed after final edits.
Solution: Mark verification blocked, preserve exact evidence, and do not claim a green Console.
Rejected Alternatives: Using `dotnet build` as Unity proof; normalizing all mixed-line-ending shaders; editing unrelated asmdefs.
Scalability potential: Low/Middle/High/Ultra unchanged until Unity recompiles.
Hardware Impact: 0 us runtime; avoids high-churn line-ending rewrites and invalid verification.

## Decision 008

Problem: Second-pass audit found the same acoustic `in node.Position` risk in `FindNearestNode`, an unguarded foveated distance cache, and a fragment-stage fabricator-space matrix multiply in the hologram shader.
Solution: Copy acoustic node position once per loop iteration, clamp non-finite foveated distance to zero, and interpolate fabricator-local Y from the vertex stage for clip/edge calculations.
Rejected Alternatives: Leave adjacent risky code because the stale log named only earlier lines; add runtime logging; rewrite hologram assembly into geometry or simulation.
Scalability potential: Low keeps stack-only acoustic work and cheaper shader ALU; Middle keeps deterministic foveated state; High/Ultra spend saved fragment ALU on denser hologram visuals instead of duplicate coordinate transforms.
Hardware Impact: 0 us measured; low-end MX350/i3 path avoids one float4x4 multiply per affected hologram fragment and prevents NaN-driven foveated wrap churn without managed allocations.

## Decision 009

Problem: Fresh Unity log tail mixed stale compile errors with later shader imports, while MCP Console remained unavailable.
Solution: Treat generated Unity `.rsp` files as the current source-of-truth compile probe and re-run affected assemblies directly through Unity's bundled Roslyn compiler.
Rejected Alternatives: Editing already-fixed `DroneFleetManager` and `PlayerInventory` fields from stale errors; claiming Editor Console success without a live MCP session.
Scalability potential: Low/Middle/High/Ultra unaffected directly; evidence discipline prevents destabilizing unrelated domains during concurrent agent work.
Hardware Impact: 0 us runtime; process-only guard.

## Decision 010

Problem: `HullDentShaderController` in `Hecton8.Vehicles.VFX` referenced `SystemDispatcher.CurrentFrameUnscaledDeltaTime`, an `internal` Core property invisible from the VFX asmdef.
Solution: Resolve unscaled delta through the public `GlobalRegistry.TickDispatcher.TimeSnapshot` contract, with a finite `Time.unscaledDeltaTime` fallback and a 1 second clamp for hitch-safe repair fade.
Rejected Alternatives: Making `SystemDispatcher` internals public; adding asmdef friend access; reading raw Unity time as the primary path.
Scalability potential: Low keeps dent repair shader-only and cheap; Middle/High/Ultra retain visual hull damage without simulation mesh mutation.
Hardware Impact: Estimated 0 us frame cost; no allocation, one service pointer read and scalar sanitize on late-frame VFX tick.

## Decision 011

Problem: `PlayerInventory` contained titanium repair signal drain state and method, but `SlowTick()` did not call it, leaving the repair-tool titanium coupling dead.
Solution: Invoke `DrainRepairToolTitaniumSignals()` after salinity biome signal drain and before durability degradation/corrosion passes.
Rejected Alternatives: Removing the method and fields; scanning inventory every frame; adding managed event subscriptions.
Scalability potential: Low uses existing slow tick and `ReadOnlySpan` signal snapshot; Middle/High/Ultra keep deterministic repair feedback while preserving the native inventory SOA.
Hardware Impact: Estimated less than 1 us on slow tick when no signals; zero managed allocation.

## Decision 012

Problem: Burst previously failed resolving `Hecton8.Vehicles.VFX` because `Library/ScriptAssemblies` did not contain the assembly after a failed compile.
Solution: Fix the VFX compile blocker and verify all 36 non-editor/non-test runtime `Hecton8*.rsp` files exit clean through Unity's generated compiler response files.
Rejected Alternatives: Manually copying Bee artifact DLLs into `Library/ScriptAssemblies`; starting another Unity editor over the active project; treating line-ending warnings as C# compile blockers.
Scalability potential: Low/Middle/High/Ultra all benefit from restored runtime assembly graph; final ScriptAssemblies copy remains Unity Editor's import responsibility.
Hardware Impact: 0 us runtime; prevents Burst entry-point scan failure caused by missing runtime assembly output.

## Decision 013

Problem: `Hecton8.EditModeTests.rsp` failed because `using NativeArray<T>` and `using NativeList<T>` declarations made the local struct receivers readonly, blocking indexer writes in `AcousticPortalPropagationTests`.
Solution: Replace using declarations with ordinary local native containers and explicit `try/finally` disposal in reverse ownership order.
Rejected Alternatives: Moving test data into managed arrays; suppressing the test; changing production acoustic structs to hide the C# readonly receiver rule.
Scalability potential: Runtime unaffected; tests continue validating the cheap portal-corner acoustic route and sealed-bulkhead fake.
Hardware Impact: 0 us runtime; editor test allocation pattern remains `Allocator.TempJob` with deterministic disposal.

## Decision 014

Problem: `Hecton8.Editor.rsp` references `Assets/_Project/Scripts/Editor/ScreenSpaceLightShaftPrefabRepair.cs`, but that tracked file and its meta are currently deleted by a concurrent change.
Solution: Do not restore the deleted file; verify a temporary filtered copy of `Hecton8.Editor.rsp` to distinguish stale Bee response data from real editor compile errors.
Rejected Alternatives: Reverting another agent's deletion; editing `Library/Bee` artifacts as a source fix; reporting the filtered pass as full Unity Editor import proof.
Scalability potential: Low/Middle/High/Ultra unaffected directly; avoids accidental resurrection of editor-only prefab repair code.
Hardware Impact: 0 us runtime; editor verification only.

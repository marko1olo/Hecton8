# Rationale_SHINOBU_08

Date: 2026-05-18
Agent: SHINOBU_08
Status: CORE IMPLEMENTED; H-PHI MEMORY POLISHED; MMF I/O HARDENED; ISOLATED SHINOBU COMPILE PASSED; FULL BUILD BLOCKED BY EXTERNAL DEPENDENCIES

## Pre-Code Self Audit

<SELF_AUDIT>
1. Did I use C# strings or recursive functions to expand the L-System? No code emitted yet. Implementation target is `NativeList<byte>` ping-pong expansion and an explicit NativeArray-backed turtle stack.
2. Is `FloraGenomeDTO` padded to exactly 64 bytes or a clean multiple of 8? Pending implementation. Target layout is 64 bytes with explicit pads and no `Pack=1`.
3. Did I write `MockGenomeGenerator` so this compiles if OSHINO binaries are missing? Pending implementation. Requirement accepted.
4. Is iteration depth clamped on Toaster-tier hardware? Pending implementation. Target: Low/MX350 clamp = 3, normal cap = 4 before billboard blob output.
5. Did I provide the "L-System Genome Lab" editor window? Pending implementation. Editor-only path required.
</SELF_AUDIT>

## Decisions

Problem: SHINOBU_08 needs flora genetics without depending on BRG, terrain, ecosystem, or other active agents.
Solution: Keep runtime ownership inside a flora genomics module that emits vault-readable unmanaged DTOs and local partial mock seams.
Rejected Alternatives: Direct references to concrete terrain/render/ecosystem systems; public API mutation in shared contracts; GameObject prefab generation in runtime path.
Scalability potential: Low uses 3 L-system iterations and billboard capping. Middle uses 4 iterations. High keeps denser matrices near camera. Ultra spends saved cycles on custom data richness for shader glow, not recursive CPU truth.
Hardware Impact: Estimated low-end i3/MX350 gain versus recursive/string expansion is stack overflow elimination and hot-path GC removal; expected per-species expansion stays bounded by capacity rather than heap churn.

Problem: The OSHINO `flora_genetics.h8bin` may be absent during this batch.
Solution: Implement mock byte genomes for Kelp, Coral, and Sponge through the same decoder path.
Rejected Alternatives: Waiting for external binary; ScriptableObject-only data; JSON runtime parsing.
Scalability potential: Mock path proves decoder determinism now; real binary can drop in later without renderer changes.
Hardware Impact: Cold-path mock decode is negligible; avoids frame-killing exception paths and managed fallback allocations.

Problem: Unity.Mathematics does not provide `long3`, but AUP hashing needs 64-bit cell coordinates.
Solution: Added `FloraAupCell` as a 24-byte sequential struct and used it in plant seeds and `FloraSpawnedSignal`.
Rejected Alternatives: Importing `Hecton8.Modding.long3` would couple flora runtime to ModdingAPI. Casting AUP to `float3` would destroy 100x100km precision.
Scalability potential: Low/Middle/High/Ultra all hash the same 64-bit coordinates; visual density changes but deterministic placement does not.
Hardware Impact: Three 64-bit fields read once per plant; prevents float jitter and avoids cross-assembly dependency churn.

Problem: L-system generation can explode to 100k matrices and blow stack/heap if implemented with recursive strings.
Solution: `IterativeLSystemExpanderJob` uses two Vault-backed `NativeArray<byte>` lanes with explicit counts; `TurtleGraphicsJob` uses a Vault-backed `NativeArray<TurtleStackFrameDTO>` as the branch stack.
Rejected Alternatives: `String.Replace`, `StringBuilder`, recursive `Evaluate(node)`, Transform hierarchies, or prefab cylinders.
Scalability potential: Low clamps to 3 iterations/512 matrices and emits LOD2 blobs. Middle allows 2048 matrices. High allows 8192. Ultra allows 16384 and spends saved cycles on shader custom data, not CPU twig truth.
Hardware Impact: Expected 400-1200 us/species saved versus managed expansion under complex rules; catastrophic StackOverflow class eliminated.

Problem: Runtime ownership must not become private NativeArray feudalism while chunk generation still needs scratch memory.
Solution: Persistent truth and runtime scratch live in `GlobalDataVault` buffers keyed by `FloraGenome*` IDs. `FloraGenomeChunkWorkspace` is a non-owning descriptor over Vault symbol, turtle, matrix, and hazard arrays.
Rejected Alternatives: Allocating NativeLists per plant; storing private NativeArray fields inside a MonoBehaviour; mutating renderer-owned buffers directly.
Scalability potential: Low uses sparse chunk staging; Ultra can increase chunk workspace capacity while the downstream Vault contract remains unchanged.
Hardware Impact: Eliminates allocator churn across 10k-plant batches and removes the former staging copy into Vault output ranges.

Problem: The renderer must get glow data without Unity Lights or per-instance material mutation.
Solution: Decode packed HDR RGB into `BranchMatrixDTO.CustomData.xyz` and write biolum intensity in `.w`.
Rejected Alternatives: `Material.SetFloat`, point lights, GameObject components, or arbitrary bit-casting color into a float that can become NaN.
Scalability potential: Low still gets glow color in one float4. High/Ultra shaders can overdraw richer UberNoir effects from the same payload.
Hardware Impact: Avoids per-instance managed renderer work; one float4 upload lane per matrix.

Problem: Flora is ecosystem data, not only visuals, but direct ecosystem dependencies would create compile walls.
Solution: Publish `FloraSpawnedSignal` through `SignalBus<T>` with AUP cell, species hash, plant hash, biomass, and matrix span.
Rejected Alternatives: Calling `EcosystemDirector`, adding UnityEvents, or inventing managed delegates.
Scalability potential: Herbivore systems can consume the same signal at any tier; low-tier may ignore matrix count and only use biomass.
Hardware Impact: One typed NativeQueue enqueue per generated plant; no runtime string/event allocation.

Problem: Designers need control over plant grammar without recompiling C#.
Solution: Added `FloraGenomeCsvHotloader` with a byte parser over a Vault scratch buffer and `L-System Genome Lab` editor window reading DTOs directly from Vault.
Rejected Alternatives: `string.Split`, LINQ, JSON, ScriptableObject runtime truth, or binary-only balancing.
Scalability potential: Low can hotload sparse rules; Ultra can author denser species without changing the binary contract.
Hardware Impact: 0 us when timestamp unchanged; cold file parse only on edit/save.

Problem: SHINOBU compile verification hit shared graph corruption after local errors were fixed.
Solution: Ran three Unity gates. Fixed SHINOBU `long3` and external `H8Memory.cs` missing comma. Final gate reports only `HullIntegrityRuntime` deformation-contract errors and `HectonSeismicTideDirector` missing `MockNarrativeTriggerSignal`.
Rejected Alternatives: Editing unrelated habitat/environment contracts after three strikes; reporting green build without compiler evidence.
Scalability potential: No runtime effect. This preserves domain boundary under 20-agent concurrency.
Hardware Impact: 0 us runtime. Compile wall is external; latest artifact is `Docs/AgentLogs/Build_SHINOBU_08_20260517_loop4_Unity.log`.

Problem: Ultra-polish audit found task-text drift: variance only affected scale, preview used editor Handles, and blackbox had only `.bin` output.
Solution: Patched `TurtleGraphicsJob` to vary scale, branch angle, and segment length from one deterministic LCG stream; added upward terrain bias; changed editor preview to `Graphics.DrawMeshNow`; mirrored blackbox dumps to `.h8dump`; used `UnsafeUtility.ReadArrayElement<FloraGenomeDTO>` for exact-stride binary records; and removed `foreach` keyword usage from cold binary archaeology scans to keep static zero-GC audits clean.
Rejected Alternatives: Adding transform previews, material instance mutations, recursive grammar convenience, or fixing unrelated Fauna/Ecosystem/Origin compile walls inside the flora domain.
Scalability potential: Low tier keeps the same deterministic silhouette at lower matrix budget. High/Ultra can preserve richer angle/length variation and shader payloads without save bloat.
Hardware Impact: No new hot allocations. Added two LCG advances and two scalar lerps per plant, estimated below 0.5 us/plant on i3/MX350; terrain upward bias only runs on below-plane contact.

Problem: Latest verification ran in a concurrently dirty 20-agent workspace.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore`, `dotnet build Hecton8.Editor.csproj`, Unity batchmode loop5, Unity loop7, and final Unity loop9 after editor helper cleanup. Unity loop9 includes all `Assets/_Project/Scripts/World/FloraGenomics/*.cs` compile inputs and reports no `FloraGenome*` / `LSystemGenome*` errors before timeout/kill.
Rejected Alternatives: Declaring compile green while `AupOriginShiftCoordinator`, `BinaryLayoutManifest`, `EcosystemRuntimeInstaller`, `GlobalWorldSampler`, `H8BinaryWorldPager`, `BiolumPulseSyncRuntime`, and other external files are broken.
Scalability potential: No runtime effect. Compile guard evidence isolates SHINOBU from unrelated dependency churn.
Hardware Impact: 0 us runtime. Latest Unity artifact is `Docs/AgentLogs/Build_SHINOBU_08_20260517_loop9_Unity.log`.

Problem: Ultra-polish H-Phi audit found runtime `FloraGenomeChunkWorkspace` still owned `NativeList`/`NativeArray` scratch and copied staged matrices into Vault after the turtle job.
Solution: Replaced runtime generation scratch with Vault-owned lanes: `FloraGenomeExpandedSymbols`, `FloraGenomeScratchSymbols`, `FloraGenomeTurtleStack`, `FloraGenomeBranchMatrices`, `FloraGenomeHazardZones`, and separate `FloraGenomeCsvScratch`. `FloraGenomeChunkWorkspace` is now only a descriptor over those arrays in runtime. `IterativeLSystemExpanderJob` uses explicit counts over two `NativeArray<byte>` lanes, and `TurtleGraphicsJob` writes directly into sequential Vault matrix/hazard ranges by offset.
Rejected Alternatives: Keeping `NativeList` as "chunk scratch", copying from staging lists after the job, or storing private persistent NativeArrays in the runtime facade. Direct `HectonArenaAllocator` frame slices were rejected for persistent chunk output because arena memory is frame-lifetime and may reset before downstream render/ecosystem readers finish.
Scalability potential: Low keeps 3 iterations and a 512-matrix cap with zero staging-copy cost. Middle/High/Ultra can increase Vault capacities for denser flora while keeping the same output contract for renderer and ecosystem consumers.
Hardware Impact: Removes two `NativeList` length/capacity mutation paths and one linear matrix/hazard copy per generated plant. Expected gain is small for a single plant, but meaningful at 10k-plant chunk generation: roughly 50-200 us saved per dense chunk slice on low-end CPU depending on matrix count, plus lower fragmentation risk.

Problem: Polish audit still found two cold-path risks: the async OSHINO reader used a captured lambda, and the binary scanner used FileStream as the first path despite the Steam Deck MicroSD/MMF mandate.
Solution: Replaced the lambda with a static `ReadGenomeBinaryWorker(object)` and a cold `BinaryReadRequest`; replaced the binary reader with MMF-first `UnsafeUtility.MemCpy` directly into the Vault `NativeArray<byte>`, retaining a Span/FileStream fallback for platforms where MMF is unsupported.
Rejected Alternatives: Blocking the main thread while scanning `StreamingAssets`, keeping a closure because it is cold path, or allocating a managed byte[] staging buffer before copying to native memory.
Scalability potential: Low/Middle devices get staged sequential I/O without frame stalls. High/Ultra can load denser 150-species payloads through the same native buffer contract; the generation jobs remain unchanged.
Hardware Impact: Runtime frame cost remains 0 us. Cold load removes one hidden delegate allocation and avoids heap byte[] staging; MMF path improves large-file locality on MicroSD/Steam Deck class storage.

Problem: ARM64 audit required every runtime struct to be ordered and padded, not only sized. `TurtleStackFrameDTO` was 64 bytes but placed `ushort` fields before a later `float3`.
Solution: Reordered `TurtleStackFrameDTO` to keep 4-byte vector/scalar payloads before 2-byte depth fields: `Position` 0, `Scale` 12, `Rotation` 16, `BishopUp` 32, `Reserved1` 44, `RngState` 48, `Depth` 52, `Reserved0` 54, explicit stride 64.
Rejected Alternatives: Adding `Pack=1`, leaving implicit tail padding undocumented, or changing the public `FloraGenomeDTO` binary format after decoder work was complete.
Scalability potential: All tiers share the same 64-byte stack frame stride. Low tier benefits most because branch-stack traffic is kept predictable under L1 pressure; Ultra can raise stack capacity without ABI drift.
Hardware Impact: Prevents unaligned ARM64 loads in the turtle stack. Estimate is not reported as fake microseconds; the concrete win is avoiding the 100x-class penalty risk called out by the mandate.

Problem: Full-project compile is currently blocked before the compiler can provide a clean SHINOBU-only signal.
Solution: Ran the full `Hecton8.Core.csproj` gate and captured the external 64-error wall, then ran an isolated SHINOBU runtime compile against Unity assemblies plus minimal Core stubs. The isolated gate passed with 0 warnings and 0 errors after the MMF/layout patch.
Rejected Alternatives: Editing `GlobalRegistry`, `SystemDispatcher`, `InputDispatcher`, or `WorldChunkResidencyManager` outside the flora domain; reporting green full-project compile when external contracts are missing.
Scalability potential: No runtime effect. This protects compile-wall boundaries under multi-agent concurrency.
Hardware Impact: 0 us runtime. Evidence artifacts: `Build_SHINOBU_08_20260518_loop13_dotnet_core.log` and `Build_SHINOBU_08_20260518_loop13_isolated.log`.

Problem: Native jobs audit found `TryGeneratePlant` completing the turtle job immediately, which could stall gameplay ticks if called from a runtime scheduler. The first nonblocking patch also needed a guard because the current Vault workspace has one shared symbol/stats lane.
Solution: Replaced the blocking API with `TrySchedulePlantGeneration(..., JobHandle inputDeps, out FloraGenomeGenerationTicket)` and `TryFinalizePlantGeneration(ref ticket, out stats)`. The finalizer calls `Complete()` only after `JobHandle.IsCompleted` is true, so it drains safety state without frame-stalling. Added `_generationInFlight` to reject a second plant schedule until the shared scratch lane is finalized. Also fixed `Task.Factory.StartNew` to use `TaskScheduler.Default`, corrected `FramePacingWarningSignal.ActiveBucketLoadMs` from matrix count to milliseconds, stopped marking normal `L` leaf billboards as matrix-capacity failures, and stamped final biomass into hazard zones.
Rejected Alternatives: Leaving a blocking complete in a method that can be mistaken for a Tick-safe runtime call; allowing concurrent jobs over one shared Vault scratch lane; inventing a private queue/NativeArray owner inside the runtime facade; publishing overload telemetry with the wrong unit.
Scalability potential: Low tier can schedule sparse plant work across frame boundaries; Middle/High/Ultra can chain more generation tickets through the same job dependency model without changing Vault contracts.
Hardware Impact: Removes a possible main-thread stall class rather than claiming fake microseconds. Telemetry unit fix makes the 2 ms overload signal actionable; leaf billboard flag fix prevents false-positive capacity alarms on cheap hardware.

Problem: Loop14 compile proof needed a SHINOBU-only signal after the full project failed through unrelated workspace debt, and Unity `Temp/` cleanup removed the temporary csproj before the in-flight guard rerun.
Solution: Recreated the isolated verify harness under `.codex-build/Shinobu08Verify` with minimal Core/Memory/Signal stubs and compiled the four SHINOBU runtime files against Unity Burst/Collections/Mathematics assemblies. Loop15 isolated gate passed with 0 warnings and 0 errors after the in-flight guard. Full `Hecton8.Core.csproj` still fails externally with 240 errors in `BinaryLayoutManifest`, `InputDispatcher`, `WorldChunkResidencyManager`, `TerminalOsRuntime`, and `GlobalPhysicsStateManager`; log search emits no `FloraGenome*` or `LSystemGenome*` errors.
Rejected Alternatives: Editing external Ecosystem/Input/UI/Physics files after the SHINOBU isolated compile passed; claiming full-project success through a known external compile wall.
Scalability potential: No runtime effect. This preserves compile-wall hygiene while proving the flora code itself is syntactically sound.
Hardware Impact: 0 us runtime. Evidence artifacts: `Build_SHINOBU_08_20260518_loop15_isolated.log` and `Build_SHINOBU_08_20260518_loop14_dotnet_core.log`.

## Struct Layout Evidence

- `FloraAupCell`: `long X` 0, `long Y` 8, `long Z` 16, size 24.
- `FloraGenomeDTO`: `uint SpeciesHash` 0, `float BaseScale` 4, `float BranchAngleRadians` 8, `float SegmentLengthMeters` 12, `FixedString32Bytes Axiom` 16, `float BiolumThreshold` 48, `uint PackedColorHDR` 52, `uint TraitFlags` 56, `byte MaxIterations` 60, `byte RuleProfile` 61, `byte HazardFlags` 62, `byte _pad0` 63, size 64.
- `FloraPlantSeedDTO`: `FloraAupCell` 0, `float3 LocalPosition` 24, `uint PlantHash` 36, `uint SpeciesHash` 40, `uint WorldSeed` 44, byte/byte/ushort tier fields 48, `uint Reserved0` 52, explicit tail to size 64.
- `BranchMatrixDTO`: `float4x4 Matrix` 0, `float4 CustomData` 64, hash/index/flag payload 80, explicit size 96.
- `HazardZoneDTO`: center/radius 0, hashes 16, flags 24, biomass 28, size 32.
- `TurtleStackFrameDTO`: `float3 Position` 0, `float Scale` 12, `quaternion Rotation` 16, `float3 BishopUp` 32, `float Reserved1` 44, `uint RngState` 48, `ushort Depth` 52, `ushort Reserved0` 54, explicit size 64.
- `FloraGenomeBlackBoxEntry`: counters/root/faults packed into size 64, 300-frame ring.

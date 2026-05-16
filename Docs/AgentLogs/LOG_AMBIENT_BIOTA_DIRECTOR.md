# LOG_AMBIENT_BIOTA_DIRECTOR

## 2026-05-16 - Prompt Extraction Blocker

What was wrong: `AMBIENT_BIOTA_DIRECTOR` is listed in the companion launcher instruction file, but the active `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="AMBIENT_BIOTA_DIRECTOR">`. The batch audit confirms this ID is missing and states missing prompts must not be invented or synthesized.

What was done: Read `AGENTS.md`, `Docs/Actual Domains of Project.txt`, extracted/search-validated the batch with PowerShell CLI, read eight relevant `.agents-skills` mandates, created `Docs/Tasks/Status_AMBIENT_BIOTA_DIRECTOR.md`, created `Docs/AgentLogs/Rationale_AMBIENT_BIOTA_DIRECTOR.md`, and recorded this log.

Cinematic Cheats used: None implemented. Future authorized ambient biota work should prefer deterministic spawn fakes, pooled presentation objects, GPU/instanced visual density, and Math LOD instead of simulating every background organism as gameplay truth.

Exact Microseconds saved: 0 us runtime. No code path was changed. Blocking prevents unauthorized runtime cost from unscoped spawning/pooling logic.

Verification: No compilation attempted because no source code was modified and no authorized task list exists.

## 2026-05-16 - Phase 1 Great Purge / Loop 1

What was wrong: `AMBIENT_BIOTA_DIRECTOR` prompt was injected after the initial blocker. Phase 1 required removing ambient biota object-spawn patterns and replacing them with a GPU-ready stream. Existing runtime scans found no direct `AmbientLifeManager.Instance` and no direct ambient/fish `Object.Instantiate`, but there was also no authoritative `AI/Ambient` SOA service for consumers to bind.

What was done: Added `SystemID.AmbientBiota`; added `BufferID.BiotaAUPs`, `BufferID.BiotaVelocities`, and `BufferID.BiotaStates`; added `AmbientBiotaState` and `IAmbientBiotaService`; registered `GlobalRegistry.AmbientBiota`; created `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` with direct `Hecton8.Core`, `Hecton8.Core.Contracts`, and `Hecton8.Core.Memory` references; implemented `AmbientBiotaDirector` with DataVault-backed AUP/velocity/state arrays, deterministic Burst spawn, modulo-bucket drift, biomass gating, abyssal-flow input, and late-frame job completion. No `Instantiate`, `Random.Range`, `foreach`, or Unity `Update` exists in the new ambient domain.

Cinematic Cheats used: Background organisms are treated as SOA visual particles with deterministic Brownian noise and flow-field advection, not physical agents. Low tier marks slots as billboard-ready and runs 1/16 drift buckets. High/Ultra can spend the same contract on larger capacity and later indirect GPU draw variation.

Exact Microseconds saved: 2000-8000 us estimated spawn-spike avoidance per 64-object burst versus `Instantiate`; 1-3 us/frame estimated singleton/scene-path avoidance after consumers bind `GlobalRegistry.AmbientBiota`; 50-150 us estimated cold realloc avoidance after DataVault residency; 300-1200 us/frame estimated avoided versus per-fish MonoBehaviour transform drift at equivalent visible density. Profiler confirmation is blocked by project-wide compile errors.

Verification: Static scans passed for forbidden patterns in `Assets/_Project/Scripts/AI/Ambient` and touched Core contracts. `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` failed outside this task on unrelated project-wide errors: missing `JobAdmissionLane` references, missing `HectonShaderGlobalDataVaultBridge`, missing `GlobalSignals` signal types, missing voxel-debris constants, and stale generated project references. Compile status recorded as `[BLOCKED BY DEPENDENCY]` in `Docs/Tasks/Status_AMBIENT_BIOTA_DIRECTOR.md`.

## 2026-05-16 - Loop 2/3 Data Sovereignty, NaN Guard, Blackbox

What was wrong: The first ambient director pass still owned private `NativeArray` fields and a local macro counter allocation. That failed the data-sovereignty requirement, left no 300-frame ambient blackbox, and did not yet cover AUP double precision, low-tier fake math, high-tier light reaction, or reactive organic debris.

What was done: Replaced director-owned arrays with `VaultBufferHandle<T>` for AUPs, velocities, states, macro hydration counters, telemetry ring, and telemetry cursor. Reworked drift/spawn/dehydrate distance gates to use `double3` AUP deltas. Added modulo bucket enforcement through `ISimulationBucketer`, low-tier triangle-noise billboard motion, high-tier headlight flee/emission panic, finite guards around dt/velocity/rsqrt/AUP math, persistent velocity motion-vector source, bounded organic `DebrisSpawnSignal` emission on expiry, and a vault-owned 300-entry blackbox that dumps `Dump_AMBIENT_BIOTA_DIRECTOR.bin` on sanitized faults.

Cinematic Cheats used: Low tier uses ring offsets, triangle noise, billboard flags, and 30 m stress-radius culling instead of collision or steering. High/Ultra spend the saved CPU on larger visible bubble, richer scale/emission, and headlight panic rather than physical fish simulation.

Exact Microseconds saved: No profiler run was possible. Engineering estimates remain: 70-90% drift CPU avoided by 1/16 modulo buckets versus full sweep; 15-40 us/frame low-tier savings by skipping high-tier reaction math; 2000-8000 us spawn-spike avoidance per 64-object burst versus `Instantiate`; under 2 us/late-frame expected for the fixed telemetry write. High-tier overkill intentionally spends an estimated 20-60 us/frame on the active bucket for visible reactivity.

Verification: Static audit in `Assets/_Project/Scripts/AI/Ambient` found no `Instantiate`, `Object.Instantiate`, `Random.Range`, `Update(`, `foreach`, `string.Format`, `EventBus`, direct `H8Memory.Allocate`, or private `NativeArray` fields. `git diff --check` passed for the ambient file. Compile gate is currently `[BLOCKED BY DEPENDENCY]`: `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt` records unrelated errors in `World/SargassumMicroFaunaBoids.cs` and `RepairTool.cs`. The ambient asmdef Bee response file exists, but its `Hecton8.Core.ref.dll` reference is stale/missing while the core project build is blocked by those foreign errors.

## 2026-05-16 - Loop 4 Indirect Draw And Biome Sync

What was wrong: The SOA stream existed, but there was no renderer bridge for `Graphics.RenderMeshIndirect`, and biome identity was not influencing species/emission. A hidden correctness bug also remained: macro-hydrated state wrote arbitrary swarm hashes into the `Reserved` flag field.

What was done: Added optional indirect rendering inside `AmbientBiotaDirector`: persistent GPU buffers for AUPs, velocities/motion vectors, states, one indirect args buffer, fallback quad mesh, and one `Graphics.RenderMeshIndirect` call. Added existing typed `BiomeChangedSignal` snapshot consumption through `ReadOnlySpan<BiomeChangedSignal>` and folded the biome hash into deterministic species/emission selection. Cleared active-state `Reserved` to flags-only so debris/fault flags cannot be spoofed by swarm hashes.

Cinematic Cheats used: Low tier can render simple vertex-animated billboard quads from velocity/state buffers. High/Ultra can let the material interpret the same buffers for pale abyssal jellyfish, colorful plankton, panic biolume, and dense organic soup without CPU matrix generation.

Exact Microseconds saved: No profiler run. Expected win is removal of per-instance matrix construction and transform submission; the remaining CPU work is three bulk buffer uploads and one indirect draw. Biome snapshot scan is slow-path and expected below 1 us when no transition signals exist.

Verification: Static ambient scan still passes with no forbidden hot-path patterns. `git diff --check` passes. Full compile remains blocked by unrelated `World/SargassumMicroFaunaBoids.cs` and `RepairTool.cs` errors in `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`.

## 2026-05-16 - Loop 5 Omega Polish And Final Validation

What was wrong: Omega polish was not complete. `AmbientBiotaDriftJob.Execute` still had branch source in the advection kernel, and final validation was still carrying an older project-wide compile wall.

What was done: Re-read the `AMBIENT_BIOTA_DIRECTOR` XML block from `Docs/Tasks/CURRENT_BATCH.md`, applied the Omega mandate to the drift kernel, and replaced early branch exits with mask-driven `math.select` decisions. Added fixed selectors for AUP/state/double3 payloads, made normalization and clamp finite guards branchless, kept `Reserved` as flags-only, and reran the static forbidden-pattern audit. Reran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false`; it succeeded with 0 warnings and 0 errors.

Cinematic Cheats used: Low tier remains a deterministic billboard soup: triangle noise, ring spawning, 30 m stress cull, no collision. High/Ultra retain light avoidance, panic emission, larger density, biome-colored species families, and indirect GPU presentation without CPU matrices.

Exact Microseconds saved: Exact measured microseconds are unavailable; no Unity Profiler/GCMonitor run occurred. Engineering estimates unchanged: `Instantiate` burst avoidance 2000-8000 us per 64-object burst, modulo bucket drift reduction 70-90% versus full sweep, telemetry under 2 us/late frame, and indirect draw replacing per-instance matrix submission. Branchless Omega pass is a determinism/compliance tradeoff, not a measured frame-time claim.

Verification: `AmbientBiotaDriftJob.Execute` has no `if (` source after the Omega pass. `Assets/_Project/Scripts/AI/Ambient` has no forbidden ambient hot-path patterns from the static scan. `git diff --check` passed with CRLF warnings only. Final compile log is `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`: build succeeded, 0 warnings, 0 errors. Unity Editor import, Play Mode, GCMonitor, Frame Debugger, RenderDoc, and player-build proof remain pending.

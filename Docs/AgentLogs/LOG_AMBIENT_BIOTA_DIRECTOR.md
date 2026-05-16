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

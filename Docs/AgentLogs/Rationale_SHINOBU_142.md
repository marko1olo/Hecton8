# SHINOBU_142 Rationale

Date: 2026-05-19
Status: PENDING VERIFICATION

## Initial Decision Record

Problem: Legacy craft/build flows may instantiate prefabs, mutate renderer materials, or run coroutine/Update progress loops during fabrication.
Solution: Replace build-time object animation with owner/Vault-native progress DTOs and shader-visible scalar data; keep gameplay truth in SIMULATION, completion in POST_SIMULATION, GPU upload in VISUAL_SYNC.
Rejected Alternatives: Standard Unity Instantiate/coroutine/material mutation was rejected because it creates managed allocation, renderer material clones, and main-thread spikes during craft bursts.
Scalability potential: Low uses scalar alpha cutoff and zero optional VFX; Middle uses bounded edge glow; High adds richer shader rim/noise; Ultra spends saved CPU on visual overkill in shader/VFX, not CPU prefab churn.
Hardware Impact: Expected gain on i3/MX350 is removal of prefab/coroutine/material-clone stalls during active fabrication; exact microseconds remain PENDING VERIFICATION until profiler evidence.

Problem: Task requires new layout-sensitive runtime DTOs.
Solution: Use explicit unmanaged 32-byte FabricationJobDTO with offset-audited double3 AUP, float progress, uint hash.
Rejected Alternatives: C# properties and sequential layout guessing were rejected because CS1612 copies and hidden padding break Burst/native snapshot confidence.
Scalability potential: Same DTO feeds all tiers; high/ultra adds presentation fields in separate GPU payloads instead of bloating simulation truth.
Hardware Impact: 32-byte linear records keep two jobs per 64-byte cache line; estimated traversal cost remains under suspicious 0.1 ms budget for 100 active jobs pending measurement.

## Loop 1 Decision Record

Problem: Fabricator assembly progress was visually driven by a per-renderer `MaterialPropertyBlock`, while craft truth lived in local C# scalar state.
Solution: Introduced `FabricationAssemblerRuntime` with Vault-backed `FabricationJobDTO`, `FabricationRuntimeDTO`, `FabricationGpuPayloadDTO`, and dispatcher phase adapters. Fabricator now starts a Vault job and reads progress from `FabricationJobDTO.Progress01`; the shader consumes `_H8FabricationAssemblyPayloads`.
Rejected Alternatives: Keeping MPB or coroutine-style C# animation was rejected because it dirties renderers and keeps CPU-owned presentation state in the craft loop.
Scalability potential: Low uploads a small payload budget and gets scalar clipping; Middle keeps edge glow; High and Ultra use the same saved CPU budget for shader rim/wire/fresnel overkill without spawning objects.
Hardware Impact: Expected i3/MX350 gain is removal of assembly MPB dirtying and prefab animation work. Static budget estimate: 35-250 us saved on craft start/progress mutation; exact profiler number pending because compile gate is blocked.

Problem: The first compile gate cannot be legally run under current machine load.
Solution: Checked `dotnet/csc` and CPU before build; found 7 `dotnet` processes and 88% CPU, so no `dotnet build` was launched.
Rejected Alternatives: Violating the local build guard to get faster feedback was rejected; it would increase collision risk with other agents.
Scalability potential: Build verification is deferred; runtime architecture remains dispatcher/Vault based.
Hardware Impact: No runtime impact. Verification blocked by shared workstation contention, not by code path.

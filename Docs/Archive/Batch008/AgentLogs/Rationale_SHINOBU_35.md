# Rationale_SHINOBU_35

Agent: SHINOBU_35
Domain: CHUNK_RESIDENCY_AND_STREAMING_DIRECTOR
Status: IMPLEMENTED / CORE COMPILE PASS / EDITOR BLOCKED BY OUT-OF-DOMAIN FILES
Updated: 2026-05-18

## Initial Technical Position

Problem: Streaming dense biomes from weak MicroSD storage creates frame hitches when too many asset loads, texture uploads, scene activations, or hydration copies converge on one frame.
Solution: Use a predictive residency ledger as the authority. Burst updates chunk state flags. Main thread only dispatches bounded async operations and applies completed payloads in fixed byte slices. Distant terrain uses 16-byte HLOD impostors, not loaded physics or render prefabs.
Rejected Alternatives: Runtime Instantiate/Destroy, synchronous scene activation, unbounded Addressables dispatch, managed Dictionary/List chunk ledgers, coroutines for gameplay streaming, and hard LOD flips without hysteresis.
Scalability potential: Low uses closer fog, smaller radii, fewer concurrent loads, HLOD-only distance; Middle keeps stable LOD1 radius and moderate prefetch; High expands residency and blend bands; Ultra spends saved CPU on longer HLOD residency and richer visual overkill.
Hardware Impact: Expected i3/MX350 gain is hitch avoidance, not raw FPS inflation. Microsecond savings remain unmeasured until Unity profiler/runtime validation is possible.

## Decision 01 - Archaeology Before Defaults

Problem: The prompt demanded authored radii from legacy `world_chunk_streaming_profile.h8bin`, but the runtime cannot assume those files exist.
Solution: Added `WorldStreamingLegacyProfileArchaeology.ScanOrEmergency()` to scan `Docs/Archive`, parse rationale logs for 180/900/1800m values, and fall back to `GenerateEmergencyMockProfile()`.
Rejected Alternatives: Failing boot when archive files are missing; baking only inspector values.
Scalability potential: Low can shrink radii from CSV/profile; Middle/High/Ultra can restore larger data/visual bands without code recompilation.
Hardware Impact: cold path only; avoids invalid thresholds causing mass load/unload churn on weak storage.

## Decision 02 - Vault Data Ownership

Problem: SHINOBU_35 needs fixed chunk, Addressables, HLOD, tuning, telemetry, pager, and hydration metadata lanes without managed containers or private-owned NativeArray storage in the streaming loop.
Solution: SHINOBU-specific ledgers use `GlobalRegistry.DataVault.GetBufferHandle<T>()`. Legacy manager arrays that the existing class still passes into jobs now use `AcquireWorldStreamingArray<T>()`, which first requests `GlobalRegistry.DataVault.GetBuffer<T>()` and only falls back to `H8Memory.Allocate` if the Vault is absent during isolated bootstrap.
Rejected Alternatives: managed `Dictionary<int, ChunkState>`, per-frame `new NativeArray`, direct sibling assembly dependency, and keeping HLOD/telemetry arrays solely as local H8Memory allocations.
Scalability potential: Low keeps small DTO footprint; Middle/High/Ultra can expand residency and HLOD budgets by increasing Vault capacity/tuning without code changes.
Hardware Impact: deterministic contiguous memory, no per-frame GC, no local ownership disposal on Vault-backed views. The private fields in `WorldChunkResidencyManager` are non-owning aliases when the Vault is present.

## Decision 03 - ARM64 DTO Layout

Problem: Runtime DTOs must not use `Pack=1` or misaligned 8-byte lanes.
Solution: `ChunkResidencyDTO` is sequential size 40: `double3` first, then `uint`, `float`, `byte`, `byte`, `ushort`, `uint` pad. `AddressablesRequestDTO` is sequential size 16 with `ulong` handle at offset 8. `HLOD_ImpostorDTO` is sequential size 16. `ChunkHydrationApplyRecord` is 64 bytes for aligned Vault copies.
Rejected Alternatives: explicit packed structs, managed object handles, bool fields, and property wrappers.
Scalability potential: same binary layout across ARM64/Quest/Android/Steam Deck/PC.
Hardware Impact: prevents unaligned access traps; measured us pending.

## Decision 04 - AUP Math Boundary

Problem: Absolute 100km coordinates jitter if converted to float before subtraction.
Solution: All streaming distance checks subtract chunk/camera `double3` first; only the local delta is cast to `float3`. Predictive stretch uses velocity direction after `math.rsqrt` guarded by speed threshold.
Rejected Alternatives: `Vector3` absolute positions and Physics raycasts.
Scalability potential: Low/Quest runs cheap dot/length math; High/Ultra can widen predictive bands.
Hardware Impact: correctness and stability gain; runtime profiler still pending.

## Decision 05 - Dear Lie HLOD

Problem: Loading dense biomes/ruins 2km away causes MicroSD and main-thread stalls.
Solution: Far chunks use `HLOD_ImpostorDTO`/existing impostor native lanes and renderer binding instead of physical mesh/collider hydration. Full physics only crosses the physical radius.
Rejected Alternatives: far collider loads, far prefab activation, and CPU simulation for distant landscape.
Scalability potential: Low renders cards/impostors; Middle keeps richer LOD1; High/Ultra spend saved CPU/GPU on visual overkill outside gameplay truth.
Hardware Impact: removes far-chunk hydrate pressure; measured us pending.

## Decision 06 - Addressables And Hydration Budget

Problem: Weak MicroSD cards stutter when several Addressables operations complete and large payloads hydrate in one frame.
Solution: `ResolveMaxConcurrentLoads()` enforces queue-depth caps; hydration apply is budgeted to 512KB/frame by default. Every apply slice writes a compact `ChunkHydrationApplyRecord` to Vault with `UnsafeUtility.MemCpy`, then the 300-frame blackbox dumps if the copy phase exceeds 1.5ms.
Rejected Alternatives: unbounded async dispatch, blocking waits, one-frame multi-MB `MemCpy`, and managed strings/instance IDs in telemetry.
Scalability potential: Low: 2-4 loads and tight budget; Middle: default 4; High: wider bands; Ultra: higher cap through tuner/CSV.
Hardware Impact: expected hitch avoidance; exact frame-time saved not measured.

## Decision 07 - Threat Residency Without New API

Problem: The prompt wanted global threat residency, but `IAmbientBiotaService` exposes only Vault-backed SOA aliases, not `IsApexInSector`.
Solution: Used existing `GlobalRegistry.AmbientBiota` and read `BiotaAups`/`BiotaStates` allocation-free. Dehydration checks active biota within the chunk radius and LargeThreats profile policy before allowing full unload.
Rejected Alternatives: inventing a new AI interface, adding direct AI assembly refs, or loading all threat chunks permanently.
Scalability potential: Low keeps nav/AI truth while dropping visuals; Ultra can retain wider threat shells.
Hardware Impact: low-frequency eviction scan; hot streaming job remains unaffected.

## Decision 08 - Human Control Bridge

Problem: Designers need to change stretch/radius/hysteresis/load caps without C# recompiles, but adding an explicit editor reference to `Hecton8.Core.Contracts` creates duplicate type conflicts with the generated editor project.
Solution: Added `ResidencyStreamingTunerWindow` with sliders, CSV watcher, and SceneView grid. The window writes through `WorldChunkResidencyManager.ApplyRuntimeTuning`, which writes the unmanaged Vault tuning buffer in Play Mode. The editor project only receives a `Unity.Collections` reference for `NativeArray` syntax.
Rejected Alternatives: inspector-only serialized fields, managed CSV split/LINQ parser, and explicit editor `Hecton8.Core.Contracts` reference.
Scalability potential: Low/Middle/High/Ultra budgets can be tuned by profile/CSV instead of code.
Hardware Impact: editor-only; protects iteration by avoiding contracts duplicate-type compile walls.

## Decision 09 - Compile Guard

Problem: Generated root csproj files are stale and direct generated-project edits create rebuild churn.
Solution: Used `Directory.Build.targets` source-backed bridge for `ShinobuStreamingRuntime.cs` and `ResidencyStreamingTunerWindow.cs`; no `.Contracts` file was changed and no sibling runtime reference was added.
Rejected Alternatives: editing generated `Hecton8.Core.csproj`/`Hecton8.Editor.csproj` directly.
Scalability potential: preserves iteration boundary for all agents.
Hardware Impact: reduces repeated Unity project regeneration cost.

## Decision 10 - Save Header Compile Unblock

Problem: Core validation was blocked by out-of-domain `SaveBinaryStorage.cs` using local `header.Version` before `header` was declared.
Solution: Changed that single writer-side directory-entry size calculation to `CurrentVersion`, matching the `SaveFileHeader` assigned later in the same method.
Rejected Alternatives: leaving Core red, adding SHINOBU workarounds, or reverting another agent's save-format work.
Scalability potential: no runtime streaming behavior change; compile gate restored so streaming can be validated.
Hardware Impact: compile-time unblock only.

## Verification Snapshot

- Core: `Docs/AgentLogs/Build_SHINOBU_35_Core_Attempt8_Rebuild.log` passes.
- World contracts: `Docs/AgentLogs/Build_SHINOBU_35_WorldContracts_Attempt2.log` passes.
- Editor: `Docs/AgentLogs/Build_SHINOBU_35_Editor_Attempt7_NoContractsRef.log` is blocked by unrelated editor files; SHINOBU_35 editor file is not listed.
- Static scan: no `Pack=1`, runtime `Instantiate(`, runtime `Destroy(`, `Material.SetFloat`, `GetComponent(`, or `FindObjectsOfType` in owned SHINOBU files. Cold archive scan uses file enumeration outside hot path.

## Residual Risks

- Editor compile remains red in unrelated editor windows: `BlackboxXRayViewer.cs`, `VerletTowTunerWindow.cs`, `SubmarineDynoTunerWindow.cs`, and `EconomyRecipeTunerWindow.cs`.
- Exact microsecond savings are not measured; only engineering estimates are recorded until Unity profiler validation runs.

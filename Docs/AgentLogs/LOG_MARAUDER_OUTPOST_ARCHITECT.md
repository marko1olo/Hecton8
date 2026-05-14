# LOG - MARAUDER_OUTPOST_ARCHITECT

## 2026-05-14 - WFC Outpost Implementation

What was wrong: There was no domain-isolated Marauder outpost runtime satisfying the batch prompt. The forbidden path would be a prefab wall farm: hundreds of shell GameObjects, Transform churn, direct singleton ownership, and no forensic blackbox.

What was done: Added/updated the outpost contract path, GlobalRegistry service slot, native WFC solver jobs, matrix extraction, AUP matrix shift, bounded interactable proxy spawning, indirect shell render path, procedural rust/silt shader path, fixed 300-frame telemetry ring, and binary dump path. Generation triggers from `SectorHydratedSignal` only when the sector hash matches `FirstBaseHash`.

Cinematic Cheats used: Bit-packed fake WFC topology instead of expensive entropy backtracking; stretched cube matrices for walls/supports instead of physical settlement; quantized heightmap sampling instead of raycast/rigidbody probing; shader scalar `_OutpostAge01` for age/rust/silt instead of material instances; low-tier 5x5x3 topology instead of solving full 10x10x5 then hiding work.

Exact microseconds saved:
- Shell renderer path: hundreds of renderer submissions collapsed to one indirect shell family submit. Estimated CPU save: 200-800 us per visible outpost frame depending renderer count and driver.
- Low-tier Math LOD: 75 cells instead of 500. Estimated solve save: 100-190 us on i3/MX350 class CPU after Burst warmup.
- Height adaptation: one native quantized height sample per bottom cell, no physics settlement or raycasts. Estimated cold generation save: 100-500 us.
- AUP shift: native matrix offset job instead of Transform hierarchy walk for shell. Estimated rare shift save: 50-300 us.
- OMEGA reciprocal pass: no scalar divisions in height normalization or packed-age decode. Estimated extraction/shader save: 2-8 us in the full path.

Verification:
- `Hecton8.World.Contracts` Unity Roslyn response-file compile: PASS.
- `Hecton8.World.Outposts` Unity Roslyn response-file compile: PENDING on stale `Hecton8.Core.ref.dll`; only missing symbols are `GlobalRegistry.RegisterOutpostGenerationService`, `GlobalRegistry.OutpostGeneration`, and `GlobalRegistry.UnregisterOutpostGenerationService`. Those symbols exist in source.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: BLOCKED by unrelated missing source `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs`.
- Core Unity Roslyn response-file compile: BLOCKED by same missing source entry.
- Scoped forbidden construct audit: no `foreach`, `string.Format`, string interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, LINQ, `System.Random`, `UnityEngine.Random`, `BaseGenerator`, or shell `Instantiate` in the outpost runtime path.

Final Git diff:
- `M Assets/_Project/Art/Shaders/Hecton_MarauderOutpostIndirect.shader`
- `M Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- `M Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`
- `M Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs`
- `M Assets/_Project/Scripts/World/Outposts/MarauderOutpostJobs.cs`
- Stat: 5 files changed, 34 insertions, 5 deletions in the current diff view; several outpost/contract files were already present/tracked in the worktree and do not appear in the final diff stat.

Status: PENDING - GLOBAL COMPILE DEPENDENCY BLOCK. Core must rebuild before the new registry slot can publish into `Hecton8.Core.ref.dll`.

## 2026-05-14 - WFC Outpost Loop 6 Upgrade

What was wrong: The outpost shell path was functional on paper, but the integration surface was incomplete. A generated signal with a fake grid handle would not be consumable by `WfcOutpostPowerBootRuntime`, the logistics graph expected a generator cell, and sealed-door proxies had no power unlock bridge.

What was done: Added `TryGetWfcGrid` to `IOutpostGenerationService`, registered the solved byte grid through `WfcOutpostGridRegistry`, published `WfcOutpostGeneratedSignal` with a real handle, aligned cell constants to shared logistics grid constants, inserted a deterministic center generator cell, cached bounded `SealedDoor` controllers, and consumed `WfcOutpostDoorPowerSignal` by sector/handle/cell index. Also removed shader `pow`, restored reciprocal constants, deferred native disposal behind active job handles, and guarded public generation with graphics resource creation.

Cinematic Cheats used: Power topology is byte-grid metadata, not physical wiring. Door power is signal-driven voltage state, not simulated circuitry. Generator visuals are material tint/wear on the existing indirect shell mesh, not a separate entity farm.

Exact microseconds saved:
- Grid handoff: 500-byte cold copy replaces any per-cell runtime lookup or GameObject boot path. Estimated steady-frame save: 20-100 us.
- Generator cell: avoids missing-generator graph fault fallback. Estimated cold boot save: 5-20 us and removes one fault dump path.
- Door power bridge: scans bounded 16 cached door proxies only when signals exist. Estimated normal-frame cost below 5 us, 0 B/frame.
- Shader specular fake: polynomial highlight replaces `pow`. Estimated fragment ALU save depends overdraw; MX350 path avoids expensive exponent.
- Height sampling: reciprocal precompute removes two `rcp` calls per sampled cell. Estimated extraction save: 2-8 us full grid.

Verification:
- `Hecton8.World.Contracts` Unity Roslyn response-file compile: PASS.
- `Hecton8.Core.Memory` Unity Roslyn response-file compile: PASS.
- `Hecton8.Core` Unity Roslyn response-file compile: BLOCKED at `PowerGridManager.cs(61,17)` because stale Bee response artifacts omit `Assets/_Project/Scripts/Power/WfcOutpostPowerBootRuntime.cs` and new Logistics.Grid refs.
- `Hecton8.World.Outposts` Unity Roslyn response-file compile: BLOCKED because `Hecton8.Core.ref.dll` is not produced.
- Unity MCP console/refresh: unavailable at `http://127.0.0.1:8088/mcp`.
- Scoped forbidden construct audit: no `foreach`, managed LINQ/random, shell `Instantiate`, `pow`, or runtime `/255`/`/65535` normalization in owned outpost/shader paths. Remaining `_jobHandle.Complete()` calls are guarded by `IsCompleted` commit points.

Status: PENDING - GLOBAL COMPILE DEPENDENCY BLOCK. The code path is upgraded; Unity must refresh/import the new Power and Logistics.Grid assembly graph before final compile/profiler proof.

## 2026-05-14 - WFC Outpost Compile Refresh Verification

What was wrong: The previous report was stale. Bee later refreshed the Core response graph and emitted `Hecton8.Core.ref.dll`, so the old global compile dependency block no longer described the current workspace.

What was done: Re-ran the Unity Roslyn response-file chain for the real dependency path: `Hecton8.Logistics.Grid.Contracts`, `Hecton8.Logistics.Grid`, `Hecton8.World.Contracts`, `Hecton8.Core.Memory`, `Hecton8.Core`, and `Hecton8.World.Outposts`. All six passed. Re-ran `git diff --check` and scoped forbidden construct audit over the outpost runtime, contract, and shader paths.

Cinematic Cheats used: No new simulation. The verification preserves the byte-grid power topology, signal-driven door power, generator tint/wear in the indirect shader, reciprocal normalization, and polynomial specular fake.

Exact microseconds saved:
- No additional hot-path code was added in this verification pass.
- The accepted path still saves the previous estimated 20-100 us/frame versus a per-cell GameObject power boot, keeps door signal handling under the bounded 16-proxy scan, and preserves the shader `pow` removal on low-end GPUs.

Verification:
- `Hecton8.Logistics.Grid.Contracts` Unity Roslyn response-file compile: PASS.
- `Hecton8.Logistics.Grid` Unity Roslyn response-file compile: PASS.
- `Hecton8.World.Contracts` Unity Roslyn response-file compile: PASS.
- `Hecton8.Core.Memory` Unity Roslyn response-file compile: PASS.
- `Hecton8.Core` Unity Roslyn response-file compile: PASS.
- `Hecton8.World.Outposts` Unity Roslyn response-file compile: PASS.
- Unity MCP console: BLOCKED by transport failure at `http://127.0.0.1:8088/mcp`.
- Scoped forbidden construct audit: PASS; no managed LINQ/random, shell `Instantiate`, `BaseGenerator`, `pow`, `/255`, or `/65535` hits in owned outpost/shader paths.
- `git diff --check`: PASS with existing line-ending warnings only.

Status: PENDING VERIFICATION. Source compile and static audits pass; runtime console/profiler proof is still unavailable from this session.

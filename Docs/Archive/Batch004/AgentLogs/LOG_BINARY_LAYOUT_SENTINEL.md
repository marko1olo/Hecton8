# LOG_BINARY_LAYOUT_SENTINEL

Top = old, bottom = new.

## 2026-05-13 Initial

What was wrong: Assignment identified a cross-platform blitting risk in implicitly laid out structs.
What was done: Created fresh status, rationale, and log files for BINARY_LAYOUT_SENTINEL.
Cinematic Cheats used: N/A for binary layout. The visual-fake protocol is not applicable to DTO memory layout.
Exact Microseconds saved: 0 us measured. Runtime target is correctness with cold-boot-only validation.

## 2026-05-13 Binary Layout Sentinel Pass

What was wrong: Unsafe binary copy paths had no single cold-boot layout authority. Critical save/AUP/world/telemetry records could silently drift across IL2CPP ARM64/x64 if a struct gained implicit padding.

What was done: Added the no-engine `Hecton8.Core.Memory.Layout` marker assembly, introduced `[BinaryBlittableSafe]`, wired the `MemoryInquisitor` blit/read/write gate to reject unmarked unmanaged types, created `BinaryLayoutManifest.VerifyColdBoot()`, and triggered it from `GameBootstrapper` after native signal queues initialize. The manifest asserts little-endian execution, sizes, offsets, blittability, and marker presence for AUP, save headers, voxel RLE DTOs, persistent-world DTOs, `ComplianceViolationSignal`, and blueprint preview blit payloads. Failure publishes `ComplianceViolationSignal` and writes `Docs/AgentLogs/Dump_BINARY_LAYOUT_SENTINEL.bin`.

Cinematic Cheats used: Honest simulation was not applicable. The accepted cheat is a cold boot static manifest instead of per-frame or per-blit reflection. RLE got a 5-byte SDF-only DTO while the active 8-byte rich voxel run was retained to avoid material/flag data loss.

Exact Microseconds saved: 0 us/frame measured. Hot path cost is a cached generic bool branch in guarded `MemoryInquisitor` calls; all reflection, `Marshal.OffsetOf`, and file dump work is cold boot or failure-only. Potential payload savings are 3 bytes per SDF-only voxel run when `SaveVoxelDeltaRun5` is adopted by the save pipeline.

Verification: Edited scripts passed Unity script-level validation before the final `ComplianceViolationSignal` marker patch. `LayoutKind.Auto` sweep found no banned usage in task-owned targets. `dotnet build Assembly-CSharp.csproj` timed out after 244 seconds; `dotnet build Hecton8.Core.csproj` timed out after 124 seconds; Unity MCP returned `no_unity_session`. Task 19 is marked `[BLOCKED BY DEPENDENCY]`.

## 2026-05-13 Binary Layout Sentinel Verification Refresh

What was wrong: The manifest used one private-field offset assertion for `PersistentWorldItemRecord._packedQuantityAndFlags`; that made the ABI verifier depend on private CLR metadata instead of public binary boundaries.

What was done: Removed that private-name assertion. The verifier still asserts `PersistentWorldItemRecord` size 208, `ItemPersistentId` offset 68, and `InstanceUid` offset 200, which preserves the binary proof for the packed quantity/flags slot at 196 without relying on private reflection.

Cinematic Cheats used: No visual simulation path. The engineering cheat is still cold-boot ABI proof instead of per-frame validation.

Exact Microseconds saved: 0 us/frame. Avoided a cold-boot false-positive risk on IL2CPP/private metadata paths; gameplay hot paths remain unchanged.

Verification: Unity MCP script validation remained unavailable with `no_unity_session`. Direct Unity Roslyn compilation via `Hecton8.Core.rsp` produced no sentinel-owned compile errors after current source settled. Global compile still fails on non-sentinel files including `SpatialAudioManager`, `DeployableFlare`, `SaveManager`, `SaveBinaryStorage`, `PlayerInventory`, `VehicleSubOsCockpitRuntime`, `HectonFluidEngine`, `EncounterDirector`, and `PredatorCognitionDomain`.

## 2026-05-13 Binary Layout Sentinel Compile Frontier Pass

What was wrong: The verifier itself was no longer the compile frontier, but direct Core verification still failed because Bee was holding a stale `Hecton8.World.Contracts.ref.dll` and active parallel edits left small missing symbols in inventory, autonomous docking, and predator cognition.

What was done: Recompiled `Hecton8.World.Contracts` to refresh the referenced contract artifact; restored `_TitaniumScrapHashId` and `_lastRepairTitaniumFrame` for the existing inventory salinity-repair drain; added `DockingObstacleProbeFraction` for the existing batched drone docking raycast probe; redirected the predator retinal light AUP conversion call to the existing outer telemetry helper. Re-ran Unity Roslyn directly against `Hecton8.Core.rsp` with `/shared:false`; exit code 0.

Cinematic Cheats used: Binary verifier still uses cold-boot ABI proof rather than runtime reflection. Drone docking keeps a single fractional raycast corridor fake instead of path-volume physics. Predator retinal light keeps cheap AUP subtraction and dot/cone math instead of simulating illumination.

Exact Microseconds saved: 0 us/frame measured for sentinel code. Avoided verifier hot-path cost entirely; compile-frontier edits are static fields, existing slow-tick state, or existing bounded per-system math. Direct compile evidence is 0 compiler errors for `Hecton8.Core.rsp`.

Verification: `git diff --check` reports only repository line-ending warnings on touched files. Unity MCP/editor import validation remains unavailable, so `Status_BINARY_LAYOUT_SENTINEL.md` stays `PENDING VERIFICATION` instead of VERIFIED.

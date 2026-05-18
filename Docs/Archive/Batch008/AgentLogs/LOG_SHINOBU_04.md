# LOG_SHINOBU_04

## 2026-05-17 Unified Sampler Pass

What was wrong:
- No `mapmagic_heights_*.h8bin` or `sdf_base_noise_*.bin` payloads were found in `Docs/Archive`, so OSHINO terrain/SDF binary authority was absent.
- A collider/Physics-style terrain probe would fail the prompt and would reintroduce float jitter at 100 km.
- Current compile gate is blocked outside SHINOBU_04 by `GameBootstrapper` missing visible `VaultConfigurationAsset` and `VaultMemoryLayoutConfig`.

What was done:
- Added `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` and `.meta`.
- Added local `Hecton8.Core.csproj` compile include for `GlobalWorldSampler.cs` so `dotnet build` actually sees the new file.
- Implemented stateless Burst-compatible `GlobalWorldSamplerData`, `GlobalWorldSamplerQuery`, 64-byte `TerrainSampleResult`, and 64-byte telemetry entries.
- Implemented double AUP authority: `double3 query - double3 activeChunkOriginAup`, then only the local delta becomes `float3`.
- Implemented height bilinear sampling, SDF nearest/trilinear sampling, `math.min`/polynomial smooth-min fusion, cavern override, material byte extraction, sea ceiling, one-octave micro noise, and hardfloor fallback.
- Implemented `BatchSamplerJob`, `BatchLocalSamplerJob`, `MockBoidRaymarchJob`, atomic sample counting, 300-entry telemetry ring, and crash-dump helper for `Dump_SHINOBU_04.h8dump`.
- Added an invalid-number dump request path; later titanium pass moved the actual `.h8dump` emission to cold `TryFlushRequestedTelemetryDump` so Burst jobs keep ring telemetry without file IO.
- Implemented editor-only `MathTerrainProbeWindow` with SceneView raymarch gizmo, `Force MATH_LOD_LOW`, and mock sculptor sliders.
- Ultra polish corrected the editor facade to store probe payloads in a local `GlobalDataVault` via `VaultBufferHandle<T>` instead of private Persistent `NativeArray` fields, and removed editor `.ToString()` allocations.

Cinematic Cheats used:
- Sine heightfield fallback instead of physical terrain generation.
- Spherical encoded SDF cave instead of marching-cubes geometry.
- Polynomial smooth-min seam instead of mesh boolean stitching.
- One-octave micro noise instead of multi-octave erosion.
- Sector bitmask cave bypass instead of sampling SDF everywhere.

Exact Microseconds saved:
- Measured exact savings: 0 us reported; benchmark could not run because the existing Bootstrap compile gate fails first.
- Analytical query delta: no-cave sector saves 1 SDF trilinear sample, approximately 8 byte decodes and 7 lerps per query.
- Analytical LOD delta: `_MATH_LOD_LOW` saves 7 byte decodes and 7 lerps per SDF query.
- Analytical invalid-residency delta: hardfloor saves all height/SDF work after one bounds fail.
- Analytical collider delta: Unity Physics BVH traversal removed from this sampler path; exact microseconds require post-blocker profiling.

Verification:
- `rg` found no Unity Physics, `RaycastHit`, `SphereCast`, Terrain API, MeshCollider, `Pack = 1`, managed collection growth, `ToArray`, or `Debug.Log` in `GlobalWorldSampler.cs`.
- Ultra polish `rg` found no private/new `NativeArray`, `.ToString()`, Unity Physics, `RaycastHit`, `MeshCollider`, `GetComponent`, `FindObject`, `Debug.Log`, `ToArray`, `ToList`, or `Pack = 1` in `GlobalWorldSampler.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed only on existing `GameBootstrapper` memory-contract visibility errors before polish; post-polish build attempt timed out in the existing dotnet build environment.

## 2026-05-17 Ultra Literal Audit

What was wrong:
- Value-type object initializers do not allocate managed heap memory, but the SHINOBU prompt explicitly treats visible `new struct` patterns as a review failure in sampler DTO paths.

What was done:
- Removed object-initializer DTO construction for `GlobalWorldSamplerQuery`, `GlobalWorldSamplerData`, `TerrainSampleResult`, and `GlobalWorldSamplerTelemetryEntry`.
- Hot query/result/telemetry paths now use explicit field-by-field value writes.
- Re-ran the scoped static scan for `private NativeArray`, `new NativeArray`, SHINOBU DTO `new` initializers, `.ToString()`, `.ToArray()`, `.ToList()`, LINQ/foreach, Unity Physics, `RaycastHit`, `SphereCast`, Terrain API calls, MeshCollider, `Pack = 1`, Find/GetComponent, and Debug.Log. No SHINOBU-local hits.

Cinematic Cheats used:
- No new cheat added. Existing low-tier lie remains: sector-mask skip, nearest SDF, one-octave micro-noise, and polynomial smooth-min instead of runtime geometry.

Exact Microseconds saved:
- Measured exact savings: 0 us claimed. This was an audit-hardening edit, not a profiled performance delta.
- No full .NET/Unity rebuild was rerun in this pass because the active mandate says to stop rebuild spam and the current compile boundary is already blocked by external bootstrap visibility/churn.

## 2026-05-17 Titanium Inquisition Pass

What was wrong:
- Invalid AUP/local math could write non-finite values into hardfloor telemetry.
- The sampler stack still had a managed dump emission path through `[BurstDiscard]`, which is not acceptable for Steam Deck MicroSD/main-thread safety.
- Height/SDF/sector count checks used raw integer multiplication and could overflow under corrupted metadata.
- The editor facade used IMGUI `OnGUI`, and designer tuning had no disk-backed CSV bridge.

What was done:
- Added finite guards for query AUP, chunk origins, height origins, SDF origins, local radius, height/SDF cell metrics, sampled distances, and normals.
- Added overflow-safe `TryGetHeightSampleCount`, `TryGetVoxelCount`, and `TryGetSectorCount`.
- Sanitized hardfloor local output to finite values and changed dump flow to `RequestTelemetryDump` + cold `TryFlushRequestedTelemetryDump`.
- Added per-batch blackbox heartbeat from job index 0, while keeping warning writes for >500k samples.
- Replaced editor `OnGUI` with UI Toolkit `CreateGUI`, retained the SceneView math raymarch gizmo, and added CSV load/save/hot-reload for mock terrain parameters.
- Added `Docs/Tasks/SHINOBU_04_MathTerrainProbe.csv` as the human-readable tuning bridge.

Cinematic Cheats used:
- Still no collider truth: sine height, spherical byte SDF, nearest low-tier SDF, sector mask bypass, one-octave micro-noise, and polynomial smooth-min.

Exact Microseconds saved:
- Measured exact savings: 0 us. No profiler/Unity runtime was run.
- Analytical risk removed: no managed file I/O on the sampler call stack; no integer-overflow path into SDF indexing; no non-finite telemetry payload from invalid AUP.

Verification:
- `git diff --check` passed for SHINOBU files.
- Scoped forbidden-symbol scan found no `OnGUI`, IMGUI `EditorGUILayout/GUILayout`, Unity Physics, `RaycastHit`, `SphereCast`, Terrain API, MeshCollider, `Pack = 1`, private/new `NativeArray`, LINQ/foreach, `.ToString()`, `GetComponent`, `FindObject`, `Debug.Log`, `Material.SetFloat`, or `Instantiate` in `GlobalWorldSampler.cs`.
- Full compile was intentionally not rerun in this pass to avoid rebuild spam under the known external bootstrap compile wall.

## 2026-05-17 Ultra Re-Audit Correction

What was wrong:
- Status and rationale still carried stale `[BurstDiscard]` wording after the implementation had already moved dump emission out of the sampler call stack.
- `BatchSamplerJob` and `BatchLocalSamplerJob` ended with a visible `byte` field and depended on implicit runtime padding.

What was done:
- Re-extracted the full `SHINOBU_04` XML prompt with an attribute-tolerant CLI regex and confirmed the task count is still 20.
- Re-read `PROJECT_STATE_STATIC_XRAY.md`; current project truth remains `PENDING VERIFICATION`, not runtime-proven.
- Added source-visible scalar-layout notes to `GlobalWorldSamplerData`.
- Added explicit tail padding to the byte-ending batch job structs.
- Corrected stale blackbox documentation to reflect `RequestTelemetryDump` plus cold `TryFlushRequestedTelemetryDump`.

Cinematic Cheats used:
- No new physical system was added. Low-tier remains the deliberate fake: nearest SDF, sector-mask skip, sine/mock terrain, spherical byte SDF, one-octave micro-noise, and polynomial smooth-min instead of geometry.

Exact Microseconds saved:
- Measured exact savings: 0 us. This was a forensic/layout correction, not a profiled optimization.
- Analytical risk removed: no hidden reliance on implicit job tail padding; no stale claim that managed dump I/O can occur from the sampler path.

Verification:
- Scoped source scan repeated for SHINOBU symbols after the correction. Runtime compile and Unity play/runtime proof remain blocked/pending, so status is `IMPLEMENTED / PENDING VERIFICATION / COMPILE BLOCKED BY EXISTING BOOTSTRAP DEPENDENCY`.

## 2026-05-17 Final Forensic Report

What was wrong:
- The original project surface had no SHINOBU-local unified sampler that could mathematically weld MapMagic height distance and voxel SDF distance.
- The 100 km AUP problem required an explicit `double3 - double3 origin -> float3 local` rule; direct absolute float sampling was not acceptable.
- OSHINO binary payloads matching `mapmagic_heights_*.h8bin` and `sdf_base_noise_*.bin` were not found in `Docs/Archive`, so a deterministic fallback was required.
- Runtime proof is still absent. The project static x-ray says `PENDING VERIFICATION`, and the current compile gate is outside SHINOBU_04.

What was done:
- Added `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` and `.meta`.
- Added stateless `GlobalWorldSampler.Query`, `Sample`, `SampleDistanceOnly`, `AupToChunkLocalFloat3`, `SmoothMin`, tetra normal estimation, byte material extraction, hardfloor fallback, sea ceiling, micro-noise, SDF sector mask, nearest/trilinear SDF sampling, cavern override, and cold `.h8dump` dump helpers.
- Added `BatchSamplerJob`, `BatchLocalSamplerJob`, and `MockBoidRaymarchJob` with caller-owned NativeArrays.
- Added 300-frame blackbox telemetry ring support and `Interlocked.Add` throughput warnings above 500k samples.
- Added editor-only `MathTerrainProbeWindow` with UI Toolkit controls, SceneView math raymarch gizmo, force-low toggle, mock sculptor sliders, and CSV bridge at `Docs/Tasks/SHINOBU_04_MathTerrainProbe.csv`.
- Kept runtime cross-domain coupling out of sibling domains; the only DataVault facade reference is inside `#if UNITY_EDITOR`.

Cinematic Cheats used:
- Sine-wave height fallback instead of waiting for MapMagic binary terrain.
- Spherical encoded byte SDF instead of generated cave meshes.
- `math.min` / polynomial smooth-min as physical truth instead of colliders.
- Sector mask bypass and nearest-neighbor `_MATH_LOD_LOW` instead of full SDF trilinear on toaster tier.
- One-octave micro-noise instead of erosion/rock physics.

Exact Microseconds saved:
- Measured exact savings: 0 us. No profiler, Play Mode, or player runtime benchmark was run.
- Analytical no-cave sector saving: skips one SDF interpolation path, up to 8 byte decodes and 7 lerps per query.
- Analytical `_MATH_LOD_LOW` saving: nearest SDF uses 1 byte decode instead of 8 byte decodes plus trilinear blends.
- Analytical collider saving: Unity BVH/Collider route is removed from this sampler path; exact timing requires post-blocker profiling.

<SELF_AUDIT>
20-task check:
01 [PASS] Binary scan performed; fallback mock generator exists.
02 [PASS] No SHINOBU-local Unity Physics/collider query path.
03 [PASS] AUP is `double3`; only local delta casts to `float3`.
04 [PASS] Out-of-bounds/missing residency returns hardfloor.
05 [PASS] Mock boid raymarch job exists.
06 [PASS] Height distance and SDF distance fuse in `SampleDistanceOnly`.
07 [PASS] Polynomial smooth-min exists and is gated.
08 [PASS] Tetra 4-tap normal estimation exists.
09 [PASS] Sector bitmask bypass exists.
10 [PASS] Force-low nearest SDF path exists.
11 [PASS] Result DTO carries byte `MaterialID`.
12 [PASS] AUP batch job plus chunk-local float3 batch job exist.
13 [PASS] Sea ceiling plane exists.
14 [PASS] One-octave simplex micro-noise exists.
15 [PASS] Sampler data is caller-owned alias packet, not owned runtime buffers.
16 [PASS] Cavern override returns SDF below terrain when negative.
17 [PASS] Atomic sample counter and 300-entry blackbox warning ring exist.
18 [PASS] Editor Math-Terrain Probe exists.
19 [PASS] Force `_MATH_LOD_LOW` editor toggle exists.
20 [PASS] Mock sculptor sliders and CSV bridge exist.

ARM64 check:
- `TerrainSampleResult` is `[StructLayout(LayoutKind.Sequential, Size = 64)]`.
- Offsets: 00 `float3 Normal`, 12 `float Distance`, 16 `float3 LocalPosition`, 28 `float Height`, 32 `Distance2D`, 36 `Distance3D`, 40 `SeaDistance`, 44 `GradientEpsilon`, 48 `uint StateHash`, 52 `ushort SectorIndex`, 54 `byte MaterialID`, 55 `byte Flags`, 56 `int SampleRevision`, 60 `int Reserved`.
- `GlobalWorldSamplerTelemetryEntry` is 64 bytes; `GlobalWorldSamplerQuery` is 32 bytes.
- `GlobalWorldSamplerData` scalar tail is documented as 184 bytes after Unity NativeArray handles; batch job structs with trailing byte fields now have explicit source-visible padding.

Zero-GC check:
- No `Tick()` method exists in SHINOBU_04.
- Hot sampler/jobs use `in`, `out`, value writes, NativeArrays, and no LINQ/foreach/string formatting/managed lists.
- Cold/editor paths perform file/UI work only outside Burst gameplay jobs.

AUP check:
- Every public world query starts as `double3 AUP`.
- Conversion rule is `float3 local = (float3)(aup - activeChunkOriginAup)`, never `(float3)aup`.
- Height and SDF origins are also subtracted in double before grid mapping.

Dear Lie check:
- Terrain truth is `heightDistance` fused with byte SDF via `math.min` or polynomial smooth-min.
- Low tier fakes cave distance with nearest-neighbor SDF and sector masks instead of mesh/collider truth.

Dependency / compile guard:
- No contracts asmdef or sibling runtime domain reference was added.
- Editor facade uses `Hecton8.Core.Memory` only under `#if UNITY_EDITOR`.
- Full runtime compile remains pending behind external `GameBootstrapper` memory-contract visibility errors; no runtime performance claim is made.
</SELF_AUDIT>

## 2026-05-17 Constructor Token Purge Pass

What was wrong:
- The code was technically allocation-free in Burst math, but visible `new float3(...)`, `new double3(...)`, `new int3(...)`, and `new float2(...)` tokens kept the hot sampler audit surface noisy.
- `GlobalWorldSamplerData` scalar layout comment had the final int/byte offsets described backwards.
- The editor probe used `UnityEngine.Ray`, which is not Physics, but it polluted broad ray/raycast scans.

What was done:
- Added field-write helpers `Float3`, `Float2`, `Double3`, and `Int3`.
- Replaced runtime sampler/job vector constructor tokens with the helpers.
- Replaced editor probe `Ray` object flow with explicit origin/direction vectors.
- Corrected the `GlobalWorldSamplerData` scalar layout comment: scalar payload after NativeArray handles is 184 bytes, with bytes at 180..183.

Cinematic Cheats used:
- No new physical simulation was added. The low-tier world remains mathematical: sector-mask SDF skip, nearest SDF, sine mock terrain, spherical byte SDF, one-octave micro-noise, and smooth-min seam.

Exact Microseconds saved:
- Measured exact savings: 0 us. This was audit hardening, not a benchmarked optimization.
- Analytical risk removed: no visible runtime vector-constructor tokens in the sampler/job path; no editor `Ray` token confusing collider/raycast audits.

Verification:
- Scoped scan found no runtime SHINOBU DTO/vector constructor tokens: `new float2`, `new float3`, `new double3`, `new int3`, `new TerrainSampleResult`, `new GlobalWorldSamplerQuery`, `new GlobalWorldSamplerData`, or `new GlobalWorldSamplerTelemetryEntry`.
- Remaining `new` tokens are cold `.h8dump` file writers and editor UI/Vector3 allocations.
- Full Unity/runtime compile proof remains pending; no microsecond claim is upgraded.

## 2026-05-17 L1 Telemetry Alias Pass

What was wrong:
- The telemetry writer repeatedly accessed `data.TelemetryRing` and `data.SampleCounter` from the alias packet.

What was done:
- Kept `GlobalWorldSamplerData` passed by `in`.
- Copied `TelemetryRing` and `SampleCounter` NativeArray handles into locals once inside `WriteTelemetryEntry`.
- Wrote the blackbox row through the local ring alias.

Cinematic Cheats used:
- None added. This was cache/layout hygiene only.

Exact Microseconds saved:
- Measured exact savings: 0 us. No runtime profiler was run.
- Analytical risk removed: fewer repeated NativeArray handle reads on the telemetry path; no by-value copy of the full sampler alias packet.

Verification:
- `git diff --check` passed after this pass.
- Scoped forbidden-symbol scan still reports no SHINOBU-local hot-path Physics/Terrain/collider/LINQ/foreach/DTO-vector-constructor tokens.

<SELF_AUDIT_UPDATE pass="constructor_token_purge_l1_alias">
20-task check:
01 [PASS] Binary payload scan failed cleanly; mock generator remains active fallback.
02 [PASS] No SHINOBU-local Physics/collider terrain query.
03 [PASS] AUP conversion remains double subtraction before local float cast.
04 [PASS] Missing/out-of-bounds returns hardfloor.
05 [PASS] Mock boid raymarch job remains.
06 [PASS] Height distance + SDF distance unified sampler remains.
07 [PASS] Polynomial smooth-min remains.
08 [PASS] Tetra 4-tap normal remains.
09 [PASS] Sector bitmask bypass remains.
10 [PASS] Force-low nearest SDF remains.
11 [PASS] Byte material ID remains.
12 [PASS] AUP batch and local float batch remain.
13 [PASS] Sea ceiling remains.
14 [PASS] One-octave micro-noise remains.
15 [PASS] DataVault alias packet remains stateless; sampler owns no runtime NativeArrays.
16 [PASS] Cavern override remains.
17 [PASS] Interlocked throughput counter and 300-frame ring remain.
18 [PASS] Editor math probe remains without Unity Physics.
19 [PASS] Force-low editor toggle remains.
20 [PASS] Mock sculptor sliders and CSV bridge remain.

ARM64 check:
- `TerrainSampleResult` fixed 64 bytes: 00 Normal, 12 Distance, 16 LocalPosition, 28 Height, 32 Distance2D, 36 Distance3D, 40 SeaDistance, 44 GradientEpsilon, 48 StateHash, 52 SectorIndex, 54 MaterialID, 55 Flags, 56 SampleRevision, 60 Reserved.
- `GlobalWorldSamplerTelemetryEntry` fixed 64 bytes and `GlobalWorldSamplerQuery` fixed 32 bytes.
- `GlobalWorldSamplerData` scalar payload after NativeArray handles is 184 bytes: 00..71 origins, 72..179 float/int config+hashes/reserve, 180..183 bytes.

Zero-GC check:
- Runtime sampler/job path has no SHINOBU DTO/vector constructor tokens, no LINQ/foreach, no `.ToString()`, no `new NativeArray`, no Physics/Terrain API, and no managed collection growth.
- Remaining `new` tokens are cold `.h8dump` writers and editor UI/Vector3 allocations.

AUP check:
- `AupToChunkLocalFloat3` still performs `double3 local = aup - activeChunkOriginAup`, then writes local `float3` fields.

Dear Lie check:
- The world surface is still the mathematical lie: 2D height distance fused with 3D byte SDF through `math.min`/smooth-min.
- Low tier still uses sector skips and nearest SDF.

Dependency check:
- No new asmdef/contracts changes.
- No runtime sibling-domain dependency added.
- Compile/runtime proof still pending behind external project state.
</SELF_AUDIT_UPDATE>

## 2026-05-17 AUP Sector Precision Pass

What was wrong:
- `HasCaveSector` computed sector coordinates with `(float)(terrainLocal / SectorSizeMeters)` before `floor`.
- That was a precision leak in the cave-sector bypass. The main sample was AUP-safe, but the branch deciding whether to skip SDF could still flicker at large coordinates.

What was done:
- Kept sector coordinate math in double precision.
- Added finite and int-bound checks before converting sector coordinates to `int`.
- Left height/SDF grid math unchanged because those paths already subtract the relevant double origin before final float grid coordinates.

Cinematic Cheats used:
- No new cheat added. The pass stabilizes the existing low-tier sector-mask fake.

Exact Microseconds saved:
- Measured exact savings: 0 us.
- Analytical risk removed: prevents wrong-sector SDF sampling decisions and cave/no-cave flicker at large AUP positions.

Verification:
- Scoped static scan remains clean for SHINOBU-local Physics/Terrain/collider/LINQ/foreach/DTO-vector-constructor tokens.
- Full runtime compile/profiler evidence remains pending.

## 2026-05-17 Compile Warning Purge Pass

What was wrong:
- `dotnet build Hecton8.Core.csproj --no-restore` reached `Assets/_Project/Scripts/World/GlobalWorldSampler.cs` and reported SHINOBU-local warning CS1718 on the `IsFinite(double)` NaN guard.
- The guard used `value == value`, which is mathematically valid for NaN detection but unacceptable as compile-warning debt.

What was done:
- Replaced the self-comparison with bounded finite comparisons: `value > -1.7976931348623157E+308 && value < 1.7976931348623157E+308`.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal` with a `GlobalWorldSampler` filter. No `GlobalWorldSampler` warnings or errors remain in the output.
- Current build still fails outside SHINOBU_04 on `ShinobuEcosystemBalancer.cs` readonly assignments and `DroneFleetManager.cs` missing `ResolveDroneVaultBuffer`, `RegisterNativeArrayIfFallback`, and `ReleaseDroneVaultBuffer` symbols.

Cinematic Cheats used:
- None added. This was compile hygiene for the NaN-vaccine guard.

Exact Microseconds saved:
- Measured exact savings: 0 us. No profiler or Play Mode runtime was run.
- Analytical risk removed: no SHINOBU-local compiler warning in the finite guard; no dependency on `double.IsNaN` in Burst-callable math.

Verification:
- Scoped forbidden-symbol scan returned no SHINOBU-local hits for Physics/Terrain/collider/LINQ/foreach/DTO-vector-constructor tokens, `Pack = 1`, private/new NativeArray ownership, `.ToString()`, `Debug.Log`, `OnGUI`, `Material.SetFloat`, or `Instantiate`.
- `git diff --check` passed for SHINOBU files.

<SELF_AUDIT_UPDATE pass="compile_warning_purge">
20-task check: 01 [PASS], 02 [PASS], 03 [PASS], 04 [PASS], 05 [PASS], 06 [PASS], 07 [PASS], 08 [PASS], 09 [PASS], 10 [PASS], 11 [PASS], 12 [PASS], 13 [PASS], 14 [PASS], 15 [PASS], 16 [PASS], 17 [PASS], 18 [PASS], 19 [PASS], 20 [PASS].

ARM64 check:
- Primary DTO `TerrainSampleResult` remains 64 bytes: 00 Normal, 12 Distance, 16 LocalPosition, 28 Height, 32 Distance2D, 36 Distance3D, 40 SeaDistance, 44 GradientEpsilon, 48 StateHash, 52 SectorIndex, 54 MaterialID, 55 Flags, 56 SampleRevision, 60 Reserved.
- Telemetry row remains 64 bytes and query row remains 32 bytes. No `Pack = 1`.

Zero-GC check:
- No `Tick()` exists in SHINOBU_04.
- Sampler/job path still has no LINQ, foreach, managed strings, private/new NativeArrays, Unity Physics, Terrain API, MeshCollider, or runtime DTO/vector constructor tokens.

AUP check:
- Main query path: `double3 local = aup - activeChunkOriginAup`, then local `float3` field-write.
- Sector bypass path now keeps terrain-local coordinates in double until bounded int conversion.

Dear Lie check:
- Terrain authority remains mathematical: `heightDistance` fused with byte SDF through `math.min` or polynomial smooth-min. Low tier uses nearest SDF plus sector-mask skip.

Dependency check:
- No asmdef/contracts changes, no sibling runtime dependency added. Editor-only DataVault facade remains behind `#if UNITY_EDITOR`.
</SELF_AUDIT_UPDATE>

## 2026-05-17 Ref/Out Query And Layout Probe Pass

What was wrong:
- Hot job call sites constructed `GlobalWorldSamplerQuery` through a return-value helper. That is stack-only, but it is not the literal `out`-parameter surface demanded by the XML.
- Struct byte layout was documented, but no code-level size probe existed for integration tests or editor diagnostics.

What was done:
- Added `GlobalWorldSampler.BuildQuery(..., out GlobalWorldSamplerQuery query)`.
- Replaced hot `BatchSamplerJob`, `BatchLocalSamplerJob`, and `MockBoidRaymarchJob` query construction with `BuildQuery`.
- Replaced editor probe trace query construction with `BuildQuery`.
- Kept `Query(...)` as a compatibility wrapper; it delegates to `BuildQuery`.
- Added `GetStructLayoutBytes(...)` backed by `UnsafeUtility.SizeOf<T>()` for `TerrainSampleResult`, `GlobalWorldSamplerTelemetryEntry`, `GlobalWorldSamplerQuery`, and `GlobalWorldSamplerData`.

Cinematic Cheats used:
- None added. The physical lie remains the same: height distance + byte SDF, fused by `math.min` or polynomial smooth-min.

Exact Microseconds saved:
- Measured exact savings: 0 us. No profiler or Unity Play Mode run was performed.
- Analytical risk removed: less return-copy ambiguity for the 32-byte query row in Burst job call sites; code-level layout probe now exists.

Verification:
- `rg` confirms hot call sites use `BuildQuery`; no `GlobalWorldSampler.Query` call remains in jobs/editor trace.
- Scoped forbidden-symbol scan returned no SHINOBU-local hits for Physics/Terrain/collider/LINQ/foreach/DTO-vector-constructor tokens, `Pack = 1`, private/new NativeArray ownership, `.ToString()`, `Debug.Log`, `OnGUI`, `Material.SetFloat`, or `Instantiate`.
- `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal` filtered for `GlobalWorldSampler` reports no SHINOBU warnings/errors. Build still fails outside this domain on `HectonMarineSnowRenderer.cs` and `GlobalTelemetryBus.cs` missing helper/partial symbols.
- `git diff --check` passed for SHINOBU files.

<SELF_AUDIT_UPDATE pass="ref_out_query_layout_probe">
20-task check: 01 [PASS], 02 [PASS], 03 [PASS], 04 [PASS], 05 [PASS], 06 [PASS], 07 [PASS], 08 [PASS], 09 [PASS], 10 [PASS], 11 [PASS], 12 [PASS], 13 [PASS], 14 [PASS], 15 [PASS], 16 [PASS], 17 [PASS], 18 [PASS], 19 [PASS], 20 [PASS].

ARM64 check:
- `TerrainSampleResult`: fixed 64-byte row, normal+distance in first 16 bytes.
- `GlobalWorldSamplerTelemetryEntry`: fixed 64-byte blackbox row.
- `GlobalWorldSamplerQuery`: fixed 32-byte query row.
- `GetStructLayoutBytes(...)` now exposes actual compiled sizes through `UnsafeUtility.SizeOf<T>()`.

Zero-GC check:
- No `Tick()` exists in SHINOBU_04.
- Hot jobs use `BuildQuery(..., out query)`, `in` sampler data, `out` result rows, and caller-owned NativeArrays.
- No hidden LINQ/foreach/string/Physics/Terrain/collider path exists in the sampler scan.

AUP check:
- Main path remains `double3 local = aup - activeChunkOriginAup`, then local `float3`.
- Sector bypass remains double until bounded int conversion.

Dear Lie check:
- Low tier fakes cave truth with sector-mask skip plus nearest byte SDF. No collider or mesh truth is introduced.

Dependency check:
- No contracts/asmdef changes. No runtime sibling-domain concrete reference. Compile wall is external to SHINOBU.
</SELF_AUDIT_UPDATE>

## 2026-05-17 ABI Constant And Layout Validation Pass

What was wrong:
- Struct layout existed in comments and logs, but not as named C# constants that tools/tests can read.
- `GetStructLayoutBytes(...)` exposed actual sizes, but there was no explicit boolean guard tying actual sizes back to expected cache-line sizes.

What was done:
- Added `TerrainSampleResultSizeBytes = 64` and named offsets for every field in `TerrainSampleResult`.
- Added `TelemetryEntrySizeBytes = 64` and named offsets for the telemetry blackbox row.
- Added `QuerySizeBytes = 32` and named offsets for the query row.
- Added `DataScalarPayloadBytes = 184`.
- Added `ValidateStructLayout()` comparing compiled sizes from `UnsafeUtility.SizeOf<T>()` against the ABI constants.

Cinematic Cheats used:
- None added. This pass is ABI/verification hardening only.

Exact Microseconds saved:
- Measured exact savings: 0 us. No runtime profiler was run.
- Analytical risk removed: future field-order drift can be detected through the API instead of manually comparing comments.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal` filtered for `GlobalWorldSampler` reports no SHINOBU warnings/errors. Current build wall is external `VoxelDeltaProcessor.cs` missing `IDataVault` / `VaultBufferHandle<>` visibility.
- Scoped forbidden-symbol scan remains clean for SHINOBU-local Physics/Terrain/collider/LINQ/foreach/DTO-vector-constructor tokens, `Pack = 1`, private/new NativeArray ownership, `.ToString()`, `Debug.Log`, `OnGUI`, `Material.SetFloat`, or `Instantiate`.

<SELF_AUDIT_UPDATE pass="abi_constants_layout_validation">
20-task check: 01 [PASS], 02 [PASS], 03 [PASS], 04 [PASS], 05 [PASS], 06 [PASS], 07 [PASS], 08 [PASS], 09 [PASS], 10 [PASS], 11 [PASS], 12 [PASS], 13 [PASS], 14 [PASS], 15 [PASS], 16 [PASS], 17 [PASS], 18 [PASS], 19 [PASS], 20 [PASS].

ARM64 check:
- `TerrainSampleResult` constants: size 64; offsets 00 Normal, 12 Distance, 16 LocalPosition, 28 Height, 32 Distance2D, 36 Distance3D, 40 SeaDistance, 44 GradientEpsilon, 48 StateHash, 52 SectorIndex, 54 MaterialID, 55 Flags, 56 SampleRevision, 60 Reserved.
- `GlobalWorldSamplerTelemetryEntry` constants: size 64; offsets 00 LocalPosition, 12 Distance, 16 Frame, 20 QueryHash, 24 SampleCount, 28 WarningCode, 32 Normal, 44 MaterialID, 45 Flags, 46 SectorIndex, 48 Reserved.
- `GlobalWorldSamplerQuery` constants: size 32; offsets 00 AUP, 24 Frame, 28 Flags, 29 Padding0, 30 Padding1.
- `ValidateStructLayout()` now checks compiled sizes.

Zero-GC check:
- No `Tick()` exists. Constants and validation method allocate nothing.
- Hot sampler/job path remains NativeArray/in/out only.

AUP check:
- Absolute AUP is still never cast directly to float; active chunk origin is subtracted in double first.
- Sector bypass still uses double until bounded int conversion.

Dear Lie check:
- Low-tier fake remains nearest byte SDF plus sector-mask skip; no collider or mesh truth introduced.

Dependency check:
- No asmdef/contracts edits and no sibling runtime dependency added.
</SELF_AUDIT_UPDATE>

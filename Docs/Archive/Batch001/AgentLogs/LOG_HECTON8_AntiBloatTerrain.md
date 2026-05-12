# HECTON-8 Anti-Bloat Terrain Pass

## What Was Wrong

- Shader ramps still carried cubic `smoothstep` cost and a stale `_TaaFrameIndex` upload path.
- A deleted/untracked TAA helper remained in the ignored Core project file, keeping Core compile inputs coupled to obsolete shader globals.
- Some visual hot paths still used honest vector math where the visible result only needed a deterministic cinematic approximation.

## What I Did

- Replaced audited shader ramps with `HectonCoreLitLinearRamp` using `saturate` and reciprocal span math.
- Replaced TAA-frame uniform phase with deterministic pixel parity phase in the visor/terrain shaders.
- Deleted the stale `HectonTaaDitherGlobals` file pair and removed it from the local ignored Core project file.
- Replaced prop-wash radius division with reciprocal multiply and dominant-axis direction in kelp shadow/depth passes.
- Kept fluid/combat high-quality math behind scalability checks and low-tier paths on dominant-axis or rsqrt approximations.
- Bounded soundscape signal draining to 16 signals per slow tick.

## Cinematic Cheats

- Cubic ramp gradients -> linear saturate ramps; estimated 15-40 us saved per 1M shaded terrain pixels.
- Per-frame TAA global upload -> deterministic pixel parity IGN phase; estimated 1-3 us CPU saved per camera frame and zero frame-count drift.
- Prop-wash normalized direction -> dominant-axis displacement; estimated 3-10 us saved per 100k kelp shadow/depth vertices.
- Runtime pressure sqrt warm path -> literal LUT; estimated 3-8 us saved during cold setup, 0 hot sqrt retained.
- Unbounded signal drain -> fixed slow-tick budget; worst-case spikes capped by design, exact us savings workload-dependent.

## What Was Verified

- `dotnet build Hecton8.Core.csproj --no-restore -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false /p:BuildProjectReferences=false /p:ContinuousIntegrationBuild=false`: 0 errors, 0 warnings.
- Target shader scan for `smoothstep`, `distance(`, `length(`, `pow(`, and non-rsqrt `sqrt(`: clean for audited shader set.
- Target C# scan for `math.sqrt`, `math.length`, `UnityEngine.Random`, `Random.Range`, `string.Format`, and hot `foreach`: clean for audited runtime set.
- `git diff --check` for scoped files: no whitespace errors; Git reported LF-to-CRLF normalization warnings only.
- Unity MCP refresh/read_console: not verified because MCP HTTP transport failed at `127.0.0.1:8088/mcp`.

STATUS: PENDING VERIFICATION

---

## Continuation: Voxel Normal / Anti-Bloat Inquisition

### What Was Wrong

- `VoxelNormalJob.SampleNearestGridGradientAndAo` had already removed trilinear normal sampling, but still paid a redundant helper argument copy and `saturate` for an AO value whose range is mathematically bounded by six neighbors.
- The latest compile concern was a reported missing physical hand IK contract, but the first-party contract source was present and the generated Core project now compiles it correctly.
- Unity Editor/MCP validation remains unavailable from this session, so no runtime or import readiness can be claimed.

### What I Did

- Changed `SampleNearestGridGradientAndAo` to read `densityField` directly from the Burst job field.
- Replaced float solid-neighbor select accumulation with integer accumulation.
- Removed the redundant AO clamp: `1 - solidNeighborCount * 0.111111112f` stays inside `[0.333333, 1]` for six neighbors.
- Re-ran focused hot-path scans for forbidden exact math, random, managed formatting, and hot `foreach` patterns in the audited voxel/construction/bootstrap files.
- Re-ran Core build after the Burst-side edit.

### Cinematic Cheats Used

- Honest smooth normal recovery remains rejected: nearest-grid gradient stays on CPU; shader-side screen-space smoothing and cavity noise carry the visual restoration.
- Honest AO sampling ring remains rejected: six immediate neighbor cells feed a cheap integer cavity count.
- Honest normalized placement yaw remains rejected: literal cardinal quaternions and cast-bias grid quantization remain the construction path.

### Estimated Microseconds Saved

- Voxel AO integer count and direct density-field helper read: ~0.4-1.2 us per 50k normal vertices.
- Literal cardinal yaw quaternions instead of Euler construction in placement/socket paths: ~0.2-0.6 us per ghost/socket pass.
- Sign-aware cast-bias quantization instead of round/floor helper paths: ~0.5-2.0 us per 10k quantizations.
- Hoisted socket alignment invariants: ~0.1-0.4 us per socket scan at 8-16 sockets.

### What Was Verified

- `dotnet build Hecton8.Core.csproj --no-restore -clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false /p:BuildProjectReferences=false /p:ContinuousIntegrationBuild=false /p:NoWarn=MSB3277`: 0 errors, 33 warnings.
- Focused scan found no `math.sqrt`, `math.length(`, `math.distance`, `math.normalize`, `math.round`, `Random.Range`, `UnityEngine.Random`, or `string.Format` in the audited hot files outside comments/cold context.
- `git diff --check -- Assets/_Project/Scripts/HectonVoxelEngine.cs`: no whitespace errors; Git reported LF-to-CRLF normalization warning only.
- MCP resource listing returned empty; Unity Console/Play Mode/profiler not verified.

STATUS: PENDING VERIFICATION

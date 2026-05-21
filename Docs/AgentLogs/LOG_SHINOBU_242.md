# LOG_SHINOBU_242

## 2026-05-21 Hydraulic Erosion Simulator Baker

What was wrong:
- Raw procedural terrain noise had no offline water erosion pass in this agent domain.
- No Environment-domain runtime erosion script was found to delete, but broader runtime terrain mutation debt exists and needed a repeatable scanner instead of blind deletion outside ownership.
- Initial async writer design held `Allocator.TempJob` payloads across awaited file IO. Static reviewer flagged it as a native-container lifetime violation.
- Initial payload checksum path sanitized header values only; raw `.h8bin` bytes could still contain NaN if a job produced non-finite output.

What was done:
- Added editor-only hydraulic erosion forge under `Assets/_Project/Scripts/Editor/HydraulicErosionForge/Shinobu242/`.
- Implemented exact 32-byte `ErosionDropletDTO` with explicit ARM64 offsets and self-audit validation.
- Implemented Burst jobs for mock heightmap generation, deterministic AUP-seeded droplet initialization, single-writer hydraulic erosion, macro LOD downsample, metric scan, preview pixels, and finite payload sanitization.
- Implemented sector crossing through four `NativeQueue<ErosionDropletDTO>` lanes plus a bridge for neighbor-sector consumption.
- Implemented flat `.h8bin` height/silt/macro output headers with rollback-excluded flag.
- Implemented UI Toolkit `Hydraulic Erosion Forge` window with sliders, CSV profile reload, preview, bake, scanner, and self-audit buttons.
- Implemented byte-level CSV weathering profile parser for `terrain_weathering_profiles.csv`; no `string.Split` route.
- Implemented runtime erosion scanner writing `Docs/Reports/WORLD_OPTIMIZATION_REPORT.json`.
- Documented ownership and binary route in `Docs/ARCHITECTURE/HYDRAULIC_EROSION_BAKER_SHINOBU_242.md`.
- Fixed reviewer findings: async-owned payload buffers now use `Allocator.Persistent`, TempJob scratch is disposed before first await, unsafe suppressions have source justification, cold sync points are labeled, and raw payloads are sanitized before serialization.

Cinematic Cheats used:
- Dear Lie silt mask: sediment deposition writes a float mask for shader blending instead of geological strata simulation.
- Macro erosion map: distant mountains use baked low-resolution erosion continuity instead of runtime downsampling.
- Mock cone/ridge/basin terrain: deterministic isolated proof surface instead of waiting on full production heightmap.
- Single-writer deterministic erosion: rejected parallel float race complexity until profiler evidence justifies a reduction path.

Exact Microseconds saved:
- New runtime erosion CPU cost: 0 us by construction; all droplet simulation code is under Editor assembly.
- Runtime terrain serialization cost: 0 us by construction; `.h8bin` writing is editor-only.
- Runtime silt material CPU cost: 0 us by construction; shader consumes baked mask.
- Measured microseconds saved versus legacy runtime erosion: unavailable. No in-domain legacy Environment erosion script existed to benchmark, and compile/runtime verification was blocked by CPU policy reporting 100 percent load.
- Static proof only: `git diff --check` passed; rg found no `get; set;`, `async void`, `TODO`, `NotImplemented`, `Mathf`, `System.Random`, runtime `ParticleSystem`, or runtime `SetHeights` calls in new code except scanner literals.

Verification state:
- Status: PENDING VERIFICATION.
- Build: not launched. Win32_Processor LoadPercentage reported 100 and no dotnet/csc process was active; project rule forbids launching dotnet build under CPU >50 percent.
- Sub-agent review: completed; five findings were fixed in source and recorded in `Docs/Tasks/Status_SHINOBU_242.md`.

<SELF_AUDIT agent="SHINOBU_242" status="PENDING_VERIFICATION">
  <DROPLET_DTO name="ErosionDropletDTO" size="32" layout="explicit">
    <FIELD name="Position" offset="0" size="8" />
    <FIELD name="Direction" offset="8" size="8" />
    <FIELD name="Velocity" offset="16" size="4" />
    <FIELD name="WaterVolume" offset="20" size="4" />
    <FIELD name="SedimentCapacity" offset="24" size="4" />
    <FIELD name="_pad0" offset="28" size="4" />
  </DROPLET_DTO>
  <ARRAY_FORMAT height="float32 little-endian normalized height" silt="float32 little-endian normalized silt mask" macro="float32 little-endian macro erosion" headerBytes="160" rollbackExcluded="true" />
  <EDITOR_TOOLING forgeWindow="Hydraulic Erosion Forge" csv="Assets/_Project/Data/Terrain/terrain_weathering_profiles.csv" scanner="Terrain_Runtime_Scanner_Erosion" selfAudit="HydraulicErosionForgeSelfAudit" />
  <REALTIME_EROSION runtimeExecution="excluded" note="Droplet simulation lives under Editor assembly and writes immutable .h8bin payloads." />
  <LIMITATION compile="blocked_by_cpu_policy" unityExecution="not_run" />
</SELF_AUDIT>

## Loop 11 Zero-Droplet Scheduling Guard
What was wrong:
- `ScheduleCore` scheduled `InitializeErosionDropletsJob` with `math.max(0, settings.DropletCount)`. A legal zero-droplet diagnostic bake therefore depended on Unity accepting `IJobParallelFor.Schedule(0, ...)`.

What was done:
- Added `dropletInitCount = math.clamp(settings.DropletCount, 0, droplets.IsCreated ? droplets.Length : 0)`.
- Skipped only the droplet initialization job when `dropletInitCount == 0`.
- Left `GenerateMockHeightmapJob` and `SimulateHydraulicErosionJob` dependency order intact; simulation clamps its own droplet loop to zero and no-ops.

Cinematic Cheats used:
- None. This is scheduler hardening for the offline baker. The existing Dear Lie remains the baked silt mask sidecar consumed by shader material blending instead of runtime sediment simulation.

Exact Microseconds saved:
- Runtime: 0 us, editor-only path.
- Zero-rain diagnostic bake: one cold job schedule call removed; exact microseconds pending Unity profiler because build/import is blocked by CPU policy.

Verification:
- `CURRENT_BATCH.md` SHINOBU_242 XML re-extracted by CLI: 65 lines / 16175 chars.
- Forbidden-pattern `rg` scan: clean.
- Async `Awaitable` signature scan: clean.
- `git diff --check` on `HydraulicErosionForgeBaker.cs`: clean.
- Compile: not launched. CPU policy check reported 100 percent load and no dotnet/csc process was active.

## Loop 12 Seam Queue Prewarm Pass
What was wrong:
- Seam queues were registered with an expected byte count, but `NativeQueue` storage itself was not prewarmed. A bake with heavy border flow could therefore allocate/grow queue blocks inside `SimulateHydraulicErosionJob` during `Enqueue`.

What was done:
- Added cold `PrewarmQueue<T>` to `HydraulicErosionForgeBaker`.
- `NewTrackedQueue` now enqueues and drains `expectedCapacity` default rows before `NativeMemorySentinel.RegisterNativeQueue`.
- Pattern matches existing project queue prewarm practice in bootstrap and procedural tooling.
- Updated `HYDRAULIC_EROSION_BAKER_SHINOBU_242.md`, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, and `SHINOBU_242_SELF_AUDIT.xml` with the seam queue memory contract.

Cinematic Cheats used:
- None added. This is allocator discipline for the existing seam-sidecar route.

Exact Microseconds saved:
- Runtime: 0 us; editor-only path.
- Editor bake: removes unpredictable queue expansion from the Burst seam-transfer phase. Exact microseconds require Unity profiler; compile/import remains blocked by CPU policy.

Verification:
- Forbidden-pattern `rg` scan: clean.
- Async `Awaitable` signature scan: clean.
- `git diff --check` on `HydraulicErosionForgeBaker.cs`: clean.
- Compile: not launched. CPU policy check reported 100 percent load and no dotnet/csc process was active.

## Loop 13 Designer Baseline Consistency Pass
What was wrong:
- `ScheduleCore` supported zero droplets, but the Forge UI slider minimum was still 1000. The diagnostic baseline was therefore not reachable from the requested human-control facade.

What was done:
- Changed `HydraulicErosionForgeWindow` droplet slider lower bound from `1000` to `0`.
- Full bake default remains one million droplets.

Cinematic Cheats used:
- None added. This exposes a zero-rain diagnostic path for payload/header/scanner proof without mutating terrain with a forced droplet.

Exact Microseconds saved:
- Runtime: 0 us; editor-only path.
- Zero-baseline diagnostics skip droplet initialization and all droplet simulation loops; exact editor microseconds pending Unity profiler.

Verification:
- `rg` confirmed `SliderInt("Droplet Count", 0, DefaultDropletCount)`.
- Forbidden-pattern `rg` scan: clean.
- `git diff --check` on `HydraulicErosionForgeWindow.cs`: clean.
- Compile: not launched. CPU policy check reported 100 percent load and no dotnet/csc process was active.

## Loop 14 Numeric Tuning Facade Pass
What was wrong:
- The Forge window had sliders but no numeric input fields. That blocks exact reproduction of CSV/profile values and causes avoidable rebake variance.

What was done:
- Enabled `showInputField = true` for droplet count, rain rate, evaporation speed, sediment capacity, erosion aggressiveness, and `GlobalQualityWeight`.

Cinematic Cheats used:
- None added. This is human-control plumbing for the offline baker.

Exact Microseconds saved:
- Runtime: 0 us; editor-only path.
- Editor: prevents avoidable rebakes caused by imprecise slider-only tuning; exact time saved is workflow-dependent and not claimed as a measured profiler number.

Verification:
- `rg` confirmed six `showInputField` bindings.
- Forbidden-pattern `rg` scan: clean.
- Async `Awaitable` signature scan: clean.
- `git diff --check` on `HydraulicErosionForgeWindow.cs`: clean.
- Compile: not launched. CPU policy check reported 100 percent load and no dotnet/csc process was active.

## Loop 15 Static Compile-Risk Gate
What was wrong:
- Build/import proof is still blocked by CPU policy, so remaining risk must be reduced through targeted source checks instead of fake compile claims.

What was done:
- Scanned all SHINOBU_242 `[BurstCompile]` attributes. All seven jobs carry `CompileSynchronously = true`, `FloatMode.Fast`, and `FloatPrecision.Standard`.
- Cross-checked project usage of `async Awaitable`, `Awaitable.BackgroundThreadAsync`, Span-based `FileStream` reads/writes, UI Toolkit `showInputField`, `NativeQueue` prewarm, and `[NoAlias]` patterns.
- Closed the compile-risk sub-agent after two waits returned no result; no background sub-agent remains active.

Cinematic Cheats used:
- None added in this loop. Existing Dear Lie remains the baked silt shader mask and macro map instead of runtime sediment physics.

Exact Microseconds saved:
- Runtime: 0 us; editor-only source/static gate.
- Compile-risk reduction has no measured microsecond value without Unity import/profiler.

Verification:
- Burst directive scan: clean.
- Forbidden-pattern `rg` scan: clean.
- `SELF_AUDIT.xml` parse: clean.
- `git diff --check`: clean for SHINOBU_242 files; `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` reports only existing LF-to-CRLF warning.
- Compile: not launched. CPU policy check reported 100 percent load and no dotnet/csc process was active.

## 2026-05-21 Loop 9 Forensic Hardening

What was wrong:
- `NativeMemorySentinel` coalesces pointerless queues by `(owner,label)`. Preview and bake queues used the same labels, so concurrent editor use could unregister the wrong queue record.
- Empty seam directions skipped file output. A prior non-empty `.h8seam` could remain on disk and poison the next importer/bake validation.
- Height/seam headers had no explicit endian marker. The payloads are little-endian, but the binary contract did not prove it.
- Payload byte-count casts were unchecked.
- The new Unity script/folder assets lacked stable `.meta` files.

What was done:
- Split queue labels into `Preview.*` and `Bake.*` lanes.
- Rewrote seam output to always emit all four sidecars, including valid zero-count `HSEM` headers.
- Added `LittleEndianMarker=0x01020304` to `HHE2` and `HSEM` headers and self-audit validation.
- Added reversed-magic detection for defensive binary audit failures.
- Switched payload byte counts to checked arithmetic.
- Added stable `.meta` files for the HydraulicErosionForge folders and C# scripts.

Cinematic Cheats used:
- Empty seam sidecars are an explicit proof artifact, not a runtime repair system.
- The shader-facing Silt Mask remains the material Dear Lie; no runtime erosion or geology simulation was added.

Exact Microseconds saved:
- Runtime: 0 us added; all changes are editor/file/metadata proof.
- Future stale-seam cleanup: avoids false importer work; exact microseconds depend on future runtime owner.
- Empty seam write cost: four 160-byte headers per baked sector when no directional transfer exists.
- Build: still not launched. CPU gate reported `CPU_PERCENT=100`; no dotnet/csc process was active.

## 2026-05-21 Loop 10 Human-Control Scalability

What was wrong:
- `GlobalQualityWeight` was mathematically consumed by the erosion kernel but hidden from the Forge UI.
- Slider-driven preview was still button-centered, so tuning feedback did not match the "live preview" mandate tightly enough.

What was done:
- Added a `Global Quality Weight` slider to `Hydraulic Erosion Forge`.
- Routed the slider into both preview and full-bake `HydraulicErosionSettingsDTO`.
- Coalesced slider changes through `EditorApplication.delayCall` into one preview refresh, skipping preview while a full bake is active.
- Preview droplet count now honors lower designer-requested droplet counts while staying capped by `PreviewDropletCount`.

Cinematic Cheats used:
- The preview remains a reduced Burst patch, not a full 100km bake.
- Low-quality preview collapses math through the same continuous erosion curves used by the full bake.

Exact Microseconds saved:
- Runtime: 0 us; UI exists only under Editor.
- Editor preview spam avoided: one queued preview per change burst instead of one synchronous preview per slider event. Exact milliseconds pending Unity execution.
- Build: still not launched under CPU policy.

## 2026-05-21 Loop 7 Polish Pass - Seam/AUP/Memory Forensics

What was wrong:
- Directional seam queues existed but were consumed by the async writer path before queue disposal, which allowed `Allocator.TempJob` queues to live across an `await` boundary.
- Seam transfer state had no durable binary contract; queue-only evidence could not survive bake ordering, CI artifact checks, or editor restart.
- `GlobalQualityWeight` was stored in settings/header but did not materially scale the erosion kernel.
- AUP hashing used raw double bits, making deterministic rain placement sensitive to meaningless sub-millimeter residue.
- Native allocations were not registered with `NativeMemorySentinel`.
- Async methods carried `in` parameters, which is a C# compile-wall defect for async state machines.
- `Docs/Reports/SHINOBU_242_SELF_AUDIT.xml` was absent; Unity execution remains unrun, so the report now carries a static-source evidence label.

What was done:
- Added `ErosionSeamTransferFileHeaderDTO=160`, `HSEM` magic, and `.h8seam` sidecar output for directional droplet handoff.
- Split seam handling into synchronous `CaptureSeamTransfers` plus async persistent-scratch serialization; TempJob queues/droplets are disposed before file awaits.
- Added millimeter AUP quantization for FNV seed identity, payload headers, seam headers, and black-box telemetry.
- Changed droplet initialization to seed `Unity.Mathematics.Random` from the quantized FNV value.
- Wired continuous `GlobalQualityWeight` into interpolation, droplet lifetime, capacity, erosion scale, and erosion-kernel distribution.
- Registered all SHINOBU_242 native arrays/queues with `NativeMemorySentinel` and unregistered before disposal.
- Added dedicated `Hecton8.World.HydraulicErosionForge.Editor.asmdef`, Editor-only, with only `Hecton8.Core` and Unity Burst/Collections/Jobs/Mathematics references.
- Updated architecture route card and binary payload ledger with sidecar boundary, no Data Monolith section, no Vault BufferID, and no runtime authority claim.
- Refreshed `Docs/Reports/SHINOBU_242_SELF_AUDIT.xml` as `STATIC_SOURCE_NO_UNITY_IMPORT`.

Cinematic Cheats used:
- Silt remains a baked shader mask, not runtime sediment physics.
- Seam continuity is persisted as droplet-state sidecars, not a monolithic global RAM bake.
- Low-quality erosion math smoothly collapses toward cheaper nearest-style sampling and shorter droplet lifetimes instead of binary hardware switches.
- Runtime cost remains a shader/sample problem; the expensive `O(droplets*lifetime)` water erosion is paid once offline.

Exact Microseconds saved:
- Runtime droplet erosion: 0 us by construction; all kernels are Editor-only.
- Runtime seam repair: 0 us by construction; seam state is offline `.h8seam` data for future importer ownership.
- Runtime DataVault lookup: 0 us added; baker requests 0 Vault handles and does not use `TryGetLatestCreated`.
- Low-weight bake loop reduction: lifetime scales down to roughly 55 percent of max before clamp; exact editor microseconds pending Unity/Burst execution.
- Static gates: `git diff --check` passed for SHINOBU_242 paths; rg found no `Task`, `System.Threading.Tasks`, `async void`, `FloatMode.Deterministic`, auto-properties, `Pack=1`, `UnityEngine.Random`, `System.Random`, `Mathf`, `foreach`, TODO, or NotImplemented in SHINOBU_242 source.
- Build: not launched. CPU gate reported `CPU_PERCENT=100`; project rule forbids dotnet build above 50 percent CPU. No dotnet/csc process was active.

<SELF_AUDIT agent="SHINOBU_242" status="PENDING_VERIFICATION" evidence="STATIC_SOURCE_NO_UNITY_IMPORT">
  <TASK_RECONCILIATION total="20" pass="20" fail="0" />
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="ErosionDropletDTO" size="32" math="float2 8 + float2 8 + float 4 + float 4 + float 4 + uint 4 = 32" />
    <DTO name="ErosionBakeTelemetryEntry" size="64" falseSharing="one row per cache line" />
    <DTO name="ErosionHeightmapFileHeaderDTO" size="160" alignment="32-byte multiple" />
    <DTO name="ErosionSeamTransferFileHeaderDTO" size="160" alignment="32-byte multiple" />
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE below_0_3="nearest-biased sampling, shorter droplet lifetime, lower capacity, lower erosion spread" mid="smoothstep interpolation/capacity/lifetime ramp" ultra="bilinear sampling, longer lifetime, richer silt masks" />
  <H_PHI_VAULT_STATUS handles="0" reason="Editor-only sidecar baker; future runtime terrain streamer owns Vault import if assigned." />
  <POINTER_ALIASING noAlias="true" graph="GenerateMockHeightmapJob -> InitializeErosionDropletsJob -> SimulateHydraulicErosionJob -> ErosionMetricScanJob; macro/sanitize complete only at cold editor IO boundary." />
  <COMPILE_GUARD assembly="Hecton8.World.HydraulicErosionForge.Editor" siblingRuntimeRefs="0" />
  <DEAR_LIE before="Runtime erosion O(droplets*lifetime)" after="Offline bake O(droplets*lifetime) once; runtime samples height/silt payloads O(1)" />
  <LIMITATION compile="blocked_by_cpu_policy" unityImport="not_run" payloadBake="not_run" />
</SELF_AUDIT>

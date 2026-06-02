# Rationale_14GRP

Problem: `14GRP` has no XML block in active `CURRENT_BATCH.md`, but user assigned graphics scalability directly.
Solution: Treat direct prompt as ad-hoc directive, keep source edits inside graphics/rendering/VFX domain, and prove by static scans.
Rejected Alternatives: Do not impersonate neighboring agent prompts; do not edit archived batch files; do not inflate JSON reports.
Scalability potential: Low uses cheap visual fakes and no hot lookups; middle/high/ultra spend saved CPU/GPU budget on richer presentation paths.
Hardware Impact: Avoiding build spam and runtime lookup drift protects i3/MX350 CPU budget; measured gain PENDING VERIFICATION.

Problem: Graphics work risks fake readiness claims without profiler or Unity import evidence.
Solution: Separate static proof from runtime proof; final status remains PENDING VERIFICATION for profiler/build/player facts.
Rejected Alternatives: Do not claim 60 FPS, 0 B GC, SetPass, or VRAM values without captures.
Scalability potential: Static architecture fixes are tier-neutral and reduce drift before quality-specific tuning.
Hardware Impact: Static-only validation avoids consuming CPU while other agents may be compiling; gain is CPU contention avoidance, not runtime FPS proof.

Problem: `DynamicPointLightCullingDirector.GenerateMockLightCullingData()` referenced `sources` and `states` outside the scope where those NativeArray aliases were declared, creating a compile-time failure and hiding lock-lifetime intent.
Solution: Capture `seededCapacity` as a scalar while the mock seed guard is held, complete the cold seed job, release the guard in `finally`, then publish the source manifest using only scalar data.
Rejected Alternatives: Do not hold source manifest mutation inside the mock seed guard; do not keep NativeArray aliases alive past release; do not replace the subsystem with a broad graphics rewrite.
Scalability potential: Low tier gets the same cheap light DTO path without extra scene objects; middle/high/ultra keep capacity-correct culling payloads for richer light presentation.
Hardware Impact: Runtime delta is 0 us because the scalar already existed logically; compile blocker removed; i3/MX350 gains by avoiding failed import/build churn.

Problem: APEX verification demanded compile confidence while CPU load was above the project build throttle.
Solution: Use source-only checks while CPU was high; when CPU dropped to 33%, run exactly one runtime assembly compile gate: `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`.
Rejected Alternatives: Do not launch `dotnet build` while CPU samples are above 50%; do not spin repeated builds; do not claim Unity runtime proof without Editor import.
Scalability potential: Build-throttle compliance preserves shared machine responsiveness for parallel agents and prevents graphics work from becoming integration noise.
Hardware Impact: Runtime assembly compiled in 21.43 s with 0 errors / 0 warnings; editor/test assembly was skipped when CPU returned to 77%.

Problem: `VisorHUDController.LateFrameTick()` drained bios font swaps through `_queuedHudFont.material`, a hot presentation property lookup during visual sync.
Solution: Cache the target TMP font material alongside the queued font, refresh terminal/default font materials during cold lifecycle and SlowTick, and drain with `_queuedHudFontMaterial`.
Rejected Alternatives: Do not call TMP material access per drain; do not create a new material; do not move font swap back into simulation phase.
Scalability potential: Weak devices avoid swap-frame property churn; middle/high/ultra preserve staged HUD font transition and can spend budget on visor effects.
Hardware Impact: Expected gain is small and event-bound, roughly 0-5 us on swap frames; no steady-frame cost added.

Problem: `VisorHUDController` carried a legacy camera command-buffer scissor fallback (`AddCommandBuffer`/`RemoveCommandBuffer`) that contradicts the URP RenderGraph mandate even if the URP branch normally bypassed it.
Solution: Retire the camera command-buffer path and leave a zero-cost no-op shim; visor clipping/presentation must stay in the RenderGraph/URP-owned path.
Rejected Alternatives: Do not keep legacy built-in camera command buffers as a runtime fallback; do not replace it with `Graphics.Blit`; do not migrate unrelated GPU compute command buffers in the same patch.
Scalability potential: Weak devices avoid fallback command-buffer churn and hidden camera state; higher tiers keep the same RenderGraph visor presentation path.
Hardware Impact: Runtime steady-state gain is 0 us on URP path because it was already bypassed; architectural gain is removal of a forbidden fallback and reduced integration drift.

Problem: Static scan still found direct `Graphics.ExecuteCommandBuffer` owners in `GlobalShaderDispatcher` and `ParasiteSwarmGpuRuntime`.
Solution: Record as follow-up because those paths own global shader upload and compute dispatch; migration needs explicit RenderGraph/compute scheduling proof.
Rejected Alternatives: Do not blindly delete compute dispatch command buffers; do not claim the entire command-buffer surface is clean.
Scalability potential: Proper migration can improve graph visibility and GPU scheduling on middle/high/ultra without damaging low-tier fallback.
Hardware Impact: Pending verification; no runtime claim made.

Problem: `GlobalShaderDispatcher` used a persistent `CommandBuffer` only to set frame-wide shader globals in `LateFrameTick`, adding a command-buffer allocation/submit path with no compute, draw, or render-pass ownership.
Solution: Remove the static command buffer and publish vectors/floats/buffers/textures directly through `Shader.SetGlobal*` inside the existing `LateFrameTick` visual-sync owner. This matches the existing `HectonShaderGlobalDataVaultBridge.FlushFallbackVisualSync()` route and keeps simulation-to-presentation transfer as stack/scalar/NativeArray snapshots.
Rejected Alternatives: Do not force scalar shader globals into RenderGraph; do not touch `ParasiteSwarmGpuRuntime` compute command buffers without compute ownership proof; do not add a new global dispatcher abstraction.
Scalability potential: Low tier avoids one persistent command buffer and a submit for shader-global upload; middle/high/ultra keep identical shader data and can spend saved CPU/GPU scheduling slack on richer fog/caustic/visor effects.
Hardware Impact: Runtime assembly compiled cleanly after patch. Expected gain is small but steady on weak CPUs: one cold `CommandBuffer` allocation removed and one visual-sync command-buffer execute removed per global dispatch; exact microseconds need Unity profiler capture.

Problem: `ThermalDynamicResolutionAdapter.WriteTelemetry()` could detect non-finite DRS state while holding the telemetry DataVault write guard, then call `ResetInvalidScaleStateAndCommit()`, which mutates scale state through a separate guard. That created a nested write-lock path during a fault frame.
Solution: Return a bool from `WriteTelemetry()`, write only telemetry under the telemetry guard, release it in a strict `finally`, then call `DumpBlackBoxOnce()` and `ResetInvalidScaleStateAndCommit()` outside the guard. `RecoverInvalidScaleState()` keeps a direct reset fallback only when telemetry guard acquisition fails.
Rejected Alternatives: Do not hold telemetry lock while resetting scale state; do not remove the black-box dump; do not allocate a managed copy of the telemetry ring to avoid a lock unless profiler data proves file IO under one guard is the bottleneck.
Scalability potential: Low tier avoids a deadlock/stall vector during thermal or NaN recovery; middle tier preserves stable DRS convergence; high/ultra keep visual-overkill recovery because invalid scale state now collapses without cross-lock drift.
Hardware Impact: No steady-frame cost added. Expected gain is pathological only: eliminates an unbounded deadlock/stall route on weak i3/MX350-class CPUs during non-finite recovery; exact microseconds are not profiler-measured.

Problem: Latest runtime compile gate failed before C# source compilation with MSBuild target cycle `ResolveProjectReferences` involving generated `Unity.RenderPipelines.Universal.Runtime.csproj` and `MoreMountains.Tools.csproj`.
Solution: Treat this as generated project graph blocker, not a graphics C# compile error. A narrower `/p:BuildProjectReferences=false` pass was attempted only after CPU returned below throttle and also failed before C# compile on `_GetCopyToOutputDirectoryItemsFromTransitiveProjectReferences` in `MoreMountains.Tools.csproj`. The spawned MSBuild node-reuse process was stopped by PID after the failed pass.
Rejected Alternatives: Do not edit generated third-party/package csproj files outside graphics domain; do not run a third build after two project-graph failures; do not claim compile pass for the latest patch.
Scalability potential: Build discipline prevents graphics verification from starving parallel agents and keeps the machine usable for low-end performance work.
Hardware Impact: Normal pass consumed 36.39 s, narrow pass consumed 37.16 s, both stopped at project graph resolution; no C# diagnostics were produced for the DRS patch.

Problem: `FontStreamingManager` still had a transitive hot presentation path where `LateFrameTick()` could evaluate primary/bios font readiness by calling `LocalizedFontResolver.IsFontReady()` or queue swaps through `targetFont.material`.
Solution: Cache primary and BIOS TMP font materials in cold lifecycle/scene/language-change paths, then make `LateFrameTick()` read only cached `Material` references and main-texture readiness. Add scene-loaded refresh so rebuilt HUD canvases do not leave stale cold caches.
Rejected Alternatives: Do not poll TMP `.material` during visual sync; do not instantiate replacement materials; do not rebuild all HUD text on font readiness.
Scalability potential: Weak devices avoid swap-frame TMP property churn; middle devices keep stable localized HUD transitions; high/ultra retain the same presentation path and can spend budget on visor effects instead.
Hardware Impact: Estimated 0-5 us saved on rare font-swap frames; no steady-frame work added; profiler proof pending.

Problem: `VfxComputeParticleBudgetCatalog.ApplyKillSwitchCount()` returned zero for bubble/debris pools whenever `NonCriticalVfxMask` was active, causing binary ambience loss exactly when the scalability pillar requires graceful continuous degradation.
Solution: Replace hard zero with pressure-smoothed survival counts: `125 permille` emergency multiplier, `32` bubble floor, `8` debris floor, clamped to active count. Legacy `byte.MaxValue` overload maps to full emergency pressure instead of binary zero.
Rejected Alternatives: Do not keep a low/high binary kill switch; do not allocate per-pool policy data; do not touch unrelated compute dispatch ownership in this patch.
Scalability potential: Low tier keeps cheap sparse bubbles/debris for depth and motion; middle tier compresses gracefully under pressure; high/ultra still receive full counts when pressure mask is clear.
Hardware Impact: Low-end i3/MX350 cost is bounded by 12.5% of active non-critical pools under emergency, not full pool; exact GPU us requires particle profiler capture.

Problem: `BiolumPulseSyncRuntime.DumpBlackBox()` acquired the black-box ring guard and then called `CopyBlackBoxDumpSnapshot()`, which acquired the dump scratch guard. That nested DataVault guard path could deadlock or stall during NaN/job-overrun fault handling.
Solution: Add a persistent owner-owned `NativeArray<BiolumPulseTelemetryEntry>[300]` snapshot. Copy ring entries under `BlackBoxGuardMask`, release in `finally`, then serialize the snapshot to scratch bytes under `BlackBoxDumpScratchGuardMask`. Dispose the snapshot on runtime disposal and require it during dump worker setup.
Rejected Alternatives: Do not use a managed array/list copy; do not hold both DataVault guards; do not remove the black-box dump requirement; do not write files from the fault frame directly.
Scalability potential: Low tier avoids an unbounded fault-frame lock stall while preserving crash evidence; middle/high/ultra keep full 300-frame telemetry dump and visual fault diagnostics.
Hardware Impact: Adds one cold 19.2 KB native snapshot allocation. Steady-frame cost is 0 us. Fault-frame work remains O(300) copy/serialize but no nested lock vector remains.

Problem: The latest verification still needs compile honesty without build spam.
Solution: Run one runtime build only after CPU was 36% and no `dotnet/MSBuild/csc` processes existed. It failed before C# compile on the same generated `ResolveProjectReferences` cycle in `Unity.RenderPipelines.Universal.Runtime.csproj` and `MoreMountains.Tools.csproj`; no project files were modified; leftover `dotnet` node-reuse process was stopped. Roslyn syntax parse was attempted from local `Assets/Plugins/Roslyn` assemblies, but Windows PowerShell could not initialize Roslyn (`Roslyn.Utilities.StringTable`).
Rejected Alternatives: Do not run repeated `dotnet build` loops; do not modify generated package/project graph from graphics domain; do not claim compile success when MSBuild never reached C# compile.
Scalability potential: Verification discipline prevents graphics work from consuming shared CPU and keeps integration fault attribution factual.
Hardware Impact: One 41.40 s build-wall attempt consumed CPU only under throttle-compliant conditions; no residual build processes left running.

Problem: `SuitHUDV4CanvasOverlay.ApplyAcousticRadarVisuals()` read `Image.material` in the visual-sync acoustic radar path before assigning the runtime radar material. That turns Graphic material ownership into a per-frame getter check and violates the hot material-access rule.
Solution: Add `_acousticRadarOverlayMaterialBound` as the owner-state proof. `ApplyAcousticRadarVisuals()` now calls `BindAcousticRadarOverlayMaterial()` and contains no `.material` token. The bind helper writes the material only when the cached binding flag is false; cleanup paths reset the flag explicitly.
Rejected Alternatives: Do not read `Graphic.material` every visual-sync frame; do not create or swap extra materials; do not push radar overlay state into a new subsystem for a single binding fault.
Scalability potential: Low tier avoids needless UI material getter traffic during the acoustic overlay; middle/high/ultra keep the same radar shader and can spend saved presentation slack on richer radar corruption/pulse parameters.
Hardware Impact: Expected gain is small and hot-path only, roughly 0-2 us on radar-active frames; exact value needs Unity profiler. Steady memory cost is one bool.

Problem: The latest patch needed compile discipline while another agent or process was already compiling.
Solution: Refused to launch `dotnet build` because `dotnet build Hecton8.slnx -maxcpucount:1 --no-restore...` was already active at PID 58308 and CPU was 48%. Used source proofs instead: diff check, brace parser, hot-token scan, and method-body source assertion.
Rejected Alternatives: Do not stop a build process not started by 14GRP; do not launch a second compiler; do not claim runtime compile proof for this HUD patch.
Scalability potential: Prevents verification from becoming a shared-machine frame-time/CPU contention problem.
Hardware Impact: No CPU build cost added by 14GRP in this loop; no orphan build process created.

Problem: `HectonMarineSnowRenderer.BuildContinuousScalabilityParams()` converted homeostasis advection/volumetric masks into binary `0f/1f` policy multipliers. Under pressure, marine snow could lose flow, SDF/depth collision, and depth response abruptly instead of degrading as a continuous visual fake.
Solution: Added `VfxComputeParticleBudgetCatalog.ResolvePolicyQualityWeight()` so policy masks compress feature intensity to a pressure-smoothed floor instead of killing it. `BuildContinuousScalabilityParams()` now routes flow, collision, and depth quality through this scalar. `ResolvePolicyFlowResampleFrames()` keeps masked flow on a sparse non-zero cadence up to 16 frames.
Rejected Alternatives: Do not rewrite the compute shader dispatch path; do not leave `mask ? 0f : 1f`; do not use a full flow cadence during emergency pressure; do not allocate policy tables.
Scalability potential: Low tier keeps sparse drift and cheap depth belief, middle tier degrades cadence and quality smoothly, high/ultra retain full flow and richer fake occlusion when pressure clears.
Hardware Impact: Managed GC remains 0 B; added scalar ALU only during budget refresh, not per-particle CPU work. MX350/i3 impact is expected to be below measurement noise on CPU while removing visible VFX popping under thermal/VRAM pressure. Profiler proof pending.

Problem: Verification after the marine snow policy patch could not use the compiler without violating the throttle.
Solution: Used `git diff --check`, brace-depth parsing, scoped source-shape proof, and hot-token scan. Refused build because CPU sampled at 99.1%, above the 50% gate.
Rejected Alternatives: Do not run `dotnet build` into a saturated host; do not claim compile success; do not edit generated project graph.
Scalability potential: Keeps shared integration machine usable while still proving the source-level invariants relevant to 14GRP.
Hardware Impact: No build CPU cost added by 14GRP in this loop; no orphan process created.

Problem: Marine snow shadow tap policy still contained a renderer-local hard cap: `VolumetricFogHighResMask` forced `shadowTaps` down to middle tier regardless of continuous quality weight.
Solution: Added `VfxComputeParticleBudgetCatalog.ResolvePolicyShadowTaps()` with pressure-smoothed compression and a minimum masked pressure floor, then routed `HectonMarineSnowRenderer.BuildContinuousPressureBudget()` and debug tap reporting through the shared resolver.
Rejected Alternatives: Do not keep renderer-local binary clamp; do not add a shader variant or keyword for pressure; do not rewrite the compute snow shader to solve a scalar budget problem.
Scalability potential: Low tier keeps cheap fake depth/fog belief at compressed tap count, middle tier transitions without popping, high/ultra retain overkill taps when pressure clears.
Hardware Impact: CPU cost is scalar ALU during budget refresh only, not per-particle CPU work. Expected MX350/i3 delta is below profiler noise; visual win is removal of abrupt fake-occlusion collapse.

Problem: `NativeTrailRenderer` rendered one generated trail mesh through `Graphics.DrawMeshInstanced` and retained a one-element matrix array, paying instancing ceremony for no instance fan-out.
Solution: Removed `DrawInstanceCount` and `_drawMatrices`, then changed `Render()` to `Graphics.DrawMesh(... Matrix4x4.identity ...)` with the same material, layer, camera, shadows, and probe settings.
Rejected Alternatives: Do not migrate this tiny generated mesh into BRG without a batching owner; do not keep single-instance instancing; do not add MaterialPropertyBlock.
Scalability potential: Weak devices avoid useless instancing payload for short-lived trails; middle/high/ultra keep the same visual trail while larger batching work remains a separate renderer-owner task.
Hardware Impact: Removes one cold `Matrix4x4[1]` managed array and one hot single-instance draw API path. Exact microseconds need Unity profiler; expected gain is small but deterministic.

Problem: Latest code batch needed compile honesty without build spam.
Solution: Ran static source checks first. Launched exactly one runtime assembly build only after CPU was 43.9% and no compiler process was active. MSBuild failed before C# compilation on the known generated project-reference cycle in `Unity.RenderPipelines.Universal.Runtime.csproj` and `MoreMountains.Tools.csproj`; residual `dotnet` node-reuse process was stopped by PID 11448.
Rejected Alternatives: Do not edit generated package/third-party project graph from graphics domain; do not retry the same failing build; do not claim compile success when C# compilation did not start.
Scalability potential: Verification remains bounded and does not starve parallel agents; source-level graphics invariants are still proven by static scans.
Hardware Impact: One 54.36 s wall-time build attempt under throttle gate; no residual compiler process left running.

Problem: `AbyssalFluidDecalManager` defaulted to `screenSpaceFluidDecals=true`, but source search found no `CopyScreenSpaceDecals()` consumer. Active aftermath decals could register, drift, and expire without ever being drawn.
Solution: Keep the intended screen-space route, but make it proof-driven: `CopyScreenSpaceDecals()` marks a consumer frame; `LateFrameTick()` uses the mesh fallback only when no active screen-space consumer has been seen within two frames. Pressure-spray matrix submission now uses `ResolvePressureSprayDrawLimit(capacity, GlobalQualityWeight, PressureLevel)` with an 18.75% survival floor.
Rejected Alternatives: Do not flip the serialized route blindly; do not delete the screen-space API; do not leave a silent visual sink; do not draw all spray ribbons during emergency pressure.
Scalability potential: Low devices keep visible leak/silt belief through sparse cheap quads; middle devices transition draw count continuously; high/ultra keep full spray density when pressure clears and a future RenderGraph collector can suppress fallback automatically.
Hardware Impact: No managed hot allocation added. Low-end i3/MX350 avoids worst-case full spray submission under pressure and restores visible feedback when RenderGraph collector is absent; exact microseconds need Unity profiler.

Problem: `PDADataLogTab.RenderSelectedLoreHologram()` rendered a single selected lore hologram through `Graphics.DrawMeshInstanced` and retained `Matrix4x4[1]`, paying instancing setup for one mesh.
Solution: Remove `_hologramMatrices` and submit the proxy through `Graphics.DrawMesh` with the same material, layer, shadows-off, and light-probe-off semantics.
Rejected Alternatives: Do not keep one-instance instancing; do not add MaterialPropertyBlock; do not convert this one proxy into a BRG/indirect path without batching ownership.
Scalability potential: Low devices avoid a pointless retained array and draw API overhead; middle/high/ultra keep the same hologram visual and can spend budget on shader detail rather than submission ceremony.
Hardware Impact: Removes one cold `Matrix4x4[1]` managed field and one single-instance instanced draw call. Expected runtime gain is small but deterministic; profiler proof pending.

Problem: Loop 9 needed compile discipline after two C# files changed.
Solution: Used static validation first: diff check, brace parser, source-shape assertions, and scoped hot-token scans. Build was refused because CPU sampled 61.3% and then 64.8% with no compiler processes, above the 50% gate.
Rejected Alternatives: Do not start `dotnet build` on a busy host; do not claim compile pass; do not alter generated project graph from graphics domain.
Scalability potential: Keeps shared machine throughput available while preserving source-level proof of phase and allocation invariants.
Hardware Impact: No compiler CPU cost added by 14GRP in this loop; no orphan build process created.

Problem: `SuitHUDV4CanvasOverlay` kept stale scanner hologram mesh-era resources (`Matrix4x4[1]`, `MaterialPropertyBlock`, material, mesh) even though the active scanner hologram path is a flat canvas fake updated through rect/color fields.
Solution: Delete the unused runtime fields and the Awake `MaterialPropertyBlock` allocation; keep `EnsureScannerHologramRuntimeResources()` as a no-op proof that the path has no mesh/material/TRS payload.
Rejected Alternatives: Do not reintroduce mesh hologram rendering; do not keep dormant resources for a path that already renders through UI rectangles; do not create a new subsystem for one stale payload.
Scalability potential: Low devices avoid cold UI resource waste; middle/high/ultra keep the same scanner visual fake and can spend budget on shader/UI polish instead of unused mesh state.
Hardware Impact: Removes one cold `Matrix4x4[1]` managed allocation and one cold `MaterialPropertyBlock`; steady-frame runtime remains 0 us changed because the fields were stale.

Problem: `HectonFabricatorUI.RenderSelectedRecipeHologram()` drew exactly one selected recipe preview through `Graphics.DrawMeshInstanced` and a one-element matrix array while the real ingredient fan-out batch already has its own 16-matrix buffer.
Solution: Convert the selected preview `float4x4` into a stack `Matrix4x4` and submit it through `Graphics.DrawMesh`; keep `_hologramMatrixBuffer` and `DrawMeshInstanced` in `RenderActiveRecipeHologram()` for actual multi-instance ingredient cells.
Rejected Alternatives: Do not remove real batching; do not add BRG/indirect rendering for one preview mesh; do not allocate a new MaterialPropertyBlock.
Scalability potential: Low devices avoid one pointless retained array and single-instance instancing ceremony; middle/high/ultra keep selected preview fidelity and ingredient batch density.
Hardware Impact: Removes one cold `Matrix4x4[1]` field and a single-instance instanced draw call from the selected preview path. Exact microseconds require Unity profiler; expected gain is small but deterministic.

Problem: Loop 10 needed compile honesty after UI presentation edits.
Solution: Ran source-only proof first: diff check, brace parser, scoped source assertions, and hot-token scan. Refused build because CPU sampled 66.1%, 93.0%, and 76.8%, above the 50% gate.
Rejected Alternatives: Do not launch `dotnet build` into a saturated host; do not claim compile success; do not stop foreign compiler processes.
Scalability potential: Keeps verification bounded while preserving 14GRP source invariants under parallel-agent load.
Hardware Impact: No compiler CPU cost added by 14GRP in this loop; no orphan process created.

Problem: `SuitHUDV4CanvasOverlay.EnsureSavingProgressPulseRuntimeResources()` used `Image.material` equality reads to decide whether the DATA save lamp/needle had the pulse material bound. Even if called from lifecycle/build paths, that makes the UI Graphic material getter the state oracle and can drift into presentation refresh paths.
Solution: Add `_savingProgressDataLampPulseMaterialBound` and `_savingProgressDataNeedlePulseMaterialBound`, route binding through `BindSavingProgressPulseMaterials()`, and release/reset through explicit flags. `BuildSavingProgressHierarchy()` and `InvalidateVisualCaches()` reset the flags before new Image refs are used.
Rejected Alternatives: Do not keep `.material ==` or `.material !=` checks; do not create duplicate pulse materials; do not replace the save indicator with a new subsystem for a material-state bug.
Scalability potential: Low devices avoid Graphic material getter traffic during HUD rebuild/save indicator lifecycle; middle/high/ultra keep the same shader-time DATA pulse and can spend budget on actual visual polish.
Hardware Impact: Expected runtime gain is small and lifecycle-bound, roughly 0-2 us on save-indicator setup/dispose frames. Main win is deterministic owner-state material binding and lower drift risk.

Problem: Loop 11 had CPU headroom but the project has a known generated MSBuild graph failure that stops before C# compile, and the user requested compilation throttling plus static source validation rather than build spam.
Solution: Ran source-only validation: diff check, brace parser, method source assertions, and scoped hot-token scan. Did not launch `dotnet build` despite CPU samples 30.7%, 22.7%, 37.5% because the known project graph failure would consume CPU without reaching C# compilation for this UI-only patch.
Rejected Alternatives: Do not rerun a known pre-C# project graph failure for every small UI source patch; do not claim compile success; do not mutate generated package csproj files from graphics domain.
Scalability potential: Keeps integration machine available to parallel agents while preserving 14GRP source invariants.
Hardware Impact: No compiler CPU cost added by 14GRP in this loop; no orphan process created.

Problem: `SuitHUDV4CanvasOverlay.CreateText()` read `label.font.material` for every generated TMP label during HUD hierarchy rebuild. This is not a steady-frame hot path, but it repeats a TMP material getter across many labels and creates another material-state route outside owner control.
Solution: Add a two-slot owner cache keyed by `TMP_FontAsset`. `CreateText()` resolves a local font, assigns it once, and calls `ResolveFontSharedMaterial(resolvedFont)`. `InvalidateVisualCaches()` clears the cache when HUD layout/config is rebuilt.
Rejected Alternatives: Do not create font material clones; do not add a dictionary or allocation-heavy cache; do not push font ownership into a new service for a local HUD factory.
Scalability potential: Low devices avoid repeated TMP material getter work during canvas rebuilds; middle/high/ultra keep exact text visuals and can spend budget on actual visor/HUD effects.
Hardware Impact: Expected saving is cold rebuild-only and small; no steady-frame CPU or GC added. The cache is four references.

Problem: Loop 12 validation had an active compiler process and saturated CPU.
Solution: Used source-only validation and refused to launch build because foreign `dotnet` PID 44748 was active and CPU sampled 67.7%, 100%, 100%.
Rejected Alternatives: Do not start a second build; do not stop another agent's compiler process; do not claim compile success.
Scalability potential: Preserves shared machine throughput and avoids turning graphics verification into CPU contention.
Hardware Impact: No compiler CPU cost added by 14GRP in this loop; no orphan process created.

Problem: `SuitHUDV4CanvasOverlay.ApplyDitheredBackgroundMaterial()` read `image.material` to compare against the runtime dithered backdrop material before binding it. This is a cold hierarchy/factory route, but it still made Unity `Graphic.material` the owner-state source and left another material getter path in the HUD presentation system.
Solution: Remove the getter comparison and bind `_ditheredUiBackgroundMaterial` directly after cold material ensure. The route remains a deterministic shader fake: alpha-clipped dithered UI backdrop, no mesh simulation, no new allocation in frame refresh.
Rejected Alternatives: Do not add per-image dictionaries or binding maps; do not instantiate duplicate materials; do not leave `image.material !=` as a state oracle.
Scalability potential: Low devices avoid repeated Graphic material getter checks during HUD rebuild; middle/high/ultra keep the same dithered backdrop effect and can spend budget on visible visor/HUD shader detail.
Hardware Impact: Expected gain is cold-path and small, roughly 0-1 us per dithered backdrop bind. Main value is deterministic material ownership and lower drift risk; profiler proof pending.

Problem: Loop 13 validation was blocked by host saturation and a foreign compiler process.
Solution: Ran source-only gates: diff check, comment-aware brace parser, targeted dithered-material source proof, and hot-token scan. Refused `dotnet build` because CPU sampled 99.4%, 99.4%, 97.3% and foreign `dotnet` PID 65920 was active.
Rejected Alternatives: Do not launch a second build into saturated CPU; do not stop another agent's compiler; do not claim compile success.
Scalability potential: Keeps graphics verification from starving shared integration CPU under parallel-agent load.
Hardware Impact: No compiler CPU cost added by 14GRP in this loop; no orphan process created.

Problem: `FakeRadarBlipController` and `AcousticRadarSphereRenderer` treated non-finite `HomeostasisBrain.GlobalQualityWeight` as `1f` inside `SmoothStep01()`. A NaN/Inf quality signal therefore resolved radar presentation to maximum fake blip/matrix load instead of fail-safe survival capacity.
Solution: Add `SanitizeQualityWeight01()` with non-finite fallback `0f` and route blip/matrix capacity through `ResolveQualityCapacity()`. This preserves continuous quality scaling for valid values and collapses invalid values to minimum visual load.
Rejected Alternatives: Do not keep `? value : 1f`; do not hard-disable radar visuals on invalid quality; do not move quality ownership into another global route.
Scalability potential: Low and faulted devices retain minimum readable radar cues; middle/high/ultra still scale continuously to full blip density when quality is valid.
Hardware Impact: Under invalid quality, fake hostile radar caps at 16 blips instead of 64 and thermal ghost blips at 0 instead of 8; acoustic voxel radar caps at 16 matrices instead of 64. Steady valid-quality CPU cost is unchanged except one small helper call.

Problem: Loop 14 had CPU below the numeric threshold but the generated project graph has an unresolved pre-C# MSBuild failure already proven in prior loops.
Solution: Ran source-only gates for touched files and refused another `dotnet build` because it would repeat a known generated graph failure without reaching C# compile. CPU samples were 45.3%, 38.4%, 38.9%; no compiler process was listed.
Rejected Alternatives: Do not spam a known failing project graph; do not claim compile success; do not mutate generated package csproj files from graphics domain.
Scalability potential: Keeps parallel-agent CPU available and attributes build blockers to the generated graph, not graphics source.
Hardware Impact: No compiler CPU cost added by 14GRP in this loop; no orphan process created.

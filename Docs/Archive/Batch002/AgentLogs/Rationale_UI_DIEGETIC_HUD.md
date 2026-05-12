# Rationale_UI_DIEGETIC_HUD

Status: PENDING VERIFICATION
Prompt: `UI_DIEGETIC_HUD`
Domain: `PRESENTATION & UX / Visor AR`

## Mandate Intake

Problem: Existing HUD prompt targets Canvas bloat, text allocations, and non-diegetic overlay behavior.
Solution: Use `Span<char>`/`TryFormat` over persistent `char[]`, TMP `SetCharArray`, physical mesh projection, shader-side visual cheats, and analytical hit projection.
Rejected Alternatives: Screen Space Overlay Canvas, `TMP_Text.text`, `string.Format`, coroutine flicker, `Physics.Raycast` for terminal cursor projection, Unity LayoutGroups for runtime HUD.
Scalability potential: Low uses low vertex count, half math, stencil rejection, no texture-heavy passes. Middle raises visor mesh resolution and shader detail. High adds stronger chromatic offsets and damage distortion. Ultra spends saved CPU/GPU on richer scanlines, dirt, edge aberration, and denser curved geometry.
Hardware Impact: i3/MX350 avoids canvas rebuild spikes, avoids string GC, avoids raycaster walks, and rejects visor-edge fragments before shader work. Estimated gains remain PENDING VERIFICATION until Unity profiler and Frame Debugger evidence exists.

## Pass 1 Decisions

Problem: HUD text needed `Span<char>` ingress while existing systems still expose TMP objects.
Solution: Added `DiegeticHudTextNode` as a strict TMP wrapper: caller supplies `ReadOnlySpan<char>`, wrapper copies into a persistent cold `char[]`, hashes the payload, and commits through `SetCharArray()` only when dirty.
Rejected Alternatives: Direct `TMP_Text.text`, `StringBuilder`, and pooled strings were rejected because they create GC or hidden TMP parsing churn under metric spam.
Scalability potential: Low keeps a 256-char static lane and skips duplicate commits. Middle can raise per-node capacity for localized diagnostics. High/Ultra can route more visor labels through the same API without creating per-frame strings.
Hardware Impact: i3/MX350 estimate is 11 us saved during dense metric refresh and removal of text-update GC risk. Pending profiler verification.

Problem: Formatting helpers existed but did not expose the explicit fast append API required by the prompt, and `FixedCharBuffer.AppendFloat` retained a format string branch.
Solution: Added `ZeroGCFormatter.FastIntToChars` and `FastFloatToChars` with caller-owned span cursor mutation, then routed `FixedCharBuffer` through those APIs.
Rejected Alternatives: `string.Format`, interpolated strings, `ToString`, and per-call format-string selection were rejected because the HUD path cannot tolerate managed allocations or formatter boxing.
Scalability potential: Low uses integer and fixed precision float paths. Middle/High can add template wrappers without changing callers. Ultra can swap in Burst-compatible integer-only telemetry glyph emission.
Hardware Impact: i3/MX350 estimate is 4 us saved per formatted float burst and no garbage collector wakeup from HUD metrics. Pending profiler verification.

Problem: The prompt requires the player HUD to stop being a screen-space overlay and exist as a physical helmet surface.
Solution: Added `DiegeticVisorHudMesh`, a `MeshFilter`/`MeshRenderer` owner that resolves `GlobalRegistry.Player.PlayerCamera`, parents itself to that camera, and builds a curved mesh once on enable.
Rejected Alternatives: Screen Space Overlay Canvas, camera-space Canvas, and GraphicRaycaster were rejected because they preserve canvas rebuild cost and non-diegetic composition.
Scalability potential: Low uses 24x10 segments. Middle raises mesh segments. High adds denser geometry. Ultra spends saved canvas CPU on richer shader failure states.
Hardware Impact: i3/MX350 estimate is 60 us saved during HUD pose/rebuild spikes compared with canvas layout and raycaster overhead. Pending profiler verification.

Problem: HUD pixels must not bleed past the helmet glass.
Solution: Created `Hecton8/UI/DiegeticVisorCurvedHUD` with `Stencil Comp Equal` and runtime `_StencilRef` binding from `DiegeticVisorHudMesh`.
Rejected Alternatives: UV edge fade and alpha-only clip were rejected because they still execute fragment work outside the glass mask.
Scalability potential: Low rejects visor-edge fragments before heavy work. Middle adds mild chromatic offsets. High/Ultra can increase dirt and glitch layers because stencil keeps the shaded region bounded.
Hardware Impact: i3/MX350 estimate is 35 us fragment work avoided near helmet edges in dense HUD scenes. Pending Frame Debugger verification.

Problem: Curved UI projection needs cheap deterministic geometry without exact tangent calls.
Solution: Added `RationalTan()` Padé-style approximation and used it for mesh vertices plus analytical visor-plane cursor projection.
Rejected Alternatives: Exact `Mathf.Tan`, spline projection, and physics-driven hit tests were rejected because predictable cheap math is enough for visor placement.
Scalability potential: Low uses the approximation with sparse mesh. Middle/High increase segment count. Ultra spends the same projection contract on stronger edge warping.
Hardware Impact: i3/MX350 estimate is 2 us saved per mesh/projection burst. Pending profiler verification.

Problem: Compile verification was required after tasks 1-5, but the project has existing non-UI compile errors.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` and separately ran Unity `validate_script` on all touched C# files. Script validation returned zero diagnostics.
Rejected Alternatives: Editing `HectonArenaAllocator`, `TetherInstance`, or `AbyssalThermalManager` was rejected because they are outside the assigned Presentation & UX domain and not introduced by this pass.
Scalability potential: Build health remains blocked by other domains; UI chunk remains isolated and ready for the next compile gate once dependencies are repaired.
Hardware Impact: No runtime impact. Verification state remains PENDING VERIFICATION because full project compile is blocked externally.

## Pass 2 Decisions

Problem: Chromatic aberration was required for the visor but a full-screen post-process would tax low hardware and affect non-HUD pixels.
Solution: Added edge-weighted RGB channel sampling in `Hecton8/UI/DiegeticVisorCurvedHUD`, bounded by the visor mesh and stencil mask.
Rejected Alternatives: Full-screen URP renderer feature and CPU-side vertex/color offsets were rejected because they spend work outside the HUD and complicate ordering.
Scalability potential: Low keeps a small channel offset. Middle raises `_ChromaticStrength`. High/Ultra can add stronger edge chroma because work is local to HUD pixels.
Hardware Impact: i3/MX350 estimate is 18 us saved versus a post-process pass. Pending shader import and GPU profiler verification.

Problem: Brownout must feel like failing electronics without a CPU-side flicker loop or queue race.
Solution: Shader multiplies emission by `_PanelPowerLevel` and global `_HectonVRBrownoutIntensity`, using a cheap triangle flicker. `DiegeticVisorHudMesh.ApplyBrownoutSignal()` accepts direct `BrownoutSignal` packets without draining `GlobalSignals.TryDequeueBrownout`, leaving the existing `VisorHUDController` consumer authoritative.
Rejected Alternatives: Draining the global brownout queue locally was rejected because it would steal packets from another system. Coroutine flicker and material swapping were rejected as avoidable CPU churn.
Scalability potential: Low uses scalar flicker only. Middle adds material power smoothing. High/Ultra can layer richer failure noise in shader.
Hardware Impact: i3/MX350 estimate is 7 us saved by avoiding CPU animation and text/material rebuilds. Pending profiler verification.

Problem: Helmet damage glitch must react to low health while avoiding mesh rebuilds.
Solution: `DiegeticVisorHudMesh` now receives canonical `IDamageReceiver` packets, player trauma packets, and explicit `DamageSignal` adapters, then drives `_DamageGlitch` for shader-side sine-wave vertex tearing when health/integrity is below 30%.
Rejected Alternatives: Canvas shake, rebuilding mesh vertices, and coroutine glitch timers were rejected because the effect is a visual fake that belongs in the vertex shader.
Scalability potential: Low uses sparse tear bands. Middle raises amplitude. High/Ultra can add scanline gaps and per-band offsets.
Hardware Impact: i3/MX350 estimate is 14 us saved per hit burst versus CPU vertex rewrite. Pending profiler verification.

Problem: Cursor targeting against a curved visor/terminal surface must not use physics raycasts.
Solution: Added `TryProjectRayToVisor()` and `TryProjectViewportPoint()` analytical local-plane intersection using rational extents. This supports center-dot and mouse projection with no `Physics.Raycast`.
Rejected Alternatives: `Physics.Raycast`, GraphicRaycaster, and colliders on visor UI were rejected because they add broadphase/UI traversal cost for a deterministic plane hit.
Scalability potential: Low uses one plane intersection. Middle can add clipped curved correction. High/Ultra can add local parallax offsets without touching physics.
Hardware Impact: i3/MX350 estimate is 25 us saved per cursor query. Pending profiler verification.

Problem: O2 text must not update every frame.
Solution: `DiegeticHudTextNode.SetOxygenPercent()` stores `_lastOxygenPercent`, skips unchanged writes, and formats only integer percent into the persistent buffer.
Rejected Alternatives: Float oxygen formatting and per-frame `SetCharArray()` were rejected because visual fidelity does not improve with sub-integer HUD spam.
Scalability potential: Low updates only on integer changes. Middle can batch several vitals through one node. High/Ultra can add animated gauge mesh while text remains dirty-gated.
Hardware Impact: i3/MX350 estimate is 9 us saved per unchanged HUD frame. Pending profiler verification.

Problem: Compile verification after tasks 6-10 still failed outside the UI domain.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`; failure remains in survival/core/world dependencies. Script validation and forbidden-pattern scans passed for touched files.
Rejected Alternatives: Editing survival/core/world compile blockers was rejected as domain drift and potential architectural sabotage.
Scalability potential: UI pass remains decoupled and can be verified when the shared compile wall is removed.
Hardware Impact: No runtime impact. Full compile remains PENDING VERIFICATION.

## Pass 3 Decisions

Problem: The visor cannot use runtime Unity layout components.
Solution: Added `DiegeticHudManualLayout`, which collects diegetic child transforms and runs `DiegeticHudLayoutJob` as a Burst `IJobParallelFor` to compute deterministic local offsets.
Rejected Alternatives: Managed layout components, rect rebuilds, and per-child hand-authored scripts were rejected because they introduce rebuild cost and inconsistent authoring drift.
Scalability potential: Low computes sparse rows. Middle handles larger PDA clusters. High/Ultra can feed denser transform lanes while the job remains simple.
Hardware Impact: i3/MX350 estimate is 22 us saved per layout refresh versus layout rebuilds. Pending profiler verification.

Problem: PDA close focus requires a raycast but must not become an unbounded focus polling system.
Solution: Added `DiegeticPdaFocusDistanceController`, which registers on the UI tick lane and performs at most one `Physics.RaycastNonAlloc` per frame only while focus is armed, then writes URP `DepthOfField.focusDistance`.
Rejected Alternatives: One raycast per widget, unbounded focus probes, and per-frame component/profile resolution were rejected because they waste CPU for a single close-focus decision.
Scalability potential: Low performs one 1-slot non-alloc ray. Middle blends focus faster. High/Ultra can add optical parameter changes while keeping one probe.
Hardware Impact: i3/MX350 estimate is 16 us saved versus repeated PDA focus probes. Pending profiler verification.

Problem: Humidity needs dirty visor glass without another pass or CPU texture compositing.
Solution: `Hecton8/UI/DiegeticVisorCurvedHUD` blends `_DirtTex` by `_Humidity01`, and `DiegeticVisorHudMesh` samples `BaseAtmosphereEngine` humidity on a slow interval or accepts `ApplyAtmosphereHumidity(byte)`.
Rejected Alternatives: Runtime texture writes, particle/decal overlay pass, and canvas dirt overlay were rejected because the effect is a cheap shader blend.
Scalability potential: Low uses one dirt sample. Middle raises dirt strength. High/Ultra can layer scratch masks and chroma without changing CPU cost materially.
Hardware Impact: i3/MX350 estimate is 24 us saved versus extra UI/decal pass. Pending GPU profiler verification.

Problem: Recon evidence had to live on disk and cover UI script violations.
Solution: Wrote `Docs/AgentLogs/RECON_UI_DIEGETIC_HUD.md` with scan commands and results for forced canvas updates, layout rebuilds, direct text assignment, and layout component names.
Rejected Alternatives: Chat-only recon and broad edits to tooltip/PDA/localization were rejected because the assignment requires evidence logging and these violations predate this pass.
Scalability potential: No runtime path. Recon provides integrator evidence for a later cleanup pass.
Hardware Impact: No runtime impact.

Problem: Omega compile check cannot pass because unrelated domains still fail after three attempts.
Solution: Ran a third `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`. Marked task 15 `[BLOCKED BY DEPENDENCY]`.
Rejected Alternatives: Editing survival, tether, core allocator, or transport-domain files was rejected because it violates the assigned Presentation & UX boundary and would risk cross-agent overwrite.
Scalability potential: UI work remains isolated behind registry/event interfaces and can compile once those external contracts are repaired.
Hardware Impact: No runtime claim. Status remains PENDING VERIFICATION.

## OMEGA POLISH CHANGES

Problem: Fixed authoring segment counts created a middle-ground visor mesh instead of a scalability matrix.
Solution: `DiegeticVisorHudMesh` now resolves segment counts from `GlobalRegistry.ScalabilityTier`: Low halves, MX350 uses 75%, High increases 150%, Ultra clamps to max.
Rejected Alternatives: One-size mesh density and per-frame rebuild were rejected. Segment LOD is resolved during cold rebuild.
Scalability potential: Low is toaster-safe sparse geometry. Middle/MX350 keeps shape readable. High/Ultra buys visual overkill through denser curved glass projection.
Hardware Impact: i3/MX350 reduces visor vertex count from authored 24x10 to 18x7 under MX350 tier. Estimated cold rebuild and vertex transform saving: 20 us during rebuild, pending profiler verification.

Problem: Remaining projection math used raw floating division.
Solution: Replaced visor projection divisions and rational tangent final divide with `math.rcp()` multiplication; mesh UV step now uses precomputed reciprocals.
Rejected Alternatives: Exact division in hot projection and exact tangent were rejected under frame-time dictatorship.
Scalability potential: Same visual result, cheaper scalar math across tiers.
Hardware Impact: i3/MX350 estimate is 1-2 us saved during cursor projection bursts. Pending profiler verification.

Problem: Burst layout job had a branch on layout axis.
Solution: Replaced branch with `axisMask = Settings.Axis & 1` and branchless horizontal/vertical lane selection.
Rejected Alternatives: Boolean branch in Burst job was rejected by the polish mandate.
Scalability potential: Low/High use same deterministic job, no managed layout components.
Hardware Impact: i3/MX350 estimate is sub-us per layout element but removes branch divergence debt. Pending profiler verification.

Problem: PDA focus blend used reciprocal-shaped math with explicit division.
Solution: Replaced `1f / denominator` with `math.rcp(denominator)`.
Rejected Alternatives: Raw division in per-frame focus path was rejected.
Scalability potential: Same one-raycast policy, cheaper blend math.
Hardware Impact: i3/MX350 estimate is sub-us per armed focus frame. Pending profiler verification.

Problem: Final polish required another build and diff evidence, but the project compile remains blocked externally.
Solution: Ran post-polish `validate_script` on modified UI scripts, ran forbidden-pattern scans, ran `git diff --check`, and ran final `dotnet build`.
Rejected Alternatives: Marking "VERIFIED MASTER GRADE" was rejected because `Hecton8.Core.csproj` still fails on `HectonSurvivalSystem.cs:298` missing `SurvivalPhysiologyScalarResult`; the prompt's required status remains PENDING VERIFICATION.
Scalability potential: UI work is isolated and ready for verification once survival-domain compile is repaired.
Hardware Impact: No runtime impact.

Final Git Diff:
- Modified tracked files: `Assets/_Project/Scripts/Core/FixedCharBuffer.cs`, `Assets/_Project/Scripts/Core/ZeroGCFormatter.cs`
- Added untracked files: `Assets/_Project/Scripts/UI/DiegeticHudManualLayout.cs`, `Assets/_Project/Scripts/UI/DiegeticHudTextNode.cs`, `Assets/_Project/Scripts/UI/DiegeticPdaFocusDistanceController.cs`, `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs`, `Assets/_Project/Shaders/UI/Hecton_DiegeticVisorCurvedHUD.shader`, `Docs/AgentLogs/RECON_UI_DIEGETIC_HUD.md`, `Docs/AgentLogs/Rationale_UI_DIEGETIC_HUD.md`, `Docs/Tasks/Status_UI_DIEGETIC_HUD.md`
- Tracked diff stat: 2 files changed, 67 insertions, 42 deletions. Untracked additions are listed above and pending git add.

Unity Import Evidence:
- Requested `refresh_unity` with compile. The editor entered compiling state.
- Waiting for readiness timed out after 60 seconds.
- `read_console` was reachable and reported external errors in `SpectrumSystem`, `SaveBinaryStorage`, `CombatDamageRuntime`, `HectonVisorUberPostFeature`, `SargassumMicroFaunaBoids`, `AbyssalThermalManager`, `HectonArenaAllocator`, and `DroneFleetManager`.
- No console error referenced `DiegeticHudTextNode`, `DiegeticVisorHudMesh`, `DiegeticHudManualLayout`, `DiegeticPdaFocusDistanceController`, `ZeroGCFormatter`, `FixedCharBuffer`, or `Hecton_DiegeticVisorCurvedHUD.shader`.

## R&D PASS 4 HONEST AAA HARDENING

Problem: `DiegeticHudTextNode.SetOxygenPercent()` updated `_lastOxygenPercent` before proving the char-array commit succeeded.
Solution: Moved `_lastOxygenPercent` assignment behind successful `Commit(cursor)`.
Rejected Alternatives: Leaving the dirty flag optimistic was rejected because one failed commit could permanently suppress retry for the same O2 integer.
Scalability potential: Same low/high visual path, stronger failure recovery.
Hardware Impact: No measurable frame saving; removes correctness risk under missing TMP target or too-small buffer.

Problem: `DiegeticVisorHudMesh.OnDisable()` destroyed runtime mesh/material and disposed the black box, creating re-enable allocation churn for normal HUD visibility toggles.
Solution: Added `releaseRuntimeObjectsOnDisable` and `releaseBlackBoxOnDisable`, default false. Disable now unregisters signals/tick only; destroy still releases owned resources.
Rejected Alternatives: Always releasing on disable was rejected because AAA UI can be toggled many times during PDA/menu/helmet transitions. Never releasing on destroy was rejected as a leak.
Scalability potential: Low/MX350 avoid repeated cold allocations during visibility toggles. High/Ultra preserve expensive dense mesh/material state for instant reactivation.
Hardware Impact: i3/MX350 estimate: avoids one mesh, material, NativeArray, and vertex/index array rebuild per visor re-enable; about 80-140 us saved per toggle, pending profiler verification.

Problem: `DiegeticHudManualLayout.CollectDirectChildren()` allocated a new `Transform[]` on every enable.
Solution: Reuse the existing target buffer when direct child count is unchanged.
Rejected Alternatives: Reallocating every enable was rejected as avoidable managed churn. Persistent global arrays were rejected as unnecessary.
Scalability potential: Low and High both keep authoring convenience while avoiding repeated managed allocation on common toggles.
Hardware Impact: i3/MX350 estimate: saves one managed array allocation and about 3-10 us per re-enable depending child count.

Problem: `DiegeticPdaFocusDistanceController` could retry `GetComponentInChildren` / `GetComponentInParent` every armed frame while references were missing.
Solution: Added `_nextResolveFrame` 30-frame backoff for unresolved reference lookup and immediate retry reset when focus is activated.
Rejected Alternatives: Per-frame component lookup was rejected. One-shot lookup was rejected because late-spawned camera/volume would never bind.
Scalability potential: Low/MX350 avoid lookup spam under broken setup; High/Ultra still recover late-bound references.
Hardware Impact: i3/MX350 estimate: 8-20 us saved per armed frame while references are absent, pending profiler verification.

Problem: Compile wall status shifted again after R&D pass.
Solution: Re-ran the same `dotnet build` gate. It now fails outside UI with `HectonBoidController` missing `IAcousticPingEventListener.OnAcousticPing(in AcousticPingEvent)` and `VoxelDeltaProcessor` missing `SaveVoxelDeltaRun8`.
Rejected Alternatives: Editing fauna/voxel domain files was rejected under the domain boundary.
Scalability potential: UI remains decoupled and ready for verification once external compile blockers clear.
Hardware Impact: No runtime impact.

## R&D PASS 5 HONEST AAA HARDENING

Problem: `DiegeticVisorHudMesh.OnEnable()` still called `RebuildMesh()` unconditionally. Even with retained runtime mesh/material, re-enabling the visor rebuilt `Vector3[]`, `Vector2[]`, and `int[]` geometry arrays.
Solution: Added mesh geometry state tracking for tier, segment counts, distance, angular size, and curvature. `RebuildMesh()` now reattaches the existing mesh when geometry is unchanged.
Rejected Alternatives: Always rebuilding on enable was rejected because PDA/menu/helmet visibility transitions can be frequent. Per-frame adaptive mesh rebuild was rejected as visual overkill in the wrong place.
Scalability potential: Low/MX350 avoid repeated cold mesh allocations on toggles. High/Ultra keep dense visor geometry hot and instant to reactivate.
Hardware Impact: i3/MX350 estimate: avoids four managed geometry arrays and mesh clear/reassign work per unchanged visor re-enable; about 45-90 us saved per toggle, pending profiler verification.

Problem: `DiegeticHudManualLayout.OnDisable()` disposed persistent NativeArrays, turning common UI visibility toggles into allocator churn.
Solution: Added `releaseNativeStateOnDisable`, default false. Disable now retains layout lanes unless an author explicitly wants teardown; destroy still disposes.
Rejected Alternatives: Always disposing on disable was rejected because the layout is scene-owned and reused. Never disposing was rejected because destroy must release NativeArray ownership.
Scalability potential: Low/MX350 avoid repeated NativeArray allocation. High/Ultra can keep larger diegetic clusters hot without allocator pressure.
Hardware Impact: i3/MX350 estimate: saves two NativeArray allocations, two sentinel registrations, and 12-30 us per layout re-enable depending child count.

Problem: Black-box NaN handling could dump the same 300-frame buffer every tick while a bad state persisted.
Solution: Added `_blackBoxDumped` guard reset when the black box is recreated. First NaN still writes `Dump_UI_DIEGETIC_HUD.bin`; repeat frames do not create FileStream storms.
Rejected Alternatives: Disabling the dump was rejected because post-mortem evidence is mandatory. Dumping every bad frame was rejected because it can bury the real crash under I/O cost.
Scalability potential: Same telemetry fidelity for first failure on all tiers; low hardware avoids repeated disk I/O collapse.
Hardware Impact: No normal-frame saving. Under persistent NaN, prevents repeated FileStream/BinaryWriter cost after the first dump.

Problem: `ZeroGCFormatter.TryFormatFloat(value, precision)` used manual digit extraction through integer scaling. It risked overflow for large floats and conflicted with the mandate preference for `TryFormat`.
Solution: Replaced manual precision formatting with `float.TryFormat` using literal `F0` through `F6` span format lanes.
Rejected Alternatives: Manual `/` digit extraction and runtime format string construction were rejected. `string.Format` and interpolated strings remain banned.
Scalability potential: Same zero-GC contract across tiers with less fragile numeric behavior.
Hardware Impact: i3/MX350 estimate: no honest speed claim; improves correctness and mandate compliance while staying allocation-free.

Problem: Pass 5 verification hit a build process failure without useful compiler diagnostics.
Solution: Wrote build output to `Docs/AgentLogs/Build_UI_DIEGETIC_HUD_pass5.log`, reran Unity console read, and kept UI status at PENDING VERIFICATION.
Rejected Alternatives: Claiming compile success was rejected because `dotnet build` exited `-1`. Editing `NativeArenaArrayEditTests.cs` or `SaveBinaryStorage.cs` was rejected as non-UI domain work.
Scalability potential: UI remains isolated; verification can finish after external Burst/test compile faults are repaired.
Hardware Impact: No runtime impact.

## R&D PASS 6 HONEST AAA HARDENING

Problem: Pass 5 removed unchanged visor re-enable rebuilds, but any legitimate same-count mesh rebuild still allocated new managed geometry arrays.
Solution: Added retained `_vertices`, `_normals`, `_uv`, and `_indices` buffers in `DiegeticVisorHudMesh`, allocated only when vertex/index counts change and released with runtime mesh teardown.
Rejected Alternatives: Keeping local arrays in `RebuildMesh()` was rejected because scalability tier or projection changes can happen during runtime settings transitions. Switching to `List<T>` mesh APIs was rejected because it would add another managed container lane.
Scalability potential: Low/MX350 keep sparse retained buffers. High/Ultra can rebuild denser curved glass on quality changes without repeated same-count array churn.
Hardware Impact: i3/MX350 estimate: saves four managed array allocations on same-count mesh rebuilds; about 30-70 us per geometry refresh, pending profiler verification.

Problem: `DiegeticHudTextNode.OnEnable()` still called `TMP_TextRegistry.EnsureRegistered(target)`, whose fallback can `AddComponent<HectonTextNode>()` at runtime.
Solution: Added `HectonTextNode` as a required component and gated registration behind `target.TryGetComponent(out HectonTextNode _)`, removing the hidden runtime AddComponent path.
Rejected Alternatives: Allowing runtime AddComponent was rejected because UI registration should be authored/baked, not allocated during visor activation. Removing registry integration was rejected because font streaming still needs the text registry.
Scalability potential: Low/MX350 avoid component allocation on HUD activation. High/Ultra retain deterministic font-streaming ownership for richer visor text.
Hardware Impact: i3/MX350 estimate: avoids one component allocation and Awake/OnEnable registration cost for mis-authored text nodes; about 20-60 us during activation, pending profiler verification.

Problem: Pass 6 verification was partially blocked by Unity MCP instability and external compile errors.
Solution: Ran available validations, repeated forbidden scans, read Unity console after readiness returned, and preserved PENDING VERIFICATION.
Rejected Alternatives: Marking visor mesh validation green was rejected because the MCP command disconnected. Editing `UserOptionsPersistence.cs` missing `HectonPersistentPathPolicy` was rejected as Input domain work.
Scalability potential: UI pass remains isolated and can be fully verified once the external compile wall and MCP validation instability clear.
Hardware Impact: No runtime impact.

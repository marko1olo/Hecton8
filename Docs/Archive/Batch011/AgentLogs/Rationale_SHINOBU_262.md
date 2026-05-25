# Rationale_SHINOBU_262

Status: PENDING VERIFICATION

## Intake Decisions

Problem: Assignment can be corrupted by neighboring XML prompts.
Solution: Extracted only `<AGENT_PROMPT id="SHINOBU_262">` from `Docs/Tasks/CURRENT_BATCH.md` by CLI regex and discarded neighboring task context.
Rejected Alternatives: Basic file read or manual scroll; both risk truncation or neighboring prompt bleed.
Scalability potential: Preserves one-owner route discipline before render pipeline changes.
Hardware Impact: Avoids architectural drift that would reintroduce extra camera submissions on i3/MX350.

Problem: Status and rationale files were absent at session start.
Solution: Created fresh `Docs/Tasks/Status_SHINOBU_262.md` and `Docs/AgentLogs/Rationale_SHINOBU_262.md`.
Rejected Alternatives: Chat-only tracking; rejected by batch protocol and vulnerable to context compression.
Scalability potential: Disk state supports strict iterative loops without losing decisions.
Hardware Impact: None directly; prevents false reports and compile-wall amnesia.

## Architecture Decisions

Problem: Crest implements depth, reflection, and wake/depth probes by hidden Camera constructors in multiple packages.
Solution: Keep third-party package surface minimal and install first-party `OceanSinglePass` RenderGraph route for depth mask and wake texture while disabling camera constructors at each known Crest path.
Rejected Alternatives: Refactor Crest LOD stack; too broad, high dependency risk with 20+ parallel agents, and still carries hidden render-camera assumptions.
Scalability potential: Low tier uses downscaled wake target plus analytic foam; middle/high/ultra spend saved camera submissions on higher compute resolution and shader detail.
Hardware Impact: Removes extra scene submissions and RT churn; expected low-end i3/MX350 gain is hundreds to thousands of microseconds where previous hidden cameras rendered terrain/reflections.

Problem: Replacement ocean visuals need GPU parameters without `Shader.SetGlobalFloat` scatter.
Solution: Publish one 32-byte `OceanVisualOverridesDTO` from VISUAL_SYNC into a double-buffered `GraphicsBuffer.Target.Constant`, bound by RenderGraph.
Rejected Alternatives: Per-scalar globals; too many global writes and violates task 11.
Scalability potential: Continuous `GlobalQualityWeight` controls foam, shoreline fade, wake strength, and wake resolution without DTO shape changes.
Hardware Impact: One constant-buffer upload avoids managed global state churn; expected low-end savings are small per frame but removes sync-stall risk.

Problem: The first runtime draft published from a late-frame lane, not the mandated dispatcher owner phase.
Solution: `OceanSinglePassRuntime` now implements `IDispatcherSystem` and publishes CBuffer/wake/telemetry state from `DispatcherPhase.VisualSync`. VisualSync performs no cold service lookup and does not allocate missing graphics buffers.
Rejected Alternatives: Keep `ILateFrameTickable`; it is a looser lane and does not prove rollback/visual presentation fencing.
Scalability potential: VisualSync can be shed by dispatcher health pressure while preserving gameplay truth and rollback state.
Hardware Impact: Prevents wasted RenderGraph work during VisualSync suppression; expected low-end gain depends on health pressure but avoids full wake/depth presentation work in shed frames.

Problem: Crest depth cache and planar reflection source still contained dormant manual render and RenderTexture tokens.
Solution: Removed target `Camera.Render`, `RenderCameraWithoutCustomPasses`, and `new RenderTexture` call sites from `OceanDepthCache.cs` and `OceanPlanarReflection.cs`, and killed the default `BuildCommandBuffer` builder body.
Rejected Alternatives: Rely on constant guarded branches; static scanners and future merges could still resurrect forbidden camera paths.
Scalability potential: Low/mid/high/ultra tiers all share one primary camera route; quality scales inside the replacement RenderGraph/compute path only.
Hardware Impact: Removes extra depth/reflection submissions; estimated Quest/i3/MX350 savings are 3000-12000 us in ocean-heavy frames before profiler proof.

Problem: The replacement system needed human tuning without C# recompilation.
Solution: Added UI Toolkit `SinglePassOceanTunerWindow` with sliders for foam threshold, wake lifespan, shoreline fade, telemetry graph, mock state command, and live wake texture preview.
Rejected Alternatives: Inspector constants or static ScriptableObject edits; both slow iteration and do not prove live Vault route.
Scalability potential: Artists can tune low/mid/high/ultra response curves through continuous scalar lanes instead of binary quality assets.
Hardware Impact: No runtime frame impact; editor-only iteration time reduction.

Problem: Multi-camera eradication needed a proof artifact, not verbal reporting.
Solution: Added `Camera_Proliferation_Scanner` and `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`; scanner inspects prefabs and scene YAML and flags active non-UI camera counts above one.
Rejected Alternatives: Manual hierarchy inspection; non-repeatable and weak under 20+ parallel agents.
Scalability potential: Prevents regressions that would silently reintroduce full-scene submissions on mobile and VR.
Hardware Impact: Prevents each extra camera from consuming geometry, fill, shadow, and RT bandwidth; static report currently shows zero camera violations in scanned assets.

Problem: Build verification is required but local hardware gate forbids builds over 50 percent CPU.
Solution: Sampled CPU and dotnet/csc/VBCSCompiler processes before build. CPU was 74 percent, so compile is blocked by protocol. Static checks continued: scoped `git diff --check`, brace counts, forbidden token scans, and DTO layout tests added.
Rejected Alternatives: Launch `dotnet build` under CPU pressure; violates explicit command discipline and risks stealing cycles from other agents.
Scalability potential: Protects shared workstation throughput while preserving objective pending-proof state.
Hardware Impact: Avoids compile IO/CPU spike during already-loaded system state.

Problem: Crest kill switches were initially `const bool true`, and killed manual camera render blocks used `return` inside `try/finally`; under warnings-as-errors this can create unreachable-code compile noise.
Solution: Converted SHINOBU_262 Crest kill switches to `static readonly bool` runtime sentinels and replaced the killed render call bodies with explicit no-op comments inside the existing restoration `try/finally` blocks.
Rejected Alternatives: Keep compile-time constants; rejected because the disabled path is already runtime-dead and compile-wall hygiene matters more than constant-folding a cold safety branch.
Scalability potential: Maintains one-camera route across low/mid/high/ultra tiers while keeping future compile verification cleaner.
Hardware Impact: No frame-time regression; avoids build churn that would mask real RenderGraph compile failures.

Problem: Mock render-state originally wrote Vault state but the RenderGraph feature still rejected all non-Play-Mode frames, weakening Task 05 CI/blank-scene proof.
Solution: Added a cold 32-byte mock `GraphicsBuffer.Target.Constant`, `IsMockRenderStateActive()`, and `ConsumeMockRenderFrameBudget()` so the ocean feature can run a bounded editor-frame pass without Crest cameras or scene integration.
Rejected Alternatives: Instantiate a camera/plane harness in editor; rejected because the task forbids extra camera routes and because URP can execute the existing feature with a mock CBuffer.
Scalability potential: Low/mid/high/ultra runtime path remains unchanged; mock is editor/CI presentation proof only.
Hardware Impact: Runtime hot path unchanged; editor mock avoids full scene boot cost during isolated RenderGraph validation.

Problem: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` is a shared artifact and currently has SHINOBU_265 data at the root.
Solution: Preserved the existing root report and added the SHINOBU_262 camera-guillotine proof as `shinobu_262_camera_guillotine`; updated the scanner writer to preserve a shared root instead of blind overwrite.
Rejected Alternatives: Overwrite the file with SHINOBU_262-only JSON; rejected because it would destroy another agent's evidence artifact.
Scalability potential: Multiple rendering agents can coexist in the same report without erasing proof routes.
Hardware Impact: No runtime impact; avoids report churn and false regression archaeology.

Problem: The first SHINOBU_262 runtime files landed under the parent `Hecton8.Core` asmdef, which protects functionality but widens the compile wall for a rendering-only iteration.
Solution: Added `Hecton8.Rendering.OceanSinglePass.asmdef` with references limited to Core/Core.Contracts/Core.Memory, Unity Collections/Jobs/Mathematics/Burst, and URP/Core RenderGraph assemblies. Added explicit references from `Hecton8.Project.Editor` and `Hecton8.EditModeTests` so the tuner, validator, scanner, and edit tests can see the narrow runtime assembly.
Rejected Alternatives: Leave the files in `Hecton8.Core`; rejected because every ocean RenderGraph edit would unnecessarily perturb the core assembly scope. Create a broad rendering mega-asmdef; rejected because it would couple SHINOBU_262 to sibling rendering domains.
Scalability potential: No visual tier change; this is production throughput protection so low/mid/high/ultra render tuning can iterate without hitting the core compile wall.
Hardware Impact: Runtime frame impact is zero. Editor hardware impact is reduced recompilation scope after Unity regenerates projects; exact wall-clock savings require Unity compile proof when CPU gate opens.

Problem: Renderer assets did not yet serialize `HectonSinglePassOceanFeature`, so the RenderGraph code could exist without being executed by URP after import.
Solution: Added `SinglePassOceanRendererFeatureInstaller` with an initialize-on-load installer, manual menu entry, and build guard. It creates exactly one `HectonSinglePassOceanFeature` sub-asset per PC/PC High/Mobile/Quest renderer asset, binds the hidden depth shader and wake compute shader, keeps injection at `BeforeRenderingTransparents`, and forces `m_RequireDepthTexture` true on the URP assets.
Rejected Alternatives: Hand-edit renderer asset YAML without Unity-generated GUID/local IDs; rejected because new script assets do not have stable import GUIDs before Unity import. Leave setup manual; rejected because the pipeline could silently ship without the single-pass ocean route.
Scalability potential: Installer makes the same feature available across low, medium, high, and Quest renderers; continuous quality remains in runtime math instead of per-renderer binary feature presence.
Hardware Impact: Runtime work only exists when the feature is installed. It prevents fallback to Crest hidden camera routes and preserves the 3000-12000 us ocean-heavy frame saving estimate.

Problem: New Unity assets without `.meta` files would get machine-local GUIDs on import, destabilizing renderer feature type identity, shader import identity, and team merges.
Solution: Added `.meta` files for the SHINOBU_262 OceanSinglePass folder, runtime scripts, asmdef, hidden depth shader, wake compute shader, editor tools, installer, and edit test. GUIDs are explicitly scoped to the SHINOBU_262 asset set and verified by static search.
Rejected Alternatives: Let Unity auto-generate metas later; rejected because that creates nondeterministic GUIDs across machines. Generate metas for unrelated untracked Propwash/particle files; rejected because they are not SHINOBU_262 ownership.
Scalability potential: No visual tier change; preserves deterministic asset identity for renderer installation across low/mid/high/Quest renderer assets.
Hardware Impact: Runtime frame impact is zero. Editor impact is reduced import churn and fewer broken asset references during multi-agent merges.

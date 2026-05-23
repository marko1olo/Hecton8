# SHINOBU_348 Rationale

Status: POLISH_R19_EDITOR_TUNER_WRITE_FENCE_STATIC_PENDING_BUILD_GUARD
Evidence Class: STATIC_SOURCE until Unity import, Play Mode, Frame Debugger, GCMonitor, and profiler artifacts exist.

## Preflight Decision 001 - Screen-Space PDA Route

Problem: Wrist PDA prompt requires deleting World-Space Canvas dependence while preserving diegetic arm attachment and readable UI.
Solution: Use a RenderGraph fullscreen projection pass fed by explicit unmanaged PDA DTOs. The shader performs ray-plane UV reconstruction and glass distortion; CPU owns only camera-relative matrix staging.
Rejected Alternatives: World-Space Canvas and GraphicRaycaster paths rebuild/sort managed UI geometry and violate the task. Mesh-attached PDA quads still require geometry updates and material management.
Scalability potential: Low uses flat one-sample atlas lookup; Middle adds cheap refraction; High adds chromatic offset; Ultra spends saved CPU on stronger glass/salt/noise polish without changing gameplay truth.
Hardware Impact: Estimated low-end gain is avoiding managed Canvas rebuild and transform sorting on i3/MX350; exact microseconds are PENDING VERIFICATION until profiler capture.

## Preflight Decision 002 - Data Ownership

Problem: The task names GlobalDataVault ownership, but the existing source must be scanned before assuming concrete Vault APIs exist.
Solution: Implement isolated unmanaged DTO/job/rendering code and adapt to existing vault/registry contracts discovered by grep. If no stable Vault API exists, provide a compile-safe adapter boundary and document the missing integration as PENDING VERIFICATION.
Rejected Alternatives: Inventing a direct dependency on a non-existent Vault method or polling GlobalRegistry in the render pass. Both would violate global authority law under parallel agent execution.
Scalability potential: Low/Middle/High/Ultra are driven through one continuous GlobalQualityWeight float, not binary hardware switches.
Hardware Impact: Avoids hot-path scene lookup and managed heap traffic; exact gain PENDING VERIFICATION.

## Loop 12 Decision - ReadOnly Vault Accessor Hardening

Problem: Public/editor `TryGet*` PDA projector accessors could return mutable `NativeArray<T>` views through the legacy `TryReadHandle` route. That is not a hot allocation defect, but it weakens the Global Systems Doctrine because a consumer-side readback can accidentally mutate Vault-owned presentation rows.
Solution: Add `TryReadOnlyPdaProjectionVaultBuffer<T>` over `IDataVault.TryReadOnlyHandle<T>` and route `TryGetActivePdaProjectionTuning`, `TryGetActivePdaProjectionTelemetry`, the UI Toolkit graph, and the SceneView gizmo through `NativeArray<T>.ReadOnly`. Keep `TryReadHandle` only in `DumpPdaProjectionBlackBoxOnce`, where the fault path needs a raw read pointer for binary export. The profile table validator now requires all `64` configured rows before owner readiness/CSV ingestion accepts the lane.
Rejected Alternatives: Leaving public telemetry mutable was faster to write but violates accessor purity. Converting the fault dump to a managed copy was rejected because it would add allocation and defeat the raw 300-frame postmortem path. Accepting `PdaInterfaceProfileDTO[1]` was rejected because the prompt's atlas profile table is a 64-row unmanaged route.
Scalability potential: Low keeps the one-sample PDA atlas path; middle/high/ultra still scale only shader math and profile richness through `GlobalQualityWeight`. Read-only access does not change DTO layout, save identity, rollback identity, or shader ABI.
Hardware Impact: Runtime microsecond gain is not claimed. The concrete benefit is removing mutable consumer views and preventing malformed short profile rows without adding per-frame work. Guarded build was not launched because guard samples hit CPU `80%` with zero compiler processes, CPU `77%` with 8 compiler processes, then CPU `6%` with 7 compiler processes; compiler-process ban remained active.

## Loop 1 Decision 003 - Runtime Anchor

Problem: The batch prompt names `HectonUIRuntime_WristProjector.cs`, but repository archaeology found no `HectonUIRuntime`; the real wrist presentation owner is `Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs`.
Solution: Convert `WristHologramHudRuntime` to a partial class and add `WristHologramHudRuntime_PdaScreenProjector.cs` as the isolated projector shard. Hook only cold lifecycle and visual-sync calls.
Rejected Alternatives: A standalone `HectonPdaProjectorManager` would create a second update owner and invite scene lookup. Renaming/creating `HectonUIRuntime` would be false architecture.
Scalability potential: Low uses the same owner with one DTO row; Middle/High/Ultra add shader work only, not CPU owners.
Hardware Impact: Estimated i3/MX350 gain is removal of one potential registry tick owner and avoided transform/Canvas search; exact microseconds remain PENDING PROFILER.

## Loop 1 Decision 004 - Canvas Deletion Boundary

Problem: The task demands deletion of wrist World-Space Canvas components, but scans did not prove such a component in `Player.prefab` or scenes. The known `DiegeticPDAController` manages a physical tablet/RenderTexture route, not a confirmed wrist canvas.
Solution: Do not mutate prefab YAML blindly. Implement a static scanner/report and route wrist projection through RenderGraph. Leave legacy physical PDA scripts until a concrete forbidden wrist Canvas artifact is found.
Rejected Alternatives: Deleting `DiegeticPDAController` or prefab components based on naming would sabotage other PDA/physical panel ownership and risk broken serialized references.
Scalability potential: Low/Middle/High/Ultra all benefit from removing the wrist projection Canvas dependency without breaking unrelated physical screens.
Hardware Impact: Claimed savings for Canvas deletion are 0 us until a forbidden component is proven; projection path itself targets zero Canvas rebuild cost.

## Loop 1 Decision 005 - Vault ID Range

Problem: Initial candidate local IDs `73190..73198` collide with active SHINOBU_325 screen-space trauma decal ownership.
Solution: Reserve owner-local IDs `348730..348739` for SHINOBU_348 PDA projection buffers after focused scan found no matches.
Rejected Alternatives: Editing the central `BufferID` enum in a crowded core file or reusing `73190..73198`; both are unnecessary and collision-prone.
Scalability potential: Low uses state/tuning/telemetry rows only; Middle adds profiles; High/Ultra use the same ABI and richer shader CBuffer values.
Hardware Impact: Avoids core enum churn and DataVault type-hash collisions; estimated compile/integration risk reduction is material, microseconds not applicable.

## Loop 2 Decision 006 - Matrix Ownership

Problem: The PDA screen must follow the wrist without World-Space Canvas rebuilds or absolute-float drift.
Solution: Stage `PdaProjectionInputDTO` with `double3 WristAup` and `double3 CameraAup`; `CompilePdaMatricesJob` subtracts in double precision, then builds `float4x4 LocalToWorld` from localized meters and normalized wrist rotation.
Rejected Alternatives: Uploading `Transform.localToWorldMatrix`, Canvas transform hierarchy, or direct absolute float conversion. All fail AUP precision or rebuild UI geometry.
Scalability potential: Low/Middle/High/Ultra share one 80-byte `PdaStateDTO`; visual tiering happens in shader weights.
Hardware Impact: Estimated i3/MX350 gain is avoiding Canvas sorting/rebuild and avoiding large matrix arrays; exact profiler cost PENDING.

## Loop 2 Decision 007 - GPU Upload

Problem: PDA state must reach RenderGraph without `GraphicsBuffer.SetData`, per-frame material mutation, or CPU mesh upload.
Solution: Use double-buffered `GraphicsBuffer` objects, `LockBufferForWrite`, and direct mapped `UnsafeUtility.MemCpy` from Vault-backed DTO rows into GPU-visible write windows. RenderGraph imports the buffers and declares `UseBuffer(Read)`. Loop 6 removed the prior one-row upload jobs as a tiny-job violation.
Rejected Alternatives: `MaterialPropertyBlock` hot mutation for the projection path, `Graphics.DrawMeshInstanced`, `SetData` uploads, and one-element upload jobs. These retain CPU render-object management or job-wrapper overhead the task removes.
Scalability potential: Low writes one DTO and one CBuffer; Middle/High/Ultra only change scalar weights in the same CBuffer layout.
Hardware Impact: Estimated low-end gain is fewer driver sync points and no per-frame geometry upload; exact microseconds PENDING frame debugger/profiler.

## Loop 3 Decision 008 - Continuous Quality

Problem: The prompt asks to collapse refraction under thermal pressure, but global project law rejects binary quality switches.
Solution: Use `GlobalQualityWeight` as a continuous scalar for refraction amplitude, curvature, chromatic blend, and corruption noise. No `isLowEndHardware` branch was added.
Rejected Alternatives: `UseRefraction = step(0.5, GlobalQualityWeight)` as a hard branch. It would violate the non-binary quality mandate and create visual popping.
Scalability potential: Low remains readable flat atlas; Middle adds mild bend; High adds stronger glass; Ultra pushes chroma/curvature without changing the CPU or DTO ABI.
Hardware Impact: ALU savings are not claimed without shader assembly/profiler. The guaranteed gain is CPU-side Canvas elimination; GPU cost is PENDING CAPTURE.

## Loop 3 Decision 009 - Rollback Exclusion

Problem: Wrist screen pose and boot visuals are cosmetic and would poison deterministic rollback if hashed as gameplay truth.
Solution: Document `348730..348736` as presentation-only in `SHINOBU_348_SCREEN_SPACE_PDA_PROJECTOR_ROUTE_CARD.md` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
Rejected Alternatives: Adding `PdaStateDTO` to `StateRingBuffer`, save identity, or Merkle hashing. That would desync network resimulation from harmless wrist jitter.
Scalability potential: Low/Middle/High/Ultra can change visual pose/refraction with no gameplay truth impact.
Hardware Impact: Avoids deterministic hash work and rollback state bandwidth for 80-byte visual rows; exact network/frame savings PENDING integrator measurement.

## Loop 3 Decision 010 - Black Box and CSV

Problem: A projection fault must leave proof, and atlas tab profiles need deterministic cold loading without managed parsing.
Solution: Record 300 frames of `PdaProjectionTelemetryEntry`; dump raw bytes to `Dump_SHINOBU_348.bin` on non-finite input or >100 us compile. Parse `pda_interface_profiles.csv` using `ReadOnlySpan<byte>`, FNV-1a, and custom float parsing into unmanaged DTO rows.
Rejected Alternatives: `string.Split`, `float.Parse`, console-only error messages, or inspector-only atlas rects.
Scalability potential: Low uses fallback full-atlas profile; Middle/High/Ultra can select richer atlas subregions with the same lookup rows.
Hardware Impact: Runtime hot path avoids CSV/string allocations; dump I/O is fault-only. Exact microseconds saved PENDING GC/profiler capture.

## Loop 5 Decision 011 - RenderGraph Buffer Discipline and Shader Branch Audit

Problem: The first RenderGraph pass version imported buffers for resource declaration but also carried raw `GraphicsBuffer` fields in pass data; the HLSL fragment path also retained branch-like ternaries during finite/active checks.
Solution: Keep the imported `BufferHandle` objects as the pass-data source of truth and convert them to `GraphicsBuffer` only inside the render function. Later Loop 7 intentionally introduced uniform quality branches to shed texture taps; those branches are driven by a frame-level scalar and are not hardware-tier or pixel-divergent route switches.
Rejected Alternatives: Leaving raw side-channel buffers in pass data or relying on a branchless four-sample shader that wastes low-quality texture bandwidth. Both were easy to harden without ABI churn.
Scalability potential: Low uses one atlas sample; Middle/High/Ultra admit refraction/chroma taps through continuous `smoothstep` weights, with no DTO or authority-route change.
Hardware Impact: Avoids hidden RenderGraph dependency ambiguity and low-tier texture bandwidth waste. Exact GPU microseconds remain PENDING Frame Debugger/profiler.

## Loop 5 Decision 012 - Compile Guard

Problem: The prompt requires compile verification, but local policy forbids `dotnet build` when CPU is above 50% or another compiler is active.
Solution: Repeatedly sampled compiler processes and CPU. Final guard had no active `dotnet` or `csc`, but CPU was 100%, so compilation was not launched. Static verification artifacts were recorded instead.
Rejected Alternatives: Forcing a build under 100% CPU would violate the hardware-protection rule and risk starving other agents. Reporting compile success without a build would be false.
Scalability potential: No runtime scalability effect; protects the shared workstation from unnecessary compile-wall pressure.
Hardware Impact: Saves an unmeasured build spike on the user's machine. Runtime microseconds remain profiler-pending; static source expected savings are Canvas rebuild/sort removal, no per-frame mesh upload, and no managed UI text mutations in the projector path.

## Loop 6 Decision 013 - Read Accessor Purity

Problem: Public `TryGetActivePdaProjectionTuning` and `TryGetActivePdaProjectionTelemetry` used the mutation-capable Vault resolve route. The Global Authority rule says methods named `TryGet*`/`Read*` must be pure and must not publish, allocate, grow, complete, or mutate global state.
Solution: Added `TryReadPdaProjectionVaultBuffer` backed by `IDataVault.TryReadHandle` and routed public reads, editor gizmo reads, and black-box telemetry reads through it. Mutation paths such as tuning writes, seed, CSV ingest, matrix compile, and telemetry cost patch keep `TryResolveHandle` because they actually write owner-local rows.
Rejected Alternatives: Renaming read APIs to hide the impurity or leaving `TryResolveHandle` in `TryGet*` methods. Standard Unity inspector reads were also rejected because they would not produce the Vault proof route.
Scalability potential: Low/Middle/High/Ultra all use the same descriptor and row layout; quality changes only shader math and cadence-visible presentation, not read authority.
Hardware Impact: Estimated i3/MX350 gain is small per call, roughly 4 us risk/counter noise avoided in editor and render-feature read paths. Exact cost remains PENDING PROFILER.

## Loop 6 Decision 014 - One-Row Upload Job Removal

Problem: The previous GPU upload path used two one-element Burst jobs only to copy `PdaStateDTO=80B` and `PdaProjectionGlobalsDTO=64B` into mapped `GraphicsBuffer` memory. That obeyed the literal XML upload-job wording but violated the project-wide rejection of tiny same-frame jobs without profiler proof.
Solution: Kept the required matrix/math jobs, but replaced upload jobs with direct owner-phase `UnsafeUtility.MemCpy` between Vault/native DTO memory and `LockBufferForWrite` mappings. RenderGraph still consumes imported buffers; no `SetData`, material clone, or MPB route was added.
Rejected Alternatives: `GraphicsBuffer.SetData`, `MaterialPropertyBlock`, scheduling the copy jobs, or leaving `.Run()` wrappers for a single cache-line transfer. The DOD path is direct native copy because the work is smaller than job setup.
Scalability potential: Low writes the same two rows with minimal CPU overhead; Middle/High/Ultra only change scalar values in the 64B globals row, so there is no ABI or upload path fork.
Hardware Impact: Estimated low-end saving is 3-8 us by avoiding job wrapper overhead and scheduler bookkeeping for two tiny copies. Exact microseconds remain PENDING PROFILER.

## Loop 6 Decision 015 - Depth-Aware Screen-Space Lie

Problem: The first shader ray-plane implementation projected the PDA atlas correctly but could render through opaque scene geometry because the depth texture was bound but not used. That weakens the "screen-space projector" claim and makes the optical lie visually brittle.
Solution: The fragment shader samples `_CameraDepthTexture`, linearizes scene depth, computes the PDA hit eye depth from the camera-relative ray-plane intersection, and multiplies the inside mask by a branchless `smoothstep(sceneEyeDepth - planeEyeDepth)`. This preserves the no-Canvas route while making real geometry occlude the wrist projection softly.
Rejected Alternatives: Physics raycasts, World-Space Canvas sorting, mesh clipping planes, or CPU-side depth readback. All are heavier than one screen-space depth sample and a few ALU ops.
Scalability potential: Low keeps one depth sample and flat atlas sample; Middle increases continuous refraction; High adds chroma/curvature; Ultra spends more ALU on glass without changing state ownership.
Hardware Impact: Cost increases by one depth texture read per PDA-covered pixel but avoids CPU physics/UI rebuilds. On MX350/i3 the expected CPU win remains Canvas rebuild/sort removal; GPU cost needs Frame Debugger/Profiler capture.

## Loop 6 Decision 016 - Post-Patch Compile Guard

Problem: The polish patch needs compile verification, but the workstation guard explicitly forbids launching build while CPU is above 50% or another compiler is active.
Solution: Ran focused static scans and sampled the guard. Static checks passed; build was not launched because `csc` PID 30004 and `dotnet` PID 27128 were active and CPU sampled at 85%.
Rejected Alternatives: Starting a second build anyway, or claiming compile proof from static source. Both would violate the batch instructions and contaminate integrator evidence.
Scalability potential: No runtime quality impact; this protects shared iteration velocity for the 20+ concurrent agent environment.
Hardware Impact: Avoids extra compiler CPU/IO pressure on the user's machine. Runtime microsecond claims remain STATIC ESTIMATES until Unity profiler capture.

## Loop 7 Decision 017 - Subagent Assembly Finding Adjudication

Problem: Subagent reported that `PdaProjectionTunerWindow` could not see runtime projector types from `Hecton8.UI.Editor`.
Solution: Audited asmdef hierarchy. Runtime UI files under `Assets/_Project/Scripts/UI` are compiled by parent `Hecton8.Core.asmdef`; `Hecton8.UI.Editor.asmdef` already references `Hecton8.Core`. The tuner remains in `Assets/_Project/Scripts/UI/Editor`, preserving editor isolation.
Rejected Alternatives: Moving the EditorWindow into the Core/runtime assembly under `#if UNITY_EDITOR`. That would compile but worsen the compile wall by making editor-tool edits dirty the large Core assembly.
Scalability potential: No visual quality effect; protects iteration speed across low-end and high-end developer machines.
Hardware Impact: Runtime 0 us. Compile-wall risk reduced by keeping editor-only tooling in the editor asmdef.

## Loop 7 Decision 018 - Cold Ownership And CSV Repair

Problem: Default atlas profiles set `_pdaProjectionProfilesLoaded = true`, preventing the authored CSV parser from ever running. Late-frame visual sync also called cold Vault/GPU ensure methods that could allocate or resolve generation handles.
Solution: Split `_pdaProjectionDefaultProfilesSeeded` from `_pdaProjectionProfilesLoaded`; defaults now seed once and CSV remains loadable. Late-frame now checks `_pdaProjectionNativeBuffersReady` and `_pdaProjectionGraphicsBuffersReady` only. Cold setup and DataVault service replacement own handle creation. `OnDisable` releases PDA `GraphicsBuffer` resources.
Rejected Alternatives: Polling CSV every frame, calling `EnsureGenerationHandle` from late frame, or retaining VRAM until destroy. Those paths hide I/O/allocation/driver costs in visual sync.
Scalability potential: Low/Middle/High/Ultra all share the same Vault rows; authored atlas profiles improve art control without changing gameplay truth.
Hardware Impact: Prevents unpredictable late-frame stalls and VRAM retention. Exact frame-time savings are PENDING PROFILER.

## Loop 7 Decision 019 - Uniform Shader Math LOD

Problem: Previous shader quality scaling dimmed refraction but still paid four PDA atlas samples at low quality. That failed the low-tier cost-collapse requirement.
Solution: Use uniform branches driven by continuous `GlobalQualityWeight`: below `0.20`, one direct atlas sample; `0.20..0.36` smooth refraction admission; `0.52..0.88` smooth chromatic tap admission. The branch condition is a uniform quality scalar, not a hardware-class switch, and visual transitions are smooth.
Rejected Alternatives: A branchless four-sample path, shader variants/multi_compile, or a binary low-end hardware flag. The branchless path wasted texture bandwidth; variants risk shader warmup/stutter; hardware flags violate the quality continuum.
Scalability potential: Low is readable flat atlas; Middle adds one refracted sample; High/Ultra add chroma taps and stronger glass. DTO layout, save identity, rollback status, and authority route remain unchanged.
Hardware Impact: Low tier skips three atlas texture samples per covered PDA pixel versus the previous path. GPU microseconds remain PENDING Frame Debugger/profiler.

## Loop 7 Decision 020 - Scanner Proof Narrowing

Problem: Broad source scanning can miss neutral file names, but naive case-insensitive `Pda` text matching falsely matched `Updatable`.
Solution: The scanner now covers all project source/YAML but counts only path-scoped PDA/Wrist files or local context around World-Space Canvas hits with exact `PDA`/`PlayerPDA` or case-insensitive `Wrist`/`WristOS`. The rendering report is merged into the shared JSON instead of overwriting sibling agents' entries.
Rejected Alternatives: Reporting zero from a UI/Player-only scan, or counting every project World-Space Canvas. The first is weak evidence; the second claims ownership outside SHINOBU_348.
Scalability potential: Proof tooling only; no runtime quality impact.
Hardware Impact: Runtime 0 us. Editor scanner remains cold proof tooling; shell equivalent currently reports zero scoped source hits and zero scoped YAML hits.

## Loop 8 Decision 021 - Truth Extraction Repair

Problem: The previous exact XML extractor looked for `<AGENT_PROMPT id="SHINOBU_348">` and failed because `CURRENT_BATCH.md` now includes role/chat attributes on the same tag.
Solution: Re-ran CLI extraction with `<AGENT_PROMPT id="SHINOBU_348"[\\s\\S]*?</AGENT_PROMPT>`, confirmed `TaskCount=20`, and printed Task 01 through Task 20 names from the live file.
Rejected Alternatives: Trusting stale chat state or reading the first prompt in the file, which currently belongs to SHINOBU_300 and would corrupt the domain.
Scalability potential: No runtime effect; prevents wrong-agent work under context compression.
Hardware Impact: Runtime 0 us. Reduces rework and compile-wall risk from accidental neighboring-domain edits.

## Loop 8 Decision 022 - Vault CSV Scratch

Problem: The PDA CSV parser previously borrowed `_csvReadBuffer`, a managed byte array owned by the legacy wrist HUD font metrics route, even though SHINOBU_348 already had Vault scratch `348736`.
Solution: Read `pda_interface_profiles.csv` directly into `NativeArray<byte>` scratch through `FileStream.Read(Span<byte>)`; parse `ReadOnlySpan<byte>` over the unmanaged pointer and write `PdaInterfaceProfileDTO[64]`.
Rejected Alternatives: Reusing the legacy managed byte array, calling `File.ReadAllBytes`, or parsing into strings. Those routes either create a shadow scratch owner or allocate.
Scalability potential: Low/Middle/High/Ultra share the same profile ABI; art can author atlas rects without C# recompiles.
Hardware Impact: Removes a managed scratch dependency and uses the full 16KB Vault budget. Runtime hot path remains unaffected because this is cold boot/profile load.

## Loop 8 Decision 023 - PDA Shader Boot Warmup

Problem: `Hecton_PdaScreen.shader` was asset-referenced by the renderer feature, but no boot shader variant collection warmed it before first gameplay use.
Solution: Added `Hecton_PdaScreen_Warmup.shadervariants` with the no-keyword pass for shader GUID `0d75901ecc6a479385541da8be342394` and serialized it into `00_BOOTSTRAP.unity` `shaderVariantCollections`, using the existing `GameBootstrapper.WarmConfiguredShaderVariantCollectionsAsync` route.
Rejected Alternatives: Runtime `Shader.Find`, feature-owned `ShaderVariantCollection.WarmUp()`, shader multi_compile variants, or accepting first-use compilation. All add stutter risk or runtime ownership.
Scalability potential: No gameplay truth impact; makes the PDA projection shader resident before mobile/VR first use and lets high-tier visuals enter without first-frame hitch.
Hardware Impact: Prevents a first-use shader compile hitch. Exact milliseconds saved require Unity import/player capture.

## Loop 8 Decision 024 - Owned Report Sidecar

Problem: The shared rendering report was overwritten by other agents after SHINOBU_348 inserted its proof object.
Solution: `OOP_Canvas_Scanner_SHINOBU_348` now writes an owned sidecar `RENDERING_OPTIMIZATION_REPORT_SHINOBU_348.json` and also merges the same object into the shared report.
Rejected Alternatives: Relying only on shared JSON in a 20+ agent workspace, or replacing the shared file wholesale.
Scalability potential: Proof tooling only.
Hardware Impact: Runtime 0 us; improves audit durability.

## Loop 8 Decision 025 - Fresh Compile Guard

Problem: Loop 8 changed runtime/editor/shader/bootstrap assets and needs compile/import verification, but the local build rule forbids launching when CPU exceeds 50% or active compiler processes exist.
Solution: Run static checks and sample the workstation guard before any build. The latest guard reported CPU 52% and 7 active `dotnet` processes, so no build was launched.
Rejected Alternatives: Starting a second build under compiler load, or claiming compile proof from static checks. Both are false evidence and damage the shared workstation.
Scalability potential: No runtime quality effect; preserves iteration capacity for the 20+ agent environment.
Hardware Impact: Avoided extra compiler CPU/IO pressure while another build/import lane is active. Runtime microsecond claims remain static estimates until Unity import/profiler capture.

## Loop 8 Decision 026 - Long Clamp Compile Risk

Problem: The Vault CSV scratch reader used `math.min/math.max` with `long` operands while compile verification is blocked by the workstation guard.
Solution: Replace the clamp with explicit `long` branch bounds before casting to `int`, preserving zero-GC cold I/O while removing dependency on Unity.Mathematics overload availability.
Rejected Alternatives: Keeping the overload assumption until compile time, or switching back to managed `File.ReadAllBytes`.
Scalability potential: No visual quality effect; keeps authored atlas CSV loading available across all tiers.
Hardware Impact: Runtime hot path 0 us. Reduces future compile-wall churn once the guard allows an import/build.

## Loop 9 Decision 027 - CSV Tab Hash Route Bridge

Problem: `PDAEventPayload.CurrentTab` drives the runtime projector through `ResolvePdaTabHash(int)`, but the CSV profile parser hashed authored names with FNV-1a. That made rows such as `Tab_Inventory` or `Logbook_Tab` unable to match the active tab route.
Solution: Route numeric `tab_#`/`pda_tab_#` tokens and canonical PDA tab names through the same `ResolvePdaTabHash(int)` function used by PDA events. Preserve FNV-1a only for unknown future authored tabs. Change profile lookup to prefer exact matches before using a `TabHashID=0` default row.
Rejected Alternatives: Keeping an FNV-only CSV identity, requiring designers to hand-author opaque integer hashes, or treating the first default row as an unconditional match. Those paths either break the human tuning bridge or shadow specific profiles.
Scalability potential: Low can keep one default atlas rect; Middle/High/Ultra can use specific atlas subregions without recompiling or changing DTO layout.
Hardware Impact: Hot path remains one bounded 64-row profile scan. CSV parsing stays cold and zero-GC; no per-frame allocations or scene searches are introduced.

## Loop 10 Decision 028 - View-Space Ray-Plane Repair

Problem: Subagent audit found that the shader mixed a camera-relative PDA matrix with `UNITY_MATRIX_I_VP` / `_WorldSpaceCameraPos` world ray reconstruction. At large AUP distances, subtracting absolute floats in the shader can drift from the CPU-localized matrix and break depth occlusion.
Solution: Keep `PdaStateDTO.LocalToWorld` as camera-relative world axes from the Burst AUP subtraction job, then convert its center and basis to view space with the rotational part of `UNITY_MATRIX_V`. Build the pixel ray from `UNITY_MATRIX_I_P` in view space and compare PDA hit depth as `-hit.z` against `LinearEyeDepth`.
Rejected Alternatives: Uploading absolute world-space PDA matrices, adding a second CPU view-matrix DTO, or reading depth/physics on CPU. Absolute world matrices violate AUP precision; a second DTO changes the mandated ABI; CPU depth/physics destroys the Dear Lie.
Scalability potential: Low/Middle/High/Ultra all use the same 80-byte state row and 64-byte globals row. Quality still only controls shader sample/tap admission, not coordinate authority.
Hardware Impact: Runtime ALU is effectively neutral: inverse VP plus world-camera subtraction becomes inverse projection plus view rotation. The important gain is precision stability on far-origin mobile/VR frames without adding CPU work.

## Loop 10 Decision 029 - Fresh Build Guard After Space Repair

Problem: The shader and CSV bridge need compile/import verification after the subagent P2 fix, but the workstation guard remains explicit: no rebuild while CPU exceeds 50% or compiler processes are active.
Solution: Ran static scans, JSON parse, focused `git diff --check`, and trailing-whitespace scan. Sampled CPU/compiler guard afterward: first sample CPU 82% with active `csc` PID 30120 and `dotnet` PIDs 27468/27604; latest sample CPU 83% with no compiler processes. No build was launched because CPU still exceeds the 50% guard.
Rejected Alternatives: Starting a build under an active compiler load, or claiming green compile from static source. Both are false evidence.
Scalability potential: No runtime quality effect; protects concurrent agent iteration capacity.
Hardware Impact: Avoided extra compiler CPU/IO pressure. Runtime profiler and Frame Debugger proof remain pending until the guard clears.

## Loop 11 Decision 030 - Stale Proof Correction

Problem: Earlier status/log proof claimed a zero-branch shader path and mislabeled `PdaProjectionGlobalsDTO` as an inverse-view-projection matrix after later loops deliberately changed shader quality LOD and finalized the 64-byte globals row.
Solution: Correct the status wording to distinguish uniform quality branches from forbidden hardware-tier/pixel-divergent branches, and correct the self-audit globals layout to `ScreenParams`, `RefractionParams`, `AtlasRect`, and `VisualParams` at 16-byte offsets.
Rejected Alternatives: Leaving contradictory audit records and relying on the newest appended report to override stale proof. That is not evidence-grade documentation.
Scalability potential: Documentation-only; it preserves the intended low/middle/high/ultra quality explanation without changing runtime.
Hardware Impact: Runtime 0 us. Integration risk reduced because the audit no longer points reviewers toward a non-existent inverse-view-projection CBuffer.

## Loop 11 Decision 031 - Static Verification Under CPU Guard

Problem: R11 proof edits needed a fresh evidence pass, but the workstation guard still forbids compile/import verification while CPU load exceeds 50%.
Solution: Re-ran stale-proof text scan, focused owned runtime/render banned-call scan, and owned/shared rendering JSON parse. Sampled the build guard afterward: CPU 100%, zero active compiler processes. No build was launched because CPU alone violates the rule.
Rejected Alternatives: Starting a build just because no compiler process was active, or upgrading static scan results into compile proof. Both would create false evidence and risk contention with the shared workstation.
Scalability potential: No runtime visual-tier change; this keeps documentation aligned with the continuous quality path already implemented in shader.
Hardware Impact: Runtime 0 us. Compile-wall pressure avoided under saturated CPU; profiler, Frame Debugger, Unity Console, and import proof remain pending until the guard clears.

## Loop 13 Decision 032 - URP Renderer Activation

Problem: Kepler found the RenderGraph feature class was not serialized into active URP renderer assets. That makes the pass compile-time present but render-time inert, so the World-Space Canvas replacement route is false in player cameras.
Solution: Add one active `WristPdaScreenProjectorFeature` object to each active renderer asset: `PC_Renderer`, `PC_High_Renderer`, `Mobile_Renderer`, and `Quest_VR_Renderer`. Insert each before `HectonVisorUberPostFeature`, bind the serialized `Hecton_PdaScreen.shader`, and regenerate each `m_RendererFeatureMap` from the local feature list as little-endian signed 64-bit fileIDs. Update route card, ledger, reports, and scanner source so future proof generation preserves the activation evidence.
Rejected Alternatives: Runtime renderer injection was rejected because it would add managed boot mutation and import-order ambiguity. Enabling only PC was rejected because Mobile/Quest are first-class targets. Editing only `m_RendererFeatures` without the map was rejected because URP renderer assets use both serialized lists.
Scalability potential: Low/Mobile/Quest still use the same pass and collapse shader taps through `GlobalQualityWeight`; PC High keeps the same ABI and spends quality on richer glass/chroma. Activation does not create a hardware fork or change DTO layout, save identity, rollback route, or authority ownership.
Hardware Impact: Runtime microsecond gain is not claimed. The hard benefit is eliminating an inert render route. Static verifier reports map/list parity for all four renderer assets. The first post-patch build guard blocked at CPU `100%`; the later guarded build probe is recorded in Decision 033.

## Loop 13 Decision 033 - Guarded Build Probe External Wall

Problem: After renderer activation and proof updates, static source verification was not enough once the workstation guard cleared. The project still requires an honest compile probe when allowed.
Solution: Launch one guarded `dotnet build .\Hecton8.Core.csproj --no-restore --nologo -m:1 /nr:false -clp:ErrorsOnly` only after the command sampled CPU `41%` and zero `dotnet`/`csc`/`VBCSCompiler`/`MSBuild` processes. The build failed with two external Construction errors: `HatchLockJobs.cs(12,45)` and `BulkheadContainmentRuntime_HatchLocks.cs(15,45)` cannot resolve namespace `Hecton8.Habitat`.
Rejected Alternatives: Rebuilding repeatedly, editing generated project files, or touching Construction/Habitat ownership from the UI projector lane. Those would violate the compile-wall and domain-boundary rules.
Scalability potential: No runtime quality impact. SHINOBU_348 activation remains static-verified; compile proof is blocked by an external namespace dependency outside the wrist PDA projector domain.
Hardware Impact: Build consumed one guarded no-restore single-node probe and stopped with external errors. No SHINOBU_348 diagnostics were emitted before the external wall.

## Loop 14 Decision 034 - Physical PDA Atlas CSV Source

Problem: The runtime cold parser and Vault scratch route for `pda_interface_profiles.csv` existed, but the repository did not contain the actual CSV source. That made Task 17 depend on a future hand-created file and left CI/art tuning without a deterministic fallback input.
Solution: Add repo-root `pda_interface_profiles.csv` with one default atlas rect plus canonical PDA tab rows. Update the route card, binary payload ledger, owned/shared rendering reports, and scanner builder so the proof chain records a physical source file, not only parser capability.
Rejected Alternatives: Keeping only seeded in-code defaults was rejected because it bypasses the requested human-readable CSV bridge. Moving the file into `Assets` was rejected because the existing cold loader resolves project-root `pda_interface_profiles.csv`; changing that path would be an ABI/authoring-route change outside this repair.
Scalability potential: Low can still use the default full-atlas row. Middle/High/Ultra can bind authored atlas subregions per tab without C# recompilation or DTO layout changes. Quality remains a shader scalar and does not alter tab identity, save identity, or authority routing.
Hardware Impact: Hot path 0 us. The parser remains cold and streams bytes into Vault scratch `348736` with `ReadOnlySpan<byte>`, avoiding `File.ReadAllBytes`, `string.Split`, and `float.Parse`.

## Loop 14 Decision 035 - CSV Proof Verification Under Guard

Problem: The CSV source repair needed evidence without violating the explicit shared-workstation compile guard.
Solution: Validate the CSV shape, parse owned/shared JSON reports, scan the route card/ledger/report/scanner proof chain, run a focused forbidden hot-path scan over the owned runtime/render/shader files, and run focused `git diff --check`. Sample the build guard after static verification.
Rejected Alternatives: Running `dotnet build` while the CPU was saturated and seven `dotnet` processes were active, or describing static checks as compile proof. Both would be false process evidence.
Scalability potential: No runtime quality change. The physical CSV strengthens the Low/Middle/High/Ultra atlas authoring route while preserving the same shader-side continuous `GlobalQualityWeight` curve.
Hardware Impact: Hot path remains 0 us. Compile-wall pressure avoided because guard sampled CPU `100%` and `7` active `dotnet` processes. Static scans found no `SetData`, `MaterialPropertyBlock`, `.Complete()`, `TryGetLatestCreated`, `Camera.main`, `FindObject`, `Shader.Find`, `new Material`, `_csvReadBuffer`, absolute world shader reconstruction, or managed CSV parser calls in the owned projector hot path.

## Loop 15 Decision 036 - Camera-Relative Gizmo Proof Repair

Problem: `PdaProjectorOnDrawGizmosSelected()` read `PdaStateDTO.LocalToWorld` and submitted it directly to `Gizmos.matrix`. That DTO is intentionally camera-relative after the AUP subtraction job, so the editor proof box could appear near the origin or at an offset unrelated to the active camera when inspecting far-origin scenes.
Solution: Resolve the render camera before drawing, add `camera.transform.position` to the DTO translation for the SceneView gizmo matrix only, and preserve the camera-relative orientation axes and runtime DTO route unchanged. The yellow proof ray now draws from the resolved camera position to the converted world center.
Rejected Alternatives: Mutating `PdaStateDTO` into absolute world space was rejected because the shader and AUP route rely on camera-relative data. Adding a second debug matrix row was rejected because it would expand the Vault/shader proof surface for an editor-only visualization. Leaving the stale gizmo was rejected because Task 18 would produce misleading evidence away from origin.
Scalability potential: No runtime quality route changes. Low/Middle/High/Ultra still scale only shader samples/refraction through continuous `GlobalQualityWeight`; the editor gizmo now visualizes the same camera-relative fact without changing DTO layout, save identity, rollback boundary, or authority route.
Hardware Impact: Runtime hot path 0 us. Editor-only work is one camera resolve already used by the runtime owner plus vector addition and one matrix column write during selected-gizmo drawing.

## Loop 15 Decision 037 - Subagent P1/P2 Hardening

Problem: External static audit found three production-grade risks: uninitialized profile/telemetry rows could be read after cold fallback or early crash, repo-root CSV was not packaged for player builds, and mock/forced visibility serialized true by default could keep the PDA route active outside real wrist/PDA state. It also found atlas chroma taps could bleed into neighboring atlas rects and scanner summaries would overclaim success if future findings appear.
Solution: Clear all profile rows before seeding default row 0, clear the telemetry ring at cold seed, upgrade dump header to 64-byte version 2 with valid-count/start-index fields, and write telemetry rows oldest-to-newest. Add packaged `Assets/StreamingAssets/Hecton8/PDA/pda_interface_profiles.csv` with Unity meta and make the loader prefer that path, using repo-root CSV only in editor/development. Change mock/forced-visible defaults to false and compile mock input acceptance to editor/development builds. Clamp high-quality chroma samples inside the active atlas rect. Make scanner summary conditional on finding count.
Rejected Alternatives: Leaving uninitialized rows because the current fallback usually hits row 0 was rejected; undefined flags are not evidence. Loading player CSV only from repo root was rejected because packaged builds cannot rely on workspace files. Keeping mock true was rejected as a production no-op risk. Adding a second atlas texture or shader variant for chroma clamping was rejected because a rect clamp solves the bleed without variant/stutter cost.
Scalability potential: Low remains one direct atlas sample and the PDA pass stays inactive when closed. Middle/High/Ultra keep the same DTO/shader ABI, but high-quality chroma no longer leaks into adjacent authored tabs. The packaged CSV strengthens art tuning without changing gameplay truth, authority, save identity, or rollback route.
Hardware Impact: Low-end player builds avoid paying the fullscreen PDA path from mock/force defaults when the PDA is closed. Profile/telemetry clearing is cold-only. Fault dump ordering is fault-only. Chroma clamp adds a few ALU ops only inside the high-quality chroma branch.

## Loop 16 Decision 038 - StreamingAssets URI Boundary

Problem: Loop 15 correctly packaged `Assets/StreamingAssets/Hecton8/PDA/pda_interface_profiles.csv`, but the proof text overreached by treating it as an unrestricted player-runtime readable source. On Android/Quest, `Application.streamingAssetsPath` can be URI-backed inside the APK. A direct `FileStream` route cannot hydrate that path, and `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent, so a player-runtime DataMonolith profile claim would be false.
Solution: `ResolvePdaProfileCsvPath()` now accepts the packaged CSV only when `Application.streamingAssetsPath` is a direct filesystem path. URI-backed StreamingAssets targets fail closed to the deterministic default profile row already seeded and zero-cleared by the owner. The route card, binary payload ledger, scanner report builder, and owned/shared rendering reports now state direct-file scope, Android/Quest fail-closed behavior, and no SHINOBU_348 `static_data.h8bin` readiness claim.
Rejected Alternatives: `UnityWebRequest` cold staging was rejected because it adds a new managed asset route and download-buffer behavior outside the task's DataVault CSV bridge. Claiming DataMonolith readiness without `static_data.h8bin` plus import/bake/boot validation was rejected as false evidence. Falling back to repo-root CSV in production was rejected because packaged players cannot depend on workspace files.
Scalability potential: Low/Middle/High/Ultra keep the same `PdaInterfaceProfileDTO[64]` ABI and the same continuous shader quality curve. Direct-file platforms can hydrate authored atlas rows; URI-backed mobile/Quest remains visually stable through the default full-atlas row until the binary import lane exists. Quality never changes tab identity, save identity, rollback boundary, or authority route.
Hardware Impact: Runtime hot path 0 us. The only code branch is cold profile resolution. Mobile avoids a failed direct file open on URI-backed StreamingAssets and avoids introducing `UnityWebRequest`/managed staging into gameplay startup. Exact platform proof remains pending Unity Android/Quest player build.

## Loop 17 Decision 039 - Mobile Graphics Capability And Dump Path Boundary

Problem: `Hecton_PdaScreen.shader` declares `#pragma target 4.5` and reads `StructuredBuffer<PdaStateDTO>`, but runtime graphics setup only checked `SystemInfo.supportsSetConstantBuffer`. That could leave mobile/Quest renderer assets active on a graphics API that cannot support the shader/buffer ABI. The fault dump path also wrote only under project-root `Docs/AgentLogs`, which is an Editor proof location, not a writable Android/Quest player path.
Solution: `EnsurePdaProjectionGraphicsBuffers()` now cold-gates PDA graphics allocation with `SystemInfo.supportsSetConstantBuffer && SystemInfo.graphicsShaderLevel >= 45`; unsupported targets release any PDA buffers, mark the GPU payload invalid, and fail closed through the existing `TryGetActivePdaProjectionResources()` path. `DumpPdaProjectionBlackBoxOnce()` now resolves the dump directory to project `Docs/AgentLogs` only in Editor; player builds use `Application.persistentDataPath/Hecton8/AgentLogs`.
Rejected Alternatives: A mobile shader fallback was rejected because it would create a second shader ABI and a likely Canvas/material fallback path without import proof. Keeping only `supportsSetConstantBuffer` was rejected because it does not prove SM4.5/StructuredBuffer support. Writing player dumps to project `Docs` was rejected because packaged players do not have that writable workspace route.
Scalability potential: Low/Middle/High/Ultra visual scaling remains inside the same `GlobalQualityWeight` shader curve when the GPU route is supported. Capability failure is not a quality tier; it is a platform feasibility boundary that fails closed without changing DTO layout, save identity, rollback boundary, or authority route.
Hardware Impact: Supported devices keep the same hot path. Unsupported mobile/GLES-era devices avoid invalid graphics buffer allocation and render-pass submission. Fault dump path remains fault-only; player path correctness improves without per-frame work. Static verification passed; guarded rebuild was not launched because CPU sampled `77%` with `7` active `dotnet` processes. Exact Android/Quest proof remains pending Unity player build.

## Loop 18 Decision 040 - Late-Frame Telemetry Resolve Removal

Problem: `PdaProjectorLateFrameTick()` already resolves the telemetry ring and cursor as part of the owner write phase, but `PatchPdaProjectionTelemetryJobCost()` re-resolved those same Vault handles immediately after the matrix kernel ran. That is redundant hot-path metadata work and weakens the "open once, mutate phase-local views" route.
Solution: Pass the already-opened `NativeArray<PdaProjectionTelemetryEntry>` and `NativeArray<int>` cursor into `PatchPdaProjectionTelemetryJobCost()`. The helper is now static, validates only `IsCreated`/length, and writes the latest row directly.
Rejected Alternatives: Keeping the second resolve because it is small was rejected; it still violates the owner-phase locality goal. Scheduling a separate telemetry patch job was rejected because this is one row of bookkeeping and would reintroduce tiny-job overhead.
Scalability potential: Low/Middle/High/Ultra visuals are unchanged. Continuous quality remains shader-side; the projector simply spends less owner-phase CPU metadata work whenever the PDA is visible.
Hardware Impact: Removes two generation-checked Vault handle resolutions from active PDA projection frames. Exact microseconds require profiler capture; the expected gain is small but deterministic and more important for low-end CPU headroom than for desktop. Guarded build was not launched after the patch because CPU sampled `68.2%` with `7` active `dotnet` processes.

## Loop 19 Decision 041 - Editor-Only Tuning Writer Fence

Problem: `TrySetActivePdaProjectionTuning()` is a writer that resolves the mutable tuning row. Its only caller is the UI Toolkit editor tuner, but the method was compiled into player builds as a public static mutation bridge.
Solution: Wrap only the writer in `#if UNITY_EDITOR`. Keep read-only tuning/telemetry accessors and render resource query available because they are pure/read-only and feed editor diagnostics or RenderGraph.
Rejected Alternatives: Leaving the player public writer in place was rejected because production should not expose a designer-only mutable Vault surface. Wrapping all accessors was rejected because the render feature still needs the resource query and read-only diagnostics are not mutation authority.
Scalability potential: Low/Middle/High/Ultra visual quality is unchanged. This is an authority-surface reduction, not a quality switch.
Hardware Impact: Runtime hot path 0 us. Player builds drop one unused public mutation method; Editor keeps the live tuning bridge for designers without C# recompilation. Guarded build was not launched after this fence because CPU sampled `44.2%` but `7` active `dotnet` processes kept the compiler-process ban closed.
